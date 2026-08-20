// Feature: Plugin bootstrap for the Bugfixes and QoL mod.
using BepInEx;
using BepInEx.Bootstrap;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using System;

namespace BugfixesAndQoL
{
    [BepInDependency(ScriptExtenderGuid, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(LegacySomeSettingsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInIncompatibility(LegacyTroopMovementFixGuid)]
    [BepInIncompatibility(TroopMovementFix2Guid)]
    [BepInIncompatibility(TroopMovementFix3Guid)]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class BugfixesAndQoLPlugin : BaseUnityPlugin
    {
        private const string ScriptExtenderGuid = "000shcdese";
        private const string LegacyTroopMovementFixGuid = "TroopMovementFix_Serp";
        private const string TroopMovementFix2Guid = "TroopMovementFix2_Serp";
        private const string TroopMovementFix3Guid = "TroopMovementFix3_Serp";
        private const string LegacySomeSettingsGuid = "SomeSettings_Serp";

        public const string PluginGuid = "BugfixesAndQoL_Serp";
        public const string PluginName = "Bugfixes and QoL";
        public const string PluginVersion = "1.0.34";

        private static DisplayResolutionPersistenceHook displayResolutionPersistenceHook;
        private static DisplayResolutionDiagnostic displayResolutionDiagnostic;
        private static SteamLobbyInvitePrompt steamLobbyInvitePrompt;
        private BugfixesAndQoLRuntime runtime;
        private object observedLobby;
        private int observedLobbyMemberCount = -1;
        private bool lobbyPlayerResolutionAttempted;
        private bool marketGoodsVisualRefreshFailureLogged;

        public BugfixesAndQoLViewModel Settings { get; private set; }

        private void Awake()
        {
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            bool legacySomeSettingsLoaded = Chainloader.PluginInfos.ContainsKey(LegacySomeSettingsGuid);
            if (legacySomeSettingsLoaded)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"The obsolete mod '{LegacySomeSettingsGuid}' is loaded together with {PluginName}. " +
                    "Uninstall it to avoid duplicate or conflicting features.");
            }

            // Pass the startup result into the view model so the warning occupies no UI space otherwise.
            Settings = new BugfixesAndQoLViewModel(legacySomeSettingsLoaded);
            try
            {
                // Install before FatControler.Start loads settings.cfg and begins screen monitoring.
                displayResolutionPersistenceHook =
                    new DisplayResolutionPersistenceHook(Logger, Settings);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL display-resolution persistence could not be initialized; Vanilla behavior remains active: {ex}");
            }

            try
            {
                // Temporary and behavior-neutral; kept separate for clean removal after diagnosis.
                displayResolutionDiagnostic = new DisplayResolutionDiagnostic(Logger);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    Logger,
                    $"Bugfixes and QoL display-resolution diagnostic could not be initialized: {ex}");
            }

            runtime = new BugfixesAndQoLRuntime(Logger, Settings);
            CrusaderLibrary.Instance.LibraryLoaded += OnCrusaderLibraryLoaded;
        }

        private void InitializeLocalPlayerTracking()
        {
            // These publishers outlive the BepInEx component and retain the callbacks for the process lifetime.
            SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.PropertyChanged +=
                (_, __) =>
                {
                    RefreshLobbyLocalPlayerId();
                    // The game fills MainViewModel.GameSprites after the script extender loads.
                    // A hub change, including opening the settings, is the safe point to retry visuals.
                    try
                    {
                        Settings.RefreshMarketGoodsOrderVisuals();
                    }
                    catch (Exception ex)
                    {
                        // A visual retry must never escape through PropertyChanged and abort tab registration.
                        if (!marketGoodsVisualRefreshFailureLogged)
                        {
                            marketGoodsVisualRefreshFailureLogged = true;
                            Shared.DebugLogHelper.LogError(
                                Logger,
                                $"Bugfixes and QoL market-goods visual refresh failed; text controls remain usable: {ex}");
                        }
                    }
                };
            MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post)
                {
                    Settings.TrySetLocalPlayerId(GamePlayerManagerAPI.Instance.GetLocalPlayerId());
                    steamLobbyInvitePrompt?.TryInitialize();
                }
            });
            RefreshLobbyLocalPlayerId();
        }

        private void RefreshLobbyLocalPlayerId()
        {
            Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
            if (lobby == null)
            {
                observedLobby = null;
                observedLobbyMemberCount = -1;
                lobbyPlayerResolutionAttempted = false;
                return;
            }

            int memberCount = lobby.members?.Count ?? -1;
            if (ReferenceEquals(observedLobby, lobby) &&
                observedLobbyMemberCount == memberCount &&
                lobbyPlayerResolutionAttempted)
            {
                return;
            }

            observedLobby = lobby;
            observedLobbyMemberCount = memberCount;
            lobbyPlayerResolutionAttempted = true;
            Settings.TrySetLocalPlayerId(GameNetworkAPI.GetLocalPlayerId());
        }

        private void OnCrusaderLibraryLoaded(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            // Construct the editor rows before subscribing to hub changes, so even an early
            // settings event can only refresh an already complete 20-item collection.
            try
            {
                Settings.InitializeMarketGoodsOrderEditor(Logger);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL market-order editor initialization failed: {ex}");
            }

            InitializeLocalPlayerTracking();

            try
            {
                runtime.InitializeSurrenderFeature();
                GameXAMLManagerAPI.Instance.RegisterBinding(
                    "BugfixesAndQoLSurrenderButtonHost",
                    runtime.SurrenderButton);
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL surrender UI/network initialization failed; the button remains inactive: {ex}");
            }

            // Keep UI registration independent so one native feature cannot hide the whole mod.
            try
            {
                runtime.InitializeNative(
                    libraryHandle,
                    memory,
                    Shared.DebugLogHelper.IsCurrentNativeLibraryVersion());
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL native runtime initialization failed; unaffected features may continue: {ex}");
            }

            try
            {
                Shared.LobbyModSettingsPresetRegistration.Register(
                    this,
                    Logger,
                    PluginGuid,
                    Settings,
                    "ScriptExtenderUI/BugfixesAndQoLSettings.xaml");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL settings UI registration failed: {ex}");
            }

            try
            {
                steamLobbyInvitePrompt = new SteamLobbyInvitePrompt(Logger, Settings);
                steamLobbyInvitePrompt.TryInitialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL Steam lobby-invite prompt could not be initialized; Vanilla invite handling remains active: {ex}");
            }

            try
            {
                runtime.ApplySettings();
                Shared.DebugLogHelper.LogDebug(Logger, "Crusader library loaded; Bugfixes and QoL initialized.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(Logger, $"Bugfixes and QoL settings reconciliation failed; already initialized features remain active: {ex}");
            }
        }
    }
}
