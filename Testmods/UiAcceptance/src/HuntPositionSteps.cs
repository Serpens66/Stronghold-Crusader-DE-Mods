using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Sight;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static unsafe partial class HuntSteps
    {
        private const int MinimumChickenDistance = 4;
        private const int MaximumChickenDistance = 10;
        private const int MaximumChickenGranaryDistance = 28;
        private const int MaximumFixtureWallAttempts = 12;
        private const int ApproachFixtureDistance = 48;
        private const int CloserFixtureDistance = 16;
        private static readonly (int X, int Y)[] ApproachDirections = { (1, 0), (-1, 0), (0, 1), (0, -1) };

        private static void RegisterHuntPositionSteps()
        {
            Runner.Register("Hunt.BlockedOwnChicken", BlockedOwnChicken);
            Runner.Register("Hunt.ExpectFirstFiringPosition", s => GameSteps.Do(() =>
            {
                lock (Engine())
                {
                    Runner.Require(corridorStartedBlocked, "the initial shot at the fixture was open");
                    Runner.Require(corridorHasFirstApproach && corridorFirstApproachBlocked,
                        "the chicken entered clear sight before the Hunter started hunting it");
                    Runner.Require(corridorFirstApproachDistance >= 3,
                        "the first approach went to the chicken instead of a separate firing tile");
                }
            }));
            Runner.Register("Hunt.CloserSameRabbit", CloserSameRabbit);
        }

        private static IEnumerable<Wait> BlockedOwnChicken(Step step)
        {
            int local = Local();
            yield return new Until("the Hunter has finished returning to his hut", () =>
            {
                lock (Engine()) return IsChickenFixtureHunterIdle(local);
            });
            lock (Engine()) PlaceBlockedOwnChicken(local, step.Int(0, 0));
        }

        private static bool IsChickenFixtureHunterIdle(int local)
        {
            GameUnit* hunter = FindHunter(local);
            return hunter != null && hunter->r_CarryGoodAmount == 0 &&
                hunter->r_PathPlanStateBitFlags == 0 && (hunter->r_AIState == 7 || hunter->r_AIState == 8);
        }

        private static (int X, int Y, int Dx, int Dy, string Obstacle) FindBlockedChickenTile(
            int local, GameUnit* hunter, int granaryX, int granaryY, int rotation)
        {
            var tileApi = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int hx = hunter->r_CurrentTilePositionX, hy = hunter->r_CurrentTilePositionY;
            int start = tileApi.GetTileId(hx, hy);
            ushort land = components[start];
            Runner.Require(land != 0, "the Hunter is not on a land component");

            // A building or existing wall is enough if it blocks the real sight ray and
            // the chicken still shares the Hunter's connected land. Try building a single
            // wall cell only when all the naturally blocked sites failed.
            var triedWall = new HashSet<int>();
            for (int phase = 0; phase < 2; phase++)
            {
                for (int distance = MinimumChickenDistance; distance <= MaximumChickenDistance; distance++)
                {
                    for (int offset = 0; offset < ApproachDirections.Length * 2; offset++)
                    {
                        (int dx, int dy) = ApproachDirections[(rotation + offset % ApproachDirections.Length) % ApproachDirections.Length];
                        if (offset >= ApproachDirections.Length)
                            (dx, dy) = (dx - dy, dy + dx);
                        int x = hx + distance * dx, y = hy + distance * dy;
                        if (Math.Max(Math.Abs(x - granaryX), Math.Abs(y - granaryY)) > MaximumChickenGranaryDistance ||
                            !IsHuntingFixtureTile(tileApi, components, land, x, y))
                            continue;
                        if (IsChickenSiteBlocked(hunter, x, y))
                            return (x, y, dx, dy, phase == 0 ? "existing" : "wall");
                        if (phase == 0 || triedWall.Count >= MaximumFixtureWallAttempts)
                            continue;
                        int wx = hx + (distance / 2) * dx, wy = hy + (distance / 2) * dy;
                        if (!IsHuntingFixtureTile(tileApi, components, land, wx, wy) ||
                            !triedWall.Add(tileApi.GetTileId(wx, wy)))
                            continue;
                        GameBuildingManagerAPI.Instance.CreateWall(local, wx, wy, wx, wy, eMappers.MAPPER_WALL);
                        if (CorridorHasWall(wx, wy) && IsChickenSiteBlocked(hunter, x, y))
                            return (x, y, dx, dy, "wall");
                    }
                }
            }
            throw new InvalidOperationException("no blocked chicken site close to the granary: hunter=" + hx + "," + hy +
                " granary=" + granaryX + "," + granaryY + " land=" + land + " wallAttempts=" + triedWall.Count);
        }

        private static bool IsChickenSiteBlocked(GameUnit* hunter, int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            int height = tiles.GetTileHeight(tiles.GetTileId(x, y));
            var point = new SightPoint(x * 8, y * 8, height + LineOfSight.TargetHeight);
            return LineOfSight.TryGetUnitPoint(in *hunter, LineOfSight.ViewerEyeHeight, out SightPoint viewer) &&
                LineOfSight.TryEvaluate(viewer, point, LineOfSightRoutine.Bidirectional, out int result, out _) && result == 0;
        }

        private static void PlaceBlockedOwnChicken(int local, int rotation)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter for the chicken fixture");
            Runner.Require(rotation >= 0 && rotation < ApproachDirections.Length, "fixture rotation must be 0 to 3");
            Runner.Require(TryFindBuilding(local, eStructs.STRUCT_GRANARY, out int granaryX, out int granaryY), "no granary");
            (int px, int py, int dx, int dy, string obstacle) = FindBlockedChickenTile(local, hunter, granaryX, granaryY, rotation);
            var tileApi = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int tile = tileApi.GetTileId(px, py);
            int hx = hunter->r_CurrentTilePositionX, hy = hunter->r_CurrentTilePositionY;
            int distance = Math.Max(Math.Abs(px - hx), Math.Abs(py - hy));
            ushort land = components[tile];
            Runner.Require(land != 0 && land == components[tileApi.GetTileId(hx, hy)],
                "the obstruction made the chicken unreachable");
            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (UnitAccess.IsReallyAlive(unit) && unit->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN &&
                    unit->r_ControllableForPlayerId == local)
                {
                    unit->r_AliveState = AliveState.MarkedForDeletion;
                }
            }
            int preyId = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(local, local, px, py,
                tileApi.GetTileHeight(tile), eChimps.CHIMP_TYPE_CHICKEN));
            Runner.Require(preyId > 0, "the chicken was not created");
            BeginHuntTrace(hunter, preyId);
            Runner.Require(UnitAccess.TryGetById(preyId, out GameUnit* chicken, out _) &&
                chicken->r_ControllableForPlayerId == local, "the fixture chicken has the wrong owner");
            Runner.Require(corridorStartedBlocked, "the test obstruction did not block initial sight");
            AcceptLog.Info("HUNT_BLOCKED_CHICKEN hunter=" + corridorHunter + " prey=" + corridorPrey +
                "/" + corridorPreyGlobal + " hunterTile=" + hx + "," + hy + " at=" + px + "," + py +
                " granary=" + granaryX + "," + granaryY + " distance=" + distance + " land=" + land +
                " obstruction=" + obstacle + " direction=" + dx + "," + dy + " rotation=" + rotation);
        }

        private static IEnumerable<Wait> CloserSameRabbit(Step step)
        {
            int local = Local();
            lock (Engine()) PlaceFarApproachPrey(local);
            yield return new Until("the Hunter approaches the fixture rabbit", () =>
            {
                lock (Engine()) return corridorHasFirstApproach;
            });

            int preyX, preyY;
            lock (Engine()) (preyX, preyY) = MoveSameRabbitCloser();
            int hunterX = 0, hunterY = 0, nearAt = 0;
            yield return new Until("the same rabbit walks into the Hunter's attack range", () =>
            {
                lock (Engine()) return TryObserveCloserPosition(preyX, preyY, out hunterX, out hunterY, out nearAt);
            });
            yield return new Until("the Hunter aims at the same closer rabbit early", () =>
            {
                lock (Engine()) return TryObserveCloserAim(hunterX, hunterY, nearAt);
            });
            AcceptLog.Info("HUNT_CLOSER_AIM prey=" + corridorPrey + "/" + corridorPreyGlobal +
                " aimTicks=" + (corridorFirstAimTick - nearAt));
        }

        private static void PlaceFarApproachPrey(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter for the moving prey fixture");
            int hx = hunter->r_CurrentTilePositionX, hy = hunter->r_CurrentTilePositionY;
            var tileApi = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            ushort land = components[tileApi.GetTileId(hx, hy)];
            foreach ((int dx, int dy) in ApproachDirections)
            {
                int x = hx + ApproachFixtureDistance * dx, y = hy + ApproachFixtureDistance * dy;
                int nearX = hx + CloserFixtureDistance * dx, nearY = hy + CloserFixtureDistance * dy;
                if (!IsHuntingFixtureTile(tileApi, components, land, x, y) ||
                    !IsHuntingFixtureTile(tileApi, components, land, nearX, nearY) ||
                    !HasSightFromHunter(hunter, nearX, nearY))
                    continue;
                SpawnPreyAt("rabbit", x, y);
                BeginHuntTrace(hunter, deer[deer.Count - 1]);
                return;
            }
            throw new InvalidOperationException("no connected site for the far approach fixture");
        }

        private static (int, int) MoveSameRabbitCloser()
        {
            Runner.Require(UnitAccess.TryGetById(corridorHunter, out GameUnit* hunter, out _) && hunter != null &&
                hunter->r_GlobalId == corridorHunterGlobal && hunter->r_AIState == 1, "original approach is inactive");
            Runner.Require(UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) && prey != null &&
                prey->r_GlobalId == corridorPreyGlobal && prey->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT,
                "the fixture rabbit was replaced");
            int hx = hunter->r_CurrentTilePositionX, hy = hunter->r_CurrentTilePositionY;
            int oldX = hunter->r_TargetTilePositionX2, oldY = hunter->r_TargetTilePositionY2;
            int dx = oldX - hx, dy = oldY - hy;
            int previousDistance = Math.Abs(dx) + Math.Abs(dy);
            Runner.Require(previousDistance >= 44, "the Hunter had already walked too far for the trigger");
            int x = hx + dx * CloserFixtureDistance / previousDistance;
            int y = hy + dy * CloserFixtureDistance / previousDistance;
            var tileApi = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            ushort land = components[tileApi.GetTileId(hx, hy)];
            Runner.Require(IsHuntingFixtureTile(tileApi, components, land, x, y), "nearer rabbit site is not usable");
            int preyX = prey->r_CurrentTilePositionX, preyY = prey->r_CurrentTilePositionY;
            corridorReturnedBeforeArrow = false;
            GameUnitManagerAPI.Instance.MoveToTile(corridorPrey, x, y);
            AcceptLog.Info("HUNT_CLOSER_ORDER prey=" + corridorPrey + "/" + corridorPreyGlobal +
                " start=" + preyX + "," + preyY + " destination=" + x + "," + y);
            return (preyX, preyY);
        }

        private static bool TryObserveCloserPosition(int preyX, int preyY, out int hunterX, out int hunterY, out int tick)
        {
            hunterX = hunterY = tick = 0;
            Runner.Require(!corridorReturnedBeforeArrow, "the Hunter gave up while the rabbit approached");
            Runner.Require(UnitAccess.TryGetById(corridorHunter, out GameUnit* hunter, out _) && hunter != null &&
                hunter->r_GlobalId == corridorHunterGlobal && *(uint*)((byte*)hunter + HunterTargetGlobalOffset) == corridorPreyGlobal,
                "the Hunter changed the acquired target before the rabbit approached");
            Runner.Require(UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) && prey != null &&
                prey->r_GlobalId == corridorPreyGlobal && UnitAccess.IsReallyAlive(prey), "the fixture prey disappeared");
            hunterX = hunter->r_CurrentTilePositionX;
            hunterY = hunter->r_CurrentTilePositionY;
            int moved = Math.Abs(preyX - prey->r_CurrentTilePositionX) + Math.Abs(preyY - prey->r_CurrentTilePositionY);
            int distance = Math.Abs(hunterX - prey->r_CurrentTilePositionX) + Math.Abs(hunterY - prey->r_CurrentTilePositionY);
            return moved >= 12 && distance <= 28 &&
                LineOfSight.TryEvaluateUnits(in *hunter, in *prey, LineOfSightRoutine.Directional, out int forward, out _) && forward > 0 &&
                LineOfSight.TryEvaluateUnits(in *prey, LineOfSight.TargetHeight, in *hunter, LineOfSight.ViewerEyeHeight,
                    LineOfSightRoutine.Directional, out int reverse, out _) && reverse > 0 &&
                SimulationTickEvents.TryGetCurrentTick(out tick);
        }

        private static bool TryObserveCloserAim(int initialX, int initialY, int movedAt)
        {
            Runner.Require(!corridorReturnedBeforeArrow, "the Hunter returned to the hut before aiming at the closer prey");
            Runner.Require(UnitAccess.TryGetById(corridorHunter, out GameUnit* hunter, out _) && hunter != null &&
                hunter->r_GlobalId == corridorHunterGlobal, "the fixture Hunter was replaced");
            Runner.Require(*(uint*)((byte*)hunter + HunterTargetGlobalOffset) == corridorPreyGlobal,
                "the Hunter abandoned the moved prey's identity before aiming");
            if (corridorFirstAimTick != 0)
            {
                Runner.Require(corridorFirstAimTick - movedAt <= 100,
                    "the Hunter did not start aiming within 100 map ticks");
                Runner.Require(Math.Abs(hunter->r_CurrentTilePositionX - initialX) +
                    Math.Abs(hunter->r_CurrentTilePositionY - initialY) <= 2,
                    "the Hunter kept following the old route after the rabbit came closer");
                return true;
            }

            Runner.Require(UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) && prey != null &&
                prey->r_GlobalId == corridorPreyGlobal && UnitAccess.IsReallyAlive(prey), "the fixture prey disappeared before aim");
            Runner.Require(SimulationTickEvents.TryGetCurrentTick(out int tick) && tick - movedAt <= 100,
                "the Hunter did not aim within 100 map ticks of the prey approaching");
            return false;
        }

        private static bool HasSightFromHunter(GameUnit* hunter, int x, int y)
        {
            return LineOfSight.TryGetUnitPoint(in *hunter, LineOfSight.ViewerEyeHeight, out SightPoint viewer) &&
                LineOfSight.TryGetTileViewpoint(x, y, LineOfSight.TargetHeight, out SightPoint target) &&
                LineOfSight.TryEvaluate(viewer, target, LineOfSightRoutine.Directional, out int forward, out _) && forward > 0 &&
                LineOfSight.TryEvaluate(target, viewer, LineOfSightRoutine.Directional, out int reverse, out _) && reverse > 0;
        }

        private static bool IsHuntingFixtureTile(GameTileManagerAPI tiles, Span<ushort> landGrid, ushort land, int x, int y)
        {
            if (land == 0 || !tiles.IsTileInsideMapBounds(x, y))
                return false;
            int tile = tiles.GetTileId(x, y);
            return (uint)tile < (uint)landGrid.Length && landGrid[tile] == land && tiles.IsTileWalkableAndUnoccupied(tile);
        }
    }
}
