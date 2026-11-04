using System.Collections.Generic;

namespace AIBuildDiagnoseTest
{
    // Private companion seam, not an APIShared service. The hook owner supplies the
    // synchronous capture point; the companion owns all expensive native readers.
    internal interface IAiBuildDetailCapture
    {
        AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode);
        AiEconomyGridEvidence CaptureEconomyGridEvidence(int x, int y);
        AiEconomyGridEvidence CaptureEconomyGridEvidence(ulong state, int mode, int x, int y);
        AiNearbyPathEvidence CaptureNearbyPathEvidence(ulong state, int inputX, int inputY,
            int resultX, int resultY, bool before);
        AiRouteEvidence CaptureRouteEvidence(int player, int mapper, int x, int y);
        IReadOnlyList<AiPathTileSample> CaptureTiles(int x, int y, int width, int height);
    }
}
