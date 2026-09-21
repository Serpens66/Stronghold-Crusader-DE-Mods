# Mod-specific data for Custom Lord AICs / Mod-spezifische Daten für Custom-Lord-AICs

[English](#english) | [Deutsch](#deutsch)

## English

`name.modlord.json` stores static, mod-specific data for one Custom Lord AI configuration. Several mods can extend the same AIC without adding unknown fields to Vanilla's `.lordjson` format.

### Files and placement

| File | Purpose |
|---|---|
| `name.lordjson` | Vanilla AI configuration. |
| `lordmeta.json` | Script Extender metadata for the complete Custom Lord package. |
| `name.modlord.json` | Mod-specific data tied to one AIC. |

Place each sidecar directly beside its matching `.lordjson` and retain the complete base name:

```text
My Custom Lord/
  aggressive.lordjson
  aggressive.modlord.json
  aggressive.v2.lordjson
  aggressive.v2.modlord.json
  lordmeta.json
```

The sidecar is optional. Its absence must never prevent the underlying AIC from loading.

### JSON structure

The root is an object whose case-insensitive keys are mod GUIDs. Every value is an object owned and validated by that mod:

```json
{
  "com.example.first-mod": {
    "schemaVersion": 1,
    "aggressionMultiplier": 1.25,
    "preferredUnits": [
      "ArabianSwordsman",
      "HorseArcher"
    ]
  },
  "org.example.other-mod": {
    "schemaVersion": 2,
    "customFeature": {
      "enabled": true
    }
  }
}
```

There is no global schema version. Keys that differ only by casing conflict and invalidate the shared container. Empty GUIDs and non-object namespace values are invalid.

### Reading a namespace

Reference `ExtendedData.dll` and `ExtendedData.Core.dll`. If the exact `.lordjson` path is known:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    lordJsonPath,
    "author.example-mod");
```

Code that already has the game's `CustomLordConfig` can use the convenience overload:

```csharp
ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    customLordConfig,
    "author.example-mod");
```

ExtendedData replaces only the final `.lordjson` suffix with `.modlord.json`. The config overload first builds `Path.Combine(config.path, config.name + ".lordjson")`.

On success, `Data` is a deeply read-only object tree and `Json` contains only the requested namespace. Nested objects implement `IReadOnlyDictionary<string, object>` and arrays implement `IReadOnlyList<object>`. ExtendedData validates the shared container; the consuming mod validates its own fields and `schemaVersion`.

Possible status values are `Success`, `FileNotFound`, `NamespaceNotFound`, `InvalidDocument`, `ReadError`, and `InvalidRequest`. `Source` contains the derived sidecar path and `Diagnostic` explains failures. Missing sidecars and namespaces mean default behavior. Invalid data must disable only the consuming extension, never the underlying AIC.

### Authoring and validation

- Encode strict UTF-8 JSON without comments or trailing commas.
- Store only information that supplements the matching AIC; do not mirror Vanilla fields.
- Use a stable BepInEx plugin GUID and read only that namespace.
- Ignore unknown fields unless the mod-owned schema explicitly rejects them.
- Preserve all foreign namespaces and their unknown fields when updating one namespace.
- Keep package-wide presentation metadata in `lordmeta.json`.
- Test exact-path and `CustomLordConfig` access, missing files and namespaces, invalid JSON, and unsupported mod-owned schemas.
- Verify that packaging retains every sidecar beside its AIC and that the AIC still loads without the sidecar or its consuming mods.

---

## Deutsch

`name.modlord.json` speichert statische, mod-spezifische Daten für eine einzelne Custom-Lord-KI-Konfiguration. Mehrere Mods können dieselbe AIC erweitern, ohne unbekannte Felder zum Vanilla-Format `.lordjson` hinzuzufügen.

### Dateien und Ablageort

| Datei | Zweck |
|---|---|
| `name.lordjson` | Vanilla-KI-Konfiguration. |
| `lordmeta.json` | Script-Extender-Metadaten für das vollständige Custom-Lord-Paket. |
| `name.modlord.json` | Mod-spezifische Daten für eine einzelne AIC. |

Lege jedes Sidecar direkt neben die zugehörige `.lordjson` und behalte den vollständigen Basisnamen bei:

```text
My Custom Lord/
  aggressive.lordjson
  aggressive.modlord.json
  aggressive.v2.lordjson
  aggressive.v2.modlord.json
  lordmeta.json
```

Das Sidecar ist optional. Sein Fehlen darf das Laden der zugrunde liegenden AIC niemals verhindern.

### JSON-Struktur

Die Wurzel ist ein Objekt, dessen Schlüssel ohne Beachtung der Groß-/Kleinschreibung Mod-GUIDs darstellen. Jeder Wert ist ein Objekt, das dem jeweiligen Mod gehört und von ihm validiert wird:

```json
{
  "com.example.first-mod": {
    "schemaVersion": 1,
    "aggressionMultiplier": 1.25,
    "preferredUnits": [
      "ArabianSwordsman",
      "HorseArcher"
    ]
  },
  "org.example.other-mod": {
    "schemaVersion": 2,
    "customFeature": {
      "enabled": true
    }
  }
}
```

Es gibt keine globale Schemaversion. Schlüssel, die sich nur durch Groß-/Kleinschreibung unterscheiden, stehen im Konflikt und machen den gemeinsamen Container ungültig. Leere GUIDs und Namensraumwerte, die keine Objekte sind, sind ungültig.

### Einen Namensraum lesen

Referenziere `ExtendedData.dll` und `ExtendedData.Core.dll`. Wenn der genaue `.lordjson`-Pfad bekannt ist:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    lordJsonPath,
    "author.example-mod");
```

Code, der bereits über die `CustomLordConfig` des Spiels verfügt, kann den Komfort-Overload verwenden:

```csharp
ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    customLordConfig,
    "author.example-mod");
```

ExtendedData ersetzt ausschließlich das letzte `.lordjson`-Suffix durch `.modlord.json`. Der Config-Overload bildet zuerst `Path.Combine(config.path, config.name + ".lordjson")`.

Bei Erfolg enthält `Data` einen tief schreibgeschützten Objektbaum und `Json` ausschließlich den angeforderten Namensraum. Verschachtelte Objekte implementieren `IReadOnlyDictionary<string, object>`, Arrays implementieren `IReadOnlyList<object>`. ExtendedData validiert den gemeinsamen Container; der konsumierende Mod validiert seine eigenen Felder und `schemaVersion`.

Mögliche Statuswerte sind `Success`, `FileNotFound`, `NamespaceNotFound`, `InvalidDocument`, `ReadError` und `InvalidRequest`. `Source` enthält den abgeleiteten Sidecar-Pfad und `Diagnostic` erklärt Fehler. Fehlende Sidecars und Namensräume bedeuten Standardverhalten. Ungültige Daten dürfen nur die konsumierende Erweiterung deaktivieren, niemals die zugrunde liegende AIC.

### Erstellung und Validierung

- Verwende striktes UTF-8-JSON ohne Kommentare oder abschließende Kommas.
- Speichere ausschließlich Ergänzungen zur passenden AIC; spiegle keine Vanilla-Felder.
- Verwende eine stabile BepInEx-Plugin-GUID und lies nur diesen Namensraum.
- Ignoriere unbekannte Felder, sofern das mod-eigene Schema sie nicht ausdrücklich ablehnt.
- Erhalte beim Aktualisieren eines Namensraums alle fremden Namensräume und deren unbekannte Felder.
- Bewahre paketweite Darstellungsmetadaten in `lordmeta.json` auf.
- Teste den Zugriff über den genauen Pfad und `CustomLordConfig`, fehlende Dateien und Namensräume, ungültiges JSON sowie nicht unterstützte mod-eigene Schemata.
- Prüfe, dass die Paketierung jedes Sidecar neben seiner AIC erhält und die AIC weiterhin ohne Sidecar oder konsumierende Mods lädt.
