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
        fx/speech/my-lord-message.ogg

Vanilla owns the `.lordjson`, `.aivjson`, and optional 144x144 `avatar.png`. The current Script Extender owns `info.json`, its existing `lordmeta.json` media fields, and the files below `Override`. BugfixesAndQoL adds only the localized description fields documented below and copies the validated extra package into Vanilla's temporary Workshop staging folder.

Steam installs the complete uploaded folder automatically. Players need the current Script Extender for the custom audio, video, and `Override` assets; they do not need to copy files into Vanilla's `CustomMedia` folder.

## Localized detail fields

The following optional dictionaries may be added to `lordmeta.json` without changing its existing Script Extender fields:

- `LocalizedDescription`
- `LocalizedDifficultyRating`
- `LocalizedFavouriteTroops`
- `LocalizedCastles`
- `LocalizedPlayStyle`
- `LocalizedFavouriteSaying`

Each dictionary maps a locale code to text. The game first uses the current locale and then falls back to `en-US`. Missing, blank, or incorrectly typed optional detail values are left empty.

Supported locale codes are `ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN`, and `zh-HK`.

Always provide `en-US` when text should remain available for players whose language is not translated.

## Media and safety rules

Only `.png`, `.ogg`, `.wav`, and `.webm` files below `Override` are transferred as extras. `info.json` must describe Script Extender asset Manifest `0`, and both JSON files must be structurally valid. Asset references must be relative and may not contain absolute paths, drive prefixes, empty path segments, `.` or `..`. Reparse points and symbolic links are rejected.

`init.lua`, executables, DLLs, and unrelated files are never transferred by this feature. If validation or copying fails, BugfixesAndQoL logs the reason and lets the normal Vanilla Custom Lord upload continue without any extra files. The original local Lord folder is never modified.

The `CustomLordExtendedPackageTemplate` directory beside this guide contains copyable `info.json` and `lordmeta.json` starting files. Replace every placeholder before publishing and add media using the paths described in its `Override/ASSET_PATHS.txt` file.
