using System;

namespace PitchMoatLineTest
{
    internal readonly struct LineTile
    {
        internal readonly int X;
        internal readonly int Y;

        internal LineTile(int x, int y)
        {
            X = x;
            Y = y;
        }
    }

    internal enum LineTileState : byte { Buildable, Existing, Blocked, Material, Capacity, OverlapUnavailable }

    internal struct LineQuote
    {
        internal int TotalCost;
        internal int Stock;
        internal int BuildableCount;
        internal int ExistingCount;
        internal int BlockedCount;
        internal int MaterialCount;
        internal int CapacityCount;
        internal int OverlapUnavailableCount;

        internal void Count(LineTileState state)
        {
            switch (state)
            {
                case LineTileState.Buildable: BuildableCount++; break;
                case LineTileState.Existing: ExistingCount++; break;
                case LineTileState.Blocked: BlockedCount++; break;
                case LineTileState.Material: MaterialCount++; break;
                case LineTileState.Capacity: CapacityCount++; break;
                case LineTileState.OverlapUnavailable: OverlapUnavailableCount++; break;
            }
        }
    }

    internal enum PitchPayment
    {
        Standard,
        CheckStockOnly,
        Free
    }

    internal struct PitchBudget
    {
        private const int TilesPerPitch = 4;
        internal readonly bool Valid;
        internal readonly int Stock;
        private readonly int rawMinimum;
        private readonly PitchPayment payment;
        private readonly bool ironAllowed, laterResourcesAllowed;
        private int remainingStock, phase;

        internal PitchBudget(int stock, int phase, int rawMinimum, PitchPayment payment,
            bool ironAllowed = true, bool laterResourcesAllowed = true)
        {
            Valid = stock >= 0 && phase >= 0 && phase < TilesPerPitch && rawMinimum >= 0;
            Stock = remainingStock = stock;
            this.phase = phase;
            this.rawMinimum = rawMinimum;
            this.payment = payment;
            this.ironAllowed = ironAllowed;
            this.laterResourcesAllowed = laterResourcesAllowed;
        }

        internal int Spent => Stock - remainingStock;

        internal bool TryPlace()
        {
            if (payment == PitchPayment.Free)
                return true;
            // CC420 checks iron first. A prepaid tile with a pitch shortage
            // skips the later gold, wood and stone checks.
            if (!ironAllowed || !(remainingStock < rawMinimum ? phase != 0 : laterResourcesAllowed))
                return false;
            if (payment == PitchPayment.Standard)
            {
                if (phase == 0 && remainingStock > 0)
                    remainingStock--;
                phase = (phase + 1) % TilesPerPitch;
            }
            return true;
        }
    }

    internal static class LinePolicy
    {
        internal const int MaxTiles = 64;

        internal static int GetTiles(LineTile start, LineTile end, LineTile[] output)
        {
            int count = 0;
            int limit = Math.Min(MaxTiles, output.Length);
            int x = start.X;
            int y = start.Y;
            while (count < limit)
            {
                output[count++] = new LineTile(x, y);
                if (x == end.X && y == end.Y)
                    break;

                // Like the wall preview, advance each coordinate that has yet to reach the end.
                if (x < end.X) x++;
                else if (x > end.X) x--;
                if (y < end.Y) y++;
                else if (y > end.Y) y--;
            }

            return count;
        }

        internal static LineQuote ApplyLimits(LineTileState[] states, int count, int capacity, PitchBudget budget)
        {
            var quote = new LineQuote { Stock = budget.Stock };
            for (int i = 0; i < count; i++)
            {
                if (states[i] == LineTileState.Buildable)
                {
                    if (quote.BuildableCount >= capacity)
                        states[i] = LineTileState.Capacity;
                    else if (!budget.TryPlace())
                        states[i] = LineTileState.Material;
                }
                quote.Count(states[i]);
            }
            quote.TotalCost = budget.Spent;
            return quote;
        }
    }
}
