using System;
using UnityEngine;

namespace Shared
{
    public static class ToolTipPresentation
    {
        // Modsettings must keep overflowing localized text and wide controls reachable
        // instead of clipping them at the settings viewport boundary.
        public static Noesis.ScrollBarVisibility AutomaticScrollBarVisibility =>
            Noesis.ScrollBarVisibility.Auto;

        // Leave a margin for popup placement on narrower resolutions while allowing
        // substantially longer lines on wide screens.
        public static double MaximumWidth =>
            Math.Max(320.0, Math.Min(1000.0, Screen.width - 80.0));
    }
}
