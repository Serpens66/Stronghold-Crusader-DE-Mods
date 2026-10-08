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
    /// <summary>Preset source selection, save/load UI and settings search bindings.</summary>
    public abstract partial class PresetLobbyModSettingsViewModel
    {
#if !API_SHARED_PRESET_TESTS
        public string System_PresetLoadText =>
            ResolveSettingsUiTextSafe("Common.PresetLoad", "Load preset");

        public string System_PresetSaveText =>
            ResolveSettingsUiTextSafe("Common.PresetSave", "Save preset");

        public string System_SettingsSourceText =>
            ResolveSettingsUiTextSafe("Common.SettingsSource", "Reset settings to");

        public string System_SettingsSourceLoadText =>
            ResolveSettingsUiTextSafe("Common.SettingsSourceLoad", "Reset");

        public string System_SettingsSourceHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.SettingsSourceHelp",
                "Resets this mod's settings to the selected source. Personal presets are not changed. In multiplayer, only the host can reset host settings.");

        public ObservableCollection<ModSettingsWorkingSource> System_SettingsSources => settingsSources;

        public ModSettingsWorkingSource System_SelectedSettingsSource
        {
            get => selectedSettingsSource;
            set
            {
                if (ReferenceEquals(selectedSettingsSource, value)) return;
                selectedSettingsSource = value;
                base.OnPropertyChanged(nameof(System_SelectedSettingsSource));
                base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
            }
        }

        public bool System_CanLoadSettingsSource =>
            selectedSettingsSource != null && (!IsMissionPresetSelected || missionPresetEditable);

        public Visibility System_SettingsSourceVisibility =>
            presetController?.HasPersistentSettings == true ? Visibility.Visible : Visibility.Collapsed;

        public string System_PresetStatusText => !string.IsNullOrEmpty(System_ApplicationNotice) ? System_ApplicationNotice : presetController?.GetStatusText(
            ResolveSettingsUiTextSafe("Common.PresetBasedOn", "Based on"),
            ResolveSettingsUiTextSafe("Common.PresetModified", "modified"),
            ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets"),
            ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod"),
            ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets")) ?? string.Empty;

        public Visibility System_PresetStatusVisibility =>
            string.IsNullOrWhiteSpace(System_PresetStatusText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility System_PresetLoadPanelVisibility =>
            presetLoadPanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<ModSettingsPresetListEntry> System_PresetLoadEntries =>
            presetLoadEntries;

        public ModSettingsPresetListEntry System_SelectedPresetLoadEntry
        {
            get => selectedPresetLoadEntry;
            set
            {
                if (ReferenceEquals(selectedPresetLoadEntry, value)) return;
                selectedPresetLoadEntry = value;
                base.OnPropertyChanged(nameof(System_SelectedPresetLoadEntry));
                base.OnPropertyChanged(nameof(System_CanDeleteSelectedPreset));
                base.OnPropertyChanged(nameof(System_PresetDeleteVisibility));
            }
        }

        public string System_PresetLoadConfirmText =>
            ResolveSettingsUiTextSafe("Common.PresetLoadConfirm", "Load");

        public string System_PresetLoadCancelText =>
            ResolveSettingsUiTextSafe("Common.PresetLoadCancel", "Cancel");

        public string System_PresetDeleteText =>
            ResolveSettingsUiTextSafe("Common.PresetDelete", "Delete");

        public bool System_CanDeleteSelectedPreset =>
            selectedPresetLoadEntry?.CanDelete == true;

        public Visibility System_PresetDeleteVisibility =>
            System_CanDeleteSelectedPreset ? Visibility.Visible : Visibility.Collapsed;

        public ObservableCollection<ModSettingsPresetSaveTarget> System_PresetSaveTargets =>
            presetSaveTargets;

        public ModSettingsPresetSaveTarget System_SelectedPresetSaveTarget
        {
            get => selectedPresetSaveTarget;
            set
            {
                if (ReferenceEquals(selectedPresetSaveTarget, value)) return;
                selectedPresetSaveTarget = value;
                if (value?.Preset != null)
                {
                    presetSaveName = value.Preset.Name;
                    presetSaveDescription = value.Preset.Description;
                    ApplyPresetToSaveRows(value.Preset);
                }
                else if (value != null)
                {
                    ResetPresetSaveForm();
                }
                base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
                base.OnPropertyChanged(nameof(System_PresetSaveName));
                base.OnPropertyChanged(nameof(System_PresetSaveDescription));
                base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
                base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            }
        }

        public string System_PresetSaveTargetText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveTarget", "Save as");

        public Visibility System_PresetSavePanelVisibility =>
            presetSavePanelOpen ? Visibility.Visible : Visibility.Collapsed;

        public string System_PresetSaveName
        {
            get => presetSaveName;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(presetSaveName, normalized, StringComparison.Ordinal)) return;
                presetSaveName = normalized;
                base.OnPropertyChanged(nameof(System_PresetSaveName));
                base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
                base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            }
        }

        public string System_PresetSaveDescription
        {
            get => presetSaveDescription;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(presetSaveDescription, normalized, StringComparison.Ordinal)) return;
                presetSaveDescription = normalized;
                base.OnPropertyChanged(nameof(System_PresetSaveDescription));
            }
        }

        public ObservableCollection<PresetSaveSettingViewModel> System_PresetSaveSettings =>
            presetSaveSettings;

        public string System_PresetSaveNameText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveName", "Preset name");

        public string System_PresetSaveDescriptionText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveDescription", "Description (optional)");

        public string System_PresetSaveBulkModeText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveBulkMode", "Set all modes");

        public string System_PresetSaveBulkModeHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.PresetSaveBulkModeHelp",
                "Default: use the mod default. Player: keep the player's current value. Fixed: apply the saved value. Host Fixed: fix host settings and keep player/local values.");

        public string System_PresetLoadSelectionHelpText =>
            ResolveSettingsUiTextSafe(
                "Common.PresetLoadSelectionHelp",
                "Selecting a preset changes nothing until you choose Load.");

        public ComboBoxItem[] System_PresetSaveBulkModeOptions => new[]
        {
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed") },
            new ComboBoxItem { Content = ResolveSettingsUiTextSafe("Common.PresetModeHostFixed", "Host Fixed") },
            new ComboBoxItem
            {
                Content = ResolveSettingsUiTextSafe("Common.PresetModeMixed", "Mixed"),
                IsEnabled = false,
            },
        };

        public int System_PresetSaveBulkModeIndex
        {
            get
            {
                if (presetSaveSettings.Count == 0)
                    return (int)PresetSaveBulkMode.HostFixed;
                int[] modes = presetSaveSettings.Select(item => item.SelectedModeIndex).Distinct().ToArray();
                if (modes.Length == 1)
                    return modes[0];
                if (presetSaveSettings.All(item =>
                    item.SelectedModeIndex == (int)(item.Scope == PresetSettingScope.Host
                        ? PublishedPresetValueMode.Fixed
                        : PublishedPresetValueMode.Player)))
                {
                    return (int)PresetSaveBulkMode.HostFixed;
                }
                return (int)PresetSaveBulkMode.Mixed;
            }
            set
            {
                if (value < 0 || value > (int)PresetSaveBulkMode.HostFixed)
                    return;
                applyingPresetSaveBulkMode = true;
                try
                {
                    foreach (PresetSaveSettingViewModel setting in presetSaveSettings)
                    {
                        setting.SelectedModeIndex = value == (int)PresetSaveBulkMode.HostFixed
                            ? (int)(setting.Scope == PresetSettingScope.Host
                                ? PublishedPresetValueMode.Fixed
                                : PublishedPresetValueMode.Player)
                            : value;
                    }
                }
                finally
                {
                    applyingPresetSaveBulkMode = false;
                }
                base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            }
        }

        public string System_PresetSaveConfirmText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveConfirm", "Save");

        public bool System_CanConfirmPresetSave =>
            !string.IsNullOrWhiteSpace(presetSaveName);

        public string System_PresetSaveConfirmHelpText => System_CanConfirmPresetSave
            ? System_PresetSaveConfirmText
            : ResolveSettingsUiTextSafe("Common.PresetSaveNameRequired", "Enter a preset name before saving.");

        public string System_PresetSaveCancelText =>
            ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");

        public RelayCommand System_OpenPresetLoadCommand { get; }

        public RelayCommand System_ConfirmPresetLoadCommand { get; }

        public RelayCommand System_DeletePresetCommand { get; }

        public RelayCommand System_CancelPresetLoadCommand { get; }

        public RelayCommand System_OpenPresetSaveCommand { get; }

        public RelayCommand System_ConfirmPresetSaveCommand { get; }

        public RelayCommand System_CancelPresetSaveCommand { get; }

        public RelayCommand System_LoadSettingsSourceCommand { get; }

        public RelayCommand System_ConfirmPresetInlineActionCommand { get; }

        public RelayCommand System_CancelPresetInlineActionCommand { get; }

        public RelayCommand System_DismissPresetStatusCommand { get; }

        public string System_PresetInlineConfirmationTitle => presetInlineConfirmationTitle;
        public string System_PresetInlineConfirmationMessage => presetInlineConfirmationMessage;
        public Visibility System_PresetInlineConfirmationVisibility =>
            pendingDeletePreset != null || pendingOverwriteId != null ? Visibility.Visible : Visibility.Collapsed;
        public string System_PresetInlineConfirmText => ResolveSettingsUiTextSafe("Common.PresetConfirm", "Confirm");
        public string System_PresetInlineCancelText => ResolveSettingsUiTextSafe("Common.PresetSaveCancel", "Cancel");
        public string System_PresetOperationStatusText => presetOperationStatus;
        public Visibility System_PresetOperationStatusVisibility =>
            presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public Visibility System_PresetOperationErrorVisibility =>
            presetOperationFailed && !string.IsNullOrWhiteSpace(presetOperationStatus) ? Visibility.Visible : Visibility.Collapsed;
        public Visibility System_PresetOperationSuccessVisibility =>
            Visibility.Collapsed;
        public string System_PresetStatusDismissText => ResolveSettingsUiTextSafe("Common.PresetStatusDismiss", "Close");

        public string System_ModSettingsSearchText
        {
            get => modSettingsSearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(modSettingsSearchText, normalized, StringComparison.Ordinal) &&
                    modSettingsSearchExactKey.Length == 0)
                {
                    return;
                }
                modSettingsSearchText = normalized;
                modSettingsSearchExactKey = string.Empty;
                RaiseModSettingsSearchProperties();
            }
        }

        public bool System_ModSettingsSearchIncludeToolTips
        {
            get => modSettingsSearchIncludeToolTips;
            set
            {
                if (modSettingsSearchIncludeToolTips == value)
                    return;
                modSettingsSearchIncludeToolTips = value;
                RaiseModSettingsSearchProperties();
            }
        }

        public string System_ModSettingsSearchExactKey => modSettingsSearchExactKey;

        public int System_ModSettingsSearchFocusRequest => modSettingsSearchFocusRequest;

        public bool System_ModSettingsSearchHasActiveFilter =>
            modSettingsSearchExactKey.Length > 0 ||
            !string.IsNullOrWhiteSpace(modSettingsSearchText);

        public Visibility System_ModSettingsSearchPanelVisibility =>
            modSettingsSearchExpanded ? Visibility.Visible : Visibility.Collapsed;

        public Visibility System_ModSettingsSearchInactiveVisibility =>
            System_ModSettingsSearchHasActiveFilter ? Visibility.Collapsed : Visibility.Visible;

        public Visibility System_ModSettingsSearchNoResultsVisibility =>
            System_ModSettingsSearchHasActiveFilter &&
            !ModSettingsSearch.HasMatches(
                this,
                modSettingsSearchText,
                modSettingsSearchIncludeToolTips,
                modSettingsSearchExactKey)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string System_ModSettingsSearchLabelText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchLabel", "Search");

        public string System_ModSettingsSearchHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchHelp", "Search setting titles. Optionally include tooltips.");

        public string System_ModSettingsSearchToggleHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchToggleHelp", "Show or hide the settings search.");

        public string System_ModSettingsSearchIncludeToolTipsText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTips", "Search tooltips");

        public string System_ModSettingsSearchIncludeToolTipsHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchIncludeToolTipsHelp", "Also search the explanatory tooltips of settings.");

        public string System_ModSettingsSearchClearHelpText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchClearHelp", "Clear the settings filter.");

        public string System_ModSettingsSearchNoResultsText =>
            ResolveSettingsUiTextSafe("Common.ModSettingsSearchNoResults", "No matching settings found.");

        public RelayCommand System_ToggleModSettingsSearchCommand { get; }

        public RelayCommand System_ClearModSettingsSearchCommand { get; }

        private void OpenPresetLoad()
        {
            if (presetLoadPanelOpen)
            {
                presetLoadPanelOpen = false;
                RaisePresetDialogProperties();
                return;
            }
            presetSavePanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
            presetLoadPanelOpen = true;
            RaisePresetDialogProperties();
        }

        private void ConfirmPresetLoad()
        {
            if (selectedPresetLoadEntry?.Preset == null)
                return;

            try
            {
                ApplyConfirmedPresetSelection(selectedPresetLoadEntry.Preset);
                presetLoadPanelOpen = false;
                RaisePresetDialogProperties();
                RaiseAccessProperties();
                DismissPresetStatus();
            }
            catch (Exception exception)
            {
                SetPresetStatus(
                    ResolveSettingsUiTextSafe("Common.PresetLoadFailedTitle", "Preset load failed") + ": " + exception.Message,
                    true);
            }
        }

        private void CancelPresetLoad()
        {
            presetLoadPanelOpen = false;
            RaisePresetDialogProperties();
        }

        private void DeleteSelectedPreset()
        {
            PublishedModSettingsPreset preset = selectedPresetLoadEntry?.Preset;
            if (preset == null || !System_CanDeleteSelectedPreset)
                return;

            string message = ResolveSettingsUiTextSafe(
                "Common.PresetDeleteConfirm",
                "The personal preset will be permanently deleted. Continue?") +
                Environment.NewLine + Environment.NewLine + preset.Name;
            pendingDeletePreset = preset;
            pendingOverwriteId = null;
            pendingOverwriteSelections = null;
            presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetDeleteTitle", "Delete personal preset");
            presetInlineConfirmationMessage = message;
            RaisePresetInlineProperties();
        }

        private void RebuildSettingsSources()
        {
            if (presetController == null) return;
            string previous = selectedSettingsSource?.Id;
            settingsSources.Clear();
            settingsSources.Add(new ModSettingsWorkingSource
            {
                Id = ModSettingsWorkingSourceRegistry.ModDefaultsId,
                Kind = ModSettingsWorkingSourceKind.ModDefault,
                DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceDefaults", "Mod defaults"),
            });
            foreach (ModSettingsWorkingSource source in ModSettingsWorkingSourceRegistry.GetProviderSources(presetController.TargetGuid))
            {
                if (source != null && !string.IsNullOrWhiteSpace(source.Id) &&
                    !string.Equals(source.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    if (source.Kind == ModSettingsWorkingSourceKind.Trail)
                        source.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceTrail", "Trail settings");
                    else if (source.Kind == ModSettingsWorkingSourceKind.Map)
                        source.DisplayName = ResolveSettingsUiTextSafe("Common.SettingsSourceMap", "Map settings");
                    settingsSources.Add(source);
                }
            }
            ModSettingsWorkingSource preferredSource = settingsSources.FirstOrDefault(item => item.IsPreferred) ??
                settingsSources.FirstOrDefault(item => string.Equals(item.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal));
            string preferred = preferredSource?.Id ?? ModSettingsWorkingSourceRegistry.ModDefaultsId;
            string preferredToken = preferred + "\n" + (preferredSource?.PreferenceContextId ?? string.Empty);
            bool preferredChanged = !string.Equals(
                preferredSettingsSourceToken,
                preferredToken,
                StringComparison.Ordinal);
            selectedSettingsSource = (preferredChanged
                    ? settingsSources.FirstOrDefault(item => string.Equals(item.Id, preferred, StringComparison.Ordinal))
                    : settingsSources.FirstOrDefault(item => string.Equals(item.Id, previous, StringComparison.Ordinal))) ??
                settingsSources.FirstOrDefault(item => string.Equals(item.Id, preferred, StringComparison.Ordinal)) ??
                settingsSources.FirstOrDefault();
            preferredSettingsSourceToken = preferredToken;
            base.OnPropertyChanged(nameof(System_SettingsSources));
            base.OnPropertyChanged(nameof(System_SelectedSettingsSource));
            base.OnPropertyChanged(nameof(System_CanLoadSettingsSource));
            base.OnPropertyChanged(nameof(System_SettingsSourceVisibility));
        }

        private void LoadSelectedSettingsSource()
        {
            ModSettingsWorkingSource source = selectedSettingsSource;
            if (source == null || !System_CanLoadSettingsSource) return;
            try
            {
                System_RefreshOwnConfiguration();
                if (string.Equals(source.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    System_LoadModDefaults();
                }
                else
                {
                    ModSettingsWorkingSourceRegistry.Apply(presetController.TargetGuid, source.Id);
                    System_CommitConfiguration();
                }
                DismissPresetStatus();
                RaiseAccessProperties();
            }
            catch (Exception exception)
            {
                SetPresetStatus(ResolveSettingsUiTextSafe("Common.SettingsSourceLoadFailed", "Could not reset settings") + ": " + exception.Message, true);
            }
        }

        private void CompletePresetDelete(PublishedModSettingsPreset preset)
        {
            try
            {
                presetController?.DeletePersonalPreset(preset);
                RebuildPresetDialogCatalogs();
                selectedPresetLoadEntry = presetLoadEntries.FirstOrDefault();
                RaisePresetDialogProperties();
                RaiseAccessProperties();
                DismissPresetStatus();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure("delete", preset?.Id, exception);
                SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetDeleteFailedTitle", "Preset deletion failed") + ": " + exception.Message, true);
            }
        }

        private void OpenPresetSave()
        {
            if (presetSavePanelOpen)
            {
                presetSavePanelOpen = false;
                RaisePresetSaveProperties();
                return;
            }
            presetLoadPanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            selectedPresetSaveTarget = presetSaveTargets.FirstOrDefault();
            ExecutePresetAction();
            base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
        }

        private void RebuildPresetDialogCatalogs()
        {
            presetLoadEntries.Clear();
            presetSaveTargets.Clear();
            presetSaveTargets.Add(new ModSettingsPresetSaveTarget
            {
                DisplayText = ResolveSettingsUiTextSafe("Common.PresetSaveNew", "New personal preset"),
            });

            foreach (PublishedModSettingsPreset preset in presetController?.PublishedPresets ??
                Array.Empty<PublishedModSettingsPreset>())
            {
                string sourceLabel;
                switch (preset.SourceKind)
                {
                    case ModSettingsPresetSourceKind.Personal:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourcePersonal", "Personal presets");
                        break;
                    case ModSettingsPresetSourceKind.Bundled:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourceBundled", "Bundled with this mod");
                        break;
                    default:
                        sourceLabel = ResolveSettingsUiTextSafe("Common.PresetSourceExternal", "External presets") +
                            ": " + preset.ProviderName;
                        break;
                }
                presetLoadEntries.Add(new ModSettingsPresetListEntry
                {
                    Preset = preset,
                    SourceLabel = sourceLabel,
                });
                if (preset.CanOverwrite)
                {
                    presetSaveTargets.Add(new ModSettingsPresetSaveTarget
                    {
                        Preset = preset,
                        DisplayText = preset.Name,
                    });
                }
            }
        }

        private void ApplyPresetToSaveRows(PublishedModSettingsPreset preset)
        {
            if (preset == null)
                return;
            foreach (PresetSaveSettingViewModel row in presetSaveSettings)
            {
                if (preset.Settings.TryGetValue(row.PropertyName, out PublishedPresetSetting setting))
                {
                    row.SelectedModeIndex = (int)setting.Mode;
                }
                else
                {
                    row.SelectedModeIndex = (int)PublishedPresetValueMode.Player;
                }
            }
        }

        private void RaisePresetDialogProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetLoadPanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetSavePanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetLoadEntries));
            base.OnPropertyChanged(nameof(System_SelectedPresetLoadEntry));
            base.OnPropertyChanged(nameof(System_CanDeleteSelectedPreset));
            base.OnPropertyChanged(nameof(System_PresetDeleteVisibility));
            base.OnPropertyChanged(nameof(System_PresetSaveTargets));
            base.OnPropertyChanged(nameof(System_SelectedPresetSaveTarget));
            base.OnPropertyChanged(nameof(System_PresetStatusText));
            base.OnPropertyChanged(nameof(System_PresetStatusVisibility));
        }

        private void ExecutePresetAction()
        {
            presetSaveSettings.Clear();
            string[] modeOptions =
            {
                ResolveSettingsUiTextSafe("Common.PresetModeDefault", "Default"),
                ResolveSettingsUiTextSafe("Common.PresetModePlayer", "Player"),
                ResolveSettingsUiTextSafe("Common.PresetModeFixed", "Fixed"),
            };
            foreach (PresetSettingDescriptor descriptor in System_GetPresetSettingDescriptors())
            {
                var setting = new PresetSaveSettingViewModel(
                    descriptor,
                    ResolvePresetSettingScopeText(descriptor.Scope),
                    modeOptions);
                setting.RestartHelp = ResolveSettingsUiTextSafe("Common.RestartRequiredOption", "Restart required");
                setting.PropertyChanged += OnPresetSaveSettingPropertyChanged;
                presetSaveSettings.Add(setting);
            }
            ResetPresetSaveForm();
            presetSavePanelOpen = true;
            RaisePresetSaveProperties();
        }

        private void ResetPresetSaveForm()
        {
            foreach (PresetSaveSettingViewModel setting in presetSaveSettings)
            {
                setting.SelectedModeIndex = (int)(setting.Scope == PresetSettingScope.Host
                    ? PublishedPresetValueMode.Fixed
                    : PublishedPresetValueMode.Player);
            }
            presetSaveName = string.Empty;
            presetSaveDescription = string.Empty;
            base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
        }

        private void OnPresetSaveSettingPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (!applyingPresetSaveBulkMode &&
                string.Equals(args?.PropertyName, nameof(PresetSaveSettingViewModel.SelectedModeIndex), StringComparison.Ordinal))
            {
                base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            }
        }

        private string ResolvePresetSettingScopeText(PresetSettingScope scope)
        {
            switch (scope)
            {
                case PresetSettingScope.Host:
                    return ResolveSettingsUiTextSafe("Common.PresetScopeHost", "Host");
                case PresetSettingScope.Player:
                    return ResolveSettingsUiTextSafe("Common.PresetScopePlayer", "Player");
                case PresetSettingScope.Local:
                    return ResolveSettingsUiTextSafe("Common.PresetScopeLocal", "Local");
                default:
                    return scope.ToString();
            }
        }

        private void ConfirmPresetSave()
        {
            try
            {
                PresetSaveSelection[] selections = CreatePresetSaveSelections();
                PublishedModSettingsPreset existing = selectedPresetSaveTarget?.Preset;
                if (existing != null)
                {
                    pendingDeletePreset = null;
                    pendingOverwriteId = existing.Id;
                    pendingOverwriteSelections = selections;
                    presetInlineConfirmationTitle = ResolveSettingsUiTextSafe("Common.PresetSaveOverwriteTitle", "Overwrite personal preset");
                    presetInlineConfirmationMessage = ResolveSettingsUiTextSafe("Common.PresetSaveOverwrite", "The selected personal preset will be completely replaced. Continue?");
                    RaisePresetInlineProperties();
                    return;
                }

                string id = presetController?.CreateUniquePersonalPresetId(presetSaveName) ??
                    CreatePublishedPresetId(presetSaveName);
                System_SavePersonalPreset(
                    id,
                    presetSaveName,
                    presetSaveDescription,
                    selections,
                    overwrite: false);
                ShowPresetSaveCompleted();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure("save", selectedPresetSaveTarget?.Preset?.Id, exception);
                ShowPresetSaveError(exception);
            }
        }

        private PresetSaveSelection[] CreatePresetSaveSelections() =>
            presetSaveSettings.Select(item => item.ToSelection()).ToArray();

        private void CompletePresetSave(string id, PresetSaveSelection[] selections, bool overwrite)
        {
            try
            {
                System_SavePersonalPreset(
                    id,
                    presetSaveName,
                    presetSaveDescription,
                    selections,
                    overwrite);
                ShowPresetSaveCompleted();
            }
            catch (Exception exception)
            {
                presetController?.LogPresetOperationFailure(overwrite ? "overwrite" : "save", id, exception);
                ShowPresetSaveError(exception);
            }
        }

        private void ShowPresetSaveCompleted()
        {
            presetSavePanelOpen = false;
            presetController?.RefreshCatalog();
            RebuildPresetDialogCatalogs();
            RaisePresetSaveProperties();
            DismissPresetStatus();
        }

        private void ShowPresetSaveError(Exception exception)
        {
            SetPresetStatus(ResolveSettingsUiTextSafe("Common.PresetSaveFailedTitle", "Preset save failed") + ": " + exception.Message, true);
        }

        private void ConfirmPresetInlineAction()
        {
            PublishedModSettingsPreset delete = pendingDeletePreset;
            string overwriteId = pendingOverwriteId;
            PresetSaveSelection[] selections = pendingOverwriteSelections;
            ClearPresetInlineConfirmation();
            if (delete != null) CompletePresetDelete(delete);
            else if (overwriteId != null) CompletePresetSave(overwriteId, selections ?? Array.Empty<PresetSaveSelection>(), true);
        }

        private void CancelPresetInlineAction()
        {
            if (pendingDeletePreset != null) presetController?.LogPresetOperationCancelled("delete", pendingDeletePreset.Id);
            else if (pendingOverwriteId != null) presetController?.LogPresetOperationCancelled("overwrite", pendingOverwriteId);
            ClearPresetInlineConfirmation();
        }

        private void ClearPresetInlineConfirmation()
        {
            pendingDeletePreset = null;
            pendingOverwriteId = null;
            pendingOverwriteSelections = null;
            presetInlineConfirmationTitle = string.Empty;
            presetInlineConfirmationMessage = string.Empty;
            RaisePresetInlineProperties();
        }

        private void SetPresetStatus(string message, bool failed)
        {
            presetOperationStatus = message ?? string.Empty;
            presetOperationFailed = failed;
            RaisePresetInlineProperties();
        }

        private void DismissPresetStatus() => SetPresetStatus(string.Empty, false);

        private void RaisePresetInlineProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationTitle));
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationMessage));
            base.OnPropertyChanged(nameof(System_PresetInlineConfirmationVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationStatusText));
            base.OnPropertyChanged(nameof(System_PresetOperationStatusVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationErrorVisibility));
            base.OnPropertyChanged(nameof(System_PresetOperationSuccessVisibility));
        }

        private void CancelPresetSave()
        {
            presetSavePanelOpen = false;
            RaisePresetSaveProperties();
        }

        private void RaisePresetSaveProperties()
        {
            base.OnPropertyChanged(nameof(System_PresetSavePanelVisibility));
            base.OnPropertyChanged(nameof(System_PresetSaveName));
            base.OnPropertyChanged(nameof(System_PresetSaveDescription));
            base.OnPropertyChanged(nameof(System_PresetSaveSettings));
            base.OnPropertyChanged(nameof(System_PresetSaveBulkModeIndex));
            base.OnPropertyChanged(nameof(System_CanConfirmPresetSave));
            base.OnPropertyChanged(nameof(System_PresetSaveConfirmHelpText));
            RaisePresetDialogProperties();
        }

        private static string CreatePublishedPresetId(string name)
        {
            string trimmed = (name ?? string.Empty).Trim();
            if (trimmed.Length == 0)
                throw new InvalidDataException("A preset name is required.");
            var result = new System.Text.StringBuilder(trimmed.Length);
            bool separator = false;
            foreach (char character in trimmed.ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_')
                {
                    result.Append(character);
                    separator = false;
                }
                else if (!separator)
                {
                    result.Append('-');
                    separator = true;
                }
            }
            string id = result.ToString().Trim('-');
            if (id.Length == 0)
                throw new InvalidDataException("The preset name does not contain a usable id.");
            return id.Length <= 128 ? id : id.Substring(0, 128).TrimEnd('-');
        }

        /// <summary>Safe reflection bridge used by the optional global search host.</summary>
        public bool System_ApplyModSettingsSearchTarget(string key, string title)
        {
            string normalizedKey = ModSettingsSearchMatcher.Normalize(key);
            if (normalizedKey.Length == 0)
                return false;

            modSettingsSearchText = title ?? string.Empty;
            modSettingsSearchExactKey = normalizedKey;
            modSettingsSearchExpanded = true;
            RaiseModSettingsSearchProperties();
            return true;
        }

        private void ToggleModSettingsSearch()
        {
            modSettingsSearchExpanded = !modSettingsSearchExpanded;
            RaiseModSettingsSearchProperties();
            if (!modSettingsSearchExpanded)
                return;

            unchecked
            {
                modSettingsSearchFocusRequest++;
                if (modSettingsSearchFocusRequest <= 0)
                    modSettingsSearchFocusRequest = 1;
            }
            base.OnPropertyChanged(nameof(System_ModSettingsSearchFocusRequest));
        }

        private void ClearModSettingsSearch()
        {
            modSettingsSearchText = string.Empty;
            modSettingsSearchExactKey = string.Empty;
            RaiseModSettingsSearchProperties();
        }

        private void RaiseModSettingsSearchProperties()
        {
            base.OnPropertyChanged(nameof(System_ModSettingsSearchText));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchIncludeToolTips));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchExactKey));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchHasActiveFilter));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchPanelVisibility));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchInactiveVisibility));
            base.OnPropertyChanged(nameof(System_ModSettingsSearchNoResultsVisibility));
        }
#endif
    }
}
