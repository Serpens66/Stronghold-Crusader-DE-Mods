using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace PerfFixTest
{
    // V7 prototype: merge the TeamColour sprite sheets (and their identical-layout _TeamMask sheets)
    // as whole sheets onto 16384² pages, one material set per page. Sprites are recreated on the page
    // with the same rect size, pivot, PPU and the original mesh (sprite data access API), so the
    // drawn pixels stay identical; only the texture/material identity changes and neighbouring
    // sprites of different sheets on one page can batch.
    // Feasibility was verified in-game (2026-10-20): max texture 16384, CopyTexture for BC7/DXT1,
    // 111 492 sprites on 13 sheets, 0 mesh vertices outside their rect, masks same size as sheets.
    // Replaces references in spriteLoader (gmSprites, gmAltSprites, allSprites, gmMaterials) and,
    // when applied in a running mission, on all existing SpriteRenderers.
    // Test only: original sheets stay loaded (extra VRAM), Script Extender sprite overrides are not
    // considered (none installed in the measured setup).
    internal static class SheetMerge
    {
        private const int Page = 16384, Slot = 8192;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        // Grouping from the census page simulation (sheets that follow each other in sort order).
        private static readonly string[][] PreferredPages =
        {
            new[] { "body12Sprites", "body14Sprites", "body16Sprites", "body13Sprites" },
            new[] { "body1Sprites", "body2Sprites", "body8Sprites", "anims1Sprites" },
            new[] { "body7Sprites", "body5Sprites", "body4Sprites", "body15Sprites" },
            new[] { "body6Sprites" },
        };

        internal static bool Applied { get; private set; }
        internal static int GeometryFailures { get; private set; }
        private static readonly Dictionary<Sprite, Sprite> spriteMap = new Dictionary<Sprite, Sprite>();
        private static readonly Dictionary<Material, Material> materialMap = new Dictionary<Material, Material>();
        // Per merged sheet: original material set and its page set (index = cutoff stage); used by the render check.
        internal static readonly List<SheetSet> SheetSets = new List<SheetSet>();
        internal sealed class SheetSet { public Texture2D Sheet; public Material[] Original, Page; }
        internal static IReadOnlyDictionary<Sprite, Sprite> SpriteMap => spriteMap;

        private sealed class SheetInfo
        {
            public Texture2D Color;
            public Texture Mask;
            public Material[] Materials;
            public readonly List<int> Files = new List<int>();
            public int PageIndex, OffsetX, OffsetY;
        }

        // Normal play: apply once right after Vanilla has loaded its sprites. The Script Extender applies
        // its sprite/atlas overrides synchronously in the same loader step that sets spritesLoaded, so
        // they are complete when the flag is seen here. There is no public "sprites loaded" event; the
        // callback stays registered (no teardown, process lifetime) and is a single bool read afterwards.
        private static Action<string> armedLog;

        internal static void ArmAfterSpriteLoad(Action<string> log)
        {
            armedLog = log;
            Application.onBeforeRender += WaitForSprites;
        }

        private static void WaitForSprites()
        {
            if (Applied) return;
            spriteLoader loader = spriteLoader.instance;
            if (loader == null || !loader.spritesLoaded) return;
            try { Apply(armedLog); }
            catch (Exception exception) { armedLog("v7 FAIL exception " + exception); }
        }

        internal static void Apply(Action<string> log)
        {
            if (Applied) return;
            Applied = true;
            long started = Stopwatch.GetTimestamp();
            ulong textureMemoryBefore = Texture.currentTextureMemory;
            var gm = AccessTools.Field(typeof(spriteLoader), "gmSprites").GetValue(spriteLoader.instance) as Sprite[][];
            var alt = AccessTools.Field(typeof(spriteLoader), "gmAltSprites").GetValue(spriteLoader.instance) as Sprite[][];
            var materials = AccessTools.Field(typeof(spriteLoader), "gmMaterials").GetValue(spriteLoader.instance) as Material[][];
            var all = AccessTools.Field(typeof(spriteLoader), "allSprites").GetValue(spriteLoader.instance) as Dictionary<string, Sprite>;
            if (gm == null || alt == null || materials == null || all == null) { log("v7 FAIL spriteLoader fields unavailable"); return; }

            // 1. TeamColour sheets: one sheet = one Material[7] (shared by its GM files) = one mask.
            var sheets = new Dictionary<Texture2D, SheetInfo>();
            int skippedFiles = 0;
            for (int file = 0; file < gm.Length; file++)
            {
                Material[] set = file < materials.Length ? materials[file] : null;
                if (set == null || set.Length != 7 || set[0] == null || set[0].shader == null || set[0].shader.name != "Unlit/TeamColour") continue;
                // Only untouched Vanilla files: one texture across main and alt sprites. Files partly or fully
                // replaced by Script Extender asset/atlas overrides keep their sprites and materials (the
                // Extender's partial atlases rely on the Vanilla mask material of their GM file).
                var textures = (gm[file] ?? new Sprite[0]).Concat(file < alt.Length && alt[file] != null ? alt[file] : new Sprite[0])
                    .Where(s => s != null).Select(s => s.texture).Distinct().ToList();
                if (textures.Count == 0) continue;
                if (textures.Count > 1) { skippedFiles++; continue; }
                Texture2D color = textures[0];
                Texture mask = set[0].GetTexture("_TeamMask");
                // A sheet must match its mask exactly and own its material set; anything else (e.g. a fully
                // replaced file whose atlas reuses the Vanilla material) stays untouched.
                bool shapeOk = mask != null && mask.width == color.width && mask.height == color.height &&
                    color.width == Slot && (color.height == Slot || color.height == Slot / 2);
                bool setOwnedElsewhere = sheets.Values.Any(s => ReferenceEquals(s.Materials, set) && s.Color != color);
                if (!shapeOk || setOwnedElsewhere) { skippedFiles++; continue; }
                if (!sheets.TryGetValue(color, out SheetInfo info))
                    sheets[color] = info = new SheetInfo { Color = color, Mask = mask, Materials = set };
                else if (!ReferenceEquals(info.Materials, set)) { log("v7 FAIL sheet " + color.name + " has more than one material set"); return; }
                info.Files.Add(file);
            }
            if (skippedFiles > 0) log("v7 skipped " + skippedFiles + " GM file(s) with replaced sprites (asset mods)");
            if (sheets.Values.Any(s => s.Mask == null || s.Mask.width != s.Color.width || s.Mask.height != s.Color.height ||
                s.Color.width != Slot || (s.Color.height != Slot && s.Color.height != Slot / 2)))
            { log("v7 FAIL unexpected sheet or mask size"); return; }

            // 2. Pages: preferred grouping first, remaining sheets fill free slots.
            var pageSheets = new List<List<SheetInfo>>();
            var byName = sheets.Values.ToDictionary(s => s.Color.name);
            var used = new HashSet<SheetInfo>();
            foreach (string[] group in PreferredPages)
            {
                var page = group.Where(byName.ContainsKey).Select(n => byName[n]).ToList();
                if (page.Count == 0) continue;
                pageSheets.Add(page);
                used.UnionWith(page);
            }
            foreach (SheetInfo rest in sheets.Values.Where(s => !used.Contains(s)))
            {
                var page = pageSheets.FirstOrDefault(p => p.Count < 4);
                if (page == null) pageSheets.Add(page = new List<SheetInfo>());
                page.Add(rest);
            }

            // 3. Build pages on the GPU: color and mask with the sheets' exact graphics formats.
            var pageMaterials = new List<Material[]>();
            int sprites = 0;
            for (int p = 0; p < pageSheets.Count; p++)
            {
                List<SheetInfo> group = pageSheets[p];
                Texture2D firstColor = group[0].Color;
                Texture firstMask = group[0].Mask;
                var colorPage = new Texture2D(Page, Page, firstColor.graphicsFormat, TextureCreationFlags.None)
                { name = "PerfFixTest_Page" + p, filterMode = firstColor.filterMode, wrapMode = firstColor.wrapMode, anisoLevel = firstColor.anisoLevel };
                var maskPage = new Texture2D(Page, Page, firstMask.graphicsFormat, TextureCreationFlags.None)
                { name = "PerfFixTest_PageMask" + p, filterMode = firstMask.filterMode, wrapMode = firstMask.wrapMode, anisoLevel = firstMask.anisoLevel };
                maskPage.Apply(false, true);
                for (int s = 0; s < group.Count; s++)
                {
                    SheetInfo sheet = group[s];
                    if (sheet.Color.graphicsFormat != firstColor.graphicsFormat || sheet.Mask.graphicsFormat != firstMask.graphicsFormat)
                    { log("v7 FAIL format mismatch on page " + p + ": " + sheet.Color.name); return; }
                    sheet.PageIndex = p;
                    sheet.OffsetX = (s % 2) * Slot;
                    sheet.OffsetY = (s / 2) * Slot;
                }
                // One material per cutoff stage, cloned from the first sheet (same shader and cutoff values).
                var set = new Material[7];
                for (int i = 0; i < 7; i++)
                {
                    set[i] = new Material(group[0].Materials[i]) { name = "PerfFixTest_Page" + p + "_" + i };
                    set[i].SetTexture("_TeamMask", maskPage);
                }
                pageMaterials.Add(set);
                foreach (SheetInfo sheet in group)
                {
                    for (int i = 0; i < 7; i++) materialMap[sheet.Materials[i]] = set[i];
                    SheetSets.Add(new SheetSet { Sheet = sheet.Color, Original = sheet.Materials, Page = set });
                    foreach (int file in sheet.Files) materials[file] = set;
                }
                // 4. Recreate every sprite of these sheets on the page with its original geometry.
                foreach (SheetInfo sheet in group)
                {
                    foreach (int file in sheet.Files)
                    {
                        sprites += Remap(gm[file], sheet, colorPage);
                        if (file < alt.Length) sprites += Remap(alt[file], sheet, colorPage);
                    }
                }
                // Sprites and their geometry are created while the page is still CPU-readable (content
                // irrelevant; verified working in this order). Then the page is uploaded, its CPU copy
                // dropped, and the sheets copied on the GPU.
                colorPage.Apply(false, true);
                foreach (SheetInfo sheet in group)
                {
                    Graphics.CopyTexture(sheet.Color, 0, 0, 0, 0, sheet.Color.width, sheet.Color.height, colorPage, 0, 0, sheet.OffsetX, sheet.OffsetY);
                    Graphics.CopyTexture(sheet.Mask, 0, 0, 0, 0, sheet.Mask.width, sheet.Mask.height, maskPage, 0, 0, sheet.OffsetX, sheet.OffsetY);
                }
            }
            foreach (string key in all.Keys.ToList())
                if (all[key] != null && spriteMap.TryGetValue(all[key], out Sprite replacement)) all[key] = replacement;
            double buildMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            int renderers = SwapLiveRenderers();
            log(string.Format(C, "v7 applied pages={0} sheets={1} sprites={2} geometryFailures={6} materialsMapped={3} liveRenderersSwapped={4} buildMs={5:0}",
                pageSheets.Count, sheets.Count, sprites, materialMap.Count, renderers, buildMs, GeometryFailures));
            log(string.Format(C, "v7 textureMemoryMB {0:0} -> {1:0}", textureMemoryBefore / 1048576.0, Texture.currentTextureMemory / 1048576.0));
            for (int p = 0; p < pageSheets.Count; p++)
                log("v7 page" + p + ": " + string.Join(" + ", pageSheets[p].Select(s => s.Color.name + "@" + s.OffsetX + "," + s.OffsetY)));
        }

        private static int Remap(Sprite[] array, SheetInfo sheet, Texture2D page)
        {
            if (array == null) return 0;
            int created = 0;
            for (int i = 0; i < array.Length; i++)
            {
                Sprite original = array[i];
                if (original == null || original.texture != sheet.Color) continue;
                if (!spriteMap.TryGetValue(original, out Sprite moved))
                {
                    Rect rect = original.rect;
                    Vector2 pivotPixels = original.pivot;
                    float ppu = original.pixelsPerUnit;
                    moved = Sprite.Create(page, new Rect(rect.x + sheet.OffsetX, rect.y + sheet.OffsetY, rect.width, rect.height),
                        new Vector2(pivotPixels.x / rect.width, pivotPixels.y / rect.height), ppu, 0, SpriteMeshType.FullRect, original.border, false);
                    // Original tight mesh: Sprite.OverrideGeometry is refused at runtime for these sprites
                    // ("Not allowed to override geometry"), so positions (local units, unchanged) and UVs
                    // (moved into the page slot) are written through the sprite data access API.
                    Vector2[] vertices = original.vertices;
                    Vector2[] uvs = original.uv;
                    ushort[] triangles = original.triangles;
                    var positions = new NativeArray<Vector3>(vertices.Length, Allocator.Temp);
                    var pageUvs = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
                    var indices = new NativeArray<ushort>(triangles, Allocator.Temp);
                    float sheetW = sheet.Color.width, sheetH = sheet.Color.height;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        positions[v] = vertices[v];
                        pageUvs[v] = new Vector2((uvs[v].x * sheetW + sheet.OffsetX) / Page, (uvs[v].y * sheetH + sheet.OffsetY) / Page);
                    }
                    moved.SetVertexCount(vertices.Length);
                    moved.SetVertexAttribute(VertexAttribute.Position, positions);
                    moved.SetVertexAttribute(VertexAttribute.TexCoord0, pageUvs);
                    moved.SetIndices(indices);
                    positions.Dispose(); pageUvs.Dispose(); indices.Dispose();
                    if (moved.vertices.Length != vertices.Length || moved.triangles.Length != triangles.Length) GeometryFailures++;
                    moved.name = original.name;
                    spriteMap[original] = moved;
                    created++;
                }
                array[i] = moved;
            }
            return created;
        }

        private static int SwapLiveRenderers()
        {
            int swapped = 0;
            foreach (SpriteRenderer renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                // The page mask is only valid with the page sprite (same UVs): material and sprite move together.
                Sprite sprite = renderer.sprite;
                if (sprite == null || !spriteMap.TryGetValue(sprite, out Sprite moved)) continue;
                renderer.sprite = moved;
                Material material = renderer.sharedMaterial;
                if (material != null && materialMap.TryGetValue(material, out Material mapped)) renderer.sharedMaterial = mapped;
                swapped++;
            }
            return swapped;
        }
    }
}
