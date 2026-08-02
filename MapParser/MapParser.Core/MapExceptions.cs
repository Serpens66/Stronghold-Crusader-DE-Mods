using System;

namespace MapParser.Core
{
    public class MapParseException : Exception
    {
        public MapParseException(string message)
            : base(message)
        {
        }

        public MapParseException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class MapCorruptDataException : MapParseException
    {
        public MapCorruptDataException(string message)
            : base(message)
        {
        }

        public MapCorruptDataException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class MapUnsupportedFormatException : MapParseException
    {
        public MapUnsupportedFormatException(string message)
            : base(message)
        {
        }
    }

    public sealed class MapSectionCrcException : MapCorruptDataException
    {
        public MapSectionCrcException(int sectionId, uint expected, uint actual)
            : base(
                $"Section {sectionId} CRC32 mismatch: " +
                $"expected=0x{expected:X8}, actual=0x{actual:X8}.")
        {
            SectionId = sectionId;
            Expected = expected;
            Actual = actual;
        }

        public int SectionId { get; }
        public uint Expected { get; }
        public uint Actual { get; }
    }

    public sealed class MapSectionNotFoundException : MapParseException
    {
        public MapSectionNotFoundException(int sectionId, bool logicalId)
            : base(
                $"Map section {(logicalId ? "logical " : string.Empty)}" +
                $"ID {sectionId} was not found.")
        {
            SectionId = sectionId;
            LogicalId = logicalId;
        }

        public int SectionId { get; }
        public bool LogicalId { get; }
    }
}
