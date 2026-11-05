using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using APIShared;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Drives PlayerControlRelayTest through the game's real input path: EditorDirector's own
    // preDLLCallActions computes and passes the gesture to EngineInterface.TroopSelection and
    // the order tile to DLL_RunTick. Only what Unity's mouse would supply is replaced, so the
    // PC stays usable during a run: button state, unit IDs under the cursor and the mouse tile.
    // The fixture allies one AI with the local player and gives it soldiers in the scenario's own
    // fresh skirmish.
    internal static unsafe class RelaySteps
    {
        private const int ReleasedLeftButton = 3, FinishedDragAction = 9;
        private static readonly List<int> units = new List<int>();
        private static readonly Dictionary<int, int> previousTargets = new Dictionary<int, int>();
        private static readonly HashSet<int> usedTargets = new HashSet<int>();
        private static readonly FieldInfo selectedList = AccessTools.Field(typeof(EditorDirector), "selectedChimpList");
        private static readonly FieldInfo newSelectionInfo = AccessTools.Field(typeof(EditorDirector), "gotNewSelectionInfo");
        private static readonly FieldInfo boxOn = AccessTools.Field(typeof(EditorDirector), "troopSelectionBoxOn");
        private static readonly FieldInfo chimps = AccessTools.Field(typeof(GameMap), "chimps");
        private static object engine;
        private static int startX, startY, targetX, targetY, stopX, stopY, stopFrame = -1;
        private static bool patched;
        private static Gesture pending, active;

        private sealed class Gesture
        {
            internal bool Right, Box, Shift;
            internal int[] UnderCursor = new int[0], Boxed = new int[0];
            internal int X = -1, Y = -1;
        }

        internal static void Register()
        {
            Runner.Register("Relay.Fixture", Fixture);
            Runner.Register("Relay.PickCheck", PickCheck);
            Runner.Register("Relay.ClickUnit", s => Perform(new Gesture { UnderCursor = new[] { units[s.Int(0, 0)] }, Shift = s.Arg(1, "") == "shift" }));
            Runner.Register("Relay.BoxUnits", s => Perform(new Gesture { Box = true, Boxed = Range(s.Int(0), s.Int(1)) }));
            Runner.Register("Relay.MoveExpect", MoveExpect);
            Runner.Register("Relay.PressStop", PressStop);
            Runner.Register("Relay.RightClick", s => Perform(new Gesture { Right = true }));
            Runner.Register("Relay.CheckGroups", s => GameSteps.Do(CheckGroups));
            Runner.Register("Relay.CheckMotion", s => GameSteps.Do(() => CheckMotion(s.Int(0), s.Int(1))));
        }

        private static void Patch()
        {
            if (patched) return;
            Runner.Require(selectedList != null && newSelectionInfo != null && boxOn != null && chimps != null, "game input fields changed");
            var harmony = UiAcceptancePlugin.Harmony;
            var caller = AccessTools.Method(typeof(EditorDirector), nameof(EditorDirector.preDLLCallActions));
            var key = AccessTools.Method(typeof(KeyManager), nameof(KeyManager.IsActionPressed), new[] { typeof(Enums.KeyFunctions) });
            Runner.Require(caller != null && key != null, "game input methods changed");
            harmony.Patch(caller,
                prefix: new HarmonyMethod(typeof(RelaySteps), nameof(BeforePreDll)) { priority = Priority.First },
                postfix: new HarmonyMethod(typeof(RelaySteps), nameof(AfterPreDllFirst)) { priority = Priority.First });
            harmony.Patch(caller, postfix: new HarmonyMethod(typeof(RelaySteps), nameof(AfterPreDllLast)) { priority = Priority.Last });
            harmony.Patch(AccessTools.Method(typeof(EditorDirector), "getMouseStateForEngine"),
                postfix: new HarmonyMethod(typeof(RelaySteps), nameof(AfterMouseState)) { priority = Priority.First });
            harmony.Patch(AccessTools.Method(typeof(EngineInterface), nameof(EngineInterface.TroopSelection)),
                prefix: new HarmonyMethod(typeof(RelaySteps), nameof(BeforeTroopSelection)) { priority = Priority.First });
            harmony.Patch(key, postfix: new HarmonyMethod(typeof(RelaySteps), nameof(AfterKey)) { priority = Priority.First });
            patched = true;
        }

        private static IEnumerable<Wait> Perform(Gesture gesture)
        {
            Patch();
            Volatile.Write(ref pending, gesture);
            yield return new Until("engine pass read the gesture", () => Volatile.Read(ref pending) == null && Volatile.Read(ref active) == null);
        }

        // A finished drag: the state Update leaves behind on mouse-up, which the real caller
        // turns into CurrentAction 0 and selection_on.
        private static void BeforePreDll(EditorDirector __instance)
        {
            active = Interlocked.Exchange(ref pending, null);
            if (active == null || !active.Box) return;
            MainControls.instance.CurrentAction = FinishedDragAction;
            boxOn.SetValue(__instance, true);
            selectedList.SetValue(__instance, active.Boxed);
            newSelectionInfo.SetValue(__instance, true);
        }

        private static void AfterMouseState(ref int __result, ref bool rightUp)
        {
            if (active == null) return;
            __result = active.Right ? 0 : ReleasedLeftButton;
            rightUp = active.Right;
        }

        // Update overwrites the real list every frame, so it is replaced at the call itself.
        private static void BeforeTroopSelection(ref int[] underCursorChimps)
        {
            if (active != null && !active.Box) underCursorChimps = active.UnderCursor;
        }

        private static void AfterPreDllFirst(EditorDirector __instance, ref int mouseOverX, ref int mouseOverY)
        {
            if (active == null) return;
            mouseOverX = active.X; mouseOverY = active.Y;
            __instance.shiftPressed = active.Shift;
        }

        private static void AfterPreDllLast() => Volatile.Write(ref active, null);

        // Key edges last one Unity frame, so Vanilla and the mod see it like a real key press.
        private static void AfterKey(Enums.KeyFunctions __0, ref bool __result)
        {
            if (__0 == Enums.KeyFunctions.Stop && UnityEngine.Time.frameCount == stopFrame) __result = true;
        }

        // The real picking must also return units of another owner, or no real click could reach them.
        private static IEnumerable<Wait> PickCheck(Step step)
        {
            Patch();
            var drawn = (Dictionary<int, Chimp>)chimps.GetValue(GameMap.instance);
            Runner.Require(drawn.TryGetValue(units[0], out Chimp chimp), "fixture unit is not drawn");
            var world = new UnityEngine.Vector3(chimp.position.x, chimp.position.y + chimp.bodyHeight / 2f, chimp.position.z);
            UnityEngine.Vector3 screen = UnityEngine.Camera.main.WorldToScreenPoint(world);
            var center = new UnityEngine.Vector2(screen.x, screen.y);
            var margin = new UnityEngine.Vector2(12f, 12f);
            int[] under = null;
            int depth = -1;
            int[] boxed = GameMap.instance.grabTroopsOnScreen(center - margin, center + margin, ref under, center, ref depth);
            var fixture = new HashSet<int>(units);
            int boxedHits = boxed.Count(fixture.Contains), underHits = under.Count(fixture.Contains);
            AcceptLog.Info("RELAY_PICK boxed=" + boxed.Length + " boxedFixture=" + boxedHits + " under=" + under.Length + " underFixture=" + underHits);
            Runner.Require(boxedHits > 0, "real box picking did not return foreign units");
            Runner.Require(underHits > 0, "real cursor picking did not return foreign units");
            yield break;
        }

        // Not every tile accepts an order: the native dispatcher rightly returns false for an
        // unreachable target. Tries unused walkable tiles until one is accepted, then checks the
        // exact members right after execution, before the AI gives new orders.
        private static IEnumerable<Wait> MoveExpect(Step step)
        {
            int first = step.Int(0), count = step.Int(1), rounds = 0;
            var result = new Regex("owner=\\d (Executed|Uncertain|Rejected):");
            var accepted = new Regex("owner=\\d Executed: " + count + " units ordered");
            for (int radius = 8; radius <= 40; radius += 2)
                foreach (var offset in new[] { new[] { 1, 1 }, new[] { -1, 1 }, new[] { 1, -1 }, new[] { -1, -1 } })
                {
                    int x = startX + offset[0] * radius, y = startY + offset[1] * radius;
                    if (usedTargets.Contains(x << 16 | y) || !PrepareTarget(x, y)) continue;
                    usedTargets.Add(x << 16 | y);
                    rounds++;
                    long mark = AcceptLog.Position;
                    foreach (var wait in Perform(new Gesture { X = targetX, Y = targetY })) yield return wait;
                    yield return new Until("order result", () => AcceptLog.Count(mark, result) > 0);
                    if (AcceptLog.Count(mark, accepted) == 0) continue;
                    AcceptLog.Info("RELAY_MOVE attempts=" + rounds + " target=" + targetX + "," + targetY);
                    ExpectTargets(first, count);
                    yield break;
                }
            throw new StepFailure("no tested tile accepted the order after " + rounds + " attempts");
        }

        private static bool PrepareTarget(int x, int y)
        {
            lock (engine)
            {
                var tiles = GameTileManagerAPI.Instance;
                if (!tiles.IsTileInsideMapBounds(x, y) || !tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(x, y))) return false;
                targetX = x; targetY = y;
                RememberTargets();
                return true;
            }
        }

        // args: first, count. The native Stop returns 1 regardless, so the executed line proves the
        // path up to that call; the logged fields are context, and the AI may order again at once.
        private static IEnumerable<Wait> PressStop(Step step)
        {
            Patch();
            int first = step.Int(0), count = step.Int(1);
            long mark = AcceptLog.Position;
            stopFrame = UnityEngine.Time.frameCount + 1;
            var accepted = new Regex("owner=\\d Executed: " + count + " units ordered");
            yield return new Until("stop executed", () => AcceptLog.Count(mark, accepted) > 0);
            LogStopped(units[first]);
        }

        private static void LogStopped(int id)
        {
            lock (engine)
            {
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _))
                {
                    stopX = unit->r_CurrentTilePositionX; stopY = unit->r_CurrentTilePositionY;
                    AcceptLog.Info("RELAY_STOP unit=" + id + " tile=" + stopX + "," + stopY +
                        " target=" + unit->r_TargetTilePositionX + "," + unit->r_TargetTilePositionY);
                }
            }
        }

        // The real effect, later: the stopped unit stayed near its stop tile, while a unit of the
        // same former group walked on. The AI could still order them, so this is evidence for this run.
        private static void CheckMotion(int stopped, int moving)
        {
            lock (engine)
            {
                GameUnit* still = null, walker = null;
                Runner.Require(APIShared.UnitAccess.TryGetById(units[stopped], out still, out _) &&
                    APIShared.UnitAccess.TryGetById(units[moving], out walker, out _), "motion fixture units missing");
                int stoppedDistance = Math.Max(Math.Abs(still->r_CurrentTilePositionX - stopX), Math.Abs(still->r_CurrentTilePositionY - stopY));
                int walkedDistance = Math.Max(Math.Abs(walker->r_CurrentTilePositionX - stopX), Math.Abs(walker->r_CurrentTilePositionY - stopY));
                AcceptLog.Info("RELAY_MOTION stopped=" + still->r_CurrentTilePositionX + "," + still->r_CurrentTilePositionY + " stopTile=" + stopX + "," + stopY +
                    " stoppedTribe=" + still->r_TribeId + " moving=" + walker->r_CurrentTilePositionX + "," + walker->r_CurrentTilePositionY +
                    " movingTarget=" + walker->r_TargetTilePositionX + "," + walker->r_TargetTilePositionY + " movingTribe=" + walker->r_TribeId);
                Runner.Require(stoppedDistance <= 2, "stopped unit moved " + stoppedDistance + " tiles");
                Runner.Require(walkedDistance > 2, "ordered unit did not walk away");
            }
        }

        private static int[] Range(int first, int count) => units.Skip(first).Take(count).ToArray();

        private static IEnumerable<Wait> Fixture(Step step)
        {
            int ally = step.Int(0), count = step.Int(1, 3);
            Runner.Require(GamePlayerManagerAPI.Instance.IsAIPlayer(ally), "fixture ally is no AI player");
            var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            Runner.Require(field != null, "native monitor contract changed");
            engine = field.GetValue(null);
            usedTargets.Clear();
            lock (engine) BuildFixture(ally, count);
            yield return new Delay(step.Int(2, 500));
            CheckGroups();
        }

        private static void BuildFixture(int ally, int count)
        {
            var players = GamePlayerManagerAPI.Instance;
            int local = PlayerPerspectiveAPI.GetControlledPlayerId();
            Runner.Require(local >= 1, "fixture needs a participating local player");
            players.SetPlayerTeam(ally, players.GetPlayerTeam(local));
            Runner.Require(players.IsPlayerAlliedTo(local, ally), "team setter did not ally the fixture");
            Runner.Require(APIShared.UnitAccess.TryGetById(players.GetLordUnitId(ally), out GameUnit* lord, out _), "fixture lord missing");
            startX = lord->r_CurrentTilePositionX; startY = lord->r_CurrentTilePositionY;
            int height = lord->r_HeightElevation;
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(ally, false));
            Runner.Require(tribe > 0, "fixture group creation failed");
            units.Clear();
            for (int index = 0; index < count; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(ally, ally, startX, startY, height, eChimps.CHIMP_TYPE_ARCHER));
                GameUnit* unit = null;
                Runner.Require(id > 0 && APIShared.UnitAccess.TryGetById(id, out unit, out _), "fixture unit missing");
                units.Add(id);
                if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, id);
                Runner.Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "fixture group assignment failed");
            }
            // The markers are screen-space: the screenshot needs the units in view.
            players.SetScreenCenterToUnit(units[0]);
            AcceptLog.Info("RELAY_FIXTURE ally=" + ally + " tribe=" + tribe + " units=" + units.Count + " start=" + startX + "," + startY);
        }

        private static void RememberTargets()
        {
            previousTargets.Clear();
            foreach (int id in units)
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _))
                    previousTargets[id] = unit->r_TargetTilePositionX << 16 | unit->r_TargetTilePositionY;
        }

        private static void ExpectTargets(int first, int count)
        {
            lock (engine)
            {
                int ordered = 0, others = 0;
                var tribes = new HashSet<int>();
                for (int index = 0; index < units.Count; index++)
                {
                    if (!APIShared.UnitAccess.TryGetById(units[index], out GameUnit* unit, out _)) continue;
                    bool changed = !previousTargets.TryGetValue(units[index], out int before) ||
                        before != (unit->r_TargetTilePositionX << 16 | unit->r_TargetTilePositionY);
                    bool near = changed && Math.Abs(unit->r_TargetTilePositionX - targetX) <= 25 && Math.Abs(unit->r_TargetTilePositionY - targetY) <= 25;
                    bool wanted = index >= first && index < first + count;
                    if (wanted && near) { ordered++; tribes.Add(unit->r_TribeId); }
                    if (!wanted && near) others++;
                }
                AcceptLog.Info("RELAY_TARGETS wanted=" + count + " ordered=" + ordered + " others=" + others + " groups=" + tribes.Count + " target=" + targetX + "," + targetY);
                Runner.Require(ordered == count, "only " + ordered + " of " + count + " selected units got the target");
                Runner.Require(others == 0, others + " units outside the selection got the target");
                Runner.Require(tribes.Count == 1, "selected units are not one native group");
            }
            CheckGroups();
        }

        // Every fixture unit must belong to a live group whose member count covers it: a group
        // reused or cleared behind its members would leave units claiming a group without them.
        private static void CheckGroups()
        {
            lock (engine)
            {
                var claims = new SortedDictionary<int, int>();
                foreach (int id in units)
                    if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && APIShared.UnitAccess.IsReallyAlive(unit))
                        claims[unit->r_TribeId] = (claims.TryGetValue(unit->r_TribeId, out int seen) ? seen : 0) + 1;
                var text = new List<string>();
                string broken = null;
                foreach (var claim in claims)
                {
                    int count = -1;
                    string state = "none";
                    if (claim.Key > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(claim.Key, out GameTribe* tribe) && tribe != null)
                    {
                        count = tribe->r_UnitsInGroup;
                        state = tribe->r_AliveState.ToString();
                        if (tribe->r_AliveState != AliveState.IsAlive) broken = broken ?? "group " + claim.Key + " is " + state;
                    }
                    else broken = broken ?? "units without a group: " + claim.Key;
                    if (count < claim.Value) broken = broken ?? "group " + claim.Key + " has " + count + " members but " + claim.Value + " units claim it";
                    text.Add(claim.Key + ":claims=" + claim.Value + ",members=" + count + "," + state);
                }
                AcceptLog.Info("RELAY_GROUPS " + string.Join(" ", text));
                Runner.Require(broken == null, broken);
            }
        }
    }
}
