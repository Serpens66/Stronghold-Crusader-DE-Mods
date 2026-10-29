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
| pf-iter-18 | 9 | 6 | 1 | 206 ms | – |

Ab pf-iter-15 verhalten sich die KI-Lords anders (34.000 statt 29.000 Ticks, 51–94 statt 16 Torwechsel je Lauf), seit mehr Einheiten Anker-Routen bekommen; die Läufe sind deshalb nur eingeschränkt mit den früheren vergleichbar.

Verbleibende Spitzen (pf-iter-18, je Lauf 6 Lücken von 105–206 ms):
- **Grabenarbeits-Auswahl der KI-Gräber** (`MoatWorkSelectionScope`): 42 verwaltete Erreichbarkeitssuchen je Lauf, im Mittel 390.000 Zellen und 27 ms, höchstens 62 ms; mehrere Gräber im selben Tick ergeben bis zu 206 ms.
- **Vanillas Builder außerhalb von Anker-Gruppenbefehlen** (Einzel- und Angriffsbefehle): 143–168 ms.
- **Gewichtete Assassinen-Suche** (BFQoL `TryBuildWeightedRoute`): 105–115 ms.
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

## Regeln (gemeinsam für alle Suchen)

- Gewichtete Kosten statt reiner Schrittzahl: Graben, Klettern und Diagonalen nach Vanillas Bewegungszeiten, wie beim Assassinen-Kostenmodell.
- Exakte Antworten oder ein beweisbares „unerreichbar“. Ein Budgetabbruch fällt auf Vanilla zurück und wird nie als „unerreichbar“ gewertet.
- Jede veröffentlichte Route wird einmal live geprüft. Deterministisch für Mehrspieler, nie abhängig vom lokalen Spieler.
- Gemeinsame Masken und Felder statt Suchen je Einheit; Invalidierung kachelgenau.
