#if SERP_DEVDIAG
namespace AIMarketOverbuyDiagnostic
{
    // Gameplay admission has no dependency on diagnostic buffers, wall time or disk progress.
    internal struct MarketDemandMissionState
    {
        internal long SessionId;
        internal bool IsEditor, IsMultiplayer, Agreed, InRepair, NativeArmed;
        internal bool CanObserve => SessionId != 0 && NativeArmed && !IsEditor && !InRepair && (!IsMultiplayer || Agreed);
        internal bool Begin(long id, bool editor, bool multiplayer)
        {
            bool changed = SessionId != id;
            SessionId = id; IsEditor = editor; IsMultiplayer = multiplayer;
            if (changed) { InRepair = false; NativeArmed = false; }
            return changed;
        }
        internal void End() { SessionId = 0; InRepair = false; Agreed = false; NativeArmed = false; }
    }
}
#endif
