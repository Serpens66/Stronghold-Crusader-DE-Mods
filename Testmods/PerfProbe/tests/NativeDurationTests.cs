using System;
using System.Diagnostics;

namespace PerfProbe
{
    public static class NativeDurationTests
    {
        private static int checks;
        private static void Check(bool value,string reason)
        {checks++;if(!value)throw new InvalidOperationException(reason);}
        public static string Run()
        {
            checks=0;
            var histogram=new PerfProbeNativeDurationHistogram(1000000);
            Check(double.IsNaN(histogram.QuantileUpperMs(.5)),"empty window must not manufacture zero-latency samples");
            Check(!histogram.Add(-1,0,false) && !histogram.Add(1,-2,false) && histogram.Count==0,"invalid times cannot alter counts");
            foreach(long t in new long[]{0,250,1000,50000,50001,100000,100001,1024000,2000000})
                Check(histogram.Add(t,t==1000?-1:2000,t==0),"valid sample rejected");
            Check(histogram.Count==9 && histogram.Skipped==1 && histogram.CpuCount==8,"CPU-unavailable and skip samples must remain distinguishable");
            Check(histogram.Over50==5 && histogram.Over100==3 && histogram.Overflow==2,"strict threshold or overflow boundary changed");
            Check(histogram.Maximum==2000000 && histogram.WallTicks==3325252 && histogram.Cpu100ns==16000,"duration sums/max mismatch");
            Check(histogram.QuantileUpperMs(.5)==50.25 && double.IsPositiveInfinity(histogram.QuantileUpperMs(.95)),"bounded nearest-rank percentile semantics changed");
            var bounded=new PerfProbeNativeDurationHistogram(1000000);
            for(int i=0;i<100000;i++)bounded.Add(i%1000,0,false);
            Check(bounded.Count==100000 && bounded.QuantileUpperMs(.99)==1,"fixed histogram must retain large windows without truncation");
            bool was=PerfProbeRunTiming.Instrumented;
            try
            {
                PerfProbeRunTiming.Instrumented=false;
                Check(!PerfProbeNativeDurations.Begin("missing-seam"),"uninstalled native timing must not claim valid measurement");
                PerfProbeRunTiming.Instrumented=true;
                Check(!PerfProbeNativeDurations.Begin("bad label") && PerfProbeNativeDurations.Begin("empty"),"window label/ownership contract");
                Check(!PerfProbeNativeDurations.Begin("other") && PerfProbeNativeDurations.End("other")==null,"foreign window must not replace or close owner");
                var parent=PerfProbeRunTiming.Begin(false);
                PerfProbeRunTiming.BufferResult(null);
                PerfProbeRunTiming.Complete(parent);
                Check(PerfProbeNativeDurations.End("empty").Contains("nativeCompleted=0"),"no-buffer run is not a completed native tick");
                Check(PerfProbeNativeDurations.Begin("actual-seam"),"second window must open");
                parent=PerfProbeRunTiming.Begin(true);
                PerfProbeRunTiming.BeforeNative();
                // Exercises the actual before/after production seam, no native game call.
                PerfProbeRunTiming.AfterNative();
                PerfProbeRunTiming.Complete(parent);
                string actual=PerfProbeNativeDurations.End("actual-seam");
                Check(actual.Contains("nativeCompleted=1") && actual.Contains("skipCalls=1"),"production seam must record exactly its one completed invocation");
                Check(PerfProbeNativeDurations.Begin("old"),"old window begin");
                var old=PerfProbeNativeDurations.Capture();
                PerfProbeNativeDurations.End("old");
                Check(PerfProbeNativeDurations.Begin("new"),"new window begin");
                old.Record(Stopwatch.Frequency,0,false);
                Check(PerfProbeNativeDurations.End("new").Contains("nativeCompleted=0"),"late completion must never contaminate next window");
            }
            finally
            {
                PerfProbeNativeDurations.End("empty");PerfProbeNativeDurations.End("actual-seam");
                PerfProbeNativeDurations.End("old");PerfProbeNativeDurations.End("new");
                PerfProbeRunTiming.Instrumented=was;
            }
            return "PASS: NativeDurationTests "+checks+" checks (production histogram and run seam; no game invocation).";
        }
    }
}
