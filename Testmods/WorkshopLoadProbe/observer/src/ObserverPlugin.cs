using System;
using System.Globalization;
using BepInEx;
using BepInEx.Logging;

namespace WorkshopLoadProbe
{
    // Test helper for unattended game runs: with config quitafter=<seconds> it flushes the
    // BepInEx disk log and quits the game. A static Application.onBeforeRender handler (rooted by
    // Unity, unaffected by the startup cleanup) drives it; BepInEx writes the log on a timer, so
    // an immediate Quit would lose the last lines. No other runtime work.
    [BepInPlugin(Guid, "Workshop Load Probe", "0.2.0")]
    public sealed class ObserverPlugin : BaseUnityPlugin
    {
        public const string Guid = "WorkshopLoadProbe_Serp";
        private static ManualLogSource log;
        private static float quitAt = -1f, flushedAt = -1f;

        private void Awake()
        {
            log = Logger;
            try
            {
                if (!(AppDomain.CurrentDomain.GetData("WorkshopLoadProbe.QuitAfter") is int seconds) || seconds <= 0) return;
                quitAt = UnityEngine.Time.realtimeSinceStartup + seconds;
                UnityEngine.Application.onBeforeRender += QuitTick;
                Mark("QUIT_SCHEDULED", "after=" + seconds + "s");
            }
            catch (Exception ex) { log.LogError(Stamp() + " WLP OBSERVER_ERROR " + ex); }
        }

        private static void QuitTick()
        {
            try
            {
                float now = UnityEngine.Time.realtimeSinceStartup;
                if (quitAt < 0f || now < quitAt) return;
                if (flushedAt < 0f) { Mark("QUIT", "flushing log"); Flush(); flushedAt = now; return; }
                if (now < flushedAt + 2f) return;
                Flush();
                quitAt = -1f;
                UnityEngine.Application.Quit();
            }
            catch (Exception ex) { quitAt = -1f; log?.LogError(Stamp() + " WLP OBSERVER_ERROR quit " + ex.Message); }
        }

        private static void Flush()
        {
            foreach (var listener in BepInEx.Logging.Logger.Listeners)
                if (listener is DiskLogListener disk) disk.LogWriter?.Flush();
        }

        private static void Mark(string marker, string detail) => log?.LogInfo(Stamp() + " WLP " + marker + " " + detail);

        private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
