using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace PerfFixTest.Automation
{
    // V7 correctness probe: renders sampled sprites once with the original sheet sprite + material
    // and once with the page sprite + page material, for every cutoff stage, off-screen into a
    // pixel-exact render target, and counts differing pixels. Separates real rendering errors of
    // the merge (UV-dependent shader terms such as _SpriteCutoff, mask lookup, filtering at slot
    // borders) from pure draw-order changes between overlapping sprites.
    internal static class SheetMergeRenderCheck
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        internal static readonly Vector3 Origin = new Vector3(-100000f, -100000f, 0f);

        internal static void Run(Action<string> log, int perSheet)
        {
            if (!SheetMerge.Applied || SheetMerge.SheetSets.Count == 0) { log("probe=rendercheck skipped: merge not applied"); return; }
            var host = new GameObject("PerfFixTest_RenderCheck");
            host.transform.position = Origin;
            var renderer = host.AddComponent<SpriteRenderer>();
            // A non-white tint exercises the team-colour mask path of the shader.
            renderer.color = new Color(0.2f, 0.55f, 1f, 1f);
            var command = new CommandBuffer { name = "PerfFixTest_RenderCheck" };
            Texture2D readA = null, readB = null;
            try
            {
                var bySheet = SheetMerge.SpriteMap.GroupBy(pair => pair.Key.texture).ToDictionary(g => g.Key, g => g.ToList());
                int totalTests = 0, totalBad = 0, dumped = 0;
                foreach (SheetMerge.SheetSet set in SheetMerge.SheetSets)
                {
                    if (!bySheet.TryGetValue(set.Sheet, out var pairs)) continue;
                    if (dumped++ < 3) { log("probe=rendercheck material original " + Describe(set.Original[0])); log("probe=rendercheck material page " + Describe(set.Page[0])); }
                    var histogram = new long[5];
                    // Evenly spread sample across the sheet's sprites (deterministic).
                    int step = Math.Max(1, pairs.Count / perSheet);
                    var badPerStage = new int[7];
                    long worstPixels = 0; string worstName = "";
                    int tested = 0;
                    for (int i = 0; i < pairs.Count && tested < perSheet; i += step, tested++)
                    {
                        Sprite original = pairs[i].Key, moved = pairs[i].Value;
                        Rect rect = original.rect;
                        int w = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, 2048), h = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, 2048);
                        var target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                        try
                        {
                            for (int stage = 0; stage < 7; stage++)
                            {
                                Color32[] a = Draw(command, renderer, original, set.Original[stage], target, ref readA);
                                Color32[] b = Draw(command, renderer, moved, set.Page[stage], target, ref readB);
                                long bad = 0;
                                for (int p = 0; p < a.Length; p++)
                                {
                                    int d = Math.Max(Math.Max(Math.Abs(a[p].r - b[p].r), Math.Abs(a[p].g - b[p].g)), Math.Max(Math.Abs(a[p].b - b[p].b), Math.Abs(a[p].a - b[p].a)));
                                    histogram[d == 0 ? 0 : d <= 2 ? 1 : d <= 16 ? 2 : d <= 64 ? 3 : 4]++;
                                    if (d > 2) bad++;
                                }
                                totalTests++;
                                if (bad == 0) continue;
                                badPerStage[stage]++;
                                totalBad++;
                                if (bad > worstPixels)
                                {
                                    worstPixels = bad; worstName = original.name + " stage=" + stage + " px=" + bad + "/" + a.Length;
                                    if (stage == 0) { Save(readA, set.Sheet.name + "_A"); Save(readB, set.Sheet.name + "_B"); }
                                }
                            }
                        }
                        finally { RenderTexture.ReleaseTemporary(target); }
                    }
                    log("probe=rendercheck sheet=" + set.Sheet.name + " sprites=" + tested + " badByStage=" + string.Join(",", badPerStage.Select(n => n.ToString(C))) + " deltaHist(0|1-2|3-16|17-64|65+)=" + string.Join("|", histogram.Select(n => n.ToString(C))) +
                        (worstName.Length > 0 ? " worst=" + worstName : ""));
                }
                log("probe=rendercheck total tests=" + totalTests + " mismatching=" + totalBad);
            }
            catch (Exception exception) { log("probe=rendercheck exception " + exception); }
            finally
            {
                command.Release();
                UnityEngine.Object.Destroy(host);
                if (readA != null) UnityEngine.Object.Destroy(readA);
                if (readB != null) UnityEngine.Object.Destroy(readB);
                RenderTexture.active = null;
            }
        }

        private static string Describe(Material material)
        {
            var parts = new List<string> { material.name, "shader=" + material.shader.name, "queue=" + material.renderQueue, "keywords=" + string.Join("+", material.shaderKeywords) };
            Shader shader = material.shader;
            for (int i = 0; i < shader.GetPropertyCount(); i++)
            {
                string name = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case ShaderPropertyType.Texture:
                        Texture texture = material.GetTexture(name);
                        parts.Add(name + "=" + (texture == null ? "null" : texture.name + "(" + texture.width + "x" + texture.height + "," + texture.filterMode + "," + texture.wrapMode + ")") +
                            " st=" + material.GetTextureScale(name).ToString("F4") + material.GetTextureOffset(name).ToString("F4"));
                        break;
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range: parts.Add(name + "=" + material.GetFloat(name).ToString("0.####", C)); break;
                    default: parts.Add(name + "=" + material.GetVector(name).ToString("F3")); break;
                }
            }
            return string.Join(" ", parts);
        }

        private static void Save(Texture2D texture, string name)
        {
            try
            {
                string dir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "PerfFixTest_shots");
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "rc_" + name + ".png"), texture.EncodeToPNG());
            }
            catch (Exception) { }
        }

        // Orthographic view onto exactly the sprite's rect (1 texel = 1 sprite pixel).
        internal static Color32[] Draw(CommandBuffer command, SpriteRenderer renderer, Sprite sprite, Material material, RenderTexture target, ref Texture2D read)
        {
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            Rect rect = sprite.rect;
            float ppu = sprite.pixelsPerUnit;
            float left = -sprite.pivot.x / ppu, bottom = -sprite.pivot.y / ppu;
            Matrix4x4 view = Matrix4x4.Scale(new Vector3(1f, 1f, -1f)) * Matrix4x4.Translate(-Origin);
            Matrix4x4 projection = Matrix4x4.Ortho(left, left + rect.width / ppu, bottom, bottom + rect.height / ppu, -100f, 100f);
            command.Clear();
            command.SetRenderTarget(target);
            command.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0f));
            command.SetViewProjectionMatrices(view, projection);
            command.DrawRenderer(renderer, material, 0, 0);
            Graphics.ExecuteCommandBuffer(command);
            if (read == null || read.width != target.width || read.height != target.height)
            {
                if (read != null) UnityEngine.Object.Destroy(read);
                read = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            }
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            read.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0, false);
            RenderTexture.active = previous;
            return read.GetPixels32();
        }
    }
}
