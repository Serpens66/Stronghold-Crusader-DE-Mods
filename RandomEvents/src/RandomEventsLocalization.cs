using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace RandomEvents
{
    internal static class RandomEventsLocalization
    {
        private const string DefaultLocale = "en-US";
        private static Dictionary<string, string> texts;
        private static string loadedLocale;

        private static readonly Dictionary<string, string> Fallbacks = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Common.ResetToDefault", "Reset to Default" },
            { "Common.EnableMod", "Enable Mod" },
            { "RandomEvents.Interval", "Interval (Vanilla months)" },
            { "RandomEvents.IntervalHelp", "The first roll happens after one complete interval. Every event rolls independently." },
            { "RandomEvents.ChancesTitle", "EVENT CHANCES (%)" },
            { "RandomEvents.StrengthTitle", "EVENT STRENGTH" },
            { "RandomEvents.ScaledStrengthHelp", "Bandits and archers: units per 3 elapsed game months (one game minute). The rolled factor is multiplied by elapsed time." },
            { "RandomEvents.Minimum", "Min" },
            { "RandomEvents.Maximum", "Max" },
            { "RandomEvents.MultiplayerMode", "Reserved multiplayer mode" },
            { "RandomEvents.MultiplayerModeHelp", "Reserved for a future version. Random Events is fully disabled in network games." },
            { "RandomEvents.MultiplayerShared", "Shared events" },
            { "RandomEvents.MultiplayerIndividual", "Individual rolls" },
            { "RandomEvents.Event.Fair", "Fair" },
            { "RandomEvents.Event.Plague", "Plague" },
            { "RandomEvents.Event.WheatInfestation", "Wheat infestation" },
            { "RandomEvents.Event.HopsBeetles", "Hops beetles" },
            { "RandomEvents.Event.AppleBlight", "Apple blight" },
            { "RandomEvents.Event.TreeBlight", "Tree blight" },
            { "RandomEvents.Event.Rabbits", "Rabbit infestation" },
            { "RandomEvents.Event.LionAttack", "Lion attack" },
            { "RandomEvents.Event.Bandits", "Bandits" },
            { "RandomEvents.Event.MadCows", "Mad cows" },
            { "RandomEvents.Event.Archers", "Archers" },
            { "RandomEvents.Event.Marriage", "Marriage" },
            { "RandomEvents.Event.Bard", "Bard" },
            { "RandomEvents.Event.GranaryTheft", "Granary theft" },
            { "RandomEvents.Event.Fire", "Fire" }
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

            Dictionary<string, string> loaded = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;
            LoadFile(loaded, Path.Combine(directory, "Locales", DefaultLocale + ".txt"));
            if (!string.Equals(locale, DefaultLocale, StringComparison.OrdinalIgnoreCase))
                LoadFile(loaded, Path.Combine(directory, "Locales", locale + ".txt"));
            texts = loaded;
            loadedLocale = locale;
        }

        private static void LoadFile(Dictionary<string, string> target, string path)
        {
            if (!File.Exists(path)) return;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                int separator = line.IndexOf('=');
                if (separator <= 0) continue;
                target[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim().Replace("\\n", Environment.NewLine);
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
                        return locale.Substring(0, 2).ToLowerInvariant() + "-" + locale.Substring(2, 2).ToUpperInvariant();
                    return locale;
                }
            }
            catch { }
            return DefaultLocale;
        }
    }
}
