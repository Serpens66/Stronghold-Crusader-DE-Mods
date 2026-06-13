using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

// TODO: Recheck refund guard calculation once BuildingRefundEventArgs.Percentage is fixed in the Script Extender.


namespace StartConditions
{
    public sealed partial class StartConditionsRuntime
    {
        private void ApplyStartResources()
        {
            ForEachAlivePlayer(playerId =>
            {
                LogDebug("Applying start resources for player", playerId);
                ApplyStartGold(playerId);
                ReplaceStartGoods(playerId);
            });
        }

        private void ApplyStartGold(int playerId)
        {
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int setGold = isAI ? settings.SetStartGoldAI : settings.SetStartGoldHuman;
            int addGold = isAI ? settings.AddStartGoldAI : settings.AddStartGoldHuman;

            if (setGold >= 0)
            {
                GamePlayerManagerAPI.Instance.SetPlayerGold(playerId, (uint)setGold);
                LogDebug("Set gold of player", playerId, "to", setGold);
            }

            if (addGold != 0)
            {
                GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, addGold);
                LogDebug("Add gold to player", playerId, addGold);
            }
        }

        private void ReplaceStartGoods(int playerId)
        {
            Dictionary<eGoods, int> aiGoods = ParseEnumAmounts<eGoods>(settings.StartGoodsAI);
            Dictionary<eGoods, int> humanGoods = ParseEnumAmounts<eGoods>(settings.StartGoodsHuman);

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            Dictionary<eGoods, int> goods = isAI ? aiGoods : humanGoods;
            foreach (KeyValuePair<eGoods, int> entry in goods)
            {
                if (entry.Value < 0)
                    continue;

                if (!IsConfigurableStoredGood(entry.Key))
                {
                    LogDebug("Ignoring non-storage start good", entry.Key, "for player", playerId);
                    continue;
                }

                GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, entry.Key, IncomingGoodClearAmount);
                if (entry.Value > 0)
                    GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, entry.Key, entry.Value);
                LogDebug("Set incoming good", entry.Key, "to", entry.Value, "for player", playerId);
            }
        }

        private void OnGoodsyardAddGood(AddGoodToGoodsyardEventArgs args)
        {
            int playerId = GameBuildingManagerAPI.Instance.GetOwner(args.BuildingId);
            string key = BuildResourceEventKey(playerId, args.Good);
            bool reentryGuardActive = resourceAddReentryGuards.Contains(key);

            LogInfoForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood:",
                "phase", args.Phase,
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "add", args.Add,
                "buildingId", args.BuildingId,
                "buildingGlobalId", args.BuildingGlobalId,
                "capacity", args.Capacity,
                "reentryGuard", reentryGuardActive);

            if (args.Phase != EventHookPhase.Post)
                return;

            if (!args.Add)
                return;

            if (args.AddAmount <= 0)
                return;

            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return;

            PruneExpiredResourceGuards();

            bool marketGuardActive = marketBuyResourceGuards.TryGetValue(key, out ResourceEventCountGuard marketGuard);
            bool refundGuardActive = refundResourceGuards.TryGetValue(key, out ResourceEventCountGuard refundGuard);

            if (reentryGuardActive)
            {
                LogInfoForResourceEventPlayer(playerId, "OnGoodsyardAddGood ignored own TryAddGood event:", "player", playerId, "good", args.Good, "addAmount", args.AddAmount);
                return;
            }

            if (marketGuardActive)
            {
                marketGuard.RemainingAmount -= args.AddAmount;
                if (marketGuard.RemainingAmount <= 0)
                    marketBuyResourceGuards.Remove(key);

                LogInfoForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored market buy resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingMarketGuardAmount", marketGuard.RemainingAmount);
                return;
            }

            if (refundGuardActive)
            {
                refundGuard.RemainingAmount -= args.AddAmount;
                if (refundGuard.RemainingAmount <= 0)
                    refundResourceGuards.Remove(key);

                LogInfoForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood ignored building refund resource event:",
                    "player", playerId,
                    "good", args.Good,
                    "addAmount", args.AddAmount,
                    "remainingRefundGuardAmount", refundGuard.RemainingAmount);
                return;
            }

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int multiplyGoods = isAI ? settings.MultiplyGoodsGainAI : settings.MultiplyGoodsGainHuman;
            int multiplyMoney = isAI ? settings.MultiplyGoodsGainInMoneyAI : settings.MultiplyGoodsGainInMoneyHuman;
            LogInfoForResourceEventPlayer(
                playerId,
                "OnGoodsyardAddGood processing:",
                "player", playerId,
                "good", args.Good,
                "addAmount", args.AddAmount,
                "marketBuyGuard", marketGuardActive,
                "refundGuard", refundGuardActive,
                "isAI", isAI,
                "multiplyGoods", multiplyGoods,
                "multiplyMoney", multiplyMoney);

            if (multiplyGoods > 0)
            {
                int bonusAmount = args.AddAmount * multiplyGoods;
                LogInfoForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood TryAddGood bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "sourceAmount", args.AddAmount,
                    "bonusAmount", bonusAmount,
                    "multiplyGoods", multiplyGoods);
                resourceAddReentryGuards.Add(key);
                try
                {
                    GamePlayerManagerAPI.Instance.TryAddGood(playerId, args.Good, bonusAmount);
                }
                finally
                {
                    resourceAddReentryGuards.Remove(key);
                }
            }

            if (multiplyMoney > 0)
            {
                PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetTradeBasePrice(args.Good);
                int sellPricePerItem = price.SellPrice / 5;
                int money = args.AddAmount * sellPricePerItem * multiplyMoney;
                LogInfoForResourceEventPlayer(
                    playerId,
                    "OnGoodsyardAddGood money bonus:",
                    "player", playerId,
                    "good", args.Good,
                    "amount", args.AddAmount,
                    "sellPricePerItem", sellPricePerItem,
                    "money", money,
                    "multiplyMoney", multiplyMoney);
                if (money != 0)
                    GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, money);
            }
        }

        private void OnPlayerMarketInteraction(PlayerMarketInteractionEventArgs args)
        {
            string key = BuildResourceEventKey(args.PlayerId, args.Good);
            LogInfoForResourceEventPlayer(
                args.PlayerId,
                "OnPlayerMarketInteraction:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "selling", args.Selling,
                "good", args.Good,
                "shiftModifier", args.ShiftModifier,
                "skipOriginal", args.SkipOriginalFunction,
                "key", key);

            if (args.Selling)
                return;

            if (args.Phase == EventHookPhase.Pre)
            {
                PruneExpiredMarketBuyResourceGuards();
                int expectedAmount = GetMarketInteractionAmount(args);
                marketBuyResourceGuards[key] = new ResourceEventCountGuard
                {
                    RemainingAmount = expectedAmount,
                    ExpiresAt = DateTime.UtcNow + MarketBuyGuardLifetime
                };
                LogInfoForResourceEventPlayer(
                    args.PlayerId,
                    "OnPlayerMarketInteraction market buy guard added:",
                    "player", args.PlayerId,
                    "good", args.Good,
                    "key", key,
                    "expectedAmount", expectedAmount);
                return;
            }

            if (args.Phase == EventHookPhase.Post)
            {
                marketBuyResourceGuards.Remove(key);
                LogInfoForResourceEventPlayer(args.PlayerId, "OnPlayerMarketInteraction market buy guard removed on post:", "player", args.PlayerId, "good", args.Good, "key", key);
            }
        }

        private void OnBuildingRefund(BuildingRefundEventArgs args)
        {
            if (args.PlayerId <= 0)
                return;

            PruneExpiredResourceGuards();
            string duplicateKey = BuildRefundEventDuplicateKey(args.PlayerId, args.BuildingId, args.Percentage);
            if (refundEventDuplicateGuards.ContainsKey(duplicateKey))
            {
                LogInfoForResourceEventPlayer(
                    args.PlayerId,
                    "OnBuildingRefund duplicate event ignored:",
                    "phase", args.Phase,
                    "player", args.PlayerId,
                    "buildingId", args.BuildingId,
                    "percentage", args.Percentage,
                    "duplicateKey", duplicateKey);
                return;
            }

            refundEventDuplicateGuards[duplicateKey] = DateTime.UtcNow + RefundGuardLifetime;
            AddBuildingRefundGuards(args);
            LogInfoForResourceEventPlayer(
                args.PlayerId,
                "OnBuildingRefund refund guard added:",
                "phase", args.Phase,
                "player", args.PlayerId,
                "buildingId", args.BuildingId,
                "percentage", args.Percentage,
                "skipOriginal", args.SkipOriginalFunction);
        }

        private static string BuildResourceEventKey(int playerId, eGoods good)
        {
            return playerId + ":" + (int)good;
        }

        private static string BuildRefundEventDuplicateKey(int playerId, int buildingId, int percentage)
        {
            return playerId + ":" + buildingId;
        }

        private void ClearResourceEventGuards()
        {
            resourceAddReentryGuards.Clear();
            marketBuyResourceGuards.Clear();
            refundResourceGuards.Clear();
            refundEventDuplicateGuards.Clear();
        }

        private void PruneExpiredResourceGuards()
        {
            PruneExpiredMarketBuyResourceGuards();
            PruneExpiredRefundResourceGuards();
            PruneExpiredRefundEventDuplicateGuards();
        }

        private void PruneExpiredMarketBuyResourceGuards()
        {
            if (marketBuyResourceGuards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in marketBuyResourceGuards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                marketBuyResourceGuards.Remove(expiredKeys[i]);
        }

        private void PruneExpiredRefundResourceGuards()
        {
            PruneExpiredCountGuardKeys(refundResourceGuards);
        }

        private void PruneExpiredRefundEventDuplicateGuards()
        {
            PruneExpiredResourceGuardKeys(refundEventDuplicateGuards);
        }

        private static void PruneExpiredResourceGuardKeys<TKey>(Dictionary<TKey, DateTime> guards)
        {
            if (guards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<TKey> expiredKeys = null;
            foreach (KeyValuePair<TKey, DateTime> entry in guards)
            {
                if (entry.Value > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<TKey>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                guards.Remove(expiredKeys[i]);
        }

        private static void PruneExpiredCountGuardKeys(Dictionary<string, ResourceEventCountGuard> guards)
        {
            if (guards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = null;
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in guards)
            {
                if (entry.Value.ExpiresAt > now)
                    continue;

                if (expiredKeys == null)
                    expiredKeys = new List<string>();

                expiredKeys.Add(entry.Key);
            }

            if (expiredKeys == null)
                return;

            for (int i = 0; i < expiredKeys.Count; i++)
                guards.Remove(expiredKeys[i]);
        }

        private static int GetMarketInteractionAmount(PlayerMarketInteractionEventArgs args)
        {
            return args.ShiftModifier != 0 ? MarketBuyShiftAmount : MarketBuyAmount;
        }

        private void AddBuildingRefundGuards(BuildingRefundEventArgs args)
        {
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(args.PlayerId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            eStructs buildingType = buildingApi.GetType(args.BuildingId);
            DateTime expiresAt = DateTime.UtcNow + RefundGuardLifetime;

            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_WOOD_PLANKS, GetRefundAmount(buildingApi.GetWoodCost(buildingType), buildingApi.WoodRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_STONE_BLOCKS, GetRefundAmount(buildingApi.GetStoneCost(buildingType), buildingApi.StoneRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_IRON_INGOTS, GetRefundAmount(buildingApi.GetIronIngotCost(buildingType), buildingApi.IronRefundMultiplier), expiresAt);
            AddBuildingRefundGuard(args.PlayerId, eGoods.STORED_PITCH_RAW, GetRefundAmount(buildingApi.GetRawPitchCost(buildingType), buildingApi.PitchRefundMultiplier), expiresAt);
        }

        private void AddBuildingRefundGuard(int playerId, eGoods good, int amount, DateTime expiresAt)
        {
            if (amount <= 0)
                return;

            string key = BuildResourceEventKey(playerId, good);
            refundResourceGuards[key] = new ResourceEventCountGuard
            {
                RemainingAmount = amount,
                ExpiresAt = expiresAt
            };

            LogInfoForResourceEventPlayer(
                playerId,
                "OnBuildingRefund resource guard added:",
                "player", playerId,
                "good", good,
                "expectedAmount", amount,
                "key", key);
        }

        private static int GetRefundAmount(int cost, float refundMultiplier)
        {
            if (cost <= 0 || refundMultiplier <= 0)
                return 0;

            return (int)(cost * refundMultiplier);
        }

        private void LogInfoForResourceEventPlayer(int playerId, params object[] parts)
        {
            if (ShouldLogResourceEventPlayer(playerId))
                LogInfo(parts);
        }

        private static bool ShouldLogResourceEventPlayer(int playerId)
        {
            try
            {
                return GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            }
            catch
            {
                return false;
            }
        }
    }
}
