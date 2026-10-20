using System;
using System.IO;
using System.Text;

namespace ExtendedData.Core
{
    // The original map reference at the start of a saved skirmish restart block, as written by
    // HUD_IngameMenu.RestartSkirmishMapInfo.encode: version byte, member count, 12 bytes per member,
    // source byte (1 built-in, 2 Workshop, otherwise user) and the UTF-8 file name. Vanilla resolves
    // it against the installed maps; here it is only provenance and a fallback display name.
    internal sealed class TrailRestartMapReference
    {
        private const int MaximumMembers = 16;
        private const int MaximumNameBytes = 4096;

        private TrailRestartMapReference(string source, string name)
        {
            Source = source;
            Name = name;
        }

        internal string Source { get; }
        internal string Name { get; }

        internal static TrailRestartMapReference Read(byte[] restart)
        {
            if (restart == null) throw new ArgumentNullException(nameof(restart));
            int position = 1;
            int members = ReadInt32(restart, ref position);
            if (members < 0 || members > MaximumMembers)
                throw new InvalidDataException("The Trail restart block has an invalid member count.");
            Skip(restart, ref position, members * 12);
            if (position >= restart.Length)
                throw new InvalidDataException("The Trail restart block ended before its map reference.");
            byte kind = restart[position++];
            int length = ReadInt32(restart, ref position);
            if (length < 0 || length > MaximumNameBytes)
                throw new InvalidDataException("The Trail restart block has an invalid map name length.");
            if (length > restart.Length - position)
                throw new InvalidDataException("The Trail restart block ended inside its map name.");
            string name = new UTF8Encoding(false, true).GetString(restart, position, length);
            return new TrailRestartMapReference(kind == 1 ? "builtIn" : kind == 2 ? "workshop" : "user", name);
        }

        private static int ReadInt32(byte[] data, ref int position)
        {
            if (position < 0 || position > data.Length - 4)
                throw new InvalidDataException("The Trail restart block ended unexpectedly.");
            int value = BitConverter.ToInt32(data, position);
            position += 4;
            return value;
        }

        private static void Skip(byte[] data, ref int position, int count)
        {
            if (count > data.Length - position)
                throw new InvalidDataException("The Trail restart block ended unexpectedly.");
            position += count;
        }
    }
}
