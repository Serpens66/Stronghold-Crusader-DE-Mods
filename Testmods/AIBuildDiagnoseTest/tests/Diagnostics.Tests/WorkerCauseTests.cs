using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public sealed class WorkerCauseTests
    {
        private static BuildingEvidenceIdentity Hut(int slot = 4, uint global = 100) =>
            new BuildingEvidenceIdentity(slot, global, 3, (int)eStructs.STRUCT_WOODCUTTERS_HUT, 250, 350, (int)AliveState.IsAlive);
        private static WorkerSnapshot Worker(int slot = 8, uint global = 80, uint health = 0, uint linkedGlobal = 100,
            BuildingEvidenceIdentity building = null) => new WorkerSnapshot(slot, global, 3, (int)eChimps.CHIMP_TYPE_WOODCUTTER,
                110, (int)AliveState.MarkedForDeletion, health, 260, 360, 280, 370, 4, linkedGlobal, building ?? Hut());

        [TestMethod]
        public void CombatDeathLinksByBothIdentitiesAndRepeatedIncrementIsNotASecondDeath()
        {
            var ledger = new WorkerCauseLedger(); ledger.Reset(1);
            var death = ledger.Trigger(1, Worker(), 20, "killed-melee", "attacker=enemy", true);
            var first = ledger.Increment(1, Worker(), 10);
            Assert.AreEqual("killed-melee", first.Cause); Assert.AreSame(death, first.Trigger); Assert.IsFalse(first.RepeatedUnit);
            Assert.IsNull(ledger.Trigger(1, Worker(), 21, "killed-melee", "duplicate", true));
            Assert.AreEqual(1L, ledger.DuplicateDeaths);
            Assert.IsTrue(ledger.Increment(1, Worker(), 20).RepeatedUnit);
            var secondWorker = Worker(9, 90);
            ledger.Trigger(1, secondWorker, 22, "killed-projectile", "projectile=4", true);
            Assert.AreEqual("killed-projectile", ledger.Increment(1, secondWorker, 30).Cause);
            StringAssert.Contains(ledger.Describe(Hut()), "matchedIncrementsAreNotUniqueDeaths=true");
            ledger.Trigger(1, secondWorker, 23, "killed-melee", "conflicting-event", true);
            Assert.AreEqual("conflicting-death-observations", ledger.Increment(1, secondWorker, 40).Cause);
        }

        [TestMethod]
        public void MissingHistoryReuseAndAssignmentChangesNeverBecomeCombatEvidence()
        {
            var ledger = new WorkerCauseLedger(); ledger.Reset(1);
            Assert.AreEqual("unobserved-history", ledger.Increment(1, Worker(), 20).Cause);
            ledger.Trigger(1, Worker(), 20, "killed-melee", "enemy", true);
            Assert.AreEqual("unobserved-history", ledger.Increment(1, Worker(global: 81), 30).Cause);
            Assert.AreEqual("assignment-changed-after-death", ledger.Increment(1, Worker(linkedGlobal: 200, building: Hut(global: 200)), 40).Cause);
            Assert.AreEqual("building-identity-conflict", ledger.Increment(1, Worker(building: Hut(global: 300)), 50).Cause);
            StringAssert.Contains(ledger.Describe(Hut(global: 300)), "building-identity-conflict");
            Assert.AreEqual("death-health-conflict", ledger.Increment(1, Worker(health: 10), 60).Cause);
            ledger.Reset(2);
            Assert.IsNull(ledger.Increment(1, Worker(), 70)); Assert.IsNull(ledger.Trigger(1, Worker(), 0, "killed-melee", "stale", true));
            StringAssert.Contains(ledger.Describe(Hut()), "unobserved");
        }

        [TestMethod]
        public void ReturnFailuresAndContradictoryTriggersStayDistinct()
        {
            var ledger = new WorkerCauseLedger(); ledger.Reset(1);
            ledger.Trigger(1, Worker(health: 10), 1, "return-accessibility-effective-rejected", "helper-zero", false);
            Assert.AreEqual("return-accessibility-effective-rejected", ledger.Increment(1, Worker(health: 10), 10).Cause);
            ledger.Trigger(1, Worker(), 2, "killed-projectile", "later-death", true);
            Assert.AreEqual("multiple-trigger-observations", ledger.Increment(1, Worker(), 20).Cause);
        }

        [TestMethod]
        public void UnitHistoryCapCountsDroppedRecordsAndResetClearsIt()
        {
            var ledger = new WorkerCauseLedger(); ledger.Reset(1);
            for (int i = 1; i <= WorkerCauseLedger.Limit; i++)
                Assert.IsNotNull(ledger.Trigger(1, Worker(i, (uint)i), 0, "killed-melee", "source", true));
            Assert.IsNull(ledger.Trigger(1, Worker(WorkerCauseLedger.Limit + 1), 0, "killed-melee", "source", true));
            Assert.AreEqual(1L, ledger.Omitted);
            ledger.Reset(2); Assert.AreEqual(0L, ledger.Omitted);
            // Hut capacity is independent: keep one worker identity and change only the
            // actual native counter destination. A bad link remains an explicit conflict.
            for (int i = 1; i <= WorkerCauseLedger.Limit; i++)
                ledger.Increment(2, Worker(building: Hut(i, (uint)i), linkedGlobal: (uint)i), 10);
            Assert.AreEqual(0L, ledger.Omitted);
            var extra = Hut(WorkerCauseLedger.Limit + 1, 99999);
            ledger.Increment(2, Worker(building: extra), 10);
            Assert.AreEqual(1L, ledger.Omitted);
            StringAssert.Contains(ledger.Describe(extra), "unobserved");
        }

        [TestMethod]
        public void ManyAlternatingHutsCannotConsumeTheCauseAndFinalBudgets()
        {
            var budget = new EconomyLogBudget();
            string line = new string('x', 1024);
            for (int i = 0; i < 50000; i++)
            {
                var op = new EconomyOperation(i, 1, "wood-counter-retirement", 3, 100, null) { ArgumentA = i % 30 + 1 };
                Assert.IsFalse(EconomyDecisionEvidence.EmitGenericResult(op.Kind));
            }
            while (budget.Accept("DECISION_RESULT", line)) { }
            Assert.IsTrue(budget.Accept("WORKER_DEATH", line));
            while (budget.Accept("WORKER_COUNTER_INCREMENT", line)) { }
            Assert.IsTrue(budget.Accept("FINAL_SUMMARY", "complete final totals"));
            Assert.IsTrue(budget.Bytes <= EconomyLogBudget.ReservedBytes);
            Assert.AreEqual(budget.Bytes, budget.RoutineBytes + budget.CauseBytes + budget.FinalBytes);
            var a = new EconomyOperation(1, 1, "build-resources", 3, 100, null) { ArgumentA = 1 };
            var b = new EconomyOperation(2, 1, "build-resources", 3, 100, null) { ArgumentA = 2 };
            Assert.AreNotEqual(EconomyDecisionEvidence.DecisionKey(a), EconomyDecisionEvidence.DecisionKey(b));
        }

        [TestMethod]
        public void ProductionEndRetainsFinalSummariesAfterBothOtherBudgetsAreFullAndResetsHistory()
        {
            var owner = RuntimeFixture.Create<EconomyDiagnostics>();
            var output = new ConcurrentQueue<string>(); var budget = new EconomyLogBudget();
            var workers = new WorkerCauseLedger(); workers.Reset(1);
            workers.Trigger(1, Worker(), 1, "killed-melee", "source", true);
            RuntimeFixture.Set(owner, "output", output); RuntimeFixture.Set(owner, "budget", budget);
            RuntimeFixture.Set(owner, "ledger", new EconomyLedger()); RuntimeFixture.Set(owner, "workerCauses", workers);
            var removals = new RemovalEvidenceLedger(); removals.Reset(1); RuntimeFixture.Set(owner, "removals", removals);
            RuntimeFixture.Set(owner, "active", true); RuntimeFixture.Set(owner, "session", 1L);
            string line = new string('x', 1024);
            while (budget.Accept("DECISION_RESULT", line)) { }
            while (budget.Accept("WORKER_DEATH", line)) { }
            owner.End();
            Assert.AreEqual(8, output.Count(s => s.Contains("AI_ECONOMY_SUMMARY:") && s.Contains("phase=end")));
            Assert.IsTrue(output.Any(s => s.StartsWith("AI_ECONOMY_BUDGET:")));
            Assert.AreEqual(0L, workers.Session);
            owner.Start(2, false, true); Assert.AreEqual(2L, workers.Session);
            Assert.IsNull(workers.Increment(1, Worker(), 10));
        }

        [TestMethod]
        public void InstalledUnitLayoutAndNativeOffsetSourcesAreChecked()
        {
            EconomyDiagnostics.ValidateWorkerLayout();
            Assert.AreEqual(7, WorkerCauseNativeHooks.UnitIdFromOffsets(true, 7 * 0x490, 100));
            Assert.AreEqual(9, WorkerCauseNativeHooks.UnitIdFromOffsets(false, 100, 9 * 0x490));
            Assert.ThrowsExactly<InvalidOperationException>(() => WorkerCauseNativeHooks.UnitIdFromOffsets(true, 7 * 0x490 + 1, 0));
        }
    }
}
