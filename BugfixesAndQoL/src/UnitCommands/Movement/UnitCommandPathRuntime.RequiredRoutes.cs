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
            return Traversal.TryFindRequiredFriendlyRouteTraversal(plan, exactTarget, allowReservedTarget,
                out summary, evaluateMissing);
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
