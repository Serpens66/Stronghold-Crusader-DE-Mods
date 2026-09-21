# Mod-specific data for maps / Mod-spezifische Daten für Maps

[English](#english) | [Deutsch](#deutsch)

## English

`modmap.json` stores static, map-specific data that mods can interpret. It is not a lobby-settings file, save data, or general Script Extender metadata. Typical uses include spawn regions, scenario rules, identifiers, and other structured information outside the base map format.

### Where the file is stored

A Script Extender map consists of the base map followed by an appended ZIP archive:

```text
example.map
├─ base map data
└─ appended ZIP archive
   ├─ info.json
   ├─ init.lua
   └─ modmap.json
```

Place `modmap.json` at the root of the appended archive beside `info.json` and `init.lua`, not merely beside the `.map` file. See the current [Script Extender Map Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/map-creation-guide.md?ref_type=heads) for archive creation and packaging.

The base map can still be opened without ExtendedData; only the supplemental namespaced values are unavailable.

### File responsibilities

| Data | Purpose |
|---|---|
| `info.json` | General Script Extender map/package metadata. |
| `modmap.json` | Static, mod-specific source data authored with the map. |
| `_SE_ModData_<ModId>.msgpack` | Mutable runtime/save data managed by the Script Extender `ModSaveDataAPI`. |
| `_SE_ModData_ExtendedData-MapModSettings.msgpack` | ExtendedData's schema-3 Map settings document; its content is UTF-8 JSON. |

`modmap.json` does not replace multiplayer synchronization or mutable save state.

### JSON structure

The root is an object whose case-insensitive keys are mod GUIDs. Every value is an object owned by that mod:

```json
{
  "author.example-mod": {
    "schemaVersion": 1,
    "spawnRegions": [
      {
        "name": "northern-reinforcements",
        "x": 120,
        "y": 340
      }
    ]
  },
  "org.example.spawn-control": {
    "schemaVersion": 2,
    "waves": [
      {
        "startTick": 1200,
        "unit": "ArabianSwordsman",
        "count": 20
      }
    ]
  }
}
```

There is no global schema version. Prefer a stable BepInEx plugin GUID in lowercase ASCII. Keys differing only by casing conflict and invalidate the container. Empty GUIDs and non-object namespace values are invalid.

### Reading a namespace

Reference `ExtendedData.dll` and `ExtendedData.Core.dll`, then request only your namespace:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result =
    ExtendedDataModDataApi.ReadCurrentMapNamespace("author.example-mod");

if (result.Success)
{
    IReadOnlyDictionary<string, object> data = result.Data;
    string namespaceJson = result.Json;
}
```

`Data` is a deeply read-only snapshot. Nested objects implement `IReadOnlyDictionary<string, object>` and arrays implement `IReadOnlyList<object>`. `Json` contains only the requested namespace. ExtendedData validates the shared container but leaves `schemaVersion` and all mod-owned fields to the consumer.

Possible status values are:

- `Success`: the requested namespace was returned.
- `FileNotFound`: no active archive or no `modmap.json` exists.
- `NamespaceNotFound`: the document is valid but has no matching GUID.
- `InvalidDocument`: JSON, UTF-8, the root object, GUID keys, or namespace objects are invalid.
- `ReadError`: the archive could not be read.
- `InvalidRequest`: the supplied mod GUID is empty.

Missing files and namespaces mean normal default behavior. For other failures, log `Diagnostic` and disable only the dependent feature.

Call the API after the Script Extender has loaded the Map archive. It reads on demand and does not cache or poll. Retain a validated model only for the lifetime of the corresponding Map.

### Authoring and validation

- Use strict UTF-8 JSON without comments or trailing commas.
- Store static Map information, not user preferences or mutable runtime state.
- Read and assign meaning only to your own GUID namespace.
- Ignore unknown fields unless your schema explicitly rejects them.
- When updating one namespace, preserve all foreign namespaces and their unknown fields.
- Handle gameplay-relevant data consistently on every multiplayer participant.
- Test missing files, missing namespaces, malformed data, and every combination of consuming mods.
- Inspect the final archive and verify that all expected entries remain present.

---

## Deutsch

`modmap.json` speichert statische, map-spezifische Daten, die Mods auswerten können. Sie ist keine Lobby-Einstellungsdatei, kein Spielstand und keine allgemeine Script-Extender-Metadatendatei. Typische Anwendungen sind Spawn-Bereiche, Szenarioregeln, Kennungen und andere strukturierte Informationen außerhalb des grundlegenden Map-Formats.

### Ablageort der Datei

Eine Script-Extender-Map besteht aus der zugrunde liegenden Map und einem angehängten ZIP-Archiv:

```text
example.map
├─ base map data
└─ appended ZIP archive
   ├─ info.json
   ├─ init.lua
   └─ modmap.json
```

Lege `modmap.json` an der Wurzel des angehängten Archivs neben `info.json` und `init.lua` ab, nicht lediglich neben der `.map`-Datei. Der aktuelle [Script Extender Map Creation Guide](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/map-creation-guide.md?ref_type=heads) beschreibt Erstellung und Paketierung des Archivs.

Die zugrunde liegende Map kann weiterhin ohne ExtendedData geöffnet werden; lediglich die zusätzlichen Namensraumwerte stehen dann nicht zur Verfügung.

### Aufgaben der Dateien

| Daten | Zweck |
|---|---|
| `info.json` | Allgemeine Script-Extender-Metadaten für Map oder Paket. |
| `modmap.json` | Statische, mod-spezifische Quelldaten, die mit der Map erstellt werden. |
| `_SE_ModData_<ModId>.msgpack` | Veränderliche Laufzeit-/Speicherdaten der Script-Extender-`ModSaveDataAPI`. |
| `_SE_ModData_ExtendedData-MapModSettings.msgpack` | Schema-3-Dokument für ExtendedData-Map-Einstellungen; der Inhalt ist UTF-8-JSON. |

`modmap.json` ersetzt weder Multiplayer-Synchronisierung noch veränderlichen Speicherzustand.

### JSON-Struktur

Die Wurzel ist ein Objekt, dessen Schlüssel ohne Beachtung der Groß-/Kleinschreibung Mod-GUIDs darstellen. Jeder Wert ist ein Objekt im Besitz des jeweiligen Mods:

```json
{
  "author.example-mod": {
    "schemaVersion": 1,
    "spawnRegions": [
      {
        "name": "northern-reinforcements",
        "x": 120,
        "y": 340
      }
    ]
  },
  "org.example.spawn-control": {
    "schemaVersion": 2,
    "waves": [
      {
        "startTick": 1200,
        "unit": "ArabianSwordsman",
        "count": 20
      }
    ]
  }
}
```

Es gibt keine globale Schemaversion. Bevorzuge eine stabile BepInEx-Plugin-GUID in ASCII-Kleinbuchstaben. Schlüssel, die sich nur durch Groß-/Kleinschreibung unterscheiden, stehen im Konflikt und machen den Container ungültig. Leere GUIDs und Namensraumwerte, die keine Objekte sind, sind ungültig.

### Einen Namensraum lesen

Referenziere `ExtendedData.dll` und `ExtendedData.Core.dll` und fordere anschließend ausschließlich deinen Namensraum an:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result =
    ExtendedDataModDataApi.ReadCurrentMapNamespace("author.example-mod");

if (result.Success)
{
    IReadOnlyDictionary<string, object> data = result.Data;
    string namespaceJson = result.Json;
}
```

`Data` ist eine tief schreibgeschützte Momentaufnahme. Verschachtelte Objekte implementieren `IReadOnlyDictionary<string, object>`, Arrays implementieren `IReadOnlyList<object>`. `Json` enthält ausschließlich den angeforderten Namensraum. ExtendedData validiert den gemeinsamen Container, überlässt `schemaVersion` und alle mod-eigenen Felder jedoch dem Konsumenten.

Mögliche Statuswerte sind:

- `Success`: Der angeforderte Namensraum wurde zurückgegeben.
- `FileNotFound`: Es gibt kein aktives Archiv oder keine `modmap.json`.
- `NamespaceNotFound`: Das Dokument ist gültig, besitzt aber keine passende GUID.
- `InvalidDocument`: JSON, UTF-8, Wurzelobjekt, GUID-Schlüssel oder Namensraumobjekte sind ungültig.
- `ReadError`: Das Archiv konnte nicht gelesen werden.
- `InvalidRequest`: Die übergebene Mod-GUID ist leer.

Fehlende Dateien und Namensräume bedeuten normales Standardverhalten. Protokolliere bei anderen Fehlern `Diagnostic` und deaktiviere ausschließlich das abhängige Feature.

Rufe die API auf, nachdem der Script Extender das Map-Archiv geladen hat. Sie liest bei Bedarf und verwendet weder Cache noch Polling. Bewahre ein validiertes Modell nur für die Lebensdauer der zugehörigen Map auf.

### Erstellung und Validierung

- Verwende striktes UTF-8-JSON ohne Kommentare oder abschließende Kommas.
- Speichere statische Map-Informationen, keine Benutzereinstellungen oder veränderlichen Laufzeitzustände.
- Lies und interpretiere ausschließlich deinen eigenen GUID-Namensraum.
- Ignoriere unbekannte Felder, sofern dein Schema sie nicht ausdrücklich ablehnt.
- Erhalte beim Aktualisieren eines Namensraums alle fremden Namensräume und deren unbekannte Felder.
- Verarbeite gameplay-relevante Daten auf allen Multiplayer-Teilnehmern konsistent.
- Teste fehlende Dateien, fehlende Namensräume, fehlerhafte Daten und jede Kombination konsumierender Mods.
- Untersuche das endgültige Archiv und prüfe, dass alle erwarteten Einträge erhalten sind.
