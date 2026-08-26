using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;

namespace CastlePlanner
{
    internal static class AivRawDataEncoder
    {
        private const int GridTileCount = 10000;
        private const int MaxPauseEntries = 50;
        // Native AIV preparation owns a fixed 1000-entry build queue.
        private const int MaxFrameEntries = 1000;

        public static short[] Encode(AivJsonDocument document)
        {
            if (document == null)
                throw new InvalidDataException("The AIVJSON document is null.");
            if (document.frames == null)
                throw new InvalidDataException("The AIVJSON frames array is missing.");
            if (document.miscItems == null)
                throw new InvalidDataException("The AIVJSON miscItems array is missing.");
            if (document.frames.Count == 0)
                throw new InvalidDataException("The AIVJSON frames array is empty.");
            if (document.frames.Count > MaxFrameEntries)
            {
                throw new InvalidDataException(
                    $"The AIVJSON frames count exceeds the native {MaxFrameEntries}-frame queue: {document.frames.Count}.");
            }

            var raw = new List<short>();
            raw.Add(ToShort(document.pauseDelayAmount, "pauseDelayAmount"));

            // Vanilla always starts the pause table with frame zero.
            var pauses = new List<short> { 0 };
            short nativeFrameNumber = 2;
            foreach (AivJsonFrame frame in document.frames)
            {
                if (frame == null)
                    throw new InvalidDataException("The AIVJSON contains a null frame.");

                if (frame.shouldPause && pauses.Count < MaxPauseEntries)
                    pauses.Add(nativeFrameNumber);

                nativeFrameNumber = checked((short)(nativeFrameNumber + 1));
            }

            raw.Add(ToShort(pauses.Count, "pause count"));
            raw.AddRange(pauses);
            raw.Add(ToShort(document.frames.Count, "frame count"));

            int keepPlacementCount = 0;
            for (int frameIndex = 0; frameIndex < document.frames.Count; frameIndex++)
            {
                AivJsonFrame frame = document.frames[frameIndex];
                ValidateItemType(frame.itemType, $"frames[{frameIndex}].itemType");
                if (frame.tilePositionOfsets == null)
                {
                    throw new InvalidDataException(
                        $"frames[{frameIndex}].tilePositionOfsets is missing.");
                }
                if (frame.tilePositionOfsets.Count == 0)
                {
                    throw new InvalidDataException(
                        $"frames[{frameIndex}].tilePositionOfsets must contain at least one position.");
                }
                if (frame.tilePositionOfsets.Count > short.MaxValue)
                {
                    throw new InvalidDataException(
                        $"frames[{frameIndex}].tilePositionOfsets count is outside the native positive Int16 range: {frame.tilePositionOfsets.Count}.");
                }

                bool isKeep = frame.itemType >= (int)eMappers.MAPPER_KEEP1 &&
                    frame.itemType <= (int)eMappers.MAPPER_KEEP5;
                if (isKeep)
                {
                    // Keep cardinality is defined by placements, not merely Keep frames.
                    keepPlacementCount += frame.tilePositionOfsets.Count;
                    if (frame.tilePositionOfsets.Count != 1)
                    {
                        throw new InvalidDataException(
                            $"frames[{frameIndex}].tilePositionOfsets must contain exactly one Keep position; found {frame.tilePositionOfsets.Count}.");
                    }
                }
                if (frame.tilePositionOfsets.Count == 1)
                {
                    raw.Add(ToShort(frame.itemType, $"frames[{frameIndex}].itemType"));
                    raw.Add(ToPosition(
                        frame.tilePositionOfsets[0],
                        $"frames[{frameIndex}].tilePositionOfsets[0]"));
                    continue;
                }

                raw.Add(ToShort(-frame.itemType, $"frames[{frameIndex}].itemType"));
                raw.Add(ToShort(
                    frame.tilePositionOfsets.Count,
                    $"frames[{frameIndex}] position count"));
                for (int positionIndex = 0;
                     positionIndex < frame.tilePositionOfsets.Count;
                     positionIndex++)
                {
                    raw.Add(ToPosition(
                        frame.tilePositionOfsets[positionIndex],
                        $"frames[{frameIndex}].tilePositionOfsets[{positionIndex}]"));
                }
            }

            if (keepPlacementCount == 0)
                throw new InvalidDataException("The AIVJSON contains no keep frame.");
            if (keepPlacementCount != 1)
            {
                throw new InvalidDataException(
                    $"The AIVJSON must contain exactly one Keep position; found {keepPlacementCount}.");
            }

            raw.Add(ToShort(document.miscItems.Count, "miscItems count"));
            for (int index = 0; index < document.miscItems.Count; index++)
            {
                AivJsonMiscItem item = document.miscItems[index];
                if (item == null)
                    throw new InvalidDataException($"miscItems[{index}] is null.");

                int nativeItemType = item.itemType > 9000
                    ? item.itemType - 9000
                    : item.itemType;
                raw.Add(ToShort(nativeItemType, $"miscItems[{index}].itemType"));
                raw.Add(ToPosition(
                    item.positionOfset,
                    $"miscItems[{index}].positionOfset"));
                raw.Add(ToShort(item.number, $"miscItems[{index}].number"));
            }

            return raw.ToArray();
        }

        private static void ValidateItemType(int itemType, string field)
        {
            if (itemType <= 0 || itemType > short.MaxValue)
            {
                throw new InvalidDataException(
                    $"{field} is outside the native positive Int16 range: {itemType}.");
            }
        }

        private static short ToPosition(int value, string field)
        {
            if (value < 0 || value >= GridTileCount)
            {
                throw new InvalidDataException(
                    $"{field} is outside the 100x100 AIV grid: {value}.");
            }

            return (short)value;
        }

        private static short ToShort(int value, string field)
        {
            if (value < short.MinValue || value > short.MaxValue)
            {
                throw new InvalidDataException(
                    $"{field} is outside the native Int16 range: {value}.");
            }

            return (short)value;
        }
    }
}
