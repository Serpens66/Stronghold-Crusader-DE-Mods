// Feature: Sort and filter the AIV and AIC lists in the AI castle/settings dialog.
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
    internal sealed class AiCastleSettingsListEnhancementHook : INotifyPropertyChanged, IDisposable
    {
        private delegate void ShowDelegate(
            int thisPlayer,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode);

        private delegate void ButtonClickedDelegate(FRONT_Multiplayer_AISettings self, string param);

        private enum SortField
        {
            None,
            Name,
            Power,
            Origin
        }

        private static readonly FieldInfo AivInfoField = FindField("AIVInfo");
        private static readonly FieldInfo AivListField = FindField("aivList");
        private static readonly FieldInfo LordListField = FindField("lordList");

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Hook showHook;
        private readonly Hook buttonClickedHook;
        private readonly ShowDelegate showTrampoline;
        private readonly ButtonClickedDelegate buttonClickedTrampoline;
        private readonly HashSet<FRONT_Multiplayer_AISettings> attachedViews =
            new HashSet<FRONT_Multiplayer_AISettings>();

        private FRONT_Multiplayer_AISettings activeView;
        private ListView aivListControl;
        private ListView aicListControl;
        private TextBox aivSearchBox;
        private TextBox aicSearchBox;
        private Grid aivHeaderPanel;
        private Grid aicHeaderPanel;
        private Grid aicSearchPanel;
        private bool aivSearchHasFocus;
        private bool aicSearchHasFocus;
        private bool disposed;
        private string aivSearchText = string.Empty;
        private string aicSearchText = string.Empty;
        private SortField aivSortField;
        private SortField aicSortField;
        private bool aivSortAscending;
        private bool aicSortAscending;

        public AiCastleSettingsListEnhancementHook(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            ClearAivSearchCommand = new RelayCommand(() => AivSearchText = string.Empty);
            ClearAicSearchCommand = new RelayCommand(() => AicSearchText = string.Empty);

            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAivHeaderPanel", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAivSearchPanel", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAicHeaderPanel", this);
            GameXAMLManagerAPI.Instance.RegisterBinding("BugfixesAndQoLAicSearchPanel", this);

            MethodInfo showMethod = typeof(FRONT_Multiplayer_AISettings).GetMethod(
                "Show",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(FRONT_Multiplayer.MPAIVInfo), typeof(bool) },
                null);
            MethodInfo buttonClickedMethod = typeof(FRONT_Multiplayer_AISettings).GetMethod(
                "ButtonClicked",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(string) },
                null);
            if (showMethod == null || buttonClickedMethod == null)
                throw new MissingMethodException("The Vanilla AI-settings dialog methods were not found.");

            Hook installedShow = null;
            Hook installedButtonClicked = null;
            try
            {
                installedShow = new Hook(showMethod, (ShowDelegate)ShowHook);
                showTrampoline = installedShow.GenerateTrampoline<ShowDelegate>();
                installedButtonClicked = new Hook(
                    buttonClickedMethod,
                    (ButtonClickedDelegate)ButtonClickedHook);
                buttonClickedTrampoline =
                    installedButtonClicked.GenerateTrampoline<ButtonClickedDelegate>();
                showHook = installedShow;
                buttonClickedHook = installedButtonClicked;
            }
            catch
            {
                installedButtonClicked?.Dispose();
                installedShow?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL AI castle/settings list hook installed.");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public RelayCommand ClearAivSearchCommand { get; }
        public RelayCommand ClearAicSearchCommand { get; }
        public Visibility EnhancementVisibility => IsActive ? Visibility.Visible : Visibility.Collapsed;
        public Visibility AivSearchPlaceholderVisibility =>
            IsActive && !aivSearchHasFocus && string.IsNullOrEmpty(aivSearchText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public Visibility AicSearchPlaceholderVisibility =>
            IsActive && !aicSearchHasFocus && string.IsNullOrEmpty(aicSearchText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        public string NameHeaderText => SerpLocalization.Get("BugfixesAndQoL.CustomLordNameHeader");
        public string OriginHeaderText => SerpLocalization.Get("BugfixesAndQoL.AiListOriginHeader");
        public string PowerHeaderText => SerpLocalization.Get("BugfixesAndQoL.CustomLordPowerHeader");
        public string SearchHelpText => SerpLocalization.Get("BugfixesAndQoL.AiListSearchHelp");
        public string ClearSearchHelpText => SerpLocalization.Get("BugfixesAndQoL.AiListClearSearchHelp");
        public string AivOriginSortHelpText => SerpLocalization.Get("BugfixesAndQoL.AivOriginSortHelp");
        public string AivNameSortHelpText => SerpLocalization.Get("BugfixesAndQoL.AivNameSortHelp");
        public string AicOriginSortHelpText => SerpLocalization.Get("BugfixesAndQoL.AicOriginSortHelp");
        public string AicNameSortHelpText => SerpLocalization.Get("BugfixesAndQoL.AicNameSortHelp");
        public string AicPowerSortHelpText => SerpLocalization.Get("BugfixesAndQoL.AicPowerSortHelp");

        public string AivSearchText
        {
            get => aivSearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(aivSearchText, normalized, StringComparison.Ordinal))
                    return;
                aivSearchText = normalized;
                OnPropertyChanged(nameof(AivSearchText));
                OnPropertyChanged(nameof(AivSearchPlaceholderVisibility));
                RefreshEnhancedLists();
            }
        }

        public string AicSearchText
        {
            get => aicSearchText;
            set
            {
                string normalized = value ?? string.Empty;
                if (string.Equals(aicSearchText, normalized, StringComparison.Ordinal))
                    return;
                aicSearchText = normalized;
                OnPropertyChanged(nameof(AicSearchText));
                OnPropertyChanged(nameof(AicSearchPlaceholderVisibility));
                RefreshEnhancedLists();
            }
        }

        private bool IsActive => settings.EnableMod && settings.EnableCustomLordListEnhancements;

        public void ApplySetting()
        {
            OnPropertyChanged(nameof(EnhancementVisibility));
            OnPropertyChanged(nameof(AivSearchPlaceholderVisibility));
            OnPropertyChanged(nameof(AicSearchPlaceholderVisibility));
            if (activeView == null || GetAivInfo(activeView) == null)
                return;

            if (IsActive)
            {
                AttachAndRefresh(activeView, "setting enabled");
                UpdateDialogKeyboardState();
                return;
            }

            RestoreCanonicalLists(activeView, GetAivInfo(activeView));
            activeView.populateList(null, false);
            RestoreVanillaPresentation(activeView);
            UpdateDialogKeyboardState();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            buttonClickedHook?.Undo();
            showHook?.Undo();
            buttonClickedHook?.Dispose();
            showHook?.Dispose();
        }

        private void ShowHook(
            int thisPlayer,
            FRONT_Multiplayer.MPAIVInfo aivInfo,
            bool mpMode)
        {
            showTrampoline(thisPlayer, aivInfo, mpMode);

            if (!IsActive)
                return;

            // Wait until Vanilla Init and every other Init hook have completely unwound before
            // replacing native Noesis list sources.
            AttachAndRefresh(FRONT_Multiplayer_AISettings.Instance, "dialog shown");
            UpdateDialogKeyboardState();
        }

        private void ButtonClickedHook(FRONT_Multiplayer_AISettings self, string param)
        {
            if (!IsActive)
            {
                buttonClickedTrampoline(self, param);
                return;
            }

            // Preset changes use fixed indexes, so they must start from Vanilla's complete list.
            if (RequiresCanonicalLists(param))
                RestoreCanonicalLists(self, GetAivInfo(self));

            buttonClickedTrampoline(self, param);
            if (IsListMutation(param))
                AttachAndRefresh(self, $"button '{param}'");
            if (string.Equals(param, "Back", StringComparison.Ordinal))
                UpdateDialogKeyboardState();
        }

        private void AttachAndRefresh(FRONT_Multiplayer_AISettings self, string reason)
        {
            try
            {
                Attach(self);
                RefreshEnhancedLists();
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL refreshed AI castle/settings lists after {reason}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL could not enhance the AI castle/settings lists after {reason}; Vanilla remains usable: {ex}");
            }
        }

        private void Attach(FRONT_Multiplayer_AISettings self)
        {
            activeView = self;
            aivListControl = self.FindName("AIVList") as ListView;
            aicListControl = self.FindName("LordList") as ListView;
            aivSearchBox = self.FindName("BugfixesAndQoLAivSearchBox") as TextBox;
            aicSearchBox = self.FindName("BugfixesAndQoLAicSearchBox") as TextBox;
            aivHeaderPanel = self.FindName("BugfixesAndQoLAivHeaderPanel") as Grid;
            aicHeaderPanel = self.FindName("BugfixesAndQoLAicHeaderPanel") as Grid;
            aicSearchPanel = self.FindName("BugfixesAndQoLAicSearchPanel") as Grid;
            if (aivListControl == null || aicListControl == null || aivSearchBox == null ||
                aicSearchBox == null || aivHeaderPanel == null || aicHeaderPanel == null ||
                aicSearchPanel == null)
                throw new InvalidOperationException("The patched AI-settings controls were not found.");

            // Search focus must not disable Ctrl/Shift input used by Vanilla multi-selection.
            aivListControl.SelectionMode = SelectionMode.Extended;

            if (!attachedViews.Add(self))
                return;

            AttachHeader(self, "BugfixesAndQoLAivOriginHeader", HeaderClicked);
            AttachHeader(self, "BugfixesAndQoLAivNameHeader", HeaderClicked);
            AttachHeader(self, "BugfixesAndQoLAicOriginHeader", HeaderClicked);
            AttachHeader(self, "BugfixesAndQoLAicNameHeader", HeaderClicked);
            AttachHeader(self, "BugfixesAndQoLAicPowerHeader", HeaderClicked);
            aivSearchBox.IsKeyboardFocusedChanged += AivSearchFocusChanged;
            aicSearchBox.IsKeyboardFocusedChanged += AicSearchFocusChanged;
            self.IsVisibleChanged += DialogVisibilityChanged;
            // Vanilla registered its handler in the control constructor, so this runs after a
            // double-click action and can safely restore the enhanced presentation.
            aivListControl.MouseDoubleClick += AivListDoubleClicked;
        }

        private static void AttachHeader(
            FRONT_Multiplayer_AISettings self,
            string name,
            RoutedEventHandler handler)
        {
            GridViewColumnHeader header = self.FindName(name) as GridViewColumnHeader;
            if (header == null)
                throw new InvalidOperationException($"The patched header '{name}' was not found.");
            ((ButtonBase)header).Click += handler;
        }

        private void HeaderClicked(object sender, RoutedEventArgs e)
        {
            if (!IsActive)
                return;

            string tag = (sender as GridViewColumnHeader)?.Tag as string;
            bool isAic = !string.IsNullOrEmpty(tag) && tag.StartsWith("AIC_", StringComparison.Ordinal);
            SortField requested = tag != null && tag.EndsWith("Power", StringComparison.Ordinal)
                ? SortField.Power
                : tag != null && tag.EndsWith("Origin", StringComparison.Ordinal)
                    ? SortField.Origin
                    : SortField.Name;

            if (isAic)
                ChangeSort(ref aicSortField, ref aicSortAscending, requested);
            else
                ChangeSort(ref aivSortField, ref aivSortAscending, requested);
            RefreshEnhancedLists();
        }

        private void AivListDoubleClicked(object sender, MouseButtonEventArgs e)
        {
            if (IsActive && activeView != null)
                AttachAndRefresh(activeView, "AIV double-click");
        }

        private static void ChangeSort(ref SortField current, ref bool ascending, SortField requested)
        {
            if (current == requested)
                ascending = !ascending;
            else
            {
                current = requested;
                ascending = requested == SortField.Name;
            }
        }

        private void RefreshEnhancedLists()
        {
            if (!IsActive || activeView == null ||
                aivListControl == null || aicListControl == null)
                return;

            FRONT_Multiplayer.MPAIVInfo info = GetAivInfo(activeView);
            List<CustomisationFileManager.CustomAIV> canonicalAivs = GetCanonicalAivs(info);
            List<CustomisationFileManager.CustomLordConfig> canonicalAics = GetCanonicalAics(info);
            if (canonicalAivs == null || canonicalAics == null)
                return;

            List<CustomisationFileManager.CustomAIV> selectedAivs = GetSelectedAivs(aivListControl);
            List<CustomisationFileManager.CustomAIV> visibleAivs =
                new List<CustomisationFileManager.CustomAIV>(canonicalAivs);
            visibleAivs.RemoveAll(aiv => !ContainsIgnoreCase(aiv?.AIVName, aivSearchText));
            if (aivSortField != SortField.None)
                visibleAivs.Sort((left, right) => CompareAivs(left, right, canonicalAivs, info));

            ObservableCollection<FileRow> aivRows = BuildAivRows(visibleAivs);
            // Vanilla and CastlePlanner both translate selected row indexes through this field.
            AivListField.SetValue(activeView, visibleAivs);
            aivListControl.ItemsSource = aivRows;
            SelectAivs(aivRows, selectedAivs);

            CustomisationFileManager.CustomLordConfig selectedAic =
                info?.lordConfig ?? (aicListControl.SelectedItem as FileRow)?.lordConfig;
            List<CustomisationFileManager.CustomLordConfig> visibleAics =
                new List<CustomisationFileManager.CustomLordConfig>(canonicalAics);
            if (info != null && !info.builtInLord)
            {
                visibleAics.RemoveAll(aic => !ContainsIgnoreCase(aic?.name, aicSearchText));
                if (aicSortField != SortField.None)
                    visibleAics.Sort(CompareAics);
            }

            // Vanilla's SelectionChanged handler indexes this private list directly.
            LordListField.SetValue(activeView, visibleAics);
            ObservableCollection<FileRow> aicRows = BuildAicRows(visibleAics);
            aicListControl.ItemsSource = aicRows;
            if (info != null && !info.builtInLord)
                SelectAic(aicRows, selectedAic);

            aivListControl.Margin = new Thickness(0f, 24f, 0f, 0f);
            aicListControl.Margin = new Thickness(0f, 24f, 0f, 0f);
            SetColumnWidths(aivListControl, 70f, 290f);
            SetColumnWidths(aicListControl, 70f, 200f, 90f);
            bool aicEnabled = aicListControl.IsEnabled;
            aicHeaderPanel.IsEnabled = aicEnabled;
            aicSearchPanel.IsEnabled = aicEnabled;
        }

        private int CompareAivs(
            CustomisationFileManager.CustomAIV left,
            CustomisationFileManager.CustomAIV right,
            List<CustomisationFileManager.CustomAIV> canonical,
            FRONT_Multiplayer.MPAIVInfo info)
        {
            int comparison = 0;
            if (aivSortField == SortField.Origin)
            {
                comparison = GetAivOrigin(left, canonical.IndexOf(left), info)
                    .CompareTo(GetAivOrigin(right, canonical.IndexOf(right), info));
                if (!aivSortAscending)
                    comparison = -comparison;
            }
            if (comparison == 0)
                comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left?.AIVName, right?.AIVName);
            return aivSortField == SortField.Name && !aivSortAscending ? -comparison : comparison;
        }

        private int CompareAics(
            CustomisationFileManager.CustomLordConfig left,
            CustomisationFileManager.CustomLordConfig right)
        {
            int comparison = 0;
            if (aicSortField == SortField.Origin)
                comparison = (left?.workshop ?? false).CompareTo(right?.workshop ?? false);
            else if (aicSortField == SortField.Power)
                comparison = GetPower(left).CompareTo(GetPower(right));

            if (comparison != 0 && !aicSortAscending)
                comparison = -comparison;
            if (comparison == 0)
                comparison = StringComparer.CurrentCultureIgnoreCase.Compare(left?.name, right?.name);
            return aicSortField == SortField.Name && !aicSortAscending ? -comparison : comparison;
        }

        private static int GetAivOrigin(
            CustomisationFileManager.CustomAIV aiv,
            int canonicalIndex,
            FRONT_Multiplayer.MPAIVInfo info)
        {
            if (aiv == null)
                return int.MaxValue;
            if (aiv.builtIn)
            {
                if (info != null && info.lordType < 16 && canonicalIndex >= 16)
                    return 2;
                if (info != null && info.lordType < 16 && canonicalIndex >= 8)
                    return 1;
                return 0;
            }
            return aiv.workshop ? 4 : 3;
        }

        private static int GetPower(CustomisationFileManager.CustomLordConfig config) =>
            config == null ? 0 : config.lordData.lord_power_display_level;

        private static ObservableCollection<FileRow> BuildAivRows(
            IEnumerable<CustomisationFileManager.CustomAIV> aivs)
        {
            ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();
            foreach (CustomisationFileManager.CustomAIV aiv in aivs)
            {
                rows.Add(new FileRow
                {
                    Text1 = aiv.AIVName,
                    aiv = aiv,
                    TypeImage = aiv.builtIn
                        ? MainViewModel.Instance.GameSprites[88]
                        : aiv.workshop
                            ? MainViewModel.Instance.GameSprites[89]
                            : MainViewModel.Instance.GameSprites[90]
                });
            }
            return rows;
        }

        private static ObservableCollection<FileRow> BuildAicRows(
            IEnumerable<CustomisationFileManager.CustomLordConfig> configs)
        {
            ObservableCollection<FileRow> rows = new ObservableCollection<FileRow>();
            foreach (CustomisationFileManager.CustomLordConfig config in configs)
            {
                rows.Add(new FileRow
                {
                    Text1 = config.name,
                    Text2 = GetPower(config).ToString(),
                    lordConfig = config,
                    TypeImage = config.workshop
                        ? MainViewModel.Instance.GameSprites[89]
                        : MainViewModel.Instance.GameSprites[90]
                });
            }
            return rows;
        }

        private void SelectAivs(
            ObservableCollection<FileRow> rows,
            List<CustomisationFileManager.CustomAIV> selected)
        {
            if (selected == null || selected.Count == 0)
                return;
            foreach (FileRow row in rows)
            {
                if (selected.Contains(row.aiv))
                    aivListControl.SelectedItems.Add(row);
            }
        }

        private static List<CustomisationFileManager.CustomAIV> GetSelectedAivs(ListView list)
        {
            List<CustomisationFileManager.CustomAIV> selected =
                new List<CustomisationFileManager.CustomAIV>();
            if (list?.SelectedItems == null)
                return selected;
            foreach (object item in list.SelectedItems)
            {
                CustomisationFileManager.CustomAIV aiv = (item as FileRow)?.aiv;
                if (aiv != null)
                    selected.Add(aiv);
            }
            return selected;
        }

        private void SelectAic(
            ObservableCollection<FileRow> rows,
            CustomisationFileManager.CustomLordConfig selected)
        {
            if (selected == null)
                return;
            foreach (FileRow row in rows)
            {
                if (ReferenceEquals(row.lordConfig, selected) || row.lordConfig.checksum == selected.checksum)
                {
                    aicListControl.SelectedItem = row;
                    return;
                }
            }
        }

        private void AivSearchFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            aivSearchHasFocus = e.NewValue is bool focused && focused;
            UpdateDialogKeyboardState();
            OnPropertyChanged(nameof(AivSearchPlaceholderVisibility));
        }

        private void AicSearchFocusChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            aicSearchHasFocus = e.NewValue is bool focused && focused;
            UpdateDialogKeyboardState();
            OnPropertyChanged(nameof(AicSearchPlaceholderVisibility));
        }

        private void DialogVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) =>
            UpdateDialogKeyboardState();

        private void UpdateDialogKeyboardState()
        {
            bool dialogVisible = IsActive && activeView?.IsVisible == true;
            MainViewModel.Instance.SetNoesisKeyboardState(dialogVisible);
        }

        private void RestoreCanonicalLists(
            FRONT_Multiplayer_AISettings self,
            FRONT_Multiplayer.MPAIVInfo info)
        {
            List<CustomisationFileManager.CustomAIV> aivs = GetCanonicalAivs(info);
            List<CustomisationFileManager.CustomLordConfig> aics = GetCanonicalAics(info);
            if (aivs != null)
                AivListField.SetValue(self, aivs);
            if (aics != null)
                LordListField.SetValue(self, aics);
        }

        private static void RestoreVanillaPresentation(FRONT_Multiplayer_AISettings self)
        {
            ListView aiv = self.FindName("AIVList") as ListView;
            ListView aic = self.FindName("LordList") as ListView;
            if (aiv != null)
            {
                aiv.Margin = new Thickness(0f);
                SetColumnWidths(aiv, 40f, 320f);
            }
            if (aic != null)
            {
                aic.Margin = new Thickness(0f);
                SetColumnWidths(aic, 40f, 320f, 0f);
            }
        }

        private static void SetColumnWidths(ListView list, params float[] widths)
        {
            GridView grid = list?.View as GridView;
            if (grid == null || grid.Columns.Count != widths.Length)
                return;
            for (int i = 0; i < widths.Length; i++)
                grid.Columns[i].Width = widths[i];
        }

        private static List<CustomisationFileManager.CustomAIV> GetCanonicalAivs(
            FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return null;
            return !string.IsNullOrEmpty(info.lordName)
                ? CustomisationFileManager.Instance.getLordAIVList(-1, info.lordName)
                : CustomisationFileManager.Instance.getLordAIVList(info.lordType);
        }

        private static List<CustomisationFileManager.CustomLordConfig> GetCanonicalAics(
            FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                return null;
            return !string.IsNullOrEmpty(info.lordName)
                ? CustomisationFileManager.Instance.getLordLordList(-1, info.lordName)
                : CustomisationFileManager.Instance.getLordLordList(info.lordType);
        }

        private static FRONT_Multiplayer.MPAIVInfo GetAivInfo(FRONT_Multiplayer_AISettings self) =>
            AivInfoField.GetValue(self) as FRONT_Multiplayer.MPAIVInfo;

        private static bool ContainsIgnoreCase(string value, string query) =>
            string.IsNullOrWhiteSpace(query) ||
            (!string.IsNullOrEmpty(value) && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);

        private static FieldInfo FindField(string name) =>
            typeof(FRONT_Multiplayer_AISettings).GetField(
                name,
                BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingFieldException(typeof(FRONT_Multiplayer_AISettings).FullName, name);

        private static bool RequiresCanonicalLists(string param)
        {
            switch (param)
            {
                case "Default":
                case "Community":
                case "Historical":
                case "User":
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsListMutation(string param)
        {
            if (!string.IsNullOrEmpty(param) && param.StartsWith("Kick_", StringComparison.Ordinal))
                return true;

            switch (param)
            {
                case "Default":
                case "Community":
                case "Historical":
                case "User":
                case "Add_Selected":
                case "Replace_Selected":
                case "Clear_Selected":
                case "LordDefault":
                case "LordUser":
                    return true;
                default:
                    return false;
            }
        }

        private void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
