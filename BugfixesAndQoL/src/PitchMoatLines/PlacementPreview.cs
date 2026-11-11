using System.Collections.Generic;
using SHCDESE.Interop;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BugfixesAndQoL.PitchMoatLines
{
    // The live map owns this view and its children. Placement does not depend on them.
    public sealed class PlacementPreview : MonoBehaviour
    {
        private const float GhostAlpha = 0.5f;
        private const int AssetRetryFrames = 60;
        private static PlacementPreview view;
        private static readonly Color Valid = new Color(0.65f, 0.85f, 0.68f, 0.42f);
        private static readonly Color Invalid = new Color(0.9f, 0.37f, 0.34f, 0.68f);
        private static readonly Color Unaffordable = new Color(0.92f, 0.63f, 0.36f, 0.68f);
        private static readonly Color Existing = new Color(0.75f, 0.75f, 0.75f, 0.6f);
        private static readonly Color Capacity = new Color(0.88f, 0.78f, 0.39f, 0.68f);

        private readonly Ghost[] ghosts = new Ghost[LinePolicy.MaxTiles];
        private readonly Outline[] outlines = new Outline[LinePolicy.MaxTiles];
        private readonly Dictionary<Tilemap, TilemapRenderer> terrainRenderers = new Dictionary<Tilemap, TilemapRenderer>();
        private PreviewSnapshot frameSnapshot;
        private GameMap currentMap;
        private spriteLoader loader;
        private Sprite pitchSprite;
        private int nextPitchLookup;
        private int visibleGhosts, outlineCount;

        internal static void EnsureOn(EditorDirector director)
        {
            if (view != null)
            {
                if (view.gameObject == director.gameObject)
                    return;
                view.enabled = false;
            }
            view = director.GetComponent<PlacementPreview>();
            if (view == null)
                view = director.gameObject.AddComponent<PlacementPreview>();
            view.enabled = true;
        }

        private void LateUpdate()
        {
            var snapshot = PlacementRuntime.GetPreview();
            if (snapshot == null || !Application.isFocused)
            {
                HideFrame();
                return;
            }
            var map = GameMap.instance;
            var controls = MainControls.instance;
            if (map == null || controls == null)
            {
                HideFrame();
                return;
            }
            if (currentMap != map)
            {
                currentMap = map;
                terrainRenderers.Clear();
            }

            DrawFrame(snapshot, map, controls);
        }

        private void DrawFrame(PreviewSnapshot snapshot, GameMap map, MainControls controls)
        {
            frameSnapshot = snapshot;
            outlineCount = 0;
            int shown = 0;
            bool pitch = snapshot.Mapper == (int)eMappers.MAPPER_PITCH_DITCH;
            Sprite stockPitch = pitch && snapshot.Available ? GetPitchSprite() : null;
            Color tint = pitch ? Color.white : TilemapManager.tileColBlue;
            tint.a = GhostAlpha;
            for (int i = 0; i < snapshot.Tiles.Length && i < LinePolicy.MaxTiles; i++)
            {
                var tile = snapshot.Tiles[i];
                map.mapGameTileToTilemapCoord(tile.X, tile.Y, out int x, out int y);
                var visible = map.getMapTile(x, y);
                if (visible == null || visible.gameMapX != tile.X || visible.gameMapY != tile.Y)
                    continue;
                var tilemap = visible.tilemapRef;
                var heightOffset = new Vector3(0f, visible.height, 0f);
                Vector3 center = tilemap == null
                    ? controls.getCellCentre(x, y) + heightOffset
                    : tilemap.GetCellCenterWorld(new Vector3Int(x, y, 0)) + tilemap.transform.TransformVector(heightOffset);

                var state = snapshot.Available ? snapshot.States[i] : LineTileState.Blocked;
                var color = TileColor(state);
                if (state == LineTileState.Buildable)
                {
                    // Vanilla marks a planned moat by tinting the existing terrain sprite.
                    Sprite sprite = pitch ? stockPitch : visible.tileImage;
                    if (sprite != null && tilemap != null && ShowGhost(shown, sprite, tint, center, tilemap))
                        shown++;
                    else
                        color.a = 0.7f;
                }
                outlines[outlineCount++] = new Outline(center, color);
            }
            HideExtraGhosts(shown);
        }

        private Sprite GetPitchSprite()
        {
            var current = spriteLoader.instance;
            if (loader != current)
            {
                loader = current;
                pitchSprite = null;
                nextPitchLookup = 0;
            }
            if (current == null)
                return null;

            int frame = Time.frameCount;
            if (pitchSprite == null && frame >= nextPitchLookup)
            {
                nextPitchLookup = frame + AssetRetryFrames;
                // GetSprite can detect an unloaded asset without a Vanilla "missing sprite" log.
                var stock = current.GetSprite("pitch_ditches 001");
                if (stock != null)
                {
                    var atlasSprite = current.GetGMSprite(Enums.GM.GM_PITCH_DITCHES, 1);
                    pitchSprite = atlasSprite != null ? atlasSprite : stock;
                }
            }
            return pitchSprite;
        }

        private TilemapRenderer TerrainRenderer(Tilemap tilemap)
        {
            if (!terrainRenderers.TryGetValue(tilemap, out var terrain))
            {
                terrain = tilemap.GetComponent<TilemapRenderer>();
                terrainRenderers.Add(tilemap, terrain);
            }
            return terrain;
        }

        private bool ShowGhost(int index, Sprite sprite, Color tint, Vector3 center, Tilemap tilemap)
        {
            var terrain = TerrainRenderer(tilemap);
            var material = terrain == null ? null : terrain.sharedMaterial;
            if (material == null)
                return false;

            ref var ghost = ref ghosts[index];
            bool newlyVisible = index >= visibleGhosts;
            if (ghost.Renderer == null)
            {
                var item = new GameObject("Pitch or moat preview");
                item.transform.SetParent(transform, true);
                ghost.Renderer = item.AddComponent<SpriteRenderer>();
                newlyVisible = true;
            }
            var renderer = ghost.Renderer;
            if (newlyVisible || ghost.Terrain != terrain || ghost.Sprite != sprite || ghost.Material != material || ghost.Tint != tint)
            {
                ghost.Terrain = terrain;
                ghost.Sprite = sprite;
                ghost.Material = material;
                ghost.Tint = tint;
                renderer.gameObject.layer = terrain.gameObject.layer;
                renderer.sortingLayerID = terrain.sortingLayerID;
                renderer.sortingOrder = terrain.sortingOrder + 1;
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.color = tint;
            }
            if (newlyVisible || ghost.Center != center)
            {
                ghost.Center = center;
                renderer.transform.position = center;
            }
            if (newlyVisible)
                renderer.enabled = true;
            return true;
        }

        private void HideExtraGhosts(int shown)
        {
            for (int i = shown; i < visibleGhosts; i++)
                if (ghosts[i].Renderer != null)
                    ghosts[i].Renderer.enabled = false;
            visibleGhosts = shown;
        }

        private void HideFrame()
        {
            frameSnapshot = null;
            outlineCount = 0;
            HideExtraGhosts(0);
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused)
                HideFrame();
        }

        private void OnDisable() => HideFrame();

        private void OnDestroy()
        {
            if (view == this)
                view = null;
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Application.isFocused || outlineCount == 0 ||
                !ReferenceEquals(frameSnapshot, PlacementRuntime.GetPreview()))
                return;
            var camera = Camera.main;
            if (camera == null)
                return;

            var originalColor = GUI.color;
            var originalMatrix = GUI.matrix;
            try
            {
                for (int i = 0; i < outlineCount; i++)
                {
                    var outline = outlines[i];
                    var center = outline.Center;
                    Vector3 point = camera.WorldToScreenPoint(center);
                    if (point.z < 0 || point.x < -100 || point.x > Screen.width + 100 ||
                        point.y < -100 || point.y > Screen.height + 100)
                        continue;
                    var corner = camera.WorldToScreenPoint(center + new Vector3(0.47f, 0.23f, 0f));
                    var p = new Vector2(point.x, Screen.height - point.y);
                    DrawTile(p, Mathf.Abs(corner.x - point.x), Mathf.Abs(corner.y - point.y),
                        outline.Color, originalMatrix);
                }
            }
            finally
            {
                GUI.matrix = originalMatrix;
                GUI.color = originalColor;
            }
        }

        private static Color TileColor(LineTileState state)
        {
            switch (state)
            {
                case LineTileState.Buildable: return Valid;
                case LineTileState.Existing: return Existing;
                case LineTileState.Material: return Unaffordable;
                case LineTileState.Capacity: return Capacity;
                default: return Invalid;
            }
        }

        private static void DrawTile(Vector2 center, float halfWidth, float halfHeight, Color color, Matrix4x4 matrix)
        {
            GUI.color = color;
            var top = center + new Vector2(0, -halfHeight);
            var right = center + new Vector2(halfWidth, 0);
            var bottom = center + new Vector2(0, halfHeight);
            var left = center + new Vector2(-halfWidth, 0);
            DrawEdge(top, right, matrix);
            DrawEdge(right, bottom, matrix);
            DrawEdge(bottom, left, matrix);
            DrawEdge(left, top, matrix);
        }

        private static void DrawEdge(Vector2 start, Vector2 end, Matrix4x4 matrix)
        {
            var delta = end - start;
            GUI.matrix = matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - 0.5f, delta.magnitude, 1f), Texture2D.whiteTexture);
        }

        private readonly struct Outline
        {
            internal readonly Vector3 Center;
            internal readonly Color Color;

            internal Outline(Vector3 center, Color color)
            {
                Center = center;
                Color = color;
            }
        }

        private struct Ghost
        {
            internal SpriteRenderer Renderer;
            internal TilemapRenderer Terrain;
            internal Sprite Sprite;
            internal Material Material;
            internal Vector3 Center;
            internal Color Tint;
        }
    }
}
