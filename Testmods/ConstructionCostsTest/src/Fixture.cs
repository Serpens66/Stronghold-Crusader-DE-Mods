using System;
using System.Collections.Generic;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Buildings;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace ConstructionCostsTest
{
    // Explicit-request-only scenario, rooted by Plugin.active and APIShared's durable automation.
    // No OS input, custom native hook, JSON parser, polling component or teardown subscription.
    internal sealed partial class Fixture
    {
        private readonly string map, scenario;
        private readonly Guid runId;
        private readonly Action<string> log;
        private readonly Dictionary<ConstructionCostTarget, ConstructionCostValues> originals = new Dictionary<ConstructionCostTarget, ConstructionCostValues>();
        private HumanConstructionCostRegistration extras;
        private int owner, ai, keepX, keepY, wellId, wellX, wellY, towerId, towerX, towerY;
        private volatile int brazierSlot;
        private volatile int turretId;
        private uint wellGlobal, brazierGlobal;
        private int paidWellExtra = 12;
        private bool stopped;
        private ConstructionCostTarget well = ConstructionCostTarget.FromMapper(eMappers.MAPPER_WELL);
        private ConstructionCostTarget tower = ConstructionCostTarget.FromMapper(eMappers.MAPPER_TOWER4);
        private static readonly eGoods[] NativeGoods = { eGoods.STORED_WOOD_PLANKS, eGoods.STORED_STONE_BLOCKS, eGoods.STORED_IRON_INGOTS, eGoods.STORED_PITCH_RAW, eGoods.STORED_GOLD };
        internal Fixture(string map, string scenario, Guid runId, Action<string> log) { this.map = map; this.scenario = scenario; this.runId = runId; this.log = log; }
        internal static bool Owns(string value) => value == "costs" || value == "costs-extra-refund" || value == "costs-tooltip" || value == "costs-freebuild-undo" || value == "costs-refund70" || value == "costs-callbacks";
        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("typed-contract-and-registration", TimeSpan.FromSeconds(30), Register);
            if (scenario == "costs-freebuild-undo")
            {
                foreach (var step in FreeBuildSteps()) yield return step;
                yield break;
            }
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            if (scenario == "costs-refund70")
            {
                yield return new AutomationStep("precision70-paid-well", TimeSpan.FromSeconds(60), PrecisionWell, true);
                yield return new AutomationStep("precision70-native-and-human-refund", TimeSpan.FromSeconds(45), Refund, true);
                yield return new AutomationStep("restore-native-costs", TimeSpan.FromSeconds(20), Restore, true);
                yield break;
            }
            yield return new AutomationStep("natural-first-free", TimeSpan.FromSeconds(60), FirstFree, true);
            yield return new AutomationStep("ordinary-paid-free-ai-failed", TimeSpan.FromSeconds(90), Ordinary, true);
            if (scenario == "costs-callbacks")
            {
                yield return new AutomationStep("clear-original-paid-well", TimeSpan.FromSeconds(45), Refund, true);
                foreach (var source in new[] { BonusSource.GoldPre, BonusSource.GoldPost, BonusSource.WoodPre })
                {
                    var selected = source;
                    yield return new AutomationStep("independent-bonus-" + selected, TimeSpan.FromSeconds(60), run => BonusRefund(run, selected), true);
                }
                yield return new AutomationStep("brazier-callback-and-later-raw-spawns", TimeSpan.FromSeconds(60), BrazierCallbackSpawns, true);
                yield return new AutomationStep("only-paid-brazier-refund", TimeSpan.FromSeconds(60), BrazierRefund, true);
                yield return new AutomationStep("restore-native-costs", TimeSpan.FromSeconds(20), Restore, true);
                yield break;
            }
            if (scenario == "costs-tooltip")
            {
                yield return new AutomationStep("actual-rollover-presentation", TimeSpan.FromSeconds(60), run => TooltipAcceptance.Check(run, log, extras), true);
                yield return new AutomationStep("restore-native-costs", TimeSpan.FromSeconds(20), Restore, true);
                yield break;
            }
            yield return new AutomationStep("ordinary-human-refund", TimeSpan.FromSeconds(45), Refund, true);
            yield return new AutomationStep("combined-insufficient-and-gold-rebate", TimeSpan.FromSeconds(90), InsufficientAndRebate, true);
            yield return new AutomationStep("gold-rebate-refund-offset", TimeSpan.FromSeconds(45), Refund, true);
            yield return new AutomationStep("brazier-chore-payment", TimeSpan.FromSeconds(60), Brazier, true);
            yield return new AutomationStep("brazier-human-refund", TimeSpan.FromSeconds(60), BrazierRefund, true);
            yield return new AutomationStep("ballista-build-and-retire", TimeSpan.FromSeconds(90), run => Turret(run, eChimps.CHIMP_TYPE_BALLISTA), true);
            yield return new AutomationStep("mangonel-build-and-retire", TimeSpan.FromSeconds(90), run => Turret(run, eChimps.CHIMP_TYPE_MANGONEL), true);
            yield return new AutomationStep("actual-rollover-presentation", TimeSpan.FromSeconds(60), run => TooltipAcceptance.Check(run, log, extras), true);
            yield return new AutomationStep("save-load-payment-history", TimeSpan.FromSeconds(240), SaveReload);
            yield return new AutomationStep("restored-disabled-payment-refund", TimeSpan.FromSeconds(45), Refund, true);
            yield return new AutomationStep("restore-native-costs", TimeSpan.FromSeconds(20), Restore, true);
        }
        private IEnumerable<AutomationWait> Register(AutomationRun run)
        {
            Require(ConstructionCosts.IsAvailable, ConstructionCosts.UnavailableReason);
            foreach (var type in new[] { eStructs.STRUCT_MANGONEL, eStructs.STRUCT_BALLISTA })
            {
                Require(ConstructionCosts.TryGetDefault(ConstructionCostTarget.FromStructure(type), out var value, out string reason), reason);
                Require(value.Wood == 0 && value.Stone == 0 && value.Iron == 0 && value.Pitch == 0 && value.Gold == 50, "Stationary engine default cost must resolve to 50 gold: " + type);
                log("typed-default " + type + " gold=" + value.Gold);
            }
            var gates = new[] { eStructs.STRUCT_GATE_STONE2A, eStructs.STRUCT_GATE_STONE2B };
            foreach (var alias in gates)
                Require(ConstructionCostTarget.FromStructure(alias) == ConstructionCostTarget.FromMapper(eMappers.MAPPER_GATE_MAIN), "Gate aliases must coalesce");
            Require(!ConstructionCosts.TrySetCurrent(ConstructionCostTarget.FromStructure(eStructs.STRUCT_BRAZIER), eGoods.STORED_GOLD, 12, out _), "Brazier must never write a Vanilla cost row");
            Require(HumanConstructionCosts.TryRegister(Plugin.Guid, "acceptance", "construction-acceptance", out extras, out string registerReason), registerReason);
            extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, scenario == "costs-refund70" ? 10 : 12 } });
            extras.SetCosts(ConstructionCostTarget.FromMapper(eMappers.MAPPER_WOODSMAN), new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
            extras.SetCosts(ConstructionCostTarget.FromMapper(eMappers.MAPPER_BRAZIER), new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
            extras.SetCosts(ConstructionCostTarget.FromStructure(eStructs.STRUCT_BALLISTA), new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
            extras.SetCosts(ConstructionCostTarget.FromStructure(eStructs.STRUCT_MANGONEL), new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
            extras.SetActive(true);
            var buildingPlugin = BepInEx.Bootstrap.Chainloader.PluginInfos["BuildingCosts_Serp"].Instance;
            object buildingSettings = buildingPlugin.GetType().GetProperty("Settings").GetValue(buildingPlugin);
            buildingSettings.GetType().GetProperty("EnableMod").SetValue(buildingSettings, true);
            var humanSettings = buildingSettings.GetType().GetProperty("HumanExtraBuildingCosts");
            humanSettings.SetValue(buildingSettings, string.Empty);
            var normalized = (Dictionary<eMappers, IReadOnlyDictionary<eGoods, int>>)buildingSettings.GetType().GetMethod("ParseHumanExtraBuildingCosts").Invoke(buildingSettings, null);
            foreach (var row in normalized.Values) foreach (int amount in row.Values)
                Require(amount == 0, "Unable to isolate the main mod's saved Human extras before mission");
            log("main mod Human extras explicitly empty; generic acceptance registration owns tested surcharge; runner must restore settings");
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("ExtraFeatures_Serp", out var featurePlugin))
            {
                object settings = featurePlugin.Instance.GetType().GetProperty("Settings").GetValue(featurePlugin.Instance);
                bool customRefund = scenario == "costs-extra-refund" || scenario == "costs-refund70";
                int refundPercent = scenario == "costs-refund70" ? 70 : 75;
                settings.GetType().GetProperty("EnableMod").SetValue(settings, customRefund);
                if (customRefund) settings.GetType().GetProperty("GoldRefundPercent").SetValue(settings, refundPercent);
                log("ExtraFeatures enabled=" + customRefund + " before mission; custom GoldRefundPercent=" + refundPercent + "; runner must restore settings");
            }
            else Require(scenario != "costs-extra-refund" && scenario != "costs-refund70", "ExtraFeatures is required for custom-refund scenario");
            yield break;
        }
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT })) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
        private IEnumerable<AutomationWait> Ordinary(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                var players = GamePlayerManagerAPI.Instance;
                owner = players.GetLocalPlayerId();
                Require(owner > 0 && !players.IsAIPlayer(owner), "Local Human required");
                FindKeep(owner, out keepX, out keepY);
                for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                {
                    bool isAi = players.IsAIPlayer(player);
                    bool keep = TryFindKeep(player, out _, out _);
                    log("control player=" + player + " isAI=" + isAi + " lord=" + players.GetAILord(player) +
                        " lordSlot=" + players.GetLordUnitId(player) + " lordGlobal=" + players.GetLordUnitGlobalId(player) + " keep=" + keep);
                    if (isAi && keep && ai == 0) ai = player;
                }
                Require(ai > 0, "AI control with a real active Keep required; see roster diagnostics");
                SaveAndSet(well, 20); SaveAndSet(tower, 0);
                SaveAndSet(ConstructionCostTarget.FromStructure(eStructs.STRUCT_BALLISTA), 50);
                SaveAndSet(ConstructionCostTarget.FromStructure(eStructs.STRUCT_MANGONEL), 50);
                players.AddPlayerGold(owner, 1000); players.AddPlayerGold(ai, 1000);
                int gold = players.GetGoodAmount(owner, eGoods.STORED_GOLD);
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out wellId, out wellGlobal, out wellX, out wellY), "No legal paid well placement");
                Require(gold - players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 32, "Paid well must charge 20 base + 12 Human gold exactly once");
                Require(ReceiptCount() == 1, "Paid well needs one persistent receipt");
                gold = players.GetGoodAmount(owner, eGoods.STORED_GOLD);
                GameBuildingManagerAPI.Instance.CreatePrefab(owner, wellX, wellY, eMappers.MAPPER_WELL, BuildingScales.GetScale(eMappers.MAPPER_WELL), 0, false);
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == gold && ReceiptCount() == 1, "Failed occupied placement must refund its reservation and create no receipt");
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, true, keepX, keepY, out _, out _, out _, out _), "No legal free well placement");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == gold && ReceiptCount() == 1, "Free placement must not charge or create a receipt");
                FindKeep(ai, out int ax, out int ay);
                int aiGold = players.GetGoodAmount(ai, eGoods.STORED_GOLD);
                Require(PlaceNear(ai, eMappers.MAPPER_WELL, false, ax, ay, out _, out _, out _, out _), "No legal AI control well placement");
                Require(aiGold - players.GetGoodAmount(ai, eGoods.STORED_GOLD) == 20 && ReceiptCount() == 1, "AI pays only base cost");
                Require(PlaceNear(owner, eMappers.MAPPER_TOWER4, true, keepX, keepY, out towerId, out _, out towerX, out towerY), "No legal large tower for Brazier placement");
                log("PASS ordinary paid=32 free=0 failed=0 AI=20 receipts=" + ReceiptCount() + " well=" + wellId + ":" + wellGlobal);
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
        private IEnumerable<AutomationWait> FirstFree(AutomationRun run)
        {
            var woodcutter = ConstructionCostTarget.FromMapper(eMappers.MAPPER_WOODSMAN);
            run.InSimulation(run.CurrentMission, () => {
                var players = GamePlayerManagerAPI.Instance;
                owner = players.GetLocalPlayerId();
                Require(owner > 0 && !players.IsAIPlayer(owner), "Local Human required for actual first-free construction");
                FindKeep(owner, out keepX, out keepY);
                SaveAndSet(woodcutter, 20);
                Require(ConstructionCosts.TrySetCurrent(woodcutter, eGoods.STORED_WOOD_PLANKS, 7, out string why), why);
                int initialWood = players.GetGoodAmount(owner, eGoods.STORED_WOOD_PLANKS);
                Require(initialWood >= 0, "Invalid initial wood balance");
                players.RemoveGood(owner, eGoods.STORED_WOOD_PLANKS, initialWood);
                Require(players.GetGoodAmount(owner, eGoods.STORED_WOOD_PLANKS) == 0, "First-free precondition needs insufficient native wood");
                players.AddPlayerGold(owner, 1000);
                Require(extras.TryGetCosts(woodcutter, out var configured) && configured[eGoods.STORED_GOLD] == 12, "First-free target requires configured positive extras");
                Require(NativeFree(woodcutter), "Fresh local map must provide Vanilla's first-free woodcutter; no native state is forced");
                Require(!HumanConstructionCosts.TryGetEffectiveCosts(woodcutter, owner, out _), "First-free quote must suppress Human extras");
                int gold = players.GetGoodAmount(owner, eGoods.STORED_GOLD);
                Require(PlaceNear(owner, eMappers.MAPPER_WOODSMAN, false, keepX, keepY, out int id, out uint global, out _, out _), "No legal first-free woodcutter placement");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == gold && ReceiptCount() == 0,
                    "Natural first-free with bIsFree=false must charge neither native20 nor Human12 and create no receipt");
                log("PASS natural-first-free explicitFree=false baseWood=7 baseGold=20 configuredExtra=12 charged=0 receipts=0 id=" + id + ":" + global);
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
        private IEnumerable<AutomationWait> InsufficientAndRebate(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                var players = GamePlayerManagerAPI.Instance;
                Require(players.SetPlayerGold(owner, 31), "Unable to prepare exact combined affordability boundary");
                Require(!PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out _, out _, out _, out _),
                    "Gold31 must reject a native20 + Human12 well despite sufficient native-only funds");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 31 && ReceiptCount() == 0,
                    "Combined insufficient placement must change neither resources nor receipts");
                extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, -7 } });
                Require(players.SetPlayerGold(owner, 19), "Unable to prepare rebate native affordability boundary");
                Require(!PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out _, out _, out _, out _),
                    "Gold19 must not use the later7 rebate to finance the native20 cost");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 19 && ReceiptCount() == 0,
                    "Failed native affordability must grant no rebate or receipt");
                Require(players.SetPlayerGold(owner, 1000), "Unable to replenish acceptance gold");
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out wellId, out wellGlobal, out wellX, out wellY), "No legal paid rebate well placement");
                Require(players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 987 && ReceiptCount() == 1,
                    "Successful native20 plus Human-7 must debit13 and record the actual rebate exactly once");
                paidWellExtra = -7;
                log("PASS combined-affordability positive31<32 rejected; negative19<20 rejected; success base20 rebate7 net13 receipts=1");
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
        private IEnumerable<AutomationWait> Refund(AutomationRun run)
        {
            int before = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
            int percent = run.InSimulation(run.CurrentMission, () => RefundPercent(well));
            float multiplier = run.InSimulation(run.CurrentMission, () => (float)GameBuildingManagerAPI.Instance.GoldRefundMultiplier);
            int expected = ReferenceMaterialRefund(20, multiplier, percent) + ReferenceMaterialRefund(paidWellExtra, multiplier, percent);
            if (scenario == "costs-extra-refund" && paidWellExtra == 12) Require(expected == 24, "ExtraFeatures 75% must apply once to base and extras");
            if (scenario == "costs-refund70") Require(paidWellExtra == 10, "70% precision fixture requires an actually paid Human10 receipt");
            int seNativeReference = ReferenceMaterialRefund(20, multiplier, percent);
            int seHumanReference = ReferenceMaterialRefund(paidWellExtra, multiplier, percent);
            log("Refund reference before command: percent=" + percent + " materialMultiplierR=" + multiplier.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " materialMultiplierBits=0x" + BitConverter.ToInt32(BitConverter.GetBytes(multiplier), 0).ToString("X8") +
                " percentageFractionR=" + (percent / 100f).ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                " percentageFractionBits=0x" + BitConverter.ToInt32(BitConverter.GetBytes(percent / 100f), 0).ToString("X8") +
                " nominalExpression=" + expected + " seStyleNative=" + seNativeReference + " seStyleHuman=" + seHumanReference + " paidExtra=" + paidWellExtra);
            int id = wellId, actor = owner, preCount = 0, refundPostCount = 0, postCount = 0, eventBefore = 0, eventNativeAfter = 0, eventAfter = 0, eventPercent = 0;
            uint global = wellGlobal;
            using (BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(args => {
                if (args.BuildingId != id || args.PlayerId != actor || !MatchesBuilding(id, global, actor)) return;
                if (args.Phase == EventHookPhase.Pre)
                {
                    preCount++; eventBefore = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                    eventPercent = args.Percentage;
                }
                else if (args.Phase == EventHookPhase.Post && preCount == 1)
                {
                    // Native refund has returned; the Human receipt is credited only
                    // after the actual C4290 removal mark, observed separately below.
                    refundPostCount++; eventNativeAfter = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                }
            }))
            using (BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(args => {
                // The installed SE's Delete event is actual C4290 mark-state3. Its Post
                // follows both native refund and the production extra-cost commit.
                if (args.Phase != EventHookPhase.Post || args.BuildingId != id || preCount != 1 || !MatchesBuilding(id, global, actor)) return;
                postCount++; eventAfter = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
            }))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                EngineInterface.DeleteBuilding(wellX + 1, wellY + 1, owner, true, 1);
                yield return AutomationWait.Until("paid well confirmed removed", () => run.InSimulation(run.CurrentMission, () => Removed(id, global)));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            run.InSimulation(run.CurrentMission, () => {
                Require(preCount == 1 && refundPostCount == 1 && postCount == 1, "Refund proof requires one matching owner/generation PreRefund, PostRefund and PostMark: " + preCount + "/" + refundPostCount + "/" + postCount);
                int actual = eventAfter - eventBefore;
                int outerDelta = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) - before;
                log("Refund observed before assertion: total=" + actual + " nativeActual=" + (eventNativeAfter - eventBefore) + " humanActual=" + (eventAfter - eventNativeAfter) +
                    " effectivePercent=" + percent + " refundEventPercent=" + eventPercent + " nominalExpression=" + expected + " seStyleNative=" + seNativeReference +
                    " seStyleHuman=" + seHumanReference + " outerDelta=" + outerDelta + " receipts=" + ReceiptCount());
                Require(actual == expected, "Refund differs: expected=" + expected + " actual=" + actual);
                Require(ReceiptCount() == 0, "Refunded receipt must be consumed");
                log("PASS refund gold=" + actual + " outerDelta=" + outerDelta + " outsideRefundDelta=" + (outerDelta - actual) + " paidExtra=" + paidWellExtra + " percent=" + percent + " materialMultiplier=" + multiplier + " receipts=0");
                if (paidWellExtra < 0)
                {
                    extras.SetCosts(well, new Dictionary<eGoods, int> { { eGoods.STORED_GOLD, 12 } });
                    paidWellExtra = 12;
                }
                return true;
            });
        }
        private IEnumerable<AutomationWait> Brazier(AutomationRun run)
        {
            brazierSlot = 0;
            int sourceGold = 0, charged = 0;
            using (SHCDESE.EventAPI.ProjectileR3EventHooks.OnProjectileSpawn.Observable.Subscribe(args => {
                if (args.PlayerSourceId != owner || args.ProjectileType != ProjectileType.Brazier) return;
                if (args.Phase == EventHookPhase.Pre) sourceGold = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                else if (args.ReturnValue >= 25 && args.ReturnValue <= int.MaxValue)
                {
                    brazierSlot = (int)args.ReturnValue;
                    charged = sourceGold - GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD);
                }
            }))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                // Native mapper selection rejects every action while Game_Paused is set.
                Require(EngineInterface.StartMapperItem((int)eMappers.MAPPER_BRAZIER) >= 0, "Brazier mapper refused while unpaused");
                yield return run.WaitForTicks(run.CurrentMission, 4);
                int center = BuildingScales.GetScale(eMappers.MAPPER_TOWER4) / 2;
                EngineInterface.PlaceMapperItem((int)eMappers.MAPPER_BRAZIER, towerX + center, towerY + center, 0, owner, true, false, 1);
                yield return AutomationWait.Until("actual Chore69 Brazier spawn", () => brazierSlot >= 25);
                yield return AutomationWait.Until("initialized Brazier", () => run.InSimulation(run.CurrentMission, BrazierReady));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            run.InSimulation(run.CurrentMission, () => {
                Require(charged == 12, "Brazier actual spawn must charge 12 Human gold, observed=" + charged);
                Require(ReceiptCount() == 1, "Brazier needs one receipt");
                ReadBrazierIdentity();
                log("PASS Brazier actual Chore69 charge=" + charged + " slot=" + brazierSlot + " global=" + brazierGlobal);
                return true;
            });
        }
        private IEnumerable<AutomationWait> Restore(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                foreach (var entry in originals) foreach (var good in NativeGoods)
                    Require(ConstructionCosts.TrySetCurrent(entry.Key, good, entry.Value.GetAmount(good), out string why), why);
                originals.Clear(); return true;
            });
            log("native cost overrides restored before automation completion");
            Stop();
            yield break;
        }
        private IEnumerable<AutomationWait> BrazierRefund(AutomationRun run)
        {
            int before = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
            var target = ConstructionCostTarget.FromMapper(eMappers.MAPPER_BRAZIER);
            int percent = run.InSimulation(run.CurrentMission, () => RefundPercent(target, 50));
            float multiplier = run.InSimulation(run.CurrentMission, () => (float)GameBuildingManagerAPI.Instance.GoldRefundMultiplier);
            int expected = ReferenceMaterialRefund(12, multiplier, percent);
            int center = BuildingScales.GetScale(eMappers.MAPPER_TOWER4) / 2;
            int slot = brazierSlot, actor = owner; uint global = brazierGlobal;
            using (var witness = new GoldPayoutWitness(97, actor, () => BrazierMarked(slot, global, actor)))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                EngineInterface.DeleteBuilding(towerX + center, towerY + center, owner, true, 1);
                yield return AutomationWait.Until("Brazier confirmed removed through ordinary demolition", () => run.InSimulation(run.CurrentMission, () => BrazierRemoved()));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                run.InSimulation(run.CurrentMission, () => {
                    Require(witness.Failure == null && witness.PreCount == 1 && witness.PostCount == 1,
                        "Brazier refund needs one accepted Gold payout inside authenticated Chore97 for its marked generation: " + witness.PreCount + "/" + witness.PostCount + " " + witness.Failure);
                    int actual = witness.ActualDelta;
                    int outerDelta = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) - before;
                    Require(actual == expected, "Brazier refund differs: expected=" + expected + " actual=" + actual);
                    Require(ReceiptCount() == 0, "Brazier receipt must be consumed exactly once");
                    log("PASS Brazier ordinary-delete refund=" + actual + " outerDelta=" + outerDelta + " outsideRefundDelta=" + (outerDelta - actual) + " percent=" + percent + " materialMultiplier=" + multiplier + " receipts=0");
                    return true;
                });
            }
        }
        private IEnumerable<AutomationWait> Turret(AutomationRun run, eChimps type)
        {
            eMappers mapper = type == eChimps.CHIMP_TYPE_BALLISTA ? eMappers.MAPPER_BALLISTA : eMappers.MAPPER_MANGONEL;
            int x = 0, y = 0;
            run.InSimulation(run.CurrentMission, () => {
                Require(PlaceNear(owner, eMappers.MAPPER_TOWER4, true, keepX, keepY, out _, out _, out x, out y), "No legal tower for " + type);
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            turretId = 0;
            int before = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
            int charged = 0;
            uint global = 0;
            using (var charge = new GoldPayoutWitness(42, owner, () => true, subtract: true))
            {
                using (UnitR3EventHooks.OnUnitCreate.Observable.Subscribe(args => {
                    if (args.Phase == EventHookPhase.Post && args.PlayerOwnerId == owner && args.UnitType == type && args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
                        turretId = (int)args.ReturnValue;
                }))
                {
                    foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                    Require(EngineInterface.StartMapperItem((int)mapper) >= 0, "Turret mapper refused while unpaused: " + mapper);
                    int center = BuildingScales.GetScale(eMappers.MAPPER_TOWER4) / 2;
                    EngineInterface.PlaceMapperItem((int)mapper, x + center, y + center, 0, owner, true, false, 1);
                    yield return AutomationWait.Until("actual Chore42 turret spawn " + type, () => turretId > 0);
                    yield return AutomationWait.Until("initialized turret " + type, () => run.InSimulation(run.CurrentMission, () => TurretAlive(turretId)));
                    foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                }
                global = run.InSimulation(run.CurrentMission, () => UnitGeneration(turretId));
                Require(charge.Failure == null && charge.PreCount == 2 && charge.PostCount == 2,
                    "Turret payment requires native and Human Gold debit pairs inside authenticated Chore42: " + charge.PreCount + "/" + charge.PostCount + " " + charge.Failure);
                charged = charge.ActualDelta;
                Require(charged == 62 && ReceiptCount() == 1, "Turret must charge 50 base + 12 Human gold exactly once: " + type + " actual=" + charged);
                int outerCharge = run.InSimulation(run.CurrentMission, () => before - GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
                log("PASS turret payment=" + type + " authenticatedChore42 gold=" + charged + " outerDebit=" + outerCharge + " outsideIncome=" + (charged - outerCharge) + " receipts=1");
            }
            int beforeDelete = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD));
            int id = turretId, actor = owner, preCount = 0, postCount = 0, eventBefore = 0, eventAfter = 0;
            using (UnitR3EventHooks.OnUnitDelete.Observable.Subscribe(args => {
                if (args.UnitId != id) return;
                if (args.Phase == EventHookPhase.Pre && MatchesUnit(id, global, actor))
                {
                    preCount++; eventBefore = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                }
                else if (args.Phase == EventHookPhase.Post && preCount == 1)
                {
                    postCount++; eventAfter = GamePlayerManagerAPI.Instance.GetGoodAmount(actor, eGoods.STORED_GOLD);
                }
            }))
            {
                // Standard Skirmish records no construction Undo. Real terminal cleanup
                // must retire paid history without issuing a demolition refund.
                Require(run.InSimulation(run.CurrentMission, () => DeleteTurret(id, global)), "Unable to mark the actual paid turret for native cleanup");
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return AutomationWait.Until("native turret cleanup completed " + type, () => run.InSimulation(run.CurrentMission, () => TurretCleared(id, global) && ReceiptCount() == 0));
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            Require(preCount == 1 && postCount == 1, "Terminal cleanup proof requires one matching identity/owner PreDelete and PostDelete: " + preCount + "/" + postCount);
            int refunded = eventAfter - eventBefore;
            int outerDelta = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_GOLD) - beforeDelete);
            Require(refunded == 0 && ReceiptCount() == 0, "Native turret cleanup must retire history without granting a demolition refund: " + type + " actual=" + refunded);
            log("PASS turret=" + type + " actual Chore42 charge=" + charged + " terminal-cleanup refund=0 outerDelta=" + outerDelta + " outsideCleanupDelta=" + (outerDelta - refunded) + " receipts=0 id=" + id + ":" + global);
        }
        internal void Stop()
        {
            if (stopped) return;
            stopped = true;
            extras?.SetActive(false); extras?.ClearCosts();
            // Finish marks the AutomationRun complete before invoking this callback. Its
            // InSimulation guard is intentionally unavailable here; the normal final step
            // restores native costs, and failure exits this isolated test process.
            log("acceptance registration disabled; remaining process-local native overrides=" + originals.Count + "; isolated test process exits");
        }
        private void SaveAndSet(ConstructionCostTarget target, int gold)
        {
            Require(ConstructionCosts.TryGetCurrent(target, out var value, out string why), why);
            originals.Add(target, value);
            foreach (var good in NativeGoods) Require(ConstructionCosts.TrySetCurrent(target, good, good == eGoods.STORED_GOLD ? gold : 0, out why), why);
        }
        private int ReceiptCount()
        {
            object ledger = typeof(HumanConstructionCostRegistration).GetField("Ledger", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(extras);
            return (int)ledger.GetType().GetProperty("Count", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(ledger);
        }
        private int RefundPercent(ConstructionCostTarget target, int nativePercent = 50) => (int)typeof(ConstructionRefundPolicies).GetMethod("Evaluate", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { target, owner, nativePercent });
        private bool NativeFree(ConstructionCostTarget target) => (bool)typeof(HumanConstructionCosts).Assembly.GetType("APIShared.Buildings.HumanConstructionRuntime")
            .GetMethod("IsFreeConstruction", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { target, owner });
        private static unsafe void FindKeep(int player, out int x, out int y)
        {
            if (TryFindKeep(player, out x, out y)) return;
            throw new InvalidOperationException("No active Keep for owner " + player);
        }
        private static unsafe bool TryFindKeep(int player, out int x, out int y)
        {
            foreach (ref GameBuilding b in GameBuildingManagerAPI.Instance.GetBuildingsAsSpan())
                if (b.r_PlayerIdOwner == player && b.r_AliveState == AliveState.IsAlive && b.r_BuildingType >= eStructs.STRUCT_KEEP_ONE && b.r_BuildingType <= eStructs.STRUCT_KEEP_FIVE)
                { x = b.r_TilePositionXBegin; y = b.r_TilePositionYBegin; return true; }
            x = y = 0; return false;
        }
        private static unsafe bool Removed(int id, uint global) => !BuildingAccess.TryGetById(id, out GameBuilding* b, out _) || b->r_GlobalId != global || b->r_AliveState == AliveState.MarkedForDeletion || b->r_AliveState == AliveState.None;
        private unsafe void ReadBrazierIdentity() { var p = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan()[brazierSlot]; Require(p.r_ProjectileType == ProjectileType.Brazier, "Expected real Brazier projectile"); brazierGlobal = p.r_GlobalId; }
        private unsafe bool BrazierReady()
        {
            var span = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            return brazierSlot >= 25 && brazierSlot < span.Length && span[brazierSlot].r_ProjectileType == ProjectileType.Brazier && span[brazierSlot].r_AliveState == AliveState.IsAlive;
        }
        private unsafe bool BrazierRemoved()
        {
            var span = GameProjectileManagerAPI.Instance.GetProjectilesAsSpan();
            if (brazierSlot >= span.Length) return true;
            ref var p = ref span[brazierSlot];
            return p.r_GlobalId != brazierGlobal || p.r_AliveState == AliveState.MarkedForDeletion || p.r_AliveState == AliveState.None;
        }
        private static bool PlaceNear(int player, eMappers mapper, bool free, int x, int y, out int id, out uint global, out int bx, out int by)
        {
            for (int dy = 8; dy <= 32; dy += 4)
            for (int dx = -24; dx <= 24; dx += 4)
            {
                if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(x + dx, y + dy)) continue;
                int captured = 0; uint generation = 0;
                using (BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(args => {
                    if (args.Phase == EventHookPhase.Post && args.PlayerId == player && args.TileX == x + dx && args.TileY == y + dy && args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue)
                    { captured = (int)args.ReturnValue; generation = Generation(captured); }
                }))
                    GameBuildingManagerAPI.Instance.CreatePrefab(player, x + dx, y + dy, mapper, BuildingScales.GetScale(mapper), 0, free);
                if (captured <= 0) continue;
                id = captured; global = generation; bx = x + dx; by = y + dy; return true;
            }
            id = 0; global = 0; bx = by = 0; return false;
        }
        private static unsafe uint Generation(int id) => BuildingAccess.TryGetById(id, out GameBuilding* b, out _) ? b->r_GlobalId : 0;
        private static unsafe bool MatchesBuilding(int id, uint global, int player) => BuildingAccess.TryGetById(id, out GameBuilding* b, out _) && b->r_GlobalId == global && b->r_PlayerIdOwner == player;
        private static unsafe uint UnitGeneration(int id) => UnitAccess.TryGetById(id, out GameUnit* unit, out _) ? unit->r_GlobalId : 0;
        private static unsafe bool MatchesUnit(int id, uint global, int actor) => UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == global && unit->r_ControllableForPlayerId == actor;
        private static unsafe bool TurretAlive(int id) => UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_AliveState == AliveState.IsAlive;
        private static unsafe bool DeleteTurret(int id, uint global)
        {
            if (global == 0 || UnitGeneration(id) != global) return false;
            return APIShared.UnitAccess.TryGetById(id, out _, out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(id);
        }
        private static unsafe bool TurretCleared(int id, uint global) => !UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit->r_GlobalId != global || unit->r_AliveState == AliveState.None;
        // Reference SE's material refund CIL: two float multiplies followed by conv.u8.
        // A signed conversion or an intervening float store changes Mono's truncation.
        private static int ReferenceMaterialRefund(int paid, float multiplier, int percentage)
        {
            float fraction = percentage / 100f;
            ulong raw = (ulong)(Math.Abs(paid) * multiplier * fraction);
            return paid < 0 ? -(int)raw : (int)raw;
        }
        private static void Require(bool success, string why) { if (!success) throw new InvalidOperationException(why); }
    }
}
