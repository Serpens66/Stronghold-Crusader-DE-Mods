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

        internal static bool Owns(string value) => value == "rescue-editor-new-off" || value == "rescue-editor-new-on" ||
            value == "rescue-editor-load-off" || value == "rescue-editor-load-on" || value == "rescue-loadcache" || value == "rescue-early-provider" || value == "rescue-on" || value == "rescue-off" ||
            value == "rescue-group-on" || value == "rescue-group-off" ||
            value == "rescue-aiwall-on" || value == "rescue-aiwall-off" ||
            value == "rescue-moat-on" || value == "rescue-moat-off" ||
            value == "rescue-moatworker-on" || value == "rescue-moatworker-off" ||
            value == "rescue-worker-on" || value == "rescue-worker-off" ||
            value == "rescue-ox-on" || value == "rescue-ox-off" ||
            value == "rescue-siege-on" || value == "rescue-siege-off" ||
            value == "rescue-mountedkeep-on" || value == "rescue-mountedkeep-off" ||
            value == "rescue-aivbridge-on" || value == "rescue-aivbridge-off" ||
            value == "rescue-auto-worker-on" || value == "rescue-auto-worker-off" ||
            value == "rescue-auto-ox-on" || value == "rescue-auto-ox-off" ||
            value == "rescue-auto-bridge-on" || value == "rescue-auto-bridge-off" ||
            value == "rescue-auto-aiwall-on" || value == "rescue-auto-aiwall-off" ||
            value == "rescue-auto-next-on" || value == "rescue-auto-next-off" ||
            value == "rescue-auto-group-on" || value == "rescue-auto-group-off" ||
            value == "rescue-auto-oxhome-on" || value == "rescue-auto-oxhome-off" ||
            value == "rescue-auto-worker-cargo-on" || value == "rescue-auto-worker-cargo-off" ||
            value == "rescue-auto-applestep-on" || value == "rescue-auto-applestep-off" ||
            value == "rescue-auto-applestep-cargo-on" || value == "rescue-auto-applestep-cargo-off" ||
            value == "rescue-auto-miller-on" || value == "rescue-auto-miller-off" ||
            value == "rescue-auto-baker-on" || value == "rescue-auto-baker-off" ||
            value == "rescue-auto-brewer-on" || value == "rescue-auto-brewer-off" ||
            value == "rescue-auto-mason-on" || value == "rescue-auto-mason-off" ||
            value == "rescue-natural-dig-bank-on" || value == "rescue-natural-dig-bank-off" ||
            value == "rescue-natural-dig-task-on" || value == "rescue-natural-dig-task-off" ||
            value == "rescue-natural-fill-bank-on" || value == "rescue-natural-fill-bank-off" ||
            value == "rescue-auto-poleturner-on" || value == "rescue-auto-poleturner-off" ||
            value == "rescue-auto-smith-on" || value == "rescue-auto-smith-off" ||
            value == "rescue-auto-armourer-on" || value == "rescue-auto-armourer-off" ||
            value == "rescue-auto-appletree2-on" || value == "rescue-auto-appletree2-off" ||
            value == "rescue-auto-appletree8-on" || value == "rescue-auto-appletree8-off" ||
            value == "rescue-control-appletree2-on" || value == "rescue-control-appletree8-on" ||
            value == "rescue-negative-appletree2-delete-on" || value == "rescue-negative-appletree8-delete-on" ||
            value == "rescue-negative-appletree2-reuse-on" || value == "rescue-negative-appletree8-reuse-on" ||
            value == "rescue-auto-idle-on" || value == "rescue-auto-idle-off" ||
            value == "rescue-auto-idlearcher-on" || value == "rescue-auto-idlearcher-off" ||
            value == "rescue-auto-commandmoat-on" || value == "rescue-auto-commandmoat-off" ||
            value == "rescue-auto-commandmoat-postmidpoint-on" || value == "rescue-auto-commandmoat-postmidpoint-off" ||
            value == "rescue-auto-commandmoat-nextonly-premidpoint-on" || value == "rescue-auto-commandmoat-nextonly-premidpoint-off" ||
            value == "rescue-auto-commandmoat-nextonly-postmidpoint-on" || value == "rescue-auto-commandmoat-nextonly-postmidpoint-off" ||
            value == "rescue-persistence-parked-on" || value == "rescue-worker-native0save-on" ||
            value == "rescue-diag-health4000-on" ||
            value == "rescue-perf-1000-on" ||
            value == "rescue-perf-1000-passive-on" ||
            value == "rescue-perf-4000-idle1000walk-passive-on" || value == "rescue-perf-9871-idle1000walk-passive-on" ||
            value == "rescue-persistence-on" || value == "rescue-persistence-off" ||
            RescueScenarioSelection.IsMountedKeepPart(value);
        internal void Stop() { stopped = true; }

        internal IEnumerable<AutomationStep> Steps()
        {
#if !SERP_DEVDIAG
            Require(scenario!="rescue-diag-health4000-on","Health event diagnostic profile requires build.bat /devdiag");
#endif
            if (scenario.StartsWith("rescue-editor-",StringComparison.Ordinal))
            {
                foreach(var step in EditorAcceptanceSteps())yield return step;
                yield break;
            }
            if (scenario == "rescue-early-provider")
            {
                foreach(var step in EarlyProviderSteps())yield return step;
                yield break;
            }
            if (scenario == "rescue-loadcache")
            {
                foreach (var step in AssemblyLoadCacheSteps()) yield return step;
                yield break;
            }
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            if (scenario == "rescue-worker-native0save-on")
            {
                foreach (var step in Native0WorkerPersistenceSteps()) yield return step;
                yield break;
            }
            if (scenario == "rescue-perf-1000-on")
            {
                foreach (var step in PerformanceSteps()) yield return step;
                yield break;
            }
            if (scenario == "rescue-perf-1000-passive-on")
            {
                foreach (var step in PerformanceSteps(observeArmyMoves:false)) yield return step;
                yield break;
            }
            if (scenario=="rescue-diag-health4000-on")
            {
#if SERP_DEVDIAG
                foreach(var step in PerformanceSteps(population:4000,observeArmyMoves:false,observeHealthEvents:true))yield return step;
#else
                throw new InvalidOperationException("Health event diagnostic profile requires build.bat /devdiag");
#endif
                yield break;
            }
            if (IsHigherPerformanceTier())
            {
                foreach(var step in PerformanceSteps(population:scenario=="rescue-perf-4000-idle1000walk-passive-on"?4000:9871,observeArmyMoves:false))yield return step;
                yield break;
            }
            if (scenario == "rescue-persistence-parked-on")
            {
                foreach (var step in ParkedPersistenceSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-persistence-"))
            {
                foreach (var step in PersistenceSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-commandmoat-"))
            {
                foreach (var step in AutomaticCommandMoatSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-idle-") || scenario.Contains("-auto-idlearcher-"))
            {
                foreach (var step in AutomaticIdleSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-miller-"))
            {
                foreach (var step in AutomaticMillerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-baker-"))
            {
                foreach (var step in AutomaticBakerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-brewer-"))
            {
                foreach (var step in AutomaticBrewerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-mason-"))
            {
                foreach (var step in AutomaticMasonSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-natural-dig-"))
            {
                foreach (var step in NaturalMoatWorkSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-natural-fill-"))
            {
                foreach (var step in NaturalMoatFillSteps()) yield return step;
                yield break;
            }
            if (scenario == "rescue-auto-smith-on" || scenario == "rescue-auto-smith-off")
            {
                foreach (var step in AutomaticSmithSteps()) yield return step;
                yield break;
            }
            if (scenario == "rescue-auto-armourer-on" || scenario == "rescue-auto-armourer-off")
            {
                foreach (var step in AutomaticArmourerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-poleturner-"))
            {
                foreach (var step in AutomaticPoleturnerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-applestep-"))
            {
                foreach (var step in AutomaticAppleStepSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-appletree2-") || scenario.Contains("-auto-appletree8-"))
            {
                foreach (var step in AutomaticAppleTreeSteps()) yield return step;
                yield break;
            }
            if (scenario=="rescue-control-appletree2-on" || scenario=="rescue-control-appletree8-on")
            {
                foreach(var step in AppleTreeControlSteps())yield return step;
                yield break;
            }
            if (scenario=="rescue-negative-appletree2-delete-on" || scenario=="rescue-negative-appletree8-delete-on" ||
                scenario=="rescue-negative-appletree2-reuse-on" || scenario=="rescue-negative-appletree8-reuse-on")
            {
                foreach(var step in AppleLifetimeSteps())yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-oxhome-"))
            {
                foreach (var step in AutomaticOxHomeSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-group-"))
            {
                foreach (var step in AutomaticGroupSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-next-"))
            {
                foreach (var step in AutomaticNextSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-bridge-"))
            {
                foreach (var step in AutomaticBridgeSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-aiwall-"))
            {
                foreach (var step in AutomaticWallSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-worker-"))
            {
                foreach (var step in AutomaticWorkerSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-auto-ox-"))
            {
                foreach (var step in AutomaticOxSteps()) yield return step;
                yield break;
            }
            if (scenario.Contains("-aivbridge-"))
            {
                foreach (var step in AivBridgeSteps()) yield return step;
                yield break;
            }
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
                foreach (var step in SiegeSteps()) yield return step;
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
            yield return AutomationWait.Until("fixture main menu before settings",()=>RescueScenarioSeries.MenuReady(run));
            PrepareDeterministicModSettings();
            enable(expectedEnabled); // Only before the mission, never a mid-game setting mutation.
            // The SDK owns the lobby lifetime and invokes preparation only after its roster is ready.
            // The hidden SP slider would reset peace, so configure the world at this final boundary.
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT },
                peaceTimeMinutes: null, beforeLaunch: ConfigureDeterministicLobby)) yield return wait;
            Require(deterministicLobbyConfigured, "Deterministic lobby was not configured before launch");
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
            VerifyDeterministicWorld(run);
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
