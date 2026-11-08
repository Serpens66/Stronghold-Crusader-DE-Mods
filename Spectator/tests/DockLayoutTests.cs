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
        var parentStyle=canvas.Elements().Single(e=>e.Name.LocalName=="Canvas.Style");
        Check(parentStyle.Descendants().Any(e=>e.Name.LocalName=="Setter" && (string)e.Attribute("Property")=="Visibility" &&
            ((string)e.Attribute("Value")??"").Contains("Show_HUD_Extras") && ((string)e.Attribute("Value")??"").Contains("FallbackValue=Collapsed")),
            "The release Extras gate belongs to the parent and fails closed before binding.");
        foreach(string flag in new[]{"Show_HUD_Briefing","Show_HUD_MissionOver","Show_HUD_IngameMenu"})
            Check(parentStyle.Descendants().Any(e=>e.Name.LocalName=="DataTrigger" && ((string)e.Attribute("Binding")??"").Contains(flag) &&
                e.Descendants().Any(s=>(string)s.Attribute("Property")=="Visibility" && (string)s.Attribute("Value")=="Collapsed")),
                "The whole surface, including input and previews, hides for "+flag);
        Check(ReferenceEquals(nativeHud.Parent,canvas.Parent), "Overlay and native HUD must use the same stacking parent.");
        int nativeLayer = (int?)nativeHud.Attribute("Panel.ZIndex") ?? 0;
        Check((int)canvas.Attribute("Panel.ZIndex") > nativeLayer, "Overlay draws above the native HUD at the common ancestor.");
        var commonSurface = canvas.Descendants().Single(e => (string)e.Attribute(x+"Name") == "SpectatorEditorHumanPanel");
        Check((string)commonSurface.Attribute("Opacity")=="0" && (string)commonSurface.Attribute("IsHitTestVisible")=="False",
            "Before the first valid HUD layout, neither an unpositioned menu nor its hit surface may appear.");
        foreach(string name in new[]{"SpectatorSnapFrame","SpectatorToolSnapFrame","SpectatorSnapGrip"})
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
        var dateCanvas = after.Descendants().Single(e => (string)e.Attribute(x + "Name") == "SpectatorHiddenDateCanvas");
        var date = dateCanvas.Elements().Single();
        Check(ReferenceEquals(dateCanvas.Parent, nativeHud.Parent) && dateCanvas.Attribute("Background") == null &&
            date.Name.LocalName == "TextBlock" && date.Attribute("Background") != null && (string)date.Attribute("Visibility") == "Collapsed",
            "The hidden-HUD date is independent of the native HUD and owns only a collapsed date hit surface at startup.");
        dateCanvas.Remove();
        canvas.Remove();
        // Full semantic tree comparison, not merely an absence check: no original binding,
        // input mask, resource, HUD or command may change when applying this production patch.
        Check(XNode.DeepEquals(before,after), "The complete Vanilla screen must remain unchanged beneath the overlay.");
        // Use actual Vanilla margins to prove the relocated control leaves every
        // construction/unit tab and the old trumpet-adjacent slot available.
        var hud = XDocument.Load(args[2]);
        Func<string,XElement> named = name => hud.Descendants().Single(e => (string)e.Attribute("Name")==name || (string)e.Attribute(x+"Name")==name);
        Func<XElement,int,float> margin = (e,index) => {
            var parts=((string)e.Attribute("Margin")??"0").Split(',');
            return float.Parse(parts.Length==1?parts[0]:parts[index],System.Globalization.CultureInfo.InvariantCulture);
        };
        var tabs=named("BottomTabs1"); float cursor=margin(tabs,0);
        var nativeTabs=new List<Spectator.ToolbarRect>();
        foreach(var tab in tabs.Elements().Where(e=>e.Name.LocalName=="RadioButton"))
        {
            if((string)tab.Attribute("Name")=="RadioButtonMERuins") continue;
            cursor+=margin(tab,0);
            float w=(float)tab.Attribute("Width"),h=(float)tab.Attribute("Height");
            nativeTabs.Add(new Spectator.ToolbarRect(cursor,166-h,w,h));
            cursor+=w+margin(tab,2);
        }
        Check(nativeTabs.Count==10,"The placement fixture must exercise six construction and four unit tabs.");
        var trumpet=named("ButtonAllies");
        var allyBounds=new Spectator.ToolbarRect(margin(trumpet,0),166-(float)trumpet.Attribute("Height"),(float)trumpet.Attribute("Width"),(float)trumpet.Attribute("Height"));
        var oldSlot=new Spectator.ToolbarRect(allyBounds.X-62,allyBounds.Bottom-24,60,22);
        foreach(float scale in new[]{.75f,1f,1.5f,2f,3f}) foreach(int count in Enumerable.Range(1,8))
        {
            var pose=Spectator.ToolbarLayout.Arrange(new Spectator.ToolbarRect(0,0,914*scale,166*scale),
                new Spectator.ToolbarRect(114*scale,22*scale,800*scale,144*scale),count,false);
            Check(Math.Abs(pose.Roster.Y-pose.Control.Bottom)<.001 &&
                Math.Abs(pose.Control.X+pose.Control.Width/2-pose.Roster.X-pose.Roster.Width/2)<.001,
                "Tools/Stop is centered above the unchanged portrait raster.");
            foreach(var tab in nativeTabs) Check(!pose.Control.Intersects(new Spectator.ToolbarRect(tab.X*scale,tab.Y*scale,tab.Width*scale,tab.Height*scale)),
                "The relocated control leaves every actual construction/unit tab clear.");
            Check(!pose.Bounds.Intersects(new Spectator.ToolbarRect(oldSlot.X*scale,oldSlot.Y*scale,oldSlot.Width*scale,oldSlot.Height*scale)),
                "The closed toolbar owns no background, bounds or click area in the former native slot.");
        }
        var slot=own.Single(e=>(string)e.Attribute(x+"Name")=="SpectatorControlSlot");
        Check(slot.Descendants().Any(e=>(string)e.Attribute(x+"Name")=="SpectatorExpand") &&
            slot.Descendants().Any(e=>(string)e.Attribute(x+"Name")=="SpectatorStopPlacement"),"Tools and Stop share the relocated roster control.");
        Console.WriteLine("PASS: real Extender patcher, complete Vanilla tree preserved, single overlay and all actions/goods/tooltips.");
    }
}
