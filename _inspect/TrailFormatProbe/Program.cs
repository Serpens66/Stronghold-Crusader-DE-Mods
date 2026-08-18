using System.Text;
using TrailEditor.Core;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: TrailFormatProbe <file.trail> [...]");
    return 1;
}

foreach (string path in args)
{
    byte[] restart = TrailContainerCodec.ReadTrail(path).RestartData;
    ProbeRestart(path, restart);
}

return 0;

static void ProbeRestart(string path, byte[] data)
{
    using var stream = new MemoryStream(data, writable: false);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    int version = reader.ReadByte();
    int playerCount = reader.ReadInt32();
    Skip(reader, checked(playerCount * 12));
    reader.ReadByte();
    ReadString(reader);
    ReadString(reader);
    Skip(reader, version >= 60 ? 5 : version >= 52 ? 4 : 0);

    int aiCount = version >= 53 ? reader.ReadByte() : 0;
    Console.WriteLine($"FILE={Path.GetFileName(path)} RESTART={data.Length} VERSION={version} AI_SLOTS={aiCount}");
    for (int slot = 0; slot < aiCount; slot++)
    {
        int lordType = reader.ReadInt32();
        bool builtIn = reader.ReadBoolean();
        bool community = reader.ReadBoolean();
        bool historical = reader.ReadBoolean();
        int rotation = version >= 54 ? reader.ReadInt32() : 0;
        int aivCount = reader.ReadInt32();
        var aivSizes = new List<int>();
        for (int aiv = 0; aiv < aivCount; aiv++)
        {
            int length = ReadLength(reader, "AIV");
            aivSizes.Add(length);
            Skip(reader, length);
        }

        bool builtInLord = version < 55 || reader.ReadBoolean();
        int? lordEnvelopeSize = null;
        int? lordConfigSize = null;
        int? lordConfigVersion = null;
        string? configName = null;
        if (!builtInLord)
        {
            lordEnvelopeSize = ReadLength(reader, "lord envelope");
            long envelopeEnd = checked(reader.BaseStream.Position + lordEnvelopeSize.Value);
            reader.ReadByte();
            reader.ReadInt32();
            reader.ReadUInt64();
            configName = ReadString(reader);
            lordConfigSize = ReadLength(reader, "lord config");
            long configStart = reader.BaseStream.Position;
            lordConfigVersion = reader.ReadInt32();
            reader.BaseStream.Position = configStart;
            Skip(reader, lordConfigSize.Value);
            if (reader.BaseStream.Position != envelopeEnd)
                throw new InvalidDataException("Lord envelope size does not match its contents.");
        }

        string lordName = version >= 56 ? ReadString(reader) : string.Empty;
        if (version >= 58)
            Skip(reader, ReadLength(reader, "image"));

        string aivSizeText = aivSizes.Count == 0 ? "-" : string.Join(',', aivSizes);
        Console.WriteLine(
            $"  SLOT={slot + 1} LORD_TYPE={lordType} BUILTIN={builtIn} COMMUNITY={community} HISTORICAL={historical} " +
            $"ROTATION={rotation} AIV_COUNT={aivCount} AIV_SIZES={aivSizeText} BUILTIN_LORD={builtInLord} " +
            $"LORD_ENVELOPE={lordEnvelopeSize?.ToString() ?? "-"} LORD_CONFIG={lordConfigSize?.ToString() ?? "-"} " +
            $"LORD_CONFIG_VERSION={lordConfigVersion?.ToString() ?? "-"} " +
            $"CONFIG_NAME={configName ?? "-"} LORD_NAME={lordName}");
    }
}

static int ReadLength(BinaryReader reader, string label)
{
    int length = reader.ReadInt32();
    if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
        throw new InvalidDataException($"Invalid {label} length {length} at offset {reader.BaseStream.Position - 4}.");
    return length;
}

static string ReadString(BinaryReader reader)
{
    int length = ReadLength(reader, "string");
    return Encoding.UTF8.GetString(reader.ReadBytes(length));
}

static void Skip(BinaryReader reader, int length)
{
    if (length < 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
        throw new EndOfStreamException();
    reader.BaseStream.Position += length;
}
