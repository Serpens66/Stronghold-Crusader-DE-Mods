using System;
using System.Globalization;

namespace Spectator
{
    internal enum TestActionKind { Credit, Attack, Defend, Cancel, Power, Gate, GateAutomation, PowerToggle }

    // Pure policy used by both the runtime and its independent behavioral checks.
    internal static class CreditMath
    {
        internal static bool TryParse(string text, out int amount)
        { return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0; }

        internal static bool Fits(long current, int amount)
        { return current >= 0 && amount > 0 && current <= int.MaxValue - (long)amount; }

    }

    internal static class CreditInput
    {
        internal static bool IsEditable(string text)
        {
            if (text == null) return false;
            if (text.Length == 0) return true;
            foreach (char character in text) if (character < '0' || character > '9') return false;
            int value;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        internal static bool TryReplace(string text, int start, int length, string insertion, out string candidate)
        {
            candidate = text;
            if (text == null || insertion == null || start < 0 || length < 0 ||
                start > text.Length || length > text.Length - start) return false;
            string next = text.Remove(start, length).Insert(start, insertion);
            if (!IsEditable(next)) return false;
            candidate = next;
            return true;
        }

        internal static string Normalize(string text)
        {
            int value;
            return IsEditable(text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
                ? value.ToString(CultureInfo.InvariantCulture) : "0";
        }
    }

    internal sealed class TestActionLedger
    {
        private long session, lastOperation;
        internal void Reset(long sessionId) { session = sessionId; lastOperation = 0; }
        internal bool TryBegin(long sessionId, long operation)
        {
            if (session <= 0 || sessionId != session || operation <= lastOperation) return false;
            lastOperation = operation;
            return true;
        }
    }

    internal sealed class ForcedOrderContext
    {
        internal readonly TestActionKind Kind;
        internal readonly int Actor, Requester, Target;
        internal ForcedOrderContext(TestActionKind kind, int actor, int requester, int target)
        { Kind = kind; Actor = actor; Requester = requester; Target = target; }
        internal bool Matches(TestActionKind kind, int actor, int requester, int target)
        { return Kind == kind && Actor == actor && Requester == requester && Target == target; }
    }
}
