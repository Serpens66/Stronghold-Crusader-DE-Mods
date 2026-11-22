#if SERP_DEVDIAG
using System.Globalization;

namespace AIMarketOverbuyDiagnostic
{
    // Copied numeric evidence only. These allocating formatters belong exclusively
    // to the background writer; the native observer never calls them.
    internal struct MarketDemandEvidence
    {
        internal long Session, Sequence, SourceSequence, Goal;
        internal bool IsMaterial;
        internal int Kind, Player, Good, Tick, SourceTick, SourceRva, SourceStock, Stock, Before, After, AicId, BoundaryRva, OriginalPackage, Credited, MaterialCost, MaterialPadding;
    }

    internal static class MarketDemandLog
    {
        internal const int ExampleLineBudgetBytes = 500, SummaryLineBudgetBytes = 320;
        internal const int HealthyStartupBudgetBytes = 4000;

        internal static long EightHourAdditionalBudgetBytes(int players, int goods) =>
            (8L * 60 * 60 / 10 + 2) * SummaryLineBudgetBytes +
            (long)players * goods * 2 * ExampleLineBudgetBytes + HealthyStartupBudgetBytes;

        internal static string FormatSource(MarketDemandEvidence item) => string.Format(CultureInfo.InvariantCulture,
            "MARKET_STALE_NATIVE_REQUEST: session={0} player={1} good={2} nativeTick={3} sequence={4} sourceRva={5:X} initialStock={6} amount={7} initialSum={8} aicNativeIndex={9} pendingWrites=0",
            item.Session, item.Player, item.Good, item.Tick, item.Sequence, item.SourceRva,
            item.SourceStock, item.After, item.Goal, item.AicId);

        internal static string FormatAdjustment(MarketDemandEvidence item) => string.Format(CultureInfo.InvariantCulture,
            "MARKET_PENDING_ADJUSTED: session={0} player={1} good={2} nativeTick={3} sourceRva={4:X} sourceTick={5} sourceSequence={6} initialStock={7} mode={15} package={8} credited={14} cost={16} pad={17} stock={9} oldPending={10} newPending={11} aic={12} boundaryRva={13:X} corrected=1",
            item.Session, item.Player, item.Good, item.Tick, item.SourceRva, item.SourceTick, item.SourceSequence,
            item.SourceStock, item.OriginalPackage, item.Stock, item.Before, item.After, item.AicId, item.BoundaryRva,
            item.Credited, item.IsMaterial ? "m" : "b", item.MaterialCost, item.MaterialPadding);

        internal static string FormatSummary(long stores, long reductions, long increases, long cancellations,
            long sourceP99Micros, long beforeP99Micros, long afterP99Micros, long stockP99Micros, long omitted) =>
            "MD_SUM scope=process delta36=" + Base36(stores) + "/" + Base36(reductions) +
            "/" + Base36(increases) + "/" + Base36(cancellations) + " p99us36=" +
            Base36(sourceP99Micros) + "/" + Base36(beforeP99Micros) + "/" + Base36(afterP99Micros) + "/" + Base36(stockP99Micros) + " om36=" + Base36(omitted);

        private static string Base36(long value)
        {
            const string digits = "0123456789abcdefghijklmnopqrstuvwxyz";
            char[] text = new char[13];
            int position = text.Length;
            ulong remaining = value < 0 ? 0UL : (ulong)value;
            do { text[--position] = digits[(int)(remaining % 36)]; remaining /= 36; } while (remaining != 0);
            return new string(text, position, text.Length - position);
        }
    }
}
#endif
