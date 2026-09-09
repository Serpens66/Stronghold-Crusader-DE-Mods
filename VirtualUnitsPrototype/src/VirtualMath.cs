using System;

namespace VirtualUnitsPrototype
{
    internal static class VirtualMath
    {
        public static int ScalePositive(int value, int numerator, int denominator, int maximum)
        {
            if (value < 0 || numerator <= 0 || denominator <= 0 || maximum <= 0)
                throw new ArgumentOutOfRangeException();
            long scaled = ((long)value * numerator + denominator / 2L) / denominator;
            return (int)Math.Min(maximum, Math.Max(0L, scaled));
        }

        public static int ScaleHealth(int current, int oldMaximum, int newMaximum)
        {
            if (current <= 0 || oldMaximum <= 0 || newMaximum <= 0)
                return 0;
            long scaled = ((long)current * newMaximum + oldMaximum / 2L) / oldMaximum;
            return (int)Math.Min(newMaximum, Math.Max(1L, scaled));
        }

        public static int ScaleMovementSpeed(int encodedSpeed, int movementNumerator, int movementDenominator, int maximum)
        {
            // SHCDE encodes faster movement with a smaller delay value.
            return ScalePositive(encodedSpeed, movementDenominator, movementNumerator, maximum);
        }

        public static int SignedLow32(long value) => unchecked((int)(uint)value);
        public static string HexLow32(long value) => $"0x{unchecked((uint)value):X8}";
    }
}
