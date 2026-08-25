// Feature: Pure policy for improved AI obstruction cleanup.
using System;

namespace BugfixesAndQoL
{
    internal enum BetterAIOverbuildProtectionReason
    {
        None,
        AlwaysBroad,
        KeepRadius,
    }

    internal static class BetterAIOverbuildPolicy
    {
        internal const int KeepManhattanRadius = 20;

        internal const int MapperStores = 52;
        internal const int MapperHovel = 54;
        internal const int MapperTradepost = 77;
        internal const int MapperBedouinStockade = 79;
        internal const int MapperGranary = 80;
        internal const int MapperArmoury = 81;
        internal const int MapperBarracksWood = 86;
        internal const int MapperBarracksStone = 87;

        internal const int StructureHovel = 1;
        internal const int StructureBedouinStockade = 2;
        internal const int StructureBarracksWood = 8;
        internal const int StructureBarracksStone = 9;
        internal const int StructureGoodsYard = 10;
        internal const int StructureArmoury = 11;
        internal const int StructureGranary = 19;
        internal const int StructureTradepost = 26;

        internal static bool IsAddedAlwaysBroadMapper(int mapper) =>
            mapper == MapperStores ||
            mapper == MapperTradepost ||
            mapper == MapperGranary ||
            mapper == MapperArmoury;

        internal static bool IsAlwaysBroadMapper(int mapper) =>
            IsAddedAlwaysBroadMapper(mapper) ||
            mapper == MapperHovel ||
            mapper == MapperBedouinStockade ||
            mapper == MapperBarracksWood ||
            mapper == MapperBarracksStone;

        internal static bool IsAlwaysBroadStructure(int structureType) =>
            structureType == StructureHovel ||
            structureType == StructureBedouinStockade ||
            structureType == StructureBarracksWood ||
            structureType == StructureBarracksStone ||
            structureType == StructureGoodsYard ||
            structureType == StructureArmoury ||
            structureType == StructureGranary ||
            structureType == StructureTradepost;

        internal static BetterAIOverbuildProtectionReason ClassifyForeignBlocker(
            int placingPlayerId,
            int blockerOwnerId,
            bool blockerOwnerIsAi,
            int blockerStructureType,
            bool blockerHasKeep,
            int blockerAnchorX,
            int blockerAnchorY,
            int keepX,
            int keepY,
            out long manhattanDistance)
        {
            manhattanDistance = -1;
            if (placingPlayerId < 1 || placingPlayerId > 8 ||
                blockerOwnerId < 1 || blockerOwnerId > 8 ||
                blockerOwnerId == placingPlayerId ||
                !blockerOwnerIsAi)
            {
                return BetterAIOverbuildProtectionReason.None;
            }

            if (IsAlwaysBroadStructure(blockerStructureType))
                return BetterAIOverbuildProtectionReason.AlwaysBroad;

            if (!blockerHasKeep)
                return BetterAIOverbuildProtectionReason.None;

            manhattanDistance = ManhattanDistance(blockerAnchorX, blockerAnchorY, keepX, keepY);
            return manhattanDistance <= KeepManhattanRadius
                ? BetterAIOverbuildProtectionReason.KeepRadius
                : BetterAIOverbuildProtectionReason.None;
        }

        internal static long ManhattanDistance(int x1, int y1, int x2, int y2)
        {
            long dx = (long)x1 - x2;
            long dy = (long)y1 - y2;
            return Math.Abs(dx) + Math.Abs(dy);
        }
    }
}
