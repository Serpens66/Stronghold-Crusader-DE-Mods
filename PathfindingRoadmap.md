# Pathfinding-Fahrplan der Testlinie `dev/pathfinding`

Stand 29.10.2026. Ziel des Nutzers: alle Wegfindungen der Mods schneller und mit besseren Regeln, wie bei der gewichteten Assassinen-Suche. Umbauten und weitere Analysen sind ausdrücklich erwünscht. BFQoL-Änderungen bleiben auf diesem Branch, bis der Nutzer sie annimmt. Details zur Umsetzung stehen in `Testmods/MoatMove/ANCHOR_MODE_PLAN.md` und `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`.

## Erreicht (Momentaufnahme 29.10.2026, Messläufe 1–3)

- Gruppenbewegungen mit bis zu etwa 2500 Einheiten über den Anker-Kern: Stillstand je Befehl 140–170 ms statt 500–650 ms in Vanilla. Gewichtete Grabenentscheidung je Gruppe; Nicht-Gräber über ein eigenes Bodenfeld.
- KI-Spitzen von 350–1050 ms und der Ausreißer von 4,5 s sind behoben. Im dritten Lauf kam durch die kachelgenaue Markierung ein neuer Engpass hinzu; die Korrektur ist noch nicht im Spiel geprüft.

## Automatisierte Stressläufe (Momentaufnahme 29.10.2026)

Save `test_stresstest4.sav`, 180 s Lauf nach Warmup, `tools/Run-PathfindingIteration.ps1` und `tools/Analyze-PathfindingRun.ps1` (Lücken = Simulationstick > 100 ms nach dem Kartenstart, ohne Pausen). Logs unter `Testmods/MoatMove/.local/runs/`.

| Lauf | Lücken | im Spiel (> 5 s nach Kartenstart) | davon ≥ 200 ms | größte im Spiel | Danach behobene Hauptursache |
|---|---|---|---|---|---|
| pf-base-1 | 16 | 12 | 9 | 647 ms | – |
| pf-iter-2 | 51 | 48 | 11 | 343 ms | kachelgenaue Markierung, gebündelte Abfragen, Grundmaske |
| pf-iter-3 | 41 | 38 | 7 | 333 ms | Erreichbarkeitswörter, begrenzter Refresh |
| pf-iter-4 | 19 | 16 | 3 | 325 ms | Closure je Aufruf in `ReachabilityWords`/`AnchorTraversal` (GC 24 %), erschöpfte Unteranker beweisen Unerreichbarkeit |
| pf-iter-5 | 10 | 7 | 3 | 347 ms | Unteranker dünn gespeichert statt Vollkarten-Kopie (Abfragezeit 4,1 s → 0,24 s) |
| pf-iter-6 | 10 | 7 | 3 | 410 ms | nur Diagnose (Vanilla nach „unerreichbar“) |
| pf-iter-7 | 9 | 6 | 3 | 332 ms | Boden-Profile mit `DA590`-Kanten statt Grabenmodus-Höhenregel; Gräber weichen auf die Bodenaufgabe aus |
| pf-iter-8 | 9 | 6 | 1 | 216 ms | Erreichbarkeitswörter aus der Ankermaske; Refresh ohne Sortierung |
| pf-iter-9 | 8 | 5 | 1 | 235 ms | nur Diagnose (Vanillas Route gegen die Maske) |
| pf-iter-10 | 7 | 4 | 1 | 218 ms | Wächter für native Richtungsbytes (unbemeldete Torwechsel) |
| pf-iter-11 | 10 | 7 | 0 | 152 ms | 3×3-Markierung je Bytewechsel; wortbasierte Erreichbarkeitssuche |
| pf-iter-12 | 5 | 2 | 0 | 119 ms | Hauptfeld-Cache im Kern (Maskenrevision) |
| pf-iter-13 | 7 | 4 | 0 | 116 ms | Diagnose der Vanilla-Rückfälle (`vanillaWhy`) |
| pf-iter-14 | 7 | 4 | 0 | 120 ms | Bodenkanten übernehmen die Eckregel des Audits (424 abgelehnte Veröffentlichungen je Lauf) |
| pf-iter-15 | 8 | 5 | 0 | 185 ms | Erreichbarkeitswörter bei Neuabtastung vergessen statt voll neu lesen |
| pf-iter-16 | 9 | 6 | 0 | 193 ms | Masken-Neuaufbau `DAA50` meldet nur Richtungsbytes (Byte-Wächter statt Neuabtastung) |
| pf-iter-17 | 9 | 6 | 0 | 189 ms | Diagnose der Erreichbarkeitssuchen je Aufrufer |
| pf-iter-18 | 9 | 6 | 1 | 206 ms | Diagnose der langsamen Erreichbarkeitssuchen (`reachability-slow`) |
| pf-iter-19 | 7 | 4 | 1 | 222 ms | Rückwärtsbeweis je Ziel (in pf-iter-20/21 schlechter: 307 bzw. 559 ms, zurückgenommen) |
| pf-iter-22b | 11 | 7 | 1 | 216 ms | Erschöpfte Suche beantwortet spätere Starts (222.272 Treffer); Lauf mit Videolast, 260 s |
| pf-iter-23 | 10 | 7 | 0 | 188 ms | nur Diagnose (BFQoL-DEVDIAG, Assassinen-Häufung) |
| pf-iter-24 | 10 | 8 | 0 | 185 ms | Hauptfelder als A* zur Startbox (Gruppen 2–3× schneller, gleiche Qualität) |
| pf-iter-25 | 8 | 5 | 1 | 244 ms | KI-Angriffsbefehle per PerfProbe (`Automation.Actions`), ab hier alle Läufe mit zwei Assassinen-Angriffen |
| pf-iter-26 | 10 | 7 | 1 | 225 ms | Messung der Builder-Aufrufe außerhalb von Gruppenbefehlen (`outsideBuilds`) |
| pf-iter-27 | 16 | 13 | 2 | 219 ms | Einzelwege über fortsetzbare Felder je Ziel (Option `AnchorSingleMoves`): 17,8 s statt 7,2 s Vanilla, verworfen |
| pf-iter-28 | 8 | 5 | 1 | 258 ms | Einzelwege als A* vom Ziel zum Start: 82 µs je Weg, gleichauf mit Vanilla |
| pf-iter-29 | 7 | 4 | 1 | 228 ms | Gleichstände im A* zuletzt eingefügt zuerst: 65 µs je Weg (Vanilla ~81 µs), 902 statt 1.377 Knoten |
| pf-fire-3 | 8 | 5 | 1 | 287 ms | Feuerregeln und Umplanen (Pech gezündet); Lücke nach dem Feuer: 189 Assassinen-Builder-Aufrufe in einem Fenster |
| pf-iter-30 | 8 | 5 | 1 | 226 ms | Assassinen-A* tiefer zuerst bei Gleichstand: weiter ~25.000 Knoten je Weg, zurückgenommen |
| pf-iter-31 | 13 | 10 | 1 | 217 ms | **10.000-Einheiten-Lauf:** 8 × 600 Soldaten per `Spawn` (bis 9.929 lebende Einheiten), Feuer und Angriffe; 18.409 statt ~35.000 Ticks (Vanillas Einheitenkosten). Größte Lücken: Assassinen-Schub (147 Aufrufe, 183 ms; etwa je zur Hälfte Vanillas Builder und BFQoLs A*), Vanillas Grabenmodus-Einzelwege nach dem Spawn (131 Aufrufe, 254 ms im Fenster) |
| pf-iter-32 | 7 | 5 | 0 | 135 ms | Assassinen-Einzelwege über den Bodenweg des Kerns, wo F4930 zuerst DA590 rechnet (Audit 30.10.): 1.912 Kernwege, Vanillas Assassinen-Builder 241 statt 632 Aufrufe; 10.000 Einheiten, 19.947 Ticks. Rest: BFQoLs gewichtete Klettersuche (135 ms), Gruppenfelder (129 ms) |
| pf-iter-33 | 6 | 4 | 0 | 127 ms | **Release-Builds** von BFQoL und MoatMove, gleiche 10.000-Einheiten-Last: 24.951 Ticks (DEVDIAG kostete ~20 %). Spitzen der Laufphase 121 ms (viele KI-Gruppenfelder in einem Tick, `mma_step`), 119 und 107 ms (Assassinen über Regionsgrenzen: Vanillas `D9C40` und BFQoLs gewichtete Klettersuche) |
| pf-iter-34 | 6 | 4 | 0 | 144 ms (Laufphase 118) | Dial-Buckets statt Vierer-Heap in BFQoLs gewichteter Assassinen-Suche: keine Assassinen-Suche mehr in den Lücken; 27.069 Ticks. Rest im Kern (Gruppenfelder, Gruppenabfragen, Einzelwege) |
| pf-iter-35 | 7 | 4 | 0 | 142 ms (Laufphase 142, 122, 107) | Einschluss-Beweis vor Einzelwegen (Vorwärtsflutung ab Start, 2.048 Knoten): Einzelwege 5,8 statt 8,9 s, 77 statt 187 Mio. Knoten. 142 ms: 88 Kernwege eines Gruppenbefehls von der Live-Prüfung abgelehnt (veralteter Übergang), danach 88 fehlschlagende Vanilla-Suchen (84 ms); 246 Graben- und 66.671 Bodenwege aus dem Kern |
| pf-iter-38 | 8 | 5 | 0 | 200 ms (Laufphase 125, 140, 200) | Erste 8 Abfragen eines KI-Gruppenbefehls als exakte Einzelwege, erst danach das Feld: 6.951 Einzelwege kosteten 1,8 s (≈ 260 µs, lange Angriffswege), Feldzeit nur 3,5 → 2,7 s; Angriffs-Burst 140 + 200 ms. Ohne Budget verworfen |
| pf-iter-39 | 6 | 3 | 0 | 158 ms (Laufphase 126, 158, 106) | Dieselben Einzelwege mit höchstens 4.096 A*-Knoten (`mma_single_route_limited`; darüber `OverLimit`, das Feld übernimmt): 5.725 Einzelwege in 0,24 s (41 µs), Feldzeit 3,0 s. Rest: Gruppenfelder (`mma_step`), 109 abgelehnte Kernwege mit 109 scheiternden Vanilla-Suchen (112 ms), 108 Sub-Anker in einem Fenster (118 ms) |
| pf-iter-40 | 4 | 1 | 0 | 115 ms (Laufphase 115) | Zusätzlich vor jedem neuen Sub-Anker derselbe Einzelweg mit Budget (Vertrag `0x0400000D`): Sub-Anker 6.014 → 1, Kern-Abfragezeit 923 → 629 ms. In der Laufphase nur noch eine Lücke über 100 ms (115 ms; zu ~45 % Gruppenfelder eines Ticks); die drei übrigen Lücken liegen in den ersten 200 Ticks nach dem Laden |
| pf-iter-41 | 5 | 2 | 0 | 120 ms (Laufphase 113, 120) | 32 statt 8 Einzelwege je Gruppenbefehl: Felder nur 1.488 → 1.434, Einzelwege +115 ms, Feldzeit −144 ms, also neutral; zurück auf 8. Die Lücke um Tick ~6.180 tritt in jedem Lauf auf (pf-iter-35: 6.200, 40: 6.175, 41: 6.181): ein KI-Burst mit 39 Feldern in einem Fenster (117 ms, einzelne bis 9,7 ms) |
| pf-diag-field(2) | – | – | – | DEVDIAG | Feldkosten gegen abgefragte Mitglieder: fast alle teuren Felder setzten ~234.000 Knoten ab (die ganze Komponente), auch mit Starts 20–30 Kacheln vom Anker. Ursache: Mitgliedsstarts ohne ausgehende Kante (Vorwärtskomponente 1 Knoten: 2.998 Starts, ≤ 8 Knoten: 40; alle übrigen > 512), deren Unerreichbarkeit das Feld durch Ausschöpfen beweisen musste |
| pf-iter-42 | 4 | 1 | 0 | 108 ms (Laufphase 108) | Startfilter vor jedem Gruppenfeld (Vertrag `0x0400000E`): Starts, deren Vorwärtskomponente sich in ≤ 16 Knoten ohne Anker schließt, gehen nicht ins Feld; ihre Abfragen beweist der Einzelweg exakt. 47.256 Starts gefiltert, Feldzeit 3,0 → 1,1 s, Feldknoten 89 → 36 Mio.; der Burst um Tick ~6.180 ist weg. Rest: ein Assassinen-Schub (Einzelwege, BFQoL-Klettersuche, Vanillas `D9C40`/`DA590`) mit 108 ms |
| pf-iter-43 | 3 | 0 | 0 | – (Laufphase keine) | Wiederholung von pf-iter-42 ohne Codeänderung: **keine Simulationslücke über 100 ms in der Laufphase.** Längster Simulationsaufruf 102 ms 17 s nach dem Kartenstart (Assassinen über Regionsgrenzen: Vanillas `D9C40` 34 ms, BFQoLs gewichtete Suche ~35 ms); das einzige Bild über 100 ms (140 ms) ist PerfProbes `Spawn` von 4.800 Einheiten. Assassinen-Builder über Regionsgrenzen 289 Aufrufe / 0,5 s, Einzelwege 105.856 / 5,5 s je Lauf |
| pf-iter-44 | 5 | 2 | 0 | 152 ms (Laufphase 107, 152) | Wiederholung: 107 ms bei Tick 6.838 (Kaskade: 85 von der Live-Prüfung abgelehnte Kernwege, 85 scheiternde Vanilla-Suchen, 72 ms), 152 ms bei Tick 29.169 (Assassinen: BFQoLs gewichtete Suche ~80 ms, Vanillas `D9C40` 23 ms) |
| pf-iter-45 | 4 | 1 | 0 | 129 ms (Laufphase 129) | Wiederholung: 129 ms bei Tick 6.793 (dieselbe Kaskade, 99 scheiternde Vanilla-Suchen, 89 ms) |
| pf-known-1..8 | – | – | – | DEVDIAG | Gescheiterte Bodensuchen wiederverwenden (`GroundFailureProofs`, Vanilla-Audit 30.10.2026, Nutzerentscheidung „Vanilla-Fehlschläge“). Die Läufe deckten drei Lücken der ersten Fassung auf: Gruppenmitglieder haben eigene Platzziele (Beweis gilt für jedes Ziel außerhalb der geschlossenen Menge), `+0x80` ist meist gesetzt (nur `DAAC0` entfällt), normale Einheiten nutzen `F3060` statt `F32B0`, und die Kaskade überquert Regionsgrenzen (`E2F60` einmal wie Vanilla). pf-known-8: 113 von 113 vorhergesagten Fehlschlägen stimmten mit Vanillas Ergebnis und Endzustand überein, 0 Abweichungen |
| pf-iter-46 | 3 | 0 | 0 | – (Laufphase keine) | Release mit `GroundFailureProofs` (b25bdf1d3): die Kaskade trat auf, 88 Mitglieder in zusammen 0,5 ms beantwortet (vorher ~0,85 ms je Mitglied, 72–89 ms je Kaskade) |
| pf-iter-47 | 4 | 1 | 0 | 134 ms (Laufphase 134) | Wiederholung ohne Kaskade; 134 ms bei Tick 13.214: Assassinen-Schub, 7 Builds über Regionsgrenzen mit 77,6 ms (bis 25,8 ms je Build, überwiegend BFQoLs gewichtete Klettersuche) |
| pf-iter-48 | 4 | 1 | 0 | – (Laufphase keine) | Wiederholung: die einzige Lücke nach der 5-s-Grenze lag bei Tick 6 (Lade-Aufholphase) |
| pf-native-1 | – | – | – | DEVDIAG | Native gewichtete Assassinen-Suche in BFQoL (`native/AssassinSearch.cpp`, Nutzerentscheidung „native Kletter-Suche“). Offline identisch mit der verwalteten Suche auf allen Fixture-Terrains und vier Zufallsterrains mit allen Kachelmerkmalen; fünf Kern-Mutanten fallen auf. Im Spiel wiederholt der DEVDIAG-Build jede native Suche verwaltet: alle gleich, 0 Abweichungen |
| pf-iter-49 | 3 | 0 | 0 | – (Laufphase keine) | Release mit nativer Assassinen-Suche (1d9b7b20a): Assassinen-Builds über Regionsgrenzen höchstens 11,2 ms je Aufruf (vorher 24–26 ms) |
| pf-iter-50 | 4 | 1 | 0 | – (spielbedingt keine) | Wiederholung; die Lücke von 112 ms fällt auf PerfProbes `Spawn` von 4.800 Einheiten unter der Engine-Sperre (Simulation nur 15 ms beschäftigt) |
| pf-iter-51 | 3 | 0 | 0 | – (Laufphase keine) | Wiederholung; Assassinen-Builds höchstens 11,5 ms |
| pf-iter-52 | 3 | 0 | 0 | – (Laufphase keine) | Wiederholung; Assassinen-Builds höchstens 14,1 ms |
| pf-iter-53 | 3 | 0 | 0 | – (Laufphase keine) | Wiederholung; Assassinen-Builds höchstens 11,3 ms. Fünf Release-Läufe in Folge (49–53) ohne spielbedingte Simulationslücke über 100 ms; übrig sind nur die Lücken der ersten ~100 Ticks nach dem Laden (erster Tick ~150 ms Simulationsarbeit, danach Lade-Aufholphase) und das Einzelbild von PerfProbes `Spawn` |

Ab pf-iter-15 verhalten sich die KI-Lords anders (34.000 statt 29.000 Ticks, 51–94 statt 16 Torwechsel je Lauf), seit mehr Einheiten Anker-Routen bekommen; die Läufe sind deshalb nur eingeschränkt mit den früheren vergleichbar.

Verbleibende Spitzen (pf-iter-18, je Lauf 6 Lücken von 105–206 ms):
- **Grabenarbeits-Auswahl der KI-Gräber** (`MoatWorkSelectionScope`): 42 verwaltete Erreichbarkeitssuchen je Lauf, im Mittel 390.000 Zellen und 27 ms, höchstens 62 ms; mehrere Gräber im selben Tick ergeben bis zu 206 ms.
- **Vanillas Builder außerhalb von Anker-Gruppenbefehlen** (Einzel- und Angriffsbefehle): 143–168 ms.
- **Gewichtete Assassinen-Suche** (BFQoL `TryBuildWeightedRoute`): 105–115 ms.
  - pf-iter-23 (BFQoL-DEVDIAG): Einzelanfragen höchstens 35 ms. Die Lücken entstehen durch Häufung (stärkstes 5-s-Fenster: 13 Anfragen/s, 404 ms; davon 9/s gewichtete A*-Treffer, für die Vanilla keine Route fand, je ~10 ms). Vor dem Startfeld je Tick ist zu messen, ob die Anfragen eines Ticks Startkacheln teilen.
- **Hauptfelder vieler Gruppenbefehle in einem Tick** (`mma_step`): rund 65 ms Anteil.

Die größte Lücke liegt seit pf-iter-4 im ersten Tick nach dem Laden (Erstinitialisierung aller Mods, ohne Wegfindungs-Samples); sie ist ein Kartenstart-Thema.

Befund pf-iter-6: Die verbleibenden Spitzen von 285–410 ms bestehen aus Vanillas Builder (`F4930` → `DA590`) und Hauptfeldern (`mma_step`). Alle 4.974 Einheiten, die der Kern als unerreichbar meldete, bekamen von Vanilla eine Route zum selben Ziel. Start oder Zielplatz hatten dabei keine Kante in der Ankermaske (Wort `0xFF000000`): Bergfried/Lagerplatz, Türme, Mauern. `DA590` sucht nur über die Richtungsbits von `0x51890D0`; das BFQoL-Prädikat prüft zusätzlich Höhe, Torrichtlinie und Grabenbesitz. Ergebnis pf-iter-7: In allen Stichproben lehnte die Höhenkorrektur des Grabenmodus (`DAFD0`) die Kante ab, obwohl das native Byte `0xFF` war. Seit pf-iter-8 nutzen Boden-Profile die Boden-Bits mit `DA590`-Regeln (Vanilla-Builder in Anker-Bewegungen 5.028 → 466). pf-iter-10: Die restlichen Fälle waren veraltete Wörter an Burgtoren, deren Richtungsbyte ohne Kachelmeldung wechselt; der Bytewächter senkte sie auf 39 je Lauf. Details zu `F4930`/`DAAC0`/`DA590` in der Native-Baseline (`GROUND_ROUTE_BUILDERS.md`).

## Kandidaten, nach Nutzen geordnet

1. **(Umgesetzt 29.10., ungeprüft im Spiel) Anker-Abfragen gebündelt und parallel.** Heute fragt jede Einheit einzeln und nacheinander an (etwa 14 µs je Einheit, 35–45 ms je Befehl mit 2500 Einheiten). BFQoL kennt bei Formationsbefehlen alle Platzzuweisungen schon vor `11B520`. Ein Formations-zu-Anker-Vertrag (Einheit → Platz) würde alle Routen im Pre gebündelt mit 8 Threads im Kern berechnen; die Einheiten lesen dann nur ab. Erwartung: 35–45 auf etwa 6–8 ms. Voraussetzung: BFQoL veröffentlicht die Platzliste, deterministisch und unabhängig vom lokalen Spieler.
2. **(Umgesetzt 29.10., ungeprüft im Spiel) Spielerunabhängige Gelände-Grundmaske.** Acht Spielermasken lesen heute dasselbe Gelände achtmal, mit 8–16 `TryGetTraversalEdge` je Knoten. Spielerabhängig sind nur Grabenbesitz, Torsperren und die Torrichtlinie. Eine gemeinsame Grundmaske mit spielerweiser Überlagerung an Gräben und Toren senkt Aufbau, Refresh und Resweep etwa auf ein Achtel.
3. **Einzelbefehle und KI-Mikrobefehle** (Kampffortsetzung, Angriffsanlauf, Arbeitswege) laufen noch über Vanillas Einzelsuche `F4930`/`DA590`. Deren Anteil war im KI-Lauf klein (0,9 %), bei menschlichen Massenbefehlen außerhalb von `11B520` ist er ungemessen. Möglich ist ein Zielfeld-Cache je Spieler und Ziel, solange die Maske unverändert ist, mit denselben gewichteten Regeln. Vorher braucht es den Vanilla-Audit der Aufrufkette `196280` → `F4930`.
4. **Assassinen:** Startfeld je Tick für gleiche Startkacheln (Plan in `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`). Erwartung im Angriffs-Burst: Mod-Anteil etwa 70 statt 1450 ms.
5. **Torrouting und Torhaus-Abfragen** (BFQoL `RouteTilePolicySnapshot.IsDirectionAllowed`, `ReachableEnemyGatehouseRuntime`, ExtraFeatures `GatehouseAutomationRuntime`) zusammen etwa 2 % der Sim-Zeit im KI-Lauf: auf doppelte Abfragen je Tick prüfen.
6. **Unbekannte heiße Simulationsfunktionen** `0x18C160` (14 %) und `0xF5A40` (6 %): vor jeder Maßnahme ein featurebezogener Vanilla-Audit, um festzustellen, ob sie Wegfindung betreffen.

## Regelschicht (Entscheidung des Nutzers 29.10.2026: Vertrag in APIShared, Engine in MoatMove)

Ziel: Mods definieren zusätzliche Wegfindungsregeln (Feuer ausweichen, Gefahrenzonen, gesperrte Bereiche, Kostenaufschläge), ohne eigene Suchen und ohne Lags. Vanilla bleibt unverändert, solange keine Engine installiert ist.

- **APIShared (additiv, `APIShared.Pathfinding`):** passive Kachelregeln nach dem Muster von `GateAccessRules`.
  - Registrierung je Besitzer-GUID und Registrierungs-ID; prozessweit.
  - Je Kachel und Spielermaske ein Effekt: `Forbidden` (nicht betreten, außer als Start oder Ziel) oder `Penalty` (Aufschlag 1–255 in Bodenschritt-Einheiten beim Betreten).
  - Änderungsjournal mit monotoner Version, damit Engines nur geänderte Kacheln neu lesen.
  - Vertrag: Aufrufe nur auf dem Simulationsthread, aus deterministischem Spielzustand. Mehrspieler-Gleichheit der Regel-Mods liegt beim Verbraucher (`NetworkMode=1`). Regeln schränken nur ein oder verteuern, sie erlauben nie eine Kante, die Vanilla verbietet. Ohne Engine haben sie keine Wirkung.
- **Engine (MoatMove-Anker):**
  - `Forbidden` entfernt die eingehenden Kanten einer Kachel aus den Masken; markiert werden die Kachel und ihre acht Nachbarn.
  - `Penalty` wird zur Kantenkostenbeigabe je Zielknoten im Kern (Byte-Feld je Spieler, Summe begrenzt auf `MaxCost`).
  - Jede veröffentlichte Route wird weiter live geprüft, die Regeln eingeschlossen.
  - Vanilla-Befehle und Vanillas eigene Suchen bleiben unverändert.
- **Beispielregel „Feuer ausweichen“:** Aufschlag auf brennende Gebäudegrundflächen ±1. Bekannte Bausteine: Brandzähler der Gebäude (`0xC4D30`, `0xC60F0`, `0xC3C80`, siehe `FIREMAN_TARGETING.md`). Vor der Umsetzung ist ein Vanilla-Audit des Brandzustands nötig; Pechgräben sind in der Baseline noch nicht belegt.

**Umsetzungsstand (29.10.2026, Testlinie):**
- APIShared (`pathfinding-pin`): `APIShared.Pathfinding.RouteTileRules`, `RouteTileRuleSet`, `RouteTileEffect`, mit Core-Test, API-Basis (nur Ergänzungen), Katalogabschnitt „Route tile rules“ und Beispiel `examples/ThirdPartyMod/RouteTileRulesExample.cs`.
- MoatMove:
  - `Forbidden` im Kantenprädikat (Masken, Erreichbarkeit, Live-Audit), Journal-Leser im Anker-Tick (Kachel und Nachbarn neu lesen, bei Verlust volle Neuabtastung).
  - `Penalty` als Eintrittskosten im Kern (Vertrag `0x04000009`, `mma_penalty_update`), geschrieben bei Journaländerungen und in jedem Masken-Abschnitt.
  - Tests mit Gegenproben: Wirkung nur für die gewählten Spieler, Verlassen bleibt möglich, Aufgaben werden veraltet, Umweg um bestrafte Kacheln, Batch gleich Einzelabfrage.
- Umplanen bereits laufender Einheiten (29.10.2026, Nutzervorgabe „Abstand 3, Umweg ≤ 50 %“):
  - APIShared: `RouteReplanPolicy` je Regelsatz (Auslöseabstand, erlaubter Umweg in Prozent und Mindestschritten, Wartezeit), `RouteTileRules.GetReplanPolicy`, `HasReplanRules`; additiv, mit Core-Test.
  - MoatMove `RouteReplanWatch` (nur mit `AnchorSingleMoves`):
    - Prüfung: je Tick jede 40. Einheiten-ID, für 8 Ticks nach neuen Regelkacheln jede 8., dazu eine heiße Liste für Einheiten mit Regelkachel weiter voraus. Je Prüfung höchstens 40 Wegschritte gegen eine Bitkarte; ohne Regelkacheln kehrt der Wächter sofort zurück.
    - Umweg: `196280` zum unveränderten Ziel (dieselbe Grundfunktion, die Vanillas Einheitenzustände nutzen; Befehl und Zustand bleiben). Der neue Weg gilt, wenn er erlaubt lang ist und in den nächsten 40 Schritten keine Regelkachel hat.
    - Sonst Halt auf der nächsten Kachel. Gruppenmitglieder (gleicher Stamm, Ziel in derselben 16×16-Zelle) übernehmen eine Warteentscheidung ohne eigene Suche. Alle 40 Ticks prüft der Kern, ob ein freier Weg existiert; nach der Wartezeit läuft die Einheit weiter. Ein neuer Befehl beendet das Warten.
  - Verbraucher `FireRouteRules` (Option `AvoidFire`): Kacheln mit Vanillas Feuer-Effekt (Typ 9 im Effektmanager, Baseline `FIRE_EFFECTS.md`; deckt Pech, Brandgeschosse und brennende Gebäude ab) als Aufschlag 254 für alle Spieler, Regel 3 / 50 % / 10 Schritte / 400 Ticks. `PitchState.OnFire` ist nur der Zündauslöser (pf-fire-2: nach 1,5 s verschwunden).
  - pf-fire-3 (Pech gezündet bei 15 s, dazu zwei Assassinen-Angriffe): bis zu 65 Feuerkacheln über etwa 13 s; 31 Auslösungen, 10 Umwege, 21 Halte (9 davon aus der Truppentscheidung übernommen), 3 Weiterläufe bei freiem Weg, 4 nach Ablauf, 13 durch neue KI-Befehle beendet. Kosten 7–23 ms je 1,5-s-Fenster, höchstens 2,2 ms in einem Tick. Der Restweg ab der nächsten Kachel endete in etwa 98 % von rund 100.000 Prüfungen beim Bewegungsziel.
  - Integrationstest gegen den echten Kern mit Gegenprobe (Umweg, Halt, Weiterlaufen, Ruhe ohne Regeln).
- Offen:
  - Wartende Einheiten werden nicht im Spielstand gespeichert: Nach dem Laden bleiben sie stehen, bis ein Befehl kommt (KI-Einheiten planen selbst neu).
  - Ausnahme „Ziel liegt auf verbotener Kachel“: heute unerreichbar, dann Vanilla.
  - Sichtprüfung durch den Nutzer: Halt und Weiterlaufen sehen im Spiel natürlich aus (kein Ruckeln beim Halt mitten im Schritt).

## Regeln (gemeinsam für alle Suchen)

- Gewichtete Kosten statt reiner Schrittzahl: Graben, Klettern und Diagonalen nach Vanillas Bewegungszeiten, wie beim Assassinen-Kostenmodell.
- Exakte Antworten oder ein beweisbares „unerreichbar“. Ein Budgetabbruch fällt auf Vanilla zurück und wird nie als „unerreichbar“ gewertet.
- Jede veröffentlichte Route wird einmal live geprüft. Deterministisch für Mehrspieler, nie abhängig vom lokalen Spieler.
- Gemeinsame Masken und Felder statt Suchen je Einheit; Invalidierung kachelgenau.

## Stand und Grenzen (30.10.2026, nach pf-iter-41)

- Last: Stress-Save mit 8 KI-Lords auf der 800er-Karte, per `Spawn` bis 9.938 Einheiten, Feuer und zwei Assassinen-Angriffe, Release-Builds. In der Laufphase höchstens noch eine Lücke knapp über 100 ms in 260 s (pf-iter-42: 108 ms, pf-iter-43: keine; pf-iter-40/41: 1–2 mit 113–120 ms; bis pf-iter-35 3–4 Lücken bis 142 ms); zu Beginn (pf-base-1, ~5.150 Einheiten) 12 Lücken bis 647 ms. Die Lücken der ersten 200 Ticks nach dem Laden sind Kartenstart (Aufholphase).
- Exakte Einzelwege mit Knotenbudget (seit 905f3df16): die ersten 8 Abfragen eines KI-Gruppenbefehls und jede Abfrage, die sonst einen Sub-Anker bauen würde, rechnet der Kern als A* mit höchstens 4.096 Knoten; darüber übernimmt das Feld bzw. der Sub-Anker. Gruppenfelder 2.402 → ~1.490 je Lauf, Sub-Anker 6.014 → 1.
- Verbleibende Ursachen, alle Häufungen in einem Tick:
  - Gruppenfelder (`mma_step`): seit dem Startfilter (pf-iter-42) 1,1 statt 3,0 s je Lauf; die ~9-ms-Felder, die für Starts auf Gebäude- oder Turmkacheln die ganze Komponente ausschöpften, entfallen. Unerreichbare Starts in großen Komponenten (> 16 Knoten) schöpfen weiter aus;
  - Assassinen-Schübe (Einzelwege des Kerns, BFQoLs gewichtete Klettersuche, Vanillas `D9C40`). BFQoL ruft `D9C40` vor der gewichteten Suche immer auf, weil `E1640`/`F4930` dessen Rückgabe und Warteschlangenzustand weiterverwenden; ihn einzusparen verlangt eine Nachbildung dieses Zustands (eigener Vanilla-Audit), für zuletzt 0,5 s je Lauf;
  - von der Live-Prüfung abgelehnte Kernwege eines Befehls zu einem tatsächlich unerreichbaren Ziel, danach Vanillas fehlschlagende Suchen. Diese ohne Vanilla zu beantworten, wäre nicht exakt (andere Starts könnten einen Weg haben).
- Weitere Hebel mit ungünstigem Aufwand-Nutzen: Gruppenfelder über Ticks verteilen (verzögert KI-Befehle, Verhaltensänderung), Vanilla-Fehlschläge zwischenspeichern (nicht exakt), Assassinen-Klettersuche nativ portieren (seit den Buckets nicht mehr in den Lücken).
- Regelkonstrukt: APIShared-Leitfaden `docs/PATHFINDING_RULES.md` (Kachelregeln mit Umplanen, Grabenkosten, Torzugang, Mauerklettern). Im Spiel geprüft: Feuer (pf-fire-3), Grabenquerung (246 Kernwege in pf-iter-35), gewichtete Kletterwege (BFQoL, alle Läufe), Kachelstrafen im Kern (Integrationstests).
- Sperren im Spiel (pf-rule-1, DEVDIAG-Probe `Diagnostics.RuleProbeTicks`): Band x 399–401 über die ganze Karte mit Lücke y 380–420, 2.277 gesperrte Kacheln in 10 ms gesetzt. 0 veröffentlichte Einzelwege betraten eine gesperrte Kachel, 1.511 querten das Band durch die Lücke. Schlechtester Fall: lange Umwege hinter einer kartenweiten Sperre ließen den Einzelweg-A* je Weg fast eine Kartenhälfte durchsuchen (553 Wege, 13,7 Mio. Knoten, 528 ms in einem Tick); Vanillas Breitensuche hat denselben schlechtesten Fall. Ein gemeinsames exaktes Zielfeld ab der dritten Anfrage eines Ziels (pf-rule-2) war deutlich schlechter: 1.859 Vollfelder für 4.586 Treffer, Einzelwege 17,2 statt 5,2 s, zurückgenommen. Ziele wiederholen sich je Maskenstand im Mittel nur ~2,5-mal.
- Routenkosten-Regeln im Kern (seit ea2d8dcd3 veröffentlichen die gewichteten Grabenfelder der Gruppenbefehle `RouteSearchEvents`): pf-cost-1, DEVDIAG-Probe `Diagnostics.MoatCostProbeTicks` (Grabenkanten 50-mal so teuer wie Bodenkanten ab Tick 2.000) – vorher 221 Grabenwege in 8 Fenstern (~28 je Fenster), danach 26 in 101 Fenstern (~0,26 je Fenster). Damit sind Grabenquerung, Feuer, Sperren und Routenkosten im Spiel belegt, Mauerklettern über BFQoLs gewichtete Assassinen-Suche.
- Seit b25bdf1d3 (pf-iter-46..48) ist die Kaskade behoben; in drei Läufen blieb eine Lücke über 100 ms (134 ms, Assassinen-Schub). Seit 1d9b7b20a rechnet ein nativer Kern BFQoLs gewichtete Klettersuche (identische Ergebnisse); pf-iter-49..53 (fünf Läufe): keine spielbedingte Lücke über 100 ms in der Laufphase. Offen bleibt der erste Tick nach dem Laden (~150 ms, Initialisierung aller Mods, ein Kartenstart-Thema).
- Wiederholungen mit dem Stand d0ac89b64 (pf-iter-42..45): 0–2 Lücken über 100 ms je 260 s, 107–152 ms. Zwei Ursachen, beide in jedem Lauf möglich:
  - **Kaskade bei Tick ~6.800** (jeder Lauf, auch pf-iter-35): ein KI-Gruppenbefehl zu einem soeben unerreichbar gewordenen Ziel; die Live-Prüfung lehnt die Kernwege ab, Vanillas `F4930` scheitert danach für jedes Mitglied (`F32B0` → `DAAC0` → `DA590`, je ~0,85 ms). Das sind Vanillas eigene Kosten. Exakt abkürzen ließe sich das nur, indem ein durch Erschöpfung gescheitertes `DA590` (weniger als 400.000 Entnahmen) für weitere Starts in seiner besuchten Komponente mit demselben Ziel wiederverwendet wird: `F32B0` und `DAAC0` laufen nur über Kanten von `DA590`s Richtungsbits (`0x51890D0`), scheitern dort also auch. Dafür müsste die Mod `F4930`s Fehlerpfad nachbilden (`+0x7C=0`, `+0xAC++`, `+0x155F68=0`, `79C0` nach `0x34A9F50`) und dürfte die Suchen samt Generation `+4` und Stempeln `0x52C2550` überspringen. Lese-Prüfung 30.10.2026: 18 Funktionen lesen die Stempel ohne eigene Generationserhöhung. `196280`s Fehlerzweig (`199CD0`) und `123460` lesen sie nicht, `123090` sucht vorher selbst. Offen sind die übrigen Leser und die 67 Aufrufer von `11B520`.
  - **Assassinen-Schübe:** BFQoLs gewichtete Suche (verwaltet, ~29.000 Knoten je Suche) und Vanillas `D9C40` im selben Tick. Spürbar schneller ginge es nur mit einer nativen Suche in BFQoL oder ohne Vanillas `D9C40`-Lauf (Zustandsnachbildung, siehe `ASSASSIN_PATHFINDING_PLAN.md` Punkt 3).
- Zurückhalten von KI-Gruppenbefehlen (Lese-Prüfung 30.10.2026, FBCB9319): `0x11B520` hat 67 Aufrufstellen in rund 35 Funktionen (KI-Planer, Befehlsverteiler `0x11E960`, Wegpunkte `0x11A980`); nur etwa 6 prüfen den Rückgabewert direkt. Der Aufruf schreibt zuerst den Stamm-Zustand (Ziel, Modus `+0x5DC`, Platzdaten) und ruft dann je Mitglied `0x196280`. Ein Verschieben des ganzen Befehls ließe die KI Ticks lang mit veraltetem Stamm-Zustand rechnen; ein Verschieben nur der Mitgliederwege verlangt eine Teil-Nachbildung von `11B520`. Beides ist weder exakt noch für alle Nutzer sicher; nicht umgesetzt. Die verbleibenden Spitzen (105–150 ms, 3–4 je 260 s bei 10.000 Einheiten) sind damit der mit vertretbarem Aufwand erreichbare Stand.
- Gebündelte Abfragen für KI-Gruppenbefehle aus den Platzzielen `r_AttackMoveToTargetTile` (pf-iter-36): 185.208 vorab berechnete Mitgliederwege, davon 15.299 abgefragt (8 %), Abfragezeit 8,8 statt 2,0 s, größte Lücke 181 ms; zurückgenommen. Vanilla plant in der zweiten Schleife von `11B520` nur einen kleinen Teil der Mitglieder neu.
- Dasselbe nur für Mitglieder, deren Platz vom aktuellen Ziel abweicht, die nicht laufen oder KI-Zustand 0x6A haben (pf-iter-37): 157.599 Vorabwege für 15.457 Treffer, größte Lücke 175 ms; zurückgenommen. Welche Mitglieder `11B520` tatsächlich neu plant, hängt an weiteren Zustandsprüfungen der Schleife und ist ohne deren Nachbildung nicht sicher vorhersagbar.
