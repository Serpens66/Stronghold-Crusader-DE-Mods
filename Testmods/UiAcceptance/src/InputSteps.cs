using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HarmonyLib;
using SHCDESE.API;
using UnityEngine;

namespace UiAcceptance
{
    // Synthetic input without the real mouse: Noesis hit-test clicks/typing, an engine-boundary
    // map click and (only for the focus test) Win32 minimize/restore of the game window.
    internal static class InputSteps
    {
        internal static void Register()
        {
            Runner.Register("NoesisClick", NoesisClick);
            Runner.Register("NoesisType", NoesisType);
            Runner.Register("MapClick", MapClick);
            Runner.Register("FocusCycle", FocusCycle);
            Runner.Register("Resolution", Resolution);
            Runner.Register("Camera", Camera);
        }

        // The same public engine/map commands Vanilla's rotate and flatten keys use.
        private static IEnumerable<Wait> Camera(Step step)
        {
            switch (step.Arg(0))
            {
                case "rotate-left": GameMap.instance.RotateMapLeft(); break;
                case "rotate-right": GameMap.instance.RotateMapRight(); break;
                case "flatten": EngineInterface.toggleFlattenedLandscapeMode(); break;
                default: throw new StepFailure("Camera expects rotate-left|rotate-right|flatten");
            }
            yield return new Delay(800);
        }

        // NoesisView (Noesis Unity component) exposes the injection methods itself.
        private static NoesisView NoesisView() => FatControler.instance?.NGview ?? throw new StepFailure("no Noesis view");

        // Move, press and release within one frame so the real cursor cannot move the hit point between them.
        private static IEnumerable<Wait> NoesisClick(Step step)
        {
            Noesis.FrameworkElement element = null;
            yield return new Until("element " + step.Arg(0), () => (element = GameXAMLManagerAPI.Instance.FindGlobalElement(step.Arg(0))) != null && element.IsVisible);
            Noesis.Point center = element.PointToScreen(new Noesis.Point((float)element.ActualWidth / 2, (float)element.ActualHeight / 2));
            int x = (int)center.X, y = (int)center.Y;
            NoesisView view = NoesisView();
            view.MouseMove(x, y);
            view.MouseButtonDown(x, y, Noesis.MouseButton.Left);
            view.MouseButtonUp(x, y, Noesis.MouseButton.Left);
            AcceptLog.Info("NOESIS_CLICK " + step.Arg(0) + " at " + x + "," + y);
            yield return new Delay(200);
        }

        private static IEnumerable<Wait> NoesisType(Step step)
        {
            NoesisView view = NoesisView();
            foreach (char c in step.Arg(0)) { view.Char(c); yield return NextFrame.Instance; }
        }

        // One left click on a game tile: preDLLCallActions publishes the tile, getMouseStateForEngine
        // the button state (1 = down, then 3 = up). Priority.First lets Spectator's own postfixes see it.
        private static int clickX = -1, clickY = -1, clickState;
        private static bool patched;
        private static IEnumerable<Wait> MapClick(Step step)
        {
            if (!patched)
            {
                var harmony = UiAcceptancePlugin.Harmony;
                harmony.Patch(AccessTools.Method(typeof(EditorDirector), "preDLLCallActions"),
                    postfix: new HarmonyMethod(typeof(InputSteps), nameof(AfterPreDll)) { priority = Priority.First });
                harmony.Patch(AccessTools.Method(typeof(EditorDirector), "getMouseStateForEngine"),
                    postfix: new HarmonyMethod(typeof(InputSteps), nameof(AfterMouseState)) { priority = Priority.First });
                patched = true;
            }
            clickX = step.Int(0); clickY = step.Int(1);
            clickState = 1;
            yield return new Until("engine read the button-down", () => clickState == 3);
            yield return new Until("engine read the button-up", () => clickState == 0);
            clickX = clickY = -1;
            yield return new Delay(200);
        }

        private static void AfterPreDll(ref int mouseOverX, ref int mouseOverY)
        {
            if (clickX < 0) return;
            mouseOverX = clickX; mouseOverY = clickY;
        }

        private static void AfterMouseState(ref int __result)
        {
            if (clickX < 0 || clickState == 0) return;
            __result = clickState;
            clickState = clickState == 1 ? 3 : 0;
        }

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);

        // The game's own top-level window, independent of which window currently has focus (the user
        // may keep working on the PC). Restoring re-activates the game only if it was in front before.
        private static IEnumerable<Wait> FocusCycle(Step step)
        {
            IntPtr window = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            Runner.Require(window != IntPtr.Zero, "game main window not found");
            bool wasForeground = GetForegroundWindow() == window;
            long from = AcceptLog.Position;
            ShowWindow(window, 6); // SW_MINIMIZE
            yield return new Delay(step.Int(0, 1500));
            ShowWindow(window, wasForeground ? 9 : 4); // SW_RESTORE, else SW_SHOWNOACTIVATE
            yield return new Until("window restored", () => !IsIconic(window));
            yield return new Delay(1500);
            IntPtr after = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            Runner.Require(after == window, "main window changed after the focus cycle");
            int recovery = AcceptLog.Count(from, new System.Text.RegularExpressions.Regex("after two rendered frames"));
            AcceptLog.Info("FOCUS_CYCLE window=0x" + window.ToInt64().ToString("X") + " wasForeground=" + wasForeground +
                " focused=" + Application.isFocused + " recoveryMarkers=" + recovery + " screen=" + Screen.width + "x" + Screen.height);
        }

        // args: width, height, [set|check]. Logs the effective mode and the settings.cfg values; a
        // deviation is a FAIL with details (windowed mode may ignore FullWidth/FullHeight).
        // "cfg" as the only argument expects the FullWidth/FullHeight the runner wrote into settings.cfg.
        private static IEnumerable<Wait> Resolution(Step step)
        {
            int width, height;
            if (step.Arg(0) == "cfg")
            {
                width = height = -1;
                foreach (string line in System.IO.File.ReadAllLines(System.IO.Path.Combine(Application.persistentDataPath, "settings.cfg")))
                {
                    if (line.StartsWith("FullWidth:")) width = int.Parse(line.Substring(10));
                    if (line.StartsWith("FullHeight:")) height = int.Parse(line.Substring(11));
                }
                Runner.Require(width > 0 && height > 0, "settings.cfg has no FullWidth/FullHeight");
            }
            else { width = step.Int(0); height = step.Int(1); }
            if (step.Arg(2, "check") == "set") Screen.SetResolution(width, height, Screen.fullScreenMode);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < 5000 && !(Screen.width == width && Screen.height == height))
                yield return NextFrame.Instance;
            string configured = "unknown";
            try
            {
                string file = System.IO.Path.Combine(Application.persistentDataPath, "settings.cfg");
                var values = new List<string>();
                foreach (string line in System.IO.File.ReadAllLines(file))
                    if (line.StartsWith("FullWidth:") || line.StartsWith("FullHeight:") || line.StartsWith("FullscreenType:")) values.Add(line);
                configured = string.Join(" ", values);
            }
            catch (Exception error) { configured = "unreadable: " + error.Message; }
            string detail = "screen=" + Screen.width + "x" + Screen.height + " mode=" + Screen.fullScreenMode +
                " display=" + Screen.currentResolution.width + "x" + Screen.currentResolution.height + " cfg=" + configured;
            AcceptLog.Info("RESOLUTION expected=" + width + "x" + height + " " + detail);
            Runner.Require(Screen.width == width && Screen.height == height, "resolution deviates: " + detail);
        }
    }
}
