using System;
using System.Globalization;
using System.IO;

namespace WorkshopLoadProbe
{
    // BepInEx/config/WorkshopLoadProbe.txt, written by tools/Set-ProbeConfig.ps1.
    // A missing file keeps the probe inert. Unknown or malformed lines also keep it inert.
    internal sealed class ProbeConfig
    {
        internal const string FileName = "WorkshopLoadProbe.txt";
        internal bool Install;
        internal int QuitAfter;
        internal string Error;

        internal static ProbeConfig Read(string configDirectory)
        {
            var config = new ProbeConfig();
            string path = Path.Combine(configDirectory, FileName);
            if (!File.Exists(path)) { config.Error = "no " + FileName; return config; }
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int split = line.IndexOf('=');
                string key = split > 0 ? line.Substring(0, split).Trim().ToLowerInvariant() : "";
                string value = split > 0 ? line.Substring(split + 1).Trim() : "";
                if (key == "install" && (value == "on" || value == "off")) config.Install = value == "on";
                else if (key == "quitafter" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int seconds) && seconds <= 3600) config.QuitAfter = seconds;
                else return new ProbeConfig { Error = "invalid line: " + line };
            }
            return config;
        }

        public override string ToString() => "install=" + Install + " quitAfter=" + QuitAfter;
    }
}
