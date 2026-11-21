using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace AreaConstructionTest
{
    internal sealed class GhostView
    {
        private static readonly Color Allowed = new Color(1f, 1f, 1f, 0.59f);
        private static readonly Color Blocked = new Color(0.78f, 0.28f, 0.28f, 0.76f);
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
            Camera camera = Camera.main;
            int shown = 0;
            foreach (var site in sites)
            {
                if (site.Frame <= 0)
                    continue;
                Color tint = site.Buildable ? Allowed : Blocked;
                int cornerX = site.Cursor.X - scale / 2;
                int cornerY = site.Cursor.Y - scale / 2;
                for (int y = 0; y < scale; y++)
                {
                    for (int x = 0; x < scale; x++)
                    {
                        int mapX = cornerX + x, mapY = cornerY + y;
                        map.mapGameTileToTilemapCoord(mapX, mapY, out int tx, out int ty);
                        var tile = map.getMapTile(tx, ty);
                        if (tile == null || tile.gameMapX != mapX || tile.gameMapY != mapY || tile.tilemapRef == null)
                            continue;

                        var row = tile.tilemapRef;
                        Vector3 center = row.GetCellCenterWorld(new Vector3Int(tx, ty, 0)) +
                            row.transform.TransformVector(new Vector3(0f, tile.height, 0f));
                        if (camera != null && !NearViewport(camera.WorldToViewportPoint(center)))
                            continue;

                        if (!rows.TryGetValue(row, out var terrain) || terrain == null)
                        {
                            terrain = row.GetComponent<TilemapRenderer>();
                            rows[row] = terrain;
                        }
                        if (terrain == null)
                            continue;

                        var sprite = spriteLoader.instance.GetGMSprite(file, site.Frame + Index(x, y, scale, rotation));
                        if (sprite == null)
                            continue;
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
            }

            HideFrom(shown);
            return shown > 0;
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
