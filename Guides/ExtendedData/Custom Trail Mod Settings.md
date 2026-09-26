# Mod settings in Maps and Custom Trails / Mod-Einstellungen in Maps und Custom Trails

[English](#english) | [Deutsch](#deutsch)

## English

`ExtendedData` can store shared gameplay settings in a Map or beside a Trail mission. Compatible installed mods are discovered automatically, so authors normally configure everything in the Map Editor or Trail Maker instead of editing JSON.

This guide is for Map and Trail authors. Mod developers should use [ExtendedData compatibility for mod authors](Mod%20Compatibilty%20ExtendedData.md#english).

APIShared distinguishes editable normal working settings, personal/bundled/external JSON presets, and temporary mission contexts. Maps and Trails use the temporary mission form: it is editable in Customize or Trail Maker, read-only when started directly, and restores the previous normal working settings and preset status afterwards. Normal preset creation and distribution are documented in [Extensible ModSettings Presets](ModSettings%20Presets.md#english).

### Choose the settings to store

1. Install and enable `ExtendedData` and every compatible gameplay mod the Map or Trail should use.
2. Open the Map Editor or Trail Maker and load or create the Map or mission.
3. Open the `ExtendedData` mod settings and expand a compatible mod.
4. Choose one mode for every relevant setting:
   - **Mod default** uses the safe baseline supplied by that mod. For most gameplay mods this disables the mod or feature.
   - **Player/host** uses the normal saved setting of the singleplayer user or multiplayer host. The mission permits the setting but does not prescribe its value.
   - **Fixed value** stores the value currently shown in the owning mod and applies that value during the mission.
5. Configure fixed values in the owning mod's settings panel, then save the Map or Trail mission.

The selector on a mod heading changes all its settings at once. `Mixed` means that the settings use different modes. Large related lists, including Unit Costs and Extra Features market multipliers, are presented as one atomic selection.

Only persistent `[SyncHostOnly]` settings can become Map or Trail rules. Personal, per-player, local, and transient settings remain controlled by each player.

### Trail Maker authoring and tests

Opening a saved Trail Maker mission loads its matching sidecar into an editable temporary **Trail** context; without a sidecar it uses the safe mod defaults. A new unsaved mission also starts with safe default values in an editable draft, but its available settings initially use **Fixed value** so the values shown in the owning mods can be saved as creator values. Select **Mod default** for any setting that should instead remain at its mod-defined baseline. Personal, bundled, and external normal presets can be loaded into the draft. The common source selector always offers **Mod defaults**, and additionally offers **Trail settings** or **Map settings** when those valid sources exist. This lets an author reload the saved Trail settings or use the selected Map as a template without modifying either source.

ExtendedData keeps the editable authoring draft while the mission is tested, restarted, opened in the Map Editor, or returned to the Trail Maker. **Include modsettings** beside the Trail Maker Save button controls whether saving writes the draft as a sidecar; it is enabled by default. Disabling it deliberately removes an existing sidecar after the mission is saved. The normal preset Save dialog can create a personal preset from the draft, but can never overwrite a Trail, Map archive, or Coop package. Leaving the authoring context discards the draft and restores the previous normal working settings and status. If a draft cannot be loaded or restored safely, ExtendedData falls back to editable mod defaults rather than retaining a partial preset.

### Resulting files

The Map Editor Save dialog shows **Include modsettings**. It starts unchecked when the opened Map has no ExtendedData entry, including a new Map, but can be checked to add settings on this save. It starts checked when the Map already contains the entry. Its tooltip points to Extended Data's **MOD SETTINGS IN MAPS AND CUSTOM TRAILS** section, where the stored Mod default, Player/host, and Fixed value modes are selected. Saving with the option checked writes the schema-3 document into the appended Map archive as:

```text
_SE_ModData_ExtendedData-MapModSettings.msgpack
```

Despite its suffix, this entry contains UTF-8 JSON. It is separate from `modmap.json`, whose GUID-based namespaces remain unchanged. Disabling the option deliberately removes only this ExtendedData entry; other archive and mod data remain untouched. Capture, removal, path binding, and the resulting file are verified fail-closed, so an unsafe publication is cancelled or rolled back.

Saving `Trail_Mission_1.trail` creates the optional sidecar:

```text
Trail_Mission_1.modtrail.json
```

Keep the sidecar beside its matching `.trail` file with the same base name. If no setting departs from `Mod default`, the document contains no active mod entries. A missing sidecar likewise uses every compatible mod's safe baseline.

A portable Coop Trail package uses matching mission base names. The exporter creates the optional settings sidecar when that mission has active mod entries:

```text
CoopMissions/01.coopmission.json
CoopMissions/01.modtrail.json
```

When uploading a normal or Coop Trail, leave **Include modsettings** checked to ship any sidecars. Uncheck it to upload without modsettings sidecars.

### What players need

Players need `ExtendedData` and every mod explicitly mentioned by the Map or mission. A missing mentioned mod is reported when the preset is activated. Unmentioned mods and settings use their mod-defined safe baseline instead of arbitrary local gameplay values.

After the mission ends, compatible mods restore the player's previous normal working settings and preset status. Selecting a Custom Trail first shows a read-only preview: mods absent from its sidecar retain their personal values during selection. Starting that mission directly applies its settings as a read-only mission context, with the safe baseline for unmentioned mods and settings.

For a free Singleplayer Skirmish or Multiplayer host lobby, selecting a Map with valid embedded settings initializes an editable Map working copy. A Trail opened through **Customize** initializes the editable Trail working copy instead; its Map settings remain available only as an explicit source in the common selector. Changing the selected Map or leaving the lobby restores the previous normal working state; launching from the editable copy applies its current values as a read-only mission snapshot.

Only the Multiplayer host can activate or clear Map settings. ExtendedData authenticates the host packet and binds it to the selected Map name and CRC. Late joiners receive the active state, while malformed data is rejected without partially applying it.

### JSON format

The editors are the authoritative way to create the file. Manual editing is intended for inspection and tooling; invalid documents are rejected as a whole.

Schema 3 uses the owning BepInEx plugin GUID as each key below `mods`:

```json
{
  "schemaVersion": 3,
  "mods": {
    "StartConditions_Serp": {
      "playerSettings": [
        "EnableMod"
      ],
      "overrides": {
        "SetStartGoldHuman": 500
      }
    }
  }
}
```

Names in `playerSettings` use **Player/host**. Values in `overrides` use **Fixed value**. A property cannot occur in both collections; unlisted properties use **Mod default**. Names and value types must exactly match the installed compatible mod.

See `ExtendedData/Examples/01.modtrail.json.example` for a larger example.

### Troubleshooting

- If a mod is absent, confirm that it supports the mission-preset contract and search `BepInEx/LogOutput.log` for `Map/Trail mod settings`.
- If a mod is reported missing, install the plugin whose GUID is named by the mission.
- If a fixed value is wrong, reopen the Map or mission in its editor, select **Fixed value**, set the value in the owning mod, and save again.
- When moving files manually, keep `.trail` and `.modtrail.json` base names identical.

---

## Deutsch

`ExtendedData` kann gemeinsame Gameplay-Einstellungen in einer Map oder neben einer Trail-Mission speichern. Installierte kompatible Mods werden automatisch erkannt, sodass Ersteller normalerweise alles im Map Editor oder Trail Maker konfigurieren und kein JSON von Hand bearbeiten müssen.

Dieser Guide richtet sich an Map- und Trail-Ersteller. Modentwickler verwenden [ExtendedData-Kompatibilität für Modentwickler](Mod%20Compatibilty%20ExtendedData.md#deutsch).

APIShared unterscheidet bearbeitbare normale Arbeitswerte, persönliche/mitgelieferte/externe JSON-Presets und temporäre Missionskontexte. Maps und Trails verwenden die temporäre Missionsform: In Customize oder im Trail Maker ist sie bearbeitbar, beim direkten Start schreibgeschützt, und anschließend werden die vorherigen normalen Arbeitswerte samt Presetstatus wiederhergestellt. Erstellung und Verteilung normaler Presets beschreibt [Erweiterbare ModSettings-Presets](ModSettings%20Presets.md#deutsch).

### Zu speichernde Einstellungen auswählen

1. Installiere und aktiviere `ExtendedData` sowie alle kompatiblen Gameplay-Mods, die die Map oder der Trail verwenden soll.
2. Öffne den Map Editor oder Trail Maker und lade oder erstelle die Map beziehungsweise Mission.
3. Öffne die Mod-Einstellungen von `ExtendedData` und klappe einen kompatiblen Mod auf.
4. Wähle für jede relevante Einstellung einen Modus:
   - **Mod-Standard** verwendet den sicheren Ausgangswert des jeweiligen Mods. Bei den meisten Gameplay-Mods deaktiviert dies den Mod oder das Feature.
   - **Spieler/Host** verwendet die normale gespeicherte Einstellung des Einzelspielers beziehungsweise Multiplayer-Hosts. Die Mission erlaubt den Wert, schreibt ihn aber nicht vor.
   - **Fester Wert** speichert den aktuell im zugehörigen Mod angezeigten Wert und wendet ihn während der Mission an.
5. Konfiguriere feste Werte in den Einstellungen des jeweiligen Mods und speichere anschließend die Map oder Trail-Mission.

Der Auswahlknopf an einer Mod-Überschrift ändert alle zugehörigen Einstellungen gleichzeitig. `Gemischt` bedeutet, dass verschiedene Modi verwendet werden. Große zusammengehörige Listen, darunter Unit Costs und die Markt-Multiplikatoren von Extra Features, erscheinen als eine atomare Auswahl.

Nur dauerhafte `[SyncHostOnly]`-Einstellungen können zu Map- oder Trail-Regeln werden. Persönliche, spielerspezifische, lokale und vorübergehende Einstellungen bleiben unter der Kontrolle des jeweiligen Spielers.

### Trail-Maker-Bearbeitung und Tests

Beim Öffnen einer gespeicherten Trail-Maker-Mission wird das passende Sidecar als bearbeitbarer temporärer **Trail**-Kontext geladen; ohne Sidecar gelten die sicheren Mod-Standardwerte. Auch eine neue ungespeicherte Mission beginnt mit sicheren Standardwerten in einem bearbeitbaren Entwurf. Ihre verfügbaren Einstellungen stehen zunächst auf **Fester Wert**, damit die in den zugehörigen Mods angezeigten Werte als Erstellerwerte gespeichert werden können. Wähle **Mod-Standard** für Einstellungen, die stattdessen auf der moddefinierten Ausgangslage bleiben sollen. Persönliche, mitgelieferte und externe normale Presets können in den Entwurf geladen werden. Der gemeinsame Quellenwähler bietet immer **Mod-Standards** und bei gültiger Quelle zusätzlich **Trail-Einstellungen** beziehungsweise **Map-Einstellungen**. Damit kann der Autor die gespeicherten Trail-Einstellungen erneut laden oder die ausgewählte Map als Vorlage verwenden, ohne eine der Quelldateien zu verändern.

ExtendedData behält den bearbeitbaren Entwurf während eines Tests, Neustarts, Wechsels in den Map Editor oder der Rückkehr zum Trail Maker bei. **Modsettings einschließen** neben dem Speichern-Button des Trail Makers legt fest, ob der Entwurf als Sidecar geschrieben wird; die Option ist standardmäßig aktiv. Beim Deaktivieren wird ein vorhandenes Sidecar nach dem Speichern der Mission bewusst entfernt. Über den normalen Preset-Speicherdialog kann aus dem Entwurf ein persönliches Preset entstehen; Trail-, Map- und Koop-Dateien können dort niemals überschrieben werden. Beim Verlassen des Bearbeitungskontexts wird der Entwurf verworfen und der vorherige normale Arbeitsstand samt Status wiederhergestellt. Kann ein Entwurf nicht sicher geladen oder wiederhergestellt werden, verwendet ExtendedData bearbeitbare Mod-Standardwerte statt eines unvollständigen Presets.

### Erzeugte Dateien

Der Speicherdialog des Map Editors zeigt **Modsettings einschließen**. Bei einer neuen Map oder einer Map ohne ExtendedData-Eintrag ist die Option zunächst nicht angehakt; sie kann für das erstmalige Speichern der Einstellungen aktiviert werden. Enthält die Map den Eintrag bereits, ist sie zunächst angehakt. Der Tooltip verweist auf den Abschnitt **MODSETTINGS IN MAPS UND CUSTOM TRAILS** in Extended Data, in dem die Modi Mod-Standard, Spieler/Host und Fester Wert gewählt werden. Ist die Option angehakt, wird das Schema-3-Dokument unter folgendem Namen in das angehängte Map-Archiv geschrieben:

```text
_SE_ModData_ExtendedData-MapModSettings.msgpack
```

Trotz der Dateiendung enthält dieser Eintrag UTF-8-JSON. Er ist von `modmap.json` getrennt, deren GUID-basierte Namensräume unverändert bleiben. Beim Deaktivieren wird bewusst nur dieser ExtendedData-Eintrag entfernt; andere Archiv- und Moddaten bleiben unangetastet. Erfassung, Entfernung, Pfadbindung und Ergebnisdatei werden fehlersicher geprüft, sodass eine unsichere Veröffentlichung abgebrochen oder zurückgerollt wird.

Beim Speichern von `Trail_Mission_1.trail` entsteht das optionale Sidecar:

```text
Trail_Mission_1.modtrail.json
```

Das Sidecar muss neben der zugehörigen `.trail`-Datei liegen und denselben Basisnamen verwenden. Weicht keine Einstellung von `Mod-Standard` ab, enthält das Dokument keine aktiven Mod-Einträge. Ein fehlendes Sidecar verwendet ebenfalls die sichere Ausgangslage jedes kompatiblen Mods.

Ein portables Koop-Trail-Paket verwendet übereinstimmende Missions-Basisnamen. Der Exporter erzeugt das optionale Settings-Sidecar, wenn die Mission aktive Mod-Einträge hat:

```text
CoopMissions/01.coopmission.json
CoopMissions/01.modtrail.json
```

Lasse beim Workshop-Upload eines normalen oder Koop-Trails **Modsettings aufnehmen** angehakt, um vorhandene Sidecars mitzuliefern. Entferne den Haken, um ohne Modsettings-Sidecars hochzuladen.

### Voraussetzungen für Spieler

Spieler benötigen `ExtendedData` und jeden von der Map oder Mission ausdrücklich genannten Mod. Ein fehlender genannter Mod wird bei der Aktivierung des Presets gemeldet. Nicht genannte Mods und Einstellungen verwenden ihre moddefinierte sichere Ausgangslage statt beliebiger lokaler Gameplay-Werte.

Nach Missionsende stellen kompatible Mods die vorherigen normalen Arbeitswerte und den Presetstatus des Spielers wieder her. Die Auswahl eines Custom Trails zeigt zunächst eine schreibgeschützte Vorschau: Mods, die nicht im Sidecar genannt werden, behalten während der Auswahl ihre persönlichen Werte. Beim direkten Start werden die Missionseinstellungen als schreibgeschützter Kontext angewendet; für nicht genannte Mods und Einstellungen gilt die sichere Ausgangslage.

In einem freien Einzelspieler-Scharmützel oder einer Multiplayer-Host-Lobby initialisiert die Auswahl einer Map mit gültigen eingebetteten Einstellungen eine bearbeitbare Map-Arbeitskopie. Ein über **Customize** geöffneter Trail initialisiert stattdessen seine bearbeitbare Trail-Arbeitskopie; die Map-Einstellungen stehen nur als ausdrücklich ladbare Quelle im gemeinsamen Wähler bereit. Die Auswahl einer anderen Map oder das Verlassen der Lobby stellt den vorherigen normalen Arbeitsstand wieder her. Beim Start aus der bearbeitbaren Kopie werden deren aktuelle Werte als schreibgeschützter Missions-Snapshot angewendet.

Nur der Multiplayer-Host kann Map-Einstellungen aktivieren oder löschen. ExtendedData authentifiziert das Host-Paket und bindet es an Namen und CRC der ausgewählten Map. Später beitretende Spieler erhalten den aktiven Zustand; fehlerhafte Daten werden ohne teilweise Anwendung abgelehnt.

### JSON-Format

Die Editoren sind der maßgebliche Weg zum Erstellen der Datei. Manuelle Bearbeitung ist für Kontrolle und Werkzeuge gedacht; ungültige Dokumente werden vollständig abgelehnt.

Schema 3 verwendet die BepInEx-Plugin-GUID des jeweiligen Mods als Schlüssel unter `mods`:

```json
{
  "schemaVersion": 3,
  "mods": {
    "StartConditions_Serp": {
      "playerSettings": [
        "EnableMod"
      ],
      "overrides": {
        "SetStartGoldHuman": 500
      }
    }
  }
}
```

Namen in `playerSettings` verwenden **Spieler/Host**. Werte in `overrides` verwenden **Fester Wert**. Eine Eigenschaft darf nicht in beiden Sammlungen vorkommen; nicht aufgeführte Eigenschaften verwenden **Mod-Standard**. Namen und Werttypen müssen exakt zum installierten kompatiblen Mod passen.

Ein größeres Beispiel befindet sich unter `ExtendedData/Examples/01.modtrail.json.example`.

### Fehlerbehebung

- Fehlt ein Mod, prüfe dessen Unterstützung des Missions-Preset-Vertrags und suche in `BepInEx/LogOutput.log` nach `Map/Trail mod settings`.
- Wird ein Mod als fehlend gemeldet, installiere das Plugin mit der von der Mission genannten GUID.
- Ist ein fester Wert falsch, öffne die Map oder Mission erneut im Editor, wähle **Fester Wert**, setze den Wert im zugehörigen Mod und speichere erneut.
- Beim manuellen Verschieben müssen `.trail` und `.modtrail.json` denselben Basisnamen behalten.
