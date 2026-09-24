# Gezielte AIV-Serie: natürliche verbundene Baugruppen

Status: **am 24.09.2026 für den Spieltest installiert**. Beide
Diagnoseplugins liegen wieder unter `BepInEx/plugins`; die bisherigen
Rohtraces bleiben in `C:\Users\Serpens66\Desktop\Neuer Ordner`.
`AivLobbyNaturalLinkSeries.json` ist als aktive
`BepInEx/config/AivLobbyTestSeries.json` installiert. Die passende
Fortschrittsdatei steht bei Index 0. Die vorherige Konfiguration liegt
unter `Findings/AIVPlacement/AivSeries-20260924/NaturalLinkSeriesSetup`.

Die acht Einträge setzen alle verfügbaren Keep-Slots der jeweiligen Karte.
Nur die ersten beiden KIs werden in der umgekehrten Folge getauscht; ihre
Keep-Slots und AIVs bleiben gleich. Für jeden Aufbau folgen „Completed
Castles“ aus und an direkt aufeinander.

| Läufe | Karte, SHA-256 | Frühe KIs | Spieler |
| --- | --- | --- | ---: |
| `Crossing-forward-off/on` | `CrusadesCrossing.map`, `B5AB8BCC5C4C2783697EEF4BBE692AE829B2C7D59C4AFF540F79E140C49417FE` | Wolf Default 7 auf Slot 0, Sentinel Default 2 auf Slot 1 | 8 |
| `Crossing-reverse-off/on` | gleicher Hash | Sentinel Default 2 auf Slot 1, Wolf Default 7 auf Slot 0 | 8 |
| `Reed-forward-off/on` | `Reed Sea.map`, `C6ABE1353E0EA1829B626408B4F3807E4473B428AE239FD15A52E9745FFF8116` | Wolf Default 2 auf Slot 5, Sentinel Default 2 auf Slot 2 | 7 |
| `Reed-reverse-off/on` | gleicher Hash | Sentinel Default 2 auf Slot 2, Wolf Default 2 auf Slot 5 | 7 |

Auf Crusades Crossing sitzt der Mensch auf Slot 5; die weiteren KIs sind
Nizar Default 6, Marshal Default 7, Emir Default 1, Abbot Default 2 und
Jewel Default 5. Auf Reed Sea sitzt der Mensch auf Slot 1; die weiteren
KIs sind Nizar Default 6, Marshal Default 7, Emir Default 1 und Abbot
Default 2. Der Testmod prüft beim Setzen und Kartenstart die tatsächliche
Karte, AIV-Daten, Spielerfolge, Keep-Zuordnung und Startoption.

## Erwarteter Auslöser und Grenze

`Find-NaturalLinkPairs.ps1` hat mit dem aktuellen Offline-Projektor
zwei gezielte Überschneidungen gefunden. Auf Crusades Crossing liegt
Wolfs Mapper-87-Bauschritt 6 vor dem ersten blockierten Bauschritt 7;
43 projizierte Mapper-Zellen schneiden den nächsten Keep-Bereich.
Auf Reed Sea liegt Mapper-87-Bauschritt 7 vor dem ersten blockierten
Bauschritt 81; die projektierte Schnittmenge umfasst 78 Zellen.
Das ist **keine** Behauptung, dass Vanilla alle Gebäude dieser Gruppe
tatsächlich baut. Genau das sollen Native-Auswahl, Frame-Trace,
Gebäuderecords und Tile-Diff feststellen. In der umgekehrten Reihenfolge
kann der Konflikt anders oder gar nicht auftreten; das ist der
Kontrollfall.

Map-Dateien und sämtliche verwendeten Built-in-AIV-Dateien wurden vor
Erzeugung der Serie mit dem installierten MapParser und AIVParser gelesen.
Die Map-Hashes, maximalen Spielerzahlen (8 und 7), exakten Keep-Anker
und die AIV-Validität werden vom Erzeugungsscript geprüft. Der Testmod
validiert im Spiel zusätzlich die Lobby-Daten. Ein unbestätigter Lauf
verschiebt den Serienfortschritt nicht.

Ein gezielter Konstruktorabbruch ist noch nicht als reproduzierbares
Preset belegbar. Die fünf gerade ausgewerteten Starts und die älteren
Traces zeigen dafür kein gesetztes natives Fehlerflag. Die bekannten
Prüfzweige in RVA `0x77E60` hängen unter anderem von lebenden Einheiten,
gegnerischen Distanzen und Pfaden ab. Eine erfundene Abbruchkarte würde
hier nur einen unsicheren Spielstart verbrauchen. Dieser Zweig bleibt im
Produkt `NotEvaluable`, bis ein konkreter Auslöser belegt ist.

Vor der Installation wurden der Native-Hash
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
und die installierte Script-Extender-Version 2.10.1 geprüft. Die
Diagnose-DLLs und Konfigurationen sind bytegleich mit den vorbereiteten
Quellen. Die acht verwendeten AIVJSON-Dateien stimmen ebenfalls bytegleich
mit dem validierten CastlePlanner-Paket überein. Ein Build war nicht nötig,
weil keine Runtime-Quelle geändert wurde.

Je Eintrag reicht eine neue lokale Skirmish-Lobby und der sichtbare
Spielbeginn; normaler späterer KI-Bau muss nicht abgewartet werden.
Die Aufstellung und „Completed Castles“ nicht von Hand ändern.
Beim ersten Lobby-Öffnen muss das Log
`Test series run 1/8: Crossing-forward-off` melden. Falls diese Meldung
oder die automatische Aufstellung fehlt, den Kartenstart auslassen und
das Log prüfen lassen. Nach dem Block werden Log und Rohtraces sofort
mit Prüfsummen in den Workspace kopiert.

## Einzel-Kontrolllauf nach der Serie

Nach dem ersten Start zeigte Spieler 2 zwar Startflagge und Lagerplatz,
aber keinen Keep. Die synchronen Native-Traces belegen, dass Spieler 3
dessen Keep-Gruppe beim eigenen Start räumte. Um Einflüsse anderer Mods
zu begrenzen, ist `AivLobbyNaturalLinkControlSeries.json` als **separate,
noch nicht installierte** Ein-Match-Serie mit genau demselben ersten
Preset vorbereitet. Ihre eigene Fortschrittsdatei beginnt bei Index 0.
Sie wird erst nach Abschluss und Archivierung der acht laufenden Starts
aktiviert. Für die automatische Lobby und Diagnose bleiben Script
Extender, APIShared, BugfixesAndQoL, ActiveAIVDetector und der Testmod
erforderlich; sachfremde Gameplay-Mods können für den Kontrolllauf
deaktiviert werden. Weitere Änderungen an der aktuell laufenden Serie
erfolgen nicht.

Die acht Läufe sind inzwischen beendet. Der Testmod entfernt nach dem
letzten bestätigten Kartenstart beim nächsten Öffnen einer lokalen
Skirmish-Lobby einmalig alle KI-Spieler und merkt dies in
`endLobbyCleared`. Eine neue Serie mit eigener ID beginnt weiterhin bei
Index 0. So zeigt eine leere Lobby das Serienende und die zuletzt
gesetzte KI-Aufstellung führt nicht versehentlich zu einem Zusatzlauf.
