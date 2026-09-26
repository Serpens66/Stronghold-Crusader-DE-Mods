using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace AIVParser.Core
{
    public static class AivRawDataDecoder
    {
        private const int GridTileCount = 10000;
        private const int MaxPauseEntries = 50;

        public static string Hash(short[] raw)
        {
            if (raw == null)
                return "<missing>";
            var bytes = new byte[raw.Length * sizeof(short)];
            Buffer.BlockCopy(raw, 0, bytes, 0, bytes.Length);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] digest = sha.ComputeHash(bytes);
                var result = new StringBuilder(digest.Length * 2);
                foreach (byte item in digest)
                    result.Append(item.ToString("x2"));
                return result.ToString();
            }
        }

        public static AivJsonDocument Decode(short[] raw)
        {
            if (raw == null)
                throw new InvalidDataException("The native AIV array is null.");

            int cursor = 0;
            int pauseDelay = Read(raw, ref cursor, "pause delay");
            int pauseCount = Read(raw, ref cursor, "pause count");
            if (pauseCount < 1 || pauseCount > MaxPauseEntries)
                throw new InvalidDataException($"Invalid native pause count: {pauseCount}.");

            var pauses = new HashSet<int>();
            for (int index = 0; index < pauseCount; index++)
            {
                int pause = Read(raw, ref cursor, $"pause[{index}]");
                if (pause < 0 || !pauses.Add(pause))
                    throw new InvalidDataException($"Invalid or duplicate native pause index: {pause}.");
            }
            if (!pauses.Contains(0))
                throw new InvalidDataException("The native pause table does not contain frame zero.");

            int frameCount = Read(raw, ref cursor, "frame count");
            if (frameCount < 1)
                throw new InvalidDataException($"Invalid native frame count: {frameCount}.");

            var frames = new List<AivJsonFrame>(frameCount);
            int keepCount = 0;
            for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
            {
                int encodedType = Read(raw, ref cursor, $"frame[{frameIndex}].itemType");
                if (encodedType == short.MinValue)
                    throw new InvalidDataException($"Invalid native item type at frame {frameIndex}: {encodedType}.");

                int itemType = Math.Abs(encodedType);
                int positionCount = encodedType > 0
                    ? 1
                    : Read(raw, ref cursor, $"frame[{frameIndex}].positionCount");
                if (positionCount < 0)
                    throw new InvalidDataException($"Invalid position count at frame {frameIndex}: {positionCount}.");
                if (encodedType == 0 && positionCount != 0)
                    throw new InvalidDataException($"Native no-op frame {frameIndex} contains positions.");

                var positions = new List<int>(positionCount);
                for (int positionIndex = 0; positionIndex < positionCount; positionIndex++)
                {
                    int position = Read(raw, ref cursor, $"frame[{frameIndex}].position[{positionIndex}]");
                    ValidatePosition(position, $"frame[{frameIndex}].position[{positionIndex}]");
                    positions.Add(position);
                }

                if (AivMapperCatalog.IsKeep(itemType))
                {
                    if (positionCount != 1)
                        throw new InvalidDataException($"Native Keep frame {frameIndex} has {positionCount} positions.");
                    keepCount += positionCount;
                }
                frames.Add(new AivJsonFrame
                {
                    itemType = itemType,
                    tilePositionOfsets = positions,
                    shouldPause = pauses.Contains(frameIndex + 2)
                });
            }

            if (keepCount != 1)
                throw new InvalidDataException($"The native AIV must contain exactly one Keep frame; found {keepCount}.");

            foreach (int pause in pauses)
            {
                if (pause != 0 && (pause < 2 || pause >= frameCount + 2))
                    throw new InvalidDataException($"Native pause index {pause} does not reference a frame.");
            }

            int miscCount = Read(raw, ref cursor, "misc item count");
            if (miscCount < 0)
                throw new InvalidDataException($"Invalid native misc item count: {miscCount}.");
            var miscItems = new List<AivJsonMiscItem>(miscCount);
            for (int index = 0; index < miscCount; index++)
            {
                int itemType = Read(raw, ref cursor, $"miscItems[{index}].itemType");
                int position = Read(raw, ref cursor, $"miscItems[{index}].position");
                int number = Read(raw, ref cursor, $"miscItems[{index}].number");
                if (itemType <= 0)
                    throw new InvalidDataException($"Invalid misc item type at index {index}: {itemType}.");
                ValidatePosition(position, $"miscItems[{index}].position");
                miscItems.Add(new AivJsonMiscItem
                {
                    itemType = itemType,
                    positionOfset = position,
                    number = number
                });
            }

            if (cursor != raw.Length)
                throw new InvalidDataException($"The native AIV contains {raw.Length - cursor} trailing Int16 values.");

            return new AivJsonDocument
            {
                pauseDelayAmount = pauseDelay,
                frames = frames,
                miscItems = miscItems
            };
        }

        private static int Read(short[] raw, ref int cursor, string field)
        {
            if (cursor >= raw.Length)
                throw new InvalidDataException($"The native AIV ended while reading {field}.");
            return raw[cursor++];
        }

        private static void ValidatePosition(int position, string field)
        {
            if (position < 0 || position >= GridTileCount)
                throw new InvalidDataException($"{field} is outside the 100x100 AIV grid: {position}.");
        }
    }
}
