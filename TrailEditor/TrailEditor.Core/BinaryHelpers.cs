using System.Buffers.Binary;
using System.Text;

namespace TrailEditor.Core;

internal sealed class BinaryCursor
{
    private readonly byte[] data;

    public BinaryCursor(byte[] data)
    {
        this.data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public int Position { get; private set; }
    public int Remaining => data.Length - Position;

    public byte ReadByte(string field)
    {
        Require(1, field);
        return data[Position++];
    }

    public bool ReadBool(string field) => ReadByte(field) != 0;

    public int ReadInt32(string field)
    {
        Require(4, field);
        int value = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(Position, 4));
        Position += 4;
        return value;
    }

    public ulong ReadUInt64(string field)
    {
        Require(8, field);
        ulong value = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(Position, 8));
        Position += 8;
        return value;
    }

    public string ReadString(string field)
    {
        int length = ReadLength(field + " length", 4 * 1024 * 1024);
        byte[] bytes = ReadBytes(length, field);
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    public int ReadLength(string field, int maximum)
    {
        int length = ReadInt32(field);
        if (length < 0 || length > maximum || length > Remaining)
            throw new InvalidDataException($"Invalid {field}: {length} (remaining {Remaining}).");
        return length;
    }

    public byte[] ReadBytes(int length, string field)
    {
        Require(length, field);
        byte[] value = data.AsSpan(Position, length).ToArray();
        Position += length;
        return value;
    }

    public void RequireEnd()
    {
        if (Remaining != 0)
            throw new InvalidDataException($"Restart data has {Remaining} unexpected trailing byte(s).");
    }

    private void Require(int length, string field)
    {
        if (length < 0 || length > Remaining)
            throw new InvalidDataException($"Unexpected end while reading {field} at offset {Position}.");
    }
}

internal static class BinaryWriterExtensions
{
    public static void WriteBool(this BinaryWriter writer, bool value) => writer.Write((byte)(value ? 1 : 0));

    public static void WriteUtf8(this BinaryWriter writer, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
