using System;

namespace BugfixesAndQoL.AutoSend
{
    // Simulation snapshot; this type and its paint comparison require no UI assembly.
    internal sealed class AutoSendDisplay
    {
        internal long SessionId;
        internal int SenderPlayerId, DesiredReserve;
        internal AutoSendRule DesiredRule = new AutoSendRule(0, 0, false);
        internal bool HasDesiredValues;
        internal bool Selected, Ready, SenderReady, Pending, Paused, IdentityMismatch, TransferUnavailable;
        internal bool ChatMessages, ChatSuppressionUnavailable;
        internal AutoSendRecipientReadiness RecipientReadiness;
        internal int Reserve, SourceStock, RecipientStock, Capacity, ActiveRules;
        internal AutoSendRule Rule = new AutoSendRule(0, 0, false);
        internal string Feedback;

        // The compact editor body shows these values. Disclosure and ware badges
        // update before this comparison; selection/context are checked by the view.
        // Stock totals, legacy status fields and schedule clocks need no editor paint.
        internal bool HasSamePaint(AutoSendDisplay other) => other != null &&
            SessionId == other.SessionId && SenderPlayerId == other.SenderPlayerId &&
            Ready == other.Ready && SenderReady == other.SenderReady && Pending == other.Pending &&
            IdentityMismatch == other.IdentityMismatch && TransferUnavailable == other.TransferUnavailable &&
            ChatMessages == other.ChatMessages && ChatSuppressionUnavailable == other.ChatSuppressionUnavailable &&
            HasDesiredValues == other.HasDesiredValues && Reserve == other.Reserve && DesiredReserve == other.DesiredReserve &&
            SameRulePaint(Rule, other.Rule) && SameRulePaint(DesiredRule, other.DesiredRule) &&
            string.Equals(Feedback, other.Feedback, StringComparison.Ordinal);

        private static bool SameRulePaint(AutoSendRule left, AutoSendRule right) => left.Minimum == right.Minimum &&
            left.Maximum == right.Maximum && left.Enabled == right.Enabled && left.IntervalSeconds == right.IntervalSeconds;
    }
}
