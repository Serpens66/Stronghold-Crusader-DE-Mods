using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using APIShared.Automation;
using BepInEx;

namespace CrashDiagnostics
{
    // Explicit developer acceptance only: no request file means no automation, loading or quitting.
    internal sealed partial class CrashDiagnosticsRuntime
    {
        private Guid acceptanceRunId;
        private string acceptanceScenario, acceptanceFixture, acceptanceRequest;

        private void ReadAutomationRequest()
        {
            acceptanceRequest = Path.Combine(Paths.ConfigPath, "SerpAutomation", CrashDiagnosticsPlugin.PluginGuid + ".run");
            if (!File.Exists(acceptanceRequest)) return;
            if (new FileInfo(acceptanceRequest).Length > 4096)
            { Log("ACCEPTANCE request exceeds limit; left unchanged."); return; }
            string[] lines = File.ReadAllLines(acceptanceRequest);
            if (lines.Length < 2 || (lines[1] != "startup" && lines[1] != "smoke" && lines[1] != "load")) return;
            if (!Guid.TryParseExact(lines[0], "N", out acceptanceRunId) || acceptanceRunId == Guid.Empty)
            { Log("ACCEPTANCE owned request has invalid run ID; left unchanged."); return; }
            acceptanceScenario = lines[1];
            acceptanceFixture = lines.Length > 2 && !string.IsNullOrWhiteSpace(lines[2]) ? lines[2] :
                Path.Combine(Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
        }

        private void StartRequestedAutomation()
        {
            if (acceptanceScenario == null || renderCallbacks < 150) return;
            string scenario = acceptanceScenario;
            acceptanceScenario = null;
            // BeforeRender is the validated Unity entry point; no global OS input is involved.
            client.Automation.Start(acceptanceRunId, AcceptanceSteps(scenario), log,
                result => Log("ACCEPTANCE finished status=" + result.Status + "; detail=" + result.Detail),
                quitWhenDone: true);
            File.Delete(acceptanceRequest);
        }

        private IEnumerable<AutomationStep> AcceptanceSteps(string scenario)
        {
            yield return new AutomationStep("diagnostic runtime after startup cleanup", TimeSpan.FromSeconds(20), StartupAcceptance, false);
            if (scenario != "load") yield break;
            yield return new AutomationStep("load existing fixture through normal game path", TimeSpan.FromSeconds(180), LoadAcceptance, false);
            yield return new AutomationStep("mission context and tick observations", TimeSpan.FromSeconds(60), TickAcceptance, true);
        }

        private IEnumerable<AutomationWait> StartupAcceptance(AutomationRun run)
        {
            if (!nativeActive || (NativeMethods.CD_GetStatus() & 3) != 3)
                throw new InvalidOperationException("Native capture or collector readiness is unavailable.");
            if (!File.Exists(Path.Combine(sessionDirectory, "emergency.bin")))
                throw new InvalidOperationException("The file-backed emergency capture is missing.");
            if (!finalInventoryWritten || renderCallbacks < 150)
                throw new InvalidOperationException("Post-startup inventory was not written.");
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(2));
            if (!nativeActive) throw new InvalidOperationException("The diagnostic runtime stopped during acceptance.");
            Log("ACCEPTANCE startup assertions=4 session=" + sessionDirectory);
        }

        private IEnumerable<AutomationWait> LoadAcceptance(AutomationRun run)
        {
            foreach (var wait in run.LoadSave(acceptanceFixture)) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
        }

        private IEnumerable<AutomationWait> TickAcceptance(AutomationRun run)
        {
            var mission = run.CurrentMission;
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 5);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(2));
            if (Interlocked.Read(ref missionId) != mission.SessionId || Volatile.Read(ref tickKnown) != 1)
                throw new InvalidOperationException("Current-mission context or real map tick observations are missing.");
            if (Volatile.Read(ref missionTickMarker) != 2)
                throw new InvalidOperationException("A post-mission Unity heartbeat has not confirmed the tick marker.");
            Log("ACCEPTANCE mission assertions=3 id=" + mission.SessionId + "; tick=" + Interlocked.Read(ref lastTick));
        }
    }
}
