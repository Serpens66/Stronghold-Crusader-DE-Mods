using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.GateRouting
{
    // Executes the production DemandGatedContextHook stub (installed RedBird/Iced) on a private
    // harness: incoming flags must reach the instruction after the hook (JE), GPRs, stack and
    // volatile XMM must survive the dormant guard, the callback runs exactly when the demand byte
    // is set, sees the hook-site registers and can write them back (unchanged AfterCallback
    // contract), and a throwing callback fails open.
    internal static unsafe class DemandGatedContextHookTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int RunHarness(IntPtr unused, IntPtr unused2, IntPtr results, int compareValue);
        // Published test hooks, delegates and pages live until the test process exits.
        private static readonly List<object> Roots = new List<object>();
        private const ulong Sentinel = 0x0FEDCBA987654321;

        internal static void Run()
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)4096, 0x3000, 0x40);
            Require(page != IntPtr.Zero, "harness allocation");
            Roots.Add(page);
            // Hook site: flag-neutral `lea r13,[r13+1]` plus NOPs; r13 proves the displaced code runs once.
            byte[] original = new byte[14];
            for (int i = 0; i < original.Length; i++) original[i] = 0x90;
            original[0] = 0x4D; original[1] = 0x8D; original[2] = 0x6D; original[3] = 0x01;
            byte[] harness = BuildHarness(original, unchecked((ulong)page.ToInt64()), out int offset);
            Marshal.Copy(harness, 0, page, harness.Length);

            IntPtr demand = Marshal.AllocHGlobal(8);
            Marshal.WriteInt64(demand, 0);
            Roots.Add(demand);
            IntPtr results = Marshal.AllocHGlobal(256);
            IntPtr counter = Marshal.AllocHGlobal(8);
            Marshal.WriteInt64(counter, 0);
            Roots.Add(counter);
            ulong counterAddress = unchecked((ulong)counter.ToInt64());
            // Native stand-in for RedBird's context stub: counts entries and preserves flags/RAX, so
            // every register/flag difference observed below comes from the guard itself.
            X64InlineHook hook = DemandGatedContextHook.CreateGated(unchecked((ulong)page.ToInt64()) + (uint)offset, 14,
                _ => (asm, instructions, returnAddress) =>
                {
                    asm.pushfq(); asm.push(rax);
                    asm.mov(rax, counterAddress); asm.inc(__dword_ptr[rax]);
                    asm.pop(rax); asm.popfq();
                }, demand, "demandGateFixture", NullLogger.Instance);
            Require(hook.DisplacedByteCount == 14, "exact displacement");
            hook.Enable();
            Roots.Add(hook);
            var run = (RunHarness)Marshal.GetDelegateForFunctionPointer(page, typeof(RunHarness));
            int expected = 0;
            try
            {
                foreach (byte demandValue in new byte[] { 0, 1, 0, 2, 255 })
                foreach (int value in new[] { 0, 1 })
                {
                    bool active = demandValue != 0;
                    Marshal.WriteByte(demand, demandValue);
                    int before = Marshal.ReadInt32(counter);
                    Require(run(IntPtr.Zero, IntPtr.Zero, results, value) == (value == 0 ? 2 : 1),
                        "incoming ZF reaches the instruction after the hook");
                    Require(Marshal.ReadInt32(counter) - before == (active ? 1 : 0), "active body runs exactly when demand is set");
                    if (active) expected++;
                    Require(Marshal.ReadInt64(results, 8) == Marshal.ReadInt64(results, 16), "stack balanced");
                    Require((ulong)Marshal.ReadInt64(results, 24) == Sentinel && (ulong)Marshal.ReadInt64(results, 32) == Sentinel + 1,
                        "volatile R11/R10 preserved");
                    for (int i = 0; i < 7; i++)
                        Require((ulong)Marshal.ReadInt64(results, 80 + i * 8) == Sentinel + (ulong)i + 2, "nonvolatile register " + i);
                    Require((ulong)Marshal.ReadInt64(results, 40) == Sentinel + 9, "RCX restored on both paths");
                    Require((ulong)Marshal.ReadInt64(results, 72) == Sentinel + 10, "displaced instructions run exactly once");
                    for (int i = 0; i < 6; i++) Require(Marshal.ReadInt64(results, 144 + i * 16) == -1, "XMM" + i + " preserved");
                }
                Require(Marshal.ReadInt32(counter) == expected, "no extra active entries");
            }
            finally { Marshal.FreeHGlobal(results); }
            Console.WriteLine("PASS: demand-gated context hook guard: transparent for flags/GPR/RCX/XMM/stack, active body only on demand (any non-zero byte), displaced instructions once on both paths.");
        }

        // Saves nonvolatiles, loads sentinels, sets ZF from compareValue, runs the 14-byte hook site,
        // then records the JE outcome and all registers into results.
        private static byte[] BuildHarness(byte[] site, ulong ip, out int siteOffset)
        {
            var a = new Assembler(64);
            a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi);
            a.push(r12); a.push(r13); a.push(r14); a.push(r15);
            a.sub(rsp, 64);
            a.mov(__qword_ptr[r8 + 8], rsp);
            a.mov(eax, r9d);
            a.mov(r11, Sentinel); a.mov(r10, Sentinel + 1);
            a.mov(rbx, Sentinel + 2); a.mov(rbp, Sentinel + 3);
            a.mov(rsi, Sentinel + 4); a.mov(rdi, Sentinel + 5);
            a.mov(r12, Sentinel + 6); a.mov(r14, Sentinel + 7); a.mov(r15, Sentinel + 8);
            a.mov(rcx, Sentinel + 9); a.mov(r13, Sentinel + 9);
            a.pcmpeqd(xmm0, xmm0); a.pcmpeqd(xmm1, xmm1); a.pcmpeqd(xmm2, xmm2);
            a.pcmpeqd(xmm3, xmm3); a.pcmpeqd(xmm4, xmm4); a.pcmpeqd(xmm5, xmm5);
            a.cmp(eax, 0);
            Label siteLabel = a.CreateLabel();
            a.Label(ref siteLabel);
            a.db(site);
            Label reject = a.CreateLabel(), collect = a.CreateLabel();
            a.je(reject);
            a.mov(__dword_ptr[r8], 1);
            a.jmp(collect);
            a.Label(ref reject);
            a.mov(__dword_ptr[r8], 2);
            a.Label(ref collect);
            a.mov(__qword_ptr[r8 + 16], rsp);
            a.mov(__qword_ptr[r8 + 24], r11); a.mov(__qword_ptr[r8 + 32], r10);
            a.mov(__qword_ptr[r8 + 40], rcx); a.mov(__qword_ptr[r8 + 72], r13);
            a.mov(__qword_ptr[r8 + 80], rbx); a.mov(__qword_ptr[r8 + 88], rbp);
            a.mov(__qword_ptr[r8 + 96], rsi); a.mov(__qword_ptr[r8 + 104], rdi);
            a.mov(__qword_ptr[r8 + 112], r12); a.mov(__qword_ptr[r8 + 120], r14);
            a.mov(__qword_ptr[r8 + 128], r15);
            a.movdqu(__xmmword_ptr[r8 + 144], xmm0); a.movdqu(__xmmword_ptr[r8 + 160], xmm1);
            a.movdqu(__xmmword_ptr[r8 + 176], xmm2); a.movdqu(__xmmword_ptr[r8 + 192], xmm3);
            a.movdqu(__xmmword_ptr[r8 + 208], xmm4); a.movdqu(__xmmword_ptr[r8 + 224], xmm5);
            a.mov(eax, __dword_ptr[r8]);
            a.add(rsp, 64);
            a.pop(r15); a.pop(r14); a.pop(r13); a.pop(r12);
            a.pop(rdi); a.pop(rsi); a.pop(rbp); a.pop(rbx);
            a.ret();
            using (var stream = new MemoryStream())
            {
                var result = a.Assemble(new StreamCodeWriter(stream), ip, BlockEncoderOptions.ReturnNewInstructionOffsets);
                siteOffset = (int)(result.GetLabelRIP(siteLabel) - ip);
                return stream.ToArray();
            }
        }

        private static void Require(bool value, string reason)
        { if (!value) throw new InvalidOperationException("Demand-gated context hook regression: " + reason); }
    }
}
