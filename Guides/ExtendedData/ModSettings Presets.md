# Extensible ModSettings presets / Erweiterbare ModSettings-Presets

[English](#english) | [Deutsch](#deutsch)

## English

APIShared gives every compatible target mod two normal actions: **Load preset** and **Save preset**. Loading materializes a preset into the current working settings. Those settings remain editable; editing them never changes the source JSON. Saving creates or deliberately replaces a personal preset.

ExtendedData is optional. It uses the same typed property contract for Maps and Trails, while normal preset support requires only APIShared.

### Target-mod integration

1. Add a reference and hard dependency on the required APIShared version.
2. Derive the settings ViewModel from `Shared.PresetLobbyModSettingsViewModel`.
3. In every persistent setter, call `CanMutateSetting()` before changing state and `OnPropertyChanged()` afterwards.
4. Register through `LobbyModSettingsPresetRegistration.Register`.
5. Copy the standard Load/Save XAML block from a preset-capable mod in this repository.

No Shared source links, preset compile symbols, or ExtendedData reference are required. A minimal header contains:

```xml
<Button IsEnabled="{Binding CanChangePreset}"
        Content="{Binding System_PresetLoadText}"
        Command="{Binding System_OpenPresetLoadCommand}"/>
<Button IsEnabled="{Binding CanChangePreset}"
        Content="{Binding System_PresetSaveText}"
        Command="{Binding System_OpenPresetSaveCommand}"/>
<TextBlock Text="{Binding System_PresetStatusText}"
           Visibility="{Binding System_PresetStatusVisibility}"/>
```

The complete block also binds `System_PresetLoadEntries`, `System_SelectedPresetLoadEntry`, the personal-only delete command, `System_PresetSaveTargets`, `System_PresetSaveSettings`, the bulk mode selector, inline overwrite/delete confirmation and error status, and the corresponding confirm/cancel commands. Successful Load, Reset, Save, Overwrite, and Delete operations do not show a result banner; failures remain visible and dismissible. Pressing an already open Load or Save button closes its panel again. No Vanilla popup is opened and the ModSettings hub remains visible.

### Sources and locations

The load list visibly distinguishes:

- **Personal presets**, stored at `LobbyModSettings/Presets/Override/<target GUID>/preset_<id>.json`;
- **Bundled with this mod**, stored below the target mod at `Override/<target GUID>/preset_<id>.json`;
- **External presets**, stored in the same `Override/<target GUID>/` structure of another registered loose asset mod, with its provider name shown.

Display names need not be unique. Identity is based on source kind, provider GUID, target GUID, and preset ID. Only personal entries can be overwritten or deleted. Deletion is permanent after explicit confirmation and leaves the materialized working settings unchanged. Saving an external or bundled preset therefore creates an independent personal file, even when the display name is identical.

Loose asset mods may serve several targets:

```text
MyPresetPack/
  info.json
  Override/
    BuildingCosts_Serp/
      preset_competitive-economy.json
    UnitLimit_Serp/
      preset_large-armies.json
```

Version 1 supports loose registered asset folders. `.semod` providers are skipped with a log message.

### JSON schema

```json
{
  "schemaVersion": 1,
  "id": "competitive-economy",
  "name": "Competitive Economy",
  "description": "Higher costs while retaining selected player preferences.",
  "targetGuid": "BuildingCosts_Serp",
  "minimumTargetVersion": "1.2.0",
  "maximumTargetVersion": "2.0.0",
  "settings": {
    "EnableMod": { "mode": "fixed", "value": true },
    "WoodCostMultiplier": { "mode": "fixed", "value": 1.5 },
    "ShowDetailedCosts": { "mode": "player" },
    "ExperimentalRule": { "mode": "modDefault" }
  }
}
```

- `fixed` applies the JSON value.
- `player` keeps the current normal working value. In a Map/Trail context it uses the normal value saved before entering that context.
- `modDefault` applies the target mod's captured code default.

Only listed properties are changed. All resulting settings remain editable according to their normal Host, Player, or Local ownership. Primitive values, strings, enums, and one-dimensional arrays use readable JSON; other MessagePack-compatible values may use `messagepack-base64:<data>`.

Unknown members, properties or modes, invalid values, incompatible target versions, unsafe paths, duplicate IDs within one provider/target, oversized files, and overly deep documents fail closed.

### Loading, saving, and migration

Selecting a row does nothing until **Load** is pressed. The status then shows the preset name and source; later edits add “modified”. If the source disappears, the materialized working values stay intact and only the source association is cleared.

The standard save dialog always writes every persistent property. Each row selects `Default`, `Player`, or `Fixed`; the `Host Fixed` bulk choice sets Host properties to `Fixed` and Player/Local properties to `Player`. Partial presets remain supported through the public save API and hand-authored JSON. Existing partial presets initialize omitted rows as `Player` when edited, so overwriting them does not unexpectedly fix previously omitted values. A new personal preset starts with an empty name, while selecting an existing personal preset fills its name and description for deliberate replacement. A nonempty name is required before Save becomes available.

Descriptions support line breaks and up to 8192 characters. The standard editor shows five lines and scrolls vertically for longer text. The dialog can create a new personal preset or select an existing personal preset. Existing files require a second overwrite confirmation and are atomically replaced. Bundled, external, Map, Trail, archive, and Coop-package data are never overwrite targets.

Old Preset 1 is migrated to `legacy-preset-1`; Preset 2 is migrated only when it existed. The formerly active slot becomes the editable working state. Old files from `LobbyModSettings/PresetExports/` are copied once into the personal folder and the originals are retained.

### Maps and Trails

Every compatible mod has one **Reset settings to** selector. **Mod defaults** is always available; **Trail settings** and **Map settings** appear only when a valid source exists. Selecting an entry changes nothing until **Reset** is pressed. Resetting replaces only the working copy: it never writes a Trail sidecar or Map archive, and it never changes personal presets.

Direct Map/Trail starts are read-only. Customize and Trail Maker use a freely editable temporary working copy, just like a normal Custom Game. For a Trail, Trail settings have initial precedence and embedded Map settings are ignored until the author explicitly loads **Map settings**. Only the visible **Include modsettings** options on the normal Trail Maker and Map Editor save paths may write or remove a sidecar or the ExtendedData archive entry. Saving a personal preset never modifies these mission sources. Leaving Customize restores the previous normal working values and status.

---

## Deutsch

APIShared gibt jedem kompatiblen Ziel-Mod zwei normale Aktionen: **Preset laden** und **Preset speichern**. Laden materialisiert ein Preset in die aktuellen Arbeitswerte. Diese bleiben frei bearbeitbar; Änderungen schreiben niemals in die Quelldatei. Speichern erstellt ein persönliches Preset oder ersetzt nach ausdrücklicher Bestätigung ein vorhandenes persönliches Preset.

ExtendedData ist optional. Es verwendet denselben typisierten Property-Vertrag für Maps und Trails; normale Presets benötigen nur APIShared.

### Integration des Ziel-Mods

1. APIShared referenzieren und als harte Abhängigkeit mit passender Mindestversion angeben.
2. Das Settings-ViewModel von `Shared.PresetLobbyModSettingsViewModel` ableiten.
3. In jedem persistenten Setter vor der Änderung `CanMutateSetting()` und danach `OnPropertyChanged()` aufrufen.
4. Mit `LobbyModSettingsPresetRegistration.Register` registrieren.
5. Den Standard-XAML-Block für Laden/Speichern aus einem presetfähigen Mod dieses Repositories übernehmen.

Shared-Quelllinks, Preset-Compile-Symbole und eine ExtendedData-Referenz sind nicht nötig. Das minimale XAML-Beispiel im englischen Abschnitt sowie die vollständigen Blöcke der vorhandenen Mods zeigen alle Bindings einschließlich des ausschließlich für eigene Presets sichtbaren Löschbefehls sowie der Inline-Bestätigung. Erfolgreiches Laden, Zurücksetzen, Speichern, Überschreiben und Löschen zeigt kein zusätzliches Ergebnisbanner; Fehler bleiben sichtbar und schließbar. Der ModSettings-Hub bleibt dabei sichtbar. Ein erneuter Klick auf den bereits geöffneten Laden- oder Speichern-Button schließt sein Panel wieder.

### Quellen und Ablageorte

Der Ladedialog unterscheidet sichtbar:

- **Eigene Presets** unter `LobbyModSettings/Presets/Override/<Ziel-GUID>/preset_<id>.json`;
- **Mit diesem Mod geliefert** unter `Override/<Ziel-GUID>/preset_<id>.json` des Ziel-Mods;
- **Externe Presets** in derselben Override-Struktur eines anderen registrierten losen Asset-Mods, einschließlich Providername.

Anzeigenamen müssen nicht eindeutig sein. Die Identität besteht aus Quellentyp, Provider-GUID, Ziel-GUID und Preset-ID. Nur eigene Presets dürfen überschrieben oder gelöscht werden. Das Löschen ist nach ausdrücklicher Bestätigung endgültig und verändert die bereits materialisierten Arbeitswerte nicht. Aus einem mitgelieferten oder externen Preset entsteht beim Speichern daher immer eine unabhängige persönliche Datei – ausdrücklich auch mit demselben Anzeigenamen.

Ein loser Asset-Mod kann Presets für mehrere Ziele enthalten; das Verzeichnisbeispiel steht im englischen Abschnitt. `.semod`-Provider werden in Version 1 mit einem Loghinweis übersprungen.

### JSON-Schema und Modi

Das vollständige JSON-Beispiel im englischen Abschnitt gilt unverändert.

- `fixed` übernimmt den JSON-Wert.
- `player` behält den aktuellen normalen Arbeitswert; im Map-/Trail-Kontext ist das der vor Eintritt gesicherte normale Wert.
- `modDefault` übernimmt den beim Start erfassten Code-Standard des Ziel-Mods.

Nur aufgeführte Properties werden geändert. Danach bleiben alle Werte gemäß ihrer normalen Host-, Player- oder Local-Besitzregeln editierbar. Primitive Werte, Strings, Enums und eindimensionale Arrays bleiben lesbares JSON; andere MessagePack-kompatible Typen dürfen `messagepack-base64:<daten>` verwenden.

Unbekannte Member, Properties oder Modi, ungültige Werte, unpassende Zielversionen, unsichere Pfade, doppelte IDs desselben Providers/Ziels, zu große Dateien und zu tiefe Dokumente werden fail-closed abgelehnt.

### Laden, Speichern und Migration

Die Auswahl eines Eintrags ändert noch nichts; erst **Laden** übernimmt ihn. Die Statuszeile zeigt danach Name und Quelle und kennzeichnet spätere Änderungen mit „geändert“. Verschwindet die Quelle, bleiben die materialisierten Arbeitswerte erhalten; nur die Quellenverknüpfung wird entfernt.

Der Standardspeicherdialog schreibt immer alle persistenten Properties. Pro Zeile stehen `Standard`, `Spieler` und `Fest` zur Wahl. Die Sammelwahl `Host fest` setzt Host-Properties auf `Fest` und Player-/Local-Properties auf `Spieler`. Teil-Presets bleiben über die öffentliche Speicher-API und handgeschriebenes JSON möglich. Beim Bearbeiten eines vorhandenen Teil-Presets werden fehlende Properties als `Spieler` vorbelegt, damit das Überschreiben zuvor ausgelassene Werte nicht unerwartet fixiert. Ein neues eigenes Preset beginnt mit leerem Namen; erst die Auswahl eines vorhandenen eigenen Presets übernimmt dessen Namen und Beschreibung zum bewussten Ersetzen. Speichern wird erst mit einem nicht leeren Namen aktiviert.

Beschreibungen unterstützen Zeilenumbrüche und bis zu 8192 Zeichen. Der Standardeditor zeigt fünf Zeilen und scrollt bei längerem Text vertikal. Der Speicherdialog erstellt ein neues eigenes Preset oder wählt gezielt ein vorhandenes eigenes Preset. Das vollständige atomare Ersetzen erfordert eine zweite Bestätigung. Mitgelieferte, externe, Map-, Trail-, Archiv- und Koop-Paket-Daten sind niemals Überschreibziele.

Altes Preset 1 wird als `legacy-preset-1` migriert, Preset 2 nur wenn es vorhanden war. Der vorher aktive Slot wird zum editierbaren Arbeitsstand. Alte Dateien aus `LobbyModSettings/PresetExports/` werden einmalig in den persönlichen Ordner kopiert; die Originale bleiben erhalten.

### Maps und Trails

Jeder kompatible Mod besitzt einen einheitlichen Wähler **Einstellungen zurücksetzen auf**. **Mod-Standards** ist immer verfügbar; **Trail-Einstellungen** und **Map-Einstellungen** erscheinen nur bei einer gültigen Quelle. Die Auswahl allein ändert nichts, erst **Zurücksetzen** ersetzt die Arbeitskopie. Dabei werden weder persönliche Presets noch Trail-Sidecar oder Maparchiv verändert.

Direkt gestartete Maps und Trails sind schreibgeschützt. Customize und Trail Maker verwenden dagegen eine vollständig editierbare temporäre Arbeitskopie wie ein normales Custom Game. Bei einem Trail haben zunächst ausschließlich die Trail-Einstellungen Vorrang; Map-Einstellungen werden erst übernommen, wenn der Autor sie ausdrücklich lädt. Nur die sichtbaren Optionen **Modsettings einschließen** an den normalen Speicherpfaden von Trail Maker und Map Editor dürfen ein Sidecar beziehungsweise den ExtendedData-Archiveintrag schreiben oder entfernen. Persönliches Preset-Speichern verändert diese Missionsquellen nie. Beim Verlassen werden die vorherigen normalen Arbeitswerte und ihr Status wiederhergestellt.
