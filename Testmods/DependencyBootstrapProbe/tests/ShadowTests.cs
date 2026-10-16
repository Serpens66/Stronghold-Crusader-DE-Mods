using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace DependencyBootstrapProbe
{
    internal static class ShadowTests
    {
        private static void Require(bool ok, string name) { if (!ok) throw new Exception("Shadow fixture: " + name); }
        internal static void Run()
        {
            TestIndexSwitch();
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shadow-fixtures");
            Directory.CreateDirectory(root);
            var original = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var failed = new HashSet<string>(StringComparer.Ordinal);
            Func<string, string> accepted = _ => null;
            var a = Plugin(root, "a"); var b = Plugin(root, "b");
            Func<List<ShadowDecision>> evaluate = () => ShadowInventory.Evaluate(new[] { a, b }, root, "game", original, failed, true, accepted);
            Action<string, string> expect = (state, name) => Require(evaluate()[0].State == state, name);
            expect("Eligible", "valid nested provider");
            a.Dependencies.Add(new ShadowDependency { Guid = "b", Minimum = "1.0" });
            expect("Eligible", "hard chain");
            b.Dependencies.Add(new ShadowDependency { Guid = "missing" });
            expect("Excluded", "transitive missing");
            b.Dependencies.Clear();
            a.Dependencies[0].Minimum = "2.0"; expect("Excluded", "hard too old");
            a.Dependencies[0].Minimum = "1.0";
            b.Dependencies.Add(new ShadowDependency { Guid = "a" }); expect("Excluded", "hard cycle");
            b.Dependencies.Clear();
            failed.Add("b"); expect("Excluded", "prior failure"); failed.Clear();
            b.Selected = false; expect("Excluded", "excluded dependency"); b.Selected = true;
            a.Processes.Add("another.exe"); expect("Excluded", "process filter"); a.Processes.Clear();
            a.Dependencies.Add(new ShadowDependency { Guid = "optional", Hard = false }); expect("Eligible", "optional absent");
            b.Guid = "A"; expect("Excluded", "case collision"); b.Guid = "a"; expect("Excluded", "duplicate GUID"); b.Guid = "b";
            original["a"] = "0.1"; expect("Excluded", "root authoritative"); original.Clear();
            original["A"] = "0.1"; expect("Excluded", "root case collision"); original.Clear();
            string manifest = ShadowInventory.FindManifest(a.Path, root);
            File.WriteAllText(manifest, "{broken"); expect("Excluded", "malformed nearest");
            File.WriteAllText(Path.Combine(root, "pack", "info.json"), "{\"GUID\":\"a\",\"Version\":\"1.0\"}");
            expect("Excluded", "no parent fallback");
            File.WriteAllText(manifest, "{\"GUID\":\"other\",\"Version\":\"1.0\"}"); expect("Excluded", "wrong nearest GUID");
            File.WriteAllText(manifest, "{\"GUID\":\"a\",\"Version\":\"garbage\"}"); expect("Excluded", "invalid version");
            File.WriteAllText(manifest, "{\"GUID\":\"a\",\"Version\":\"2.0\"}"); expect("Excluded", "mismatched version");
            a = Plugin(root, "a");
            File.WriteAllText(manifest, "{\"GUID\":\"a\",\"Version\":\"1.0\",\"AssetMode\":\"Local\"}"); expect("Eligible", "string AssetMode");
            File.WriteAllText(manifest, "{\"GUID\":\"a\",\"Version\":\"1.0\",\"AssetMode\":2}"); expect("Excluded", "unknown AssetMode");
            a = Plugin(root, "a");
            accepted = _ => "blocked by SE"; expect("Excluded", "KillSwitch/source rejection"); accepted = _ => null;
            Require(ShadowInventory.Evaluate(new[] { a }, root, "game", original, failed, false, null)[0].State == "Unclear", "offline uncertainty");
            string orphan = Path.Combine(root, "orphan", "sub", "x.dll"); Directory.CreateDirectory(Path.GetDirectoryName(orphan));
            Require(ShadowInventory.FindManifest(orphan, root) == null, "missing manifest");
            Require(ShadowInventory.FindManifest(Path.Combine(root, "..", "outside.dll"), root) == null, "root escape");
            var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "a", "1.2" } };
            var dependency = new ShadowDependency { Guid = "a", Minimum = "1.2.0", Maximum = "v1.2.0+test" };
            Require(ShadowInventory.Issue(dependency, index) == null, "inclusive equal normalized bounds");
            dependency.Minimum = "2.0"; dependency.Maximum = null; Require(ShadowInventory.Issue(dependency, index) == "too old", "minimum");
            dependency.Minimum = null; dependency.Maximum = "1.1"; Require(ShadowInventory.Issue(dependency, index) == "too new", "maximum");
            dependency.Maximum = "bad"; Require(ShadowInventory.Issue(dependency, index) == "invalid maximum", "invalid bound");
            dependency.Maximum = "1.0"; dependency.Minimum = "2.0"; Require(ShadowInventory.Issue(dependency, index) == "inverted bounds", "inverted bounds");
            File.WriteAllText(ShadowInventory.FindManifest(a.Path, root), "{\"GUID\":\"a\",\"Version\":\"1.0\",\"Dependencies\":[{\"GUID\":\"b\",\"MaximumVersion\":\"0.1\"}]}");
            expect("Excluded", "manifest version dependency");
            File.WriteAllText(ShadowInventory.FindManifest(a.Path, root), "{\"GUID\":\"a\",\"Version\":\"1.0\",\"Dependencies\":[{\"GUID\":\"b\"}]}");
            File.WriteAllText(ShadowInventory.FindManifest(b.Path, root), "{\"GUID\":\"b\",\"Version\":\"1.0\",\"Dependencies\":[{\"GUID\":\"a\"}]}");
            expect("Excluded", "manifest cycle");
            Require(original.Count == 0 && failed.Count == 0, "inputs unchanged");
            Console.WriteLine("PASS: shadow eligibility fixtures (manifest boundaries, versions, duplicates, process/exclusion, hard/soft/transitive/cyclic dependencies, source rejection and uncertainty).");
        }
        private static void TestIndexSwitch()
        {
            var value = new object();
            var original = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "root", value } };
            var prepared = new Dictionary<string, object>(original, StringComparer.OrdinalIgnoreCase) { { "nested", new object() } };
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => prepared, true), prepared), "complete copy selected");
            Require(original.Count == 1 && ReferenceEquals(original["root"], value), "original immutable");
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => { throw new Exception(); }, true), original), "preparation exception fallback");
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => null, true), original), "null preparation fallback");
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => { throw new Exception("must not call"); }, false), original), "disabled preserves original");
            prepared["root"] = new object();
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => prepared, true), original), "root replacement rejected");
            prepared.Remove("root");
            Require(ReferenceEquals(ShadowRuntime.SelectIndex(original, () => prepared, true), original), "missing original entry rejected");
            Require(original.Count == 1 && ReferenceEquals(original["root"], value), "failed preparation preserves original");
            Console.WriteLine("PASS: actual index selector, disabled path, complete copy, preparation failure and original-entry invariants.");
        }
        private static ShadowPlugin Plugin(string root, string guid)
        {
            string directory = Path.Combine(root, "pack", guid);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "info.json"), "{\"GUID\":\"" + guid + "\",\"Version\":\"1.0\"}");
            return new ShadowPlugin { Guid = guid, Version = "1.0", Path = Path.Combine(directory, "plugin.dll") };
        }
        private static object CecilMetadata(Type attribute, TypeDefinition type) => attribute.GetMethod("FromCecilType", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { type });
        internal static void CompareExtender(Type manager)
        {
            var parse = manager.GetMethod("TryParseModVersion", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (string value in new[] { null, "", "1", "1.2", "v1.2.3+meta", "1.2.3-beta", "1.2.3 suffix", "V2", "x", "1.2.3.4.5", "-1", "0.0.0.0" })
            {
                object[] args = { value, null };
                bool actual = (bool)parse.Invoke(null, args);
                bool ours = ShadowInventory.ParseVersion(value, out var version);
                Require(actual == ours && (!actual || version.Equals(args[1])), "SE version differential " + value);
            }
            var installed = manager.GetNestedType("InstalledModVersion", BindingFlags.NonPublic);
            var dictionaryType = typeof(Dictionary<,>).MakeGenericType(typeof(string), installed);
            var dependencyType = manager.GetMethod("TryGetDependencyIssue", BindingFlags.Static | BindingFlags.NonPublic).GetParameters()[0].ParameterType;
            var check = manager.GetMethod("TryGetDependencyIssue", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (string minimum in new[] { null, "1", "1.2", "2", "bad" })
            foreach (string maximum in new[] { null, "1", "1.2", "2", "bad" })
            foreach (string raw in new[] { null, "1.2", "garbage" })
            {
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictionaryType, new object[] { StringComparer.OrdinalIgnoreCase });
                var plain = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (raw != null) { dict.Add("p", Activator.CreateInstance(installed, new object[] { "p", raw })); plain.Add("p", raw); }
                var dependency = Activator.CreateInstance(dependencyType);
                dependencyType.GetProperty("GUID").SetValue(dependency, "P");
                dependencyType.GetProperty("MinimumVersion").SetValue(dependency, minimum);
                dependencyType.GetProperty("MaximumVersion").SetValue(dependency, maximum);
                object[] args = { dependency, dict, null };
                bool actual = (bool)check.Invoke(null, args);
                Require(actual == (ShadowInventory.Issue(new ShadowDependency { Guid = "P", Minimum = minimum, Maximum = maximum }, plain) != null), "SE bounds differential");
            }
            Console.WriteLine("PASS: real SE version parser and 75 dependency comparisons agree; compatibility method never executed.");
        }
        internal static void Offline(string game, string trace, string output)
        {
            var lines = File.ReadAllLines(trace);
            int start = Array.FindLastIndex(lines, l => l.Contains("[DBP:SESSION]"));
            if (start < 0) throw new Exception("No session");
            var winners = new List<ShadowPlugin>();
            var hashes = new List<object>();
            using (var resolver = new DefaultAssemblyResolver())
            {
                resolver.AddSearchDirectory(Path.Combine(game, "BepInEx/core"));
                resolver.AddSearchDirectory(Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Managed"));
                var selected = lines.Skip(start).Select(l => Regex.Match(l, @"\[DBP:WINNER\] guid=(\S+) version=(\S+) path=(.*)$")).Where(m => m.Success).ToArray();
                foreach (var match in selected) resolver.AddSearchDirectory(Path.GetDirectoryName(match.Groups[3].Value));
                foreach (var match in selected)
                {
                    string guid = match.Groups[1].Value, version = match.Groups[2].Value, path = match.Groups[3].Value;
                    var plugin = new ShadowPlugin { Guid = guid, Version = version, Path = path };
                    winners.Add(plugin);
                    try
                    {
                        using (var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver }))
                        {
                            var matches = assembly.MainModule.GetTypes().Select(t => new { Type = t, Meta = (BepInEx.BepInPlugin)CecilMetadata(typeof(BepInEx.BepInPlugin), t) })
                                .Where(t => t.Meta != null && t.Meta.GUID == guid && t.Meta.Version.ToString() == version).ToArray();
                            if (matches.Length != 1) throw new Exception("recorded winner does not match exactly one current metadata type");
                            var type = matches[0].Type;
                            foreach (var dep in (IEnumerable<BepInEx.BepInDependency>)CecilMetadata(typeof(BepInEx.BepInDependency), type))
                                plugin.Dependencies.Add(new ShadowDependency { Guid = dep.DependencyGUID, Minimum = dep.MinimumVersion.ToString(), Hard = (dep.Flags & BepInEx.BepInDependency.DependencyFlags.HardDependency) != 0 });
                            plugin.Processes.AddRange(((IEnumerable<BepInEx.BepInProcess>)CecilMetadata(typeof(BepInEx.BepInProcess), type)).Select(p => p.ProcessName));
                        }
                        hashes.Add(new Dictionary<string, object> { { "path", path }, { "sha256", ProbeIdentity.Hash(path) } });
                    }
                    catch (Exception ex) { plugin.MetadataError = "offline metadata unresolved: " + ex.GetBaseException().Message; }
                }
            }
            if (winners.Count == 0) throw new Exception("No winners in last session");
            // This is not a reconstruction of SE discovery: root candidates/packages and live KillSwitch remain unknown.
            var original = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var result = ShadowInventory.Evaluate(winners, Path.Combine(game, "BepInEx/plugins"), "Stronghold Crusader Definitive Edition", original, new HashSet<string>(), false, null);
            var rows = result.Select(d => (object)new Dictionary<string, object> { { "guid", d.Plugin.Guid }, { "version", d.Plugin.Version }, { "path", d.Plugin.Path }, { "manifest", d.Manifest }, { "state", d.State }, { "reason", d.Reason } }).ToList();
            var comparisons = new List<object>();
            var selectedVersions = winners.ToDictionary(p => p.Guid, p => p.Version, StringComparer.OrdinalIgnoreCase);
            foreach (var winner in winners)
            {
                string manifest = ShadowInventory.FindManifest(winner.Path, Path.Combine(game, "BepInEx/plugins"));
                if (manifest == null) continue;
                var json = Shared.DependencyFreeJson.Parse(File.ReadAllText(manifest)) as Dictionary<string, object>;
                if (json == null || !json.TryGetValue("GUID", out var guid) || !Equals(guid, winner.Guid) || !json.TryGetValue("Dependencies", out var dependencies) || !(dependencies is List<object> list)) continue;
                foreach (var entry in list.OfType<Dictionary<string, object>>())
                {
                    entry.TryGetValue("GUID", out var dg); entry.TryGetValue("MinimumVersion", out var min); entry.TryGetValue("MaximumVersion", out var max);
                    var dep = new ShadowDependency { Guid = dg as string, Minimum = min as string, Maximum = max as string };
                    string issue = ShadowInventory.Issue(dep, selectedVersions);
                    comparisons.Add(new Dictionary<string, object> { { "consumer", winner.Guid }, { "dependency", dg }, { "minimum", min }, { "maximum", max }, { "selectedVersionIssue", issue }, { "liveIndexVerified", false } });
                }
            }
            var report = new Dictionary<string, object> { { "session", lines[start] }, { "traceSha256", ProbeIdentity.Hash(trace) },
                { "limitation", "Current DLL bytes/metadata, recorded winner list; no plugin code executed. Original SE index, KillSwitch and prior failures not observed offline. Manifest dependency presence uses provisional selected versions. No correction is authorized by this report." }, { "binaries", hashes }, { "candidates", rows }, { "dependencyComparisons", comparisons } };
            File.WriteAllText(output, Shared.DependencyFreeJson.Serialize(report));
            foreach (var d in result) Console.WriteLine(d.Plugin.Guid + ": " + d.State + " - " + d.Reason);
            Console.WriteLine("Offline report: " + output);
        }
    }
}
