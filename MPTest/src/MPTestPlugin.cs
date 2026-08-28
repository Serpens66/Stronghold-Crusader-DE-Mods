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
        public const string PluginVersion = "1.3.3";

        private static MPTestRuntime runtime;
        private static bool libraryLoadedHandled;
        private ConfigEntry<bool> comprehensiveBarrierTestEnabled;
        private ConfigEntry<int> barrierTestIncomingDelayMilliseconds;
        private ConfigEntry<int> commandsPerClick;
        private ConfigEntry<bool> fullTrafficCaptureOnClick;
        private ConfigEntry<int> fullTrafficCaptureDurationMilliseconds;

        private void Awake()
        {
            if (runtime != null)
                return;

            comprehensiveBarrierTestEnabled = Config.Bind(
                "ChoreProbe",
                "ComprehensiveBarrierTestEnabled",
                true,
                "Enables the comprehensive no-op Chore test. Only non-host peers delay incoming opcode-111 probes.");
            barrierTestIncomingDelayMilliseconds = Config.Bind(
                "ChoreProbe",
                "BarrierTestIncomingDelayMs",
                500,
                "Real-time delay for incoming opcode-111 probes on non-host peers while SyncEvents pass normally. Values above 2500 are clamped.");
            commandsPerClick = Config.Bind(
                "ChoreProbe",
                "CommandsPerClick",
                5,
                "Number of consecutive no-op Chores queued by one multiplayer button click. Values are clamped to 1 through 10.");
            fullTrafficCaptureOnClick = Config.Bind(
                "ChoreProbe",
                "FullTrafficCaptureOnClick",
                false,
                "Captures exact native Chore metadata, hashes, bounded payload bytes, and managed GameAction calls after an MPTest button click.");
            fullTrafficCaptureDurationMilliseconds = Config.Bind(
                "ChoreProbe",
                "FullTrafficCaptureDurationMs",
                20000,
                "Duration of optional full Chore traffic capture after a button click. Values are clamped to 1000 through 60000 ms.");

            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            Shared.DebugLogHelper.LogInfo(
                Logger,
                $"{PluginName} comprehensive test profile: enabled={comprehensiveBarrierTestEnabled.Value}, " +
                $"nonHostIncomingDelayMs={barrierTestIncomingDelayMilliseconds.Value}, " +
                $"commandsPerClick={commandsPerClick.Value}, " +
                $"fullTrafficCaptureOnClick={fullTrafficCaptureOnClick.Value}, " +
                $"fullTrafficCaptureDurationMs={fullTrafficCaptureDurationMilliseconds.Value}.");
            runtime = new MPTestRuntime(
                Logger,
                () => comprehensiveBarrierTestEnabled.Value
                    ? barrierTestIncomingDelayMilliseconds.Value
                    : 0,
                () => commandsPerClick.Value,
                () => fullTrafficCaptureOnClick.Value,
                () => fullTrafficCaptureDurationMilliseconds.Value);
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
                bool referenceHashMatches = Shared.DebugLogHelper.ReportNativeLibraryVersion(
                        Logger,
                        PluginName,
                        requireCurrentVersion: false);

                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "MPTestWoodcutterSpawnButtonHost",
                    runtime.ButtonViewModel);

                runtime.Initialize(libraryHandle, memory, referenceHashMatches);
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
