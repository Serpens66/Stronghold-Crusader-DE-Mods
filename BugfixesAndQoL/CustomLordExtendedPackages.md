# Complete Vanilla and Script Extender Custom Lord packages

A single Custom Lord Workshop item can support both an unmodded game and a game with the Script Extender. Vanilla loads the normal lord configuration, castles, and avatar. The Script Extender then reads additional metadata, localized presentation, media, Lua, and asset overrides from the same lord folder.

The extended files do not repair an invalid Vanilla lord. Always test the Vanilla base package independently.

## Required folder layout

The Workshop upload contains one directory for the lord. Place the Vanilla and extended files together as follows:

    Workshop content/
      My Lord/
        one-or-more.lordjson
        one-or-more.aivjson
        avatar.png
        info.json
        lordmeta.json
        init.lua
        optional-lord-module.lua
        Override/
          Assets/GUI/Sprites/my-lord-face.png
          Assets/GUI/Video/my-lord-message.webm
          fx/speech/my-lord-neutral.ogg
          Locales/en-US/fx/speech/my-lord-message.ogg
          Locales/de-DE/fx/speech/my-lord-message.ogg
        Locales/
          en-US/crusader.txt
        Scripts/
          init.lua

Only the `.lordjson`, `.aivjson`, and optional `avatar.png` are required by Vanilla:

- At least one valid `.lordjson` and one valid `.aivjson` must be directly inside the lord directory. Vanilla does not scan its subdirectories for these files.
- `avatar.png` is optional. Vanilla accepts it only when it is exactly 144x144 pixels and smaller than 80,000 bytes. Otherwise the question-mark portrait is used.
- Vanilla ignores `info.json`, `lordmeta.json`, Lua files, and the extra subdirectories. They therefore do not prevent the same package from working without the Script Extender.

The Script Extender lets Vanilla process the lord first. When it subsequently encounters the direct `info.json`, it registers the lord directory as an asset mod and reads the direct `lordmeta.json` and optional direct `init.lua`.

## What the in-game uploader publishes

Vanilla first stages the direct `.lordjson`, `.aivjson`, and valid `avatar.png`. The Script Extender uploader hook leaves those files unchanged and adds every other regular file from the local lord directory, recursively.

The only files deliberately not added by the hook are:

- direct `.lordjson`, `.aivjson`, and `avatar.png`, because Vanilla already owns them;
- direct `.data` and `.ldata`, because these are local Workshop upload records rather than package content.

There is no extension allowlist and the uploader does not reject a package merely because its metadata or an asset is invalid. The installed game and Script Extender remain responsible for interpreting the files. Unsafe paths, reparse points, symbolic links, path traversal, or conflicting staging files are rejected. If adding extras fails, Vanilla's base upload continues unchanged.

Before a `Custom Lord` upload starts, the Script Extender performs an advisory preflight against the documented package contract. It reports high-confidence problems such as malformed or missing direct metadata, a missing/empty asset GUID, an unusable version string, unsupported WAV data (including 48-kHz files), an invalid Vanilla avatar, special files in an obviously wrong folder, root-level media that is not indexed through `Override`, and recognizable development/archive material. All findings are shown together in a scrollable Vanilla-style confirmation. Choose **No** to cancel or **Yes** to upload the package unchanged despite the warnings. This preflight uses the same metadata models, version parser, message-key parsing, and relevant WAV constraints as the current runtime. It is deliberately not a complete duplicate of every Asset API schema, so a warning-free package must still be tested in the game.

Keep the publishable lord directory clean. Source recordings, conversion projects, archives, executables, DLLs, backups, and directories such as `_LegacyMediaSource` are uploaded if left inside it. The preflight warns about common examples but does not remove them; store such material outside the lord directory.

## `info.json`

`info.json` identifies the automatically registered asset mod. Use this canonical form:

    {
      "GUID": "author.unique-custom-lord",
      "Author": "Your Name",
      "Name": "Your Lord",
      "Description": "A Custom Lord for Stronghold Crusader Definitive Edition.",
      "Version": "1.0.0",
      "Website": "",
      "Manifest": 0
    }

Use a non-empty GUID that is globally unique to this package. If two loaded asset mods use the same GUID, the later one is ignored by the asset-mod registry. `Manifest: 0`, a valid version, and meaningful name and author values are the recommended canonical format. The Custom Lord asset-registration path currently defaults a missing `Manifest` to `0` and does not require that exact value, so the preflight does not warn about another deserializable numeric manifest value.

The version parser accepts a numeric .NET-style version core, optionally preceded by `v` or `V`. A bare major is expanded to `<major>.0`, and a suffix beginning with `-`, `+`, or a space is ignored for comparison. Consequently `1.0.0-test`, `v2.3.4`, `3`, and `1.4.0+build9` are valid. An absent or unusable version does not prevent initial asset registration, but is treated as `0.0.0.0` when duplicate GUIDs are compared and therefore produces a warning.

`SupportedGameVersions` is not a property of the current runtime `ModInfo` model. If an older template includes it, the JSON deserializer ignores it; it neither restricts loading nor needs to be present.

An absent or malformed `info.json`, or a blank/duplicate GUID, prevents reliable registration of the extended assets. It does not invalidate otherwise valid Vanilla `.lordjson` and `.aivjson` files.

## `lordmeta.json`

`lordmeta.json` is read only after `info.json` has been deserialized and the asset mod has been offered for registration. A complete example is:

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
      },
      "FacePath": "Assets/GUI/Sprites/my-lord-face",
      "JoinAudioPath": "fx/speech/my-lord-join",
      "LeaveAudioPath": "fx/speech/my-lord-leave",
      "Messages": {
        "IncomingMessage": [
          {
            "VideoPath": "my-lord-neutral",
            "AudioPath": "my-lord-message",
            "LocalizedText": {
              "en-US": "A message from the Gatekeeper.",
              "de-DE": "Eine Nachricht vom Torwächter."
            }
          }
        ],
        "WillAttack": [
          {
            "VideoPath": "my-lord-angry",
            "AudioPath": "my-lord-attack",
            "LocalizedText": {
              "en-US": "Your walls will fall!",
              "de-DE": "Eure Mauern werden fallen!"
            }
          }
        ]
      }
    }

All fields are optional at the C# model level, but `lordmeta.json` itself must deserialize as the expected object before the lord receives extended metadata. Missing optional fields are allowed. A field with the wrong JSON type can make deserialization of the entire document fail; it does not merely clear that one field.

| Field | Actual behavior |
|---|---|
| `LocalizedDisplayName` | Localized lord name. The detail panel falls back to Vanilla's custom-lord name if no usable extended name is available. |
| `LocalizedTitles` | Lists of title suffixes. Any non-empty list length is accepted and duplicate strings are allowed. |
| `LocalizedDescription` | Description in Vanilla's existing lord-detail panel. |
| `LocalizedDifficultyRating` | Text displayed after the Vanilla lord-power value, for example `(8) Difficult`. |
| `LocalizedFavouriteTroops` | Favourite troops field in the detail panel. |
| `LocalizedCastles` | Castle description field in the detail panel. |
| `LocalizedPlayStyle` | Play-style field in the detail panel. |
| `LocalizedFavouriteSaying` | Favourite-saying field in the detail panel. |
| `FacePath` | Logical Asset API texture path. An omitted extension can resolve `.png`, `.jpg`, or `.tga`. The valid Vanilla `avatar.png` remains the UI fallback. |
| `JoinAudioPath` | Full logical audio path used when the lord joins the lobby, normally `fx/speech/name`. |
| `LeaveAudioPath` | Full logical audio path used when the lord leaves the lobby. |
| `Messages` | Object keyed case-insensitively by `AILordMessageType`; each value is a list from which one clip is selected. |
| `IncomingMessage` | A legacy top-level model property that is not currently read by the runtime. Do not use it; use `Messages.IncomingMessage` instead. |

### Text localization

The localized dictionaries are looked up by the Script Extender's exact current game-language key, then `en-US`. Provide `en-US` for every text that should have a general fallback. Details and subtitles become empty if neither entry is usable. The extended UI falls back from a missing display name to Vanilla's lord name.

Dictionary keys are not restricted during JSON deserialization, but a key is useful only when it exactly matches the current language or `en-US`. Common game keys are:

`ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN`, and `zh-HK`.

### Titles

Titles are suffixes, so include punctuation and spacing such as `", Keeper of the Gate"`. Title selection uses the player-slot-based index supplied by the game. When the index is at least the list length, it wraps modulo the list length. There is no requirement for eight titles and no requirement that they be distinct. If neither the current locale nor `en-US` provides a list on initial resolution, the Extender has no title suffix; it does not synthesize a Vanilla ordinal. In the current version, changing the language at runtime from a resolved locale to one with neither an exact nor `en-US` list can retain the previously cached title list until the lord data is reloaded. Provide `en-US` to avoid that edge case.

### Message clips

`Messages` keys are parsed case-insensitively. A key that cannot be parsed as `AILordMessageType` is logged and skipped. Numeric strings are technically accepted by the runtime enum parser, including unnamed values, but only the named values below have documented mappings and should be used. Every configured message value should be a non-null JSON array containing non-null clip objects; the preflight warns about null lists or clips because the playback path does not handle them safely. Each clip supports:

- `VideoPath`: the message-video name used by the native message system. The supported convention is a bare stem such as `my-lord-angry`, backed by `Override/Assets/GUI/Video/my-lord-angry.webm`.
- `AudioPath`: the speech name without `fx/speech/`, because the native AI-message path adds that prefix. An extension may be omitted.
- `LocalizedText`: optional subtitle dictionary with current-language then `en-US` fallback.

Available message names are:

`IncomingMessage`, `WillAttack`, `TauntSiege2`, `TauntSiege3`, `TauntSiege4`, `AngerSiegeFailed`, `AngerFortressDamaged`, `PleadDeath`, `PleadOutsideWalls`, `NervousInsideWalls`, `Counterattack`, `Unk11`, `Won`, `Unk13`, `RequestGoods`, `ReceivedGoods`, `DefeatedAgain`, `AllyNotificationCongratulations`, `AllyNotificationHasDefeatedEnemy`, `AllyNotificationRequestReinforcements`, `AllyNotificationMerryChristmas`, `Unk21`, `Unk22`, `AllyNotificationWillSiegeEnemySoon`, `AllyNotificationCannotAttackEnemy`, `AllyNotificationWillNotAttackToday`, `AllyNotificationCannotNotHelp`, `AllyNotificationWillNotHelp`, `AllyNotificationWillNotSendRequestedGoods`, `AllyNotificationHasSentRequestedGoods`, `AllyNotificationConfidentInVictory`, `AllyNotificationConfidentInLosing`, `AllyNotificationSentReinforcements`, and `AllyNotificationAgree`.

`DefeatedAgain`, `AllyNotificationCongratulations`, and `AllyNotificationHasDefeatedEnemy` map to the distinct native IDs 16, 17, and 18 respectively. Values named `Unk` are mapped but their exact trigger is not yet documented.

## Media paths and formats

The registered asset index treats paths below `Override` as logical game paths.

For locale-aware assets such as audio, resolution order is:

1. `Override/Locales/<current-language>/<logical-path>`
2. `Override/Locales/en-US/<logical-path>`
3. `Override/<logical-path>`

For example, `JoinAudioPath: "fx/speech/my-lord-join"` can resolve any of:

    Override/Locales/de-DE/fx/speech/my-lord-join.ogg
    Override/Locales/en-US/fx/speech/my-lord-join.ogg
    Override/fx/speech/my-lord-join.ogg

Both direct and localized `fx/speech` directories are supported. Use lord-specific asset names to avoid collisions with other packages.

- OGG Vorbis is recommended. Its channel count and sample rate are read from the stream.
- WAV must be RIFF/WAVE PCM format 1, mono or stereo, exactly 44.1 kHz (44,100 Hz), 16-bit, with a valid non-empty `data` chunk. The current loader reads the PCM format, channel count, sample rate, and bit depth from the standard fixed `fmt ` header offsets and advances through chunks without RIFF odd-byte padding. Export a conventional PCM WAV with the normal `fmt ` chunk directly after the WAVE header; avoid unusual leading metadata chunks and odd-sized chunks before `data`. The Script Extender does not resample other WAV rates: common legacy 48 kHz (48,000-Hz) speech files are rejected even though the uploader transports them successfully.
- Message video should be WEBM with VP8 at 348x348, preferably without an audio track. The resolver currently also probes `.mp4`, but MP4 is not an officially guaranteed message format.
- Texture resolution supports exact indexed paths and, when the extension is omitted, localized `.png`, `.jpg`, and `.tga` candidates.

Video resolution does not apply the locale-directory fallback used for audio. Keep message videos under the global `Override/Assets/GUI/Video` path.

## Lua and inherited asset-mod features

There are two different Lua entry points:

- Direct `My Lord/init.lua` is the isolated Custom Lord AI script. It may define `ai_init(playerId)` and may load relative `.lua` modules from the lord directory.
- `My Lord/Scripts/init.lua` is the normal automatically registered asset-mod script and uses the asset-mod lifecycle such as `mod_init`, `mod_load`, and `mod_unload`.

Because a valid extended lord is registered as an asset mod, it can also provide every file type supported by the normal Asset API under `Override`, including textures, sprites, audio, music, XAML, atlas definitions/textures, AssetBundles, and other indexed resources. Root `Locales/<locale>/crusader.txt` and `Scripts/init.lua` are likewise part of the asset-mod layout. Consult the Script Extender's `asset-api.md`, `translation-api.md`, and Lua documentation for the schema of those independent systems.

The uploader intentionally packages these files without trying to duplicate all current or future Asset API rules.

## Compatibility and testing checklist

Before publishing:

1. Keep exactly one publishable lord directory and remove development-only material from it.
2. Test its direct `.lordjson`, `.aivjson`, and optional `avatar.png` without the Script Extender.
3. Test `info.json`, `lordmeta.json`, localization, media, and Lua with the current Script Extender.
4. Upload with the exact `Custom Lord` Workshop tag.
5. Subscribe to or download the resulting item and verify that it contains exactly one lord directory with both the Vanilla base and extended tree.
6. Check the BepInEx log for JSON, GUID, asset, audio, video, Lua, and uploader warnings.

One package is usable in separate Vanilla and Script Extender installations. This does not guarantee deterministic mixed multiplayer when only some participants run gameplay-changing Lua or other Extender features; multiplayer participants should use a consistent mod setup for such features.

The `CustomLordExtendedPackageTemplate` directory beside this guide contains starter `info.json`, `lordmeta.json`, and asset-path notes. Replace every placeholder before publishing.
