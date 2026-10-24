using APIShared.ModSettings;
using Noesis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace ExtraFeatures
{
    // Session-only view state. Settings, persistence and search metadata remain with their owners.
    public sealed class ModSettingsPresentation : INotifyPropertyChanged
    {
        private bool searchActive;

        internal ModSettingsPresentation()
        {
            Topics = new[] { Units, Gatehouses, Building, Economy, Demolition, Plague, Market };
            ExpandAllCommand = new SettingsViewCommand(() => SetAll(true));
            CollapseAllCommand = new SettingsViewCommand(() => SetAll(false));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public SettingsTopicPresentation Units { get; } = new SettingsTopicPresentation("Units", "extra.topic.units");
        public SettingsTopicPresentation Gatehouses { get; } = new SettingsTopicPresentation("Gatehouses", "extra.topic.gatehouses");
        public SettingsTopicPresentation Building { get; } = new SettingsTopicPresentation("Building", "extra.topic.building");
        public SettingsTopicPresentation Economy { get; } = new SettingsTopicPresentation("Economy", "extra.topic.economy");
        public SettingsTopicPresentation Demolition { get; } = new SettingsTopicPresentation("Demolition", "extra.topic.demolition");
        public SettingsTopicPresentation Plague { get; } = new SettingsTopicPresentation("Plague", "extra.topic.plague");
        public SettingsTopicPresentation Market { get; } = new SettingsTopicPresentation("Market", "extra.topic.market");
        internal IReadOnlyList<SettingsTopicPresentation> Topics { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public Visibility BulkActionsVisibility => searchActive ? Visibility.Collapsed : Visibility.Visible;
        public string ExpandAllText => Text("ExpandAll");
        public string CollapseAllText => Text("CollapseAll");
        public string ExpandAllHelpText => Text("ExpandAllHelp");
        public string CollapseAllHelpText => Text("CollapseAllHelp");
        public string TopicToggleHelpText => Text("TopicToggleHelp");
        internal static string Text(string suffix) => SerpLocalization.Get("ExtraFeatures.SettingsTopics." + suffix);

        // Both the preset search properties and SourceChanged are published on the Unity UI thread.
        internal void RefreshSearch(string text, bool includeToolTips, string exactKey,
            IReadOnlyList<ModSettingsSearchEntry> entries)
        {
            searchActive = !string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(exactKey);
            foreach (SettingsTopicPresentation topic in Topics)
            {
                ModSettingsSearchEntry[] section = (entries ?? Array.Empty<ModSettingsSearchEntry>())
                    .Where(entry => entry.SectionKey == topic.Key).ToArray();
                // Count the full catalog, never the filtered result or individual editor controls.
                int count = section.Where(entry => !entry.IsSection).Select(entry => entry.Key)
                    .Distinct(StringComparer.Ordinal).Count();
                bool matches = section.Any(entry => ModSettingsSearchMatcher.IsMatch(
                    text, includeToolTips, exactKey, entry.Key, entry.Title, entry.ToolTip,
                    entry.SectionKey, entry.SectionTitle));
                topic.Refresh(searchActive, matches, count);
            }
            foreach (string property in new[] { nameof(BulkActionsVisibility), nameof(ExpandAllText),
                nameof(CollapseAllText), nameof(ExpandAllHelpText), nameof(CollapseAllHelpText), nameof(TopicToggleHelpText) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        private void SetAll(bool expanded)
        {
            if (searchActive) return;
            foreach (SettingsTopicPresentation topic in Topics) topic.SetExpanded(expanded);
        }
    }

    public sealed class SettingsTopicPresentation : INotifyPropertyChanged
    {
        private readonly string id;
        private bool manualExpanded, searchActive, hasMatches = true;
        private int optionCount;

        internal SettingsTopicPresentation(string id, string key)
        {
            this.id = id;
            Key = key;
            ToggleCommand = new SettingsViewCommand(() => SetExpanded(!manualExpanded));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public string Key { get; }
        public string Title => ModSettingsPresentation.Text("Topic." + id);
        public int OptionCount => optionCount;
        public string CountText => optionCount.ToString(CultureInfo.InvariantCulture);
        public bool IsExpanded => ContentVisibility == Visibility.Visible;
        public Visibility Visibility => !searchActive || hasMatches ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ContentVisibility => (!searchActive && manualExpanded) || (searchActive && hasMatches)
            ? Visibility.Visible : Visibility.Collapsed;
        public ICommand ToggleCommand { get; }

        internal void SetExpanded(bool expanded)
        {
            // Search expansion must not overwrite the user's manual session state.
            if (searchActive || manualExpanded == expanded) return;
            manualExpanded = expanded;
            Notify();
        }

        internal void Refresh(bool searching, bool matches, int count)
        {
            searchActive = searching;
            hasMatches = matches;
            optionCount = count;
            Notify();
        }

        private void Notify()
        {
            foreach (string property in new[] { nameof(Visibility), nameof(ContentVisibility), nameof(IsExpanded),
                nameof(Title), nameof(OptionCount), nameof(CountText) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }

    internal sealed class SettingsViewCommand : ICommand
    {
        private readonly Action execute;
        internal SettingsViewCommand(Action execute) => this.execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => execute();
        public event System.EventHandler CanExecuteChanged { add { } remove { } }
    }

    // Horizontal Auto measures content with unbounded width; constrain topics to the viewport.
    public sealed class SettingsTopicWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double width = value is double number ? number : value is float single ? single : 0;
            // Noesis Width requires a boxed Single, including during the initial unmeasured layout.
            return double.IsNaN(width) || double.IsInfinity(width) ? 0f
                : (float)Math.Min(float.MaxValue, Math.Max(0.0, width - 20.0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
