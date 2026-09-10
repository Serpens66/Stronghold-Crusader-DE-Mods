# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 10. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender: 2.3.0

Evidenz: insbesondere `BepInEx\logs\Log_050.log` und der frühere Durchbruchlauf `BepInEx\logs\Log\_047.log`. In den sauberen Tests waren nur UU-ImGUI, Script Extender und `PreplacedTest` aktiv.

## Gesicherte Erkenntnisse

### Vorplatzierte Turmruinen erzeugen einen künstlichen AIV-Startdelay

Auf der Ruinenkarte gehören fünf vorplatzierte zerstörte Turmrecords derselben, je Kartenstart wechselnden KI. Nur diese KI erhält kurz nach dem Kartenstart `crushed_building_delay=1`; die Vergleichs-KI bleibt bei `0`. Die Ruinen liegen außerhalb des später bestimmten 100×100-AIV-Bereichs.

Der Scheduler behandelt den Wert wie einen echten Gebäudeverlust, zählt ihn bis zum AIC-Grenzwert hoch und führt in dieser Zeit keine AIV-Schritte aus. Danach baut die KI normal.

`Log_050.log` grenzt die Quelle weiter ein:

- Der normale Schadensschreiber bei RVA `0x7F074` wurde bei der frühen Aktivierung nicht ausgeführt.
- Die Altformatkonvertierung `FUN_1800D4290` kopierte für alle neun Records ausschließlich `0→0`. Sie ist in diesem Lauf nachweislich nicht die Timerquelle.
- `FUN_18000B520` verarbeitet ein allgemeines Dateicontainer-/Headerlayout. Dessen Feld bei `+0x7E4` ist nur eine Offsetübereinstimmung und kein Beleg für einen Zugriff auf `GamePlayerResources`.
- Der Übergang wurde erstmals nach dem vollständigen Wirtschaftsrasteraufbau `FUN_180050720` beobachtet. Das ist bislang nur eine zeitliche Beobachtungsgrenze: Der statische Code von `0x50720` schreibt Wirtschaftsrasterdaten, aber kein belegtes Spieler-Timerfeld.

Ein späterer echter Gebäudeverlust bleibt davon getrennt. Im früheren Lauf `_047.log` aktivierte tödlicher Schaden an einem vorplatzierten Torhaus den normalen Schadenspfad legitim.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive Path-Connection-Label-ID der gesamten Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. Für jede 5×5-Grobrasterzelle zählt `byte+04`, wie viele ihrer 25 Tiles nicht zu dieser globalen Referenz-PCL gehören. `0` bedeutet vollständig Referenz-PCL, `25` vollständig andere PCL.

Die Wirtschaftssuchen verwenden diese Werte unterschiedlich:

- Farmen (`0x575B0`): Expansion bei `signed byte+04 < 17`.
- Holz (`0x58020`): Expansion bei `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): Expansion bei `signed byte+04 < 15`; ein Kandidat verlangt zusätzlich `byte+04 == 0` und weitere Rohbedingungen.
- Ressourcen (`0x57B80`): Expansion anhand der Differenz aus `signed byte+04` und `signed byte+16`, danach typabhängige Kandidatenbedingungen.

Auf der Mauerkarte besuchen beide KIs bei echten Holzsuchen nur fünf Zellen um ihre Burg. Die angrenzenden Randzellen mit `byte+04=25` gelangen nicht in die Queue. Die Suche erreicht dadurch weder `FUN_1800C3BF0`, den Platzierungsvalidator noch `FUN_18006D580`.

### Funktionierende eigene Torhäuser helfen der Wirtschaftssuche nicht

Die „KI mit Torhäusern“ besitzt vier eigene intakte Torhäuser und kann Einheiten für Angriffe hindurchschicken. Ihre konkrete Spieler-ID ist nicht stabil; in `Log_050.log`, Kartenlauf 2, war es Spieler 7. Ihre Wirtschaftssuche scheitert dennoch wie die zeitgleiche „KI mit geschlossener Mauer“ ohne Torhäuser, in diesem Lauf Spieler 8.

Einheitenwegfindung und Wirtschaftsraster verwenden unterschiedliche Erreichbarkeitsmodelle. Für eine spielerspezifische Gegenrechnung sind der tatsächliche aktuelle Gebäudeinhaber und die Diplomatie maßgeblich. Das ungeklärte rohe Owner-Feld eines Portalrecords wird nicht als Besitzer interpretiert.

### Der bisherige automatische Durchbruchsmelder war nicht zuverlässig

Im zweiten Kartenlauf von `Log_050.log` wurde nur die Mauer der KI ohne Torhäuser sichtbar durchbrochen. Die Diagnose meldete außerdem bei der Torhaus-KI während eines gewöhnlichen Brunnenbaus eine Verbindung. Dieser Treffer ist ein Fehlalarm: Die vermeintlichen „Anker“ stammten aus besuchten und abgewiesenen Wirtschaftszellen und waren nicht an eine echte Mauerkomponente gebunden.

Die spätere Meldung für die KI ohne Torhäuser fällt zeitlich mit dem sichtbaren Durchbruch zusammen, reicht mit derselben fehleranfälligen Grundlage aber allein nicht als technischer Beweis. Normale Mauersegmente sind keine Building-Records. Mauerbesitz, Mauerzustand und Durchbrüche müssen aus `LogicGrid`, `WallOwnerGrid`, `DamageGrid`, `StructureWasGrid`, `GatePathGrid` und lokalen PCL-Werten abgeleitet werden.

`_047.log` bleibt Evidenz dafür, dass nach einem sichtbaren Durchbruch spätere echte Wirtschaftssuchen mit neuer Generation erneut scheitern und `byte+04=25` bestehen kann. Ein bloß stehengebliebener Cooldown erklärt das Verhalten daher nicht.

### Korrektur des PCL-Span-Vertrags

Vanilla verarbeitet RVA `0x50EC690` bis exklusiv `0x51890D0`: 641.600 Byte beziehungsweise 320.800 `ushort`-Einträge, gültige Indizes `0..320799`. Script Extender 2.3.0 stellt ab derselben Startadresse 640.000 Einträge bereit. Die zusätzlichen Einträge liegen in benachbarten nativen Rastern und sind keine PCL-Daten.

Frühere vollständige Verteilungen über den öffentlichen überlangen Span sind ungültig. Lokale Werte gültiger Tile-IDs bleiben verwendbar. `PreplacedTest` begrenzt alle PCL-Auswertungen auf die belegten 320.800 Einträge.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0`: In den beobachteten Mauerfällen werden beide nicht erreicht.
- Fehlende oder funktionslose Torhäuser: Vier eigene Tore funktionieren für Einheiten.
- Der AIV-100×100-Bereich: Externe Wirtschaftsbauten benutzen einen anderen Suchpfad.
- Holzsuch-Cooldown `-1`: Das ist ein suchbereiter Vanilla-Zustand.
- `FUN_1800D4290` oder `FUN_18000B520` als Quelle der frühen Timeraktivierung.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen: weiterhin nur eine Hypothese.
- Eine feste Spieler-ID oder Farbe für eine Testrolle.

## Anforderungen an einen sicheren späteren Fix

1. Der Ruinenfix darf ausschließlich eine durch die Initialisierung einer neuen Karte und vorplatzierte Baselineobjekte erzeugte Timeraktivierung neutralisieren. Geladene Save-Zustände und echte spätere Verluste bleiben unverändert.
2. Der Mauerfix muss pro aktueller KI direkt sowie über gültige eigene oder verbündete Portale erreichbare PCLs berücksichtigen. Geschlossene Mauern, feindliche Tore und isolierte Regionen bleiben gesperrt.
3. Vollaufbau und periodische Aktualisierung des Wirtschaftsrasterzustands müssen konsistent behandelt werden. Rohstoff-, Bauflächen-, Platzierungs- und nachgeschaltete Erreichbarkeitsprüfungen bleiben aktiv.
4. Der Gameplay-Fix gehört mit `NetworkMode=1` in `BugfixesAndQoL`; `PreplacedTest` bleibt passiv bei `NetworkMode=0`.

Der vorhandene „replace buildings“-Pfad in `BugfixesAndQoL` behandelt vor allem Ersatzbauten nahe der Startposition. Er muss später abgestimmt werden, behebt die vorgelagerte externe Wirtschaftssuche aber nicht automatisch.

## Noch benötigte Belege

- Der erste konkrete Initialisierungsschritt, in dessen Ausführung der Ruinen-Timer `0→1` wechselt.
- Ein Durchbruch, der zugleich durch ein verlorenes Baseline-Mauertile und eine echte Verbindung stabiler Innen-/Außenanker bestätigt wird.
- Eine vollständige passive Shadow-Suche: Die Torhaus-KI muss Außenzellen und geeignete Kandidaten erreichen, während die geschlossene Kontroll-KI an ihrer Mauer endet.
- Die genaue Unterscheidung früher Suchausgänge ohne neue Besuchsgeneration.
- Ein isolierter Test vorplatzierter Produktionsgebäude für die unbelegte Gebäudezählungshypothese.
