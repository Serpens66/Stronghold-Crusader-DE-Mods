using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Diagnostics;
using System.Reflection;

namespace StockpileAccessFixTest
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StockpileAccessFixTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "StockpileAccessFixTest_Serp";
        private const string PluginName = "Stockpile Access Fix Test";
        private const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static StockpileAccessFixTestRuntime runtime;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, gameplaySynchronized=true, " +
                "confirmationTicks=50, retryCooldownTicks=200, supportedWorkers=8.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                    return;

                runtime = new StockpileAccessFixTestRuntime(
                    persistentLog,
                    context.Memory,
                    unchecked((ulong)context.ModuleHandle.ToInt64()),
                    referenceHashMatches);
                runtime.Apply();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"STOCKPILE_ACCESS_DIAGNOSTIC_DISABLED: reason=initialization failed, exception={exception}");
            }
        }

        private static void LogScriptExtenderIdentity()
        {
            try
            {
                Assembly assembly = typeof(CrusaderLibrary).Assembly;
                string informational = assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "unknown";
                string location = assembly.Location;
                string fileVersion = string.IsNullOrEmpty(location)
                    ? "unknown"
                    : FileVersionInfo.GetVersionInfo(location).FileVersion;
                bool auditedCommit = informational.IndexOf(
                    StockpileAccessFixNativeDefinition.AuditedScriptExtenderCommit,
                    StringComparison.OrdinalIgnoreCase) >= 0;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: auditedVersion={StockpileAccessFixNativeDefinition.AuditedScriptExtenderVersion}, " +
                    $"auditedCommit={StockpileAccessFixNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedCommitMatch={auditedCommit}.");
                if (!auditedCommit)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        "Script Extender differs from the audited 2.2.0 commit; native recovery remains hash-gated.");
                }
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    persistentLog,
                    $"Script Extender identity could not be reported: {exception.Message}");
            }
        }
    }
}
