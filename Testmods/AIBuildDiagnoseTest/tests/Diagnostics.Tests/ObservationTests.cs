using AIBuildDiagnoseTest;
using BugfixesAndQoL.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass, DoNotParallelize]
    public class ObservationTests
    {
        // This fixture never installs a native hook. Reset only the unpublished test registry
        // and thread-local attempt context between cases; production has no teardown API.
        [TestInitialize]
        public void Initialize() => ResetUnpublishedFixture();
        [TestCleanup]
        public void Cleanup() => ResetUnpublishedFixture();
        private static void ResetUnpublishedFixture()
        {
            foreach (string field in new[] { "observer", "woodBuildGate", "nearbyWoodOverlay", "woodAttempts" })
                typeof(AiBuildDiagnostic).GetField(field, BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            typeof(AiBuildExperimentBridge).GetField("observer", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            typeof(AiBuildExperimentBridge).GetField("capture", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            typeof(AiBuildExperimentBridge).GetField("wallObserver", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
            typeof(AiBuildObservation).GetField("sink", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        }
        private static void Observe(Action<AiBuildDiagnosticRecord> observer)
        {
            typeof(AiBuildDiagnostic).GetField("observer", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, observer);
            Assert.IsTrue(AiBuildObservation.TryRegister(AiBuildObservationSink.Instance, null, out _));
        }
        [TestMethod]
        public void NoTestModMeansNoGateAttemptSnapshotOrNativeHook()
        {
            Assert.IsFalse(AiBuildObservation.HasObserver);
            Assert.IsFalse(AiBuildObservation.ShouldDeferWoodBuild(6));
            Assert.AreEqual(0L, AiBuildObservation.BeginWoodAttempt(6));
            Assert.IsFalse(AiBuildObservation.TryGetCurrentWoodAttempt(out long id, out int player));
            Assert.AreEqual(0L, id); Assert.AreEqual(0, player);
            Assert.IsFalse(AiBuildDiagnostic.SchedulerReady);
            Assert.IsFalse(AiBuildDiagnostic.RouteReady);
            AiEconomyGridEvidence unavailable = AiBuildDiagnostic.CaptureEconomyGridEvidence(0UL, -1);
            Assert.AreEqual("unavailable", unavailable.Status);
            Assert.AreEqual(-1, unavailable.CoarseX);
            Assert.AreEqual(-1, unavailable.CoarseY);
            AiBuildObservation.Publish("unused", 6);
            AiBuildObservation.EndWoodAttempt(0);
        }
        [TestMethod]
        public void RegistrationRejectsNullAndConflictsButAcceptsSameSink()
        {
            Assert.IsFalse(AiBuildObservation.TryRegister(null, null, out _));
            Assert.IsTrue(AiBuildObservation.TryRegister(AiBuildObservationSink.Instance, null, out _));
            Assert.IsTrue(AiBuildObservation.TryRegister(AiBuildObservationSink.Instance, null, out _));
            Assert.IsFalse(AiBuildObservation.TryRegister(new AiBuildObservationSink(), null, out string error));
            Assert.IsFalse(string.IsNullOrEmpty(error));
        }
        [TestMethod]
        public void AttemptsNestAndPublishedValuesKeepPlayerAndAttemptAttribution()
        {
            var records = new List<AiBuildDiagnosticRecord>();
            Observe(records.Add);
            Assert.AreEqual(0L, AiBuildObservation.BeginWoodAttempt(0));
            Assert.AreEqual(0L, AiBuildObservation.BeginWoodAttempt(9));
            long outer = AiBuildObservation.BeginWoodAttempt(6);
            long inner = AiBuildObservation.BeginWoodAttempt(2);
            Assert.IsTrue(inner > outer && outer > 0);
            AiBuildObservation.Publish("inner", 2, 10, 20, 30, 40);
            AiBuildObservation.Publish("unattributed", 6);
            AiBuildObservation.EndWoodAttempt(inner);
            AiBuildObservation.Publish("outer", 6);
            AiBuildObservation.EndWoodAttempt(outer);
            Assert.IsFalse(AiBuildObservation.TryGetCurrentWoodAttempt(out _, out _));
            Assert.AreEqual(inner, records[0].AttemptId);
            Assert.AreEqual(10L, records[0].A); Assert.AreEqual(40L, records[0].D);
            Assert.AreEqual(0L, records[1].AttemptId);
            Assert.AreEqual(outer, records[2].AttemptId);
            Assert.AreEqual(6, records[2].PlayerId);
        }
        [TestMethod]
        public void MismatchedAttemptEndClearsAttribution()
        {
            Observe(_ => { });
            long outer = AiBuildObservation.BeginWoodAttempt(6);
            AiBuildObservation.BeginWoodAttempt(2);
            AiBuildObservation.EndWoodAttempt(outer);
            Assert.IsFalse(AiBuildObservation.TryGetCurrentWoodAttempt(out _, out _));
        }
        [TestMethod]
        public void ObserverAndGateFailuresDoNotEscapeOrBlockVanilla()
        {
            Observe(_ => throw new InvalidOperationException("observer"));
            Assert.IsTrue(AiBuildDiagnostic.TryRegisterWoodBuildGate("CanarariWoodTest_Serp",
                _ => throw new InvalidOperationException("gate"), out _));
            AiBuildObservation.Publish("failure", 6);
            Assert.IsFalse(AiBuildObservation.ShouldDeferWoodBuild(6));
        }
        [TestMethod]
        public void RestoreRunsOnceEvenWithoutAnObserverAndExceptionsAreIsolated()
        {
            int restored = 0;
            AiBuildObservation.EndNearbyWoodObservation(() => restored++, 0, 6, 1, 2);
            Assert.AreEqual(1, restored);
            Observe(_ => { });
            AiBuildObservation.EndNearbyWoodObservation(() => restored++, 0, 6, 1, 2);
            Assert.AreEqual(2, restored);
            AiBuildObservation.EndNearbyWoodObservation(() => throw new InvalidOperationException("restore"), 0, 6, 1, 2);
        }
        [TestMethod]
        public void DiagnosticsRequireTheirTestOwnerAndAValidatedModule()
        {
            Assert.IsFalse(AiBuildDiagnostic.TryRegister("Other.Mod", _ => { }, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegister(AIBuildDiagnosePluginGuid, _ => { }, out _));
            Assert.IsFalse(AiBuildDiagnostic.HasObserver);
            Observe(_ => { });
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterWoodBuildGate("Other.Mod", _ => true, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterNearbyWoodOverlay("Other.Mod", (s, p, x, y) => null, out _));
        }
        [TestMethod]
        public void CompanionIsOptionalExclusiveAndCannotClaimGeneralObserverOwnership()
        {
            Action<AiBuildDiagnosticRecord> callback = _ => { };
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Observe(_ => { });
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister(AIBuildDiagnosePluginGuid, callback, out _));
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsFalse(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ => { }, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegister("CanarariWoodTest_Serp", callback, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterWoodBuildGate(AIBuildDiagnosePluginGuid, _ => true, out _));
            Assert.IsFalse(AiBuildDiagnostic.TryRegisterNearbyWoodOverlay(AIBuildDiagnosePluginGuid, (s,p,x,y) => null, out _));
        }
        [TestMethod]
        public void CompanionDeliveryIsIndependentAndContainsExceptions()
        {
            Observe(_ => { });
            int calls = 0;
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ =>
                { calls++; throw new InvalidOperationException("companion"); }, out _));
            AiBuildExperimentBridge.Deliver(new AiBuildDiagnosticRecord("test", 2, 0, 0, 0, 0, 0), null);
            AiBuildExperimentBridge.Deliver(new AiBuildDiagnosticRecord("test", 3, 0, 0, 0, 0, 0), null);
            Assert.AreEqual(2, calls);
            Assert.IsFalse(AiBuildDiagnostic.ShouldDeferWoodBuild(2));
        }
        private const string AIBuildDiagnosePluginGuid = "AIBuildDiagnoseTest_Serp";

        [TestMethod]
        public void GeneralObservationWithoutCompanionDoesNotCaptureSpecialSnapshots()
        {
            var records = new List<AiBuildDiagnosticRecord>();
            Observe(records.Add);
            long attempt = AiBuildDiagnostic.BeginWoodAttempt(6);
            Assert.IsFalse(AiBuildExperimentBridge.HasDetails);
            // Deliberately non-dereferenceable: absent demand must return before a read.
            AiBuildDiagnostic.PublishEconomyGridEvidence("economy-grid-before", 1, 0);
            AiBuildDiagnostic.PublishNearbyPathEvidence("wood-nearby-path-before", 6, 1, 2, 3, -1, -1);
            Assert.AreEqual(0, records.Count);
            AiBuildDiagnostic.Publish("route-result", 6, 0, 3, 10, 15);
            Assert.AreEqual(1, records.Count);
            Assert.AreEqual(attempt, records[0].AttemptId);
            AiBuildDiagnostic.EndWoodAttempt(attempt);
        }

        [TestMethod]
        public void GeneralAndCompanionFailuresAreIndependentInProductionDispatch()
        {
            Observe(_ => { });
            int general = 0, companion = 0;
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ =>
                { companion++; throw new InvalidOperationException("companion"); }, out _));
            var record = new AiBuildDiagnosticRecord("scheduler-before", 2, 0, 0, 0, 0, 0);
            AiBuildExperimentBridge.Dispatch(record, _ =>
                { general++; throw new InvalidOperationException("general"); }, null);
            AiBuildExperimentBridge.Dispatch(record, _ => general++, null);
            Assert.AreEqual(2, general);
            Assert.AreEqual(2, companion);
        }

        [TestMethod]
        public void DetailRegistrationDoesNotEnableAnyMutationGate()
        {
            var records = new List<AiBuildDiagnosticRecord>();
            Observe(records.Add);
            Assert.IsTrue(AiBuildExperimentBridge.TryRegister("CanarariWoodTest_Serp", _ => { }, out _));
            var capture = new DetailCapture();
            Assert.IsTrue(AiBuildExperimentBridge.TryRegisterDetails("CanarariWoodTest_Serp", capture, _ => { }, out _));
            Assert.IsTrue(AiBuildExperimentBridge.HasDetails);
            AiBuildDiagnostic.PublishEconomyGridEvidence("economy-grid-before", 1, 0);
            long attempt = AiBuildDiagnostic.BeginWoodAttempt(2);
            AiBuildDiagnostic.PublishNearbyPathEvidence("wood-nearby-path-before", 2, 1, 2, 3, -1, -1);
            AiBuildDiagnostic.EndWoodAttempt(attempt);
            Assert.AreEqual(2, capture.Calls);
            Assert.AreEqual(2, records.Count);
            Assert.IsFalse(AiBuildDiagnostic.ShouldDeferWoodBuild(2));
        }

        private sealed class DetailCapture : IAiBuildDetailCapture
        {
            internal int Calls;
            public AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode)
            { Calls++; return new AiEconomyGridEvidence("fixture", state, mode, 1, 2, 0, 0, 0, null); }
            public AiEconomyGridEvidence CaptureEconomyGridEvidence(int x, int y) => CaptureEconomyGridEvidence(0, -1);
            public AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode, int x, int y) => CaptureEconomyGridEvidence(state, mode);
            public AiNearbyPathEvidence CaptureNearbyPathEvidence(ulong state, int x, int y, int rx, int ry, bool before)
            { Calls++; return new AiNearbyPathEvidence("fixture", x, y, rx, ry, null, null, null, null, null); }
            public AiRouteEvidence CaptureRouteEvidence(int player, int mapper, int x, int y) => null;
            public IReadOnlyList<AiPathTileSample> CaptureTiles(int x, int y, int w, int h) => Array.Empty<AiPathTileSample>();
        }
    }
}
