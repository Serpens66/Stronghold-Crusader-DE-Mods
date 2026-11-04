# Assassinen-Wegsuche: Lastanalyse und Optimierungsplan

Stand 29.10.2026, Testlinie `dev/pathfinding`. Reine Analyse, keine Codeänderung. Native-Hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installierte DLL, mit `CURRENT.json` abgeglichen). Vanilla-Grundlagen: Native-Baseline `sem/FBCB9319/knowledge/ASSASSIN_WEIGHTED_SEARCH_COST.md`.

## Befund aus dem Spiel

- `test_stresstest4.sav`, 14:14:31 (`Testmods/MoatMove/.local/ingame-breakdown-2.log` im Hauptordner): eine Lücke von 1187 ms direkt nach einem über Spectator erzwungenen KI-Angriff (`TEST_ACTION kind=Attack actor=6 target=2 force=True`).
- Stichproben der Lücke (1171): Vanillas Builder `0xD9C40` 446 (38 %). BFQoL `AssassinPathfindingRuntime`: `FindAnyWeightedRoute` 116, `SiftDown` 103, `ComesBefore` 81, `IsValidCoordinate` 39, `KeyOf` 28, also mindestens 31 %. MoatMoves Anker-Kern `mma_step` 36 (3 %).
- Das Muster deckt sich mit dem Baseline-Befund vom 23.10.: Ein Angriffsbefehl erzeugt in kurzer Zeit viele Zielanfragen derselben KI-Gruppe von derselben Startkachel.

## Ablauf je Anfrage (BFQoL `BuildWeightedPathCore`)

1. `AssassinPathAPI.RunVanillaBuilder` führt Vanillas `0xD9C40` immer genau einmal aus. Das verlangt der APIShared-Vertrag (`RegisterWeightedBuilder`): Aufrufer lesen Vanillas Feld bzw. die Generation `context+4`. Diese Kosten entstehen auch in reinem Vanilla.
2. Für Existenzanfragen (Gruppen-/Feldaufrufer mit vollem Budget) folgt `FindAnyWeightedRoute`: eine gierige Bestensuche über dieselben Kanten wie A*. Davor steht die Rückwärtsprüfung `ProvesTargetUnreachable` (512 Kacheln).
3. Für exakte Anfragen folgt das gewichtete A* `TryBuildWeightedRoute` mit derselben Rückwärtsprüfung. Routen werden im Befehls-Cache abgelegt (`MaximumCachedRoutes` 64, Suffix-Kosten je Ziel).
4. Die Caches gelten je Befehl (`AssassinCommandScope`). Viele aufeinanderfolgende Befehle mit derselben Startkachel rechnen deshalb jeweils neu.

## Ursachen der Mod-Kosten

- **Keine Wiederverwendung über Befehle hinweg:** Gleiche Startkachel, gleicher Spieler, gleiche Bewegungsgeschwindigkeit und gleiche Regeln (Klettern, reservierte Endpunkte, Torrichtlinie). Trotzdem rechnet jede Zielanfrage eine eigene Suche; erreichbare Ziele kosten nach der Baseline je etwa 13.000–43.000 Knoten und 10 ms.
- **Die Heap-Kosten dominieren:** Vierer-Heap mit gepacktem Schlüssel (`SiftDown`, `ComesBefore`, `KeyOf`).
- **Unerreichbare Ziele** über 512 Rückwärtskacheln laufen in die volle Komponentensuche (etwa 243.000 Knoten, 70 ms).

## Vorschläge (Reihenfolge nach Nutzen und Risiko)

1. **Startfeld je Tick (nur BFQoL, kein nativer Eingriff).** Für Anfragen derselben Startkachel und derselben Regeln innerhalb eines Ticks wird einmal ein exaktes gewichtetes Feld vom Start berechnet, mit denselben Kanten und Kosten wie A* (Dijkstra mit Abbruch erst bei Erschöpfung oder Budget). Existenzanfragen und exakte Routen werden daraus abgelesen.
   - Gültigkeitsschlüssel: Start, Spieler, Geschwindigkeit, Klettern, reservierte Endpunkte, Torrichtlinien-Schnappschuss (`AssassinGateRoutePolicy.IsCurrent`), Direktklettern über Torhäuser, Tick, Kartenepoche und Platzierungsrevision.
   - Kosten: ein Feld (bis etwa 243.000 Knoten, etwa 70 ms) statt N Suchen. Im Burst mit etwa 40 erreichbaren und 15 unerreichbaren Zielen sind das grob 70 statt 1450 ms Mod-Anteil.
   - Exaktheit: gleiche Kosten wie A*, bei Gleichstand ist ein anderer, gleich teurer Weg möglich. Deterministisch und unabhängig vom lokalen Spieler, also mehrspielertauglich.
   - Offline prüfbar mit `tests/AssassinPathfinding.Tests` (`ExactRouteTests` als Orakel, `ProductionPerformanceTests` als Last).
2. **Rückwärtsmengen je Ziel wiederverwenden:** Ein im Tick schon bewiesenes unerreichbares Ziel (gleiche Regeln) braucht keine zweite Rückwärtssuche.
3. **Vanillas `0xD9C40` je Anfrage einsparen:** nur nach einem vollständigen featurebezogenen Vanilla-Audit aller Aufrufer (`0xE2CA0`, `0xF4930`, `0x11B520`, `0x123090`) und ihrer Leser des Feldes. Außerdem bräuchte es eine APIShared-Vertragsänderung, weil der Builder heute genau einmal laufen muss. Zurzeit kein Vorschlag, nur ein offener Prüfpunkt.

## Offline-Testplan für Vorschlag 1

- Orakel: Für zufällige Gitter mit Klettern, Toren und reservierten Endpunkten müssen Feldantworten in Erreichbarkeit und Kosten exakt mit `TryBuildWeightedRoute` übereinstimmen. Jede gelieferte Route wird kantenweise gegen dieselben Prädikate geprüft.
- Last: ein nachgestellter Burst (eine Startkachel, 100 Ziele, davon 15 unerreichbar) mit Zeitvergleich alt/neu und Zähler für Feld-Neuberechnungen.
- Ungültigkeit: Ein Torwechsel, eine Kachelrevision oder ein neuer Tick verwirft das Feld.
