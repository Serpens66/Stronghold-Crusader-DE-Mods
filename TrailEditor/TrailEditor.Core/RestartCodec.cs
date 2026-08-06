using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using SHCDESE.AICDecoder;

namespace TrailEditor.Core;

public static class RestartCodec
{
    public const int CurrentVersion = 60;
    public const int SetupVersion = -12;

    private const int LordConfigVersion1 = 1;
    private const int LordConfigVersion2 = 2;
    private const int LordConfigSharedPayloadSize = 0x450;
    private const int LordConfigVersion2TailSize = 12;
    private const int LordConfigVersion1Size = 4 + LordConfigSharedPayloadSize;
    private const int LordConfigVersion2Size = LordConfigVersion1Size + LordConfigVersion2TailSize;
    private const int InternalAicSiegeTailOffset = 0x454;

    static RestartCodec()
    {
        // Trail transfer data omits extendedLordParent, unlike the native InternalAIC layout.
        RequireInternalAicOffset(nameof(InternalAIC.extendedLordParent), 0x450);
        RequireInternalAicOffset(nameof(InternalAIC.siege_max_troops), InternalAicSiegeTailOffset);
        RequireInternalAicOffset(nameof(InternalAIC.siege_normal_wave_multiplier), 0x458);
        RequireInternalAicOffset(nameof(InternalAIC.siege_high_gold_wave_multiplier), 0x45C);
    }

    public static TrailData Decode(byte[] bytes)
    {
        var cursor = new BinaryCursor(bytes);
        int version = cursor.ReadByte("restart version");
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported restart version {version}; expected {CurrentVersion}.");

        var result = new TrailData { FormatVersion = version };
        int playerCount = ReadCount(cursor, "player count", 8);
        for (int i = 0; i < playerCount; i++)
        {
            result.Players.Add(new TrailPlayerSlot
            {
                LordType = cursor.ReadInt32($"player {i + 1} lord type"),
                Team = cursor.ReadInt32($"player {i + 1} team"),
                Colour = cursor.ReadInt32($"player {i + 1} colour")
            });
        }

        result.Map.SourceKind = cursor.ReadByte("map source kind");
        result.Map.FileName = cursor.ReadString("map filename");
        result.Setup = DecodeSetup(cursor.ReadString("multiplayer setup"));
        result.ExtremeTroops = cursor.ReadBool("extreme troops");
        result.ExtremePowers = cursor.ReadBool("extreme powers");
        result.ExtremePowersAroundLord = cursor.ReadBool("extreme powers around lord");
        result.AllowOutposts = cursor.ReadBool("allow outposts");
        result.CustomisedExtremeTrail = cursor.ReadBool("customised extreme trail");

        int aiCount = cursor.ReadByte("AI slot count");
        if (aiCount > 8)
            throw new InvalidDataException($"AI slot count {aiCount} exceeds 8.");
        for (int slotIndex = 0; slotIndex < aiCount; slotIndex++)
            result.AiSlots.Add(ReadAiSlot(cursor, slotIndex));

        result.CustomTestMission = cursor.ReadBool("custom test mission");
        result.CustomTrail = cursor.ReadBool("custom trail");
        result.CustomTrailLevel = cursor.ReadInt32("custom trail level");
        result.CustomTrailName = cursor.ReadString("custom trail name");
        result.CustomTrailDifficulty = cursor.ReadInt32("custom trail difficulty");
        cursor.RequireEnd();
        return result;
    }

    public static byte[] Encode(TrailData data)
    {
        Validate(data);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)CurrentVersion);
        writer.Write(data.Players.Count);
        foreach (TrailPlayerSlot player in data.Players)
        {
            writer.Write(player.LordType);
            writer.Write(player.Team);
            writer.Write(player.Colour);
        }

        writer.Write((byte)data.Map.SourceKind);
        writer.WriteUtf8(data.Map.FileName);
        writer.WriteUtf8(EncodeSetup(data.Setup));
        writer.WriteBool(data.ExtremeTroops);
        writer.WriteBool(data.ExtremePowers);
        writer.WriteBool(data.ExtremePowersAroundLord);
        writer.WriteBool(data.AllowOutposts);
        writer.WriteBool(data.CustomisedExtremeTrail);
        writer.Write((byte)data.AiSlots.Count);
        foreach (TrailAiSlot slot in data.AiSlots)
            WriteAiSlot(writer, slot);
        writer.WriteBool(data.CustomTestMission);
        writer.WriteBool(data.CustomTrail);
        writer.Write(data.CustomTrailLevel);
        writer.WriteUtf8(data.CustomTrailName);
        writer.Write(data.CustomTrailDifficulty);
        writer.Flush();
        return stream.ToArray();
    }

    public static MultiplayerSetupData DecodeSetup(string value)
    {
        string[] raw = value.Split(',', StringSplitOptions.None);
        if (raw.Length == 0 || !int.TryParse(raw[0], out int version) || version != SetupVersion)
            throw new InvalidDataException($"Unsupported multiplayer setup version '{raw.FirstOrDefault()}'; expected {SetupVersion}.");
        var numbers = new List<int>();
        for (int i = 1; i < raw.Length; i++)
        {
            if (i == raw.Length - 1 && raw[i].Length == 0)
                continue;
            if (!int.TryParse(raw[i], out int parsed))
                throw new InvalidDataException($"Invalid multiplayer setup integer at index {i}: '{raw[i]}'.");
            numbers.Add(parsed);
        }
        if (numbers.Count != 116)
            throw new InvalidDataException($"Multiplayer setup {SetupVersion} contains {numbers.Count} values; expected 116.");

        int p = 0;
        var data = new MultiplayerSetupData
        {
            Fairness = numbers[p++], StartingGameSpeed = numbers[p++], StartingGoodsLevel = numbers[p++],
            WinCondition = numbers[p++], AllowAutoTrading = numbers[p++], NoKnockdownWalls = numbers[p++],
            AutoSave = numbers[p++], PeaceTime = numbers[p++], NoCows = numbers[p++], NoDogs = numbers[p++],
            ExtremeTroops = numbers[p++], ExtremePowers = numbers[p++], ExtremePowersAroundLord = numbers[p++],
            AllowOutposts = numbers[p++], AdvancedOptions = numbers[p++], AdvancedSkirmishOptions = numbers[p++],
            PreBuild = numbers[p++], ImprovedArabSwordsmen = numbers[p++], ImprovedLaddermen = numbers[p++],
            ImprovedSpearmen = numbers[p++], RebalancedHorseArchers = numbers[p++], ImprovedFletchers = numbers[p++],
            UncappedPeasants = numbers[p++], FasterPeasants = numbers[p++], EnemyHitPoints = numbers[p++],
            GlobalImprovedSieging = numbers[p++], Healers = numbers[p++], Eunuchs = numbers[p++], NoGold = numbers[p++],
            GlobalImprovedSieging2 = numbers[p++]
        };
        data.BuildingsAvailable = Take(numbers, ref p, 13);
        data.GoodsAvailable = Take(numbers, ref p, 25);
        data.TroopsAvailable = Take(numbers, ref p, 32);
        data.PreferredAivs = Take(numbers, ref p, 8);
        data.KeepLocationOrder = Take(numbers, ref p, 8);
        return data;
    }

    public static string EncodeSetup(MultiplayerSetupData data)
    {
        RequireArray(data.BuildingsAvailable, 13, nameof(data.BuildingsAvailable));
        RequireArray(data.GoodsAvailable, 25, nameof(data.GoodsAvailable));
        RequireArray(data.TroopsAvailable, 32, nameof(data.TroopsAvailable));
        RequireArray(data.PreferredAivs, 8, nameof(data.PreferredAivs));
        RequireArray(data.KeepLocationOrder, 8, nameof(data.KeepLocationOrder));
        var values = new List<int>
        {
            SetupVersion, data.Fairness, data.StartingGameSpeed, data.StartingGoodsLevel, data.WinCondition,
            data.AllowAutoTrading, data.NoKnockdownWalls, data.AutoSave, data.PeaceTime, data.NoCows, data.NoDogs,
            data.ExtremeTroops, data.ExtremePowers, data.ExtremePowersAroundLord, data.AllowOutposts,
            data.AdvancedOptions, data.AdvancedSkirmishOptions, data.PreBuild, data.ImprovedArabSwordsmen,
            data.ImprovedLaddermen, data.ImprovedSpearmen, data.RebalancedHorseArchers, data.ImprovedFletchers,
            data.UncappedPeasants, data.FasterPeasants, data.EnemyHitPoints, data.GlobalImprovedSieging,
            data.Healers, data.Eunuchs, data.NoGold, data.GlobalImprovedSieging2
        };
        values.AddRange(data.BuildingsAvailable);
        values.AddRange(data.GoodsAvailable);
        values.AddRange(data.TroopsAvailable);
        values.AddRange(data.PreferredAivs);
        values.AddRange(data.KeepLocationOrder);
        return string.Join(',', values) + ",";
    }

    private static TrailAiSlot ReadAiSlot(BinaryCursor cursor, int slotIndex)
    {
        var slot = new TrailAiSlot
        {
            LordType = cursor.ReadInt32($"AI slot {slotIndex + 1} lord type"),
            BuiltIn = cursor.ReadBool($"AI slot {slotIndex + 1} built-in"),
            Community = cursor.ReadBool($"AI slot {slotIndex + 1} community"),
            Historical = cursor.ReadBool($"AI slot {slotIndex + 1} historical"),
            Rotation = cursor.ReadInt32($"AI slot {slotIndex + 1} rotation")
        };
        int aivCount = ReadCount(cursor, $"AI slot {slotIndex + 1} AIV count", 64);
        for (int i = 0; i < aivCount; i++)
        {
            int length = cursor.ReadLength($"AI slot {slotIndex + 1} AIV {i + 1} size", 32 * 1024 * 1024);
            slot.Aivs.Add(DecodeCustomAiv(cursor.ReadBytes(length, "custom AIV")));
        }
        slot.BuiltInLord = cursor.ReadBool($"AI slot {slotIndex + 1} built-in lord");
        if (!slot.BuiltInLord)
        {
            int length = cursor.ReadLength($"AI slot {slotIndex + 1} lord config size", 4 * 1024 * 1024);
            slot.LordConfig = DecodeCustomLord(cursor.ReadBytes(length, "custom lord"));
        }
        slot.LordName = cursor.ReadString($"AI slot {slotIndex + 1} lord name");
        int imageLength = cursor.ReadLength($"AI slot {slotIndex + 1} image size", 32 * 1024 * 1024);
        slot.ImageData = imageLength == 0 ? null : cursor.ReadBytes(imageLength, "lord image");
        return slot;
    }

    private static void WriteAiSlot(BinaryWriter writer, TrailAiSlot slot)
    {
        writer.Write(slot.LordType);
        writer.WriteBool(slot.BuiltIn);
        writer.WriteBool(slot.Community);
        writer.WriteBool(slot.Historical);
        writer.Write(slot.Rotation);
        writer.Write(slot.Aivs.Count);
        foreach (CustomAivData aiv in slot.Aivs)
        {
            byte[] encoded = EncodeCustomAiv(aiv);
            writer.Write(encoded.Length);
            writer.Write(encoded);
        }
        writer.WriteBool(slot.BuiltInLord);
        if (!slot.BuiltInLord)
        {
            if (slot.LordConfig == null)
                throw new InvalidDataException("A non-built-in lord requires a lord config.");
            byte[] encoded = EncodeCustomLord(slot.LordConfig);
            writer.Write(encoded.Length);
            writer.Write(encoded);
        }
        writer.WriteUtf8(slot.LordName);
        writer.Write(slot.ImageData?.Length ?? 0);
        if (slot.ImageData != null)
            writer.Write(slot.ImageData);
    }

    private static CustomAivData DecodeCustomAiv(byte[] bytes)
    {
        var cursor = new BinaryCursor(bytes);
        int version = cursor.ReadByte("custom AIV version");
        if (version != 1)
            throw new InvalidDataException($"Unsupported custom AIV version {version}.");
        var result = new CustomAivData
        {
            FormatVersion = version,
            LordType = cursor.ReadInt32("custom AIV lord type"),
            BuiltIn = cursor.ReadBool("custom AIV built-in"),
            Checksum = cursor.ReadUInt64("custom AIV checksum"),
            Name = cursor.ReadString("custom AIV name")
        };
        int count = ReadCount(cursor, "custom AIV word count", 16 * 1024 * 1024);
        if (cursor.Remaining != count * 2)
            throw new InvalidDataException("Custom AIV word count does not match its payload.");
        result.Data = new short[count];
        for (int i = 0; i < count; i++)
            result.Data[i] = unchecked((short)(cursor.ReadByte("AIV low byte") | cursor.ReadByte("AIV high byte") << 8));
        cursor.RequireEnd();
        return result;
    }

    private static byte[] EncodeCustomAiv(CustomAivData data)
    {
        // Built-in AIVs use stable catalogue IDs (1..8, 51..58, 61) instead of content CRCs.
        if (!data.BuiltIn)
            data.Checksum = TrailCrc.CreateCombined(data.Name, ToLittleEndianBytes(data.Data));
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write((byte)1);
        writer.Write(data.LordType);
        writer.WriteBool(data.BuiltIn);
        writer.Write(data.Checksum);
        writer.WriteUtf8(data.Name);
        writer.Write(data.Data.Length);
        foreach (short value in data.Data)
            writer.Write(value);
        writer.Flush();
        return stream.ToArray();
    }

    private static CustomLordData DecodeCustomLord(byte[] bytes)
    {
        var cursor = new BinaryCursor(bytes);
        int version = cursor.ReadByte("custom lord version");
        if (version != 1)
            throw new InvalidDataException($"Unsupported custom lord version {version}.");
        int lordType = cursor.ReadInt32("custom lord type");
        ulong checksum = cursor.ReadUInt64("custom lord checksum");
        string name = cursor.ReadString("custom lord name");
        int configLength = cursor.ReadLength("custom lord config size", 4 * 1024 * 1024);
        byte[] configBytes = cursor.ReadBytes(configLength, "custom lord config");
        cursor.RequireEnd();
        (int configVersion, InternalAIC config) = DecodeLordConfig(configBytes);
        return new CustomLordData
        {
            FormatVersion = version,
            LordType = lordType,
            Checksum = checksum,
            Name = name,
            ConfigVersion = configVersion,
            Config = config
        };
    }

    private static byte[] EncodeCustomLord(CustomLordData data)
    {
        byte[] configBytes = EncodeLordConfig(data.Config, data.ConfigVersion);
        data.Checksum = TrailCrc.CreateCombined(data.Name, configBytes);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write((byte)1);
        writer.Write(data.LordType);
        writer.Write(data.Checksum);
        writer.WriteUtf8(data.Name);
        writer.Write(configBytes.Length);
        writer.Write(configBytes);
        writer.Flush();
        return stream.ToArray();
    }

    internal static byte[] EncodeLordConfig(InternalAIC config, int version = 2)
    {
        int size = GetLordConfigSize(version);
        byte[] bytes = new byte[size];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, version);
        ReadOnlySpan<byte> internalBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref config, 1));
        internalBytes[..LordConfigSharedPayloadSize].CopyTo(bytes.AsSpan(4));
        if (version == LordConfigVersion2)
        {
            // Skip extendedLordParent, which exists only in the native runtime structure.
            internalBytes.Slice(InternalAicSiegeTailOffset, LordConfigVersion2TailSize)
                .CopyTo(bytes.AsSpan(LordConfigVersion1Size));
        }
        return bytes;
    }

    private static (int Version, InternalAIC Config) DecodeLordConfig(byte[] bytes)
    {
        if (bytes.Length < 4)
            throw new InvalidDataException($"Custom lord config is {bytes.Length} bytes; expected at least 4.");
        int version = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        int expectedSize = GetLordConfigSize(version);
        if (bytes.Length != expectedSize)
            throw new InvalidDataException($"Custom lord config version {version} is {bytes.Length} bytes; expected {expectedSize}.");

        var config = new InternalAIC();
        Span<byte> internalBytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref config, 1));
        bytes.AsSpan(4, LordConfigSharedPayloadSize).CopyTo(internalBytes);
        if (version == LordConfigVersion2)
        {
            bytes.AsSpan(LordConfigVersion1Size, LordConfigVersion2TailSize)
                .CopyTo(internalBytes.Slice(InternalAicSiegeTailOffset, LordConfigVersion2TailSize));
        }
        else
        {
            // These are the defaults used by the game's decoder for version 1 payloads.
            config.siege_max_troops = 200;
            config.siege_normal_wave_multiplier = 5;
            config.siege_high_gold_wave_multiplier = 7;
        }
        return (version, config);
    }

    private static int GetLordConfigSize(int version) => version switch
    {
        LordConfigVersion1 => LordConfigVersion1Size,
        LordConfigVersion2 => LordConfigVersion2Size,
        _ => throw new InvalidDataException($"Unsupported custom lord config version {version}.")
    };

    private static void RequireInternalAicOffset(string field, int expected)
    {
        int actual = Marshal.OffsetOf<InternalAIC>(field).ToInt32();
        if (actual != expected)
            throw new InvalidOperationException($"InternalAIC.{field} moved to 0x{actual:X}; expected 0x{expected:X}.");
    }

    private static void Validate(TrailData data)
    {
        if (data.FormatVersion != CurrentVersion)
            throw new InvalidDataException($"Only restart version {CurrentVersion} can be written.");
        if (data.Players.Count > 8 || data.AiSlots.Count > 8)
            throw new InvalidDataException("At most eight player and AI slots are supported.");
        if (data.Map.SourceKind is < 0 or > 2)
            throw new InvalidDataException("Map source kind must be 0 (local), 1 (built-in), or 2 (workshop).");
        _ = SetupSemantics.GetStartingGold(data);
    }

    private static int ReadCount(BinaryCursor cursor, string field, int maximum)
    {
        int value = cursor.ReadInt32(field);
        if (value < 0 || value > maximum)
            throw new InvalidDataException($"Invalid {field}: {value}.");
        return value;
    }

    private static int[] Take(List<int> values, ref int position, int count)
    {
        int[] result = values.GetRange(position, count).ToArray();
        position += count;
        return result;
    }

    private static void RequireArray(int[]? value, int length, string name)
    {
        if (value == null || value.Length != length)
            throw new InvalidDataException($"{name} must contain exactly {length} values.");
    }

    private static byte[] ToLittleEndianBytes(short[] values)
    {
        byte[] bytes = new byte[values.Length * 2];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }
}

internal static class TrailCrc
{
    public static ulong CreateCombined(string name, byte[] payload)
    {
        uint nameCrc = Compute(Encoding.UTF8.GetBytes(name ?? string.Empty));
        return ((ulong)nameCrc << 32) | Compute(payload);
    }

    public static uint Compute(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++)
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return crc;
    }
}
