using System.Threading;

namespace BugfixesAndQoL.EditorRegions
{
    internal enum RefreshDecision { None, AlreadyRebuilt, Rebuild }

    // Mutations are serialized by Vanilla's engine lock. Pending is also the cheap
    // cross-thread fast gate; no map memory is read while there is no editing work.
    internal sealed class RefreshState
    {
        private long sessionId;
        private bool editor, enabled, flushing;
        private int pending, nextBatch;
        private long placementInput;
        private uint generation;
        internal bool Active => editor && enabled;
        internal bool HasPending => Volatile.Read(ref pending) != 0;
        internal int PendingInputTicket => Volatile.Read(ref pending);
        internal bool CanFlush
        {
            get
            {
                int ticket = PendingInputTicket;
                return ticket != 0 && Volatile.Read(ref placementInput) == InputSnapshot(ticket, false);
            }
        }

        // The main thread publishes a sample for the batch it actually observed.
        // A late sample for a retired batch cannot release a newer brush stroke.
        internal void ObservePlacementButton(int ticket, bool held)
        {
            if (ticket != 0) Volatile.Write(ref placementInput, InputSnapshot(ticket, held));
        }

        internal void InvalidatePlacementInput() { Volatile.Write(ref placementInput, 0); }

        private static long InputSnapshot(int ticket, bool held) =>
            ((long)(uint)ticket << 2) | (held ? 1L : 2L);

        internal void Start(long id, bool isEditor, bool configured)
        {
            if (sessionId != id) { Clear(); sessionId = id; }
            editor = isEditor;
            SetEnabled(configured);
        }

        internal void End(long id)
        {
            if (sessionId == id) { Clear(); sessionId = 0; editor = false; }
        }

        internal void SetEnabled(bool value)
        {
            enabled = value;
            if (!Active) { Volatile.Write(ref pending, 0); InvalidatePlacementInput(); }
        }

        internal bool Changed(uint currentGeneration)
        {
            if (!Active) return false;
            // With deferred invalidation only a rebuild AFTER the latest edit is
            // proof of freshness; a rebuild midway through a stroke is not enough.
            generation = currentGeneration;
            if (!HasPending)
            {
                InvalidatePlacementInput();
                int ticket = unchecked(++nextBatch);
                if (ticket == 0) ticket = unchecked(++nextBatch);
                Volatile.Write(ref pending, ticket);
            }
            return true;
        }

        internal RefreshDecision BeginFlush(uint currentGeneration, bool dirty)
        {
            if (!Active || flushing || !CanFlush) return RefreshDecision.None;
            // Consume before calling native code: an edit made by a nested callback
            // belongs to the next pass and must survive completion of this one.
            Volatile.Write(ref pending, 0);
            if (!dirty && currentGeneration != generation) return RefreshDecision.AlreadyRebuilt;
            flushing = true;
            return RefreshDecision.Rebuild;
        }

        internal void CompleteFlush() { flushing = false; }
        private void Clear() { Volatile.Write(ref pending, 0); InvalidatePlacementInput(); flushing = false; }
    }
}
