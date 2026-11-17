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
## Ruhigere Rasterwege in 0.1.11 (17.11.2026)

Der im laufenden Spiel gelesene Steinbruchweg enthielt einen Abschnitt von
(316,364) nach (361,342): 45 Schritte und 44 Richtungswechsel. Das bisherige
Bresenham-Raster verteilte die beiden benachbarten Laufrichtungen fast abwechselnd.
Diagonale Bewegung war vorhanden. Bei beliebigen Zielwinkeln bleiben Korrekturen
auf dem Acht-Richtungs-Raster erforderlich; exakt passende Diagonalen brauchen sie nicht.

Der gemeinsame Quiet-Rasterlauf behält eine Richtung innerhalb des bisherigen
Kreuzproduktbands `abs(cross) <= Chebyshev(Start,Ziel)` länger bei. Schrittzahl,
Richtungsanzahlen und Endpunkt bleiben erhalten. Die Lauflängen werden ganzzahlig
berechnet; die exakte Reziprokenrechnung ist gegen normale Division geprüft.
Der öffentliche Direktpuffer wird erst nach vollständiger Prüfung beschrieben.
Scheitert die ruhigere Linie, bleibt das bisherige Raster verfügbar.

Für die Glättung bleibt die bisherige Bresenham-Auswahl der Abschnittsenden erhalten.
Nur die Ausgabe in den vorhandenen privaten Puffer versucht die ruhigere Linie,
höchstens zu den tatsächlich validierten Bresenham-Kosten. Ein Fehlversuch wird
zurückgesetzt, bevor der gültige bisherige Abschnitt ausgegeben wird. Das vermeidet
zusätzliche Validierungsdurchläufe und die im ersten Kandidaten nachgewiesenen
Mehrwege von ein bis zwei Feldern bei einigen Gruppenabfragen.

Wiederholte seitliche Zickzacks werden nur mit erlaubten, nicht teureren Ersatzkanten
vereinfacht. Bei geglätteten Wegen erfordert dies ein wiederholtes `a,b,a`-Muster.
Eine isolierte große Hindernisecke bleibt erhalten: Eine ursprünglich breitere
Paarregel hatte im vorhandenen Blocktest drei geometrische Abschnitte in vier
verwandelt. Der Fehler wurde reproduziert und ohne Lockerung des Tests behoben.

Einzelkantenprüfungen filtern nur noch die tatsächlich betretene Oberfläche statt
aller acht Ausgänge. Der vollständige Richtungsgraph der Suche bleibt unverändert.
Reiner Boden ohne Zuschläge erlaubt eine gleichwertige konstante Kostenzählung.
Alle Rasterversuche und lokalen Zusatzprüfungen bleiben im bisherigen deterministischen
Arbeitsrahmen; begonnene Raster können die Schwelle wie bisher linear überschreiten.
Es gibt keine zusätzlichen Suchfelder, Caches, Ausgabepuffer oder Laufzeithooks.

**Absicherung:** Der reguläre native Build besteht 3.343.566 Assertions. Dazu gehören
alle Richtungen und Steigungen, 45/22 mit höchstens 15 statt 44 Richtungswechseln,
wiederholte Brücken mit höchstens zwei statt 19 Wechseln, alter Raster-Fallback,
gewichtete Referenzkosten, Oberflächen- und Randrechte, kurze Puffer, alte Masken,
Arbeitsbudget und gleiche Ergebnisse mit 1/2/4/8 Workern. Die vorhandenen Hindernistests
behalten ihre Drei-Abschnitt-Grenze. Der vollständige Modbuild einschließlich
Managed-Integration, Veröffentlichungs-/Fallbackprüfungen, Tooltip- und Lifecycleprüfungen
ist erfolgreich. Öffentliche API, nativer Vertrag `0x04000012` und 32 Statistikfelder
bleiben unverändert. Die erneute eigene und unabhängige Abschlussprüfung fand keine
bestätigten verbleibenden Fehler.

**Spieltest:** `PathfindingSurfaceTest/ground-route-shape`, Lauf `fa19402099c6`,
besteht mit den installierten Begleitmods: neu erteilter Einzel- und Gruppenbefehl
(je eine Einheit), jeweils 45 tatsächlich veröffentlichte Schritte, 15 Richtungswechsel,
maximaler Kreuzproduktbetrag 44 und anschließende Ankunft am angeforderten Ziel.
Die erste Wiederholung scheiterte vor dem Gruppenbefehl an einer inzwischen belegten
Testfläche; deshalb wird nun vor jedem Fall eine frische freie Fläche gesucht.
Leerflächenprüfung und Ankunftsanforderung wurden nicht gelockert. Keine OS-Eingaben;
Pakete, Einstellungen und Extender-Cache wurden nach dem Test geprüft zurückgesetzt.

**Leistung:** Gegen 0.1.10 wurden identische Anfragen abwechselnd über die echten DLLs
verglichen: acht vollständige A/B-Paare und 32 Wiederholungspaare der sechs näher
untersuchten Gruppenfälle, einschließlich dynamischer Maskenänderungen. 1.216
unabhängige Dijkstra-Prüfungen, keine höheren tatsächlichen Wegkosten oder
Statusabweichungen, identische Worker-Ergebnisse und gezählte Spitzenspeicherstände.
Die kritischen Standardfälle bleiben etwa auf dem bisherigen Niveau; eingeschränkte
Oberflächenprofile benötigen ungefähr 55 % weniger CPU. Brückenfolgen und dichtes
Hindernisfeld kosten zusätzlich etwa 0,04 beziehungsweise 0,18 Mikrosekunden je Route.
Diese kleine Mehrarbeit wurde vom Nutzer am 17.11.2026 ausdrücklich akzeptiert.
Messwerte und Grenzen: [kompakte Auswertung](../../.work/a/pf-quiet/001/performance.md).
Das ist keine universelle Kostenfreiheits-, FPS- oder echte Host/Client-Abnahme.

Version 0.1.11 wurde regulär gebaut und mit 29 einzeln hashgeprüften Paketdateien
installiert. Das vorhandene APIShared-SDK und der Bootstrap bleiben hashgleich.
Die native Paket-DLL hat denselben Maschinencode wie die gemessene Variante.
Tooltips und Changelog erklären Nutzen und Grenzen für Spieler. Bereits laufende
oder geladene Wege werden erst durch eine neue Wegberechnung geändert.

## Engere Bodenwege in 0.1.12 (17.11.2026)

Im Spielstand `test_diagonal.sav` waren Diagonalen vorhanden. Gleich teure
Rasterwege und die bisherige Glättung konnten um die Steinbruchgebäude jedoch
weit von der direkten Verbindung abweichen. Das kann Einheiten unerwartet näher
an feindliche Burgen führen. Eine eigene Gefahrenbewertung ist weiterhin nicht
Bestandteil der Wegkosten; sie steht als optionale Untersuchung G13 in der TODO.

Nach der bisherigen Glättung werden höchstens zwei nicht überlappende Kandidaten
von jeweils höchstens 128 Schritten in ihrer Qualitätsrangfolge versucht. Nur
gleichmäßig bepreiste Bodenprofile ohne Grabenprofil und ohne aktive Kachelzuschläge
sind zugelassen. Die Suche bleibt in Bändern von 4/8/12 Kacheln um die Verbindung.
Gerichtete Kanten, Oberflächenrechte und die bestehende Live-Publikation bleiben
maßgeblich. Bodenprofil bedeutet trocken, nicht pauschal ohne erhöhte Flächen:
Fußgänger behalten ihre erlaubten Keep-, Lagerplatz- und Treppenwege.

Die eingefrorene gesamte Ausgangsroute erlaubt höchstens abgerundet 5 % mehr
Schritte und Kosten, zusätzlich begrenzt durch den echten Ausgabepuffer und
2.000 Schritte. Das ersetzte Fenster muss geometrisch mindestens 5 % kürzer sein,
bei zusätzlichen Schritten mindestens 10 %. Maximale und mittlere lokale
Linienabweichung müssen um mindestens 25 % sinken; die globale maximale Abweichung
darf nicht steigen. Pro Berechnung wird höchstens eine Ersetzung übernommen.
Präfix und Suffix stammen unverändert aus der Ausgangsroute. Erst die vollständige
Validierung der zusammengesetzten Route erlaubt die Übernahme; danach endet die
optionale Suche. Bei Budget- oder Speicherabbruch davor bleibt die Ausgangsroute.

Die vollständigen Farmerwege deckten einen Fehler der ersten Variante auf:
Nach der Auswahl wurden Fenster wieder nach Wegposition sortiert. Ein kleiner,
früher 39-Schritt-Bogen verbrauchte 6.881 von 8.192 Arbeitseinheiten ohne brauchbare
Verbesserung. Das wichtige spätere 128-Schritt-L kam nicht mehr zum Zug. Allein
findet es bereits mit dem schmalsten Band nach 3.051 Einheiten eine Verbesserung.
Die echte Rangfolge und die Rückkehr nach dem ersten vollständig geprüften Erfolg
verhindern diesen Budgetverlust, ohne das Arbeitslimit oder den Speicher zu erhöhen.

Zusätzliche Vorprüfungen vermeiden aussichtslose Suchen: eine Octile-Untergrenze
für das geometrische Verbesserungsziel und eine vollständig gesperrte gerichtete
Schnittspalte im größten Band. Eine einzelne erlaubte Lücke widerlegt die zweite
Prüfung bereits. Heapoperationen werden budgetiert; ihre interne Arbeit ist durch
die feste Zustandsgrenze ebenfalls beschränkt. Der temporäre Speicher wird sofort
freigegeben; es entsteht kein weiterer dauerhafter Suchcache.

**Absicherung:** 3.402.505 native Assertions einschließlich des eingefrorenen
Spielgraphs, tatsächlicher alter API-Ergebnisse und der vollständigen 269-/273-
Schritt-Anfragen. Geprüft sind Rangfolge, lokale Qualitätsgrenzen, unveränderte
Präfixe/Suffixe, globale Grenzen, Kostenmetadaten bei erlaubten Zusatzschritten,
kurze Puffer, Speicherabbrüche, gerichtete Schnittbeweise, Oberflächen und
Worker-Gleichheit. Die eigene und unabhängige erneute Abschlussprüfung des
Kerns und seiner Publikationskette fand keine bestätigten verbleibenden Fehler.
Der reguläre Modbuild einschließlich Managed-Integration besteht ebenfalls.
API, ABI `0x04000012`, 32 Statistikfelder, Suchgewichte und Gruppenanker bleiben
unverändert. `StraightenSaved` bezeichnet die vorzeichenbehaftete Nettokosteneinsparung
und darf bei einer erlaubten Qualitätskorrektur negativ sein.

**Leistung:** Acht vollständige abwechselnde A/B-Paare und 32 zusätzliche ruhige
Vergleichspaare der kritischen Fälle vergleichen die echten DLLs mit 0.1.11.
4.147.712 Anfragen bestehen die Status-, Kosten-, Schritt-, Grenzen- und
Workerprüfungen. In diesem Messdatensatz gibt es keine zusätzlichen Wegschritte.
Der 89-Schritt-Weg sinkt von 27,99 auf 2,46 Kacheln maximaler Linienabweichung,
der 201-Schritt-Weg von 54,75 auf 7,52. Die vollständigen Farmerwege behalten
269/273 Schritte und werden geometrisch um 12,55 % beziehungsweise 12,32 % kürzer.
Ihre globale Maximalabweichung bleibt wegen des Burgausgangs unverändert; sie ist
nicht mit dem verbesserten lokalen Abschnitt nach dem Ausgang zu verwechseln.

Warme verbesserte Gruppenwege benötigen mit einem Worker zusätzlich etwa 21–32 µs
je Route. Vollständige Einzelwegsuchen liegen in den beiden Fällen bei etwa
714→735 µs und 698→744 µs. Erfolglose breite Blockaden kosten etwa 4,2 µs zusätzlich,
ohne Allokation oder Budgeterschöpfung. Der zusätzlich gezählte Spitzenspeicher
beträgt 64.148 Byte je aktiver Workerbank, bei vier Workern 256.592 Byte.

Die Mehrarbeit bleibt bei großen gleichzeitigen Gruppen sichtbar: 768 erfolgreich
verbesserte vollständige Wege ergeben im Stressfall rund 34 ms mit einem Worker
beziehungsweise 12–13 ms mit vier Workern (gemittelte Maxima von jeweils acht
Batchproben). Höchste beobachtete Einzelbatches: rund 41 ms beziehungsweise 15 ms.
Das ist keine globale p95- oder FPS-Zusage. Verfahren, Streuung und Rohbelege:
[abschließende Auswertung](../../.work/a/pf-corridor/001/performance-v5.txt).
Die gemessene Kandidaten-DLL ist bytegenau gleich der regulären Paket-DLL.

Tooltips und Changelog erklären Nutzen, Grenzen und höchstens 5 % zusätzliche
Schritte. Bereits geladene oder laufende Wege ändern sich erst bei einer neuen
Berechnung. Das vorhandene SDK wurde als hashgleicher, geprüfter Stand getrennt
bereitgestellt, weil parallel bearbeitete Workspace-Ausgaben dessen alte
Paketnachweise nicht mehr erfüllten; keine APIShared-Abhängigkeit wurde aktualisiert.

**Spielabnahme:** `PathfindingSurfaceTest/natural-route-corridor`, Lauf
`f37c6846567b`, besteht mit den installierten Begleitmods. Eine normal platzierte
Milchfarm erhielt ohne manuelle Job-, Zustands- oder Bewegungszuweisung einen
neu eingestellten Farmer. Seine frische, beschleunigt veröffentlichte Route
(307,324)→(521,363) hat 273 Schritte; der vollständige monotone Abschnitt nach
dem Burgausgang (298,362)→(521,363) erreicht maximal 11,206 Kacheln Abweichung.
Pre-/Post-Identität, vollständige gerichtete Kanten, tatsächliches Default/Any-
Profil, gültige trockene Kacheln und der angeforderte/eigene Endpunkt sind geprüft.
Die Einheit hatte bereits eine globale ID als anderer Beruf; `birthNewGlobal=False`
wird ausdrücklich nicht als neue Geburt ausgegeben.

Die anschließenden neuen Einzel-/Gruppenbefehle liefern 89/201 Schritte mit
maximaler Abweichung 2,461/7,498 und erreichen exakt (444,368)/(500,367).
Die 201er Startkachel wird bei vorübergehendem Verkehr freigegeben abgewartet.
Der natürliche Farmerfall läuft unter den ursprünglichen Armeen mit einem
temporären öffentlichen Spielerpaar-Waffenstillstand aus `AttackTargetRules`.
Erst danach werden für störungsfreie Ankunftstests militärische Einheiten und
Kriegsmaschinen über `DeleteUnitSafe` entfernt; acht Lords und Zivilisten bleiben
erhalten, eigene Testakteure sind ausgenommen. Es gibt keine HP-Erhöhung,
Schadensabbruch-Hooks oder manuellen Farmerbefehle. Das beweist Wegpublikation
und -ausführung, keine allgemeine Kampfsicherheit oder Multiplayer-Abnahme.

Frühe Testläufe deckten zwei falsche Testannahmen auf: gespeicherte Previous-
Koordinaten sind während eines Schrittes nicht der neue Weganfang (siehe nativen
Vertrag in FIRE_EFFECTS.md und MOAT_TRAVERSAL_ALL_UNITS.md), und Fußgänger dürfen
zulässige erhöhte Flächen benutzen. Beides wurde ohne Lockerung von Kanten,
Kartenrändern, gültigen Kacheln oder Endpunkten korrigiert. Die verbleibende
Budgetverdrängung der großen Biegung war dagegen ein echter Fehler der neuen
Korrektur und wurde zusätzlich im nativen Regressionstest abgesichert.

Der Freiflächen-Spieltest `a4d984a22d65` besteht für Einzel- und Gruppenbefehl
mit je 45 Schritten, 15 Richtungswechseln und exakter Ankunft. V5 verändert diesen
Direktstrahlpfad nicht; die native Suite prüft ihn erneut in allen Richtungen.
Kein Test verwendet OS-Eingaben oder schreibt den Spielstand. Der Runner bestätigt
die Wiederherstellung von Paketen, Einstellungen und Extender-Cache.

**Installation:** Version 0.1.12 ist mit 29 einzeln hashgeprüften Paketdateien
installiert. Die native DLL stimmt bytegenau mit der gemessenen und im letzten
Spieltest verwendeten Variante überein. APIShared und Bootstrap bleiben hashgleich.
Nachweise: `.work/a/pf-corridor/001/installation.json`, `build-v5.log`,
`delivery-code-comparison-v5.json` und der oben genannte Ingame-Lauf.
