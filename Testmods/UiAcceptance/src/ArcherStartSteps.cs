using System;
using System.Collections.Generic;
using System.Linq;
using APIShared;
using APIShared.Sight;
using APIShared.Simulation;
using R3;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Hash-bound observation of the ordinary archer update in a single-player scenario.
    // SimulationTickEvents samples just before the unit pass. Projectile events confirm actual shots.
    internal static unsafe class ArcherStartSteps
    {
        private const int RangedTargetGlobalOffset = 0x9C;
        private const int RabbitOrderFlagOffset = 0x564;
        // ArcherUpdate 0x13F540 and ArabBowUpdate advance from wind-up to release.
        private const int WindUpState = 5;
        private const int ReleaseState = 6;
        private const int ShotFrame = 6;
        private const int MaxTraceLines = 1200;

        private static readonly Dictionary<uint, ArcherSample> previous = new Dictionary<uint, ArcherSample>();
        private static readonly Dictionary<uint, int> drawStarts = new Dictionary<uint, int>();
        private static readonly HashSet<uint> moved = new HashSet<uint>();
        private static readonly HashSet<(uint Archer, int Target)> rabbitArrows = new HashSet<(uint, int)>();
        private static readonly HashSet<int> rabbitAims = new HashSet<int>();
        private static IDisposable projectileSubscription;
        private static IDisposable damageSubscription;
        private static bool registered, active, sampledStart;
        private static int tick, rows, local, humanSamples, aiSamples, focusTribe;
        private static uint focusArcher;
        private static int orderedRabbit, nearbyRabbit;

        internal static void Register()
        {
            Runner.Register("ArcherStart.Arm", s => GameSteps.Do(Arm));
            Runner.Register("ArcherStart.Move", s => GameSteps.Do(() => IssueMove(s.Int(0, 4))));
            Runner.Register("ArcherStart.End", s => GameSteps.Do(End));
            Runner.Register("ArcherStart.RabbitFixture", s => GameSteps.Do(PlaceRabbitControl));
            Runner.Register("ArcherStart.RabbitCheck", s => GameSteps.Do(() => CheckRabbitControl(s.Arg(0))));
            Runner.Register("ArcherStart.RabbitAttack", s => GameSteps.Do(AttackRabbit));
        }

        private static void Arm()
        {
            lock (HuntSteps.Engine())
            {
                ProbeSteps.RequireNativeReferenceHash();
                if (!registered)
                {
                    Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "archer-start",
                        Observe, out string reason, 900), reason);
                    registered = true;
                }
                previous.Clear();
                drawStarts.Clear();
                moved.Clear();
                rabbitArrows.Clear();
                rabbitAims.Clear();
                local = tick = rows = humanSamples = aiSamples = focusTribe = orderedRabbit = nearbyRabbit = 0;
                focusArcher = 0;
                sampledStart = false;
                projectileSubscription?.Dispose();
                projectileSubscription = ProjectileR3EventHooks.OnProjectileSpawn.Observable.Subscribe(ObserveProjectile);
                damageSubscription?.Dispose();
                damageSubscription = UnitR3EventHooks.OnUnitTakeProjectileDamage.Observable.Subscribe(ObserveDamage);
                active = true;
            }
        }

        private static void Observe(int mapTick)
        {
            if (!active) return;
            tick = mapTick;
            local = PlayerPerspectiveAPI.GetControlledPlayerId();
            if (local < 1) return;
            if (!sampledStart)
            {
                sampledStart = true;
                AcceptLog.Info($"ARCHER_START_BEGIN tick={tick} local={local}");
            }

            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (IsArcher(unit))
                    ObserveArcher(index + 1, unit);
            }
        }

        private static bool IsArcher(GameUnit* unit) =>
            unit != null && UnitAccess.IsReallyAlive(unit) &&
            (unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARCHER || unit->r_UnitChimp == eChimps.CHIMP_TYPE_ARAB_BOW);

        private static void ObserveArcher(int id, GameUnit* unit)
        {
            uint global = unit->r_GlobalId;
            int aim = unchecked((short)unit->r_RangedAttackTargetUnitId);
            uint aimGlobal = *(uint*)((byte*)unit + RangedTargetGlobalOffset);
            int flag = RabbitFlag(unit->r_TribeId);
            var now = new ArcherSample(unit->r_AIState, unit->r_CurrentSpriteAnimationFrame,
                aim, aimGlobal, unit->r_AI_LastIssuedTribeCommand, flag, unit->r_PathPlanStateBitFlags,
                unit->r_CurrentPathPlanIndex);
            bool known = previous.TryGetValue(global, out ArcherSample before);
            previous[global] = now;
            if (!known)
            {
                if (unit->r_ControllableForPlayerId == local) humanSamples++;
                else if (unit->r_ControllableForPlayerId > 0) aiSamples++;
            }
            bool startedDraw = now.State == WindUpState &&
                (!known || before.State != WindUpState || now.Frame < before.Frame);
            if (startedDraw)
                drawStarts[global] = drawStarts.TryGetValue(global, out int count) ? count + 1 : 1;
            bool shotFrame = now.State == ReleaseState && now.Frame == ShotFrame &&
                (!known || before.State != ReleaseState || before.Frame != ShotFrame);
            if (known && !startedDraw && !shotFrame &&
                before.State == now.State && before.Aim == aim && before.AimGlobal == aimGlobal &&
                before.Order == now.Order && before.Flag == flag &&
                before.Path == now.Path && before.PathIndex == now.PathIndex)
            {
                return;
            }
            if (rows++ >= MaxTraceLines) return;

            GameUnit* target = null;
            string sight = "-";
            string targetInfo = "-";
            if (aim > 0 && UnitAccess.TryGetById(aim, out target, out _) && target != null)
            {
                if (target->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT && target->r_GlobalId == aimGlobal)
                    rabbitAims.Add(aim);
                int dx = unit->r_CurrentWorldPositionX / 8 - target->r_CurrentWorldPositionX / 8;
                int dy = unit->r_CurrentWorldPositionY / 8 - target->r_CurrentWorldPositionY / 8;
                eChimps type = unit->r_UnitChimp;
                int range = GameUnitManagerAPI.Instance.GetUnitAttackRange(type);
                int tableRange = GameProjectileManagerAPI.Instance.GetAttackRangeTiles(
                    GameUnitManagerAPI.GetDefaultAttackRangeProjectileType(type));
                if (LineOfSight.TryEvaluateUnits(in *unit, in *target, LineOfSightRoutine.Bidirectional,
                        out int visibility, out _))
                    sight = visibility.ToString();
                targetInfo = $"{target->r_UnitChimp}/{target->r_GlobalId},alive={target->r_AliveState}," +
                    $"hp={target->r_CurrentHealth},state={target->r_AIState}," +
                    $"xy={target->r_CurrentWorldPositionX}:{target->r_CurrentWorldPositionY}," +
                    $"distanceSquared={dx * dx + dy * dy},range={range},tableRange={tableRange}";
            }
            AcceptLog.Info($"ARCHER_START_SAMPLE tick={tick} archer={id}/{global} owner={unit->r_ControllableForPlayerId} " +
                $"local={local} type={unit->r_UnitChimp} tribe={unit->r_TribeId} rabbitFlag={flag} order={now.Order} " +
                $"explicit={unit->r_AI_ContextTargetUnitId}/{unit->r_AI_ContextTargetUnitGlobalId} aim={aim}/{aimGlobal} " +
                $"state={now.State} frame={now.Frame} draw={startedDraw} releaseFrame={shotFrame} moved={moved.Contains(global)} " +
                $"xy={unit->r_CurrentWorldPositionX}:{unit->r_CurrentWorldPositionY} " +
                $"path={now.Path}/{now.PathIndex}/{unit->r_PathPlanLength} " +
                $"destination={unit->r_TargetTilePositionX2}:{unit->r_TargetTilePositionY2} " +
                $"connection={unit->r_SelectedConnectionRecordId} target={targetInfo} sight={sight}");
        }

        private static int RabbitFlag(int tribeId) =>
            GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null
                ? *(ushort*)((byte*)tribe + RabbitOrderFlagOffset)
                : -1;

        private static void ObserveProjectile(ProjectileSpawnEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Post ||
                !UnitAccess.TryGetById(args.SourceUnitId, out GameUnit* unit, out _) || !IsArcher(unit))
                return;

            int targetId = args.AttackedUnitId;
            string kind = "-";
            if (targetId > 0 && UnitAccess.TryGetById(targetId, out GameUnit* target, out _) && target != null)
            {
                kind = target->r_UnitChimp.ToString();
                if (args.ReturnValue > 0 && target->r_UnitChimp == eChimps.CHIMP_TYPE_RABBIT)
                    rabbitArrows.Add((unit->r_GlobalId, targetId));
            }
            AcceptLog.Info($"ARCHER_START_ARROW tick={tick} archer={args.SourceUnitId}/{unit->r_GlobalId} " +
                $"owner={unit->r_ControllableForPlayerId} tribe={unit->r_TribeId} rabbitFlag={RabbitFlag(unit->r_TribeId)} " +
                $"target={targetId}:{kind} projectile={args.ReturnValue} type={args.ProjectileType}");
        }

        private static void ObserveDamage(UnitTakeDamageByProjectileEventArgs args)
        {
            if (!active || args.Phase != EventHookPhase.Post || args.ReturnValue == 0 ||
                !GameProjectileManagerAPI.Instance.TryGetProjectileById(args.ProjectileId, out GameProjectile* projectile) ||
                projectile == null ||
                !UnitAccess.TryGetById(projectile->r_SourceUnitId, out GameUnit* archer, out _) || !IsArcher(archer) ||
                !UnitAccess.TryGetById(args.AttackedUnitId, out GameUnit* target, out _) || target == null)
                return;
            AcceptLog.Info($"ARCHER_START_HIT tick={tick} archer={projectile->r_SourceUnitId}/{archer->r_GlobalId} " +
                $"target={args.AttackedUnitId}/{target->r_GlobalId}:{target->r_UnitChimp} " +
                $"hp={target->r_CurrentHealth} projectile={args.ProjectileId}");
        }

        private static GameUnit* SelectLocalArcher()
        {
            int bestDraws = -1;
            GameUnit* chosen = null;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (!IsArcher(unit) || unit->r_ControllableForPlayerId != HuntSteps.Local() || unit->r_TribeId == 0)
                    continue;
                int draws = drawStarts.TryGetValue(unit->r_GlobalId, out int observed) ? observed : 0;
                if (focusArcher != 0 ? unit->r_GlobalId != focusArcher : draws <= bestDraws)
                    continue;
                chosen = unit;
                bestDraws = draws;
            }
            Runner.Require(chosen != null, "no local starting archer with an orderable group");
            return chosen;
        }

        private static void IssueMove(int distance)
        {
            lock (HuntSteps.Engine())
            {
                GameUnit* unit = SelectLocalArcher();
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                int tribe = unit->r_TribeId;
                int before = RabbitFlag(tribe);
                foreach ((int dx, int dy) in new[] { (distance, 0), (0, distance), (-distance, 0), (0, -distance) })
                {
                    if (!tiles.IsTileInsideMapBounds(x + dx, y + dy) ||
                        !tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(x + dx, y + dy)))
                        continue;
                    if (!GameTribeManagerAPI.Instance.MoveTo(tribe, x + dx, y + dy))
                        continue;

                    SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
                    for (int index = 0; index < units.Length; index++)
                    {
                        GameUnit* member = units.GetValuePointer(index);
                        if (IsArcher(member) && member->r_TribeId == tribe)
                            moved.Add(member->r_GlobalId);
                    }
                    AcceptLog.Info($"ARCHER_START_MOVE tick={tick} archer={unit->r_GlobalId} tribe={tribe} " +
                        $"from={x}:{y} to={x + dx}:{y + dy} flagBefore={before} flagAfter={RabbitFlag(tribe)}");
                    return;
                }
                Runner.Require(false, "no accepted short archer move");
            }
        }

        private static void PlaceRabbitControl()
        {
            lock (HuntSteps.Engine())
            {
                GameUnit* unit = SelectLocalArcher();
                focusArcher = unit->r_GlobalId;
                focusTribe = unit->r_TribeId;
                var placed = new List<int>();
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                foreach ((int dx, int dy) in new[] { (6, 0), (7, 1), (0, 6), (1, 7), (-6, 0), (0, -6) })
                {
                    int x = unit->r_CurrentTilePositionX + dx, y = unit->r_CurrentTilePositionY + dy;
                    if (!tiles.IsTileInsideMapBounds(x, y))
                        continue;
                    int tile = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(tile))
                        continue;
                    int id = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(0, 0, x, y,
                        tiles.GetTileHeight(tile), eChimps.CHIMP_TYPE_RABBIT));
                    if (id > 0)
                        placed.Add(id);
                    if (placed.Count == 2)
                        break;
                }
                Runner.Require(placed.Count == 2, "could not place two rabbits near the local archer");
                orderedRabbit = placed[0];
                nearbyRabbit = placed[1];
                AcceptLog.Info($"ARCHER_CONTROL tick={tick} archer={focusArcher} tribe={focusTribe} " +
                    $"orderedRabbit={orderedRabbit} nearbyRabbit={nearbyRabbit}");
            }
        }

        private static void AttackRabbit()
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(GameTribeManagerAPI.Instance.AttackUnit(focusTribe, orderedRabbit),
                    "explicit rabbit order rejected");
                Runner.Require(RabbitFlag(focusTribe) > 0, "explicit ranged attack did not set the rabbit flag");
            }
        }

        private static void CheckRabbitControl(string phase)
        {
            lock (HuntSteps.Engine())
            {
                switch (phase)
                {
                    case "idle":
                        Runner.Require(RabbitFlag(focusTribe) == 0 &&
                            !rabbitAims.Contains(orderedRabbit) && !rabbitAims.Contains(nearbyRabbit) &&
                            !rabbitArrows.Any(s => s.Target == orderedRabbit || s.Target == nearbyRabbit),
                            "an archer aimed at or shot a rabbit before its order");
                        break;
                    case "shot":
                        Runner.Require(rabbitArrows.Any(s => s.Target == orderedRabbit) &&
                            rabbitArrows.Any(s => s.Target == nearbyRabbit),
                            "missing an actual arrow on the ordered or follow-up rabbit");
                        break;
                    case "moved":
                        Runner.Require(RabbitFlag(focusTribe) == 0, "movement did not clear rabbit permission");
                        break;
                    default:
                        Runner.Require(false, "unknown rabbit-control phase: " + phase);
                        break;
                }
            }
        }

        private static void End()
        {
            lock (HuntSteps.Engine())
            {
                active = false;
                projectileSubscription?.Dispose();
                projectileSubscription = null;
                damageSubscription?.Dispose();
                damageSubscription = null;
                AcceptLog.Info($"ARCHER_START_SUMMARY tick={tick} playerArchers={humanSamples} aiArchers={aiSamples} samples={rows}");
                Runner.Require(humanSamples > 0 && aiSamples > 0, "did not observe both player and AI archers");
            }
        }

        private readonly struct ArcherSample
        {
            internal readonly int State, Aim, Order, Flag, Path, PathIndex;
            internal readonly uint Frame, AimGlobal;

            internal ArcherSample(int state, uint frame, int aim, uint aimGlobal, int order, int flag,
                int path, int pathIndex)
            {
                State = state;
                Frame = frame;
                Aim = aim;
                AimGlobal = aimGlobal;
                Order = order;
                Flag = flag;
                Path = path;
                PathIndex = pathIndex;
            }
        }
    }
}
