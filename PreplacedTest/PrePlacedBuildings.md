# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

Stand: 10. September 2026
Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
Script Extender: 2.3.0
Evidenz: `BepInEx\logs\Log\_047.log` und die danach getesteten sauberen Abschnitte in `BepInEx\LogOutput.log`; aktiv waren nur UU-ImGUI, Script Extender und `PreplacedTest`.

## Gesicherte Erkenntnisse

### Vorplatzierte Turmruinen erzeugen einen künstlichen AIV-Startdelay

Auf der Ruinenkarte gehören fünf vorplatzierte zerstörte Turmrecords demselben Spieler. Die konkrete Spieler-ID hängt von der Slot-/Farbzuweisung des jeweiligen Kartenstarts ab und ist keine Eigenschaft des Fehlers. Nur die KI, der diese Records aktuell gehören, erhält beim Kartenstart `crushed_building_delay=1`; die Vergleichs-KI bleibt bei `0` und beginnt sofort zu bauen. Die Ruinen liegen außerhalb des später bestimmten 100×100-AIV-Bereichs.

Der Scheduler behandelt den Wert wie einen echten Gebäudeverlust, zählt ihn bis zum AIC-Grenzwert hoch und überspringt währenddessen die AIV-Ausführung. Danach baut die KI normal.

Der jüngste Ruinenlauf traf den exakten Schadens-Schreibblock bei RVA `0x7F074` während der frühen Aktivierung nicht. Damit stammt der Startwert nicht aus dem zuvor vermuteten normalen Schadensaufruf.

Die statische Analyse zeigt stattdessen einen konkreten Initialisierungspfad: Für Mapformatversionen kleiner als `0xD5` ruft der Ladepfad bei RVA `0x96CE` `FUN_1800D4290` auf. Diese Funktion kopiert für neun Spieler jeweils `0x39F4` Bytes aus geladenen Altformat-Spielerrecords in die aktuellen `0x583C`-Records. Das Timerfeld bei Offset `0x7E4` liegt vollständig innerhalb dieses Kopierbereichs. Damit kann ein bereits im Maprecord gespeicherter Wert zusammen mit den übrigen Spielerdaten übernommen werden. Der nächste Lauf prüft Quell- und Zielwert unmittelbar vor und nach dieser Kopie.

Ein später echter Verlust bleibt davon getrennt: In `_047.log` wurde ein vorplatziertes Torhaus im Kampf tödlich beschädigt; dabei aktivierte der normale Schadenspfad den Timer legitim.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive Path-Connection-Label-ID der Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. Für jede 5×5-Grobrasterzelle zählt `byte+04`, wie viele ihrer 25 Tiles nicht zu dieser globalen Referenz-PCL gehören. `0` bedeutet vollständig Referenz-PCL, `25` vollständig andere PCL.

Die Wirtschaftssuchen verwenden unterschiedliche Grenzen:

- Farmen (`0x575B0`): `signed byte+04 < 17`.
- Holz (`0x58020`): `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): `signed byte+04 < 15`; Kandidat nur bei `byte+04 == 0` und weiteren Bedingungen.
- Ressourcen (`0x57B80`): Differenz aus `signed byte+04` und `signed byte+16`, danach typabhängige Bedingungen.

Auf der Mauerkarte besuchen beide KIs bei echten Holzsuchen nur fünf Zellen um ihre Burg. Die acht orthogonalen Randzellen haben `byte+04=25`; sie gelangen nicht in die Queue. Die Suche findet keinen Kandidaten und erreicht weder `FUN_1800C3BF0`, den Platzierungsvalidator noch `FUN_18006D580`.

### Funktionierende eigene Torhäuser helfen der Wirtschaftssuche nicht

Eine der beiden KIs besitzt vier eigene intakte Torhäuser und kann Einheiten für Angriffe hindurchschicken. Die Besitzer-ID dieser Torhaus-KI wechselt zwischen Kartenstarts und wird deshalb immer aus den aktuellen Building-Records bestimmt. Ihre Wirtschaftssuche scheitert dennoch genauso wie die zeitgleiche Kontrolle mit dichter Mauer ohne Tor.

Einheitenwegfindung und Wirtschaftsraster verwenden damit unterschiedliche Erreichbarkeitsmodelle. Das rohe Owner-Feld eines Portalrecords ist nicht als Spielerbesitzer belegt und darf nicht zur Freund-/Feindklassifikation verwendet werden. Maßgeblich für die neue passive Gegenrechnung sind der tatsächliche aktuelle Gebäudeinhaber und die aktuelle Teamzuordnung.

### Ein Durchbruch aktualisiert das Wirtschaftsraster nicht zuverlässig

`_047.log` enthält einen Durchbruch, nach dem Innen- und Außenseite laut lokalen PCL-Werten verbunden waren, während `byte+04=25` bestehen blieb. Spätere echte Wirtschaftssuchen liefen mit fortgeschrittener Generation erneut und scheiterten weiter; ein bloß stehengebliebener Cooldown war daher nicht die Ursache.

Im jüngsten Mauerlauf fand sichtbar erneut mindestens ein Durchbruch statt, die bisherige automatische Erkennung meldete ihn jedoch nicht. Die zahlreichen bisherigen `PCL_COMPONENT_MERGE`-Meldungen sind kein belastbarer Gegenbeweis: Sie beruhen überwiegend auf globalen PCL-Label-Neunummerierungen und nicht zwingend auf geänderter Konnektivität. Die Diagnose vergleicht künftig stabile Innen-/Außen-Tilepaare und wertet nur den Übergang von unterschiedlichen positiven PCLs zu derselben PCL als Verbindung.

### Korrektur des PCL-Span-Vertrags

Vanilla verarbeitet im relevanten Pfad exakt RVA `0x50EC690` bis exklusiv `0x51890D0`: 641.600 Byte beziehungsweise 320.800 `ushort`-Einträge, gültige Indizes `0..320799`. Script Extender 2.3.0 beginnt am richtigen Offset, stellt `PathConnectionGrid` aber mit 640.000 Einträgen bereit. Die zusätzlichen 319.200 Einträge interpretieren benachbarte native Raster als PCL-Daten.

Unsere frühere vollständige Häufigkeitszählung hat diesen überlangen öffentlichen Vertrag ungeprüft übernommen. Die daraus erzeugten großen Verteilungen und PCL-IDs bis 65535 sind ungültig. Lokal gelesene Werte an gültigen Tile-IDs bleiben aussagekräftig. Der Mod verwendet ausschließlich die belegten 320.800 Einträge.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0`: In den Mauerfällen werden beide nicht erreicht.
- Fehlende Torhausrecords: Vier funktionierende eigene Tore ändern die Wirtschaftssuche nicht.
- Der AIV-100×100-Bereich: Externe Wirtschaftsbauten verwenden einen anderen Suchpfad.
- Der Holzsuch-Cooldown `-1`: Er ist ein suchbereiter Vanilla-Zustand.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen: weiterhin nur Hypothese.
- Eine feste Spieler-ID oder Farbe: Besitzerrollen ändern sich zwischen Kartenstarts.

## Anforderungen an einen sicheren späteren Fix

1. Der Ruinenfix darf nur einen durch die Initialisierung einer neuen Karte übernommenen fehlerhaften Timerwert korrigieren. Geladene Save-Zustände und echte spätere Verluste bleiben unverändert.
2. Der Mauerfix muss pro aktueller KI die direkt sowie über gültige eigene oder verbündete Portale erreichbaren PCLs berücksichtigen. Geschlossene Mauern, feindliche Tore und isolierte Regionen bleiben gesperrt.
3. Vollaufbau und periodische Aktualisierung des Wirtschaftsrasterzustands müssen konsistent behandelt werden. Eine globale pauschale Freigabe ist nicht sicher.
4. Vanillas Rohstoff-, Bauflächen-, Platzierungs- und nachgeschaltete echte Erreichbarkeitsprüfungen bleiben aktiv.
5. Der Gameplay-Fix gehört mit `NetworkMode=1` in `BugfixesAndQoL`; `PreplacedTest` bleibt passiv bei `NetworkMode=0`.

Der vorhandene „replace buildings“-Pfad in `BugfixesAndQoL` behandelt vor allem Ersatzbauten nahe der Startposition. Er muss später abgestimmt werden, behebt die vorgelagerte externe Wirtschaftssuche aber nicht automatisch.

## Noch benötigte Belege

- Quell- und Zielwert des Timers unmittelbar um `FUN_1800D4290`, getrennt nach neuer Karte und Save-Kontext.
- Reproduktion eines sichtbaren Durchbruchs mit label-unabhängiger lokaler Konnektivitätserkennung.
- Ergebnis der passiven spielerspezifischen Gegenrechnung: Torhaus-KI muss hypothetisch geeignete Außenzellen erreichen, während die geschlossene Kontrolle gesperrt bleibt.
- Eindeutige Gründe der Wirtschaftssuchen, wenn keine neue Besuchsgeneration gestartet wird.
- Ein isolierter Test vorplatzierter Produktionsgebäude für die bisher unbelegte Gebäudezählungshypothese.
