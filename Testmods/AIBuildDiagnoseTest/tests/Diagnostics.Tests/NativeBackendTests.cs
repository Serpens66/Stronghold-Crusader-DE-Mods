using AIBuildDiagnoseTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass, DoNotParallelize]
    public class NativeBackendTests
    {
        [TestMethod]
        public void InstalledBackendAcceptsBothProductionObservationProloguesOnPrivateCopies()
        {
            string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR");
            Assert.IsFalse(string.IsNullOrEmpty(game), "Set SHCDE_GAME_DIR for this local integration test.");
            byte[] image = File.ReadAllBytes(Path.Combine(game,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"));
            Assert.AreEqual(NativeDiagnosticContracts.SupportedHash, NativeDiagnosticContracts.ComputeSha256(image));
            VerifyProbe(image, 0x539B0, "ProbeBackend", new object[] { null, new Action<AiBuildDiagnosticRecord>(_ => { }) });
            VerifyProbe(image, 0xC3BF0, "ProbeRouteBackend", new object[] { null });
        }

        private static void VerifyProbe(byte[] image, int rva, string method, object[] arguments)
        {
            int pe = BitConverter.ToInt32(image, 0x3C);
            int count = BitConverter.ToUInt16(image, pe + 6);
            int optionalSize = BitConverter.ToUInt16(image, pe + 20);
            int fileOffset = -1;
            for (int i = 0; i < count; i++)
            {
                int section = pe + 24 + optionalSize + i * 40;
                int size = Math.Min(BitConverter.ToInt32(image, section + 8), BitConverter.ToInt32(image, section + 16));
                int start = BitConverter.ToInt32(image, section + 12);
                if (rva >= start && rva + 64 <= start + size)
                { fileOffset = BitConverter.ToInt32(image, section + 20) + rva - start; break; }
            }
            Assert.IsTrue(fileOffset >= 0, "The complete probe prologue must be mapped in the native image.");
            IntPtr scratch = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x10000), 0x3000, 0x40);
            Assert.AreNotEqual(IntPtr.Zero, scratch, "Private probe allocation failed.");
            try
            {
                Marshal.Copy(image, fileOffset, scratch, 64);
                arguments[0] = unchecked((ulong)scratch.ToInt64());
                // Invoke the actual production copied-buffer probe: exact NativeX64
                // scheme, displacement, installed patch, pointer slot and entry checks.
                typeof(AiBuildDiagnostic).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, arguments);
                byte[] after = new byte[64];
                Marshal.Copy(scratch, after, 0, after.Length);
                byte[] before = new byte[64];
                Array.Copy(image, fileOffset, before, 0, before.Length);
                CollectionAssert.AreEqual(before, after, "The production probe must not patch its input copy.");
            }
            finally { Assert.IsTrue(VirtualFree(scratch, UIntPtr.Zero, 0x8000), "Scratch release failed."); }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
    }
}
