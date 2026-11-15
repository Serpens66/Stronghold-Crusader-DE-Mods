using System;
using System.Collections.Generic;
using System.IO;
using APIShared;
using APIShared.Automation;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;

namespace MultiplayerRestartTest
{
    // Explicit opt-in only. Normal installation never launches a test or quits the game.
    internal static class SmokeAutomation
    {
        internal static void TryStart(ModApiClient api, ManualLogSource log, Action<string> report)
        {
            string path = Path.Combine(Paths.ConfigPath, "SerpAutomation", RestartPlugin.Guid + ".run");
            if (!File.Exists(path)) return;
            string[] lines = File.ReadAllLines(path);
            if (lines.Length < 2 || lines[1] != "startup-smoke" || !Guid.TryParseExact(lines[0], "N", out Guid run) || run == Guid.Empty) return;
            api.WhenReady(_ => api.Ui.TryPost(() => {
                File.Delete(path); // Only our own valid scenario request is consumed.
                api.Automation.Start(run, new[] { new AutomationStep("startup-barrier", TimeSpan.FromSeconds(90), Wait) },
                    log, result => report("SMOKE " + result.Status + " " + result.Detail), quitWhenDone: true);
            }));
        }
        private static IEnumerable<AutomationWait> Wait(AutomationRun run)
        {
            yield return AutomationWait.Until("main view and actual worker checkpoint", () => MainViewModel.viewModelLoaded && StopBarrier.Installed && StopBarrier.Observed);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(3));
            if (!StopBarrier.Installed || !StopBarrier.Observed || StopBarrier.Requested)
                throw new InvalidOperationException("Startup barrier did not remain initialized and dormant.");
        }
    }
}
