using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace BugfixesAndQoL
{
    internal static class Program
    {
        private const string ExpectedHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int FindRva = ImprovedMoatFillingNativeContractPolicy.FindMoatWorkTargetRva;
        private const int ResolveRva = ImprovedMoatFillingNativeContractPolicy.ResolveMoatWorkTileRva;
        private const int DispatcherRva = 0x13F540;
        private const int DispatcherSize = 10069;
        private const int PlannerRva = ImprovedMoatFillingNativeContractPolicy.MovementPlannerRva;

        private static int failures;

        private static int Main()
        {
            TestFreeApproachSelection();
            TestReservationAndRollback();
            TestContextIsolation();
            TestHookOwnership();
            TestLiveEntryOwnership();
            TestAiDefensePatrolPolicy();
            TestAiDefensePatrolIntegration();
            TestNativeContracts();
            if (failures == 0)
            {
                Console.WriteLine("BugfixesAndQoL policy and native-contract tests passed.");
                return 0;
            }
            Console.Error.WriteLine($"BugfixesAndQoL policy and native-contract tests failed: {failures}.");
            return 1;
        }

        private static void TestFreeApproachSelection()
        {
            MoatApproachCandidate[] candidates = RejectedCandidates();
            candidates[0] = Candidate(0, 10, 9, false); // Occupied in the runtime policy.
            candidates[1] = Candidate(1, 11, 9, true);
            candidates[2] = Candidate(2, 11, 10, true);
            Check(ImprovedMoatFillingPolicy.TryChoose(
                    candidates, 10, 8, out MoatApproachCandidate selected) && selected.Order == 1,
                "occupied nearest candidate falls through to the next free neighbour");

            candidates = RejectedCandidates();
            candidates[0] = Candidate(0, 9, 10, true);
            candidates[1] = Candidate(1, 11, 10, true);
            Check(ImprovedMoatFillingPolicy.TryChoose(candidates, 10, 10, out selected) &&
                    selected.Order == 0,
                "distance ties preserve native neighbour order");

            Check(!ImprovedMoatFillingPolicy.TryChoose(
                    RejectedCandidates(), 10, 10, out _),
                "fully unsuitable moat has no approach so the next Vanilla moat can be queried");
            Check(ImprovedMoatFillingPolicy.IsSameNativeRegion(0, 0) &&
                    !ImprovedMoatFillingPolicy.IsSameNativeRegion(0, 7),
                "region zero follows exact Vanilla region equality");
            Check(ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(0x10) &&
                    ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(0x20) &&
                    ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(0x100) &&
                    ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(0x10000000) &&
                    !ImprovedMoatFillingPolicy.HasDownstreamMovementBlockingFlags(0x8000),
                "movement flag policy matches the downstream planner gates");
            Check(ImprovedMoatFillingPolicy.IsCompletedMoat(0x40000000),
                "completed hostile moat cannot be used as a standalone standing tile");
        }

        private static void TestReservationAndRollback()
        {
            Check(ImprovedMoatFillingPolicy.TryUndoVanillaReservation(20, out byte zero) && zero == 0,
                "reservation increment is undone symmetrically");
            Check(ImprovedMoatFillingPolicy.TryUndoVanillaReservation(100, out byte eighty) && eighty == 80,
                "nonzero reservation is restored symmetrically");
            Check(!ImprovedMoatFillingPolicy.TryUndoVanillaReservation(19, out byte unchanged) && unchanged == 19,
                "reservation underflow fails closed");

            byte[] records = { 40, 60 };
            byte original = byte.MaxValue;
            try
            {
                ImprovedMoatFillingPolicy.TryUndoVanillaReservation(records[0], out original);
                records[0] = ImprovedMoatFillingPolicy.TemporarilyExcludedReservation;
                throw new InvalidOperationException("rollback probe");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                records[0] = original;
            }
            Check(records[0] == 20 && records[1] == 60,
                "temporary exclusion rolls back after exceptions");
        }

        private static void TestContextIsolation()
        {
            Check(ImprovedMoatFillingPolicy.ShouldInspectSelection(true, 2),
                "host-enabled hostile fill is inspected");
            Check(!ImprovedMoatFillingPolicy.ShouldInspectSelection(false, 2) &&
                    !ImprovedMoatFillingPolicy.ShouldInspectSelection(true, 1) &&
                    !ImprovedMoatFillingPolicy.ShouldInspectSelection(true, 2, false),
                "disabled setting, excavation and unsupported units remain Vanilla");
            Check(ImprovedMoatFillingPolicy.ShouldReplaceResolverResult(2, true) &&
                    !ImprovedMoatFillingPolicy.ShouldReplaceResolverResult(1, true) &&
                    !ImprovedMoatFillingPolicy.ShouldReplaceResolverResult(2, false),
                "only the correlated mode-2 resolver result is replaced");
        }

        private static void TestHookOwnership()
        {
            Check(MoatFillHookOwnershipPolicy.Resolve(false, false, 0) == MoatFillHookOwner.Standalone,
                "BugfixesAndQoL alone owns the hooks");
            Check(MoatFillHookOwnershipPolicy.Resolve(true, true, 1) == MoatFillHookOwner.MoveMoat,
                "MoveMoat owns shared hooks when its bridge is ready");
            Check(MoatFillHookOwnershipPolicy.Resolve(true, true, 0) == MoatFillHookOwner.Standalone,
                "BugfixesAndQoL safely takes over when MoveMoat reports hook failure");
            Check(MoatFillHookOwnershipPolicy.Resolve(true, false, 0) == MoatFillHookOwner.Conflict &&
                    MoatFillHookOwnershipPolicy.Resolve(true, true, 7) == MoatFillHookOwner.Conflict,
                "missing or unknown MoveMoat bridge fails closed");
        }

        private static void TestLiveEntryOwnership()
        {
            Check(ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(FindRva) &&
                    ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(ResolveRva),
                "owned selector and resolver hooks require pristine live entries");
            Check(!ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(PlannerRva) &&
                    !ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(
                        ImprovedMoatFillingNativeContractPolicy.MovementPlannerLowFlagGateRva) &&
                    !ImprovedMoatFillingNativeContractPolicy.RequiresPristineLiveBytes(
                        ImprovedMoatFillingNativeContractPolicy.MovementPlannerStructureFlagGateRva),
                "downstream planner and gates may already contain compatible live hooks");
        }

        private static void TestAiDefensePatrolPolicy()
        {
            Check(AiDefensePatrolPolicy.NeedsCastleDefender(19, 20),
                "AI defense patrol detects wall-defense underfill");
            Check(!AiDefensePatrolPolicy.NeedsCastleDefender(20, 20) &&
                    !AiDefensePatrolPolicy.NeedsCastleDefender(30, 20),
                "AI defense patrol preserves patrol assignment after the wall quota is met");
            Check(!AiDefensePatrolPolicy.NeedsCastleDefender(0, 0),
                "AI defense patrol handles zero DefWalls and DefTotal");
            Check(AiDefensePatrolPolicy.NeedsCastleDefender(0, int.MaxValue) &&
                    !AiDefensePatrolPolicy.NeedsCastleDefender(int.MaxValue, int.MaxValue),
                "AI defense patrol handles integer boundary quotas");
            Check(AiDefensePatrolPolicy.SelectComparisonValue(true) == unchecked((uint)int.MaxValue) &&
                    AiDefensePatrolPolicy.SelectComparisonValue(false) == unchecked((uint)int.MinValue),
                "AI defense patrol emits signed-jl comparison sentinels");
        }

        private static void TestAiDefensePatrolIntegration()
        {
            string projectDirectory = FindProjectDirectory();
            string runtime = File.ReadAllText(Path.Combine(projectDirectory, "src", "AiDefensePatrolFix.cs"));
            string orchestrator = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLRuntime.cs"));
            string viewModel = File.ReadAllText(Path.Combine(projectDirectory, "src", "BugfixesAndQoLViewModel.cs"));
            string xaml = File.ReadAllText(Path.Combine(
                projectDirectory,
                "Override",
                "ScriptExtenderUI",
                "BugfixesAndQoLSettings.xaml"));

            Check(runtime.Contains("OverwrittenInstructionPlacement.BeforeCallback") &&
                    runtime.Contains("X64SmartCPUContextRegs.All") &&
                    runtime.Contains("BugfixesHookInfrastructure.CreateOwnedTransaction") &&
                    runtime.Contains("settings.EnableMod") &&
                    runtime.Contains("settings.EnableAiFixes") &&
                    runtime.Contains("settings.EnableAiDefensePatrolFix"),
                "AI defense patrol runtime uses the owned before-callback hook and all setting gates");
            Check(runtime.Contains("registers->RAX = originalRax") &&
                    runtime.Contains("TryGetUnitById(unitId") &&
                    runtime.Contains("for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)"),
                "AI defense patrol retains Vanilla fallback and explicit ID/index contracts");
            Check(orchestrator.Contains("EnsureAiDefensePatrolFix") &&
                    orchestrator.Contains("aiDefensePatrolFix?.ApplySetting()") &&
                    orchestrator.Contains("aiDefensePatrolFix?.Dispose()"),
                "AI defense patrol participates in native initialization, setting reconciliation and final disposal");
            Check(viewModel.Contains("private bool enableAiDefensePatrolFix = true;") &&
                    viewModel.Contains("public bool EnableAiDefensePatrolFix") &&
                    viewModel.Contains("EnableAiDefensePatrolFix = true;"),
                "AI defense patrol host setting defaults and resets to enabled");
            Check(xaml.Contains("bugfixes.enable-ai-defense-patrol-fix") &&
                    xaml.Contains("IsChecked=\"{Binding EnableAiDefensePatrolFix, Mode=TwoWay}\""),
                "AI defense patrol setting is searchable and bound in XAML");
        }

        private static void TestNativeContracts()
        {
            string root = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string path = Path.Combine(root,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            Check(File.Exists(path), "canonical native DLL exists");
            if (!File.Exists(path))
                return;
            byte[] file = File.ReadAllBytes(path);
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty);
                Check(string.Equals(hash, ExpectedHash, StringComparison.OrdinalIgnoreCase),
                    "canonical native SHA-256 matches the audited baseline");
                Check(string.Equals(Shared.DebugLogHelper.CurrentNativeSha256, ExpectedHash,
                        StringComparison.OrdinalIgnoreCase),
                    "shared native SHA-256 matches the AI defense patrol baseline");
            }
            var image = new PeImage(file);
            try
            {
                AiDefensePatrolNativeDefinition.ValidateManagedLayout();
                AiDefensePatrolNativeDefinition.Validate(MapPeImage(file));
                Check(true,
                    "AI defense patrol GameUnit layout, unique signature, instruction span, branches and call targets");
            }
            catch (Exception exception)
            {
                Check(false, "AI defense patrol native contract: " + exception.Message);
            }
            CheckBytes(image, FindRva, new byte[] { 0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x48, 0x83, 0xEC, 0x68,
                0x48, 0x8B, 0xE9 }, "selector entry bytes");
            CheckBytes(image, ResolveRva, new byte[] { 0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44, 0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49,
                0x63, 0xD9, 0x4C, 0x63, 0xDA }, "resolver entry bytes");
            CheckBytes(image, PlannerRva, new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x20, 0x55, 0x56, 0x57,
                0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57, 0x48, 0x83, 0xEC, 0x30, 0x48, 0x63,
                0xF2 }, "movement planner entry bytes");
            CheckBytes(image, ImprovedMoatFillingNativeContractPolicy.MovementPlannerLowFlagGateRva,
                new byte[] { 0xF6, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x30 },
                "movement low-flag gate bytes");
            CheckBytes(image, ImprovedMoatFillingNativeContractPolicy.MovementPlannerStructureFlagGateRva,
                new byte[] { 0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04,
                0x00, 0x01, 0x00, 0x10 }, "movement structure-flag gate bytes");
            Check(image.CountNearCalls(DispatcherRva, DispatcherSize, FindRva) >= 2 &&
                    image.CountNearCalls(DispatcherRva, DispatcherSize, ResolveRva) >= 3 &&
                    image.CountNearCalls(DispatcherRva, DispatcherSize, PlannerRva) >= 1,
                "dispatcher contains initial and follow-up moat-work call chain");
        }

        private static byte[] MapPeImage(byte[] file)
        {
            int peOffset = ReadInt32(file, 0x3C);
            if (ReadInt32(file, peOffset) != 0x00004550)
                throw new InvalidDataException("Invalid PE signature.");

            int sectionCount = ReadUInt16(file, peOffset + 6);
            int optionalHeaderSize = ReadUInt16(file, peOffset + 20);
            int optionalHeader = peOffset + 24;
            int sizeOfImage = ReadInt32(file, optionalHeader + 56);
            int sizeOfHeaders = ReadInt32(file, optionalHeader + 60);
            byte[] image = new byte[sizeOfImage];
            Buffer.BlockCopy(file, 0, image, 0, Math.Min(sizeOfHeaders, file.Length));

            int sectionTable = optionalHeader + optionalHeaderSize;
            for (int index = 0; index < sectionCount; index++)
            {
                int section = sectionTable + index * 40;
                int virtualAddress = ReadInt32(file, section + 12);
                int rawSize = ReadInt32(file, section + 16);
                int rawAddress = ReadInt32(file, section + 20);
                if (rawSize <= 0)
                    continue;
                if (rawAddress < 0 || rawAddress > file.Length - rawSize ||
                    virtualAddress < 0 || virtualAddress > image.Length - rawSize)
                {
                    throw new InvalidDataException("PE section lies outside the file or virtual image.");
                }

                Buffer.BlockCopy(file, rawAddress, image, virtualAddress, rawSize);
            }

            return image;
        }

        private static string FindProjectDirectory()
        {
            DirectoryInfo directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "BugfixesAndQoL.csproj")))
                    return directory.FullName;
                directory = directory.Parent;
            }

            throw new DirectoryNotFoundException("BugfixesAndQoL project directory was not found.");
        }

        private static int ReadUInt16(byte[] bytes, int offset) =>
            bytes[offset] | bytes[offset + 1] << 8;

        private static int ReadInt32(byte[] bytes, int offset) =>
            bytes[offset] |
            bytes[offset + 1] << 8 |
            bytes[offset + 2] << 16 |
            bytes[offset + 3] << 24;

        private static MoatApproachCandidate[] RejectedCandidates()
        {
            var candidates = new MoatApproachCandidate[8];
            for (int i = 0; i < candidates.Length; i++)
                candidates[i] = Candidate(i, 0, 0, false);
            return candidates;
        }

        private static MoatApproachCandidate Candidate(int order, int x, int y, bool eligible) =>
            new MoatApproachCandidate(order, x, y, order, eligible);

        private static void CheckBytes(PeImage image, int rva, byte[] expected, string name)
        {
            byte[] actual = image.ReadRva(rva, expected.Length);
            bool equal = actual.Length == expected.Length;
            for (int i = 0; equal && i < expected.Length; i++)
                equal = actual[i] == expected[i];
            Check(equal, name);
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
                Console.WriteLine("PASS " + name);
            else
            {
                failures++;
                Console.Error.WriteLine("FAIL " + name);
            }
        }

        private sealed class PeImage
        {
            private readonly byte[] file;
            private readonly List<Section> sections = new List<Section>();

            internal PeImage(byte[] file)
            {
                this.file = file;
                int pe = ReadInt32(0x3C);
                int count = ReadUInt16(pe + 6);
                int table = pe + 24 + ReadUInt16(pe + 20);
                for (int i = 0; i < count; i++)
                {
                    int entry = table + i * 40;
                    sections.Add(new Section(ReadInt32(entry + 12),
                        Math.Max(ReadInt32(entry + 8), ReadInt32(entry + 16)), ReadInt32(entry + 20)));
                }
            }

            internal byte[] ReadRva(int rva, int length)
            {
                int offset = RvaToOffset(rva);
                var result = new byte[length];
                Buffer.BlockCopy(file, offset, result, 0, length);
                return result;
            }

            internal int CountNearCalls(int startRva, int length, int targetRva)
            {
                byte[] bytes = ReadRva(startRva, length);
                int count = 0;
                for (int i = 0; i <= bytes.Length - 5; i++)
                {
                    if (bytes[i] != 0xE8)
                        continue;
                    int displacement = bytes[i + 1] | bytes[i + 2] << 8 |
                        bytes[i + 3] << 16 | bytes[i + 4] << 24;
                    if (startRva + i + 5 + displacement == targetRva)
                        count++;
                }
                return count;
            }

            private int RvaToOffset(int rva)
            {
                foreach (Section section in sections)
                {
                    if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.Size)
                        return checked(section.RawOffset + rva - section.VirtualAddress);
                }
                throw new InvalidOperationException($"RVA 0x{rva:X} is outside PE sections.");
            }

            private int ReadUInt16(int offset) => file[offset] | file[offset + 1] << 8;
            private int ReadInt32(int offset) => file[offset] | file[offset + 1] << 8 |
                file[offset + 2] << 16 | file[offset + 3] << 24;

            private readonly struct Section
            {
                internal Section(int virtualAddress, int size, int rawOffset)
                {
                    VirtualAddress = virtualAddress;
                    Size = size;
                    RawOffset = rawOffset;
                }
                internal int VirtualAddress { get; }
                internal int Size { get; }
                internal int RawOffset { get; }
            }
        }
    }
}
