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

    private static int Main()
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
            Console.WriteLine("PASS: " + checks + " assertions against production host/catalog sources; Noesis tree unavailable.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Run(string name, Action<string> test)
    {
        // A fresh modeled process per case: detach only test-created event handlers.
        typeof(ModSettingsSearchCatalog).GetField("SourceChanged", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
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
}
