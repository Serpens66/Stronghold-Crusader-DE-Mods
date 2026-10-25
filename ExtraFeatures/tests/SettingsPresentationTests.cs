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
        { "Units", new[] { "fear-factor-neutralization", "monks-always-run", "knight-dismount", "instant-horse", "knight-transform-gold", "knight-transform-delay", "lord-health" } },
        { "Gatehouses", new[] { "elevated-moat", "human-gate-reopen-delay", "ai-gate-reopen-delay", "human-gate-closing-distance", "ai-gate-closing-distance" } },
        { "Building", new[] { "building-repair", "keep-build-range", "human-enemy-proximity-singleplayer", "ai-enemy-proximity-singleplayer", "human-enemy-proximity-multiplayer", "ai-enemy-proximity-multiplayer", "ai-tower-gate-rebuild-delay" } },
        { "Economy", new[] { "extra-church-priests", "campfire-peasants", "no-kill-reward", "multiply-goods-gain", "multiply-goods-as-money" } },
        { "Demolition", new[] { "keep-storage-content", "wood-refund", "stone-refund", "iron-refund", "pitch-refund", "gold-refund" } },
        { "Plague", new[] { "plague-damage-walls", "plague-damage-gates", "plague-damage-towers", "plague-damage-keeps", "plague-duration", "apothecary-plague-search-distance", "healer-targets" } },
        { "Market", new[] { "market-prices-also-ai", "market-buy-price-multiplier", "market-sell-price-multiplier" } }
        ,{ "Popularity", new[] { "popularity.Rations.human", "popularity.Rations.ai", "popularity.RationsPopModifierNone.human", "popularity.RationsPopModifierNone.ai", "popularity.RationsPopModifierHalf.human", "popularity.RationsPopModifierHalf.ai", "popularity.RationsPopModifierFull.human", "popularity.RationsPopModifierFull.ai", "popularity.RationsPopModifierExtra.human", "popularity.RationsPopModifierExtra.ai", "popularity.RationsPopModifierDouble.human", "popularity.RationsPopModifierDouble.ai", "popularity.RationsPopModifierNoPreferredFood.human", "popularity.RationsPopModifierNoPreferredFood.ai", "popularity.RationsPopModifierNoPopulation.human", "popularity.RationsPopModifierNoPopulation.ai", "popularity.Variety.human", "popularity.Variety.ai", "popularity.FoodVarietyPopModifierTwoTypes.human", "popularity.FoodVarietyPopModifierTwoTypes.ai", "popularity.FoodVarietyPopModifierThreeTypes.human", "popularity.FoodVarietyPopModifierThreeTypes.ai", "popularity.FoodVarietyPopModifierFourTypes.human", "popularity.FoodVarietyPopModifierFourTypes.ai", "popularity.Crowding.human", "popularity.Crowding.ai", "popularity.OverpopulationPopModifierTierOne.human", "popularity.OverpopulationPopModifierTierOne.ai", "popularity.OverpopulationPopModifierTierTwo.human", "popularity.OverpopulationPopModifierTierTwo.ai", "popularity.OverpopulationPopModifierTierThree.human", "popularity.OverpopulationPopModifierTierThree.ai", "popularity.OverpopulationPopModifierTierFour.human", "popularity.OverpopulationPopModifierTierFour.ai", "popularity.OverpopulationPopModifierTierFive.human", "popularity.OverpopulationPopModifierTierFive.ai", "popularity.Taxes.human", "popularity.Taxes.ai", "popularity.TaxPopModifierHugeDonation.human", "popularity.TaxPopModifierHugeDonation.ai", "popularity.TaxPopModifierBigDonation.human", "popularity.TaxPopModifierBigDonation.ai", "popularity.TaxPopModifierSmallDonation.human", "popularity.TaxPopModifierSmallDonation.ai", "popularity.TaxPopModifierNone.human", "popularity.TaxPopModifierNone.ai", "popularity.TaxPopModifierLow.human", "popularity.TaxPopModifierLow.ai", "popularity.TaxPopModifierModerate.human", "popularity.TaxPopModifierModerate.ai", "popularity.TaxPopModifierHigh.human", "popularity.TaxPopModifierHigh.ai", "popularity.TaxPopModifierMean.human", "popularity.TaxPopModifierMean.ai", "popularity.TaxPopModifierUsurious.human", "popularity.TaxPopModifierUsurious.ai", "popularity.TaxPopModifierCruel.human", "popularity.TaxPopModifierCruel.ai", "popularity.TaxPopModifierMostCruel.human", "popularity.TaxPopModifierMostCruel.ai", "popularity.TaxPopModifierCruellest.human", "popularity.TaxPopModifierCruellest.ai", "popularity.TaxPopModifierUnaffordableDonation.human", "popularity.TaxPopModifierUnaffordableDonation.ai", "popularity.TaxPopModifierUnknownMode.human", "popularity.TaxPopModifierUnknownMode.ai", "popularity.Religion.human", "popularity.Religion.ai", "popularity.ReligionPopModifierTierOne.human", "popularity.ReligionPopModifierTierOne.ai", "popularity.ReligionPopModifierTierTwo.human", "popularity.ReligionPopModifierTierTwo.ai", "popularity.ReligionPopModifierTierThree.human", "popularity.ReligionPopModifierTierThree.ai", "popularity.ReligionPopModifierTierFour.human", "popularity.ReligionPopModifierTierFour.ai", "popularity.ChurchPopModifier.human", "popularity.ChurchPopModifier.ai", "popularity.CathedralPopModifier.human", "popularity.CathedralPopModifier.ai", "popularity.Ale.human", "popularity.Ale.ai", "popularity.AlePopModifierTierOne.human", "popularity.AlePopModifierTierOne.ai", "popularity.AlePopModifierTierTwo.human", "popularity.AlePopModifierTierTwo.ai", "popularity.AlePopModifierTierThree.human", "popularity.AlePopModifierTierThree.ai", "popularity.AlePopModifierTierFour.human", "popularity.AlePopModifierTierFour.ai", "popularity.Fairs.human", "popularity.Fairs.ai", "popularity.FairsPopularityEventModifier.human", "popularity.FairsPopularityEventModifier.ai", "popularity.NegativeFear.human", "popularity.NegativeFear.ai", "popularity.FearNegative1.human", "popularity.FearNegative1.ai", "popularity.FearNegative2.human", "popularity.FearNegative2.ai", "popularity.FearNegative3.human", "popularity.FearNegative3.ai", "popularity.FearNegative4.human", "popularity.FearNegative4.ai", "popularity.FearNegative5.human", "popularity.FearNegative5.ai", "popularity.PositiveFear.human", "popularity.PositiveFear.ai", "popularity.FearPositive1.human", "popularity.FearPositive1.ai", "popularity.FearPositive2.human", "popularity.FearPositive2.ai", "popularity.FearPositive3.human", "popularity.FearPositive3.ai", "popularity.FearPositive4.human", "popularity.FearPositive4.ai", "popularity.FearPositive5.human", "popularity.FearPositive5.ai" } }
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
        Check(topics.Length == Membership.Count, "declared topic sections");
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
        // Keep each pair on one grid row. This directly guards the reported
        // WrapPanel regression; widths may vary inside the agreed usable bounds.
        var popularityBlocks=nodes.Where(n=>(Search(n,"Key")??"").StartsWith("extra.popularity.")).ToArray();
        foreach(XmlNode row in popularityBlocks.Select(n=>n.ParentNode).Distinct()) {
            XmlNode[] blocks=popularityBlocks.Where(n=>ReferenceEquals(n.ParentNode,row)).ToArray();
            Check(row.LocalName=="Grid" && blocks.Length==2 && !Ancestors(blocks[0]).Any(n=>n.LocalName=="WrapPanel"),"both player editors stay on one non-wrapping table row");
            Check(((XmlElement)blocks[0]).GetAttribute("Grid.Row")==((XmlElement)blocks[1]).GetAttribute("Grid.Row") &&
                int.Parse(blocks[0].Attributes["Grid.Column"].Value)<int.Parse(blocks[1].Attributes["Grid.Column"].Value),"Human then AI on the same row");
            XmlNode caption=row.SelectNodes("./*[local-name()='TextBlock']").Cast<XmlNode>().Single();
            Check(caption.Attributes["TextWrapping"].Value=="NoWrap" && caption.Attributes["TextTrimming"].Value=="CharacterEllipsis" &&
                caption.Attributes["ToolTip"]!=null,"long row captions remain single-line and accessible in a tooltip");
            foreach(XmlNode block in blocks) {
                if(block.SelectNodes(".//*[local-name()='Slider']").Count>0)
                    Check(block.SelectNodes(".//*[local-name()='CheckBox' or local-name()='TextBlock']").Count==0,"value cells contain only slider and numeric editor");
                else {
                    XmlNode[] switches=block.SelectNodes("./*[local-name()='CheckBox']").Cast<XmlNode>().ToArray();
                    Check(switches.Length==1 && switches[0].Attributes["Content"].Value=="{Binding "+(Search(block,"Key").EndsWith(".human")?"HumanText":"AiText")+"}","group header contains one labelled switch per player type");
                    Check(block.Attributes["Visibility"].Value.Contains("SettingsPresentation.Popularity"),"group switch follows contextual visibility rather than matching only its own title");
                }
                foreach(XmlNode slider in block.SelectNodes(".//*[local-name()='Slider']"))
                    Check(slider.Attributes["Width"]==null && slider.Attributes["MinWidth"].Value=="80" && slider.Attributes["MaxWidth"].Value=="120" &&
                        block.SelectNodes("./*[local-name()='TextBox' and @Width='64']").Count==1,"bounded shrinking sliders retain 64px numeric inputs");
            }
        }
        var activationBindings=popularityBlocks.SelectMany(block=>block.SelectNodes("./*[local-name()='CheckBox']").Cast<XmlNode>()).Select(n=>n.Attributes["IsChecked"].Value).ToArray();
        Check(activationBindings.Length==activationBindings.Distinct().Count(),"group switches are not duplicated per child value");
        ValidateCompactLayout(document);
    }

    // Structural guardrails for the two reported layout regressions, not a native rendering proof.
    private static string EffectiveStyleValue(XmlNode node, string property)
    {
        XmlAttribute direct = node.Attributes == null ? null : node.Attributes[property];
        if (direct != null) return direct.Value;
        string reference = node.Attributes == null || node.Attributes["Style"] == null ? "" : node.Attributes["Style"].Value;
        while (reference.StartsWith("{StaticResource ", StringComparison.Ordinal))
        {
            string key = reference.Substring(16).TrimEnd('}');
            XmlNode style = node.OwnerDocument.SelectNodes("//*[local-name()='Style']").Cast<XmlNode>()
                .Single(n => n.Attributes.Cast<XmlAttribute>().Any(a => a.LocalName == "Key" && a.Value == key));
            XmlNode setter = style.SelectNodes("./*[local-name()='Setter']").Cast<XmlNode>().FirstOrDefault(n => n.Attributes["Property"].Value == property);
            if (setter != null) return setter.Attributes["Value"].Value;
            reference = style.Attributes["BasedOn"] == null ? "" : style.Attributes["BasedOn"].Value;
        }
        return "";
    }

    private static void ValidateCompactLayout(XmlDocument document)
    {
        XmlNode[] nodes = document.SelectNodes("//*").Cast<XmlNode>().ToArray();
        foreach (XmlNode row in nodes.Where(n => Search(n, "Key") != null && Search(n, "Key").StartsWith("extra.", StringComparison.Ordinal)))
        {
            Check(float.Parse(EffectiveStyleValue(row, "MinHeight"), CultureInfo.InvariantCulture) >= 28, "setting rows reserve checkbox height");
            bool spacing = EffectiveStyleValue(row, "Margin").Split(',').Last() == "8" || Ancestors(row).Any(n =>
                EffectiveStyleValue(n, "Margin").Split(',').Last() == "8");
            Check(spacing, "every setting row or enclosing table row has eight-unit spacing");
            if(row.SelectNodes("./*[local-name()='TextBlock']").Count>0) Check(row.SelectNodes("./*[local-name()='Grid.ColumnDefinitions']/*").Cast<XmlNode>().Take(1).All(n => n.Attributes["Width"].Value != "*"), "setting labels never consume the remaining page width");
            foreach (XmlNode check in row.SelectNodes(".//*[local-name()='CheckBox']"))
                Check(float.Parse(check.Attributes["MinHeight"].Value, CultureInfo.InvariantCulture) >= 28, "checkboxes retain minimum hit/layout height");
        }
        foreach (string key in new[] { "extra.knight-transform-gold", "extra.knight-transform-delay" })
        {
            XmlNode row = nodes.Single(n => Search(n, "Key") == key);
            XmlNode[] columns = row.SelectNodes("./*[local-name()='Grid.ColumnDefinitions']/*").Cast<XmlNode>().ToArray();
            Check(columns[0].Attributes["Width"].Value == "Auto" && columns[0].Attributes["SharedSizeGroup"].Value == "SettingLabel" &&
                columns[1].Attributes["Width"].Value == "16", "mounting cost/delay labels share a compact content-sized column");
        }
        foreach (XmlNode header in nodes.Where(n => n.LocalName == "Grid" && n.SelectNodes("./*[local-name()='TextBlock' and @FontWeight='Bold']").Count == 2))
        {
            XmlNode[] texts = header.SelectNodes("./*[local-name()='TextBlock']").Cast<XmlNode>().ToArray();
            Check(texts[0].Attributes["Text"].Value == "{Binding HumanText}" && texts[0].Attributes["Grid.Column"].Value == "2" &&
                texts[1].Attributes["Text"].Value == "{Binding AiText}" && texts[1].Attributes["Grid.Column"].Value == "4", "human-first role headers");
        }
        foreach (string name in new[] { "AllowElevatedMoatHuman", "NoKillRewardHuman", "MultiplyGoodsGainHuman", "MultiplyGoodsGainInMoneyHuman", "HumanLordHealthPercent" })
        {
            XmlNode editor = nodes.Single(n => n.Attributes["IsChecked"] != null && n.Attributes["IsChecked"].Value == "{Binding " + name + ", Mode=TwoWay}" ||
                n.LocalName == "Slider" && n.Attributes["Value"] != null && n.Attributes["Value"].Value == "{Binding " + name + ", Mode=TwoWay}");
            XmlNode roleCell = new[] { editor }.Concat(Ancestors(editor)).First(n => n.Attributes != null && n.Attributes["Grid.Column"] != null &&
                (n.Attributes["Grid.Column"].Value == "2" || n.Attributes["Grid.Column"].Value == "4"));
            Check(roleCell.Attributes["Grid.Column"].Value == "2", "human editor occupies the left role column: " + name);
        }
        XmlNode context = nodes.Single(n => n.Attributes["Text"] != null && n.Attributes["Text"].Value == "{Binding EnemyProximityTitleText}");
        Check(Ancestors(context).Any(n => n.Attributes != null && n.Attributes["Visibility"] != null && n.Attributes["Visibility"].Value == "{Binding SettingsPresentation.EnemyProximityOptions.Visibility}"), "enemy-proximity context follows its search group");
        foreach (string text in new[] { "BuildingsProductionTitleText", "EconomyBuffsTitleText", "LordHealthTitleText", "BulldozeHelpText", "PlagueDamageSurfacesText", "MarketGoodPriceMultipliersHelpText" })
            Check(nodes.Any(n => n.Attributes["Text"] != null && n.Attributes["Text"].Value == "{Binding " + text + "}"), "retained contextual heading/explanation: " + text);
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
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[local-name()='CheckBox' and contains(@IsChecked,'EnableKnightDismount')]")).SetAttribute("MinHeight", "20"));
        NegativeXaml(document, d => ((XmlElement)d.SelectSingleNode("//*[@shared:ModSettingsSearch.Key='extra.knight-transform-gold']/*[local-name()='Grid.ColumnDefinitions']/*[1]", Namespaces(d))).SetAttribute("Width", "*"));
        NegativeXaml(document, d => { XmlNode n = d.SelectSingleNode("//*[local-name()='TextBlock' and @Text='{Binding EnemyProximityTitleText}']"); n.ParentNode.RemoveChild(n); });

        NegativeXaml(document,d=>{
            XmlNode role=d.SelectSingleNode("//*[@shared:ModSettingsSearch.Key='extra.popularity.RationsPopModifierNone.ai']",Namespaces(d));
            ((XmlElement)role).SetAttribute("Grid.Row","1");
        });
        NegativeXaml(document,d=>{
            XmlElement caption=(XmlElement)d.SelectSingleNode("//*[@shared:ModSettingsSearch.Key='extra.popularity.RationsPopModifierNone.ai']/../*[local-name()='TextBlock']",Namespaces(d));
            caption.SetAttribute("TextWrapping","Wrap");
        });

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
        Action<string, bool, string> search = (text, tooltips, key) => refresh.Invoke(presentation, new object[] { text, tooltips, key, entries, Enum.ToObject(catalog.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchStateFilter", true), 0), fixture });
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
        refresh.Invoke(presentation, new object[] { "", false, "", duplicates, Enum.ToObject(catalog.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchStateFilter", true), 0), fixture });
        Check((int)Get(market, "OptionCount") == marketCount, "duplicate catalog keys do not inflate counts");
        string goodKey = SettingsSearchFixtureBase.Goods.Last().SellSearchKey;
        search("", false, goodKey);
        Check(Visible(market, "ContentVisibility") && topics.Where(t => !ReferenceEquals(t, market)).All(t => !Visible(t, "Visibility")), "exact goods navigation opens only its topic");
        Check((int)Get(market, "OptionCount") == marketCount && !Visible(presentation, "BulkActionsVisibility"), "filter does not change count; bulk actions hidden");
        Check(presentationChanges.Contains("BulkActionsVisibility") && topicChanges.Contains("Visibility") && topicChanges.Contains("Title"), "search and refresh notify visible/localized bindings");
        Execute(presentation, "CollapseAllCommand"); Execute(presentation, "ExpandAllCommand"); Execute(market, "ToggleCommand"); Execute(topics[0], "ToggleCommand");
        search("", false, "");
        Check(Visible(topics[0], "ContentVisibility") && !Visible(market, "ContentVisibility") && topics.Skip(1).All(t => !Visible(t, "ContentVisibility")), "search and commands preserve manual state");
        search("", false, "extra.human-enemy-proximity-singleplayer");
        Check(Visible(Get(presentation, "EnemyProximityOptions"), "Visibility") && Visible(Get(presentation, "EnemySingleplayerOptions"), "Visibility") &&
            !Visible(Get(presentation, "EnemyMultiplayerOptions"), "Visibility") && !Visible(Get(presentation, "BuildingOptions"), "Visibility") &&
            !Visible(Get(presentation, "AiRebuildOptions"), "Visibility"), "single enemy-distance hit retains only its context and table row");
        search("Text.EnemyProximityTitleText", false, "");
        Check(Visible(Get(presentation, "EnemySingleplayerOptions"), "Visibility") && Visible(Get(presentation, "EnemyMultiplayerOptions"), "Visibility"), "contextual search titles find all enemy-distance role/mode values");
        search("", false, "extra.instant-horse");
        Check(Visible(Get(presentation, "KnightOptions"), "Visibility") && !Visible(Get(presentation, "SoldierOptions"), "Visibility") &&
            !Visible(Get(presentation, "LordOptions"), "Visibility"), "knight search hides unrelated subsection captions");
        search("", false, (string)Get(Get(presentation, "Units"), "Key"));
        Check(new[] { "KnightOptions", "SoldierOptions", "LordOptions" }.All(name => Visible(Get(presentation, name), "Visibility")), "whole-topic target retains every contextual group");
        search("", false, "extra.plague-duration");
        Check(Visible(Get(presentation, "PlagueOptions"), "Visibility") && !Visible(Get(presentation, "PlagueSurfacesOptions"), "Visibility") &&
            !Visible(Get(presentation, "HealerOptions"), "Visibility"), "duration search does not leave a detached plague-surface explanation");
        search("", false, (string)Get(market, "Key"));
        Check(Visible(market, "ContentVisibility"), "exact section navigation");
        search("Topic.Plague", false, "");
        Check(Visible(Get(presentation, "Plague"), "ContentVisibility") && !Visible(market, "Visibility"), "topic-title search");
        object popularity = Get(presentation, "Popularity");
        int popularityCount = (int)Get(popularity, "OptionCount");
        search("", false, "extra.popularity.FearNegative3.human");
        Check(Visible(popularity, "ContentVisibility") && topics.Where(t => !ReferenceEquals(t,popularity)).All(t => !Visible(t,"Visibility")), "fear navigation opens only Popularity");
        Check(Visible(Get(presentation,"PopularityFearOptions"),"Visibility") && Visible(Get(presentation,"PopularityNegativeFearOptions"),"Visibility") &&
            !Visible(Get(presentation,"PopularityPositiveFearOptions"),"Visibility") && !Visible(Get(presentation,"PopularityRationsOptions"),"Visibility"), "fear navigation retains only relevant subgroup headings");
        Check(Visible(Get(presentation,"PopularityRowFearNegative3"),"Visibility"),"exact role navigation retains its row caption");
        search("",false,"extra.popularity.FearNegative3.ai");
        Check(Visible(Get(presentation,"PopularityRowFearNegative3"),"Visibility") && !Visible(Get(presentation,"PopularityRowFearNegative2"),"Visibility"),"AI navigation retains only matching value row");
        Check((int)Get(popularity,"OptionCount")==popularityCount,"Popularity count stays complete during search");
        Check(Visible(Get(presentation,"PopularityNegativeFearAIOptions"),"Visibility") && !Visible(Get(presentation,"PopularityNegativeFearHumanOptions"),"Visibility"),"fear AI value search retains its activating group switch");
        search("",false,"extra.popularity.RationsPopModifierFull.human");
        Check(Visible(Get(presentation,"PopularityRationsHumanOptions"),"Visibility") && !Visible(Get(presentation,"PopularityRationsAIOptions"),"Visibility") && Visible(Get(presentation,"PopularityRowRationsPopModifierFull"),"Visibility"),"value search retains its own role group switch and caption");
        search("",false,"extra.popularity.Rations.ai");
        Check(Visible(Get(presentation,"PopularityRationsAIOptions"),"Visibility") && !Visible(Get(presentation,"PopularityRationsHumanOptions"),"Visibility"),"new group switches have independent search navigation");
        search("Topic.Popularity", false, "");
        Check(Visible(Get(presentation,"PopularityRationsOptions"),"Visibility") && Visible(Get(presentation,"PopularityPositiveFearOptions"),"Visibility"),"whole Popularity topic search retains its subsections");
        search("",false,"");
        Check(!Visible(popularity,"ContentVisibility"),"search restores closed manual Popularity state");
        search("Help.MarketSellPriceMultiplierHelpText", false, "");
        Check(!Visible(market, "Visibility"), "tooltip excluded unless requested");
        search("Help.MarketSellPriceMultiplierHelpText", true, "");
        Check(Visible(market, "ContentVisibility"), "tooltip search opens topic");
        search("no-such-setting-913", true, "");
        Check(topics.All(t => !Visible(t, "Visibility")), "no results hides all topics");
        object[] groups = ((IEnumerable)Get(presentation, "Groups")).Cast<object>().ToArray();
        Check(groups.All(group => !Visible(group, "Visibility")), "no results hides every subsection caption/row");
        search("", false, "");
        Check(topics.All(t => Visible(t, "Visibility")) && Visible(topics[0], "ContentVisibility"), "clearing no-results restores manual state");
        Check(groups.All(group => Visible(group, "Visibility")), "clearing search restores contextual groups");

        // Replacement snapshots cover newly available goods and language changes without persisting UI state.
        SettingsSearchFixtureBase.Labels.Market.Title = "Localized market topic";
        SettingsSearchFixtureBase.Goods = SettingsSearchFixtureBase.Goods.Concat(new[] { new SettingsSearchGood { Id = 999999 } }).ToArray();
        catalog.GetMethod("RegisterSource").Invoke(null, new object[] { fixture, path, null, "ExtraFeatures UI fixture", null });
        read[1] = null; catalog.GetMethod("TryGetEntries").Invoke(null, read); entries = read[1];
        search("Localized market topic", false, "");
        Check(Visible(market, "ContentVisibility") && (int)Get(market, "OptionCount") == marketCount + 2, "replaced catalog updates localized sections and dynamic counts");
        refresh.Invoke(presentation, new object[] { "", false, "", null, Enum.ToObject(catalog.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchStateFilter", true), 0), fixture });
        Check(topics.All(t => Visible(t, "Visibility")) && Visible(topics[0], "ContentVisibility"), "empty catalog preserves navigation/manual state without a native fallback");
        var activeState=Enum.ToObject(catalog.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchStateFilter",true),1);
        fixture.GetType().GetProperty("RationsHumanEnabled").SetValue(fixture,true,null);
        refresh.Invoke(presentation,new object[]{"",false,"extra.popularity.Rations.human",entries,activeState,fixture});
        Check(Visible(Get(presentation,"PopularityRationsHumanOptions"),"Visibility"),"active status filter includes the live group switch");
        fixture.GetType().GetProperty("RationsPopModifierNoneHuman").SetValue(fixture,0,null);
        refresh.Invoke(presentation,new object[]{"",false,"extra.popularity.RationsPopModifierNone.human",entries,activeState,fixture});
        Check(Visible(Get(presentation,"PopularityRationsHumanOptions"),"Visibility"),"status-filtered child values retain group activation context");
        fixture.GetType().GetProperty("RationsPopModifierNoneHuman").SetValue(fixture,-8,null);
        fixture.GetType().GetProperty("RationsHumanEnabled").SetValue(fixture,false,null);
        search("",false,"");
        Type stateType = catalog.Assembly.GetType("APIShared.ModSettings.ModSettingsSearchStateFilter", true);
        refresh.Invoke(presentation, new object[] { "", false, "", entries, Enum.ToObject(stateType, 1), fixture });
        Check(topics.All(t => !Visible(t, "Visibility")), "unknown and unchecked values cannot create active topics");
        ((SettingsSearchFixtureBase)fixture).EnableFearFactorNeutralization = true;
        refresh.Invoke(presentation, new object[] { "", false, "", entries, Enum.ToObject(stateType, 1), fixture });
        Check(Visible(topics[0], "ContentVisibility") && topics.Skip(1).All(t => !Visible(t, "Visibility")), "status-only filter opens the matching topic");
        ((SettingsSearchFixtureBase)fixture).EnableFearFactorNeutralization = false;
        refresh.Invoke(presentation, new object[] { "", false, "", entries, Enum.ToObject(stateType, 2), fixture });
        Check(Visible(topics[0], "ContentVisibility"), "inactive checkbox makes its topic searchable");
        search("", false, "");
        Check(Visible(topics[0], "ContentVisibility") && !Visible(market, "ContentVisibility"), "clearing status restores manual expansion");

        object converter = Activator.CreateInstance(mod.GetType("ExtraFeatures.SettingsTopicWidthConverter", true));
        MethodInfo convert = converter.GetType().GetMethod("Convert");
        Type widthType = type.GetProperty("BulkActionsVisibility").PropertyType.Assembly.GetType("Noesis.FrameworkElement", true).GetProperty("Width").PropertyType;
        Assembly noesis = type.GetProperty("BulkActionsVisibility").PropertyType.Assembly;
        Check(noesis.GetType("Noesis.Grid", true).GetMethod("GetIsSharedSizeScope") != null &&
            noesis.GetType("Noesis.DefinitionBase", true).GetProperty("SharedSizeGroup").PropertyType == typeof(string), "installed Noesis exposes the shared-column XAML contract");
        foreach (object input in new object[] { null, 0d, 12f, 640d, double.NaN, double.PositiveInfinity, double.MaxValue })
        {
            object result = convert.Invoke(converter, new[] { input, (object)widthType, null, CultureInfo.InvariantCulture });
            Check(result is float && widthType.IsInstanceOfType(result) && (float)result >= 0 && !float.IsInfinity((float)result) && !float.IsNaN((float)result), "width converter returns finite Noesis Single");
        }
        Check((float)convert.Invoke(converter, new object[] { 640d, widthType, null, CultureInfo.InvariantCulture }) == 620f, "width accounts for page margin");
        XmlNode popularityTopic=document.SelectNodes("//*[@shared:ModSettingsSearch.IsSection='True']",Namespaces(document)).Cast<XmlNode>()
            .Single(n=>Search(n,"SectionKey")=="extra.topic.popularity");
        var parameter=Regex.Match(popularityTopic.Attributes["Width"].Value,@"ConverterParameter=(\d+)");
        Check(parameter.Success,"Popularity topic declares a minimum scroll extent");
        float minimum=float.Parse(parameter.Groups[1].Value,CultureInfo.InvariantCulture);
        foreach(object input in new object[]{0d,320d,640d,960d,double.NaN,double.PositiveInfinity}) {
            float extent=(float)convert.Invoke(converter,new object[]{input,widthType,parameter.Groups[1].Value,CultureInfo.InvariantCulture});
            Check(extent>=minimum && !float.IsNaN(extent) && !float.IsInfinity(extent),"narrow or unmeasured viewports retain the complete table scroll extent");
        }
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
            string[] layout = File.ReadAllLines(file).Where(line => line.StartsWith("ExtraFeatures.SettingsLayout.", StringComparison.Ordinal)).ToArray();
            string[] layoutKeys = new[] { "SoldierBehavior", "KnightTransformation", "GateControl", "ReopenDelay", "ClosingDistance", "Singleplayer", "Multiplayer" }
                .Select(key => "ExtraFeatures.SettingsLayout." + key).OrderBy(key => key).ToArray();
            Check(layout.Select(line => line.Split('=')[0]).OrderBy(key => key).SequenceEqual(layoutKeys) && layout.All(line => line.IndexOf('=') < line.Length - 1), "compact layout locales are complete/unique/nonempty");
        }
    }
}

public class SettingsSearchFixtureBase
{
    public bool EnableFearFactorNeutralization { get; set; }
    public bool RationsHumanEnabled { get; set; }
    private int rationPoints = -8;
    public int RationsPopModifierNoneHuman { get { return rationPoints; } set { rationPoints = value; } }
    public static SettingsSearchLabels Labels;
    public static SettingsSearchGood[] Goods;
    public SettingsSearchLabels SettingsPresentation { get { return Labels; } }
    public SettingsSearchGood[] MarketGoodPriceItems { get { return Goods; } }
    public string HumanEnemyProximitySingleplayerSearchTitle { get { return "Text.EnemyProximityTitleText - Text.HumanEnemyProximitySingleplayerText"; } }
    public string HumanEnemyProximityMultiplayerSearchTitle { get { return "Text.EnemyProximityTitleText - Text.HumanEnemyProximityMultiplayerText"; } }
    public string AIEnemyProximitySingleplayerSearchTitle { get { return "Text.EnemyProximityTitleText - Text.AIEnemyProximitySingleplayerText"; } }
    public string AIEnemyProximityMultiplayerSearchTitle { get { return "Text.EnemyProximityTitleText - Text.AIEnemyProximityMultiplayerText"; } }
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
        Popularity = new SettingsSearchTopicLabel { Title = "Topic.Popularity" };
    }
    public SettingsSearchTopicLabel Units { get; private set; }
    public SettingsSearchTopicLabel Gatehouses { get; private set; }
    public SettingsSearchTopicLabel Building { get; private set; }
    public SettingsSearchTopicLabel Economy { get; private set; }
    public SettingsSearchTopicLabel Demolition { get; private set; }
    public SettingsSearchTopicLabel Plague { get; private set; }
    public SettingsSearchTopicLabel Market { get; private set; }
    public SettingsSearchTopicLabel Popularity { get; private set; }
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
