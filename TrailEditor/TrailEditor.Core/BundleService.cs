using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MapParser.Core;
using Serilog;
using SHCDESE.AICDecoder;
using SHCDESE.AIVDecoder.Models;
using SHCDESE.AIVDecoder.Services;

namespace TrailEditor.Core;

public sealed class BundleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        IncludeFields = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly ILogger codecLogger = new LoggerConfiguration().MinimumLevel.Fatal().CreateLogger();

    public TrailManifest Export(string trailPath, string bundleDirectory)
    {
        string fullBundle = Path.GetFullPath(bundleDirectory);
        if (Directory.Exists(fullBundle) || File.Exists(fullBundle))
            throw new IOException($"Bundle target already exists: {fullBundle}");

        TrailContainerDocument container = TrailContainerCodec.ReadTrail(trailPath);
        TrailData trail = RestartCodec.Decode(container.RestartData);
        Directory.CreateDirectory(fullBundle);
        Directory.CreateDirectory(Path.Combine(fullBundle, "aivs"));
        Directory.CreateDirectory(Path.Combine(fullBundle, "lords"));
        Directory.CreateDirectory(Path.Combine(fullBundle, "images"));

        WriteNewBytes(Path.Combine(fullBundle, "map.map"), TrailContainerCodec.ExtractMap(container));
        var manifest = new TrailManifest
        {
            OriginalFileName = Path.GetFileName(trailPath),
            OriginalSha256 = TrailContainerCodec.Sha256(container.Bytes),
            Trail = ToManifest(trail, fullBundle)
        };
        WriteJson(Path.Combine(fullBundle, "trail.json"), manifest);
        return manifest;
    }

    public byte[] Build(string manifestPath)
    {
        string fullManifest = Path.GetFullPath(manifestPath);
        string bundle = Path.GetDirectoryName(fullManifest) ?? throw new InvalidDataException("Manifest has no directory.");
        TrailManifest manifest = ReadJson<TrailManifest>(fullManifest);
        if (manifest.SchemaVersion != 1)
            throw new InvalidDataException($"Unsupported bundle schema {manifest.SchemaVersion}; expected 1.");
        string mapPath = ResolveBundlePath(bundle, manifest.MapFile);
        byte[] mapBytes = File.ReadAllBytes(mapPath);
        MapDocument map = MapFileReader.Parse(mapBytes);
        TrailData trail = FromManifest(manifest.Trail, bundle);
        if (!string.IsNullOrWhiteSpace(map.Metadata.StandaloneFileName))
            trail.Map.FileName = map.Metadata.StandaloneFileName;
        byte[] restart = RestartCodec.Encode(trail);
        byte[] result = TrailContainerCodec.BuildTrail(mapBytes, restart);
        RestartCodec.Decode(TrailContainerCodec.ReadRestart(result));
        return result;
    }

    public TrailManifest ReadManifest(string path) => ReadJson<TrailManifest>(Path.GetFullPath(path));

    private TrailManifestData ToManifest(TrailData source, string bundle)
    {
        var result = new TrailManifestData
        {
            FormatVersion = source.FormatVersion,
            Players = source.Players,
            Map = source.Map,
            Setup = source.Setup,
            ExtremeTroops = source.ExtremeTroops,
            ExtremePowers = source.ExtremePowers,
            ExtremePowersAroundLord = source.ExtremePowersAroundLord,
            AllowOutposts = source.AllowOutposts,
            CustomisedExtremeTrail = source.CustomisedExtremeTrail,
            CustomTestMission = source.CustomTestMission,
            CustomTrail = source.CustomTrail,
            CustomTrailLevel = source.CustomTrailLevel,
            CustomTrailName = source.CustomTrailName,
            CustomTrailDifficulty = source.CustomTrailDifficulty
        };

        for (int slotIndex = 0; slotIndex < source.AiSlots.Count; slotIndex++)
        {
            TrailAiSlot slot = source.AiSlots[slotIndex];
            var target = new TrailAiSlotManifest
            {
                LordType = slot.LordType,
                BuiltIn = slot.BuiltIn,
                Community = slot.Community,
                Historical = slot.Historical,
                Rotation = slot.Rotation,
                BuiltInLord = slot.BuiltInLord,
                LordName = slot.LordName
            };

            for (int aivIndex = 0; aivIndex < slot.Aivs.Count; aivIndex++)
            {
                CustomAivData aiv = slot.Aivs[aivIndex];
                string relative = $"aivs/slot-{slotIndex + 1}-aiv-{aivIndex + 1}.aivjson";
                SaveData decoded = new AIVDecoder(codecLogger).Decode(aiv.Data);
                WriteJson(ResolveBundlePath(bundle, relative), decoded);
                target.Aivs.Add(new CustomAivManifest
                {
                    FormatVersion = aiv.FormatVersion,
                    LordType = aiv.LordType,
                    BuiltIn = aiv.BuiltIn,
                    Checksum = aiv.Checksum,
                    Name = aiv.Name,
                    DataFile = relative
                });
            }

            if (slot.LordConfig != null)
            {
                string lordFile = $"lords/slot-{slotIndex + 1}.lordjson";
                string internalsFile = $"lords/slot-{slotIndex + 1}.internals.json";
                WriteJson(ResolveBundlePath(bundle, lordFile), new LordJsonWrapper
                {
                    Lord = PublicAIC.FromInternal(slot.LordConfig.Config)
                });
                WriteJson(ResolveBundlePath(bundle, internalsFile), ReadInternals(slot.LordConfig.Config));
                target.LordConfig = new CustomLordManifest
                {
                    FormatVersion = slot.LordConfig.FormatVersion,
                    LordType = slot.LordConfig.LordType,
                    Name = slot.LordConfig.Name,
                    ConfigVersion = slot.LordConfig.ConfigVersion,
                    LordJsonFile = lordFile,
                    InternalsFile = internalsFile
                };
            }

            if (slot.ImageData is { Length: > 0 })
            {
                string extension = ImageProbe.GetExtension(slot.ImageData);
                string imageFile = $"images/slot-{slotIndex + 1}{extension}";
                WriteNewBytes(ResolveBundlePath(bundle, imageFile), slot.ImageData);
                target.ImageFile = imageFile;
                target.OriginalImageSha256 = TrailContainerCodec.Sha256(slot.ImageData);
            }
            result.AiSlots.Add(target);
        }
        return result;
    }

    private TrailData FromManifest(TrailManifestData source, string bundle)
    {
        var result = new TrailData
        {
            FormatVersion = source.FormatVersion,
            Players = source.Players ?? new(),
            Map = source.Map ?? new(),
            Setup = source.Setup ?? new(),
            ExtremeTroops = source.ExtremeTroops,
            ExtremePowers = source.ExtremePowers,
            ExtremePowersAroundLord = source.ExtremePowersAroundLord,
            AllowOutposts = source.AllowOutposts,
            CustomisedExtremeTrail = source.CustomisedExtremeTrail,
            CustomTestMission = source.CustomTestMission,
            CustomTrail = source.CustomTrail,
            CustomTrailLevel = source.CustomTrailLevel,
            CustomTrailName = source.CustomTrailName ?? string.Empty,
            CustomTrailDifficulty = source.CustomTrailDifficulty
        };

        foreach (TrailAiSlotManifest slot in source.AiSlots ?? new())
        {
            var target = new TrailAiSlot
            {
                LordType = slot.LordType,
                BuiltIn = slot.BuiltIn,
                Community = slot.Community,
                Historical = slot.Historical,
                Rotation = slot.Rotation,
                BuiltInLord = slot.BuiltInLord,
                LordName = slot.LordName ?? string.Empty
            };
            foreach (CustomAivManifest aiv in slot.Aivs ?? new())
            {
                SaveData save = ReadJson<SaveData>(ResolveBundlePath(bundle, aiv.DataFile));
                short[] words = new AIVEncoder(codecLogger).Encode(save);
                target.Aivs.Add(new CustomAivData
                {
                    FormatVersion = aiv.FormatVersion,
                    LordType = aiv.LordType,
                    BuiltIn = aiv.BuiltIn,
                    Checksum = aiv.Checksum,
                    Name = aiv.Name ?? string.Empty,
                    Data = words
                });
            }

            if (slot.LordConfig != null)
            {
                LordJsonWrapper publicJson = ReadJson<LordJsonWrapper>(ResolveBundlePath(bundle, slot.LordConfig.LordJsonFile));
                if (publicJson.Lord == null)
                    throw new InvalidDataException("Lord JSON must contain a 'lord' object.");
                AicInternals internals = ReadJson<AicInternals>(ResolveBundlePath(bundle, slot.LordConfig.InternalsFile));
                InternalAIC config = ApplyInternals(publicJson.Lord.ToInternal(), internals);
                target.LordConfig = new CustomLordData
                {
                    FormatVersion = slot.LordConfig.FormatVersion,
                    LordType = slot.LordConfig.LordType,
                    Name = slot.LordConfig.Name ?? string.Empty,
                    ConfigVersion = slot.LordConfig.ConfigVersion,
                    Config = config
                };
            }

            if (!string.IsNullOrWhiteSpace(slot.ImageFile))
            {
                byte[] image = File.ReadAllBytes(ResolveBundlePath(bundle, slot.ImageFile));
                if (ImageProbe.TryGetDimensions(image, out int width, out int height))
                {
                    if (width != 144 || height != 144)
                        throw new InvalidDataException($"Lord image '{slot.ImageFile}' is {width}x{height}; expected 144x144.");
                }
                else if (!string.Equals(TrailContainerCodec.Sha256(image), slot.OriginalImageSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Changed lord image '{slot.ImageFile}' has an unsupported format.");
                }
                target.ImageData = image;
            }
            result.AiSlots.Add(target);
        }
        return result;
    }

    private static AicInternals ReadInternals(InternalAIC config)
    {
        // PublicAIC deliberately omits this field, but the trail transfer format preserves it.
        return new AicInternals
        {
            Fields = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [nameof(InternalAIC.opponent_type_for_speech)] = config.opponent_type_for_speech
            }
        };
    }

    private static InternalAIC ApplyInternals(InternalAIC config, AicInternals internals)
    {
        const string expected = nameof(InternalAIC.opponent_type_for_speech);
        if (internals.Fields == null || internals.Fields.Count != 1 ||
            !internals.Fields.TryGetValue(expected, out int opponentTypeForSpeech))
        {
            throw new InvalidDataException($"AIC internals must contain exactly this field: {expected}.");
        }
        config.opponent_type_for_speech = opponentTypeForSpeech;
        return config;
    }

    public static string ResolveBundlePath(string bundle, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            throw new InvalidDataException($"Bundle path must be relative: '{relativePath}'.");
        string root = Path.GetFullPath(bundle).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string resolved = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Bundle path escapes its root: '{relativePath}'.");
        return resolved;
    }

    public static void WriteJson<T>(string path, T value)
    {
        string json = JsonSerializer.Serialize(value, JsonOptions);
        string crlf = json.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal) + "\r\n";
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(crlf);
        }
        if (!string.Equals(crlf, File.ReadAllText(path, Encoding.UTF8), StringComparison.Ordinal))
            throw new IOException($"Text verification failed after writing '{path}'.");
    }

    private static T ReadJson<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path, Encoding.UTF8), JsonOptions)
                ?? throw new InvalidDataException($"JSON file is empty: {path}");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid JSON '{path}': {ex.Message}", ex);
        }
    }

    private static void WriteNewBytes(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
    }

    private sealed class LordJsonWrapper
    {
        [JsonPropertyName("lord")]
        public PublicAIC? Lord { get; set; }
    }
}

internal static class ImageProbe
{
    public static string GetExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return ".png";
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8)
            return ".jpg";
        return ".bin";
    }

    public static bool TryGetDimensions(byte[] bytes, out int width, out int height)
    {
        width = height = 0;
        if (GetExtension(bytes) == ".png" && bytes.Length >= 24)
        {
            width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
            return width > 0 && height > 0;
        }
        if (GetExtension(bytes) != ".jpg")
            return false;
        int offset = 2;
        while (offset + 9 < bytes.Length)
        {
            if (bytes[offset++] != 0xFF)
                continue;
            byte marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9)
                continue;
            if (offset + 2 > bytes.Length)
                return false;
            int length = (bytes[offset] << 8) | bytes[offset + 1];
            if (length < 2 || offset + length > bytes.Length)
                return false;
            if (marker is >= 0xC0 and <= 0xC3 or >= 0xC5 and <= 0xC7 or >= 0xC9 and <= 0xCB or >= 0xCD and <= 0xCF)
            {
                height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                return width > 0 && height > 0;
            }
            offset += length;
        }
        return false;
    }
}
