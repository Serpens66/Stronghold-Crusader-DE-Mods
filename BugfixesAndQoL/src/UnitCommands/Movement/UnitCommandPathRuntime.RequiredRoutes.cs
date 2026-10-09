using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {




        internal bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary)
        {
            summary = default;
            return TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                plan, exactTarget: plan != null && plan.ExactRouteEndpoints, allowReservedTarget: false, out summary);
        }

        internal bool TryFindRequiredFriendlyCompletedMoatRouteForPlan(
            PlanScope plan,
            bool exactTarget,
            bool allowReservedTarget,
            out RouteProbeSummary summary,
            bool evaluateMissing = true)
        {
            summary = default;
            if (nativeManualProbe) return false;
            if (!TraversalEnabled) return TryQualifyNativeMoatStart(plan, out summary);
            if (RequiredOnlyMode) return TryFindFastRequiredRoute(plan, allowReservedTarget, evaluateMissing, out summary);
            if (plan == null || plan.TargetX < 0 || plan.TargetX >= MapWidth ||
                plan.TargetY < 0 || plan.TargetY >= MapWidth ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) ||
                unit == null)
            {
                return false;
            }

            if (!CanDigMoat(unit))
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
                targetedRouteProbeBusy || weightedShadowBusy)
            {
                return false;
            }
            plan.PlayerId = playerId;
            PrepareMovementSearch(plan, playerId);

            if (!RequiredOnlyMode && plan.MoatWorkMovement && evaluateMissing && !plan.ExactRouteEndpoints &&
                (plan.RouteStartX < 0 || (plan.MoatWorkSearch != null &&
                 plan.MoatWorkSearch.StartX == startX && plan.MoatWorkSearch.StartY == startY)))
            {
                if (TryGetMoatWorkRoute(plan.MoatWorkSearch, plan.TargetX, plan.TargetY, out summary)) return true;
                // The native consumer can choose a terminal contact endpoint beyond the work tile.
                // Qualify that exact endpoint below instead of losing the work contract.
            }

            int startRegion = pathRegionGrid[startTileId];
            int targetRegion = pathRegionGrid[targetTileId];
            // Required-only deliberately proves ground reachability before doing any moat work.
            // Exact mode retains the per-unit movement profile and its weighted route choice.
            bool requiredOnly = RequiredOnlyMode;
            WeightedMovementCostProfile routeCost = default;
            bool hasCost = !requiredOnly && TryCaptureWeightedMovementCostProfile(
                unit, out routeCost, out _);
            if (!hasCost) routeCost = default;
            var cacheKey = new RouteDecisionKey(mapEpoch, CaptureCurrentGameTick(), playerId, startTileId,
                targetTileId, allowReservedTarget, plan.MoatWorkTargetTileId, placementRevision, routeCost);
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

            Stopwatch stopwatch = Stopwatch.StartNew();
            WeightedMoatRouteSummary ground = default;
            WeightedMoatRouteSummary friendly = default;
            long nodesBefore = weightedMoatRoutePlanner.SearchNodes;
            long runsBefore = weightedMoatRoutePlanner.SearchRuns;
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
                long groundStarted = Stopwatch.GetTimestamp();
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
                long exactNodesBefore = weightedMoatRoutePlanner.SearchNodes;
                long exactFieldHitsBefore = weightedMoatRoutePlanner.CachedFieldHits;
                groundReachable = groundDecision == GroundConnectionDecision.Reachable ||
                    (exactGroundSearch && weightedMoatRoutePlanner.TryProbeReachability(
                        playerId, startX, startY, plan.TargetX, plan.TargetY,
                        allowReservedTarget, MoatTraversalPolicy.GroundOnly, out ground));
                long groundElapsed = Stopwatch.GetTimestamp() - groundStarted;
                if (requiredMetrics != null)
                {
                    requiredMetrics.GroundChecks++;
                    if (groundReachable) requiredMetrics.GroundHits++;
                    if (samePclProof) requiredMetrics.SamePclHits++;
                    if (groundDecision == GroundConnectionDecision.Excluded &&
                        !IsCompletedMoatTile(startTileId) && !IsCompletedMoatTile(targetTileId))
                        requiredMetrics.TopologyExclusions++;
                    if (exactGroundSearch)
                    {
                        requiredMetrics.ExactGroundSearches++;
                        requiredMetrics.ExactGroundNodes += (int)Math.Min(
                            int.MaxValue, Math.Max(
                                0, weightedMoatRoutePlanner.SearchNodes - exactNodesBefore));
                        requiredMetrics.ExactGroundFieldCacheHits += (int)Math.Max(
                            0, weightedMoatRoutePlanner.CachedFieldHits - exactFieldHitsBefore);
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
                    long requiredSearchStarted = Stopwatch.GetTimestamp();
                    if (requiredMetrics != null) requiredMetrics.Searches++;
                    WeightedMoatEncodedRoute encoded = default;
                    if (requiredOnly) fastSearches++;
                    long fastSearchStarted = requiredOnly ? Stopwatch.GetTimestamp() : 0;
                    long fastNodesBefore = requiredOnly ? weightedMoatRoutePlanner.SearchNodes : 0;
                    friendlyReachable = requiredOnly
                        ? weightedMoatRoutePlanner.TryBuildReachabilityEncoded(playerId, startX, startY,
                            plan.TargetX, plan.TargetY, allowReservedTarget, out friendly, out encoded,
                            FastSearchNodeBudget)
                        : hasCost
                            ? weightedMoatRoutePlanner.TryBuildEncoded(playerId, startX, startY,
                                plan.TargetX, plan.TargetY, routeCost, allowReservedTarget,
                                out friendly, out encoded)
                            : weightedMoatRoutePlanner.TryBuildReachabilityEncoded(playerId, startX, startY,
                                plan.TargetX, plan.TargetY, allowReservedTarget, out friendly, out encoded);
                    if (requiredOnly)
                        RecordFastSearch(friendly, fastSearchStarted, fastNodesBefore);
                    if (friendlyReachable)
                        plan.QualifiedRoute = new QualifiedMovementRoute(startX, startY, plan.TargetX, plan.TargetY,
                            playerId, mapEpoch, CaptureCurrentGameTick(), placementRevision, encoded, friendly, routeCost, hasCost);
                    // Reachability is not limited by the native output buffer.
                    if (!friendlyReachable && !requiredOnly)
                        friendlyReachable = weightedMoatRoutePlanner.TryProbeReachability(playerId, startX, startY,
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
                        long searchElapsed = Stopwatch.GetTimestamp() - requiredSearchStarted;
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
                TargetedExpandedNodes = (int)Math.Min(int.MaxValue, weightedMoatRoutePlanner.SearchNodes - nodesBefore),
                TargetedSearchMilliseconds = stopwatch.Elapsed.TotalMilliseconds
            };
            if (command != null)
            {
                command.TargetedRouteSearches++;
                command.TargetedRouteSearchPasses += (int)(weightedMoatRoutePlanner.SearchRuns - runsBefore);
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

        internal bool TryGetCachedRequiredFriendlyRouteForPlan(
            PlanScope plan, out RouteProbeSummary summary) =>
            TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                plan, exactTarget: false, allowReservedTarget: false,
                out summary, evaluateMissing: false);

        internal bool TryQualifyMoveCommandFloodBypass(MoveCommandScope command)
        {
            EnsureMoveCommandGroupSummary(command);
            if (command == null || command.DiggersAtDispatch == 0)
                return false;
            if (command.FloodOwnerRouteEvaluated)
                return command.FloodOwnerRouteAllowed;

            command.FloodOwnerRouteEvaluated = true;
            DirectCursorMoveScope direct = activeDirectCursorMove;
            if (direct != null && direct.MapEpoch == mapEpoch &&
                direct.TribeId == command.TribeId && direct.TargetX == command.TargetX &&
                direct.TargetY == command.TargetY)
            {
                command.FloodOwnerRouteAllowed = true;
                command.MoatRelevant = true;
                return true;
            }

            var probedSources = new HashSet<string>(StringComparer.Ordinal);
            foreach (int unitId in command.ActiveUnitIdsAtDispatch)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != command.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }
                int startTileId = GameTileManagerAPI.Instance.GetTileId(
                    unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                if (!IsValidTileId(startTileId))
                    continue;
                int startRegion = pathRegionGrid[startTileId];
                string sourceKey = startRegion > 0 && !IsCompletedMoatTile(startTileId) &&
                    (tileFlags[startTileId] & CursorSpecialStructureTileFlagMask) == 0
                        ? $"r:{unit->r_ControllableForPlayerId}:{startRegion}"
                        : $"t:{unit->r_ControllableForPlayerId}:{startTileId}";
                if (!probedSources.Add(sourceKey))
                    continue;

                var plan = new PlanScope(unitId, command.TargetX, command.TargetY)
                {
                    VanillaFailureProven = RequiredOnlyMode
                };
                if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary route))
                {
                    continue;
                }
                command.FloodOwnerRouteAllowed = true;
                command.MoatRelevant = true;
                MarkCommandMoatRelevant(command, route);
                return true;
            }
            return false;
        }

        internal bool TryFindRequiredFriendlyCompletedMoatRouteToEndpoint(
            PlanScope plan,
            int endpointTileId,
            bool requireBuildingReservation,
            out RouteProbeSummary summary,
            out int distance)
        {
            distance = int.MaxValue;
            if (TryFindRequiredFriendlyCompletedMoatRouteForPlan(
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
                    (nativeMovementMasks[neighbourTileId] & EndpointSourceEdgeMasks[index]) == 0)
                {
                    continue;
                }

                PlanScope neighbourPlan = new PlanScope(plan.UnitId, neighbourX, neighbourY);
                if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        neighbourPlan, exactTarget: true,
                        allowReservedTarget: false,
                        out RouteProbeSummary neighbourSummary))
                {
                    observed.MergeObservations(neighbourSummary);
                    continue;
                }

                neighbourSummary.TargetRegion = pathRegionGrid[endpointTileId];
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
                    if (movementTargetAvailability[candidateCell] == 0)
                        continue;

                    int candidateTileId = GameTileManagerAPI.Instance.GetTileId(candidateX, candidateY);
                    if (!IsValidTileId(candidateTileId) ||
                        (tileFlags[candidateTileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (tileFlags[candidateTileId] & CursorSpecialStructureTileFlagMask) != 0)
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

        internal sealed class RequiredRouteMetrics
        {
            internal const int MaximumTrackedUnits = 8;
            internal readonly HashSet<int> trackedUnitIds = new HashSet<int>();
            public int GroundChecks, GroundHits, SamePclHits, TopologyExclusions;
            public int ExactGroundSearches, ExactGroundFieldCacheHits, Searches, Qualified, Published, Rejected;
            public int DecisionCacheHits, GroundDecisionCacheHits, ExactGroundNodes;
            public int TrackersStarted, TrackersSuppressed;
            public long GroundTicks, ExactGroundTicks, SearchTicks, PublicationTicks, PublicationAuditTicks;
            public long NestedGroundTicks, NestedSearchTicks;
            public Dictionary<string, int> RejectionReasons { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public double GroundMilliseconds => GroundTicks * 1000.0 / Stopwatch.Frequency;
            public double ExactGroundMilliseconds => ExactGroundTicks * 1000.0 / Stopwatch.Frequency;
            public double SearchMilliseconds => SearchTicks * 1000.0 / Stopwatch.Frequency;
            public double PublicationMilliseconds => PublicationTicks * 1000.0 / Stopwatch.Frequency;
            public double ExclusivePublicationMilliseconds =>
                Math.Max(0, PublicationTicks - PublicationAuditTicks) * 1000.0 / Stopwatch.Frequency;
            public double ExclusiveGroundMilliseconds =>
                Math.Max(0, GroundTicks - NestedGroundTicks) * 1000.0 / Stopwatch.Frequency;
            public double ExclusiveSearchMilliseconds =>
                Math.Max(0, SearchTicks - NestedSearchTicks) * 1000.0 / Stopwatch.Frequency;

            public void RecordDecisionCacheHit(TargetedRouteDecision decision)
            {
                DecisionCacheHits++;
                if (decision.Summary.ReachedWithoutMoat) GroundDecisionCacheHits++;
            }

            public void RecordNestedGroundTicks(long ticks)
            {
                if (attackQualificationTimingDepth > 0)
                    NestedGroundTicks += ticks;
            }

            public void RecordNestedSearchTicks(long ticks)
            {
                if (attackQualificationTimingDepth > 0)
                    NestedSearchTicks += ticks;
            }

            public bool TryTrackUnit(int unitId)
            {
                if (trackedUnitIds.Contains(unitId)) return true;
                if (trackedUnitIds.Count >= MaximumTrackedUnits)
                {
                    TrackersSuppressed++;
                    return false;
                }
                trackedUnitIds.Add(unitId);
                TrackersStarted++;
                return true;
            }
            public void Reject(string reason)
            {
                reason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
                RejectionReasons.TryGetValue(reason, out int count);
                RejectionReasons[reason] = count + 1;
                Rejected++;
            }
        }

        internal sealed class RequiredRouteCache
        {
            public Dictionary<RouteDecisionKey, TargetedRouteDecision> Decisions { get; } =
                new Dictionary<RouteDecisionKey, TargetedRouteDecision>();
        }

    }
}
