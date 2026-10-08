using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Noesis;
using UnityEngine;

namespace Shared
{
    // Keeps the original Lord artwork pixel-identical except for its red cloth.
    internal static class LordPortraitPalette
    {
        private static readonly Dictionary<int, ImageSource> variants = new Dictionary<int, ImageSource>();
        private static readonly List<Texture2D> textures = new List<Texture2D>();
        private static readonly List<TextureSource> sources = new List<TextureSource>();
        private static readonly UnityEngine.Color32[] colors = {
            new UnityEngine.Color32(0, 0, 0, 0),
            new UnityEngine.Color32(235, 72, 56, 255),
            new UnityEngine.Color32(238, 156, 55, 255),
            new UnityEngine.Color32(237, 212, 72, 255),
            new UnityEngine.Color32(87, 147, 229, 255),
            new UnityEngine.Color32(163, 158, 155, 255),
            new UnityEngine.Color32(171, 105, 203, 255),
            new UnityEngine.Color32(107, 211, 223, 255),
            new UnityEngine.Color32(103, 207, 92, 255)
        };

        internal static ImageSource Get(int colorId)
        {
            ImageSource fallback = GetOriginal();
            if (colorId < 2 || colorId >= colors.Length) return fallback;
            if (variants.TryGetValue(colorId, out ImageSource cached)) return cached;
            try
            {
                string path = System.IO.Path.Combine(Paths.PluginPath, "BugfixesAndQoL_Serp", "Override", "Assets", "GUI", "Sprites", "BugfixesAndQoL-Lord.png");
                if (!File.Exists(path)) return fallback;
                var original = new Texture2D(2, 2, UnityEngine.TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(original, File.ReadAllBytes(path))) return fallback;
                UnityEngine.Color32[] pixels = original.GetPixels32();
                UnityEngine.Color32 target = colors[colorId];
                for (int i = 0; i < pixels.Length; i++)
                {
                    UnityEngine.Color32 pixel = pixels[i];
                    if (pixel.a == 0)
                    {
                        pixel.r = pixel.g = pixel.b = 0;
                        pixels[i] = pixel;
                        continue;
                    }
                    if (pixel.r >= 45 && pixel.r >= pixel.g + 15 && pixel.r >= pixel.b + 12)
                    {
                        int strength = pixel.r;
                        pixel.r = (byte)Math.Min(255, strength * target.r / 235);
                        pixel.g = (byte)Math.Min(255, strength * target.g / 235);
                        pixel.b = (byte)Math.Min(255, strength * target.b / 235);
                    }
                    // Noesis TextureSource expects premultiplied colour channels.
                    if (pixel.a < 255)
                    {
                        pixel.r = (byte)((pixel.r * pixel.a + 127) / 255);
                        pixel.g = (byte)((pixel.g * pixel.a + 127) / 255);
                        pixel.b = (byte)((pixel.b * pixel.a + 127) / 255);
                    }
                    pixels[i] = pixel;
                }
                var recolored = new Texture2D(original.width, original.height, UnityEngine.TextureFormat.RGBA32, false);
                recolored.SetPixels32(pixels);
                recolored.Apply(false, true);
                var source = new TextureSource(recolored);
                var cropped = new CroppedBitmap(source, new Int32Rect(67, 42, 105, 179));
                textures.Add(original);
                textures.Add(recolored);
                sources.Add(source);
                variants.Add(colorId, cropped);
                return cropped;
            }
            catch
            {
                return fallback;
            }
        }

        private static ImageSource GetOriginal()
        {
            try { return GUI.GetApplicationResources()?["BugfixesAndQoL-LordIcon"] as ImageSource; }
            catch { return null; }
        }
    }
}
