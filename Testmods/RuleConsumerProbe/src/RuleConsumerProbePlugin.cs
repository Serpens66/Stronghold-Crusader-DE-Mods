using System;
using System.Collections.Generic;
using System.Diagnostics;
using APIShared;
using APIShared.Pathfinding;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;

namespace RuleConsumerProbe
{
    // Test consumer of APIShared's route rules (RouteTileRules, RouteReplanPolicy, RouteSearchEvents) that
    // uses only the public APIShared assembly, as a third-party mod would. Off by default (StartTicks = 0).
    // From StartTicks simulation ticks after a mission start it sets, for all players:
    // - a Forbidden band x 399..401 over the whole map with a gap at y 380..420 (worst case: long detours);
    // - moving hazard zones: 20 discs of radius 4 with the maximum penalty and a replan policy, moved every
    //   40 ticks to positions derived from a counter (deterministic, like spreading fire);
    // - a RouteSearchEvents observer that makes moat edges 50 times as expensive as ground edges.
    // Every 10 s it logs the time its own rule updates took. Without a route engine the rules have no effect.
    // Lifetime: the OnTick handler and the mission observer are static and rooted by the Extender's and
    // APIShared's process-lived publishers; nothing is removed in a plugin teardown path.
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("000shcdese", "2.14.0")]
    [BepInDependency("APIShared_Serp", "0.18.0")]
    public sealed class RuleConsumerProbePlugin : BaseUnityPlugin
    {
        public const string Guid = "RuleConsumerProbe_Serp", Name = "RuleConsumerProbe", Version = "0.1.0";
        private const int Zones = 20, ZoneRadius = 4, MoveTicks = 40, BandX0 = 399, BandX1 = 401, GapY0 = 380, GapY1 = 420;
        private static ManualLogSource log;
        private static int startTicks;
        private static bool registered, active, started, costActive, costRegistered;
        private static int elapsedTicks, moves;
        private static RouteTileRuleSet band, hazards;
        private static readonly List<int> zoneTiles = new List<int>();
        private static long updateTicks, maxUpdateTicks, nextReport;

        private void Awake()
        {
            if (registered) return;
            registered = true;
            log = Logger;
            startTicks = Config.Bind("Probe", "StartTicks", 0,
                "Simulation ticks after a mission start at which the test rules start; 0 = off. Use the same value on all multiplayer peers.").Value;
            Log("loaded; StartTicks=" + startTicks + (startTicks <= 0 ? " (off)." : "."));
            if (startTicks <= 0) return;
            var api = ApiShared.ForMod(Guid);
            if (!api.TryGetMissionLifecycle(out IMissionLifecycleCapability missions, out var diagnostic) ||
                !missions.TryRegisterObserver("rule-consumer", OnStarted, OnEnded, null, out diagnostic))
            { Log("mission lifecycle unavailable: " + diagnostic.Reason); return; }
            if (!RouteTileRules.TryRegister(Guid, "band", out band, out string reason) ||
                !RouteTileRules.TryRegister(Guid, "hazards", out hazards, out reason))
            { Log("route tile rules unavailable: " + reason); return; }
            hazards.ReplanPolicy = new RouteReplanPolicy(3, 50, 10, 400);
            GameTimeManagerAPI.Instance.OnTick += OnTick;
        }

        private static void OnStarted(MissionLifecycleNotification notification)
        {
            Reset();
            active = true;
            Log("mission started; rules start after " + startTicks + " ticks.");
        }

        private static void OnEnded(MissionLifecycleNotification notification) { Reset(); active = false; }

        private static void Reset()
        {
            band?.ClearAll(); hazards?.ClearAll(); zoneTiles.Clear();
            elapsedTicks = moves = 0; started = costActive = false; updateTicks = maxUpdateTicks = 0;
        }

        // Simulation thread (Extender pre-tick hook), as the rule contract requires.
        private static void OnTick(int tick)
        {
            if (!active || ++elapsedTicks < startTicks) return;
            long begin = Stopwatch.GetTimestamp();
            if (!started)
            {
                started = true; int tiles = 0;
                for (int y = 0; y < RouteTileRules.MapSize; y++)
                {
                    if (y >= GapY0 && y <= GapY1) continue;
                    for (int x = BandX0; x <= BandX1; x++) if (band.Set(x, y, RouteTileEffect.Forbidden)) tiles++;
                }
                if (!costRegistered)
                    costRegistered = RouteSearchEvents.TryRegister(Guid, "moat-cost",
                        e => { if (costActive) e.MoatEdgeCost = Math.Min(e.Context.MaximumEdgeCost, e.GroundEdgeCost * 50); }, null, out _);
                costActive = costRegistered;
                Log("rules set: forbiddenTiles=" + tiles + " moatCostObserver=" + costRegistered + " ms=" + Ms(Stopwatch.GetTimestamp() - begin));
                nextReport = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
            }
            if ((elapsedTicks - startTicks) % MoveTicks == 0) MoveZones();
            long spent = Stopwatch.GetTimestamp() - begin;
            updateTicks += spent; if (spent > maxUpdateTicks) maxUpdateTicks = spent;
            if (Stopwatch.GetTimestamp() >= nextReport)
            {
                Log("updates: zoneMoves=" + moves + " hazardTiles=" + zoneTiles.Count + " ms=" + Ms(updateTicks) + " maxMs=" + Ms(maxUpdateTicks));
                updateTicks = maxUpdateTicks = 0; nextReport = Stopwatch.GetTimestamp() + Stopwatch.Frequency * 10;
            }
        }

        private static void MoveZones()
        {
            foreach (int tile in zoneTiles) hazards.Clear(tile % RouteTileRules.MapSize, tile / RouteTileRules.MapSize);
            zoneTiles.Clear();
            uint seed = (uint)moves * 2654435761u ^ 0x9E3779B9u;
            for (int z = 0; z < Zones; z++)
            {
                seed = seed * 1664525u + 1013904223u; int cx = 40 + (int)(seed % 720u);
                seed = seed * 1664525u + 1013904223u; int cy = 40 + (int)(seed % 720u);
                for (int y = cy - ZoneRadius; y <= cy + ZoneRadius; y++)
                    for (int x = cx - ZoneRadius; x <= cx + ZoneRadius; x++)
                        if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= ZoneRadius * ZoneRadius &&
                            hazards.Set(x, y, RouteTileEffect.Penalty, RouteTileRules.MaximumPenalty))
                            zoneTiles.Add(y * RouteTileRules.MapSize + x);
            }
            moves++;
        }

        private static string Ms(long ticks) => (ticks * 1000.0 / Stopwatch.Frequency).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        private static void Log(string message) => log?.LogInfo("[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] RuleConsumerProbe " + message);
    }
}
