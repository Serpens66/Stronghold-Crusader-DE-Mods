# Gemeinsamer Multiplayer-Test für Resync, KI-Handel und ESC-Neustart

## Automatischer Ablauf

Der Host-Treiber startet eine private Partie auf **The Jordan Valley** und
richtet null Minuten Friedenszeit und den verbündeten Testlord
**SerpMarketRest4** für den Handel ein. Die Burgpositionen sind frei.
Sobald die Lobby bereit ist, genügt der normale Spielstart am Client.
UiAcceptance übernimmt Beitritt und Bereitmeldung. Die aktivierte
Testkonfiguration gilt für einen Start.

Der Host sucht im laufenden Spiel freie Testflächen und teilt sie dem Client mit.
Die automatische Probe setzt und entfernt einen 3×3-Graben über Vanillas
synchronisierte Mapper-Befehle. Sie vergleicht das Kachelraster und die
private Historie während der Reparatur sowie das Oasengras nach dem Entfernen.
Der Lauf legt auch zwei Holzfällerhütten für die Einzelpause an und testet
zwei Einheiten-Resyncs, KI-Handel sowie Abbrechen und zweimaliges Bestätigen
des originalen ESC-Neustarts. Das natürliche Graben und Verfüllen durch
Arbeitseinheiten lässt sich mit dem manuellen Ablauf unten gesondert prüfen.

## Manueller Ablauf

Der Ablauf benötigt einen Spielstart pro Rechner. Host und Client verwenden
getrennte Steam-Konten und starten eine **neue private Skirmish-Partie** mit zwei
Menschen und mindestens einer KI. Wählt eine Karte mit Oasengras in Reichweite
der Spieler und Platz für eine wirtschaftlich aktive KI. Der Host aktiviert
vorher in BugfixesAndQoL **Boden nach Burggrabenentfernung wiederherstellen** und
**Pausieren einzelner Gebäude aktivieren**. Verschiedene Spielsprachen auf
Host und Client decken zusätzlich die Übersetzung des Neustartdialogs ab.

Für einen kurzen Lauf eignet sich **The Jordan Valley** mit beliebigen
Startplätzen und **0 Minuten Friedenszeit**. Auf dem Host kann der Testlord
**SerpMarketRest4** als Verbündeter hinzugefügt werden. Seine KI-Probe läuft
nebenbei. Für die Graben-, Gebäudepause- und Neustartprüfung reicht eine
gewöhnliche KI. Den Graben hebt der Host auf einer freien Grasfläche aus.
Einheiten-Resync und Vorkommen wurden bereits geprüft und müssen für die
offenen Fälle nicht wiederholt werden.

Benötigt werden die identischen Pakete auf beiden Rechnern, insbesondere
APIShared **0.69.0** mit passendem Patcher, BugfixesAndQoL **1.0.237**,
MakeDesync **0.2.1**, Deposit Restoration **0.3.1**, der
AI-Market-Diagnosebuild **0.1.0** und MultiplayerRestartTest **0.1.4**.
Die frühere Partie `test_stone` deckt bereits Mauerabriss und Vorkommen ab.
Für die positiven ESC-Neustarts bleibt die neue Partie die Ausgangsmission.

## 1. Vorbereitung in der laufenden Partie

1. Lasst die KI einen Markt betreiben und Waffen rekrutieren. Genügend Gold
   und ein laufender Handel ergeben mehr Beobachtungen vor und nach der
   Reparatur. Der Client-Lord bleibt bei voller Gesundheit.
2. Hebt auf Oasengras **genau ein Grabensegment** aus. Merkt euch die Stelle
   und wartet, bis die Arbeitseinheit das Segment verlassen hat.
3. Falls zwei Produktionsgebäude desselben Typs vorhanden sind, pausiert den
   Typ über die normale Pause-Schaltfläche. Startet danach nur Gebäude A
   wieder, indem ihr **Strg** beim Klick auf die Schaltfläche haltet.
   Gebäude B bleibt pausiert. Wartet, bis die Zustände auf beiden Rechnern
   angekommen sind.

## 2. Resync und Handel

1. Am **Client** einmal **Strg+Umschalt+F9** drücken. Wartet, bis das Spiel
   weiterläuft. Wiederholt F9 nach dem vollständigen Abschluss, wenn ihr den
   Einheitenfall zweimal in Folge prüfen wollt. Die beiden Versuche müssen
   getrennte Reparaturnummern erhalten.
2. Kampf und Änderungen in der Nähe des Grabens während des Versuchs
   vermeiden. Am **Client** einmal **Strg+Umschalt+F11** drücken. Der
   erwartete Clientabschluss enthält `mode=moat`, `divergedAtStart=True`,
   `tileRestored=True`, `historyRestored=True` und `restored=True`.
   Wenn nur der Grabendatensatz abweicht, meldet APIShared einen übermittelten
   Datensatz mit **641.616 Bytes**. Ein zweiter abweichender Datensatz
   wird anhand der Logs ausgewertet.
3. Nach dem bestätigten Abschluss das Segment mit einem normalen
   Spielbefehl verfüllen. Beide Seiten vergleichen den ursprünglichen
   Boden. Am **Client** einmal **Strg+Umschalt+F8** drücken. Der Logmarker
   `MAKE_DESYNC_MOAT_FILL` soll `restored=True` melden.
4. Falls ihr die Gebäudepause eingerichtet habt, prüft auf beiden Rechnern
   direkt nach dem Resync den fortbestehenden Unterschied zwischen A und B.
   Ein anschließender normaler Typ-Pausenbefehl räumt die Einzelausnahmen
   für den Typ absichtlich auf.
5. Lasst die KI noch weiter handeln. Für jeden Resync vergleichen wir auf
   beiden Rechnern `MAKE_DESYNC_MARKET_BOUNDARY` bei `stage=start`
   und `stage=complete`. `pass=True` bestätigt für diese Callbackgrenzen
   unveränderte native Kaufzellen, geleerte private Zuordnungen und die
   erwarteten Writer-Gates. Neue Handelsmarker oder Zähler nach dem
   Abschluss belegen, dass die KI wieder Aufträge bearbeitet.

Der spezielle Marktfall mit echtem Fünferauftrag, Rest 4 und Gutschrift 1
braucht einen bestätigten Auftrag mit genau dieser Historie. `partialBefore`
zeigt, ob der Beobachter zu Beginn einen passenden Zwischenstand gesehen
hat. Für eine gezielte Reproduktion fehlt eine synchronisierte MP-Fixture.

## 3. ESC-Neustart zuletzt

1. Öffnet ESC auf beiden Rechnern. Der Host sieht genau den originalen
   Neustartknopf, der Client kann den Neustart nicht auslösen. Prüft auch
   die zusätzlichen Mod-Aktionen auf einen zweiten Neustarteintrag.
2. Host klickt den Originalknopf und antwortet einmal **Nein**. Die Partie
   läuft weiter. Dann erneut klicken und **Ja** wählen.
3. Lobby-Wiederaufbau und Spielstart laufen selbständig. In der Zwischenlobby
   nichts anklicken oder umstellen. In der gestarteten Partie auf beide
   Rechner schauen: Karte, Slots, Teams, Farben, Lords und KI stimmen
   mit dem ursprünglichen Start überein. Das Spiel muss auf beiden
   Rechnern wirklich weiterlaufen. Wartet auf den vollständigen Start
   und wiederholt den Neustart einmal.
4. Für die Auswertung brauchen wir auf beiden Rechnern dieselbe
   `MP_RESTART_TEST`-Operation bis `RESTART_COMPLETE`, dazu
   `TARGET_LOADED`, `SIMULATION_READY` und fortlaufende Simulation.
   Host `TARGET_SEED_SENT allPeersLoaded=true` muss auf die Ladebelege
   beider Teilnehmer folgen.

Bricht eine bereits ausgelöste Reparatur oder ein Neustart nach dem Stoppen
der alten Partie ab, beendet die Testpartie. Die Abbruch- und
Disconnect-Fälle werden getrennt von diesem positiven Durchlauf geprüft.
Die beiden BepInEx-Logs und die Sichtprüfung nach F8 und nach jedem
Neustart reichen für die gemeinsame Auswertung.

[MakeDesync-Kürzel](USAGE.md) ·
[ESC-Neustart und Logmarker](../MultiplayerRestartTest/TESTING.md)
