using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using UnityEngine;

namespace CastlePlanner
{
    internal sealed class BulkObservableCollection<T> : ObservableCollection<T>
    {
        internal void ReplaceWith(IEnumerable<T> values)
        {
            Items.Clear();
            foreach (T value in values)
                Items.Add(value);
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
        }
    }

    public sealed class CastlePlannerSettingsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly ManualLogSource log;
        private AivFileCatalog catalog = new AivFileCatalog();
        private readonly LobbyModSettingsStorage runtimeStorage;
        private readonly RuntimePersistedState runtimeState =
            new RuntimePersistedState();
        private bool enableClientFeatures = true;
        private bool enableMod = true;
        private bool enableAivPlacementLobby;
        private bool blueprints = true;
        private bool blueprintShowFortifications = true;
        private bool blueprintShowBuildings = true;
        private bool blueprintShowDefensiveGroundFeatures = true;
        private bool blueprintShowFearFactorBuildings = true;
        private bool spawnCastle;
        private bool spawnFortifications =
            CastleSpawnContentPolicy.DefaultFortifications;
        private bool spawnBuildings = CastleSpawnContentPolicy.DefaultBuildings;
        private bool spawnDefensiveGroundFeatures =
            CastleSpawnContentPolicy.DefaultDefensiveGroundFeatures;
        private bool spawnFearFactorBuildings =
            CastleSpawnContentPolicy.DefaultFearFactorBuildings;
        private bool spawnSiegeEngines =
            CastleSpawnContentPolicy.DefaultSiegeEngines;
        private bool spawnBraziersAndFlags;
        private readonly bool[] spawnBraziersAndFlagsData = new bool[9];
        private readonly int[] spawnBraziersAndFlagsReportData =
            Enumerable.Repeat(-1, 9).ToArray();
        private int localPlayerId;
        private string selectedCastle;
        private bool castleCatalogLoaded;
        private Task<CatalogLoadResult> castleCatalogTask;
        private DateTime nextCatalogRetryUtc;
        private KeyCode blueprintHotkey;
        private double blueprintIconScale;
        private double blueprintIconAlpha;
        private bool isCapturingHotkey;
        private readonly BulkObservableCollection<string> castleOptions =
            new BulkObservableCollection<string>();

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        protected override void OnSettingsSnapshotApplied()
        {
            base.OnSettingsSnapshotApplied();
            if (!CanEditHostSettings ||
                !CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                    spawnCastle,
                    true,
                    spawnFortifications,
                    spawnBuildings,
                    spawnDefensiveGroundFeatures,
                    spawnFearFactorBuildings,
                    spawnSiegeEngines))
            {
                return;
            }

            // Snapshot property order must not decide the final invariant.
            SpawnCastle = false;
        }

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

            blueprintHotkey = KeyCode.None;
            blueprintIconScale = 1.0;
            blueprintIconAlpha = 0.3;

            runtimeStorage = new LobbyModSettingsStorage(
                pluginAssemblyLocation,
                CastlePlannerPlugin.PluginGuid + ".Runtime");
            runtimeStorage.Load(runtimeState);
            NormalizeRuntimeState();
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
            AssignHotkeyCommand = new RelayCommand(BeginHotkeyCapture);
            ClearHotkeyCommand = new RelayCommand(ClearHotkey);
            HotkeyInputCommand =
                new ParameterRelayCommand(CaptureNoesisHotkeyInput);
        }

        protected override void ConfigurePerPlayerLobbySettings(
            Shared.PerPlayerLobbySettingsBuilder settings)
        {
            settings
                .ResetSlotsWith(nameof(SpawnBraziersAndFlags), () => false)
                .ResetSlotsWith(nameof(SpawnBraziersAndFlagsReport), () => -1)
                .RequireReport(
                    nameof(SpawnBraziersAndFlagsReport),
                    value => value is int report && report == 1)
                .WhenLocalPlayerResolved(playerId =>
                {
                    localPlayerId = playerId;
                    if (playerId >= 1 && playerId <= 8)
                    {
                        spawnBraziersAndFlagsData[playerId] = spawnBraziersAndFlags;
                        spawnBraziersAndFlagsReportData[playerId] = 1;
                    }
                });
        }

        internal event Action SettingsChanged;
        internal event Action BlueprintVisualSettingsChanged;
        internal event Action BlueprintContentSettingsChanged;
        internal event Action HotkeyCaptureRequested;

        public ObservableCollection<string> CastleOptions => castleOptions;

        public ICommand AssignHotkeyCommand { get; }
        public ICommand ClearHotkeyCommand { get; }
        public ICommand HotkeyInputCommand { get; }
        public RelayCommand ResetToDefaultCommand { get; }

        public int AvailableFileCount => CastleOptions.Count;

        internal bool EnsureCastleCatalogLoaded()
        {
            PumpCastleCatalogLoad();
            return castleCatalogLoaded;
        }

        internal void PumpCastleCatalogLoad()
        {
            if (castleCatalogLoaded)
                return;

            if (castleCatalogTask != null)
            {
                if (!castleCatalogTask.IsCompleted)
                    return;

                Task<CatalogLoadResult> completed = castleCatalogTask;
                castleCatalogTask = null;
                if (completed.Status != TaskStatus.RanToCompletion)
                {
                    Exception error = completed.Exception?.GetBaseException() ??
                        new InvalidOperationException(
                            $"AIVJSON catalog task ended with {completed.Status}.");
                    nextCatalogRetryUtc = DateTime.UtcNow.AddSeconds(5);
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Asynchronous AIVJSON catalog loading failed and will be retried: {error}");
                    return;
                }

                ApplyCastleCatalog(completed.Result);
                return;
            }

            if ((!IsBlueprintMode && !IsSpawnMode) ||
                DateTime.UtcNow < nextCatalogRetryUtc ||
                !Shared.WorkshopContentPaths.IsSteamworksReady())
            {
                return;
            }

            try
            {
                // Unity/Steam-backed source discovery stays on the main thread. All recursive
                // filesystem work and hashing then runs incrementally on a worker.
                AivFileCatalog.DiscoveryPlan plan = AivFileCatalog.PrepareDiscovery(
                    message => Shared.DebugLogHelper.LogWarning(log, message));
                castleCatalogTask = Task.Run(() =>
                {
                    var workerCatalog = new AivFileCatalog();
                    var warnings = new System.Collections.Generic.List<string>();
                    IReadOnlyList<string> options = workerCatalog.Discover(
                        plan,
                        warnings.Add);
                    return new CatalogLoadResult(
                        workerCatalog,
                        options.ToArray(),
                        warnings.ToArray());
                });
                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Asynchronous AIVJSON catalog loading started; recursive discovery and hashing will not block the game thread.");
            }
            catch (Exception ex)
            {
                nextCatalogRetryUtc = DateTime.UtcNow.AddSeconds(5);
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AIVJSON catalog source preparation failed and will be retried: {ex}");
            }
        }

        private void ApplyCastleCatalog(CatalogLoadResult result)
        {
            if (result == null)
                throw new InvalidOperationException("The AIVJSON catalog task returned no result.");
            foreach (string warning in result.Warnings)
                Shared.DebugLogHelper.LogWarning(log, warning);

            catalog = result.Catalog;
            RefreshCastleOptions(result.Options, notifySelectionChange: true);
        }

        private void RefreshCastleOptions(
            IReadOnlyList<string> discovered,
            bool notifySelectionChange)
        {
            castleCatalogLoaded = true;
            if (CastleOptions.Count == discovered.Count)
            {
                bool unchanged = true;
                for (int index = 0; index < discovered.Count; index++)
                    unchanged &= string.Equals(CastleOptions[index], discovered[index], StringComparison.Ordinal);
                if (unchanged)
                {
                    return;
                }
            }

            string previous = selectedCastle ?? string.Empty;
            castleOptions.ReplaceWith(discovered);
            string defaultCastle = CastleOptions.Count > 0 ? CastleOptions[0] : string.Empty;
            string normalized = NormalizeCastle(previous, defaultCastle);
            bool selectionChanged = !string.Equals(selectedCastle, normalized, StringComparison.Ordinal);
            selectedCastle = normalized;
            OnPropertyChanged(nameof(AvailableFileCount));
            if (selectionChanged)
                OnPropertyChanged(nameof(SelectedCastle));
            if (notifySelectionChange && selectionChanged)
                SettingsChanged?.Invoke();
            Shared.DebugLogHelper.LogInfo(
                log,
                $"CastlePlanner cached AIVJSON choices including Steam Workshop content; " +
                $"unique={CastleOptions.Count}, identicalDuplicatesIgnored={catalog.IdenticalFileCount}.");
        }

        private sealed class CatalogLoadResult
        {
            internal CatalogLoadResult(
                AivFileCatalog catalog,
                IReadOnlyList<string> options,
                IReadOnlyList<string> warnings)
            {
                Catalog = catalog;
                Options = options;
                Warnings = warnings;
            }

            internal AivFileCatalog Catalog { get; }
            internal IReadOnlyList<string> Options { get; }
            internal IReadOnlyList<string> Warnings { get; }
        }

        public string ResetToDefaultText => SerpLocalization.Get("Common.ResetToDefault");
        public string EnableClientFeaturesText => SerpLocalization.Get("CastlePlanner.EnableClientFeatures");
        public string EnableClientFeaturesHelpText => SerpLocalization.Get("CastlePlanner.EnableClientFeaturesHelp");
        public string EnableHostFeaturesText => SerpLocalization.Get("CastlePlanner.EnableHostFeatures");
        public string EnableHostFeaturesHelpText => SerpLocalization.Get("CastlePlanner.EnableHostFeaturesHelp");
        public string EnableAivPlacementLobbyText => SerpLocalization.Get("CastlePlanner.EnableAivPlacementLobby");
        public string EnableAivPlacementLobbyHelpText => SerpLocalization.Get("CastlePlanner.EnableAivPlacementLobbyHelp");
        public string TitleText => SerpLocalization.Get("CastlePlanner.Title");
        public string HelpText => SerpLocalization.Get("CastlePlanner.Help");
        public string BlueprintsText => SerpLocalization.Get("CastlePlanner.Blueprints");
        public string BlueprintsHelpText => SerpLocalization.Get("CastlePlanner.BlueprintsHelp");
        public string SpawnCastleText => SerpLocalization.Get("CastlePlanner.SpawnCastle");
        public string SpawnCastleHelpText => SerpLocalization.Get("CastlePlanner.SpawnCastleHelp");
        public string SpawnFortificationsText => SerpLocalization.Get("CastlePlanner.SpawnFortifications");
        public string SpawnFortificationsHelpText => SerpLocalization.Get("CastlePlanner.SpawnFortificationsHelp");
        public string SpawnBuildingsText => SerpLocalization.Get("CastlePlanner.SpawnBuildings");
        public string SpawnBuildingsHelpText => SerpLocalization.Get("CastlePlanner.SpawnBuildingsHelp");
        public string SpawnDefensiveGroundFeaturesText => SerpLocalization.Get("CastlePlanner.SpawnDefensiveGroundFeatures");
        public string SpawnDefensiveGroundFeaturesHelpText => SerpLocalization.Get("CastlePlanner.SpawnDefensiveGroundFeaturesHelp");
        public string SpawnFearFactorBuildingsText => SerpLocalization.Get("CastlePlanner.SpawnFearFactorBuildings");
        public string SpawnFearFactorBuildingsHelpText => SerpLocalization.Get("CastlePlanner.SpawnFearFactorBuildingsHelp");
        public string SpawnSiegeEnginesText => SerpLocalization.Get("CastlePlanner.SpawnSiegeEngines");
        public string SpawnSiegeEnginesHelpText => SerpLocalization.Get("CastlePlanner.SpawnSiegeEnginesHelp");
        public string SpawnBraziersAndFlagsText => SerpLocalization.Get("CastlePlanner.SpawnBraziersAndFlags");
        public string SpawnBraziersAndFlagsHelpText => SerpLocalization.Get("CastlePlanner.SpawnBraziersAndFlagsHelp");
        public string HotkeyText => SerpLocalization.Get("CastlePlanner.Hotkey");
        public string HotkeyHelpText => SerpLocalization.Get("CastlePlanner.HotkeyHelp");
        public string ClearText => SerpLocalization.Get("Common.Clear");
        public string ClearHelpText => SerpLocalization.Get("CastlePlanner.ClearHelp");
        public string LocalOptionsText => SerpLocalization.Get("CastlePlanner.LocalOptions");
        public string CastleSectionTitleText => SerpLocalization.Get("CastlePlanner.CastleSectionTitle");
        public string PlacementControlsTitleText => SerpLocalization.Get("CastlePlanner.PlacementControlsTitle");

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
                PumpCastleCatalogLoad();
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
                PumpCastleCatalogLoad();
                SettingsChanged?.Invoke();
            }
        }

        [SyncHostOnly]
        public bool EnableAivPlacementLobby
        {
            get => enableAivPlacementLobby;
            set
            {
                if (!CanMutateSetting(nameof(EnableAivPlacementLobby)) ||
                    enableAivPlacementLobby == value)
                {
                    return;
                }

                enableAivPlacementLobby = value;
                OnPropertyChanged(nameof(EnableAivPlacementLobby));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner hidden host AIV placement feature changed to {enableAivPlacementLobby}.");
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
                PumpCastleCatalogLoad();
                SettingsChanged?.Invoke();
            }
        }

        [Shared.PresetLocal]
        public bool BlueprintShowFortifications
        {
            get => blueprintShowFortifications;
            set => SetBlueprintContentOption(
                ref blueprintShowFortifications,
                value,
                nameof(BlueprintShowFortifications));
        }

        [Shared.PresetLocal]
        public bool BlueprintShowBuildings
        {
            get => blueprintShowBuildings;
            set => SetBlueprintContentOption(
                ref blueprintShowBuildings,
                value,
                nameof(BlueprintShowBuildings));
        }

        [Shared.PresetLocal]
        public bool BlueprintShowDefensiveGroundFeatures
        {
            get => blueprintShowDefensiveGroundFeatures;
            set => SetBlueprintContentOption(
                ref blueprintShowDefensiveGroundFeatures,
                value,
                nameof(BlueprintShowDefensiveGroundFeatures));
        }

        [Shared.PresetLocal]
        public bool BlueprintShowFearFactorBuildings
        {
            get => blueprintShowFearFactorBuildings;
            set => SetBlueprintContentOption(
                ref blueprintShowFearFactorBuildings,
                value,
                nameof(BlueprintShowFearFactorBuildings));
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

                if (CastleSpawnContentPolicy.ShouldResetBeforeEnabling(
                        spawnCastle,
                        value,
                        CanEditHostSettings && !IsApplyingSettingsSnapshot))
                {
                    ResetHostSpawnContentToDefaults();
                }

                spawnCastle = value;
                OnPropertyChanged(nameof(SpawnCastle));
                OnPropertyChanged(nameof(IsSpawnMode));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner host Spawn Castle changed to {spawnCastle}.");
                PumpCastleCatalogLoad();
                SettingsChanged?.Invoke();
            }
        }

        [SyncHostOnly]
        public bool SpawnFortifications
        {
            get => spawnFortifications;
            set => SetHostSpawnOption(
                ref spawnFortifications,
                value,
                nameof(SpawnFortifications));
        }

        [SyncHostOnly]
        public bool SpawnBuildings
        {
            get => spawnBuildings;
            set => SetHostSpawnOption(ref spawnBuildings, value, nameof(SpawnBuildings));
        }

        [SyncHostOnly]
        public bool SpawnDefensiveGroundFeatures
        {
            get => spawnDefensiveGroundFeatures;
            set => SetHostSpawnOption(ref spawnDefensiveGroundFeatures, value, nameof(SpawnDefensiveGroundFeatures));
        }

        [SyncHostOnly]
        public bool SpawnFearFactorBuildings
        {
            get => spawnFearFactorBuildings;
            set => SetHostSpawnOption(ref spawnFearFactorBuildings, value, nameof(SpawnFearFactorBuildings));
        }

        [SyncHostOnly]
        public bool SpawnSiegeEngines
        {
            get => spawnSiegeEngines;
            set => SetHostSpawnOption(ref spawnSiegeEngines, value, nameof(SpawnSiegeEngines));
        }

        public bool[] SpawnBraziersAndFlagsData => spawnBraziersAndFlagsData;

        public int[] SpawnBraziersAndFlagsReportData => spawnBraziersAndFlagsReportData;

        // A separate sentinel is required because every bool value is otherwise
        // indistinguishable from an unreported slot reset to false.
        [SyncPerPlayer]
        public int SpawnBraziersAndFlagsReport
        {
            get => 1;
            set { }
        }

        [SyncPerPlayer]
        public bool SpawnBraziersAndFlags
        {
            get => spawnBraziersAndFlags;
            set
            {
                if (!CanMutateSetting(nameof(SpawnBraziersAndFlags)) ||
                    spawnBraziersAndFlags == value)
                {
                    return;
                }

                spawnBraziersAndFlags = value;
                if (localPlayerId >= 1 && localPlayerId <= 8)
                    spawnBraziersAndFlagsData[localPlayerId] = value;
                OnPropertyChanged(nameof(SpawnBraziersAndFlags));
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner personal SpawnBraziersAndFlags changed to {value}.");
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

        internal AivSpawnOptions GetSpawnOptions(int playerId)
        {
            bool decorations = playerId >= 1 && playerId <= 8
                ? spawnBraziersAndFlagsData[playerId]
                : false;
            return new AivSpawnOptions
            {
                SpawnFortifications = SpawnFortifications,
                SpawnBuildings = SpawnBuildings,
                SpawnDefensiveGroundFeatures = SpawnDefensiveGroundFeatures,
                SpawnFearFactorBuildings = SpawnFearFactorBuildings,
                SpawnSiegeEngines = SpawnSiegeEngines,
                SpawnBraziersAndFlags = decorations
            };
        }

        internal AivSpawnOptions GetLocalPreviewSpawnOptions()
        {
            AivSpawnOptions options = GetSpawnOptions(localPlayerId);
            options.SpawnBraziersAndFlags = SpawnBraziersAndFlags;
            return options;
        }

        internal AivSpawnOptions GetBlueprintDisplayOptions()
        {
            return new AivSpawnOptions
            {
                SpawnFortifications = BlueprintShowFortifications,
                SpawnBuildings = BlueprintShowBuildings,
                SpawnDefensiveGroundFeatures =
                    BlueprintShowDefensiveGroundFeatures,
                SpawnFearFactorBuildings = BlueprintShowFearFactorBuildings,
                SpawnSiegeEngines = false,
                SpawnBraziersAndFlags = false
            };
        }

        private void SetBlueprintContentOption(
            ref bool field,
            bool value,
            string propertyName)
        {
            if (field == value)
                return;

            field = value;
            OnPropertyChanged(propertyName);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"CastlePlanner local {propertyName} changed to {value}.");
            BlueprintContentSettingsChanged?.Invoke();
        }

        private void SetHostSpawnOption(ref bool field, bool value, string propertyName)
        {
            if (!CanMutateSetting(propertyName) || field == value)
                return;

            bool projectedFortifications =
                propertyName == nameof(SpawnFortifications)
                    ? value
                    : spawnFortifications;
            bool projectedBuildings = propertyName == nameof(SpawnBuildings)
                ? value
                : spawnBuildings;
            bool projectedDefensiveGroundFeatures =
                propertyName == nameof(SpawnDefensiveGroundFeatures)
                    ? value
                    : spawnDefensiveGroundFeatures;
            bool projectedFearFactorBuildings =
                propertyName == nameof(SpawnFearFactorBuildings)
                    ? value
                    : spawnFearFactorBuildings;
            bool projectedSiegeEngines =
                propertyName == nameof(SpawnSiegeEngines)
                    ? value
                    : spawnSiegeEngines;

            // Publish SpawnCastle=false before the last content option. Clients
            // therefore never observe an enabled spawn with an empty castle.
            if (CastleSpawnContentPolicy.ShouldDisableBeforeContentChange(
                    spawnCastle,
                    CanEditHostSettings && !IsApplyingSettingsSnapshot,
                    projectedFortifications,
                    projectedBuildings,
                    projectedDefensiveGroundFeatures,
                    projectedFearFactorBuildings,
                    projectedSiegeEngines))
            {
                SpawnCastle = false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            Shared.DebugLogHelper.LogInfo(log, $"CastlePlanner host {propertyName} changed to {value}.");
            SettingsChanged?.Invoke();
        }

        private void ResetHostSpawnContentToDefaults()
        {
            SpawnFortifications = CastleSpawnContentPolicy.DefaultFortifications;
            SpawnBuildings = CastleSpawnContentPolicy.DefaultBuildings;
            SpawnDefensiveGroundFeatures =
                CastleSpawnContentPolicy.DefaultDefensiveGroundFeatures;
            SpawnFearFactorBuildings =
                CastleSpawnContentPolicy.DefaultFearFactorBuildings;
            SpawnSiegeEngines = CastleSpawnContentPolicy.DefaultSiegeEngines;
        }

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

        internal bool TryPrepareSelectedCastle(
            int playerId,
            int rotation,
            out FreeCastleSelection selection,
            out string error)
        {
            selection = null;
            error = string.Empty;
            if (playerId < 1 || playerId > 8)
            {
                error = $"Invalid local player ID {playerId}.";
                return false;
            }
            if (rotation != 0 && rotation != 2 && rotation != 4 && rotation != 6)
            {
                error = $"Invalid castle rotation {rotation}.";
                return false;
            }
            if (!EnsureCastleCatalogLoaded() ||
                !catalog.TryResolve(selectedCastle, out string filePath))
            {
                error = $"The selected AIVJSON is unavailable: '{selectedCastle}'.";
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                short[] raw = AivRawDataEncoder.Encode(AivJsonReader.Parse(json));
                selection = new FreeCastleSelection
                {
                    PlayerId = playerId,
                    Rotation = rotation,
                    DisplayName = Path.GetFileNameWithoutExtension(selectedCastle),
                    RawData = raw,
                    ContentHash = FreeCastleProtocol.HashRaw(raw)
                };
                FreeCastleProtocol.ValidateSelection(selection);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                selection = null;
                return false;
            }
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
                // Preserve persisted choices until the one cached catalog scan has
                // actually run and Steam Workshop paths are available.
                if (!castleCatalogLoaded ||
                    !Shared.WorkshopContentPaths.IsSteamworksReady())
                {
                    return candidate;
                }

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
            BlueprintShowFortifications = true;
            BlueprintShowBuildings = true;
            BlueprintShowDefensiveGroundFeatures = true;
            BlueprintShowFearFactorBuildings = true;
            if (CanEditHostSettings)
            {
                EnableMod = true;
                EnableAivPlacementLobby = false;
                SpawnCastle = false;
                ResetHostSpawnContentToDefaults();
            }

            // Every participant resets their own Blueprint preference.
            SelectedCastle = CastleOptions.Count > 0 ? CastleOptions[0] : string.Empty;
            BlueprintHotkey = (int)KeyCode.None;
            BlueprintIconScale = 1.0;
            BlueprintIconAlpha = 0.3;
            SpawnBraziersAndFlags = false;
        }

        private void NormalizeRuntimeState()
        {
            runtimeState.BlueprintHudPositionX = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionX);
            runtimeState.BlueprintHudPositionY = NormalizeUnitValue(
                runtimeState.BlueprintHudPositionY);
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
