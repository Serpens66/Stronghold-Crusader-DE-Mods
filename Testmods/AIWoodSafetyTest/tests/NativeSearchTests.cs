using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AIBuildDiagnoseTest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace AIWoodSafetyTest.Tests
{
    [TestClass, DoNotParallelize]
    public sealed class NativeSearchTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Search(IntPtr state, uint x, uint y);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFree(IntPtr p, UIntPtr size, uint kind);
        private static byte[] Image()
        {
            string game = Environment.GetEnvironmentVariable("SHCDE_GAME_DIR");
            var bytes = File.ReadAllBytes(Path.Combine(game, "Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll"));
            using (var sha = SHA256.Create()) Assert.AreEqual(NativeDiagnosticContracts.SupportedHash, BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""));
            return bytes;
        }
        private static int Offset(byte[] pe, int rva)
        {
            int header = BitConverter.ToInt32(pe, 0x3C), sections = BitConverter.ToUInt16(pe, header + 6);
            int table = header + 24 + BitConverter.ToUInt16(pe, header + 20);
            for (int i = 0; i < sections; i++)
            {
                int s = table + i * 40, start = BitConverter.ToInt32(pe, s + 12), size = BitConverter.ToInt32(pe, s + 16);
                if (rva >= start && rva < start + size) return rva - start + BitConverter.ToInt32(pe, s + 20);
            }
            throw new InvalidOperationException("RVA not in image");
        }
        private static int Cell(int x, int y) => x * 160 + y;
        private static void Prepare(IntPtr state, HashSet<int> excluded)
        {
            Marshal.Copy(new byte[0x1C0000], 0, state, 0x1C0000);
            for (int x = 0; x < 160; x++) for (int y = 0; y < 160; y++)
                Marshal.WriteByte(state, 0x5B834 + Cell(x, y) * 0x30, 15);
            // Interior bounded patch; the actual search retains its queue, generation and limits.
            for (int x = 68; x <= 73; x++) for (int y = 68; y <= 73; y++)
            {
                Marshal.WriteByte(state, 0x5B834 + Cell(x, y) * 0x30, 0);
                if (excluded.Contains(Cell(x, y))) Marshal.WriteByte(state, 0x5B837 + Cell(x, y) * 0x30, 1);
            }
        }
        private static long Result(IntPtr state) => Marshal.ReadInt64(state, 0x1B983C);
        private static byte[] QueueState(IntPtr state)
        {
            var result = new byte[0x3200C]; Marshal.Copy(state + 0x187830, result, 0, result.Length); return result;
        }
        [TestMethod]
        public void RealVanillaBfsResumesAfterVetoFindsAlternativeAndExhaustsNormally()
        {
            IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x300000, 0x3000, 0x40);
            Assert.AreNotEqual(IntPtr.Zero, code);
            IntPtr state = Marshal.AllocHGlobal(0x1C0000), control = Marshal.AllocHGlobal(40), calls = Marshal.AllocHGlobal(8);
            var masks = new ushort[9 * ThreatMasks.OwnerStride]; var pin = GCHandle.Alloc(masks, GCHandleType.Pinned);
            X64InlineHook hook = null;
            try
            {
                var image = Image();
                Marshal.Copy(image, Offset(image, 0x58950), code + 0x58950, 0x290);
                Marshal.Copy(image, Offset(image, 0x2D2E50), code + 0x2D2E50, 64);
                var search = Marshal.GetDelegateForFunctionPointer<Search>(code + 0x58950);
                Prepare(state, new HashSet<int>()); search(state, 70, 70); long first = Result(state);
                Assert.AreNotEqual(-1L, first);
                int firstX = unchecked((int)first), firstY = unchecked((int)(first >> 32));
                var one = new HashSet<int> { Cell(firstX, firstY) };
                var all = new HashSet<int>(); for (int x = 68; x <= 73; x++) for (int y = 68; y <= 73; y++) all.Add(Cell(x, y));
                long[] results = new long[2]; byte[][] queues = new byte[2][];
                int scenario = 0;
                foreach (var excluded in new[] { one, all })
                {
                    Prepare(state, excluded); search(state, 70, 70); results[scenario] = Result(state); queues[scenario++] = QueueState(state);
                }
                Assert.AreNotEqual(first, results[0]); Assert.AreNotEqual(-1L, results[0]); Assert.AreEqual(-1L, results[1]);
                Marshal.Copy(new byte[40], 0, control, 40); Marshal.Copy(new byte[8], 0, calls, 8);
                Marshal.WriteInt64(control, 8, state.ToInt64()); Marshal.WriteInt64(control, 16, pin.AddrOfPinnedObject().ToInt64());
                hook = WoodCandidateHook.CreatePrepared(unchecked((ulong)(code + WoodCandidateHook.Rva).ToInt64()),
                    unchecked((ulong)(code + WoodCandidateHook.FallbackRva).ToInt64()), control,
                    _ => (a, original, next) =>
                    {
                        // Only the CLR callback boundary is substituted. Execute the production
                        // prefilter, displacement and both original Vanilla continuations verbatim.
                        a.mov(rax, unchecked((ulong)calls.ToInt64())); a.inc(__dword_ptr[rax]); a.mov(eax, __dword_ptr[rax + 4]);
                    });
                hook.Enable(); WoodCandidateHook.ValidatePublished(hook);
                foreach (int owner in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
                {
                    scenario = 0;
                    foreach (var excluded in new[] { one, all })
                    {
                        Array.Clear(masks, 0, masks.Length);
                        foreach (int cell in excluded) masks[owner * ThreatMasks.OwnerStride + (cell / 160 * 5) * 800 + cell % 160 * 5] = 1;
                        Marshal.WriteInt32(calls, 4, 1); Marshal.WriteByte(control, 1); Marshal.WriteInt32(control, 4, 1 << owner);
                        Prepare(state, new HashSet<int>()); search(state, 70, 70);
                        Assert.AreEqual(results[scenario], Result(state), "owner=" + owner);
                        CollectionAssert.AreEqual(queues[scenario], QueueState(state), "same Vanilla queue including limits and order"); scenario++;
                    }
                }
                // Relevant candidate callback may decline (e.g. disabled/stale context).
                int before = Marshal.ReadInt32(calls); Marshal.WriteInt32(calls, 4, 0);
                Prepare(state, new HashSet<int>()); search(state, 70, 70); Assert.AreEqual(first, Result(state)); Assert.IsTrue(Marshal.ReadInt32(calls) > before);
                foreach (int bypass in new[] { 0, 1, 2 })
                {
                    before = Marshal.ReadInt32(calls); Marshal.WriteInt32(calls, 4, 1);
                    Marshal.WriteByte(control, bypass == 0 ? (byte)0 : (byte)1);
                    Marshal.WriteInt64(control, 8, bypass == 1 ? state.ToInt64() + 1 : state.ToInt64());
                    Marshal.WriteInt32(control, 4, bypass == 2 ? 0 : 1 << 8);
                    Prepare(state, new HashSet<int>()); search(state, 70, 70); Assert.AreEqual(first, Result(state)); Assert.AreEqual(before, Marshal.ReadInt32(calls));
                }
                Assert.IsTrue(Marshal.ReadInt64(control, 24) > 0); Assert.IsTrue(Marshal.ReadInt64(control, 32) > 0);
            }
            finally
            {
                // Restoring hooks is permitted only on these private, synchronous test buffers.
                hook?.Dispose(); pin.Free(); Marshal.FreeHGlobal(state); Marshal.FreeHGlobal(control); Marshal.FreeHGlobal(calls);
                Assert.IsTrue(VirtualFree(code, UIntPtr.Zero, 0x8000));
            }
        }
    }
}
