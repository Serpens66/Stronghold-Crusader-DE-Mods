using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        internal sealed class ProjectionContext
        {
            internal readonly GameMap Map;
            internal readonly GameTileManagerAPI Tiles;
            internal readonly Vector3 Origin;
            internal readonly Vector3Int OriginCell;
            internal readonly ScreenPoint ScreenOrigin,ScreenX,ScreenY,ScreenHeight;
            internal readonly ScreenRect Viewport;
            internal readonly float Zoom;
            private readonly int rotation,size;
            private readonly bool flattened;
            internal ProjectionContext(ScreenSelection source,ScreenRect viewport,float zoom)
            {
                Map=source.map;Tiles=source.tiles;Origin=source.origin;OriginCell=source.originCell;
                ScreenOrigin=source.screenOrigin;ScreenX=source.screenX;ScreenY=source.screenY;ScreenHeight=source.screenHeight;
                Viewport=viewport;Zoom=zoom;rotation=(int)Map.CurrentRotation();size=Map.getMapTileSize();flattened=EngineInterface.FlattenedLandscape;
            }
            // Camera-only movement is harmless: all screen axes above were frozen on capture.
            internal bool IsMapCurrent => ReferenceEquals(Map,GameMap.instance) && Map!=null &&
                rotation==(int)Map.CurrentRotation() && size==Map.getMapTileSize() && flattened==EngineInterface.FlattenedLandscape;
        }

        internal ProjectionContext CaptureProjection()
        {
            Camera current=Camera.main;
            if(current==null)throw new InvalidOperationException("The selection camera is unavailable.");
            Rect pixels=current.pixelRect;
            var viewport=new ScreenRect(pixels.xMin,pixels.yMin,pixels.xMax,pixels.yMax);
            if(!PrepareProjection(viewport))throw new InvalidOperationException("The selection viewport is invalid.");
            if(PerfectPixelWithZoom.instance==null)throw new InvalidOperationException("Vanilla's current zoom is unavailable.");
            float zoom=PerfectPixelWithZoom.instance.getZoom();
            if(!ScreenRect.Finite(zoom) || zoom<=0)throw new InvalidOperationException("Vanilla's current zoom is invalid.");
            return new ProjectionContext(this,viewport,zoom);
        }

        private void UseProjection(ProjectionContext context,ScreenRect rectangle)
        {
            AssertUnityThread();
            if(context==null || !context.IsMapCurrent)throw new InvalidOperationException("The captured map projection is no longer current.");
            map=context.Map;tiles=context.Tiles;origin=context.Origin;originCell=context.OriginCell;
            screenOrigin=context.ScreenOrigin;screenX=context.ScreenX;screenY=context.ScreenY;screenHeight=context.ScreenHeight;
            centerX=GameTileManagerAPI.MAX_WIDTH/2;centerY=GameTileManagerAPI.MAX_HEIGHT/2;
            checkedTilemaps.Clear();visited.Clear();hits.Clear();
            SetColumnRanges(rectangle);
        }

        internal sealed class HitGeometryPreparation
        {
            internal readonly ProjectionContext Context;
            internal readonly PreviewHitCandidate[] Candidates;
            internal readonly ScreenRect? SelectArea;
            internal readonly ScreenHitGeometry Geometry;
            internal readonly List<DemolitionTarget> Selected=new List<DemolitionTarget>();
            internal readonly HashSet<string> SelectedIds=new HashSet<string>(StringComparer.Ordinal);
            internal int CandidateIndex,RequestIndex,OccupiedIndex,Phase;
            internal bool CurrentHit;
            internal DemolitionTarget[] SelectedRoots=Array.Empty<DemolitionTarget>();
            internal bool Complete;
            internal HitGeometryPreparation(PreviewHitCandidate[] candidates,ProjectionContext context,ScreenRect? selectArea)
            {Candidates=candidates??Array.Empty<PreviewHitCandidate>();Context=context;SelectArea=selectArea;Geometry=new ScreenHitGeometry(Candidates.Length);}
        }

        internal HitGeometryPreparation BeginHitGeometry(PreviewHitCandidate[] candidates,ProjectionContext context,ScreenRect? selectArea=null)
        {
            UseProjection(context,context.Viewport);
            hitCells.Clear();emptyHitCells.Clear();spriteHeights.Clear();rootShapes.Clear();bodyCells.Clear();groundCells.Clear();
            return new HitGeometryPreparation(candidates,context,selectArea);
        }

        internal bool StepHitGeometry(HitGeometryPreparation job,long deadline,out ScreenHitGeometry geometry)
        {
            AssertUnityThread();geometry=null;
            if(job==null || !job.Context.IsMapCurrent)throw new InvalidOperationException("The geometry job has no current map.");
            if(job.Complete){geometry=job.Geometry;return true;}
            while(Stopwatch.GetTimestamp()<deadline && job.CandidateIndex<job.Candidates.Length)
            {
                var candidate=job.Candidates[job.CandidateIndex];
                if(candidate?.Root==null){job.CandidateIndex++;continue;}
                if(job.Phase==0)
                {
                    rootShapes.Clear();bodyCells.Clear();groundCells.Clear();
                    job.RequestIndex=job.OccupiedIndex=0;job.CurrentHit=false;job.Phase=1;
                }
                if(job.Phase==1)
                {
                    var request=candidate.RequestTiles??Array.Empty<int>();
                    if(job.RequestIndex<request.Length)
                    {
                        int tile=request[job.RequestIndex++];
                        if(bodyCells.Add(tile))
                        {var cell=GetHitCell(tile,job.Context.Zoom,job.Context.Viewport);if(cell.HasBody)AddHull(job,cell.Body);}
                        continue;
                    }
                    job.Phase=2;
                }
                var root=candidate.Root;
                var occupied=root.OccupiedTiles;
                if(occupied!=null && occupied.Length!=0)
                {
                    if(job.OccupiedIndex<occupied.Length)
                    {
                        int tile=occupied[job.OccupiedIndex++];
                        if(groundCells.Add(tile))
                        {var cell=GetHitCell(tile,job.Context.Zoom,job.Context.Viewport);if(cell.HasGround)AddHull(job,cell.Ground);}
                        continue;
                    }
                }
                else if(job.OccupiedIndex++==0 && tiles.IsTileInsideMapBounds(root.X,root.Y))
                {
                    var cell=GetHitCell(tiles.GetTileId(root.X,root.Y),job.Context.Zoom,job.Context.Viewport);
                    if(cell.HasGround)AddHull(job,cell.Ground);
                    continue;
                }
                if(rootShapes.Count!=0)
                {
                    job.Geometry.AddRoot(root,rootShapes.ToArray());
                    if(job.CurrentHit && job.SelectedIds.Add(root.Identity))job.Selected.Add(root);
                }
                job.Phase=0;job.CandidateIndex++;
            }
            if(job.CandidateIndex<job.Candidates.Length)return false;
            job.SelectedRoots=job.Selected.ToArray();job.Complete=true;geometry=job.Geometry;return true;
        }
        private void AddHull(HitGeometryPreparation job,ScreenHitHull hull)
        {
            rootShapes.Add(hull);
            if(job.SelectArea.HasValue && !job.CurrentHit && hull.Intersects(job.SelectArea.Value))job.CurrentHit=true;
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

        // This broad phase performs no per-cell Unity work. Its row/cell cursor is
        // retained across frames; the native owner/mapper pass runs before exact geometry.
        internal sealed class GroundTilePreparation
        {
            internal readonly ProjectionContext Context;
            internal readonly List<int> Tiles=new List<int>();
            internal int Row,Column;
            internal int[] Result;
            internal GroundTilePreparation(ProjectionContext context,int first){Context=context;Column=first;}
        }
        internal GroundTilePreparation BeginGroundTileCapture(ScreenRect rectangle,ProjectionContext context)
        {UseProjection(context,rectangle);return new GroundTilePreparation(context,firstColumns[0]);}
        internal bool StepGroundTileCapture(GroundTilePreparation job,long deadline,out int[] ids)
        {
            AssertUnityThread();ids=null;
            if(job==null || !job.Context.IsMapCurrent)throw new InvalidOperationException("The ground job has no current map.");
            if(job.Result!=null){ids=job.Result;return true;}
            while(job.Row<GameTileManagerAPI.MAX_HEIGHT && Stopwatch.GetTimestamp()<deadline)
            {
                if(job.Column>lastColumns[job.Row])
                {job.Row++;if(job.Row<GameTileManagerAPI.MAX_HEIGHT)job.Column=firstColumns[job.Row];continue;}
                int x=job.Column++;
                if(tiles.IsTileInsideMapBounds(x,job.Row))
                {int tile=tiles.GetTileId(x,job.Row);if(tiles.IsValidTileId(tile))job.Tiles.Add(tile);}
            }
            if(job.Row<GameTileManagerAPI.MAX_HEIGHT)return false;
            ids=job.Result=job.Tiles.ToArray();return true;
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
            SetColumnRanges(rectangle);
            return true;
        }

        private void AssertUnityThread()
        {if(Thread.CurrentThread.ManagedThreadId!=unityThread)throw new InvalidOperationException("Screen projection requires Unity's main thread.");}
        private void SetColumnRanges(ScreenRect rectangle)
        {
            double halfWidth = (Math.Abs(screenX.X) + Math.Abs(screenY.X)) * 0.5;
            for (int y = 0; y < GameTileManagerAPI.MAX_HEIGHT; y++)
            {
                double rowBaseX = screenOrigin.X - (double)screenX.X * centerX + (double)screenY.X * (y - centerY);
                if (!rectangle.TryGetColumnRange(rowBaseX, screenX.X, screenHeight.X, MinimumHeight, MaximumHeight,
                    halfWidth, 0, GameTileManagerAPI.MAX_WIDTH - 1, out firstColumns[y], out lastColumns[y]))
                { firstColumns[y] = 0; lastColumns[y] = -1; }
            }
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
