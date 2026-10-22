using System;
using System.Collections.Generic;
using SHCDESE.Interop;
namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal UnitCommandTraversalProvider Traversal => UnitCommandPathAPI.Traversal;
        internal bool TraversalEnabled => !nativeManualProbe && !disposed && CurrentOptions.TraversalEnabled && Traversal?.Enabled == true;
        internal bool ManualCommandsEnabled => !disposed && settings.EnableMod && settings.EnableImprovedManualUnitCommands;
        internal void InvalidateFastMoatData()
        {
            // Maintenance must continue while policy is disabled; no route is searched.
            Traversal?.InvalidateFastMoatData();
             return;
        }
        internal void LogAndResetFastMoatMetrics()
        {
            Traversal?.LogAndResetFastMoatMetrics();
             return;
        }
        internal void ClearDeferredFastMoveScope()
        {
            Traversal?.ClearDeferredFastMoveScope();
             return;
        }
        internal void CaptureDeferredFastMoveScope(MoveCommandScope command)
        {
            if (TraversalEnabled) Traversal.CaptureDeferredFastMoveScope(command);
             return;
        }
        internal bool IsDeferredFastMoveAuthorized(PlanScope plan, GameUnit* unit)
        {
            if (TraversalEnabled) return Traversal.IsDeferredFastMoveAuthorized(plan, unit);
             return false;
        }
        internal bool TryChooseFastFormation(IntPtr manager, int x, int y, out int tile)
        {
            if (TraversalEnabled) return Traversal.TryChooseFastFormation(manager, x, y, out tile);
            tile = default; return false;
        }
    }
}
