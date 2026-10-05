using ExtendedData.Core;
using Noesis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using SHCDESE.NoesisUtil;
namespace ExtendedData
{
    internal sealed class TrailSettingGroupDefinition
    {
        internal TrailSettingGroupDefinition(string key, string displayName, string[] propertyNames)
        {
            Key = key;
            DisplayName = displayName;
            PropertyNames = propertyNames ?? Array.Empty<string>();
        }

        internal string Key { get; }
        internal string DisplayName { get; }
        internal string[] PropertyNames { get; }
    }

    public sealed class TrailModSelectionItem : INotifyPropertyChanged
    {
        private readonly Action<string, IEnumerable<string>, TrailSettingMode> changed;
        private readonly Func<string, string, TrailSettingMode> getMode;
        private readonly DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem> page;
        private readonly Action<string> diagnostics;
        internal TrailModSelectionItem(string modId, string displayName, IEnumerable<TrailSettingGroupDefinition> definitions,
            Func<string, string, TrailSettingMode> getMode, Action<string, IEnumerable<string>, TrailSettingMode> changed,
            string helpText, Action<string> diagnostics = null)
        {
            ModId = modId; DisplayName = displayName; HelpText = helpText;
            this.changed = changed; this.getMode = getMode; this.diagnostics = diagnostics;
            ModeOptions = CreateModeOptions(includeMixed: true);
            page = new DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem>(definitions,
                (d, term) => d.DisplayName.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 || d.PropertyNames.Any(x => x.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0),
                d => new TrailSettingSelectionItem(modId, d.Key, d.DisplayName, d.PropertyNames, getMode, changed, OnSettingChanged, helpText));
            SearchText = string.Join(" ", new[] { displayName }.Concat(page.Definitions.Select(x => x.DisplayName)));
            PreviousPageCommand = new ActionCommand(() => { page.Page--; RefreshPage(); });
            NextPageCommand = new ActionCommand(() => { page.Page++; RefreshPage(); });
        }
        internal void RestorePresentation(TrailModSelectionItem previous)
        {
            page.Filter = previous.page.Filter; page.Page = previous.page.Page; page.Expanded = previous.page.Expanded;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string ModId { get; }
        public string DisplayName { get; }
        public string HelpText { get; }
        public string SearchText { get; }
        public ComboBoxItem[] ModeOptions { get; }
        internal IReadOnlyList<TrailSettingGroupDefinition> Definitions => page.Definitions;
        public TrailSettingSelectionItem[] Settings
        {
            get
            {
                var watch = Stopwatch.StartNew(); int before = page.CreatedRows;
                var rows = page.Rows;
                if (page.CreatedRows != before) diagnostics?.Invoke("[PresetPerf] visible rows: mod=" + ModId + ", created=" + (page.CreatedRows - before) + ", total=" + page.Count + ", ms=" + watch.Elapsed.TotalMilliseconds.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                return rows;
            }
        }
        public bool IsExpanded { get => page.Expanded; set { if (page.Expanded == value) return; page.Expanded = value; RefreshPage(); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); } }
        public string Filter { get => page.Filter; set { page.Filter = value; RefreshPage(); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter))); } }
        public Visibility PagingVisibility => IsExpanded && page.Count > DeferredSettingsPage<TrailSettingGroupDefinition, TrailSettingSelectionItem>.PageSize ? Visibility.Visible : Visibility.Collapsed;
        public string PageText => (page.Page + 1) + " / " + page.PageCount + " · " + page.FilteredCount;
        public string FilterHelp => SerpLocalization.Get("ExtendedData.Options.Filter");
        public string PreviousHelp => SerpLocalization.Get("ExtendedData.Options.Previous");
        public string NextHelp => SerpLocalization.Get("ExtendedData.Options.Next");
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand { get; }
        private void RefreshPage()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Settings)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PagingVisibility)));
        }
        public int SelectedModeIndex
        {
            get
            {
                int first = -1;
                foreach (var definition in page.Definitions)
                    foreach (string property in definition.PropertyNames)
                    {
                        int current = (int)getMode(ModId, property);
                        if (first < 0) first = current; else if (first != current) return 3;
                    }
                return first < 0 ? 3 : first;
            }
            set
            {
                if (value >= 0 && value <= (int)TrailSettingMode.Fixed)
                    changed?.Invoke(ModId, page.Definitions.SelectMany(x => x.PropertyNames), (TrailSettingMode)value);
            }
        }
        internal void RefreshState()
        {
            foreach (var setting in page.MaterializedRows) setting.RefreshState();
            OnSettingChanged();
        }
        private void OnSettingChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
        internal static ComboBoxItem[] CreateModeOptions(bool includeMixed)
        {
            var options = new List<ComboBoxItem>
            {
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.ModDefault") },
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.Player") },
                new ComboBoxItem { Content = SerpLocalization.Get("ExtendedData.Mode.Fixed") },
            };
            if (includeMixed)
            {
                options.Add(new ComboBoxItem
                {
                    Content = SerpLocalization.Get("ExtendedData.Mode.Mixed"),
                    IsEnabled = false,
                });
            }
            return options.ToArray();
        }
    }

    public sealed class TrailSettingSelectionItem : INotifyPropertyChanged
    {
        private readonly string modId;
        private readonly Func<string, string, TrailSettingMode> getMode;
        private readonly Action<string, IEnumerable<string>, TrailSettingMode> changed;
        private readonly Action parentChanged;

        internal TrailSettingSelectionItem(
            string modId,
            string key,
            string displayName,
            string[] propertyNames,
            Func<string, string, TrailSettingMode> getMode,
            Action<string, IEnumerable<string>, TrailSettingMode> changed,
            Action parentChanged,
            string helpText)
        {
            this.modId = modId;
            this.getMode = getMode;
            this.changed = changed;
            this.parentChanged = parentChanged;
            Key = key;
            DisplayName = displayName;
            PropertyNames = propertyNames ?? Array.Empty<string>();
            HelpText = helpText;
            ModeOptions = TrailModSelectionItem.CreateModeOptions(includeMixed: true);
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public string Key { get; }
        public string DisplayName { get; }
        public string HelpText { get; }
        public string[] PropertyNames { get; }
        public ComboBoxItem[] ModeOptions { get; }

        public int SelectedModeIndex
        {
            get
            {
                TrailSettingMode[] modes = PropertyNames
                    .Select(propertyName => getMode(modId, propertyName))
                    .Distinct()
                    .ToArray();
                return modes.Length == 1 ? (int)modes[0] : 3;
            }
            set
            {
                if (value < 0 || value > (int)TrailSettingMode.Fixed)
                    return;
                changed?.Invoke(modId, PropertyNames, (TrailSettingMode)value);
                parentChanged?.Invoke();
            }
        }

        internal void RefreshState() =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedModeIndex)));
    }

    internal sealed class ActionCommand : ICommand
    {
        private readonly Action execute;
        public ActionCommand(Action execute) => this.execute = execute;
        public event System.EventHandler CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute();
    }
}
