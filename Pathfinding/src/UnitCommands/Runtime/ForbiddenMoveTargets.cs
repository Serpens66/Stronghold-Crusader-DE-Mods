using System;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        // Reject only a verified rule of the active engine's real group owner. This is a
        // command boundary, not a replacement target search or a change to Vanilla's APIs.
        private bool IsForbiddenMoveTarget(int tribeId, int x, int y)
        {
            try
            {
                if (!EngineEnabled || !RouteTileRules.HasRules || tribeId <= 0 || tribeId >= MaximumTribeCount ||
                    (uint)x >= MapWidth || (uint)y >= MapWidth ||
                    !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                    tribe->r_AliveState != AliveState.IsAlive) return false;
                int player = tribe->r_PlayerIdOwner;
                return player >= 1 && player <= 8 && RouteTileRules.GetEffect(x, y, player, out _) == RouteTileEffect.Forbidden;
            }
            catch (Exception exception)
            {
                // Unknown context retains Vanilla. Never let a rule-read failure unwind into native code.
                ReportNativeDetourBarrier("forbidden-move-target", exception);
                return false;
            }
        }
    }
}
