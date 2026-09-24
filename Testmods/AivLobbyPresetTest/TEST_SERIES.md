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

Für die **Regressionsprüfung nach einer Korrektur** liegt die
`AivLobbyStartRebuildRegressionSeries.json` bereit. Sie enthält ausschließlich
`CC-A-off/on` und `CC-B-off/on` in dieser Reihenfolge, also vier statt zehn
Kartenstarts. Am 24.09.2026 wurde sie nach dem Native-Audit als
`BepInEx/config/AivLobbyTestSeries.json` installiert und der Fortschritt
für die neue `seriesId` ausdrücklich auf Index 0 gesetzt. Der erste
ausstehende Lauf ist `CC-A-off`. Die bisherige
Zehn-Match-Aufnahme und ihre Berichte bleiben erhalten.

Der Detector zeichnet jetzt zusätzlich den nativen Keep-Start über den
Script-Extender-Event `OnBuildStructure` auf. Für jeden bestätigten
Kartenstart stehen die nativen Statusfelder vor/nach dem Aufruf und die Änderungen der acht
fitrelevanten Tile-Schichten in `ActiveAIVDetector_Serp/StartTraces`.
Die vier Craggy-Einträge können damit die offene Konstruktorfrage
gezielt prüfen; der erste Start validiert zunächst, dass der neue
Event-Trace tatsächlich vollständig ist. Es genügt jeweils, die
vorbereitete Lobby zu öffnen und die Karte einmal bis zum sichtbaren
Spielstart zu laden. Normalen späteren KI-Bau nicht abwarten.

Die vier Einträge wurden am 24.09.2026 abgeschlossen (`nextIndex=4`,
letzter Lauf `CC-B-on`). Alle 32 Keep-Start-Traces sind vollständig und
unter `CastlePlanner/Diagnostics/AivSeries-20260924/StartRebuildRegression`
archiviert. Die gemessenen Starts belegen keinen fehlgeschlagenen
Konstruktor und keine allgemeine Schreibbereichsgrenze; weitere
Kartenstarts sollen nur einen konkret offenen Native-Zweig prüfen.

Für die nächste Messung wurde `AivLobbyFullMapProbeSeries.json` als
**einziger** automatischer Lauf vorbereitet und am 24.09.2026 unter
`BepInEx/config/AivLobbyTestSeries.json` aktiviert (`nextIndex=0`). Der
Lauf `CC-A-on` nutzt Craggy Cliffs, sieben feste KIs/AIVs und
„Completed Castles“ an. Er ist gewählt, weil der frühere regionale Trace
beim Emir-Start 60 gelöschte und acht ersetzte Gebäudezellen zeigte.
Der neue Detector misst stattdessen alle 320.800 Tile-IDs vor und nach
jedem Keep-Start. Nach diesem einen bestätigten Kartenstart ist die
Serie vollständig und die automatische Vorbereitung endet. Die vorherige
Serienkonfiguration samt Fortschritt ist unter
`CastlePlanner/Diagnostics/AivSeries-20260924/FullGridProbeSetup`
gesichert. Es genügt, die vorbereitete lokale Lobby zu öffnen und die
Karte bis zum sichtbaren Spielbeginn zu laden; normalen KI-Bau muss man
nicht abwarten. Keine Optionen oder Spieler von Hand ändern.

Der Vollkartenlauf `CC-A-on` wurde abgeschlossen. Alle acht
Keep-Aufnahmen waren vollständig; die Archivierung und Auswertung steht
unter `CastlePlanner/Diagnostics/AivSeries-20260924/FullGridProbeResults`.
Für den nächsten **einzelnen** Lauf liegt
`AivLobbyCrater180ProbeSeries.json` bereit: `CL-A-on` auf Crater Lake,
sieben KI-Spieler und „Completed Castles“ an. Das alte Oracle-Archiv
zeigt bei dieser Aufstellung 180°-Vanilla-Auswahlen für mehrere KIs;
die neuen Vollkarten-Start-Traces sollen diesen noch nicht gemessenen
Konstruktorzweig prüfen. Nach beendetem Spiel wurde diese Serie am
24.09.2026 in `BepInEx/config/AivLobbyTestSeries.json` aktiviert
(`nextIndex=0`). Die zuvor abgeschlossene Serie und ihr Fortschritt liegen
unter `CastlePlanner/Diagnostics/AivSeries-20260924/Crater180ProbeSetup`.
Nach dem einen bestätigten Match endet die automatische Vorbereitung wieder.

Die Ein-Match-Serie wurde vor ihrem ersten Lauf durch
`AivLobbySixMatchSeries.json` ersetzt. Die neue Serie übernimmt aus der
bereits validierten Zehn-Match-Quelle genau `CL-A-off`, `CL-A-on`,
`CL-B-on`, `CL-Reverse-on`, `CC-B-off` und `CC-B-on` in dieser Reihenfolge.
Alle sechs Presets enthalten sieben KI-Spieler. Die Testprüfung vergleicht
Karte, Hash, Sofortspawn-Option und jede Spieler-/Keep-/AIV-Zuordnung mit
der Quelle und prüft das Fortschreiten bis zum Serienende. Der schon
aufgenommene Vollkartenlauf `CC-A-on` bleibt Vergleichsdatenpunkt.
Die Serie wurde bei beendetem Spiel als
`BepInEx/config/AivLobbyTestSeries.json` aktiviert. Der Fortschritt steht
auf `CL-A-off` bei Index 0. Die vorherige Ein-Match-Konfiguration samt
Fortschritt liegt unter
`CastlePlanner/Diagnostics/AivSeries-20260924/SixMatchSetup`.
