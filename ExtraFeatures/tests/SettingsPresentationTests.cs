using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Xml;

// Runs the compiled production presentation and APIShared catalog, without constructing a Noesis view.
// Topic membership is the agreed UI specification; it is independent of private method/source layout.
public static class SettingsPresentationTests
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static int checks;
    private static readonly Dictionary<string, string[]> Membership = new Dictionary<string, string[]>
    {
        { "Units", new[] { "lord-health", "fear-factor-neutralization", "monks-always-run", "knight-dismount", "instant-horse", "knight-transform-gold", "knight-transform-delay" } },
        { "Gatehouses", new[] { "human-gate-reopen-delay", "ai-gate-reopen-delay", "human-gate-closing-distance", "ai-gate-closing-distance", "elevated-moat" } },
        { "Building", new[] { "building-repair", "keep-build-range", "human-enemy-proximity-singleplayer", "human-enemy-proximity-multiplayer", "ai-enemy-proximity-singleplayer", "ai-enemy-proximity-multiplayer", "ai-tower-gate-rebuild-delay" } },
        { "Economy", new[] { "campfire-peasants", "extra-church-priests", "multiply-goods-gain", "multiply-goods-as-money", "no-kill-reward" } },
        { "Demolition", new[] { "wood-refund", "stone-refund", "iron-refund", "pitch-refund", "gold-refund", "keep-storage-content" } },
        { "Plague", new[] { "plague-damage-walls", "plague-damage-gates", "plague-damage-towers", "plague-damage-keeps", "plague-duration", "apothecary-plague-search-distance", "healer-targets" } },
        { "Market", new[] { "market-buy-price-multiplier", "market-sell-price-multiplier", "market-prices-also-ai" } }
    };

    private static object Get(object instance, string property) { return instance.GetType().GetProperty(property, All).GetValue(instance, null); }
    private static bool Visible(object instance, string property) { return Get(instance, property).ToString() == "Visible"; }
    private static void Execute(object instance, string command)
    {
        object value = Get(instance, command);
        value.GetType().GetMethod("Execute", All).Invoke(value, new object[] { null });
    }
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Settings presentation: " + message);
    }
    private static string Search(XmlNode node, string name)
    {
        XmlAttribute attribute = node.Attributes == null ? null : node.Attributes.Cast<XmlAttribute>()
            .FirstOrDefault(a => a.LocalName == "ModSettingsSearch." + name);
        return attribute == null ? null : attribute.Value;
    }
    private static IEnumerable<XmlNode> Ancestors(XmlNode node)
    {
        for (XmlNode parent = node.ParentNode; parent != null; parent = parent.ParentNode) yield return parent;
    }
    private static bool HostGuard(XmlNode node)
    {
        return node.Attributes != null && node.Attributes["IsEnabled"] != null &&
            node.Attributes["IsEnabled"].Value == "{Binding CanEditHostSettings}";
    }

    private static void ValidateXaml(XmlDocument document)
    {
        XmlNode[] nodes = document.SelectNodes("//*").Cast<XmlNode>().ToArray();
        XmlNode[] topics = nodes.Where(n => Search(n, "IsSection") == "True").ToArray();
        Check(topics.Length == Membership.Count, "seven topic sections");
        foreach (KeyValuePair<string, string[]> pair in Membership)
        {
            string prefix = "SettingsPresentation." + pair.Key;
            XmlNode topic = topics.Single(n => Search(n, "SectionTitle") == "{Binding " + prefix + ".Title}");
            Check(!HostGuard(topic) && !Ancestors(topic).Any(HostGuard), "clients can operate topic headings: " + pair.Key);
            Check(topic.Attributes["Width"].Value.Contains("ViewportWidth, ElementName=SettingsViewport"), "finite viewport width: " + pair.Key);
            XmlNode heading = topic.SelectNodes("./*[local-name()='Button']").Cast<XmlNode>().Single();
            Check(!HostGuard(heading) && Search(heading, "Exclude") == "True" &&
                heading.Attributes["Command"].Value == "{Binding " + prefix + ".ToggleCommand}", "topic command outside host guard");
            string[] keys = topic.SelectNodes(".//*").Cast<XmlNode>().Select(n => Search(n, "Key"))
                .Where(k => k != null && k.StartsWith("extra.", StringComparison.Ordinal)).ToArray();
            Check(keys.SequenceEqual(pair.Value.Select(k => "extra." + k)), "setting order/membership: " + pair.Key);
            XmlNode content = topic.SelectNodes("./*").Cast<XmlNode>().Single(n => HostGuard(n));
            Check(content.Attributes["Visibility"].Value == "{Binding " + prefix + ".ContentVisibility}", "fold only the guarded content");
        }
        foreach (XmlNode node in nodes.Where(n => new[] { "Button", "CheckBox", "ComboBox", "Slider", "TextBox" }.Contains(n.LocalName)))
        {
            Check(node.Attributes["ToolTipService.ShowDuration"] != null && node.Attributes["ToolTipService.ShowDuration"].Value == "60000" &&
                node.Attributes["ToolTip"] != null && !string.IsNullOrWhiteSpace(node.Attributes["ToolTip"].Value), "nonempty 60000-ms tooltip: " + node.LocalName);
            if (!Ancestors(node).Any(n => Search(n, "IsSection") == "True")) continue;
            if (node.LocalName == "Button" && Search(node, "Exclude") == "True") continue;
            Check(Ancestors(node).Any(HostGuard), "every topic editor remains host guarded");
            Check(Ancestors(node).Count(n => Search(n, "IsSection") == "True") == 1, "editor in exactly one topic");
            Check(Ancestors(node).Any(n => Search(n, "Key") != null), "editor retains an explicit setting key");
        }
        XmlNode items = nodes.Single(n => n.LocalName == "ItemsControl" && n.Attributes["ItemsSource"] != null && n.Attributes["ItemsSource"].Value == "{Binding MarketGoodPriceItems}");
        Check(items.Attributes["Width"] == null && items.Attributes["HorizontalAlignment"].Value == "Stretch" &&
            items.SelectNodes(".//*[local-name()='WrapPanel']").Cast<XmlNode>().All(n => n.Attributes["Width"] == null), "goods wrap to the finite available width");
        Check(nodes.Any(n => n.LocalName == "ScrollViewer" && n.Attributes["HorizontalScrollBarVisibility"].Value == "Auto" &&
            n.Attributes["VerticalScrollBarVisibility"].Value == "Auto"), "outer Auto scrollbars retained");
    }

    private static void NegativeXaml(XmlDocument original, Action<XmlDocument> mutate)
    {
        XmlDocument changed = (XmlDocument)original.CloneNode(true);
        mutate(changed);
        bool rejected = false;
        try { ValidateXaml(changed); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "invalid layout is rejected by structural guard");
    }

    public static void Run(Assembly mod, string project)
    {
        checks = 0;
        var document = new XmlDocument();
        string path = Path.Combine(project, "Override/ScriptExtenderUI/ExtraFeaturesSettings.xaml");
        document.Load(path);
        ValidateXaml(document);
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[@shared:ModSettingsSearch.IsSection='True']", Namespaces(d))).SetAttribute("IsEnabled", "{Binding CanEditHostSettings}"));
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[@IsEnabled='{Binding CanEditHostSettings}' and @Opacity]")).RemoveAttribute("IsEnabled"));
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[local-name()='Slider']")).SetAttribute("ToolTipService.ShowDuration", "100"));
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[local-name()='ItemsPanelTemplate']/*[local-name()='WrapPanel']")).SetAttribute("Width", "760"));

        Type type = mod.GetType("ExtraFeatures.ModSettingsPresentation", true);
        object presentation = Activator.CreateInstance(type, true);
        object[] topics = ((IEnumerable)Get(presentation, "Topics")).Cast<object>().ToArray();
        var topicChanges = new HashSet<string>();
        ((INotifyPropertyChanged)topics[0]).PropertyChanged += (sender, args) => topicChanges.Add(args.PropertyName);
        var presentationChanges = new HashSet<string>();
        ((INotifyPropertyChanged)presentation).PropertyChanged += (sender, args) => presentationChanges.Add(args.PropertyName);
        Check(topics.Length == Membership.Count && topics.All(t => !Visible(t, "ContentVisibility") && Visible(t, "Visibility")), "initially closed, visible topics");
        var modelType = mod.GetType("ExtraFeatures.ExtraFeaturesViewModel", true);
        PropertyInfo viewState = modelType.GetProperty("SettingsPresentation");
        Check(viewState.GetSetMethod(true) == null && viewState.GetCustomAttributesData().Count == 0, "view state cannot become a persisted setting");
        Execute(topics[0], "ToggleCommand"); Execute(topics[1], "ToggleCommand");
        Check(Visible(topics[0], "ContentVisibility") && Visible(topics[1], "ContentVisibility"), "independent manual expansion");
        Check(topicChanges.Contains("ContentVisibility") && topicChanges.Contains("IsExpanded"), "manual changes notify content and arrow bindings");
        Execute(presentation, "ExpandAllCommand");
        Check(topics.All(t => Visible(t, "ContentVisibility")), "expand all");
        Execute(presentation, "CollapseAllCommand");
        Check(topics.All(t => !Visible(t, "ContentVisibility")), "collapse all");
        Execute(topics[0], "ToggleCommand");

        // The fixture supplies only text/items. Catalog extraction and matching are the real APIShared implementation.
        object fixture = CreateFixture(document, mod);
        Type catalog = modelType.BaseType.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchCatalog", true);
        catalog.GetMethod("RegisterSource").Invoke(null, new object[] { fixture, path, null, "ExtraFeatures UI fixture", null });
        object[] read = { fixture, null };
        Check((bool)catalog.GetMethod("TryGetEntries").Invoke(null, read), "catalog registered before any topic is realized");
        object entries = read[1];
        object[] indexed = ((IEnumerable)entries).Cast<object>().ToArray();
        string[] missing = Membership.Values.SelectMany(keys => keys).Where(key => !indexed.Any(e => (string)Get(e, "Key") == "extra." + key)).ToArray();
        Check(missing.Length == 0, "catalog contains every static row; missing: " + string.Join(", ", missing));
        MethodInfo refresh = type.GetMethod("RefreshSearch", All);
        Action<string, bool, string> search = (text, tooltips, key) => refresh.Invoke(presentation, new object[] { text, tooltips, key, entries });
        search("", false, "");
        foreach (object topic in topics)
        {
            string key = (string)Get(topic, "Key");
            int expected = indexed.Count(e => (string)Get(e, "SectionKey") == key && !(bool)Get(e, "IsSection"));
            string name = Membership.Keys.Single(k => ReferenceEquals(Get(presentation, k), topic));
            int specified = Membership[name].Length + (name == "Market" ? SettingsSearchFixtureBase.Goods.Length * 2 : 0);
            Check((int)Get(topic, "OptionCount") == expected && expected == specified, "full catalog count, one per logical row: " + name);
        }
        object market = Get(presentation, "Market");
        int marketCount = (int)Get(market, "OptionCount");
        Check(marketCount == Membership["Market"].Length + SettingsSearchFixtureBase.Goods.Length * 2, "all tradeable goods have buy and sell entries");
        Array duplicates = Array.CreateInstance(indexed[0].GetType(), indexed.Length + 1);
        for (int index = 0; index < indexed.Length; index++) duplicates.SetValue(indexed[index], index);
        duplicates.SetValue(indexed.First(e => (string)Get(e, "SectionKey") == (string)Get(market, "Key") && !(bool)Get(e, "IsSection")), indexed.Length);
        refresh.Invoke(presentation, new object[] { "", false, "", duplicates });
        Check((int)Get(market, "OptionCount") == marketCount, "duplicate catalog keys do not inflate counts");
        string goodKey = SettingsSearchFixtureBase.Goods.Last().SellSearchKey;
        search("", false, goodKey);
        Check(Visible(market, "ContentVisibility") && topics.Where(t => !ReferenceEquals(t, market)).All(t => !Visible(t, "Visibility")), "exact goods navigation opens only its topic");
        Check((int)Get(market, "OptionCount") == marketCount && !Visible(presentation, "BulkActionsVisibility"), "filter does not change count; bulk actions hidden");
        Check(presentationChanges.Contains("BulkActionsVisibility") && topicChanges.Contains("Visibility") && topicChanges.Contains("Title"), "search and refresh notify visible/localized bindings");
        Execute(presentation, "CollapseAllCommand"); Execute(presentation, "ExpandAllCommand"); Execute(market, "ToggleCommand"); Execute(topics[0], "ToggleCommand");
        search("", false, "");
        Check(Visible(topics[0], "ContentVisibility") && !Visible(market, "ContentVisibility") && topics.Skip(1).All(t => !Visible(t, "ContentVisibility")), "search and commands preserve manual state");
        search("", false, (string)Get(market, "Key"));
        Check(Visible(market, "ContentVisibility"), "exact section navigation");
        search("Topic.Plague", false, "");
        Check(Visible(Get(presentation, "Plague"), "ContentVisibility") && !Visible(market, "Visibility"), "topic-title search");
        search("Help.MarketSellPriceMultiplierHelpText", false, "");
        Check(!Visible(market, "Visibility"), "tooltip excluded unless requested");
        search("Help.MarketSellPriceMultiplierHelpText", true, "");
        Check(Visible(market, "ContentVisibility"), "tooltip search opens topic");
        search("no-such-setting-913", true, "");
        Check(topics.All(t => !Visible(t, "Visibility")), "no results hides all topics");
        search("", false, "");
        Check(topics.All(t => Visible(t, "Visibility")) && Visible(topics[0], "ContentVisibility"), "clearing no-results restores manual state");

        // Replacement snapshots cover newly available goods and language changes without persisting UI state.
        SettingsSearchFixtureBase.Labels.Market.Title = "Localized market topic";
        SettingsSearchFixtureBase.Goods = SettingsSearchFixtureBase.Goods.Concat(new[] { new SettingsSearchGood { Id = 999999 } }).ToArray();
        catalog.GetMethod("RegisterSource").Invoke(null, new object[] { fixture, path, null, "ExtraFeatures UI fixture", null });
        read[1] = null; catalog.GetMethod("TryGetEntries").Invoke(null, read); entries = read[1];
        search("Localized market topic", false, "");
        Check(Visible(market, "ContentVisibility") && (int)Get(market, "OptionCount") == marketCount + 2, "replaced catalog updates localized sections and dynamic counts");
        refresh.Invoke(presentation, new object[] { "", false, "", null });
        Check(topics.All(t => Visible(t, "Visibility")) && Visible(topics[0], "ContentVisibility"), "empty catalog preserves navigation/manual state without a native fallback");

        object converter = Activator.CreateInstance(mod.GetType("ExtraFeatures.SettingsTopicWidthConverter", true));
        MethodInfo convert = converter.GetType().GetMethod("Convert");
        Type widthType = type.GetProperty("BulkActionsVisibility").PropertyType.Assembly.GetType("Noesis.FrameworkElement", true).GetProperty("Width").PropertyType;
        foreach (object input in new object[] { null, 0d, 12f, 640d, double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            object result = convert.Invoke(converter, new[] { input, (object)widthType, null, CultureInfo.InvariantCulture });
            Check(result is float && widthType.IsInstanceOfType(result) && (float)result >= 0 && !float.IsInfinity((float)result) && !float.IsNaN((float)result), "width converter returns finite Noesis Single");
        }
        Check((float)convert.Invoke(converter, new object[] { 640d, widthType, null, CultureInfo.InvariantCulture }) == 620f, "width accounts for page margin");
        ValidateLocales(project);
        Console.WriteLine("PASS: ExtraFeatures settings presentation, catalog, layout/authority guardrails and locales (" + checks + " checks; no native rendering).");
    }

    private static XmlNamespaceManager Namespaces(XmlDocument document)
    {
        var manager = new XmlNamespaceManager(document.NameTable);
        manager.AddNamespace("shared", "clr-namespace:APIShared.ModSettings;assembly=APIShared");
        return manager;
    }

    private static object CreateFixture(XmlDocument document, Assembly mod)
    {
        Type definition = mod.GetType("ExtraFeatures.MarketGoodPriceDefinition", true);
        int count = (int)definition.GetProperty("Count").GetValue(null, null);
        SettingsSearchFixtureBase.Goods = Enumerable.Range(0, count).Select(index => new SettingsSearchGood
        { Id = (int)definition.GetMethod("GetGood").Invoke(null, new object[] { index }) }).ToArray();
        SettingsSearchFixtureBase.Labels = new SettingsSearchLabels();
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("ExtraFeaturesSettingsFixture"), AssemblyBuilderAccess.Run);
        TypeBuilder builder = assembly.DefineDynamicModule("Fixture").DefineType("SettingsFixture", TypeAttributes.Public, typeof(SettingsSearchFixtureBase));
        IEnumerable<string> properties = document.SelectNodes("//@*").Cast<XmlAttribute>()
            .Select(a => Regex.Match(a.Value, @"^\{Binding\s+(\w+)").Groups[1].Value).Where(p => p.Length > 0).Distinct();
        foreach (string property in properties)
        {
            if (typeof(SettingsSearchFixtureBase).GetProperty(property) != null) continue;
            PropertyBuilder field = builder.DefineProperty(property, PropertyAttributes.None, typeof(string), null);
            MethodBuilder getter = builder.DefineMethod("get_" + property, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
            ILGenerator il = getter.GetILGenerator();
            il.Emit(OpCodes.Ldstr, (property.Contains("Help") ? "Help." : "Text.") + property); il.Emit(OpCodes.Ret);
            field.SetGetMethod(getter);
        }
        return Activator.CreateInstance(builder.CreateType());
    }

    private static void ValidateLocales(string project)
    {
        string prefix = "ExtraFeatures.SettingsTopics.";
        string[] expected = Membership.Keys.Select(k => prefix + "Topic." + k).Concat(new[] { "ExpandAll", "CollapseAll", "ExpandAllHelp", "CollapseAllHelp", "TopicToggleHelp" }.Select(k => prefix + k)).OrderBy(k => k).ToArray();
        foreach (string file in Directory.GetFiles(Path.Combine(project, "Locales"), "*.txt"))
        {
            string[] lines = File.ReadAllLines(file).Where(line => line.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            Check(lines.Select(line => line.Split('=')[0]).OrderBy(k => k).SequenceEqual(expected), "new locale keys are complete/unique: " + Path.GetFileName(file));
            Check(lines.All(line => line.IndexOf('=') < line.Length - 1), "new locale text is nonempty");
        }
    }
}

public class SettingsSearchFixtureBase
{
    public static SettingsSearchLabels Labels;
    public static SettingsSearchGood[] Goods;
    public SettingsSearchLabels SettingsPresentation { get { return Labels; } }
    public SettingsSearchGood[] MarketGoodPriceItems { get { return Goods; } }
}
public class SettingsSearchTopicLabel { public string Title { get; set; } }
public class SettingsSearchLabels
{
    public SettingsSearchLabels()
    {
        Units = new SettingsSearchTopicLabel { Title = "Topic.Units" };
        Gatehouses = new SettingsSearchTopicLabel { Title = "Topic.Gatehouses" };
        Building = new SettingsSearchTopicLabel { Title = "Topic.Building" };
        Economy = new SettingsSearchTopicLabel { Title = "Topic.Economy" };
        Demolition = new SettingsSearchTopicLabel { Title = "Topic.Demolition" };
        Plague = new SettingsSearchTopicLabel { Title = "Topic.Plague" };
        Market = new SettingsSearchTopicLabel { Title = "Topic.Market" };
    }
    public SettingsSearchTopicLabel Units { get; private set; }
    public SettingsSearchTopicLabel Gatehouses { get; private set; }
    public SettingsSearchTopicLabel Building { get; private set; }
    public SettingsSearchTopicLabel Economy { get; private set; }
    public SettingsSearchTopicLabel Demolition { get; private set; }
    public SettingsSearchTopicLabel Plague { get; private set; }
    public SettingsSearchTopicLabel Market { get; private set; }
}
public class SettingsSearchGood
{
    public int Id;
    public string BuySearchKey { get { return "MarketGoodPrice.Buy." + Id; } }
    public string SellSearchKey { get { return "MarketGoodPrice.Sell." + Id; } }
    public string BuySearchTitle { get { return "Buy good " + Id; } }
    public string SellSearchTitle { get { return "Sell good " + Id; } }
    public string BuyToolTip { get { return "Buy tooltip " + Id; } }
    public string SellToolTip { get { return "Sell tooltip " + Id; } }
}
