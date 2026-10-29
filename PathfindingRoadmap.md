# Pathfinding-Fahrplan der Testlinie `dev/pathfinding`

Stand 29.10.2026. Ziel des Nutzers: alle Wegfindungen der Mods schneller und mit besseren Regeln, wie bei der gewichteten Assassinen-Suche. Umbauten und weitere Analysen sind ausdrücklich erwünscht. BFQoL-Änderungen bleiben auf diesem Branch, bis der Nutzer sie annimmt. Details zur Umsetzung stehen in `Testmods/MoatMove/ANCHOR_MODE_PLAN.md` und `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`.

## Erreicht (Momentaufnahme 29.10.2026, Messläufe 1–3)

- Gruppenbewegungen mit bis zu etwa 2500 Einheiten über den Anker-Kern: Stillstand je Befehl 140–170 ms statt 500–650 ms in Vanilla. Gewichtete Grabenentscheidung je Gruppe; Nicht-Gräber über ein eigenes Bodenfeld.
- KI-Spitzen von 350–1050 ms und der Ausreißer von 4,5 s sind behoben. Im dritten Lauf kam durch die kachelgenaue Markierung ein neuer Engpass hinzu; die Korrektur ist noch nicht im Spiel geprüft.

## Kandidaten, nach Nutzen geordnet

1. **Anker-Abfragen gebündelt und parallel.** Heute fragt jede Einheit einzeln und nacheinander an (etwa 14 µs je Einheit, 35–45 ms je Befehl mit 2500 Einheiten). BFQoL kennt bei Formationsbefehlen alle Platzzuweisungen schon vor `11B520`. Ein Formations-zu-Anker-Vertrag (Einheit → Platz) würde alle Routen im Pre gebündelt mit 8 Threads im Kern berechnen; die Einheiten lesen dann nur ab. Erwartung: 35–45 auf etwa 6–8 ms. Voraussetzung: BFQoL veröffentlicht die Platzliste, deterministisch und unabhängig vom lokalen Spieler.
2. **Spielerunabhängige Gelände-Grundmaske.** Acht Spielermasken lesen heute dasselbe Gelände achtmal, mit 8–16 `TryGetTraversalEdge` je Knoten. Spielerabhängig sind nur Grabenbesitz, Torsperren und die Torrichtlinie. Eine gemeinsame Grundmaske mit spielerweiser Überlagerung an Gräben und Toren senkt Aufbau, Refresh und Resweep etwa auf ein Achtel.
3. **Einzelbefehle und KI-Mikrobefehle** (Kampffortsetzung, Angriffsanlauf, Arbeitswege) laufen noch über Vanillas Einzelsuche `F4930`/`DA590`. Deren Anteil war im KI-Lauf klein (0,9 %), bei menschlichen Massenbefehlen außerhalb von `11B520` ist er ungemessen. Möglich ist ein Zielfeld-Cache je Spieler und Ziel, solange die Maske unverändert ist, mit denselben gewichteten Regeln. Vorher braucht es den Vanilla-Audit der Aufrufkette `196280` → `F4930`.
4. **Assassinen:** Startfeld je Tick für gleiche Startkacheln (Plan in `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`). Erwartung im Angriffs-Burst: Mod-Anteil etwa 70 statt 1450 ms.
5. **Torrouting und Torhaus-Abfragen** (BFQoL `RouteTilePolicySnapshot.IsDirectionAllowed`, `ReachableEnemyGatehouseRuntime`, ExtraFeatures `GatehouseAutomationRuntime`) zusammen etwa 2 % der Sim-Zeit im KI-Lauf: auf doppelte Abfragen je Tick prüfen.
6. **Unbekannte heiße Simulationsfunktionen** `0x18C160` (14 %) und `0xF5A40` (6 %): vor jeder Maßnahme ein featurebezogener Vanilla-Audit, um festzustellen, ob sie Wegfindung betreffen.

## Regeln (gemeinsam für alle Suchen)

- Gewichtete Kosten statt reiner Schrittzahl: Graben, Klettern und Diagonalen nach Vanillas Bewegungszeiten, wie beim Assassinen-Kostenmodell.
- Exakte Antworten oder ein beweisbares „unerreichbar“. Ein Budgetabbruch fällt auf Vanilla zurück und wird nie als „unerreichbar“ gewertet.
- Jede veröffentlichte Route wird einmal live geprüft. Deterministisch für Mehrspieler, nie abhängig vom lokalen Spieler.
- Gemeinsame Masken und Felder statt Suchen je Einheit; Invalidierung kachelgenau.
