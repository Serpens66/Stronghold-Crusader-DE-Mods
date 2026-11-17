# EnemyBridgePathTest – Übergabe

Stand: 17.11.2026. **Das Feature gehört seit Pathfinding 0.1.10 zu Pathfinding**
(Einstellung `EnableEnemyDrawbridgeClosure`, „Feinde von abgesenkten Zugbrücken vor Torhäusern
fernhalten“; Code `Pathfinding/src/GateRouting/DrawbridgeDeckClosure.cs` und die
Deck-Kantenmaske in `GateTopologySnapshotProvider.cs`; native Verträge in
`Pathfinding/UpdateToNewDLL.md`). Dieser Testmod enthält nur noch die automatischen
Spielabnahmen und einen rein lesenden Deck-Inspektor (`src/DeckInspector.cs`, eigene
unabhängige Geometrie in `src/DeckClosureCore.cs`). Er installiert keine Hooks und ändert
keinen Spielspeicher. Die Geometrie-Unit-Tests liegen in `Pathfinding/tests/DrawbridgeDeck.Tests`.
Die frühere passive Diagnose liegt in der Git-Historie (bis Commit `4b15f1aa7`) und in `history/`.
Die Abschnitte unten ab „Ursache in Vanilla“ beschreiben die Entwicklung (Momentaufnahmen).

## Ziel

Feindliche Einheiten (menschlich gesteuert und KI) sollen Wege über eine abgesenkte
Zugbrücke vor einem Torhaus gar nicht erst als gültig berechnen. Grund: Das Tor schließt,
sobald Feinde in der Nähe sind, und zieht dabei die Brücke hoch. Einheiten liefen deshalb
zur offenen Brücke und standen dann vor dem Graben. Bei seitlich ans Land stoßendem
Deck wirkte die Brücke sogar als Weg ins Burginnere.

## Ursache in Vanilla

Der Regionsaufbau `0xE49D0` behandelt beim Fluten alle Tore als geschlossen
(`0xC4BF0(1)`). Er übersieht aber, dass ein geschlossenes Tor seine Zugbrücke hochzieht.
Das abgesenkte Deck wird dadurch Teil der Außenregion und verbindet unter Umständen
außen und innen.

Alle Planer lesen nur diese Regionen (PCL) bzw. die Portalabfrage `0xE2610/0xE2F60`.
Dazu gehören:
- die KI-Belagerungsplanung `0xD95E0`, die ebenfalls mit geschlossenen Toren flutet,
- der Burgzugang `CF360/CF400`,
- Gruppenbefehle `0x11B520`,
- Pathfindings Cursor-Topologie.

## Lösung

1. **Virtuell hochgezogenes Deck:** In jedem Fenster „alle Tore zu“ (Detour auf `0xC4BF0`)
   verliert jedes abgesenkte Deck vor einem Torhaus alle Kanten zur Umgebung.
   - Die Torkante Deck↔Korridor erhält dagegen Vanillas eigenen Offen-Wert (über `0xD8CE0`).
   - Das Deck gehört damit zur Korridorregion des Tors.
   - Vor dem nächsten `0xC4BF0`-Aufruf werden alle geänderten Richtungsbytes exakt zurückgeschrieben.
2. **Portal-Endpunkt:** Nach `0xF4540` zeigt der Torportal-Endpunkt auf dem Deck auf die
   Region hinter dem Brückenende.
   - Besitzer, Verbündete und Eroberer kommen dadurch über das Tor-Portal weiterhin hinein und hinaus.
   - Für alle anderen gilt Vanillas Portalregel: kein Weg auf oder über das Deck.

Kosten: keine zusätzlichen Suchen.
- Pro Fenster werden einige Dutzend Bytes geändert.
- Pro Tick wird eine kleine Schleife über höchstens 199 Portale ausgeführt.
- Die Zeiten stehen in der Kartenende-Zeile (`cutMs`, `patchMs`).

## Bekannte Grenzen (bewusst)

- Land, das nur seitlich über das Deck erreichbar ist, ist auf Regionsebene für alle
  unerreichbar, genau wie bei hochgezogener Brücke.
- Eine von Hand dauerhaft abgesenkte Brücke (ExtraFeatures) gilt für Feinde ebenfalls als geschlossen. Das ist eine Nutzerentscheidung vom 16.11.2026 und entspricht dem Umgang mit offenen Toren. Sonst könnte man KI-Einheiten zum Tor locken und es vor ihrer Nase schließen.
- Steht ein Tor geschlossen und die Brücke ist noch unten, sind eigene Einheiten auf dem Deck
  regionsmäßig isoliert wie im Torkorridor.
- Zugbrücken ohne Torhaus davor bleiben Vanilla.
- Kachelsuchen zwischen zwei Punkten derselben Region (Pathfinding-Kantenmasken) blockieren
  Deckkanten noch nicht. Das ist nur bei ungewöhnlichen Layouts relevant, in denen das Deck
  zwei Teile derselben Außenregion verbindet.

## Prüfen

- `build.bat /nopause` führt die Quellprüfungen aus, baut und installiert. Pathfinding muss
  installiert sein (harte Abhängigkeit, mindestens 0.1.10).
- Spielabnahme (automatisch, ohne Maus/Tastatur):
  `Shared/Tools/Automation/Invoke-InGameTest.ps1 -ModGuid EnemyBridgePathTest_Serp -Scenario bridge-<save.sav> …`.
  Ein Modus pro Spielprozess, weil die Automatisierung Spielstände nur aus dem Hauptmenü lädt:
  - `bridge-<save>`: mit Zugbrückensperre; `bridge-off-<save>`: Vanilla-Gegenprobe.
  - `bridge-ai-on|off-<save>`: KI-Beobachtung; `bridge-perf-on|off-<save>`: Leistung.
  - Der Modus wird vor dem Laden über Pathfindings eigene Einstellung gesetzt (dazu auch
    `EnableMod` und `EnableEnemyGatePathfindingFix`); das Laden nutzt die aktuellen statt der
    im Spielstand gespeicherten Einstellungen. Am Laufende werden die geänderten Werte
    wiederhergestellt. Schritt `mode` prüft, dass die Sperre wirklich im verlangten Zustand ist.
  - Ablauf der Abnahme: Inventar, Regionen je Spieler, echte Bewegungsbefehle feindlicher
    Testsoldaten aufs Deck und ins Burginnere.
  - Ergebniszeilen: `BRIDGE_TEST ACCEPT PASS|FAIL`, am Ende `BRIDGE_TEST SUMMARY` mit
    Pathfindings Zählern.

Native Adressen und Updateprüfung: `Pathfinding/UpdateToNewDLL.md` (Abschnitt „Enemy drawbridge closure“).

## Ergebnisse 16.11.2026 (Momentaufnahme, Belege unter `.work/t/bridge-accept/`)

- `test_gates_bridge.sav`: 53/53, `test_gates_bridge2.sav`: 40/40 Prüfungen bestanden.
  - Mit Funktion erreichen die feindlichen KI-Spieler 2–8 weder Deck noch Burginneres.
  - Der Besitzer erreicht beides über das offene Tor.
  - Ohne Funktion (Gegenprobe) erreichen Feinde das Deck und sogar das Burginnere. In
    Spielstand 2 liegt das Burginnere dann in Region 1, also in der ganzen Außenwelt.
- Feindliche `MoveHere`-Befehle aufs Deck:
  - Ohne Funktion: 6/6 berechnete Pfade über das Deck.
  - Mit Funktion: 0/6, es wird kein Weg berechnet.
- Echtes KI-Verhalten über 7 500 Ticks, Tore offen gehalten:
  - Ohne Funktion betraten 11 verschiedene feindliche Einheiten das Deck.
  - Mit Funktion 0.
- Kosten:
  - Etwa 27 µs je Schließfenster mit Brücken und etwa 4 µs je Portal-Tick, zusammen etwa 0,1 % Laufzeit
    auf der 800er Karte mit ~10 000 Einheiten (`test_stresstest5.sav`, KI-Spieler 5 und 7 besitzen Torbrücken).
  - Ticks/s unterschieden sich je nach Kampfverlauf: Mit Funktion überleben mehr Einheiten, weil Angreifer
    nicht mehr über die Brücken in die Burgen stürmen.
  - Vanillas KI-Planungszeit in Schließfenstern und die Zahl der Regionsabfragen blieben vergleichbar.
- Szenarien:
  - `bridge-<save>`: Abnahme.
  - `bridge-ai-on|off-<save>`: KI-Beobachtung.
  - `bridge-perf-on|off-<save>`: Leistung. Den Modus vor dem Laden setzen.

## Offene Punkte

- Erledigt 17.11.2026: Integration in Pathfinding mit hostgesteuerter Einstellung und die
  Deck-Kantenmaske pro Spieler.
- Optional: Gate-Liste je Regionsaufbau cachen. Das ist nicht nötig, die Kosten sind gemessen gering.
- Optional: TODO G12 in `Pathfinding/src/TODO.md` (geschlossenes Tor bei abgesenkter Brücke).

## Zweiter Weg zum Keep – Kantenmaske (16./17.11.2026)

- Nutzerbefund: Gab es zusätzlich einen anderen Weg zur Burg, nutzten Gegner die abgesenkte Brücke
  trotzdem. Dann liegen Außenwelt und Burg in einer Region, und nur Kachelsuchen entscheiden.
- Lösung wie bei feindlichen Torhäusern: Pathfinding (ab 0.1.10,
  `GateTopologySnapshotProvider`) sperrt in `RouteTilePolicySnapshot` für jeden vom Tor nicht
  zugelassenen Spieler alle Kanten in die Deckzellen. Nur Kanten ins Deck, nie aus dem Deck heraus;
  nur Brücken desselben Besitzers wie das Tor.
  - Die Maske ist nur aktiv, solange dieser Testmod `SerpEnemyBridgeDeckClosureActive` meldet und
    die native DLL die geprüfte Version hat.
  - Entwickelt in einer Arbeitskopie (Branch `wip/bridge-deck-mask`); nach Review am 17.11.2026 ins
    Original übernommen, Arbeitskopie und Branch gelöscht.
- `test_gates_bridge3.sav` (zweiter Weg vorhanden): 54/54 Prüfungen.
  - Befehle ins Burginnere ohne Funktion: 6/6 über das Deck (13 Kacheln).
  - Mit Funktion: 6/6 über den anderen Weg (~380 Kacheln), 0 über das Deck.
  - KI-Beobachtung: 42 feindliche Einheiten auf dem Deck ohne Funktion, 0 mit Funktion.
- Abnahme mit dem installierten Pathfinding 0.1.10 (17.11.2026, Belege `.work/t/bridge-accept/`
  `42495ed22f84`, `b123aaf04687`, `6d98f35f6576`): `test_gates_bridge3.sav` 58/58,
  KI-Beobachtung 0 feindliche Deckbesucher, `test_gates_bridge.sav` 57/57.

## Geschlossenes Tor bei abgesenkter Brücke (Nutzerentscheidung 17.11.2026: vorerst so belassen)

- Ist ein Tor geschlossen, die Brücke aber unten (z. B. Einheiten auf dem Deck oder Handsteuerung),
  dann ist das Torportal deaktiviert.
  - Deck, Brückenkopf und nur seitlich erreichbares Land sind dann regionsmäßig auch für den Besitzer
    vom Burginneren getrennt.
  - Kachelweise kann der seitliche Weg offen sein.
  - Wirkung nur, wenn das Deck die einzige Verbindung ist. Gibt es einen anderen Weg, liegen
    Start und Ziel in derselben Region. Eigene und verbündete Einheiten laufen dann weiter über
    das Deck, weil die Kantenmaske nur nicht zugelassene Spieler sperrt (Nutzertest 17.11.2026
    bestätigt).
- In der künftigen Modsetting kurz und verständlich erwähnen; mögliche Ansätze als optionales TODO G12 in `Pathfinding/src/TODO.md`.
- Ein Spiel-Freeze beim Befehl „eigene Einheiten aus dem Graben zum Keep“ (`Log_369.log`, Zustand wie
  in `test_gates_bridge2.sav`) wurde in einer eigenen Sitzung untersucht: Der Testmod ist nicht als Ursache belegt;
  zwei Reproduktionsläufe froren nicht ein, der Befehl wurde sauber abgelehnt. Die Ursache bleibt
  offen, weil kein Speicherabbild existiert.

## Abnahme der Pathfinding-Integration (17.11.2026, Momentaufnahme)

Nur Pathfinding 0.1.10 mit Einstellung `EnableEnemyDrawbridgeClosure`, dieser Testmod 0.2.0 als
reine Abnahme. Belege unter `.work/t/bridge-accept/` (`398db8405139`, `cead1e6c5d2e`,
`5020605048b4`, `cc796af66467`, `9c894816e11c`).
- `test_gates_bridge3.sav` an: 30/30. Feindliche Befehle aufs Deck: kein Weg. Befehle ins
  Burginnere: 6/6 über den anderen Weg (~380 Kacheln), 0 über das Deck.
- `test_gates_bridge3.sav` aus (Gegenprobe): 6/6 Wege über das Deck, 100 Deckbesuche.
- KI-Beobachtung an: 0 feindliche Einheiten auf dem Deck.
- `test_gates_bridge.sav` 29/29, `test_gates_bridge2.sav` 28/28.
- Lauf `277e23e6bcd3` scheiterte einmal an einem veralteten Portal-Mittelfeld (C = 91 statt 92):
  Ein Regionsneuaufbau nach `0xF4540` im selben Tick lässt die Portalfelder bis zum nächsten
  Tick veraltet (Vanilla, siehe Baseline `PORTAL_TABLE.md`). Die Abnahme liest Portale seitdem
  erst bei aktuellem Stand (`PORTAL stale snapshot` im Log, sonst zwei Ticks weiter).
