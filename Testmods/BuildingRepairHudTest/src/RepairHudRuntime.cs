using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using BepInEx;

namespace BuildingRepairHudTest
{
    internal sealed unsafe class RepairHudRuntime
    {
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int CanRepairRva = 0xD51A0;
        private const int WoodCostOffset = 0x31B824;
        private const int StoneCostOffset = 0x31B828;
        private const string ButtonName = "BuildingRepairHudTestButton";

        private static readonly byte[] CanRepairEntry =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0x1D, 0xE3, 0x31, 0x71, 0x06
        };

        private delegate EngineInterface.PlayState CopyStateDelegate(
            EngineInterface.PlayStateReturnData source, int[] selectedChimps);
        private delegate bool ShowRepairDelegate(HUD_Buildings self, int type, int panel);
        private delegate void HudUpdateDelegate(FatControler self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong NativeCanRepairDelegate();

        private static readonly ConditionalWeakTable<EngineInterface.PlayState, RepairSnapshot> Snapshots =
            new ConditionalWeakTable<EngineInterface.PlayState, RepairSnapshot>();

        private readonly NativeCanRepairDelegate nativeCanRepair;
        private readonly Hook copyHook;
        private readonly Hook classifierHook;
        private readonly Hook hudHook;
        private readonly CopyStateDelegate originalCopy;
        private readonly ShowRepairDelegate originalShowRepair;
        private readonly HudUpdateDelegate originalHudUpdate;
        private bool postStartupLogged;
        private HUD_Buildings lastHud;

        private RepairHudRuntime(CrusaderLibraryLoadContext context)
        {
            ValidateNative(context);
            nativeCanRepair = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<NativeCanRepairDelegate>(
                IntPtr.Add(context.ModuleHandle, CanRepairRva));

            Hook candidateCopy = null;
            Hook candidateClassifier = null;
            Hook candidateHud = null;
            try
            {
                candidateCopy = new Hook(FindMethod(typeof(EngineInterface), "CopyPlayStateStruct",
                    BindingFlags.Public | BindingFlags.Static,
                    typeof(EngineInterface.PlayStateReturnData), typeof(int[])),
                    (CopyStateDelegate)CopyStateHook);
                originalCopy = candidateCopy.GenerateTrampoline<CopyStateDelegate>();

                candidateClassifier = new Hook(FindMethod(typeof(HUD_Buildings), "GetBuildingShowRepair",
                    BindingFlags.Public | BindingFlags.Instance, typeof(int), typeof(int)),
                    (ShowRepairDelegate)ShowRepairHook);
                originalShowRepair = candidateClassifier.GenerateTrampoline<ShowRepairDelegate>();

                candidateHud = new Hook(FindMethod(typeof(FatControler), "NoesisGUIUpdateChecksInGame",
                    BindingFlags.Public | BindingFlags.Instance),
                    (HudUpdateDelegate)HudUpdateHook);
                originalHudUpdate = candidateHud.GenerateTrampoline<HudUpdateDelegate>();

                copyHook = candidateCopy;
                classifierHook = candidateClassifier;
                hudHook = candidateHud;
            }
            catch
            {
                // Roll back only this unpublished initialization candidate.
                candidateHud?.Undo();
                candidateHud?.Dispose();
                candidateClassifier?.Undo();
                candidateClassifier?.Dispose();
                candidateCopy?.Undo();
                candidateCopy?.Dispose();
                throw;
            }
        }

        internal static RepairHudRuntime Install(CrusaderLibraryLoadContext context) => new RepairHudRuntime(context);

        private static MethodInfo FindMethod(Type owner, string name, BindingFlags flags, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, flags, null, parameters, null);
            if (method == null) throw new MissingMethodException(owner.FullName, name);
            return method;
        }

        private static void ValidateNative(CrusaderLibraryLoadContext context)
        {
            string path = System.IO.Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data",
                "Plugins", "x86_64", "CrusaderDE.dll");
            if (!File.Exists(path)) throw new FileNotFoundException("Installed native game DLL is unavailable.", path);
            string actual;
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
                actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
            if (!string.Equals(actual, NativeHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Native game hash differs from the audited repair contract: " + actual);
            if (context.ModuleHandle == IntPtr.Zero || context.Memory.Length < CanRepairRva + CanRepairEntry.Length)
                throw new InvalidOperationException("Native repair function is outside the loaded module.");
            for (int i = 0; i < CanRepairEntry.Length; i++)
                if (context.Memory[CanRepairRva + i] != CanRepairEntry[i])
                    throw new InvalidOperationException("Native repair function entry differs from the audited bytes.");
        }

        private EngineInterface.PlayState CopyStateHook(
            EngineInterface.PlayStateReturnData source, int[] selectedChimps)
        {
            EngineInterface.PlayState state = originalCopy(source, selectedChimps);
            // CopyPlayStateStruct runs inside EngineInterface.run's threadLock immediately after DLL_RunTick.
            try
            {
                RepairSnapshot snapshot = FillVanillaRepairState(state);
                if (snapshot != null) Snapshots.Add(state, snapshot);
            }
            catch (Exception ex)
            {
                BuildingRepairHudTestPlugin.LogError("Native repair-state sample failed: " + ex);
            }
            return state;
        }

        private RepairSnapshot FillVanillaRepairState(EngineInterface.PlayState state)
        {
            if (state == null || !IsBuildingHud(state) || state.in_structure <= 0)
                return null;

            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            if (!api.TryGetBuildingById(state.in_structure, out GameBuilding* building) || building == null)
                return null;
            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            eStructs type = building->r_BuildingType;
            if (localPlayerId <= 0 || building->r_PlayerIdOwner != localPlayerId ||
                building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0 ||
                building->r_MaxHealth == 0 || building->r_MaxHealth > short.MaxValue ||
                building->r_CurrentHealth < 0 || building->r_CurrentHealth > building->r_MaxHealth ||
                type <= eStructs.STRUCT_NULL || type > eStructs.STRUCT_GARDEN_LARGE ||
                state.in_structure_type != (int)type)
                return null;

            bool allowed = nativeCanRepair() != 0;
            IntPtr manager = (IntPtr)api.GetBuildingManager().Pointer;
            if (manager == IntPtr.Zero) return null;
            int wood = System.Runtime.InteropServices.Marshal.ReadInt32(manager, WoodCostOffset);
            int stone = System.Runtime.InteropServices.Marshal.ReadInt32(manager, StoneCostOffset);
            if (wood < 0 || stone < 0) return null;

            state.repair_wood_needed = wood;
            state.repair_stone_needed = stone;
            state.building_hps_for_repair = building->r_CurrentHealth;
            state.building_maxhps_for_repair = (short)building->r_MaxHealth;
            state.can_do_repairs = (short)(allowed ? 1 : 0);
            return new RepairSnapshot(state.in_structure, state.app_sub_mode);
        }

        private static bool IsBuildingHud(EngineInterface.PlayState state)
        {
            if (state.app_mode != (int)Enums.AppModes.APP_MODE_IN_BUILDING) return false;
            int panel = state.app_sub_mode;
            if (panel == (int)Enums.InBuildingModes.INSIDE_PEACETIME ||
                panel == (int)Enums.InBuildingModes.INSIDE_CHIMP ||
                (panel >= (int)Enums.InBuildingModes.SUB_MODE_REPORTS_ARMY4 &&
                 panel <= (int)Enums.InBuildingModes.SUB_MODE_REPORTS_RELIGION)) return false;
            return panel >= (int)Enums.InBuildingModes.INSIDE_BARRACKS &&
                panel <= (int)Enums.InBuildingModes.INSIDE_CATHEDRAL;
        }

        private bool ShowRepairHook(HUD_Buildings self, int type, int panel)
        {
            bool vanilla = originalShowRepair(self, type, panel);
            // BugfixesAndQoL queries panel zero for other owned buildings; actual HUD panels stay Vanilla.
            return vanilla || (panel == (int)Enums.InBuildingModes.INSIDE_NULL &&
                type > (int)eStructs.STRUCT_NULL && type <= (int)eStructs.STRUCT_GARDEN_LARGE);
        }

        private void HudUpdateHook(FatControler self)
        {
            originalHudUpdate(self);
            try { UpdateButton(); }
            catch (Exception ex) { BuildingRepairHudTestPlugin.LogError("Repair HUD update failed: " + ex); }
        }

        private void UpdateButton()
        {
            if (!MainViewModel.viewModelLoaded) return;
            MainViewModel view = MainViewModel.Instance;
            if (view?.HUDmain == null || view.HUDBuildingPanel == null) return;
            HUD_Buildings hud = view.HUDBuildingPanel;
            if (!postStartupLogged || !ReferenceEquals(hud, lastHud))
            {
                BuildingRepairHudTestPlugin.LogInfo("Repair HUD runtime executed after startup cleanup; HUD instance=" + hud.GetHashCode() + ".");
                postStartupLogged = true;
                lastHud = hud;
            }

            Button button = hud.FindName(ButtonName) as Button;
            if (button == null) return;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            RepairSnapshot snapshot;
            bool valid = state != null && Snapshots.TryGetValue(state, out snapshot) &&
                snapshot.BuildingId == state.in_structure && snapshot.Panel == state.app_sub_mode &&
                IsBuildingHud(state);
            if (!valid)
            {
                button.Visibility = Visibility.Hidden;
                button.IsEnabled = false;
                return;
            }

            bool special = hud.RefBarracksPanel.Visibility == Visibility.Visible ||
                hud.RefMercPostPanel.Visibility == Visibility.Visible ||
                hud.RefBedouinStockadePanel.Visibility == Visibility.Visible;
            if (special)
            {
                button.Width = 20;
                button.Height = 20;
                button.Margin = new Thickness(0, 0, 235, 27);
            }
            else
            {
                button.Width = 24;
                button.Height = 24;
                button.Margin = new Thickness(0, 0, 262, 44);
            }
            button.Visibility = Visibility.Visible;
            button.IsEnabled = hud.RefButtonRepair.IsEnabled;
            button.Opacity = button.IsEnabled ? 1.0f : 0.5f;
        }

        private sealed class RepairSnapshot
        {
            internal readonly int BuildingId;
            internal readonly int Panel;
            internal RepairSnapshot(int buildingId, int panel)
            {
                BuildingId = buildingId;
                Panel = panel;
            }
        }
    }
}
