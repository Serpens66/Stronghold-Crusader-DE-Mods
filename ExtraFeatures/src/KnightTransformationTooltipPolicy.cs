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
        // Same bounds as the KnightTransformation* settings setters in ExtraFeaturesViewModel.
        internal const int MaximumGoldCost = 1000;
        internal const int MaximumDelaySeconds = 120;

        internal static int ClampGoldCost(int goldCost) => Math.Max(0, Math.Min(MaximumGoldCost, goldCost));
        internal static int ClampDelaySeconds(int delaySeconds) => Math.Max(0, Math.Min(MaximumDelaySeconds, delaySeconds));

        public static KnightTransformationTooltipMetadata Create(int goldCost, int delaySeconds)
        {
            return new KnightTransformationTooltipMetadata(
                ClampGoldCost(goldCost),
                ClampDelaySeconds(delaySeconds));
        }
    }
}
