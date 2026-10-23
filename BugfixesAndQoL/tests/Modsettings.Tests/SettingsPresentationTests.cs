using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Xml;

internal static partial class Program
{
    private static object Get(object value, string property) => value.GetType().GetProperty(property, All).GetValue(value, null);
    private static bool Visible(object value, string property) => Get(value, property).ToString() == "Visible";
    private static void Execute(object value, string property) => ((ICommand)Get(value, property)).Execute(null);
    private static string SearchAttribute(XmlNode node, string name) => node.Attributes?.Cast<XmlAttribute>()
        .FirstOrDefault(a => a.LocalName == "ModSettingsSearch." + name)?.Value;
    private static string BindingProperty(string value) => Regex.Match(value ?? "", @"^\{Binding (\w+)").Groups[1].Value;

    private static void SettingsPresentationTests(string workspace)
    {
        Type presentationType = mod.GetType("BugfixesAndQoL.ModSettingsPresentation", true);
        int cancelled = 0;
        object presentation = Activator.CreateInstance(presentationType, All, null,
            new object[] { (Action)(() => cancelled++) }, null);
        var topics = ((IEnumerable)Get(presentation, "Topics")).Cast<object>().ToArray();
        var options = ((IEnumerable)Get(presentation, "Options")).Cast<object>().ToArray();
        string file = Path.Combine(workspace, "BugfixesAndQoL/Override/ScriptExtenderUI/BugfixesAndQoLSettings.xaml");
        var document = new XmlDocument(); document.Load(file);
        XmlNode[] nodes = document.SelectNodes("//*").Cast<XmlNode>().ToArray();
        string proposals = File.ReadAllText(Path.Combine(workspace, "BugfixesAndQoL/ModsettingsRecommendations.md"));
        var approved = proposals.Split('\n').Where(line => line.StartsWith("| ") && line.Contains("`"))
            .ToDictionary(line => Regex.Match(line, "`(\\w+)`").Groups[1].Value,
                line => line.Split('|')[2].Trim(), StringComparer.Ordinal);
        // The complete editorial list was explicitly approved on 2026-10-23. Compare
        // identities and classifications, not default values or historical UI categories.
        Check(approved.Values.Count(v => v == "Empfohlen") == 47 && approved.Values.Count(v => v == "Geschmackssache") == 60,
            "complete approved editorial list contains 47 recommendations and 60 preferences");
        XmlNode[] topicNodes = nodes.Where(n => SearchAttribute(n, "IsSection") == "True").ToArray();
        Check(topics.Length == 10 && topicNodes.Length == 10, "one collapsible level with ten topics");
        Check(topicNodes.All(n => n.Attributes?["Width"]?.Value.Contains("ViewportWidth, ElementName=SettingsViewport") == true &&
            n.Attributes?["HorizontalAlignment"]?.Value == "Left"), "topics have a finite viewport width even with horizontal Auto scrolling");
        Type widthConverter = mod.GetType("BugfixesAndQoL.SettingsTopicWidthConverter", true);
        object converter = Activator.CreateInstance(widthConverter);
        MethodInfo convert = widthConverter.GetMethod("Convert");
        Type scrollViewer = presentationType.GetProperty("BulkActionsVisibility").PropertyType.Assembly.GetType("Noesis.ScrollViewer", true);
        Type widthType = scrollViewer.Assembly.GetType("Noesis.FrameworkElement", true).GetProperty("Width").PropertyType;
        Func<object, float> topicWidth = v =>
        {
            object result = convert.Invoke(converter, new object[] { v, widthType, null, System.Globalization.CultureInfo.InvariantCulture });
            // Noesis checks assignability before accepting converter output; numeric
            // equality alone missed the boxed Double regression on its Single Width.
            Check(widthType.IsInstanceOfType(result) && result is float,
                "converter output matches the installed Noesis Width type for every input");
            float width = (float)result;
            Check(!float.IsNaN(width) && !float.IsInfinity(width) && width >= 0,
                "converted width is finite and nonnegative");
            return width;
        };
        Check(topicWidth(500.0) == 480f && topicWidth(320.0f) == 300f &&
            topicWidth(40f) == 20f && topicWidth(20f) == 0f && topicWidth(10f) == 0f,
            "viewport binding reserves the actual page margins even in narrow views");
        foreach (object input in new object[] { null, 0.0, 0f, -1.0, -1f,
            double.NaN, double.PositiveInfinity, double.NegativeInfinity,
            float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            Check(topicWidth(input) == 0f, "initial or invalid viewport sizes use zero width");
        Check(topicWidth(double.MaxValue) == float.MaxValue, "large finite input cannot overflow the Noesis Width type");
        Check(scrollViewer.GetProperty("ViewportWidth") != null, "the installed Noesis assembly exposes the bound viewport member");
        Type buttonType = scrollViewer.Assembly.GetType("Noesis.Button", true);
        Check(new[] { "Template", "ContentTemplate", "Foreground", "IsMouseOver", "IsKeyboardFocused" }
            .All(name => buttonType.GetProperty(name) != null), "plain header uses supported installed Noesis properties");
        Check(topics.All(t => !Visible(t, "ContentVisibility") && Visible(t, "Visibility")), "first visit shows every closed topic");
        Check(topics.All(t => !(bool)Get(t, "IsExpanded")), "initial vector-arrow state is closed");
        XmlNode headingStyle = nodes.Single(n => n.LocalName == "Style" && n.Attributes.Cast<XmlAttribute>().Any(a => a.LocalName == "Key" && a.Value == "TopicHeading"));
        Check(headingStyle.SelectSingleNode(".//*[local-name()='ControlTemplate']/*[local-name()='Border' and @Background='Transparent' and @BorderThickness='0']/*[local-name()='ContentPresenter']") != null,
            "topic headings replace the default colored button chrome with transparent content");
        foreach (string trigger in new[] { "IsMouseOver", "IsKeyboardFocused" })
            Check(headingStyle.SelectSingleNode(".//*[local-name()='Trigger' and @Property='" + trigger + "']/*[local-name()='Setter' and @Property='Foreground']") != null,
                "plain headings retain visible feedback: " + trigger);
        XmlNode chevronStyle = nodes.Single(n => n.LocalName == "Style" && n.Attributes.Cast<XmlAttribute>().Any(a => a.LocalName == "Key" && a.Value == "TopicChevron"));
        string closedPath = chevronStyle.SelectSingleNode("./*[local-name()='Setter' and @Property='Data']").Attributes["Value"].Value;
        string openPath = chevronStyle.SelectSingleNode(".//*[local-name()='DataTrigger' and @Binding='{Binding IsExpanded}']/*[local-name()='Setter' and @Property='Data']").Attributes["Value"].Value;
        Check(closedPath != openPath && closedPath.EndsWith("Z") && openPath.EndsWith("Z"), "open and closed arrows use distinct filled vector paths");
        foreach (XmlNode topicNode in topicNodes)
        {
            XmlNode head = topicNode.SelectSingleNode("./*[local-name()='Button']");
            Check(head.Attributes?["Style"]?.Value == "{StaticResource TopicHeading}" &&
                head.SelectSingleNode(".//*[local-name()='Path' and @Style='{StaticResource TopicChevron}']") != null &&
                !head.OuterXml.Contains(".Arrow}"), "every topic uses the plain header and a font-independent arrow");
            XmlNode count = head.SelectSingleNode(".//*[local-name()='Run' and contains(@Text,'.CountText}')]");
            Check(count?.Attributes?["Foreground"]?.Value == "#FFBBBBBB" && count.Attributes?["FontSize"]?.Value == "14",
                "option count is visually secondary");
            XmlNode[] inline = count?.ParentNode.ChildNodes.Cast<XmlNode>().Where(n => n.NodeType == XmlNodeType.Element).ToArray();
            Check(inline?.Length == 3 && inline[0].Attributes?["Text"]?.Value.EndsWith(".Title}") == true &&
                inline[1].Attributes?["Text"]?.Value == " " && ReferenceEquals(inline[2], count) &&
                count.ParentNode.Attributes?["TextWrapping"]?.Value == "Wrap",
                "topic title and count share one wrapping text with a single space");
        }
        foreach (string role in new[] { "Client", "Host" })
        {
            XmlNode title = nodes.Single(n => n.LocalName == "TextBlock" && n.Attributes?["Text"]?.Value == "{Binding " + role + "OptionsText}");
            XmlNode roleRow = title.ParentNode;
            XmlNode[] actions = roleRow.SelectNodes(".//*[local-name()='Button']").Cast<XmlNode>().ToArray();
            Check(roleRow.LocalName == "WrapPanel" && actions.Length == 2 &&
                actions.All(n => n.Attributes?["Margin"]?.Value == "4,0,0,0") &&
                roleRow.SelectSingleNode(".//*[local-name()='ColumnDefinition']") == null,
                "role title and bulk commands flow together without a stretching gap: " + role);
        }

        // Read the actual editor bindings, rather than fixing the total to a historical snapshot.
        var editors = nodes.Where(n => n.LocalName == "CheckBox" || n.LocalName == "Slider" || n.LocalName == "ComboBox")
            .Where(n => Ancestors(n).Any(a => SearchAttribute(a, "SectionKey")?.StartsWith("bugfixes.topic.", StringComparison.Ordinal) == true))
            .Select(n => new { Node = n, Property = BindingProperty(n.Attributes?[n.LocalName == "CheckBox" ? "IsChecked" : n.LocalName == "Slider" ? "Value" : "SelectedIndex"]?.Value) })
            .Where(x => x.Property.Length > 0).ToArray();
        Check(editors.Length == options.Length && editors.Select(e => e.Property).Distinct().Count() == editors.Length,
            "each current main editor appears exactly once");
        Check(new HashSet<string>(editors.Select(e => e.Property)).SetEquals(options.Select(o => (string)Get(o, "PropertyName"))),
            "all current options have presentation metadata, without stale entries");
        // These XAML guardrails target the reported spacing regression. Actual wrap
        // positions still require acceptance in the game's native Noesis renderer.
        foreach (var editor in editors.Where(e => e.Node.LocalName == "Slider" || e.Node.LocalName == "ComboBox"))
        {
            XmlNode group = editor.Node.ParentNode;
            XmlNode flow = group.ParentNode;
            XmlNode row = Ancestors(editor.Node).First(a => SearchAttribute(a, "Key") != null);
            XmlNode label = flow.SelectSingleNode("./*[local-name()='TextBlock']");
            Check(group.LocalName == "WrapPanel" && flow.LocalName == "WrapPanel" &&
                group.Attributes?["Margin"]?.Value == "4,0,0,0" &&
                label?.Attributes?["Text"]?.Value == SearchAttribute(row, "Title") &&
                label.Attributes?["TextWrapping"]?.Value == "Wrap" && label.Attributes?["Margin"] == null &&
                flow.SelectSingleNode(".//*[local-name()='ColumnDefinition']") == null &&
                editor.Node.Attributes?["Grid.Column"] == null,
                "value editor follows its wrapping title without a stretching column: " + editor.Property);
            string width = editor.Node.LocalName == "Slider" || editor.Property == "SpriteSheetMergeModeIndex" ? "160" : "180";
            Check(editor.Node.Attributes?["Width"]?.Value == width,
                "compact editor preserves its usable width: " + editor.Property);
        }
        Check(topics.Sum(t => (int)Get(t, "OptionCount")) == editors.Length, "topic counts count main editors only");
        foreach (var editor in editors)
        {
            object option = options.Single(o => (string)Get(o, "PropertyName") == editor.Property);
            XmlNode row = Ancestors(editor.Node).First(a => SearchAttribute(a, "Key") != null);
            Check(SearchAttribute(row, "Key") == (string)Get(option, "Key"), "stable unique search key: " + editor.Property);
            XmlNode topicNode = Ancestors(row).First(a => SearchAttribute(a, "IsSection") == "True");
            object topic = topics.Single(t => (string)Get(t, "Key") == SearchAttribute(topicNode, "SectionKey"));
            string authority = (bool)Get(topic, "IsHost") ? "CanEditHostSettings" : "CanEditClientSettings";
            Check(Ancestors(editor.Node).Any(a => a.Attributes?["IsEnabled"]?.Value == "{Binding " + authority + "}"),
                "editor retains role authority: " + editor.Property);
            Check(!Ancestors(topicNode).Any(a => a.Attributes?["IsEnabled"] != null), "topic header remains browsable independently of authority");
            XmlNode marker = row.ChildNodes.Cast<XmlNode>().Single(n => n.LocalName == "TextBlock");
            Check(marker.Attributes?["DataContext"]?.Value == "{Binding SettingsPresentation." + editor.Property + "}" &&
                marker.Attributes?["Text"]?.Value == "{Binding Symbol}" &&
                marker.Attributes?["Grid.Column"] == null &&
                row.SelectSingleNode("*[local-name()='Grid.ColumnDefinitions']/*[1]")?.Attributes?["Width"]?.Value == "28",
                "fixed left recommendation column: " + editor.Property);
            bool recommended = approved[editor.Property] == "Empfohlen";
            Check((string)Get(option, "Symbol") == (recommended ? "★" : "◇") &&
                (bool)Get(option, "IsPreference") == !recommended,
                "marker matches the author's explicit classification: " + editor.Property);
            Check((string)Get(option, "ReasonKey") == "BugfixesAndQoL.SettingsRework.Reason." + editor.Property,
                "reason is keyed to the stable setting identity: " + editor.Property);
        }
        Check(options.All(o => o.GetType().GetProperties().All(p => p.GetCustomAttributesData().All(a =>
            !a.AttributeType.Name.StartsWith("Sync") && a.AttributeType.Name != "PresetLocalAttribute"))), "view metadata is not a setting");
        Check(modelType.GetProperty("SettingsPresentation").SetMethod == null &&
            modelType.GetProperty("SettingsPresentation").GetCustomAttributesData().Count == 0, "presentation is read-only unclassified view state");

        object example = options.First();
        object originalKind = Get(example, "Recommendation");
        string originalReason = (string)Get(example, "ReasonKey");
        Type kind = mod.GetType("BugfixesAndQoL.SettingsRecommendationKind", true);
        MethodInfo approve = example.GetType().GetMethod("SetApprovedRecommendation", All);
        approve.Invoke(example, new[] { Enum.Parse(kind, "Recommended"), "Confirmed example reason" });
        Check((string)Get(example, "Symbol") == "★" && !(bool)Get(example, "IsPreference"), "approved recommendation uses gold-star state");
        approve.Invoke(example, new[] { Enum.Parse(kind, "Preference"), "Confirmed preference reason" });
        Check((string)Get(example, "Symbol") == "◇" && (bool)Get(example, "IsPreference"), "approved preference uses diamond state");
        approve.Invoke(example, new object[] { Enum.Parse(kind, "Unreviewed"), null });
        Check((string)Get(example, "Symbol") == "", "removing editorial classification restores blank column");
        bool rejectedReason = false;
        try { approve.Invoke(example, new object[] { Enum.Parse(kind, "Recommended"), "" }); }
        catch (TargetInvocationException ex) { rejectedReason = ex.InnerException is ArgumentException; }
        Check(rejectedReason, "visible recommendations require an explanatory reason");
        approve.Invoke(example, new[] { originalKind, originalReason });
        MethodInfo format = example.GetType().GetMethod("FormatToolTip", All);
        Func<string, string> firstLanguage = key => "first/" + key;
        Func<string, string> nextLanguage = key => "next/" + key;
        string firstTooltip = (string)format.Invoke(example, new object[] { firstLanguage });
        string nextTooltip = (string)format.Invoke(example, new object[] { nextLanguage });
        Check(firstTooltip.Contains("first/" + originalReason) && nextTooltip.Contains("next/" + originalReason) && firstTooltip != nextTooltip,
            "tooltip resolves its locale key on demand instead of caching a language at startup");
        var tooltipChanges = new List<string>();
        ((INotifyPropertyChanged)example).PropertyChanged += (_, args) => tooltipChanges.Add(args.PropertyName);
        presentationType.GetMethod("RefreshLocalization", All).Invoke(presentation, null);
        Check(tooltipChanges.SequenceEqual(new[] { "ToolTip" }), "catalog language refresh updates marker tooltip bindings");

        object display = Get(presentation, "ClientDisplay"), controls = Get(presentation, "ClientControls");
        int notifications = 0;
        ((INotifyPropertyChanged)display).PropertyChanged += (_, e) => notifications++;
        Execute(display, "ToggleCommand"); Execute(controls, "ToggleCommand");
        Check(Visible(display, "ContentVisibility") && Visible(controls, "ContentVisibility"), "several topics open together");
        Check((bool)Get(display, "IsExpanded") && (bool)Get(controls, "IsExpanded"), "manual expansion updates vector arrows");
        Execute(presentation, "ExpandHostCommand");
        Check(topics.Where(t => (bool)Get(t, "IsHost")).All(t => Visible(t, "ContentVisibility")), "host expand all");
        Execute(presentation, "CollapseHostCommand");
        Check(topics.Where(t => (bool)Get(t, "IsHost")).All(t => !Visible(t, "ContentVisibility")) && Visible(display, "ContentVisibility"),
            "bulk operation is limited to its role");
        Check(notifications > 0, "view changes publish binding notifications");

        // Use the real shared catalog on the production XAML. Test doubles supply only text/item getters.
        // No native UI is created, measured, traversed or required by this fixture.
        object fixture = CreateSearchFixture(nodes, topics);
        Type catalog = modelType.BaseType.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchCatalog", true);
        MethodInfo register = catalog.GetMethod("RegisterSource", BindingFlags.Public | BindingFlags.Static);
        register.Invoke(null, new object[] { fixture, file, null, "BFQoL search fixture", null });
        object[] read = { fixture, null };
        Check((bool)catalog.GetMethod("TryGetEntries").Invoke(null, read), "shared source registered");
        object entries = read[1];
        object[] indexed = ((IEnumerable)entries).Cast<object>().ToArray();
        Check(options.All(o => indexed.Any(e => (string)Get(e, "Key") == (string)Get(o, "Key"))), "closed-topic XAML catalog includes every main option");
        MethodInfo refresh = presentationType.GetMethod("RefreshSearch", All);
        Action<string, bool, string> search = (text, tooltips, exact) => refresh.Invoke(presentation, new[] { (object)text, tooltips, exact, entries });

        object performance = Get(presentation, "HostPerformance"), market = Get(presentation, "ClientMarket");
        string performanceKey = (string)Get(performance, "Key");
        object assassin = Get(presentation, "EnableImprovedAssassinPathfindingAI");
        string assassinKey = (string)Get(assassin, "Key");
        search("", false, assassinKey);
        Check(Visible(performance, "ContentVisibility") && !Visible(display, "Visibility") && !Visible(presentation, "BulkActionsVisibility"),
            "exact global option opens closed topic and hides unrelated topics");
        Check((bool)Get(performance, "IsExpanded"), "search expansion updates the vector arrow");
        Check(!Visible(performance, "FixesVisibility") && Visible(performance, "QolVisibility"), "only matching category heading remains");
        Execute(presentation, "ExpandHostCommand"); Execute(performance, "ToggleCommand");
        search("", false, "");
        Check(!Visible(performance, "ContentVisibility") && Visible(display, "ContentVisibility") && Visible(controls, "ContentVisibility"),
            "search and commands during search do not overwrite manual expansion");
        Check(!(bool)Get(performance, "IsExpanded"), "clearing search restores the arrow's manual state");
        search("", false, performanceKey);
        Check(Visible(performance, "FixesVisibility") && Visible(performance, "QolVisibility"), "exact theme hit reveals its categories");
        search("Theme HostPerformance", false, "");
        Check(Visible(performance, "ContentVisibility"), "theme title text reveals its options");
        search("Help.EnableImprovedAssassinPathfindingAIHelpText", false, "");
        Check(!Visible(performance, "Visibility"), "tooltip search can be disabled");
        search("Help.EnableImprovedAssassinPathfindingAIHelpText", true, "");
        Check(Visible(performance, "ContentVisibility"), "tooltip search reveals matching closed topic");
        search("", false, "bugfixes.market-good.fixture");
        Check(Visible(market, "ContentVisibility") && Visible(market, "QolVisibility"), "dynamic goods exact hit reveals editor and QoL heading");
        search("", false, "bugfixes.restore-hd-market-order");
        Check(Visible(market, "ContentVisibility"), "restore action reveals its editor");
        search("no-such-setting-xyz", true, "");
        Check(topics.All(t => !Visible(t, "Visibility")), "no match leaves no empty topics");
        search("", true, "");
        Check(topics.All(t => Visible(t, "Visibility")) && Visible(display, "ContentVisibility") && !Visible(market, "ContentVisibility"),
            "clearing filter restores the session view");
        Execute(presentation, "CollapseClientCommand");
        Check(cancelled > 0, "closing/hiding hotkey topic cancels capture");

        // A replacement catalog changes metadata without changing the remembered manual state.
        register.Invoke(null, new object[] { fixture, file, null, "BFQoL refreshed fixture", null });
        read = new object[] { fixture, null }; catalog.GetMethod("TryGetEntries").Invoke(null, read); entries = read[1];
        search("", true, assassinKey);
        Check(Visible(performance, "ContentVisibility"), "refreshed catalog remains searchable");
        search("", true, "");
        Check(!Visible(performance, "ContentVisibility"), "catalog replacement preserves manual state");

        Check(topicNodes.All(n => !n.SelectNodes(".//*[local-name()='ScrollViewer']").Cast<XmlNode>().Any()), "no nested scrolling in topics");
        Check(nodes.Where(n => n.LocalName == "Button" && Ancestors(n).Any(a => SearchAttribute(a, "IsSection") == "True"))
            .All(n => n.Attributes?["ToolTipService.ShowDuration"]?.Value == "60000" && n.Attributes?["ToolTip"] != null), "new and retained buttons have 60s tooltips");
        string[] expectedKeys = null;
        foreach (string locale in Directory.GetFiles(Path.Combine(workspace, "BugfixesAndQoL/Locales"), "*.txt"))
        {
            string[] lines = File.ReadAllLines(locale);
            string[] keys = lines.Where(l => l.StartsWith("BugfixesAndQoL.SettingsRework.", StringComparison.Ordinal)).Select(l => l.Split('=')[0]).ToArray();
            Check(keys.Length == keys.Distinct().Count() && keys.All(k => lines.Any(l => l.StartsWith(k + "=") && l.Length > k.Length + 1)), "nonempty unique locale additions: " + Path.GetFileName(locale));
            Check(options.All(o => keys.Contains((string)Get(o, "ReasonKey"))), "every approved reason exists: " + Path.GetFileName(locale));
            Check(!lines.Where(l => l.StartsWith("BugfixesAndQoL.SettingsRework.Reason.")).Any(l => Regex.IsMatch(l.Split('=')[1], @"\b\w+HelpText\b")),
                "reason texts contain readable descriptions rather than getter placeholders: " + Path.GetFileName(locale));
            Check(lines.Any(l => l.StartsWith("BugfixesAndQoL.SettingsRework.Reason.InaccessibleAIBuildingDemolitionProtection=") && l.Contains("(1)")),
                "recommended demolition-protection slider value is explicit: " + Path.GetFileName(locale));
            if (expectedKeys == null) expectedKeys = keys;
            Check(new HashSet<string>(expectedKeys).SetEquals(keys), "rework locale parity: " + Path.GetFileName(locale));
        }
        Check(options.All(o => Regex.Matches(proposals, "`" + (string)Get(o, "PropertyName") + "`").Count == 1), "editorial proposals cover each current option once");
    }

    private static object CreateSearchFixture(XmlNode[] nodes, object[] topics)
    {
        SearchFixtureBase.Labels = new SearchPresentationLabels();
        var assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName("BFQoLSettingsSearchFixture"), AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly.DefineDynamicModule("Fixture").DefineType("SearchFixture", TypeAttributes.Public, typeof(SearchFixtureBase));
        string[] properties = nodes.SelectMany(n => n.Attributes?.Cast<XmlAttribute>() ?? Enumerable.Empty<XmlAttribute>())
            .Select(a => BindingProperty(a.Value)).Where(p => p.EndsWith("Text", StringComparison.Ordinal)).Distinct().ToArray();
        foreach (string property in properties)
        {
            PropertyBuilder p = type.DefineProperty(property, PropertyAttributes.None, typeof(string), Type.EmptyTypes);
            MethodBuilder getter = type.DefineMethod("get_" + property, MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, typeof(string), Type.EmptyTypes);
            ILGenerator il = getter.GetILGenerator(); il.Emit(OpCodes.Ldstr, "Help." + property); il.Emit(OpCodes.Ret); p.SetGetMethod(getter);
        }
        return Activator.CreateInstance(type.CreateType());
    }
}

public class SearchFixtureBase
{
    public static SearchPresentationLabels Labels;
    public SearchPresentationLabels SettingsPresentation => Labels;
    public SearchGoodFixture[] MarketGoodsOrderItems => new[] { new SearchGoodFixture() };
}
public class SearchGoodFixture
{
    public string SearchKey => "bugfixes.market-good.fixture";
    public string SearchTitle => "Fixture goods";
    public string GoodToolTip => "Fixture goods tooltip";
}
public class SearchTopicLabel { public string Title { get; set; } }
public class SearchPresentationLabels
{
    public SearchTopicLabel ClientDisplay => new SearchTopicLabel { Title = "Theme ClientDisplay" };
    public SearchTopicLabel ClientControls => new SearchTopicLabel { Title = "Theme ClientControls" };
    public SearchTopicLabel ClientMenus => new SearchTopicLabel { Title = "Theme ClientMenus" };
    public SearchTopicLabel ClientMarket => new SearchTopicLabel { Title = "Theme ClientMarket" };
    public SearchTopicLabel HostSetup => new SearchTopicLabel { Title = "Theme HostSetup" };
    public SearchTopicLabel HostAi => new SearchTopicLabel { Title = "Theme HostAi" };
    public SearchTopicLabel HostTroops => new SearchTopicLabel { Title = "Theme HostTroops" };
    public SearchTopicLabel HostEconomy => new SearchTopicLabel { Title = "Theme HostEconomy" };
    public SearchTopicLabel HostSiege => new SearchTopicLabel { Title = "Theme HostSiege" };
    public SearchTopicLabel HostPerformance => new SearchTopicLabel { Title = "Theme HostPerformance" };
}
