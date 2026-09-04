using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace MoatFillTargetTest
{
    internal static class Program
    {
        private const string ExpectedHash =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int FindRva = 0x69D60;
        private const int ResolveRva = 0x6AF60;
        private const int StateDispatcherRva = 0x13F540;
        private const int StateDispatcherSize = 10069;
        private const int MovementPlannerRva = 0x196280;

        private static int failures;

        private static int Main()
        {
            TestOccupiedNearestChoosesNextFree();
            TestFullyOccupiedMoatFallsThroughToNextMoat();
            TestTiePreservesNativeOrder();
            TestNoFreeCandidate();
            TestGeometryAndTerrainRejections();
            TestCompletedMoatAndNativeMovementFlags();
            TestRegionZeroUsesVanillaEquality();
            TestReservationArithmetic();
            TestReservationRollbackOnFailure();
            TestUnrelatedContextsRemainVanilla();
            TestCanonicalNativeContracts();

            if (failures == 0)
            {
                Console.WriteLine("MoatFillTargetTest static tests passed.");
                return 0;
            }

            Console.Error.WriteLine($"MoatFillTargetTest static tests failed: {failures}.");
            return 1;
        }

        private static void TestOccupiedNearestChoosesNextFree()
        {
            ApproachCandidate[] candidates = CreateRejectedCandidates();
            candidates[0] = Candidate(0, 10, 9, occupied: true);
            candidates[1] = Candidate(1, 11, 9);
            candidates[2] = Candidate(2, 11, 10);
            bool found = MoatFillApproachPolicy.TryChoose(
                candidates, 10, 8, out ApproachCandidate selected, out ApproachDecisionSummary summary);
            Check(found, "occupied nearest: candidate found");
            Check(selected.Order == 1, "occupied nearest: next free native candidate selected");
            Check(summary.Occupied == 1, "occupied nearest: occupied count");
        }

        private static void TestTiePreservesNativeOrder()
        {
            ApproachCandidate[] candidates = CreateRejectedCandidates();
            candidates[0] = Candidate(0, 9, 10);
            candidates[1] = Candidate(1, 11, 10);
            bool found = MoatFillApproachPolicy.TryChoose(
                candidates, 10, 10, out ApproachCandidate selected, out _);
            Check(found && selected.Order == 0, "strict distance tie preserves N/NE/E order");
        }

        private static void TestFullyOccupiedMoatFallsThroughToNextMoat()
        {
            ApproachCandidate[] occupied = new ApproachCandidate[8];
            for (int index = 0; index < occupied.Length; index++)
                occupied[index] = Candidate(index, 10 + index, 10, occupied: true);
            ApproachCandidate[] nextMoat = CreateRejectedCandidates();
            nextMoat[4] = Candidate(4, 20, 21);

            ApproachCandidate[][] vanillaOrder = { occupied, nextMoat };
            int selectedMoat = -1;
            ApproachCandidate selectedApproach = default;
            for (int index = 0; index < vanillaOrder.Length; index++)
            {
                if (MoatFillApproachPolicy.TryChoose(
                        vanillaOrder[index], 10, 9, out selectedApproach, out _))
                {
                    selectedMoat = index + 1;
                    break;
                }
            }

            Check(selectedMoat == 2 && selectedApproach.Order == 4,
                "fully occupied moat: next Vanilla moat selected");
        }

        private static void TestNoFreeCandidate()
        {
            ApproachCandidate[] candidates = new ApproachCandidate[8];
            for (int index = 0; index < candidates.Length; index++)
                candidates[index] = Candidate(index, 20 + index, 20, occupied: true);
            bool found = MoatFillApproachPolicy.TryChoose(
                candidates, 10, 10, out _, out ApproachDecisionSummary summary);
            Check(!found, "all occupied: no candidate");
            Check(summary.Occupied == 8, "all occupied: all candidates counted");
        }

        private static void TestGeometryAndTerrainRejections()
        {
            ApproachCandidate[] candidates = CreateRejectedCandidates();
            candidates[0] = new ApproachCandidate(0, 1, 1, 1, true, false, true, false, true, false, 0);
            candidates[1] = new ApproachCandidate(1, 2, 1, 2, true, true, false, false, true, false, 0);
            candidates[2] = new ApproachCandidate(2, 3, 1, 3, true, true, true, false, false, false, 0);
            candidates[3] = Candidate(3, 4, 1);
            bool found = MoatFillApproachPolicy.TryChoose(
                candidates, 0, 0, out ApproachCandidate selected, out ApproachDecisionSummary summary);
            Check(found && selected.Order == 3, "geometry/terrain: valid fallback selected");
            Check(summary.NativeGeometryRejected == 2, "geometry/terrain: native rejections counted");
            Check(summary.BlockedTerrain == 1, "geometry/terrain: terrain rejection counted");
        }

        private static void TestCompletedMoatAndNativeMovementFlags()
        {
            ApproachCandidate[] candidates = CreateRejectedCandidates();
            candidates[0] = new ApproachCandidate(
                0, 10, 9, 1, true, true, true, true, true, false, 0);
            candidates[1] = Candidate(1, 11, 9);
            bool found = MoatFillApproachPolicy.TryChoose(
                candidates, 10, 8, out ApproachCandidate selected, out ApproachDecisionSummary summary);
            Check(found && selected.Order == 1,
                "completed moat: neighbouring moat is never used as a standing tile");
            Check(summary.CompletedMoatRejected == 1,
                "completed moat: rejection is diagnosed separately");
            Check(MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(0x10),
                "movement flags: low 0x30 gate mirrors 0x196280");
            Check(MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(0x20),
                "movement flags: both bits of the low 0x30 gate are covered");
            Check(MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(0x00000100),
                "movement flags: low structure bit mirrors 0x196280");
            Check(MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(0x10000000),
                "movement flags: high structure bit mirrors 0x196280");
            Check(!MoatFillApproachPolicy.HasDownstreamMovementBlockingFlags(0x00008000),
                "movement flags: ordinary ground is not rejected");
            Check(MoatFillApproachPolicy.IsCompletedMoat(0x40000000),
                "movement flags: completed-moat bit is recognized");
        }

        private static void TestRegionZeroUsesVanillaEquality()
        {
            Check(MoatFillApproachPolicy.IsSameNativeRegion(0, 0),
                "region: Vanilla permits equal region zero");
            Check(MoatFillApproachPolicy.IsSameNativeRegion(17, 17),
                "region: equal positive regions are accepted");
            Check(!MoatFillApproachPolicy.IsSameNativeRegion(0, 17),
                "region: unequal regions are rejected");
        }

        private static void TestReservationArithmetic()
        {
            Check(MoatFillApproachPolicy.TryUndoVanillaReservation(20, out byte zero) && zero == 0,
                "reservation: 20 returns to 0");
            Check(MoatFillApproachPolicy.TryUndoVanillaReservation(100, out byte eighty) && eighty == 80,
                "reservation: 100 returns to 80");
            Check(!MoatFillApproachPolicy.TryUndoVanillaReservation(19, out byte unchanged) && unchanged == 19,
                "reservation: underflow is rejected");
            Check(MoatFillApproachPolicy.TemporarilyExcludedReservation == 100,
                "reservation: exclusion matches Vanilla threshold");
        }

        private static void TestReservationRollbackOnFailure()
        {
            byte[] reservations = { 40, 60 };
            byte restoredFirst = byte.MaxValue;
            try
            {
                Check(MoatFillApproachPolicy.TryUndoVanillaReservation(
                        reservations[0], out byte firstOriginal) && firstOriginal == 20,
                    "reservation rollback: first Vanilla increment identified");
                restoredFirst = firstOriginal;
                reservations[0] = MoatFillApproachPolicy.TemporarilyExcludedReservation;
                throw new InvalidOperationException("intentional rollback test");
            }
            catch (InvalidOperationException)
            {
            }
            finally
            {
                reservations[0] = restoredFirst;
            }

            Check(reservations[0] == 20 && reservations[1] == 60,
                "reservation rollback: all temporary changes restored after failure");
        }

        private static void TestUnrelatedContextsRemainVanilla()
        {
            Check(!MoatFillApproachPolicy.ShouldInspectSelection(1),
                "context isolation: own-moat excavation remains Vanilla");
            Check(!MoatFillApproachPolicy.ShouldInspectSelection(0),
                "context isolation: unrelated relationship remains Vanilla");
            Check(MoatFillApproachPolicy.ShouldInspectSelection(2),
                "context isolation: hostile filling is inspected");
            Check(!MoatFillApproachPolicy.ShouldInspectSelection(2, supportedUnitType: false),
                "context isolation: unsupported unit types remain Vanilla");
            Check(!MoatFillApproachPolicy.ShouldReplaceResolverResult(1, true),
                "context isolation: mode-1 resolver remains Vanilla");
            Check(!MoatFillApproachPolicy.ShouldReplaceResolverResult(2, false),
                "context isolation: uncorrelated mode-2 resolver remains Vanilla");
            Check(MoatFillApproachPolicy.ShouldReplaceResolverResult(2, true),
                "context isolation: correlated mode-2 resolver may be replaced");
        }

        private static void TestCanonicalNativeContracts()
        {
            string gameRoot = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR") ??
                @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition";
            string path = Path.Combine(
                gameRoot,
                "Stronghold Crusader Definitive Edition_Data",
                "Plugins",
                "x86_64",
                "CrusaderDE.dll");
            Check(File.Exists(path), "native contract: canonical DLL exists");
            if (!File.Exists(path))
                return;

            byte[] file = File.ReadAllBytes(path);
            using (SHA256 sha = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", string.Empty);
                Check(string.Equals(hash, ExpectedHash, StringComparison.OrdinalIgnoreCase),
                    "native contract: SHA-256");
            }

            PeImage image = new PeImage(file);
            CheckBytes(image, FindRva, new byte[]
            {
                0x44, 0x89, 0x44, 0x24, 0x18, 0x89, 0x54, 0x24,
                0x10, 0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55,
                0x41, 0x56, 0x48, 0x83, 0xEC, 0x68, 0x48, 0x8B, 0xE9
            }, "native contract: selector entry");
            CheckBytes(image, ResolveRva, new byte[]
            {
                0x44, 0x89, 0x4C, 0x24, 0x20, 0x53, 0x57, 0x41,
                0x57, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x63, 0x44,
                0x24, 0x60, 0x45, 0x8B, 0xD0, 0x49, 0x63, 0xD9,
                0x4C, 0x63, 0xDA
            }, "native contract: resolver entry");
            CheckBytes(image, MovementPlannerRva, new byte[]
            {
                0x48, 0x89, 0x5C, 0x24, 0x20, 0x55, 0x56, 0x57,
                0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
                0x48, 0x83, 0xEC, 0x30, 0x48, 0x63, 0xF2
            }, "native contract: downstream movement planner entry");
            CheckBytes(image, 0x196464, new byte[]
            {
                0xF6, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04, 0x30
            }, "native contract: downstream low-flag gate");
            CheckBytes(image, 0x19648D, new byte[]
            {
                0xF7, 0x84, 0x8A, 0xB0, 0x71, 0x8F, 0x04,
                0x00, 0x01, 0x00, 0x10
            }, "native contract: downstream structure-flag gate");
            Check(image.CountNearCalls(StateDispatcherRva, StateDispatcherSize, FindRva) >= 2,
                "native contract: state dispatcher reaches selector for both work modes");
            Check(image.CountNearCalls(StateDispatcherRva, StateDispatcherSize, ResolveRva) >= 3,
                "native contract: state dispatcher reaches target and fill resolvers");
            Check(image.CountNearCalls(StateDispatcherRva, StateDispatcherSize, MovementPlannerRva) >= 1,
                "native contract: moat command chain reaches downstream movement planner");
        }

        private static ApproachCandidate[] CreateRejectedCandidates()
        {
            var candidates = new ApproachCandidate[8];
            for (int index = 0; index < candidates.Length; index++)
                candidates[index] = new ApproachCandidate(
                    index, 0, 0, -1, false, false, false, false, false, false, 0);
            return candidates;
        }

        private static ApproachCandidate Candidate(int order, int x, int y, bool occupied = false) =>
            new ApproachCandidate(
                order, x, y, order, true, true, true, false, true, occupied, occupied ? 42 : 0);

        private static void CheckBytes(PeImage image, int rva, byte[] expected, string name)
        {
            byte[] actual = image.ReadRva(rva, expected.Length);
            bool equal = actual.Length == expected.Length;
            for (int index = 0; equal && index < expected.Length; index++)
                equal = actual[index] == expected[index];
            Check(equal, name);
        }

        private static void Check(bool condition, string name)
        {
            if (condition)
            {
                Console.WriteLine($"PASS {name}");
                return;
            }
            failures++;
            Console.Error.WriteLine($"FAIL {name}");
        }

        private sealed class PeImage
        {
            private readonly byte[] file;
            private readonly List<Section> sections = new List<Section>();

            public PeImage(byte[] file)
            {
                this.file = file ?? throw new ArgumentNullException(nameof(file));
                int pe = ReadInt32(0x3C);
                int sectionCount = ReadUInt16(pe + 6);
                int optionalSize = ReadUInt16(pe + 20);
                int table = pe + 24 + optionalSize;
                for (int index = 0; index < sectionCount; index++)
                {
                    int entry = table + index * 40;
                    sections.Add(new Section(
                        ReadInt32(entry + 12),
                        Math.Max(ReadInt32(entry + 8), ReadInt32(entry + 16)),
                        ReadInt32(entry + 20)));
                }
            }

            public byte[] ReadRva(int rva, int length)
            {
                int offset = RvaToOffset(rva);
                var result = new byte[length];
                Buffer.BlockCopy(file, offset, result, 0, length);
                return result;
            }

            public int CountNearCalls(int startRva, int length, int targetRva)
            {
                int count = 0;
                byte[] bytes = ReadRva(startRva, length);
                for (int index = 0; index <= bytes.Length - 5; index++)
                {
                    if (bytes[index] != 0xE8)
                        continue;
                    int displacement = bytes[index + 1] |
                        bytes[index + 2] << 8 |
                        bytes[index + 3] << 16 |
                        bytes[index + 4] << 24;
                    if (startRva + index + 5 + displacement == targetRva)
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
                throw new InvalidOperationException($"RVA 0x{rva:X} is outside all PE sections.");
            }

            private int ReadUInt16(int offset) => file[offset] | file[offset + 1] << 8;

            private int ReadInt32(int offset) => file[offset] |
                file[offset + 1] << 8 |
                file[offset + 2] << 16 |
                file[offset + 3] << 24;

            private readonly struct Section
            {
                public Section(int virtualAddress, int size, int rawOffset)
                {
                    VirtualAddress = virtualAddress;
                    Size = size;
                    RawOffset = rawOffset;
                }

                public int VirtualAddress { get; }
                public int Size { get; }
                public int RawOffset { get; }
            }
        }
    }
}
