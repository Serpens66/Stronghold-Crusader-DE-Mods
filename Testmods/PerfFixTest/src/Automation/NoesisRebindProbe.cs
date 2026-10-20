using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using HarmonyLib;
using Noesis;
using UnityTexture2D = UnityEngine.Texture2D;

namespace PerfFixTest.Automation
{
    // D3D12 HUD feasibility probe: XAML bitmaps reach Noesis through NoesisTextureProvider
    // (native StoreTextureInfo path), which draws nothing under D3D12, while TextureSource
    // (Noesis_WrapTexture) works (minimap). In the paused scene this walks every NoesisView's visual
    // tree and replaces BitmapImage sources of Image elements and ImageBrush backgrounds by a
    // TextureSource around the same Unity texture from the provider map. Screenshots before/after show
    // whether the workaround path renders. Measurement only; the scene is not restored.
    internal static class NoesisRebindProbe
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static long started;
        private static bool applied;

        internal static bool Step(Action<string> log)
        {
            if (started == 0) started = Stopwatch.GetTimestamp();
            if (!applied) { applied = true; Rebind(log); }
            // Leave a few seconds for Noesis to re-render before the "after" screenshot.
            return (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency >= 4;
        }

        private static void Rebind(Action<string> log)
        {
            var map = AccessTools.Field(typeof(NoesisTextureProvider), "_textures")?.GetValue(NoesisTextureProvider.instance) as IDictionary;
            if (map == null) { log("probe=rebind provider map unavailable"); return; }
            var textures = new Dictionary<string, UnityTexture2D>(StringComparer.OrdinalIgnoreCase);
            foreach (DictionaryEntry entry in map)
            {
                var texture = entry.Value.GetType().GetField("texture")?.GetValue(entry.Value) as UnityTexture2D;
                if (texture != null) textures[(string)entry.Key] = texture;
            }
            var counts = new Dictionary<string, int>();
            void Count(string key) => counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            var cache = new Dictionary<string, TextureSource>(StringComparer.OrdinalIgnoreCase);
            ImageSource Replacement(ImageSource source)
            {
                if (!(source is BitmapImage bitmap) || bitmap.UriSource == null) return null;
                string key = bitmap.UriSource.OriginalString.TrimStart('/');
                if (!textures.TryGetValue(key, out UnityTexture2D texture)) { Count("uriWithoutTexture"); return null; }
                if (!cache.TryGetValue(key, out TextureSource wrapped)) cache[key] = wrapped = new TextureSource(texture);
                return wrapped;
            }
            void Visit(DependencyObject node, int depth)
            {
                if (node == null || depth > 200) return;
                Count("visited");
                try
                {
                    if (node is Image image)
                    {
                        ImageSource replacement = Replacement(image.Source);
                        if (replacement != null) { image.Source = replacement; Count("image"); }
                    }
                    Brush background = node is Panel panel ? panel.Background : node is Border border ? border.Background : node is Control control ? control.Background : null;
                    if (background is ImageBrush brush)
                    {
                        ImageSource replacement = Replacement(brush.ImageSource);
                        if (replacement != null)
                        {
                            var copy = new ImageBrush(replacement) { Stretch = brush.Stretch, Viewbox = brush.Viewbox, ViewboxUnits = brush.ViewboxUnits, Opacity = brush.Opacity };
                            if (node is Panel p) p.Background = copy; else if (node is Border b) b.Background = copy; else ((Control)node).Background = copy;
                            Count("imageBrush");
                        }
                    }
                }
                catch (Exception exception) { Count("error:" + exception.GetType().Name); }
                int children = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < children; i++) Visit(VisualTreeHelper.GetChild(node, i), depth + 1);
            }
            int views = 0;
            foreach (NoesisView view in UnityEngine.Object.FindObjectsOfType<NoesisView>())
            {
                views++;
                Visit(view.Content, 0);
            }
            var parts = new List<string>();
            foreach (var pair in counts) parts.Add(pair.Key + "=" + pair.Value.ToString(C));
            log("probe=rebind views=" + views + " providerTextures=" + textures.Count + " wrapped=" + cache.Count + " " + string.Join(" ", parts));
        }
    }
}
