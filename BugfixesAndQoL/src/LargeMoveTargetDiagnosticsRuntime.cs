using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace BugfixesAndQoL
{
    internal unsafe sealed class LargeMoveTargetDiagnosticsRuntime
    {
        private const int DrawListCountOffset = 0x622248;
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetMarkerRenderer renderer;
        private readonly Dictionary<int, TrackedMoveGroup> groups =
            new Dictionary<int, TrackedMoveGroup>();
        private readonly List<int> tribeIdBuffer = new List<int>();
        private int currentOverlayTribeId;
        private bool overlayPassActive;
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
                FinalizeGroup(previous, "command-replaced", forceInterrupt: true, tick);

            List<TrackedMoveUnit> units = CaptureTribeUnits(tribeId);
            if (!LargeMoveTargetDiagnosticsModel.ShouldTrack(units.Count))
            {
                groups.Remove(tribeId);
                RefreshRenderer();
                return;
            }

            var group = new TrackedMoveGroup(
                tribeId,
                commandX,
                commandY,
                tick,
                source,
                units);
            groups[tribeId] = group;
            RefreshActiveTiles(group);
            RefreshRenderer();

            List<MoveTargetCoordinate> targets = units.Select(unit => unit.Planned).ToList();
            int unique = targets.Distinct().Count();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MOVE_TARGET_TRACK_START: tribeId={tribeId}, source={source}, " +
                $"command={commandX},{commandY}, tick={tick}, units={units.Count}, " +
                $"plannedUnique={unique}, plannedDuplicates={targets.Count - unique}, " +
                $"plannedBounds={LargeMoveTargetDiagnosticsModel.Bounds(targets)}, " +
                $"plannedFingerprint={LargeMoveTargetDiagnosticsModel.Fingerprint(targets)}, " +
                $"vanillaSharedCapacity={LargeMoveTargetDiagnosticsModel.VanillaDrawCapacity}, " +
                $"replacementAvailable={MarkerReplacementAvailable}.");
        }

        public void OnTick(int tick)
        {
            if (!FeatureEnabled)
            {
                if (groups.Count != 0)
                    Reset(tick, "setting-disabled");
                return;
            }
            if (!trackingAvailable || groups.Count == 0)
                return;
            tribeIdBuffer.Clear();
            foreach (int tribeId in groups.Keys)
                tribeIdBuffer.Add(tribeId);

            bool rendererChanged = false;
            for (int groupIndex = 0; groupIndex < tribeIdBuffer.Count; groupIndex++)
            {
                int tribeId = tribeIdBuffer[groupIndex];
                if (!groups.TryGetValue(tribeId, out TrackedMoveGroup group))
                    continue;
                bool changed = false;
                int moving = 0;
                for (int index = 0; index < group.Units.Count; index++)
                {
                    TrackedMoveUnit tracked = group.Units[index];
                    if (tracked.Kind == MoveTargetOutcomeKind.Lost)
                        continue;
                    if (!TryGetMatchingLivingUnit(tracked, out GameUnit* unit))
                    {
                        tracked.Kind = MoveTargetOutcomeKind.Lost;
                        changed = true;
                        continue;
                    }

                    tracked.Actual = new MoveTargetCoordinate(
                        unit->r_CurrentTilePositionX,
                        unit->r_CurrentTilePositionY);
                    if (tracked.Kind == MoveTargetOutcomeKind.Interrupted)
                        continue;
                    if (unit->r_AttackMoveToTargetTileX != tracked.Planned.X ||
                        unit->r_AttackMoveToTargetTileY != tracked.Planned.Y)
                    {
                        tracked.Kind = MoveTargetOutcomeKind.Interrupted;
                        changed = true;
                        continue;
                    }
                    if (tracked.Actual.Equals(tracked.Planned))
                    {
                        if (tracked.Kind != MoveTargetOutcomeKind.Exact)
                        {
                            tracked.Kind = MoveTargetOutcomeKind.Exact;
                            changed = true;
                        }
                        tracked.StableIdleTicks = 0;
                        continue;
                    }

                    bool pathPending = unit->p_PathPlanSize != 0 &&
                        unit->p_CurrentPathPlanPosition < unit->p_PathPlanSize;
                    bool tileTransitionPending = unit->r_NextTilePositionX2 != unit->r_CurrentTilePositionX ||
                        unit->r_NextTilePositionY2 != unit->r_CurrentTilePositionY;
                    if (pathPending || tileTransitionPending)
                    {
                        tracked.StableIdleTicks = 0;
                        if (tracked.Kind != MoveTargetOutcomeKind.Moving)
                        {
                            tracked.Kind = MoveTargetOutcomeKind.Moving;
                            changed = true;
                        }
                        moving++;
                        continue;
                    }

                    tracked.StableIdleTicks++;
                    if (tracked.StableIdleTicks >= LargeMoveTargetDiagnosticsModel.RequiredStableIdleTicks)
                    {
                        if (tracked.Kind != MoveTargetOutcomeKind.SettledElsewhere)
                        {
                            tracked.Kind = MoveTargetOutcomeKind.SettledElsewhere;
                            changed = true;
                        }
                    }
                    else
                    {
                        moving++;
                    }
                }

                if (changed)
                {
                    RefreshActiveTiles(group);
                    rendererChanged = true;
                }
                if (moving == 0 && group.Units.All(unit => unit.Kind != MoveTargetOutcomeKind.Moving))
                {
                    group.StableCompletionTicks++;
                    if (group.StableCompletionTicks >= LargeMoveTargetDiagnosticsModel.RequiredStableIdleTicks)
                    {
                        FinalizeGroup(group, "completed", forceInterrupt: false, tick);
                        rendererChanged = true;
                    }
                }
                else
                    group.StableCompletionTicks = 0;
            }

            if (rendererChanged)
                RefreshRenderer();
        }

        public void Reset(int tick, string reason)
        {
            foreach (TrackedMoveGroup group in groups.Values.ToArray())
                FinalizeGroup(group, reason, forceInterrupt: true, tick);
            groups.Clear();
            overlayPassActive = false;
            currentOverlayTribeId = 0;
            RefreshRenderer();
        }

        public void Shutdown()
        {
            renderer.Shutdown();
        }

        public void ApplySetting(int tick)
        {
            if (!FeatureEnabled && groups.Count != 0)
                Reset(tick, "setting-disabled");
        }

        public void BeginOverlayPass(int tribeId)
        {
            currentOverlayTribeId = tribeId;
            overlayPassActive = groups.TryGetValue(tribeId, out TrackedMoveGroup group) &&
                !group.FirstOverlayCaptured;
            if (overlayPassActive)
            {
                group.CurrentOverlayAttempts = 0;
                group.CurrentOverlayFirstCount = -1;
                group.CurrentOverlayMaximumCount = -1;
            }
        }

        public void EndOverlayPass()
        {
            if (overlayPassActive && groups.TryGetValue(currentOverlayTribeId, out TrackedMoveGroup group) &&
                group.CurrentOverlayAttempts > 0)
            {
                group.FirstOverlayCaptured = true;
                group.FirstOverlayAttempts = group.CurrentOverlayAttempts;
                group.FirstOverlayOccupiedBefore = group.CurrentOverlayFirstCount;
                group.FirstOverlayMaximumObserved = group.CurrentOverlayMaximumCount;
            }
            overlayPassActive = false;
            currentOverlayTribeId = 0;
        }

        public bool ObserveAndShouldSuppressMarker(
            IntPtr drawManager,
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
                !groups.TryGetValue(currentOverlayTribeId, out TrackedMoveGroup group) ||
                !group.ActiveTargetTiles.Contains(tileId))
            {
                return false;
            }

            if (overlayPassActive)
            {
                int count = drawManager == IntPtr.Zero
                    ? -1
                    : System.Runtime.InteropServices.Marshal.ReadInt32(drawManager, DrawListCountOffset);
                group.CurrentOverlayAttempts++;
                if (group.CurrentOverlayFirstCount < 0)
                    group.CurrentOverlayFirstCount = count;
                group.CurrentOverlayMaximumCount = Math.Max(group.CurrentOverlayMaximumCount, count);
            }
            return MarkerReplacementAvailable;
        }

        private void FinalizeGroup(
            TrackedMoveGroup group,
            string reason,
            bool forceInterrupt,
            int tick)
        {
            if (forceInterrupt)
            {
                foreach (TrackedMoveUnit tracked in group.Units)
                {
                    if (TryGetMatchingLivingUnit(tracked, out GameUnit* unit))
                    {
                        tracked.Actual = new MoveTargetCoordinate(
                            unit->r_CurrentTilePositionX,
                            unit->r_CurrentTilePositionY);
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
                        !TryGetMatchingLivingUnit(tracked, out GameUnit* unit))
                    {
                        tracked.Kind = MoveTargetOutcomeKind.Lost;
                        continue;
                    }
                    tracked.Actual = new MoveTargetCoordinate(
                        unit->r_CurrentTilePositionX,
                        unit->r_CurrentTilePositionY);
                    if (tracked.Kind != MoveTargetOutcomeKind.Interrupted)
                    {
                        tracked.Kind = tracked.Actual.Equals(tracked.Planned)
                            ? MoveTargetOutcomeKind.Exact
                            : MoveTargetOutcomeKind.SettledElsewhere;
                    }
                }
            }

            MoveTargetOutcome[] outcomes = group.Units.Select(unit => new MoveTargetOutcome(
                unit.UnitId,
                unit.GlobalId,
                unit.Planned,
                unit.Actual,
                unit.Kind)).ToArray();
            MoveTargetComparisonSummary summary = LargeMoveTargetDiagnosticsModel.Compare(outcomes);
            string examples = summary.Examples.Count == 0
                ? "none"
                : string.Join("|", summary.Examples);
            bool multisetEqual = summary.PlannedFingerprint == summary.ActualFingerprint &&
                summary.Total - summary.Lost == summary.Total;
            int vanillaSlotsAvailable = group.FirstOverlayOccupiedBefore < 0
                ? -1
                : Math.Max(0,
                    LargeMoveTargetDiagnosticsModel.VanillaDrawCapacity - group.FirstOverlayOccupiedBefore);
            int predictedVanillaAccepted = vanillaSlotsAvailable < 0
                ? -1
                : Math.Min(group.FirstOverlayAttempts, vanillaSlotsAvailable);
            int predictedVanillaDropped = predictedVanillaAccepted < 0
                ? -1
                : Math.Max(0, group.FirstOverlayAttempts - predictedVanillaAccepted);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MOVE_TARGET_TRACK_RESULT: tribeId={group.TribeId}, source={group.Source}, " +
                $"reason={reason}, startTick={group.StartTick}, endTick={tick}, elapsedTicks={tick - group.StartTick}, " +
                $"units={summary.Total}, exact={summary.Exact}, reassigned={summary.Reassigned}, " +
                $"settledElsewhere={summary.SettledElsewhere}, interrupted={summary.Interrupted}, " +
                $"lost={summary.Lost}, moving={summary.Moving}, collectiveMatches={summary.CollectiveMatches}, " +
                $"multisetEqual={multisetEqual}, plannedUnique={summary.PlannedUnique}, " +
                $"actualUnique={summary.ActualUnique}, plannedDuplicates={summary.PlannedDuplicates}, " +
                $"actualDuplicates={summary.ActualDuplicates}, plannedBounds={summary.PlannedBounds}, " +
                $"actualBounds={summary.ActualBounds}, avgManhattan={summary.AverageManhattan.ToString("F3", CultureInfo.InvariantCulture)}, " +
                $"maxManhattan={summary.MaximumManhattan}, avgChebyshev={summary.AverageChebyshev.ToString("F3", CultureInfo.InvariantCulture)}, " +
                $"maxChebyshev={summary.MaximumChebyshev}, plannedFingerprint={summary.PlannedFingerprint}, " +
                $"actualFingerprint={summary.ActualFingerprint}, firstOverlayAttempts={group.FirstOverlayAttempts}, " +
                $"firstOverlayOccupiedBefore={group.FirstOverlayOccupiedBefore}, " +
                $"firstOverlayMaximumObserved={group.FirstOverlayMaximumObserved}, " +
                $"predictedVanillaAccepted={predictedVanillaAccepted}, " +
                $"predictedVanillaDropped={predictedVanillaDropped}, " +
                $"vanillaSharedCapacity={LargeMoveTargetDiagnosticsModel.VanillaDrawCapacity}, examples={examples}.");
            groups.Remove(group.TribeId);
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
                    new MoveTargetCoordinate(
                        unit.r_AttackMoveToTargetTileX,
                        unit.r_AttackMoveToTargetTileY),
                    new MoveTargetCoordinate(
                        unit.r_CurrentTilePositionX,
                        unit.r_CurrentTilePositionY)));
            }
            return result;
        }

        private static bool TryGetMatchingLivingUnit(TrackedMoveUnit tracked, out GameUnit* unit)
        {
            unit = null;
            return GameUnitManagerAPI.Instance.IsValidId(tracked.UnitId) &&
                GameUnitManagerAPI.Instance.TryGetUnitById(tracked.UnitId, out unit) &&
                unit != null && unit->r_AliveState == AliveState.IsAlive &&
                unit->r_GlobalId == tracked.GlobalId;
        }

        private static void RefreshActiveTiles(TrackedMoveGroup group)
        {
            group.ActiveTargetTiles.Clear();
            foreach (TrackedMoveUnit unit in group.Units)
            {
                group.ActiveTargetTiles.Add(
                    GameTileManagerAPI.Instance.GetTileId(unit.Planned.X, unit.Planned.Y));
            }
        }

        private void RefreshRenderer()
        {
            var states = new Dictionary<MoveTargetCoordinate, bool>();
            foreach (TrackedMoveGroup group in groups.Values)
            {
                foreach (TrackedMoveUnit unit in group.Units)
                {
                    bool active = unit.Kind == MoveTargetOutcomeKind.Moving;
                    if (!states.TryGetValue(unit.Planned, out bool current) || (!current && active))
                        states[unit.Planned] = active;
                }
            }
            renderer.SetMarkers(states.Select(pair => new LargeMoveMarkerPoint(pair.Key, pair.Value)).ToArray());
        }

        private sealed class TrackedMoveGroup
        {
            public TrackedMoveGroup(
                int tribeId,
                int commandX,
                int commandY,
                int startTick,
                string source,
                List<TrackedMoveUnit> units)
            {
                TribeId = tribeId;
                CommandX = commandX;
                CommandY = commandY;
                StartTick = startTick;
                Source = source;
                Units = units;
            }

            public int TribeId { get; }
            public int CommandX { get; }
            public int CommandY { get; }
            public int StartTick { get; }
            public string Source { get; }
            public List<TrackedMoveUnit> Units { get; }
            public HashSet<int> ActiveTargetTiles { get; } = new HashSet<int>();
            public bool FirstOverlayCaptured { get; set; }
            public int FirstOverlayAttempts { get; set; }
            public int FirstOverlayOccupiedBefore { get; set; } = -1;
            public int FirstOverlayMaximumObserved { get; set; } = -1;
            public int CurrentOverlayAttempts { get; set; }
            public int CurrentOverlayFirstCount { get; set; } = -1;
            public int CurrentOverlayMaximumCount { get; set; } = -1;
            public int StableCompletionTicks { get; set; }
        }

        private sealed class TrackedMoveUnit
        {
            public TrackedMoveUnit(
                int unitId,
                uint globalId,
                MoveTargetCoordinate planned,
                MoveTargetCoordinate actual)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Planned = planned;
                Actual = actual;
                Kind = planned.Equals(actual)
                    ? MoveTargetOutcomeKind.Exact
                    : MoveTargetOutcomeKind.Moving;
            }

            public int UnitId { get; }
            public uint GlobalId { get; }
            public MoveTargetCoordinate Planned { get; }
            public MoveTargetCoordinate Actual { get; set; }
            public MoveTargetOutcomeKind Kind { get; set; }
            public int StableIdleTicks { get; set; }
        }
    }
}
