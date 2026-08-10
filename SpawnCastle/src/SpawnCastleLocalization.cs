using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SpawnCastle
{
    internal static class SpawnCastleLocalization
    {
        private const string DefaultLocale = "en-US";
        private static Dictionary<string, string> texts;
        private static string loadedLocale;

        private static readonly Dictionary<string, string> Fallbacks =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Common.ResetToDefault", "Reset to Default" },
                { "Common.EnableMod", "Enable Mod" },
                { "Common.PresetHelp", "Selects the local settings preset." },
                { "Common.Clear", "Clear" },
                { "SpawnCastle.Title", "HUMAN CASTLE" },
                { "SpawnCastle.Help", "Displays the selected AIVJSON as a local blueprint or spawns it in a new singleplayer game. All options in this mod remain local in multiplayer." },
                { "SpawnCastle.LocalOptions", "LOCAL CLIENT OPTIONS" },
                { "SpawnCastle.Castle", "AIVJSON castle" },
                { "SpawnCastle.CastleHelp", "Selects the AIVJSON castle used for the local blueprint or singleplayer spawn." },
                { "SpawnCastle.Inventory", "{0} local AIVJSON files found. The selection remains local in multiplayer." },
                { "SpawnCastle.Mode", "Mode" },
                { "SpawnCastle.ModeHelp", "Shows the selected castle as a local blueprint or spawns it in a new singleplayer game." },
                { "SpawnCastle.Mode.Blueprint", "Blueprint" },
                { "SpawnCastle.Mode.Spawn", "Spawn castle" },
                { "SpawnCastle.Hotkey", "Blueprint toggle key" },
                { "SpawnCastle.HotkeyHelp", "Assigns the key or mouse button that toggles the blueprint display." },
                { "SpawnCastle.ClearHelp", "Removes the assigned blueprint toggle key." },
                { "SpawnCastle.NotAssigned", "Not assigned" },
                { "SpawnCastle.PressAnyKey", "Press any key..." },
                { "SpawnCastle.AssignKey", "Assign key" },
                { "SpawnCastle.Hud.Settings", "Blueprint settings" },
                { "SpawnCastle.Hud.IconScale", "Icon scale" },
                { "SpawnCastle.Hud.IconAlpha", "Icon opacity" },
                { "SpawnCastle.Hud.Unavailable", "Blueprint: unavailable" },
                { "SpawnCastle.Hud.Loading", "Blueprint: loading {0}/{1}" },
                { "SpawnCastle.Hud.On", "Blueprint: on" },
                { "SpawnCastle.Hud.Off", "Blueprint: off" }
            };

        public static string Get(string key)
        {
            EnsureLoaded();
            if (texts.TryGetValue(key, out string value))
                return value;
            return Fallbacks.TryGetValue(key, out string fallback) ? fallback : key;
        }

        private static void EnsureLoaded()
        {
            string locale = GetLocale();
            if (texts != null && string.Equals(locale, loadedLocale, StringComparison.OrdinalIgnoreCase))
                return;

            Dictionary<string, string> loaded =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? AppDomain.CurrentDomain.BaseDirectory;
            LoadFile(loaded, Path.Combine(directory, "Locales", DefaultLocale + ".txt"));
            if (!string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase))
                LoadFile(loaded, Path.Combine(directory, "Locales", locale + ".txt"));
            texts = loaded;
            loadedLocale = locale;
        }

        private static void LoadFile(Dictionary<string, string> target, string path)
        {
            if (!File.Exists(path))
                return;

            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                int separator = line.IndexOf('=');
                if (separator <= 0)
                    continue;
                target[line.Substring(0, separator).Trim()] =
                    line.Substring(separator + 1).Trim().Replace("\\r\\n", Environment.NewLine);
            }
        }

        private static string GetLocale()
        {
            try
            {
                string locale = GameAssetManagerAPI.Instance.CurrentLanguage;
                if (!string.IsNullOrWhiteSpace(locale))
                {
                    locale = locale.Trim().Replace('_', '-');
                    if (locale.Length == 4 && locale.IndexOf('-') < 0)
                    {
                        return locale.Substring(0, 2).ToLowerInvariant() + "-" +
                            locale.Substring(2, 2).ToUpperInvariant();
                    }
                    return locale;
                }
            }
            catch
            {
            }
            return DefaultLocale;
        }
    }
}
