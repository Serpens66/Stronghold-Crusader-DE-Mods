using System;
using System.Globalization;

namespace Spectator
{
    internal enum TestActionKind { Credit, Attack, Defend, Cancel, Power, Gate, GateAutomation, PowerToggle }
    internal enum CreditFailure { None, NegativeBalance, Overflow, Postcondition, Provider }

    // Pure policy used by both the runtime and its independent behavioral checks.
    internal static class CreditMath
    {
        internal static bool TryParse(string text, out int amount)
        { amount = 0; return CreditInput.IsEditable(text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out amount) && amount > 0; }

        internal static bool TryAmount(string text, bool debit, out int amount)
        {
            if (!TryParse(text, out amount)) return false;
            if (debit) amount = -amount;
            return true;
        }

        internal static bool Fits(long current, int amount)
        { return current >= 0 && amount > 0 && current <= int.MaxValue - (long)amount; }

        internal static CreditFailure Check(GoodsBalance balance, int amount, bool storedOnly)
        {
            if (balance.Stored < 0 || (!storedOnly && balance.Incoming < 0)) return CreditFailure.NegativeBalance;
            return Fits(storedOnly ? balance.Stored : balance.Total, amount) ? CreditFailure.None : CreditFailure.Overflow;
        }

    }

    internal static class CreditInput
    {
        internal static bool IsEditable(string text)
        {
            if (text == null) return false;
            if (text.Length == 0) return true;
            for (int index = 0; index < text.Length; index++)
                if (text[index] < '0' || text[index] > '9') return false;
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

    internal struct GoodsBalance
    {
        internal readonly int Stored, Incoming;
        internal GoodsBalance(int stored, int incoming) { Stored = stored; Incoming = incoming; }
        internal long Total { get { return (long)Stored + Incoming; } }
        internal void Validate()
        { if (Stored < 0 || Incoming < 0) throw new CreditAccountingException(CreditFailure.NegativeBalance, "Negative resource balance."); }
    }

    internal sealed class CreditAccountingException : InvalidOperationException
    {
        internal readonly CreditFailure Failure;
        internal CreditAccountingException(CreditFailure failure, string message) : base(message) { Failure = failure; }
    }

    // One action's locked snapshots, shared by the UI failure text and its support log.
    internal sealed class CreditTrace
    {
        internal readonly GoodsBalance Before;
        internal readonly int Amount;
        internal readonly bool StoredOnly;
        internal readonly long Expected;
        internal GoodsBalance After;
        internal bool ActualKnown;
        internal CreditFailure Failure;
        internal long Current { get { return StoredOnly ? Before.Stored : Before.Total; } }
        internal long Actual { get { return StoredOnly ? After.Stored : After.Total; } }

        internal CreditTrace(GoodsBalance before, int amount, bool storedOnly, bool reserveOnly)
        {
            Before = before; Amount = amount; StoredOnly = storedOnly; Failure = CreditFailure.None;
            // Debit remains capped at the available account, including the native mode-six reserve contract.
            Expected = amount >= 0 || before.Stored < 0 || before.Incoming < 0
                ? Current + (long)amount
                : Current - Math.Min(-(long)amount, reserveOnly ? before.Incoming : Current);
        }

        internal GoodsBalance Observe(GoodsBalance balance)
        { After = balance; ActualKnown = true; return balance; }

        internal string LogFields { get { return " balance=" + Current.ToString(CultureInfo.InvariantCulture) +
            " stored=" + Before.Stored.ToString(CultureInfo.InvariantCulture) +
            " incoming=" + Before.Incoming.ToString(CultureInfo.InvariantCulture) +
            " expected=" + Expected.ToString(CultureInfo.InvariantCulture) +
            " actual=" + (ActualKnown ? Actual.ToString(CultureInfo.InvariantCulture) : "unknown") +
            " actualStored=" + (ActualKnown ? After.Stored.ToString(CultureInfo.InvariantCulture) : "unknown") +
            " actualIncoming=" + (ActualKnown ? After.Incoming.ToString(CultureInfo.InvariantCulture) : "unknown") +
            " storedOnly=" + StoredOnly + " limit=" + int.MaxValue.ToString(CultureInfo.InvariantCulture) + " failure=" + Failure; } }
    }

    // Shared production transaction, tested with the native API boundary replaced by delegates.
    internal static class CreditDebit
    {
        internal static long Execute(int amount, bool reserveOnly, Func<GoodsBalance> read,
            Action<int> remove, Action<int> removeIncoming)
        {
            if (amount >= 0) throw new ArgumentOutOfRangeException("amount");
            long requested = -(long)amount; // int.MinValue must not overflow.
            GoodsBalance before = read(); before.Validate();
            int first = (int)Math.Min(requested, reserveOnly ? before.Incoming : before.Stored);
            if (first > 0) remove(first);
            GoodsBalance after = read(); after.Validate();
            long removed = before.Total - after.Total;
            if (removed < 0 || removed > requested)
                throw new CreditAccountingException(CreditFailure.Postcondition, "Resource removal was changed beyond the requested amount.");
            // In mode 6 the original already debits incoming goods. Observe rather than subtract twice.
            int remainder = reserveOnly ? 0 : (int)Math.Min(requested - removed, after.Incoming);
            if (remainder > 0) removeIncoming(remainder);
            GoodsBalance final = read(); final.Validate();
            long actual = before.Total - final.Total;
            if (actual < removed || actual > requested)
                throw new CreditAccountingException(CreditFailure.Postcondition, "Incoming resource removal postcondition failed.");
            return actual;
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
