using System;
using System.Collections.Generic;
using System.IO;
using APIShared.Automation;
using CrusaderDE;

namespace PathfindingLoadTest
{
    // "stress-observe": loads the runner's -SaveFixture (an AI-heavy save) and lets the ordinary
    // simulation run at speed 250 for a fixed number of native ticks. No world changes; the
    // tested mods' own diagnostics (e.g. Pathfinding resweep/policy lines) are the evidence.
    internal sealed partial class Fixture
    {
        private const int StressChunkTicks = 500, StressChunks = 8;
        private IEnumerable<AutomationStep> StressSteps()
        {
            string fixture = Path.Combine(BepInEx.Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
            yield return new AutomationStep("load-fixture", TimeSpan.FromSeconds(240), run => run.LoadSave(fixture));
            yield return new AutomationStep("observe", TimeSpan.FromSeconds(600), ObserveStress, true);
        }
        private IEnumerable<AutomationWait> ObserveStress(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            Director.instance.SetEngineFrameRate(250);
            log("stress start chunks=" + StressChunks + " ticksPerChunk=" + StressChunkTicks + " speed=250");
            for (int chunk = 1; chunk <= StressChunks; chunk++)
            {
                yield return run.WaitForTicks(run.CurrentMission, StressChunkTicks);
                log("stress progress chunk=" + chunk + "/" + StressChunks);
            }
            log("stress complete ticks>=" + StressChunks * StressChunkTicks);
        }
    }
}
