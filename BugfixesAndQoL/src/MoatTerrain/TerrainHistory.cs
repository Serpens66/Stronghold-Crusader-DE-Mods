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

        internal TerrainHistory(IntPtr buffer) { this.buffer = buffer; Reset(); }
        internal Span<ushort> Tiles => new Span<ushort>((byte*)buffer + TerrainState.DataOffset, TerrainState.Capacity);
        internal bool Active => Volatile.Read(ref *(int*)buffer) != 0;
        internal int Captured => *((int*)buffer + 1);
        internal int Restored => *((int*)buffer + 2);
        internal bool InvalidContext => *((int*)buffer + 3) != 0;

        internal void Reset()
        {
            Volatile.Write(ref *(int*)buffer, 0);
            new Span<byte>((void*)buffer, TerrainState.BufferBytes).Clear();
            pending = null; pendingMapSize = 0; mapSize = 0; session = 0; ready = false; usable = true;
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
    }
}
