using SerpsModsHost;
using System;
using System.Collections.Generic;
using System.IO;

namespace SerpsModsHostDuplicateTests
{
    internal static class Program
    {
        private static int Main()
        {
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runs", Guid.NewGuid().ToString("N"));
            string pluginRoot = Path.Combine(root, "BepInEx", "plugins");
            string expected = Path.Combine(pluginRoot, "SerpsMods_Serp", "Mods", "Test_GUID");
            string separate = Path.Combine(pluginRoot, "Test_GUID");
            string unrelated = Path.Combine(pluginRoot, "Other_GUID");
            string malformed = Path.Combine(pluginRoot, "Malformed");

            WriteManifest(expected, "Test_GUID");
            WriteManifest(separate, "Test_GUID");
            WriteManifest(unrelated, "Other_GUID");
            Directory.CreateDirectory(malformed);
            File.WriteAllText(Path.Combine(malformed, "info.json"), "{broken");

            List<string> duplicates = DuplicateInstallationDetector.FindSeparateManifestDirectories(
                pluginRoot,
                expected,
                "Test_GUID");

            if (duplicates.Count != 1 || !DuplicateInstallationDetector.PathsEqual(duplicates[0], separate))
                throw new InvalidOperationException("Expected exactly the separate Test_GUID package.");
            if (!DuplicateInstallationDetector.PathsEqual(expected, Path.Combine(expected, ".")))
                throw new InvalidOperationException("Equivalent paths were not recognized.");

            Console.WriteLine("PASS: packed copy ignored; separate duplicate detected; unrelated and malformed manifests ignored.");
            Console.WriteLine("Duplicate: " + duplicates[0]);
            return 0;
        }

        private static void WriteManifest(string directory, string guid)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "info.json"),
                "{\"GUID\":\"" + guid + "\",\"Version\":\"1.0.0\"}");
        }
    }
}
