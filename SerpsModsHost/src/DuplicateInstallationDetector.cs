using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SerpsModsHost
{
    internal static class DuplicateInstallationDetector
    {
        public static List<string> FindSeparateManifestDirectories(
            string pluginRoot,
            string expectedDirectory,
            string expectedGuid)
        {
            List<string> duplicates = new List<string>();
            if (string.IsNullOrWhiteSpace(pluginRoot) || !Directory.Exists(pluginRoot))
                return duplicates;

            string canonicalExpected = CanonicalizeDirectory(expectedDirectory);
            foreach (string infoPath in Directory.EnumerateFiles(pluginRoot, "info.json", SearchOption.AllDirectories))
            {
                string directory = Path.GetDirectoryName(infoPath);
                if (string.IsNullOrWhiteSpace(directory) || PathsEqual(directory, canonicalExpected))
                    continue;

                try
                {
                    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(infoPath)))
                    {
                        if (!document.RootElement.TryGetProperty("GUID", out JsonElement guidElement))
                            continue;
                        if (string.Equals(guidElement.GetString(), expectedGuid, StringComparison.OrdinalIgnoreCase))
                            duplicates.Add(CanonicalizeDirectory(directory));
                    }
                }
                catch (JsonException)
                {
                    // Unrelated malformed manifests are handled by their owning mod.
                }
                catch (IOException)
                {
                    // A transiently locked unrelated manifest must not disable this pack.
                }
                catch (UnauthorizedAccessException)
                {
                    // Access problems are reported by the caller if directory enumeration fails.
                }
            }

            duplicates.Sort(StringComparer.OrdinalIgnoreCase);
            return duplicates;
        }

        public static bool PathsEqual(string left, string right) =>
            string.Equals(
                CanonicalizeDirectory(left),
                CanonicalizeDirectory(right),
                StringComparison.OrdinalIgnoreCase);

        private static string CanonicalizeDirectory(string path) =>
            Path.GetFullPath(path ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
