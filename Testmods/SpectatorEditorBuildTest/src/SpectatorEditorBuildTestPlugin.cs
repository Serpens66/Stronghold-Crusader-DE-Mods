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
        private static StackPanel humanOwners;
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
            log = Logger;
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
                InstallVisualDetours(context);
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
                if (click != null && IsCurrentSession(click.SessionId) &&
                    click.Owner == owner && (int)click.Mapper == mapper)
                    return 1;
                if (mapper == (int)eMappers.MAPPER_PITCH_DITCH)
                {
                    PitchRequest pitchClick = pitchClickScope;
                    if (PitchMatches(pitchClick, owner, pitchClick?.X ?? -1,
                        pitchClick?.Y ?? -1)) return 1;
                    lock (pitchLock)
                        foreach (PitchRequest pitch in pendingPitch)
                            if (PitchMatches(pitch, owner, pitch.X, pitch.Y) &&
                                PitchChoreMatches(pitch)) return 1;
                }
                BuildingRequest queued = pendingBuilding;
                if (queued != null && queued.ClickAccepted && !queued.BuildStarted &&
                    IsCurrentSession(queued.SessionId) &&
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

        private static long CurrentSessionId => Interlocked.Read(ref activeSessionId);

        private static bool IsCurrentSession(long sessionId) =>
            sessionId > 0 && sessionId == CurrentSessionId;

        private static void OnMissionStart(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (sessionId <= 0 || sessionId == CurrentSessionId) return;
            ResetSessionState();
            Interlocked.Exchange(ref activeSessionId, sessionId);
            log?.LogInfo("SPECTATOR_EDITOR_SESSION_START id=" + sessionId);
        }

        private static void OnMissionEnd(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (!IsCurrentSession(sessionId)) return;
            Interlocked.Exchange(ref activeSessionId, 0);
            ResetSessionState();
            log?.LogInfo("SPECTATOR_EDITOR_SESSION_END id=" + sessionId);
        }

        private static void ResetSessionState()
        {
            Interlocked.Exchange(ref revealHiddenActive, 0);
            DetachHumanControls();
            humanPlacementMode = false;
            humanSelectedOwner = -1;
            humanSavedScreen = 1;
            humanSavedSubMode = 0;
            humanSavedTabs = null;
            humanCameraFailureLogged = false;
            activeWall = null;
            pendingWall = null;
            activeSpecial = null;
            lock (moatLock) pendingMoat.Clear();
            lock (pitchLock) pendingPitch.Clear();
            pitchClickScope = null;
            wallPreviewDiagnostics = 0;
            wallInputDiagnostics = 0;
            wallPressDiagnostics = wallReleaseDiagnostics = wallQueueDiagnostics = 0;
            wallOrderDiagnostics = 0;
            wallCostDiagnostics = 0;
            specialInputDiagnostics = pitchCostDiagnostics = 0;
            moatInputDiagnostics = moatOutcomeDiagnostics = deleteDiagnostics = 0;
            pendingBuilding = null;
            buildingClickScope = null;
            buildingWall = null;
            placingUnitOwner = 0;
            rawMouseReleasePending = false;
            RestoreDateLayout();
            activeHud = null;
            initialLayoutLoggedHud = null;
            lastOwner = int.MinValue;
            lastScreen = int.MinValue;
            pendingVisibilityHud = null;
            pendingVisibilityAt = DateTime.MaxValue;
        }

        private static bool IsSpectatorSession()
        {
            try
            {
                var state = GameData.Instance?.lastGameState;
                var director = Director.instance;
                var editor = EditorDirector.instance;
                var view = MainViewModel.Instance;
                return CurrentSessionId > 0 && state != null && director != null &&
                    editor != null && view != null &&
                    state.game_type == 3 && state.spectatorMode != 0 &&
                    editor.ActivePlayerID <= 0 && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !Shared.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static bool IsHumanSingleplayerSession()
        {
            try
            {
                var state = GameData.Instance?.lastGameState;
                var director = Director.instance;
                var editor = EditorDirector.instance;
                var view = MainViewModel.Instance;
                int controlled = PlayerPerspectiveAPI.GetControlledPlayerId();
                return CurrentSessionId > 0 && state != null && director != null &&
                    editor != null && view != null &&
                    state.game_type == 3 && state.spectatorMode == 0 &&
                    controlled >= 1 && controlled <= 8 && controlled == editor.ActivePlayerID &&
                    state.is_valid_player(controlled) && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !Shared.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static bool IsPlacementSession() => IsSpectatorSession() ||
            (humanPlacementMode && IsHumanSingleplayerSession());

        private static bool IsSelectableAi(int owner)
        {
            try
            {
                var state = GameData.Instance?.lastGameState;
                return state != null && owner >= 1 && owner <= 8 &&
                    state.is_skirmish_player(owner) &&
                    GamePlayerManagerAPI.Instance.IsAIPlayer(owner);
            }
            catch { return false; }
        }

        private static int CurrentAiOwner()
        {
            if (humanPlacementMode && IsHumanSingleplayerSession())
                return IsSelectableAi(humanSelectedOwner) ? humanSelectedOwner : -1;
            if (!IsSpectatorSession()) return -1;
            try
            {
                int owner = PlayerPerspectiveAPI.GetViewedPlayerId();
                return IsSelectableAi(owner) ? owner : -1;
            }
            catch { return -1; }
        }

        private static void EnsureHumanControls()
        {
            IngameUIScreens screen = MainViewModel.Instance?.IngameUI;
            HUD_Main hud = MainViewModel.Instance?.HUDmain;
            if (humanScreen != null && (!ReferenceEquals(screen, humanScreen) ||
                !ReferenceEquals(hud, humanBoundHud)))
            {
                MainControls.instance?.StopAllPlacement();
                humanPlacementMode = false;
                Interlocked.Exchange(ref revealHiddenActive, 0);
                humanSelectedOwner = -1;
                humanSavedTabs = null;
                activeWall = null;
                activeSpecial = null;
                RestoreDateLayout();
                activeHud = null;
                DetachHumanControls();
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_HUD_CHANGED");
            }
            if (screen == null || hud == null || humanPanel != null) return;
            Canvas canvas = screen.FindName("SpectatorEditorHumanCanvas") as Canvas;
            Border panel = screen.FindName("SpectatorEditorHumanPanel") as Border;
            Border dragHandle = screen.FindName("SpectatorEditorHumanDrag") as Border;
            Button toggle = screen.FindName("SpectatorEditorHumanToggle") as Button;
            TextBlock toggleText = screen.FindName("SpectatorEditorHumanToggleText") as TextBlock;
            StackPanel owners = screen.FindName("SpectatorEditorHumanOwners") as StackPanel;
            if (canvas == null || panel == null || dragHandle == null || toggle == null ||
                toggleText == null || owners == null)
                return;
            var found = new Button[9];
            var numbers = new TextBlock[9];
            for (int owner = 1; owner <= 8; owner++)
            {
                found[owner] = screen.FindName("SpectatorEditorHumanOwner" + owner) as Button;
                numbers[owner] = screen.FindName("SpectatorEditorHumanNumber" + owner) as TextBlock;
                if (found[owner] == null || numbers[owner] == null) return;
            }
            humanScreen = screen;
            humanBoundHud = hud;
            humanCanvas = canvas;
            humanPanel = panel;
            humanDragHandle = dragHandle;
            humanToggle = toggle;
            humanToggleText = toggleText;
            humanOwners = owners;
            toggle.Click += ToggleHumanMode;
            dragHandle.MouseLeftButtonDown += OnHumanDragDown;
            dragHandle.MouseMove += OnHumanDragMove;
            dragHandle.MouseLeftButtonUp += OnHumanDragUp;
            dragHandle.LostMouseCapture += OnHumanLostCapture;
            canvas.SizeChanged += OnHumanCanvasSizeChanged;
            for (int owner = 1; owner <= 8; owner++)
            {
                int selected = owner;
                humanOwnerButtons[owner] = found[owner];
                humanOwnerNumbers[owner] = numbers[owner];
                humanDisplayedColours[owner] = -1;
                humanOwnerHandlers[owner] = (sender, args) => SelectHumanAi(selected);
                found[owner].Click += humanOwnerHandlers[owner];
                humanJumpHandlers[owner] = (sender, args) =>
                {
                    if (args.ChangedButton != MouseButton.Right &&
                        args.ChangedButton != MouseButton.Middle) return;
                    if (!humanPlacementMode || !IsHumanSingleplayerSession() ||
                        !IsSelectableAi(selected)) return;
                    args.Handled = true;
                    JumpToHumanAi(selected, args.ChangedButton);
                };
                found[owner].PreviewMouseDown += humanJumpHandlers[owner];
            }
            humanPositioned = false;
            humanUserMoved = false;
            log.LogInfo("SPECTATOR_EDITOR_HUMAN_CONTROLS_READY");
        }

        private static void DetachHumanControls()
        {
            if (humanToggle != null) humanToggle.Click -= ToggleHumanMode;
            if (humanDragHandle != null)
            {
                humanDragHandle.MouseLeftButtonDown -= OnHumanDragDown;
                humanDragHandle.MouseMove -= OnHumanDragMove;
                humanDragHandle.MouseLeftButtonUp -= OnHumanDragUp;
                humanDragHandle.LostMouseCapture -= OnHumanLostCapture;
                if (humanDragHandle.IsMouseCaptured) humanDragHandle.ReleaseMouseCapture();
            }
            if (humanCanvas != null) humanCanvas.SizeChanged -= OnHumanCanvasSizeChanged;
            for (int owner = 1; owner <= 8; owner++)
            {
                if (humanOwnerButtons[owner] != null && humanOwnerHandlers[owner] != null)
                    humanOwnerButtons[owner].Click -= humanOwnerHandlers[owner];
                if (humanOwnerButtons[owner] != null && humanJumpHandlers[owner] != null)
                    humanOwnerButtons[owner].PreviewMouseDown -= humanJumpHandlers[owner];
                humanOwnerButtons[owner] = null;
                humanOwnerNumbers[owner] = null;
                humanOwnerHandlers[owner] = null;
                humanJumpHandlers[owner] = null;
                humanDisplayedColours[owner] = -1;
            }
            humanDragging = false;
            humanPositioned = false;
            humanUserMoved = false;
            humanPanel = null;
            humanDragHandle = null;
            humanCanvas = null;
            humanToggle = null;
            humanToggleText = null;
            humanOwners = null;
            humanScreen = null;
            humanBoundHud = null;
        }

        private static void RefreshHumanControls(bool eligible)
        {
            if (humanPanel == null) return;
            bool show = eligible && MainViewModel.Instance.Show_HUD_Main &&
                !MainViewModel.Instance.Show_HUD_Briefing;
            humanPanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (!show) return;
            int firstAi = -1;
            for (int owner = 1; owner <= 8; owner++)
            {
                bool available = IsSelectableAi(owner);
                if (available && firstAi < 0) firstAi = owner;
                Button button = humanOwnerButtons[owner];
                button.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
                if (available) RefreshHumanOwnerColour(owner);
            }
            if (humanPlacementMode && !IsSelectableAi(humanSelectedOwner))
            {
                humanSelectedOwner = firstAi;
                if (firstAi < 0) MainControls.instance?.StopAllPlacement();
            }
            for (int owner = 1; owner <= 8; owner++)
                humanOwnerButtons[owner].Background = owner == humanSelectedOwner ?
                    ownerSelectedBrush : ownerNormalBrush;
            humanToggle.IsEnabled = humanPlacementMode || firstAi > 0;
            humanToggleText.Text = humanPlacementMode ? "Normal bauen" : "KI platzieren";
            humanOwners.Visibility = humanPlacementMode ? Visibility.Visible : Visibility.Collapsed;
            humanPanel.Width = humanPlacementMode ? 412f : 118f;
            PositionHumanPanel();
        }

        private static void RefreshHumanOwnerColour(int owner)
        {
            int[] mapping = SpriteMapping.remapColours;
            int index = mapping != null && owner < mapping.Length ? mapping[owner] : 0;
            UnityEngine.Color colour = UnityEngine.Color.white;
            if (mapping != null && index > 0 && index < mapping.Length &&
                spriteLoader.instance != null)
                spriteLoader.instance.GetGMMaterial(Enums.GM.GM_BODY_ARCHER,
                    index, 0, out colour, 0);
            byte red = (byte)(colour.r * 255f);
            byte green = (byte)(colour.g * 255f);
            byte blue = (byte)(colour.b * 255f);
            int rgb = red << 16 | green << 8 | blue;
            if (humanDisplayedColours[owner] == rgb) return;
            humanOwnerNumbers[owner].Foreground =
                new SolidColorBrush(Noesis.Color.FromRgb(red, green, blue));
            humanDisplayedColours[owner] = rgb;
        }

        private static void PositionHumanPanel()
        {
            if (humanCanvas == null || humanPanel == null ||
                humanCanvas.ActualWidth <= 0f || humanCanvas.ActualHeight <= 0f) return;
            if (!humanPositioned || !humanUserMoved)
            {
                Canvas.SetLeft(humanPanel,
                    Math.Max(0f, humanCanvas.ActualWidth - humanPanel.Width - 52f));
                Canvas.SetTop(humanPanel, 54f);
                humanPositioned = true;
            }
            ClampHumanPanel();
        }

        private static void ClampHumanPanel()
        {
            if (!humanPositioned || humanCanvas == null || humanPanel == null ||
                humanCanvas.ActualWidth <= 0f || humanCanvas.ActualHeight <= 0f) return;
            float maxLeft = Math.Max(0f, humanCanvas.ActualWidth - humanPanel.Width);
            float maxTop = Math.Max(0f, humanCanvas.ActualHeight - humanPanel.Height);
            Canvas.SetLeft(humanPanel, Math.Max(0f, Math.Min(maxLeft, (float)Canvas.GetLeft(humanPanel))));
            Canvas.SetTop(humanPanel, Math.Max(0f, Math.Min(maxTop, (float)Canvas.GetTop(humanPanel))));
        }

        private static void OnHumanDragDown(object sender, MouseButtonEventArgs args)
        {
            if (humanCanvas == null || humanPanel == null || humanDragHandle == null) return;
            humanDragStart = args.GetPosition(humanCanvas);
            humanDragOriginLeft = (float)Canvas.GetLeft(humanPanel);
            humanDragOriginTop = (float)Canvas.GetTop(humanPanel);
            humanDragging = humanDragHandle.CaptureMouse();
            if (humanDragging) humanUserMoved = true;
            args.Handled = true;
        }

        private static void OnHumanDragMove(object sender, MouseEventArgs args)
        {
            if (!humanDragging || humanCanvas == null || humanPanel == null) return;
            Point current = args.GetPosition(humanCanvas);
            Canvas.SetLeft(humanPanel,
                humanDragOriginLeft + (float)(current.X - humanDragStart.X));
            Canvas.SetTop(humanPanel,
                humanDragOriginTop + (float)(current.Y - humanDragStart.Y));
            ClampHumanPanel();
            args.Handled = true;
        }

        private static void OnHumanDragUp(object sender, MouseButtonEventArgs args)
        {
            humanDragging = false;
            if (humanDragHandle != null && humanDragHandle.IsMouseCaptured)
                humanDragHandle.ReleaseMouseCapture();
            args.Handled = true;
        }

        private static void OnHumanLostCapture(object sender, MouseEventArgs args) =>
            humanDragging = false;

        private static void OnHumanCanvasSizeChanged(object sender, SizeChangedEventArgs args) =>
            PositionHumanPanel();

        private static RadioButton[] NormalTabs(HUD_Main hud) => new[] {
            hud.RefTabBuildCastle, hud.RefTabBuildIndustry, hud.RefTabBuildFarms,
            hud.RefTabBuildTown, hud.RefTabBuildWeapons, hud.RefTabBuildFood
        };

        private static void ToggleHumanMode(object sender, RoutedEventArgs args)
        {
            if (!IsHumanSingleplayerSession()) return;
            HUD_Main hud = MainViewModel.Instance.HUDmain;
            if (hud == null || MainControls.instance == null) return;
            if (humanPlacementMode)
            {
                humanPlacementMode = false;
                Interlocked.Exchange(ref revealHiddenActive, 0);
                MainControls.instance.StopAllPlacement();
                activeWall = null;
                activeSpecial = null;
                RestoreDateLayout();
                MainViewModel.Instance.SubMode = humanSavedSubMode;
                hud.SetupNewBuildScreen(-1000);
                if (humanSavedScreen != 0) hud.SetupNewBuildScreen(humanSavedScreen);
                hud.StartScrollSwish();
                RadioButton[] tabs = NormalTabs(hud);
                if (humanSavedTabs != null)
                    for (int index = 0; index < tabs.Length; index++)
                        if (tabs[index] != null) tabs[index].IsChecked = humanSavedTabs[index];
                activeHud = null;
                lastOwner = int.MinValue;
                lastScreen = int.MinValue;
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_MODE_OFF restoredScreen=" + humanSavedScreen);
            }
            else
            {
                int firstAi = -1;
                for (int owner = 1; owner <= 8; owner++)
                    if (IsSelectableAi(owner)) { firstAi = owner; break; }
                if (firstAi < 0) return;
                humanSavedScreen = MainViewModel.Instance.buildScreenID;
                if (humanSavedScreen < 0 || humanSavedScreen >= 23) humanSavedScreen = 1;
                humanSavedSubMode = MainViewModel.Instance.SubMode;
                RadioButton[] tabs = NormalTabs(hud);
                humanSavedTabs = new bool?[tabs.Length];
                for (int index = 0; index < tabs.Length; index++)
                    humanSavedTabs[index] = tabs[index]?.IsChecked;
                MainControls.instance.StopAllPlacement();
                if (!IsSelectableAi(humanSelectedOwner)) humanSelectedOwner = firstAi;
                humanPlacementMode = true;
                Interlocked.Exchange(ref revealHiddenActive,
                    visualMapDetour != null && visualFrameDetour != null &&
                    visualResourceDetour != null ? 1 : 0);
                if (Volatile.Read(ref revealHiddenActive) == 0)
                    log.LogWarning("SPECTATOR_EDITOR_VISUAL_UNAVAILABLE: audited native hooks are not installed");
                hud.SetupNewBuildScreen(1);
                RefreshBuildIcons(hud, humanSelectedOwner, "human-enter");
                log.LogInfo("SPECTATOR_EDITOR_HUMAN_MODE_ON owner=" + humanSelectedOwner +
                    " savedScreen=" + humanSavedScreen);
            }
            RefreshHumanControls(true);
        }

        private static void SelectHumanAi(int owner)
        {
            if (!humanPlacementMode || !IsHumanSingleplayerSession() || !IsSelectableAi(owner)) return;
            if (humanSelectedOwner == owner) return;
            MainControls.instance?.StopAllPlacement();
            activeWall = null;
            activeSpecial = null;
            humanSelectedOwner = owner;
            if (MainViewModel.Instance.HUDmain != null)
                RefreshBuildIcons(MainViewModel.Instance.HUDmain, owner, "human-owner");
            RefreshHumanControls(true);
            log.LogInfo("SPECTATOR_EDITOR_HUMAN_OWNER owner=" + owner);
        }

        private static unsafe void JumpToHumanAi(int owner, MouseButton button)
        {
            if (!humanPlacementMode || !IsHumanSingleplayerSession() || !IsSelectableAi(owner))
                return;
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (players == null) return;
                if (button == MouseButton.Right)
                {
                    GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                    if (buildings == null) return;
                    int keepId = players.GetPlayerKeepId(owner);
                    if (keepId <= 0 || !buildings.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                        keep == null || keep->r_AliveState != AliveState.IsAlive ||
                        keep->r_PlayerIdOwner != owner || keep->r_GlobalId == 0 ||
                        keep->r_BuildingType < eStructs.STRUCT_KEEP_ONE ||
                        keep->r_BuildingType > eStructs.STRUCT_KEEP_FIVE) return;
                    players.SetScreenCenterToBuilding(keepId);
                }
                else if (button == MouseButton.Middle)
                {
                    GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
                    if (units == null) return;
                    int lordId = players.GetLordUnitId(owner);
                    int globalId = players.GetLordUnitGlobalId(owner);
                    if (lordId <= 0 || globalId == 0 ||
                        !APIShared.UnitAccess.TryGetById(units, lordId, out GameUnit* lord, out _) || lord == null ||
                        lord->r_AliveState != AliveState.IsAlive ||
                        lord->r_ControllableForPlayerId != owner ||
                        lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                        lord->r_CurrentHealth == 0 ||
                        lord->r_GlobalId != unchecked((uint)globalId)) return;
                    players.SetScreenCenterToUnit(lordId);
                }
            }
            catch (Exception error)
            {
                if (humanCameraFailureLogged) return;
                humanCameraFailureLogged = true;
                log.LogError("SPECTATOR_EDITOR_HUMAN_CAMERA_FAILED owner=" + owner +
                    " button=" + button + " error=" + error);
            }
        }

        private static void OnBeforeRender()
        {
            try
            {
                bool humanEligible = IsHumanSingleplayerSession();
                Interlocked.Exchange(ref revealHiddenActive,
                    humanEligible && humanPlacementMode && visualMapDetour != null &&
                    visualFrameDetour != null && visualResourceDetour != null ? 1 : 0);
                if (humanEligible) EnsureHumanControls();
                RefreshHumanControls(humanEligible);
                if (!IsPlacementSession())
                {
                    RestoreDateLayout();
                    activeHud = null;
                    initialLayoutLoggedHud = null;
                    lastOwner = int.MinValue;
                    lastScreen = int.MinValue;
                    pendingVisibilityHud = null;
                    pendingVisibilityAt = DateTime.MaxValue;
                    activeWall = null;
                    activeSpecial = null;
                    return;
                }
                MainViewModel view = MainViewModel.Instance;
                HUD_Main hud = view.HUDmain;
                if (hud == null || !view.Show_HUD_Main) return;
                bool layoutChanged = EnsurePlacementHudLayout(hud);
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
                if (IsSpectatorSession() && view.FreezeMainControls)
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
            if (!IsPlacementSession()) return;
            try { EnsurePlacementHudLayout(__instance); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_UI_ERROR: " + error); }
        }

        private static bool EnsurePlacementHudLayout(HUD_Main hud)
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
            if (!IsPlacementSession()) return;
            try { RefreshBuildIcons(__instance, CurrentAiOwner(), "tab"); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_ICONS_ERROR: " + error); }
        }

        private static bool BeforeNewBuildScreenBlank()
        {
            if (!IsPlacementSession()) return true;
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
            if (!IsPlacementSession()) return;
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
            return !IsPlacementSession();
        }

        private static void AfterGetMouseStateForEngine(int __result)
        {
            lastEditorMouseState = __result;
            if (activeWall != null && __result == 3 && wallReleaseDiagnostics++ < 16)
                log.LogInfo("SPECTATOR_EDITOR_WALL_MOUSE state=" + __result +
                    " owner=" + activeWall.Owner + " mapper=" + activeWall.Mapper +
                    " overUI=" + EditorDirector.instance.overUI());
        }

        private static void BeforeUpdateLeftMouseStateForEngine(int state)
        {
            if (state == 3 && (activeWall != null || activeSpecial != null))
                rawMouseReleasePending = true;
        }

        private static bool BeforeClearMouseStateForEngine()
        {
            return !IsPlacementSession() ||
                (activeWall == null && activeSpecial == null) ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 5 ||
                (activeWall != null &&
                    (eMappers)MainControls.instance.CurrentSubAction != activeWall.Mapper) ||
                (activeSpecial != null &&
                    (eMappers)MainControls.instance.CurrentSubAction != activeSpecial.Mapper);
        }

        private static void AfterPreDLLCallActions(ref int mouseOverX, ref int mouseOverY)
        {
            WallRequest wall = activeWall;
            int state = lastEditorMouseState;
            lastEditorMouseState = 0;
            if ((wall != null || activeSpecial != null) && state == 0 &&
                rawMouseReleasePending &&
                EditorDirector.instance != null && !EditorDirector.instance.overUI())
            {
                state = 3;
                if (wallReleaseDiagnostics++ < 16)
                    log.LogInfo("SPECTATOR_EDITOR_RELEASE_RECOVERED tile=" +
                        mouseOverX + "," + mouseOverY);
            }
            rawMouseReleasePending = false;
            if (wall != null && (state == 1 || state == 3) &&
                wallInputDiagnostics++ < 24)
                log.LogInfo("SPECTATOR_EDITOR_WALL_FORWARD state=" + state +
                    " tile=" + mouseOverX + "," + mouseOverY +
                    " action=" + MainControls.instance?.CurrentAction);
            ForwardSpecialDrag(state, mouseOverX, mouseOverY);
            if (wall == null || (state != 2 && state != 3)) return;
            if (EditorDirector.instance != null && EditorDirector.instance.overUI())
            {
                if (state == 3) activeWall = null;
                return;
            }
            if (!IsPlacementSession() || MainControls.instance == null ||
                MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != wall.Mapper ||
                !IsCurrentSession(wall.SessionId))
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
            {
                wallQueueDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_WALL_REQUEST session=" + wall.SessionId +
                    " owner=" + wall.Owner +
                    " mapper=" + wall.Mapper + " start=" + wall.StartX + "," + wall.StartY +
                    " end=" + wall.EndX + "," + wall.EndY);
            }
        }

        private static void ForwardSpecialDrag(int state, int tileX, int tileY)
        {
            SpecialDragRequest special = activeSpecial;
            if (special == null || (state != 1 && state != 2 && state != 3)) return;
            if (!IsPlacementSession() || !IsCurrentSession(special.SessionId) ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 5 ||
                (eMappers)MainControls.instance.CurrentSubAction != special.Mapper ||
                EditorDirector.instance == null || EditorDirector.instance.overUI() ||
                tileX < 0 || tileY < 0)
            {
                if (state == 3 || !IsPlacementSession()) activeSpecial = null;
                return;
            }
            if (special.Mapper == eMappers.MAPPER_MOAT)
            {
                // The original EditorDirector call already reaches the in-game moat
                // chore path. Unlike pitch, it must not be issued a second time here.
                if (state == 3) activeSpecial = null;
                return;
            }
            EngineInterface.PlaceMapperItem((int)special.Mapper, tileX, tileY, 0,
                special.Owner, false, false, state);
            if (specialInputDiagnostics++ < 24 || state == 3)
                log.LogInfo("SPECTATOR_EDITOR_SPECIAL_FORWARD owner=" + special.Owner +
                    " mapper=" + special.Mapper + " tile=" + tileX + "," + tileY +
                    " state=" + state);
            if (state == 3) activeSpecial = null;
        }

        private static bool BeforePlaceMapperItem(int item, int x, int y, int size,
            ref int player, ref bool inGameNotEditor, bool constructingOnly, int mouseState,
            out WallStoneScope __state)
        {
            __state = null;
            if (!IsPlacementSession()) return true;
            eMappers mapper = (eMappers)item;
            if (!IsAllowedMapper(mapper)) return false;
            WallRequest wall = mouseState == 3 ? pendingWall : activeWall;
            SpecialDragRequest special = activeSpecial;
            int owner = IsWallMapper(mapper) && (mouseState == 2 || mouseState == 3) &&
                wall != null && wall.Mapper == mapper ? wall.Owner : CurrentAiOwner();
            if (IsSpecialDragMapper(mapper) && special != null &&
                special.Mapper == mapper && IsCurrentSession(special.SessionId))
                owner = special.Owner;
            if (owner < 1) return false;
            player = owner;
            // Moat drawing is a live-game dig order. Keep Vanilla's in-game flag;
            // editor placement semantics are only needed by the other mappers.
            inGameNotEditor = mapper == eMappers.MAPPER_MOAT;
            if (IsWallMapper(mapper))
            {
                if (!TryRaiseStone(owner, out __state)) return false;
                if (mouseState != 0 ? wallInputDiagnostics++ < 40 :
                    wallPreviewDiagnostics++ < 12)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_NATIVE state=" + mouseState +
                        " owner=" + owner + " mapper=" + mapper + " tile=" + x + "," + y +
                        " stone=" + __state.OriginalStone);
                if (mouseState == 1 && constructingOnly)
                {
                    activeWall = new WallRequest { Owner = owner, Mapper = mapper,
                        StartX = x, StartY = y, EndX = x, EndY = y,
                        SessionId = CurrentSessionId };
                    if (wallPressDiagnostics++ < 16)
                        log.LogInfo("SPECTATOR_EDITOR_WALL_PRESS owner=" + owner +
                            " mapper=" + mapper + " tile=" + x + "," + y);
                }
                return true;
            }
            if (IsSpecialDragMapper(mapper))
            {
                if (mouseState == 1 && constructingOnly)
                    activeSpecial = new SpecialDragRequest { Owner = owner, Mapper = mapper,
                        SessionId = CurrentSessionId };
                if (mapper == eMappers.MAPPER_MOAT && constructingOnly &&
                    (mouseState == 1 || mouseState == 2 || mouseState == 3))
                    TrackMoatTile(owner, x, y, mouseState);
                if (!constructingOnly && mapper == eMappers.MAPPER_PITCH_DITCH &&
                    (mouseState == 1 || mouseState == 2))
                {
                    PitchRequest pitch = null;
                    lock (pitchLock)
                    {
                        foreach (PitchRequest existing in pendingPitch)
                            if (PitchMatches(existing, owner, x, y))
                            {
                                pitch = existing;
                                break;
                            }
                        if (pitch == null)
                        {
                            pitch = new PitchRequest { Owner = owner, X = x, Y = y,
                                SessionId = CurrentSessionId,
                                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5) };
                            pendingPitch.Add(pitch);
                        }
                    }
                    pitchClickScope = pitch;
                    if (specialInputDiagnostics++ < 24)
                        log.LogInfo("SPECTATOR_EDITOR_PITCH_REQUEST owner=" + owner +
                            " tile=" + x + "," + y + " state=" + mouseState);
                }
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
                SessionId = CurrentSessionId,
                ExpiresAtUtc = DateTime.UtcNow.AddSeconds(5) };
            buildingClickScope = request;
            pendingBuilding = request;
            log.LogInfo("SPECTATOR_EDITOR_BUILD_REQUEST owner=" + owner + " mapper=" + mapper +
                " cursor=" + x + "," + y + " origin=" + (x - scale / 2) + "," +
                (y - scale / 2));
            return true;
        }

        private static bool BeforeDeleteBuilding(int x, int y, ref int player,
            ref bool inGameNotEditor, int mouseState)
        {
            if (!humanPlacementMode || !IsHumanSingleplayerSession() ||
                MainControls.instance == null || MainControls.instance.CurrentAction != 6)
                return true;
            int owner = CurrentAiOwner();
            if (owner < 1)
            {
                if (deleteDiagnostics++ < 16)
                    log.LogInfo("SPECTATOR_EDITOR_DELETE_BLOCKED tile=" + x + "," + y +
                        " state=" + mouseState + " owner=" + owner);
                return false;
            }
            player = owner;
            inGameNotEditor = true;
            if (deleteDiagnostics++ < 32)
                log.LogInfo("SPECTATOR_EDITOR_DELETE_REQUEST session=" + CurrentSessionId +
                    " owner=" + owner + " tile=" + x + "," + y +
                    " state=" + mouseState);
            return true;
        }

        private static void AfterPlaceMapperItem()
        {
            placingUnitOwner = 0;
            pitchClickScope = null;
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
            if (__exception != null && pitchClickScope != null)
                lock (pitchLock) pendingPitch.Remove(pitchClickScope);
            pitchClickScope = null;
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

        private static bool IsSpecialDragMapper(eMappers mapper) =>
            mapper == eMappers.MAPPER_PITCH_DITCH || mapper == eMappers.MAPPER_MOAT;

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
            return request != null && request.Mapper == args.WallType &&
                DateTime.UtcNow <= request.ExpiresAtUtc &&
                IsCurrentSession(request.SessionId) &&
                WallCoordinatesMatch(request, args);
        }

        private static bool WallCoordinatesMatch(WallRequest request, BuildWallEventArgs args)
        {
            return request != null && request.Mapper == args.WallType &&
                ((NearTile(request.StartX, request.StartY, args.TileXBegin, args.TileYBegin) &&
                  NearTile(request.EndX, request.EndY, args.TileXEnd, args.TileYEnd)) ||
                 (NearTile(request.StartX, request.StartY, args.TileXEnd, args.TileYEnd) &&
                  NearTile(request.EndX, request.EndY, args.TileXBegin, args.TileYBegin)));
        }

        private static bool NearTile(int x1, int y1, int x2, int y2) =>
            Math.Abs(x1 - x2) <= 1 && Math.Abs(y1 - y2) <= 1;

        private static bool PitchMatches(PitchRequest request, int owner, int x, int y) =>
            request != null && request.Owner == owner && request.X == x && request.Y == y &&
            IsCurrentSession(request.SessionId) && DateTime.UtcNow <= request.ExpiresAtUtc;

        private static bool PitchChoreMatches(PitchRequest request)
        {
            if (request == null || nativeImageBase == 0) return false;
            return Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FF4))) == 10 &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x85F8FEC))) == 0 &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C132C))) == request.X &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1330))) == request.Y &&
                Marshal.ReadInt32(unchecked((IntPtr)(long)(nativeImageBase + 0x86C1334))) ==
                    (int)eMappers.MAPPER_PITCH_DITCH;
        }

        private static unsafe void RestoreWallResources(WallBuildScope scope)
        {
            if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(scope.Request.Owner,
                out GamePlayerResources* resources) && resources != null)
            {
                resources->r_TotalGoodsStoneBlocks = scope.OriginalStone;
                resources->r_LastBoughtWallStoneCost = scope.OriginalCost;
            }
            if (scope.HasNativeOwnerResources && scope.NativeOwner != scope.Request.Owner &&
                GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(scope.NativeOwner,
                    out GamePlayerResources* nativeResources) && nativeResources != null)
            {
                nativeResources->r_TotalGoodsStoneBlocks = scope.NativeOwnerStone;
                nativeResources->r_LastBoughtWallStoneCost = scope.NativeOwnerCost;
            }
        }

        private static void InstallVisualDetours(CrusaderLibraryLoadContext context)
        {
            if (visualMapDetour != null) return;
            NativeDetour<VisualMapDelegate> map = null;
            NativeDetour<VisualFrameDelegate> frame = null;
            NativeDetour<VisualResourceDelegate> resource = null;
            bool published = false;
            try
            {
                ulong image = unchecked((ulong)context.ModuleHandle.ToInt64());
                map = CreateVisualDetour("map", image + VisualMapRva,
                    VisualMapEntry, (VisualMapDelegate)UpdateVisualMap);
                frame = CreateVisualDetour("frame", image + VisualFrameRva,
                    VisualFrameEntry, (VisualFrameDelegate)DrawVisualFrame);
                resource = CreateVisualDetour("resource", image + VisualResourceRva,
                    VisualResourceEntry, (VisualResourceDelegate)UpdateVisualResource);
                visualMapDetour = map;
                visualFrameDetour = frame;
                visualResourceDetour = resource;
                map.Enable();
                frame.Enable();
                resource.Enable();
                VerifyVisualPatch(map, image + VisualMapRva, VisualMapEntry.Length);
                VerifyVisualPatch(frame, image + VisualFrameRva, VisualFrameEntry.Length);
                VerifyVisualPatch(resource, image + VisualResourceRva, VisualResourceEntry.Length);
                published = true;
                log.LogInfo("SPECTATOR_EDITOR_VISUAL_READY map=0x65830 frame=0x41D60 resource=0x6E620");
            }
            catch (Exception error)
            {
                if (!published)
                {
                    Interlocked.Exchange(ref revealHiddenActive, 0);
                    visualResourceDetour = null;
                    visualFrameDetour = null;
                    visualMapDetour = null;
                    resource?.Dispose();
                    frame?.Dispose();
                    map?.Dispose();
                }
                log.LogError("SPECTATOR_EDITOR_VISUAL_DISABLED: " + error);
            }
        }

        private static NativeDetour<T> CreateVisualDetour<T>(string name, ulong entry,
            byte[] expected, T callback) where T : Delegate
        {
            VerifyVisualEntry(entry, expected);
            ProbeVisualBackend(entry, expected, callback);
            VerifyVisualEntry(entry, expected);
            var request = new DetourRequest<T> {
                Name = "SpectatorEditorBuildTest visual " + name,
                TargetAddress = entry, Callback = callback
            };
            var candidate = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
            if (candidate == null || candidate.Scheme.ToString() != "Indirect" ||
                candidate.DisplacedByteCount != expected.Length ||
                candidate.TargetAddress != entry || candidate.IsInstalled)
            {
                candidate?.Dispose();
                throw new InvalidOperationException("Visual detour candidate differs: " + name);
            }
            return candidate;
        }

        private static void VerifyVisualEntry(ulong entry, byte[] expected)
        {
            var bytes = new byte[32];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            for (int i = 0; i < expected.Length; i++)
                if (bytes[i] != expected[i])
                    throw new InvalidOperationException("Visual native entry mismatch at " + i);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            int displaced = 0;
            while (displaced < expected.Length)
            {
                Instruction instruction = decoder.Decode();
                if (decoder.LastError != DecoderError.None || instruction.IsInvalid ||
                    instruction.FlowControl != FlowControl.Next)
                    throw new InvalidOperationException("Visual entry has an unexpected instruction.");
                displaced += instruction.Length;
            }
            if (displaced != expected.Length)
                throw new InvalidOperationException("Visual entry crosses audited instructions.");
        }

        private static void ProbeVisualBackend<T>(ulong entry, byte[] expected,
            T callback) where T : Delegate
        {
            IntPtr copy = Marshal.AllocHGlobal(64);
            var bytes = new byte[64];
            Marshal.Copy(unchecked((IntPtr)(long)entry), bytes, 0, bytes.Length);
            NativeDetour<T> probe = null;
            try
            {
                Marshal.Copy(bytes, 0, copy, bytes.Length);
                ulong target = unchecked((ulong)copy.ToInt64());
                var request = new DetourRequest<T> {
                    Name = "SpectatorEditorBuildTest copied visual probe",
                    TargetAddress = target, Callback = callback
                };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<T>;
                if (probe == null || probe.Scheme.ToString() != "Indirect" ||
                    probe.DisplacedByteCount != expected.Length || probe.IsInstalled)
                    throw new InvalidOperationException("Visual copied backend probe differs.");
                probe.Enable();
                VerifyVisualPatch(probe, target, expected.Length);
            }
            finally
            {
                probe?.Dispose();
                var restored = new byte[bytes.Length];
                Marshal.Copy(copy, restored, 0, restored.Length);
                if (!EqualBytes(bytes, restored))
                    throw new InvalidOperationException("Visual probe was not restored.");
                Marshal.FreeHGlobal(copy);
            }
        }

        private static bool EqualBytes(byte[] left, byte[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }

        private static void VerifyVisualPatch<T>(NativeDetour<T> detour, ulong entry,
            int displaced) where T : Delegate
        {
            if (!detour.IsInstalled || detour.TargetAddress != entry ||
                detour.Scheme.ToString() != "Indirect" ||
                detour.DisplacedByteCount != displaced ||
                detour.PointerSlot == IntPtr.Zero || detour.HookEntryPointAddress == IntPtr.Zero ||
                detour.OriginalEntryPointAddress == IntPtr.Zero ||
                detour.TrampolineAddress == IntPtr.Zero || detour.ChainDepth != 1)
                throw new InvalidOperationException("Visual detour backend contract differs.");
            var patch = new byte[6];
            Marshal.Copy(unchecked((IntPtr)(long)entry), patch, 0, patch.Length);
            if (patch[0] != 0xFF || patch[1] != 0x25 ||
                checked((long)entry + 6 + BitConverter.ToInt32(patch, 2)) !=
                    detour.PointerSlot.ToInt64() ||
                Marshal.ReadInt64(detour.PointerSlot) != detour.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("Visual detour patch form differs.");
        }

        private static unsafe int EnterVisualSpectator()
        {
            if (Volatile.Read(ref revealHiddenActive) == 0 || nativeImageBase == 0) return -1;
            int* flag = (int*)(nativeImageBase + SpectatorFlagRva);
            int previous = *flag;
            *flag = 1;
            return previous;
        }

        private static unsafe void LeaveVisualSpectator(int previous)
        {
            if (previous >= 0 && nativeImageBase != 0)
                *(int*)(nativeImageBase + SpectatorFlagRva) = previous;
        }

        private static void UpdateVisualMap(IntPtr tileManager)
        {
            int previous = EnterVisualSpectator();
            try
            {
                if (previous >= 0 && Interlocked.Exchange(ref visualMapLogged, 1) == 0)
                    log.LogInfo("SPECTATOR_EDITOR_VISUAL_ACTIVE source=map");
                visualMapDetour.Original(tileManager);
            }
            finally { LeaveVisualSpectator(previous); }
        }

        private static void DrawVisualFrame(IntPtr map, int left, int right,
            int top, int bottom, int zoom)
        {
            int previous = EnterVisualSpectator();
            try
            {
                if (previous >= 0 && Interlocked.Exchange(ref visualFrameLogged, 1) == 0)
                    log.LogInfo("SPECTATOR_EDITOR_VISUAL_ACTIVE source=frame");
                visualFrameDetour.Original(map, left, right, top, bottom, zoom);
            }
            finally { LeaveVisualSpectator(previous); }
        }

        private static byte UpdateVisualResource(IntPtr tileManager, int buildingId)
        {
            int previous = EnterVisualSpectator();
            try
            {
                if (previous >= 0 && Interlocked.Exchange(ref visualResourceLogged, 1) == 0)
                    log.LogInfo("SPECTATOR_EDITOR_VISUAL_ACTIVE source=resource");
                return visualResourceDetour.Original(tileManager, buildingId);
            }
            finally { LeaveVisualSpectator(previous); }
        }

        private static unsafe void OnBuildWall(BuildWallEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                WallRequest request = pendingWall;
                if (wallOrderDiagnostics++ < 40)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EVENT phase=Pre owner=" + args.PlayerId +
                        " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                        args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                        " pending=" + (request == null ? "none" : request.Owner + "/" +
                            request.Mapper + "/" + request.StartX + "," + request.StartY + "-" +
                            request.EndX + "," + request.EndY));
                int nativeOwner = args.PlayerId;
                if (!WallMatches(request, args) ||
                    (nativeOwner != request.Owner &&
                     nativeOwner != PlayerPerspectiveAPI.GetControlledPlayerId()) ||
                    !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(request.Owner,
                        out GamePlayerResources* resources) || resources == null) return;
                uint nativeStone = 0, nativeCost = 0;
                bool hasNativeResources = false;
                if (nativeOwner != request.Owner &&
                    GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(nativeOwner,
                        out GamePlayerResources* nativeResources) && nativeResources != null)
                {
                    nativeStone = nativeResources->r_TotalGoodsStoneBlocks;
                    nativeCost = nativeResources->r_LastBoughtWallStoneCost;
                    hasNativeResources = true;
                }
                pendingWall = null;
                buildingWall = new WallBuildScope { Request = request,
                    OriginalStone = resources->r_TotalGoodsStoneBlocks,
                    OriginalCost = resources->r_LastBoughtWallStoneCost,
                    NativeOwner = nativeOwner, NativeOwnerStone = nativeStone,
                    NativeOwnerCost = nativeCost,
                    HasNativeOwnerResources = hasNativeResources };
                args.PlayerId = request.Owner;
                log.LogInfo("SPECTATOR_EDITOR_WALL_MATCH session=" + request.SessionId +
                    " owner=" + request.Owner + " nativeOwner=" + nativeOwner +
                    " mapper=" + request.Mapper + " stone=" + resources->r_TotalGoodsStoneBlocks);
                if (resources->r_TotalGoodsStoneBlocks < 125)
                    resources->r_TotalGoodsStoneBlocks = 125;
                return;
            }
            WallBuildScope scope = buildingWall;
            if (scope == null || !WallCoordinatesMatch(scope.Request, args)) return;
            RestoreWallResources(scope);
            buildingWall = null;
            log.LogInfo("SPECTATOR_EDITOR_WALL_BUILT owner=" + args.PlayerId +
                " mapper=" + args.WallType + " start=" + args.TileXBegin + "," +
                args.TileYBegin + " end=" + args.TileXEnd + "," + args.TileYEnd +
                " stone=" + scope.OriginalStone + " skippedDeductions=" +
                scope.Subtractions + " remainder=" + scope.OriginalCost);
        }

        private static void OnSubtractResource(PlayerSubtractResourceEventArgs args)
        {
            if (args.Good == eGoods.STORED_PITCH_RAW)
            {
                lock (pitchLock)
                    foreach (PitchRequest pitch in pendingPitch)
                        if (PitchMatches(pitch, args.PlayerId, pitch.X, pitch.Y) &&
                            PitchChoreMatches(pitch))
                        {
                            args.SkipOriginalFunction = true;
                            if (pitchCostDiagnostics++ < 24)
                                log.LogInfo("SPECTATOR_EDITOR_PITCH_COST_SKIPPED owner=" +
                                    args.PlayerId + " tile=" + pitch.X + "," + pitch.Y +
                                    " amount=" + args.Amount);
                            return;
                        }
            }
            WallBuildScope scope = buildingWall;
            if (scope == null || args.PlayerId != scope.Request.Owner ||
                args.Good != eGoods.STORED_STONE_BLOCKS) return;
            args.SkipOriginalFunction = true;
            scope.Subtractions++;
            if (wallCostDiagnostics++ < 40)
                log.LogInfo("SPECTATOR_EDITOR_WALL_COST_SKIPPED owner=" + args.PlayerId +
                    " good=" + args.Good + " amount=" + args.Amount);
        }

        private static void OnBuildPitchDitch(BuildPitchDitchEventArgs args)
        {
            lock (pitchLock)
                foreach (PitchRequest pitch in pendingPitch)
                    if (PitchMatches(pitch, args.PlayerId, args.TileX, args.TileY) &&
                        PitchChoreMatches(pitch))
                    {
                        if (args.Phase == EventHookPhase.Post) pitch.Built = args.ReturnValue > 0;
                        if (pitchCostDiagnostics++ < 24)
                            log.LogInfo("SPECTATOR_EDITOR_PITCH_BUILD phase=" + args.Phase +
                                " owner=" + args.PlayerId + " tile=" + args.TileX + "," +
                                args.TileY + " result=" + args.ReturnValue);
                        return;
                    }
        }

        private static unsafe Exception FinalizeBuildWall(Exception __exception)
        {
            WallBuildScope scope = buildingWall;
            if (scope == null) return __exception;
            RestoreWallResources(scope);
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

        private static unsafe void TrackMoatTile(int owner, int x, int y, int mouseState)
        {
            if (x < 0 || x >= 800 || y < 0 || y >= 800) return;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles == null) return;
            int tileId = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(tileId)) return;
            lock (moatLock)
            {
                if (moatInputDiagnostics >= 32) return;
                foreach (MoatProbe probe in pendingMoat)
                    if (probe.Owner == owner && probe.X == x && probe.Y == y &&
                        IsCurrentSession(probe.SessionId)) return;
                var probeNew = new MoatProbe {
                    Owner = owner, X = x, Y = y, SessionId = CurrentSessionId,
                    BeforeIndex = tiles.GetMoatWorkTaskIndexAtTile(x, y),
                    BeforeFlags = (uint)tiles.GetTilePropertyFlag(tileId),
                    ExpiresAtUtc = DateTime.UtcNow.AddSeconds(3)
                };
                pendingMoat.Add(probeNew);
                moatInputDiagnostics++;
                log.LogInfo("SPECTATOR_EDITOR_MOAT_INPUT session=" + probeNew.SessionId +
                    " owner=" + owner + " tile=" + x + "," + y +
                    " state=" + mouseState + " priorTask=" + probeNew.BeforeIndex +
                    " priorFlags=0x" + probeNew.BeforeFlags.ToString("X8") +
                    " activeTasks=" + tiles.GetMoatWorkTaskActiveCount());
            }
        }

        private static unsafe void CheckMoatProbes(int tick)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (tiles == null) return;
            lock (moatLock)
            {
                for (int index = pendingMoat.Count - 1; index >= 0; index--)
                {
                    MoatProbe probe = pendingMoat[index];
                    if (!IsCurrentSession(probe.SessionId))
                    {
                        pendingMoat.RemoveAt(index);
                        continue;
                    }
                    int tileId = tiles.GetTileId(probe.X, probe.Y);
                    if (!tiles.IsValidTileId(tileId))
                    {
                        pendingMoat.RemoveAt(index);
                        continue;
                    }
                    int taskIndex = tiles.GetMoatWorkTaskIndexAtTile(probe.X, probe.Y);
                    uint flags = (uint)tiles.GetTilePropertyFlag(tileId);
                    int taskOwner = 0;
                    Span<MoatWorkTask> tasks = tiles.GetMoatWorkTasks();
                    if (taskIndex > 0 && taskIndex < tasks.Length &&
                        tasks[taskIndex].r_TileId == tileId)
                        taskOwner = tasks[taskIndex].r_OwnerPlayerId;
                    probe.Samples++;
                    bool completed = taskIndex != probe.BeforeIndex ||
                        ((flags ^ probe.BeforeFlags) & (uint)TilePropertyFlag.PlannedMoat) != 0;
                    if (!completed && probe.Samples < 5 &&
                        DateTime.UtcNow < probe.ExpiresAtUtc) continue;
                    if (moatOutcomeDiagnostics++ < 32)
                        log.LogInfo("SPECTATOR_EDITOR_MOAT_OUTCOME tick=" + tick +
                            " session=" + probe.SessionId + " owner=" + probe.Owner +
                            " tile=" + probe.X + "," + probe.Y +
                            " task=" + taskIndex + " taskOwner=" + taskOwner +
                            " flags=0x" + flags.ToString("X8") +
                            " activeTasks=" + tiles.GetMoatWorkTaskActiveCount() +
                            " samples=" + probe.Samples);
                    pendingMoat.RemoveAt(index);
                    if (completed && taskOwner == probe.Owner &&
                        humanPlacementMode && IsHumanSingleplayerSession())
                        tiles.RefreshTileAreaVisuals(probe.X, probe.Y);
                }
            }
        }

        private static void OnSimulationTick(int tick)
        {
            try { CheckMoatProbes(tick); }
            catch (Exception error) { LogOnce("SPECTATOR_EDITOR_MOAT_DIAGNOSTIC_ERROR: " + error); }
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
                if (wallOrderDiagnostics++ < 40)
                    log.LogInfo("SPECTATOR_EDITOR_WALL_EXPIRED tick=" + tick +
                        " owner=" + wall.Owner + " mapper=" + wall.Mapper);
            }
            lock (pitchLock)
                pendingPitch.RemoveAll(pitch => DateTime.UtcNow > pitch.ExpiresAtUtc ||
                    !IsCurrentSession(pitch.SessionId));
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
                !IsCurrentSession(request.SessionId)) return;
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
                if (!APIShared.UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _) ||
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
