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
        private GameMap map;
        private Camera camera;
        private GameTileManagerAPI tiles;
        private Vector3 origin;
        private Vector3Int originCell;
        private ScreenPoint screenOrigin, screenX, screenY, screenHeight;
        private int centerX, centerY;

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
            if (!TryPlane(centerX, centerY, out originCell, out origin) ||
                !TryPlane(centerX + 1, centerY, out _, out Vector3 nextX) ||
                !TryPlane(centerX, centerY + 1, out _, out Vector3 nextY))
                throw new InvalidOperationException("Vanilla's map grid is unavailable.");
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

        private bool TryPlane(int x, int y, out Vector3Int location, out Vector3 center)
        {
            map.mapGameTileToTilemapCoord(x, y, out int tx, out int ty);
            GameMapTile tile = map.getMapTile(tx, ty); location = new Vector3Int(tx, ty, 0); center = default;
            if (tile == null || tile.tilemapRef == null) return false;
            center = tile.tilemapRef.GetCellCenterWorld(location); return true;
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
