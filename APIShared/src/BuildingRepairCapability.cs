using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Marshal = System.Runtime.InteropServices.Marshal;

namespace APIShared
{
    internal sealed unsafe class BuildingRepairService
    {
        private const int CanRepairRva = 0xD51A0;
        private const int WoodCostOffset = 0x31B824;
        private const int StoneCostOffset = 0x31B828;
        private const string BigButtonId = "APIShared.VanillaRepairButton";
        private static readonly byte[] CanRepairEntry =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08, 0x57, 0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0x1D, 0xE3, 0x31, 0x71, 0x06
        };

        private delegate EngineInterface.PlayState CopyStateDelegate(
            EngineInterface.PlayStateReturnData source, int[] selectedChimps);
        private delegate void HudUpdateDelegate(FatControler self);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ulong NativeCanRepairDelegate();

        private sealed class PendingRepair
        {
            internal int PlayerId;
            internal int BuildingId;
            internal int GlobalId;
            internal int PreviousHealth;
            internal int Iron;
            internal int Pitch;
            internal int Gold;
        }

        [ThreadStatic] private static PendingRepair pendingRepair;

        private readonly object ownersSync = new object();
        private readonly object errorSync = new object();
        private readonly HashSet<string> seenErrors = new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> pendingErrors = new Queue<string>();
        private readonly Dictionary<string, OwnerView> owners = new Dictionary<string, OwnerView>(StringComparer.Ordinal);
        private readonly ConditionalWeakTable<EngineInterface.PlayState, BuildingRepairQuote> snapshots =
            new ConditionalWeakTable<EngineInterface.PlayState, BuildingRepairQuote>();
        private readonly RepairTooltipViewModel tooltip = new RepairTooltipViewModel();
        private readonly ManualLogSource log;
        private readonly NativeCanRepairDelegate nativeCanRepair;
        private Hook copyHook;
        private Hook hudHook;
        private CopyStateDelegate originalCopy;
        private HudUpdateDelegate originalHudUpdate;
        private IDisposable repairSubscription;
        private int activeOwners;
        private string hoveredButton;
        private Button lastBigButton;
        private bool postStartupLogged;
        private BuildingRepairQuote firstSample;
        private bool firstSampleLogged;

        private BuildingRepairService(long moduleBase, ManualLogSource logger)
        {
            log = logger;
            nativeCanRepair = Marshal.GetDelegateForFunctionPointer<NativeCanRepairDelegate>(
                new IntPtr(checked(moduleBase + CanRepairRva)));
        }

        internal static bool TryCreate(string hash, long moduleBase, ManualLogSource log,
            out BuildingRepairService service, out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            if (!string.Equals(hash, ApiSharedRuntime.SupportedHash, StringComparison.OrdinalIgnoreCase) ||
                moduleBase == 0)
            {
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.BuildingRepair,
                    NativeCapabilityState.UnsupportedBuild, hash, "The installed native repair build is not verified.");
                return false;
            }

            Hook candidateCopy = null;
            Hook candidateHud = null;
            IDisposable candidateSubscription = null;
            try
            {
                byte[] actual = new byte[CanRepairEntry.Length];
                Marshal.Copy(new IntPtr(checked(moduleBase + CanRepairRva)), actual, 0, actual.Length);
                for (int index = 0; index < actual.Length; index++)
                    if (actual[index] != CanRepairEntry[index])
                        throw new InvalidOperationException("Native repair entry does not match the audited function.");

                BuildingRepairService candidate = new BuildingRepairService(moduleBase, log);
                MethodInfo copyTarget = FindMethod(typeof(EngineInterface), "CopyPlayStateStruct",
                    BindingFlags.Public | BindingFlags.Static,
                    typeof(EngineInterface.PlayStateReturnData), typeof(int[]));
                MethodInfo hudTarget = FindMethod(typeof(FatControler), "NoesisGUIUpdateChecksInGame",
                    BindingFlags.Public | BindingFlags.Instance);
                candidateCopy = new Hook(copyTarget, (CopyStateDelegate)candidate.CopyStateHook);
                candidate.originalCopy = candidateCopy.GenerateTrampoline<CopyStateDelegate>();
                candidateHud = new Hook(hudTarget, (HudUpdateDelegate)candidate.HudUpdateHook);
                candidate.originalHudUpdate = candidateHud.GenerateTrampoline<HudUpdateDelegate>();
                GameXAMLManagerAPI.Instance.RegisterBinding("APISharedRepairTooltipHost", candidate.tooltip);
                candidateSubscription = BuildingR3EventHooks.OnBuildingRepair.Observable.Subscribe(candidate.OnBuildingRepair);
                candidate.copyHook = candidateCopy;
                candidate.hudHook = candidateHud;
                candidate.repairSubscription = candidateSubscription;
                service = candidate;
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.BuildingRepair,
                    NativeCapabilityState.Available, hash, "Verified Vanilla repair path and shared repair handlers installed.");
                NativeApiLog.Info(log, $"Building repair capability installed: build={hash}, resolution=reference-rva, nativeRva=0x{CanRepairRva:X}, managedHooks=CopyPlayStateStruct/NoesisGUIUpdateChecksInGame.");
                return true;
            }
            catch (Exception ex)
            {
                // These candidates have not been published to the process-wide API.
                try { candidateSubscription?.Dispose(); } catch { }
                try { candidateHud?.Undo(); } catch { }
                try { candidateHud?.Dispose(); } catch { }
                try { candidateCopy?.Undo(); } catch { }
                try { candidateCopy?.Dispose(); } catch { }
                diagnostic = new NativeCapabilityDiagnostic(NativeCapabilityIds.BuildingRepair,
                    NativeCapabilityState.ValidationFailed, hash, ex.Message);
                NativeApiLog.Error(log, $"Building repair capability unavailable: {ex}");
                return false;
            }
        }

        private static MethodInfo FindMethod(Type owner, string name, BindingFlags flags, params Type[] parameters)
        {
            MethodInfo method = owner.GetMethod(name, flags, null, parameters, null);
            if (method == null) throw new MissingMethodException(owner.FullName, name);
            return method;
        }

        internal IBuildingRepairCapability Bind(string ownerGuid)
        {
            lock (ownersSync)
            {
                if (owners.TryGetValue(ownerGuid, out OwnerView existing)) return existing;
                var view = new OwnerView(this, ownerGuid);
                owners.Add(ownerGuid, view);
                Interlocked.Increment(ref activeOwners);
                return view;
            }
        }

        private void SetOwnerActive(OwnerView view, bool active)
        {
            lock (ownersSync)
            {
                if (view.Active == active) return;
                view.Active = active;
                if (active) Interlocked.Increment(ref activeOwners);
                else if (Interlocked.Decrement(ref activeOwners) == 0) hoveredButton = null;
            }
        }

        private EngineInterface.PlayState CopyStateHook(
            EngineInterface.PlayStateReturnData source, int[] selectedChimps)
        {
            EngineInterface.PlayState state = originalCopy(source, selectedChimps);
            if (Volatile.Read(ref activeOwners) == 0) return state;
            // EngineInterface.run holds threadLock around DLL_RunTick and this copy operation.
            try
            {
                BuildingRepairQuote quote = CaptureSelectedQuote(state);
                if (quote != null)
                {
                    snapshots.Remove(state);
                    snapshots.Add(state, quote);
                    Interlocked.CompareExchange(ref firstSample, quote, null);
                }
            }
            catch (Exception ex)
            {
                RecordError("Building repair snapshot failed: " + ex);
            }
            return state;
        }

        private BuildingRepairQuote CaptureSelectedQuote(EngineInterface.PlayState state)
        {
            if (state == null || !IsBuildingHud(state) || state.in_structure <= 0) return null;
            GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (buildings == null || players == null ||
                !buildings.TryGetBuildingById(state.in_structure, out GameBuilding* building) || building == null)
                return null;
            int playerId = players.GetLocalPlayerId();
            eStructs type = building->r_BuildingType;
            if (playerId <= 0 || building->r_PlayerIdOwner != playerId ||
                building->r_AliveState != AliveState.IsAlive || building->r_GlobalId == 0 ||
                building->r_MaxHealth == 0 || building->r_MaxHealth > short.MaxValue ||
                building->r_CurrentHealth < 0 || building->r_CurrentHealth > building->r_MaxHealth ||
                type <= eStructs.STRUCT_NULL || type > eStructs.STRUCT_GARDEN_LARGE ||
                state.in_structure_type != (int)type)
                return null;

            bool allowed = nativeCanRepair() != 0;
            IntPtr manager = (IntPtr)buildings.GetBuildingManager().Pointer;
            if (manager == IntPtr.Zero) return null;
            int wood = Marshal.ReadInt32(manager, WoodCostOffset);
            int stone = Marshal.ReadInt32(manager, StoneCostOffset);
            int baseIron = buildings.GetIronIngotCost(type);
            int basePitch = buildings.GetRawPitchCost(type);
            int baseGold = buildings.GetGoldCost(type);
            if (wood < 0 || stone < 0 || baseIron < 0 || basePitch < 0 || baseGold < 0)
                return null;
            int iron = RepairCost(baseIron, building->r_CurrentHealth, building->r_MaxHealth);
            int pitch = RepairCost(basePitch, building->r_CurrentHealth, building->r_MaxHealth);
            int gold = RepairCost(baseGold, building->r_CurrentHealth, building->r_MaxHealth);

            state.repair_wood_needed = wood;
            state.repair_stone_needed = stone;
            state.building_hps_for_repair = building->r_CurrentHealth;
            state.building_maxhps_for_repair = (short)building->r_MaxHealth;
            state.can_do_repairs = (short)(allowed ? 1 : 0);
            return new BuildingRepairQuote(state.in_structure, unchecked((int)building->r_GlobalId),
                state.app_sub_mode, allowed, building->r_CurrentHealth, building->r_MaxHealth,
                wood, stone, iron, pitch, gold,
                AvailableForTooltip(state, players, playerId, eGoods.STORED_WOOD_PLANKS),
                AvailableForTooltip(state, players, playerId, eGoods.STORED_STONE_BLOCKS),
                AvailableForTooltip(state, players, playerId, eGoods.STORED_IRON_INGOTS),
                AvailableForTooltip(state, players, playerId, eGoods.STORED_PITCH_RAW),
                AvailableForTooltip(state, players, playerId, eGoods.STORED_GOLD));
        }

        private static int AvailableForTooltip(EngineInterface.PlayState state,
            GamePlayerManagerAPI players, int playerId, eGoods good)
        {
            int index = (int)good;
            bool useKeep = state.game_type == (int)Enums.eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER &&
                (good == eGoods.STORED_WOOD_PLANKS || good == eGoods.STORED_STONE_BLOCKS ||
                 good == eGoods.STORED_GOLD);
            int[] values = useKeep ? state.keep_storage : state.resources;
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : players.GetGoodAmount(playerId, good);
        }

        private static int RepairCost(int baseCost, int health, int maxHealth)
        {
            if (baseCost < 0 || health < 0 || maxHealth <= 0 || health >= maxHealth) return 0;
            if (baseCost == 0) return 0;
            long amount = (long)(maxHealth - health) * baseCost / maxHealth;
            return (int)Math.Min(int.MaxValue, Math.Max(1L, amount));
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

        private void OnBuildingRepair(BuildingRepairEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                pendingRepair = null;
                if (Volatile.Read(ref activeOwners) == 0 || args.SkipOriginalFunction) return;
                try
                {
                    GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                    GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                    if (buildings == null || players == null || args.BuildingId <= 0 ||
                        !players.TryGetPlayerResourcesById(args.PlayerId, out GamePlayerResources* resources) ||
                        !buildings.TryGetBuildingById(args.BuildingId, out GameBuilding* building) || building == null ||
                        building->r_PlayerIdOwner != args.PlayerId ||
                        unchecked((int)building->r_GlobalId) != args.BuildingGlobalId ||
                        building->r_AliveState != AliveState.IsAlive || building->r_MaxHealth == 0 ||
                        building->r_MaxHealth > short.MaxValue || building->r_CurrentHealth < 0 ||
                        building->r_BuildingType <= eStructs.STRUCT_NULL ||
                        building->r_BuildingType > eStructs.STRUCT_GARDEN_LARGE)
                    {
                        args.SkipOriginalFunction = true;
                        RecordError("Building repair event had an invalid building identity or state; native repair skipped.");
                        return;
                    }
                    if (building->r_CurrentHealth >= building->r_MaxHealth) return;

                    eStructs type = building->r_BuildingType;
                    int baseIron = buildings.GetIronIngotCost(type);
                    int basePitch = buildings.GetRawPitchCost(type);
                    int baseGold = buildings.GetGoldCost(type);
                    if (baseIron < 0 || basePitch < 0 || baseGold < 0)
                    {
                        args.SkipOriginalFunction = true;
                        RecordError("Building repair has an invalid negative construction cost; native repair skipped.");
                        return;
                    }
                    int iron = RepairCost(baseIron, building->r_CurrentHealth, building->r_MaxHealth);
                    int pitch = RepairCost(basePitch, building->r_CurrentHealth, building->r_MaxHealth);
                    int gold = RepairCost(baseGold, building->r_CurrentHealth, building->r_MaxHealth);
                    if (!players.HasGoodsAmount(args.PlayerId, eGoods.STORED_IRON_INGOTS, iron) ||
                        !players.HasGoodsAmount(args.PlayerId, eGoods.STORED_PITCH_RAW, pitch) ||
                        !players.HasGoodsAmount(args.PlayerId, eGoods.STORED_GOLD, gold))
                    {
                        args.SkipOriginalFunction = true;
                        return;
                    }

                    pendingRepair = new PendingRepair
                    {
                        PlayerId = args.PlayerId,
                        BuildingId = args.BuildingId,
                        GlobalId = args.BuildingGlobalId,
                        PreviousHealth = building->r_CurrentHealth,
                        Iron = iron,
                        Pitch = pitch,
                        Gold = gold
                    };
                }
                catch (Exception ex)
                {
                    args.SkipOriginalFunction = true;
                    RecordError("Building repair cost check failed closed: " + ex);
                }
                return;
            }

            if (args.Phase != EventHookPhase.Post) return;
            PendingRepair pending = pendingRepair;
            pendingRepair = null;
            if (pending == null || pending.PlayerId != args.PlayerId || pending.BuildingId != args.BuildingId ||
                pending.GlobalId != args.BuildingGlobalId) return;
            try
            {
                GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                if (!buildings.TryGetBuildingById(args.BuildingId, out GameBuilding* building) || building == null ||
                    unchecked((int)building->r_GlobalId) != pending.GlobalId ||
                    pending.PreviousHealth >= building->r_MaxHealth ||
                    building->r_CurrentHealth != building->r_MaxHealth) return;
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (pending.Iron > 0) players.RemoveGood(args.PlayerId, eGoods.STORED_IRON_INGOTS, pending.Iron);
                if (pending.Pitch > 0) players.RemoveGood(args.PlayerId, eGoods.STORED_PITCH_RAW, pending.Pitch);
                if (pending.Gold > 0 && !players.AddPlayerGold(args.PlayerId, -pending.Gold))
                    RecordError($"Building repair gold charge could not resolve player {args.PlayerId}.");
            }
            catch (Exception ex)
            {
                RecordError("Building repair additional cost charge failed: " + ex);
            }
        }

        private void RecordError(string message)
        {
            lock (errorSync)
            {
                if (seenErrors.Add(message)) pendingErrors.Enqueue(message);
            }
        }

        private void FlushErrors()
        {
            lock (errorSync)
            {
                while (pendingErrors.Count > 0)
                    NativeApiLog.Error(log, pendingErrors.Dequeue());
            }
        }

        private bool TryGetSelectedQuote(out BuildingRepairQuote quote)
        {
            quote = null;
            if (Volatile.Read(ref activeOwners) == 0) return false;
            EngineInterface.PlayState state = GameData.Instance?.lastGameState;
            if (state == null || !IsBuildingHud(state) ||
                !snapshots.TryGetValue(state, out BuildingRepairQuote stored) ||
                stored.BuildingId != state.in_structure || stored.Panel != state.app_sub_mode)
                return false;
            quote = stored;
            return true;
        }

        private void HudUpdateHook(FatControler self)
        {
            originalHudUpdate(self);
            try
            {
                FlushErrors();
                if (Volatile.Read(ref activeOwners) == 0) { tooltip.Clear(); return; }
                if (!MainViewModel.viewModelLoaded) return;
                MainViewModel view = MainViewModel.Instance;
                if (view?.HUDmain == null || view.HUDBuildingPanel == null) return;
                Button big = view.HUDBuildingPanel.RefButtonRepair;
                if (big != null && !ReferenceEquals(big, lastBigButton))
                {
                    big.MouseEnter += OnBigButtonEnter;
                    big.MouseLeave += OnBigButtonLeave;
                    lastBigButton = big;
                }
                if (!postStartupLogged)
                {
                    postStartupLogged = true;
                    NativeApiLog.Info(log, "Building repair capability executed after startup cleanup on the persistent HUD hook.");
                }
                BuildingRepairQuote sampled = Volatile.Read(ref firstSample);
                if (!firstSampleLogged && sampled != null)
                {
                    firstSampleLogged = true;
                    NativeApiLog.Info(log, $"Building repair field map: woodOffset=0x{WoodCostOffset:X}, stoneOffset=0x{StoneCostOffset:X}, buildingId={sampled.BuildingId}, globalId={sampled.BuildingGlobalId}, hp={sampled.CurrentHealth}/{sampled.MaxHealth}, wood={sampled.Wood}, stone={sampled.Stone}, canRepair={sampled.CanRepair}.");
                }
                if (hoveredButton == BigButtonId && (lastBigButton == null || !lastBigButton.IsMouseOver))
                    hoveredButton = null;
                RefreshTooltip();
            }
            catch (Exception ex)
            {
                tooltip.Clear();
                RecordError("Building repair HUD refresh failed: " + ex);
            }
        }

        private void OnBigButtonEnter(object sender, MouseEventArgs args) => BeginHover(BigButtonId);
        private void OnBigButtonLeave(object sender, MouseEventArgs args) => EndHover(BigButtonId);

        private void BeginHover(string buttonId)
        {
            if (string.IsNullOrEmpty(buttonId)) return;
            hoveredButton = buttonId;
            try { RefreshTooltip(); }
            catch (Exception ex) { RecordError("Building repair hover failed: " + ex); tooltip.Clear(); }
        }

        private void EndHover(string buttonId)
        {
            if (!string.Equals(hoveredButton, buttonId, StringComparison.Ordinal)) return;
            hoveredButton = null;
            try { tooltip.Clear(); }
            catch (Exception ex) { RecordError("Building repair hover cleanup failed: " + ex); }
        }

        private void RefreshTooltip()
        {
            if (hoveredButton == null || !TryGetSelectedQuote(out BuildingRepairQuote quote) ||
                quote.CurrentHealth >= quote.MaxHealth)
            {
                tooltip.Clear();
                return;
            }
            tooltip.Show(quote);
        }

        private sealed class OwnerView : IBuildingRepairCapability
        {
            private readonly BuildingRepairService service;
            internal bool Active = true;
            internal OwnerView(BuildingRepairService service, string ownerGuid)
            {
                this.service = service;
                OwnerGuid = ownerGuid;
            }
            internal string OwnerGuid { get; }
            public void SetActive(bool active) => service.SetOwnerActive(this, active);
            public bool TryGetSelectedQuote(out BuildingRepairQuote quote)
            {
                if (Active) return service.TryGetSelectedQuote(out quote);
                quote = null;
                return false;
            }
            public void BeginHover(string buttonId) { if (Active) service.BeginHover(buttonId); }
            public void EndHover(string buttonId) => service.EndHover(buttonId);
        }
    }

    /// <summary>One resource displayed by the shared repair tooltip.</summary>
    public sealed class RepairTooltipEntry
    {
        /// <summary>Required resource amount.</summary>
        public string Required { get; set; }
        /// <summary>Available resource amount.</summary>
        public string Available { get; set; }
        /// <summary>Vanilla goods icon.</summary>
        public ImageSource Icon { get; set; }
    }

    /// <summary>Noesis binding source for the shared repair tooltip.</summary>
    public sealed class RepairTooltipViewModel : INotifyPropertyChanged
    {
        private Noesis.Visibility visibility = Noesis.Visibility.Hidden;
        private string title = string.Empty;
        private string signature;
        /// <summary>Raised when tooltip visibility or title changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>Visible resource costs.</summary>
        public ObservableCollection<RepairTooltipEntry> Costs { get; } = new ObservableCollection<RepairTooltipEntry>();
        /// <summary>Current tooltip visibility.</summary>
        public Noesis.Visibility Visibility
        {
            get => visibility;
            private set { if (visibility != value) { visibility = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility))); } }
        }
        /// <summary>Localized repair caption.</summary>
        public string Title
        {
            get => title;
            private set { if (title != value) { title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); } }
        }

        /// <summary>Shows the costs from an immutable simulation snapshot.</summary>
        public void Show(BuildingRepairQuote quote)
        {
            string next = string.Join(":", new[] { quote.BuildingGlobalId, quote.CurrentHealth, quote.MaxHealth,
                quote.Wood, quote.Stone, quote.Iron, quote.Pitch, quote.Gold,
                quote.AvailableWood, quote.AvailableStone, quote.AvailableIron, quote.AvailablePitch, quote.AvailableGold });
            if (signature == next && Visibility == Noesis.Visibility.Visible) return;
            signature = next;
            string localized = Translate.Instance.lookUpText(
                Enums.eTextSections.TEXT_BUBBLE_HELP_TEXT, Enums.eTextValues.BHELP_TEXT_REPAIR);
            if (string.IsNullOrWhiteSpace(localized)) localized = "Repair";
            Title = localized;
            Costs.Clear();
            Add(eGoods.STORED_WOOD_PLANKS, quote.Wood, quote.AvailableWood);
            Add(eGoods.STORED_STONE_BLOCKS, quote.Stone, quote.AvailableStone);
            Add(eGoods.STORED_IRON_INGOTS, quote.Iron, quote.AvailableIron);
            Add(eGoods.STORED_PITCH_RAW, quote.Pitch, quote.AvailablePitch);
            Add(eGoods.STORED_GOLD, quote.Gold, quote.AvailableGold);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Costs)));
            Visibility = Noesis.Visibility.Visible;
        }

        private void Add(eGoods good, int required, int available)
        {
            if (required <= 0) return;
            Costs.Add(new RepairTooltipEntry
            {
                Required = "  " + required + " ",
                Available = "(" + available + ")",
                Icon = MainViewModel.Instance.getSmallGoodsIcon((int)good)
            });
        }

        /// <summary>Hides the tooltip.</summary>
        public void Clear()
        {
            if (Visibility == Noesis.Visibility.Hidden) return;
            signature = null;
            Visibility = Noesis.Visibility.Hidden;
            Costs.Clear();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Costs)));
        }
    }
}
