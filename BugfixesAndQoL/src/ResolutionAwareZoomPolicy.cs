// Feature: Pure rules for resolution-normalized and extended camera zoom.
using System;

namespace BugfixesAndQoL
{
    internal static class ResolutionAwareZoomPolicy
    {
        internal const float ReferenceScreenHeight = 1080f;
        internal const float VanillaLockedMaximumPosition = 3f;
        internal const float ExtendedLockedMaximumPosition = 4f;

        internal static float GetResolutionScale(int screenHeight)
        {
            if (screenHeight <= 0)
                return 1f;

            return Math.Max(1f, screenHeight / ReferenceScreenHeight);
        }

        internal static float GetEffectiveZoom(float vanillaZoom, int screenHeight) =>
            vanillaZoom * GetResolutionScale(screenHeight);

        internal static float ResolvePosition(
            float currentPosition,
            float adjustment,
            bool mapLocked,
            bool canUserExtraZoom,
            bool mapEditorMode,
            bool loop)
        {
            float position = currentPosition + adjustment;
            if (!mapLocked)
                return Math.Max(0f, Math.Min(5f, position));

            float minimum = canUserExtraZoom ? 1f : 2f;
            if (mapEditorMode)
                minimum -= 1f;

            if (position > ExtendedLockedMaximumPosition)
                return loop ? minimum : ExtendedLockedMaximumPosition;
            if (position < minimum)
                return loop ? ExtendedLockedMaximumPosition : minimum;
            return position;
        }
    }
}
