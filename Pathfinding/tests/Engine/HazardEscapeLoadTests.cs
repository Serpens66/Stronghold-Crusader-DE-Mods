using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace EngineHarness
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        // Production FireEscapeTick/TryFindFireEscape against the real native anchor
        // corridor and fake UnitAccess boundary (IDs 1..1024). A no-outlet hazard must
        // try every eligible unit, throttle retries and never start a bogus move.
        private void HazardEscapeLoadTests(ref int ticks)
        {
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            var saved = new GameUnit[1025];
            for (int id = 0; id < saved.Length; id++) saved[id] = units.Units[id];
            bool oldActive = fireEscapeActive, oldPlague = plagueEscapeActive, oldSingle = settings.AnchorSingleMoves;
            bool oldInfinite = settings.FireWaitIndefinitely;
            var oldPolicy = fireEscapePolicy; var oldAbort = fireAbortPolicy;
            int now = ticks;
            int Next() { now += 8 - (now & 7); return now; }
            try
            {
                ResetFireEscapes(); ResetReplanWatch(); fireTiles.Clear(); plagueTiles.Clear();
                fireEscapeActive = true; plagueEscapeActive = false;
                fireEscapePolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 2400);
                for (int x = 136; x <= 180; x++) fireTiles.Set(10 * MapWidth + x, true);
                for (int id = 1; id <= 1024; id++) units.Units[id] = default;
                foreach (int count in new[] { 1, 225, 1000 })
                {
                    ResetFireEscapes();
                    for (int id = 1; id <= count; id++)
                    {
                        units.Units[id] = new GameUnit { r_GlobalId = (uint)(700000 + id), r_AliveState = AliveState.IsAlive,
                            r_ControllableForPlayerId = 1, r_SelectionAllowed = 1, r_CurrentTilePositionX = 150, r_CurrentTilePositionY = 10,
                            r_NextTilePositionX2 = 150, r_NextTilePositionY2 = 10, r_TargetTilePositionX2 = 150, r_TargetTilePositionY2 = 10 };
                    }
                    long failures = escapeFailed, start = Stopwatch.GetTimestamp(), beforeBytes = GC.GetAllocatedBytesForCurrentThread();
                    FireEscapeTick(Next());
                    long elapsed = Stopwatch.GetTimestamp() - start, bytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
                    Check(escapeFailed == failures + count && fireEscapes.Count == 0 && fireEscapeRetry.Count == count,
                        "no-outlet load skipped an eligible ID or published a false escape");
                    Check(fireEscapeRetry.ContainsKey(count), "highest eligible ID did not get an escape attempt");
                    long afterFirst = escapeFailed;
                    int firstAt = now;
                    while (now - firstAt < EscapeRetryTicks - EscapeInterval) FireEscapeTick(Next());
                    Check(escapeFailed == afterFirst, "no-outlet load ignored escape retry throttle");
                    FireEscapeTick(Next());
                    Check(escapeFailed == afterFirst + count, "no-outlet load failed to retry all IDs at the deadline");
                    Console.WriteLine($"HAZARD ESCAPE OFFLINE soldiers={count} noOutletAttempts={count} firstTickMs={elapsed*1000.0/Stopwatch.Frequency:F4} allocatedBytes={bytes} retryTicks={EscapeRetryTicks} highIdAttempted=true (CLR fixture; not game tick/FPS)");
                    for (int id = 1; id <= count; id++) units.Units[id] = default;
                }
                ResetFireEscapes(); fireTiles.Clear();
                // Shared-budget pressure experiment: leave one settle per processing
                // pass, as can happen after earlier consumers of the shared budget.
                // It measures service order; no claim that this corridor recreates
                // a full-map 250000-settle game workload is made.
                settings.AnchorSingleMoves = true; settings.FireWaitIndefinitely = true;
                fireAbortPolicy = fireEscapePolicy;
                for (int id = 1; id <= 32; id++)
                {
                    units.Units[id] = new GameUnit { r_GlobalId = (uint)(800000 + id), r_AliveState = AliveState.IsAlive,
                        r_ControllableForPlayerId = 1, r_SelectionAllowed = 1, r_CurrentTilePositionX = 150, r_CurrentTilePositionY = 10,
                        r_NextTilePositionX2 = 150, r_NextTilePositionY2 = 10, r_TargetTilePositionX2 = 150, r_TargetTilePositionY2 = 10 };
                    replanWaits[id] = new ReplanWait { Global = (uint)(800000 + id), Player = 1, StopX = 150, StopY = 10,
                        TargetX = 170, TargetY = 10, Remaining = 20, NextProbe = now, Until = now + 1, Policy = fireAbortPolicy };
                }
                int savedTick = now;
                var savedCases = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                CaptureMovementCases((name, bytes) => savedCases.Add(name, bytes));
                var served = new HashSet<int>(); var serviceOrder = new List<int>();
                for (int pass = 0; pass < 40; pass++)
                {
                    int at = Next(); replanProbeTick = at; replanProbeLeft = 1;
                    ProcessReplanWaits(at);
                    foreach (var entry in replanWaits) if (entry.Value.NextProbe == at + ReplanWaitProbeTicks) { served.Add(entry.Key); serviceOrder.Add(entry.Key); }
                }
                Console.WriteLine($"HAZARD WAIT PRESSURE OFFLINE passes=40 waits=32 remainingSettlesPerPass=1 served={served.Count} highestServed={(served.Count==0?0:System.Linq.Enumerable.Max(served))} highIdServed={served.Contains(32)} (controlled remaining-budget boundary; not full-map game reproduction)");
                Check(replanWaits.Count == 32, "budget-limited indefinite waits expired or issued a move");
                Check(served.Count == 32 && served.Contains(32), "indefinite wait probe budget starves later unit IDs under sustained pressure");
                ResetReplanWatch(); StageMovementCases(savedCases); ApplyLoadedMovementCases(); now = savedTick;
                var restoredOrder = new List<int>();
                for (int pass = 0; pass < 40; pass++)
                {
                    int at = Next(); replanProbeTick = at; replanProbeLeft = 1;
                    ProcessReplanWaits(at);
                    foreach (var entry in replanWaits) if (entry.Value.NextProbe == at + ReplanWaitProbeTicks) restoredOrder.Add(entry.Key);
                }
                Check(System.Linq.Enumerable.SequenceEqual(serviceOrder, restoredOrder), "saved native tick and wait records changed deterministic probe rotation");
            }
            finally
            {
                for (int id = 0; id < saved.Length; id++) units.Units[id] = saved[id];
                ResetFireEscapes(); ResetReplanWatch(); fireTiles.Clear(); plagueTiles.Clear();
                fireEscapeActive = oldActive; plagueEscapeActive = oldPlague; fireEscapePolicy = oldPolicy; fireAbortPolicy = oldAbort;
                settings.AnchorSingleMoves = oldSingle; settings.FireWaitIndefinitely = oldInfinite; ticks = now;
            }
        }
    }
}
