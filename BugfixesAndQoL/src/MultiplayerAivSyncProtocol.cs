using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerAivManifest
    {
        public ulong LobbyId;
        public string VanillaChecksum = string.Empty;
        public readonly List<MultiplayerAivSlot> Slots = new List<MultiplayerAivSlot>();
    }

    internal sealed class MultiplayerAivSlot
    {
        public int PlayerId;
        public readonly List<MultiplayerAivCandidate> Candidates = new List<MultiplayerAivCandidate>();
    }

    internal sealed class MultiplayerAivCandidate
    {
        public ulong Checksum;
        public byte[] DataHash;
        public short[] Data;
    }

    internal static class MultiplayerAivSyncProtocol
    {
        internal const int ProtocolVersion = 1;
        internal const int MaximumCandidatesPerLord = 50;
        internal const int MaximumUncompressedBytes = 16 * 1024 * 1024;
        internal const int MaximumChunkBytes = 192 * 1024;
        private const int Magic = 0x56494153; // "SAIV" in little endian.

        public static byte[] Encode(MultiplayerAivManifest manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(ProtocolVersion);
                writer.Write(manifest.LobbyId);
                WriteString(writer, manifest.VanillaChecksum, 256);
                if (manifest.Slots.Count > 7)
                    throw new InvalidDataException("Invalid AIV slot count.");
                var orderedSlots = new List<MultiplayerAivSlot>(manifest.Slots);
                orderedSlots.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
                writer.Write(orderedSlots.Count);
                var blobs = new Dictionary<string, short[]>(StringComparer.Ordinal);
                int previousPlayerId = 1;
                foreach (MultiplayerAivSlot slot in orderedSlots)
                {
                    if (slot.PlayerId <= previousPlayerId || slot.PlayerId > 8 ||
                        slot.Candidates.Count < 1 || slot.Candidates.Count > MaximumCandidatesPerLord)
                        throw new InvalidDataException("Invalid AIV slot or candidate count.");
                    previousPlayerId = slot.PlayerId;
                    writer.Write(slot.PlayerId);
                    writer.Write(slot.Candidates.Count);
                    foreach (MultiplayerAivCandidate candidate in slot.Candidates)
                    {
                        byte[] hash = HashData(candidate.Data);
                        string key = ToHex(hash);
                        if (!blobs.ContainsKey(key))
                            blobs.Add(key, candidate.Data ?? Array.Empty<short>());
                        writer.Write(candidate.Checksum);
                        writer.Write(hash);
                    }
                }

                writer.Write(blobs.Count);
                foreach (KeyValuePair<string, short[]> blob in blobs)
                {
                    byte[] hash = FromHex(blob.Key);
                    writer.Write(hash);
                    writer.Write(blob.Value.Length);
                    foreach (short value in blob.Value)
                        writer.Write(value);
                }
                writer.Flush();
                if (stream.Length > MaximumUncompressedBytes)
                    throw new InvalidDataException("The AIV manifest exceeds the 16 MiB limit.");
                return stream.ToArray();
            }
        }

        public static MultiplayerAivManifest Decode(byte[] data)
        {
            if (data == null || data.Length == 0 || data.Length > MaximumUncompressedBytes)
                throw new InvalidDataException("Invalid AIV manifest size.");
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != ProtocolVersion)
                    throw new InvalidDataException("Unsupported AIV manifest format.");
                var manifest = new MultiplayerAivManifest
                {
                    LobbyId = reader.ReadUInt64(),
                    VanillaChecksum = ReadString(reader, 256)
                };
                int slotCount = reader.ReadInt32();
                if (slotCount < 0 || slotCount > 7)
                    throw new InvalidDataException("Invalid AIV slot count.");
                var references = new List<Tuple<MultiplayerAivCandidate, byte[]>>();
                int previousPlayerId = 1;
                for (int slotIndex = 0; slotIndex < slotCount; slotIndex++)
                {
                    var slot = new MultiplayerAivSlot { PlayerId = reader.ReadInt32() };
                    int candidateCount = reader.ReadInt32();
                    if (slot.PlayerId <= previousPlayerId || slot.PlayerId > 8 ||
                        candidateCount < 1 || candidateCount > MaximumCandidatesPerLord)
                        throw new InvalidDataException("Non-canonical AIV slot data.");
                    previousPlayerId = slot.PlayerId;
                    for (int candidateIndex = 0; candidateIndex < candidateCount; candidateIndex++)
                    {
                        var candidate = new MultiplayerAivCandidate { Checksum = reader.ReadUInt64() };
                        byte[] hash = ReadExact(reader, 32);
                        references.Add(Tuple.Create(candidate, hash));
                        slot.Candidates.Add(candidate);
                    }
                    manifest.Slots.Add(slot);
                }

                int blobCount = reader.ReadInt32();
                if (blobCount < 0 || blobCount > slotCount * MaximumCandidatesPerLord)
                    throw new InvalidDataException("Invalid AIV blob count.");
                var blobs = new Dictionary<string, short[]>(StringComparer.Ordinal);
                for (int blobIndex = 0; blobIndex < blobCount; blobIndex++)
                {
                    byte[] hash = ReadExact(reader, 32);
                    int shortCount = reader.ReadInt32();
                    if (shortCount < 0 || shortCount > MaximumUncompressedBytes / 2)
                        throw new InvalidDataException("Invalid AIV blob length.");
                    var values = new short[shortCount];
                    for (int index = 0; index < shortCount; index++)
                        values[index] = reader.ReadInt16();
                    string key = ToHex(hash);
                    if (!FixedEquals(hash, HashData(values)) || blobs.ContainsKey(key))
                        throw new InvalidDataException("Invalid or duplicate AIV blob hash.");
                    blobs.Add(key, values);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Trailing AIV manifest data.");
                foreach (Tuple<MultiplayerAivCandidate, byte[]> reference in references)
                {
                    if (!blobs.TryGetValue(ToHex(reference.Item2), out short[] values))
                        throw new InvalidDataException("AIV candidate references a missing blob.");
                    reference.Item1.DataHash = reference.Item2;
                    reference.Item1.Data = values;
                }
                return manifest;
            }
        }

        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true))
                    deflate.Write(data, 0, data.Length);
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] compressed, int expectedLength)
        {
            if (expectedLength < 1 || expectedLength > MaximumUncompressedBytes)
                throw new InvalidDataException("Invalid uncompressed AIV manifest length.");
            using (var input = new MemoryStream(compressed ?? Array.Empty<byte>(), false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedLength))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > expectedLength)
                        throw new InvalidDataException("AIV manifest expands beyond its declared length.");
                    output.Write(buffer, 0, read);
                }
                if (output.Length != expectedLength)
                    throw new InvalidDataException("AIV manifest length mismatch.");
                return output.ToArray();
            }
        }

        public static List<byte[]> Split(byte[] data)
        {
            var chunks = new List<byte[]>();
            for (int offset = 0; offset < data.Length; offset += MaximumChunkBytes)
            {
                int count = Math.Min(MaximumChunkBytes, data.Length - offset);
                var chunk = new byte[count];
                Buffer.BlockCopy(data, offset, chunk, 0, count);
                chunks.Add(chunk);
            }
            if (chunks.Count == 0)
                chunks.Add(Array.Empty<byte>());
            return chunks;
        }

        public static byte[] HashBytes(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
                return sha.ComputeHash(data ?? Array.Empty<byte>());
        }

        public static byte[] HashData(short[] data)
        {
            short[] source = data ?? Array.Empty<short>();
            var bytes = new byte[source.Length * 2];
            Buffer.BlockCopy(source, 0, bytes, 0, bytes.Length);
            return HashBytes(bytes);
        }

        public static bool FixedEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;
            int difference = 0;
            for (int index = 0; index < left.Length; index++)
                difference |= left[index] ^ right[index];
            return difference == 0;
        }

        public static string ToHex(byte[] data) =>
            BitConverter.ToString(data ?? Array.Empty<byte>()).Replace("-", string.Empty);

        public static byte[] FromHex(string value)
        {
            if (string.IsNullOrEmpty(value) || (value.Length & 1) != 0)
                throw new InvalidDataException("Invalid hexadecimal hash.");
            var result = new byte[value.Length / 2];
            for (int index = 0; index < result.Length; index++)
                result[index] = Convert.ToByte(value.Substring(index * 2, 2), 16);
            return result;
        }

        private static void WriteString(BinaryWriter writer, string value, int maximumBytes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maximumBytes)
                throw new InvalidDataException("Manifest string is too long.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader, int maximumBytes)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > maximumBytes)
                throw new InvalidDataException("Invalid manifest string length.");
            return Encoding.UTF8.GetString(ReadExact(reader, count));
        }

        private static byte[] ReadExact(BinaryReader reader, int count)
        {
            byte[] value = reader.ReadBytes(count);
            if (value.Length != count)
                throw new EndOfStreamException();
            return value;
        }
    }
}
