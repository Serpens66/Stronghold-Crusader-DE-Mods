// Feature: Resolve a Keep center without reading Vanilla's intentionally truncated occupied-tile array.
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal enum QuarryKeepCenterResolution
    {
        Invalid = 0,
        NotReady = 1,
        Ready = 2
    }

    internal static class QuarryKeepCenterPolicy
    {
        private const int MaximumNativeGridSize = 13;
        private const int MaximumMapCoordinate = 799;

        public static QuarryKeepCenterResolution Resolve(
            eStructs buildingType,
            uint gridSize,
            int beginX,
            int beginY,
            out int centerXTimesTwo,
            out int centerYTimesTwo)
        {
            centerXTimesTwo = 0;
            centerYTimesTwo = 0;

            if (!IsKeepType(buildingType))
                return QuarryKeepCenterResolution.Invalid;
            if (gridSize == 0)
                return QuarryKeepCenterResolution.NotReady;
            if (gridSize > MaximumNativeGridSize ||
                beginX < 0 || beginY < 0 ||
                beginX > MaximumMapCoordinate || beginY > MaximumMapCoordinate)
            {
                return QuarryKeepCenterResolution.Invalid;
            }

            long endX = beginX + (long)gridSize - 1;
            long endY = beginY + (long)gridSize - 1;
            if (endX > MaximumMapCoordinate || endY > MaximumMapCoordinate)
                return QuarryKeepCenterResolution.Invalid;

            centerXTimesTwo = checked(2 * beginX + (int)gridSize - 1);
            centerYTimesTwo = checked(2 * beginY + (int)gridSize - 1);
            return QuarryKeepCenterResolution.Ready;
        }

        private static bool IsKeepType(eStructs buildingType)
        {
            return buildingType == eStructs.STRUCT_KEEP_ONE ||
                   buildingType == eStructs.STRUCT_KEEP_TWO ||
                   buildingType == eStructs.STRUCT_KEEP_THREE ||
                   buildingType == eStructs.STRUCT_KEEP_FOUR ||
                   buildingType == eStructs.STRUCT_KEEP_FIVE;
        }
    }
}
