using System;
using System.Globalization;
using System.Threading;

namespace PerfProbe
{
    // Diagnostic-only, explicit windows; no launcher, native owner or game state access.
    // Reflection callers may start/end under the existing engine lock while paused.
    public static class PerfProbeNativeDurations
    {
        internal sealed class Window
        {
            internal readonly string Label;
            private readonly object gate=new object();
            private readonly PerfProbeNativeDurationHistogram histogram=new PerfProbeNativeDurationHistogram(System.Diagnostics.Stopwatch.Frequency);
            private bool closed;
            internal Window(string label){Label=label;}
            internal void Record(long elapsed,long cpu,bool skip)
            {lock(gate){if(!closed)histogram.Add(elapsed,cpu,skip);}}
            internal string Close(){lock(gate){closed=true;return histogram.Describe(Label);}}
        }
        private static Window active;
        public static bool Begin(string label)
        {
            if(string.IsNullOrEmpty(label) || label.Length>64)return false;
            foreach(char c in label)if(!char.IsLetterOrDigit(c) && c!='-' && c!='_')return false;
            if(!PerfProbeRunTiming.Instrumented || Volatile.Read(ref active)!=null)return false;
            return Interlocked.CompareExchange(ref active,new Window(label),null)==null;
        }
        public static string End(string label)
        {
            var window=Volatile.Read(ref active);
            if(window==null || !string.Equals(label,window.Label,StringComparison.Ordinal) ||
                Interlocked.CompareExchange(ref active,null,window)!=window)return null;
            string result=window.Close();
            Shared.DevDiag.Log(()=>result);
            return result;
        }
        internal static Window Capture()=>Volatile.Read(ref active);
    }

    // Fixed 0.25ms upper-bound buckets to 1024ms, then an explicit overflow bucket.
    // Quantiles are bucket bounds, not exact observed percentiles. One fixed array per
    // requested window; Add allocates nothing and is called only by that window's gate.
    internal sealed class PerfProbeNativeDurationHistogram
    {
        internal const int Buckets=4096;
        private readonly long frequency;
        private readonly long[] bins=new long[Buckets+1];
        internal long Count,Skipped,CpuCount,Over50,Over100,Overflow,Maximum;
        internal double WallTicks,Cpu100ns;
        internal PerfProbeNativeDurationHistogram(long frequency)
        {if(frequency<=0)throw new ArgumentOutOfRangeException(nameof(frequency));this.frequency=frequency;}
        internal bool Add(long elapsed,long cpu,bool skip)
        {
            if(elapsed<0 || cpu < -1)return false;
            double scaled=elapsed/(double)frequency*4000.0;
            int bucket=scaled>=Buckets?Buckets:(int)Math.Floor(scaled);
            bins[bucket]++;Count++;WallTicks+=elapsed;Maximum=Math.Max(Maximum,elapsed);
            if(skip)Skipped++;
            if(cpu>=0){CpuCount++;Cpu100ns+=cpu;}
            if(elapsed/(double)frequency>0.05)Over50++;
            if(elapsed/(double)frequency>0.1)Over100++;
            if(bucket==Buckets)Overflow++;
            return true;
        }
        internal double QuantileUpperMs(double fraction)
        {
            if(Count==0)return double.NaN;
            if(fraction<=0 || fraction>1 || double.IsNaN(fraction))throw new ArgumentOutOfRangeException(nameof(fraction));
            long rank=(long)Math.Ceiling(Count*fraction),seen=0;
            for(int i=0;i<bins.Length;i++)if((seen+=bins[i])>=rank)
                return i==Buckets?double.PositiveInfinity:(i+1)*0.25;
            throw new InvalidOperationException("Native duration histogram count mismatch.");
        }
        internal string Describe(string label)=>string.Format(CultureInfo.InvariantCulture,
            "nativeDurationWindow label={0} nativeCompleted={1} skipCalls={2} wallTotalMs={3:F3} wallMeanMs={4:F3} maxMs={5:F3} "+
            "p50UpperMs={6:F3} p95UpperMs={7:F3} p99UpperMs={8:F3} over50Ms={9} over100Ms={10} overflowGE1024Ms={11} "+
            "cpuAvailableCalls={12} cpuTotalMs={13:F3} cpuMeanMs={14:F3} bucketWidthMs=0.25 advancingTicks=external",
            label,Count,Skipped,WallTicks*1000/frequency,Count==0?double.NaN:WallTicks*1000/frequency/Count,
            Maximum*1000.0/frequency,QuantileUpperMs(.50),QuantileUpperMs(.95),QuantileUpperMs(.99),Over50,Over100,Overflow,
            CpuCount,Cpu100ns/10000,CpuCount==0?double.NaN:Cpu100ns/10000/CpuCount);
    }
}
