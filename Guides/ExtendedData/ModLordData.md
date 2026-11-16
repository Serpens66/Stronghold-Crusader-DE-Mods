# Mod-specific data for Custom Lord AICs / Mod-spezifische Daten für Custom-Lord-AICs

[English](#english) | [Deutsch](#deutsch)

## English

You do not need `name.modlord.json` for an ordinary Custom Lord. Use this optional file only when a gameplay mod asks you to provide extra data for one of your Lord's AI configurations. Follow that mod's instructions for the actual fields; there is no universal set of settings.

### Add mod data to a Lord

1. Create and test the Lord's `.lordjson` file first.
2. Place the mod's JSON data in a file with the same base name and the suffix `.modlord.json`, beside the `.lordjson` file. For example, `aggressive.lordjson` uses `aggressive.modlord.json`.
3. Keep both files together when sharing the Lord. If the optional file is absent, the Lord's normal AI configuration still loads.

For portraits, translations, speech, and Workshop packaging, use [Custom Lord packages with Script Extender](CustomLordExtendedPackages.md#english).

### For advanced users and mod authors

The reference below describes the shared JSON container and the API that mods use to read their own data. Several mods can extend the same AI configuration without adding fields to the game's `.lordjson` format.

#### Files and placement

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

#### JSON structure

The example fields belong to imaginary mods. Replace them with the fields required by the mod you actually use.

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

#### Reading a namespace

Install the complete current APIShared package. Declare the same supported minimum version of `APIShared_Serp` in BepInEx and `info.json`, and reference `APIShared.dll` and `APIShared.Content.dll` with `Private=false`. No ExtendedData assembly reference is required. During a selected Lord session, read the Lord assigned to a one-based player slot:

```csharp
using APIShared.ModData;

ModDataReadResult result =
    ModDataAccess.ReadSelectedLordNamespace(playerId, "author.example-mod");
```

ExtendedData supplies verified local selections or synchronized host snapshots, including `.modlord.json` values embedded in a Trail, through an optional provider. Without a ready provider, the call returns `HostDataUnavailable`; no local file substitutes for missing host confirmation. Use the player-ID API when several selected Lords share a configuration name. A provider registration alone does not establish selection readiness.

For an explicitly local read with a known `.lordjson` path, use:

```csharp
using APIShared.ModData;

ModDataReadResult result = ModDataAccess.ReadLocalLordNamespace(
    lordJsonPath,
    "author.example-mod");
```

This reads the adjacent sidecar regardless of a selected session and carries no host-authority claim. It replaces only the final `.lordjson` suffix with `.modlord.json`. Both local and selected readers enforce 64 KiB per UTF-8 document and 16 KiB per compact JSON namespace. ExtendedData's network snapshot has a separate 256 KiB total limit.

Call on the Unity thread after Extender initialization, with the matching confirmed mission/selection context. Reads are on demand. Keep returned data with that context and discard it on end, replacement or selection change. See the [shared API](https://github.com/SHCDE-APIShared/APIShared/blob/main/docs/MOD_DATA.md) for provider and lifecycle contracts.

#### ExtendedData facade

Consumers referencing `ExtendedData.dll` and `ExtendedData.Core.dll` can use `ExtendedDataModDataApi.ReadSelectedLordNamespace` or its path/config overloads. For the game's `CustomLordConfig`:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    customLordConfig,
    "author.example-mod");
```

Outside a selected session, the facade reads the sidecar beside the supplied configuration. During a selected session, it checks that the configuration belongs to the confirmed selection. Unselected or ambiguous host configurations do not fall back to local files. Prefer the player-ID API for the Lord currently assigned to a player.

On success, `Data` is a deeply read-only object tree and `Json` contains only the requested namespace. Nested objects implement `IReadOnlyDictionary<string, object>` and arrays implement `IReadOnlyList<object>`. APIShared validates the shared container; the consuming mod validates its own fields and `schemaVersion`.

Possible status values are `Success`, `FileNotFound`, `NamespaceNotFound`, `InvalidDocument`, `ReadError`, `InvalidRequest`, and `HostDataUnavailable`. `Source` identifies the sidecar or selected host slot when available, and `Diagnostic` explains failures. Missing sidecars and namespaces mean default behavior. `HostDataUnavailable` means the selected host snapshot is not ready or the configuration cannot be identified uniquely; wait for valid session data or use the player-ID API. Do not substitute unsynchronized local files. Invalid data must disable only the consuming extension, never the underlying AIC.

#### Authoring and validation

- Encode strict UTF-8 JSON without comments or trailing commas.
- Store only information that supplements the matching AIC; do not mirror Vanilla fields.
- Use a stable BepInEx plugin GUID and read only that namespace.
- Ignore unknown fields unless the mod-owned schema explicitly rejects them.
- Preserve all foreign namespaces and their unknown fields when updating one namespace.
- Keep package-wide presentation metadata in `lordmeta.json`.
- Test selected player-ID, exact-path, and `CustomLordConfig` access, unavailable host data, missing files and namespaces, invalid JSON, and unsupported mod-owned schemas.
- Verify that packaging retains every sidecar beside its AIC and that the AIC still loads without the sidecar or its consuming mods.

---

## Deutsch

Für einen normalen Custom Lord benötigst du keine `name.modlord.json`. Verwende diese optionale Datei nur, wenn ein Gameplay-Mod zusätzliche Daten für eine KI-Konfiguration deines Lords verlangt. Richte dich beim Inhalt nach der Anleitung dieses Mods; es gibt keine allgemeingültigen Einstellungsfelder.

### Mod-Daten zu einem Lord hinzufügen

1. Erstelle und teste zuerst die `.lordjson`-Datei des Lords.
2. Speichere die JSON-Daten des Mods neben der `.lordjson`-Datei mit demselben Basisnamen und der Endung `.modlord.json`. Zu `aggressive.lordjson` gehört beispielsweise `aggressive.modlord.json`.
3. Gib beide Dateien zusammen weiter. Fehlt die optionale Datei, lädt die normale KI-Konfiguration des Lords trotzdem.

Für Porträts, Übersetzungen, Sprachausgabe und Workshop-Pakete nutze [Custom-Lord-Pakete mit Script Extender](CustomLordExtendedPackages.md#deutsch).

### Für Fortgeschrittene und Modentwickler

Die Referenz unten erklärt den gemeinsamen JSON-Container und die API, mit der Mods ihre eigenen Daten lesen. Mehrere Mods können dieselbe KI-Konfiguration erweitern, ohne Felder zum `.lordjson`-Format des Spiels hinzuzufügen.

#### Dateien und Ablageort

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

#### JSON-Struktur

Die Beispielfelder gehören zu erfundenen Mods. Ersetze sie durch die Felder, die der tatsächlich verwendete Mod verlangt.

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

#### Einen Namensraum lesen

Installiere das vollständige aktuelle APIShared-Paket. Deklariere dieselbe unterstützte Mindestversion von `APIShared_Serp` in BepInEx und `info.json` und referenziere `APIShared.dll` sowie `APIShared.Content.dll` mit `Private=false`. Eine ExtendedData-Assemblyreferenz ist nicht erforderlich. Lies während einer ausgewählten Lord-Sitzung den Lord eines einsbasierten Spielerplatzes:

```csharp
using APIShared.ModData;

ModDataReadResult result =
    ModDataAccess.ReadSelectedLordNamespace(playerId, "author.example-mod");
```

ExtendedData liefert geprüfte lokale Auswahlen oder synchronisierte Host-Snapshots einschließlich der in einen Trail eingebetteten `.modlord.json`-Werte über einen optionalen Anbieter. Ohne bereiten Anbieter liefert der Aufruf `HostDataUnavailable`; eine lokale Datei ersetzt keine fehlende Hostbestätigung. Wenn mehrere ausgewählte Lords denselben Konfigurationsnamen besitzen, verwende die Spieler-ID-API. Die Registrierung eines Anbieters allein bestätigt keine Bereitschaft der Auswahl.

Für einen ausdrücklich lokalen Zugriff mit bekanntem `.lordjson`-Pfad verwende:

```csharp
using APIShared.ModData;

ModDataReadResult result = ModDataAccess.ReadLocalLordNamespace(
    lordJsonPath,
    "author.example-mod");
```

Damit wird das benachbarte Sidecar unabhängig von einer ausgewählten Sitzung gelesen; der Zugriff bestätigt keine Hostautorität. Ausschließlich das letzte `.lordjson`-Suffix wird durch `.modlord.json` ersetzt. Lokale und ausgewählte Leser prüfen 64 KiB je UTF-8-Dokument und 16 KiB je kompaktem JSON-Namensraum. Für ExtendedDatas Netzwerksnapshot gilt ein separates Gesamtlimit von 256 KiB.

Rufe die API nach der Extender-Initialisierung auf dem Unity-Thread mit dem passenden bestätigten Missions-/Auswahlkontext auf. Die Leser arbeiten bei Bedarf. Ordne zurückgegebene Daten diesem Kontext zu und verwerfe sie bei Ende, Ersetzung oder Auswahlwechsel. Die [gemeinsame API](https://github.com/SHCDE-APIShared/APIShared/blob/main/docs/MOD_DATA.md) beschreibt Anbieter- und Lifecycle-Verträge.

#### ExtendedData-Fassade

Verbraucher mit Referenzen auf `ExtendedData.dll` und `ExtendedData.Core.dll` können `ExtendedDataModDataApi.ReadSelectedLordNamespace` oder dessen Pfad-/Config-Overloads verwenden. Für die `CustomLordConfig` des Spiels:

```csharp
using ExtendedData;

ExtendedDataModDataReadResult result = ExtendedDataModDataApi.ReadLordNamespace(
    customLordConfig,
    "author.example-mod");
```

Außerhalb einer ausgewählten Sitzung liest die Fassade die Zusatzdatei neben der übergebenen Konfiguration. Während einer ausgewählten Sitzung prüft sie, ob die Konfiguration zur bestätigten Auswahl gehört. Nicht ausgewählte oder mehrdeutige Hostkonfigurationen werden nicht durch lokale Dateien ersetzt. Verwende für den aktuell einem Spieler zugewiesenen Lord bevorzugt die Spieler-ID-API.

Bei Erfolg enthält `Data` einen tief schreibgeschützten Objektbaum und `Json` ausschließlich den angeforderten Namensraum. Verschachtelte Objekte implementieren `IReadOnlyDictionary<string, object>`, Arrays implementieren `IReadOnlyList<object>`. APIShared validiert den gemeinsamen Container; der konsumierende Mod validiert seine eigenen Felder und `schemaVersion`.

Mögliche Statuswerte sind `Success`, `FileNotFound`, `NamespaceNotFound`, `InvalidDocument`, `ReadError`, `InvalidRequest` und `HostDataUnavailable`. `Source` bezeichnet das Sidecar oder den ausgewählten Host-Platz, soweit verfügbar, und `Diagnostic` erklärt Fehler. Fehlende Sidecars und Namensräume bedeuten Standardverhalten. `HostDataUnavailable` bedeutet, dass der ausgewählte Host-Snapshot noch nicht bereitsteht oder die Konfiguration nicht eindeutig bestimmt werden kann; warte auf gültige Sitzungsdaten oder verwende die Spieler-ID-API. Ersetze sie nicht durch unsynchronisierte lokale Dateien. Ungültige Daten dürfen nur die konsumierende Erweiterung deaktivieren, niemals die zugrunde liegende AIC.

#### Erstellung und Validierung

- Verwende striktes UTF-8-JSON ohne Kommentare oder abschließende Kommas.
- Speichere ausschließlich Ergänzungen zur passenden AIC; spiegle keine Vanilla-Felder.
- Verwende eine stabile BepInEx-Plugin-GUID und lies nur diesen Namensraum.
- Ignoriere unbekannte Felder, sofern das mod-eigene Schema sie nicht ausdrücklich ablehnt.
- Erhalte beim Aktualisieren eines Namensraums alle fremden Namensräume und deren unbekannte Felder.
- Bewahre paketweite Darstellungsmetadaten in `lordmeta.json` auf.
- Teste den Zugriff über Spieler-ID, genauen Pfad und `CustomLordConfig`, nicht verfügbare Host-Daten, fehlende Dateien und Namensräume, ungültiges JSON sowie nicht unterstützte mod-eigene Schemata.
- Prüfe, dass die Paketierung jedes Sidecar neben seiner AIC erhält und die AIC weiterhin ohne Sidecar oder konsumierende Mods lädt.
