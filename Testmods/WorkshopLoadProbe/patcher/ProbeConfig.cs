using System;
using System.Collections.Generic;
using System.IO;

namespace WorkshopLoadProbe
{
    internal enum ProbeMode { Off, Observe, Load }

    // BepInEx/config/WorkshopLoadProbe.txt, written by tools/Set-ProbeConfig.ps1.
    // A missing file keeps the probe inert. Unknown or malformed lines also keep it inert,
    // so a typo never loads anything by accident.
    internal sealed class ProbeConfig
    {
        internal const string FileName = "WorkshopLoadProbe.txt";
        internal ProbeMode Mode = ProbeMode.Off;
        internal readonly List<string> ExtraItems = new List<string>();
        internal readonly HashSet<string> LoadItems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal bool LocalWins = true;
        internal bool TypeCache = true;
        internal bool Resolver = true;
        internal bool HashCache = true;
        internal string Error;

        internal static ProbeConfig Read(string configDirectory)
        {
            var config = new ProbeConfig();
            string path = Path.Combine(configDirectory, FileName);
            if (!File.Exists(path)) { config.Error = "no " + FileName; return config; }
            var mode = ProbeMode.Off;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int split = line.IndexOf('=');
                if (split <= 0) return Fail(config, "malformed line: " + line);
                string key = line.Substring(0, split).Trim().ToLowerInvariant();
                string value = line.Substring(split + 1).Trim();
                switch (key)
                {
                    case "mode":
                        if (value == "off") mode = ProbeMode.Off;
                        else if (value == "observe") mode = ProbeMode.Observe;
                        else if (value == "load") mode = ProbeMode.Load;
                        else return Fail(config, "unknown mode " + value);
                        break;
                    case "item":
                        if (!Path.IsPathRooted(value)) return Fail(config, "item must be absolute: " + value);
                        config.ExtraItems.Add(Path.GetFullPath(value));
                        break;
                    case "load":
                        config.LoadItems.Add(Path.IsPathRooted(value) ? Path.GetFullPath(value) : value);
                        break;
                    case "duplicates":
                        if (value == "skip") config.LocalWins = true;
                        else if (value == "bepinex") config.LocalWins = false;
                        else return Fail(config, "unknown duplicates " + value);
                        break;
                    case "typecache": if (!Switch(value, out config.TypeCache)) return Fail(config, line); break;
                    case "resolver": if (!Switch(value, out config.Resolver)) return Fail(config, line); break;
                    case "hashcache": if (!Switch(value, out config.HashCache)) return Fail(config, line); break;
                    default: return Fail(config, "unknown key " + key);
                }
            }
            config.Mode = mode;
            return config;
        }

        private static bool Switch(string value, out bool result)
        {
            result = value == "on";
            return value == "on" || value == "off";
        }

        private static ProbeConfig Fail(ProbeConfig config, string error)
        {
            config.Mode = ProbeMode.Off;
            config.Error = error;
            return config;
        }

        public override string ToString() =>
            "mode=" + Mode + " items=" + ExtraItems.Count + " load=[" + string.Join(", ", new List<string>(LoadItems).ToArray()) +
            "] localWins=" + LocalWins + " typeCache=" + TypeCache + " resolver=" + Resolver + " hashCache=" + HashCache;
    }
}
