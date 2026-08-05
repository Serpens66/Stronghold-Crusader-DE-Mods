# Coop Trail Replacer

`CoopTrailReplacer` ersetzt einzelne Missionen der eingebauten Coop-Trails durch lokale `*.coopmission.json`-Bundles. Zur Laufzeit werden keine `.trail`-Dateien benötigt. Map, Lord-Konfigurationen (`.lordjson`) und Burgen (`.aivjson`) dürfen aus der Spielinstallation stammen oder direkt mit einer Mission geliefert werden.

Der Mod ist für SHCDE V2.8 gebaut und benötigt BepInEx sowie den SHCDE Script Extender. Host und Gast müssen den Mod und exakt dieselben verwendeten Dateien besitzen.

## Installation und Platzzuordnung

Die installierte Struktur lautet:

    BepInEx\plugins\CoopTrailReplacer_Serp\
      CoopTrailReplacer.dll
      CoopTrailReplacer.Core.dll
      CoopTrails\
        Trail1\
          01.coopmission.json
          ...
          10.coopmission.json
        Trail2\
        Trail3\
        Trail4\

`01.coopmission.json` ersetzt Platz 1, `10.coopmission.json` Platz 10. Fehlende oder ungültige Dateien ändern den jeweiligen Vanilla-Platz nicht. Unterordner sind erlaubt; der Wurzelordner einer Mission ist immer der Ordner, in dem ihre `*.coopmission.json` liegt.

Wichtige V2.8-Einschränkung: Die installierte Assembly besitzt nur die echten Missionsarrays `CoopTrail1`, `CoopTrail2` und `CoopTrail3`. Die irreführend benannte Klasse `FRONT_CoopTrail4` ist die Custom-Game-Lobby und kein vierter Vanilla-Missionstrail. `Trail4` bleibt deshalb für eine mögliche spätere Spielversion reserviert; Dateien darin werden in V2.8 mit einer verständlichen Logmeldung ignoriert.

Zum Bauen und Installieren `build.bat` als Administrator ausführen. `/nopause` unterdrückt die Abschlussabfrage.

## Vollständiges JSON-Beispiel

Siehe `Examples\01.coopmission.json.example`. Die Dateiendung `.example` verhindert ein unbeabsichtigtes Laden. Nach dem Kopieren nach `CoopTrails\TrailN\01.coopmission.json` müssen alle referenzierten Dateien bzw. installierten Namen existieren.

## Oberste Felder

| Feld | Werte und Wirkung |
| --- | --- |
| `schemaVersion` | Muss in dieser Version `1` sein. Andere Versionen werden abgelehnt. |
| `displayName` | Eigener Missionsname in der Coop-Lobby. Darf nicht leer sein. |
| `description` | Eigene Missionsbeschreibung. Optional. |
| `map` | Mapreferenz nach dem unten beschriebenen Quellformat. |
| `settings` | Vanilla-Coop-Startpreset und Gebäudefreigaben. Fehlend bedeutet die unten genannten Standardwerte. |
| `players` | Zwei bis acht aktive Plätze. Die Reihenfolge bestimmt Mensch/Mensch/KIs. |
| `startConditions` | Exakte Ressourcen- und Truppenänderungen für Menschen und KIs. |

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

Beliebiges Startgold, etwa exakt 500 nur für Menschen, wird anschließend über `startConditions.setStartGoldHuman` festgelegt.

## Startbedingungen

`startConditions` verwendet denselben Kern wie der Mod `StartConditions`:

| Feld | Werte und Wirkung |
| --- | --- |
| `setStartGoldAI`, `setStartGoldHuman` | `-1` lässt Vanilla-Gold unverändert; `0..100000` setzt es exakt. |
| `addStartGoldAI`, `addStartGoldHuman` | `-100000..100000`, wird nach dem Setzen addiert. Negative Werte ziehen Gold ab. |
| `multiplyStartTroopsAI`, `multiplyStartTroopsHuman` | `0..100`; `0` entfernt vorhandene Starttruppen, `1` lässt sie unverändert, höhere Werte vervielfachen sie. |
| `startGoodsAI`, `startGoodsHuman` | Objekt `Schlüssel: Menge`; `-1` lässt dieses Gut unverändert, `0..100000` setzt es exakt. Nicht genannte Güter bleiben unverändert. |
| `addStartTroopsAI`, `addStartTroopsHuman` | Objekt `Truppenschlüssel: Anzahl`; `0..100000` zusätzliche Einheiten nach der Multiplikation. |

Unterstützte Güterschlüssel:

    STORED_WOOD_PLANKS, STORED_RAW_HOPS, STORED_STONE_BLOCKS,
    STORED_IRON_INGOTS, STORED_PITCH_RAW, STORED_RAW_WHEAT,
    STORED_FOOD_BREAD, STORED_FOOD_CHEESE, STORED_FOOD_MEAT,
    STORED_FOOD_FRUIT, STORED_FOOD_ALE, STORED_FLOUR, STORED_BOWS,
    STORED_CROSSBOWS, STORED_SPEARS, STORED_PIKES, STORED_MACES,
    STORED_SWORDS, STORED_LEATHER_ARMOUR, STORED_METAL_ARMOUR

Unterstützte Truppenschlüssel:

    CHIMP_TYPE_ARCHER, CHIMP_TYPE_SPEARMAN, CHIMP_TYPE_MACEMAN,
    CHIMP_TYPE_XBOWMAN, CHIMP_TYPE_PIKEMAN, CHIMP_TYPE_SWORDSMAN,
    CHIMP_TYPE_KNIGHT, CHIMP_TYPE_ENGINEER, CHIMP_TYPE_MONK,
    CHIMP_TYPE_LADDERMAN, CHIMP_TYPE_TUNNELER, CHIMP_TYPE_ARAB_BOW,
    CHIMP_TYPE_ARAB_SLAVE, CHIMP_TYPE_ARAB_SLINGER,
    CHIMP_TYPE_ARAB_ASSASIN, CHIMP_TYPE_ARAB_HORSEMAN,
    CHIMP_TYPE_ARAB_SWORDSMAN, CHIMP_TYPE_ARAB_GRENADIER,
    CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, CHIMP_TYPE_BEDOUIN_HEALER,
    CHIMP_TYPE_BEDOUIN_EUNUCH, CHIMP_TYPE_BEDOUIN_AMBUSHER,
    CHIMP_TYPE_BEDOUIN_SKIRMISHER, CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
    CHIMP_TYPE_BEDOUIN_SAPPER, CHIMP_TYPE_BEDOUIN_DEMOLISHER

Bei installiertem `StartConditions` erhält dieser nur einen temporären Missions-Override; gespeicherte Lobbysettings werden nicht geändert. Fehlt das Plugin, verwendet `CoopTrailReplacer` denselben verlinkten Kern selbst. Wird die Mission über die von `SomeSettings` angebotene anpassbare Skirmish-/MP-Lobby geöffnet, wird der Missions-Override gelöscht und nur die vom Host gewählte StartConditions-Konfiguration angewendet. Der Kontext wird beim Missionswechsel und Mapende zurückgesetzt.

## Multiplayer-Prüfung

Aus kanonischem JSON und den tatsächlich verwendeten Map-, Lord- und AIV-Bytes wird SHA-256 berechnet. Host und Gast senden Schema-Version, Trail, Platz und Hash über ein explizit formatiertes Script-Extender-Paket. `Ready`, `ReadyLock` und `Play` werden blockiert, bis der Partner denselben Hash bestätigt hat.

Lobbyanzeige:

| Suffix | Bedeutung |
| --- | --- |
| `[checking files]` | Noch keine passende Bestätigung vom Partner. Der Partner hat den Mod eventuell nicht. |
| `[asset mismatch]` | JSON oder mindestens eine tatsächlich verwendete Datei unterscheidet sich. |

Es findet absichtlich keine automatische Dateiübertragung statt. Die genaue Ursache eines ungültigen Platzes oder einer Auflösungs-/Hashabweichung steht mit Millisekunden-Zeitstempel in `BepInEx\LogOutput.log`.

## Validierungsfehler

Ein einzelner Platz wird verworfen und bleibt Vanilla, wenn unter anderem:

- JSON oder `schemaVersion` ungültig ist;
- Map/Lord/AIV fehlt oder der Katalogname nicht aufgelöst werden kann;
- die Map keine Multiplayer-Skirmish-Map ist oder zu wenig Plätze besitzt;
- ein gebündelter Pfad absolut ist bzw. den Missionsordner verlässt;
- Teams, Farben, Keep-Positionen oder AIV-Rotationen außerhalb des gültigen Bereichs liegen;
- Lord- oder AIV-Konfiguration nicht eindeutig bzw. nicht passend geladen werden kann.

## Entwicklung und Tests

`build.bat` führt die Core-Tests aus, baut das Plugin, erzeugt das lokale Paket unter `BepInEx\plugins\CoopTrailReplacer_Serp` und installiert es atomar über einen temporären Ordner in die Spielinstallation. Die Runtime verwendet die installierte SHCDE-V2.8-`Assembly-CSharp.dll` und den lokalen Script-Extender-Fork als Referenz.
