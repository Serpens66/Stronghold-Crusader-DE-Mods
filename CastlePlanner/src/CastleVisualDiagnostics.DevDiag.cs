using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using AIVParser.Core;
using BepInEx;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using UnityEngine;
using UnityEngine.Tilemaps;
using AivRotation = AIVParser.Core.AivRotation;

namespace CastlePlanner
{
    // Observation only: no native calls that refresh, create, repair or change graphics.
    internal static unsafe partial class CastleVisualDiagnostics
    {
        private static readonly object Gate = new object();
        private static Session current;
        private static int unityThread, lastFrame = -1, lastTick = -1;
        private static bool initialized, wrongThreadReported;
        private static readonly Dictionary<string, int> failures = new Dictionary<string, int>();
        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        internal static void Initialize(ManualLogSource log)
        {
            Guard("install", () =>
            {
                if (initialized) return;
                initialized = true; unityThread = Thread.CurrentThread.ManagedThreadId;
                Shared.DevDiag.Attach(log);
                Application.quitting += OnQuitting;
                InstallPresentationHooks();
                Log("installed version=" + CastlePlannerPlugin.PluginVersion +
                    ", nativeSha256=" + Shared.DebugLogHelper.InstalledNativeSha256() +
                    ", uiThread=" + unityThread + ", readOnly=true");
            });
        }
        private static void OnQuitting() { End("application-quitting"); }
        private static void Log(string text) { Shared.DevDiag.Log(() => "CASTLE_VISUAL " + text); }
        private static string Clean(object value) => Convert.ToString(value, CultureInfo.InvariantCulture)?
            .Replace("\t", " ").Replace("\r", " ").Replace("\n", " ") ?? "<unknown>";

        internal static void Start(APIShared.MissionContext context)
        {
            Guard("start", () =>
            {
                lock (Gate)
                {
                    EndLocked("replacement");
                    if (context == null) { Log("start-unavailable reason=missing-context"); return; }
                    string directory = Path.Combine(Paths.BepInExRootPath, "logs", "CastlePlannerDiag",
                        DateTime.Now.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) +
                        "-p" + Process.GetCurrentProcess().Id + "-s" + context.SessionId);
                    current = new Session(context.SessionId, context.MapName, context.FilePath,
                        context.Mode.ToDiagnosticString(), directory, context.IsSave);
                    lastFrame = -1; lastTick = -1;
                    failures.Clear();
                    Log("armed " + current.Description + ", directory=" + directory);
                }
            });
        }
        internal static void End(string reason) { Guard("end", () => { lock (Gate) EndLocked(reason); }); }
        private static void EndLocked(string reason)
        {
            Session session = current; current = null;
            if (session == null) return;
            session.Lifecycle.End();
            Log($"pipeline-summary session={session.Id}, processMap={session.Callbacks[0]}, setMouseTile={session.Callbacks[1]}, getTileData={session.Callbacks[2]}, endRefresh={session.Callbacks[3]}, ringDropped={session.TraceBuffer.Dropped}; zero setMouseTile calls can be normal");
            Log("session-ended " + session.Description + ", reason=" + reason);
            FlushTrace(session, true);
            session.Writer.Complete();
        }

        internal static void BeginCastle(int player, string castle, int keepX, int keepY,
            AivRotation rotation, int preparedX, int preparedY)
        {
            Guard("before-native-castle", () =>
            {
                lock (Gate)
                {
                    Session session = current;
                    if (session == null) return;
                    int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
                    foreach (int row in new[] { 0, AivGridPoint.GridSize - 1 })
                        foreach (int column in new[] { 0, AivGridPoint.GridSize - 1 })
                        {
                            AivWorldTile tile = AivWorldTransform.ProjectNativeFit(new AivGridPoint(row, column), keepX, keepY, rotation);
                            minX = Math.Min(minX, tile.X); minY = Math.Min(minY, tile.Y);
                            maxX = Math.Max(maxX, tile.X); maxY = Math.Max(maxY, tile.Y);
                        }
                    var region = new VisualDiagnosticRegion(minX - 2, minY - 2, maxX + 2, maxY + 2);
                    if (!region.Valid) { Log("region-unavailable player=" + player + ", rawRegion=" + region); return; }
                    var watch = new Watch(player, region, castle, rotation, Now);
                    session.Watches[player] = watch;
                    Log("castle-begin " + session.Description + ", " + watch.Description +
                        $", keepReference=({keepX},{keepY}), preparedKeep=({preparedX},{preparedY})");
                    NativeSnapshot(session, watch, "before-native-castle", region);
                }
            });
        }
        internal static void Snapshot(int player, string phase)
        {
            Guard(phase, () =>
            {
                lock (Gate)
                    if (current != null && current.Watches.TryGetValue(player, out Watch watch))
                        NativeSnapshot(current, watch, phase, watch.Region);
            });
        }
        internal static void Spawn(BuildingSpawnEventArgs args)
        {
            Guard("spawn", () =>
            {
                lock (Gate)
                {
                    Session session = current;
                    if (session == null || args == null) return;
                    foreach (Watch watch in session.Watches.Values)
                    {
                        if (!watch.Region.Contains(args.TileX, args.TileY) || watch.Window.Expired(Now)) continue;
                        var text = Header(session, watch, "spawn-post");
                        text.AppendLine("spawn\treturnedId\towner\ttype\tx\ty");
                        text.AppendLine($"spawn\t{args.ReturnValue}\t{args.PlayerId}\t{args.Building}\t{args.TileX}\t{args.TileY}");
                        // Validate the range before the native lookup; never infer a status as an ID.
                        Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                        if (args.ReturnValue > 0 && args.ReturnValue <= buildings.Length)
                            AppendBuilding(text, watch, (int)args.ReturnValue, ref buildings[(int)args.ReturnValue - 1]);
                        else text.AppendLine("unavailable\tspawn-event-id-outside-building-span");
                        if (watch.Spawns.Length + text.Length < 2 * 1024 * 1024)
                            watch.Spawns.Append(text);
                        else if (++watch.DroppedSpawns == 1)
                            Log("spawn-records-truncated session=" + session.Id + ", player=" + watch.Player);
                    }
                }
            });
        }
        internal static void Tick(int tick)
        {
            Guard("tick", () =>
            {
                lock (Gate)
                {
                    lastTick = tick;
                    if (current == null || current.AwaitingSaveStart) return;
                    if (current.RetrySaveDiscovery)
                    {
                        current.RetrySaveDiscovery = false;
                        DiscoverSaveTargets(current, "save-first-native-tick");
                        if (current.Watches.Count == 0) { EndLocked("save-targets-unavailable"); return; }
                    }
                    foreach (Watch watch in current.Watches.Values)
                    {
                        if (watch.Window.Expired(Now)) continue;
                        if (!watch.FirstTick)
                        { watch.FirstTick = true; NativeSnapshot(current, watch, "first-native-tick", watch.Region); }
                        else if (watch.PendingNativePhase != null && Now - watch.NativeTime >= 0.5)
                        {
                            NativeSnapshot(current, watch, watch.PendingNativePhase, watch.PendingNativeRegion);
                            watch.PendingNativePhase = null;
                        }
                    }
                }
            });
        }

        internal static void VisualFrame(bool blueprintVisible)
        {
            Guard("ui-frame", () =>
            {
                if (Thread.CurrentThread.ManagedThreadId != unityThread)
                {
                    if (!wrongThreadReported) { wrongThreadReported = true; Log("ui-unavailable reason=wrong-thread"); }
                    return;
                }
                lock (Gate)
                {
                    Session session = current;
                    if (session == null || session.AwaitingSaveStart || session.Watches.Count == 0 || lastFrame == Time.frameCount || GameMap.instance == null) return;
                    lastFrame = Time.frameCount;
                    EnsureUiMapping(session);
                    int rotation = (int)GameMap.instance.CurrentRotation();
                    int x = session.Hover?.X ?? int.MinValue, y = session.Hover?.Y ?? int.MinValue;
                    int hovered = session.Hover?.BuildingId ?? 0;
                    Trace(session, "presentation", "view", $"rotation={rotation},blueprintVisible={blueprintVisible}");
                    bool anyActive = false;
                    foreach (Watch watch in session.Watches.Values)
                    {
                        double now = Now;
                        if (watch.Window.Expired(now)) continue;
                        anyActive = true;
                        // Cursor movement elsewhere must not consume this castle's capture budget.
                        int trackedX = watch.Region.Contains(x, y) ? x : int.MinValue;
                        int trackedY = trackedX == int.MinValue ? int.MinValue : y;
                        string phase = watch.Window.Take(now, rotation, blueprintVisible, trackedX, trackedY,
                            trackedX == int.MinValue ? 0 : hovered);
                        if (phase == null) continue;
                        VisualDiagnosticRegion region = phase.StartsWith("hover-", StringComparison.Ordinal) && trackedX != int.MinValue
                            ? new VisualDiagnosticRegion(x - 4, y - 4, x + 4, y + 4) : watch.Region;
                        // Request the simulation-owned copy; never race native managers from a UI callback.
                        watch.PendingNativePhase = phase; watch.PendingNativeRegion = region;
                        ManagedSnapshot(session, watch, phase, region, rotation, blueprintVisible, x, y, hovered);
                        CaptureAnimations(session, watch);
                    }
                    FlushTrace(session, false);
                    if (!anyActive && session.Watches.Count != 0) EndLocked("observation-window-complete");
                }
            });
        }

        private static StringBuilder Header(Session session, Watch watch, string phase)
        {
            var text = new StringBuilder(4096);
            text.AppendLine("# CASTLE_VISUAL schema=2; raw observations, not a verdict; native/UI captures may belong to different update phases");
            text.AppendLine("# " + session.Description + ", " + watch.Description);
            text.AppendLine($"# phase={phase}, utc={DateTime.UtcNow:o}, tick={lastTick}, thread={Thread.CurrentThread.ManagedThreadId}");
            text.AppendLine(FormattableString.Invariant($"# monotonic={Now:R}, nativeSequence={watch.NativeSequence}, nativeAgeSeconds={Now - watch.NativeTime:R}"));
            return text;
        }
        private static void NativeSnapshot(Session session, Watch watch, string phase, VisualDiagnosticRegion region)
        {
            watch.NativeTime = Now; watch.NativeSequence++; session.MappingRevision++;
            var text = Header(session, watch, phase);
            if (watch.Spawns.Length != 0)
            {
                text.AppendLine("# synchronous spawn observations since previous snapshot");
                text.Append(watch.Spawns); watch.Spawns.Clear();
            }
            if (watch.DroppedSpawns != 0) text.AppendLine("# droppedSpawnRecords=" + watch.DroppedSpawns);
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            watch.Focus.Clear();
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding b = ref buildings[index];
                if (b.r_BuildingType == eStructs.STRUCT_GRANARY && b.r_GlobalId != 0 &&
                    watch.Region.Intersects(b.r_TilePositionXBegin, b.r_TilePositionYBegin, (int)b.r_OccupyTileGridSize))
                    watch.Focus.Add(FocusRegion(buildings, index));
            }
            text.AppendLine("# building rows: id globalId owner type state x y size height material variation spriteColour productionAnimation rawRecordHex");
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding building = ref buildings[index];
                if (building.r_GlobalId == 0 && building.r_PlayerIdOwner == 0 && building.r_BuildingType == eStructs.STRUCT_NULL) continue;
                // Unknown sizes/states remain diagnostic evidence; do not suppress them as invalid buildings.
                int size = building.r_OccupyTileGridSize <= int.MaxValue ? (int)building.r_OccupyTileGridSize : int.MaxValue;
                if (region.Intersects(building.r_TilePositionXBegin, building.r_TilePositionYBegin, size))
                    AppendBuilding(text, watch, index + 1, ref building);
            }
            GameTileManagerAPI api = GameTileManagerAPI.Instance;
            GameTileManagerView tiles = api.TileManager;
            int capacity = TileCapacity(tiles);
            int valid = 0, unavailable = 0;
            text.AppendLine("# tile rows: x y id building height defaultHeight logic logic2 gfx alpha construction pillar wall showHi misc changed structureWas damage luminescence floating");
            for (int y = region.MinY; y <= region.MaxY; y++)
                for (int x = region.MinX; x <= region.MaxX; x++)
                {
                    if (!TryTile(api, x, y, capacity, out int id))
                    { unavailable++; text.AppendLine($"tile-unavailable\t{x}\t{y}"); continue; }
                    valid++;
                    int owner = 0; uint global = 0; string type = "unknown";
                    int buildingId = tiles.StructureGrid[id];
                    if (buildingId > 0 && buildingId <= buildings.Length)
                    {
                        ref GameBuilding occupied = ref buildings[buildingId - 1];
                        owner = occupied.r_PlayerIdOwner; global = occupied.r_GlobalId; type = occupied.r_BuildingType.ToString();
                    }
                    watch.Tiles[VisualDiagnosticPackets.Key(x, y)] = new TrackedTile(watch, x, y, id, buildingId, global, owner, type);
                    text.Append("tile\t").Append(x).Append('\t').Append(y).Append('\t').Append(id).Append('\t')
                        .Append(tiles.StructureGrid[id]).Append('\t').Append(tiles.HeightGrid[id]).Append('\t')
                        .Append(tiles.DefaultHeightGrid[id]).Append('\t').Append(tiles.LogicGrid[id]).Append('\t')
                        .Append(tiles.Logic2Grid[id]).Append('\t').Append(tiles.GFXGrid[id]).Append('\t')
                        .Append(tiles.AlphaGFXGrid[id]).Append('\t').Append(tiles.ConstructionGrid[id]).Append('\t')
                        .Append(tiles.PillarGFXGrid[id]).Append('\t').Append(tiles.WallGFXGrid[id]).Append('\t')
                        .Append(tiles.ShowHiGrid[id]).Append('\t').Append(tiles.MiscDisplayGrid[id]).Append('\t')
                        .Append(tiles.ChangedGrid[id]).Append('\t').Append(tiles.StructureWasGrid[id]).Append('\t')
                        .Append(tiles.DamageGrid[id]).Append('\t').Append(tiles.LuminesenceGrid[id]).Append('\t')
                        .Append(tiles.FloatingGrid[id]).AppendLine();
                }
            text.AppendLine($"# validTiles={valid}, unavailableTiles={unavailable}, region={region}");
            bool accepted = session.Writer.Enqueue(phase + "-native", text.ToString());
            Log($"snapshot session={session.Id}, player={watch.Player}, phase={phase}, kind=native, validTiles={valid}, unavailableTiles={unavailable}, queued={accepted}");
        }
        private static void AppendBuilding(StringBuilder text, Watch watch, int id, ref GameBuilding building)
        {
            if (watch.Identities.Observe(id, building.r_GlobalId, out uint prior))
                text.AppendLine($"identity-change\t{id}\t{prior}\t{building.r_GlobalId}");
            text.Append("building\t").Append(id).Append('\t').Append(building.r_GlobalId).Append('\t')
                .Append(building.r_PlayerIdOwner).Append('\t').Append(building.r_BuildingType).Append('\t')
                .Append(building.r_AliveState).Append('\t').Append(building.r_TilePositionXBegin).Append('\t')
                .Append(building.r_TilePositionYBegin).Append('\t').Append(building.r_OccupyTileGridSize).Append('\t')
                .Append(building.r_HeightElevation).Append('\t').Append(building.r_GameMaterialIndex).Append('\t')
                .Append(building.r_SpriteVariationIndex).Append('\t').Append(building.r_SpritePlayerColorId).Append('\t')
                .Append(building.r_ProductionAnimationIndex).Append('\t');
            fixed (GameBuilding* pointer = &building)
            {
                // Size comes from the actual referenced interop type; no guessed tail fields are read.
                byte* bytes = (byte*)pointer;
                for (int offset = 0; offset < sizeof(GameBuilding); offset++) text.Append(bytes[offset].ToString("X2", CultureInfo.InvariantCulture));
            }
            text.AppendLine();
        }
        private static int TileCapacity(GameTileManagerView tiles)
        {
            int capacity = tiles.StructureGrid.Length;
            foreach (int length in new[] { tiles.HeightGrid.Length, tiles.DefaultHeightGrid.Length, tiles.LogicGrid.Length,
                tiles.Logic2Grid.Length, tiles.GFXGrid.Length, tiles.AlphaGFXGrid.Length, tiles.ConstructionGrid.Length,
                tiles.PillarGFXGrid.Length, tiles.WallGFXGrid.Length, tiles.ShowHiGrid.Length, tiles.MiscDisplayGrid.Length,
                tiles.ChangedGrid.Length, tiles.StructureWasGrid.Length, tiles.DamageGrid.Length,
                tiles.LuminesenceGrid.Length, tiles.FloatingGrid.Length }) capacity = Math.Min(capacity, length);
            return capacity;
        }
        private static bool TryTile(GameTileManagerAPI api, int x, int y, int capacity, out int id)
        {
            id = -1;
            if (!api.IsTileInsideMapBounds(x, y)) return false;
            id = api.GetTileId(x, y);
            return VisualDiagnosticRegion.IsReadableTile(id, capacity) && api.IsValidTileId(id);
        }

        private static void ManagedSnapshot(Session session, Watch watch, string phase, VisualDiagnosticRegion region,
            int rotation, bool visible, int mouseX, int mouseY, int hovered)
        {
            var text = Header(session, watch, phase);
            text.AppendLine($"# uiFrame={Time.frameCount}, cameraRotation={rotation}, blueprintVisible={visible}, mouse=({mouseX},{mouseY}), hoveredBuildingId={hovered}");
            var sprites = new HashSet<int>();
            var renderers = new HashSet<int>();
            text.AppendLine("# ui-tile rows: nativeX nativeY tileId mapX mapY row column height buildingHeight testHeight mouseOver image mirror construction chevron tilemap");
            for (int y = region.MinY; y <= region.MaxY; y++)
                for (int x = region.MinX; x <= region.MaxX; x++)
                {
                    if (!watch.Tiles.TryGetValue(VisualDiagnosticPackets.Key(x, y), out TrackedTile tracked)) continue;
                    int id = tracked.TileId;
                    GameMap.instance.mapGameTileToTilemapCoord(x, y, out int mapX, out int mapY);
                    GameMapTile tile = GameMap.instance.getMapTile(mapX, mapY);
                    if (tile == null) { text.AppendLine($"ui-unavailable\t{x}\t{y}\t{id}\t{mapX}\t{mapY}"); continue; }
                    text.Append("ui-tile\t").Append(x).Append('\t').Append(y).Append('\t').Append(id).Append('\t')
                        .Append(mapX).Append('\t').Append(mapY).Append('\t').Append(tile.row).Append('\t').Append(tile.column).Append('\t')
                        .Append(Clean(tile.height)).Append('\t').Append(Clean(tile.buildingHeight)).Append('\t').Append(Clean(tile.testHeight)).Append('\t')
                        .Append(tile.mouseOver).Append('\t').Append(SpriteId(tile.tileImage)).Append('\t').Append(SpriteId(tile.mirrorTileImage)).Append('\t')
                        .Append(SpriteId(tile.constructionOrigImage)).Append('\t').Append(SpriteId(tile.chevronImage)).Append('\t')
                        .Append(tile.tilemapRef != null ? tile.tilemapRef.GetInstanceID() : 0).AppendLine();
                    AppendSprite(text, sprites, tile.tileImage); AppendSprite(text, sprites, tile.mirrorTileImage);
                    AppendSprite(text, sprites, tile.constructionOrigImage); AppendSprite(text, sprites, tile.chevronImage);
                    if (tile.tilemapRef == null || !renderers.Add(tile.tilemapRef.GetInstanceID())) continue;
                    TilemapRenderer renderer = tile.tilemapRef.GetComponent<TilemapRenderer>();
                    Material material = renderer != null ? renderer.sharedMaterial : null;
                    text.AppendLine($"renderer\t{tile.tilemapRef.GetInstanceID()}\tenabled={renderer?.enabled}\tactive={tile.tilemapRef.gameObject.activeInHierarchy}" +
                        $"\tsortingLayer={renderer?.sortingLayerID}\tsortingOrder={renderer?.sortingOrder}\tmode={renderer?.mode}" +
                        $"\tmaterialId={(material != null ? material.GetInstanceID() : 0)}\tmaterial={Clean(material != null ? material.name : null)}" +
                        $"\tshader={Clean(material != null && material.shader != null ? material.shader.name : null)}\trenderQueue={material?.renderQueue}" +
                        $"\tposition={Clean(tile.tilemapRef.transform.position)}\tscale={Clean(tile.tilemapRef.transform.lossyScale)}");
                }
            session.Writer.Enqueue(phase + "-ui", text.ToString());
            Log($"ui-snapshot session={session.Id}, player={watch.Player}, phase={phase}, frame={Time.frameCount}, cameraRotation={rotation}, blueprintVisible={visible}, hoveredBuildingId={hovered}");
        }
        private static int SpriteId(Sprite sprite) => sprite != null ? sprite.GetInstanceID() : 0;
        private static void AppendSprite(StringBuilder text, HashSet<int> seen, Sprite sprite)
        {
            if (sprite == null || !seen.Add(sprite.GetInstanceID())) return;
            Texture2D texture = sprite.texture;
            text.AppendLine($"sprite\t{sprite.GetInstanceID()}\t{Clean(sprite.name)}\ttextureId={(texture != null ? texture.GetInstanceID() : 0)}" +
                $"\ttexture={Clean(texture != null ? texture.name : null)}\trect={Clean(sprite.rect)}\tpivot={Clean(sprite.pivot)}" +
                $"\tpixelsPerUnit={Clean(sprite.pixelsPerUnit)}");
        }
        private static void Guard(string phase, Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                lock (Gate)
                {
                    failures.TryGetValue(phase, out int count); failures[phase] = ++count;
                    if (count == 1 || (count & (count - 1)) == 0)
                        Log("capture-unavailable phase=" + phase + ", count=" + count + ", error=" + Clean(ex));
                }
            }
        }
        private sealed class Session
        {
            internal readonly long Id;
            internal readonly string Description;
            internal readonly VisualDiagnosticWriter Writer;
            internal readonly Dictionary<int, Watch> Watches = new Dictionary<int, Watch>();
            internal readonly VisualDiagnosticTraceBuffer TraceBuffer = new VisualDiagnosticTraceBuffer();
            internal readonly Dictionary<long, TrackedTile> UiTiles = new Dictionary<long, TrackedTile>();
            internal readonly Dictionary<long, TrackedTile> NativeTiles = new Dictionary<long, TrackedTile>();
            internal readonly HashSet<long> RefreshPending = new HashSet<long>();
            internal readonly HashSet<int> SpriteMetadataSeen = new HashSet<int>();
            internal readonly long[] Callbacks = new long[4];
            internal readonly Dictionary<string, ReturnedSprite> ReturnedSprites = new Dictionary<string, ReturnedSprite>();
            internal int MappingRevision, MappedRevision = -1, MappedRotation = int.MinValue;
            internal long PacketSequence;
            internal double NextFlush;
            internal long ReportedDrops;
            internal readonly VisualDiagnosticSessionState Lifecycle;
            internal int[] SaveRoster = Array.Empty<int>(), SavePlayers = Array.Empty<int>();
            internal bool RetrySaveDiscovery;
            internal bool AwaitingSaveStart => !Lifecycle.Active;
            internal TrackedTile Hover;
            internal Session(long id, string map, string path, string mode, string directory, bool isSave)
            {
                Id = id; Description = $"session={id}, map={Clean(map)}, file={Clean(path)}, mode={Clean(mode)}";
                Lifecycle = new VisualDiagnosticSessionState(id, isSave);
                Writer = new VisualDiagnosticWriter(directory, message => Log("session=" + id + ", " + message));
            }
        }
        private sealed class Watch
        {
            internal readonly int Player;
            internal readonly VisualDiagnosticRegion Region;
            internal readonly string Description;
            internal VisualDiagnosticWindow Window;
            internal readonly Dictionary<long, TrackedTile> Tiles = new Dictionary<long, TrackedTile>();
            internal readonly List<VisualDiagnosticRegion> Focus = new List<VisualDiagnosticRegion>();
            internal long NativeSequence;
            internal double NativeTime;
            internal string PendingNativePhase;
            internal VisualDiagnosticRegion PendingNativeRegion;
            internal readonly VisualDiagnosticIdentities Identities = new VisualDiagnosticIdentities();
            internal readonly StringBuilder Spawns = new StringBuilder();
            internal int DroppedSpawns;
            internal bool FirstTick;
            internal Watch(int player, VisualDiagnosticRegion region, string castle, AivRotation? rotation, double now)
            {
                Player = player; Region = region; Window = new VisualDiagnosticWindow(now);
                Description = $"player={player}, castle={Clean(castle)}, castleRotation={(rotation.HasValue ? rotation.ToString() : "unknown")}, region={region}";
            }
        }
    }
}
