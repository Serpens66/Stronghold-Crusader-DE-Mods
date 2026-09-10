using System.Buffers.Binary;
using System.Security.Cryptography;
using MapParser.Core;

namespace TrailEditor.Core;

public sealed class TrailContainerDocument
{
    internal TrailContainerDocument(byte[] bytes, MapDocument map, byte[] restartData)
    {
        Bytes = bytes;
        Map = map;
        RestartData = restartData;
    }

    public byte[] Bytes { get; }
    public MapDocument Map { get; }
    public byte[] RestartData { get; }
}

public static class TrailContainerCodec
{
    public static TrailContainerDocument ReadTrail(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".trail", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Trail input must use the .trail extension.");
        byte[] bytes = File.ReadAllBytes(path);
        MapDocument map = MapFileReader.Parse(bytes);
        MapPreambleInfo preamble = map.Preamble;
        if (preamble.RestartSizeFieldOffset < 0 || preamble.RestartInfoSize == 0)
            throw new InvalidDataException("The file does not contain a trail restart block.");
        ValidateTerminator(bytes, preamble);
        byte[] restart = bytes.AsSpan(preamble.RestartPayloadOffset, checked((int)preamble.RestartInfoSize)).ToArray();
        return new TrailContainerDocument(bytes, map, restart);
    }

    public static byte[] ExtractMap(TrailContainerDocument trail)
    {
        MapPreambleInfo p = trail.Map.Preamble;
        using var output = new MemoryStream(trail.Bytes.Length - trail.RestartData.Length - 4);
        output.Write(trail.Bytes, 0, p.RestartSizeFieldOffset);
        output.Write(new byte[4]); // A regular map carries an empty restart marker.
        output.Write(trail.Bytes, p.DirectoryTagOffset, trail.Bytes.Length - p.DirectoryTagOffset);
        byte[] mapBytes = output.ToArray();
        MapDocument parsed = MapFileReader.Parse(mapBytes);
        if (parsed.Preamble.RestartInfoSize != 0)
            throw new InvalidDataException("Extracted map unexpectedly retained restart data.");
        return mapBytes;
    }

    public static byte[] BuildTrail(byte[] mapBytes, byte[] restartData)
    {
        if (restartData == null || restartData.Length == 0)
            throw new InvalidDataException("A trail requires non-empty restart data.");
        MapDocument map = MapFileReader.Parse(mapBytes);
        MapPreambleInfo p = map.Preamble;
        if (p.RestartSizeFieldOffset < 0)
            throw new InvalidDataException("The map has no restart marker in its preamble.");
        if (p.RestartInfoSize != 0)
            ValidateTerminator(mapBytes, p);

        using var output = new MemoryStream(checked(mapBytes.Length - (int)p.RestartInfoSize + restartData.Length + 4));
        output.Write(mapBytes, 0, p.RestartSizeFieldOffset);
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(length, restartData.Length);
        output.Write(length);
        output.Write(restartData);
        output.Write(new byte[4]);
        output.Write(mapBytes, p.DirectoryTagOffset, mapBytes.Length - p.DirectoryTagOffset);
        byte[] result = output.ToArray();
        MapDocument reparsed = MapFileReader.Parse(result);
        if (reparsed.Preamble.RestartInfoSize != restartData.Length)
            throw new InvalidDataException("Repacked trail failed restart-size verification.");
        return result;
    }

    public static byte[] ReadRestart(byte[] trailBytes)
    {
        MapDocument map = MapFileReader.Parse(trailBytes);
        MapPreambleInfo p = map.Preamble;
        if (p.RestartInfoSize == 0)
            throw new InvalidDataException("Container has no restart data.");
        ValidateTerminator(trailBytes, p);
        return trailBytes.AsSpan(p.RestartPayloadOffset, checked((int)p.RestartInfoSize)).ToArray();
    }

    public static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    private static void ValidateTerminator(byte[] bytes, MapPreambleInfo p)
    {
        if (p.RestartTerminatorOffset < 0 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(p.RestartTerminatorOffset, 4)) != 0)
        {
            throw new InvalidDataException("Restart block terminator is missing or non-zero.");
        }
    }
}
