using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;

namespace DependencyBootstrapProbe
{
    internal static class ShadowRuntime
    {
        internal static List<ShadowPlugin> Winners;
        internal static HashSet<string> Failed;
        private static int runs;
        private static bool correction;
        private static bool correctionReady;
        private static object expectedIndex;
        private static int expectedIssues;
        internal static void Configure()
        {
            string path = Path.Combine(BepInEx.Paths.ConfigPath, "DependencyBootstrapProbe.correct");
            correction = File.Exists(path);
            if (correction && File.ReadAllText(path).Trim() != ProbeIdentity.Hash(typeof(ShadowRuntime).Assembly.Location))
                throw new InvalidOperationException("Correction switch belongs to another build");
            ProbeTrace.Mark("CORRECTION_MODE", correction ? "Enabled" : "Disabled");
        }
        internal static ShadowPlugin FromInfo(BepInEx.PluginInfo info)
        {
            var result = new ShadowPlugin { Guid = info.Metadata.GUID, Version = info.Metadata.Version.ToString(), Path = info.Location };
            result.Processes.AddRange(info.Processes.Select(p => p.ProcessName));
            foreach (var dependency in info.Dependencies)
                result.Dependencies.Add(new ShadowDependency { Guid = dependency.DependencyGUID, Minimum = dependency.MinimumVersion.ToString(), Hard = (dependency.Flags & BepInEx.BepInDependency.DependencyFlags.HardDependency) != 0 });
            return result;
        }
        private static object Property(object value, string name) => value.GetType().GetProperty(name).GetValue(value, null);
        internal static object Observe(object manager, object originalObject)
        {
            try
            {
                correctionReady = false;
                if (++runs != 1 || Winners == null || Failed == null) throw new InvalidOperationException("shadow invocation/state mismatch");
                ProbeTrace.Mark("SHADOW_BEGIN", "read-only; failures=" + Failed.Count);
                foreach (var guid in Failed.OrderBy(g => g, StringComparer.Ordinal)) ProbeTrace.Mark("SHADOW_FAILED", guid);
                var original = (IDictionary)originalObject;
                var entryList = new List<DictionaryEntry>();
                foreach (DictionaryEntry entry in original) entryList.Add(entry);
                var entries = entryList.ToArray();
                var raw = entries.ToDictionary(e => (string)e.Key, e => (string)Property(e.Value, "RawVersion"), StringComparer.OrdinalIgnoreCase);
                foreach (var entry in raw) ProbeTrace.Mark("SHADOW_INDEX", Line(new Dictionary<string, object> { { "guid", entry.Key }, { "version", entry.Value } }));
                var shadow = (IDictionary)Activator.CreateInstance(original.GetType(), new object[] { StringComparer.OrdinalIgnoreCase });
                foreach (var entry in entries) shadow.Add(entry.Key, entry.Value);
                var managerType = manager.GetType();
                var preparedField = managerType.GetField("_preparedCandidates", BindingFlags.Instance | BindingFlags.NonPublic);
                object prepared = preparedField.GetValue(manager);
                var candidates = ((IEnumerable)prepared).Cast<object>().ToArray();
                var reader = managerType.GetMethod("TryReadCandidate", BindingFlags.Static | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
                var decisions = ShadowInventory.Evaluate(Winners, BepInEx.Paths.PluginPath, BepInEx.Paths.ProcessName, raw, Failed, true,
                    directory => ValidateCandidate(reader, directory));
                int added = 0;
                var installedType = original.GetType().GetGenericArguments()[1];
                foreach (var decision in decisions)
                {
                    ProbeTrace.Mark("SHADOW_CANDIDATE", Line(new Dictionary<string, object> {
                        {"guid", decision.Plugin.Guid}, {"version", decision.Plugin.Version}, {"path", decision.Plugin.Path},
                        {"manifest", decision.Manifest}, {"state", decision.State}, {"reason", decision.Reason} }));
                    if (decision.State != "Eligible") continue;
                    shadow.Add(decision.Plugin.Guid, Activator.CreateInstance(installedType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new object[] { decision.Plugin.Guid, decision.Plugin.Version }, null));
                    added++;
                }
                // Use the real SE comparison method on both dictionaries. Its original caller still runs unchanged.
                var enumerate = managerType.GetMethod("EnumerateDependencies", BindingFlags.Static | BindingFlags.NonPublic);
                var issue = managerType.GetMethod("TryGetDependencyIssue", BindingFlags.Static | BindingFlags.NonPublic);
                int before = 0, after = 0, compared = 0;
                foreach (var candidate in candidates)
                {
                    var info = Property(candidate, "Info");
                    foreach (object dependency in (IEnumerable)enumerate.Invoke(null, new[] { info }))
                    {
                        object[] oldArgs = { dependency, original, null }, newArgs = { dependency, shadow, null };
                        bool oldIssue = (bool)issue.Invoke(null, oldArgs), newIssue = (bool)issue.Invoke(null, newArgs);
                        compared++;
                        if (oldIssue) before++;
                        if (newIssue) after++;
                        ProbeTrace.Mark("SHADOW_DEPENDENCY", Line(new Dictionary<string, object> {
                            {"consumer", Property(info, "GUID")}, {"dependency", Property(dependency, "GUID")},
                            {"originalIssue", oldIssue}, {"shadowIssue", newIssue}, {"originalReason", oldArgs[2]}, {"shadowReason", newArgs[2]} }));
                    }
                }
                if (original.Count != entries.Length || entries.Any(e => !original.Contains(e.Key) || !ReferenceEquals(original[e.Key], e.Value)) ||
                    !ReferenceEquals(prepared, preparedField.GetValue(manager)) || !candidates.SequenceEqual(((IEnumerable)prepared).Cast<object>()) || shadow.Count != entries.Length + added)
                    throw new InvalidOperationException("shadow isolation invariant failed");
                ProbeTrace.Mark("SHADOW_COMPLETE", "candidates=" + decisions.Count + " original=" + entries.Length + " added=" + added +
                    " dependencies=" + compared + " issuesBefore=" + before + " issuesAfter=" + after + " originalUnchanged=True");
                bool enabled = PairActivation.AllowsCorrection(correction, PairActivation.Bound);
                expectedIssues = enabled ? after : before;
                expectedIndex = SelectIndex(original, () => shadow, enabled);
                if (enabled && !ReferenceEquals(expectedIndex, shadow)) throw new InvalidOperationException("Prepared index rejected");
                correctionReady = enabled;
                if (enabled) ProbeTrace.Mark("CORRECTION_PREPARED", "original=" + entries.Length + " effective=" + shadow.Count + " added=" + added + " expectedIssues=" + after);
                return expectedIndex;
            }
            catch (Exception ex)
            {
                expectedIndex = originalObject;
                expectedIssues = -1;
                ProbeTrace.Mark("ERROR", "Shadow " + ex);
                if (correction) ProbeTrace.Mark("CORRECTION_FALLBACK", "original index retained");
                return originalObject;
            }
        }
        // Only a complete independent dictionary can replace the local. No original entry is ever overwritten.
        internal static IDictionary SelectIndex(IDictionary original, Func<IDictionary> prepare, bool enabled)
        {
            if (!enabled) return original;
            try
            {
                var candidate = prepare();
                if (candidate == null || ReferenceEquals(candidate, original) || candidate.GetType() != original.GetType()) return original;
                foreach (DictionaryEntry entry in original)
                    if (!candidate.Contains(entry.Key) || !ReferenceEquals(candidate[entry.Key], entry.Value)) return original;
                return candidate;
            }
            catch { return original; }
        }
        internal static void ConfirmIndex(object effective)
        {
            try
            {
                if (!ReferenceEquals(effective, expectedIndex)) throw new InvalidOperationException("Effective index differs from prepared index");
                if (correctionReady) ProbeTrace.Mark("CORRECTION_APPLIED", "effective=" + ((IDictionary)effective).Count);
            }
            catch (Exception ex) { ProbeTrace.Mark("ERROR", "Index confirmation " + ex); }
        }
        internal static void ObserveResult(object result)
        {
            try
            {
                var issues = ((IEnumerable)result).Cast<object>().ToArray();
                foreach (var issue in issues)
                {
                    var info = Property(issue, "Info"); var dependency = Property(issue, "Dependency");
                    ProbeTrace.Mark("ORIGINAL_ISSUE", Line(new Dictionary<string, object> {
                        {"consumer", Property(info, "GUID")}, {"dependency", Property(dependency, "GUID")},
                        {"minimum", Property(dependency, "MinimumVersion")}, {"maximum", Property(dependency, "MaximumVersion")}, {"reason", Property(issue, "Reason")} }));
                }
                if (expectedIssues < 0 || issues.Length != expectedIssues) throw new InvalidOperationException("Original result differs from forecast");
                ProbeTrace.Mark("ORIGINAL_RESULT", "issues=" + issues.Length + " expected=" + expectedIssues + " correction=" + correctionReady);
            }
            catch (Exception ex) { ProbeTrace.Mark("ERROR", "Original result " + ex); }
        }
        private static string Line(object value) => Shared.DependencyFreeJson.Serialize(value).Replace("\r", "").Replace("\n", "");
        private static string ValidateCandidate(MethodInfo reader, string directory)
        {
            object candidate = reader.Invoke(null, new object[] { directory });
            if (candidate == null) return "SE candidate reader rejected source (including KillSwitch/AssetMode)";
            // This source has never been registered or published. Only its temporary resources are released.
            using ((IDisposable)Property(candidate, "Source"))
            {
                return ShadowInventory.ParseVersion((string)Property(Property(candidate, "Info"), "Version"), out _) ? null : "SE source has invalid version";
            }
        }
    }
}
