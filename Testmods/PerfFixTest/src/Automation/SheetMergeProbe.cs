using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerfFixTest.Automation
{
    // V7 feasibility probe (read-only apart from one temporary, destroyed texture):
    // - GPU limits: maximum texture size, CopyTexture support, BC7/DXT1 support, VRAM;
    // - every sprite of the TeamColour sheets: tight mesh, and whether all mesh vertices lie inside
    //   the sprite rect (Sprite.OverrideGeometry only accepts vertices inside the rect, so a sheet
    //   merge can only rebuild such sprites with identical geometry);
    // - one real GPU copy of a whole BC7 sheet into a 16384² BC7 page, timed.
    // Runs once in the main menu when [Probes] SheetMergeProbe is enabled.
    internal static class SheetMergeProbe
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        internal static void Run(Action<string> log)
        {
            log("probe=sheetmerge gpu=" + SystemInfo.graphicsDeviceName + " vramMB=" + SystemInfo.graphicsMemorySize +
                " maxTexture=" + SystemInfo.maxTextureSize + " copyTexture=" + SystemInfo.copyTextureSupport +
                " bc7=" + SystemInfo.SupportsTextureFormat(TextureFormat.BC7) + " dxt1=" + SystemInfo.SupportsTextureFormat(TextureFormat.DXT1));
            var gm = AccessTools.Field(typeof(spriteLoader), "gmSprites")?.GetValue(spriteLoader.instance) as Sprite[][];
            var alt = AccessTools.Field(typeof(spriteLoader), "gmAltSprites")?.GetValue(spriteLoader.instance) as Sprite[][];
            var materials = AccessTools.Field(typeof(spriteLoader), "gmMaterials")?.GetValue(spriteLoader.instance) as Material[][];
            if (gm == null || materials == null) { log("probe=sheetmerge FAIL spriteLoader fields unavailable"); return; }

            // Sheets per material: a TeamColour GM file maps to one sheet (one _TeamMask) -> its 7 materials.
            var sheetSprites = new Dictionary<Texture2D, HashSet<Sprite>>();
            var sheetMask = new Dictionary<Texture2D, Texture>();
            var sheetFiles = new Dictionary<Texture2D, HashSet<int>>();
            for (int file = 0; file < gm.Length; file++)
            {
                Material first = materials.Length > file && materials[file] != null && materials[file].Length > 0 ? materials[file][0] : null;
                if (first == null || first.shader == null || first.shader.name != "Unlit/TeamColour") continue;
                foreach (Sprite[] array in new[] { gm[file], alt != null && alt.Length > file ? alt[file] : null })
                {
                    if (array == null) continue;
                    foreach (Sprite sprite in array)
                    {
                        if (sprite == null || sprite.texture == null) continue;
                        if (!sheetSprites.TryGetValue(sprite.texture, out HashSet<Sprite> set)) sheetSprites[sprite.texture] = set = new HashSet<Sprite>();
                        set.Add(sprite);
                        if (!sheetFiles.TryGetValue(sprite.texture, out HashSet<int> files)) sheetFiles[sprite.texture] = files = new HashSet<int>();
                        files.Add(file);
                        if (!sheetMask.ContainsKey(sprite.texture)) sheetMask[sprite.texture] = first.GetTexture("_TeamMask");
                    }
                }
            }
            int totalSprites = 0, totalTight = 0, totalOutside = 0, totalPacked = 0, totalMaskMismatch = 0;
            float worstOverhang = 0f;
            foreach (KeyValuePair<Texture2D, HashSet<Sprite>> sheet in sheetSprites.OrderBy(e => e.Key.name))
            {
                int tight = 0, outside = 0, packed = 0;
                float overhang = 0f;
                foreach (Sprite sprite in sheet.Value)
                {
                    if (sprite.packed) packed++;
                    Vector2[] vertices = sprite.vertices;
                    if (vertices.Length > 4) tight++;
                    Rect rect = sprite.rect;
                    float ppu = sprite.pixelsPerUnit;
                    Vector2 pivot = sprite.pivot;
                    bool any = false;
                    foreach (Vector2 v in vertices)
                    {
                        // Local units -> pixels in rect space (origin at the rect's lower-left corner).
                        float x = v.x * ppu + pivot.x, y = v.y * ppu + pivot.y;
                        float over = Math.Max(Math.Max(-x, x - rect.width), Math.Max(-y, y - rect.height));
                        if (over > 0.01f) { any = true; overhang = Math.Max(overhang, over); }
                    }
                    if (any) outside++;
                }
                Texture mask = sheetMask[sheet.Key];
                bool maskMatches = mask != null && mask.width == sheet.Key.width && mask.height == sheet.Key.height;
                if (!maskMatches) totalMaskMismatch++;
                totalSprites += sheet.Value.Count; totalTight += tight; totalOutside += outside; totalPacked += packed;
                worstOverhang = Math.Max(worstOverhang, overhang);
                log(string.Format(C, "probe=sheetmerge sheet={0} {1}x{2} {3} files={4} sprites={5} tight={6} verticesOutsideRect={7} maxOverhangPx={8:0.0} packed={9} mask={10} maskSameSize={11}",
                    sheet.Key.name, sheet.Key.width, sheet.Key.height, sheet.Key.format, sheetFiles[sheet.Key].Count, sheet.Value.Count, tight, outside,
                    overhang, packed, mask != null ? mask.name + " " + mask.width + "x" + mask.height : "none", maskMatches));
            }
            log(string.Format(C, "probe=sheetmerge summary sheets={0} sprites={1} tight={2} verticesOutsideRect={3} maxOverhangPx={4:0.0} packed={5} maskSizeMismatch={6}",
                sheetSprites.Count, totalSprites, totalTight, totalOutside, worstOverhang, totalPacked, totalMaskMismatch));
            TryPageCopy(log, sheetSprites.Keys.FirstOrDefault(t => t.width == 8192 && t.height == 8192));
        }

        private static void TryPageCopy(Action<string> log, Texture2D sheet)
        {
            if (sheet == null) { log("probe=sheetmerge copy skipped: no 8192² sheet"); return; }
            Texture2D page = null;
            try
            {
                long started = Stopwatch.GetTimestamp();
                page = new Texture2D(16384, 16384, sheet.format, false);
                // Upload once and drop the CPU copy; afterwards the page exists only on the GPU.
                page.Apply(false, true);
                double createMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                started = Stopwatch.GetTimestamp();
                Graphics.CopyTexture(sheet, 0, 0, 0, 0, sheet.width, sheet.height, page, 0, 0, 8192, 0);
                double copyMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                log(string.Format(C, "probe=sheetmerge copy OK sheet={0} format={1} pageCreateMs={2:0.0} copyMs={3:0.0} (GPU work may complete asynchronously)",
                    sheet.name, sheet.format, createMs, copyMs));
            }
            catch (Exception exception) { log("probe=sheetmerge copy FAIL " + exception.GetType().Name + ": " + exception.Message); }
            finally { if (page != null) UnityEngine.Object.Destroy(page); }
        }
    }
}
