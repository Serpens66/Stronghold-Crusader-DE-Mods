# Custom Lord packages with Script Extender

This guide describes the Custom Lord support in **SHCDE Script Extender (at least) 2.3.0**. It starts with the smallest useful extension—a localized description—and then covers portraits, titles, media, Lua, asset overrides, and publishing.

A Script Extender package extends a working Vanilla Custom Lord. It does not replace the normal `.lordjson` and `.aivjson` files or repair an invalid Vanilla lord. Test the Vanilla files independently before adding Script Extender metadata.

## Quick start: add only a custom description

Use this section when the lord already works in Vanilla and you only want text in the existing lord-detail panel.

### 1. Add the two metadata files

The lord directory must contain its existing Vanilla files plus a direct `info.json` and `lordmeta.json`:

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png              # Optional Vanilla portrait
  info.json
  lordmeta.json
```

At least one valid `.lordjson` and one valid `.aivjson` must remain directly inside the lord directory. Vanilla does not search subdirectories for them. `avatar.png` is optional; Vanilla accepts it only when it is exactly 144x144 pixels and smaller than 80,000 bytes.

Script Extender processes a Custom Lord as an extended lord only when it encounters a direct `info.json`. Both `info.json` and `lordmeta.json` must exist and parse as the expected JSON objects.

Use a unique GUID in `info.json`:

```json
{
  "GUID": "yourname.my-custom-lord",
  "Author": "Your Name",
  "Name": "My Custom Lord",
  "Description": "Adds localized detail text to My Custom Lord.",
  "Version": "1.0.0",
  "Website": "",
  "VersionCheckUrl": "",
  "WorkshopUrl": "",
  "Manifest": 0,
  "NetworkMode": 0
}
```

For description-only metadata, `Manifest: 0` identifies an asset mod and `NetworkMode: 0` identifies it as client-side-only. Use an GUID that is globally unique to this package (ASCII).

### 2. Add Description

The smallest useful `lordmeta.json` is:

```json
{
  "LocalizedDisplayName": {
    "en-US": "The Gatekeeper",
    "de-DE": "Der Torwächter"
  },
  "LocalizedTitles": {
    "en-US": [", Keeper of the Gate", ", The Resolute"],
    "de-DE": [", Hüter des Tores", ", der Entschlossene"]
  },
  "LocalizedDescription": {
    "en-US": "A patient defensive lord.",
    "de-DE": "Ein geduldiger defensiver Burgherr."
  },
  "LocalizedDifficultyRating": {
    "en-US": "Difficult",
    "de-DE": "Schwierig"
  },
  "LocalizedFavouriteTroops": {
    "en-US": "Archers and swordsmen",
    "de-DE": "Bogenschützen und Schwertkämpfer"
  },
  "LocalizedCastles": {
    "en-US": "Compact stone castles",
    "de-DE": "Kompakte Steinburgen"
  },
  "LocalizedPlayStyle": {
    "en-US": "Defensive",
    "de-DE": "Defensiv"
  },
  "LocalizedFavouriteSaying": {
    "en-US": "Patience wins wars.",
    "de-DE": "Geduld gewinnt Kriege."
  }
}
```

Select the lord in the skirmish lobby to see the description in Vanilla's existing lord-detail panel.

The lookup order is:

1. The exact current game-language key, such as `de-DE`.
2. `en-US`.
3. An empty description when neither entry contains usable text.

Always provide `en-US` when the description should have a general fallback. Keys are matched against the game's current language string; they are not converted from similar keys such as `de` to `de-DE`.  
  
- German: `de-DE`
- Polish: `pl-PL`
- French: `fr-FR`
- Spanish: `es-ES`
- Brazilian Portuguese: `pt-BR`
- Russian: `ru-RU`
- Ukrainian: `uk-UA`
- Simplified Chinese: `zh-CN`
- Traditional Chinese: `zh-HK`
  
If the description does not appear, inspect `BepInEx/LogOutput.log` for a missing or malformed `info.json` or `lordmeta.json`, then validate both files with a JSON parser.

## Advanced package

The following layout combines the Vanilla lord with every major Script Extender extension. Add only the parts you actually use.

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png
  info.json
  lordmeta.json
  init.lua                         # Optional isolated lord AI
  optional-lord-module.lua
  MapAreas/
    keep-surroundings.sema
  Override/
    Assets/GUI/Sprites/my-lord-face.png
    Assets/GUI/Video/my-lord-angry.webm
    fx/speech/my-lord-join.ogg
    fx/speech/my-lord-leave.ogg
    fx/speech/my-lord-attack.ogg
    Locales/en-US/fx/speech/my-lord-attack.ogg
    Locales/de-DE/fx/speech/my-lord-attack.ogg
  Locales/
    en-US/crusader.txt             # Optional asset-mod translation file
  Scripts/
    init.lua                       # Optional normal asset-mod script
```

### Complete `lordmeta.json` example

All properties are optional at the `LordInfo` model level. Omitting one does not disable the others. A wrong JSON type can, however, prevent the entire file from deserializing.

```json
{
  "LocalizedDisplayName": {
    "en-US": "The Gatekeeper",
    "de-DE": "Der Torwächter"
  },
  "LocalizedTitles": {
    "en-US": [
      ", Keeper of the Gate",
      ", The Resolute"
    ],
    "de-DE": [
      ", Hüter des Tores",
      ", der Entschlossene"
    ]
  },
  "LocalizedDescription": {
    "en-US": "A patient defensive lord.",
    "de-DE": "Ein geduldiger defensiver Burgherr."
  },
  "LocalizedDifficultyRating": {
    "en-US": "Difficult",
    "de-DE": "Schwierig"
  },
  "LocalizedFavouriteTroops": {
    "en-US": "Archers and swordsmen",
    "de-DE": "Bogenschützen und Schwertkämpfer"
  },
  "LocalizedCastles": {
    "en-US": "Compact stone castles",
    "de-DE": "Kompakte Steinburgen"
  },
  "LocalizedPlayStyle": {
    "en-US": "Defensive",
    "de-DE": "Defensiv"
  },
  "LocalizedFavouriteSaying": {
    "en-US": "Patience wins wars.",
    "de-DE": "Geduld gewinnt Kriege."
  },
  "FacePath": "Assets/GUI/Sprites/my-lord-face",
  "JoinAudioPath": "fx/speech/my-lord-join",
  "LeaveAudioPath": "fx/speech/my-lord-leave",
  "Messages": {
    "IncomingMessage": [
      {
        "VideoPath": "my-lord-angry",
        "AudioPath": "my-lord-attack",
        "LocalizedText": {
          "en-US": "A message from the Gatekeeper.",
          "de-DE": "Eine Nachricht vom Torwächter."
        }
      }
    ],
    "AngryCastleDamaged": [
      {
        "VideoPath": "my-lord-angry",
        "AudioPath": "my-lord-attack",
        "LocalizedText": {
          "en-US": "You will pay for that!",
          "de-DE": "Das werdet Ihr mir büßen!"
        }
      }
    ]
  }
}
```

`LuaInitPath` is **not** a property of `lordmeta.json` in 2.4.0. The lord-specific Lua entry point is detected from a physical `init.lua` directly in the lord directory.

### `LordInfo` field reference

| Field | JSON type | Behavior |
|---|---|---|
| `LocalizedDisplayName` | `{ locale: string }` | Localized name. The detail panel retains the Vanilla custom-lord name when no usable extended name is available. |
| `LocalizedTitles` | `{ locale: string[] }` | Localized title suffixes selected by zero-based player-slot index. |
| `LocalizedDescription` | `{ locale: string }` | Description in the skirmish-lobby detail panel. |
| `LocalizedDifficultyRating` | `{ locale: string }` | Text displayed after the numeric lord power, for example `(8) Difficult`. |
| `LocalizedFavouriteTroops` | `{ locale: string }` | Favourite-troops text in the detail panel. |
| `LocalizedCastles` | `{ locale: string }` | Castle-design text in the detail panel. |
| `LocalizedPlayStyle` | `{ locale: string }` | Play-style text in the detail panel. |
| `LocalizedFavouriteSaying` | `{ locale: string }` | Favourite-saying text in the detail panel. |
| `FacePath` | `string` | Logical Asset API texture path for the extended portrait. |
| `JoinAudioPath` | `string` | Full logical audio path played when the lord joins the lobby. |
| `LeaveAudioPath` | `string` | Full logical audio path played when the lord leaves the lobby. |
| `Messages` | `{ messageName: clip[] }` | Message variants keyed case-insensitively by an exact `AILordMessageType` member name. |
| `IncomingMessage` | `string` | Legacy model property that the 2.4.0 runtime does not read. Use `Messages.IncomingMessage` instead. |

The six detail fields are resolved independently on every access. Missing or blank values become empty strings and do not affect another field. The display name and message subtitles use the same current-language then `en-US` lookup. A missing display name is filtered out so the Vanilla name remains; message subtitles have the 2.4.0 edge case described below.

Common current game-language keys are:

`ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN`, and `zh-HK`.

### Titles

Titles are suffixes appended to a lord name, so include the required punctuation and spacing, for example `", Keeper of the Gate"`.

The Script Extender selects a title using the zero-based player-slot index. If the index is greater than or equal to the number of titles, it wraps with modulo. A non-empty list can have any length and entries do not have to be unique. Provide `en-US` as a fallback list.

### Portraits and asset paths

`FacePath` is a logical path below `Override`, not an absolute filesystem path. For this value:

```text
"FacePath": "Assets/GUI/Sprites/my-lord-face"
```

place the image at:

```text
Override/Assets/GUI/Sprites/my-lord-face.png
```

When the extension is omitted, the texture resolver probes supported image extensions including `.png`, `.jpg`, and `.tga`. If `FacePath` is absent or cannot be loaded, the detail panel falls back to Vanilla's validated `avatar.png`; without either image, Vanilla's question-mark portrait remains.

Use lord-specific asset names. The registered asset index is shared, so generic paths can collide with another loaded asset mod.

### Join and leave audio

`JoinAudioPath` and `LeaveAudioPath` use full logical audio paths, normally beginning with `fx/speech/`:

```text
"JoinAudioPath": "fx/speech/my-lord-join"
"LeaveAudioPath": "fx/speech/my-lord-leave"
```

An omitted extension allows the audio resolver to probe `.ogg` and `.wav`.

### Message clips

Each `Messages` property must use an actual `AILordMessageType` member name. Matching is case-insensitive. Unknown keys are logged and skipped. The value is a list of clip objects; when multiple clips are present, the runtime selects one through its deterministic random source.

Each `LordMessageClip` supports:

| Field | Behavior |
|---|---|
| `VideoPath` | Native message-video name, normally a bare stem such as `my-lord-angry`. Use an empty string to keep the original video. |
| `AudioPath` | Native message-speech name without `fx/speech/`, normally a bare stem such as `my-lord-attack`. Use an empty string to keep the original audio. |
| `LocalizedText` | Optional subtitle dictionary. It falls back from the current language to `en-US`. Always provide `en-US` to avoid the 2.4.0 `not-set` placeholder edge case. |

The corresponding assets are normally:

```text
Override/Assets/GUI/Video/my-lord-angry.webm
Override/fx/speech/my-lord-attack.ogg
```

Empty video or audio values leave that original native component unchanged. This permits a text-only, audio-only, or video-only override for a message.

In 2.4.0, an absent `LocalizedText` dictionary or one with neither the current language nor `en-US` resolves to the literal internal placeholder `not-set`, which can then be shown as the subtitle. Provide a non-empty `en-US` subtitle for every configured clip. If you deliberately want to retain the original subtitle, provide an empty string for the active locale or avoid configuring that message entry.

### Message names in 2.4.0

Use these exact enum names; the names are case-insensitive but are otherwise not translated or normalized:

```text
IncomingMessage
Taunt1
Taunt2
Taunt3
Taunt4
AngrySiegeLost
AngryCastleDamaged
Defeat
NervPreSiege
NervWeak
VictoryGood
VictoryHarass
KillPlayer
KilNpc
RequestGoods
ThankGoods
DieAlly
CongratsOnKill
BoastOfKill
AllyNeedHelp
MerryChristmas
Unk21
Unk22
About2Siege
CantAttack
WontAttack
CantHelp
WontHelp
NotSendingGoods
SentGoods
TeamWinning
TeamLosing
WillSendTroops
WillAttackEnemy
Nickname1
Nickname2
Nickname3
Nickname4
Nickname5
Nickname6
Nickname7
Nickname8
```

`KilNpc` is intentionally spelled with one `l` in the 2.4.0 enum. `Unk21` and `Unk22` are mapped names whose exact gameplay triggers are not documented. The `Nickname1` through `Nickname8` members exist, but this guide does not promise a normal AI-message trigger for them.

Names from other revisions such as `TauntSiege2`, `AngerSiegeFailed`, `AngerFortressDamaged`, `PleadDeath`, or `AllyNotificationAgree` do not match the 2.4.0 enum and are skipped.

### Media localization and formats

Audio resolution is locale-aware. For the logical path `fx/speech/my-lord-attack`, the lookup order is:

1. `Override/Locales/<current-language>/fx/speech/my-lord-attack.ogg`
2. `Override/Locales/en-US/fx/speech/my-lord-attack.ogg`
3. `Override/fx/speech/my-lord-attack.ogg`

The resolver performs the same order for an exact extension and then probes `.ogg` followed by `.wav` when necessary.

Video resolution does not use that locale-folder fallback. Keep lord message videos at the global logical path under `Override/Assets/GUI/Video/`.

- OGG Vorbis is recommended. The decoder reads its channel count and sample rate from the stream.
- WAV must be RIFF/WAVE PCM format 1, mono or stereo, exactly 44,100 Hz, and 16-bit. Other WAV sample rates, including 48,000 Hz, are rejected.
- Message video should be WEBM with VP8 at 348x348, preferably without an audio track. The resolver also probes `.mp4`, but WEBM is the documented safe choice.

### `info.json`, GUIDs, versions, and networking

The canonical `info.json` example from the quick start is also suitable for the advanced asset-only package.

- `GUID` must be non-empty and unique for reliable asset registration. GUID comparison is case-insensitive.
- `Manifest: 0` selects an asset/Lua mod without a BepInEx DLL.
- `NetworkMode: 0` is appropriate for presentation-only text and media.
- Use `NetworkMode: 1` when Lua or another package feature changes gameplay. Multiplayer participants must then use a matching mod setup.
- `Version` should use a numeric .NET-style version core. The 2.4.0 parser also accepts a leading `v`/`V`, a bare major, and suffixes beginning with `-`, `+`, or a space. An unusable version is treated as `0.0.0.0` for duplicate resolution.
- `VersionCheckUrl` optionally accepts the HTTPS root URL of a public GitHub or GitLab repository.
- `WorkshopUrl` optionally identifies the package's Steam Workshop page.

`SupportedGameVersions` is not a property of the 2.4.0 `ModInfo` model and is deliberately absent from the examples.

If the same GUID is discovered more than once during normal asset-mod discovery, the highest parseable version wins. A duplicate encountered after another copy is already registered is ignored and logged. Do not deliberately reuse another package's GUID.

### Two different Lua entry points

The two `init.lua` locations have different contracts:

- `My Lord/init.lua` is the Custom Lord AI script. It is loaded into an isolated per-lord table environment and may define `ai_init(playerId, loadMode)`. It can access Script Extender APIs through the shared global fallback without placing its own variables in another lord's environment.
- `My Lord/Scripts/init.lua` is the normal asset-mod script registered by `info.json`. It uses the asset-mod lifecycle, including `mod_init`, `mod_load`, and `mod_unload`.

Do not put `LuaInitPath` in `lordmeta.json`. The Custom Lord entry checks only for direct root `init.lua`.

Lua that changes resources, units, buildings, terrain, AI behavior, or other simulation state is gameplay-affecting and requires `NetworkMode: 1` plus matching multiplayer installations.

### Other inherited asset-mod features

Because the lord directory is registered as an asset mod, it can use the normal 2.4.0 Asset API layout. This includes indexed files below `Override`, root `Locales/<locale>/crusader.txt`, `Scripts/init.lua`, XAML patches, atlases, sprites, textures, audio, music, AssetBundles, and private mod resources.

These systems have their own schemas and lifecycle rules. Do not infer them from `lordmeta.json`; use the linked API guides below.

## Publishing and installation

A Workshop item must install with the lord directory as an immediate child of the item's content directory, because Vanilla scans those child directories for Custom Lords:

```text
Workshop item content/
  My Lord/
    one-or-more.lordjson
    one-or-more.aivjson
    info.json
    lordmeta.json
    ...all optional extended files...
```

Script Extender 2.4.0 does **not** contain a hook that adds arbitrary extended files to Vanilla's Custom Lord upload, and it does not provide the package preflight described by some later or experimental documentation. Do not assume the in-game uploader included `info.json`, `lordmeta.json`, subdirectories, media, or Lua.

Use a Steam UGC publishing workflow that uploads the complete prepared content directory when the Vanilla uploader does not preserve those files. After publishing, subscribe to or download the item and inspect the installed Workshop directory. Verify that there is exactly one intended lord directory and that every extended file is present at the same relative path.

The Script Extender's generic `.map` Workshop packager installs mods through its separate map-archive system; do not substitute that layout for a Custom Lord package unless you intentionally build and test a separate installation design.

## Test checklist

1. Test the direct `.lordjson`, `.aivjson`, and optional valid `avatar.png` as a Vanilla Custom Lord.
2. Add `info.json` and the minimal `lordmeta.json`; select the lord and confirm its description in at least `en-US` and one translated game language.
3. Confirm that a missing translation falls back to `en-US` and that omitted detail fields stay empty.
4. Add advanced fields one subsystem at a time: display name and titles, portrait, join/leave audio, then message clips and subtitles.
5. Test both localized and global audio fallback paths and check that unsupported WAV files fail without breaking unrelated metadata.
6. If Lua is present, test new-game and saved-game loading and verify the correct `NetworkMode` classification.
7. Check `BepInEx/LogOutput.log` for JSON, GUID, duplicate, asset, media, message-key, and Lua errors.
8. Publish, install the resulting Workshop item, inspect its actual files, and repeat the runtime tests from the installed copy.

## Script Extender 2.4.0 references

- [Extended AI Modding](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/extended-ai-modding.md)
- [Asset API](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/asset-api.md)
- [Translation API](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/translation-api.md)
- [Lua quick start](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/lua-quick-start.md)
- [Lua reference](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/lua-reference.md)
- [Workshop Mod Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/v2.4.0/docs/guides/workshop-mod-creation-guide.md)
