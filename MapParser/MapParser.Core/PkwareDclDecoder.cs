using System;
using System.Collections.Generic;

namespace MapParser.Core
{
    internal static class PkwareDclDecoder
    {
        private const int MaxBits = 13;

        private static readonly byte[] LiteralLengths =
        {
            11,124,8,7,28,7,188,13,76,4,10,8,12,10,12,10,8,23,8,9,7,6,7,8,7,6,55,8,
            23,24,12,11,7,9,11,12,6,7,22,5,7,24,6,11,9,6,7,22,7,11,38,7,9,8,25,11,8,
            11,9,12,8,12,5,38,5,38,5,11,7,5,6,21,6,10,53,8,7,24,10,27,44,253,253,253,
            252,252,252,13,12,45,12,45,12,61,12,45,44,173
        };

        private static readonly byte[] LengthLengths = { 2,35,36,53,38,23 };
        private static readonly byte[] DistanceLengths = { 2,20,53,230,247,151,248 };
        private static readonly int[] LengthBase =
            { 3,2,4,5,6,7,8,9,10,12,16,24,40,72,136,264 };
        private static readonly int[] LengthExtra =
            { 0,0,0,0,0,0,0,0,1,2,3,4,5,6,7,8 };

        private static readonly Huffman LiteralCode = new Huffman(LiteralLengths);
        private static readonly Huffman LengthCode = new Huffman(LengthLengths);
        private static readonly Huffman DistanceCode = new Huffman(DistanceLengths);

        public static byte[] DecodeSection(
            byte[] source,
            int offset,
            int storedSize,
            int sectionId,
            int expectedSize)
        {
            try
            {
                if (storedSize < 12)
                    throw new MapCorruptDataException(
                        $"Compressed section {sectionId} is shorter than its 12-byte header.");

                int declaredSize = checked((int)LittleEndian.ReadUInt32(source, offset));
                int compressedSize = checked((int)LittleEndian.ReadUInt32(source, offset + 4));
                uint expectedCrc = LittleEndian.ReadUInt32(source, offset + 8);
                if (declaredSize != expectedSize)
                {
                    throw new MapCorruptDataException(
                        $"Section {sectionId} uncompressed size mismatch: " +
                        $"directory={expectedSize}, header={declaredSize}.");
                }
                if (compressedSize < 0 || compressedSize != storedSize - 12)
                {
                    throw new MapCorruptDataException(
                        $"Section {sectionId} compressed size does not match its stored size.");
                }

                byte[] result = Decode(source, offset + 12, compressedSize, expectedSize);
                uint actualCrc = Crc32.Compute(result);
                if (actualCrc != expectedCrc)
                    throw new MapSectionCrcException(sectionId, expectedCrc, actualCrc);
                return result;
            }
            catch (MapParseException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MapCorruptDataException(
                    $"Could not decompress section {sectionId}: {ex.Message}", ex);
            }
        }

        internal static byte[] Decode(
            byte[] source,
            int offset,
            int compressedSize,
            int expectedSize)
        {
            var bits = new BitReader(source, offset, compressedSize);
            int literalMode = bits.ReadBits(8);
            if (literalMode < 0 || literalMode > 1)
                throw new MapCorruptDataException($"Invalid PKWARE literal mode {literalMode}.");

            int dictionaryBits = bits.ReadBits(8);
            if (dictionaryBits < 4 || dictionaryBits > 6)
                throw new MapCorruptDataException(
                    $"Invalid PKWARE dictionary size code {dictionaryBits}.");

            var output = new List<byte>(expectedSize);
            while (true)
            {
                if (bits.ReadBits(1) == 0)
                {
                    int literal = literalMode == 0
                        ? bits.ReadBits(8)
                        : bits.Decode(LiteralCode);
                    if (output.Count >= expectedSize)
                        throw new MapCorruptDataException("PKWARE output exceeds declared size.");
                    output.Add((byte)literal);
                    continue;
                }

                int symbol = bits.Decode(LengthCode);
                int length = LengthBase[symbol] + bits.ReadBits(LengthExtra[symbol]);
                if (length == 519)
                    break;

                int distanceBits = length == 2 ? 2 : dictionaryBits;
                int distance =
                    (bits.Decode(DistanceCode) << distanceBits) +
                    bits.ReadBits(distanceBits) + 1;
                if (distance <= 0 || distance > output.Count)
                    throw new MapCorruptDataException("PKWARE stream contains an invalid distance.");
                if (output.Count + length > expectedSize)
                    throw new MapCorruptDataException("PKWARE output exceeds declared size.");

                int copyStart = output.Count - distance;
                for (int index = 0; index < length; index++)
                    output.Add(output[copyStart + index]);
            }

            if (output.Count != expectedSize)
            {
                throw new MapCorruptDataException(
                    $"PKWARE output length mismatch: expected={expectedSize}, actual={output.Count}.");
            }
            return output.ToArray();
        }

        private sealed class Huffman
        {
            public Huffman(byte[] repeatEncodedLengths)
            {
                var lengths = new List<int>();
                foreach (byte value in repeatEncodedLengths)
                {
                    int repeat = (value >> 4) + 1;
                    int length = value & 15;
                    for (int index = 0; index < repeat; index++)
                        lengths.Add(length);
                }

                Count = new int[MaxBits + 1];
                foreach (int length in lengths)
                    Count[length]++;

                var offsets = new int[MaxBits + 2];
                for (int length = 1; length <= MaxBits; length++)
                    offsets[length + 1] = offsets[length] + Count[length];

                Symbols = new int[lengths.Count];
                for (int symbol = 0; symbol < lengths.Count; symbol++)
                {
                    int length = lengths[symbol];
                    if (length != 0)
                        Symbols[offsets[length]++] = symbol;
                }
            }

            public int[] Count { get; }
            public int[] Symbols { get; }
        }

        private sealed class BitReader
        {
            private readonly byte[] source;
            private readonly int end;
            private int position;
            private uint bitBuffer;
            private int bitCount;

            public BitReader(byte[] source, int offset, int length)
            {
                this.source = source;
                position = offset;
                end = checked(offset + length);
            }

            public int ReadBits(int needed)
            {
                if (needed == 0)
                    return 0;
                while (bitCount < needed)
                {
                    if (position >= end)
                        throw new MapCorruptDataException("PKWARE bitstream ended unexpectedly.");
                    bitBuffer |= (uint)source[position++] << bitCount;
                    bitCount += 8;
                }

                uint mask = (1u << needed) - 1u;
                int result = (int)(bitBuffer & mask);
                bitBuffer >>= needed;
                bitCount -= needed;
                return result;
            }

            public int Decode(Huffman huffman)
            {
                int code = 0;
                int first = 0;
                int index = 0;
                for (int length = 1; length <= MaxBits; length++)
                {
                    code |= ReadBits(1) ^ 1;
                    int count = huffman.Count[length];
                    if (code < first + count)
                        return huffman.Symbols[index + code - first];
                    index += count;
                    first = (first + count) << 1;
                    code <<= 1;
                }
                throw new MapCorruptDataException("PKWARE Huffman code is invalid.");
            }
        }
    }

    internal static class LittleEndian
    {
        public static ushort ReadUInt16(byte[] data, int offset)
        {
            Require(data, offset, 2);
            return (ushort)(data[offset] | (data[offset + 1] << 8));
        }

        public static short ReadInt16(byte[] data, int offset) =>
            unchecked((short)ReadUInt16(data, offset));

        public static uint ReadUInt32(byte[] data, int offset)
        {
            Require(data, offset, 4);
            return (uint)(
                data[offset] |
                (data[offset + 1] << 8) |
                (data[offset + 2] << 16) |
                (data[offset + 3] << 24));
        }

        public static int ReadInt32(byte[] data, int offset) =>
            unchecked((int)ReadUInt32(data, offset));

        public static void Require(byte[] data, int offset, int length)
        {
            if (offset < 0 || length < 0 || offset > data.Length - length)
                throw new MapCorruptDataException(
                    $"Map data ended unexpectedly at offset {offset} while reading {length} bytes.");
        }
    }
}
