using APIShared.GameModes;
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
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;
using System.Threading;

namespace SpectatorEditorBuildTest
{
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.9.2")]
    [BepInDependency("BugfixesAndQoL_Serp", "1.0.192")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("CheatMod_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtremePowers_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(Guid, Name, Version)]
    public sealed partial class SpectatorEditorBuildTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "SpectatorEditorBuildTest_Serp";
        public const string Name = "Spectator Editor Build Test";
        public const string Version = "0.1.2";

        // The plugin component is destroyed during startup. Hooks, callbacks, and the logger
        // therefore remain rooted in static fields for the entire process lifetime.
        private static Harmony harmony;
        private static TimestampLog log;
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
        private static readonly HashSet<string> loggedFailures = new HashSet<string>();
        private static IDisposable tribeSubscription;
        private static int unitDiagnostics;
        private static int unitRequests;
        [ThreadStatic] private static int placingUnitOwner;
        [ThreadStatic] private static int lastEditorMouseState;
        [ThreadStatic] private static bool rawMouseReleasePending;
        [ThreadStatic] private static BuildingRequest buildingClickScope;
        [ThreadStatic] private static PitchRequest pitchClickScope;
        [ThreadStatic] private static WallBuildScope buildingWall;
        private static volatile BuildingRequest pendingBuilding;
        private static volatile WallRequest activeWall;
        private static volatile WallRequest pendingWall;
        private static IDisposable buildingSpawnSubscription;
        private static IDisposable buildStructureSubscription;
        private static IDisposable wallBuildSubscription;
        private static IDisposable subtractResourceSubscription;
        private static IDisposable pitchBuildSubscription;
        private static NativeDetour<BuildAvailabilityDelegate> availabilityDetour;
        private static NativeDetour<VisualMapDelegate> visualMapDetour;
        private static NativeDetour<VisualFrameDelegate> visualFrameDetour;
        private static NativeDetour<VisualResourceDelegate> visualResourceDetour;
        private static int revealHiddenActive;
        private static int visualMapLogged, visualFrameLogged, visualResourceLogged;
        private static ulong nativeImageBase;
        private static int wallPreviewDiagnostics;
        private static int wallInputDiagnostics;
        private static int wallPressDiagnostics, wallReleaseDiagnostics, wallQueueDiagnostics;
        private static int wallOrderDiagnostics;
        private static int wallCostDiagnostics;
        private static int specialInputDiagnostics, pitchCostDiagnostics;
        private static int moatInputDiagnostics, moatOutcomeDiagnostics, deleteDiagnostics;
        private static volatile SpecialDragRequest activeSpecial;
        private static readonly List<MoatProbe> pendingMoat = new List<MoatProbe>();
        private static readonly object moatLock = new object();
        private static readonly List<PitchRequest> pendingPitch = new List<PitchRequest>();
        private static readonly object pitchLock = new object();
        private static int buildingDiagnostics;
        private static int vanillaWellDiagnostics;
        private static readonly List<BuildingVisualSample> visualSamples = new List<BuildingVisualSample>();
        private static readonly object visualSamplesLock = new object();
        private static IMissionLifecycleCapability missionLifecycle;
        private static long activeSessionId;
        private static bool humanPlacementMode;
        private static int humanSelectedOwner;
        private static int humanSavedScreen = 1;
        private static int humanSavedSubMode;
        private static bool?[] humanSavedTabs;
        private static IngameUIScreens humanScreen;
        private static HUD_Main humanBoundHud;
        private static Canvas humanCanvas;
        private static Border humanPanel;
        private static Border humanDragHandle;
        private static Button humanToggle;
        private static TextBlock humanToggleText;
        private static Panel humanOwners;
        private static readonly Button[] humanOwnerButtons = new Button[9];
        private static readonly TextBlock[] humanOwnerNumbers = new TextBlock[9];
        private static readonly RoutedEventHandler[] humanOwnerHandlers = new RoutedEventHandler[9];
        private static readonly MouseButtonEventHandler[] humanJumpHandlers = new MouseButtonEventHandler[9];
        private static readonly int[] humanDisplayedColours = new int[9];
        private static bool humanDragging, humanPositioned, humanUserMoved;
        private static bool humanCameraFailureLogged;
        private static Point humanDragStart;
        private static float humanDragOriginLeft, humanDragOriginTop;
        private static readonly SolidColorBrush ownerNormalBrush =
            new SolidColorBrush(Noesis.Color.FromArgb(160, 38, 23, 16));
        private static readonly SolidColorBrush ownerSelectedBrush =
            new SolidColorBrush(Noesis.Color.FromArgb(220, 135, 71, 25));

        private const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int AvailabilityRva = 0xCC420;
        private const int SpectatorFlagRva = 0x3666080;
        private const int VisualMapRva = 0x65830;
        private const int VisualFrameRva = 0x41D60;
        private const int VisualResourceRva = 0x6E620;
        private static readonly byte[] VisualMapEntry = {
            0x4C, 0x8B, 0xDC, 0x53, 0x55, 0x48, 0x81, 0xEC, 0x88, 0x00, 0x00, 0x00
        };
        private static readonly byte[] VisualFrameEntry = {
            0x48, 0x89, 0x5C, 0x24, 0x10, 0x48, 0x89, 0x6C, 0x24, 0x18
        };
        private static readonly byte[] VisualResourceEntry = {
            0x48, 0x89, 0x5C, 0x24, 0x18, 0x55
        };
        private static readonly byte[] AvailabilityEntry = {
            0x48, 0x89, 0x5C, 0x24, 0x20, 0x44, 0x89, 0x44, 0x24, 0x18
        };
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildAvailabilityDelegate(IntPtr manager, int mapper, int owner, int showMessage);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VisualMapDelegate(IntPtr tileManager);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VisualFrameDelegate(IntPtr map, int left, int right,
            int top, int bottom, int zoom);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate byte VisualResourceDelegate(IntPtr tileManager, int buildingId);

        private sealed class BuildingRequest
        {
            internal int Owner, X, Y, Scale;
            internal eMappers Mapper;
            internal long SessionId;
            internal bool Spawned;
            internal volatile bool ClickAccepted, BuildStarted;
            internal DateTime ExpiresAtUtc;
        }

        private sealed class WallRequest
        {
            internal int Owner, StartX, StartY, EndX, EndY;
            internal eMappers Mapper;
            internal long SessionId;
            internal DateTime ExpiresAtUtc;
        }

        private sealed class SpecialDragRequest
        {
            internal int Owner;
            internal eMappers Mapper;
            internal long SessionId;
        }

        private sealed class MoatProbe
        {
            internal int Owner, X, Y, BeforeIndex;
            internal uint BeforeFlags;
            internal long SessionId;
            internal DateTime ExpiresAtUtc;
            internal int Samples;
        }

        private sealed class PitchRequest
        {
            internal int Owner, X, Y;
            internal long SessionId;
            internal DateTime ExpiresAtUtc;
            internal bool Built;
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
            internal int NativeOwner;
            internal uint NativeOwnerStone, NativeOwnerCost;
            internal bool HasNativeOwnerResources;
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
            log = new TimestampLog(Logger);
            var candidate = new Harmony(Guid);
            IDisposable candidateTribeSubscription = null;
            IDisposable candidateBuildingSpawn = null;
            IDisposable candidateBuildStructure = null;
            IDisposable candidateWallBuild = null;
            IDisposable candidateSubtractResource = null;
            IDisposable candidatePitchBuild = null;
            bool tickRegistered = false;
            bool renderRegistered = false;
            bool libraryRegistered = false;
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
                Patch(candidate, typeof(GameData), nameof(GameData.setGameState), nameof(AfterToolsPlayState), true, typeof(EngineInterface.PlayState));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.InBuildingGameAction), nameof(BeforeVanillaHudTransition), false);
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.TroopsSelectedGameAction), nameof(BeforeVanillaTroopTransition), false, typeof(bool));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.DefaultGameUIGameAction), nameof(BeforeVanillaHudTransition), false, typeof(bool));
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.DefaultMapEditorUIGameAction), nameof(BeforeVanillaHudTransition), false);
                Patch(candidate, typeof(MainViewModel), nameof(MainViewModel.setUpInbuilding),
                    nameof(BeforeGatePanel), postfix: false, typeof(int), typeof(int));
                Patch(candidate, typeof(MainViewModel), "ButtonGateControls", nameof(BeforeTestGateCommand), false, typeof(object));
                Patch(candidate, typeof(MainViewModel), "ButtonDrawbridgeControls", nameof(BeforeTestBridgeCommand), false, typeof(object));
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.reportSubModeTabChange),
                    nameof(BeforeReportSubModeTabChange), postfix: false, typeof(int));
                Patch(candidate, typeof(EditorDirector), "getMouseStateForEngine",
                    nameof(AfterGetMouseStateForEngine), postfix: true,
                    typeof(bool).MakeByRefType(), typeof(bool).MakeByRefType());
                Patch(candidate, typeof(EditorDirector),
                    nameof(EditorDirector.updateLeftMouseStateForEngine),
                    nameof(BeforeUpdateLeftMouseStateForEngine), postfix: false, typeof(int));
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.clearMouseStateForEngine),
                    nameof(BeforeClearMouseStateForEngine), postfix: false);
                Patch(candidate, typeof(EditorDirector), nameof(EditorDirector.preDLLCallActions),
                    nameof(AfterPreDLLCallActions), postfix: true,
                    typeof(int).MakeByRefType(), typeof(int).MakeByRefType());
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.PlaceMapperItem),
                    nameof(BeforePlaceMapperItem), postfix: false,
                    typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(bool), typeof(bool), typeof(int));
                Patch(candidate, typeof(EngineInterface), nameof(EngineInterface.DeleteBuilding),
                    nameof(BeforeDeleteBuilding), postfix: false,
                    typeof(int), typeof(int), typeof(int), typeof(bool), typeof(int));
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
                candidatePitchBuild = BuildingR3EventHooks.OnBuildPitchDitch.Observable
                    .Subscribe(OnBuildPitchDitch);
                tribeSubscription = candidateTribeSubscription;
                buildingSpawnSubscription = candidateBuildingSpawn;
                buildStructureSubscription = candidateBuildStructure;
                wallBuildSubscription = candidateWallBuild;
                subtractResourceSubscription = candidateSubtractResource;
                pitchBuildSubscription = candidatePitchBuild;
                GameTimeManagerAPI.Instance.OnTick += OnSimulationTick;
                tickRegistered = true;
                UnityEngine.Application.onBeforeRender += OnBeforeRender;
                renderRegistered = true;
                CrusaderLibrary.Instance.LibraryLoaded += OnNativeLibraryLoaded;
                libraryRegistered = true;
                if (!ApiShared.Current.TryGetMissionLifecycle(Guid,
                    out IMissionLifecycleCapability lifecycle, out NativeCapabilityDiagnostic diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
                if (!lifecycle.TryRegisterObserver("SpectatorEditorBuildTest.Session",
                    OnMissionStart, OnMissionEnd, null, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason);
                missionLifecycle = lifecycle;
                harmony = candidate;
                RegisterTestActions();
                RegisterPowerInput();
                log.LogInfo("SPECTATOR_EDITOR_HOOKS_INSTALLED");
            }
            catch (Exception error)
            {
                // This candidate has not been published; a failed installation may be rolled back.
                if (libraryRegistered) CrusaderLibrary.Instance.LibraryLoaded -= OnNativeLibraryLoaded;
                if (renderRegistered) UnityEngine.Application.onBeforeRender -= OnBeforeRender;
                if (tickRegistered) GameTimeManagerAPI.Instance.OnTick -= OnSimulationTick;
                candidateTribeSubscription?.Dispose();
                candidateBuildingSpawn?.Dispose();
                candidateBuildStructure?.Dispose();
                candidateWallBuild?.Dispose();
                candidateSubtractResource?.Dispose();
                candidatePitchBuild?.Dispose();
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

        private static void LogOnce(string message)
        {
            string key = message.Split(new[] { ':', ' ' }, 2)[0];
            lock (loggedFailures) if (!loggedFailures.Add(key)) return;
            log?.LogError(message);
        }
    }
}
