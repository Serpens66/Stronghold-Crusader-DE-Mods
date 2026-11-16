# G10: Wegform um größere Hindernisse – Offline-Messung

Messung vom 16.11.2026. Basis ist der Kern im Commit `2cc3cccfc`, der unverändert dem Stand
seit `30e321701` entspricht. Kandidat ist der Arbeitsstand mit `StraightenRoute`
(ABI `0x04000012`). Beide liefen nacheinander auf demselben Rechner und mit identischen
Anfragen. Ausführung: x64-C++, MSVC, `/O2`, 4 Threads, offline. Es lief keine
Unity-/Mono-Messung und kein Spiel. Die Zeiten stammen aus einer einzelnen Messserie und
sind keine Latenzgarantie; Lauf-zu-Lauf-Schwankungen von ±20 % wurden beobachtet.

Werkzeug: [tools/RouteShape/Run-RouteShape.ps1](../tools/RouteShape/Run-RouteShape.ps1).
Lasten: synthetische 256×256-Szenarien sowie der echte 800×800-Capture
`Testmods/GroupPathProbe/input/capture-20261027-182413-691-s1.raw.gz`, exportiert mit
`GroupPathProbe --export-anchor-loads`. Kompakte Nachweise liegen unter `.work/a/g10/001/full5/`
(`compare.txt`, `baseline/summary.csv`, `candidate/summary.csv`).

## Ursache

Jeder Schritt kostet dieselbe Zeit, gerade wie diagonal (siehe Baseline
[GROUND_ROUTE_BUILDERS](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUND_ROUTE_BUILDERS.md)).
Dadurch gibt es zu jedem Ziel sehr viele gleich teure Wege.

Der Abstieg im Kern wählt unter gleich teuren Nachbarn den niedrigsten Richtungsindex.
Daraus entstehen große L-Formen. Der Direktstrahl aus 0.1.8 hilft nur, wenn die gesamte
Strecke frei ist. Die Wege über den Anker und den Boxrand kosteten zusätzlich echte Umwege.

## Umsetzung

`StraightenRoute` bearbeitet jede gefundene Kernroute nach. Ausgehend von jedem behaltenen
Knoten sucht sie per Galopp und anschließender Bisektion einen möglichst weit entfernten
späteren Routenknoten, dessen Rasterstrahl zwei Bedingungen erfüllt:

- Jede Kante ist in der Sicht der Aufgabe erlaubt.
- Der Strahl kostet höchstens so viel wie der ersetzte Abschnitt.

Gibt es im Bodenprofil keine Strafkachel, ist jede Kante gleich teuer. Dann entfällt die
Kostenrechnung, und nur die Kantenbits werden geprüft.

## Ergebnis

Auf den Lasten, die beide Varianten mit identischen Anfragen beantwortet haben, gilt:

- Es gibt **keinen Statuswechsel und keine teurere Route** (`compare.txt`: 206.320 Routen).
- 31.721 Routen sind billiger, zusammen um 170.306 Kosteneinheiten.

Fehlgeschlagene Einzelrouten im Bench (NoMemory des gezählten Kontingents bei Bodenkosten 3
auf 800×800) traten in beiden Varianten gleich auf.

Die Wegform wird als Zahl der maximalen digital geraden Stücke pro Route gemessen. Ein
Bresenham-Strahl ist ein Stück, ein großes L sind zwei. Bei den Einzelrouten ist der
Gewinn ähnlich.

| Last (Gruppe, Bodenprofil) | Stücke/Route vorher → nachher | Gruppenzeit ms vorher → nachher | Begradigen µs/Route |
|---|---:|---:|---:|
| syn-wall | 4,43 → 2,42 | 4,49 → 5,87 | 2,2 |
| syn-block | 3,08 → 1,67 | 3,13 → 4,16 | 2,1 |
| syn-ring (Mauerring mit Tor) | 3,44 → 1,88 | 3,36 → 4,42 | 1,9 |
| syn-choke (drei Engstellen) | 9,53 → 8,23 | 5,48 → 10,41 | 5,5 |
| syn-moat, Grabenprofil | 4,00 → 1,69 | 4,27 → 6,09 | 2,3 |
| cap-group-1024 | 38,48 → 19,87 | 3,47 → 5,00 | 7,6 |
| cap-group-mix-10000 | 17,75 → 11,09 | 50,44 → 62,77 | 4,7 |
| cap-converging-groups-10000 | 11,72 → 6,82 | 49,53 → 55,94 | 3,6 |
| cap-scattered-group-mix-10000 | 16,26 → 8,88 | 54,99 → 66,27 | 4,9 |
| cap-worker-trips-10000 | 11,85 → 6,82 | 14,93 → 22,97 | 3,7 |
| cap-moat-group-mix-10000 (Bodenprofil, Wege ~880 Schritte) | 6,11 → 3,01 | 36,28 → 62,65 | 10,4 |

Die euklidische Weglänge im Verhältnis zur Luftlinie sinkt im Capture um 3–7 %, zum
Beispiel bei `cap-group-mix-10000` von 1,37 auf 1,31.

## Kosten

**Einzelwege:** Die Zeit der Einzelwege bleibt im Rahmen der Messschwankung unverändert.
Ihre Suche kostet je Anfrage 0,1–6 ms; das Begradigen trägt wenige µs dazu bei.

**Gruppenbefehle:** Pro Einheit kommen etwa 2–10 µs CPU hinzu. Über 4 Threads gemessen
macht das bei 10.000 Einheiten +6 bis +26 ms aus. Bei üblichen Befehlen mit bis zu einigen
hundert Einheiten liegt der Zuschlag unter etwa 1–2 ms.

**Speicher:** Je Arbeitsbereich kommen drei wiederverwendete Puffer in Routenlänge hinzu.
Es gibt keinen Feldspeicher und keine zusätzliche Suche.

**Arbeitsgrenze:** Die Grenze von `16*Schritte+64` Zellen wurde in keiner Last erreicht
(`StraightenCapped=0`).

## Grenzen und offene Punkte

**Geprüfte Varianten:** Eine Interpolationssuche auf den Fehlschlagpunkt des Strahls
brachte keine Ersparnis. Eine Bisektion mit 1/8-Toleranz sparte kaum Strahlen und
verschlechterte die Wegform. Beide wurden verworfen.

**Messraum:** Alle Metriken gelten im Kachelraum. Da die Kamera drehbar ist, wird keine
Bildschirmprojektion angenommen.

**Verbleibende Knicke an Hindernissen:** Viele kleine Einzelhindernisse („forest“) und
Engstellen lassen naturgemäß mehrere Knicke übrig. Ob Einheiten an eng umrundeten Ecken
stärker drängeln, ist nur im Spiel prüfbar; das ist offen.

**Ausstehende Abnahmen:** Ingame-Abnahme, PerfProbe-A/B und Mehrspielerprüfung stehen aus.

## Spieltest vom 16.11.2026 (Einzelspieler)

Testmod `Testmods/PathfindingLoadTest`, Szenarien `shape-gap`, `shape-end`, `shape-far`,
`wall-5000` und `wall-5000-long` auf `test AI.map`, Speed 250. Die Plugin-Layouts sind Kopien
der installierten Mods (inklusive RandomEvents) und unterscheiden sich nur in `Pathfinding_Serp`:

- **A:** veröffentlichtes 0.1.8.
- **B:** Arbeitsstand mit `StraightenRoute`.

Der Runner stellte nach jedem Lauf Plugins und Einstellungen geprüft wieder her. Die Rohlogs
wurden nach der Auswertung entfernt; die Zahlen stehen hier.

**Wegform, 100 Spearmen:**

- Alle Läufe bestanden. Die veröffentlichten Routen sind gültig: keine ungültigen Richtungen,
  jedes Ende ist das Ziel des Mitglieds. Alle Mitglieder kamen zur Ruhe.
- Die Routen von A und B waren identisch. Gemessen wurden 3,07 / 3,72 / 3,27 Stücke pro Route
  bei 50 / 57 / 114 Schritten.
- Laut Pathfinding-Diagnose liefen bei diesem Befehl nur 18–19 Mitglieder über den
  Pathfinding-Routenbau. Alle wurden als Einzelrouten beantwortet, und
  `StraightenRoute` sparte dabei nichts. Diese Kernwege waren in dieser
  Geometrie bereits gerade.
- Die übrigen Routen stammen nicht aus dem Kern. Die im Offline-Bench gemessene Verbesserung
  (große Gruppen auf dem echten Capture) war in diesen Spielszenarien deshalb nicht sichtbar,
  eine Verschlechterung aber auch nicht.
- Die Ankunft war gleich: alle Mitglieder zur Ruhe nach 1050–1650 Ticks, bei A und B gleich
  innerhalb von ±50 Ticks; nur `old-end-diag` brauchte 2400 Ticks.

**Leistung, 5000 Spearmen** (alternierend A/B, auch in umgekehrter Reihenfolge):

- Befehlsdauer A 267–276 ms, B 250–298 ms, gleich innerhalb der Streuung.
- Ohne die unten beschriebene Neuberechnung verhalten sich A und B gleich: Stichprobenabstand
  0,4 s, nahezu gleiche Ankunftskurven über 30 Stichproben.

**Gefundene Auffälligkeit, nicht durch G10 verursacht:**

- Nach dem Befehl fordert Pathfinding die Spielermaske neu an. Meldet der Neuaufbau
  eine Zugangsänderung (`accessChanges=1`, `resweepWhy portals=1`), folgt eine
  kartenweite Erreichbarkeits-Neuberechnung: `sweepNodes` bis 262.144 pro Fenster,
  zusammen 690–1.077 ms CPU.
- Mit 5000 gleichzeitig laufenden Einheiten blieb die Simulation dabei 7–9 s
  ununterbrochen belegt. Die Main-Thread-Tickabfrage bekam die Engine-Sperre nicht,
  die Einheiten liefen aber weiter.
- Häufigkeit der Neuberechnung: A in 1 von 9 Läufen (`old-far`), B in 5 von 9 Läufen; vier davon
  meldeten eine Zugangsänderung, `new-far` nicht.
- Der Code dafür ist von G10 unberührt. Die ungleiche Häufigkeit ist nicht erklärt; sie
  hängt vermutlich vom Zeitpunkt der Zugangsänderung relativ zum Maskenaufbau ab.
- Ob die Belastung als FPS-Einbruch sichtbar wird, ist offen. Das ist ein eigener
  Untersuchungspunkt.

## Aufklärung der Neuberechnung (16.11.2026, behoben im Arbeitsstand 0.1.9)

**Ursache:** Der Wert an `pathManager+0` ist keine Eintragsanzahl. `F4540` setzt ihn jeden Tick
auf die höchste belegte Portal-Id + 1. Neue Einträge legt nur `D8510` an: Torhäuser über die
Platzierung `0x6D580` und Leitern (Typ 1, ohne Gebäude) über `0x1950B0`. Die Rat-KI auf
`test AI.map` baut in manchen Läufen kurz nach Kartenstart ein Torhaus. Das erklärt die
sporadische Häufigkeit; mit G10 hängt sie nicht zusammen.

**Mechanik:** Jede Änderung dieses Werts startete eine Neuberechnung aller Ankermasken
(1024 Knoten pro Tick, rund 625 Ticks, etwa 1,3 ms pro Tick). Wurde währenddessen eine
Spielermaske fertig, begann die Runde neu. Bei Speed 250 mit 5000 Einheiten hat die
Simulation nur etwa 0,3 ms Spielraum pro Tick. Vanilla (`Director.runSimTickTest`) baut
daraufhin einen unbegrenzten Rückstand an `FramesToProcess` auf und holt ihn danach ohne
Pause nach. Die Spielgeschwindigkeit sinkt, die Bildrate bleibt laut PerfProbe unverändert
(etwa 280–296 FPS).

**Behebung:**
- Einträge behalten ihre Id. Eine verschobene Grenze wird deshalb wie jede andere
  Eintragsänderung Id für Id verglichen (`MarkChangedGates` bis max(alt, neu)).
- Eine neu veröffentlichte Tor-Policy markiert genau die Knoten, deren Richtungsbyte sich
  geändert hat (`MarkChangedGatePolicy`). Diese Wörter lesen die Policy an ihrer eigenen
  Kachel.

**A/B-Spieltest:** Testmod-Szenario `wall-5000-gate`, je 6 Läufe.

| | 0.1.9 vorher | mit Behebung |
|---|---|---|
| Blockierte Testzeit | 6,1–22,5 s | 1,2–4,1 s |
| `AnchorTick` gesamt | 1,38–1,86 s | 0,62–0,67 s (Niveau ohne Torhaus) |
| Abgelehnte Routen, Fallbacks | 0 | 0 |

Die verbleibenden Sekunden entsprechen der Grundlast von 5000 Einheiten bei Speed 250;
auch Kontrollläufe ohne Torhaus erreichen sie. Kompakte Daten liegen unter
`.work/t/pf-resweep/001/results.txt`.

**Mehrspieler und Policy-Zeitpunkt (Einzelspieler-Prüfung):** Die Policy wird über
`ProcessGatePolicies(tick)` im Simulationstick veröffentlicht. In 9 Diagnoseläufen geschah
das immer auf dem Sim-Thread und nie verschoben. Eine Verschiebung ist nur möglich, wenn alle
Masken-Slots noch Leser haben; dieselbe Lücke betrifft die Live-Policy schon ohne Fix 0b.

**Große Torhäuser (Fix 3):** Im Stress-Save `test_stresstest5.sav` lösten Belagerungen weiterhin
4–6 kartenweite Resweeps je ~10 Minuten aus (3,3–4,7 s Sim-CPU). Ursache:
`GameBuildingFootprint.TryGetBounds` akzeptiert höchstens 6×6 Kacheln, weil die
Extender-Struktur nur 36 belegte Kacheln speichert. Ein großes Torhaus (7×7) scheiterte deshalb
bei jeder Änderung seines Portaleintrags. `TryGetGateFootprint` nimmt dann das Quadrat ab
`r_TilePositionX/YBegin` mit `r_OccupyTileGridSize` (Baseline `GATEHOUSE_QUERY_PASS.md`). Bei
5×5-Torhäusern stimmt dieses Quadrat im Spiel mit der Kachelliste überein. Mit großem
Test-Torhaus (`wall-5000-gatelarge`) wurde die Fläche lokal markiert, ohne Sweep.

**Offen:** Die Quelle der zusätzlichen globalen Resweeps (`global=216`) aus zwei früheren
Läufen trat in 9 Diagnoseläufen nicht wieder auf. Die DevDiag-Messpunkte (`pf-global`,
`pf-resweep`) zeigen sie beim nächsten Auftreten.
