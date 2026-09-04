# MoveMoatTest – Erkenntnisse und Übergabestand

Stand: 5. September 2026

## Aktueller Reparaturstand nach erneuter Codeprüfung

Die letzte Optimierung (`5f4e696b`, Vergleichsbasis `ce67cd30`) verursachte zwei
Regressionen. Der Lauf vom 5. September, 01:07–01:08 Uhr, belegt beide:

- Bei einem Move mit 20 grabfähigen Units wurden 20 Builder aufgerufen, aber nur ein
  Fallback ausgeführt. Nach `mode unit=2` wurde weiter `builder unit=1` protokolliert.
  Ein bereits qualifizierter `pendingPlan` der ersten Unit wurde für weitere Units
  wiederverwendet. Der neue Puffervergleich lehnte deren abweichenden Unitpuffer korrekt ab.
- Beim Fill wurden innerhalb einer Arbeitszielauswahl wiederholt zielgerichtete Suchen
  gestartet. Deren Cache existierte nur im Move-Command, nicht bei automatischen
  Arbeitsfolgezielen. Das Log zeigt ungefähr 480.000 besuchte Knoten und 285–317 ms
  Suchzeit je Unit-Auswahl. Die reine gewichtete Pfadoptimierung war dort deutlich kürzer.

Der reparierte Quellstand enthält folgende Änderungen:

- Mode-Freigaben gehören immer zur konkreten Unit. Ein passender aktiver Plan hat Vorrang;
  andernfalls wird der passende ausstehende Plan verwendet. Ein fremder äußerer Plan darf
  einen passenden Arbeitsplan nicht verdecken. Zentral übergebene Formationstargets bleiben
  erhalten, verschachtelte Planneraufrufe stellen ihren vorherigen Kontext wieder her.
- Der Builder wählt seinen Plan anhand des tatsächlichen Unitpuffers und Zielpaars.
  Vor dem Retry wird zusätzlich der native Startvertrag geprüft: aktuelles Tile bei
  `r_PathPlanStateBitFlags == 0 && r_MovingRelevant == 8`, sonst `r_NextTilePositionX2/Y2`.
  Ein Cachemiss im Modepfad wird berechnet; die reine Diagnose darf weiterhin nur nachsehen.
  Beide Zugriffe verwenden dieselbe Implementierung und denselben Cache-Schlüssel.
- Dig-/Fill-Auswahlen verwenden eine lazily aufgebaute Boden-/Friendly-Erreichbarkeitskarte
  für ihren exakten Spieler und Start. Positive und negative Endpunktentscheidungen werden
  innerhalb der Auswahl geteilt. Die unmittelbar zugehörige Resolver-/Builderübergabe
  erhält denselben Suchkontext. Neue Auswahlen bauen frisch auf, auch bei unverändertem
  Start. Tickwechsel, andere Units/Starts und ersetzte Suchkarten dürfen keine alten
  Endpunktentscheidungen einschleusen. Belegung und Arbeitsobjekt werden live nachgeprüft.
- Eine Suchkarte wird erst nach vollständig erfolgreicher Traversierung als Cache freigegeben.
  Ein abgebrochener Aufbau bleibt ungültig. Feindliche Wege werden nur bei ausdrücklich
  angeforderter konservativer Cursorunterscheidung zusätzlich berechnet.
- Negative oder fehlerhafte Retries stellen die 1000 Pufferbytes, den Ausgabepointer,
  die Länge, Route-Variante und den Moatmodus wieder her. Das gilt auch bei einer
  Audit-Exception. Positive Vanilla-Builderpfade werden weiterhin genau einmal ausgeführt.
  Owner-Audit und eng begrenzter terminaler Fill-Kontakt bleiben erhalten.
- Routine-Mode-/Pipeline-Details werden bei Gruppen- und Arbeitsauswahlen nicht mehr
  vorab formatiert. Aggregierte Such-/Pfadzähler und relevante Ergebnisse bleiben sichtbar.

Neue beziehungsweise ergänzte Logfelder:

- Move: `targetedSearches` zählt Qualifikationen, `targetedSearchPasses` die tatsächlichen
  Boden-/Friendly-Suchdurchläufe; außerdem `contractRejections` und `fallbackRollbacks`.
- Arbeitsauswahl: `searchBuilds`, `endpointQueries`, `endpointCacheHits`, `expanded`,
  `searchMs`, `elapsedMs`. Erwartet wird normalerweise ein Kartenaufbau je Auswahl,
  unabhängig von der Zahl ihrer Kandidaten. Eine verschachtelte fremde Suche kann einen
  erneuten Aufbau notwendig machen.
- Die ersten drei Puffer-/Kontextabweichungen erscheinen zusätzlich als
  `stage=fallback-contract-rejected`; weitere Fälle werden im Command gezählt.

Die erneute Codeprüfung und die automatisierten Regressionstests sind abgeschlossen:
**309 Assertions erfolgreich**, Syntaxprüfung aller sechs Runtime-Quelldateien.
Der eigenständige Runner unter `_inspect/MoveMoatRegressionTests` extrahiert mit Roslyn
48 tatsächliche Runtime-Member und kompiliert sie zusammen mit dem unveränderten
`WeightedMoatRoutePlanner` gegen simulierte native Grids und API-Adapter. Er prüft
27 Gruppenmitglieder, getrennte Puffer, Formationstargets, verschachtelte Arbeitskontexte,
positive/negative Caches, Belegungswechsel, Terrainänderungen, Tickablauf, feindliche Moats,
Start auf Moat, Audit-/Retry-Exceptions und Wiederanlauf nach einem Suchfehler.

Aufruf aus dem Workspace-Root:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj -- .

Build und Installation wurden am 5. September 2026 um 01:32 Uhr einmal über
`MoveMoatTest/build.bat /nopause` abgeschlossen: **0 Warnungen, 0 Fehler**.
Die installierten Dateien `MoveMoatTest.dll`, `MoveMoatTest.pdb` und `info.json`
stimmen per SHA-256 mit dem lokalen Buildpaket überein. DLL-Hash dieses Reparaturbuilds:

`7B03FB1789C84BBCC43EDDAB8EB8ACF7ACD194CBA6A63E8280DE92A7F8607122`

Diese Tests führen das Spiel nicht aus und belegen keine Ingame-Latenz oder vollständige
native Hookintegration. Die Gruppen-/Fill-Wiederholung im Spiel und der Multiplayer-Test
stehen weiterhin aus. Die historischen erfolgreichen Spieltests weiter unten sind keine
Abnahme dieses neuen Reparaturstands. Modversion bleibt während der Testphase `1.0.0`.

## Ziel und aktueller Vertrag

`MoveMoatTest` erlaubt ausgewählten Bodeneinheiten, fertige eigene oder verbündete Burggräben
als reguläre Wegkante zu benutzen. Der Mod bleibt Vanilla-first: Positive Vanilla-Ergebnisse
werden übernommen; ein Fallback greift nur, wenn Vanilla an einer Burggraben-Grenze scheitert
oder ein nachweislich schnellerer freundlicher Burggrabenweg veröffentlicht werden kann.

Die Fähigkeit ist absichtlich auf dieselben Unittypen begrenzt, die Vanillas Command 6
(`DigMoatTileId`) pro Unit akzeptiert. Maßgeblich ist der Inline-Switch in `0x11E960`, bestätigt
durch den Auswahlhelper `0x191C00` und dessen Call bei `0x8D3CE`:

- Bogenschütze (`CHIMP_TYPE_ARCHER`)
- Speerträger (`CHIMP_TYPE_SPEARMAN`)
- Pikeniere (`CHIMP_TYPE_PIKEMAN`)
- Streitkolbenkämpfer (`CHIMP_TYPE_MACEMAN`)
- Ingenieure (`CHIMP_TYPE_ENGINEER`)
- arabische Sklaven (`CHIMP_TYPE_ARAB_SLAVE`)
- Eunuchen (`CHIMP_TYPE_BEDOUIN_EUNUCH`)
- Plänkler (`CHIMP_TYPE_BEDOUIN_SKIRMISHER`)
- Sappeure (`CHIMP_TYPE_BEDOUIN_SAPPER`)
- Demolierer (`CHIMP_TYPE_BEDOUIN_DEMOLISHER`)

Assassinen, Armbrustschützen, Schwertkämpfer, Ritter und Belagerungsgeräte erhalten keinen
Moat-Fallback. Das historische Feld `unit+0x170` ist keine bestätigte Capabilityquelle.

Eigene und verbündete fertige Moats sind begehbar. Feindliche fertige Moats dürfen Arbeitsziel
zum Zuschütten sein, aber niemals Traversierungskante. Wasser, massive Gebäude und ungültige
Mauer-/Strukturkanten bleiben Vanilla.

## Native Bindung

Alle RVAs in diesem Dokument gelten ausschließlich für:

`CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Die installierte DLL und `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` müssen vor einer
Wiederverwendung übereinstimmen. Bei einem Update sind Pattern, vollständige Entrybytes,
Callziele, ABI und interne Kontrollflüsse erneut zu prüfen; andernfalls bleibt der Mod
fail-closed.

Wichtige Stellen:

| RVA | bestätigte Bedeutung |
| --- | --- |
| `0x11E960` | Tribe-Command-Dispatcher; Command 6/7 und per-Unit-Grabfilter |
| `0x191C00` | Auswahl enthält mindestens eine grabfähige Unit |
| `0x196840` | Unit steht aktuell auf einem fertigen Moat |
| `0x196870` | Auswahlarten-/Cursor-Gate vor genaueren Tileprüfungen |
| `0xE2CA0` | Tilepaarprüfung; ruft bei Regionsgrenzen den nativen Suchhelper auf |
| `0xE2610` / `0xE7C40` | frühe Regions-/Gruppenprüfungen; keine universellen booleschen Gates |
| `0x11B520` | gemeinsamer MoveHere-Gruppenpfad |
| `0x117BC0` / `0x119F90` | Gruppen-Moatmodus und Gruppeniterator |
| `0x196280` | per-Unit-Pfadanforderung und Bindung des echten Unit-Pfadpuffers |
| `0xF4930` | zentraler finaler Unit-Builder |
| `0xDAFD0` | nativer Moat-/Gruppenbuilder mit achtgerichtetem Tilegraph |
| `0xE1640` / `0xE4E90` | Rekonstruktion und nibble-codierter Pfad, maximal 2000 Schritte |
| `0xDBC60` | Annäherungssuche für `AttackUnit` |
| `0xB70C0` | Cursor-Erreichbarkeit eines Gebäude-Footprints |
| `0xDA020` / `0x123090` | Gebäude-Annäherung und Kandidatenverbrauch |
| `0x69D60` / `0x6AF60` / `0x6C490` | Auswahl und Auflösung von Dig-/Fill-Arbeitszielen |
| `0x1853F0` / `0x1976C0` | Wiederaufnahme gespeicherter Bewegung nach einem Kampf |
| `0x19B260` | wirksame Unitgeschwindigkeit und Moat-Verlangsamungsphase |
| `0x18410C` | Dispatchanker zur dynamischen Ermittlung möglicher `r_SpeedBonus`-Werte |
| `0x107160` | native Sonderstrukturprüfung für Treppen-/Mauer-/Rampenkanten |

`MoveMoatTest` detourt weder `0xD9C40` noch `0xDA590`. Die gemeinsame Installation mit
`BugfixesAndQoL` verwendet dessen Reflection-Bridge für die Moat-Arbeitszielhooks; im bestätigten
Lauf meldete `BugfixesAndQoL 1.0.126` `hookOwner=MoveMoatTest`. Es darf nur einen Owner dieser
Hookgruppe geben.

## Warum mehrere dünne Adapter nötig sind

Vanilla besitzt keinen einzelnen globalen Schalter „diese Unit darf Moats überqueren“. Cursor,
Gruppenregionsprüfung, Entity-Annäherung, Command-Zuweisung, Arbeitszielauswahl und finaler Builder
können jeweils vorher abbrechen. Ein frühes positives Cursorergebnis erzeugt noch keinen Pfad.

Die Lösung besteht deshalb aus einer gemeinsamen Traversierungsregel und schmalen Vanilla-first-
Adaptern an den Stellen, die den zentralen Builder sonst nicht erreichen würden. Entity-, Hover-
und Zielkontext wird nur zur sicheren Bindung verwendet; Vanilla bestimmt weiterhin Zielentity,
Annäherungstiles, Arbeitsreihenfolge und Formation. Es werden keine Commands künstlich aufgeteilt
oder erneut ausgegeben.

## Gemeinsame Traversierungsregel

Die owner-sichere Probe verwendet denselben achtgerichteten Kantenvertrag wie der gewichtete
Planer und ist an `0xDAFD0`/`0xE1640` angeglichen. Sie prüft Richtungs- und Bewegungsmasken, Höhe,
Diagonalbedingungen, StructureGrid und die native Sonderstrukturprüfung. Path-Regionen werden
nur noch für native Rückgabewerte und Diagnose verwendet, nicht als Traversierungs-Whitelist.

Die frühe Befehlsqualifikation flutet nicht mehr für jede Unit die vollständige Karte. Sie sucht
zielgerichtet zuerst ohne Moat und nur nach einem Fehlschlag mit eigenen beziehungsweise
verbündeten Moats. Eine feindliche Route wird ausschließlich dort berechnet, wo ein negativer
Cursorbefund sie wirklich unterscheiden muss. Starts auf einem freundlichen fertigen Moat werden
direkt als moatgebundener Start behandelt.

Innerhalb eines synchronen Gruppenbefehls werden boolesche Entscheidungen für gewöhnliche Ziele
nach Spieler, Startregion und Zielregion geteilt. Region `0`, Moat-, Struktur-, reservierte und
Arbeitsendpunkte bleiben an das exakte Tile gebunden. Formationsoffsets derselben Region lösen
somit keine vollständige Suche je Unit aus. Rekonstruierte Pfade werden nie regionsweise geteilt;
sie bleiben stets an das konkrete Start-/Zielpaar gebunden. Alle Command-Caches enden mit dem
synchronen Command.

## Funktionierende Bereiche

Folgende Fälle wurden in Editor und teilweise in Skirmish praktisch bestätigt:

- wiederholter normaler Move durch einen eigenen fertigen Moat;
- notwendige und optionale Moat-Routen sowie mehrere Bodenregionen;
- direktes Ziel auf einem eigenen Moat und Start auf einem eigenen Moat;
- Shift-Move-Queues mit notwendigen Moat-Wegpunkten an mehreren Queuepositionen;
- Patrol über Moat;
- `AttackUnit` einschließlich wiederholter Befehle und Sprite-Hover;
- `AttackBuilding` hinter eigenem Moat einschließlich vollständigem Gebäudesprite;
- kürzestes gültiges Gebäude-Annäherungsfeld statt eines festen Hovertiles;
- begehbare, reservierte Gebäude-Endpunkte;
- Post-Combat-Fortsetzung eines gespeicherten Moat-Move-Ziels;
- gemischte Gruppen vor, auf und hinter einem Moat;
- Gruppen aus grabfähigen und ungeeigneten Units; nur grabfähige Units erhalten den Fallback;
- direkte sowie automatische Folgeziele beim Ausheben und Zuschütten;
- feindlicher Moat als Fill-Arbeitsobjekt, aber nicht als erlaubte Wegkante;
- Treppen, Rampen und begehbare Wall-Top-Ziele hinter einem eigenen Moat;
- normales Assassinen-Mauerklettern ohne Moat bleibt Vanilla;
- KI-gesteuerte grabfähige Units nutzen ihren eigenen Moat;
- mehrere Befehle und mehrere Units pro Spielstart ohne den früheren Einmal-Effekt.

Die Gebäudeoptimierung reduzierte einen früheren großen KI-Fall von bis zu 16 vollständigen
Reachability-Suchen pro Unit auf ungefähr eine Karte je Unit und Zielregion. Gemessene
Gebäudephasen lagen anschließend ungefähr bei 10 ms (`0xDA020`) und höchstens 2,5 ms
(`0x123090`) statt einer Pause von rund 2,2 Sekunden.

Ein späterer siebenminütiger KI-Lauf zeigte dennoch, dass die allgemeine Qualifikation noch zu
teuer war: 16.521 MoveMoat-Zeilen (rund 8,5 MB), 634 vollständige Kartenaufbauten, bis zu 66.265
geprüfte Strukturkanten je Aufbau und ungefähr 1,3 Sekunden synchrone Modarbeit bei zehn
grabfähigen Units. In den auswertbaren Move-Befehlen summierte sich die Modzeit auf rund 32
Sekunden. Logging verstärkte die Pausen, Hauptursache war aber die Vollkartensuche pro Unit.

Die vorangegangene Optimierung ersetzte diese Vollkartensuchen im normalen
Commandpfad durch die oben beschriebene zielgerichtete, regionsweise geteilte Qualifikation.
`0x196840` liefert nur Vanillas Aussage, ob die konkrete Unit gerade auf einem fertigen Moat
steht, und startet keine eigene Suche mehr. Normale AI-/Bodenbefehle, leere Queue-Snapshots,
unveränderte Flood-Aufrufe und wiederholte Tick-/Stallzustände werden nicht mehr einzeln geloggt.
Ein Performanceeintrag entsteht bei Moat-Eingriff oder einem messbar langsamen Command.
Die dabei eingeführten Gruppen-/Fill-Regressionen und ihre Reparatur stehen im neuen
Abschnitt am Anfang dieses Dokuments.

Der letzte Strukturtest endete für 65 beobachtete Pfade am Ziel; 35 Moat-Eintritte und 35
Moat-Austritte wurden protokolliert. Es gab keine MoveMoat-Exception.

## Gewichtete Wegwahl

Der zentrale Publisher läuft am echten Unit-Pfadpuffer im `0xF4930`-Detour und ist nicht von
Move-, Attack-, Combat- oder Arbeits-Commands abhängig. Er kann daher auch automatische Dig-/Fill-
Folgewege optimieren.

Das Kostenmodell liest die Runtimefelder jeder konkreten Unit; es enthält keine feste
Unit-Geschwindigkeitstabelle. Berücksichtigt werden `r_CurrentSpeed`, `r_CurrentSpeed2`,
`r_SpeedBonus`, zusätzliche Teilschritte/Verzögerung und die Moatphase aus `0x19B260`. Eine
Moatkante verwendet den bestätigten stabilen Delay-Aufschlag `+6`. Strukturpfade werden mangels
kalibrierter Strukturkosten nicht durch den gewichteten Publisher ersetzt.

Ein Kandidat wird nur veröffentlicht, wenn:

- er mindestens eine eigene oder verbündete Moatkante verwendet;
- jede Kante und der nibble-codierte Roundtrip gültig sind;
- er unter jedem aus dem nativen Handler dekodierten plausiblen `SpeedBonus` strikt schneller
  als Vanillas Pfad bleibt;
- er im beim Builder erfassten tatsächlichen Runtimeprofil mindestens 40 Ticks spart.

Die frühere Regel verlangte 40 Ticks Ersparnis unter jedem theoretischen Profil. Der Fill-Test
vom 4. September zeigte deshalb innerhalb derselben Gruppe unterschiedliche Wege: 21
Fill-Auswertungen blieben trotz schnellerem Shadow-Pfad unveröffentlicht; darunter wurden 11
ausdrücklich allein wegen der alten Profilregel zurückgewiesen. Weitere 11 Fill-Pfade wurden
veröffentlicht. Bei den betroffenen Macemen war der tatsächlich erfasste `SpeedBonus` 0. Die
Regel wurde deshalb präzisiert: Alternative Profile
dürfen den Kandidaten weiterhin niemals langsamer machen, die volle Sicherheitsmarge gilt aber
für den konkreten Runtime-Snapshot. Damit soll die beobachtete unnötige lange Wegwahl beseitigt
werden, ohne einen unter irgendeinem bekannten Profil schlechteren Pfad zu veröffentlichen.

## Owner-Sicherheit veröffentlichter Fallbackpfade

Positive Vanilla-Builderpfade bleiben unverändert. Nur ein durch den Mod nach einem echten
Vanilla-Nuller erzeugter nativer Retry wird anschließend am tatsächlichen nibble-codierten
Unit-Pfad vollständig auditiert. Eigene und verbündete Moat-Tiles sind zulässig; ein fremder
Moat, eine fremde reine Diagonalecke oder ein ungültiger Owner macht den Retry unsicher.

Beim Zuschütten darf ausschließlich das exakt gebundene feindliche Arbeits-Moat einmal als
terminaler Arbeitskontakt vorkommen: Es muss der vorletzte Pfadknoten direkt vor Vanillas
Annäherungstile sein und darf weder wiederholt noch als Durchgang verwendet werden. Findet der
Audit einen anderen fremden Moat, berechnet der Mod nur dann einen exakten owner-sicheren
Ersatzpfad zum unveränderten Vanilla-Ziel. Der Ersatz wird über denselben 1000-Byte-Unitpuffer,
Längenvertrag und Decode-Roundtrip veröffentlicht. Scheitert irgendeine Prüfung, werden Puffer,
Länge und Builderzustand auf den Stand vor dem Retry zurückgesetzt.

`ownerSafetyViolation` bezeichnet damit nur noch einen vom Mod veröffentlichten Pfad mit fremdem
Nicht-Ziel-Moat oder ungültigem Owner. Ein unveränderter positiver Vanilla-Pfad wird nicht mehr
als Modverletzung klassifiziert.

## Verworfen oder ersetzt

- Globale Bytepatches im Cursordispatcher verursachten falsche Mauer-/Klettercursor und wurden
  vollständig entfernt.
- Der Assassin-Sonderpfad über `pathManager+0x88` und eine Reflection-Routenbrücke erzeugte zwar
  kombinierte Probewege, Vanilla konsumierte sie aber nicht zuverlässig. Assassinen sind nach
  dem finalen Capabilityvertrag ohnehin ausgeschlossen; dieser Sondercode wurde entfernt.
- Eine auf Start-/Zielregion beschränkte Managed-BFS schnitt gültige Zwischenregionen ab und
  wurde durch die regionsunabhängige Tilegraph-Probe ersetzt.
- Das nachträgliche Erfinden fehlender Gebäude-Kontexttiles (`candidate+4`) war falsch. Der
  Fallback greift jetzt früher in `0xDA020` ein, sodass Vanilla vollständige Kandidatenpaare
  erzeugt.
- Gebäude-Hover darf nicht von gelegentlich auf `(0,0)` springenden Cursor-X/Y-Globals abhängen.
  Maßgeblich sind Vanillas Hover-Building-ID, ein gültiges Mouse-Tile und das nächstgelegene echte
  StructureGrid-Tile desselben Gebäudes.
- Hochfrequente Shadow-, Per-Tick-, Stall-, leere Queue- und gewöhnliche Bodenwegdiagnosen wurden
  entfernt. Beibehalten sind verwendete Fallbacks/gewichtete Veröffentlichungen, Rollbacks,
  Exceptions, Ownerverletzungen und aggregierte langsame Commands.

## Noch offen beziehungsweise erneut zu bestätigen

- Der reparierte Unit-Kontext, der zielgerichtete Command-Cache, die gebündelte Arbeitszielsuche
  und der abschließende Retry-Pfadaudit benötigen einen
  gezielten Performance- und Fill-Wiederholungstest. Erwartet wird höchstens eine Qualifikation je
  Start-/Zielregion statt einer Suche je Formationsoffset oder Unit.
- Der früher als `ownerSafetyViolation=True` gemeldete Fill-Fall ist mit dem alten Log allein nicht
  eindeutig: Vanilla darf beim Zuschütten das feindliche Arbeitsobjekt berühren. Der neue Audit
  unterscheidet genau diesen einmaligen terminalen Kontakt von echter Durchquerung und rollt nur
  letztere zurück beziehungsweise ersetzt sie owner-sicher.
- Verbündete Moats verwenden denselben Allianzfilter wie eigene Moats, wurden aber nicht in allen
  Befehls- und Gruppenvarianten praktisch wiederholt.
- Multiplayer wurde architektonisch vorbereitet (`NetworkMode: 1`, deterministische Daten,
  kein eigenes Netzwerkprotokoll), aber ein Host-/Client-Smoke-Test mit identischen Modversionen
  und Desyncbeobachtung steht noch aus.
- `ForceAttackBuilding` konnte nicht in jedem Aufbau gezielt ausgelöst werden; der gemeinsame
  Gebäudeweg unterstützt den Commandwert, die vollständige praktische Abnahme ist offen.

## Empfohlener kurzer Abschlusstest

1. Gruppen mit 1, 5, 10 und 27 grabfähigen Units zunächst über normalen Boden und danach über
   einen zwingenden freundlichen Moat schicken. Normale Befehle dürfen keinen Fallback auslösen;
   gleiche Startregionen sollen Cachetreffer statt proportionaler Suchen zeigen.
2. Mehrere grabfähige Units gleichzeitig feindliche Moats zuschütten lassen. Ein terminaler
   Zielkontakt muss als zulässig erscheinen; jeder fremde Durchgang muss ersetzt oder vollständig
   zurückgerollt werden.
3. Optionalen kurzen Moat, gemischte Gruppen, Shift-Queue, Patrol, `AttackUnit`,
   `AttackBuilding`, Dig und Treppe regressionsprüfen.
4. KI-Skirmish mindestens zehn Minuten schnell vorlaufen lassen. Ziel sind typische Commands
   deutlich unter 50 ms, 27 gleiche Startregionen möglichst unter 100 ms, keine harten Pausen,
   keine Exceptions und keine tausenden Diagnosezeilen.
5. Danach Host und Client mit identischen Paketen starten, eigenen sowie feindlichen Moat testen
   und auf Desync/Exceptions achten.

## Update-Reihenfolge

Bei einer neuen Spiel- oder Extender-DLL:

1. installierten DLL-Hash gegen `CURRENT.json` prüfen;
2. zuerst `0x196280 -> 0xF4930`, echten Unit-Pfadpuffer und `0xDAFD0 -> 0xE1640` wiederfinden;
3. Command-6-Switch `0x11E960` und Auswahlhelper `0x191C00` erneut abgleichen;
4. Cursor-/Entitypfade `0x196870`, `0xE2CA0`, `0xB70C0`, `0xDBC60`, `0xDA020`, `0x123090`
   validieren;
5. Arbeitszielkette `0x69D60`, `0x6AF60`, `0x6C490` und Bridge-Ownership prüfen;
6. Geschwindigkeitsvertrag `0x19B260` und Unit-Handlerdispatch erneut dekodieren;
7. erst danach Hooks installieren; jede nicht vollständig validierte Gruppe bleibt Vanilla.

README und Modversion wurden während dieser Testphase bewusst nicht geändert.
