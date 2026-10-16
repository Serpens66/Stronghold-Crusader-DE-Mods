using System;
using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace DependencyBootstrapProbe
{
    [BepInPlugin("DependencyBootstrapProbe_Serp", "Dependency Bootstrap Probe", "0.1.0")]
    public sealed class ProbePlugin : BaseUnityPlugin
    {
        private static ManualLogSource rootedLog;
        private static ProbePlugin originalComponent;
        private static int lastFrame = -1;
        private static int frames;
        private static bool reported;

        private void Awake()
        {
            rootedLog = Logger;
            string identity = "plugin=" + ProbeIdentity.Hash(typeof(ProbePlugin).Assembly.Location);
            if (System.IO.File.Exists(System.IO.Path.Combine(Paths.ConfigPath, "DependencyBootstrapProbe.disabled")))
            {
                Mark("PLUGIN_DISABLED", identity);
                return;
            }
            originalComponent = this;
            Application.onBeforeRender += Observe;
            Mark("PLUGIN_AWAKE", identity + " static render observer attached");
        }

        private static void Observe()
        {
            if (reported || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            frames++;
            if (frames < 120 || originalComponent != null) return;
            reported = true;
            Mark("POST_CLEANUP_RENDER", "original Unity component destroyed; distinct render frames=" + frames);
        }

        private static void Mark(string marker, string detail)
        {
            ProbeTrace.WriteFile(marker, detail);
            try { rootedLog.LogInfo(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [DBP:" + marker + "] " + detail); }
            catch { }
        }
    }
}
