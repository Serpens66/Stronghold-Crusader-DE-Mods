using System;
using System.Reflection;
using CrusaderDE;
using HarmonyLib;
using UnityEngine;

namespace PerfFixTest
{
    // V1: MainViewModel.TestRadarMap wraps the same radarTexture in a new Noesis TextureSource on
    // every simulation tick and re-binds RadarMapImage. GameMap.ApplyRadarMap updates that texture
    // in place via SetPixelData/Apply, so while the texture instance and the bound image are
    // unchanged the re-bind is skipped. A new texture instance (map load, rotation, resolution)
    // binds normally.
    internal static class RadarRebindFix
    {
        private static Texture2D boundTexture;
        private static object boundImage;
        private static long skipped, bound;
        private static int skipLogged;

        internal static void Install()
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(MainViewModel), nameof(MainViewModel.TestRadarMap), Type.EmptyTypes);
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("MainViewModel.TestRadarMap signature differs from the audit.");
                PerfFixTestPlugin.Harmony.Patch(target, prefix: new HarmonyMethod(typeof(RadarRebindFix), nameof(Prefix)),
                    postfix: new HarmonyMethod(typeof(RadarRebindFix), nameof(Postfix)));
                PerfFixTestPlugin.Note("V1 RadarRebindSkip installed on MainViewModel.TestRadarMap.");
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("V1 RadarRebindSkip disabled: " + exception.Message); }
        }

        private static bool Prefix(MainViewModel __instance, out bool __state)
        {
            __state = false;
            try
            {
                Texture2D texture = GameMap.instance != null ? GameMap.instance.getRadarTexture() : null;
                object image = __instance.RadarMapImage;
                if (texture == null || image == null || !ReferenceEquals(texture, boundTexture) || !ReferenceEquals(image, boundImage))
                    return true;
                __state = true;
                skipped++;
                if (skipLogged == 0)
                {
                    skipLogged = 1;
                    PerfFixTestPlugin.Note("V1 confirmed: first minimap re-bind skipped (same texture and bound image).");
                }
                return false;
            }
            catch (Exception exception)
            {
                PerfFixTestPlugin.Fail("V1 prefix failed; Vanilla runs: " + exception.Message);
                return true;
            }
        }

        private static void Postfix(MainViewModel __instance, bool __state)
        {
            if (__state) return;
            try
            {
                boundTexture = GameMap.instance != null ? GameMap.instance.getRadarTexture() : null;
                boundImage = __instance.RadarMapImage;
                bound++;
                // Logged for the first binds and each new texture instance only (map load etc.).
                if (bound <= 3 || bound % 1000 == 0)
                    PerfFixTestPlugin.Note("V1 bind #" + bound + " (Vanilla ran); skipped so far=" + skipped + ".");
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("V1 postfix failed: " + exception.Message); }
        }
    }
}
