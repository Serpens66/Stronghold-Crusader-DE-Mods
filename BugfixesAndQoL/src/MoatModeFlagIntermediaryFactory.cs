using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.X64.Assembly;
using System;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    // Vanilla's moat-mode helper leaves ZF equivalent to (EAX == 0). Its native
    // callers may consume that flag before executing their own test instruction.
    internal sealed class MoatModeFlagIntermediaryFactory : IDetourIntermediaryFactory
    {
        internal static readonly MoatModeFlagIntermediaryFactory Instance =
            new MoatModeFlagIntermediaryFactory();

        private MoatModeFlagIntermediaryFactory() { }

        public DetourIntermediaryStubs Create(in DetourIntermediaryContext context)
        {
            if (context.Architecture != DetourArchitecture.X64 ||
                context.CallbackAddress == IntPtr.Zero ||
                context.TrampolineAddress == IntPtr.Zero)
                throw new InvalidOperationException("The moat-mode detour has no valid x64 callback or trampoline.");

            // Win64 entry RSP is 8 modulo 16. Reserve 32 bytes of shadow space
            // and 8 bytes for alignment, then restore RSP before setting ZF.
            ulong callback = unchecked((ulong)context.CallbackAddress.ToInt64());
            ExecutableFunction entry = ExecutableFunction.Create(asm =>
            {
                asm.sub(rsp, 0x28);
                asm.mov(rax, callback);
                asm.call(rax);
                asm.add(rsp, 0x28);
                asm.test(eax, eax);
                asm.ret();
            });
            return new DetourIntermediaryStubs(
                entry.EntryPoint, context.TrampolineAddress, entry);
        }

        internal static void ValidateNativeHook<TFunction>(
            NativeDetour<TFunction> hook, ulong targetAddress, IntPtr callbackAddress,
            bool installed) where TFunction : Delegate
        {
            if (hook == null || hook.TargetAddress != targetAddress ||
                hook.Scheme != DetourScheme.Indirect ||
                hook.DisplacedByteCount != 10 ||
                hook.TrampolineAddress == IntPtr.Zero ||
                hook.OriginalEntryPointAddress != hook.TrampolineAddress ||
                hook.HookEntryPointAddress == IntPtr.Zero ||
                hook.HookEntryPointAddress == callbackAddress ||
                hook.PointerSlot == IntPtr.Zero || hook.IsInstalled != installed)
                throw new InvalidOperationException("The installed NativeX64 moat-mode detour contract changed.");

            byte[] code = new byte[32];
            Marshal.Copy(hook.HookEntryPointAddress, code, 0, code.Length);
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(code));
            decoder.IP = unchecked((ulong)hook.HookEntryPointAddress.ToInt64());
            Mnemonic[] expected =
            {
                Mnemonic.Sub, Mnemonic.Mov, Mnemonic.Call,
                Mnemonic.Add, Mnemonic.Test, Mnemonic.Ret
            };
            foreach (Mnemonic mnemonic in expected)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || instruction.Mnemonic != mnemonic ||
                    (mnemonic == Mnemonic.Test &&
                     (instruction.Op0Register != Register.EAX ||
                      instruction.Op1Register != Register.EAX)))
                    throw new InvalidOperationException("The moat-mode entry stub does not restore ZF from EAX.");
            }

            if (!installed)
                return;
            IntPtr target = new IntPtr(unchecked((long)targetAddress));
            if (Marshal.ReadByte(target) != 0xFF || Marshal.ReadByte(target, 1) != 0x25)
                throw new InvalidOperationException("The installed moat-mode detour has no indirect jump.");
            int displacement = Marshal.ReadInt32(target, 2);
            if (IntPtr.Add(target, 6 + displacement) != hook.PointerSlot ||
                Marshal.ReadInt64(hook.PointerSlot) != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("The moat-mode pointer slot does not target its entry stub.");
        }
    }
}
