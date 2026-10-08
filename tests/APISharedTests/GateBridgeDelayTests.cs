using APIShared;
using Iced.Intel;
using RedBird.X64.Hooks;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace APISharedTests
{
    internal static partial class GateBridgeAutomationTests
    {
        private static void RunDelayFlow(Action<bool, string> assert, byte[] installedImage)
        {
            // Includes native player-role tables at RVA 8574xxx.
            IntPtr allocation = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x9000000, 0x3000, 0x40);
            if (allocation == IntPtr.Zero) throw new InvalidOperationException("Delay flow allocation failed.");
            ulong module = unchecked((ulong)allocation.ToInt64());
            var hooks = new List<X64InlineHook>();
            const int gateId = 7, bridgeId = 11, otherBridgeId = 12, owner = 1, globalId = 123;
            const int managerRva = 0x64CCBB0;
            IntPtr output = Ptr(module + OutputRva);
            IntPtr gate = Ptr(module + managerRva + gateId * 0x32C);
            IntPtr bridge = Ptr(module + managerRva + bridgeId * 0x32C);
            IntPtr otherBridge = Ptr(module + managerRva + otherBridgeId * 0x32C);
            try
            {
                foreach (var range in new[] { new[] { 0xD5810, 96 }, new[] { 0xB73D0, 2325 }, new[] { 0xC5300, 519 } })
                    Marshal.Copy(installedImage, range[0], Ptr(module + (ulong)range[0]), range[1]);
                // Skip capture and the original function prologue/epilogue. The wrapper supplies
                // the audited frame for the COMPLETE automatic section B7992..B7CB4.
                var exit = new Assembler(64);
                exit.jmp(module + WrapperRva + 0x1000);
                Write(exit, module + 0xB7CB4);
                var lookup = new Assembler(64);
                lookup.mov(r11, module + OutputRva);
                lookup.inc(__dword_ptr[r11]);
                lookup.mov(eax, __dword_ptr[r11]); lookup.and(eax, 1);
                Label second = lookup.CreateLabel(); lookup.je(second);
                lookup.mov(eax, bridgeId); lookup.ret();
                lookup.Label(ref second); lookup.mov(eax, otherBridgeId); lookup.ret();
                Write(lookup, module + 0xB9330);
                var query = new Assembler(64);
                query.mov(r11, module + OutputRva);
                query.movsxd(rax, ecx);
                query.mov(eax, __dword_ptr[r11 + rax * 4 + 0x100]);
                // Destroy all volatile state except the return value; productive stubs must preserve it.
                query.mov(ecx, -1); query.mov(edx, -2); query.mov(r8d, -3);
                query.mov(r9d, -4); query.mov(r10d, -5); query.mov(r11d, -6);
                query.xorps(xmm0, xmm0); query.test(ecx, ecx); query.ret();
                Write(query, module + QueryRva);
                Marshal.WriteInt32(output, 0x200, 200); Marshal.WriteInt32(output, 0x204, 9);
                Marshal.WriteInt32(output, 0x208, 140); Marshal.WriteInt32(output, 0x20C, 5);
                Marshal.WriteIntPtr(output, 0x300, Ptr(module + OutputRva + 0x200));
                for (int site = 0; site < GatehouseAutomationNativeState.HookRvas.Length; site++)
                {
                    int captured = site;
                    var hook = new X64InlineHook(module + (ulong)GatehouseAutomationNativeState.HookRvas[site],
                        GatehouseAutomationNativeState.HookBytes[site].Length);
                    hooks.Add(hook);
                    hook.Generate((asm, displaced, continuation) =>
                    {
                        GatehouseAutomationNativeState.GenerateHook(asm, displaced.ToArray(), captured, module,
                            module + QueryRva, continuation, module + OutputRva + 0x300);
                        RuntimeTests.AssembleAndDecode(asm, module + 0x300000 + (ulong)captured * 0x10000);
                    });
                    assert(hook.DisplacedByteCount == GatehouseAutomationNativeState.HookBytes[site].Length,
                        "delay flow backend displacement matches complete native span");
                    hook.Enable();
                }
                var decision = new X64InlineHook(module + GatehousePermanentRuntimeState.DecisionHookRva,
                    GatehousePermanentRuntimeState.DecisionDisplacedBytes);
                hooks.Add(decision);
                decision.Generate((asm, displaced, continuation) => GatehousePermanentRuntimeState.GenerateDecision(
                    asm, displaced.ToArray(), module + OutputRva + 0x300, module,
                    module + GatehousePermanentRuntimeState.DecisionReturnRva,
                    module + GatehousePermanentRuntimeState.ClosePathRva, module + QueryRva));
                decision.Enable();

                Marshal.WriteInt16(gate, 0x12E, (short)eStructs.STRUCT_GATE_MAIN);
                Marshal.WriteInt16(gate, 0x132, owner); Marshal.WriteInt32(gate, 0x134, globalId);
                Marshal.WriteByte(gate, 0x2FE, 2); // Already closed gate; still forward direct commands.
                Marshal.WriteInt16(bridge, 0x12E, (short)eStructs.STRUCT_DRAWBRIDGE);
                Marshal.WriteInt16(otherBridge, 0x12E, (short)eStructs.STRUCT_DRAWBRIDGE);
                Marshal.WriteInt32(output, 0x100 + gateId * 4, 1);
                Marshal.WriteInt32(output, 0x100 + otherBridgeId * 4, 1); // Second bridge is manual.
                var direct = Marshal.GetDelegateForFunctionPointer<NativeCoupling>(Ptr(module + 0xD5810));
                Execute tick = CreateAutomaticSectionWrapper(module, gateId, owner);
                Action<int> enemies = count => Marshal.WriteInt32(Ptr(module + 0x379CE58 + owner * 0x583C), count);
                Action<int, int, int, int> role = (mode, control, enabled, special) =>
                {
                    Marshal.WriteInt32(Ptr(module + 0x8574B90), mode);
                    Marshal.WriteInt32(Ptr(module + 0x8574BCC + owner * 4), control);
                    Marshal.WriteInt32(Ptr(module + 0x8574C44 + owner * 4), enabled);
                    Marshal.WriteInt32(Ptr(module + 0x3669040), special);
                };
                // An actual native candidate, not a replacement enemy predicate.
                Marshal.WriteInt16(Ptr(module + 0x38C19E8 + owner * 0x4E20), 1);
                Marshal.WriteInt32(Ptr(module + 0x38ED908 + owner * 0x9C40), 456);
                Marshal.WriteInt32(Ptr(module + 0x67E8AF0 + 0x490), 456);
                Marshal.WriteInt16(Ptr(module + 0x67E8AE4 + 0x490), (short)AliveState.IsAlive);
                Marshal.WriteInt16(Ptr(module + 0x67E8CFC + 0x490), owner);
                // All branches of Vanilla's human-delay selection, including mode 99 exception.
                foreach (var fixture in new[] {
                    new[] { 0, -1, 1, 0, 9 }, new[] { 1, 0, 1, 0, 9 },
                    new[] { 1, -1, 0, 0, 9 }, new[] { 1, -1, 1, 0, 5 },
                    new[] { 99, -1, 1, 1, 9 }, new[] { 99, -1, 1, 0, 5 } })
                {
                    role(fixture[0], fixture[1], fixture[2], fixture[3]); enemies(0);
                    Marshal.WriteByte(bridge, 0x2F0, 0); Marshal.WriteByte(bridge, 0x2EF, 0x55);
                    Marshal.WriteByte(otherBridge, 0x2F0, 0); Marshal.WriteByte(otherBridge, 0x2EF, 0x55);
                    direct(Ptr(module + managerRva), gateId, 10, globalId);
                    assert(Marshal.ReadInt16(gate, 0x31C) == fixture[4], "direct close chooses the exact configured native role delay");
                    assert(Marshal.ReadByte(bridge, 0x2EF) == 10 && Marshal.ReadByte(otherBridge, 0x2EF) == 0x55,
                        "direct close immediately forwards only to automatic bridge");
                    Marshal.WriteByte(bridge, 0x2F0, 2); // Native animation completion fixture.
                    Marshal.WriteByte(bridge, 0x2EF, 0x55);
                    for (int remaining = fixture[4] - 1; remaining >= 0; remaining--)
                    {
                        tick();
                        assert(Marshal.ReadInt16(gate, 0x31C) == remaining, "empty enemy list does not cancel manual-gate countdown");
                        assert(Marshal.ReadByte(bridge, 0x2EF) == (remaining == 0 ? 11 : 0x55),
                            "automatic bridge opens only at expiry; closed manual gate cannot resynchronize it");
                        assert(Marshal.ReadByte(otherBridge, 0x2EF) == 0x55, "manual second bridge stays untouched throughout countdown");
                    }
                    // Native enemy scan and the existing decision hook renew the SAME timer.
                    enemies(1); tick();
                    assert(Marshal.ReadInt16(gate, 0x31C) == fixture[4], "native enemy decision uses the same role selection and renews delay");
                    enemies(0); tick();
                    assert(Marshal.ReadInt16(gate, 0x31C) == fixture[4] - 1, "enemy disappearance preserves remaining delay");
                    direct(Ptr(module + managerRva), gateId, 10, globalId);
                    assert(Marshal.ReadInt16(gate, 0x31C) == fixture[4], "repeated direct close restarts configured delay");
                }
                role(1, -1, 1, 0); enemies(0);
                direct(Ptr(module + managerRva), gateId, 10, globalId);
                tick();
                byte[] savedBuilding = new byte[0x32C];
                Marshal.Copy(gate, savedBuilding, 0, savedBuilding.Length);
                Marshal.WriteInt16(gate, 0x31C, 0);
                Marshal.Copy(savedBuilding, 0, gate, savedBuilding.Length);
                tick();
                assert(Marshal.ReadInt16(gate, 0x31C) == 3,
                    "restored native building record retains countdown with no external timer state");
                Marshal.WriteInt16(gate, 0x31C, 3);
                Marshal.WriteByte(gate, 0x2FF, 0x55);
                direct(Ptr(module + managerRva), gateId, 10, globalId + 1);
                assert(Marshal.ReadInt16(gate, 0x31C) == 3 && Marshal.ReadByte(gate, 0x2FF) == 0x55,
                    "invalid/recycled Global-ID never reaches delay hook or command write");
                direct(Ptr(module + managerRva), gateId, 11, globalId);
                assert(Marshal.ReadInt16(gate, 0x31C) == 0 && Marshal.ReadInt16(gate, 0x314) == 600,
                    "direct opening preserves Vanilla timers without new delay");
                Marshal.WriteInt32(output, 0x20C, 0);
                direct(Ptr(module + managerRva), gateId, 10, globalId);
                tick();
                assert(Marshal.ReadInt16(gate, 0x31C) == 0 && Marshal.ReadByte(bridge, 0x2EF) == 11,
                    "zero delay opens the automatic bridge at the next regular native check");
                Marshal.WriteInt32(output, 0x100 + gateId * 4, 0); // Disabled policy / automatic gate.
                Marshal.WriteInt16(gate, 0x31C, 3);
                direct(Ptr(module + managerRva), gateId, 10, globalId);
                assert(Marshal.ReadInt16(gate, 0x31C) == 0, "automatic/inactive direct command preserves Vanilla reset");
                Marshal.WriteInt16(gate, 0x31C, 3); tick();
                assert(Marshal.ReadInt16(gate, 0x31C) == 0, "automatic/inactive empty-list shortcut remains Vanilla");
                Console.WriteLine("PASS: direct native command -> native countdown/enemy scan -> native coupling (role branches, expiry, enemies, repeated close, open, inactive, Global-ID).");
            }
            finally
            {
                for (int index = hooks.Count - 1; index >= 0; index--) hooks[index].Dispose();
                VirtualFree(allocation, UIntPtr.Zero, 0x8000);
            }
        }

        private static Execute CreateAutomaticSectionWrapper(ulong module, int gateId, int owner)
        {
            var asm = new Assembler(64);
            asm.push(rbx); asm.push(rbp); asm.push(rsi); asm.push(rdi);
            asm.push(r12); asm.push(r13); asm.push(r14); asm.push(r15);
            // Jump into an aligned BODY frame, not CALL: C5300 legitimately writes to its
            // caller's home area. An artificial return address at body RSP would be overwritten.
            asm.sub(rsp, 0xB8);
            asm.mov(__word_ptr[rsp + 0x64], (short)owner);
            asm.mov(__dword_ptr[rsp + 0x6C], 0);
            asm.mov(rbp, module); asm.mov(r13, module + 0x64CCBB0);
            asm.mov(r10d, gateId); asm.xor(r11d, r11d); asm.xor(r12d, r12d); asm.xor(r14d, r14d);
            asm.jmp(module + 0xB7992);
            Write(asm, module + WrapperRva);
            var epilogue = new Assembler(64);
            epilogue.add(rsp, 0xB8);
            epilogue.pop(r15); epilogue.pop(r14); epilogue.pop(r13); epilogue.pop(r12);
            epilogue.pop(rdi); epilogue.pop(rsi); epilogue.pop(rbp); epilogue.pop(rbx); epilogue.ret();
            Write(epilogue, module + WrapperRva + 0x1000);
            return Marshal.GetDelegateForFunctionPointer<Execute>(Ptr(module + WrapperRva));
        }
    }
}
