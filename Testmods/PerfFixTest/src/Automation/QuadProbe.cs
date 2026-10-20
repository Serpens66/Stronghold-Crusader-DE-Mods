using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.U2D;

namespace PerfFixTest.Automation
{
    // V11 probe (requires V7): can the tight sprite meshes (~8 vertices) be replaced by the 4-vertex
    // bounding rectangle of the mesh? Per live page sprite, a quad sprite with the same rect, pivot
    // and texture is built and rendered off-screen against the tight original (same material); it is
    // "lossless" when every pixel matches. Then, in the paused scene, three windows are measured:
    // control, all live renderers on quads (upper bound, may show neighbour fragments), only lossless
    // quads (achievable without image change), and a closing control. Renderer sprites are restored
    // exactly. Materials are untouched (same page texture and mask).
    internal static class QuadProbe
    {
        private const double WindowSeconds = 6, SkipSeconds = 1.5;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static readonly string[] Names = { "control-start", "quads-all(upper bound)", "quads-lossless", "control-end" };

        private static bool prepared;
        private static int index = -1;
        private static long windowStamp, lastFrameStamp;
        private static readonly FrameStats frames = new FrameStats();
        private static readonly Dictionary<Sprite, Sprite> quads = new Dictionary<Sprite, Sprite>();
        private static readonly HashSet<Sprite> lossless = new HashSet<Sprite>();
        private static readonly List<KeyValuePair<SpriteRenderer, Sprite>> swapped = new List<KeyValuePair<SpriteRenderer, Sprite>>();
        private static double drawCallSum, vertexSum;
        private static int counterSamples;
        private static ProfilerRecorder drawCalls, vertexCount;

        internal static bool Step(Action<string> log)
        {
            long now = Stopwatch.GetTimestamp();
            if (!prepared)
            {
                prepared = true;
                if (!Prepare(log)) return true;
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                vertexCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
                Begin(0, Stopwatch.GetTimestamp());
                return false;
            }
            double sinceWindow = (now - windowStamp) / (double)Stopwatch.Frequency;
            if (sinceWindow >= SkipSeconds)
            {
                frames.Add((now - lastFrameStamp) * 1000.0 / Stopwatch.Frequency);
                if (drawCalls.Valid) { drawCallSum += drawCalls.LastValue; vertexSum += vertexCount.LastValue; counterSamples++; }
            }
            lastFrameStamp = now;
            if (sinceWindow < WindowSeconds) return false;
            End(log);
            if (index + 1 < Names.Length) { Begin(index + 1, now); return false; }
            drawCalls.Dispose();
            vertexCount.Dispose();
            log("probe=quad done");
            return true;
        }

        private static bool Prepare(Action<string> log)
        {
            if (!SheetMerge.Applied) { log("probe=quad skipped: V7 not applied"); return false; }
            var pageSprites = new HashSet<Sprite>(SheetMerge.SpriteMap.Values);
            var materialOf = new Dictionary<Sprite, Material>();
            int liveRenderers = 0;
            foreach (SpriteRenderer renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                Sprite sprite = renderer.sprite;
                if (sprite == null || !pageSprites.Contains(sprite)) continue;
                liveRenderers++;
                if (!materialOf.ContainsKey(sprite)) materialOf[sprite] = renderer.sharedMaterial;
            }
            long started = Stopwatch.GetTimestamp();
            int alreadyQuad = 0;
            foreach (Sprite sprite in materialOf.Keys)
            {
                Sprite quad = BuildQuad(sprite);
                if (quad == null) { alreadyQuad++; continue; }
                quads[sprite] = quad;
            }
            double buildMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

            // Lossless check: tight vs quad, same material (the renderer's), tinted like a team colour.
            var host = new GameObject("PerfFixTest_QuadCheck");
            host.transform.position = SheetMergeRenderCheck.Origin;
            var probeRenderer = host.AddComponent<SpriteRenderer>();
            probeRenderer.color = new Color(0.2f, 0.55f, 1f, 1f);
            var command = new CommandBuffer { name = "PerfFixTest_QuadCheck" };
            Texture2D readA = null, readB = null;
            long tightVertices = 0, quadVertices = 0;
            started = Stopwatch.GetTimestamp();
            try
            {
                foreach (var pair in quads)
                {
                    Sprite tight = pair.Key, quad = pair.Value;
                    Rect rect = tight.rect;
                    int w = Mathf.Clamp(Mathf.RoundToInt(rect.width), 1, 2048), h = Mathf.Clamp(Mathf.RoundToInt(rect.height), 1, 2048);
                    var target = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    try
                    {
                        Material material = materialOf[tight];
                        Color32[] a = SheetMergeRenderCheck.Draw(command, probeRenderer, tight, material, target, ref readA);
                        Color32[] b = SheetMergeRenderCheck.Draw(command, probeRenderer, quad, material, target, ref readB);
                        bool same = true;
                        for (int p = 0; p < a.Length && same; p++)
                            if (Math.Abs(a[p].r - b[p].r) > 2 || Math.Abs(a[p].g - b[p].g) > 2 || Math.Abs(a[p].b - b[p].b) > 2 || Math.Abs(a[p].a - b[p].a) > 2) same = false;
                        if (same) { lossless.Add(tight); tightVertices += tight.vertices.Length; quadVertices += 4; }
                    }
                    finally { RenderTexture.ReleaseTemporary(target); }
                }
            }
            finally
            {
                command.Release();
                UnityEngine.Object.Destroy(host);
                if (readA != null) UnityEngine.Object.Destroy(readA);
                if (readB != null) UnityEngine.Object.Destroy(readB);
            }
            double checkMs = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
            int liveLossless = Resources.FindObjectsOfTypeAll<SpriteRenderer>().Count(r => r.sprite != null && lossless.Contains(r.sprite));
            log(string.Format(C, "probe=quad prepared liveRenderers={0} liveSprites={1} quads={2} alreadyQuad={3} lossless={4} ({5:0.0}%) liveRenderersLossless={6} ({7:0.0}%) " +
                "losslessVertices {8}->{9} buildMs={10:0} checkMs={11:0}", liveRenderers, materialOf.Count, quads.Count, alreadyQuad, lossless.Count,
                quads.Count == 0 ? 0 : lossless.Count * 100.0 / quads.Count, liveLossless, liveRenderers == 0 ? 0 : liveLossless * 100.0 / liveRenderers,
                tightVertices, quadVertices, buildMs, checkMs));
            return quads.Count > 0;
        }

        // Bounding rectangle of the tight mesh as a 4-vertex sprite on the same texture; null if the
        // sprite has 4 vertices already.
        private static Sprite BuildQuad(Sprite tight)
        {
            Vector2[] vertices = tight.vertices;
            if (vertices.Length <= 4) return null;
            float minX = vertices.Min(v => v.x), maxX = vertices.Max(v => v.x), minY = vertices.Min(v => v.y), maxY = vertices.Max(v => v.y);
            Rect rect = tight.rect;
            Vector2 pivot = tight.pivot;
            float ppu = tight.pixelsPerUnit;
            Texture2D texture = tight.texture;
            Sprite quad = Sprite.Create(texture, rect, new Vector2(pivot.x / rect.width, pivot.y / rect.height), ppu, 0, SpriteMeshType.FullRect, tight.border, false);
            quad.name = tight.name + "_quad";
            var corners = new[] { new Vector2(minX, minY), new Vector2(minX, maxY), new Vector2(maxX, maxY), new Vector2(maxX, minY) };
            var positions = new NativeArray<Vector3>(4, Allocator.Temp);
            var uvs = new NativeArray<Vector2>(4, Allocator.Temp);
            for (int i = 0; i < 4; i++)
            {
                positions[i] = corners[i];
                uvs[i] = new Vector2((rect.x + corners[i].x * ppu + pivot.x) / texture.width, (rect.y + corners[i].y * ppu + pivot.y) / texture.height);
            }
            var indices = new NativeArray<ushort>(new ushort[] { 0, 1, 2, 2, 3, 0 }, Allocator.Temp);
            quad.SetVertexCount(4);
            quad.SetVertexAttribute(VertexAttribute.Position, positions);
            quad.SetVertexAttribute(VertexAttribute.TexCoord0, uvs);
            quad.SetIndices(indices);
            positions.Dispose(); uvs.Dispose(); indices.Dispose();
            return quad;
        }

        private static void Begin(int next, long now)
        {
            index = next;
            windowStamp = lastFrameStamp = now;
            frames.Clear();
            drawCallSum = vertexSum = 0;
            counterSamples = 0;
            swapped.Clear();
            if (index != 1 && index != 2) return;
            foreach (SpriteRenderer renderer in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
            {
                Sprite sprite = renderer.sprite;
                if (sprite == null || !quads.TryGetValue(sprite, out Sprite quad)) continue;
                if (index == 2 && !lossless.Contains(sprite)) continue;
                swapped.Add(new KeyValuePair<SpriteRenderer, Sprite>(renderer, sprite));
                renderer.sprite = quad;
            }
        }

        private static void End(Action<string> log)
        {
            foreach (var pair in swapped) if (pair.Key != null) pair.Key.sprite = pair.Value;
            log(string.Format(C, "probe=quad window={0} swapped={1} drawCalls={2:0} vertices={3:0} {4}", Names[index], swapped.Count,
                counterSamples == 0 ? 0 : drawCallSum / counterSamples, counterSamples == 0 ? 0 : vertexSum / counterSamples, frames.Summary()));
            swapped.Clear();
        }
    }
}
