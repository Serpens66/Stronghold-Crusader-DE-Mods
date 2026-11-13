using System;
using System.IO;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;

namespace PathfindingHazardTest
{
    [BepInPlugin(Guid, "Pathfinding Hazard Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.51.0")]
    [BepInDependency("Pathfinding_Serp", "0.1.5")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PathfindingHazardTest_Serp";
        private static ManualLogSource log;
        private static Fixture active;
        private static bool initialized;
        private void Awake()
        {
            if (initialized) return;
            initialized = true; log = Logger;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Scenario.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("invalid owned request; not consumed"); return; }
            var scenario = new Scenario {
                Name = lines[1],
                SavePath = Config.Bind("Fixture", "SavePath", "", "Existing absolute prepared .sav; no effects are spawned by this test.").Value,
                ActorId = Config.Bind("Fixture", "ActorId", 0, "One-based GameUnit ID of a living human-controlled spearman in a one-man group.").Value,
                TargetX = Config.Bind("Fixture", "TargetX", -1, "Wait scenario destination tile X.").Value,
                TargetY = Config.Bind("Fixture", "TargetY", -1, "Wait scenario destination tile Y.").Value,
                HazardX = Config.Bind("Fixture", "HazardX", -1, "Tile that must genuinely be marked by the chosen production scanner.").Value,
                HazardY = Config.Bind("Fixture", "HazardY", -1, "Hazard witness tile Y.").Value,
                WaitSeconds = Config.Bind("Fixture", "WaitSeconds", 2, "Finite route wait limit; also retained finite moving-escape resume window.").Value,
                MaximumTicks = Config.Bind("Fixture", "MaximumTicks", 12000, "Observation limit in native ticks, not wall time.").Value
            };
            try { scenario.Validate(); }
            catch (Exception error) { Stamp("invalid fixture; request not consumed: " + error.Message); return; }
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    try
                    {
                        if (!api.TryGetMissionLifecycle(out var missions, out var diagnostic)) throw new InvalidOperationException(diagnostic.Reason);
                        active = new Fixture(scenario, missions, Stamp, action => { api.Ui.TryPost(action); });
                        if (!SimulationTickEvents.TryRegister(Guid, "hazard-observer", active.Tick, out string reason, 700))
                            throw new InvalidOperationException(reason);
                        File.Delete(request); // Only this validated, owned request is consumed.
                        api.Automation.Start(runId, active.Steps(), log, result => {
                            active.RequestCleanup();
                            Stamp("automation status=" + result.Status + " detail=" + result.Detail +
                                "; cleanup=" + active.CleanupStatus + "; no automatic quit (pending cleanup needs a simulation tick)");
                        }, quitWhenDone: false);
                    }
                    catch (Exception error) { active?.RequestCleanup(); Stamp("start failed: " + error); }
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }
        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] PF_HAZARD " + text);
    }
}
