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

        public const string PluginGuid = "StartConditions";
        public const string PluginName = "Start Conditions";
        public const string PluginVersion = "0.1.0";

        private StartConditionsRuntime runtime;
        private bool runtimeDisposed;

        public StartConditionsLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

            Settings = new StartConditionsLobbyViewModel();
            runtime = new StartConditionsRuntime(Logger, Settings);
            runtime.SubscribeHooks();
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogInfo("StartConditionsPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogInfo("StartConditionsPlugin OnApplicationQuit called; disposing runtime.");
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
                Settings.RefreshLocalizedNames(message => Logger.LogInfo(message));
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "StartConditions",
                    Settings,
                    "ScriptExtenderUI/StartConditionsSettings.xaml");

                Logger.LogInfo("Crusader library loaded; StartConditions UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing StartConditions after library load: {ex}");
            }
        }
    }
}
