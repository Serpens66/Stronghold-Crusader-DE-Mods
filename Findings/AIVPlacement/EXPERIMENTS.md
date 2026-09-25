# AIVPlacement: Versuche und Belege

Stand: 25.09.2026. Diese Datei fasst die bisherigen Ergebnisberichte und den
früheren CastlePlanner-Forschungsstand zusammen. Der aktuelle Produktumfang
steht in [STATUS.md](STATUS.md), ältere native Forschung in
[HISTORY.md](HISTORY.md). Der maßgebliche native Vertrag steht weiterhin in
der [Native-Baseline](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md).

## Provenienz und Wiederherstellung

Die aktuelle untersuchte `CrusaderDE.dll` hat SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die Aufnahmen stammen aus verschiedenen Script-Extender-Ständen; die beiden
ausdrücklich protokollierten installierten Versionen sind 2.9.0 und 2.10.1.
Map-, AIV-, DLL- und Log-Hashes jeder Sitzung stehen in ihren ursprünglichen
Berichten und Manifesten im Archiv. Alte Oracle-Korpora mit anderem DLL-Hash
sind Vergleichsdaten, kein Nachweis für den aktuellen Native-Vertrag.

`EVIDENCE.zip` enthält die ursprünglichen 3.696 Pfade einschließlich aller
Rohtraces, ursprünglichen Berichte, Skripte, Logs, Karten, AIVs, Manifeste,
`Findings/AICastlePlanner.md` und des CastlePlanner-Forschungsstands. Sein
SHA-256 ist
`FDB74AA7A477BF771C095E55387DA95D1B30CBA99BC71C78221FF6A6B7ACDCCC`.
Das Archiv speichert 2.861 unterschiedliche SHA-256-Datenblöcke; die
Pfadzuordnung und Dateilängen stehen in `manifest.json` im Archiv. Die
Wiederherstellung wurde für alle 3.696 Dateien mit Hashprüfung erprobt.
Von 3.324 TSV-Pfaden waren 749 bytegleich doppelt; die übrigen 2.575
unterschiedlichen TSV-Inhalte bleiben ebenfalls vollständig erhalten.

Prüfen oder in einen **neuen** Zielordner wiederherstellen:

```powershell
& 'Findings/AIVPlacement/Restore-Evidence.ps1' -VerifyOnly
& 'Findings/AIVPlacement/Restore-Evidence.ps1' -Destination 'D:\AivEvidenceRestore'
```

Der Zielordner enthält die ursprünglichen workspace-relativen Pfade. Die elf
von CastlePlanner-Tests direkt verwendeten Karten-, AIV- und Prüfsummendateien
bleiben zusätzlich unter `AivSeries-20260924/NaturalLinkEightResults/Assets`
sofort zugänglich. Archivierte Skripte, die mit relativen Verzeichnissen
arbeiten, werden aus einer Wiederherstellung ausgeführt. Historische absolute
Pfade in Oracle-Manifesten können auf dem aktuellen Rechner abweichen; ihr
Dateihash muss vor einem Vergleich erneut geprüft werden.

## Ergebnisüberblick

Die Zahlen verschiedener Zeilen **überlappen** teilweise. Sie dürfen nicht zu
einer globalen Erfolgsquote addiert werden. „Grau“ bedeutet eine bewusst
unterdrückte exakte Vanilla-Prognose; die getrennte geometrische Praxisfarbe
ist davon nicht erfasst.

| Messreihe | Native Fits | Exakt | Bewusst grau | Abweichung | Aussage |
| --- | ---: | ---: | ---: | ---: | --- |
| Zehn Starts: Crater Lake und Craggy Cliffs, ursprünglicher Stand | 119 | 57 | 59 | 3 | Drei Craggy-Abweichungen zeigten fehlerhafte Startzustandsrekonstruktion. |
| Dieselben zehn Starts nach konservativer Korrektur | 119 | 38 | 81 | 0 | Unbelegte Nachbarzustände werden gesperrt. |
| Vollständiger Import einschließlich zusätzlichem Crater-Lauf | 129 | 47 | 82 | 0 | Der Zusatzlauf zählt nicht zur Zehn-Match-Serie. |
| Acht Starts mit mehreren Default-AIVs | 114 | 52 | 62 | 0 | Varianten und zufallsabhängige Auswahl bleiben getrennt. |
| Possible-start-Vergleich: aktuelles Crater Lake | 54 | 36 | 18 | 0 | Mögliche Marker und Drehungen berücksichtigt. |
| Possible-start-Vergleich: aktuelles Craggy Cliffs | 102 | 18 | 84 | 0 | Nahe Starts bleiben überwiegend unbewiesen. |
| Natural-link-Serie: Crossing, vier Starts | 64 | 16 | 48 | 0 | Ein früherer Keep wurde durch einen späteren KI-Start gelöscht. |
| Natural-link-Serie: Reed Sea, vier Starts | 66 | 9 | 49 | 8 Offline-Komparator | Diese acht waren **keine** falsch farbigen Lobby-Fits; sie blieben dort grau. |
| Dynamic-link-Folgeprobe, fünf Starts einschließlich Wiederholung | 57 | 5 | 52 | 0 | Dynamisch verknüpfte Gruppe wurde tatsächlich geräumt. |
| Thasos, zwei historische Korpora | 72 | 12 | 60 | 0 | Installierte `Thasos.map` hat den erwarteten Hash der früheren Kopie. |

Die spätere Guard-Nachprüfung über teilweise überlappende Korpora meldete
310 exakte und 576 graue Native-Fits ohne Score-, Prozent- oder
Blockzellenabweichung. Die einzelnen Lauf- und Korpusberichte samt
Prüfsummen bleiben im Archiv erhalten.

## Was die Traces klärten

- **Fit und Bau:** Vanillas Raster-Fit `0x57080 -> 0x7B060` ist von den
  nachfolgenden Baupfaden getrennt. Die Höhe-12-Grenze für Burggraben und
  Zugbrücke gehört zum Bau, nicht zum Kandidaten-Score. CastlePlanner zeigt
  dazu nur einen kartenbezogenen Hinweis je projizierter Drehung.
- **Startzustand:** Serielle KI-Startgebäude werden vor der Auswahl vorbereitet.
  Keep, Lager, Goods Yard und verbundene Gebäuderecords können benachbarte
  Belegungen verändern. In Crossing verschwand nach dem zweiten KI-Start ein
  früherer 49-Zellen-Keep mit weiteren verknüpften Zellen. Der Nutzer
  reproduzierte den fehlenden Keep auch in Vanilla.
- **Marker und Rotation:** Die Aufnahme enthält auch einen 180°-Start sowie
  Serien mit vertauschter Spielerfolge und mehreren geordneten Default-AIVs.
  Der ausgewählte Startmarker und die Drehung gehören zum möglichen
  Folgezustand; eine einzelne beobachtete Zufallsauswahl ersetzt die übrigen
  Möglichkeiten nicht.
- **Abbruch und Räumung:** Ein protokollierter Fehlergrund ohne Fehlerflag ist
  kein Konstruktorabbruch. Die vollständig aufgenommenen Starttraces der
  frühen Serien zeigten den erwarteten Bau, aber keinen Beleg für alle
  möglichen Abbruchzweige. Spätere Proben belegten die Räumung dynamisch
  verbundener Records; sie begrenzen nicht alle denkbaren Folgezustände. Der
  Abgleich gegen den tatsächlich installierten Script Extender korrigierte
  dabei die Feldzuordnung: Der native Link bei `0x2A8` ist
  `r_UsedInSiegeAttemptId`, während `r_GlobalId` bei `0xD8` liegt.
- **„Completed Castles“:** Der Sofortbau verändert bei bestimmten nahen
  Craggy-Starts spätere Fits. Auf Crater Lake blieben einige beobachtete
  Paarungen gleich; daraus folgt keine allgemeine Unabhängigkeit. Der
  tatsächliche sequenzielle Bauzustand aller möglichen früheren AIV-Auswahlen
  ist weiterhin nicht vollständig rekonstruierbar.
- **Diagnosezählung:** 32 Warnungen zu Cell-Trace-Zählern waren eine
  Messsemantikfrage: Der Rasterlauf kann eine Zelle vor dem Tile-Validator
  verwerfen. Der Native-Gesamtzähler blieb der Vergleichsmaßstab.

## Anwendbare Grenze

Eine einzelne beobachtete zufällige Autoauswahl beweist keine eindeutige
Lobby-Auswahl. Der exakte Vanilla-Fit wird nur bei nachgewiesenem Eingangszustand
veröffentlicht. Für geplante Überschneidungen gibt es zusätzlich die separat
gekennzeichnete geometrische Praxisbewertung; sie simuliert keinen
vollständigen sequenziellen KI-Bau. Neue Tests sollen nur einen konkreten,
bislang unbelegten Konstruktor- oder Tile-Schreibzweig gezielt treffen.
