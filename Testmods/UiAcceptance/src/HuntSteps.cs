using System;
using System.Collections;
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
        private const int HerdManagerRva = 0x7CC6720, HerdStride = 0x688, HerdFleeingOffset = 0x5F4, HerdLeaderOffset = 0x5A;
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
            Runner.Register("Hunt.ExpectCorpseTaken", ExpectCorpseTaken);
            Runner.Register("Hunt.ExpectCorpseIgnored", ExpectCorpseIgnored);
            Runner.Register("Hunt.FadingCorpseIgnored", FadingCorpseIgnored);
            Runner.Register("Hunt.ExpectOneCollector", ExpectOneCollector);
            Runner.Register("Hunt.ExpectCarries", ExpectCarries);
            Runner.Register("Hunt.ForeignHit", ForeignHit);
            Runner.Register("Hunt.Stress", s => GameSteps.Do(() => { lock (Engine()) Stress(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.Supply", s => GameSteps.Do(() => { lock (Engine()) Supply(); }));
            Runner.Register("Hunt.Prey", s => GameSteps.Do(() => { lock (Engine()) SpawnPrey(s.Arg(0), s.Arg(1) == "ring" ? int.MinValue : s.Int(1), s.Arg(1) == "ring" ? 0 : s.Int(2)); }));
            Runner.Register("Hunt.ClearPrey", s => GameSteps.Do(() => { lock (Engine()) deer.Clear(); }));
            Runner.Register("Hunt.SightWall", s => GameSteps.Do(() => { lock (Engine()) SightWall(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.SightBuilding", s => GameSteps.Do(() => { lock (Engine()) SightBuilding(s.Int(0), s.Int(1)); }));
            Runner.Register("Hunt.Ring", Ring);
            Runner.Register("Hunt.CloseRing", CloseRing);
            Runner.Register("Hunt.OpenRing", OpenRing);
            Runner.Register("Hunt.ExpectTargetDropped", ExpectTargetDropped);
            Runner.Register("Hunt.WaitHunterFree", WaitHunterFree);
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
            while (!closed)
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
            {
                // Beside the herd in the open, and outside the earlier ring.
                x = animal->r_CurrentTilePositionX + (animal->r_CurrentTilePositionX % 10 < 5 ? 1 : -1);
                y = animal->r_CurrentTilePositionY + (animal->r_CurrentTilePositionY % 10 < 5 ? 1 : -1);
            }
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
            kind == "chicken" ? eChimps.CHIMP_TYPE_CHICKEN : eChimps.CHIMP_TYPE_DEER;

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
            int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, atX, atY, height, type));
            Runner.Require(id > 0, "prey missing");
            deer.Add(id);
            AcceptLog.Info("HUNT_PREY id=" + id + " type=" + type + " at=" + atX + "," + atY);
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

        // args: milliseconds. Two Hunters at work never hunt the same animal.
        private static IEnumerable<Wait> ExpectSeparateTargets(Step step)
        {
            int local = Local(), end = Environment.TickCount + step.Int(0), both = 0, shared = 0;
            while (Environment.TickCount < end)
            {
                lock (Engine()) CountTargets(local, ref both, ref shared);
                yield return NextFrame.Instance;
            }
            AcceptLog.Info("HUNT_TARGETS both=" + both + " shared=" + shared);
            Runner.Require(shared == 0, "two Hunters hunted one animal");
            Runner.Require(both > 0, "the two Hunters never hunted at the same time");
        }

        private static void CountTargets(int local, ref int both, ref int shared)
        {
            int first = 0, count = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_ControllableForPlayerId != local || unit->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER || unit->r_AIState != 1)
                    continue;
                int target = *(ushort*)((byte*)unit + HunterTargetIdOffset);
                if (!deer.Contains(target))
                    continue;
                count++;
                if (first == target)
                    shared++;
                first = target;
            }
            if (count >= 2)
                both++;
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
