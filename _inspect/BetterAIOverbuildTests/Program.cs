using BugfixesAndQoL;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BetterAIOverbuildTests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            TestAlwaysBroadSets();
            TestReservedAreaSetsAndGeometry();
            TestForeignBlockerPolicy();
            TestOverflowSafeDistance();
            TestDiagnosticDeduplicationAndRetry();
            TestDiagnosticRemovalCorrelationAndSummary();

            if (failures == 0)
            {
                Console.WriteLine("PASS: Better AI overbuild policy and diagnostic-state tests passed.");
                return 0;
            }

            Console.Error.WriteLine($"FAIL: Better AI overbuild tests reported {failures} failure(s).");
            return 1;
        }

        private static void TestAlwaysBroadSets()
        {
            int[] expectedMappers = { 52, 54, 77, 79, 80, 81, 86, 87 };
            int[] expectedStructures = { 1, 8, 9, 10, 11, 19, 26, 108 };
            for (int value = 0; value <= 120; value++)
            {
                Assert(
                    BetterAIOverbuildPolicy.IsAlwaysBroadMapper((eMappers)value) == Contains(expectedMappers, value),
                    $"unexpected always-broad mapper classification for {value}");
                Assert(
                    BetterAIOverbuildPolicy.IsAlwaysBroadStructure((eStructs)value) == Contains(expectedStructures, value),
                    $"unexpected always-broad structure classification for {value}");
            }

            int[] addedMappers = { 52, 77, 80, 81 };
            for (int value = 0; value <= 120; value++)
            {
                Assert(
                    BetterAIOverbuildPolicy.IsAddedAlwaysBroadMapper((eMappers)value) == Contains(addedMappers, value),
                    $"unexpected added mapper classification for {value}");
            }
        }

        private static void TestReservedAreaSetsAndGeometry()
        {
            int[] expectedReserved = { 51, 53, 56, 57, 58, 59 };
            int[] expectedAlwaysProtected = { 56, 57, 58 };
            for (int value = 0; value <= 120; value++)
            {
                Assert(
                    BetterAIOverbuildPolicy.IsReservedAreaStructure((eStructs)value) ==
                        Contains(expectedReserved, value),
                    $"unexpected reserved-area structure classification for {value}");
                Assert(
                    BetterAIOverbuildPolicy.IsAlwaysProtectedReservedArea((eStructs)value) ==
                        Contains(expectedAlwaysProtected, value),
                    $"unexpected always-protected reserved-area classification for {value}");
            }

            Assert(BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_LGT, eStructs.STRUCT_BARRACKS_WOOD),
                "light paradeground should accept the mercenary post as parent");
            Assert(BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_MISS, eStructs.STRUCT_BEDOUIN_STOCKADE),
                "missile paradeground should accept the Bedouin stockade as parent");
            Assert(BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_ENG, eStructs.STRUCT_ENGINEERS_GUILD),
                "engineer paradeground should accept the engineers guild as parent");
            Assert(BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_TUN, eStructs.STRUCT_TUNNELLERS_GUILD),
                "tunneller paradeground should accept the tunnellers guild as parent");
            Assert(BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_OIL, eStructs.STRUCT_OIL_SMELTER),
                "oil paradeground should accept the oil smelter as parent");
            Assert(!BetterAIOverbuildPolicy.IsReservationParentCandidate(
                eStructs.STRUCT_PARADEGROUND_ENG, eStructs.STRUCT_OIL_SMELTER),
                "reserved areas must reject unrelated parents");

            Assert(BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                eStructs.STRUCT_PARADEGROUND_LGT, 105, 95, 100, 100),
                "5-tile reservation offset should match");
            Assert(!BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                eStructs.STRUCT_PARADEGROUND_LGT, 104, 96, 100, 100),
                "inside the five-tile component boundary should not match");
            Assert(!BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                eStructs.STRUCT_PARADEGROUND_LGT, 106, 95, 100, 100),
                "6-tile reservation offset should not match");
            Assert(BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                eStructs.STRUCT_PARADEGROUND_OIL, 104, 96, 100, 100),
                "4-tile oil reservation offset should match");
            Assert(!BetterAIOverbuildPolicy.IsWithinReservationParentRange(
                eStructs.STRUCT_PARADEGROUND_OIL, 105, 96, 100, 100),
                "5-tile oil reservation offset should not match");
        }

        private static void TestForeignBlockerPolicy()
        {
            AssertReason(
                BetterAIOverbuildProtectionReason.AlwaysBroad,
                placing: 2, owner: 3, ownerIsAi: true, type: 10,
                protectedReservationParent: false,
                hasKeep: false, blockerX: 100, blockerY: 100, keepX: 0, keepY: 0,
                "always-broad foreign AI building");
            AssertReason(
                BetterAIOverbuildProtectionReason.KeepRadius,
                placing: 2, owner: 3, ownerIsAi: true, type: 30,
                protectedReservationParent: false,
                hasKeep: true, blockerX: 110, blockerY: 110, keepX: 100, keepY: 100,
                "distance exactly 20");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 3, ownerIsAi: true, type: 30,
                protectedReservationParent: false,
                hasKeep: true, blockerX: 111, blockerY: 110, keepX: 100, keepY: 100,
                "distance 21");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 2, ownerIsAi: true, type: 10,
                protectedReservationParent: false,
                hasKeep: true, blockerX: 100, blockerY: 100, keepX: 100, keepY: 100,
                "same owner");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 3, ownerIsAi: false, type: 10,
                protectedReservationParent: false,
                hasKeep: true, blockerX: 100, blockerY: 100, keepX: 100, keepY: 100,
                "human owner");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 0, ownerIsAi: false, type: 10,
                protectedReservationParent: false,
                hasKeep: false, blockerX: 100, blockerY: 100, keepX: 0, keepY: 0,
                "neutral owner");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 3, ownerIsAi: true, type: 30,
                protectedReservationParent: false,
                hasKeep: false, blockerX: 100, blockerY: 100, keepX: 100, keepY: 100,
                "missing keep");
            AssertReason(
                BetterAIOverbuildProtectionReason.ReservedArea,
                placing: 2, owner: 3, ownerIsAi: true, type: 57,
                protectedReservationParent: false,
                hasKeep: false, blockerX: 100, blockerY: 100, keepX: 0, keepY: 0,
                "recruitment reservation of an always-broad building");
            AssertReason(
                BetterAIOverbuildProtectionReason.ReservedArea,
                placing: 2, owner: 3, ownerIsAi: true, type: 53,
                protectedReservationParent: true,
                hasKeep: true, blockerX: 125, blockerY: 100, keepX: 100, keepY: 100,
                "reservation inherits protected parent classification");
            AssertReason(
                BetterAIOverbuildProtectionReason.None,
                placing: 2, owner: 3, ownerIsAi: true, type: 53,
                protectedReservationParent: false,
                hasKeep: true, blockerX: 125, blockerY: 100, keepX: 100, keepY: 100,
                "unprotected parent does not protect a distant reservation");
        }

        private static void TestOverflowSafeDistance()
        {
            long expected = 2L * uint.MaxValue;
            long actual = BetterAIOverbuildPolicy.ManhattanDistance(
                int.MinValue, int.MinValue, int.MaxValue, int.MaxValue);
            Assert(actual == expected, $"overflow-safe distance expected {expected}, got {actual}");
        }

        private static void TestDiagnosticDeduplicationAndRetry()
        {
            var state = new BetterAIOverbuildDiagnosticState();
            Assert(state.RecordPromotion(10, 2, 52, 100, 100), "first promotion should be recorded");
            Assert(!state.RecordPromotion(10, 2, 52, 100, 100), "same-tick promotion should deduplicate");
            Assert(state.RecordPromotion(11, 2, 52, 100, 100), "later retry tick should be recorded");

            Assert(state.RecordDecision(
                20, 2, 52, 100, 100, 0, 123, 999u, true, out _),
                "first protected decision should be recorded");
            Assert(!state.RecordDecision(
                20, 2, 52, 100, 100, 0, 123, 999u, true, out _),
                "same-tick protected decision should deduplicate");
            Assert(state.RecordDecision(
                21, 2, 52, 100, 100, 0, 123, 999u, true, out _),
                "protected retry on later tick should be recorded");

            BetterAIOverbuildDiagnosticSummary summary = state.SnapshotAndReset();
            Assert(summary.PromotionCounts.TryGetValue(52, out int promotions) && promotions == 2,
                "promotion count should include later retry but exclude duplicate");
            Assert(summary.ProtectedCount == 2, "protected count should be two");
            Assert(summary.DuplicateCount == 2, "duplicate count should be two");
        }

        private static void TestDiagnosticRemovalCorrelationAndSummary()
        {
            var state = new BetterAIOverbuildDiagnosticState();
            Assert(state.RecordDecision(
                30, 2, 54, 50, 50, 0, 200, 1000u, false, out PendingRemoval pending),
                "delegated decision should be recorded");
            Assert(pending.BlockerId == 200 && pending.BlockerGlobalId == 1000u,
                "pending removal identity should be retained");
            Assert(state.ConfirmRemoval(30, 200, 1000u, out PendingRemoval confirmed),
                "same-tick bulldoze should correlate");
            Assert(confirmed.PlacingPlayerId == 2 && confirmed.Mapper == 54,
                "correlated removal should retain placement context");

            Assert(state.RecordDecision(
                31, 2, 54, 50, 50, 0, 201, 1001u, false, out _),
                "second delegated decision should be recorded");
            state.RecordPromotion(32, 2, 77, 51, 51);
            Assert(!state.ConfirmRemoval(32, 201, 1001u, out _),
                "a later-tick bulldoze must not correlate");

            BetterAIOverbuildDiagnosticSummary summary = state.SnapshotAndReset();
            Assert(summary.DelegatedCount == 2, "delegated count should be two");
            Assert(summary.ConfirmedRemovalCount == 1, "confirmed removal count should be one");
            Assert(summary.UncorrelatedDelegationCount == 1,
                "uncorrelated delegation count should be one");

            BetterAIOverbuildDiagnosticSummary reset = state.SnapshotAndReset();
            Assert(reset.DelegatedCount == 0 && reset.ConfirmedRemovalCount == 0 &&
                reset.PromotionCounts.Count == 0,
                "snapshot should reset all map state");

            // The gameplay policy has no diagnostic dependency and remains callable after resets.
            Assert(BetterAIOverbuildPolicy.IsAlwaysBroadMapper(eMappers.MAPPER_GRANARY),
                "diagnostic state must not influence gameplay policy");
        }

        private static void AssertReason(
            BetterAIOverbuildProtectionReason expected,
            int placing,
            int owner,
            bool ownerIsAi,
            int type,
            bool protectedReservationParent,
            bool hasKeep,
            int blockerX,
            int blockerY,
            int keepX,
            int keepY,
            string scenario)
        {
            BetterAIOverbuildProtectionReason actual =
                BetterAIOverbuildPolicy.ClassifyForeignBlocker(
                    placing,
                    owner,
                    ownerIsAi,
                    (eStructs)type,
                    protectedReservationParent,
                    hasKeep,
                    blockerX,
                    blockerY,
                    keepX,
                    keepY,
                    out _);
            Assert(actual == expected, $"{scenario}: expected {expected}, got {actual}");
        }

        private static bool Contains(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }
            return false;
        }

        private static void Assert(bool condition, string message)
        {
            if (condition)
                return;
            failures++;
            Console.Error.WriteLine("FAIL: " + message);
        }
    }
}
