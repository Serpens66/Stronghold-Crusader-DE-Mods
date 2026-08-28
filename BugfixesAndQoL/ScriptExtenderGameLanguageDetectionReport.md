# `LanguageProvider = detect` uses the OS culture instead of the game's language

## Current behavior

`Plugin.InitLanguageProviderEarly()` resolves `detect` through `CultureInfo.CurrentCulture.Name` and stores that value for `InitLanguageProviderLate()`:

    _currentCultureName = LanguageProvider.Value == "detect"
        ? CultureInfo.CurrentCulture.Name
        : LanguageProvider.Value;

The late initialization consequently assigns the same OS locale to both `LocalizationManager` and `GameAssetManagerAPI.CurrentLanguage`.

On a German Windows installation with SHCDE configured to English, the Script Extender therefore logs `Set language provider to de-DE`. Localized assets under `Override/Locales/de-DE/` are selected instead of `Override/Locales/en-US/`, so Custom Lord speech and other Asset API resources use German while the game UI is English.

## Suggested fix

Keep the OS culture only as an early fallback while Steam is not initialized. During late initialization, when `LanguageProvider` is `detect`, query the actual game language through `Steamworks.SteamApps.GetCurrentGameLanguage()`, normalize it to the locale codes used by the Asset API, and update both:

    LocalizationManager.Instance.SetCulture(gameCulture);
    GameAssetManagerAPI.Instance.CurrentLanguage = normalizedGameLocale;

An explicit `LanguageProvider` value should remain authoritative. If Steam's runtime language is temporarily unavailable, the app manifest's `UserConfig/language` value can be used before falling back to the OS culture.

## Reproduction

1. Set the Windows display/culture language to German.
2. Set SHCDE's Steam/game language to English.
3. Leave `LanguageProvider = detect` in `000shcdese.cfg`.
4. Provide the same overridden speech key under both `Override/Locales/de-DE/` and `Override/Locales/en-US/`.
5. Start the game and trigger the speech.

The Script Extender reports `de-DE` and plays the German asset although the game is configured to English.
