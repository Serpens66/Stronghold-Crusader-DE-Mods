using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Simulation;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static unsafe partial class HuntSteps
    {
        private const int StressMapSize = 800;
        private const int StressHutSpacing = 7;
        private const int StressMaximumRadius = 350;
        private const int ResidentsPerHovel = 6;
        private const int StressPreyPerHut = 3;
        private static readonly Dictionary<int, (int X, int Y, int WorkerX, int WorkerY)> stressFixtureHuts =
            new Dictionary<int, (int X, int Y, int WorkerX, int WorkerY)>();
        private static readonly HashSet<uint> stressHuntersWithPrey = new HashSet<uint>();
        private static int stressFixtureOwner;

        private static void RegisterHuntStress800Steps()
        {
            Runner.Register("Hunt.PrepareStress800", PrepareStress800);
            Runner.Register("Hunt.MeasureStress800", MeasureStress800);
        }

        private static IEnumerable<Wait> PrepareStress800(Step step)
        {
            int owner = step.Int(1, 1), requested = step.Int(0);
            int ready = 0, lastReported = -1, firstTick = -1, lastReportTick = 0;
            int minimumWorkers = MinimumStressWorkers(requested);
            lock (Engine())
            {
                stressFixtureOwner = 0;
                Runner.Require(PeaceTimeState.TryGetActive(out bool peace) && peace,
                    "the 800-by-800 stress fixture requires active peace time");
                BuildStressHuts(owner, requested);
            }
            yield return new Until("new Hunter's Huts have live workers", () =>
            {
                lock (Engine())
                {
                    ready = CountStressHunters(owner, trackPrey: false, out _);
                    if (SimulationTickEvents.TryGetCurrentTick(out int tick))
                    {
                        if (firstTick < 0)
                            firstTick = tick;
                        if (lastReported < 0 || (ready != lastReported && tick - lastReportTick >= 200))
                        {
                            LogStressWorkforce(owner, tick, ready, minimumWorkers);
                            lastReportTick = tick;
                            lastReported = ready;
                        }
                        Runner.Require(tick - firstTick < 2400 || ready >= minimumWorkers,
                            "only " + ready + " test huts received a live Hunter");
                    }
                    return ready >= minimumWorkers;
                }
            });
            lock (Engine())
            {
                SpawnStressPrey();
                stressFixtureOwner = owner;
            }
            AcceptLog.Info("HUNT_STRESS800_READY mapSize=" + StressMapSize + " owner=" + owner + " hunters=" + ready);
        }

        private static void BuildStressHuts(int local, int requested)
        {
            Runner.Require(requested >= 1 && requested <= 500, "the fixture takes 1 to 500 huts");
            Runner.Require(local >= 1 && local <= GamePlayerManagerAPI.MAX_PLAYERS, "invalid stress fixture player");
            var tiles = GameTileManagerAPI.Instance;
            Runner.Require(tiles.GetCurrentMapSize() == StressMapSize, "stress fixture requires a live 800-by-800 map");
            var players = GamePlayerManagerAPI.Instance;
            Runner.Require(UnitAccess.TryGetById(players.GetLordUnitId(local), out GameUnit* lord, out _) &&
                UnitAccess.IsReallyAlive(lord) && lord->r_ControllableForPlayerId == local,
                "the stress fixture player has no living Lord: " + local);
            var door = players.GetPlayerKeepDoorPosition(local);
            Runner.Require(tiles.IsTileInsideMapBounds(door.X, door.Y), "the stress fixture keep door is outside the map");
            stressFixtureHuts.Clear();
            var buildings = GameBuildingManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance;
            var manager = buildings.GetBuildingManager();
            Runner.Require(!manager.IsNull, "no building manager for the stress fixture");
            int occupiedUnits = 0;
            var unitArray = units.GetUnitArray();
            for (int index = 0; index < unitArray.Length; index++)
                if (unitArray.GetValuePointer(index)->r_AliveState != AliveState.None)
                    occupiedUnits++;
            int freeUnitRecords = unitArray.Length - occupiedUnits;
            uint freeBuildings = manager.Pointer->FreeBuildings;
            AcceptLog.Info("HUNT_STRESS800_CAPACITY owner=" + local + " freeBuildings=" + freeBuildings +
                " occupiedUnits=" + occupiedUnits + " freePhysicalUnitRecords=" + freeUnitRecords);
            int requiredHovels = (requested + ResidentsPerHovel - 1) / ResidentsPerHovel;
            Runner.Require(freeBuildings >= requested + requiredHovels, "the map has too few free building records");
            Runner.Require(freeUnitRecords >= requested * (1 + StressPreyPerHut),
                "the map has too few physical unit records for workers and deer");
            int scale = BuildingScales.GetScale(eMappers.MAPPER_HUNTER);
            Runner.Require(scale == 3, "the Hunter's Hut footprint changed");
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int keepTile = tiles.GetTileId(door.X, door.Y);
            Runner.Require((uint)keepTile < (uint)components.Length && components[keepTile] != 0,
                "the test owner's keep has no accessible path component");
            ushort keepLand = components[keepTile];
            int hovelScale = BuildingScales.GetScale(eMappers.MAPPER_HOVEL);
            int hovels = 0, candidates = 0, attempts = 0, peasants = 0, rejectSamples = 0;
            eStructs expectedType = eStructs.STRUCT_HOVEL;
            int captureX = -1, captureY = -1, capturedId = 0;
            using (BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post && args.PlayerId == local &&
                    args.Building == expectedType &&
                    args.TileX == captureX && args.TileY == captureY &&
                    args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
                    capturedId = (int)args.ReturnValue;
            }))
            {
                foreach ((int x, int y) in StressHutSites(door.X, door.Y))
                {
                    if (hovels >= requiredHovels)
                        break;
                    if (!IsStressHutSite(tiles, components, keepLand, hovelScale, x, y,
                            x + hovelScale, y + 1, out _))
                        continue;

                    captureX = x;
                    captureY = y;
                    capturedId = 0;
                    buildings.CreatePrefab(local, x, y, eMappers.MAPPER_HOVEL, hovelScale,
                        AutomaticSpriteVariation, bIsFree: true, bypassPlacementRules: true);
                    if (BuildingAccess.TryGetById(capturedId, out GameBuilding* hovel, out _) && hovel != null &&
                        hovel->r_BuildingType == eStructs.STRUCT_HOVEL && hovel->r_PlayerIdOwner == local)
                        hovels++;
                }
                AcceptLog.Info("HUNT_STRESS800_HOUSING owner=" + local + " hovels=" + hovels +
                    " placesAtSixPerHovel=" + (hovels * ResidentsPerHovel));
                Runner.Require(hovels == requiredHovels, "the map has too few connected hovel sites");

                expectedType = eStructs.STRUCT_HUNTERS_HUT;
                foreach ((int x, int y) in StressHutSites(door.X, door.Y))
                {
                    if (stressFixtureHuts.Count >= requested)
                        break;
                    candidates++;
                    int workerX = x + scale, workerY = y + 1;
                    if (!IsStressHutSite(tiles, components, keepLand, scale, x, y, workerX, workerY, out int workerTile))
                        continue;

                    attempts++;
                    captureX = x;
                    captureY = y;
                    capturedId = 0;
                    long nativeResult = buildings.CreatePrefab(local, x, y, eMappers.MAPPER_HUNTER, scale,
                        AutomaticSpriteVariation, bIsFree: true, bypassPlacementRules: true);
                    int id = capturedId;
                    captureX = captureY = -1;
                    if (!BuildingAccess.TryGetById(id, out GameBuilding* hut, out _) || hut == null ||
                        hut->r_BuildingType != eStructs.STRUCT_HUNTERS_HUT || hut->r_PlayerIdOwner != local ||
                        (hut->r_AliveState != AliveState.IsAlive && hut->r_AliveState != AliveState.NeedsInit) ||
                        hut->r_TilePositionXBegin != x || hut->r_TilePositionYBegin != y ||
                        stressFixtureHuts.ContainsKey(id))
                    {
                        if (rejectSamples++ < 5)
                        {
                            AcceptLog.Info("HUNT_STRESS800_SITE at=" + x + "," + y + " nativeRawResult=" + nativeResult +
                                " blocked=" + tiles.TileManager.IsPlacementBlocked + " eventBuilding=" + id +
                                " gridBuilding=" + tiles.GetTileBuildingId(tiles.GetTileId(x, y)));
                        }
                        continue;
                    }
                    stressFixtureHuts.Add(id, (x, y, workerX, workerY));
                    if (units.CreateUnitLocal(local, local, workerX, workerY, tiles.GetTileHeight(workerTile),
                        eChimps.CHIMP_TYPE_PEASANT) > 0)
                    {
                        peasants++;
                    }
                }
            }
            AcceptLog.Info("HUNT_STRESS800_PLACEMENT owner=" + local + " door=" + door.X + "," + door.Y +
                " candidates=" + candidates + " eligible=" + attempts + " confirmed=" + stressFixtureHuts.Count +
                " peasants=" + peasants);
            Runner.Require(stressFixtureHuts.Count == requested, "the map did not supply enough connected, buildable sites");
            Runner.Require(peasants == requested, "the save did not have enough unit slots for the new workers");
        }

        private static IEnumerable<(int X, int Y)> StressHutSites(int centerX, int centerY)
        {
            for (int radius = StressHutSpacing; radius <= StressMaximumRadius; radius += StressHutSpacing)
            {
                for (int offset = -radius; offset <= radius; offset += StressHutSpacing)
                {
                    yield return (centerX + offset, centerY - radius);
                    yield return (centerX + offset, centerY + radius);
                }
                for (int offset = -radius + StressHutSpacing; offset < radius; offset += StressHutSpacing)
                {
                    yield return (centerX - radius, centerY + offset);
                    yield return (centerX + radius, centerY + offset);
                }
            }
        }

        private static bool IsStressHutSite(GameTileManagerAPI tiles, Span<ushort> components, ushort keepLand, int scale,
            int x, int y, int workerX, int workerY, out int workerTile)
        {
            workerTile = 0;
            if (!tiles.IsTileInsideMapBounds(workerX, workerY))
                return false;
            workerTile = tiles.GetTileId(workerX, workerY);
            if ((uint)workerTile >= (uint)components.Length ||
                !tiles.IsTileWalkableAndUnoccupied(workerTile) || components[workerTile] != keepLand)
                return false;

            // The actual recruitment queue also requires a route from the
            // workplace access back to the owner's keep.
            for (int row = 0; row < scale; row++)
                for (int column = 0; column < scale; column++)
                    if (!IsHuntingFixtureTile(tiles, components, keepLand, x + column, y + row))
                        return false;
            return true;
        }

        private static void LogStressWorkforce(int owner, int tick, int ready, int minimumWorkers)
        {
            var players = GamePlayerManagerAPI.Instance;
            var door = players.GetPlayerKeepDoorPosition(owner);
            int connected = 0;
            if (APIShared.Pathfinding.PathReachability.TryGetComponent(door.X, door.Y, out int keepLand) && keepLand != 0)
            {
                foreach (int hutId in stressFixtureHuts.Keys)
                {
                    if (BuildingAccess.TryGetById(hutId, out GameBuilding* hut, out _) && hut != null &&
                        APIShared.Pathfinding.PathReachability.TryGetComponent(
                            hut->r_TileAccessPositionX, hut->r_TileAccessPositionY, out int accessLand) && accessLand == keepLand)
                    {
                        connected++;
                    }
                }
            }

            if (players.TryGetPlayerResourcesById(owner, out GamePlayerResources* player) && player != null)
            {
                AcceptLog.Info("HUNT_STRESS800_WORKERS tick=" + tick + " ready=" + ready + " minimum=" + minimumWorkers +
                    " sameKeepComponent=" + connected + " readyPeasants=" + player->r_ReadyPeasants +
                    " vacancies=" + player->r_VacantWorkBuildings + " housing=" + player->r_CivilianHousingSpace);
            }
        }

        private static void SpawnStressPrey()
        {
            var tiles = GameTileManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            var occupiedPreyTiles = new HashSet<int>();
            int spawned = 0;
            foreach ((int hx, int hy, int workerX, int workerY) in stressFixtureHuts.Values)
            {
                ushort land = components[tiles.GetTileId(workerX, workerY)];
                int spawnedAtHut = 0;
                for (int distance = 7; distance <= 24 && spawnedAtHut < StressPreyPerHut; distance++)
                {
                    foreach ((int dx, int dy) in ApproachDirections)
                    {
                        int x = hx + distance * dx, y = hy + distance * dy;
                        if (!IsHuntingFixtureTile(tiles, components, land, x, y))
                            continue;
                        int tile = tiles.GetTileId(x, y);
                        if (occupiedPreyTiles.Contains(tile) ||
                            units.CreateUnitLocal(0, 0, x, y, tiles.GetTileHeight(tile), eChimps.CHIMP_TYPE_DEER) <= 0)
                            continue;

                        occupiedPreyTiles.Add(tile);
                        spawned++;
                        if (++spawnedAtHut == StressPreyPerHut)
                            break;
                    }
                }
            }
            AcceptLog.Info("HUNT_STRESS800_PREY spawned=" + spawned);
            Runner.Require(spawned >= stressFixtureHuts.Count * (StressPreyPerHut - 1),
                "the map did not supply at least two nearby prey per hut on average");
        }

        private static IEnumerable<Wait> MeasureStress800(Step step)
        {
            int ticks = step.Int(0), minimumHuntersOnPrey = step.Int(1);
            int owner = stressFixtureOwner;
            Runner.Require(owner > 0 && stressFixtureHuts.Count > 0, "the stress fixture was not prepared");
            int start, elapsed = 0, peak = 0, peakLive = 0, lastSample = 0;
            lock (Engine())
            {
                Runner.Require(SimulationTickEvents.TryGetCurrentTick(out start), "the stress fixture needs a live map tick");
                stressHuntersWithPrey.Clear();
            }
            Runner.Require(PeaceTimeState.TryGetActive(out bool peace) && peace, "peace time ended before measurement");
            AcceptLog.Info("HUNT_STRESS800_BEGIN tick=" + start);
            yield return new Until("stress measurement reached its map tick window", () =>
            {
                lock (Engine())
                {
                    if (!SimulationTickEvents.TryGetCurrentTick(out int now))
                        return false;
                    elapsed = now - start;
                    if (elapsed >= lastSample + 20)
                    {
                        lastSample = elapsed;
                        peakLive = Math.Max(peakLive, CountStressHunters(owner, trackPrey: true, out int hunting));
                        peak = Math.Max(peak, hunting);
                    }
                    return elapsed >= ticks;
                }
            });
            Runner.Require(PeaceTimeState.TryGetActive(out peace) && peace, "peace time ended during measurement");
            AcceptLog.Info("HUNT_STRESS800_END elapsedTicks=" + elapsed + " peakLive=" + peakLive +
                " peakHunting=" + peak + " distinctHunting=" + stressHuntersWithPrey.Count);
            Runner.Require(peakLive >= MinimumStressWorkers(stressFixtureHuts.Count),
                "the measurement did not see the requested working roster");
            Runner.Require(stressHuntersWithPrey.Count >= minimumHuntersOnPrey,
                "too few fixture Hunters used a live target in the measurement window");
        }

        private static int MinimumStressWorkers(int huts) => (huts * 9 + 9) / 10;

        private static int CountStressHunters(int local, bool trackPrey, out int hunting)
        {
            int count = 0;
            hunting = 0;
            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    unit->r_ControllableForPlayerId != local || !UnitAccess.IsReallyAlive(unit) ||
                    !stressFixtureHuts.ContainsKey(unit->r_LinkedProductionBuildingId))
                    continue;
                count++;
                if (!trackPrey || (unit->r_AIState != 1 && unit->r_AIState != 9 && unit->r_AIState != 10))
                    continue;
                int preyId = *(ushort*)((byte*)unit + HunterTargetIdOffset);
                if (!UnitAccess.TryGetById(preyId, out GameUnit* prey, out _) ||
                    !UnitAccess.IsReallyAlive(prey) ||
                    prey->r_GlobalId != *(uint*)((byte*)unit + HunterTargetGlobalOffset))
                    continue;
                hunting++;
                stressHuntersWithPrey.Add(unit->r_GlobalId);
            }
            return count;
        }
    }
}
