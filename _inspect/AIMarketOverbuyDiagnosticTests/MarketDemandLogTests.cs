using System;
using System.Globalization;
using System.Text;
using AIMarketOverbuyDiagnostic;

internal static class MarketDemandLogTests
{
    private static void Check(bool value, string detail)
    { if (!value) throw new Exception("Market demand log budget failed: " + detail); }

    internal static void Run()
    {
        // Player/good/AIC/RVA use deliberately larger values than valid game
        // contexts. Goal's true maximum is the sum of two nonnegative Int32s.
        var item = new MarketDemandEvidence
        {
            Session = long.MaxValue, Sequence = long.MaxValue, SourceSequence = long.MaxValue,
            Kind = 1, Goal = 2L * int.MaxValue, Player = int.MaxValue, Good = int.MaxValue,
            Tick = int.MinValue, SourceTick = int.MinValue, SourceRva = int.MaxValue,
            SourceStock = int.MaxValue, Stock = int.MaxValue, Before = int.MaxValue,
            After = int.MaxValue, AicId = int.MaxValue, BoundaryRva = int.MaxValue,
            OriginalPackage = int.MaxValue, Credited = int.MaxValue,
            IsMaterial = true, MaterialCost = int.MaxValue, MaterialPadding = 2
        };
        CultureInfo original = CultureInfo.CurrentCulture;
        int sourceBytes, adjustmentBytes, summaryBytes;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            sourceBytes = LineBytes(MarketDemandLog.FormatSource(item));
            adjustmentBytes = LineBytes(MarketDemandLog.FormatAdjustment(item));
            summaryBytes = LineBytes(MarketDemandLog.FormatSummary(long.MaxValue, long.MaxValue,
                long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue));
        }
        finally { CultureInfo.CurrentCulture = original; }
        Check(sourceBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual source formatter exceeds its line budget");
        Check(adjustmentBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual adjustment formatter exceeds its line budget");
        Check(summaryBytes <= MarketDemandLog.SummaryLineBudgetBytes, "actual summary formatter exceeds its line budget");
        long additional = MarketDemandLog.EightHourAdditionalBudgetBytes(8, 23);
        Check(additional == 1110240, "eight-hour ceiling includes 2882 summaries, 368 examples and startup allowance");
        // The authoritative combined assertion uses the overview suite's live
        // capture result; do not freeze an earlier measured output in this fixture.
        Check(additional <= 1.06 * 1024 * 1024, "additional bound exceeds 1.06 MiB");
        Console.WriteLine("PASS: actual demand log formatter bytes source=" + sourceBytes +
            " adjustment=" + adjustmentBytes + " summary=" + summaryBytes +
            "; additional8h=" + additional + ".");
    }

    private static int LineBytes(string text) => Encoding.UTF8.GetByteCount(text) + 100 + 2;
}
