using System;
using System.IO;
using APIShared;
using APIShared.Automation;
using BepInEx;
using BepInEx.Logging;

namespace PathfindingLoadTest
{
    [BepInPlugin(Guid, "Pathfinding Load Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.49.0")]
    [BepInDependency("Pathfinding_Serp", "0.1.2")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "PathfindingLoadTest_Serp";
        private static ManualLogSource log;
        private static Fixture active;
        private static bool registered;

        private void Awake()
        {
            if (registered) return;
            registered = true;
            log = Logger;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            var lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var run) || run == System.Guid.Empty)
            { Stamp("Invalid owned automation request; no run started."); return; }
            string map = Config.Bind("Fixture", "Map", "test AI.map",
                "Unique local Skirmish map filename; missing/unsuitable maps fail the test.").Value;
            string scenario = lines[1];
            if (scenario == "startup-probe") StartupProbe.Enable();
            File.Delete(request); // Only our recognized scenario is consumed.
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(map, scenario, Stamp);
                    api.Automation.Start(run, active.Steps(), log,
                        result => { active?.Complete(); StartupProbe.Flush(Stamp); Stamp("finished status=" + result.Status + " detail=" + result.Detail); }, quitWhenDone: true);
                })) Stamp("Unable to dispatch owned test to Unity.");
            });
        }

        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] PF_LOAD " + text);
    }
}
