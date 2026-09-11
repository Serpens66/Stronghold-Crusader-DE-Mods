# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 11. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Getesteter Script Extender: 2.5.0

Maßgebliche Evidenz ist der saubere Prozesslauf im `BepInEx\LogOutput.log` vom 11. September 2026 ab 14:54:01. Er enthält zwei Ruinenstarts und drei Mauerstarts. Frühere Läufe werden nur dort weiter berücksichtigt, wo sie mit diesem Stand vereinbar sind. Aktiv waren UU-ImGUI, Script Extender und `PreplacedTest`.

## Gesicherte Erkenntnisse

### Der Ruinen-Delay wird aus dem serialisierten Altformat-Spielerrecord übernommen

Auf der Ruinenkarte existieren fünf vorplatzierte zerstörte Turmrecords. Im aktuellen Lauf gehörten sie beim frühen Snapshot Spieler 8. Ausschließlich dessen `crushed_building_delay` wurde aktiv und blockierte den AIV-Scheduler, bis der Zähler den konfigurierten Grenzwert 50 erreicht hatte.

Die korrigierte Diagnose von `FUN_1800D4290` belegt um 14:13:50.922:

- Kartenformatversion 172, also Altformatkonvertierung;
- kein geladener Spielstand;
- serialisiertes Feld `record+0x2AE0` für Index 8 bereits vor dem Transfer `1`;
- Laufzeitfeld vor dem Transfer `0` und danach `1`;
- alle anderen Spielerrecords blieben `0`.

`FUN_1800D4290` erzeugt den Wert nicht, sondern kopiert den vollständigen alten Player-State einschließlich des bereits gespeicherten Timers in den aktuellen Laufzeitrecord. `0x50720`, `0x15B90`, `0x1F5F0` und der spätere Schadensschreiber `0x7F074` sind im beobachteten Lauf nicht die Aktivierungsquelle.

Während der weiteren Karteninitialisierung wurden die Besitzer der zerstörten Turmrecords von 8 auf 4 umgesetzt. Daher darf ein Fix die Zuordnung nicht allein aus dem späteren Building-Owner ableiten.

Beide Ruinenstarts erfüllten dieselben engen Fixkriterien. Der aktive Testfix normalisiert den Laufzeitwert deshalb bei `OnStartMap Post` nur dann, wenn zusätzlich eine inzwischen bestätigte KI-Zuordnung, passende beim Transfer vorplatzierte zerstörte Turmrecords, ein weiterhin exakter Timerwert `1` und kein später beobachteter Schadensschreiber vorliegen. Geladene Spielstände, bereits aktive Laufzeitwerte, abweichende Quellwerte, andere zerstörte Gebäudetypen und spätere echte Verluste bleiben unverändert.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive PCL der gesamten Karte. `FUN_180050720` speichert sie bei einem Vollaufbau in `state+0x5B504`. `byte+04` jeder 5×5-Grobrasterzelle zählt anschließend die Tiles, deren PCL von dieser globalen Referenz abweicht.

Die Wirtschaftssuchen behandeln `byte+04` als Durchquerbarkeitswert:

- Farm (`0x575B0`): Expansion bei `signed byte+04 < 17`.
- Holz (`0x58020`): Expansion bei `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): Expansion bei `signed byte+04 < 15`; Kandidaten verlangen `byte+04 == 0` und weitere Bedingungen.
- Ressourcen (`0x57B80`): Expansion bei `signed byte+04 - signed byte+16 < 16`; Kandidaten verlangen Gleichheit beziehungsweise beim Eisenmodus eine Differenz kleiner 5 sowie weitere Vanilla-Bedingungen.

Die bisherige Shadow-Diagnose hatte den abschließenden Höhenvergleich für Stein, Eisen und Pech umgekehrt. Vanilla akzeptiert die Zelle bei ausreichender Dichte, wenn die vorzeichenbehaftete Differenz `byte+0D - byte+0C` kleiner als 40, 30 beziehungsweise 12 ist. Dieser Diagnosefehler ist im aktuellen Quellstand korrigiert; die vorher protokollierten null Rohstoffkandidaten sind deshalb nicht als Gegenbeleg verwendbar.

### Torhäuser funktionieren für Einheiten, aber nicht für die Wirtschafts-Rastersuche

In den drei aktuellen Mauerstarts gehörten die vier intakten Torhäuser Spieler 7; Spieler 8 war die torlose Kontrolle. Diese IDs gelten nur für diese Sitzungen. Rollen werden bei jedem Kartenstart dynamisch bestimmt.

Die spielerspezifische Gegenrechnung war in allen drei Starts reproduzierbar:

- Torhaus-KI: ungefähr 2.159 erreichbare Zellen, 425 Farm-, 47 Holz-, 93 Stein- und 39 Eisenkandidaten.
- Torlose KI: ungefähr 909 eingeschlossene Zellen und keine Kandidaten dieser vier Arten.

In keinem der drei aktuellen Starts erfolgte ein sichtbarer oder in den Tile-Daten belegter Mauerdurchbruch. Die Torhaus-KI wählte dabei nacheinander die AIV-Kandidaten 0, 1 und 7; das Ausbleiben eines Angriffs lässt sich daher nicht auf eine einzige wiederholt gewählte AIV-Datei reduzieren. Für die Wirtschaftsursache ist ein Durchbruch nicht mehr nötig: Eigene beziehungsweise nach Vanillas Regeln erreichbare Portalverbindungen bilden für Farm, Holz, Stein und Eisen ein reproduzierbares spielerspezifisches Gegenmodell, während die zeitgleiche geschlossene Kontrolle gesperrt bleibt.

Ein früherer Lauf mit sichtbarem Durchbruch zeigte außerdem: Vanilla verwendete die danach erreichbare Verbindung weiterhin nicht. Echte Holzsuchen liefen regelmäßig, besuchten aber erneut nur fünf Zellen um die Burg, fanden keinen Kandidaten und aktivierten den Cooldown. Identische Vollausgaben wurden aggregiert; die Intervall- und Gesamtsummen belegen die späteren Traversierungen.

Farm- und Rohstoffsuchen verließen die Funktion im relevanten Mauerabschnitt überwiegend mit `desired=0`. Die Diagnose führt deshalb weiterhin alle drei Ressourcen-Shadow-Suchen unabhängig von der aktuellen AIC-Nachfrage aus; der aktive Overlayfix selbst greift ausschließlich in einem echten, eindeutig zugeordneten Wirtschaftssuchkontext ein.

### Der aktuelle Lauf enthielt keinen Mauerdurchbruch

Die beobachteten Änderungen an Torhaustiles verloren kein Wall-Flag und waren Initialisierungs-, Öffnungs- oder Routingzustände. Es wurde weder `wallLost=true` noch ein bestätigter Durchbruch protokolliert. Ein früherer Lauf belegt weiterhin, dass die Shadow-Reichweite nach einem echten Tileverlust anwächst; der aktuelle Lauf liefert dafür keine neue Evidenz.

### Das bisherige Farm-Laufzeitorakel verwendete veraltete Koordinaten

`0x575B0` baut eine gefundene Farm direkt über `0x6D580` und schreibt ihre Koordinaten nicht in das gemeinsame Ergebnisfeld. Die zehn bisherigen Farm-`SHADOW_NATIVE_RESULT_MISMATCH`-Meldungen verglichen deshalb erfolgreiche Suchläufe mit alten Koordinaten. Sie widerlegen das PCL-Gegenmodell nicht.

Der aktive Teststand korreliert Farmen nun mit den tatsächlichen `0x6D580`-Aufrufen desselben Suchkontexts. Bis Spieler-Verfügbarkeitsmasken und Orientierungs-/Offsetbedingungen vollständig bestätigt sind, werden einfache Zelltreffer ausdrücklich nur als `prefilter-candidate` bezeichnet. Die acht überprüfbaren erfolgreichen Stein-, Eisen- und Pechergebnisse des aktuellen offenen Laufs stimmten bereits mit den korrigierten Ressourcenprädikaten überein.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0`: Die fehlgeschlagenen Fünf-Zellen-Suchen erreichen diese Pfade nicht.
- Fehlende oder funktionslose Torhäuser.
- Der AIV-100×100-Bereich; externe Wirtschaftsbauten verwenden eine andere Suche.
- Ein dauerhaft hängender Cooldown; nach seinem Ablauf scheitern neue Traversierungen erneut am Raster.
- Eine feste Spieler-ID oder Farbe für die Torhausrolle.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen; diese Hypothese wurde noch nicht isoliert getestet.

## Aktive Fixerprobung in PreplacedTest 0.1.1

1. Der Ruinenfix normalisiert ausschließlich den oben beschriebenen streng belegten Startwert.
2. Vor jeder synchronen KI-Wirtschaftssuche wird nur `byte+04` temporär aus den laut Vanillas eigener `ExcludeLadderClimb`-Routenabfrage für diesen Spieler erreichbaren PCLs gebildet.
3. Nach dem Originalaufruf werden alle 25.600 Werte bytegenau restauriert. Alle übrigen Vanilla-Felder und Prüfungen bleiben bestehen.
4. `PreplacedTest` ist während dieser Erprobung gameplayverändernd und verwendet deshalb `NetworkMode=1`. Eine Übernahme nach `BugfixesAndQoL` erfolgt erst nach erfolgreicher Laufzeitabnahme.

## Noch benötigte Bestätigung

- Auf der Ruinenkarte muss `LEGACY_TIMER_FIX_APPLIED` erscheinen und der bisherige 49-Tick-AIV-Startdelay ausbleiben.
- Auf der Mauerkarte muss die Torhaus-KI außerhalb bauen können, während die torlose KI weiterhin eingeschlossen bleibt.
- Jede Overlaymeldung muss `restoredExactly=true` ausgeben; ein Mauerdurchbruch oder militärischer Angriff ist für diese Abnahme nicht erforderlich.
