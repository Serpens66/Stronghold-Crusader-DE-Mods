# Offline-Guard-Prüfung nach Script Extender 2.10.1

Native-DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installierter Script Extender 2.10.1 SHA-256:
`85591256082C6F2329EDC0BFB0C1C163D9CF1BF2991DC8B11F6790907B60F2F2`.

Der native Startpfad `0x94350` räumt serialisierte Startrecords in einer
ersten Spielerschleife mit `0xC43A0` ab. Erst die zweite Schleife wählt
KI-AIVs und baut neue Starts. `AivPreplacementMapState` entfernt dieselben
serialisierten Records aus dem Offline-Fit-Raster. Der Kollisions-Guard
überspringt nun nur die bereits entfernten **source**-IDs des eindeutig
gefundenen Start-Keeps und seiner nichtnullen Cleanup-Linkgruppe. Die
bisherige Ausnahme für die eigenen transformierten Zellen bleibt bestehen.
Der Guard prüft weiterhin
erhaltene Gebäude und tatsächlich rekonstruierte **rebuilt**-Zellen weiter.
Die Kandidatenprüfung um mögliche native Bauwirkungen bleibt unverändert.

Im dichten Korpus wechselte bei elf Fällen lediglich der genaue graue
Sperrgrund: Acht nennen nun die kandidatennahe Bauunsicherheit, drei einen
anderen neu gebauten Startrecord. **Kein zusätzlicher Fit erhielt eine Farbe.**
Alle 74 Fälle bleiben bei sechs exakten und 68 nicht auswertbaren Ergebnissen,
ohne Abweichung.

Die 16 Berichte in diesem Ordner vergleichen weitere 886 archivierte
Native-Fits: 310 exakt, 576 `NotEvaluable`, null Score-/Prozent-/Blocked-Cell-
Abweichungen und null Verarbeitungsfehler. Darin enthalten ist die ältere
Referenzgruppe mit 582 Fällen (234 exakt, 348 grau) sowie die späteren
129 Possible-Start-, 125 Cleanup-Link- und 50 Connected-Record-Fälle.
Zusammen mit dem dichten Korpus wurden somit 960 Fälle erneut verglichen:
316 exakt, 644 grau, null Abweichungen. Die Korpora überlappen sich teils;
diese Zahlen sind Testausführungen, keine Zahl einzigartiger Spielsituationen.

`AIVPlacement/build.bat` bestand mit 36/36 Tests. `CastlePlanner/build.bat`
bestand mit 90/90 Tests und installierte CastlePlanner einschließlich des
neuen gemeinsamen Kerns. Die installierten DLL-Hashes stimmen bytegenau
mit dem Workspace-Paket überein. Das Spiel war beim Build beendet.

Die weiterhin offene Grenze ist `0xB8310 -> 0x61FC0` einschließlich
verbundener Records und Startabbruch. Die bisherigen Traces belegen keinen
allgemeinen Schreibbereich für alle möglichen früheren Starts. Spätere
Fits mit Sofortbau bleiben daher grau, bis ihre Eingangszustände exakt
rekonstruierbar sind. Ein weiterer allgemeiner Kartenstart wäre kein
Beleg für den noch unbeobachteten Zweig.

Für die Planung eines gezielten Tests wurden außerdem die 185 zuvor
als auswertbar erkannten installierten Karten mit mindestens drei
Keep-Slots nach **anfänglich** lebenden Records mit nichtnull
Cleanup-Link durchsucht. Innerhalb eines Quadrats von 24 Kacheln um
einen fremden Keep liegen 42 Record-Ursprünge auf zwei Karten:
41 auf der dichten Spezialkarte und einer auf Caesarea Swampland.
Sie gehören sämtlich zu den serialisierten Startgruppen, die Vanilla
vor der KI-Auswahl entfernt. `map-linked-near-start.csv` enthält die
Record-IDs und Koordinaten. Dieser Ursprungsscan erfasst weder ganze
Gebäude-Footprints noch zur Laufzeit neu gebaute Records. Er liefert
somit kein natürliches Preset, das die noch offene dynamische
Mehrfachlöschung sicher auslöst.

Der direkte native Löschpfad ist breiter als `0x61FC0`:
`0xB8310` ruft zuvor `0xB8460`, `0x1977A0` und `0xB5C40`
sowie danach `0xCFE90` auf. Ihre transitiven fitrelevanten
Seiteneffekte und die Abbruchzweige sind die nächste statische
Audit-Grenze. Eine testbare Aufstellung muss anschließend eine
**zur Laufzeit** erzeugte verknüpfte Gruppe treffen; die
serialisierten Startgruppen aus der Karte genügen dafür nicht.
