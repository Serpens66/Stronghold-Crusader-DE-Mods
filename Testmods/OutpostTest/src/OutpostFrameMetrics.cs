using System;
using System.Diagnostics;
using System.Reflection;

namespace OutpostTest
{
    // Bounded diagnostics: 60 warmup + 1800 active frames, <=300 samples per category.
    // No allocations while sampling; one aggregate string after the entire window.
    internal sealed class OutpostFrameMetrics
    {
        private readonly Func<long> allocated;
        private readonly int[] counts=new int[3],warmup=new int[3];
        private readonly long[] ticks=new long[3], maxima=new long[3], bytes=new long[3];
        private int frames;
        private long start,allocatedStart;
        private bool sampling;
        internal OutpostFrameMetrics()
        {
            var method=typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread",BindingFlags.Public|BindingFlags.Static,null,Type.EmptyTypes,null);
            if(method!=null) allocated=(Func<long>)Delegate.CreateDelegate(typeof(Func<long>),method);
        }
        internal void Begin()
        {
            sampling=frames>=60 && frames<1860;
            if(frames<1860) frames++;
            if(!sampling)return;
            allocatedStart=allocated!=null?allocated():0;start=Stopwatch.GetTimestamp();
        }
        internal string End(int category)
        {
            if(!sampling)return null;
            sampling=false;
            long elapsed=Stopwatch.GetTimestamp()-start;
            long allocation=allocated!=null?allocated()-allocatedStart:0;
            if(++warmup[category]>30 && counts[category]<300) {
                counts[category]++;ticks[category]+=elapsed;bytes[category]+=allocation;
                if(elapsed>maxima[category])maxima[category]=elapsed;
            }
            if(frames!=1860)return null;
            string report="rally-frame-metrics (microseconds; allocation counter="+(allocated!=null?"available":"unavailable")+")";
            for(int i=0;i<3;i++) report+=$" category={i} samples={counts[i]} meanUs={(counts[i]>0?ticks[i]*1000000.0/Stopwatch.Frequency/counts[i]:0):F3} maxUs={maxima[i]*1000000.0/Stopwatch.Frequency:F3} allocatedBytes={(allocated!=null?bytes[i].ToString():"unknown")}";
            return report+"; categories=0:no-outpost,1:no-target,2:target; window complete";
        }
    }
}