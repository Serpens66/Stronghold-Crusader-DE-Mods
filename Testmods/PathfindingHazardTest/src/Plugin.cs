using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;

namespace PathfindingHazardTest
{
    // Lifetime: Awake only reads the request; the run lives in APIShared's automation driver and the
    // process-lifetime SimulationTickEvents publisher (static Plugin.active), not in this component,
    // which the SHCDE startup cleanup destroys.
    [BepInPlugin(Guid, "Pathfinding Hazard Test", "0.2.0")]
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
            string map = Config.Bind("Fixture", "Map", "test AI.map", "Local Skirmish map; a flat open area near the own keep is searched.").Value;
            int waitSeconds = Config.Bind("Fixture", "WaitSeconds", 2, "Finite route wait of the bounded cases (1..15 game seconds).").Value;
            try { Scenario.ValidateWaitSeconds(waitSeconds); }
            catch (Exception error) { Stamp("invalid fixture; request not consumed: " + error.Message); return; }
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(map, Scenario.Select(lines[1]), waitSeconds, Stamp);
                    File.Delete(request); // Only the owned and syntactically valid request is consumed.
                    api.Automation.Start(runId, active.Steps(), log, result => {
                        active.Stop();
                        Stamp("finished status=" + result.Status + " detail=" + result.Detail);
                    }, quitWhenDone: true);
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }
        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] PF_HAZARD " + text);
    }
}
