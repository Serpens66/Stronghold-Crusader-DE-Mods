using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace AIDefensePatrolTest
{
    [BepInDependency(ScriptExtenderGuid, ScriptExtenderVersion)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class AIDefensePatrolTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string ScriptExtenderVersion = "1.42.0";
        private const string PluginGuid = "AIDefensePatrolTest_Serp";
        private const string PluginName = "AI Defense Patrol Test";
        private const string PluginVersion = "0.1.0";

        private static AIDefensePatrolTestRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            Version actualVersion = typeof(CrusaderLibrary).Assembly.GetName().Version;
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; correctionActive=true, modSettings=false, " +
                $"requiredScriptExtender={ScriptExtenderVersion}, actualScriptExtender={actualVersion}.");

            if (actualVersion == null || actualVersion != new Version(1, 42, 0, 0))
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"{PluginName} remains inactive because the loaded Script Extender is not exactly 1.42.0.0.");
                return;
            }

            if (libraryLoadedHandled || libraryLoadedSubscriptionInstalled)
                return;

            libraryLoadedSubscriptionInstalled = true;
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr moduleHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true))
                {
                    StopListening();
                    return;
                }

                AIDefensePatrolTestRuntime runtime = new AIDefensePatrolTestRuntime(Logger);
                runtime.Apply(moduleHandle, memory);
                persistentRuntime = runtime;
                libraryLoadedHandled = true;
                StopListening();
            }
            catch (Exception exception)
            {
                StopListening();
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"{PluginName} remains inactive because initialization failed: {exception}");
            }
        }

        private void StopListening()
        {
            if (!libraryLoadedSubscriptionInstalled)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            libraryLoadedSubscriptionInstalled = false;
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"AI_DEFENSE_PATROL_PLUGIN_COMPONENT_DESTROYED: preserving process-lifetime runtime and hook; " +
                $"libraryLoadedHandled={libraryLoadedHandled}, runtimeActive={persistentRuntime != null}.");
        }
    }
}
