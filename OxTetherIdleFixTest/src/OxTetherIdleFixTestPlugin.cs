using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace OxTetherIdleFixTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OxTetherIdleFixTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "OxTetherIdleFixTest_Serp";
        private const string PluginName = "Ox Tether Idle Fix Test";
        private const string PluginVersion = "0.1.0";

        // SHCDE destroys the early BepInEx manager component during normal startup.
        // Keep the non-Unity runtime alive independently for the process lifetime.
        private static OxTetherIdleFixTestRuntime persistentRuntime;
        private static bool libraryLoadedSubscriptionInstalled;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, gameplaySynchronized=true, " +
                "auditedScriptExtender=1.42.0, auditedCommit=171d68e155a8f98c5f8c4ee154d9af154c9a2443.");
            if (!libraryLoadedHandled && !libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
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
                    return;
                }

                OxTetherIdleFixTestRuntime initializedRuntime =
                    new OxTetherIdleFixTestRuntime(Logger);
                initializedRuntime.Apply();
                persistentRuntime = initializedRuntime;
                libraryLoadedHandled = true;

                CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = false;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"{PluginName} remains inactive because initialization failed: {exception}");
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"OX_IDLE_PLUGIN_COMPONENT_DESTROYED: preserving process-lifetime runtime and subscriptions; " +
                $"libraryLoadedHandled={libraryLoadedHandled}, runtimeActive={persistentRuntime != null}.");
        }
    }
}
