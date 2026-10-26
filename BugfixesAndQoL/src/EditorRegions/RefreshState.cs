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
        private int pending;
        private uint generation;
        internal bool Active => editor && enabled;
        internal bool HasPending => Volatile.Read(ref pending) != 0;

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
            if (!Active) Volatile.Write(ref pending, 0);
        }

        internal bool Changed(uint currentGeneration)
        {
            if (!Active) return false;
            if (!HasPending) generation = currentGeneration;
            Volatile.Write(ref pending, 1);
            return true;
        }

        internal RefreshDecision BeginFlush(uint currentGeneration, bool dirty)
        {
            if (!Active || flushing || !HasPending) return RefreshDecision.None;
            // Consume before calling native code: an edit made by a nested callback
            // belongs to the next pass and must survive completion of this one.
            Volatile.Write(ref pending, 0);
            if (!dirty && currentGeneration != generation) return RefreshDecision.AlreadyRebuilt;
            flushing = true;
            return RefreshDecision.Rebuild;
        }

        internal void CompleteFlush() { flushing = false; }
        private void Clear() { Volatile.Write(ref pending, 0); flushing = false; }
    }
}
