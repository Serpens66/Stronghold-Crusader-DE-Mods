using Iced.Intel;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static BugfixesAndQoL.AiOutpostRetargetNative;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class Program
{
    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
    [DllImport("kernel32")]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint operation);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate int Execute();

    private const uint GlobalId = 0x1456789A;
    private const int OwnerContext = 0x654321, TribeId = 73, NewTarget = 4;
    private const int RegSentinel = 0x12345, XmmSentinel = 0x54321;
    private static int checks;

    private static void Check(bool result, string name)
    {
        checks++;
        if (!result) throw new InvalidOperationException(name);
    }

    private static int Main()
    {
        const int memorySize = 0x1000000;
        IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)memorySize, 0x3000, 0x40);
        if (memory == IntPtr.Zero) throw new InvalidOperationException("Unable to allocate fixture.");
        try
        {
            Run((ulong)memory.ToInt64());
            Console.WriteLine("PASS: Outpost hook installed by RedBird and executed, checks=" + checks);
            return 0;
        }
        finally { VirtualFree(memory, UIntPtr.Zero, 0x8000); }
    }

    private static void Run(ulong image)
    {
        ulong entry = image + 0x200, callback = image + 0x400, values = image + 0x800;
        ulong demand = values + 0x80, tribe = image + 0x1000, losses = image + 0x2000;
        ulong slots = image + 0x60000, globals = slots + 0x40, units = image + 0x70000;
        ulong target = tribe + TribeOrigin + TargetPlayerOffset;

        // Recreate the audited site stack alignment and continuation. The
        // callback is a native stand-in that deliberately clobbers volatile state.
        var wrapper = new Assembler(64);
        wrapper.push(rbp); wrapper.push(rdi); wrapper.push(rsi); wrapper.push(r13); wrapper.push(r14);
        wrapper.sub(rsp, 0xA0);
        wrapper.mov(rdi, tribe); wrapper.xor(r14d, r14d);
        wrapper.mov(esi, TribeId); wrapper.mov(r13d, OwnerContext);
        wrapper.mov(r11, values); wrapper.mov(__qword_ptr[r11], rsp);
        wrapper.mov(eax, RegSentinel);
        wrapper.mov(ecx, RegSentinel); wrapper.mov(edx, RegSentinel);
        wrapper.mov(r8d, RegSentinel); wrapper.mov(r9d, RegSentinel); wrapper.mov(r10d, RegSentinel); wrapper.mov(r11d, RegSentinel);
        wrapper.mov(ebp, XmmSentinel);
        wrapper.movd(xmm0, ebp); wrapper.movd(xmm5, ebp);
        wrapper.cmp(eax, eax);
        wrapper.AddUnrestrictedJmp(entry);
        Write(wrapper, image);

        Marshal.Copy(Original, 0, (IntPtr)entry, Original.Length);
        var continuation = new Assembler(64);
        continuation.pushfq(); continuation.push(r11);
        continuation.mov(r11, values);
        continuation.mov(__qword_ptr[r11 + 8], rax);
        continuation.mov(__qword_ptr[r11 + 16], rcx);
        continuation.mov(__qword_ptr[r11 + 24], rdx);
        continuation.mov(__qword_ptr[r11 + 32], r8);
        continuation.mov(__qword_ptr[r11 + 40], r9);
        continuation.mov(__qword_ptr[r11 + 48], r10);
        continuation.pop(rax); continuation.mov(__qword_ptr[r11 + 56], rax);
        continuation.pop(rax); continuation.mov(__qword_ptr[r11 + 64], rax);
        continuation.mov(__qword_ptr[r11 + 72], rsp);
        continuation.movd(eax, xmm0); continuation.mov(__dword_ptr[r11 + 80], eax);
        continuation.movd(eax, xmm5); continuation.mov(__dword_ptr[r11 + 84], eax);
        continuation.mov(eax, ebp);
        continuation.add(rsp, 0xA0);
        continuation.pop(r14); continuation.pop(r13); continuation.pop(rsi); continuation.pop(rdi); continuation.pop(rbp);
        continuation.ret();
        Write(continuation, entry + (ulong)Original.Length);

        var slow = new Assembler(64);
        slow.mov(rax, values);
        slow.mov(__qword_ptr[rax + 88], rcx);
        slow.mov(__qword_ptr[rax + 96], rdx);
        slow.mov(__qword_ptr[rax + 104], rsp);
        slow.inc(__dword_ptr[rax + 112]);
        slow.mov(rax, target); slow.mov(__word_ptr[rax], NewTarget);
        slow.xor(eax, eax); slow.xor(ecx, ecx); slow.xor(edx, edx);
        slow.xor(r8d, r8d); slow.xor(r9d, r9d); slow.xor(r10d, r10d); slow.xor(r11d, r11d);
        slow.pxor(xmm0, xmm0); slow.pxor(xmm5, xmm5);
        slow.ret();
        Write(slow, callback);

        using (var hook = new X64InlineHook(entry, Original.Length))
        {
            Check(hook.DisplacedByteCount == Original.Length, "installed backend displaces 22 bytes");
            hook.Generate((a, instructions, resume) =>
            {
                Check(resume == entry + (ulong)Original.Length, "backend resumes at first untouched instruction");
                EmitStub(a, instructions, demand, losses, slots, globals, units, callback);
            });
            hook.Enable();
            Check(hook.IsInstalled && *(ushort*)entry == 0x25FF &&
                *(ulong*)(entry + 6) == (ulong)hook.StubAddress.ToInt64(), "actual installed patch points to generated stub");
            var execute = Marshal.GetDelegateForFunctionPointer<Execute>((IntPtr)image);

            void Expect(bool enabled, short player, WinLossState loss, ushort lord, AliveState alive,
                uint identity, ushort death, bool retarget, string scenario)
            {
                *(byte*)demand = enabled ? (byte)1 : (byte)0;
                *(short*)target = player;
                *(int*)(values + 112) = 0;
                if (player >= 1 && player <= 8)
                {
                    *(WinLossState*)(losses + (ulong)(player * PlayerStride)) = loss;
                    *(ushort*)(slots + (ulong)(player * sizeof(short))) = lord;
                    *(uint*)(globals + (ulong)(player * sizeof(uint))) = GlobalId;
                }
                if (lord > 0 && lord <= GameUnitManager.LastLiveUnitId)
                {
                    ulong unit = units + (ulong)(lord * UnitStride);
                    *(AliveState*)(unit + UnitAlive) = alive;
                    *(uint*)(unit + UnitGlobal) = identity;
                    *(ushort*)(unit + UnitDeath) = death;
                }

                Check(execute() == (retarget ? NewTarget : player), scenario + ": continuation reads correct target");
                Check(*(int*)(values + 112) == (retarget ? 1 : 0), scenario + ": slow-path calls");
                for (int offset = 8; offset <= 56; offset += 8)
                    Check(*(ulong*)(values + (ulong)offset) == RegSentinel, scenario + ": restored volatile GPR");
                Check((*(ulong*)(values + 64) & 0x41) == 0x40, scenario + ": restored flags");
                Check(*(ulong*)values == *(ulong*)(values + 72), scenario + ": balanced stack");
                Check(*(int*)(values + 80) == XmmSentinel && *(int*)(values + 84) == XmmSentinel, scenario + ": restored SIMD");
                if (retarget)
                    Check(*(ulong*)(values + 88) == OwnerContext && *(ulong*)(values + 96) == TribeId &&
                        (*(ulong*)(values + 104) & 15) == 8, scenario + ": Win64 arguments and alignment");
            }

            Expect(false, 1, WinLossState.Loss, 1, AliveState.IsAlive, GlobalId, 0, false, "disabled");
            Expect(true, 1, WinLossState.None, 1, AliveState.IsAlive, GlobalId, 0, false, "first living target");
            Expect(true, 8, WinLossState.None, GameUnitManager.LastLiveUnitId, AliveState.IsAlive, GlobalId, 0, false, "last living target");
            Expect(true, 8, WinLossState.Loss, 1, AliveState.IsAlive, GlobalId, 0, true, "official loss");
            Expect(true, 1, WinLossState.None, 1, AliveState.IsAlive, GlobalId, 1, true, "death before official loss");
            Expect(true, 1, WinLossState.None, 1, AliveState.IsAlive, GlobalId + 1, 0, true, "recycled Lord slot");
            Expect(true, 1, WinLossState.None, 1, AliveState.MarkedForDeletion, GlobalId, 0, true, "Lord marked for deletion");
            Expect(true, 1, WinLossState.None, 1, AliveState.NeedsInit, GlobalId, 0, false, "replacement Lord still initializing");
            Expect(true, 1, WinLossState.None, 0, AliveState.None, 0, 0, true, "Lord absent after removal");
            Expect(true, 1, WinLossState.None, ushort.MaxValue, AliveState.None, 0, 0, false, "out-of-range Lord slot");
            Expect(true, 0, WinLossState.None, 0, AliveState.None, 0, 0, false, "unassigned target");
            Expect(true, -1, WinLossState.None, 0, AliveState.None, 0, 0, false, "negative target");
            Expect(true, 9, WinLossState.None, 0, AliveState.None, 0, 0, false, "out-of-range target");
        }
    }

    private static void Write(Assembler assembler, ulong address)
    {
        using (var stream = new MemoryStream())
        {
            assembler.Assemble(new StreamCodeWriter(stream), address);
            Marshal.Copy(stream.ToArray(), 0, (IntPtr)address, (int)stream.Length);
        }
    }
}
