# Crossing-forward-off: fehlender Keep nach Spieler-3-Start

Der Testmod bestätigte den Kartenstart als Lauf 1/8; der Fortschritt steht
danach auf `Crossing-forward-on` (Index 1). Native-DLL:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Karte `CrusadesCrossing.map`:
`B5AB8BCC5C4C2783697EEF4BBE692AE829B2C7D59C4AFF540F79E140C49417FE`.
Die Log-Kopie und alle 8 Starttraces mit Record-Daten sowie die 16
Fit-/Live-Grid-Paare liegen hier mit SHA-256-Manifest. Die Log-Kopie ist
ein Snapshot aus dem noch laufenden Spielprozess und enthält bereits
die Lobby-Vorbereitung von Lauf 2; sie ist kein vollständiger
Prozessabschlusslog.
Der importierte Oracle-Korpus enthält 16 Native-Fits. Mit dem aktuellen
Offline-Kern sind vier exakt, zwölf bewusst `NotEvaluable`; es gibt
keine Score-, Prozent-, Blockzellen- oder Verarbeitungsabweichung.

Der Lobby-Keep-Slot von Spieler 2 liegt bei `(510,495)`, der von Spieler 3
bei `(548,483)`. Vanillas AIV-Auswahl setzte beide auf 90° und startete
die tatsächlichen Keep-Konstruktionen bei `(510,502)` und `(523,489)`.
Spieler 2 baute zuerst neun Startrecords und 117 neue Gebäudezellen,
darunter Gebäuderecord 10, Typ 41, Besitzer 2, mit 7×7 Zellen bei
`x=510..516, y=502..508`. Sein Konstruktor-Fehlerflag blieb null.

Im synchronen Vorher/Nachher-Snapshot des **nachfolgenden nativen
Startaufrufs für Spieler 3** wurden 101 bisherige Gebäudezellen von
Spieler 2 geleert: 49 des Keep-Records 10, je eine der verbundenen
Records 11–13 und 49 des 7×7-Records 14. Die Cleanup-Linkwerte dieser
fünf Records waren identisch (`465609`). Ihre IDs 10–14 wurden direkt
für neue Startrecords von Spieler 3 wiederverwendet. Spieler 3 baute
ebenfalls 117 neue Gebäudezellen; sein Fehlerflag blieb null.
Die vier 2×2-Lagerplatz-Records 15–18 von Spieler 2 waren im nächsten
Live-Raster weiterhin vorhanden. Deshalb passt die Bildbeobachtung
„Flagge und Lagerplatz, aber kein Keep“ genau zu den nativen Tile-Diffs.

Die lokale Fixes-Quelle ersetzt optional den nachgelagerten
Goodsyard-Tailcall des Keep-Konstruktors. Für den hier beobachteten
Gruppenräumungspfad `0x74DA0 -> 0x5D3A0 -> 0xC4290 -> 0xB8310`
ist dort kein Patch gefunden worden. Der Befund belegt eine Räumung
während des nativen Startaufrufs; er behauptet keine vollständige
Rekonstruktion aller möglichen früheren AIV-Auswahlen.

Ein anderer Mod (`Random Events`) meldete danach, dass er für Spieler 2
keinen bereiten Keep fand. Das ist ein nachgelagerter Effekt und trat
nach den vollständigen Start- und Fit-Aufnahmen auf. Der erste Lauf ist
für die AIV-Diagnose gültig; er wäre kein brauchbarer normaler
Skirmish-Spielstand. Für die verbleibenden Teststarts genügt weiterhin
der sichtbare Spielbeginn.
