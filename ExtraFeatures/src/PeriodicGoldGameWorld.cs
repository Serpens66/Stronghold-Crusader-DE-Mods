using SHCDESE.API;
using SHCDESE.Interop;
using System;

namespace ExtraFeatures
{
    internal sealed class PeriodicGoldGameWorld : IPeriodicGoldWorld
    {
        public bool TryGetCurrentTick(out int tick) =>
            APIShared.Simulation.SimulationTickEvents.TryGetCurrentTick(out tick);

        public bool TryRegisterTick(Action<int> callback, out string reason) =>
            APIShared.Simulation.SimulationTickEvents.TryRegister(
                ExtraFeaturesPlugin.PluginGuid, "periodic-gold", callback, out reason, 700);

        public void VisitAlivePlayers(Action<int, bool, int, int> visitor)
        {
            var api = GamePlayerManagerAPI.Instance;
            var players = api.QueryPlayerResources().Where(GamePlayerManagerAPI.PlayerResourcePredicates.IsAlive).GetEnumerator();
            while (players.MoveNext())
            {
                int playerId = players.CurrentId;
                ref GamePlayerResources player = ref players.Current;
                visitor(playerId, api.IsAIPlayer(playerId), player.r_TotalGoodsGold, unchecked((int)player.r_incomingGold));
            }
        }

        public void AddIncomingGold(int playerId, int amount) =>
            GamePlayerManagerAPI.Instance.AddIncomingGood(playerId, eGoods.STORED_GOLD, amount);
        public void SetGold(int playerId, int gold)
        {
            if (!GamePlayerManagerAPI.Instance.SetPlayerGold(playerId, gold))
                throw new InvalidOperationException("Periodic gold player no longer available: " + playerId);
        }
    }
}
