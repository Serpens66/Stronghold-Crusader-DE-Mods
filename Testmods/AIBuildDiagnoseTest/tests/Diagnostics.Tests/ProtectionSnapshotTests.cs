using System;
using System.Reflection;
using AIBuildDiagnoseTest;
using BepInEx.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public class ProtectionSnapshotTests
    {
        private sealed class SettingsProbe
        {
            public bool EnableMod => true;
            public bool PreventEmergencyDemolition => true;
            public bool PreventHovelDeletion => throw new InvalidOperationException("unavailable getter");
        }
        private sealed class HookProbe
        {
            public object settings = new SettingsProbe();
            public object emergencyDemolitionHook = new HandleProbe();
        }
        private sealed class HandleProbe { public bool Success => true; }
        private sealed class RuntimeProbe { public object aiEconomyProtectionHook = new HookProbe(); }
        private sealed class DestroyedPluginProbe
        {
            public object runtime = new RuntimeProbe();
            public object Settings => new SettingsProbe();
            public static bool operator ==(DestroyedPluginProbe left, DestroyedPluginProbe right) => true;
            public static bool operator !=(DestroyedPluginProbe left, DestroyedPluginProbe right) => false;
            public override bool Equals(object other) => ReferenceEquals(this, other);
            public override int GetHashCode() => 0;
        }
        [TestMethod]
        public void DestroyedUnityStyleEqualityDoesNotHideManagedRuntime()
        {
            var plugin = new DestroyedPluginProbe();
            Assert.IsTrue(plugin == null);
            string snapshot = ProtectionSnapshot.Capture(true, plugin, "1.2.3");
            StringAssert.Contains(snapshot, "ownerVersion=1.2.3");
            StringAssert.Contains(snapshot, "runtime=available, protectionHook=available");
            StringAssert.Contains(snapshot, "settingsScope=installed-hook-settings");
            StringAssert.Contains(snapshot, "PreventEmergencyDemolition=True");
            StringAssert.Contains(snapshot, "emergencyDemolitionHook.Success=True");
            StringAssert.Contains(snapshot, "historicalHookEffect=unknown");
        }
        [TestMethod]
        public void MissingOwnersRuntimeAndHookRemainDistinctAndSettingsSurvive()
        {
            StringAssert.Contains(ProtectionSnapshot.Capture(false, null, "none"), "owner-not-registered");
            StringAssert.Contains(ProtectionSnapshot.Capture(true, null, "1"), "owner-managed-reference-missing");
            var plugin = new DestroyedPluginProbe { runtime = null };
            string snapshot = ProtectionSnapshot.Capture(true, plugin, "1");
            StringAssert.Contains(snapshot, "runtime=unavailable, protectionHook=unavailable");
            StringAssert.Contains(snapshot, "settingsScope=configuration-only");
            StringAssert.Contains(snapshot, "EnableMod=True");
            plugin.runtime = new RuntimeProbe { aiEconomyProtectionHook = null };
            StringAssert.Contains(ProtectionSnapshot.Capture(true, plugin, "1"), "runtime=available, protectionHook=unavailable");
        }
        [TestMethod]
        public void FailedGettersAndMissingMembersDoNotDiscardOtherEvidence()
        {
            string snapshot = ProtectionSnapshot.Capture(true, new DestroyedPluginProbe(), "1");
            StringAssert.Contains(snapshot, "PreventHovelDeletion=unavailable");
            StringAssert.Contains(snapshot, "PreventHovelDeletion:read-failed-");
            StringAssert.Contains(snapshot, "InaccessibleAIBuildingDemolitionProtection:member-missing");
            StringAssert.Contains(snapshot, "aiHovelDemolitionHook:member-missing");
            StringAssert.Contains(snapshot, "emergencyDemolitionHook.Success=True");
        }
        [TestMethod]
        public void ProductionPluginLookupDoesNotUseUnityNullOperator()
        {
            byte[] il = typeof(ProtectionSnapshot).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static,
                null, Type.EmptyTypes, null).GetMethodBody().GetILAsByteArray();
            var equality = typeof(UnityEngine.Object).GetMethod("op_Equality", BindingFlags.Public | BindingFlags.Static);
            // Decode call tokens rather than execute Unity native methods outside the game.
            for (int i = 0; i + 4 < il.Length; i++)
                if (il[i] == 0x28)
                {
                    try { Assert.AreNotEqual(equality, typeof(ProtectionSnapshot).Module.ResolveMethod(BitConverter.ToInt32(il, i + 1))); }
                    catch (ArgumentException) { }
                }
        }
        [TestMethod]
        public void ExpectedCoverageGapWarnsButActualFailuresRemainErrors()
        {
            Assert.AreEqual(LogLevel.Warning, EconomyDiagnostics.GetLogLevel("AI_ECONOMY_CAPABILITY: feature=protection-decisions, available=False, status=unsupported, detail=no publisher"));
            Assert.AreEqual(LogLevel.Error, EconomyDiagnostics.GetLogLevel("AI_ECONOMY_CAPABILITY: feature=native, available=False, status=failed, detail=hook failed"));
            Assert.AreEqual(LogLevel.Error, EconomyDiagnostics.GetLogLevel("AI_ECONOMY_CAPTURE_FAILED: status=unsupported, detail=failure"));
            Assert.AreEqual(LogLevel.Info, EconomyDiagnostics.GetLogLevel("AI_ECONOMY_CAPABILITY: feature=native, available=True, status=available, detail=ok"));
        }
    }
}
