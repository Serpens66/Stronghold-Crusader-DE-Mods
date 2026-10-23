using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using System;
using System.Reflection;
using System.Threading;
using UnityEngine;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V1: GameMap.ApplyRadarMap writes the new minimap pixels into the same radarTexture
    // (SetPixelData/Apply) and then calls MainViewModel.TestRadarMap on every simulation tick, which
    // wraps that texture in a new Noesis TextureSource and re-binds RadarMapImage (0.9-3.8 ms each).
    // TestRadarMap has no other side effect (radarTexture2D receives the same instance), so the call
    // is skipped while the texture instance and the bound image are the ones of the last Vanilla bind.
    // A new texture instance (map creation, radar resize) or a foreign RadarMapImage binds normally.
    internal static class RadarRebindOptimization
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.VanillaPerformance.Radar");
        private static ManualLogSource log;
        private static Texture2D boundTexture;
        private static object boundImage;
        private static int enabled, failed, skipLogged;

        internal static void Install(ManualLogSource logger)
        {
            log = logger;
            try
            {
                MethodInfo target = typeof(MainViewModel).GetMethod(nameof(MainViewModel.TestRadarMap),
                    BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("MainViewModel.TestRadarMap signature differs from the audit.");
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(RadarRebindOptimization), nameof(BeforeTestRadarMap)),
                    postfix: new HarmonyMethod(typeof(RadarRebindOptimization), nameof(AfterTestRadarMap)));
                Shared.DebugLogHelper.LogInfo(log, "VANILLA_PERF_RADAR_INSTALLED: permanent MainViewModel.TestRadarMap prefix/postfix.");
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        internal static void SetEnabled(bool value) =>
            Volatile.Write(ref enabled, value && Volatile.Read(ref failed) == 0 ? 1 : 0);

        // Main thread (Director.Update -> GameMap.ApplyRadarMap, EditorDirector).
        private static bool BeforeTestRadarMap(MainViewModel __instance, out bool __state)
        {
            __state = false;
            if (Volatile.Read(ref enabled) == 0) return true;
            try
            {
                GameMap map = GameMap.instance;
                Texture2D texture = map != null ? map.getRadarTexture() : null;
                object image = __instance.RadarMapImage;
                if (texture == null || image == null ||
                    !ReferenceEquals(texture, boundTexture) || !ReferenceEquals(image, boundImage))
                    return true;
                __state = true;
                if (skipLogged == 0)
                {
                    skipLogged = 1;
                    Shared.DebugLogHelper.LogInfo(log,
                        "VANILLA_PERF_RADAR_CONFIRMED: first minimap re-bind skipped (same texture and bound image).");
                }
                return false;
            }
            catch (Exception exception)
            {
                FailOpen(exception);
                return true;
            }
        }

        // Records every Vanilla bind, also while disabled, so a later activation compares current state.
        private static void AfterTestRadarMap(MainViewModel __instance, bool __state)
        {
            if (__state) return;
            try
            {
                GameMap map = GameMap.instance;
                boundTexture = map != null ? map.getRadarTexture() : null;
                boundImage = __instance.RadarMapImage;
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        private static void FailOpen(Exception exception)
        {
            Volatile.Write(ref enabled, 0);
            boundTexture = null;
            boundImage = null;
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/VanillaPerformance/RadarRebindOptimization.cs:94",
                    "VANILLA_PERF_RADAR_DISABLED: Vanilla minimap binding retained; " + exception);
        }
    }
}
