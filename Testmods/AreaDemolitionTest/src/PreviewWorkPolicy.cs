using System;

namespace AreaDemolitionTest
{
    // Only expensive quotes are paced. The screen frame continues following every input frame.
    internal sealed class PreviewWorkPolicy
    {
        internal const int MaximumRootsPerStep=64;
        private readonly long interval, budget;
        private long nextQuote;
        internal PreviewWorkPolicy(long frequency)
        {
            if(frequency<=0) throw new ArgumentOutOfRangeException(nameof(frequency));
            interval=Math.Max(1,frequency/10);
            budget=Math.Max(1,frequency/500);
        }
        internal bool CanBegin(long now,bool finalSelection) => finalSelection || now>=nextQuote;
        internal void Began(long now) => nextQuote=now+interval;
        internal long Deadline(long now) => now+budget;
        internal void Reset() => nextQuote=0;
    }
}
