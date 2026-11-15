using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;

namespace CastlePlannerRotationTest
{
    [BepInPlugin(Guid, "CastlePlanner Rotation Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.51.0")]
    [BepInDependency("CastlePlanner_Serp", "0.8.52")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "CastlePlannerRotationTest_Serp";
        private static ManualLogSource log;
        private static Fixture active;
        private static bool initialized;

        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            log = Logger;
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !Fixture.Owns(lines[1])) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("invalid owned request; not consumed"); return; }
            string scenario = lines[1];
            string map = Config.Bind("Fixture", "Map", "4spielerkarte.map", "Existing local Skirmish map used for the real selection and restart.").Value;
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(map, scenario, runId, Stamp);
                    File.Delete(request);
                    api.Automation.Start(runId, active.Steps(), log,
                        result => Stamp("finished status=" + result.Status + " detail=" + result.Detail), quitWhenDone: true);
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }

        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] CASTLE_ROTATION " + text);
    }
}
