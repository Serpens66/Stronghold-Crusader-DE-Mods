using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;

namespace SpectatorEditorBuildTest
{
    [BepInDependency("000shcdese", "2.12.0")]
    [BepInDependency("APIShared_Serp", "0.4.7")]
    [BepInDependency("BugfixesAndQoL_Serp")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed class SpectatorEditorBuildTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "SpectatorEditorBuildTest_Serp";
        public const string Name = "Spectator Editor Build Test";
        public const string Version = "0.1.0";

        // The plugin component is destroyed during startup. Hooks, callbacks, and the logger
        // therefore remain rooted in static fields for the entire process lifetime.
        private static Harmony harmony;
        private static ManualLogSource log;
        private static HUD_Main activeHud;
        private static int lastOwner = int.MinValue;
        private static int lastScreen = int.MinValue;
        private static HUD_Main pendingVisibilityHud;
        private static int pendingVisibilityScreen;
        private static int pendingVisibilityOwner;
        private static DateTime pendingVisibilityAt = DateTime.MaxValue;
        private static int visibilityDiagnostics;
        private static int hudLayoutDiagnostics;
        private static int dateLayoutDiagnostics;
        private static HUD_Main initialLayoutLoggedHud;
        private static DateLayoutState dateLayout;
        private static int suppressedBlankCalls;
        private static int suppressedFreezeCalls;
        private static FieldInfo buildIconListsField;
        private static FieldInfo buildButtonsField;
        private static bool runtimeConfirmed;
        private static bool failureLogged;
        private static IDisposable tribeSubscription;
        private static int unitDiagnostics;
        private static int unitRequests;
        [ThreadStatic] private static int placingUnitOwner;
        [ThreadStatic] private static int lastEditorMouseState;
        [ThreadStatic] private static BuildingRequest buildingClickScope;
        [ThreadStatic] private static WallBuildScope buildingWall;
        private static volatile BuildingRequest pendingBuilding;
        private static volatile WallRequest activeWall;
        private static volatile WallRequest pendingWall;
        private static IDisposable buildingSpawnSubscription;
        private static IDisposable buildStructureSubscription;
        private static IDisposable wallBuildSubscription;
        private static IDisposable subtractResourceSubscription;
        private static NativeDetour<BuildAvailabilityDelegate> availabilityDetour;
        private static ulong nativeImageBase;
        private static int wallDiagnostics;
        private static int buildingDiagnostics;
        private static int vanillaWellDiagnostics;
        private static readonly List<BuildingVisualSample> visualSamples = new List<BuildingVisualSample>();
        private static readonly object visualSamplesLock = new object();

        private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int AvailabilityRva = 0xCC420;
        private static readonly byte[] AvailabilityEntry = {
            0x48, 0x89, 0x5C, 0x24, 0x20, 0x44, 0x89, 0x44, 0x24, 0x18
        };
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildAvailabilityDelegate(IntPtr manager, int mapper, int owner, int showMessage);

        private sealed class BuildingRequest
        {
            internal int Owner, X, Y, Scale;
            internal eMappers Mapper;
            internal object GameState;
            internal bool Spawned;
            internal volatile bool ClickAccepted, BuildStarted;
            internal DateTime ExpiresAtUtc;
        }

        private sealed class WallRequest
        {
            internal int Owner, StartX, StartY, EndX, EndY;
            internal eMappers Mapper;
            internal object GameState;
            internal DateTime ExpiresAtUtc;
        }

        private sealed class WallStoneScope
        {
            internal int Owner;
            internal uint OriginalStone;
        }

        private sealed class WallBuildScope
        {
            internal WallRequest Request;
            internal uint OriginalStone, OriginalCost;
            internal int Subtractions;
        }

        private sealed class DateLayoutState
        {
            internal HUD_Main Hud;
            internal TextBlock Date;
            internal TranslateTransform Translate;
            internal float OriginalX, OriginalFontSize;
            internal bool OriginalHitTest;
            internal double LastLeft = double.NaN, LastRight = double.NaN;
            internal double LastHudWidth = double.NaN;
            internal float LastTranslation = float.NaN, LastFontSize = float.NaN;
            internal string LastText;
        }

        private void Awake()
        {
            if (harmony != null) return;
            log = Logger;
            var candidate = new Harmony(Guid);
            IDisposable candidateTribeSubscription = null;
            IDisposable candidateBuildingSpawn = null;
            IDisposable candidateBuildStructure = null;
            IDisposable candidateWallBuild = null;
            IDisposable candidateSubtractResource = null;
            bool tickRegistered = false;
            bool renderRegistered = false;
            try
            {
                ValidateHudFields();
                Patch(candidate, typeof(HUD_Main), nameof(HUD_Main.SetupModeDependantUI),
                    nameof(AfterSetupModeDependantUI), postfix: true);
                Patch(candidate, typeof(HUD_Main), nameof(HUD_Main.SetupNewBuildScreen),
                    nameof(AfterSetupNewBuildScreen), postfix: true, typeof(int));
                Patch(candidate, typeof(HUD_Main), nameof(HUD_Main.NewBuildScreenBlank),
                    nameof(BeforeNewBuildScreenBlank), postfix: false);
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.buildControlsFreeze),
                    nameof(BeforeBuildControlsFreeze), postfix: false, typeof(bool));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.CanPlaceMapper),
                    nameof(AfterCanPlaceMapper), postfix: true, typeof(object));
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.reportSubModeTabChange),
                    nameof(BeforeReportSubModeTabChange), postfix: false, typeof(int));
                Patch(candidate, typeof(EditorDirector), "getMouseStateForEngine",
                    nameof(AfterGetMouseStateForEngine), postfix: true,
                    typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType());
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.clearMouseStateForEngine),
                    nameof(BeforeClearMouseStateForEngine), postfix: false);
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.preDLLCallActions),
                    nameof(AfterPreDLLCallActions), postfix: true,
                    typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.PlaceMapperItem),
                    nameof(BeforePlaceMapperItem), postfix: false,
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int));
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.PlaceMapperItem),
                    nameof(AfterPlaceMapperItem), postfix: true,
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int));
                MethodInfo placeMapper = AccessTools.Method(typeof(EngineInterface),
                    nameof(EngineInterface.PlaceMapperItem), new[] { typeof(int), typeof(int),
                    typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int) });
                candidate.Patch(placeMapper, finalizer: new HarmonyMethod(AccessTools.Method(
                    typeof(SpectatorEditorBuildTestPlugin), nameof(FinalizePlaceMapperItem))));
                MethodInfo buildWall = AccessTools.Method(typeof(BulkBuildingDetours),
                    nameof(BulkBuildingDetours.c_game_build_wall_hook_impl));
                if (buildWall == null) throw new MissingMethodException(
                    typeof(BulkBuildingDetours).FullName, "c_game_build_wall_hook_impl");
                candidate.Patch(buildWall, finalizer: new HarmonyMethod(AccessTools.Method(
                    typeof(SpectatorEditorBuildTestPlugin), nameof(FinalizeBuildWall))));
                candidateTribeSubscription = TribeR3EventHooks.OnTribeAssignUnit.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(OnTribeAssigned);
                candidateBuildingSpawn = BuildingR3EventHooks.OnBuildingSpawn.Observable
                    .Subscribe(OnBuildingSpawn);
                candidateBuildStructure = BuildingR3EventHooks.OnBuildStructure.Observable
                    .Subscribe(OnBuildStructure);
                candidateWallBuild = BuildingR3EventHooks.OnBuildWall.Observable.Subscribe(OnBuildWall);
                candidateSubtractResource = PlayerR3EventHooks.OnPlayerSubtractResource.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre).Subscribe(OnSubtractResource);
                tribeSubscription = candidateTribeSubscription;
                buildingSpawnSubscription = candidateBuildingSpawn;
                buildStructureSubscription = candidateBuildStructure;
                wallBuildSubscription = candidateWallBuild;
                subtractResourceSubscription = candidateSubtractResource;
                GameTimeManagerAPI.Instance.OnTick += OnSimulationTick;
                tickRegistered = true;
                UnityEngine.Application.onBeforeRender += OnBeforeRender;
                renderRegistered = true;
                log.LogInfo("SPECTATOR_EDITOR_HOOKS_INSTALLED");
                harmony = candidate;
                CrusaderLibrary.Instance.LibraryLoaded += OnNativeLibraryLoaded;
            }
            catch (Exception error)
            {
                // This candidate has not been published; a failed installation may be rolled back.
                if (renderRegistered) UnityEngine.Application.onBeforeRender -= OnBeforeRender;
                if (tickRegistered) GameTimeManagerAPI.Instance.OnTick -= OnSimulationTick;
                candidateTribeSubscription?.Dispose();
                candidateBuildingSpawn?.Dispose();
                candidateBuildStructure?.Dispose();
                candidateWallBuild?.Dispose();
                candidateSubtractResource?.Dispose();
                candidate.UnpatchSelf();
                log.LogError("SPECTATOR_EDITOR_INIT_FAILED: " + error);
            }
        }

        private static void Patch(Harmony candidate, Type targetType, string targetName,
            string patchName, bool postfix, params Type[] arguments)
        {
            MethodInfo target = AccessTools.Method(targetType, targetName, arguments);
            MethodInfo patch = AccessTools.Method(typeof(SpectatorEditorBuildTestPlugin), patchName);
            if (target == null || patch == null)
                throw new MissingMethodException(targetType.FullName, targetName);
            candidate.Patch(target, prefix: postfix ? null : new HarmonyMethod(patch),
                postfix: postfix ? new HarmonyMethod(patch) : null);
        }

        private static void ValidateHudFields()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            buildIconListsField = typeof(HUD_Main).GetField("BuildIconLists", flags);
            buildButtonsField = typeof(HUD_Main).GetField("buildButtons", flags);
            if (buildIconListsField?.FieldType != typeof(int[,]) ||
                buildButtonsField?.FieldType != typeof(Button[]))
                throw new MissingFieldException("HUD_Main", "BuildIconLists/buildButtons");
        }

        private sealed class BuildingVisualSample
        {
            internal int BuildingId, X, Y, NextTick, Samples;
            internal string Source;
        }

        private static void OnNativeLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (availabilityDetour != null) return;
            NativeDetour<BuildAvailabilityDelegate> candidate = null;
            bool published = false;
            try
            {
                string path = Path.Combine(Paths.GameRootPath,
                    "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64",
                    "CrusaderDE.dll");
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                    if (!string.Equals(hash, NativeSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Native DLL hash differs: " + hash);
                }
                if (typeof(NativeDetour<>).Assembly.GetName().Version !=
                    new Version(1, 5, 0, 0))
                    throw new InvalidOperationException("Installed RedBird NativeX64 differs from 1.5.0.0.");
                ulong entry = unchecked((ulong)context.ModuleHandle.ToInt64()) + AvailabilityRva;
                VerifyAvailabilityEntry(entry);
                ProbeAvailabilityBackend(entry);
                VerifyAvailabilityEntry(entry);
                var request = new DetourRequest<BuildAvailabilityDelegate>
                {
                    Name = "SpectatorEditorBuildTest building availability",
                    TargetAddress = entry,
                    Callback = CheckBuildingAvailability
                };
                candidate = NativeDetourBackend.Instance.CreateDetour(in request)
                    as NativeDetour<BuildAvailabilityDelegate>;
                if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                    candidate.DisplacedByteCount != AvailabilityEntry.Length ||
                    candidate.TargetAddress != entry || candidate.IsInstalled)
                    throw new InvalidOperationException("Availability detour candidate differs from audited entry.");
                nativeImageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
                availabilityDetour = candidate;
                candidate.Enable();
                VerifyAvailabilityPatch(candidate, entry);
                candidate = null;
                published = true;
                log.LogInfo("SPECTATOR_EDITOR_BUILD_AVAILABILITY_READY rva=0xCC420 scheme=Indirect displaced=10");
            }
            catch (Exception error)
            {
                // Only a candidate that has not passed validation may be rolled back.
                if (!published)
                {
                    availabilityDetour = null;
                    nativeImageBase = 0;
                    candidate?.Dispose();
                }
                log.LogError("SPECTATOR_EDITOR_BUILD_AVAILABILITY_DISABLED: " + error);
            }
        }

        private static void VerifyAvailabilityEntry(ulong entry)
        {
            byte[] bytes = new byte[32];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            for (int index = 0; index < AvailabilityEntry.Length; index++)
                if (bytes[index] != AvailabilityEntry[index])
                    throw new InvalidOperationException("Native availability entry mismatch at byte " + index);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            int displaced = 0;
            while (displaced < AvailabilityEntry.Length)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Availability entry contains an unexpected instruction.");
                displaced += instruction.Length;
            }
            if (displaced != AvailabilityEntry.Length)
                throw new InvalidOperationException("Availability entry crosses the audited 10-byte boundary.");
        }

        private static void ProbeAvailabilityBackend(ulong entry)
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            var bytes = new byte[64];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            NativeDetour<BuildAvailabilityDelegate> probe = null;
            try
            {
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                var request = new DetourRequest<BuildAvailabilityDelegate>
                {
                    Name = "SpectatorEditorBuildTest copied-entry probe",
                    TargetAddress = unchecked((ulong)copy.ToInt64()),
                    Callback = CheckBuildingAvailability
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request)
                    as NativeDetour<BuildAvailabilityDelegate>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != AvailabilityEntry.Length || probe.IsInstalled)
                    throw new InvalidOperationException("Copied RedBird availability probe failed.");
                probe.Enable();
                VerifyAvailabilityPatch(probe, unchecked((ulong)copy.ToInt64()));
            }
            finally
            {
                probe?.Dispose();
                var restored = new byte[bytes.Length];
                Marshal.Copy(copy, restored, 0, restored.Length);
                for (int index = 0; index < bytes.Length; index++)
                    if (restored[index] != bytes[index])
                        throw new InvalidOperationException("Copied RedBird probe was not restored.");
                Marshal.FreeHGlobal(copy);
            }
        }

        private static void VerifyAvailabilityPatch(NativeDetour<BuildAvailabilityDelegate> detour,
            ulong entry)
        {
            if (!detour.IsInstalled || detour.TargetAddress != entry ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != AvailabilityEntry.Length ||
                detour.PointerSlot == IntPtr.Zero ||
                detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException("Installed availability detour contract differs.");
            byte[] patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)entry), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)entry + 6 + BitConverter.ToInt32(patch, 2)) !=
                    detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Installed availability detour patch differs.");
        }

        private static int CheckBuildingAvailability(IntPtr manager, int mapper, int owner,
            int showMessage)
        {
            try
            {
                BuildingRequest click = buildingClickScope;
                if (click != null && click.Owner == owner && (int)click.Mapper == mapper)
                    return 1;
                BuildingRequest queued = pendingBuilding;
                if (queued != null && queued.ClickAccepted && !queued.BuildStarted &&
                    DateTime.UtcNow <= queued.ExpiresAtUtc &&
                    queued.Owner == owner && (int)queued.Mapper == mapper && nativeImageBase != 0 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FF4))) == 10 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FEC))) == 0 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C132C))) ==
                        queued.X - queued.Scale / 2 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1330))) ==
                        queued.Y - queued.Scale / 2 &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1334))) == mapper &&
                    Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1338))) == queued.Scale)
                    return 1;
            }
            catch (Exception error)
            {
                if (buildingDiagnostics++ < 8)
                    log?.LogError("SPECTATOR_EDITOR_BUILD_AVAILABILITY_ERROR: " + error);
            }
            return availabilityDetour.Original(manager, mapper, owner, showMessage);
        }

        private static bool IsSpectatorSession()
        {
            try
            {
                var state = GameData.Instance?.lastGameState;
                var director = Director.instance;
                var editor = EditorDirector.instance;
                var view = MainViewModel.Instance;
                return state != null && director != null && editor != null && view != null &&
                    state.game_type == 3 && state.spectatorMode != 0 &&
                    editor.ActivePlayerID <= 0 && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !Shared.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static int CurrentAiOwner()
        {
            if (!IsSpectatorSession()) return -1;
            try
            {
                int owner = PlayerPerspectiveAPI.GetViewedPlayerId();
                return owner >= 1 && owner <= 8 &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(owner) ? owner : -1;
            }
            catch { return -1; }
        }

        private static void OnBeforeRender()
        {
            try
            {
                if (!IsSpectatorSession())
                {
                    RestoreDateLayout();
                    activeHud = null;
                    initialLayoutLoggedHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                    activeWall = null;
                    pendingWall = null;
                    pendingBuilding = null;
                    return;
                }
                MainViewModel view = MainViewModel.Instance;
                HUD_Main hud = view.HUDmain;
                if (hud == null || !view.Show_HUD_Main) return;
                bool layoutChanged = EnsureSpectatorHudLayout(hud);
                bool firstLayout = !ReferenceEquals(hud, initialLayoutLoggedHud);
                if (layoutChanged || firstLayout) hud.UpdateLayout();
                if (layoutChanged || firstLayout)
                {
                    initialLayoutLoggedHud = hud;
                    if (hudLayoutDiagnostics++ < 12)
                        log.LogInfo("SPECTATOR_EDITOR_HUD_LAYOUT screen=" + view.buildScreenID +
                            " tabs=" + EditorTabVisibility(hud) +
                            " corrected=" + layoutChanged);
                }
                if (view.FreezeMainControls)
                {
                    view.buildControlsFreeze(false);
                    log.LogInfo("SPECTATOR_EDITOR_FREEZE_RECOVERED");
                }
                int owner = CurrentAiOwner();
                int screen = view.buildScreenID;
                if (!ReferenceEquals(hud, activeHud) || owner != lastOwner || screen != lastScreen)
                {
                    if (screen <= 0 || screen == 15 || screen == 17 ||
                        screen == 18 || screen == 21 || screen == 22 || screen >= 23)
                        hud.SetupNewBuildScreen(1);
                    else
                        RefreshBuildIcons(hud, owner, "session");
                }
                if (!runtimeConfirmed)
                {
                    runtimeConfirmed = true;
                    log.LogInfo("SPECTATOR_EDITOR_RUNTIME_AFTER_STARTUP: HUD active; owner=" +
                        owner + " screen=" + view.buildScreenID);
                }
                if (ReferenceEquals(hud, pendingVisibilityHud) &&
                    DateTime.UtcNow >= pendingVisibilityAt)
                {
                    LogVisibilityAfterAnimation(hud);
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                }
                PositionDate(hud);
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_HUD_ERROR: " + error);
            }
        }

        private static void AfterSetupModeDependantUI(HUD_Main __instance)
        {
            if (!IsSpectatorSession()) return;
            try { EnsureSpectatorHudLayout(__instance); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UI_ERROR: " + error); }
        }

        private static bool EnsureSpectatorHudLayout(HUD_Main hud)
        {
            bool changed = false;
            changed |= EnsureVisibility(hud, "MainFrameBuildings", Visibility.Visible);
            changed |= EnsureVisibility(hud, "MainFrameTerrain", Visibility.Hidden);
            changed |= EnsureVisibility(hud, "BuildMenuGrid", Visibility.Visible);
            changed |= EnsureVisibility(hud, "MEMenuGrid", Visibility.Hidden);
            changed |= EnsureVisibility(hud, "BottomTabs1", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMETroops", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMEArabTroops", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMESiege", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMEBedouin", Visibility.Visible);
            changed |= EnsureVisibility(hud, "RadioButtonMERuins", Visibility.Collapsed);
            changed |= EnsureVisibility(hud, "ButtonBuildModeTerrain", Visibility.Collapsed);
            return changed;
        }

        private static bool EnsureVisibility(HUD_Main hud, string name, Visibility visibility)
        {
            var element = hud.FindName(name) as FrameworkElement;
            if (element == null) throw new MissingMemberException("HUD_Main", name);
            if (element.Visibility == visibility) return false;
            element.Visibility = visibility;
            return true;
        }

        private static string EditorTabVisibility(HUD_Main hud)
        {
            string[] names = { "RadioButtonMETroops", "RadioButtonMESiege",
                "RadioButtonMEArabTroops", "RadioButtonMEBedouin" };
            string result = "";
            foreach (string name in names)
            {
                var tab = hud.FindName(name) as FrameworkElement;
                result += (result.Length == 0 ? "" : ",") + name + "=" +
                    (tab == null ? "missing" : tab.Visibility + "/" + tab.IsVisible);
            }
            return result;
        }

        private static void AfterSetupNewBuildScreen(HUD_Main __instance)
        {
            if (!IsSpectatorSession()) return;
            try { RefreshBuildIcons(__instance, CurrentAiOwner(), "tab"); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_ICONS_ERROR: " + error); }
        }

        private static bool BeforeNewBuildScreenBlank()
        {
            if (!IsSpectatorSession()) return true;
            if (++suppressedBlankCalls <= 3)
                log.LogInfo("SPECTATOR_EDITOR_BLANK_SUPPRESSED screen=" +
                    MainViewModel.Instance.buildScreenID);
            return false;
        }

        private static bool BeforeBuildControlsFreeze(bool Mode)
        {
            if (!Mode || !IsSpectatorSession()) return true;
            if (++suppressedFreezeCalls <= 3)
                log.LogInfo("SPECTATOR_EDITOR_FREEZE_SUPPRESSED frozen=" +
                    MainViewModel.Instance.FreezeMainControls);
            return false;
        }

        private static void LogVisibilityAfterAnimation(HUD_Main hud)
        {
            if (MainViewModel.Instance.buildScreenID != pendingVisibilityScreen ||
                CurrentAiOwner() != pendingVisibilityOwner)
                return;
            int[,] lists = (int[,])buildIconListsField.GetValue(hud);
            Button[] buttons = (Button[])buildButtonsField.GetValue(hud);
            int selected = 0;
            int visible = 0;
            int rendered = 0;
            int enabled = 0;
            for (int slot = 0; slot < lists.GetLength(1); slot++)
            {
                int index = lists[pendingVisibilityScreen, slot];
                if (index <= 0) break;
                Button button = buttons[AlternateBuildingButton(index)];
                if (button == null) continue;
                selected++;
                if (button.Visibility == Visibility.Visible) visible++;
                if (button.IsVisible) rendered++;
                if (button.IsEnabled) enabled++;
            }
            visibilityDiagnostics++;
            log.LogInfo("SPECTATOR_EDITOR_ICONS_SETTLED screen=" + pendingVisibilityScreen +
                " owner=" + pendingVisibilityOwner + " selected=" + selected +
                " visible=" + visible + " rendered=" + rendered + " enabled=" +
                enabled + " frozen=" + MainViewModel.Instance.FreezeMainControls +
                " blankSuppressed=" + suppressedBlankCalls);
        }

        private static void RefreshBuildIcons(HUD_Main hud, int owner, string reason)
        {
            int[,] lists = (int[,])buildIconListsField.GetValue(hud);
            Button[] buttons = (Button[])buildButtonsField.GetValue(hud);
            int screen = MainViewModel.Instance.buildScreenID;
            if (lists == null || lists.GetLength(0) != 23 || lists.GetLength(1) != 17 ||
                buttons == null || buttons.Length != 588 || screen < 1 || screen >= 23)
                throw new InvalidOperationException("Unexpected HUD build-list layout or screen " + screen);

            bool[] known = new bool[buttons.Length];
            bool[] selected = new bool[buttons.Length];
            for (int alternate = 583; alternate <= 587; alternate++) known[alternate] = true;
            for (int page = 0; page < lists.GetLength(0); page++)
            {
                for (int slot = 0; slot < lists.GetLength(1); slot++)
                {
                    int index = lists[page, slot];
                    if (index <= 0) break;
                    if (index >= buttons.Length) throw new IndexOutOfRangeException("HUD build button " + index);
                    known[index] = true;
                    int alternate = AlternateBuildingButton(index);
                    if (alternate != index) known[alternate] = true;
                    if (page == screen) selected[alternate] = true;
                }
            }

            // Validate the current page before changing any visibility.
            for (int index = 1; index < buttons.Length; index++)
                if (selected[index] && buttons[index] == null)
                    throw new MissingMemberException("HUD_Main", "buildButtons[" + index + "]");

            int selectedCount = 0;
            int visibleCount = 0;
            int renderedCount = 0;
            int enabledCount = 0;
            for (int index = 1; index < buttons.Length; index++)
            {
                if (!known[index] || buttons[index] == null) continue;
                Button button = buttons[index];
                if (selected[index])
                {
                    selectedCount++;
                    button.IsEnabled = owner > 0 &&
                        MainViewModel.Instance.CanPlaceMapper(button.CommandParameter);
                    button.Visibility = Visibility.Visible;
                    if (button.Visibility == Visibility.Visible) visibleCount++;
                    if (button.IsVisible) renderedCount++;
                    if (button.IsEnabled) enabledCount++;
                }
                else
                {
                    button.IsEnabled = false;
                    button.Visibility = Visibility.Hidden;
                }
            }

            bool changed = !ReferenceEquals(hud, activeHud) || owner != lastOwner || screen != lastScreen;
            activeHud = hud;
            lastOwner = owner;
            lastScreen = screen;
            if (changed)
            {
                log.LogInfo("SPECTATOR_EDITOR_ICONS reason=" + reason + " screen=" + screen +
                    " owner=" + owner + " selected=" + selectedCount + " visible=" +
                    visibleCount + " rendered=" + renderedCount + " enabled=" + enabledCount);
                if (visibilityDiagnostics < 32)
                {
                    pendingVisibilityHud = hud;
                    pendingVisibilityScreen = screen;
                    pendingVisibilityOwner = owner;
                    pendingVisibilityAt = DateTime.UtcNow.AddMilliseconds(650);
                }
            }
        }

        private static int AlternateBuildingButton(int index)
        {
            if (index != 87 && index != 88 && index != 89 && index != 417 && index != 172)
                return index;
            var state = GameData.Instance?.lastGameState;
            if (state == null || (state.lord_Type != 1 && state.lord_Type != 2 &&
                state.lord_Type != 6 && state.lord_Type != 7))
                return index;
            switch (index)
            {
                case 87: return 583;
                case 88: return 584;
                case 89: return 585;
                case 417: return 586;
                default: return 587;
            }
        }

        private static void AfterCanPlaceMapper(MainViewModel __instance, object parameter, ref bool __result)
        {
            if (!IsSpectatorSession()) return;
            string key = parameter as string;
            if (string.IsNullOrEmpty(key)) return;
            if (key == "STRUCT_MENU_RETURN_KEEPS")
            {
                __result = CurrentAiOwner() > 0;
                return;
            }
            eMappers mapper = (eMappers)(int)__instance.getStructToMapperEnum(key);
            if (!IsAllowedMapper(mapper)) return;
            __result = CurrentAiOwner() > 0;
        }

        private static bool BeforeReportSubModeTabChange(int newMode)
        {
            // Vanilla editor tab handlers already set MainViewModel.SubMode. In a live game,
            // passing editor submodes to SetAppMode would alter unrelated native game state.
            return !IsSpectatorSession();
        }

        private static void AfterGetMouseStateForEngine(int __result)
        {
            lastEditorMouseState = __result;
            if (activeWall != null && wallDiagnostics++ < 48)
                log.LogInfo("SPECTATOR_EDITOR_WALL_MOUSE state=" + __result +
                    " owner=" + activeWall.Owner + " mapper=" + activeWall.Mapper);
        }

        private static bool BeforeClearMouseStateForEngine()
        {
            return !IsSpectatorSession() || activeWall == null ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != activeWall.Mapper;
        }

        private static void AfterPreDLLCallActions(ref int mouseOverX, ref int mouseOverY)
        {
            WallRequest wall = activeWall;
            int state = lastEditorMouseState;
            lastEditorMouseState = 0;
            if (wall != null && wallDiagnostics++ < 48)
                log.LogInfo("SPECTATOR_EDITOR_WALL_FORWARD state=" + state +
                    " tile=" + mouseOverX + "," + mouseOverY);
            if (wall == null || (state != 2 && state != 3)) return;
            if (!IsSpectatorSession() || MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != wall.Mapper ||
                !ReferenceEquals(wall.GameState, GameData.Instance?.lastGameState))
            {
                activeWall = null;
                return;
            }
            int tileX = mouseOverX;
            int tileY = mouseOverY;
            if (tileX < 0 || tileY < 0)
            {
                if (state != 3) return;
                tileX = wall.EndX;
                tileY = wall.EndY;
            }
            if (state == 2)
            {
                wall.EndX = tileX;
                wall.EndY = tileY;
            }
            if (state == 3)
            {
                wall.EndX = tileX;
                wall.EndY = tileY;
                wall.ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5);
                pendingWall = wall;
                activeWall = null;
            }
            EngineInterface.PlaceMapperItem((int)wall.Mapper, tileX, tileY,
                0, wall.Owner, false, state == 2, state);
            if (state == 3)
                log.LogInfo("SPECTATOR_EDITOR_WALL_REQUEST owner=" + wall.Owner +
                    " mapper=" + wall.Mapper + " start=" + wall.StartX + "," + wall.StartY +
                    " end=" + wall.EndX + "," + wall.EndY);
        }

        private static bool BeforePlaceMapperItem(int item, int x, int y, int size,
            ref int player, ref bool inGameNotEditor, bool constructingOnly, int mouseState,
            out WallStoneScope __state)
        {
            __state = null;
            if (!IsSpectatorSession()) return true;
            eMappers mapper = (eMappers)item;
            if (!IsAllowedMapper(mapper)) return false;
            WallRequest wall = mouseState == 3 ? pendingWall : activeWall;
            int owner = IsWallMapper(mapper) && (mouseState == 2 || mouseState == 3) &&
                wall != null && wall.Mapper == mapper ? wall.Owner : CurrentAiOwner();
            if (owner < 1) return false;
            player = owner;
            inGameNotEditor = false;
            if (IsWallMapper(mapper))
            {
                if (!TryRaiseStone(owner, out __state)) return false;
                if (wallDiagnostics++ < 48)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_NATIVE state=" + mouseState +
                        " owner=" + owner + " mapper=" + mapper + " tile=" + x + "," + y +
                        " stone=" + __state.OriginalStone);
                if (mouseState == 1 && !constructingOnly)
                    activeWall = new WallRequest { Owner = owner, Mapper = mapper,
                        StartX = x, StartY = y, EndX = x, EndY = y,
                        GameState = GameData.Instance?.lastGameState };
                return true;
            }
            if (IsUnitMapper(mapper))
            {
                if (mouseState == 3 && !constructingOnly)
                {
                    placingUnitOwner = owner;
                    if (unitRequests++ < 32)
                        log.LogInfo("SPECTATOR_EDITOR_UNIT_REQUEST owner=" + owner + " mapper=" +
                            mapper + " tile=" + x + "," + y);
                }
                return true;
            }
            bool buildingClick = mouseState == 1 ||
                (mapper == eMappers.MAPPER_ARAB_BALLISTA && mouseState == 3);
            if (constructingOnly || !buildingClick)
                return true;
            if (availabilityDetour == null || !availabilityDetour.IsInstalled)
            {
                if (buildingDiagnostics++ < 8)
                    log.LogWarning("SPECTATOR_EDITOR_BUILD_BLOCKED: audited cost-check hook unavailable");
                return false;
            }
            int scale = BuildingScales.GetScale(mapper);
            if (scale < 0) return false;
            var request = new BuildingRequest { Owner = owner, Mapper = mapper,
                X = x, Y = y, Scale = scale,
                GameState = GameData.Instance?.lastGameState,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5) };
            buildingClickScope = request;
            pendingBuilding = request;
            log.LogInfo("SPECTATOR_EDITOR_BUILD_REQUEST owner=" + owner + " mapper=" + mapper +
                " cursor=" + x + "," + y + " origin=" + (x - scale / 2) + "," +
                (y - scale / 2));
            return true;
        }

        private static void AfterPlaceMapperItem()
        {
            placingUnitOwner = 0;
            BuildingRequest request = buildingClickScope;
            if (request != null)
            {
                request.ClickAccepted = true;
                buildingClickScope = null;
            }
        }

        private static Exception FinalizePlaceMapperItem(Exception __exception,
            WallStoneScope __state)
        {
            if (__state != null) RestoreStone(__state.Owner, __state.OriginalStone);
            placingUnitOwner = 0;
            if (__exception != null && buildingClickScope != null &&
                ReferenceEquals(pendingBuilding, buildingClickScope))
                pendingBuilding = null;
            buildingClickScope = null;
            return __exception;
        }

        private static bool IsWallMapper(eMappers mapper)
        {
            return mapper == eMappers.MAPPER_WALL ||
                mapper == eMappers.MAPPER_CRENAL ||
                mapper == eMappers.MAPPER_WOODWALL;
        }

        private static unsafe bool TryRaiseStone(int owner, out WallStoneScope scope)
        {
            scope = null;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,
                out GamePlayerResources* resources) || resources == null) return false;
            scope = new WallStoneScope { Owner = owner,
                OriginalStone = resources->r_TotalGoodsStoneBlocks };
            if (resources->r_TotalGoodsStoneBlocks < 125)
                resources->r_TotalGoodsStoneBlocks = 125;
            return true;
        }

        private static unsafe void RestoreStone(int owner, uint original)
        {
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(owner,
                out GamePlayerResources* resources) && resources != null)
                resources->r_TotalGoodsStoneBlocks = original;
        }

        private static bool WallMatches(WallRequest request, BuildWallEventArgs args)
        {
            return request != null && request.Owner == args.PlayerId &&
                request.Mapper == args.WallType &&
                DateTime.UtcNow <= request.ExpiresAtUtc &&
                ReferenceEquals(request.GameState, GameData.Instance?.lastGameState) &&
                ((NearTile(request.StartX, request.StartY, args.TileXBegin, args.TileYBegin) &&
                  NearTile(request.EndX, request.EndY, args.TileXEnd, args.TileYEnd)) ||
                 (NearTile(request.StartX, request.StartY, args.TileXEnd, args.TileYEnd) &&
                  NearTile(request.EndX, request.EndY, args.TileXBegin, args.TileYBegin)));
        }

        private static bool NearTile(int x1, int y1, int x2, int y2) =>
            Math.Abs(x1 - x2) <= 1 && Math.Abs(y1 - y2) <= 1;

        private static unsafe void OnBuildWall(BuildWallEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                WallRequest request = pendingWall;
                if (wallDiagnostics++ < 48)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EVENT phase=Pre owner=" + args.PlayerId +
                        " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                        args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                        " pending=" + (request == null ? "none" : request.Owner + "/" +
                            request.Mapper + "/" + request.StartX + "," + request.StartY + "-" +
                            request.EndX + "," + request.EndY));
                if (!WallMatches(request, args) || !IsSpectatorSession() ||
                    !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(args.PlayerId,
                        out GamePlayerResources* resources) || resources == null) return;
                pendingWall = null;
                buildingWall = new WallBuildScope { Request = request,
                    OriginalStone = resources->r_TotalGoodsStoneBlocks,
                    OriginalCost = resources->r_LastBoughtWallStoneCost };
                if (resources->r_TotalGoodsStoneBlocks < 125)
                    resources->r_TotalGoodsStoneBlocks = 125;
                return;
            }
            WallBuildScope scope = buildingWall;
            if (scope == null || !WallMatches(scope.Request, args)) return;
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(args.PlayerId,
                out GamePlayerResources* postResources) && postResources != null)
            {
                postResources->r_TotalGoodsStoneBlocks = scope.OriginalStone;
                postResources->r_LastBoughtWallStoneCost = scope.OriginalCost;
            }
            buildingWall = null;
            log.LogInfo("SPECTATOR_EDITOR_WALL_BUILT owner=" + args.PlayerId +
                " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                " stone=" + scope.OriginalStone + " skippedDeductions=" +
                scope.Subtractions + " remainder=" + scope.OriginalCost);
        }

        private static void OnSubtractResource(PlayerSubtractResourceEventArgs args)
        {
            WallBuildScope scope = buildingWall;
            if (scope == null || args.PlayerId != scope.Request.Owner ||
                args.Good != eGoods.STORED_STONE_BLOCKS) return;
            args.SkipOriginalFunction = true;
            scope.Subtractions++;
            if (wallDiagnostics++ < 48)
                log.LogInfo("SPECTATOR_EDITOR_WALL_COST_SKIPPED owner=" + args.PlayerId +
                    " good=" + args.Good + " amount=" + args.Amount);
        }

        private static unsafe Exception FinalizeBuildWall(Exception __exception)
        {
            WallBuildScope scope = buildingWall;
            if (scope == null) return __exception;
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(scope.Request.Owner,
                out GamePlayerResources* resources) && resources != null)
            {
                resources->r_TotalGoodsStoneBlocks = scope.OriginalStone;
                resources->r_LastBoughtWallStoneCost = scope.OriginalCost;
            }
            buildingWall = null;
            log.LogWarning("SPECTATOR_EDITOR_WALL_SCOPE_RECOVERED owner=" +
                scope.Request.Owner + " error=" + __exception);
            return __exception;
        }

        private static void PositionDate(HUD_Main hud)
        {
            var date = hud.FindName("OST_Date") as TextBlock;
            var lastTab = hud.FindName("RadioButtonMEBedouin") as FrameworkElement;
            var allies = hud.FindName("ButtonAllies") as FrameworkElement;
            var merit = hud.FindName("ButtonMerit") as FrameworkElement;
            if (date == null || lastTab == null || merit == null) return;
            if (dateLayout == null || !ReferenceEquals(dateLayout.Hud, hud) ||
                !ReferenceEquals(dateLayout.Date, date))
            {
                RestoreDateLayout();
                var group = date.RenderTransform as TransformGroup;
                var translate = group != null && group.Children.Count > 0
                    ? group.Children[0] as TranslateTransform : null;
                if (translate == null)
                    throw new MissingMemberException("HUD_Main", "OST_Date translation");
                dateLayout = new DateLayoutState { Hud = hud, Date = date,
                    Translate = translate, OriginalX = translate.X,
                    OriginalFontSize = date.FontSize,
                    OriginalHitTest = date.IsHitTestVisible };
            }
            date.IsHitTestVisible = false;
            if (!lastTab.IsVisible || lastTab.ActualWidth <= 0) return;
            double left = lastTab.TranslatePoint(new Point(lastTab.ActualWidth, 0), hud).X;
            double right = allies != null && allies.IsVisible
                ? allies.TranslatePoint(new Point(0, 0), hud).X
                : merit.TranslatePoint(new Point(0, 0), hud).X;
            if (right <= left + 8) return;
            DateLayoutState state = dateLayout;
            string text = date.Text ?? "";
            if (state.LastLeft == left && state.LastRight == right &&
                state.LastHudWidth == hud.ActualWidth && state.LastText == text &&
                state.LastTranslation == state.Translate.X &&
                state.LastFontSize == date.FontSize)
                return;

            state.Translate.X = 0;
            if (date.FontSize != state.OriginalFontSize)
                date.FontSize = state.OriginalFontSize;
            date.UpdateLayout();
            double available = right - left - 8;
            double naturalWidth = date.ActualWidth;
            if (naturalWidth <= 0) return;
            if (naturalWidth > available)
            {
                date.FontSize = (float)Math.Max(1,
                    state.OriginalFontSize * available / naturalWidth);
                date.UpdateLayout();
                if (date.ActualWidth > available)
                {
                    date.FontSize *= (float)(available / date.ActualWidth);
                    date.UpdateLayout();
                }
            }
            double baseX = date.TranslatePoint(new Point(0, 0), hud).X;
            double targetX = left + 4 + (available - date.ActualWidth) / 2;
            state.Translate.X = (float)(targetX - baseX);
            state.LastLeft = left;
            state.LastRight = right;
            state.LastHudWidth = hud.ActualWidth;
            state.LastText = text;
            state.LastTranslation = state.Translate.X;
            state.LastFontSize = date.FontSize;
            if (dateLayoutDiagnostics++ < 24)
                log.LogInfo("SPECTATOR_EDITOR_DATE_LAYOUT left=" + left +
                    " right=" + right + " textWidth=" + date.ActualWidth +
                    " font=" + date.FontSize + " target=" + targetX +
                    " translation=" + state.Translate.X + " text=" + text);
        }

        private static void RestoreDateLayout()
        {
            DateLayoutState state = dateLayout;
            dateLayout = null;
            if (state == null) return;
            try
            {
                state.Translate.X = state.OriginalX;
                state.Date.FontSize = state.OriginalFontSize;
                state.Date.IsHitTestVisible = state.OriginalHitTest;
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_DATE_RESTORE_ERROR: " + error);
            }
        }

        private static void OnSimulationTick(int tick)
        {
            lock (visualSamplesLock)
            {
                for (int index = visualSamples.Count - 1; index >= 0; index--)
                {
                    BuildingVisualSample sample = visualSamples[index];
                    if (tick < sample.NextTick) continue;
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_VISUAL_LATER tick=" + tick +
                        " source=" + sample.Source + " buildingId=" + sample.BuildingId +
                        " " + DescribeBuildingVisual(sample.BuildingId, sample.X, sample.Y));
                    sample.Samples++;
                    if (sample.Samples >= 3) visualSamples.RemoveAt(index);
                    else sample.NextTick = tick + (sample.Samples == 1 ? 4 : 16);
                }
            }
            BuildingRequest request = pendingBuilding;
            if (request != null && DateTime.UtcNow > request.ExpiresAtUtc)
            {
                pendingBuilding = null;
                if (buildingDiagnostics++ < 20)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_EXPIRED tick=" + tick +
                        " owner=" + request.Owner + " mapper=" + request.Mapper +
                        " spawned=" + request.Spawned);
            }
            WallRequest wall = pendingWall;
            if (wall != null && DateTime.UtcNow > wall.ExpiresAtUtc)
            {
                pendingWall = null;
                if (wallDiagnostics++ < 48)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EXPIRED tick=" + tick +
                        " owner=" + wall.Owner + " mapper=" + wall.Mapper);
            }
        }

        private static void OnBuildStructure(BuildStructureEventArgs args)
        {
            BuildingRequest request = pendingBuilding;
            if (request == null || !request.ClickAccepted ||
                DateTime.UtcNow > request.ExpiresAtUtc ||
                args.PlayerId != request.Owner || args.Mappers != request.Mapper ||
                args.BuildingScaleUnknown != request.Scale ||
                args.TileX != request.X - request.Scale / 2 ||
                args.TileY != request.Y - request.Scale / 2 ||
                !ReferenceEquals(request.GameState, GameData.Instance?.lastGameState) ||
                !IsSpectatorSession()) return;
            if (args.Phase == EventHookPhase.Pre)
            {
                request.BuildStarted = true;
                args.IsFree = true;
                if (buildingDiagnostics++ < 24)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_NATIVE owner=" + args.PlayerId +
                        " mapper=" + args.Mappers + " tile=" + args.TileX + "," +
                        args.TileY + " scale=" + args.BuildingScaleUnknown +
                        " variant=" + args.Unknown1 + " free=True");
            }
            else if (request.BuildStarted)
            {
                pendingBuilding = null;
                if (buildingDiagnostics++ < 24)
                    log.LogInfo("SPECTATOR_EDITOR_BUILD_FINISHED owner=" + args.PlayerId +
                        " mapper=" + args.Mappers + " spawned=" + request.Spawned);
            }
        }

        private static unsafe void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Building != eStructs.STRUCT_WELL && pendingBuilding == null) return;
            BuildingRequest request = pendingBuilding;
            bool ours = request != null && request.BuildStarted &&
                request.Owner == args.PlayerId &&
                request.X - request.Scale / 2 == args.TileX &&
                request.Y - request.Scale / 2 == args.TileY;
            if (!ours && (args.Building != eStructs.STRUCT_WELL ||
                vanillaWellDiagnostics++ >= 8)) return;
            if (args.Phase == EventHookPhase.Post && ours)
                request.Spawned = args.ReturnValue > 0;
            log.LogInfo("SPECTATOR_EDITOR_BUILD_VISUAL source=" + (ours ? "spectator" : "vanilla") +
                " phase=" + args.Phase + " owner=" + args.PlayerId +
                " building=" + args.Building + " tile=" + args.TileX + "," + args.TileY +
                " buildingId=" + args.ReturnValue + " visualOwner=" + args.VisualPlayerId +
                " variation=" + args.SpriteVariationIndex + " " +
                DescribeBuildingVisual((int)args.ReturnValue, args.TileX, args.TileY));
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                args.Building == eStructs.STRUCT_WELL)
                lock (visualSamplesLock)
                    visualSamples.Add(new BuildingVisualSample {
                        BuildingId = (int)args.ReturnValue, X = args.TileX, Y = args.TileY,
                        Source = ours ? "spectator" : "vanilla"
                    });
        }

        private static unsafe string DescribeBuildingVisual(int buildingId, int x, int y)
        {
            try
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                var gfx = GameTileManagerAPI.Instance.GetGfxLayer();
                var alpha = GameTileManagerAPI.Instance.GetAlphaGfxLayer();
                var structure = GameTileManagerAPI.Instance.GetStructureLayer();
                string tiles = tileId >= 0 && tileId < gfx.Length &&
                    tileId < alpha.Length && tileId < structure.Length ?
                    "gfx=" + gfx[tileId] + " alpha=" + alpha[tileId] +
                    " structure=" + structure[tileId] : "tile=invalid";
                if (buildingId > 0 && GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building) && building != null)
                    return tiles + " record=" + building->r_BuildingType + "/" +
                        building->r_SpriteVariationIndex + "/" +
                        building->r_SpritePlayerColorId + "/" +
                        building->r_TilePositionXBegin + "," + building->r_TilePositionYBegin;
                return tiles + " record=none";
            }
            catch (Exception error)
            {
                LogOnce("SPECTATOR_EDITOR_VISUAL_DIAGNOSTIC_ERROR: " + error);
                return "visual=unavailable";
            }
        }

        private static unsafe void OnTribeAssigned(TribeAssignUnitEventArgs args)
        {
            int owner = placingUnitOwner;
            if (owner <= 0 || unitDiagnostics >= 32) return;
            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(args.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_ControllableForPlayerId != owner)
                    return;
                unitDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_UNIT_GROUP owner=" + owner + " unitId=" +
                    args.UnitId + " tribeId=" + args.TribeId + " aiRole=" + unit->r_AITribeRole);
            }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UNIT_DIAGNOSTIC_ERROR: " + error); }
        }

        private static bool IsAllowedMapper(eMappers mapper)
        {
            if (IsUnitMapper(mapper) || IsWallMapper(mapper)) return true;
            string name = mapper.ToString();
            return BuildingScales.GetScale(mapper) >= 0 &&
                !name.Contains("RUINS") && !name.Contains("POND") &&
                !name.Contains("FLAG") && !name.Contains("SIGNPOST") &&
                !name.Contains("DELETE") && !name.Contains("ASSEMBLY") &&
                !name.Contains("MARKER");
        }

        private static bool IsUnitMapper(eMappers mapper)
        {
            int value = (int)mapper;
            return (value >= 0x10e && value <= 0x11e) ||
                   (value >= 0x15e && value <= 0x165) ||
                   (value >= 400 && value <= 407);
        }

        private static void LogOnce(string message)
        {
            if (failureLogged) return;
            failureLogged = true;
            log?.LogError(message);
        }
    }
}
