using System;

namespace ImprovedHunters
{
    /// <summary>
    /// Where the native Tanner stubs find a Tannery's meat runs and its current trip: the native layout of
    /// APIShared's building state table (TannerMeatState), passed in so the stubs can also run against a
    /// test table. Entries are indexed by building ID; an entry only counts while its global ID matches the
    /// building's, so a new building on a reused slot starts clean.
    /// </summary>
    internal sealed class TannerStateLayout
    {
        public TannerStateLayout(ulong address, int capacity, int entrySize, int globalIdOffset, int meatRunsOffset, int meatTripOffset)
        {
            // The stubs address an entry with one scaled index register.
            if (address == 0 || capacity <= 0 || (entrySize != 1 && entrySize != 2 && entrySize != 4 && entrySize != 8))
                throw new ArgumentException("The Tannery table layout cannot be addressed by the native stubs.");
            Address = address;
            Capacity = capacity;
            EntrySize = entrySize;
            GlobalIdOffset = globalIdOffset;
            MeatRunsOffset = meatRunsOffset;
            MeatTripOffset = meatTripOffset;
        }

        public ulong Address { get; }
        public int Capacity { get; }
        public int EntrySize { get; }
        public int GlobalIdOffset { get; }
        /// <summary>How many of a cow's hides become meat; written only by the owner's synchronized request.</summary>
        public int MeatRunsOffset { get; }
        /// <summary>Whether the Tanner's current trip carries meat; written only by the native stubs.</summary>
        public int MeatTripOffset { get; }
    }
}
