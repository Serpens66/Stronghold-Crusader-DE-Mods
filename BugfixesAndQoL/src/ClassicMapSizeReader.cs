// Feature: Recover classic/HD map sizes from the canonical map-size section.
using SHCDESE.API;
using System;
using System.IO;

namespace BugfixesAndQoL
{
    internal static class ClassicMapSizeReader
    {
        private const int ClassicHeaderId = -1;
        private const int MapSizeSectionId = 1050;
        private const int ScalarSectionSize = sizeof(int);
        private const int DirectoryHeaderSize = 28;
        private const int DirectoryArrayCount = 5;
        private const int OldDirectoryTag = 2036;
        private const int StandardDirectoryTag = 3036;
        private const int ExtendedDirectoryTag = 4036;

        internal static bool ShouldPopulate(
            bool settingActive,
            bool classicSave,
            int currentWorldSize,
            string path)
        {
            return settingActive && classicSave && currentWorldSize <= 0 &&
                !string.IsNullOrWhiteSpace(path) &&
                string.Equals(Path.GetExtension(path), ".map", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool TryRead(string path, out int worldSize)
        {
            worldSize = default;
            try
            {
                using (var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                {
                    return TryRead(stream, out worldSize);
                }
            }
            catch (Exception)
            {
                // A changing or malformed user map must never break Vanilla's background scan.
                worldSize = default;
                return false;
            }
        }

        internal static bool TryRead(Stream stream, out int worldSize)
        {
            worldSize = default;
            if (stream == null || !stream.CanRead || !stream.CanSeek)
                return false;

            try
            {
                stream.Position = 0;
                if (!TryReadInt32(stream, out int headerId) || headerId != ClassicHeaderId)
                    return false;

                int u4Size = 0;
                for (int blockIndex = 0; blockIndex < 6; blockIndex++)
                {
                    if (!TryReadInt32(stream, out int blockSize) || blockSize < 0 ||
                        !TrySkip(stream, blockSize))
                    {
                        return false;
                    }

                    if (blockIndex == 5)
                        u4Size = blockSize;
                }

                if (u4Size > 0)
                {
                    if (!TryReadInt32(stream, out int restartSize) || restartSize < 0 ||
                        !TrySkip(stream, restartSize) ||
                        (restartSize > 0 && !TrySkip(stream, sizeof(int))))
                    {
                        return false;
                    }
                }

                if (!TryReadInt32(stream, out int directoryTag) || !IsKnownDirectoryTag(directoryTag))
                    return false;

                long directoryBodyOffset = stream.Position;
                int directoryBodySize = directoryTag - sizeof(int);
                if (!IsRangeInside(stream, directoryBodyOffset, directoryBodySize) ||
                    !TryReadInt32(stream, out int payloadSize) || payloadSize < 0 ||
                    !TryReadInt32(stream, out int sectionCount))
                {
                    return false;
                }

                int capacity = (directoryTag - 36) / (DirectoryArrayCount * sizeof(int));
                if (sectionCount < 0 || sectionCount > capacity)
                    return false;

                long payloadOffset = directoryBodyOffset + directoryBodySize;
                if (!IsRangeInside(stream, payloadOffset, payloadSize))
                    return false;

                long arraysOffset = directoryBodyOffset + DirectoryHeaderSize;
                for (int sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
                {
                    if (!TryReadDirectoryValue(
                            stream, arraysOffset, capacity, arrayIndex: 2, sectionIndex, out int sectionId))
                    {
                        return false;
                    }
                    if (sectionId != MapSizeSectionId)
                        continue;

                    if (!TryReadDirectoryValue(stream, arraysOffset, capacity, 0, sectionIndex, out int unpackedSize) ||
                        !TryReadDirectoryValue(stream, arraysOffset, capacity, 1, sectionIndex, out int storedSize) ||
                        !TryReadDirectoryValue(stream, arraysOffset, capacity, 3, sectionIndex, out int compressionFlag) ||
                        !TryReadDirectoryValue(stream, arraysOffset, capacity, 4, sectionIndex, out int relativeOffset) ||
                        unpackedSize != ScalarSectionSize || storedSize != ScalarSectionSize ||
                        compressionFlag != 0 || relativeOffset < 0 ||
                        relativeOffset > payloadSize - ScalarSectionSize)
                    {
                        return false;
                    }

                    stream.Position = payloadOffset + relativeOffset;
                    if (!TryReadInt32(stream, out int candidate) ||
                        candidate <= 0 || candidate > GameTileManagerAPI.MAX_WIDTH)
                    {
                        return false;
                    }

                    worldSize = candidate;
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                worldSize = default;
                return false;
            }
        }

        private static bool IsKnownDirectoryTag(int directoryTag)
        {
            return directoryTag == OldDirectoryTag ||
                directoryTag == StandardDirectoryTag ||
                directoryTag == ExtendedDirectoryTag;
        }

        private static bool TryReadDirectoryValue(
            Stream stream,
            long arraysOffset,
            int capacity,
            int arrayIndex,
            int sectionIndex,
            out int value)
        {
            long offset = arraysOffset +
                ((long)arrayIndex * capacity + sectionIndex) * sizeof(int);
            if (!IsRangeInside(stream, offset, sizeof(int)))
            {
                value = default;
                return false;
            }

            stream.Position = offset;
            return TryReadInt32(stream, out value);
        }

        private static bool TrySkip(Stream stream, int byteCount)
        {
            if (byteCount < 0 || !IsRangeInside(stream, stream.Position, byteCount))
                return false;

            stream.Position += byteCount;
            return true;
        }

        private static bool IsRangeInside(Stream stream, long offset, long size)
        {
            return offset >= 0 && size >= 0 && offset <= stream.Length - size;
        }

        private static bool TryReadInt32(Stream stream, out int value)
        {
            value = default;
            if (!IsRangeInside(stream, stream.Position, sizeof(int)))
                return false;

            int b0 = stream.ReadByte();
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            int b3 = stream.ReadByte();
            if ((b0 | b1 | b2 | b3) < 0)
                return false;

            value = b0 | b1 << 8 | b2 << 16 | b3 << 24;
            return true;
        }
    }
}
