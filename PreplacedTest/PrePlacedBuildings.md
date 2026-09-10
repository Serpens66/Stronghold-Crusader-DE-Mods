# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

Stand: 10. September 2026
Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
Script Extender: 2.3.0
Evidenz: `BepInEx\logs\Log\_047.log` und die danach getesteten sauberen Abschnitte in `BepInEx\LogOutput.log`; aktiv waren nur UU-ImGUI, Script Extender und `PreplacedTest`.

## Gesicherte Erkenntnisse

### Vorplatzierte Turmruinen erzeugen einen künstlichen AIV-Startdelay

Auf der Ruinenkarte gehören die fünf vorplatzierten zerstörten Turmrecords (`STRUCT_TOWER1_DESTROYED` bis `STRUCT_TOWER5_DESTROYED`) Spieler 8. Nur dessen `crushed_building_delay` wechselt beim Kartenstart von `0` auf `1`; die Vergleichs-KI ohne diese Objekte bleibt bei `0` und beginnt sofort zu bauen. Die Ruinen liegen außerhalb des später bestimmten 100×100-AIV-Bereichs.

Der Wechsel liegt zwischen `OnStartMap Pre` und der ersten AIV-Zuweisung. Danach behandelt der Scheduler den Zähler wie einen echten Gebäudeverlust, zählt ihn bis zum AIC-Wert `50` hoch und überspringt währenddessen die AIV-Ausführung. Anschließend baut die KI normal.

Die statische Analyse findet außerhalb des Schedulers nur einen möglichen Schreiber: Im Gebäudeschadenspfad `FUN_18007EB00` setzt der Block bei RVA `0x7F074` den Zähler bei Aktivierungsmodus `1` von `0` auf `1`. Die neue Diagnose beobachtet genau diesen Schreibblock passiv. Erst der nächste Lauf kann damit beweisen, welches Turmrecord und welche Schadensparameter die Initialisierung verwendet.

Ein später echter Verlust muss unverändert bleiben: In `_047.log` wurde das vorplatzierte Torhaus Building-ID 21, Global-ID 6614, Spieler 7, im Kampf tödlich beschädigt. Dabei wechselte der Delay legitim von `0` auf `1`.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive Path-Connection-Label-ID (PCL) der Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. Für jede 5×5-Grobrasterzelle zählt `byte+04`, wie viele ihrer 25 Tiles nicht zu dieser globalen Referenz-PCL gehören. `0` bedeutet vollständig Referenz-PCL, `25` vollständig andere PCL.

Die Wirtschaftssuchen verwenden unterschiedliche Grenzen:

- Farmen (`0x575B0`): `signed byte+04 < 17`.
- Holz (`0x58020`): `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): `signed byte+04 < 15`; Kandidat nur bei `byte+04 == 0` und weiteren Bedingungen.
- Ressourcen (`0x57B80`): Differenz aus `signed byte+04` und `signed byte+16`, danach typabhängige Bedingungen.

Auf der Mauerkarte besuchen beide KIs nur fünf Zellen um ihre Burg. Die acht orthogonalen Randzellen haben `byte+04=25`; die Suche nimmt sie nicht in die Queue auf, findet keinen Kandidaten und erreicht weder `FUN_1800C3BF0`, den Platzierungsvalidator noch den Bauaufruf `FUN_18006D580`.

### Funktionierende Torhäuser helfen der Wirtschaftssuche nicht

Eine KI besitzt vier eigene, intakte Torhäuser und kann Einheiten für Angriffe hindurchschicken. Ihre Wirtschaftssuche scheitert trotzdem genauso wie die zeitgleiche Kontrolle mit dichter Mauer ohne Tor. Einheitenwegfindung und Wirtschaftsraster benutzen damit unterschiedliche Erreichbarkeitsmodelle.

Das rohe Owner-Feld eines nativen Portalrecords ist nicht als Spielerbesitzer belegt: Bei den vier tatsächlich Spieler 8 gehörenden Torhäusern enthält es den Wert `1`. Der Mod trennt deshalb künftig rohen Portalwert, tatsächlichen Gebäudeinhaber und beobachtete `0xE2610`-Entscheidung. Frühere Einteilungen dieses Felds in freundlich oder feindlich werden nicht weiterverwendet.

### Ein Mauerdurchbruch ändert die PCL-Verbindung, aber nicht das Wirtschaftsraster

`_047.log` und der dritte Kartenstart des neuesten Laufs belegen jeweils einen einseitigen Durchbruch. Im dritten Start werden um `01:09:25` zuvor getrennte PCL-Komponenten auf der Innen- und Außenseite der betroffenen Mauer zusammengeführt. Gleichzeitig bleibt der rohe Wirtschaftsrasterzustand einschließlich `byte+04=25` unverändert.

Spätere echte Wirtschaftssuchen laufen weiter und scheitern weiterhin: Unter anderem führt Spieler 7 um `01:09:50` eine neue Holzsuche mit fortgeschrittener Suchgeneration aus, besucht erneut fünf Zellen, sieht acht Randzellen und findet keinen Kandidaten. Ein stehengebliebener Cooldown ist damit nicht die Ursache. PCL-Verbindung und gespeicherte Wirtschaftsrasterklassifikation laufen nach dem Durchbruch auseinander.

Mauerdurchbrüche erzeugen nicht zuverlässig Building-Damage-, Delete- oder Bulldoze-Ereignisse. Die neue Diagnose erkennt sie deshalb anhand lokaler PCL-Komponentenzusammenführung im zuletzt von der jeweiligen KI erreichten Such-/Mauerrandbereich und korreliert die nachfolgenden Suchen automatisch.

### Korrektur eines Diagnosefehlers

Vanilla verarbeitet im relevanten PCL-Pfad exakt den Speicherbereich RVA `0x50EC690` bis exklusiv `0x51890D0`: 641.600 Byte beziehungsweise 320.800 `ushort`-Einträge, gültige Indizes `0..320799`. Script Extender 2.3.0 stellt `PathConnectionGrid` am richtigen Startoffset, aber mit 640.000 Einträgen bereit. Die zweite Hälfte gehört nicht zum von Vanilla hier ausgewerteten PCL-Array.

Unsere frühere vollständige Häufigkeitszählung hat die zu große Extender-Sicht ungeprüft übernommen. Die daraus entstandenen riesigen Verteilungen und PCL-IDs bis 65535 sind ungültig und werden nicht weiterverwendet. Lokal gelesene Werte an gültigen Tile-IDs und die beobachtete Komponentenzusammenführung bleiben aussagekräftig. Der Mod liest künftig ausschließlich die belegten 320.800 Einträge und protokolliert jeden angeforderten Index außerhalb dieses Bereichs als ungültig.

## Nicht als Ursache bestätigt

- Der Platzierungsvalidator oder `FUN_1800C3BF0`: In den Mauerfällen werden beide gar nicht erreicht.
- Fehlende Torhaus- oder Portalrecords: Vier funktionierende Tore ändern das Ergebnis nicht.
- Der AIV-100×100-Bereich: Externe Wirtschaftsbauten folgen einem anderen Suchpfad.
- Der Holzsuch-Cooldown `-1`: Er ist ein suchbereiter Vanilla-Zustand; neue Traversals erfolgen nach dem Durchbruch.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen: weiterhin nur Hypothese, nicht als Ursache belegt.

## Anforderungen an einen sicheren späteren Fix

1. Der Ruinenfix darf nur die nachweislich durch Karteninitialisierung eines identischen Baselineobjekts erzeugte Aktivierung unterdrücken. Echte spätere Verluste und geladene Save-Zustände bleiben erhalten.
2. Der Mauerfix muss die Wirtschaftsrasterklassifikation pro KI aus tatsächlich direkt oder über nach Vanilla-Regeln gültige eigene beziehungsweise verbündete Portale erreichbaren PCLs ableiten. Geschlossene Mauern, feindliche Tore und isolierte Regionen bleiben gesperrt.
3. Vollaufbau und periodische Aktualisierung des Rasters müssen konsistent korrigiert werden. Eine pauschale globale Änderung ist wegen mehrerer gleichzeitig suchender KIs nicht sicher.
4. Nach der Rastersuche bleiben Vanillas Bauflächen-, Rohstoff-, Platzierungs- und echte Erreichbarkeitsprüfungen aktiv.
5. Der Gameplay-Fix gehört mit `NetworkMode=1` in `BugfixesAndQoL`; `PreplacedTest` bleibt passiv bei `NetworkMode=0`.

Der vorhandene „replace buildings“-Pfad in `BugfixesAndQoL` behandelt vor allem Ersatzbauten nahe der Startposition. Er muss bei der späteren Umsetzung abgestimmt werden, behebt die hier vorgelagerte Blockade externer Wirtschaftsraster-Suchen aber nicht automatisch.

## Noch benötigte Belege

- Ein Ruinenlauf mit dem neuen exakten `0x7F074`-Writer-Hook, um Zielrecord und Initialisierungsparameter sicher zuzuordnen.
- Ein Mauerlauf, in dem die automatische lokale Durchbrucherkennung und die anschließende Suchkorrelation denselben Befund reproduzieren.
- Die vollständige Ursache dafür, dass `byte+04` nach PCL-Änderungen nicht neu aufgebaut wird, einschließlich aller Vollaufbau- und periodischen Update-Aufrufer.
- Ein isolierter Test vorplatzierter Produktionsgebäude für die bisher unbelegte Gebäudezählungshypothese.
