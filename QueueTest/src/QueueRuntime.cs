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
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly List<int> queueIdBuffer = new List<int>();
        private readonly List<VisualQueueEntry> visualEntryBuffer = new List<VisualQueueEntry>(9);
        private readonly List<QueueVisualMarkerMode> projectedModeBuffer =
            new List<QueueVisualMarkerMode>(9);
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
        private bool runtimeTickLogged;
        private int currentTick;
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
                referenceHashMatches: true,
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
                referenceHashMatches: true,
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
                referenceHashMatches: true,
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
                .Subscribe(args => ResetMapState()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => ResetMapState()));
            GameTimeManagerAPI.Instance.OnTick += OnTick;

            installed = true;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"INITIALIZED: waypointRva=0x{resolution.Rva:X}, functionSize=71, " +
                $"movementCompleteRva=0x{movementCompleteResolution.Rva:X}, " +
                $"overlayRenderRva=0x{overlayRenderResolution.Rva:X}, " +
                $"drawFilterInstalled={drawFilterInstalled}, " +
                $"targetMarkerProjectionAvailable={targetMarkerProjectionAvailable}, " +
                $"GameTribeSize=0x{actualTribeSize:X}, " +
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
                    log: null);
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
                    $"VISUAL_FLAG_FILTER_FALLBACK: command queues and all destination flags remain enabled, " +
                    $"but hidden markers and future-page numbers may remain visible; {exception.Message}");
            }
        }

        private void OnMapStart()
        {
            queues.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
        }

        private void ResetMapState()
        {
            queues.Clear();
            observedAttacks.Clear();
            loggedUnsupportedCommands.Clear();
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
                CancelQueue(args.TribeId);
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
                CancelQueue(args.TribeId);
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
        }

        private void OnMoveOrder(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!installed || internalDispatch || !IsLocalSelectedTribe(args.TribeId, out GameTribe* tribe))
                return;

            if (!IsShiftPressed())
            {
                observedAttacks.Remove(args.TribeId);
                CancelQueue(args.TribeId);
                return;
            }

            TribeQueueState state = GetOrCreateState(args.TribeId, tribe);

            QueueCommand command = new QueueCommand(
                QueueCommandKind.Move,
                args.TileX,
                args.TileY,
                (int)args.MoveType);
            bool duplicateEvent = state.TryConsumeExpectedMoveEvent(command, currentTick);
            if (!duplicateEvent)
            {
                if (!state.TryEnqueue(command))
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
                }
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
                if (installed && !internalDispatch)
                {
                    TribeQueueState state = null;
                    if (queues.TryGetValue(tribeId, out TribeQueueState existing) &&
                        TryGetMatchingAliveTribe(tribeId, existing.TribeGlobalId, out _))
                    {
                        state = existing;
                    }
                    else if (IsShiftPressed() &&
                        IsLocalSelectedTribe(tribeId, out GameTribe* selectedTribe))
                    {
                        // The native Chore can be observed before the managed move event.
                        // Create the queue before Vanilla writes its capacity-limited waypoint.
                        state = GetOrCreateState(tribeId, selectedTribe);
                    }

                    if (state != null)
                    {
                        QueueCommand command = new QueueCommand(QueueCommandKind.Move, tileX, tileY, moveMode);
                        if (state.TryConsumeExpectedMoveChore(command, currentTick))
                        {
                            return;
                        }

                        if (state.TryEnqueue(command))
                        {
                            // Some native input paths may reach the Chore hook first. In that
                            // case this is the authoritative insertion and the later event is deduplicated.
                            state.ExpectMoveEvent(command, currentTick + ExpectedMoveChoreLifetimeTicks);
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

            if (state.WaitForVanillaMovement)
                state.UpdateVanillaVisualProgress(ReadMovementWaypointIndex(tribe));

            if (state.CurrentVisualSlots.Count == 0)
            {
                trampolineEntered = true;
                tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
                return;
            }

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
            // The 1.42.0 interop declares this native ushort as UInt32. Read and write
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
                    tribeOverlayRenderHook.Value.Hook.Trampoline(tribeManager, tribeId);
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
            if (!runtimeTickLogged)
            {
                runtimeTickLogged = true;
                Shared.DebugLogHelper.LogInfo(log, $"RUNTIME_ACTIVE: firstTick={tick}.");
            }
            queueIdBuffer.Clear();
            foreach (int tribeId in queues.Keys)
                queueIdBuffer.Add(tribeId);
            for (int index = 0; index < queueIdBuffer.Count; index++)
                ProcessQueue(queueIdBuffer[index]);
        }

        private void ProcessQueue(int tribeId)
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
                    return;
                }
            }

            if (state.ExternalAttack != null)
            {
                if (IsTargetAlive(state.ExternalAttack))
                    return;

                state.ExternalAttack = null;
                observedAttacks.Remove(tribeId);
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
                queues.Remove(tribeId);
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

        private TribeQueueState GetOrCreateState(int tribeId, GameTribe* tribe)
        {
            if (queues.TryGetValue(tribeId, out TribeQueueState existing) &&
                existing.MatchesTribe(tribe->r_GlobalId))
            {
                return existing;
            }

            List<QueueUnitIdentity> selectedMembers = CaptureSelectedMembers(tribeId);
            int relatedTribeId = 0;
            TribeQueueState relatedState = null;
            foreach (KeyValuePair<int, TribeQueueState> pair in queues)
            {
                if (!pair.Value.SharesMemberWith(selectedMembers))
                    continue;
                relatedTribeId = pair.Key;
                relatedState = pair.Value;
                break;
            }
            if (relatedState != null)
            {
                int oldTribeId = relatedTribeId;
                TribeQueueState migrated = relatedState;
                queues.Remove(oldTribeId);
                observedAttacks.Remove(oldTribeId);
                migrated.RebindTribe(tribe->r_GlobalId);
                queues[tribeId] = migrated;
                return migrated;
            }

            TribeQueueState state = new TribeQueueState(
                tribe->r_GlobalId,
                MaximumPendingCommands,
                selectedMembers);
            ushort nativeWaypointCount = ReadMovementWaypointCount(tribe);
            bool hasExternalAttack = observedAttacks.TryGetValue(tribeId, out ObservedAttack observed) &&
                observed.TribeGlobalId == tribe->r_GlobalId &&
                IsTargetAlive(observed.Command);
            state.WaitForVanillaMovement = !hasExternalAttack &&
                nativeWaypointCount != 0 &&
                !HasCompletedMovementSequence(tribeId, tribe);
            if (state.WaitForVanillaMovement)
                CaptureVanillaVisualSlots(state, tribe, nativeWaypointCount);
            if (hasExternalAttack)
                state.ExternalAttack = observed.Command;

            queues[tribeId] = state;
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

        private void CancelQueue(int tribeId)
        {
            queues.Remove(tribeId);

            List<QueueUnitIdentity> selectedMembers = CaptureSelectedMembers(tribeId);
            List<int> relatedTribeIds = null;
            foreach (KeyValuePair<int, TribeQueueState> pair in queues)
            {
                if (!pair.Value.SharesMemberWith(selectedMembers))
                    continue;
                if (relatedTribeIds == null)
                    relatedTribeIds = new List<int>();
                relatedTribeIds.Add(pair.Key);
            }
            if (relatedTribeIds == null)
                return;

            foreach (int relatedTribeId in relatedTribeIds)
            {
                queues.Remove(relatedTribeId);
                observedAttacks.Remove(relatedTribeId);
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

            foreach (QueueUnitIdentity member in state.Members)
            {
                if (!GameUnitManagerAPI.Instance.IsValidId(member.UnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(member.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_GlobalId != member.GlobalId)
                {
                    continue;
                }

                int memberTribeId = unit->r_TribeId;
                if (memberTribeId <= 0)
                    continue;
                if (newTribeId != 0 && newTribeId != memberTribeId)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"QUEUE_MIGRATION_AMBIGUOUS: oldTribeId={oldTribeId}, " +
                        $"candidates={newTribeId}/{memberTribeId}; queue discarded.");
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
                newTribe = null;
                return false;
            }

            queues.Remove(oldTribeId);
            observedAttacks.Remove(oldTribeId);
            state.RebindTribe(newTribe->r_GlobalId);
            queues[newTribeId] = state;
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
    }
}
