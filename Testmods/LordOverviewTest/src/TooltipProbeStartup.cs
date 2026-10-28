using System.Diagnostics;
using BepInEx.Logging;
using Noesis;

namespace LordOverviewTest
{
    internal static class TooltipProbeStartup
    {
        [Conditional("SERP_DEVDIAG")]
        internal static void Start(ManualLogSource log)
        {
#if SERP_DEVDIAG
            Shared.DevDiag.Attach(log);
            TooltipReferenceProbe.Start();
#endif
        }

        [Conditional("SERP_DEVDIAG")]
        internal static void Observe(ref Style style, string surface)
        {
#if SERP_DEVDIAG
            style = TooltipReferenceProbe.Observe(style, surface);
#endif
        }
    }
}
