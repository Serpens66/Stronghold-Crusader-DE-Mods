# MoveMoatTest – Erkenntnisse und Übergabestand

Stand: 4. September 2026

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

Die Probe führt getrennte Zustände für:

- ohne Moat erreichbare Tiles;
- über mindestens einen eigenen/verbündeten Moat erreichbare Tiles;
- rein diagnostisch nur über feindlichen Moat erreichbare Tiles.

Starts auf einem freundlichen fertigen Moat beginnen direkt im freundlichen Zustand. Der Cache
ist nur an Kartenepoch, Spieler und Starttile gebunden und wird nicht commandübergreifend
persistiert. Dadurch funktionieren auch mehrere Bodenregionen zwischen zwei Moatabschnitten.

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

Die Gebäudeoptimierung reduzierte den beobachteten großen KI-Fall von bis zu 16 vollständigen
Reachability-Suchen pro Unit auf ungefähr eine Karte je Unit und Zielregion. Gemessene
Gebäudephasen lagen anschließend ungefähr bei 10 ms (`0xDA020`) und höchstens 2,5 ms
(`0x123090`) statt einer Pause von rund 2,2 Sekunden.

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
- Pro-Hover-Diagnosen und die alte Assassin-/Builderzustandsdiagnose wurden nach erfolgreicher
  Abnahme entfernt. Funktionsrelevante Scopes und deduplizierte Pfad-/Owner-Meilensteine bleiben.

## Noch offen beziehungsweise erneut zu bestätigen

- Die neue, weniger überkonservative gewichtete Fill-Gruppenregel benötigt einen gezielten
  Wiederholungstest mit mehreren gleichzeitig zuschüttenden Units.
- Ein früher Lauf meldete bei einem Fill-Folgeweg ein beobachtetes feindliches Moat-Tile. Die
  aktuelle Diagnose unterscheidet nun Arbeitsziel von echter Traversierung und prüft den Owner am
  Pfadende erneut. Bis ein entsprechender Lauf `workTarget` oder `traversed` eindeutig bestätigt,
  ist dieser einzelne Owner-Befund nicht endgültig geklärt.
- Verbündete Moats verwenden denselben Allianzfilter wie eigene Moats, wurden aber nicht in allen
  Befehls- und Gruppenvarianten praktisch wiederholt.
- Multiplayer wurde architektonisch vorbereitet (`NetworkMode: 1`, deterministische Daten,
  kein eigenes Netzwerkprotokoll), aber ein Host-/Client-Smoke-Test mit identischen Modversionen
  und Desyncbeobachtung steht noch aus.
- `ForceAttackBuilding` konnte nicht in jedem Aufbau gezielt ausgelöst werden; der gemeinsame
  Gebäudeweg unterstützt den Commandwert, die vollständige praktische Abnahme ist offen.

## Empfohlener kurzer Abschlusstest

1. Mehrere grabfähige Units gleichzeitig mehrere feindliche Moats zuschütten lassen; prüfen,
   dass alle den schnelleren eigenen/verbündeten Moatweg wählen.
2. Dabei einen Fill-Folgeweg erzeugen, dessen Arbeitsobjekt ein feindlicher Moat ist; die
   Ownerdiagnose muss `workTarget` statt `traversed` melden oder andernfalls den echten Fehler
   eindeutig zeigen.
3. Move, Shift-Queue, Patrol, `AttackUnit`, `AttackBuilding`, Dig und Treppe jeweils einmal kurz
   regressionsprüfen.
4. Danach Host und Client mit identischen Paketen starten, eigenen sowie feindlichen Moat testen
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
