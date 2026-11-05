using System;
using System.Runtime.InteropServices;
using Iced.Intel;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    // Context hook for hot native loops whose callback is a no-op most of the time. The stub first
    // tests a native demand byte: 0 runs only the displaced original instructions and jumps back
    // (no managed transition); otherwise RedBird's regular context stub calls the callback, then the
    // same displaced instructions run (AfterCallback order, unchanged callback context).
    // The guard uses only push/pop/mov/movzx/jrcxz/jmp: no flags are read or written, RCX is restored
    // on both paths and the stack is balanced before either path continues. The displaced
    // instructions exist once in the stub (Iced rejects duplicate instruction IPs).
    // The hook is prepared, not enabled; the caller enables it within its own initialization and
    // keeps it for the process lifetime (logical activation only through the demand byte).
    internal static class DemandGatedContextHook
    {
        internal static X64InlineHook Create(
            ulong address,
            int minimumHookSize,
            ContextHookDelegate callback,
            ContextHookOptions options,
            IntPtr demand,
            string name,
            ILogger logger)
        {
            if (demand == IntPtr.Zero)
                throw new ArgumentException("A demand-gated context hook requires a demand byte.", nameof(demand));
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            if (options == null || options.Placement != OverwrittenInstructionPlacement.AfterCallback ||
                options.InstructionSelector != null)
                throw new ArgumentException("Demand-gated context hooks support AfterCallback without instruction selection.", nameof(options));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            var contextOptions = new ContextHookOptions
            {
                Registers = options.Registers,
                HookSize = options.HookSize,
                ErrorMode = options.ErrorMode,
                RecoveryAction = options.RecoveryAction,
                Placement = OverwrittenInstructionPlacement.Suppress
            };
            // RedBird's own context stub (Suppress: this generator places the displaced instructions).
            return CreateGated(address, minimumHookSize, hook => ContextStub.CreateGenerator(
                hook, ContextStub.Guard(callback, options, address, logger, name), contextOptions), demand, name, logger);
        }

        // Guard around an arbitrary active body. The active body must not emit the displaced
        // instructions; they follow both paths exactly once. Separate so the machine test can run the
        // real guard with a non-generic body (the CLR test host cannot marshal ContextHookDelegate).
        internal static X64InlineHook CreateGated(
            ulong address,
            int minimumHookSize,
            Func<X64InlineHook, InlineHookGenerator> activeBody,
            IntPtr demand,
            string name,
            ILogger logger)
        {
            if (demand == IntPtr.Zero)
                throw new ArgumentException("A demand-gated hook requires a demand byte.", nameof(demand));
            var hook = new X64InlineHook(address, minimumHookSize, logger, name);
            if (hook.State == HookState.Failed)
                throw new InvalidOperationException("Demand-gated context hook could not decode its span: " + name);
            InlineHookGenerator context = activeBody(hook);
            ulong demandAddress = unchecked((ulong)demand.ToInt64());
            hook.Generate((asm, original, returnAddress) =>
            {
                var dormant = asm.CreateLabel();
                var active = asm.CreateLabel();
                var displaced = asm.CreateLabel();
                asm.push(rcx);
                asm.mov(rcx, demandAddress);
                asm.movzx(ecx, __byte_ptr[rcx]);
                asm.jrcxz(dormant);
                asm.pop(rcx);
                asm.jmp(active);
                asm.Label(ref dormant);
                asm.pop(rcx);
                asm.jmp(displaced);
                asm.Label(ref active);
                context(asm, original, returnAddress);
                asm.Label(ref displaced);
                asm.AddInstructions(original);
                // X64InlineHook appends the jump back to returnAddress.
            });
            ValidateGuard(hook, demandAddress);
            return hook;
        }

        // Fail closed before publication: the stub must start with exactly the guard above.
        internal static void ValidateGuard(X64InlineHook hook, ulong demandAddress)
        {
            if (hook.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("Demand-gated context hook has no stub: " + hook.Name);
            var bytes = new byte[64];
            Marshal.Copy(hook.StubAddress, bytes, 0, bytes.Length);
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = unchecked((ulong)hook.StubAddress.ToInt64());
            Instruction a = decoder.Decode(), b = decoder.Decode(), c = decoder.Decode(), d = decoder.Decode(),
                e = decoder.Decode(), f = decoder.Decode(), g = decoder.Decode(), h = decoder.Decode();
            if (a.Mnemonic != Mnemonic.Push || a.Op0Register != Register.RCX ||
                b.Mnemonic != Mnemonic.Mov || b.Op0Register != Register.RCX || b.Immediate64 != demandAddress ||
                c.Mnemonic != Mnemonic.Movzx || c.Op0Register != Register.ECX || c.MemoryBase != Register.RCX ||
                    c.MemorySize != MemorySize.UInt8 || c.MemoryDisplacement64 != 0 ||
                // jrcxz -> dormant block (g); the active path's jmp (f) lands right after that block.
                d.Mnemonic != Mnemonic.Jrcxz || d.NearBranchTarget != g.IP ||
                e.Mnemonic != Mnemonic.Pop || e.Op0Register != Register.RCX ||
                f.Mnemonic != Mnemonic.Jmp || f.NearBranchTarget != h.NextIP ||
                g.Mnemonic != Mnemonic.Pop || g.Op0Register != Register.RCX ||
                h.Mnemonic != Mnemonic.Jmp || h.NearBranchTarget <= h.NextIP)
                throw new InvalidOperationException("Demand guard ABI changed: " + hook.Name);
        }
    }
}
