using APIShared;
using APIShared.Automation;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnemyBridgePathTest
{
    // Opt-in in-game acceptance, started only by an owned SerpAutomation request
    // ("bridge-<save file>"). It loads the save, then compares feature ON / OFF / ON within
    // one process: closed decks and portal endpoints, per-player region answers of the
    // native query 0xE2610, and real MoveHere orders of spawned hostile soldiers.
    internal static class BridgeAcceptance
    {
        private const string Guid = "EnemyBridgePathTest_Serp", Prefix = "bridge-";
        private const int SampleChunks = 18, TicksPerChunk = 40, SoldierCount = 6;
        private static ManualLogSource log;
        private static DrawbridgeDeckRuntime runtime;
        private static int passed, failed;
        private static List<DrawbridgeDeckRuntime.DeckReport> decks = new List<DrawbridgeDeckRuntime.DeckReport>();
        private static readonly List<int> soldiers = new List<int>();
        private static int enemy, onDeckVisits, samples;
        // Set by an owned "ai-off"/"perf-off" request before loading, so that the map's first
        // component rebuild already runs in the requested mode.
        internal static bool? ForcedEnabled { get; private set; }

        internal static void TryStart(ManualLogSource logger, DrawbridgeDeckRuntime deckRuntime)
        {
            string request = Path.Combine(Paths.ConfigPath, "SerpAutomation", Guid + ".run");
            if (!File.Exists(request)) return;
            string[] lines = File.ReadAllLines(request);
            if (lines.Length < 2 || !lines[1].Trim().StartsWith(Prefix, StringComparison.Ordinal)) return;
            log = logger; runtime = deckRuntime;
            if (!System.Guid.TryParseExact(lines[0].Trim(), "N", out var run) || run == System.Guid.Empty) { Stamp("Invalid owned request; no run."); return; }
            string save = lines[1].Trim().Substring(Prefix.Length);
            if (save.StartsWith("ai-off-", StringComparison.Ordinal) || save.StartsWith("perf-off-", StringComparison.Ordinal)) ForcedEnabled = false;
            else if (save.StartsWith("ai-on-", StringComparison.Ordinal) || save.StartsWith("perf-on-", StringComparison.Ordinal)) ForcedEnabled = true;
            if (ForcedEnabled.HasValue) runtime.Enabled = ForcedEnabled.Value;
            File.Delete(request); // only our own scenario is consumed
            var api = ApiShared.ForMod(Guid);
            api.WhenReady(_ =>
            {
                if (!api.Ui.TryPost(() => api.Automation.Start(run, Steps(save), log,
                    result => Stamp("SUMMARY status=" + result.Status + " passed=" + passed + " failed=" + failed + " detail=" + result.Detail + " :: " + runtime.Summary()),
                    quitWhenDone: true)))
                    Stamp("Unable to dispatch the run to Unity.");
            });
        }

        private static IEnumerable<AutomationStep> Steps(string scenario)
        {
            // "ai-on-<save>"/"ai-off-<save>": watch real AI decisions with gates held open.
            // "perf-on-<save>"/"perf-off-<save>": tick rate and hook cost under load.
            foreach (string kind in new[] { "ai", "perf" })
                foreach (bool on in new[] { true, false })
                {
                    string prefix = kind + (on ? "-on-" : "-off-");
                    if (scenario.StartsWith(prefix, StringComparison.Ordinal))
                        return kind == "ai" ? AiSteps(scenario.Substring(prefix.Length), on) : PerfSteps(scenario.Substring(prefix.Length), on);
                }
            return AcceptanceSteps(scenario);
        }

        private static IEnumerable<AutomationStep> AiSteps(string save, bool on)
        {
            yield return new AutomationStep("load", TimeSpan.FromSeconds(180), run => run.LoadSave(save));
            yield return new AutomationStep("settle", TimeSpan.FromSeconds(120), Settle, missionBound: true);
            yield return new AutomationStep("inventory", TimeSpan.FromSeconds(20), Inventory, missionBound: true);
            yield return new AutomationStep("mode", TimeSpan.FromSeconds(120), run => Rebuild(run, on), missionBound: true);
            yield return new AutomationStep("watch", TimeSpan.FromSeconds(900), run => WatchAi(run, on), missionBound: true);
            yield return new AutomationStep("verdict", TimeSpan.FromSeconds(10), Verdict, missionBound: true);
        }

        private static IEnumerable<AutomationStep> PerfSteps(string save, bool on)
        {
            yield return new AutomationStep("load", TimeSpan.FromSeconds(240), run => run.LoadSave(save));
            yield return new AutomationStep("settle", TimeSpan.FromSeconds(180), Settle, missionBound: true);
            yield return new AutomationStep("measure", TimeSpan.FromSeconds(900), run => MeasurePerf(run, on), missionBound: true);
        }

        private static IEnumerable<AutomationStep> AcceptanceSteps(string save)
        {
            yield return new AutomationStep("load", TimeSpan.FromSeconds(180), run => run.LoadSave(save));
            yield return new AutomationStep("settle", TimeSpan.FromSeconds(120), Settle, missionBound: true);
            yield return new AutomationStep("inventory", TimeSpan.FromSeconds(20), Inventory, missionBound: true);
            yield return new AutomationStep("on-regions", TimeSpan.FromSeconds(20), run => Regions(run, true), missionBound: true);
            yield return new AutomationStep("on-orders", TimeSpan.FromSeconds(240), run => Orders(run, true), missionBound: true);
            yield return new AutomationStep("on-inside-orders", TimeSpan.FromSeconds(60), run => InsideOrders(run, true), missionBound: true);
            yield return new AutomationStep("off-rebuild", TimeSpan.FromSeconds(120), run => Rebuild(run, false), missionBound: true);
            yield return new AutomationStep("off-regions", TimeSpan.FromSeconds(20), run => Regions(run, false), missionBound: true);
            yield return new AutomationStep("off-orders", TimeSpan.FromSeconds(240), run => Orders(run, false), missionBound: true);
            yield return new AutomationStep("off-inside-orders", TimeSpan.FromSeconds(60), run => InsideOrders(run, false), missionBound: true);
            yield return new AutomationStep("on-rebuild", TimeSpan.FromSeconds(120), run => Rebuild(run, true), missionBound: true);
            yield return new AutomationStep("on-regions-again", TimeSpan.FromSeconds(20), run => Regions(run, true), missionBound: true);
            yield return new AutomationStep("verdict", TimeSpan.FromSeconds(10), Verdict, missionBound: true);
        }

        // ---- steps ---------------------------------------------------------------------------

        private static IEnumerable<AutomationWait> Settle(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            Director.instance.SetEngineFrameRate(250);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 400); // forced load rebuild plus one countdown
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private static IEnumerable<AutomationWait> Inventory(AutomationRun run)
        {
            foreach (string line in run.InSimulation(run.CurrentMission, () => runtime.DescribeStructures())) Stamp("STRUCTURE " + line);
            var all = run.InSimulation(run.CurrentMission, () => runtime.DescribeDecks());
            foreach (var d in all) Stamp("DECK " + Describe(d) + " raised=" + d.Raised);
            decks = all.Where(d => !d.Raised && d.LandingTile >= 0).ToList();
            Check(decks.Count > 0, "inventory", "lowered gate drawbridge with walkable landing present (found " + all.Count + " gate decks)");
            if (decks.Count == 0) throw new InvalidOperationException("The save has no lowered drawbridge in front of a gatehouse.");
            yield break;
        }

        private static IEnumerable<AutomationWait> Rebuild(AutomationRun run, bool enabled)
        {
            runtime.Enabled = enabled;
            run.InSimulation(run.CurrentMission, () => { HoldGatesOpen(); GamePathingManagerAPI.Instance.RequestComponentRebuild(); return 0; });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 260); // 0xE49D0 countdown is 200 calls
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            var all = run.InSimulation(run.CurrentMission, () => runtime.DescribeDecks());
            decks = all.Where(d => !d.Raised && d.LandingTile >= 0).ToList();
            foreach (var d in decks) Stamp("DECK " + (enabled ? "on " : "off ") + Describe(d));
        }

        private static IEnumerable<AutomationWait> Regions(AutomationRun run, bool enabled)
        {
            string phase = enabled ? "on" : "off";
            run.InSimulation(run.CurrentMission, () =>
            {
                var players = GamePlayerManagerAPI.Instance;
                var paths = GamePathingManagerAPI.Instance;
                foreach (var d in decks)
                {
                    string id = phase + " gate=" + d.Gate + " bridge=" + d.Bridge;
                    if (enabled)
                    {
                        Check(d.DeckRegion != 0 && d.DeckRegion == d.GateRegion && d.FrontRegion == d.GateRegion, id, "deck and front joined the gate corridor region (deck=" + d.DeckRegion + ", front=" + d.FrontRegion + ", gate=" + d.GateRegion + ")");
                        Check(d.DeckRegion != d.LandingRegion, id, "deck separated from the landing region");
                        Check(d.EndpointField == d.LandingRegion, id, "portal endpoint points at the landing (" + d.EndpointField + "/" + d.LandingRegion + ")");
                    }
                    else Check(d.DeckRegion == d.LandingRegion, id, "Vanilla merges the lowered deck with the landing (control)");
                    bool gateOpen = GateOpen(d.Gate);
                    for (int p = 1; p <= 8; p++)
                    {
                        if (!PlayerPresent(p)) continue;
                        bool admitted = p == d.GateOwner || players.IsPlayerAlliedTo(d.GateOwner, p);
                        bool toDeck = paths.ArePathComponentsConnected(p, d.LandingRegion, d.DeckRegion);
                        bool toInside = d.OtherField > 0 && paths.ArePathComponentsConnected(p, d.LandingRegion, d.OtherField);
                        Stamp("REGION " + id + " player=" + p + " admitted=" + admitted + " gateOpen=" + gateOpen + " captured=" + d.GateCaptured +
                              " landing->deck=" + toDeck + " landing->inside=" + toInside);
                        if (!enabled || d.GateCaptured != 0) continue;
                        if (!admitted) Check(!toDeck, id + " player=" + p, "hostile player cannot reach the lowered deck");
                        else if (gateOpen) Check(toDeck, id + " player=" + p, "admitted player reaches the deck through the open gate portal");
                    }
                }
                return 0;
            });
            yield break;
        }

        private static IEnumerable<AutomationWait> Orders(AutomationRun run, bool enabled)
        {
            string phase = enabled ? "on" : "off";
            var d = decks[0];
            onDeckVisits = 0; samples = 0;
            int target = d.DeckTiles[d.DeckTiles.Count / 2];
            run.InSimulation(run.CurrentMission, () =>
            {
                HoldGatesOpen();
                soldiers.Clear(); Spawn(d); // fresh soldiers per phase, the AI may have taken the old ones
                foreach (int unit in soldiers) MoveTo(unit, target);
                return 0;
            });
            Check(soldiers.Count > 0, phase + "-orders", "hostile test soldiers spawned for player " + enemy);
            if (soldiers.Count == 0) yield break;
            // The decisive result: the route Vanilla computed for each order (decoded from the stored plan).
            var plans = run.InSimulation(run.CurrentMission, () => soldiers.Select(u => DescribePlan(u, d, target)).ToList());
            int planned = plans.Count(p => p.Length > 0), crossing = plans.Count(p => p.CrossesDeck), reachTarget = plans.Count(p => p.EndsAtTarget);
            Stamp("ORDERS " + phase + " player=" + enemy + " soldiers=" + soldiers.Count + " target=" + Tile(target) + " withPlan=" + planned +
                  " crossingDeck=" + crossing + " endingAtTarget=" + reachTarget + " plans=" + string.Join(" ", plans.Select(p => p.Text)));
            if (enabled) Check(crossing == 0, phase + "-orders", "no hostile route over the lowered deck is computed");
            else
            {
                Check(crossing > 0, phase + "-orders", "control: without the feature Vanilla computes hostile routes over the deck");
                Check(reachTarget > 0, phase + "-orders", "control: decoded routes end on the ordered deck tile (decoder sanity)");
            }
            for (int chunk = 0; chunk < SampleChunks; chunk++)
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, TicksPerChunk);
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                run.InSimulation(run.CurrentMission, () => { HoldGatesOpen(); Sample(d); foreach (int unit in soldiers) MoveTo(unit, target); return 0; });
                if (chunk == 0 || chunk == SampleChunks / 2) Stamp("ORDERS " + phase + " chunk=" + chunk + " positions=" + string.Join(" ", soldiers.Select(u => Position(u))));
            }
            Stamp("ORDERS " + phase + " result: samples=" + samples + " onDeckVisits=" + onDeckVisits + " positions=" +
                  string.Join(" ", soldiers.Select(u => Position(u))));
            // Informational: AI lords take over units of their own player, so presence is not a check.
            if (enabled) Check(onDeckVisits == 0, phase + "-orders", "hostile soldiers never enter the lowered deck");
            // Leave the deck free for the next phase: soldiers that got onto it are removed.
            run.InSimulation(run.CurrentMission, () => { RemoveSoldiersOnDeck(d); return 0; });
        }

        // The reported failure case: the castle interior is also reachable another way, so the
        // whole route lies in one region and only the per-player tile mask can keep hostile
        // routes off the deck. Orders target the tile just inside the gate.
        private static IEnumerable<AutomationWait> InsideOrders(AutomationRun run, bool enabled)
        {
            string phase = enabled ? "on" : "off";
            var d = decks[0];
            int target = d.InnerTile;
            var setup = run.InSimulation(run.CurrentMission, () =>
            {
                HoldGatesOpen();
                soldiers.Clear(); Spawn(d);
                int inside = DrawbridgeDeckRuntime.RegionOf(target);
                bool alternate = enemy > 0 && inside > 0 && GamePathingManagerAPI.Instance.ArePathComponentsConnected(enemy, d.LandingRegion, inside);
                if (alternate) foreach (int unit in soldiers) MoveTo(unit, target);
                return Tuple.Create(alternate, inside);
            });
            Stamp("INSIDE " + phase + " gate=" + d.Gate + " target=" + Tile(target) + " insideRegion=" + setup.Item2 + " landingRegion=" + d.LandingRegion +
                  " alternateRoute=" + setup.Item1 + " soldiers=" + soldiers.Count);
            if (!setup.Item1 || soldiers.Count == 0) yield break; // no second way into this castle: nothing to compare
            var plans = run.InSimulation(run.CurrentMission, () => soldiers.Select(u => DescribePlan(u, d, target)).ToList());
            int planned = plans.Count(p => p.Length > 0), crossing = plans.Count(p => p.CrossesDeck);
            Stamp("INSIDE " + phase + " withPlan=" + planned + " crossingDeck=" + crossing + " plans=" + string.Join(" ", plans.Select(p => p.Text)));
            if (enabled) Check(crossing == 0, phase + "-inside-orders", "routes into the castle use the other way, never the lowered deck");
            run.InSimulation(run.CurrentMission, () => { foreach (int unit in soldiers) if (Alive(unit, out _)) Kill(unit); soldiers.Clear(); return 0; });
        }

        private static unsafe void Kill(int unit)
        {
            if (APIShared.UnitAccess.TryGetById(unit, out GameUnit* _, out _)) GameUnitManagerAPI.Instance.KillUnit(unit);
        }

        private static IEnumerable<AutomationWait> Verdict(AutomationRun run)
        {
            Stamp("VERDICT passed=" + passed + " failed=" + failed);
            if (failed > 0) throw new InvalidOperationException(failed + " acceptance checks failed.");
            yield break;
        }

        // ---- world helpers (engine monitor held by InSimulation) --------------------------

        private static void Spawn(DrawbridgeDeckRuntime.DeckReport d)
        {
            var players = GamePlayerManagerAPI.Instance;
            enemy = 0;
            for (int p = 1; p <= 8 && enemy == 0; p++)
                if (PlayerPresent(p) && p != d.GateOwner && !players.IsPlayerAlliedTo(d.GateOwner, p)) enemy = p;
            if (enemy == 0) return;
            var tiles = GameTileManagerAPI.Instance;
            var landing = tiles.GetTileVectorFromId(d.LandingTile);
            var deck = new HashSet<int>(d.DeckTiles);
            var heights = tiles.GetHeightLayer();
            var structures = tiles.GetStructureLayer();
            for (int radius = 0; radius <= 4 && soldiers.Count < SoldierCount; radius++)
                for (int dy = -radius; dy <= radius && soldiers.Count < SoldierCount; dy++)
                    for (int dx = -radius; dx <= radius && soldiers.Count < SoldierCount; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        int x = landing.X + dx, y = landing.Y + dy;
                        if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                        int tile = tiles.GetTileId(x, y);
                        if (deck.Contains(tile) || structures[tile] != 0 || DrawbridgeDeckRuntime.RegionOf(tile) != d.LandingRegion) continue;
                        long id = GameUnitManagerAPI.Instance.CreateUnitLocal(enemy, enemy, x, y, heights[tile], eChimps.CHIMP_TYPE_MACEMAN);
                        if (id > 0 && id <= int.MaxValue && Alive((int)id, out int at) && at == tile) soldiers.Add((int)id);
                        else Stamp("SPAWN rejected id=" + id + " tile=" + x + "/" + y + " actual=" + (id > 0 && id <= int.MaxValue ? Position((int)id) : "invalid"));
                    }
            Stamp("SPAWN player=" + enemy + " soldiers=" + string.Join(",", soldiers) + " near=" + Tile(d.LandingTile));
        }

        private static unsafe void MoveTo(int unit, int tile)
        {
            var v = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            if (APIShared.UnitAccess.TryGetById(unit, out GameUnit* _, out _)) GameUnitManagerAPI.Instance.MoveToTile(unit, v.X, v.Y);
        }

        // ---- AI watch and performance -------------------------------------------------------

        private const int WatchChunks = 75, WatchTicksPerChunk = 100;

        private static IEnumerable<AutomationWait> WatchAi(AutomationRun run, bool on)
        {
            var deckOwners = new Dictionary<int, int>();
            foreach (var d in decks) foreach (int t in d.DeckTiles) deckOwners[t] = d.GateOwner;
            var visitors = new HashSet<uint>();
            long deckSamples = 0;
            for (int chunk = 0; chunk < WatchChunks; chunk++)
            {
                run.InSimulation(run.CurrentMission, () => { HoldGatesOpen(); return 0; });
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, WatchTicksPerChunk);
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                var counts = run.InSimulation(run.CurrentMission, () => CountHostilesOnDecks(deckOwners, visitors));
                deckSamples += counts.Item1;
                if (chunk % 15 == 14)
                    Stamp("AIWATCH " + (on ? "on" : "off") + " ticks=" + (chunk + 1) * WatchTicksPerChunk + " hostileDeckSamples=" + deckSamples +
                          " distinctHostileVisitors=" + visitors.Count + " hostileUnits=" + counts.Item2 + " decksLowered=" + LoweredDeckCount(run) + " :: " + runtime.Summary());
            }
            Stamp("AIWATCH result mode=" + (on ? "on" : "off") + " hostileDeckSamples=" + deckSamples + " distinctHostileVisitors=" + visitors.Count);
            if (on) Check(deckSamples == 0, "ai-on", "no hostile AI unit stands on a lowered gate drawbridge");
        }

        private static int LoweredDeckCount(AutomationRun run) =>
            run.InSimulation(run.CurrentMission, () => runtime.DescribeDecks().Count(d => !d.Raised));

        private static Tuple<int, int> CountHostilesOnDecks(Dictionary<int, int> deckOwners, HashSet<uint> visitors)
        {
            var players = GamePlayerManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int onDeck = 0, hostile = 0;
            int anyOwner = deckOwners.Count > 0 ? deckOwners.Values.First() : 0;
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_AliveState != SHCDESE.Interop.Enums.AliveState.IsAlive) continue;
                int owner = unit.r_ControllableForPlayerId;
                if (owner <= 0 || owner > 8) continue;
                if (anyOwner > 0 && owner != anyOwner && !players.IsPlayerAlliedTo(anyOwner, owner)) hostile++;
                if (!tiles.IsTileInsideMapBounds(unit.r_CurrentTilePositionX, unit.r_CurrentTilePositionY)) continue;
                int tile = tiles.GetTileId(unit.r_CurrentTilePositionX, unit.r_CurrentTilePositionY);
                if (!deckOwners.TryGetValue(tile, out int deckOwner) || owner == deckOwner || players.IsPlayerAlliedTo(deckOwner, owner)) continue;
                onDeck++;
                if (visitors.Add(unit.r_GlobalId) && visitors.Count <= 10)
                    Stamp("AIWATCH hostile on deck: unit=" + (spanIndex + 1) + " owner=" + owner + " type=" + unit.r_UnitChimp + " tile=" + Tile(tile));
            }
            return Tuple.Create(onDeck, hostile);
        }

        private const int PerfSpawnPerPlayer = 600, PerfTicks = 3000;

        private static IEnumerable<AutomationWait> MeasurePerf(AutomationRun run, bool on)
        {
            foreach (var d in run.InSimulation(run.CurrentMission, () => runtime.DescribeDecks())) Stamp("DECK " + Describe(d) + " raised=" + d.Raised);
            int spawned = run.InSimulation(run.CurrentMission, () => SpawnLoad());
            Stamp("PERF mode=" + (on ? "on" : "off") + " spawned=" + spawned + " liveUnits=" + run.InSimulation(run.CurrentMission, () => CountUnits()) +
                  " mapSize=" + GameTileManagerAPI.Instance.GetCurrentMapSize() + " gateDecks=" + run.InSimulation(run.CurrentMission, () => runtime.DescribeDecks().Count));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 300); // warm up after spawning
            string before = runtime.Summary();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            yield return run.WaitForTicks(run.CurrentMission, PerfTicks);
            clock.Stop();
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            Stamp("PERF result mode=" + (on ? "on" : "off") + " ticks=" + PerfTicks + " seconds=" + clock.Elapsed.TotalSeconds.ToString("F2", inv) +
                  " ticksPerSecond=" + (PerfTicks / clock.Elapsed.TotalSeconds).ToString("F2", inv) +
                  " liveUnits=" + run.InSimulation(run.CurrentMission, () => CountUnits()) + " before=[" + before + "] after=[" + runtime.Summary() + "]");
        }

        private static int SpawnLoad()
        {
            var players = GamePlayerManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            var heights = tiles.GetHeightLayer();
            int spawned = 0;
            for (int p = 1; p <= 8; p++)
            {
                int lord = players.GetLordUnitId(p);
                if (lord <= 0 || !Alive(lord, out int lordTile)) continue;
                var at = tiles.GetTileVectorFromId(lordTile);
                for (int n = 0; n < PerfSpawnPerPlayer; n++)
                {
                    int x = at.X + (n % 20) - 10, y = at.Y + (n / 20) % 20 - 10;
                    if (!tiles.IsTileInsideMapBounds(x, y)) continue;
                    long id = GameUnitManagerAPI.Instance.CreateUnitLocal(p, p, x, y, heights[tiles.GetTileId(x, y)], eChimps.CHIMP_TYPE_SPEARMAN);
                    if (id > 0) spawned++;
                }
            }
            return spawned;
        }

        private static int CountUnits()
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            int alive = 0;
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
                if (units[spanIndex].r_AliveState == SHCDESE.Interop.Enums.AliveState.IsAlive) alive++;
            return alive;
        }

        private struct PlanInfo { internal int Length; internal bool CrossesDeck, EndsAtTarget; internal string Text; }

        // Packed plan directions use the direction-grid order (N, NE, E, SE, S, SW, W, NW; 15 = same tile).
        private static PlanInfo DescribePlan(int unit, DrawbridgeDeckRuntime.DeckReport d, int target)
        {
            var info = new PlanInfo { Text = unit + ":none" };
            var paths = GamePathingManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            if (!Alive(unit, out int tile)) return info;
            int length = paths.GetUnitPathPlanLength(unit), index = Math.Max(0, paths.GetUnitPathPlanCurrentIndex(unit));
            info.Length = Math.Max(0, length);
            if (length <= 0) { info.Text = unit + ":noPlan"; return info; }
            var deck = new HashSet<int>(d.DeckTiles);
            var v = tiles.GetTileVectorFromId(tile);
            int x = v.X, y = v.Y;
            for (int i = index; i < length; i++)
            {
                if (!paths.TryGetPackedPathDirection(unit, i, out var direction)) break;
                int dir = (int)direction;
                if (dir > 7) continue;
                x += DirectionBits.Dx[dir]; y += DirectionBits.Dy[dir];
                if (tiles.IsTileInsideMapBounds(x, y) && deck.Contains(tiles.GetTileId(x, y))) info.CrossesDeck = true;
            }
            int end = tiles.IsTileInsideMapBounds(x, y) ? tiles.GetTileId(x, y) : -1;
            info.EndsAtTarget = end == target;
            info.Text = unit + ":len" + length + (info.CrossesDeck ? ",deck" : "") + ",end" + Tile(end);
            return info;
        }

        private static void Sample(DrawbridgeDeckRuntime.DeckReport d)
        {
            samples++;
            var deck = new HashSet<int>(d.DeckTiles);
            foreach (int unit in soldiers)
                if (Alive(unit, out int tile) && deck.Contains(tile)) onDeckVisits++;
        }

        private static unsafe void RemoveSoldiersOnDeck(DrawbridgeDeckRuntime.DeckReport d)
        {
            var deck = new HashSet<int>(d.DeckTiles);
            for (int i = soldiers.Count - 1; i >= 0; i--)
                if (!Alive(soldiers[i], out int tile) || deck.Contains(tile))
                {
                    int unit = soldiers[i];
                    if (APIShared.UnitAccess.TryGetById(unit, out GameUnit* _, out _)) GameUnitManagerAPI.Instance.KillUnit(unit);
                    soldiers.RemoveAt(i);
                }
        }

        private static unsafe bool Alive(int unit, out int tile)
        {
            tile = -1;
            if (!APIShared.UnitAccess.TryGetById(unit, out GameUnit* u, out _) || u == null || (u->r_AliveState != SHCDESE.Interop.Enums.AliveState.IsAlive && u->r_AliveState != SHCDESE.Interop.Enums.AliveState.NeedsInit)) return false;
            tile = GameTileManagerAPI.Instance.GetTileId(u->r_CurrentTilePositionX, u->r_CurrentTilePositionY);
            return true;
        }

        private static string Position(int unit) => Alive(unit, out int tile) ? unit + "@" + Tile(tile) : unit + "@dead";

        // Keeps every examined gate from auto-closing, so its drawbridge stays lowered while
        // hostile test soldiers are near. Test-world preparation only.
        private static unsafe void HoldGatesOpen()
        {
            foreach (var d in decks)
                if (APIShared.BuildingAccess.TryGetById(d.Gate, out GameBuilding* gate, out _) && gate != null)
                    gate->r_GateDoNotCloseForTicks = 30000;
        }

        private static unsafe bool GateOpen(int gate) =>
            APIShared.BuildingAccess.TryGetById(gate, out GameBuilding* g, out _) && g != null && g->r_GateState != 2;

        private static bool PlayerPresent(int player) => GamePlayerManagerAPI.Instance.GetLordUnitId(player) > 0;

        private static string Tile(int tile)
        {
            if ((uint)tile >= DrawbridgeDeckRuntime.TileCount) return "none";
            var v = GameTileManagerAPI.Instance.GetTileVectorFromId(tile);
            return v.X + "/" + v.Y;
        }

        private static string Describe(DrawbridgeDeckRuntime.DeckReport d) =>
            "gate=" + d.Gate + " owner=" + d.GateOwner + " captured=" + d.GateCaptured + " bridge=" + d.Bridge + " portal=" + d.Portal +
            " side=" + d.PortalSide + " front=" + Tile(d.FrontTile) + " gateTile=" + Tile(d.GateTile) + " landing=" + Tile(d.LandingTile) +
            " deckRegion=" + d.DeckRegion + " frontRegion=" + d.FrontRegion + " gateRegion=" + d.GateRegion + " landingRegion=" + d.LandingRegion +
            " endpoint=" + d.EndpointField + " other=" + d.OtherField + " centre=" + d.CenterField + " deckCells=" + d.DeckTiles.Count;

        private static void Check(bool condition, string scope, string what)
        {
            if (condition) passed++; else failed++;
            Stamp("ACCEPT " + (condition ? "PASS " : "FAIL ") + scope + " :: " + what);
        }

        private static void Stamp(string text) => Shared.DebugLogHelper.LogInfo(log, "BRIDGE_TEST " + text);
    }
}
