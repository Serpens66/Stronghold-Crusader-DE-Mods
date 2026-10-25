namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class CursorGroundRejectProof
    {
        partial void DiagGroundRejectReason(int kind) => ManualProbeCapture.GroundReject(kind, 0, 0, 0);
    }
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        partial void DiagCursorReject(int kind) => ManualProbeCapture.RegionReject(kind);
        partial void DiagCursorGround(int kind, long started, int sourceRegion, int targetRegion) =>
            ManualProbeCapture.GroundReject(kind, started, sourceRegion, targetRegion);
        partial void DiagCursorGroundReady(bool ready) => Shared.DevDiag.Log(() =>
            "CURSOR_GROUND_PROOF ready=" + ready + " native=FBCB9319 builder=0xF4930 connections=0x51890D0 neighborOffsets=0x405EDB0 batches=8 proofsPerBatch=4 minimumSelection=16");
    }
}
