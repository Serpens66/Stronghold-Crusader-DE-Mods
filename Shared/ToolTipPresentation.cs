using System;
using UnityEngine;

namespace Shared
{
    public static class ToolTipPresentation
    {
        private const double ReferenceHeight = 1440.0;
        private const double BaseFontSize = 20.0;

        // Preserve the physical tooltip size when the game silently switches to a
        // higher render resolution. Lower resolutions retain the readable baseline.
        private static double ResolutionScale =>
            Math.Max(1.0, Screen.height / ReferenceHeight);

        public static double FontSize =>
            BaseFontSize * ResolutionScale;

        // Modsettings must keep overflowing localized text and wide controls reachable
        // instead of clipping them at the settings viewport boundary.
        public static Noesis.ScrollBarVisibility AutomaticScrollBarVisibility =>
            Noesis.ScrollBarVisibility.Auto;

        // Scale width and edge margin together with the font so line lengths remain
        // comparable between 1440p and higher render resolutions.
        public static double MaximumWidth =>
            Math.Max(
                320.0 * ResolutionScale,
                Math.Min(1000.0 * ResolutionScale, Screen.width - (80.0 * ResolutionScale)));
    }
}
