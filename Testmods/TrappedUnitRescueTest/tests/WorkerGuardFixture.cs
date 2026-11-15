using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace TrappedUnitRescueTest
{
    // Executes the production guard and the audited complete Fixes worker body on
    // private code/data. The game DLL and every live game's unit record stay untouched.
    internal static class WorkerGuardFixture
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void InvokeFixture();
        private const ulong R9Canary = 0x0123456789ABCDEFUL;
        private const int UnitId = 1, UnitStride = 0x490, LifeOffset = 0x6E4, TypeOffset = 0x6E6;
        private const int FlagsMask = 0x8C5; // CF,PF,ZF,SF,OF; AF after TEST is unspecified.

        internal static int Run()
        {
            if (IntPtr.Size != 8) throw new InvalidOperationException("Worker guard fixture requires Windows x64.");
            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x10000, 0x3000, 0x40);
            if (memory == IntPtr.Zero) throw new InvalidOperationException("Worker guard private allocation failed.");
            X64InlineHook hook = null;
            int checks = 0;
            try
            {
                ulong page = unchecked((ulong)memory.ToInt64());
                ulong entry = page + 0x200, probe = page + 0x400, inspect = page + 0x600;
                ulong fixes = page + 0x800, tail = page + 0x1000;
                ulong active = page + 0x3100, probeResult = page + 0x3104, moatMode = page + 0x3108;
                ulong observed = page + 0x3200, units = page + 0x4000;
                ulong resume = entry + FixesIntegration.WorkerGuardLength;

                var wrapper = new Assembler(64);
                wrapper.push(r15); wrapper.push(r12); wrapper.push(rsi); wrapper.sub(rsp, 0x80);
                wrapper.mov(r15, units); wrapper.mov(r12d, 13); wrapper.mov(esi, UnitId);
                wrapper.mov(r9, R9Canary); wrapper.mov(rax, observed); wrapper.mov(__qword_ptr[rax + 72], rsp);
                wrapper.jmp(entry);
                Write(page, FixesIntegration.Assemble(wrapper, page));

                var original = new Assembler(64);
                original.mov(rcx, r15); original.movsx(eax, r12w); original.mov(__dword_ptr[rsp + 0x70], eax);
                original.call(probe);
                byte[] displaced = FixesIntegration.Assemble(original, entry);
                Check(displaced.Length == 16, "Private source block must displace exactly 16 bytes.", ref checks);
                Write(entry, displaced);
                var next = new Assembler(64); next.jmp(inspect); Write(resume, FixesIntegration.Assemble(next, resume));

                var helper = new Assembler(64);
                helper.mov(rax, probeResult); helper.mov(eax, __dword_ptr[rax]); helper.test(eax, eax); helper.ret();
                Write(probe, FixesIntegration.Assemble(helper, probe));

                // Observe the guard's continuation without changing any register or flag.
                var capture = new Assembler(64);
                capture.push(rdx); capture.mov(rdx, observed);
                capture.pushfq(); capture.pop(__qword_ptr[rdx + 48]);
                capture.mov(__dword_ptr[rdx + 56], eax);
                capture.mov(__qword_ptr[rdx + 64], rsp);
                capture.pop(rdx); capture.jmp(fixes);
                Write(inspect, FixesIntegration.Assemble(capture, inspect));

                // Exact original 196352..196364 replay with its RIP load retargeted
                // to this fixture's private global. The final CMOV remains in the tail.
                byte[] replay = { 0x8B,0x0D,0,0,0,0,0x85,0xC0,0x41,0xBB,1,0,0,0,0x45,0x0F,0xBF,0xC4 };
                Array.Copy(BitConverter.GetBytes(checked((int)((long)moatMode - (long)(resume + 6)))), 0, replay, 2, 4);
                var foreign = new Assembler(64);
                FixesIntegration.EmitExpectedFixesWorker(foreign, FixesIntegration.Decode(replay, resume), units);
                foreign.AddUnrestrictedJmp(tail);
                Write(fixes, FixesIntegration.Assemble(foreign, fixes));

                var finish = new Assembler(64);
                finish.cmovne(ecx, r11d); finish.pushfq(); finish.pop(r10);
                finish.mov(rdx, observed); finish.mov(__dword_ptr[rdx], eax); finish.mov(__dword_ptr[rdx + 4], ecx);
                finish.mov(__qword_ptr[rdx + 8], r8); finish.mov(__qword_ptr[rdx + 16], r9);
                finish.mov(__qword_ptr[rdx + 24], r11); finish.mov(__qword_ptr[rdx + 32], r10);
                finish.mov(eax, __dword_ptr[rsp + 0x70]); finish.mov(__dword_ptr[rdx + 40], eax);
                finish.add(rsp, 0x80); finish.pop(rsi); finish.pop(r12); finish.pop(r15); finish.ret();
                Write(tail, FixesIntegration.Assemble(finish, tail));

                hook = new X64InlineHook(entry, 14, name: "TrappedUnitRescue.WorkerFixture");
                Check(hook.DisplacedByteCount == 16 && !hook.IsInstalled, "Exact production backend displacement.", ref checks);
                hook.Generate((a, instructions, returnAddress) => FixesIntegration.EmitWorkerGuard(a, instructions, active, returnAddress));
                var expected = new Assembler(64);
                FixesIntegration.EmitWorkerGuard(expected, FixesIntegration.Decode(displaced, entry), active, resume);
                expected.AddUnrestrictedJmp(resume);
                byte[] expectedStub = FixesIntegration.Assemble(expected, unchecked((ulong)hook.StubAddress.ToInt64()));
                byte[] actualStub = new byte[expectedStub.Length]; Marshal.Copy(hook.StubAddress, actualStub, 0, actualStub.Length);
                Check(actualStub.SequenceEqual(expectedStub), "Generated production stub matches exact backend output.", ref checks);
                hook.Enable();
                Check(hook.IsInstalled && hook.DisplacedByteCount == 16, "Private backend hook installed.", ref checks);

                var invoke = (InvokeFixture)Marshal.GetDelegateForFunctionPointer(memory, typeof(InvokeFixture));
                var cases = new[] {
                    Tuple.Create(eChimps.CHIMP_TYPE_PEASANT, true),
                    Tuple.Create(eChimps.CHIMP_TYPE_HEALER, true),
                    Tuple.Create(eChimps.CHIMP_TYPE_MOTHER, true),
                    Tuple.Create(eChimps.CHIMP_TYPE_FIREEATER, true),
                    Tuple.Create(eChimps.CHIMP_TYPE_NULL, false),
                    Tuple.Create(eChimps.CHIMP_TYPE_SPEARMAN, false),
                    Tuple.Create(eChimps.CHIMP_TYPE_ENGINEER, false),
                    Tuple.Create(eChimps.CHIMP_TYPE_ARAB_ASSASIN, false) };
                foreach (var sample in cases)
                for (int enabled = 0; enabled <= 1; enabled++)
                for (int onMoat = 0; onMoat <= 1; onMoat++)
                {
                    byte[] before = Enumerable.Repeat((byte)0x5A, 0x1000).ToArray();
                    int life = UnitId * UnitStride + LifeOffset, type = UnitId * UnitStride + TypeOffset;
                    Array.Copy(BitConverter.GetBytes((short)AliveState.IsAlive), 0, before, life, 2);
                    Array.Copy(BitConverter.GetBytes((ushort)sample.Item1), 0, before, type, 2);
                    Marshal.Copy(before, 0, new IntPtr((long)units), before.Length);
                    Marshal.WriteInt32(new IntPtr((long)active), enabled);
                    Marshal.WriteInt32(new IntPtr((long)probeResult), onMoat);
                    Marshal.WriteInt32(new IntPtr((long)moatMode), 7);
                    invoke();

                    string label = sample.Item1 + "/active=" + enabled + "/moat=" + onMoat;
                    bool deleted = enabled == 0 && onMoat == 1 && sample.Item2;
                    if (deleted) Array.Copy(BitConverter.GetBytes((short)AliveState.MarkedForDeletion), 0, before, life, 2);
                    byte[] after = new byte[before.Length]; Marshal.Copy(new IntPtr((long)units), after, 0, after.Length);
                    Check(after.SequenceEqual(before), "Only expected original Fixes life write: " + label, ref checks);
                    Check(Read32(observed) == onMoat && Read32(observed + 56) == onMoat, "Moat EAX preserved: " + label, ref checks);
                    Check(Read32(observed + 4) == (onMoat == 1 ? 1 : 7), "Vanilla mode CMOV preserved: " + label, ref checks);
                    Check(Read64(observed + 8) == 13 && Read64(observed + 16) == R9Canary && Read64(observed + 24) == 1,
                        "Replay R8/R9/R11 preserved: " + label, ref checks);
                    Check(Read32(observed + 40) == 13, "Original stack argument preserved: " + label, ref checks);
                    // Inspection temporarily pushed RDX; its stack observation is exactly eight lower.
                    Check(Read64(observed + 64) + 8 == Read64(observed + 72), "Stack balanced at continuation: " + label, ref checks);
                    int probeFlags = onMoat == 0 ? 0x44 : 0;
                    int guardedFlags = enabled == 1 ? 0x44 : probeFlags;
                    Check(((int)Read64(observed + 48) & FlagsMask) == guardedFlags, "Native guard flags: " + label, ref checks);
                    Check(((int)Read64(observed + 32) & FlagsMask) == probeFlags, "Vanilla TEST flags restored: " + label, ref checks);
                }
                GC.KeepAlive(invoke);
            }
            finally
            {
                // Private synchronous fixture only: no other thread has the pointer, no
                // invocation is pending and the delegate is no longer used after this point.
                hook?.Dispose();
                VirtualFree(memory, UIntPtr.Zero, 0x8000);
            }
            return checks;
        }

        private static void Write(ulong address, byte[] bytes) => Marshal.Copy(bytes, 0, new IntPtr((long)address), bytes.Length);
        private static int Read32(ulong address) => Marshal.ReadInt32(new IntPtr((long)address));
        private static ulong Read64(ulong address) => unchecked((ulong)Marshal.ReadInt64(new IntPtr((long)address)));
        private static void Check(bool passed, string message, ref int count)
        {
            count++;
            if (!passed) throw new InvalidOperationException("Worker guard fixture failed: " + message);
        }
    }
}
