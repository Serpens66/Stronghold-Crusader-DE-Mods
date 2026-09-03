using BepInEx;
using SHCDESE.API.LowLevel;
using System;

namespace EngineerSiegeFix
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class EngineerSiegeFixPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string PluginGuid = "EngineerSiegeFix_Serp";
        private const string PluginName = "Engineer Siege Fix";
        private const string PluginVersion = "0.1.0";

        private EngineerSiegeFixRuntime runtime;

        private void Awake()
        {
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} {PluginVersion} loaded; standaloneTestMod=true, gameplaySynchronized=true.");
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (runtime != null)
                return;

            try
            {
                bool currentNative = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                    Logger,
                    PluginName,
                    requireCurrentVersion: true);
                if (!currentNative)
                    return;

                runtime = new EngineerSiegeFixRuntime(
                    Logger,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()));
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"{PluginName} remains inactive because native initialization failed: {exception}");
            }
        }

        private void Update()
        {
            runtime?.PollRuntimeDiagnostics();
        }

        private void OnDestroy()
        {
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtime = null;
        }
    }
}
