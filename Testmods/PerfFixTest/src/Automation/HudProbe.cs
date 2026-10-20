using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace PerfFixTest.Automation
{
    // Cost of the Noesis HUD: in the paused scene, control window, then every NoesisView component
    // disabled for one window, then a closing control. Separates the D3D12 frame-rate gain from the
    // fact that the HUD bitmaps are not drawn under D3D12. The views are re-enabled afterwards.
    internal static class HudProbe
    {
        private const double WindowSeconds = 6, SkipSeconds = 1.5;
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static readonly string[] Names = { "control-start", "hud-off", "control-end" };
        private static int index = -1;
        private static long windowStamp, lastFrameStamp;
        private static readonly FrameStats frames = new FrameStats();
        private static NoesisView[] disabled = new NoesisView[0];

        internal static bool Step(Action<string> log)
        {
            long now = Stopwatch.GetTimestamp();
            if (index < 0) { Begin(0, now); return false; }
            double since = (now - windowStamp) / (double)Stopwatch.Frequency;
            if (since >= SkipSeconds) frames.Add((now - lastFrameStamp) * 1000.0 / Stopwatch.Frequency);
            lastFrameStamp = now;
            if (since < WindowSeconds) return false;
            foreach (NoesisView view in disabled) if (view != null) view.enabled = true;
            log(string.Format(C, "probe=hud window={0} viewsDisabled={1} api={2} {3}", Names[index], disabled.Length, SystemInfo.graphicsDeviceType, frames.Summary()));
            disabled = new NoesisView[0];
            if (index + 1 < Names.Length) { Begin(index + 1, now); return false; }
            log("probe=hud done");
            return true;
        }

        private static void Begin(int next, long now)
        {
            index = next;
            windowStamp = lastFrameStamp = now;
            frames.Clear();
            if (Names[index] != "hud-off") return;
            disabled = UnityEngine.Object.FindObjectsOfType<NoesisView>().Where(v => v.enabled).ToArray();
            foreach (NoesisView view in disabled) view.enabled = false;
        }
    }
}
