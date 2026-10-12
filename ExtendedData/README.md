# ExtendedData

[English](#english) | [Deutsch](#deutsch)

## English

ExtendedData adds shared supplemental data for Custom Trails, Custom Lords, and Script Extender maps to Stronghold Crusader DE. Vanilla file formats remain usable, while mod authors receive clearly separated integration points.

### Custom Trails and Coop Trails

Trail authors can store host-managed settings of compatible mods per mission. ExtendedData also supports portable Coop Trail packages containing their missions and required maps, Lords, and AIV files.

A custom Coop Trail ends after its last included mission. Later mission slots are unavailable while that package is selected; select Vanilla Coop Trails to play the original missions again.

Coop Trail export also resolves Extended Lords whose saved display name is empty. Lord and AIV files are taken from the same resolved author package, with AIV names and checksums checked together. If export fails, the dialog directs you to `BepInEx/LogOutput.log` for the affected mission and source details.

With BugfixesAndQoL installed and **Show extra map, save and Trail controls** enabled, the Import Trail dialog can delete local Coop Trail packages after confirmation. ExtendedData supplies the source folder and Workshop subscription status so subscribed Trails remain protected. Deletion creates no backup.

- [Mod settings in Maps and Custom Trails](../Guides/ExtendedData/Custom%20Trail%20Mod%20Settings.md#english)
- [ExtendedData compatibility for mod authors](../Guides/ExtendedData/Mod%20Compatibilty%20ExtendedData.md#english)

### Custom Lords

ExtendedData supports Script Extender content in Custom Lord packages. Mods can also read additional static data for one AIC from GUID-separated namespaces in a matching `name.modlord.json` file.

- [Custom Lord packages with Script Extender](../Guides/ExtendedData/CustomLordExtendedPackages.md#english)
- [Mod-specific data for Custom Lord AICs](../Guides/ExtendedData/ModLordData.md#english)

### Map data

Map authors can add a `modmap.json` file to the appended archive of a Script Extender map. Each mod reads only its own GUID namespace. The base map remains usable without ExtendedData; only the supplemental namespaced data is unavailable.

- [Mod-specific data for maps](../Guides/ExtendedData/ModMapData.md#english)

The complete index is available under [ExtendedData guides](../Guides/ExtendedData/README.md#english).

### Mod settings in saved games

Saved games retain the host settings of compatible mods. Loading normally restores those saved values. For customizable games, **Use current mod settings** in the Load Game dialog lets you use your currently active host settings instead. Personal settings remain your own. ExtendedData's own options are excluded from these gameplay-setting snapshots.

### Compatibility and diagnostics

This mod requires the Script Extender and **APIShared 0.5.0 or newer**. Subscribe to APIShared separately when using the Steam modpack.

Shared services handle presets and game-mode classification. Routine activity is logged at Debug level. Repeated diagnostics are limited to reduce log spam; warnings and errors remain available. No gameplay setting is needed for this change. Validated Fixes preference property metadata is cached; preference values and snapshot checks are unchanged.

---

## Deutsch

ExtendedData ergänzt Stronghold Crusader DE um gemeinsam nutzbare Zusatzdaten für Custom Trails, Custom Lords und Script-Extender-Maps. Die Vanilla-Dateiformate bleiben verwendbar, während Modentwickler klar abgegrenzte Schnittstellen erhalten.

### Custom Trails und Koop-Trails

Trail-Ersteller können hostverwaltete Einstellungen kompatibler Mods pro Mission speichern. ExtendedData unterstützt außerdem portable Koop-Trail-Pakete mit ihren Missionen und den benötigten Maps, Lords und AIV-Dateien.

Ein benutzerdefinierter Koop-Trail endet nach seiner letzten enthaltenen Mission. Spätere Missionsplätze sind nicht verfügbar, solange dieses Paket ausgewählt ist. Wähle wieder die Vanilla-Koop-Trails, um die ursprünglichen Missionen zu spielen.

Der Koop-Trail-Export löst Extended Lords auch bei leerem gespeichertem Anzeigenamen auf. Lord- und AIV-Dateien stammen aus demselben ermittelten Autorenpaket; bei AIVs werden Name und Prüfsumme gemeinsam geprüft. Schlägt der Export fehl, verweist der Dialog auf `BepInEx/LogOutput.log` mit Angaben zur betroffenen Mission und Quelle.

Mit installiertem BugfixesAndQoL und aktivierter Option **Zusätzliche Karten-, Spielstand- und Trailbuttons anzeigen** lassen sich lokale Koop-Trail-Pakete nach Bestätigung im Trail-Importdialog löschen. ExtendedData liefert den Quellordner und den Workshop-Abonnementstatus, damit abonnierte Trails geschützt bleiben. Beim Löschen wird kein Backup erstellt.

- [Mod-Einstellungen in Maps und Custom Trails](../Guides/ExtendedData/Custom%20Trail%20Mod%20Settings.md#deutsch)
- [ExtendedData-Kompatibilität für Modentwickler](../Guides/ExtendedData/Mod%20Compatibilty%20ExtendedData.md#deutsch)

### Custom Lords

ExtendedData unterstützt Script-Extender-Inhalte in Custom-Lord-Paketen. Mods können außerdem zusätzliche statische Daten für eine einzelne AIC aus GUID-getrennten Bereichen einer passenden `name.modlord.json` lesen.

- [Custom-Lord-Pakete mit Script Extender](../Guides/ExtendedData/CustomLordExtendedPackages.md#deutsch)
- [Mod-spezifische Daten für Custom-Lord-AICs](../Guides/ExtendedData/ModLordData.md#deutsch)

### Map-Daten

Map-Ersteller können eine `modmap.json` in das angehängte Archiv einer Script-Extender-Map aufnehmen. Jeder Mod liest ausschließlich seinen eigenen GUID-Bereich. Die zugrunde liegende Map bleibt ohne ExtendedData verwendbar; lediglich die zusätzlichen Namensraumdaten stehen dann nicht zur Verfügung.

- [Mod-spezifische Daten für Maps](../Guides/ExtendedData/ModMapData.md#deutsch)

Das vollständige Inhaltsverzeichnis befindet sich unter [ExtendedData Guides](../Guides/ExtendedData/README.md#deutsch).

### Mod-Einstellungen in Spielständen

Spielstände behalten die Host-Einstellungen kompatibler Mods. Beim Laden werden normalerweise diese gespeicherten Werte wiederhergestellt. Bei anpassbaren Spielen kannst du im Dialog zum Laden mit **Aktuelle Modsettings verwenden** stattdessen deine derzeit aktiven Host-Einstellungen übernehmen. Persönliche Einstellungen bleiben deine eigenen. ExtendedDatas eigene Optionen sind von diesen Spielsettings-Snapshots ausgenommen.

### Kompatibilität und Diagnosen

Diese Mod benötigt den Script Extender und **APIShared 0.5.0 oder neuer**. APIShared muss beim Steam-Modpack separat abonniert werden.

Gemeinsame Dienste übernehmen Presets und die Erkennung des Spielmodus. Routineausgaben erscheinen auf Debug-Ebene. Wiederholte Diagnosen werden begrenzt, um das Log zu entlasten; Warnungen und Fehler bleiben verfügbar. Dafür ist keine Spieleinstellung nötig. Geprüfte Metadaten der Fixes-Einstellungen werden zwischengespeichert; Einstellungswerte und Snapshot-Prüfungen bleiben unverändert.
