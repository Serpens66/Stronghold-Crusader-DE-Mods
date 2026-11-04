using AIBuildDiagnoseTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Reflection;

namespace CanarariWoodTest.Tests
{
    [TestClass]
    public sealed class NearbyClassificationTests
    {
        private static string Classify(AiNearbyPathEvidence before, AiNearbyPathEvidence after)
        {
            var runtime = typeof(CanarariWoodTest.CanariPassiveDiagnostics);
            var attemptType = runtime.GetNestedType("Attempt", BindingFlags.NonPublic);
            var attempt = Activator.CreateInstance(attemptType, BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { 6 }, null);
            attemptType.GetField("NearbyBefore", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attempt, before);
            attemptType.GetField("NearbyAfter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(attempt, after);
            return (string)runtime.GetMethod("AnalyzeNearby", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new[] { attempt });
        }
        private static AiNearbyPathEvidence Snapshot(int native, int api, bool after)
        {
            var anchor = new AiPathTileSample(345, 485, 221520, native, api, 0, 0, 0, 0, "ok");
            return new AiNearbyPathEvidence("ok", 69, 98, after ? 69 : -1, after ? 97 : -1,
                new[] { anchor }, null, null, null, null);
        }
        [TestMethod]
        public void DistinguishesStableZeroPositiveChangesAndUnavailableEvidence()
        {
            Assert.AreEqual("vanilla-coarse-search-selected-zero-component-anchor", Classify(Snapshot(0, 0, false), Snapshot(0, 0, true)));
            Assert.AreEqual("vanilla-coarse-search-selected-positive-component-anchor", Classify(Snapshot(3, 3, false), Snapshot(3, 3, true)));
            Assert.AreEqual("component-changed-during-nearby-search", Classify(Snapshot(3, 3, false), Snapshot(0, 0, true)));
            Assert.AreEqual("native-api-component-view-divergence", Classify(Snapshot(3, 0, false), Snapshot(3, 0, true)));
            Assert.AreEqual("nearby-snapshot-unobserved", Classify(null, null));
        }
    }
}
