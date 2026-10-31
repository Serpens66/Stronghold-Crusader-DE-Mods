using System;
using System.Threading;
using AIBuildDiagnoseTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;

namespace AIWoodSafetyTest.Tests
{
    [TestClass, DoNotParallelize]
    public sealed class RuntimeTests
    {
        private static WoodSafetyRuntime Ready(long session = 1)
        {
            var r = new WoodSafetyRuntime(null); r.BeginSession(session, true, true, true);
            r.Masks.SetRange(54); r.Masks.Update(1, new Fortification(1, 1, 2, eStructs.STRUCT_TOWER1, new SafetyRect(100, 100, 10), true));
            return r;
        }
        [TestMethod]
        public void NestedWoodContextsRestoreEnemiesEvenWhenInnerBodyThrows()
        {
            var r = Ready(); var outer = r.EnterPreparedBuild(3, 1 << 2);
            Assert.IsTrue(r.RejectCandidate(22, 22));
            try { var inner = r.EnterPreparedBuild(2, 0); try { Assert.IsFalse(r.RejectCandidate(22, 22)); throw new Exception(); } finally { inner(); } }
            catch (Exception) { }
            Assert.IsTrue(r.RejectCandidate(22, 22)); outer(); outer(); Assert.IsFalse(r.RejectCandidate(22, 22)); r.End();
        }
        [TestMethod]
        public void OldFinallyCannotUnlinkReplacementSessionAndFailedLoadClearsEverything()
        {
            var r = Ready(); var old = r.EnterPreparedBuild(3, 1 << 2);
            r.End(); r.BeginSession(2, true, true, true);
            r.Masks.SetRange(54); r.Masks.Update(1, new Fortification(1, 2, 2, eStructs.STRUCT_TOWER1, new SafetyRect(100, 100, 10), true));
            var replacement = r.EnterPreparedBuild(3, 1 << 2); old(); Assert.IsTrue(r.RejectCandidate(22, 22));
            r.End(); Assert.AreEqual(0, r.Masks.RecordCount); Assert.IsFalse(r.RejectCandidate(22, 22)); replacement();
            r.BeginSession(3, false, true, true); Assert.IsNull(r.EnterPreparedBuild(3, 4)); r.End();
        }
        [TestMethod]
        public void DisabledBuildRuleAndObserverFaultOnlyDisableTheirOwnRule()
        {
            var r = Ready(); var frame = r.EnterPreparedBuild(3, 4);
            r.BuildFailure(new Exception("observer")); Assert.IsFalse(r.RejectCandidate(22, 22)); frame();
            r.BeginSession(2, true, false, true); Assert.IsNull(r.EnterPreparedBuild(3, 4)); r.End();
        }
        [TestMethod]
        public void LayoutFailureDisablesOnlyItsRuleAndRemainsDisabledAcrossSessions()
        {
            WoodSafetyBridge.SetCounterReady(true);
            try
            {
                var buildBad = new WoodSafetyRuntime(null);
                buildBad.ValidateCapabilities(() => throw new Exception("footprint"), () => { });
                buildBad.BeginSession(1, true, true, true);
                Assert.IsNull(buildBad.EnterPreparedBuild(3, 4)); Assert.IsTrue(buildBad.DeathRuleActive);
                buildBad.BuildFailure(new Exception()); Assert.IsTrue(buildBad.DeathRuleActive); buildBad.End();
                var counterBad = Ready(); counterBad.End();
                counterBad.ValidateCapabilities(() => { }, () => throw new Exception("counter"));
                counterBad.BeginSession(2, true, true, true);
                Assert.IsFalse(counterBad.DeathRuleActive); var scope = counterBad.EnterPreparedBuild(3, 0); Assert.IsNotNull(scope); scope(); counterBad.End();
                counterBad.BeginSession(3, true, true, true); Assert.IsFalse(counterBad.DeathRuleActive); counterBad.End();
            }
            finally { WoodSafetyBridge.SetCounterReady(false); }
        }
        [TestMethod]
        public void FixedAivPlacementVetoIsPostOnlyAiWoodOnlyAndNeverForcesAnAllowedPlacement()
        {
            var r = Ready(); int writes = 0;
            Action block = () => writes++;
            BuildingPlacementValidationEventArgs Args(EventHookPhase phase, eMappers mapper, int x = 110) =>
                new BuildingPlacementValidationEventArgs(phase, 3, x, 110, mapper, 0, 0);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Pre, eMappers.MAPPER_WOODSMAN), 4, true, block); Assert.AreEqual(0, writes);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_WOODSMAN), 4, false, block); Assert.AreEqual(0, writes);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_KEEP1), 4, true, block); Assert.AreEqual(0, writes);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_WOODSMAN), 0, true, block); Assert.AreEqual(0, writes);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_WOODSMAN, 600), 4, true, block); Assert.AreEqual(0, writes);
            r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_WOODSMAN), 4, true, block); Assert.AreEqual(1, writes);
            r.End(); r.ApplyPreparedPlacement(Args(EventHookPhase.Post, eMappers.MAPPER_WOODSMAN), 4, true, block); Assert.AreEqual(1, writes);
        }
        [TestMethod]
        public void ContextIsThreadLocalAndConcurrentSearchCannotOverwriteNativeGate()
        {
            var r = Ready(); var frame = r.EnterPreparedBuild(3, 4); bool rejected = true, refused = false;
            var t = new Thread(() => { rejected = r.RejectCandidate(22, 22); try { r.EnterPreparedBuild(4, 4); } catch (InvalidOperationException) { refused = true; } });
            t.Start(); Assert.IsTrue(t.Join(3000)); Assert.IsFalse(rejected); Assert.IsTrue(refused);
            Assert.IsTrue(r.RejectCandidate(22, 22)); frame(); r.End();
        }
        private sealed class FailingConsumer : IWoodSafetyIntervention
        {
            internal int Entered, Exited; internal bool Fail;
            public Action EnterWoodBuild(int player) { Entered++; if (Fail) throw new Exception(); return () => { Exited++; if (Fail) throw new Exception(); }; }
            public WoodCounterCorrection CorrectDeathCounter(WoodCounterPoint point) { if (Fail) throw new Exception(); return default; }
        }
        [TestMethod]
        public void PrivateBridgeHasOneOwnerAndFaultsNeverEscapeOriginalCallBoundary()
        {
            Assert.IsNull(WoodSafetyBridge.EnterWoodBuild(3));
            Assert.IsFalse(WoodSafetyBridge.CorrectDeathCounter(default).Applied);
            var observer = new FailingConsumer();
            Assert.IsFalse(WoodSafetyBridge.TryRegister("other", observer, out _));
            Assert.IsTrue(WoodSafetyBridge.TryRegister("AIWoodSafetyTest_Serp", observer, out _));
            Assert.IsFalse(WoodSafetyBridge.TryRegister("AIWoodSafetyTest_Serp", new FailingConsumer(), out _));
            var restore = WoodSafetyBridge.EnterWoodBuild(3); int vanillaCalls = 0;
            observer.Fail = true;
            try { vanillaCalls++; WoodSafetyBridge.CorrectDeathCounter(default); } finally { WoodSafetyBridge.ExitWoodBuild(restore); }
            Assert.IsNull(WoodSafetyBridge.EnterWoodBuild(3));
            Assert.AreEqual(1, vanillaCalls); Assert.AreEqual(1, observer.Exited); Assert.AreEqual(3L, WoodSafetyBridge.CallbackFailures);
            observer.Fail = false;
        }
    }
}
