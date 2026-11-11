using System;
using System.Collections.Generic;
using System.Threading;
using SHCDESE.API;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AreaDemolitionTest
{
    // Called only while the gesture needs a new quote. Native snapshots never call Unity.
    internal sealed class ScreenSelection
    {
        // Native 1A2D90 emits signed-short row5; GameMap.processTestMap divides it by32.
        // This covers flattened mode, its transition, special surfaces and negative offsets.
        private const float MinimumHeight = short.MinValue / 32f, MaximumHeight = short.MaxValue / 32f;
        private readonly int unityThread = Thread.CurrentThread.ManagedThreadId;
        private readonly HashSet<int> visited = new HashSet<int>();
        private readonly HashSet<Tilemap> checkedTilemaps = new HashSet<Tilemap>();
        private readonly List<int> hits = new List<int>();
        private readonly int[] firstColumns = new int[GameTileManagerAPI.MAX_HEIGHT];
        private readonly int[] lastColumns = new int[GameTileManagerAPI.MAX_HEIGHT];
        private readonly Dictionary<int, CellHitShapes> hitCells = new Dictionary<int, CellHitShapes>();
        private readonly HashSet<int> emptyHitCells = new HashSet<int>();
        private readonly Dictionary<Sprite, float> spriteHeights = new Dictionary<Sprite, float>();
        private readonly List<ScreenHitHull> rootShapes = new List<ScreenHitHull>();
        private readonly HashSet<int> bodyCells = new HashSet<int>(), groundCells = new HashSet<int>();
        private GameMap map;
        private Camera camera;
        private GameTileManagerAPI tiles;
        private Vector3 origin;
        private Vector3Int originCell;
        private ScreenPoint screenOrigin, screenX, screenY, screenHeight;
        private int centerX, centerY;

        internal ScreenHitGeometry CreateHitGeometry(IEnumerable<PreviewHitCandidate> candidates)
        {
            Camera currentCamera = Camera.main;
            if (currentCamera == null) throw new InvalidOperationException("The selection camera is unavailable.");
            Rect pixels = currentCamera.pixelRect;
            var viewport = new ScreenRect(pixels.xMin, pixels.yMin, pixels.xMax, pixels.yMax);
            if (!PrepareProjection(viewport)) throw new InvalidOperationException("The selection viewport is invalid.");
            if (PerfectPixelWithZoom.instance == null) throw new InvalidOperationException("Vanilla's current zoom is unavailable.");
            float zoom = PerfectPixelWithZoom.instance.getZoom();
            if (!ScreenRect.Finite(zoom) || zoom <= 0) throw new InvalidOperationException("Vanilla's current zoom is invalid.");
            hitCells.Clear(); emptyHitCells.Clear(); spriteHeights.Clear();
            var roots = new List<DemolitionTarget>();
            var shapes = new List<ScreenHitHull[]>();
            if (candidates != null)
                foreach (PreviewHitCandidate candidate in candidates)
                {
                    if (candidate?.Root == null) continue;
                    rootShapes.Clear(); bodyCells.Clear(); groundCells.Clear();
                    foreach (int tileId in candidate.RequestTiles ?? Array.Empty<int>())
                        if (bodyCells.Add(tileId))
                        {
                            CellHitShapes cell = GetHitCell(tileId, zoom, viewport);
                            if (cell.HasBody) rootShapes.Add(cell.Body);
                        }
                    DemolitionTarget root = candidate.Root;
                    // Retain every prior occupied-ground hit as a union, independently of the
                    // request-cell hulls needed for the Vanilla hover/delete resolver.
                    if (root.OccupiedTiles != null && root.OccupiedTiles.Length != 0)
                    {
                        foreach (int tileId in root.OccupiedTiles)
                            if (groundCells.Add(tileId))
                            {
                                CellHitShapes cell = GetHitCell(tileId, zoom, viewport);
                                if (cell.HasGround) rootShapes.Add(cell.Ground);
                            }
                    }
                    else if (tiles.IsTileInsideMapBounds(root.X, root.Y))
                    {
                        CellHitShapes cell = GetHitCell(tiles.GetTileId(root.X, root.Y), zoom, viewport);
                        if (cell.HasGround) rootShapes.Add(cell.Ground);
                    }
                    if (rootShapes.Count == 0) continue;
                    roots.Add(root); shapes.Add(rootShapes.ToArray());
                }
            return new ScreenHitGeometry(roots.ToArray(), shapes.ToArray());
        }

        private CellHitShapes GetHitCell(int tileId, float zoom, ScreenRect viewport)
        {
            if (hitCells.TryGetValue(tileId, out CellHitShapes cached)) return cached;
            if (emptyHitCells.Contains(tileId)) return default;
            CellHitShapes result = BuildHitCell(tileId, zoom, viewport);
            // Most world candidates can be offscreen. Cache their ID only, rather than
            // retaining two empty hull structs for every offscreen request tile.
            if (result.HasBody || result.HasGround) hitCells[tileId] = result;
            else emptyHitCells.Add(tileId);
            return result;
        }

        private CellHitShapes BuildHitCell(int tileId, float zoom, ScreenRect viewport)
        {
            if (!tiles.IsValidTileId(tileId)) return default;
            var point = tiles.GetTileVectorFromId(tileId);
            if (!tiles.IsTileInsideMapBounds(point.X, point.Y) || point.X < firstColumns[point.Y] || point.X > lastColumns[point.Y]) return default;
            map.mapGameTileToTilemapCoord(point.X, point.Y, out int tx, out int ty);
            GameMapTile tile = map.getMapTile(tx, ty);
            if (tile == null || tile.tilemapRef == null) return default;
            ValidateGrid(tile.tilemapRef);
            float sx = screenOrigin.X + screenX.X * (point.X - centerX) + screenY.X * (point.Y - centerY);
            float sy = screenOrigin.Y + screenX.Y * (point.X - centerX) + screenY.Y * (point.Y - centerY);
            ScreenHitHull body = default, ground = default;
            bool hasBody = false, hasGround;
            ScreenHitHull plane = MakeHull(sx, sy, 0);
            // No resolver probe rises farther than800screen pixels. Avoid sprite/height
            // queries for request cells whose entire possible body hull is offscreen.
            if (plane.Bounds.MinY <= viewport.MaxY && plane.Bounds.MaxY + 800 >= viewport.MinY)
            {
                float spriteHeight = 0;
                if (tile.buildingHeight == 0)
                {
                    Sprite sprite = tile.constructionOrigImage != null ? tile.constructionOrigImage : tile.mirrorTileImage;
                    if (sprite != null && !spriteHeights.TryGetValue(sprite, out spriteHeight))
                    { spriteHeight = sprite.rect.height; spriteHeights.Add(sprite, spriteHeight); }
                }
                body = MakeHull(sx, sy, VanillaHitGeometry.MaximumRise(tile.testHeight, tile.buildingHeight, spriteHeight, zoom));
                hasBody = body.Intersects(viewport);
            }
            float height = tile.height;
            if (!ScreenRect.Finite(height) || height < MinimumHeight || height > MaximumHeight)
                throw new InvalidOperationException("A rendered map height is outside Vanilla's signed-short contract.");
            ground = MakeHull(sx + screenHeight.X * height, sy + screenHeight.Y * height, 0);
            hasGround = ground.Intersects(viewport);
            return new CellHitShapes(body, hasBody, ground, hasGround);
        }

        private ScreenHitHull MakeHull(float sx, float sy, float rise)
        {
            float ux = screenX.X * 0.5f, uy = screenX.Y * 0.5f, vx = screenY.X * 0.5f, vy = screenY.Y * 0.5f;
            return new ScreenHitHull(new ScreenPoint(sx - ux - vx, sy - uy - vy), new ScreenPoint(sx + ux - vx, sy + uy - vy),
                new ScreenPoint(sx + ux + vx, sy + uy + vy), new ScreenPoint(sx - ux + vx, sy - uy + vy), rise);
        }

        private readonly struct CellHitShapes
        {
            internal readonly ScreenHitHull Body, Ground;
            internal readonly bool HasBody, HasGround;
            internal CellHitShapes(ScreenHitHull body, bool hasBody, ScreenHitHull ground, bool hasGround)
            { Body = body; HasBody = hasBody; Ground = ground; HasGround = hasGround; }
        }

        internal int[] CaptureTileIds(ScreenRect rectangle, IEnumerable<DemolitionTarget> candidates, IEnumerable<int> groundCandidates = null)
        {
            if (!PrepareProjection(rectangle)) return Array.Empty<int>();
            if (candidates != null)
                foreach (DemolitionTarget target in candidates)
                {
                    if (target == null) continue;
                    if (target.OccupiedTiles != null && target.OccupiedTiles.Length != 0)
                        foreach (int tileId in target.OccupiedTiles) AddIfIntersecting(rectangle, tileId);
                    else if (tiles.IsTileInsideMapBounds(target.X, target.Y)) AddIfIntersecting(rectangle, tiles.GetTileId(target.X, target.Y));
                }
            if (groundCandidates != null)
                foreach (int tileId in groundCandidates) AddIfIntersecting(rectangle, tileId);
            hits.Sort(); return hits.ToArray();
        }

        // This broad phase performs no per-cell Unity work. The native raw owner/mapper
        // filter runs on these IDs before the exact projection touches ground candidates.
        internal int[] CaptureGroundTileIds(ScreenRect rectangle)
        {
            if (!PrepareProjection(rectangle)) return Array.Empty<int>();
            for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++)
            {
                for (int x = firstColumns[y]; x <= lastColumns[y]; x++)
                    if (tiles.IsTileInsideMapBounds(x, y))
                    {
                        int tileId = tiles.GetTileId(x, y);
                        if (tiles.IsValidTileId(tileId)) hits.Add(tileId);
                    }
            }
            return hits.ToArray();
        }

        private bool PrepareProjection(ScreenRect rectangle)
        {
            if (Thread.CurrentThread.ManagedThreadId != unityThread) throw new InvalidOperationException("Screen projection requires Unity's main thread.");
            visited.Clear(); hits.Clear(); checkedTilemaps.Clear();
            if (!rectangle.IsFinite) return false;
            map = GameMap.instance; camera = Camera.main; tiles = GameTileManagerAPI.Instance;
            if (map == null || camera == null || !camera.orthographic || TilemapManager.instance == null)
                throw new InvalidOperationException("Vanilla's orthographic map projection is unavailable.");
            centerX = GameTileManagerAPI.MAX_WIDTH / 2; centerY = GameTileManagerAPI.MAX_HEIGHT / 2;
            if (!TryCell(centerX, centerY, out originCell) ||
                !TryCell(centerX + 1, centerY, out Vector3Int nextXCell) ||
                !TryCell(centerX, centerY + 1, out Vector3Int nextYCell))
                throw new InvalidOperationException("Vanilla's map grid is unavailable.");
            // The point resolver uses gameTileMap.WorldToCell, so its unraised grid is
            // the authoritative base. Rendered ground rows are validated against this grid.
            Tilemap baseMap = TilemapManager.instance.gameTileMap;
            if (baseMap == null) throw new InvalidOperationException("Vanilla's resolver grid is unavailable.");
            origin = baseMap.GetCellCenterWorld(originCell);
            Vector3 nextX = baseMap.GetCellCenterWorld(nextXCell);
            Vector3 nextY = baseMap.GetCellCenterWorld(nextYCell);
            screenOrigin = Project(origin);
            screenX = Difference(Project(nextX), screenOrigin); screenY = Difference(Project(nextY), screenOrigin);
            screenHeight = Difference(Project(origin + Vector3.up), screenOrigin);
            double determinant = (double)screenX.X * screenY.Y - (double)screenX.Y * screenY.X;
            if (Math.Abs(determinant) < 1e-8) throw new InvalidOperationException("The map grid has a singular screen projection.");
            double halfWidth = (Math.Abs(screenX.X) + Math.Abs(screenY.X)) * 0.5;
            for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++)
            {
                double rowBaseX = screenOrigin.X - (double)screenX.X * centerX + (double)screenY.X * (y - centerY);
                if (!rectangle.TryGetColumnRange(rowBaseX, screenX.X, screenHeight.X, MinimumHeight, MaximumHeight,
                    halfWidth, 0, GameTileManagerAPI.MAX_WIDTH - 1, out firstColumns[y], out lastColumns[y]))
                { firstColumns[y] = 0; lastColumns[y] = -1; }
            }
            return true;
        }

        private void AddIfIntersecting(ScreenRect rectangle, int tileId)
        {
            if (!tiles.IsValidTileId(tileId) || !visited.Add(tileId)) return;
            var point = tiles.GetTileVectorFromId(tileId);
            if (!tiles.IsTileInsideMapBounds(point.X, point.Y)) return;
            // Reuse the same proven broad phase for all-map building footprints too, before
            // touching their managed render cells, rather than projecting every world candidate.
            if (point.X < firstColumns[point.Y] || point.X > lastColumns[point.Y]) return;
            map.mapGameTileToTilemapCoord(point.X, point.Y, out int tx, out int ty);
            GameMapTile tile = map.getMapTile(tx, ty);
            if (tile == null || tile.tilemapRef == null) return;
            ValidateGrid(tile.tilemapRef);
            float height = tile.height;
            if (!ScreenRect.Finite(height) || height < MinimumHeight || height > MaximumHeight)
                throw new InvalidOperationException("A rendered map height is outside Vanilla's signed-short contract.");
            float sx = screenOrigin.X + screenX.X * (point.X - centerX) + screenY.X * (point.Y - centerY) + screenHeight.X * height;
            float sy = screenOrigin.Y + screenX.Y * (point.X - centerX) + screenY.Y * (point.Y - centerY) + screenHeight.Y * height;
            float ux = screenX.X * 0.5f, uy = screenX.Y * 0.5f, vx = screenY.X * 0.5f, vy = screenY.Y * 0.5f;
            if (rectangle.IntersectsQuad(new ScreenPoint(sx - ux - vx, sy - uy - vy), new ScreenPoint(sx + ux - vx, sy + uy - vy),
                new ScreenPoint(sx + ux + vx, sy + uy + vy), new ScreenPoint(sx - ux + vx, sy - uy + vy))) hits.Add(tileId);
        }

        private static ScreenPoint Difference(ScreenPoint first, ScreenPoint second) => new ScreenPoint(first.X - second.X, first.Y - second.Y);

        private ScreenPoint Project(Vector3 world)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            if (screen.z <= 0) throw new InvalidOperationException("The map footprint is behind the selection camera.");
            return new ScreenPoint(screen.x, screen.y);
        }

        private bool TryCell(int x, int y, out Vector3Int location)
        {
            map.mapGameTileToTilemapCoord(x, y, out int tx, out int ty);
            GameMapTile tile = map.getMapTile(tx, ty); location = new Vector3Int(tx, ty, 0);
            return tile != null && tile.tilemapRef != null;
        }

        private void ValidateGrid(Tilemap tilemap)
        {
            if (!checkedTilemaps.Add(tilemap)) return;
            // Vanilla clones every row tilemap from the same grid. Validate this before reusing
            // its affine projection, including after a camera/map/rotation change.
            Vector3 actual = tilemap.GetCellCenterWorld(originCell);
            if ((actual - origin).sqrMagnitude > 1e-8f)
                throw new InvalidOperationException("A row tilemap no longer shares Vanilla's map grid.");
        }

    }
}
