using Microsoft.VisualStudio.TestTools.UnitTesting;
using APIShared;
using System.Collections;
using System.Reflection;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public sealed class RuntimeStateTests
    {
        [TestMethod]
        public void GeneralTickRunsWithoutAnyPassiveObserverAndLoadingRetiresItsState()
        {
            var runtime = RuntimeFixture.Create<AIBuildDiagnoseRuntime>();
            runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext(RuntimeFixture.Notification(100)));
            RuntimeFixture.Set(runtime, "aivWallPlanCaptured", true);
            RuntimeFixture.Set(runtime, "nextWallMapTick", int.MaxValue);
            RuntimeFixture.Set(runtime, "nextSummaryTick", int.MaxValue);
            RuntimeFixture.Set(runtime, "firstTick", true);
            runtime.OnTick(42);
            Assert.AreEqual(42, RuntimeFixture.Get(runtime, "lastTick"));
            runtime.OnNativeRecord(new AiBuildDiagnosticRecord("farm-search-before", 2, 0, 17, 0, 0, 0));
            Assert.IsTrue(((bool[])RuntimeFixture.Get(runtime, "farmSearchInCall"))[2]);
            typeof(AIBuildDiagnoseRuntime).GetMethod("OnMapLoading", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(runtime, new object[] { RuntimeFixture.Notification(101, MissionInitializationPhase.BeforeLoad) });
            Assert.IsFalse((bool)RuntimeFixture.Get(runtime, "active"));
            runtime.OnTick(99);
            Assert.AreEqual(42, RuntimeFixture.Get(runtime, "lastTick"));
            runtime.OnSessionStarted(new Shared.GameplaySessionStartedContext(RuntimeFixture.Notification(102)));
            Assert.IsFalse(((bool[])RuntimeFixture.Get(runtime, "farmSearchInCall"))[2]);
            Assert.AreEqual(0, ((IDictionary)RuntimeFixture.Get(runtime, "seen")).Count);
            Assert.AreEqual(102L, RuntimeFixture.Get(runtime, "sessionId"));
        }
    }
}
