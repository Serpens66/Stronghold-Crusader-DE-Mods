using SerpsModsHost;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SerpsModsHostDuplicateTests
{
    internal static class Program
    {
        private static int Main()
        {
            TestScriptExtenderCompatibility();
            TestModInventoryCompatibility();
            TestPluginLoadDiagnostics();
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
            if (!RegisteredAssetDirectoryPolicy.TryValidate(expected, true, expected, out _) ||
                !RegisteredAssetDirectoryPolicy.TryValidate(expected, true, expected.ToUpperInvariant(), out _))
            {
                throw new InvalidOperationException("Same or case-varied registered paths were rejected.");
            }
            if (RegisteredAssetDirectoryPolicy.TryValidate(expected, false, null, out _) ||
                RegisteredAssetDirectoryPolicy.TryValidate(expected, true, separate, out _))
            {
                throw new InvalidOperationException("Missing or conflicting GUID registration was accepted.");
            }
            // The extender keeps the first GUID registration. A later child must therefore be
            // rejected against that authoritative path instead of incrementing registeredCount.
            string firstRegisteredDirectory = expected;
            if (RegisteredAssetDirectoryPolicy.TryValidate(separate, true, firstRegisteredDirectory, out _))
                throw new InvalidOperationException("A later child was allowed to replace the first GUID directory.");

            PackManifest parsedManifest = PackManifestJson.Read(
                "{\"schemaversion\":2,\"packguid\":\"SerpsMods_Serp\"," +
                "\"infrastructure\":[{\"guid\":\"APIShared_Serp\",\"state\":\"Infrastructure\"}]," +
                "\"mods\":[{\"guid\":\"Test_GUID\",\"files\":[{\"path\":\"Test.dll\",\"size\":12}]}]}");
            if (parsedManifest.SchemaVersion != 2 || parsedManifest.Infrastructure.Count != 1 ||
                parsedManifest.Infrastructure[0].Guid != "APIShared_Serp" || parsedManifest.Mods.Count != 1 ||
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

        private static void TestPluginLoadDiagnostics()
        {
            var apiShared = new PackModRecord
            {
                Name = "APIShared",
                Guid = "APIShared_Serp",
                Version = "0.3.0"
            };
            var child = new PackModRecord
            {
                Name = "Bugfixes and QoL",
                Guid = "BugfixesAndQoL_Serp",
                Version = "1.0.142"
            };

            string missingApi = PackPluginDiagnosticMessages.MissingInfrastructure(apiShared);
            if (!missingApi.Contains("APIShared is missing or was not loaded") ||
                !missingApi.Contains("APIShared_Serp v0.3.0"))
            {
                throw new InvalidOperationException("Missing APIShared diagnostic is not explicit enough.");
            }

            string childWithoutApi = PackPluginDiagnosticMessages.MissingChild(child, false);
            if (!childWithoutApi.Contains("Bugfixes and QoL (BugfixesAndQoL_Serp) v1.0.142") ||
                !childWithoutApi.Contains("APIShared is missing or incompatible"))
            {
                throw new InvalidOperationException("Missing child diagnostic does not identify APIShared as a possible cause.");
            }

            string childWithApi = PackPluginDiagnosticMessages.MissingChild(child, true);
            if (!childWithApi.Contains("APIShared is loaded") ||
                childWithApi.Contains("APIShared is missing"))
            {
                throw new InvalidOperationException("Missing child diagnostic incorrectly blames an available APIShared.");
            }

            string oldExtender = PackPluginDiagnosticMessages.MissingChildForScriptExtender(
                child,
                "2.3.0",
                "2.4.0",
                string.Empty);
            if (!oldExtender.Contains("installed Script Extender 2.3.0 is too old") ||
                !oldExtender.Contains("requires version 2.4.0 or newer") ||
                oldExtender.Contains("APIShared is missing"))
            {
                throw new InvalidOperationException("Outdated Script Extender diagnostic is incomplete or misleading.");
            }

            string wrongApiVersion = PackPluginDiagnosticMessages.InfrastructureVersionMismatch(apiShared, "0.2.1");
            if (!wrongApiVersion.Contains("loaded APIShared version is incompatible") ||
                !wrongApiVersion.Contains("expected 0.3.0, actual 0.2.1"))
            {
                throw new InvalidOperationException("APIShared version mismatch diagnostic is incomplete.");
            }
        }

        private static void TestModInventoryCompatibility()
        {
            var host = new List<ModInventoryEntry>
            {
                new ModInventoryEntry("plugin", "BugfixesAndQoL_Serp", "1.0.126"),
                new ModInventoryEntry("asset", "BugfixesAndQoL_Serp", "1.0.126"),
                new ModInventoryEntry("asset", "serpens66.testlord-serp.extended-package-test", "1.0.0-test"),
                new ModInventoryEntry("plugin", "Versioned", "2.0.0"),
            };
            var client = new List<ModInventoryEntry>
            {
                new ModInventoryEntry("plugin", "BugfixesAndQoL_Serp", "1.0.126"),
                new ModInventoryEntry("asset", "BugfixesAndQoL_Serp", "1.0.126"),
                new ModInventoryEntry("plugin", "ClientOnly", "1.0.0"),
                new ModInventoryEntry("plugin", "Versioned", "1.0.0"),
            };

            string encoded = ModInventoryCompatibility.Encode(host);
            if (!ModInventoryCompatibility.TryDecode(encoded, out List<ModInventoryEntry> decoded) ||
                !string.Equals(encoded, ModInventoryCompatibility.Encode(decoded), StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Mod inventory encoding is not deterministic.");
            }

            ModInventoryDifference difference = ModInventoryCompatibility.Compare(decoded, client);
            if (difference.HostOnly.Count != 1 ||
                !difference.HostOnly[0].Contains("serpens66.testlord-serp.extended-package-test") ||
                difference.ClientOnly.Count != 1 || !difference.ClientOnly[0].Contains("ClientOnly") ||
                difference.VersionMismatches.Count != 1 || !difference.VersionMismatches[0].Contains("Versioned"))
            {
                throw new InvalidOperationException("Mod inventory differences were not classified correctly.");
            }

            if (ModInventoryCompatibility.TryDecode("v2", out _) ||
                ModInventoryCompatibility.TryDecode("v1\ninvalid", out _) ||
                ModInventoryCompatibility.TryDecode("v1\ncGx1Z2lu|/w==|MS4w", out _) ||
                ModInventoryCompatibility.TryDecode(new string('a', 8192), out _) ||
                ModInventoryCompatibility.TryDecode(string.Empty, out _))
            {
                throw new InvalidOperationException("Malformed mod inventory metadata was accepted.");
            }
        }

        private static void TestScriptExtenderCompatibility()
        {
            string placeholder = VersionText(1, 0, 0);
            string placeholderAssembly = VersionText(1, 0, 0, 0);
            string installed = VersionText(1, 43, 2);
            string installedAssembly = VersionText(1, 43, 2, 0);
            string installedProduct = installed + "+commit";
            string newer = VersionText(1, 44, 0);

            AssertResolvedVersion(
                installed,
                false,
                new ScriptExtenderVersionEvidence("info", placeholder),
                new ScriptExtenderVersionEvidence("assembly", installedAssembly),
                new ScriptExtenderVersionEvidence("product", installedProduct));
            AssertResolvedVersion(
                null,
                true,
                new ScriptExtenderVersionEvidence("info", placeholder),
                new ScriptExtenderVersionEvidence("assembly", placeholderAssembly));
            AssertResolvedVersion(
                null,
                false,
                new ScriptExtenderVersionEvidence("info", installed),
                new ScriptExtenderVersionEvidence("assembly", newer));

            AssertCompatibility(installed, installed, "", ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility(newer, installed, null, ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility(VersionText(1, 43), VersionText(1, 43, 0), VersionText(1, 43, 0, 0), ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility(VersionText(1, 43, 1), installed, "", ScriptExtenderCompatibilityStatus.BelowMinimum);
            AssertCompatibility(VersionText(1, 44, 1), installed, newer, ScriptExtenderCompatibilityStatus.AboveMaximum);
            AssertCompatibility(installed, newer, VersionText(1, 43, 0), ScriptExtenderCompatibilityStatus.InvalidRange);
            AssertCompatibility("preview", installed, "", ScriptExtenderCompatibilityStatus.InvalidInstalledVersion);
            AssertCompatibility(installed, "", "", ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility(installed, null, newer, ScriptExtenderCompatibilityStatus.Compatible);
            AssertCompatibility(VersionText(1, 44, 1), null, newer, ScriptExtenderCompatibilityStatus.AboveMaximum);
            AssertCompatibility(installed, installed, "latest", ScriptExtenderCompatibilityStatus.InvalidMaximumVersion);

            AssertCompatibilityIssues(
                installed,
                0,
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Compatible",
                    MinimumVersion = installed,
                    MaximumVersion = newer
                });
            AssertCompatibilityIssues(
                installed,
                1,
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Single newer requirement",
                    MinimumVersion = newer
                });
            AssertCompatibilityIssues(
                installed,
                2,
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Needs newer",
                    MinimumVersion = newer
                },
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Needs older",
                    MaximumVersion = VersionText(1, 42, 9)
                },
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Compatible",
                    MinimumVersion = VersionText(1, 42, 0)
                });
            AssertCompatibilityIssues(
                installed,
                3,
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Invalid minimum",
                    MinimumVersion = "preview"
                },
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Invalid maximum",
                    MaximumVersion = "latest"
                },
                new ScriptExtenderCompatibilityRequirement
                {
                    Name = "Invalid range",
                    MinimumVersion = newer,
                    MaximumVersion = installed
                });

            var manifest = new PackManifest
            {
                Infrastructure = new List<PackModRecord>
                {
                    new PackModRecord { Name = "Infrastructure", State = "Infrastructure" }
                },
                Mods = new List<PackModRecord>
                {
                    new PackModRecord { Name = "Active", State = "Active" },
                    new PackModRecord { Name = "Retired", State = "Retired" },
                    new PackModRecord { Name = "Case insensitive", State = "active" }
                }
            };
            List<PackModRecord> runtimeRecords = ScriptExtenderCompatibility.SelectRuntimePackRecords(manifest);
            if (runtimeRecords.Count != 3 ||
                runtimeRecords[0].Name != "Infrastructure" ||
                runtimeRecords[1].Name != "Active" ||
                runtimeRecords[2].Name != "Case insensitive" ||
                runtimeRecords.Any(record => record.Name == "Retired"))
            {
                throw new InvalidOperationException("Runtime asset selection did not preserve infrastructure-first order or exclude retired mods.");
            }
        }

        private static string VersionText(params int[] parts) => string.Join(".", parts);

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

        private static void AssertCompatibilityIssues(
            string installed,
            int expectedCount,
            params ScriptExtenderCompatibilityRequirement[] requirements)
        {
            List<ScriptExtenderCompatibilityIssue> issues =
                ScriptExtenderCompatibility.EvaluateAll(installed, requirements);
            if (issues.Count != expectedCount)
            {
                throw new InvalidOperationException(
                    $"Expected {expectedCount} Script Extender compatibility issues, got {issues.Count}.");
            }
            if (issues.Any(issue => string.IsNullOrWhiteSpace(issue.Requirement.Name)))
                throw new InvalidOperationException("A compatibility issue lost its component name.");
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
