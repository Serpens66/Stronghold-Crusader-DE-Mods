using AIVParser.Core;
using SHCDESE.Interop;
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
        public bool SpawnDefensiveGroundFeatures { get; set; }
        public bool SpawnFearFactorBuildings { get; set; }
        public bool SpawnSiegeEngines { get; set; }
        public bool SpawnTroops { get; set; }
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
                    .Where(frame => IsFrameEnabled(ClassifyFrame(frame.itemType), options))
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
            return TryMapUnit(engineType, out _) ? AivMiscSpawnCategory.Troop : AivMiscSpawnCategory.Unknown;
        }

        public static bool TryMapUnit(int itemType, out eChimps chimp)
        {
            switch (NormalizeMiscType(itemType))
            {
                case 1: chimp = eChimps.CHIMP_TYPE_ENGINEER; return true;
                case 2: chimp = eChimps.CHIMP_TYPE_MANGONEL; return true;
                case 3: chimp = eChimps.CHIMP_TYPE_BALLISTA; return true;
                case 4: chimp = eChimps.CHIMP_TYPE_TREBUCHET; return true;
                case 5: chimp = eChimps.CHIMP_TYPE_ARAB_BALLISTA; return true;
                case 6: chimp = eChimps.CHIMP_TYPE_ARCHER; return true;
                case 7: chimp = eChimps.CHIMP_TYPE_XBOWMAN; return true;
                case 8: chimp = eChimps.CHIMP_TYPE_SPEARMAN; return true;
                case 9: chimp = eChimps.CHIMP_TYPE_PIKEMAN; return true;
                case 10: chimp = eChimps.CHIMP_TYPE_MACEMAN; return true;
                case 11: chimp = eChimps.CHIMP_TYPE_SWORDSMAN; return true;
                case 12: chimp = eChimps.CHIMP_TYPE_KNIGHT; return true;
                case 13: chimp = eChimps.CHIMP_TYPE_ARAB_SLAVE; return true;
                case 14: chimp = eChimps.CHIMP_TYPE_ARAB_SLINGER; return true;
                case 15: chimp = eChimps.CHIMP_TYPE_ARAB_ASSASIN; return true;
                case 16: chimp = eChimps.CHIMP_TYPE_ARAB_BOW; return true;
                case 17: chimp = eChimps.CHIMP_TYPE_ARAB_HORSEMAN; return true;
                case 18: chimp = eChimps.CHIMP_TYPE_ARAB_SWORDSMAN; return true;
                case 19: chimp = eChimps.CHIMP_TYPE_ARAB_GRENADIER; return true;
                case 23: chimp = eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER; return true;
                case 24: chimp = eChimps.CHIMP_TYPE_BEDOUIN_HEALER; return true;
                case 25: chimp = eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH; return true;
                case 26: chimp = eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER; return true;
                case 27: chimp = eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER; return true;
                case 28: chimp = eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL; return true;
                case 29: chimp = eChimps.CHIMP_TYPE_BEDOUIN_SAPPER; return true;
                case 30: chimp = eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER; return true;
                default: chimp = eChimps.CHIMP_TYPE_NULL; return false;
            }
        }

        public static int GetRequiredEngineerCount(int siegeItemType)
        {
            switch (NormalizeMiscType(siegeItemType))
            {
                case 2: return 2; // Mangonel
                case 3: return 2; // Ballista
                case 4: return 3; // Trebuchet
                case 5: return 2; // Fire Ballista
                default: return 0;
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
                case AivMiscSpawnCategory.Troop: return options.SpawnTroops;
                case AivMiscSpawnCategory.SiegeEngine: return options.SpawnSiegeEngines;
                case AivMiscSpawnCategory.Decoration: return options.SpawnBraziersAndFlags;
                case AivMiscSpawnCategory.Unknown:
                    return options.SpawnTroops || options.SpawnSiegeEngines || options.SpawnBraziersAndFlags;
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
