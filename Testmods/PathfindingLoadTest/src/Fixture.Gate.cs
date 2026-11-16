using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingLoadTest
{
    // "-gate" scenarios: one ordinary own gatehouse, placed away from the unit region before the
    // topology wait, adds an entry to Vanilla's portal table (D8510 via placement 6D580) and moves
    // the table's id bound (pathManager+0, written by F4540 each tick; FBCB9319). This makes the
    // Pathfinding access-change path that used to trigger a whole-map anchor resweep deterministic.
    internal sealed partial class Fixture
    {
        // Hash-bound (BindMove checks FBCB9319): Vanilla path manager / portal table base.
        private const long PortalTableRva = 0x60AD660;
        private const int PortalEntryInts = 0x81, PortalStateInt = 0x809, PortalBuildingInt = 0x80C, PortalSlots = 200;
        private IEnumerable<AutomationWait> PlaceGate(AutomationRun run)
        {
            string before = run.InSimulation(run.CurrentMission, DescribePortals);
            int x = 0, y = 0; bool found = false;
            // East of the own keep: the unit region, wall column and target lie west of it.
            for (int dy = -40; dy <= 40 && !found; dy += 4)
                for (int dx = 12; dx <= 60 && !found; dx += 4)
                {
                    int px = keepX + dx, py = keepY + dy;
                    found = run.InSimulation(run.CurrentMission, () => GateSiteFree(px, py));
                    if (found) { x = px; y = py; }
                }
            Require(found, "No vacant flat gatehouse site east of the own keep");
            Require(run.TrySpawnBuilding(run.CurrentMission, owner, x, y, eMappers.MAPPER_GATE_INNER, 0, out int gateId, out uint gateGlobal),
                "Gatehouse placement failed at " + x + "," + y);
            string placed = run.InSimulation(run.CurrentMission, DescribePortals);
            log("gate placed id=" + gateId + " global=" + gateGlobal + " at=" + x + "," + y + " portalsBefore=[" + before + "] portalsAfterPlace=[" + placed + "]");
            yield break;
        }
        // Bound = pathManager+0; top = highest slot with a non-zero state and its building.
        private string DescribePortals()
        {
            IntPtr table = new IntPtr(GetModuleHandle("CrusaderDE.dll").ToInt64() + PortalTableRva);
            int bound = Marshal.ReadInt32(table), used = 0, top = 0, topBuilding = 0;
            for (int id = 1; id < PortalSlots; id++)
            {
                if (Marshal.ReadInt32(table, (id * PortalEntryInts + PortalStateInt) * 4) == 0) continue;
                used++; top = id; topBuilding = Marshal.ReadInt32(table, (id * PortalEntryInts + PortalBuildingInt) * 4);
            }
            return "bound=" + bound + " used=" + used + " top=" + top + " topBuilding=" + topBuilding;
        }
        private bool GateSiteFree(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            if (Math.Max(Math.Abs(x - keepX), Math.Abs(x + 7 - keepX)) >= keepRadius ||
                Math.Max(Math.Abs(y - keepY), Math.Abs(y + 7 - keepY)) >= keepRadius) return false;
            // Never near the unit region, the wall column or the target.
            if (x + 12 >= originX - 14 && x - 7 <= originX + width + 30 && y + 12 >= originY - 16 && y - 7 <= originY + height + 16) return false;
            int first = -1;
            for (int yy = y - 7; yy <= y + 12; yy++)
                for (int xx = x - 7; xx <= x + 12; xx++)
                {
                    if (!tiles.IsTileInsideMapBounds(xx, yy)) return false;
                    int tile = tiles.GetTileId(xx, yy);
                    if (!tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetTileBuildingId(tile) != 0 || tiles.GetChimpLayer()[tile] != 0 ||
                        (tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.IsBuilding | TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated)) != 0) return false;
                    int height = tiles.GetTileHeight(tile); if (first >= 0 && height != first) return false; first = height;
                }
            return true;
        }
    }
}
