// Feature: Encode and decode remembered AI AIV/AIC selections.
using CrusaderDE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BugfixesAndQoL
{
    internal static class AiAivSelectionCodec
    {
        private const string PayloadPrefix = "v3:";
        private const string LegacyPayloadPrefix = "v2:";
        private const int PayloadVersion = 3;
        private const int LegacyPayloadVersion = 2;
        private const int MaxStringBytes = 1024 * 1024;
        private const int MaxAivPayloadBytes = 8 * 1024 * 1024;
        private const int MaxLordConfigBytes = 8 * 1024 * 1024;
        private const int MaxImageBytes = 32 * 1024 * 1024;

        public static string Encode(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));
            return Encode(Capture(info));
        }

        public static string Encode(AiAivSelectionSnapshot snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Aivs.Count > AivAicPresetStore.MaximumAivEntries)
            {
                throw new InvalidDataException(
                    $"AIV selection contains {snapshot.Aivs.Count} entries; maximum is " +
                    $"{AivAicPresetStore.MaximumAivEntries}.");
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(PayloadVersion);
                writer.Write(snapshot.LordType);
                WriteString(writer, snapshot.LordName);
                writer.Write(snapshot.BuiltIn);
                writer.Write(snapshot.Community);
                writer.Write(snapshot.Historical);
                writer.Write(snapshot.Rotation);
                writer.Write(snapshot.BuiltInLord);
                writer.Write(snapshot.Aic != null);
                if (snapshot.Aic != null)
                    WriteReference(writer, snapshot.Aic);
                writer.Write(snapshot.Aivs.Count);
                foreach (AivAicAssetReference reference in snapshot.Aivs)
                    WriteReference(writer, reference);

                writer.Flush();
                return PayloadPrefix +
                    Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
            }
        }

        public static bool TryDecode(
            string encoded,
            out AiAivSelectionSnapshot snapshot,
            out bool legacyPayload,
            out string error)
        {
            snapshot = null;
            legacyPayload = false;
            error = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(encoded) && encoded.StartsWith(PayloadPrefix, StringComparison.Ordinal))
                {
                    snapshot = DecodeCurrent(encoded.Substring(PayloadPrefix.Length));
                    return true;
                }
                if (!string.IsNullOrEmpty(encoded) && encoded.StartsWith(LegacyPayloadPrefix, StringComparison.Ordinal))
                {
                    snapshot = DecodeLegacy(encoded.Substring(LegacyPayloadPrefix.Length));
                    legacyPayload = true;
                    return true;
                }
                error = "unsupported or missing payload version";
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static AiAivSelectionApplyResult Apply(
            AiAivSelectionSnapshot snapshot,
            FRONT_Multiplayer.MPAIVInfo target,
            IList<CustomisationFileManager.CustomAIV> availableAivs,
            IList<CustomisationFileManager.CustomLordConfig> availableAics)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.lordType = snapshot.LordType;
            target.lordName = snapshot.LordName;
            target.builtIn = snapshot.BuiltIn;
            target.community = snapshot.Community;
            target.historical = snapshot.Historical;
            target.rotation = snapshot.Rotation;

            int missingAivs = 0;
            target.aivs.Clear();
            foreach (AivAicAssetReference reference in snapshot.Aivs)
            {
                CustomisationFileManager.CustomAIV resolved =
                    AivAicPresetStore.ResolveAiv(reference, availableAivs);
                if (resolved == null)
                {
                    missingAivs++;
                    continue;
                }
                if (!target.aivs.Exists(item => item.checksum == resolved.checksum))
                    target.aivs.Add(resolved);
            }

            bool missingAic = false;
            if (snapshot.BuiltInLord || snapshot.Aic == null)
            {
                target.builtInLord = true;
                target.lordConfig = null;
            }
            else
            {
                target.lordConfig = AivAicPresetStore.ResolveAic(snapshot.Aic, availableAics);
                missingAic = target.lordConfig == null;
                target.builtInLord = missingAic;
            }

            // The current custom-lord image belongs to the package, not this remembered selection.
            return new AiAivSelectionApplyResult(target.aivs.Count, missingAivs, missingAic);
        }

        public static string BuildLordKey(AiAivSelectionSnapshot snapshot)
        {
            if (snapshot == null)
                return string.Empty;
            return !string.IsNullOrEmpty(snapshot.LordName)
                ? "custom:" + snapshot.LordName
                : "builtin:" + snapshot.LordType;
        }

        public static bool UsesFileBackedAssets(AiAivSelectionSnapshot snapshot)
        {
            if (snapshot == null)
                return false;
            if (!string.IsNullOrEmpty(snapshot.LordName) || !string.IsNullOrEmpty(snapshot.Aic?.Path))
                return true;
            foreach (AivAicAssetReference reference in snapshot.Aivs)
            {
                if (!string.IsNullOrEmpty(reference?.Path))
                    return true;
            }
            return false;
        }

        public static bool ShouldRefreshAssetLists(
            AiAivSelectionSnapshot snapshot,
            bool filesChanged) => filesChanged && UsesFileBackedAssets(snapshot);

        private static AiAivSelectionSnapshot Capture(FRONT_Multiplayer.MPAIVInfo info)
        {
            int aivCount = info.aivs?.Count ?? 0;
            if (aivCount > AivAicPresetStore.MaximumAivEntries)
            {
                throw new InvalidDataException(
                    $"AIV selection contains {aivCount} entries; maximum is " +
                    $"{AivAicPresetStore.MaximumAivEntries}.");
            }

            bool storeAsBuiltIn = !info.builtIn && !info.community && !info.historical && aivCount == 0;
            var snapshot = new AiAivSelectionSnapshot
            {
                LordType = info.lordType,
                LordName = info.lordName,
                BuiltIn = info.builtIn || storeAsBuiltIn,
                Community = storeAsBuiltIn ? false : info.community,
                Historical = storeAsBuiltIn ? false : info.historical,
                Rotation = info.rotation,
                BuiltInLord = info.builtInLord || info.lordConfig == null,
                Aic = info.builtInLord || info.lordConfig == null
                    ? null
                    : AivAicPresetStore.CaptureAic(info.lordConfig)
            };
            if (info.aivs != null)
            {
                foreach (CustomisationFileManager.CustomAIV aiv in info.aivs)
                {
                    if (aiv == null)
                        throw new InvalidDataException("AIV selection contains a null entry.");
                    snapshot.Aivs.Add(AivAicPresetStore.CaptureAiv(aiv));
                }
            }
            return snapshot;
        }

        private static AiAivSelectionSnapshot DecodeCurrent(string encodedPayload)
        {
            byte[] payload = Convert.FromBase64String(encodedPayload);
            using (MemoryStream stream = new MemoryStream(payload, writable: false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                int version = reader.ReadInt32();
                if (version != PayloadVersion)
                    throw new InvalidDataException($"Unsupported payload version {version}.");
                var snapshot = new AiAivSelectionSnapshot
                {
                    LordType = reader.ReadInt32(),
                    LordName = ReadString(reader, stream),
                    BuiltIn = reader.ReadBoolean(),
                    Community = reader.ReadBoolean(),
                    Historical = reader.ReadBoolean(),
                    Rotation = reader.ReadInt32(),
                    BuiltInLord = reader.ReadBoolean()
                };
                if (reader.ReadBoolean())
                    snapshot.Aic = ReadReference(reader, stream);
                int aivCount = reader.ReadInt32();
                if (aivCount < 0 || aivCount > AivAicPresetStore.MaximumAivEntries)
                    throw new InvalidDataException($"Invalid AIV count {aivCount}.");
                for (int i = 0; i < aivCount; i++)
                    snapshot.Aivs.Add(ReadReference(reader, stream));
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Payload has trailing data.");
                return snapshot;
            }
        }

        private static AiAivSelectionSnapshot DecodeLegacy(string encodedPayload)
        {
            byte[] payload = Convert.FromBase64String(encodedPayload);
            using (MemoryStream stream = new MemoryStream(payload, writable: false))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                int version = reader.ReadInt32();
                if (version != LegacyPayloadVersion)
                    throw new InvalidDataException($"Unsupported legacy payload version {version}.");
                var snapshot = new AiAivSelectionSnapshot
                {
                    LordType = reader.ReadInt32(),
                    LordName = ReadString(reader, stream),
                    BuiltIn = reader.ReadBoolean(),
                    Community = reader.ReadBoolean(),
                    Historical = reader.ReadBoolean(),
                    Rotation = reader.ReadInt32(),
                    BuiltInLord = reader.ReadBoolean()
                };
                byte[] lordConfigData = ReadBytes(reader, stream, MaxLordConfigBytes);
                if (lordConfigData != null)
                {
                    ValidateLordConfigPayload(lordConfigData);
                    CustomisationFileManager.CustomLordConfig aic =
                        CustomisationFileManager.CustomLordConfig.decode(lordConfigData);
                    aic.workshop = reader.ReadBoolean();
                    aic.workshopUploadInfoAvailable = reader.ReadBoolean();
                    aic.path = ReadString(reader, stream);
                    snapshot.Aic = AivAicPresetStore.CaptureAic(aic);
                }
                ReadBytes(reader, stream, MaxImageBytes);
                int aivCount = reader.ReadInt32();
                const int legacyMaximumAivEntries = 999;
                if (aivCount < 0 || aivCount > legacyMaximumAivEntries)
                    throw new InvalidDataException($"Invalid AIV count {aivCount}.");
                for (int i = 0; i < aivCount; i++)
                {
                    byte[] aivData = ReadBytes(reader, stream, MaxAivPayloadBytes);
                    if (aivData == null || aivData.Length == 0)
                        throw new InvalidDataException($"AIV payload {i} is empty.");
                    ValidateAivPayload(aivData);
                    CustomisationFileManager.CustomAIV aiv =
                        CustomisationFileManager.CustomAIV.decode(aivData, 0);
                    aiv.workshop = reader.ReadBoolean();
                    aiv.workshopUploadInfoAvailable = reader.ReadBoolean();
                    aiv.path = ReadString(reader, stream);
                    if (snapshot.Aivs.Count < AivAicPresetStore.MaximumAivEntries)
                        snapshot.Aivs.Add(AivAicPresetStore.CaptureAiv(aiv));
                }
                if (stream.Position != stream.Length)
                    throw new InvalidDataException("Payload has trailing data.");
                return snapshot;
            }
        }

        private static void WriteReference(BinaryWriter writer, AivAicAssetReference reference)
        {
            if (reference == null)
                throw new InvalidDataException("Selection contains a null asset reference.");
            writer.Write(reference.BuiltIn);
            writer.Write(reference.Workshop);
            writer.Write(reference.LordType);
            WriteString(writer, reference.Name);
            WriteString(writer, reference.Path);
            WriteString(writer, reference.Checksum);
        }

        private static AivAicAssetReference ReadReference(BinaryReader reader, Stream stream)
        {
            var reference = new AivAicAssetReference
            {
                BuiltIn = reader.ReadBoolean(),
                Workshop = reader.ReadBoolean(),
                LordType = reader.ReadInt32(),
                Name = ReadString(reader, stream),
                Path = ReadString(reader, stream),
                Checksum = ReadString(reader, stream)
            };
            if (reference.Name == null || reference.Path == null || reference.Checksum == null)
                throw new InvalidDataException("Selection contains an incomplete asset reference.");
            return reference;
        }

        private static void ValidateAivPayload(byte[] payload)
        {
            const int nameLengthOffset = 14;
            const int nameDataOffset = 18;
            if (payload == null || payload.Length < nameDataOffset + sizeof(int))
                throw new InvalidDataException("AIV payload is truncated.");
            if (payload[0] != 1)
                throw new InvalidDataException($"Unsupported AIV payload version {payload[0]}.");
            int nameLength = BitConverter.ToInt32(payload, nameLengthOffset);
            if (nameLength < 0 || nameLength > MaxStringBytes ||
                nameDataOffset + (long)nameLength + sizeof(int) > payload.Length)
                throw new InvalidDataException($"Invalid AIV name length {nameLength}.");
            int dataCountOffset = nameDataOffset + nameLength;
            int dataCount = BitConverter.ToInt32(payload, dataCountOffset);
            long expectedLength = dataCountOffset + sizeof(int) + dataCount * (long)sizeof(short);
            if (dataCount < 0 || expectedLength != payload.Length)
                throw new InvalidDataException($"Invalid AIV data count {dataCount}.");
        }

        private static void ValidateLordConfigPayload(byte[] payload)
        {
            const int nameLengthOffset = 13;
            const int nameDataOffset = 17;
            if (payload == null || payload.Length < nameDataOffset + sizeof(int))
                throw new InvalidDataException("AIC payload is truncated.");
            if (payload[0] != 1)
                throw new InvalidDataException($"Unsupported AIC payload version {payload[0]}.");
            int nameLength = BitConverter.ToInt32(payload, nameLengthOffset);
            if (nameLength < 0 || nameLength > MaxStringBytes ||
                nameDataOffset + (long)nameLength + sizeof(int) > payload.Length)
                throw new InvalidDataException($"Invalid AIC name length {nameLength}.");
            int dataLengthOffset = nameDataOffset + nameLength;
            int dataLength = BitConverter.ToInt32(payload, dataLengthOffset);
            long expectedLength = dataLengthOffset + sizeof(int) + (long)dataLength;
            if (dataLength < 0 || expectedLength != payload.Length)
                throw new InvalidDataException($"Invalid AIC data length {dataLength}.");
        }

        private static void WriteString(BinaryWriter writer, string value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        private static string ReadString(BinaryReader reader, Stream stream)
        {
            int length = reader.ReadInt32();
            if (length == -1)
                return null;
            if (length < 0 || length > MaxStringBytes || length > stream.Length - stream.Position)
                throw new InvalidDataException($"Invalid string length {length}.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();
            return Encoding.UTF8.GetString(bytes);
        }

        private static byte[] ReadBytes(BinaryReader reader, Stream stream, int maximumLength)
        {
            int length = reader.ReadInt32();
            if (length == -1)
                return null;
            if (length < 0 || length > maximumLength || length > stream.Length - stream.Position)
                throw new InvalidDataException($"Invalid byte payload length {length}.");
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length)
                throw new EndOfStreamException();
            return bytes;
        }
    }

    internal sealed class AiAivSelectionSnapshot
    {
        public int LordType { get; set; }
        public string LordName { get; set; } = string.Empty;
        public bool BuiltIn { get; set; }
        public bool Community { get; set; }
        public bool Historical { get; set; }
        public int Rotation { get; set; }
        public bool BuiltInLord { get; set; }
        public AivAicAssetReference Aic { get; set; }
        public List<AivAicAssetReference> Aivs { get; } = new List<AivAicAssetReference>();
    }

    internal readonly struct AiAivSelectionApplyResult
    {
        public AiAivSelectionApplyResult(int loadedAivs, int missingAivs, bool missingAic)
        {
            LoadedAivs = loadedAivs;
            MissingAivs = missingAivs;
            MissingAic = missingAic;
        }

        public int LoadedAivs { get; }
        public int MissingAivs { get; }
        public bool MissingAic { get; }
    }
}
