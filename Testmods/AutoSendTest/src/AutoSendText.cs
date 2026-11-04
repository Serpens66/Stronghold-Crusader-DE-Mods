using System;
using System.Collections.Generic;
using System.IO;

namespace AutoSendTest
{
    // Loaded only on Unity when the section is created; simulation never reads locale files.
    internal static class AutoSendText
    {
        private static readonly Dictionary<string, string> text = new Dictionary<string, string>(StringComparer.Ordinal);
        internal static void Load()
        {
            if (text.Count != 0) return;
            string directory = Path.Combine(Path.GetDirectoryName(typeof(AutoSendText).Assembly.Location), "Locales");
            Read(Path.Combine(directory, "en-US.txt"));
            string language = SHCDESE.API.GameAssetManagerAPI.Instance.CurrentLanguage;
            try
            {
                string configured = SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LanguageProvider.Value;
                if (!string.IsNullOrWhiteSpace(configured) && !configured.Equals("detect", StringComparison.OrdinalIgnoreCase)) language = configured;
            }
            catch { }
            if (!string.IsNullOrEmpty(language))
            {
                if (language.Equals("german", StringComparison.OrdinalIgnoreCase)) language = "de-DE";
                if (language.Equals("english", StringComparison.OrdinalIgnoreCase)) language = "en-US";
                // Asset language/provider is untrusted path input; only a catalogue filename is accepted.
                if (language.IndexOfAny(new[] { '/', '\\', ':' }) < 0) Read(Path.Combine(directory, language + ".txt"));
            }
        }
        private static void Read(string path)
        {
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            { int split = line.IndexOf('='); if (split > 0 && !line.StartsWith("#")) text[line.Substring(0, split)] = line.Substring(split + 1); }
        }
        internal static string Get(string key) => text.TryGetValue(key, out string value) ? value : key;
    }
}
