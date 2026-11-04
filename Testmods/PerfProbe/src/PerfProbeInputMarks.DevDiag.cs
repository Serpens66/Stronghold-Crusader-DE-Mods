using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using BepInEx.Configuration;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using UnityEngine;

namespace PerfProbe
{
    // Time markers for manual runs: "lag after a command" needs the command's time. The Extender's
    // key event covers every KeyCode including mouse buttons (Vanilla KeyManager scans all of them),
    // so clicks and an explicit marker key are logged without polling. The offline analysis links
    // each slow event to the latest preceding marker. Rooted by the static subscription field.
    internal static class PerfProbeInputMarks
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static KeyCode[] keys = { KeyCode.Mouse0, KeyCode.Mouse1, KeyCode.ScrollLock };
        private static IDisposable subscription;
        private static bool attempted;
        private static long sequence;

        internal static string Configuration => keys.Length == 0 ? "off" : string.Join("+", Array.ConvertAll(keys, key => key.ToString()));

        internal static void BindConfig(ConfigFile config)
        {
            string text = config.Bind("Markers", "Keys", "Mouse0,Mouse1,ScrollLock",
                "Keys/mouse buttons (Unity KeyCode names, comma separated) written as inputMark lines, so slow events can be " +
                "linked to the preceding click or command. Mouse1 = right click (move order); ScrollLock = manual marker. Empty disables.").Value;
            var parsed = new List<KeyCode>();
            foreach (string part in (text ?? "").Split(','))
                if (Enum.TryParse(part.Trim(), true, out KeyCode key) && !parsed.Contains(key)) parsed.Add(key);
            keys = parsed.ToArray();
        }

        // Main thread, from the static render callback after plugin startup (Extender hooks exist).
        internal static void Subscribe()
        {
            if (attempted) return;
            attempted = true;
            if (keys.Length == 0) return;
            try { subscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown); }
            catch (Exception exception)
            {
                string detail = exception.GetType().Name + " " + exception.Message;
                Shared.DevDiag.Log(() => "inputMarks unavailable: " + detail);
                return;
            }
            Shared.DevDiag.Log(() => "inputMarks ready keys=" + Configuration);
        }

        // Main thread (KeyManager update). Pre phase only; Post repeats the same key press.
        private static void OnKeyDown(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Pre || Array.IndexOf(keys, args.Key) < 0) return;
            long now = Stopwatch.GetTimestamp(), mark = ++sequence;
            string line = string.Format(C, "inputMark key={0} mark={1} stamp={2} frame={3} lastTickAgeMs={4:F1} simRunActive={5}",
                args.Key, mark, now, Time.frameCount, PerfProbeTickMonitor.LastTickAgeMs(now), PerfProbeRuntime.SimTickActive);
            Shared.DevDiag.Log(() => line);
        }
    }
}
