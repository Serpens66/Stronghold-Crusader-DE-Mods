using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;

namespace AiGroupAdmissionTest
{
    // Explicit-request-only acceptance of AI group admission (APIShared AiMilitary). Without a
    // matching run request the plugin stays inert. The run is rooted by APIShared's durable
    // automation and the static Plugin.active field; there is no Update, coroutine or teardown path.
    [BepInPlugin(Guid, "AI Group Admission Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.56.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "AiGroupAdmissionTest_Serp";
        private static ManualLogSource log;
        private static Fixture active;
        private static bool initialized;

        private void Awake()
        {
            if (initialized) return;
            initialized = true; log = Logger;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            var lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("invalid owned request; not consumed"); return; }
            bool siege = lines[1] == Fixture.SiegeScenario;
            string map = siege
                ? Config.Bind("Fixture", "SiegeMap", "test AI overbuild eachother.map", "Existing local Skirmish map with at least two AI starts (ai-siege).").Value
                : Config.Bind("Fixture", "Map", "test AI.map", "Existing local Skirmish map with one AI start.").Value;
            int speed = Config.Bind("Fixture", "GameSpeed", 250, "Engine frame rate used during the run.").Value;
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(map, speed, runId, Stamp);
                    File.Delete(request);
                    api.Automation.Start(runId, siege ? active.SiegeSteps() : active.Steps(), log,
                        result => Stamp("finished status=" + result.Status + " step=" + result.Step + " detail=" + result.Detail), quitWhenDone: true);
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }

        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] AI_GROUP_ACCEPT " + text);
    }
}
