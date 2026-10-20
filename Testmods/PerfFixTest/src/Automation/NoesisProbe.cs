using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace PerfFixTest.Automation
{
    // D3D12 investigation: the Noesis HUD bitmaps are missing under -force-d3d12 while text and the
    // runtime minimap texture render. Noesis receives XAML images through NoesisTextureProvider
    // (Noesis.NoesisGUI 3.2.3): UpdateTextures passes GetNativeTexturePtr, size and mip count to the
    // native plugin. This probe lists what is passed (format, mips, native pointer) and can test the
    // hypothesis "mip count" by passing 1 instead (transpiler on UpdateTextures; the Script Extender
    // hooks GetTextureInfo, not UpdateTextures, so the patches do not overlap).
    internal static class NoesisProbe
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;

        internal static void InstallSingleMip(Action<string> log)
        {
            MethodInfo target = AccessTools.Method(typeof(NoesisTextureProvider), nameof(NoesisTextureProvider.UpdateTextures));
            if (target == null) { log("probe=noesis singleMip FAIL target missing"); return; }
            PerfFixTestPlugin.Harmony.Patch(target, transpiler: new HarmonyMethod(typeof(NoesisProbe), nameof(SingleMipTranspiler)));
            log("probe=noesis singleMip installed (patched sites=" + patchedSites + ")");
        }

        private static int patchedSites;

        private static IEnumerable<CodeInstruction> SingleMipTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getMips = AccessTools.PropertyGetter(typeof(Texture2D), nameof(Texture2D.mipmapCount));
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.Calls(getMips))
                {
                    patchedSites++;
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                }
            }
        }

        internal static void Dump(Action<string> log)
        {
            try
            {
                log("probe=noesis graphicsDevice=" + SystemInfo.graphicsDeviceType + " multiThreaded=" + SystemInfo.graphicsMultiThreaded);
                var map = AccessTools.Field(typeof(NoesisTextureProvider), "_textures")?.GetValue(NoesisTextureProvider.instance) as IDictionary;
                if (map == null) { log("probe=noesis provider map unavailable"); return; }
                var rows = new List<string>();
                var groups = new Dictionary<string, int>();
                FieldInfo textureField = null;
                foreach (DictionaryEntry entry in map)
                {
                    object value = entry.Value;
                    if (textureField == null) textureField = value.GetType().GetField("texture");
                    var texture = textureField?.GetValue(value) as Texture;
                    string key = texture == null ? "null" : string.Format(C, "{0} {1} mips={2} readable={3}", texture.GetType().Name, texture.graphicsFormat,
                        texture is Texture2D t2 ? t2.mipmapCount : 1, texture.isReadable);
                    groups[key] = groups.TryGetValue(key, out int n) ? n + 1 : 1;
                    if (rows.Count < 25 && texture != null)
                        rows.Add(string.Format(C, "{0}: {1} {2}x{3} ptr={4}", entry.Key, key, texture.width, texture.height, texture.GetNativeTexturePtr() != IntPtr.Zero));
                }
                log("probe=noesis provider textures=" + map.Count + " groups: " + string.Join(" | ", groups.OrderByDescending(g => g.Value).Select(g => g.Value + "x " + g.Key)));
                foreach (string row in rows) log("probe=noesis texture " + row);
                Texture radar = GameMap.instance != null ? AccessTools.Field(typeof(GameMap), "radarTexture")?.GetValue(GameMap.instance) as Texture : null;
                if (radar != null) log(string.Format(C, "probe=noesis radar {0} mips={1} {2}x{3}", radar.graphicsFormat, radar is Texture2D r2 ? r2.mipmapCount : 1, radar.width, radar.height));
            }
            catch (Exception exception) { log("probe=noesis dump exception " + exception.Message); }
        }
    }
}
