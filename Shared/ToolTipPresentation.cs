using System;
using UnityEngine;

namespace Shared
{
    public static class ToolTipPresentation
    {
        // Leave a margin for popup placement on narrower resolutions while allowing
        // substantially longer lines on wide screens.
        public static double MaximumWidth =>
            Math.Max(320.0, Math.Min(1000.0, Screen.width - 80.0));
    }
}
