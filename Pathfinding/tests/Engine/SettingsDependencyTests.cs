using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Pathfinding.Settings.Tests
{
    internal static class SettingsDependencyTests
    {
        internal static void Run(string root)
        {
            string modDirectory = Path.Combine(root, "Pathfinding");
            int assertions = 0;
            void check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
            // Execute the production getters/notifications with a small settings boundary double.
            // This checks binding behavior, not Noesis rendering or native input handling.
            string production = File.ReadAllText(Path.Combine(modDirectory, "src/Settings/PathfindingViewModel.Dependencies.cs"));
            const string boundary = @"namespace Pathfinding {
                public sealed partial class PathfindingViewModel {
                    public bool EnableMod = true, EnablePathfindingCore, AvoidFire, AvoidPlague, EscapePlague;
                    public bool EnableFriendlyMoatTraversal = true, AnchorSingleMoves = true, EscapeFire = true, EscapePlagueRaised = true;
                    public bool FireWaitIndefinitely, PlagueWaitIndefinitely;
                    public int FireWaitSeconds = 37, FireReturnSeconds = 119, FireDetourPercent = 75, FireDetourMinTiles = 14;
                    public int PlagueWaitSeconds = 83, PlagueReturnSeconds = 173;
                    public bool HostSettingsEnabled = true, CanEditHostSettings = true;
                    public readonly System.Collections.Generic.List<string> Changes = new System.Collections.Generic.List<string>();
                    private void OnPropertyChanged(string name) { Changes.Add(name); }
                    public void Changed(string name) { NotifyDependentSettingsChanged(name); }
                }
            }
            namespace Pathfinding.UnitCommands { }";
            {
                var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
                    .Select(path => MetadataReference.CreateFromFile(path));
                var compilation = CSharpCompilation.Create("SettingsDependencies", new[] {
                    CSharpSyntaxTree.ParseText(production), CSharpSyntaxTree.ParseText(boundary),
                    CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(modDirectory, "src/Engine/EngineOptions.cs"))) }, references,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                using var bytes = new MemoryStream();
                var result = compilation.Emit(bytes);
                if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics));
                Type type = Assembly.Load(bytes.ToArray()).GetType("Pathfinding.PathfindingViewModel");
                object model = Activator.CreateInstance(type);
                Type optionsType = type.Assembly.GetType("Pathfinding.Engine.EngineOptions");
                var capture = optionsType.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic);
                object Captured(string property) => optionsType.GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(capture.Invoke(null, new object[] { model, "fixture-plugin" }));
                void Set(string name, bool value) => type.GetField(name).SetValue(model, value);
                bool Read(string name) => type.GetProperty(name) != null
                    ? (bool)type.GetProperty(name).GetValue(model) : (bool)type.GetField(name).GetValue(model);
                var document = XDocument.Load(Path.Combine(modDirectory, "Override/ScriptExtenderUI/PathfindingSettings.xaml"));
                XNamespace shared = "clr-namespace:Shared;assembly=APIShared";
                XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
                XElement Control(string setting) => document.Descendants().Single(e =>
                    (string)e.Attribute("IsChecked") == "{Binding " + setting + ", Mode=TwoWay}");
                bool LocalEnabled(XElement element)
                {
                    string binding = (string)element.Attribute("IsEnabled");
                    if (binding == null) return true;
                    return Read(binding.Substring("{Binding ".Length).TrimEnd('}'));
                }
                bool EffectiveEnabled(XElement element) => element.AncestorsAndSelf().All(LocalEnabled);
                var dependentControls = new Dictionary<string, string> {
                    { "AnchorSingleMoves", "CoreOptionsEnabled" }, { "EnableFriendlyMoatTraversal", "CoreOptionsEnabled" },
                    { "AvoidFire", "CoreOptionsEnabled" }, { "AvoidPlague", "CoreOptionsEnabled" },
                    { "EscapeFire", "FireOptionsEnabled" }, { "EscapePlague", "PlagueOptionsEnabled" },
                    { "EscapePlagueRaised", "PlagueRaisedOptionsEnabled" },
                    { "FireWaitIndefinitely", "FireOptionsEnabled" }, { "PlagueWaitIndefinitely", "PlagueOptionsEnabled" }
                };
                foreach (var pair in dependentControls)
                {
                    check((string)Control(pair.Key).Attribute("IsEnabled") == "{Binding " + pair.Value + "}",
                        "child checkbox uses its complete dependency: " + pair.Key);
                    check((string)Control(pair.Key).Parent.Attribute("Style") == "{StaticResource DependentGrid}",
                        "child dimming preserves the checkbox's existing template: " + pair.Key);
                }
                var dependentEditors = new Dictionary<string, string> {
                    { "FireWaitSeconds", "FireOptionsEnabled" }, { "FireReturnSeconds", "FireOptionsEnabled" },
                    { "PlagueWaitSeconds", "PlagueOptionsEnabled" }, { "PlagueReturnSeconds", "PlagueOptionsEnabled" },
                    { "FireDetourPercent", "DetourSettingsEnabled" }, { "FireDetourMinTiles", "DetourSettingsEnabled" }
                };
                foreach (var pair in dependentEditors)
                {
                    XElement editor = document.Descendants().Single(e => (string)e.Attribute("Value") == "{Binding " + pair.Key + ", Mode=TwoWay}");
                    check((string)editor.Parent.Attribute("IsEnabled") == "{Binding " + pair.Value + "}" &&
                        (string)editor.Parent.Attribute("Style") == "{StaticResource DependentGrid}",
                        "hazard value editor shares its complete dependency and dimming: " + pair.Key);
                }
                check(!(bool)Captured("FireWaitIndefinitely") && !(bool)Captured("PlagueWaitIndefinitely"),
                    "old settings without the optional wait modes keep bounded waiting");
                for (int flags = 0; flags < 64; flags++)
                {
                    bool core = (flags & 1) != 0, fire = (flags & 2) != 0, plague = (flags & 4) != 0, escape = (flags & 8) != 0;
                    bool fireForever = (flags & 16) != 0, plagueForever = (flags & 32) != 0;
                    Set("EnablePathfindingCore", core); Set("AvoidFire", fire); Set("AvoidPlague", plague); Set("EscapePlague", escape);
                    Set("FireWaitIndefinitely", fireForever); Set("PlagueWaitIndefinitely", plagueForever);
                    check(!(bool)Captured("AvoidFire") && !(bool)Captured("AvoidPlague"), "release capture overrides persisted hazard opt-ins " + flags);
                    check((bool)Captured("EnablePathfindingCore") == core && (bool)Captured("AnchorSingleMoves") &&
                        (bool)Captured("EnableFriendlyMoatTraversal") && (string)Captured("PluginDirectory") == "fixture-plugin",
                        "release gate preserves independent core options " + flags);
                    check((bool)Captured("FireWaitIndefinitely") == fireForever && (bool)Captured("PlagueWaitIndefinitely") == plagueForever,
                        "mission capture preserves independent wait modes while hazards remain gated " + flags);
                    check((int)Captured("FireWaitSeconds") == 37 && (int)Captured("FireReturnSeconds") == 119 &&
                        (int)Captured("FireDetourPercent") == 75 && (int)Captured("FireDetourMinTiles") == 14 && (bool)Captured("EscapeFire") &&
                        (int)Captured("PlagueWaitSeconds") == 83 && (int)Captured("PlagueReturnSeconds") == 173 &&
                        (bool)Captured("EscapePlague") == escape && (bool)Captured("EscapePlagueRaised"), "release gate preserves stored parameters " + flags);
                    check(Read("CoreOptionsEnabled") == core && Read("FireOptionsEnabled") == (core && fire) &&
                        Read("PlagueOptionsEnabled") == (core && plague) && Read("PlagueRaisedOptionsEnabled") == (core && plague && escape) &&
                        Read("DetourSettingsEnabled") == (core && (fire || plague)), "dependency truth table " + flags);
                    check(EffectiveEnabled(Control("EscapePlagueRaised")) == (core && plague && escape) &&
                        EffectiveEnabled(Control("EscapeFire")) == (core && fire) &&
                        EffectiveEnabled(Control("FireWaitIndefinitely")) == (core && fire) &&
                        EffectiveEnabled(Control("PlagueWaitIndefinitely")) == (core && plague), "hidden UI binding fixture " + flags);
                    Set("CanEditHostSettings", false);
                    check(dependentControls.Keys.All(key => !EffectiveEnabled(Control(key))), "host permission also disables children " + flags);
                    Set("CanEditHostSettings", true);
                    check(Read("AvoidFire") == fire && Read("AvoidPlague") == plague && Read("EscapePlague") == escape,
                        "disabled parents preserve stored child values " + flags);
                }
                Set("EnableMod", false);
                check(!(bool)Captured("EnablePathfindingCore"), "release capture respects disabled mod");
                Set("EnableMod", true);
                foreach (string parent in new[] { "EnablePathfindingCore", "AvoidFire", "AvoidPlague", "EscapePlague" })
                {
                    var changes = (List<string>)type.GetField("Changes").GetValue(model); changes.Clear();
                    type.GetMethod("Changed").Invoke(model, new object[] { parent });
                    string[] expected = parent == "EnablePathfindingCore" ? new[] { "CoreOptionsEnabled", "FireOptionsEnabled", "PlagueOptionsEnabled", "PlagueRaisedOptionsEnabled", "DetourSettingsEnabled" }
                        : parent == "AvoidFire" ? new[] { "FireOptionsEnabled", "DetourSettingsEnabled" }
                        : parent == "AvoidPlague" ? new[] { "PlagueOptionsEnabled", "PlagueRaisedOptionsEnabled", "DetourSettingsEnabled" }
                        : new[] { "PlagueRaisedOptionsEnabled" };
                    check(changes.OrderBy(n => n).SequenceEqual(expected.OrderBy(n => n)), "dependent binding notifications: " + parent);
                }
                foreach (string section in new[] { "fire", "plague" })
                {
                    XElement panel = document.Descendants().Single(e => (string)e.Attribute(shared + "ModSettingsSearch.SectionKey") == "pathfinding." + section);
                    check((string)panel.Attribute("Visibility") == "Collapsed" && (string)panel.Attribute(shared + "ModSettingsSearch.Exclude") == "True",
                        "release hides hazard panel and excludes its search entries: " + section);
                }
                foreach (string target in new[] { "Grid" })
                {
                    XElement style = document.Descendants().Single(e => (string)e.Attribute(x + "Key") == "Dependent" + target);
                    check(style.Descendants().Any(e => (string)e.Attribute("Property") == "Opacity" && (string)e.Attribute("Value") == "0.65") &&
                        style.Descendants().Any(e => (string)e.Attribute("Binding") == "{Binding HostSettingsEnabled}" && (string)e.Attribute("Value") == "True"),
                        "child opacity matches host without double dimming: " + target);
                }
                XElement human = Control("EnableImprovedAssassinPathfinding"), ai = Control("EnableImprovedAssassinPathfindingAI");
                check(human.Parent == ai.Parent && (string)human.Attribute("Grid.Column") != (string)ai.Attribute("Grid.Column") &&
                    human.Attribute(shared + "ModSettingsSearch.Title") != null && ai.Attribute(shared + "ModSettingsSearch.Title") != null,
                    "Human and AI Assassin controls are side by side with distinct search titles");
            }
            Console.WriteLine("PASS: " + assertions + " settings dependency, hidden UI and Human/AI layout checks (isolated binding fixture).");
        }
    }
}
