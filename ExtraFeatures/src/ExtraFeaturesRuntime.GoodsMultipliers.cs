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
            // Raised for every stockpile change of every player (both phases, additions and
            // removals): the event-shape checks come before any native lookup or key.
            if (args.Phase != EventHookPhase.Post || !args.Add || args.AddAmount <= 0)
                return;
            int playerId = GameBuildingManagerAPI.Instance.GetOwner(args.BuildingId);
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId))
                return;
            long key = ResourceEventKey(playerId, args.Good);

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

        private static bool ConsumeGuard(Dictionary<long, ResourceEventCountGuard> guards, long key, int amount)
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

        private void PruneExpiredCountGuardKeys(Dictionary<long, ResourceEventCountGuard> guards)
        {
            if (guards.Count == 0)
                return;

            int now = CurrentGameTick();
            // Reused buffer: pruning runs before every guarded stockpile addition.
            List<long> expiredKeys = expiredResourceGuardKeys;
            expiredKeys.Clear();
            foreach (KeyValuePair<long, ResourceEventCountGuard> entry in guards)
            {
                if (IsTickWindowExpired(entry.Value.CreatedTick, now, ResourceGuardLifetimeTicks))
                    expiredKeys.Add(entry.Key);
            }
            for (int index = 0; index < expiredKeys.Count; index++)
                guards.Remove(expiredKeys[index]);
            expiredKeys.Clear();
        }

        private void LogDebugForResourceEventPlayer(int playerId, params object[] parts)
        {
            try
            {
                if (GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) &&
                    !GamePlayerManagerAPI.Instance.IsAIPlayer(playerId))
                {
                    Shared.DebugLogHelper.LogDebugLimited(log, "ExtraFeatures/src/ExtraFeaturesRuntime.GoodsMultipliers.cs:100", parts);
                }
            }
            catch
            {
                // Diagnostic filtering must never affect resource handling.
            }
        }
    }
}
