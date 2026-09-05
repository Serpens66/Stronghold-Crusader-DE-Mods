using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using MonoMod.RuntimeDetour;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int PathReconstructionDelegate(IntPtr pathManager);
        private PathReconstructionDelegate originalPathReconstruction, rootedPathReconstruction;
        private NativeDetour pathReconstructionDetour;
        private readonly Dictionary<long, bool> nativeGroundDecisions = new Dictionary<long, bool>();
        private object nativeGroundOwner;
        private int nativeGroundEpoch, nativeGroundTick, nativeGroundPlayer;
        private bool nativeGroundProbeBusy;
        private long nativeGroundQueries, nativeGroundCacheHits;

        private bool TryBuildTerminalFillRoute(PlanScope plan, GameUnit* unit, int startX, int startY,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            summary = default; route = default;
            if (!plan.MoatWorkMovement || (TribeAICommand)unit->r_AI_LastIssuedTribeCommand !=
                TribeAICommand.Unknown7 ||
                !IsCompletedEnemyMoatForPlayer(plan.PlayerId, plan.MoatWorkTargetTileId)) return false;
            var contact = GameTileManagerAPI.Instance.GetTileVectorFromId(plan.MoatWorkTargetTileId);
            int endDirection = -1;
            for (int d = 0; d < 8; d++)
                if (contact.X + WeightedMoatRoutePlanner.DirectionX[d] == plan.TargetX &&
                    contact.Y + WeightedMoatRoutePlanner.DirectionY[d] == plan.TargetY) endDirection = d;
            if (endDirection < 0) return false;
            if (!IsTerminalFillEdgeValid(plan.PlayerId, contact.X, contact.Y, plan.TargetX, plan.TargetY, endDirection)) return false;
            int bestLength = int.MaxValue;
            for (int d = 0; d < 8; d++)
            {
                int x = contact.X - WeightedMoatRoutePlanner.DirectionX[d];
                int y = contact.Y - WeightedMoatRoutePlanner.DirectionY[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth ||
                    !IsTerminalFillEdgeValid(plan.PlayerId, x, y, contact.X, contact.Y, d) ||
                    !weightedMoatRoutePlanner.TryBuildReachabilityEncoded(plan.PlayerId, startX, startY, x, y, false,
                        out WeightedMoatRouteSummary candidate, out WeightedMoatEncodedRoute prefix) ||
                    !prefix.IsValid || candidate.MoatEdges <= 0 || prefix.DirectionCount + 2 > 2000 ||
                    prefix.DirectionCount + 2 >= bestLength) continue;
                int nextLength = prefix.DirectionCount + 2;
                var bytes = new byte[(nextLength + 1) / 2];
                Array.Copy(prefix.Bytes, bytes, prefix.Bytes.Length);
                // Clear the unused high nibble before appending the exact terminal contact.
                int at = prefix.DirectionCount;
                if ((at & 1) != 0) bytes[at >> 1] &= 0x0F;
                bytes[at >> 1] |= (byte)(d << ((at & 1) * 4)); at++;
                bytes[at >> 1] |= (byte)(endDirection << ((at & 1) * 4));
                // The common publication audit validates BOTH added edges, height, corners,
                // ownership and the exact penultimate work tile before accepting any bytes.
                route = new WeightedMoatEncodedRoute(bytes, nextLength);
                summary = candidate; bestLength = nextLength;
            }
            return route.IsValid;
        }

        private bool IsTerminalFillEdgeValid(int player, int x, int y, int nextX, int nextY, int direction)
        {
            int from = GameTileManagerAPI.Instance.GetTileId(x, y), to = GameTileManagerAPI.Instance.GetTileId(nextX, nextY);
            if (!IsValidTileId(from) || !IsValidTileId(to)) return false;
            if ((direction & 1) != 0 &&
                (IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(nextX, y)) ||
                 IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(x, nextY)))) return false;
            return weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, from, nextX, nextY, to,
                direction, true, false, MoatTraversalPolicy.AllowEnemyForDiagnostic, out _, out _);
        }

        private bool TryDeferToNativeGroundPlan(PlanScope plan, GameUnit* unit)
        {
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame == null || frame.Args.Unknown != 0 || plan == null || plan.MoatWorkMovement ||
                plan.AttackMovementQualified || activeAttackCommand != null) return false;
            GetNativeMovementStart(unit, out int x, out int y);
            int start = GameTileManagerAPI.Instance.GetTileId(x, y);
            int target = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            if (!IsValidTileId(start) || !IsValidTileId(target) ||
                ((tileFlags[start] | tileFlags[target]) & (CompletedMoatTileFlag | CursorSpecialStructureTileFlagMask)) != 0)
                return false;
            int sourceRegion = pathRegionGrid[start], targetRegion = pathRegionGrid[target];
            if (sourceRegion <= 0 || targetRegion <= 0) return false;
            if (sourceRegion == targetRegion) return true;
            if (originalRegionPairReachability == null || nativeGroundProbeBusy) return false;
            PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
            // 1.42.0 / FBCB9319: MoveHere's native mode is a SHORT at slot+0x9B8,
            // i.e. GameUnit+0x35C. Keep the actual native source,target argument order.
            int mode = *(short*)((byte*)unit + 0x35C);
            long key = ((long)sourceRegion << 32) | ((long)targetRegion << 16) | (ushort)mode;
            if (nativeGroundDecisions.TryGetValue(key, out bool reachable))
            { nativeGroundCacheHits++; return reachable; }
            byte* context = (byte*)nativePathManager.ToPointer();
            int state = *(int*)(context + 0xC0), counter = *(int*)(context + 0xC4), routeFlag = *(int*)(context + 0x98);
            nativeGroundProbeBusy = true;
            try
            {
                nativeGroundQueries++;
                *(int*)(context + 0x98) = 0;
                reachable = originalRegionPairReachability(nativePathManager, unit->r_ControllableForPlayerId,
                    sourceRegion, targetRegion, mode) != 0 && *(int*)(context + 0x98) == 0;
                nativeGroundDecisions[key] = reachable;
                return reachable;
            }
            finally
            {
                *(int*)(context + 0xC0) = state; *(int*)(context + 0xC4) = counter;
                *(int*)(context + 0x98) = routeFlag; nativeGroundProbeBusy = false;
            }
        }

        private int BuildReconstructedUnitPath(IntPtr pathManager)
        {
            PlanScope plan = GetBuilderPlan(pathManager, reportMismatch: true);
            BuilderWeightedScope shadow = TryCaptureBuilderWeightedScope(pathManager);
            try
            {
                int result = BuildPathWithCompletedMoatRouteVariantCore(pathManager, 0, 0, plan, true, true);
                if (shadow != null) ObserveWeightedMoatShadowResult(pathManager, result, shadow);
                return shadow != null && shadow.PublishedBuilderResult >= 0 ? shadow.PublishedBuilderResult : result;
            }
            finally
            {
                PlanScope handoff = GetCurrentUnitMoveFrame()?.InheritedPlan ?? plan;
                if (handoff != null && handoff.MoatWorkMovement && ReferenceEquals(pendingPlan, handoff)) pendingPlan = null;
            }
        }

        private void PrepareMovementSearch(PlanScope plan, int playerId, object explicitOwner = null)
        {
            // Work callbacks may mutate terrain between two selections in the same tick.
            // Their exact synchronous scope takes precedence over a surrounding command.
            object session = explicitOwner ?? (object)activeMoatWorkSelection ?? plan?.MoatWorkSearch ??
                (object)activeMoveCommand ?? GetCurrentUnitMoveFrame() ??
                (object)activeAttackCommand ?? plan;
            int tick = CaptureCurrentGameTick();
            if (!ReferenceEquals(session, nativeGroundOwner) || nativeGroundEpoch != mapEpoch ||
                nativeGroundTick != tick || nativeGroundPlayer != playerId)
            {
                if (nativeGroundOwner != null && activeMoveCommand != null)
                    activeMoveCommand.TargetedRouteDecisions.Clear();
                nativeGroundDecisions.Clear(); nativeGroundOwner = session;
                nativeGroundEpoch = mapEpoch; nativeGroundTick = tick; nativeGroundPlayer = playerId;
            }
            weightedMoatRoutePlanner.SetSearchSession(session, playerId, mapEpoch, tick);
        }

        private bool TryAllowUnitMoveRegion(IntPtr pathManager, int playerId, int targetRegion,
            int startX, int startY, int vanilla, out int result)
        {
            result = vanilla;
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame == null || activeMoatWorkSelection != null || activeAttackApproachDiagnostic != null) return false;
            frame.RegionReached = true;
            PlanScope plan = GetUnitMovePlan(frame, frame.Args.UnitId);
            if (plan == null || pathManager != nativePathManager || vanilla != 0) return true;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(plan.UnitId, out GameUnit* unit) || unit == null)
                return true;
            GetNativeMovementStart(unit, out int actualX, out int actualY);
            int tile = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            bool valid = plan.ModeObserved && plan.FriendlyRouteQualified &&
                plan.PlayerId == playerId && *moatPathMode == 1 &&
                actualX == startX && actualY == startY && IsValidTileId(tile) &&
                targetRegion > 0 && targetRegion <= MaximumRegionId && pathRegionGrid[tile] == targetRegion;
            if (!valid) return false;
            result = targetRegion;
            return true;
        }
    }
}
