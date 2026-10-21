using APIShared.ModSettings;
using BepInEx.Logging;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SerpsModsHost
{
    public sealed class ModSettingsSearchViewModel : INotifyPropertyChanged
    {
        private readonly ManualLogSource log;
        private readonly List<IndexedModSetting> index = new List<IndexedModSetting>();
        private string searchText = string.Empty;
        private bool includeToolTips;
        private ModSettingsSearchFilter selectedModFilter;
        private string selectedModName;
        private bool filtersDirty = true;
        private bool indexDirty = true;
        private bool refreshing;
        private bool refreshPending;
        private bool rebuildingModFilters;
        private int stateVersion;

        public ModSettingsSearchViewModel(ManualLogSource log)
        {
            this.log = log;
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            Results = new ObservableCollection<ModSettingsSearchResult>();
            ModFilters = new ObservableCollection<ModSettingsSearchFilter>();
            RequestRefresh();
            GameXAMLManagerAPI.Instance.RegisteredModSettings.CollectionChanged += OnModTabsChanged;
            Plugin.ModSettingsHubViewModel.PropertyChanged += OnHubPropertyChanged;
            ModSettingsSearchCatalog.SourceChanged += OnSearchSourceChanged;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ObservableCollection<ModSettingsSearchResult> Results { get; }
        public ObservableCollection<ModSettingsSearchFilter> ModFilters { get; }
        public RelayCommand ClearSearchCommand { get; }
        public string SearchLabelText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchLabel);
        public string SearchHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchHelp);
        public string ClearSearchHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchClearHelp);
        public string ModFilterHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchModFilterHelp);
        public string IncludeToolTipsText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchIncludeToolTips);
        public string IncludeToolTipsHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchIncludeToolTipsHelp);
        public string ResultsTitleText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchResultsTitle);
        public string NoResultsText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchNoResults);

        public string SearchText
        {
            get => searchText;
            set
            {
                string normalized = (value ?? string.Empty).TrimStart();
                if (string.Equals(searchText, normalized, StringComparison.Ordinal)) return;
                searchText = normalized;
                stateVersion++;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchResultsVisibility));
                RequestRefresh();
            }
        }

        public bool IncludeToolTips
        {
            get => includeToolTips;
            set
            {
                if (includeToolTips == value) return;
                includeToolTips = value;
                stateVersion++;
                OnPropertyChanged();
                RequestRefresh();
            }
        }

        public ModSettingsSearchFilter SelectedModFilter
        {
            get => selectedModFilter;
            set
            {
                // Clearing ItemsSource may synchronously write a temporary null selection.
                if ((rebuildingModFilters && value == null) || ReferenceEquals(selectedModFilter, value)) return;
                selectedModFilter = value;
                selectedModName = value?.ModName;
                stateVersion++;
                OnPropertyChanged();
                RequestRefresh();
            }
        }

        public Visibility SearchResultsVisibility =>
            string.IsNullOrWhiteSpace(searchText) ? Visibility.Collapsed : Visibility.Visible;
        public Visibility NoResultsVisibility =>
            !string.IsNullOrWhiteSpace(searchText) && Results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        public string ResultCountText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchResultCount, "Count", Results.Count);

        private void OnModTabsChanged(object sender, NotifyCollectionChangedEventArgs args) => InvalidateCatalog();

        private void OnSearchSourceChanged(object viewModel)
        {
            // Normal preset registration announces its source before publishing the tab. Do not
            // evaluate consumer getters until that tab's normal registration has completed.
            if (GameXAMLManagerAPI.Instance.RegisteredModSettings.Any(tab => ReferenceEquals(tab.ViewModel, viewModel)))
                InvalidateCatalog();
        }

        private void InvalidateCatalog()
        {
            stateVersion++;
            filtersDirty = true;
            indexDirty = true;
            RequestRefresh();
        }

        private void OnHubPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SHCDESE.ViewModels.LobbyModSettingsHubViewModel.WindowVisibility) &&
                Plugin.ModSettingsHubViewModel.WindowVisibility == Visibility.Visible)
            {
                // Do not evaluate catalogs or mutate bound collections during modal opening.
                stateVersion++;
                indexDirty = true;
                if (refreshing) refreshPending = true;
            }
        }

        private void RebuildModFilters()
        {
            filtersDirty = false;
            LobbyModSettingsEntry[] tabs = GetSupportedTabs();
            rebuildingModFilters = true;
            try
            {
                ModFilters.Clear();
                ModFilters.Add(new ModSettingsSearchFilter(SerpLocalization.Get(SerpLocalization.SerpsModsSearchAllMods), null));
                foreach (LobbyModSettingsEntry tab in tabs)
                    ModFilters.Add(new ModSettingsSearchFilter(tab.Name, tab.Name));
                selectedModFilter = ModFilters.FirstOrDefault(filter =>
                    string.Equals(filter.ModName, selectedModName, StringComparison.Ordinal)) ?? ModFilters[0];
                selectedModName = selectedModFilter.ModName;
                OnPropertyChanged(nameof(SelectedModFilter));
            }
            finally { rebuildingModFilters = false; }
        }

        private static LobbyModSettingsEntry[] GetSupportedTabs() =>
            GameXAMLManagerAPI.Instance.RegisteredModSettings
                .Where(tab => tab != null && !(tab.ViewModel is SerpsModsDiagnosticsViewModel) &&
                    ModSettingsSearchCatalog.IsRegistered(tab.ViewModel)).ToArray();

        private void RebuildIndex()
        {
            // The flag describes validity, not entry count: an empty catalog is a valid snapshot.
            indexDirty = false;
            var snapshot = new List<IndexedModSetting>();
            foreach (LobbyModSettingsEntry tab in GetSupportedTabs())
            {
                if (!ModSettingsSearchCatalog.TryGetEntries(tab.ViewModel, out IReadOnlyList<ModSettingsSearchEntry> entries)) continue;
                foreach (ModSettingsSearchEntry entry in entries)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Title)) continue;
                    snapshot.Add(new IndexedModSetting(tab, entry.Key, entry.Title, entry.ToolTip,
                        entry.SectionKey, entry.SectionTitle, entry.IsSection));
                }
            }
            index.Clear();
            index.AddRange(snapshot);
        }

        private void RequestRefresh()
        {
            refreshPending = true;
            if (refreshing) return;
            refreshing = true;
            try
            {
                int passes = 0;
                // Bindings and consumer getters can synchronously request another refresh. Finish
                // the current notification before publishing anything for the latest state.
                while (refreshPending)
                {
                    // An opted-in consumer may replace its source from a title getter. If it
                    // never settles, leave search retryable instead of locking the Unity thread.
                    if (++passes > 8)
                    {
                        refreshPending = false;
                        Shared.DebugLogHelper.LogWarningLimited(log, "ModSettingsSearch.UnstableSource",
                            "[ModSettingsSearch] Reentrant settings changes did not settle; retaining the last results until the next search action.");
                        break;
                    }
                    refreshPending = false;
                    if (filtersDirty) RebuildModFilters();
                    string query = searchText.Trim();
                    if (query.Length > 0 && indexDirty) RebuildIndex();
                    int version = stateVersion;
                    List<ModSettingsSearchResult> snapshot = BuildResults(query, selectedModName, includeToolTips);
                    if (version != stateVersion || filtersDirty || (query.Length > 0 && indexDirty))
                    {
                        refreshPending = true;
                        continue;
                    }
                    Results.Clear();
                    foreach (ModSettingsSearchResult result in snapshot)
                    {
                        if (version != stateVersion) break;
                        Results.Add(result);
                    }
                    NotifyResultPresentation();
                }
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ModSettingsSearch.Refresh",
                    $"[ModSettingsSearch] Could not refresh search results: {ex}");
            }
            finally { refreshing = false; }
        }

        private List<ModSettingsSearchResult> BuildResults(string query, string modName, bool toolTips)
        {
            if (query.Length == 0) return new List<ModSettingsSearchResult>();
            List<IndexedModSetting> candidates = index.Where(candidate => string.IsNullOrEmpty(modName) ||
                string.Equals(candidate.Tab.Name, modName, StringComparison.Ordinal)).ToList();
            var matchedSections = new HashSet<string>(candidates
                .Where(candidate => candidate.IsSection && ModSettingsSearchMatcher.IsSectionTitleMatch(query, candidate.Title))
                .Select(candidate => candidate.SectionIdentity), StringComparer.Ordinal);
            return candidates.Where(candidate =>
                    ModSettingsSearchPolicy.ShouldIncludeCandidate(candidate.IsSection, candidate.SectionIdentity,
                        candidate.SectionIdentity, matchedSections) &&
                    (candidate.IsSection || ModSettingsSearchMatcher.IsMatch(query, toolTips, string.Empty,
                        candidate.Key, candidate.Title, candidate.ToolTip, candidate.SectionKey, candidate.SectionTitle)))
                .OrderBy(candidate => ModSettingsSearchPolicy.Rank(candidate.Title, candidate.ToolTip,
                    candidate.SectionTitle, query, toolTips))
                .ThenBy(candidate => candidate.Tab.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(candidate => candidate.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(candidate => new ModSettingsSearchResult(candidate, OpenResult)).ToList();
        }

        private void NotifyResultPresentation()
        {
            OnPropertyChanged(nameof(ResultCountText));
            OnPropertyChanged(nameof(NoResultsVisibility));
        }

        private void OpenResult(IndexedModSetting result)
        {
            try
            {
                var tabs = GameXAMLManagerAPI.Instance.RegisteredModSettings;
                if (!tabs.Contains(result.Tab) ||
                    !ModSettingsSearchCatalog.TryGetEntries(result.Tab.ViewModel, out IReadOnlyList<ModSettingsSearchEntry> entries) ||
                    !entries.Any(entry => string.Equals(entry.Key, result.Key, StringComparison.Ordinal)))
                {
                    InvalidateCatalog();
                    return;
                }
                ModSettingsSearchCatalog.TryNavigate(result.Tab.ViewModel, result.Key);
                if (tabs.Contains(result.Tab)) Plugin.ModSettingsHubViewModel.SelectedTab = result.Tab;
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "ModSettingsSearch.OpenResult",
                    $"[ModSettingsSearch] Could not open search result: {ex}");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class ModSettingsSearchFilter
    {
        public ModSettingsSearchFilter(string displayName, string modName)
        {
            DisplayName = displayName;
            ModName = modName;
        }

        public string DisplayName { get; }
        public string ModName { get; }
    }

    public sealed class ModSettingsSearchResult
    {
        internal ModSettingsSearchResult(IndexedModSetting entry, Action<IndexedModSetting> open)
        {
            Entry = entry;
            OpenCommand = new RelayCommand(() => open(entry));
        }

        internal IndexedModSetting Entry { get; }
        public string ModName => Entry.Tab.Name;
        public string Title => Entry.Title;
        public string ToolTip => Entry.ToolTip;
        public string DisplayToolTip => string.IsNullOrWhiteSpace(ToolTip) ? Title : ToolTip;
        public string OpenResultHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchOpenResultHelp);
        public Visibility ToolTipVisibility => string.IsNullOrWhiteSpace(ToolTip) ? Visibility.Collapsed : Visibility.Visible;
        public RelayCommand OpenCommand { get; }
    }

    internal sealed class IndexedModSetting
    {
        public IndexedModSetting(
            LobbyModSettingsEntry tab,
            string key,
            string title,
            string toolTip,
            string sectionKey = "",
            string sectionTitle = "",
            bool isSection = false)
        {
            Tab = tab;
            Key = key;
            Title = title ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            SectionKey = sectionKey ?? string.Empty;
            SectionTitle = sectionTitle ?? string.Empty;
            IsSection = isSection;
        }

        public LobbyModSettingsEntry Tab { get; }
        public string Key { get; }
        public string Title { get; }
        public string ToolTip { get; }
        public string SectionKey { get; }
        public string SectionTitle { get; }
        public bool IsSection { get; }
        public string SectionIdentity =>
            Tab.Name + "\u001f" + (IsSection ? Key : SectionKey);
    }
}
