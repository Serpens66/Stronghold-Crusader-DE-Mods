using APIShared;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace APISharedTests
{
    // Executes the PRODUCTIVE generators through the installed RedBird backend on a private
    // synthetic module. The only replacements are native continuation/coupling test endpoints.
    internal static partial class GateBridgeAutomationTests
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocation, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint kind);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Execute();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeCoupling(IntPtr manager, int buildingId, int open, int manualSource);
        private const int OutputRva = 0x220000;
        private const int WrapperRva = 0x210000;
        private const int QueryRva = 0x230000;
        private const int BuildingId = 7;
        private const int Slot = BuildingId * 0x32C;

        internal static void Run(Action<bool, string> assert, byte[] installedImage)
        {
            assert(IntPtr.Size == 8, "gate automation executable probes require x64");
            GatehouseAutomationNativeState.ValidateImage(installedImage, 0x180000000);
            IntPtr allocation = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x7000000, 0x3000, 0x40);
            if (allocation == IntPtr.Zero) throw new InvalidOperationException("Native gate test allocation failed.");
            ulong module = unchecked((ulong)allocation.ToInt64());
            var hooks = new List<X64InlineHook>();
            try
            {
                // Every native exit touched by a generated stub has an explicit, observable endpoint.
                int[] exits = { 0xB7A5F, 0xB7CB4, 0xB7A71, 0xB7C03, 0xB7C7F,
                    0xC53E7, 0xC54E8, 0xC54B0, 0xC54D8, 0xB7C39, 0xB7BE0 };
                for (int i = 0; i < exits.Length; i++)
                {
                    var endpoint = new Assembler(64);
                    endpoint.mov(__dword_ptr[r15 + 8], i + 1);
                    endpoint.ret();
                    Write(endpoint, module + (ulong)exits[i]);
                }
                var coupling = new Assembler(64);
                coupling.inc(__dword_ptr[r15 + 12]);
                coupling.mov(__dword_ptr[r15 + 16], edx);
                coupling.mov(__dword_ptr[r15 + 20], r8d);
                coupling.ret();
                Write(coupling, module + GatehouseAutomationNativeState.CouplingRva);
                var query = new Assembler(64);
                query.mov(__dword_ptr[r15 + 4], ecx);
                query.mov(eax, __dword_ptr[r15]);
                // Deliberately clobber volatile registers, flags and SIMD as a managed call may do.
                query.mov(ecx, -12); query.mov(edx, -13); query.mov(r8d, -14);
                query.mov(r9d, -15); query.mov(r10d, -16); query.mov(r11d, -17);
                query.xorps(xmm0, xmm0); query.xorps(xmm1, xmm1); query.xorps(xmm2, xmm2);
                query.xorps(xmm3, xmm3); query.xorps(xmm4, xmm4); query.xorps(xmm5, xmm5);
                query.cmp(ecx, edx);
                query.ret();
                Write(query, module + QueryRva);

                for (int i = 0; i < 4; i++)
                {
                    int site = i;
                    ulong address = module + (ulong)GatehouseAutomationNativeState.HookRvas[site];
                    byte[] bytes = GatehouseAutomationNativeState.HookBytes[site];
                    Marshal.Copy(bytes, 0, Ptr(address), bytes.Length);
                    var hook = new X64InlineHook(address, bytes.Length);
                    hooks.Add(hook);
                    assert(hook.DisplacedByteCount == bytes.Length, "real inline backend displacement matches gate contract");
                    hook.Generate((asm, displaced, continuation) =>
                    {
                        GatehouseAutomationNativeState.GenerateHook(
                            asm, displaced.ToArray(), site, module, module + QueryRva, continuation);
                        Program.AssembleAndDecode(asm, module + 0x300000 + (ulong)site * 0x10000);
                    });
                    hook.Enable();
                    assert(Marshal.ReadByte(Ptr(address)) == 0xFF && Marshal.ReadByte(Ptr(address + 1)) == 0x25,
                        "real inline backend uses the audited absolute jump");
                }
                IntPtr output = Ptr(module + OutputRva);
                Marshal.WriteInt32(output, 0x200, 200);
                Marshal.WriteInt32(output, 0x204, 1200);
                Marshal.WriteInt32(output, 0x208, 140);
                Marshal.WriteInt32(output, 0x20C, 100);
                Marshal.WriteIntPtr(output, 0x300, Ptr(module + OutputRva + 0x200));
                ulong decisionAddress = module + GatehousePermanentRuntimeState.DecisionHookRva;
                var decisionBytes = new byte[GatehousePermanentRuntimeState.DecisionDisplacedBytes];
                Buffer.BlockCopy(installedImage, GatehousePermanentRuntimeState.DecisionHookRva, decisionBytes, 0, decisionBytes.Length);
                Marshal.Copy(decisionBytes, 0, Ptr(decisionAddress), decisionBytes.Length);
                var decisionHook = new X64InlineHook(decisionAddress, decisionBytes.Length);
                hooks.Add(decisionHook);
                decisionHook.Generate((asm, displaced, continuation) =>
                {
                    GatehousePermanentRuntimeState.GenerateDecision(
                        asm, displaced.ToArray(), module + OutputRva + 0x300, module,
                        module + GatehousePermanentRuntimeState.DecisionReturnRva,
                        module + GatehousePermanentRuntimeState.ClosePathRva, module + QueryRva);
                    Program.AssembleAndDecode(asm, module + 0x340000);
                });
                decisionHook.Enable();
                assert(decisionHook.DisplacedByteCount == decisionBytes.Length, "decision backend span unchanged");

                foreach (bool manual in new[] { false, true })
                {
                    foreach (short timer in new short[] { -1, 0, 600 })
                    {
                        Probe(module, 0, manual, timer, false, 50);
                        assert(Marshal.ReadInt32(output, 8) == (manual ? 3 : timer < 0 ? 2 : 1),
                            "manual gate bypasses only native command cooldown, automatic gate preserves all branches");
                        AssertPreserved(assert, output);
                    }
                    foreach (short timer in new short[] { 0, 99 })
                    {
                        Probe(module, 1, manual, timer, false, 50);
                        assert(Marshal.ReadInt32(output, 8) == (manual ? 2 : timer == 0 ? 4 : 5),
                            "reopen decision preserves native automatic paths and exits manual gate before resynchronization");
                        assert(Marshal.ReadInt32(output, 12) == (manual && timer == 0 ? 1 : 0),
                            "manual gate forwards exactly one open command only when its native delay expires");
                        if (manual && timer == 0)
                            assert(Marshal.ReadInt32(output, 16) == BuildingId && Marshal.ReadInt32(output, 20) == 1,
                                "reopen forwarding uses the original gate ID and native open argument");
                    }
                    foreach (int site in new[] { 2, 3 })
                    {
                        Probe(module, site, manual, 0, false, 50);
                        assert(Marshal.ReadInt32(output, 8) == (site == 2 ? manual ? 7 : 6 : manual ? 9 : 8),
                            "manual linked recipient skips all command writes while automatic recipient uses Vanilla");
                        AssertPreserved(assert, output);
                    }
                    foreach (bool human in new[] { false, true })
                    {
                        Probe(module, 4, manual, 0, human, 50);
                        assert(Marshal.ReadInt32(output, 8) == (manual ? 2 : 10),
                            "manual gate exits before Fixes; automatic gate still enters Fixes/Vanilla close path");
                        assert(Marshal.ReadInt32(output, 12) == (manual ? 1 : 0), "close is forwarded exactly once");
                        if (manual)
                        {
                            assert(Marshal.ReadInt32(output, 16) == BuildingId && Marshal.ReadInt32(output, 20) == 0,
                                "close forwarding retains one-based source ID and close argument");
                            assert(Marshal.ReadInt16(Ptr(module + GatehouseAutomationNativeState.ReopenTimerRva + Slot)) ==
                                (human ? 100 : 1200), "manual bridge control retains selected native human/AI delay");
                        }
                        Probe(module, 4, manual, 0, human, human ? 140 : 200);
                        assert(Marshal.ReadInt32(output, 8) == 11 && Marshal.ReadInt32(output, 12) == 0,
                            "distance equality retains Vanilla's strict less-than predicate");
                    }
                }
                Console.WriteLine("PASS: gate/bridge productive native stubs executed through RedBird (cooldowns, recipient filtering, delay, distance boundaries, ABI/SIMD).");
            }
            finally
            {
                // Private test buffers were never published to a game process.
                for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
                VirtualFree(allocation, UIntPtr.Zero, 0x8000);
            }
            RunCompleteCoupling(assert, installedImage);
            RunDelayFlow(assert, installedImage);
        }

        private static void RunCompleteCoupling(Action<bool, string> assert, byte[] installedImage)
        {
            IntPtr allocation = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x7000000, 0x3000, 0x40);
            if (allocation == IntPtr.Zero) throw new InvalidOperationException("Coupling test allocation failed.");
            ulong module = unchecked((ulong)allocation.ToInt64());
            var hooks = new List<X64InlineHook>();
            const int gateId = 7, firstBridgeId = 11, secondBridgeId = 12;
            const int managerRva = 0x64CCBB0;
            try
            {
                // Execute the COMPLETE hash-validated Vanilla function, including both loop
                // iterations and every command/timer write. Only the spatial lookup is a fixture.
                Marshal.Copy(installedImage, GatehouseAutomationNativeState.CouplingRva,
                    Ptr(module + GatehouseAutomationNativeState.CouplingRva), 519);
                var lookup = new Assembler(64);
                lookup.mov(r11, module + OutputRva);
                lookup.inc(__dword_ptr[r11]);
                lookup.cmp(__dword_ptr[r11], 1);
                Label second = lookup.CreateLabel();
                lookup.jne(second);
                lookup.mov(eax, __dword_ptr[r11 + 16]);
                lookup.ret();
                lookup.Label(ref second);
                lookup.mov(eax, __dword_ptr[r11 + 20]);
                lookup.ret();
                Write(lookup, module + 0xB9330);
                var query = new Assembler(64);
                query.mov(r11, module + OutputRva);
                query.inc(__dword_ptr[r11 + 24]);
                query.movsxd(rax, ecx);
                query.mov(eax, __dword_ptr[r11 + rax * 4 + 0x100]);
                query.mov(ecx, -12); query.mov(edx, -13); query.mov(r8d, -14);
                query.mov(r9d, -15); query.mov(r10d, -16); query.mov(r11d, -17);
                query.xorps(xmm0, xmm0); query.cmp(ecx, edx); query.ret();
                Write(query, module + QueryRva);
                for (int site = 2; site < 4; site++)
                {
                    int captured = site;
                    var hook = new X64InlineHook(module + (ulong)GatehouseAutomationNativeState.HookRvas[site],
                        GatehouseAutomationNativeState.HookBytes[site].Length);
                    hooks.Add(hook);
                    hook.Generate((asm, displaced, continuation) => GatehouseAutomationNativeState.GenerateHook(
                        asm, displaced.ToArray(), captured, module, module + QueryRva, continuation));
                    assert(hook.DisplacedByteCount == GatehouseAutomationNativeState.HookBytes[site].Length,
                        "full coupling backend displacement matches audited span");
                    hook.Enable();
                }
                IntPtr output = Ptr(module + OutputRva);
                IntPtr manager = Ptr(module + managerRva);
                IntPtr gate = Ptr(module + managerRva + gateId * 0x32C);
                IntPtr firstBridge = Ptr(module + managerRva + firstBridgeId * 0x32C);
                IntPtr secondBridge = Ptr(module + managerRva + secondBridgeId * 0x32C);
                Marshal.WriteInt16(gate, 0x12E, (short)eStructs.STRUCT_GATE_MAIN);
                Marshal.WriteInt16(firstBridge, 0x12E, (short)eStructs.STRUCT_DRAWBRIDGE);
                Marshal.WriteInt16(secondBridge, 0x12E, (short)eStructs.STRUCT_DRAWBRIDGE);
                NativeCoupling call = Marshal.GetDelegateForFunctionPointer<NativeCoupling>(
                    Ptr(module + GatehouseAutomationNativeState.CouplingRva));
                int cases = 0;
                for (int mask = 0; mask < 8; mask++)
                for (int open = 0; open < 2; open++)
                for (byte state = 0; state < 4; state++)
                {
                    bool manualGate = (mask & 1) != 0;
                    bool manualFirst = (mask & 2) != 0;
                    bool manualSecond = (mask & 4) != 0;
                    Marshal.WriteInt32(output, 0x100 + gateId * 4, manualGate ? 1 : 0);
                    Marshal.WriteInt32(output, 0x100 + firstBridgeId * 4, manualFirst ? 1 : 0);
                    Marshal.WriteInt32(output, 0x100 + secondBridgeId * 4, manualSecond ? 1 : 0);
                    // Gate -> two independently configured bridges, with different animation states.
                    Marshal.WriteInt32(output, 0, 0); Marshal.WriteInt32(output, 24, 0);
                    Marshal.WriteInt32(output, 16, firstBridgeId); Marshal.WriteInt32(output, 20, secondBridgeId);
                    Marshal.WriteByte(firstBridge, 0x2F0, state);
                    Marshal.WriteByte(secondBridge, 0x2F0, (byte)(3 - state));
                    Marshal.WriteByte(firstBridge, 0x2EF, 0x55); Marshal.WriteByte(secondBridge, 0x2EF, 0x55);
                    call(manager, gateId, open, 0);
                    assert(Marshal.ReadByte(firstBridge, 0x2EF) == ExpectedBridgeCommand(state, open, manualFirst),
                        "complete Vanilla coupling filters first bridge before any command write");
                    assert(Marshal.ReadByte(secondBridge, 0x2EF) == ExpectedBridgeCommand(3 - state, open, manualSecond),
                        "complete Vanilla loop reaches automatic second bridge after manual first bridge");
                    assert(Marshal.ReadInt32(output, 24) == 2, "both recipients are queried exactly once");
                    cases++;
                    // Bridge -> gate: both native source modes, including fallback to inner gate lookup.
                    for (int manualSource = 0; manualSource < 2; manualSource++)
                    for (int fallback = 0; fallback < 2; fallback++)
                    {
                        Marshal.WriteInt32(output, 0, 0); Marshal.WriteInt32(output, 24, 0);
                        Marshal.WriteInt32(output, 16, fallback == 0 ? gateId : 0);
                        Marshal.WriteInt32(output, 20, gateId);
                        Marshal.WriteByte(gate, 0x2FE, state); Marshal.WriteByte(gate, 0x2FF, 0x55);
                        Marshal.WriteInt16(gate, 0x314, 321);
                        call(manager, firstBridgeId, open, manualSource);
                        bool write = !manualGate && (open == 1 ? state == 2 : state == 0);
                        assert(Marshal.ReadByte(gate, 0x2FF) == (write ? open == 1 ? 11 : 10 : 0x55),
                            "complete Vanilla bridge-to-gate writes respect receiver mode and state");
                        assert(Marshal.ReadInt16(gate, 0x314) == (write && manualSource == 1 ? open == 1 ? 600 : -1 : 321),
                            "manual gate timer remains untouched by linked commands in both source modes");
                        assert(Marshal.ReadInt32(output, 24) == 1, "linked gate queried exactly once after either lookup");
                        cases++;
                    }
                }
                // No linked recipient: no policy query and no invented standalone automation.
                foreach (int source in new[] { gateId, firstBridgeId })
                {
                    Marshal.WriteInt32(output, 0, 0); Marshal.WriteInt32(output, 24, 0);
                    Marshal.WriteInt32(output, 16, 0); Marshal.WriteInt32(output, 20, 0);
                    call(manager, source, 0, 1);
                    assert(Marshal.ReadInt32(output, 24) == 0, "unlinked buildings receive no invented target");
                }
                Console.WriteLine($"PASS: complete Vanilla coupling executed ({cases} mixed-mode/state/source/fallback cases, two bridges and no-link paths).");
            }
            finally
            {
                for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
                VirtualFree(allocation, UIntPtr.Zero, 0x8000);
            }
        }

        private static int ExpectedBridgeCommand(int state, int open, bool manual) =>
            manual ? 0x55 : open == 1 ? (state == 1 || state == 2 ? 11 : 0x55) :
                (state == 0 || state == 3 ? 10 : 0x55);

        private static void Probe(ulong module, int site, bool manual, short timer, bool human, int distance)
        {
            IntPtr output = Ptr(module + OutputRva);
            for (int i = 0; i < 128; i += 4) Marshal.WriteInt32(output, i, 0);
            Marshal.WriteInt32(output, manual ? 1 : 0);
            Marshal.WriteInt16(Ptr(module + 0x64CCEC4 + Slot), timer);
            Marshal.WriteInt16(Ptr(module + GatehouseAutomationNativeState.ReopenTimerRva + Slot), timer);
            var wrapper = new Assembler(64);
            wrapper.push(rbx); wrapper.push(rbp); wrapper.push(rsi); wrapper.push(rdi);
            wrapper.push(r12); wrapper.push(r13); wrapper.push(r14); wrapper.push(r15);
            // Enter the inline body with RSP%16 == 0, just like the native parent functions.
            wrapper.sub(rsp, 48);
            wrapper.mov(r15, module + OutputRva);
            wrapper.mov(rbx, Slot); wrapper.mov(rbp, module); wrapper.mov(r13, module + 0x64CCBB0);
            wrapper.mov(r10d, BuildingId); wrapper.mov(ecx, BuildingId); wrapper.mov(eax, BuildingId);
            wrapper.mov(r8d, distance); wrapper.mov(r9d, 0x55667788); wrapper.mov(r11d, 0x12345678);
            wrapper.mov(esi, human ? 1 : 0); wrapper.xor(edi, edi);
            wrapper.pcmpeqd(xmm0, xmm0);
            wrapper.call(module + (ulong)(site < 4 ? GatehouseAutomationNativeState.HookRvas[site] : GatehousePermanentRuntimeState.DecisionHookRva));
            wrapper.mov(__qword_ptr[r15 + 24], r8); wrapper.mov(__qword_ptr[r15 + 32], r9);
            wrapper.mov(__qword_ptr[r15 + 40], r10); wrapper.mov(__qword_ptr[r15 + 48], r11);
            wrapper.movdqu(__xmmword_ptr[r15 + 64], xmm0);
            wrapper.add(rsp, 48);
            wrapper.pop(r15); wrapper.pop(r14); wrapper.pop(r13); wrapper.pop(r12);
            wrapper.pop(rdi); wrapper.pop(rsi); wrapper.pop(rbp); wrapper.pop(rbx); wrapper.ret();
            Write(wrapper, module + WrapperRva);
            Marshal.GetDelegateForFunctionPointer<Execute>(Ptr(module + WrapperRva))();
        }

        private static void AssertPreserved(Action<bool, string> assert, IntPtr output)
        {
            assert(Marshal.ReadInt32(output, 4) == BuildingId, "callback receives the exact one-based destination ID");
            assert(Marshal.ReadInt64(output, 24) == 50 && Marshal.ReadInt64(output, 32) == 0x55667788 && Marshal.ReadInt64(output, 40) == BuildingId &&
                Marshal.ReadInt64(output, 48) == 0x12345678, "live volatile registers survive native callback");
            assert(Marshal.ReadInt64(output, 64) == -1 && Marshal.ReadInt64(output, 72) == -1,
                "live XMM state survives native callback");
        }

        private static void Write(Assembler asm, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                asm.Assemble(new StreamCodeWriter(stream), address);
                byte[] bytes = stream.ToArray();
                Marshal.Copy(bytes, 0, Ptr(address), bytes.Length);
            }
        }
        private static IntPtr Ptr(ulong address) => new IntPtr(unchecked((long)address));
    }
}
