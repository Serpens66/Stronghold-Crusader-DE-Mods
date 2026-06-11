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

namespace SettingsMod
{
    public sealed partial class SettingsModRuntime
    {
        private void ApplyStartResources()
        {
            ForEachAlivePlayer(playerId =>
            {
                LogInfo("Applying start resources for player", playerId);
                ApplyStartGold(playerId);
                ReplaceStartGoods(playerId);
            });
        }

        private void ApplyStartGold(int playerId)
        {
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            string setGold = isAI ? settings.SetStartGoldAI : settings.SetStartGoldHuman;
            int addGold = isAI ? settings.AddStartGoldAI : settings.AddStartGoldHuman;

            if (TryParseNullableInt(setGold, out int startGold))
            {
                GamePlayerManagerAPI.Instance.SetPlayerGold(playerId, (uint)Math.Max(0, startGold));
                LogInfo("Set gold of player", playerId, "to", startGold);
            }

            if (addGold != 0)
            {
                GamePlayerManagerAPI.Instance.AddPlayerGold(playerId, addGold);
                LogInfo("Add gold to player", playerId, addGold);
            }
        }

        private void ReplaceStartGoods(int playerId)
        {
            Dictionary<eGoods, int> aiGoods = ParseEnumAmounts<eGoods>(settings.StartGoodsAI);
            Dictionary<eGoods, int> humanGoods = ParseEnumAmounts<eGoods>(settings.StartGoodsHuman);

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            bool overwrite = isAI ? settings.OverwriteStartGoodsAI : settings.OverwriteStartGoodsHuman;
            if (!overwrite)
                return;

            Dictionary<eGoods, int> goods = isAI ? aiGoods : humanGoods;
            ClearIncomingGoodsWithoutMoney(playerId);
            foreach (KeyValuePair<eGoods, int> entry in goods)
            {
                if (entry.Value <= 0)
                    continue;

                GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, entry.Key, entry.Value);
                LogInfo("AddIncomingGood", entry.Value, entry.Key, "to player", playerId);
            }
        }

        private void ClearIncomingGoodsWithoutMoney(int playerId)
        {
            foreach (eGoods good in IncomingGoodsWithoutMoney)
                GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, good, IncomingGoodClearAmount);
        }

        private static eGoods[] CreateIncomingGoodsWithoutMoney()
        {
            Array values = Enum.GetValues(typeof(eGoods));
            List<eGoods> goods = new List<eGoods>(values.Length);
            foreach (eGoods good in values)
            {
                if (good != eGoods.STORED_GOLD)
                    goods.Add(good);
            }

            return goods.ToArray();
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
