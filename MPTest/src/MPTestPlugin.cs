using BepInEx;
using BepInEx.Configuration;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace MPTest
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MPTestPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "MPTest_Serp";
        public const string PluginName = "MPTest";
        public const string PluginVersion = "1.1.0";

        private static MPTestRuntime runtime;
        private static bool libraryLoadedHandled;
        private ConfigEntry<int> delayIncomingProbeMilliseconds;

        private void Awake()
        {
            if (runtime != null)
                return;

            delayIncomingProbeMilliseconds = Config.Bind(
                "ChoreProbe",
                "DelayIncomingProbeMs",
                0,
                "Delays incoming opcode-111 probe chores on non-host peers. Use 0 normally and 500 for the barrier test. Values above 2500 are clamped.");

            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            runtime = new MPTestRuntime(Logger, () => delayIncomingProbeMilliseconds.Value);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(
                Logger,
                "MPTestPlugin OnDestroy called; keeping process-lifetime runtime and LibraryLoaded registration active.");
        }

        private void OnApplicationQuit()
        {
            Shared.DebugLogHelper.LogDebug(
                Logger,
                "MPTestPlugin OnApplicationQuit called; process-lifetime native Chore hooks remain rooted until process exit.");
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (libraryLoadedHandled)
                return;

            try
            {
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "MPTestWoodcutterSpawnButtonHost",
                    runtime.ButtonViewModel);

                runtime.Initialize(libraryHandle);
                libraryLoadedHandled = true;
                Shared.DebugLogHelper.LogInfo(Logger, "MPTest Crusader library loaded; UI binding and runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"MPTest initialization failed: {ex}");
            }
        }
    }
}
