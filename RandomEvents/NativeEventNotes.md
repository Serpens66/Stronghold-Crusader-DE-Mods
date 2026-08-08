# Random Events – native Ereignisnotizen

## Referenz-DLL

- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`
- Die installierte `CrusaderDE.dll` ist die kanonische Quelle. RVAs sind nur zusammen mit diesem Hash feste Referenzen.
- Bei anderen DLLs werden die Funktionen über eindeutige semantische AOBs gesucht und strukturell gegeneinander validiert. Schlägt dies fehl, bleibt nur das betroffene Ereignis deaktiviert.

## Hasenplage

- Der Mod wählt eine zufällige lebende Getreide- oder Hopfenfarm des Zielspielers und innerhalb von 12 Tiles genau einen Vanilla-kompatiblen Quellpunkt.
- Statt roher Einzel-Units ruft er den gemeinsamen nativen Wildtierhandler mit Aktion `222` auf. Dieser erzeugt wie das Originalereignis einen richtigen Hasenstamm mit 14 bis 21 Tieren und registriert dessen Quelle; dadurch greifen Vanillas Verteilung und Farmfraß. Am gewählten Quellpunkt wird außerdem Vanillas ActionPoint eingereiht, damit das anklickbare Ausrufungszeichen dorthin springt.
- Vorher wird Vanillas Limit von 160 Hasen geprüft. Der originale 1200-Tick-Zustand und die Quellkoordinaten im Tribe-Manager werden wie im Vanilla-Wrapper gesetzt; anschließend werden die originale Video- und Sprachnachricht eingereiht.
- Handler RVA `0x11E0B0`, Prädikat RVA `0x117700` und Spawner RVA `0x123A20` gelten nur für die Referenz-DLL; Tile-Maske und Adressen werden aus semantisch validierten Codepfaden abgeleitet.

## Lebende Zielspieler

- `r_LordUnitId` dient nur als Verweis. Unmittelbar vor jeder Ereignisausführung muss die referenzierte Einheit auflösbar sein, `AliveState.IsAlive` besitzen, vom Typ `CHIMP_TYPE_LORD` sein und dem Zielspieler gehören.
- Fehlende PlayerResources, ein ungültiger Verweis, ein toter Lord oder eine falsche Einheit überspringen das Ereignis. Periodisches Lord-ID-Logging wurde entfernt.

## Löwenangriff

- Der Mod wählt aus Vanillas registrierten, lebenden Wegweisern denjenigen mit der geringsten Entfernung zur lebenden Burg des Zielspielers. Ist keine Burg nutzbar, dient der lebende Lord als Distanzanker.
- Auf dem nächstgelegenen Vanilla-kompatiblen Tile innerhalb von 12 Tiles um diesen Wegweiser wird der gemeinsame Wildtierhandler mit Aktion `221` einmal je Stärkepunkt aufgerufen.
- Für jeden erzeugten Stamm wird Vanillas ActionPoint-Handler mit dem Spawnpunkt aufgerufen; dadurch erscheint wieder das anklickbare Ausrufungszeichen und verwendet den originalen Kamera-Sprung. Danach erhält der Stamm denselben Aktivierungswert `0x10000`, den Vanillas Ereigniswrapper setzt. Die originale Sprachnachricht `Random_Events14.wav` wird ohne Video eingereiht, da die Installation kein Löwen-Ereignisvideo enthält.
- Stamm-Stride, Aktivierungsfeld, Tile-Maske und ActionPoint-Pfad werden über getrennte semantische Signaturen aufgelöst. Scheitert nach einem Update nur die ActionPoint-Auflösung, bleiben Löwenangriff und Nachricht aktiv und ausschließlich das Ausrufungszeichen wird mit einem Error deaktiviert.

## Erster Ereignistermin

- Der erste Termin bleibt Kartenstart plus das konfigurierte Monatsintervall. Ereigniswürfe werden jedoch erst vorbereitet, sobald mindestens ein aktiver menschlicher Spieler einen auflösbaren lebenden Lord hat.
- Damit kann die frühe Karteninitialisierung keinen leeren ersten Batch mehr erzeugen. Bei zwei Monaten Intervall wird der erste vorbereitete Batch nach zwei statt erst nach vier Monaten ausgeführt.

## Automatische Wegweiser

- Kandidaten werden zunächst auf eine freie, begehbare 2x2-Fläche gefiltert. `CreatePrefab(..., bypassPlacementRules: false)` führt danach Vanillas maßgebliche Gebäude-, Gelände- und Footprint-Prüfung aus.
- Verwirft Vanilla eine Position, wird der nächste bevorzugte Kandidat, danach jeder weitere vorgefilterte Kandidat und schließlich eine andere Randtiefe versucht. Ein unvollständig erzeugter neutraler Wegweiser wird sicher entfernt.

## Manuelle Banditen

- Teststand 1.0.28 erzeugt ausschließlich rohe Streitkolbenkämpfer mit Spielerfarbe und Owner `0` über `CreateUnitLocal`. Danach erfolgen keine Tribe-Erzeugung oder -Zuweisung, keine Haltungsänderung, kein Bewegungs- oder Angriffsbefehl, keine Zielzuweisung und keine spätere Überwachung oder Änderung. Damit isoliert der Test das Vanilla-Verhalten der Naturzugehörigkeit.
- Der Mittelpunkt eines Wegweiser-Footprints besitzt nicht zwingend eine native Pfadkomponente. Der tatsächliche Banditen-Spawn wird deshalb auf das nächstgelegene freie, begehbare Randtile mit einer Pfadkomponente ungleich `0` gelegt; dieselbe Komponente begrenzt anschließend die erreichbaren Ziele.
- Der native Stammesbefehl-Dispatcher liegt in der Referenz-DLL bei RVA `0x11E8C0`. Der vom Script Extender als `ForceAttackBuilding` bezeichnete Befehl `36` validiert seinen Zielwert im Zweig um RVA `0x11F0B6` mit dem Einheiten-Stride `0x490`; eine Gebäude-ID wird deshalb ohne Fehler, aber auch ohne Einheitenbefehl verworfen.
- Befehl `9` ist zwar ein Gebäudeangriff, sein Einheiten-Switch verwirft Streitkolbenkämpfer bei normalen Gebäudetypen. Auch Befehl `5` (`Attack Here`) lieferte zwar einen erfolgreichen API-Rückgabewert, setzte bei neutralen Streitkolbenkämpfern aber nachweislich keinen Bewegungs- oder Angriffskontext. Der einzelne Unit-`MoveHere`-Handler bewegte sie ohne Laufanimation. RandomEvents verwendet deshalb den vollständigen Tribe-`IssueMoveHereCommand` und schickt jede Gruppe auf ein freies, verbundenes Tile direkt neben ihrem zufälligen Ziel.
- Vanillas FreeBuild-Case `146` setzt vor dem Spawn für den lokalen Spieler einen 16-Bit-Ereignisstatus auf `16` (Write bei RVA `0x104C32`). Dieser Status erzeugt und beendet den zeitweiligen Popularitätsmalus über die normale Simulation. RandomEvents löst das Feld über eine semantische Signatur auf und setzt es stattdessen für die explizite menschliche Zielspieler-ID.
- Schlägt diese Prüfung nach Wiederholungen fehl, werden nur weitere Banditenereignisse für die laufende Karte deaktiviert. Die übrigen Zufallsereignisse und ihre Timer bleiben aktiv.

## Direkte Vanilla-Handler

- `GameTimeManagerAPI.OnTick` wird aus einem nativen Pre-Tick-Kontext vor der Zeit-/Datumsverarbeitung aufgerufen. Timeline-Vektoren dort zu verändern führte reproduzierbar zu einem nativen Zugriffsfehler beim Kartenstart.
- Ein eigenes, früh erzeugtes `DontDestroyOnLoad`-Objekt erhielt keine `LateUpdate()`-Callbacks. Auch der Script-Extender-`UnityMainThreadDispatcher` akzeptierte die Coroutine, führte aber in RandomEvents und SpawnCastle nie deren ersten Callback aus. Der gemeinsame Dispatcher und der davon abhängige Timeline-Pfad wurden deshalb entfernt.
- Die fünf Aktionen werden ohne Timeline-Eintrag über die Handler aus dem Vanilla-Switch ausgelöst. Vor jedem Effekt läuft dieselbe Vanilla-Voraussetzung: Farmtyp `30`, `31`, `32` oder `33` muss für den Zielspieler existieren; Kornspeicherdiebstahl verlangt eine gesetzte erste Kornspeicher-ID.
- Getreidebefall: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC30E0`. Hopfenkäfer: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2DE0`. Obstfäule: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2BE0`.
- Wahnsinnige Rinder führt nach RVA `0xB8D00` zuerst den Einheitenhandler RVA `0x194BA0` und danach den Gebäudehandler RVA `0xC6040` aus. Kornspeicherdiebstahl nutzt RVA `0xC5F20` mit Spieler-ID und Prozentstärke.
- Nach erfolgreichem Effekt wird Vanillas Nachrichtenhandler RVA `0x103110` verwendet. Videos und Sprachdateien entsprechen den Timeline-Cases; bei wahnsinnigen Rindern bleiben Vanillas getrennte Audio- und Videoeinträge erhalten.
- Auf dem Referenz-Build werden zuerst die erwarteten RVAs semantisch validiert. Nach kleinen Spielupdates sucht ein gemeinsamer Resolver nur in ausführbaren PE-Sektionen nach eindeutigen Signaturen und leitet relative Ziele weiterhin aus dem Code ab.
- Handler werden unabhängig aufgelöst. Ein geänderter Effekthandler deaktiviert deshalb nur die davon abhängigen Ereignisse; ein Fehler der gemeinsamen Gebäudeprüfung oder Nachrichten-Queue betrifft weiterhin alle Ereignisse, die diese Vanilla-Komponente benötigen.
