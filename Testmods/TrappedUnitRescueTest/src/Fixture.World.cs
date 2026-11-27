using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private void FindSite()
        {
            var tiles = GameTileManagerAPI.Instance;
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            int size = tiles.GetCurrentMapSize();
            for (int radius = 12; radius < size; radius += 8)
                for (int dy = -radius; dy <= radius; dy += 8)
                    for (int dx = -radius; dx <= radius; dx += 8)
                    {
                        int x = keep.X + dx, y = keep.Y + dy;
                        if (!FlatSquare(x, y, 12, out int height)) continue;
                        cx = x; cy = y; elevation = height;
                        log("site center=" + cx + "," + cy + " elevation=" + height + " keep=" + keep.X + "," + keep.Y);
                        return;
                    }
            throw new InvalidOperationException("No empty flat25x25 site for the controlled fixture");
        }

        private static bool FlatSquare(int x, int y, int radius, out int elevation)
        {
            var tiles = GameTileManagerAPI.Instance;
            elevation = -1;
            for (int yy = y - radius; yy <= y + radius; yy++)
                for (int xx = x - radius; xx <= x + radius; xx++)
                {
                    if (!tiles.IsTileInsideMapBounds(xx, yy)) return false;
                    int tile = tiles.GetTileId(xx, yy);
                    var flags = tiles.GetTilePropertyFlag(tile);
                    if (!tiles.IsValidTileId(tile) || tiles.GetTileBuildingId(tile) != 0 || tiles.GetTileUnitId(tile) != 0 ||
                        !tiles.IsTileWalkableAndUnoccupied(tile) || (flags & (TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsTree)) != 0) return false;
                    int height = tiles.GetTileHeight(tile);
                    if (elevation >= 0 && elevation != height) return false;
                    elevation = height;
                }
            return elevation >= 0;
        }

        private void SpawnMatrix(eChimps type)
        {
            Require(actors.Count == 0, "Previous actors were not retired");
            for (int y = cy; y < cy + 5; y++) for (int x = cx; x < cx + 5; x++) SpawnSingle(type, x, y);
        }

        private unsafe void SpawnSingle(eChimps type, int x, int y)
        {
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, elevation, type));
            GameUnit* unit = null;
            Require(id > 0 && UnitAccess.TryGetById(id, out unit, out _) && unit != null, "Actor spawn failed");
            Require(unit->r_UnitChimp == type && unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y, "Spawn projected unexpectedly");
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(owner));
            Require(tribe > 0 && GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "Actor tribe assignment failed");
            actors.Add(new Actor { Id = id, Global = unit->r_GlobalId, Type = type, Tribe = tribe, X = x, Y = y, SourceX = x, SourceY = y });
        }

        private unsafe void VerifyLiveAndRemember()
        {
            foreach (var actor in actors)
            {
                GameUnit* unit = Resolve(actor);
                Require(UnitAccess.IsReallyAlive(unit), "Actor was not initialized alive");
                Require(unit->r_CurrentHealth > 0, "Actor has zero health");
                Require(unit->r_MovementSubstep == 8 && unit->r_CurrentTilePositionX == unit->r_NextTilePositionX2 &&
                    unit->r_CurrentTilePositionY == unit->r_NextTilePositionY2, "Fixture requires an actually stationary actor");
                actor.Health = unit->r_CurrentHealth;
                actor.SourceX = actor.X = unit->r_CurrentTilePositionX; actor.SourceY = actor.Y = unit->r_CurrentTilePositionY;
                CheckActor(actor, true);
            }
        }

        private List<int> BuildBridgeAndCheck(eChimps type)
        {
            var scope = BeginMutation("controlled-native-drawbridge-" + type, cx + 2, cy + 2, 5);
            List<int> trapped;
            try
            {
                // High-level native placement, with its explicit test-world placement bypass. No terrain is painted by this case.
                GameBuildingManagerAPI.Instance.CreatePrefab(owner, cx, cy, eMappers.MAPPER_DRAWBRIDGE, 5, 0, true, true);
                var tiles = GameTileManagerAPI.Instance;
                int bridge = tiles.GetTileBuildingId(tiles.GetTileId(cx, cy));
                Require(bridge > 0, "Native drawbridge creation did not establish an occupied footprint");
                Require(BridgeType(bridge), "Native placement made a different building type");
                int cells = 0;
                for (int y = cy; y < cy + 5; y++) for (int x = cx; x < cx + 5; x++)
                    if (tiles.GetTileBuildingId(tiles.GetTileId(x, y)) == bridge) cells++;
                Require(cells == 25, "Native bridge footprint differs from the matrix: " + cells + "/25");
                trapped = actors.Where(a => !HasStructuralExit(a)).Select(a => a.Id).ToList();
                log("bridge-pre-rescue type=" + type + " building=" + bridge + " footprint=" + cells +
                    " trapped=" + trapped.Count + " actorCount=" + actors.Count + " enabled=" + expectedEnabled);
                Require(trapped.Count > 0, "Native bridge did not trap any actor; this fixture cannot establish rescue");
                var results = scope.Complete(false);
                foreach (var result in results) log("rescue-result " + result);
            }
            finally { scope.Dispose(); }
            foreach (var actor in actors)
            {
                CheckActor(actor, true);
                if (!trapped.Contains(actor.Id)) continue;
                if (expectedEnabled)
                    Require(!AtSource(actor) && HasStructuralExit(actor), "A genuinely trapped actor was not locally rescued: " + actor.Id);
                else Require(AtSource(actor) && !HasStructuralExit(actor), "Baseline bridge trap did not remain unchanged");
            }
            return trapped;
        }

        private static unsafe bool BridgeType(int id) => BuildingAccess.TryGetById(id, out GameBuilding* bridge, out _) &&
            bridge != null && bridge->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE;

        // Independent physical oracle for this bridge fixture: ordinary links, reversed destination links,
        // stored moat permission and conservative Assassin climb eligibility. No native predicate is called as a probe.
        private unsafe bool HasStructuralExit(Actor actor)
        {
            GameUnit* unit = Resolve(actor);
            var tiles = GameTileManagerAPI.Instance;
            var paths = GamePathingManagerAPI.Instance;
            int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
            int source = tiles.GetTileId(x, y);
            var sourceFlags = tiles.GetTilePropertyFlag(source);
            // Native typed+36C is the stored moat permission (native slot+9C8).
            ushort moat = *(ushort*)((byte*)unit + 0x36C);
            for (int d = 0; d < 8; d++)
            {
                int nx = x + Dx[d], ny = y + Dy[d];
                if (!tiles.IsTileInsideMapBounds(nx, ny)) continue;
                int next = tiles.GetTileId(nx, ny);
                var flags = tiles.GetTilePropertyFlag(next);
                if ((flags & (TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder)) != 0) continue;
                if (paths.HasPathEdge(x, y, (PackedPathDirection)d) || paths.HasPathEdge(nx, ny, (PackedPathDirection)(d ^ 4))) return true;
                if (moat != 0 && ((flags & TilePropertyFlag.IsMoat) != 0 ||
                    ((sourceFlags & TilePropertyFlag.IsMoat) != 0 && ((uint)flags & 0x4A5015B1u) == 0))) return true;
                if (actor.Type == eChimps.CHIMP_TYPE_ARAB_ASSASIN && ((sourceFlags | flags) & TilePropertyFlag.IsWall) != 0 &&
                    ((uint)flags & 0x4A5014B1u) == 0) return true;
            }
            return false;
        }

        private unsafe GameUnit* Resolve(Actor actor)
        {
            Require(UnitAccess.TryGetById(actor.Id, out GameUnit* unit, out _) && unit != null && unit->r_GlobalId == actor.Global &&
                unit->r_UnitChimp == actor.Type && unit->r_ControllableForPlayerId == owner, "Actor identity changed: " + actor.Id);
            return unit;
        }

        private unsafe void CheckActor(Actor actor, bool exactHealth)
        {
            GameUnit* unit = Resolve(actor);
            Require(UnitAccess.IsReallyAlive(unit), "Actor no longer alive: " + actor.Id);
            Require(!exactHealth || unit->r_CurrentHealth == actor.Health, "Health changed during rescue: " + actor.Id);
            Require(unit->r_TribeId == actor.Tribe, "Rescue changed tribe membership: " + actor.Id);
            actor.X = unit->r_CurrentTilePositionX; actor.Y = unit->r_CurrentTilePositionY;
            int current = GameTileManagerAPI.Instance.GetTileId(actor.X, actor.Y);
            Require(unit->r_CurrentPositionTileId == (uint)current, "Coordinates and packed tile disagree: " + actor.Id);
            Require(CountInTileChain(current, actor.Id) == 1, "Actor is not registered exactly once on its current tile: " + actor.Id);
            if (actor.X != actor.SourceX || actor.Y != actor.SourceY)
                Require(CountInTileChain(GameTileManagerAPI.Instance.GetTileId(actor.SourceX, actor.SourceY), actor.Id) == 0,
                    "Relocated actor remains in the old tile chain: " + actor.Id);
        }

        private unsafe void CheckBaselineIdentity(Actor actor)
        {
            GameUnit* unit = Resolve(actor);
            Require(UnitAccess.IsReallyAlive(unit) && unit->r_CurrentHealth == actor.Health && unit->r_TribeId == actor.Tribe,
                "Baseline wall handling changed actor identity, health or tribe");
            actor.X = unit->r_CurrentTilePositionX; actor.Y = unit->r_CurrentTilePositionY;
        }

        private static unsafe int CountInTileChain(int tile, int wanted)
        {
            int id = GameTileManagerAPI.Instance.GetTileUnitId(tile), found = 0;
            var seen = new HashSet<int>();
            while (id != 0)
            {
                Require(seen.Add(id) && seen.Count <= 10000, "Cycle in native tile-unit chain");
                Require(UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null, "Unreadable tile-chain member");
                if (id == wanted) found++;
                id = unit->r_InterestingTodo4MaybeRandomPathing;
            }
            return found;
        }

        private unsafe bool AtSource(Actor actor)
        {
            var unit = Resolve(actor);
            return unit->r_CurrentTilePositionX == actor.SourceX && unit->r_CurrentTilePositionY == actor.SourceY;
        }

        private void FindMovementTarget(Actor actor, out int x, out int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int distance = 4; distance <= 10; distance++)
                for (int d = 0; d < 8; d++)
                {
                    int nx = actor.X + Dx[d] * distance, ny = actor.Y + Dy[d] * distance;
                    if (!tiles.IsTileInsideMapBounds(nx, ny)) continue;
                    int tile = tiles.GetTileId(nx, ny);
                    if (tiles.IsTileWalkableAndUnoccupied(tile) && tiles.GetTileUnitId(tile) == 0 &&
                        (tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.IsMoat) == 0)
                    { x = nx; y = ny; return; }
                }
            throw new InvalidOperationException("No free native movement endpoint");
        }

        private unsafe bool MovementOutcome(Actor actor, int targetX, int targetY)
        {
            CheckActor(actor, true);
            GameUnit* unit = Resolve(actor);
            bool stoppedMoving = unit->r_PathPlanStateBitFlags == 0 ||
                (unit->r_CurrentPathPlanIndex >= unit->r_PathPlanLength && unit->r_MovementSubstep == 8);
            return Math.Abs(actor.X - targetX) <= 1 && Math.Abs(actor.Y - targetY) <= 1 && stoppedMoving;
        }

        private unsafe void RetireActors()
        {
            foreach (var actor in actors)
            {
                Resolve(actor);
                bool retired = APIShared.UnitAccess.TryGetById(actor.Id, out GameUnit* checkedUnit, out _) &&
                    GameUnitManagerAPI.Instance.DeleteUnitSafe(actor.Id);
                Require(retired, "Actor retirement rejected");
            }
            actors.Clear();
        }
    }
}
