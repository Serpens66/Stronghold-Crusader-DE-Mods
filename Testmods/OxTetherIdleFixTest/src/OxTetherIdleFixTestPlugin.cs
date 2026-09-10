using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace OxTetherIdleFixTest
{
    [BepInDependency(ScriptExtenderGuid, "2.4.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OxTetherIdleFixTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "OxTetherIdleFixTest_Serp";
        private const string PluginName = "Ox Tether Idle Fix Test";
        private const string PluginVersion = "0.1.3";

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
                "Script Extender compatibility comes from info.json; auditedCommit=5d5719c1002aec043d331162d72b2e7f3111b34b.");
            if (!libraryLoadedHandled && !libraryLoadedSubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                libraryLoadedSubscriptionInstalled = true;
            }
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryLoadedHandled)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));

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
