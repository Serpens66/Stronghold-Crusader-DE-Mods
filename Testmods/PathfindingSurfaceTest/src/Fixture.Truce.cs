using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Units;
using SHCDESE.Interop;

namespace PathfindingSurfaceTest
{
    internal sealed partial class Fixture
    {
        private AttackTargetRuleRegistration corridorTruce;
        private long corridorTruceRevision;
        private AutomationRun corridorTruceRun;
        private MissionContext corridorTruceMission;

        private IEnumerable<AutomationWait> EstablishCorridorTruce(AutomationRun run)
        {
            corridorTruceRun = run; corridorTruceMission = run.CurrentMission;
            Execute(run, () => {
                Require(AttackTargetRules.IsAvailable && !run.CurrentMission.Mode.IsRealMultiplayer,
                    "Temporary test truce requires the published public API and an offline mission");
                var rules = new List<AttackTargetRule>();
                for (int player = 1; player <= 8; player++)
                    if (player != NaturalOwner)
                        rules.Add(new AttackTargetRule(new AttackTargetSelector(playerId: NaturalOwner),
                            new AttackTargetSelector(playerId: player), bidirectional: true, condition: AttackTargetCondition.Always));
                Require(corridorTruce.TryReplaceRules(run.CurrentMission, ++corridorTruceRevision, rules, out string reason),
                    "Public temporary test-truce publication failed: " + reason);
                log("corridor-truce published=true available=true owner=" + NaturalOwner + " pairs=" + rules.Count +
                    " revision=" + corridorTruceRevision + " condition=Always alliancesChanged=false");
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            // Existing projectiles may already be in flight when exclusions publish.
            yield return run.WaitForTicks(run.CurrentMission, 256);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(AttackTargetRules.IsAvailable && missions?.Current?.SessionId == session,
                    "Public test truce or its mission was lost during the settling interval");
                log("corridor-truce settledTicks=256 available=true session=" + session);
            });
        }

        private void ClearCorridorTruce()
        {
            if (corridorTruce == null || corridorTruceRun == null || missions?.Current?.SessionId != session) return;
            try
            {
                corridorTruceRun.InSimulation(corridorTruceMission, () => {
                    bool cleared = corridorTruce.TryReplaceRules(corridorTruceMission, ++corridorTruceRevision,
                        Array.Empty<AttackTargetRule>(), out string reason);
                    log("corridor-truce cleared=" + cleared + " reason=" + reason);
                    return 0;
                });
            }
            catch (Exception failure)
            {
                // Public mission-owned rules are also retired at the native world boundary.
                log("corridor-truce clear-not-current reason=" + failure.Message);
            }
        }

        private unsafe void TraceCorridorActorFailure(Actor actor, GameUnit* unit)
        {
            if (scenario != "natural-route-corridor" || UnitAccess.IsReallyAlive(unit)) return;
            log("corridor-actor-failed actor=" + actor.Id + " global=" + unit->r_GlobalId + " expectedGlobal=" + actor.Global +
                " owner=" + unit->r_ControllableForPlayerId + " type=" + unit->r_UnitChimp + " aiState=" + unit->r_AIState +
                " health=" + unit->r_CurrentHealth + " maximum=" + unit->r_MaxHealth + " aliveState=" + unit->r_AliveState +
                " projectileDeathMarker=" + unit->r_IsKilledByProjectile + " tile=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY +
                " target=" + unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY + " tribe=" + unit->r_TribeId);
        }
    }
}
