using AIBuildDiagnoseTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using Iced.Intel;

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
                (method == "EconomyProbe" ? typeof(EconomyNativeHooks) : typeof(AiBuildDiagnostic))
                    .GetMethod(method == "EconomyProbe" ? "ProbeBackend" : method, BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, arguments);
                byte[] after = new byte[64];
                Marshal.Copy(scratch, after, 0, after.Length);
                byte[] before = new byte[64];
                Array.Copy(image, fileOffset, before, 0, before.Length);
                CollectionAssert.AreEqual(before, after, "The production probe must not patch its input copy.");
            }
            finally { Assert.IsTrue(VirtualFree(scratch, UIntPtr.Zero, 0x8000), "Scratch release failed."); }
        }
        [TestMethod]
        public void EveryEconomyEntryUsesTheActualInstalledBackendAndWholeAuditedInstructions()
        {
            string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR");
            byte[] image = File.ReadAllBytes(Path.Combine(game,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll"));
            Assert.AreEqual(NativeDiagnosticContracts.SupportedHash, NativeDiagnosticContracts.ComputeSha256(image));
            foreach (EconomyHookContract contract in EconomyHookContract.All)
                VerifyProbe(image, contract.Rva, "EconomyProbe", new object[] { null, contract.Length });
        }
        [TestMethod]
        public void RelocatedSaleEntryExecutesBothSidesOfItsConditionalBranch()
        {
            var contract = EconomyHookContract.Find(0x29700);
            IntPtr copy = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x10000), 0x3000, 0x40);
            Assert.AreNotEqual(IntPtr.Zero, copy);
            NativeDetour<EconomyNativeHooks.QuadResult> probe = null;
            EconomyNativeHooks.QuadResult original = null;
            int calls = 0;
            EconomyNativeHooks.QuadResult callback = (state, player, good, amount) =>
            { calls++; return original(state, player, good, amount); };
            try
            {
                Marshal.Copy(contract.Prefix, 0, copy, contract.Prefix.Length);
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(contract.Prefix));
                decoder.IP = unchecked((ulong)copy.ToInt64());
                decoder.Decode(); var branch = decoder.Decode();
                Assert.AreEqual(FlowControl.ConditionalBranch, branch.FlowControl);
                int negativeOffset = checked((int)(branch.NearBranchTarget - unchecked((ulong)copy.ToInt64())));
                Assert.IsTrue(negativeOffset > contract.Length && negativeOffset < 0x10000 - 6);
                // Synthetic pure return bodies: no game globals or native game calls execute.
                Marshal.Copy(new byte[] { 0xB8, 17, 0, 0, 0, 0xC3 }, 0, copy + contract.Length, 6);
                Marshal.Copy(new byte[] { 0xB8, 23, 0, 0, 0, 0xC3 }, 0, copy + negativeOffset, 6);
                var request = new DetourRequest<EconomyNativeHooks.QuadResult>
                { Name = "private economy branch fixture", TargetAddress = unchecked((ulong)copy.ToInt64()), Callback = callback };
                probe = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<EconomyNativeHooks.QuadResult>;
                EconomyNativeHooks.Validate(probe, unchecked((ulong)copy.ToInt64()), contract.Length, false);
                original = Marshal.GetDelegateForFunctionPointer<EconomyNativeHooks.QuadResult>(probe.TrampolineAddress);
                probe.Enable();
                EconomyNativeHooks.Validate(probe, unchecked((ulong)copy.ToInt64()), contract.Length, true);
                var entry = Marshal.GetDelegateForFunctionPointer<EconomyNativeHooks.QuadResult>(copy);
                Assert.AreEqual(17, entry(0, 1, 0, 1));
                Assert.AreEqual(23, entry(0, 1, 0, -1));
                Assert.AreEqual(2, calls, "Each invocation must call through the original exactly once.");
            }
            finally
            {
                probe?.Dispose(); GC.KeepAlive(callback); GC.KeepAlive(original);
                Assert.IsTrue(VirtualFree(copy, UIntPtr.Zero, 0x8000));
            }
        }
        [TestMethod]
        public void NewRemovalProloguesExecuteWithUnchangedArgumentsAndSingleOriginalCall()
        {
            foreach (int rva in new[] { 0x3B270, 0xC8F50, 0x55E10 })
            {
                var contract = EconomyHookContract.Find(rva);
                IntPtr copy = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x10000), 0x3000, 0x40);
                Assert.AreNotEqual(IntPtr.Zero, copy);
                NativeDetour<EconomyNativeHooks.QuadResult> hook = null;
                EconomyNativeHooks.QuadResult original = null;
                int calls = 0;
                EconomyNativeHooks.QuadResult callback = (state, a, b, c) => { calls++; return original(state, a, b, c); };
                try
                {
                    Marshal.Copy(contract.Prefix, 0, copy, contract.Prefix.Length);
                    // Pure synthetic body returns argument 2 + argument 3. The sweep prologue
                    // pushes RSI/RDI/R14 and reserves 32 bytes; restore exactly that frame.
                    byte[] body = rva == 0xC8F50
                        ? new byte[] { 0x8B, 0xC2, 0x44, 0x01, 0xC0, 0x48, 0x83, 0xC4, 0x20, 0x41, 0x5E, 0x5F, 0x5E, 0xC3 }
                        : new byte[] { 0x8B, 0xC2, 0x44, 0x01, 0xC0, 0xC3 };
                    Marshal.Copy(body, 0, copy + contract.Length, body.Length);
                    var request = new DetourRequest<EconomyNativeHooks.QuadResult>
                    { Name = "private removal entry execution", TargetAddress = unchecked((ulong)copy.ToInt64()), Callback = callback };
                    hook = NativeDetourBackend.Instance.CreateDetour(in request) as NativeDetour<EconomyNativeHooks.QuadResult>;
                    EconomyNativeHooks.Validate(hook, unchecked((ulong)copy.ToInt64()), contract.Length, false);
                    original = Marshal.GetDelegateForFunctionPointer<EconomyNativeHooks.QuadResult>(hook.TrampolineAddress);
                    hook.Enable();
                    EconomyNativeHooks.Validate(hook, unchecked((ulong)copy.ToInt64()), contract.Length, true);
                    var entry = Marshal.GetDelegateForFunctionPointer<EconomyNativeHooks.QuadResult>(copy);
                    Assert.AreEqual(23, entry(0x12345678, 17, 6, 0));
                    Assert.AreEqual(-17, entry(0x12345678, -23, 6, 0));
                    Assert.AreEqual(2, calls);
                }
                finally { hook?.Dispose(); GC.KeepAlive(callback); GC.KeepAlive(original); Assert.IsTrue(VirtualFree(copy, UIntPtr.Zero, 0x8000)); }
            }
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
    }
}
