# TODO: Gatehouse-Distanz vom Gebäudemittelpunkt

## Beobachtung und bestätigte Ursache

Die Gatehouse-Timing-Capability funktioniert im Spiel, aber die Schließdistanz ist abhängig von der Annäherungsrichtung. Die Native-Baseline bestätigt die Ursache: Die Vanilla-Funktion misst nicht vom Mittelpunkt des Gatehouse, sondern von `GameBuilding.r_TilePositionXBegin` und `r_TilePositionYBegin`.

Maßgebliche Provenienz:

- kanonische DLL-SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Gatehouse-Funktion: RVA `0xB73D0`, Bereich `[0xB73D0, 0xB7CE5)`
- Funktions-SHA-256: `F73E9FF6F69D9EC1ECD59D528BC6D4861739F54E0A9C59C6E6BAD91369FA57C8`
- Baseline-Einstieg: `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`

Die Funktion wandelt die Begin-Koordinate mit acht nativen Einheiten pro Feld um und berechnet für eine Einheit:

    dx = abs(beginX * 8 - unitX)
    dy = abs(beginY * 8 - unitY)
    distance = max(dx, dy)

Es handelt sich damit um Chebyshev-Distanz. Die vier bereits katalogisierten Immediates ändern nur Grenzwerte und Wartezeiten; sie ändern den Bezugspunkt nicht.

## Gewünschte Mittelpunktsemantik

Für den Mittelpunkt der inklusiven Gebäude-Bounding-Box lässt sich ohne Rundungsverlust in nativen Einheiten rechnen:

    centerXNative = (beginX + endX) * 4
    centerYNative = (beginY + endY) * 4

Dadurch bleiben auch Mittelpunkte auf einem halben Feld exakt darstellbar. Die vorhandene Chebyshev-Metrik und die konfigurierten Human-/AI-Grenzwerte sollten unverändert bleiben.

Vor einer Implementierung muss noch belegt werden, dass dieser Bounding-Box-Mittelpunkt für alle Gatehouse-Typen und Ausrichtungen der fachlich richtige Bezugspunkt ist. Als Alternative sind die Mittelpunkte zwischen `GameGatehouseEntry.r_EntryDoorTilePositionX/Y` und `r_ExitDoorTilePositionX/Y` zu vergleichen. Dafür sollten Laufzeitdiagnosen mehrere kleine und große Gatehouses in allen Ausrichtungen erfassen.

## Warum `OnGatehouseQuery` allein nicht genügt

Der Script Extender vermittelt `BuildingR3EventHooks.OnGatehouseQuery` in der Einheitenschleife. Der aktuelle Inline-Hook ersetzt die Kandidatenprüfung unmittelbar vor der Distanzarithmetik. Danach führt Vanilla weiterhin seine Distanzprüfung gegen die Begin-Koordinate aus.

Ein Event-Abonnent kann deshalb einen Kandidaten ablehnen, aber ein `ShouldClose=true` umgeht die nachfolgende falsche Distanzschranke nicht. Ein Gegner, der relativ zum gewünschten Mittelpunkt nah genug, relativ zur Begin-Koordinate aber zu weit entfernt ist, bleibt weiterhin wirkungslos. Ein reiner Event-Fix ist daher nicht vollständig.

Im bestätigten Build liegt der Extender-Hook unmittelbar vor dem nativen Distanzblock. Der Distanzblock beginnt bei RVA `0xB7B70`; die Grenzwertentscheidung beginnt bei RVA `0xB7BBB`. Ein eigener Eingriff darf den Extender-Bereich nicht überlappen und muss dessen Register- und Kontrollflussvertrag erhalten.

## Empfohlene nächste Analyse

1. Den Hash der installierten DLL erneut gegen `CURRENT.json` prüfen und alle Adressen bei Abweichung verwerfen.
2. Bounding-Box- und Door-Mittelpunkte für sämtliche Gatehouse-Typen und Drehungen protokollieren und gegen die sichtbare Gebäudemitte sowie das beobachtete Schließverhalten vergleichen.
3. Die exakten Bytes, Registerwerte, Sprungziele und Live-Patches im Bereich um `0xB7B4B` bis `0xB7BBB` nach geladenem Script Extender dokumentieren.
4. Bevorzugt einen kleinen, rein nativen und hashgebundenen Ersatz ausschließlich für die Distanzarithmetik untersuchen. Er soll den Mittelpunkt berechnen, Chebyshev-Distanz liefern und vor den vorhandenen Human-/AI-Vergleichen zurückkehren. Ein Managed Callback pro Gate/Einheit wäre wegen Aufruffrequenz und Reentranz weniger attraktiv.
5. Alternativ einen Event-gestützten Ansatz nur dann verwenden, wenn formal nachgewiesen wird, dass er Kandidaten auf beiden Seiten vollständig erfasst und fremde Event-Abonnenten die Filterung nicht wieder überschreiben können.
6. Den neuen Codebereich als exklusives halboffenes Intervall im Besitzmanager führen. Aktivierung, vier Immediates und Mittelpunktpatch müssen einen konsistenten erwarteten Zustand besitzen.
7. `Enabled=false` muss sämtliche Mittelpunktbytes und alle vier Vanilla-Immediates wiederherstellen, den Prozessbesitz aber wie bisher nicht freigeben.

## Erforderliche Tests

- kleine und große Gatehouses in jeder Ausrichtung sowie Annäherung von allen Seiten und diagonal;
- ganzzahlige und halbfeldige Mittelpunkte;
- identische Chebyshev-Grenze auf gegenüberliegenden Seiten;
- getrennte Human-/AI-Distanzen und weiterhin unveränderte Wiederöffnungszeiten;
- exakte Grenzfälle knapp unter, auf und über dem nativen Vergleichswert;
- unbekannter DLL-Hash, falscher Funktionshash, veränderte Bytes und überlappender Fremdpatch jeweils fail-closed ohne Mutation;
- keine Überschneidung mit dem Script-Extender-`OnGatehouseQuery`-Hook;
- idempotente Wiederholung, externe Mutation, vollständige Transaktion, Rollback sowie kombinierte Schutz-/Cache-Flush-Fehler;
- Deaktivierung stellt nachweislich Vanilla-Bezugspunkt und Vanilla-Werte wieder her;
- APITest enthält weiterhin keine Gatehouse-RVAs, Scanner, Seitenschutzaufrufe oder eigene Detours.

Der Mittelpunkt-Fix ist mit diesem Dokument ausdrücklich noch nicht implementiert.
