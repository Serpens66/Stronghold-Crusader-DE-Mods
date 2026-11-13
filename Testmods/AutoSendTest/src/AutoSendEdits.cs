using System;
using System.Collections.Generic;

namespace AutoSendTest
{
    internal enum AutoSendEdit { Hello, Reserve, Minimum, Maximum, Enabled, Paused, Ready, SetRule }

    internal static class AutoSendRuleEdits
    {
        internal static bool TryApply(AutoSendRule old, AutoSendEdit operation, int amount, bool enabled,
            out AutoSendRule result, out string reason)
        {
            result = old; reason = string.Empty;
            if (amount < 0) { reason = "Amounts must not be negative."; return false; }
            int min = old.Minimum, max = old.Maximum; bool on = old.Enabled;
            if (operation == AutoSendEdit.Minimum) { min = amount; max = Math.Max(max, min); }
            else if (operation == AutoSendEdit.Maximum) { max = amount; min = Math.Min(min, max); if (max == 0) on = false; }
            else if (operation == AutoSendEdit.Enabled) on = enabled;
            else { reason = "Unknown rule edit."; return false; }
            if (on && max == 0) { reason = "Choose a positive maximum before starting this rule."; return false; }
            result = new AutoSendRule(min, max, on); return true;
        }
    }

    internal sealed class AutoSendEditIntent
    {
        internal readonly long Session;
        internal readonly int Player, Recipient, Good, Amount;
        internal readonly AutoSendEdit Operation;
        internal readonly bool Enabled;
        internal AutoSendEditIntent(long session, int player, AutoSendEdit operation, int recipient, int good, int amount, bool enabled)
        {
            Session = session; Player = player; Operation = operation; Amount = amount; Enabled = enabled;
            Recipient = operation == AutoSendEdit.Reserve || operation == AutoSendEdit.Paused ? 0 : recipient;
            Good = operation == AutoSendEdit.Paused ? 0 : good;
        }
        internal bool SameKey(AutoSendEditIntent other) => Session == other.Session && Player == other.Player &&
            Recipient == other.Recipient && Good == other.Good && Operation == other.Operation;
    }

    // Unity adds immutable intents; the Chore receiver acknowledges the one submitted
    // revision. All calls are made under the owning runtime lock, never its transport.
    internal sealed class AutoSendEditQueue
    {
        private readonly List<AutoSendEditIntent> waiting = new List<AutoSendEditIntent>();
        internal AutoSendEditIntent InFlight { get; private set; }
        private long epoch, revision;
        internal bool Pending => InFlight != null || waiting.Count != 0;
        internal int WaitingCount => waiting.Count;
        internal void Reset() { waiting.Clear(); InFlight = null; epoch = revision = 0; }
        internal void Enqueue(AutoSendEditIntent intent)
        {
            for (int index = 0; index < waiting.Count; index++)
                if (waiting[index].SameKey(intent)) { waiting.RemoveAt(index); break; }
            // Moving a replacement to the tail preserves which min/max edit was last.
            waiting.Add(intent);
        }
        internal bool TryBegin(long session, int player, long commandEpoch, long commandRevision, out AutoSendEditIntent intent)
        {
            intent = null;
            if (InFlight != null) return false;
            while (waiting.Count > 0)
            {
                var candidate = waiting[0]; waiting.RemoveAt(0);
                if (candidate.Session != session || candidate.Player != player) continue;
                InFlight = intent = candidate; epoch = commandEpoch; revision = commandRevision; return true;
            }
            return false;
        }
        internal bool Acknowledge(long session, int player, long commandEpoch, long commandRevision)
        {
            if (InFlight == null || InFlight.Session != session || InFlight.Player != player ||
                commandEpoch != epoch || commandRevision != revision) return false;
            InFlight = null; epoch = revision = 0; return true;
        }
        internal void Failed(AutoSendEditIntent intent)
        { if (ReferenceEquals(InFlight, intent)) { InFlight = null; epoch = revision = 0; } }
        internal bool Project(long session, int player, int recipient, int good, ref int reserve, ref AutoSendRule rule)
        {
            bool changed = ProjectOne(InFlight, session, player, recipient, good, ref reserve, ref rule);
            for (int index = 0; index < waiting.Count; index++)
                changed |= ProjectOne(waiting[index], session, player, recipient, good, ref reserve, ref rule);
            return changed;
        }
        private static bool ProjectOne(AutoSendEditIntent intent, long session, int player, int recipient, int good,
            ref int reserve, ref AutoSendRule rule)
        {
            if (intent == null || intent.Session != session || intent.Player != player || intent.Good != good) return false;
            if (intent.Operation == AutoSendEdit.Reserve) { reserve = intent.Amount; return true; }
            if (intent.Recipient != recipient) return false;
            if (!AutoSendRuleEdits.TryApply(rule, intent.Operation, intent.Amount, intent.Enabled, out var next, out _)) return false;
            rule = next; return true;
        }
    }
}
