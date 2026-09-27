using BugfixesAndQoL;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

internal static class MoatModeFlagContract
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int ModeProbe(IntPtr manager, int unitId);

    private static int callbackCount;
    private static ExecutableFunction targetRoot;
    private static ExecutableFunction callerRoot;
    private static NativeDetour<ModeProbe> hookRoot;
    private static ModeProbe callbackRoot;

    private static int ManagedMode(IntPtr manager, int unitId)
    {
        callbackCount++;
        return unitId == 7 ? 0 : 1;
    }

    internal static void Validate()
    {
        ExecutableFunction target = ExecutableFunction.Create(asm =>
        {
            // The first 10 bytes exactly match the installed 0x196840 prologue.
            asm.movsxd(rax, edx);
            asm.imul(rdx, rax, 0x490);
            asm.mov(eax, edx);
            asm.and(eax, 1);
            asm.ret();
        });
        byte[] expectedPrefix = Convert.FromHexString("4863C24869D090040000");
        if (!target.OriginalBytes.Take(10).SequenceEqual(expectedPrefix))
            throw new Exception("Moat-mode fixture no longer matches the audited native prologue.");

        ExecutableFunction caller = ExecutableFunction.Create(asm =>
        {
            asm.sub(rsp, 0x28);
            asm.mov(r11, target.Address);
            asm.call(r11);
            asm.pushfq();
            asm.pop(rax);
            asm.shr(rax, 6);
            asm.and(eax, 1);
            asm.add(rsp, 0x28);
            asm.ret();
        });
        ModeProbe invoke = caller.GetDelegate<ModeProbe>();
        ModeProbe callback = ManagedMode;
        var request = new DetourRequest<ModeProbe>
        {
            Name = "BugfixesAndQoL moat-mode ZF contract",
            TargetAddress = target.Address,
            Callback = callback,
            IntermediaryFactory = MoatModeFlagIntermediaryFactory.Instance
        };
        var candidate = (NativeDetour<ModeProbe>)NativeDetourBackend.Instance.CreateDetour(in request);
        // A published executable hook remains rooted until this test process exits.
        targetRoot = target;
        callerRoot = caller;
        callbackRoot = callback;
        hookRoot = candidate;
        try
        {
            if (candidate.Scheme != DetourScheme.Indirect ||
                candidate.DisplacedByteCount != 10 ||
                candidate.TargetAddress != target.Address ||
                candidate.TrampolineAddress == IntPtr.Zero ||
                candidate.OriginalEntryPointAddress != candidate.TrampolineAddress ||
                candidate.HookEntryPointAddress == Marshal.GetFunctionPointerForDelegate(callback) ||
                candidate.PointerSlot == IntPtr.Zero)
                throw new Exception("Installed NativeX64 moat-mode detour contract changed.");

            ValidateStub(candidate.HookEntryPointAddress);
            candidate.Enable();
            if (!candidate.IsInstalled || Marshal.ReadByte(target.EntryPoint) != 0xFF ||
                Marshal.ReadByte(target.EntryPoint, 1) != 0x25)
                throw new Exception("NativeX64 did not publish the expected indirect jump.");
            int displacement = Marshal.ReadInt32(target.EntryPoint, 2);
            if (IntPtr.Add(target.EntryPoint, 6 + displacement) != candidate.PointerSlot ||
                Marshal.ReadInt64(candidate.PointerSlot) != candidate.HookEntryPointAddress.ToInt64())
                throw new Exception("NativeX64 pointer slot does not target the flag-preserving stub.");

            if (invoke(IntPtr.Zero, 7) != 1 || invoke(IntPtr.Zero, 8) != 0 ||
                callbackCount != 2)
                throw new Exception("The native caller did not receive ZF matching the managed result.");
        }
        finally { GC.KeepAlive(callback); }
        Console.WriteLine("PASS: installed NativeX64 moat-mode detour preserves EAX/ZF for zero and one.");
    }

    private static void ValidateStub(IntPtr entry)
    {
        byte[] bytes = new byte[32];
        Marshal.Copy(entry, bytes, 0, bytes.Length);
        Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
        decoder.IP = unchecked((ulong)entry.ToInt64());
        Mnemonic[] expected =
        {
            Mnemonic.Sub, Mnemonic.Mov, Mnemonic.Call,
            Mnemonic.Add, Mnemonic.Test, Mnemonic.Ret
        };
        foreach (Mnemonic mnemonic in expected)
        {
            Instruction instruction = decoder.Decode();
            if (instruction.IsInvalid || instruction.Mnemonic != mnemonic)
                throw new Exception("The generated moat-mode stub has an unexpected instruction.");
            if (mnemonic == Mnemonic.Test &&
                (instruction.Op0Register != Register.EAX ||
                 instruction.Op1Register != Register.EAX))
                throw new Exception("The moat-mode stub must test EAX immediately before RET.");
        }
    }
}
