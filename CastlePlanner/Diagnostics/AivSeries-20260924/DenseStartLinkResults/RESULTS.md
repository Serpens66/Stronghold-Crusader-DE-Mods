# Dichte Startplätze: Ergebnis vom 24. September 2026

Native-DLL SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installierter Script Extender SHA-256:
`710B4DA701D08250C4760181B4B5C0702A333AEAE8EBA33C311B30530FE9A697`.
Installierter ActiveAIVDetector SHA-256:
`EB9C2BA1D04BFE458D9375FE2AF5D35523C2E9E01D5B5B1E8470D396F166A077`.
Karten-SHA-256:
`D63CD2FF3AEABA80BC3BC173BB615207666F1EAF759ECAC573FAD0DA61979DF3`.
Der bytegetreue letzte BepInEx-Prozessabschnitt ist
`session-current.original.bin` mit SHA-256
`8A911BCFB22E186D0D99A3EB14255C2B137E9A1A5E2F74218B23CD16E93F8653`.
`Inputs/` enthält die geprüfte Karte, vier tatsächlich ausgewählte
Default-AIV-Dateien und die entpackte Gebäuderecord-Sektion.

## Bestätigte Läufe

Der Testmod bestätigte alle vier Serienläufe und steht auf
`nextIndex: 4`, `lastCompletedRunId: OB-reverse-on`. Anschließend
startete der Benutzer zwei weitere Karten ohne Preset-Übernahme.
Diese wiederholten das Setup `OB-reverse-on`; ihre Native-Fit-Signatur
ist exakt gleich wie die des vierten Laufs. Die Runtime-Lordnamen zu
den gewählten numerischen IDs sind Nizar, Wolf, Marshal und Jewel.

| Map-Load | Setup | Sofortspawn | Native-Fits | Exakt | NotEvaluable | Prebuild-Traces |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| 001 | OB-forward-off | aus | 10 | 1 | 9 | 0 |
| 002 | OB-forward-on | an | 13 | 1 | 12 | 2 |
| 003 | OB-reverse-off | aus | 12 | 1 | 11 | 0 |
| 004 | OB-reverse-on | an | 13 | 1 | 12 | 2 |
| 005–006 | Wiederholungen von 004 | an | 26 | 2 | 24 | 4 |
| **Gesamt** | | | **74** | **6** | **68** | **8** |

Alle 30 Keep-Starttraces sind vollständig; ihre 30 verknüpften
Record-Traces haben jeweils genau zweimal so viele Vorher-/Nachher-
Zeilen wie deklarierte geänderte Records. Alle 30 nativen
Konstruktor-Fehlerflags sind null. Die acht Sofortbau-Traces besitzen
vollständige Frames und Provenienz, ohne Pointer- oder Capture-Fehler.
74 Native-Fits haben 74 Cell-Traces und 74 Live-Building-Grids. Der
Offline-Vergleich ergab **keine** Score-, Prozent- oder
Blocked-Cell-Abweichung in den sechs freigegebenen Fällen und
keinen Vergleichsfehler.

## Record-Gruppen und Räumung

Die `.map` enthält in Sektion 4013 genau 45 anfänglich lebende
Gebäuderecords. Alle 45 haben einen nichtnullen Wert bei Offset
`0x2A8`: pro Spieler fünf Keep-/Lager-Records unter einem Linkwert
und vier Yard-Records unter einem zweiten. Beim ersten Native-Fit
des ersten KI-Spielers in Map-Load 001 zeigt das vollständige
Live-Building-Grid nur die neun gerade aufgebauten Records des
menschlichen Starts (IDs 1–9); die serialisierten KI-Startgruppen
sind zu diesem Zeitpunkt bereits entfernt. Diese Beobachtung
begrenzt die Aussage der Keep-Hooks: Sie erfassen nicht die frühere
Kartenstart-Normalisierung.

In den 30 Keep-Startaufrufen wurden 279 Records geändert. Bei 65
geänderten Record-Slots war der Vorher-Zustand lebendig. **Keiner**
dieser 65 alten Records hatte einen nichtnullen Cleanup-Linkwert.
Neun alte Records wurden vollständig gelöscht: je drei bei Spieler 4
in den Map-Loads 004, 005 und 006. Sie waren Typ 67, gehörten dem
vorigen Spieler 2 und hatten Linkwert null. Ihre Löschung ist eine
echte Folge der nahen Starts mit Sofortspawn, aber kein Beleg für die
zusätzliche Gruppenlöschung durch `0xC43A0`. In den Läufen ohne
Sofortspawn wurde bei keinem Keep-Start ein vorher lebender Record
überschrieben. Ein Startabbruch trat nicht auf.

Die zwei ungeplanten Wiederholungen liefern für diese Entscheidung
keinen neuen Zweig. Ein weiterer identischer Start ist nicht sinnvoll.
Die Karte ist unter den geprüften 373 installierten `.map`-Dateien
bereits diejenige mit dem kleinsten Abstand zweier wählbarer
Keep-Slots (24,76 Kacheln); die Abstände stehen in
`map-start-spacing.csv`. Ein tatsächlicher Zusammenstoß zweier
verknüpfter Startkomplexe ist damit mit der vorhandenen Aufstellung
nicht gezielt erzwingbar.

## Statische Eingrenzung der initialen Gruppen

Der installierte Native-Hash zeigt in `0x94350` zwei getrennte
Schleifen: Die erste entfernt für die vorhandenen Spieler
serialisierte Startrecords über `0xC43A0`; erst die zweite beginnt
die KI-Auswahl und den erneuten Startbau. `0xC3FA0` liegt im
nachfolgenden spielerbezogenen Zweig vor dessen Auswahl.
Das erklärt die Live-Grid-Beobachtung. Bei elf der 68 grauen
Oracle-Fälle nennt der aktuelle `StartOverlapUnproven`-Guard noch
einen **source tile** eines solchen serialisierten KI-Records als
möglichen Kollisionsauslöser. Beispielsweise ist Building-ID 10 in
der `.map` ein Typ-41-Record von Spieler 2 mit Linkwert 11, aber im
ersten KI-Fit bereits aus dem Live-Raster entfernt. Neun weitere
graue Fälle nennen dagegen **rebuilt tile** eines tatsächlich
früher gebauten Starts; 48 Fälle sind wegen eines vorigen
Sofortbaus gesperrt.

Eine spätere Kernkorrektur darf die bereits entfernten **source**-
Records bei der Kollisionsprüfung ignorieren, muss aber die
rekonstruierten früheren Starts, erhaltene menschliche Gebäude,
Konstruktorabbrüche und alle Kandidatendrehungen weiter prüfen.
Allein das Entfernen dieses einen Fehlalarms belegt noch keinen
exakten Fit der elf Fälle: Der zweite, kandidatenbezogene
Unsicherheits-Guard kann weiterhin greifen. Deshalb wurde während
des parallelen Script-Extender-Updates kein Runtime-Code geändert.

## Produktionsentscheidung

Die initiale Gruppenzugehörigkeit ist aus der Karte lesbar. Die
Räumung vor dem ersten KI-Fit und die möglichen später dynamisch
gebauten Gruppen sind jedoch nicht vollständig rekonstruiert.
Der bestehende `StartOverlapUnproven`-Schutz bleibt aktiv; aus diesen
Läufen wird kein weiterer späterer KI-Fit freigegeben. Die
Sofortspawn-Fälle späterer KIs bleiben entsprechend grau. Für eine
engere sichere Grenze sind zuerst die nativen Effekte der
Kartenstart-Normalisierung und der typabhängigen Löschung
`0xB8310 -> 0x61FC0` vollständig zu verfolgen. Neue Spielstarts
lohnen erst mit einer Aufstellung, die nachweislich eine
verbundene dynamische Record-Gruppe trifft.
