using BugfixesAndQoL;
using BugfixesAndQoL.TrappedUnitRescue;
using System;
using System.Collections.Generic;
using APIShared.Automation;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationWait> AiWall(AutomationRun run)
        {
            Execute(run, () => { FindSite(); SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN, cx, cy); });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                VerifyLiveAndRemember();
                Actor actor = actors[0];
                var scope = BeginMutation("controlled-native-ai-wall", cx, cy, 1);
                try
                {
                    // Public SE wrapper invokes the actual AI wall routine and both Fixes event phases.
                    // The controlled human owner isolates movement; this is not an autonomous AI decision test.
                    BulkAIDetours.c_game_ai_build_wall_hook_impl(GameTileManagerAPI.Instance.GetTileManager(), owner, cx, cy, eMappers.MAPPER_WALL);
                    int tile = GameTileManagerAPI.Instance.GetTileId(cx, cy);
                    Require((GameTileManagerAPI.Instance.GetTilePropertyFlag(tile) & TilePropertyFlag.IsWall) != 0,
                        "Actual AI wall routine did not construct the wall");
                    scope.Complete(false);
                }
                finally { scope.Dispose(); }
                if (expectedEnabled) CheckActor(actor, true);
                else CheckBaselineIdentity(actor);
                int distance = Math.Max(Math.Abs(actor.X - actor.SourceX), Math.Abs(actor.Y - actor.SourceY));
                if (expectedEnabled)
                    Require(distance > 0 && distance <= 8 && HasStructuralExit(actor), "Active AI-wall rescue did not produce a free local position");
                log("ai-wall-result nativeWallVerified=true enabled=" + expectedEnabled + " unit=" + actor.Id + "/" + actor.Global +
                    " displacement=" + distance + " hpPreserved=true tribePreserved=true occupancyRequired=" + expectedEnabled +
                    " currentTileOccurrences=" + CountInTileChain(GameTileManagerAPI.Instance.GetTileId(actor.X, actor.Y), actor.Id) +
                    " baselineFixesMayTeleportToKeep=true");
                RetireActors();
            });
        }

        private IEnumerable<AutomationWait> MoatControl(AutomationRun run)
        {
            Execute(run, () => { FindSite(); SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN, cx, cy); });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            int targetX = 0, targetY = 0;
            Execute(run, () => {
                VerifyLiveAndRemember();
                var scope = BeginMutation("controlled-native-direct-completed-moat", cx, cy, 1);
                try
                {
                    var tiles = GameTileManagerAPI.Instance;
                    int tile = tiles.GetTileId(cx, cy);
                    // The SE parameter named brushSize is actually issuer at native6FE90;
                    // packed tile, ordinaryY,width1,Moat operation,variant2 reproduce completed placement.
                    EditorBrush.Call(tiles.GetTileManager(), owner,
                        tile, cy, 1, unchecked((int)(uint)TilePropertyFlag.IsMoat), 2, 0);
                    Require((tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.IsMoat) != 0,
                        "Actual native completed-moat writer did not accept the occupied fixture tile");
                    scope.Complete(false);
                }
                finally { scope.Dispose(); }
                CheckActor(actors[0], true);
                FindMovementTarget(actors[0], out targetX, out targetY);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 32);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            bool accepted = false;
            Execute(run, () => accepted = GameTribeManagerAPI.Instance.IssueMoveHereCommand(actors[0].Tribe, targetX, targetY));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            bool arrived = false;
            for (int i = 0; i < 48; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 24);
                arrived = run.InSimulation(run.CurrentMission, () => MovementOutcome(actors[0], targetX, targetY));
                if (arrived) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(accepted && arrived, "Spearman did not leave a real completed moat through a real native move");
                log("native-moat-control complete=true enabled=" + expectedEnabled +
                    " expectation=ordinary-moat-egress notProofOfPersistentTrap=true identity=true hp=true occupancy=true");
                RetireActors();
            });
        }
    }
}
