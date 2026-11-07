// Feature: Project queued Shift commands onto Vanilla's tribe overlay and filter its markers.
using BepInEx.Logging;
using RedBird.Abstractions.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace Pathfinding
{
    internal sealed unsafe class ExtendedShiftOverlayRenderer
    {
        private const int MaximumNativeMovementWaypoints =
            UnitCommandPathRuntime.MaximumNativeMovementWaypoints;
        private const int MovementWaypointCountOffset = QueueNativeContract.GameTribeWaypointCountOffset;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void RenderTribeOverlayDelegate(IntPtr tribeManager, int tribeId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void DrawSubmissionDelegate(
            IntPtr drawManager,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags);

        private readonly ManualLogSource log;
        private readonly LargeMoveTargetMarkerRuntime largeMoveTargets;
        private readonly List<VisualQueueEntry> visualEntryBuffer = new List<VisualQueueEntry>(9);
        private readonly List<QueueVisualMarkerMode> projectedModeBuffer =
            new List<QueueVisualMarkerMode>(9);
        private readonly DetourHandle<RenderTribeOverlayDelegate> tribeOverlayRenderHook =
            new DetourHandle<RenderTribeOverlayDelegate>();
        private readonly DetourHandle<DrawSubmissionDelegate> drawSubmissionHook =
            new DetourHandle<DrawSubmissionDelegate>();
        private bool drawFilterFailureLogged;
        private bool overlayDrawFilterActive;
        private bool overlayShowPageNumbers;
        private bool overlaySawQueueMarker;
        private int overlayRenderThreadId;
        private int overlayFlagCallIndex;
        private int overlayNumberCallIndex;
        private IReadOnlyList<QueueVisualMarkerMode> overlayProjectedModes =
            Array.Empty<QueueVisualMarkerMode>();

        internal ExtendedShiftOverlayRenderer(
            ManualLogSource log,
            LargeMoveTargetMarkerRuntime largeMoveTargets)
        {
            this.log = log;
            this.largeMoveTargets = largeMoveTargets;
        }

        internal DetourHandle<RenderTribeOverlayDelegate> TribeOverlayRenderHook => tribeOverlayRenderHook;
        internal DetourHandle<DrawSubmissionDelegate> DrawSubmissionHook => drawSubmissionHook;
        internal bool DrawFilterInstalled { get; set; }
        internal bool TargetMarkerProjectionAvailable { get; set; }

        internal void InvokeOriginalTribeOverlay(IntPtr tribeManager, int tribeId)
        {
            bool completed = false;
            largeMoveTargets.BeginOverlayPass(tribeId);
            try
            {
                tribeOverlayRenderHook.Original(tribeManager, tribeId);
                completed = true;
            }
            finally
            {
                largeMoveTargets.EndOverlayPass(completed);
            }
        }

        internal void RenderCohortOverlay(
            IntPtr tribeManager,
            int tribeId,
            GameTribe* tribe,
            TribeQueueState state,
            ref bool trampolineEntered)
        {
            ushort savedCount = ReadMovementWaypointCount(tribe);
            ushort originX = 0;
            ushort originY = 0;
            if (APIShared.UnitAccess.TryGetById(
                    tribe->r_LeaderUnitId,
                    out GameUnit* leader, out _) && leader != null)
            {
                originX = leader->r_CurrentTilePositionX;
                originY = leader->r_CurrentTilePositionY;
            }
            ushort* points = &tribe->r_PatrolPoint1TileX;
            ushort* savedPoints = stackalloc ushort[MaximumNativeMovementWaypoints * 2];
            for (int index = 0; index < MaximumNativeMovementWaypoints * 2; index++)
                savedPoints[index] = points[index];

            ushort savedIndex = tribe->r_PatrolCurrentTargetIndex;
            // The current interop declares this native ushort as UInt32. Read and write
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
                    overlayDrawFilterActive = DrawFilterInstalled;
                    trampolineEntered = true;
                    InvokeOriginalTribeOverlay(tribeManager, tribeId);
                    if (DrawFilterInstalled && !overlaySawQueueMarker)
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
                if (!APIShared.UnitAccess.TryGetById(command.Argument1, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_CurrentHealth <= 0 ||
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
            if (!TargetMarkerProjectionAvailable)
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

        internal void SubmitOverlayMarker(
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
                largeMoveTargets.TryCaptureOverflowCandidate(
                    drawManager,
                    category,
                    spriteId,
                    layer,
                    verticalOffset,
                    tileId,
                    flags);
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
                    Shared.DebugLogHelper.LogWarningLimited(log, "Pathfinding/ExtendedShiftCommandQueueRuntime/SubmitOverlayMarker", $"VISUAL_FLAG_FILTER_FAIL_OPEN: {exception.Message}");
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

        private static ushort ReadMovementWaypointCount(GameTribe* tribe) =>
            *(ushort*)((byte*)tribe + MovementWaypointCountOffset);

        private static void WriteMovementWaypointCount(GameTribe* tribe, ushort count) =>
            *(ushort*)((byte*)tribe + MovementWaypointCountOffset) = count;

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
    }
}
