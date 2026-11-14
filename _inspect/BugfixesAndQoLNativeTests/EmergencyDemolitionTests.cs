using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using BugfixesAndQoL;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using static Iced.Intel.AssemblerRegisters;
using Demolition = BugfixesAndQoL.AIEmergencyDemolitionDispatch.DemolitionDelegate;

// Executes the actual canonical strategy tail with only external callees replaced.
// This catches the original regression (skipping notification/state work together with
// demolition); the state algorithm itself is never reimplemented in this fixture.
internal static class EmergencyDemolitionTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StrategyTail(IntPtr manager, int playerId, int lordIndex, byte gate);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
    private static int checks;
    private const int Player = 3, Stride = 0x583C;
    private const int Panic = 0x379E6C0, Nervous = 0x379E6BC, Gold = 0x379B2DC;
    private const int Stance = 0x379DEC4, Urgency = 0x379D848, DonationTick = 0x379E792;
    private const int DonationCount = 0x379E790;

    internal static int Run(byte[] image)
    {
        checks = 0;
        AIEmergencyDemolitionNativeContract.ValidateImage(image, AIEmergencyDemolitionNativeContract.FunctionRva);
        CheckSignature(image);
        AIEmergencyDemolitionNativeContract.ValidateIncomingBranches(image, AIEmergencyDemolitionNativeContract.FunctionRva);
        CheckRejectedChanges(image);
        Check(image.Skip(0x2D810).Take(6).SequenceEqual(AIEmergencyDemolitionNativeContract.Entry), "production entry bytes");
        var instructions = Decode(image, 0x2F080, 1520);
        int[] calls = instructions.Where(i => i.FlowControl == FlowControl.Call && i.NearBranchTarget == 0x2D810)
            .Select(i => (int)i.IP).ToArray();
        Check(calls.SequenceEqual(new[] { 0x2F516, 0x2F527, 0x2F5B6, 0x2F5CA, 0x2F5DE, 0x2F609 }), "all six native demolition call sites");
        Check(!Decode(image, 0x2D810, 474).Any(i =>
            (i.FlowControl == FlowControl.ConditionalBranch || i.FlowControl == FlowControl.UnconditionalBranch) &&
            i.NearBranchTarget > 0x2D810 && i.NearBranchTarget < 0x2D816), "no function branch enters displaced entry");
        Instruction gate = instructions.Single(i => i.IP == 0x2F45C);
        Check(gate.Mnemonic == Mnemonic.Je && gate.NearBranchTarget == 0x2F653, "old gate bypasses the entire state tail");
        using (var fixture = new Fixture(image))
        {
            TestDispatchAndBackend(fixture);
            TestNativeStateTail(fixture);
        }
        Console.WriteLine("PASS: " + checks + " production emergency demolition/strategy execution checks.");
        return checks;
    }

    private static void CheckSignature(byte[] image)
    {
        string[] pattern = AIEmergencyDemolitionNativeContract.Signature.Split(' ');
        int pe = BitConverter.ToInt32(image, 0x3C), count = BitConverter.ToUInt16(image, pe + 6);
        int table = pe + 24 + BitConverter.ToUInt16(image, pe + 20);
        var matches = new List<int>();
        for (int section = 0; section < count; section++)
        {
            int header = table + section * 40;
            if ((BitConverter.ToInt32(image, header + 36) & 0x20000000) == 0) continue;
            int start = BitConverter.ToInt32(image, header + 12), end = start + BitConverter.ToInt32(image, header + 8);
            for (int at = start; at <= end - pattern.Length; at++)
            {
                if (image[at] != Convert.ToByte(pattern[0], 16)) continue;
                bool match = true;
                for (int token = 1; token < pattern.Length; token++)
                    if (pattern[token] != "??" && image[at + token] != Convert.ToByte(pattern[token], 16)) { match = false; break; }
                if (match) matches.Add(at);
            }
        }
        Check(matches.SequenceEqual(new[] { AIEmergencyDemolitionNativeContract.FunctionRva }), "production fallback signature is unique in executable sections");
        byte original = image[0x2D986]; // Audited refund immediate, part of the behavior contract.
        try
        {
            image[0x2D986] ^= 1;
            bool rejected = false;
            try { AIEmergencyDemolitionNativeContract.ValidateImage(image, 0x2D810); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "changed refund semantics are rejected even when the entry pattern still matches");
        }
        finally { image[0x2D986] = original; }
    }

    private static void CheckRejectedChanges(byte[] image)
    {
        byte selector = image[0x2D8A6]; // cmp edi,1: selection must remain the audited seven-category function.
        try
        {
            image[0x2D8A6] = 2;
            bool rejected = false;
            try { AIEmergencyDemolitionNativeContract.ValidateImage(image, 0x2D810); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "changed category selector fails semantic validation");
        }
        finally { image[0x2D8A6] = selector; }
        byte[] original = image.Skip(0x2D997).Take(4).ToArray();
        try
        {
            // Repoint an actual external call, outside strategy_update, at this callee.
            // Its newly broadened ownership must disable the optional hook.
            Array.Copy(BitConverter.GetBytes(0x2D810 - (0x2D996 + 5)), 0, image, 0x2D997, 4);
            bool rejected = false;
            try { AIEmergencyDemolitionNativeContract.ValidateIncomingBranches(image, 0x2D810); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "new caller outside the audited strategy fails ownership validation");
        }
        finally { Array.Copy(original, 0, image, 0x2D997, 4); }
    }

    private static List<Instruction> Decode(byte[] image, int start, int length)
    {
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(image.Skip(start).Take(length).ToArray()));
        decoder.IP = (ulong)start;
        var result = new List<Instruction>();
        while (decoder.IP < (ulong)(start + length))
        {
            var value = decoder.Decode();
            Check(!value.IsInvalid, "native instruction decodes at " + decoder.IP.ToString("X"));
            result.Add(value);
        }
        return result;
    }

    private static void TestDispatchAndBackend(Fixture f)
    {
        foreach (bool enabled in new[] { false, true })
        foreach (byte flag in new byte[] { 0, 1, 255 })
        {
            f.Clear(); f.Suppress = enabled;
            f.Demolish(f.Manager, Player, 7, flag);
            Check(f.Read(0) == (enabled ? 0 : 1) && f.Read(4) == (enabled ? 0 : 1), "real hooked probe refund and deletion follow activation");
            if (!enabled)
            {
                Check(f.Read(8) == Player && f.Read(12) == 7 && Marshal.ReadByte(f.Manager, 16) == flag,
                    "trampoline forwards all ABI arguments including full byte");
            }
        }
        foreach (byte flag in new byte[] { 0, 1, 255 })
        {
            f.Clear(); f.ThrowPolicy = true;
            f.Demolish(f.Manager, Player, 6, flag);
            Check(f.Read(0) == 1 && f.Read(4) == 1 && f.Read(8) == Player && f.Read(12) == 6 &&
                Marshal.ReadByte(f.Manager, 16) == flag && f.Errors == 1, "policy exception fails open exactly once through native trampoline");
            f.ThrowPolicy = false;
        }
        int originals = 0;
        var dispatch = new AIEmergencyDemolitionDispatch(() => false,
            (manager, player, category, flag) => { originals++; throw new InvalidOperationException("original sentinel"); }, _ => { });
        bool threw = false;
        try { dispatch.Invoke(f.Manager, Player, 1, 1); } catch (InvalidOperationException) { threw = true; }
        Check(threw && originals == 1, "original failure must propagate without a second original invocation");
    }

    private static void TestNativeStateTail(Fixture f)
    {
        foreach (bool suppress in new[] { false, true })
        {
            f.Clear(); f.Suppress = suppress; f.Write(32, 1); f.Set16(DonationTick, 9);
            f.Tail(f.Manager, Player, 0, 1);
            Check(f.Read(20) == 1 && f.Get(Panic) == 8 && f.Get16(DonationTick) == 0, "native panic entry retains notification and state initialization");
            Check(f.Read(0) == 0 && f.Read(4) == 0, "entry itself does not demolish");

            foreach (int count in new[] { 7, 1 })
            {
                f.Clear(); f.Suppress = suppress; f.Set(Panic, count); f.Set(Gold, 100); f.Write(32, 1);
                f.Tail(f.Manager, Player, 16, 1);
                Check(f.Get(Panic) == (count == 1 ? 1 : count - 1) && f.Get(Stance) == 1 && f.Get(Stance + 4) == 0,
                    "native panic continuation retains weak/defender state and exact countdown");
                Check(f.Read(0) == (suppress ? 0 : 2) && f.Read(4) == (suppress ? 0 : 2), "both panic demolition calls are gated independently of state");
                if (!suppress) Check(f.Read(12) == 2 && Marshal.ReadByte(f.Manager, 16) == 1, "panic second category and special-lord flag reach original");
            }
            f.Clear(); f.Suppress = suppress; f.Set(Panic, 5); f.Write(32, 0);
            f.Tail(f.Manager, Player, 0, 1);
            Check(f.Get(Panic) == 0 && f.Read(0) == 0, "native panic clears when threat ends");

            f.Clear(); f.Suppress = suppress; f.Set(Panic, 3); f.Set(Gold, 3000); f.Write(32, 1);
            f.Tail(f.Manager, Player, 0, 1);
            Check(f.Read(24) == 1 && f.Read(28) == 300 && f.Get16(DonationTick) == 1 && f.Get(Panic) == 2,
                "native emergency donation amount, parity and countdown survive protection");
            Check(f.Read(0) == 0, "donation path has no demolition");

            f.Clear(); f.Suppress = suppress; f.Write(36, 1);
            f.Tail(f.Manager, Player, 0, 1);
            Check(f.Get(Nervous) == 8 && f.Read(20) == 0, "native nervous-phase entry remains available");
            foreach (int count in new[] { 8, 7, 6, 5, 4, 3, 2, 1 })
            {
                f.Clear(); f.Suppress = suppress; f.Set(Nervous, count); f.Write(36, 1);
                f.Tail(f.Manager, Player, 0, 1);
                Check(f.Get(Nervous) == count - 1 && f.Get(Urgency) == 20 && f.Get(Stance) == 1 && f.Get(Stance + 4) == 0,
                    "native nervous phase preserves urgency, stance and countdown stage " + count);
                int expectedCalls = count == 6 ? 3 : count >= 4 ? 1 : 0;
                Check(f.Read(0) == (suppress ? 0 : expectedCalls) && f.Read(4) == (suppress ? 0 : expectedCalls),
                    "all seven native category selections gate refund and delete stage " + count);
                if (!suppress && expectedCalls != 0)
                    Check(f.Read(12) == (count == 8 ? 1 : count == 7 ? 2 : count == 6 ? 5 : count == 5 ? 6 : 7), "native category argument stage " + count);
            }
            f.Clear(); f.Suppress = suppress; f.Set(Nervous, 6); f.Write(36, 0);
            f.Tail(f.Manager, Player, 0, 1);
            Check(f.Get(Nervous) == 0 && f.Read(0) == 0 && f.Get(Urgency) == 0, "native nervous state clears without demolition after threat ends");
            f.Clear(); f.Suppress = suppress; f.Set(Panic, 7); f.Set(Nervous, 6); f.Write(32, 1); f.Write(36, 1);
            f.Tail(f.Manager, Player, 0, 0);
            Check(f.Get(Panic) == 7 && f.Get(Nervous) == 6 && f.Read(20) == 0 && f.Read(0) == 0, "original gate-zero path remains unchanged");
        }
    }

    private static void Check(bool condition, string reason)
    {
        checks++;
        if (!condition) throw new InvalidOperationException("Emergency demolition: " + reason);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IntPtr image;
        private readonly NativeDetour<Demolition> hook;
        private readonly Demolition callback;
        internal readonly IntPtr Manager;
        internal readonly Demolition Demolish;
        internal readonly StrategyTail Tail;
        internal bool Suppress, ThrowPolicy;
        internal int Errors;
        internal Fixture(byte[] native)
        {
            image = VirtualAlloc(IntPtr.Zero, new UIntPtr(0x3800000u), 0x3000, 0x40);
            if (image == IntPtr.Zero) throw new InvalidOperationException("VirtualAlloc: " + Marshal.GetLastWin32Error());
            Manager = At(0x1000);
            try
            {
                Marshal.Copy(native, 0x2F454, At(0x2F454), 0x21C);
                var a = new Assembler(64);
                a.sub(rsp, 0x28); a.inc(__dword_ptr[rcx]); a.inc(__dword_ptr[rcx + 4]);
                a.mov(__dword_ptr[rcx + 8], edx); a.mov(__dword_ptr[rcx + 12], r8d); a.mov(__byte_ptr[rcx + 16], r9b);
                a.add(rsp, 0x28); a.pop(r14); a.pop(rdi); a.pop(rsi); a.pop(rbp); a.ret();
                Marshal.Copy(native, 0x2D810, At(0x2D810), 6); Emit(a, 0x2D816);
                a = new Assembler(64); a.mov(eax, __dword_ptr[rcx + 32]); a.ret(); Emit(a, 0x2B510);
                a = new Assembler(64); a.mov(eax, __dword_ptr[rcx + 36]); a.ret(); Emit(a, 0x2B490);
                a = new Assembler(64); a.inc(__dword_ptr[rcx + 20]); a.ret(); Emit(a, 0x3F3B0);
                a = new Assembler(64); a.inc(__dword_ptr[rcx + 24]); a.mov(__dword_ptr[rcx + 28], r8d); a.ret(); Emit(a, 0x30B90);
                a = new Assembler(64);
                a.mov(__byte_ptr[rsp + 0x18], r9b); a.push(rbx); a.push(rbp); a.push(rsi); a.push(rdi); a.push(r14); a.sub(rsp, 0x40);
                a.mov(__qword_ptr[rsp + 0x70], r12); a.mov(__qword_ptr[rsp + 0x78], r13); a.mov(__qword_ptr[rsp + 0x88], r15);
                a.movsxd(rsi, edx); a.imul(rbp, rsi, Stride); a.mov(r14, Address(0x366C210)); a.mov(rdi, rcx); a.mov(r12d, r8d); a.mov(r13d, 2);
                a.jmp(Address(0x2F454)); Emit(a, 0x2000);
                Tail = Marshal.GetDelegateForFunctionPointer<StrategyTail>(At(0x2000));
                AIEmergencyDemolitionDispatch dispatch = null;
                callback = (manager, player, category, flag) => dispatch.Invoke(manager, player, category, flag);
                var request = new DetourRequest<Demolition> { Name = "emergency demolition executable regression probe", TargetAddress = Address(0x2D810), Callback = callback };
                hook = AIEmergencyDemolitionNativeContract.Backend().CreateDetour(in request) as NativeDetour<Demolition>;
                Check(hook != null, "production backend creates real detour");
                AIEmergencyDemolitionNativeContract.Validate(hook, Address(0x2D810), false);
                dispatch = new AIEmergencyDemolitionDispatch(() => ThrowPolicy ? throw new InvalidOperationException("policy sentinel") : Suppress,
                    hook.Original, _ => Errors++);
                hook.Enable();
                AIEmergencyDemolitionNativeContract.Validate(hook, Address(0x2D810), true);
                Demolish = Marshal.GetDelegateForFunctionPointer<Demolition>(At(0x2D810));
            }
            catch { Dispose(); throw; }
        }
        private IntPtr At(int rva) => new IntPtr(image.ToInt64() + rva);
        private ulong Address(int rva) => unchecked((ulong)At(rva).ToInt64());
        private void Emit(Assembler assembler, int rva)
        {
            using (var stream = new MemoryStream())
            {
                if (!assembler.TryAssemble(new StreamCodeWriter(stream), Address(rva), out string error, out _)) throw new InvalidOperationException(error);
                byte[] code = stream.ToArray(); Marshal.Copy(code, 0, At(rva), code.Length);
            }
        }
        internal int Read(int offset) => Marshal.ReadInt32(Manager, offset);
        internal void Write(int offset, int value) => Marshal.WriteInt32(Manager, offset, value);
        internal int Get(int field) => Marshal.ReadInt32(At(field + Player * Stride));
        internal void Set(int field, int value) => Marshal.WriteInt32(At(field + Player * Stride), value);
        internal short Get16(int field) => Marshal.ReadInt16(At(field + Player * Stride));
        internal void Set16(int field, short value) => Marshal.WriteInt16(At(field + Player * Stride), value);
        internal void Clear()
        {
            for (int i = 0; i < 64; i += 4) Write(i, 0);
            foreach (int field in new[] { Panic, Nervous, Gold, Stance, Stance + 4, Urgency }) Set(field, 0);
            Set16(DonationTick, 0); Set16(DonationCount, 0); Suppress = false; ThrowPolicy = false; Errors = 0;
        }
        public void Dispose()
        {
            // This private copied page has no other callers or hooks, and execution has returned.
            hook?.Dispose();
            if (image != IntPtr.Zero) VirtualFree(image, UIntPtr.Zero, 0x8000);
            GC.KeepAlive(callback);
        }
    }
}
