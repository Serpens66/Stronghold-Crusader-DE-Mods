using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace StartConditions
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class StartConditionsPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "StartConditions_Serp";
        public const string PluginName = "Start Conditions";
        public const string PluginVersion = "1.0.3";

        private StartConditionsRuntime runtime;
        private bool runtimeDisposed;

        public StartConditionsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            Settings = new StartConditionsLobbyViewModel();
            runtime = new StartConditionsRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "StartConditionsPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Shared.DebugLogHelper.LogDebug(Logger, "StartConditionsPlugin OnApplicationQuit called; disposing runtime.");
            DisposeRuntime();
        }

        private void DisposeRuntime()
        {
            if (runtimeDisposed)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
            runtime?.Dispose();
            runtimeDisposed = true;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            try
            {
                Settings.RefreshLocalizedNames(message => Shared.DebugLogHelper.LogDebug(Logger, message));
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "StartConditions_Serp",
                    Settings,
                    "ScriptExtenderUI/StartConditionsSettings.xaml");

                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; StartConditions UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing StartConditions after library load: {ex}");
            }
        }
    }
}
