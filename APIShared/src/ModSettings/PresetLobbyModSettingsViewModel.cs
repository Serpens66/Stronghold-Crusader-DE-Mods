using APIShared.GameModes;
using APIShared.ModSettings;
using APIShared.SerpsMods;
using APIShared.Internal;
#pragma warning disable 1591 // XAML and integration surface is documented by the APIShared preset guide.
using BepInEx;
using BepInEx.Logging;
using MessagePack;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
#if !API_SHARED_PRESET_TESTS
using R3;
using SHCDESE.EventAPI;
using SHCDESE.NoesisUtil;
#endif
using ComboBoxItem = Noesis.ComboBoxItem;
using Visibility = Noesis.Visibility;
#if API_SHARED_LOBBY_OBSERVER && !API_SHARED_PRESET_TESTS
using APIShared;
#endif

namespace APIShared.ModSettings
{
    /// <summary>
    /// Persists a setting in the shared local preset file without exposing it to
    /// the Script Extender's multiplayer synchronization layer.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class PresetLocalAttribute : Attribute
    {
    }

    /// <summary>
    /// Adds two local presets to a Script Extender lobby-settings ViewModel while
    /// keeping the outer MessagePack dictionary readable by the Script Extender.
    /// </summary>
    public abstract partial class PresetLobbyModSettingsViewModel : LobbyModSettingsBaseViewModel, IModSettingsWorkingCopyEndpoint, IModSettingsMissionSourceEndpoint
    {
        private enum SettingsMenuContext { Other, Campaign, DirectTrail, CustomizeSetup }
        private static readonly MethodInfo NotifyRevertMethod =
            typeof(LobbyModSettingsBaseViewModel).GetMethod(
                "NotifyRevert",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private PresetController presetController;
        private int selectedPreset;
        private bool missionPresetContext;
        private bool missionPresetEditable;
        private bool missionPresetHasExplicitSettings;
        private bool showDirectLaunchNotice;
        private bool isCastlePlannerSettings;
        private SettingsMenuContext settingsMenuContext;
        private bool isRealMultiplayer;
        private bool isLocalHost = true;
        private PerPlayerLobbySettingsCoordinator perPlayerSettingsCoordinator;
#if !API_SHARED_PRESET_TESTS
        private string modSettingsSearchText = string.Empty;
        private string modSettingsSearchExactKey = string.Empty;
        private bool modSettingsSearchIncludeToolTips;
        private bool modSettingsSearchExpanded;
        private int modSettingsSearchFocusRequest;
        private bool presetSavePanelOpen;
        private bool presetLoadPanelOpen;
        private string presetSaveName = string.Empty;
        private string presetSaveDescription = string.Empty;
        private ModSettingsPresetListEntry selectedPresetLoadEntry;
        private ModSettingsPresetSaveTarget selectedPresetSaveTarget;
        private readonly ObservableCollection<ModSettingsPresetListEntry> presetLoadEntries =
            new ObservableCollection<ModSettingsPresetListEntry>();
        private readonly ObservableCollection<ModSettingsPresetSaveTarget> presetSaveTargets =
            new ObservableCollection<ModSettingsPresetSaveTarget>();
        private bool applyingPresetSaveBulkMode;
        private readonly ObservableCollection<PresetSaveSettingViewModel> presetSaveSettings =
            new ObservableCollection<PresetSaveSettingViewModel>();
        private readonly ObservableCollection<ModSettingsWorkingSource> settingsSources =
            new ObservableCollection<ModSettingsWorkingSource>();
        private ModSettingsWorkingSource selectedSettingsSource;
        private string preferredSettingsSourceToken = string.Empty;
        private PublishedModSettingsPreset pendingDeletePreset;
        private string pendingOverwriteId;
        private PresetSaveSelection[] pendingOverwriteSelections;
        private string presetInlineConfirmationTitle = string.Empty;
        private string presetInlineConfirmationMessage = string.Empty;
        private string presetOperationStatus = string.Empty;
        private bool presetOperationFailed;
#endif

        protected PresetLobbyModSettingsViewModel()
        {
#if !API_SHARED_PRESET_TESTS
            System_ToggleModSettingsSearchCommand = new RelayCommand(ToggleModSettingsSearch);
            System_ClearModSettingsSearchCommand = new RelayCommand(ClearModSettingsSearch);
            System_OpenPresetLoadCommand = new RelayCommand(OpenPresetLoad);
            System_ConfirmPresetLoadCommand = new RelayCommand(ConfirmPresetLoad);
            System_DeletePresetCommand = new RelayCommand(DeleteSelectedPreset);
            System_CancelPresetLoadCommand = new RelayCommand(CancelPresetLoad);
            System_OpenPresetSaveCommand = new RelayCommand(OpenPresetSave);
            System_ConfirmPresetSaveCommand = new RelayCommand(ConfirmPresetSave);
            System_CancelPresetSaveCommand = new RelayCommand(CancelPresetSave);
            System_LoadSettingsSourceCommand = new RelayCommand(LoadSelectedSettingsSource);
            System_ConfirmPresetInlineActionCommand = new RelayCommand(ConfirmPresetInlineAction);
            System_CancelPresetInlineActionCommand = new RelayCommand(CancelPresetInlineAction);
            System_DismissPresetStatusCommand = new RelayCommand(DismissPresetStatus);
#endif
        }

        public bool HasHostSettings => presetController?.HasHostSettings ?? false;

        public bool HasClientSettings => presetController?.HasClientSettings ?? false;

        public bool HasHostSettingsActivation => presetController?.HasHostSettingsActivation ?? false;

        public bool HasClientSettingsActivation => presetController?.HasClientSettingsActivation ?? false;

        public bool HostSettingsEnabled
        {
            get => presetController?.HostSettingsEnabled ?? false;
            set => presetController?.SetHostSettingsEnabled(value);
        }

        public bool ClientSettingsEnabled
        {
            get => presetController?.ClientSettingsEnabled ?? false;
            set => presetController?.SetClientSettingsEnabled(value);
        }

        public bool IsLocalSettingsHost => isLocalHost;

        public bool IsRealMultiplayerContext => isRealMultiplayer;

        public bool MissionPresetEditable => missionPresetEditable;

        public bool IsMissionPresetSelected => missionPresetContext && selectedPreset == (presetController?.MissionPresetIndex ?? 2);

        public Visibility System_DirectLaunchNoticeVisibility =>
            showDirectLaunchNotice &&
            (settingsMenuContext == SettingsMenuContext.Campaign ||
             (settingsMenuContext == SettingsMenuContext.DirectTrail &&
              (!missionPresetContext || !missionPresetHasExplicitSettings)))
                ? Visibility.Visible : Visibility.Collapsed;

        public string System_DirectLaunchNoticeText => isCastlePlannerSettings
            ? ResolveSettingsUiTextSafe("Common.DirectLaunchCastlePlannerNotice",
                "Castle spawning and gameplay changes are inactive for this direct start. Blueprints remain available. Use Customize to play with these changes; edits here are saved for later games.")
            : ResolveSettingsUiTextSafe("Common.DirectLaunchNotice",
                "This mod's gameplay changes are inactive for this direct start. Use Customize to play with them; edits here are saved for later games.");

        public Visibility System_TrailSourceNoticeVisibility =>
            settingsMenuContext == SettingsMenuContext.DirectTrail &&
            missionPresetContext && !missionPresetEditable && missionPresetHasExplicitSettings
                ? Visibility.Visible : Visibility.Collapsed;

        public string System_TrailSourceNoticeText =>
            ResolveSettingsUiTextSafe("Common.TrailSourceReadOnlyNotice",
                "These settings come from the selected Trail and are read-only here. Use Customize to change them.");

        public bool CanEditHostSettings =>
            isLocalHost && (!IsMissionPresetSelected || missionPresetEditable);

        /// <summary>Recognizes removed properties whose stored preset values may be ignored.</summary>
        protected virtual bool IsRetiredPresetProperty(string propertyName) => false;

        public bool CanEditClientSettings => !IsMissionPresetSelected || missionPresetEditable;

        public bool CanToggleHostSettings =>
            HasHostSettings && HasHostSettingsActivation && CanEditHostSettings;

        public bool CanToggleClientSettings =>
            HasClientSettings && HasClientSettingsActivation && CanEditClientSettings;

        public bool CanChangePreset =>
            (!IsMissionPresetSelected || missionPresetEditable) && (isLocalHost || HasClientSettings);

        public bool CanResetSettings => CanEditHostSettings || (HasClientSettings && CanEditClientSettings);

        public Visibility PresetVisibility =>
            missionPresetContext || isLocalHost || HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility ClientSettingsActivationVisibility =>
            HasClientSettingsActivation
                ? Visibility.Visible
                : Visibility.Collapsed;

        public Visibility HostReadOnlyNoticeVisibility =>
            HasHostSettings && isRealMultiplayer && !isLocalHost
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string HostOptionsText =>
            ResolveSettingsUiTextSafe("Common.HostOptions", "HOST OPTIONS");

        public string ClientOptionsText =>
            ResolveSettingsUiTextSafe("Common.ClientOptions", "LOCAL CLIENT OPTIONS");

        public string PresetText =>
            ResolveSettingsUiTextSafe("Common.Preset", "Preset");

        public string ModEnabledText =>
            ResolveSettingsUiTextSafe("Common.EnableMod", "Enable Mod");

        public string HostActivationLabelText =>
            ResolveSettingsUiTextSafe("Common.HostActivationLabel", "(Host-)");

        public string ClientActivationLabelText =>
            ResolveSettingsUiTextSafe("Common.ClientActivationLabel", "(Client settings)");

        public Visibility ActionsScopeNoticeVisibility =>
            isRealMultiplayer && HasClientSettings
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string ActionsScopeNoticeText =>
            HasHostSettings && isLocalHost
                ? ResolveSettingsUiTextSafe(
                    "Common.ActionsScopeHost",
                    "Loading a preset or resetting settings affects host settings and your local client settings.")
                : ResolveSettingsUiTextSafe(
                    "Common.ActionsScopeClient",
                    "Loading a preset or resetting settings affects only your local client settings.");

        public string HostReadOnlyNoticeText =>
            ResolveSettingsUiTextSafe("Common.HostReadOnly", "Values from host - read-only");

        public string EnableModHelpText =>
            ResolveSettingsUiTextSafe("Common.EnableModHelp", "Enables or disables this mod for the match.");

        public string HostSettingsActivationHelpText =>
            ResolveSettingsUiTextSafe("Common.HostSettingsActivationHelp", "Enables or disables all host-controlled settings of this mod.");

        public string ClientSettingsActivationHelpText =>
            ResolveSettingsUiTextSafe("Common.ClientSettingsActivationHelp", "Enables or disables all local and personal client settings of this mod.");

        public string PresetHelpText =>
            ResolveSettingsUiTextSafe("Common.PresetHelp", "Loads saved settings as an editable working copy.");



        // Compatibility alias for older views. New XAML binds host and client
        // sections separately so multiplayer and Trail locks remain independent.
        public bool AreSettingsEditable => CanEditHostSettings;

        public bool IsMissionPresetActive => missionPresetContext;

        protected virtual string ResolveSettingsUiText(string key, string fallback) => fallback;

        /// <summary>Applies an explicitly confirmed selection. Overrides may add deferred application.</summary>
        protected virtual void ApplyConfirmedPresetSelection(PublishedModSettingsPreset preset)
        {
            if (presetController == null) throw new InvalidOperationException("Preset controller is not initialized.");
            if (SettingsApplicationBackend != null)
                SettingsApplicationBackend.ReplaceDesiredValues(SettingsApplicationBackend.ReadOwnValues());
            presetController.LoadPreset(preset);
            System_CommitConfiguration();
        }

        private string ResolveSettingsUiTextSafe(string key, string fallback)
        {
            string resolved = ResolveSettingsUiText(key, fallback);
            return string.IsNullOrWhiteSpace(resolved) || string.Equals(resolved, key, StringComparison.Ordinal)
                ? ResolveApiSharedFallback(key, fallback)
                : resolved;
        }

        private static string ResolveApiSharedFallback(string key, string english)
        {
            string language = string.Empty;
#if !API_SHARED_PRESET_TESTS
            try { language = GameAssetManagerAPI.Instance.CurrentLanguage ?? string.Empty; }
            catch { }
#endif
            if (!language.StartsWith("de", StringComparison.OrdinalIgnoreCase))
                return english;
            switch (key)
            {
                case "Common.PresetLoad": return "Preset laden";
                case "Common.PresetSave": return "Preset speichern";
                case "Common.PresetBasedOn": return "Basiert auf";
                case "Common.PresetModified": return "geändert";
                case "Common.PresetLoadConfirm": return "Laden";
                case "Common.PresetLoadCancel": return "Abbrechen";
                case "Common.PresetDelete": return "Löschen";
                case "Common.PresetDeleteTitle": return "Eigenes Preset löschen";
                case "Common.PresetDeleteConfirm": return "Das eigene Preset wird endgültig gelöscht. Fortfahren?";
                case "Common.PresetDeleteFailedTitle": return "Preset konnte nicht gelöscht werden";
                case "Common.PresetSaveTarget": return "Speichern als";
                case "Common.PresetSaveNew": return "Neues persönliches Preset";
                case "Common.PresetSaveName": return "Presetname";
                case "Common.PresetSaveDescription": return "Beschreibung (optional)";
                case "Common.PresetSaveBulkMode": return "Alle Modi setzen";
                case "Common.PresetSaveBulkModeHelp": return "Standard: Mod-Standard verwenden. Spieler: aktuellen Spielerwert behalten. Fest: gespeicherten Wert anwenden. Host fest: Hostwerte festlegen, Spieler-/lokale Werte behalten.";
                case "Common.PresetLoadSelectionHelp": return "Die Auswahl ändert noch nichts. Erst Laden wendet das Preset an.";
                case "Common.PresetSaveCancel": return "Abbrechen";
                case "Common.PresetSourcePersonal": return "Eigene Presets";
                case "Common.PresetSourceBundled": return "Mit diesem Mod geliefert";
                case "Common.PresetSourceExternal": return "Externe Presets";
                case "Common.PresetSaveConfirm": return "Speichern";
                case "Common.PresetSaveNameRequired": return "Vor dem Speichern einen Presetnamen eingeben.";
                case "Common.PresetModeHostFixed": return "Host fest";
                case "Common.PresetSaveOverwriteTitle": return "Eigenes Preset überschreiben";
                case "Common.PresetSaveOverwrite": return "Das gewählte eigene Preset wird vollständig ersetzt. Fortfahren?";
                case "Common.PresetSaveFailedTitle": return "Preset konnte nicht gespeichert werden";
                case "Common.PresetLoadFailedTitle": return "Preset konnte nicht geladen werden";
                case "Common.SettingsSource": return "Einstellungen zurücksetzen auf";
                case "Common.SettingsSourceLoad": return "Zurücksetzen";
                case "Common.SettingsSourceHelp": return "Setzt die Einstellungen dieser Mod auf die gewählte Quelle zurück. Eigene Presets bleiben unverändert. Im Mehrspieler kann nur der Host die Host-Einstellungen zurücksetzen.";
                case "Common.SettingsSourceDefaults": return "Mod-Standards";
                case "Common.SettingsSourceTrail": return "Trail-Einstellungen";
                case "Common.SettingsSourceMap": return "Map-Einstellungen";
                case "Common.SettingsSourceLoadFailed": return "Einstellungen konnten nicht zurückgesetzt werden";
                case "Common.DirectLaunchNotice": return "Die Spieländerungen dieser Mod sind für diesen direkten Start inaktiv. Über „Customize“ starten, um damit zu spielen. Änderungen hier werden für spätere Partien gespeichert.";
                case "Common.DirectLaunchCastlePlannerNotice": return "Burgplatzierung und Spieländerungen sind für diesen direkten Start inaktiv; Blaupausen bleiben verfügbar. Über „Customize“ starten, um die Spieländerungen zu nutzen. Änderungen hier werden gespeichert.";
                case "Common.TrailSourceReadOnlyNotice": return "Diese Werte stammen aus dem gewählten Trail und sind hier schreibgeschützt. Zum Ändern „Customize“ wählen.";
                case "Common.PresetConfirm": return "Bestätigen";
                case "Common.PresetStatusDismiss": return "Schließen";
                default: return english;
            }
        }

        protected bool IsApplyingSettingsSnapshot =>
            presetController?.IsApplyingSnapshot == true;

        /// <summary>
        /// Replaces one captured code-default value without changing the current working settings.
        /// This is intended for defaults which can only be materialized after dynamic discovery.
        /// </summary>
        protected void SetModDefaultValue<T>(string propertyName, T value)
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before dynamic defaults are updated.");
            presetController.SetDefaultValue(propertyName, value);
        }

        /// <summary>Optional dynamically described local working configuration.</summary>
        protected virtual IDynamicPresetSettingsProvider DynamicSettingsProvider => null;
        protected virtual IModSettingsApplicationBackend SettingsApplicationBackend => DynamicSettingsProvider as IModSettingsApplicationBackend;
        public bool System_HasDynamicSettings => DynamicSettingsProvider != null;
        public object System_ReadDescribedValue(string key) => DynamicSettingsProvider.ReadValue(key);
        public bool System_HasApplicationBackend => SettingsApplicationBackend != null;
        public string System_ApplicationNotice { get; private set; } = "";
        public bool System_HasPendingConfiguration
        {
            get
            {
                try { return SettingsApplicationBackend != null && (SettingsApplicationBackend.ReadPendingValues() != null || ModSettingsApplication.HasRestartPreparation); }
                catch { return SettingsApplicationBackend != null; } // Keep discard reachable for a corrupt package.
            }
        }
        public void System_DiscardPendingConfiguration()
        {
            if (ModSettingsApplication.HasRestartPreparation) ModSettingsApplication.DiscardPreparation();
            else DiscardApplicationPackage();
        }
        internal void DiscardApplicationPackage()
        {
            var backend = SettingsApplicationBackend;
            if (backend == null) return;
            backend.DiscardPendingConfiguration();
            bool returnToOwn = !string.IsNullOrEmpty(backend.ActiveContextId);
            backend.ReplaceDesiredValues(returnToOwn ? backend.ReadOwnValues() : backend.ReadActiveValues());
            if (returnToOwn)
            {
                if (System_GetPresetSettingDescriptors().Any(x => x.RequiresRestart)) backend.ReturnToOwnConfiguration();
                else
                {
                    backend.ApplyValues(backend.ReadDesiredValues(), "");
                    var applied = backend.ReadActiveValues();
                    if (backend.ReadDesiredValues().Any(x => !applied.TryGetValue(x.Key, out var value) || !Equals(value, x.Value)))
                        throw new InvalidOperationException("Configuration backend did not restore personal values.");
                }
            }
            var activeAfterDiscard = backend.ReadActiveValues();
            var desiredAfterDiscard = backend.ReadDesiredValues();
            ReportConfigurationResult(returnToOwn && System_GetPresetSettingDescriptors().Any(x => x.RequiresRestart &&
                (!activeAfterDiscard.TryGetValue(x.PropertyName, out var value) || !Equals(value, desiredAfterDiscard[x.PropertyName]))));
        }
        public void System_CommitConfiguration()
        {
            bool restart = ModSettingsApplication.Commit(presetController.TargetGuid);
            ReportConfigurationResult(restart);
        }
        internal void ReportConfigurationResult(bool restart) => SetConfigurationNotice(restart
            ? ResolveSettingsUiTextSafe("Common.RestartRequired", "Settings prepared. Restart the game to apply them.") : "");
        protected void SetConfigurationNotice(string message)
        {
            System_ApplicationNotice = message ?? "";
            RefreshConfigurationBindings();
        }
        internal void RefreshConfigurationBindings()
        {
            OnPropertyChanged(nameof(System_ApplicationNotice));
            OnPropertyChanged(nameof(System_HasPendingConfiguration));
#if !API_SHARED_PRESET_TESTS
            OnPropertyChanged(nameof(System_PresetStatusText));
            OnPropertyChanged(nameof(System_PresetStatusVisibility));
#endif
        }
        internal Dictionary<string, byte[]> CaptureApplicationSnapshot()
        {
            if (SettingsApplicationBackend == null) return System_CreateCurrentWorkingSnapshot();
            return SettingsApplicationBackend.ReadDesiredValues().ToDictionary(x => x.Key,
                x => MessagePackSerializer.Serialize(x.Value.GetType(), x.Value), StringComparer.Ordinal);
        }
        internal Dictionary<string, string> CaptureRestartSources()
        {
#if API_SHARED_PRESET_TESTS
            return new Dictionary<string, string>();
#else
            return new Dictionary<string, string> {
                ["resetSource"] = selectedSettingsSource?.Id ?? "",
                ["label"] = presetController?.GetStatusText("Based on", "modified", "Personal presets", "Bundled with this mod", "External presets") ?? ""
            };
#endif
        }
        internal void RestoreRestartSources(Dictionary<string, string> sources)
        {
#if !API_SHARED_PRESET_TESTS
            if (sources.TryGetValue("resetSource", out string selected))
                System_SelectedSettingsSource = settingsSources.FirstOrDefault(x => x.Id == selected) ?? selectedSettingsSource;
            if (sources.TryGetValue("label", out string label) && !string.IsNullOrWhiteSpace(label))
                presetController?.RestorePreparationLabel(label);
            RaiseAccessProperties();
#endif
        }
        public string System_OwnConfigurationFingerprint()
        {
            if (SettingsApplicationBackend == null) return "";
            var values = SettingsApplicationBackend.ReadOwnValues();
            using (var buffer = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(buffer))
            {
                foreach (var pair in values.OrderBy(x => x.Key, StringComparer.Ordinal))
                {
                    writer.Write(pair.Key);
                    byte[] bytes = MessagePackSerializer.Serialize(pair.Value.GetType(), pair.Value);
                    writer.Write(bytes.Length); writer.Write(bytes);
                }
                writer.Flush();
                using (var sha = System.Security.Cryptography.SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(buffer.ToArray())).Replace("-", "");
            }
        }
        public void System_RefreshOwnConfiguration()
        {
            if (SettingsApplicationBackend != null)
                SettingsApplicationBackend.ReplaceDesiredValues(SettingsApplicationBackend.ReadOwnValues());
        }
        public bool System_ConfigurationNeedsRestart()
        {
            if (SettingsApplicationBackend == null) return false;
            if (SettingsApplicationBackend is INetworkModSettingsApplicationBackend network && network.IsNetworkConfigurationClient)
                return false; // The provider's authenticated transport owns client startup preparation.
            var active = SettingsApplicationBackend.ReadActiveValues();
            var desired = SettingsApplicationBackend.ReadDesiredValues();
            return System_GetPresetSettingDescriptors().Any(x => x.RequiresRestart &&
                (!active.TryGetValue(x.PropertyName, out var value) || !Equals(value, desired[x.PropertyName])));
        }
        public bool System_ApplyConfiguration(string contextId) => System_ApplyConfiguration(contextId, false);
        internal bool System_ApplyConfiguration(string contextId, bool personalChoiceConfirmed)
        {
            var backend = SettingsApplicationBackend;
            if (backend == null) return false;
            if (backend is INetworkModSettingsApplicationBackend network && network.IsNetworkConfigurationClient)
                return !network.PrepareNetworkConfiguration();
            var desired = backend.ReadDesiredValues();
            var active = backend.ReadActiveValues();
            var changed = System_GetPresetSettingDescriptors().Where(x => !active.TryGetValue(x.PropertyName, out var value) || !Equals(value, desired[x.PropertyName])).ToArray();
            if (changed.Length == 0)
            {
                // A temporary configuration can already match the requested personal preset.
                // Preserve that personal choice for future starts without inventing a restart need.
                var own = backend.ReadOwnValues();
                bool persistPersonal = string.IsNullOrEmpty(contextId) && desired.Any(x =>
                    !own.TryGetValue(x.Key, out var value) || !Equals(value, x.Value));
                if (persistPersonal && personalChoiceConfirmed)
                {
                    if (System_GetPresetSettingDescriptors().Any(x => x.RequiresRestart)) backend.StageValues(desired, "");
                    else backend.ApplyValues(desired, "");
                }
                else
                {
                    var pending = backend.ReadPendingValues();
                    bool keepConfirmedPersonalChoice = persistPersonal && pending != null && desired.All(x =>
                        pending.TryGetValue(x.Key, out var value) && Equals(value, x.Value));
                    if (pending != null && !keepConfirmedPersonalChoice) backend.DiscardPendingConfiguration();
                }
                return false;
            }
            if (changed.Any(x => x.RequiresRestart))
            {
                backend.StageValues(desired, contextId);
                return true;
            }
            backend.ApplyValues(desired, contextId);
            var applied = backend.ReadActiveValues();
            if (desired.Any(x => !applied.TryGetValue(x.Key, out var value) || !Equals(value, x.Value)))
                throw new InvalidOperationException("Configuration backend did not apply the requested values.");
            if (backend.ReadPendingValues() != null) backend.DiscardPendingConfiguration();
            return false;
        }
        public void System_ReturnToOwnConfiguration()
        {
            var backend = SettingsApplicationBackend;
            if (backend == null) return;
            // Leaving for an explicitly prepared restart must not replace that preparation.
            if (backend.ReadPendingValues() != null) return;
            backend.ReplaceDesiredValues(backend.ReadOwnValues());
            if (!string.IsNullOrEmpty(backend.ActiveContextId))
            {
                if (System_GetPresetSettingDescriptors().Any(x => x.RequiresRestart)) backend.ReturnToOwnConfiguration();
                else
                {
                    backend.ApplyValues(backend.ReadDesiredValues(), "");
                    var applied = backend.ReadActiveValues();
                    if (backend.ReadDesiredValues().Any(x => !applied.TryGetValue(x.Key, out var value) || !Equals(value, x.Value)))
                        throw new InvalidOperationException("Configuration backend did not restore personal values.");
                }
            }
        }

        protected virtual void OnSettingsSnapshotApplied()
        {
        }

        /// <summary>
        /// Declares the few domain-specific parts of personal settings. Transport,
        /// player-slot ownership, lobby convergence and readiness stay in APIShared.
        /// </summary>
        protected virtual void ConfigurePerPlayerLobbySettings(
            PerPlayerLobbySettingsBuilder settings)
        {
        }

        public bool IsPerPlayerLobbySettingsReady =>
            perPlayerSettingsCoordinator?.IsReady ?? true;

        public string PerPlayerLobbySettingsReadinessError =>
            perPlayerSettingsCoordinator?.ReadinessError ?? string.Empty;

        public void System_RequestPerPlayerSettingsPublish()
        {
            perPlayerSettingsCoordinator?.RequestPublish();
        }

#if !API_SHARED_PRESET_TESTS
        // SerpsModsHost discovers this method by reflection. Keeping the bridge on the
        // common base type lets every mod remain usable without the optional pack host.
        public IReadOnlyList<ModSettingsSearchEntry> System_GetModSettingsSearchEntries(
            Noesis.FrameworkElement view) =>
            ModSettingsSearch.Export(this, view);

#endif

        public bool System_ArePerPlayerSettingsReady(
            IEnumerable<int> playerIds,
            out string error)
        {
            if (perPlayerSettingsCoordinator == null)
            {
                error = string.Empty;
                return true;
            }

            return perPlayerSettingsCoordinator.ArePlayersReady(playerIds, out error);
        }

#if API_SHARED_PRESET_TESTS
        internal void System_TestObservePerPlayerLobby(
            ulong? lobbyId,
            IReadOnlyDictionary<int, ulong> players,
            bool hasUnresolvedPlayers,
            int localPlayerId,
            bool preserveForMapTransition = false)
        {
            perPlayerSettingsCoordinator?.Observe(
                lobbyId,
                players,
                hasUnresolvedPlayers,
                localPlayerId,
                preserveForMapTransition);
        }

        internal bool System_TestRemapPerPlayerLobbyForMapTransition(
            IReadOnlyDictionary<int, ulong> players,
            int localPlayerId,
            out string error)
        {
            if (perPlayerSettingsCoordinator == null)
            {
                error = "The per-player settings coordinator is unavailable.";
                return false;
            }
            return perPlayerSettingsCoordinator.RemapForMapTransition(
                players,
                localPlayerId,
                out error);
        }
#endif

        /// <summary>
        /// Authorizes a settings mutation before any backing state is changed.
        /// Preset and Trail snapshots are trusted internal applications; all other
        /// writes use the Script Extender's ownership gate.
        /// </summary>
        protected bool CanMutateSetting([CallerMemberName] string propertyName = null)
        {
            if (presetController?.IsApplyingSnapshot == true)
                return true;

            // The Extender reaches the setter only after it has verified the packet's
            // sender and opened its authorised-update scope. A read-only Trail locks
            // local edits, but must not reject that authoritative host state.
            if (PresetController.IsNetworkSyncInProgress())
                return CanEdit(propertyName);

            System_RefreshSettingsAccess();
            if (IsMissionPresetSelected && !missionPresetEditable)
            {
                NotifyRejectedProperty(propertyName);
                return false;
            }

            return CanEdit(propertyName);
        }

        /// <summary>
        /// Also refreshes editable proxy properties after a rejected write. The
        /// Extender's private revert path keeps these notifications out of sync
        /// and storage just like the primary property notification.
        /// </summary>
        protected bool CanMutateSettingWithDependents(
            string propertyName,
            params string[] dependentPropertyNames)
        {
            if (CanMutateSetting(propertyName))
                return true;

            if (dependentPropertyNames == null)
                return false;

            foreach (string dependentPropertyName in dependentPropertyNames)
            {
                if (!string.IsNullOrEmpty(dependentPropertyName) &&
                    !string.Equals(propertyName, dependentPropertyName, StringComparison.Ordinal))
                {
                    NotifyRejectedProperty(dependentPropertyName);
                }
            }

            return false;
        }

        private void NotifyRejectedProperty(string propertyName)
        {
            if (NotifyRevertMethod != null && !string.IsNullOrEmpty(propertyName))
                NotifyRevertMethod.Invoke(this, new object[] { propertyName });
        }

#if API_SHARED_PRESET_TESTS
        internal int System_WorkingStateWriteCount => presetController?.TestWriteCount ?? 0;

        internal string System_TestPresetStatusText => presetController?.GetStatusText(
            "Based on", "modified", "Personal presets", "Bundled with this mod", "External presets") ?? string.Empty;

        // Retained only in the source-linked regression harness for hostile legacy selection attempts.
        public int SelectedPreset
        {
            get => selectedPreset;
            set
            {
                int normalized = presetController?.NormalizeSelection(value, missionPresetContext) ??
                    (value == 1 ? 1 : 0);
                if (selectedPreset == normalized)
                    return;

                if (presetController == null)
                {
                    selectedPreset = normalized;
                    base.OnPropertyChanged(nameof(SelectedPreset));
                    return;
                }

                presetController.SwitchTo(normalized);
            }
        }
#endif

        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName,
            string targetGuid,
            Version targetVersion,
            bool logRoutineActivity = true, bool publishParticipant = true)
        {
            if (presetController != null)
                throw new InvalidOperationException($"Preset storage for [{modName}] was already prepared.");

            presetController = new PresetController(
                this,
                log,
                pluginAssemblyLocation,
                modName,
                targetGuid,
                targetVersion,
                logRoutineActivity);
            presetController.CaptureDefaults();
            if (publishParticipant)
            {
#if !API_SHARED_PRESET_TESTS
                PublishParticipant(targetGuid, pluginAssemblyLocation);
#endif
            }
#if !API_SHARED_PRESET_TESTS
            RebuildSettingsSources();
#endif
            PropertyChanged += (_, __) => System_RefreshSettingsAccess();
            System_RefreshSettingsAccess();
        }

#if API_SHARED_PRESET_TESTS
        internal void PreparePresets(
            ManualLogSource log,
            string pluginAssemblyLocation,
            string modName)
        {
            PreparePresets(
                log,
                pluginAssemblyLocation,
                modName,
                "Tests." + modName,
                new Version(1, 0, 0));
        }
#endif

        internal void ActivatePresets()
        {
            if (presetController == null)
                throw new InvalidOperationException("Preset storage must be prepared before it is activated.");

            presetController.Activate();
        }

        internal void PublishParticipant(string id, string assemblyPath)
        {
            ModSettingsApplication.Register(id, this, assemblyPath);
#if !API_SHARED_PRESET_TESTS
            ModSettingsWorkingSourceRegistry.SourcesChanged += RebuildSettingsSources;
#endif
        }

        internal void PreparePerPlayerLobbySettings(
            ManualLogSource log,
            string modName,
            string ownerGuid,
            bool logRoutineActivity = true)
        {
            if (perPlayerSettingsCoordinator != null)
                throw new InvalidOperationException($"Per-player lobby settings for [{modName}] were already prepared.");

            var builder = new PerPlayerLobbySettingsBuilder(this);
            ConfigurePerPlayerLobbySettings(builder);
            perPlayerSettingsCoordinator = new PerPlayerLobbySettingsCoordinator(
                this,
                log,
                modName,
                ownerGuid,
                builder.Build(),
                logRoutineActivity);
        }

        internal void ActivatePerPlayerLobbySettings()
        {
            if (perPlayerSettingsCoordinator == null)
                throw new InvalidOperationException("Per-player lobby settings must be prepared before activation.");
            perPlayerSettingsCoordinator.Activate();
        }

        internal void DeactivatePerPlayerLobbySettings()
        {
            perPlayerSettingsCoordinator?.Deactivate();
            perPlayerSettingsCoordinator = null;
        }

        // Typed mission-preset endpoint used by ExtendedData and other optional coordinators.
        public Dictionary<string, byte[]> System_CreateDisabledMissionPresetSnapshot() =>
            presetController?.CreateDisabledSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreateModDefaultSnapshot() =>
            presetController?.CreateDefaultSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public Dictionary<string, byte[]> System_CreateCurrentMissionPresetSnapshot() =>
            System_CreateCurrentWorkingSnapshot();

        public Dictionary<string, byte[]> System_CreatePlayerMissionPresetSnapshot()
        {
            if (SettingsApplicationBackend != null)
                return SettingsApplicationBackend.ReadOwnValues().ToDictionary(x => x.Key,
                    x => MessagePackSerializer.Serialize(x.Value.GetType(), x.Value), StringComparer.Ordinal);
            return presetController?.CreatePlayerMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        public void System_ApplyMissionPresetSnapshot(Dictionary<string, byte[]> snapshot, string label)
        {
            presetController?.ApplyMissionWorkingSnapshot(snapshot, label);
            RaiseAccessProperties();
        }

        public Dictionary<string, byte[]> System_CreateCurrentWorkingSnapshot() =>
            SettingsApplicationBackend != null ? CaptureApplicationSnapshot() :
            presetController?.CreateCurrentMissionSnapshot() ?? new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public void System_ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot)
        {
            presetController?.ApplyWorkingSnapshot(snapshot);
            RaiseAccessProperties();
        }

        public void System_LoadModDefaults()
        {
            SetConfigurationNotice("");
            var backend = SettingsApplicationBackend;
            var activeBefore = backend?.ReadActiveValues();
            var pendingBefore = backend?.ReadPendingValues();
            if (IsMissionPresetSelected)
                ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, ModSettingsWorkingSourceRegistry.ModDefaultsId);
            else
                presetController?.ApplyDefaultsAsWorkingCopy();
            System_CommitConfiguration();
            if (backend != null)
            {
                var desired = backend.ReadDesiredValues();
                var pendingAfter = backend.ReadPendingValues();
                bool activeMatches = ConfigurationValuesEqual(desired, backend.ReadActiveValues());
                if (pendingBefore != null && pendingAfter == null && activeMatches)
                    SetConfigurationNotice(ResolveSettingsUiTextSafe("Common.DefaultResetDiscarded",
                        "Previous preparation discarded. Mod defaults are active. No restart needed."));
                else if (pendingBefore != null && !ConfigurationValuesEqual(pendingBefore, pendingAfter) && pendingAfter != null)
                    SetConfigurationNotice(ResolveSettingsUiTextSafe("Common.DefaultResetReplaced",
                        "Previous preparation replaced with mod defaults.") + " " + (activeMatches
                            ? ResolveSettingsUiTextSafe("Common.DefaultResetPreparedActive", "Prepared for future starts. Current values already match; no restart needed.")
                            : ResolveSettingsUiTextSafe("Common.RestartRequired", "Settings prepared. Restart the game to apply them.")));
                else if (activeMatches && pendingAfter != null)
                    SetConfigurationNotice(ResolveSettingsUiTextSafe("Common.DefaultResetPreparedActive",
                        "Prepared for future starts. Current values already match; no restart needed."));
                else if (activeMatches)
                    SetConfigurationNotice(ConfigurationValuesEqual(activeBefore, desired)
                        ? ResolveSettingsUiTextSafe("Common.DefaultResetUnchanged", "Settings already match the mod defaults. No restart needed.")
                        : ResolveSettingsUiTextSafe("Common.DefaultResetApplied", "Mod defaults applied. No restart needed."));
            }
            RaiseAccessProperties();
        }

        private static bool ConfigurationValuesEqual(Dictionary<string, object> left, Dictionary<string, object> right) =>
            left != null && right != null && left.Count == right.Count &&
            left.All(pair => right.TryGetValue(pair.Key, out var value) && Equals(pair.Value, value));

        /// <summary>Returns the persistent settings available to shared preset authoring UI.</summary>
        public IReadOnlyList<PresetSettingDescriptor> System_GetPresetSettingDescriptors() =>
            presetController?.GetSettingDescriptors() ?? Array.Empty<PresetSettingDescriptor>();

        /// <summary>Imports a validated preset through the shared catalog, without overwriting an existing preset.</summary>
        public string System_ImportPresetJson(string json)
        {
            if (!CanChangePreset) throw new InvalidOperationException("Preset editing is locked.");
            string path = presetController.ImportPresetJson(json);
#if !API_SHARED_PRESET_TESTS
            RebuildPresetDialogCatalogs();
            RaisePresetDialogProperties();
#endif
            return path;
        }

#if !API_SHARED_PRESET_TESTS
        /// <summary>Exports the selected catalog preset with its original per-option modes.</summary>
        public string System_ExportSelectedPresetJson()
        {
            PublishedModSettingsPreset preset = selectedPresetLoadEntry?.Preset;
            if (preset == null) throw new InvalidOperationException("Select a saved preset in the load list first.");
            return ModSettingsPresetJson.Serialize(presetController.TargetGuid, preset.Id, preset.Name,
                preset.Description, preset.MinimumTargetVersion, preset.MaximumTargetVersion, preset.Settings);
        }

#endif
        /// <summary>Saves selected settings as a persistent, personal preset JSON file.</summary>
        public string System_SavePersonalPreset(
            string id,
            string name,
            string description,
            IEnumerable<PresetSaveSelection> selections,
            bool overwrite) =>
            presetController?.SavePersonalPreset(id, name, description, selections, overwrite) ?? string.Empty;

#if API_SHARED_PRESET_TESTS
        internal IReadOnlyList<PublishedModSettingsPreset> System_TestPublishedPresets =>
            presetController?.PublishedPresets ?? Array.Empty<PublishedModSettingsPreset>();

        internal void System_TestRefreshPresetCatalog() => presetController?.RefreshCatalog();

        internal void System_TestLoadPreset(string stableId)
        {
            PublishedModSettingsPreset preset = presetController?.PublishedPresets.FirstOrDefault(item =>
                string.Equals(item.StableId, stableId, StringComparison.Ordinal));
            if (preset == null)
                throw new InvalidDataException("The requested test preset is unavailable.");
            ApplyConfirmedPresetSelection(preset);
        }

        internal void System_TestDeletePreset(string stableId)
        {
            PublishedModSettingsPreset preset = presetController?.PublishedPresets.FirstOrDefault(item =>
                string.Equals(item.StableId, stableId, StringComparison.Ordinal));
            if (preset == null)
                throw new InvalidDataException("The requested test preset is unavailable.");
            presetController.DeletePersonalPreset(preset);
        }

#endif

        public void System_SetExplicitMissionSettings(bool hasExplicitSettings)
        {
            missionPresetHasExplicitSettings = hasExplicitSettings;
            RaiseAccessProperties();
        }

        public void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable)
        {
            if (presetController == null)
                return;

            missionPresetContext = true;
            missionPresetEditable = editable;
            presetController.EnterMissionPreset(snapshot, label, editable);
            RaiseAccessProperties();
        }

        public void System_ExitMissionPreset()
        {
            if (!missionPresetContext || presetController == null)
                return;

            missionPresetContext = false;
            missionPresetEditable = false;
            missionPresetHasExplicitSettings = false;
            presetController.ExitMissionPreset();
            RaiseAccessProperties();
        }

        public void System_RefreshSettingsAccess()
        {
            bool currentIsRealMultiplayer;
            bool currentIsHost;
            SettingsMenuContext currentMenuContext;
            try
            {
                currentIsRealMultiplayer = GameModeHelper.IsRealMultiplayer();
                // Authority and game-mode presentation are independent. The Extender
                // correctly reports local Skirmish and Trail lobbies as local host.
                currentIsHost = GameNetworkAPI.IsLocalHost();
                currentMenuContext = CaptureSettingsMenuContext();
            }
            catch
            {
                // Registration can precede the network singleton. Preserve the last
                // confirmed role so a transient failure never unlocks a client.
                return;
            }

            if (isLocalHost == currentIsHost && isRealMultiplayer == currentIsRealMultiplayer &&
                settingsMenuContext == currentMenuContext)
                return;

            isLocalHost = currentIsHost;
            isRealMultiplayer = currentIsRealMultiplayer;
            settingsMenuContext = currentMenuContext;
            RaiseAccessProperties();
        }

        private static SettingsMenuContext CaptureSettingsMenuContext()
        {
#if API_SHARED_PRESET_TESTS
            return SettingsMenuContext.Other;
#else
            // Instance lazily constructs the entire game ViewModel. Settings are registered
            // before Vanilla has finished creating it, so never trigger that work here.
            if (!CrusaderDE.MainViewModel.viewModelLoaded)
                return SettingsMenuContext.Other;
            try
            {
                CrusaderDE.MainViewModel viewModel = CrusaderDE.MainViewModel.Instance;
                if (viewModel == null)
                    return SettingsMenuContext.Other;
                bool campaign = viewModel.Show_Historical1CampaignMenu || viewModel.Show_Historical2CampaignMenu ||
                    viewModel.Show_Historical3CampaignMenu || viewModel.Show_Historical4CampaignMenu ||
                    viewModel.Show_Historical5CampaignMenu || viewModel.Show_Historical6CampaignMenu ||
                    viewModel.Show_Historical7CampaignMenu;
                bool trail = viewModel.Show_TrailCampaignMenu || viewModel.Show_Trail2CampaignMenu ||
                    viewModel.Show_Trail3CampaignMenu || viewModel.Show_SandsTrail1Menu ||
                    viewModel.Show_SandsTrail2Menu || viewModel.Show_SandsTrail3Menu ||
                    viewModel.Show_SandsTrail4Menu || viewModel.Show_SandsTrail5Menu ||
                    viewModel.Show_SandsTrail6Menu || viewModel.Show_SandsTrail7Menu ||
                    viewModel.Show_SandsTrail8Menu;
                bool coopTrail = viewModel.Show_CoopTrail1 || viewModel.Show_CoopTrail2 ||
                    viewModel.Show_CoopTrail3 || viewModel.Show_CoopTrail4;
                return ResolveSettingsMenuContext(viewModel.Show_MultiplayerSetup, campaign, trail, coopTrail);
            }
            catch
            {
                // An unavailable front-end view never proves that a direct launch is active.
            }
            return SettingsMenuContext.Other;
#endif
        }

        private static SettingsMenuContext ResolveSettingsMenuContext(
            bool customizeSetup, bool campaign, bool trail, bool coopTrail)
        {
            // Vanilla keeps MultiplayerSetup visible behind a direct Coop Trail page.
            if (coopTrail && !campaign) return SettingsMenuContext.DirectTrail;
            if (customizeSetup) return SettingsMenuContext.CustomizeSetup;
            if (campaign && !trail) return SettingsMenuContext.Campaign;
            if (trail && !campaign) return SettingsMenuContext.DirectTrail;
            return SettingsMenuContext.Other;
        }

#if API_SHARED_PRESET_TESTS
        public void System_TestSetSettingsMenuContext(
            bool customizeSetup, bool campaign, bool trail, bool coopTrail = false)
        {
            settingsMenuContext = ResolveSettingsMenuContext(customizeSetup, campaign, trail, coopTrail);
            RaiseAccessProperties();
        }
#endif

        public void System_ConfigureDirectLaunchNotice(string modGuid)
        {
            if (string.IsNullOrWhiteSpace(modGuid))
                return;
            try
            {
                SerpsModProfiles.GetProfile(modGuid, modGuid);
                showDirectLaunchNotice = true;
                isCastlePlannerSettings = string.Equals(modGuid, "CastlePlanner_Serp", StringComparison.Ordinal);
            }
            catch (ArgumentOutOfRangeException)
            {
                showDirectLaunchNotice = false;
                isCastlePlannerSettings = false;
            }
            RaiseAccessProperties();
        }

        // Let the Extender process the notification first; then update our own working state.
        protected new void OnPropertyChanged(string name)
        {
            try
            {
                base.OnPropertyChanged(name);

                if (presetController?.IsHostSettingsActivationProperty(name) == true)
                    base.OnPropertyChanged(nameof(HostSettingsEnabled));
                if (presetController?.IsClientSettingsActivationProperty(name) == true)
                    base.OnPropertyChanged(nameof(ClientSettingsEnabled));
            }
            finally
            {
                presetController?.AfterPropertyChanged(name);
                System_RefreshSettingsAccess();
            }
        }

        private void SetSelectedPresetCore(int value)
        {
            if (selectedPreset == value)
                return;

            selectedPreset = value;
#if API_SHARED_PRESET_TESTS
            OnPropertyChanged(nameof(SelectedPreset));
#endif
            RaiseAccessProperties();
        }

        private void RaiseAccessProperties()
        {
            base.OnPropertyChanged(nameof(IsLocalSettingsHost));
            base.OnPropertyChanged(nameof(IsRealMultiplayerContext));
            base.OnPropertyChanged(nameof(HasHostSettings));
            base.OnPropertyChanged(nameof(HasClientSettings));
            base.OnPropertyChanged(nameof(HasHostSettingsActivation));
            base.OnPropertyChanged(nameof(HasClientSettingsActivation));
            base.OnPropertyChanged(nameof(HostSettingsEnabled));
            base.OnPropertyChanged(nameof(ClientSettingsEnabled));
            base.OnPropertyChanged(nameof(MissionPresetEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetSelected));
            base.OnPropertyChanged(nameof(CanEditHostSettings));
            base.OnPropertyChanged(nameof(CanEditClientSettings));
            base.OnPropertyChanged(nameof(CanToggleHostSettings));
            base.OnPropertyChanged(nameof(CanToggleClientSettings));
            base.OnPropertyChanged(nameof(CanChangePreset));
            base.OnPropertyChanged(nameof(CanResetSettings));
            base.OnPropertyChanged(nameof(PresetVisibility));
            base.OnPropertyChanged(nameof(ClientSettingsActivationVisibility));
            base.OnPropertyChanged(nameof(HostReadOnlyNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeVisibility));
            base.OnPropertyChanged(nameof(ActionsScopeNoticeText));
            base.OnPropertyChanged(nameof(AreSettingsEditable));
            base.OnPropertyChanged(nameof(IsMissionPresetActive));
            base.OnPropertyChanged(nameof(System_DirectLaunchNoticeVisibility));
            base.OnPropertyChanged(nameof(System_DirectLaunchNoticeText));
            base.OnPropertyChanged(nameof(System_TrailSourceNoticeVisibility));
            base.OnPropertyChanged(nameof(System_TrailSourceNoticeText));
#if !API_SHARED_PRESET_TESTS
            base.OnPropertyChanged(nameof(System_PresetStatusText));
            base.OnPropertyChanged(nameof(System_PresetStatusVisibility));
            base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
#endif
        }

        private static string GetVanillaText(
            ManualLogSource log,
            string key,
            string fallback)
        {
            try
            {
                if (CrusaderDE.Translate.Instance.GameTexts.TryGetValue(key, out string value) &&
                    !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch (Exception exception)
            {
                DebugLogHelper.LogWarning(
                    log,
                    $"Could not read Vanilla preset text [{key}]: {exception.Message}");
            }

            DebugLogHelper.LogWarning(
                log,
                $"Vanilla preset text [{key}] is unavailable; using [{fallback}].");
            return fallback;
        }


    }

}
