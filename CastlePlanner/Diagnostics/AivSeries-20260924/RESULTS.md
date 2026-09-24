# AIV-Diagnoseserie vom 24.09.2026

## Provenienz und Umfang

- Installierte `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Crater Lake: Map-SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
- Craggy Cliffs: Map-SHA-256 `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
- Die zehn vom Testmod bestätigten Starts sind `CL-A-off/on`, `CL-B-off/on`, `CL-Reverse-off/on`, `CC-A-off/on` und `CC-B-off/on`. Der dazwischen versehentlich erneut ohne Sofortspawn gestartete Lauf `map-load-002` auf Crater Lake ist aus dem Serienvergleich entfernt. Zwei spätere freie Starts nach Serienende gehören ebenfalls nicht dazu.
- `series-oracle.log` enthält die für den Import nötigen Zeilen der gestarteten Spielprozess-Sitzung. Die nach Karte getrennten `oracle.log`-Dateien und importierten JSON-Korpora erhalten die Map-Zuordnung. Der Importer nutzt bei `mapName=<unknown>` nun den tatsächlichen Map-Dateinamen und den Map-Hash im Ausgabedateinamen; der gemeinsame Import beider Karten liegt unter `SixMatchResults/CombinedCorpus`.
- Die 34 relevanten Bau-Traces sind zusätzlich in `prebuild-traces.zip` archiviert (SHA-256 `D4591C8FD3DB5CAC3774BD0A504225CAC621B2CD7921E722689FB64CFE82F460`). `cell-and-live-grids.zip` enthält 218 Cell-Traces und native Gebäuderaster aus den betreffenden Minuten (SHA-256 `9E2254715FE1800FE49644A6AF9B274A071BC2286CBCF2B8103162928ECC8ECB`). Die Dateiköpfe tragen Map-/Native-/AIV-Hash und Aufnahmezeit.

## Offline-Oracle-Vergleich

| Karte | Native Versuche der zehn bestätigten Starts | Exakt | Absichtlich `NotEvaluable` | Abweichung | Fehler |
| --- | ---: | ---: | ---: | ---: | ---: |
| Crater Lake | 62 | 34 | 28 | 0 | 0 |
| Craggy Cliffs | 57 | 23 | 31 | 3 | 0 |
| Gesamt | 119 | 57 | 59 | 3 | 0 |

Auf Crater Lake stammen alle 28 nicht auswertbaren Versuche von späteren KI-Spielern nach Sofortbau. Die 34 auswertbaren Fälle stimmen einschließlich Status, Rohscore, Prozentwert, geprüfter und blockierter Zellen. Die drei Craggy-Abweichungen liegen **ohne** Sofortbau:

| Lauf, Spieler und AIV | Drehung | Native / Offline | Unterschied |
| --- | ---: | --- | --- |
| `CC-A-off`, Spieler 5, Emir `Default 1` | 0° | Partial, Score 67, 96 %, 84 / 86 blockierte Zellen | Zwei zusätzliche Offline-Sperren |
| `CC-A-off`, Spieler 5, Emir `Default 1` | 180° | Partial, Score 95, 98 %, 29 / 31 blockierte Zellen | Zwei zusätzliche Offline-Sperren |
| `CC-B-off`, Spieler 7, Jewel `Default 1` | 270° | Partial, Score 3, 95 % / 94 %, 106 / 117 blockierte Zellen | Prozentwert und elf Zellentscheidungen |

Die jeweiligen `report.json` beziehungsweise `series-only.report.json` enthalten alle Einzelresultate. Für die drei Abweichungen liegen zusätzlich element- und zellweise Offline-Diagnosen vor. Die nativen Cell-Traces melden bei mehreren Teilfits eine Abweichung zwischen ihren nachträglich gezählten Validator-Sperren und dem nativen Gesamtzähler. Der **native Oracle-Gesamtzähler** bleibt Vergleichsmaßstab; aus einem Cell-Trace-Boolean allein wird keine vermeintlich exakte Zellursache abgeleitet.

Die Live-Gebäuderaster grenzen einen Fehler in der Startzustandsrekonstruktion ein: Vor Emirs 0°-Fit enthält das Offline-Modell an `(383,530)` und `(384,530)` jeweils Gebäude-ID 32, während Vanillas unmittelbar vor dem Fit aufgenommenes Gebäuderaster dort keine Belegung hat. Beim 180°-Fit betrifft dieselbe Richtung die modellierten Zellen `(389,519)` und `(389,521)` (IDs 34/36, nativ unbelegt). Vor Jewels 270°-Fit stimmen auf 20 von dessen betroffenen Kandidatenzellen modellierte und native Gebäude-IDs nicht überein, beispielsweise modelliert `(490..496,403)` ID 50, nativ unbelegt. Das ist direkte Evidenz für eine ungenaue Rekonstruktion der früheren Startgebäude; die anderen Tile-Schichten und die gesamte Differenz der Jewel-Prozentzahl sind dadurch noch nicht vollständig erklärt. Das Modell darf für solche Überlappungen nicht als exakt gelten.

## Wirkung von „Completed Castles“

| Paar | Gemeinsame Spieler/AIV-Hash/Drehungs-Versuche | Native Fitänderungen | Vollständige Bau-Traces | Spätere Fit-Traces mit Schnitt zur vorherigen Tile-Änderung |
| --- | ---: | ---: | ---: | ---: |
| `CL-A` aus/an | 10 | 0 | 7/7 | 0/9 |
| `CL-B` aus/an | 11 | 0 | 7/7 | 0/10 |
| `CL-Reverse` aus/an | 10 | 0 | 7/7 | 0/9 |
| `CC-A` aus/an | 13 | 5 | 6/6 | 8/15 |
| `CC-B` aus/an | 11 | 6 | 7/7 | 12/16 |

Alle 34 erfassten Bau-Sequenzen melden `frameSnapshotsComplete=True`, `provenanceComplete=True` sowie null Pointer- und Aufnahmefehler. `CC-A-on` hat sechs statt sieben Bau-Traces, weil Emir `Default 1` nativ in allen vier Drehungen abgelehnt wurde (`placementState=0`); der Detector meldet entsprechend sechs bestätigte AIV-Auswahlen. Das ist keine verlorene Aufnahme. In `CC-A` wechseln Emirs vier gemeinsame Versuche gegenüber ohne Sofortbau von Partial zu Rejected; Jewel `Default 5` verliert einen vollständigen Fit. In `CC-B` verlieren Emir `Default 8`, Jewel `Default 1` und Nomade `Default 7` beobachtete Fitqualität. Diese Unterschiede zeigen, dass frühere Bauten auf Craggy Cliffs spätere Kandidaten tatsächlich beeinflussen. Die bloße Schnittmenge von Tile-IDs beweist für sich allein noch nicht die Ursache jeder einzelnen Fitentscheidung.

## Folgerung vor der nachfolgenden Startzustandsprüfung

Die Crater-Lake-Paare sind beobachtete Fälle ohne Fitänderung, kein allgemeiner Unabhängigkeitsbeweis. Spätere KIs bei aktiviertem Sofortbau bleiben `NotEvaluable`. Auf Craggy Cliffs sind sogar bei deaktiviertem Sofortbau drei Offline-Ergebnisse nicht exakt; eine allgemeine Freigabe dieser rekonstruierten Startzustände wäre falsch.

Nächster Schritt ohne Spielstart: den nativen Startgebäude-Konstruktorpfad `0x94350 -> 0x6D580 -> 0x77E60` samt Platzierungs-, Abbruch- und Nebeneffektzweigen gegen die vorhandenen Live-Gebäuderaster und Cell-Traces prüfen. Die bisherige affine `13x13`-Rekonstruktion muss danach entweder korrigiert oder für betroffene Kandidaten fail-closed begrenzt werden. Anschließend die vorhandenen 119 Fälle erneut vergleichen. Erst nach einer solchen Änderung sind höchstens die beiden Craggy-Paare `CC-A` und `CC-B` als gezielte Wiederholung nötig. Eine weitere unveränderte Zehn-Match-Serie würde keine neue offene Frage beantworten. Die Spezialkarte mit sich überbauenden KIs bleibt wie vereinbart zurückgestellt.

Die vier Einträge sind in `Testmods/AivLobbyPresetTest/AivLobbyStartRebuildRegressionSeries.json` als separate Serie mit eigener `seriesId` vorbereitet. Nach der unten dokumentierten Korrektur wurde sie am 24.09.2026 in die installierte Testkonfiguration übernommen; Fortschrittsindex 0 zeigt auf `CC-A-off`.

## Nachprüfung des abgesicherten Startzustands (24.09.2026)

Nach Korrektur der durch Live-Raster belegten 0- und 270-Grad-Offsets
und der Schutzgrenze für ungeklärte native Startkonstruktion wurden die
beiden archivierten Korpora erneut verglichen. Für die festgelegte
Zehn-Match-Serie (119 Versuche, Crater `map-load-002` weiterhin
ausgenommen) ergeben sich **38 exakte Fits, 81 `NotEvaluable`, null
Abweichungen und null Auswertungsfehler**. Der vollständige Import mit
dem aus der Serie ausgeschlossenen Crater-Lake-Lauf enthält 129 Fälle:
47 exakt, 82 `NotEvaluable`, null Abweichungen und null Fehler.

Die alte Craggy-Abweichung von Jewel `Default 1` bei 270 Grad wird
wegen Lesezellen nahe einem früheren KI-Start nicht mehr als sicherer
Score ausgewiesen. Emirs zwei alte Abweichungen verschwanden bereits
durch den korrigierten 0-Grad-Offset; nahe Startinteraktionen bleiben
im Produkt trotzdem gesperrt, solange die Konstruktor- und Abbruchzweige
nicht vollständig belegt sind. Die neuen maschinenlesbaren Vergleichsberichte
liegen in `.inspect/oracle-crater-safe.json` und
`.inspect/oracle-craggy-safe.json`. Das ist eine bewusste Verringerung
der farbigen Ergebnisse, keine neue Behauptung vollständiger Vanilla-Gleichheit.

## Native-Nachprüfung des KI-Startbaus

Der Mapper `0x3d` des KI-Starts wird in der installierten DLL
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
zu Typ 41 aufgelöst. Nach erfolgreichem Validator `0x77E60` erstellt
`0x74DA0` einen zusammengesetzten Keep-Komplex: 7x7-Keep, drei
Einzelzellen, 7x7-Startlager und optional den Goods Yard. Die festen
Rotations-Offsets und die Fixes-Eingriffsstelle sind in der
Native-Baseline und im CastlePlanner-Forschungsstand dokumentiert.
Der Validator hängt außerdem von lebenden Einheiten, anderen Spielern
und Erreichbarkeit ab. Die vorhandenen Zell- und Bau-Traces enthalten
keinen vollständigen Vorher/Nachher-Snapshot **dieses** Validators und
keinen allgemeinen Beleg seiner Abbruchzweige. Die 38 exakten
Vergleiche bleiben gültig; die 81 grauen Ergebnisse werden dadurch
noch nicht freigegeben. Ein unveränderter weiterer Kartenstart würde
diese Lücke nicht schließen.

## Gezielte Craggy-Wiederholung mit Startbau-Aufnahme

Die vier Läufe `CC-A-off/on` und `CC-B-off/on` sind abgeschlossen;
der Testserien-Fortschritt steht auf 4/4. Der neue Detector erfasste
32 vollständige Keep-Start-Traces ohne Aufnahmefehler. Alle 57
erneuten nativen Fitversuche reproduzieren die archivierten Werte
für Rohscore, Prozent und blockierte Zellen exakt. Archiv:
`StartRebuildRegression/start-traces.zip` mit 32 Rohdateien,
`SHA256SUMS.txt` und `runtime-oracle.log`. ZIP-SHA-256:
`C71156A44357ACC4A4CF99127E28D4BEB2726955D9D620F346BA0526D1BEA0CA`.

In allen 32 Starts war das native Fehlerflag null; 24 dennoch
ungleich null gemeldete Fehlergründe sind ohne Flag nicht als Abbruch
verwendbar. Die normalen Starts änderten je 117 Gebäude-ID-Zellen.
`CC-A-on`/Emir überschrieb zusätzlich vorhandene Bauten (60 geräumte,
acht ersetzte Gebäudezellen); `CC-B-on`/Jewel räumte 16. Alle
in den **41×41-Zellen-Ausschnitten** beobachteten Fit-Schichtänderungen
blieben innerhalb von 22 Zellen des Map-Keeps. Zellen außerhalb des
Ausschnitts wurden damals nicht gelesen; aus diesen Traces folgt daher
auch für die vier konkreten Läufe keine Vollkarten-Obergrenze.

Der Native-Pfad kann bestehende Gebäuderecords entfernen und
Pfad-/Tile-Helfer aufrufen; vier erfolgreiche Starts beweisen dafür
keine allgemeine räumliche Obergrenze. Ebenso fehlt ein beobachteter
Startabbruch. Der Produkt-Schutzbereich wird durch diese Daten
deshalb nicht verkleinert. Weitere farbige Fits nach verschobenem
Startmarker oder Sofortbau wären derzeit unbewiesen.

Nach der Diagnosekorrektur wurden 31/31 AIVPlacement- und 86/86
CastlePlanner-AIV-Tests bestanden. Der erneute Offline-Oracle-Vergleich
ergab für die ausgewählte Serie 119 Fälle mit 38 exakt, 81
`NotEvaluable`, null Abweichungen und null Fehler; mit dem zusätzlichen
Crater-Lauf 129 Fälle mit 47/82/0/0. Die Berichte liegen unter
`.inspect/oracle-craggy-regression-20260924.json`,
`.inspect/oracle-crater-series-regression-20260924.json` und
`.inspect/oracle-crater-full-regression-20260924.json`. Der neu gebaute
Detector wurde installiert; lokale und installierte DLL haben den
damaligen SHA-256 `1EF9BD7ADBDBF57A7EBC1EA333FF14B3C233ADA4DBA19AF8F022F15A4A33D324`.

## Vollkarten-Nachmessung vorbereitet (24.09.2026)

Der Detector erfasst nun vor und nach jedem KI-Keep-Start alle 320.800
Tile-IDs der acht Fit-Schichten und die nativen Räum-/Record-Markierungen.
Der neue lokale und installierte DLL-SHA-256 ist
`B0BF470988860B8414BC249AC8E4B583AAA81F71B1EC73B5DF253422B9D5EFA7`.
Ein einziger automatischer `CC-A-on`-Lauf ist in der lokalen Testmod-Serie
vorbereitet (`nextIndex=0`). Er soll prüfen, ob der vorher nicht gemessene
Außenbereich geändert wurde, und den tatsächlich genommenen
Überschneidungszweig protokollieren. Ein Ergebnis dieses neuen Laufs
liegt noch nicht vor. Das Auswerteskript
`Analyze-FullGridStartTrace.ps1` validiert Vollständigkeitsmarker,
Hashes, Änderungszähler, frühere Fenstergrenze und Scan-Zeiten.

Der Lauf wurde bestätigt (`nextIndex=1`). Acht vollständige Keep-Traces
hatten null Änderungen außerhalb des alten 41×41-Fensters und null
abweichende Änderungszeilen gegenüber dem früheren `CC-A-on`-Lauf.
Alle acht nativen Startaufrufe führten den Räumzweig mit Fehlerflag null
aus. Die sechs Sofortbau-Traces stimmen in Gebäude- und
Fit-Schichtänderungen framegenau mit dem ursprünglichen `CC-A-on` aus
der Zehn-Match-Serie überein. Bei 8 von 16 späteren Fit-Versuchen las
der Validator zuvor geänderte Sofortbau-Zellen; bei einem davon auch
geänderte Keep-Start-Zellen. Die anderen acht sind nur für den gemessenen
Ablauf disjunkt. Der erneute Offline-Vergleich ergab 1 exakt,
15 `NotEvaluable`, null Abweichungen und null Fehler.

Archiv: `FullGridProbeResults/start-traces.zip` (8 Dateien, SHA-256
`FD2676EF570A9721E4AB62739D816542328FD51F4CDAD680BC1C99D90F61657E`),
`prebuild-traces.zip` (6, `BDCDD38CE2CF025982C31BF27CD084123ECBEA31D2B91753131597B630E4302E`),
`cell-and-live-grids.zip` (32,
`CF64F7F3A60EA656D9D2603DA0BFBE735FF5729002F7055B59784C085AD08ACD`),
`runtime-log.log`, `full-grid-summary.json`, `read-intersections.json`
und `report.json`. Die automatische Ein-Match-Serie ist abgeschlossen.
Eine allgemeine Änderung des CastlePlanner-Fits folgt daraus noch nicht:
Die nativen Räum-/Gebäudeketten sind für andere Mapper und Startabbrüche
nicht räumlich bewiesen.

## Nächster gezielter Lauf: Crater Lake 180°

Die letzte zwischenzeitliche Spielrunde betraf andere Mods; AIVPlacement
war dabei nicht installiert und sie liefert keinen AIV-Beleg. Nach dem
Spielende wurden `ActiveAIVDetector_Serp` und `AivLobbyPresetTest_Serp`
erneut mit ihren `build.bat` installiert. Ihre installierten DLL-Hashes
sind `B0BF470988860B8414BC249AC8E4B583AAA81F71B1EC73B5DF253422B9D5EFA7`
beziehungsweise `13F88C6ED3D3BF02FBC65378E9E8D9DE48B96BA96613E26D4F718471EC02D4B2`.
Das eigenständige AIVPlacement-Plugin ist nicht erforderlich, da
CastlePlanner den gemeinsamen Kern lädt.

Die einmalige Serie `aiv-full-grid-crater-180-20260924` ist mit
`CL-A-on`, `nextIndex=0` aktiviert. Quelle:
`Testmods/AivLobbyPresetTest/AivLobbyCrater180ProbeSeries.json`;
die vorige Konfiguration liegt unter `Crater180ProbeSetup`. Der Map-Hash
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`
stimmt mit der installierten `Crater Lake.map` überein. Erst der neue
Spielstart kann zeigen, ob der 180°-Konstruktorzweig dieselbe beobachtete
Schreibgrenze einhält. Bis dahin erfolgt keine zusätzliche Freigabe
späterer Fits.

Die geplante Einzelmessung wurde vor ihrem Start zu einer Sechs-Match-Serie
erweitert. Die Quelle `Testmods/AivLobbyPresetTest/AivLobbySixMatchSeries.json`
enthält `CL-A-off`, `CL-A-on`, `CL-B-on`, `CL-Reverse-on`, `CC-B-off` und
`CC-B-on`. Der vorhandene Vollkarten-Trace `CC-A-on` wird wiederverwendet.
Die bisher installierte Ein-Match-Serie samt Index 0 wurde unter
`SixMatchSetup` gesichert. Die neue Serie ist mit Index 0 aktiviert und
beginnt bei `CL-A-off`; neue Native-Erkenntnisse werden erst nach
bestätigten Starts ausgewertet.

## Sechs-Match-Serie: Aufnahme und Auswertung

Alle sechs Matches wurden in der vorgesehenen Reihenfolge bestätigt;
`nextIndex=6`, letzter Lauf `CC-B-on`. Die Rohdaten samt vollständigem
Spiel-Log, Konfiguration und Hashmanifest liegen in `SixMatchResults`.
Es gibt 48 vollständige Vollkarten-KI-Starttraces (acht je Lauf) und
28 vollständige Sofortbau-Traces (sieben in jedem der vier `on`-Läufe).
Alle Startaufrufe melden Fehlerflag null, und keine geänderte
Fit-Schichtzelle liegt außerhalb des früheren 41×41-Messfensters.
Das ist keine allgemeine Schreibbereichsgarantie.

Acht Starttraces mit 180° und kanonischem AIV-Marker `(56,43)` wurden
mit den 117 serialisierten Gebäudezellen des jeweiligen Startkomplexes
aus der geprüften `.map` verknüpft. Der Native-Nachher-Zustand stimmt
jeweils zellgenau mit dem Drehpivot 13 überein; mit dem bisherigen
Pivot 12 fehlen jeweils 31 Zellen. Nichtkanonische Marker zeigen
Verschiebungen des nativen Keep-Ankers und bleiben für nachfolgende
KI-Fits gesperrt. Die Codekorrektur betrifft nur die 180°-Rekonstruktion.

Der neue Oracle-Korpus enthält 41 Crater- und 28 Craggy-Versuche.
Nach der Pivot-Korrektur: **19 exakt, 50 `NotEvaluable`, null Abweichungen,
null Fehler**. Die Berichte stehen in `SixMatchResults/CraterCorpus` und
`SixMatchResults/CraggyCorpus`. Der Importer verwendet bei unbekanntem
Map-Namen künftig den tatsächlichen Dateinamen und einen Hash-Suffix;
der gemeinsame Import erzeugte zwei getrennte Dateien in
`SixMatchResults/CombinedCorpus`.

Die beiden älteren Archive wurden mit demselben korrigierten Kern erneut
verglichen: 129 Versuche, davon 47 exakt und 82 `NotEvaluable`, ohne
Abweichung oder Verarbeitungsfehler. Zusammen mit den 69 neuen Versuchen
sind das 198 geprüfte Fälle. AIVPlacement `build.bat` bestand 31/31
synthetische Tests. CastlePlanner `build.bat` bestand 86/86 Tests und
installierte das Plugin nach dem Build. Die lokalen und installierten
SHA-256-Hashes stimmen für `CastlePlanner.dll`, `AIVPlacement.Core.dll`
und `CastlePlanner.AIVPlacement.Core.dll` überein. Die neue Laufzeitversion
wurde noch nicht durch einen weiteren Spielstart bestätigt.

Für die Laufzeitprüfung sind zwei gezielte Starts vorbereitet:
`CL-A-off`, danach `CL-A-on`, mit identischer Karte, Aufstellung und
AIV-Wahl wie in der bestätigten Sechs-Match-Serie. Der neue Serienstand
`aiv-pivot13-runtime-regression-20260924` steht auf Index 0; die
vorherige Konfiguration und ihr abgeschlossener Fortschritt liegen unter
`Pivot13RuntimeRegression`. `ActiveAIVDetector_Serp` und
`AivLobbyPresetTest_Serp` wurden nach ihrem zwischenzeitlichen Fehlen
mit den vorgesehenen Buildtreibern wieder installiert und ihre DLL-Hashes
gegen die lokalen Pakete geprüft. Hier soll nur die installierte
CastlePlanner-Version und der Umschaltpfad `Completed Castles` geprüft
werden; die 180°-Native-Zellen selbst sind bereits zellweise belegt.

Beim ersten Öffnen der Lobby nach der Vorbereitung griff der Testmod
nicht ein. Das Log belegt, dass er geladen war und sein langlebiger
Lobby-Callback lief; `TestSeriesProgress.Read` lehnte allein den von
mir falsch gesetzten Wert `lastCompletedRunId: null` ab. Für
`nextIndex: 0` verlangt der Testmod `lastCompletedRunId: ""`.
Die installierte Fortschrittsdatei wurde darauf korrigiert. Serien-ID,
Reihenfolge `CL-A-off`/`CL-A-on` und Index 0 sind unverändert; JSON und
CRLF wurden geprüft. Ein bestätigter Kartenstart hat noch nicht
stattgefunden. Beim nächsten Lobby-Öffnen muss das Log
`Test series run 1/2: CL-A-off` statt `Preset invalid` melden.
Der folgende Lobby-Start um 16:06 Uhr bestätigte genau diesen Marker,
den geprüften Crater-Lake-Hash, alle acht Spieler-/Keep-/AIV-Zuordnungen
und `Test series preset applied: CL-A-off, Completed Castles=0`.
Der Fortschritt darf erst nach einem bestätigten Kartenstart steigen.

## Laufzeitregression der installierten Korrektur

Die zwei Starts `CL-A-off` und `CL-A-on` wurden bestätigt; der
Serienfortschritt steht auf `nextIndex=2`. Die Aufnahmen vom 24.09.2026
um 16:10/16:11 Uhr liegen unter `Pivot13RuntimeRegression/Observed`:
16 vollständige native Starttraces (acht je Match), sieben vollständige
Sofortbau-Traces im `on`-Match und 40 Cell-Traces. Alle Starttraces
melden Fehlerflag null, vollständigen Vollkartenscan und keine Änderung
außerhalb des früheren 41×41-Fensters. Beide Läufe nutzen den geprüften
Crater-Lake-Hash `C5D9906A…` und Native-Hash `FBCB9319…`.

Der Oracle-Import enthält 20 native Fit-Versuche. Mit dem installierten
Offline-Kern ergeben sich **10 exakte Vergleiche, 10 `NotEvaluable`,
null Abweichungen und null Verarbeitungsfehler**. Ohne Sofortbau sind
neun Fälle exakt; der letzte bleibt nach Jewels verschobenem Startmarker
grau. Mit Sofortbau ist der erste KI-Fit exakt; alle neun späteren
Versuche bleiben wegen des unbekannten sequenziellen Bauzustands grau.
CastlePlanner protokollierte in der Lobby dieselbe Grenze. Die
180°-Varianten von Marshal und Emir stimmen in der neuen Aufnahme mit
Vanillas Score überein. Diese Wiederholung belegt den installierten
Laufzeitpfad, nicht allgemein die offenen Konstruktor- und Sofortbauzweige.
