using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using MultiplayerRestartTest;

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
        var loaded = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (string locale in required)
        {
            var texts = new Dictionary<string, string>(StringComparer.Ordinal);
            read.Invoke(null, new object[] { texts, Path.Combine(root, locale + ".txt") });
            Check(texts.Keys.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(RestartText.Keys.OrderBy(x => x, StringComparer.Ordinal)),
                "Locale key parity failed: " + locale);
            foreach (string key in RestartText.Keys)
                Check(!string.IsNullOrWhiteSpace(RestartText.Resolve(key, requested => texts[requested])), "Empty localized message: " + locale + "/" + key);
            foreach (string key in new[] { "Restart.ConfirmTitle", "Restart.RecoveryTitle" })
            {
                string title = texts[key];
                Check(new StringInfo(title).LengthInTextElements <= RestartText.MaximumTitleElements && title.IndexOfAny(new[] { '\r', '\n' }) < 0,
                    "Shipped confirmation title exceeds the single-line budget: " + locale);
                Check(RestartText.Resolve(key, requested => texts[requested]) == title, "Shipped title unexpectedly fell back: " + locale);
            }
            loaded.Add(locale, texts);
        }
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Check(RestartText.Resolve("Restart.Label", key => loaded["en-US"][key]) == "Restart mission",
                "English game catalog was replaced by the Windows UI language.");
            Check(RestartText.Resolve("Restart.ConfirmTitle", key => loaded["de-DE"][key]) == "Neustart?", "German title did not use the German catalog.");
            foreach (string titleKey in new[] { "Restart.ConfirmTitle", "Restart.RecoveryTitle" })
            {
                string expected = loaded["en-US"][titleKey];
                Check(RestartText.Resolve(titleKey, key => new string('W', 100)) == expected, "Overlong external translation reached the popup heading.");
                Check(RestartText.Resolve(titleKey, key => "short\ntext") == expected, "Multiline external translation reached the heading.");
                Check(RestartText.Resolve(titleKey, key => key) == expected, "Missing locale entry did not fall back to English.");
                Check(RestartText.Resolve(titleKey, key => " ") == expected, "Blank locale entry did not fall back to English.");
            }
        }
        finally { CultureInfo.CurrentUICulture = originalCulture; }
        Console.WriteLine("Restart locale fixtures passed: " + checks + " checks; 21 locale catalogs and compact headings.");
    }
}
