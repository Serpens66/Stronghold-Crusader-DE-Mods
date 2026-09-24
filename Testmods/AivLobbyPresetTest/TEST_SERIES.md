# AIV-Diagnoseserie

Die Datei `AivLobbyTestSeries.json` enthält zehn vorbereitete Kartenstarts.
Der Testmod kopiert sie beim ersten Start nach `BepInEx/config/AivLobbyTestSeries.json`.
Diese Konfigurationsdatei ist editierbar. Jeder Lauf enthält genau einen
menschlichen Spieler, sieben KIs, eine Karte mit SHA-256, feste Keep-Slots,
je KI eine eingebaute `Default N`-AIV und den Wert von „Completed Castles“.
Die AIV-Drehung bleibt auf Vanilla-Auto.

| Reihenfolge | Aufstellung | Sofortspawn |
| ---: | --- | --- |
| 1–2 | Crater Lake: Nizar Default 6, Wolf Default 8, Marschall Default 7, Emir Default 1, Abt Default 2, Jewel Default 5, Nomade Default 4 | aus, an |
| 3–4 | Crater Lake: Nizar Default 5, Wolf Default 3, Marschall Default 2, Emir Default 8, Abt Default 6, Jewel Default 1, Nomade Default 7 | aus, an |
| 5–6 | Crater Lake: erste Variantenmenge in umgekehrter KI-Reihenfolge und an denselben Keep-Slots | aus, an |
| 7–8 | Craggy Cliffs: erste Variantenmenge | aus, an |
| 9–10 | Craggy Cliffs: alternative Variantenmenge | aus, an |

Beim Öffnen einer lokalen Host-Skirmish-Lobby wird der aktuelle Lauf gesetzt.
Handänderungen bleiben möglich. Weicht die Lobby beim Start von Karte,
Spielerfolge, Keep-Slots, AIV-Daten oder Sofortspawn-Wert ab, bleibt der
Fortschritt stehen. Nach bestätigtem Matchstart wird
`BepInEx/config/AivLobbyTestSeries.progress.json` atomar auf den nächsten
Eintrag gesetzt. Spiel beenden und Lobby wieder öffnen genügt; auf den
normalen späteren Burgbau der KIs muss nicht gewartet werden. Nach Lauf 10
greift der Testmod nicht mehr in die Lobby-Vorbereitung ein.

Für das bisherige manuelle Dreier-Preset `enabled` in der Serienkonfiguration
auf `false` setzen. Danach gilt wieder `BepInEx/config/AivLobbyPresetTest.json`.
Zum Neubeginn der Serie die Fortschrittsdatei gezielt entfernen oder
`nextIndex` auf `0` und `lastCompletedRunId` auf `""` setzen. Ein veränderter
bereits abgeschlossener Teil der Serie wird nicht stillschweigend übersprungen.
Der Detector schreibt unabhängig von der vorgegebenen Spieler-ID für jeden
aufgenommenen Fall native Auswahl, Fit und gegebenenfalls Bau-Differenzen.

Die erste Serie wurde am 24.09.2026 vollständig aufgenommen (`nextIndex=10`,
letzter Lauf `CC-B-on`). Der [Auswertungsbericht](../../CastlePlanner/Diagnostics/AivSeries-20260924/RESULTS.md)
archiviert die Oracle-Korpora und Roh-Traces. Drei Offline-Abweichungen auf
Craggy Cliffs betreffen die Rekonstruktion benachbarter Startgebäude.
Weitere Starts mit unverändertem Mod sind zunächst nicht nötig; vor einer
gezielten Wiederholung muss dieser native Startbaupfad geklärt werden.

Für die **Regressionsprüfung nach einer Korrektur** liegt die noch inaktive
`AivLobbyStartRebuildRegressionSeries.json` bereit. Sie enthält ausschließlich
`CC-A-off/on` und `CC-B-off/on` in dieser Reihenfolge, also vier statt zehn
Kartenstarts. Sie wird nicht automatisch geladen. Erst nach dem Code-Fix
die Datei als `BepInEx/config/AivLobbyTestSeries.json` einsetzen und den
Fortschrittsstand für die neue `seriesId` ausdrücklich auf Index 0 setzen;
andernfalls verweigert der Testmod die Übernahme absichtlich. Die bisherige
Zehn-Match-Aufnahme und ihre Berichte bleiben erhalten.
