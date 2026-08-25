// Feature: Pure policy for improved AI obstruction cleanup.
using System;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    internal enum BetterAIOverbuildProtectionReason
    {
        None,
        AlwaysBroad,
        ReservedArea,
        KeepRadius,
        RepeatedConflict,
    }

    internal static class BetterAIOverbuildPolicy
    {
        internal const int KeepManhattanRadius = 20;

        internal static bool IsAddedAlwaysBroadMapper(eMappers mapper) =>
            mapper == eMappers.MAPPER_STORES ||
            mapper == eMappers.MAPPER_TRADEPOST ||
            mapper == eMappers.MAPPER_GRANARY ||
            mapper == eMappers.MAPPER_ARMOURY;

        internal static bool IsAlwaysBroadMapper(eMappers mapper) =>
            IsAddedAlwaysBroadMapper(mapper) ||
            mapper == eMappers.MAPPER_HOVEL ||
            mapper == eMappers.MAPPER_BEDOUIN_STOCKADE ||
            mapper == eMappers.MAPPER_BARRACKS_WOOD ||
            mapper == eMappers.MAPPER_BARRACKS_STONE;

        internal static bool IsAlwaysBroadStructure(eStructs structureType) =>
            structureType == eStructs.STRUCT_HOVEL ||
            structureType == eStructs.STRUCT_BEDOUIN_STOCKADE ||
            structureType == eStructs.STRUCT_BARRACKS_WOOD ||
            structureType == eStructs.STRUCT_BARRACKS_STONE ||
            structureType == eStructs.STRUCT_GOODS_YARD ||
            structureType == eStructs.STRUCT_ARMOURY ||
            structureType == eStructs.STRUCT_GRANARY ||
            structureType == eStructs.STRUCT_TRADEPOST;

        internal static bool IsReservedAreaStructure(eStructs structureType) =>
            structureType == eStructs.STRUCT_PARADEGROUND_OIL ||
            structureType == eStructs.STRUCT_PARADEGROUND_ENG ||
            structureType == eStructs.STRUCT_PARADEGROUND_MISS ||
            structureType == eStructs.STRUCT_PARADEGROUND_LGT ||
            structureType == eStructs.STRUCT_PARADEGROUND_HVY ||
            structureType == eStructs.STRUCT_PARADEGROUND_TUN;

        internal static bool IsAlwaysProtectedReservedArea(eStructs structureType) =>
            structureType == eStructs.STRUCT_PARADEGROUND_MISS ||
            structureType == eStructs.STRUCT_PARADEGROUND_LGT ||
            structureType == eStructs.STRUCT_PARADEGROUND_HVY;

        internal static bool IsReservationParentCandidate(
            eStructs reservedAreaType,
            eStructs candidateParentType)
        {
            switch (reservedAreaType)
            {
                case eStructs.STRUCT_PARADEGROUND_OIL:
                    return candidateParentType == eStructs.STRUCT_OIL_SMELTER;
                case eStructs.STRUCT_PARADEGROUND_ENG:
                    return candidateParentType == eStructs.STRUCT_ENGINEERS_GUILD;
                case eStructs.STRUCT_PARADEGROUND_TUN:
                    return candidateParentType == eStructs.STRUCT_TUNNELLERS_GUILD;
                case eStructs.STRUCT_PARADEGROUND_MISS:
                case eStructs.STRUCT_PARADEGROUND_LGT:
                case eStructs.STRUCT_PARADEGROUND_HVY:
                    return candidateParentType == eStructs.STRUCT_BARRACKS_WOOD ||
                        candidateParentType == eStructs.STRUCT_BARRACKS_STONE ||
                        candidateParentType == eStructs.STRUCT_BEDOUIN_STOCKADE;
                default:
                    return false;
            }
        }

        internal static int ReservationParentMaximumChebyshevDistance(eStructs reservedAreaType) =>
            reservedAreaType == eStructs.STRUCT_PARADEGROUND_OIL
                ? 4
                : IsReservedAreaStructure(reservedAreaType) ? 5 : -1;

        internal static bool IsWithinReservationParentRange(
            eStructs reservedAreaType,
            int reservedX,
            int reservedY,
            int parentX,
            int parentY)
        {
            int maximum = ReservationParentMaximumChebyshevDistance(reservedAreaType);
            if (maximum < 0)
                return false;

            long dx = Math.Abs((long)reservedX - parentX);
            long dy = Math.Abs((long)reservedY - parentY);
            // Compound components begin exactly one core width away from the
            // visible parent; requiring that boundary avoids borrowing a nearby
            // unrelated building of the same owner as a false parent.
            return Math.Max(dx, dy) == maximum;
        }

        internal static BetterAIOverbuildProtectionReason ClassifyForeignBlocker(
            int placingPlayerId,
            int blockerOwnerId,
            bool blockerOwnerIsAi,
            eStructs blockerStructureType,
            bool hasProtectedReservationParent,
            bool blockerHasKeep,
            int blockerAnchorX,
            int blockerAnchorY,
            int keepX,
            int keepY)
        {
            if (placingPlayerId < 1 || placingPlayerId > 8 ||
                blockerOwnerId < 1 || blockerOwnerId > 8 ||
                blockerOwnerId == placingPlayerId ||
                !blockerOwnerIsAi)
            {
                return BetterAIOverbuildProtectionReason.None;
            }

            if (IsReservedAreaStructure(blockerStructureType) &&
                (IsAlwaysProtectedReservedArea(blockerStructureType) ||
                 hasProtectedReservationParent))
            {
                return BetterAIOverbuildProtectionReason.ReservedArea;
            }

            if (IsAlwaysBroadStructure(blockerStructureType))
                return BetterAIOverbuildProtectionReason.AlwaysBroad;

            if (!blockerHasKeep)
                return BetterAIOverbuildProtectionReason.None;

            return ManhattanDistance(blockerAnchorX, blockerAnchorY, keepX, keepY) <=
                KeepManhattanRadius
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
