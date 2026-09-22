using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;

namespace SerpsModsHost
{
    public sealed class SerpsModsDiagnosticsViewModel : Shared.PresetLobbyModSettingsViewModel
    {
        private readonly object sync = new object();
        private readonly List<string> errors = new List<string>();
        private Action refreshAction;
        private string packVersion = "unknown";
        private int expectedCount;
        private int validatedCount;
        private int registeredCount;
        private string scriptExtenderCompatibilityWarning = string.Empty;
        private ModSettingsSearchViewModel search;
        private string[] presetTargetGuids = Array.Empty<string>();
        private readonly ObservableCollection<ModSettingsWorkingSource> globalSources = new ObservableCollection<ModSettingsWorkingSource>();
        private ModSettingsWorkingSource selectedGlobalSource;
        private ModSettingsWorkingSource pendingGlobalSource;
        private bool globalResetConfirmationVisible;
        private string globalResetStatus = string.Empty;
        private bool globalResetStatusIsError;

        public SerpsModsDiagnosticsViewModel()
        {
            RefreshCommand = new RelayCommand(() => refreshAction?.Invoke());
            ClearErrorsCommand = new RelayCommand(ClearErrors);
            LoadGlobalSettingsSourceCommand = new RelayCommand(RequestGlobalSettingsReset);
            ConfirmGlobalSettingsResetCommand = new RelayCommand(ConfirmGlobalSettingsReset);
            CancelGlobalSettingsResetCommand = new RelayCommand(CancelGlobalSettingsReset);
            DismissGlobalSettingsResetStatusCommand = new RelayCommand(DismissGlobalSettingsResetStatus);
            ModSettingsWorkingSourceRegistry.SourcesChanged += RefreshGlobalSources;
            PropertyChanged += OnDiagnosticsPropertyChanged;
            RefreshGlobalSources();
        }

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearErrorsCommand { get; }
        public RelayCommand LoadGlobalSettingsSourceCommand { get; }
        public RelayCommand ConfirmGlobalSettingsResetCommand { get; }
        public RelayCommand CancelGlobalSettingsResetCommand { get; }
        public RelayCommand DismissGlobalSettingsResetStatusCommand { get; }
        public ObservableCollection<ModSettingsWorkingSource> GlobalSettingsSources => globalSources;
        public ModSettingsWorkingSource SelectedGlobalSettingsSource
        {
            get => selectedGlobalSource;
            set
            {
                if (ReferenceEquals(selectedGlobalSource, value))
                    return;
                selectedGlobalSource = value;
                CancelGlobalSettingsReset();
                OnPropertyChanged(nameof(SelectedGlobalSettingsSource));
                RaiseGlobalSettingsResetProperties();
            }
        }
        public bool CanLoadGlobalSettingsSource
        {
            get
            {
                IModSettingsWorkingCopyEndpoint[] endpoints = GetTargetEndpoints();
                return GlobalSettingsResetPolicy.CanReset(
                    globalResetConfirmationVisible,
                    IsLocalSettingsHost,
                    selectedGlobalSource != null,
                    endpoints.Length,
                    endpoints.Any(IsReadOnlyMissionEndpoint));
            }
        }
        public bool CanSelectGlobalSettingsSource
        {
            get
            {
                IModSettingsWorkingCopyEndpoint[] endpoints = GetTargetEndpoints();
                return GlobalSettingsResetPolicy.CanSelect(
                    globalResetConfirmationVisible,
                    IsLocalSettingsHost,
                    endpoints.Length,
                    endpoints.Any(IsReadOnlyMissionEndpoint));
            }
        }
        public string GlobalSettingsSourceText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsTo);
        public string LoadGlobalSettingsSourceText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettings);
        public string GlobalSettingsSourceHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsHelp);
        public string GlobalSettingsResetConfirmationTitle => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsConfirmTitle);
        public string GlobalSettingsResetConfirmationText => SerpLocalization.Get(
            SerpLocalization.SerpsModsResetSettingsConfirm,
            "Source", pendingGlobalSource?.DisplayName ?? selectedGlobalSource?.DisplayName ?? string.Empty);
        public string GlobalSettingsResetConfirmText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsConfirmButton);
        public string GlobalSettingsResetCancelText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsCancelButton);
        public Visibility GlobalSettingsResetConfirmationVisibility =>
            globalResetConfirmationVisible ? Visibility.Visible : Visibility.Collapsed;
        public string GlobalSettingsResetStatusText => globalResetStatus;
        public Visibility GlobalSettingsResetStatusVisibility =>
            string.IsNullOrWhiteSpace(globalResetStatus) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility GlobalSettingsResetErrorVisibility =>
            globalResetStatusIsError && !string.IsNullOrWhiteSpace(globalResetStatus)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public Visibility GlobalSettingsResetSuccessVisibility =>
            !globalResetStatusIsError && !string.IsNullOrWhiteSpace(globalResetStatus)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public string GlobalSettingsResetDismissText => SerpLocalization.Get(SerpLocalization.SerpsModsResetSettingsDismiss);
        public ModSettingsSearchViewModel Search => search;
        public string TitleText => SerpLocalization.Get(SerpLocalization.SerpsModsStatusTitle);
        public string GameModeNoticeText => SerpLocalization.Get(SerpLocalization.SerpsModsGameModeNotice);
        public string SummaryTitleText => SerpLocalization.Get(SerpLocalization.SerpsModsSummaryTitle);
        public string ErrorsTitleText => SerpLocalization.Get(SerpLocalization.SerpsModsErrorsTitle);
        public string RefreshText => SerpLocalization.Get(SerpLocalization.SerpsModsRefresh);
        public string RefreshHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsRefreshHelp);
        public string ClearErrorsText => SerpLocalization.Get(SerpLocalization.SerpsModsClearErrors);
        public string ClearErrorsHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsClearErrorsHelp);
        public string NoErrorsText => SerpLocalization.Get(SerpLocalization.SerpsModsNoErrors);
        public string ScriptExtenderCompatibilityWarning => scriptExtenderCompatibilityWarning;
        public Visibility ScriptExtenderCompatibilityWarningVisibility =>
            string.IsNullOrWhiteSpace(scriptExtenderCompatibilityWarning)
                ? Visibility.Collapsed
                : Visibility.Visible;
        public string SummaryText => SerpLocalization.Get(
            SerpLocalization.SerpsModsSummaryFormat,
            "Version", packVersion,
            "Expected", expectedCount,
            "Validated", validatedCount,
            "Registered", registeredCount);
        public string ErrorsText
        {
            get
            {
                lock (sync)
                    return errors.Count == 0 ? NoErrorsText : string.Join(Environment.NewLine, errors.ToArray());
            }
        }
        public Visibility ErrorVisibility
        {
            get
            {
                lock (sync)
                    return errors.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public void SetRefreshAction(Action action) => refreshAction = action;

        public void SetPresetTargetGuids(IEnumerable<string> guids)
        {
            presetTargetGuids = (guids ?? Enumerable.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).ToArray();
            RaiseGlobalSettingsResetProperties();
        }

        private void RefreshGlobalSources()
        {
            string selectedId = selectedGlobalSource?.Id;
            globalSources.Clear();
            globalSources.Add(new ModSettingsWorkingSource { Id = ModSettingsWorkingSourceRegistry.ModDefaultsId, Kind = ModSettingsWorkingSourceKind.ModDefault, DisplayName = SerpLocalization.Get("Common.SettingsSourceDefaults") });
            foreach (ModSettingsWorkingSource source in ModSettingsWorkingSourceRegistry.GetProviderSources(string.Empty))
            {
                if (source.Kind == ModSettingsWorkingSourceKind.Trail) source.DisplayName = SerpLocalization.Get("Common.SettingsSourceTrail");
                if (source.Kind == ModSettingsWorkingSourceKind.Map) source.DisplayName = SerpLocalization.Get("Common.SettingsSourceMap");
                globalSources.Add(source);
            }
            selectedGlobalSource = globalSources.FirstOrDefault(item => string.Equals(item.Id, selectedId, StringComparison.Ordinal)) ?? globalSources.FirstOrDefault();
            OnPropertyChanged(nameof(GlobalSettingsSources));
            OnPropertyChanged(nameof(SelectedGlobalSettingsSource));
            CancelGlobalSettingsReset();
            RaiseGlobalSettingsResetProperties();
        }

        private void RequestGlobalSettingsReset()
        {
            if (!CanLoadGlobalSettingsSource)
                return;
            pendingGlobalSource = selectedGlobalSource;
            globalResetConfirmationVisible = true;
            DismissGlobalSettingsResetStatus();
            RaiseGlobalSettingsResetProperties();
        }

        private void ConfirmGlobalSettingsReset()
        {
            ModSettingsWorkingSource source = pendingGlobalSource;
            if (!globalResetConfirmationVisible || source == null)
                return;

            pendingGlobalSource = null;
            globalResetConfirmationVisible = false;
            try
            {
                IModSettingsWorkingCopyEndpoint[] endpoints = GetTargetEndpoints();
                if (endpoints.Length == 0 || !IsLocalSettingsHost || endpoints.Any(IsReadOnlyMissionEndpoint))
                    throw new InvalidOperationException("The shared ModSettings reset is not available in the current context.");

                bool missionContext = endpoints.Any(endpoint => endpoint.IsMissionPresetActive);
                if (missionContext || !string.Equals(source.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    ModSettingsWorkingSourceRegistry.ApplyMany(presetTargetGuids, source.Id);
                }
                else
                {
                    GlobalSettingsResetPolicy.ApplyAtomically(
                        endpoints,
                        endpoint => endpoint.System_CreateCurrentWorkingSnapshot(),
                        endpoint => endpoint.System_LoadModDefaults(),
                        (endpoint, snapshot) => endpoint.System_ApplyWorkingSnapshot(snapshot));
                }

                globalResetStatusIsError = false;
                globalResetStatus = SerpLocalization.Get(
                    SerpLocalization.SerpsModsResetSettingsCompleted,
                    "Source", source.DisplayName ?? string.Empty);
            }
            catch (Exception exception)
            {
                globalResetStatusIsError = true;
                globalResetStatus = SerpLocalization.Get(
                    SerpLocalization.SerpsModsResetSettingsFailed,
                    "Reason", exception.Message);
                RecordError("Could not reset the shared ModSettings source: " + exception.Message);
            }
            RaiseGlobalSettingsResetProperties();
        }

        private void CancelGlobalSettingsReset()
        {
            if (!globalResetConfirmationVisible && pendingGlobalSource == null)
                return;
            pendingGlobalSource = null;
            globalResetConfirmationVisible = false;
            RaiseGlobalSettingsResetProperties();
        }

        private void DismissGlobalSettingsResetStatus()
        {
            if (globalResetStatus.Length == 0)
                return;
            globalResetStatus = string.Empty;
            globalResetStatusIsError = false;
            RaiseGlobalSettingsResetProperties();
        }

        private IModSettingsWorkingCopyEndpoint[] GetTargetEndpoints() =>
            GameXAMLManagerAPI.Instance.RegisteredModSettings
                .Where(entry => entry?.Plugin?.Info?.Metadata != null &&
                    presetTargetGuids.Contains(entry.Plugin.Info.Metadata.GUID, StringComparer.Ordinal))
                .Select(entry => entry.ViewModel as IModSettingsWorkingCopyEndpoint)
                .Where(endpoint => endpoint != null)
                .Distinct()
                .ToArray();

        private static bool IsReadOnlyMissionEndpoint(IModSettingsWorkingCopyEndpoint endpoint) =>
            endpoint.IsMissionPresetActive &&
            endpoint is PresetLobbyModSettingsViewModel settings &&
            !settings.MissionPresetEditable;

        private void OnDiagnosticsPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args == null ||
                string.Equals(args.PropertyName, nameof(IsLocalSettingsHost), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(IsMissionPresetActive), StringComparison.Ordinal) ||
                string.Equals(args.PropertyName, nameof(MissionPresetEditable), StringComparison.Ordinal))
            {
                RaiseGlobalSettingsResetProperties();
            }
        }

        private void RaiseGlobalSettingsResetProperties()
        {
            OnPropertyChanged(nameof(CanLoadGlobalSettingsSource));
            OnPropertyChanged(nameof(CanSelectGlobalSettingsSource));
            OnPropertyChanged(nameof(GlobalSettingsResetConfirmationText));
            OnPropertyChanged(nameof(GlobalSettingsResetConfirmationVisibility));
            OnPropertyChanged(nameof(GlobalSettingsResetStatusText));
            OnPropertyChanged(nameof(GlobalSettingsResetStatusVisibility));
            OnPropertyChanged(nameof(GlobalSettingsResetErrorVisibility));
            OnPropertyChanged(nameof(GlobalSettingsResetSuccessVisibility));
        }

        public void SetSearch(ModSettingsSearchViewModel value)
        {
            search = value;
            OnPropertyChanged(nameof(Search));
        }

        public void SetStatus(string version, int expected, int validated, int registered)
        {
            packVersion = string.IsNullOrWhiteSpace(version) ? "unknown" : version;
            expectedCount = expected;
            validatedCount = validated;
            registeredCount = registered;
            OnPropertyChanged(nameof(SummaryText));
        }

        public void SetScriptExtenderCompatibilityWarning(string warning)
        {
            scriptExtenderCompatibilityWarning = warning ?? string.Empty;
            OnPropertyChanged(nameof(ScriptExtenderCompatibilityWarning));
            OnPropertyChanged(nameof(ScriptExtenderCompatibilityWarningVisibility));
        }

        public void RecordError(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text.Trim()}";
            lock (sync)
            {
                if (errors.Count > 199)
                    errors.RemoveAt(0);
                errors.Add(line);
            }
            OnPropertyChanged(nameof(ErrorsText));
            OnPropertyChanged(nameof(ErrorVisibility));
        }

        private void ClearErrors()
        {
            lock (sync)
                errors.Clear();
            OnPropertyChanged(nameof(ErrorsText));
            OnPropertyChanged(nameof(ErrorVisibility));
        }
    }
}
