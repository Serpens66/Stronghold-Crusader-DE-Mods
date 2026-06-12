using BepInEx;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;

namespace UnitLimit
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitLimitPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "UnitLimit";
        public const string PluginName = "Unit Limit";
        public const string PluginVersion = "0.1.0";

        private UnitLimitRuntime runtime;
        private bool runtimeDisposed;

        public UnitLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded.");

            Settings = new UnitLimitLobbyViewModel();
            runtime = new UnitLimitRuntime(Logger, Settings);
            runtime.SubscribeHooks();
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogInfo("UnitLimitPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogInfo("UnitLimitPlugin OnApplicationQuit called; disposing runtime.");
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
                Settings.RefreshLocalizedNames();
                GameXAMLManagerAPI.Instance.RegisterLobbyModSettings(
                    this,
                    "UnitLimit",
                    Settings,
                    "ScriptExtenderUI/UnitLimitSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitNotificationOverlay",
                    runtime.LimitNotification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitSiegeNotificationInlineHost",
                    runtime.SiegeLimitNotification);

                Logger.LogInfo("Crusader library loaded; UnitLimit UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing UnitLimit after library load: {ex}");
            }
        }
    }
}
