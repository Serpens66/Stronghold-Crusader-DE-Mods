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

        private OxTetherIdleFixTestRuntime runtime;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, gameplaySynchronized=true, " +
                "auditedScriptExtender=1.42.0, auditedCommit=171d68e155a8f98c5f8c4ee154d9af154c9a2443.");
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (runtime != null)
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

                runtime = new OxTetherIdleFixTestRuntime(Logger);
                runtime.Apply();
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
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtime = null;
        }
    }
}
