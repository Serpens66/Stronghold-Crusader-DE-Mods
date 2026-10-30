using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml.XPath;
using SHCDESE.API.Components.Noesis.XAML;
namespace SHCDESE.Logging
{
    internal static class LogHelper
    {
        internal static readonly List<string> Failures = new List<string>();
        internal static void Information(string text) { }
        internal static void Warning(string text) { Failures.Add(text); }
        internal static void Error(Exception error, string text) { Failures.Add(text + error); }
    }
}
internal static class DockLayoutTests
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static void Main(string[] args)
    {
        string original = File.ReadAllText(args[0]);
        var before = XDocument.Parse(original);
        var patch = XDocument.Load(args[1]);
        var manager = XamlPatcher.CreateNamespaceManager(before);
        var operations = new List<XmlPatchOperation>();
        foreach (var op in patch.Root.Elements("Operation"))
        {
            var type = (XmlPatchType)Enum.Parse(typeof(XmlPatchType), (string)op.Attribute("Type"));
            Check(type == XmlPatchType.Add, "The overlay must only add its own subtree.");
            Check(before.XPathSelectElements((string)op.Attribute("XPath"), manager).Count() == 1, "Unique Vanilla host.");
            operations.Add(new XmlPatchOperation { Type = type, XPath = (string)op.Attribute("XPath"),
                Content = op.Element("Content").Elements().Single().ToString() });
        }
        var after = XDocument.Parse(XamlPatcher.ApplyPatches(original, operations));
        Check(SHCDESE.Logging.LogHelper.Failures.Count == 0, string.Join("\n",SHCDESE.Logging.LogHelper.Failures));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var canvas = after.Descendants().Single(e => (string)e.Attribute(x+"Name") == "SpectatorEditorHumanCanvas");
        var nativeHud = after.Descendants().Single(e => (string)e.Attribute(x+"Name") == "MainHUD");
        Check(ReferenceEquals(nativeHud.Parent,canvas.Parent), "Overlay and native HUD must use the same stacking parent.");
        int nativeLayer = (int?)nativeHud.Attribute("Panel.ZIndex") ?? 0;
        Check((int)canvas.Attribute("Panel.ZIndex") > nativeLayer, "Overlay draws above the native HUD at the common ancestor.");
        var commonSurface = canvas.Descendants().Single(e => (string)e.Attribute(x+"Name") == "SpectatorEditorHumanPanel");
        Check((string)commonSurface.Attribute("Opacity")=="0" && (string)commonSurface.Attribute("IsHitTestVisible")=="False",
            "Before the first valid HUD layout, neither an unpositioned menu nor its hit surface may appear.");
        foreach(string name in new[]{"SpectatorSnapFrame","SpectatorToolSnapFrame"})
        {
            var guide=canvas.Descendants().Single(e => (string)e.Attribute(x+"Name") == name);
            Check(guide.Ancestors().Contains(commonSurface),"Guides must inherit common HUD visibility/opacity: "+name);
            Check((string)guide.Attribute("IsHitTestVisible")=="False","Guides cannot enlarge the effective input surface.");
        }
        Check((string)commonSurface.Attribute("ClipToBounds")=="False","Inverse-translated dock guides remain visible while dragging away.");
        var own = canvas.DescendantsAndSelf().ToArray();
        var names = own.Select(e => (string)e.Attribute(x+"Name")).Where(n => n != null).ToArray();
        Check(names.Distinct().Count() == names.Length, "Unique names in the single overlay.");
        var commands = own.Select(e => (string)e.Attribute("CommandParameter")).Where(c => c != null).ToArray();
        foreach (string command in new[] { "Placement","Goods","Orders","TogglePowers","Collapse","Credit","Debit","ResetAmount","Attack","Defend","Cancel" })
            Check(commands.Contains(command), "Missing action "+command);
        var goods = commands.Where(c => c.Length == 3 && c[0] == 'G' && char.IsDigit(c[1])).ToArray();
        Check(goods.Length == 21 && goods.Distinct().Count() == 21,"Every good exactly once.");
        foreach (var e in own.Where(e => new[] {"Button","CheckBox","TextBox"}.Contains(e.Name.LocalName)))
            Check((string)e.Attribute("ToolTipService.ShowDuration") == "60000" && e.Elements().Any(c => c.Name.LocalName.EndsWith(".ToolTip")), "Every input retains a styled long tooltip.");
        canvas.Remove();
        // Full semantic tree comparison, not merely an absence check: no original binding,
        // input mask, resource, HUD or command may change when applying this production patch.
        Check(XNode.DeepEquals(before,after), "The complete Vanilla screen must remain unchanged beneath the overlay.");
        Console.WriteLine("PASS: real Extender patcher, complete Vanilla tree preserved, single overlay and all actions/goods/tooltips.");
    }
}
