using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Pathfinding's fire escape for AI soldiers that carry a stale AI exclusion mark (GameUnit+0x428).
    // The native group move skips such a member; Pathfinding clears the mark of the escaping soldier
    // through APIShared's GroupMoveExclusion right before its own group move. The fixture gives an AI
    // player maceman in a fresh group on dormant pitch, writes the mark the AI leaves behind after its
    // own group orders (during peace time the AI does not take the fixture over, so the test writes it)
    // and ignites the pitch. Only the AI's order routines and Pathfinding write the mark; the fresh group
    // is in no AI role table.
    internal static unsafe class FireEscapeSteps
    {
        private static readonly List<int> units = new List<int>();
        private static readonly List<int> pitches = new List<int>();
        private static readonly HashSet<int> pitchTiles = new HashSet<int>();
        private static readonly HashSet<int> onPitch = new HashSet<int>();
        private static readonly List<int> controls = new List<int>();
        private static readonly HashSet<int> escapedSeen = new HashSet<int>(), exposed = new HashSet<int>(), burningTiles = new HashSet<int>();
        private static readonly Dictionary<int, uint> globals = new Dictionary<int, uint>();
        private static object engine;
        private static int marked, fireOwner, fireHeight;

        internal static void Register()
        {
            Runner.Register("FireEscape.Fixture", Fixture);
            Runner.Register("FireEscape.Ignite", s => GameSteps.Do(Ignite));
            Runner.Register("FireEscape.Expect", Expect);
        }

        // args: AI player, unit count, settle ms
        private static IEnumerable<Wait> Fixture(Step step)
        {
            int owner = step.Int(0), count = step.Int(1, 6);
            Runner.Require(GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "fixture owner is no AI player");
            var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            Runner.Require(field != null, "native monitor contract changed");
            engine = field.GetValue(null);
            lock (engine) Build(owner, count);
            // Military updates set r_SelectionAllowed (Pathfinding's soldier filter) on their first ticks.
            yield return new Delay(step.Int(2, 1500));
            lock (engine) Mark();
        }

        private static void Build(int owner, int count)
        {
            var players = GamePlayerManagerAPI.Instance;
            var tiles = GameTileManagerAPI.Instance;
            Runner.Require(APIShared.UnitAccess.TryGetById(players.GetLordUnitId(owner), out GameUnit* lord, out _), "fixture lord missing");
            int height = lord->r_HeightElevation;
            fireOwner = owner; fireHeight = height;
            units.Clear(); controls.Clear(); pitches.Clear(); pitchTiles.Clear(); globals.Clear();
            // A free 3x3 field beside the lord, inside the map's diamond.
            Runner.Require(FindFreeField(lord->r_CurrentTilePositionX, lord->r_CurrentTilePositionY, 4, out int cx, out int cy),
                "no free 3x3 field beside the AI lord");
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    int id = GamePitchManagerAPI.Instance.CreatePitch(cx + dx, cy + dy, owner);
                    if (id > 0) { pitches.Add(id); pitchTiles.Add(tiles.GetTileId(cx + dx, cy + dy)); }
                }
            Runner.Require(pitches.Count > 0, "no pitch ditch was created");
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(owner, false));
            Runner.Require(tribe > 0, "fixture group creation failed");
            for (int index = 0; index < count; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, cx + index % 3 - 1, cy + index / 3 % 3 - 1, height, eChimps.CHIMP_TYPE_MACEMAN));
                GameUnit* unit = null;
                Runner.Require(id > 0 && APIShared.UnitAccess.TryGetById(id, out unit, out _), "fixture unit missing");
                if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, id);
                Runner.Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "fixture group assignment failed");
                units.Add(id); globals[id] = unit->r_GlobalId;
            }
            // Control group far from the fire, also marked, in a fresh group of its own: no fire move
            // orders it, so its marks must stay (nothing else clears them).
            Runner.Require(FindFreeField(cx, cy, 15, out int kx, out int ky), "no free field for the control group");
            int controlTribe = checked((int)GameTribeManagerAPI.Instance.Create(owner, false));
            Runner.Require(controlTribe > 0, "control group creation failed");
            for (int index = 0; index < 2; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, kx + index, ky, height, eChimps.CHIMP_TYPE_MACEMAN));
                GameUnit* unit = null;
                Runner.Require(id > 0 && APIShared.UnitAccess.TryGetById(id, out unit, out _), "control unit missing");
                if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, id);
                Runner.Require(GameTribeManagerAPI.Instance.AssignUnit(controlTribe, id), "control group assignment failed");
                controls.Add(id); globals[id] = unit->r_GlobalId;
            }
            players.SetScreenCenterToUnit(units[0]);
            AcceptLog.Info("FIRE_ESCAPE_FIXTURE owner=" + owner + " tribe=" + tribe + " units=" + units.Count + " pitches=" + pitches.Count + " center=" + cx + "," + cy +
                " controlTribe=" + controlTribe + " control=" + controls.Count + " at " + kx + "," + ky);
        }

        private static bool FindFreeField(int originX, int originY, int minimumRadius, out int cx, out int cy)
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int radius = minimumRadius; radius <= minimumRadius + 30; radius++)
                foreach (int[] offset in new[] { new[] { radius, 0 }, new[] { -radius, 0 }, new[] { 0, radius }, new[] { 0, -radius },
                    new[] { radius, radius }, new[] { -radius, -radius } })
                {
                    int x = originX + offset[0], y = originY + offset[1];
                    bool free = true;
                    for (int dx = -1; dx <= 1 && free; dx++)
                        for (int dy = -1; dy <= 1 && free; dy++)
                            free = tiles.IsTileInsideMapBounds(x + dx, y + dy) && tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(x + dx, y + dy));
                    if (free) { cx = x; cy = y; return true; }
                }
            cx = cy = -1;
            return false;
        }

        private static void Mark()
        {
            marked = 0;
            foreach (int id in units.Concat(controls))
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == globals[id])
                { unit->N000000D8 = 1; marked++; }
            int soldiers = units.Count(id => APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_SelectionAllowed != 0);
            AcceptLog.Info("FIRE_ESCAPE_MARKED marks=" + marked + " soldiers=" + soldiers);
            Runner.Require(marked == units.Count + controls.Count, "not every fixture unit received the mark");
        }

        private static void Ignite()
        {
            int ignited = 0;
            lock (engine)
            {
                // Units standing on the pitch now must escape; others nearby may stand in spreading fire too.
                onPitch.Clear(); escapedSeen.Clear(); exposed.Clear(); exposureTile.Clear();
                foreach (int id in units)
                    if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) &&
                        pitchTiles.Contains(GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY)))
                        onPitch.Add(id);
                // Freshly built ditches hold no pitch: OnFire alone made no fire record in run 002 of
                // 09.11.2026. Native 0x9C120 (Extender wrapper with its Pre/Post) lights each tile; world
                // coordinates are tile*8, its spread argument 2 adds no type-0x1A record.
                ProbeSteps.RequireNativeReferenceHash();
                var spawn = typeof(GameUnitManagerAPI).Assembly.GetType("SHCDESE.Detours.BulkProjectileDetours", true)
                    .GetMethod("c_game_spawn_fire_internal_hook_impl", BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }, null);
                Runner.Require(spawn != null && spawn.ReturnType == typeof(int), "Installed SE fire wrapper ABI changed.");
                foreach (int id in pitches)
                    if (GamePitchManagerAPI.Instance.TryGetPitchById(id, out GamePitchDescriptor* pitch) && pitch->IsAlive)
                    {
                        if (pitch->State == PitchState.Dormant) pitch->State = PitchState.OnFire;
                        if ((int)spawn.Invoke(null, new object[] { fireOwner, pitch->TileX * 8 + 4, pitch->TileY * 8 + 4, fireHeight, 2, 0 }) > 0) ignited++;
                    }
            }
            AcceptLog.Info("FIRE_ESCAPE_IGNITED fires=" + ignited + " unitsOnPitch=" + onPitch.Count + " beside=" + (units.Count - onPitch.Count) + " control=" + controls.Count);
            Runner.Require(ignited > 0, "no fire record was spawned on the pitch");
            Runner.Require(onPitch.Count > 0, "no fixture unit stands on the pitch");
        }

        // Qualifying units: marked fixture units seen on a tile with fire. The fire is short and 0x9C120
        // jitters its target, so not every pitch tile burns: units never seen on fire are excluded and
        // counted with that reason, units that moved before a sample saw them on fire likewise. Passes
        // after the fire is out when at least the minimum number qualified, every qualifying unit was later
        // seen with its mark cleared (only Pathfinding's move clears it here) and a move target that is
        // neither its fire tile nor burning, and the far control group kept its marks.
        // args: limit ms (below the step timeout, so the last state is logged on failure), minimum units
        private static IEnumerable<Wait> Expect(Step step)
        {
            ProbeSteps.RequireNativeReferenceHash();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string state = "";
            bool done = false;
            long nextReport = 0;
            minimumQualifying = step.Int(1, 3);
            while (!(done = Check(out state)) && watch.ElapsedMilliseconds < step.Int(0, 30000))
            {
                if (watch.ElapsedMilliseconds >= nextReport) { AcceptLog.Info("FIRE_ESCAPE_PROGRESS ms=" + watch.ElapsedMilliseconds + " " + state); nextReport += 2000; }
                yield return new Delay(250);
            }
            AcceptLog.Info("FIRE_ESCAPE_" + (done ? "DONE" : "TIMEOUT") + " marked=" + marked + " ms=" + watch.ElapsedMilliseconds + " " + state);
            Runner.Require(done, "AI soldiers did not escape the burning pitch: " + state);
            Runner.Require(controlKept, "a control unit far from the fire lost its mark: " + state);
        }

        private static bool controlKept;
        private static int minimumQualifying;
        private static readonly Dictionary<int, int> exposureTile = new Dictionary<int, int>();

        private static bool Check(out string state)
        {
            lock (engine)
            {
                // walking: r_AIState 101/122, the military walking states 0x11B520 sets for admitted members.
                int alive = 0, cleared = 0, away = 0, walking = 0, control = 0, controlMarked = 0, unexposed = 0, movedUnsampled = 0;
                var sample = new List<string>();
                var tiles = GameTileManagerAPI.Instance;
                string fire = FireRecords();
                foreach (int id in units)
                {
                    if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit->r_GlobalId != globals[id] ||
                        !APIShared.UnitAccess.IsReallyAlive(unit)) continue;
                    // Only units seen marked on a burning tile qualify: 0x9C120 jitters its target, so not
                    // every pitch tile burns (runs 006/007 of 09.11.2026: unhurt units stayed, rightly).
                    bool clear = unit->N000000D8 == 0;
                    int here = tiles.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                    if (!clear && burningTiles.Contains(here) && !exposed.Contains(id)) { exposed.Add(id); exposureTile[id] = here; }
                    int target = tiles.GetTileId(unit->r_TargetTilePositionX2, unit->r_TargetTilePositionY2);
                    // Seen once is enough: Pathfinding returns escaped soldiers once the short fire is out
                    // (run 005 of 09.11.2026), and a soldier may first get an unburnt-pitch fallback target.
                    if (clear && exposed.Contains(id) && target != exposureTile[id] && !burningTiles.Contains(target)) escapedSeen.Add(id);
                    if (!exposed.Contains(id))
                    {
                        if (clear) movedUnsampled++; else unexposed++;
                        continue;
                    }
                    alive++;
                    if (clear) cleared++;
                    if (escapedSeen.Contains(id)) { away++; if (unit->r_AIState == 101 || unit->r_AIState == 122) walking++; }
                    else if (sample.Count < 4)
                        sample.Add(id + ":mark=" + unit->N000000D8 + " ai=" + unit->r_AIState + " hp%=" + unit->r_CurrentHealthPercentage +
                            " tile=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY + " target=" + unit->r_TargetTilePositionX2 + "," +
                            unit->r_TargetTilePositionY2 + " tribe=" + unit->r_TribeId + " selectable=" + unit->r_SelectionAllowed);
                }
                foreach (int id in controls)
                    if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == globals[id] &&
                        APIShared.UnitAccess.IsReallyAlive(unit))
                    { control++; if (unit->N000000D8 != 0) controlMarked++; }
                controlKept = control > 0 && controlMarked == control;
                state = "qualifying=" + alive + "/min" + minimumQualifying + " cleared=" + cleared + " escaped=" + away + " walkingNow=" + walking +
                    " excluded(neverOnFireWhileMarked=" + unexposed + " clearedBeforeFireSampled=" + movedUnsampled + ") control=" + control +
                    " controlMarked=" + controlMarked + " " + fire + " sample=[" + string.Join("; ", sample) + "]";
                // Wait until the fire is out, so every exposure is observed before the verdict.
                return burningTiles.Count == 0 && alive >= minimumQualifying && away == alive;
            }
        }

        // Read only, hash-bound (FIRE_EFFECTS.md): effect manager 0x2F7A680, slots 1..5999 of 0xE8 bytes,
        // +0x3C state (2 active), +0x3E type (9 fire), +0x60 native tile id.
        private const int EffectManagerRva = 0x2F7A680, EffectStride = 0xE8;
        private static string FireRecords()
        {
            byte* manager = (byte*)GetModuleHandle("CrusaderDE.dll") + EffectManagerRva;
            int burning = 0, onPitchTiles = 0, pitchOnFire = 0;
            burningTiles.Clear();
            for (int slot = 1; slot < 6000; slot++)
            {
                byte* record = manager + slot * EffectStride;
                short recordState = *(short*)(record + 0x3C);
                if (*(short*)(record + 0x3E) != 9 || recordState != 1 && recordState != 2) continue;
                // A new record (1) turns active (2) on its first update; counting it as burning sees units
                // that Pathfinding moves within the first sampling interval.
                burningTiles.Add(*(int*)(record + 0x60));
                if (recordState != 2) continue;
                burning++;
                if (pitchTiles.Contains(*(int*)(record + 0x60))) onPitchTiles++;
            }
            foreach (int id in pitches)
                if (GamePitchManagerAPI.Instance.TryGetPitchById(id, out GamePitchDescriptor* pitch) && pitch->IsAlive && pitch->State == PitchState.OnFire)
                    pitchOnFire++;
            return "fireRecords=" + burning + " onPitch=" + onPitchTiles + " pitchOnFire=" + pitchOnFire;
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern System.IntPtr GetModuleHandle(string name);
    }
}
