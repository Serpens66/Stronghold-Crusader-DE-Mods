using System;
using System.Collections.Generic;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Interop;

namespace ConstructionCostsTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> PrecisionWell(AutomationRun run)
        {
            run.InSimulation(run.CurrentMission, () => {
                var players = GamePlayerManagerAPI.Instance;
                owner = players.GetLocalPlayerId();
                Require(owner > 0 && !players.IsAIPlayer(owner), "Precision fixture requires the actual local Human");
                FindKeep(owner, out keepX, out keepY);
                SaveAndSet(well, 20);
                players.AddPlayerGold(owner, 1000);
                int before = players.GetGoodAmount(owner, eGoods.STORED_GOLD);
                Require(PlaceNear(owner, eMappers.MAPPER_WELL, false, keepX, keepY, out wellId, out wellGlobal, out wellX, out wellY),
                    "No legal paid well for 70% precision fixture");
                Require(before - players.GetGoodAmount(owner, eGoods.STORED_GOLD) == 30 && ReceiptCount() == 1,
                    "Precision fixture must actually pay native20 + Human10 exactly once");
                paidWellExtra = 10;
                log("PASS precision70 actual paid=30 native=20 Human=10 receipts=1 identity=" + wellId + ":" + wellGlobal);
                return true;
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
    }
}
