using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.LordTeleportGuard
{
    // All offsets are manager-relative, including reserved unit slot zero.
    internal static class LordTeleportNative
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int EntryRva = 0x19BA40, ResumeRva = 0x19BA50, DisplacedLength = 16;
        internal const int StandingRescueCallRva = 0x18300F, StandingRescueReturnRva = StandingRescueCallRva + 5;
        internal const int UnitsRva = 0x67E8400, AiLineUpRva = 0x8574C44;
        internal const int RecordOrigin = 0x65C, Stride = GameUnitManager.UnitRecordByteSize;
        internal static readonly byte[] EntryBytes = {
            0x48,0x89,0x6C,0x24,0x18,0x56,0x57,0x41,0x56,0x48,0x83,0xEC,0x20,0x49,0x63,0xF0 };
        // Longer than the overwritten span; used only for executable-section uniqueness evidence.
        internal static readonly byte[] EntrySignature = {
            0x48,0x89,0x6C,0x24,0x18,0x56,0x57,0x41,0x56,0x48,0x83,0xEC,0x20,0x49,0x63,0xF0,
            0x4C,0x8B,0xF1,0x49,0x63,0xF9,0x48,0x63,0xEA,0x81,0xFE,0x1F,0x03,0,0 };

        internal static bool ShouldEnable(bool installed, bool modEnabled, bool optionEnabled) =>
            installed && modEnabled && optionEnabled;

        internal static Instruction[] Decode(ulong address)
        {
            var d = Decoder.Create(64, new ByteArrayCodeReader(EntryBytes));
            d.IP = address;
            var result = new Instruction[6];
            for (int i = 0; i < result.Length; i++) result[i] = d.Decode();
            return result;
        }

        internal static void ValidateInstructions(ReadOnlySpan<Instruction> instructions, ulong resume)
        {
            var expected = Decode(resume - DisplacedLength);
            if (instructions.Length != expected.Length) throw new InvalidOperationException("Teleport hook instruction count changed.");
            int length = 0;
            for (int i = 0; i < expected.Length; i++)
            {
                if (instructions[i] != expected[i]) throw new InvalidOperationException("Teleport hook instruction contract changed.");
                length += instructions[i].Length;
            }
            if (length != DisplacedLength) throw new InvalidOperationException("Teleport hook span changed.");
        }

        internal static void ValidateStandingRescueCall(ReadOnlySpan<byte> bytes, ulong image)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes.ToArray()));
            decoder.IP = image + StandingRescueCallRva;
            Instruction call = decoder.Decode();
            if (bytes.Length != 5 || call.Length != 5 || call.Mnemonic != Mnemonic.Call ||
                call.Op0Kind != OpKind.NearBranch64 || call.NearBranchTarget != image + EntryRva ||
                call.NextIP != image + StandingRescueReturnRva)
                throw new InvalidOperationException("Standing-tile rescue caller contract changed.");
        }

        internal static void ValidateLayouts()
        {
            // These literals are the hash-bound native instruction/allocation contract, not gameplay defaults.
            if (Stride != 0x490 || GameUnitManager.NativeUnitSlotCount != 10000 ||
                GameUnitManager.FirstLiveUnitId != 1 || GameUnitManager.LastLiveUnitId != 9999 ||
                (int)eChimps.CHIMP_TYPE_LORD != 0x37 || (int)AliveState.IsAlive != 2 ||
                (int)AILords.SK_NULL != 0 || GamePlayerManagerAPI.MAX_PLAYERS != 8)
                throw new InvalidOperationException("Installed SDK constants differ from the audited native contract.");
            if (Marshal.SizeOf(typeof(GameUnit)) != Stride) throw new InvalidOperationException("Unit stride changed.");
            Offset(typeof(GameUnitManager), nameof(GameUnitManager.GameUnitArray), RecordOrigin);
            Offset(typeof(GameUnit), nameof(GameUnit.r_AliveState), 0x88);
            Offset(typeof(GameUnit), nameof(GameUnit.r_UnitChimp), 0x8A);
            Offset(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId), 0x92);
            Offset(typeof(GameUnit), nameof(GameUnit.r_GlobalId), 0x94);
            Offset(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX), 0xC0);
            Offset(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionY), 0xC2);
            Offset(typeof(GameUnit), nameof(GameUnit.r_HeightElevation), 0xB6);
            Offset(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingTileEndX), 0x336);
            Offset(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingTileEndY), 0x338);
            Offset(typeof(GameUnit), nameof(GameUnit.r_AIState), 0x2BC);
        }

        private static void Offset(Type type, string field, int value)
        {
            if (Marshal.OffsetOf(type, field).ToInt32() != value)
                throw new InvalidOperationException("Unsupported field layout: " + type.Name + "." + field);
        }

        internal static void Emit(Assembler a, ReadOnlySpan<Instruction> original,
            ulong enabled, ulong units, ulong aiLineUp)
        {
            Label vanilla = a.CreateLabel();
            // No calls or SIMD operations. Both exits restore every scratch register and the flags.
            a.pushfq(); a.push(rax); a.push(r10); a.push(r11);
            a.mov(r10, enabled); a.cmp(__dword_ptr[r10], 0); a.je(vanilla);
            // Four pushes place the original return address at rsp+32. Preserve Vanilla's terrain rescue.
            a.mov(r10, units - UnitsRva + StandingRescueReturnRva);
            a.cmp(__qword_ptr[rsp + 32], r10); a.je(vanilla);
            a.mov(r10, units); a.cmp(rcx, r10); a.jne(vanilla);
            a.cmp(edx, GameUnitManager.FirstLiveUnitId); a.jl(vanilla);
            a.cmp(edx, GameUnitManager.LastLiveUnitId); a.jg(vanilla);
            a.movsxd(rax, edx); a.imul(rax, rax, Stride); a.add(rax, rcx);
            a.cmp(__word_ptr[rax + 0x6E4], (int)AliveState.IsAlive); a.jne(vanilla);
            a.cmp(__word_ptr[rax + 0x6E6], (int)eChimps.CHIMP_TYPE_LORD); a.jne(vanilla);
            a.movsx(r11d, __word_ptr[rax + 0x6EE]);
            a.cmp(r11d, 1); a.jl(vanilla); a.cmp(r11d, GamePlayerManagerAPI.MAX_PLAYERS); a.jg(vanilla);
            a.mov(r10, aiLineUp); a.cmp(__dword_ptr[r10 + r11 * 4], (int)AILords.SK_NULL); a.jne(vanilla);
            a.movsx(r10d, __word_ptr[rax + 0x992]); a.cmp(r8d, r10d); a.jne(vanilla);
            a.movsx(r10d, __word_ptr[rax + 0x994]); a.cmp(r9d, r10d); a.jne(vanilla);
            Restore(a); a.ret(); // Void function: skip all position and occupancy writes.
            a.Label(ref vanilla);
            Restore(a);
            foreach (Instruction instruction in original.CloneInstructionsWithoutIP()) a.AddInstruction(instruction);
            // X64InlineHook.Generate adds exactly one jump to the original continuation.
        }

        private static void Restore(Assembler a)
        {
            a.pop(r11); a.pop(r10); a.pop(rax); a.popfq();
        }


        internal static byte[] Assemble(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), address, out string error, out _))
                    throw new InvalidOperationException("Teleport guard assembly failed: " + error);
                byte[] bytes = stream.ToArray();
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
                while (decoder.IP < address + (ulong)bytes.Length)
                {
                    Instruction i = decoder.Decode();
                    if (i.IsInvalid) throw new InvalidOperationException("Invalid guard instruction at 0x" + i.IP.ToString("X"));
                    // RedBird's terminal FF 25 00000000 is followed by an eight-byte address, not code.
                    if (i.Mnemonic == Mnemonic.Jmp && i.IsIPRelativeMemoryOperand &&
                        i.IPRelativeMemoryAddress == i.NextIP && i.NextIP + 8 == address + (ulong)bytes.Length) break;
                }
                return bytes;
            }
        }
    }
}
