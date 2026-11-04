using System;
using System.Collections.Generic;
using System.Text;

namespace AIBuildDiagnoseTest
{
    // Pure production arithmetic: all values are measured; missing attribution is never filled in.
    internal sealed class EconomyLedger
    {
        private readonly Dictionary<int, long> baseline = new Dictionary<int, long>();
        private readonly Dictionary<int, long> booked = new Dictionary<int, long>();
        internal void Reset() { baseline.Clear(); booked.Clear(); }
        internal void Book(int player, long delta)
        { booked[player] = (booked.TryGetValue(player, out long old) ? old : 0) + delta; }
        internal string Reconcile(int player, int gold)
        {
            long known = booked.TryGetValue(player, out long amount) ? amount : 0;
            string value = baseline.TryGetValue(player, out long previous)
                ? $"gold={gold}, actualDelta={(long)gold - previous}, attributedDelta={known}, unattributedDelta={(long)gold - previous - known}"
                : $"gold={gold}, baselineOnly=true, historicalCause=unknown";
            baseline[player] = gold; booked[player] = 0;
            return value;
        }
    }

    internal sealed class EconomyLogBudget
    {
        internal const int ReservedBytes = 8 * 1024 * 1024;
        internal const int RoutineLimit = 4 * 1024 * 1024;
        internal const int FinalLimit = 64 * 1024;
        internal const int CauseLimit = ReservedBytes - RoutineLimit - FinalLimit;
        internal long Bytes { get; private set; }
        internal long RoutineBytes { get; private set; }
        internal long CauseBytes { get; private set; }
        internal long FinalBytes { get; private set; }
        internal readonly Dictionary<string, int> Omitted = new Dictionary<string, int>();
        internal void Reset() { Bytes = RoutineBytes = CauseBytes = FinalBytes = 0; Omitted.Clear(); }
        internal static bool IsCause(string category) => category.StartsWith("WORKER_", StringComparison.Ordinal) ||
            category.StartsWith("REMOVAL_", StringComparison.Ordinal) || category == "REMOVAL_CONFIRMED" ||
            category == "REBUILD_AFTER_REMOVAL" || category == "DELETE_BEGIN" || category == "DELETE_END";
        internal bool Accept(string category, string line)
        {
            int size = Encoding.UTF8.GetByteCount(line) + 80; // includes logger prefix/timestamp
            bool final = category.StartsWith("FINAL_", StringComparison.Ordinal);
            bool cause = !final && IsCause(category);
            long used = final ? FinalBytes : cause ? CauseBytes : RoutineBytes;
            long limit = final ? FinalLimit : cause ? CauseLimit : RoutineLimit;
            if (used + size <= limit)
            {
                Bytes += size;
                if (final) FinalBytes += size; else if (cause) CauseBytes += size; else RoutineBytes += size;
                return true;
            }
            Omitted[category] = (Omitted.TryGetValue(category, out int old) ? old : 0) + 1;
            return false;
        }
    }

    internal sealed class EconomyOperation
    {
        internal readonly long Id, Session;
        internal readonly int Player, GoldBefore;
        internal readonly string Kind;
        internal readonly EconomyOperation Parent;
        internal long ChildDelta;
        internal int ArgumentA, ArgumentB;
        internal int? StockBefore, RawWoodPrecondition;
        internal bool? NativeOriginalEvaluated;
        internal string DecisionInputs;
        internal RemovalInputs RemovalInputs;
        internal BuildingEvidenceIdentity RemovalTarget;
        internal EconomyOperation(long id, long session, string kind, int player, int gold,
            EconomyOperation parent)
        { Id = id; Session = session; Kind = kind; Player = player; GoldBefore = gold; Parent = parent; }
        internal long ExclusiveDelta(int after) => (long)after - GoldBefore - ChildDelta;
        internal EconomyOperation ForSession(long session) => Session == session ? this : null;
        internal EconomyOperation ForPlayer(int player)
        {
            for (EconomyOperation p = this; p != null; p = p.Parent)
                if (p.Session == Session && p.Player == player) return p;
            return null;
        }
        internal void Close(int after)
        {
            // Propagate through intervening operations belonging to other players.
            for (EconomyOperation p = Parent; p != null; p = p.Parent)
                if (p.Session == Session && p.Player == Player) { p.ChildDelta += (long)after - GoldBefore; break; }
        }
    }

    internal sealed class BuildingEvidenceIdentity
    {
        internal readonly int Slot, Player, Type, X, Y, State;
        internal readonly uint GlobalId;
        internal readonly uint Group;
        internal BuildingEvidenceIdentity(int slot, uint globalId, int player, int type, int x, int y, int state, uint group = 0)
        { Slot = slot; GlobalId = globalId; Player = player; Type = type; X = x; Y = y; State = state; Group = group; }
        internal bool Matches(int slot, uint globalId) => Slot == slot && GlobalId == globalId;
        internal static bool IsRemoved(BuildingEvidenceIdentity before, BuildingEvidenceIdentity after) =>
            after != null && (!before.Matches(after.Slot, after.GlobalId) ||
                after.State == (int)SHCDESE.Interop.Enums.AliveState.None);
        public override string ToString() => $"building={Slot}, globalId={GlobalId}, type={Type}, tile=({X},{Y}), state={State}, nativeGroup={Group}";
    }

    internal static class EconomyDecisionEvidence
    {
        // The counter path already emits its own identity-aware decision record.
        internal static bool EmitGenericResult(string kind) => kind != "wood-counter-retirement";
        internal static string DecisionKey(EconomyOperation op) =>
            op.Player + ":" + op.Kind + ":" + op.ArgumentA + ":" + op.ArgumentB + ":" + op.RemovalInputs?.Target?.GlobalId;
        internal static bool HasMeasuredBookingContext(string kind) =>
            kind == "tax-settlement" || kind == "ai-buy" || kind == "ai-sell" || kind == "build" ||
            kind == "bulldoze" || kind == "refund" || kind == "ai-recruitment" ||
            kind == "hovel-reduction" || kind == "wood-retirement" || kind == "emergency-demolition";
        internal static string Reason(string kind, int? result, int? rawWoodPrecondition)
        {
            if (!result.HasValue) return "native-result-unavailable";
            if (kind == "wood-eligibility" && result == 0 && rawWoodPrecondition.HasValue && rawWoodPrecondition < 1)
                return "native-first-predicate:raw_3505470<1";
            if (kind.EndsWith("eligibility", StringComparison.Ordinal))
                return result == 0 ? "native-eligibility-rejected; subcondition=unknown" : "native-eligibility-accepted";
            if (kind == "build-resources") return result == 0 ? "native-resource-check-rejected" : "native-resource-check-accepted";
            return "native-result-observed";
        }
        internal static string TradeOutcome(string kind, int? result, int? before, int after)
        {
            if (kind == "ai-buy" && result == 0) return "purchase-returned-failure";
            if (!before.HasValue) return "stock-evidence-unavailable";
            long delta = (long)after - before.Value;
            if (kind == "ai-buy" && result.HasValue && result != 0 && delta > 0) return "purchase-and-stock-increase-observed";
            if (kind == "ai-sell" && delta < 0) return "sale-and-stock-decrease-observed";
            return "transaction-return-without-confirmed-stock-change";
        }
    }
}
