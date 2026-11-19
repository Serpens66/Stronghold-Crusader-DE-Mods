using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Sight;
using APIShared.Simulation;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static unsafe partial class HuntSteps
    {
        private const int CorridorLength = 12;
        private static int corridorHunter, corridorPrey, corridorStartTick;
        private static int corridorArrowsAtPrey, corridorDelivered;
        private static int corridorPreviousState, corridorVisibleTerminalAborts;
        private static bool corridorStartedBlocked, corridorTraceFinished;
        private static uint corridorHunterGlobal, corridorPreyGlobal;
        private static bool corridorObserverRegistered;
        private static string corridorLastSample;
        private static IDisposable corridorProjectiles;
        private static IDisposable corridorDeliverySubscription;

        private static void RegisterCorridorSteps()
        {
            Runner.Register("Hunt.Corridor", Corridor);
            Runner.Register("Hunt.ExpectCorridorShot", s => GameSteps.Do(() =>
            {
                lock (Engine())
                {
                    Runner.Require(corridorArrowsAtPrey > 0, "Hunter carried meat without a recorded arrow at the fixture");
                    Runner.Require(s.Arg(0) != "blocked" || corridorStartedBlocked, "counterexample did not start blocked");
                    Runner.Require(s.Arg(0) != "no-terminal-abort" || corridorVisibleTerminalAborts == 0,
                        "Hunter abandoned visible prey while the final movement step was still active");
                }
            }));
            Runner.Register("Hunt.ExpectCorridorDelivery", ExpectCorridorDelivery);
            Runner.Register("Hunt.StopCorridorTrace", s => GameSteps.Do(() =>
            {
                lock (Engine())
                {
                    corridorHunter = 0;
                    corridorProjectiles?.Dispose();
                    corridorProjectiles = null;
                    corridorDeliverySubscription?.Dispose();
                    corridorDeliverySubscription = null;
                }
            }));
        }

        private static IEnumerable<Wait> Corridor(Step step)
        {
            const int length = CorridorLength;
            string layout = step.Arg(0);
            int local = Local();
            int x, y, signX, signY;
            bool walled = layout != "open";
            lock (Engine())
            {
                (x, y, signX, signY) = FindCorridorSite(local);
                if (walled)
                {
                    var buildings = GameBuildingManagerAPI.Instance;
                    foreach (int side in new[] { -1, 1 })
                    {
                        for (int along = 1; along <= length; along++)
                        {
                            int tx = x + along * signX - side * signY;
                            int ty = y + along * signY + side * signX;
                            buildings.CreateWall(local, tx, ty, tx, ty, eMappers.MAPPER_WALL);
                        }
                    }
                    if (layout == "cross")
                    {
                        int tx = x + length / 2 * signX, ty = y + length / 2 * signY;
                        buildings.CreateWall(local, tx, ty, tx, ty, eMappers.MAPPER_WALL);
                    }
                    AcceptLog.Info("HUNT_CORRIDOR_WALLS at=" + x + "," + y + " direction=" + signX + "," + signY +
                        " left=" + CorridorWallMask(x, y, signX, signY, -1) +
                        " right=" + CorridorWallMask(x, y, signX, signY, 1));
                }
            }

            if (walled)
                yield return new Until("corridor walls are present", () =>
                {
                    lock (Engine())
                    {
                        foreach (int side in new[] { -1, 1 })
                        for (int along = 1; along <= length; along++)
                            if (!CorridorHasWall(x + along * signX - side * signY, y + along * signY + side * signX))
                                return false;
                        return layout != "cross" || CorridorHasWall(x + length / 2 * signX, y + length / 2 * signY);
                    }
                });

            StartCorridorTrace(layout, local, x, y, signX, signY);
        }

        private static (int X, int Y, int Dx, int Dy) FindCorridorSite(int local)
        {
            GameUnit* hunter = FindHunter(local);
            Runner.Require(hunter != null, "no Hunter for corridor");
            var tileApi = GameTileManagerAPI.Instance;
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int hunterX = hunter->r_CurrentTilePositionX;
            int hunterY = hunter->r_CurrentTilePositionY;
            ushort land = components[tileApi.GetTileId(hunterX, hunterY)];
            Runner.Require(land != 0, "Hunter has no land component");
            for (int approach = 0; approach <= 16; approach++)
            {
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int x = hunterX + approach * dx;
                    int y = hunterY + approach * dy;
                    bool clear = true;
                    if (!tileApi.IsTileInsideMapBounds(x, y))
                        continue;
                    int height = tileApi.GetTileHeight(tileApi.GetTileId(x, y));
                    for (int along = 1; along <= CorridorLength + 1 && clear; along++)
                    {
                        for (int across = -1; across <= 1; across++)
                        {
                            int tx = x + along * dx - across * dy;
                            int ty = y + along * dy + across * dx;
                            if (!tileApi.IsTileInsideMapBounds(tx, ty))
                            {
                                clear = false;
                                break;
                            }
                            int tile = tileApi.GetTileId(tx, ty);
                            if (components[tile] != land || !tileApi.IsTileWalkableAndUnoccupied(tile) ||
                                tileApi.GetTileHeight(tile) != height)
                            {
                                clear = false;
                                break;
                            }
                        }
                    }
                    if (clear)
                        return (x, y, dx, dy);
                }
            }
            throw new InvalidOperationException("no clear three-tile strip near the Hunter");
        }

        private static void StartCorridorTrace(string layout, int local, int x, int y, int signX, int signY)
        {
            lock (Engine())
            {
                if (!corridorObserverRegistered)
                {
                    Runner.Require(SimulationTickEvents.TryRegister("UiAcceptance_Serp", "hunt-corridor",
                        TraceCorridorTick, out string reason, 900), reason);
                    corridorObserverRegistered = true;
                }
                GameUnit* hunter = FindHunter(local);
                Runner.Require(hunter != null, "Hunter vanished before the corridor test");
                int preyX = x + CorridorLength * signX;
                int preyY = y + CorridorLength * signY;
                if (layout == "corner")
                {
                    preyX += signX - signY;
                    preyY += signY + signX;
                }
                SpawnPreyAt("deer", preyX, preyY);
                corridorPrey = deer[deer.Count - 1];
                Runner.Require(UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) && prey != null, "no prey");
                corridorPreyGlobal = prey->r_GlobalId;
                corridorHunter = checked((int)(hunter - GameUnitManagerAPI.Instance.GetUnitArray()._array) + 1);
                corridorHunterGlobal = hunter->r_GlobalId;
                corridorStartTick = 0;
                corridorLastSample = null;
                corridorArrowsAtPrey = 0;
                corridorDelivered = 0;
                corridorPreviousState = -1;
                corridorVisibleTerminalAborts = 0;
                corridorTraceFinished = false;
                corridorStartedBlocked = LineOfSight.TryEvaluateUnits(in *hunter, in *prey,
                    LineOfSightRoutine.Bidirectional, out int initialSight, out _) && initialSight == 0;
                corridorProjectiles ??= ProjectileR3EventHooks.OnProjectileSpawn.Observable.Subscribe(TraceCorridorProjectile);
                corridorDeliverySubscription ??= UnitR3EventHooks.OnHunterDropOffMeat.Observable.Subscribe(ObserveCorridorDelivery);
                AcceptLog.Info("HUNT_CORRIDOR layout=" + layout + " hunter=" + corridorHunter + " prey=" + corridorPrey +
                    " start=" + x + "," + y + " end=" + preyX + "," + preyY + " blocked=" + corridorStartedBlocked);
            }
        }

        private static void TraceCorridorProjectile(ProjectileSpawnEventArgs args)
        {
            if (corridorHunter == 0 || args.Phase != EventHookPhase.Post ||
                args.SourceUnitId != corridorHunter || args.ReturnValue <= 0 ||
                args.ProjectileType != ProjectileType.ArcherArrow)
                return;
            if (args.AttackedUnitId == corridorPrey &&
                UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) && prey != null &&
                prey->r_GlobalId == corridorPreyGlobal)
                corridorArrowsAtPrey++;
            SimulationTickEvents.TryGetCurrentTick(out int tick);
            AcceptLog.Info("HUNT_CORRIDOR_ARROW tick=" + tick + " hunter=" + corridorHunter +
                " target=" + args.AttackedUnitId + " projectile=" + args.ReturnValue);
        }

        private static void ObserveCorridorDelivery(UnitHunterDropOffMeatEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre && args.UnitId == corridorHunter &&
                UnitAccess.TryGetById(args.UnitId, out GameUnit* hunter, out _) && hunter != null &&
                hunter->r_GlobalId == corridorHunterGlobal)
                corridorDelivered++;
        }

        private static IEnumerable<Wait> ExpectCorridorDelivery(Step step)
        {
            yield return new Until("the corridor Hunter delivers meat", () =>
            {
                lock (Engine()) return corridorDelivered > 0;
            });
            AcceptLog.Info("HUNT_CORRIDOR_DELIVERED hunter=" + corridorHunter + " meat=" + corridorDelivered);
        }

        private static bool CorridorHasWall(int x, int y)
        {
            var tileApi = GameTileManagerAPI.Instance;
            return (tileApi.GetTilePropertyFlag(tileApi.GetTileId(x, y)) & TilePropertyFlag.IsWall) != 0;
        }

        private static string CorridorWallMask(int x, int y, int dx, int dy, int side)
        {
            var mask = new char[CorridorLength];
            for (int i = 0; i < mask.Length; i++)
                mask[i] = CorridorHasWall(x + (i + 1) * dx - side * dy, y + (i + 1) * dy + side * dx) ? '#' : '.';
            return new string(mask);
        }

        private static void TraceCorridorTick(int tick)
        {
            if (corridorHunter == 0 || corridorTraceFinished)
                return;
            if (corridorStartTick == 0)
                corridorStartTick = tick;
            if (tick - corridorStartTick > 6000 ||
                !UnitAccess.TryGetById(corridorHunter, out GameUnit* hunter, out _) ||
                !UnitAccess.TryGetById(corridorPrey, out GameUnit* prey, out _) ||
                hunter == null || prey == null ||
                hunter->r_GlobalId != corridorHunterGlobal || prey->r_GlobalId != corridorPreyGlobal)
            {
                corridorTraceFinished = true;
                return;
            }
            LineOfSight.TryEvaluateUnits(in *hunter, in *prey, LineOfSightRoutine.Directional, out int forward, out _);
            LineOfSight.TryEvaluateUnits(in *prey, LineOfSight.TargetHeight, in *hunter, LineOfSight.ViewerEyeHeight,
                LineOfSightRoutine.Directional, out int reverse, out _);
            if (corridorPreviousState == 1 && hunter->r_AIState == 6 &&
                hunter->r_PathPlanStateBitFlags == 2 &&
                hunter->r_CurrentPathPlanIndex == hunter->r_PathPlanLength && hunter->r_MovementSubstep < 8 &&
                forward > 0 && reverse > 0)
            {
                corridorVisibleTerminalAborts++;
                AcceptLog.Info("HUNT_CORRIDOR_VISIBLE_TERMINAL_ABORT tick=" + tick + " hunter=" + corridorHunter);
            }
            corridorPreviousState = hunter->r_AIState;
            bool shotFrame = hunter->r_AIState == 10 && hunter->r_CurrentSpriteAnimationFrame == 22;
            string sample = "state=" + hunter->r_AIState + " target=" + *(ushort*)((byte*)hunter + HunterTargetIdOffset) +
                " hunter=" + hunter->r_CurrentTilePositionX + "," + hunter->r_CurrentTilePositionY +
                " prey=" + prey->r_CurrentTilePositionX + "," + prey->r_CurrentTilePositionY +
                " seen=" + (forward > 0) + "/" + (reverse > 0) + " hp=" + prey->r_CurrentHealth +
                " carry=" + hunter->r_CarryGoodAmount + " shotFrame=" + shotFrame;
            if (sample == corridorLastSample)
                return;
            corridorLastSample = sample;
            AcceptLog.Info("HUNT_CORRIDOR_TICK tick=" + tick + " " + sample +
                " sight=" + forward + "/" + reverse + " path=" + hunter->r_PathPlanStateBitFlags + "/" +
                hunter->r_MovementSubstep + "/" +
                hunter->r_CurrentPathPlanIndex + "/" + hunter->r_PathPlanLength +
                " frame=" + hunter->r_CurrentSpriteAnimationFrame);
        }
    }
}
