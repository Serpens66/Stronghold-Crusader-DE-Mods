extern alias BugfixesRuntime;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL MovementSearchContext.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal bool nativeGroundProbeBusy;

        internal bool TryBuildTerminalFillRoute(PlanScope plan, GameUnit* unit, int startX, int startY,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            summary = default; route = default;
            if (!plan.MoatWorkMovement || (TribeAICommand)unit->r_AI_LastIssuedTribeCommand !=
                TribeAICommand.Unknown7 ||
                !runtime.IsCompletedEnemyMoatForPlayer(plan.PlayerId, plan.MoatWorkTargetTileId)) return false;
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
                    !TryBuildMovementReachabilityEncoded(plan.PlayerId, startX, startY, x, y, false,
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

        internal bool IsTerminalFillEdgeValid(int player, int x, int y, int nextX, int nextY, int direction)
        {
            int from = GameTileManagerAPI.Instance.GetTileId(x, y), to = GameTileManagerAPI.Instance.GetTileId(nextX, nextY);
            if (!IsValidTileId(from) || !IsValidTileId(to)) return false;
            if ((direction & 1) != 0 &&
                (runtime.IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(nextX, y)) ||
                 runtime.IsCompletedEnemyMoatForPlayer(player, GameTileManagerAPI.Instance.GetTileId(x, nextY)))) return false;
            return runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, from, nextX, nextY, to,
                direction, true, false, MoatTraversalPolicy.AllowEnemyForDiagnostic, out _, out _);
        }

        internal bool TryDeferToNativeGroundPlan(PlanScope plan, GameUnit* unit)
        {
            UnitMoveFrame frame = runtime.GetCurrentUnitMoveFrame();
            if (frame == null || frame.Args.Unknown != 0 || plan == null || plan.MoatWorkMovement ||
                plan.AttackMovementQualified || activeAttackCommand != null) return false;
            GetNativeMovementStart(unit, out int x, out int y);
            int start = GameTileManagerAPI.Instance.GetTileId(x, y);
            int target = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            if (!IsValidTileId(start) || !IsValidTileId(target) ||
                ((runtime.tileFlags[start] | runtime.tileFlags[target]) & (CompletedMoatTileFlag | CursorSpecialStructureTileFlagMask)) != 0)
                return false;
            int sourceRegion = runtime.pathRegionGrid[start], targetRegion = runtime.pathRegionGrid[target];
            if (sourceRegion <= 0 || targetRegion <= 0) return false;
            if (sourceRegion == targetRegion) return true;
            if (runtime.originalRegionPairReachability == null || nativeGroundProbeBusy) return false;
            runtime.PrepareMovementSearch(plan, unit->r_ControllableForPlayerId);
            // FBCB9319: MoveHere's native mode is a SHORT at slot+0x9B8,
            // i.e. GameUnit+0x35C. Keep the actual native source,target argument order.
            int mode = *(short*)((byte*)unit + 0x35C);
            long key = ((long)sourceRegion << 32) | ((long)targetRegion << 16) | (ushort)mode;
            if (runtime.nativeGroundDecisions.TryGetValue(key, out bool reachable))
            { runtime.nativeGroundCacheHits++; return reachable; }
            byte* context = (byte*)runtime.nativePathManager.ToPointer();
            int state = *(int*)(context + 0xC0), counter = *(int*)(context + 0xC4), routeFlag = *(int*)(context + 0x98);
            nativeGroundProbeBusy = true;
            try
            {
                runtime.nativeGroundQueries++;
                *(int*)(context + 0x98) = 0;
                reachable = runtime.originalRegionPairReachability(runtime.nativePathManager, unit->r_ControllableForPlayerId,
                    sourceRegion, targetRegion, mode) != 0 && *(int*)(context + 0x98) == 0;
                runtime.nativeGroundDecisions[key] = reachable;
                return reachable;
            }
            finally
            {
                *(int*)(context + 0xC0) = state; *(int*)(context + 0xC4) = counter;
                *(int*)(context + 0x98) = routeFlag; nativeGroundProbeBusy = false;
            }
        }
    }
}
