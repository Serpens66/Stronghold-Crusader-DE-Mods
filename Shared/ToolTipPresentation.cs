using System;
using UnityEngine;

namespace Shared
{
    public static class ToolTipPresentation
    {
        private const float ReferenceHeight = 1440.0f;
        private const float BaseFontSize = 30.0f;

        // Preserve the physical tooltip size when the game silently switches to a
        // higher render resolution. Lower resolutions retain the readable baseline.
        private static float ResolutionScale =>
            Math.Max(1.0f, Screen.height / ReferenceHeight);

        // Noesis FontSize and MaxWidth are floats. Returning the exact CLR type is
        // required because x:Static values are not converted like XAML literals.
        public static float FontSize =>
            BaseFontSize * ResolutionScale;

        // Scale width and edge margin together with the font so line lengths remain
        // comparable between 1440p and higher render resolutions.
        public static float MaximumWidth =>
            Math.Max(
                320.0f * ResolutionScale,
                Math.Min(1000.0f * ResolutionScale, Screen.width - (80.0f * ResolutionScale)));
    }
}
