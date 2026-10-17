using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DependencyBootstrapProbe
{
    internal sealed class ShadowDependency
    {
        internal string Guid, Minimum, Maximum;
        internal bool Hard = true;
    }
    internal sealed class ShadowPlugin
    {
        internal string Guid, Version, Path;
        internal string MetadataError = null;
        internal bool Selected = true;
        internal readonly List<ShadowDependency> Dependencies = new List<ShadowDependency>();
        internal readonly List<string> Processes = new List<string>();
    }
    internal sealed class ShadowDecision
    {
        internal ShadowPlugin Plugin;
        internal string State, Reason, Manifest;
        internal readonly List<ShadowDependency> ManifestDependencies = new List<ShadowDependency>();
    }

    // Pure policy shared by the offline executable and the late runtime observer.
    internal static class ShadowInventory
    {
        internal static bool ParseVersion(string raw, out Version version)
        {
            version = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string text = raw.Trim();
            if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V')) text = text.Substring(1);
            int suffix = text.IndexOfAny(new[] { '-', '+', ' ' });
            if (suffix >= 0) text = text.Substring(0, suffix);
            if (!text.Contains(".")) text += ".0";
            if (!Version.TryParse(text, out var parsed)) return false;
            version = new Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            return true;
        }
        internal static string Issue(ShadowDependency dependency, IDictionary<string, string> index)
        {
            if (string.IsNullOrWhiteSpace(dependency.Guid)) return "missing dependency GUID";
            if (!index.TryGetValue(dependency.Guid.Trim(), out var raw)) return "missing " + dependency.Guid;
            Version min = null, max = null;
            if (!string.IsNullOrWhiteSpace(dependency.Minimum) && !ParseVersion(dependency.Minimum, out min)) return "invalid minimum";
            if (!string.IsNullOrWhiteSpace(dependency.Maximum) && !ParseVersion(dependency.Maximum, out max)) return "invalid maximum";
            if (min != null && max != null && min > max) return "inverted bounds";
            if (min == null && max == null) return null;
            if (!ParseVersion(raw, out var version)) return "invalid installed version";
            if (min != null && version < min) return "too old";
            if (max != null && version > max) return "too new";
            return null;
        }
        internal static string FindManifest(string dll, string pluginRoot)
        {
            string root = System.IO.Path.GetFullPath(pluginRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar);
            var dir = new DirectoryInfo(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(dll)));
            if (!dir.FullName.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return null;
            // Never follow a junction outside the bounded plugin tree.
            var chain = new List<DirectoryInfo>();
            for (var current = dir; current != null && !string.Equals(current.FullName, root, StringComparison.OrdinalIgnoreCase); current = current.Parent)
                chain.Add(current);
            if (chain.Any(d => (d.Attributes & FileAttributes.ReparsePoint) != 0)) throw new InvalidOperationException("reparse-point path");
            foreach (var current in chain)
            {
                string manifest = System.IO.Path.Combine(current.FullName, "info.json");
                if (File.Exists(manifest)) return manifest;
            }
            return null;
        }
        private static string Text(Dictionary<string, object> json, string key)
        {
            if (!json.TryGetValue(key, out var value) || value == null) return null;
            if (!(value is string text)) throw new InvalidOperationException("non-string " + key);
            return text;
        }
        internal static List<ShadowDecision> Evaluate(IList<ShadowPlugin> plugins, string root, string process,
            IDictionary<string, string> original, ISet<string> failed, bool runtimeKnown, Func<string, string> validateSource)
        {
            var decisions = new List<ShadowDecision>();
            var groups = plugins.GroupBy(p => p.Guid, StringComparer.OrdinalIgnoreCase).ToArray();
            var ambiguous = new HashSet<string>(groups.Where(g => g.Count() != 1).Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
            var byGuid = plugins.GroupBy(p => p.Guid, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var plugin in plugins)
            {
                var decision = new ShadowDecision { Plugin = plugin, State = "Excluded" };
                decisions.Add(decision);
                try
                {
                    string loader = LoaderIssue(plugin, byGuid, ambiguous, failed, process, new HashSet<string>(StringComparer.Ordinal));
                    if (loader != null) { decision.Reason = loader; continue; }
                    if (original.Keys.Any(k => string.Equals(k, plugin.Guid, StringComparison.OrdinalIgnoreCase)))
                    { decision.Reason = "original index authoritative"; continue; }
                    decision.Manifest = FindManifest(plugin.Path, root);
                    if (decision.Manifest == null) { decision.Reason = "no bounded manifest"; continue; }
                    string relative = System.IO.Path.GetFullPath(plugin.Path).Substring(System.IO.Path.GetFullPath(root).TrimEnd('\\', '/').Length + 1);
                    if (relative.Split(System.IO.Path.DirectorySeparatorChar).Length < 3)
                    { decision.Reason = "not a nested plugin"; continue; }
                    var json = Shared.DependencyFreeJson.Parse(File.ReadAllText(decision.Manifest)) as Dictionary<string, object>;
                    if (json == null) throw new InvalidOperationException("manifest is not an object");
                    if (!string.Equals(Text(json, "GUID"), plugin.Guid, StringComparison.Ordinal))
                    { decision.Reason = "nearest manifest GUID mismatch"; continue; }
                    if (!ParseVersion(Text(json, "Version"), out var mv) || !ParseVersion(plugin.Version, out var pv) || mv != pv)
                    { decision.Reason = "invalid or mismatched manifest version"; continue; }
                    if (json.TryGetValue("AssetMode", out var mode) && !ValidAssetMode(mode))
                    { decision.Reason = "invalid AssetMode"; continue; }
                    if (json.TryGetValue("Dependencies", out var deps) && deps != null)
                    {
                        if (!(deps is List<object> list)) throw new InvalidOperationException("invalid Dependencies");
                        foreach (var value in list)
                        {
                            var dep = value as Dictionary<string, object>;
                            if (dep == null) throw new InvalidOperationException("invalid dependency entry");
                            decision.ManifestDependencies.Add(new ShadowDependency { Guid = Text(dep, "GUID"), Minimum = Text(dep, "MinimumVersion"), Maximum = Text(dep, "MaximumVersion") });
                        }
                    }
                    string rejection = validateSource?.Invoke(System.IO.Path.GetDirectoryName(decision.Manifest));
                    if (rejection != null) { decision.Reason = rejection; continue; }
                    decision.State = runtimeKnown && validateSource != null ? "Eligible" : "Unclear";
                    decision.Reason = decision.State == "Eligible" ? "selected; static hard closure and SE source accepted; future Awake unknown" : "static checks passed; live SE source/KillSwitch and prior loader failures unverified";
                }
                catch (Exception ex) { decision.State = "Excluded"; decision.Reason = "manifest/path validation: " + ex.Message; }
            }
            // Manifest requirements are checked against original entries and statically suitable providers.
            // Reject cycles conservatively; do not promote an uncertain provider into a runtime correction.
            foreach (var decision in decisions.Where(d => d.State != "Excluded").ToArray())
            {
                var comparison = new Dictionary<string, string>(original, StringComparer.OrdinalIgnoreCase);
                if (!runtimeKnown)
                {
                    // Provisional presence only. Offline decisions remain Unclear regardless of this comparison.
                    foreach (var p in plugins.Where(p => !ambiguous.Contains(p.Guid)))
                        if (!comparison.ContainsKey(p.Guid)) comparison.Add(p.Guid, p.Version);
                }
                string issue = ManifestClosure(decision, decisions, comparison, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                if (issue != null) { decision.State = "Excluded"; decision.Reason = issue; }
            }
            return decisions;
        }
        private static bool ValidAssetMode(object mode)
        {
            if (mode is long numeric) return numeric == 0 || numeric == 1;
            if (!(mode is string text)) return false;
            // The SE enum uses JsonStringEnumConverter (case-insensitive names, numeric strings accepted).
            return string.Equals(text.Trim(), "Global", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text.Trim(), "Local", StringComparison.OrdinalIgnoreCase) || text.Trim() == "0" || text.Trim() == "1";
        }
        private static string ManifestClosure(ShadowDecision decision, List<ShadowDecision> all, IDictionary<string, string> original, HashSet<string> visiting)
        {
            if (!visiting.Add(decision.Plugin.Guid)) return "manifest dependency cycle";
            try
            {
                foreach (var dependency in decision.ManifestDependencies)
                {
                    if (string.IsNullOrWhiteSpace(dependency.Guid)) return "invalid manifest dependency GUID";
                    var index = new Dictionary<string, string>(original, StringComparer.OrdinalIgnoreCase);
                    ShadowDecision provider = null;
                    if (!index.ContainsKey(dependency.Guid.Trim()))
                    {
                        provider = all.SingleOrDefault(d => d.State != "Excluded" && string.Equals(d.Plugin.Guid, dependency.Guid.Trim(), StringComparison.OrdinalIgnoreCase));
                        if (provider != null) index[provider.Plugin.Guid] = provider.Plugin.Version;
                    }
                    string issue = Issue(dependency, index);
                    if (issue != null) return "manifest dependency " + dependency.Guid + ": " + issue;
                    if (provider != null)
                    {
                        issue = ManifestClosure(provider, all, original, visiting);
                        if (issue != null) return dependency.Guid + " -> " + issue;
                    }
                }
                return null;
            }
            finally { visiting.Remove(decision.Plugin.Guid); }
        }
        private static string LoaderIssue(ShadowPlugin plugin, Dictionary<string, ShadowPlugin> all, HashSet<string> ambiguous,
            ISet<string> failed, string process, HashSet<string> visiting)
        {
            if (ambiguous.Contains(plugin.Guid)) return "duplicate/case-conflicting winner GUID";
            if (!plugin.Selected) return "not selected by loader";
            if (plugin.MetadataError != null) return plugin.MetadataError;
            if (failed.Contains(plugin.Guid)) return "known loader failure: " + plugin.Guid;
            if (plugin.Processes.Count != 0 && plugin.Processes.All(p => !string.Equals(p.Replace(".exe", ""), process, StringComparison.InvariantCultureIgnoreCase))) return "process excluded";
            if (!Version.TryParse(plugin.Version, out _)) return "invalid BepInEx version";
            if (!visiting.Add(plugin.Guid)) return "hard dependency cycle";
            try
            {
                foreach (var dep in plugin.Dependencies.Where(d => d.Hard))
                {
                    if (dep.Guid == null || !all.TryGetValue(dep.Guid, out var target)) return "missing hard dependency: " + dep.Guid;
                    if (!Version.TryParse(dep.Minimum ?? "0.0", out var min) || !Version.TryParse(target.Version, out var version) || version < min)
                        return "hard dependency version: " + dep.Guid;
                    string issue = LoaderIssue(target, all, ambiguous, failed, process, visiting);
                    if (issue != null) return dep.Guid + " -> " + issue;
                }
                return null;
            }
            finally { visiting.Remove(plugin.Guid); }
        }
    }
}
