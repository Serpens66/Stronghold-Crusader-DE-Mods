using BepInEx;
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
        public const string PluginVersion = "1.0.0";

        private static MPTestRuntime runtime;
        private static bool libraryLoadedHandled;

        private void Awake()
        {
            if (runtime != null)
                return;

            Shared.DebugLogHelper.LogInfo(Logger, $"{PluginName} {PluginVersion} loaded.");
            runtime = new MPTestRuntime(Logger);
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
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtime = null;
            libraryLoadedHandled = false;
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

                runtime.Initialize();
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
