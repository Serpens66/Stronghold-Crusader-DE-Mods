using System.Threading;

namespace BugfixesAndQoL.VanillaPerformance
{
    // Pure decision and bookkeeping, no game access, so the tests can run it outside the game.
    // Thread contract: ShouldDraw and MarkBuffer run on the simulation thread (inside
    // DLL_RunTick); IsStale runs on the Unity main thread after the buffer was handed over.
    internal sealed class RadarThrottlePolicy
    {
        private readonly long intervalTicks;
        private readonly long gapTicks;
        private readonly bool redrawOnCameraChange;
        private bool hasDraw;
        private long lastDraw;
        private long lastCall;
        private int cameraX, cameraY, tilesWide, tilesHigh, radarWidth, radarHeight, radarZoom;

        // intervalTicks: maximum age of the drawn minimap; gapTicks: a pause between two calls
        // this long (map load, menu) forces a fresh image for the first tick afterwards.
        internal RadarThrottlePolicy(long intervalTicks, long gapTicks, bool redrawOnCameraChange)
        {
            this.intervalTicks = intervalTicks < 0 ? 0 : intervalTicks;
            this.gapTicks = gapTicks <= 0 ? 1 : gapTicks;
            this.redrawOnCameraChange = redrawOnCameraChange;
        }

        internal bool ShouldDraw(long now, int cameraX, int cameraY, int tilesWide, int tilesHigh,
            int radarWidth, int radarHeight, int radarZoom)
        {
            bool gap = !hasDraw || now - lastCall >= gapTicks;
            lastCall = now;
            // A different radar texture size or zoom changes the pixel layout of the image and the
            // click table; an old image would be wrong, not only late.
            bool layout = radarWidth != this.radarWidth || radarHeight != this.radarHeight ||
                radarZoom != this.radarZoom;
            bool camera = redrawOnCameraChange && (cameraX != this.cameraX || cameraY != this.cameraY ||
                tilesWide != this.tilesWide || tilesHigh != this.tilesHigh);
            if (!gap && !layout && !camera && now - lastDraw < intervalTicks) return false;
            hasDraw = true;
            lastDraw = now;
            this.cameraX = cameraX; this.cameraY = cameraY;
            this.tilesWide = tilesWide; this.tilesHigh = tilesHigh;
            this.radarWidth = radarWidth; this.radarHeight = radarHeight; this.radarZoom = radarZoom;
            return true;
        }
    }

    // Freshness per managed radar buffer, keyed by the pinned array address the native pass
    // receives (Mono's collector does not move arrays). Vanilla keeps six buffers; a resize
    // allocates new arrays, so unused entries are recycled round-robin.
    internal sealed class RadarBufferFreshness
    {
        internal const int Capacity = 16;
        private readonly long[] addresses = new long[Capacity];
        private readonly int[] stale = new int[Capacity];
        private int next;

        internal void MarkBuffer(long address, bool fresh)
        {
            if (address == 0) return;
            int slot = Find(address);
            if (slot < 0)
            {
                slot = next;
                next = (next + 1) % Capacity;
                Volatile.Write(ref addresses[slot], address);
            }
            Volatile.Write(ref stale[slot], fresh ? 0 : 1);
        }

        // Unknown buffers count as fresh: Vanilla behaviour is the fallback.
        internal bool IsStale(long address)
        {
            if (address == 0) return false;
            int slot = Find(address);
            return slot >= 0 && Volatile.Read(ref stale[slot]) != 0;
        }

        private int Find(long address)
        {
            for (int index = 0; index < Capacity; index++)
                if (Volatile.Read(ref addresses[index]) == address) return index;
            return -1;
        }
    }
}
