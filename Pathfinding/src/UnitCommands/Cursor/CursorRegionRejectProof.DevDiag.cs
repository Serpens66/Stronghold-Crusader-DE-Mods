namespace Pathfinding.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        partial void DiagCursorGroundReady(bool ready) => Shared.DevDiag.Log(() =>
            "CURSOR_GROUND_PROOF ready=" + ready + " native=FBCB9319 builder=0xF4930 connections=0x51890D0 neighborOffsets=0x405EDB0 batches=8 proofsPerBatch=4 minimumSelection=16");
    }
}
