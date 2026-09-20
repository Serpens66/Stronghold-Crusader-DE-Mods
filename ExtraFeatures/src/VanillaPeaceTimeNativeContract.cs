// Feature: Audited native contract for Vanilla peace-time gameplay outside multiplayer.
using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Linq;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal enum VanillaPeaceTimePatchKind
    {
        Nop,
        MoveEbxEdi,
        MoveEdiEbx,
        MoveEbxEbp,
        JumpEqual,
        Jump
    }

    internal sealed class VanillaPeaceTimePatchSite
    {
        internal VanillaPeaceTimePatchSite(
            int rva,
            string expectedBytes,
            VanillaPeaceTimePatchKind kind,
            int targetRva,
            string name)
        {
            Rva = rva;
            ExpectedBytes = ParseBytes(expectedBytes);
            Kind = kind;
            TargetRva = targetRva;
            Name = name;
        }

        internal int Rva { get; }
        internal byte[] ExpectedBytes { get; }
        internal VanillaPeaceTimePatchKind Kind { get; }
        internal int TargetRva { get; }
        internal string Name { get; }

        private static byte[] ParseBytes(string value) =>
            value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => Convert.ToByte(token, 16))
                .ToArray();
    }

    internal static class VanillaPeaceTimeNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const ulong PreferredImageBase = 0x180000000;
        internal const int PeaceTimeActiveFlagRva = 0x38722DC;
        internal const int StartingTroopsDispatcherRva = 0x119050;
        internal const int StartingTroopsHookLength = 16;

        private static readonly byte[] StartingTroopsHookBytes =
        {
            0x40, 0x53, 0x56, 0x57, 0x41, 0x54, 0x48, 0x83,
            0xEC, 0x68, 0x8B, 0x05, 0xC8, 0xCE, 0x54, 0x03
        };

        internal static readonly VanillaPeaceTimePatchSite[] PatchSites =
        {
            new VanillaPeaceTimePatchSite(0x8A148, "74 4E", VanillaPeaceTimePatchKind.Nop, 0, "player action mode-0 bypass"),
            new VanillaPeaceTimePatchSite(0x8D366, "74 0F", VanillaPeaceTimePatchKind.Nop, 0, "command gate mode-0 bypass A"),
            new VanillaPeaceTimePatchSite(0x8D7C4, "74 09", VanillaPeaceTimePatchKind.Nop, 0, "command gate mode-0 bypass B"),
            new VanillaPeaceTimePatchSite(0x8E05E, "74 05", VanillaPeaceTimePatchKind.Nop, 0, "target gate mode-99 bypass A"),
            new VanillaPeaceTimePatchSite(0x8E062, "0F 45 DF", VanillaPeaceTimePatchKind.MoveEbxEdi, 0, "target gate mode-0 selection A"),
            new VanillaPeaceTimePatchSite(0x8E0DE, "74 09", VanillaPeaceTimePatchKind.Nop, 0, "target gate mode-0 bypass C"),
            new VanillaPeaceTimePatchSite(0x8E5FF, "74 09", VanillaPeaceTimePatchKind.Nop, 0, "target gate mode-0 bypass D"),
            new VanillaPeaceTimePatchSite(0x8EA59, "74 05", VanillaPeaceTimePatchKind.Nop, 0, "target gate mode-99 bypass B"),
            new VanillaPeaceTimePatchSite(0x8EA5D, "0F 45 FB", VanillaPeaceTimePatchKind.MoveEdiEbx, 0, "target gate mode-0 selection B"),
            new VanillaPeaceTimePatchSite(0x8EC9E, "74 05", VanillaPeaceTimePatchKind.Nop, 0, "target gate mode-99 bypass C"),
            new VanillaPeaceTimePatchSite(0x8ECA2, "0F 45 DD", VanillaPeaceTimePatchKind.MoveEbxEbp, 0, "target gate mode-0 selection C"),
            new VanillaPeaceTimePatchSite(0xABC88, "74 20", VanillaPeaceTimePatchKind.JumpEqual, 0xABC9D, "group update mode-0 peace redirect"),
            new VanillaPeaceTimePatchSite(0xABC8E, "74 1A", VanillaPeaceTimePatchKind.JumpEqual, 0xABC9D, "group update mode-99 peace redirect"),
            new VanillaPeaceTimePatchSite(0xC70BA, "74 0D", VanillaPeaceTimePatchKind.Nop, 0, "combat damage mode-0 bypass"),
            new VanillaPeaceTimePatchSite(0xCDD85, "75 45", VanillaPeaceTimePatchKind.Jump, 0xCDDCC, "combat state active-peace exit"),
            new VanillaPeaceTimePatchSite(0x105520, "74 1F", VanillaPeaceTimePatchKind.JumpEqual, 0x105534, "recruitment mode-0 peace redirect"),
            new VanillaPeaceTimePatchSite(0x105525, "74 1A", VanillaPeaceTimePatchKind.JumpEqual, 0x105534, "recruitment mode-99 peace redirect"),
            new VanillaPeaceTimePatchSite(0x124BB4, "0F 85 9C 02 00 00", VanillaPeaceTimePatchKind.Jump, 0x124E56, "tribe state active-peace exit A"),
            new VanillaPeaceTimePatchSite(0x1256C3, "0F 85 DE 02 00 00", VanillaPeaceTimePatchKind.Jump, 0x1259A7, "tribe state active-peace exit B"),
            new VanillaPeaceTimePatchSite(0x1259F3, "0F 85 F2 02 00 00", VanillaPeaceTimePatchKind.Jump, 0x125CEB, "tribe state active-peace exit C"),
            new VanillaPeaceTimePatchSite(0x156EAB, "0F 85 99 00 00 00", VanillaPeaceTimePatchKind.Jump, 0x156F4A, "unit targeting active-peace exit A"),
            new VanillaPeaceTimePatchSite(0x17F3E4, "75 27", VanillaPeaceTimePatchKind.Jump, 0x17F40D, "unit targeting active-peace exit B"),
            new VanillaPeaceTimePatchSite(0x1868B6, "75 D6", VanillaPeaceTimePatchKind.Jump, 0x18688E, "hostility active-peace result A"),
            new VanillaPeaceTimePatchSite(0x186976, "74 35", VanillaPeaceTimePatchKind.Nop, 0, "hostility mode-99 bypass B"),
            new VanillaPeaceTimePatchSite(0x18697A, "74 31", VanillaPeaceTimePatchKind.Nop, 0, "hostility mode-0 bypass B"),
            new VanillaPeaceTimePatchSite(0x1869A3, "74 08", VanillaPeaceTimePatchKind.Nop, 0, "hostility mode-99 bypass C"),
            new VanillaPeaceTimePatchSite(0x1869A7, "0F 85 E1 FE FF FF", VanillaPeaceTimePatchKind.Jump, 0x18688E, "hostility active-peace result C")
        };

        internal static readonly int[] PeaceFlagReferenceRvas =
        {
            0x15D63, 0x15FD4, 0x22561, 0x2AEA4, 0x2AF1C, 0x88234,
            0x8A14A, 0x8D368, 0x8D7C6, 0x8E04C, 0x8E0E0, 0x8E601,
            0x8EA43, 0x8EC88, 0xABC9D, 0xC70BC, 0xCA874, 0xCA8CD,
            0xCA917, 0xCA92C, 0xCDD6F, 0xD1E95, 0xD1F42, 0xD2935,
            0x10073D, 0x100820, 0x105534, 0x119110, 0x124B98, 0x1256A7,
            0x1259D7, 0x156E8D, 0x17F3CE, 0x1868A0, 0x186964, 0x186991
        };

        internal static void Validate(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The complete Vanilla peace-time gameplay patch is available only for the audited CrusaderDE.dll.");
            }

            ValidateBytes(memory, StartingTroopsDispatcherRva, StartingTroopsHookBytes,
                "starting-troop dispatcher entry");
            ValidateStartingTroopsHook(memory, imageBase);

            foreach (VanillaPeaceTimePatchSite site in PatchSites)
            {
                ValidateBytes(memory, site.Rva, site.ExpectedBytes, site.Name);
                ValidateSingleInstruction(memory, imageBase, site);
            }

            ValidatePeaceFlagReferences(memory, imageBase);
            ValidateNoIncomingDirectBranchTargets(
                memory,
                StartingTroopsDispatcherRva,
                checked(StartingTroopsDispatcherRva + StartingTroopsHookLength));
        }

        internal static void EmitPatch(
            Assembler assembler,
            VanillaPeaceTimePatchSite site,
            ulong imageBase)
        {
            switch (site.Kind)
            {
                case VanillaPeaceTimePatchKind.Nop:
                    assembler.nop(site.ExpectedBytes.Length);
                    break;
                case VanillaPeaceTimePatchKind.MoveEbxEdi:
                    assembler.mov(ebx, edi);
                    assembler.nop();
                    break;
                case VanillaPeaceTimePatchKind.MoveEdiEbx:
                    assembler.mov(edi, ebx);
                    assembler.nop();
                    break;
                case VanillaPeaceTimePatchKind.MoveEbxEbp:
                    assembler.mov(ebx, ebp);
                    assembler.nop();
                    break;
                case VanillaPeaceTimePatchKind.JumpEqual:
                    assembler.je(imageBase + unchecked((ulong)site.TargetRva));
                    break;
                case VanillaPeaceTimePatchKind.Jump:
                    assembler.jmp(imageBase + unchecked((ulong)site.TargetRva));
                    if (site.ExpectedBytes.Length == 6)
                        assembler.nop();
                    break;
                default:
                    throw new InvalidOperationException("Unknown Vanilla peace-time patch kind.");
            }
        }

        internal static void EmitStartingTroopsGuardPrefix(
            Assembler assembler,
            ulong peaceFlagAddress)
        {
            Label vanilla = assembler.CreateLabel("vanillaPeaceTimeStartingTroops");
            assembler.mov(rax, peaceFlagAddress);
            assembler.cmp(__byte_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.ret();
            assembler.Label(ref vanilla);
        }

        private static void ValidateStartingTroopsHook(
            ReadOnlySpan<byte> memory,
            ulong imageBase)
        {
            Decoder decoder = CreateDecoder(
                memory,
                imageBase,
                StartingTroopsDispatcherRva,
                StartingTroopsHookLength);
            int[] expectedLengths = { 2, 1, 1, 2, 4, 6 };
            Mnemonic[] expectedMnemonics =
            {
                Mnemonic.Push, Mnemonic.Push, Mnemonic.Push,
                Mnemonic.Push, Mnemonic.Sub, Mnemonic.Mov
            };
            int total = 0;
            for (int index = 0; index < expectedLengths.Length; index++)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid || instruction.Length != expectedLengths[index] ||
                    instruction.Mnemonic != expectedMnemonics[index] ||
                    instruction.FlowControl != FlowControl.Next)
                {
                    throw new InvalidOperationException(
                        "The starting-troop dispatcher entry no longer matches its audited ABI.");
                }
                total += instruction.Length;
            }

            if (total != StartingTroopsHookLength)
                throw new InvalidOperationException("The starting-troop hook boundary changed.");
        }

        private static void ValidateSingleInstruction(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            VanillaPeaceTimePatchSite site)
        {
            Decoder decoder = CreateDecoder(memory, imageBase, site.Rva, site.ExpectedBytes.Length);
            decoder.Decode(out Instruction instruction);
            if (instruction.IsInvalid || instruction.Length != site.ExpectedBytes.Length ||
                decoder.IP != imageBase + unchecked((ulong)(site.Rva + site.ExpectedBytes.Length)))
            {
                throw new InvalidOperationException(
                    $"The Vanilla peace-time patch '{site.Name}' is not one complete instruction.");
            }
        }

        private static void ValidatePeaceFlagReferences(
            ReadOnlySpan<byte> memory,
            ulong imageBase)
        {
            ulong flagAddress = imageBase + PeaceTimeActiveFlagRva;
            var actual = new List<int>();
            foreach (Shared.NativeCodeRange range in Shared.NativePatternResolver.GetExecutableCodeRanges(memory))
            {
                Decoder decoder = CreateDecoder(memory, imageBase, range.Offset, range.Length);
                ulong end = imageBase + unchecked((ulong)(range.Offset + range.Length));
                while (decoder.IP < end)
                {
                    decoder.Decode(out Instruction instruction);
                    if (instruction.IsInvalid)
                        throw new InvalidOperationException("The executable image contains invalid code.");
                    if (instruction.IsIPRelativeMemoryOperand &&
                        instruction.IPRelativeMemoryAddress == flagAddress)
                    {
                        actual.Add(checked((int)(instruction.IP - imageBase)));
                    }
                }
                if (decoder.IP != end)
                    throw new InvalidOperationException("An executable native range ended inside an instruction.");
            }

            if (!actual.SequenceEqual(PeaceFlagReferenceRvas))
            {
                throw new InvalidOperationException(
                    $"The Vanilla peace-time flag reference set changed: expected={PeaceFlagReferenceRvas.Length}, actual={actual.Count}.");
            }
        }

        private static void ValidateBytes(
            ReadOnlySpan<byte> memory,
            int rva,
            IReadOnlyList<byte> expected,
            string name)
        {
            if (rva < 0 || expected.Count <= 0 || rva > memory.Length - expected.Count)
                throw new InvalidOperationException($"The native range for '{name}' is outside the image.");
            for (int index = 0; index < expected.Count; index++)
            {
                if (memory[rva + index] != expected[index])
                {
                    throw new InvalidOperationException(
                        $"The audited bytes for '{name}' changed at RVA 0x{rva + index:X}.");
                }
            }
        }

        private static Decoder CreateDecoder(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            int rva,
            int length)
        {
            if (rva < 0 || length <= 0 || rva > memory.Length - length)
                throw new InvalidOperationException("A native validation range is outside the loaded image.");
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(memory.Slice(rva, length).ToArray()));
            decoder.IP = imageBase + unchecked((ulong)rva);
            return decoder;
        }

        private static void ValidateNoIncomingDirectBranchTargets(
            ReadOnlySpan<byte> memory,
            int hookStart,
            int hookEnd)
        {
            foreach (Shared.NativeCodeRange range in Shared.NativePatternResolver.GetExecutableCodeRanges(memory))
            {
                int end = checked(range.Offset + range.Length);
                for (int source = range.Offset; source < end; source++)
                {
                    int instructionLength;
                    int displacement;
                    byte opcode = memory[source];
                    if ((opcode == 0xE8 || opcode == 0xE9) && source <= end - 5)
                    {
                        instructionLength = 5;
                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 1);
                    }
                    else if ((opcode == 0xEB || (opcode >= 0x70 && opcode <= 0x7F) ||
                        (opcode >= 0xE0 && opcode <= 0xE3)) && source <= end - 2)
                    {
                        instructionLength = 2;
                        displacement = unchecked((sbyte)memory[source + 1]);
                    }
                    else if (opcode == 0x0F && source <= end - 6 &&
                        memory[source + 1] >= 0x80 && memory[source + 1] <= 0x8F)
                    {
                        instructionLength = 6;
                        displacement = Shared.NativePatternResolver.ReadInt32(memory, source + 2);
                    }
                    else
                    {
                        continue;
                    }

                    long target = (long)source + instructionLength + displacement;
                    bool sourceInsideSpan = source >= hookStart && source < hookEnd;
                    if (!sourceInsideSpan && target > hookStart && target < hookEnd)
                    {
                        throw new InvalidOperationException(
                            $"A direct control transfer at RVA 0x{source:X} targets the interior " +
                            $"of the starting-troop hook at RVA 0x{target:X}.");
                    }
                }
            }
        }
    }
}
