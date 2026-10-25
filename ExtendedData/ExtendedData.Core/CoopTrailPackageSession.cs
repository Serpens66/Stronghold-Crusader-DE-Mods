using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData.Core
{
    // One validated package per session owner. File metadata is an invalidation hint;
    // every cache miss still uses the complete, unchanged SHA-256 package contract.
    internal sealed class CoopTrailPackageSession
    {
        private readonly Func<string, CoopTrailPackage> load;
        private string identity;
        private string[] states;
        private CoopTrailPackage current;

        internal CoopTrailPackageSession(Func<string, CoopTrailPackage> load = null)
        {
            this.load = load ?? CoopTrailPackageCatalog.Load;
        }

        internal void Clear() { identity = null; states = null; current = null; }

        internal CoopTrailPackage GetOrLoad(IEnumerable<CoopTrailPackageOverview> overview,
            string packageId, out bool cacheHit, bool forceValidation = false)
        {
            cacheHit = false;
            CoopTrailPackageOverview[] candidates = overview
                .Where(item => string.Equals(item.Manifest.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length == 0) { Clear(); throw new FileNotFoundException("Coop Trail package is missing: " + packageId); }
            string nextIdentity = string.Join("\n", candidates.Select(item => item.RootPath + "\n" + item.ManifestContent));
            string[] before = CaptureCandidates(candidates);
            bool unchanged = current != null && string.Equals(identity, nextIdentity, StringComparison.Ordinal) &&
                states.SequenceEqual(before, StringComparer.Ordinal);
            if (unchanged && !forceValidation) { cacheHit = true; return current; }

            var valid = new List<CoopTrailPackage>();
            var errors = new List<string>();
            foreach (CoopTrailPackageOverview candidate in candidates)
            {
                try
                {
                    CoopTrailPackage loaded = load(candidate.RootPath);
                    if (!string.Equals(loaded.Manifest.PackageId, packageId, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Coop Trail package identity changed during selection.");
                    valid.Add(loaded);
                }
                catch (Exception exception) { errors.Add(exception.Message); }
            }
            if (valid.Count == 0) { Clear(); throw new InvalidDataException(string.Join("; ", errors)); }
            if (valid.Any(item => !SameReplica(valid[0], item)))
            {
                Clear();
                throw new InvalidDataException("Conflicting Coop Trail packageId [" + packageId + "] in: " +
                    string.Join(", ", valid.Select(item => item.RootPath)));
            }
            string[] after = CaptureCandidates(candidates);
            if (!before.SequenceEqual(after, StringComparer.Ordinal))
            {
                Clear();
                throw new IOException("Coop Trail package changed during validation; select it again after the update completes.");
            }
            // A forced restart validation may reuse the original prepared definitions only
            // after the full byte check succeeds and none of their inputs have changed.
            CoopTrailPackage result = unchanged ? current : valid[0];
            identity = nextIdentity;
            states = after;
            current = result;
            return result;
        }

        private static bool SameReplica(CoopTrailPackage left, CoopTrailPackage right) =>
            string.Equals(left.Manifest.DisplayName, right.Manifest.DisplayName, StringComparison.Ordinal) &&
            left.Manifest.MissionCount == right.Manifest.MissionCount &&
            string.Equals(left.Manifest.ContentFingerprint, right.Manifest.ContentFingerprint, StringComparison.OrdinalIgnoreCase);

        private static string[] CaptureCandidates(IEnumerable<CoopTrailPackageOverview> candidates)
        {
            var result = new List<string>();
            foreach (CoopTrailPackageOverview candidate in candidates)
            {
                string missions = Path.Combine(candidate.RootPath, "CoopMissions");
                result.Add(candidate.RootPath);
                result.Add(FileState(Path.Combine(candidate.RootPath, "cooptrail.json")));
                // Including the directory's file set detects newly added optional sidecars,
                // repairs of rejected replicas, removals and renames without reading assets.
                if (Directory.Exists(missions))
                    foreach (string file in Directory.GetFiles(missions, "*", SearchOption.AllDirectories)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)) result.Add(FileState(file));
                else result.Add("missing:" + missions);
            }
            return result.ToArray();
        }

        internal static string FileState(string path)
        {
            var file = new FileInfo(Path.GetFullPath(path));
            return file.FullName + "|" + (file.Exists ? file.Length + "|" + file.LastWriteTimeUtc.Ticks : "missing");
        }
    }
}
