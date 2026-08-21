using BepInEx.Logging;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using UnityEngine;

namespace CastlePlanner
{
    public sealed class CastlePlannerSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly ManualLogSource log;
        private readonly AivFileCatalog catalog = new AivFileCatalog();
        private readonly LobbyModSettingsStorage runtimeStorage;
        private readonly RuntimePersistedState runtimeState =
            new RuntimePersistedState();
        private bool enableClientFeatures = true;
        private bool enableMod = true;
        private bool blueprints = true;
        private bool spawnCastle;
        private string selectedCastle;
        private string spawnSelectedCastle = string.Empty;
        private string spawnInventoryManifest = string.Empty;
        private Noesis.ComboBoxItem[] spawnCastleOptions = Array.Empty<Noesis.ComboBoxItem>();
        private string[] spawnCastleOptionNames = Array.Empty<string>();
        private readonly CastleSpawnLobbyState spawnLobbyState =
            new CastleSpawnLobbyState();
        private readonly string[] decodedManifestSources = new string[9];
        private readonly IReadOnlyDictionary<string, string>[] decodedInventories =
            new IReadOnlyDictionary<string, string>[9];
        private long compatibilityChangedTimestamp = Stopwatch.GetTimestamp();
        private bool compatibilityBroadcastPending;
        private bool spawnSelectionResetPending;
        private bool spawnOptionsRebuildPending;
        private bool workshopCatalogReadyObserved;
        private bool lobbyCompatibilitySyncAvailable;
        private KeyCode blueprintHotkey;
        private double blueprintIconScale;
        private double blueprintIconAlpha;
        private bool isCapturingHotkey;

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public CastlePlannerSettingsViewModel(
            ManualLogSource log,
            string pluginAssemblyLocation)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (string.IsNullOrWhiteSpace(pluginAssemblyLocation))
            {
                throw new ArgumentException(
                    "The plugin assembly location is required.",
                    nameof(pluginAssemblyLocation));
            }

            RefreshCastleOptions(notifySelectionChange: false);
            blueprintHotkey = KeyCode.None;
            blueprintIconScale = 1.0;
            blueprintIconAlpha = 0.3;

            runtimeStorage = new LobbyModSettingsStorage(
                pluginAssemblyLocation,
                CastlePlannerPlugin.PluginGuid + ".Runtime");
            runtimeStorage.Load(runtimeState);
            NormalizeRuntimeState();
            TryMigrateLegacySettings(pluginAssemblyLocation);
            SpawnSelectedCastleData[GetLocalPlayerId()] = spawnSelectedCastle;
            PublishLocalInventory(forceBroadcast: false);
            RebuildSpawnCastleOptions();

            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            AssignHotkeyCommand = new RelayCommand(BeginHotkeyCapture);
            ClearHotkeyCommand = new RelayCommand(ClearHotkey);
            HotkeyInputCommand =
                new ParameterRelayCommand(CaptureNoesisHotkeyInput);
            PropertyChanged += OnNetworkCompatibilityPropertyChanged;
        }

        internal event Action SettingsChanged;
        internal event Action BlueprintVisualSettingsChanged;
        internal event Action HotkeyCaptureRequested;

        public ObservableCollection<string> CastleOptions { get; } =
            new ObservableCollection<string>();

        public Noesis.ComboBoxItem[] SpawnCastleOptions => spawnCastleOptions;
        public string[] SpawnSelectedCastleData { get; } = new string[9];
        public string[] SpawnInventoryManifestData { get; } = new string[9];

        public ICommand AssignHotkeyCommand { get; }
        public ICommand ClearHotkeyCommand { get; }
        public ICommand HotkeyInputCommand { get; }
        public RelayCommand ResetToDefaultCommand { get; }

        public int AvailableFileCount => CastleOptions.Count;

        internal void RefreshCastleOptions() =>
            RefreshCastleOptions(notifySelectionChange: true);

        private void RefreshCastleOptions(bool notifySelectionChange)
        {
            IReadOnlyList<string> discovered = catalog.Discover(message =>
                Shared.DebugLogHelper.LogWarning(log, message));
            if (CastleOptions.Count == discovered.Count)
            {
                bool unchanged = true;
                for (int index = 0; index < discovered.Count; index++)
                    unchanged &= string.Equals(CastleOptions[index], discovered[index], StringComparison.Ordinal);
                if (unchanged)
                {
                    NormalizeSpawnSelectionAfterCompleteCatalogRefresh();
                    PublishLocalInventory(forceBroadcast: false);
                    RebuildSpawnCastleOptions();
                    return;
                }
            }

            string previous = selectedCastle ?? string.Empty;
            CastleOptions.Clear();
            foreach (string option in discovered)
                CastleOptions.Add(option);
            NormalizeSpawnSelectionAfterCompleteCatalogRefresh();
            string defaultCastle = CastleOptions.Count > 0 ? CastleOptions[0] : string.Empty;
            string normalized = NormalizeCastle(previous, defaultCastle);
            bool selectionChanged = !string.Equals(selectedCastle, normalized, StringComparison.Ordinal);
            selectedCastle = normalized;
            PublishLocalInventory(forceBroadcast: false);
            RebuildSpawnCastleOptions();
            OnPropertyChanged(nameof(AvailableFileCount));
            OnPropertyChanged(nameof(InventoryText));
            if (selectionChanged)
                OnPropertyChanged(nameof(SelectedCastle));
            if (notifySelectionChange && selectionChanged)
                SettingsChanged?.Invoke();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"CastlePlanner refreshed AIVJSON choices including Steam Workshop content; count={CastleOptions.Count}.");
        }

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableClientFeaturesText => SerpLocalization.Get("CastlePlanner.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("CastlePlanner.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("CastlePlanner.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("CastlePlanner.EnableHostFeaturesHelp");
        public string TitleText => SerpLocalization.Get("CastlePlanner.Title");
        public string HelpText => SerpLocalization.Get("CastlePlanner.Help");
        public string CastleText => SerpLocalization.Get("CastlePlanner.Castle");
        public string CastleHelpText => SerpLocalization.Get("CastlePlanner.CastleHelp");
        public string BlueprintsText => SerpLocalization.Get("CastlePlanner.Blueprints");
        public string BlueprintsHelpText => SerpLocalization.Get("CastlePlanner.BlueprintsHelp");
        public string SpawnCastleText => SerpLocalization.Get("CastlePlanner.SpawnCastle");
        public string SpawnCastleHelpText => SerpLocalization.Get("CastlePlanner.SpawnCastleHelp");
        public string HotkeyText => SerpLocalization.Get("CastlePlanner.Hotkey");
        public string HotkeyHelpText => SerpLocalization.Get("CastlePlanner.HotkeyHelp");
        public string ClearText => SerpLocalization.Get("Common.Clear");
        public string ClearHelpText => SerpLocalization.Get("CastlePlanner.ClearHelp");
        public string LocalOptionsText => SerpLocalization.Get("CastlePlanner.LocalOptions");
        public string CastleSectionTitleText => SerpLocalization.Get("CastlePlanner.CastleSectionTitle");
        public string SpawnSelectionTitleText => SerpLocalization.Get("CastlePlanner.SpawnSelectionTitle");
        public string SpawnSelectionNoticeText => SerpLocalization.Get("CastlePlanner.SpawnSelectionNotice");
        public string PlacementControlsTitleText => SerpLocalization.Get("CastlePlanner.PlacementControlsTitle");
        public string InventoryText => string.Format(
            SerpLocalization.Get("CastlePlanner.Inventory"),
            AvailableFileCount);

        [Shared.PresetLocal]
        public bool EnableClientFeatures
        {
            get => enableClientFeatures;
            set
            {
                if (enableClientFeatures == value)
                    return;

                enableClientFeatures = value;
                OnPropertyChanged(nameof(EnableClientFeatures));
                OnPropertyChanged(nameof(IsBlueprintMode));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner local activation changed to {enableClientFeatures}.");
                SettingsChanged?.Invoke();
            }
        }

        [SyncHostOnly]
        public bool EnableMod
        {
            get => enableMod;
            set
            {
                if (!CanMutateSetting(nameof(EnableMod)) || enableMod == value)
                    return;

                enableMod = value;
                OnPropertyChanged(nameof(EnableMod));
                OnPropertyChanged(nameof(IsSpawnMode));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner host activation changed to {enableMod}.");
                SettingsChanged?.Invoke();
            }
        }

        [Shared.PresetLocal]
        public bool Blueprints
        {
            get => blueprints;
            set
            {
                if (blueprints == value)
                    return;

                blueprints = value;
                OnPropertyChanged(nameof(Blueprints));
                OnPropertyChanged(nameof(IsBlueprintMode));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner local Blueprints changed to {blueprints}.");
                SettingsChanged?.Invoke();
            }
        }

        [SyncHostOnly]
        public bool SpawnCastle
        {
            get => spawnCastle;
            set
            {
                if (!CanMutateSetting(nameof(SpawnCastle)))
                    return;

                if (spawnCastle == value)
                    return;

                spawnCastle = value;
                OnPropertyChanged(nameof(SpawnCastle));
                OnPropertyChanged(nameof(IsSpawnMode));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner host Spawn Castle changed to {spawnCastle}.");
                SettingsChanged?.Invoke();
            }
        }

        [Shared.PresetLocal]
        public string SelectedCastle
        {
            get => selectedCastle;
            set
            {
                string normalized = NormalizeCastle(value, string.Empty);
                if (string.Equals(selectedCastle, normalized, StringComparison.Ordinal))
                    return;

                selectedCastle = normalized;
                OnPropertyChanged(nameof(SelectedCastle));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner local AIVJSON selection changed to '{selectedCastle}'.");
                SettingsChanged?.Invoke();
            }
        }

        [SyncPerPlayer]
        public string SpawnSelectedCastle
        {
            get => spawnSelectedCastle;
            set
            {
                string normalized = NormalizeSpawnCastle(value);
                if (string.Equals(spawnSelectedCastle, normalized, StringComparison.Ordinal))
                    return;

                spawnSelectedCastle = normalized;
                int localPlayerId = GetLocalPlayerId();
                SpawnSelectedCastleData[localPlayerId] = normalized;
                MarkCompatibilityChanged();
                OnPropertyChanged(nameof(SpawnSelectedCastle));
                OnPropertyChanged(nameof(SelectedSpawnCastleOption));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner personal spawn AIVJSON selection changed: " +
                    $"playerId={localPlayerId}, selection='{spawnSelectedCastle}'.");
                SettingsChanged?.Invoke();
            }
        }

        [SyncPerPlayer, DoNotPersist]
        public string SpawnInventoryManifest
        {
            get => spawnInventoryManifest;
            set
            {
                value = value ?? string.Empty;
                if (string.Equals(spawnInventoryManifest, value, StringComparison.Ordinal))
                    return;

                spawnInventoryManifest = value;
                int localPlayerId = GetLocalPlayerId();
                SpawnInventoryManifestData[localPlayerId] = value;
                InvalidateDecodedInventory(localPlayerId);
                MarkCompatibilityChanged();
                OnPropertyChanged(nameof(SpawnInventoryManifest));
            }
        }

        public Noesis.ComboBoxItem SelectedSpawnCastleOption
        {
            get
            {
                if (spawnCastleOptions.Length == 0)
                    return null;
                int index = Array.FindIndex(
                    spawnCastleOptionNames,
                    name => string.Equals(name, spawnSelectedCastle, StringComparison.OrdinalIgnoreCase));
                return spawnCastleOptions[index >= 0 ? index : 0];
            }
            set
            {
                if (value == null || !value.IsEnabled)
                    return;
                int index = Array.IndexOf(spawnCastleOptions, value);
                if (index >= 0 && index < spawnCastleOptionNames.Length)
                    SpawnSelectedCastle = spawnCastleOptionNames[index];
            }
        }

        [Shared.PresetLocal]
        public int BlueprintHotkey
        {
            get => (int)blueprintHotkey;
            set => SetHotkey(NormalizeKeyCode(value));
        }

        [Shared.PresetLocal]
        public double BlueprintIconScale
        {
            get => blueprintIconScale;
            set
            {
                double normalized = NormalizeIconScale(value);
                if (Math.Abs(blueprintIconScale - normalized) < 0.0001)
                    return;

                blueprintIconScale = normalized;
                OnPropertyChanged(nameof(BlueprintIconScale));
                OnPropertyChanged(nameof(BlueprintIconScaleText));
                BlueprintVisualSettingsChanged?.Invoke();
            }
        }

        [Shared.PresetLocal]
        public double BlueprintIconAlpha
        {
            get => blueprintIconAlpha;
            set
            {
                double normalized = NormalizeIconAlpha(value);
                if (Math.Abs(blueprintIconAlpha - normalized) < 0.0001)
                    return;

                blueprintIconAlpha = normalized;
                OnPropertyChanged(nameof(BlueprintIconAlpha));
                OnPropertyChanged(nameof(BlueprintIconAlphaText));
                BlueprintVisualSettingsChanged?.Invoke();
            }
        }

        public string BlueprintIconScaleText =>
            BlueprintIconScale.ToString("0.00");

        public string BlueprintIconAlphaText =>
            BlueprintIconAlpha.ToString("0.00");

        public string HotkeyDisplayText =>
            blueprintHotkey == KeyCode.None
                ? SerpLocalization.Get("CastlePlanner.NotAssigned")
                : GetKeyDisplayName(blueprintHotkey);

        public string HotkeyCaptureButtonText =>
            isCapturingHotkey
                ? SerpLocalization.Get("CastlePlanner.PressAnyKey")
                : SerpLocalization.Get("CastlePlanner.AssignKey");

        public bool IsCapturingHotkey => isCapturingHotkey;
        public bool IsBlueprintMode => enableClientFeatures && blueprints;
        public bool IsSpawnMode => enableMod && spawnCastle;
        internal KeyCode BlueprintHotkeyCode => blueprintHotkey;
        internal float BlueprintIconScaleValue => (float)blueprintIconScale;
        internal float BlueprintIconAlphaValue => (float)blueprintIconAlpha;

        internal bool TryGetBlueprintHudPosition(
            out double normalizedX,
            out double normalizedY)
        {
            normalizedX = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionX);
            normalizedY = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionY);
            return runtimeState.HasBlueprintHudPosition;
        }

        internal void SaveBlueprintHudPosition(
            double normalizedX,
            double normalizedY)
        {
            runtimeState.HasBlueprintHudPosition = true;
            runtimeState.BlueprintHudPositionX =
                NormalizeUnitValue(normalizedX);
            runtimeState.BlueprintHudPositionY =
                NormalizeUnitValue(normalizedY);
            runtimeStorage.Save(runtimeState);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint HUD position saved: " +
                $"x={runtimeState.BlueprintHudPositionX:0.000}, " +
                $"y={runtimeState.BlueprintHudPositionY:0.000}.");
        }

        internal void LogBlueprintHudMessage(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, message);
        }

        internal bool TryResolveSelectedFile(out string fullPath)
        {
            return catalog.TryResolve(selectedCastle, out fullPath);
        }

        internal bool TryCreateSpawnPlan(
            IEnumerable<int> humanPlayerIds,
            out List<CastleSpawnRequest> requests,
            out string error)
        {
            requests = new List<CastleSpawnRequest>();
            error = string.Empty;
            int[] playerIds = (humanPlayerIds ?? Enumerable.Empty<int>())
                .Where(id => id > 0 && id < SpawnSelectedCastleData.Length)
                .Distinct()
                .OrderBy(id => id)
                .ToArray();
            if (playerIds.Length == 0)
            {
                error = "No human player IDs were available for castle spawning.";
                return false;
            }

            if (!TryValidateCompatibilityReadiness(playerIds, out error))
                return false;

            foreach (int playerId in playerIds)
            {
                if (string.IsNullOrEmpty(SpawnInventoryManifestData[playerId]))
                {
                    error = $"Player {playerId} has not reported an AIVJSON inventory.";
                    return false;
                }
                if (SpawnSelectedCastleData[playerId] == null)
                {
                    error = $"Player {playerId} has not reported a personal spawn selection.";
                    return false;
                }
            }

            IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> inventories =
                DecodeInventories(playerIds);

            foreach (int playerId in playerIds)
            {
                string castle = SpawnSelectedCastleData[playerId] ?? string.Empty;
                if (string.IsNullOrEmpty(castle))
                    continue;

                if (!CastleSpawnCompatibility.IsAvailableToAll(
                        castle,
                        playerIds,
                        inventories,
                        out string hash))
                {
                    error = $"Player {playerId} selected AIVJSON '{castle}', but its name and SHA-256 are not identical for every human player.";
                    return false;
                }
                if (!catalog.TryResolve(castle, out string filePath))
                {
                    error = $"Locally unavailable AIVJSON for player {playerId}: '{castle}'.";
                    return false;
                }
                if (!catalog.TryCaptureHash(castle, out string actualHash, out string hashError))
                {
                    error = hashError;
                    return false;
                }
                if (!string.Equals(hash, actualHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Locally changed AIVJSON for player {playerId}: '{castle}'. " +
                        $"Announced SHA-256={hash}, current SHA-256={actualHash}.";
                    return false;
                }

                requests.Add(new CastleSpawnRequest(playerId, castle, hash, filePath));
            }

            return true;
        }

        internal void PublishSpawnCompatibilityState()
        {
            compatibilityBroadcastPending = true;
            ObserveLobbyCompatibilityState(forceObservation: true);
            RebuildSpawnCastleOptions();
        }

        internal void ObserveLobbyCompatibilityState() =>
            ObserveLobbyCompatibilityState(forceObservation: false);

        internal void RequestLobbyCompatibilityBroadcast()
        {
            compatibilityBroadcastPending = true;
            ObserveLobbyCompatibilityState(forceObservation: true);
        }

        internal void SetLobbyCompatibilitySyncAvailable()
        {
            lobbyCompatibilitySyncAvailable = true;
        }

        private void ObserveLobbyCompatibilityState(bool forceObservation)
        {
            bool steamworksReady = Shared.WorkshopContentPaths.IsSteamworksReady();
            if (steamworksReady && !workshopCatalogReadyObserved)
            {
                workshopCatalogReadyObserved = true;
                RefreshCastleOptions(notifySelectionChange: false);
                compatibilityBroadcastPending = true;
            }

            Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
            if (lobby == null)
            {
                // activeLobby may disappear during the map transition while gameMembers
                // already contains the authoritative multiplayer roster. Do not mistake
                // that transition for leaving the session and erase the converged snapshot.
                if (HasActiveMultiplayerGameMembers())
                    return;

                CastleSpawnLobbyChange leaveChange = spawnLobbyState.Observe(null, null);
                if (leaveChange.MembershipChanged)
                {
                    ClearCompatibilitySlots(leaveChange.SlotsToClear);
                    int localPlayerId = GetLocalPlayerId();
                    SpawnInventoryManifestData[localPlayerId] = spawnInventoryManifest;
                    SpawnSelectedCastleData[localPlayerId] = spawnSelectedCastle;
                    MarkCompatibilityChanged();
                    RebuildSpawnCastleOptions();
                }
                compatibilityBroadcastPending = false;
                return;
            }

            CaptureLobbyHumanPlayers(
                lobby,
                out Dictionary<int, ulong> players,
                out bool hasUnresolvedPlayers);
            CastleSpawnLobbyChange change = spawnLobbyState.Observe(
                lobby.id.m_SteamID,
                players);
            if (change.MembershipChanged)
            {
                ClearCompatibilitySlots(change.SlotsToClear);
                compatibilityBroadcastPending = true;
                MarkCompatibilityChanged();
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner lobby roster changed: lobby={lobby.id.m_SteamID}, " +
                    $"sessionChanged={change.SessionChanged}, " +
                    $"humanPlayers=[{string.Join(",", players.Keys.OrderBy(id => id))}], " +
                    $"unresolved={hasUnresolvedPlayers}, clearedSlots=[{string.Join(",", change.SlotsToClear)}].");
            }

            if (change.MembershipChanged)
                RebuildSpawnCastleOptions();
            else
                ApplyPendingSpawnOptionsRebuild();

            int networkLocalPlayerId = GameNetworkAPI.GetLocalPlayerId();
            bool localPlayerResolved =
                networkLocalPlayerId > 0 && networkLocalPlayerId < SpawnInventoryManifestData.Length &&
                players.ContainsKey(networkLocalPlayerId);
            ApplyPendingSpawnSelectionReset();
            if ((compatibilityBroadcastPending || forceObservation) &&
                localPlayerResolved && !hasUnresolvedPlayers)
            {
                PublishLocalInventory(forceBroadcast: true);
                SpawnSelectedCastleData[networkLocalPlayerId] = spawnSelectedCastle;
                OnPropertyChanged(nameof(SpawnSelectedCastle));
                compatibilityBroadcastPending = false;
                MarkCompatibilityChanged();
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner advertised personal spawn compatibility after lobby convergence: " +
                    $"playerId={networkLocalPlayerId}, roster=[{string.Join(",", players.Keys.OrderBy(id => id))}].");
            }

        }

        private void PublishLocalInventory(bool forceBroadcast)
        {
            string manifest = CastleSpawnCompatibility.EncodeManifest(
                catalog.CaptureHashes(message => Shared.DebugLogHelper.LogWarning(log, message)));
            int localPlayerId = GetLocalPlayerId();
            bool changed = !string.Equals(spawnInventoryManifest, manifest, StringComparison.Ordinal);
            spawnInventoryManifest = manifest;
            SpawnInventoryManifestData[localPlayerId] = manifest;
            if (changed || forceBroadcast)
            {
                InvalidateDecodedInventory(localPlayerId);
                MarkCompatibilityChanged();
                OnPropertyChanged(nameof(SpawnInventoryManifest));
            }
        }

        private void OnNetworkCompatibilityPropertyChanged(
            object sender,
            PropertyChangedEventArgs args)
        {
            if (args.PropertyName != nameof(SpawnInventoryManifestData) &&
                args.PropertyName != nameof(SpawnSelectedCastleData))
            {
                return;
            }

            if (args.PropertyName == nameof(SpawnInventoryManifestData))
            {
                for (int playerId = 1; playerId < SpawnInventoryManifestData.Length; playerId++)
                    InvalidateDecodedInventory(playerId);
            }
            MarkCompatibilityChanged();
            spawnOptionsRebuildPending = true;
        }

        private void RebuildSpawnCastleOptions()
        {
            spawnOptionsRebuildPending = false;
            LobbyHumanPlayerSnapshot lobbyPlayers = GetLobbyHumanPlayerSnapshot();
            int[] humanPlayerIds = lobbyPlayers.PlayerIds;
            bool multiplayer = lobbyPlayers.HumanMemberCount > 1;
            bool allReported = humanPlayerIds.All(id =>
                id > 0 && id < SpawnInventoryManifestData.Length &&
                !string.IsNullOrEmpty(SpawnInventoryManifestData[id]) &&
                SpawnSelectedCastleData[id] != null) &&
                !lobbyPlayers.HasUnresolvedPlayers;
            IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> inventories =
                DecodeInventories(humanPlayerIds);

            var options = new List<Noesis.ComboBoxItem>();
            var names = new List<string>();
            Noesis.ComboBoxItem none = CreateSpawnOption(
                SerpLocalization.Get("CastlePlanner.NoCastle"),
                true,
                SerpLocalization.Get("CastlePlanner.NoCastleHelp"));
            options.Add(none);
            names.Add(string.Empty);

            foreach (string castle in CastleOptions)
            {
                bool compatible = !multiplayer ||
                    (allReported && CastleSpawnCompatibility.IsAvailableToAll(
                         castle,
                         humanPlayerIds,
                         inventories,
                         out _));
                options.Add(CreateSpawnOption(
                    castle,
                    compatible,
                    compatible
                        ? CastleHelpText
                        : SerpLocalization.Get("CastlePlanner.CastleUnavailableForAllHelp")));
                names.Add(castle);
            }

            spawnCastleOptions = options.ToArray();
            spawnCastleOptionNames = names.ToArray();
            OnPropertyChanged(nameof(SpawnCastleOptions));
            OnPropertyChanged(nameof(SelectedSpawnCastleOption));

            if (multiplayer && allReported && !string.IsNullOrEmpty(spawnSelectedCastle) &&
                !CastleSpawnCompatibility.IsAvailableToAll(
                     spawnSelectedCastle,
                     humanPlayerIds,
                     inventories,
                     out _))
            {
                // Incoming settings are applied under the Extender's echo-suppression flag.
                // Defer the local synchronized reset to the persistent lobby observer.
                spawnSelectionResetPending = true;
            }
        }

        private void ApplyPendingSpawnOptionsRebuild()
        {
            if (spawnOptionsRebuildPending)
                RebuildSpawnCastleOptions();
        }

        private void ApplyPendingSpawnSelectionReset()
        {
            if (!spawnSelectionResetPending)
                return;
            spawnSelectionResetPending = false;

            LobbyHumanPlayerSnapshot lobbyPlayers = GetLobbyHumanPlayerSnapshot();
            if (lobbyPlayers.HumanMemberCount <= 1 ||
                lobbyPlayers.HasUnresolvedPlayers ||
                string.IsNullOrEmpty(spawnSelectedCastle))
            {
                return;
            }

            int[] playerIds = lobbyPlayers.PlayerIds;
            bool allReported = playerIds.All(id =>
                id > 0 && id < SpawnInventoryManifestData.Length &&
                !string.IsNullOrEmpty(SpawnInventoryManifestData[id]) &&
                SpawnSelectedCastleData[id] != null);
            if (!allReported || CastleSpawnCompatibility.IsAvailableToAll(
                    spawnSelectedCastle,
                    playerIds,
                    DecodeInventories(playerIds),
                    out _))
            {
                return;
            }

            string invalidSelection = spawnSelectedCastle;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Personal spawn selection reset because it is not identical for every lobby participant: '{invalidSelection}'.");
            SpawnSelectedCastle = string.Empty;
        }

        private static Noesis.ComboBoxItem CreateSpawnOption(
            string content,
            bool enabled,
            string tooltip)
        {
            var item = new Noesis.ComboBoxItem
            {
                Content = content,
                IsEnabled = enabled,
                ToolTip = tooltip
            };
            Noesis.ToolTipService.SetShowDuration(item, 60000);
            if (!enabled)
                item.Background = new Noesis.SolidColorBrush(
                    Noesis.Color.FromArgb(96, 128, 24, 24));
            return item;
        }

        private static LobbyHumanPlayerSnapshot GetLobbyHumanPlayerSnapshot()
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPLobby lobby = platform?.activeLobby;
            if (lobby?.members == null)
            {
                int[] gamePlayerIds = platform?.gameMembers?
                    .Where(member => member != null && !member.skirmishAI && !member.kicked)
                    .Select(member => member.playerID)
                    .Where(id => id > 0 && id <= 8)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToArray() ?? Array.Empty<int>();
                if (gamePlayerIds.Length > 1)
                {
                    return new LobbyHumanPlayerSnapshot(
                        gamePlayerIds,
                        gamePlayerIds.Length,
                        false);
                }
                return new LobbyHumanPlayerSnapshot(
                    new[] { GetLocalPlayerId() },
                    1,
                    false);
            }

            CaptureLobbyHumanPlayers(
                lobby,
                out Dictionary<int, ulong> players,
                out bool hasUnresolvedPlayers);
            int humanMemberCount = lobby.members.Count(member =>
                member != null && (!member.SkirmishMember || member.SkirmishHumanMember));
            return new LobbyHumanPlayerSnapshot(
                players.Keys.OrderBy(id => id).ToArray(),
                humanMemberCount,
                hasUnresolvedPlayers);
        }

        private static bool HasActiveMultiplayerGameMembers()
        {
            Platform_Multiplayer platform = Platform_Multiplayer.Instance;
            if (platform?.gameMembers == null)
                return false;
            return platform.gameMembers.Count(member =>
                member != null && !member.skirmishAI && !member.kicked) > 1;
        }

        private bool TryValidateCompatibilityReadiness(
            int[] expectedPlayerIds,
            out string error)
        {
            error = string.Empty;
            catalog.Discover(message =>
                Shared.DebugLogHelper.LogWarning(log, message));
            string currentManifest = CastleSpawnCompatibility.EncodeManifest(
                catalog.CaptureHashes(
                    message => Shared.DebugLogHelper.LogWarning(log, message),
                    forceRefresh: true));
            if (!string.Equals(currentManifest, spawnInventoryManifest, StringComparison.Ordinal))
            {
                error = "The local AIVJSON inventory changed after its last lobby announcement. " +
                    "Reopen the CastlePlanner settings and wait for synchronization before starting.";
                return false;
            }

            if (expectedPlayerIds.Length <= 1)
                return true;

            if (!lobbyCompatibilitySyncAvailable)
            {
                error = "The CastlePlanner lobby synchronization controller is unavailable.";
                return false;
            }

            LobbyHumanPlayerSnapshot lobbyPlayers = GetLobbyHumanPlayerSnapshot();
            if (lobbyPlayers.HasUnresolvedPlayers ||
                lobbyPlayers.HumanMemberCount != expectedPlayerIds.Length ||
                !lobbyPlayers.PlayerIds.SequenceEqual(expectedPlayerIds))
            {
                error = $"The human lobby roster is not fully resolved or differs from the map roster: " +
                    $"lobby=[{string.Join(",", lobbyPlayers.PlayerIds)}], " +
                    $"map=[{string.Join(",", expectedPlayerIds)}], " +
                    $"unresolved={lobbyPlayers.HasUnresolvedPlayers}.";
                return false;
            }

            if (compatibilityBroadcastPending)
            {
                error = "The local compatibility advertisement is still pending.";
                return false;
            }

            double stableSeconds =
                (Stopwatch.GetTimestamp() - compatibilityChangedTimestamp) /
                (double)Stopwatch.Frequency;
            const double requiredStableSeconds = 2.0;
            if (stableSeconds < requiredStableSeconds)
            {
                error = $"The synchronized castle state has only been stable for " +
                    $"{stableSeconds:0.00}s; at least {requiredStableSeconds:0.00}s are required.";
                return false;
            }

            return true;
        }

        private IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> DecodeInventories(
            IEnumerable<int> playerIds)
        {
            var result = new Dictionary<int, IReadOnlyDictionary<string, string>>();
            foreach (int playerId in playerIds ?? Enumerable.Empty<int>())
            {
                if (playerId <= 0 || playerId >= SpawnInventoryManifestData.Length)
                    continue;

                string manifest = SpawnInventoryManifestData[playerId] ?? string.Empty;
                if (decodedInventories[playerId] == null ||
                    !string.Equals(decodedManifestSources[playerId], manifest, StringComparison.Ordinal))
                {
                    decodedManifestSources[playerId] = manifest;
                    decodedInventories[playerId] = CastleSpawnCompatibility.DecodeManifest(manifest);
                }
                result[playerId] = decodedInventories[playerId];
            }
            return result;
        }

        private void InvalidateDecodedInventory(int playerId)
        {
            if (playerId <= 0 || playerId >= decodedInventories.Length)
                return;
            decodedManifestSources[playerId] = null;
            decodedInventories[playerId] = null;
        }

        private void ClearCompatibilitySlots(IEnumerable<int> playerIds)
        {
            foreach (int playerId in playerIds ?? Enumerable.Empty<int>())
            {
                if (playerId <= 0 || playerId >= SpawnInventoryManifestData.Length)
                    continue;
                SpawnInventoryManifestData[playerId] = null;
                SpawnSelectedCastleData[playerId] = null;
                InvalidateDecodedInventory(playerId);
            }
            OnPropertyChanged(nameof(SpawnInventoryManifestData));
            OnPropertyChanged(nameof(SpawnSelectedCastleData));
        }

        private void MarkCompatibilityChanged()
        {
            compatibilityChangedTimestamp = Stopwatch.GetTimestamp();
        }

        private static void CaptureLobbyHumanPlayers(
            Platform_Multiplayer.MPLobby lobby,
            out Dictionary<int, ulong> players,
            out bool hasUnresolvedPlayers)
        {
            players = new Dictionary<int, ulong>();
            hasUnresolvedPlayers = false;
            if (lobby?.members == null)
                return;

            foreach (Platform_Multiplayer.MPLobbyMember member in lobby.members)
            {
                if (member == null || (member.SkirmishMember && !member.SkirmishHumanMember))
                    continue;

                int playerId = GameNetworkAPI.GetPlayerIdForSteamId(member.id);
                if (playerId <= 0 || playerId > 8 || member.id.m_SteamID == 0)
                {
                    hasUnresolvedPlayers = true;
                    continue;
                }
                if (players.TryGetValue(playerId, out ulong existingSteamId) &&
                    existingSteamId != member.id.m_SteamID)
                {
                    hasUnresolvedPlayers = true;
                    players.Remove(playerId);
                    continue;
                }
                players[playerId] = member.id.m_SteamID;
            }
        }

        private static int GetLocalPlayerId()
        {
            int playerId = GameNetworkAPI.GetLocalPlayerId();
            return playerId > 0 && playerId <= 8 ? playerId : 1;
        }

        private string NormalizeSpawnCastle(string value)
        {
            // LibraryLoaded precedes SteamManager.Awake. Keep a persisted Workshop
            // selection until the first complete catalog refresh can validate it.
            return CastleSpawnCompatibility.NormalizeSelection(
                value,
                CastleOptions,
                Shared.WorkshopContentPaths.IsSteamworksReady());
        }

        private void NormalizeSpawnSelectionAfterCompleteCatalogRefresh()
        {
            if (!Shared.WorkshopContentPaths.IsSteamworksReady() ||
                string.IsNullOrEmpty(spawnSelectedCastle))
            {
                return;
            }

            // Run through the public setting so a removed persisted file is reset,
            // persisted and synchronized exactly like a user choosing "No castle".
            SpawnSelectedCastle = spawnSelectedCastle;
        }

        internal void CompleteHotkeyCapture(KeyCode key)
        {
            SetCaptureState(false);
            SetHotkey(key);
        }

        private void CaptureNoesisHotkeyInput(object parameter)
        {
            if (!isCapturingHotkey)
                return;

            KeyCode key;
            Noesis.RoutedEventArgs routedArgs;
            if (parameter is Noesis.KeyEventArgs keyArgs &&
                TryMapNoesisKey(keyArgs.Key, out key))
            {
                routedArgs = keyArgs;
            }
            else if (parameter is Noesis.MouseButtonEventArgs mouseArgs &&
                     TryMapNoesisMouseButton(
                         mouseArgs.ChangedButton,
                         out key))
            {
                routedArgs = mouseArgs;
            }
            else
            {
                return;
            }

            routedArgs.Handled = true;
            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = false;
            CompleteHotkeyCapture(key);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint hotkey captured directly from Noesis: " +
                $"key={key}, value={(int)key}.");
        }

        private void BeginHotkeyCapture()
        {
            SetCaptureState(true);
            HotkeyCaptureRequested?.Invoke();
        }

        private void ClearHotkey()
        {
            SetCaptureState(false);
            if (KeyManager.instance != null)
                KeyManager.instance.HotKeySelectorMode = false;
            SetHotkey(KeyCode.None);
        }

        private static bool TryMapNoesisMouseButton(
            Noesis.MouseButton button,
            out KeyCode key)
        {
            switch (button)
            {
                case Noesis.MouseButton.Left:
                    key = KeyCode.Mouse0;
                    return true;
                case Noesis.MouseButton.Right:
                    key = KeyCode.Mouse1;
                    return true;
                case Noesis.MouseButton.Middle:
                    key = KeyCode.Mouse2;
                    return true;
                case Noesis.MouseButton.XButton1:
                    key = KeyCode.Mouse3;
                    return true;
                case Noesis.MouseButton.XButton2:
                    key = KeyCode.Mouse4;
                    return true;
                default:
                    key = KeyCode.None;
                    return false;
            }
        }

        private static bool TryMapNoesisKey(
            Noesis.Key source,
            out KeyCode key)
        {
            if (source >= Noesis.Key.A && source <= Noesis.Key.Z)
            {
                key = (KeyCode)((int)KeyCode.A +
                    ((int)source - (int)Noesis.Key.A));
                return true;
            }
            if (source >= Noesis.Key.D0 && source <= Noesis.Key.D9)
            {
                key = (KeyCode)((int)KeyCode.Alpha0 +
                    ((int)source - (int)Noesis.Key.D0));
                return true;
            }
            if (source >= Noesis.Key.NumPad0 &&
                source <= Noesis.Key.NumPad9)
            {
                key = (KeyCode)((int)KeyCode.Keypad0 +
                    ((int)source - (int)Noesis.Key.NumPad0));
                return true;
            }
            if (source >= Noesis.Key.F1 && source <= Noesis.Key.F15)
            {
                key = (KeyCode)((int)KeyCode.F1 +
                    ((int)source - (int)Noesis.Key.F1));
                return true;
            }

            switch (source)
            {
                case Noesis.Key.Back: key = KeyCode.Backspace; return true;
                case Noesis.Key.Tab: key = KeyCode.Tab; return true;
                case Noesis.Key.Clear: key = KeyCode.Clear; return true;
                case Noesis.Key.Return: key = KeyCode.Return; return true;
                case Noesis.Key.Pause: key = KeyCode.Pause; return true;
                case Noesis.Key.Escape: key = KeyCode.Escape; return true;
                case Noesis.Key.Space: key = KeyCode.Space; return true;
                case Noesis.Key.PageUp: key = KeyCode.PageUp; return true;
                case Noesis.Key.PageDown: key = KeyCode.PageDown; return true;
                case Noesis.Key.End: key = KeyCode.End; return true;
                case Noesis.Key.Home: key = KeyCode.Home; return true;
                case Noesis.Key.Left: key = KeyCode.LeftArrow; return true;
                case Noesis.Key.Up: key = KeyCode.UpArrow; return true;
                case Noesis.Key.Right: key = KeyCode.RightArrow; return true;
                case Noesis.Key.Down: key = KeyCode.DownArrow; return true;
                case Noesis.Key.Print:
                    key = KeyCode.Print;
                    return true;
                case Noesis.Key.Insert: key = KeyCode.Insert; return true;
                case Noesis.Key.Delete: key = KeyCode.Delete; return true;
                case Noesis.Key.Help: key = KeyCode.Help; return true;
                case Noesis.Key.Multiply: key = KeyCode.KeypadMultiply; return true;
                case Noesis.Key.Add: key = KeyCode.KeypadPlus; return true;
                case Noesis.Key.Subtract: key = KeyCode.KeypadMinus; return true;
                case Noesis.Key.Decimal: key = KeyCode.KeypadPeriod; return true;
                case Noesis.Key.Divide: key = KeyCode.KeypadDivide; return true;
                case Noesis.Key.NumLock: key = KeyCode.Numlock; return true;
                case Noesis.Key.Scroll: key = KeyCode.ScrollLock; return true;
                case Noesis.Key.CapsLock: key = KeyCode.CapsLock; return true;
                case Noesis.Key.LeftShift: key = KeyCode.LeftShift; return true;
                case Noesis.Key.RightShift: key = KeyCode.RightShift; return true;
                case Noesis.Key.LeftCtrl: key = KeyCode.LeftControl; return true;
                case Noesis.Key.RightCtrl: key = KeyCode.RightControl; return true;
                case Noesis.Key.LeftAlt: key = KeyCode.LeftAlt; return true;
                case Noesis.Key.RightAlt: key = KeyCode.RightAlt; return true;
                case Noesis.Key.LWin: key = KeyCode.LeftWindows; return true;
                case Noesis.Key.RWin: key = KeyCode.RightWindows; return true;
                case Noesis.Key.Apps: key = KeyCode.Menu; return true;
                case Noesis.Key.OemSemicolon: key = KeyCode.Semicolon; return true;
                case Noesis.Key.OemPlus: key = KeyCode.Equals; return true;
                case Noesis.Key.OemComma: key = KeyCode.Comma; return true;
                case Noesis.Key.OemMinus: key = KeyCode.Minus; return true;
                case Noesis.Key.OemPeriod: key = KeyCode.Period; return true;
                case Noesis.Key.OemQuestion: key = KeyCode.Slash; return true;
                case Noesis.Key.OemTilde: key = KeyCode.BackQuote; return true;
                case Noesis.Key.OemOpenBrackets:
                    key = KeyCode.LeftBracket;
                    return true;
                case Noesis.Key.OemPipe: key = KeyCode.Backslash; return true;
                case Noesis.Key.OemCloseBrackets:
                    key = KeyCode.RightBracket;
                    return true;
                case Noesis.Key.OemQuotes: key = KeyCode.Quote; return true;
                case Noesis.Key.GamepadAccept:
                    key = KeyCode.JoystickButton0;
                    return true;
                case Noesis.Key.GamepadCancel:
                    key = KeyCode.JoystickButton1;
                    return true;
                case Noesis.Key.GamepadContext1:
                    key = KeyCode.JoystickButton2;
                    return true;
                case Noesis.Key.GamepadContext2:
                    key = KeyCode.JoystickButton3;
                    return true;
                case Noesis.Key.GamepadPageLeft:
                    key = KeyCode.JoystickButton4;
                    return true;
                case Noesis.Key.GamepadPageRight:
                    key = KeyCode.JoystickButton5;
                    return true;
                case Noesis.Key.GamepadView:
                    key = KeyCode.JoystickButton6;
                    return true;
                case Noesis.Key.GamepadMenu:
                    key = KeyCode.JoystickButton7;
                    return true;
                default:
                    key = KeyCode.None;
                    return false;
            }
        }

        private void SetCaptureState(bool value)
        {
            if (isCapturingHotkey == value)
                return;

            isCapturingHotkey = value;
            OnPropertyChanged(nameof(IsCapturingHotkey));
            OnPropertyChanged(nameof(HotkeyCaptureButtonText));
        }

        private void SetHotkey(KeyCode key)
        {
            if (blueprintHotkey == key)
                return;

            blueprintHotkey = key;
            OnPropertyChanged(nameof(BlueprintHotkey));
            OnPropertyChanged(nameof(HotkeyDisplayText));
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Blueprint toggle hotkey changed to '{HotkeyDisplayText}' ({(int)blueprintHotkey}).");
        }

        private string NormalizeCastle(string value, string fallback)
        {
            string candidate = value?.Trim() ?? string.Empty;
            foreach (string option in CastleOptions)
            {
                if (string.Equals(option, candidate, StringComparison.OrdinalIgnoreCase))
                    return option;
            }

            if (!string.IsNullOrEmpty(candidate))
            {
                // The first catalog scan intentionally runs before Steam is ready. Preserve a
                // persisted Workshop selection until the dropdown's later complete refresh.
                if (!Shared.WorkshopContentPaths.IsSteamworksReady())
                    return candidate;

                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Stored AIVJSON is no longer available: '{candidate}'.");
            }

            return fallback;
        }

        private static KeyCode NormalizeKeyCode(int value)
        {
            return Enum.IsDefined(typeof(KeyCode), value)
                ? (KeyCode)value
                : KeyCode.None;
        }

        private static double NormalizeIconScale(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 1.0;

            // Keep malformed persisted values inside the range exposed by the HUD.
            return Math.Round(
                Math.Max(0.05, Math.Min(1.0, value)),
                2,
                MidpointRounding.AwayFromZero);
        }

        private static double NormalizeIconAlpha(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.3;

            return Math.Round(
                Math.Max(0.0, Math.Min(1.0, value)),
                2,
                MidpointRounding.AwayFromZero);
        }

        private static double NormalizeUnitValue(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0.0;

            return Math.Max(0.0, Math.Min(1.0, value));
        }

        private void ResetToDefault()
        {
            EnableClientFeatures = true;
            Blueprints = true;
            if (CanEditHostSettings)
            {
                EnableMod = true;
                SpawnCastle = false;
            }

            // Every participant resets their own Blueprint and spawn preferences.
            SelectedCastle = CastleOptions.Count > 0 ? CastleOptions[0] : string.Empty;
            SpawnSelectedCastle = string.Empty;
            BlueprintHotkey = (int)KeyCode.None;
            BlueprintIconScale = 1.0;
            BlueprintIconAlpha = 0.3;
        }

        private void NormalizeRuntimeState()
        {
            runtimeState.BlueprintHudPositionX = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionX);
            runtimeState.BlueprintHudPositionY = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionY);
        }

        private void TryMigrateLegacySettings(string pluginAssemblyLocation)
        {
            string pluginDirectory = Path.GetDirectoryName(pluginAssemblyLocation);
            if (string.IsNullOrEmpty(pluginDirectory))
                return;

            string legacyPath = Path.Combine(
                pluginDirectory,
                LobbyModSettingsStorage.STORAGE_FOLDER_NAME,
                CastlePlannerPlugin.PluginGuid + LobbyModSettingsStorage.FILE_EXTENSION);
            if (!File.Exists(legacyPath) || IsSharedPresetPayload(legacyPath))
                return;

            LegacyPersistedSettings legacy = new LegacyPersistedSettings();
            new LobbyModSettingsStorage(
                pluginAssemblyLocation,
                CastlePlannerPlugin.PluginGuid).Load(legacy);

            blueprints = legacy.Mode == LegacyCastlePlannerMode.Blueprint;
            spawnCastle = legacy.Mode == LegacyCastlePlannerMode.Spawn;
            string defaultCastle = CastleOptions.Count > 0 ? CastleOptions[0] : string.Empty;
            selectedCastle = NormalizeCastle(legacy.SelectedCastle, defaultCastle);
            spawnSelectedCastle = legacy.Mode == LegacyCastlePlannerMode.Spawn
                ? selectedCastle
                : string.Empty;
            blueprintHotkey = NormalizeKeyCode(legacy.BlueprintHotkey);
            blueprintIconScale = NormalizeIconScale(legacy.BlueprintIconScale);
            blueprintIconAlpha = NormalizeIconAlpha(legacy.BlueprintIconAlpha);

            if (legacy.HasBlueprintHudPosition)
            {
                runtimeState.HasBlueprintHudPosition = true;
                runtimeState.BlueprintHudPositionX = NormalizeUnitValue(
                    legacy.BlueprintHudPositionX);
                runtimeState.BlueprintHudPositionY = NormalizeUnitValue(
                    legacy.BlueprintHudPositionY);
                runtimeStorage.Save(runtimeState);
            }

            // Preset activation rewrites the legacy file in the shared format.
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Legacy CastlePlanner settings prepared for preset migration: " +
                $"blueprints={blueprints}, spawnCastle={spawnCastle}, " +
                $"selection='{selectedCastle}'.");
        }

        private static bool IsSharedPresetPayload(string path)
        {
            try
            {
                Dictionary<string, byte[]> payload =
                    MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(
                        File.ReadAllBytes(path));
                return payload != null && payload.ContainsKey("__SerpPresetSchemaVersion");
            }
            catch
            {
                return false;
            }
        }

        private static string GetKeyDisplayName(KeyCode key)
        {
            try
            {
                string display = CrusaderDE.HUD_Options.GetKeyCodeString(key);
                return string.IsNullOrWhiteSpace(display) ? key.ToString() : display;
            }
            catch
            {
                return key.ToString();
            }
        }

        private sealed class ParameterRelayCommand : ICommand
        {
            private readonly Action<object> execute;

            public ParameterRelayCommand(Action<object> execute)
            {
                this.execute =
                    execute ?? throw new ArgumentNullException(nameof(execute));
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                execute(parameter);
            }

            public event System.EventHandler CanExecuteChanged
            {
                add { }
                remove { }
            }
        }

        private sealed class LobbyHumanPlayerSnapshot
        {
            public LobbyHumanPlayerSnapshot(
                int[] playerIds,
                int humanMemberCount,
                bool hasUnresolvedPlayers)
            {
                PlayerIds = playerIds ?? Array.Empty<int>();
                HumanMemberCount = humanMemberCount;
                HasUnresolvedPlayers = hasUnresolvedPlayers;
            }

            public int[] PlayerIds { get; }
            public int HumanMemberCount { get; }
            public bool HasUnresolvedPlayers { get; }
        }

        private enum LegacyCastlePlannerMode
        {
            Disabled,
            Blueprint,
            Spawn
        }

        private sealed class LegacyPersistedSettings
        {
            [SyncPerPlayer]
            public LegacyCastlePlannerMode Mode { get; set; } =
                LegacyCastlePlannerMode.Disabled;

            [SyncPerPlayer]
            public string SelectedCastle { get; set; } = string.Empty;

            [SyncPerPlayer]
            public int BlueprintHotkey { get; set; } = (int)KeyCode.None;

            [SyncPerPlayer]
            public double BlueprintIconScale { get; set; } = 1.0;

            [SyncPerPlayer]
            public double BlueprintIconAlpha { get; set; } = 0.3;

            [SyncPerPlayer]
            public bool HasBlueprintHudPosition { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionX { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionY { get; set; }
        }

        private sealed class RuntimePersistedState
        {
            // Window position remains independent from the selected settings preset.
            [SyncPerPlayer]
            public bool HasBlueprintHudPosition { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionX { get; set; }

            [SyncPerPlayer]
            public double BlueprintHudPositionY { get; set; }
        }
    }
}
