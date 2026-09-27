using Iced.Intel;
using RedBird.X64.Extensions;
using System;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace DamagedHealthBarsTest
{
    internal static class HealthBarNativeContract
    {
        internal const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int BuildingNorthRva = 0x489C4;
        internal const int BuildingSouthRva = 0x4F9B4;
        internal const int UnitRva = 0x1A1945;
        internal const int BuildingLength = 17;
        internal const int UnitLength = 24;

        // Exact instruction spans from the installed DLL, not AOB guesses.
        private static readonly byte[] BuildingNorthBytes = Hex("833D0DD561031075083B3DC1F979067420");
        private static readonly byte[] BuildingSouthBytes = Hex("833D1D6561031075083B35D1897906741F");
        private static readonly byte[] UnitBytes = Hex("6639B42F8C8A7E0675224038B42FB48E7E060F8439020000");

        private const int BuildingCurrentHealth = 0x64CCD18;
        private const int BuildingMaxHealth = 0x64CCD1A;
        private const int UnitCurrentHealth = 0x67E8E20;
        private const int UnitMaxHealth = 0x67E8E24;

        internal static void ValidateBytes(ReadOnlySpan<byte> memory)
        {
            Match(memory, BuildingNorthRva, BuildingNorthBytes, "building north health gate");
            Match(memory, BuildingSouthRva, BuildingSouthBytes, "building south health gate");
            Match(memory, UnitRva, UnitBytes, "unit health gate");
        }

        internal static void ValidateLiveBytes(ulong imageBase)
        {
            MatchLive(imageBase, BuildingNorthRva, BuildingNorthBytes, "building north health gate");
            MatchLive(imageBase, BuildingSouthRva, BuildingSouthBytes, "building south health gate");
            MatchLive(imageBase, UnitRva, UnitBytes, "unit health gate");
        }

        private static void MatchLive(ulong imageBase, int rva, byte[] expected, string name)
        {
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(new IntPtr(unchecked((long)(imageBase + (ulong)rva))), actual, 0, actual.Length);
            Match(actual, 0, expected, name + " (live)");
        }

        private static void Match(ReadOnlySpan<byte> memory, int rva, byte[] expected, string name)
        {
            if (rva < 0 || rva > memory.Length - expected.Length)
                throw new InvalidOperationException(name + " lies outside the loaded image.");
            for (int i = 0; i < expected.Length; i++)
                if (memory[rva + i] != expected[i])
                    throw new InvalidOperationException(name + " differs from audited Vanilla bytes.");
        }

        private static byte[] Hex(string value)
        {
            byte[] result = new byte[value.Length / 2];
            for (int i = 0; i < result.Length; i++)
                result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            return result;
        }

        internal static void EmitBuildingNorth(Assembler assembler, ReadOnlySpan<Instruction> displaced,
            ulong returnAddress, ulong imageBase, ulong flagAddress) =>
            EmitBuilding(assembler, displaced, returnAddress, imageBase, flagAddress,
                BuildingNorthRva, 0x489F5, Register.RBX);

        internal static void EmitBuildingSouth(Assembler assembler, ReadOnlySpan<Instruction> displaced,
            ulong returnAddress, ulong imageBase, ulong flagAddress) =>
            EmitBuilding(assembler, displaced, returnAddress, imageBase, flagAddress,
                BuildingSouthRva, 0x4F9E4, Register.RDI);

        private static void EmitBuilding(Assembler assembler, ReadOnlySpan<Instruction> displaced,
            ulong returnAddress, ulong imageBase, ulong flagAddress, int rva, int healthBlockRva,
            Register moduleBase)
        {
            if (displaced.Length != 4 || displaced[0].Length != 7 || displaced[1].Length != 2 ||
                displaced[2].Length != 6 || displaced[3].Length != 2 ||
                displaced[0].Mnemonic != Mnemonic.Cmp || displaced[1].Mnemonic != Mnemonic.Jne ||
                displaced[2].Mnemonic != Mnemonic.Cmp || displaced[3].Mnemonic != Mnemonic.Je ||
                displaced[1].NearBranchTarget != returnAddress ||
                displaced[3].NearBranchTarget != imageBase + (ulong)healthBlockRva ||
                returnAddress != imageBase + (ulong)rva + BuildingLength)
                throw new InvalidOperationException("Building health-gate instruction contract differs.");

            Label vanilla = assembler.CreateLabel("buildingVanilla");
            Label restore = assembler.CreateLabel("buildingRestore");
            // Preserve Vanilla's gameplay-mode branch before considering the mod gate.
            assembler.AddInstruction(displaced[0]);
            assembler.AddInstruction(displaced[1]);
            EmitFlagGate(assembler, flagAddress, vanilla);
            assembler.push(rax);
            if (moduleBase == Register.RBX)
            {
                assembler.movsx(eax, __word_ptr[rbx + r13 + BuildingCurrentHealth]);
                assembler.test(eax, eax);
                assembler.jle(restore);
                assembler.cmp(ax, __word_ptr[rbx + r13 + BuildingMaxHealth]);
            }
            else
            {
                assembler.movsx(eax, __word_ptr[rdi + rdx + BuildingCurrentHealth]);
                assembler.test(eax, eax);
                assembler.jle(restore);
                assembler.cmp(ax, __word_ptr[rdi + rdx + BuildingMaxHealth]);
            }
            assembler.jae(restore);
            assembler.pop(rax);
            assembler.AddUnrestrictedJmp(imageBase + (ulong)healthBlockRva);

            assembler.Label(ref restore);
            assembler.pop(rax);
            assembler.Label(ref vanilla);
            assembler.AddInstruction(displaced[2]);
            assembler.AddInstruction(displaced[3]);
            assembler.AddUnrestrictedJmp(returnAddress);
        }

        internal static void EmitUnit(Assembler assembler, ReadOnlySpan<Instruction> displaced,
            ulong returnAddress, ulong imageBase, ulong flagAddress)
        {
            if (displaced.Length != 4 || displaced[0].Length != 8 || displaced[1].Length != 2 ||
                displaced[2].Length != 8 || displaced[3].Length != 6 ||
                displaced[0].Mnemonic != Mnemonic.Cmp || displaced[1].Mnemonic != Mnemonic.Jne ||
                displaced[2].Mnemonic != Mnemonic.Cmp || displaced[3].Mnemonic != Mnemonic.Je ||
                displaced[1].NearBranchTarget != imageBase + 0x1A1971UL ||
                displaced[3].NearBranchTarget != imageBase + 0x1A1B96UL ||
                returnAddress != imageBase + UnitRva + UnitLength)
                throw new InvalidOperationException("Unit health-gate instruction contract differs.");

            Label vanilla = assembler.CreateLabel("unitVanilla");
            Label restore = assembler.CreateLabel("unitRestore");
            EmitFlagGate(assembler, flagAddress, vanilla);
            assembler.push(rax);
            assembler.mov(eax, __dword_ptr[rdi + rbp + UnitCurrentHealth]);
            assembler.test(eax, eax);
            assembler.jz(restore);
            assembler.cmp(eax, __dword_ptr[rdi + rbp + UnitMaxHealth]);
            assembler.jae(restore);
            assembler.pop(rax);
            assembler.AddUnrestrictedJmp(imageBase + 0x1A1971UL);

            assembler.Label(ref restore);
            assembler.pop(rax);
            assembler.Label(ref vanilla);
            foreach (Instruction instruction in displaced)
                assembler.AddInstruction(instruction);
            assembler.AddUnrestrictedJmp(returnAddress);
        }

        private static void EmitFlagGate(Assembler assembler, ulong flagAddress, Label vanilla)
        {
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__dword_ptr[rax], 1);
            assembler.pop(rax);
            assembler.jne(vanilla);
        }
    }
}
