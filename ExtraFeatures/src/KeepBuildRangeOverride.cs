using System;

namespace ExtraFeatures
{
    // Own only the value we last wrote; never reset a foreign replacement.
    internal sealed class KeepBuildRangeOverride
    {
        // Protects our ownership bookkeeping only, not external ManagedValue readers.
        private readonly object sync = new object();
        private bool ownsValue;
        private int previousValue;
        private int writtenValue;

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

                requested = Math.Min(500, requested);
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
