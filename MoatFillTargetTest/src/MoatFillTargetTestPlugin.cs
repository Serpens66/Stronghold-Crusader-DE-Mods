using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace MoatFillTargetTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInIncompatibility("MoveMoatTest_Serp")]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MoatFillTargetTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "MoatFillTargetTest_Serp";
        private const string PluginName = "Moat Fill Target Test";
        private const string PluginVersion = "1.0.0";

        // SHCDE destroys the early plugin component during normal startup. The static runtime
        // keeps native detours and delegates rooted independently for the process lifetime.
        private static MoatFillTargetTestRuntime persistentRuntime;
        private static bool librarySubscriptionInstalled;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, " +
                "fillOnly=true, auditedScriptExtender=1.42.0, incompatibleWith=MoveMoatTest_Serp.");
            if (!libraryLoadedHandled && !librarySubscriptionInstalled)
            {
                CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
                librarySubscriptionInstalled = true;
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

                var runtime = new MoatFillTargetTestRuntime(
                    Logger,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()));
                runtime.Apply();
                persistentRuntime = runtime;
                libraryLoadedHandled = true;
                CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
                librarySubscriptionInstalled = false;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"{PluginName} remains inactive because initialization failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                "MOAT_FILL_PLUGIN_COMPONENT_DESTROYED: preserving process-lifetime runtime and native detours; " +
                $"libraryLoadedHandled={libraryLoadedHandled}, runtimeActive={persistentRuntime != null}.");
        }
    }
}
