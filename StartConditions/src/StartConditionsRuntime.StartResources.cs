using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

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

        private void OnPlayerAddResource(PlayerAddResourceEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post)
                return;

            string key = args.PlayerId + ":" + (int)args.Good + ":" + args.Amount;
            if (goodsAddedByCode.Remove(key))
                return;

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(args.PlayerId);
            int multiplyGoods = isAI ? settings.MultiplyGoodsGainAI : settings.MultiplyGoodsGainHuman;
            int multiplyMoney = isAI ? settings.MultiplyGoodsGainInMoneyAI : settings.MultiplyGoodsGainInMoneyHuman;

            if (multiplyGoods > 0)
            {
                goodsAddedByCode.Add(key);
                GamePlayerManagerAPI.Instance.TryAddGood(args.PlayerId, args.Good, args.Amount * multiplyGoods);
            }

            if (multiplyMoney > 0)
            {
                PackedGoodPrice price = GamePlayerManagerAPI.Instance.GetTradeBasePrice(args.Good);
                int sellPricePerItem = price.SellPrice / 5;
                int money = args.Amount * sellPricePerItem * multiplyMoney;
                if (money != 0)
                    GamePlayerManagerAPI.Instance.AddPlayerGold(args.PlayerId, money);
            }
        }
    }
}
