// Feature: Multiply gained goods or award their sell value as extra money.
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesRuntime
    {
        private void OnGoodsyardAddGood(AddGoodToGoodsyardEventArgs args)
        {
            int playerId = GameBuildingManagerAPI.Instance.GetOwner(args.BuildingId);
            string key = BuildResourceEventKey(playerId, args.Good);
            if (args.Phase != EventHookPhase.Post || !args.Add || args.AddAmount <= 0 ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
            {
                return;
            }

            PruneExpiredResourceGuards();
            if (resourceAddReentryGuards.Contains(key))
                return;

            if (ConsumeGuard(marketBuyResourceGuards, key, args.AddAmount) ||
                ConsumeGuard(refundResourceGuards, key, args.AddAmount))
            {
                return;
            }

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            double multiplyGoods = isAI ? settings.MultiplyGoodsGainAI : settings.MultiplyGoodsGainHuman;
            double multiplyMoney = isAI ? settings.MultiplyGoodsGainInMoneyAI : settings.MultiplyGoodsGainInMoneyHuman;

            if (multiplyGoods > 1)
            {
                int bonusAmount = (int)Math.Round(args.AddAmount * (multiplyGoods - 1), MidpointRounding.AwayFromZero);
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
                int money = (int)Math.Round(args.AddAmount * (price.SellPrice / 5.0) * multiplyMoney, MidpointRounding.AwayFromZero);
                if (money != 0)
                    GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, money);
            }
        }

        private static bool ConsumeGuard(Dictionary<string, ResourceEventCountGuard> guards, string key, int amount)
        {
            if (!guards.TryGetValue(key, out ResourceEventCountGuard guard))
                return false;

            guard.RemainingAmount -= amount;
            if (guard.RemainingAmount <= 0)
                guards.Remove(key);
            return true;
        }

        private void PruneExpiredResourceGuards()
        {
            PruneExpiredCountGuardKeys(marketBuyResourceGuards);
            PruneExpiredCountGuardKeys(refundResourceGuards);
        }

        private static void PruneExpiredCountGuardKeys(Dictionary<string, ResourceEventCountGuard> guards)
        {
            if (guards.Count == 0)
                return;

            DateTime now = DateTime.UtcNow;
            List<string> expiredKeys = new List<string>();
            foreach (KeyValuePair<string, ResourceEventCountGuard> entry in guards)
            {
                if (entry.Value.ExpiresAt <= now)
                    expiredKeys.Add(entry.Key);
            }
            foreach (string key in expiredKeys)
                guards.Remove(key);
        }

        private void LogDebugForResourceEventPlayer(int playerId, params object[] parts)
        {
            try
            {
                if (GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    Shared.DebugLogHelper.LogDebug(log, parts);
                }
            }
            catch
            {
                // Diagnostic filtering must never affect resource handling.
            }
        }
    }
}
