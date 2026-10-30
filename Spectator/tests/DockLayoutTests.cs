using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using Spectator;
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
    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    private static void CheckNativeRasters(string hudPath, string atlasPath, string rosterPath)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var hud = XDocument.Load(hudPath);
        var build = hud.Descendants().Single(e => (string)e.Attribute(x+"Name") == "BuildMenuGrid");
        var buttons = build.Descendants().Where(e => e.Name.LocalName == "Button").ToDictionary(e => (string)e.Attribute("Name") ?? (string)e.Attribute(x+"Name"));
        var sprites = Directory.GetFiles(Path.GetDirectoryName(atlasPath), "*.xaml").SelectMany(path => XDocument.Load(path).Descendants()).Where(e => e.Name.LocalName == "CroppedBitmap").ToDictionary(e => (string)e.Attribute(x+"Key"));
        var roster = File.ReadAllLines(rosterPath).Select(line => line.Split('\t')).ToArray();
        var bindings = roster.Where(parts => parts[0] == "@binding").GroupBy(parts => parts[1]).ToDictionary(group => group.Key, group => group.Select(parts => parts[2]).ToArray());
        var animations = hud.Descendants().Where(e => (string)e.Attribute("Storyboard.TargetName") == "BuildMenuGrid").ToArray();
        Check(animations.Length == 2 && animations.All(e => (string)e.Elements().Last().Attribute("Value") == "16"),
            "Both native swish animations must retain the audited resting X=16; their offsets are never rewritten.");
        var pages = roster.Where(parts => parts[0] != "@binding").GroupBy(parts => parts[0]);
        int count = 0, pageCount = 0; bool tall = false;
        foreach (var page in pages)
        {
            var rects = new List<ToolbarRect>();
            foreach (var entry in page)
            {
                Check(buttons.TryGetValue(entry[1],out var button), "Every native page button belongs to the borrowed raster: "+entry[1]);
                float width = button.Attribute("Width") == null ? 0 : Number((string)button.Attribute("Width"));
                float height = button.Attribute("Height") == null ? 0 : Number((string)button.Attribute("Height"));
                if (width <= 0 || height <= 0)
                {
                    var sprite = button.Attributes().First(a => a.Name.LocalName == "PropEx.Sprite1").Value;
                    string[] keys = sprite.StartsWith("{Binding ") ? bindings[sprite.Substring("{Binding ".Length).TrimEnd('}')]
                        : new[] { sprite.Substring("{StaticResource ".Length).TrimEnd('}') };
                    float naturalWidth = 0, naturalHeight = 0;
                    foreach (string key in keys)
                    {
                        Check(sprites.TryGetValue(key,out var bitmap), "Native sprite dimensions must be available: "+key);
                        var bounds = ((string)bitmap.Attribute("SourceRect")).Split(',').Select(Number).ToArray();
                        naturalWidth = Math.Max(naturalWidth,bounds[2]); naturalHeight = Math.Max(naturalHeight,bounds[3]);
                    }
                    if (width <= 0) width = height > 0 ? naturalWidth * height / naturalHeight : naturalWidth;
                    if (height <= 0) height = naturalHeight * width / naturalWidth;
                }
                var margins = ((string)button.Attribute("Margin") ?? "0,0,0,0").Split(',').Select(Number).ToArray();
                float top = margins.Length > 1 ? margins[1] : margins[0];
                tall |= height > 90 || top < 0;
                rects.Add(new ToolbarRect(margins[0],top,width,height)); count++;
            }
            var source = new ToolbarRect(rects.Min(r=>r.X),rects.Min(r=>r.Y),rects.Max(r=>r.Right)-rects.Min(r=>r.X),rects.Max(r=>r.Bottom)-rects.Min(r=>r.Y));
            var target = new ToolbarRect(130,72,496,ToolbarLayout.Body-4);
            var fit = ToolbarLayout.FitRaster(source,target);
            Check(fit.X >= target.X-.001f && fit.Y >= target.Y-.001f && fit.Right <= target.Right+.001f && fit.Bottom <= target.Bottom+.001f,
                "Complete native page including tall symbols fits its reserved area: "+page.Key);
            float scale = ToolbarLayout.RasterScale(source,target);
            foreach (var rect in rects)
            {
                float left = fit.X+(rect.X-source.X)*scale, top = fit.Y+(rect.Y-source.Y)*scale;
                Check(left >= target.X-.001f && left+rect.Width*scale <= target.Right+.001f && top >= target.Y-.001f && top+rect.Height*scale <= target.Bottom+.001f,
                    "Image and hit rectangle fit proportionally on native page "+page.Key);
            }
            pageCount++;
        }
        Check(tall && pageCount > 15 && count > 100,"Exercise real construction, unit, submenu and alternate Lord artwork geometry.");
        Console.WriteLine("PASS: native page/sprite geometry: "+pageCount+" populated pages/variants, "+count+" button rectangles; both swish animations preserved. Rendering remains an ingame check.");
    }
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
        var own = canvas.DescendantsAndSelf().ToArray();
        var names = own.Select(e => (string)e.Attribute(x+"Name")).Where(n => n != null).ToArray();
        Check(names.Distinct().Count() == names.Length, "Unique names in the single overlay.");
        var commands = own.Select(e => (string)e.Attribute("CommandParameter")).Where(c => c != null).ToArray();
        foreach (string command in new[] { "Placement","Goods","Orders","TogglePowers","Collapse","Credit","ResetAmount","Attack","Defend","Cancel" })
            Check(commands.Contains(command), "Missing action "+command);
        var goods = commands.Where(c => c.Length == 3 && c[0] == 'G' && char.IsDigit(c[1])).ToArray();
        Check(goods.Length == 21 && goods.Distinct().Count() == 21,"Every good exactly once.");
        foreach (var e in own.Where(e => new[] {"Button","CheckBox","TextBox"}.Contains(e.Name.LocalName)))
            Check((string)e.Attribute("ToolTipService.ShowDuration") == "60000" && e.Elements().Any(c => c.Name.LocalName.EndsWith(".ToolTip")), "Every input retains a styled long tooltip.");
        CheckNativeRasters(args[2],args[3],args[4]);
        canvas.Remove();
        // Full semantic tree comparison, not merely an absence check: no original binding,
        // input mask, resource, HUD or command may change when applying this production patch.
        Check(XNode.DeepEquals(before,after), "The complete Vanilla screen must remain unchanged beneath the overlay.");
        Console.WriteLine("PASS: real Extender patcher, complete Vanilla tree preserved, single overlay and all actions/goods/tooltips.");
    }
}
