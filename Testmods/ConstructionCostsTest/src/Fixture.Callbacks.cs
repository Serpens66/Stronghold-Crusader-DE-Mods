using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared.Automation;
using APIShared.Buildings;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        private enum BonusSource { GoldPre, GoldPost, WoodPre }

        // A real public resource callback grants an independent bonus. No production
        // internals are changed: only the normal paid build and native demolition run.
        private IEnumerable<AutomationWait> BonusRefund(AutomationRun run, BonusSource source)
        {
            run.InSimulation(run.CurrentMission, () => {
                Require(ReceiptCount() == 0, "Bonus fixture requires a clean payment history");
                extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, -40 } });
                var players = GamePlayerManagerAPI.Instance;
                Require(players.SetPlayerGold(owner, 1000), "Unable to prepare rebate fixture funds");
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out wellId, out wellGlobal, out wellX, out wellY), "No legal well for nested bonus fixture");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 1020 && ReceiptCount() == 1,
                    "Bonus fixture must actually pay base20 and receive rebate40");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;

            int id = wellId, actor = owner, before = 0, after = 0, pre = 0, post = 0, bonusDelta = 0;
            uint generation = wellGlobal;
            bool armed = false, granted = false;
            string failure = null;
            int percentage = run.InSimulation(run.CurrentMission, () => RefundPercent(well));
            float multiplier = run.InSimulation(run.CurrentMission, () => (float)GameBuildingManagerAPI.Instance.GoldRefundMultiplier);
            int original = ReferenceMaterialRefund(20, multiplier, percentage);
            int offset = ReferenceMaterialRefund(-40, multiplier, percentage);
            int expected = 10 + Math.Max(0, original + offset);
            using (BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(args => {
                if (args.BuildingId != id || args.PlayerId != actor || !MatchesBuilding(id, generation, actor)) return;
                if (args.Phase == EventHookPhase.Pre)
                { pre++; armed = true; before = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD); }
                else armed = false;
            }))
            using (BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(args => {
                if (args.Phase == EventHookPhase.Post && args.BuildingId == id && pre == 1 && MatchesBuilding(id, generation, actor))
                { post++; after = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD); }
            }))
            using (PlayerR3EventHooks.OnPlayerAddResource.Observable.Subscribe(args => {
                bool phaseMatches = source == BonusSource.GoldPost ? args.Phase == EventHookPhase.Post : args.Phase == EventHookPhase.Pre;
                eGoods trigger = source == BonusSource.WoodPre ? eGoods.STORED_WOOD_PLANKS : eGoods.STORED_GOLD;
                if (!armed || granted || args.PlayerId != actor || args.Good != trigger || !phaseMatches) return;
                granted = true; // The bonus recursively raises this same publisher.
                try
                {
                    int balance = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                    if (!GamePlayerManagerAPI.Instance.TryAddGood(actor, eGoods.STORED_GOLD, 10)) failure = "Independent bonus rejected";
                    bonusDelta = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD) - balance;
                }
                catch (Exception error) { failure = error.ToString(); }
            }))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                EngineInterface.DeleteBuilding(wellX + 1, wellY + 1, actor, true, 1);
                yield return AutomationWait.Until("real demolition with independent " + source + " bonus", () => run.InSimulation(run.CurrentMission, () => Removed(id, generation)));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            Require(failure == null && granted && bonusDelta == 10, "The independent bonus must retain all10 gold: " + source + " actual=" + bonusDelta + " " + failure);
            Require(pre == 1 && post == 1 && after - before == expected && ReceiptCount() == 0,
                "Only the original base refund may be offset: " + source + " expected=" + expected + " actual=" + (after - before) + " events=" + pre + "/" + post);
            log("PASS independent-bonus source=" + source + " bonus10=" + bonusDelta + " baseRefund=" + original + " offset=" + offset + " atomicTotal=" + (after - before) + " receipts=0");
            extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
        }

        private IEnumerable<AutomationWait> BrazierCallbackSpawns(AutomationRun run)
        {
            Require(ReceiptCount() == 0, "Brazier callback fixture requires no existing receipt");
            bool insideRaw = false;
            int rawPre = 0, rawPost = 0, rawLater = 0, paid = 0, before = 0, charged = 0;
            string failure = null;
            ProjectileSpawnEventArgs captured = null;
            brazierSlot = 0;
            using (ProjectileR3EventHooks.OnProjectileSpawn.Observable.Subscribe(args => {
                if (insideRaw || args.PlayerSourceId != owner || args.ProjectileType != ProjectileType.Brazier) return;
                try
                {
                    if (args.Phase == EventHookPhase.Pre)
                    {
                        before = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                        captured = args;
                        insideRaw = true;
                        try { rawPre = RawBrazier(args); } finally { insideRaw = false; }
                        if (GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) != before)
                            failure = "Raw Brazier in Pre was charged";
                    }
                    else if (args.ReturnValue >= 25 && args.ReturnValue <= int.MaxValue)
                    {
                        paid = (int)args.ReturnValue;
                        charged = before - GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                        int balance = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                        insideRaw = true;
                        try { rawPost = RawBrazier(args); } finally { insideRaw = false; }
                        if (GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) != balance)
                            failure = "Raw Brazier in Post was charged";
                    }
                }
                catch (Exception error) { failure = error.ToString(); }
            }))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                Require(EngineInterface.StartMapperItem((int)eMappers.MAPPER_BRAZIER) >= 0, "Brazier mapper refused");
                int center = BuildingScales.GetScale(eMappers.MAPPER_TOWER4) / 2;
                EngineInterface.PlaceMapperItem((int)eMappers.MAPPER_BRAZIER, towerX + center, towerY + center, 0, owner, true, false, 1);
                yield return AutomationWait.Until("paid outer Brazier and raw callback spawns", () => paid >= 25 || failure != null);
                Require(failure == null, failure);
                brazierSlot = paid;
                yield return AutomationWait.Until("paid Brazier initialized", () => run.InSimulation(run.CurrentMission, BrazierReady));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            run.InSimulation(run.CurrentMission, () => {
                Require(captured != null && rawPre >= 25 && rawPost >= 25 && paid >= 25 && rawPre != rawPost && paid != rawPre && paid != rawPost,
                    "Three separate real projectile identities are required");
                Require(charged == 12 && ReceiptCount() == 1, "Only the actual outer Chore69 must charge12 and record one receipt");
                ReadBrazierIdentity();
                Require(HasReceipt(new ConstructionObjectIdentity(ConstructionObjectKind.Decoration, paid, brazierGlobal)), "Receipt must belong to the actual paid projectile");
                int balance = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                rawLater = RawBrazier(captured);
                Require(rawLater >= 25 && rawLater != paid && GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) == balance && ReceiptCount() == 1,
                    "A later raw spawn after the real command must remain free and must not acquire a receipt");
                log("PASS Brazier callback rawPre=" + rawPre + " rawPost=" + rawPost + " rawLater=" + rawLater + " paid=" + paid + ":" + brazierGlobal + " actualCharge=" + charged + " receipts=1");
                return true;
            });
        }

        private static int RawBrazier(ProjectileSpawnEventArgs args)
        {
            long slot = GameProjectileManagerAPI.Instance.CreateProjectile(args.SourceUnitId, args.PlayerSourceId,
                args.SourceWorldTileX, args.SourceWorldTileY, args.SourceElevation,
                args.TargetWorldTileX, args.TargetWorldTileY, args.TargetElevation, ProjectileType.Brazier, args.AttackedUnitId);
            Require(slot >= 25 && slot <= int.MaxValue, "Public raw projectile API refused fixture Brazier");
            return (int)slot;
        }

        private bool HasReceipt(ConstructionObjectIdentity identity)
        {
            object ledger = typeof(HumanConstructionCostRegistration).GetField("Ledger", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(extras);
            return (bool)ledger.GetType().GetMethod("Contains", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(ledger, new object[] { identity });
        }
    }
}
