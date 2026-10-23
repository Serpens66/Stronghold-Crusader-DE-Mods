# ExtendedData

[English](#english) | [Deutsch](#deutsch)

## English

ExtendedData adds shared supplemental data for Custom Trails, Custom Lords, and Script Extender maps to Stronghold Crusader DE. Vanilla file formats remain usable, while mod authors receive clearly separated integration points.

### Custom Trails and Coop Trails

Trail authors can store host-managed settings of compatible mods per mission. ExtendedData also supports portable Coop Trail packages containing their missions and required maps, Lords, and AIV files.

Active map, Trail and savegame mod settings are kept when restarting a mission, including the Trail author's fixed rules. Packaged Coop missions use their saved AI behaviour and castles even without BugfixesAndQoL; conflicting AI selections or missing synchronization are explained before the mission starts.

A custom Coop Trail ends after its last included mission. Later mission slots are unavailable while that package is selected; select Vanilla Coop Trails to play the original missions again.

Saving or exporting a Custom or Coop Trail again preserves its required Lord content and companion files. If extra Lord data cannot be verified, you can confirm an export using only the saved AI behaviour, castles and portrait; unverified extras such as Fixes preferences and required scripts are then omitted.

Missing or conflicting mission files are reported with details in `BepInEx/LogOutput.log`. All players should use the same current version of ExtendedData for new Coop packages.

With BugfixesAndQoL installed and **Extended map, save and trail dialogs** enabled, the Import Trail dialog can delete local Coop Trail packages after confirmation. Subscribed Workshop Trails remain protected; deletion creates no backup.

- [Mod settings in Maps and Custom Trails](../Guides/ExtendedData/Custom%20Trail%20Mod%20Settings.md#english)
- [ExtendedData compatibility for mod authors](../Guides/ExtendedData/Mod%20Compatibilty%20ExtendedData.md#english)

### Custom Lords

ExtendedData supports Script Extender content in Custom Lord packages. Mods can also read additional static data for one AIC from GUID-separated namespaces in a matching `name.modlord.json` file.

Selected Lords bring their associated Fixes preferences into the match, including inherited preferences. Conflicting requirements are reported before starting, and changing the selection restores the previous preferences.

- [Custom Lord packages with Script Extender](../Guides/ExtendedData/CustomLordExtendedPackages.md#english)
- [Mod-specific data for Custom Lord AICs](../Guides/ExtendedData/ModLordData.md#english)

### Map data

Map authors can add a `modmap.json` file to the appended archive of a Script Extender map. Each mod reads only its own GUID namespace. The base map remains usable without ExtendedData. Mods can read supplemental namespaces through APIShared alone.

- [Mod-specific data for maps](../Guides/ExtendedData/ModMapData.md#english)

The complete index is available under [ExtendedData guides](../Guides/ExtendedData/README.md#english).

### Mod settings in saved games

APIShared provides **Use current mod settings** in the Load Game dialog, even when ExtendedData is absent or disabled. For customizable saves, the checkbox chooses between saved and currently active host settings supported by APIShared. Its last explicit choice is remembered across saves, games and full game restarts. Initially it is off, so saved settings are used.

Where permitted, current settings can also be used if the saved mod settings cannot be read.

Without a usable checkbox, automatic loads use current supported settings. Existing mission restrictions and fixed Trail creator rules remain in force. Forced states for old or restricted saves do not overwrite your remembered choice. Personal settings remain your own; ExtendedData's own options are excluded from these gameplay-setting snapshots.

### Choose which features to use

The host can separately enable map/Trail mod settings, custom Coop packages and extended Lord content. Personal editor/import/export tools and extended Workshop uploads have their own switches; saving author data does not automatically enable it in a match.

### Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.12.5 or newer**, including its `APIShared.Content.dll`. Subscribe to APIShared separately when using the Steam modpack.

Map/Trail compatibility messages use APIShared's shared participant checks. A broken settings provider is reported without hiding other compatible mods. APIShared prepares and applies temporary settings snapshots and handles their cleanup. ExtendedData supplies content sources and coordinates the mission lifecycle.

Shared services handle presets and game-mode classification. Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change. Validated Fixes preference property metadata is cached; preference values and snapshot checks are unchanged.

---

## Deutsch

ExtendedData ergänzt Stronghold Crusader DE um gemeinsam nutzbare Zusatzdaten für Custom Trails, Custom Lords und Script-Extender-Maps. Die Vanilla-Dateiformate bleiben verwendbar, während Modentwickler klar abgegrenzte Schnittstellen erhalten.

### Custom Trails und Koop-Trails

Trail-Ersteller können hostverwaltete Einstellungen kompatibler Mods pro Mission speichern. ExtendedData unterstützt außerdem portable Koop-Trail-Pakete mit ihren Missionen und den benötigten Maps, Lords und AIV-Dateien.

Beim Neustart einer Mission bleiben die aktiven Mod-Einstellungen aus Karte, Trail oder Spielstand sowie feste Vorgaben des Trail-Erstellers erhalten. Koop-Pakete verwenden ihr gespeichertes KI-Verhalten und ihre Burgen auch ohne BugfixesAndQoL; widersprüchliche KI-Auswahlen oder fehlende Synchronisierung werden vor dem Start erklärt.

Ein benutzerdefinierter Koop-Trail endet nach seiner letzten enthaltenen Mission. Spätere Missionsplätze sind nicht verfügbar, solange dieses Paket ausgewählt ist. Wähle wieder die Vanilla-Koop-Trails, um die ursprünglichen Missionen zu spielen.

Beim erneuten Speichern oder Exportieren eines Custom- oder Koop-Trails bleiben benötigte Lord-Inhalte und Begleitdateien erhalten. Lassen sich Lord-Zusatzdaten nicht prüfen, kannst du einen Export nur mit gespeichertem KI-Verhalten, Burgen und Porträt bestätigen; ungeprüfte Extras wie Fixes-Präferenzen und benötigte Skripte fehlen dann.

Fehlende oder widersprüchliche Missionsdateien werden mit Details in `BepInEx/LogOutput.log` gemeldet. Alle Spieler sollten für neue Koop-Pakete dieselbe aktuelle Version von ExtendedData verwenden.

Mit installiertem BugfixesAndQoL und aktivierter Option **Erweiterte Karten-, Spielstand- und Trail-Dialoge** lassen sich lokale Koop-Trail-Pakete nach Bestätigung im Trail-Importdialog löschen. Abonnierte Workshop-Trails bleiben geschützt; beim Löschen wird kein Backup erstellt.

- [Mod-Einstellungen in Maps und Custom Trails](../Guides/ExtendedData/Custom%20Trail%20Mod%20Settings.md#deutsch)
- [ExtendedData-Kompatibilität für Modentwickler](../Guides/ExtendedData/Mod%20Compatibilty%20ExtendedData.md#deutsch)

### Custom Lords

ExtendedData unterstützt Script-Extender-Inhalte in Custom-Lord-Paketen. Mods können außerdem zusätzliche statische Daten für eine einzelne AIC aus GUID-getrennten Bereichen einer passenden `name.modlord.json` lesen.

Ausgewählte Lords bringen ihre zugehörigen Fixes-Präferenzen mit in die Partie, einschließlich geerbter Einstellungen. Widersprüchliche Vorgaben werden vor dem Start gemeldet; ein Wechsel der Auswahl stellt die vorherigen Präferenzen wieder her.

- [Custom-Lord-Pakete mit Script Extender](../Guides/ExtendedData/CustomLordExtendedPackages.md#deutsch)
- [Mod-spezifische Daten für Custom-Lord-AICs](../Guides/ExtendedData/ModLordData.md#deutsch)

### Map-Daten

Map-Ersteller können eine `modmap.json` in das angehängte Archiv einer Script-Extender-Map aufnehmen. Jeder Mod liest ausschließlich seinen eigenen GUID-Bereich. Die zugrunde liegende Map bleibt ohne ExtendedData verwendbar. Mods können zusätzliche Namensraumdaten auch über APIShared allein lesen.

- [Mod-spezifische Daten für Maps](../Guides/ExtendedData/ModMapData.md#deutsch)

Das vollständige Inhaltsverzeichnis befindet sich unter [ExtendedData Guides](../Guides/ExtendedData/README.md#deutsch).

### Mod-Einstellungen in Spielständen

APIShared stellt die Checkbox **Aktuelle Modsettings verwenden** im Ladedialog bereit, auch wenn ExtendedData fehlt oder deaktiviert ist. Bei anpassbaren Spielständen wählst du damit zwischen gespeicherten und derzeit aktiven, von APIShared unterstützten Host-Einstellungen. Die letzte bewusste Auswahl bleibt über Spielstände, Partien und vollständige Spielneustarts hinweg erhalten. Anfangs ist die Checkbox aus; dann gelten die gespeicherten Einstellungen.

Soweit erlaubt, lassen sich aktuelle Einstellungen auch verwenden, wenn die gespeicherten Mod-Einstellungen nicht lesbar sind.

Fehlt eine nutzbare Checkbox, werden beim automatischen Laden die aktuellen unterstützten Einstellungen verwendet. Bestehende Missionssperren und feste Trail-Autorenregeln bleiben erhalten. Erzwungene Zustände bei alten oder gesperrten Spielständen überschreiben deine gemerkte Auswahl nicht. Persönliche Einstellungen bleiben deine eigenen; ExtendedDatas eigene Optionen sind von diesen Spielsettings-Snapshots ausgenommen.

### Funktionen einzeln auswählen

Der Host kann Mod-Einstellungen aus Karten und Trails, eigene Koop-Pakete und erweiterte Lord-Inhalte getrennt aktivieren. Persönliche Editor-, Import- und Exportwerkzeuge sowie erweiterte Workshop-Uploads haben eigene Schalter; gespeicherte Autorenangaben werden dadurch nicht automatisch in einer Partie angewendet.

### Kompatibilität und Diagnosen

Diese Mod benötigt den Script Extender und **APIShared 0.12.5 oder neuer**, einschließlich `APIShared.Content.dll`. APIShared muss beim Steam-Modpack separat abonniert werden.

Kompatibilitätsmeldungen für Maps und Trails verwenden APIShareds gemeinsame Teilnehmerprüfung. Ein fehlerhafter Settings-Anbieter wird gemeldet, ohne andere kompatible Mods auszublenden. APIShared bereitet temporäre Settings-Snapshots vor, wendet sie an und bereinigt sie. ExtendedData liefert die Inhaltsquellen und koordiniert den Missionslifecycle.

Gemeinsame Dienste übernehmen Presets und die Erkennung des Spielmodus. Routineausgaben erscheinen auf Debug-Ebene. Wiederholte Diagnosen werden begrenzt, um das Log zu entlasten; Warnungen und Fehler bleiben verfügbar. Dafür ist keine Spieleinstellung nötig. Geprüfte Metadaten der Fixes-Einstellungen werden zwischengespeichert; Einstellungswerte und Snapshot-Prüfungen bleiben unverändert.
