using AIVParser.Core;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CastlePlanner
{
    internal enum AivFrameSpawnCategory
    {
        Fortification,
        Building,
        DefensiveGroundFeature,
        FearFactor
    }

    internal enum AivMiscSpawnCategory
    {
        Unknown,
        Troop,
        SiegeEngine,
        Decoration
    }

    internal sealed class AivSpawnOptions
    {
        public bool SpawnBuildings { get; set; }
        public bool SpawnStockpile { get; set; } = true;
        public bool SpawnDefensiveGroundFeatures { get; set; }
        public bool SpawnFearFactorBuildings { get; set; }
        public bool SpawnSiegeEngines { get; set; }
        public bool SpawnBraziersAndFlags { get; set; }
    }

    internal static class AivSpawnPlan
    {
        private const int GridTileCount = 10000;
        private const int MaxPauseEntries = 50;

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
                if (encodedType == 0 || encodedType == short.MinValue)
                    throw new InvalidDataException($"Invalid native item type at frame {frameIndex}: {encodedType}.");

                int itemType = Math.Abs(encodedType);
                int positionCount = encodedType > 0
                    ? 1
                    : Read(raw, ref cursor, $"frame[{frameIndex}].positionCount");
                if (positionCount < 1)
                    throw new InvalidDataException($"Invalid position count at frame {frameIndex}: {positionCount}.");

                var positions = new List<int>(positionCount);
                for (int positionIndex = 0; positionIndex < positionCount; positionIndex++)
                {
                    int position = Read(raw, ref cursor, $"frame[{frameIndex}].position[{positionIndex}]");
                    ValidatePosition(position, $"frame[{frameIndex}].position[{positionIndex}]");
                    positions.Add(position);
                }

                if (AivMapperCatalog.IsKeep(itemType))
                    keepCount++;
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

        public static AivJsonDocument Filter(AivJsonDocument source, AivSpawnOptions options)
        {
            if (source?.frames == null || source.miscItems == null)
                throw new InvalidDataException("The decoded AIV document is incomplete.");
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return new AivJsonDocument
            {
                pauseDelayAmount = source.pauseDelayAmount,
                frames = source.frames
                    .Where(frame =>
                        (frame.itemType != (int)eMappers.MAPPER_STORES || options.SpawnStockpile) &&
                        IsFrameEnabled(ClassifyFrame(frame.itemType), options))
                    .Select(CloneFrame)
                    .ToList(),
                miscItems = source.miscItems
                    .Where(item => IsMiscEnabled(ClassifyMisc(item.itemType), options))
                    .Select(CloneMisc)
                    .ToList()
            };
        }

        public static AivFrameSpawnCategory ClassifyFrame(int itemType)
        {
            AivMapperInfo mapper = AivMapperCatalog.Resolve(itemType);
            if (mapper.VisualGroup == AivVisualGroup.PositiveFear ||
                mapper.VisualGroup == AivVisualGroup.NegativeFear)
            {
                return AivFrameSpawnCategory.FearFactor;
            }
            if (mapper.Category == AivItemCategory.Trap ||
                mapper.Category == AivItemCategory.PitchDitchPath ||
                mapper.Category == AivItemCategory.MoatPath)
            {
                return AivFrameSpawnCategory.DefensiveGroundFeature;
            }
            if (mapper.Category == AivItemCategory.Keep ||
                mapper.Category == AivItemCategory.HighWallPath ||
                mapper.Category == AivItemCategory.LowWallPath ||
                mapper.Category == AivItemCategory.CrenelPath ||
                mapper.Category == AivItemCategory.Stair ||
                itemType == 105 ||
                (itemType >= 110 && itemType <= 114) ||
                (itemType >= 144 && itemType <= 147))
            {
                return AivFrameSpawnCategory.Fortification;
            }
            return AivFrameSpawnCategory.Building;
        }

        public static AivMiscSpawnCategory ClassifyMisc(int itemType)
        {
            int engineType = NormalizeMiscType(itemType);
            if (engineType >= 2 && engineType <= 5)
                return AivMiscSpawnCategory.SiegeEngine;
            if (engineType == 20 || engineType == 21)
                return AivMiscSpawnCategory.Decoration;
            if (engineType == 1 ||
                (engineType >= 6 && engineType <= 19) ||
                (engineType >= 23 && engineType <= 30))
            {
                return AivMiscSpawnCategory.Troop;
            }
            return AivMiscSpawnCategory.Unknown;
        }

        public static bool TryMapSiegeEngine(int itemType, out eChimps chimp)
        {
            switch (NormalizeMiscType(itemType))
            {
                case 2: chimp = eChimps.CHIMP_TYPE_MANGONEL; return true;
                case 3: chimp = eChimps.CHIMP_TYPE_BALLISTA; return true;
                case 4: chimp = eChimps.CHIMP_TYPE_TREBUCHET; return true;
                case 5: chimp = eChimps.CHIMP_TYPE_ARAB_BALLISTA; return true;
                default: chimp = eChimps.CHIMP_TYPE_NULL; return false;
            }
        }

        public static bool TryMapDecoration(
            int itemType,
            int playerId,
            out eMappers mapper,
            out ProjectileType projectileType)
        {
            switch (NormalizeMiscType(itemType))
            {
                case 20:
                    mapper = eMappers.MAPPER_BRAZIER;
                    projectileType = ProjectileType.Brazier;
                    return true;
                case 21 when playerId >= 0 && playerId <= 8:
                    mapper = (eMappers)((int)eMappers.MAPPER_FLAG_TYPE0 + playerId);
                    projectileType = ProjectileType.CrusaderFlag;
                    return true;
                default:
                    mapper = default;
                    projectileType = ProjectileType.Unknown;
                    return false;
            }
        }

        public static int NormalizeMiscType(int itemType) => itemType > 9000 ? itemType - 9000 : itemType;

        private static bool IsFrameEnabled(AivFrameSpawnCategory category, AivSpawnOptions options)
        {
            switch (category)
            {
                case AivFrameSpawnCategory.Fortification: return true;
                case AivFrameSpawnCategory.Building: return options.SpawnBuildings;
                case AivFrameSpawnCategory.DefensiveGroundFeature: return options.SpawnDefensiveGroundFeatures;
                case AivFrameSpawnCategory.FearFactor: return options.SpawnFearFactorBuildings;
                default: return false;
            }
        }

        private static bool IsMiscEnabled(AivMiscSpawnCategory category, AivSpawnOptions options)
        {
            switch (category)
            {
                case AivMiscSpawnCategory.Troop: return false;
                case AivMiscSpawnCategory.SiegeEngine: return options.SpawnSiegeEngines;
                case AivMiscSpawnCategory.Decoration: return options.SpawnBraziersAndFlags;
                case AivMiscSpawnCategory.Unknown:
                    return options.SpawnSiegeEngines || options.SpawnBraziersAndFlags;
                default: return false;
            }
        }

        private static AivJsonFrame CloneFrame(AivJsonFrame frame) => new AivJsonFrame
        {
            itemType = frame.itemType,
            shouldPause = frame.shouldPause,
            tilePositionOfsets = frame.tilePositionOfsets == null
                ? null
                : new List<int>(frame.tilePositionOfsets)
        };

        private static AivJsonMiscItem CloneMisc(AivJsonMiscItem item) => new AivJsonMiscItem
        {
            itemType = item.itemType,
            positionOfset = item.positionOfset,
            number = item.number
        };

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
