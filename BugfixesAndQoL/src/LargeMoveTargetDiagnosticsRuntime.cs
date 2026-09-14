using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BugfixesAndQoL
{
    internal unsafe sealed class LargeMoveTargetDiagnosticsRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetMarkerRenderer renderer;
        private readonly Dictionary<int, TrackedMoveGroup> groups =
            new Dictionary<int, TrackedMoveGroup>();
        private readonly Dictionary<int, int> activeMarkerCounts = new Dictionary<int, int>();
        private readonly HashSet<int> observedOverlayMarkerTiles = new HashSet<int>();
        private readonly List<int> removedMarkerTileBuffer = new List<int>();
        private readonly List<int> addedMarkerTileBuffer = new List<int>();
        private int currentOverlayTribeId;
        private bool trackingAvailable;

        public LargeMoveTargetDiagnosticsRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            renderer = new LargeMoveTargetMarkerRenderer(log, () => FeatureEnabled);
        }

        private bool FeatureEnabled =>
            settings.EnableMod && settings.EnableMoveFormationEnhancements;

        public bool MarkerReplacementAvailable => renderer.ReplacementAvailable;

        public void SetPreview(IEnumerable<int> tileIds) =>
            renderer.SetPreviewMarkerTiles(tileIds);

        public void ClearPreview() => renderer.ClearPreviewMarkerTiles();

        public void Install(
            bool layoutValidated,
            bool markerReplacementEnabled,
            CrusaderLibraryLoadContext context,
            bool fixedLayoutHashValidated)
        {
            trackingAvailable = layoutValidated;
            if (trackingAvailable && markerReplacementEnabled)
            {
                try
                {
                    renderer.Install(context, fixedLayoutHashValidated);
                }
                catch (Exception exception)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"MOVE_TARGET_MARKER_HOOK_FAIL_CLOSED: limited Vanilla markers retained; " +
                        exception.Message);
                }
            }
        }

        public void CaptureSuccessfulMove(
            int tribeId,
            int commandX,
            int commandY,
            int tick,
            string source)
        {
            if (!trackingAvailable || !FeatureEnabled)
                return;
            if (groups.TryGetValue(tribeId, out TrackedMoveGroup previous))
                FinalizeGroup(previous, "command-replaced", forceInterrupt: true);

            bool hasSnapshot = MoveFormationCommandSnapshotStore.TryConsume(
                tribeId, commandX, commandY, out MoveFormationCommandSnapshot snapshot);
            if (!hasSnapshot && !MayContainLargeGroup(tribeId))
            {
                groups.Remove(tribeId);
                renderer.PublishMarkerTiles();
                return;
            }

            List<TrackedMoveUnit> units = hasSnapshot
                ? CaptureSnapshotUnits(tribeId, snapshot.Units)
                : CaptureTribeUnits(tribeId);
            if (!LargeMoveTargetDiagnosticsModel.ShouldTrack(units.Count))
            {
                groups.Remove(tribeId);
                renderer.PublishMarkerTiles();
                return;
            }

            int inferredAssassinStructureCalls = CountAssassinStructureTargets(units);
            string spacingSummary = hasSnapshot
                ? snapshot.Audit.FormatCompact(inferredAssassinStructureCalls)
                : "command-spacing-unavailable";
            var group = new TrackedMoveGroup(
                tribeId,
                source,
                units,
                spacingSummary,
                MarkerReplacementAvailable);
            groups[tribeId] = group;
            InitializeActiveMarkerTiles(group);
            renderer.PublishMarkerTiles();
        }

        public void OnTick(int tick)
        {
            if (!FeatureEnabled)
            {
                if (groups.Count != 0 || renderer.ReplacementActive)
                    Reset(tick, "setting-disabled");
            }
        }

        public void Reset(int tick, string reason)
        {
            renderer.ClearPreviewMarkerTiles();
            MoveFormationCommandSnapshotStore.Clear();
            foreach (TrackedMoveGroup group in groups.Values.ToArray())
                FinalizeGroup(group, reason, forceInterrupt: true);
            groups.Clear();
            currentOverlayTribeId = 0;
            observedOverlayMarkerTiles.Clear();
            renderer.PublishMarkerTiles();
        }

        public void ApplySetting(int tick)
        {
            if (!FeatureEnabled && (groups.Count != 0 || renderer.ReplacementActive))
                Reset(tick, "setting-disabled");
        }

        public void BeginOverlayPass(int tribeId)
        {
            currentOverlayTribeId = tribeId;
            observedOverlayMarkerTiles.Clear();
        }

        public void EndOverlayPass()
        {
            int tribeId = currentOverlayTribeId;
            currentOverlayTribeId = 0;
            if (!FeatureEnabled ||
                !groups.TryGetValue(tribeId, out TrackedMoveGroup group))
            {
                observedOverlayMarkerTiles.Clear();
                return;
            }

            if (observedOverlayMarkerTiles.Count == 0)
            {
                group.EmptyOverlayPasses++;
                bool completed =
                    LargeMoveTargetDiagnosticsModel.ShouldCompleteAfterEmptyOverlayPasses(
                        group.EmptyOverlayPasses);
                observedOverlayMarkerTiles.Clear();
                if (completed)
                {
                    FinalizeGroup(group, "completed", forceInterrupt: false);
                    renderer.PublishMarkerTiles();
                }
                return;
            }

            group.EmptyOverlayPasses = 0;
            bool changed = ReconcileObservedMarkerTiles(group);
            observedOverlayMarkerTiles.Clear();
            if (changed)
                renderer.PublishMarkerTiles();
        }

        public bool ObserveAndShouldSuppressMarker(
            IntPtr _drawManager,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags)
        {
            if (!FeatureEnabled ||
                !LargeMoveTargetDiagnosticsModel.IsVanillaMoveTargetMarker(
                    category, spriteId, layer, verticalOffset, flags) ||
                !groups.TryGetValue(currentOverlayTribeId, out TrackedMoveGroup group))
            {
                return false;
            }

            observedOverlayMarkerTiles.Add(tileId);
            return group.ActiveMarkerTiles.Contains(tileId) && renderer.ReplacementActive;
        }

        private bool ReconcileObservedMarkerTiles(TrackedMoveGroup group)
        {
            if (!LargeMoveTargetDiagnosticsModel.BuildMarkerDelta(
                group.ActiveMarkerTiles,
                observedOverlayMarkerTiles,
                removedMarkerTileBuffer,
                addedMarkerTileBuffer))
            {
                return false;
            }

            foreach (int tileId in removedMarkerTileBuffer)
                AdjustActiveMarkerCount(group, tileId, -1);
            foreach (int tileId in addedMarkerTileBuffer)
                AdjustActiveMarkerCount(group, tileId, 1);
            return true;
        }

        private void FinalizeGroup(
            TrackedMoveGroup group,
            string reason,
            bool forceInterrupt)
        {
            Span<GameUnit> unitSpan = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            if (forceInterrupt)
            {
                foreach (TrackedMoveUnit tracked in group.Units)
                {
                    if (TryGetMatchingLivingUnit(tracked, unitSpan, out int spanIndex))
                    {
                        ref GameUnit unit = ref unitSpan[spanIndex];
                        tracked.Actual = new MoveTargetCoordinate(
                            unit.r_CurrentTilePositionX,
                            unit.r_CurrentTilePositionY);
                        if (tracked.Kind == MoveTargetOutcomeKind.Moving)
                        {
                            tracked.Kind = tracked.Actual.Equals(tracked.Planned)
                                ? MoveTargetOutcomeKind.Exact
                                : MoveTargetOutcomeKind.Interrupted;
                        }
                        else if (tracked.Kind == MoveTargetOutcomeKind.Exact ||
                            tracked.Kind == MoveTargetOutcomeKind.SettledElsewhere)
                        {
                            tracked.Kind = tracked.Actual.Equals(tracked.Planned)
                                ? MoveTargetOutcomeKind.Exact
                                : MoveTargetOutcomeKind.SettledElsewhere;
                        }
                    }
                    else
                    {
                        tracked.Kind = MoveTargetOutcomeKind.Lost;
                    }
                }
            }
            else
            {
                foreach (TrackedMoveUnit tracked in group.Units)
                {
                    if (tracked.Kind == MoveTargetOutcomeKind.Lost ||
                        !TryGetMatchingLivingUnit(tracked, unitSpan, out int spanIndex))
                    {
                        tracked.Kind = MoveTargetOutcomeKind.Lost;
                        continue;
                    }
                    ref GameUnit unit = ref unitSpan[spanIndex];
                    tracked.Actual = new MoveTargetCoordinate(
                        unit.r_CurrentTilePositionX,
                        unit.r_CurrentTilePositionY);
                    bool targetChanged =
                        unit.r_AttackMoveToTargetTileX != tracked.Planned.X ||
                        unit.r_AttackMoveToTargetTileY != tracked.Planned.Y;
                    tracked.Kind = targetChanged
                        ? MoveTargetOutcomeKind.Interrupted
                        : tracked.Actual.Equals(tracked.Planned)
                            ? MoveTargetOutcomeKind.Exact
                            : MoveTargetOutcomeKind.SettledElsewhere;
                }
            }

            MoveTargetOutcome[] outcomes = group.Units.Select(unit => new MoveTargetOutcome(
                unit.UnitId,
                unit.GlobalId,
                unit.Planned,
                unit.Actual,
                unit.Kind)).ToArray();
            MoveTargetComparisonSummary summary = LargeMoveTargetDiagnosticsModel.Compare(outcomes);
            bool hasDeviation = summary.Deviated != 0 || summary.Reassigned != 0 ||
                summary.Interrupted != 0 || summary.Lost != 0;
            string examples = hasDeviation && summary.Examples.Count != 0
                ? $", examples={string.Join("|", summary.Examples)}"
                : string.Empty;
            string markerMode = group.MarkerReplacementAtStart && MarkerReplacementAvailable
                ? "native-full"
                : "vanilla-fallback";
            RemoveGroupMarkers(group);
            groups.Remove(group.TribeId);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MOVE_TARGET_RESULT: tribe={group.TribeId}, source={group.Source}, reason={reason}, " +
                $"units={summary.Total}, spacing={group.SpacingSummary}, " +
                $"plannedUnique={summary.PlannedUnique}, actualUnique={summary.ActualUnique}, " +
                $"exact={summary.Exact}, collectiveOnly={summary.Reassigned}, " +
                $"deviated={summary.Deviated}, interrupted={summary.Interrupted}, " +
                $"lost={summary.Lost}, maxDistance={summary.MaximumManhattan}/{summary.MaximumChebyshev}, " +
                $"markers={markerMode}{examples}.");
        }

        private static int CountAssassinStructureTargets(List<TrackedMoveUnit> units)
        {
            if (units.Count == 0 || units.Any(
                unit => unit.UnitType != eChimps.CHIMP_TYPE_ARAB_ASSASIN))
                return 0;

            int count = 0;
            TilePropertyFlag mask = TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated;
            foreach (TrackedMoveUnit unit in units)
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(unit.Planned.X, unit.Planned.Y);
                if (GameTileManagerAPI.Instance.HasTilePropertyFlag(tileId, mask))
                    count++;
            }
            return count;
        }

        private static List<TrackedMoveUnit> CaptureTribeUnits(int tribeId)
        {
            var result = new List<TrackedMoveUnit>();
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive || unit.r_TribeId != tribeId)
                    continue;
                result.Add(new TrackedMoveUnit(
                    spanIndex + 1,
                    unit.r_GlobalId,
                    unit.r_UnitChimp,
                    new MoveTargetCoordinate(
                        unit.r_AttackMoveToTargetTileX,
                        unit.r_AttackMoveToTargetTileY),
                    new MoveTargetCoordinate(
                        unit.r_CurrentTilePositionX,
                        unit.r_CurrentTilePositionY)));
            }
            return result;
        }

        private static bool MayContainLargeGroup(int tribeId)
        {
            return GameTribeManagerAPI.Instance.IsValidId(tribeId) &&
                GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) &&
                tribe != null && tribe->r_AliveState == AliveState.IsAlive &&
                tribe->r_UnitsInGroup >= MoveFormationCommandSnapshotStore.MinimumTrackedUnits;
        }

        private static List<TrackedMoveUnit> CaptureSnapshotUnits(
            int tribeId,
            MoveFormationUnitIdentity[] identities)
        {
            var result = new List<TrackedMoveUnit>(identities.Length);
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int index = 0; index < identities.Length; index++)
            {
                MoveFormationUnitIdentity identity = identities[index];
                int spanIndex = identity.UnitId - 1;
                if ((uint)spanIndex >= (uint)units.Length)
                    continue;
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != AliveState.IsAlive ||
                    unit.r_TribeId != tribeId || unit.r_GlobalId != identity.GlobalId)
                    continue;
                result.Add(new TrackedMoveUnit(
                    identity.UnitId,
                    identity.GlobalId,
                    unit.r_UnitChimp,
                    new MoveTargetCoordinate(
                        unit.r_AttackMoveToTargetTileX,
                        unit.r_AttackMoveToTargetTileY),
                    new MoveTargetCoordinate(
                        unit.r_CurrentTilePositionX,
                        unit.r_CurrentTilePositionY)));
            }
            return result;
        }

        private static bool TryGetMatchingLivingUnit(
            TrackedMoveUnit tracked,
            Span<GameUnit> units,
            out int spanIndex)
        {
            spanIndex = tracked.UnitId - 1;
            return (uint)spanIndex < (uint)units.Length &&
                units[spanIndex].r_AliveState == AliveState.IsAlive &&
                units[spanIndex].r_GlobalId == tracked.GlobalId;
        }

        private void InitializeActiveMarkerTiles(TrackedMoveGroup group)
        {
            foreach (TrackedMoveUnit unit in group.Units)
            {
                int targetTileId = GameTileManagerAPI.Instance.GetTileId(
                    unit.Planned.X, unit.Planned.Y);
                if (unit.Kind == MoveTargetOutcomeKind.Moving)
                    AdjustActiveMarkerCount(group, targetTileId, 1);
            }
        }

        private void AdjustActiveMarkerCount(
            TrackedMoveGroup group, int tileId, int delta)
        {
            if (delta > 0)
            {
                if (!group.ActiveMarkerTiles.Add(tileId))
                    return;
            }
            else if (!group.ActiveMarkerTiles.Remove(tileId))
            {
                return;
            }

            int globalCount = LargeMoveTargetDiagnosticsModel.ApplyMarkerReferenceDelta(
                activeMarkerCounts, tileId, delta);
            if (globalCount == 0)
            {
                renderer.RemoveMarkerTile(tileId);
            }
            else
            {
                if (delta > 0 && globalCount == 1)
                    renderer.AddMarkerTile(tileId);
            }
        }

        private void RemoveGroupMarkers(TrackedMoveGroup group)
        {
            foreach (int tileId in group.ActiveMarkerTiles)
            {
                int globalCount = LargeMoveTargetDiagnosticsModel.ApplyMarkerReferenceDelta(
                    activeMarkerCounts, tileId, -1);
                if (globalCount == 0)
                {
                    renderer.RemoveMarkerTile(tileId);
                }
            }
            group.ActiveMarkerTiles.Clear();
        }

        private sealed class TrackedMoveGroup
        {
            public TrackedMoveGroup(
                int tribeId,
                string source,
                List<TrackedMoveUnit> units,
                string spacingSummary,
                bool markerReplacementAtStart)
            {
                TribeId = tribeId;
                Source = source;
                Units = units;
                SpacingSummary = spacingSummary;
                MarkerReplacementAtStart = markerReplacementAtStart;
            }

            public int TribeId { get; }
            public string Source { get; }
            public List<TrackedMoveUnit> Units { get; }
            public string SpacingSummary { get; }
            public bool MarkerReplacementAtStart { get; }
            public HashSet<int> ActiveMarkerTiles { get; } = new HashSet<int>();
            public int EmptyOverlayPasses { get; set; }
        }

        private sealed class TrackedMoveUnit
        {
            public TrackedMoveUnit(
                int unitId,
                uint globalId,
                eChimps unitType,
                MoveTargetCoordinate planned,
                MoveTargetCoordinate actual)
            {
                UnitId = unitId;
                GlobalId = globalId;
                UnitType = unitType;
                Planned = planned;
                Actual = actual;
                Kind = planned.Equals(actual)
                    ? MoveTargetOutcomeKind.Exact
                    : MoveTargetOutcomeKind.Moving;
            }

            public int UnitId { get; }
            public uint GlobalId { get; }
            public eChimps UnitType { get; }
            public MoveTargetCoordinate Planned { get; }
            public MoveTargetCoordinate Actual { get; set; }
            public MoveTargetOutcomeKind Kind { get; set; }
        }
    }
}
