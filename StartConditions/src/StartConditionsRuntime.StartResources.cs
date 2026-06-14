using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
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

            if (addGold > 0)
            {
                GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, eGoods.STORED_GOLD, addGold);
                LogDebug("Add gold to player", playerId, addGold);
            }
            else if (addGold < 0)
            {
                GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, eGoods.STORED_GOLD, -addGold);
                LogDebug("Subtract incoming gold from player", playerId, -addGold);
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
    }
}
