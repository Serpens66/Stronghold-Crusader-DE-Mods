using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace QueueTest
{
    internal sealed unsafe class QueueRuntime
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
        private const int WaitDiagnosticIntervalTicks = 100;
        private const int ExpectedMoveChoreLifetimeTicks = 30;

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

        private readonly ManualLogSource log;
        private readonly Dictionary<int, TribeQueueState> queues = new Dictionary<int, TribeQueueState>();
        private readonly Dictionary<int, ObservedAttack> observedAttacks = new Dictionary<int, ObservedAttack>();
        private readonly HashSet<int> loggedUnsupportedCommands = new HashSet<int>();
        private readonly HashSet<string> loggedOverlayProjections = new HashSet<string>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private HookTransaction nativeTransaction;
        private HookTransaction drawFilterTransaction;
        private HookRef<X64ManagedFunctionDetourAOB<AppendMovementWaypointDelegate>> waypointAppendHook =
            new HookRef<X64ManagedFunctionDetourAOB<AppendMovementWaypointDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<RenderTribeOverlayDelegate>> tribeOverlayRenderHook =
            new HookRef<X64ManagedFunctionDetourAOB<RenderTribeOverlayDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<DrawSubmissionDelegate>> drawSubmissionHook =
            new HookRef<X64ManagedFunctionDetourAOB<DrawSubmissionDelegate>>();
        private IsTribeMovementCompleteDelegate isTribeMovementComplete;
        private IntPtr tribeManagerPointer;
        private bool installed;
        private bool internalDispatch;
        private bool tickMarkerLogged;
        private int currentTick;
        private bool drawFilterInstalled;
        private bool drawFilterFailureLogged;
        private bool overlayDrawFilterActive;
        private int overlayRenderThreadId;
        private int overlayMarkerCallIndex;
        private IReadOnlyList<QueueCommandKind> overlayProjectedKinds = Array.Empty<QueueCommandKind>();

        public QueueRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Install(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (installed)
                return;

            int actualTribeSize = Marshal.SizeOf(typeof(GameTribe));
            if (actualTribeSize != GameTribeSize)
            {
                throw new InvalidOperationException(
                    $"GameTribe layout mismatch: expected=0x{GameTribeSize:X}, actual=0x{actualTribeSize:X}.");
            }
            int actualUnitSize = Marshal.SizeOf(typeof(GameUnit));
            int actualUnitGlobalIdOffset = Marshal.OffsetOf(
                typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32();
            int actualBuildingSize = Marshal.SizeOf(typeof(GameBuilding));
            int actualBuildingGlobalIdOffset = Marshal.OffsetOf(
                typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32();
            if (actualUnitSize != QueueNativeContract.GameUnitSize ||
                actualUnitGlobalIdOffset != QueueNativeContract.GameUnitGlobalIdOffset ||
                actualBuildingSize != QueueNativeContract.GameBuildingSize ||
                actualBuildingGlobalIdOffset != QueueNativeContract.GameBuildingGlobalIdOffset)
            {
                throw new InvalidOperationException(
                    $"Target marker layout mismatch: unit=0x{actualUnitSize:X}/0x{actualUnitGlobalIdOffset:X}, " +
                    $"building=0x{actualBuildingSize:X}/0x{actualBuildingGlobalIdOffset:X}.");
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
                referenceHashMatches: true,
                name: "Vanilla movement-waypoint append helper",
                log: log);
            if (resolution.Rva != ReferenceWaypointAppendRva)
            {
                throw new InvalidOperationException(
                    $"Waypoint helper resolved at unexpected RVA 0x{resolution.Rva:X}.");
            }

            Shared.NativeResolution movementCompleteResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                MovementCompletePattern,
                ReferenceMovementCompleteRva,
                referenceHashMatches: true,
                name: "Vanilla tribe-movement completion predicate",
                log: log);
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
                referenceHashMatches: true,
                name: "Vanilla tribe overlay renderer",
                log: log);
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
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            nativeTransaction.AddDetour(
                ref waypointAppendHook,
                unchecked((ulong)libraryHandle.ToInt64()) + ReferenceWaypointAppendRva,
                AppendMovementWaypoint);
            nativeTransaction.AddDetour(
                ref tribeOverlayRenderHook,
                unchecked((ulong)libraryHandle.ToInt64()) + ReferenceTribeOverlayRenderRva,
                RenderTribeOverlay);
            nativeTransaction.Commit();
            if (!waypointAppendHook.Success || !tribeOverlayRenderHook.Success)
                throw new InvalidOperationException("One or more QueueTest native hooks were not installed.");

            InstallOptionalDrawFilter(libraryHandle, memory);

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTargetOrder));
            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnMoveOrder));
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => OnMapStart()));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => ResetMapState("load-save")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => ResetMapState("unload")));
            GameTimeManagerAPI.Instance.OnTick += OnTick;

            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"INITIALIZED: waypointRva=0x{resolution.Rva:X}, functionSize=71, " +
                $"movementCompleteRva=0x{movementCompleteResolution.Rva:X}, " +
                $"overlayRenderRva=0x{overlayRenderResolution.Rva:X}, " +
                $"drawFilterInstalled={drawFilterInstalled}, GameTribeSize=0x{actualTribeSize:X}, " +
                $"queueLimit={MaximumPendingCommands}, " +
                "allModesEnabled=true. Command capture no longer depends on an OnStartMap event.");
        }

        private void InstallOptionalDrawFilter(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                    memory,
                    DrawSubmissionPattern,
                    ReferenceDrawSubmissionRva,
                    referenceHashMatches: true,
                    name: "Vanilla overlay draw submission",
                    log: log);
                if (resolution.Rva != ReferenceDrawSubmissionRva)
                    throw new InvalidOperationException($"Draw submission resolved at unexpected RVA 0x{resolution.Rva:X}.");

                drawFilterTransaction = new HookTransaction(
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                drawFilterTransaction.AddDetour(
                    ref drawSubmissionHook,
                    unchecked((ulong)libraryHandle.ToInt64()) + ReferenceDrawSubmissionRva,
                    SubmitOverlayMarker);
                drawFilterTransaction.Commit();
                drawFilterInstalled = drawSubmissionHook.Success;
                if (!drawFilterInstalled)
                    throw new InvalidOperationException("Draw-submission hook reported no success.");
            }
            catch (Exception exception)
            {
                drawFilterInstalled = false;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"VISUAL_FLAG_FILTER_FALLBACK: numbered attack targets retain a Move flag; {exception.Message}");
            }
        }

        private void OnMapStart()
        {
            queues.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
            loggedOverlayProjections.Clear();
            tickMarkerLogged = false;

            bool multiplayer = GameNetworkAPI.IsMultiplayerGame();
            bool mapEditor = GamePlayerManagerAPI.Instance.IsInMapEditor();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MAP_START: QueueTest enabled; multiplayer={multiplayer}, mapEditor={mapEditor}." +
                (multiplayer
                    ? " Multiplayer support is experimental with Script Extender 1.42.0."
                    : string.Empty));
        }

        private void ResetMapState(string reason)
        {
            int discarded = queues.Count;
            queues.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
            loggedOverlayProjections.Clear();
            tickMarkerLogged = false;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MAP_RESET: reason={reason}, discardedTribeQueues={discarded}, allModesRemainEnabled=true.");
        }

        private void OnTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!installed || internalDispatch || !IsLocalSelectedTribe(args.TribeId, out GameTribe* tribe))
                return;

            int commandValue = (int)args.AICommand;
            bool supported = QueueCommandClassifier.TryClassifyTarget(commandValue, out QueueCommandKind kind);
            bool shiftPressed = IsShiftPressed();

            if (!shiftPressed)
            {
                CancelQueue(args.TribeId, "non-shift target order");
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
                CancelQueue(args.TribeId, $"unsupported Shift target order {args.AICommand}");
                observedAttacks.Remove(args.TribeId);
                if (loggedUnsupportedCommands.Add(commandValue))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"UNSUPPORTED_SHIFT_COMMAND: command={args.AICommand}/{commandValue}, " +
                        $"arg1={args.TargetValue1}, arg2={args.TargetValue2}, arg3={args.a6}; Vanilla handles it.");
                }
                return;
            }

            TribeQueueState state = GetOrCreateState(args.TribeId, tribe);
            QueueCommand command = new QueueCommand(kind, args.TargetValue1, args.TargetValue2, args.a6);
            if (!state.TryEnqueue(command))
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
            Shared.DebugLogHelper.LogInfo(
                log,
                $"ENQUEUE: tribeId={args.TribeId}, command={command}, pending={state.PendingCount}.");
        }

        private void OnMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!installed || internalDispatch || !IsLocalSelectedTribe(args.TribeId, out GameTribe* tribe))
                return;

            if (!IsShiftPressed())
            {
                observedAttacks.Remove(args.TribeId);
                CancelQueue(args.TribeId, "non-shift move order");
                return;
            }

            if (!queues.TryGetValue(args.TribeId, out TribeQueueState state) ||
                !state.MatchesTribe(tribe->r_GlobalId))
            {
                return;
            }

            QueueCommand command = new QueueCommand(
                QueueCommandKind.Move,
                args.TileX,
                args.TileY,
                (int)args.MoveType);
            if (state.TryConsumeExpectedMoveEvent(command, currentTick))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"SUPPRESS_DUPLICATE_MOVE_EVENT: tribeId={args.TribeId}, command={command}, " +
                    $"expectedEvents={state.ExpectedMoveEventCount}.");
            }
            else if (!state.TryEnqueue(command))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"QUEUE_FULL: tribeId={args.TribeId}, limit={MaximumPendingCommands}, rejected={command}.");
            }
            else
            {
                // The already-created input chore can still reach the native append helper
                // even though this order callback is skipped. Match it there and suppress it.
                state.ExpectMoveChore(command, currentTick + ExpectedMoveChoreLifetimeTicks);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"ENQUEUE_MOVE_EVENT: tribeId={args.TribeId}, command={command}, " +
                    $"pending={state.PendingCount}, expectedChores={state.ExpectedMoveChoreCount}.");
            }

            args.SkipOriginalFunction = true;
            args.ReturnValue = 1;
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
                // Chore 8 serializes the same one-based game ID used by the manager APIs.
                // Treating it as a span index misses the real queue and lets Vanilla append
                // the Move a second time, which breaks mixed-command ordering.
                if (installed && !internalDispatch && queues.TryGetValue(tribeId, out TribeQueueState state) &&
                    TryGetMatchingAliveTribe(tribeId, state.TribeGlobalId, out GameTribe* tribe))
                {
                    QueueCommand command = new QueueCommand(QueueCommandKind.Move, tileX, tileY, moveMode);
                    if (state.TryConsumeExpectedMoveChore(command, currentTick))
                    {
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"SUPPRESS_MOVE_CHORE: tribeId={tribeId}, nativeIndex={waypointIndex}, " +
                            $"command={command}, expectedChores={state.ExpectedMoveChoreCount}.");
                        return;
                    }

                    if (state.TryEnqueue(command))
                    {
                        // Some native input paths may reach the Chore hook first. In that
                        // case this is the authoritative insertion and the later event is deduplicated.
                        state.ExpectMoveEvent(command, currentTick + ExpectedMoveChoreLifetimeTicks);
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"ENQUEUE_MOVE_CHORE_FALLBACK: tribeId={tribeId}, nativeIndex={waypointIndex}, " +
                            $"command={command}, pending={state.PendingCount}, " +
                            $"expectedEvents={state.ExpectedMoveEventCount}.");
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
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"WAYPOINT_HOOK_FAIL_OPEN: {exception}");
            }

            waypointAppendHook.Value.Hook.Trampoline(
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
                    tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
            }
        }

        private void RenderTribeOverlayCore(IntPtr tribeManager, int tribeId, ref bool trampolineEntered)
        {
            if (!installed || !queues.TryGetValue(tribeId, out TribeQueueState state) ||
                !TryGetMatchingAliveTribe(tribeId, state.TribeGlobalId, out GameTribe* tribe))
            {
                trampolineEntered = true;
                tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
                return;
            }

            ushort savedCount = ReadMovementWaypointCount(tribe);
            int firstManagedPoint = state.WaitForVanillaMovement && savedCount != 0
                ? Math.Min((int)savedCount, MaximumNativeMovementWaypoints)
                : 1;
            int managedCapacity = MaximumNativeMovementWaypoints - firstManagedPoint;
            List<VisualQueueEntry> visualEntries = BuildVisualQueueEntries(state, managedCapacity);
            if (visualEntries.Count == 0)
            {
                trampolineEntered = true;
                tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
                return;
            }

            ushort* points = &tribe->r_PatrolPoint1TileX;
            ushort* savedPoints = stackalloc ushort[MaximumNativeMovementWaypoints * 2];
            for (int index = 0; index < MaximumNativeMovementWaypoints * 2; index++)
                savedPoints[index] = points[index];

            ushort savedIndex = tribe->r_PatrolCurrentTargetIndex;
            // The 1.42.0 interop declares this native ushort as UInt32. Read and write
            // the proven 16-bit field directly so the adjacent word cannot affect capacity.
            TribePatrolMode savedMode = tribe->r_PatrolMode;
            string visualSequence = string.Join(
                ">",
                visualEntries.Select(entry => entry.Command.ToString()));
            string projectionKey = $"{tribeId}/{state.TribeGlobalId}/{firstManagedPoint}/{visualSequence}";
            if (loggedOverlayProjections.Add(projectionKey))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"OVERLAY_SEQUENCE_PROJECTED: tribeId={tribeId}, nativePoints={savedCount}, " +
                    $"firstManagedNumber={firstManagedPoint}, entries={visualSequence}, " +
                    $"drawFilter={drawFilterInstalled}, savedMode={savedMode}.");
            }
            try
            {
                if (!state.WaitForVanillaMovement || savedCount == 0)
                {
                    // PatrolOnce omits point zero. It is only the visual origin here;
                    // every pending Move or attack starts at point one.
                    ushort originX = visualEntries[0].TileX;
                    ushort originY = visualEntries[0].TileY;
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(
                            tribe->r_LeaderUnitId,
                            out GameUnit* leader) && leader != null)
                    {
                        originX = leader->r_CurrentTilePositionX;
                        originY = leader->r_CurrentTilePositionY;
                    }

                    points[0] = originX;
                    points[1] = originY;
                }

                for (int index = 0; index < visualEntries.Count; index++)
                {
                    int pointIndex = firstManagedPoint + index;
                    points[pointIndex * 2] = visualEntries[index].TileX;
                    points[pointIndex * 2 + 1] = visualEntries[index].TileY;
                }
                tribe->r_PatrolCurrentTargetIndex = 0;
                WriteMovementWaypointCount(
                    tribe,
                    unchecked((ushort)(firstManagedPoint + visualEntries.Count)));

                // These values exist only while Vanilla emits overlay draw commands. They are
                // restored before simulation code can observe or execute the projected path.
                tribe->r_PatrolMode = TribePatrolMode.PatrolOnce;
                List<QueueCommandKind> projectedKinds = new List<QueueCommandKind>(
                    firstManagedPoint - 1 + visualEntries.Count);
                for (int index = 1; index < firstManagedPoint; index++)
                    projectedKinds.Add(QueueCommandKind.Move);
                projectedKinds.AddRange(visualEntries.Select(entry => entry.Command.Kind));
                overlayProjectedKinds = projectedKinds;
                overlayMarkerCallIndex = 0;
                overlayRenderThreadId = Thread.CurrentThread.ManagedThreadId;
                overlayDrawFilterActive = drawFilterInstalled;
                trampolineEntered = true;
                tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
                ProjectAttackTargetMarkers(tribeId, visualEntries, projectionKey);
            }
            finally
            {
                overlayDrawFilterActive = false;
                overlayRenderThreadId = 0;
                overlayMarkerCallIndex = 0;
                overlayProjectedKinds = Array.Empty<QueueCommandKind>();
                tribe->r_PatrolMode = savedMode;
                tribe->r_PatrolCurrentTargetIndex = savedIndex;
                WriteMovementWaypointCount(tribe, savedCount);
                for (int index = 0; index < MaximumNativeMovementWaypoints * 2; index++)
                    points[index] = savedPoints[index];
            }
        }

        private List<VisualQueueEntry> BuildVisualQueueEntries(TribeQueueState state, int maximum)
        {
            List<VisualQueueEntry> entries = new List<VisualQueueEntry>(Math.Max(0, maximum));
            if (maximum <= 0)
                return entries;

            foreach (QueueCommand command in state.GetPendingCommands(
                maximum,
                command => TryResolveVisualTarget(command, out _, out _)))
            {
                if (!TryResolveVisualTarget(command, out ushort tileX, out ushort tileY))
                    continue;

                entries.Add(new VisualQueueEntry(command, tileX, tileY));
                if (entries.Count == maximum)
                    break;
            }
            return entries;
        }

        private static bool TryResolveVisualTarget(QueueCommand command, out ushort tileX, out ushort tileY)
        {
            tileX = 0;
            tileY = 0;
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
            return true;
        }

        private void ProjectAttackTargetMarkers(
            int tribeId,
            IReadOnlyList<VisualQueueEntry> entries,
            string projectionKey)
        {
            int projected = 0;
            foreach (VisualQueueEntry entry in entries)
            {
                QueueCommand command = entry.Command;
                if (command.Kind == QueueCommandKind.AttackUnit)
                {
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(command.Argument1, out GameUnit* unit) &&
                        unit != null && unit->r_AliveState == AliveState.IsAlive &&
                        unit->r_CurrentHealth > 0 &&
                        unit->r_GlobalId == unchecked((uint)command.Argument2))
                    {
                        *(ushort*)((byte*)unit + QueueNativeContract.GameUnitAttackMarkerOffset) = 1;
                        projected++;
                    }
                }
                else if (command.Kind == QueueCommandKind.AttackBuilding ||
                    command.Kind == QueueCommandKind.ForceAttackBuilding)
                {
                    if (GameBuildingManagerAPI.Instance.TryGetBuildingById(
                            command.Argument1,
                            out GameBuilding* building) &&
                        building != null && building->r_AliveState == AliveState.IsAlive &&
                        building->r_CurrentHealth > 0 &&
                        building->r_GlobalId == unchecked((uint)command.Argument2))
                    {
                        *(ushort*)((byte*)building + QueueNativeContract.GameBuildingAttackMarkerOffset) = 1;
                        projected++;
                    }
                }
            }

            if (projected != 0 && loggedOverlayProjections.Add(projectionKey + "/attack-markers"))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"ATTACK_MARKERS_PROJECTED: tribeId={tribeId}, count={projected}.");
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
                if (overlayDrawFilterActive &&
                    overlayRenderThreadId == Thread.CurrentThread.ManagedThreadId &&
                    QueueVisualContract.IsPatrolOnceFlagSubmission(
                        category,
                        spriteId,
                        layer,
                        verticalOffset,
                        flags))
                {
                    int markerIndex = overlayMarkerCallIndex++;
                    if (markerIndex < overlayProjectedKinds.Count &&
                        QueueVisualContract.ShouldSuppressFlag(
                            overlayProjectedKinds[markerIndex],
                            category,
                            spriteId,
                            layer,
                            verticalOffset,
                            flags))
                    {
                        return;
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

            drawSubmissionHook.Value.Hook.Trampoline(
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

            if (!tickMarkerLogged)
            {
                tickMarkerLogged = true;
                Shared.DebugLogHelper.LogInfo(log, $"RUNTIME_TICK: tick={tick}, queueCount={queues.Count}.");
            }

            foreach (int tribeId in queues.Keys.ToArray())
                ProcessQueue(tribeId, tick);
        }

        private void ProcessQueue(int tribeId, int tick)
        {
            if (!queues.TryGetValue(tribeId, out TribeQueueState state))
                return;
            int originalTribeId = tribeId;
            if (!TryGetMatchingAliveTribe(tribeId, state.TribeGlobalId, out GameTribe* tribe))
            {
                if (!TryMigrateQueue(tribeId, state, out tribeId, out tribe))
                {
                    queues.Remove(originalTribeId);
                    observedAttacks.Remove(originalTribeId);
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"QUEUE_DISCARDED: tribeId={originalTribeId}, " +
                        $"reason=no-live-common-replacement-tribe, " +
                        $"members={FormatMembers(state.Members)}.");
                    return;
                }
            }

            if (state.ExternalAttack != null)
            {
                if (IsTargetAlive(state.ExternalAttack))
                {
                    LogQueueWait(state, tick, tribeId, tribe, "predecessor-attack");
                    return;
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"PREDECESSOR_COMPLETE: tick={tick}, tribeId={tribeId}, command={state.ExternalAttack}.");
                state.ExternalAttack = null;
                observedAttacks.Remove(tribeId);
                state.ResetWaitDiagnostic();
            }

            if (state.WaitForVanillaMovement)
            {
                if (!HasCompletedMovementSequence(tribeId, tribe, out bool reached))
                {
                    LogQueueWait(state, tick, tribeId, tribe, "vanilla-movement", reached);
                    return;
                }

                state.WaitForVanillaMovement = false;
                state.ResetWaitDiagnostic();
                Shared.DebugLogHelper.LogInfo(log, $"VANILLA_MOVEMENT_COMPLETE: tick={tick}, tribeId={tribeId}.");
            }

            if (state.Active != null)
            {
                if (state.ActiveNeedsRedispatch)
                {
                    if (state.Active.IsAttack && !IsTargetAlive(state.Active))
                    {
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"COMPLETE_DURING_MIGRATION: tick={tick}, tribeId={tribeId}, " +
                            $"command={state.Active}.");
                        state.CompleteActive();
                    }
                    else if (Dispatch(tribeId, state.Active))
                    {
                        state.MarkActiveRedispatched();
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"REDISPATCH_AFTER_MIGRATION: tick={tick}, tribeId={tribeId}, " +
                            $"command={state.Active}.");
                        return;
                    }
                    else
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"REDISPATCH_FAILED: tick={tick}, tribeId={tribeId}, " +
                            $"command={state.Active}; skipping.");
                        state.CompleteActive();
                    }
                }

                if (state.Active != null)
                {
                    bool reached = false;
                    bool complete = state.Active.Kind == QueueCommandKind.Move
                        ? HasCompletedMovementSequence(tribeId, tribe, out reached)
                        : !IsTargetAlive(state.Active);
                    if (!complete)
                    {
                        LogQueueWait(
                            state,
                            tick,
                            tribeId,
                            tribe,
                            state.Active.Kind == QueueCommandKind.Move ? "managed-movement" : "managed-attack",
                            state.Active.Kind == QueueCommandKind.Move ? reached : (bool?)null);
                        return;
                    }

                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"COMPLETE: tick={tick}, tribeId={tribeId}, command={state.Active}.");
                    state.CompleteActive();
                }
            }

            while (state.TryActivateNext(out QueueCommand command))
            {
                if (command.IsAttack && !IsTargetAlive(command))
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"SKIP_INVALID_TARGET: tick={tick}, tribeId={tribeId}, command={command}.");
                    state.CompleteActive();
                    continue;
                }

                bool issued = Dispatch(tribeId, command);
                if (issued)
                {
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"DISPATCH: tick={tick}, tribeId={tribeId}, command={command}, remaining={state.PendingCount}.");
                    return;
                }

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"DISPATCH_FAILED: tick={tick}, tribeId={tribeId}, command={command}; skipping.");
                state.CompleteActive();
            }

            if (state.IsEmpty)
            {
                queues.Remove(tribeId);
                Shared.DebugLogHelper.LogInfo(log, $"QUEUE_EMPTY: tick={tick}, tribeId={tribeId}.");
            }
        }

        private bool HasCompletedMovementSequence(int tribeId, GameTribe* tribe, out bool reached)
        {
            reached = isTribeMovementComplete(tribeManagerPointer, tribeId) == 1;
            return ReadMovementMode(tribe) == 0 && reached;
        }

        private void LogQueueWait(
            TribeQueueState state,
            int tick,
            int tribeId,
            GameTribe* tribe,
            string reason,
            bool? reached = null)
        {
            if (!state.ShouldLogWaitDiagnostic(tick, WaitDiagnosticIntervalTicks))
                return;

            string reachedText = reached.HasValue ? reached.Value.ToString() : "not-applicable";
            Shared.DebugLogHelper.LogInfo(
                log,
                $"QUEUE_WAIT: tick={tick}, tribeId={tribeId}, reason={reason}, reached={reachedText}, " +
                $"movementMode={ReadMovementMode(tribe)}, waypointIndex={ReadMovementWaypointIndex(tribe)}, " +
                $"waypointCount={ReadMovementWaypointCount(tribe)}, active={state.Active}, " +
                $"pending={state.PendingCount}.");
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

            if (issued && command.IsAttack)
                LogAttackDispatchEvidence(tribeId, command);
            return issued;
        }

        private void LogAttackDispatchEvidence(int tribeId, QueueCommand command)
        {
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) ||
                tribe == null ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(tribe->r_LeaderUnitId, out GameUnit* leader) ||
                leader == null)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"DISPATCH_EVIDENCE_UNAVAILABLE: tribeId={tribeId}, command={command}.");
                return;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"DISPATCH_EVIDENCE: tribeId={tribeId}, leaderUnitId={tribe->r_LeaderUnitId}, " +
                $"lastCommand={leader->r_AI_LastIssuedTribeCommand}, " +
                $"targetUnitId={leader->r_AI_ContextTargetUnitId}, " +
                $"targetUnitGlobalId={leader->r_AI_ContextTargetUnitGlobalId}, command={command}.");
        }

        private TribeQueueState GetOrCreateState(int tribeId, GameTribe* tribe)
        {
            if (queues.TryGetValue(tribeId, out TribeQueueState existing) &&
                existing.MatchesTribe(tribe->r_GlobalId))
            {
                return existing;
            }

            List<QueueUnitIdentity> selectedMembers = CaptureSelectedMembers(tribeId);
            KeyValuePair<int, TribeQueueState>? related = queues
                .FirstOrDefault(pair => pair.Value.SharesMemberWith(selectedMembers));
            if (related.HasValue && related.Value.Value != null)
            {
                int oldTribeId = related.Value.Key;
                TribeQueueState migrated = related.Value.Value;
                queues.Remove(oldTribeId);
                observedAttacks.Remove(oldTribeId);
                migrated.RebindTribe(tribe->r_GlobalId);
                queues[tribeId] = migrated;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"QUEUE_MIGRATED_ON_INPUT: oldTribeId={oldTribeId}, newTribeId={tribeId}, " +
                    $"newTribeGlobalId={tribe->r_GlobalId}, members={FormatMembers(migrated.Members)}.");
                return migrated;
            }

            TribeQueueState state = new TribeQueueState(
                tribe->r_GlobalId,
                MaximumPendingCommands,
                selectedMembers)
            {
                WaitForVanillaMovement = ReadMovementWaypointCount(tribe) != 0
            };
            if (observedAttacks.TryGetValue(tribeId, out ObservedAttack observed) &&
                observed.TribeGlobalId == tribe->r_GlobalId && IsTargetAlive(observed.Command))
            {
                state.ExternalAttack = observed.Command;
            }

            queues[tribeId] = state;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"QUEUE_CREATED: tribeId={tribeId}, tribeGlobalId={tribe->r_GlobalId}, " +
                $"members={FormatMembers(state.Members)}, " +
                $"nativeWaypointIndex={ReadMovementWaypointIndex(tribe)}, " +
                $"nativeWaypointCount={ReadMovementWaypointCount(tribe)}, " +
                $"externalAttack={(state.ExternalAttack == null ? "none" : state.ExternalAttack.ToString())}.");
            return state;
        }

        private void CancelQueue(int tribeId, string reason)
        {
            if (queues.Remove(tribeId))
                Shared.DebugLogHelper.LogInfo(log, $"QUEUE_CANCELLED: tribeId={tribeId}, reason={reason}.");

            List<QueueUnitIdentity> selectedMembers = CaptureSelectedMembers(tribeId);
            foreach (int relatedTribeId in queues
                .Where(pair => pair.Value.SharesMemberWith(selectedMembers))
                .Select(pair => pair.Key)
                .ToArray())
            {
                queues.Remove(relatedTribeId);
                observedAttacks.Remove(relatedTribeId);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"QUEUE_CANCELLED: tribeId={relatedTribeId}, commandTribeId={tribeId}, " +
                    $"reason={reason}, matchedByUnitIdentity=true.");
            }
        }

        private bool TryMigrateQueue(
            int oldTribeId,
            TribeQueueState state,
            out int newTribeId,
            out GameTribe* newTribe)
        {
            newTribeId = 0;
            newTribe = null;
            List<string> memberStates = new List<string>();

            foreach (QueueUnitIdentity member in state.Members)
            {
                if (!GameUnitManagerAPI.Instance.IsValidId(member.UnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(member.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_GlobalId != member.GlobalId)
                {
                    memberStates.Add($"{member}:gone");
                    continue;
                }

                int memberTribeId = unit->r_TribeId;
                memberStates.Add($"{member}:tribe={memberTribeId}");
                if (memberTribeId <= 0)
                    continue;
                if (newTribeId != 0 && newTribeId != memberTribeId)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"QUEUE_MIGRATION_AMBIGUOUS: oldTribeId={oldTribeId}, " +
                        $"memberStates={string.Join(",", memberStates)}.");
                    return false;
                }
                newTribeId = memberTribeId;
            }

            if (newTribeId <= 0 || queues.ContainsKey(newTribeId) ||
                !GameTribeManagerAPI.Instance.IsValidId(newTribeId) ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(newTribeId, out newTribe) ||
                newTribe == null || newTribe->r_AliveState != AliveState.IsAlive ||
                newTribe->r_PlayerIdOwner != GameNetworkAPI.GetLocalPlayerId())
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"QUEUE_MIGRATION_UNAVAILABLE: oldTribeId={oldTribeId}, candidateTribeId={newTribeId}, " +
                    $"memberStates={string.Join(",", memberStates)}.");
                newTribe = null;
                return false;
            }

            queues.Remove(oldTribeId);
            observedAttacks.Remove(oldTribeId);
            state.RebindTribe(newTribe->r_GlobalId);
            queues[newTribeId] = state;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"QUEUE_MIGRATED: oldTribeId={oldTribeId}, newTribeId={newTribeId}, " +
                $"newTribeGlobalId={newTribe->r_GlobalId}, memberStates={string.Join(",", memberStates)}.");
            return true;
        }

        private static List<QueueUnitIdentity> CaptureSelectedMembers(int tribeId)
        {
            List<QueueUnitIdentity> members = new List<QueueUnitIdentity>();
            int[] selectedUnitIds = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int unitId = selectedUnitIds[index];
                if (!GameUnitManagerAPI.Instance.IsValidId(unitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive || unit->r_TribeId != tribeId)
                {
                    continue;
                }
                members.Add(new QueueUnitIdentity(unitId, unit->r_GlobalId));
            }
            return members;
        }

        private static string FormatMembers(IReadOnlyList<QueueUnitIdentity> members) =>
            members.Count == 0 ? "none" : string.Join(",", members);

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

            int[] selectedUnitIds = GamePlayerManagerAPI.Instance.GetSelectedChimps();
            for (int index = 0; index < selectedUnitIds.Length; index++)
            {
                int unitId = selectedUnitIds[index];
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

        private static bool TryGetMatchingAliveTribe(int tribeId, uint expectedGlobalId, out GameTribe* tribe)
        {
            tribe = null;
            return GameTribeManagerAPI.Instance.IsValidId(tribeId) &&
                GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out tribe) &&
                tribe != null &&
                tribe->r_AliveState == AliveState.IsAlive &&
                tribe->r_GlobalId == expectedGlobalId;
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

        private sealed class VisualQueueEntry
        {
            public VisualQueueEntry(QueueCommand command, ushort tileX, ushort tileY)
            {
                Command = command;
                TileX = tileX;
                TileY = tileY;
            }

            public QueueCommand Command { get; }
            public ushort TileX { get; }
            public ushort TileY { get; }
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
    }
}
