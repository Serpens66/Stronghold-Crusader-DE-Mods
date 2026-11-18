using System;
using System.Globalization;
using BepInEx.Logging;

namespace WorkshopLoadProbe
{
    // Every line carries a millisecond timestamp and a stable marker, so Check-Run.ps1
    // can evaluate a run from LogOutput.log alone.
    internal static class ProbeLog
    {
        private static ManualLogSource log;

        internal static void Initialize()
        {
            try { if (log == null) log = Logger.CreateLogSource("WorkshopLoadProbe"); }
            catch { }
        }

        internal static void Info(string marker, string detail) => Write(LogLevel.Info, marker, detail);
        internal static void Warning(string marker, string detail) => Write(LogLevel.Warning, marker, detail);
        internal static void Error(string marker, string detail) => Write(LogLevel.Error, marker, detail);

        private static void Write(LogLevel level, string marker, string detail)
        {
            try
            {
                Initialize();
                log?.Log(level, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " WLP " + marker + " " + detail);
            }
            catch { }
        }
    }
}
