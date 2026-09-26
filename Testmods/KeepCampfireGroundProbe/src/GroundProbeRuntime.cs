using System;
using System.Collections.Generic;
using APIShared;
using SHCDESE.API;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Input;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace KeepCampfireGroundProbe
{
    internal sealed class GroundProbeRuntime
    {
        private const int KeepRadius = 10;
        private const int CampRadius = 5;
        private const int CoreRadius = 2;
        private const int MaxObservations = 32;

        private readonly Action<string> log;
        private readonly object gate = new object();
        private readonly List<Observation> observations = new List<Observation>();
        private readonly Dictionary<string, ComparisonBaseline> saveReferences =
            new Dictionary<string, ComparisonBaseline>();
        private long sessionId;
        private bool active;
        private bool manualRequested;
        private bool firstTickReported;
        private bool firstRenderReported;

        internal GroundProbeRuntime(Action<string> log)
        {
            this.log = log;
        }

        internal void OnInitialization(MissionLifecycleNotification notification)
        {
            lock (gate)
            {
                if (notification.Phase == MissionInitializationPhase.BeforeLoad)
                {
                    observations.Clear();
                    if (!notification.Context.IsSave) saveReferences.Clear();
                    active = false;
                    manualRequested = false;
                    firstTickReported = false;
                    firstRenderReported = false;
                }
                sessionId = notification.Context.SessionId;
                log("lifecycle session=" + sessionId + " init=" + notification.Phase +
                    " kind=" + notification.Context.StartKind + " mode=" +
                    notification.Context.Mode.ToDiagnosticString());
            }
        }

        internal void OnStart(MissionLifecycleNotification notification)
        {
            lock (gate)
            {
                sessionId = notification.Context.SessionId;
                active = true;
                log("lifecycle session=" + sessionId + " START kind=" +
                    notification.Context.StartKind + " replay=" + notification.IsReplay +
                    " mode=" + notification.Context.Mode.ToDiagnosticString());
                try
                {
                    DiscoverExistingBuildings();
                    foreach (Observation observation in observations)
                    {
                        if (notification.Context.IsSave &&
                            saveReferences.TryGetValue(observation.Key, out ComparisonBaseline reference))
                        {
                            observation.Native = reference.Native;
                            observation.Managed = reference.Managed;
                            observation.NativeCount = 1;
                            log("save comparison restored for " + observation.Label);
                        }
                        CaptureNative(observation, "map-start", true);
                        observation.NativeChecksRemaining = 2;
                        observation.RenderChecksRemaining = 3;
                    }
                }
                catch (Exception ex) { log("map-start capture failed: " + ex); }
            }
        }

        internal void OnEnd(MissionLifecycleNotification notification)
        {
            lock (gate)
            {
                log("lifecycle session=" + notification.Context.SessionId + " END reason=" +
                    notification.EndReason + " targets=" + observations.Count);
                active = false;
                observations.Clear();
                manualRequested = false;
            }
        }

        internal void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!IsTarget(args.Building))
                return;

            lock (gate)
            {
                try
                {
                    if (args.Phase == SHCDESE.EventAPI.EventHookPhase.Pre)
                    {
                        if (observations.Count >= MaxObservations)
                        {
                            log("target cap reached; ignored spawn " + args.Building);
                            return;
                        }
                        Observation observation = new Observation(args.Building, args.PlayerId,
                            args.TileX, args.TileY, IsKeep(args.Building) ? KeepRadius : CampRadius);
                        observations.Add(observation);
                        log("spawn PRE session=" + sessionId + " type=" + args.Building +
                            " player=" + args.PlayerId + " origin=" + args.TileX + "," + args.TileY);
                        CaptureNative(observation, "spawn-pre", true);
                    }
                    else if (args.Phase == SHCDESE.EventAPI.EventHookPhase.Post)
                    {
                        Observation observation = FindUnpaired(args);
                        if (observation == null)
                        {
                            if (observations.Count >= MaxObservations) return;
                            observation = new Observation(args.Building, args.PlayerId,
                                args.TileX, args.TileY, IsKeep(args.Building) ? KeepRadius : CampRadius);
                            observations.Add(observation);
                            log("spawn POST without PRE; original ground unavailable at this event");
                        }
                        observation.PostSeen = true;
                        observation.BuildingId = (int)args.ReturnValue;
                        log("spawn POST session=" + sessionId + " type=" + args.Building +
                            " id=" + observation.BuildingId + " origin=" + args.TileX + "," + args.TileY);
                        CaptureNative(observation, "spawn-post", false);
                        observation.NativeChecksRemaining = 2;
                        observation.RenderChecksRemaining = 3;
                    }
                }
                catch (Exception ex) { log("spawn capture failed: " + ex); }
            }
        }

        internal void OnKeyDown(UnityInputEventArgs args)
        {
            if (args.Phase != SHCDESE.EventAPI.EventHookPhase.Pre || args.Key != KeyCode.F8)
                return;
            lock (gate)
            {
                if (!active) return;
                manualRequested = true;
                log("F8 snapshot requested session=" + sessionId +
                    "; input remains available to Vanilla");
            }
        }

        internal void OnTick(int tick)
        {
            lock (gate)
            {
                if (!active) return;
                if (!firstTickReported)
                {
                    firstTickReported = true;
                    log("post-cleanup runtime tick session=" + sessionId + " tick=" + tick);
                }
                foreach (Observation observation in observations)
                {
                    if (observation.NativeChecksRemaining <= 0) continue;
                    try { CaptureNative(observation, "tick-" + tick, false); }
                    catch (Exception ex) { log("tick capture failed: " + ex); }
                    observation.NativeChecksRemaining--;
                }
            }
        }

        internal void OnBeforeRender()
        {
            lock (gate)
            {
                if (!active || (!manualRequested && !NeedsRender())) return;
                if (!firstRenderReported)
                {
                    firstRenderReported = true;
                    log("post-cleanup render callback session=" + sessionId);
                }
                bool manual = manualRequested;
                manualRequested = false;
                foreach (Observation observation in observations)
                {
                    if (!manual && observation.RenderChecksRemaining <= 0) continue;
                    try
                    {
                        if (manual) CaptureNative(observation, "manual-F8", true);
                        CaptureManaged(observation, manual ? "manual-F8" : "render", manual);
                        if (manual)
                            saveReferences[observation.Key] = new ComparisonBaseline(
                                observation.Native, observation.Managed);
                    }
                    catch (Exception ex) { log("render capture failed: " + ex); }
                    if (observation.RenderChecksRemaining > 0)
                        observation.RenderChecksRemaining--;
                }
            }
        }

        private bool NeedsRender()
        {
            foreach (Observation observation in observations)
                if (observation.RenderChecksRemaining > 0) return true;
            return false;
        }

        private void DiscoverExistingBuildings()
        {
            var api = GameBuildingManagerAPI.Instance;
            if (api == null) return;
            var buildings = api.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_AliveState != AliveState.IsAlive || !IsTarget(building.r_BuildingType))
                    continue;
                int buildingId = spanIndex + 1;
                bool known = false;
                foreach (Observation observation in observations)
                    if (observation.BuildingId == buildingId && observation.BuildingId > 0)
                        known = true;
                if (known) continue;
                if (observations.Count >= MaxObservations)
                {
                    log("target cap reached while scanning existing buildings");
                    return;
                }
                Observation found = new Observation(building.r_BuildingType,
                    building.r_PlayerIdOwner, building.r_TilePositionXBegin,
                    building.r_TilePositionYBegin,
                    IsKeep(building.r_BuildingType) ? KeepRadius : CampRadius);
                found.BuildingId = buildingId;
                found.PostSeen = true;
                observations.Add(found);
                log("discovered existing type=" + found.Type + " id=" + buildingId +
                    " origin=" + found.X + "," + found.Y +
                    "; pre-spawn ground unavailable for this object");
            }
        }

        private Observation FindUnpaired(BuildingSpawnEventArgs args)
        {
            for (int i = observations.Count - 1; i >= 0; i--)
            {
                Observation observation = observations[i];
                if (!observation.PostSeen && observation.Type == args.Building &&
                    observation.PlayerId == args.PlayerId && observation.X == args.TileX &&
                    observation.Y == args.TileY)
                    return observation;
            }
            return null;
        }

        private void CaptureNative(Observation observation, string stage, bool forceCore)
        {
            var api = GameTileManagerAPI.Instance;
            if (api == null)
            {
                log(stage + " " + observation.Label + " tile API unavailable");
                return;
            }
            var gfx = api.GetGfxLayer();
            var alpha = api.GetAlphaGfxLayer();
            var logic = api.GetLogicLayer();
            var logic2 = api.GetLogic2Layer();
            var structure = api.GetStructureLayer();
            var random = api.GetRandomLayer();
            var height = api.GetHeightLayer();
            int side = observation.Radius * 2 + 1;
            TileSample[] current = new TileSample[side * side];
            int changed = 0, gfxChanged = 0, logicChanged = 0, valid = 0;
            for (int dy = -observation.Radius; dy <= observation.Radius; dy++)
            for (int dx = -observation.Radius; dx <= observation.Radius; dx++)
            {
                int x = observation.X + dx, y = observation.Y + dy;
                int index = (dy + observation.Radius) * side + dx + observation.Radius;
                if (!api.IsTileInsideMapBounds(x, y)) continue;
                int id = api.GetTileId(x, y);
                if (id < 0 || id >= gfx.Length || id >= alpha.Length || id >= logic.Length ||
                    id >= logic2.Length || id >= structure.Length || id >= random.Length ||
                    id >= height.Length) continue;
                TileSample sample = new TileSample(x, y, id, gfx[id], alpha[id],
                    logic[id], logic2[id], structure[id], random[id], height[id]);
                current[index] = sample;
                valid++;
                TileSample previous = observation.Native == null ? default(TileSample) : observation.Native[index];
                bool differs = observation.Native != null && !sample.SameAs(previous);
                if (differs)
                {
                    changed++;
                    if (sample.Gfx != previous.Gfx || sample.Alpha != previous.Alpha) gfxChanged++;
                    if (sample.Logic != previous.Logic || sample.Logic2 != previous.Logic2 ||
                        sample.Structure != previous.Structure) logicChanged++;
                }
                bool core = Math.Abs(dx) <= CoreRadius && Math.Abs(dy) <= CoreRadius;
                if (differs || (forceCore && core))
                    log("NATIVE stage=" + stage + " " + observation.Label + " " +
                        (differs ? previous.ToString() + " -> " : "") + sample);
            }
            observation.Native = current;
            log("NATIVE summary stage=" + stage + " " + observation.Label +
                " valid=" + valid + " changed=" + changed + " graphic=" + gfxChanged +
                " logicOrStructure=" + logicChanged +
                " baseline=" + (observation.NativeCount++ == 0 ? "first-observed" : "prior-snapshot"));
        }

        private void CaptureManaged(Observation observation, string stage, bool forceCore)
        {
            GameMap map = GameMap.instance;
            if (map == null)
            {
                log("MANAGED stage=" + stage + " " + observation.Label + " map unavailable");
                return;
            }
            int side = observation.Radius * 2 + 1;
            string[] current = new string[side * side];
            int changed = 0, visible = 0;
            for (int dy = -observation.Radius; dy <= observation.Radius; dy++)
            for (int dx = -observation.Radius; dx <= observation.Radius; dx++)
            {
                int x = observation.X + dx, y = observation.Y + dy;
                int index = (dy + observation.Radius) * side + dx + observation.Radius;
                if (x < 0 || x >= 800 || y < 0 || y >= 800) continue;
                map.mapGameTileToTilemapCoord(x, y, out int mapX, out int mapY);
                GameMapTile tile = map.getMapTile(mapX, mapY);
                string sprite = tile?.tileImage == null ? "<none>" :
                    tile.tileImage.name.Replace(' ', '_').Replace('|', '_');
                current[index] = sprite;
                if (tile?.tileImage != null) visible++;
                bool differs = observation.Managed != null &&
                    !string.Equals(sprite, observation.Managed[index], StringComparison.Ordinal);
                if (differs) changed++;
                if (differs || (forceCore || observation.Managed == null) &&
                    Math.Abs(dx) <= CoreRadius && Math.Abs(dy) <= CoreRadius)
                    log("MANAGED stage=" + stage + " " + observation.Label +
                        " tile=" + x + "," + y + " sprite=" + sprite +
                        (differs ? " prior=" + observation.Managed[index] : ""));
            }
            observation.Managed = current;
            log("MANAGED summary stage=" + stage + " " + observation.Label +
                " changed=" + changed + " visible=" + visible);
        }

        private static bool IsKeep(eStructs type) =>
            type == eStructs.STRUCT_KEEP_ONE || type == eStructs.STRUCT_KEEP_TWO ||
            type == eStructs.STRUCT_KEEP_THREE || type == eStructs.STRUCT_KEEP_FOUR ||
            type == eStructs.STRUCT_KEEP_FIVE;

        private static bool IsTarget(eStructs type) =>
            IsKeep(type) || type == eStructs.STRUCT_CAMPGROUND;

        private sealed class Observation
        {
            internal readonly eStructs Type;
            internal readonly int PlayerId, X, Y, Radius;
            internal int BuildingId;
            internal bool PostSeen;
            internal int NativeChecksRemaining, RenderChecksRemaining, NativeCount;
            internal TileSample[] Native;
            internal string[] Managed;
            internal string Label => "type=" + Type + " id=" + BuildingId +
                " player=" + PlayerId + " origin=" + X + "," + Y;
            internal string Key => Type + "/" + PlayerId + "/" + X + "/" + Y;

            internal Observation(eStructs type, int playerId, int x, int y, int radius)
            {
                Type = type; PlayerId = playerId; X = x; Y = y; Radius = radius;
            }
        }

        private sealed class ComparisonBaseline
        {
            internal readonly TileSample[] Native;
            internal readonly string[] Managed;
            internal ComparisonBaseline(TileSample[] native, string[] managed)
            {
                Native = native;
                Managed = managed;
            }
        }

        private struct TileSample
        {
            internal readonly int X, Y, Id, Gfx, Alpha, Logic;
            internal readonly byte Logic2, Height;
            internal readonly ushort Structure, Random;
            internal readonly bool Valid;

            internal TileSample(int x, int y, int id, int gfx, int alpha, int logic,
                byte logic2, ushort structure, ushort random, byte height)
            {
                X = x; Y = y; Id = id; Gfx = gfx; Alpha = alpha; Logic = logic;
                Logic2 = logic2; Structure = structure; Random = random;
                Height = height; Valid = true;
            }

            internal bool SameAs(TileSample other) => Valid == other.Valid &&
                Gfx == other.Gfx && Alpha == other.Alpha && Logic == other.Logic &&
                Logic2 == other.Logic2 && Structure == other.Structure &&
                Random == other.Random && Height == other.Height;

            public override string ToString() => Valid
                ? "tile=" + X + "," + Y + " id=" + Id + " gfx=0x" + Gfx.ToString("X8") +
                  " alpha=0x" + Alpha.ToString("X8") + " logic=0x" + Logic.ToString("X8") +
                  " logic2=0x" + Logic2.ToString("X2") + " structure=" + Structure +
                  " random=" + Random + " height=" + Height
                : "<unavailable>";
        }
    }
}
