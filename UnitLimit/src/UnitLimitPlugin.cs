using BepInEx;
using BepInEx.Configuration;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace UnitLimit
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class UnitLimitPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";

        public const string PluginGuid = "UnitLimit_Serp";
        public const string PluginName = "Unit Limit";
        public const string PluginVersion = "1.0.90";

        private UnitLimitRuntime runtime;
        private int libraryInitializationStarted;
        private ConfigEntry<bool> verboseUnitEventLogging;

        public UnitLimitLobbyViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");

            verboseUnitEventLogging = Config.Bind(
                "Diagnostics",
                "VerboseUnitEventLogging",
                false,
                "Log every UnitLimit unit-cache event and count change. Keep disabled during normal play to avoid large logs.");
            Settings = new UnitLimitLobbyViewModel();
            runtime = new UnitLimitRuntime(Logger, Settings, verboseUnitEventLogging.Value);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // A late subscription can race with the regular event raise; initialize only once.
            if (Interlocked.Exchange(ref libraryInitializationStarted, 1) != 0)
                return;

            CrusaderLibrary.Instance.LibraryLoaded -= OnCrusaderLibraryLoaded;

            TryInitializeStage("native version diagnostics", () => Shared.DebugLogHelper.ReportNativeLibraryVersion(Logger, PluginName));
            TryInitializeStage("localized names", Settings.RefreshLocalizedNames);
            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    "UnitLimit_Serp",
                    Settings,
                    "ScriptExtenderUI/UnitLimitSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"UnitLimit settings registration failed; gameplay runtime stopped fail-closed: {ex}");
                return;
            }

            RegisterBinding("UnitLimitNotificationOverlay", runtime.LimitNotification);
            RegisterBinding("UnitLimitSiegeNotificationInlineHost", runtime.SiegeLimitNotification);
            string[] tooltipTargets =
            {
                "UnitLimitTroopLimitInlineHost",
                "UnitLimitArabTroopLimitInlineHost",
                "UnitLimitBedouinTroopLimitInlineHost",
                "UnitLimitEngineersLimitInlineHost",
                "UnitLimitTunellersLimitInlineHost",
                "UnitLimitMonkLimitInlineHost",
                "UnitLimitSiegeLimitInlineHost",
            };
            foreach (string target in tooltipTargets)
                RegisterBinding(target, runtime.UnitLimitTooltip);

            try
            {
                runtime.InitializeAfterLibraryLoaded();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; UnitLimit runtime initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Error while initializing UnitLimit after library load: {ex}");
            }
        }

        private void TryInitializeStage(string stageName, Action initialize)
        {
            try { initialize(); }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"UnitLimit {stageName} failed; independent stages continue: {ex}");
            }
        }

        private void RegisterBinding(string target, object value)
        {
            TryInitializeStage($"binding '{target}'", () => GameXAMLManagerAPI.Instance.RegisterBinding(target, value));
        }
    }
}
