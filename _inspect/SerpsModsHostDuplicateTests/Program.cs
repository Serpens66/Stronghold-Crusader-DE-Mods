using SerpsModsHost;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace SerpsModsHostDuplicateTests
{
    internal static class Program
    {
        private static int Main()
        {
            TestGlobalSettingsResetPolicy();
            TestGlobalSettingsResetUiContract();
            TestModSettingsRegistrationOrder();
            TestScriptExtenderCompatibility();
            TestModInventoryCompatibility();
            TestLobbyChatMessageFormatting();
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
                "ERROR: Mods differ: {Player} vs {Host}.",
                out _))
            {
                throw new InvalidOperationException("Equal Script Extender mod hashes were reported as different.");
            }

            if (!ModHashCompatibility.TryCreateMismatchMessage(
                "AAAAAAAAAAAAAAAA",
                "BBBBBBBBBBBBBBBB",
                "Alice",
                "Bob",
                "ERROR: Mods differ: {Player} vs {Host}.",
                out string mismatchMessage) ||
                !string.Equals(
                    mismatchMessage,
                    "ERROR: Mods differ: Alice vs Bob.",
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

        private static void TestGlobalSettingsResetPolicy()
        {
            if (GlobalSettingsResetPolicy.CanReset(false, false, true, 2, false) ||
                GlobalSettingsResetPolicy.CanReset(false, true, true, 0, false) ||
                GlobalSettingsResetPolicy.CanReset(false, true, true, 2, true) ||
                GlobalSettingsResetPolicy.CanReset(true, true, true, 2, false) ||
                !GlobalSettingsResetPolicy.CanReset(false, true, true, 2, false))
            {
                throw new InvalidOperationException("Global reset availability does not follow host, target, confirmation, and mission ownership.");
            }

            var first = new ResetTarget(10, false);
            var second = new ResetTarget(20, true);
            bool failed = false;
            try
            {
                GlobalSettingsResetPolicy.ApplyAtomically(
                    new[] { first, second },
                    target => target.Value,
                    target => target.ApplyDefault(),
                    (target, value) => target.Value = value);
            }
            catch (InvalidOperationException)
            {
                failed = true;
            }

            if (!failed || first.Value != 10 || second.Value != 20 || first.ApplyCount != 1 || second.ApplyCount != 1)
                throw new InvalidOperationException("A failed global reset did not restore every captured working value exactly once.");

            second.Fail = false;
            GlobalSettingsResetPolicy.ApplyAtomically(
                new[] { first, second },
                target => target.Value,
                target => target.ApplyDefault(),
                (target, value) => target.Value = value);
            if (first.Value != 0 || second.Value != 0)
                throw new InvalidOperationException("A successful global reset did not apply defaults to every target.");
        }

        private static void TestGlobalSettingsResetUiContract()
        {
            string workspace = FindWorkspaceRoot();
            string source = File.ReadAllText(Path.Combine(
                workspace, "SerpsModsHost", "src", "SerpsModsDiagnosticsViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                workspace, "SerpsModsHost", "Override", "ScriptExtenderUI", "SerpsModsStatus.xaml"));
            if (source.Contains("SerpsModsResetSettingsCompleted") ||
                xaml.Contains("GlobalSettingsResetSuccessVisibility") ||
                !source.Contains("globalResetStatus = string.Empty;") ||
                !xaml.Contains("GlobalSettingsResetErrorVisibility"))
            {
                throw new InvalidOperationException(
                    "The global reset must stay silent after success and retain a visible error-only status.");
            }
        }

        private sealed class ResetTarget
        {
            internal ResetTarget(int value, bool fail)
            {
                Value = value;
                Fail = fail;
            }

            internal int Value { get; set; }
            internal bool Fail { get; set; }
            internal int ApplyCount { get; private set; }

            internal void ApplyDefault()
            {
                ApplyCount++;
                Value = 0;
                if (Fail)
                    throw new InvalidOperationException("Expected reset failure.");
            }
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
            const string hostJson = "[" +
                "{\"g\":\"BugfixesAndQoL_Serp\",\"n\":\"Bugfixes\",\"v\":\"1.0.126\",\"w\":\"\",\"c\":false}," +
                "{\"g\":\"serpens66.testlord-serp.extended-package-test\",\"n\":\"Test Lord\",\"v\":\"1.0.1-test\",\"w\":\"\",\"c\":true}," +
                "{\"g\":\"HostGameplay\",\"n\":\"Host Gameplay\",\"v\":\"1.0.0\",\"w\":\"\",\"c\":false}," +
                "{\"g\":\"Versioned\",\"n\":\"Versioned\",\"v\":\"2.0.0\",\"w\":\"\",\"c\":false}" +
                "]";
            if (!ModInventoryCompatibility.TryDecodeScriptExtenderMetadata(
                hostJson,
                out List<ModInventoryEntry> host))
            {
                throw new InvalidOperationException("Valid Script Extender lobby metadata was rejected.");
            }

            List<ModInventoryEntry> client = ModInventoryCompatibility.BuildCanonicalLocalInventory(
                new[]
                {
                    new ModInventoryEntry(
                        "BugfixesAndQoL_Serp",
                        "Bugfixes asset",
                        "1.0.126",
                        false),
                    new ModInventoryEntry(
                        "serpens66.testlord-serp.extended-package-test",
                        "Test Lord",
                        "1.0.0-test",
                        true),
                    new ModInventoryEntry("ClientOnlyAsset", "Client Asset", "1.0.0", true),
                },
                new[]
                {
                    new ModInventoryEntry(
                        "bugfixesandqol_serp",
                        "Duplicate plugin",
                        "9.9.9",
                        false),
                    new ModInventoryEntry("ClientOnlyPlugin", "Client Plugin", "1.0.0", false),
                    new ModInventoryEntry("Versioned", "Versioned", "1.0.0", false),
                });

            ModInventoryEntry canonicalBugfixes = client.Single(
                entry => string.Equals(
                    entry.Guid,
                    "BugfixesAndQoL_Serp",
                    StringComparison.OrdinalIgnoreCase));
            if (!string.Equals(canonicalBugfixes.Version, "1.0.126", StringComparison.Ordinal) ||
                canonicalBugfixes.Clientside)
            {
                throw new InvalidOperationException("Asset metadata did not override the duplicate plugin GUID.");
            }

            ModInventoryDifference difference = ModInventoryCompatibility.Compare(host, client);
            if (difference.HostOnly.Count != 1 ||
                !difference.HostOnly[0].Guid.Contains("HostGameplay") ||
                difference.ClientOnly.Count != 1 || !difference.ClientOnly[0].Guid.Contains("ClientOnlyPlugin") ||
                difference.VersionMismatches.Count != 1 ||
                !difference.VersionMismatches[0].Client.Guid.Contains("Versioned"))
            {
                throw new InvalidOperationException("Gameplay mod differences were not classified correctly.");
            }

            var clientsideOnlyHost = new List<ModInventoryEntry>
            {
                new ModInventoryEntry("Visual", "Visual", "2.0.0", true),
                new ModInventoryEntry("HostVisual", "Host Visual", "1.0.0", true),
            };
            var clientsideOnlyClient = new List<ModInventoryEntry>
            {
                new ModInventoryEntry("Visual", "Visual", "1.0.0", true),
                new ModInventoryEntry("ClientVisual", "Client Visual", "1.0.0", true),
            };
            if (ModInventoryCompatibility.Compare(
                clientsideOnlyHost,
                clientsideOnlyClient).Count != 0)
            {
                throw new InvalidOperationException("Client-side-only mod differences were treated as incompatible.");
            }

            string[] malformedMetadata =
            {
                "{}",
                "[{\"g\":\"MissingFields\"}]",
                "[{\"g\":\"WrongType\",\"n\":\"Wrong\",\"v\":1,\"c\":false}]",
                "[{\"g\":\"Duplicate\",\"n\":\"One\",\"v\":\"1\",\"c\":false}," +
                    "{\"g\":\"duplicate\",\"n\":\"Two\",\"v\":\"1\",\"c\":false}]",
                new string('a', 8192),
                string.Empty,
            };
            if (malformedMetadata.Any(value =>
                ModInventoryCompatibility.TryDecodeScriptExtenderMetadata(value, out _)))
            {
                throw new InvalidOperationException("Malformed Script Extender lobby metadata was accepted.");
            }
        }

        private static void TestLobbyChatMessageFormatting()
        {
            var difference = new ModInventoryDifference();
            difference.HostOnly.Add(new ModInventoryEntry(
                "Long.Internal.Host.Guid",
                "Host Mod",
                "1.0.0",
                false));
            difference.ClientOnly.Add(new ModInventoryEntry(
                "Long.Internal.Client.Guid",
                "Client Mod",
                "2.0.0",
                false));
            for (int index = 0; index < 4; index++)
            {
                difference.VersionMismatches.Add(new ModVersionMismatch(
                    new ModInventoryEntry(
                        "Long.Internal.Version.Guid." + index,
                        "Version Mod " + index,
                        "1.0." + index,
                        false),
                    new ModInventoryEntry(
                        "Long.Internal.Version.Guid." + index,
                        "Version Mod " + index,
                        "2.0." + index,
                        false)));
            }

            IReadOnlyList<string> messages = LobbyChatMessageFormatter.BuildMessages(
                "ERROR: Mods differ: Alice vs lobby host Bob.",
                difference,
                "Host only",
                "Only Alice",
                "Different versions",
                "{Count} more differences are in the BepInEx log.",
                "Exact mod list unavailable.",
                "Check BepInEx\\plugins and CustomLords (local + Workshop). Full details: BepInEx log.");
            string combined = string.Join("\n", messages);
            if (!combined.Contains("Host Mod v1.0.0") ||
                !combined.Contains("Client Mod v2.0.0") ||
                !combined.Contains("Version Mod 0: 1.0.0 / 2.0.0") ||
                !combined.Contains("2 more differences") ||
                combined.Contains("Long.Internal") ||
                combined.Contains("AAAAAAAAAAAAAAAA") ||
                combined.Contains("BBBBBBBBBBBBBBBB"))
            {
                throw new InvalidOperationException("Compact lobby chat messages lost details or exposed internal identifiers.");
            }
            if (messages.Any(message =>
                message.Length > LobbyChatMessageFormatter.MaximumMessageLength ||
                message.IndexOf('\r') >= 0 ||
                message.IndexOf('\n') >= 0 ||
                HasUnpairedSurrogate(message)))
            {
                throw new InvalidOperationException("A lobby chat message violates the Vanilla-safe length or Unicode contract.");
            }

            IReadOnlyList<string> unavailable = LobbyChatMessageFormatter.BuildMessages(
                "Mismatch",
                null,
                "Host only",
                "Client only",
                "Versions",
                "{Count} more",
                "Exact inventory unavailable.",
                "See BepInEx log.");
            if (unavailable.Count != 2 ||
                !unavailable[1].Contains("Exact inventory unavailable.") ||
                !unavailable[1].Contains("See BepInEx log."))
            {
                throw new InvalidOperationException("Unavailable inventory guidance was not kept compact and complete.");
            }

            AssertFormattedSummaryLength(new string('a', 280), 280);
            AssertFormattedSummaryLength(new string('b', 279) + "😀", 280);
            AssertFormattedSummaryLength(new string('c', 298) + "😀", 280);
        }

        private static void AssertFormattedSummaryLength(string summary, int expectedLength)
        {
            IReadOnlyList<string> messages = LobbyChatMessageFormatter.BuildMessages(
                summary,
                new ModInventoryDifference(),
                "Host",
                "Client",
                "Versions",
                "{Count} more",
                "Unavailable",
                string.Empty);
            if (messages.Count != 1 || messages[0].Length != expectedLength ||
                HasUnpairedSurrogate(messages[0]))
            {
                throw new InvalidOperationException("Lobby summary truncation is not Unicode-safe or deterministic.");
            }
        }

        private static bool HasUnpairedSurrogate(string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        return true;
                    index++;
                }
                else if (char.IsLowSurrogate(value[index]))
                {
                    return true;
                }
            }
            return false;
        }

        private static void TestModSettingsRegistrationOrder()
        {
            object fixes = new object();
            object serpsMods = new object();
            object laterMod = new object();
            object missingMod = new object();
            var registrations = new ObservableCollection<object> { fixes, serpsMods };
            int collectionChanges = 0;
            NotifyCollectionChangedAction? lastAction = null;
            registrations.CollectionChanged += (_, args) =>
            {
                collectionChanges++;
                lastAction = args.Action;
            };

            if (!ModSettingsRegistrationOrder.PromoteToFront(registrations, serpsMods) ||
                !ReferenceEquals(registrations[0], serpsMods) ||
                !ReferenceEquals(registrations[1], fixes) ||
                collectionChanges != 1 ||
                lastAction != NotifyCollectionChangedAction.Move)
            {
                throw new InvalidOperationException("Serps Mods was not moved to the front with one Move notification.");
            }

            collectionChanges = 0;
            lastAction = null;
            if (ModSettingsRegistrationOrder.PromoteToFront(registrations, serpsMods) || collectionChanges != 0)
                throw new InvalidOperationException("An already-first Serps Mods registration was changed.");

            if (ModSettingsRegistrationOrder.PromoteToFront(registrations, missingMod) || collectionChanges != 0)
                throw new InvalidOperationException("A missing Serps Mods registration changed the collection.");

            registrations.Add(laterMod);
            if (!ReferenceEquals(registrations[0], serpsMods) ||
                !ReferenceEquals(registrations[registrations.Count - 1], laterMod))
            {
                throw new InvalidOperationException("A later registration did not remain behind Serps Mods.");
            }

            string workspace = FindWorkspaceRoot();
            string hostSource = File.ReadAllText(Path.Combine(
                workspace, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
            int promoteIndex = hostSource.IndexOf(
                "ModSettingsRegistrationOrder.PromoteToFront(registrations, registration);",
                StringComparison.Ordinal);
            int selectIndex = hostSource.IndexOf(
                "SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel.SelectedTab = registration;",
                StringComparison.Ordinal);
            if (promoteIndex < 0 || selectIndex <= promoteIndex)
            {
                throw new InvalidOperationException(
                    "Serps Mods must become the initial selected tab after it is promoted.");
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

            string workspace = FindWorkspaceRoot();
            string hostSource = File.ReadAllText(Path.Combine(
                workspace, "SerpsModsHost", "src", "SerpsModsHostPlugin.cs"));
            string localizationSource = File.ReadAllText(Path.Combine(
                workspace, "Shared", "SerpLocalization.cs"));
            if (!hostSource.Contains("SerpsModsScriptExtenderRequiredAction") ||
                !localizationSource.Contains("Required action: install a Script Extender version") ||
                !localizationSource.Contains("- {Name}: requires Script Extender {Minimum} or newer."))
            {
                throw new InvalidOperationException(
                    "Script Extender mismatch warning no longer names the mod/minimum or tells the player to update and restart.");
            }

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

        private static string FindWorkspaceRoot()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "SerpsModsHost")) &&
                    Directory.Exists(Path.Combine(directory.FullName, "Shared")))
                {
                    return directory.FullName;
                }
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Workspace root was not found from the test output directory.");
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
