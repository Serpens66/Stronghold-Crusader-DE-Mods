# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 10. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender: 2.4.0

Evidenz sind der aktuelle saubere Lauf im `BepInEx\LogOutput.log` vom 10. September 2026 ab 13:45:19, `Log_050.log` und der frühere Durchbruchlauf `BepInEx\logs\Log\_047.log`. Aktiv waren nur UU-ImGUI, Script Extender und `PreplacedTest`.

## Gesicherte Erkenntnisse

### Vorplatzierte zerstörte Türme erzeugen einen künstlichen AIV-Startdelay

Auf der Ruinenkarte gehören fünf vorplatzierte zerstörte Turmrecords derselben, je Kartenstart wechselnden KI. Im aktuellen Lauf war dies Spieler 8. Nur diese KI erhält kurz nach Kartenstart `crushed_building_delay=1`; die Vergleichs-KI bleibt bei `0`. Die Ruinen liegen außerhalb des später bestimmten 100×100-AIV-Bereichs.

Der Scheduler behandelt den Wert wie einen echten Gebäudeverlust, zählt ihn bis zum AIC-Grenzwert 49 hoch und führt währenddessen keine AIV-Schritte aus. Danach baut die KI normal. Der normale Schadensschreiber bei RVA `0x7F074` wurde bei der frühen Aktivierung nicht ausgeführt.

Im aktuellen Lauf war der Timer beim Eintritt in `FUN_180050720` bereits `1`. Diese Funktion ist damit als Schreiber ausgeschlossen. Sie war in früheren Logs lediglich die erste Beobachtungsgrenze nach dem bislang uninstrumentierten Schreibvorgang.

Eine wichtige Offsetkorrektur hebt die frühere Entlastung von `FUN_1800D4290` auf: Der vollständige serialisierte Player-Record beginnt bei RVA `0x379ADD0`, der aktive `GamePlayerResources`-Teil darin aber erst bei `+0x22FC` beziehungsweise RVA `0x379D0CC`. Das aktive Timerfeld `GamePlayerResources+0x7E4` liegt daher im serialisierten Record bei `+0x2AE0`. Die alte Diagnose las fälschlich `record+0x7E4`; ihre `0→0`-Werte sagen über den echten Timer nichts aus.

`FUN_180015B90` überträgt über `FUN_18001F5F0` einen vollständigen `0x583C`-Player-Record zwischen Chore-Puffer und Laufzeitdaten. Dieser Pfad und die korrigierte Altformatkonvertierung sind nun die stärksten Timerkandidaten. `FUN_18000B520` bleibt ausgeschlossen; dessen gleich aussehender Offset gehört zu einem anderen Dateicontainerlayout.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive Path-Connection-Label-ID der gesamten Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. Für jede 5×5-Grobrasterzelle zählt `byte+04`, wie viele ihrer 25 Tiles nicht zu dieser globalen Referenz-PCL gehören. `0` bedeutet vollständig Referenz-PCL, `25` vollständig andere PCL.

Die Wirtschaftssuchen verwenden diese Werte unterschiedlich:

- Farmen (`0x575B0`): Expansion bei `signed byte+04 < 17`.
- Holz (`0x58020`): Expansion bei `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): Expansion bei `signed byte+04 < 15`; ein Kandidat verlangt zusätzlich `byte+04 == 0` und weitere Rohbedingungen.
- Ressourcen (`0x57B80`): Expansion bei `signed byte+04 - signed byte+16 < 16`. Kandidaten verlangen zusätzlich `byte+04 == byte+16`, beim Eisenmodus alternativ eine Differenz kleiner 5, sowie Besitzerklassen-, Belegungs-, Ressourcendichte- und Höhenbedingungen.

Auf der Mauerkarte besuchen echte Vanilla-Holzsuchen beider KIs nur fünf Zellen um ihre Burg. Benachbarte Zellen mit `byte+04=25` gelangen nicht in die Queue. Dadurch werden weder `FUN_1800C3BF0`, Platzierungsvalidator noch der Bauaufruf `FUN_18006D580` erreicht.

### Funktionierende Torhäuser werden von der Wirtschaftssuche nicht berücksichtigt

Im aktuellen Mauerlauf gehörten die vier intakten Torhäuser Spieler 7; Spieler 8 war die geschlossene Kontrolle. Diese IDs gelten nur für diesen Lauf und werden nicht als feste Rollen verwendet. Die Torhaus-KI kann Einheiten für Angriffe durch ihre Tore schicken, ihre Vanilla-Wirtschaftssuche scheitert dennoch am globalen Grobraster.

Die spielerspezifische Gegenrechnung unterscheidet beide Fälle:

- Torhaus-KI: von Beginn an ungefähr 2.159 erreichbare Zellen, 47 Holzkandidaten und 425 Farmkandidaten.
- Geschlossene KI: ungefähr 909 innere Zellen und keine Farm- oder Holzkandidaten.
- Nach dem sichtbaren Durchbruch der geschlossenen KI: ungefähr 2.235 bis 2.244 erreichbare Zellen, zunächst 74 Holzkandidaten und 398 Farmkandidaten.

Damit ist für Farm- und Holzsuchen stark belegt, dass direkt sowie über intakte eigene oder verbündete Tore erreichbare PCLs anstelle einer einzigen globalen PCL verwendet werden müssen. Für Einheiten und Wirtschaft werden unterschiedliche Erreichbarkeitsmodelle benutzt.

### Der aktuelle Durchbruch ist in den Tile-Daten belegt

Ab 13:46:59.798 verlieren bei der geschlossenen KI mehrere Baseline-Mauertiles, darunter die Tiles 199753, 200449, 201143, 202525 und 203213, ihren Wall-Zustand. Bereits ab 13:46:57.448 zeigt die nächste Shadow-Auswertung die Verbindung zur Außenregion und externe Farm-/Holzkandidaten. Der zeitliche Versatz entsteht durch das ungefähr sekündliche Polling der Mauerdaten.

Der alte automatische Melder bestätigte den Durchbruch nicht, weil er eine 25 Tiles große Wall-Komponente direkt am Keep auswählte. Keeps tragen selbst Wall-Flags und dürfen nicht als äußere Einfassung gelten. Die Diagnose schließt nun den Keep-Footprint aus, bestimmt Innen- und Außenraum per orthogonalem Flood-Fill, verbindet dicke und diagonale Mauerstücke über 8er-Nachbarschaft, behandelt Torhausfootprints zunächst als Blocker und ordnet sie erst anschließend der Einfassung als Portale zu. Ein Durchbruch wird nur nach verlorenem Einfassungs-Wall-Tile sowie physischer und PCL-Verbindung bestätigt.

Reine PCL-Neunummerierungen, etwa nach gewöhnlichem Gebäudebau, sind kein Durchbruch.

### Korrektur des PCL-Span-Vertrags

Vanilla verarbeitet RVA `0x50EC690` bis exklusiv `0x51890D0`: 320.800 `ushort`-Einträge mit gültigen Indizes `0..320799`. Script Extender 2.4.0 bildet diesen nativen Vertrag nun korrekt ab; der frühere 2.3.0-Span mit 640.000 Einträgen reichte in benachbarte native Raster.

Frühere vollständige Verteilungen über den öffentlichen überlangen Span sind ungültig. Lokale Werte gültiger Tile-IDs bleiben verwendbar. `PreplacedTest` begrenzt alle PCL-Auswertungen auf 320.800 Einträge.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0`: In den beobachteten Mauerfällen werden beide nicht erreicht.
- Fehlende oder funktionslose Torhäuser.
- Der AIV-100×100-Bereich; externe Wirtschaftsbauten benutzen einen anderen Suchpfad.
- Ein stehengebliebener Such-Cooldown; spätere echte Traversierungen scheitern weiterhin am Raster.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen; dies bleibt eine ungetestete Hypothese.
- Eine feste Spieler-ID oder Farbe für Torhaus- oder Kontrollrolle.

## Anforderungen an einen sicheren späteren Fix

1. Der Ruinenfix darf ausschließlich eine beim Start einer neuen Karte aus vorplatzierten Baselineobjekten geladene oder erzeugte Timeraktivierung neutralisieren. Geladene Spielstände und echte spätere Verluste bleiben unverändert.
2. Der Mauerfix muss pro aktueller KI direkt sowie über gültige eigene oder verbündete Portale erreichbare PCLs berücksichtigen. Geschlossene Mauern, feindliche Tore und isolierte Regionen bleiben gesperrt.
3. Alle originalen Ressourcen-, Bauflächen-, Besitzerklassen-, Höhen-, Platzierungs- und nachgeschalteten Erreichbarkeitsbedingungen bleiben aktiv.
4. Der Gameplay-Fix gehört mit `NetworkMode=1` in `BugfixesAndQoL`; `PreplacedTest` bleibt passiv bei `NetworkMode=0`.

Der vorhandene „replace buildings“-Pfad in `BugfixesAndQoL` behandelt vor allem Ersatzbauten nahe der Startposition. Er muss später abgestimmt werden, behebt die vorgelagerte externe Wirtschaftssuche aber nicht automatisch.

## Noch benötigte Belege

- Ob `record+0x2AE0` bereits im Chore-Puffer `1` enthält oder erst während der nachfolgenden Initialisierung gesetzt wird.
- Ob die korrigierte Altformatkonvertierung oder der `0x15B90/0x1F5F0`-Transfer den ersten echten Timerübergang enthält.
- Exakte Shadow-Ergebnisse und erste Ablehnungsgründe für Steinbruch-, Eisen- und Pech-Kandidaten.
- Ein isolierter Test vorplatzierter Produktionsgebäude für die Gebäudezählungshypothese.
