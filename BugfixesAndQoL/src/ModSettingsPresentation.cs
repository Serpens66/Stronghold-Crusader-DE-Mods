using APIShared.ModSettings;
using Noesis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace BugfixesAndQoL
{
    // View state only. No setting setters, preset attributes, persistence or simulation callbacks.
    public sealed partial class ModSettingsPresentation : INotifyPropertyChanged
    {
        private readonly Dictionary<string, SettingsOptionPresentation> options =
            new Dictionary<string, SettingsOptionPresentation>(StringComparer.Ordinal);
        private readonly List<SettingsTopicPresentation> topics = new List<SettingsTopicPresentation>();
        private readonly Action cancelHotkeyCapture;
        private bool searchActive;

        internal ModSettingsPresentation(Action cancelHotkeyCapture = null)
        {
            this.cancelHotkeyCapture = cancelHotkeyCapture;
            CreateTopics();
            ApplyApprovedRecommendations();
            ExpandClientCommand = new SettingsViewCommand(() => SetAll(false, true));
            CollapseClientCommand = new SettingsViewCommand(() => SetAll(false, false));
            ExpandHostCommand = new SettingsViewCommand(() => SetAll(true, true));
            CollapseHostCommand = new SettingsViewCommand(() => SetAll(true, false));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public ICommand ExpandClientCommand { get; }
        public ICommand CollapseClientCommand { get; }
        public ICommand ExpandHostCommand { get; }
        public ICommand CollapseHostCommand { get; }
        public Visibility BulkActionsVisibility => searchActive ? Visibility.Collapsed : Visibility.Visible;
        public string LegendRecommendedText => Text("LegendRecommended");
        public string LegendPreferenceText => Text("LegendPreference");
        public string LegendUnreviewedText => Text("LegendUnreviewed");
        public string ExpandAllText => Text("ExpandAll");
        public string CollapseAllText => Text("CollapseAll");
        public string ExpandAllHelpText => Text("ExpandAllHelp");
        public string CollapseAllHelpText => Text("CollapseAllHelp");
        public string TopicToggleHelpText => Text("TopicToggleHelp");
        public string PerformanceHelpText => Text("PerformanceHeavyHelp");
        public string PossibleFixesHelpText => Text("PossibleFixesHelp");

        internal IReadOnlyList<SettingsTopicPresentation> Topics => topics;
        internal IEnumerable<SettingsOptionPresentation> Options => options.Values;
        internal static string Text(string suffix) => SerpLocalization.Get("BugfixesAndQoL.SettingsRework." + suffix);

        internal void RefreshLocalization()
        {
            foreach (SettingsOptionPresentation option in options.Values) option.RefreshLocalization();
        }

        private SettingsTopicPresentation AddTopic(string id, string key, bool host)
        {
            var topic = new SettingsTopicPresentation(id, key, host, () => searchActive, OnTopicClosed);
            topics.Add(topic);
            return topic;
        }

        private SettingsOptionPresentation AddOption(string property, string key, SettingsCategory category,
            SettingsTopicPresentation topic)
        {
            var option = new SettingsOptionPresentation(property, key, category);
            options.Add(property, option);
            topic.Add(option);
            return option;
        }

        // Called on the UI thread by filter changes and the catalog's SourceChanged event.
        // Metadata and matching remain owned by APIShared, including DataTemplate item snapshots.
        internal void RefreshSearch(string text, bool includeToolTips, string exactKey,
            IReadOnlyList<ModSettingsSearchEntry> entries)
        {
            searchActive = !string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(exactKey);
            foreach (SettingsTopicPresentation topic in topics)
            {
                IEnumerable<ModSettingsSearchEntry> matches = (entries ?? Array.Empty<ModSettingsSearchEntry>())
                    .Where(entry => entry.SectionKey == topic.Key && ModSettingsSearchMatcher.IsMatch(
                        text, includeToolTips, exactKey, entry.Key, entry.Title, entry.ToolTip,
                        entry.SectionKey, entry.SectionTitle));
                topic.Refresh(searchActive, matches);
            }
            // If filtering hides the hotkey editor, stop consuming unrelated keyboard input.
            if (ClientDisplay.ContentVisibility != Visibility.Visible) cancelHotkeyCapture?.Invoke();
            foreach (string property in new[] { nameof(BulkActionsVisibility), nameof(LegendRecommendedText),
                nameof(LegendPreferenceText), nameof(LegendUnreviewedText), nameof(ExpandAllText), nameof(CollapseAllText),
                nameof(ExpandAllHelpText), nameof(CollapseAllHelpText), nameof(TopicToggleHelpText),
                nameof(PerformanceHelpText), nameof(PossibleFixesHelpText) })
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }

        private void SetAll(bool host, bool expanded)
        {
            if (searchActive) return;
            foreach (SettingsTopicPresentation topic in topics)
                if (topic.IsHost == host) topic.SetExpanded(expanded);
        }

        private void OnTopicClosed(SettingsTopicPresentation topic)
        {
            if (ReferenceEquals(topic, ClientDisplay)) cancelHotkeyCapture?.Invoke();
        }
    }

    internal enum SettingsCategory { Fixes, Qol, Possible }
    internal enum SettingsRecommendationKind { Unreviewed, Recommended, Preference }

    public sealed class SettingsOptionPresentation : INotifyPropertyChanged
    {
        internal SettingsOptionPresentation(string property, string key, SettingsCategory category)
        {
            PropertyName = property;
            Key = key;
            Category = category;
        }

        public string PropertyName { get; }
        public string Key { get; }
        internal SettingsCategory Category { get; }
        public event PropertyChangedEventHandler PropertyChanged;
        // Editorial metadata only; the approved table is keyed by stable PropertyName.
        internal SettingsRecommendationKind Recommendation { get; private set; }
        internal string ReasonKey { get; private set; }
        public string Symbol => Recommendation == SettingsRecommendationKind.Recommended ? "★" :
            Recommendation == SettingsRecommendationKind.Preference ? "◇" : string.Empty;
        public bool IsPreference => Recommendation == SettingsRecommendationKind.Preference;
        public string ToolTip => FormatToolTip(SerpLocalization.Get);

        internal string FormatToolTip(Func<string, string> localize) =>
            Recommendation == SettingsRecommendationKind.Unreviewed ? null :
                localize("BugfixesAndQoL.SettingsRework." + (IsPreference ? "Preference" : "Recommended")) +
                ": " + localize(ReasonKey);

        internal void SetApprovedRecommendation(SettingsRecommendationKind kind, string reasonLocaleKey)
        {
            if (kind != SettingsRecommendationKind.Unreviewed && kind != SettingsRecommendationKind.Recommended &&
                kind != SettingsRecommendationKind.Preference) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind != SettingsRecommendationKind.Unreviewed && string.IsNullOrWhiteSpace(reasonLocaleKey))
                throw new ArgumentException("An approved recommendation needs a reason locale key.", nameof(reasonLocaleKey));
            Recommendation = kind;
            ReasonKey = reasonLocaleKey;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Symbol)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPreference)));
            RefreshLocalization();
        }

        internal void RefreshLocalization() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolTip)));
    }

    public sealed class SettingsTopicPresentation : INotifyPropertyChanged
    {
        private readonly Dictionary<string, SettingsOptionPresentation> options =
            new Dictionary<string, SettingsOptionPresentation>(StringComparer.Ordinal);
        private readonly Func<bool> isSearching;
        private readonly Action<SettingsTopicPresentation> onClosed;
        private bool manualExpanded, activeSearch, hasMatches = true;
        private bool fixes, qol, possible;

        internal SettingsTopicPresentation(string id, string key, bool host,
            Func<bool> isSearching, Action<SettingsTopicPresentation> onClosed)
        {
            Id = id; Key = key; IsHost = host;
            this.isSearching = isSearching; this.onClosed = onClosed;
            ToggleCommand = new SettingsViewCommand(() => SetExpanded(!manualExpanded));
        }

        public event PropertyChangedEventHandler PropertyChanged;
        internal string Id { get; }
        public string Key { get; }
        public bool IsHost { get; }
        public string Title => ModSettingsPresentation.Text("Topic." + Id);
        public int OptionCount => options.Count;
        public string CountText => OptionCount.ToString(CultureInfo.InvariantCulture);
        public bool IsExpanded => ContentVisibility == Visibility.Visible;
        public ICommand ToggleCommand { get; }
        public Visibility Visibility => hasMatches ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ContentVisibility => hasMatches && (activeSearch || manualExpanded)
            ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FixesVisibility => fixes ? Visibility.Visible : Visibility.Collapsed;
        public Visibility QolVisibility => qol ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PossibleVisibility => possible ? Visibility.Visible : Visibility.Collapsed;

        internal void Add(SettingsOptionPresentation option)
        {
            options.Add(option.Key, option);
            fixes |= option.Category == SettingsCategory.Fixes;
            qol |= option.Category == SettingsCategory.Qol;
            possible |= option.Category == SettingsCategory.Possible;
        }

        internal void SetExpanded(bool expanded)
        {
            // Search expansion is transient and must never overwrite the manual session state.
            if (isSearching() || manualExpanded == expanded) return;
            manualExpanded = expanded;
            if (!expanded) onClosed?.Invoke(this);
            Notify();
        }

        internal void Refresh(bool searching, IEnumerable<ModSettingsSearchEntry> matches)
        {
            activeSearch = searching;
            hasMatches = !searching;
            fixes = qol = possible = false;
            if (!searching)
            {
                foreach (SettingsOptionPresentation option in options.Values) ShowCategory(option.Category);
            }
            else
            {
                foreach (ModSettingsSearchEntry entry in matches)
                {
                    hasMatches = true;
                    if (entry.IsSection) continue;
                    // Auxiliary controls (goods and blacklist actions) belong to the theme's QoL group.
                    ShowCategory(options.TryGetValue(entry.Key, out SettingsOptionPresentation option)
                        ? option.Category : SettingsCategory.Qol);
                }
            }
            Notify();
        }

        private void ShowCategory(SettingsCategory category)
        {
            if (category == SettingsCategory.Fixes) fixes = true;
            else if (category == SettingsCategory.Qol) qol = true;
            else possible = true;
        }

        private void Notify()
        {
            foreach (string property in new[] { nameof(Visibility), nameof(ContentVisibility), nameof(IsExpanded),
                nameof(FixesVisibility), nameof(QolVisibility), nameof(PossibleVisibility), nameof(Title), nameof(CountText) })
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

    // Horizontal Auto measures content with unbounded width. Bound topic rows to the
    // visible viewport so labels and the goods WrapPanel wrap, while the outer viewer
    // can still scroll wider preset editors. The page has a 10-unit margin on each side.
    public sealed class SettingsTopicWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double width = value is double number ? number : value is float single ? single : 0;
            // Noesis requires a boxed Single for Width and rejects a boxed Double.
            return double.IsNaN(width) || double.IsInfinity(width) ? 0f
                : (float)Math.Min(float.MaxValue, Math.Max(0.0, width - 20.0));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
