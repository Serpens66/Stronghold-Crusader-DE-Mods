# Dynamic-Link-Folgeprobe mit Script Extender 2.10.1

Die vier geplanten Starts `DL-forward-off/on` und `DL-reverse-off/on`
wurden vom Testmod bestätigt (`nextIndex: 4`). Ein fünfter Start nach
Serienende wiederholte `DL-reverse-on` und wird getrennt gezählt. Der
archivierte BepInEx-Abschnitt `Log_126.log` hat SHA-256
`BD388CC1D9831C4C7EC147AC31BA038538200461658468E7C9FDE2327D8B440D`.
Die native DLL hat weiterhin SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`;
die installierte `SHCDESE.dll` 2.10.1 hat SHA-256
`85591256082C6F2329EDC0BFB0C1C163D9CF1BF2991DC8B11F6790907B60F2F2`.
Die Karte und die vier verwendeten Vanilla-AIVJSON-Dateien liegen zusammen
mit den Rohtraces und ihren Hashes in diesem Ordner.

| Lauf | Native Fits | Exakt | Grau | Keep-Starts | Sofortbau-Traces |
| --- | ---: | ---: | ---: | ---: | ---: |
| `DL-forward-off` | 10 | 1 | 9 | 5 | 0 |
| `DL-forward-on` | 13 | 1 | 12 | 5 | 1 |
| `DL-reverse-off` | 8 | 1 | 7 | 5 | 0 |
| `DL-reverse-on` | 13 | 1 | 12 | 5 | 2 |
| Wiederholung von `DL-reverse-on` | 13 | 1 | 12 | 5 | 2 |
| **Summe** | **57** | **5** | **52** | **25** | **5** |

`OracleCorpus/` und `oracle-report.json` sind mit dem aktuellen Offline-Kern
erzeugt. Score, Prozentwert, Zellzahl und blockierte Zellen stimmen in allen
fünf freigegebenen Fällen exakt; es gibt keinen Mismatch und keinen
Verarbeitungsfehler. Die einzelnen Native-Zellen wurden zusätzlich mit
`OfflineCellDiagnostics/` verglichen: 10.660 ausgewertete Zellen, null
Unterschiede bei Kartenkoordinate oder Blockstatus. Die 52 grauen Fälle
zerfallen in 16 wegen möglicher Start-Räumung und 36 nach vorherigem
Sofortbau. `cell-review.csv` nennt für jeden Fall den ersten grauen
Sperrgrund und, wo anwendbar, die erste Zelle, die ein vorheriger
Sofortbau tatsächlich geändert und der spätere Native-Fit gelesen hat.
In sämtlichen 36 Sofortbau-Fällen gibt es eine solche Schnittmenge;
die graue Grenze ist hier nicht bloß eine theoretische Vorsicht.

## Verbundene dynamische Gruppe tatsächlich geräumt

Im vierten Lauf baute Spieler 3 (`Sentinel Default 2`) im Sofortbau-Frame 53
mit Mapper 87 eine vierteilige Gruppe. Die Record-IDs 62, 63, 65 und 340
hatten danach denselben nativen Cleanup-Linkwert `903`. Es waren die
Typen 9, 57, 58 und 56 mit je 5×5 Zellen bei `(265,420)`, `(270,420)`,
`(265,425)` und `(270,425)`. Der spätere Keep-Start von Spieler 5 bei
`(258,417)` räumte **alle 100 Zellen** der Gruppe: 12 wurden vom neuen
Start überbaut, 88 wurden frei. Der zusätzliche fünfte Kartenstart
wiederholte exakt dieselben Record-IDs, Positionen und Zellzahlen.
Alle Starttraces sind vollständig; das native Fehlerflag blieb null.

Der installierte Native-Code erklärt den Befund: `0x5D3A0` markiert über
`0xC4290` einen getroffenen Gebäuderecord. `0xC4290` markiert zusätzlich
alle lebenden Records mit demselben nichtnullen Feld bei Manager-Offset
`+0x304` (`GameBuilding+0x2A8`). Anschließend entfernt `0x5D3A0` alle
markierten Records über `0xB8310`. Daher kann eine Start-Räumung Zellen
außerhalb des unmittelbar berührten Gebäudes ändern. Die beobachtete
Gruppe war **zur Laufzeit** durch Sofortbau entstanden; sie war nicht
Bestandteil der serialisierten `.map`.

Die vier Record-IDs wurden im Starttrace als vor dem Aufruf lebend und
danach von neuen Startrecords wiederverwendet protokolliert. Die
Zell-Diffs belegen ihre vollständige Entfernung; aus dem bloßen
Record-Zähler `removed=0` hätte man diese Folge übersehen. Das ist ein
Beleg für den ausgeführten Zweig, nicht für die vollständige
Rekonstruktion beliebiger früherer AIV-Bauzustände.

## Offene Grenze

`0xB8310` durchläuft vor und nach der fitrelevanten Tile-Räumung in
`0x61FC0` weitere Routinen. Deren direkte Datenflüsse wurden erneut
geprüft: `0xB8460` passt Besitzervorräte an, `0x1977A0` bearbeitet
eine zugeordnete Einheit, `0xB5C40` berührt Pfad-/Darstellungsdaten
und `0xCFE90` die Besitzer-Gebäudeliste. Die transitiven Folgen der
Typzweige von `0x61FC0` sowie die Abbruchzweige von `0x77E60` sind
noch keine allgemeine räumliche Schreibgrenze. Keine der 25
Startkonstruktionen setzte ein Fehlerflag. Es wird deshalb keine
zusätzliche späteren KI-Prognose freigegeben, nur weil ein einzelner
beobachteter Ablauf reproduzierbar war.

`Analyze-Traces.ps1` reproduziert den Zellabgleich und die
Sofortbau-/Native-Lesemengen-Schnittmengen aus den archivierten Daten.
