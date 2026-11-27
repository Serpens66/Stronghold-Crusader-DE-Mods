using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using APIShared;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    internal static partial class PairedResyncSteps
    {
        private static (int X, int Y) moat, buildingA, buildingB;
        private static bool haveMoat, haveHuts;
        private static object engineGate;

        private static void RegisterMoat()
        {
            Runner.Register("Paired.CreateMoat", CreateMoat);
            Runner.Register("Paired.SelectFixture", SelectFixture);
            Runner.Register("Paired.ReceiveFixture", ReceiveFixture);
            Runner.Register("Paired.WaitMoat", s => WaitForTerrain(dug: true));
            Runner.Register("Paired.RemoveMoat", RemoveMoat);
            Runner.Register("Paired.WaitSoil", s => WaitForTerrain(dug: false));
            Runner.Register("Paired.ClientCheckpoint", ClientCheckpoint);
            Runner.Register("Paired.WaitCheckpoint", s => Checkpoint(s.Arg(0)));
            Runner.Register("Paired.HostSetup", s => Checkpoint("host-setup"));
            Runner.Register("Paired.PauseBuildings", PauseBuildings);
            Runner.Register("Paired.CheckPausedBuilding", CheckPausedBuilding);
        }

        private static IEnumerable<Wait> PauseBuildings(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
            RequireHuts();
            foreach (var point in new[] { buildingA, buildingB })
            {
                lock (Gate())
                {
                    Runner.Require(player > 0 && IsClearPad(point, grass: false),
                        "production building fixture is occupied");
                    PlaceMapper(eMappers.MAPPER_WOODSMAN, point.X, point.Y, player);
                }
                yield return new Until("woodcutter hut admitted", () =>
                {
                    lock (Gate()) return BuildingAt(point).Owner == player;
                });
            }

            lock (Gate())
            {
                var first = BuildingAt(buildingA);
                var second = BuildingAt(buildingB);
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

        private static IEnumerable<Wait> SelectFixture(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            int before = Volatile.Read(ref UiAcceptancePlugin.Ticks);
            yield return new Until("a simulation tick before selecting test tiles", () =>
                Volatile.Read(ref UiAcceptancePlugin.Ticks) > before);
            lock (Gate())
            {
                RequireMap();
                var origin = HostKeepPosition();
                var moatPad = FindPad(origin, grass: true);
                var reserves = moatPad.HasValue ? new[] { moatPad.Value } : Array.Empty<(int, int)>();
                var firstHut = FindPad(origin, grass: false, reserves);
                var secondHut = firstHut.HasValue
                    ? FindPad(origin, grass: false, moatPad.HasValue ? new[] { moatPad.Value, firstHut.Value } : new[] { firstHut.Value })
                    : null;
                haveMoat = moatPad.HasValue;
                haveHuts = firstHut.HasValue && secondHut.HasValue;
                moat = moatPad.GetValueOrDefault();
                buildingA = firstHut.GetValueOrDefault();
                buildingB = secondHut.GetValueOrDefault();
            }
            AcceptLog.Info($"PAIRED_FIXTURE run={AcceptLog.RunId} moat={FormatPoint(moat, haveMoat)} " +
                $"hutA={FormatPoint(buildingA, haveHuts)} hutB={FormatPoint(buildingB, haveHuts)}");
        }

        private static string FormatPoint((int X, int Y) point, bool available) => available ? $"{point.X},{point.Y}" : "none";

        private static unsafe (int X, int Y) HostKeepPosition()
        {
            int host = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
            Runner.Require(host > 0, "the local host player is unavailable");
            int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(host);
            if (!BuildingAccess.TryGetById(keepId, out GameBuilding* keep, out var failure))
                throw new StepFailure("the host keep is unavailable: " + failure);
            Runner.Require(keep->r_PlayerIdOwner == host, "the reported keep does not belong to the host");
            return (keep->r_TilePositionXBegin, keep->r_TilePositionYBegin);
        }

        private static (int X, int Y)? FindPad((int X, int Y) origin, bool grass, params (int X, int Y)[] reserved)
        {
            // Search near the host first, then widen the search. All candidates are examined on the loaded map.
            for (int radius = 12; radius <= 180; radius += 3)
                for (int dy = -radius; dy <= radius; dy += 3)
                    for (int dx = -radius; dx <= radius; dx += 3)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != radius) continue;
                        var point = (X: origin.X + dx, Y: origin.Y + dy);
                        if (Array.Exists(reserved, other => Math.Abs(point.X - other.X) < 8 && Math.Abs(point.Y - other.Y) < 8)) continue;
                        if (IsClearPad(point, grass)) return point;
                    }
            return null;
        }

        private static bool IsClearPad((int X, int Y) point, bool grass)
        {
            if (grass) return TerrainMatches(point, dug: false, vacancy: true);
            var tiles = GameTileManagerAPI.Instance;
            var height = tiles.GetHeightLayer();
            var logic = tiles.GetLogicLayer();
            var organisms = tiles.GetOrganismLayer();
            var units = tiles.GetChimpLayer();
            var structures = tiles.GetStructureLayer();
            int? level = null;
            for (int y = point.Y - 2; y <= point.Y + 2; y++)
                for (int x = point.X - 2; x <= point.X + 2; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y)) return false;
                    int id = tiles.GetTileId(x, y);
                    if (!tiles.IsValidTileId(id) || units[id] != 0 || structures[id] != 0 || organisms[id] != 0 ||
                        (TilePropertyFlag)logic[id] != TilePropertyFlag.IsLand) return false;
                    level ??= height[id];
                    if (height[id] != level.Value) return false;
                }
            return true;
        }

        private static IEnumerable<Wait> ReceiveFixture(Step step)
        {
            RequireRole(host: false);
            var path = CheckpointPath("fixture");
            yield return new Until("host test tile selection", () => { RequireTerrainAvailable(); return File.Exists(path); });
            var fields = File.ReadAllLines(path);
            Runner.Require(fields.Length == 4 && fields[0] == AcceptLog.RunId, "invalid host fixture data");
            haveMoat = fields[1] != "none";
            haveHuts = fields[2] != "none" && fields[3] != "none";
            if (haveMoat) moat = ParsePoint(fields[1]);
            if (haveHuts)
            {
                buildingA = ParsePoint(fields[2]);
                buildingB = ParsePoint(fields[3]);
            }
        }

        private static (int X, int Y) ParsePoint(string data)
        {
            var parts = data.Split(',');
            if (parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int x) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int y) ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(x, y)) throw new StepFailure("invalid host test tile: " + data);
            return (x, y);
        }

        private static void RequireHuts() => Runner.Require(haveHuts, "no clear pads were found for two woodcutter huts");
        private static void RequireMoat() => Runner.Require(haveMoat, "no clear 3x3 oasis grass was found for the moat");

        private static void RequireTerrainAvailable() =>
            Runner.Require(!File.Exists(CheckpointPath("terrain-aborted")), "the host could not complete the terrain test");

        private static IEnumerable<Wait> CheckPausedBuilding(Step step)
        {
            RequireMap();
            RequireHuts();
            yield return new Until("only woodcutter A has an active pause override", () =>
            {
                lock (Gate())
                {
                    var first = BuildingAt(buildingA);
                    var second = BuildingAt(buildingB);
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

        private static unsafe (int Global, int Owner, bool Sleeping) BuildingAt((int X, int Y) point)
        {
            var tiles = GameTileManagerAPI.Instance;
            int id = tiles.GetTileBuildingId(tiles.GetTileId(point.X, point.Y));
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
            RequireTerrainAvailable();
            RequireMoat();
            lock (Gate())
            {
                int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
                Runner.Require(player > 0, "host player is not resolved");
                Runner.Require(TerrainMatches(moat, dug: false, vacancy: true), "the nine fixture tiles are no longer flat, clear oasis grass");
                PlaceMapper(eMappers.MAPPER_DUGMOAT, moat.X, moat.Y, player);
            }
            foreach (var wait in WaitForTerrain(dug: true)) yield return wait;
            AcceptLog.Info("PAIRED_MOAT_CREATED x=" + moat.X + " y=" + moat.Y);
        }

        private static IEnumerable<Wait> RemoveMoat(Step step)
        {
            RequireRole(host: true);
            RequireMap();
            RequireTerrainAvailable();
            RequireMoat();
            lock (Gate())
            {
                int player = UiAcceptancePlugin.Missions.Current.LocalPlayerId ?? 0;
                Runner.Require(player > 0 && TerrainMatches(moat, dug: true, vacancy: true), "the repaired test moat is unavailable or occupied");
                PlaceMapper(eMappers.MAPPER_UNDUGMOAT, moat.X, moat.Y, player);
            }
            foreach (var wait in WaitForTerrain(dug: false)) yield return wait;
            AcceptLog.Info("PAIRED_MOAT_SOIL_RESTORED tiles=9");
        }

        private static IEnumerable<Wait> WaitForTerrain(bool dug)
        {
            RequireMap();
            RequireTerrainAvailable();
            RequireMoat();
            yield return new Until(dug ? "nine dug moat cells" : "restored oasis grass on all nine cells", () =>
            {
                RequireTerrainAvailable();
                lock (Gate()) return TerrainMatches(moat, dug, vacancy: false);
            });
        }

        private static IEnumerable<Wait> ClientCheckpoint(Step step)
        {
            RequireRole(host: true);
            string checkpoint = step.Arg(0);
            Runner.Require(checkpoint == "moat" || checkpoint == "soil", "unknown client checkpoint");
            var path = CheckpointPath(checkpoint);
            yield return new Until("paired checkpoint " + checkpoint, () => { RequireTerrainAvailable(); return File.Exists(path); });
        }

        private static IEnumerable<Wait> Checkpoint(string checkpoint)
        {
            var path = CheckpointPath(checkpoint);
            yield return new Until("paired checkpoint " + checkpoint, () => File.Exists(path));
        }

        private static string CheckpointPath(string checkpoint)
        {
            Runner.Require(Regex.IsMatch(AcceptLog.RunId, "^paired-[a-f0-9]+$"), "invalid paired run label");
            Runner.Require(Regex.IsMatch(checkpoint, "^[A-Za-z0-9_-]{1,48}$"), "invalid paired checkpoint name");
            return Path.Combine(BepInEx.Paths.ConfigPath, "UiAcceptance", AcceptLog.RunId + "-" + checkpoint + ".ok");
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

        private static bool TerrainMatches((int X, int Y) point, bool dug, bool vacancy)
        {
            var tiles = GameTileManagerAPI.Instance;
            var logic = tiles.GetLogicLayer();
            var soil = tiles.GetLogic2Layer();
            var height = tiles.GetHeightLayer();
            var organisms = tiles.GetOrganismLayer();
            var units = tiles.GetChimpLayer();
            var structures = tiles.GetStructureLayer();
            for (int y = point.Y - 1; y <= point.Y + 1; y++)
                for (int x = point.X - 1; x <= point.X + 1; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y)) return false;
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
            var mission = UiAcceptancePlugin.Missions?.Current;
            Runner.Require(Runner.Active.Series.StartsWith("paired-resync-", StringComparison.Ordinal) &&
                mission?.Mode.IsRealMultiplayer == true, "the fixture is only valid in the dedicated paired run");
        }
    }
}
