using Noesis;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.ObjectModel;
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

        public SerpsModsDiagnosticsViewModel()
        {
            RefreshCommand = new RelayCommand(() => refreshAction?.Invoke());
            ClearErrorsCommand = new RelayCommand(ClearErrors);
            LoadGlobalSettingsSourceCommand = new RelayCommand(LoadGlobalSettingsSource);
            ModSettingsWorkingSourceRegistry.SourcesChanged += RefreshGlobalSources;
            RefreshGlobalSources();
        }

        protected override string ResolveSettingsUiText(string key, string fallback) =>
            SerpLocalization.Get(key);

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearErrorsCommand { get; }
        public RelayCommand LoadGlobalSettingsSourceCommand { get; }
        public ObservableCollection<ModSettingsWorkingSource> GlobalSettingsSources => globalSources;
        public ModSettingsWorkingSource SelectedGlobalSettingsSource
        {
            get => selectedGlobalSource;
            set { selectedGlobalSource = value; OnPropertyChanged(nameof(SelectedGlobalSettingsSource)); OnPropertyChanged(nameof(CanLoadGlobalSettingsSource)); }
        }
        public bool CanLoadGlobalSettingsSource => IsLocalSettingsHost && selectedGlobalSource != null && presetTargetGuids.Length != 0;
        public string GlobalSettingsSourceText => SerpLocalization.Get("Common.SettingsSource");
        public string LoadGlobalSettingsSourceText => SerpLocalization.Get("Common.SettingsSourceLoad");
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
            OnPropertyChanged(nameof(CanLoadGlobalSettingsSource));
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
            OnPropertyChanged(nameof(CanLoadGlobalSettingsSource));
        }

        private void LoadGlobalSettingsSource()
        {
            if (!CanLoadGlobalSettingsSource) return;
            try
            {
                if (!string.Equals(selectedGlobalSource.Id, ModSettingsWorkingSourceRegistry.ModDefaultsId, StringComparison.Ordinal))
                {
                    ModSettingsWorkingSourceRegistry.ApplyMany(presetTargetGuids, selectedGlobalSource.Id);
                    return;
                }
                var endpoints = GameXAMLManagerAPI.Instance.RegisteredModSettings
                    .Where(entry => entry?.Plugin?.Info?.Metadata != null && presetTargetGuids.Contains(entry.Plugin.Info.Metadata.GUID, StringComparer.Ordinal))
                    .Select(entry => entry.ViewModel as IModSettingsWorkingCopyEndpoint)
                    .Where(endpoint => endpoint != null)
                    .Distinct()
                    .ToArray();
                var rollback = endpoints.ToDictionary(endpoint => endpoint, endpoint => endpoint.System_CreateCurrentWorkingSnapshot());
                try
                {
                    foreach (IModSettingsWorkingCopyEndpoint endpoint in endpoints) endpoint.System_LoadModDefaults();
                }
                catch
                {
                    foreach (KeyValuePair<IModSettingsWorkingCopyEndpoint, Dictionary<string, byte[]>> item in rollback)
                        item.Key.System_ApplyWorkingSnapshot(item.Value);
                    throw;
                }
            }
            catch (Exception exception) { RecordError("Could not load the shared ModSettings source: " + exception.Message); }
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
