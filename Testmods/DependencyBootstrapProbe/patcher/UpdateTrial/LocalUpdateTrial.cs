using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DependencyBootstrapProbe
{
    // Diagnostic-only input adapter. Never calls Steam, staging or deployment methods.
    internal static class LocalUpdateTrial
    {
        internal const string Folder = @"D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods\Testmods\DependencyBootstrapProbe\update-trial\remote";
        internal const string MapName = "DependencyBootstrapProbeLocalTrial.map";
        internal const string RequestName = "DependencyBootstrapProbe.local-update.json";
        internal static string[] OriginalPaths { get; private set; }
        internal static string[] AddedPaths { get; private set; } = new string[0];
        private static bool read;
        private static string request;
        internal static List<string> Apply(List<string> original)
        {
            OriginalPaths = original?.ToArray() ?? new string[0];
            AddedPaths = new string[0];
            try
            {
                if (!read)
                {
                    read = true;
                    string path = Path.Combine(BepInEx.Paths.ConfigPath, RequestName);
                    if (File.Exists(path)) { PairContract.Unlinked(path); request = File.ReadAllText(path); }
                }
                if (request == null) return original;
                string loaded = typeof(LocalUpdateTrial).Assembly.Location;
                var result = Extend(original, request, Folder, ProbeIdentity.Hash(loaded),
                    ProbeIdentity.Hash(Path.Combine(BepInEx.Paths.GameRootPath, PairContract.Runtime)), ProbeVersion.Value);
                if (!ReferenceEquals(result, original))
                {
                    AddedPaths = new[] { Folder };
                    PairDiagnostics.Mark("TRIAL_PATH_ADDED", "version=" + ProbeVersion.Value + " map=" + ProbeIdentity.Hash(Path.Combine(Folder, MapName)));
                }
                return result;
            }
            catch (Exception ex) { PairDiagnostics.Mark("TRIAL_REJECTED", ex.Message); return original; }
        }
        internal static List<string> Extend(List<string> original, string json, string folder, string patcherHash, string runtimeHash, string version)
        {
            if (json == null) return original;
            if (original == null) throw new InvalidDataException("Missing original list");
            var data = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
            if (data == null || data.Count != 4 || !data.ContainsKey("Schema") || !data.ContainsKey("TrialId") ||
                !data.ContainsKey("MapSHA256") || !data.ContainsKey("Builds") || !(data["Schema"] is long || data["Schema"] is int) || Convert.ToInt64(data["Schema"]) != 1 ||
                !(data["TrialId"] is string id) || !Guid.TryParseExact(id, "N", out _) || !(data["Builds"] is List<object> builds) || builds.Count != 2)
                throw new InvalidDataException("Invalid trial request");
            int matches = 0;
            foreach (var value in builds)
            {
                var build = value as Dictionary<string, object>;
                if (build == null || build.Count != 3 || !build.ContainsKey("Version") || !build.ContainsKey("PatcherSHA256") || !build.ContainsKey("RuntimeSHA256") ||
                    !(build["Version"] is string v) || !Version.TryParse(v, out _) || !Digest(build["PatcherSHA256"]) || !Digest(build["RuntimeSHA256"]))
                    throw new InvalidDataException("Invalid trial build");
                if ((string)build["Version"] == version && (string)build["PatcherSHA256"] == patcherHash && (string)build["RuntimeSHA256"] == runtimeHash) matches++;
            }
            if(matches != 1 || !Digest(data["MapSHA256"])) throw new InvalidDataException("Unauthorized trial build");
            PairContract.Unlinked(folder);
            var maps = Directory.GetFiles(folder,"*.map");
            string map = Path.Combine(folder, MapName);
            if (maps.Length != 1 || !string.Equals(maps[0], map, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ambiguous test map");
            PairContract.Unlinked(map);
            if (ProbeIdentity.Hash(map) != (string)data["MapSHA256"]) throw new InvalidDataException("Changed test package");
            if (original.Any(p => string.Equals(Path.GetFullPath(p), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Test path already present; origin ambiguous");
            var result = new List<string>(original); result.Add(folder); return result;
        }
        private static bool Digest(object value) => value is string s && s.Length == 64 && s.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        internal static bool IsSyntheticPackage(string path) => AddedPaths.Length != 0 &&
            string.Equals(Path.GetFullPath(path), Path.GetFullPath(Path.Combine(Folder, MapName)), StringComparison.OrdinalIgnoreCase);
    }
}
