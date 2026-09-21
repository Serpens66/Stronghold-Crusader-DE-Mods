# Extensible ModSettings Presets / Erweiterbare ModSettings-Presets

[English](#english) | [Deutsch](#deutsch)

## English

APIShared lets a target mod offer local Preset 1/2 plus presets supplied as loose, human-readable JSON files. ExtendedData uses the same property scopes, modes, and conversion rules for Map and Trail presets, but it is not required for normal presets.

### Target-mod integration

Follow [ExtendedData compatibility for mod authors](Mod%20Compatibilty%20ExtendedData.md#english): reference APIShared 0.4.0+, derive the ViewModel from `PresetLobbyModSettingsViewModel`, use guarded persistent setters, and call `LobbyModSettingsPresetRegistration.Register`.

The dropdown is dynamic: Preset 1, Preset 2, compatible external presets, and—when applicable—a temporary Map/Trail entry. The stable external identity is stored in the normal local MessagePack file. If that preset disappears, the last local Preset 1/2 is restored.

### Target-mod XAML

Use `xmlns:shared="clr-namespace:Shared;assembly=APIShared"`. The essential row is:

```xml
<ComboBox IsEnabled="{Binding CanChangePreset}"
          Visibility="{Binding PresetVisibility}"
          ItemsSource="{Binding PresetOptions}"
          SelectedIndex="{Binding SelectedPreset, Mode=TwoWay}"/>
<Button Content="{Binding System_PresetActionText}"
        Command="{Binding System_PresetActionCommand}"/>
```

The standard export editor additionally binds:

- `System_PresetExportPanelVisibility`, `System_PresetExportName`, and `System_PresetExportDescription`;
- `System_PresetExportSettings` with `IsSelected`, `PropertyName`, `ScopeText`, `ModeOptions`, and `SelectedModeIndex`;
- the All, Host only, Export, and Cancel commands exposed by the base ViewModel.

The repository's preset-capable mod XAML files contain a complete copyable block.

### JSON location and schema

Place every file at:

```text
Override/<target BepInEx GUID>/preset_<unique-id>.json
```

Example:

```json
{
  "schemaVersion": 1,
  "id": "competitive-economy",
  "name": "Competitive Economy",
  "description": "Higher costs with the player's preferred local UI settings.",
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

Modes:

- `modDefault`: use the target mod's captured code default;
- `player`: use the same property from the player's underlying local Preset 1/2;
- `fixed`: use the JSON `value`.

Only listed properties are applied and locked. Unlisted properties remain unchanged and editable. Host properties can be selected only by the host and then use normal Script Extender synchronization. Personal and local values apply only to that player.

Primitive values, strings, enums, and one-dimensional arrays are readable JSON. APIShared writes other MessagePack-compatible values as `messagepack-base64:<data>`. Do not hand-edit that payload.

Unknown members, properties, modes, malformed values, unsafe paths, oversized/deep documents, duplicate IDs in one provider/target, and incompatible target versions fail closed and are logged. Equal display names from different providers remain available with the provider name appended.

### Distribution

For direct installation, copy the JSON file unchanged into the target mod's matching `Override/<target GUID>/` folder.

A loose Script Extender asset mod can provide presets for several targets:

```text
MyPresetPack/
  info.json
  Override/
    BuildingCosts_Serp/
      preset_competitive-economy.json
    UnitLimit_Serp/
      preset_large-armies.json
```

Version 1 scans loose registered asset-mod folders only. `.semod` providers are deliberately skipped with a log message because the Script Extender does not expose safe archive enumeration through this contract.

### Copy and export

For an external or Map/Trail preset, the action copies the materialized result into Preset 1 or 2 after slot selection and overwrite confirmation. The local slot then becomes active and editable.

For local Preset 1/2, the action opens the export editor. Choose a name, optional description, properties, and a mode per property. Output is written atomically to:

```text
LobbyModSettings/PresetExports/Override/<target GUID>/preset_<sanitized-name>.json
```

Existing files require confirmation and are replaced completely. Use distinctive names and stable IDs to avoid confusing users; a provider must not contain the same ID twice for one target.

---

## Deutsch

APIShared ermöglicht einem Ziel-Mod lokale Presets 1/2 sowie Presets aus losen, menschenlesbaren JSON-Dateien. ExtendedData verwendet dieselben Property-Scopes, Modi und Konvertierungsregeln für Map-/Trail-Presets, ist für normale Presets aber nicht erforderlich.

### Integration des Ziel-Mods

Folge [ExtendedData-Kompatibilität für Modentwickler](Mod%20Compatibilty%20ExtendedData.md#deutsch): APIShared ab 0.4.0 referenzieren, das ViewModel von `PresetLobbyModSettingsViewModel` ableiten, persistente Setter absichern und über `LobbyModSettingsPresetRegistration.Register` registrieren.

Das Dropdown enthält dynamisch Preset 1, Preset 2, kompatible externe Presets und bei Bedarf einen temporären Map-/Trail-Eintrag. Die stabile externe Identität wird in der normalen lokalen MessagePack-Datei gespeichert. Fehlt das externe Preset später, wird das zuletzt aktive lokale Preset 1/2 wiederhergestellt.

### XAML des Ziel-Mods

Verwende `xmlns:shared="clr-namespace:Shared;assembly=APIShared"`. Der minimale Dropdown-/Aktionsblock und die Bindings des vollständigen Exporteditors stehen im englischen Abschnitt. Die presetfähigen Mods dieses Repositories enthalten einen vollständig kopierbaren Standardblock.

### JSON-Ablage und Schema

Jede Datei liegt unter:

```text
Override/<BepInEx-GUID des Ziels>/preset_<eindeutige-id>.json
```

Das vollständige Beispiel im englischen Abschnitt gilt unverändert. Die Modi bedeuten:

- `modDefault`: erfasster Code-Standard des Ziel-Mods;
- `player`: dieselbe Property aus dem zugrunde liegenden lokalen Preset 1/2 des Spielers;
- `fixed`: der Wert aus `value`.

Nur aufgeführte Properties werden angewendet und gesperrt. Nicht aufgeführte Properties bleiben unverändert und editierbar. Host-Properties können nur vom Host ausgewählt werden und werden danach normal über den Script Extender synchronisiert. Persönliche und lokale Werte gelten nur für den jeweiligen Spieler.

Primitive Werte, Strings, Enums und eindimensionale Arrays bleiben lesbares JSON. Andere MessagePack-kompatible Werte schreibt APIShared als `messagepack-base64:<data>`; dieser Payload sollte nicht von Hand bearbeitet werden.

Unbekannte Member, Properties oder Modi, ungültige Werte, unsichere Pfade, zu große/tiefe Dokumente, doppelte IDs desselben Providers/Ziels und unpassende Zielversionen werden fail-closed abgelehnt und protokolliert. Gleiche Anzeigenamen verschiedener Provider bleiben erhalten und erhalten einen Providerzusatz.

### Verteilung

Für eine direkte Installation wird die JSON-Datei unverändert in den passenden Ordner `Override/<Ziel-GUID>/` des Ziel-Mods kopiert.

Ein loser Script-Extender-Asset-Mod darf dieselbe `Override`-Struktur für mehrere Ziel-Mods enthalten; das Verzeichnisbeispiel steht im englischen Abschnitt. Version 1 durchsucht ausschließlich lose registrierte Asset-Mod-Ordner. `.semod`-Provider werden mit einem Loghinweis übersprungen, weil der Script Extender für diesen Vertrag keine sichere Archiv-Auflistung bereitstellt.

### Kopieren und Exportieren

Bei einem externen oder Map-/Trail-Preset kopiert die Aktion das materialisierte Ergebnis nach Slotwahl und Überschreibbestätigung in Preset 1 oder 2. Anschließend ist der lokale Slot aktiv und frei editierbar.

Bei lokalem Preset 1/2 öffnet die Aktion den Exporteditor. Wähle Name, optionale Beschreibung, Properties und je einen Modus. Die atomar geschriebene Ausgabe liegt unter:

```text
LobbyModSettings/PresetExports/Override/<Ziel-GUID>/preset_<bereinigter-name>.json
```

Vorhandene Dateien werden nur nach Bestätigung vollständig ersetzt. Verwende möglichst eindeutige Namen und stabile IDs; ein Provider darf dieselbe ID für ein Ziel nicht doppelt enthalten.
