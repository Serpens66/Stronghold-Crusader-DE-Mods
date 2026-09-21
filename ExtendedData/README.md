# ExtendedData

[English](#english) | [Deutsch](#deutsch)

## English

ExtendedData adds shared supplemental data for Custom Trails, Custom Lords, and Script Extender maps to Stronghold Crusader DE. Vanilla file formats remain usable, while mod authors receive clearly separated integration points.

### Custom Trails and Coop Trails

Trail authors can store host-managed settings of compatible mods per mission. ExtendedData also supports portable Coop Trail packages containing their missions and required maps, Lords, and AIV files.

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

---

## Deutsch

ExtendedData ergänzt Stronghold Crusader DE um gemeinsam nutzbare Zusatzdaten für Custom Trails, Custom Lords und Script-Extender-Maps. Die Vanilla-Dateiformate bleiben verwendbar, während Modentwickler klar abgegrenzte Schnittstellen erhalten.

### Custom Trails und Koop-Trails

Trail-Ersteller können hostverwaltete Einstellungen kompatibler Mods pro Mission speichern. ExtendedData unterstützt außerdem portable Koop-Trail-Pakete mit ihren Missionen und den benötigten Maps, Lords und AIV-Dateien.

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
