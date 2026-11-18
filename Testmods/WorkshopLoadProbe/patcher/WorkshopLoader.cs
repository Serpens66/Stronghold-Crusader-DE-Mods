using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using Mono.Cecil;

namespace WorkshopLoadProbe
{
    // Prototype of plan WorkshopLoad.md, Phase 0 (T1, T3, T5). Reads Workshop folders only;
    // nothing under steamapps/workshop is ever written.
    internal static class WorkshopLoader
    {
        internal const string AppId = "3024040";
        internal const string RootsDataKey = "WorkshopLoadProbe.Roots";

        internal static void Merge(Dictionary<string, List<PluginInfo>> local, Func<TypeDefinition, PluginInfo> selector, Func<AssemblyDefinition, bool> filter)
        {
            var config = ProbePatcher.Config;
            var total = Stopwatch.StartNew();
            string content = ContentDirectory(Paths.GameRootPath, out string library);
            if (library != null) Acf.Dump(Path.Combine(library, "workshop", "appworkshop_" + AppId + ".acf"));

            var localGuids = LocalGuids(local);
            var cache = config.HashCache ? HashCache.Load() : null;
            var additions = new Dictionary<string, List<PluginInfo>>(StringComparer.OrdinalIgnoreCase);
            var loadedRoots = new List<string>();
            int others = 0;

            foreach (string item in Items(content, config.ExtraItems))
            {
                Package package;
                try { package = Package.Read(item); }
                catch (Exception ex) { ProbeLog.Warning("DECISION", Key(item) + " skip-manifest " + ex.Message); continue; }
                if (package == null) { others++; continue; }

                var inventory = Inventory.Verify(item, package, cache);
                ProbeLog.Info("INVENTORY", Key(item) + " " + package.Id + " " + package.Version + " " + inventory);
                if (!inventory.Ok) { ProbeLog.Warning("DECISION", Key(item) + " skip-integrity"); continue; }

                var scan = Stopwatch.StartNew();
                var found = new Dictionary<string, List<PluginInfo>>(StringComparer.OrdinalIgnoreCase);
                var roots = package.Roots.Select(root => Path.Combine(Path.Combine(item, "Payload"), root.Replace('/', Path.DirectorySeparatorChar))).ToList();
                for (int i = 0; i < roots.Count; i++)
                {
                    string cacheName = config.TypeCache ? "wlp_" + Safe(Key(item)) + "_" + i : null;
                    foreach (var entry in TypeLoader.FindPluginTypes(roots[i], selector, filter, cacheName)) found[entry.Key] = entry.Value;
                }
                var guids = new HashSet<string>(found.Values.SelectMany(list => list).Select(info => info.Metadata.GUID), StringComparer.OrdinalIgnoreCase);
                foreach (string root in roots) foreach (string guid in InfoGuids(root)) guids.Add(guid);
                if (package.BootstrapGuid != null) guids.Remove(package.BootstrapGuid);
                foreach (var plugin in found.SelectMany(entry => entry.Value.Select(info => new { entry.Key, info })))
                    ProbeLog.Info("PLUGIN", Key(item) + " " + plugin.info.Metadata.GUID + " " + plugin.info.Metadata.Version + " " + plugin.Key);
                ProbeLog.Info("SCAN", Key(item) + " assemblies=" + found.Count + " guids=" + guids.Count + " typeCache=" + config.TypeCache + " ms=" + scan.ElapsedMilliseconds);

                var overlap = guids.Where(localGuids.Contains).OrderBy(g => g, StringComparer.Ordinal).ToArray();
                bool selected = config.LoadItems.Contains(Key(item)) || config.LoadItems.Contains(item);
                if (overlap.Length > 0 && config.LocalWins)
                    ProbeLog.Info("DECISION", Key(item) + " skip-local [" + string.Join(", ", overlap) + "]");
                else if (config.Mode != ProbeMode.Load || !selected)
                    ProbeLog.Info("DECISION", Key(item) + " observe-only (would load; overlap=" + overlap.Length + ")");
                else
                {
                    if (overlap.Length > 0) ProbeLog.Warning("DECISION", Key(item) + " load-with-duplicates [" + string.Join(", ", overlap) + "] BepInEx decides");
                    else ProbeLog.Info("DECISION", Key(item) + " load");
                    foreach (var entry in found) additions[entry.Key] = entry.Value;
                    loadedRoots.AddRange(roots);
                }
            }
            cache?.Save();

            // Published only after every item was handled, so a failure above adds nothing.
            foreach (var entry in additions) local[entry.Key] = entry.Value;
            if (loadedRoots.Count > 0)
            {
                AppDomain.CurrentDomain.SetData(RootsDataKey, loadedRoots.ToArray());
                Resolver.Register(loadedRoots, config.Resolver);
            }
            ProbeLog.Info("DISCOVERY_DONE", "localGuids=" + localGuids.Count + " otherItems=" + others + " addedAssemblies=" + additions.Count +
                " roots=" + loadedRoots.Count + " ms=" + total.ElapsedMilliseconds);
        }

        internal static string Key(string item) => Path.GetFileName(item.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        private static string Safe(string value) => new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());

        // <library>/steamapps/common/<game> -> <library>/steamapps/workshop/content/3024040
        private static string ContentDirectory(string game, out string library)
        {
            library = null;
            var common = Directory.GetParent(Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar));
            var apps = common?.Parent;
            if (common == null || apps == null || !common.Name.Equals("common", StringComparison.OrdinalIgnoreCase) ||
                !apps.Name.Equals("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                ProbeLog.Warning("WORKSHOP_DIR", "game is not below steamapps/common: " + game);
                return null;
            }
            library = apps.FullName;
            string content = Path.Combine(Path.Combine(Path.Combine(library, "workshop"), "content"), AppId);
            ProbeLog.Info("WORKSHOP_DIR", content + " exists=" + Directory.Exists(content));
            return content;
        }

        private static IEnumerable<string> Items(string content, List<string> extra)
        {
            if (content != null && Directory.Exists(content))
                foreach (string directory in Directory.GetDirectories(content).OrderBy(d => d, StringComparer.Ordinal))
                    yield return directory;
            foreach (string directory in extra)
            {
                if (Directory.Exists(directory)) yield return directory;
                else ProbeLog.Warning("ITEM_MISSING", directory);
            }
        }

        private static HashSet<string> LocalGuids(Dictionary<string, List<PluginInfo>> local)
        {
            var watch = Stopwatch.StartNew();
            var guids = new HashSet<string>(local.Values.SelectMany(list => list).Select(info => info.Metadata.GUID), StringComparer.OrdinalIgnoreCase);
            int plugins = guids.Count;
            foreach (string guid in InfoGuids(Paths.PluginPath)) guids.Add(guid);
            ProbeLog.Info("LOCAL", "pluginGuids=" + plugins + " withInfoJson=" + guids.Count + " ms=" + watch.ElapsedMilliseconds);
            return guids;
        }

        private static IEnumerable<string> InfoGuids(string root)
        {
            if (!Directory.Exists(root)) yield break;
            foreach (string file in Directory.GetFiles(root, "info.json", SearchOption.AllDirectories))
            {
                string guid = null;
                try
                {
                    if (Shared.DependencyFreeJson.Parse(File.ReadAllText(file), true) is Dictionary<string, object> json &&
                        json.TryGetValue("GUID", out object value)) guid = value as string;
                }
                catch (Exception ex) { ProbeLog.Warning("INFO_JSON", file + " " + ex.Message); }
                if (!string.IsNullOrEmpty(guid)) yield return guid;
            }
        }
    }

    internal sealed class Package
    {
        internal string Id, Version, BootstrapGuid;
        internal List<string> Roots = new List<string>();
        internal List<(string Path, long Size, string Sha)> Files = new List<(string, long, string)>();

        internal static Package Read(string item)
        {
            string path = Path.Combine(item, "apishared-workshop.json");
            if (!File.Exists(path)) return null;
            if (!(Shared.DependencyFreeJson.Parse(File.ReadAllText(path)) is Dictionary<string, object> json)) throw new InvalidDataException("manifest is not an object");
            var package = new Package { Id = Text(json, "PackageId"), Version = Text(json, "Version") };
            foreach (object root in List(json, "Roots"))
            {
                string value = root as string;
                if (value == null || !value.StartsWith("BepInEx/plugins/", StringComparison.Ordinal) || !Relative(value)) throw new InvalidDataException("bad root " + value);
                package.Roots.Add(value);
            }
            if (package.Roots.Count == 0) throw new InvalidDataException("no roots");
            foreach (object file in List(json, "Files"))
            {
                if (!(file is Dictionary<string, object> record)) throw new InvalidDataException("bad file record");
                string relative = Text(record, "Path");
                if (!Relative(relative)) throw new InvalidDataException("bad path " + relative);
                package.Files.Add((relative, Convert.ToInt64(record["Size"]), Text(record, "Sha256").ToLowerInvariant()));
            }
            if (json.TryGetValue("Bootstrap", out object bootstrap) && bootstrap is Dictionary<string, object> boot && boot.TryGetValue("Guid", out object guid))
                package.BootstrapGuid = guid as string;
            return package;
        }

        private static bool Relative(string value) =>
            value.Length > 0 && !value.StartsWith("/", StringComparison.Ordinal) && value.IndexOf('\\') < 0 && value.IndexOf(':') < 0 &&
            value.Split('/').All(part => part.Length > 0 && part != "." && part != "..");

        private static string Text(Dictionary<string, object> json, string key) =>
            json.TryGetValue(key, out object value) && value is string text ? text : throw new InvalidDataException("missing " + key);

        private static List<object> List(Dictionary<string, object> json, string key) =>
            json.TryGetValue(key, out object value) && value is List<object> list ? list : throw new InvalidDataException("missing " + key);
    }
}
