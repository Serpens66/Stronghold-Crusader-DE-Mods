using Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace AivLobbyPresetTest
{
    internal sealed class PresetPlayer
    {
        internal int Id, LordType, AivDefault, KeepSlot, RadarX, RadarY, KeepX, KeepY;
        internal bool Human;
        internal readonly List<int> AivDefaults = new List<int>();
    }

    internal sealed class LobbyPreset
    {
        internal bool Enabled;
        internal string MapFileName, MapSha256;
        internal readonly List<PresetPlayer> Players = new List<PresetPlayer>();

        internal static LobbyPreset Read(string path)
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 65536)
                throw new InvalidDataException("Preset file missing or larger than 64 KiB: " + path);
            return FromValue(DependencyFreeJson.Parse(File.ReadAllText(path)));
        }

        internal static LobbyPreset FromValue(object source)
        {
            var root = Object(source, "root");
            var preset = new LobbyPreset
            {
                Enabled = Bool(root, "enabled"),
                MapFileName = String(root, "mapFileName"),
                MapSha256 = String(root, "mapSha256")
            };
            if (!preset.Enabled)
                return preset;
            if (Path.GetFileName(preset.MapFileName) != preset.MapFileName ||
                !preset.MapFileName.EndsWith(".map", StringComparison.OrdinalIgnoreCase) ||
                preset.MapSha256.Length != 64)
                throw new InvalidDataException("Invalid map filename or SHA-256.");
            var players = root["players"] as IList;
            if (players == null || players.Count < 2 || players.Count > 8)
                throw new InvalidDataException("Expected one human and 1-7 AI players.");
            var ids = new HashSet<int>();
            var slots = new HashSet<int>();
            int humanCount = 0;
            foreach (object value in players)
            {
                var item = Object(value, "player");
                var p = new PresetPlayer
                {
                    Id = Int(item, "id"),
                    Human = item.ContainsKey("human") && Bool(item, "human"),
                    KeepSlot = Int(item, "keepSlot"),
                    RadarX = Int(item, "radarX"), RadarY = Int(item, "radarY"),
                    KeepX = Int(item, "keepX"), KeepY = Int(item, "keepY")
                };
                if (p.Human) humanCount++;
                else
                {
                    p.LordType = Int(item, "lordType");
                    if (item.ContainsKey("aivDefaults"))
                    {
                        if (item.ContainsKey("aivDefault"))
                            throw new InvalidDataException("Specify either aivDefault or aivDefaults for player " + p.Id);
                        var defaults = item["aivDefaults"] as IList;
                        if (defaults == null || defaults.Count < 1 || defaults.Count > 8)
                            throw new InvalidDataException("Expected 1-8 built-in AIV variants for player " + p.Id);
                        foreach (object entry in defaults)
                        {
                            if (!(entry is int) && !(entry is long))
                                throw new InvalidDataException("Expected an integer AIV variant for player " + p.Id);
                            long numberValue = Convert.ToInt64(entry);
                            if (numberValue < 1 || numberValue > 8 ||
                                p.AivDefaults.Contains((int)numberValue))
                                throw new InvalidDataException("Invalid or repeated built-in AIV variant for player " + p.Id);
                            p.AivDefaults.Add((int)numberValue);
                        }
                    }
                    else
                    {
                        p.AivDefaults.Add(Int(item, "aivDefault"));
                    }
                    p.AivDefault = p.AivDefaults[0];
                    if (p.LordType < 0 || p.LordType > 28 || p.AivDefault < 1 || p.AivDefault > 8)
                        throw new InvalidDataException("Invalid lord type or built-in Default AIV for player " + p.Id);
                }
                if (p.Id < 1 || p.Id > 8 || p.KeepSlot < 0 || p.KeepSlot > 7 ||
                    !ids.Add(p.Id) || !slots.Add(p.KeepSlot))
                    throw new InvalidDataException("Invalid or repeated player ID / Keep slot.");
                preset.Players.Add(p);
            }
            if (humanCount != 1 || !preset.Players[0].Human)
                throw new InvalidDataException("The first player must be the only human.");
            for (int i = 0; i < preset.Players.Count; i++)
                if (preset.Players[i].Id != i + 1)
                    throw new InvalidDataException("Player IDs must follow lobby addition order, beginning at 1.");
            return preset;
        }

        private static IDictionary<string, object> Object(object value, string name) =>
            value as IDictionary<string, object> ??
            throw new InvalidDataException("Expected JSON object: " + name);

        private static string String(IDictionary<string, object> item, string key) =>
            item.TryGetValue(key, out object value) && value is string text && !string.IsNullOrWhiteSpace(text)
                ? text : throw new InvalidDataException("Missing string: " + key);

        private static bool Bool(IDictionary<string, object> item, string key) =>
            item.TryGetValue(key, out object value) && value is bool flag
                ? flag : throw new InvalidDataException("Missing Boolean: " + key);

        private static int Int(IDictionary<string, object> item, string key)
        {
            if (!item.TryGetValue(key, out object value))
                throw new InvalidDataException("Missing number: " + key);
            try { return Convert.ToInt32(value); }
            catch (Exception exception) when (exception is FormatException || exception is InvalidCastException || exception is OverflowException)
            { throw new InvalidDataException("Invalid number: " + key, exception); }
        }
    }
}
