using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Threading;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace CastlePlanner
{
    internal static unsafe partial class CastleVisualDiagnostics
    {
        // Published hooks live to process exit. Map transitions only retire their observation session.
        private static readonly List<Hook> presentationHooks = new List<Hook>();
        private static FieldInfo animationField;
        private static MaterialPropertyBlock materialProperties;
        [ThreadStatic] private static long activePacket;
        private delegate void ProcessMap(GameMap self, short[] source, int count, EngineInterface.PlayState state, byte[] radar);
        private delegate void ProcessMapHook(ProcessMap original, GameMap self, short[] source, int count, EngineInterface.PlayState state, byte[] radar);
        private delegate bool MouseTile(GameMap self, int x, int y, bool value);
        private delegate bool MouseTileHook(MouseTile original, GameMap self, int x, int y, bool value);
        private delegate void EndRefresh(TilemapManager self, bool noGameTick);
        private delegate void EndRefreshHook(EndRefresh original, TilemapManager self, bool noGameTick);
        private delegate void TileDataCall(gameTile self, Vector3Int location, ITilemap map, ref TileData data);
        private delegate void TileDataHook(TileDataCall original, gameTile self, Vector3Int location, ITilemap map, ref TileData data);

        private static void InstallPresentationHooks()
        {
            // Validate all reflection boundaries before publishing any hook. Other mods' IL hooks
            // (notably APIShared.Audio on processTestMap) remain in the MonoMod chain.
            MethodInfo process = Required(typeof(GameMap), "processTestMap", typeof(void), typeof(short[]), typeof(int), typeof(EngineInterface.PlayState), typeof(byte[]));
            MethodInfo mouse = Required(typeof(GameMap), "setMouseTile", typeof(bool), typeof(int), typeof(int), typeof(bool));
            MethodInfo refresh = Required(typeof(TilemapManager), "endTileRefresh", typeof(void), typeof(bool));
            MethodInfo tile = Required(typeof(gameTile), "GetTileData", typeof(void), typeof(Vector3Int), typeof(ITilemap), typeof(TileData).MakeByRefType());
            animationField = typeof(GameMap).GetField("buildingAnims", BindingFlags.Instance | BindingFlags.NonPublic);
            if (animationField == null || animationField.FieldType != typeof(Dictionary<int, BuildingAnim>))
                throw new InvalidOperationException("GameMap buildingAnims read contract changed.");
            Publish(process, (ProcessMapHook)ObserveProcessMap);
            Publish(mouse, (MouseTileHook)ObserveMouseTile);
            Publish(refresh, (EndRefreshHook)ObserveEndRefresh);
            Publish(tile, (TileDataHook)ObserveTileData);
            Log("presentation-hooks-ready count=" + presentationHooks.Count + ", nativeHooksAdded=0, originalCalls=exactly-once");
        }
        private static MethodInfo Required(Type type, string name, Type result, params Type[] args)
        {
            MethodInfo method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, args, null);
            if (method == null || method.ReturnType != result) throw new InvalidOperationException(type.Name + "." + name + " contract changed.");
            return method;
        }
        private static void Publish(MethodInfo method, Delegate callback)
        {
            var hook = new Hook(method, callback, new HookConfig { ManualApply = true });
            presentationHooks.Add(hook); // Root before publication, including a partially failed install.
            hook.Apply();
            if (!hook.IsApplied) throw new InvalidOperationException("Diagnostic hook publication incomplete: " + method.Name);
            Log("presentation-hook-ready method=" + method.DeclaringType.Name + "." + method.Name);
        }

        // Save NativeLoaded is the existing loader-owned synchronous context, before the simulation
        // resumes. No native manager is dereferenced by a Unity frame or managed render hook.
        internal static void LoadedSave(APIShared.MissionContext context, Func<int[]> humanPlayers)
        {
            Start(context);
            Guard("save-native-loaded", () =>
            {
                lock (Gate)
                {
                    Session session = current;
                    if (session == null) return;
                    session.SaveRoster = context.Mode.IsRealMultiplayer ? humanPlayers() : Array.Empty<int>();
                    session.SavePlayers = VisualDiagnosticSavePlayers.Select(context.Mode.IsRealMultiplayer, context.LocalPlayerId, session.SaveRoster);
                    Log("save-player-selection session=" + session.Id + ", local=" + Clean(context.LocalPlayerId) + ", realMultiplayer=" + context.Mode.IsRealMultiplayer +
                        ", selected=" + string.Join(",", session.SavePlayers) + ", source=mission-context-and-multiplayer-only-roster");
                    DiscoverSaveTargets(session, "save-native-loaded");
                }
            });
        }
        private static void DiscoverSaveTargets(Session session, string phase)
        {
            var humans = new HashSet<int>(session.SavePlayers);
            int candidates = 0;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding b = ref buildings[index];
                if (b.r_BuildingType != eStructs.STRUCT_GRANARY) continue;
                candidates++;
                if (!humans.Contains(b.r_PlayerIdOwner) ||
                    (b.r_AliveState != AliveState.IsAlive && b.r_AliveState != AliveState.NeedsInit)) continue;
                var region = FocusRegion(buildings, index);
                if (!region.Valid) continue;
                var watch = new Watch(b.r_PlayerIdOwner, region, "unknown (loaded save); granaryId=" + (index + 1) +
                    ", globalId=" + b.r_GlobalId, null, Now);
                watch.FirstTick = !session.AwaitingSaveStart;
                session.Watches[-(index + 1)] = watch;
                NativeSnapshot(session, watch, phase, region);
            }
            Log((session.Watches.Count == 0 ? "save-targets-unavailable" : "save-targets-ready") + " session=" + session.Id +
                ", phase=" + phase + ", granaryWatches=" + session.Watches.Count + ", nativeGranaryRecords=" + candidates +
                ", selectedPlayers=" + string.Join(",", session.SavePlayers) + ", waitingForSuccessfulStart=" + session.AwaitingSaveStart);
        }
        private static VisualDiagnosticRegion FocusRegion(Span<GameBuilding> buildings, int index)
        {
            ref GameBuilding b = ref buildings[index];
            int x = b.r_TilePositionXBegin, y = b.r_TilePositionYBegin;
            // This limits observation area, never declares an unexpected native value invalid.
            int size = b.r_OccupyTileGridSize <= 100 ? Math.Max(1, (int)b.r_OccupyTileGridSize) : 1;
            if (b.r_OccupyTileGridSize > 100) Log("focus-truncated id=" + (index + 1) + ",rawSize=" + b.r_OccupyTileGridSize);
            var seed = new VisualDiagnosticRegion(x - 4, y - 4, x + size + 3, y + size + 3);
            int minX = seed.MinX, minY = seed.MinY, maxX = seed.MaxX, maxY = seed.MaxY;
            for (int neighbor = 0; neighbor < buildings.Length; neighbor++)
            {
                ref GameBuilding n = ref buildings[neighbor];
                int nSize = n.r_OccupyTileGridSize <= 100 ? (int)n.r_OccupyTileGridSize : 0;
                if (n.r_GlobalId == 0 || !seed.Intersects(n.r_TilePositionXBegin, n.r_TilePositionYBegin, nSize)) continue;
                if (nSize <= 0) { Log("focus-neighbor-truncated id=" + (neighbor + 1) + ",rawSize=" + n.r_OccupyTileGridSize); continue; }
                minX = Math.Min(minX, n.r_TilePositionXBegin - 4); minY = Math.Min(minY, n.r_TilePositionYBegin - 4);
                maxX = Math.Max(maxX, n.r_TilePositionXBegin + nSize + 3); maxY = Math.Max(maxY, n.r_TilePositionYBegin + nSize + 3);
            }
            var region = new VisualDiagnosticRegion(minX, minY, maxX, maxY);
            if (!region.Valid) { Log("focus-truncated id=" + (index + 1) + ",rawRegion=" + region); return seed; }
            return region;
        }
        internal static void MissionStarted(APIShared.MissionContext context)
        {
            if (context == null || !context.IsSave) return;
            Guard("save-started", () =>
            {
                lock (Gate)
                {
                    if (current == null || !current.Lifecycle.ConfirmSave(context.SessionId)) return;
                    int[] selected = VisualDiagnosticSavePlayers.Select(context.Mode.IsRealMultiplayer, context.LocalPlayerId, current.SaveRoster);
                    if (!new HashSet<int>(current.SavePlayers).SetEquals(selected))
                    {
                        Log("save-player-selection-updated session=" + current.Id + ", selected=" + string.Join(",", selected));
                        current.Watches.Clear(); current.MappingRevision++;
                    }
                    current.SavePlayers = selected;
                    // A late local identity or initially unavailable building array is retried only
                    // on the existing native tick, never by reading native managers from a UI hook.
                    current.RetrySaveDiscovery = current.Watches.Count == 0;
                    foreach (Watch watch in current.Watches.Values) watch.Window = new VisualDiagnosticWindow(Now);
                    Log("save-observation-started session=" + current.Id + ", durationSeconds=185, targets=" + current.Watches.Count +
                        ", local=" + Clean(context.LocalPlayerId) + ", retryOnFirstNativeTick=" + current.RetrySaveDiscovery);
                }
            });
        }

        private sealed class TrackedTile
        {
            internal readonly Watch Watch;
            internal readonly int X, Y, TileId, BuildingId, Owner;
            internal readonly uint GlobalId;
            internal readonly string Type;
            internal readonly long NativeSequence;
            internal readonly double NativeTime;
            internal TrackedTile(Watch watch, int x, int y, int tileId, int buildingId, uint global, int owner, string type)
            { Watch = watch; X = x; Y = y; TileId = tileId; BuildingId = buildingId; GlobalId = global; Owner = owner; Type = type;
                NativeSequence = watch.NativeSequence; NativeTime = watch.NativeTime; }
            internal string Identity => FormattableString.Invariant($"native=({X},{Y}),tile={TileId},building={BuildingId},global={GlobalId},owner={Owner},type={Type},nativeSequence={NativeSequence},nativeTime={NativeTime:R}");
        }
        private static bool CanObserve(Session session) => session != null && !session.AwaitingSaveStart &&
            Thread.CurrentThread.ManagedThreadId == unityThread && GameMap.instance != null;
        private static void EnsureUiMapping(Session session)
        {
            int rotation = (int)GameMap.instance.CurrentRotation();
            if (rotation == session.MappedRotation && session.MappingRevision == session.MappedRevision) return;
            session.UiTiles.Clear();
            session.NativeTiles.Clear();
            foreach (Watch watch in session.Watches.Values)
            {
                if (watch.Window.Expired(Now)) continue;
                foreach (TrackedTile tracked in watch.Tiles.Values)
                {
                    bool focused = false;
                    foreach (VisualDiagnosticRegion focus in watch.Focus) if (focus.Contains(tracked.X, tracked.Y)) { focused = true; break; }
                    if (!focused) continue;
                    GameMap.instance.mapGameTileToTilemapCoord(tracked.X, tracked.Y, out int x, out int y);
                    session.UiTiles[VisualDiagnosticPackets.Key(x, y)] = tracked;
                    session.NativeTiles[VisualDiagnosticPackets.Key(tracked.X, tracked.Y)] = tracked;
                }
            }
            session.MappedRotation = rotation; session.MappedRevision = session.MappingRevision;
        }
        private static void Trace(Session session, string kind, string key, string payload, bool ordered = false)
        {
            session.TraceBuffer.Add(FormattableString.Invariant($"session={session.Id},t={Now:R},frame={Time.frameCount},packet={activePacket},lastPacket={session.PacketSequence}"), kind, key, payload, ordered);
        }
        private static void ConfirmCallback(Session session, int index, string method)
        {
            if (++session.Callbacks[index] == 1) Log("presentation-callback-confirmed session=" + session.Id + ", method=" + method + ", frame=" + Time.frameCount);
        }
        private static void FlushTrace(Session session, bool force)
        {
            if (!force && Now < session.NextFlush) return;
            session.NextFlush = Now + 0.5;
            long lost = session.TraceBuffer.Dropped;
            if (lost != session.ReportedDrops)
            { Log("trace-ring-dropped session=" + session.Id + ", count=" + lost); session.ReportedDrops = lost; }
            if (session.TraceBuffer.Bytes == 0) return;
            string payload = "# CASTLE_VISUAL schema=2; ordered read-only pipeline trace; " + session.Description +
                "\r\n# seq\tcontext\tstage\tkey\tvalues; nativeAge is explicit, not a same-generation assertion\r\n" + session.TraceBuffer.Drain();
            session.Writer.Enqueue("pipeline", payload);
        }
        private static void ObserveProcessMap(ProcessMap original, GameMap self, short[] source, int count, EngineInterface.PlayState state, byte[] radar)
        {
            Session session = null;
            List<Tuple<int, TrackedTile, short[]>> rows = null;
            long previousPacket = activePacket;
            try
            {
                VisualDiagnosticCall.Around(() =>
                {
                    lock (Gate)
                    {
                        session = current;
                        if (!CanObserve(session)) { session = null; return; }
                        ConfirmCallback(session, 0, "processTestMap");
                        EnsureUiMapping(session); activePacket = ++session.PacketSequence;
                        Trace(session, "play-state", "current", PresentationState(state));
                        // The normal building hover is carried in PlayState; setMouseTile is a
                        // separate Vanilla highlighting path and is not called by normal hover.
                        TrackedTile presentationHover = null;
                        if (state.in_structure > 0)
                            foreach (TrackedTile candidate in session.NativeTiles.Values)
                                if (candidate.BuildingId == state.in_structure) { presentationHover = candidate; break; }
                        session.Hover = presentationHover;
                        int length = VisualDiagnosticPackets.Count(source, count, out bool bounded);
                        rows = new List<Tuple<int, TrackedTile, short[]>>();
                        if (bounded) Trace(session, "packet-bounds", activePacket.ToString(), "requested=" + count + ",bounded=" + bounded + ",records=" + length, true);
                        for (int ordinal = 0; ordinal < length; ordinal++)
                        {
                            int offset = ordinal * VisualDiagnosticPackets.Width;
                            // Coordinates in non-tile records remain raw observations with their record kind.
                            if (!session.NativeTiles.TryGetValue(VisualDiagnosticPackets.Key(source[offset + 1], source[offset + 2]), out TrackedTile tracked)) continue;
                            short[] raw = VisualDiagnosticPackets.Copy(source, ordinal);
                            rows.Add(Tuple.Create(ordinal, tracked, raw));
                            Trace(session, "packet-row", tracked.TileId.ToString(), "ordinal=" + ordinal + "," + tracked.Identity + ",raw21=" + string.Join(",", raw), true);
                        }
                    }
                }, () => original(self, source, count, state, radar), () =>
                {
                    lock (Gate)
                    {
                        if (session == null || current != session || rows == null) return;
                        foreach (var row in rows)
                        {
                            self.mapGameTileToTilemapCoord(row.Item2.X, row.Item2.Y, out int x, out int y);
                            session.RefreshPending.Add(VisualDiagnosticPackets.Key(x, y));
                            Trace(session, "packet-result", row.Item2.TileId.ToString(), "phase=after-whole-packet,ordinal=" + row.Item1 + "," + row.Item2.Identity + "," + TileState(self.getMapTile(x, y)), true);
                        }
                        if (rows.Count != 0) Trace(session, "packet-end", activePacket.ToString(), "trackedRecords=" + rows.Count, true);
                    }
                }, error => Guard("packet-hook", () => { throw error; }));
            }
            finally { activePacket = previousPacket; }
        }
        // Values are copied by name only after the actual struct is inspected; raw scalar fields
        // preserve future/unknown presentation states without holding the game state's references.
        private static readonly FieldInfo[] stateFields = typeof(EngineInterface.PlayState).GetFields(BindingFlags.Public | BindingFlags.Instance);
        private static string PresentationState(EngineInterface.PlayState state)
        {
            var text = new StringBuilder(); object boxed = state;
            foreach (FieldInfo field in stateFields)
                if ((field.FieldType.IsPrimitive || field.FieldType.IsEnum) &&
                    (field.Name.StartsWith("camera_target_", StringComparison.Ordinal) || field.Name.StartsWith("in_", StringComparison.Ordinal) ||
                    field.Name == "game_paused" || field.Name == "mouse_selector_state" || field.Name == "rotateHappened" || field.Name == "flattenedHappened"))
                    text.Append(field.Name).Append('=').Append(Clean(field.GetValue(boxed))).Append(';');
            return text.ToString();
        }
        private static bool ObserveMouseTile(MouseTile original, GameMap self, int x, int y, bool value)
        {
            bool result = original(self, x, y, value);
            Guard("mouse-tile-hook", () =>
            {
                lock (Gate)
                {
                    Session session = current; if (!CanObserve(session)) return;
                    ConfirmCallback(session, 1, "setMouseTile");
                    EnsureUiMapping(session);
                    if (!session.UiTiles.TryGetValue(VisualDiagnosticPackets.Key(x, y), out TrackedTile tracked)) return;
                    if (result && value) session.Hover = tracked;
                    else if (session.Hover != null && session.Hover.X == tracked.X && session.Hover.Y == tracked.Y) session.Hover = null;
                    Trace(session, "mouse-tile", tracked.TileId.ToString(), tracked.Identity + $",map=({x},{y}),set={value},result={result}," + TileState(self.getMapTile(x, y)), true);
                    session.RefreshPending.Add(VisualDiagnosticPackets.Key(x, y));
                }
            });
            return result;
        }
        private static string TileState(GameMapTile tile)
        {
            if (tile == null) return "ui-unavailable";
            return $"row={tile.row},column={tile.column},gameMap=({tile.gameMapX},{tile.gameMapY}),height={Clean(tile.height)},buildingHeight={Clean(tile.buildingHeight)},testHeight={Clean(tile.testHeight)}," +
                $"hover={tile.mouseOver},sprite={SpriteId(tile.tileImage)},mirror={SpriteId(tile.mirrorTileImage)},construction={SpriteId(tile.constructionOrigImage)},debug={SpriteId(tile.debugTileImage)}," +
                $"chevron={SpriteId(tile.chevronImage)},light={tile.light},floatColour={tile.floatColour},chevronLight={tile.chevronLight},chevheightdiff={Clean(tile.chevheightdiff)},chevronChanged={tile.chevronChanged}," +
                $"refresh0={tile.isRefreshBitSet(0)},refresh1={tile.isRefreshBitSet(1)},tilemap={(tile.tilemapRef != null ? tile.tilemapRef.GetInstanceID() : 0)}";
        }
        private static string Colour(Color value) => value.r.ToString("R", CultureInfo.InvariantCulture) + "," + value.g.ToString("R", CultureInfo.InvariantCulture) + "," +
            value.b.ToString("R", CultureInfo.InvariantCulture) + "," + value.a.ToString("R", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => value.x.ToString("R", CultureInfo.InvariantCulture) + "," + value.y.ToString("R", CultureInfo.InvariantCulture) + "," + value.z.ToString("R", CultureInfo.InvariantCulture);
        private static string DataState(TileData data) => $"sprite={SpriteId(data.sprite)},colour={Colour(data.color)},transform={Matrix(data.transform)},flags={data.flags},collider={data.colliderType}";
        private static string Matrix(Matrix4x4 value)
        {
            var text = new StringBuilder();
            for (int i = 0; i < 16; i++) { if (i != 0) text.Append(','); text.Append(value[i].ToString("R", CultureInfo.InvariantCulture)); }
            return text.ToString();
        }
        private static void ObserveTileData(TileDataCall original, gameTile self, Vector3Int location, ITilemap map, ref TileData data)
        {
            // Most refreshed cells are outside the focused area. Avoid closures, formatting and
            // allocations for those cells. The map index is UI-owned; native code only publishes
            // copied coordinates under Gate. A late map-end simply retires this read-only session.
            Session quickSession = current;
            bool relevant = false;
            try
            {
                if (CanObserve(quickSession))
                {
                    lock (Gate)
                    {
                        EnsureUiMapping(quickSession);
                        relevant = quickSession.UiTiles.ContainsKey(VisualDiagnosticPackets.Key(location.x, location.y));
                    }
                }
            }
            catch (Exception error) { Guard("tile-data-filter", () => { throw error; }); }
            if (!relevant) { original(self, location, map, ref data); return; }
            ObserveTrackedTileData(original, self, location, map, ref data);
        }
        private static void ObserveTrackedTileData(TileDataCall original, gameTile self, Vector3Int location, ITilemap map, ref TileData data)
        {
            Session session = null; TrackedTile tracked = null;
            TileData before = data;
            Guard("tile-data-before", () =>
            {
                lock (Gate)
                {
                    session = current; if (!CanObserve(session)) { session = null; return; }
                    ConfirmCallback(session, 2, "GetTileData");
                    EnsureUiMapping(session);
                    session.UiTiles.TryGetValue(VisualDiagnosticPackets.Key(location.x, location.y), out tracked);
                    if (tracked != null) Trace(session, "tile-data-before", tracked.TileId + ":" + location.z,
                        tracked.Identity + "," + DataState(before) + "," + TileState(GameMap.instance.getMapTile(location.x, location.y)));
                }
            });
            original(self, location, map, ref data);
            TileData after = data;
            Guard("tile-data-after", () =>
            {
                lock (Gate)
                {
                    if (session == null || current != session || tracked == null) return;
                    session.RefreshPending.Add(VisualDiagnosticPackets.Key(location.x, location.y));
                    Tilemap callerMap = map.GetComponent<Tilemap>();
                    Trace(session, "tile-data-return", tracked.TileId + ":" + location.z,
                        tracked.Identity + ",callerTilemap=" + (callerMap != null ? callerMap.GetInstanceID() : 0) + "," + DataState(after) + "," + TileState(GameMap.instance.getMapTile(location.x, location.y)));
                    if (callerMap != null)
                        session.ReturnedSprites[callerMap.GetInstanceID() + ":" + location.x + ":" + location.y + ":" + location.z] =
                            new ReturnedSprite(SpriteId(after.sprite), Time.frameCount, activePacket);
                    SpriteMetadata(session, after.sprite);
                }
            });
        }
        private static void ObserveEndRefresh(EndRefresh original, TilemapManager self, bool noGameTick)
        {
            original(self, noGameTick);
            Guard("refresh-result", () =>
            {
                lock (Gate)
                {
                    Session session = current; if (!CanObserve(session)) return;
                    ConfirmCallback(session, 3, "endTileRefresh");
                    EnsureUiMapping(session);
                    // Only cells touched by real Vanilla work, not a full castle scan per frame.
                    var pending = new List<long>(session.RefreshPending); session.RefreshPending.Clear();
                    foreach (long key in pending)
                    {
                        if (!session.UiTiles.TryGetValue(key, out TrackedTile tracked)) continue;
                        int x = (int)(key >> 32), y = (int)key;
                        GameMapTile tile = GameMap.instance.getMapTile(x, y);
                        if (tile == null || tile.tilemapRef == null) continue;
                        Tilemap actual = tile.row >= 0 && self.rowTileMaps != null && tile.row < self.rowTileMaps.Length ? self.rowTileMaps[tile.row] : null;
                        CaptureActualTile(session, tracked, tile.tilemapRef, x, y, "assigned", noGameTick);
                        if (actual != null && actual != tile.tilemapRef) CaptureActualTile(session, tracked, actual, x, y, "row", noGameTick);
                    }
                    foreach (Watch watch in session.Watches.Values) CaptureAnimations(session, watch);
                }
            });
        }
        private static void CaptureActualTile(Session session, TrackedTile tracked, Tilemap tilemap, int x, int y, string role, bool noTick)
        {
            RendererMetadata(session, tilemap);
            for (int layer = 0; layer <= 1; layer++)
            {
                var cell = new Vector3Int(x, y, layer);
                Sprite sprite = tilemap.GetSprite(cell);
                Trace(session, "tilemap-result", tracked.TileId + ":" + layer + ":" + role,
                    $"native=({tracked.X},{tracked.Y}),building={tracked.BuildingId},global={tracked.GlobalId},owner={tracked.Owner},type={tracked.Type},map=({x},{y}),tilemap={tilemap.GetInstanceID()}," +
                    $"sprite={SpriteId(sprite)},colour={Colour(tilemap.GetColor(cell))},transform={Matrix(tilemap.GetTransformMatrix(cell))},flags={tilemap.GetTileFlags(cell)},noGameTick={noTick}");
                if (session.ReturnedSprites.TryGetValue(tilemap.GetInstanceID() + ":" + x + ":" + y + ":" + layer, out ReturnedSprite returned) && returned.Sprite != SpriteId(sprite))
                    Trace(session, "pipeline-observation", tracked.TileId + ":" + layer + ":" + role,
                        $"stage=tilemap-result,returnedSprite={returned.Sprite},actualSprite={SpriteId(sprite)},returnFrame={returned.Frame},returnPacket={returned.Packet}; pending-refresh-or-divergence, not an error verdict");
                SpriteMetadata(session, sprite);
            }
        }
        private static void SpriteMetadata(Session session, Sprite sprite)
        {
            if (sprite == null || !session.SpriteMetadataSeen.Add(SpriteId(sprite))) return;
            var text = new StringBuilder(); AppendSprite(text, new HashSet<int>(), sprite);
            Trace(session, "sprite", SpriteId(sprite).ToString(), text.ToString());
        }
        private static void RendererMetadata(Session session, Tilemap map)
        {
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            Material material = renderer != null ? renderer.sharedMaterial : null;
            Trace(session, "renderer", map.GetInstanceID().ToString(), $"active={map.gameObject.activeInHierarchy},enabled={renderer?.enabled},sortingLayer={renderer?.sortingLayerID},order={renderer?.sortingOrder},mode={renderer?.mode}," +
                $"material={(material != null ? material.GetInstanceID() : 0)},shader={Clean(material != null && material.shader != null ? material.shader.name : null)},renderQueue={material?.renderQueue},position={Vector(map.transform.position)},scale={Vector(map.transform.lossyScale)}");
            if (renderer != null) MaterialMetadata(session, renderer, material);
        }
        private static void MaterialMetadata(Session session, Renderer renderer, Material material)
        {
            if (materialProperties == null) materialProperties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(materialProperties); // Copies into diagnostic storage; never SetPropertyBlock.
            Texture texture = materialProperties.GetTexture("_MainTex");
            Texture materialTexture = material != null && material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Trace(session, "material-properties", renderer.GetInstanceID().ToString(),
                $"blockEmpty={materialProperties.isEmpty},blockHasColour={materialProperties.HasProperty("_Color")},blockColour={Colour(materialProperties.GetColor("_Color"))}," +
                $"blockTexture={(texture != null ? texture.GetInstanceID() : 0)},materialColour={(material != null && material.HasProperty("_Color") ? Colour(material.GetColor("_Color")) : "unavailable")}," +
                $"materialTexture={(materialTexture != null ? materialTexture.GetInstanceID() : 0)}");
        }
        private readonly struct ReturnedSprite
        {
            internal readonly int Sprite, Frame;
            internal readonly long Packet;
            internal ReturnedSprite(int sprite, int frame, long packet) { Sprite = sprite; Frame = frame; Packet = packet; }
        }
        private static void CaptureAnimations(Session session, Watch watch)
        {
            if (animationField == null || GameMap.instance == null) return;
            var animations = animationField.GetValue(GameMap.instance) as Dictionary<int, BuildingAnim>;
            if (animations == null) return;
            foreach (var pair in animations)
            {
                BuildingAnim a = pair.Value;
                if (a == null || !session.UiTiles.TryGetValue(VisualDiagnosticPackets.Key(a.mapX, a.mapY), out TrackedTile tracked) || tracked.Watch != watch) continue;
                SpriteRenderer renderer = a.sprRenderer;
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Trace(session, "building-animation", pair.Key.ToString(), $"object={a.objectID},native=({tracked.X},{tracked.Y}),building={tracked.BuildingId},global={tracked.GlobalId},owner={tracked.Owner},type={tracked.Type}," +
                    $"file={a.type},image={a.state},colour={a.colour},transparency={a.transparency},layerDelay={a.layerDelay},row={a.row},column={a.column}," +
                    $"sprite={(renderer != null ? SpriteId(renderer.sprite) : 0)},rendererColour={Colour(renderer != null ? renderer.color : default(Color))},sortingLayer={renderer?.sortingLayerID},order={renderer?.sortingOrder},enabled={renderer?.enabled}," +
                    $"active={(a.gameObject != null && a.gameObject.activeInHierarchy)},instance={(a.gameObject != null ? a.gameObject.GetInstanceID() : 0)},position={Vector(a.gameObject != null ? a.gameObject.transform.position : default(Vector3))},material={(material != null ? material.GetInstanceID() : 0)}");
                if (renderer != null) { SpriteMetadata(session, renderer.sprite); MaterialMetadata(session, renderer, material); }
            }
        }
    }
}
