using System;
using System.Collections.Generic;
using System.Linq;

namespace DependencyBootstrapProbe
{
    internal sealed class LifecyclePackage
    {
        public string Path { get; set; }
        public string Guid { get; set; }
        public string Version { get; set; }
        public string Manifest { get; set; }
        public bool Loaded { get; set; }
        public List<string> Payload { get; set; } = new List<string>();
        public string Error { get; set; }
        public string LocalState { get; set; }
        public string LocalEvidence { get; set; }
    }
    internal sealed class LifecycleOwner
    {
        public string Guid { get; set; }
        public string Version { get; set; }
        public string Path { get; set; }
        public string SHA256 { get; set; }
        public string State { get; set; }
        public string Reason { get; set; }
        public string[] Claims { get; set; }
    }
    internal static class LifecycleOwnership
    {
        internal static LifecycleOwner Evaluate(string guid, string version, string path, string hash, string relative,
            IList<LifecyclePackage> packages, bool complete)
        {
            var matches = packages.Where(p => p.Loaded && p.Manifest == "BepInEx" && p.Payload.Any(e => string.Equals(e, relative, StringComparison.OrdinalIgnoreCase))).ToArray();
            string state = matches.Length > 1 ? "Ambiguous" : !complete || relative == null || matches.Any(p => p.Error != null || p.LocalState == "Conflict") ? "Unverifiable" : matches.Length == 1 ? "Unique" : "NotFound";
            return new LifecycleOwner { Guid = guid, Version = version, Path = path, SHA256 = hash, State = state,
                Claims = matches.Select(p => p.Path).ToArray(), Reason = state == "Unique" ? "One observed package claims this path; not an ownership receipt or byte-equivalence proof" :
                state == "NotFound" ? "No observed package claim; manual installation is possible, absence does not prove unsubscribe" :
                state == "Ambiguous" ? "Multiple observed packages claim the path" : "Incomplete observation cannot establish exclusive ownership" };
        }
        internal static void BindLocal(LifecyclePackage package, string path, IDictionary<string, object> metadata)
        {
            package.LocalEvidence = path;
            if (metadata == null) { package.LocalState = "NotFound"; return; }
            object guid, version;
            package.LocalState = metadata.TryGetValue("GUID", out guid) && metadata.TryGetValue("Version", out version) &&
                string.Equals(guid as string, package.Guid, StringComparison.Ordinal) &&
                string.Equals(version as string, package.Version, StringComparison.Ordinal) ? "MatchesObservedMetadata" : "Conflict";
        }
    }
}
