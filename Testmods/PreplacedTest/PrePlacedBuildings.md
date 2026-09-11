# Vorplatzierte Gebäude und Vanilla-KI – aktueller Wissensstand

- Stand: 12. September 2026
- Native Version: `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Getesteter Script Extender: 2.5.0
- Wesentliche Laufzeitevidenz: `Log_057.log`, `Log_058.log`, die aktiven Ruinen-/Mauerläufe vom 11. und 12. September 2026 sowie `SHCDESE-crash-2026-09-11-20-27-48-152-pid48388-tid50040.log`

Aktiv waren bei den maßgeblichen Tests nur UU-ImGUI, Script Extender und `PreplacedTest`. Spieler-IDs und Farben sind keine festen Testrollen. Torhaus-KI und torlose Kontroll-KI werden bei jedem Kartenstart aus den aktuellen Gebäude- und Tile-Daten neu bestimmt.

## Gesicherte Erkenntnisse

### Der Ruinen-Delay stammt aus einem serialisierten Altformat-Spielerrecord

Auf der Ruinenkarte existieren fünf vorplatzierte Records der benannten Typen `STRUCT_TOWER1_DESTROYED` bis `STRUCT_TOWER5_DESTROYED`. Der zugehörige Spieler erhält beim Start `crushed_building_delay=1`; der normale Scheduler wartet anschließend bis zum Vanilla-Grenzwert und beginnt dadurch ungefähr 49 Ticks später mit der AIV-Ausführung.

Der beobachtete Altformatpfad `FUN_1800D4290` erzeugt diesen Wert nicht. Er übernimmt ihn aus `record+0x2AE0` in den aktuellen `0x583C`-Laufzeitrecord. Im belegten frischen Kartenstart war der serialisierte Quellwert stabil `1`, das Zielfeld wechselte ausschließlich durch diesen Transfer von `0` auf `1`, und es gab zuvor keinen echten Schadensschreiber. `0x50720`, `0x15B90`, `0x1F5F0` und der spätere Schadenspfad bei `0x7F074` sind für diesen Lauf als Aktivierungsquelle ausgeschlossen.

Die zerstörten Turmtypen können entgegen der früheren Annahme mit `AliveState.IsAlive` im Building-Array stehen. `AliveState` beschreibt hier also nicht zuverlässig, ob der fachliche Record eine Ruine ist. Ein Ruinenfix muss die fünf expliziten `eStructs`-Typen und eine stabile Game-/Global-ID verwenden und darf diese Records nicht wegen `IsAlive` verwerfen.

Während der Initialisierung kann der Building-Besitzer später umgeschrieben werden. Maßgeblich ist deshalb der Besitzer des stabil identifizierten Records zum Zeitpunkt des Altformattransfers, nicht dessen später sichtbarer Owner. Der aktive Testfix bleibt auf frische Altformatkarten ohne Save, Quellwert `1`, Zielwechsel `0→1`, bestätigte spätere KI-Zuordnung, weiterhin exakten Timerwert `1`, passende stabile Turmruinenidentitäten und das Ausbleiben eines echten Schadensschreibers begrenzt. Serialisierte Daten und Building-Records werden nicht verändert.

Der anschließende Langzeittest bestätigte außerdem zwei echte spätere Gebäudeverluste im Kampf. Die normale Schadens- und Verlustverarbeitung blieb aktiv, und weder der frühere Sprungziel-Crash noch ein anderer Diagnosefehler trat auf. Der beobachtete Timer blieb in diesem Lauf bei `0`; der Lauf belegt daher die Crashfreiheit bei den beiden Verlusten, aber nicht zusätzlich eine konkrete spätere Timeraktivierung. Der eng begrenzte Ruinen-Startfix gilt damit als praktisch bestätigt; die weitere Arbeit betrifft das Wirtschaftsraster.

### Das Wirtschaftsraster ist global statt spielerspezifisch klassifiziert

`FUN_1800572B0` wählt die häufigste positive PCL der gesamten Karte. `FUN_180050720` speichert sie beim Vollaufbau in `state+0x5B504`. `byte+04` jeder 5×5-Grobrasterzelle zählt danach die Tiles, deren PCL von dieser einen globalen Referenz abweicht. Freundliche, für Einheiten funktionierende Torportale ändern diese Klassifikation nicht.

Externe Wirtschaftsgebäude verwenden nicht je Gebäudetyp einen unabhängigen Algorithmus, sondern vier gemeinsame Suchfamilien:

- Farmen: `0x575B0`
- Steinbruch, Eisenmine und Pechgrube: `0x57B80` mit Modus 2, 3 beziehungsweise 4
- Holzfäller: `0x58020`
- Nahsuche für Holzfäller, Ochsenjoch und Steinbruch: `0x58950`

`byte+16` ist kein zweiter frei ersetzbarer PCL-Wert. `0x50720` erzeugt es aus eigenen Tile-Eigenschaften; der aktive Testfix lässt es deshalb unverändert. Die Ressourcenfamilie verwendet die Differenz von `byte+04` und `byte+16` sowie danach Belegung, Blocker, Spielerklasse, Ressourcendichte und Höhe. Der Höhenvergleich ist `byte+0D - byte+0C` kleiner als 40, 30 oder 12 für Stein, Eisen oder Pech. Dichtebytes werden vorzeichenbehaftet verglichen.

### Die spätere Platzsuche ist nicht der erste betroffene Wirtschaftspfad

Die vollständige Baselineprüfung zeigt vor den vier eigentlichen Suchfamilien einen entscheidenden weiteren Verbraucher. Der Kartenstart `0x94350` ruft nach dem vollständigen Rasteraufbau `0x50720` und der Spielerpfadinitialisierung `0x2A340` für jeden passenden Spieler `0x55FE0` auf. Diese Funktion beginnt ebenfalls exakt bei den Spielerfeldern `0x379AFA8/0x379AFAC`, traversiert bis Tiefe 60 über die vier orthogonalen Nachbarn und lässt eine Zelle nur bei der vorzeichenbehafteten Bedingung `byte+04 - byte+16 < 16` in die Queue.

`0x55FE0` zählt während dieser Traversierung die Verfügbarkeit aller fünf externen Wirtschaftsgruppen. Die Ergebnisse stehen im aktuellen Spielerrecord bei `+0x1686` bis `+0x168E`; die zugehörigen Suchfreigaben beziehungsweise Cooldowns bei `+0x167C` bis `+0x1684` werden zunächst auf `-1` gesetzt und nur bei mindestens einem gefundenen Standort auf `0` freigegeben. Die später beobachteten Dispatcherwerte `desired=0` können daher bereits durch die globale PCL-Klassifikation dieses Start-Census entstehen. Ein Fix ausschließlich in `0x575B0`, `0x57B80`, `0x58020` und `0x58950` wäre unvollständig, weil diese Suchen bei fehlender vorgelagerter Verfügbarkeit gar nicht erst aufgerufen werden.

Der Kartenstart-Census darf jedoch nicht sofort mit dem Overlay wiederholt werden: Im aktuellen Lauf war der native Portalgraph bei diesem Zeitpunkt noch nicht betriebsbereit. Die Torhaus-KI sah zunächst nur ihre Burg-PCL; erst rund 1,7 Sekunden später erkannte Vanillas `ExcludeLadderClimb`-Abfrage beim ersten Wirtschaftssuchaufruf die zusätzlichen Portal-PCLs. Der aktive Testfix bewahrt deshalb den frühen Vanilla-Census unverändert und wiederholt `0x55FE0` genau einmal, sobald eine spätere echte Wirtschaftssuche die belastbare Portalroute nachweist. Um diesen verzögerten Re-Census liegt das identische temporäre spielerspezifische `byte+04`-Overlay. Alle zehn Ergebnisfelder werden vor und nach dem Census korreliert; `byte+16`, Queue, Besuchsgeneration und sämtliche anderen Zellbytes bleiben unverändert, und `byte+04` wird im `finally`-Pfad bytegenau restauriert.

Eine vollständige XRef-Prüfung der Baseline findet außer Rasteraufbau, Census und den vier Wirtschaftssuchen noch zwei Leser von `byte+04`: `0x583A0` wird ausschließlich aus dem alternativen AIV-Ausführungspfad `0x52270` aufgerufen und sucht eine freie 3×3-Fläche; `0x58BE0` wird ausschließlich aus `0x54CC0` aufgerufen und prüft freie AIV-Platzierungsflächen zusätzlich selbst über `0xE2610`. Beide gehören zur AIV-Layoutplatzierung, nicht zur externen Wirtschaftsentscheidung. Sie werden als separate Verträge überwacht, aber vom eng begrenzten Wirtschaftsfix bewusst nicht verändert.

### Das aktive Overlay ermöglicht der Torhaus-KI externe Wirtschaftsbauten

Im neuesten Mauerlauf war Spieler 7 die dynamisch ermittelte Torhaus-KI. Sie baute um `20:27:10.435` einen Holzfäller bei `(500,375)` und um `20:27:25.423` einen zweiten bei `(505,375)`. Der erste Holzfäller entstand etwa sieben Sekunden vor dem bestätigten Durchbruch der Einfassung von Spieler 8 ohne Torhäuser um `20:27:17.024`. Damit liegt erstmals positive Laufzeitevidenz vor, dass das spielerspezifische `byte+04`-Overlay die Holzsuche der Torhaus-KI über die vorplatzierten Portale hinaus erweitert.

Der Mauerlauf vom 12. September bestätigt die praktische Wirkung erneut und deutlicher: Die visuell identifizierte KI mit den vier eigenen vorplatzierten Torhäusern platzierte bereits vor einem Durchbruch Wirtschaftsgebäude außerhalb ihrer Einfassung. Die Portalaktivierung und der verzögerte Re-Census funktionieren damit für den Torhausfall. Der Lauf war wegen der damaligen Diagnosemenge noch nicht lang genug, um den Durchbruchspfad der torlosen Kontrolle abzunehmen.

Dabei wurde eine wichtige Abgrenzung korrigiert. `OnStartMap Post` ist zu spät, um die Herkunft eines Gebäudes festzulegen: Zu diesem Zeitpunkt können erste AIV-Torhäuser und Burgmauern bereits existieren. Im Lauf wurden solche AIV-Tore irrtümlich als vorplatziert klassifiziert, wodurch auch die eigentlich torlose KI einen Portalzustand erhalten konnte.

Der erste Lauf mit der daraufhin zunächst nach `OnLoadMap Post` verschobenen Erfassung belegte eine unerwartete Extender-Ereignisreihenfolge: `OnStartMap Post` und damit die Profilauswertung liefen um `00:51:28.618`, `OnLoadMap Post` aber erst um `00:51:28.633`. Der Fix blieb deshalb vollständig inaktiv (`walledEconomy=False`, keine Fixzustände, kein Re-Census und kein Overlay). Sämtliche Wirtschaftszweige endeten trotz Nachfrage vor der eigentlichen Suche.

Der native Diagnosepfad liefert den korrekten früheren Grenzpunkt. Beim ersten Eintritt in `0x50680` um `00:51:28.578` existierten genau die vier kartenplatzierten Torhäuser mit IDs `19..22`; die späteren AIV-Burggebäude waren noch nicht vorhanden. Baselineidentitäten und Wall-Tiles werden deshalb nun unmittelbar vor dem ersten Originalaufruf von `AllocateSpec` einmalig festgehalten. `OnLoadMap Post` ist nur ein fail-closed Fallback und darf eine vorhandene frühe Baseline nicht überschreiben. Bei `OnStartMap Post` werden die Records über stabile Game-ID, Global-ID und Typ mit dem aktuellen Besitzer abgeglichen. Spätere AIV-Bauten können den Vorplatzierungsfix damit nicht mehr aktivieren.

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
2. Jede KI besitzt einen kartenlokalen Zustand `None`, `PendingPortal`, `PendingBreach`, `ActivePortal`, `ActiveBreach` oder `Suspended`. Saves und offene KIs bleiben `None`.
3. Eine KI mit eigenem oder aktuell verbündetem vorplatziertem Tor bleibt zunächst `PendingPortal`. Erst eine spätere echte Wirtschaftssuche darf sie nach einer von Vanillas `ExcludeLadderClimb`-Route belegten Portalverbindung aktivieren.
4. Eine torlos eingeschlossene KI bleibt als `PendingBreach` vollständig Vanilla. Erst wenn ein Baseline-Mauertile seinen blockierenden Zustand verliert, Innen- und Außenraum physisch verbunden sind und die spielerspezifische PCL-Reichweite die Außenseite erreicht, darf sie `ActiveBreach` werden. Eine dabei entstehende gemeinsame PCL ist ausdrücklich zulässig; bloße PCL-Neunummerierung, Schaden oder Toranimation reichen nicht.
5. Bei der ersten Aktivierung wird `byte+04` temporär spielerspezifisch projiziert, `0x55FE0` genau einmal erneut ausgeführt und das Raster bytegenau restauriert. Erst danach läuft die bereits betretene Originalsuche genau einmal mit demselben normalen Overlay.
6. Als erreichbar gelten nur PCLs, für die Vanillas eigene Routenabfrage vom Burg-PCL mit `ExcludeLadderClimb` einen positiven nächsten Schritt liefert. Leiterpfade, feindliche Tore, nur später von der AIV gebaute Tore und echte Isolation aktivieren den Portalpfad nicht.
7. Verliert eine aktive Portal-KI ihre Route oder wird der bestätigte Durchbruch wieder unzugänglich, wird der Fix ausgesetzt. Eine später wieder gültige Topologie kann einen neuen Re-Census auslösen. Vertrags-, Verschachtelungs- oder Restaurierungsfehler deaktivieren den Wirtschaftstestfix prozessweit.
8. Projektionen werden pro Spieler und Topologie-/Portalsignatur wiederverwendet. Vanillas aktuelles `byte+04` wird bei jedem Originalaufruf weiterhin separat gesichert und vollständig restauriert; gewöhnliche Abbauwerte erzwingen aber keine erneute 320.800-Tile-Analyse.
9. `PreplacedTest` ist dadurch gameplayverändernd und verwendet `NetworkMode=1`. Eine Übernahme nach `BugfixesAndQoL` erfolgt erst nach erfolgreicher Laufzeitabnahme.

Die frühere Vollanalyse jeder Wirtschaftssuche war für die Ursachenfindung nützlich, erzeugte auf der Mauerkarte aber erhebliche Last. Im aktuellen Testprofil werden vollständige Routing-, Portal-, Traversierungs- und Shadow-Ausgaben nicht mehr aus Hot Paths aufgerufen. AIV-Zuweisung und Platzierungsfestlegung erzeugen ebenfalls keine vollständigen Building-Inventare mehr. Such-, Validator-, Bau- und native Konstruktionsereignisse werden mit stabilen Schlüsseln ohne wechselnde Frame-, Tile- oder Positionswerte aggregiert; periodische Intervalle laufen im Mauerprofil alle fünf Sekunden. Die Durchbruchüberwachung prüft im Normalfall nur Baseline-Mauertiles und feste Anker. Der teurere physische Flood-Fill läuft erst, wenn sowohl echter Tileverlust als auch ein passender stabiler PCL-Anker eine mögliche Öffnung melden.

## Nicht als Ursache bestätigt

- Platzierungsvalidator oder `FUN_1800C3BF0` als primäre Ursache der frühen Fünf-Zellen-Abbrüche; diese Pfade werden häufig gar nicht erreicht.
- Der AIV-100×100-Bereich; externe Wirtschaftsbauten verwenden die oben genannten 160×160-Suchen.
- Ein dauerhaft hängender Cooldown; nach Ablauf scheitern neue Traversierungen mit demselben Rasterzustand erneut.
- Eine feste Spieler-ID, Farbe oder Portal-Owner-Rohwertsemantik.
- Vorplatzierte Gebäude in globalen KI-Sollzählungen; diese Hypothese wurde nicht isoliert belegt und wird nicht aktiv korrigiert.

## Nächste Laufzeitabnahme

- Ruinenkarte: `LEGACY_TIMER_FIX_APPLIED` muss erscheinen und die betroffene KI ohne 49-Tick-Sperre beginnen.
- Mauerkarte: Die dynamisch identifizierte Torhaus-KI muss zunächst `PendingPortal` und beim ersten belastbaren späteren Routennachweis `ActivePortal` erreichen. `ECONOMY_CENSUS_RECONCILED` muss dabei die zehn Verfügbarkeits-/Cooldownfelder sowie eine exakte Rasterrestaurierung ausweisen. Die torlose Kontrolle bleibt davor Vanilla.
- Nach einem sichtbaren Durchbruch der torlosen KI müssen Baseline-Tileverlust, physische Innen-/Außenverbindung und PCL-Reichweitengewinn gemeinsam `CONFIRMED_WALL_BREACH` und danach `ActiveBreach` auslösen. Der erneute Census muss externe Verfügbarkeiten freigeben; anschließend soll die KI außerhalb wirtschaftlich bauen.
- Jede Overlaymeldung muss `restoredExactly=true` ausgeben. Erst danach sind Ruinen- und Wirtschaftskorrektur für eine getrennt schaltbare Übernahme nach `BugfixesAndQoL` freigegeben.
