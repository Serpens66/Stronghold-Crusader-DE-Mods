using System;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using SHCDESE.Interop;

namespace PathfindingLoadTest
{
    // Selection-phase guard (16.11.2026, after one run failed with "Spawn identity lost" after
    // 72 s in initialize-select): a foreign move order for the freshly selected group changes
    // its tribe, so the exact selection never matches and the unpaused wait ran until a unit
    // died. The guard names that cause directly and bounds the wait in native ticks.
    internal sealed partial class Fixture
    {
        private const int SelectionTickLimit = 50;
        private int? selectionFirstTick;
        private string selectionMismatch = "not evaluated";

        private bool SelectionReady(AutomationRun run)
        {
            if (!SimulationClock.TryGetCurrentTick(run.CurrentMission, out int now)) return false;
            if (!selectionFirstTick.HasValue) selectionFirstTick = now;
            RequireNoSelectionMove(now);
            if (SelectionMatches()) return true;
            if (SimulationClock.TryGetElapsedTicks(selectionFirstTick.Value, now, out int elapsed) && elapsed > SelectionTickLimit)
                throw new InvalidOperationException("Exact selection not reached within " + SelectionTickLimit +
                    " ticks (tick " + now + "): " + selectionMismatch);
            return false;
        }

        // Spawned units keep their spawn target until the test's own command; any change during
        // selection is a move order from elsewhere (player input, another mod).
        private unsafe void RequireNoSelectionMove(int tick)
        {
            int changed = 0; string sample = "";
            foreach (var identity in units)
            {
                if (!UnitAccess.TryGetById(identity.Id, out GameUnit* unit, out _) || unit->r_GlobalId != identity.Global) continue;
                if (unit->r_TargetTilePositionX == identity.BeforeTargetX && unit->r_TargetTilePositionY == identity.BeforeTargetY) continue;
                if (changed++ < 3)
                    sample += identity.Id + ":" + identity.BeforeTargetX + "," + identity.BeforeTargetY + "->" +
                        unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY + "/tribe=" + unit->r_TribeId + " ";
            }
            if (changed != 0)
                throw new InvalidOperationException("Unexpected move order for the selected units during selection: changed=" +
                    changed + "/" + units.Count + " tick=" + tick + " sample=[" + sample.Trim() + "]");
        }
    }
}
