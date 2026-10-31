using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

namespace AIBuildDiagnoseTest.Tests
{
    [TestClass, DoNotParallelize]
    public sealed class WorkerNativeTests
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Run(IntPtr data, int state, int branch);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr p, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFree(IntPtr p, UIntPtr size, uint kind);
        private const ulong Sentinel = 0x0FEDCBA987654321;
        private static void Assemble(Assembler a, IntPtr destination)
        {
            using (var stream = new MemoryStream())
            { a.Assemble(new StreamCodeWriter(stream), unchecked((ulong)destination.ToInt64())); var bytes = stream.ToArray(); Marshal.Copy(bytes, 0, destination, bytes.Length); }
        }
        private static void Harness(IntPtr destination, IntPtr site)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r12); a.push(r13); a.push(r14); a.push(r15);
            a.sub(rsp, 40);
            a.mov(r12, rcx); a.xor(ebx, ebx); a.xor(ecx, ecx); a.xor(esi, esi);
            a.mov(r14, Sentinel); a.mov(eax, edx);
            a.mov(__byte_ptr[r12 + 0xA48], r8b);
            a.pcmpeqd(xmm0, xmm0);
            a.cmp(edi, edi);
            a.call(unchecked((ulong)site.ToInt64()));
            a.mov(__qword_ptr[r12 + 0x1200], rcx); a.mov(__qword_ptr[r12 + 0x1208], rbx);
            a.mov(__qword_ptr[r12 + 0x1210], r14);
            a.pushfq(); a.pop(__qword_ptr[r12 + 0x1218]);
            a.movdqu(__xmmword_ptr[r12 + 0x1240], xmm0);
            a.add(rsp, 40);
            a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx); a.ret();
            Assemble(a, destination);
        }

        [TestMethod]
        public void ProductionNativeGuardsExecuteRealSpansBothBranchesAndFilterWithoutRegisterOrFlagDamage()
        {
            foreach (var contract in WorkerCauseNativeHooks.Sites)
            {
                IntPtr code = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x2000, 0x3000, 0x40);
                Assert.AreNotEqual(IntPtr.Zero, code);
                IntPtr data = Marshal.AllocHGlobal(0x2000), demand = Marshal.AllocHGlobal(1), calls = Marshal.AllocHGlobal(8);
                X64InlineHook hook = null;
                try
                {
                    Marshal.Copy(new byte[0x2000], 0, data, 0x2000);
                    Marshal.WriteInt32(calls, 0); Marshal.WriteByte(demand, 0);
                    IntPtr site = code + 0x400;
                    Marshal.Copy(contract.Bytes, 0, site, contract.Bytes.Length);
                    if (contract.ReturnSite) Marshal.WriteByte(site, contract.Length, 0xC3);
                    else
                    {
                        // The actual JNE +0x1F exits the 21-byte span at site+0x2B.
                        Marshal.Copy(new byte[] { 0xB8, 11, 0, 0, 0, 0xC3 }, 0, site + 21, 6);
                        Marshal.Copy(new byte[] { 0xB8, 22, 0, 0, 0, 0xC3 }, 0, site + 0x2B, 6);
                    }
                    Marshal.WriteInt16(data, 0x6EE, -7);
                    Harness(code, site);
                    var run = Marshal.GetDelegateForFunctionPointer<Run>(code);
                    long[,] flags = new long[2, 2];
                    foreach (int stateIndex in new[] { 0, 1 }) foreach (int branch in new[] { 0, 1 })
                    {
                        int state = stateIndex == 0 ? 6 : 110;
                        Assert.AreEqual(contract.ReturnSite ? state : branch == 0 ? 11 : 22, run(data, state, branch));
                        flags[stateIndex, branch] = Marshal.ReadInt64(data, 0x1218);
                    }
                    hook = WorkerCauseNativeHooks.CreatePrepared(unchecked((ulong)site.ToInt64()), contract, demand,
                        _ => (a, original, next) =>
                        {
                            // CLR cannot marshal RedBird's generic context delegate. This native
                            // body replaces only that boundary, exercising the real production gate.
                            a.pushfq(); a.push(rax); a.mov(rax, unchecked((ulong)calls.ToInt64()));
                            a.inc(__dword_ptr[rax]); a.pop(rax); a.popfq();
                        });
                    Assert.AreEqual(contract.Length, hook.DisplacedByteCount);
                    hook.Enable(); WorkerCauseNativeHooks.ValidatePublished(hook, contract.Length);
                    foreach (byte enabled in new byte[] { 0, 1, 0, 255 })
                        foreach (int stateIndex in new[] { 0, 1 }) foreach (int branch in new[] { 0, 1 })
                        {
                            int state = stateIndex == 0 ? 6 : 110;
                            Marshal.WriteByte(demand, enabled); int before = Marshal.ReadInt32(calls);
                            Assert.AreEqual(contract.ReturnSite ? state : branch == 0 ? 11 : 22, run(data, state, branch));
                            Assert.AreEqual(enabled != 0 && (!contract.ReturnSite || state == 110) ? 1 : 0, Marshal.ReadInt32(calls) - before);
                            Assert.AreEqual(flags[stateIndex, branch] & 0x8D5, Marshal.ReadInt64(data, 0x1218) & 0x8D5);
                            Assert.AreEqual(0L, Marshal.ReadInt64(data, 0x1200)); Assert.AreEqual(0L, Marshal.ReadInt64(data, 0x1208));
                            Assert.AreEqual(unchecked((long)Sentinel), Marshal.ReadInt64(data, 0x1210), "R14 is not a unit ID at every hook site.");
                            for (int i = 0; i < 16; i++) Assert.AreEqual((byte)255, Marshal.ReadByte(data, 0x1240 + i));
                            if (contract.ReturnSite)
                            { Assert.AreEqual((short)state, Marshal.ReadInt16(data, 0x918)); Assert.AreEqual((short)0, Marshal.ReadInt16(data, 0x920)); }
                        }
                    // Decode the generated production stub only to its external continuation.
                    // Every displaced state store / CMP must appear exactly once, not in both gate arms.
                    var bytes = new byte[128]; Marshal.Copy(hook.StubAddress, bytes, 0, bytes.Length);
                    var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = unchecked((ulong)hook.StubAddress.ToInt64());
                    int writes = 0, comparisons = 0;
                    for (int i = 0; i < 50; i++)
                    {
                        var ins = decoder.Decode();
                        if (ins.Mnemonic == Mnemonic.Mov && (ins.MemoryDisplacement64 == 0x918 || ins.MemoryDisplacement64 == 0x920)) writes++;
                        if (ins.Mnemonic == Mnemonic.Cmp && ins.MemoryDisplacement64 == 0xA48) comparisons++;
                        if (ins.Mnemonic == Mnemonic.Jmp && ins.Op0Kind == OpKind.NearBranch64 && ins.NearBranchTarget == unchecked((ulong)site.ToInt64()) + (uint)contract.Length) break;
                    }
                    Assert.AreEqual(contract.ReturnSite ? 2 : 0, writes); Assert.AreEqual(contract.ReturnSite ? 0 : 1, comparisons);
                }
                finally
                {
                    // Only private, synchronously executed test buffers are restored/freed.
                    hook?.Dispose(); Marshal.FreeHGlobal(data); Marshal.FreeHGlobal(demand); Marshal.FreeHGlobal(calls);
                    Assert.IsTrue(VirtualFree(code, UIntPtr.Zero, 0x8000));
                }
            }
        }
    }
}
