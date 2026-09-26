using System;
using System.Collections.Generic;
using UnityEngine;

namespace KeepCampfireGroundPreserveTest
{
    // Vanilla graphic IDs are captured before the native store is suppressed.
    // Tile placement follows those IDs, rather than a hand-built sprite table.
    internal sealed class CampfireOverlayRuntime
    {
        private const int Footprint = 7;
        private const int MaskedTiles = 9;
        private readonly Action<string> log, error;
        private readonly Dictionary<int, Overlay> buildings = new Dictionary<int, Overlay>();
        private readonly MaskedSpriteCatalog catalog;
        private CampgroundNativeHook hook;
        private int lastRotation = int.MinValue;

        internal CampfireOverlayRuntime(Action<string> log, Action<string> error)
        { this.log = log; this.error = error; catalog = new MaskedSpriteCatalog(log, error); }

        internal void SetHook(CampgroundNativeHook value) => hook = value;
        internal bool Prepare() => catalog.Prepare();
        internal bool IsShown(int id) => id > 0 && buildings.TryGetValue(id, out Overlay o) &&
            o.Root != null && o.Root.activeSelf && o.Positioned;

        internal void ShowExisting(IReadOnlyList<GroundPreserveRuntime.CampObservation> camps)
        { foreach (var camp in camps) Show(camp.BuildingId, camp.X, camp.Y); }

        internal void Show(int id, int x, int y)
        {
            if (id <= 0 || buildings.ContainsKey(id) || buildings.Count >= 24) return;
            buildings.Add(id, new Overlay(id, x, y));
            lastRotation = int.MinValue;
            RefreshPositions();
        }

        internal void Hide(int id)
        {
            if (!buildings.TryGetValue(id, out Overlay o)) return;
            o.Hidden = true;
            if (o.Root != null) o.Root.SetActive(false);
        }

        internal void Remove(int id)
        {
            if (!buildings.TryGetValue(id, out Overlay o)) return;
            if (o.Root != null) UnityEngine.Object.Destroy(o.Root);
            buildings.Remove(id);
        }

        internal void Clear()
        {
            foreach (Overlay o in buildings.Values)
                if (o.Root != null) UnityEngine.Object.Destroy(o.Root);
            buildings.Clear();
            lastRotation = int.MinValue;
        }

        // Script Extender tick and spawn/load events invoke this. No per-render
        // polling or MonoBehaviour callback is needed.
        internal void RefreshPositions()
        {
            if (buildings.Count == 0 || GameMap.instance == null || hook == null ||
                !hook.IsPublished || !catalog.Prepare()) return;
            int rotation = (int)GameMap.instance.CurrentRotation();
            bool rotationChanged = rotation != lastRotation;
            foreach (Overlay o in buildings.Values)
            {
                if (o.Hidden) continue;
                try { RefreshOne(o, rotation, rotationChanged); }
                catch (Exception ex) {
                    o.Hidden = true;
                    if (o.Root != null) o.Root.SetActive(false);
                    error("masked hearth failed building=" + o.Id + ": " + ex);
                }
            }
            lastRotation = rotation;
        }

        private void RefreshOne(Overlay o, int rotation, bool rotationChanged)
        {
            var api = SHCDESE.API.GameTileManagerAPI.Instance;
            var parts = new TilePart[MaskedTiles];
            var nextGraphics = new int[Footprint * Footprint];
            int captured = 0, masked = 0;
            bool idsChanged = false;
            for (int dy = 0; dy < Footprint; dy++)
            for (int dx = 0; dx < Footprint; dx++)
            {
                int x = o.X + dx, y = o.Y + dy;
                if (!api.IsTileInsideMapBounds(x, y)) return;
                int gfx = hook.CapturedGraphic(api.GetTileId(x, y));
                if (gfx == 0) continue;
                captured++;
                int slot = dy * Footprint + dx;
                nextGraphics[slot] = gfx;
                if (o.Captured[slot] != gfx) idsChanged = true;
                if (!catalog.TryGet(gfx, out Sprite sprite)) continue;
                if (masked >= MaskedTiles)
                    throw new InvalidOperationException("Too many masked camp sprites.");
                parts[masked++] = new TilePart(x, y, gfx, sprite);
            }
            if (captured != 49 || masked != MaskedTiles) {
                if (++o.WaitingChecks == 4)
                    error("masked hearth incomplete building=" + o.Id +
                        " captured=" + captured + "/49 masked=" + masked + "/9; hidden");
                return;
            }
            // A complete ring must contain every source tile once. Fail closed
            // if a different view/load path yields a different nine-tile set.
            var unique = new HashSet<int>();
            foreach (TilePart part in parts) unique.Add(part.Gfx);
            if (unique.Count != MaskedTiles)
                throw new InvalidOperationException("Duplicate foreground graphic ID.");
            if (o.Positioned && !rotationChanged && !idsChanged) return;
            if (o.Root == null) CreateRenderers(o);
            for (int i = 0; i < parts.Length; i++)
            {
                TilePart part = parts[i];
                GameMap.instance.mapGameTileToTilemapCoord(part.X, part.Y,
                    out int mapX, out int mapY);
                GameMapTile tile = GameMap.instance.getMapTile(mapX, mapY);
                if (tile == null || tile.tilemapRef == null)
                    throw new InvalidOperationException("Missing Tilemap cell.");
                var cell = new Vector3Int(mapX, mapY, 0);
                Vector3 position = tile.tilemapRef.GetCellCenterWorld(cell);
                position.y += tile.height;
                position.z = GameMap.instance.getSpritePosVector(mapX, mapY).z;
                SpriteRenderer renderer = o.Renderers[i];
                renderer.sprite = part.Sprite;
                renderer.transform.position = position;
                renderer.sortingOrder = -20000 + tile.row * 49 + 1;
                renderer.color = tile.tilemapRef.GetColor(cell);
                renderer.enabled = true;
            }
            Array.Copy(nextGraphics, o.Captured, nextGraphics.Length);
            o.Root.SetActive(true);
            o.Positioned = true;
            o.WaitingChecks = 0;
            log("masked hearth positioned building=" + o.Id +
                " origin=" + o.X + "," + o.Y + " rotation=" + rotation +
                " captured=49 masked=9 ids=" + string.Join(",",
                    Array.ConvertAll(parts, p => (p.Gfx & 0xFFFF).ToString("X2"))));
        }

        private static void CreateRenderers(Overlay o)
        {
            o.Root = new GameObject("KeepCampfireHearth_" + o.Id);
            o.Root.SetActive(false);
            for (int i = 0; i < o.Renderers.Length; i++) {
                var child = new GameObject("MaskedHearth_" + i);
                child.transform.SetParent(o.Root.transform, false);
                var renderer = child.AddComponent<SpriteRenderer>();
                renderer.sharedMaterial = spriteLoader.instance.plainMaterials[0];
                renderer.enabled = false;
                o.Renderers[i] = renderer;
            }
        }

        private readonly struct TilePart
        {
            internal readonly int X, Y, Gfx;
            internal readonly Sprite Sprite;
            internal TilePart(int x, int y, int gfx, Sprite sprite)
            { X = x; Y = y; Gfx = gfx; Sprite = sprite; }
        }

        private sealed class Overlay
        {
            internal readonly int Id, X, Y;
            internal readonly int[] Captured = new int[49];
            internal readonly SpriteRenderer[] Renderers = new SpriteRenderer[MaskedTiles];
            internal GameObject Root;
            internal bool Hidden, Positioned;
            internal int WaitingChecks;
            internal Overlay(int id, int x, int y) { Id = id; X = x; Y = y; }
        }
    }
}
