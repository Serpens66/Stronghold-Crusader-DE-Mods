# AIV-Diagnoseserie vom 24.09.2026

## Provenienz und Umfang

- Installierte `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Crater Lake: Map-SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
- Craggy Cliffs: Map-SHA-256 `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
- Die zehn vom Testmod bestätigten Starts sind `CL-A-off/on`, `CL-B-off/on`, `CL-Reverse-off/on`, `CC-A-off/on` und `CC-B-off/on`. Der dazwischen versehentlich erneut ohne Sofortspawn gestartete Lauf `map-load-002` auf Crater Lake ist aus dem Serienvergleich entfernt. Zwei spätere freie Starts nach Serienende gehören ebenfalls nicht dazu.
- `series-oracle.log` enthält die für den Import nötigen Zeilen der gestarteten Spielprozess-Sitzung. Die nach Karte getrennten `oracle.log`-Dateien und importierten JSON-Korpora erhalten die Map-Zuordnung. Der Importer benennt aktuell beide Karten `unknown.json`, weil das Live-Log `mapName=<unknown>` meldet; ein gemeinsames Importverzeichnis würde eine Karte überschreiben.
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

## Folgerung und nächste gezielte Prüfung

Die Crater-Lake-Paare sind beobachtete Fälle ohne Fitänderung, kein allgemeiner Unabhängigkeitsbeweis. Spätere KIs bei aktiviertem Sofortbau bleiben `NotEvaluable`. Auf Craggy Cliffs sind sogar bei deaktiviertem Sofortbau drei Offline-Ergebnisse nicht exakt; eine allgemeine Freigabe dieser rekonstruierten Startzustände wäre falsch.

Nächster Schritt ohne Spielstart: den nativen Startgebäude-Konstruktorpfad `0x94350 -> 0x6D580 -> 0x77E60` samt Platzierungs-, Abbruch- und Nebeneffektzweigen gegen die vorhandenen Live-Gebäuderaster und Cell-Traces prüfen. Die bisherige affine `13x13`-Rekonstruktion muss danach entweder korrigiert oder für betroffene Kandidaten fail-closed begrenzt werden. Anschließend die vorhandenen 119 Fälle erneut vergleichen. Erst nach einer solchen Änderung sind höchstens die beiden Craggy-Paare `CC-A` und `CC-B` als gezielte Wiederholung nötig. Eine weitere unveränderte Zehn-Match-Serie würde keine neue offene Frage beantworten. Die Spezialkarte mit sich überbauenden KIs bleibt wie vereinbart zurückgestellt.

Die vier Einträge sind in `Testmods/AivLobbyPresetTest/AivLobbyStartRebuildRegressionSeries.json` bereits als separate, **nicht aktive** Serie mit eigener `seriesId` vorbereitet. Vor der Korrektur wird sie nicht in die installierte Konfiguration übernommen.
