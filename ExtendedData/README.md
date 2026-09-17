# ExtendedData

ExtendedData ergänzt Stronghold Crusader DE um gemeinsam nutzbare Zusatzdaten für Custom Trails, Custom Lords und Script-Extender-Maps. Der Mod hält die Vanilla-Dateiformate verwendbar und stellt Modentwicklern klar abgegrenzte Schnittstellen bereit.

## Custom Trails und Koop-Trails

Trail-Ersteller können hostverwaltete Einstellungen kompatibler Mods pro Mission speichern. ExtendedData unterstützt außerdem exportierbare Koop-Trail-Pakete mit Missionen und den benötigten Maps, Lords und AIV-Dateien.

- [Modsettings in Custom Trails](../Guides/ExtendedData/Custom%20Trail%20Mod%20Settings.md)
- [ExtendedData-Kompatibilität für Modentwickler](../Guides/ExtendedData/Mod%20Compatibilty%20ExtendedData.md)

## Custom Lords

ExtendedData unterstützt Script-Extender-Inhalte in Custom-Lord-Paketen. Zusätzliche statische Daten für eine einzelne AIC können Mods über GUID-getrennte Bereiche einer passenden `name.modlord.json` lesen.

- [Custom-Lord-Pakete mit Script Extender](../Guides/ExtendedData/CustomLordExtendedPackages.md)
- [Mod-spezifische Daten für Custom-Lord-AICs](../Guides/ExtendedData/ModLordData.md)

## Map-Daten

Map-Ersteller können eine `modmap.json` in das angehängte Archiv einer Script-Extender-Map aufnehmen. Mods lesen daraus ausschließlich ihren eigenen GUID-Bereich. Die eigentliche Vanilla-Map bleibt ohne Mods normal ladbar; lediglich die Zusatzdaten werden dann nicht ausgewertet.

- [Mod-spezifische Daten für Maps](../Guides/ExtendedData/ModMapData.md)

Das vollständige Inhaltsverzeichnis befindet sich unter [ExtendedData Guides](../Guides/ExtendedData/README.md).
