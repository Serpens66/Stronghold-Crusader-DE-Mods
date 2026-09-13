using System;
using System.Collections.Generic;
using SHCDESE.API;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private long fastVanillaBypasses;
        private FastRouteField fastCandidateField;
        private MoatSearchEdge fastCandidateEdge;
        private bool fastCandidateBusy;
        private long fastMaximumSynchronousTicks, fastMaximumQueueWaitTicks;

        private static long StartFastMeasurement() => System.Diagnostics.Stopwatch.GetTimestamp();
        private void FinishFastMeasurement(long started)
        { fastMaximumSynchronousTicks = Math.Max(fastMaximumSynchronousTicks, System.Diagnostics.Stopwatch.GetTimestamp() - started); }

        private int[] ResolveMovementCandidates(MoatCandidateField precise, IList<int> starts,
            IList<int> targets, MoatSearchEdge edge, MoatSearchEdge terminal, out int expanded)
        {
            if (!RequiredOnlyMode)
            { var result = precise.Resolve(starts, targets, edge, terminal); expanded = precise.Expanded; return result; }
            if (fastCandidateBusy) throw new InvalidOperationException("Nested Fast candidate field lease.");
            fastCandidateBusy = true;
            try
            {
                fastCandidateEdge = edge;
                if (fastCandidateField == null)
                    fastCandidateField = new FastRouteField(MapWidth, MapWidth,
                        (int a, int b, int d, out bool wet, out bool structure) =>
                            fastCandidateEdge(b, a, (d + 4) & 7, out wet, out structure));
                fastCandidateField.ResetRoots(starts);
                var result = new int[targets.Count];
                for (int i = 0; i < targets.Count; i++)
                {
                    int target = targets[i], best = int.MaxValue;
                    if ((uint)target < MapWidth * MapWidth)
                    {
                        if (fastCandidateField.Advance(target, int.MaxValue) == FastRouteStatus.Found)
                            best = fastCandidateField.Distance(target);
                        for (int d = 0; d < 8; d++)
                        {
                            int x = target % MapWidth - WeightedMoatRoutePlanner.DirectionX[d];
                            int y = target / MapWidth - WeightedMoatRoutePlanner.DirectionY[d];
                            if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                            int from = y * MapWidth + x;
                            if (terminal(from, target, d, out _, out _) &&
                                fastCandidateField.Advance(from, int.MaxValue, 1999) == FastRouteStatus.Found)
                                best = Math.Min(best, fastCandidateField.Distance(from) + 1);
                        }
                    }
                    result[i] = best <= 2000 ? best : -1;
                }
                expanded = fastCandidateField.Expanded; fastNewExpanded += expanded;
                return result;
            }
            finally { fastCandidateEdge = null; fastCandidateBusy = false; }
        }

        private bool TryProbeFastCursorRoute(int playerId, int startTileId, int targetTileId,
            out RouteProbeSummary summary)
        {
            summary = new RouteProbeSummary(playerId);
            if (!IsValidTileId(startTileId) || !IsValidTileId(targetTileId)) return false;
            var a = GameTileManagerAPI.Instance.GetTileVectorFromId(startTileId);
            var b = GameTileManagerAPI.Instance.GetTileVectorFromId(targetTileId);
            int start = a.Y * MapWidth + a.X, target = b.Y * MapWidth + b.X;
            FastRoutingState state = GetFastRouting(true); RefreshFastRouting(state);
            using (var ground = state.Pool.Acquire(new FastFieldKey(playerId, target, true)))
            using (var friendly = state.Pool.Acquire(new FastFieldKey(playerId, target, false)))
            {
                // Separate UI state cannot consume simulation budgets or seed its fields.
                FastRouteStatus status = ground?.Field.Advance(start, 8192, int.MaxValue) ?? FastRouteStatus.Pending;
                summary.StartRegion = pathRegionGrid[startTileId]; summary.TargetRegion = pathRegionGrid[targetTileId];
                if (status == FastRouteStatus.Pending) return false;
                summary.ReachedWithoutMoat = status == FastRouteStatus.Found;
                if (summary.ReachedWithoutMoat)
                { summary.AttackProbeEvaluated = summary.RouteFound = true; summary.RouteDistance = ground.Field.Distance(start); return true; }
                status = friendly?.Field.Advance(start, 8192) ?? FastRouteStatus.Pending;
                summary.AttackProbeEvaluated = status != FastRouteStatus.Pending;
                summary.RouteFound = summary.ReachedWithMoat = status == FastRouteStatus.Found;
                summary.RouteDistance = summary.RouteFound ? friendly.Field.Distance(start) : int.MaxValue;
                summary.FriendlyMoatTiles = summary.RouteFound ? 1 : 0;
                return summary.RouteFound;
            }
        }

        private void LogAndResetFastMoatMetrics()
        {
            if (fastNewQueries == 0 && fastQueuedCommands == 0 && fastExecutedCommands == 0 && fastCommands.Commands.Count == 0) return;
            Shared.DebugLogHelper.LogInfo(log, "MoatMove stage=fast-performance " +
                $"queued={fastQueuedCommands} completed={fastExecutedCommands} pending={fastCommands.Commands.Count} " +
                $"queries={fastNewQueries} expanded={fastNewExpanded} tooLong={fastNewTooLong} " +
                $"retries={fastCommandRetries} topologyChanges={fastNewInvalidations} " +
                $"maxWaitTicks={fastMaximumQueueWaitTicks} maxSynchronousMs={fastMaximumSynchronousTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency:F3} " +
                $"fields={fastSimulationRouting?.Pool.Builds ?? 0} reused={fastSimulationRouting?.Pool.Hits ?? 0} " +
                $"fieldBytes={(fastSimulationRouting?.Pool.BufferBytes ?? 0) + (fastCandidateField?.BufferBytes ?? 0)}.");
            fastNewQueries = fastNewExpanded = fastNewTooLong = fastNewInvalidations = 0;
            fastQueuedCommands = fastExecutedCommands = fastCommandRetries = fastVanillaBypasses = 0;
            fastMaximumSynchronousTicks = fastMaximumQueueWaitTicks = 0;
        }
    }
}
