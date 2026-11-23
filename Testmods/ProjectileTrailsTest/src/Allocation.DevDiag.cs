#if SERP_DEVDIAG
using System;
using System.Reflection;
using Shared;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private static Func<long> threadAllocations;
        private static bool traceVisibility;
        private static int sampledPositions, sampledPublishes;

        static partial void InitializeAllocationCounters()
        {
            DevDiag.Attach(log);
            try
            {
                var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null) throw new InvalidOperationException("Mono did not expose a per-thread counter.");
                var read = (Func<long>)method.CreateDelegate(typeof(Func<long>));
                long before = read();
                var sample = new byte[4096];
                long difference = read() - before;
                GC.KeepAlive(sample);
                if (difference < sample.Length)
                    throw new InvalidOperationException("Mono did not count a known 4096-byte allocation.");
                threadAllocations = read;
            }
            catch (Exception ex) { WriteInfo("thread allocation counter unavailable: " + ex.Message); }
        }

        static partial void SampleMeshAllocation(ref long allocated)
        {
            if (threadAllocations != null) allocated = threadAllocations();
        }

        static partial void RecordMeshAllocation(long before)
        {
            if (threadAllocations == null) return;
            long bytes = threadAllocations() - before;
            DevDiag.Count("projectileTrail.meshUpload");
            DevDiag.Count("projectileTrail.meshUploadAllocatedBytes", bytes);
        }

        static partial void TraceVanilla(int id, int x, int y, int subX, int subY, float height,
            int file, int image, int transparency, int layerDelay, int kind, System.Collections.Generic.Dictionary<int, Fly> flies)
        {
            if (!traceVisibility || kind != (int)SHCDESE.Interop.Enums.ProjectileType.AfterImage ||
                file != (int)Enums.GM.GM_BODY_MISSILE) return;
            referenceFlies = flies;
            referenceIds.Add(id);
            if (sampledPositions++ >= 260) return;
            WriteInfo(FormattableString.Invariant(
                $"reference tick={native.Tick} id={id} tile={x},{y} sub={subX},{subY} height={height} file={file} frame={image} t={transparency} delay={layerDelay} order={flies[id].sprRenderer.sortingOrder}"));
        }

        static partial void RememberEchoDetails(ref TrailBuffer.Sample echo, NativeFlightSamples.EntrySample sample,
            int x, int y, int subX, int subY)
        {
            if (!traceVisibility) return;
            echo.TileX = x;
            echo.TileY = y;
            echo.SubX = subX;
            echo.SubY = subY;
            echo.File = sample.File;
            echo.Frame = sample.Frame;
            echo.Height = sample.Height;
        }

        static partial void TracePublish(Camera current)
        {
            if (!traceVisibility) return;
            if (sampledPublishes++ < 12)
                WriteInfo($"publish camera={current.name} position={current.transform.position} head={lastHead} tick={lastTick} drawn={LastSegmentCount}");
            for (int i = 0; i < LastSegmentCount && sampledPositions < 260; i++)
            {
                var echo = meshes.GetAccepted(i);
                uint age = unchecked(lastTick - echo.Tick);
                sampledPositions++;
                WriteInfo(FormattableString.Invariant(
                    $"echo tick={lastTick} born={echo.Tick} slot={echo.SourceSlot} tile={echo.TileX},{echo.TileY} sub={echo.SubX},{echo.SubY} height={echo.Height / NativeHeightScale} file={echo.File} frame={echo.Frame} opacity={TrailBuffer.Opacity(age, afterimageCount)} order={(int)echo.SortKey}"));
            }
        }
    }
}
#endif
