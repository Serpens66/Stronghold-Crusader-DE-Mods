# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 11. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Getesteter Script Extender: 2.5.0
- Wesentliche Laufzeitevidenz: `Log_057.log`, der aktive Ruinen-/Mauerlauf vom 11. September 2026 ab 20:25 Uhr sowie `SHCDESE-crash-2026-09-11-20-27-48-152-pid48388-tid50040.log`

Aktiv waren bei den maßgeblichen Tests nur UU-ImGUI, Script Extender und `PreplacedTest`. Spieler-IDs und Farben sind keine festen Testrollen. Torhaus-KI und torlose Kontroll-KI werden bei jedem Kartenstart aus den aktuellen Gebäude- und Tile-Daten neu bestimmt.

## Gesicherte Erkenntnisse

### Der Ruinen-Delay stammt aus einem serialisierten Altformat-Spielerrecord

Auf der Ruinenkarte existieren fünf vorplatzierte Records der benannten Typen `STRUCT_TOWER1_DESTROYED` bis `STRUCT_TOWER5_DESTROYED`. Der zugehörige Spieler erhält beim Start `crushed_building_delay=1`; der normale Scheduler wartet anschließend bis zum Vanilla-Grenzwert und beginnt dadurch ungefähr 49 Ticks später mit der AIV-Ausführung.

Der beobachtete Altformatpfad `FUN_1800D4290` erzeugt diesen Wert nicht. Er übernimmt ihn aus `record+0x2AE0` in den aktuellen `0x583C`-Laufzeitrecord. Im belegten frischen Kartenstart war der serialisierte Quellwert stabil `1`, das Zielfeld wechselte ausschließlich durch diesen Transfer von `0` auf `1`, und es gab zuvor keinen echten Schadensschreiber. `0x50720`, `0x15B90`, `0x1F5F0` und der spätere Schadenspfad bei `0x7F074` sind für diesen Lauf als Aktivierungsquelle ausgeschlossen.

Die zerstörten Turmtypen können entgegen der früheren Annahme mit `AliveState.IsAlive` im Building-Array stehen. `AliveState` beschreibt hier also nicht zuverlässig, ob der fachliche Record eine Ruine ist. Ein Ruinenfix muss die fünf expliziten `eStructs`-Typen und eine stabile Game-/Global-ID verwenden und darf diese Records nicht wegen `IsAlive` verwerfen.

Während der Initialisierung kann der Building-Besitzer später umgeschrieben werden. Maßgeblich ist deshalb der Besitzer des stabil identifizierten Records zum Zeitpunkt des Altformattransfers, nicht dessen später sichtbarer Owner. Der aktive Testfix bleibt auf frische Altformatkarten ohne Save, Quellwert `1`, Zielwechsel `0→1`, bestätigte spätere KI-Zuordnung, weiterhin exakten Timerwert `1`, passende stabile Turmruinenidentitäten und das Ausbleiben eines echten Schadensschreibers begrenzt. Serialisierte Daten und Building-Records werden nicht verändert.

### Das Wirtschaftsraster ist global statt spielerspezifisch klassifiziert

`FUN_1800572B0` wählt die häufigste positive PCL der gesamten Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. `byte+04` jeder 5×5-Grobrasterzelle zählt danach die Tiles, deren PCL von dieser einen globalen Referenz abweicht. Freundliche, für Einheiten funktionierende Torportale ändern diese Klassifikation nicht.

Externe Wirtschaftsgebäude verwenden nicht je Gebäudetyp einen unabhängigen Algorithmus, sondern vier gemeinsame Suchfamilien:

- Farmen: `0x575B0`
- Steinbruch, Eisenmine und Pechgrube: `0x57B80` mit Modus 2, 3 beziehungsweise 4
- Holzfäller: `0x58020`
- Nahsuche für Holzfäller, Ochsenjoch und Steinbruch: `0x58950`

`byte+16` ist kein zweiter frei ersetzbarer PCL-Wert. `0x50720` erzeugt es aus eigenen Tile-Eigenschaften; der aktive Testfix lässt es deshalb unverändert. Die Ressourcenfamilie verwendet die Differenz von `byte+04` und `byte+16` sowie danach Belegung, Blocker, Spielerklasse, Ressourcendichte und Höhe. Der Höhenvergleich ist `byte+0D - byte+0C` kleiner als 40, 30 oder 12 für Stein, Eisen oder Pech. Dichtebytes werden vorzeichenbehaftet verglichen.

### Das aktive Overlay ermöglichte erstmals einen externen Holzfäller vor einem fremden Durchbruch

Im neuesten Mauerlauf war Spieler 7 die dynamisch ermittelte Torhaus-KI. Sie baute um `20:27:10.435` einen Holzfäller bei `(500,375)` und um `20:27:25.423` einen zweiten bei `(505,375)`. Der erste Holzfäller entstand etwa sieben Sekunden vor dem bestätigten Durchbruch der Einfassung von Spieler 8 ohne Torhäuser um `20:27:17.024`. Damit liegt erstmals positive Laufzeitevidenz vor, dass das spielerspezifische `byte+04`-Overlay die Holzsuche der Torhaus-KI über die vorplatzierten Portale hinaus erweitert.

Farm-, Steinbruch-, Eisen- und Pechsuche waren in diesem Lauf keine negativen Gegenproben: Die nativen Eintrittsdaten meldeten jeweils `built=0, desired=0`, weshalb Vanilla diese Suchen legitim vor der Traversierung beendete. Sie müssen bei einem Test mit echter Nachfrage oder über die proaktiven Modelle beurteilt werden.

Nach dem Durchbruch der torlosen KI lief deren echte Holzsuche noch rund 30 Sekunden mehrfach mit Ergebnis `(-1,-1)` weiter. In dieser kurzen Zeit entstand kein Holzfäller. Das kann ein noch unvollständiges Modell, die native Kandidatenbewertung oder den Such-Cooldown betreffen und ist noch kein Beleg gegen die physische Öffnung.

Die bisherige Gegenrechnung mit einem manuell rekonstruierten Portalgraphen war nützlich, aber kein Beweis für Vanillas spielerspezifische Entscheidung. Der aktive Overlaypfad verwendet deshalb ausschließlich `GamePathingManagerAPI.FindNextComponentTowardDestination` mit `PathConnectionQueryMode.ExcludeLadderClimb`. Eine neue Routenmatrix vergleicht zusätzlich `IncludeAll`, `LadderClimbOnly` und die umgekehrte Richtung, ohne diese Vergleichswerte für den Fix zu verwenden.

### Farmen benötigen ein eigenes Laufzeitorakel

`0x575B0` schreibt eine gefundene Farmposition nicht in das gemeinsame Ergebnisfeld. Die früheren Farm-Mismatch-Meldungen mit diesem Feld waren ungültig. Ein akzeptierter Farmtreffer ist jetzt ausschließlich ein innerhalb desselben `0x575B0`-Aufrufs beobachteter `0x6D580`-Aufruf mit Vanilla-Fehlerwert `0` und passendem Building-Delta. Abgelehnte Konstruktionsversuche werden separat erhalten.

Die Offsettabelle bei RVA `0x2D13B0` besitzt 32 Koordinatenpaare. Der einzige belegte Writer in `0x57330` erhöht den Ringindex in `state+0x5B508`, vergleicht ihn mit 31 und setzt ihn ab 31 auf 0; regulär auswählbar sind damit die Indizes `0..30`. Das letzte Tabellenpaar mit Index 31 wird durch diesen Pfad nicht gewählt. Die frühere Diagnosegrenze von neun Einträgen war ebenfalls falsch. Vor jedem korrelierten Farm-Konstruktionsaufruf werden Ringindex, tatsächliches Offset, Grobrasterzelle, Rohfelder und die vier Verfügbarkeitsmasken gesichert. Solange noch kein akzeptiertes Native-Orakel sämtliche Farmzweige bestätigt, heißen rein aus Rasterfeldern ermittelte Treffer ausdrücklich `farm-prefilter`.

### Ressourcen- und Holzergebnisse müssen vor der Overlay-Restaurierung geprüft werden

Das gemeinsame Ergebnisfeld von `0x57B80` und `0x58020` bezeichnet den nativen Treffer. Sein Zellzustand muss unmittelbar nach dem Originalaufruf gelesen werden, solange das temporäre `byte+04`-Overlay noch aktiv ist. Ein Vergleich nach Restaurierung oder nach einer Konstruktion kann ein anderes Raster abbilden und war deshalb nicht belastbar. Der Testmod erfasst diese Orakel nun vor dem `finally`-Restaurierungspfad und protokolliert den ersten abweichenden Vanilla-Zweig.

Das bisherige Shadow-Modell meldete für beide KIs schon deutlich vor Vanillas erstem Treffer zahlreiche Holzkandidaten. Vanilla gab zu diesen Zeitpunkten dennoch `(-1,-1)` zurück. Die alte Diagnose prüfte nur, ob eine von Vanilla gelieferte Ergebniszelle das Shadow-Prädikat erfüllt, nicht den umgekehrten Widerspruch. Die neue Differentialdiagnose vergleicht deshalb native Besuchsmarker, Tiefenwerte und Queue-Reihenfolge mit der Shadow-Ausführung und meldet `SHADOW_NATIVE_NO_RESULT_MISMATCH`, sobald Shadow einen Kandidaten hat, Vanilla aber keinen auswählt.

### Der Crash um 20:27:48 wurde durch einen unsicheren Diagnosehook verursacht

Der tödliche Schaden am zweiten Holzfäller aktivierte für dessen Besitzer den normalen Verlusttimer. Der Crashdump endet exakt bei `CrusaderDE.dll+0x7F07C`. Unser damaliger RedBird-Context-Hook begann bei `0x7F074` und verdrängte 15 Byte bis einschließlich `0x7F082`. Vanilla besitzt jedoch bei `0x7F05D` und `0x7F072` zwei bedingte Sprünge direkt nach `0x7F07C`, also mitten in diesen überschriebenen Bereich. Ein genommener Originalzweig sprang dadurch in den Hookstub und verursachte den Nullzugriff.

Dieser Crash stammte ausschließlich aus der Diagnose. Weder die enge Ruinen-Timernormalisierung noch das temporäre Wirtschaftsraster-Overlay lagen auf dem abstürzenden Kontrollflusspfad. Der Inline-Hook bei `0x7F074` ist vollständig entfernt. Schadensereignisse des Script Extenders und die bereits vorhandenen Timervergleiche bleiben als passive Verlustkontrolle erhalten. Ein statischer Regressionstest verwirft künftig jeden Inline-Hook-Span, in dessen Inneres ein nativer Sprung zielt.

## Aktive Fixerprobung in PreplacedTest 0.1.1

1. Der Ruinenfix normalisiert ausschließlich den eng belegten, aus einer passenden Altformat-Turmruinenbaseline übernommenen Startwert.
2. Vor einer eindeutig einem KI-Spieler zugeordneten Suche wird nur `byte+04` der 25.600 Wirtschaftszellen temporär spielerspezifisch neu gebildet.
3. Als erreichbar gelten nur PCLs, für die Vanillas eigene Routenabfrage vom Burg-PCL mit `ExcludeLadderClimb` einen positiven nächsten Schritt liefert. Eigene oder nach Vanilla zulässige verbündete Portale können die Menge erweitern; Leiterpfade, feindliche Tore und echte Isolation bleiben ausgeschlossen.
4. Jede originale Suchfunktion läuft genau einmal. Noch vor der Restaurierung werden Treffer und Rohfelder gesichert; anschließend werden alle 25.600 `byte+04`-Werte bytegenau wiederhergestellt. Bei Vertrags- oder Restaurierungsfehler wird der Wirtschaftstestfix für den restlichen Prozess deaktiviert.
5. Native Besuchsmarker, Tiefenbytes und Queue-Reihenfolge werden unmittelbar nach der Originalsuche gesichert und gegen denselben Shadow-Eingang verglichen. Große Portal- und Ankerinventare erscheinen einmal vollständig; danach werden nur semantische Änderungen mit verlustfreien Wiederholungszählern ausgegeben.
6. `PreplacedTest` ist dadurch gameplayverändernd und verwendet `NetworkMode=1`. Eine Übernahme nach `BugfixesAndQoL` erfolgt erst nach erfolgreicher Laufzeitabnahme.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0` als primäre Ursache der frühen Fünf-Zellen-Abbrüche; diese Pfade werden häufig gar nicht erreicht.
- Der AIV-100×100-Bereich; externe Wirtschaftsbauten verwenden die oben genannten 160×160-Suchen.
- Ein dauerhaft hängender Cooldown; nach Ablauf scheitern neue Traversierungen mit demselben Rasterzustand erneut.
- Eine feste Spieler-ID, Farbe oder Portal-Owner-Rohwertsemantik.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen; diese Hypothese wurde nicht isoliert belegt und wird nicht aktiv korrigiert.

## Nächste Laufzeitabnahme

- Ruinenkarte: `LEGACY_TIMER_FIX_APPLIED` muss erscheinen und die betroffene KI ohne 49-Tick-Sperre beginnen.
- Mauerkarte: Ein einzelner Start genügt; ein Durchbruch ist nicht erforderlich. Die dynamisch identifizierte Torhaus-KI muss in der nativen Routenmatrix Zugriff auf die relevanten äußeren Kandidaten-PCLs erhalten, die torlose Kontrolle nicht.
- Der Lauf soll mindestens bis zu einer echten Holzsuche der Torhaus-KI reichen. Bei einem Widerspruch muss die Differentialdiagnose die erste abweichende Queuezelle, Besuchsmenge, Tiefe oder das Ergebnisverhältnis benennen.
- Die proaktiven Snapshotmodelle müssen für Holz und Ressourcen Kandidaten oder den exakten ersten Vanilla-Ablehnungsgrund melden, auch wenn die AIC aktuell `desired=0` setzt. Für Farmen liefert der Snapshot einen ausdrücklich so benannten Präfilter; die vollständige Bestätigung erfolgt am korrelierten nativen Konstruktionsaufruf.
- Jede Overlaymeldung muss `restoredExactly=true` ausgeben. Erst danach sind Ruinen- und Wirtschaftskorrektur für eine getrennt schaltbare Übernahme nach `BugfixesAndQoL` freigegeben.
