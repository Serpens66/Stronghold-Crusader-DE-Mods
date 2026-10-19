using System;
using System.IO;

namespace AutoLoadSave
{
    internal sealed class LoadRequest
    {
        internal string ConfiguredPath { get; }
        internal bool Consumed { get; private set; }

        internal LoadRequest(string path)
        {
            ConfiguredPath = (path ?? string.Empty).Trim();
            Consumed = ConfiguredPath.Length == 0;
        }

        internal bool TryBegin(bool menuReady)
        {
            if (Consumed || !menuReady) return false;
            // Consume before parsing or calling Vanilla: failures must never retry.
            Consumed = true;
            return true;
        }

        internal string ResolvePath(string savesDirectory)
        {
            string path = Path.GetFullPath(Path.IsPathRooted(ConfiguredPath)
                ? ConfiguredPath : Path.Combine(savesDirectory, ConfiguredPath));
            if (!string.Equals(Path.GetExtension(path), ".sav", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Only local single-player .sav files are supported (no .msv or .map).");
            if (!File.Exists(path)) throw new FileNotFoundException("Configured savegame does not exist.", path);
            return path;
        }
    }
}
