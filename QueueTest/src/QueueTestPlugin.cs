using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace QueueTest
{
    [BepInDependency(ScriptExtenderGuid, "2.2.0")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class QueueTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        public const string PluginGuid = "QueueTest_Serp";
        public const string PluginName = "Queue Test";
        public const string PluginVersion = "1.0.0";

        // SHCDE destroys the original plugin component during startup. The runtime therefore
        // remains rooted statically and owns process-lifetime hooks and subscriptions.
        private static QueueRuntime runtime;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            if (runtime != null)
                return;

            runtime = new QueueRuntime(Logger);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                if (!Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: true,
                        logSuccess: false))
                {
                    return;
                }

                if (context == null)
                    throw new ArgumentNullException(nameof(context));

                runtime.Install(context);
                libraryLoadedHandled = true;
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"QueueTest initialization failed; Vanilla behavior remains active: {exception}");
            }
        }
    }
}
