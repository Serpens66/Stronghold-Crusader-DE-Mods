namespace AutoSendTest
{
    // Local presentation only: neither menu/HUD recreation nor a selection change
    // ends the mission. A new mission (including loading a save) starts collapsed.
    internal sealed class AutoSendDisclosureState
    {
        private long session;
        internal bool Expanded { get; private set; }
        internal void ObserveSession(long current)
        {
            if (session == current) return;
            session = current; Expanded = false;
        }
        internal void Toggle(long current)
        {
            ObserveSession(current);
            if (current != 0) Expanded = !Expanded;
        }
        internal bool IsVisible(bool sendMode) => session != 0 && sendMode && Expanded;
    }
}
