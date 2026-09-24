# Craggy Cliffs: verbundene Gebäuderecords beim KI-Start

Native-Basis: `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Karte: `Craggy Cliffs.map` SHA-256
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.

## Konkrete Frage

Der erfolgreiche Start bei `0x6D580 -> 0x77E60 -> 0x74DA0` kann über
`0x5D3A0 -> 0xC4290 -> 0xB8310` bestehende und verbundene Gebäude
entfernen. Die archivierten Aufnahmen von `CC-A-on` und `CC-B-on` zeigen
gelöschte Gebäudezellen, aber keine vor/nach dem Keep-Start verglichenen
Gebäuderecords. Die neue Detector-Aufnahme misst pro Start alle 320.800
Tile-IDs und ausgewählte Felder aller Gebäuderecords. Sie soll zeigen,
welche Record-IDs und Global-IDs ihren Status ändern
und ob deren Zellen außerhalb der Keep-/Lager-/Yard-Flächen liegen.

## Testserie

Die Datei `Testmods/AivLobbyPresetTest/AivLobbyConnectedRecordProbeSeries.json`
enthält exakt zwei Läufe mit bereits geprüfter sieben-KI-Aufstellung:

1. `CC-A-on`: Craggy Cliffs, erste feste Default-AIV-Varianten,
   „Completed Castles“ an. Im alten Trace wurden bei einem KI-Start
   60 vorhandene Gebäudezellen gelöscht und acht ersetzt.
2. `CC-B-on`: dieselbe Karte mit alternativen Default-AIV-Varianten,
   „Completed Castles“ an. Im alten Trace wurden bei einem KI-Start
   16 vorhandene Gebäudezellen gelöscht.

Beide Presets werden unverändert aus
`AivLobbyStartRebuildRegressionSeries.json` übernommen. Die Testserie
steuert Karte, Spieler, Keep-Slots, AIV-Auswahl und Sofortspawn; weder der
Detector noch die spätere Auswertung setzen feste Spieler-IDs voraus.
Der bisher installierte Serienstand ist als `Previous-*` hier gesichert.

Pro Lauf eine neue lokale Skirmish-Lobby öffnen, die automatisch gesetzte
Aufstellung unverändert lassen und die Karte bis zum sichtbaren Spielbeginn
starten. Regulären späteren KI-Bau nicht abwarten. Nach Lauf 2 ist die
Serie abgeschlossen; den Spielprozess danach beenden und die Auswertung
anfordern. Fehlstarts oder manuelle Änderungen dürfen den Fortschritt
nicht verschieben.

## Auswertung und Grenzen

Jede neue `StartTraces/oracle-start-trace-*.tsv` verweist auf eine
gleichnamige `.records.tsv` mit einer vor/nach-Zeile pro geändertem
1-basiertem Building-ID-Slot. Vor einer Regeländerung werden die Logs und
Rohtraces in den Workspace kopiert und mit Map-, Native- und AIV-Hashes
archiviert. Record-Änderungen werden mit den Tile-Diffs und dem
`postNativeFailureFlag` verbunden. Die Aufnahme misst ausgeführte
Vanilla-Zweige; sie beweist allein noch keine universelle Reichweite aller
möglichen Starts. Falls kein fehlgeschlagener Start auftritt, bleibt der
Abbruchzweig separat offen.

Die Serie ist abgeschlossen. Die Auswertung liegt in
`../ConnectedRecordProbeResults/RESULTS.md`. Der nachträgliche Native-Audit
zeigt, dass die Verbindung für die Record-Räumung über Struct-Offset
`0x2A8` und nicht über die hier mitgespeicherte `r_GlobalId` läuft.
