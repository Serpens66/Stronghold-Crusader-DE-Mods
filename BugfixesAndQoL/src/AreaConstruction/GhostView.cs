using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BugfixesAndQoL.AreaConstruction
{
    internal sealed class GhostView
    {
        private static readonly Color Allowed = new Color(1f, 1f, 1f, 0.59f);
        private static readonly Color Blocked = new Color(0.78f, 0.28f, 0.28f, 0.76f);
        private static readonly Color CrewGround = new Color(0.62f, 1f, 0.65f, 0.8f);
        private static readonly Color PileGround = new Color(1f, 0.79f, 0.48f, 0.85f);
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        private readonly Dictionary<Tilemap, TilemapRenderer> rows = new Dictionary<Tilemap, TilemapRenderer>();
        private GameObject root;
        private int visible;

        internal bool Show(EditorDirector director, GameMap map, eMappers tool, int scale, List<Site> sites)
        {
            if (spriteLoader.instance == null || map == null || !TryRotation(map.CurrentRotation(), out int rotation))
            {
                Hide();
                return false;
            }

            if (root == null || root.transform.parent != director.transform)
            {
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                root = new GameObject("Area construction preview");
                root.transform.SetParent(director.transform, false);
                sprites.Clear();
                rows.Clear();
                visible = 0;
            }

            var file = NativePlacement.SpriteFile(tool);
            bool farm = NativePlacement.IsFarm(tool);
            bool crewFootprint = CrewPlacement.Required(tool) != 0;
            int height = NativePlacement.FootprintHeight(tool, scale);
            Camera camera = Camera.main;
            int shown = 0;
            foreach (var site in sites)
            {
                if (site.Frame <= 0 && !crewFootprint)
                    continue;
                Color tint = site.Buildable ? (crewFootprint ? CrewGround : Allowed) : Blocked;
                int cornerX = site.Cursor.X - scale / 2;
                int cornerY = site.Cursor.Y - scale / 2;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < scale; x++)
                    {
                        int mapX = cornerX + x, mapY = cornerY + y;
                        if (!TryProject(mapX, mapY, out var tile, out var terrain, out var center))
                            continue;

                        Sprite sprite;
                        if (crewFootprint)
                        {
                            sprite = tile.tileImage;
                        }
                        else if (site.PreviewTiles != null)
                        {
                            int token = site.PreviewTiles[y * scale + x];
                            if (token == 0)
                                continue;
                            sprite = spriteLoader.instance.GetGMSprite((Enums.GM)(token >> 16), (token & 0xFFFF) + 1);
                        }
                        else if (farm)
                        {
                            sprite = x < 3 && y < 3
                                ? spriteLoader.instance.GetGMSprite(file, site.Frame + Index(x, y, 3, rotation))
                                : tile.tileImage;
                        }
                        else if (y >= scale)
                        {
                            sprite = spriteLoader.instance.GetGMSprite(Enums.GM.GM_BUILDINGS1,
                                297 + Index(x, y - scale, scale, rotation));
                        }
                        else
                        {
                            sprite = spriteLoader.instance.GetGMSprite(file, site.Frame + Index(x, y, scale, rotation));
                        }
                        Paint(terrain, center, sprite, tint);
                    }
                }
                if (site.Pile is Tile pile)
                {
                    for (int y = 0; y < QuarryLayout.PileScale; y++)
                    for (int x = 0; x < QuarryLayout.PileScale; x++)
                        if (TryProject(pile.X + x, pile.Y + y, out var tile, out var terrain, out var center))
                            Paint(terrain, center, tile.tileImage, site.Buildable ? PileGround : Blocked);
                }
            }

            HideFrom(shown);
            return shown > 0;

            bool TryProject(int mapX, int mapY, out GameMapTile tile, out TilemapRenderer terrain, out Vector3 center)
            {
                terrain = null;
                center = default;
                map.mapGameTileToTilemapCoord(mapX, mapY, out int tx, out int ty);
                tile = map.getMapTile(tx, ty);
                if (tile == null || tile.gameMapX != mapX || tile.gameMapY != mapY || tile.tilemapRef == null)
                    return false;
                var row = tile.tilemapRef;
                center = row.GetCellCenterWorld(new Vector3Int(tx, ty, 0)) +
                    row.transform.TransformVector(new Vector3(0f, tile.height, 0f));
                if (camera != null && !NearViewport(camera.WorldToViewportPoint(center)))
                    return false;
                if (!rows.TryGetValue(row, out terrain) || terrain == null)
                {
                    terrain = row.GetComponent<TilemapRenderer>();
                    rows[row] = terrain;
                }
                return terrain != null;
            }

            void Paint(TilemapRenderer terrain, Vector3 center, Sprite sprite, Color tint)
            {
                if (sprite == null)
                    return;
                SpriteRenderer renderer = Take(shown++);
                renderer.gameObject.layer = terrain.gameObject.layer;
                renderer.sortingLayerID = terrain.sortingLayerID;
                renderer.sortingOrder = terrain.sortingOrder + 1;
                renderer.sharedMaterial = terrain.sharedMaterial;
                renderer.transform.position = center;
                renderer.sprite = sprite;
                renderer.color = tint;
                renderer.enabled = true;
            }
        }

        internal void Hide() => HideFrom(0);

        private void HideFrom(int first)
        {
            for (int i = first; i < visible; i++)
                if (sprites[i] != null) sprites[i].enabled = false;
            visible = first;
        }

        private SpriteRenderer Take(int index)
        {
            if (index < sprites.Count)
                return sprites[index];
            var item = new GameObject("Building preview tile");
            item.transform.SetParent(root.transform, false);
            var renderer = item.AddComponent<SpriteRenderer>();
            sprites.Add(renderer);
            return renderer;
        }

        private static bool NearViewport(Vector3 point) =>
            point.z >= 0 && point.x >= -0.15f && point.x <= 1.15f && point.y >= -0.15f && point.y <= 1.15f;

        private static int Index(int x, int y, int scale, int rotation)
        {
            int edge = scale - 1;
            switch (rotation)
            {
                case 2: return DiagonalFrame(y, edge - x, scale);
                case 4: return DiagonalFrame(edge - x, edge - y, scale);
                case 6: return DiagonalFrame(edge - y, x, scale);
                default: return DiagonalFrame(x, y, scale);
            }
        }

        private static int DiagonalFrame(int x, int y, int scale)
        {
            int diagonal = x + y;
            int firstRow = Math.Max(0, diagonal - scale + 1);
            int preceding = diagonal < scale
                ? diagonal * (diagonal + 1) / 2
                : scale * scale - (2 * scale - diagonal - 1) * (2 * scale - diagonal) / 2;
            return scale * scale - 1 - preceding - (y - firstRow);
        }

        private static bool TryRotation(Enums.Dircs direction, out int rotation)
        {
            rotation = (int)direction - (int)Enums.Dircs.North;
            return rotation >= 0 && rotation <= 6 && rotation % 2 == 0;
        }
    }
}
