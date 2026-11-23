namespace AutoSendTest
{
    // Snapshot from the existing simulation-side recipient check. Unknown must not
    // be presented as an enemy while native resources or the sender are not ready.
    internal enum AutoSendRecipientReadiness { Unknown, AlliedLiving, NotAllied, NotLiving }
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
        internal bool IsVisible(bool sendMode, bool eligibleRecipient = true, bool layoutAvailable = true) =>
            session != 0 && sendMode && Expanded && eligibleRecipient && layoutAvailable;
        internal static string UnavailableReason(bool senderReady, bool transferUnavailable, bool identityMismatch,
            bool ownView, AutoSendRecipientReadiness recipient, bool layoutAvailable)
        {
            if (transferUnavailable) return "TransferUnavailable";
            if (identityMismatch) return "IdentityMismatch";
            if (!ownView) return "OwnView";
            if (!senderReady) return "NotReady";
            if (recipient == AutoSendRecipientReadiness.NotAllied) return "RecipientNotAllied";
            if (recipient == AutoSendRecipientReadiness.NotLiving) return "RecipientNotLiving";
            if (recipient != AutoSendRecipientReadiness.AlliedLiving) return "NotReady";
            return layoutAvailable ? null : "LayoutUnavailable";
        }
    }
}
