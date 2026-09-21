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
            ForEachActivePlayer(playerId =>
            {
                TryRunFeature($"start gold for player {playerId}", () => ApplyStartGold(playerId));
                TryRunFeature($"start goods for player {playerId}", () => ReplaceStartGoods(playerId));
            });
        }

        private void ApplyStartGold(int playerId)
        {
            IStartConditionsSettings current = EffectiveSettings;
            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            int setGold = isAI ? current.SetStartGoldAI : current.SetStartGoldHuman;
            int addGold = isAI ? current.AddStartGoldAI : current.AddStartGoldHuman;

            if (!StartGoldPolicy.IsValidConfiguredValue(setGold))
                throw new System.InvalidOperationException($"Configured start gold {setGold} is outside -1..{StartGoldPolicy.MaximumGold}.");

            if (setGold >= 0)
            {
                GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, eGoods.STORED_GOLD, 1000000);
                int directlySetGold = addGold < 0
                    ? StartGoldPolicy.CalculateGold(0, setGold, addGold)
                    : setGold;
                GamePlayerManagerAPI.Instance.SetPlayerGold(playerId, directlySetGold);
            }

            if (addGold > 0)
            {
                GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, eGoods.STORED_GOLD, addGold);
            }
            else if (addGold < 0 && setGold < 0)
            {
                GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, eGoods.STORED_GOLD, -addGold);
            }
        }

        private void ReplaceStartGoods(int playerId)
        {
            IStartConditionsSettings current = EffectiveSettings;
            Dictionary<eGoods, int> aiGoods = ParseEnumAmounts<eGoods>(current.StartGoodsAI, -1, 10000);
            Dictionary<eGoods, int> humanGoods = ParseEnumAmounts<eGoods>(current.StartGoodsHuman, -1, 10000);

            bool isAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);
            Dictionary<eGoods, int> goods = isAI ? aiGoods : humanGoods;
            foreach (KeyValuePair<eGoods, int> entry in goods)
            {
                if (entry.Value < 0)
                    continue;

                if (!IsConfigurableStoredGood(entry.Key))
                {
                    continue;
                }

                try
                {
                    GamePlayerManagerAPI.Instance.SubtractIncomingGood(playerId, entry.Key, IncomingGoodClearAmount);
                    if (entry.Value > 0)
                        GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, entry.Key, entry.Value);
                }
                catch (System.Exception ex)
                {
                    LogError("Start Conditions start-good operation failed; remaining goods continue:", entry.Key, "player", playerId, ex);
                }
            }
        }
    }
}
