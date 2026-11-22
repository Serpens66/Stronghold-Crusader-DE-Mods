#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using APIShared.Automation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace ProjectileTrailsTest
{
    public sealed partial class Plugin
    {
        private static Dictionary<int, Fly> referenceFlies;
        private static readonly HashSet<int> referenceIds = new HashSet<int>();
        private static int pinnedGreenSlot = -1;

        private static IEnumerable<AutomationWait> StartGreen(AutomationRun run, bool extreme)
        {
            var setupField = typeof(FRONT_Multiplayer).GetField("MPsetupData", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(setupField?.FieldType == typeof(EngineInterface.MultiplayerSetupData), "Vanilla lobby setup field changed.");
            foreach (var wait in run.StartSkirmish("test_green.map", new[] { AILords.SK_RAT }))
            {
                yield return wait;
                var view = MainViewModel.Instance;
                var lobby = view.FRONTMultiplayer;
                if (!view.Show_MPGameCreation || lobby?.currentLobby == null || !lobby.currentLobby.isHost) continue;
                var setup = (EngineInterface.MultiplayerSetupData)setupField.GetValue(lobby);
                if (setup == null) continue;
                if ((setup.extreme_troops != 0) != extreme) lobby.ButtonClicked("Settings_ExtremeTroops");
                if (view.Show_MP_ExtremeWarning) lobby.ButtonClicked("CloseExtremeWarning");
                Require((setup.extreme_troops != 0) == extreme, "Vanilla declined the requested Extreme Troops setting.");
                if (pinnedGreenSlot >= 0 || !lobby.currentLobby.members.Any(m => m.SkirmishMember && !m.SkirmishHumanMember))
                    continue;

                var self = lobby.currentLobby.members.Single(m => m.SkirmishMember && m.SkirmishHumanMember && m.IsSelf());
                int player = lobby.currentLobby.getThisPlayerFromSteamID(self.GetSteamID());
                int[,] locations = GameData.Instance.Keep_Locations;
                int target = Enumerable.Range(0, Math.Min(locations.GetLength(0), setup.start_keep_location_order.Length))
                    .Where(i => locations[i, 0] >= 0 && locations[i, 1] >= 0)
                    .OrderByDescending(i => locations[i, 0]).ThenBy(i => locations[i, 1]).First();
                int source = Array.IndexOf(setup.start_keep_location_order, player - 1);
                Require(player > 0 && source >= 0, "No local test_green keep slot.");
                if (source != target)
                {
                    lobby.SkirmishRadar_OffClick(null, null);
                    lobby.ButtonClicked("Radar" + (source + 1).ToString(CultureInfo.InvariantCulture));
                    lobby.ButtonClicked("Radar" + (target + 1).ToString(CultureInfo.InvariantCulture));
                }
                Require(setup.start_keep_location_order[target] == player - 1, "Could not move the human player to the eastern keep.");
                pinnedGreenSlot = target;
                WriteInfo($"test_green player={player} slot={target + 1} radar={locations[target, 0]},{locations[target, 1]}");
            }
            Require(pinnedGreenSlot >= 0, "The test_green starting slot was not pinned.");
        }

        private static IEnumerable<AutomationWait> GreenFlight(AutomationRun run, System.Guid runId, bool extreme)
        {
            var mission = run.CurrentMission;
            var players = GamePlayerManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance;
            var projectiles = GameProjectileManagerAPI.Instance;
            int owner = players.GetLocalPlayerId();
            var shots = new List<int>();
            var archers = new List<int>();
            var source = run.InSimulation(mission, () =>
            {
                units.GetAllUnits(archers, AliveState.IsAlive, eChimps.CHIMP_TYPE_ARCHER, PlayerRelationship.Self, owner);
                var keep = players.GetPlayerKeepPosition(owner);
                int starting = archers.Count;
                if (starting == 0)
                {
                    var nearby = new List<int>();
                    units.GetAllUnits(nearby, AliveState.IsAlive, relationship: PlayerRelationship.Self, povPlayerId: owner);
                    WriteInfo("test_green starting troops=" + string.Join(",", nearby.Take(20)));
                    var terrain = GameTileManagerAPI.Instance;
                    for (int dx = -6; dx <= 6 && archers.Count == 0; dx++)
                    {
                        for (int dy = -6; dy <= 6 && archers.Count == 0; dy++)
                        {
                            int x = keep.X + dx, y = keep.Y + dy;
                            if (!terrain.IsTileInsideMapBounds(x, y)) continue;
                            int tile = terrain.GetTileId(x, y);
                            if (!terrain.IsTileWalkableAndUnoccupied(tile)) continue;
                            int idSpawned = checked((int)units.CreateUnitLocal(owner, owner, x, y, terrain.GetTileHeight(tile), eChimps.CHIMP_TYPE_ARCHER));
                            if (idSpawned > 0) archers.Add(idSpawned);
                        }
                    }
                    Require(archers.Count != 0, "Could not prepare an archer near the test_green keep.");
                }
                int id = archers.OrderBy(unit =>
                {
                    var pt = units.GetCurrentLocalTilePosition(unit);
                    return Math.Abs(pt.X - keep.X) + Math.Abs(pt.Y - keep.Y);
                }).ThenBy(unit => unit).First();
                var position = units.GetCurrentLocalTilePosition(id);
                WriteInfo($"test_green own keep={keep.X},{keep.Y} starting archers={starting} source={id} tile={position.X},{position.Y}");
                return (id, origin: new Vector2Int(position.X, position.Y));
            });

            var tiles = GameTileManagerAPI.Instance;
            Vector2Int target = run.InSimulation(mission, () =>
            {
                for (int d = 22; d >= 12; d--)
                {
                    foreach (var delta in new[] { new Vector2Int(-d, 0), new Vector2Int(0, d), new Vector2Int(0, -d), new Vector2Int(d, 0) })
                    {
                        var point = source.origin + delta;
                        if (tiles.IsTileInsideMapBounds(point.x, point.y) &&
                            tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(point.x, point.y))) return point;
                    }
                }
                throw new InvalidOperationException("No clear target within the archer fixture.");
            });
            var center = (source.origin + target) / 2;
            foreach (var wait in PositionCamera(run, center, Enums.Dircs.North)) yield return wait;
            for (int step = 0; step < 3 && Camera.main.orthographicSize > 12f; step++)
                foreach (var wait in run.ZoomStep(mission, true, _ => { })) yield return wait;
            players.SetScreenCenterToTilePosition(new System.Numerics.Vector2(center.x, center.y));
            Director.instance.SetEngineFrameRate(15);
            yield return run.WaitForTicks(mission, 3);
            traceVisibility = true;

            int tribe = run.InSimulation(mission, () =>
            {
                var tribes = GameTribeManagerAPI.Instance;
                int group = checked((int)tribes.Create(owner, false));
                return group > 0 && tribes.AssignUnit(group, source.id) && tribes.AttackTile(group, target.x, target.y) ? group : 0;
            });
            int shotId = 0;
            for (var timer = System.Diagnostics.Stopwatch.StartNew(); tribe > 0 && timer.Elapsed.TotalSeconds < 6;)
            {
                yield return AutomationWait.NextFrame;
                shotId = run.InSimulation(mission, () =>
                {
                    projectiles.GetAllProjectiles(shots, projType: ProjectileType.ArcherArrow);
                    return shots.FirstOrDefault(id => projectiles.GetSourceUnit(id) == source.id);
                });
                if (shotId > 0) break;
            }
            bool natural = shotId > 0;
            if (!natural)
            {
                shotId = run.InSimulation(mission, () =>
                {
                    int elevation = tiles.GetTileHeight(tiles.GetTileId(source.origin.x, source.origin.y)) + 96;
                    return checked((int)projectiles.CreateProjectile(source.id, owner, FineTileCentre(source.origin.x),
                        FineTileCentre(source.origin.y), elevation, FineTileCentre(target.x), FineTileCentre(target.y),
                        elevation, ProjectileType.ArcherArrow, 0));
                });
            }
            Require(shotId > 0, "The starting archer fixture failed to launch an arrow.");
            WriteInfo($"test_green launch extreme={extreme} source={source.id} slot={shotId} natural={natural} origin={source.origin} target={target} tick={native.Tick}");

            int peak = 0;
            uint firstTick = native.Tick, recorded = uint.MaxValue;
            for (var timer = System.Diagnostics.Stopwatch.StartNew(); timer.Elapsed.TotalSeconds < 10;)
            {
                yield return AutomationWait.NextFrame;
                uint tick = native.Tick;
                if (tick == recorded) continue;
                recorded = tick;
                int count = extreme ? LastSegmentCount : CountVanillaReference();
                peak = Math.Max(peak, count);
                WriteInfo($"test_green frame extreme={extreme} tick={tick} copies={count}");
                if (tick - firstTick >= 16 && peak >= 10) break;
            }
            Require(peak >= 10 && peak <= (extreme ? 10 : 11), "Unexpected peak of simultaneous afterimages: " + peak);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            int paused = extreme ? LastSegmentCount : CountVanillaReference();
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(350));
            Require(paused > 0 && paused == (extreme ? LastSegmentCount : CountVanillaReference()), "Afterimages changed while paused.");
            WriteInfo($"GREEN_RESULT extreme={extreme} natural={natural} peak={peak} paused={paused} rotation={GameMap.instance.CurrentRotation()} zoom={PerfectPixelWithZoom.instance.getZoom()}");
            foreach (var wait in Capture(runId, extreme ? "test-green-extreme" : "test-green-standard")) yield return wait;
            traceVisibility = false;
        }

        private static int CountVanillaReference()
        {
            if (referenceFlies == null) return 0;
            int count = 0;
            foreach (int id in referenceIds)
                if (referenceFlies.TryGetValue(id, out var fly) && fly.type == (int)Enums.GM.GM_BODY_MISSILE &&
                    fly.sprRenderer != null && fly.sprRenderer.enabled && fly.sprRenderer.gameObject.activeInHierarchy)
                    count++;
            return count;
        }
    }
}
#endif
