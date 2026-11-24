namespace BugfixesAndQoL.AutoSend
{
    // A deferred click belongs to one exact menu/selection snapshot. Validation
    // of live sessions and player/goods domains remains with the UI caller.
    internal readonly struct AutoSendDisclosureTarget
    {
        private readonly long session, generation;
        private readonly int sender, viewed, recipient, good;
        internal AutoSendDisclosureTarget(long session, long generation, int sender, int viewed, int recipient, int good)
        { this.session = session; this.generation = generation; this.sender = sender; this.viewed = viewed; this.recipient = recipient; this.good = good; }
        internal bool Matches(AutoSendDisclosureTarget other) => session == other.session && generation == other.generation &&
            sender == other.sender && viewed == other.viewed && recipient == other.recipient && good == other.good;
    }

    internal sealed class AutoSendPendingDisclosureClick
    {
        private AutoSendDisclosureTarget target;
        internal bool HasPending { get; private set; }
        internal void Record(AutoSendDisclosureTarget value) { target = value; HasPending = true; }
        internal void Cancel() { HasPending = false; target = default; }
        internal bool TryConsume(AutoSendDisclosureTarget current)
        {
            bool matches = HasPending && target.Matches(current);
            Cancel(); return matches;
        }
    }

    // Snapshot from the existing simulation-side recipient check. Unknown must not
    // be presented as an enemy while native resources or the sender are not ready.
    internal enum AutoSendRecipientReadiness { Unknown, AlliedLiving, NotAllied, NotLiving }
    // Local presentation only. Every new menu/HUD/mission starts collapsed;
    // selection changes within the same send menu retain the user's choice.
    internal sealed class AutoSendDisclosureState
    {
        private long session;
        private long panelGeneration;
        private bool sendMode;
        internal bool Expanded { get; private set; }
        internal void ObserveMenu(long currentSession, bool currentSendMode, long currentPanelGeneration)
        {
            if (session != currentSession || !currentSendMode || panelGeneration != currentPanelGeneration)
                Expanded = false;
            session = currentSession; sendMode = currentSendMode; panelGeneration = currentPanelGeneration;
        }
        internal void ObserveGoodsClick(bool confirmedRuleEnabled)
        {
            if (session != 0 && sendMode && confirmedRuleEnabled) Expanded = true;
        }
        internal void Toggle(long current)
        {
            if (current != 0 && current == session && sendMode) Expanded = !Expanded;
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
