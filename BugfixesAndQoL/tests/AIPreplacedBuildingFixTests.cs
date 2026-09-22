using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace BugfixesAndQoL
{
    internal static class AIPreplacedBuildingFixTests
    {
        public static void Run(Action<bool, string> check)
        {
            string eligible = LegacyTimerFixEligibility.Classify(true, false, 0xD4, 0xD5, 1, 1, 0, 1);
            check(LegacyTimerFixEligibility.IsEligible(eligible), "preplaced AI ruin legacy transfer is eligible");
            check(!LegacyTimerFixEligibility.IsEligible(
                LegacyTimerFixEligibility.Classify(true, true, 0xD4, 0xD5, 1, 1, 0, 1)),
                "preplaced AI ruin fix rejects saves");
            check(!LegacyTimerFixEligibility.IsEligible(
                LegacyTimerFixEligibility.Classify(true, false, 0xD4, 0xD5, 0, 0, 0, 0)),
                "preplaced AI ruin fix rejects a missing serialized activation");
            check(LegacyTimerFixEligibility.IsApplicationEligible(
                LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, false, 1)),
                "preplaced AI ruin fix accepts a stable matching tower identity");
            check(!LegacyTimerFixEligibility.IsApplicationEligible(
                LegacyTimerFixEligibility.ClassifyAtApplication(eligible, true, true, 1)),
                "preplaced AI ruin fix preserves later real damage activations");

            check(PortalOwnerSynchronizationModel.IsEligible(false, true, true, true, true, true,
                true, true, true, true, true), "validated preplaced portal is synchronized");
            check(!PortalOwnerSynchronizationModel.IsEligible(true, true, true, true, true, true,
                true, true, true, true, true), "save portal is not synchronized");
            check(!PortalOwnerSynchronizationModel.IsEligible(false, false, true, true, true, true,
                true, true, true, true, true), "later-built portal is not synchronized");
            check(!PortalOwnerSynchronizationModel.IsEligible(false, true, true, false, true, true,
                true, true, true, true, true) &&
                !PortalOwnerSynchronizationModel.IsEligible(false, true, false, true, true, true,
                    true, true, true, true, true),
                "destroyed or ambiguous portal is not synchronized");
            check(EconomyFixActivationModel.Initial(false, WallAccessRole.GatedWallCandidate, true) ==
                EconomyFixActivationState.PendingPortal, "friendly portal waits for native route readiness");
            check(EconomyFixActivationModel.Initial(false, WallAccessRole.ClosedWallCandidate, false) ==
                EconomyFixActivationState.PendingBreach, "closed enclosure remains pending until a breach");
            check(WallBreachConfirmation.IsConfirmed(true, 4, 5, 7, 7),
                "lost wall tile plus connected stable anchors confirms a breach");
            check(!WallBreachConfirmation.IsConfirmed(false, 4, 5, 7, 7) &&
                !WallBreachConfirmation.IsConfirmed(true, 4, 5, 7, 8) &&
                !WallBreachConfirmation.IsConfirmed(true, 4, 4, 7, 7),
                "damage, a closed wall, and pure PCL relabeling do not confirm a breach");

            const int nativePackedTileCapacity = 320800;
            const int wallFlag = 0x10000000;
            var logicGrid = new int[nativePackedTileCapacity];
            logicGrid[0] = wallFlag;
            logicGrid[nativePackedTileCapacity - 1] = wallFlag | 7;
            var collectedWallTiles = new HashSet<int>();
            WallTileBaselineCollector.Collect(logicGrid, wallFlag, collectedWallTiles);
            check(collectedWallTiles.SetEquals(new[] { 0, nativePackedTileCapacity - 1 }) &&
                collectedWallTiles.All(tileId => (uint)tileId < nativePackedTileCapacity),
                "pre-AIV wall capture includes boundary slots and cannot emit an invalid packed tile ID");

            check(EconomyGridOverlayProjection.ProjectOutsideCount(
                new[] { 1, 1, 2, 0, 3 }, new HashSet<int> { 1, 3 }) == 2,
                "player-specific economy PCL projection is exact");
            byte[] grid = { 3, 4, 5, 6 };
            byte[] original = (byte[])grid.Clone();
            int[] changed = { 0, 2 };
            byte[] values = { grid[0], grid[2] };
            try
            {
                grid[0] = 0;
                grid[2] = 1;
                throw new InvalidOperationException("restoration test");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                for (int index = 0; index < changed.Length; index++) grid[changed[index]] = values[index];
            }
            check(EconomyGridOverlayProjection.RestoredExactly(original, grid),
                "economy overlay is byte-exact after an exception");

            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            string runtime = File.ReadAllText(Path.Combine(root, "src", "AIPreplacedBuildingFixRuntime.cs"));
            string plugin = File.ReadAllText(Path.Combine(root, "src", "BugfixesAndQoLPlugin.cs"));
            string viewModel = File.ReadAllText(Path.Combine(root, "src", "BugfixesAndQoLViewModel.cs"));
            string processMapLoad = ExtractMethod(runtime, "ProcessMapLoad");
            string processMapStart = ExtractMethod(runtime, "ProcessMapStart");
            string allocateSpec = ExtractMethod(runtime, "AllocateSpec");
            string captureBaseline = ExtractMethod(runtime, "CaptureMapLoadBuildingIdentities");
            string processDamage = ExtractMethod(runtime, "ProcessBuildingDamage");
            check(processMapLoad.Contains("MissionInitializationPhase.BeforeLoad") &&
                !processMapLoad.Contains("CaptureMapLoadBuildingIdentities") &&
                !processMapLoad.Contains("LogInfo"),
                "BeforeLoad resets without capturing or logging the pre-AIV baseline");
            check(allocateSpec.IndexOf("CaptureMapLoadBuildingIdentities", StringComparison.Ordinal) >= 0 &&
                allocateSpec.IndexOf("CaptureMapLoadBuildingIdentities", StringComparison.Ordinal) <
                allocateSpec.LastIndexOf("allocateHook.Original", StringComparison.Ordinal),
                "0x50680 captures the authoritative baseline before its single Vanilla call");
            check(captureBaseline.Contains("preAivBaselineCaptured || preAivBaselineCaptureClosed") &&
                processMapStart.Contains("preAivBaselineCaptureClosed = true") &&
                processMapStart.Contains("PREPLACED_BASELINE_MISSING"),
                "the pre-AIV capture is one-shot, closes at AfterNativeStart, and fails closed when missing");
            check(captureBaseline.Contains("WallTileBaselineCollector.Collect(") &&
                !captureBaseline.Contains("GetTileId(") &&
                !captureBaseline.Contains("LogicGrid[tileId]"),
                "the pre-AIV wall capture iterates only the bounded packed grid without early coordinate lookup");
            check(Regex.Matches(runtime, @"transaction\.AddDetour\(").Count == 10 &&
                Regex.Matches(runtime, @"transaction\.AddContextHook\(").Count == 1,
                "AI preplaced-building fix has exactly ten detours and one context hook");
            var wrappers = new Dictionary<string, int>
            {
                ["FarmSearch"] = 2,
                ["ResourceSearch"] = 2,
                ["WoodSearch"] = 2,
                ["NearbySearch"] = 3
            };
            foreach (KeyValuePair<string, int> wrapper in wrappers)
                check(Regex.Matches(ExtractMethod(runtime, wrapper.Key), @"\.Original\(").Count == wrapper.Value,
                    wrapper.Key + " retains mutually exclusive paths with one Vanilla call each");
            check(Regex.Matches(ExtractMethod(runtime, "ReconcileEconomyAvailability"),
                @"initializeEconomyAvailabilityNative\(").Count == 1,
                "AI economy Re-Census invokes Vanilla exactly once per activation");
            check(!Regex.IsMatch(runtime,
                @"\b(?:Update|LateUpdate|FixedUpdate|OnDestroy|OnDisable|OnApplicationQuit)\s*\("),
                "AI preplaced-building fix has no polling or Unity teardown method");
            check(!runtime.Contains("PREPLACED_MAP_LOAD") &&
                !runtime.Contains("PREPLACED_MAP_START") &&
                !runtime.Contains("PREPLACED_SESSION_RESET") &&
                !runtime.Contains("PREPLACED_PRE_AIV_BASELINE") &&
                !runtime.Contains("PREPLACED_ECONOMY_PROFILE") &&
                !runtime.Contains("PREPLACED_ECONOMY_FIX_PENDING") &&
                !runtime.Contains("PREPLACED_ECONOMY_FIX_WAITING_FOR_ROUTE") &&
                !runtime.Contains("PREPLACED_ECONOMY_CENSUS_RECONCILED") &&
                !runtime.Contains("PREPLACED_PORTAL_OWNER_SYNC"),
                "AI preplaced-building fix omits routine lifecycle and diagnostic logging");
            check(!runtime.Contains("CaptureEconomyAvailabilityFields") &&
                !runtime.Contains("ComputeEconomyAccessSignature") &&
                !runtime.Contains("EmitEconomyFixState"),
                "logging cannot trigger census snapshots, portal scans, or route-cache construction");
            check(processDamage.Contains("CaptureDamageContext(args, economyProfileResolved)") &&
                processDamage.Contains("pendingDamage.Push(null)") &&
                processDamage.Contains("if (completed == null) return"),
                "ordinary combat damage uses an allocation-free balanced event sentinel after profile resolution");
            string wallBaseline = ExtractMethod(runtime, "CaptureWallBaseline");
            check(wallBaseline.Contains("foreach (int tileId in mapLoadWallTiles)") &&
                !wallBaseline.Contains("for (int x = 0; x < NativeTileGridWidth"),
                "per-player wall baselines iterate only the captured wall tiles");
            check(!runtime.Contains("System.Text.Json") && !runtime.Contains("Newtonsoft") &&
                !runtime.Contains("JavaScriptSerializer") && !runtime.Contains("JsonUtility"),
                "AI preplaced-building fix has no runtime JSON parser");
            check(runtime.Contains("finally") && runtime.Contains("RestoreEconomyGridOverlay") &&
                runtime.Contains("IsExpectedAivState"),
                "AI preplaced-building fix restores overlays and validates the public AIV pointer");
            string economyStart = ExtractMethod(runtime, "TryGetNativeEconomyStart");
            string runtimeField = ExtractMethod(runtime, "TryGetPlayerRuntimeFieldPointer");
            check(runtime.Contains("ActivePlayerRuntimeStateBaseRva = 0x379D0CC") &&
                runtime.Contains("NativeEconomyStartXRva = 0x379AFA8") &&
                runtime.Contains("NativeEconomyStartYRva = 0x379AFAC") &&
                economyStart.Contains("nativeModuleBase") &&
                economyStart.Contains("playerId * PlayerRuntimeStateStride") &&
                !economyStart.Contains("TryGetPlayerResourcesById") &&
                runtimeField.Contains("activeLayoutIndexBase") &&
                !runtimeField.Contains("TryGetPlayerResourcesById"),
                "AI runtime timer, census, and economy-start fields use Vanilla's separate active-player state");
            check(runtime.Contains("Registers = X64SmartCPUContextRegs.All") &&
                runtime.Contains("Placement = OverwrittenInstructionPlacement.BeforeCallback") &&
                runtime.Contains("DisplacedByteCount != WoodScoreFloorHookLength"),
                "AI wood-score hook preserves the audited registers and exact displaced span");
            check(viewModel.Contains("[SyncHostOnly]") &&
                viewModel.Contains("FixAIPreplacedMapBuildings") &&
                viewModel.Contains("fixAIPreplacedMapBuildings = true"),
                "AI preplaced-building host setting is synchronized and enabled by default");
            check(plugin.Contains("BepInIncompatibility(PreplacedTestGuid)") &&
                plugin.Contains("BepInIncompatibility(EnemyGatePathfindingTestGuid)"),
                "overlapping AI preplaced-building test mods are incompatible");
            TestNativeContracts(check, runtime);
        }

        private static void TestNativeContracts(Action<bool, string> check, string runtime)
        {
            const string expectedHash =
                "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
            const int hookRva = 0x58057;
            const int returnRva = 0x58066;
            string dll = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll";
            byte[] image = File.ReadAllBytes(dll);
            using (SHA256 sha = SHA256.Create())
                check(BitConverter.ToString(sha.ComputeHash(image)).Replace("-", "") == expectedHash,
                    "AI preplaced-building native hash is current");
            byte[] expected =
            {
                0x48, 0x63, 0xC2, 0x4C, 0x69, 0xC8, 0x3C, 0x58,
                0x00, 0x00, 0xB8, 0x67, 0x66, 0x66, 0x66
            };
            int offset = RvaToFileOffset(image, hookRva);
            check(image.Skip(offset).Take(expected.Length).SequenceEqual(expected) &&
                expected.Length == returnRva - hookRva,
                "AI wood-score hook has the audited exact 15-byte span");
            check(!HasRelativeBranchTargetInside(image, hookRva, returnRva),
                "AI wood-score hook span has no incoming relative branch target");
            const int activeLayoutReferenceRva = 0x55F64;
            int activeLayoutReferenceOffset = RvaToFileOffset(image, activeLayoutReferenceRva);
            int activeLayoutTarget = activeLayoutReferenceRva + 10 +
                BitConverter.ToInt32(image, activeLayoutReferenceOffset + 6);
            check(activeLayoutTarget == 0x379D0CC,
                "AI active-player runtime-state RIP reference resolves to the audited Vanilla table");

            var contracts = new Dictionary<string, int>
            {
                ["AllocateSpecPattern"] = 0x50680,
                ["ActiveLayoutReferencePattern"] = activeLayoutReferenceRva,
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
                    "\\s*=\\s*\"([^\"]+)\";", RegexOptions.Singleline);
                check(declaration.Success, "AI native signature exists: " + contract.Key);
                byte?[] pattern = declaration.Groups[1].Value
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(token => token == "??" ? (byte?)null : Convert.ToByte(token, 16))
                    .ToArray();
                check(PatternMatches(image, RvaToFileOffset(image, contract.Value), pattern),
                    "AI native signature matches reference RVA: " + contract.Key);
                check(CountPatternMatches(image, pattern) == 1,
                    "AI native signature is unique: " + contract.Key);
            }
        }

        private static bool PatternMatches(byte[] image, int offset, byte?[] pattern)
        {
            if (offset < 0 || offset + pattern.Length > image.Length) return false;
            for (int index = 0; index < pattern.Length; index++)
                if (pattern[index].HasValue && image[offset + index] != pattern[index].Value) return false;
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
                    displacement = unchecked((sbyte)image[offset + 1]);
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
            throw new InvalidOperationException("RVA is not file-backed: 0x" + rva.ToString("X"));
        }

        private static int PeSectionOffset(byte[] image, string name)
        {
            int pe = BitConverter.ToInt32(image, 0x3C);
            int sections = BitConverter.ToInt16(image, pe + 6);
            int table = pe + 24 + BitConverter.ToInt16(image, pe + 20);
            for (int index = 0; index < sections; index++)
            {
                int section = table + index * 40;
                if (System.Text.Encoding.ASCII.GetString(image, section, 8).TrimEnd('\0') == name)
                    return section;
            }
            throw new InvalidOperationException("PE section missing: " + name);
        }

        private static string ExtractMethod(string source, string methodName)
        {
            Match declaration = Regex.Match(source,
                @"(?m)^[ \t]*private(?:[ \t]+static)?(?:[ \t]+unsafe)?[ \t]+" +
                @"[^\x0D\x0A(]+?[ \t]+" + Regex.Escape(methodName) + @"[ \t]*\(");
            if (!declaration.Success) throw new InvalidOperationException("Method missing: " + methodName);
            int open = source.IndexOf('{', declaration.Index + declaration.Length);
            int depth = 0;
            for (int index = open; index >= 0 && index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(open, index - open + 1);
            }
            throw new InvalidOperationException("Method body is incomplete: " + methodName);
        }
    }
}
