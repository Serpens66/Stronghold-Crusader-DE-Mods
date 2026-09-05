using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private readonly Dictionary<string, long> fillRouteDecisions = new Dictionary<string, long>();
        private int fillRouteLogTick = -1, fillRouteLogCount;

        private BuilderWeightedScope RejectWeightedCapture(string reason)
        {
            string key = "capture-" + reason;
            fillRouteDecisions.TryGetValue(key, out long count);
            fillRouteDecisions[key] = count + 1;
            // Captures can run outside commands. Keep those failures observable too.
            int tick = CaptureCurrentGameTick();
            if (fillRouteLogTick < 0 || tick < fillRouteLogTick || tick - fillRouteLogTick >= 60)
            { fillRouteLogTick = tick; fillRouteLogCount = 0; }
            if (fillRouteLogCount++ < 3)
                Shared.DebugLogHelper.LogInfo(log, $"MoveMoat stage=route-capture-rejected reason={reason} total={count + 1}.");
            return null;
        }

        private bool TryGetTerminalFillContact(PlanScope plan, GameUnit* unit,
            int targetX, int targetY, out int workTile)
        {
            workTile = -1;
            if (plan == null || unit == null || !plan.MoatWorkMovement ||
                plan.TargetX != targetX || plan.TargetY != targetY ||
                unit->r_AliveState != AliveState.IsAlive || !CanDigMoat(unit) ||
                (TribeAICommand)unit->r_AI_LastIssuedTribeCommand != TribeAICommand.Unknown7 ||
                plan.PlayerId != unit->r_ControllableForPlayerId ||
                (plan.IdentityBound && plan.UnitGlobalId != unit->r_GlobalId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* live) || live != unit ||
                (plan.MoatWorkSearch != null && (plan.MoatWorkSearch.MapEpoch != mapEpoch ||
                    plan.MoatWorkSearch.CapturedTick != CaptureCurrentGameTick())) ||
                !IsCompletedEnemyMoatForPlayer(plan.PlayerId, plan.MoatWorkTargetTileId)) return false;
            var position = GameTileManagerAPI.Instance.GetTileVectorFromId(plan.MoatWorkTargetTileId);
            if (Math.Max(Math.Abs(targetX - position.X), Math.Abs(targetY - position.Y)) != 1) return false;
            workTile = plan.MoatWorkTargetTileId;
            return true;
        }

        private bool DescribeWeightedRoute(BuilderWeightedScope shadow, byte* path, int length,
            out WeightedMoatRouteSummary summary, bool comparisonOnly = false)
        {
            int contact = -1;
            if (shadow.FillPlan != null)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(shadow.UnitId, out GameUnit* unit) ||
                    unit == null || unit->r_GlobalId != shadow.UnitGlobalId ||
                    !TryGetTerminalFillContact(shadow.FillPlan, unit, shadow.TargetX, shadow.TargetY, out contact))
                { summary = WeightedMoatRouteSummary.Failed("fill-context-changed", 0); return false; }
            }
            return weightedMoatRoutePlanner.TryDescribeEncodedPath(shadow.PlayerId, shadow.StartX, shadow.StartY,
                shadow.TargetX, shadow.TargetY, shadow.CostProfile, path, length,
                shadow.AllowReservedTarget, out summary, contact, comparisonOnly);
        }

        private bool TryImproveFillPrefix(BuilderWeightedScope shadow, byte* nativePath, int nativeLength,
            MoatSearchLimit[] limits, out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            summary = default; route = default;
            if (shadow.FillPlan == null || nativeLength < 3 ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(shadow.UnitId, out GameUnit* unit) ||
                !TryGetTerminalFillContact(shadow.FillPlan, unit, shadow.TargetX, shadow.TargetY, out int contact)) return false;
            int last = nativeLength - 1, before = nativeLength - 2;
            int exit = (nativePath[last >> 1] >> ((last & 1) * 4)) & 15;
            int entry = (nativePath[before >> 1] >> ((before & 1) * 4)) & 15;
            if (entry >= 8 || exit >= 8) return false;
            int cx = shadow.TargetX - WeightedMoatRoutePlanner.DirectionX[exit];
            int cy = shadow.TargetY - WeightedMoatRoutePlanner.DirectionY[exit];
            if (GameTileManagerAPI.Instance.GetTileId(cx, cy) != contact) return false;
            int px = cx - WeightedMoatRoutePlanner.DirectionX[entry];
            int py = cy - WeightedMoatRoutePlanner.DirectionY[entry];
            // Both preserved edges touch completed moat. Subtract their fixed costs
            // from EVERY profile bound before searching the friendly prefix.
            var prefixLimits = new MoatSearchLimit[limits.Length];
            for (int i = 0; i < limits.Length; i++)
            {
                if (limits[i].Maximum < 2 * limits[i].Moat) return false;
                prefixLimits[i] = new MoatSearchLimit(limits[i].Ground, limits[i].Moat,
                    limits[i].Maximum - 2 * limits[i].Moat);
            }
            bool found = weightedMoatRoutePlanner.TryBuildImprovement(shadow.PlayerId,
                shadow.StartX, shadow.StartY, px, py, shadow.CostProfile, false, prefixLimits,
                out WeightedMoatRouteSummary prefixSummary, out WeightedMoatEncodedRoute prefix,
                requireMoat: false, maximumEdges: WeightedMoatRoutePlanner.MaximumRouteEdges - 2);
            shadow.AccumulatedSearchMilliseconds += prefixSummary.SearchMilliseconds;
            bool emptyPrefix = prefix.DirectionCount == 0 && prefix.Bytes != null && shadow.StartX == px && shadow.StartY == py;
            if (!found || (!prefix.IsValid && !emptyPrefix) || prefix.DirectionCount + 2 > WeightedMoatRoutePlanner.MaximumRouteEdges) return false;
            int length = prefix.DirectionCount + 2;
            var bytes = new byte[(length + 1) / 2];
            Array.Copy(prefix.Bytes, bytes, prefix.Bytes.Length);
            int at = prefix.DirectionCount;
            if ((at & 1) != 0) bytes[at >> 1] &= 15;
            bytes[at >> 1] |= (byte)(entry << ((at & 1) * 4)); at++;
            bytes[at >> 1] |= (byte)(exit << ((at & 1) * 4));
            fixed (byte* encoded = bytes)
                if (!DescribeWeightedRoute(shadow, encoded, length, out summary)) return false;
            route = new WeightedMoatEncodedRoute(bytes, length);
            return true;
        }

        private void RecordFillRouteDecision(BuilderWeightedScope shadow, string reason)
        {
            // Include attacks and automatic repaths: previously their decoder failures
            // vanished before the command counters were updated.
            fillRouteDecisions.TryGetValue(reason, out long count);
            fillRouteDecisions[reason] = count + 1;
            int now = CaptureCurrentGameTick();
            if (fillRouteLogTick < 0 || now < fillRouteLogTick || now - fillRouteLogTick >= 60)
            { fillRouteLogTick = now; fillRouteLogCount = 0; }
            if (fillRouteLogCount++ >= 3) return;
            Shared.DebugLogHelper.LogInfo(log, $"MoveMoat stage=fill-route unit={shadow.UnitId} " +
                $"start=({shadow.StartX},{shadow.StartY}) target=({shadow.TargetX},{shadow.TargetY}) " +
                $"workTile={shadow.FillPlan?.MoatWorkTargetTileId ?? -1} command={shadow.Command} decision={reason} total={count + 1} " +
                $"edgeDetail=[{weightedMoatRoutePlanner.DescribeLastRejectedEdge()}] " +
                $"searches={shadow.SearchPasses} searchMs={shadow.AccumulatedSearchMilliseconds:F3}.");
        }
    }
}
