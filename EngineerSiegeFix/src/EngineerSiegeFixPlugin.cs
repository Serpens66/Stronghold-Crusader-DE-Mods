using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace EngineerSiegeFix
{
    [BepInDependency(ScriptExtenderGuid, "2.0.2")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EngineerSiegeFixPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "EngineerSiegeFix_Serp";
        private const string PluginName = "Engineer Siege Fix";
        private const string PluginVersion = "0.1.0";

        // SHCDE destroys the early BepInEx component during normal startup. Keep all
        // process-wide state independent of that Unity object's lifetime.
        private static ManualLogSource processLog;
        private static EngineerSiegeFixRuntime runtime;
        private static bool libraryEventSubscribed;

        private void Awake()
        {
            processLog = processLog ?? Logger;
            Shared.DebugLogHelper.LogInfo(
                processLog,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, gameplaySynchronized=true.");
            if (!libraryEventSubscribed)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryEventSubscribed = true;
            }
        }

        private static void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (runtime != null)
                return;

            try
            {
                bool currentNative = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    processLog,
                    PluginName,
                    requireCurrentVersion: true);
                if (!currentNative)
                    return;

                runtime = new EngineerSiegeFixRuntime(
                    processLog,
                    context.Memory,
                    unchecked((ulong)context.ModuleHandle.ToInt64()));
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    processLog,
                    $"{PluginName} remains inactive because native initialization failed: {exception}");
            }
        }
    }
}
