using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace PreplacedTest.Tests
{
    internal static class Program
    {
        private const string ExpectedNativeHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private static int checks;

        private static int Main()
        {
            try
            {
                TestLegacyTimerFix();
                TestPortalAndActivationModels();
                TestBreachModel();
                TestOverlayProjectionAndRestoration();
                TestRuntimeShape();
                TestManifestContracts();
                TestNativeContracts();
                Console.WriteLine($"PreplacedTest fix tests passed: {checks} checks.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void TestLegacyTimerFix()
        {
            string eligible = LegacyTimerFixEligibility.Classify(true, false, 0xD4, 0xD5, 1, 1, 0, 1);
            Check(LegacyTimerFixEligibility.IsEligible(eligible), "fresh legacy transfer rejected");
            Check(!LegacyTimerFixEligibility.IsEligible(
                LegacyTimerFixEligibility.Classify(true, true, 0xD4, 0xD5, 1, 1, 0, 1)), "save accepted");
            Check(!LegacyTimerFixEligibility.IsEligible(
                LegacyTimerFixEligibility.Classify(true, false, 0xD4, 0xD5, 0, 0, 0, 0)), "zero source accepted");
            Check(!LegacyTimerFixEligibility.IsEligible(
                LegacyTimerFixEligibility.Classify(true, false, 0xD4, 0xD5, 1, 1, 1, 1)), "active target accepted");
            Check(LegacyTimerFixEligibility.IsApplicationEligible(
                LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, false, 1)),
                "matching stable tower rejected");
            Check(!LegacyTimerFixEligibility.IsApplicationEligible(
                LegacyTimerFixEligibility.ClassifyAtApplication(eligible, false, false, 1)),
                "missing tower accepted");
            Check(!LegacyTimerFixEligibility.IsApplicationEligible(
                LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, true, 1)),
                "later damage accepted");
            Check(CrushedTimerTransition.IsActivation(0, 1) && !CrushedTimerTransition.IsActivation(1, 2),
                "timer activation contract changed");
            var identity = new PreplacedIdentity(4, 77, 8, 122);
            Check(identity.MatchesStableRecord(4, 77, 122), "stable preplaced identity mismatch");
            Check(!identity.MatchesStableRecord(5, 77, 122), "reused building slot accepted");
        }

        private static void TestPortalAndActivationModels()
        {
            Check(PortalOwnerSynchronizationModel.IsEligible(false, true, true, true, true, true,
                true, true, true, true, true), "valid preplaced portal rejected");
            Check(!PortalOwnerSynchronizationModel.IsEligible(true, true, true, true, true, true,
                true, true, true, true, true), "save portal modified");
            Check(!PortalOwnerSynchronizationModel.IsEligible(false, false, true, true, true, true,
                true, true, true, true, true), "later portal modified");
            Check(!PortalOwnerSynchronizationModel.IsEligible(false, true, true, false, true, true,
                true, true, true, true, true), "destroyed portal modified");
            Check(!PortalOwnerSynchronizationModel.IsEligible(false, true, false, true, true, true,
                true, true, true, true, true), "ambiguous portal modified");
            Check(EconomyFixActivationModel.Initial(false, WallAccessRole.GatedWallCandidate, true) ==
                EconomyFixActivationState.PendingPortal, "portal did not become pending");
            Check(EconomyFixActivationModel.Initial(false, WallAccessRole.ClosedWallCandidate, false) ==
                EconomyFixActivationState.PendingBreach, "closed wall did not await breach");
            Check(EconomyFixActivationModel.Initial(true, WallAccessRole.GatedWallCandidate, true) ==
                EconomyFixActivationState.None, "save activated economy fix");
            Check(EconomyFixActivationModel.Activate(EconomyFixActivationState.PendingPortal) ==
                EconomyFixActivationState.ActivePortal, "portal activation failed");
            Check(EconomyFixActivationModel.Activate(EconomyFixActivationState.PendingBreach) ==
                EconomyFixActivationState.ActiveBreach, "breach activation failed");
            Check(WallOwnerEncodingResolver.Resolve(4, 0) == WallOwnerEncoding.OneBased &&
                WallOwnerEncodingResolver.Decode(8, WallOwnerEncoding.OneBased) == 8,
                "one-based wall owner contract failed");
            Check(WallOwnerEncodingResolver.Resolve(0, 4) == WallOwnerEncoding.ZeroBased &&
                WallOwnerEncodingResolver.Decode(7, WallOwnerEncoding.ZeroBased) == 8,
                "zero-based wall owner contract failed");
        }

        private static void TestBreachModel()
        {
            Check(WallBreachConfirmation.IsConfirmed(true, 4, 5, 7, 7), "real breach rejected");
            Check(!WallBreachConfirmation.IsConfirmed(false, 4, 5, 7, 7), "damage without loss accepted");
            Check(!WallBreachConfirmation.IsConfirmed(true, 4, 5, 7, 8), "closed wall accepted");
            Check(!WallBreachConfirmation.IsConfirmed(true, 4, 4, 7, 7), "PCL relabel accepted as breach");
            Check(WallAccessRoleClassifier.Classify(20, 4) == WallAccessRole.GatedWallCandidate,
                "gated enclosure role failed");
            Check(WallAccessRoleClassifier.Classify(20, 0) == WallAccessRole.ClosedWallCandidate,
                "closed enclosure role failed");
        }

        private static void TestOverlayProjectionAndRestoration()
        {
            byte projected = EconomyGridOverlayProjection.ProjectOutsideCount(
                new[] { 1, 1, 2, 0, 3 }, new HashSet<int> { 1, 3 });
            Check(projected == 2, "player-specific PCL projection is wrong");
            byte[] grid = { 3, 4, 5, 6 };
            byte[] before = (byte[])grid.Clone();
            int[] changed = { 0, 2 };
            byte[] originals = { grid[0], grid[2] };
            try
            {
                grid[0] = 0;
                grid[2] = 1;
                throw new InvalidOperationException("expected test exception");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                for (int index = 0; index < changed.Length; index++)
                    grid[changed[index]] = originals[index];
            }
            Check(EconomyGridOverlayProjection.RestoredExactly(before, grid),
                "overlay exception path did not restore exactly");
        }

        private static void TestRuntimeShape()
        {
            string root = ProjectRoot();
            string runtime = File.ReadAllText(Path.Combine(root, "src", "PreplacedTestRuntime.cs"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "PreplacedTestPlugin.cs"));
            string allRuntime = string.Join("\n", Directory.GetFiles(Path.Combine(root, "src"), "*.cs")
                .Select(File.ReadAllText));
            Check(Regex.Matches(runtime, @"transaction\.AddDetour\(").Count == 10,
                "native fix set must contain exactly ten detours");
            Check(Regex.Matches(runtime, @"transaction\.AddContextHook\(").Count == 1,
                "native fix set must contain exactly one context hook");
            Check(!Regex.IsMatch(allRuntime,
                @"\b(?:Update|LateUpdate|FixedUpdate|OnDestroy|OnDisable|OnApplicationQuit)\s*\("),
                "polling or Unity teardown method remains");
            Check(!Regex.IsMatch(allRuntime,
                @"JavaScriptSerializer|System\.Text\.Json|Newtonsoft|DataContractJsonSerializer|JsonUtility"),
                "forbidden runtime JSON dependency remains");
            Check(!allRuntime.Contains("DiagnosticCounter") && !allRuntime.Contains("ShadowEconomy") &&
                !File.Exists(Path.Combine(root, "src", "DiagnosticModel.cs")),
                "diagnostic model remains in runtime");
            Check(!plugin.Contains("TestedScript") && !plugin.Contains("TestedApi") &&
                !plugin.Contains("TestedRedBird") && !plugin.Contains("LoadedPluginVersion") &&
                !plugin.Contains("LogCompatibility"), "custom version comparison remains");
            Check(!runtime.Contains("DateTime") && !runtime.Contains("Thread.Sleep") &&
                !runtime.Contains("System.Threading.Timer"), "timer or polling throttle remains");
            Check(runtime.Contains("new byte[EconomyGridCellCount]") &&
                runtime.Contains("new int[EconomyGridCellCount]"), "overlay scratch buffers are not reusable");
            Check(runtime.Contains("finally") && runtime.Contains("RestoreEconomyGridOverlay"),
                "overlay restoration is not exception-safe");
            Check(runtime.Contains("activeEconomyOverlayScopes") &&
                runtime.Contains("ApplyWoodScoreFloor"), "scoped wood fix context missing");
            Check(Regex.Matches(runtime, @"Def\(").Count == 13,
                "unexpected native signatures remain");
            Check(runtime.Contains("new LegacyRuinTimerFix()") &&
                runtime.Contains("new PreplacedEconomyAccessFix()"),
                "independent transfer components are not rooted by the runtime");

            var wrappers = new Dictionary<string, int>
            {
                ["FarmSearch"] = 2,
                ["ResourceSearch"] = 2,
                ["WoodSearch"] = 2,
                ["NearbySearch"] = 3
            };
            foreach (KeyValuePair<string, int> wrapper in wrappers)
                Check(Regex.Matches(ExtractMethod(runtime, wrapper.Key), @"\.Original\(").Count == wrapper.Value,
                    wrapper.Key + " wrapper changed its mutually exclusive Vanilla call paths");
            Check(Regex.Matches(ExtractMethod(runtime, "ReconcileEconomyAvailability"),
                @"initializeEconomyAvailabilityNative\(").Count == 1,
                "Re-Census no longer calls Vanilla exactly once per activation");
            string overlayHotPath = ExtractMethod(runtime, "TryApplyEconomyGridOverlay");
            Check(!Regex.IsMatch(overlayHotPath,
                @"\bnew\s+(?:byte|int|bool|HashSet|List|Dictionary|Stack)\b"),
                "normal overlay application allocates buffers or collections");
            string definitions = ExtractMethod(runtime, "ResolveAll");
            string[] approvedDefinitions =
            {
                "economy-oxen", "economy-quarry", "economy-wood", "farm-search",
                "resource-search", "wood-search", "nearby-search", "economy-grid-update",
                "initialize-economy-availability", "legacy-player-state-copy", "allocate",
                "active-layout-reference"
            };
            Check(approvedDefinitions.All(name => definitions.Contains("\"" + name + "\"")) &&
                Regex.Matches(definitions, @"Def\(").Count == approvedDefinitions.Length,
                "native signature set differs from the approved fix contracts");
        }

        private static void TestManifestContracts()
        {
            string root = ProjectRoot();
            string manifest = File.ReadAllText(Path.Combine(root, "info.json"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "PreplacedTestPlugin.cs"));
            Match minimum = Regex.Match(manifest, "\\\"MinimumScriptExtenderVersion\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            Match version = Regex.Match(manifest, "\\\"Version\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            Check(minimum.Success && plugin.Contains("[BepInDependency(ScriptExtenderGuid, \"" + minimum.Groups[1].Value + "\")]"),
                "manifest and BepIn minimum Script Extender versions differ");
            Check(version.Success && plugin.Contains("PluginVersion = \"" + version.Groups[1].Value + "\""),
                "plugin and manifest versions differ");
            Check(manifest.Contains("\"NetworkMode\": 1"), "NetworkMode is not synchronized");
            Check(plugin.Contains("BepInDependency(\"fixes\"") &&
                plugin.Contains("DependencyFlags.SoftDependency"), "Fixes load ordering is missing");
            Check(plugin.Contains("APIShared_Serp") && manifest.Contains("2.7.1"),
                "declared dependencies are incomplete");
        }

        private static void TestNativeContracts()
        {
            string runtime = File.ReadAllText(Path.Combine(ProjectRoot(), "src", "PreplacedTestRuntime.cs"));
            string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            Check(File.Exists(dll), "canonical native DLL missing");
            byte[] image = File.ReadAllBytes(dll);
            using (SHA256 sha = SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") == ExpectedNativeHash,
                    "canonical native hash changed");
            int hookOffset = RvaToFileOffset(image, 0x58057);
            byte[] expected = ParseBytes("48 63 C2 4C 69 C8 3C 58 00 00 B8 67 66 66 66");
            Check(image.Skip(hookOffset).Take(expected.Length).SequenceEqual(expected),
                "wood score hook bytes changed");
            Check(expected.Length == 15, "wood score hook span changed");
            Check(!HasRelativeBranchTargetInside(image, 0x58057, 0x58066),
                "native branch targets the inside of the wood hook span");

            var contracts = new Dictionary<string, int>
            {
                ["AllocateSpecPattern"] = 0x50680,
                ["ActiveLayoutReferencePattern"] = 0x55F64,
                ["EconomyOxenPattern"] = 0x50F90,
                ["EconomyQuarryPattern"] = 0x51270,
                ["EconomyWoodPattern"] = 0x51540,
                ["FarmSearchPattern"] = 0x575B0,
                ["ResourceSearchPattern"] = 0x57B80,
                ["WoodSearchPattern"] = 0x58020,
                ["NearbySearchPattern"] = 0x58950,
                ["EconomyGridUpdatePattern"] = 0x50720,
                ["InitializeEconomyAvailabilityPattern"] = 0x55FE0,
                ["LegacyPlayerStateCopyPattern"] = 0xD4290
            };
            foreach (KeyValuePair<string, int> contract in contracts)
            {
                Match declaration = Regex.Match(runtime,
                    "private const string\\s+" + Regex.Escape(contract.Key) +
                    "\\s*=\\s*\"([^\"]+)\";",
                    RegexOptions.Singleline);
                Check(declaration.Success, "native signature declaration missing: " + contract.Key);
                string[] tokens = declaration.Groups[1].Value.Split(new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
                byte?[] pattern = tokens.Select(token => token == "??"
                    ? (byte?)null : Convert.ToByte(token, 16)).ToArray();
                int expectedOffset = RvaToFileOffset(image, contract.Value);
                Check(PatternMatches(image, expectedOffset, pattern),
                    contract.Key + " no longer matches its reference RVA");
                Check(CountPatternMatches(image, pattern) == 1,
                    contract.Key + " is not unique in the canonical DLL");
            }
        }

        private static bool PatternMatches(byte[] image, int offset, byte?[] pattern)
        {
            if (offset < 0 || offset + pattern.Length > image.Length) return false;
            for (int index = 0; index < pattern.Length; index++)
                if (pattern[index].HasValue && image[offset + index] != pattern[index].Value)
                    return false;
            return true;
        }

        private static int CountPatternMatches(byte[] image, byte?[] pattern)
        {
            int count = 0;
            for (int offset = 0; offset <= image.Length - pattern.Length; offset++)
                if (PatternMatches(image, offset, pattern)) count++;
            return count;
        }

        private static bool HasRelativeBranchTargetInside(byte[] image, int startRva, int endRva)
        {
            int section = PeSectionOffset(image, ".text");
            int textRva = BitConverter.ToInt32(image, section + 12);
            int textSize = BitConverter.ToInt32(image, section + 8);
            int textOffset = BitConverter.ToInt32(image, section + 20);
            for (int offset = textOffset; offset < textOffset + textSize - 6; offset++)
            {
                int length;
                int displacement;
                byte opcode = image[offset];
                if (opcode == 0xE8 || opcode == 0xE9)
                {
                    length = 5;
                    displacement = BitConverter.ToInt32(image, offset + 1);
                }
                else if (opcode == 0x0F && image[offset + 1] >= 0x80 && image[offset + 1] <= 0x8F)
                {
                    length = 6;
                    displacement = BitConverter.ToInt32(image, offset + 2);
                }
                else if ((opcode >= 0x70 && opcode <= 0x7F) || opcode == 0xEB)
                {
                    length = 2;
                    displacement = (sbyte)image[offset + 1];
                }
                else continue;
                int sourceRva = textRva + offset - textOffset;
                int target = sourceRva + length + displacement;
                if (target > startRva && target < endRva) return true;
            }
            return false;
        }

        private static int RvaToFileOffset(byte[] image, int rva)
        {
            int pe = BitConverter.ToInt32(image, 0x3C);
            int sections = BitConverter.ToInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToInt16(image, pe + 20);
            for (int index = 0; index < sections; index++)
            {
                int section = table + index * 40;
                int virtualSize = BitConverter.ToInt32(image, section + 8);
                int virtualAddress = BitConverter.ToInt32(image, section + 12);
                int rawSize = BitConverter.ToInt32(image, section + 16);
                int rawAddress = BitConverter.ToInt32(image, section + 20);
                if (rva >= virtualAddress && rva < virtualAddress + Math.Max(virtualSize, rawSize))
                    return rawAddress + rva - virtualAddress;
            }
            throw new InvalidOperationException("RVA outside file-backed PE sections: 0x" + rva.ToString("X"));
        }

        private static int PeSectionOffset(byte[] image, string name)
        {
            int pe = BitConverter.ToInt32(image, 0x3C);
            int sections = BitConverter.ToInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToInt16(image, pe + 20);
            for (int index = 0; index < sections; index++)
            {
                int section = table + index * 40;
                string actual = System.Text.Encoding.ASCII.GetString(image, section, 8).TrimEnd('\0');
                if (actual == name) return section;
            }
            throw new InvalidOperationException("PE section missing: " + name);
        }

        private static byte[] ParseBytes(string value) =>
            value.Split(' ').Select(item => Convert.ToByte(item, 16)).ToArray();

        private static string ExtractMethod(string source, string methodName)
        {
            Match declaration = Regex.Match(source,
                @"private[^\x0D\x0A]+\s+" + Regex.Escape(methodName) + @"\s*\(");
            if (!declaration.Success) throw new InvalidOperationException("method missing: " + methodName);
            int open = source.IndexOf('{', declaration.Index + declaration.Length);
            if (open < 0) throw new InvalidOperationException("method body missing: " + methodName);
            int depth = 0;
            for (int index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(open, index - open + 1);
            }
            throw new InvalidOperationException("unterminated method: " + methodName);
        }

        private static string ProjectRoot() => Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", ".."));

        private static void Check(bool condition, string message)
        {
            checks++;
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
