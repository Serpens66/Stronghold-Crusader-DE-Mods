using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.API;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace AreaDemolitionTest
{
    // All scene work is on Unity. The native simulation only supplies immutable target snapshots.
    internal sealed class SelectionRenderer
    {
        private const string PlainShader = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
        private const int AnimationIdStride = 4000; // Native 19D110: layer * 4000 + 1-based building ID.
        private readonly ManualLogSource log;
        private readonly int unityThread;
        private readonly FieldInfo animationsField, wallFillinsField;
        private readonly HashSet<long> selectedCells = new HashSet<long>();
        private readonly HashSet<int> selectedBuildings = new HashSet<int>();
        private readonly HashSet<Tilemap> checkedTilemaps = new HashSet<Tilemap>();
        private readonly HashSet<SpriteRenderer> tintedSprites = new HashSet<SpriteRenderer>();
        private readonly List<TileRestore> tileRestore = new List<TileRestore>();
        private readonly List<SpriteRestore> spriteRestore = new List<SpriteRestore>();
        private readonly List<RenderCell> renderCells = new List<RenderCell>();
        private readonly Dictionary<int, GroundRow> rows = new Dictionary<int, GroundRow>();
        private readonly Color tint = new Color(0.49f, 0.12f, 0.12f, 1f);
        private AreaRect? area;
        private DemolitionTarget[] lastTargets;
        private DemolitionTarget[] scopeTargets;
        private GameMap currentMap;
        private Dictionary<int, BuildingAnim> animations;
        private Dictionary<int, WallFillin> wallFillins;
        private Camera activeCamera;
        private GameObject groundRoot;
        private Material fillMaterial, spriteMaterial;
        private int rotation = -1;
        private bool flattened, hasRenderScope, ready = true;

        internal SelectionRenderer(ManualLogSource log)
        {
            this.log = log; unityThread = Thread.CurrentThread.ManagedThreadId;
            animationsField = RequireField("buildingAnims", typeof(Dictionary<int, BuildingAnim>));
            wallFillinsField = RequireField("wallFillins", typeof(Dictionary<int, WallFillin>));
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

        internal void SetSelection(AreaRect? selection, DemolitionTarget[] targets)
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
                EnsureMaterials();
                bool differentMap = !ReferenceEquals(map, currentMap);
                bool newGeometry = differentMap || !SameArea(area, selection) || rotation != (int)map.CurrentRotation() || flattened != EngineInterface.FlattenedLandscape;
                if (differentMap)
                {
                    DestroyGround(); checkedTilemaps.Clear();
                    animations = animationsField.GetValue(map) as Dictionary<int, BuildingAnim>;
                    wallFillins = wallFillinsField.GetValue(map) as Dictionary<int, WallFillin>;
                    if (animations == null || wallFillins == null)
                        throw new InvalidOperationException("Vanilla's scene render dictionaries are unavailable.");
                }
                bool newTargets = differentMap || !ReferenceEquals(lastTargets, targets);
                currentMap = map; area = selection; rotation = (int)map.CurrentRotation(); flattened = EngineInterface.FlattenedLandscape;
                if (newTargets)
                {
                    selectedCells.Clear(); selectedBuildings.Clear(); lastTargets = targets;
                    GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                    foreach (DemolitionTarget target in targets ?? Array.Empty<DemolitionTarget>())
                    {
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
                if (newGeometry) RebuildGround();
                if (groundRoot != null) groundRoot.SetActive(true);
                if (newTargets || newGeometry) RebuildRenderCells();
            }
            catch (Exception error) { Fail(error); }
        }

        private static bool SameArea(AreaRect? first, AreaRect? second) => first.HasValue == second.HasValue &&
            (!first.HasValue || first.Value.MinX == second.Value.MinX && first.Value.MinY == second.Value.MinY && first.Value.MaxX == second.Value.MaxX && first.Value.MaxY == second.Value.MaxY);
        private static long CellKey(int x, int y) => ((long)(uint)x << 32) | (uint)y;

        private void EnsureMaterials()
        {
            Material[] plain = spriteLoader.instance.plainMaterials;
            Material source = plain != null && plain.Length > 0 ? plain[0] : null;
            if (source == null || source.shader == null || source.shader.name != PlainShader || !source.shader.isSupported)
                throw new InvalidOperationException("The loaded Vanilla plain-sprite shader is unavailable.");
            if (spriteMaterial == null) spriteMaterial = new Material(source) { name = "AreaDemolition_SpriteTint" };
            if (fillMaterial == null) fillMaterial = new Material(source) { name = "AreaDemolition_GroundFill", mainTexture = Texture2D.whiteTexture };
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
                if (rotation != (int)currentMap.CurrentRotation() || flattened != EngineInterface.FlattenedLandscape)
                {
                    checkedTilemaps.Clear(); rotation = (int)currentMap.CurrentRotation(); flattened = EngineInterface.FlattenedLandscape;
                    RebuildGround(); RebuildRenderCells();
                }
                activeCamera = camera; scopeTargets = lastTargets ?? Array.Empty<DemolitionTarget>(); hasRenderScope = true;
                foreach (RenderCell cell in renderCells)
                {
                    Tilemap tilemap = cell.Tile.tilemapRef;
                    if (tilemap == null || !tilemap.gameObject.activeInHierarchy) continue;
                    TintTile(tilemap, cell.Position);
                    Vector3Int side = cell.Position; side.z = 1; TintTile(tilemap, side);
                }
                foreach (BuildingAnim animation in animations.Values)
                {
                    int buildingId = animation.objectID % AnimationIdStride;
                    if (selectedBuildings.Contains(buildingId) || ContainsRenderedCell(animation.mapX, animation.mapY)) TintSprite(animation.sprRenderer);
                }
                foreach (WallFillin fillin in wallFillins.Values)
                    if (ContainsRenderedCell(fillin.mapX, fillin.mapY)) TintSprite(fillin.sprRenderer);
            }
            catch (Exception error) { Fail(error); }
        }

        private bool ContainsRenderedCell(int x, int y)
        {
            GameMapTile tile = currentMap.getMapTile(x, y);
            return tile != null && selectedCells.Contains(CellKey(tile.gameMapX, tile.gameMapY));
        }

        private void RebuildRenderCells()
        {
            renderCells.Clear();
            // A quote is immutable and replaced at bounded intervals. Resolve coordinates only
            // when that quote or the projection changes, not for every target on every frame.
            foreach (long cell in selectedCells)
            {
                if (!TryMapCell((int)(cell >> 32), unchecked((int)cell), out GameMapTile tile, out Vector3Int location)) continue;
                ValidateTilemap(tile.tilemapRef); renderCells.Add(new RenderCell(tile, location));
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
            if (renderer == null || renderer.sprite == null || !renderer.gameObject.activeInHierarchy || !tintedSprites.Add(renderer)) return;
            Color original = renderer.color;
            spriteRestore.Add(new SpriteRestore(renderer, renderer.sprite, renderer.sharedMaterial, original));
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
                Restore();
                // This exact immutable snapshot has now crossed a complete main-camera render.
                // Consumers may schedule a later commit; no mutation is performed by the renderer.
                Presented?.Invoke(shown);
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
            spriteRestore.Clear(); tileRestore.Clear(); tintedSprites.Clear(); hasRenderScope = false; activeCamera = null; scopeTargets = null;
        }

        private bool TryMapCell(int x, int y, out GameMapTile tile, out Vector3Int location)
        {
            currentMap.mapGameTileToTilemapCoord(x, y, out int tx, out int ty);
            tile = currentMap.getMapTile(tx, ty); location = new Vector3Int(tx, ty, 0);
            return tile != null && tile.tilemapRef != null;
        }

        private bool TryGround(int x, int y, out GameMapTile tile, out Vector3 center, out Vector3 planeCenter)
        {
            center = planeCenter = default;
            if (!TryMapCell(x, y, out tile, out Vector3Int location)) return false;
            planeCenter = tile.tilemapRef.GetCellCenterWorld(location); center = planeCenter;
            center.z = currentMap.getSpritePosVector(location.x, location.y).z;
            if (flattened) center.y += tile.height;
            else
            {
                GameTileManagerAPI api = GameTileManagerAPI.Instance;
                if (!api.IsTileInsideMapBounds(x, y)) return false;
                int id = api.GetTileId(x, y);
                if (!api.IsValidTileId(id)) return false;
                center.y += api.GetTileDefaultHeight(id) / 32f;
            }
            return true;
        }

        private void RebuildGround()
        {
            if (!area.HasValue || currentMap == null) return;
            if (groundRoot == null) groundRoot = new GameObject("AreaDemolition_Selection");
            foreach (GroundRow row in rows.Values) row.Clear();
            AreaRect bounds = area.Value;
            for (int y = bounds.MinY; y <= bounds.MaxY; y++)
            {
                for (int x = bounds.MinX; x <= bounds.MaxX; x++)
                {
                    if (!TryGround(x, y, out GameMapTile tile, out Vector3 center, out Vector3 plane)) continue;
                    Vector3 u, v;
                    if (TryGround(x + 1, y, out _, out _, out Vector3 nextX)) u = nextX - plane;
                    else if (TryGround(x - 1, y, out _, out _, out Vector3 previousX)) u = plane - previousX;
                    else continue;
                    if (TryGround(x, y + 1, out _, out _, out Vector3 nextY)) v = nextY - plane;
                    else if (TryGround(x, y - 1, out _, out _, out Vector3 previousY)) v = plane - previousY;
                    else continue;
                    u.z = v.z = 0;
                    if (!rows.TryGetValue(tile.row, out GroundRow row))
                    {
                        row = new GroundRow(tile.row, groundRoot.transform, fillMaterial); rows.Add(tile.row, row);
                    }
                    Vector3 p0 = center - u * 0.5f - v * 0.5f, p1 = center + u * 0.5f - v * 0.5f;
                    Vector3 p2 = center + u * 0.5f + v * 0.5f, p3 = center - u * 0.5f + v * 0.5f;
                    row.Quad(p0, p1, p2, p3, new Color32(132, 123, 109, 28));
                    if (x == bounds.MinX) row.Edge(p0, p3);
                    if (x == bounds.MaxX) row.Edge(p1, p2);
                    if (y == bounds.MinY) row.Edge(p0, p1);
                    if (y == bounds.MaxY) row.Edge(p3, p2);
                }
            }
            foreach (GroundRow row in rows.Values) row.Upload();
        }

        internal void Clear()
        {
            AssertUnityThread(); Restore(); area = null; lastTargets = null; selectedCells.Clear(); selectedBuildings.Clear(); renderCells.Clear();
            checkedTilemaps.Clear(); currentMap = null; animations = null; wallFillins = null; rotation = -1; DestroyGround();
        }

        private void DestroyGround()
        {
            foreach (GroundRow row in rows.Values) row.Destroy();
            rows.Clear();
            if (groundRoot != null) Object.Destroy(groundRoot);
            groundRoot = null;
        }

        private void Fail(Exception error)
        {
            Restore(); area = null;
            if (groundRoot != null) groundRoot.SetActive(false);
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
        private readonly struct RenderCell
        {
            internal readonly GameMapTile Tile; internal readonly Vector3Int Position;
            internal RenderCell(GameMapTile tile, Vector3Int position) { Tile = tile; Position = position; }
        }
        private readonly struct SpriteRestore
        {
            internal readonly SpriteRenderer Renderer; internal readonly Sprite Sprite; internal readonly Material Material; internal readonly Color Color;
            internal SpriteRestore(SpriteRenderer renderer, Sprite sprite, Material material, Color color) { Renderer = renderer; Sprite = sprite; Material = material; Color = color; }
        }

        private sealed class GroundRow
        {
            private readonly GameObject gameObject;
            private readonly Mesh mesh;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();
            internal GroundRow(int row, Transform parent, Material material)
            {
                gameObject = new GameObject("AreaDemolition_Row_" + row); gameObject.transform.SetParent(parent, false);
                mesh = new Mesh { name = gameObject.name, indexFormat = IndexFormat.UInt32 }; mesh.MarkDynamic();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.sortingOrder = -20000 + row * 49 + 1;
            }
            internal void Clear() { vertices.Clear(); colors.Clear(); uv.Clear(); triangles.Clear(); }
            internal void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color32 color)
            {
                int start = vertices.Count; vertices.Add(p0); vertices.Add(p1); vertices.Add(p2); vertices.Add(p3);
                for (int i = 0; i < 4; i++) { colors.Add(color); uv.Add(new Vector2(0.5f, 0.5f)); }
                if (Vector3.Cross(p1 - p0, p2 - p0).z < 0)
                { triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2); triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3); }
                else
                { triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1); triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2); }
            }
            internal void Edge(Vector3 first, Vector3 last)
            {
                Vector3 delta = last - first;
                Vector3 normal = new Vector3(-delta.y, delta.x, 0).normalized * 0.009f;
                Quad(first - normal, last - normal, last + normal, first + normal, new Color32(153, 141, 121, 130));
            }
            internal void Upload()
            {
                gameObject.SetActive(vertices.Count != 0);
                mesh.Clear(); if (vertices.Count == 0) return;
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            }
            internal void Destroy() { Object.Destroy(mesh); Object.Destroy(gameObject); }
        }
    }
}
