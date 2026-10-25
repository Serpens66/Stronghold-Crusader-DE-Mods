using APIShared.ModSettings;
using SerpsModsHost;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.BepInEx.Bootstrap;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;

internal static class Program
{
    private static int checks;
    private const string Page = "<Grid><CheckBox Content='{Binding Title}' ToolTip='Help text'/></Grid>";
    public sealed class Settings
    {
        public string Caption = "Gate";
        public int Reads;
        public Action OnRead;
        public string Title { get { Reads++; OnRead?.Invoke(); return Caption; } }
        public object System_GetModSettingsSearchEntries(object view) => throw new Exception("Legacy export was invoked");
    }

    private static int Main(string[] args)
    {
        try
        {
            Run("explicit support and empty index", ExplicitSupport);
            Run("reentrant result updates", ReentrantResults);
            Run("reentrant filter updates", ReentrantFilters);
            Run("source replacement and stale navigation", ReplacementAndNavigation);
            Run("registration during catalog evaluation", ReentrantRegistration);
            Run("modal opening remains side-effect free", ModalOpening);
            Run("unstable consumer cannot trap the UI thread", UnstableConsumer);
            Run("invalid title survives matching and ranking", InvalidTitle);
            Run("configured state and slider neutral metadata", ConfiguredState);
            Run("global status filters and live nested changes", StatusFilters);
            Run("nested bulk updates, failures and source replacement", BulkUpdates);
            Run("leaf edits keep observers; replacements release old owners", ObserverLifetime);
            Run("reset search work scales linearly", ResetWork);
            if (args.Contains("/bench"))
                for (int pass = 0; pass < 2; pass++)
                    foreach (int count in new[] { 64, 256, 512 })
                        foreach (bool active in new[] { false, true })
                            Run("reset benchmark", path => MeasureReset(path, count, active, true));
            Console.WriteLine("PASS: " + checks + " assertions against production host/catalog sources; Noesis tree unavailable.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run(string name, Action<string> test)
    {
        // A fresh modeled process per case: detach only test-created event handlers.
        typeof(ModSettingsSearchCatalog).GetField("SourceChanged", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        typeof(ModSettingsSearchCatalog).GetField("StateChanged", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        GameXAMLManagerAPI.Instance = new GameXAMLManagerAPI();
        Plugin.ModSettingsHubViewModel = new SHCDESE.ViewModels.LobbyModSettingsHubViewModel();
        string path = Path.GetTempFileName();
        try { File.WriteAllText(path, Page); test(path); Console.WriteLine("PASS: " + name); }
        finally { File.Delete(path); }
    }

    private static LobbyModSettingsEntry Add(string name, object model)
    {
        var tab = new LobbyModSettingsEntry { Name = name, ViewModel = model };
        GameXAMLManagerAPI.Instance.RegisteredModSettings.Add(tab);
        return tab;
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void ExplicitSupport(string path)
    {
        var unknown = new Settings();
        Add("unsupported", unknown);
        var supported = new Settings();
        ModSettingsSearchCatalog.RegisterSource(supported, path, null, "independent.author");
        Add("supported", supported);
        File.WriteAllText(path, "<Grid/>");
        var empty = new Settings();
        ModSettingsSearchCatalog.RegisterSource(empty, path, null, "empty");
        Add("empty", empty);
        var host = new ModSettingsSearchViewModel(null);
        Check(host.ModFilters.Count == 3, "unsupported mod appeared in the filter");
        host.SearchText = "gate";
        Check(host.Results.Count == 0, "empty catalogs generated entries");
        File.WriteAllText(path, Page);
        host.SearchText = "gates";
        Check(supported.Reads == 0 && empty.Reads == 0 && unknown.Reads == 0, "valid empty index was rebuilt");
        ModSettingsSearchCatalog.RegisterSource(supported, path, null, "independent.author");
        host.SearchText = "gate";
        Check(host.Results.Single().ModName == "supported", "replacement was not indexed");
        Check(unknown.Reads == 0, "unsupported model was inspected");
    }

    private static void ReentrantResults(string path)
    {
        var settings = new Settings();
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign");
        Add("foreign", settings);
        var host = new ModSettingsSearchViewModel(null);
        bool entered = false;
        host.Results.CollectionChanged += (sender, args) => {
            if (entered || args.Action != NotifyCollectionChangedAction.Add) return;
            entered = true;
            host.SearchText = "missing";
            host.IncludeToolTips = true;
        };
        host.Results.CollectionChanged += (sender, args) => { };
        host.SearchText = "gate";
        Check(entered && host.Results.Count == 0, "reentrant query did not win");
        host.SearchText = "help";
        Check(host.Results.Count == 1, "tooltip change was lost");
        host.SearchText = "\uD800";
        Check(host.Results.Count == 0, "malformed query did not complete");
        host.SearchText = "";
        Check(host.Results.Count == 0, "clear did not complete");
    }

    private static void ReentrantFilters(string path)
    {
        var first = new Settings();
        ModSettingsSearchCatalog.RegisterSource(first, path, null, "first");
        Add("first", first);
        var host = new ModSettingsSearchViewModel(null);
        bool entered = false;
        host.ModFilters.CollectionChanged += (sender, args) => {
            if (entered || args.Action != NotifyCollectionChangedAction.Add) return;
            entered = true;
            host.SelectedModFilter = null;
            host.SearchText = "gate";
            GameXAMLManagerAPI.Instance.RegisteredModSettings.RemoveAt(0);
        };
        host.ModFilters.CollectionChanged += (sender, args) => { };
        ModSettingsSearchCatalog.RegisterSource(first, path, null, "first");
        Check(entered && host.ModFilters.Count == 1 && host.Results.Count == 0, "reentrant tab/filter update was lost");
    }

    private static void ReplacementAndNavigation(string path)
    {
        var settings = new Settings();
        int navigations = 0;
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign", (key, title) => { navigations++; return true; });
        var tab = Add("foreign", settings);
        var host = new ModSettingsSearchViewModel(null) { SearchText = "gate" };
        var stale = host.Results.Single();
        stale.OpenCommand.Execute();
        Check(navigations == 1 && ReferenceEquals(tab, Plugin.ModSettingsHubViewModel.SelectedTab), "supported navigation failed");
        settings.Caption = "Health";
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign", (key, title) => { navigations++; return true; });
        Check(host.Results.Count == 0, "source replacement did not refresh active query");
        stale.OpenCommand.Execute();
        Check(navigations == 1, "stale key navigated after replacement");
        host.SearchText = "health";
        var removed = host.Results.Single();
        GameXAMLManagerAPI.Instance.RegisteredModSettings.Remove(tab);
        Plugin.ModSettingsHubViewModel.SelectedTab = null;
        removed.OpenCommand.Execute();
        Check(Plugin.ModSettingsHubViewModel.SelectedTab == null && navigations == 1, "removed tab was reopened");
        Add("foreign", settings);
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign", (key, title) => throw new Exception("navigation"));
        host.Results.Single().OpenCommand.Execute();
        Check(ReferenceEquals(tab.ViewModel, Plugin.ModSettingsHubViewModel.SelectedTab.ViewModel), "failed callback prevented ordinary tab opening");
    }

    private static void ReentrantRegistration(string path)
    {
        var settings = new Settings();
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign");
        var tab = Add("foreign", settings);
        var host = new ModSettingsSearchViewModel(null);
        bool entered = false;
        settings.OnRead = () => {
            if (entered) return;
            entered = true;
            GameXAMLManagerAPI.Instance.RegisteredModSettings.Remove(tab);
        };
        host.SearchText = "gate";
        Check(entered && host.Results.Count == 0 && host.ModFilters.Count == 1, "removed tab leaked from a reentrant getter");
    }

    private static void ModalOpening(string path)
    {
        var settings = new Settings();
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "foreign");
        Add("foreign", settings);
        var host = new ModSettingsSearchViewModel(null);
        Plugin.ModSettingsHubViewModel.Open();
        Check(settings.Reads == 0, "opening the modal evaluated a catalog");
        host.SearchText = "gate";
        int reads = settings.Reads;
        Plugin.ModSettingsHubViewModel.Open();
        Check(settings.Reads == reads && Plugin.ModSettingsHubViewModel.SelectedTab == null, "reopening evaluated or selected a tab");
    }

    private static void UnstableConsumer(string path)
    {
        var settings = new Settings();
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "unstable");
        Add("unstable", settings);
        var host = new ModSettingsSearchViewModel(null);
        settings.OnRead = () => ModSettingsSearchCatalog.RegisterSource(settings, path, null, "unstable");
        int warnings = Shared.DebugLogHelper.Warnings;
        host.SearchText = "gate";
        Check(Shared.DebugLogHelper.Warnings == warnings + 1, "unstable consumer was not bounded and reported");
        settings.OnRead = null;
        host.SearchText = "gat";
        Check(host.Results.Count == 1, "search could not recover after the consumer settled");
    }

    private static void InvalidTitle(string path)
    {
        Check(ModSettingsSearchMatcher.IsSectionTitleMatch("\uFFFE", "\uFFFE"), "normalization sentinel did not match itself safely");
        Check(ModSettingsSearchMatcher.IsSectionTitleMatch("😀", "😀"), "valid astral character was changed");
        var settings = new Settings { Caption = "Gate\uD800" };
        ModSettingsSearchCatalog.RegisterSource(settings, path, null, "unicode");
        Add("unicode", settings);
        var host = new ModSettingsSearchViewModel(null) { SearchText = "gate" };
        Check(host.Results.Count == 1, "invalid UTF-16 title failed during ranking");
    }

    public sealed class StateSettings : System.ComponentModel.INotifyPropertyChanged
    {
        private System.ComponentModel.PropertyChangedEventHandler changed;
        public int Subscriptions, Unsubscriptions;
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged
        {
            add { Subscriptions++; changed += value; }
            remove { Unsubscriptions++; changed -= value; }
        }
        public bool Enabled { get; set; }
        private double number;
        public long ValueReads;
        public double Number { get { ValueReads++; return number; } set { number = value; } }
        public StateSettings Nested { get; set; }
        public double Broken => throw new InvalidOperationException("value getter");
        public StateSettings[] Items { get; set; } = Array.Empty<StateSettings>();
        public string Caption { get; set; } = "Item";
        public void Notify(string name) => changed?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
    }

    private const string StatePage = "<Grid xmlns:s='urn:search'>" +
        "<Grid s:IsSection='True' s:SectionKey='section' s:SectionTitle='Group'/>" +
        "<Grid s:Key='mixed' s:Title='Mixed' s:ToolTipText='Mixed tip' s:SectionKey='section' s:SectionTitle='Group'>" +
        "<CheckBox IsChecked='{Binding Enabled}'/><Slider Value='{Binding Number, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}'/>" +
        "<Slider Value='{Binding Broken}'/></Grid>" +
        "<Slider s:Key='factor' s:Title='Factor' ToolTip='Factor tip' Value='{Binding Nested.Number}' s:InactiveValues='1'/>" +
        "<Slider s:Key='percent' s:Title='Percent' ToolTip='Percent tip' Value='{Binding Number}' s:InactiveValues='100'/>" +
        "<Slider s:Key='bad' s:Title='Bad metadata' ToolTip='Bad' Value='{Binding Number}' s:InactiveValues='invalid'/>" +
        "<Button s:Key='button' Content='Button' ToolTip='Action'/>" +
        "<ItemsControl ItemsSource='{Binding Items}'><ItemsControl.ItemTemplate><DataTemplate>" +
        "<Grid s:Key='{Binding Caption}' s:Title='{Binding Caption}' s:ToolTipText='Item help'><Slider Value='{Binding Number}' s:InactiveValues='1;100'/></Grid>" +
        "</DataTemplate></ItemsControl.ItemTemplate></ItemsControl></Grid>";

    private static void ConfiguredState(string path)
    {
        File.WriteAllText(path, StatePage);
        var model = new StateSettings { Nested = new StateSettings { Number = 1 }, Items = new[] { new StateSettings { Number = 100 } } };
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "states");
        foreach (double value in new[] { -1d, 0d, 1d, 100d, -2d })
        {
            model.Number = value;
            var expected = value == -1 || value == 0 ? ModSettingsSearchState.Inactive : ModSettingsSearchState.Inactive | ModSettingsSearchState.Active;
            Check(ModSettingsSearchCatalog.GetState(model, "mixed") == expected, "mixed state failed at " + value);
        }
        Check(ModSettingsSearchCatalog.GetState(model, "factor") == ModSettingsSearchState.Inactive, "factor 1 was not neutral");
        model.Nested.Number = 0;
        Check(ModSettingsSearchCatalog.GetState(model, "factor") == ModSettingsSearchState.Active, "explicit neutral values did not replace defaults");
        model.Number = 100;
        Check(ModSettingsSearchCatalog.GetState(model, "percent") == ModSettingsSearchState.Inactive, "100 percent was not neutral");
        Check(ModSettingsSearchCatalog.GetState(model, "Item") == ModSettingsSearchState.Inactive, "template item or multiple neutral values failed");
        Check(ModSettingsSearchCatalog.GetState(model, "bad") == ModSettingsSearchState.Unknown &&
            ModSettingsSearchCatalog.GetState(model, "button") == ModSettingsSearchState.Unknown &&
            ModSettingsSearchCatalog.GetState(model, "missing") == ModSettingsSearchState.Unknown, "unknown state was guessed");
        model.Enabled = true; model.Number = double.NaN;
        Check(ModSettingsSearchCatalog.GetState(model, "mixed") == ModSettingsSearchState.Active, "NaN poisoned checkbox state");
        model.Enabled = false; model.Number = double.PositiveInfinity;
        Check(ModSettingsSearchCatalog.GetState(model, "mixed") == ModSettingsSearchState.Inactive, "infinite value was classified");
    }

    private static void StatusFilters(string path)
    {
        File.WriteAllText(path, StatePage);
        var model = new StateSettings { Number = 1, Nested = new StateSettings { Number = 1 } };
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "states");
        Add("states", model);
        var host = new ModSettingsSearchViewModel(null);
        host.StateFilter = ModSettingsSearchStateFilter.Active;
        Check(host.Results.Any(r => r.Entry.Key == "mixed") && !host.Results.Any(r => r.Entry.Key == "factor"), "empty-text active filter failed");
        host.StateFilter = ModSettingsSearchStateFilter.Inactive;
        Check(host.Results.Any(r => r.Entry.Key == "mixed") && host.Results.Any(r => r.Entry.Key == "factor"), "mixed row did not appear in both filters");
        host.SearchText = "group";
        Check(host.Results.Single().Entry.Key == "mixed", "section hit suppressed the classified child");
        host.SearchText = "";
        model.Nested.Number = 2; model.Nested.Notify(nameof(StateSettings.Number));
        Check(!host.Results.Any(r => r.Entry.Key == "factor"), "nested live value did not update results");
        var previous = model.Nested;
        model.Nested = new StateSettings { Number = 1 }; model.Notify(nameof(StateSettings.Nested));
        Check(host.Results.Any(r => r.Entry.Key == "factor"), "replaced nested model was not observed");
        int notifications = 0;
        ModSettingsSearchCatalog.StateChanged += _ => notifications++;
        previous.Notify(nameof(StateSettings.Number));
        Check(notifications == 0, "old nested model retained an observer");
        model.Nested.Number = 3;
        var thread = new System.Threading.Thread(() => model.Nested.Notify(nameof(StateSettings.Number)));
        thread.Start(); thread.Join();
        Check(notifications == 0, "off-thread notification reached UI synchronously");
        APIShared.Internal.UnityMainThreadDispatch.Drain();
        Check(notifications == 1 && !host.Results.Any(r => r.Entry.Key == "factor"), "queued UI state update failed");
        host.SearchText = "factor";
        Check(host.Results.Count == 0, "text and status were not combined");
        host.StateFilter = ModSettingsSearchStateFilter.Active;
        Check(host.Results.Single().Entry.Key == "factor", "combined active filter failed");
        host.ClearSearchCommand.Execute();
        Check(host.StateFilter == ModSettingsSearchStateFilter.All && host.SearchText == "" && host.Results.Count == 0, "clear did not restore original empty-search behavior");
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "replacement");
        model.Nested.Notify(nameof(StateSettings.Number));
        Check(notifications == 1, "replaced catalog retained old value subscriptions");
    }

    private static void BulkUpdates(string path)
    {
        File.WriteAllText(path, StatePage);
        var model = new StateSettings { Number = 2, Nested = new StateSettings { Number = 2 } };
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "batch");
        ModSettingsSearchCatalog.GetState(model, "factor");
        int count = 0;
        ModSettingsSearchCatalog.StateChanged += owner => { if (ReferenceEquals(owner, model)) count++; };
        using (ModSettingsSearchUpdateScope.Begin(model))
        {
            model.Nested.Number = 1; model.Nested.Notify(nameof(StateSettings.Number));
            using (ModSettingsSearchUpdateScope.Begin(model))
            {
                model.Nested.Number = 0; model.Nested.Notify(nameof(StateSettings.Number));
            }
            Check(count == 0, "nested batch published an intermediate state");
            Check(ModSettingsSearchCatalog.GetState(model, "factor") == ModSettingsSearchState.Active, "explicit read returned a stale batched value");
        }
        Check(count == 1, "outer batch must publish exactly once");
        using (ModSettingsSearchUpdateScope.Begin(model)) { }
        Check(count == 1, "unchanged batch published a state event");
        try
        {
            using (ModSettingsSearchUpdateScope.Begin(model))
            {
                model.Nested.Number = 1; model.Nested.Notify(nameof(StateSettings.Number));
                throw new InvalidOperationException("fixture failure");
            }
        }
        catch (InvalidOperationException) { }
        Check(count == 2 && ModSettingsSearchCatalog.GetState(model, "factor") == ModSettingsSearchState.Inactive, "failed operation left refreshes suppressed");
        using (ModSettingsSearchUpdateScope.Begin(model))
        {
            model.Nested.Notify(nameof(StateSettings.Number));
            ModSettingsSearchCatalog.RegisterSource(model, path, null, "replaced");
            ModSettingsSearchCatalog.GetState(model, "factor");
            model.Nested.Number = 2; model.Nested.Notify(nameof(StateSettings.Number));
        }
        Check(count == 3, "retired source published after a replacement inside a batch");
        model.Nested.Number = 1; model.Nested.Notify(nameof(StateSettings.Number));
        Check(count == 4, "ordinary single edits stopped publishing synchronously");
    }

    private static void ResetWork(string path)
    {
        foreach (int count in new[] { 64, 256, 512 })
            foreach (bool active in new[] { false, true }) MeasureReset(path, count, active, false);
    }

    private static void ObserverLifetime(string path)
    {
        File.WriteAllText(path, StatePage);
        var old = new StateSettings { Number = 2 };
        var model = new StateSettings { Nested = old };
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "lifetime");
        ModSettingsSearchCatalog.GetState(model, "factor");
        int added = old.Subscriptions, removed = old.Unsubscriptions;
        old.ValueReads = 0;
        for (int index = 0; index < 10; index++) { old.Number = index; old.Notify(nameof(StateSettings.Number)); }
        Check(old.Subscriptions == added && old.Unsubscriptions == removed, "leaf changes rebuilt their observers");
        Check(old.ValueReads == 0, "leaf notifications must not eagerly read values with no presentation demand");
        model.Nested = new StateSettings { Number = 1 };
        model.Notify(nameof(StateSettings.Nested));
        Check(old.Subscriptions == old.Unsubscriptions, "replaced nested owner retained its subscription");
        Check(model.Nested.Subscriptions > 0, "replacement owner was not observed until another search action");
        Check(ModSettingsSearchCatalog.GetState(model, "factor") == ModSettingsSearchState.Inactive, "replacement value was stale");
    }

    private static void MeasureReset(string path, int size, bool active, bool report)
    {
        File.WriteAllText(path, StatePage);
        var model = new StateSettings { Nested = new StateSettings { Number = 2 },
            Items = Enumerable.Range(0, size).Select(index => new StateSettings { Caption = "Item " + index, Number = 2 }).ToArray() };
        ModSettingsSearchCatalog.RegisterSource(model, path, null, "reset");
        var tab = Add("reset", model);
        var host = new ModSettingsSearchViewModel(null) { StateFilter = ModSettingsSearchStateFilter.Active };
        if (!active) host.StateFilter = ModSettingsSearchStateFilter.All;
        model.ValueReads = 0; model.Nested.ValueReads = 0;
        foreach (var item in model.Items) item.ValueReads = 0;
        int publications = 0;
        Action<object> listener = owner => { if (ReferenceEquals(owner, model)) publications++; };
        ModSettingsSearchCatalog.StateChanged += listener;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using (ModSettingsSearchUpdateScope.Begin(model))
        {
            foreach (var item in model.Items) { item.Number = 1; item.Notify(nameof(StateSettings.Number)); }
        }
        watch.Stop();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        long reads = model.ValueReads + model.Nested.ValueReads + model.Items.Sum(item => item.ValueReads);
        Check(publications == 1, "one reset must produce one status publication");
        Check(active ? reads <= size + 12 : reads == 0, "reset reread unrelated values or scaled quadratically");
        if (active) Check(!host.Results.Any(result => result.Entry.Key.StartsWith("Item ", StringComparison.Ordinal)), "reset left stale active item results");
        if (report) Console.WriteLine($"BENCH N={size} active={active} ms={watch.Elapsed.TotalMilliseconds:F3} reads={reads} allocatedBytes={allocated}");
        ModSettingsSearchCatalog.StateChanged -= listener;
        host.StateFilter = ModSettingsSearchStateFilter.All;
        GameXAMLManagerAPI.Instance.RegisteredModSettings.Remove(tab);
    }
}
