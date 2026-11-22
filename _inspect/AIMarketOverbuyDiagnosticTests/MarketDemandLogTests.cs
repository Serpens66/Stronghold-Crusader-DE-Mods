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
            IsMaterial = true, MaterialCost = int.MaxValue, MaterialPadding = 2,
            IsAmmo = false, UnitId = int.MaxValue, UnitGlobalId = uint.MaxValue,
            UnitCreationTick = uint.MaxValue, Ammo = short.MinValue, AmmoState = 2
        };
        CultureInfo original = CultureInfo.CurrentCulture;
        int sourceBytes, adjustmentBytes, summaryBytes, batchSourceBytes, batchAdjustmentBytes, ammoSourceBytes, ammoAdjustmentBytes;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            sourceBytes = LineBytes(MarketDemandLog.FormatSource(item));
            adjustmentBytes = LineBytes(MarketDemandLog.FormatAdjustment(item));
            item.IsMaterial = false;
            batchSourceBytes = LineBytes(MarketDemandLog.FormatSource(item));
            batchAdjustmentBytes = LineBytes(MarketDemandLog.FormatAdjustment(item));
            item.IsAmmo = true;
            ammoSourceBytes = LineBytes(MarketDemandLog.FormatSource(item));
            ammoAdjustmentBytes = LineBytes(MarketDemandLog.FormatAdjustment(item));
            summaryBytes = LineBytes(MarketDemandLog.FormatSummary(long.MaxValue, long.MaxValue,
                long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue, long.MaxValue));
        }
        finally { CultureInfo.CurrentCulture = original; }
        Check(sourceBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual source formatter exceeds its line budget");
        Check(adjustmentBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual adjustment formatter exceeds its line budget");
        Check(batchSourceBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual fixed source formatter exceeds its line budget");
        Check(batchAdjustmentBytes <= MarketDemandLog.ExampleLineBudgetBytes, "actual fixed adjustment formatter exceeds its line budget");
        Check(summaryBytes <= MarketDemandLog.SummaryLineBudgetBytes, "actual summary formatter exceeds its line budget");
        Check(ammoSourceBytes <= MarketDemandLog.ExampleLineBudgetBytes && ammoAdjustmentBytes <= MarketDemandLog.ExampleLineBudgetBytes,
            "actual ammo formatter exceeds its line budget");
        var goal = new MarketDemandEvidence { IsMaterial = true, MaterialCost = 20, MaterialPadding = 2,
            OriginalPackage = 5, Credited = 3, SourceStock = 17, Goal = 22, Before = 5, After = 22 };
        string material = MarketDemandLog.FormatAdjustment(goal);
        Check(Field(material, "mode") == "M" && Field(material, "cost") == "20" && Field(material, "pad") == "2" &&
            Field(material, "package") == null && Field(material, "credited") == null,
            "material goal must show required stock rather than misleading package credit");
        goal.IsMaterial = false;
        string batch = MarketDemandLog.FormatAdjustment(goal);
        Check(Field(batch, "mode") == "B" && Field(batch, "package") == "5" && Field(batch, "credited") == "3" &&
            Field(batch, "cost") == null && Field(batch, "pad") == null,
            "fixed goal must show original procurement and credited deliveries");
        goal.After = 5;
        Check(Field(MarketDemandLog.FormatSource(goal), "initialSum") == "22", "fixed source preserves original numeric evidence");
        goal.IsMaterial = true;
        string source = MarketDemandLog.FormatSource(goal);
        Check(Field(source, "cost") == "20" && Field(source, "initialSum") == null,
            "material source must identify the actual required cost");
        goal.IsAmmo = true; goal.UnitId = 7; goal.UnitGlobalId = 81; goal.UnitCreationTick = 104;
        goal.MaterialCost = 10; goal.Ammo = 256; goal.AmmoState = 1;
        string ammo = MarketDemandLog.FormatAdjustment(goal);
        Check(Field(ammo, "mode") == "A" && Field(ammo, "u") == "7/81/104" && Field(ammo, "need") == "10" &&
            Field(ammo, "a") == "256/1" && Field(ammo, "package") == null && Field(ammo, "cost") == null,
            "ammo example must preserve unit generation and signed ammo/result without implying batch credit");
        long additional = MarketDemandLog.EightHourAdditionalBudgetBytes(8, 23);
        Check(additional == 1110240, "eight-hour ceiling includes 2882 summaries, 368 examples and startup allowance");
        // The authoritative combined assertion uses the overview suite's live
        // capture result; do not freeze an earlier measured output in this fixture.
        Check(additional <= 1.06 * 1024 * 1024, "additional bound exceeds 1.06 MiB");
        Console.WriteLine("PASS: actual demand log formatter bytes materialSource=" + sourceBytes +
            " materialAdjustment=" + adjustmentBytes + " batchSource=" + batchSourceBytes +
            " batchAdjustment=" + batchAdjustmentBytes + " summary=" + summaryBytes +
            " ammoSource=" + ammoSourceBytes + " ammoAdjustment=" + ammoAdjustmentBytes +
            "; additional8h=" + additional + ".");
    }

    private static int LineBytes(string text) => Encoding.UTF8.GetByteCount(text) + 100 + 2;
    private static string Field(string text, string name)
    {
        string prefix = name + "=";
        foreach (string part in text.Split(' '))
            if (part.StartsWith(prefix, StringComparison.Ordinal)) return part.Substring(prefix.Length);
        return null;
    }
}
