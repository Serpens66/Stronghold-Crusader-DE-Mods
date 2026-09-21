# Custom Lord packages with Script Extender / Custom-Lord-Pakete mit Script Extender

[English](#english) | [Deutsch](#deutsch)

## English

This guide describes Custom Lord packages for the current SHCDE Script Extender 2.8.0 and ExtendedData uploader. A Script Extender package extends a working base Custom Lord; it does not replace the required `.lordjson` and `.aivjson` files.

### Quick start: localized details

The Lord directory contains its base files plus direct `info.json` and `lordmeta.json` files:

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png              # Optional base portrait
  info.json
  lordmeta.json
```

Keep at least one valid `.lordjson` and `.aivjson` directly in the directory. `avatar.png` is optional; the game accepts it when it is exactly 144x144 pixels and smaller than 80,000 bytes.

The Script Extender registers extended metadata only when both `info.json` and `lordmeta.json` exist and deserialize as their expected objects.

Use a globally unique ASCII GUID and local asset mode in `info.json`:

```json
{
  "GUID": "yourname.my-custom-lord",
  "Author": "Your Name",
  "Name": "My Custom Lord",
  "Description": "Adds localized details and media to My Custom Lord.",
  "Version": "1.0.0",
  "Dependencies": [],
  "Website": "",
  "VersionCheckUrl": "",
  "WorkshopUrl": "",
  "Manifest": 0,
  "AssetMode": "Local",
  "NetworkMode": 0
}
```

`Manifest: 0` identifies an asset/Lua package. `AssetMode: "Local"` keeps the Lord's `Override/` files private to its GUID, so several Lords can safely reuse names such as `face.png` or `attack.ogg`. Use `NetworkMode: 1` instead of `0` when Lua or another package feature changes simulation state.

A useful `lordmeta.json` can provide all detail-panel text at once:

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

The lookup order is the exact current game-language key, then `en-US`. Optional detail fields become empty if neither contains usable text. Always provide `en-US` as the general fallback. Locale keys are not normalized from forms such as `de` to `de-DE`.

### Complete package layout

Add only the components the Lord actually uses:

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png
  info.json
  lordmeta.json
  init.lua                         # Optional isolated Lord AI
  optional-lord-module.lua
  MapAreas/
    keep-surroundings.sema
  Override/
    Assets/GUI/Sprites/face.png
    Assets/GUI/Video/angry.webm
    fx/speech/join.ogg
    fx/speech/leave.ogg
    fx/speech/attack.ogg
    Locales/en-US/fx/speech/attack.ogg
    Locales/de-DE/fx/speech/attack.ogg
  Locales/
    en-US/crusader.txt
  Scripts/
    init.lua                       # Optional asset-mod lifecycle script
```

The direct root `init.lua` is the Lord AI entry point. Do not add a path field for it to `lordmeta.json`.

### Complete metadata example

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
  },
  "FacePath": "Assets/GUI/Sprites/face",
  "JoinAudioPath": "fx/speech/join",
  "LeaveAudioPath": "fx/speech/leave",
  "Messages": {
    "IncomingMessage": [
      {
        "VideoPath": "Assets/GUI/Video/angry",
        "AudioPath": "fx/speech/attack",
        "LocalizedText": {
          "en-US": "A message from the Gatekeeper.",
          "de-DE": "Eine Nachricht vom Torwächter."
        }
      }
    ],
    "AngryCastleDamaged": [
      {
        "VideoPath": "Assets/GUI/Video/angry",
        "AudioPath": "fx/speech/attack",
        "LocalizedText": {
          "en-US": "You will pay for that!",
          "de-DE": "Das werdet Ihr mir büßen!"
        }
      }
    ]
  }
}
```

### Active `lordmeta.json` fields

| Field | JSON type | Behavior |
|---|---|---|
| `LocalizedDisplayName` | `{ locale: string }` | Localized display name; the base name remains when no usable value exists. |
| `LocalizedTitles` | `{ locale: string[] }` | Title suffixes selected by zero-based player-slot index and wrapped with modulo. |
| `LocalizedDescription` | `{ locale: string }` | Description in the Custom Lord detail panel. |
| `LocalizedDifficultyRating` | `{ locale: string }` | Text displayed with the numeric Lord power. |
| `LocalizedFavouriteTroops` | `{ locale: string }` | Favourite-troops text. |
| `LocalizedCastles` | `{ locale: string }` | Castle-design text. |
| `LocalizedPlayStyle` | `{ locale: string }` | Play-style text. |
| `LocalizedFavouriteSaying` | `{ locale: string }` | Favourite-saying text. |
| `FacePath` | `string` | Asset API texture path for the extended portrait. |
| `JoinAudioPath` | `string` | Audio path played when the Lord joins the lobby. |
| `LeaveAudioPath` | `string` | Audio path played when the Lord leaves the lobby. |
| `Messages` | `{ messageName: clip[] }` | Message variants keyed case-insensitively by an exact `AILordMessageType` name. |

Titles are suffixes, so include their punctuation and leading space. The six optional detail fields resolve independently and do not suppress one another.

Common game-language keys are `ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN`, and `zh-HK`.

### Assets and provider-local paths

Paths in `lordmeta.json` are relative to `Override/`. With `AssetMode: "Local"`, the Script Extender automatically qualifies face, join/leave audio, and message video/audio paths with the owning GUID. Other local packages can use the same relative names without collisions.

An omitted texture extension probes `.png`, `.jpg`, `.tga`, and `.dds`. DDS is supported by the runtime but is not part of ExtendedData's Custom Lord upload allowlist. If `FacePath` cannot be loaded, the detail panel falls back to the validated `avatar.png`, then to the game's question-mark portrait.

Audio resolution checks:

1. `Override/Locales/<current-language>/<path>`
2. `Override/Locales/en-US/<path>`
3. `Override/<path>`

For extensionless audio, `.ogg` is checked before `.wav`. OGG Vorbis is recommended. WAV must be RIFF/WAVE PCM format 1, mono or stereo, 44,100 Hz, and 16-bit.

Video uses `.webm` or `.mp4`; WEBM with VP8 at 348x348 is the documented safe choice. Keep message videos below `Override/Assets/GUI/Video/`.

### Message clips and names

Each `Messages` property must match an `AILordMessageType` name case-insensitively. Unknown keys are logged and skipped. Multiple clips are selected through the Script Extender's deterministic random source.

| Clip field | Behavior |
|---|---|
| `VideoPath` | Asset-relative video path. An empty string keeps the original video. |
| `AudioPath` | Asset-relative speech path. An empty string keeps the original audio. |
| `LocalizedText` | Optional subtitle dictionary using current-language then `en-US` fallback. |

Provide `en-US` subtitles for configured clips unless an empty current-language string is intentionally used to suppress a replacement subtitle.

Current message names are:

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
KillNpc
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

`Unk21` and `Unk22` have no documented gameplay meaning. The nickname members exist, but a normal AI-message trigger is not guaranteed.

### Two Lua entry points

- `My Lord/init.lua` is the isolated Lord AI script. It can define `ai_init(playerId, loadMode)` and receives the one-based player ID plus the Script Extender load mode.
- `My Lord/Scripts/init.lua` is the normal asset-mod lifecycle script. It can define `mod_init`, `mod_load`, and `mod_unload`.

Lua that changes resources, units, buildings, terrain, AI behavior, or other simulation state requires `NetworkMode: 1` and matching multiplayer installations.

### Publishing with ExtendedData

A Workshop item must install the Lord directory as an immediate child of the item's content directory. On the upload page, keep **Upload additional files for mod support** enabled to stage supported extended files.

The game handles direct root `.lordjson`, `.aivjson`, and optional `avatar.png`. ExtendedData adds this case-insensitive allowlist:

| Location | Files added by ExtendedData |
|---|---|
| Lord root | direct `*.json` and `*.lua` |
| `Scripts/**` | `*.lua` |
| `MapAreas/**` | `*.sema` |
| `Locales/<locale>/` | exactly `crusader.txt` |
| `Override/**` | `.png`, `.jpg`, `.tga`, `.ogg`, `.wav`, `.webm`, `.mp4` |

Direct root `.data` and `.ldata` files are local uploader controls and are not uploaded. Other files are inventoried but excluded. The confirmation warning reports the count and examples; the complete sorted list is written to `BepInEx/LogOutput.log`. If an allowed file cannot be staged safely, the upload is cancelled instead of publishing an incomplete package.

Packages requiring DDS, XAML patches, atlases, AssetBundles, nested JSON resources, or other excluded files need a separate complete Steam UGC publishing workflow.

### Test checklist

1. Test the direct `.lordjson`, `.aivjson`, and optional valid `avatar.png` first.
2. Add `info.json` and minimal `lordmeta.json`; verify `en-US` and one translated language.
3. Add display name, titles, portrait, join/leave audio, message clips, and Lua one subsystem at a time.
4. Test local asset isolation with another Lord using the same relative filenames.
5. Test localized and global audio fallback and reject unsupported WAV files cleanly.
6. Verify `NetworkMode` for every Lua or gameplay-affecting feature.
7. Review every ExtendedData preflight warning and the full excluded-file list in the log.
8. Install the published Workshop item, inspect its actual files, and repeat the runtime tests.

Current upstream references:

- [Extended AI Modding](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/extended-ai-modding.md)
- [Asset API](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/asset-api.md)
- [Lua quick start](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/lua-quick-start.md)
- [Workshop Mod Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/workshop-mod-creation-guide.md)

---

## Deutsch

Dieser Guide beschreibt Custom-Lord-Pakete für den aktuellen SHCDE Script Extender 2.8.0 und den ExtendedData-Uploader. Ein Script-Extender-Paket erweitert einen funktionierenden grundlegenden Custom Lord; es ersetzt nicht die erforderlichen `.lordjson`- und `.aivjson`-Dateien.

### Schnellstart: lokalisierte Details

Das Lord-Verzeichnis enthält seine Basisdateien sowie direkte Dateien `info.json` und `lordmeta.json`:

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png              # Optional base portrait
  info.json
  lordmeta.json
```

Mindestens eine gültige `.lordjson` und `.aivjson` muss direkt im Verzeichnis liegen. `avatar.png` ist optional; das Spiel akzeptiert sie bei exakt 144x144 Pixeln und weniger als 80.000 Byte.

Der Script Extender registriert erweiterte Metadaten nur, wenn `info.json` und `lordmeta.json` vorhanden sind und als die erwarteten Objekte deserialisiert werden können.

Verwende in `info.json` eine weltweit eindeutige ASCII-GUID und den lokalen Asset-Modus:

```json
{
  "GUID": "yourname.my-custom-lord",
  "Author": "Your Name",
  "Name": "My Custom Lord",
  "Description": "Adds localized details and media to My Custom Lord.",
  "Version": "1.0.0",
  "Dependencies": [],
  "Website": "",
  "VersionCheckUrl": "",
  "WorkshopUrl": "",
  "Manifest": 0,
  "AssetMode": "Local",
  "NetworkMode": 0
}
```

`Manifest: 0` kennzeichnet ein Asset-/Lua-Paket. `AssetMode: "Local"` hält die Dateien unter `Override/` im privaten GUID-Namensraum des Lords, sodass mehrere Lords Namen wie `face.png` oder `attack.ogg` sicher wiederverwenden können. Verwende `NetworkMode: 1` statt `0`, wenn Lua oder ein anderes Paketfeature den Simulationszustand verändert.

Eine nützliche `lordmeta.json` kann alle Texte des Detailfensters gemeinsam bereitstellen:

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

Die Suchreihenfolge ist der exakte aktuelle Spielsprachenschlüssel und danach `en-US`. Optionale Detailfelder bleiben leer, wenn keiner davon nutzbaren Text enthält. Stelle `en-US` immer als allgemeinen Fallback bereit. Locale-Schlüssel wie `de` werden nicht zu `de-DE` normalisiert.

### Vollständiger Paketaufbau

Füge nur die Komponenten hinzu, die der Lord tatsächlich verwendet:

```text
My Lord/
  one-or-more.lordjson
  one-or-more.aivjson
  avatar.png
  info.json
  lordmeta.json
  init.lua                         # Optional isolated Lord AI
  optional-lord-module.lua
  MapAreas/
    keep-surroundings.sema
  Override/
    Assets/GUI/Sprites/face.png
    Assets/GUI/Video/angry.webm
    fx/speech/join.ogg
    fx/speech/leave.ogg
    fx/speech/attack.ogg
    Locales/en-US/fx/speech/attack.ogg
    Locales/de-DE/fx/speech/attack.ogg
  Locales/
    en-US/crusader.txt
  Scripts/
    init.lua                       # Optional asset-mod lifecycle script
```

Die direkte `init.lua` an der Wurzel ist der Lord-KI-Einstiegspunkt. Füge dafür kein Pfadfeld zu `lordmeta.json` hinzu.

### Vollständiges Metadatenbeispiel

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
  },
  "FacePath": "Assets/GUI/Sprites/face",
  "JoinAudioPath": "fx/speech/join",
  "LeaveAudioPath": "fx/speech/leave",
  "Messages": {
    "IncomingMessage": [
      {
        "VideoPath": "Assets/GUI/Video/angry",
        "AudioPath": "fx/speech/attack",
        "LocalizedText": {
          "en-US": "A message from the Gatekeeper.",
          "de-DE": "Eine Nachricht vom Torwächter."
        }
      }
    ],
    "AngryCastleDamaged": [
      {
        "VideoPath": "Assets/GUI/Video/angry",
        "AudioPath": "fx/speech/attack",
        "LocalizedText": {
          "en-US": "You will pay for that!",
          "de-DE": "Das werdet Ihr mir büßen!"
        }
      }
    ]
  }
}
```

### Aktive Felder in `lordmeta.json`

| Feld | JSON-Typ | Verhalten |
|---|---|---|
| `LocalizedDisplayName` | `{ locale: string }` | Lokalisierter Anzeigename; ohne nutzbaren Wert bleibt der Basisname erhalten. |
| `LocalizedTitles` | `{ locale: string[] }` | Titelsuffixe, ausgewählt über den nullbasierten Spielerslot und mit Modulo umgebrochen. |
| `LocalizedDescription` | `{ locale: string }` | Beschreibung im Custom-Lord-Detailfenster. |
| `LocalizedDifficultyRating` | `{ locale: string }` | Text zusammen mit der numerischen Lord-Stärke. |
| `LocalizedFavouriteTroops` | `{ locale: string }` | Text für bevorzugte Truppen. |
| `LocalizedCastles` | `{ locale: string }` | Text für Burgdesigns. |
| `LocalizedPlayStyle` | `{ locale: string }` | Text für den Spielstil. |
| `LocalizedFavouriteSaying` | `{ locale: string }` | Text für den Lieblingsspruch. |
| `FacePath` | `string` | Asset-API-Texturpfad für das erweiterte Porträt. |
| `JoinAudioPath` | `string` | Audiopfad beim Beitritt des Lords zur Lobby. |
| `LeaveAudioPath` | `string` | Audiopfad beim Verlassen der Lobby. |
| `Messages` | `{ messageName: clip[] }` | Nachrichtenvarianten mit einem exakten, ohne Beachtung der Groß-/Kleinschreibung verglichenen `AILordMessageType`-Namen. |

Titel sind Suffixe; füge daher Satzzeichen und führendes Leerzeichen hinzu. Die sechs optionalen Detailfelder werden unabhängig aufgelöst und unterdrücken einander nicht.

Übliche Spielsprachenschlüssel sind `ar`, `cs-CZ`, `de-DE`, `el-GR`, `en-US`, `es-ES`, `fr-FR`, `hu-HU`, `it-IT`, `ja-JP`, `ko-KR`, `nl-NL`, `pl-PL`, `pt-BR`, `ru-RU`, `sv-SE`, `th-TH`, `tr-TR`, `uk-UA`, `zh-CN` und `zh-HK`.

### Assets und providerlokale Pfade

Pfade in `lordmeta.json` sind relativ zu `Override/`. Mit `AssetMode: "Local"` versieht der Script Extender Pfade für Gesicht, Beitritts-/Verlassensaudio sowie Nachrichtenvideo/-audio automatisch mit der Besitzer-GUID. Andere lokale Pakete können dieselben relativen Namen ohne Kollision verwenden.

Bei einer Textur ohne Endung werden `.png`, `.jpg`, `.tga` und `.dds` geprüft. DDS wird zur Laufzeit unterstützt, gehört aber nicht zur Custom-Lord-Upload-Allowlist von ExtendedData. Kann `FacePath` nicht geladen werden, fällt das Detailfenster auf die validierte `avatar.png` und danach auf das Fragezeichenporträt des Spiels zurück.

Die Audioauflösung prüft:

1. `Override/Locales/<current-language>/<path>`
2. `Override/Locales/en-US/<path>`
3. `Override/<path>`

Bei Audio ohne Endung wird `.ogg` vor `.wav` geprüft. OGG Vorbis wird empfohlen. WAV muss RIFF/WAVE PCM Format 1, mono oder stereo, 44.100 Hz und 16 Bit sein.

Videos verwenden `.webm` oder `.mp4`; WEBM mit VP8 bei 348x348 ist die dokumentierte sichere Wahl. Lege Nachrichtenvideos unter `Override/Assets/GUI/Video/` ab.

### Nachrichtenclips und Namen

Jede Property unter `Messages` muss ohne Beachtung der Groß-/Kleinschreibung einem `AILordMessageType`-Namen entsprechen. Unbekannte Schlüssel werden protokolliert und übersprungen. Mehrere Clips werden über die deterministische Zufallsquelle des Script Extenders ausgewählt.

| Clip-Feld | Verhalten |
|---|---|
| `VideoPath` | Asset-relativer Videopfad. Ein leerer String behält das ursprüngliche Video. |
| `AudioPath` | Asset-relativer Sprachpfad. Ein leerer String behält das ursprüngliche Audio. |
| `LocalizedText` | Optionales Untertitel-Dictionary mit Fallback von aktueller Sprache zu `en-US`. |

Stelle für konfigurierte Clips `en-US`-Untertitel bereit, sofern nicht absichtlich ein leerer String der aktuellen Sprache einen Ersatzuntertitel unterdrücken soll.

Aktuelle Nachrichtennamen sind:

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
KillNpc
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

Für `Unk21` und `Unk22` ist keine Gameplay-Bedeutung dokumentiert. Die Nickname-Member existieren, ein normaler KI-Nachrichtenauslöser ist jedoch nicht garantiert.

### Zwei Lua-Einstiegspunkte

- `My Lord/init.lua` ist das isolierte Lord-KI-Skript. Es kann `ai_init(playerId, loadMode)` definieren und erhält die 1-basierte Spieler-ID sowie den Script-Extender-Lademodus.
- `My Lord/Scripts/init.lua` ist das normale Asset-Mod-Lebenszyklusskript. Es kann `mod_init`, `mod_load` und `mod_unload` definieren.

Lua, das Ressourcen, Einheiten, Gebäude, Gelände, KI-Verhalten oder anderen Simulationszustand ändert, erfordert `NetworkMode: 1` und übereinstimmende Multiplayer-Installationen.

### Veröffentlichung mit ExtendedData

Ein Workshop-Element muss das Lord-Verzeichnis als direktes Kind seines Inhaltsverzeichnisses installieren. Lasse auf der Upload-Seite **Upload additional files for mod support** aktiviert, um unterstützte erweiterte Dateien bereitzustellen.

Das Spiel verarbeitet direkte `.lordjson`, `.aivjson` und eine optionale `avatar.png` an der Wurzel. ExtendedData ergänzt diese Allowlist ohne Beachtung der Groß-/Kleinschreibung:

| Ort | Von ExtendedData ergänzte Dateien |
|---|---|
| Lord-Wurzel | direkte `*.json` und `*.lua` |
| `Scripts/**` | `*.lua` |
| `MapAreas/**` | `*.sema` |
| `Locales/<locale>/` | exakt `crusader.txt` |
| `Override/**` | `.png`, `.jpg`, `.tga`, `.ogg`, `.wav`, `.webm`, `.mp4` |

Direkte `.data`- und `.ldata`-Dateien an der Wurzel sind lokale Uploader-Steuerdateien und werden nicht hochgeladen. Andere Dateien werden inventarisiert, aber ausgeschlossen. Die Bestätigungswarnung nennt Anzahl und Beispiele; die vollständige sortierte Liste wird in `BepInEx/LogOutput.log` geschrieben. Kann eine erlaubte Datei nicht sicher bereitgestellt werden, wird der Upload abgebrochen, statt ein unvollständiges Paket zu veröffentlichen.

Pakete, die DDS, XAML-Patches, Atlanten, AssetBundles, verschachtelte JSON-Ressourcen oder andere ausgeschlossene Dateien benötigen, brauchen einen separaten vollständigen Steam-UGC-Veröffentlichungsablauf.

### Prüfliste

1. Teste zuerst die direkten `.lordjson`, `.aivjson` und eine optionale gültige `avatar.png`.
2. Ergänze `info.json` und eine minimale `lordmeta.json`; prüfe `en-US` und eine übersetzte Sprache.
3. Ergänze Anzeigename, Titel, Porträt, Beitritts-/Verlassensaudio, Nachrichtenclips und Lua jeweils einzeln.
4. Teste die lokale Asset-Isolation mit einem anderen Lord, der dieselben relativen Dateinamen verwendet.
5. Teste lokalisierte und globale Audio-Fallbacks sowie die saubere Ablehnung nicht unterstützter WAV-Dateien.
6. Prüfe `NetworkMode` für jedes Lua- oder gameplay-verändernde Feature.
7. Prüfe jede ExtendedData-Preflight-Warnung und die vollständige Liste ausgeschlossener Dateien im Log.
8. Installiere das veröffentlichte Workshop-Element, kontrolliere seine tatsächlichen Dateien und wiederhole die Laufzeittests.

Aktuelle Upstream-Referenzen:

- [Extended AI Modding](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/extended-ai-modding.md)
- [Asset API](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/asset-api.md)
- [Lua quick start](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/lua-quick-start.md)
- [Workshop Mod Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/workshop-mod-creation-guide.md)
