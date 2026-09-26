using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace KeepCampfireGroundPreserveTest
{
    // Common file/image-ID catalog. More audited structures can supply
    // foreground masks without a new placement algorithm.
    internal sealed class MaskedSpriteCatalog
    {
        private const int Width = 64, Height = 32;
        private static readonly int[] CampImages = { 36, 41, 42, 47, 48, 49, 54, 55, 60 };
        private readonly Action<string> log, error;
        private readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();
        private bool attempted, available;

        internal MaskedSpriteCatalog(Action<string> log, Action<string> error)
        { this.log = log; this.error = error; }

        internal bool TryGet(int graphic, out Sprite sprite) =>
            sprites.TryGetValue(graphic, out sprite);

        internal bool Prepare()
        {
            if (attempted) return available;
            if (spriteLoader.instance == null) return false;
            attempted = true;
            try
            {
                string root = Path.Combine(Paths.PluginPath,
                    KeepCampfireGroundPreserveTestPlugin.Guid);
                foreach (int image in CampImages)
                {
                    string name = "tile_buildings1 " + image.ToString("000") + ".png";
                    Texture2D source = Load(Path.Combine(root, "fire-source", name));
                    Texture2D mask = Load(Path.Combine(root, "fire-mask", name));
                    Color32[] pixels = source.GetPixels32();
                    Color32[] alpha = mask.GetPixels32();
                    int kept = 0;
                    for (int p = 0; p < pixels.Length; p++) {
                        pixels[p].a = (byte)(pixels[p].a * alpha[p].a / 255);
                        if (pixels[p].a != 0) kept++;
                    }
                    if (kept == 0)
                        throw new InvalidOperationException("Empty foreground mask: " + name);
                    source.SetPixels32(pixels);
                    source.Apply(false, true);
                    source.filterMode = FilterMode.Point;
                    source.wrapMode = TextureWrapMode.Clamp;
                    Sprite vanilla = spriteLoader.instance.GetGMSprite(
                        Enums.GM.GM_BUILDINGS1, image);
                    if (vanilla == null || vanilla.rect.width != Width ||
                        vanilla.rect.height != Height)
                        throw new InvalidOperationException("Vanilla sprite metadata differs: " + name);
                    var pivot = new Vector2(vanilla.pivot.x / Width, vanilla.pivot.y / Height);
                    Sprite sprite = Sprite.Create(source, new Rect(0, 0, Width, Height),
                        pivot, vanilla.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = "MaskedForeground_6_" + image;
                    sprites.Add((6 << 16) | image, sprite);
                    UnityEngine.Object.Destroy(mask);
                }
                available = sprites.Count == CampImages.Length;
                log("foreground mask catalog ready: file=6 images=" + sprites.Count);
            }
            catch (Exception ex) { error("foreground masks unavailable: " + ex); }
            return available;
        }

        private static Texture2D Load(string path)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)) ||
                texture.width != Width || texture.height != Height)
                throw new InvalidOperationException("Unexpected image dimensions: " + path);
            return texture;
        }
    }
}
