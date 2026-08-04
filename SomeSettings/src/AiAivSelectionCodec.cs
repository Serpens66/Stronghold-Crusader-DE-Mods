using CrusaderDE;
using Noesis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SomeSettings
{
    internal static class AiAivSelectionCodec
    {
        private const string PayloadPrefix = "v2:";
        private const int PayloadVersion = 2;
        private const int MaxStringBytes = 1024 * 1024;
        private const int MaxAivPayloadBytes = 8 * 1024 * 1024;
        private const int MaxLordConfigBytes = 8 * 1024 * 1024;
        private const int MaxImageBytes = 32 * 1024 * 1024;

        public static string Encode(FRONT_Multiplayer.MPAIVInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            int aivCount = info.aivs?.Count ?? 0;
            if (aivCount > SkirmishAiSelectionMemoryHook.MaxStoredAivEntriesPerLord)
                throw new InvalidDataException(
                    $"AIV selection contains {aivCount} entries; maximum is {SkirmishAiSelectionMemoryHook.MaxStoredAivEntriesPerLord}.");

            // Vanilla also treats an empty custom selection as the default AIV mode.
            bool storeAsBuiltIn = !info.builtIn && !info.community && !info.historical && aivCount == 0;

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(PayloadVersion);
                writer.Write(info.lordType);
                WriteString(writer, info.lordName);
                writer.Write(info.builtIn || storeAsBuiltIn);
                writer.Write(storeAsBuiltIn ? false : info.community);
                writer.Write(storeAsBuiltIn ? false : info.historical);
                writer.Write(info.rotation);
                writer.Write(info.builtInLord);

                byte[] lordConfigData = info.lordConfig?.encode();
                WriteBytes(writer, lordConfigData);
                if (lordConfigData != null)
                {
                    writer.Write(info.lordConfig.workshop);
                    writer.Write(info.lordConfig.workshopUploadInfoAvailable);
                    WriteString(writer, info.lordConfig.path);
                }

                WriteBytes(writer, info.imageData);
                writer.Write(aivCount);
                for (int i = 0; i < aivCount; i++)
                {
                    CustomisationFileManager.CustomAIV aiv = info.aivs[i];
                    if (aiv == null)
                        throw new InvalidDataException($"AIV selection contains a null entry at index {i}.");

                    WriteBytes(writer, aiv.encode());
                    writer.Write(aiv.workshop);
                    writer.Write(aiv.workshopUploadInfoAvailable);
                    WriteString(writer, aiv.path);
                }

                writer.Flush();
                return PayloadPrefix +
                       Convert.ToBase64String(stream.GetBuffer(), 0, checked((int)stream.Length));
            }
        }

        public static bool TryDecode(string encoded, out FRONT_Multiplayer.MPAIVInfo info, out string error)
        {
            info = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(encoded) || !encoded.StartsWith(PayloadPrefix, StringComparison.Ordinal))
            {
                error = "unsupported or missing payload version";
                return false;
            }

            try
            {
                byte[] payload = Convert.FromBase64String(encoded.Substring(PayloadPrefix.Length));
                using (MemoryStream stream = new MemoryStream(payload, writable: false))
                using (BinaryReader reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
                {
                    int version = reader.ReadInt32();
                    if (version != PayloadVersion)
                        throw new InvalidDataException($"Unsupported payload version {version}.");

                    FRONT_Multiplayer.MPAIVInfo decoded = new FRONT_Multiplayer.MPAIVInfo
                    {
                        lordType = reader.ReadInt32(),
                        lordName = ReadString(reader, stream),
                        builtIn = reader.ReadBoolean(),
                        community = reader.ReadBoolean(),
                        historical = reader.ReadBoolean(),
                        rotation = reader.ReadInt32(),
                        builtInLord = reader.ReadBoolean()
                    };

                    byte[] lordConfigData = ReadBytes(reader, stream, MaxLordConfigBytes);
                    if (lordConfigData != null)
                    {
                        ValidateLordConfigPayload(lordConfigData);
                        decoded.lordConfig = CustomisationFileManager.CustomLordConfig.decode(lordConfigData);
                        decoded.lordConfig.workshop = reader.ReadBoolean();
                        decoded.lordConfig.workshopUploadInfoAvailable = reader.ReadBoolean();
                        decoded.lordConfig.path = ReadString(reader, stream);
                    }
                    else
                    {
                        decoded.lordConfig = null;
                    }

                    decoded.imageData = ReadBytes(reader, stream, MaxImageBytes);
                    decoded.image = LoadLordImage(decoded.imageData);

                    int aivCount = reader.ReadInt32();
                    if (aivCount < 0 || aivCount > SkirmishAiSelectionMemoryHook.MaxStoredAivEntriesPerLord)
                        throw new InvalidDataException($"Invalid AIV count {aivCount}.");

                    decoded.aivs = new List<CustomisationFileManager.CustomAIV>(aivCount);
                    HashSet<ulong> checksums = new HashSet<ulong>();
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

                        if (checksums.Add(aiv.checksum))
                            decoded.aivs.Add(aiv);
                    }

                    if (stream.Position != stream.Length)
                        throw new InvalidDataException("Payload has trailing data.");

                    info = decoded;
                    return true;
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void CopyInto(FRONT_Multiplayer.MPAIVInfo source, FRONT_Multiplayer.MPAIVInfo target)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (target == null)
                throw new ArgumentNullException(nameof(target));

            target.lordType = source.lordType;
            target.lordName = source.lordName;
            target.builtIn = source.builtIn;
            target.community = source.community;
            target.historical = source.historical;
            target.rotation = source.rotation;
            target.builtInLord = source.builtInLord;
            target.lordConfig = source.lordConfig;
            target.imageData = source.imageData;
            target.image = source.image;
            target.aivs.Clear();
            target.aivs.AddRange(source.aivs);
        }

        private static TextureSource LoadLordImage(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0 || MainViewModel.Instance == null)
                return null;

            TextureSource image = MainViewModel.Instance.LoadImageFile(imageData);
            if ((BaseComponent)(object)image == (BaseComponent)null)
                return null;

            return image.Width == 144f && image.Height == 144f ? image : null;
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
            if (nameLength < 0 ||
                nameLength > MaxStringBytes ||
                nameDataOffset + (long)nameLength + sizeof(int) > payload.Length)
            {
                throw new InvalidDataException($"Invalid AIV name length {nameLength}.");
            }

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
            if (nameLength < 0 ||
                nameLength > MaxStringBytes ||
                nameDataOffset + (long)nameLength + sizeof(int) > payload.Length)
            {
                throw new InvalidDataException($"Invalid AIC name length {nameLength}.");
            }

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

        private static void WriteBytes(BinaryWriter writer, byte[] value)
        {
            if (value == null)
            {
                writer.Write(-1);
                return;
            }

            writer.Write(value.Length);
            writer.Write(value);
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
}
