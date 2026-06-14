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

        public const string PluginGuid = "UnitLimit_Serp";
        public const string PluginName = "Unit Limit";
        public const string PluginVersion = "0.1.0";

        private UnitLimitRuntime runtime;
        private bool runtimeDisposed;

        public UnitLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Logger.LogDebug($"{PluginName} {PluginVersion} loaded.");

            Settings = new UnitLimitLobbyViewModel();
            runtime = new UnitLimitRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnDestroy()
        {
            Logger.LogDebug("UnitLimitPlugin OnDestroy called; keeping runtime active until application quit.");
            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;
        }

        private void OnApplicationQuit()
        {
            Logger.LogDebug("UnitLimitPlugin OnApplicationQuit called; disposing runtime.");
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
                    "UnitLimit_Serp",
                    Settings,
                    "ScriptExtenderUI/UnitLimitSettings.xaml");
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitNotificationOverlay",
                    runtime.LimitNotification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitSiegeNotificationInlineHost",
                    runtime.SiegeLimitNotification);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitTroopLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitArabTroopLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitBedouinTroopLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitEngineersLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitTunellersLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitMonkLimitInlineHost",
                    runtime.UnitLimitTooltip);
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "UnitLimitSiegeLimitInlineHost",
                    runtime.UnitLimitTooltip);

                Logger.LogDebug("Crusader library loaded; UnitLimit UI registered.");
                runtime.InitializeAfterLibraryLoaded();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error while initializing UnitLimit after library load: {ex}");
            }
        }
    }
}
