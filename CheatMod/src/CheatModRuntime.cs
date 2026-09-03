using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CheatMod
{
    internal sealed class CheatModRuntime
    {
        private delegate void SetExtremePowersEnabledDelegate(
            GamePlayerManagerAPI self,
            bool enabled);

        internal const int FullExtremePowersMana = 7000;
        internal const int RechargeTickInterval = 40;

        private readonly ManualLogSource log;
        private readonly CheatModSettingsViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private bool initialized;
        private bool mapActive;
        private bool tickSubscribed;
        private Hook extremePowersEnabledHook;
        private SetExtremePowersEnabledDelegate setExtremePowersEnabledTrampoline;

        public CheatModRuntime(ManualLogSource log, CheatModSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Shared.GameplayModActivationGate.Initialize(log, CheatModPlugin.PluginGuid, CheatModPlugin.PluginName, () => settings.EnableMod);
            Shared.GameplayModActivationGate.StateChanged += OnModeAllowedChanged;
        }

        public void InitializeAfterLibraryLoaded()
        {
            if (initialized)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => BeginMap()));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap()));
            settings.SettingChanged += OnSettingChanged;
            try
            {
                InstallExtremePowersEnabledHook();
            }
            catch (Exception ex)
            {
                // Map-start gating and self-disabling remain safe even when another
                // managed detour prevents observation of later setter calls.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Cheat Mod could not observe Extreme Powers activation changes: {ex}");
            }
            initialized = true;
        }

        private void BeginMap()
        {
            mapActive = !Shared.GameModeHelper.IsMapEditor();
            RefreshTickSubscription("map start");
            Shared.DebugLogHelper.LogDebug(
                log,
                mapActive
                    ? $"Cheat Mod map runtime started; extreme-powers recharge interval={RechargeTickInterval} ticks, mana={FullExtremePowersMana}, tickSubscribed={tickSubscribed}."
                    : "Cheat Mod remains inactive in the map editor.");
        }

        private void EndMap()
        {
            mapActive = false;
            RefreshTickSubscription("map unload");
        }

        private void OnModeAllowedChanged(bool allowed)
        {
            if (!allowed)
                mapActive = false;
            RefreshTickSubscription("game-mode gate changed");
        }

        private void OnGameTick(int tick)
        {
            // This also catches an external native write which bypassed the managed setter.
            if (!GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled())
            {
                RefreshTickSubscription("Vanilla Extreme Powers disabled outside the Script Extender setter");
                return;
            }

            if (tick % RechargeTickInterval != 0)
            {
                return;
            }

            try
            {
                RefillHumanPlayers();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Cheat Mod extreme-powers recharge failed: {ex}");
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(CheatModSettingsViewModel.EnableMod) ||
                propertyName == nameof(CheatModSettingsViewModel.EndlessExtremePowers))
            {
                RefreshTickSubscription($"setting changed: {propertyName}");
            }
        }

        private void InstallExtremePowersEnabledHook()
        {
            MethodInfo method = typeof(GamePlayerManagerAPI).GetMethod(
                nameof(GamePlayerManagerAPI.SetLocalPlayerExtremePowersEnabled),
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(bool) },
                null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(
                    typeof(GamePlayerManagerAPI).FullName,
                    nameof(GamePlayerManagerAPI.SetLocalPlayerExtremePowersEnabled));

            extremePowersEnabledHook = new Hook(
                method,
                (SetExtremePowersEnabledDelegate)SetExtremePowersEnabledHook);
            setExtremePowersEnabledTrampoline =
                extremePowersEnabledHook.GenerateTrampoline<SetExtremePowersEnabledDelegate>();
            Shared.DebugLogHelper.LogDebug(
                log,
                "Cheat Mod listens for Script Extender Extreme Powers activation changes.");
        }

        private void SetExtremePowersEnabledHook(GamePlayerManagerAPI self, bool enabled)
        {
            setExtremePowersEnabledTrampoline(self, enabled);
            RefreshTickSubscription(
                $"SetLocalPlayerExtremePowersEnabled({enabled})");
        }

        private void RefreshTickSubscription(string reason)
        {
            bool shouldSubscribe = mapActive &&
                Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
                settings.EndlessExtremePowers &&
                GamePlayerManagerAPI.Instance.IsLocalPlayerExtremePowersEnabled();
            if (tickSubscribed == shouldSubscribe)
                return;

            if (shouldSubscribe)
                GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            else
                GameTimeManagerAPI.Instance.OnTick -= OnGameTick;

            tickSubscribed = shouldSubscribe;
            Shared.DebugLogHelper.LogDebug(
                log,
                $"Cheat Mod extreme-powers tick updater {(shouldSubscribe ? "enabled" : "disabled")}: {reason}.");
        }

        private static void RefillHumanPlayers()
        {
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            foreach (int playerId in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                if (players.IsPlayerIdValid(playerId) && !players.IsAIPlayer(playerId))
                    players.SetLocalPlayerExtremePowersMana(playerId, FullExtremePowersMana);
            }
        }
    }
}
