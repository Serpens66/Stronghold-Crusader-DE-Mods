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
    public sealed partial class ModSettingsPresentation : INotifyPropertyChanged
    {
        private bool searchActive;

        internal ModSettingsPresentation()
        {
            Topics = new[] { Units, Gatehouses, Building, Economy, Demolition, Plague, Market, Popularity };
            Groups = new[] { SoldierOptions, KnightOptions, LordOptions, MoatOptions, GateOptions, GateReopenOptions,
                GateDistanceOptions, BuildingOptions, EnemyProximityOptions, EnemySingleplayerOptions,
                EnemyMultiplayerOptions, AiRebuildOptions, PopulationOptions, KillRewardOptions,
                EconomyBonusOptions, PeriodicGoldOptions, RefundOptions, PlagueOptions, PlagueSurfacesOptions, HealerOptions, MarketGeneralOptions }.Concat(PopularityGroups).ToArray();
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
        public SettingsTopicPresentation Popularity { get; } = new SettingsTopicPresentation("Popularity", "extra.topic.popularity");
        internal IReadOnlyList<SettingsTopicPresentation> Topics { get; }
        // Group membership only controls contextual captions/rows; APIShared remains the sole search index.
        public SettingsGroupPresentation SoldierOptions { get; } = new SettingsGroupPresentation("fear-factor-neutralization", "monks-always-run");
        public SettingsGroupPresentation KnightOptions { get; } = new SettingsGroupPresentation("knight-dismount", "instant-horse", "knight-transform-gold", "knight-transform-delay");
        public SettingsGroupPresentation LordOptions { get; } = new SettingsGroupPresentation("lord-health");
        public SettingsGroupPresentation MoatOptions { get; } = new SettingsGroupPresentation("elevated-moat");
        public SettingsGroupPresentation GateOptions { get; } = new SettingsGroupPresentation("human-gate-reopen-delay", "ai-gate-reopen-delay", "human-gate-closing-distance", "ai-gate-closing-distance");
        public SettingsGroupPresentation GateReopenOptions { get; } = new SettingsGroupPresentation("human-gate-reopen-delay", "ai-gate-reopen-delay");
        public SettingsGroupPresentation GateDistanceOptions { get; } = new SettingsGroupPresentation("human-gate-closing-distance", "ai-gate-closing-distance");
        public SettingsGroupPresentation BuildingOptions { get; } = new SettingsGroupPresentation("building-repair", "keep-build-range");
        public SettingsGroupPresentation EnemyProximityOptions { get; } = new SettingsGroupPresentation("human-enemy-proximity-singleplayer", "ai-enemy-proximity-singleplayer", "human-enemy-proximity-multiplayer", "ai-enemy-proximity-multiplayer");
        public SettingsGroupPresentation EnemySingleplayerOptions { get; } = new SettingsGroupPresentation("human-enemy-proximity-singleplayer", "ai-enemy-proximity-singleplayer");
        public SettingsGroupPresentation EnemyMultiplayerOptions { get; } = new SettingsGroupPresentation("human-enemy-proximity-multiplayer", "ai-enemy-proximity-multiplayer");
        public SettingsGroupPresentation AiRebuildOptions { get; } = new SettingsGroupPresentation("ai-tower-gate-rebuild-delay");
        public SettingsGroupPresentation PopulationOptions { get; } = new SettingsGroupPresentation("extra-church-priests", "campfire-peasants");
        public SettingsGroupPresentation KillRewardOptions { get; } = new SettingsGroupPresentation("no-kill-reward");
        public SettingsGroupPresentation EconomyBonusOptions { get; } = new SettingsGroupPresentation("multiply-goods-gain", "multiply-goods-as-money");
        public SettingsGroupPresentation PeriodicGoldOptions { get; } = new SettingsGroupPresentation("periodic-gold-human", "periodic-gold-ai");
        public SettingsGroupPresentation RefundOptions { get; } = new SettingsGroupPresentation("wood-refund", "stone-refund", "iron-refund", "pitch-refund", "gold-refund");
        public SettingsGroupPresentation PlagueOptions { get; } = new SettingsGroupPresentation("plague-damage-walls", "plague-damage-gates", "plague-damage-towers", "plague-damage-keeps", "plague-duration", "apothecary-plague-search-distance");
        public SettingsGroupPresentation PlagueSurfacesOptions { get; } = new SettingsGroupPresentation("plague-damage-walls", "plague-damage-gates", "plague-damage-towers", "plague-damage-keeps");
        public SettingsGroupPresentation HealerOptions { get; } = new SettingsGroupPresentation("healer-targets");
        public SettingsGroupPresentation MarketGeneralOptions { get; } = new SettingsGroupPresentation("market-buy-price-multiplier", "market-sell-price-multiplier");
        internal IReadOnlyList<SettingsGroupPresentation> Groups { get; }
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
            IReadOnlyList<ModSettingsSearchEntry> entries,
            ModSettingsSearchStateFilter stateFilter = ModSettingsSearchStateFilter.All, object model = null)
        {
            searchActive = stateFilter != ModSettingsSearchStateFilter.All || !string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(exactKey);
            bool Matches(ModSettingsSearchEntry entry) =>
                (stateFilter == ModSettingsSearchStateFilter.All || (!entry.IsSection && ModSettingsSearchStateMatcher.IsMatch(
                    stateFilter, ModSettingsSearchCatalog.GetState(model, entry.Key)))) &&
                ModSettingsSearchMatcher.IsMatch(text, includeToolTips, exactKey, entry.Key, entry.Title, entry.ToolTip, entry.SectionKey, entry.SectionTitle);
            foreach (SettingsTopicPresentation topic in Topics)
            {
                ModSettingsSearchEntry[] section = (entries ?? Array.Empty<ModSettingsSearchEntry>())
                    .Where(entry => entry.SectionKey == topic.Key).ToArray();
                // Count the full catalog, never the filtered result or individual editor controls.
                int count = section.Where(entry => !entry.IsSection).Select(entry => entry.Key)
                    .Distinct(StringComparer.Ordinal).Count();
                bool matches = section.Any(entry => Matches(entry));
                topic.Refresh(searchActive, matches, count);
            }
            foreach (SettingsGroupPresentation group in Groups)
                group.Refresh(searchActive, (entries ?? Array.Empty<ModSettingsSearchEntry>()).Any(entry =>
                    group.Contains(entry.Key) && Matches(entry)));
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

    public sealed class SettingsGroupPresentation : INotifyPropertyChanged
    {
        private readonly HashSet<string> keys;
        private Visibility visibility = Visibility.Visible;
        internal SettingsGroupPresentation(params string[] suffixes) =>
            keys = new HashSet<string>(suffixes.Select(key => "extra." + key), StringComparer.Ordinal);
        internal bool Contains(string key) => keys.Contains(key);
        public Visibility Visibility => visibility;
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Refresh(bool searching, bool matches)
        {
            Visibility next = !searching || matches ? Visibility.Visible : Visibility.Collapsed;
            if (next == visibility) return;
            visibility = next;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Visibility)));
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
            // A table may request a minimum viewport width so the outer Auto
            // scroller includes its complete one-line content instead of clipping it.
            double minimum = double.TryParse(parameter as string, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double parsed) && !double.IsNaN(parsed) &&
                !double.IsInfinity(parsed) ? Math.Max(0.0, parsed) : 0.0;
            double available = double.IsNaN(width) || double.IsInfinity(width) ? 0.0 : Math.Max(0.0, width - 20.0);
            // Noesis Width requires a boxed Single, including before measurement.
            return (float)Math.Min(float.MaxValue, Math.Max(minimum, available));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
