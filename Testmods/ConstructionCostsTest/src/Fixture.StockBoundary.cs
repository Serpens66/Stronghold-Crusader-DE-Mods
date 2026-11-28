using System;
using System.Collections.Generic;
using APIShared.Automation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> ExactGoldBalance(AutomationRun run)
        {
            int x = 0, y = 0;
            int scale = BuildingScales.GetScale(eMappers.MAPPER_WELL);
            var players = GamePlayerManagerAPI.Instance;
            // Vanilla refuses mapper selection while the game is paused.
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            run.InSimulation(run.CurrentMission, () => {
                owner = players.GetLocalPlayerId();
                Require(owner > 0 && !players.IsAIPlayer(owner), "Local Human required");
                FindKeep(owner, out keepX, out keepY);
                SaveAndSet(well, 20);
                Require(players.SetPlayerGold(owner, 44), "Cannot provision the terrain-control balance");
                FindPreviewSite(scale, out x, out y);
                Require(players.SetPlayerGold(owner, 31) && !PreviewWell(x, y, scale), "31 gold must block preview on the validated tile");
                Require(TryWellPrefab(x, y, scale) == 0 && players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 31 && ReceiptCount() == 0,
                    "31 gold must also reject direct construction without a charge or receipt");
                return true;
            });

            int builds = 0, spawns = 0, remaining = -1;
            using (var debit = new GoldPayoutWitness(28, owner, () => true, subtract: true))
            using (BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args => {
                if (args.Phase != EventHookPhase.Post || args.PlayerId != owner || args.TileX != x || args.TileY != y || args.ReturnValue <= 0) return;
                spawns++;
                wellId = checked((int)args.ReturnValue);
                wellGlobal = Generation(wellId);
            }))
            using (BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(args => {
                if (args.Phase != EventHookPhase.Post || args.PlayerId != owner || args.Mappers != eMappers.MAPPER_WELL || args.TileX != x || args.TileY != y) return;
                remaining = players.GetGoodAmount(owner, eGoods.STORED_GOLD);
                builds++;
            }))
            {
                run.InSimulation(run.CurrentMission, () => {
                    Require(players.SetPlayerGold(owner, 32) && PreviewWell(x, y, scale), "32 gold must allow preview on the same tile");
                    EngineInterface.PlaceMapperItem((int)eMappers.MAPPER_WELL, x + scale / 2, y + scale / 2, 0, owner, true, false, 1);
                    return true;
                });
                yield return AutomationWait.Until("regular well construction returned", () => run.InSimulation(run.CurrentMission, () => builds != 0));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                run.InSimulation(run.CurrentMission, () => {
                    Require(builds == 1 && spawns == 1 && wellGlobal != 0 && MatchesBuilding(wellId, wellGlobal, owner),
                        "32 gold must produce one real well: build calls=" + builds + " spawns=" + spawns + " remaining=" + remaining);
                    Require(debit.Failure == null && debit.ActualDelta == 32 && remaining == 0 && ReceiptCount() == 1,
                        "Chore 28 must debit 32 gold and leave one receipt: debit=" + debit.ActualDelta + " remaining=" + remaining + " failure=" + debit.Failure);
                    wellX = x; wellY = y;
                    log("PASS exact gold boundary: 31 blocked; 32 built; actual Chore 28 debit=32 remainder=0 receipts=1");
                    return true;
                });
            }
        }

        private IEnumerable<AutomationWait> ExactBreadAndLateVetoes(AutomationRun run)
        {
            int x = 0, y = 0;
            int scale = BuildingScales.GetScale(eMappers.MAPPER_WELL);
            var players = GamePlayerManagerAPI.Instance;
            var bread = eGoods.STORED_FOOD_BREAD;
            run.InSimulation(run.CurrentMission, () => {
                Require(PlaceNear(owner, eMappers.MAPPER_GRANARY, true, keepX, keepY, out _, out _, out _, out _), "Granary required to stock bread");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            run.InSimulation(run.CurrentMission, () => {
                extras.SetCosts(well, new Dictionary<eGoods, int> { { bread, 10 } });
                players.RemoveGood(owner, bread, players.GetGoodAmount(owner, bread));
                Require(players.TryAddGood(owner, bread, 10) && players.GetGoodAmount(owner, bread) == 10, "Cannot provision exactly 10 bread");
                Require(players.SetPlayerGold(owner, 20), "Cannot provision native gold");
                FindPreviewSite(scale, out x, out y);
                players.RemoveGood(owner, bread, 1);
                Require(!PreviewWell(x, y, scale) && TryWellPrefab(x, y, scale) == 0 && players.GetGoodAmount(owner, bread) == 9 && ReceiptCount() == 0,
                    "9 bread must reject preview and construction without payment");
                Require(players.TryAddGood(owner, bread, 1), "Cannot restore 10 bread");

                bool spent = false;
                using (PlayerR3EventHooks.OnPlayerSubtractResource.Observable.Subscribe(args => {
                    if (spent || args.Phase != EventHookPhase.Post || args.PlayerId != owner || args.Good != bread) return;
                    spent = true;
                    players.RemoveGood(owner, eGoods.STORED_GOLD, 1);
                }))
                    Require(TryWellPrefab(x, y, scale) == 0, "A late shortfall in a different base good must still veto the build");
                Require(spent && players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 19 && players.GetGoodAmount(owner, bread) == 10 && ReceiptCount() == 0,
                    "The bread reservation must roll back while preserving the independent gold debit");

                Require(players.SetPlayerGold(owner, 20), "Cannot restore native gold for placement veto");
                bool vetoed = false;
                using (BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(args => {
                    if (args.Phase != EventHookPhase.Pre || args.Unknown2 != 0 || args.PlayerId != owner || args.Mappers != eMappers.MAPPER_WELL || args.TileX != x || args.TileY != y) return;
                    vetoed = true;
                    args.CustomValidationRules = true;
                    args.ForceBlockPlacementState = true;
                }))
                    Require(TryWellPrefab(x, y, scale) == 0, "Another placement rule must still veto paid construction");
                Require(vetoed && players.GetGoodAmount(owner, bread) == 10 && ReceiptCount() == 0, "Foreign veto must roll back the reservation");

                Require(PreviewWell(x, y, scale) && TryWellPrefab(x, y, scale) > 0, "Exactly 10 bread must allow construction");
                Require(players.GetGoodAmount(owner, bread) == 0 && players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 0 && ReceiptCount() == 1,
                    "Successful building must debit 10 bread and 20 native gold once");
                log("PASS exact bread boundary and reservation rollback after late resource loss or foreign placement veto");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private void FindPreviewSite(int scale, out int x, out int y)
        {
            Require(EngineInterface.StartMapperItem((int)eMappers.MAPPER_WELL) >= 0, "Well mapper refused");
            for (int dy = 8; dy <= 32; dy += 4)
            for (int dx = -24; dx <= 24; dx += 4)
            {
                x = keepX + dx; y = keepY + dy;
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y) && PreviewWell(x, y, scale)) return;
            }
            throw new InvalidOperationException("No valid well preview site near the test Keep");
        }

        private bool PreviewWell(int x, int y, int scale)
        {
            bool observed = false, allowed = false;
            using (BuildingR3EventHooks.OnPlacementValidation.Observable.Subscribe(args => {
                if (args.Phase != EventHookPhase.Post || args.Unknown2 != 1 || args.PlayerId != owner || args.Mappers != eMappers.MAPPER_WELL || args.TileX != x || args.TileY != y) return;
                observed = true;
                allowed = !GameTileManagerAPI.Instance.TileManager.IsPlacementBlocked;
            }))
                EngineInterface.PlaceMapperItem((int)eMappers.MAPPER_WELL, x + scale / 2, y + scale / 2, 0, owner, true, true, 0);
            Require(observed, "The real well preview did not validate the requested tile");
            return allowed;
        }

        private int TryWellPrefab(int x, int y, int scale)
        {
            int built = 0;
            using (BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args => {
                if (args.Phase == EventHookPhase.Post && args.PlayerId == owner && args.TileX == x && args.TileY == y && args.ReturnValue > 0)
                    built = checked((int)args.ReturnValue);
            }))
                GameBuildingManagerAPI.Instance.CreatePrefab(owner, x, y, eMappers.MAPPER_WELL, scale, 0, false);
            return built;
        }
    }
}
