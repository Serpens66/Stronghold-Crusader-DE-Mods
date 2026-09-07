using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Diagnostics;
using System.Reflection;

namespace FearFactorNeutralizationTest
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class FearFactorNeutralizationTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "FearFactorNeutralizationTest_Serp";
        private const string PluginName = "Fear Factor Neutralization Test";
        private const string PluginVersion = "0.1.0";

        private static ManualLogSource persistentLog;
        private static FearFactorNeutralizationRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            persistentLog = Logger;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, alwaysEnabled=true, " +
                "gameplaySynchronized=true, scope=allPlayersAndAI.");

            if (!libraryLoadedHandled && !libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryLoadedHandled)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            try
            {
                bool hashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!hashMatches)
                    return;

                FearFactorNeutralizationRuntime initializedRuntime =
                    new FearFactorNeutralizationRuntime(persistentLog, context);
                initializedRuntime.Apply(hashMatches);
                persistentRuntime = initializedRuntime;
                libraryLoadedHandled = true;

                CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = false;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"FEAR_FACTOR_NEUTRALIZATION_DISABLED: initialization failed before activation; exception={exception}");
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
                    FearFactorNativeDefinition.AuditedScriptExtenderCommit,
                    StringComparison.OrdinalIgnoreCase) >= 0;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: auditedVersion={FearFactorNativeDefinition.AuditedScriptExtenderVersion}, " +
                    $"auditedCommit={FearFactorNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedCommitMatch={auditedCommit}.");
                if (!auditedCommit)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        "Script Extender differs from the audited 2.3.0 commit; native activation remains hash-gated.");
                }
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    persistentLog,
                    $"Script Extender identity could not be reported: {exception.Message}");
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                persistentLog,
                $"FEAR_FACTOR_PLUGIN_COMPONENT_DESTROYED: preserving process-lifetime runtime and hooks; " +
                $"libraryLoadedHandled={libraryLoadedHandled}, runtimeActive={persistentRuntime != null}.");
        }
    }
}
