# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 11. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Getesteter Script Extender: 2.5.0

Maßgebliche Evidenz ist der saubere Prozesslauf im `BepInEx\LogOutput.log` vom 11. September 2026, 14:13:02 bis 14:16:44. Frühere Läufe werden nur dort weiter berücksichtigt, wo sie mit diesem Stand vereinbar sind. Aktiv waren UU-ImGUI, Script Extender und `PreplacedTest`.

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

Ein sicherer späterer Fix kann den übernommenen Wert auf einer frisch gestarteten Altformatkarte nach dem Transfer normalisieren, wenn das Laufzeitfeld vorher null war und ausschließlich durch den stabilen serialisierten Einzelaktivierungswert `1` aktiviert wurde. Geladene Spielstände, bereits aktive Laufzeitwerte, abweichende Quellwerte und spätere echte Verluste müssen unverändert bleiben.

### Das Wirtschaftsraster verwendet eine globale statt spielerspezifische PCL-Referenz

`FUN_1800572B0` wählt die häufigste positive PCL der gesamten Karte. `FUN_180050720` speichert sie bei einem Vollaufbau in `state+0x5B504`. `byte+04` jeder 5×5-Grobrasterzelle zählt anschließend die Tiles, deren PCL von dieser globalen Referenz abweicht.

Die Wirtschaftssuchen behandeln `byte+04` als Durchquerbarkeitswert:

- Farm (`0x575B0`): Expansion bei `signed byte+04 < 17`.
- Holz (`0x58020`): Expansion bei `signed byte+04 < 16` und `byte+13 == 0`.
- Nahbereich (`0x58950`): Expansion bei `signed byte+04 < 15`; Kandidaten verlangen `byte+04 == 0` und weitere Bedingungen.
- Ressourcen (`0x57B80`): Expansion bei `signed byte+04 - signed byte+16 < 16`; Kandidaten verlangen Gleichheit beziehungsweise beim Eisenmodus eine Differenz kleiner 5 sowie weitere Vanilla-Bedingungen.

Die bisherige Shadow-Diagnose hatte den abschließenden Höhenvergleich für Stein, Eisen und Pech umgekehrt. Vanilla akzeptiert die Zelle bei ausreichender Dichte, wenn die vorzeichenbehaftete Differenz `byte+0D - byte+0C` kleiner als 40, 30 beziehungsweise 12 ist. Dieser Diagnosefehler ist im aktuellen Quellstand korrigiert; die vorher protokollierten null Rohstoffkandidaten sind deshalb nicht als Gegenbeleg verwendbar.

### Torhäuser funktionieren für Einheiten, aber nicht für die Wirtschafts-Rastersuche

Im Mauerlauf gehörten die vier intakten Torhäuser Spieler 8; Spieler 7 war die torlose Kontrolle. Diese IDs gelten nur für diesen Lauf. Rollen werden bei jedem Kartenstart dynamisch bestimmt.

Die spielerspezifische Gegenrechnung zeigte vor dem Durchbruch:

- Torhaus-KI: ungefähr 2.159 erreichbare Zellen, 425 Farm- und 47 Holzkandidaten.
- Torlose KI: ungefähr 909 eingeschlossene Zellen und keine Farm- oder Holzkandidaten.

Nach dem sichtbaren Durchbruch der torlosen KI sprang die Gegenrechnung zunächst auf ungefähr 2.244 erreichbare Zellen mit 388 Farm- und 84 Holzkandidaten und später auf rund 3.000 erreichbare Zellen. Eigene beziehungsweise nach Diplomatie verbündete Portalverbindungen sind damit für Farm und Holz ein stark belegtes spielerspezifisches Gegenmodell.

Vanilla verwendete diese Verbindung weiterhin nicht. Auch nach dem Durchbruch liefen echte Holzsuchen regelmäßig, besuchten aber erneut nur fünf Zellen um die Burg, fanden keinen Kandidaten und aktivierten den Cooldown. Identische Vollausgaben wurden aggregiert; die Intervall- und Gesamtsummen belegen die späteren Traversierungen.

Farm- und Rohstoffsuchen verließen die Funktion im relevanten Mauerabschnitt überwiegend mit `desired=0`. Der nächste Diagnosebuild führt deshalb alle drei Ressourcen-Shadow-Suchen unabhängig von der aktuellen AIC-Nachfrage aus.

### Der sichtbare Durchbruch ist in den Tile-Daten belegt

Bei Spieler 7 verloren mehrere Baseline-Mauertiles um 14:15:56 ihren Wall-Zustand. Die Shadow-Reichweite war bereits unmittelbar davor deutlich gewachsen. Dies stimmt mit dem sichtbaren einseitigen Durchbruch überein.

Die automatische Bestätigung blieb aus, weil das bisherige orthogonale Flood-Fill beide Seiten als verbunden bewertete und deshalb keine Anker erzeugte. Ein wahrscheinlicher konkreter Grund ist, dass Turmfootprints bislang nicht als Teile der Einfassung behandelt wurden. Der nächste Diagnosebuild vergleicht deshalb Wall-only- und wall-aware Flood-Fill, nimmt angrenzende Turm- und Portalfootprints auf und verwendet zusätzlich feste PCL-Ankertiles. Reine PCL-Neunummerierungen bleiben ausdrücklich kein Durchbruch.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0`: Die fehlgeschlagenen Fünf-Zellen-Suchen erreichen diese Pfade nicht.
- Fehlende oder funktionslose Torhäuser.
- Der AIV-100×100-Bereich; externe Wirtschaftsbauten verwenden eine andere Suche.
- Ein dauerhaft hängender Cooldown; nach seinem Ablauf scheitern neue Traversierungen erneut am Raster.
- Eine feste Spieler-ID oder Farbe für die Torhausrolle.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen; diese Hypothese wurde noch nicht isoliert getestet.

## Anforderungen an den späteren Gameplay-Fix

1. Der Ruinenfix normalisiert ausschließlich einen auf einer frischen Karte aus dem serialisierten Altformatrecord übernommenen Startwert. Savegames und spätere echte Verluste bleiben unverändert.
2. Der Mauerfix berücksichtigt pro aktueller KI direkt sowie über gültige eigene oder verbündete Portale erreichbare PCLs. Geschlossene Mauern, feindliche Tore und isolierte Regionen bleiben gesperrt.
3. Ressourcen-, Gelände-, Besitzerklassen-, Belegungs-, Höhen-, Platzierungs- und nachgeschaltete Erreichbarkeitsprüfungen bleiben erhalten.
4. Der Gameplay-Fix gehört mit `NetworkMode=1` in `BugfixesAndQoL`; `PreplacedTest` bleibt vollständig passiv.

## Noch benötigte Bestätigung

- Die passive Timer-Fixentscheidung muss auf Ruinenkarte und Nicht-Ruinenkarte erwartungsgemäß zwischen freigabefähig und unverändert unterscheiden.
- Korrigierte Stein-, Eisen- und Pech-Shadow-Kandidaten müssen gegen erfolgreiche Vanilla-Ergebniszellen geprüft werden.
- Wall-aware Flood-Fill und feste PCL-Anker müssen den sichtbaren Tileverlust als Durchbruch bestätigen, ohne Toranimationen oder PCL-Umlabelungen falsch zu melden.
