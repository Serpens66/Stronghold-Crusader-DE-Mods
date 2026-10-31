using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public sealed class RemovalEvidenceTests
    {
        private static BuildingEvidenceIdentity Hut(int slot = 4, uint global = 20, int x = 51, int y = 61,
            int player = 2, AliveState state = AliveState.MarkedForDeletion) =>
            new BuildingEvidenceIdentity(slot, global, player, (int)eStructs.STRUCT_WOODCUTTERS_HUT, x, y, (int)state);

        [TestMethod]
        public void ExactSignedNativeThresholdsAndBothRetirementBranches()
        {
            var input = new RemovalInputs { Kind = "wood-retirement", Profile = 1, GridWood = 3, Wood = 21, StaffedHuts = 2, WorstPath = 25 };
            Assert.AreEqual("wood-distance", input.CandidateReason);
            string gates = input.GateSignature;
            input.Wood = 22; Assert.AreEqual(gates, input.GateSignature, "Unchanged native gates need no new non-removing detail sample.");
            input.Wood = 20; Assert.IsFalse(input.Eligible);
            input.Wood = 21; input.StaffedHuts = 1; Assert.IsFalse(input.Eligible);
            input.StaffedHuts = 2; input.WorstPath = 24; Assert.IsFalse(input.Eligible);
            input.GridWood = 0; input.Wood = -1; Assert.AreEqual("wood-grid-empty", input.CandidateReason);
            input.Profile = 0; Assert.IsFalse(input.Eligible);
            input = new RemovalInputs { Kind = "wood-counter-retirement", Counter = 20, RawGuard1B8 = 0, Control = -1, Enabled = 1, GameActive = 1 };
            Assert.AreEqual("wood-counter", input.CandidateReason);
            input.Counter = 19; Assert.IsFalse(input.Eligible);
            input.Counter = -1; Assert.IsFalse(input.Eligible);
            input.Counter = 20; input.RawGuard1B8 = -1; Assert.IsFalse(input.Eligible);
            input.RawGuard1B8 = 0; input.Enabled = 0; Assert.IsFalse(input.Eligible);
            input.Enabled = 1; input.Control = 0; Assert.IsFalse(input.Eligible);
            input.Control = -1; input.GameActive = 0; Assert.IsFalse(input.Eligible);
            EconomyDiagnostics.ValidateRemovalLayout();
        }

        [TestMethod]
        public void MarkingIsNotRemovalAndOnlyConfirmedRemovalCanCorrelateASpawn()
        {
            var ledger = new RemovalEvidenceLedger(); ledger.Reset(10);
            var before = Hut();
            var mark = new RemovalMark { Session = 10, Identity = before, Tick = 5, Reason = "wood-distance" };
            Assert.IsTrue(ledger.Mark(mark));
            Assert.IsNull(ledger.Spawn(Hut(5, 21, state: AliveState.NeedsInit)));
            Assert.IsNull(ledger.Confirm(before, null, 6));
            Assert.IsNull(ledger.Confirm(before, before, 6));
            Assert.IsNull(ledger.Confirm(Hut(global: 22), Hut(state: AliveState.None), 6));
            Assert.AreSame(mark, ledger.Confirm(before, Hut(state: AliveState.None), 7));
            var rebuild = ledger.Spawn(Hut(5, 21, state: AliveState.NeedsInit));
            Assert.IsNotNull(rebuild); Assert.IsTrue(rebuild.SameCell);
            Assert.AreEqual(7, rebuild.Previous.ConfirmedTick); Assert.AreEqual(1, rebuild.Count);
            Assert.IsNull(ledger.Spawn(Hut(6, 22, player: 3, state: AliveState.IsAlive)));
            Assert.IsNull(ledger.Spawn(Hut(6, 22, x: 100, state: AliveState.IsAlive)));
        }

        [TestMethod]
        public void AdjacentCellUsesLatestConfirmationAndGroupDoesNotOverwriteDirectEvidence()
        {
            var ledger = new RemovalEvidenceLedger(); ledger.Reset(1);
            var direct = new RemovalMark { Session = 1, Identity = Hut(), Reason = "wood-counter", Tick = 1 };
            Assert.IsTrue(ledger.Mark(direct));
            Assert.IsFalse(ledger.Mark(new RemovalMark { Session = 1, Identity = Hut(), Reason = "accessibility-group-followup", GroupFollowup = true }));
            // Slot reuse is terminal evidence for the old identity, never attribution to the new one.
            Assert.AreSame(direct, ledger.Confirm(Hut(), Hut(global: 99, state: AliveState.IsAlive), 4));
            var spawn = ledger.Spawn(Hut(6, 100, x: 56, state: AliveState.IsAlive));
            Assert.IsFalse(spawn.SameCell); Assert.AreSame(direct, spawn.Previous);
            Assert.AreEqual(2, ledger.Spawn(Hut(7, 101, state: AliveState.IsAlive)).Count);
            ledger.Reset(2);
            Assert.IsNull(ledger.Spawn(Hut(8, 102, state: AliveState.IsAlive)));
            Assert.IsFalse(ledger.Mark(direct)); Assert.AreEqual(0, ledger.PendingCount);
        }

        [TestMethod]
        public void BoundedStateExplicitlyCountsOmissions()
        {
            var ledger = new RemovalEvidenceLedger(); ledger.Reset(1);
            for (int i = 1; i <= RemovalEvidenceLedger.Limit; i++)
                Assert.IsTrue(ledger.Mark(new RemovalMark { Session = 1, Identity = Hut(i, (uint)i) }));
            Assert.IsFalse(ledger.Mark(new RemovalMark { Session = 1, Identity = Hut(RemovalEvidenceLedger.Limit + 1) }));
            Assert.AreEqual(1L, ledger.Omitted);
            ledger.Reset(2); Assert.AreEqual(0L, ledger.Omitted);
        }

        [TestMethod]
        public void ReusedSlotCanKeepBothOldAndNewMarksUntilTheirOwnTerminalObservation()
        {
            var ledger = new RemovalEvidenceLedger(); ledger.Reset(1);
            var old = new RemovalMark { Session = 1, Identity = Hut(), Reason = "wood-distance" };
            var next = new RemovalMark { Session = 1, Identity = Hut(global: 99), Reason = "wood-counter" };
            Assert.IsTrue(ledger.Mark(old)); Assert.IsTrue(ledger.Mark(next));
            Assert.AreEqual(2, ledger.PendingCount);
            Assert.AreSame(old, ledger.Confirm(old.Identity, next.Identity, 10));
            Assert.AreSame(next, ledger.Find(next.Identity)); Assert.AreEqual(1, ledger.PendingCount);
            Assert.IsNull(ledger.Confirm(next.Identity, next.Identity, 11));
        }

        [TestMethod]
        public void ProductionCounterWrapperIsolatesBothObserverFailuresAndCallsVanillaOnce()
        {
            int originals = 0, ends = 0, errors = 0;
            int result = EconomyNativeHooks.ObserveCounter((state, slot) => { originals++; Assert.AreEqual(7, slot); return 1; }, 0, 7,
                slot => throw new InvalidOperationException("begin"), (op, value) => { ends++; Assert.AreEqual(1, value); throw new Exception("end"); },
                error => errors++);
            Assert.AreEqual(1, result); Assert.AreEqual(1, originals); Assert.AreEqual(1, ends); Assert.AreEqual(2, errors);
            var nativeError = new InvalidOperationException("native");
            Assert.AreSame(nativeError, Assert.ThrowsExactly<InvalidOperationException>(() =>
                EconomyNativeHooks.ObserveCounter((state, slot) => throw nativeError, 0, 7, null,
                    (op, value) => { Assert.IsNull(value); throw new Exception("end"); }, error => throw new Exception("logger"))));
        }

        [TestMethod]
        public void NestedCellCallsRetainTheirOwnCapturesAndDoNotMaskNativeExceptions()
        {
            int originals = 0, ends = 0;
            EconomyNativeHooks.ObserveCell((state, x, y) =>
            {
                originals++;
                EconomyNativeHooks.ObserveCell((s, a, b) => originals++, state, 2, 3,
                    (s, a, b) => new RemovalCellObservation { X = a }, c => { Assert.AreEqual(2, c.X); ends++; }, e => Assert.Fail(e.Message));
            }, 0, 1, 3, (state, x, y) => new RemovalCellObservation { X = x },
                c => { Assert.AreEqual(1, c.X); ends++; }, e => Assert.Fail(e.Message));
            Assert.AreEqual(2, originals); Assert.AreEqual(2, ends);
            var nativeError = new InvalidOperationException("native");
            Assert.AreSame(nativeError, Assert.ThrowsExactly<InvalidOperationException>(() =>
                EconomyNativeHooks.ObserveCell((s, x, y) => throw nativeError, 0, 1, 2,
                    (s, x, y) => throw new Exception("capture"), c => throw new Exception("end"), e => { })));
        }

        [TestMethod]
        public void ProductionCellReaderCapturesSignedBytesBeforeAndAfterAndRejectsRetiredSession()
        {
            var owner = RuntimeFixture.Create<EconomyDiagnostics>();
            var output = new ConcurrentQueue<string>();
            RuntimeFixture.Set(owner, "output", output);
            RuntimeFixture.Set(owner, "budget", new EconomyLogBudget());
            RuntimeFixture.Set(owner, "active", true);
            RuntimeFixture.Set(owner, "nativeLayout", true);
            RuntimeFixture.Set(owner, "removalLayout", true);
            RuntimeFixture.Set(owner, "session", 11L);
            // A private AIV-sized buffer, with a synthetic image base. No game manager is initialized.
            IntPtr root = Marshal.AllocHGlobal(0x60000);
            try
            {
                RuntimeFixture.Set(owner, "module", root.ToInt64() - 0x34A9F70L);
                RuntimeFixture.Set(owner, "moduleSize", 0x34A9F70 + 0x60000);
                int cellOffset = (2 * 160 + 4) * 0x30;
                Marshal.WriteByte(root, cellOffset + 0x5B837, 253); // Native signed byte -3.
                Marshal.WriteByte(root, cellOffset + 0x5B843, 0);
                Marshal.WriteByte(root, cellOffset + 0x5B847, 2);
                EconomyNativeHooks.ObserveCell((state, x, y) =>
                {
                    Marshal.WriteByte(root, cellOffset + 0x5B843, 16);
                    Marshal.WriteByte(root, cellOffset + 0x5B847, 3);
                }, unchecked((ulong)root.ToInt64()), 10, 20, owner.SafeBeginCell, owner.SafeEndCell, error => Assert.Fail(error.Message));
                Assert.IsTrue(output.TryDequeue(out string line));
                StringAssert.Contains(line, "before=[gridWoodWeight=-3, retryDelay=0, removalCount=2]");
                StringAssert.Contains(line, "after=[gridWoodWeight=-3, retryDelay=16, removalCount=3]");
                var capture = owner.SafeBeginCell(unchecked((ulong)root.ToInt64()), 10, 20);
                RuntimeFixture.Set(owner, "session", 12L);
                owner.SafeEndCell(capture);
                Assert.IsTrue(output.IsEmpty, "A late callback cannot publish into a replacement/failed-load session.");
                var unavailable = owner.SafeBeginCell(unchecked((ulong)root.ToInt64()) + 1, 10, 20);
                StringAssert.Contains(unavailable.Before, "invalid-root-or-cell");
            }
            finally { Marshal.FreeHGlobal(root); }
        }
    }
}
