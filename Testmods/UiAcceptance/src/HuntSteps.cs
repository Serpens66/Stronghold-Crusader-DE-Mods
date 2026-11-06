using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using CrusaderDE;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Observes ImprovedHunters in a real skirmish through native state only: chickens must keep their
    // owner and wander, a Hunter must take its own player's chicken through Vanilla's whole chain, and
    // melee units must ignore rabbits until ordered. Nothing here steers the Hunter.
    internal static unsafe class HuntSteps
    {
        private const int HunterTargetIdOffset = 0x39A, HunterTargetGlobalOffset = 0x39C, CorpseOffset = 0x29C;
        // Vanilla's automatic sprites, as its AIV builder requests them; 0 selects camera-relative sheets.
        private const int AutomaticSpriteVariation = 0x0F;
        private static object engine;
        private static readonly HashSet<int> tiles = new HashSet<int>();
        private static readonly List<int> rabbits = new List<int>();
        private static int rabbitTribe, foreignTargets;

        internal static void Register()
        {
            Runner.Register("Hunt.PlaceHut", PlaceHut);
            Runner.Register("Hunt.ExpectChickens", ExpectChickens);
            Runner.Register("Hunt.Cycle", Cycle);
            Runner.Register("Hunt.Rabbits", RabbitFixture);
            Runner.Register("Hunt.RabbitsAlive", RabbitsAlive);
            Runner.Register("Hunt.AttackRabbit", AttackRabbit);
            Runner.Register("Hunt.RabbitDead", RabbitDead);
            Runner.Register("Hunt.FarRabbit", s => GameSteps.Do(() => { lock (Engine()) { if (s.Arg(1, "") == "clear") rabbits.Clear(); AddFarRabbit(s.Int(0)); } }));
            Runner.Register("Hunt.MoveSoldiers", s => GameSteps.Do(() => { lock (Engine()) MoveSoldiers(s.Int(0)); }));
            Runner.Register("Hunt.Stance", s => GameSteps.Do(() => { lock (Engine()) GameTribeManagerAPI.Instance.SetStance(rabbitTribe, (TribeStance)Enum.Parse(typeof(TribeStance), s.Arg(0))); }));
            Runner.Register("Hunt.Deer", s => GameSteps.Do(() => { lock (Engine()) SpawnDeer(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.DeerCycle", DeerCycle);
            Runner.Register("Hunt.ExpectPeace", s => GameSteps.Do(() =>
            {
                lock (Engine())
                    Runner.Require(APIShared.Simulation.PeaceTimeState.TryGetActive(out bool active) && active, "peace time is not active");
            }));
        }

        private static object Engine()
        {
            if (engine != null) return engine;
            FieldInfo field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            Runner.Require(field != null, "native monitor contract changed");
            return engine = field.GetValue(null);
        }

        private static int Local()
        {
            int local = PlayerPerspectiveAPI.GetControlledPlayerId();
            Runner.Require(local >= 1, "needs a participating local player");
            return local;
        }

        private static bool TryFindBuilding(int owner, eStructs type, out int x, out int y)
        {
            x = y = 0;
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState == AliveState.None || building->r_BuildingType != type || building->r_PlayerIdOwner != owner)
                    continue;
                x = building->r_TilePositionXBegin;
                y = building->r_TilePositionYBegin;
                return true;
            }
            return false;
        }

        private static IEnumerable<Wait> PlaceHut(Step step)
        {
            int local = Local();
            lock (Engine()) PlaceBuildings(local);
            yield return new Until("hut registered", () =>
            {
                lock (Engine()) return TryFindBuilding(local, eStructs.STRUCT_HUNTERS_HUT, out _, out _);
            });
        }

        internal static void PlaceBuildings(int local)
        {
            Runner.Require(APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no local lord");
            int x = lord->r_CurrentTilePositionX, y = lord->r_CurrentTilePositionY;
            // Not every start has a granary; chickens and the Hunter's delivery both need one.
            if (!TryFindBuilding(local, eStructs.STRUCT_GRANARY, out int gx, out int gy))
                Runner.Require(Place(local, x, y, eMappers.MAPPER_GRANARY, eStructs.STRUCT_GRANARY, 8, out gx, out gy), "no free granary site");
            Runner.Require(Place(local, gx, gy, eMappers.MAPPER_HUNTER, eStructs.STRUCT_HUNTERS_HUT, 10, out _, out _), "no free Hunter's Hut site");
        }

        private static int CountBuildings(int owner, eStructs type)
        {
            int count = 0;
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.None && building->r_BuildingType == type && building->r_PlayerIdOwner == owner)
                    count++;
            }
            return count;
        }

        private static bool Place(int local, int x, int y, eMappers mapper, eStructs type, int firstRadius, out int placedX, out int placedY)
        {
            placedX = x;
            placedY = y;
            int scale = BuildingScales.GetScale(mapper);
            int before = CountBuildings(local, type);
            for (int radius = firstRadius; radius < firstRadius + 40; radius += 2)
            {
                foreach ((int dx, int dy) in new[] { (radius, 0), (-radius, 0), (0, radius), (0, -radius), (radius, radius), (-radius, -radius), (radius, -radius), (-radius, radius) })
                {
                    // The return value is a native status residue, not proof: a rejected site can return non-zero.
                    GameBuildingManagerAPI.Instance.CreatePrefab(local, x + dx, y + dy, mapper, scale, AutomaticSpriteVariation, true, false);
                    if (CountBuildings(local, type) == before)
                        continue;
                    placedX = x + dx;
                    placedY = y + dy;
                    AcceptLog.Info("HUNT_PLACED " + mapper + " at=" + (x + dx) + "," + (y + dy));
                    return true;
                }
            }
            return false;
        }

        private static void CountChickens(int owner, out int own, out int neutral, out int foreign)
        {
            own = neutral = foreign = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    *(ushort*)((byte*)unit + CorpseOffset) != 0)
                    continue;
                if (unit->r_ControllableForPlayerId == 0) neutral++;
                else if (unit->r_ControllableForPlayerId == owner)
                {
                    own++;
                    tiles.Add(unit->r_CurrentTilePositionX << 16 | unit->r_CurrentTilePositionY);
                }
                else foreign++;
            }
        }

        // Population and ownership: exactly the configured chickens, none neutral, and they walk.
        private static IEnumerable<Wait> ExpectChickens(Step step)
        {
            int local = Local(), expected = step.Int(0), distinct = step.Int(1, 1);
            tiles.Clear();
            int own = 0, neutral = 0, foreign = 0, overshoot = 0;
            yield return new Until("own chickens " + expected + " on " + distinct + " tiles", () =>
            {
                lock (Engine()) CountChickens(local, out own, out neutral, out foreign);
                overshoot = Math.Max(overshoot, own);
                return own == expected && tiles.Count >= distinct;
            });
            AcceptLog.Info("HUNT_CHICKENS own=" + own + " neutral=" + neutral + " foreign=" + foreign +
                " maxOwn=" + overshoot + " tilesSeen=" + tiles.Count);
            Runner.Require(neutral == 0, "chickens were neutralized: " + neutral);
            Runner.Require(overshoot <= expected, "population exceeded the target: " + overshoot);
        }

        // Target -> dead on the arrow -> carried -> delivered. The meat amount is unique to chickens,
        // so a delivery of it cannot be another animal's.
        private static IEnumerable<Wait> Cycle(Step step)
        {
            int local = Local(), meat = step.Int(0);
            int startMeat;
            lock (Engine()) startMeat = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT);
            int targetId = 0, hunterId = 0;
            uint targetGlobal = 0;
            foreignTargets = 0;
            yield return new Until("Hunter targets its own chicken", () =>
            {
                lock (Engine()) return FindChickenHunter(local, ref hunterId, ref targetId, ref targetGlobal);
            });
            AcceptLog.Info("HUNT_TARGET hunter=" + hunterId + " chicken=" + targetId + "/" + targetGlobal);
            bool neutralSeen = false;
            int lastChicken = targetId;
            yield return new Until("chicken killed by the Hunter", () =>
            {
                lock (Engine()) return ChickenKilled(local, ref hunterId, ref lastChicken, ref targetGlobal, ref neutralSeen);
            });
            Runner.Require(!neutralSeen, "the hunted chicken changed owner");
            AcceptLog.Info("HUNT_KILLED chicken=" + lastChicken + "/" + targetGlobal + " foreignTargets=" + foreignTargets);
            int delivered = 0;
            yield return new Until("meat delivered", () =>
            {
                lock (Engine()) delivered = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT) - startMeat;
                return delivered >= meat;
            });
            AcceptLog.Info("HUNT_DELIVERED meat=" + delivered + " expectedPerChicken=" + meat);
            Runner.Require(foreignTargets == 0, "a Hunter targeted a foreign chicken");
        }

        private static bool ChickenKilled(int local, ref int hunterId, ref int chickenId, ref uint chickenGlobal, ref bool neutralSeen)
        {
            if (!APIShared.UnitAccess.TryGetById(chickenId, out GameUnit* chicken, out _) || chicken->r_GlobalId != chickenGlobal)
            {
                // Another chicken was chosen meanwhile: follow the Hunter.
                FindChickenHunter(local, ref hunterId, ref chickenId, ref chickenGlobal);
                return false;
            }

            if (chicken->r_ControllableForPlayerId != local) neutralSeen = true;
            return *(ushort*)((byte*)chicken + CorpseOffset) != 0;
        }

        private static bool FindChickenHunter(int local, ref int hunterId, ref int chickenId, ref uint chickenGlobal)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* hunter = units.GetValuePointer(index);
                if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || !APIShared.UnitAccess.IsReallyAlive(hunter)) continue;
                byte* raw = (byte*)hunter;
                int target = *(ushort*)(raw + HunterTargetIdOffset);
                uint global = *(uint*)(raw + HunterTargetGlobalOffset);
                if (target == 0 || global == 0 || !APIShared.UnitAccess.TryGetById(target, out GameUnit* prey, out _) ||
                    prey->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN || prey->r_GlobalId != global)
                    continue;
                if (prey->r_ControllableForPlayerId != hunter->r_ControllableForPlayerId) foreignTargets++;
                if (hunter->r_ControllableForPlayerId != local) continue;
                hunterId = index + 1;
                chickenId = target;
                chickenGlobal = global;
                return true;
            }
            return false;
        }

        // Soldiers of the local player in a tribe, with rabbits on the neighbouring tiles.
        private static IEnumerable<Wait> RabbitFixture(Step step)
        {
            int local = Local();
            lock (Engine()) BuildRabbits(local, step.Int(0, 3), step.Int(1, 3), step.Int(2, 6), step.Int(3, 6));
            yield break;
        }

        private static void BuildRabbits(int local, int soldiers, int count, int offsetX, int offsetY)
        {
            int lordId = GamePlayerManagerAPI.Instance.GetLordUnitId(local);
            Runner.Require(APIShared.UnitAccess.TryGetById(lordId, out GameUnit* lord, out _), "no local lord");
            int x = lord->r_CurrentTilePositionX + offsetX, y = lord->r_CurrentTilePositionY + offsetY;
            int height = lord->r_HeightElevation;
            rabbitTribe = checked((int)GameTribeManagerAPI.Instance.Create(local, false));
            Runner.Require(rabbitTribe > 0, "fixture group creation failed");
            for (int index = 0; index < soldiers; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(local, local, x + index, y, height, eChimps.CHIMP_TYPE_SWORDSMAN));
                GameUnit* unit = null;
                Runner.Require(id > 0 && APIShared.UnitAccess.TryGetById(id, out unit, out _), "fixture soldier missing");
                if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, id);
                Runner.Require(GameTribeManagerAPI.Instance.AssignUnit(rabbitTribe, id), "fixture group assignment failed");
            }

            rabbits.Clear();
            for (int index = 0; index < count; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x + index, y + 1, height, eChimps.CHIMP_TYPE_RABBIT));
                Runner.Require(id > 0, "fixture rabbit missing");
                rabbits.Add(id);
            }

            GamePlayerManagerAPI.Instance.SetScreenCenterToUnit(rabbits[0]);
            AcceptLog.Info("HUNT_RABBITS tribe=" + rabbitTribe + " at=" + x + "," + y + " rabbits=" + string.Join(",", rabbits));
        }

        private static bool RabbitLive(int id)
        {
            return APIShared.UnitAccess.TryGetById(id, out GameUnit* rabbit, out _) &&
                rabbit->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT &&
                (rabbit->r_AliveState == AliveState.NeedsInit || APIShared.UnitAccess.IsReallyAlive(rabbit)) &&
                *(ushort*)((byte*)rabbit + CorpseOffset) == 0;
        }

        // A rabbit out of contact range of the fixture: only a chase can reach it.
        private static void AddFarRabbit(int distance)
        {
            int soldier = GameTribeManagerAPI.Instance.GetLeaderUnitId(rabbitTribe);
            Runner.Require(APIShared.UnitAccess.TryGetById(soldier, out GameUnit* unit, out _), "fixture soldier missing");
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(
                0, 0, unit->r_CurrentTilePositionX + distance, unit->r_CurrentTilePositionY, unit->r_HeightElevation, eChimps.CHIMP_TYPE_RABBIT));
            Runner.Require(id > 0, "far rabbit missing");
            rabbits.Add(id);
            AcceptLog.Info("HUNT_FAR_RABBIT index=" + (rabbits.Count - 1) + " rabbit=" + id + " distance=" + distance);
        }

        private static void MoveSoldiers(int distance)
        {
            int soldier = GameTribeManagerAPI.Instance.GetLeaderUnitId(rabbitTribe);
            Runner.Require(APIShared.UnitAccess.TryGetById(soldier, out GameUnit* unit, out _), "fixture soldier missing");
            Runner.Require(GameTribeManagerAPI.Instance.MoveTo(
                rabbitTribe, unit->r_CurrentTilePositionX + distance, unit->r_CurrentTilePositionY), "move order was rejected");
            AcceptLog.Info("HUNT_MOVE tribe=" + rabbitTribe);
        }

        private static readonly List<int> deer = new List<int>();

        private static void SpawnDeer(int count, int distance)
        {
            int local = Local();
            Runner.Require(TryFindBuilding(local, eStructs.STRUCT_HUNTERS_HUT, out int x, out int y), "no hut for the deer");
            Runner.Require(APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no local lord");
            deer.Clear();
            for (int index = 0; index < count; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(
                    0, 0, x + distance + index * 2, y - distance, lord->r_HeightElevation, eChimps.CHIMP_TYPE_DEER));
                Runner.Require(id > 0, "fixture deer missing");
                deer.Add(id);
            }
            AcceptLog.Info("HUNT_DEER at=" + (x + distance) + "," + (y - distance) + " deer=" + string.Join(",", deer));
        }

        // Records which deer the Hunter holds. Every switch to another deer while the previous one still
        // lives is a lost hunt, which is what the post-shot continuation has to prevent.
        private static IEnumerable<Wait> DeerCycle(Step step)
        {
            int local = Local(), kills = step.Int(0), maxSwitches = step.Int(1, 0);
            int startMeat;
            lock (Engine()) startMeat = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT);
            int current = 0, switches = 0;
            var dead = new HashSet<int>();
            int delivered = 0;
            yield return new Until("deer hunted " + kills, () =>
            {
                lock (Engine())
                {
                    ObserveDeerHunter(ref current, ref switches, dead);
                    delivered = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT) - startMeat;
                }
                return dead.Count >= kills && delivered > 0;
            });
            AcceptLog.Info("HUNT_DEER_RESULT killed=" + dead.Count + " meat=" + delivered + " switchesFromLiveDeer=" + switches);
            Runner.Require(switches <= maxSwitches, "the Hunter left a living deer " + switches + " times");
        }

        private static void ObserveDeerHunter(ref int current, ref int switches, HashSet<int> dead)
        {
            foreach (int id in deer)
            {
                if (dead.Contains(id)) continue;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* animal, out _) ||
                    *(ushort*)((byte*)animal + CorpseOffset) != 0)
                    dead.Add(id);
            }

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* hunter = units.GetValuePointer(index);
                if (hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || !APIShared.UnitAccess.IsReallyAlive(hunter)) continue;
                int target = *(ushort*)((byte*)hunter + HunterTargetIdOffset);
                if (target == 0 || !deer.Contains(target) || target == current) return;
                if (current != 0 && !dead.Contains(current))
                {
                    switches++;
                    AcceptLog.Info("HUNT_DEER_SWITCH from=" + current + " to=" + target);
                }
                AcceptLog.Info("HUNT_DEER_TARGET deer=" + target);
                current = target;
                return;
            }
        }

        // Unordered soldiers beside rabbits: every rabbit must still live after the wait.
        private static IEnumerable<Wait> RabbitsAlive(Step step)
        {
            yield return new Delay(step.Int(0));
            lock (Engine())
            {
                List<int> dead = rabbits.Where(id => !RabbitLive(id)).ToList();
                AcceptLog.Info("HUNT_RABBITS_ALIVE live=" + (rabbits.Count - dead.Count) + "/" + rabbits.Count);
                Runner.Require(dead.Count == 0, "soldiers attacked rabbits without an order: " + string.Join(",", dead));
            }
        }

        private static IEnumerable<Wait> AttackRabbit(Step step)
        {
            lock (Engine())
            {
                int target = rabbits[step.Int(0, 0)];
                Runner.Require(RabbitLive(target), "ordered rabbit is not alive");
                Runner.Require(GameTribeManagerAPI.Instance.AttackUnit(rabbitTribe, target), "attack order was rejected");
                AcceptLog.Info("HUNT_ATTACK tribe=" + rabbitTribe + " rabbit=" + target);
            }
            yield break;
        }

        private static IEnumerable<Wait> RabbitDead(Step step)
        {
            int index = step.Int(0);
            yield return new Until("rabbit " + index + " dead", () =>
            {
                lock (Engine()) return !RabbitLive(rabbits[index]);
            });
            AcceptLog.Info("HUNT_RABBIT_DEAD index=" + index + " rabbit=" + rabbits[index]);
        }
    }
}
