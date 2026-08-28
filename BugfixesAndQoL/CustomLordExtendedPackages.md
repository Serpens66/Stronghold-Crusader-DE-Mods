# Complete Custom Lord Workshop packages

BugfixesAndQoL can add Script Extender media and localized lord details to Vanilla's Custom Lord Workshop upload. The host setting **Enable complete Custom Lord packages** must be enabled while uploading and while viewing the additional details.

## Folder layout

Place the extra files directly in the same local Custom Lord folder that already contains the Vanilla files:

    My Lord/
      one-or-more.lordjson
      one-or-more.aivjson
      avatar.png
      info.json
      lordmeta.json
      Override/
        Assets/GUI/Sprites/my-lord-message-face.png
        Assets/GUI/Video/my-lord-message.webm
        Locales/en-US/fx/speech/my-lord-message.ogg
        Locales/de-DE/fx/speech/my-lord-message.ogg

Vanilla owns the `.lordjson`, `.aivjson`, and optional 144x144 `avatar.png`. The current Script Extender owns `info.json`, its existing `lordmeta.json` media fields, and the files below `Override`. BugfixesAndQoL adds only the localized description fields documented below and copies the validated extra package into Vanilla's temporary Workshop staging folder.

Steam installs the complete uploaded folder automatically. Players need the current Script Extender for the custom audio, video, and `Override` assets; they do not need to copy files into Vanilla's `CustomMedia` folder.

## Localized texts and titles

The current Script Extender already localizes the Custom Lord display name, titles, and message subtitles. BugfixesAndQoL additionally reads the six detail dictionaries listed below. All of these fields use the same locale fallback: current game language, then `en-US`, then empty.

- `LocalizedDisplayName`: one display name per locale
- `LocalizedTitles`: eight title suffixes per locale, one for each repeated-lord slot
- `Messages` -> `LocalizedText`: one subtitle per locale and voice-line entry

The following optional dictionaries may be added to `lordmeta.json` without changing its existing Script Extender fields:

- `LocalizedDescription`
- `LocalizedDifficultyRating`
- `LocalizedFavouriteTroops`
- `LocalizedCastles`
- `LocalizedPlayStyle`
- `LocalizedFavouriteSaying`

Each dictionary maps a locale code to text. The game first uses the current locale and then falls back to `en-US`. Missing, blank, or incorrectly typed optional detail values are left empty.

Provide eight distinct entries in every `LocalizedTitles` array. BugfixesAndQoL shows these titles in the lobby and in-game. Missing, blank, or duplicate slots fall back to Vanilla's localized ordinal for that slot (for example, `The Third`). A Script Extender title normally includes its separator, such as `", Keeper of the Gate"`; the lobby column displays it without the leading separator.

Supported locale codes are `ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN`, and `zh-HK`.

Always provide `en-US` when text should remain available for players whose language is not translated.

## Media and safety rules

Speech audio can also be localized without changing `AudioPath`, `JoinAudioPath`, or `LeaveAudioPath`. Put the same relative asset key below a locale folder:

    Override/Locales/en-US/fx/speech/my-lord-message.ogg
    Override/Locales/de-DE/fx/speech/my-lord-message.ogg

Every localized Custom Lord package must place its complete English speech set under `Locales/en-US`; do not use a shortened `en` directory. Add translated sets in parallel locale directories and keep identical relative file names. The Script Extender checks the current locale first, then `en-US`. It can also resolve a global `Override/fx/speech` fallback, but this package convention deliberately reserves that location for truly language-neutral audio.

OGG Vorbis is recommended. WAV files must be mono or stereo PCM at exactly 44100 Hz; the current Script Extender rejects the 48000-Hz WAV files used by some legacy `CustomMedia.aivjson` packages.

For the current Script Extender schema, `JoinAudioPath` and `LeaveAudioPath` include the `fx/speech/` prefix. A `Messages` entry uses the speech name without that prefix because the native AI-message path adds it before playback. The copyable template demonstrates both forms. Use lord-specific file names to avoid collisions with assets from other packages.

Only `.png`, `.ogg`, `.wav`, and `.webm` files below `Override` are transferred as extras. `info.json` must describe Script Extender asset Manifest `0`, and both JSON files must be structurally valid. Asset references must be relative and may not contain absolute paths, drive prefixes, empty path segments, `.` or `..`. Reparse points and symbolic links are rejected.

`init.lua`, executables, DLLs, and unrelated files are never transferred by this feature. If validation or copying fails, BugfixesAndQoL logs the reason and lets the normal Vanilla Custom Lord upload continue without any extra files. The original local Lord folder is never modified.

The `CustomLordExtendedPackageTemplate` directory beside this guide contains copyable `info.json` and `lordmeta.json` starting files. Replace every placeholder before publishing and add media using the paths described in its `Override/ASSET_PATHS.txt` file.
