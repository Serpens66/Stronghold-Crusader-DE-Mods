using System;

namespace KeepFlagRotationTest
{
    internal readonly struct FlagPosition
    {
        public FlagPosition(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

        public override string ToString() => $"({X},{Y})";
    }

    internal static class KeepFlagRotationPolicy
    {
        internal static bool TryGetPositions(
            int originX,
            int originY,
            int scale,
            int orientation,
            out FlagPosition vanillaPosition,
            out FlagPosition correctedPosition)
        {
            vanillaPosition = default;
            correctedPosition = default;
            if (originX < 0 || originY < 0 || scale < 1)
                return false;

            int microX;
            int microY;
            switch (orientation)
            {
                case 0:
                    microX = 7;
                    microY = 0;
                    break;
                case 2:
                    microX = 7;
                    microY = 7;
                    break;
                case 4:
                    microX = 0;
                    microY = 7;
                    break;
                case 6:
                    microX = 0;
                    microY = 0;
                    break;
                default:
                    return false;
            }

            int farX = checked(originX + scale - 1);
            int farY = checked(originY + scale - 1);
            vanillaPosition = new FlagPosition(
                checked(farX * 8 + microX),
                checked(originY * 8 + microY));
            correctedPosition = new FlagPosition(
                checked((microX == 7 ? farX : originX) * 8 + microX),
                checked((microY == 7 ? farY : originY) * 8 + microY));
            return true;
        }

        internal static int ExpectedScale(int mapperOrStructKind)
        {
            // Callers normalize Keep1/Keep2 to 1/2 and Keep3 to 3.
            return mapperOrStructKind == 3 ? 11 :
                mapperOrStructKind == 1 || mapperOrStructKind == 2 ? 7 : 0;
        }
    }
}
