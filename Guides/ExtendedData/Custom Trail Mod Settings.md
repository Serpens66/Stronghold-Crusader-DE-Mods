# Mod settings in Maps and Custom Trails / Mod-Einstellungen in Maps und Custom Trails

[English](#english) | [Deutsch](#deutsch)

## English

`ExtendedData` can store shared gameplay settings in a Map or beside a Trail mission. Compatible installed mods are discovered automatically, so authors normally configure everything in the Map Editor or Trail Maker instead of editing JSON.

This guide is for Map and Trail authors. Mod developers should use [ExtendedData compatibility for mod authors](Mod%20Compatibilty%20ExtendedData.md#english).

APIShared distinguishes local Preset 1/2, loose external JSON presets, and temporary mission presets. Maps and Trails use the temporary mission form: it can be editable in Trail Maker, is read-only during play, and restores the previously active local or external preset afterwards. Normal external preset creation and distribution are documented in [Extensible ModSettings Presets](ModSettings%20Presets.md#english).

### Choose the settings to store

1. Install and enable `ExtendedData` and every compatible gameplay mod the Map or Trail should use.
2. Open the Map Editor or Trail Maker and load or create the Map or mission.
3. Open the `ExtendedData` mod settings and expand a compatible mod.
4. Choose one mode for every relevant setting:
   - **Mod default** uses the safe baseline supplied by that mod. For most gameplay mods this disables the mod or feature.
   - **Player/host** uses the normal saved setting of the singleplayer user or multiplayer host. The mission permits the setting but does not prescribe its value.
   - **Fixed creator value** stores the value currently shown in the owning mod and applies that exact value during the mission.
5. Configure fixed values in the owning mod's settings panel, then save the Map or Trail mission.

The selector on a mod heading changes all its settings at once. `Mixed` means that the settings use different modes. Large related lists, including Unit Costs and Extra Features market multipliers, are presented as one atomic selection.

Only persistent `[SyncHostOnly]` settings can become Map or Trail rules. Personal, per-player, local, and transient settings remain controlled by each player.

### Trail Maker authoring and tests

Opening a saved Trail Maker mission loads its matching sidecar into an editable **Trail** preset. A new unsaved mission starts from the safe mod defaults.

ExtendedData keeps the editable authoring draft while the mission is tested, restarted, opened in the Map Editor, or returned to the Trail Maker. Saving refreshes the draft and writes the sidecar. Leaving the authoring context discards the draft and restores the previous normal preset. If a draft cannot be loaded or restored safely, ExtendedData falls back to editable mod defaults rather than retaining a partial preset.

### Resulting files

Saving a `.map` from the Map Editor writes the schema-3 document into the appended Map archive as:

```text
_SE_ModData_ExtendedData-MapModSettings.msgpack
```

Despite its suffix, this entry contains UTF-8 JSON. It is separate from `modmap.json`, whose GUID-based namespaces remain unchanged. A successful empty capture records safe defaults for all compatible mods. If capture fails, an existing archive entry is retained.

Saving `Trail_Mission_1.trail` creates the optional sidecar:

```text
Trail_Mission_1.modtrail.json
```

Keep the sidecar beside its matching `.trail` file with the same base name. If no setting departs from `Mod default`, the document contains no active mod entries. A missing sidecar likewise uses every compatible mod's safe baseline.

A portable Coop Trail package uses matching mission base names:

```text
CoopMissions/01.coopmission.json
CoopMissions/01.modtrail.json
```

When uploading a normal or Coop Trail, keep **Include mod settings** enabled to ship the sidecars. Disable it only for a deliberately Vanilla/default-only package.

### What players need

Players need `ExtendedData` and every mod explicitly mentioned by the Map or mission. A missing mentioned mod is reported when the preset is activated. Unmentioned mods and settings use their mod-defined safe baseline instead of arbitrary local gameplay values.

After the mission ends, compatible mods restore the player's previous normal preset. Trail-owned host settings remain read-only during play; personal client settings remain editable.

For a free Singleplayer Skirmish, Multiplayer host lobby, or Trail opened through **Customize**, select the Map and press **Use Map modsettings**. Selecting a Map alone does not activate its preset. The button is disabled when the archive has no valid settings, hidden in Trail Maker, and hidden for Multiplayer clients. Changing the selected Map or leaving the lobby restores the previous local preset; otherwise the Map context remains active until the mission ends.

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

Names in `playerSettings` use **Player/host**. Values in `overrides` use **Fixed creator value**. A property cannot occur in both collections; unlisted properties use **Mod default**. Names and value types must exactly match the installed compatible mod.

See `ExtendedData/Examples/01.modtrail.json.example` for a larger example.

### Troubleshooting

- If a mod is absent, confirm that it supports the mission-preset contract and search `BepInEx/LogOutput.log` for `Map/Trail mod settings`.
- If a mod is reported missing, install the plugin whose GUID is named by the mission.
- If a fixed value is wrong, reopen the Map or mission in its editor, select **Fixed creator value**, set the value in the owning mod, and save again.
- When moving files manually, keep `.trail` and `.modtrail.json` base names identical.

---

## Deutsch

`ExtendedData` kann gemeinsame Gameplay-Einstellungen in einer Map oder neben einer Trail-Mission speichern. Installierte kompatible Mods werden automatisch erkannt, sodass Ersteller normalerweise alles im Map Editor oder Trail Maker konfigurieren und kein JSON von Hand bearbeiten müssen.

Dieser Guide richtet sich an Map- und Trail-Ersteller. Modentwickler verwenden [ExtendedData-Kompatibilität für Modentwickler](Mod%20Compatibilty%20ExtendedData.md#deutsch).

APIShared unterscheidet lokale Presets 1/2, lose externe JSON-Presets und temporäre Missionspresets. Maps und Trails verwenden die temporäre Missionsform: Im Trail Maker kann sie bearbeitbar sein, während des Spiels ist sie schreibgeschützt und anschließend wird das zuvor aktive lokale oder externe Preset wiederhergestellt. Erstellung und Verteilung normaler externer Presets beschreibt [Erweiterbare ModSettings-Presets](ModSettings%20Presets.md#deutsch).

### Zu speichernde Einstellungen auswählen

1. Installiere und aktiviere `ExtendedData` sowie alle kompatiblen Gameplay-Mods, die die Map oder der Trail verwenden soll.
2. Öffne den Map Editor oder Trail Maker und lade oder erstelle die Map beziehungsweise Mission.
3. Öffne die Mod-Einstellungen von `ExtendedData` und klappe einen kompatiblen Mod auf.
4. Wähle für jede relevante Einstellung einen Modus:
   - **Mod default** verwendet den sicheren Ausgangswert des jeweiligen Mods. Bei den meisten Gameplay-Mods deaktiviert dies den Mod oder das Feature.
   - **Player/host** verwendet die normale gespeicherte Einstellung des Einzelspielers beziehungsweise Multiplayer-Hosts. Die Mission erlaubt den Wert, schreibt ihn aber nicht vor.
   - **Fixed creator value** speichert den aktuell im zugehörigen Mod angezeigten Wert und wendet genau diesen während der Mission an.
5. Konfiguriere feste Werte in den Einstellungen des jeweiligen Mods und speichere anschließend die Map oder Trail-Mission.

Der Auswahlknopf an einer Mod-Überschrift ändert alle zugehörigen Einstellungen gleichzeitig. `Mixed` bedeutet, dass verschiedene Modi verwendet werden. Große zusammengehörige Listen, darunter Unit Costs und die Markt-Multiplikatoren von Extra Features, erscheinen als eine atomare Auswahl.

Nur dauerhafte `[SyncHostOnly]`-Einstellungen können zu Map- oder Trail-Regeln werden. Persönliche, spielerspezifische, lokale und vorübergehende Einstellungen bleiben unter der Kontrolle des jeweiligen Spielers.

### Trail-Maker-Bearbeitung und Tests

Beim Öffnen einer gespeicherten Trail-Maker-Mission wird das passende Sidecar als bearbeitbares **Trail**-Preset geladen. Eine neue ungespeicherte Mission beginnt mit den sicheren Mod-Standardwerten.

ExtendedData behält den bearbeitbaren Entwurf während eines Tests, Neustarts, Wechsels in den Map Editor oder der Rückkehr zum Trail Maker bei. Beim Speichern wird der Entwurf aktualisiert und das Sidecar geschrieben. Beim Verlassen des Bearbeitungskontexts wird der Entwurf verworfen und das vorherige normale Preset wiederhergestellt. Kann ein Entwurf nicht sicher geladen oder wiederhergestellt werden, verwendet ExtendedData bearbeitbare Mod-Standardwerte statt eines unvollständigen Presets.

### Erzeugte Dateien

Beim Speichern einer `.map` aus dem Map Editor wird das Schema-3-Dokument unter folgendem Namen in das angehängte Map-Archiv geschrieben:

```text
_SE_ModData_ExtendedData-MapModSettings.msgpack
```

Trotz der Dateiendung enthält dieser Eintrag UTF-8-JSON. Er ist von `modmap.json` getrennt, deren GUID-basierte Namensräume unverändert bleiben. Eine erfolgreiche leere Erfassung speichert die sicheren Standardwerte aller kompatiblen Mods. Schlägt die Erfassung fehl, bleibt ein vorhandener Archiveintrag erhalten.

Beim Speichern von `Trail_Mission_1.trail` entsteht das optionale Sidecar:

```text
Trail_Mission_1.modtrail.json
```

Das Sidecar muss neben der zugehörigen `.trail`-Datei liegen und denselben Basisnamen verwenden. Weicht keine Einstellung von `Mod default` ab, enthält das Dokument keine aktiven Mod-Einträge. Ein fehlendes Sidecar verwendet ebenfalls die sichere Ausgangslage jedes kompatiblen Mods.

Ein portables Koop-Trail-Paket verwendet übereinstimmende Missions-Basisnamen:

```text
CoopMissions/01.coopmission.json
CoopMissions/01.modtrail.json
```

Beim Workshop-Upload eines normalen oder Koop-Trails muss **Include mod settings** aktiviert bleiben, damit die Sidecars enthalten sind. Deaktiviere die Option nur für ein bewusst reines Vanilla-/Standardpaket.

### Voraussetzungen für Spieler

Spieler benötigen `ExtendedData` und jeden von der Map oder Mission ausdrücklich genannten Mod. Ein fehlender genannter Mod wird bei der Aktivierung des Presets gemeldet. Nicht genannte Mods und Einstellungen verwenden ihre moddefinierte sichere Ausgangslage statt beliebiger lokaler Gameplay-Werte.

Nach Missionsende stellen kompatible Mods das vorherige normale Preset des Spielers wieder her. Vom Trail vorgegebene Host-Einstellungen bleiben während des Spiels schreibgeschützt; persönliche Client-Einstellungen bleiben bearbeitbar.

Wähle für ein freies Einzelspieler-Scharmützel, eine Multiplayer-Host-Lobby oder einen über **Customize** geöffneten Trail zuerst die Map und drücke anschließend **Use Map modsettings**. Allein die Map-Auswahl aktiviert kein Preset. Der Knopf ist deaktiviert, wenn das Archiv keine gültigen Einstellungen besitzt, und wird im Trail Maker sowie für Multiplayer-Clients ausgeblendet. Die Auswahl einer anderen Map oder das Verlassen der Lobby stellt das vorherige lokale Preset wieder her; andernfalls bleibt der Map-Kontext bis zum Missionsende aktiv.

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

Namen in `playerSettings` verwenden **Player/host**. Werte in `overrides` verwenden **Fixed creator value**. Eine Eigenschaft darf nicht in beiden Sammlungen vorkommen; nicht aufgeführte Eigenschaften verwenden **Mod default**. Namen und Werttypen müssen exakt zum installierten kompatiblen Mod passen.

Ein größeres Beispiel befindet sich unter `ExtendedData/Examples/01.modtrail.json.example`.

### Fehlerbehebung

- Fehlt ein Mod, prüfe dessen Unterstützung des Missions-Preset-Vertrags und suche in `BepInEx/LogOutput.log` nach `Map/Trail mod settings`.
- Wird ein Mod als fehlend gemeldet, installiere das Plugin mit der von der Mission genannten GUID.
- Ist ein fester Wert falsch, öffne die Map oder Mission erneut im Editor, wähle **Fixed creator value**, setze den Wert im zugehörigen Mod und speichere erneut.
- Beim manuellen Verschieben müssen `.trail` und `.modtrail.json` denselben Basisnamen behalten.
