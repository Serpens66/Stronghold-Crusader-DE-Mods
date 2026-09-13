// Feature: Derive the compact mount/dismount tooltip metadata from synchronized settings.
using System;

namespace ExtraFeatures
{
    internal readonly struct KnightTransformationTooltipMetadata
    {
        public KnightTransformationTooltipMetadata(int goldCost, int delaySeconds)
        {
            GoldCost = goldCost;
            DelaySeconds = delaySeconds;
        }

        public int GoldCost { get; }
        public int DelaySeconds { get; }
        public bool ShowGold => GoldCost > 0;
        public bool ShowDelay => DelaySeconds > 0;
        public bool ShowSeparator => ShowGold && ShowDelay;
        public bool ShowHost => ShowGold || ShowDelay;
    }

    internal static class KnightTransformationTooltipPolicy
    {
        public static KnightTransformationTooltipMetadata Create(int goldCost, int delaySeconds)
        {
            return new KnightTransformationTooltipMetadata(
                Math.Max(0, Math.Min(1000, goldCost)),
                Math.Max(0, Math.Min(120, delaySeconds)));
        }
    }
}
