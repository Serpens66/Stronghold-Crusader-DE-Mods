// Feature: Sort and filter the custom-lord picker in singleplayer and multiplayer skirmish lobbies.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class CustomLordListEnhancementHook : INotifyPropertyChanged, IDisposable
    {
        private delegate void SkirmishAiAddClickDelegate(FRONT_Multiplayer self, string param);

        private enum SortField
        {
            None,
            Name,
            Power,
            Workshop
        }

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();
        private readonly Hook hook;
        private readonly SkirmishAiAddClickDelegate trampoline;
        private FRONT_Multiplayer activeView;
        private ListView activeList;
        private TextBox activeSearchBox;
        private GridViewColumnHeader typeHeader;
        private GridViewColumnHeader nameHeader;
        private GridViewColumnHeader powerHeader;
        private SortField sortField;
        private bool sortAscending;
        private bool searchHasFocus;
        private string searchText = string.Empty;
        private bool firstRefreshLogged;
        private bool disposed;

        public CustomLordListEnhancementHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ClearSearchCommand = new RelayCommand(ClearSearch);

            // These elements keep the game view model everywhere else in FRONT_Multiplayer.
            GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordSearchPanel", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordHeaderPanel", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordTypeHeader", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordNameHeader", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("CustomLordPowerHeader", this);

            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(
                "SkirmishAIAddClick",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            if (method == null)
                throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, "SkirmishAIAddClick(string)");

            Hook installedHook = null;
            try
            {
                installedHook = new Hook(method, (SkirmishAiAddClickDelegate)SkirmishAiAddClickHook);
                trampoline = installedHook.GenerateTrampoline<SkirmishAiAddClickDelegate>();
                hook = installedHook;
            }
            catch
            {
                installedHook?.Dispose();
                throw;
            }
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL custom-lord list hook installed.");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand ClearSearchCommand { get; }
        public Visibility EnhancementVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
        public Visibility SearchPlaceholderVisibility =>
            IsActive && !searchHasFocus && string.IsNullOrEmpty(searchText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public string ClearSearchHelpText => SerpLocalization.Get("BugfixesAndQoL.CustomLordClearSearchHelp");
        public string WorkshopSortHelpText => SerpLocalization.Get("BugfixesAndQoL.CustomLordWorkshopSortHelp");
        public string NameHeaderText => SerpLocalization.Get("BugfixesAndQoL.CustomLordNameHeader");
        public string NameSortHelpText => SerpLocalization.Get("BugfixesAndQoL.CustomLordNameSortHelp");
        public string PowerHeaderText => SerpLocalization.Get("BugfixesAndQoL.CustomLordPowerHeader");
        public string PowerSortHelpText => SerpLocalization.Get("BugfixesAndQoL.CustomLordPowerSortHelp");

        public string SearchText
        {
            get => searchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(searchText, normalized, StringComparison.Ordinal))
                    return;

                searchText = normalized;
                OnPropertyChanged(nameof(SearchText));
                OnPropertyChanged(nameof(SearchPlaceholderVisibility));
                RefreshRows();
            }
        }

        private bool IsActive => settings.EnableMod && settings.EnableCustomLordListEnhancements;

        public void ApplySetting()
        {
            OnPropertyChanged(nameof(EnhancementVisibility));
            OnPropertyChanged(nameof(SearchPlaceholderVisibility));
            RefreshActiveView();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook?.Undo();
            hook?.Dispose();
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL custom-lord list hook disposed.");
        }

        private void SkirmishAiAddClickHook(FRONT_Multiplayer self, string param)
        {
            trampoline(self, param);
            if (!string.Equals(param, "98", StringComparison.Ordinal))
                return;

            try
            {
                Attach(self);
                RefreshActiveView();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not enhance the custom-lord list; the Vanilla list remains usable: {ex}");
            }
        }

        private void Attach(FRONT_Multiplayer self)
        {
            if (ReferenceEquals(activeView, self) && activeList != null && activeSearchBox != null)
                return;

            ListView list = self.FindName("CustomLordList") as ListView;
            TextBox searchBox = self.FindName("CustomLordSearchBox") as TextBox;
            if (list == null || searchBox == null)
                throw new InvalidOperationException("The patched custom-lord list controls were not found.");

            GridView gridView = list.View as GridView;
            if (gridView == null || gridView.Columns.Count != 3)
                throw new InvalidOperationException("The patched custom-lord GridView does not contain three columns.");

            GridViewColumnHeader newTypeHeader = self.FindName("CustomLordTypeHeader") as GridViewColumnHeader;
            GridViewColumnHeader newNameHeader = self.FindName("CustomLordNameHeader") as GridViewColumnHeader;
            GridViewColumnHeader newPowerHeader = self.FindName("CustomLordPowerHeader") as GridViewColumnHeader;
            if (newTypeHeader == null || newNameHeader == null || newPowerHeader == null)
                throw new InvalidOperationException("The patched custom-lord column headers were not found.");

            activeView = self;
            activeList = list;
            activeSearchBox = searchBox;
            typeHeader = newTypeHeader;
            nameHeader = newNameHeader;
            powerHeader = newPowerHeader;

            ((ButtonBase)typeHeader).Click += HeaderClicked;
            ((ButtonBase)nameHeader).Click += HeaderClicked;
            ((ButtonBase)powerHeader).Click += HeaderClicked;
            activeSearchBox.IsKeyboardFocusedChanged += SearchFocusChanged;

            // Reapply a retained query when the frontend recreates its lobby view.
            if (!string.Equals(activeSearchBox.Text, searchText, StringComparison.Ordinal))
                activeSearchBox.Text = searchText;
        }

        private void HeaderClicked(object sender, RoutedEventArgs e)
        {
            if (!IsActive)
                return;

            GridViewColumnHeader header = sender as GridViewColumnHeader;
            string tag = header?.Tag as string;
            SortField requested;
            if (string.Equals(tag, "Power", StringComparison.Ordinal))
                requested = SortField.Power;
            else if (string.Equals(tag, "Workshop", StringComparison.Ordinal))
                requested = SortField.Workshop;
            else
                requested = SortField.Name;

            if (sortField == requested)
                sortAscending = !sortAscending;
            else
            {
                sortField = requested;
                // Names begin A-Z; power and the Steam column begin with their highlighted group.
                sortAscending = requested == SortField.Name;
            }

            RefreshRows();
        }

        private void SearchFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            searchHasFocus = e.NewValue is bool focused && focused;
            MainViewModel.Instance.SetNoesisKeyboardState(searchHasFocus);
            OnPropertyChanged(nameof(SearchPlaceholderVisibility));
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            if (activeSearchBox != null)
                activeSearchBox.Text = string.Empty;
        }

        private void RefreshActiveView()
        {
            if (activeView == null || activeList == null)
                return;

            GridView gridView = activeList.View as GridView;
            if (gridView == null || gridView.Columns.Count != 3)
                return;

            bool active = IsActive;
            gridView.Columns[1].Width = active ? 280f : 370f;
            gridView.Columns[2].Width = active ? 90f : 0f;
            activeList.Margin = active ? new Thickness(0f, 24f, 0f, 0f) : new Thickness(0f);
            typeHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            nameHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
            powerHeader.Visibility = active ? Visibility.Visible : Visibility.Collapsed;

            RefreshRows();
            if (!firstRefreshLogged)
            {
                firstRefreshLogged = true;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL custom-lord list refreshed. enabled={active}, entries={rows.Count}");
            }
        }

        private void RefreshRows()
        {
            if (activeView == null || activeList == null)
                return;

            CustomisationFileManager.CustomLord selectedLord =
                (activeList.SelectedItem as FileRow)?.lord;
            List<CustomisationFileManager.CustomLord> lords =
                CustomisationFileManager.Instance.GetCustomLords();

            if (IsActive)
            {
                lords.RemoveAll(lord => !MatchesSearch(lord));
                if (sortField != SortField.None)
                    lords.Sort(CompareLords);
            }

            rows.Clear();
            FileRow selectedRow = null;
            foreach (CustomisationFileManager.CustomLord lord in lords)
            {
                FileRow row = new FileRow
                {
                    Text1 = lord.lordDisplayName,
                    Text2 = IsActive ? GetPower(lord).ToString() : string.Empty,
                    lord = lord,
                    TypeImage = lord.workshop ? MainViewModel.Instance.GameSprites[89] : null
                };
                rows.Add(row);
                if (ReferenceEquals(lord, selectedLord))
                    selectedRow = row;
            }

            activeList.ItemsSource = rows;
            if (selectedRow != null)
                activeList.SelectedItem = selectedRow;
            else if (rows.Count > 0)
                activeList.SelectedIndex = 0;
        }

        private bool MatchesSearch(CustomisationFileManager.CustomLord lord)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return true;

            return ContainsIgnoreCase(lord.lordDisplayName, searchText) ||
                   ContainsIgnoreCase(lord.lordName, searchText);
        }

        private int CompareLords(CustomisationFileManager.CustomLord left, CustomisationFileManager.CustomLord right)
        {
            int comparison;
            if (sortField == SortField.Workshop)
            {
                comparison = left.workshop.CompareTo(right.workshop);
                if (!sortAscending)
                    comparison = -comparison;
                if (comparison != 0)
                    return comparison;
            }
            else if (sortField == SortField.Power)
            {
                comparison = GetPower(left).CompareTo(GetPower(right));
                if (!sortAscending)
                    comparison = -comparison;
                if (comparison != 0)
                    return comparison;
            }

            comparison = StringComparer.CurrentCultureIgnoreCase.Compare(
                left?.lordDisplayName ?? string.Empty,
                right?.lordDisplayName ?? string.Empty);
            return sortField == SortField.Name && !sortAscending ? -comparison : comparison;
        }

        private static int GetPower(CustomisationFileManager.CustomLord lord)
        {
            // Vanilla selects configs[0] when the lord is first added, so the list reports that same AIC.
            return lord?.configs != null && lord.configs.Count > 0
                ? lord.configs[0].lordData.lord_power_display_level
                : 0;
        }

        private static bool ContainsIgnoreCase(string value, string query)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
