using System;
using System.Collections.Generic;

namespace BugfixesAndQoL.VanillaPerformance
{
    // Behaviour tests of the production policy and freshness table. The buffer-ring model mirrors
    // Vanilla's MemoryBuffers (6 buffers, getFreeBuffer/returnBuffer/getNextBufferToRender), up to
    // three processTestMap calls per frame and one ApplyRadarMap per frame (Assembly-CSharp BC8B6A39).
    public static class RadarThrottleTests
    {
        public static void Run()
        {
            PolicyInterval();
            PolicyForcedRedraws();
            FreshnessTable();
            int checkedFrames = 0;
            foreach (long interval in new long[] { 0, 50, 100, 500 })
                foreach (int seed in new[] { 1, 7, 42, 1234 })
                    checkedFrames += RingModel(interval, seed, restore: true, expectRegression: false);
            // Control: without the restore the same model must expose an old image; otherwise the
            // model could not detect the bug the restore prevents.
            RingModel(50, 42, restore: false, expectRegression: true);
            Console.WriteLine("PASS: radar throttle policy, freshness table and 6-buffer ring model (" + checkedFrames + " frames, intervals 0/50/100/500 ms, control regression detected).");
        }

        private static void PolicyInterval()
        {
            var policy = new RadarThrottlePolicy(50, 1000, true);
            Check(policy.ShouldDraw(0, 1, 1, 10, 10, 100, 100, 1), "first call draws");
            Check(!policy.ShouldDraw(10, 1, 1, 10, 10, 100, 100, 1), "inside interval skips");
            Check(!policy.ShouldDraw(49, 1, 1, 10, 10, 100, 100, 1), "49 of 50 skips");
            Check(policy.ShouldDraw(50, 1, 1, 10, 10, 100, 100, 1), "interval reached draws");
            Check(!policy.ShouldDraw(60, 1, 1, 10, 10, 100, 100, 1), "new interval starts at last draw");
            var always = new RadarThrottlePolicy(0, 1000, true);
            for (int tick = 0; tick < 5; tick++) Check(always.ShouldDraw(tick, 1, 1, 10, 10, 100, 100, 1), "interval 0 always draws");
        }

        private static void PolicyForcedRedraws()
        {
            var policy = new RadarThrottlePolicy(500, 1000, true);
            Check(policy.ShouldDraw(0, 1, 1, 10, 10, 100, 100, 1), "first");
            Check(policy.ShouldDraw(10, 2, 1, 10, 10, 100, 100, 1), "camera x change draws");
            Check(policy.ShouldDraw(20, 2, 3, 10, 10, 100, 100, 1), "camera y change draws");
            Check(policy.ShouldDraw(30, 2, 3, 11, 10, 100, 100, 1), "screen tiles change draws");
            Check(policy.ShouldDraw(40, 2, 3, 11, 10, 200, 200, 1), "radar size change draws");
            Check(policy.ShouldDraw(50, 2, 3, 11, 10, 200, 200, 2), "radar zoom change draws");
            Check(!policy.ShouldDraw(60, 2, 3, 11, 10, 200, 200, 2), "unchanged skips");
            Check(policy.ShouldDraw(1060, 2, 3, 11, 10, 200, 200, 2), "gap after load/menu draws");
            var noCamera = new RadarThrottlePolicy(500, 1000, false);
            Check(noCamera.ShouldDraw(0, 1, 1, 10, 10, 100, 100, 1), "first without camera redraw");
            Check(!noCamera.ShouldDraw(10, 9, 9, 10, 10, 100, 100, 1), "camera change ignored when disabled");
            Check(noCamera.ShouldDraw(20, 9, 9, 10, 10, 300, 300, 1), "layout change still draws when camera redraw is off");
        }

        private static void FreshnessTable()
        {
            var table = new RadarBufferFreshness();
            Check(!table.IsStale(0) && !table.IsStale(123), "unknown and null buffers count as fresh");
            table.MarkBuffer(123, false);
            Check(table.IsStale(123), "skipped buffer is stale");
            table.MarkBuffer(123, true);
            Check(!table.IsStale(123), "redrawn buffer is fresh again");
            for (int index = 0; index < RadarBufferFreshness.Capacity + 3; index++) table.MarkBuffer(1000 + index, false);
            Check(table.IsStale(1000 + RadarBufferFreshness.Capacity + 2), "recycled entries keep the newest buffers");
        }

        private sealed class Buffer
        {
            internal long Address; internal bool Locked, GotData, Processed, BeingFilled; internal long FrameId;
            internal long ImageTime = -1; // simulation time of the image currently in the array
        }

        private static int RingModel(long interval, int seed, bool restore, bool expectRegression)
        {
            var random = new Random(seed);
            var buffers = new List<Buffer>();
            for (int index = 0; index < 6; index++) buffers.Add(new Buffer { Address = 0x1000 * (index + 1) });
            var policy = new RadarThrottlePolicy(interval, 1000, true);
            var freshness = new RadarBufferFreshness();
            long commonFrameId = 0, now = 0, lastUploaded = -1;
            int frames = 0, regressions = 0;
            Buffer last = null;
            for (int step = 0; step < 20000; step++)
            {
                if (random.Next(3) != 0)
                {
                    // Simulation tick (EngineInterface.run): ~1-9 ms per tick, camera mostly still.
                    now += 1 + random.Next(9);
                    Buffer free = buffers.Find(b => !b.Locked && !b.GotData);
                    if (free == null) continue;
                    free.Locked = true; free.FrameId = commonFrameId++; free.Processed = false; free.BeingFilled = true;
                    int camera = random.Next(50) == 0 ? random.Next(4) : 0;
                    bool draw = policy.ShouldDraw(now, camera, 0, 10, 10, 100, 100, 1);
                    freshness.MarkBuffer(free.Address, draw);
                    if (draw) free.ImageTime = now;
                    free.BeingFilled = false; free.GotData = true; free.Locked = false;
                }
                else
                {
                    // Frame (Director.Update): up to three buffers, then one ApplyRadarMap.
                    for (int processed = 0; processed < 3; processed++)
                    {
                        Buffer next = null;
                        foreach (Buffer b in buffers)
                            if (!b.Locked && !b.Processed && b.GotData && !b.BeingFilled && (next == null || b.FrameId < next.FrameId)) next = b;
                        if (next == null) break;
                        next.Processed = true; next.Locked = true; next.GotData = false;
                        Buffer before = last;
                        last = next;
                        if (restore && freshness.IsStale(next.Address)) last = before;
                        next.Locked = false;
                    }
                    if (last != null)
                    {
                        if (last.ImageTime >= 0)
                        {
                            if (last.ImageTime < lastUploaded) regressions++;
                            else lastUploaded = last.ImageTime;
                        }
                        last = null;
                    }
                    frames++;
                    if (!expectRegression && lastUploaded >= 0)
                        // Interval plus the backlog of six queued ticks (at most 9 ms each) and frame spacing.
                        Check(now - lastUploaded <= interval + 150, "image age bounded (interval " + interval + ", age " + (now - lastUploaded) + ")");
                }
            }
            if (expectRegression) Check(regressions > 0, "control model shows old images without the restore");
            else Check(regressions == 0, "no older image uploaded after a newer one (interval " + interval + ", seed " + seed + ")");
            return frames;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Radar throttle test failed: " + message);
        }
    }
}
