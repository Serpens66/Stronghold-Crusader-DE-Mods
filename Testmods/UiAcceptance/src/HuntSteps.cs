using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
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
        private const int SectorGridRva = 0x3395E10, SectorStride = 0xA0, SectorUnitsOffset = 0x48;
        private const int MinimumCalmMs = 500, SpeedDeerDistance = 46, SpeedDeerStep = 30, HunterSpeedStageOffset = 0x348, CalmLeaderMs = 800, RingAttemptMs = 2500;
        private static int rabbitTribe, foreignTargets, nearDeer;
        private static int soldierSector = -1, corpseFixture, ringX, ringY, ringPlug, ringTry;
        private static uint corpseFixtureGlobal;
        private const int FadingCorpseState = 0x6E, CollectingState = 2, FreshIdleState = 6, HutIdleState = 7, ForcedSearchFrames = 120;
        private const int RingRadius = 3, RingGapHalf = 1;
        private const eMappers RingPlugMapper = eMappers.MAPPER_HOVEL;
        private static int speedTarget, speedLastStage, speedRounds;
        private static uint speedGlobal;
        private static bool speedSlowed;
        // Vanilla's herd records, pinned by this test mod's native audit.
        private const int HerdManagerRva = 0x7CC6720, HerdStride = 0x688, HerdFleeingOffset = 0x5F4, HerdLeaderOffset = 0x5A, HerdCount = 0x1194, MapTickRva = 0x3665F58, HerdGoRva = 0x11B520, SpawnHerdRva = 0x11E150, ReturnPlaceReach = 12, StartDifference = 8, BeforeMissionStartTicks = 20;
        // Index of the food page in HUD_Main.BuildIconLists.
        private const int FoodBuildScreen = 3;

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
            Runner.Register("Hunt.LogAnimals", s => GameSteps.Do(() => { lock (Engine()) LogAnimals(); }));
            Runner.Register("Hunt.LogHerds", s => GameSteps.Do(() => { lock (Engine()) LogHerds(s.Arg(0, "")); }));
            Runner.Register("Hunt.WatchHerds", WatchHerds);
            Runner.Register("Hunt.RememberStarts", s => GameSteps.Do(() => { lock (Engine()) RememberStarts(); }));
            Runner.Register("Hunt.ClearOtherAnimals", s => GameSteps.Do(() => { lock (Engine()) ClearOtherAnimals(s.Arg(0, "") == "all"); }));
            Runner.Register("Hunt.PickHerd", PickHerd);
            Runner.Register("Hunt.WalledUnitThenHerd", WalledUnitThenHerd);
            Runner.Register("Hunt.ExpectNewHerds", ExpectNewHerds);
            Runner.Register("Hunt.SpawnHerd", s => GameSteps.Do(() => { lock (Engine()) SpawnHerd(s.Arg(0), s.Arg(1, "400"), s.Arg(2, "400")); }));
            Runner.Register("Hunt.KillHerd", s => GameSteps.Do(() => { lock (Engine()) KillHerd(s.Int(0)); }));
            Runner.Register("Hunt.ExpectNoReturn", ExpectNoReturn);
            Runner.Register("Hunt.WaitSinceKill", WaitSinceKill);
            Runner.Register("Hunt.ExpectReturn", ExpectReturn);
            Runner.Register("Hunt.ExpectHerdGrows", ExpectHerdGrows);
            Runner.Register("Hunt.ExpectHerdKept", s => GameSteps.Do(() => { lock (Engine()) LogHerdKept(); }));
            Runner.Register("Hunt.ExpectNoWildlife", ExpectNoWildlife);
            Runner.Register("Hunt.TrackRange", TrackRange);
            Runner.Register("Hunt.TrackFlight", TrackFlight);
            Runner.Register("Hunt.SendHerdAway", s => GameSteps.Do(() => { lock (Engine()) SendHerdAway(s.Int(0)); }));
            Runner.Register("Hunt.ExpectHerdBack", ExpectHerdBack);
            Runner.Register("Hunt.BuildHutByHand", BuildHutByHand);
            Runner.Register("Hunt.DeerNearHunter", s => GameSteps.Do(() => { lock (Engine()) SpawnDeerNearHunter(s.Int(0)); }));
            Runner.Register("Hunt.ExpectFarFromHunter", ExpectFarFromHunter);
            Runner.Register("Hunt.ExpectNoDeerTarget", ExpectNoDeerTarget);
            Runner.Register("Hunt.ExpectDeerTarget", ExpectDeerTarget);
            Runner.Register("Hunt.ExpectOneSwitch", ExpectOneSwitch);
            Runner.Register("Hunt.ExpectHutButtonDisabled", ExpectHutButtonDisabled);
            Runner.Register("Hunt.ExpectWildlife", ExpectWildlife);
            Runner.Register("Hunt.ExpectHunterCarries", ExpectHunterCarries);
            Runner.Register("Hunt.SpeedBothWays", SpeedBothWays);
            Runner.Register("Hunt.Corpse", s => GameSteps.Do(() => { lock (Engine()) SpawnCorpse(s.Arg(0), s.Int(1)); }));
            Runner.Register("Hunt.HoldForeignReservation", HoldForeignReservation);
            Runner.Register("Hunt.SecondHut", SecondHut);
            Runner.Register("Hunt.LogHunters", s => GameSteps.Do(() => { lock (Engine()) LogHunters(); }));
            Runner.Register("Hunt.ExpectCorpseTaken", ExpectCorpseTaken);
            Runner.Register("Hunt.ExpectCorpseIgnored", ExpectCorpseIgnored);
            Runner.Register("Hunt.FadingCorpseIgnored", FadingCorpseIgnored);
            Runner.Register("Hunt.ExpectOneCollector", ExpectOneCollector);
            Runner.Register("Hunt.ExpectCarries", ExpectCarries);
            Runner.Register("Hunt.ForeignHit", ForeignHit);
            Runner.Register("Hunt.Stress", s => GameSteps.Do(() => { lock (Engine()) Stress(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.Supply", s => GameSteps.Do(() => { lock (Engine()) Supply(); }));
            Runner.Register("Hunt.PreyBehindKeep", s => GameSteps.Do(() => { lock (Engine()) SpawnPreyBehindKeep(s.Arg(0)); }));
            Runner.Register("Hunt.PreyAt", s => GameSteps.Do(() => { lock (Engine()) SpawnPreyAt(s.Arg(0), s.Int(1), s.Int(2)); }));
            Runner.Register("Hunt.Prey", s => GameSteps.Do(() => { lock (Engine()) SpawnPrey(s.Arg(0), s.Arg(1) == "ring" ? int.MinValue : s.Int(1), s.Arg(1) == "ring" ? 0 : s.Int(2)); }));
            Runner.Register("Hunt.ClearPrey", s => GameSteps.Do(() => { lock (Engine()) deer.Clear(); }));
            Runner.Register("Hunt.RegisterPrey", s => GameSteps.Do(() => RegisterPrey(s.Arg(0), s.Int(1, -1), s.Arg(2, ""))));
            Runner.Register("Hunt.PreyOwned", s => GameSteps.Do(() => { lock (Engine()) SpawnOwnedPrey(s.Arg(0), s.Int(1), s.Int(2)); }));
            Runner.Register("Hunt.ExpectButcherRemoval", ExpectButcherRemoval);
            Runner.Register("Hunt.RefusePreyType", s => GameSteps.Do(() => RefusePreyType(s.Arg(0))));
            Runner.Register("Hunt.ExpectPreyAlive", s => GameSteps.Do(() => { lock (Engine()) ExpectPreyAlive(s.Arg(0)); }));
            Runner.Register("Hunt.ExpectTargetType", ExpectTargetType);
            Runner.Register("Hunt.ExpectLeaseReleased", ExpectLeaseReleased);
            Runner.Register("Hunt.SightWall", s => GameSteps.Do(() => { lock (Engine()) SightWall(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.SightBuilding", s => GameSteps.Do(() => { lock (Engine()) SightBuilding(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.Ring", Ring);
            Runner.Register("Hunt.CloseRing", CloseRing);
            Runner.Register("Hunt.OpenRing", OpenRing);
            Runner.Register("Hunt.ExpectTargetDropped", ExpectTargetDropped);
            Runner.Register("Hunt.WaitHunterFree", WaitHunterFree);
            Runner.Register("Hunt.WaitHuntersFree", WaitHuntersFree);
            Runner.Register("Hunt.ExpectSeparateTargets", ExpectSeparateTargets);
            Runner.Register("Hunt.AxisDeer", AxisDeer);
            Runner.Register("Hunt.ExpectOpenStart", s => GameSteps.Do(() => { lock (Engine()) ExpectOpenStart(); }));
            Runner.Register("Hunt.WatchForeignHit", WatchForeignHit);
            Runner.Register("Hunt.UnitInHerdSector", UnitInHerdSector);
            Runner.Register("Hunt.ExpectHerdFlight", ExpectHerdFlight);
            Runner.Register("Hunt.ExpectPeace", s => GameSteps.Do(() =>
            {
                lock (Engine())
                    Runner.Require(APIShared.Simulation.PeaceTimeState.TryGetActive(out bool active) && active, "peace time is not active");
            }));
        }

        internal static object Engine()
        {
            if (engine != null) return engine;
            FieldInfo field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            Runner.Require(field != null, "native monitor contract changed");
            return engine = field.GetValue(null);
        }

        internal static int Local()
        {
            int local = PlayerPerspectiveAPI.GetControlledPlayerId();
            Runner.Require(local >= 1, "needs a participating local player");
            return local;
        }

        internal static bool TryFindBuilding(int owner, eStructs type, out int x, out int y)
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
            // From the keep door, not the wandering lord, so the same start place gives the same layout in every run.
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(local);
            int x = door.X, y = door.Y;
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

        internal static bool Place(int local, int x, int y, eMappers mapper, eStructs type, int firstRadius, out int placedX, out int placedY, bool bypassGroundRules = false)
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
                    GameBuildingManagerAPI.Instance.CreatePrefab(local, x + dx, y + dy, mapper, scale, AutomaticSpriteVariation, true, bypassGroundRules);
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
            // args: meat setting, expected carried amount with Vanilla's bonus (first kill of this Hunter).
            int local = Local(), meat = step.Int(0), expectedCarry = step.Int(1, 0), maxCarry = 0;
            int startMeat;
            lock (Engine()) startMeat = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT);
            int targetId = 0, hunterId = 0;
            uint targetGlobal = 0;
            foreignTargets = 0;
            yield return new Until("Hunter targets its own chicken", () =>
            {
                lock (Engine())
                {
                    // The Hunter can finish a whole trip between two polls of this wait.
                    maxCarry = Math.Max(maxCarry, HunterCarry(local));
                    return FindChickenHunter(local, ref hunterId, ref targetId, ref targetGlobal);
                }
            });
            AcceptLog.Info("HUNT_TARGET hunter=" + hunterId + " chicken=" + targetId + "/" + targetGlobal);
            bool neutralSeen = false;
            int lastChicken = targetId;
            yield return new Until("chicken killed by the Hunter", () =>
            {
                lock (Engine())
                {
                    maxCarry = Math.Max(maxCarry, HunterCarry(local));
                    return ChickenKilled(local, ref hunterId, ref lastChicken, ref targetGlobal, ref neutralSeen);
                }
            });
            Runner.Require(!neutralSeen, "the hunted chicken changed owner");
            AcceptLog.Info("HUNT_KILLED chicken=" + lastChicken + "/" + targetGlobal + " foreignTargets=" + foreignTargets);
            int delivered = 0;
            yield return new Until("meat delivered", () =>
            {
                lock (Engine())
                {
                    delivered = GamePlayerManagerAPI.Instance.GetGoodAmount(local, eGoods.STORED_FOOD_MEAT) - startMeat;
                    maxCarry = Math.Max(maxCarry, HunterCarry(local));
                }
                return delivered >= meat;
            });
            AcceptLog.Info("HUNT_DELIVERED meat=" + delivered + " setting=" + meat + " maxCarried=" + maxCarry);
            // The bonus fraction of an earlier kill can add one more.
            Runner.Require(expectedCarry == 0 || maxCarry == expectedCarry || maxCarry == expectedCarry + 1, "the Hunter carried " + maxCarry + ", expected " + expectedCarry);
            Runner.Require(foreignTargets == 0, "a Hunter targeted a foreign chicken");
        }

        private static int HunterCarry(int local)
        {
            int carry = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                    carry = Math.Max(carry, unit->r_CarryGoodAmount);
            }
            return carry;
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

        // Iterators cannot hold pointers, so the lookup lives outside the step body.
        private static bool TryKillUnit(int unitId)
        {
            if (APIShared.UnitAccess.TryGetById(unitId, out GameUnit* _, out _))
                GameUnitManagerAPI.Instance.KillUnit(unitId);
            else
                return false;
            return true;
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
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int home = components[tiles.GetTileId(lord->r_CurrentTilePositionX, lord->r_CurrentTilePositionY)];
            int signX = 1, signY = -1;
            foreach ((int dx, int dy) in new[] { (1, -1), (-1, -1), (1, 1), (-1, 1) })
            {
                if (components[tiles.GetTileId(x + dx * distance, y + dy * distance)] != home)
                    continue;
                signX = dx;
                signY = dy;
                break;
            }
            for (int index = 0; index < count; index++)
            {
                int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(
                    0, 0, x + signX * distance + index * 2, y + signY * distance, lord->r_HeightElevation, eChimps.CHIMP_TYPE_DEER));
                Runner.Require(id > 0, "fixture deer missing");
                deer.Add(id);
            }
            AcceptLog.Info("HUNT_DEER at=" + (x + signX * distance) + "," + (y + signY * distance) + " deer=" + string.Join(",", deer));
        }

        // One line per living herd with its native record, as the mod reads it.
        private static void LogHerds(string label)
        {
            byte* manager = (byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva;
            int tick = *(int*)((byte*)GetModuleHandle("CrusaderDE.dll") + MapTickRva), herds = 0;
            for (int herd = 1; herd < HerdCount; herd++)
            {
                byte* record = manager + herd * HerdStride;
                if (*(short*)(record + 0x40) != 2)
                    continue;

                short kind = *(short*)(record + 0x42);
                int leader = *(short*)(record + HerdLeaderOffset);
                if (!APIShared.UnitAccess.TryGetById(leader, out GameUnit* unit, out _) || unit->r_UnitChimp >= eChimps.CHIMP_NUM_TYPES)
                    continue;
                if (kind != 0xD && kind != 0x28 && kind != 0xF && kind != 0x10 && kind != 0xE && kind != 0x29 && kind != 0x2A)
                    continue;

                herds++;
                int routeCount = *(short*)(record + 0x5DE);
                var route = new List<string>();
                for (int point = 0; point < routeCount && point < 10; point++)
                    route.Add(*(short*)(record + 0x5B4 + point * 4) + "," + *(short*)(record + 0x5B6 + point * 4));
                AcceptLog.Info("HUNT_HERD " + label + " tick=" + tick + " herd=" + herd + " kind=0x" + kind.ToString("X") + " serial=" + *(int*)(record + 0x34) +
                    " owner=" + *(int*)(record + 0x2C) + " count=" + *(short*)(record + 0x5C) + " living=" + LivingMembers(herd) + " flee=" + *(short*)(record + HerdFleeingOffset) +
                    " leader=" + leader + " at=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY +
                    " route=" + *(short*)(record + 0x5DC) + "/" + routeCount + " [" + string.Join(" ", route) + "]");
            }
            AcceptLog.Info("HUNT_HERDS " + label + " tick=" + tick + " herds=" + herds);
        }

        private static int LivingMembers(int herd)
        {
            int living = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_TribeId == herd && unit->r_AliveState == AliveState.IsAlive && unit->r_CurrentHealth > 0 && *(ushort*)((byte*)unit + CorpseOffset) == 0)
                    living++;
            }
            return living;
        }

        // args: milliseconds in all, milliseconds between lines.
        private static IEnumerable<Wait> WatchHerds(Step step)
        {
            int end = Environment.TickCount + step.Int(0), next = 0, line = 0;
            while (Environment.TickCount < end)
            {
                if (Environment.TickCount - next >= 0)
                {
                    next = Environment.TickCount + step.Int(1);
                    lock (Engine()) LogHerds("w" + line++);
                }
                yield return NextFrame.Instance;
            }
        }

        // Herd fixtures for the wildlife return and range: what the map placed, as the mod should see it.
        private static readonly Dictionary<int, HerdStart> herdStarts = new Dictionary<int, HerdStart>();
        private static HerdStart herdFixture;
        private static int herdKillTick, routeReports, herdSends, worstRoam;

        private struct HerdStart
        {
            public int Herd, Serial, X, Y;
            public short Kind;
        }

        private static byte* HerdRecord(int herd) => (byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva + herd * HerdStride;
        private static int NativeTick() => *(int*)((byte*)GetModuleHandle("CrusaderDE.dll") + MapTickRva);

        private static bool LeaderTile(int herd, out int x, out int y)
        {
            x = y = 0;
            if (*(short*)(HerdRecord(herd) + 0x40) != 2 ||
                !APIShared.UnitAccess.TryGetById(*(short*)(HerdRecord(herd) + HerdLeaderOffset), out GameUnit* leader, out _) || leader == null)
                return false;

            x = leader->r_CurrentTilePositionX;
            y = leader->r_CurrentTilePositionY;
            return true;
        }

        private static void RememberStarts()
        {
            herdStarts.Clear();
            for (int herd = 1; herd < HerdCount; herd++)
            {
                short kind = *(short*)(HerdRecord(herd) + 0x42);
                if ((kind == 0xD || kind == 0x28) && LeaderTile(herd, out int x, out int y))
                    herdStarts[*(int*)(HerdRecord(herd) + 0x34)] = new HerdStart { Herd = herd, Serial = *(int*)(HerdRecord(herd) + 0x34), X = x, Y = y, Kind = kind };
            }
            AcceptLog.Info("HUNT_STARTS herds=" + herdStarts.Count + " deer=" + herdStarts.Values.Count(s => s.Kind == 0xD) + " goats=" + herdStarts.Values.Count(s => s.Kind == 0x28));
        }

        // Lions, hyenas and crocodiles kill herds, and rabbits scare them: the fixture must only lose what the test kills.
        // args: optional "all" also removes living deer, goats and camels, so a Hunter has nothing to hunt but the fixtures.
        private static void ClearOtherAnimals(bool all)
        {
            int removed = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && (unit->r_UnitChimp == eChimps.CHIMP_TYPE_LION || unit->r_UnitChimp == eChimps.CHIMP_TYPE_HYENA ||
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_CROCODILE || unit->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT ||
                    all && (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER || unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT || unit->r_UnitChimp == eChimps.CHIMP_TYPE_CAMEL)))
                {
                    unit->r_AliveState = AliveState.MarkedForDeletion;
                    removed++;
                }
            }
            AcceptLog.Info("HUNT_CLEARED " + removed);
        }

        private static int RoamOf(HerdStart start) =>
            LeaderTile(start.Herd, out int x, out int y) ? Math.Max(Math.Abs(x - start.X), Math.Abs(y - start.Y)) : -1;

        // args: deer or goat, how far the herd must have left its start. Waits for such a herd, not in flight.
        private static IEnumerable<Wait> PickHerd(Step step)
        {
            short kind = step.Arg(0) == "goat" ? (short)0x28 : (short)0xD;
            int roam = step.Int(1);
            bool found = false;
            yield return new Until("a herd that has moved", () =>
            {
                lock (Engine()) found = TryPickHerd(kind, roam);
                return found;
            });
            lock (Engine())
                AcceptLog.Info("HUNT_PICKED herd=" + herdFixture.Herd + " serial=" + herdFixture.Serial + " kind=0x" + herdFixture.Kind.ToString("X") + " start=" + herdFixture.X + "," + herdFixture.Y +
                    " roam=" + RoamOf(herdFixture) + " living=" + LivingMembers(herdFixture.Herd));
        }

        private static bool TryPickHerd(short kind, int roam)
        {
            foreach (HerdStart start in herdStarts.Values.OrderBy(s => s.Serial))
            {
                if (start.Kind == kind && *(int*)(HerdRecord(start.Herd) + 0x34) == start.Serial && LivingMembers(start.Herd) > 0 && RoamOf(start) >= roam)
                {
                    herdFixture = start;
                    return true;
                }
            }
            return false;
        }

        // args: deer, goat or none, herds expected, animals in each. Waits for the mod's first second, then counts herds
        // not there at the start and checks each against the keeps.
        private static IEnumerable<Wait> ExpectNewHerds(Step step)
        {
            int until = 0;
            lock (Engine()) until = (NativeTick() / 40 + 2) * 40;
            yield return new Until("the mod's first second", () => { lock (Engine()) return NativeTick() >= until; });
            lock (Engine()) CheckNewHerds(step.Arg(0), step.Int(1), step.Int(2));
        }

        private static void CheckNewHerds(string kindName, int expected, int animals)
        {
            short kind = kindName == "goat" ? (short)0x28 : (short)0xD;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int found = 0;
            for (int herd = 1; herd < HerdCount; herd++)
            {
                short herdKind = *(short*)(HerdRecord(herd) + 0x42);
                int serial = *(int*)(HerdRecord(herd) + 0x34);
                if ((herdKind != 0xD && herdKind != 0x28) || herdStarts.ContainsKey(serial) || !LeaderTile(herd, out int x, out int y))
                    continue;

                found++;
                int living = LivingMembers(herd), nearest = int.MaxValue, nearestPlayer = 0;
                bool sameLand = false;
                for (int player = 1; player <= 8; player++)
                {
                    if (players.GetPlayerKeepId(player) <= 0)
                        continue;
                    var door = players.GetPlayerKeepDoorPosition(player);
                    // From the door, which lies a few tiles from the keep's middle the mod measures from.
                    int distance = Math.Max(Math.Abs(door.X - x), Math.Abs(door.Y - y));
                    if (distance < nearest)
                    {
                        nearest = distance;
                        nearestPlayer = player;
                        sameLand = GamePathingManagerAPI.Instance.ArePathComponentsConnected(player, components[tiles.GetTileId(door.X, door.Y)], components[tiles.GetTileId(x, y)], PathConnectionQueryMode.ExcludeLadderClimb);
                    }
                }
                AcceptLog.Info("HUNT_NEW_HERD herd=" + herd + " kind=0x" + herdKind.ToString("X") + " at=" + x + "," + y + " living=" + living + " count=" + *(short*)(HerdRecord(herd) + 0x5C) +
                    " nearestKeep=" + nearestPlayer + " distance=" + nearest + " sameLand=" + sameLand);
                Runner.Require(herdKind == kind, "a herd of the wrong kind");
                Runner.Require(living == animals, "expected " + animals + " animals, found " + living);
                Runner.Require(sameLand, "the herd is cut off from its keep");
                Runner.Require(nearest >= 45 && nearest <= 85, "the herd stands " + nearest + " tiles from the keep");
            }
            // -1: one for each player with a keep; the lobby does not always hold the same players.
            if (expected < 0)
            {
                expected = 0;
                for (int player = 1; player <= 8; player++)
                {
                    if (players.GetPlayerKeepId(player) > 0)
                        expected++;
                }
            }
            AcceptLog.Info("HUNT_NEW_HERDS found=" + found + " expected=" + expected);
            Runner.Require(found == expected, "expected " + expected + " new herds, found " + found);
        }

        // args: animals to leave alive. The bodies stay, as after a hunt.
        private static void KillHerd(int keep)
        {
            int kept = 0, killed = 0, herd = herdFixture.Herd;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_TribeId != herd || unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth <= 0 || *(ushort*)((byte*)unit + CorpseOffset) != 0)
                    continue;
                if (kept++ < keep)
                    continue;
                int id = index + 1;
                if (APIShared.UnitAccess.TryGetById(id, out _, out _))
                    GameUnitManagerAPI.Instance.KillUnit(id);
                killed++;
            }
            herdKillTick = NativeTick();
            AcceptLog.Info("HUNT_KILLED herd=" + herd + " killed=" + killed + " left=" + LivingMembers(herd) + " tick=" + herdKillTick);
            Runner.Require(LivingMembers(herd) == keep, "the herd has other living animals than wanted");
        }

        private static bool FindReturned(out int herd, out int living)
        {
            herd = living = 0;
            for (int candidate = 1; candidate < HerdCount; candidate++)
            {
                int serial = *(int*)(HerdRecord(candidate) + 0x34);
                if (*(short*)(HerdRecord(candidate) + 0x42) != herdFixture.Kind || herdStarts.ContainsKey(serial) ||
                    !LeaderTile(candidate, out int x, out int y) || Math.Max(Math.Abs(x - herdFixture.X), Math.Abs(y - herdFixture.Y)) > ReturnPlaceReach)
                {
                    continue;
                }

                herd = candidate;
                living = LivingMembers(candidate);
                return true;
            }
            return false;
        }

        // args: map ticks since the kill.
        private static IEnumerable<Wait> WaitSinceKill(Step step)
        {
            int until = herdKillTick + step.Int(0);
            yield return new Until("ticks since the kill", () => { lock (Engine()) return NativeTick() >= until; });
        }

        // args: ticks since the kill during which nothing may have returned, also for an animal left alive.
        private static IEnumerable<Wait> ExpectNoReturn(Step step)
        {
            int until = herdKillTick + step.Int(0);
            while (true)
            {
                lock (Engine())
                {
                    Runner.Require(!FindReturned(out int herd, out _), "herd " + herd + " returned too early at tick " + NativeTick());
                    if (NativeTick() >= until)
                        break;
                }
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_NO_RETURN until=" + until);
        }

        // args: earliest and latest ticks after the kill. Exactly two animals stay; the fixture becomes the new herd.
        private static IEnumerable<Wait> ExpectReturn(Step step)
        {
            int earliest = step.Int(0), latest = step.Int(1), newHerd = 0, returnedAt = 0;
            yield return new Until("the herd's return", () =>
            {
                lock (Engine())
                {
                    Runner.Require(NativeTick() <= herdKillTick + latest, "no return within " + latest + " ticks");
                    if (!FindReturned(out newHerd, out _))
                        return false;
                    returnedAt = NativeTick();
                    return true;
                }
            });
            // The extra animals leave in the same tick and the herd record follows one tick later.
            int settled = returnedAt + 5;
            yield return new Until("the returned herd settling", () => { lock (Engine()) return NativeTick() >= settled; });
            lock (Engine()) CheckReturned(newHerd, returnedAt, earliest);
        }

        private static void CheckReturned(int newHerd, int returnedAt, int earliest)
        {
            byte* record = HerdRecord(newHerd);
            int living = LivingMembers(newHerd), count = *(short*)(record + 0x5C), present = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_TribeId == newHerd && unit->r_AliveState != AliveState.None)
                    present++;
            }
            AcceptLog.Info("HUNT_RETURNED herd=" + newHerd + " serial=" + *(int*)(record + 0x34) + " after=" + (returnedAt - herdKillTick) + " living=" + living + " count=" + count + " present=" + present);
            Runner.Require(returnedAt - herdKillTick >= earliest, "returned after " + (returnedAt - herdKillTick) + " ticks, before the wait was over");
            Runner.Require(living == 2 && count == 2 && present == 2, "expected exactly 2 animals, living=" + living + " count=" + count + " present=" + present);
            herdFixture.Herd = newHerd;
            herdFixture.Serial = *(int*)(record + 0x34);
            herdStarts[herdFixture.Serial] = herdFixture;
        }

        // args: animals. The fixture grows by Vanilla's own rule.
        private static IEnumerable<Wait> ExpectHerdGrows(Step step)
        {
            int minimum = step.Int(0), start = 0, living = 0;
            lock (Engine()) start = NativeTick();
            yield return new Until("the herd growing", () =>
            {
                lock (Engine())
                {
                    living = LivingMembers(herdFixture.Herd);
                    return living >= minimum;
                }
            });
            lock (Engine()) AcceptLog.Info("HUNT_GREW living=" + living + " ticks=" + (NativeTick() - start));
        }

        private static void LogHerdKept() =>
            AcceptLog.Info("HUNT_HERD_AFTER_LOAD herd=" + herdFixture.Herd + " serial=" + *(int*)(HerdRecord(herdFixture.Herd) + 0x34) + " tick=" + NativeTick() + " killTick=" + herdKillTick);

        // args: milliseconds. A map without these herds gets none.
        private static IEnumerable<Wait> ExpectNoWildlife(Step step)
        {
            int end = Environment.TickCount + step.Int(0);
            while (Environment.TickCount < end)
            {
                lock (Engine())
                {
                    CountWildlife(out int deerCount, out int goats);
                    Runner.Require(deerCount + goats == 0, "wildlife appeared: deer=" + deerCount + " goats=" + goats);
                }
                yield return NextFrame.Instance;
            }
            lock (Engine()) AcceptLog.Info("HUNT_NO_WILDLIFE tick=" + NativeTick());
        }

        // args: area size, milliseconds, inside or free. Measures how far leaders get and whether routes leave the area.
        private static IEnumerable<Wait> TrackRange(Step step)
        {
            int size = step.Int(0), end = Environment.TickCount + step.Int(1), reach = size / 2;
            bool inside = step.Arg(2) == "inside";
            int allowedOver = step.Int(3);
            var farthest = new Dictionary<int, int>();
            int outsideRoutes = 0, samples = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine()) outsideRoutes += SampleRange(reach, farthest);
                samples++;
                yield return new Delay(200);
            }
            int deepest = farthest.Count == 0 ? 0 : farthest.Values.Max();
            var byKind = farthest.GroupBy(pair => herdStarts[pair.Key].Kind).Select(g => "0x" + g.Key.ToString("X") + ":max=" + g.Max(p => p.Value) + ",over=" + g.Count(p => p.Value > reach) + "/" + g.Count());
            AcceptLog.Info("HUNT_RANGE size=" + size + " samples=" + samples + " max=" + deepest + " routesOutside=" + outsideRoutes + " " + string.Join(" ", byKind));
            Runner.Require(farthest.Count > 0, "no herd was measured");
            if (inside)
            {
                Runner.Require(outsideRoutes == 0, outsideRoutes + " route points outside the area");
                Runner.Require(deepest <= reach + allowedOver, "a leader went " + deepest + " tiles from its start");
            }
            else
                Runner.Require(deepest > reach + allowedOver, "without the limit herds stayed within " + deepest + " tiles");
        }

        // args: milliseconds, highest or lowest allowed share of herds in flight in percent. Reports growth as well.
        private static IEnumerable<Wait> TrackFlight(Step step)
        {
            int end = Environment.TickCount + step.Int(0), limit = step.Int(1);
            bool atMost = step.Arg(2) == "atmost";
            long samples = 0, fleeing = 0;
            int first = -1, last = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine())
                {
                    SampleFlight(ref samples, ref fleeing);
                    last = LivingWildlife();
                    if (first < 0)
                        first = last;
                }
                yield return new Delay(500);
            }
            long share = samples == 0 ? 0 : fleeing * 100 / samples;
            AcceptLog.Info("HUNT_FLIGHT share=" + share + "% samples=" + samples + " livingFirst=" + first + " livingLast=" + last);
            Runner.Require(samples > 0, "no herd was measured");
            Runner.Require(atMost ? share <= limit : share >= limit, "herds in flight: " + share + "%");
        }

        private static void SampleFlight(ref long samples, ref long fleeing)
        {
            foreach (HerdStart start in herdStarts.Values)
            {
                if (*(int*)(HerdRecord(start.Herd) + 0x34) != start.Serial || *(short*)(HerdRecord(start.Herd) + 0x40) != 2)
                    continue;
                samples++;
                if (*(short*)(HerdRecord(start.Herd) + HerdFleeingOffset) != 0)
                    fleeing++;
            }
        }

        private static int LivingWildlife()
        {
            CountWildlife(out int deerCount, out int goats);
            return deerCount + goats;
        }

        private static int SampleRange(int reach, Dictionary<int, int> farthest)
        {
            int outsideRoutes = 0;
            foreach (HerdStart start in herdStarts.Values)
            {
                if (*(int*)(HerdRecord(start.Herd) + 0x34) != start.Serial || !LeaderTile(start.Herd, out int x, out int y))
                    continue;

                int roam = Math.Max(Math.Abs(x - start.X), Math.Abs(y - start.Y));
                // Walls of an AI castle can cut a herd off from its start; it stays free on its side of them.
                Span<ushort> lands = GamePathingManagerAPI.Instance.GetPathComponentGrid();
                GameTileManagerAPI grid = GameTileManagerAPI.Instance;
                if (lands[grid.GetTileId(x, y)] != lands[grid.GetTileId(start.X, start.Y)])
                    continue;
                farthest.TryGetValue(start.Serial, out int known);
                if (roam > known)
                {
                    farthest[start.Serial] = roam;
                    // Each step of the herd that has gone farthest, to see how it got there.
                    if (roam > reach + 6 && roam > worstRoam)
                    {
                        worstRoam = roam;
                        byte* far = HerdRecord(start.Herd);
                        Span<ushort> land = GamePathingManagerAPI.Instance.GetPathComponentGrid();
                        GameTileManagerAPI map = GameTileManagerAPI.Instance;
                        AcceptLog.Info("HUNT_RANGE_FAR tick=" + NativeTick() + " herd=" + start.Herd + " start=" + start.X + "," + start.Y + " leader=" + x + "," + y + " roam=" + roam +
                            " flee=" + *(short*)(far + HerdFleeingOffset) + " route=" + *(short*)(far + 0x5DC) + "/" + *(short*)(far + 0x5DE) + " first=" + *(short*)(far + 0x5B4) + "," + *(short*)(far + 0x5B6) +
                            " landLeader=" + land[map.GetTileId(x, y)] + " landStart=" + land[map.GetTileId(start.X, start.Y)]);
                    }
                }
                byte* record = HerdRecord(start.Herd);
                int count = *(short*)(record + 0x5DE);
                if (roam > reach)
                    continue;
                // The mod reads the start at its first tick, a few tiles on for a herd already in flight.
                for (int point = 0; point < count && point < 10; point++)
                {
                    if (Math.Max(Math.Abs(*(short*)(record + 0x5B4 + point * 4) - start.X), Math.Abs(*(short*)(record + 0x5B6 + point * 4) - start.Y)) > reach + StartDifference)
                    {
                        // A flight that began before the mission had started: nothing could limit that route yet.
                        int flee = *(short*)(record + HerdFleeingOffset);
                        if (flee > 0 && NativeTick() - flee <= BeforeMissionStartTicks)
                            continue;
                        outsideRoutes++;
                        if (routeReports++ < 6)
                            AcceptLog.Info("HUNT_ROUTE_OUTSIDE herd=" + start.Herd + " serial=" + start.Serial + " start=" + start.X + "," + start.Y + " leader=" + x + "," + y +
                                " tick=" + NativeTick() + " flee=" + flee + " point=" + point + "/" + count + " at=" + *(short*)(record + 0x5B4 + point * 4) + "," + *(short*)(record + 0x5B6 + point * 4));
                    }
                }
            }
            return outsideRoutes;
        }

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate long HerdGoDelegate(IntPtr manager, int herd, uint x, uint y, int a, int b, int c);

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        private delegate int SpawnHerdDelegate(IntPtr manager, int kind, uint x, uint y, int height);

        // args: deer or goat, optionally the tile to start looking from. Vanilla's herd creation, as the editor's placing tool calls it.
        private static int SpawnHerdAt(string kind, int x, int y)
        {
            var spawn = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<SpawnHerdDelegate>((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + SpawnHerdRva));
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int herd = spawn((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva), kind == "goat" ? 0xE2 : 0xDC, (uint)x, (uint)y, tiles.GetTileHeight(tiles.GetTileId(x, y)));
            AcceptLog.Info("HUNT_SPAWNED herd=" + herd + " at=" + x + "," + y);
            return herd;
        }

        private static void SpawnHerd(string kind, string centreX, string centreY)
        {
            var spawn = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<SpawnHerdDelegate>((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + SpawnHerdRva));
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            byte* grid = (byte*)GetModuleHandle("CrusaderDE.dll") + SectorGridRva;
            for (int reach = 0; reach < 160; reach += 10)
            {
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = int.Parse(centreX) + dx * reach, y = int.Parse(centreY) + dy * reach, tile = tiles.GetTileId(x, y);
                    // In a quiet sector, or Vanilla's rule sends the herd running at once and it never rests.
                    if (!tiles.IsTileInsideMapBounds(x, y) || !tiles.IsValidTileId(tile) || *(int*)(grid + (x / 10 * 80 + y / 10) * SectorStride + SectorUnitsOffset) != 0)
                        continue;
                    int herd = spawn((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva), kind == "goat" ? 0xE2 : 0xDC, (uint)x, (uint)y, tiles.GetTileHeight(tile));
                    if (herd > 0)
                    {
                        AcceptLog.Info("HUNT_SPAWNED herd=" + herd + " at=" + x + "," + y);
                        return;
                    }
                }
            }
            Runner.Require(false, "no place for the herd");
        }

        // args: tiles. Sends the fixture away with Vanilla's own herd order, as if it had got there by itself.
        private static void SendHerdAway(int distance)
        {
            var go = System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<HerdGoDelegate>((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + HerdGoRva));
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (-1, -1) })
            {
                int x = herdFixture.X + dx * distance, y = herdFixture.Y + dy * distance;
                if (x > 0 && y > 0 && x < 800 && y < 800 && go((IntPtr)((byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva), herdFixture.Herd, (uint)x, (uint)y, 0, 0, 0) != 0)
                {
                    if (herdSends++ == 0)
                        AcceptLog.Info("HUNT_SENT herd=" + herdFixture.Herd + " to=" + x + "," + y);
                    return;
                }
            }
            Runner.Require(false, "the herd took no order");
        }

        // args: area size. The fixture first gets out and then comes home on its own.
        private static IEnumerable<Wait> ExpectHerdBack(Step step)
        {
            int reach = step.Int(0) / 2, farthest = 0;
            // A calm herd soon takes its next route, which replaces one order, so the order is repeated until it is out.
            int nextOrder = 0, distance = step.Int(1);
            yield return new Until("the herd outside its area", () =>
            {
                lock (Engine())
                {
                    farthest = RoamOf(herdFixture);
                    if (farthest > reach)
                        return true;
                    if (Environment.TickCount - nextOrder >= 0)
                    {
                        nextOrder = Environment.TickCount + 100;
                        SendHerdAway(distance);
                    }
                    return false;
                }
            });
            lock (Engine()) AcceptLog.Info("HUNT_OUTSIDE roam=" + farthest + " tick=" + NativeTick());
            yield return new Until("the herd back in its area", () => { lock (Engine()) { int roam = RoamOf(herdFixture); return roam >= 0 && roam <= reach; } });
            lock (Engine()) AcceptLog.Info("HUNT_BACK roam=" + RoamOf(herdFixture) + " tick=" + NativeTick());
        }

        private static void LogAnimals()
        {
            var counts = new Dictionary<eChimps, int>();
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive &&
                    (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER || unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT ||
                     unit->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT || unit->r_UnitChimp == eChimps.CHIMP_TYPE_CAMEL))
                {
                    counts.TryGetValue(unit->r_UnitChimp, out int count);
                    counts[unit->r_UnitChimp] = count + 1;
                }
            }
            AcceptLog.Info("HUNT_ANIMALS " + string.Join(" ", counts.Select(pair => pair.Key + "=" + pair.Value)));
        }

        // The Hunter's hut as a player builds it: its button on the food page, then a click on the map.
        private static IEnumerable<Wait> BuildHutByHand(Step step)
        {
            int local = Local();
            MainViewModel.Instance.HUDmain.SetupNewBuildScreen(FoodBuildScreen);
            yield return new Delay(300);
            var button = GameXAMLManagerAPI.Instance.FindGlobalElement("ButtonBuildHunter") as Noesis.Button;
            Runner.Require(button != null && button.IsVisible && button.IsEnabled, "the Hunter's hut button is not available");
            AcceptLog.Info("HUNT_HUT_BUTTON visible=" + button.IsVisible + " enabled=" + button.IsEnabled);
            Noesis.Point center = button.PointToScreen(new Noesis.Point((float)button.ActualWidth / 2, (float)button.ActualHeight / 2));
            NoesisView view = FatControler.instance.NGview;
            view.MouseMove((int)center.X, (int)center.Y);
            yield return new Delay(200);
            view.MouseButtonDown((int)center.X, (int)center.Y, Noesis.MouseButton.Left);
            view.MouseButtonUp((int)center.X, (int)center.Y, Noesis.MouseButton.Left);
            yield return new Delay(300);
            (int x, int y) = LordTile(local);
            foreach ((int dx, int dy) in new[] { (14, 0), (-14, 0), (0, 14), (0, -14), (14, 14), (-14, -14), (20, -20), (-20, 20) })
            {
                foreach (Wait wait in InputSteps.ClickTile(x + dx, y + dy)) yield return wait;
                yield return new Delay(400);
                bool placed;
                lock (Engine()) placed = TryFindBuilding(local, eStructs.STRUCT_HUNTERS_HUT, out _, out _);
                if (placed)
                {
                    AcceptLog.Info("HUNT_HUT_BUILT at=" + (x + dx) + "," + (y + dy));
                    yield break;
                }
            }
            throw new StepFailure("no click placed the Hunter's hut");
        }

        private static IEnumerable<Wait> ExpectHutButtonDisabled(Step step)
        {
            MainViewModel.Instance.HUDmain.SetupNewBuildScreen(FoodBuildScreen);
            yield return new Delay(300);
            var button = GameXAMLManagerAPI.Instance.FindGlobalElement("ButtonBuildHunter") as Noesis.Button;
            AcceptLog.Info("HUNT_HUT_BUTTON found=" + (button != null) + " enabled=" + button?.IsEnabled);
            Runner.Require(button != null && !button.IsEnabled, "Vanilla's rule should disable the Hunter's hut on a map without prey");
        }

        private static void CountWildlife(out int deerCount, out int goats)
        {
            deerCount = 0;
            goats = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0)
                    continue;
                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER)
                    deerCount++;
                else if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT)
                    goats++;
            }
        }

        // args: minimum living deer and goats together.
        private static IEnumerable<Wait> ExpectWildlife(Step step)
        {
            int minimum = step.Int(0), deerCount = 0, goats = 0;
            yield return new Until("wildlife on the map", () =>
            {
                lock (Engine()) CountWildlife(out deerCount, out goats);
                return deerCount + goats >= minimum;
            });
            AcceptLog.Info("HUNT_WILDLIFE deer=" + deerCount + " goats=" + goats);
        }

        // args: expected carried meat, or that plus one from a bonus fraction.
        private static IEnumerable<Wait> ExpectHunterCarries(Step step)
        {
            int local = Local(), expected = step.Int(0), carried = 0;
            yield return new Until("Hunter carries meat", () =>
            {
                lock (Engine()) carried = HunterCarry(local);
                return carried > 0;
            });
            AcceptLog.Info("HUNT_CARRIES " + carried);
            Runner.Require(carried == expected || carried == expected + 1, "the Hunter carried " + carried + ", expected " + expected);
        }

        private static GameUnit* FindHerdLeader(out int herd)
        {
            herd = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_CurrentHealth > 0 && unit->r_TribeId > 0 &&
                    (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER || unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT))
                {
                    herd = unit->r_TribeId;
                    // Vanilla reads the sector of the herd's leader, not of just any member.
                    byte* record = (byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva + herd * HerdStride;
                    return APIShared.UnitAccess.TryGetById(*(short*)(record + HerdLeaderOffset), out GameUnit* leader, out _) ? leader : unit;
                }
            }
            return null;
        }

        // args: walled | open. A soldier in the herd's 10x10 sector; a ring is closed before he stands in it.
        // Not every spot takes a wall and the herd moves in bursts, so each try starts from a resting herd.
        private static IEnumerable<Wait> UnitInHerdSector(Step step)
        {
            bool walled = step.Arg(0) == "walled", closed = !walled;
            int x = 0, y = 0;
            if (!walled)
            {
                // A herd on its way would leave the soldier behind before Vanilla's sector count sees him.
                foreach (Wait wait in LeaderRests())
                    yield return wait;
            }
            while (!closed)
            {
                foreach (Wait wait in LeaderRests())
                    yield return wait;
                ringTry++;
                lock (Engine())
                {
                    FindSoldierTile(true, out x, out y);
                    AlternateCorner(ringTry, ref x, ref y);
                    BuildRing(x, y);
                }
                int sector = x / 10 * 80 + y / 10, end = Environment.TickCount + RingAttemptMs;
                bool sameSector = true;
                while (!closed && sameSector && Environment.TickCount < end)
                {
                    yield return NextFrame.Instance;
                    lock (Engine())
                    {
                        closed = RingIsClosed(x, y);
                        sameSector = LeaderSector() == sector;
                    }
                }
                closed = closed && sameSector;
                string walls = "";
                lock (Engine()) walls = HerdRingWalls(x, y);
                AcceptLog.Info("HUNT_RING at=" + x + "," + y + " closed=" + closed + " sameSector=" + sameSector + " " + walls);
            }
            if (!walled)
            {
                lock (Engine()) FindSoldierTile(false, out x, out y);
            }
            lock (Engine()) PlaceSoldier(walled, x, y);
        }

        // args: deer or goat. Closes the ring on empty land first and only then sets the herd beside it, in the same
        // sector and outside the ring. A wandering herd leaves its sector before a ring can close around a corner of it.
        private static IEnumerable<Wait> WalledUnitThenHerd(Step step)
        {
            // Near the own keep, where the game lets the player build walls.
            int doorX = 0, doorY = 0;
            lock (Engine())
            {
                var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(Local());
                doorX = door.X / 10 * 10;
                doorY = door.Y / 10 * 10;
            }
            foreach ((int dx, int dy) in RingPlaces())
            {
                int x = doorX + 2 + dx, y = doorY + 5 + dy;
                bool usable = false;
                lock (Engine())
                {
                    usable = IsOpenRingPlace(x, y);
                    if (usable)
                        BuildRing(x, y);
                }
                if (!usable)
                    continue;

                int end = Environment.TickCount + RingAttemptMs;
                bool closed = false;
                while (!closed && Environment.TickCount < end)
                {
                    yield return new Delay(100);
                    lock (Engine()) closed = AreParted(x, y, x + 6, y);
                }
                AcceptLog.Info("HUNT_RING_FIRST at=" + x + "," + y + " closed=" + closed);
                if (!closed)
                    continue;

                int herd = 0;
                lock (Engine()) herd = SpawnHerdAt(step.Arg(0), x + 6, y);
                if (herd <= 0)
                    continue;

                // A new animal only counts as alive after a moment, and the soldier takes his height from it.
                yield return new Delay(300);
                lock (Engine()) PlaceSoldier(true, x, y);
                yield break;
            }
            Runner.Require(false, "no place took a closed ring and a herd");
        }

        private static IEnumerable<(int X, int Y)> RingPlaces()
        {
            for (int reach = 20; reach <= 100; reach += 20)
            {
                for (int a = -reach; a <= reach; a += 20)
                {
                    for (int b = -reach; b <= reach; b += 20)
                    {
                        if (Math.Max(Math.Abs(a), Math.Abs(b)) == reach)
                            yield return (a, b);
                    }
                }
            }
        }

        private static bool IsOpenRingPlace(int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x - 2, y - 2) || !tiles.IsTileInsideMapBounds(x + 7, y + 2))
                return false;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int land = components[tiles.GetTileId(x, y)];
            byte* grid = (byte*)GetModuleHandle("CrusaderDE.dll") + SectorGridRva;
            return land != 0 && land == components[tiles.GetTileId(x + 6, y)] &&
                *(int*)(grid + (x / 10 * 80 + y / 10) * SectorStride + SectorUnitsOffset) == 0;
        }

        private static bool AreParted(int x, int y, int otherX, int otherY)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int inner = components[tiles.GetTileId(x, y)], outer = components[tiles.GetTileId(otherX, otherY)];
            return inner != 0 && outer != 0 && inner != outer;
        }

        private static IEnumerable<Wait> LeaderRests()
        {
            int since = Environment.TickCount, last = -1;
            yield return new Until("the herd's leader rests", () =>
            {
                lock (Engine())
                {
                    int here = LeaderTile();
                    if (here != last)
                    {
                        last = here;
                        since = Environment.TickCount;
                    }
                    return Environment.TickCount - since >= CalmLeaderMs;
                }
            });
        }

        // The corner farthest from the leader first; a corner beside him is no use and is skipped by the next try.
        private static void AlternateCorner(int attempt, ref int x, ref int y)
        {
            if (attempt % 3 == 1)
                x = x / 10 * 10 + (x % 10 == 7 ? 2 : 7);
            else if (attempt % 3 == 2)
                y = y / 10 * 10 + (y % 10 == 7 ? 2 : 7);
        }

        private static int LeaderTile()
        {
            GameUnit* animal = FindHerdLeader(out _);
            Runner.Require(animal != null, "no herd");
            return animal->r_CurrentTilePositionX * 10000 + animal->r_CurrentTilePositionY;
        }

        private static string HerdRingWalls(int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            GameUnit* animal = FindHerdLeader(out _);
            int home = components[tiles.GetTileId(animal->r_CurrentTilePositionX, animal->r_CurrentTilePositionY)], walls = 0;
            for (int ox = -2; ox <= 2; ox++)
            {
                for (int oy = -2; oy <= 2; oy++)
                {
                    if ((Math.Abs(ox) == 2 || Math.Abs(oy) == 2) && components[tiles.GetTileId(x + ox, y + oy)] != home)
                        walls++;
                }
            }
            return "walls=" + walls + "/16 inner=" + components[tiles.GetTileId(x, y)] + " home=" + home;
        }

        private static int LeaderSector()
        {
            GameUnit* animal = FindHerdLeader(out _);
            Runner.Require(animal != null, "no herd");
            return animal->r_CurrentTilePositionX / 10 * 80 + animal->r_CurrentTilePositionY / 10;
        }

        private static void FindSoldierTile(bool walled, out int x, out int y)
        {
            GameUnit* animal = FindHerdLeader(out _);
            Runner.Require(animal != null, "no herd");
            int sectorX = animal->r_CurrentTilePositionX / 10 * 10, sectorY = animal->r_CurrentTilePositionY / 10 * 10;
            // The sector corner farthest from the animal, so the ring does not close around it.
            x = sectorX + (animal->r_CurrentTilePositionX % 10 < 5 ? 7 : 2);
            y = sectorY + (animal->r_CurrentTilePositionY % 10 < 5 ? 7 : 2);
            if (!walled)
                FindOpenTileBesideHerd(animal, sectorX, sectorY, out x, out y);
        }

        // In the leader's sector and on the land he stands on: a ring can cut him off from the tile next to him,
        // and a unit that cannot reach the herd must not make it run.
        private static void FindOpenTileBesideHerd(GameUnit* animal, int sectorX, int sectorY, out int x, out int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int leaderX = animal->r_CurrentTilePositionX, leaderY = animal->r_CurrentTilePositionY;
            int home = components[tiles.GetTileId(leaderX, leaderY)];
            for (int reach = 1; reach < 10; reach++)
            {
                for (int ox = -reach; ox <= reach; ox++)
                {
                    for (int oy = -reach; oy <= reach; oy++)
                    {
                        x = leaderX + ox;
                        y = leaderY + oy;
                        if (Math.Max(Math.Abs(ox), Math.Abs(oy)) != reach || x / 10 * 10 != sectorX || y / 10 * 10 != sectorY)
                            continue;
                        int tile = tiles.GetTileId(x, y);
                        if (tiles.IsValidTileId(tile) && components[tile] == home)
                        {
                            AcceptLog.Info("HUNT_OPEN_TILE at=" + x + "," + y + " home=" + home + " reach=" + reach);
                            return;
                        }
                    }
                }
            }
            x = y = 0;
            Runner.Require(false, "no tile on the herd's land in its sector");
        }

        private static void BuildRing(int x, int y)
        {
            int local = Local();
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            api.CreateWall(local, x - 2, y - 2, x + 2, y - 2, eMappers.MAPPER_WALL);
            api.CreateWall(local, x + 2, y - 2, x + 2, y + 2, eMappers.MAPPER_WALL);
            api.CreateWall(local, x + 2, y + 2, x - 2, y + 2, eMappers.MAPPER_WALL);
            api.CreateWall(local, x - 2, y + 2, x - 2, y - 2, eMappers.MAPPER_WALL);
        }

        private static bool RingIsClosed(int x, int y)
        {
            GameUnit* animal = FindHerdLeader(out _);
            Runner.Require(animal != null, "no herd");
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int inner = components[tiles.GetTileId(x, y)];
            return inner != 0 && inner != components[tiles.GetTileId(animal->r_CurrentTilePositionX, animal->r_CurrentTilePositionY)];
        }

        private static void PlaceSoldier(bool walled, int x, int y)
        {
            int local = Local();
            GameUnit* animal = FindHerdLeader(out int herd);
            Runner.Require(animal != null, "no herd");
            Runner.Require(UnitAccessHeight(animal, out int height), "no herd height");
            long id = GameUnitManagerAPI.Instance.CreateUnitLocal(local, local, x, y, height, eChimps.CHIMP_TYPE_SPEARMAN);
            Runner.Require(id > 0, "no soldier");
            soldierSector = walled ? x / 10 * 80 + y / 10 : -1;
            AcceptLog.Info("HUNT_SECTOR_UNIT walled=" + walled + " at=" + x + "," + y + " herd=" + herd + " animalAt=" + animal->r_CurrentTilePositionX + "," + animal->r_CurrentTilePositionY);
        }

        private static bool UnitAccessHeight(GameUnit* unit, out int height)
        {
            height = unit->r_HeightElevation;
            return true;
        }

        private static bool HerdIsFleeing(int herd) =>
            *(short*)((byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva + herd * HerdStride + HerdFleeingOffset) != 0;

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string name);

        // args: milliseconds, rounds. The Hunter closes in on one live deer, then it is far away again; his Vanilla stage has to follow both ways.
        private static IEnumerable<Wait> SpeedBothWays(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0);
            speedTarget = speedLastStage = speedRounds = 0;
            speedSlowed = false;
            int rounds = step.Int(1);
            yield return new Until("Hunter at work", () =>
            {
                lock (Engine()) return HasHunter(local);
            });
            lock (Engine()) SpawnSpeedDeer(local);
            while (Environment.TickCount < end && speedRounds < rounds)
            {
                lock (Engine()) SpeedTick(local);
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_SPEED_ROUNDS " + speedRounds);
            Runner.Require(speedRounds >= rounds, "the Hunter's stage did not follow the distance in both directions");
        }

        private static bool HasHunter(int local) => FindHunter(local) != null;

        private static IEnumerable<Wait> AxisDeer(Step step)
        {
            int local = Local();
            yield return new Until("Hunter at work", () =>
            {
                lock (Engine()) return HasHunter(local);
            });
            lock (Engine()) SpawnSpeedDeer(local);
            int nextLog = 0;
            yield return new Until("Hunter on his way to the deer", () =>
            {
                lock (Engine())
                {
                    if (Environment.TickCount >= nextLog)
                    {
                        nextLog = Environment.TickCount + 5000;
                        LogCorpseWait(local);
                    }
                    return HunterDeerTarget(local, out int state) != 0 && state == 1;
                }
            });
        }

        private static void SpawnSpeedDeer(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            int x = hunter->r_CurrentTilePositionX + (hunter->r_CurrentTilePositionX < 200 ? SpeedDeerDistance : -SpeedDeerDistance);
            // On one axis, so the Vanilla stages start well before the shooting range.
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x, hunter->r_CurrentTilePositionY, hunter->r_HeightElevation, eChimps.CHIMP_TYPE_DEER));
            Runner.Require(id > 0, "speed deer missing");
            deer.Add(id);
        }

        private static void SpeedTick(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            int current = HunterDeerTarget(local, out int state);
            if (current == 0 || state != 1 || !APIShared.UnitAccess.TryGetById(current, out GameUnit* prey, out _))
                return;

            if (speedTarget == 0)
            {
                speedTarget = current;
                speedGlobal = prey->r_GlobalId;
            }
            Runner.Require(current == speedTarget && prey->r_GlobalId == speedGlobal,
                "the Hunter changed his target: " + speedTarget + "/" + speedGlobal + " to " + current + "/" + prey->r_GlobalId + " " + prey->r_UnitChimp);
            int stage = *(ushort*)((byte*)hunter + HunterSpeedStageOffset);
            int dx = prey->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX;
            int dy = prey->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY;
            if (stage != speedLastStage)
            {
                AcceptLog.Info("HUNT_SPEED stage=" + stage + " manhattan=" + (Math.Abs(dx) + Math.Abs(dy)) + " chebyshev=" + Math.Max(Math.Abs(dx), Math.Abs(dy)) +
                    " speed=" + hunter->r_CurrentSpeed + " round=" + speedRounds + " target=" + speedTarget);
                speedLastStage = stage;
            }
            if (!speedSlowed && stage >= 2)
            {
                speedSlowed = true;
                var away = new UnmanagedVector2<ushort>((ushort)(prey->r_CurrentTilePositionX + Math.Sign(dx) * SpeedDeerStep), (ushort)prey->r_CurrentTilePositionY);
                if (APIShared.UnitAccess.TryGetById(speedTarget, out _, out _))
                    GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition(speedTarget, away);
                AcceptLog.Info("HUNT_SPEED_AWAY to=" + away.X + "," + away.Y);
            }
            else if (speedSlowed && stage == 1)
            {
                speedSlowed = false;
                speedRounds++;
            }
        }

        // args: deer | goat | rabbit | chicken, distance from the hut. An animal killed by nobody's Hunter.
        private static void SpawnCorpse(string kind, int distance)
        {
            int local = Local();
            Runner.Require(TryFindBuilding(local, eStructs.STRUCT_HUNTERS_HUT, out int x, out int y), "no hut for the corpse");
            eChimps type = PreyType(kind);
            Runner.Require(APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no local lord");
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x + distance, y, lord->r_HeightElevation, type));
            Runner.Require(id > 0, "corpse fixture missing");
            if (APIShared.UnitAccess.TryGetById(id, out _, out _))
                GameUnitManagerAPI.Instance.KillUnit(id);
            corpseFixture = id;
            Runner.Require(APIShared.UnitAccess.TryGetById(id, out GameUnit* corpse, out _), "the killed animal is gone");
            corpseFixtureGlobal = corpse->r_GlobalId;
            AcceptLog.Info("HUNT_CORPSE id=" + id + " type=" + type + " dead=" + *(ushort*)((byte*)corpse + CorpseOffset) +
                " reservation=" + *(ushort*)((byte*)corpse + 0x448) + " at=" + corpse->r_CurrentTilePositionX + "," + corpse->r_CurrentTilePositionY);
            Runner.Require(*(ushort*)((byte*)corpse + CorpseOffset) != 0, "the killed animal is no corpse");
        }

        // args: milliseconds. Another Hunter's claim, renewed as Vanilla renews it and with the corpse kept: nobody may take it.
        private static IEnumerable<Wait> HoldForeignReservation(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), count = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine()) count = RenewForeignReservation(local, 2);
                Runner.Require(count == 0, "a Hunter stole a reserved corpse");
                yield return NextFrame.Instance;
            }
            lock (Engine()) RenewForeignReservation(local, 0);
            AcceptLog.Info("HUNT_RESERVATION_HELD");
        }

        private static int RenewForeignReservation(int local, int value)
        {
            Runner.Require(APIShared.UnitAccess.TryGetById(corpseFixture, out GameUnit* corpse, out _), "no corpse");
            *(ushort*)((byte*)corpse + 0x448) = (ushort)value;
            *(ushort*)((byte*)corpse + 0x2C4) = 0;
            return HuntersOnCorpse(local, out _, out _);
        }

        private static IEnumerable<Wait> SecondHut(Step step)
        {
            int local = Local(), before = 0;
            lock (Engine())
            {
                before = CountBuildings(local, eStructs.STRUCT_HUNTERS_HUT);
                Runner.Require(TryFindBuilding(local, eStructs.STRUCT_GRANARY, out int gx, out int gy), "no granary");
                Runner.Require(Place(local, gx, gy, eMappers.MAPPER_HUNTER, eStructs.STRUCT_HUNTERS_HUT, 14, out _, out _), "no site for a second hut");
            }
            yield return new Until("second Hunter is at work", () =>
            {
                lock (Engine()) return CountBuildings(local, eStructs.STRUCT_HUNTERS_HUT) > before && CountHunters(local) >= 2;
            });
        }

        // One line per Hunter of the local player: what he does and what he aims at.
        private static void LogHunters()
        {
            int local = Local();
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != local || unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                    continue;
                int target = *(ushort*)((byte*)unit + HunterTargetIdOffset);
                string targetType = target > 0 && target <= units.Length ? units.GetValuePointer(target - 1)->r_UnitChimp.ToString() : "-";
                AcceptLog.Info("HUNT_HUNTER id=" + (index + 1) + " state=" + unit->r_AIState + " target=" + target + "/" + targetType +
                    " fixture=" + deer.Contains(target) + " at=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY);
            }
        }

        private static int CountHunters(int local)
        {
            int count = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                    count++;
            }
            return count;
        }

        // How many of this player's Hunters are after the corpse.
        private static int HuntersOnCorpse(int local, out int reservation, out int firstHunter)
        {
            int count = 0;
            reservation = -1;
            firstHunter = 0;
            if (APIShared.UnitAccess.TryGetById(corpseFixture, out GameUnit* corpse, out _))
                reservation = *(ushort*)((byte*)corpse + 0x448);
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER &&
                    unit->r_AIState != 0 && *(ushort*)((byte*)unit + HunterTargetIdOffset) == corpseFixture)
                {
                    count++;
                    firstHunter = index + 1;
                }
            }
            return count;
        }

        // Exactly one Hunter goes for the corpse and holds Vanilla's reservation; none joins him meanwhile.
        private static IEnumerable<Wait> ExpectCorpseTaken(Step step)
        {
            int local = Local(), count = 0, reservation = -1, hunter = 0, most = 0;
            int nextLog = 0;
            yield return new Until("a Hunter takes the corpse", () =>
            {
                lock (Engine())
                {
                    count = HuntersOnCorpse(local, out reservation, out hunter);
                    if (Environment.TickCount >= nextLog)
                    {
                        nextLog = Environment.TickCount + 5000;
                        LogCorpseWait(local);
                    }
                }
                return count > 0;
            });
            AcceptLog.Info("HUNT_CORPSE_TAKEN hunter=" + hunter + " reservation=" + reservation);
            // Vanilla writes 2 and the countdown lowers it; the Hunter renews it while he is on his way.
            Runner.Require(reservation > 0, "the corpse holds no reservation");
            int end = Environment.TickCount + step.Int(0);
            while (Environment.TickCount < end)
            {
                lock (Engine()) most = Math.Max(most, HuntersOnCorpse(local, out reservation, out _));
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_CORPSE_HUNTERS most=" + most);
            Runner.Require(most <= 1, most + " Hunters went for one corpse");
        }

        private static void LogCorpseWait(int local)
        {
            MethodInfo resolve = typeof(GameSteps).GetMethod("ResolveStatic", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            object free = resolve.Invoke(null, new object[] { "ImprovedHunters.ImprovedHuntersPlugin::persistentRuntime.freeCorpses" });
            object patch = resolve.Invoke(null, new object[] { "ImprovedHunters.ImprovedHuntersPlugin::persistentRuntime.hunterCorpseChoicePatch" });
            string text = "free=" + (free as ICollection)?.Count + " patch=" + (patch != null) + " hunters=" + CountHunters(local);
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                {
                    object choice = patch?.GetType().GetMethod("HasChoice").Invoke(patch, new object[] { index + 1 });
                    text += " hunter=" + (index + 1) + " state=" + unit->r_AIState + " target=" + *(ushort*)((byte*)unit + HunterTargetIdOffset) + " choice=" + choice +
                        " at=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY;
                }
            }
            if (APIShared.UnitAccess.TryGetById(corpseFixture, out GameUnit* corpse, out _))
                text += " corpseState=" + corpse->r_AliveState + " dead=" + *(ushort*)((byte*)corpse + CorpseOffset) + " owner=" + corpse->r_ControllableForPlayerId + " timer=" + *(ushort*)((byte*)corpse + 0x2C4);
            else
                text += " corpse gone";
            AcceptLog.Info("HUNT_CORPSE_WAIT " + text);
        }

        // args: milliseconds. A deer corpse on its way out, as after a Hunter has reached it: nobody may start on it.
        private static IEnumerable<Wait> FadingCorpseIgnored(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), count = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine()) count = HoldFading(local);
                Runner.Require(count == 0, "a Hunter went for a corpse that is fading");
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_FADING_IGNORED");
        }

        private static int HoldFading(int local)
        {
            Runner.Require(APIShared.UnitAccess.TryGetById(corpseFixture, out GameUnit* corpse, out _), "no corpse");
            *(ushort*)((byte*)corpse + 0x2BC) = FadingCorpseState;
            *(ushort*)((byte*)corpse + 0x2C4) = 0;
            return HuntersOnCorpse(local, out _, out _);
        }

        // args: milliseconds. One carcass feeds one Hunter. The moment the first arrives, the corpse fades and no longer
        // holds a reservation; the others are sent searching exactly then, as they would at a bad moment.
        private static IEnumerable<Wait> ExpectOneCollector(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), forced = 0;
            var collectors = new HashSet<int>();
            while (Environment.TickCount < end)
            {
                lock (Engine()) WatchCollectors(local, collectors, ref forced);
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_COLLECTORS " + string.Join(",", collectors) + " forcedFrames=" + forced);
            Runner.Require(collectors.Count >= 1, "no Hunter went for the corpse");
            Runner.Require(forced > 0, "the first Hunter never reached the corpse");
            Runner.Require(collectors.Count == 1, collectors.Count + " Hunters collected one corpse");
        }

        private static void WatchCollectors(int local, HashSet<int> collectors, ref int forced)
        {
            bool arrived = false;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != local || unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                    continue;
                if (unit->r_AIState != 0 && *(ushort*)((byte*)unit + HunterTargetIdOffset) == corpseFixture &&
                    *(uint*)((byte*)unit + HunterTargetGlobalOffset) == corpseFixtureGlobal)
                {
                    if (collectors.Add(index + 1))
                        AcceptLog.Info("HUNT_COLLECTOR " + (index + 1) + " state=" + unit->r_AIState);
                    arrived |= unit->r_AIState == CollectingState;
                }
            }
            if (!arrived && forced == 0 || forced >= ForcedSearchFrames)
                return;

            forced++;
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER &&
                    (unit->r_AIState == FreshIdleState || unit->r_AIState == HutIdleState) && unit->r_CarryGoodAmount == 0)
                {
                    *(ushort*)((byte*)unit + 0x2BC) = 0;
                    *(ushort*)((byte*)unit + 0x2C4) = 0;
                }
            }
        }

        private static IEnumerable<Wait> ExpectCorpseIgnored(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), count = 0, reservation = -1;
            while (Environment.TickCount < end)
            {
                lock (Engine()) count = HuntersOnCorpse(local, out reservation, out _);
                Runner.Require(count == 0, "a Hunter went for a corpse he should leave");
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_CORPSE_IGNORED reservation=" + reservation);
        }

        // Walls cost stone and a wall line stops where it ends; the start stock lasts for four rings. This run only.
        private static void Supply()
        {
            GameBuildingManagerAPI.Instance.SetLowWallCostMultiplier(0f);
            GameBuildingManagerAPI.Instance.SetHighWallCostMultiplier(0f);
        }

        // For cost measurements: many animals and Hunters on the ground the player can reach.
        private static void Stress(int animals, int huts)
        {
            int local = Local();
            HutTile(out int x, out int y, out int height);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int spawned = 0, placed = 0, size = tiles.GetCurrentMapSize();
            for (int radius = 20; radius < 200 && spawned < animals; radius += 4)
            {
                for (int step = 0; step < 16 && spawned < animals; step++)
                {
                    int px = x + (int)(Math.Cos(step * Math.PI / 8) * radius), py = y + (int)(Math.Sin(step * Math.PI / 8) * radius);
                    if (!tiles.IsTileInsideMapBounds(px, py))
                        continue;
                    if (GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, px, py, height, step % 2 == 0 ? eChimps.CHIMP_TYPE_DEER : eChimps.CHIMP_TYPE_GOAT) > 0)
                        spawned++;
                }
            }
            for (int index = 0; index < huts; index++)
            {
                if (Place(local, x, y, eMappers.MAPPER_HUNTER, eStructs.STRUCT_HUNTERS_HUT, 12 + index * 6, out _, out _))
                    placed++;
                GameUnitManagerAPI.Instance.CreateUnitLocal(local, local, x, y + 3, height, eChimps.CHIMP_TYPE_PEASANT);
            }
            AcceptLog.Info("HUNT_STRESS mapSize=" + size + " animals=" + spawned + " huts=" + placed);
        }

        private static eChimps PreyType(string kind) =>
            kind == "goat" ? eChimps.CHIMP_TYPE_GOAT : kind == "rabbit" ? eChimps.CHIMP_TYPE_RABBIT : kind == "camel" ? eChimps.CHIMP_TYPE_CAMEL :
            kind == "chicken" ? eChimps.CHIMP_TYPE_CHICKEN : kind == "crocodile" ? eChimps.CHIMP_TYPE_CROCODILE :
            kind == "lion" ? eChimps.CHIMP_TYPE_LION : eChimps.CHIMP_TYPE_DEER;

        // APIShared huntable-prey consumer for tests. Registrations are process-held; the step runs at the
        // menu so the next mission freezes the type. args: kind.
        private static readonly Dictionary<string, APIShared.Wildlife.HuntablePreyRegistration> preyRegistrations =
            new Dictionary<string, APIShared.Wildlife.HuntablePreyRegistration>();
        private const int PreyLeaseOffset = 0x448;

        // args: kind, optional base meat (-1 = Vanilla's), optional options "own" and/or "remove" (comma separated).
        private static void RegisterPrey(string kind, int baseMeat, string options)
        {
            eChimps type = PreyType(kind);
            string key = kind + "/" + baseMeat + "/" + options;
            if (!preyRegistrations.TryGetValue(key, out var registration))
            {
                var definition = new APIShared.Wildlife.HuntablePreyDefinition((int)type);
                if (baseMeat >= 0) definition = definition.WithBaseMeat(baseMeat);
                if (options.Contains("own")) definition = definition.WithOwnPlayerPrey();
                if (options.Contains("remove")) definition = definition.WithCorpseRemovedWhenButchered();
                Runner.Require(APIShared.Wildlife.HuntablePrey.TryRegister(UiAcceptancePlugin.Guid,
                    "test-" + kind + (baseMeat >= 0 ? "-meat" + baseMeat : "") + (options.Length != 0 ? "-" + options.Replace(",", "-") : ""),
                    definition, out registration, out string reason), "register: " + reason);
                preyRegistrations.Add(key, registration);
            }
            Runner.Require(registration.Status != APIShared.Wildlife.HuntablePreyRegistrationStatus.Unsupported, "unsupported: " + registration.Diagnostic);
            var catalog = APIShared.Wildlife.HuntablePrey.Current;
            AcceptLog.Info("HUNT_PREY_REGISTERED kind=" + kind + " type=" + (int)type + " status=" + registration.Status +
                " huntable=" + catalog.IsHuntable((int)type) + " digest=" + catalog.Digest);
            Runner.Require(catalog.IsHuntable((int)type), "the registered type is not in the catalog");
        }

        // args: kind, offset from the hut. An animal owned by the local player, reachable for the Hunter.
        private static void SpawnOwnedPrey(string kind, int dx, int dy)
        {
            HutTile(out int x, out int y, out int height);
            int atX = x + dx, atY = y + dy, local = Local();
            MoveToHunterLand(ref atX, ref atY);
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(local, local, atX, atY, height, PreyType(kind)));
            Runner.Require(id > 0, "prey missing");
            Runner.Require(APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_ControllableForPlayerId == local, "the prey is not owned by the local player");
            deer.Add(id);
            AcceptLog.Info("HUNT_PREY_OWNED id=" + id + " type=" + PreyType(kind) + " owner=" + local + " at=" + atX + "," + atY);
        }

        // args: kind, maximum map ticks from the start of butchering until the corpse is gone, expected carried
        // meat. Proves that an owned animal is hunted, that its corpse is removed at once (Vanilla fades it for
        // 33 updates) and the yield. The removal is measured from the Hunter's state 2 on that corpse.
        private static IEnumerable<Wait> ExpectButcherRemoval(Step step)
        {
            eChimps type = PreyType(step.Arg(0));
            int maxTicks = step.Int(1), expectedCarry = step.Int(2), local = Local(), preyId = 0, butcherTick = -1, removedTick = -1, maxCarry = 0;
            uint preyGlobal = 0;
            lock (Engine()) Runner.Require(TryFindFixture(type, out preyId, out preyGlobal), "no fixture " + type);
            yield return new Until("butchered and removed", () =>
            {
                lock (Engine())
                {
                    if (butcherTick < 0 && IsButchering(local, preyGlobal)) butcherTick = NativeTick();
                    if (butcherTick >= 0 && removedTick < 0 && !IsLivingSlot(preyId, preyGlobal)) removedTick = NativeTick();
                    maxCarry = Math.Max(maxCarry, HunterCarry(local));
                    // The Hunter carries meat only after the hut has made it; stop early only when the removal is late.
                    return removedTick >= 0 && maxCarry > 0 || butcherTick >= 0 && removedTick < 0 && NativeTick() - butcherTick > maxTicks * 4;
                }
            });
            AcceptLog.Info("HUNT_BUTCHER type=" + type + " prey=" + preyId + " butcherTick=" + butcherTick + " removedAfter=" +
                (removedTick < 0 ? "never" : (removedTick - butcherTick).ToString()) + " maxCarried=" + maxCarry);
            Runner.Require(removedTick >= 0 && removedTick - butcherTick <= maxTicks, "the corpse was not removed within " + maxTicks + " ticks");
            Runner.Require(maxCarry == expectedCarry || maxCarry == expectedCarry + 1, "the Hunter carried " + maxCarry + ", expected " + expectedCarry);
        }

        private static bool TryFindFixture(eChimps type, out int id, out uint global)
        {
            foreach (int candidate in deer)
                if (APIShared.UnitAccess.TryGetById(candidate, out GameUnit* unit, out _) && unit->r_UnitChimp == type)
                { id = candidate; global = unit->r_GlobalId; return true; }
            id = 0; global = 0;
            return false;
        }

        private static bool IsButchering(int local, uint preyGlobal)
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit hunter = ref units[spanIndex];
                if (hunter.r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER && hunter.r_ControllableForPlayerId == local &&
                    hunter.r_AIState == 2 && hunter.r_AI_ContextTargetUnitGlobalId == preyGlobal)
                    return true;
            }
            return false;
        }

        // A corpse marked for deletion (3) or already removed no longer occupies its slot as this unit.
        private static bool IsLivingSlot(int id, uint global) =>
            APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_GlobalId == global && unit->r_AliveState == AliveState.IsAlive;

        // APIShared target policy for tests: refuses one type and counts what it saw. args: kind.
        private static readonly HashSet<string> refusedKinds = new HashSet<string>();
        private static int policyRefusals, policyNonHunters, policyPresentationQueries;

        private static void RefusePreyType(string kind)
        {
            int type = (int)PreyType(kind);
            if (refusedKinds.Add(kind))
                Runner.Require(APIShared.Wildlife.HunterTargetPolicies.TryRegister(UiAcceptancePlugin.Guid, "refuse-" + kind,
                    (in APIShared.Wildlife.HunterTargetCandidate candidate) => RefuseType(type, candidate), out string reason), "policy: " + reason);
            AcceptLog.Info("HUNT_POLICY refuse=" + kind + " type=" + type);
        }

        private static APIShared.Wildlife.HunterTargetDecision RefuseType(int type, in APIShared.Wildlife.HunterTargetCandidate candidate)
        {
            if (!candidate.IsSimulationQuery) Interlocked.Increment(ref policyPresentationQueries);
            if (!IsHunterUnit(candidate.HunterUnitId)) Interlocked.Increment(ref policyNonHunters);
            if (candidate.CandidateType != type) return APIShared.Wildlife.HunterTargetDecision.Default;
            Interlocked.Increment(ref policyRefusals);
            return APIShared.Wildlife.HunterTargetDecision.Refuse;
        }

        private static bool IsHunterUnit(int unitId) =>
            APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER;

        // args: kind. Every fixture of that kind is still alive, and the refusing policy was asked with real Hunter ids.
        private static void ExpectPreyAlive(string kind)
        {
            eChimps type = PreyType(kind);
            int alive = 0, fixtures = 0;
            foreach (int id in deer)
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_UnitChimp == type)
                {
                    fixtures++;
                    if (APIShared.UnitAccess.IsReallyAlive(unit) && *(ushort*)((byte*)unit + CorpseOffset) == 0) alive++;
                }
            AcceptLog.Info("HUNT_POLICY_RESULT kind=" + kind + " fixtures=" + fixtures + " alive=" + alive + " refusals=" + policyRefusals +
                " nonHunterIds=" + policyNonHunters + " presentationQueries=" + policyPresentationQueries);
            Runner.Require(fixtures > 0 && alive == fixtures, "a refused " + kind + " was killed");
            Runner.Require(policyRefusals > 0, "the policy never saw the refused " + kind);
            Runner.Require(policyNonHunters == 0, "the policy got " + policyNonHunters + " ids that are no Hunter");
        }

        // args: kind. Waits until an own Hunter holds a fixture of that type as its target.
        private static IEnumerable<Wait> ExpectTargetType(Step step)
        {
            eChimps type = PreyType(step.Arg(0));
            int local = Local(), hunterId = 0, preyId = 0;
            yield return new Until("Hunter targets a fixture " + type, () =>
            {
                lock (Engine()) return TryFindHunterOnFixture(local, type, out hunterId, out preyId);
            });
            int lease;
            lock (Engine()) lease = PreyLease(preyId);
            AcceptLog.Info("HUNT_TARGET_TYPE type=" + type + " hunter=" + hunterId + " prey=" + preyId + " lease=" + lease);
        }

        private static uint NextUnitId() => GameUnitManagerAPI.Instance.GetUnitManager().Pointer->r_NextUnitId;

        // The Hunter's reservation word of a unit, or -1 if the unit is gone.
        private static int PreyLease(int preyId) =>
            APIShared.UnitAccess.TryGetById(preyId, out GameUnit* prey, out _) ? *(short*)((byte*)prey + PreyLeaseOffset) : -1;

        private static bool TryFindHunterOnFixture(int local, eChimps type, out int hunterId, out int preyId)
        {
            hunterId = preyId = 0;
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit hunter = ref units[spanIndex];
                if (hunter.r_AliveState != AliveState.IsAlive || hunter.r_ControllableForPlayerId != local ||
                    hunter.r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || hunter.r_AI_ContextTargetUnitGlobalId == 0)
                    continue;
                foreach (int id in deer)
                    if (APIShared.UnitAccess.TryGetById(id, out GameUnit* prey, out _) && prey->r_UnitChimp == type &&
                        prey->r_GlobalId == hunter.r_AI_ContextTargetUnitGlobalId)
                    { hunterId = spanIndex + 1; preyId = id; return true; }
            }
            return false;
        }

        // args: kind, ticks the lease must stay held, maximum ticks until it is free after the Hunter is gone.
        // Proves APIShared's release: held while the Hunter renews it, zero soon after the Hunter dies.
        private static IEnumerable<Wait> ExpectLeaseReleased(Step step)
        {
            eChimps type = PreyType(step.Arg(0));
            int holdTicks = step.Int(1), maxTicks = step.Int(2), local = Local(), hunterId = 0, preyId = 0, start = 0, minHeld = int.MaxValue;
            lock (Engine())
            {
                Runner.Require(TryFindHunterOnFixture(local, type, out hunterId, out preyId), "no Hunter holds a fixture " + type);
                start = NativeTick();
            }
            bool lost = false;
            yield return new Until("lease held for " + holdTicks + " ticks", () =>
            {
                lock (Engine())
                {
                    if (!TryFindHunterOnFixture(local, type, out int h, out int p) || h != hunterId || p != preyId) { lost = true; return true; }
                    minHeld = Math.Min(minHeld, PreyLease(preyId));
                    return NativeTick() - start >= holdTicks;
                }
            });
            Runner.Require(!lost, "the Hunter dropped the fixture before the hold check ended");
            Runner.Require(minHeld > 0, "the held lease reached zero");
            int killed;
            bool hunterKilled;
            lock (Engine())
            {
                hunterKilled = TryKillUnit(hunterId);
                killed = NativeTick();
            }
            Runner.Require(hunterKilled, "the Hunter disappeared before it could be killed for the lease release check");
            int freedAt = -1;
            yield return new Until("lease released", () =>
            {
                lock (Engine())
                {
                    int lease = PreyLease(preyId);
                    Runner.Require(lease >= 0, "fixture gone");
                    if (lease == 0) freedAt = NativeTick();
                    return freedAt >= 0 || NativeTick() - killed > maxTicks * 4;
                }
            });
            uint nextUnitId;
            lock (Engine()) nextUnitId = NextUnitId();
            AcceptLog.Info("HUNT_LEASE type=" + type + " prey=" + preyId + " heldTicks=" + holdTicks + " minHeld=" + minHeld +
                " freedAfter=" + (freedAt < 0 ? "never" : (freedAt - killed).ToString()) + " nextUnitId=" + nextUnitId);
            Runner.Require(freedAt >= 0 && freedAt - killed <= maxTicks, "the lease was not released within " + maxTicks + " ticks");
        }

        private static void HutTile(out int x, out int y, out int height)
        {
            int local = Local();
            Runner.Require(TryFindBuilding(local, eStructs.STRUCT_HUNTERS_HUT, out x, out y), "no hut");
            Runner.Require(APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no local lord");
            height = lord->r_HeightElevation;
        }

        // args: kind, offset from the hut. Hunters only take chickens of their own player.
        private static void SpawnPrey(string kind, int dx, int dy)
        {
            HutTile(out int x, out int y, out int height);
            eChimps type = PreyType(kind);
            int owner = type == eChimps.CHIMP_TYPE_CHICKEN ? Local() : 0;
            int atX = dx == int.MinValue ? ringX : x + dx, atY = dx == int.MinValue ? ringY : y + dy;
            // A fixture must be reachable for the Hunter, or he rightly ignores it. Ring fixtures stay where the ring is.
            if (dx != int.MinValue)
                MoveToHunterLand(ref atX, ref atY);
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, atX, atY, height, type));
            Runner.Require(id > 0, "prey missing");
            deer.Add(id);
            AcceptLog.Info("HUNT_PREY id=" + id + " type=" + type + " at=" + atX + "," + atY);
        }

        // args: kind. A fixture on the far side of the own keep as seen from the hut, so the keep stands between
        // the Hunter's way in and the prey in every match, whatever the start position.
        private static void SpawnPreyBehindKeep(string kind)
        {
            int local = Local();
            HutTile(out int hutX, out int hutY, out int height);
            Runner.Require(GameBuildingManagerAPI.Instance.TryGetBuildingById(GamePlayerManagerAPI.Instance.GetPlayerKeepId(local), out GameBuilding* keep) && keep != null, "no keep");
            int size = (int)keep->r_OccupyTileGridSize;
            int keepX = (int)keep->r_TilePositionXBegin + size / 2, keepY = (int)keep->r_TilePositionYBegin + size / 2;
            // Beside the keep's first row on the far side, as in the case found on 08.11.2026 (keep 400..406 x 275..281,
            // prey 399,275, Hunter from the east): the keep's corner cuts the line of sight from the near side.
            int x = keepX + Math.Sign(keepX - hutX) * (size / 2 + 1), y = (int)keep->r_TilePositionYBegin;
            MoveToHunterLand(ref x, ref y);
            eChimps type = PreyType(kind);
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x, y, height, type));
            Runner.Require(id > 0, "prey missing");
            deer.Add(id);
            AcceptLog.Info("HUNT_PREY id=" + id + " type=" + type + " at=" + x + "," + y + " keep=" + keepX + "," + keepY + " size=" + size + " hut=" + hutX + "," + hutY);
        }

        // args: kind, x, y. A fixture on an exact map tile, for places found in earlier runs; not moved.
        private static void SpawnPreyAt(string kind, int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Runner.Require(tiles.IsTileInsideMapBounds(x, y), "tile outside the map");
            eChimps type = PreyType(kind);
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x, y, tiles.GetTileHeight(tiles.GetTileId(x, y)), type));
            Runner.Require(id > 0, "prey missing");
            deer.Add(id);
            AcceptLog.Info("HUNT_PREY id=" + id + " type=" + type + " at=" + x + "," + y + " exact=True");
        }

        // The nearest walkable tile on the Hunter's own path component, at most 6 tiles from the wanted spot.
        private static void MoveToHunterLand(ref int x, ref int y)
        {
            GameUnit* hunter = FindHunter(Local());
            if (hunter == null)
                return;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int hunterTile = tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY);
            if (!tiles.IsValidTileId(hunterTile) || (uint)hunterTile >= (uint)components.Length || components[hunterTile] == 0)
                return;
            ushort land = components[hunterTile];
            for (int reach = 0; reach <= 6; reach++)
            {
                for (int ox = -reach; ox <= reach; ox++)
                {
                    for (int oy = -reach; oy <= reach; oy++)
                    {
                        if (Math.Max(Math.Abs(ox), Math.Abs(oy)) != reach || !tiles.IsTileInsideMapBounds(x + ox, y + oy))
                            continue;
                        int tile = tiles.GetTileId(x + ox, y + oy);
                        if (tiles.IsValidTileId(tile) && (uint)tile < (uint)components.Length && components[tile] == land && tiles.IsTileWalkableAndUnoccupied(tile))
                        {
                            if (reach > 0)
                                AcceptLog.Info("HUNT_PREY_MOVED from=" + x + "," + y + " to=" + (x + ox) + "," + (y + oy));
                            x += ox;
                            y += oy;
                            return;
                        }
                    }
                }
            }
            Runner.Require(false, "no reachable fixture spot near " + x + "," + y);
        }

        // A long wall across the straight line, passable around its ends.
        private static void SightWall(int dx, int halfLength)
        {
            HutTile(out int x, out int y, out _);
            GameBuildingManagerAPI.Instance.CreateWall(Local(), x + dx, y - halfLength, x + dx, y + halfLength, eMappers.MAPPER_WALL);
        }

        private static void SightBuilding(int dx, int dy)
        {
            HutTile(out int x, out int y, out _);
            int local = Local();
            Runner.Require(Place(local, x + dx, y + dy, eMappers.MAPPER_GRANARY, eStructs.STRUCT_GRANARY, 0, out int placedX, out int placedY, true), "no site for the blocker");
            AcceptLog.Info("HUNT_SIGHT_BUILDING at=" + placedX + "," + placedY);
            Runner.Require(Math.Abs(placedX - x - dx) <= 4 && Math.Abs(placedY - y - dy) <= 4, "the blocker landed away from the line of sight");
        }

        // args: plug | gap. A wall ring somewhere around the hut, with the east side's middle left out for a building that
        // can be removed later (walls cannot), or left open for now. Some ground takes no wall, so it tries other spots.
        private static IEnumerable<Wait> Ring(Step step)
        {
            bool plug = step.Arg(0) == "plug", ready = false;
            int local = Local();
            foreach ((int dx, int dy) in RingSpots())
            {
                bool tried = false;
                lock (Engine()) tried = TryBuildRing(local, dx, dy, plug);
                if (!tried)
                    continue;
                int end = Environment.TickCount + RingAttemptMs;
                while (!ready && Environment.TickCount < end)
                {
                    yield return new Delay(200);
                    lock (Engine()) ready = RingStands(local, plug);
                }
                lock (Engine()) LogRingWalls(local);
                if (ready)
                    break;
            }
            Runner.Require(ready, "no spot took the ring");
            AcceptLog.Info("HUNT_RING_BUILT at=" + ringX + "," + ringY + " plug=" + ringPlug);
        }

        private static IEnumerable<(int, int)> RingSpots()
        {
            foreach (int distance in new[] { 14, 22 })
            {
                foreach ((int dx, int dy) in new[] { (0, 1), (0, -1), (1, 1), (-1, 1), (1, -1), (-1, -1), (1, 0), (-1, 0) })
                    yield return (dx * distance, dy * distance);
            }
        }

        private static bool TryBuildRing(int local, int dx, int dy, bool plug)
        {
            HutTile(out int x, out int y, out _);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int cx = x + dx, cy = y + dy;
            if (!tiles.IsTileInsideMapBounds(cx, cy) || !RingGroundIsHome(local, cx, cy))
                return false;
            ringX = cx;
            ringY = cy;
            ringPlug = 0;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            api.CreateWall(local, ringX - RingRadius, ringY - RingRadius, ringX + RingRadius, ringY - RingRadius, eMappers.MAPPER_WALL);
            api.CreateWall(local, ringX - RingRadius, ringY + RingRadius, ringX + RingRadius, ringY + RingRadius, eMappers.MAPPER_WALL);
            api.CreateWall(local, ringX - RingRadius, ringY - RingRadius, ringX - RingRadius, ringY + RingRadius, eMappers.MAPPER_WALL);
            api.CreateWall(local, ringX + RingRadius, ringY - RingRadius, ringX + RingRadius, ringY - RingGapHalf - 1, eMappers.MAPPER_WALL);
            api.CreateWall(local, ringX + RingRadius, ringY + RingGapHalf + 1, ringX + RingRadius, ringY + RingRadius, eMappers.MAPPER_WALL);
            if (plug)
            {
                // A hovel's footprint runs from this tile on, so it fills the three-tile gap exactly.
                api.CreatePrefab(local, ringX + RingRadius, ringY - RingGapHalf, RingPlugMapper, BuildingScales.GetScale(RingPlugMapper), AutomaticSpriteVariation, true, true);
                ringPlug = tiles.GetTileBuildingId(tiles.GetTileId(ringX + RingRadius, ringY));
            }
            return true;
        }

        private static bool RingGroundIsHome(int local, int cx, int cy)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int home = components[tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY)];
            for (int ox = -RingRadius - 1; ox <= RingRadius + 1; ox++)
            {
                for (int oy = -RingRadius - 1; oy <= RingRadius + 1; oy++)
                {
                    if (components[tiles.GetTileId(cx + ox, cy + oy)] != home)
                        return false;
                }
            }
            return true;
        }

        private static void LogRingWalls(int local)
        {
            GameUnit* hunter = FindHunter(local);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int home = components[tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY)], walls = 0, tilesTried = 0;
            for (int offset = -RingRadius; offset <= RingRadius; offset++)
            {
                foreach (int tile in new[] { tiles.GetTileId(ringX + offset, ringY - RingRadius), tiles.GetTileId(ringX + offset, ringY + RingRadius), tiles.GetTileId(ringX - RingRadius, ringY + offset) })
                {
                    tilesTried++;
                    if (components[tile] != home)
                        walls++;
                }
            }
            string east = "";
            for (int offset = -RingGapHalf - 1; offset <= RingGapHalf + 1; offset++)
                east += " " + components[tiles.GetTileId(ringX + RingRadius, ringY + offset)];
            AcceptLog.Info("HUNT_RING_TRY at=" + ringX + "," + ringY + " walls=" + walls + "/" + tilesTried + " plug=" + ringPlug + " inner=" + components[tiles.GetTileId(ringX, ringY)] + " home=" + home + " east=" + east);
        }

        private static bool RingStands(int local, bool plug)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int home = components[tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY)];
            for (int offset = -RingRadius; offset <= RingRadius; offset++)
            {
                if (components[tiles.GetTileId(ringX + offset, ringY - RingRadius)] == home ||
                    components[tiles.GetTileId(ringX + offset, ringY + RingRadius)] == home ||
                    components[tiles.GetTileId(ringX - RingRadius, ringY + offset)] == home ||
                    (Math.Abs(offset) > RingGapHalf && components[tiles.GetTileId(ringX + RingRadius, ringY + offset)] == home))
                {
                    return false;
                }
            }
            return !plug || (ringPlug > 0 && RingCutOff(local));
        }

        private static bool RingCutOff(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int inner = components[tiles.GetTileId(ringX, ringY)];
            // A small closed area may carry no component of its own, which is 0.
            return inner != components[tiles.GetTileId(hunter->r_CurrentTilePositionX, hunter->r_CurrentTilePositionY)];
        }

        private static IEnumerable<Wait> CloseRing(Step step)
        {
            lock (Engine())
                GameBuildingManagerAPI.Instance.CreateWall(Local(), ringX + RingRadius, ringY - RingGapHalf - 1, ringX + RingRadius, ringY + RingGapHalf + 1, eMappers.MAPPER_WALL);
            yield return new Until("the closed ring cuts its inside off", () =>
            {
                lock (Engine()) return RingCutOff(Local());
            });
        }

        private static IEnumerable<Wait> OpenRing(Step step)
        {
            lock (Engine())
                Runner.Require(GameBuildingManagerAPI.Instance.DeleteBuildingSafe(ringPlug), "the plug would not go");
            yield return new Until("the ring is open again", () =>
            {
                lock (Engine()) return !RingCutOff(Local());
            });
        }

        // The Hunter gives up prey that became unreachable while he was on his way.
        private static IEnumerable<Wait> ExpectTargetDropped(Step step)
        {
            int local = Local(), start = Environment.TickCount, state = 0;
            yield return new Until("the Hunter drops the prey", () =>
            {
                lock (Engine()) return HunterDeerTarget(local, out state) == 0 || state != 1;
            });
            AcceptLog.Info("HUNT_TARGET_DROPPED state=" + state + " ms=" + (Environment.TickCount - start));
        }

        private static IEnumerable<Wait> WaitHunterFree(Step step)
        {
            int local = Local();
            yield return new Until("the Hunter has delivered and is idle", () =>
            {
                lock (Engine())
                {
                    HunterDeerTarget(local, out int state);
                    return HunterCarry(local) == 0 && (state == 0 || state == 6 || state == 7);
                }
            });
        }

        // args: milliseconds. Each of two Hunters takes his own fixture deer, and no two Hunters ever hold the same one.
        // Judged on game state, not on a shared moment in one state, so it does not depend on the game speed.
        private static IEnumerable<Wait> ExpectSeparateTargets(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), shared = 0;
            var targets = new Dictionary<int, int>();
            while (Environment.TickCount < end && !HasSeparateTargets(targets))
            {
                lock (Engine()) CountTargets(local, targets, ref shared);
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_TARGETS hunters=" + targets.Count + " distinct=" + new HashSet<int>(targets.Values).Count + " shared=" + shared);
            if (!HasSeparateTargets(targets))
                lock (Engine()) LogHunters();
            Runner.Require(shared == 0, "two Hunters hunted one animal");
            Runner.Require(HasSeparateTargets(targets), "the two Hunters did not each take a fixture deer of their own");
        }

        private static bool HasSeparateTargets(Dictionary<int, int> targets) => targets.Count >= 2 && new HashSet<int>(targets.Values).Count >= 2;

        // Records each Hunter's live fixture target; a target held by two Hunters in the same tick counts as shared.
        private static void CountTargets(int local, Dictionary<int, int> targets, ref int shared)
        {
            var holders = new HashSet<int>();
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != local || unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                    continue;
                int target = *(ushort*)((byte*)unit + HunterTargetIdOffset);
                // The field keeps the last target, whose ID may belong to a new unit by now.
                if (!deer.Contains(target) || !APIShared.UnitAccess.TryGetById(target, out GameUnit* prey, out _) ||
                    prey->r_GlobalId != *(uint*)((byte*)unit + HunterTargetGlobalOffset))
                {
                    continue;
                }
                targets[index + 1] = target;
                if (!holders.Add(target))
                    shared++;
            }
        }

        // Until every Hunter of the local player is idle and carries nothing.
        private static IEnumerable<Wait> WaitHuntersFree(Step step)
        {
            int local = Local();
            yield return new Until("all Hunters have delivered and are idle", () =>
            {
                lock (Engine()) return AllHuntersIdle(local);
            });
        }

        private static bool AllHuntersIdle(int local)
        {
            if (HunterCarry(local) != 0)
                return false;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER &&
                    unit->r_AIState != 0 && unit->r_AIState != 6 && unit->r_AIState != 7)
                {
                    return false;
                }
            }
            return true;
        }

        private static IDictionary HitCorpses()
        {
            object hits = typeof(GameSteps).GetMethod("ResolveStatic", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                .Invoke(null, new object[] { "ImprovedHunters.ImprovedHuntersPlugin::persistentRuntime.hunterHitCorpses" });
            Runner.Require(hits is IDictionary, "no hit list in the mod");
            return (IDictionary)hits;
        }

        // The Hunter on his way to a far deer learns of a corpse his arrow made. Only the event that reports the hit is left out.
        private static IEnumerable<Wait> ForeignHit(Step step)
        {
            int local = Local(), before = 0;
            lock (Engine()) before = InjectForeignHit(local);
            int start = Environment.TickCount, target = 0;
            yield return new Until("the Hunter turns to the corpse", () =>
            {
                lock (Engine()) target = HunterTargetId(local);
                return target == corpseFixture;
            });
            AcceptLog.Info("HUNT_FOREIGN_TRANSFER from=" + before + " to=" + target + " ms=" + (Environment.TickCount - start));
        }

        private static int InjectForeignHit(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            Runner.Require(hunter->r_AIState == 1, "the Hunter is not on his way");
            int before = *(ushort*)((byte*)hunter + HunterTargetIdOffset);
            int x = hunter->r_CurrentTilePositionX, y = hunter->r_CurrentTilePositionY;
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x + 6, y + 6, hunter->r_HeightElevation, eChimps.CHIMP_TYPE_DEER));
            Runner.Require(id > 0, "corpse fixture missing");
            if (APIShared.UnitAccess.TryGetById(id, out _, out _))
                GameUnitManagerAPI.Instance.KillUnit(id);
            corpseFixture = id;
            GameUnit* first = GameUnitManagerAPI.Instance.GetUnitArray()._array;
            HitCorpses()[checked((int)(hunter - first) + 1)] = id;
            return before;
        }

        private static int HunterTargetId(int local)
        {
            GameUnit* hunter = FindHunter(local);
            return hunter == null ? 0 : *(ushort*)((byte*)hunter + HunterTargetIdOffset);
        }

        // The spots the matrix uses must be open ground; the map's start positions differ.
        private static void ExpectOpenStart()
        {
            HutTile(out int x, out int y, out _);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            UnmanagedVector2<ushort> door = tiles.GetNearestUnoccupiedTile(x, y);
            int home = components[tiles.GetTileId(door.X, door.Y)];
            foreach ((int dx, int dy, int margin) in new[] { (24, 0, 0), (18, 0, 0), (-20, 0, 0) })
            {
                for (int ox = -margin; ox <= margin; ox++)
                {
                    for (int oy = -margin; oy <= margin; oy++)
                    {
                        int tile = tiles.GetTileId(x + dx + ox, y + dy + oy);
                        Runner.Require(components[tile] == home, "this start position has blocked ground at " + (x + dx + ox) + "," + (y + dy + oy));
                    }
                }
            }
        }

        // args: milliseconds. The Hunter's real arrow loses its aim in flight, so the kill it makes is not of the unit it says
        // it was aimed at. That is how the mod tells a foreign hit; the event and the arrow's shooter are the real ones.
        private static IEnumerable<Wait> WatchForeignHit(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), hit = 0, stripped = 0;
            while (Environment.TickCount < end && hit == 0)
            {
                lock (Engine())
                {
                    stripped += StripArrowAims(local);
                    foreach (object value in HitCorpses().Values)
                        hit = (int)value;
                }
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_FOREIGN_HIT corpse=" + hit + " strippedArrows=" + stripped);
            Runner.Require(hit != 0, "the mod saw no kill by the Hunter's arrow");
            corpseFixture = hit;
            yield return new Until("the shooter takes the corpse his arrow made", () =>
            {
                lock (Engine()) return HunterTargetId(local) == hit;
            });
            AcceptLog.Info("HUNT_FOREIGN_TAKEN corpse=" + hit);
        }

        private static int StripArrowAims(int local)
        {
            GameUnit* hunter = FindHunter(local);
            if (hunter == null)
                return 0;
            int hunterId = checked((int)(hunter - GameUnitManagerAPI.Instance.GetUnitArray()._array) + 1), count = 0;
            SimpleNativeArray<GameProjectile> arrows = GameProjectileManagerAPI.Instance.GetProjectilesArray();
            for (int index = 0; index < arrows.Length; index++)
            {
                GameProjectile* arrow = arrows.GetValuePointer(index);
                if (arrow->r_AliveState == AliveState.None || arrow->r_SourceUnitId != hunterId || arrow->r_TargetUnidId == 0)
                    continue;
                arrow->r_TargetUnidId = 0;
                count++;
            }
            return count;
        }

        // args: minimum. Meat from the corpse reaches a Hunter of this player.
        private static IEnumerable<Wait> ExpectCarries(Step step)
        {
            int local = Local(), minimum = step.Int(0), carried = 0;
            int nextLog = 0;
            yield return new Until("a Hunter carries the corpse's meat", () =>
            {
                lock (Engine())
                {
                    carried = HunterCarry(local);
                    if (Environment.TickCount >= nextLog)
                    {
                        nextLog = Environment.TickCount + 2000;
                        LogCorpseWait(local);
                    }
                }
                return carried >= minimum;
            });
            AcceptLog.Info("HUNT_CARRIES " + carried);
        }

        private static int HerdSector(int herd)
        {
            byte* record = (byte*)GetModuleHandle("CrusaderDE.dll") + HerdManagerRva + herd * HerdStride;
            Runner.Require(APIShared.UnitAccess.TryGetById(*(short*)(record + HerdLeaderOffset), out GameUnit* leader, out _), "no herd leader");
            return leader->r_CurrentTilePositionX / 10 * 80 + leader->r_CurrentTilePositionY / 10;
        }

        private static void ReadHerd(bool expected, out bool fled, out bool left, out bool threatened)
        {
            GameUnit* animal = FindHerdLeader(out int herd);
            Runner.Require(animal != null, "no herd");
            fled = HerdIsFleeing(herd);
            int sector = HerdSector(herd);
            left = !expected && !fled && soldierSector >= 0 && sector != soldierSector;
            threatened = *(int*)((byte*)GetModuleHandle("CrusaderDE.dll") + SectorGridRva + sector * SectorStride + SectorUnitsOffset) > 0;
        }

        // args: yes | no, milliseconds. The sector grid rebuilds in turns, so give it time.
        // A herd that wanders on may meet units that really see it, so "no" only covers the soldier's sector, and it
        // counts only while Vanilla's own sector count is set, because that alone would make the herd run.
        private static IEnumerable<Wait> ExpectHerdFlight(Step step)
        {
            bool expected = step.Arg(0) == "yes", fled = false, left = false, threatened = false;
            int start = Environment.TickCount, end = start + step.Int(1), threatenedSince = 0, calmUnderThreat = 0;
            while (Environment.TickCount < end && !fled && !left)
            {
                lock (Engine()) ReadHerd(expected, out fled, out left, out threatened);
                if (threatened && threatenedSince == 0)
                    threatenedSince = Environment.TickCount;
                if (threatened && !fled)
                    calmUnderThreat = Environment.TickCount - threatenedSince;
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_HERD_FLED " + fled + " expected=" + expected + " left=" + left + " calmUnderThreatMs=" + calmUnderThreat + " ms=" + (Environment.TickCount - start));
            Runner.Require(fled == expected, expected ? "the herd did not flee" : "the herd fled from a unit that cannot see or reach it");
            if (soldierSector >= 0 && !expected)
                Runner.Require(calmUnderThreat >= MinimumCalmMs, "the herd left before the sector count had held long enough to show anything");
        }

        private static (int X, int Y) LordTile(int local)
        {
            lock (Engine())
            {
                Runner.Require(APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(local), out GameUnit* lord, out _), "no lord");
                return (lord->r_CurrentTilePositionX, lord->r_CurrentTilePositionY);
            }
        }

        private static GameUnit* FindHunter(int local)
        {
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState == AliveState.IsAlive && unit->r_ControllableForPlayerId == local && unit->r_UnitChimp == eChimps.CHIMP_TYPE_HUNTER)
                    return unit;
            }
            return null;
        }

        private static int HunterDeerTarget(int local, out int state)
        {
            state = -1;
            GameUnit* hunter = FindHunter(local);
            if (hunter == null)
                return 0;
            state = hunter->r_AIState;
            int target = *(ushort*)((byte*)hunter + HunterTargetIdOffset);
            // The field keeps the last target, whose ID may belong to a new unit by now.
            return deer.Contains(target) && APIShared.UnitAccess.TryGetById(target, out GameUnit* prey, out _) &&
                prey->r_GlobalId == *(uint*)((byte*)hunter + HunterTargetGlobalOffset) ? target : 0;
        }

        private static void SpawnDeerNearHunter(int distance)
        {
            int local = Local();
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter");
            // On his way to the far deer, wherever that lies, so it is the clearly better prey.
            int signX = 1, signY = -1;
            if (APIShared.UnitAccess.TryGetById(*(ushort*)((byte*)hunter + HunterTargetIdOffset), out GameUnit* far, out _))
            {
                signX = Math.Sign(far->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX);
                signY = Math.Sign(far->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY);
            }
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(
                0, 0, hunter->r_CurrentTilePositionX + signX * distance, hunter->r_CurrentTilePositionY + signY * distance, hunter->r_HeightElevation, eChimps.CHIMP_TYPE_DEER));
            Runner.Require(id > 0, "near deer missing");
            deer.Add(id);
            nearDeer = id;
            AcceptLog.Info("HUNT_NEAR_DEER id=" + id + " hunterTarget=" + *(ushort*)((byte*)hunter + HunterTargetIdOffset));
        }

        private static bool HunterFarFromDeer(int local, int minimum)
        {
            lock (Engine())
            {
                int target = HunterDeerTarget(local, out int state);
                if (target == 0 || state != 1 || !APIShared.UnitAccess.TryGetById(target, out GameUnit* prey, out _))
                    return false;
                GameUnit* hunter = FindHunter(local);
                return Math.Max(Math.Abs(hunter->r_CurrentTilePositionX - prey->r_CurrentTilePositionX),
                    Math.Abs(hunter->r_CurrentTilePositionY - prey->r_CurrentTilePositionY)) >= minimum;
            }
        }

        // args: minimum distance from the Hunter to the held deer.
        private static IEnumerable<Wait> ExpectFarFromHunter(Step step)
        {
            int local = Local(), minimum = step.Int(0);
            int nextLog = 0;
            yield return new Until("Hunter walks toward a far deer", () =>
            {
                if (Environment.TickCount >= nextLog)
                {
                    nextLog = Environment.TickCount + 10000;
                    lock (Engine()) LogCorpseWait(local);
                }
                return HunterFarFromDeer(local, minimum);
            });
        }

        // args: milliseconds. The fixture deer stay outside the search range.
        private static IEnumerable<Wait> ExpectNoDeerTarget(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), seen = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine()) seen = Math.Max(seen, HunterDeerTarget(local, out _));
                Runner.Require(seen == 0, "the Hunter targeted deer " + seen + " outside its range");
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_NO_DEER_TARGET");
        }

        private static IEnumerable<Wait> ExpectDeerTarget(Step step)
        {
            int local = Local(), target = 0;
            yield return new Until("Hunter targets a fixture deer", () =>
            {
                lock (Engine()) target = HunterDeerTarget(local, out _);
                return target != 0;
            });
            AcceptLog.Info("HUNT_DEER_TARGET " + target);
        }

        private static bool NearDeerDead()
        {
            return !APIShared.UnitAccess.TryGetById(nearDeer, out GameUnit* prey, out _) || prey->r_CurrentHealth == 0;
        }

        // args: milliseconds. The Hunter changes to the near deer once and does not change back.
        private static IEnumerable<Wait> ExpectOneSwitch(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), last = 0, changes = 0;
            bool reached = false, dead = false;
            while (Environment.TickCount < end && !dead)
            {
                lock (Engine())
                {
                    int target = HunterDeerTarget(local, out _);
                    if (target != 0 && target != last)
                    {
                        if (last != 0)
                            changes++;
                        last = target;
                        AcceptLog.Info("HUNT_TARGET_NOW " + target);
                    }
                    reached |= target == nearDeer;
                    dead = reached && NearDeerDead();
                }
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_SWITCH reached=" + reached + " changes=" + changes + " nearDeerDead=" + dead);
            Runner.Require(reached, "the Hunter stayed with the far deer");
            Runner.Require(changes == 1, "the Hunter changed target " + changes + " times");
        }

        // Records which deer the Hunter holds. Every switch to another deer while the previous one still
        // lives is a lost hunt, which is what the post-shot continuation has to prevent.
        private static IEnumerable<Wait> DeerCycle(Step step)
        {
            int local = Local(), kills = step.Int(0), maxSwitches = step.Int(1, 0), expectedCarry = step.Int(2, 0), maxCarry = 0;
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
                    maxCarry = Math.Max(maxCarry, HunterCarry(local));
                }
                return dead.Count >= kills && delivered > 0;
            });
            AcceptLog.Info("HUNT_DEER_RESULT killed=" + dead.Count + " meat=" + delivered + " switchesFromLiveDeer=" + switches + " maxCarried=" + maxCarry);
            // The bonus fraction of an earlier kill can add one more.
            Runner.Require(expectedCarry == 0 || maxCarry == expectedCarry || maxCarry == expectedCarry + 1, "the Hunter carried " + maxCarry + ", expected " + expectedCarry);
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
