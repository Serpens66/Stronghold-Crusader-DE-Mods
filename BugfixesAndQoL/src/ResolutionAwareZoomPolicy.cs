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

        internal static bool CanUserExtraZoom(
            int screenWidth,
            int screenHeight,
            int tilemapSize)
        {
            float resolutionScale = GetResolutionScale(screenHeight);
            float normalizedWidth = screenWidth / resolutionScale;
            float normalizedHeight = screenHeight / resolutionScale;

            if (normalizedWidth > 2560f || normalizedHeight > 1440f)
                return false;

            switch (tilemapSize)
            {
                case 400:
                case 500:
                case 600:
                case 700:
                case 800:
                    if (normalizedWidth > 2560f || normalizedHeight > 1440f)
                        return false;
                    break;
                case 300:
                    if (normalizedWidth > 2300f || normalizedHeight > 1440f)
                        return false;
                    break;
                case 200:
                    if (normalizedWidth > 1500f || normalizedHeight > 900f)
                        return false;
                    break;
                case 160:
                    if (normalizedWidth > 1300f || normalizedHeight > 800f)
                        return false;
                    break;
            }

            return true;
        }

        internal static float GetLockedMinimumPosition(
            bool canUserExtraZoom,
            bool mapEditorMode,
            bool allowExtendedFarZoom)
        {
            float minimum = canUserExtraZoom ? 1f : 2f;
            if (allowExtendedFarZoom && canUserExtraZoom)
                minimum = 0f;
            if (mapEditorMode)
                minimum -= 1f;

            return Math.Max(0f, minimum);
        }

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

            float minimum = GetLockedMinimumPosition(
                canUserExtraZoom,
                mapEditorMode,
                allowExtendedFarZoom: true);

            if (position > ExtendedLockedMaximumPosition)
                return loop ? minimum : ExtendedLockedMaximumPosition;
            if (position < minimum)
                return loop ? ExtendedLockedMaximumPosition : minimum;
            return position;
        }
    }
}
