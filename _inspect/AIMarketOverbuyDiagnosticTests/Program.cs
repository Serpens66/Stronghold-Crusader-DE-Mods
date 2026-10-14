using AIMarketOverbuyDiagnostic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

internal static class Program
{
    // Synthetic layout dimensions; no game assembly or game process is loaded.
    private const int Goods = 25, Gold = 15;
    private static readonly long Frequency = Stopwatch.Frequency;
    private static readonly long Utc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc).Ticks;
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static MarketObservation New() => new MarketObservation(Goods, 1, Gold, Frequency);
    private static void Sample(MarketObservation m, int tick, long clock, uint pending, uint stock, int gold, int phase)
    {
        Assert(m.BeginTick(tick, Utc + tick * TimeSpan.TicksPerMillisecond, clock), "capture inactive");
        m.Player(1, 1, gold, phase);
        m.Good(1, 1, pending, stock, gold, phase);
        m.EndTick(1, clock);
    }
    private static void Drain(MarketObservation m, Action<string> sink) { while (m.DrainOne(sink)) { } }
    private static void LifecycleAndEvidence()
    {
        var m = New(); var lines = new List<string>();
        Assert(!m.Start(1, true, false), "editor observed");
        Assert(m.Start(2, false, true), "late replay incorrectly rejected");
        Sample(m, 100, 0, 5, 10, 1000, 0); Drain(m, lines.Add);
        Sample(m, 101, Frequency, 8, 10, 1000, 1);
        Sample(m, 102, Frequency * 2, 8, 10, 1000, 0);
        Sample(m, 103, Frequency * 3, 8, 10, 1000, 1);
        Sample(m, 104, Frequency * 4, 8, 10, 1000, 0);
        Sample(m, 105, Frequency * 12, 0, 18, 900, 1); Drain(m, lines.Add);
        Sample(m, 106, Frequency * 13, 0, 12, 950, 0);
        m.End(); Drain(m, lines.Add);
        string all = string.Join("\n", lines);
        Assert(all.Contains("AI_MARKET_PENDING_INCREASE_CANDIDATE"), "increase not preserved");
        Assert(all.Contains("AI_MARKET_PENDING_PERSISTENT_CANDIDATE"), "persistent not preserved");
        Assert(all.Contains("AI_MARKET_BUY_SELL_CANDIDATE:104@"), "buy/sell lacks original buy-before");
        Assert(all.Contains("105@2026-10-14T12:00:00.105Z(0,18,900,1)"), "buy-after changed across interval");
        Assert(all.Contains("106@2026-10-14T12:00:00.106Z(0,12,950,0)"), "sell-after absent");
        Assert(all.Contains("final=True"), "partial interval missing");
        Assert(m.Start(3, false, false), "save load start rejected");
        Sample(m, 200, Frequency * 20, 0, 0, 10, 0); Drain(m, lines.Add);
        Sample(m, 1, Frequency * 21, 0, 0, 10, 1); m.End(); Drain(m, lines.Add);
        Assert(string.Join("\n", lines).Contains("session=3, epoch=1"), "tick reset not separated");
        Assert(m.Start(4, false, false), "map change start rejected");
        Sample(m, 1, 0, 100, 100, 100, 1); m.End(); Drain(m, lines.Add);
        Assert(m.SkippedSessions == 0, "unexpected lifecycle loss");
        Assert(m.Start(5, false, false), "failure fixture start rejected");
        Assert(m.BeginTick(1, Utc, 0), "failure fixture tick rejected");
        m.Player(1,1,100,0); m.FailTick(); Drain(m,lines.Add);
        Assert(string.Join("\n",lines).Contains("incompleteTicks=1"), "partial failed tick not marked");
        Console.WriteLine("PASS: late observer, editor, loaded/new sessions, final interval, tick reset, cross-interval buy/sell evidence.");
    }

    private sealed class Reference
    {
        internal bool Valid;
        internal uint FirstP, LastP, MinP, MaxP, FirstS, LastS, MinS, MaxS;
        internal long Pc, Sc;
        internal void Add(uint p, uint s)
        {
            if (!Valid) { FirstP = MinP = MaxP = p; FirstS = MinS = MaxS = s; Valid = true; }
            else { if (p != LastP) Pc++; if (s != LastS) Sc++; MinP = Math.Min(MinP, p); MaxP = Math.Max(MaxP, p); MinS = Math.Min(MinS, s); MaxS = Math.Max(MaxS, s); }
            LastP = p; LastS = s;
        }
    }
    private static long[] ParseRange(string text)
    {
        string[] ends = text.Split('~'); string[] values = ends[0].Split('>');
        long first = long.Parse(values[0], CultureInfo.InvariantCulture), last = values.Length == 1 ? first : long.Parse(values[1], CultureInfo.InvariantCulture);
        string[] extremes = ends.Length == 1 ? null : ends[1].Split(':');
        return new[] { first, last, extremes == null || extremes[0].Length == 0 ? Math.Min(first,last) : long.Parse(extremes[0], CultureInfo.InvariantCulture),
            extremes == null || extremes[1].Length == 0 ? Math.Max(first,last) : long.Parse(extremes[1], CultureInfo.InvariantCulture) };
    }
    private static void ReferenceTrace()
    {
        var m = New(); m.Start(10, false, false);
        var reference = new Reference[9 * Goods]; var observed = new Reference[reference.Length];
        for (int i = 0; i < reference.Length; i++) { reference[i] = new Reference(); observed[i] = new Reference(); }
        var random = new Random(4219);
        Action<string> sink = line =>
        {
            if (!line.StartsWith("AI_MARKET_SUMMARY:", StringComparison.Ordinal)) return;
            int player = int.Parse(Regex.Match(line, @"player=(\d+)").Groups[1].Value, CultureInfo.InvariantCulture);
            string body = Regex.Match(line, @"goods=\[(.*)\], source").Groups[1].Value;
            body = Regex.Replace(body, @"\{[^}]*\}", "");
            foreach (string row in body.Split(';'))
            {
                if (row.Length == 0) continue;
                int colon = row.IndexOf(':'); int good = int.Parse(row.Substring(0, colon), CultureInfo.InvariantCulture);
                string[] parts = row.Substring(colon+1).Split('/'); long[] p = ParseRange(parts[0]), s = ParseRange(parts[1]);
                string[] changes = parts[2].Split(','); Reference target = observed[player * Goods + good];
                if (!target.Valid) { target.FirstP=(uint)p[0]; target.FirstS=(uint)s[0]; target.MinP=(uint)p[2]; target.MinS=(uint)s[2]; target.MaxP=(uint)p[3]; target.MaxS=(uint)s[3]; target.Valid=true; }
                target.LastP=(uint)p[1]; target.LastS=(uint)s[1]; target.MinP=Math.Min(target.MinP,(uint)p[2]); target.MaxP=Math.Max(target.MaxP,(uint)p[3]);
                target.MinS=Math.Min(target.MinS,(uint)s[2]); target.MaxS=Math.Max(target.MaxS,(uint)s[3]);
                target.Pc+=long.Parse(changes[0],CultureInfo.InvariantCulture);
                target.Sc+=long.Parse(changes[1]=="="?changes[0]:changes[1],CultureInfo.InvariantCulture);
            }
        };
        for (int tick = 0; tick < 20000; tick++)
        {
            long clock = tick * Frequency / 40;
            m.BeginTick(tick, Utc + tick * 250000L, clock);
            for (int player = 1; player <= 8; player++)
            {
                m.Player(player, player, 1000, tick % 2);
                for (int good = 1; good < Goods; good++)
                {
                    if (good == Gold) continue;
                    uint p = (uint)random.Next(0,300), s=(uint)random.Next(0,600);
                    reference[player*Goods+good].Add(p,s);
                    m.Good(player,good,p,s,1000,tick%2);
                }
            }
            m.EndTick(1,clock); Drain(m,sink);
        }
        m.End(); Drain(m,sink);
        for(int i=0;i<reference.Length;i++)
        {
            Reference a=reference[i],b=observed[i]; if(!a.Valid) continue;
            Assert(b.Valid && a.FirstP==b.FirstP && a.LastP==b.LastP && a.MinP==b.MinP && a.MaxP==b.MaxP && a.Pc==b.Pc &&
                a.FirstS==b.FirstS && a.LastS==b.LastS && a.MinS==b.MinS && a.MaxS==b.MaxS && a.Sc==b.Sc,"reference mismatch at "+i);
        }
        Console.WriteLine("PASS: 3,680,000 independently recorded samples; all ranges/endpoints/change totals match emitted summaries.");
    }

    private static void Backpressure()
    {
        var m=New(); m.Start(20,false,false);
        for(int tick=0;tick<100;tick++) Sample(m,tick,tick*Frequency*11,(uint)tick,100,100,0);
        var lines=new List<string>(); bool failed=false;
        try { m.DrainOne(_=>throw new InvalidOperationException("simulated disk failure")); } catch(InvalidOperationException){failed=true;}
        Assert(failed,"sink failure not exercised");
        Drain(m,lines.Add);
        Sample(m,100,Frequency*1100,100,100,100,1); m.End(); Drain(m,lines.Add);
        string all=string.Join("\n",lines);
        Assert(all.Contains("extended=") && Regex.IsMatch(all,@"extended=[1-9]"),"backlog extension missing");
        Assert(Regex.IsMatch(all,@"goods=\[1:(?:\d+>)?100[~/]"),"last retained state absent: "+all);
        long ticks=0; foreach(string line in lines) if(line.StartsWith("AI_MARKET_INTERVAL:",StringComparison.Ordinal)) ticks+=long.Parse(Regex.Match(line,@"ticks=(\d+)").Groups[1].Value,CultureInfo.InvariantCulture);
        Assert(ticks==101,"backpressure lost or duplicated ticks");
        int written=0; var partial=New(); partial.Start(21,false,false); Sample(partial,0,0,1,1,1,0);
        Action<string> failSecond=line=>{ if(written==1){written++;throw new InvalidOperationException();} written++; };
        try{partial.DrainOne(failSecond);}catch(InvalidOperationException){}
        int retryLines=0; Drain(partial,_=>retryLines++); Assert(retryLines==1,"already written header repeated after retry");

        // Real consumer blocks inside the sink; producer must continue without taking its lock.
        var concurrent=New(); concurrent.Start(22,false,false); Sample(concurrent,0,0,1,1,1,0);
        using(var entered=new ManualResetEventSlim()) using(var release=new ManualResetEventSlim())
        {
            var thread=new Thread(()=>concurrent.DrainOne(_=>{entered.Set();release.Wait();})); thread.Start();
            Assert(entered.Wait(2000),"writer did not enter sink");
            var watch=Stopwatch.StartNew(); for(int i=1;i<=10000;i++) Sample(concurrent,i,i*Frequency/40,(uint)i,10,100,0);
            Assert(watch.ElapsedMilliseconds<2000,"producer waited for blocked writer");
            release.Set(); Assert(thread.Join(2000),"consumer did not resume");
        }
        var boundaries=New();
        for(int i=0;i<MarketObservation.BufferCount;i++){Assert(boundaries.Start(i,false,false),"premature boundary exhaustion");boundaries.End();}
        Assert(!boundaries.Start(100,false,false) && boundaries.SkippedSessions==1,"full boundary not reported");
        Drain(boundaries,_=>{}); Assert(boundaries.Start(101,false,false),"capture did not recover after boundary backlog");
        Console.WriteLine("PASS: failed sink retries, stalled writer, retained ticks, extended windows and explicit exhausted-session reporting.");
    }

    private static void EightHours()
    {
        long beforeConstruction=GC.GetAllocatedBytesForCurrentThread();
        var m=New(); long fixedBytes=GC.GetAllocatedBytesForCurrentThread()-beforeConstruction; m.Start(30,false,false);
        long bytes=2000, summaries=0, allocated=0; var costs=new long[120000];
        var candidates=new long[4]; long retainedExamples=0;
        Action<string> sink=line=>
        {
            bytes+=Encoding.UTF8.GetByteCount(line)+100;
            if(!line.StartsWith("AI_MARKET_SUMMARY:",StringComparison.Ordinal))return;
            summaries++;
            retainedExamples+=Regex.Matches(line,"_CANDIDATE:").Count;
            string body=Regex.Match(line,@"goods=\[(.*)\], source").Groups[1].Value;
            body=Regex.Replace(body,@"\{[^}]*\}","");
            foreach(string row in body.Split(';'))
            {
                string[] columns=row.Split('/'); var counts=new long[4];
                for(int i=3;i<columns.Length;i++)
                {
                    string[] pair=columns[i].Split('='); int kind=int.Parse(pair[0],CultureInfo.InvariantCulture);
                    long count;
                    if(pair[1][0]=='^') count=counts[int.Parse(pair[1].Substring(1),CultureInfo.InvariantCulture)];
                    else
                    {
                        count=0; foreach(char digit in pair[1].Substring(0,pair[1].IndexOf('@')))
                            count=checked(count*36+(digit<='9'?digit-'0':digit-'a'+10));
                    }
                    counts[kind]=count;candidates[kind]+=count;
                }
            }
        };
        int total=8*60*60*40;
        var watch=Stopwatch.StartNew();
        for(int tick=0;tick<total;tick++)
        {
            long before=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
            long clock=tick*Frequency/40; uint pending=(uint)(20-(tick%2)*10),stock=(uint)(100+tick%2); int gold=10000-(tick%2)*100;
            m.BeginTick(tick,Utc+tick*250000L,clock);
            for(int player=1;player<=8;player++)
            {
                m.Player(player,player,gold,tick%2);
                for(int good=1;good<Goods;good++)if(good!=Gold)m.Good(player,good,pending,stock,gold,tick%2);
            }
            m.EndTick(Stopwatch.GetTimestamp()-start,clock);
            long cost=Stopwatch.GetTimestamp()-start;
            if(tick>=10000 && tick<130000) costs[tick-10000]=cost;
            if(tick>10000) allocated+=GC.GetAllocatedBytesForCurrentThread()-before;
            Drain(m,sink);
        }
        m.End();Drain(m,sink);Array.Sort(costs);
        double p99=costs[(int)(costs.Length*0.99)]*1000.0/Frequency;
        long goodsPlayers=8*23;
        Assert(candidates[0]==(total/2-1)*goodsPlayers && candidates[1]==goodsPlayers &&
            candidates[2]==total/2*goodsPlayers && candidates[3]==(total/2-1)*goodsPlayers,"compressed candidate counts differ from closed-form reference");
        Assert(retainedExamples==goodsPlayers*4,"first examples lost or repeatedly logged");
        Assert(fixedBytes<8*1024*1024,"capture buffer memory budget exceeded");
        Console.WriteLine("METRICS: 8h at 40 ticks/s, 8 players, all 23 goods changing each tick; bytes="+bytes+", MiB="+(bytes/1048576.0).ToString("F2",CultureInfo.InvariantCulture)+", summaries="+summaries+", fixedCaptureBytes="+fixedBytes+", allocatedCaptureBytes="+allocated+", p99Ms="+p99.ToString("F4",CultureInfo.InvariantCulture)+", testSeconds="+watch.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture));
        Assert(allocated==0,"ordinary capture allocated"); Assert(p99<0.5,"p99 capture budget exceeded"); Assert(bytes<=32L*1024*1024,"8h log budget exceeded");
        Console.WriteLine("PASS: log volume, fixed capture buffers, zero capture allocations, p99 performance.");
    }
    private static int Main()
    {
        try { LifecycleAndEvidence(); ReferenceTrace(); Backpressure(); EightHours(); return 0; }
        catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
