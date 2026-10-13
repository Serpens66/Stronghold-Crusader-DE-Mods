# Current next test: complete defended closed-gate descent (2026-10-13)

After installation restart the game. DetailedDiagnostics=true, MacemenDiagnostics=false, improved Assassin search=false (outer AND __SerpCurrentSettings), direct gatehouse climbing=true. Mainmods/Fixes unchanged, bridge test off.

1. Reuse the LARGE CLOSED enemy gate with side walls, no drawbridge, and a living defender of the owner on the roof to prevent capture.
2. Give one enemy Assassin a fresh ground move about five tiles behind it.
3. Do NOT issue a second command. Expected: ascend, traverse the roof, descend and reach the ground target despite prevented capture.
4. Optional short small-gate counterpart in the same map. Leave the map normally and report completion or where he stopped, plus any visible fight.

The game check verifies the narrow descent exception in actual execution. No repeat Raid/ordinary-soldier/recapture/random-match test is requested. Reference saves stay untouched. Mainmod integration remains deferred.

---
Previous instructions retained as historical context below; the current test above supersedes their next-step directions.

# Ein kurzer Vergleich mit nativer Rekonstruktionsdiagnose

## Vorbereitung

Der native Vergleich18:13--18:14 ist beobachtet bestanden. Dieser Lauf dient der internen Zuordnung der bislang unklaren Ausgaben, nicht einer Wiederholung aller Funktionsregressionen.

- EnemyGatePathfindingTest aktiv, EnemyBridgePathTest aus.
- Verbesserte Assassin-Wegsuche AUS; direkte Torhaus-Kletterfunktion AN.
- DetailedDiagnostics=true, MacemenDiagnostics=false. Beide wirksamen gespeicherten Verbesserungswerte wurden als false gelesen; Klettern ist true.
- Hauptmods und Fixes unveraendert. Nach Laden eines Presets im Modmenue nochmals Verbesserung AUS kontrollieren.
- Vorhandener Editoraufbau: kleines und grosses Tor, seitliche Mauern, keine Zugbruecken; Besitzer Spieler1, feindliche Assassinen Spieler2. Referenzsave nicht ueberschreiben.

## Nach der Installationsmeldung: ein Spielstart, zwei Ausgangslagen

1. Ausgangsstand mit offenen, uneroberten Toren laden. Einen einzelnen Assassin Spieler2 etwa zehn Kacheln vor das grosse Tor stellen. Neuen normalen Bodenbefehl etwa fuenf Kacheln dahinter geben und bis zum ersten Zugang keinen weiteren Befehl erteilen.
2. Am kleinen, noch uneroberten Tor dasselbe tun. Jeweils Klettern, Umweg, Stillstand oder Bodenpassage vor Eroberung beobachten. Automatisches Schliessen des Tors ebenfalls nennen.
3. Sobald ein Tor von Spieler2 erobert wurde: einen weiteren Assassin Spieler2 von VOR diesem Tor mit einem NEUEN Bodenbefehl dahinter schicken. Danach einen ausdruecklichen Dachbefehl an diesem Tor geben. Die neuen Befehle unterscheiden alte Kletterplaene von neuer Planung.
4. Unveraenderten Ausgangsstand erneut laden, beide Tore schliessen und die zwei einzelnen Bodenbefehle wiederholen. Anschliessend Karte verlassen.

Bitte groesses/kleines Tor und offen/geschlossen in der Rueckmeldung zuordnen. Kein langes KI-Match; Raid, normale Soldaten und Rueckeroberung sind bereits geprueft und brauchen keinen weiteren Test.

## Was das Log jetzt nachweisen soll

- Native Probe installiert und mindestens ein plausibel zugeordneter Callback; keine Callback-/Identitaetsfehler.
- Ausgefuehrte sekundaere Bodensuche und deren Fallback werden direkt beobachtet. Ein +94-Wertwechsel allein gilt nicht als Ausfuehrung.
- Kandidaten, ausgewaehlte Rekonstruktion, ersetzte Rekonstruktion und endgueltige Ausgabe bleiben getrennt.
- Nur vollstaendig uebereinstimmende finale Kanten erhalten native Boden-/Kletterbewertung. Nicht zuordenbare Puffer oder Wand-zu-Wand-Uebergaenge bleiben mit konkretem Grund unbekannt.
- Ein nicht ausgeloester Zweig bleibt nicht beobachtet; seine technische Simulation wird separat ausgewiesen. Ein weiterer Lauf wird nur fuer eine konkret benannte Restfrage verlangt.

Bis zur Auswertung Diagnose und Verbesserungseinstellungen so lassen. Hauptmodintegration bleibt zurueckgestellt.
