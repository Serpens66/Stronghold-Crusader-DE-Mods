using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AIMarketOverbuyDiagnostic
{
    // Pure numeric capture. Only the producer accesses observations/current; the consumer
    // owns published batches until it advances consumed. No game pointers cross this boundary.
    internal sealed class MarketObservation
    {
        internal const int Players = 8;
        internal const int BufferCount = 8;
        private const int PersistentTransitions = 4;
        private const int BuyWindow = 500;
        private readonly int goodsCount, firstGood, goldId;
        private readonly Observation[] previous;
        private readonly PlayerStats[] lastPlayer = new PlayerStats[Players + 1];
        private readonly Batch[] buffers = new Batch[BufferCount];
        private long published, consumed;
        private Batch current;
        private long session;
        private int epoch;
        private int lastTick = -1;
        private long nextDue;
        private readonly long interval;
        private bool firstBatch;
        private long lastElapsed;
        internal long WriterFailures = 0;
        internal long SkippedSessions;

        internal MarketObservation(int goodsCount, int firstGood, int goldId, long frequency)
        {
            this.goodsCount = goodsCount;
            this.firstGood = firstGood;
            this.goldId = goldId;
            interval = checked(frequency * 10);
            previous = new Observation[(Players + 1) * goodsCount];
            for (int i = 0; i < buffers.Length; i++) buffers[i] = new Batch(previous.Length);
        }

        internal bool Start(long id, bool editor, bool replayNotification)
        {
            End();
            // Replay is a delivery flag, never a reason to reject a live session.
            if (editor) return false;
            if (published - Volatile.Read(ref consumed) >= BufferCount)
            {
                Interlocked.Increment(ref SkippedSessions);
                return false; // Explicitly reported by the writer; never overwrite retained data.
            }
            session = id;
            epoch = 0;
            lastTick = -1;
            firstBatch = true;
            Array.Clear(previous, 0, previous.Length);
            Array.Clear(lastPlayer, 0, lastPlayer.Length);
            current = buffers[(int)(published % BufferCount)];
            current.Clear(session, epoch);
            return true;
        }

        internal bool BeginTick(int tick, long utcTicks, long clock)
        {
            if (current == null) return false;
            if (lastTick >= 0 && tick < lastTick)
            {
                // Keep the pre-reset interval intact. If the writer is blocked, stop
                // this session rather than mixing two incompatible tick timelines.
                Publish(true);
                if (published - Volatile.Read(ref consumed) >= BufferCount)
                {
                    Interlocked.Increment(ref SkippedSessions);
                    return false;
                }
                epoch++;
                for (int i = 0; i < previous.Length; i++)
                {
                    int examples = previous[i].Examples;
                    previous[i] = default;
                    previous[i].Examples = examples; // First examples are per session, not per tick epoch.
                }
                Array.Clear(lastPlayer, 0, lastPlayer.Length);
                current = buffers[(int)(published % BufferCount)];
                current.Clear(session, epoch);
                firstBatch = true;
            }
            lastTick = tick;
            if (current.Ticks == 0)
            {
                current.FirstTick = tick;
                current.FirstUtc = utcTicks;
                current.FirstClock = clock;
                nextDue = clock + interval;
            }
            current.LastTick = tick;
            current.LastUtc = utcTicks;
            current.LastClock = clock;
            current.Ticks++;
            return true;
        }

        internal void Player(int player, int lord, int gold, int phase)
        {
            ref PlayerStats p = ref current.Player[player];
            if (!p.Gold.Valid && lastPlayer[player].Gold.Valid)
            {
                p.Gold.Add(lastPlayer[player].Gold.Last);
                p.Phase.Add(lastPlayer[player].Phase.Last);
            }
            p.Lord = lord;
            p.Gold.Add(gold);
            p.Phase.Add(phase);
            lastPlayer[player] = p;
        }

        internal void Good(int player, int good, uint pending, uint stock, int gold, int phase)
        {
            int index = player * goodsCount + good;
            ref Observation old = ref previous[index];
            ref GoodStats stats = ref current.Goods[index];
            var value = new Value { Tick = lastTick, Utc = current.LastUtc,
                Pending = pending, Stock = stock, Gold = gold, Phase = phase };
            if (!stats.Pending.Valid && old.Valid)
            {
                stats.Pending.Add(old.Value.Pending);
                stats.Stock.Add(old.Value.Stock);
            }
            stats.Pending.Add(pending);
            stats.Stock.Add(stock);
            if (old.Valid)
            {
                // Include the boundary transition in exactly one interval.
                if (pending != old.Value.Pending) stats.PendingChanges++;
                if (stock != old.Value.Stock) stats.StockChanges++;
                long stockDelta = (long)stock - old.Value.Stock;
                long goldDelta = (long)gold - old.Value.Gold;
                if (pending > old.Value.Pending && old.Value.Pending > 0 && stockDelta <= 0)
                    Candidate(ref old, ref stats, 0, value);
                if (pending > 0)
                {
                    if (old.Value.Pending == 0)
                    {
                        old.PendingSince = lastTick;
                        old.Transitions = 0;
                        old.Persistent = false;
                    }
                    if (old.Value.Phase != phase && ++old.Transitions >= PersistentTransitions && !old.Persistent)
                    {
                        old.Persistent = true;
                        Candidate(ref old, ref stats, 1, value);
                    }
                }
                else { old.Transitions = 0; old.Persistent = false; old.PendingSince = -1; }
                if (stockDelta > 0 && goldDelta < 0 && (old.Value.Pending > 0 || pending > 0))
                {
                    Candidate(ref old, ref stats, 2, value);
                    old.Buy = new Evidence { Before = old.Value, After = value };
                    old.HasBuy = true;
                }
                else if (stockDelta < 0 && goldDelta > 0 && old.HasBuy &&
                    (long)lastTick - old.Buy.After.Tick <= BuyWindow)
                {
                    Candidate(ref old, ref stats, 3, value);
                    old.HasBuy = false;
                }
            }
            else
            {
                old.PendingSince = pending > 0 ? lastTick : -1;
                old.Valid = true;
            }
            old.Value = value;
        }

        private void Candidate(ref Observation old, ref GoodStats stats, int kind, Value value)
        {
            stats.Events.Add(kind, lastTick);
            int mask = 1 << kind;
            if ((old.Examples & mask) != 0) return;
            old.Examples |= mask;
            var evidence = new Evidence { Before = old.Value, After = value,
                BuyBefore = old.Buy.Before, BuyAfter = old.Buy.After, Since = old.PendingSince };
            stats.Examples.Set(kind, evidence);
            stats.ExampleMask |= mask;
        }

        internal void EndTick(long elapsed, long clock)
        {
            if (current == null) return;
            current.CaptureTicks += elapsed;
            lastElapsed = elapsed;
            if (elapsed > current.MaxCaptureTicks) current.MaxCaptureTicks = elapsed;
            if (firstBatch)
            {
                current.Start = true;
                if (Publish(false)) firstBatch = false;
            }
            else if (clock >= nextDue)
            {
                if (!Publish(false))
                {
                    current.Extended++;
                    nextDue = clock + interval;
                }
            }
        }

        internal void End()
        {
            if (current != null) Publish(true);
        }

        internal void FailTick()
        {
            if (current != null) current.IncompleteTicks++;
            End();
        }

        private bool Publish(bool final)
        {
            if (current == null) return true;
            if (!final && published - Volatile.Read(ref consumed) >= BufferCount - 1) return false;
            Batch next = null;
            if (!final)
            {
                long started = Stopwatch.GetTimestamp();
                next = buffers[(int)((published + 1) % BufferCount)];
                next.Clear(session, epoch);
                long overhead = Stopwatch.GetTimestamp() - started;
                current.CaptureTicks += overhead;
                if (lastElapsed + overhead > current.MaxCaptureTicks) current.MaxCaptureTicks = lastElapsed + overhead;
            }
            current.Final = final;
            Volatile.Write(ref published, published + 1);
            current = next;
            return true;
        }

        // Called only by the background writer. Progress survives a failed sink call,
        // so already written player summaries are not repeated on retry.
        internal bool DrainOne(Action<string> sink)
        {
            long read = consumed;
            if (read >= Volatile.Read(ref published)) return false;
            Batch batch = buffers[(int)(read % BufferCount)];
            if (!batch.HeaderWritten)
            {
                sink(Header(batch));
                batch.HeaderWritten = true;
            }
            while (batch.NextPlayer <= Players)
            {
                int player = batch.NextPlayer;
                if (batch.Player[player].Gold.Valid) sink(FormatPlayer(batch, player));
                batch.NextPlayer++;
            }
            Volatile.Write(ref consumed, read + 1);
            return true;
        }

        private string Header(Batch b)
        {
            return "AI_MARKET_INTERVAL: session=" + b.Session + ", epoch=" + b.Epoch +
                ", tick=" + b.FirstTick + ".." + b.LastTick + ", utc=" + Utc(b.FirstUtc) + ".." + Utc(b.LastUtc) +
                ", ticks=" + b.Ticks + ", incompleteTicks=" + b.IncompleteTicks + ", elapsedMs=" + ((b.LastClock - b.FirstClock) * 1000.0 / Stopwatch.Frequency).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                ", captureUs=" + (b.CaptureTicks * 1000000.0 / Stopwatch.Frequency).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                ", maxCaptureUs=" + (b.MaxCaptureTicks * 1000000.0 / Stopwatch.Frequency).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                ", extended=" + b.Extended + ", writerFailures=" + Interlocked.Read(ref WriterFailures) +
                ", skippedSessions=" + Interlocked.Read(ref SkippedSessions) + ", start=" + b.Start + ", final=" + b.Final +
                (b.Start ? ", AI_MARKET_POST_STARTUP_TICK; source=GameTimeManagerAPI.OnTick" : "") + ".";
        }

        private string FormatPlayer(Batch b, int player)
        {
            PlayerStats p = b.Player[player];
            var text = new StringBuilder(1024);
            text.Append("AI_MARKET_SUMMARY: session=").Append(b.Session).Append(", epoch=").Append(b.Epoch)
                .Append(", tick=").Append(b.FirstTick).Append("..").Append(b.LastTick)
                .Append(", player=").Append(player).Append(", lord=").Append(p.Lord).Append(", gold=");
            Range(text, p.Gold); text.Append(", phase="); Range(text, p.Phase);
            text.Append(", changes=").Append(p.Gold.Changes).Append(',').Append(p.Phase.Changes);
            text.Append(", goods=[");
            bool separator = false;
            for (int good = firstGood; good < goodsCount; good++)
            {
                if (good == goldId) continue;
                GoodStats g = b.Goods[player * goodsCount + good];
                if (!g.Pending.Valid) continue;
                if (separator) text.Append(';'); separator = true;
                text.Append(good).Append(':'); Range(text, g.Pending);
                text.Append('/'); Range(text, g.Stock);
                text.Append('/').Append(g.PendingChanges).Append(',');
                if (g.StockChanges == g.PendingChanges) text.Append('=');
                else text.Append(g.StockChanges);
                for (int kind = 0; kind < 4; kind++)
                {
                    EventCount count = g.Events.Get(kind);
                    if (count.Count != 0)
                    {
                        text.Append('/').Append(kind).Append('=');
                        int alias = -1;
                        for (int earlier = 0; earlier < kind; earlier++)
                        {
                            EventCount other = g.Events.Get(earlier);
                            if (count.Count == other.Count && count.First == other.First && count.Last == other.Last)
                            { alias = earlier; break; }
                        }
                        if (alias >= 0) text.Append('^').Append(alias);
                        else
                        {
                            Base36(text, count.Count); text.Append('@');
                            Base36(text, count.First - b.FirstTick); text.Append(':'); Base36(text, count.Last - b.FirstTick);
                        }
                    }
                    if ((g.ExampleMask & (1 << kind)) != 0)
                    {
                        Evidence e = g.Examples.Get(kind);
                        text.Append("{").Append(Markers[kind]).Append(":");
                        if (kind == 3) { ValueText(text, e.BuyBefore); text.Append('>'); ValueText(text, e.BuyAfter); text.Append('|'); }
                        ValueText(text, e.Before); text.Append('>'); ValueText(text, e.After);
                        text.Append(",since=").Append(e.Since).Append('}');
                    }
                }
            }
            return text.Append("], sourceUnconfirmed=true.").ToString();
        }

        private static readonly string[] Markers = { "AI_MARKET_PENDING_INCREASE_CANDIDATE",
            "AI_MARKET_PENDING_PERSISTENT_CANDIDATE", "AI_MARKET_BUY_CANDIDATE", "AI_MARKET_BUY_SELL_CANDIDATE" };
        internal static string Utc(long ticks) => ticks == 0 ? "none" : new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        private static void Range(StringBuilder b, NumericRange r)
        {
            b.Append(r.First);
            if (r.First != r.Last) b.Append('>').Append(r.Last);
            // Interior extrema are written only when they differ from the endpoints.
            if (r.Min != Math.Min(r.First, r.Last) || r.Max != Math.Max(r.First, r.Last))
            {
                b.Append('~');
                if (r.Min != Math.Min(r.First, r.Last)) b.Append(r.Min);
                b.Append(':');
                if (r.Max != Math.Max(r.First, r.Last)) b.Append(r.Max);
            }
        }
        private static void Base36(StringBuilder b, long value)
        {
            if (value >= 36) Base36(b, value / 36);
            b.Append("0123456789abcdefghijklmnopqrstuvwxyz"[(int)(value % 36)]);
        }
        private static void ValueText(StringBuilder b, Value v) => b.Append(v.Tick).Append('@').Append(Utc(v.Utc))
            .Append('(').Append(v.Pending).Append(',').Append(v.Stock).Append(',').Append(v.Gold).Append(',').Append(v.Phase).Append(')');

        internal struct NumericRange
        {
            internal bool Valid;
            internal long First, Last, Min, Max, Changes;
            internal void Add(long value)
            {
                if (!Valid) { First = Min = Max = value; Valid = true; }
                else { if (value != Last) Changes++; if (value < Min) Min = value; if (value > Max) Max = value; }
                Last = value;
            }
        }
        internal struct Value { internal int Tick, Gold, Phase; internal uint Pending, Stock; internal long Utc; }
        internal struct Evidence { internal Value Before, After, BuyBefore, BuyAfter; internal int Since; }
        internal struct EventCount { internal long Count; internal int First, Last; internal void Add(int tick) { if (Count++ == 0) First = tick; Last = tick; } }
        internal struct EventSet
        {
            internal EventCount A, B, C, D;
            internal void Add(int kind, int tick) { switch (kind) { case 0: A.Add(tick); break; case 1: B.Add(tick); break; case 2: C.Add(tick); break; default: D.Add(tick); break; } }
            internal EventCount Get(int kind) => kind == 0 ? A : kind == 1 ? B : kind == 2 ? C : D;
        }
        internal struct ExampleSet
        {
            internal Evidence A, B, C, D;
            internal void Set(int kind, Evidence value) { switch (kind) { case 0: A = value; break; case 1: B = value; break; case 2: C = value; break; default: D = value; break; } }
            internal Evidence Get(int kind) => kind == 0 ? A : kind == 1 ? B : kind == 2 ? C : D;
        }
        internal struct GoodStats { internal NumericRange Pending, Stock; internal long PendingChanges, StockChanges; internal EventSet Events; internal ExampleSet Examples; internal int ExampleMask; }
        private struct PlayerStats { internal int Lord; internal NumericRange Gold, Phase; }
        private struct Observation { internal bool Valid, Persistent, HasBuy; internal int PendingSince, Transitions, Examples; internal Value Value; internal Evidence Buy; }
        private sealed class Batch
        {
            internal readonly PlayerStats[] Player = new PlayerStats[Players + 1];
            internal readonly GoodStats[] Goods;
            internal long Session, FirstUtc, LastUtc, FirstClock, LastClock, Ticks, IncompleteTicks, CaptureTicks, MaxCaptureTicks, Extended;
            internal int Epoch, FirstTick, LastTick, NextPlayer;
            internal bool Start, Final, HeaderWritten;
            internal Batch(int count) { Goods = new GoodStats[count]; }
            internal void Clear(long session, int epoch)
            {
                Array.Clear(Player, 0, Player.Length); Array.Clear(Goods, 0, Goods.Length);
                Session = session; Epoch = epoch; FirstUtc = LastUtc = FirstClock = LastClock = Ticks = IncompleteTicks = CaptureTicks = MaxCaptureTicks = Extended = 0;
                FirstTick = LastTick = 0; NextPlayer = 1; Start = Final = HeaderWritten = false;
            }
        }
    }
}
