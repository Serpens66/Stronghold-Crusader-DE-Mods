using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace OccupiedWallDemolitionTest
{
    internal sealed partial class Fixture
    {
        private const int AppleFirstTreeState = 2;
        private const int AppleDeliveringState = 7;
        private const int AppleNextTreeState = 8;
        private int farmId, granaryId;
        private uint farmGlobal, granaryGlobal;
        private bool outboundObserved, carryingObserved;

        private sealed class AppleObservation
        {
            internal int Worker, PickupEvents, DeliveryEvents, StockIncreases, UnverifiedDrops;
            internal uint WorkerGlobal, LastGranaryStock, MaxGranaryStock;
            internal bool HadCargo;

            internal bool Delivered(Sample latest) => latest != null && latest.Global == WorkerGlobal
                && HadCargo && DeliveryEvents > 0 && StockIncreases > 0 && latest.Cargo == 0;
        }

        private IEnumerable<AutomationWait> PrepareAppleWorld(AutomationRun run)
        {
            InSimulation(run, () => {
                FindSite();
                granaryId = SpawnBuilding(eMappers.MAPPER_GRANARY, eStructs.STRUCT_GRANARY, siteX - 10, siteY, 4, out granaryGlobal);
                SpawnBuilding(eMappers.MAPPER_HOVEL, eStructs.STRUCT_HOVEL, siteX - 10, siteY + 6,
                    BuildingScales.GetScale(eMappers.MAPPER_HOVEL), out _);
                farmId = SpawnBuilding(eMappers.MAPPER_APPLEFARM, eStructs.STRUCT_APPLEFARM, siteX, siteY, 10, out farmGlobal);
                log($"WALL_WORKWORLD farm={farmId}/{farmGlobal} granary={granaryId}/{granaryGlobal} center={siteX},{siteY} setup=native-prefabs");
            });
            foreach (var wait in Advance(run, 32)) yield return wait;
        }

        private IEnumerable<AutomationWait> AppleWorker(AutomationRun run, bool carrying)
        {
            string name = carrying ? "apple-worker-carrying" : "apple-worker-outbound";
            Sample before = null;
            var work = new AppleObservation();
            int destinationX = -1, destinationY = -1;
            int elapsed = 0, nextReport = 0;
            bool ripened = carrying;
            using (UnitR3EventHooks.OnAppleFarmerPickUpApple.Observable.Subscribe(e => ObserveAppleEvent(work, e.UnitId, false)))
            using (UnitR3EventHooks.OnAppleFarmerDropOffApple.Observable.Subscribe(e => ObserveAppleEvent(work, e.UnitId, true)))
            {
                Director.instance.SetEngineFrameRate(carrying ? 100 : 200);
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                while (elapsed < (carrying ? 5000 : 11000) && before == null)
                {
                    int delay = ripened ? 6 : 192;
                    yield return run.WaitForTicks(run.CurrentMission, delay);
                    elapsed += delay;
                    InSimulation(run, () => {
                        if (!ripened || elapsed >= nextReport)
                        {
                            int ripe = CountRipeFarmTrees();
                            ripened |= ripe > 0;
                            if (elapsed >= nextReport)
                            {
                                log($"WALL_WORKER_WAIT case={name} waitedTicks>={elapsed} ripeTrees={ripe} {DescribeFarm()}");
                                nextReport = elapsed + 1024;
                            }
                        }
                        if (!ripened) return;
                        var candidate = FindAppleWorker(carrying, out destinationX, out destinationY);
                        if (candidate == null) return;
                        work.Worker = candidate.Id;
                        work.WorkerGlobal = candidate.Global;
                        work.HadCargo = candidate.Cargo > 0;
                        work.LastGranaryStock = work.MaxGranaryStock = ReadGranary();
                        PaintWall(candidate);
                        before = ReadUnit(candidate.Id, candidate.Tile);
                        Request(name, "synthetic-wall-under-real-worker-route", before, before.Tile);
                    });
                }
                Director.instance.SetEngineFrameRate(100);
                if (before == null)
                {
                    foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                    log($"WALL_COVERAGE case={name} status=unavailable reason=no-live-worker-in-required-route-state");
                    yield break;
                }
                foreach (var wait in Observe(run, name, before, before.Tile, carrying ? 480 : 768,
                    destinationX, destinationY, work)) yield return wait;
                if (carrying) carryingObserved = true;
                else outboundObserved = true;
            }
        }

        private IEnumerable<AutomationWait> VerifyCoverage(AutomationRun run)
        {
            log($"WALL_WORKER_COVERAGE outbound={outboundObserved} carrying={carryingObserved}");
            Require(outboundObserved && carryingObserved, "A natural apple-worker route was missing. See WALL_WORKER_COVERAGE.");
            yield return AutomationWait.NextFrame;
        }

        private unsafe int SpawnBuilding(eMappers mapper, eStructs type, int x, int y, int size, out uint global)
        {
            int id = 0;
            using (BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(e => {
                if (e.Phase == EventHookPhase.Post && e.PlayerId == owner && e.Building == type && e.ReturnValue > 0)
                    id = checked((int)e.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner, x, y, mapper, size, 0, true, true);
            if (!BuildingAccess.TryGetById(id, out GameBuilding* building, out _) || building == null ||
                building->r_BuildingType != type || building->r_PlayerIdOwner != owner ||
                (building->r_AliveState != AliveState.IsAlive && building->r_AliveState != AliveState.NeedsInit))
                throw new InvalidOperationException($"Missing expected native fixture building {type}");
            global = building->r_GlobalId;
            return id;
        }

        private unsafe Sample FindAppleWorker(bool carrying, out int targetX, out int targetY)
        {
            targetX = targetY = -1;
            int size = GameUnitManagerAPI.Instance.GetUnitsAsSpan().Length;
            for (int id = 1; id <= size; id++)
            {
                if (!UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !UnitAccess.IsReallyAlive(unit)
                    || unit->r_UnitChimp != eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_ControllableForPlayerId != owner
                    || unit->r_LinkedProductionBuildingId != farmId || unit->r_LinkedProductionBuildingGlobalId != farmGlobal
                    || !IsWorkerSegment(unit->r_AIState, carrying)
                    || (unit->r_CarryGoodAmount > 0) != carrying
                    || (unit->r_PathPlanStateBitFlags != 2 && unit->r_PathPlanStateBitFlags != 4)) continue;

                var sample = ReadUnit(id);
                if (sample == null || !CanObserve(sample, sample.Tile) || !CanPaint(sample.Tile) ||
                    !Tiles.IsTileInsideMapBounds(unit->r_TargetTilePositionX2, unit->r_TargetTilePositionY2) ||
                    Math.Max(Math.Abs(unit->r_TargetTilePositionX2 - sample.X), Math.Abs(unit->r_TargetTilePositionY2 - sample.Y)) < 2)
                    continue;
                targetX = unit->r_TargetTilePositionX2;
                targetY = unit->r_TargetTilePositionY2;
                return sample;
            }
            return null;
        }

        private static bool IsWorkerSegment(int state, bool carrying) => carrying
            ? state == AppleDeliveringState : state == AppleFirstTreeState || state == AppleNextTreeState;

        private unsafe int CountRipeFarmTrees()
        {
            int grown = 0;
            foreach (ref readonly var tree in GameVegetationManagerAPI.Instance.GetVegetationAsSpan())
                if (tree.r_AliveState == AliveState.IsAlive && tree.r_GrowthStage == 3 &&
                    tree.r_TilePositionX >= siteX && tree.r_TilePositionX < siteX + 10 &&
                    tree.r_TilePositionY >= siteY && tree.r_TilePositionY < siteY + 10)
                    grown++;
            return grown;
        }

        private unsafe string DescribeFarm()
        {
            if (!BuildingAccess.TryGetById(farmId, out GameBuilding* farm, out _) || farm == null || farm->r_GlobalId != farmGlobal)
                return "farm=missing";
            string first = "none";
            int size = GameUnitManagerAPI.Instance.GetUnitsAsSpan().Length;
            for (int id = 1; id <= size; id++)
            {
                if (!UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !UnitAccess.IsReallyAlive(unit)
                    || unit->r_UnitChimp != eChimps.CHIMP_TYPE_FARMER_APPLE || unit->r_LinkedProductionBuildingId != farmId)
                    continue;
                int tile = unchecked((int)unit->r_CurrentPositionTileId);
                first = $"{id}/{unit->r_GlobalId} state={unit->r_AIState} path={unit->r_PathPlanStateBitFlags} " +
                    $"cargo={unit->r_CarryGoodAmount} pos={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY} " +
                    $"target={unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2} buildable={CanPaint(tile)}";
                break;
            }
            return $"farmState={farm->r_AliveState} employees={farm->r_TotalCurrentWorkers}/{farm->r_TotalWorkersRequired} first={first}";
        }

        private unsafe uint ReadGranary() => TryGetGranary(out GameBuilding* granary) ? granary->r_FruitAmount : 0;

        private unsafe bool TryGetGranary(out GameBuilding* granary)
        {
            return BuildingAccess.TryGetById(granaryId, out granary, out _) && granary != null
                && granary->r_GlobalId == granaryGlobal && granary->r_PlayerIdOwner == owner
                && granary->r_AliveState == AliveState.IsAlive;
        }

        private unsafe void ObserveAppleEvent(AppleObservation observation, int id, bool delivery)
        {
            if (observation.Worker != id || !UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null ||
                unit->r_GlobalId != observation.WorkerGlobal) return;
            if (!delivery)
            {
                observation.PickupEvents++;
                return;
            }

            // The SE event fires after the native stock write, before carry is decremented.
            if (!TryGetGranary(out GameBuilding* granary) || unit->r_CarryGoodAmount == 0
                || Math.Abs(unit->r_CurrentTilePositionX - granary->r_TileAccessPositionX) >= 3
                || Math.Abs(unit->r_CurrentTilePositionY - granary->r_TileAccessPositionY) >= 3)
            {
                observation.UnverifiedDrops++;
                return;
            }
            observation.HadCargo = true;
            observation.DeliveryEvents++;
            uint fruit = granary->r_FruitAmount;
            if (fruit > observation.LastGranaryStock) observation.StockIncreases++;
            observation.MaxGranaryStock = Math.Max(observation.MaxGranaryStock, fruit);
        }

        private static void SampleApple(AppleObservation observation, Sample latest, uint currentStock)
        {
            if (observation == null || latest.Global != observation.WorkerGlobal) return;
            observation.HadCargo |= latest.Cargo > 0;
            observation.LastGranaryStock = currentStock;
            observation.MaxGranaryStock = Math.Max(observation.MaxGranaryStock, currentStock);
        }

        private void LogAppleOutcome(string name, AppleObservation work, Sample before, Sample after, bool reached)
        {
            bool sameWorker = after != null && after.Global == before.Global;
            log($"WALL_WORKER case={name} unit={before.Id}/{before.Global} sameWorkplace={sameWorker && before.Workplace == after.Workplace} " +
                $"cargoBefore={before.Cargo} cargoAfter={(sameWorker ? after.Cargo.ToString() : "unknown")} " +
                $"reachedOriginalTarget={reached} pickupEvents={work.PickupEvents} deliveryEvents={work.DeliveryEvents} " +
                $"stockIncreases={work.StockIncreases} unverifiedDrops={work.UnverifiedDrops} " +
                $"maxGranaryStock={work.MaxGranaryStock} verifiedDelivery={work.Delivered(after)}");
        }
    }
}
