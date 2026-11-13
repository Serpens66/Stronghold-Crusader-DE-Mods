using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.API;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;

namespace BugfixesAndQoL.AreaDemolition
{
    // All scene work is on Unity. The native simulation only supplies immutable target snapshots.
    internal sealed class SelectionRenderer
    {
        private const string PlainShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const int AnimationIdStride = 4000; // Native 19D110: layer * 4000 + 1-based building ID.
        private readonly ManualLogSource log;
        private readonly int unityThread;
        private readonly FieldInfo animationsField, wallFillinsField, fliesField;
        private readonly HashSet<long> selectedCells = new HashSet<long>();
        private readonly HashSet<long> selectedRenderCells = new HashSet<long>();
        private readonly HashSet<int> selectedBuildings = new HashSet<int>();
        private readonly HashSet<Tilemap> checkedTilemaps = new HashSet<Tilemap>();
        private readonly HashSet<SpriteRenderer> tintedSprites = new HashSet<SpriteRenderer>();
        private readonly List<TileRestore> tileRestore = new List<TileRestore>();
        private readonly List<SpriteRestore> spriteRestore = new List<SpriteRestore>();
        private readonly List<RenderRow> renderRows = new List<RenderRow>();
        private readonly Dictionary<Tilemap, RenderRow> rowLookup = new Dictionary<Tilemap, RenderRow>();
        private readonly Dictionary<int, DemolitionTarget> selectedDecorations = new Dictionary<int, DemolitionTarget>();
        private readonly List<DemolitionTarget> unpresentedDecorations = new List<DemolitionTarget>();
        private readonly ScreenSelection screenProjection = new ScreenSelection();
        private readonly Color tint = new Color(0.49f, 0.12f, 0.12f, 1f);
        private ScreenRect? area;
        private DemolitionTarget[] lastTargets;
        private DemolitionTarget[] scopeTargets;
        private GameMap currentMap;
        private Dictionary<int, BuildingAnim> animations;
        private Dictionary<int, WallFillin> wallFillins;
        private Dictionary<int, Fly> flies;
        private Camera activeCamera;
        private Material spriteMaterial;
        private int rotation = -1, mapSize = -1;
        private bool flattened, hasRenderScope, scopeComplete, ready = true;

        internal SelectionRenderer(ManualLogSource log)
        {
            this.log = log; unityThread = Thread.CurrentThread.ManagedThreadId;
            animationsField = RequireField("buildingAnims", typeof(Dictionary<int, BuildingAnim>));
            wallFillinsField = RequireField("wallFillins", typeof(Dictionary<int, WallFillin>));
            fliesField = RequireField("flies", typeof(Dictionary<int, Fly>));
            // These process-lived event roots survive SHCDE's destruction of early plugin components.
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        internal event Action<string> Failed;
        internal event Action<DemolitionTarget[]> Presented;
        internal bool IsReady => ready;

        private static FieldInfo RequireField(string name, Type expected)
        {
            FieldInfo field = typeof(GameMap).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != expected || !field.IsInitOnly)
                throw new InvalidOperationException("Vanilla render dictionary contract changed: " + name);
            return field;
        }

        internal void SetSelection(ScreenRect? selection, DemolitionTarget[] targets)
        {
            AssertUnityThread();
            Restore();
            if (!selection.HasValue) { Clear(); return; }
            if (!ready) return;
            try
            {
                GameMap map = GameMap.instance;
                if (map == null || TilemapManager.instance == null || spriteLoader.instance == null)
                    throw new InvalidOperationException("The map renderer is not ready.");
                bool differentMap = !ReferenceEquals(map, currentMap);
                int currentRotation = (int)map.CurrentRotation();
                int currentSize = map.getMapTileSize();
                if (!differentMap && rotation == currentRotation && mapSize == currentSize && ReferenceEquals(lastTargets, targets))
                { area = selection; flattened = EngineInterface.FlattenedLandscape; return; }
                EnsureMaterials();
                bool newGeometry = differentMap || rotation != currentRotation || mapSize != currentSize;
                if (newGeometry) { checkedTilemaps.Clear(); rowLookup.Clear(); }
                if (differentMap)
                {
                    checkedTilemaps.Clear(); rowLookup.Clear();
                    animations = animationsField.GetValue(map) as Dictionary<int, BuildingAnim>;
                    wallFillins = wallFillinsField.GetValue(map) as Dictionary<int, WallFillin>;
                    flies = fliesField.GetValue(map) as Dictionary<int, Fly>;
                    if (animations == null || wallFillins == null || flies == null)
                        throw new InvalidOperationException("Vanilla's scene render dictionaries are unavailable.");
                }
                bool newTargets = differentMap || !RenderSelectionGeometry.Equals(lastTargets, targets);
                currentMap = map; area = selection; rotation = currentRotation; mapSize = currentSize; flattened = EngineInterface.FlattenedLandscape;
                // Always replace the immutable receipt, including when its render geometry can be reused.
                lastTargets = targets;
                if (newTargets)
                {
                    selectedCells.Clear(); selectedBuildings.Clear(); selectedDecorations.Clear();
                    GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                    foreach (DemolitionTarget target in targets ?? Array.Empty<DemolitionTarget>())
                    {
                        if (target.Kind == TargetKind.Decoration) { selectedDecorations[target.Slot] = target; continue; }
                        if (target.Kind == TargetKind.Building || target.Kind == TargetKind.Pitch) selectedBuildings.Add(target.Slot);
                        if (target.OccupiedTiles != null && target.OccupiedTiles.Length != 0)
                        {
                            foreach (int tileId in target.OccupiedTiles)
                            {
                                if (!tiles.IsValidTileId(tileId)) continue;
                                var point = tiles.GetTileVectorFromId(tileId);
                                selectedCells.Add(CellKey(point.X, point.Y));
                            }
                        }
                        else selectedCells.Add(CellKey(target.X, target.Y));
                    }
                }
                if (newTargets || newGeometry) RebuildRenderCells();
            }
            catch (Exception error) { Fail(error); }
        }

        private static long CellKey(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        private void EnsureMaterials()
        {
            Material[] plain = spriteLoader.instance.plainMaterials;
            Material source = plain != null && plain.Length > 0 ? plain[0] : null;
            if (source == null || source.shader == null || source.shader.name != PlainShader || !source.shader.isSupported)
                throw new InvalidOperationException("The loaded Vanilla plain-sprite shader is unavailable.");
            if (spriteMaterial == null) spriteMaterial = new Material(source) { name = "AreaDemolition_SpriteTint" };
        }

        private void ValidateTilemap(Tilemap tilemap)
        {
            if (tilemap == null || checkedTilemaps.Contains(tilemap)) return;
            TilemapRenderer renderer = tilemap.GetComponent<TilemapRenderer>();
            if (renderer == null || renderer.sharedMaterial == null || renderer.sharedMaterial.shader == null || renderer.sharedMaterial.shader.name != PlainShader)
                throw new InvalidOperationException("A target tilemap no longer uses Vanilla's plain-sprite material.");
            checkedTilemaps.Add(tilemap);
        }

        private void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!ready || !area.HasValue || camera == null || camera != Camera.main) return;
            try
            {
                AssertUnityThread(); Restore();
                if (!ReferenceEquals(currentMap, GameMap.instance)) { Clear(); return; }
                if (rotation != (int)currentMap.CurrentRotation() || mapSize != currentMap.getMapTileSize() || flattened != EngineInterface.FlattenedLandscape)
                {
                    checkedTilemaps.Clear(); rotation = (int)currentMap.CurrentRotation(); mapSize = currentMap.getMapTileSize(); flattened = EngineInterface.FlattenedLandscape;
                    RebuildRenderCells();
                }
                activeCamera = camera; scopeTargets = lastTargets ?? Array.Empty<DemolitionTarget>(); hasRenderScope = true; scopeComplete = true;
                foreach (RenderRow row in renderRows)
                {
                    if (row.Tilemap == null || row.GameObject == null || !row.GameObject.activeInHierarchy) continue;
                    foreach (Vector3Int position in row.Positions)
                    {
                        TintTile(row.Tilemap, position);
                        Vector3Int side = position; side.z = 1; TintTile(row.Tilemap, side);
                    }
                }
                foreach (BuildingAnim animation in animations.Values)
                {
                    int buildingId = animation.objectID % AnimationIdStride;
                    if (selectedBuildings.Contains(buildingId) || ContainsRenderedCell(animation.mapX, animation.mapY)) TintSprite(animation.sprRenderer);
                }
                foreach (WallFillin fillin in wallFillins.Values)
                    if (ContainsRenderedCell(fillin.mapX, fillin.mapY)) TintSprite(fillin.sprRenderer);
                foreach (KeyValuePair<int, DemolitionTarget> entry in selectedDecorations)
                {
                    DemolitionTarget target = entry.Value;
                    if (!flies.TryGetValue(entry.Key, out Fly fly) || fly.gameObjectType != target.Type)
                    { unpresentedDecorations.Add(target); continue; }
                    GameMapTile tile = currentMap.getMapTile(fly.mapX, fly.mapY);
                    if (tile == null || tile.gameMapX != target.X || tile.gameMapY != target.Y)
                    { unpresentedDecorations.Add(target); continue; }
                    TintSprite(fly.sprRenderer);
                    if (fly.sprRenderer == null || !tintedSprites.Contains(fly.sprRenderer)) unpresentedDecorations.Add(target);
                }
                if (unpresentedDecorations.Count != 0)
                {
                    // Native 19D430 removes unseen flies. An offscreen linked effect therefore
                    // need not have an active renderer; visible ground anchors must catch up.
                    Rect viewport = camera.pixelRect;
                    var visible = new ScreenRect(viewport.xMin, viewport.yMin, viewport.xMax, viewport.yMax);
                    if (screenProjection.CaptureTileIds(visible, unpresentedDecorations).Length != 0) scopeComplete = false;
                }
            }
            catch (Exception error) { Fail(error); }
        }

        private bool ContainsRenderedCell(int x, int y)
            => selectedRenderCells.Contains(CellKey(x, y));

        private void RebuildRenderCells()
        {
            renderRows.Clear(); selectedRenderCells.Clear();
            foreach (RenderRow row in rowLookup.Values) row.Positions.Clear();
            // A quote is immutable and replaced at bounded intervals. Resolve coordinates only
            // when that quote or the projection changes, not for every target on every frame.
            foreach (long cell in selectedCells)
            {
                if (!TryMapCell((int)(cell >> 32), unchecked((int)cell), out GameMapTile tile, out Vector3Int location)) continue;
                Tilemap tilemap = tile.tilemapRef;
                ValidateTilemap(tilemap); selectedRenderCells.Add(CellKey(location.x, location.y));
                if (!rowLookup.TryGetValue(tilemap, out RenderRow row))
                { row = new RenderRow(tilemap); rowLookup.Add(tilemap, row); }
                if (row.Positions.Count == 0) renderRows.Add(row);
                row.Positions.Add(location);
            }
        }

        private void TintTile(Tilemap tilemap, Vector3Int position)
        {
            Sprite sprite = tilemap.GetSprite(position);
            if (sprite == null) return;
            Color original = tilemap.GetColor(position);
            tileRestore.Add(new TileRestore(tilemap, position, sprite, original));
            float shade = Mathf.Clamp01(Mathf.Max(original.r, Mathf.Max(original.g, original.b)));
            tilemap.SetColor(position, new Color(shade * tint.r, shade * tint.g, shade * tint.b, original.a));
        }

        private void TintSprite(SpriteRenderer renderer)
        {
            if (renderer == null || !tintedSprites.Add(renderer)) return;
            Sprite sprite = renderer.sprite;
            if (sprite == null || !renderer.gameObject.activeInHierarchy) { tintedSprites.Remove(renderer); return; }
            Color original = renderer.color;
            spriteRestore.Add(new SpriteRestore(renderer, sprite, renderer.sharedMaterial, original));
            // TeamColour interprets RGB as the palette, rather than tinting the complete sprite.
            // A render-only plain material colours all pixels and retains the current animation alpha.
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = new Color(tint.r, tint.g, tint.b, original.a);
        }

        private void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (!hasRenderScope || camera != activeCamera) return;
            try
            {
                DemolitionTarget[] shown = scopeTargets;
                bool complete = scopeComplete;
                Restore();
                // This exact immutable snapshot has now crossed a complete main-camera render.
                // Consumers may schedule a later commit; no mutation is performed by the renderer.
                if (complete) Presented?.Invoke(shown);
            }
            catch (Exception error) { Fail(error); }
        }

        private void Restore()
        {
            for (int i = spriteRestore.Count - 1; i >= 0; i--)
            {
                SpriteRestore item = spriteRestore[i];
                if (item.Renderer != null && item.Renderer.sprite == item.Sprite && item.Renderer.sharedMaterial == spriteMaterial)
                { item.Renderer.sharedMaterial = item.Material; item.Renderer.color = item.Color; }
            }
            for (int i = tileRestore.Count - 1; i >= 0; i--)
            {
                TileRestore item = tileRestore[i];
                if (item.Tilemap != null && item.Tilemap.GetSprite(item.Position) == item.Sprite) item.Tilemap.SetColor(item.Position, item.Color);
            }
            spriteRestore.Clear(); tileRestore.Clear(); tintedSprites.Clear(); unpresentedDecorations.Clear(); hasRenderScope = false; scopeComplete = false; activeCamera = null; scopeTargets = null;
        }

        private bool TryMapCell(int x, int y, out GameMapTile tile, out Vector3Int location)
        {
            currentMap.mapGameTileToTilemapCoord(x, y, out int tx, out int ty);
            tile = currentMap.getMapTile(tx, ty); location = new Vector3Int(tx, ty, 0);
            return tile != null && tile.tilemapRef != null;
        }

        internal void Clear()
        {
            AssertUnityThread(); Restore(); area = null; lastTargets = null; selectedCells.Clear(); selectedRenderCells.Clear(); selectedBuildings.Clear(); selectedDecorations.Clear(); renderRows.Clear(); rowLookup.Clear();
            checkedTilemaps.Clear(); currentMap = null; animations = null; wallFillins = null; flies = null; rotation = mapSize = -1;
        }

        private void Fail(Exception error)
        {
            Restore(); area = null;
            if (!ready) return;
            ready = false;
            string message = "Area demolition preview unavailable: " + error.Message;
            log.LogError(DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + " " + error);
            try { Failed?.Invoke(message); }
            catch (Exception callbackError) { log.LogError(DateTime.Now.ToString("HH:mm:ss.fff") + " Preview failure notification: " + callbackError); }
        }

        private void AssertUnityThread()
        { if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Selection rendering requires Unity's main thread."); }

        private readonly struct TileRestore
        {
            internal readonly Tilemap Tilemap; internal readonly Vector3Int Position; internal readonly Sprite Sprite; internal readonly Color Color;
            internal TileRestore(Tilemap tilemap, Vector3Int position, Sprite sprite, Color color) { Tilemap = tilemap; Position = position; Sprite = sprite; Color = color; }
        }
        private sealed class RenderRow
        {
            internal readonly Tilemap Tilemap;
            internal readonly GameObject GameObject;
            internal readonly List<Vector3Int> Positions = new List<Vector3Int>();
            internal RenderRow(Tilemap tilemap) { Tilemap = tilemap; GameObject = tilemap.gameObject; }
        }
        private readonly struct SpriteRestore
        {
            internal readonly SpriteRenderer Renderer; internal readonly Sprite Sprite; internal readonly Material Material; internal readonly Color Color;
            internal SpriteRestore(SpriteRenderer renderer, Sprite sprite, Material material, Color color) { Renderer = renderer; Sprite = sprite; Material = material; Color = color; }
        }

    }
}
