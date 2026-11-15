# How to Contribute a Translation

Thank you for helping translate my mods!

Our mods use a separate `Locales` folder for their translations. To make your translation easy for me to include, please edit and send me the locale file for your language.

## Which file should I translate?

In Steam, right-click the game and choose **Manage > Browse local files**. For an individually installed mod, open:

    BepInEx/plugins/<ModName>_Serp/Locales/

For a mod included in **Serps Mods**, open its folder inside the pack instead:

    BepInEx/plugins/SerpsMods_Serp/Mods/<ModName>_Serp/Locales/

The pack's own interface uses `BepInEx/plugins/SerpsMods_Serp/Locales/`. If you work from this repository, use `<ModName>/Locales/`.

Use the file whose name matches your language. Examples:

- German: `de-DE.txt`
- Polish: `pl-PL.txt`
- French: `fr-FR.txt`
- Spanish: `es-ES.txt`
- Brazilian Portuguese: `pt-BR.txt`
- Russian: `ru-RU.txt`
- Ukrainian: `uk-UA.txt`
- Simplified Chinese: `zh-CN.txt`
- Traditional Chinese: `zh-HK.txt`

If a file for your language already exists, please translate that exact file. An untranslated entry may still contain English text. Compare with `en-US.txt` from the same mod package.

If your language does not have a file yet, copy `en-US.txt`, rename the copy to the appropriate language code, and translate the copy.

Please do not translate or send DLL, XAML, JSON, or configuration files. We only need the translated `.txt` file from the `Locales` folder.

## How to edit the file

Each translatable line has this format:

    Translation.Key=Text shown in the game

Translate only the text after the first `=`.

For example:

    RandomEvents.Interval=Interval (Vanilla months)

A Polish translation could be:

    RandomEvents.Interval=Interwał (miesiące gry)

Do not change the key before the `=`:

    RandomEvents.Interval

## Important rules

- Keep every translation key exactly unchanged.
- Keep all lines in the file, in their original order.
- Use a plain-text editor, such as Notepad or Notepad++; do not save it as a Word document.
- Do not add or remove keys.
- Lines beginning with `#` are comments. Keep them unless you want to update the comment; a `#` inside a value is part of that value.
- Preserve placeholders exactly as written, including their braces. Examples:
  - `{0}`
  - `{1}`
  - `{Player}`
  - `{Host}`
  - `{Version}`
  - `{FitPercentage}`
- Preserve formatting sequences such as `\n`.
- You may use normal Unicode characters required by your language.
- Save the file as UTF-8.
- Prefer preserving the file's existing CRLF line endings.
- Please review the translation in context and avoid relying entirely on machine translation.
- If a term is ambiguous, add a separate note instead of changing the translation key.

## Check your translation in the game

1. Close the game, save the edited file in the mod's `Locales` folder, and start the game again.
2. Use the matching game language in Steam. An explicit Script Extender language override takes priority; if one is set, use the same language there.
3. Open the mod's settings and check the text in context, including tooltips and long labels.

Keep a separate copy of your translation before a mod update, which can replace installed locale files. If the language has no matching Steam option, mention that when submitting; you can still contribute the file.

## Example: translating Random Events into Polish

Open:

    RandomEvents_Serp/Locales/pl-PL.txt

Use this file as the translation target and compare it with:

    RandomEvents_Serp/Locales/en-US.txt

Translate every value after `=`, while leaving all keys and placeholders unchanged.

When finished, send us:

    RandomEvents/Locales/pl-PL.txt

The important parts are the mod name, the `Locales` folder, and the original language filename. This allows me to copy the file directly into the project.

## Translating several mods

Every mod has a separate locale file. If you translate multiple mods, please preserve this folder structure:

    BugfixesAndQoL/Locales/pl-PL.txt
    BuildingCosts/Locales/pl-PL.txt
    ExtraFeatures/Locales/pl-PL.txt
    RandomEvents/Locales/pl-PL.txt
    StartConditions/Locales/pl-PL.txt
    UnitCosts/Locales/pl-PL.txt
    UnitLimit/Locales/pl-PL.txt

These are examples; include any other mods you translated in the same way.

Place the files in a ZIP archive if possible. Please do not combine translations from different mods into one file.

## What to include with your submission

Please tell us:

- The language and regional variant
- The language code used in the filename
- The translated mod or mods
- The mod version on which the translation is based
- Whether the translation is complete
- Any wording you were uncertain about

Example:

    Language: Polish
    Language code: pl-PL
    Mod: Random Events
    Mod version: <version shown by the installed mod>
    Status: Complete
    Notes: I was unsure how “Vanilla months” should be expressed consistently with the Polish game translation.

Thank you for your contribution!
