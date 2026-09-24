# Startmarker-Laufzeitprüfung

Native-DLL: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Crater Lake: SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.

Die installierte Serie `aiv-start-marker-off-20260924` enthält zwei
zuvor nicht ohne Sofortbau aufgenommene Aufstellungen mit je sieben KIs:
`CL-B-off` und `CL-Reverse-off`. Alle AIVs sind feste Default-Varianten;
„Completed Castles“ ist jeweils aus. Die Varianten und Reihenfolgen
stammen unverändert aus der geprüften Sechs-Match-Serie. Nur die
Sofortbau-Option wurde ausgeschaltet. Die vorige abgeschlossene Serie
und ihr Fortschritt liegen als `previous-series.json` und
`previous-progress.json` in diesem Ordner.

Pro Lauf eine neue lokale Skirmish-Lobby öffnen, automatische
Aufstellung und Option unverändert lassen und die Karte bis zum
sichtbaren Spielbeginn starten. Auf den normalen späteren Burgbau muss
nicht gewartet werden. Nach zwei bestätigten Starts muss der Fortschritt
`nextIndex: 2` zeigen. Der Detector erfasst alle Spieler unabhängig von
ihren IDs. Erwartet werden 16 native Starttraces und native Fits für
die späteren KIs; das ist ein neuer Oracle-Vergleich für die Weitergabe
des ausgewählten Startmarkers und die geänderte Lobby-Anzeige.

Bleibt ein Match unbestätigt, wird derselbe Eintrag erneut angeboten.
Bei einer Abweichung oder einem unvollständigen Trace bleibt der
betroffene Offline-Fit `NotEvaluable`, bis die Ursache geklärt ist.
