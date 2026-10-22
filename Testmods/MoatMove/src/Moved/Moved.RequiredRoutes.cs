extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.RequiredRoutes.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override bool TryFindRequiredFriendlyRouteTraversal(
            PlanScope plan,
            bool exactTarget,
            bool allowReservedTarget,
            out RouteProbeSummary summary,
            bool evaluateMissing)
        {
            summary = default;
            if (runtime.RequiredOnlyMode) return TryFindFastRequiredRoute(plan, allowReservedTarget, evaluateMissing, out summary);
            if (plan == null || plan.TargetX < 0 || plan.TargetX >= MapWidth ||
                plan.TargetY < 0 || plan.TargetY >= MapWidth ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                unit == null)
            {
                return false;
            }

            if (!runtime.CanDigMoat(unit))
                return false;

            int playerId = unit->r_ControllableForPlayerId;
            if (plan.IdentityBound && (plan.UnitGlobalId != unit->r_GlobalId || plan.PlayerId != playerId)) return false;
            plan.UnitGlobalId = unit->r_GlobalId;
            plan.IdentityBound = true;
            GetNativeMovementStart(unit, out int nativeStartX, out int nativeStartY);
            int startX = plan.RouteStartX >= 0 ? plan.RouteStartX : nativeStartX;
            int startY = plan.RouteStartY >= 0 ? plan.RouteStartY : nativeStartY;
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !IsValidTileId(startTileId) || !IsValidTileId(targetTileId) ||
                targetedRouteProbeBusy || runtime.weightedShadowBusy)
            {
                return false;
            }
            plan.PlayerId = playerId;
            runtime.PrepareMovementSearch(plan, playerId);

            if (!runtime.RequiredOnlyMode && plan.MoatWorkMovement && evaluateMissing && !plan.ExactRouteEndpoints &&
                (plan.RouteStartX < 0 || (plan.MoatWorkSearch != null &&
                 plan.MoatWorkSearch.StartX == startX && plan.MoatWorkSearch.StartY == startY)))
            {
                if (runtime.TryGetMoatWorkRoute(plan.MoatWorkSearch, plan.TargetX, plan.TargetY, out summary)) return true;
                // The native consumer can choose a terminal contact endpoint beyond the work tile.
                // Qualify that exact endpoint below instead of losing the work contract.
            }

            int startRegion = runtime.pathRegionGrid[startTileId];
            int targetRegion = runtime.pathRegionGrid[targetTileId];
            // Required-only deliberately proves ground reachability before doing any moat work.
            // Exact mode retains the per-unit movement profile and its weighted route choice.
            bool requiredOnly = runtime.RequiredOnlyMode;
            WeightedMovementCostProfile routeCost = default;
            bool hasCost = !requiredOnly && runtime.TryCaptureWeightedMovementCostProfile(
                unit, out routeCost, out _);
            if (!hasCost) routeCost = default;
            var cacheKey = new RouteDecisionKey(runtime.mapEpoch, CaptureCurrentGameTick(), playerId, startTileId,
                targetTileId, allowReservedTarget, plan.MoatWorkTargetTileId, runtime.placementRevision, routeCost);
            MoveCommandScope command = activeMoveCommand;
            RequiredRouteCache requiredCache = requiredOnly
                ? command?.RequiredCache ?? activeAttackCommand?.RequiredCache
                : command?.RequiredCache;
            if (requiredCache != null && requiredCache.Decisions.TryGetValue(
                    cacheKey, out TargetedRouteDecision cached))
            {
                if (command != null) command.TargetedRouteCacheHits++;
                if (requiredOnly) (command?.Required ?? activeAttackCommand?.Required)?.RecordDecisionCacheHit(cached);
                summary = cached.Summary;
                plan.QualifiedRoute = cached.Route;
                return cached.RequiredFriendlyMoat;
            }

            if (!evaluateMissing)
                return false;

            plan.QualifiedRoute = null;

            Stopwatch stopwatch = DetailedDiagnosticsEnabled ? Stopwatch.StartNew() : null;
            WeightedMoatRouteSummary ground = default;
            WeightedMoatRouteSummary friendly = default;
            long nodesBefore = runtime.weightedMoatRoutePlanner.SearchNodes;
            long runsBefore = runtime.weightedMoatRoutePlanner.SearchRuns;
            bool groundReachable;
            bool friendlyReachable = false;
            bool fastBridgeProven = false;
            GroundConnectionDecision groundDecision = GroundConnectionDecision.Unknown;
            RequiredRouteMetrics requiredMetrics = requiredOnly
                ? activeMoveCommand?.Required ?? activeAttackCommand?.Required
                : null;
            targetedRouteProbeBusy = true;
            try
            {
                long groundStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                bool samePclProof = requiredOnly && !plan.VanillaFailureProven &&
                    IsSamePositiveGroundRegion(startTileId, targetTileId);
                groundDecision = requiredOnly && plan.VanillaFailureProven
                    ? GroundConnectionDecision.Excluded
                    : samePclProof
                        ? GroundConnectionDecision.Reachable
                        : ProbeGroundConnection(playerId, startTileId, targetTileId);
                // Fast mode is deliberately fail-closed. An unknown topology result must
                // never turn a routine AI order into one or two full-map searches.
                bool exactGroundSearch = !requiredOnly &&
                    groundDecision == GroundConnectionDecision.Unknown;
                long exactNodesBefore = runtime.weightedMoatRoutePlanner.SearchNodes;
                long exactFieldHitsBefore = runtime.weightedMoatRoutePlanner.CachedFieldHits;
                groundReachable = groundDecision == GroundConnectionDecision.Reachable ||
                    (exactGroundSearch && runtime.weightedMoatRoutePlanner.TryProbeReachability(
                        playerId, startX, startY, plan.TargetX, plan.TargetY,
                        allowReservedTarget, MoatTraversalPolicy.GroundOnly, out ground));
                long groundElapsed = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - groundStarted;
                if (requiredMetrics != null)
                {
                    requiredMetrics.GroundChecks++;
                    if (groundReachable) requiredMetrics.GroundHits++;
                    if (samePclProof) requiredMetrics.SamePclHits++;
                    if (groundDecision == GroundConnectionDecision.Excluded &&
                        !runtime.IsCompletedMoatTile(startTileId) && !runtime.IsCompletedMoatTile(targetTileId))
                        requiredMetrics.TopologyExclusions++;
                    if (exactGroundSearch)
                    {
                        requiredMetrics.ExactGroundSearches++;
                        requiredMetrics.ExactGroundNodes += (int)Math.Min(
                            int.MaxValue, Math.Max(
                                0, runtime.weightedMoatRoutePlanner.SearchNodes - exactNodesBefore));
                        requiredMetrics.ExactGroundFieldCacheHits += (int)Math.Max(
                            0, runtime.weightedMoatRoutePlanner.CachedFieldHits - exactFieldHitsBefore);
                        requiredMetrics.ExactGroundTicks += groundElapsed;
                    }
                    requiredMetrics.GroundTicks += groundElapsed;
                    requiredMetrics.RecordNestedGroundTicks(groundElapsed);
                }
                bool groundSeparationProven =
                    groundDecision == GroundConnectionDecision.Excluded ||
                    requiredOnly && plan.VanillaFailureProven;
                fastBridgeProven = !requiredOnly || groundSeparationProven &&
                    HasFastFriendlyMoatBridge(playerId, startTileId, targetTileId);
                if (!groundReachable && (!requiredOnly || fastBridgeProven))
                {
                    long requiredSearchStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                    if (requiredMetrics != null) requiredMetrics.Searches++;
                    WeightedMoatEncodedRoute encoded = default;
                    if (requiredOnly) fastSearches++;
                    long fastSearchStarted = requiredOnly ? (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) : 0;
                    long fastNodesBefore = requiredOnly ? runtime.weightedMoatRoutePlanner.SearchNodes : 0;
                    friendlyReachable = requiredOnly
                        ? runtime.weightedMoatRoutePlanner.TryBuildReachabilityEncoded(playerId, startX, startY,
                            plan.TargetX, plan.TargetY, allowReservedTarget, out friendly, out encoded,
                            FastSearchNodeBudget)
                        : hasCost
                            ? runtime.weightedMoatRoutePlanner.TryBuildEncoded(playerId, startX, startY,
                                plan.TargetX, plan.TargetY, routeCost, allowReservedTarget,
                                out friendly, out encoded)
                            : runtime.weightedMoatRoutePlanner.TryBuildReachabilityEncoded(playerId, startX, startY,
                                plan.TargetX, plan.TargetY, allowReservedTarget, out friendly, out encoded);
                    if (requiredOnly)
                        RecordFastSearch(friendly, fastSearchStarted, fastNodesBefore);
                    if (friendlyReachable)
                        plan.QualifiedRoute = new QualifiedMovementRoute(startX, startY, plan.TargetX, plan.TargetY,
                            playerId, runtime.mapEpoch, CaptureCurrentGameTick(), runtime.placementRevision, encoded, friendly, routeCost, hasCost);
                    // Reachability is not limited by the native output buffer.
                    if (!friendlyReachable && !requiredOnly)
                        friendlyReachable = runtime.weightedMoatRoutePlanner.TryProbeReachability(playerId, startX, startY,
                            plan.TargetX, plan.TargetY, allowReservedTarget, MoatTraversalPolicy.FriendlyOnly, out friendly);
                    if (!friendlyReachable && !requiredOnly && plan.MoatWorkMovement &&
                        TryBuildTerminalFillRoute(plan, unit, startX, startY, out friendly, out WeightedMoatEncodedRoute terminal))
                    {
                        plan.QualifiedTerminalRoute = terminal;
                        plan.QualifiedTerminalSummary = friendly;
                        friendlyReachable = true;
                    }
                    if (requiredMetrics != null)
                    {
                        long searchElapsed = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - requiredSearchStarted;
                        requiredMetrics.SearchTicks += searchElapsed;
                        requiredMetrics.RecordNestedSearchTicks(searchElapsed);
                    }
                }
            }
            finally
            {
                targetedRouteProbeBusy = false;
            }

            bool requiredFriendly = !groundReachable && friendlyReachable &&
                friendly.MoatEdges > 0;
            if (requiredMetrics != null && requiredFriendly)
                requiredMetrics.Qualified++;
            if (requiredMetrics != null && !groundReachable && !requiredFriendly)
            {
                string reason = requiredOnly &&
                    groundDecision == GroundConnectionDecision.Unknown && !plan.VanillaFailureProven
                        ? "ground-unproven-fast"
                        : requiredOnly && !fastBridgeProven
                            ? "no-friendly-moat-bridge"
                        : friendlyReachable ? "no-moat-edge" :
                    friendly.Reason ?? "route-not-encodable";
                requiredMetrics.Reject(reason);
            }
            summary = new RouteProbeSummary(playerId)
            {
                StartRegion = startRegion,
                TargetRegion = targetRegion,
                RouteFound = requiredFriendly,
                AttackProbeEvaluated = true,
                ReachedWithoutMoat = groundReachable,
                ReachedWithMoat = friendlyReachable,
                FriendlyMoatTiles = friendly.MoatEdges,
                StructuralEdgesObserved = Math.Max(
                    ground.StructuralEdges, friendly.StructuralEdges),
                RouteDistance = friendlyReachable
                    ? (plan.QualifiedTerminalRoute.IsValid ? plan.QualifiedTerminalRoute.DirectionCount : friendly.RouteLength)
                    : groundReachable ? ground.RouteLength : int.MaxValue,
                TargetedExpandedNodes = (int)Math.Min(int.MaxValue, runtime.weightedMoatRoutePlanner.SearchNodes - nodesBefore),
                TargetedSearchMilliseconds = (DetailedDiagnosticsEnabled ? stopwatch.Elapsed.TotalMilliseconds : 0.0)
            };
            if (command != null)
            {
                command.TargetedRouteSearches++;
                command.TargetedRouteSearchPasses += (int)(runtime.weightedMoatRoutePlanner.SearchRuns - runsBefore);
                command.TargetedRouteExpandedNodes += summary.TargetedExpandedNodes;
                command.TargetedRouteSearchMilliseconds += summary.TargetedSearchMilliseconds;
                command.TargetedRouteMaximumSearchMilliseconds = Math.Max(
                    command.TargetedRouteMaximumSearchMilliseconds,
                    summary.TargetedSearchMilliseconds);
                requiredCache.Decisions[cacheKey] =
                    new TargetedRouteDecision(requiredFriendly, summary, plan.QualifiedRoute);
            }
            else if (requiredCache != null)
            {
                requiredCache.Decisions[cacheKey] =
                    new TargetedRouteDecision(requiredFriendly, summary, plan.QualifiedRoute);
            }
            summary.AttackProbeEvaluated = true;
            if (summary.RouteFound)
            {
                LogDiggerDecision("route", plan.UnitId, unit,
                    plan.TargetX, plan.TargetY, true, friendlyMoatRequired: true);
            }
            return summary.RouteFound;
        }

        internal bool TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
            PlanScope plan,
            int endpointTileId,
            bool requireBuildingReservation,
            out RouteProbeSummary summary,
            out int distance)
        {
            distance = int.MaxValue;
            if (runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                    plan, exactTarget: true,
                    allowReservedTarget: requireBuildingReservation, out summary))
            {
                distance = summary.RouteDistance;
                return true;
            }
            if (plan == null || !IsValidTileId(endpointTileId) ||
                (requireBuildingReservation &&
                 GameTileManagerAPI.Instance.GetTileBuildingId(endpointTileId) == 0))
            {
                return false;
            }

            // DA590 expands edges from the source tile. A Vanilla-published endpoint can have
            // no outgoing mask of its own (notably an occupied UnitFlood attack position); only
            // an owner-qualified neighbour's outgoing edge is required for the final step.
            RouteProbeSummary observed = summary;
            bool found = false;
            int bestDistance = int.MaxValue;
            RouteProbeSummary bestSummary = default;
            for (int index = 0; index < EndpointNeighbourX.Length; index++)
            {
                int neighbourX = plan.TargetX + EndpointNeighbourX[index];
                int neighbourY = plan.TargetY + EndpointNeighbourY[index];
                if (neighbourX < 0 || neighbourX >= MapWidth ||
                    neighbourY < 0 || neighbourY >= MapWidth)
                {
                    continue;
                }

                int neighbourTileId = GameTileManagerAPI.Instance.GetTileId(neighbourX, neighbourY);
                if (!IsValidTileId(neighbourTileId) ||
                    (runtime.nativeMovementMasks[neighbourTileId] & EndpointSourceEdgeMasks[index]) == 0)
                {
                    continue;
                }

                PlanScope neighbourPlan = new PlanScope(plan.UnitId, neighbourX, neighbourY);
                if (!runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        neighbourPlan, exactTarget: true,
                        allowReservedTarget: false,
                        out RouteProbeSummary neighbourSummary))
                {
                    observed.MergeObservations(neighbourSummary);
                    continue;
                }

                neighbourSummary.TargetRegion = runtime.pathRegionGrid[endpointTileId];
                neighbourSummary.RouteFound = true;
                neighbourSummary.ReachedWithMoat = true;
                neighbourSummary.ReachedWithoutMoat = false;
                observed.MergeObservations(neighbourSummary);
                int candidateDistance = neighbourSummary.RouteDistance == int.MaxValue
                    ? int.MaxValue
                    : neighbourSummary.RouteDistance + 1;
                if (candidateDistance < bestDistance)
                {
                    found = true;
                    bestDistance = candidateDistance;
                    bestSummary = neighbourSummary;
                }
            }

            summary = observed;
            if (!found)
                return false;
            summary.MergeObservations(bestSummary);
            distance = bestDistance;
            return true;
        }

        internal bool TryFindFriendlyCompletedMoatRouteToAttackApproach(
            AttackCursorPairScope scope,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            approachX = -1;
            approachY = -1;
            summary = default;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (scope == null || tileManager == IntPtr.Zero ||
                !playerApi.IsPlayerIdValid(scope.PlayerId))
            {
                return false;
            }

            RouteProbeSummary bestObserved = new RouteProbeSummary(scope.PlayerId);
            for (int yOffset = -1; yOffset <= 1; yOffset++)
            {
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    if (xOffset == 0 && yOffset == 0)
                        continue;

                    int candidateX = scope.TargetX + xOffset;
                    int candidateY = scope.TargetY + yOffset;
                    if (candidateX < 0 || candidateX >= MapWidth ||
                        candidateY < 0 || candidateY >= MapWidth)
                    {
                        continue;
                    }

                    int candidateCell = (candidateY * MapWidth) + candidateX;
                    if (runtime.movementTargetAvailability[candidateCell] == 0)
                        continue;

                    int candidateTileId = GameTileManagerAPI.Instance.GetTileId(candidateX, candidateY);
                    if (!IsValidTileId(candidateTileId) ||
                        (runtime.tileFlags[candidateTileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (runtime.tileFlags[candidateTileId] & CursorSpecialStructureTileFlagMask) != 0)
                    {
                        continue;
                    }

                    EnsureReachabilityMap(
                        scope.PlayerId,
                        scope.StartX,
                        scope.StartY, deferTraversal: true, owner: scope);
                    RouteProbeSummary candidateSummary =
                        GetCachedRouteSummaryForTarget(candidateX, candidateY);
                    bool reachedWithMoat = visitedWithMoat[candidateCell] == gridGeneration;
                    bool reachedWithoutMoat = visitedWithoutMoat[candidateCell] == gridGeneration;
                    candidateSummary.AttackProbeEvaluated = true;
                    candidateSummary.ReachedWithMoat = reachedWithMoat;
                    candidateSummary.ReachedWithoutMoat = reachedWithoutMoat;
                    candidateSummary.RouteFound = reachedWithMoat && !reachedWithoutMoat &&
                        candidateSummary.FriendlyMoatTiles > 0;
                    bestObserved.MergeObservations(candidateSummary);
                    if (!candidateSummary.RouteFound)
                        continue;

                    approachX = candidateX;
                    approachY = candidateY;
                    summary = candidateSummary;
                    return true;
                }
            }

            summary = bestObserved;
            return false;
        }
    }
}
