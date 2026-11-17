using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace TrappedUnitRescueTest
{
    // The plugin retains this coordinator; all world access is under the public automation monitor.
    // Native construction is distinguished from explicitly synthetic topology tests in every log.
    internal sealed partial class Fixture
    {
        private readonly string map, scenario;
        private readonly int peaceTimeMinutes;
        private readonly Action<string> log;
        private readonly RescueRuntime runtime;
        private readonly Action<bool> enable;
        private readonly Action<bool> suppression;
        private readonly Func<bool> workerDeletionInstalled;
        private readonly List<Actor> actors = new List<Actor>();
        private bool stopped;
        private int owner, cx, cy, elevation;
        private long session;
        private readonly bool expectedEnabled;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private sealed class Actor
        {
            internal int Id, Tribe, X, Y, SourceX, SourceY;
            internal uint Global, Health;
            internal eChimps Type;
        }

        internal Fixture(string map, string scenario, Action<string> log, RescueRuntime runtime, Action<bool> enable, Action<bool> suppression, Func<bool> workerDeletionInstalled, int peaceTimeMinutes)
        {
            this.map = map; this.scenario = scenario;
            if (peaceTimeMinutes < 0) throw new ArgumentOutOfRangeException(nameof(peaceTimeMinutes));
            this.peaceTimeMinutes = peaceTimeMinutes; this.log = log;
            this.runtime = runtime; this.enable = enable;
            this.suppression = suppression ?? throw new ArgumentNullException(nameof(suppression));
            this.workerDeletionInstalled = workerDeletionInstalled ?? throw new ArgumentNullException(nameof(workerDeletionInstalled));
            expectedEnabled = scenario.EndsWith("-on", StringComparison.Ordinal);
        }

        internal static bool Owns(string value) => value == "rescue-on" || value == "rescue-off" ||
            value == "rescue-group-on" || value == "rescue-group-off" ||
            value == "rescue-aiwall-on" || value == "rescue-aiwall-off" ||
            value == "rescue-moat-on" || value == "rescue-moat-off" ||
            value == "rescue-moatworker-on" || value == "rescue-moatworker-off" ||
            value == "rescue-worker-on" || value == "rescue-worker-off" ||
            value == "rescue-ox-on" || value == "rescue-ox-off" ||
            value == "rescue-siege-on" || value == "rescue-siege-off" ||
            value == "rescue-mountedkeep-on" || value == "rescue-mountedkeep-off";
        internal void Stop() { stopped = true; }

        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            if (scenario.Contains("-mountedkeep-"))
            {
                foreach (var step in MountedKeepSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-group-"))
            {
                yield return new AutomationStep("shared-group-leader-and-member", TimeSpan.FromSeconds(240), EntryGroup, true);
                yield break;
            }
            if (scenario.Contains("-aiwall-"))
            {
                yield return new AutomationStep("controlled-native-ai-wall", TimeSpan.FromSeconds(90), AiWall, true);
                yield break;
            }
            if (scenario.Contains("-moat-"))
            {
                yield return new AutomationStep("native-moat-egress-control", TimeSpan.FromSeconds(150), MoatControl, true);
                yield break;
            }
            if (scenario.Contains("-moatworker-"))
            {
                yield return new AutomationStep("native-moat-worker-fixes-guard", TimeSpan.FromSeconds(300), MoatWorker, true);
                yield break;
            }
            if (scenario.Contains("-worker-"))
            {
                yield return new AutomationStep("working-apple-farmer", TimeSpan.FromSeconds(300), Worker, true);
                yield break;
            }
            if (scenario.Contains("-ox-"))
            {
                yield return new AutomationStep("naturally-loaded-ox", TimeSpan.FromSeconds(480), Ox, true);
                yield break;
            }
            if (scenario.Contains("-siege-"))
            {
                yield return new AutomationStep("manned-siege-rescue", TimeSpan.FromSeconds(240), Siege, true);
                yield break;
            }
            yield return new AutomationStep("native-bridge-spearmen", TimeSpan.FromSeconds(150),
                run => Bridge(run, eChimps.CHIMP_TYPE_SPEARMAN), true);
            yield return new AutomationStep("native-bridge-assassins", TimeSpan.FromSeconds(150),
                run => Bridge(run, eChimps.CHIMP_TYPE_ARAB_ASSASIN), true);
            yield return new AutomationStep("no-op-preserves-unit", TimeSpan.FromSeconds(60), NoOp, true);
            yield return new AutomationStep("valid-ground-enclosure", TimeSpan.FromSeconds(60), GroundEnclosure, true);
            yield return new AutomationStep("coverage", TimeSpan.FromSeconds(15), Coverage, true);
        }

        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            Require(Owns(scenario), "Unowned scenario");
            enable(expectedEnabled); // Only before the mission, never a mid-game setting mutation.
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT }, peaceTimeMinutes > 0 ? (int?)peaceTimeMinutes : null)) yield return wait;
        }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Director.instance.SetEngineFrameRate(250);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 16);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            session = run.CurrentMission.SessionId;
            owner = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetLocalPlayerId());
            Require(owner > 0 && owner <= 8, "No local fixture owner");
            log("fixture-ready scenario=" + scenario + " enabled=" + expectedEnabled + " session=" + session +
                " map=" + map + " owner=" + owner + " disposable=true source=controlled-native-construction" +
                " fixesSuppression=owned-synchronous-actions-only");
        }

        private IEnumerable<AutomationWait> Bridge(AutomationRun run, eChimps type)
        {
            Execute(run, () => {
                FindSite();
                SpawnMatrix(type);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            List<int> trapped = null;
            Execute(run, () => { VerifyLiveAndRemember(); trapped = BuildBridgeAndCheck(type); });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 32);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;

            Actor sample = actors.Single(a => a.Id == trapped[0]);
            int targetX = 0, targetY = 0;
            bool issued = false;
            Execute(run, () => {
                CheckActor(sample, true);
                FindMovementTarget(sample, out targetX, out targetY);
                issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(sample.Tribe, targetX, targetY);
                log("movement-issued type=" + type + " unit=" + sample.Id + " target=" + targetX + "," + targetY +
                    " accepted=" + issued + " enabled=" + expectedEnabled);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            bool arrived = false;
            for (int i = 0; i < (expectedEnabled ? 48 : 4); i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 24);
                arrived = run.InSimulation(run.CurrentMission, () => MovementOutcome(sample, targetX, targetY));
                if (expectedEnabled && arrived) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                if (expectedEnabled) Require(issued && arrived, "Rescued actor never reached the real native movement endpoint");
                else Require(!arrived && AtSource(sample), "Baseline actor escaped; this case does not demonstrate the requested failure");
                log("case-complete native-bridge type=" + type + " enabled=" + expectedEnabled + " trapped=" + trapped.Count +
                    " identity=true hp=true occupancy=true endpoint=" + arrived + " source=" + sample.SourceX + "," + sample.SourceY);
                RetireActors();
            });
        }

        private IEnumerable<AutomationWait> NoOp(AutomationRun run)
        {
            Execute(run, () => { FindSite(); SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN, cx, cy); });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                VerifyLiveAndRemember();
                var scope = runtime.BeginMutation("no-op-control", cx, cy, 1);
                try { scope.Complete(false); }
                finally { scope.Dispose(); }
                foreach (var actor in actors) { CheckActor(actor, true); Require(AtSource(actor), "No-op moved an actor"); }
                log("negative-control name=no-op preserved=true enabled=" + expectedEnabled);
                RetireActors();
            });
        }

        private IEnumerable<AutomationWait> GroundEnclosure(AutomationRun run)
        {
            Execute(run, () => { FindSite(); SpawnSingle(eChimps.CHIMP_TYPE_SPEARMAN, cx, cy); });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 8);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                VerifyLiveAndRemember();
                var tiles = GameTileManagerAPI.Instance;
                int source = tiles.GetTileId(cx, cy);
                uint beforeFlags = (uint)tiles.GetTilePropertyFlag(source);
                byte beforeHeight = tiles.GetTileHeight(source);
                var scope = runtime.BeginMutation("synthetic-valid-ground-enclosure", cx, cy, 2);
                try
                {
                    // Explicit synthetic negative: only the neighbours change. The legal source is never painted.
                    for (int d = 0; d < 8; d++)
                    {
                        int x = cx + Dx[d], y = cy + Dy[d], tile = tiles.GetTileId(x, y);
                        tiles.SetTilePropertyFlag(tile, tiles.GetTilePropertyFlag(tile) | TilePropertyFlag.IsWall);
                        tiles.SetTileHeight(tile, 90);
                    }
                    for (int d = 0; d < 8; d++) GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(cx + Dx[d], cy + Dy[d]);
                    Require((uint)tiles.GetTilePropertyFlag(source) == beforeFlags && tiles.GetTileHeight(source) == beforeHeight,
                        "Negative fixture changed the source geometry");
                    Require(!HasStructuralExit(actors[0]), "Negative enclosure did not actually close all exits");
                    scope.Complete(false);
                }
                finally { scope.Dispose(); }
                CheckActor(actors[0], true);
                Require(AtSource(actors[0]), "A valid ground enclosure was mistaken for an invalid occupied tile");
                log("negative-control name=synthetic-valid-ground-enclosure sourceGeometryUnchanged=true noExits=true preserved=true");
                RetireActors();
            });
        }

        private IEnumerable<AutomationWait> Coverage(AutomationRun run)
        {
            Require(!stopped && run.CurrentMission.SessionId == session, "Test mission replaced");
            log("coverage scenario=" + scenario + " executed=controlled-native-bridge-spearman,controlled-native-bridge-assassin,real-native-movement,identity,hp,tribe,occupancy,no-op,synthetic-enclosure " +
                "not-executed-in-this-scenario=autonomous-AI,real-moat-completion,working-apple-farmer-cargo,manned-siege,real-multiplayer,save-load,active-climb " +
                "baseline=" + !expectedEnabled + " overallCoverageComplete=false");
            yield return AutomationWait.NextFrame;
        }

        private void Execute(AutomationRun run, Action action)
        {
            Require(!stopped && run.CurrentMission.SessionId == session, "Expected fixture mission is no longer active");
            run.InSimulation(run.CurrentMission, () => {
                // The public automation boundary holds EngineInterface's existing monitor.
                // No natural simulation tick can enter between this gate and its finally.
                try
                {
                    suppression(expectedEnabled);
                    action();
                    return 0;
                }
                finally { suppression(false); }
            });
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
