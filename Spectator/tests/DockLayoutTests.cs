using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using SHCDESE.API.Components.Noesis.XAML;

// Run the real Extender patcher with only its logger replaced; this is not a Noesis render test.
namespace SHCDESE.Logging
{
    internal static class LogHelper
    {
        internal static readonly List<string> Failures = new List<string>();
        internal static void Information(string text) { }
        internal static void Warning(string text) { Failures.Add(text); }
        internal static void Error(Exception error, string text) { Failures.Add(text + " " + error); }
    }
}
internal static class DockLayoutTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Main(string[] args)
    {
        string original = File.ReadAllText(args[0]);
        var patch = XDocument.Load(args[1]);
        var before = XDocument.Parse(original);
        var manager = XamlPatcher.CreateNamespaceManager(before);
        var operations = new List<XmlPatchOperation>();
        foreach (var op in patch.Root.Elements("Operation"))
        {
            var type = (XmlPatchType)Enum.Parse(typeof(XmlPatchType), (string)op.Attribute("Type"));
            Check(type == XmlPatchType.Add || type == XmlPatchType.AddNamespace, "Dock patch cannot rewrite Vanilla controls.");
            if (type == XmlPatchType.Add)
                Check(before.XPathSelectElements((string)op.Attribute("XPath"), manager).Count() == 1, "Exactly one live Vanilla host.");
            operations.Add(new XmlPatchOperation { Type = type, XPath = (string)op.Attribute("XPath"),
                AttributeName = (string)op.Attribute("AttributeName"), Value = (string)op.Attribute("Value"),
                Content = op.Element("Content")?.Elements().Single().ToString() });
        }
        var after = XDocument.Parse(XamlPatcher.ApplyPatches(original, operations));
        Check(SHCDESE.Logging.LogHelper.Failures.Count == 0, string.Join("\n", SHCDESE.Logging.LogHelper.Failures));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var layer = after.Descendants().Single(e => (string)e.Attribute(x + "Name") == "SpectatorDockLayer");
        var header = after.Descendants().Single(e => (string)e.Attribute(x + "Name") == "SpectatorPlacementHeader");
        Check((string)header.Attribute("Visibility") == "Collapsed", "Header starts hidden independently.");
        Check((string)header.Parent.Attribute(x + "Name") == "LayoutRoot", "Header shares the Vanilla mask parent, not its lower child layer.");
        var masks = header.Parent.Elements().Where(e => e.Attribute("Background") != null && e.Attribute("Tag") != null).ToArray();
        Check(masks.Length > 0, "Fixture includes actual top-edge Vanilla masks.");
        foreach (var mask in masks)
            Check((int)header.Attribute("Panel.ZIndex") > ((int?)mask.Attribute("Panel.ZIndex") ?? 0), "Full header rectangle is above native hit masks.");
        Check(header.Attribute("Width") == null && (double)header.Attribute("Height") <= 24, "Header measures only its buttons and cannot capture the build area.");
        var own = layer.DescendantsAndSelf().Concat(header.DescendantsAndSelf()).ToArray();
        Check((string)layer.Attribute("Visibility") == "Collapsed", "No visible content before confirmed session.");
        Check(layer.Parent.Attribute("DataContext") == null, "Vanilla host DataContext is preserved.");
        Check(after.Root.GetNamespaceOfPrefix("specui")?.NamespaceName == "clr-namespace:Spectator;assembly=Spectator", "Attached tooltip type survives actual patcher namespace sanitization.");
        var names = own.Select(e => (string)e.Attribute(x + "Name")).Where(n => n != null).ToArray();
        Check(names.Distinct().Count() == names.Length, "Unique injected names.");
        foreach (string prefix in new[] { "DockOwner", "DockTarget", "PlacementOwner" })
            for (int player = 1; player <= 8; player++) Check(names.Contains(prefix + player), "Every player remains addressable: " + prefix);
        var commands = own.Select(e => (string)e.Attribute("CommandParameter")).Where(c => c != null).ToArray();
        var goods = commands.Where(c => c.Length == 3 && c[0] == 'G' && char.IsDigit(c[1])).ToArray();
        Check(goods.Length == 21 && goods.Distinct().Count() == 21, "Every Vanilla good exactly once.");
        foreach (string command in new[] { "DockPlacement", "DockStop", "DockHome", "DockGoods", "DockOrders", "TogglePowers", "Credit", "ResetAmount", "Attack", "Defend", "Cancel" })
            Check(commands.Contains(command), "Missing tool action " + command);
        foreach (var element in own.Where(e => new[] {"Button", "CheckBox", "TextBox"}.Contains(e.Name.LocalName)))
            Check((string)element.Attribute("ToolTipService.ShowDuration") == "60000" && element.Elements().Any(e => e.Name.LocalName.EndsWith(".ToolTip")), "Interactive tooltip contract.");
        // Removing only our root recovers the unchanged Vanilla visual tree.
        var amount = own.Single(e => (string)e.Attribute(x + "Name") == "DockAmount");
        Check(amount.Descendants().Any(e => e.Name.LocalName == "ScrollViewer" && (string)e.Attribute("VerticalScrollBarVisibility") == "Disabled" && (string)e.Attribute("HorizontalScrollBarVisibility") == "Disabled"), "Compact input has no inherited scrolling content host.");
        Check((string)amount.Attribute("Padding") == "4,1" && (double)amount.Attribute("FontSize") <= (double)amount.Attribute("Height") - 4, "Input leaves adequate vertical text space.");
        layer.Remove(); header.Remove();
        Check(XNode.DeepEquals(before.Root.Elements().First(), after.Root.Elements().First()), "Vanilla resources preserved.");
        Check(XNode.DeepEquals(before.Root.Elements().Last(), after.Root.Elements().Last()), "Vanilla HUD tree, bindings, clips and commands preserved.");
        Console.WriteLine("PASS: actual Extender patch application, namespace retention, unchanged Vanilla tree, all players/actions/goods and tooltip coverage.");
    }
}
