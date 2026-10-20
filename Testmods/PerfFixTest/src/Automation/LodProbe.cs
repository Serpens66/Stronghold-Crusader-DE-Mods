using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Unity.Profiling;
using UnityEngine;

namespace PerfFixTest.Automation
{
    // LOD potential probe (measurement only, paused scene): per detail category, census of the live
    // renderers (enabled, visible, mesh vertices), then each category is hidden for one window
    // (renderer.enabled = false) and exactly those renderers are restored afterwards. Frame stats,
    // draw calls and vertices per window give the upper bound of what an LOD for that category
    // could gain. Control windows at start and end check that the scene returned to its baseline.
    // Categories come from Vanilla's GameMap object dictionaries (Assembly-CSharp BC8B6A39), not from
    // sort offsets, so each renderer is attributed to its owning object and part.
    internal static class LodProbe
    {
        private const double WindowSeconds = 5, SkipSeconds = 1.5;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        private sealed class Window
        {
            public string Name;
            public Func<IEnumerable<SpriteRenderer>> Renderers;
        }

        private static List<Window> windows;
        private static int index = -1;
        private static long windowStamp, lastFrameStamp;
        private static readonly FrameStats frames = new FrameStats();
        private static readonly List<SpriteRenderer> hidden = new List<SpriteRenderer>();
        private static double drawCallSum, vertexSum;
        private static int counterSamples;
        private static ProfilerRecorder drawCalls, vertexCount;

        // Called once per frame during the paused hold; returns true when all windows are done.
        internal static bool Step(Action<string> log)
        {
            long now = Stopwatch.GetTimestamp();
            if (windows == null)
            {
                windows = BuildWindows(log);
                if (windows == null) return true;
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                vertexCount = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
                Census(log);
                Begin(0, now, log);
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
            if (index + 1 < windows.Count) { Begin(index + 1, now, log); return false; }
            drawCalls.Dispose();
            vertexCount.Dispose();
            log("probe=lod done");
            return true;
        }

        private static void Begin(int next, long now, Action<string> log)
        {
            index = next;
            windowStamp = lastFrameStamp = now;
            frames.Clear();
            drawCallSum = vertexSum = 0;
            counterSamples = 0;
            hidden.Clear();
            Window window = windows[index];
            if (window.Renderers == null) return;
            foreach (SpriteRenderer renderer in window.Renderers())
            {
                if (renderer == null || !renderer.enabled) continue;
                renderer.enabled = false;
                hidden.Add(renderer);
            }
        }

        private static void End(Action<string> log)
        {
            foreach (SpriteRenderer renderer in hidden) if (renderer != null) renderer.enabled = true;
            log(string.Format(C, "probe=lod window={0} hidden={1} drawCalls={2:0} vertices={3:0} {4}", windows[index].Name, hidden.Count,
                counterSamples == 0 ? 0 : drawCallSum / counterSamples, counterSamples == 0 ? 0 : vertexSum / counterSamples, frames.Summary()));
            hidden.Clear();
        }

        private static IEnumerable<T> Values<T>(string field)
        {
            object map = AccessTools.Field(typeof(GameMap), field)?.GetValue(GameMap.instance);
            return map is IDictionary dictionary ? dictionary.Values.OfType<T>().ToList() : new List<T>();
        }

        private static List<Window> BuildWindows(Action<string> log)
        {
            if (GameMap.instance == null) { log("probe=lod skipped: no GameMap"); return null; }
            List<Chimp> chimps = Values<Chimp>("chimps").ToList();
            Window W(string name, Func<IEnumerable<SpriteRenderer>> renderers) => new Window { Name = name, Renderers = renderers };
            return new List<Window>
            {
                W("control-start", null),
                W("unitBody(reference)", () => chimps.Select(c => c.sprRenderer)),
                W("unitPart2", () => chimps.Select(c => c.sprRenderer2)),
                W("unitPart3", () => chimps.Select(c => c.sprRenderer3)),
                W("siegeFlag", () => chimps.Select(c => c.sprRenderer4)),
                W("healthBars", () => chimps.SelectMany(c => new[] { c.sprRenderer5, c.sprRenderer6 })),
                W("targetMarker", () => chimps.Select(c => c.sprRenderer7)),
                W("flies", () => Values<Fly>("flies").SelectMany(f => new[] { f.sprRenderer, f.sprRenderer2, f.sprRendererDigit1, f.sprRendererDigit2, f.sprRendererDigit3, f.sprRendererDigit4, f.sprRendererDigit5 })),
                W("pixies", () => Values<Pixie>("pixies").Select(p => p.sprRenderer)),
                W("orgs", () => Values<Org>("orgs").Select(o => o.sprRenderer)),
                W("buildingAnims", () => Values<BuildingAnim>("buildingAnims").Select(b => b.sprRenderer)),
                W("wallFillins", () => Values<WallFillin>("wallFillins").Select(w => w.sprRenderer)),
                W("control-end", null),
            };
        }

        // Live renderers per category: enabled, visible to a camera, and their mesh vertices.
        private static void Census(Action<string> log)
        {
            int totalEnabled = 0, totalVisible = 0;
            foreach (Window window in windows.Where(w => w.Renderers != null))
            {
                int enabled = 0, visible = 0;
                long vertices = 0;
                foreach (SpriteRenderer renderer in window.Renderers())
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    enabled++;
                    if (!renderer.isVisible) continue;
                    visible++;
                    if (renderer.sprite != null) vertices += renderer.sprite.vertices.Length;
                }
                if (!window.Name.StartsWith("unitBody", StringComparison.Ordinal)) { totalEnabled += enabled; totalVisible += visible; }
                log(string.Format(C, "probe=lod census category={0} enabled={1} visible={2} visibleVertices={3}", window.Name, enabled, visible, vertices));
            }
            int allVisible = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>().Count(r => r.enabled && r.isVisible);
            log(string.Format(C, "probe=lod census allVisibleSpriteRenderers={0} (non-body categories visible={1})", allVisible, totalVisible));
        }
    }
}
