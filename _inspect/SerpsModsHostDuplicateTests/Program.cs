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
            TestScriptExtenderCompatibility();
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

            PackManifest parsedManifest = PackManifestJson.Read(
                "{\"schemaversion\":1,\"packguid\":\"SerpsMods_Serp\",\"mods\":[{" +
                "\"guid\":\"Test_GUID\",\"files\":[{\"path\":\"Test.dll\",\"size\":12}]}]}");
            if (parsedManifest.SchemaVersion != 1 || parsedManifest.Mods.Count != 1 ||
                parsedManifest.Mods[0].Files.Count != 1 || parsedManifest.Mods[0].Files[0].Size != 12)
            {
                throw new InvalidOperationException("Dependency-free pack manifest mapping failed.");
            }

            string serialized = Shared.DependencyFreeJson.Serialize(new PackFileRecord
            {
                Path = "quoted\\\"path",
                Sha256 = "abc",
                Size = 42
            });
            if (!(Shared.DependencyFreeJson.Parse(serialized) is Dictionary<string, object> serializedObject) ||
                !string.Equals(serializedObject["Path"] as string, "quoted\\\"path", StringComparison.Ordinal) ||
                !serializedObject.ContainsKey("Size"))
            {
                throw new InvalidOperationException("Dependency-free property serialization failed.");
            }

            if (!(Shared.DependencyFreeJson.Parse("{\"items\":[1,2,],}", allowTrailingCommas: true)
                is Dictionary<string, object>))
            {
                throw new InvalidOperationException("Dependency-free trailing-comma mode failed.");
            }

            var stringDictionary = new Dictionary<string, string> { ["key"] = "value" };
            string dictionaryJson = Shared.DependencyFreeJson.Serialize(stringDictionary);
            if (!(Shared.DependencyFreeJson.Parse(dictionaryJson) is Dictionary<string, object> dictionaryObject) ||
                !string.Equals(dictionaryObject["key"] as string, "value", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Generic string dictionary was not serialized as a JSON object.");
            }

            DateTime timestamp = new DateTime(2026, 8, 19, 12, 34, 56, DateTimeKind.Utc);
            if (!(Shared.DependencyFreeJson.Parse(Shared.DependencyFreeJson.Serialize(timestamp)) is string timestampText) ||
                !string.Equals(timestampText, timestamp.ToString("O"), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("DateTime serialization is not stable.");
            }

            var cyclic = new CycleNode();
            cyclic.Next = cyclic;
            ExpectInvalidData(
                () => Shared.DependencyFreeJson.Serialize(cyclic),
                "cyclic object graph was accepted");

            int nesting = Shared.DependencyFreeJson.MaximumDepth + 2;
            string tooDeepJson = new string('[', nesting) + "0" + new string(']', nesting);
            ExpectInvalidData(
                () => Shared.DependencyFreeJson.Parse(tooDeepJson),
                "overly deep JSON was accepted");
            ExpectInvalidData(
                () => PackManifestJson.Read("{\"SchemaVersion\":1,\"schemaversion\":1}"),
                "ambiguous case-insensitive manifest properties were accepted");

            object unsigned = Shared.DependencyFreeJson.Parse(ulong.MaxValue.ToString());
            if (!(unsigned is ulong unsignedInteger) || unsignedInteger != ulong.MaxValue)
                throw new InvalidOperationException("UInt64 JSON roundtrip failed.");

            string repeated = Shared.DependencyFreeJson.Serialize(new PackFileRecord
            {
                Path = "ordered",
                Sha256 = "hash",
                Size = 1
            });
            int pathIndex = repeated.IndexOf("\"Path\"", StringComparison.Ordinal);
            int shaIndex = repeated.IndexOf("\"Sha256\"", StringComparison.Ordinal);
            int sizeIndex = repeated.IndexOf("\"Size\"", StringComparison.Ordinal);
            if (pathIndex < 0 || pathIndex >= shaIndex || shaIndex >= sizeIndex ||
                !string.Equals(repeated, Shared.DependencyFreeJson.Serialize(new PackFileRecord
                {
                    Path = "ordered",
                    Sha256 = "hash",
                    Size = 1
                }), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Property serialization order is not deterministic.");
            }

            if (ModHashCompatibility.TryCreateMismatchMessage(
                "AAAAAAAAAAAAAAAA",
                "AAAAAAAAAAAAAAAA",
                "Alice",
                "Bob",
                "{Player} differs from {Host}: {PlayerHash}/{HostHash}",
                out _))
            {
                throw new InvalidOperationException("Equal Script Extender mod hashes were reported as different.");
            }

            if (!ModHashCompatibility.TryCreateMismatchMessage(
                "AAAAAAAAAAAAAAAA",
                "BBBBBBBBBBBBBBBB",
                "Alice",
                "Bob",
                "{Player} differs from {Host}: {PlayerHash}/{HostHash}",
                out string mismatchMessage) ||
                !string.Equals(
                    mismatchMessage,
                    "Alice differs from Bob: AAAAAAAAAAAAAAAA/BBBBBBBBBBBBBBBB",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Different Script Extender mod hashes did not produce the expected message.");
            }

            if (ModHashCompatibility.TryCreateMismatchMessage(
                null,
                "BBBBBBBBBBBBBBBB",
                "Alice",
                "Bob",
                "{Player} differs from {Host}",
                out _))
            {
                throw new InvalidOperationException("A missing Script Extender mod hash produced a mismatch message.");
            }

            Console.WriteLine("PASS: host diagnostics, mod-hash comparison, and deterministic serialization.");
            Console.WriteLine("Duplicate: " + duplicates[0]);
            return 0;
        }

        private static void TestScriptExtenderCompatibility()
        {
            AssertResolvedVersion(
                "1.43.2",
                false,
                new ScriptExtenderVersionEvidence("info", "1.0.0"),
                new ScriptExtenderVersionEvidence("assembly", "1.43.2.0"),
                new ScriptExtenderVersionEvidence("product", "1.43.2+commit"));
            AssertResolvedVersion(
                null,
                true,
                new ScriptExtenderVersionEvidence("info", "1.0.0"),
                new ScriptExtenderVersionEvidence("assembly", "1.0.0.0"));
            AssertResolvedVersion(
                null,
                false,
                new ScriptExtenderVersionEvidence("info", "1.43.2"),
                new ScriptExtenderVersionEvidence("assembly", "1.44.0"));

            AssertCompatibility("1.43.2", "1.43.2", "", ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility("1.44.0", "1.43.2", null, ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility("1.43", "1.43.0", "1.43.0.0", ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility("1.43.1", "1.43.2", "", ScriptExtenderCompatibilityStatus.BelowMinimum);
            AssertCompatibility("1.44.1", "1.43.2", "1.44.0", ScriptExtenderCompatibilityStatus.AboveMaximum);
            AssertCompatibility("1.43.2", "1.44.0", "1.43.0", ScriptExtenderCompatibilityStatus.InvalidRange);
            AssertCompatibility("preview", "1.43.2", "", ScriptExtenderCompatibilityStatus.InvalidInstalledVersion);
            AssertCompatibility("1.43.2", "", "", ScriptExtenderCompatibilityStatus.InvalidMinimumVersion);
            AssertCompatibility("1.43.2", "1.43.2", "latest", ScriptExtenderCompatibilityStatus.InvalidMaximumVersion);
        }

        private static void AssertResolvedVersion(
            string expected,
            bool expectedOnlyPlaceholders = false,
            params ScriptExtenderVersionEvidence[] evidence)
        {
            ScriptExtenderVersionResolution result = ScriptExtenderVersionResolver.Resolve(evidence);
            if (!string.Equals(result.Version, expected, StringComparison.Ordinal) ||
                result.ContainsOnlyPlaceholders != expectedOnlyPlaceholders)
            {
                throw new InvalidOperationException(
                    $"Expected resolved Script Extender version '{expected ?? "<none>"}', " +
                    $"placeholderOnly={expectedOnlyPlaceholders}; got '{result.Version ?? "<none>"}', " +
                    $"placeholderOnly={result.ContainsOnlyPlaceholders}: {result.Diagnostic}");
            }
        }

        private static void AssertCompatibility(
            string installed,
            string minimum,
            string maximum,
            ScriptExtenderCompatibilityStatus expected)
        {
            ScriptExtenderCompatibilityResult result = ScriptExtenderCompatibility.Evaluate(installed, minimum, maximum);
            if (result.Status != expected)
            {
                throw new InvalidOperationException(
                    $"Compatibility {installed}/{minimum}/{maximum}: expected {expected}, got {result.Status}.");
            }
        }

        private static void WriteManifest(string directory, string guid)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                Path.Combine(directory, "info.json"),
                "{\"GUID\":\"" + guid + "\",\"Version\":\"1.0.0\"}");
        }

        private static void ExpectInvalidData(Action action, string message)
        {
            try
            {
                action();
            }
            catch (InvalidDataException)
            {
                return;
            }
            throw new InvalidOperationException(message);
        }

        private sealed class CycleNode
        {
            public CycleNode Next { get; set; }
        }
    }
}
