using System;
using System.IO;
using System.Threading;

namespace BugfixesAndQoL.MoatTerrain
{
    internal delegate void TerrainFilter(Span<ushort> tiles);

    // Called only at lifecycle/settings/save boundaries under the engine lock.
    // Native stubs own ordinary per-tile writes; no managed dictionary or polling mirrors them.
    internal sealed unsafe class TerrainHistory
    {
        private readonly IntPtr buffer;
        private ushort[] pending;
        private int pendingMapSize, mapSize;
        private long session;
        private bool ready, usable = true;
        private bool repairing, wasActive, repairCommitted;

        internal TerrainHistory(IntPtr buffer) { this.buffer = buffer; Reset(); }
        internal Span<ushort> Tiles => new Span<ushort>((byte*)buffer + TerrainState.DataOffset, TerrainState.Capacity);
        internal bool Active => Volatile.Read(ref *(int*)buffer) != 0;
        internal bool Repairing => repairing;
        internal int Captured => *((int*)buffer + 1);
        internal int Restored => *((int*)buffer + 2);
        internal bool InvalidContext => *((int*)buffer + 3) != 0;

        internal void Reset()
        {
            Volatile.Write(ref *(int*)buffer, 0);
            new Span<byte>((void*)buffer, TerrainState.BufferBytes).Clear();
            pending = null; pendingMapSize = 0; mapSize = 0; session = 0; ready = false; usable = true;
            repairing = wasActive = repairCommitted = false;
        }

        internal void RejectLoad() { pending = null; usable = false; }
        internal void Stage(byte[] bytes) { pending = TerrainState.Decode(bytes, out pendingMapSize); }

        internal void Begin(long sessionId, int currentMapSize, bool enabled, TerrainFilter filter)
        {
            if (ready && session == sessionId) return; // A late lifecycle replay must not erase live history.
            Volatile.Write(ref *(int*)buffer, 0);
            Tiles.Clear();
            mapSize = currentMapSize; session = sessionId;
            if (!enabled) { pending = null; usable = true; }
            if (pending != null && usable)
            {
                if (pendingMapSize != mapSize) { RejectLoad(); throw new InvalidDataException("Moat terrain map layout differs."); }
                filter(pending.AsSpan());
                pending.AsSpan().CopyTo(Tiles);
                pending = null;
            }
            ready = true;
            SetEnabled(enabled);
        }

        internal void SetEnabled(bool enabled)
        {
            if (repairing) return;
            Volatile.Write(ref *(int*)buffer, 0);
            if (!enabled) { Tiles.Clear(); pending = null; }
            if (enabled && ready && usable) Volatile.Write(ref *(int*)buffer, 1);
        }

        internal byte[] Capture(bool enabled, TerrainFilter filter)
        {
            if (!enabled || !ready || !usable) { SetEnabled(false); return null; }
            ushort[] snapshot = Tiles.ToArray();
            filter(snapshot.AsSpan());
            return TerrainState.Encode(snapshot, mapSize);
        }

        internal void BeginRepair(bool insideEngineRun)
        {
            repairing = true;
            wasActive = Active;
            repairCommitted = false;
            if (!insideEngineRun) SuspendRepairWriters();
        }

        internal void SuspendRepairWriters()
        {
            if (repairing && Active) Volatile.Write(ref *(int*)buffer, 0);
        }

        internal byte[] CaptureRepair()
        {
            if (!repairing) throw new InvalidOperationException("Moat terrain history is outside a repair.");
            if (!wasActive) return Array.Empty<byte>();
            ushort[] snapshot = Tiles.ToArray();
            for (int index = 0; index < snapshot.Length; index++)
                if ((snapshot[index] & TerrainState.Preparing) != 0) snapshot[index] = 0;
            return TerrainState.Encode(snapshot, mapSize, repair: true);
        }

        internal ushort[] PrepareRepair(ReadOnlySpan<byte> bytes, int repairedMapSize, TerrainFilter filter)
        {
            if (!repairing || Active || wasActive != (bytes.Length != 0))
                throw new InvalidDataException("Moat terrain activation differs during repair.");
            if (!wasActive) return null;
            ushort[] replacement = TerrainState.Decode(bytes, out int recordMapSize, repair: true);
            if (recordMapSize != mapSize || recordMapSize != repairedMapSize)
                throw new InvalidDataException("Moat terrain repair map layout differs.");
            filter(replacement);
            return replacement;
        }

        internal void CommitRepair(ushort[] replacement)
        {
            if (replacement != null) replacement.AsSpan().CopyTo(Tiles);
            repairCommitted = true;
        }

        internal void CompleteRepair(bool enabled)
        {
            if (!repairing) return;
            if (!repairCommitted)
                throw new InvalidOperationException("Moat terrain repair completed without the host history.");
            repairing = false;
            SetEnabled(enabled && wasActive);
        }
    }
}
