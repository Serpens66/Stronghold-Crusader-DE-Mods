using BepInEx;
using BepInEx.Bootstrap;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;

namespace PreplacedTest
{
    [BepInDependency(ScriptExtenderGuid, "2.4.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class PreplacedTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "PreplacedTest_Serp";
        private const string PluginName = "Preplaced Test";
        private const string PluginVersion = "0.1.1";
        private static readonly string[] ConflictingPluginGuids =
        {
            "ActiveAIVDetector_Serp", "ExtraFeatures_Serp", "BugfixesAndQoL_Serp", "CastlePlanner_Serp"
        };

        private static PreplacedTestRuntime persistentRuntime;
        private static bool subscribed;
        private static bool handled;
        private readonly HashSet<string> warnedConflicts = new HashSet<string>(StringComparer.Ordinal);

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(Logger,
                $"{PluginName} {PluginVersion} loaded; activeTestFixes=legacy-tower-timer+player-specific-economy-grid+scoped-wood-score-floor, NetworkMode=1, settings=false, " +
                "minimumScriptExtender=2.4.0, testedScriptExtender=2.5.0, " +
                "auditedCommit=5f02af6d074af7c741ebdaaccb48add39eba1bf4.");
            WarnAboutConflicts("Awake");
            if (!handled && !subscribed)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
                subscribed = true;
            }
        }

        private void Start() => WarnAboutConflicts("Start");

        private void Update() => persistentRuntime?.PollFrame();

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
