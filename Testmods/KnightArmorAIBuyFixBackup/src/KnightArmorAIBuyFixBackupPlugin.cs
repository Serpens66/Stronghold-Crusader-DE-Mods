using BepInEx;
using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Diagnostics;
using System.Reflection;

namespace KnightArmorAIBuyFixBackup
{
    [BepInDependency(ScriptExtenderGuid, "2.3.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class KnightArmorAIBuyFixBackupPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "KnightArmorAIBuyFixBackup_Serp";
        private const string PluginName = "Knight Armor AI Buy Fix Backup";
        private const string PluginVersion = "0.1.0";

        // SHCDE destroys early BepInEx components during startup, so process-lifetime
        // ownership must not depend on this Unity component remaining alive.
        private static ManualLogSource persistentLog;
        private static KnightArmorAIBuyFixBackupRuntime runtime;
        private static bool librarySubscriptionInstalled;

        private void Awake()
        {
            persistentLog = Logger;
            LogScriptExtenderIdentity();
            Shared.DebugLogHelper.LogWarning(
                persistentLog,
                $"{PluginName} {PluginVersion} loaded; standaloneBackupMod=true, gameplaySynchronized=true, settings=false.");

            if (librarySubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
            librarySubscriptionInstalled = true;
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            try
            {
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    persistentLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!referenceHashMatches)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        $"{PluginName} remains inactive because the canonical CrusaderDE.dll hash did not match; Vanilla remains active.");
                    return;
                }

                runtime = new KnightArmorAIBuyFixBackupRuntime(
                    persistentLog,
                    context,
                    referenceHashMatches);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    persistentLog,
                    $"{PluginName} could not install its recruitment detour; Vanilla remains active: {exception}");
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
                    KnightArmorAIBuyFixNativeDefinition.AuditedScriptExtenderCommit,
                    StringComparison.OrdinalIgnoreCase) >= 0;
                Shared.DebugLogHelper.LogInfo(
                    persistentLog,
                    $"Script Extender identity: auditedCommit={KnightArmorAIBuyFixNativeDefinition.AuditedScriptExtenderCommit}, " +
                    $"assembly={assembly.FullName}, fileVersion={fileVersion}, informationalVersion={informational}, " +
                    $"auditedCommitMatch={auditedCommit}.");
                if (!auditedCommit)
                {
                    Shared.DebugLogHelper.LogWarning(
                        persistentLog,
                        "Script Extender differs from the audited commit; native installation remains DLL-hash-gated.");
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
