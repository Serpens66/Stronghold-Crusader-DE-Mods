using System;
using PitchMoatLineTest;

internal static class Program
{
    private static void Main()
    {
        // The mixed slopes distinguish wall movement from a Bresenham raster.
        ExpectLine("diagonal then horizontal", (10, 20), (14, 22),
            (10, 20), (11, 21), (12, 22), (13, 22), (14, 22));
        ExpectLine("reverse mixed slope starts its diagonal at the new origin", (14, 22), (10, 20),
            (14, 22), (13, 21), (12, 20), (11, 20), (10, 20));
        ExpectLine("diagonal then vertical", (10, 20), (12, 16),
            (10, 20), (11, 19), (12, 18), (12, 17), (12, 16));
        ExpectLine("horizontal", (13, 20), (10, 20), (13, 20), (12, 20), (11, 20), (10, 20));
        ExpectLine("vertical", (10, 20), (10, 22), (10, 20), (10, 21), (10, 22));
        ExpectLine("diagonal", (10, 20), (12, 18), (10, 20), (11, 19), (12, 18));
        ExpectLine("single click", (10, 20), (10, 20), (10, 20));

        var longLine = new LineTile[80];
        longLine[64] = new LineTile(-1, -1);
        int count = LinePolicy.GetTiles(new LineTile(200, 200), new LineTile(299, 260), longLine);
        Check(count == 64, "long lines are capped even when the buffer has more space");
        Check(longLine[63].X == 263 && longLine[63].Y == 260, "truncation keeps the prefix from the anchor");
        Check(longLine[64].X == -1 && longLine[64].Y == -1, "raster leaves the next slot untouched");

        var smallBuffer = new LineTile[2];
        count = LinePolicy.GetTiles(new LineTile(20, 20), new LineTile(25, 22), smallBuffer);
        Check(count == 2 && smallBuffer[1].X == 21 && smallBuffer[1].Y == 21,
            "a smaller buffer still receives a bounded prefix");

        ExpectQuote("one charge covers four tiles", 5, 1, 0, 1, PitchPayment.Standard, 1, 4);
        ExpectQuote("zero stock still covers prepaid tiles", 7, 0, 1, 1, PitchPayment.Standard, 0, 3);
        ExpectQuote("two prepaid tiles before a new purchase", 7, 1, 2, 1, PitchPayment.Standard, 1, 6);
        ExpectQuote("the last prepaid tile wraps the counter", 6, 1, 3, 1, PitchPayment.Standard, 1, 5);
        ExpectQuote("admission minimum can exceed the per-cycle debit", 8, 3, 0, 3, PitchPayment.Standard, 1, 4);
        ExpectQuote("zero configured threshold admits due and prepaid tiles with empty stock", 6, 0, 0, 0,
            PitchPayment.Standard, 0, 6);
        ExpectQuote("zero configured threshold consumes existing stock and admits the rest of the line", 6, 1, 0, 0,
            PitchPayment.Standard, 1, 6);
        ExpectQuote("pitch shortage skips later checks for prepaid tiles", 7, 0, 1, 1, PitchPayment.Standard, 0, 3,
            laterResourcesAllowed: false);
        ExpectQuote("sufficient pitch keeps later checks even on prepaid tiles", 7, 1, 1, 1, PitchPayment.Standard, 0, 0,
            laterResourcesAllowed: false);
        ExpectQuote("prepaid pitch shortage cannot bypass iron", 7, 0, 1, 1, PitchPayment.Standard, 0, 0,
            ironAllowed: false);
        ExpectQuote("free placement skips the gate", 8, 0, 0, 3, PitchPayment.Free, 0, 8);
        ExpectQuote("check-only placement at the charge boundary", 8, 1, 0, 2, PitchPayment.CheckStockOnly, 0, 0);
        ExpectQuote("check-only placement above the threshold", 8, 2, 0, 2, PitchPayment.CheckStockOnly, 0, 8);
        ExpectQuote("check-only placement retains a prepaid phase and skips later checks", 8, 0, 3, 2,
            PitchPayment.CheckStockOnly, 0, 8, laterResourcesAllowed: false);
        ExpectQuote("check-only placement with enough pitch still checks the other goods", 8, 2, 3, 2,
            PitchPayment.CheckStockOnly, 0, 0, laterResourcesAllowed: false);
        Check(!new PitchBudget(7, 4, 1, PitchPayment.Standard).Valid,
            "an unexpected engine payment phase is not treated as free placement");
        var states = new[] { LineTileState.Buildable, LineTileState.Existing, LineTileState.Blocked,
            LineTileState.Buildable, LineTileState.OverlapUnavailable, LineTileState.Buildable,
            LineTileState.Buildable, LineTileState.Buildable };
        var quote = LinePolicy.ApplyLimits(states, states.Length, 10, new PitchBudget(1, 0, 1, PitchPayment.Standard));
        Check(quote.TotalCost == 1 && quote.BuildableCount == 4 && quote.ExistingCount == 1 &&
            quote.BlockedCount == 1 && quote.OverlapUnavailableCount == 1 && quote.MaterialCount == 1 &&
            states[3] == LineTileState.Buildable && states[6] == LineTileState.Buildable && states[7] == LineTileState.Material,
            "duplicates and obstacles leave the four accepted tiles and their payment order intact");

        states = new[] { LineTileState.Blocked, LineTileState.Buildable, LineTileState.Existing,
            LineTileState.Buildable, LineTileState.Buildable };
        quote = LinePolicy.ApplyLimits(states, states.Length, 1, new PitchBudget(1, 3, 1, PitchPayment.Standard));
        Check(quote.BuildableCount == 1 && quote.CapacityCount == 2 && quote.TotalCost == 0 &&
            states[1] == LineTileState.Buildable && states[3] == LineTileState.Capacity,
            "capacity skips the next charge rather than spending pitch on it");
        Console.WriteLine("PASS: wall raster, per-tile skips, capacity, pitch affordability, and actual debit.");
    }

    private static void ExpectQuote(string name, int count, int stock, int phase, int rawMinimum,
        PitchPayment payment, int cost, int affordableCount, bool ironAllowed = true, bool laterResourcesAllowed = true)
    {
        var states = new LineTileState[count];
        var budget = new PitchBudget(stock, phase, rawMinimum, payment, ironAllowed, laterResourcesAllowed);
        LineQuote quote = LinePolicy.ApplyLimits(states, count, count, budget);
        Check(budget.Valid && quote.TotalCost == cost && quote.Stock == stock && quote.BuildableCount == affordableCount &&
            quote.MaterialCount == count - affordableCount,
            $"{name}: expected cost {cost}, stock {stock}, prefix {affordableCount}, got {quote.TotalCost}/{quote.Stock}/{quote.BuildableCount}");
    }

    private static void ExpectLine(string name, (int X, int Y) start, (int X, int Y) end,
        params (int X, int Y)[] expected)
    {
        var tiles = new LineTile[LinePolicy.MaxTiles];
        int count = LinePolicy.GetTiles(new LineTile(start.X, start.Y), new LineTile(end.X, end.Y), tiles);
        Check(count == expected.Length, $"{name}: expected {expected.Length} tiles, got {count}");
        for (int i = 0; i < count; i++)
            Check(tiles[i].X == expected[i].X && tiles[i].Y == expected[i].Y,
                $"{name}: tile {i} should be {expected[i]}, got ({tiles[i].X}, {tiles[i].Y})");
    }

    private static void Check(bool success, string detail)
    {
        if (!success) throw new InvalidOperationException(detail);
    }
}
