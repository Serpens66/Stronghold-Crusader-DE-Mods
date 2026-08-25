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
            TestConflictRequiresConfirmedBulldoze();
            TestConflictDetectionAndPersistentLock();
            TestConflictKeyIndependenceAndPassNormalization();
            TestConflictExpiryReplacementResetAndTickOverflow();
            if (failures == 0)
            {
                Console.WriteLine("PASS: Better AI overbuild policy and conflict-state tests passed.");
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

        private static void TestConflictRequiresConfirmedBulldoze()
        {
            var state = new BetterAIOverbuildConflictState();
            BetterAIOverbuildConflictKey key = ConflictKey();
            Assert(!state.ShouldProtect(100, key, 10, 1000u),
                "first blocker must remain delegated");
            state.RegisterDelegatedDecision(100, key, 10, 1000u);

            Assert(!state.ShouldProtect(101, key, 11, 1001u),
                "a decision without synchronous bulldoze must not create a conflict");

            state.RegisterDelegatedDecision(102, key, 12, 1002u);
            Assert(!state.ObserveBulldoze(103, 12, 1002u),
                "a later-tick bulldoze must not confirm a delegated decision");
            Assert(!state.ShouldProtect(104, key, 13, 1003u),
                "an uncorrelated bulldoze must not create a conflict");
        }

        private static void TestConflictDetectionAndPersistentLock()
        {
            var state = new BetterAIOverbuildConflictState();
            BetterAIOverbuildConflictKey key = ConflictKey();
            state.RegisterDelegatedDecision(200, key, 20, 2000u);
            Assert(state.ObserveBulldoze(200, 20, 2000u),
                "synchronous bulldoze should confirm the first conflict removal");

            Assert(state.ShouldProtect(300, key, 21, 2001u),
                "a rebuilt blocker with a new global ID should activate the lock");
            Assert(state.ShouldProtect(301, key, 21, 2001u),
                "an active lock should persist over later retries");

            Assert(state.ObserveRemoval(21, 2001u) == 1,
                "removing the locked blocker should clear its lock");
            Assert(!state.ShouldProtect(302, key, 22, 2002u),
                "a removed lock must not protect a later fresh blocker");
        }

        private static void TestConflictKeyIndependenceAndPassNormalization()
        {
            BetterAIOverbuildPlacementKey pass0 = BetterAIOverbuildPlacementKey.FromNativePass(
                2, 50, 300, 400, 7, 0);
            BetterAIOverbuildPlacementKey pass1 = BetterAIOverbuildPlacementKey.FromNativePass(
                2, 50, 298, 398, 7, 1);
            Assert(pass0.Equals(pass1),
                "broad passes 0 and 1 should normalize to one placement key");
            BetterAIOverbuildPlacementKey pass0Orientation11 =
                BetterAIOverbuildPlacementKey.FromNativePass(2, 50, 300, 400, 11, 0);
            BetterAIOverbuildPlacementKey pass1Orientation11 =
                BetterAIOverbuildPlacementKey.FromNativePass(2, 50, 299, 399, 11, 1);
            Assert(pass0Orientation11.Equals(pass1Orientation11),
                "orientation 11 should reverse the one-tile second-pass shift");

            var state = new BetterAIOverbuildConflictState();
            BetterAIOverbuildConflictKey original = ConflictKey();
            state.RegisterDelegatedDecision(400, original, 30, 3000u);
            state.ObserveBulldoze(400, 30, 3000u);
            BetterAIOverbuildPlacementKey p = original.Placement;
            BetterAIOverbuildBlockerKey b = original.Blocker;
            BetterAIOverbuildConflictKey[] differences =
            {
                new BetterAIOverbuildConflictKey(new BetterAIOverbuildPlacementKey(3, p.Mapper, p.BaseX, p.BaseY, p.Orientation), b),
                new BetterAIOverbuildConflictKey(new BetterAIOverbuildPlacementKey(p.PlacingPlayerId, p.Mapper + 1, p.BaseX, p.BaseY, p.Orientation), b),
                new BetterAIOverbuildConflictKey(new BetterAIOverbuildPlacementKey(p.PlacingPlayerId, p.Mapper, p.BaseX + 1, p.BaseY, p.Orientation), b),
                new BetterAIOverbuildConflictKey(new BetterAIOverbuildPlacementKey(p.PlacingPlayerId, p.Mapper, p.BaseX, p.BaseY + 1, p.Orientation), b),
                new BetterAIOverbuildConflictKey(new BetterAIOverbuildPlacementKey(p.PlacingPlayerId, p.Mapper, p.BaseX, p.BaseY, p.Orientation + 1), b),
                new BetterAIOverbuildConflictKey(p, new BetterAIOverbuildBlockerKey(b.OwnerId + 1, b.StructureType, b.AnchorX, b.AnchorY)),
                new BetterAIOverbuildConflictKey(p, new BetterAIOverbuildBlockerKey(b.OwnerId, b.StructureType + 1, b.AnchorX, b.AnchorY)),
                new BetterAIOverbuildConflictKey(p, new BetterAIOverbuildBlockerKey(b.OwnerId, b.StructureType, b.AnchorX + 1, b.AnchorY)),
                new BetterAIOverbuildConflictKey(p, new BetterAIOverbuildBlockerKey(b.OwnerId, b.StructureType, b.AnchorX, b.AnchorY + 1)),
            };
            foreach (BetterAIOverbuildConflictKey difference in differences)
                Assert(!state.ShouldProtect(401, difference, 31, 3001u),
                    "each conflict-key component must remain independent");
            Assert(state.ShouldProtect(401, original, 31, 3001u),
                "the exact conflict key should still activate the lock");
        }

        private static void TestConflictExpiryReplacementResetAndTickOverflow()
        {
            BetterAIOverbuildConflictKey key = ConflictKey();
            var expiredState = new BetterAIOverbuildConflictState();
            expiredState.RegisterDelegatedDecision(500, key, 40, 4000u);
            expiredState.ObserveBulldoze(500, 40, 4000u);
            bool expired = expiredState.ShouldProtect(
                unchecked(500 + (int)BetterAIOverbuildConflictState.RepeatWindowTicks + 1),
                key,
                41,
                4001u);
            Assert(!expired,
                "a retry after more than 12000 ticks must expire without locking");

            var sameIdState = new BetterAIOverbuildConflictState();
            sameIdState.RegisterDelegatedDecision(600, key, 50, 5000u);
            sameIdState.ObserveBulldoze(600, 50, 5000u);
            Assert(!sameIdState.ShouldProtect(601, key, 50, 5000u),
                "the same global ID must not prove a rebuild");

            var replacementState = LockedState(key, 700, 60, 6000u, 61, 6001u);
            Assert(!replacementState.ShouldProtect(702, key, 62, 6002u) &&
                replacementState.ActiveLockCount == 0,
                "a different object replacing the locked blocker should clear the lock");

            var resetState = LockedState(key, 800, 70, 7000u, 71, 7001u);
            int removed = resetState.ActiveLockCount;
            resetState.Reset();
            Assert(removed == 1 && resetState.ActiveLockCount == 0 &&
                !resetState.ShouldProtect(802, key, 71, 7001u),
                "map or setting reset should clear every conflict state");

            var sharedBlockerState = LockedState(key, 900, 90, 9000u, 92, 9002u);
            BetterAIOverbuildConflictKey secondKey = new BetterAIOverbuildConflictKey(
                new BetterAIOverbuildPlacementKey(3, 180, 310, 410, 4),
                key.Blocker);
            sharedBlockerState.RegisterDelegatedDecision(901, secondKey, 91, 9001u);
            sharedBlockerState.ObserveBulldoze(901, 91, 9001u);
            sharedBlockerState.ShouldProtect(902, secondKey, 92, 9002u);
            int sharedClears = sharedBlockerState.ObserveRemoval(92, 9002u);
            Assert(sharedClears == 2 && sharedBlockerState.ActiveLockCount == 0,
                "removing one physical blocker should clear every lock bound to its identity");

            int nearOverflow = int.MaxValue - 5;
            int afterOverflow = unchecked(nearOverflow + 10);
            Assert(BetterAIOverbuildConflictState.ElapsedTicks(afterOverflow, nearOverflow) == 10u,
                "tick difference should remain correct across signed overflow");
            var overflowState = new BetterAIOverbuildConflictState();
            overflowState.RegisterDelegatedDecision(nearOverflow, key, 80, 8000u);
            overflowState.ObserveBulldoze(nearOverflow, 80, 8000u);
            Assert(overflowState.ShouldProtect(afterOverflow, key, 81, 8001u),
                "repeat detection should work across signed tick overflow");
        }

        private static BetterAIOverbuildConflictState LockedState(
            BetterAIOverbuildConflictKey key,
            int tick,
            int firstId,
            uint firstGlobalId,
            int rebuiltId,
            uint rebuiltGlobalId)
        {
            var state = new BetterAIOverbuildConflictState();
            state.RegisterDelegatedDecision(tick, key, firstId, firstGlobalId);
            state.ObserveBulldoze(tick, firstId, firstGlobalId);
            state.ShouldProtect(tick + 1, key, rebuiltId, rebuiltGlobalId);
            return state;
        }

        private static BetterAIOverbuildConflictKey ConflictKey() =>
            new BetterAIOverbuildConflictKey(
                new BetterAIOverbuildPlacementKey(2, 50, 300, 400, 7),
                new BetterAIOverbuildBlockerKey(5, 12, 277, 408));

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
                    keepY);
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
