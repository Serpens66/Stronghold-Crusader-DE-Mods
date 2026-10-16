using CanarariWoodTest;
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AIBuildDiagnoseTest.Tests
{
    [TestClass]
    public sealed class DisposableProbeTests
    {
        [TestMethod]
        public void HashGuardRejectsMissingAndChangedSaveCopies()
        {
            string path = Path.Combine(Path.GetTempPath(), "CanarariCopy-" + Guid.NewGuid().ToString("N") + ".sav");
            const string emptyHash = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
            try
            {
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
                File.WriteAllBytes(path, Array.Empty<byte>());
                Assert.IsTrue(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, new string('0', 64)));
                File.WriteAllText(path, "changed");
                Assert.IsFalse(DisposableProbePolicy.HasExpectedHash(path, emptyHash));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        [TestMethod]
        public void OnlyNamedLoadedCopyCanPassSessionGate()
        {
            const string name = "test_canari_nowoodcutters_probe.sav";
            Assert.IsTrue(DisposableProbePolicy.IsNamedCopy(true, false, false, @"C:\saves\" + name, name));
            Assert.IsTrue(DisposableProbePolicy.IsNamedCopy(true, false, false, name.ToUpperInvariant(), name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(false, false, false, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, true, false, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, true, name, name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, false, "test_canari_nowoodcutters.sav", name));
            Assert.IsFalse(DisposableProbePolicy.IsNamedCopy(true, false, false, null, name));
        }
    }
}
