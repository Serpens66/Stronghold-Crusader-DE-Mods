using System;
using System.Globalization;
using System.IO;
using BepInEx;

namespace WorkshopLoadProbe.Payload
{
    // Test package for APIShared's safe Workshop installation. Never placed by hand: APIShared's
    // preloader swaps it into BepInEx/plugins (see TESTPLAN.md). It reports which variant
    // BepInEx loaded and which player files survived the update.
    [BepInPlugin(Guid, "Workshop Load Probe Payload", "0.2.0")]
    public sealed class PayloadPlugin : BaseUnityPlugin
    {
        public const string Guid = "WorkshopLoadProbePayload_Serp";

        private void Awake()
        {
            try
            {
                string folder = Path.GetDirectoryName(Info.Location);
                Mark("PAYLOAD_VARIANT", Text(folder, "variant.txt") + " location=" + folder +
                    " userFile=" + Text(folder, Path.Combine("config", "user.cfg")) +
                    " settings=" + Text(folder, "settings.json") + " junk=" + File.Exists(Path.Combine(folder, "junk.log")));
            }
            catch (Exception ex) { Logger.LogError(Stamp() + " WLP PAYLOAD_VARIANT_FAILED " + ex.Message); }
        }

        private static string Text(string folder, string relative)
        {
            string path = Path.Combine(folder, relative);
            return File.Exists(path) ? "'" + File.ReadAllText(path).Trim() + "'" : "missing";
        }

        private void Mark(string marker, string detail) => Logger.LogInfo(Stamp() + " WLP " + marker + " " + detail);

        private static string Stamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }
}
