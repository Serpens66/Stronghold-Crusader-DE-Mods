using BepInEx;
using BepInEx.Logging;
using ExtremePowers.Integration;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.IO;
using System.Threading;

namespace ExtremePowers
{
    [BepInDependency("000shcdese", "2.0.2")]
    [BepInDependency("SerpsMods_Serp", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class ExtremePowersPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "ExtremePowers_Serp", PluginName = "Extreme Powers", PluginVersion = "0.1.0";
        private static int initialized; private static IExtremePowersApiClient client; private static IDisposable demoHandle;
        private static Settings.ExtremePowersSettings rootedSettings;
        private static ManualLogSource rootedLogger;
        private static bool? capturedRealMultiplayer;
        private static int[] capturedPlayers = Array.Empty<int>();
        private static IDisposable mapStartSubscription, mapUnloadSubscription;
        public Settings.ExtremePowersSettings Settings { get; private set; }
        private void Awake()
        {
            Shared.GameplayModActivationGate.Initialize(Logger, PluginGuid, PluginName, () => Settings?.EnableMod == true);
            Shared.GameplayModActivationGate.StateChanged += OnModeStateChanged;
            Shared.DebugLogHelper.LogDebug(Logger, $"{PluginName} {PluginVersion} loaded.");
            CrusaderLibrary.Instance.LibraryLoaded += OnLibraryLoaded;
        }
        private void OnLibraryLoaded(CrusaderLibraryLoadContext context)
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return; CrusaderLibrary.Instance.LibraryLoaded -= OnLibraryLoaded;
            string dll = Path.Combine(Paths.GameRootPath, "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            rootedLogger = Logger;
            Settings = rootedSettings = new Settings.ExtremePowersSettings();
            client = LocalExtremePowersApiClient.Create(dll, context.ModuleHandle, context.Memory, GetProtocolReadiness, message => Shared.DebugLogHelper.LogDebug(rootedLogger, message));
            Settings.ApiProtocolReport = client.CompatibilityToken;
            mapStartSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Where(args => args.Phase == EventHookPhase.Pre).Subscribe(args => OnStartMap(args));
            mapUnloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Where(args => args.Phase == EventHookPhase.Post).Subscribe(_ => ResetMapSession());
            Shared.LobbyModSettingsPresetRegistration.Register(this, Logger, PluginGuid, Settings, "ScriptExtenderUI/ExtremePowersSettings.xaml");
            Settings.PropertyChanged += (_, __) => ApplySettings(); ApplySettings(); Shared.DebugLogHelper.LogDebug(Logger, client.Status);
        }
        private void OnStartMap(MapStartEventArgs args)
        {
            Shared.GameModeSnapshot mode = Shared.GameplayModActivationGate.Snapshot;
            capturedRealMultiplayer = mode.IsRealMultiplayer;
            capturedPlayers = Shared.ActivePlayerHelper.GetActivePlayerIds();
            Shared.DebugLogHelper.LogDebug(rootedLogger, "Extreme Powers map session captured: " + mode.ToDiagnosticString() + ", players=[" + string.Join(",", capturedPlayers) + "].");
            // Re-registering after the final mode capture also refreshes HUD metadata on persistent HUD instances.
            ApplySettings();
        }
        private static void ResetMapSession()
        {
            capturedRealMultiplayer = null;
            capturedPlayers = Array.Empty<int>();
        }
        private static ApiReadiness GetProtocolReadiness(string expectedToken)
        {
            if (!Shared.GameplayModActivationGate.IsAllowed)
                return ApiReadiness.Unavailable("current game mode does not allow regular gameplay mods");
            bool realMultiplayer = capturedRealMultiplayer ??
                Shared.GameplayModActivationGate.Snapshot.IsRealMultiplayer;
            if (!realMultiplayer) return ApiReadiness.Available;
            int[] players = capturedPlayers.Length == 0 ? Shared.ActivePlayerHelper.GetActivePlayerIds() : capturedPlayers;
            if (players.Length == 0) return ApiReadiness.Unavailable("real multiplayer participant roster is unresolved");
            if (rootedSettings == null) return ApiReadiness.Unavailable("protocol settings are unavailable");
            if (!rootedSettings.System_ArePerPlayerSettingsReady(players, out string reason)) return ApiReadiness.Unavailable("participant compatibility reports are incomplete: " + reason);
            if (client == null || !string.Equals(client.CompatibilityToken, expectedToken, StringComparison.Ordinal)) return ApiReadiness.Unavailable("local compatibility token changed unexpectedly");
            return client.EvaluateSession(true, rootedSettings.ApiProtocolReportData, players);
        }
        private void OnDestroy() { Shared.DebugLogHelper.LogDebug(Logger, "Plugin component destroyed during startup; keeping Extreme Powers hooks and settings rooted."); }
        private void ApplySettings()
        {
            try
            {
                demoHandle?.Dispose(); demoHandle = null;
                if (!Shared.GameplayModActivationGate.IsEnabled(Settings.EnableMod)) { client.RestoreVanilla(); return; }
                client.Apply(Settings); if (Settings.EnableGoldReplacement) demoHandle = client.InstallGoldDemo(Settings);
            }
            catch (Exception ex)
            {
                // Invalid live input must not leave a partially configured native backend active.
                demoHandle?.Dispose(); demoHandle = null;
                client.RestoreVanilla();
                Shared.DebugLogHelper.LogError(Logger, "Extreme Powers settings were rejected; Vanilla tuning restored: " + ex);
            }
        }

        private void OnModeStateChanged(bool allowed)
        {
            if (client != null && Settings != null)
                ApplySettings();
            if (!allowed)
                ResetMapSession();
        }
    }
}
