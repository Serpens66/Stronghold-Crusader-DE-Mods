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
        public bool SpawnFortifications { get; set; } = true;
        public bool SpawnBuildings { get; set; }
        public bool SpawnStockpile { get; set; } = true;
        public bool SpawnDefensiveGroundFeatures { get; set; }
        public bool SpawnFearFactorBuildings { get; set; }
        public bool SpawnSiegeEngines { get; set; }
        public bool SpawnBraziersAndFlags { get; set; }
    }

    internal static class AivSpawnPlan
    {

        public static AivJsonDocument Decode(short[] raw)
        {
            AIVParser.Core.AivJsonDocument source = AIVParser.Core.AivRawDataDecoder.Decode(raw);
            return new AivJsonDocument
            {
                pauseDelayAmount = source.pauseDelayAmount,
                frames = source.frames.Select(frame => new AivJsonFrame
                {
                    itemType = frame.itemType,
                    tilePositionOfsets = new List<int>(frame.tilePositionOfsets),
                    shouldPause = frame.shouldPause
                }).ToList(),
                miscItems = source.miscItems.Select(item => new AivJsonMiscItem
                {
                    itemType = item.itemType,
                    positionOfset = item.positionOfset,
                    number = item.number
                }).ToList()
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
                        frame.itemType == 0 ||
                        ((frame.itemType != (int)eMappers.MAPPER_STORES || options.SpawnStockpile) &&
                        (AivMapperCatalog.IsKeep(frame.itemType) ||
                         IsFrameEnabled(ClassifyFrame(frame.itemType), options))))
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
            if (mapper.Category == AivItemCategory.Trap ||
                mapper.Category == AivItemCategory.PitchDitchPath ||
                mapper.Category == AivItemCategory.MoatPath)
            {
                return AivFrameSpawnCategory.DefensiveGroundFeature;
            }
            // Structural categories take precedence: Dog Cages have a negative
            // Fear visual group, but are gameplay traps like Killing Pits.
            if (mapper.VisualGroup == AivVisualGroup.PositiveFear ||
                mapper.VisualGroup == AivVisualGroup.NegativeFear)
            {
                return AivFrameSpawnCategory.FearFactor;
            }
            if (mapper.Category == AivItemCategory.Keep ||
                mapper.Category == AivItemCategory.HighWallPath ||
                mapper.Category == AivItemCategory.LowWallPath ||
                mapper.Category == AivItemCategory.CrenelPath ||
                mapper.Category == AivItemCategory.Stair ||
                itemType == (int)eMappers.MAPPER_DRAWBRIDGE ||
                (itemType >= (int)eMappers.MAPPER_TOWER1 &&
                    itemType <= (int)eMappers.MAPPER_TOWER5) ||
                (itemType >= (int)eMappers.MAPPER_GATE_STONE1A &&
                    itemType <= (int)eMappers.MAPPER_GATE_STONE2B))
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
            ushort flagProjectileType,
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
                    projectileType = (ProjectileType)flagProjectileType;
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
                case AivFrameSpawnCategory.Fortification: return options.SpawnFortifications;
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

    }
}
