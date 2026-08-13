using System;

namespace ImprovedHunters
{
    internal static class HunterQueryActorPolicy
    {
        internal const ulong NativeUnitSlotSize = 0x490;

        public static bool TryReconstructHunterUnitId(
            ulong hunterSlotBase,
            ulong unitManagerBase,
            out int hunterUnitId)
        {
            hunterUnitId = 0;
            if (hunterSlotBase <= unitManagerBase)
                return false;

            ulong delta = hunterSlotBase - unitManagerBase;
            if (delta % NativeUnitSlotSize != 0)
                return false;

            ulong unitId = delta / NativeUnitSlotSize;
            if (unitId == 0 || unitId > int.MaxValue)
                return false;

            hunterUnitId = checked((int)unitId);
            return true;
        }

        public static bool IsMatchingCapture(
            int queryUnitId,
            int capturedQueryUnitId,
            int capturedHunterUnitId)
        {
            return queryUnitId > 0 &&
                queryUnitId == capturedQueryUnitId &&
                capturedHunterUnitId > 0;
        }
    }
}
