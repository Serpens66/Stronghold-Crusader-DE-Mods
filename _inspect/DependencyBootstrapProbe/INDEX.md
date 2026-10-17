# Bootstrap-Probe: finale Nachweise
Stand: 17.10.2026. Paarbindung und lokaler Extender-Paketwechsel A -> B bestanden.
Die Probe ist inzwischen gesichert und inaktiv; APIShared übernimmt den aktuellen Spieltest.

## Maßgebliche Ergebnisse
- local-update/after-20261017-172202.zip: erfolgreicher Neustart mit B, Logs,
  Lifecycle-Berichte und die zugehörigen Probe-Dateien zur Wiederherstellung.
- local-update/stage-20261017-172023-complete.zip: vollständiger Stagingnachweis.
- local-update/after-check.log und stage-check.log: Auswertung beider Phasen.
- local-update/finished.json: abgeschlossene Entfernung der Versuchsverwaltung.
- lifecycle-implementation/final/result.json: letzte 96 Offline-Paketprüfungen,
  Einzelergebnisse und Quellhashes. Kein Nachweis realer Abonnementzustände.
- APIShared-Entwicklungspaket und dessen Abschlussbericht:
  ../../APIShared/.local/bootstrap-transfer/ (ohne Installation gebaut).

## Noch benötigte Referenzen
- Correction-accepted-20261016-2154: echter positiver/negativer Korrekturversuch;
  die Pakettests verwenden außerdem dessen Binärdaten.
- Lifecycle-accepted-20261017-152951: Referenzlauf der Logprüfertests.
- Pair-accepted-20261017-161346: geprüfte Paarbindung und Referenz des Übernahmetreibers.
- Shadow-accepted-20261016-1946: Schattennachweis und frühe Quellhashes des Übernahmetreibers.
- lifecycle-runtime/AUDIT.md und evidence.zip: Extender-Audit, Quellnachweis und Prüfprotokolle.
  Diese belegen ihren jeweiligen historischen Build, nicht die aktuell installierte Version.

## Ablage und Bereinigung
Der Pakettest erzeugt nur einen kurzen temporären t-Ordner unter
lifecycle-implementation. Ein finally-Block entfernt ihn einschließlich Junctions,
ohne deren Ziele zu verfolgen. Auch bei einem Testfehler bleiben nur Ergebnis und
Fehlertext im festen final/result.json; der nächste Lauf ersetzt diesen Bericht.
Quellen werden über Hashes nachgewiesen, nicht für jeden Lauf erneut kopiert.

Entfernt wurden 153 überholte Dateien (31,17 MiB): wiederherstellbare Fixtures samt
alten Fixture-Archiven, doppelte Rückfallsicherungen, unvollständige Archivversuche
und ersetzte Zwischenstände. Die erfolgreichen, weiterhin benötigten Referenzen
wurden nicht pauschal umbenannt oder entfernt. Kein Eingriff in installierte Mods.
Automatische Bootstrap-Entfernung und eigener Unity-Mono-Nachweis der
APIShared-Assemblies bleiben offen.