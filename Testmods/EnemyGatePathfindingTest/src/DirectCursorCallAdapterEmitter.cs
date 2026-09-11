using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    // Re-emits the three cursor argument loads and replaces only the audited direct
    // DB650 call. The wrapper binds a player mask and then calls the same Vanilla search.
    internal static class DirectCursorCallAdapterEmitter
    {
        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong wrapperAddress)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            ValidateOriginal(original);
            assembler.AddInstruction(original[0]);
            assembler.AddInstruction(original[1]);
            assembler.AddInstruction(original[2]);
            assembler.mov(r11, wrapperAddress);
            assembler.call(r11);
        }

        internal static byte[] AssembleAndValidate(
            byte[] originalBytes,
            ulong originalIp,
            ulong wrapperAddress,
            ulong stubIp)
        {
            Instruction[] original = DecodeExact(originalBytes, originalIp);
            ValidateOriginal(original);
            var assembler = new Assembler(64);
            Emit(assembler, original, wrapperAddress);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateStub(encoded, stubIp, wrapperAddress);
                return encoded;
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong ip)
        {
            if (bytes == null || bytes.Length !=
                EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength)
                throw new InvalidOperationException("Direct cursor block has an unexpected length.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            var result = new List<Instruction>();
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Direct cursor block is not instruction aligned.");
                result.Add(instruction);
            }
            if (decoder.IP != end) throw new InvalidOperationException("Direct cursor block was not consumed exactly.");
            return result.ToArray();
        }

        private static void ValidateOriginal(ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 4 ||
                original[0].IP != original[1].IP - 9 ||
                original[1].IP != original[2].IP - 8 ||
                original[2].IP != original[3].IP - 7 ||
                original[3].IP - original[0].IP != 24)
                throw new InvalidOperationException("Direct cursor block does not contain its four audited instructions.");
            if (original[0].Mnemonic != Mnemonic.Movsx ||
                original[1].Mnemonic != Mnemonic.Movsx ||
                original[2].Mnemonic != Mnemonic.Lea ||
                original[3].Mnemonic != Mnemonic.Call ||
                original[3].NearBranchTarget != original[0].IP -
                    (ulong)EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva +
                    (ulong)EnemyGatePathfindingNativeDefinition.DirectTileSearchRva)
                throw new InvalidOperationException("Direct cursor call target or argument preparation differs from baseline.");
        }

        private static void ValidateStub(byte[] bytes, ulong ip, ulong wrapperAddress)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            int count = 0;
            bool wrapperPointer = false;
            bool wrapperCall = false;
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Direct cursor adapter contains invalid code.");
                count++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R11 &&
                    instruction.Immediate64 == wrapperAddress)
                    wrapperPointer = true;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == Register.R11)
                    wrapperCall = true;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.NearBranch64)
                    throw new InvalidOperationException("Direct cursor adapter retained the original direct call.");
            }
            if (count != 5 || !wrapperPointer || !wrapperCall)
                throw new InvalidOperationException("Direct cursor adapter did not emit the audited five-instruction form.");
        }
    }
}
