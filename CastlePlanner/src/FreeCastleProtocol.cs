using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CastlePlanner
{
    internal enum FreeCastlePacketKind
    {
        PreviewReady = 1,
        PreviewBegin = 2,
        SelectionBegin = 3,
        SelectionChunk = 4,
        SelectionReady = 5,
        SelectionNeedData = 6,
        Commit = 7,
        ContinueWithoutCastles = 8,
        Reject = 9,
        ParticipantStatus = 10,
        AbortRequest = 11
    }

    internal static class FreeCastlePacketRouting
    {
        internal static bool IsOperationBootstrap(
            FreeCastlePacketKind kind,
            bool receiverIsHost,
            bool senderIsHost)
        {
            if (receiverIsHost)
                return kind == FreeCastlePacketKind.PreviewReady ||
                    kind == FreeCastlePacketKind.AbortRequest;
            if (!senderIsHost)
                return false;
            return kind == FreeCastlePacketKind.PreviewBegin ||
                kind == FreeCastlePacketKind.ContinueWithoutCastles ||
                kind == FreeCastlePacketKind.Reject;
        }

        internal static bool CanHostAcceptPreviewReady(
            bool awaitingGameplay,
            bool loading,
            bool selecting) =>
            awaitingGameplay || loading || selecting;
    }

    internal static class FreeCastleParticipantReadiness
    {
        internal static bool AreAllReady(
            IEnumerable<ulong> participantSteamIds,
            ISet<ulong> readySteamIds)
        {
            if (participantSteamIds == null)
                throw new ArgumentNullException(nameof(participantSteamIds));
            if (readySteamIds == null)
                throw new ArgumentNullException(nameof(readySteamIds));

            foreach (ulong steamId in participantSteamIds)
                if (!readySteamIds.Contains(steamId))
                    return false;
            return true;
        }
    }

    internal static class FreeCastleSelectionLookup
    {
        internal static bool TryGetRotation(
            IEnumerable<FreeCastleSelection> selections,
            int playerId,
            out int rotation)
        {
            rotation = 0;
            if (selections == null)
                return false;

            foreach (FreeCastleSelection selection in selections)
            {
                if (selection == null || selection.PlayerId != playerId)
                    continue;
                if (selection.Rotation != 0 && selection.Rotation != 2 &&
                    selection.Rotation != 4 && selection.Rotation != 6)
                {
                    return false;
                }

                rotation = selection.Rotation;
                return true;
            }

            return false;
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(FreeCastlePacketFormatter))]
    internal sealed class FreeCastlePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int Kind;
        [Key(2)] public int OperationId;
        [Key(3)] public int PlayerId;
        [Key(4)] public int Rotation;
        [Key(5)] public string DisplayName;
        [Key(6)] public string ContentHash;
        [Key(7)] public int UncompressedLength;
        [Key(8)] public int CompressedLength;
        [Key(9)] public int ChunkIndex;
        [Key(10)] public int ChunkCount;
        [Key(11)] public string DataBase64;
        [Key(12)] public int TimeoutSeconds;
        [Key(13)] public string Roster;
        [Key(14)] public string Message;
    }

    internal sealed class FreeCastlePacketFormatter : IMessagePackFormatter<FreeCastlePacket>
    {
        private const int FieldCount = 15;

        public void Serialize(
            ref MessagePackWriter writer,
            FreeCastlePacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.Kind);
            writer.Write(value.OperationId);
            writer.Write(value.PlayerId);
            writer.Write(value.Rotation);
            writer.Write(value.DisplayName);
            writer.Write(value.ContentHash);
            writer.Write(value.UncompressedLength);
            writer.Write(value.CompressedLength);
            writer.Write(value.ChunkIndex);
            writer.Write(value.ChunkCount);
            writer.Write(value.DataBase64);
            writer.Write(value.TimeoutSeconds);
            writer.Write(value.Roster);
            writer.Write(value.Message);
        }

        public FreeCastlePacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            if (count > FieldCount)
                throw new MessagePackSerializationException(
                    "Free-castle packet has too many fields.");

            var value = new FreeCastlePacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: value.Kind = reader.ReadInt32(); break;
                    case 2: value.OperationId = reader.ReadInt32(); break;
                    case 3: value.PlayerId = reader.ReadInt32(); break;
                    case 4: value.Rotation = reader.ReadInt32(); break;
                    case 5: value.DisplayName = reader.ReadString(); break;
                    case 6: value.ContentHash = reader.ReadString(); break;
                    case 7: value.UncompressedLength = reader.ReadInt32(); break;
                    case 8: value.CompressedLength = reader.ReadInt32(); break;
                    case 9: value.ChunkIndex = reader.ReadInt32(); break;
                    case 10: value.ChunkCount = reader.ReadInt32(); break;
                    case 11: value.DataBase64 = reader.ReadString(); break;
                    case 12: value.TimeoutSeconds = reader.ReadInt32(); break;
                    case 13: value.Roster = reader.ReadString(); break;
                    case 14: value.Message = reader.ReadString(); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }

    internal sealed class FreeCastleSelection
    {
        public int PlayerId;
        public int Rotation;
        public string DisplayName = string.Empty;
        public string ContentHash = string.Empty;
        public short[] RawData = Array.Empty<short>();

        public bool HasCastle => RawData != null && RawData.Length > 0;

        public FreeCastleSelection Clone() => new FreeCastleSelection
        {
            PlayerId = PlayerId,
            Rotation = Rotation,
            DisplayName = DisplayName ?? string.Empty,
            ContentHash = ContentHash ?? string.Empty,
            RawData = RawData == null ? Array.Empty<short>() : (short[])RawData.Clone()
        };
    }

    internal static class FreeCastleProtocol
    {
        internal const int ProtocolVersion = 2;
        internal const int PreviewTimeoutSeconds = 120;
        internal const int MaximumChunkBytes = 24 * 1024;
        internal const int MaximumUncompressedBytes = 8 * 1024 * 1024;
        internal const int MaximumCompressedBytes =
            MaximumUncompressedBytes + MaximumChunkBytes;
        private const int Magic = 0x50434653; // "SFCP" in little endian.

        public static byte[] EncodeSelections(
            IEnumerable<FreeCastleSelection> selections)
        {
            var ordered = new List<FreeCastleSelection>(
                selections ?? Array.Empty<FreeCastleSelection>());
            ordered.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));

            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(Magic);
                writer.Write(ProtocolVersion);
                writer.Write(ordered.Count);
                int previousPlayerId = 0;
                foreach (FreeCastleSelection selection in ordered)
                {
                    ValidateSelection(selection);
                    if (selection.PlayerId <= previousPlayerId)
                        throw new InvalidDataException(
                            "Free-castle selections are not in unique player order.");
                    previousPlayerId = selection.PlayerId;
                    writer.Write(selection.PlayerId);
                    writer.Write(selection.Rotation);
                    WriteString(writer, selection.DisplayName, 512);
                    writer.Write(selection.RawData.Length);
                    foreach (short value in selection.RawData)
                        writer.Write(value);
                }
                writer.Flush();
                if (stream.Length > MaximumUncompressedBytes)
                    throw new InvalidDataException(
                        "Free-castle selection data exceeds the 8 MiB limit.");
                return stream.ToArray();
            }
        }

        public static List<FreeCastleSelection> DecodeSelections(byte[] encoded)
        {
            if (encoded == null || encoded.Length < 12 ||
                encoded.Length > MaximumUncompressedBytes)
            {
                throw new InvalidDataException("Invalid free-castle data length.");
            }

            using (var stream = new MemoryStream(encoded, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != Magic ||
                    reader.ReadInt32() != ProtocolVersion)
                {
                    throw new InvalidDataException(
                        "Unsupported free-castle data format.");
                }

                int count = reader.ReadInt32();
                if (count < 0 || count > 8)
                    throw new InvalidDataException("Invalid selection count.");
                var result = new List<FreeCastleSelection>(count);
                int previousPlayerId = 0;
                for (int index = 0; index < count; index++)
                {
                    int playerId = reader.ReadInt32();
                    int rotation = reader.ReadInt32();
                    string displayName = ReadString(reader, 512);
                    int shortCount = reader.ReadInt32();
                    if (shortCount < 0 || shortCount > MaximumUncompressedBytes / 2)
                        throw new InvalidDataException("Invalid AIV data length.");
                    var raw = new short[shortCount];
                    for (int rawIndex = 0; rawIndex < shortCount; rawIndex++)
                        raw[rawIndex] = reader.ReadInt16();

                    var selection = new FreeCastleSelection
                    {
                        PlayerId = playerId,
                        Rotation = rotation,
                        DisplayName = displayName,
                        RawData = raw,
                        ContentHash = HashRaw(raw)
                    };
                    ValidateSelection(selection);
                    if (playerId <= previousPlayerId)
                        throw new InvalidDataException(
                            "Free-castle selections are not canonical.");
                    previousPlayerId = playerId;
                    result.Add(selection);
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException(
                        "Trailing free-castle selection data.");
                return result;
            }
        }

        public static byte[] Compress(byte[] data)
        {
            using (var output = new MemoryStream())
            {
                using (var deflate = new DeflateStream(
                    output,
                    CompressionLevel.Optimal,
                    true))
                {
                    deflate.Write(data, 0, data.Length);
                }
                if (output.Length > MaximumCompressedBytes)
                    throw new InvalidDataException(
                        "Compressed free-castle data exceeds its limit.");
                return output.ToArray();
            }
        }

        public static byte[] Decompress(byte[] compressed, int expectedLength)
        {
            if (expectedLength < 12 || expectedLength > MaximumUncompressedBytes ||
                compressed == null || compressed.Length < 1 ||
                compressed.Length > MaximumCompressedBytes)
            {
                throw new InvalidDataException(
                    "Invalid compressed free-castle data length.");
            }

            using (var input = new MemoryStream(compressed, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream(expectedLength))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + read > expectedLength)
                        throw new InvalidDataException(
                            "Free-castle data expands beyond its declared length.");
                    output.Write(buffer, 0, read);
                }
                if (output.Length != expectedLength)
                    throw new InvalidDataException(
                        "Free-castle decompressed length mismatch.");
                return output.ToArray();
            }
        }

        public static List<byte[]> Split(byte[] data)
        {
            var chunks = new List<byte[]>();
            for (int offset = 0; offset < data.Length; offset += MaximumChunkBytes)
            {
                int length = Math.Min(MaximumChunkBytes, data.Length - offset);
                var chunk = new byte[length];
                Buffer.BlockCopy(data, offset, chunk, 0, length);
                chunks.Add(chunk);
            }
            if (chunks.Count == 0)
                chunks.Add(Array.Empty<byte>());
            return chunks;
        }

        public static string HashRaw(short[] raw)
        {
            short[] source = raw ?? Array.Empty<short>();
            var bytes = new byte[source.Length * 2];
            Buffer.BlockCopy(source, 0, bytes, 0, bytes.Length);
            return HashBytes(bytes);
        }

        public static string HashBytes(byte[] data)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(
                    sha.ComputeHash(data ?? Array.Empty<byte>()))
                    .Replace("-", string.Empty);
        }

        public static void ValidateSelection(FreeCastleSelection selection)
        {
            if (selection == null || selection.PlayerId < 1 || selection.PlayerId > 8)
                throw new InvalidDataException("Invalid selected player ID.");
            if (selection.Rotation != 0 && selection.Rotation != 2 &&
                selection.Rotation != 4 && selection.Rotation != 6)
            {
                throw new InvalidDataException("Invalid castle rotation.");
            }
            if (selection.DisplayName == null ||
                Encoding.UTF8.GetByteCount(selection.DisplayName) > 512)
            {
                throw new InvalidDataException("Invalid castle display name.");
            }
            if (selection.RawData == null || selection.RawData.Length == 0)
                throw new InvalidDataException("Selected castle has no AIV data.");
            string hash = HashRaw(selection.RawData);
            if (!string.IsNullOrEmpty(selection.ContentHash) &&
                !string.Equals(selection.ContentHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Selected castle hash mismatch.");
            }
        }

        private static void WriteString(
            BinaryWriter writer,
            string value,
            int maximumBytes)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > maximumBytes)
                throw new InvalidDataException("Free-castle text is too long.");
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader, int maximumBytes)
        {
            int length = reader.ReadInt32();
            if (length < 0 || length > maximumBytes)
                throw new InvalidDataException("Invalid free-castle text length.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
