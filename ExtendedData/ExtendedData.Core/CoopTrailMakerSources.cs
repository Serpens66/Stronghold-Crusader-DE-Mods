using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ExtendedData.Core
{
    internal sealed class CoopTrailMakerSource
    {
        internal CoopTrailMakerSource(CoopTrailPackageOverview overview, string localRoot, int missionCount)
        {
            PackageRoot = overview.RootPath;
            Manifest = overview.Manifest;
            Workshop = !string.Equals(Path.GetDirectoryName(PackageRoot), Path.GetFullPath(localRoot),
                StringComparison.OrdinalIgnoreCase);
            SelectionName = Workshop ? Manifest.DisplayName + " [Steam Workshop]" : Path.GetFileName(PackageRoot);
            MissionCount = missionCount;
        }

        internal string SelectionName { get; }
        internal string PackageRoot { get; }
        internal CoopTrailPackageManifest Manifest { get; }
        internal int MissionCount { get; }
        internal bool Workshop { get; }
    }

    internal sealed class CoopTrailMakerSources
    {
        private readonly CoopTrailPackageCatalog catalog = new CoopTrailPackageCatalog();

        // Menu discovery may read manifests and enumerate file names, never mission contents.
        internal IReadOnlyList<CoopTrailMakerSource> Discover(IEnumerable<string> roots, string localRoot,
            Action<string> error = null)
        {
            var sources = new List<CoopTrailMakerSource>();
            foreach (var group in catalog.Discover(roots, error)
                .GroupBy(item => item.Manifest.PackageId, StringComparer.OrdinalIgnoreCase))
            {
                CoopTrailPackageOverview first = group.First();
                if (group.Any(item => !string.Equals(item.Manifest.DisplayName, first.Manifest.DisplayName, StringComparison.Ordinal) ||
                    item.Manifest.MissionCount != first.Manifest.MissionCount ||
                    !string.Equals(item.Manifest.ContentFingerprint, first.Manifest.ContentFingerprint, StringComparison.OrdinalIgnoreCase)))
                {
                    error?.Invoke("Ignored duplicate Coop Trail packageId [" + group.Key + "] in: " +
                        string.Join(", ", group.Select(item => item.RootPath)));
                    continue;
                }
                // Roots are supplied local-first, as in the full package catalog.
                try
                {
                    string source = GetSourceDirectory(first.RootPath);
                    int count = Directory.EnumerateFiles(source, "*.trail").Count();
                    if (count > 0) sources.Add(new CoopTrailMakerSource(first, localRoot, count));
                }
                catch (Exception exception)
                {
                    error?.Invoke("Ignored Coop Trail Maker source [" + first.RootPath + "]: " + exception.Message);
                }
            }
            return sources.OrderBy(item => item.Manifest.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string GetSourceDirectory(string packageRoot)
        {
            string nested = Path.Combine(packageRoot, CoopTrailPackagePublication.TrailMakerSourceDirectory);
            // Older packages kept their editable sources at the package root.
            return Directory.Exists(nested) ? nested : packageRoot;
        }

        internal static void RequestImport(string packageRoot, Action requestConfirmation, Action<Exception> rejected)
        {
            try
            {
                // Discovery is not a content certificate. Validate only the chosen physical
                // package before Vanilla can offer its destructive import confirmation.
                CoopTrailPackageCatalog.Load(packageRoot);
                if (!Directory.EnumerateFiles(GetSourceDirectory(packageRoot), "*.trail").Any())
                    throw new InvalidDataException("The selected Coop Trail has no editable Trail Maker missions.");
            }
            catch (Exception exception)
            {
                rejected(exception);
                return;
            }
            // Keep Vanilla/UI failures separate from package-validation failures.
            requestConfirmation();
        }
    }
}
