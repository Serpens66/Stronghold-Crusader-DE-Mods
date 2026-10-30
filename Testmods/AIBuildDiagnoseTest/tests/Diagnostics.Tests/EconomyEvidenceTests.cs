using System;
using System.Reflection;
using AIBuildDiagnoseTest;
using BugfixesAndQoL.Diagnostics;
using BugfixesAndQoL;
using SpectatorEditorBuildTest;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass, DoNotParallelize]
    public class EconomyEvidenceTests
    {
        [TestMethod]
        public void ReleaseFixesCompatibilityRemovesOnlyTheControllerContract()
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => EconomyHookContract.Find(0x3F270));
            foreach (int rva in new[] { 0x3EC90, 0x29650, 0x29700, 0x40740 })
                Assert.AreEqual(rva, EconomyHookContract.Find(rva).Rva);
        }
        [TestMethod]
        public void UnobservedControllerCannotBecomeAZeroCallSummaryFinding()
        {
            CollectionAssert.DoesNotContain(EconomyDiagnostics.SummaryStages, "trade-controller");
            foreach (string stage in new[] { "purchase-decision", "ai-buy", "ai-sell" })
                CollectionAssert.Contains(EconomyDiagnostics.SummaryStages, stage);
        }
        [TestMethod]
        public void ControllerCoverageExplainsUnknownDecisionsAndWarns()
        {
            string detail = EconomyDiagnostics.TradeControllerCoverageDetail;
            foreach (string boundary in new[] { "market-gate=unknown", "buy-sell-alternation=unknown", "aborted-sale-decisions=unknown", "not-evidence-of-no-trade" })
                StringAssert.Contains(detail, boundary);
            Assert.AreEqual(BepInEx.Logging.LogLevel.Warning, EconomyDiagnostics.GetLogLevel(
                "AI_ECONOMY_CAPABILITY: feature=trade-controller, available=False, status=unsupported, detail=" + detail));
        }
        [TestMethod]
        public void InstalledInteropMatchesEverySnapshotRootAndIdentityLayout() => EconomyDiagnostics.ValidateLayout();
        [TestMethod]
        public void DecisionsAndTradesDoNotTurnMissingEvidenceIntoSuccess()
        {
            Assert.AreEqual("native-first-predicate:raw_3505470<1", EconomyDecisionEvidence.Reason("wood-eligibility", 0, 0));
            StringAssert.Contains(EconomyDecisionEvidence.Reason("farm-eligibility", 0, null), "subcondition=unknown");
            Assert.AreEqual("native-resource-check-rejected", EconomyDecisionEvidence.Reason("build-resources", 0, null));
            Assert.AreEqual("native-result-unavailable", EconomyDecisionEvidence.Reason("search", null, null));
            Assert.AreEqual("purchase-returned-failure", EconomyDecisionEvidence.TradeOutcome("ai-buy", 0, 3, 3));
            Assert.AreEqual("purchase-and-stock-increase-observed", EconomyDecisionEvidence.TradeOutcome("ai-buy", 1, 3, 23));
            Assert.AreEqual("sale-and-stock-decrease-observed", EconomyDecisionEvidence.TradeOutcome("ai-sell", null, 23, 3));
            Assert.AreEqual("transaction-return-without-confirmed-stock-change", EconomyDecisionEvidence.TradeOutcome("price-query", null, 3, 3));
        }
        [TestMethod]
        public void PriceTaxAndSpawnNotificationsAreNotBookingContexts()
        {
            foreach (string notification in new[] { "price-query", "tax-event", "donation-event", "unit-spawn", "resource-add", "resource-subtract", "market-action", "purchase-decision", "build-resources" })
                Assert.IsFalse(EconomyDecisionEvidence.HasMeasuredBookingContext(notification), notification);
            foreach (string measured in new[] { "ai-buy", "ai-sell", "tax-settlement", "ai-recruitment", "build", "refund" })
                Assert.IsTrue(EconomyDecisionEvidence.HasMeasuredBookingContext(measured), measured);
        }
        [TestMethod]
        public void LedgerKeepsNegativeGoldAndUnattributedChangesAndResetsSessions()
        {
            var ledger = new EconomyLedger();
            StringAssert.Contains(ledger.Reconcile(1, 15), "baselineOnly=true");
            ledger.Book(1, -20);
            StringAssert.Contains(ledger.Reconcile(1, -300), "unattributedDelta=-295");
            StringAssert.Contains(ledger.Reconcile(1, 0), "attributedDelta=0, unattributedDelta=300");
            ledger.Reset();
            StringAssert.Contains(ledger.Reconcile(1, 0), "baselineOnly=true");
        }
        [TestMethod]
        public void NestedTransactionsDoNotDoubleBookTheirGoldChanges()
        {
            var build = new EconomyOperation(1, 1, "build", 2, 100, null);
            var buy = new EconomyOperation(2, 1, "ai-buy", 2, 100, build);
            Assert.AreEqual(-20L, buy.ExclusiveDelta(80)); buy.Close(80);
            Assert.AreEqual(-10L, build.ExclusiveDelta(70));
            var otherPlayer = new EconomyOperation(3, 1, "build", 3, 200, build);
            var inner = new EconomyOperation(4, 1, "ai-buy", 2, 70, otherPlayer);
            Assert.AreSame(build, otherPlayer.ForPlayer(2));
            Assert.AreSame(otherPlayer, inner.ForPlayer(3));
            Assert.IsNull(inner.ForPlayer(4));
            inner.Close(60);
            Assert.AreEqual(-10L, build.ExclusiveDelta(60));
        }
        [TestMethod]
        public void RetiredThreadContextCannotCorrelateOrBookIntoTheNextSession()
        {
            var old = new EconomyOperation(1, 11, "build", 2, 100, null);
            var next = new EconomyOperation(1, 12, "ai-buy", 3, 100, old);
            Assert.IsNull(old.ForSession(12)); Assert.AreSame(next, next.ForSession(12));
            Assert.IsNull(next.ForPlayer(2));
            var samePlayer = new EconomyOperation(2, 12, "ai-buy", 2, 100, old);
            samePlayer.Close(80);
            Assert.AreEqual(0L, old.ChildDelta);
        }
        [TestMethod]
        public void IdentityRequiresBothSlotAndGlobalId()
        {
            var id = new BuildingEvidenceIdentity(3, 42, 1, 0, 10, 20, 2);
            Assert.IsTrue(id.Matches(3, 42));
            Assert.IsFalse(id.Matches(3, 43));
            Assert.IsFalse(id.Matches(4, 42));
            Assert.IsFalse(BuildingEvidenceIdentity.IsRemoved(id, null), "Unavailable lookup is not removal evidence");
            Assert.IsFalse(BuildingEvidenceIdentity.IsRemoved(id, new BuildingEvidenceIdentity(3, 42, 1, 0, 10, 20, (int)AliveState.MarkedForDeletion)));
            Assert.IsTrue(BuildingEvidenceIdentity.IsRemoved(id, new BuildingEvidenceIdentity(3, 43, 1, 0, 10, 20, (int)AliveState.IsAlive)));
            Assert.IsTrue(BuildingEvidenceIdentity.IsRemoved(id, new BuildingEvidenceIdentity(3, 0, 0, 0, 0, 0, (int)AliveState.None)));
        }
        [TestMethod]
        public void ObservationPreservesAllExistingAccessibilityProtectionModes()
        {
            Assert.AreEqual(2, TemporaryGateBlockagePolicy.ResolveAccessibilityResult(0, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, true, true));
            Assert.AreEqual(1, TemporaryGateBlockagePolicy.ResolveAccessibilityResult(1, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, true, true));
            Assert.AreEqual(2, TemporaryGateBlockagePolicy.ResolveAccessibilityResult(1, true, eStructs.STRUCT_WOODCUTTERS_HUT, 2, false, false));
            Assert.AreEqual(0, TemporaryGateBlockagePolicy.ResolveAccessibilityResult(1, true, eStructs.STRUCT_WOODCUTTERS_HUT, 0, true, true));
            Assert.AreEqual(1, TemporaryGateBlockagePolicy.ResolveAccessibilityResult(2, true, eStructs.STRUCT_WOODCUTTERS_HUT, 0, false, false));
        }
        [TestMethod]
        public void ReservedBudgetBoundsOutputAndCountsOmissions()
        {
            var budget = new EconomyLogBudget();
            Assert.IsFalse(budget.Accept("PRICE_QUERY", new string('x', EconomyLogBudget.ReservedBytes)));
            Assert.AreEqual(1, budget.Omitted["PRICE_QUERY"]);
            Assert.IsTrue(budget.Accept("PROTECTION", "small"));
            Assert.IsTrue(budget.Bytes <= EconomyLogBudget.ReservedBytes);
            budget.Reset(); Assert.AreEqual(0L, budget.Bytes); Assert.AreEqual(0, budget.Omitted.Count);
        }
        [TestMethod]
        public void AvailabilityOwnerReportsOverridesAndIsolatesBothCallbackPhases()
        {
            var field = typeof(AvailabilityObservation).GetField("availability", BindingFlags.NonPublic | BindingFlags.Static);
            field.SetValue(null, null);
            try
            {
                Assert.IsNull(AvailabilityObservation.Begin(1, 2, 0));
                int calls = 0; bool? evaluated = null; int? observed = null;
                Assert.IsTrue(AvailabilityObservation.Register((player, mapper, feedback) =>
                    (result, vanilla) => { calls++; observed = result; evaluated = vanilla; }));
                AvailabilityObservation.Begin(1, 2, 0)?.Invoke(1, false);
                Assert.AreEqual(false, evaluated); Assert.AreEqual(1, observed);
                AvailabilityObservation.Begin(1, 2, 0)?.Invoke(0, true);
                Assert.AreEqual(true, evaluated); Assert.AreEqual(0, observed); Assert.AreEqual(2, calls);
                field.SetValue(null, null);
                long failures = AvailabilityObservation.CallbackFailures;
                AvailabilityObservation.Register((a, b, c) => throw new Exception("begin"));
                Assert.IsNull(AvailabilityObservation.Begin(1, 2, 0));
                field.SetValue(null, null);
                AvailabilityObservation.Register((a, b, c) => (r, v) => throw new Exception("end"));
                AvailabilityObservation.Begin(1, 2, 0)?.Invoke(null, true);
                Assert.AreEqual(failures + 2, AvailabilityObservation.CallbackFailures);
            }
            finally { field.SetValue(null, null); }
        }
        [TestMethod]
        public void TestsUseTheUnchangedInstalledBugfixObservationContract()
        {
            // Installed Bugfix from the declared minimum (info.json/BepInDependency) on, as long as
            // it keeps the observed sink contract and has no newer per-decision economy publisher.
            Assembly bugfix = typeof(AiBuildObservation).Assembly;
            Assert.IsTrue(bugfix.GetName().Version >= new Version(1, 0, 192, 0), bugfix.GetName().Version.ToString());
            StringAssert.Contains(bugfix.Location, "BugfixesAndQoL.dll");
            Type sink = bugfix.GetType("BugfixesAndQoL.Diagnostics.IAiBuildDiagnosticSink", throwOnError: true);
            Assert.AreEqual(9, sink.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
            Assert.IsNull(bugfix.GetType("BugfixesAndQoL.Diagnostics.AiEconomyObservation"));
        }
    }
}
