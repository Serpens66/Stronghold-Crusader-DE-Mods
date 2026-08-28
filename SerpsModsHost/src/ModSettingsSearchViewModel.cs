using BepInEx.Logging;
using Noesis;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.NoesisUtil;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SerpsModsHost
{
    public sealed class ModSettingsSearchViewModel : INotifyPropertyChanged
    {
        private const string ExportMethodName = "System_GetModSettingsSearchEntries";
        private readonly ManualLogSource log;
        private readonly List<IndexedModSetting> index = new List<IndexedModSetting>();
        private string searchText = string.Empty;
        private bool includeToolTips;
        private ModSettingsSearchFilter selectedModFilter;

        public ModSettingsSearchViewModel(ManualLogSource log)
        {
            this.log = log;
            ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
            Results = new ObservableCollection<ModSettingsSearchResult>();
            ModFilters = new ObservableCollection<ModSettingsSearchFilter>();
            RebuildModFilters();

            GameXAMLManagerAPI.Instance.RegisteredModSettings.CollectionChanged += OnModTabsChanged;
            Plugin.ModSettingsHubViewModel.PropertyChanged += OnHubPropertyChanged;
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
                if (string.Equals(searchText, normalized, StringComparison.Ordinal))
                    return;
                searchText = normalized;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SearchResultsVisibility));
                if (!string.IsNullOrWhiteSpace(searchText) && index.Count == 0)
                    RebuildIndex();
                ApplyFilter();
            }
        }

        public bool IncludeToolTips
        {
            get => includeToolTips;
            set
            {
                if (includeToolTips == value)
                    return;
                includeToolTips = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        public ModSettingsSearchFilter SelectedModFilter
        {
            get => selectedModFilter;
            set
            {
                if (ReferenceEquals(selectedModFilter, value))
                    return;
                selectedModFilter = value;
                OnPropertyChanged();
                ApplyFilter();
            }
        }

        public Visibility SearchResultsVisibility =>
            string.IsNullOrWhiteSpace(searchText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility NoResultsVisibility =>
            !string.IsNullOrWhiteSpace(searchText) && Results.Count == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

        public string ResultCountText => SerpLocalization.Get(
            SerpLocalization.SerpsModsSearchResultCount,
            "Count", Results.Count);

        private void OnModTabsChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            RebuildModFilters();
            index.Clear();
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                RebuildIndex();
                ApplyFilter();
            }
        }

        private void OnHubPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(SHCDESE.ViewModels.LobbyModSettingsHubViewModel.WindowVisibility) &&
                Plugin.ModSettingsHubViewModel.WindowVisibility == Visibility.Visible)
            {
                // Opening the native Noesis modal must remain side-effect free. Indexing begins
                // only when the user enters a query, after the selected view is fully attached.
                index.Clear();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    "[ModSettingsSearch] Hub opened; indexing is deferred until the first query.");
            }
        }

        private void RebuildModFilters()
        {
            string selectedName = selectedModFilter?.ModName;
            ModFilters.Clear();
            ModFilters.Add(new ModSettingsSearchFilter(
                SerpLocalization.Get(SerpLocalization.SerpsModsSearchAllMods),
                null));
            foreach (LobbyModSettingsEntry tab in GameXAMLManagerAPI.Instance.RegisteredModSettings)
                ModFilters.Add(new ModSettingsSearchFilter(tab.Name, tab.Name));
            SelectedModFilter = ModFilters.FirstOrDefault(filter =>
                string.Equals(filter.ModName, selectedName, StringComparison.Ordinal)) ?? ModFilters[0];
        }

        private void RebuildIndex()
        {
            index.Clear();
            Shared.DebugLogHelper.LogDebug(
                log,
                "[ModSettingsSearch] Building search index without changing the selected tab.");
            foreach (LobbyModSettingsEntry tab in GameXAMLManagerAPI.Instance.RegisteredModSettings)
            {
                try
                {
                    int previousCount = index.Count;
                    List<IndexedModSetting> exportedEntries;
                    try
                    {
                        exportedEntries = ReadExplicitEntries(tab);
                    }
                    catch (Exception ex)
                    {
                        // A foreign or older shared implementation must not make the whole mod
                        // unsearchable. Its text remains available through the fallback.
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"[ModSettingsSearch] Search-anchor export failed for [{tab.Name}]; using automatic text search: {ex}");
                        exportedEntries = new List<IndexedModSetting>();
                    }

                    index.AddRange(
                        exportedEntries.Count > 0
                            ? exportedEntries
                            : ReadAutomaticEntries(tab));
                    int addedCount = index.Count - previousCount;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"[ModSettingsSearch] Indexed [{tab.Name}]: entries={addedCount}, source={(exportedEntries.Count > 0 ? "shared" : "automatic")}.");
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"[ModSettingsSearch] Could not index [{tab.Name}]; other mods remain searchable: {ex}");
                }
            }
            Shared.DebugLogHelper.LogDebug(
                log,
                $"[ModSettingsSearch] Search index ready: entries={index.Count}.");
        }

        private static List<IndexedModSetting> ReadExplicitEntries(LobbyModSettingsEntry tab)
        {
            var result = new List<IndexedModSetting>();
            MethodInfo export = tab.ViewModel?.GetType().GetMethod(
                ExportMethodName,
                BindingFlags.Instance | BindingFlags.Public);
            if (export == null || !(export.Invoke(tab.ViewModel, new object[] { tab.View }) is IEnumerable entries))
                return result;

            foreach (object entry in entries)
            {
                if (entry == null)
                    continue;
                Type type = entry.GetType();
                string key = ReadString(type, entry, "Key");
                string title = ReadString(type, entry, "Title");
                string toolTip = ReadString(type, entry, "ToolTip");
                FrameworkElement target = type.GetProperty("Target")?.GetValue(entry, null) as FrameworkElement;
                if (key.Length == 0 || title.Length == 0)
                    continue;
                result.Add(new IndexedModSetting(tab, key, title, toolTip, target, true));
            }
            return result;
        }

        private static string ReadString(Type type, object instance, string propertyName) =>
            type.GetProperty(propertyName)?.GetValue(instance, null)?.ToString()?.Trim() ?? string.Empty;

        private static List<IndexedModSetting> ReadAutomaticEntries(LobbyModSettingsEntry tab)
        {
            var result = new List<IndexedModSetting>();
            var seen = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase);
            var visited = new HashSet<DependencyObject>();
            var structuralParents = new Dictionary<DependencyObject, DependencyObject>();
            VisitAutomatic(tab, tab.View, result, seen, visited, structuralParents, null, 0);
            return result;
        }

        private static void VisitAutomatic(
            LobbyModSettingsEntry tab,
            DependencyObject current,
            List<IndexedModSetting> result,
            HashSet<string> seen,
            HashSet<DependencyObject> visited,
            Dictionary<DependencyObject, DependencyObject> structuralParents,
            DependencyObject structuralParent,
            int depth)
        {
            if (current == null || depth > 80 || !visited.Add(current))
                return;

            if (structuralParent != null && !structuralParents.ContainsKey(current))
                structuralParents.Add(current, structuralParent);

            if (current is FrameworkElement element && IsInteractive(element))
            {
                string toolTip = ReadToolTip(element);
                string title = FindAutomaticTitle(element, structuralParents);
                if (title.Length == 0 && toolTip.Length > 0)
                    title = FirstSentence(toolTip);
                string identity = title + "\u001f" + toolTip;
                if (title.Length > 0 && seen.Add(identity))
                    result.Add(new IndexedModSetting(tab, identity, title, toolTip, element, false));
            }

            foreach (DependencyObject child in EnumerateChildren(current))
                VisitAutomatic(tab, child, result, seen, visited, structuralParents, current, depth + 1);
        }

        private static IEnumerable<DependencyObject> EnumerateChildren(DependencyObject current)
        {
            var result = new List<DependencyObject>();
            var seen = new HashSet<DependencyObject>();
            if (current is Panel panel)
            {
                for (int index = 0; index < panel.Children.Count; index++)
                    AddChild(result, seen, panel.Children[index]);
            }
            if (current is Decorator decorator)
                AddChild(result, seen, decorator.Child);
            if (current is ContentControl contentControl)
                AddChild(result, seen, contentControl.Content as DependencyObject);
            int visualCount = VisualTreeHelper.GetChildrenCount(current);
            for (int index = 0; index < visualCount; index++)
                AddChild(result, seen, VisualTreeHelper.GetChild(current, index));
            return result;
        }

        private static void AddChild(
            ICollection<DependencyObject> result,
            ISet<DependencyObject> seen,
            DependencyObject child)
        {
            if (child != null && seen.Add(child))
                result.Add(child);
        }

        private static bool IsInteractive(FrameworkElement element) =>
            element is Button || element is CheckBox || element is ComboBox ||
            element is Slider || element is TextBox;

        private static string FindAutomaticTitle(
            FrameworkElement element,
            IReadOnlyDictionary<DependencyObject, DependencyObject> structuralParents)
        {
            if (element is ContentControl contentControl)
            {
                string contentText = ReadContentText(contentControl.Content);
                if (contentText.Length > 0)
                    return contentText;
            }

            DependencyObject current = element;
            for (int level = 0; level < 4; level++)
            {
                DependencyObject parent = VisualTreeHelper.GetParent(current);
                if (parent == null)
                    structuralParents.TryGetValue(current, out parent);
                if (parent == null)
                    break;
                int row = Grid.GetRow(current);
                var candidates = new List<string>();
                int count = VisualTreeHelper.GetChildrenCount(parent);
                for (int index = 0; index < count; index++)
                {
                    DependencyObject sibling = VisualTreeHelper.GetChild(parent, index);
                    if (ReferenceEquals(sibling, current))
                        continue;
                    if (sibling is TextBlock textBlock &&
                        (parent is not Grid || Grid.GetRow(textBlock) == row) &&
                        !string.IsNullOrWhiteSpace(textBlock.Text))
                    {
                        candidates.Add(textBlock.Text.Trim());
                    }
                    else if (sibling is FrameworkElement siblingElement &&
                        (parent is not Grid || Grid.GetRow(siblingElement) == row))
                    {
                        string nested = ReadContentText(siblingElement);
                        if (nested.Length > 0)
                            candidates.Add(nested);
                    }
                }
                if (candidates.Count > 0)
                    return candidates[0];
                current = parent;
            }
            return string.Empty;
        }

        private static string ReadContentText(object content)
        {
            if (content is string text)
                return text.Trim();
            if (content is TextBlock textBlock)
                return textBlock.Text?.Trim() ?? string.Empty;
            if (content is DependencyObject dependencyObject)
            {
                int count = VisualTreeHelper.GetChildrenCount(dependencyObject);
                for (int index = 0; index < count; index++)
                {
                    string nested = ReadContentText(VisualTreeHelper.GetChild(dependencyObject, index));
                    if (nested.Length > 0)
                        return nested;
                }
            }
            return string.Empty;
        }

        private static string ReadToolTip(DependencyObject element)
        {
            object toolTip = ToolTipService.GetToolTip(element);
            if (toolTip is ToolTip explicitToolTip)
                toolTip = explicitToolTip.Content;
            return ReadContentText(toolTip);
        }

        private static string FirstSentence(string text)
        {
            string normalized = (text ?? string.Empty).Trim();
            int end = normalized.IndexOfAny(new[] { '.', '!', '?', '\r', '\n' });
            if (end >= 0)
                normalized = normalized.Substring(0, end + 1);
            return normalized.Length <= 120 ? normalized : normalized.Substring(0, 117) + "...";
        }

        private void ApplyFilter()
        {
            Results.Clear();
            string query = searchText.Trim();
            if (query.Length == 0)
            {
                NotifyResultPresentation();
                return;
            }

            IEnumerable<IndexedModSetting> candidates = index;
            if (!string.IsNullOrEmpty(selectedModFilter?.ModName))
            {
                candidates = candidates.Where(candidate =>
                    string.Equals(candidate.Tab.Name, selectedModFilter.ModName, StringComparison.Ordinal));
            }

            foreach (IndexedModSetting candidate in candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Rank = ModSettingsSearchPolicy.Rank(
                        candidate.Title,
                        candidate.ToolTip,
                        query,
                        includeToolTips)
                })
                .Where(item => item.Rank < int.MaxValue)
                .OrderBy(item => item.Rank)
                .ThenBy(item => item.Candidate.Tab.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(item => item.Candidate.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(item => item.Candidate))
            {
                Results.Add(new ModSettingsSearchResult(candidate, OpenResult));
            }
            NotifyResultPresentation();
        }

        private void NotifyResultPresentation()
        {
            OnPropertyChanged(nameof(ResultCountText));
            OnPropertyChanged(nameof(NoResultsVisibility));
        }

        private void OpenResult(IndexedModSetting result)
        {
            Plugin.ModSettingsHubViewModel.SelectedTab = result.Tab;
            SearchText = string.Empty;
            UnityMainThreadDispatcher.Instance.EnqueueDeferred(() =>
            {
                try
                {
                    FrameworkElement target = result.Target;
                    if (target == null)
                    {
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"[ModSettingsSearch] Opened [{result.Tab.Name}/{result.Key}], but its control is not available for focusing yet.");
                        return;
                    }
                    FrameworkElement navigationTarget = ResolveVisibleNavigationTarget(target);
                    navigationTarget.BringIntoView();
                    if (ReferenceEquals(navigationTarget, target))
                        target.Focus();
                    var pulse = new DoubleAnimation
                    {
                        From = 1.0f,
                        To = 0.45f,
                        Duration = new Duration(TimeSpan.FromMilliseconds(220)),
                        AutoReverse = true,
                        RepeatBehavior = new RepeatBehavior(2.0f),
                        FillBehavior = FillBehavior.Stop
                    };
                    navigationTarget.BeginAnimation(UIElement.OpacityProperty, pulse);
                }
                catch (Exception ex)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"[ModSettingsSearch] Could not navigate to [{result.Tab.Name}/{result.Key}]: {ex}");
                }
            });
        }

        private static FrameworkElement ResolveVisibleNavigationTarget(FrameworkElement target)
        {
            FrameworkElement current = target;
            while (current != null && !current.IsVisible)
                current = VisualTreeHelper.GetParent(current) as FrameworkElement;
            return current ?? target;
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
            // Catalog results intentionally remain navigation-only. Cloning bindings requires
            // walking realized native Noesis controls and proved unsafe across tab transitions.
            editor = null;
            OpenCommand = new RelayCommand(() => open(entry));
        }

        private readonly FrameworkElement editor;
        internal IndexedModSetting Entry { get; }
        public string ModName => Entry.Tab.Name;
        public string Title => Entry.Title;
        public string ToolTip => Entry.ToolTip;
        public string DisplayToolTip => string.IsNullOrWhiteSpace(ToolTip) ? Title : ToolTip;
        public string OpenResultHelpText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchOpenResultHelp);
        public string DirectUnavailableText => SerpLocalization.Get(SerpLocalization.SerpsModsSearchDirectUnavailable);
        public Visibility ToolTipVisibility => string.IsNullOrWhiteSpace(ToolTip) ? Visibility.Collapsed : Visibility.Visible;
        public FrameworkElement Editor => editor;
        public Visibility EditorVisibility => Editor == null ? Visibility.Collapsed : Visibility.Visible;
        public Visibility DirectUnavailableVisibility => Editor == null ? Visibility.Visible : Visibility.Collapsed;
        public RelayCommand OpenCommand { get; }
    }

    internal sealed class IndexedModSetting
    {
        public IndexedModSetting(
            LobbyModSettingsEntry tab,
            string key,
            string title,
            string toolTip,
            FrameworkElement target,
            bool isExplicit)
        {
            Tab = tab;
            Key = key;
            Title = title ?? string.Empty;
            ToolTip = toolTip ?? string.Empty;
            Target = target;
            IsExplicit = isExplicit;
        }

        public LobbyModSettingsEntry Tab { get; }
        public string Key { get; }
        public string Title { get; }
        public string ToolTip { get; }
        public FrameworkElement Target { get; }
        public bool IsExplicit { get; }
    }
}
