using System;

namespace ExtraFeatures
{
    // Own only the value we last wrote; never reset a foreign replacement.
    internal sealed class KeepBuildRangeOverride
    {
        // Same upper bound as the KeepBuildRange setting (ExtraFeaturesViewModel).
        internal const int MaximumRange = 500;
        // Protects our ownership bookkeeping only, not external ManagedValue readers.
        private readonly object sync = new object();
        private bool ownsValue;
        private int previousValue;
        private int writtenValue;

        // Simulate the next lobby-start reconciliation without changing ownership or native state.
        internal int Preview(int requested, int current)
        {
            lock (sync)
                return requested > 0 ? Math.Min(MaximumRange, requested) :
                    ownsValue && current == writtenValue ? previousValue : current;
        }

        internal void Reconcile(int requested, Func<int> read, Action<int> write)
        {
            lock (sync)
            {
                int current = read();
                // An external replacement ends our ownership, even if it already matches
                // the next requested setting. Matching values must not become ours.
                if (ownsValue && current != writtenValue)
                    ownsValue = false;
                if (requested <= 0)
                {
                    if (ownsValue && current == writtenValue)
                        write(previousValue);
                    ownsValue = false;
                    return;
                }

                requested = Math.Min(MaximumRange, requested);
                if (current == requested)
                    return;
                if (!ownsValue)
                    previousValue = current;
                write(requested);
                writtenValue = requested;
                ownsValue = true;
            }
        }
    }
}
