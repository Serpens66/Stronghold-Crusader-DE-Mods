using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using APIShared;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static partial class PairedResyncSteps
    {
        // The Jordan Valley, native map coordinates on the verified grass pad near keep position 0.
        private const int MoatX = 497, MoatY = 438;
        private const string MapName = "TheJordanValley.map";
        private const int BuildingAX = 516, BuildingBX = 548, BuildingY = 418;
        private static object engineGate;

        private static void RegisterMoat()
        {
            Runner.Register("Paired.CreateMoat", CreateMoat);
            Runner.Register("Paired.WaitMoat", s => WaitForTerrain(dug: true));
            Runner.Register("Paired.RemoveMoat", RemoveMoat);
            Runner.Register("Paired.WaitSoil", s => WaitForTerrain(dug: false));
            Runner.Register("Paired.ClientCheckpoint", ClientCheckpoint);
            Runner.Register("Paired.HostSetup", s => Checkpoint("host-setup"));
            Runner.Register("Paired.PauseBuildings", PauseBuildings);
            Runner.Register("Paired.CheckPausedBuilding", CheckPausedBuilding);
        }

        private static IEnumerable<Wait> PauseBuildings(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
            foreach (int x in new[] { BuildingAX, BuildingBX })
            {
                lock (Gate())
                {
                    var tiles = GameTileManagerAPI.Instance;
                    int tile = tiles.GetTileId(x, BuildingY);
                    Runner.Require(player > 0 && tile >= 0 && tiles.IsTileWalkableAndUnoccupied(tile),
                        "production building fixture is occupied");
                    PlaceMapper(eMappers.MAPPER_WOODSMAN, x, BuildingY, player);
                }
                yield return new Until("woodcutter hut admitted", () =>
                {
                    lock (Gate()) return BuildingAt(x).Owner == player;
                });
            }

            lock (Gate())
            {
                var first = BuildingAt(BuildingAX);
                var second = BuildingAt(BuildingBX);
                Runner.Require(first.Global > 0 && first.Global != second.Global && !first.Sleeping && !second.Sleeping,
                    "both woodcutter huts must be live and initially unpaused");
                object hook = PauseHook();
                const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
                var send = hook.GetType().GetMethod("TrySendPauseChore", instance, null,
                    new[] { typeof(int), typeof(int), typeof(bool), typeof(int), typeof(bool) }, null);
                var action = hook.GetType().GetField("SetSingleBuildingAction", BindingFlags.Static | BindingFlags.NonPublic);
                Runner.Require(send?.ReturnType == typeof(bool) && action?.FieldType == typeof(int),
                    "single-building synchronized sender changed");
                Runner.Require((bool)send.Invoke(hook, new[] { (object)player, first.Global, true, action.GetRawConstantValue(), false }),
                    "the normal single-building pause Chore was not queued");
            }
            foreach (var wait in CheckPausedBuilding(step)) yield return wait;
        }

        private static IEnumerable<Wait> CheckPausedBuilding(Step step)
        {
            RequireMap();
            yield return new Until("only woodcutter A has an active pause override", () =>
            {
                lock (Gate())
                {
                    var first = BuildingAt(BuildingAX);
                    var second = BuildingAt(BuildingBX);
                    if (first.Global <= 0 || second.Global <= 0 || first.Global == second.Global ||
                        first.Owner != second.Owner || !first.Sleeping || second.Sleeping) return false;
                    const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                    object hook = PauseHook();
                    var store = hook.GetType().GetField("overrides", privateInstance)?.GetValue(hook);
                    var snapshot = store?.GetType().GetMethod("Snapshot", privateInstance);
                    Runner.Require(snapshot != null, "single-building pause override store changed");
                    foreach (object entry in (Array)snapshot.Invoke(store, null))
                    {
                        var type = entry.GetType();
                        if (Equals(type.GetProperty("GlobalId", privateInstance)?.GetValue(entry), first.Global) &&
                            Equals(type.GetProperty("IsSleeping", privateInstance)?.GetValue(entry), true))
                            return true;
                    }
                    return false;
                }
            });
            AcceptLog.Info("PAIRED_BUILDING_PAUSE_CONFIRMED");
        }

        private static unsafe (int Global, int Owner, bool Sleeping) BuildingAt(int x)
        {
            var tiles = GameTileManagerAPI.Instance;
            int id = tiles.GetTileBuildingId(tiles.GetTileId(x, BuildingY));
            if (!BuildingAccess.TryGetById(id, out GameBuilding* building, out _) ||
                building->r_BuildingType != eStructs.STRUCT_WOODCUTTERS_HUT) return default;
            return (unchecked((int)building->r_GlobalId), building->r_PlayerIdOwner, building->r_IsSleeping != 0);
        }

        private static object PauseHook()
        {
            const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;
            Runner.Require(BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out var mod) &&
                new Version(1, 0, 237).Equals(mod.Metadata.Version), "paired building probe requires BugfixesAndQoL 1.0.237");
            var runtime = mod.Instance.GetType().GetField("runtime", instance)?.GetValue(mod.Instance);
            var hook = runtime?.GetType().GetField("singleBuildingPauseHook", instance)?.GetValue(runtime);
            Runner.Require(hook != null, "single-building pause owner is unavailable");
            return hook;
        }

        private static IEnumerable<Wait> CreateMoat(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            lock (Gate())
            {
                var players = GamePlayerManagerAPI.Instance;
                int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
                Runner.Require(player > 0, "host player is not resolved");
                var keep = players.GetPlayerKeepPosition(player);
                Runner.Require(keep.X == 532 && keep.Y == 418, "wrong host keep for The Jordan Valley fixture");
                Runner.Require(TerrainMatches(dug: false, vacancy: true), "the nine fixture tiles are no longer flat, clear oasis grass");
                PlaceMapper(eMappers.MAPPER_DUGMOAT, MoatX, MoatY, player);
            }
            foreach (var wait in WaitForTerrain(dug: true)) yield return wait;
            AcceptLog.Info("PAIRED_MOAT_CREATED x=" + MoatX + " y=" + MoatY);
        }

        private static IEnumerable<Wait> RemoveMoat(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            lock (Gate())
            {
                int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
                Runner.Require(player > 0 && TerrainMatches(dug: true, vacancy: true), "the repaired test moat is unavailable or occupied");
                PlaceMapper(eMappers.MAPPER_UNDUGMOAT, MoatX, MoatY, player);
            }
            foreach (var wait in WaitForTerrain(dug: false)) yield return wait;
            AcceptLog.Info("PAIRED_MOAT_SOIL_RESTORED tiles=9");
        }

        private static IEnumerable<Wait> WaitForTerrain(bool dug)
        {
            RequireMap();
            yield return new Until(dug ? "nine dug moat cells" : "restored oasis grass on all nine cells", () =>
            {
                lock (Gate()) return TerrainMatches(dug, vacancy: false);
            });
        }

        private static IEnumerable<Wait> ClientCheckpoint(Step step)
        {
            RequireRole(host: true);
            string checkpoint = step.Arg(0);
            Runner.Require(checkpoint == "moat" || checkpoint == "soil", "unknown client checkpoint");
            foreach (var wait in Checkpoint(checkpoint)) yield return wait;
        }

        private static IEnumerable<Wait> Checkpoint(string checkpoint)
        {
            Runner.Require(Regex.IsMatch(AcceptLog.RunId, "^paired-[a-f0-9]+$"), "invalid paired run label");
            var path = Path.Combine(BepInEx.Paths.ConfigPath, "UiAcceptance", AcceptLog.RunId + "-" + checkpoint + ".ok");
            yield return new Until("paired checkpoint " + checkpoint, () => File.Exists(path));
        }

        private static void PlaceMapper(eMappers mapper, int x, int y, int player)
        {
            // The public Vanilla mapper path sends Chore 21. The completed and removal
            // variants operate on its normal three-by-three brush in a live MP game.
            int nativeMapper = (int)mapper;
            Runner.Require(EngineInterface.StartMapperItem(nativeMapper) == nativeMapper, "Vanilla declined " + mapper);
            try { EngineInterface.PlaceMapperItem(nativeMapper, x, y, 0, player, true, false, 1); }
            finally { EngineInterface.StartMapperItem(0); }
        }

        private static bool TerrainMatches(bool dug, bool vacancy)
        {
            var tiles = GameTileManagerAPI.Instance;
            var logic = tiles.GetLogicLayer();
            var soil = tiles.GetLogic2Layer();
            var height = tiles.GetHeightLayer();
            var organisms = tiles.GetOrganismLayer();
            var units = tiles.GetChimpLayer();
            var structures = tiles.GetStructureLayer();
            for (int y = MoatY - 1; y <= MoatY + 1; y++)
                for (int x = MoatX - 1; x <= MoatX + 1; x++)
                {
                    int tile = tiles.GetTileId(x, y);
                    if ((uint)tile >= (uint)logic.Length || (uint)tile >= (uint)soil.Length ||
                        (uint)tile >= (uint)height.Length || (uint)tile >= (uint)organisms.Length ||
                        (uint)tile >= (uint)units.Length || (uint)tile >= (uint)structures.Length) return false;
                    var flags = (TilePropertyFlag)logic[tile];
                    if (vacancy && (units[tile] != 0 || structures[tile] != 0 || organisms[tile] != 0)) return false;
                    if (dug)
                    {
                        if ((flags & TilePropertyFlag.IsMoat) == 0 || soil[tile] == (byte)TileType.OasisGrass) return false;
                    }
                    else
                    {
                        if ((flags & (TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat)) != 0 ||
                            soil[tile] != (byte)TileType.OasisGrass || height[tile] != 8 ||
                            (vacancy && flags != TilePropertyFlag.IsLand)) return false;
                    }
                }
            return true;
        }

        private static object Gate()
        {
            if (engineGate == null)
            {
                var field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
                Runner.Require(field?.FieldType == typeof(object), "engine monitor contract differs");
                engineGate = field.GetValue(null);
                Runner.Require(engineGate != null, "no engine monitor");
            }
            return engineGate;
        }

        private static void RequireMap()
        {
            // Both roles also validate the map and its agreed layout in the preceding lobby/mission steps.
            var mission = UiAcceptancePlugin.Missions?.Current;
            Runner.Require(Runner.Active.Series.StartsWith("paired-resync-", StringComparison.Ordinal) &&
                mission?.Mode.IsRealMultiplayer == true && mission.MapSize == 400 &&
                (string.Equals(Path.GetFileName(mission.FilePath), MapName, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(mission.MapName, "The Jordan Valley", StringComparison.OrdinalIgnoreCase)),
                "The Jordan Valley fixture is only valid in the dedicated paired run");
        }
    }
}
