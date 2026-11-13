using Iced.Intel;
using RedBird.X64.Hooks;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using static ExtraFeatures.MatureTreeHarvestNative;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class MatureTreeHarvestStubTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr size);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int Invoke();

        internal static void Run(byte[] image, Action<bool, string> check)
        {
            const int size = 0x940000;
            IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)size, 0x3000, 0x40);
            check(memory != IntPtr.Zero, "mature tree native fixture allocation");
            try
            {
                foreach (var site in Sites)
                    RunSite(image, memory, site, check);
            }
            finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }
        }

        private static void RunSite(byte[] image, IntPtr memory, Site site, Action<bool, string> check)
        {
            const int harness = 0x1000, fixture = 0x3000, flag = 0x4000, output = 0x5000;
            const int candidate = fixture + Stride;
            IntPtr Ptr(int rva) => IntPtr.Add(memory, rva);
            ulong Va(int rva) => unchecked((ulong)Ptr(rva).ToInt64());
            void Copy(byte[] bytes, int rva) => Marshal.Copy(bytes, 0, Ptr(rva), bytes.Length);
            void WriteCode(int rva, Assembler asm) => Copy(Assemble(asm, Va(rva)), rva);
            bool search = site.Kind == Path.Search;
            bool strike = site.Kind == Path.Strike;
            int continuation = site.Rva + site.Original.Length;
            int fallback = strike ? 0x12C291 : 0x12BFFA;
            check(image.AsSpan(site.Rva, site.Original.Length).SequenceEqual(site.Original) &&
                  image.AsSpan(continuation, site.Following.Length).SequenceEqual(site.Following) &&
                  image.AsSpan(site.Reject, site.RejectBytes.Length).SequenceEqual(site.RejectBytes),
                "mature tree native contract " + site.Kind);
            check(Shared.NativePatternResolver.ResolveUnique(image, BitConverter.ToString(site.Original).Replace('-', ' '),
                site.Rva, true, "mature tree fixture", null, Shared.NativePatternSearchScope.ExecutableSections).Rva == site.Rva,
                "mature tree signature " + site.Kind);
            Copy(site.Original, site.Rva);
            DecodeAndValidate(site, Va(site.Rva), Va(0));
            using var hook = new X64InlineHook(Va(site.Rva), site.Original.Length);
            check(hook.DisplacedByteCount == site.Original.Length, "mature tree RedBird span " + site.Kind);
            hook.Generate((asm, original, _) => Emit(asm, site.Kind, Va(flag), Va(CurrentWorkerRva), Va(site.Reject), original));
            ulong stub = unchecked((ulong)hook.StubAddress.ToInt64());

            void Exit(int rva, int outcome)
            {
                int exitRva = output + outcome * 0x100;
                var jump = new Assembler(64);
                jump.jmp(Va(exitRva));
                WriteCode(rva, jump);
                var exit = new Assembler(64);
                exit.mov(__qword_ptr[r10], rax);
                exit.mov(__qword_ptr[r10 + 8], rcx);
                exit.mov(__qword_ptr[r10 + 16], rdx);
                exit.mov(__qword_ptr[r10 + 24], rbx);
                exit.mov(__qword_ptr[r10 + 32], rsi);
                exit.mov(__qword_ptr[r10 + 40], r14);
                exit.pushfq(); exit.pop(r11);
                exit.mov(__qword_ptr[r10 + 48], r11);
                exit.mov(eax, outcome);
                exit.ret();
                WriteCode(exitRva, exit);
            }
            Exit(continuation, 1);
            Exit(site.Reject, 2);
            if (!search) Exit(fallback, 3);
            Marshal.WriteInt32(Ptr(CurrentWorkerRva), -17);

            (int Outcome, long[] Registers) Execute(ulong target, int enabled, int standing)
            {
                Marshal.WriteInt32(Ptr(flag), enabled);
                var setup = new Assembler(64);
                setup.push(rbx); setup.push(rbp); setup.push(rsi); setup.push(r14);
                setup.mov(r10, Va(output));
                setup.mov(r14, Va(fixture));
                setup.mov(rsi, Va(candidate + Alive));
                setup.mov(rbx, 0x12345678);
                setup.mov(ebp, 1);
                setup.mov(ecx, 0x123456);
                setup.mov(edx, 0x98765);
                setup.mov(eax, standing);
                setup.stc();
                setup.call(target);
                setup.pop(r14); setup.pop(rsi); setup.pop(rbp); setup.pop(rbx);
                setup.ret();
                WriteCode(harness, setup);
                if (!FlushInstructionCache(GetCurrentProcess(), memory, UIntPtr.Zero))
                    throw new InvalidOperationException("Could not flush the mature tree fixture's instruction cache.");
                int outcome = Marshal.GetDelegateForFunctionPointer<Invoke>(Ptr(harness))();
                var registers = new long[7];
                Marshal.Copy(Ptr(output), registers, 0, registers.Length);
                return (outcome, registers);
            }

            // Search's Vanilla gate sees alive == 2 and stage < 4; worker sites receive
            // the standing predicate result. Exercise each policy input on all three paths.
            for (int species = 0; species <= 5; species++)
            for (int stage = 0; stage <= 4; stage++)
            foreach (var tree in new[] { (Hp: 3, Felled: 0), (Hp: 2, Felled: 0), (Hp: 0, Felled: 1) })
            {
                Marshal.WriteInt16(Ptr(candidate + Alive), 2);
                Marshal.WriteInt16(Ptr(candidate + Species), (short)species);
                Marshal.WriteInt32(Ptr(candidate + Stage), stage);
                Marshal.WriteInt16(Ptr(candidate + Health), (short)tree.Hp);
                Marshal.WriteInt16(Ptr(candidate + MaxHealth), 3);
                Marshal.WriteInt16(Ptr(candidate + Felled), (short)tree.Felled);
                int standing = tree.Felled == 0 ? 1 : 0;
                var vanilla = Execute(Va(site.Rva), 0, standing);
                var disabled = Execute(stub, 0, standing);
                var enabled = Execute(stub, 1, standing);
                string label = $"{site.Kind} species={species} stage={stage} hp={tree.Hp} felled={tree.Felled}";
                check(disabled.Outcome == vanilla.Outcome && disabled.Registers.SequenceEqual(vanilla.Registers),
                    "mature tree disabled equals Vanilla " + label);
                bool protectedTree = species >= 1 && species <= 4 && stage < 3 && tree.Hp == 3;
                if (!protectedTree)
                    check(enabled.Outcome == vanilla.Outcome && enabled.Registers.SequenceEqual(vanilla.Registers),
                        "mature tree permitted equals Vanilla " + label);
                else
                    check(enabled.Outcome == 2 && enabled.Registers[0] == (search ? 1 : -17) &&
                        enabled.Registers[1] == 0x123456 && enabled.Registers[2] == (strike ? 1 : 0x98765) &&
                        enabled.Registers[3] == 0x12345678 && enabled.Registers[4] == (long)Va(candidate + Alive) &&
                        enabled.Registers[5] == (long)Va(fixture) && (search || (enabled.Registers[6] & 1) == 1),
                        "mature tree rejection uses the native exit contract " + label);
            }

            Marshal.WriteInt16(Ptr(candidate + Species), 1);
            Marshal.WriteInt32(Ptr(candidate + Stage), 0);
            Marshal.WriteInt16(Ptr(candidate + Health), 3);
            Marshal.WriteInt16(Ptr(candidate + Felled), 0);
            if (search)
            {
                Marshal.WriteInt16(Ptr(candidate + Alive), 0);
                check(Execute(stub, 1, 1).Outcome == 2, "search still skips a dead woodland slot");
                Marshal.WriteInt16(Ptr(candidate + Alive), 2);
            }
            else
                check(Execute(stub, 1, 0).Outcome == 3, "worker preserves Vanilla's fallback for a stale serial " + site.Kind);
            hook.Enable();
            check(Execute(Va(site.Rva), 1, 1).Outcome == 2 && Execute(Va(site.Rva), 0, 1).Outcome == 1,
                "patched native entry dispatches correctly in both modes " + site.Kind);
        }
    }
}
