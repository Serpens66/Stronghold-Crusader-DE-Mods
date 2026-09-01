# Gatehouse-Distanz vom Gebäudemittelpunkt

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

Der Bounding-Box-Mittelpunkt ist als fachlicher Bezugspunkt festgelegt. Die Door-Koordinaten beschreiben den Durchgang, nicht die Mitte des vollständigen schließenden Bauwerks. Die Laufzeitabnahme muss dennoch mehrere kleine und große Gatehouses in allen Ausrichtungen erfassen.

## Warum `OnGatehouseQuery` allein nicht genügt

Der Script Extender vermittelt `BuildingR3EventHooks.OnGatehouseQuery` in der Einheitenschleife. Der aktuelle Inline-Hook ersetzt die Kandidatenprüfung unmittelbar vor der Distanzarithmetik. Danach führt Vanilla weiterhin seine Distanzprüfung gegen die Begin-Koordinate aus.

Ein Event-Abonnent kann deshalb einen Kandidaten ablehnen, aber ein `ShouldClose=true` umgeht die nachfolgende falsche Distanzschranke nicht. Ein Gegner, der relativ zum gewünschten Mittelpunkt nah genug, relativ zur Begin-Koordinate aber zu weit entfernt ist, bleibt weiterhin wirkungslos. Ein reiner Event-Fix ist daher nicht vollständig.

Im bestätigten Build liegt der Extender-Hook unmittelbar vor dem nativen Distanzblock. Der Distanzblock beginnt bei RVA `0xB7B70`; die Grenzwertentscheidung beginnt bei RVA `0xB7BBB`. Ein eigener Eingriff darf den Extender-Bereich nicht überlappen und muss dessen Register- und Kontrollflussvertrag erhalten.

## Implementierter Ansatz

1. Der Resolver verwirft unbekannte DLL-Hashes sowie abweichende Funktions- oder Blockbytes fail-closed.
2. Der rein native Ersatz belegt exakt `[0xB7B70, 0xB7BBB)` und fällt vor den unveränderten Human-/AI-Vergleichen zurück in Vanilla.
3. Mittelpunktblock und vier Immediates werden gemeinsam exklusiv reserviert, geschrieben, verifiziert und bei einem Teilfehler vollständig zurückgerollt.
4. `Enabled=false` stellt die vier Vanilla-Timingwerte wieder her; der capabilityweite Mittelpunkt-Fix bleibt aktiv.
5. Vollständige Provenienz, Original-/Patchbytes und Registervertrag stehen in `_inspect/gatehouse-center-patch.md`.

Der erste Mittelpunktblock verursachte beim Eintritt einer Unit in die Gatehouse-Abfrage einen nativen Crash: `cdq` für `abs(dx)` zerstörte den noch für das Laden von `unitY` benötigten Unit-Offset in `RDX`. Der korrigierte 75-Byte-Block lädt beide Unit-Koordinaten vor dem ersten `cdq`; ein exakter Byte-Regressionstest sichert diese Reihenfolge ab.

## Erforderliche Tests

- kleine und große Gatehouses in jeder Ausrichtung sowie Annäherung von allen Seiten und diagonal;
- ganzzahlige und halbfeldige Mittelpunkte;
- identische Chebyshev-Grenze auf gegenüberliegenden Seiten;
- getrennte Human-/AI-Distanzen und weiterhin unveränderte Wiederöffnungszeiten;
- exakte Grenzfälle knapp unter, auf und über dem nativen Vergleichswert;
- unbekannter DLL-Hash, falscher Funktionshash, veränderte Bytes und überlappender Fremdpatch jeweils fail-closed ohne Mutation;
- keine Überschneidung mit dem Script-Extender-`OnGatehouseQuery`-Hook;
- idempotente Wiederholung, externe Mutation, vollständige Transaktion, Rollback sowie kombinierte Schutz-/Cache-Flush-Fehler;
- Deaktivierung stellt nachweislich die vier Vanilla-Timingwerte wieder her und behält den Mittelpunkt-Fix bei;
- APITest enthält weiterhin keine Gatehouse-RVAs, Scanner, Seitenschutzaufrufe oder eigene Detours.
- beide Unit-Koordinaten werden vor der ersten überschreibenden Verwendung von `RDX` geladen.

Der RDX-Liveness-Hotfix ist implementiert und automatisiert geprüft. Die aufgeführten Laufzeittests sind vor finaler Versionierung weiterhin offen.
