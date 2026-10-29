# Pathfinding-Fahrplan der Testlinie `dev/pathfinding`

Stand 29.10.2026. Ziel des Nutzers: alle Wegfindungen der Mods schneller und mit besseren Regeln, wie bei der gewichteten Assassinen-Suche. Umbauten und weitere Analysen sind ausdrücklich erwünscht. BFQoL-Änderungen bleiben auf diesem Branch, bis der Nutzer sie annimmt. Details zur Umsetzung stehen in `Testmods/MoatMove/ANCHOR_MODE_PLAN.md` und `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`.

## Erreicht (Momentaufnahme 29.10.2026, Messläufe 1–3)

- Gruppenbewegungen mit bis zu etwa 2500 Einheiten über den Anker-Kern: Stillstand je Befehl 140–170 ms statt 500–650 ms in Vanilla. Gewichtete Grabenentscheidung je Gruppe; Nicht-Gräber über ein eigenes Bodenfeld.
- KI-Spitzen von 350–1050 ms und der Ausreißer von 4,5 s sind behoben. Im dritten Lauf kam durch die kachelgenaue Markierung ein neuer Engpass hinzu; die Korrektur ist noch nicht im Spiel geprüft.

## Automatisierte Stressläufe (Momentaufnahme 29.10.2026)

Save `test_stresstest4.sav`, 180 s Lauf nach Warmup, `tools/Run-PathfindingIteration.ps1` und `tools/Analyze-PathfindingRun.ps1` (Lücken = Simulationstick > 100 ms nach dem Kartenstart, ohne Pausen). Logs unter `Testmods/MoatMove/.local/runs/`.

| Lauf | Lücken | davon ≥ 200 ms | Maximum | Behobene Hauptursache für den nächsten Lauf |
|---|---|---|---|---|
| pf-base-1 | 16 | – | 647 ms | – |
| pf-iter-2 | 51 | 12 | 533 ms | kachelgenaue Markierung, gebündelte Abfragen, Grundmaske |
| pf-iter-3 | 41 | 8 | 510 ms | Erreichbarkeitswörter, begrenzter Refresh |
| pf-iter-4 | 19 | 4 | 628 ms (Ladetick) | Closure je Aufruf in `ReachabilityWords`/`AnchorTraversal` (GC 24 %), erschöpfte Unteranker beweisen Unerreichbarkeit |
| pf-iter-5 | 10 | 4 | 521 ms (Ladetick) | Unteranker dünn gespeichert statt Vollkarten-Kopie (Abfragezeit 4,1 s → 0,24 s) |
| pf-iter-6 | 10 | 5 | 478 ms (Ladetick) | nur Diagnose |

Die größte Lücke liegt seit pf-iter-4 im ersten Tick nach dem Laden (Erstinitialisierung aller Mods, ohne Wegfindungs-Samples); sie ist ein Kartenstart-Thema.

Befund pf-iter-6: Die verbleibenden Spitzen von 285–410 ms bestehen aus Vanillas Builder (`F4930` → `DA590`) und Hauptfeldern (`mma_step`). Alle 4.974 Einheiten, die der Kern als unerreichbar meldete, bekamen von Vanilla eine Route zum selben Ziel. Start oder Zielplatz hatten dabei keine Kante in der Ankermaske (Wort `0xFF000000`): Bergfried/Lagerplatz, Türme, Mauern. `DA590` sucht nur über die Richtungsbits von `0x51890D0`; das BFQoL-Prädikat prüft zusätzlich Höhe, Torrichtlinie und Grabenbesitz. Die Ursache je Richtung wird in pf-iter-7 gemessen (`anchor-unreachable-found`).

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
