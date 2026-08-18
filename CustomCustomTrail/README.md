# Custom Custom Trail

`CustomCustomTrail` ist der zentrale Koordinator für benutzerdefinierte Trails. Er speichert die Einstellungen unterstützter Mods neben normalen Custom-Trail-Missionen als gleichnamige `.modjson`, lädt sie als Missionspreset `Trail` und stellt die gemeinsamen `Anpassen`-Übergänge für Custom- und Coop-Trails bereit. Außerdem ersetzt er einzelne Missionen der eingebauten Coop-Trails durch lokale `*.coopmission.json`-Bundles. Map, Lord-Konfigurationen (`.lordjson`) und Burgen (`.aivjson`) dürfen aus der Spielinstallation stammen oder direkt mit einer Mission geliefert werden.

Der Mod ist für SHCDE V2.8 gebaut und benötigt BepInEx sowie den SHCDE Script Extender. In echtem Coop laden Host und Gast ihre jeweilige lokale Mission desselben Trail-/Missionsplatzes. Der Mod vergleicht diese Dateien nicht; kompatible Inhalte liegen daher in der Verantwortung der Spieler.

Über die Modsettings kann der Koordinator lokal vollständig deaktiviert werden. Der Schalter wird unabhängig von Presets gespeichert und nicht über das Netzwerk synchronisiert. Im deaktivierten Zustand verhalten sich die installierten Hooks wie Vanilla: Sidecars werden weder geladen noch geschrieben oder mit Dateiverwaltungsaktionen gespiegelt, Coop-Missionen werden nicht ersetzt und die zusätzlichen `Anpassen`-Übergänge bleiben verborgen beziehungsweise inaktiv. Bereits ersetzte Coop-Katalogplätze werden beim Abschalten sofort auf ihre zuvor erfassten Vanilla-Einträge zurückgesetzt.

Die unterstützten Settings-Mods besitzen keine Abhängigkeit auf `CustomCustomTrail`. Ohne diesen Koordinator funktionieren ihre lokalen Voreinstellungen 1/2 normal weiter; `Trail` bleibt unsichtbar, vorhandene `.modjson` werden ignoriert und neue Custom-Trail-Speicherungen erzeugen keine Sidecars.

## Normale Custom Trails

Zu `Trail_Mission_1.trail` gehört ausschließlich über denselben Basisnamen `Trail_Mission_1.modjson`. Beim Speichern erfasst der Koordinator die aktuell wirksamen Einstellungen vor dem Vanilla-Speichervorgang, schreibt das Sidecar atomar und aktiviert den gespeicherten Stand anschließend wieder editierbar als `Trail`. Import, Export, Backup, Renummerierung und Löschen spiegeln die Sidecars.

Beim normalen Missionsstart ist `Trail` schreibgeschützt und standardmäßig aktiv. Über `Anpassen` wird die vollständige Skirmish-Lobby mit der richtigen Trail-Map geöffnet. Lokale Voreinstellungen 1/2 bleiben auswählbar und speichern Änderungen weiterhin normal; ein Wechsel zurück auf `Trail` stellt den unveränderten Missionsstand wieder her.

## Installation und Platzzuordnung

Die installierte Struktur lautet:

    BepInEx\plugins\CustomCustomTrail_Serp\
      CustomCustomTrail.dll
      CustomCustomTrail.Core.dll
      CoopTrails\
        Trail1\
          01.coopmission.json
          ...
          10.coopmission.json
        Trail2\
        Trail3\
        Trail4\

`01.coopmission.json` ersetzt Platz 1, `10.coopmission.json` Platz 10. Trailordner plus zweistelliger Dateiname sind die einzige Zuordnung; es gibt keine Hashbindung. Die JSON-Datei darf lokal in einem Texteditor geändert werden und wird beim nächsten Initialisieren des Coop-Missionskatalogs neu eingelesen. Fehlende oder ungültige Dateien ändern den jeweiligen Vanilla-Platz nicht. Der Wurzelordner einer Mission ist immer der Ordner, in dem ihre `*.coopmission.json` liegt.

Wichtige V2.8-Einschränkung: Die installierte Assembly besitzt nur die echten Missionsarrays `CoopTrail1`, `CoopTrail2` und `CoopTrail3`. Die irreführend benannte Klasse `FRONT_CoopTrail4` ist die Custom-Game-Lobby und kein vierter Vanilla-Missionstrail. `Trail4` bleibt deshalb für eine mögliche spätere Spielversion reserviert; Dateien darin werden in V2.8 mit einer verständlichen Logmeldung ignoriert.

Zum Bauen und Installieren `build.bat` als Administrator ausführen. `/nopause` unterdrückt die Abschlussabfrage.

## Vollständiges JSON-Beispiel

Siehe `Examples\01.coopmission.json.example`. Die Dateiendung `.example` verhindert ein unbeabsichtigtes Laden. Nach dem Kopieren nach `CoopTrails\TrailN\01.coopmission.json` müssen alle referenzierten Dateien bzw. installierten Namen existieren.

## Oberste Felder

| Feld | Werte und Wirkung |
| --- | --- |
| `schemaVersion` | Muss in dieser Version `2` sein. Schema 1 und andere Versionen werden verständlich abgelehnt. |
| `displayName` | Eigener Missionsname in der Coop-Lobby. Darf nicht leer sein. |
| `description` | Eigene Missionsbeschreibung. Optional. |
| `map` | Mapreferenz nach dem unten beschriebenen Quellformat. |
| `settings` | Vanilla-Coop-Startpreset und Gebäudefreigaben. Fehlend bedeutet die unten genannten Standardwerte. |
| `players` | Zwei bis acht aktive Plätze. Die Reihenfolge bestimmt Mensch/Mensch/KIs. |
| `modSettings` | Optionaler Trail-Presetstand für die sieben unterstützten Settings-Mods. Fehlend bedeutet sieben deaktivierte Einträge. |

Unbekannte absolute Pfade werden nicht unterstützt. Bei `bundled` werden außerdem `..`-Pfadfluchten aus dem Missionsordner abgelehnt.

## Assetquellen

Jede Map-, Lord- und AIV-Referenz hat genau ein `source`-Feld:

| `source` | Auflösung |
| --- | --- |
| `builtIn` | Eingebauter Spielinhalt. Maps nutzen `name`; Lords/AIVs nutzen die numerische `id`. |
| `installed` | Vom Spiel katalogisierter Inhalt. Maps/Lords/AIVs nutzen `name`; Lords können zusätzlich `configuration`, AIVs zusätzlich `lordName` angeben. |
| `bundled` | Relative Datei innerhalb des Missionsordners über `file`. Zulässig sind nur `.map`, `.lordjson` bzw. `.aivjson`. |

Gebündelte Dateien werden direkt aus dem Pluginordner gelesen. Es wird nichts nach `CustomLords`, `ExtendedLords` oder in den User-Maps-Ordner kopiert.

### Map

Installierte oder eingebaute Map:

    "map": { "source": "installed", "name": "My Multiplayer Map" }

Gebündelte Map:

    "map": { "source": "bundled", "file": "MeineMission/map.map" }

Die Map muss vom Spiel als Multiplayer-Skirmish-Map erkannt werden und genügend Spielerplätze besitzen.

### Lord

Ein KI-Platz braucht `lord`. Beispiele:

    "lord": { "source": "builtIn", "id": 0 }

    "lord": {
      "source": "installed",
      "name": "Custom Rat",
      "configuration": "Aggressive",
      "baseLordId": 0
    }

    "lord": {
      "source": "bundled",
      "name": "Custom Rat",
      "file": "MeineMission/lords/custom-rat.lordjson",
      "baseLordId": 0
    }

`id` bzw. `baseLordId` ist die nullbasierte Vanilla-Lord-ID, deren Stimme und Grundidentität verwendet werden. Bei `builtIn` ist `id` maßgeblich. Bei `installed` unterscheidet `configuration` mehrere `.lordjson`-Konfigurationen desselben Lordnamens. Fehlt es, wird die erste katalogisierte Konfiguration gewählt. Das offizielle `.lordjson`-Format ist maßgeblich; eine TrailEditor-`internals.json` ist nicht erforderlich.

### AIV-Liste

Jeder KI-Platz besitzt eine geordnete `aivs`-Liste. Die Quellen dürfen gemischt werden:

    "aivs": [
      { "source": "builtIn", "id": 0, "rotation": 0 },
      { "source": "installed", "lordName": "Custom Rat", "name": "castle-2", "rotation": 0 },
      { "source": "bundled", "file": "MeineMission/aivs/castle-3.aivjson", "rotation": 0 }
    ],
    "preferredAiv": -1

`rotation` ist immer ein Gradwert `0`, `90`, `180` oder `270`; native Orientierungswerte wie `2`, `4` oder `6` sind ungültig. `preferredAiv` ist `-1` für die deterministische Auswahl des Spiels aus der vollständigen synchronisierten Liste oder ein nullbasierter Listenindex, der genau eine Variante erzwingt. Ohne `preferredAiv` müssen alle Varianten dieselbe Rotation besitzen, weil das Vanilla-Coop-Setup nur eine Rotation pro KI übertragen kann.

Lord- und AIV-Dateien werden mit dem offiziellen Spielparser geladen. Dadurch entstehen dieselben Laufzeitdaten und CRC-Werte wie bei normal registrierten Custom-Lords/AIVs.

## Spielerplätze

| Feld | Werte und Wirkung |
| --- | --- |
| `active` | `true`/`false`; fehlend bedeutet `true`. Inaktive Einträge werden übersprungen. |
| `team` | `1..8`. Beim ersten aktiven Spieler maßgeblich; der zweite aktive Spieler wird unabhängig von seinem JSON-Wert automatisch demselben Team zugeordnet. |
| `colour` | `0..7`, entsprechend den internen Spielerfarben. |
| `keepPosition` | `1..8`, eindeutig pro aktivem Spieler. Wählt die Startburgposition der Map. |
| `lord` | Nur ab dem dritten aktiven Spieler erforderlich; bestimmt dessen KI-Lord. |
| `aivs` | Geordnete Burgenliste der KI. Darf leer sein, dann nutzt das Spiel die normale eingebaute Auswahl. |
| `preferredAiv` | `-1` oder ein nullbasierter Index in `aivs`. |

Die ersten beiden aktiven Einträge sind Host und Gast. Alle weiteren aktiven Einträge werden KIs. Die Host-/Gast-Zuordnung hängt somit nicht von einer separaten `isCoop`-Option ab.

## Coop-Einstellungen

| Feld | Standard | Werte und Wirkung |
| --- | ---: | --- |
| `fairness` | `3` | `1` großer Menschenvorteil, `2` Menschenvorteil, `3` gleich, `4` KI-Vorteil, `5` großer KI-Vorteil. |
| `startingGoodsLevel` | `2` | `1` wenig, `2` normal, `3` Deathmatch, `4` verstecktes Niedrig-Gold-Preset. |
| `allowBarracksHost` | `true` | Host darf die Kaserne verwenden. |
| `allowMercenaryPostHost` | `true` | Host darf den Söldnerposten verwenden. |
| `allowStockadeHost` | `true` | Host darf den Stockade-/Belagerungsbereich verwenden. |
| `allowBarracksGuest` | `true` | Entsprechende Freigabe für den Gast. |
| `allowMercenaryPostGuest` | `true` | Entsprechende Freigabe für den Gast. |
| `allowStockadeGuest` | `true` | Entsprechende Freigabe für den Gast. |

Goldtabelle (`Mensch/KI`):

| Startlevel | Fairness 1 | Fairness 2 | Fairness 3 | Fairness 4 | Fairness 5 |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 8000/2000 | 4000/2000 | 2000/2000 | 2000/4000 | 2000/8000 |
| 2 | 8000/2000 | 4000/2000 | 2000/2000 | 2000/4000 | 2000/8000 |
| 3 | 40000/3000 | 20000/7000 | 10000/10000 | 7000/20000 | 3000/40000 |
| 4 | 4000/500 | 2000/500 | 500/500 | 500/2000 | 500/4000 |

Beliebiges Startgold, etwa exakt 500 nur für Menschen, wird anschließend über `modSettings.mods.StartConditions_Serp.settings.SetStartGoldHuman` festgelegt.

## Modsettings

`modSettings` verwendet Schema 1 und enthält immer Einträge für `BuildingCosts_Serp`, `BuildingLimit_Serp`, `ExtraFeatures_Serp`, `RandomEvents_Serp`, `StartConditions_Serp`, `UnitCosts_Serp` und `UnitLimit_Serp`. Ein Eintrag besitzt `enabled` sowie bei Aktivierung ein `settings`-Objekt mit den Propertynamen aus dem jeweiligen Mod-Menü. Bool-, Ganzzahl-, Kommazahl- und Stringwerte werden als native JSON-Typen geschrieben; mehrzeilige Listen wie die Startgüter bleiben Strings mit `\r\n`. Fehlt eine inzwischen hinzugekommene Host-Einstellung, verwendet der Mod automatisch deren aktuellen Defaultwert. Nicht mehr existierende Properties werden ignoriert und beim nächsten Speichern einer `.modjson` nicht erneut geschrieben.

Beim Auswählen einer Ersatzmission wird dieser Stand als schreibgeschütztes Preset `Trail` aktiviert. Das Dropdown kann jederzeit auf die lokalen Presets 1/2 wechseln. Diese verhalten sich auch im Missionsdialog wie gewohnt: Änderungen werden in den lokalen Presetdateien gespeichert und gelten für die Lobby/Partie. Das Missionspreset `Trail` selbst wird nie in die lokalen Presetdateien geschrieben. Lokal fehlende, laut Mission aktivierte Mods werden in der Missionsbeschreibung genannt, blockieren Ready/Play aber nicht. Fehlt `modSettings`, sind alle sieben Trail-Mods deaktiviert. Ein beschädigter oder typfehlerhafter Block deaktiviert transaktional den gesamten Trail-Stand.

## Multiplayer-Zuordnung

Eine Ersatzmission wird ausschließlich durch ihren Platz `CoopTrails\TrailN\NN.coopmission.json` ausgewählt. Es werden weder JSON noch Map-, Lord- oder AIV-Dateien zwischen Host und Gast gehasht oder übertragen. `Ready`, `ReadyLock` und `Play` werden deshalb nicht durch eine Dateiprüfung blockiert. Lade- und Validierungsfehler der jeweils lokalen Datei stehen mit Millisekunden-Zeitstempel in `BepInEx\LogOutput.log`.

## Validierungsfehler

Ein einzelner Platz wird verworfen und bleibt Vanilla, wenn unter anderem:

- JSON oder `schemaVersion` ungültig ist;
- Map/Lord/AIV fehlt oder der Katalogname nicht aufgelöst werden kann;
- die Map keine Multiplayer-Skirmish-Map ist oder zu wenig Plätze besitzt;
- ein gebündelter Pfad absolut ist bzw. den Missionsordner verlässt;
- Teams, Farben, Keep-Positionen oder AIV-Rotationen außerhalb des gültigen Bereichs liegen;
- Lord- oder AIV-Konfiguration nicht eindeutig bzw. nicht passend geladen werden kann.

## Entwicklung und Tests

`build.bat` führt die zusammengeführten Core-, Sidecar- und Strukturtests aus, baut das Plugin, erzeugt das lokale Paket unter `BepInEx\plugins\CustomCustomTrail_Serp` und installiert es atomar über einen temporären Ordner in die Spielinstallation. Die Runtime verwendet die installierte SHCDE-V2.8-`Assembly-CSharp.dll` und den lokalen Script-Extender-Fork als Referenz.
