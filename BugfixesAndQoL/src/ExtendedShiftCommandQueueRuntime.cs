using BepInEx.Logging;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL
{
    internal sealed unsafe class ExtendedShiftCommandQueueRuntime
    {
        private const int ReferenceWaypointAppendRva = 0x11C3A0;
        private const int ReferenceMovementCompleteRva = 0x1178D0;
        private const int ReferenceTribeOverlayRenderRva = 0x1222A0;
        private const int ReferenceDrawSubmissionRva = 0x417A0;
        private const int GameTribeSize = 0x688;
        private const int MaximumNativeMovementWaypoints = 10;
        // Native movement code addresses these as manager + tribeId * 0x688 +
        // 0x5DC/0x5DE. A public GameTribe* starts 0x2A bytes later.
        private const int MovementWaypointIndexOffset = QueueNativeContract.GameTribeWaypointIndexOffset;
        private const int MovementWaypointCountOffset = QueueNativeContract.GameTribeWaypointCountOffset;
        private const int MovementModeOffset = QueueNativeContract.GameTribeMovementModeOffset;
        private const int MaximumPendingCommands = 128;
        private const int ExpectedMoveChoreLifetimeTicks = 30;

        private const string MoveChoreHandlerPattern =
            "40 53 48 83 EC 30 8B 05 00 85 5E 08 C7 05 FE 84 5E 08 08 00 00 00 83 F8 01 " +
            "0F 85 A4 00 00 00 33 DB 44 8D 40 01 44 8B C8 89 5C 24 20 48 8D 15 19 08 6B 08 " +
            "48 8D 0D 06 38 56 08 E8 D1 EA 00 00";

        private const string TargetOrderChoreHandlerPattern =
            "40 53 48 83 EC 30 8B 05 F0 63 5E 08 C7 05 EE 63 5E 08 0F 00 00 00 83 F8 01 " +
            "0F 85 A3 00 00 00 33 DB 44 8D 40 01 44 8B C8 89 5C 24 20 48 8D 15 09 E7 6A 08 " +
            "48 8D 0D F6 16 56 08 E8 C1 C9 00 00";

        // Complete 71-byte body of FUN_18011C3A0 for FBCB...31E2. Unlike a loose prologue
        // signature this also proves all writes and the exact RET boundary used by the detour.
        private const string WaypointAppendPattern =
            "4C 63 5C 24 28 4C 63 D2 49 69 C2 88 06 00 00 49 69 D2 A2 01 00 00 " +
            "49 03 D3 66 41 FF C3 66 44 89 84 91 B4 05 00 00 66 44 89 8C 91 B6 05 00 00 " +
            "48 03 C8 0F B7 44 24 30 66 44 89 99 DE 05 00 00 66 89 81 82 05 00 00 C3";

        private const string MovementCompletePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 48 89 7C 24 20 41 56 48 83 EC 20 " +
            "48 63 F2 33 DB 48 69 FE 88 06 00 00 48 8B E9 66 3B 5C 0F 5C 7D 58";

        private const string TribeOverlayRenderPattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 40 4C 63 E2 4C 8D 2D FA FA 8E 03 4D 69 F4 88 06 00 00 4C 8B F9";

        private const string DrawSubmissionPattern =
            "48 89 5C 24 08 48 89 74 24 10 48 89 7C 24 18 48 63 5C 24 30 41 8B F1 48 63 B9 " +
            "48 22 62 00 44 8B DA 4C 8B D1 85 DB 0F 88 AC 00 00 00 81 FF FA 00";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AppendMovementWaypointDelegate(
            IntPtr tribeManager,
            int tribeId,
            ushort tileX,
            ushort tileY,
            int waypointIndex,
            short moveMode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long IsTribeMovementCompleteDelegate(IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void RenderTribeOverlayDelegate(IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DrawSubmissionDelegate(
            IntPtr drawManager,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ChoreHandlerDelegate();

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetDiagnosticsRuntime largeMoveTargets;
        // A cohort is the smallest set of units that currently shares mutable queue progress.
        // Unit identities remain authoritative; BoundTribeId is only the current dispatch vessel.
        private readonly Dictionary<long, TribeQueueState> cohorts = new Dictionary<long, TribeQueueState>();
        private readonly Dictionary<QueueUnitIdentity, long> unitToCohort =
            new Dictionary<QueueUnitIdentity, long>();
        private readonly Dictionary<int, ObservedAttack> observedAttacks = new Dictionary<int, ObservedAttack>();
        private readonly HashSet<int> loggedUnsupportedCommands = new HashSet<int>();
        private readonly HashSet<long> loggedPredecessorRedispatchFailures = new HashSet<long>();
        private readonly HashSet<long> loggedIsolationFailures = new HashSet<long>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<long> cohortIdBuffer = new List<long>();
        private readonly List<TribeQueueState> overlayCohortBuffer = new List<TribeQueueState>();
        private readonly List<RuntimeExpectedMove> expectedMoveChores = new List<RuntimeExpectedMove>();
        private readonly List<RuntimeExpectedMove> expectedMoveEvents = new List<RuntimeExpectedMove>();
        private readonly List<VisualQueueEntry> visualEntryBuffer = new List<VisualQueueEntry>(9);
        private readonly List<QueueVisualMarkerMode> projectedModeBuffer =
            new List<QueueVisualMarkerMode>(9);
        private readonly Stack<MoveObservationScope> moveObservationScopes =
            new Stack<MoveObservationScope>();
        private HookTransaction nativeTransaction;
        private HookTransaction multiplayerTransaction;
        private HookTransaction drawFilterTransaction;
        private readonly DetourHandle<AppendMovementWaypointDelegate> waypointAppendHook =
            new DetourHandle<AppendMovementWaypointDelegate>();
        private readonly DetourHandle<RenderTribeOverlayDelegate> tribeOverlayRenderHook =
            new DetourHandle<RenderTribeOverlayDelegate>();
        private readonly DetourHandle<DrawSubmissionDelegate> drawSubmissionHook =
            new DetourHandle<DrawSubmissionDelegate>();
        private readonly DetourHandle<ChoreHandlerDelegate> moveChoreHandlerHook =
            new DetourHandle<ChoreHandlerDelegate>();
        private readonly DetourHandle<ChoreHandlerDelegate> targetOrderChoreHandlerHook =
            new DetourHandle<ChoreHandlerDelegate>();
        private IsTribeMovementCompleteDelegate isTribeMovementComplete;
        private IntPtr tribeManagerPointer;
        private IntPtr choreModePointer;
        private IntPtr choreTribeIdPointer;
        private IntPtr choreCommandOrTileXPointer;
        private IntPtr choreMoveTypePointer;
        private bool installed;
        private bool multiplayerSynchronizationReady;
        private bool multiplayerMarkerFailureLogged;
        private bool? lastRealMultiplayerMode;
        private bool internalDispatch;
        private bool runtimeTickLogged;
        private bool? lastFeatureEnabled;
        private int currentTick;
        private long nextCohortId = 1;
        private bool drawFilterInstalled;
        private bool drawFilterFailureLogged;
        private bool targetMarkerProjectionAvailable;
        private bool overlayDrawFilterActive;
        private bool overlayShowPageNumbers;
        private bool overlaySawQueueMarker;
        private int overlayRenderThreadId;
        private int overlayFlagCallIndex;
        private int overlayNumberCallIndex;
        private IReadOnlyList<QueueVisualMarkerMode> overlayProjectedModes =
            Array.Empty<QueueVisualMarkerMode>();

        public ExtendedShiftCommandQueueRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            largeMoveTargets = new LargeMoveTargetDiagnosticsRuntime(log);
        }

        private bool FeatureEnabled =>
            settings.EnableMod && settings.EnableExtendedShiftCommandQueue;

        public void Install(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            if (installed)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The Extended Shift command queue requires the validated fixed native layout.");
            }

            IntPtr libraryHandle = context.ModuleHandle;
            ReadOnlySpan<byte> memory = context.Memory;
            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());

            int actualTribeSize = Marshal.SizeOf(typeof(GameTribe));
            if (actualTribeSize != GameTribeSize)
            {
                throw new InvalidOperationException(
                    $"GameTribe layout mismatch: expected=0x{GameTribeSize:X}, actual=0x{actualTribeSize:X}.");
            }
            int actualUnitSize = Marshal.SizeOf(typeof(GameUnit));
            int actualUnitGlobalIdOffset = Marshal.OffsetOf(
                typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32();
            bool largeMoveLayoutAvailable =
                actualUnitSize == QueueNativeContract.GameUnitSize &&
                actualUnitGlobalIdOffset == QueueNativeContract.GameUnitGlobalIdOffset &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX)).ToInt32() == 0xC0 &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_NextTilePositionX2)).ToInt32() == 0xDC &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.p_CurrentPathPlanPosition)).ToInt32() == 0xF6 &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.p_PathPlanSize)).ToInt32() == 0xF8 &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TribeId)).ToInt32() == 0x2D4 &&
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AttackMoveToTargetTileX)).ToInt32() == 0x2D8;
            int actualBuildingSize = Marshal.SizeOf(typeof(GameBuilding));
            int actualBuildingGlobalIdOffset = Marshal.OffsetOf(
                typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32();
            targetMarkerProjectionAvailable = actualUnitSize == QueueNativeContract.GameUnitSize &&
                actualUnitGlobalIdOffset == QueueNativeContract.GameUnitGlobalIdOffset &&
                actualBuildingSize == QueueNativeContract.GameBuildingSize &&
                actualBuildingGlobalIdOffset == QueueNativeContract.GameBuildingGlobalIdOffset;
            if (!targetMarkerProjectionAvailable)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"VISUAL_TARGET_MARKER_FALLBACK: layout mismatch; " +
                    $"unit=0x{actualUnitSize:X}/0x{actualUnitGlobalIdOffset:X}, " +
                    $"building=0x{actualBuildingSize:X}/0x{actualBuildingGlobalIdOffset:X}. " +
                    "Command queues and destination flags remain enabled; attack icons are unavailable.");
            }
            int actualWaypointIndexOffset = Marshal.OffsetOf(
                typeof(GameTribe),
                nameof(GameTribe.r_PatrolCurrentTargetIndex)).ToInt32();
            int actualWaypointBaseOffset = Marshal.OffsetOf(
                typeof(GameTribe),
                nameof(GameTribe.r_PatrolPoint1TileX)).ToInt32();
            int actualWaypointCountOffset = Marshal.OffsetOf(
                typeof(GameTribe),
                nameof(GameTribe.r_CurrentPatrolPoints)).ToInt32();
            int actualMovementModeOffset = Marshal.OffsetOf(
                typeof(GameTribe),
                nameof(GameTribe.r_PatrolMode)).ToInt32();
            if (actualWaypointBaseOffset != QueueNativeContract.GameTribeWaypointBaseOffset ||
                actualWaypointIndexOffset != MovementWaypointIndexOffset ||
                actualWaypointCountOffset != MovementWaypointCountOffset ||
                actualMovementModeOffset != MovementModeOffset)
            {
                throw new InvalidOperationException(
                    $"GameTribe movement layout mismatch: expected=0x{QueueNativeContract.GameTribeWaypointBaseOffset:X}/" +
                    $"0x{MovementWaypointIndexOffset:X}/" +
                    $"0x{MovementWaypointCountOffset:X}/0x{MovementModeOffset:X}, " +
                    $"actual=0x{actualWaypointBaseOffset:X}/0x{actualWaypointIndexOffset:X}/" +
                    $"0x{actualWaypointCountOffset:X}/" +
                    $"0x{actualMovementModeOffset:X}.");
            }

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                WaypointAppendPattern,
                ReferenceWaypointAppendRva,
                referenceHashMatches,
                name: "Vanilla movement-waypoint append helper",
                log: null);
            if (resolution.Rva != ReferenceWaypointAppendRva)
            {
                throw new InvalidOperationException(
                    $"Waypoint helper resolved at unexpected RVA 0x{resolution.Rva:X}.");
            }

            Shared.NativeResolution movementCompleteResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MovementCompletePattern,
                ReferenceMovementCompleteRva,
                referenceHashMatches,
                name: "Vanilla tribe-movement completion predicate",
                log: null);
            if (movementCompleteResolution.Rva != ReferenceMovementCompleteRva)
            {
                throw new InvalidOperationException(
                    $"Movement completion predicate resolved at unexpected RVA " +
                    $"0x{movementCompleteResolution.Rva:X}.");
            }

            Shared.NativeResolution overlayRenderResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                TribeOverlayRenderPattern,
                ReferenceTribeOverlayRenderRva,
                referenceHashMatches,
                name: "Vanilla tribe overlay renderer",
                log: null);
            if (overlayRenderResolution.Rva != ReferenceTribeOverlayRenderRva)
            {
                throw new InvalidOperationException(
                    $"Tribe overlay renderer resolved at unexpected RVA 0x{overlayRenderResolution.Rva:X}.");
            }

            isTribeMovementComplete = (IsTribeMovementCompleteDelegate)Marshal.GetDelegateForFunctionPointer(
                libraryHandle + ReferenceMovementCompleteRva,
                typeof(IsTribeMovementCompleteDelegate));
            tribeManagerPointer = new IntPtr(GameTribeManagerAPI.Instance.GetTribeManager().Pointer);

            nativeTransaction = new HookTransaction(
                context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                CreateTransactionOptions());
            nativeTransaction.AddDetour(
                waypointAppendHook,
                HookTarget.FromAddress(libraryBase + ReferenceWaypointAppendRva),
                AppendMovementWaypoint);
            nativeTransaction.AddDetour(
                tribeOverlayRenderHook,
                HookTarget.FromAddress(libraryBase + ReferenceTribeOverlayRenderRva),
                RenderTribeOverlay);
            CommitResult nativeCommitResult = nativeTransaction.Commit();
            if (!nativeCommitResult.IsCompleteSuccess ||
                !waypointAppendHook.Success || !tribeOverlayRenderHook.Success)
                throw new InvalidOperationException(
                    "One or more Extended Shift command queue native hooks were not installed.");

            InstallMultiplayerSynchronization(context, referenceHashMatches);
            InstallOptionalDrawFilter(context, referenceHashMatches);
            largeMoveTargets.Install(largeMoveLayoutAvailable, drawFilterInstalled);

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTargetOrder));
            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                .Subscribe(OnMoveOrder));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => OnMapStart()));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => ResetMapState()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => ResetMapState()));
            GameTimeManagerAPI.Instance.OnTick += OnTick;

            installed = true;
            ApplySetting();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"INITIALIZED: waypointRva=0x{resolution.Rva:X}, functionSize=71, " +
                $"movementCompleteRva=0x{movementCompleteResolution.Rva:X}, " +
                $"overlayRenderRva=0x{overlayRenderResolution.Rva:X}, " +
                "tribeUnassign=SHCDESE-2.2.0-wrapper, " +
                $"drawFilterInstalled={drawFilterInstalled}, " +
                $"targetMarkerProjectionAvailable={targetMarkerProjectionAvailable}, " +
                $"largeMoveLayoutAvailable={largeMoveLayoutAvailable}, " +
                $"multiplayerSynchronizationReady={multiplayerSynchronizationReady}, " +
                $"GameTribeSize=0x{actualTribeSize:X}, " +
                $"queueLimit={MaximumPendingCommands}, " +
                $"featureEnabled={FeatureEnabled}. Command capture no longer depends on an OnStartMap event.");
        }

        public void ApplySetting()
        {
            bool enabled = FeatureEnabled;
            if (lastFeatureEnabled == enabled)
                return;

            lastFeatureEnabled = enabled;
            if (!enabled)
                ResetMapState();

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Bugfixes and QoL Extended Shift command queue setting applied: enabled={enabled}.");
        }

        private void InstallMultiplayerSynchronization(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            try
            {
                IntPtr libraryHandle = context.ModuleHandle;
                ReadOnlySpan<byte> memory = context.Memory;
                ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
                Shared.NativeResolution moveResolution = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    MoveChoreHandlerPattern,
                    QueueNativeContract.MoveChoreHandlerRva,
                    referenceHashMatches,
                    name: "Vanilla Chore 17 handler",
                    log: null);
                Shared.NativeResolution targetResolution = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    TargetOrderChoreHandlerPattern,
                    QueueNativeContract.TargetOrderChoreHandlerRva,
                    referenceHashMatches,
                    name: "Vanilla Chore 36 handler",
                    log: null);
                if (moveResolution.Rva != QueueNativeContract.MoveChoreHandlerRva ||
                    targetResolution.Rva != QueueNativeContract.TargetOrderChoreHandlerRva)
                {
                    throw new InvalidOperationException("One or more Chore handlers resolved at an unexpected RVA.");
                }

                // The table is populated at runtime. Checking it proves that opcodes 17, 36 and
                // 71 still select the exact handlers whose payload layouts this feature extends.
                ValidateChoreHandlerTableEntry(libraryHandle, QueueNativeContract.MoveChoreOpcode,
                    QueueNativeContract.MoveChoreHandlerRva);
                ValidateChoreHandlerTableEntry(libraryHandle, QueueNativeContract.TargetOrderChoreOpcode,
                    QueueNativeContract.TargetOrderChoreHandlerRva);
                ValidateChoreHandlerTableEntry(libraryHandle, QueueNativeContract.WaypointAppendChoreOpcode,
                    QueueNativeContract.WaypointAppendChoreHandlerRva);

                choreModePointer = libraryHandle + QueueNativeContract.ChoreModeRva;
                choreTribeIdPointer = libraryHandle + QueueNativeContract.ChoreTribeIdRva;
                choreCommandOrTileXPointer = libraryHandle + QueueNativeContract.ChoreCommandOrTileXRva;
                choreMoveTypePointer = libraryHandle + QueueNativeContract.ChoreMoveTypeRva;

                multiplayerTransaction = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    CreateTransactionOptions());
                multiplayerTransaction.AddDetour(
                    moveChoreHandlerHook,
                    HookTarget.FromAddress(libraryBase + QueueNativeContract.MoveChoreHandlerRva),
                    HandleMoveChore);
                multiplayerTransaction.AddDetour(
                    targetOrderChoreHandlerHook,
                    HookTarget.FromAddress(libraryBase + QueueNativeContract.TargetOrderChoreHandlerRva),
                    HandleTargetOrderChore);
                CommitResult multiplayerCommitResult = multiplayerTransaction.Commit();
                multiplayerSynchronizationReady =
                    multiplayerCommitResult.IsCompleteSuccess &&
                    moveChoreHandlerHook.Success && targetOrderChoreHandlerHook.Success;
                if (!multiplayerSynchronizationReady)
                    throw new InvalidOperationException("A multiplayer Chore marker hook reported no success.");
            }
            catch (Exception exception)
            {
                multiplayerSynchronizationReady = false;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"MULTIPLAYER_QUEUE_DISABLED: Vanilla multiplayer orders remain unchanged; {exception.Message}");
            }
        }

        private static void ValidateChoreHandlerTableEntry(
            IntPtr libraryHandle,
            int opcode,
            int expectedHandlerRva)
        {
            IntPtr tableEntry = libraryHandle + QueueNativeContract.ChoreHandlerTableRva + opcode * sizeof(long);
            long actual = Marshal.ReadInt64(tableEntry);
            long expected = (libraryHandle + expectedHandlerRva).ToInt64();
            if (actual != expected)
            {
                throw new InvalidOperationException(
                    $"Chore handler table mismatch for opcode {opcode}: " +
                    $"expected=0x{expected:X}, actual=0x{actual:X}.");
            }
        }

        private void HandleMoveChore()
        {
            bool trampolineEntered = false;
            bool markerWritten = false;
            int originalMoveType = 0;
            try
            {
                int observedTribeId = Marshal.ReadInt32(choreTribeIdPointer);
                if (ShouldMarkOutgoingMultiplayerOrder())
                {
                    int tribeId = observedTribeId;
                    if (IsLocalSelectedTribe(tribeId, out _) &&
                        QueueNativeContract.TryMarkMoveTypeForQueue(
                            Marshal.ReadInt32(choreMoveTypePointer),
                            out int markedMoveType))
                    {
                        originalMoveType = Marshal.ReadInt32(choreMoveTypePointer);
                        Marshal.WriteInt32(choreMoveTypePointer, markedMoveType);
                        markerWritten = true;
                    }
                }

                trampolineEntered = true;
                moveChoreHandlerHook.Original();
            }
            catch (Exception exception)
            {
                LogMultiplayerMarkerFailure("Chore 17", exception);
                if (!trampolineEntered)
                {
                    try
                    {
                        moveChoreHandlerHook.Original();
                    }
                    catch (Exception trampolineException)
                    {
                        LogMultiplayerMarkerFailure("Chore 17 trampoline", trampolineException);
                    }
                }
            }
            finally
            {
                if (markerWritten)
                {
                    try
                    {
                        Marshal.WriteInt32(choreMoveTypePointer, originalMoveType);
                    }
                    catch (Exception restoreException)
                    {
                        LogMultiplayerMarkerFailure("Chore 17 restore", restoreException);
                    }
                }
            }
        }

        private void HandleTargetOrderChore()
        {
            bool trampolineEntered = false;
            bool markerWritten = false;
            int originalCommand = 0;
            try
            {
                if (ShouldMarkOutgoingMultiplayerOrder())
                {
                    int tribeId = Marshal.ReadInt32(choreTribeIdPointer);
                    originalCommand = Marshal.ReadInt32(choreCommandOrTileXPointer);
                    if (IsLocalSelectedTribe(tribeId, out _) &&
                        QueueNativeContract.TryMarkTargetCommandForQueue(
                            originalCommand,
                            out int markedCommand))
                    {
                        Marshal.WriteInt32(choreCommandOrTileXPointer, markedCommand);
                        markerWritten = true;
                    }
                }

                trampolineEntered = true;
                targetOrderChoreHandlerHook.Original();
            }
            catch (Exception exception)
            {
                LogMultiplayerMarkerFailure("Chore 36", exception);
                if (!trampolineEntered)
                {
                    try
                    {
                        targetOrderChoreHandlerHook.Original();
                    }
                    catch (Exception trampolineException)
                    {
                        LogMultiplayerMarkerFailure("Chore 36 trampoline", trampolineException);
                    }
                }
            }
            finally
            {
                if (markerWritten)
                {
                    try
                    {
                        Marshal.WriteInt32(choreCommandOrTileXPointer, originalCommand);
                    }
                    catch (Exception restoreException)
                    {
                        LogMultiplayerMarkerFailure("Chore 36 restore", restoreException);
                    }
                }
            }
        }

        private bool ShouldMarkOutgoingMultiplayerOrder() =>
            installed &&
            FeatureEnabled &&
            multiplayerSynchronizationReady &&
            !internalDispatch &&
            Marshal.ReadInt32(choreModePointer) == QueueNativeContract.ChorePackMode &&
            IsRealMultiplayer() &&
            IsShiftPressed();

        private void LogMultiplayerMarkerFailure(string chore, Exception exception)
        {
            if (multiplayerMarkerFailureLogged)
                return;
            multiplayerMarkerFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"MULTIPLAYER_MARKER_FAIL_OPEN: {chore}; Vanilla order retained; {exception.Message}");
        }

        private void InstallOptionalDrawFilter(
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            try
            {
                IntPtr libraryHandle = context.ModuleHandle;
                ReadOnlySpan<byte> memory = context.Memory;
                ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
                Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    DrawSubmissionPattern,
                    ReferenceDrawSubmissionRva,
                    referenceHashMatches,
                    name: "Vanilla overlay draw submission",
                    log: null);
                if (resolution.Rva != ReferenceDrawSubmissionRva)
                    throw new InvalidOperationException($"Draw submission resolved at unexpected RVA 0x{resolution.Rva:X}.");

                drawFilterTransaction = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    CreateTransactionOptions());
                drawFilterTransaction.AddDetour(
                    drawSubmissionHook,
                    HookTarget.FromAddress(libraryBase + ReferenceDrawSubmissionRva),
                    SubmitOverlayMarker);
                CommitResult drawCommitResult = drawFilterTransaction.Commit();
                drawFilterInstalled = drawCommitResult.IsCompleteSuccess && drawSubmissionHook.Success;
                if (!drawFilterInstalled)
                    throw new InvalidOperationException("Draw-submission hook reported no success.");
            }
            catch (Exception exception)
            {
                drawFilterInstalled = false;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"VISUAL_FLAG_FILTER_FALLBACK: command queues and all destination flags remain enabled, " +
                    $"but hidden markers and future-page numbers may remain visible; {exception.Message}");
            }
        }

        private void OnMapStart()
        {
            largeMoveTargets.Reset(currentTick, "map-start");
            cohorts.Clear();
            unitToCohort.Clear();
            nextCohortId = 1;
            expectedMoveChores.Clear();
            expectedMoveEvents.Clear();
            moveObservationScopes.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
            loggedPredecessorRedispatchFailures.Clear();
            loggedIsolationFailures.Clear();
        }

        private void ResetMapState()
        {
            largeMoveTargets.Reset(currentTick, "map-reset");
            cohorts.Clear();
            unitToCohort.Clear();
            nextCohortId = 1;
            expectedMoveChores.Clear();
            expectedMoveEvents.Clear();
            moveObservationScopes.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
            loggedPredecessorRedispatchFailures.Clear();
            loggedIsolationFailures.Clear();
        }

        private void OnTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!installed || internalDispatch)
                return;

            int commandValue = (int)args.AICommand;
            if (IsRealMultiplayer())
            {
                if (!multiplayerSynchronizationReady)
                    return;

                if (QueueNativeContract.TryDecodeQueuedTargetCommand(
                        commandValue,
                        out int decodedCommand))
                {
                    args.AICommand = (TribeAICommand)decodedCommand;
                    if (!FeatureEnabled)
                    {
                        // A synchronized setting change can cross an already serialized Chore.
                        // Remove the private marker but let Vanilla execute the decoded command.
                        CancelQueuesForTribeUnits(args.TribeId);
                        return;
                    }

                    if (TryGetAliveTribe(args.TribeId, out GameTribe* synchronizedTribe) &&
                        QueueCommandClassifier.TryClassifyTarget(
                            decodedCommand,
                            out QueueCommandKind synchronizedKind))
                    {
                        QueueCommand synchronizedCommand = new QueueCommand(
                            synchronizedKind,
                            args.TargetValue1,
                            args.TargetValue2,
                            args.a6);
                        TryEnqueueSynchronizedCommand(args.TribeId, synchronizedTribe, synchronizedCommand);
                    }

                    // A queued Chore is consumed even if its tribe disappeared before this tick.
                    // Letting the marked command reach Vanilla would turn bit 0x80 into an invalid AI command.
                    args.SkipOriginalFunction = true;
                    args.ReturnValue = 1;
                    return;
                }

                // Every unmarked synchronized target order retains Vanilla behavior and
                // deterministically replaces any managed queue work on all peers.
                CancelQueuesForTribeUnits(args.TribeId);
                if (TryGetAliveTribe(args.TribeId, out GameTribe* vanillaTribe) &&
                    QueueCommandClassifier.TryClassifyTarget(commandValue, out QueueCommandKind vanillaKind))
                {
                    observedAttacks[args.TribeId] = new ObservedAttack(
                        vanillaTribe->r_GlobalId,
                        new QueueCommand(vanillaKind, args.TargetValue1, args.TargetValue2, args.a6));
                }
                else
                {
                    observedAttacks.Remove(args.TribeId);
                }
                return;
            }

            if (!FeatureEnabled)
                return;

            if (!IsLocalSelectedTribe(args.TribeId, out GameTribe* tribe))
                return;

            bool supported = QueueCommandClassifier.TryClassifyTarget(commandValue, out QueueCommandKind kind);
            bool shiftPressed = IsShiftPressed();

            if (!shiftPressed)
            {
                CancelQueuesForTribeUnits(args.TribeId);
                if (supported)
                {
                    observedAttacks[args.TribeId] = new ObservedAttack(
                        tribe->r_GlobalId,
                        new QueueCommand(kind, args.TargetValue1, args.TargetValue2, args.a6));
                }
                else
                {
                    observedAttacks.Remove(args.TribeId);
                }
                return;
            }

            if (!supported)
            {
                CancelQueuesForTribeUnits(args.TribeId);
                observedAttacks.Remove(args.TribeId);
                if (loggedUnsupportedCommands.Add(commandValue))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"UNSUPPORTED_SHIFT_COMMAND: command={args.AICommand}/{commandValue}, " +
                        $"arg1={args.TargetValue1}, arg2={args.TargetValue2}, arg3={args.a6}; Vanilla handles it.");
                }
                return;
            }

            QueueCommand command = new QueueCommand(kind, args.TargetValue1, args.TargetValue2, args.a6);
            if (!TryApplyQueuedCommand(args.TribeId, tribe, command))
            {
                args.SkipOriginalFunction = true;
                args.ReturnValue = 1;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"QUEUE_FULL: tribeId={args.TribeId}, limit={MaximumPendingCommands}, rejected={command}.");
                return;
            }

            args.SkipOriginalFunction = true;
            args.ReturnValue = 1;
        }

        private void OnMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!installed)
                return;

            if (args.Phase == EventHookPhase.Post)
            {
                if (moveObservationScopes.Count == 0)
                    return;
                MoveObservationScope scope = moveObservationScopes.Pop();
                if (scope.ShouldCapture && args.ReturnValue != 0 &&
                    args.IsPatrolPath == 0)
                {
                    largeMoveTargets.CaptureSuccessfulMove(
                        args.TribeId,
                        args.TileX,
                        args.TileY,
                        currentTick,
                        scope.Source);
                }
                return;
            }
            if (args.Phase != EventHookPhase.Pre)
                return;

            // A local click is not reliably nested inside Chore 17 (notably in the editor).
            // Identify it from the MoveHere contract and current local selection instead.
            bool directPlayerMove = !internalDispatch &&
                args.IsPatrolPath == 0 &&
                args.IsNewOrder &&
                IsLocalSelectedTribe(args.TribeId, out _);
            moveObservationScopes.Push(new MoveObservationScope(
                internalDispatch || directPlayerMove,
                internalDispatch ? "extended-shift" : "direct"));
            if (internalDispatch)
                return;

            if (IsRealMultiplayer())
            {
                if (!multiplayerSynchronizationReady)
                    return;

                int serializedMoveType = (int)args.MoveType;
                if (QueueNativeContract.TryDecodeQueuedMoveType(
                        serializedMoveType,
                        out int decodedMoveType))
                {
                    args.MoveType = (TribeMoveType)decodedMoveType;
                    if (!FeatureEnabled)
                    {
                        // Never expose the private marker to Vanilla after a setting transition.
                        observedAttacks.Remove(args.TribeId);
                        CancelQueuesForTribeUnits(args.TribeId);
                        return;
                    }

                    if (TryGetAliveTribe(args.TribeId, out GameTribe* synchronizedTribe))
                    {
                        QueueCommand synchronizedCommand = new QueueCommand(
                            QueueCommandKind.Move,
                            args.TileX,
                            args.TileY,
                            decodedMoveType);
                        TryEnqueueSynchronizedCommand(args.TribeId, synchronizedTribe, synchronizedCommand);
                    }

                    // Always consume the queue marker while enabled; 0x40/0x41 are not Vanilla move types.
                    SuppressCurrentMoveObservation();
                    args.SkipOriginalFunction = true;
                    args.ReturnValue = 1;
                    return;
                }

                observedAttacks.Remove(args.TribeId);
                CancelQueuesForTribeUnits(args.TribeId);
                return;
            }

            if (!FeatureEnabled)
                return;

            if (!IsLocalSelectedTribe(args.TribeId, out GameTribe* tribe))
                return;

            if (!IsShiftPressed())
            {
                observedAttacks.Remove(args.TribeId);
                CancelQueuesForTribeUnits(args.TribeId);
                return;
            }

            QueueCommand command = new QueueCommand(
                QueueCommandKind.Move,
                args.TileX,
                args.TileY,
                (int)args.MoveType);
            bool duplicateEvent = TryConsumeExpectedMoveSignal(
                expectedMoveEvents, args.TribeId, command);
            if (!duplicateEvent)
            {
                if (!TryApplyQueuedCommand(args.TribeId, tribe, command))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"QUEUE_FULL: tribeId={args.TribeId}, limit={MaximumPendingCommands}, rejected={command}.");
                }
                else
                {
                    // The already-created input chore can still reach the native append helper
                    // even though this order callback is skipped. Match it there and suppress it.
                    expectedMoveChores.Add(new RuntimeExpectedMove(
                        args.TribeId, command, currentTick + ExpectedMoveChoreLifetimeTicks));
                }
            }

            SuppressCurrentMoveObservation();
            args.SkipOriginalFunction = true;
            args.ReturnValue = 1;
        }

        private void SuppressCurrentMoveObservation()
        {
            if (moveObservationScopes.Count != 0)
                moveObservationScopes.Pop();
        }

        private void TryEnqueueSynchronizedCommand(
            int tribeId,
            GameTribe* tribe,
            QueueCommand command)
        {
            if (!TryApplyQueuedCommand(tribeId, tribe, command))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"QUEUE_FULL: tribeId={tribeId}, limit={MaximumPendingCommands}, rejected={command}.");
            }
        }

        private void AppendMovementWaypoint(
            IntPtr tribeManager,
            int serializedTribeId,
            ushort tileX,
            ushort tileY,
            int waypointIndex,
            short moveMode)
        {
            try
            {
                int tribeId = QueueNativeContract.WaypointChoreValueToTribeId(serializedTribeId);
                // Chore 71 serializes the same one-based game ID used by the manager APIs.
                // Treating it as a span index misses the real queue and lets Vanilla append
                // the Move a second time, which breaks mixed-command ordering.
                if (installed && FeatureEnabled && !internalDispatch)
                {
                    if (IsRealMultiplayer())
                    {
                        if (multiplayerSynchronizationReady &&
                            TryGetAliveTribe(tribeId, out GameTribe* synchronizedTribe))
                        {
                            TryEnqueueSynchronizedCommand(
                                tribeId,
                                synchronizedTribe,
                                new QueueCommand(QueueCommandKind.Move, tileX, tileY, moveMode));
                            return;
                        }

                        // Without the complete marker-hook set, every multiplayer command
                        // remains on its untouched Vanilla path.
                        goto Vanilla;
                    }

                    QueueCommand command = new QueueCommand(
                        QueueCommandKind.Move, tileX, tileY, moveMode);
                    if (TryConsumeExpectedMoveSignal(expectedMoveChores, tribeId, command))
                        return;

                    if ((HasQueuedUnitInTribe(tribeId) || IsShiftPressed()) &&
                        IsLocalSelectedTribe(tribeId, out GameTribe* selectedTribe))
                    {
                        if (TryApplyQueuedCommand(tribeId, selectedTribe, command))
                        {
                            // Some native input paths may reach the Chore hook first. In that
                            // case this is the authoritative insertion and the later event is deduplicated.
                            expectedMoveEvents.Add(new RuntimeExpectedMove(
                                tribeId, command, currentTick + ExpectedMoveChoreLifetimeTicks));
                        }
                        else
                        {
                            Shared.DebugLogHelper.LogWarning(
                                log,
                                $"QUEUE_FULL: tribeId={tribeId}, limit={MaximumPendingCommands}, rejected={command}.");
                        }
                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"WAYPOINT_HOOK_FAIL_OPEN: {exception}");
            }

        Vanilla:
            waypointAppendHook.Original(
                tribeManager,
                serializedTribeId,
                tileX,
                tileY,
                waypointIndex,
                moveMode);
        }

        private void RenderTribeOverlay(IntPtr tribeManager, int tribeId)
        {
            bool trampolineEntered = false;
            try
            {
                RenderTribeOverlayCore(tribeManager, tribeId, ref trampolineEntered);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, $"OVERLAY_HOOK_FAIL_OPEN: {exception}");
                if (!trampolineEntered)
                    InvokeOriginalTribeOverlay(tribeManager, tribeId);
            }
        }

        private void InvokeOriginalTribeOverlay(IntPtr tribeManager, int tribeId)
        {
            largeMoveTargets.BeginOverlayPass(tribeId);
            try
            {
                tribeOverlayRenderHook.Original(tribeManager, tribeId);
            }
            finally
            {
                largeMoveTargets.EndOverlayPass();
            }
        }

        private void RenderTribeOverlayCore(IntPtr tribeManager, int tribeId, ref bool trampolineEntered)
        {
            overlayCohortBuffer.Clear();
            if (installed && FeatureEnabled)
            {
                foreach (TribeQueueState candidate in cohorts.Values)
                {
                    if (candidate.BoundTribeId == tribeId)
                        overlayCohortBuffer.Add(candidate);
                }
                overlayCohortBuffer.Sort(CompareCohorts);
            }

            if (overlayCohortBuffer.Count == 0 || !TryGetAliveTribe(tribeId, out GameTribe* tribe))
            {
                trampolineEntered = true;
                InvokeOriginalTribeOverlay(tribeManager, tribeId);
                return;
            }

            uint tribeGlobalId = tribe->r_GlobalId;
            int tribeOwner = tribe->r_PlayerIdOwner;
            for (int index = overlayCohortBuffer.Count - 1; index >= 0; index--)
            {
                TribeQueueState state = overlayCohortBuffer[index];
                if (!state.MatchesTribe(tribeGlobalId, tribeOwner) ||
                    state.CurrentVisualSlots.Count == 0)
                    overlayCohortBuffer.RemoveAt(index);
            }
            if (overlayCohortBuffer.Count == 0)
            {
                trampolineEntered = true;
                InvokeOriginalTribeOverlay(tribeManager, tribeId);
                return;
            }

            for (int index = 0; index < overlayCohortBuffer.Count; index++)
                RenderCohortOverlay(
                    tribeManager, tribeId, tribe, overlayCohortBuffer[index], ref trampolineEntered);
            overlayCohortBuffer.Clear();
        }

        private void RenderCohortOverlay(
            IntPtr tribeManager,
            int tribeId,
            GameTribe* tribe,
            TribeQueueState state,
            ref bool trampolineEntered)
        {
            ushort savedCount = ReadMovementWaypointCount(tribe);
            ushort originX = 0;
            ushort originY = 0;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(
                    tribe->r_LeaderUnitId,
                    out GameUnit* leader) && leader != null)
            {
                originX = leader->r_CurrentTilePositionX;
                originY = leader->r_CurrentTilePositionY;
            }
            ushort* points = &tribe->r_PatrolPoint1TileX;
            ushort* savedPoints = stackalloc ushort[MaximumNativeMovementWaypoints * 2];
            for (int index = 0; index < MaximumNativeMovementWaypoints * 2; index++)
                savedPoints[index] = points[index];

            ushort savedIndex = tribe->r_PatrolCurrentTargetIndex;
            // The 2.2.0 interop still declares this native ushort as UInt32. Read and write
            // the proven 16-bit field directly so the adjacent word cannot affect capacity.
            TribePatrolMode savedMode = tribe->r_PatrolMode;
            try
            {
                int firstPageIndex = state.CurrentVisualPageIndex;

                // Render the active page first so its numbered markers win if Vanilla's
                // fixed draw-submission buffer is close to capacity. Later pages add flags only.
                for (int pageIndex = firstPageIndex; pageIndex < state.VisualPages.Count; pageIndex++)
                {
                    QueueVisualPage page = state.VisualPages[pageIndex];
                    BuildVisualQueueEntries(page.Slots, originX, originY, visualEntryBuffer);
                    bool showPageNumbers = pageIndex == firstPageIndex;

                    // Point zero is the PatrolOnce origin. Every fixed visual slot occupies
                    // its permanent one-based position after it, including hidden history.
                    points[0] = originX;
                    points[1] = originY;
                    for (int index = 0; index < visualEntryBuffer.Count; index++)
                    {
                        int pointIndex = index + 1;
                        points[pointIndex * 2] = visualEntryBuffer[index].TileX;
                        points[pointIndex * 2 + 1] = visualEntryBuffer[index].TileY;
                    }
                    tribe->r_PatrolCurrentTargetIndex = 0;
                    WriteMovementWaypointCount(
                        tribe,
                        unchecked((ushort)(visualEntryBuffer.Count + 1)));

                    // These values exist only while Vanilla emits overlay draw commands. They are
                    // restored before simulation code can observe or execute the projected path.
                    tribe->r_PatrolMode = TribePatrolMode.PatrolOnce;
                    projectedModeBuffer.Clear();
                    for (int index = 0; index < visualEntryBuffer.Count; index++)
                        projectedModeBuffer.Add(visualEntryBuffer[index].Mode);
                    overlayProjectedModes = projectedModeBuffer;
                    overlayShowPageNumbers = showPageNumbers;
                    overlayFlagCallIndex = 0;
                    overlayNumberCallIndex = 0;
                    overlaySawQueueMarker = false;
                    overlayRenderThreadId = Thread.CurrentThread.ManagedThreadId;
                    overlayDrawFilterActive = drawFilterInstalled;
                    trampolineEntered = true;
                    InvokeOriginalTribeOverlay(tribeManager, tribeId);
                    if (drawFilterInstalled && !overlaySawQueueMarker)
                        break;
                    ProjectAttackTargetMarkers(visualEntryBuffer);
                }
            }
            finally
            {
                overlayDrawFilterActive = false;
                overlayShowPageNumbers = false;
                overlayRenderThreadId = 0;
                overlayFlagCallIndex = 0;
                overlayNumberCallIndex = 0;
                overlaySawQueueMarker = false;
                overlayProjectedModes = Array.Empty<QueueVisualMarkerMode>();
                visualEntryBuffer.Clear();
                projectedModeBuffer.Clear();
                tribe->r_PatrolMode = savedMode;
                tribe->r_PatrolCurrentTargetIndex = savedIndex;
                WriteMovementWaypointCount(tribe, savedCount);
                for (int index = 0; index < MaximumNativeMovementWaypoints * 2; index++)
                    points[index] = savedPoints[index];
            }
        }

        private static void BuildVisualQueueEntries(
            IReadOnlyList<QueueVisualSlot> slots,
            ushort fallbackX,
            ushort fallbackY,
            List<VisualQueueEntry> entries)
        {
            entries.Clear();
            for (int index = 0; index < slots.Count; index++)
            {
                QueueVisualSlot slot = slots[index];
                if (slot.Completed)
                {
                    entries.Add(new VisualQueueEntry(
                        slot,
                        QueueVisualMarkerMode.Hidden,
                        fallbackX,
                        fallbackY,
                        IntPtr.Zero));
                    continue;
                }

                bool resolved = TryResolveVisualTarget(
                    slot.Command,
                    out ushort tileX,
                    out ushort tileY,
                    out IntPtr attackMarkerAddress);
                QueueVisualMarkerMode mode;
                if (!resolved)
                {
                    mode = QueueVisualMarkerMode.Hidden;
                    tileX = fallbackX;
                    tileY = fallbackY;
                    attackMarkerAddress = IntPtr.Zero;
                }
                else
                {
                    mode = slot.Command.IsAttack
                        ? QueueVisualMarkerMode.Attack
                        : QueueVisualMarkerMode.Move;
                }
                entries.Add(new VisualQueueEntry(slot, mode, tileX, tileY, attackMarkerAddress));
            }
        }

        private static bool TryResolveVisualTarget(
            QueueCommand command,
            out ushort tileX,
            out ushort tileY,
            out IntPtr attackMarkerAddress)
        {
            tileX = 0;
            tileY = 0;
            attackMarkerAddress = IntPtr.Zero;
            if (command.Kind == QueueCommandKind.Move)
            {
                if ((uint)command.Argument1 > ushort.MaxValue || (uint)command.Argument2 > ushort.MaxValue)
                    return false;
                tileX = unchecked((ushort)command.Argument1);
                tileY = unchecked((ushort)command.Argument2);
                return true;
            }

            if (command.Kind == QueueCommandKind.AttackUnit)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(command.Argument1, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth <= 0 ||
                    unit->r_GlobalId != unchecked((uint)command.Argument2))
                {
                    return false;
                }

                tileX = unit->r_CurrentTilePositionX;
                tileY = unit->r_CurrentTilePositionY;
                attackMarkerAddress = new IntPtr(
                    (byte*)unit + QueueNativeContract.GameUnitAttackMarkerOffset);
                return true;
            }

            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    command.Argument1,
                    out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_CurrentHealth <= 0 ||
                building->r_GlobalId != unchecked((uint)command.Argument2))
            {
                return false;
            }

            tileX = building->r_TilePositionXBegin;
            tileY = building->r_TilePositionYBegin;
            attackMarkerAddress = new IntPtr(
                (byte*)building + QueueNativeContract.GameBuildingAttackMarkerOffset);
            return true;
        }

        private void ProjectAttackTargetMarkers(List<VisualQueueEntry> entries)
        {
            if (!targetMarkerProjectionAvailable)
                return;

            for (int index = 0; index < entries.Count; index++)
            {
                VisualQueueEntry entry = entries[index];
                if (entry.Mode == QueueVisualMarkerMode.Attack &&
                    entry.AttackMarkerAddress != IntPtr.Zero)
                {
                    *(ushort*)entry.AttackMarkerAddress.ToPointer() = 1;
                }
            }
        }

        private void SubmitOverlayMarker(
            IntPtr drawManager,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags)
        {
            try
            {
                if (largeMoveTargets.ObserveAndShouldSuppressMarker(
                        drawManager,
                        category,
                        spriteId,
                        layer,
                        verticalOffset,
                        tileId,
                        flags))
                {
                    return;
                }
                if (overlayDrawFilterActive &&
                    overlayRenderThreadId == Thread.CurrentThread.ManagedThreadId)
                {
                    if (QueueVisualContract.IsPatrolOnceNumberSubmission(
                            category,
                            spriteId,
                            layer,
                            verticalOffset,
                            flags))
                    {
                        overlaySawQueueMarker = true;
                        int numberIndex = overlayNumberCallIndex++;
                        if (numberIndex < overlayProjectedModes.Count &&
                            QueueVisualContract.ShouldSuppressNumber(
                                overlayProjectedModes[numberIndex],
                                overlayShowPageNumbers))
                        {
                            return;
                        }
                    }
                    else if (QueueVisualContract.IsPatrolOnceFlagSubmission(
                            category,
                            spriteId,
                            layer,
                            verticalOffset,
                            flags))
                    {
                        overlaySawQueueMarker = true;
                        int flagIndex = overlayFlagCallIndex++;
                        if (flagIndex < overlayProjectedModes.Count &&
                            QueueVisualContract.ShouldSuppressFlag(overlayProjectedModes[flagIndex]))
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                if (!drawFilterFailureLogged)
                {
                    drawFilterFailureLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"VISUAL_FLAG_FILTER_FAIL_OPEN: {exception.Message}");
                }
            }

            drawSubmissionHook.Original(
                drawManager,
                category,
                spriteId,
                layer,
                verticalOffset,
                tileId,
                flags);
        }

        private void OnTick(int tick)
        {
            if (!installed)
                return;

            currentTick = tick;
            largeMoveTargets.OnTick(tick);
            if (!FeatureEnabled)
                return;
            if (!runtimeTickLogged)
            {
                runtimeTickLogged = true;
                Shared.DebugLogHelper.LogInfo(log, $"RUNTIME_ACTIVE: firstTick={tick}.");
            }
            PruneExpectedMoveSignals(expectedMoveChores);
            PruneExpectedMoveSignals(expectedMoveEvents);
            ReconcileCohorts();
            CoalesceEquivalentCohorts();

            cohortIdBuffer.Clear();
            foreach (long cohortId in cohorts.Keys)
                cohortIdBuffer.Add(cohortId);
            cohortIdBuffer.Sort((left, right) => CompareCohorts(cohorts[left], cohorts[right]));
            for (int index = 0; index < cohortIdBuffer.Count; index++)
                ProcessCohort(cohortIdBuffer[index]);
        }

        private void ProcessCohort(long cohortId)
        {
            if (!cohorts.TryGetValue(cohortId, out TribeQueueState state) ||
                state.BoundTribeId <= 0 ||
                !TryGetMatchingAliveTribe(
                    state.BoundTribeId,
                    state.TribeGlobalId,
                    state.OwnerPlayerId,
                    out GameTribe* tribe))
                return;
            int tribeId = state.BoundTribeId;

            if (state.ExternalAttack != null)
            {
                if (!IsTargetAlive(state.ExternalAttack))
                {
                    state.CompleteExternalAttack();
                }
                else if (state.ExternalAttackNeedsRedispatch)
                {
                    if (!EnsureDedicatedTribe(state, ref tribeId, ref tribe))
                        return;
                    if (!Dispatch(tribeId, state.ExternalAttack))
                    {
                        if (loggedPredecessorRedispatchFailures.Add(cohortId))
                            Shared.DebugLogHelper.LogWarning(
                                log,
                                $"PREDECESSOR_REDISPATCH_FAILED: tribeId={tribeId}, " +
                                $"command={state.ExternalAttack}; retrying.");
                        return;
                    }
                    loggedPredecessorRedispatchFailures.Remove(cohortId);
                    state.MarkExternalAttackRedispatched();
                    return;
                }
                else
                {
                    return;
                }
            }

            if (state.WaitForVanillaMovement)
            {
                state.UpdateVanillaVisualProgress(ReadMovementWaypointIndex(tribe));
                if (!HasCompletedMovementSequence(tribeId, tribe))
                    return;

                state.WaitForVanillaMovement = false;
                state.CompleteVanillaVisuals();
            }

            if (state.Active != null)
            {
                if (state.Active.IsAttack && !IsTargetAlive(state.Active))
                    state.CompleteActive();
            }

            if (state.Active != null)
            {
                if (!EnsureDedicatedTribe(state, ref tribeId, ref tribe))
                    return;
                if (state.ActiveNeedsRedispatch)
                {
                    if (state.Active.IsAttack && !IsTargetAlive(state.Active))
                        state.CompleteActive();
                    else if (Dispatch(tribeId, state.Active))
                    {
                        state.MarkActiveRedispatched();
                        return;
                    }
                    else
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"REDISPATCH_FAILED: tribeId={tribeId}, " +
                            $"command={state.Active}; skipping.");
                        state.CompleteActive();
                    }
                }

                if (state.Active != null)
                {
                    bool complete = state.Active.Kind == QueueCommandKind.Move
                        ? HasCompletedMovementSequence(tribeId, tribe)
                        : !IsTargetAlive(state.Active);
                    if (!complete)
                        return;

                    state.CompleteActive();
                }
            }

            if (state.PendingCount != 0 && !EnsureDedicatedTribe(state, ref tribeId, ref tribe))
                return;

            while (state.TryActivateNext(out QueueCommand command))
            {
                if (command.IsAttack && !IsTargetAlive(command))
                {
                    state.CompleteActive();
                    continue;
                }

                bool issued = Dispatch(tribeId, command);
                if (issued)
                    return;

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"DISPATCH_FAILED: tribeId={tribeId}, command={command}; skipping.");
                state.CompleteActive();
            }

            if (state.IsEmpty)
                RemoveCohort(cohortId);
        }

        private bool HasCompletedMovementSequence(int tribeId, GameTribe* tribe)
        {
            return ReadMovementMode(tribe) == 0 &&
                isTribeMovementComplete(tribeManagerPointer, tribeId) == 1;
        }

        private bool Dispatch(int tribeId, QueueCommand command)
        {
            bool issued;
            internalDispatch = true;
            try
            {
                switch (command.Kind)
                {
                    case QueueCommandKind.Move:
                        issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(
                            tribeId,
                            command.Argument1,
                            command.Argument2,
                            isPatrolPath: false,
                            bIsNewOrder: 1,
                            tribeMoveType: (TribeMoveType)command.Argument3);
                        break;
                    case QueueCommandKind.AttackUnit:
                        issued = GameTribeManagerAPI.Instance.AttackUnitEx(
                            tribeId,
                            command.Argument1,
                            command.Argument2);
                        break;
                    case QueueCommandKind.AttackBuilding:
                        issued = GameTribeManagerAPI.Instance.AttackBuildingEx(
                            tribeId,
                            command.Argument1,
                            command.Argument2);
                        break;
                    case QueueCommandKind.ForceAttackBuilding:
                        issued = GameTribeManagerAPI.Instance.ForceAttackBuildingEx(
                            tribeId,
                            command.Argument1,
                            command.Argument2);
                        break;
                    default:
                        issued = false;
                        break;
                }
            }
            finally
            {
                internalDispatch = false;
            }

            return issued;
        }

        private bool TryApplyQueuedCommand(int tribeId, GameTribe* tribe, QueueCommand command)
        {
            // Mode-0 is authoritative. Reconcile immediately so a Chore affects exactly
            // the units Vanilla currently placed in its serialized tribe, never stale siblings.
            ReconcileCohorts();
            List<QueueUnitIdentity> tribeMembers = CaptureTribeMembers(tribeId);
            if (tribeMembers.Count == 0)
                return false;

            List<TribeQueueState> affected = new List<TribeQueueState>();
            List<QueueUnitIdentity> unqueued = new List<QueueUnitIdentity>();
            HashSet<long> seen = new HashSet<long>();
            foreach (QueueUnitIdentity member in tribeMembers)
            {
                if (unitToCohort.TryGetValue(member, out long cohortId) &&
                    cohorts.TryGetValue(cohortId, out TribeQueueState state))
                {
                    if (seen.Add(cohortId))
                        affected.Add(state);
                }
                else
                {
                    unqueued.Add(member);
                }
            }

            affected.Sort(CompareCohorts);
            if (affected.Any(state => !state.CanEnqueue))
                return false;

            TribeQueueState created = null;
            if (unqueued.Count != 0)
            {
                created = CreateCohort(tribeId, tribe, unqueued);
                if (!created.CanEnqueue)
                {
                    RemoveCohort(created.CohortId);
                    return false;
                }
            }

            if (created != null)
                affected.Add(created);
            return QueueCohortOperations.TryEnqueueAtomically(affected, command);
        }

        private TribeQueueState CreateCohort(
            int tribeId,
            GameTribe* tribe,
            List<QueueUnitIdentity> members)
        {
            long cohortId = nextCohortId++;
            TribeQueueState state = new TribeQueueState(
                tribe->r_GlobalId,
                MaximumPendingCommands,
                members,
                tribe->r_PlayerIdOwner,
                cohortId,
                tribeId);
            ushort nativeWaypointCount = ReadMovementWaypointCount(tribe);
            bool hasExternalAttack = observedAttacks.TryGetValue(tribeId, out ObservedAttack observed) &&
                observed.TribeGlobalId == tribe->r_GlobalId && IsTargetAlive(observed.Command);
            state.WaitForVanillaMovement = !hasExternalAttack && nativeWaypointCount != 0 &&
                !HasCompletedMovementSequence(tribeId, tribe);
            if (state.WaitForVanillaMovement)
                CaptureVanillaVisualSlots(state, tribe, nativeWaypointCount);
            if (hasExternalAttack)
                state.ExternalAttack = observed.Command;
            cohorts.Add(cohortId, state);
            foreach (QueueUnitIdentity member in members)
                unitToCohort[member] = cohortId;
            return state;
        }

        private static void CaptureVanillaVisualSlots(
            TribeQueueState state,
            GameTribe* tribe,
            ushort waypointCount)
        {
            int count = Math.Min((int)waypointCount, MaximumNativeMovementWaypoints);
            int currentWaypointIndex = ReadMovementWaypointIndex(tribe);
            short moveMode = ReadMovementMode(tribe);
            ushort* points = &tribe->r_PatrolPoint1TileX;
            for (int pointIndex = 1; pointIndex < count; pointIndex++)
            {
                QueueCommand command = new QueueCommand(
                    QueueCommandKind.Move,
                    points[pointIndex * 2],
                    points[pointIndex * 2 + 1],
                    moveMode);
                state.AddVanillaWaypoint(
                    command,
                    pointIndex,
                    completed: pointIndex < currentWaypointIndex);
            }
        }

        private void CancelQueuesForTribeUnits(int tribeId)
        {
            List<QueueUnitIdentity> affected = CaptureTribeMembers(tribeId);
            foreach (QueueUnitIdentity member in affected)
            {
                if (!unitToCohort.TryGetValue(member, out long cohortId) ||
                    !cohorts.TryGetValue(cohortId, out TribeQueueState state))
                    continue;
                unitToCohort.Remove(member);
                state.RemoveMember(member);
                if (state.Members.Count == 0)
                {
                    cohorts.Remove(cohortId);
                    loggedPredecessorRedispatchFailures.Remove(cohortId);
                    loggedIsolationFailures.Remove(cohortId);
                }
            }
        }

        private void ReconcileCohorts()
        {
            cohortIdBuffer.Clear();
            cohortIdBuffer.AddRange(cohorts.Keys);
            cohortIdBuffer.Sort();
            foreach (long cohortId in cohortIdBuffer)
            {
                if (!cohorts.TryGetValue(cohortId, out TribeQueueState state))
                    continue;
                SortedDictionary<int, List<QueueUnitIdentity>> branches =
                    new SortedDictionary<int, List<QueueUnitIdentity>>();
                foreach (QueueUnitIdentity member in state.Members.ToArray())
                {
                    if (!TryGetLivingUnit(member, out GameUnit* unit))
                    {
                        unitToCohort.Remove(member);
                        continue;
                    }
                    int memberTribeId = unit->r_TribeId;
                    if (!branches.TryGetValue(memberTribeId, out List<QueueUnitIdentity> branch))
                    {
                        branch = new List<QueueUnitIdentity>();
                        branches.Add(memberTribeId, branch);
                    }
                    branch.Add(member);
                }

                if (branches.Count == 0)
                {
                    RemoveCohort(cohortId);
                    continue;
                }

                bool first = true;
                foreach (KeyValuePair<int, List<QueueUnitIdentity>> branch in branches)
                {
                    branch.Value.Sort(QueueUnitIdentity.Compare);
                    uint tribeGlobalId = 0;
                    if (branch.Key > 0 && TryGetAliveTribe(branch.Key, out GameTribe* branchTribe) &&
                        branchTribe->r_PlayerIdOwner == state.OwnerPlayerId)
                        tribeGlobalId = branchTribe->r_GlobalId;

                    if (first)
                    {
                        first = false;
                        state.ReplaceMembers(branch.Value);
                        state.RebindTribe(branch.Key, tribeGlobalId);
                        foreach (QueueUnitIdentity member in branch.Value)
                            unitToCohort[member] = state.CohortId;
                    }
                    else
                    {
                        long branchId = nextCohortId++;
                        TribeQueueState clone = state.CloneForBranch(
                            branchId, branch.Key, tribeGlobalId, branch.Value);
                        cohorts.Add(branchId, clone);
                        foreach (QueueUnitIdentity member in branch.Value)
                            unitToCohort[member] = branchId;
                    }
                }

                if (branches.Count > 1)
                    LogTopology("SPLIT", state);
            }
        }

        private bool EnsureDedicatedTribe(
            TribeQueueState state,
            ref int tribeId,
            ref GameTribe* tribe)
        {
            foreach (QueueUnitIdentity member in state.Members)
            {
                if (!TryGetLivingUnit(member, out GameUnit* unit) || unit->r_TribeId != tribeId)
                    return false;
            }

            if (tribe->r_UnitsInGroup == state.Members.Count)
                return true;

            int originalTribeId = tribeId;
            TribeStance stance = tribe->r_TribeStance;
            long createdValue = GameTribeManagerAPI.Instance.Create(state.OwnerPlayerId, false);
            if (createdValue <= 0 || createdValue > int.MaxValue)
            {
                LogIsolationFailure(state, $"create returned {createdValue}");
                return false;
            }
            int newTribeId = unchecked((int)createdValue);
            if (!TryGetAliveTribe(newTribeId, out GameTribe* newTribe))
            {
                LogIsolationFailure(state, $"created tribe {newTribeId} is unavailable");
                return false;
            }
            newTribe->r_TribeStance = stance;

            List<QueueUnitIdentity> moved = new List<QueueUnitIdentity>();
            foreach (QueueUnitIdentity member in state.Members.OrderBy(value => value.UnitId))
            {
                if (!TryUnassignUnit(member, originalTribeId) ||
                    !GameTribeManagerAPI.Instance.AssignUnit(newTribeId, member.UnitId) ||
                    !TryGetLivingUnit(member, out GameUnit* unit) || unit->r_TribeId != newTribeId)
                {
                    RollBackTribeSplit(moved, member, originalTribeId, newTribeId);
                    GameTribeManagerAPI.Instance.DeleteTribeSafe(newTribeId);
                    LogIsolationFailure(state, $"assignment failed for unit {member.UnitId}");
                    return false;
                }
                moved.Add(member);
            }

            tribeId = newTribeId;
            tribe = newTribe;
            state.RebindTribe(newTribeId, newTribe->r_GlobalId);
            loggedIsolationFailures.Remove(state.CohortId);
            LogTopology("ISOLATE", state);
            return true;
        }

        private void LogIsolationFailure(TribeQueueState state, string reason)
        {
            if (loggedIsolationFailures.Add(state.CohortId))
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"COHORT_ISOLATION_RETRY: cohort={state.CohortId}, " +
                    $"tribeId={state.BoundTribeId}, reason={reason}.");
        }

        private void RollBackTribeSplit(
            List<QueueUnitIdentity> moved,
            QueueUnitIdentity failed,
            int originalTribeId,
            int newTribeId)
        {
            if (TryGetLivingUnit(failed, out GameUnit* failedUnit) && failedUnit->r_TribeId == 0)
                GameTribeManagerAPI.Instance.AssignUnit(originalTribeId, failed.UnitId);
            for (int index = moved.Count - 1; index >= 0; index--)
            {
                QueueUnitIdentity member = moved[index];
                if (TryGetLivingUnit(member, out GameUnit* unit) && unit->r_TribeId == newTribeId &&
                    !TryUnassignUnit(member, newTribeId))
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Queue rollback could not unassign unitId={member.UnitId} from tribeId={newTribeId}.");
                    continue;
                }
                GameTribeManagerAPI.Instance.AssignUnit(originalTribeId, member.UnitId);
            }
        }

        private bool TryUnassignUnit(QueueUnitIdentity member, int tribeId)
        {
            if (!TryGetLivingUnit(member, out GameUnit* unit) || unit->r_TribeId != tribeId)
                return false;

            // Script Extender 2.2.0 fixes the wrapper's native argument order.
            if (!GameTribeManagerAPI.Instance.UnassignUnit(tribeId, member.UnitId))
                return false;

            return TryGetLivingUnit(member, out unit) && unit->r_TribeId != tribeId;
        }

        private void CoalesceEquivalentCohorts()
        {
            Dictionary<int, List<TribeQueueState>> byTribe =
                new Dictionary<int, List<TribeQueueState>>();
            foreach (TribeQueueState state in cohorts.Values)
            {
                if (!byTribe.TryGetValue(state.BoundTribeId, out List<TribeQueueState> states))
                {
                    states = new List<TribeQueueState>();
                    byTribe.Add(state.BoundTribeId, states);
                }
                states.Add(state);
            }

            List<int> tribeIds = byTribe.Keys.ToList();
            tribeIds.Sort();
            foreach (int groupedTribeId in tribeIds)
            {
                List<TribeQueueState> ordered = byTribe[groupedTribeId];
                if (ordered.Count < 2)
                    continue;
                ordered.Sort(CompareCohorts);
                for (int leftIndex = 0; leftIndex < ordered.Count; leftIndex++)
                {
                    TribeQueueState left = ordered[leftIndex];
                    if (!cohorts.ContainsKey(left.CohortId))
                        continue;
                    for (int rightIndex = leftIndex + 1; rightIndex < ordered.Count; rightIndex++)
                    {
                        TribeQueueState right = ordered[rightIndex];
                        if (!cohorts.ContainsKey(right.CohortId) ||
                            !HaveEquivalentExecutionState(left, right))
                            continue;
                        List<QueueUnitIdentity> merged = left.Members.Concat(right.Members)
                            .Distinct().OrderBy(member => member.UnitId).ThenBy(member => member.GlobalId).ToList();
                        left.ReplaceMembers(merged);
                        foreach (QueueUnitIdentity member in right.Members)
                            unitToCohort[member] = left.CohortId;
                        cohorts.Remove(right.CohortId);
                        loggedPredecessorRedispatchFailures.Remove(right.CohortId);
                        loggedIsolationFailures.Remove(right.CohortId);
                        LogTopology("COALESCE", left);
                    }
                }
            }
        }

        private static bool HaveEquivalentExecutionState(TribeQueueState left, TribeQueueState right)
        {
            if (left.OwnerPlayerId != right.OwnerPlayerId ||
                left.WaitForVanillaMovement != right.WaitForVanillaMovement ||
                left.ActiveNeedsRedispatch != right.ActiveNeedsRedispatch ||
                left.ExternalAttackNeedsRedispatch != right.ExternalAttackNeedsRedispatch ||
                !SameCommand(left.Active, right.Active) ||
                !SameCommand(left.ExternalAttack, right.ExternalAttack))
                return false;
            QueueCommand[] leftPending = left.PendingCommands.ToArray();
            QueueCommand[] rightPending = right.PendingCommands.ToArray();
            if (leftPending.Length != rightPending.Length)
                return false;
            for (int index = 0; index < leftPending.Length; index++)
            {
                if (!SameCommand(leftPending[index], rightPending[index]))
                    return false;
            }
            return left.CurrentVisualPageNumber == right.CurrentVisualPageNumber &&
                left.OutstandingVisualCount == right.OutstandingVisualCount &&
                HaveEquivalentVisualState(left, right);
        }

        private static bool HaveEquivalentVisualState(TribeQueueState left, TribeQueueState right)
        {
            if (left.VisualPages.Count != right.VisualPages.Count)
                return false;
            for (int pageIndex = 0; pageIndex < left.VisualPages.Count; pageIndex++)
            {
                QueueVisualPage leftPage = left.VisualPages[pageIndex];
                QueueVisualPage rightPage = right.VisualPages[pageIndex];
                if (leftPage.PageNumber != rightPage.PageNumber || leftPage.Slots.Count != rightPage.Slots.Count)
                    return false;
                for (int slotIndex = 0; slotIndex < leftPage.Slots.Count; slotIndex++)
                {
                    QueueVisualSlot leftSlot = leftPage.Slots[slotIndex];
                    QueueVisualSlot rightSlot = rightPage.Slots[slotIndex];
                    if (leftSlot.Ordinal != rightSlot.Ordinal || leftSlot.Completed != rightSlot.Completed ||
                        !SameCommand(leftSlot.Command, rightSlot.Command))
                        return false;
                }
            }
            return true;
        }

        private static bool SameCommand(QueueCommand left, QueueCommand right) =>
            ReferenceEquals(left, right) || (left != null && left.HasSamePayload(right));

        private void RemoveCohort(long cohortId)
        {
            if (!cohorts.TryGetValue(cohortId, out TribeQueueState state))
                return;
            foreach (QueueUnitIdentity member in state.Members)
            {
                if (unitToCohort.TryGetValue(member, out long mapped) && mapped == cohortId)
                    unitToCohort.Remove(member);
            }
            cohorts.Remove(cohortId);
            loggedPredecessorRedispatchFailures.Remove(cohortId);
            loggedIsolationFailures.Remove(cohortId);
        }

        private bool HasQueuedUnitInTribe(int tribeId)
        {
            foreach (TribeQueueState state in cohorts.Values)
            {
                if (state.BoundTribeId == tribeId)
                    return true;
            }
            return false;
        }

        private static bool TryGetLivingUnit(QueueUnitIdentity identity, out GameUnit* unit)
        {
            unit = null;
            return GameUnitManagerAPI.Instance.IsValidId(identity.UnitId) &&
                GameUnitManagerAPI.Instance.TryGetUnitById(identity.UnitId, out unit) && unit != null &&
                unit->r_AliveState == AliveState.IsAlive && unit->r_GlobalId == identity.GlobalId;
        }

        private bool TryConsumeExpectedMoveSignal(
            List<RuntimeExpectedMove> signals,
            int tribeId,
            QueueCommand command)
        {
            PruneExpectedMoveSignals(signals);
            for (int index = 0; index < signals.Count; index++)
            {
                if (signals[index].TribeId == tribeId && signals[index].Command.HasSamePayload(command))
                {
                    signals.RemoveAt(index);
                    return true;
                }
            }
            return false;
        }

        private void PruneExpectedMoveSignals(List<RuntimeExpectedMove> signals)
        {
            for (int index = signals.Count - 1; index >= 0; index--)
            {
                if (signals[index].ExpiresAfterTick < currentTick)
                    signals.RemoveAt(index);
            }
        }

        private void LogTopology(string action, TribeQueueState state)
        {
            ulong hash = 1469598103934665603UL;
            foreach (QueueUnitIdentity member in state.Members)
            {
                hash = (hash ^ unchecked((uint)member.UnitId)) * 1099511628211UL;
                hash = (hash ^ member.GlobalId) * 1099511628211UL;
            }
            hash = (hash ^ unchecked((uint)state.BoundTribeId)) * 1099511628211UL;
            hash = MixCommandHash(hash, state.Active);
            foreach (QueueCommand command in state.PendingCommands)
                hash = MixCommandHash(hash, command);
            hash = (hash ^ unchecked((uint)state.CurrentVisualPageNumber)) * 1099511628211UL;
            hash = (hash ^ unchecked((uint)state.OutstandingVisualCount)) * 1099511628211UL;
            Shared.DebugLogHelper.LogInfo(log,
                $"TOPOLOGY_{action}: cohort={state.CohortId}, tribeId={state.BoundTribeId}, " +
                $"members={state.Members.Count}, hash={hash:X16}.");
        }

        private static ulong MixCommandHash(ulong hash, QueueCommand command)
        {
            if (command == null)
                return (hash ^ uint.MaxValue) * 1099511628211UL;
            hash = (hash ^ unchecked((uint)command.Kind)) * 1099511628211UL;
            hash = (hash ^ unchecked((uint)command.Argument1)) * 1099511628211UL;
            hash = (hash ^ unchecked((uint)command.Argument2)) * 1099511628211UL;
            return (hash ^ unchecked((uint)command.Argument3)) * 1099511628211UL;
        }

        private static int CompareCohorts(TribeQueueState left, TribeQueueState right)
        {
            int owner = left.OwnerPlayerId.CompareTo(right.OwnerPlayerId);
            if (owner != 0) return owner;
            int unit = left.SmallestUnitId.CompareTo(right.SmallestUnitId);
            if (unit != 0) return unit;
            int global = left.SmallestGlobalId.CompareTo(right.SmallestGlobalId);
            return global != 0 ? global : left.CohortId.CompareTo(right.CohortId);
        }

        private static List<QueueUnitIdentity> CaptureTribeMembers(int tribeId)
        {
            List<QueueUnitIdentity> members = new List<QueueUnitIdentity>();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_TribeId != tribeId)
                    continue;
                int unitId = spanIndex + 1;
                members.Add(new QueueUnitIdentity(unitId, unit.r_GlobalId));
            }
            return members;
        }

        private bool IsLocalSelectedTribe(int tribeId, out GameTribe* tribe)
        {
            tribe = null;
            if (!GameTribeManagerAPI.Instance.IsValidId(tribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) ||
                tribe == null || tribe->r_AliveState != AliveState.IsAlive ||
                tribe->r_PlayerIdOwner != GameNetworkAPI.GetLocalPlayerId())
            {
                return false;
            }

            SelectedUnitInfo[] selectedUnits = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            for (int index = 0; index < selectedUnits.Length; index++)
            {
                int unitId = selectedUnits[index].UnitId;
                if (!GameUnitManagerAPI.Instance.IsValidId(unitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null)
                {
                    continue;
                }

                if (unit->r_AliveState == AliveState.IsAlive && unit->r_TribeId == tribeId)
                    return true;
            }
            return false;
        }

        private static HookTransactionOptions CreateTransactionOptions() =>
            new HookTransactionOptions
            {
                FailureMode = TransactionFailureMode.RollbackAndThrow,
                // This runtime is process-rooted; startup never tears these hooks down.
                OwnsHooks = false
            };

        private static bool TryGetAliveTribe(int tribeId, out GameTribe* tribe)
        {
            tribe = null;
            return GameTribeManagerAPI.Instance.IsValidId(tribeId) &&
                GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) &&
                tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive;
        }

        private static bool TryGetMatchingAliveTribe(
            int tribeId,
            uint expectedGlobalId,
            int expectedOwnerPlayerId,
            out GameTribe* tribe)
        {
            tribe = null;
            return GameTribeManagerAPI.Instance.IsValidId(tribeId) &&
                GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) &&
                tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive &&
                tribe->r_GlobalId == expectedGlobalId &&
                tribe->r_PlayerIdOwner == expectedOwnerPlayerId;
        }

        private static bool IsTargetAlive(QueueCommand command)
        {
            if (command == null || !command.IsAttack)
                return false;

            if (command.Kind == QueueCommandKind.AttackUnit)
            {
                if (!GameUnitManagerAPI.Instance.IsValidId(command.Argument1) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(command.Argument1, out GameUnit* unit) ||
                    unit == null)
                {
                    return false;
                }
                return unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_GlobalId == unchecked((uint)command.Argument2) &&
                    unit->r_CurrentHealth != 0;
            }

            if (!GameBuildingManagerAPI.Instance.IsValidId(command.Argument1) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(command.Argument1, out GameBuilding* building) ||
                building == null)
            {
                return false;
            }
            return building->r_AliveState == AliveState.IsAlive &&
                building->r_GlobalId == unchecked((uint)command.Argument2) &&
                building->r_CurrentHealth > 0;
        }

        private static ushort ReadMovementWaypointCount(GameTribe* tribe) =>
            *(ushort*)((byte*)tribe + MovementWaypointCountOffset);

        private static void WriteMovementWaypointCount(GameTribe* tribe, ushort count) =>
            *(ushort*)((byte*)tribe + MovementWaypointCountOffset) = count;

        private static ushort ReadMovementWaypointIndex(GameTribe* tribe) =>
            *(ushort*)((byte*)tribe + MovementWaypointIndexOffset);

        private static short ReadMovementMode(GameTribe* tribe) =>
            *(short*)((byte*)tribe + MovementModeOffset);

        private static bool IsShiftPressed() =>
            EditorDirector.instance != null && EditorDirector.instance.shiftPressed;

        private bool IsRealMultiplayer()
        {
            bool realMultiplayer = Shared.GameModeHelper.Capture().IsRealMultiplayer;
            if (lastRealMultiplayerMode != realMultiplayer)
            {
                lastRealMultiplayerMode = realMultiplayer;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MODE: realMultiplayer={realMultiplayer}, " +
                    $"synchronizedQueueing={(realMultiplayer && multiplayerSynchronizationReady)}.");
            }
            return realMultiplayer;
        }

        private readonly struct VisualQueueEntry
        {
            public VisualQueueEntry(
                QueueVisualSlot slot,
                QueueVisualMarkerMode mode,
                ushort tileX,
                ushort tileY,
                IntPtr attackMarkerAddress)
            {
                Slot = slot;
                Mode = mode;
                TileX = tileX;
                TileY = tileY;
                AttackMarkerAddress = attackMarkerAddress;
            }

            public QueueVisualSlot Slot { get; }
            public QueueCommand Command => Slot.Command;
            public QueueVisualMarkerMode Mode { get; }
            public ushort TileX { get; }
            public ushort TileY { get; }
            public IntPtr AttackMarkerAddress { get; }

        }

        private sealed class ObservedAttack
        {
            public ObservedAttack(uint tribeGlobalId, QueueCommand command)
            {
                TribeGlobalId = tribeGlobalId;
                Command = command;
            }

            public uint TribeGlobalId { get; }
            public QueueCommand Command { get; }
        }

        private sealed class RuntimeExpectedMove
        {
            public RuntimeExpectedMove(int tribeId, QueueCommand command, int expiresAfterTick)
            {
                TribeId = tribeId;
                Command = command;
                ExpiresAfterTick = expiresAfterTick;
            }

            public int TribeId { get; }
            public QueueCommand Command { get; }
            public int ExpiresAfterTick { get; }
        }

        private sealed class MoveObservationScope
        {
            public MoveObservationScope(bool shouldCapture, string source)
            {
                ShouldCapture = shouldCapture;
                Source = source;
            }

            public bool ShouldCapture { get; }
            public string Source { get; }
        }
    }
}
