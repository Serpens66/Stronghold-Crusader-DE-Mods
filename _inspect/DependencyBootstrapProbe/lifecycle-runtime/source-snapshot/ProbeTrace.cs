using System;
using System.Globalization;
using System.IO;
using System.Text;
using BepInEx.Logging;

namespace DependencyBootstrapProbe
{
    internal static class ProbeTrace
    {
        private static readonly object gate = new object();
        private static ManualLogSource log;
        internal static void Initialize()
        {
            WriteFile("SESSION", "id=" + Guid.NewGuid().ToString("N"));
            log = Logger.CreateLogSource("DependencyBootstrapProbe");
        }
        internal static void Mark(string marker, string detail)
        {
            // Write before BepInEx logging: its log file may not exist yet.
            WriteFile(marker, detail);
            try { log?.LogInfo(Line(marker, detail)); } catch { }
        }
        internal static void WriteFile(string marker, string detail)
        {
            try
            {
                lock (gate)
                {
                    string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "DependencyBootstrapProbe.trace.log");
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(Line(marker, detail));
                        writer.Flush();
                        stream.Flush(true);
                    }
                }
            }
            catch { /* Never interrupt the game for diagnostic I/O. Missing evidence cannot pass validation. */ }
        }
        private static string Line(string marker, string detail) => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " [DBP:" + marker + "] " + detail;
    }
}