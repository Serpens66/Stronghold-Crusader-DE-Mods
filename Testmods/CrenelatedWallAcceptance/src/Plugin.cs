using System;
using System.IO;
using APIShared;
using BepInEx;
using BepInEx.Logging;

namespace CrenelatedWallAcceptance
{
    // Runs only for an owned SerpAutomation request; a normal start leaves the game untouched.
    [BepInPlugin(Guid, "Crenelated Wall Acceptance", "0.1.0")]
    [BepInDependency("000shcdese", "2.14.1")]
    [BepInDependency("APIShared_Serp", "0.62.0")]
    [BepInDependency("CrenelatedWallTest_Serp", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "CrenelatedWallAcceptance_Serp";
        internal const string Scenario = "crenel-acceptance", EditorScenario = "crenel-editor", TopologyScenario = "crenel-vanilla-topology",
            HeightsScenario = "crenel-heights", AiScenario = "crenel-ai", StressScenario = "crenel-stress", DiagonalScenario = "crenel-diagonal-walk", LookScenario = "crenel-look", LookCornersScenario = "crenel-look-corners";
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
            if (lines.Length < 2 || (lines[1] != Scenario && lines[1] != EditorScenario && lines[1] != TopologyScenario && lines[1] != HeightsScenario && lines[1] != AiScenario && lines[1] != StressScenario && lines[1] != DiagonalScenario && lines[1] != LookScenario && lines[1] != LookCornersScenario)) return;
            if (!System.Guid.TryParseExact(lines[0], "N", out var runId) || runId == System.Guid.Empty)
            { Stamp("invalid owned request; not consumed"); return; }
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ => {
                if (!api.Ui.TryPost(() => {
                    active = new Fixture(runId.ToString("N"), lines[1], Stamp);
                    File.Delete(request); // Only the owned and valid request is consumed.
                    api.Automation.Start(runId, active.Steps(), log,
                        result => { active.Stop(); Stamp("finished status=" + result.Status + " detail=" + result.Detail); }, quitWhenDone: true);
                })) Stamp("Unity dispatch unavailable; request not consumed");
            });
        }

        internal static void Stamp(string text) =>
            log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] CRENEL_ACCEPT " + text);
    }
}
