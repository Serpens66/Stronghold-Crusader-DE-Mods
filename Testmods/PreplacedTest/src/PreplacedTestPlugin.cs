using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;

namespace PreplacedTest
{
    [BepInDependency(ScriptExtenderGuid, "2.7.1")]
    [BepInDependency("APIShared_Serp", "0.3.6")]
    [BepInDependency("fixes", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PreplacedTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "PreplacedTest_Serp";
        private const string PluginName = "Preplaced Test";
        private const string PluginVersion = "0.1.2";
        private const string TestedScriptExtenderVersion = "2.8.0";
        private const string TestedScriptExtenderCommit = "5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33";
        private const string TestedApiSharedVersion = "0.3.7";
        private const string TestedRedBirdVersion = "1.3.2.0";
        private static readonly string[] ConflictingPluginGuids =
        {
            "ActiveAIVDetector_Serp", "ExtraFeatures_Serp", "BugfixesAndQoL_Serp", "CastlePlanner_Serp",
            "EnemyGatePathfindingTest_Serp"
        };

        private static PreplacedTestRuntime persistentRuntime;
        private static bool subscribed;
        private static bool handled;
        private readonly HashSet<string> warnedConflicts = new HashSet<string>(StringComparer.Ordinal);

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger,
                $"{PluginName} {PluginVersion} loaded; activeTestFixes=legacy-tower-timer+player-specific-economy-grid+scoped-wood-score-floor, NetworkMode=1, settings=false, " +
                $"minimumScriptExtender=2.7.1, testedScriptExtender={TestedScriptExtenderVersion}, " +
                $"auditedCommit={TestedScriptExtenderCommit}.");
            LogCompatibility();
            WarnAboutConflicts("Awake");
            if (!handled && !subscribed)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
                subscribed = true;
            }
        }

        private void Start() => WarnAboutConflicts("Start");

        private void WarnAboutConflicts(string phase)
        {
            List<string> loaded = new List<string>();
            foreach (string guid in ConflictingPluginGuids)
                if (Chainloader.PluginInfos.ContainsKey(guid) && warnedConflicts.Add(guid))
                    loaded.Add(guid);
            if (loaded.Count != 0)
                Shared.DebugLogHelper.LogWarning(Logger,
                    "PREPLACED_CONFLICT: phase=" + phase + "; disable these plugins for a clean test because native hooks or AI behavior can overlap: " +
                    string.Join(",", loaded) + ".");
        }

        private void LogCompatibility()
        {
            string scriptExtender = LoadedPluginVersion(ScriptExtenderGuid);
            string apiShared = LoadedPluginVersion("APIShared_Serp");
            string fixes = LoadedPluginVersion("fixes");
            string redBird = typeof(RedBird.X64.Hooks.X64InlineHook).Assembly.GetName().Version?.ToString() ?? "unknown";
            bool exactTestedVersions = scriptExtender == TestedScriptExtenderVersion &&
                apiShared == TestedApiSharedVersion && redBird == TestedRedBirdVersion;
            Shared.DebugLogHelper.LogInfo(Logger,
                $"PREPLACED_COMPATIBILITY: scriptExtender={scriptExtender}; apiShared={apiShared}; redBird={redBird}; fixes={fixes}; " +
                $"testedScriptExtender={TestedScriptExtenderVersion}; auditedCommit={TestedScriptExtenderCommit}; exactTestedVersions={exactTestedVersions}.");
            if (!exactTestedVersions)
                Shared.DebugLogHelper.LogWarning(Logger,
                    "PREPLACED_COMPATIBILITY_DEVIATION: loaded dependency versions differ from the fully tested set; native hash and signature validation remain authoritative and fail closed.");
        }

        private static string LoadedPluginVersion(string guid) =>
            Chainloader.PluginInfos.TryGetValue(guid, out PluginInfo plugin)
                ? plugin.Metadata.Version.ToString()
                : "not-loaded";

        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (handled)
                return;
            try
            {
                PreplacedTestRuntime runtime = new PreplacedTestRuntime(Logger);
                runtime.InstallEventDiagnostics();
                bool hashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger, PluginName, requireCurrentVersion: false);
                runtime.TryInstallNativeDiagnostics(context, hashMatches);
                persistentRuntime = runtime;
                handled = true;
                CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
                subscribed = false;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger,
                    $"{PluginName} initialization failed before event diagnostics became usable: {ex}");
            }
        }

    }
}
