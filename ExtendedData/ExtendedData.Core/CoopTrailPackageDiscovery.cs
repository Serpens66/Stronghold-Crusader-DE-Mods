using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData.Core
{
    // Discovery is deliberately not a certificate of content validity. Only Load verifies assets.
    public sealed class CoopTrailPackageOverview
    {
        public string RootPath { get; internal set; }
        public CoopTrailPackageManifest Manifest { get; internal set; }
        internal string ManifestContent { get; set; }
    }

    public sealed partial class CoopTrailPackageCatalog
    {
        private HashSet<string> discoveryErrors = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<CoopTrailPackageOverview> Discover(IEnumerable<string> contentRoots,
            Action<string> error = null)
        {
            var result = new List<CoopTrailPackageOverview>();
            var errors = new HashSet<string>(StringComparer.Ordinal);
            var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in (contentRoots ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path)).Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(root)) continue;
                    foreach (string directory in Directory.GetDirectories(root)
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                    {
                        if (Path.GetFileName(directory).StartsWith(".cooptrail-build-", StringComparison.OrdinalIgnoreCase) ||
                            !directories.Add(directory)) continue;
                        string path = Path.Combine(directory, "cooptrail.json");
                        if (!File.Exists(path)) continue;
                        try
                        {
                            // Keep the exact small manifest too: timestamp-preserving edits to it
                            // must invalidate a selected package just as ordinary file edits do.
                            string content = File.ReadAllText(path);
                            CoopTrailPackageManifest manifest = CoopTrailPackageManifestJson.Parse(content);
                            CoopTrailPackageManifestJson.Validate(manifest);
                            result.Add(new CoopTrailPackageOverview {
                                RootPath = Path.GetFullPath(directory), Manifest = manifest, ManifestContent = content,
                            });
                        }
                        catch (Exception exception)
                        {
                            errors.Add("Ignored Coop Trail manifest [" + Path.GetFileName(directory) + "]: " + exception.Message);
                        }
                    }
                }
                catch (Exception exception)
                {
                    errors.Add("Could not discover Coop Trail packages in [" + root + "]: " + exception.Message);
                }
            }
            foreach (string message in errors.Except(discoveryErrors).OrderBy(value => value, StringComparer.Ordinal))
                error?.Invoke(message);
            discoveryErrors = errors;
            return result;
        }
    }
}
