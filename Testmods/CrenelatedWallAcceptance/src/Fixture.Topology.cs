using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using APIShared;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // Measurement only (mod disabled): which neighbours Vanilla treats as a straight crenel row,
    // which heights it leaves after render maintenance and which crenels are walkable.
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> TopologySteps()
        {
            yield return new AutomationStep("topology-load", TimeSpan.FromSeconds(180), r => LoadEditor(r, false, null));
            yield return new AutomationStep("topology-measure", TimeSpan.FromSeconds(300), Topology, true);
        }

        private IEnumerable<AutomationWait> Topology(AutomationRun run)
        {
            var m = run.CurrentMission;
            log(Engine(() =>
            {
                owner = 1;
                Require(CenterArea(24, 16, out ex, out ey), "no free 24x16 area");
                return "TOPO_AREA " + ex + "," + ey + " " + DirectionTable(ex + 10, ey + 8);
            }));
            // Axis rows in front of an axis wall: dragged (y=1) and piece by piece (y=4).
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 2, 3, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 4, 2, 6, 2, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 0, 5, 3, 5, null)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 4, 5, 6, 5, null)) yield return w;
            foreach (var w in UiWall(run, m, "axis-drag", 1, eMappers.MAPPER_CRENAL, 1, 1, 5, 1, false, unmanaged: true)) yield return w;
            for (int i = 1; i <= 5; i++) foreach (var w in UiWall(run, m, "axis-piece", 1, eMappers.MAPPER_CRENAL, i, 4, i, 4, false, unmanaged: true)) yield return w;
            // Main diagonal (+1,+1) like the user's line, in front of a diagonal wall: dragged and piece by piece.
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 8, 0, 13, 5, null)) yield return w;
            foreach (var w in UiWall(run, m, "diag-drag", 1, eMappers.MAPPER_CRENAL, 9, 0, 13, 4, false, unmanaged: true)) yield return w;
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 8, 8, 13, 13, null)) yield return w;
            for (int i = 0; i <= 4; i++) foreach (var w in UiWall(run, m, "diag-piece", 1, eMappers.MAPPER_CRENAL, 9 + i, 8 + i, 9 + i, 8 + i, false, unmanaged: true)) yield return w;
            // Anti-diagonal (+1,-1), dragged.
            foreach (var w in UiWall(run, m, "support", 1, eMappers.MAPPER_WALL, 16, 6, 21, 1, null)) yield return w;
            foreach (var w in UiWall(run, m, "anti-drag", 1, eMappers.MAPPER_CRENAL, 17, 6, 21, 2, false, unmanaged: true)) yield return w;
            yield return run.WaitForTicks(m, 120);
            log(Engine(() => Row("axis-drag", Enumerable.Range(1, 5).Select(i => (i, 1)))));
            log(Engine(() => Row("axis-piece", Enumerable.Range(1, 5).Select(i => (i, 4)))));
            log(Engine(() => Row("diag-drag", Enumerable.Range(0, 5).Select(i => (9 + i, i)))));
            log(Engine(() => Row("diag-piece", Enumerable.Range(0, 5).Select(i => (9 + i, 8 + i)))));
            log(Engine(() => Row("anti-drag", Enumerable.Range(0, 5).Select(i => (17 + i, 6 - i)))));
            // One real walk check per diagonal crenel of the dragged row (Assassin of slot 1, on the free side).
            for (int i = 0; i <= 4; i++)
                foreach (var w in EditorClimb(run, m, "diag-drag-" + i, 10 + i, i - 1 < 0 ? 0 : i - 1, 9 + i, i)) yield return w;
            log("TOPO_DONE");
        }

        private string Row(string label, IEnumerable<(int, int)> cells)
        {
            var edges = GamePathingManagerAPI.Instance.GetPathEdgeMaskGrid();
            var parts = new List<string>();
            foreach (var c in cells)
            {
                int x = c.Item1 + ex, y = c.Item2 + ey, id = Tile(x, y);
                uint f = Flags(id);
                parts.Add(x + "," + y + (IsCrenel(id) ? ((f & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? ":H" : ":L") : ":-") +
                    ":p" + (Parity(x, y) ? 1 : 0) + ":e" + edges[id].ToString("X2") + ":h" + GameTileManagerAPI.Instance.GetTileHeight(id));
            }
            return "TOPO_ROW " + label + " " + string.Join(" ", parts);
        }

        // The native per-row neighbour delta table at TileManager+0 (int[8] per row y).
        private string DirectionTable(int x, int y)
        {
            IntPtr manager = GameTileManagerAPI.Instance.GetTileManager();
            int center = Tile(x, y);
            var names = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                int delta = Marshal.ReadInt32(manager, (y * 8 + i) * 4);
                string match = "?";
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        if ((dx != 0 || dy != 0) && Tile(x + dx, y + dy) - center == delta) match = "(" + dx + "," + dy + ")";
                names.Add(i + "=" + delta + match);
            }
            return "TOPO_DIRS row=" + y + " " + string.Join(" ", names);
        }

        private bool climbArrived;

        private IEnumerable<AutomationWait> EditorClimb(AutomationRun run, MissionContext m, string label, int sx, int sy, int tx, int ty)
        {
            int unit = 0; uint global = 0; int tribe = 0; bool sent = false;
            int ax = sx + ex, ay = sy + ey, bx = tx + ex, by = ty + ey;
            Engine(() =>
            {
                int tile = Tile(ax, ay);
                if (!GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(tile)) return 0;
                long created = GameUnitManagerAPI.Instance.CreateUnitLocal(1, 1, ax, ay, GameTileManagerAPI.Instance.GetTileHeight(tile), eChimps.CHIMP_TYPE_ARAB_ASSASIN);
                if (created <= 0 || created > int.MaxValue) return 0;
                unit = (int)created; global = Global(unit);
                tribe = checked((int)GameTribeManagerAPI.Instance.Create(1));
                GameTribeManagerAPI.Instance.AssignUnit(tribe, unit);
                return 0;
            });
            climbArrived = false;
            if (unit == 0) { log("TOPO_CLIMB " + label + " spawn-blocked at " + ax + "," + ay); yield break; }
            yield return run.WaitForTicks(m, 10);
            sent = Engine(() => GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, bx, by));
            string state = "";
            for (int i = 0; i < 30 && sent; i++)
            {
                yield return run.WaitForTicks(m, 40);
                if (Engine(() => Settled(unit, global, out state)) && (i >= 3 || state.StartsWith(bx + "," + by + ":"))) break;
            }
            climbArrived = state.StartsWith(bx + "," + by + ":");
            log("TOPO_CLIMB " + label + " target=" + bx + "," + by + " sent=" + sent + " unit=" + state + " arrived=" + climbArrived);
            Engine(() => { Kill(unit); return 0; });
        }
    }
}
