using AIBuildDiagnoseTest;
using AIBuildDiagnoseTest.Tests;
using CanarariWoodTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using APIShared;
using System.Collections;
using System.Reflection;

namespace CanarariWoodTest.Tests
{
    [TestClass]
    public sealed class PassiveStateTests
    {
        [TestMethod]
        public void PassiveObservationDoesNotRequireAnExperimentAndLoadingClearsEarlyState()
        {
            var passive = RuntimeFixture.Create<CanariPassiveDiagnostics>();
            passive.OnSessionStarted(new Shared.GameplaySessionStartedContext(RuntimeFixture.Notification(200)));
            passive.OnNativeRecord(new AiBuildDiagnosticRecord("wood-build-before", 2, 123, 0, 0, 0, 0));
            Assert.AreEqual(1, ((IDictionary)RuntimeFixture.Get(passive, "attempts")).Count);
            passive.OnNativeRecord(new AiBuildDiagnosticRecord("coarse-audit-session", 0, 0, 0, 1, 200, 0));
            typeof(CanariPassiveDiagnostics).GetMethod("OnMapLoading", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(passive, new object[] { RuntimeFixture.Notification(201, MissionInitializationPhase.BeforeLoad) });
            Assert.IsFalse((bool)RuntimeFixture.Get(passive, "active"));
            Assert.AreEqual(0, ((IDictionary)RuntimeFixture.Get(passive, "attempts")).Count);
            Assert.AreEqual(0UL, RuntimeFixture.Get(passive, "gridState"));
            Assert.AreEqual(0, ((IList)RuntimeFixture.Get(passive, "earlyGridHistory")).Count);
            passive.OnSessionEnded(); // Failed load: no successful start is required.
            passive.OnSessionStarted(new Shared.GameplaySessionStartedContext(RuntimeFixture.Notification(202)));
            Assert.AreEqual(0, ((IDictionary)RuntimeFixture.Get(passive, "attempts")).Count);
            Assert.AreEqual(202L, RuntimeFixture.Get(passive, "sessionId"));
        }

        [TestMethod]
        public void PassiveAndExperimentRecordsUseTheSameSpecialBudgetAndKeepFinalAccounting()
        {
            var passive = RuntimeFixture.Create<CanariPassiveDiagnostics>();
            const int budget = 56 * 1024 * 1024;
            RuntimeFixture.Set(passive, "writtenBytes", budget - 50);
            passive.Log("passive event");
            var experiment = RuntimeFixture.Create<CanarariWoodRuntime>();
            RuntimeFixture.Set(experiment, "diagnosticWriter", new System.Action<string>(passive.Log));
            typeof(CanarariWoodRuntime).GetMethod("Log", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(experiment, new object[] { "experiment event" });
            Assert.AreEqual(2, RuntimeFixture.Get(passive, "droppedLines"));
            Assert.AreEqual(budget - 50, RuntimeFixture.Get(passive, "writtenBytes"));
            passive.Log("AI_BUILD_SPECIAL_BUDGET: final accounting");
            Assert.IsTrue((int)RuntimeFixture.Get(passive, "writtenBytes") > budget - 50);
        }
    }
}
