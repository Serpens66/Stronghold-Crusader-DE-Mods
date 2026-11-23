using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MultiplayerSaveLoadTest;

internal static class LocaleTests
{
    private static int checks;
    private static void Check(bool value, string reason)
    { checks++; if (!value) throw new Exception(reason); }

    internal static void Run()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Locales");
        if (!Directory.Exists(root)) throw new Exception("Locale assets missing from fixture content: " + root);
        MethodInfo read = typeof(SerpLocalization).GetMethod("LoadLocaleFile", BindingFlags.NonPublic | BindingFlags.Static);
        if (read == null) throw new Exception("Shared locale reader fixture boundary changed.");
        string[] required = { "ar", "cs-CZ", "de-DE", "el-GR", "en-US", "es-ES", "fr-FR", "hu-HU", "it-IT", "ja-JP", "ko-KR", "nl-NL", "pl-PL", "pt-BR", "ru-RU", "sv-SE", "th-TH", "tr-TR", "uk-UA", "zh-CN", "zh-HK" };
        string[] headings = { "Load.ConfirmTitle", "Load.CategoryTitle", "Load.RecoveryTitle" };
        var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (string locale in required)
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            read.Invoke(null, new object[] { texts, Path.Combine(root, locale + ".txt") });
            Check(texts.Keys.OrderBy(key => key, StringComparer.Ordinal).SequenceEqual(LoadText.Keys.OrderBy(key => key, StringComparer.Ordinal)),
                "Locale key parity failed: " + locale);
            foreach (string key in LoadText.Keys)
                Check(!string.IsNullOrWhiteSpace(LoadText.Resolve(key, requested => texts[requested])), "Empty localized text: " + locale + "/" + key);
            foreach (string key in headings)
            {
                string title = texts[key];
                Check(new StringInfo(title).LengthInTextElements <= LoadText.MaximumTitleElements && title.IndexOfAny(new[] { '\r', '\n' }) < 0,
                    "Shipped heading exceeds the single-line budget: " + locale + "/" + key);
                Check(LoadText.Resolve(key, requested => texts[requested]) == title, "Shipped heading unexpectedly fell back: " + locale + "/" + key);
            }
            loaded.Add(locale, texts);
        }

        Check(LoadText.Resolve("Load.ConfirmTitle", key => loaded["en-US"][key]) == "End match?", "English selection did not use the English catalog.");
        Check(LoadText.Resolve("Load.ConfirmTitle", key => loaded["de-DE"][key]) == "Beenden?", "German selection did not use the German catalog.");
        Check(LoadText.Resolve("Load.CategoryTitle", key => loaded["de-DE"][key]) == "Koop laden?", "German save-category heading differs from the intended question.");
        foreach (string key in LoadText.Keys)
        {
            string expected = loaded["en-US"][key];
            Check(LoadText.Resolve(key, requested => requested) == expected, "Missing translation did not fall back: " + key);
            Check(LoadText.Resolve(key, requested => " ") == expected, "Blank translation did not fall back: " + key);
        }
        foreach (string key in headings)
        {
            string expected = loaded["en-US"][key];
            Check(LoadText.Resolve(key, requested => new string('W', 100)) == expected, "Overlong external heading reached the popup.");
            Check(LoadText.Resolve(key, requested => "short\ntext") == expected, "Multiline external heading reached the popup.");
            Check(LoadText.Resolve(key, requested => "short\rtext") == expected, "Carriage-return external heading reached the popup.");
        }
        bool unknownRejected = false;
        try { LoadText.Resolve("Unknown.Key", key => "text"); } catch (ArgumentOutOfRangeException) { unknownRejected = true; }
        Check(unknownRejected, "Unknown text key was silently accepted.");
        Console.WriteLine("Multiplayer load locales passed: " + checks + " checks; 21 real file-reader catalogs, compact headings and English fallback; no game or OS-language calls.");
    }
}
