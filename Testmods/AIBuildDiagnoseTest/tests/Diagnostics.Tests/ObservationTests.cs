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
            Assert.AreEqual("unavailable", AiBuildDiagnostic.CaptureEconomyGridEvidence(0UL, -1).Status);
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
            Assert.IsTrue(AiBuildDiagnostic.TryRegisterWoodBuildGate(AIBuildDiagnosePluginGuid,
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
        private const string AIBuildDiagnosePluginGuid = "AIBuildDiagnoseTest_Serp";
    }
}
