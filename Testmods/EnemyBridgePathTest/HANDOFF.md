# EnemyBridgePathTest – Übergabe

Stand: 16.11.2026. Der Testmod wurde komplett neu geschrieben. Die frühere passive
Diagnose (Aufzeichnung, Offline-Nachrechnung, Schattengraph) liegt nur noch in der
Git-Historie (bis Commit `4b15f1aa7`) und in `history/` (Dokumente, unverändert
einschließlich der Codex-Ergänzungen vom 16.11.2026).

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

- `build.bat /nopause` führt Quellprüfungen und Kerntests aus, baut und installiert.
- Spielabnahme (automatisch, ohne Maus/Tastatur):
  `Shared/Tools/Automation/Invoke-InGameTest.ps1 -ModGuid EnemyBridgePathTest_Serp -Scenario bridge-<save.sav> …`.
  Der Ablauf, jeweils innerhalb eines Spielprozesses:
  1. Inventar.
  2. Regionen je Spieler.
  3. Echte Bewegungsbefehle feindlicher Testsoldaten aufs Deck.
  4. Gegenprobe mit abgeschalteter Funktion (`off-*`), danach wieder an.
  - Ergebniszeilen: `BRIDGE_TEST ACCEPT PASS|FAIL`, am Ende `BRIDGE_TEST SUMMARY`.

Native Adressen und Updateprüfung: [UpdateToNewDLL.md](UpdateToNewDLL.md).

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

- Integration in den Pathfinding-Mod mit einer hostgesteuerten Einstellung. Im Testmod ist `Enabled`
  eine lokale BepInEx-Einstellung; im Multiplayer müssen alle denselben Wert haben.
- Optional: Deckkanten auch in Pathfindings Kantenmasken pro Spieler sperren (siehe Grenzen).
- Optional: Gate-Liste je Regionsaufbau cachen. Das ist nicht nötig, die Kosten sind gemessen gering.
