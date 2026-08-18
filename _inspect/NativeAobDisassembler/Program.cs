using Iced.Intel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NativeAobDisassembler
{
    internal static class Program
    {
        private sealed class Section
        {
            public string Name;
            public uint VirtualAddress;
            public uint VirtualSize;
            public uint RawAddress;
            public uint RawSize;
        }

        private static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine(
                    "Usage: NativeAobDisassembler <PE file> <AOB pattern | va:HEX> [byte count]");
                return 2;
            }

            string path = Path.GetFullPath(args[0]);
            byte[] image = File.ReadAllBytes(path);
            ReadPeLayout(image, out ulong imageBase, out List<Section> sections);

            int byteCount = args.Length >= 3
                ? int.Parse(args[2], CultureInfo.InvariantCulture)
                : 512;

            if (args[1].StartsWith("xrefs:", StringComparison.OrdinalIgnoreCase))
            {
                ulong targetAddress = ulong.Parse(
                    args[1].Substring(6),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                PrintRelativeCallReferences(
                    image,
                    imageBase,
                    sections,
                    targetAddress);
                return 0;
            }

            if (args[1].StartsWith("memrefs:", StringComparison.OrdinalIgnoreCase))
            {
                ulong displacement = ulong.Parse(
                    args[1].Substring(8),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                PrintMemoryDisplacementReferences(
                    image,
                    imageBase,
                    sections,
                    displacement);
                return 0;
            }

            if (args[1].StartsWith("riprefs:", StringComparison.OrdinalIgnoreCase))
            {
                ulong targetAddress = ulong.Parse(
                    args[1].Substring(8),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                PrintRipRelativeReferences(
                    image,
                    imageBase,
                    sections,
                    targetAddress);
                return 0;
            }

            if (args[1].StartsWith("dispatch:", StringComparison.OrdinalIgnoreCase))
            {
                int unitType = int.Parse(
                    args[1].Substring(9),
                    CultureInfo.InvariantCulture);
                PrintUnitTypeDispatchEntry(
                    image,
                    imageBase,
                    sections,
                    unitType);
                return 0;
            }

            int fileOffset;
            ulong virtualAddress;
            if (args[1].StartsWith("func:", StringComparison.OrdinalIgnoreCase))
            {
                ulong insideFunction = ulong.Parse(
                    args[1].Substring(5),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                int insideFileOffset = VirtualAddressToFileOffset(
                    insideFunction,
                    imageBase,
                    sections);
                fileOffset = FindAlignedFunctionStart(image, insideFileOffset);
                virtualAddress = FileOffsetToVirtualAddress(
                    fileOffset,
                    imageBase,
                    sections);
            }
            else if (args[1].StartsWith("va:", StringComparison.OrdinalIgnoreCase))
            {
                virtualAddress = ulong.Parse(
                    args[1].Substring(3),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
                fileOffset = VirtualAddressToFileOffset(
                    virtualAddress,
                    imageBase,
                    sections);
            }
            else
            {
                PatternByte[] pattern = ParsePattern(args[1]);
                List<int> matches = FindMatches(image, pattern);
                if (matches.Count == 0)
                {
                    Console.Error.WriteLine("Pattern not found.");
                    return 3;
                }

                Console.WriteLine($"matches={matches.Count}");
                foreach (int match in matches)
                {
                    Console.WriteLine(
                        $"  file=0x{match:X} va=0x{FileOffsetToVirtualAddress(match, imageBase, sections):X}");
                }

                if (matches.Count != 1)
                    return 4;

                fileOffset = matches[0];
                virtualAddress = FileOffsetToVirtualAddress(
                    fileOffset,
                    imageBase,
                    sections);
            }

            byteCount = Math.Min(byteCount, image.Length - fileOffset);
            byte[] code = new byte[byteCount];
            Buffer.BlockCopy(image, fileOffset, code, 0, byteCount);

            Console.WriteLine(
                $"imageBase=0x{imageBase:X} fileOffset=0x{fileOffset:X} " +
                $"virtualAddress=0x{virtualAddress:X} bytes={byteCount}");
            Disassemble(code, virtualAddress);
            return 0;
        }

        private static void Disassemble(byte[] code, ulong virtualAddress)
        {
            Decoder decoder = Decoder.Create(
                64,
                new ByteArrayCodeReader(code),
                DecoderOptions.None);
            decoder.IP = virtualAddress;

            var formatter = new NasmFormatter();
            formatter.Options.DigitSeparator = "`";
            formatter.Options.FirstOperandCharIndex = 10;
            var output = new StringOutput();

            ulong endAddress = virtualAddress + (uint)code.Length;
            while (decoder.IP < endAddress)
            {
                ulong instructionAddress = decoder.IP;
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID)
                    break;

                formatter.Format(instruction, output);
                int instructionOffset = checked((int)(instructionAddress - virtualAddress));
                string instructionBytes = BitConverter.ToString(
                    code,
                    instructionOffset,
                    instruction.Length).Replace("-", " ");
                Console.WriteLine(
                    $"{instructionAddress:X16}  {instructionBytes,-32}  {output.ToStringAndReset()}");
            }
        }

        private static void PrintRelativeCallReferences(
            byte[] image,
            ulong imageBase,
            IEnumerable<Section> sections,
            ulong targetAddress)
        {
            int count = 0;
            foreach (Section section in sections)
            {
                int start = checked((int)section.RawAddress);
                int end = Math.Min(
                    image.Length - 5,
                    checked((int)(section.RawAddress + section.RawSize - 5)));
                for (int fileOffset = start; fileOffset <= end; fileOffset++)
                {
                    if (image[fileOffset] != 0xE8)
                        continue;

                    ulong sourceAddress = FileOffsetToVirtualAddress(
                        fileOffset,
                        imageBase,
                        sections);
                    long displacement = ReadInt32(image, fileOffset + 1);
                    ulong actualTarget = unchecked(
                        (ulong)((long)sourceAddress + 5 + displacement));
                    if (actualTarget != targetAddress)
                        continue;

                    Console.WriteLine(
                        $"callSite=0x{sourceAddress:X} file=0x{fileOffset:X} " +
                        $"target=0x{targetAddress:X}");
                    count++;
                }
            }

            Console.WriteLine($"xrefs={count}");
        }

        private static void PrintMemoryDisplacementReferences(
            byte[] image,
            ulong imageBase,
            IEnumerable<Section> sections,
            ulong displacement)
        {
            int count = 0;
            foreach (Section section in sections.Where(section => section.Name == ".text"))
            {
                int start = checked((int)section.RawAddress);
                int length = checked((int)section.RawSize);
                byte[] code = new byte[length];
                Buffer.BlockCopy(image, start, code, 0, length);
                ulong sectionAddress = imageBase + section.VirtualAddress;

                Decoder decoder = Decoder.Create(
                    64,
                    new ByteArrayCodeReader(code),
                    DecoderOptions.None);
                decoder.IP = sectionAddress;
                ulong endAddress = sectionAddress + (uint)code.Length;
                var formatter = new NasmFormatter();
                var output = new StringOutput();

                while (decoder.IP < endAddress)
                {
                    ulong instructionAddress = decoder.IP;
                    decoder.Decode(out Instruction instruction);
                    if (instruction.Code == Code.INVALID)
                        continue;
                    if (instruction.MemoryDisplSize == 0 ||
                        instruction.MemoryDisplacement64 != displacement)
                    {
                        continue;
                    }

                    formatter.Format(instruction, output);
                    Console.WriteLine(
                        $"address=0x{instructionAddress:X} " +
                        $"base={instruction.MemoryBase} index={instruction.MemoryIndex} " +
                        output.ToStringAndReset());
                    count++;
                }
            }

            Console.WriteLine($"memrefs=0x{displacement:X} count={count}");
        }

        private static void PrintRipRelativeReferences(
            byte[] image,
            ulong imageBase,
            IEnumerable<Section> sections,
            ulong targetAddress)
        {
            int count = 0;
            foreach (Section section in sections.Where(section => section.Name == ".text"))
            {
                int start = checked((int)section.RawAddress);
                int length = checked((int)section.RawSize);
                byte[] code = new byte[length];
                Buffer.BlockCopy(image, start, code, 0, length);
                ulong sectionAddress = imageBase + section.VirtualAddress;

                Decoder decoder = Decoder.Create(
                    64,
                    new ByteArrayCodeReader(code),
                    DecoderOptions.None);
                decoder.IP = sectionAddress;
                ulong endAddress = sectionAddress + (uint)code.Length;
                var formatter = new NasmFormatter();
                var output = new StringOutput();

                while (decoder.IP < endAddress)
                {
                    ulong instructionAddress = decoder.IP;
                    decoder.Decode(out Instruction instruction);
                    if (instruction.Code == Code.INVALID ||
                        !instruction.IsIPRelativeMemoryOperand ||
                        instruction.IPRelativeMemoryAddress != targetAddress)
                    {
                        continue;
                    }

                    formatter.Format(instruction, output);
                    Console.WriteLine(
                        $"address=0x{instructionAddress:X} " +
                        output.ToStringAndReset());
                    count++;
                }
            }

            Console.WriteLine($"riprefs=0x{targetAddress:X} count={count}");
        }

        private static void PrintUnitTypeDispatchEntry(
            byte[] image,
            ulong imageBase,
            IEnumerable<Section> sections,
            int unitType)
        {
            const string dispatchPattern =
                "41 FF 94 C6 ?? ?? ?? ?? 8B 15 ?? ?? ?? ?? " +
                "48 63 C2 48 69 C8 90 04 00 00";
            List<int> matches = FindMatches(image, ParsePattern(dispatchPattern));
            if (matches.Count != 1)
            {
                throw new InvalidDataException(
                    $"Expected one unit-type dispatch match, found {matches.Count}.");
            }

            int dispatchOffset = ReadInt32(image, matches[0] + 4);
            ulong tableAddress = imageBase + unchecked((uint)dispatchOffset);
            int tableFileOffset = VirtualAddressToFileOffset(
                tableAddress,
                imageBase,
                sections);
            int entryOffset = checked(tableFileOffset + unitType * sizeof(ulong));
            ulong handlerAddress = ReadUInt64(image, entryOffset);
            Console.WriteLine(
                $"unitType={unitType} table=0x{tableAddress:X} " +
                $"entryFile=0x{entryOffset:X} handler=0x{handlerAddress:X}");
        }

        private static int FindAlignedFunctionStart(byte[] image, int insideFileOffset)
        {
            for (int offset = insideFileOffset - 1; offset >= 4; offset--)
            {
                if (image[offset] != 0xCC ||
                    image[offset - 1] != 0xCC ||
                    image[offset - 2] != 0xCC ||
                    image[offset - 3] != 0xCC)
                {
                    continue;
                }

                int start = offset + 1;
                while (start < insideFileOffset && image[start] == 0xCC)
                    start++;
                return start;
            }

            throw new InvalidDataException(
                $"No aligned function boundary found before file offset 0x{insideFileOffset:X}.");
        }

        private static void ReadPeLayout(
            byte[] image,
            out ulong imageBase,
            out List<Section> sections)
        {
            int peOffset = ReadInt32(image, 0x3C);
            if (ReadUInt32(image, peOffset) != 0x00004550)
                throw new InvalidDataException("PE signature missing.");

            ushort sectionCount = ReadUInt16(image, peOffset + 6);
            ushort optionalHeaderSize = ReadUInt16(image, peOffset + 20);
            int optionalHeader = peOffset + 24;
            if (ReadUInt16(image, optionalHeader) != 0x20B)
                throw new InvalidDataException("Only PE32+ images are supported.");

            imageBase = ReadUInt64(image, optionalHeader + 24);
            int sectionTable = optionalHeader + optionalHeaderSize;
            sections = new List<Section>(sectionCount);
            for (int index = 0; index < sectionCount; index++)
            {
                int offset = sectionTable + index * 40;
                sections.Add(new Section
                {
                    Name = ReadAsciiName(image, offset, 8),
                    VirtualSize = ReadUInt32(image, offset + 8),
                    VirtualAddress = ReadUInt32(image, offset + 12),
                    RawSize = ReadUInt32(image, offset + 16),
                    RawAddress = ReadUInt32(image, offset + 20)
                });
            }
        }

        private static ulong FileOffsetToVirtualAddress(
            int fileOffset,
            ulong imageBase,
            IEnumerable<Section> sections)
        {
            foreach (Section section in sections)
            {
                ulong rawStart = section.RawAddress;
                ulong rawEnd = rawStart + section.RawSize;
                if ((ulong)fileOffset >= rawStart && (ulong)fileOffset < rawEnd)
                {
                    return imageBase + section.VirtualAddress +
                           ((ulong)fileOffset - rawStart);
                }
            }

            throw new InvalidDataException(
                $"File offset 0x{fileOffset:X} is outside mapped PE sections.");
        }

        private static int VirtualAddressToFileOffset(
            ulong virtualAddress,
            ulong imageBase,
            IEnumerable<Section> sections)
        {
            ulong rva = virtualAddress - imageBase;
            foreach (Section section in sections)
            {
                ulong virtualStart = section.VirtualAddress;
                ulong virtualLength = Math.Max(section.VirtualSize, section.RawSize);
                if (rva >= virtualStart && rva < virtualStart + virtualLength)
                {
                    return checked((int)(section.RawAddress + (rva - virtualStart)));
                }
            }

            throw new InvalidDataException(
                $"Virtual address 0x{virtualAddress:X} is outside mapped PE sections.");
        }

        private readonly struct PatternByte
        {
            public PatternByte(byte value, bool wildcard)
            {
                Value = value;
                Wildcard = wildcard;
            }

            public byte Value { get; }
            public bool Wildcard { get; }
        }

        private static PatternByte[] ParsePattern(string pattern)
        {
            return pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token == "?" || token == "??"
                    ? new PatternByte(0, true)
                    : new PatternByte(
                        byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        false))
                .ToArray();
        }

        private static List<int> FindMatches(byte[] image, PatternByte[] pattern)
        {
            var matches = new List<int>();
            for (int offset = 0; offset <= image.Length - pattern.Length; offset++)
            {
                bool matchesAtOffset = true;
                for (int index = 0; index < pattern.Length; index++)
                {
                    if (!pattern[index].Wildcard &&
                        image[offset + index] != pattern[index].Value)
                    {
                        matchesAtOffset = false;
                        break;
                    }
                }

                if (matchesAtOffset)
                    matches.Add(offset);
            }

            return matches;
        }

        private static string ReadAsciiName(byte[] data, int offset, int length)
        {
            int end = offset;
            while (end < offset + length && data[end] != 0)
                end++;
            return System.Text.Encoding.ASCII.GetString(data, offset, end - offset);
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return BitConverter.ToUInt16(data, offset);
        }

        private static uint ReadUInt32(byte[] data, int offset)
        {
            return BitConverter.ToUInt32(data, offset);
        }

        private static int ReadInt32(byte[] data, int offset)
        {
            return BitConverter.ToInt32(data, offset);
        }

        private static ulong ReadUInt64(byte[] data, int offset)
        {
            return BitConverter.ToUInt64(data, offset);
        }
    }
}
