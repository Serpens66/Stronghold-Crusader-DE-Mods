using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;

namespace ConstructionCostsTest
{
    [BepInPlugin(Guid, "Construction Costs Test", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.3")]
    [BepInDependency("APIShared_Serp", "0.56.0")]
    [BepInDependency("BuildingCosts_Serp")]
    [BepInDependency("ExtraFeatures_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "ConstructionCostsTest_Serp";
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
            string map = Config.Bind("Fixture", "Map", "test AI.map", "Existing local Skirmish map with legal nearby tower and well terrain.").Value;
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(map, lines[1], runId, Stamp);
                    File.Delete(request);
                    api.Automation.Start(runId, active.Steps(), log,
                        result => { active.Stop(); Stamp("finished status=" + result.Status + " detail=" + result.Detail); }, quitWhenDone: true);
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }
        private static void Stamp(string text) => log.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] CONSTRUCTION_COSTS_ACCEPT " + text);
    }
}
