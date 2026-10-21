using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V7: about 60 % of the draw calls at far zoom come from neighbouring units of different
    // TeamColour sheets, because every sheet owns its material set (the _TeamMask texture). The 13
    // sheets and their identical-layout masks are copied once on the GPU onto 16384² pages with one
    // material set per page. Every sprite is recreated on its page with the same rect size, pivot, PPU
    // and original mesh (sprite data access API), so the drawn pixels stay the same and only the
    // texture/material identity changes. Sprites and materials are always replaced together.
    // Runs once after Vanilla's SpriteLoad, which also contains the Script Extender's synchronous
    // sprite/atlas overrides; GM files changed by asset mods (more than one texture) stay unchanged.
    // The original sheets stay loaded (row tilemaps keep using them): about +1.6 GB texture memory.
    // All checks and the complete page build happen before the first spriteLoader write; a failure
    // destroys the unpublished objects and leaves Vanilla unchanged. Changes need a restart.
    internal static class SpriteSheetMergeOptimization
    {
        private const int Page = 16384, Slot = 8192, CutoffStages = 7, SheetsPerPage = 4;
        // Vanilla needs about 2.4 GB of texture memory, the merge adds about 1.6 GB.
        private const int MinimumGraphicsMemoryMb = 6144;
        private const string TeamColourShader = "Unlit/TeamColour", MaskProperty = "_TeamMask";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        // Sheets that usually follow each other in the sort order share a page (draw-call census).
        private static readonly string[][] PreferredPages =
        {
            new[] { "body12Sprites", "body14Sprites", "body16Sprites", "body13Sprites" },
            new[] { "body1Sprites", "body2Sprites", "body8Sprites", "anims1Sprites" },
            new[] { "body7Sprites", "body5Sprites", "body4Sprites", "body15Sprites" },
            new[] { "body6Sprites" },
        };
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.SpriteSheetMerge");
        private static ManualLogSource log;
        private static Func<bool> isEnabled;
        private static int armed, decided, applied, loadCalls;

        private sealed class Sheet
        {
            public Texture2D Color;
            public Texture Mask;
            public Material[] Materials;
            public readonly List<int> Files = new List<int>();
            public int OffsetX, OffsetY;
        }

        private sealed class LoaderFields
        {
            public Sprite[][] Main, Alt;
            public Material[][] Materials;
            public Dictionary<string, Sprite> All;
        }

        // Unpublished build result; Discard destroys everything it created.
        private sealed class Build
        {
            public readonly List<UnityEngine.Object> Created = new List<UnityEngine.Object>();
            public readonly Dictionary<Sprite, Sprite> Sprites = new Dictionary<Sprite, Sprite>();
            public readonly Dictionary<Material, Material> Materials = new Dictionary<Material, Material>();
            public readonly Dictionary<int, Material[]> FileMaterials = new Dictionary<int, Material[]>();
            public readonly List<List<Sheet>> Pages = new List<List<Sheet>>();

            public void Discard()
            {
                foreach (UnityEngine.Object created in Created)
                    if (created != null) UnityEngine.Object.Destroy(created);
                Created.Clear();
            }
        }

        internal static void Arm(ManualLogSource logger, Func<bool> enabled)
        {
            if (Interlocked.Exchange(ref armed, 1) != 0) return;
            log = logger;
            isEnabled = enabled;
            try
            {
                MethodInfo load = typeof(spriteLoader).GetMethod(nameof(spriteLoader.loadSprites),
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (load == null || load.ReturnType != typeof(void))
                    throw new MissingMethodException("spriteLoader.loadSprites signature differs from the audit.");
                harmony.Patch(load, prefix: new HarmonyMethod(typeof(SpriteSheetMergeOptimization), nameof(BeforeLoadSprites)));
                // No public "sprites loaded" event exists; after the decision this is a single int read per frame.
                Application.onBeforeRender += WaitForSprites;
            }
            catch (Exception exception)
            {
                Volatile.Write(ref decided, 1);
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/SpriteSheetMergeOptimization.cs:96",
                    "SPRITE_SHEET_MERGE_DISABLED: could not arm; Vanilla sprites retained; " + exception);
            }
        }

        // Vanilla loads sprites once (intro or pending multiplayer lobby). A second load would rebuild
        // the loader arrays from the original sheets; merging during such a load could pair page sprites
        // with original materials, so the merge only runs after exactly one observed load.
        private static void BeforeLoadSprites()
        {
            Interlocked.Increment(ref loadCalls);
            if (Volatile.Read(ref applied) != 0)
                Shared.DebugLogHelper.LogInfo(log,
                    "SPRITE_SHEET_MERGE_SUPERSEDED: Vanilla reloads its sprites; the reloaded GM files use the original sheets.");
        }

        private static void WaitForSprites()
        {
            if (Volatile.Read(ref decided) != 0) return;
            spriteLoader loader = spriteLoader.instance;
            if (loader == null || !loader.spritesLoaded) return;
            Volatile.Write(ref decided, 1);
            try { Decide(loader); }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/SpriteSheetMergeOptimization.cs:120",
                    "SPRITE_SHEET_MERGE_FAILED: Vanilla sprites retained; " + exception);
            }
        }

        private static void Decide(spriteLoader loader)
        {
            if (!isEnabled())
            {
                Shared.DebugLogHelper.LogInfo(log, "SPRITE_SHEET_MERGE_OFF: disabled in the settings; Vanilla sprites retained until restart.");
                return;
            }
            int loads = Volatile.Read(ref loadCalls);
            string refusal = loads != 1 ? "observed " + loads + " sprite loads instead of one" : DeviceRefusal();
            if (refusal != null)
            {
                Shared.DebugLogHelper.LogWarning(log, "SPRITE_SHEET_MERGE_SKIPPED: " + refusal + "; Vanilla sprites retained.");
                return;
            }
            long started = Stopwatch.GetTimestamp();
            ulong memoryBefore = Texture.currentTextureMemory;
            LoaderFields fields = ReadLoaderFields(loader);
            List<Sheet> sheets = PlanSheets(fields, out int skippedFiles, out refusal);
            if (refusal == null) refusal = ValidateFormats(sheets);
            if (refusal != null)
            {
                Shared.DebugLogHelper.LogWarning(log, "SPRITE_SHEET_MERGE_SKIPPED: " + refusal + "; Vanilla sprites retained.");
                return;
            }
            var build = new Build();
            try { BuildPages(fields, sheets, build); }
            catch
            {
                build.Discard();
                throw;
            }
            Commit(fields, build);
            Volatile.Write(ref applied, 1);
            int renderers = -1;
            try { renderers = SwapLiveRenderers(build); }
            catch (Exception exception)
            {
                // The loader is already consistent; renderers keep their own consistent original pair.
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/SpriteSheetMergeOptimization.cs:159",
                    "SPRITE_SHEET_MERGE_LIVE_RENDERERS_FAILED: " + exception);
            }
            double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                Shared.DebugLogHelper.LogInfo(log, string.Format(Invariant,
                    "SPRITE_SHEET_MERGE_APPLIED: pages={0} sheets={1} sprites={2} skippedAssetModFiles={3} liveRenderers={4} " +
                    "buildMs={5:0} textureMemoryMB={6:0}->{7:0}; {8}",
                    build.Pages.Count, sheets.Count, build.Sprites.Count, skippedFiles, renderers, ms,
                    memoryBefore / 1048576.0, Texture.currentTextureMemory / 1048576.0,
                    string.Join(" | ", build.Pages.Select(page => string.Join("+", page.Select(sheet => sheet.Color.name))))));
        }

        private static string DeviceRefusal()
        {
            if (SystemInfo.maxTextureSize < Page)
                return "maximum texture size " + SystemInfo.maxTextureSize + " < " + Page;
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
                return "GPU texture copies are not supported";
            if (SystemInfo.graphicsMemorySize < MinimumGraphicsMemoryMb)
                return "graphics memory " + SystemInfo.graphicsMemorySize + " MB < " + MinimumGraphicsMemoryMb + " MB";
            return null;
        }

        private static LoaderFields ReadLoaderFields(spriteLoader loader) => new LoaderFields
        {
            Main = ReadField<Sprite[][]>(loader, "gmSprites"),
            Alt = ReadField<Sprite[][]>(loader, "gmAltSprites"),
            Materials = ReadField<Material[][]>(loader, "gmMaterials"),
            All = ReadField<Dictionary<string, Sprite>>(loader, "allSprites"),
        };

        private static T ReadField<T>(spriteLoader loader, string name) where T : class
        {
            FieldInfo field = typeof(spriteLoader).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(T))
                throw new MissingFieldException("spriteLoader." + name + " differs from the audit.");
            return field.GetValue(loader) as T ?? throw new InvalidOperationException("spriteLoader." + name + " is null.");
        }

        // One sheet = one Material[7] (shared by its GM files) = one mask. Only untouched Vanilla files
        // qualify: one texture across main and alt sprites, an owned TeamColour material set and a mask
        // of the same size. Files partly or fully replaced by Script Extender asset/atlas overrides keep
        // their sprites and materials (partial atlases rely on the Vanilla mask material of their file).
        private static List<Sheet> PlanSheets(LoaderFields fields, out int skippedFiles, out string refusal)
        {
            refusal = null;
            skippedFiles = 0;
            var sheets = new Dictionary<Texture2D, Sheet>();
            for (int file = 0; file < fields.Main.Length; file++)
            {
                Material[] set = file < fields.Materials.Length ? fields.Materials[file] : null;
                if (set == null || set.Length != CutoffStages || set.Any(m => m == null) ||
                    set.Any(m => m.shader == null || m.shader.name != TeamColourShader))
                    continue;
                var textures = Sprites(fields.Main[file]).Concat(Sprites(file < fields.Alt.Length ? fields.Alt[file] : null))
                    .Select(sprite => sprite.texture).Distinct().ToList();
                if (textures.Count == 0) continue;
                Texture2D color = textures[0];
                Texture mask = set[0].GetTexture(MaskProperty);
                bool shapeOk = textures.Count == 1 && color != null && mask != null &&
                    set.All(m => ReferenceEquals(m.GetTexture(MaskProperty), mask)) &&
                    mask.width == color.width && mask.height == color.height &&
                    color.width == Slot && (color.height == Slot || color.height == Slot / 2);
                bool setOwnedElsewhere = sheets.Values.Any(s => ReferenceEquals(s.Materials, set) && s.Color != color);
                if (!shapeOk || setOwnedElsewhere)
                {
                    skippedFiles++;
                    continue;
                }
                if (!sheets.TryGetValue(color, out Sheet sheet))
                    sheets[color] = sheet = new Sheet { Color = color, Mask = mask, Materials = set };
                else if (!ReferenceEquals(sheet.Materials, set))
                {
                    refusal = "sheet " + color.name + " has more than one material set";
                    return null;
                }
                sheet.Files.Add(file);
            }
            if (sheets.Count == 0) refusal = "no Vanilla TeamColour sheet found";
            if (sheets.Values.Select(s => s.Color.name).Distinct().Count() != sheets.Count)
                refusal = "TeamColour sheet names are not unique";
            return refusal == null ? sheets.Values.ToList() : null;
        }

        private static IEnumerable<Sprite> Sprites(Sprite[] array) =>
            array == null ? Enumerable.Empty<Sprite>() : array.Where(sprite => sprite != null);

        private static List<List<Sheet>> AssignPages(List<Sheet> sheets)
        {
            var pages = new List<List<Sheet>>();
            var byName = sheets.ToDictionary(sheet => sheet.Color.name);
            var used = new HashSet<Sheet>();
            foreach (string[] group in PreferredPages)
            {
                var page = group.Where(byName.ContainsKey).Select(name => byName[name]).ToList();
                if (page.Count == 0) continue;
                pages.Add(page);
                used.UnionWith(page);
            }
            foreach (Sheet rest in sheets.Where(sheet => !used.Contains(sheet)))
            {
                var page = pages.FirstOrDefault(p => p.Count < SheetsPerPage);
                if (page == null) pages.Add(page = new List<Sheet>());
                page.Add(rest);
            }
            foreach (List<Sheet> page in pages)
                for (int slot = 0; slot < page.Count; slot++)
                {
                    page[slot].OffsetX = (slot % 2) * Slot;
                    page[slot].OffsetY = (slot / 2) * Slot;
                }
            return pages;
        }

        private static string ValidateFormats(List<Sheet> sheets)
        {
            foreach (List<Sheet> page in AssignPages(sheets))
            {
                Sheet first = page[0];
                if (page.Any(s => s.Color.graphicsFormat != first.Color.graphicsFormat || s.Mask.graphicsFormat != first.Mask.graphicsFormat))
                    return "sheet formats differ on the page of " + first.Color.name;
                if (page.Any(s => s.Color.mipmapCount != 1 || s.Mask.mipmapCount != 1))
                    return "a TeamColour sheet or mask has mipmaps";
                if (!SystemInfo.IsFormatSupported(first.Color.graphicsFormat, FormatUsage.Sample) ||
                    !SystemInfo.IsFormatSupported(first.Mask.graphicsFormat, FormatUsage.Sample))
                    return "page texture format is not supported";
            }
            return null;
        }

        private static void BuildPages(LoaderFields fields, List<Sheet> sheets, Build build)
        {
            build.Pages.AddRange(AssignPages(sheets));
            for (int p = 0; p < build.Pages.Count; p++)
            {
                List<Sheet> page = build.Pages[p];
                Sheet first = page[0];
                var colorPage = new Texture2D(Page, Page, first.Color.graphicsFormat, TextureCreationFlags.None)
                {
                    name = "BugfixesAndQoL_SpritePage" + p, filterMode = first.Color.filterMode,
                    wrapMode = first.Color.wrapMode, anisoLevel = first.Color.anisoLevel
                };
                build.Created.Add(colorPage);
                var maskPage = new Texture2D(Page, Page, first.Mask.graphicsFormat, TextureCreationFlags.None)
                {
                    name = "BugfixesAndQoL_SpritePageMask" + p, filterMode = first.Mask.filterMode,
                    wrapMode = first.Mask.wrapMode, anisoLevel = first.Mask.anisoLevel
                };
                build.Created.Add(maskPage);
                maskPage.Apply(false, true);
                // One material per cutoff stage, cloned from the first sheet (same shader and cutoff values).
                var set = new Material[CutoffStages];
                for (int stage = 0; stage < CutoffStages; stage++)
                {
                    set[stage] = new Material(first.Materials[stage]) { name = "BugfixesAndQoL_SpritePage" + p + "_" + stage };
                    build.Created.Add(set[stage]);
                    set[stage].SetTexture(MaskProperty, maskPage);
                }
                foreach (Sheet sheet in page)
                {
                    for (int stage = 0; stage < CutoffStages; stage++) build.Materials[sheet.Materials[stage]] = set[stage];
                    foreach (int file in sheet.Files)
                    {
                        build.FileMaterials[file] = set;
                        CreatePageSprites(fields.Main[file], sheet, colorPage, build);
                        if (file < fields.Alt.Length) CreatePageSprites(fields.Alt[file], sheet, colorPage, build);
                    }
                }
                // Order verified in-game: the sprites are created while the page is still CPU-readable
                // (its content is irrelevant), then the page is uploaded without a CPU copy and the
                // sheets are copied on the GPU.
                colorPage.Apply(false, true);
                foreach (Sheet sheet in page)
                {
                    Graphics.CopyTexture(sheet.Color, 0, 0, 0, 0, sheet.Color.width, sheet.Color.height, colorPage, 0, 0, sheet.OffsetX, sheet.OffsetY);
                    Graphics.CopyTexture(sheet.Mask, 0, 0, 0, 0, sheet.Mask.width, sheet.Mask.height, maskPage, 0, 0, sheet.OffsetX, sheet.OffsetY);
                }
            }
        }

        private static void CreatePageSprites(Sprite[] array, Sheet sheet, Texture2D page, Build build)
        {
            if (array == null) return;
            foreach (Sprite original in array)
            {
                if (original == null || original.texture != sheet.Color || build.Sprites.ContainsKey(original)) continue;
                Rect rect = original.rect;
                Vector2 pivot = original.pivot;
                Sprite moved = Sprite.Create(page, new Rect(rect.x + sheet.OffsetX, rect.y + sheet.OffsetY, rect.width, rect.height),
                    new Vector2(pivot.x / rect.width, pivot.y / rect.height), original.pixelsPerUnit, 0,
                    SpriteMeshType.FullRect, original.border, false);
                build.Created.Add(moved);
                moved.name = original.name;
                // Sprite.OverrideGeometry is refused at runtime for these sprites, so the original tight
                // mesh is written through the sprite data access API: positions unchanged (local units),
                // UVs moved into the page slot.
                Vector2[] vertices = original.vertices;
                Vector2[] uvs = original.uv;
                ushort[] triangles = original.triangles;
                var positions = new NativeArray<Vector3>(vertices.Length, Allocator.Temp);
                var pageUvs = new NativeArray<Vector2>(vertices.Length, Allocator.Temp);
                var indices = new NativeArray<ushort>(triangles, Allocator.Temp);
                try
                {
                    float sheetWidth = sheet.Color.width, sheetHeight = sheet.Color.height;
                    for (int v = 0; v < vertices.Length; v++)
                    {
                        positions[v] = vertices[v];
                        pageUvs[v] = new Vector2((uvs[v].x * sheetWidth + sheet.OffsetX) / Page, (uvs[v].y * sheetHeight + sheet.OffsetY) / Page);
                    }
                    moved.SetVertexCount(vertices.Length);
                    moved.SetVertexAttribute(VertexAttribute.Position, positions);
                    moved.SetVertexAttribute(VertexAttribute.TexCoord0, pageUvs);
                    moved.SetIndices(indices);
                }
                finally
                {
                    positions.Dispose();
                    pageUvs.Dispose();
                    indices.Dispose();
                }
                if (moved.vertices.Length != vertices.Length || moved.triangles.Length != triangles.Length)
                    throw new InvalidOperationException("geometry of sprite " + original.name + " could not be reproduced");
                build.Sprites[original] = moved;
            }
        }

        // Plain reference assignments only; everything that can fail happened in BuildPages.
        private static void Commit(LoaderFields fields, Build build)
        {
            foreach (KeyValuePair<int, Material[]> entry in build.FileMaterials)
            {
                fields.Materials[entry.Key] = entry.Value;
                Replace(fields.Main[entry.Key], build);
                if (entry.Key < fields.Alt.Length) Replace(fields.Alt[entry.Key], build);
            }
            foreach (string key in fields.All.Keys.ToList())
                if (fields.All[key] != null && build.Sprites.TryGetValue(fields.All[key], out Sprite moved))
                    fields.All[key] = moved;
        }

        private static void Replace(Sprite[] array, Build build)
        {
            if (array == null) return;
            for (int i = 0; i < array.Length; i++)
                if (array[i] != null && build.Sprites.TryGetValue(array[i], out Sprite moved)) array[i] = moved;
        }

        // Usually none exist in the main menu. The page mask only fits the page sprite (same UVs), so a
        // renderer's sprite and material move together.
        private static int SwapLiveRenderers(Build build)
        {
            int swapped = 0;
            foreach (SpriteRenderer renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                Sprite sprite = renderer.sprite;
                Material material = renderer.sharedMaterial;
                if (sprite == null || material == null || !build.Sprites.TryGetValue(sprite, out Sprite moved) ||
                    !build.Materials.TryGetValue(material, out Material mapped))
                    continue;
                renderer.sprite = moved;
                renderer.sharedMaterial = mapped;
                swapped++;
            }
            return swapped;
        }
    }
}
