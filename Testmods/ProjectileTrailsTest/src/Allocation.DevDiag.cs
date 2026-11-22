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

        static partial void TraceObservation(int id, Vector3 position, bool changed)
        {
            if (traceVisibility && sampledPositions++ < 12)
                WriteInfo($"sample id={id} position={position} changed={changed} hasSegments={history.HasSegments}");
        }

        static partial void TracePublish(Camera current)
        {
            if (traceVisibility && sampledPublishes++ < 12)
                WriteInfo($"publish camera={current.name} position={current.transform.position} history={history.Count} hasSegments={history.HasSegments} drawn={LastSegmentCount}");
        }
    }
}
#endif
