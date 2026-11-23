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

        private static IEnumerable<AutomationWait> GreenFlight(AutomationRun run, System.Guid runId, bool extreme, bool replace,
            bool hidden, bool fire, bool unavailable, bool restarted = false)
        {
            bool nativeReference = !extreme && (!replace || unavailable);
            Require(ReplaceStandard == (replace && !unavailable), "Unexpected native Standard replacement decision.");
            bool usesModTrails = !nativeReference && !hidden && !fire;
            var mission = run.CurrentMission;
            var players = GamePlayerManagerAPI.Instance;
            var units = GameUnitManagerAPI.Instance;
            var projectiles = GameProjectileManagerAPI.Instance;
            int owner = players.GetLocalPlayerId();
            Require(owner > 0, "The local test_green player slot is unavailable.");
            var archers = new List<int>();
            var keep = run.InSimulation(mission, () => players.GetPlayerKeepPosition(owner));
            var keepTile = new Vector2Int(keep.X, keep.Y);
            var source = run.InSimulation(mission, () =>
            {
                units.GetAllUnits(archers, AliveState.IsAlive, eChimps.CHIMP_TYPE_ARCHER, PlayerRelationship.Self, owner);
                return FindFixtureArcher(units, archers, keepTile);
            });
            int starting = archers.Count;
            if (source.id == 0)
            {
                int spawnedId = run.InSimulation(mission, () =>
                {
                    var terrain = GameTileManagerAPI.Instance;
                    for (int dx = -6; dx <= 6; dx++)
                    {
                        for (int dy = -6; dy <= 6; dy++)
                        {
                            int x = keep.X + dx, y = keep.Y + dy;
                            if (!terrain.IsTileInsideMapBounds(x, y)) continue;
                            int tile = terrain.GetTileId(x, y);
                            if (!terrain.IsTileWalkableAndUnoccupied(tile)) continue;
                            int id = checked((int)units.CreateUnitLocal(owner, owner, x, y, terrain.GetTileHeight(tile), eChimps.CHIMP_TYPE_ARCHER));
                            if (id > 0) return id;
                        }
                    }
                    return 0;
                });
                Require(spawnedId > 0, "Could not prepare an archer near the test_green keep.");
                archers.Clear();
                archers.Add(spawnedId);
                // Creation returns a slot before the next simulation tick initializes its life state.
                for (int attempt = 0; attempt < 6 && source.id == 0; attempt++)
                {
                    yield return run.WaitForTicks(mission, 1);
                    source = run.InSimulation(mission, () => FindFixtureArcher(units, archers, keepTile));
                }
                Require(source.id == spawnedId, "The spawned fixture archer did not enter the live state.");
            }
            WriteInfo($"test_green own keep={keep.X},{keep.Y} starting archers={starting} source={source.id} tile={source.origin.x},{source.origin.y}");

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
            // 0x5B is Vanilla's ignited ArcherArrow launch alias. The native creator emits
            // the normal ArcherArrow record with the ignited sprite and Steam1 trail.
            const ProjectileType fireArrowLaunch = (ProjectileType)0x5B;
            int shotId = run.InSimulation(mission, () =>
            {
                int elevation = tiles.GetTileHeight(tiles.GetTileId(source.origin.x, source.origin.y)) + 96;
                return checked((int)projectiles.CreateProjectile(source.id, owner, FineTileCentre(source.origin.x),
                    FineTileCentre(source.origin.y), elevation, FineTileCentre(target.x), FineTileCentre(target.y),
                    elevation, fire ? fireArrowLaunch : ProjectileType.ArcherArrow, 0));
            });
            Require(shotId > 0, "The starting archer fixture failed to launch an arrow.");
            WriteInfo($"test_green launch extreme={extreme} replace={replace} hidden={hidden} fire={fire} unavailable={unavailable} source={source.id} slot={shotId} origin={source.origin} target={target} tick={MapTick}");

            int peak = 0, nativePeak = 0, smokePeak = 0;
            uint firstTick = MapTick, recorded = uint.MaxValue;
            for (var timer = System.Diagnostics.Stopwatch.StartNew(); timer.Elapsed.TotalSeconds < 10;)
            {
                yield return AutomationWait.NextFrame;
                uint tick = MapTick;
                if (tick == recorded) continue;
                recorded = tick;
                int nativeCount = run.InSimulation(mission, CountNativeArrowAfterimages);
                int smokeCount = fire ? run.InSimulation(mission, CountFireSmoke) : 0;
                nativePeak = Math.Max(nativePeak, nativeCount);
                smokePeak = Math.Max(smokePeak, smokeCount);
                int count = nativeReference ? CountVanillaReference(tick, trace: true) : LastSegmentCount;
                if (nativeReference) Require(LastSegmentCount == 0, "The renderer published copies alongside the original afterimages.");
                if (usesModTrails && count > 0) TraceModEchoes(tick, count);
                peak = Math.Max(peak, count);
                WriteInfo($"test_green frame extreme={extreme} tick={tick} copies={count} native={nativeCount} smoke={smokeCount}");
                if (unchecked(tick - firstTick) >= (fire ? 8 : 16)) break;
            }
            if (hidden || fire)
                Require(peak == 0, "Unexpected mod afterimages with display off or on an ignited arrow.");
            else
                Require(peak >= 10 && peak <= (usesModTrails ? 10 : 11),
                    "Unexpected peak of simultaneous afterimages: " + peak);
            Require((nativePeak != 0) == nativeReference, "Unexpected native arrow afterimage occupancy.");
            if (fire) Require(smokePeak > 0, "The ignited arrow did not emit Vanilla smoke with replacement enabled.");

            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            int paused = nativeReference ? CountVanillaReference(MapTick, trace: false) : LastSegmentCount;
            yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(350));
            Require((hidden || fire || paused > 0) &&
                    paused == (nativeReference ? CountVanillaReference(MapTick, trace: false) : LastSegmentCount),
                "Unexpected afterimages while paused.");
            WriteInfo($"GREEN_RESULT extreme={extreme} replace={replace} hidden={hidden} fire={fire} unavailable={unavailable} peak={peak} paused={paused} nativePeak={nativePeak} smokePeak={smokePeak} rotation={GameMap.instance.CurrentRotation()} zoom={PerfectPixelWithZoom.instance.getZoom()}");
            string label = unavailable ? "test-green-standard-unavailable" : hidden ? "test-green-standard-hidden" : fire ? "test-green-fire" :
                replace ? "test-green-standard-replaced" : extreme ? "test-green-extreme" : "test-green-standard";
            if (restarted) label += "-after-restart";
            foreach (var wait in Capture(runId, label)) yield return wait;
        }

        private static int CountNativeArrowAfterimages()
        {
            int count = 0;
            foreach (ref readonly var projectile in GameProjectileManagerAPI.Instance.GetProjectilesAsSpan())
                if (projectile.r_AliveState != AliveState.None && projectile.r_ProjectileType == ProjectileType.AfterImage &&
                    (int)projectile.r_GameMaterial == (int)Enums.GM.GM_BODY_MISSILE) count++;
            return count;
        }

        private static int CountFireSmoke()
        {
            int count = 0;
            foreach (ref readonly var projectile in GameProjectileManagerAPI.Instance.GetProjectilesAsSpan())
                if (projectile.r_AliveState != AliveState.None && projectile.r_ProjectileType == ProjectileType.Steam1 &&
                    (int)projectile.r_GameMaterial == (int)Enums.GM.GM_BODY_STEAM) count++;
            return count;
        }

        private static int CountVanillaReference(uint tick, bool trace)
        {
            var flies = (Dictionary<int, Fly>)VanillaFlies.GetValue(GameMap.instance);
            int count = 0;
            foreach (var fly in flies.Values)
            {
                var renderer = fly.sprRenderer;
                if (fly.gameObjectType != (int)ProjectileType.AfterImage || fly.type != (int)Enums.GM.GM_BODY_MISSILE ||
                    renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                count++;
                if (trace) LogEcho("vanilla", tick, renderer.transform.position, renderer.sortingOrder, renderer.color.a);
            }
            return count;
        }

        private static unsafe (int id, Vector2Int origin) FindFixtureArcher(GameUnitManagerAPI units, List<int> archers, Vector2Int keep)
        {
            int nearestId = 0;
            int nearestDistance = int.MaxValue;
            Vector2Int origin = default;
            foreach (int id in archers)
            {
                if (!APIShared.UnitAccess.TryGetById(units, id, out var unit, out _) || !APIShared.UnitAccess.IsReallyAlive(unit))
                    continue;
                var tile = new Vector2Int(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY);
                int distance = Math.Abs(tile.x - keep.x) + Math.Abs(tile.y - keep.y);
                if (distance > nearestDistance || distance == nearestDistance && id > nearestId) continue;
                nearestId = id;
                nearestDistance = distance;
                origin = tile;
            }
            return (nearestId, origin);
        }

        private static void LogEcho(string source, uint tick, Vector3 position, int order, float alpha) =>
            WriteInfo(FormattableString.Invariant($"ECHO source={source} tick={tick} x={position.x:R} y={position.y:R} order={order} alpha={alpha:R}"));
    }
}
