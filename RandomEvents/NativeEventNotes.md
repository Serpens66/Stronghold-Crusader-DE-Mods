# Random Events – native Ereignisnotizen

## Referenz-DLL

- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`
- Die installierte `CrusaderDE.dll` ist die kanonische Quelle. RVAs sind nur zusammen mit diesem Hash feste Referenzen.
- Bei anderen DLLs werden die Funktionen über eindeutige semantische AOBs gesucht und strukturell gegeneinander validiert. Schlägt dies fehl, bleibt nur das betroffene Ereignis deaktiviert.

## Hasenplage

- Der Mod wählt eine zufällige lebende Getreide- oder Hopfenfarm des Zielspielers und innerhalb von 12 Tiles genau einen Vanilla-kompatiblen Quellpunkt.
- Statt roher Einzel-Units ruft er den gemeinsamen nativen Wildtierhandler mit Aktion `222` auf. Dieser erzeugt wie das Originalereignis einen richtigen Hasenstamm mit 14 bis 21 Tieren und registriert dessen Quelle; dadurch greifen Vanillas Verteilung und Farmfraß.
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

## Direkte Vanilla-Handler

- `GameTimeManagerAPI.OnTick` wird aus einem nativen Pre-Tick-Kontext vor der Zeit-/Datumsverarbeitung aufgerufen. Timeline-Vektoren dort zu verändern führte reproduzierbar zu einem nativen Zugriffsfehler beim Kartenstart.
- Ein eigenes, früh erzeugtes `DontDestroyOnLoad`-Objekt erhielt keine `LateUpdate()`-Callbacks. Auch der Script-Extender-`UnityMainThreadDispatcher` akzeptierte die Coroutine, führte aber in RandomEvents und SpawnCastle nie deren ersten Callback aus. Der gemeinsame Dispatcher und der davon abhängige Timeline-Pfad wurden deshalb entfernt.
- Die fünf Aktionen werden ohne Timeline-Eintrag über die Handler aus dem Vanilla-Switch ausgelöst. Vor jedem Effekt läuft dieselbe Vanilla-Voraussetzung: Farmtyp `30`, `31`, `32` oder `33` muss für den Zielspieler existieren; Kornspeicherdiebstahl verlangt eine gesetzte erste Kornspeicher-ID.
- Getreidebefall: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC30E0`. Hopfenkäfer: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2DE0`. Obstfäule: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2BE0`.
- Wahnsinnige Rinder führt nach RVA `0xB8D00` zuerst den Einheitenhandler RVA `0x194BA0` und danach den Gebäudehandler RVA `0xC6040` aus. Kornspeicherdiebstahl nutzt RVA `0xC5F20` mit Spieler-ID und Prozentstärke.
- Nach erfolgreichem Effekt wird Vanillas Nachrichtenhandler RVA `0x103110` verwendet. Videos und Sprachdateien entsprechen den Timeline-Cases; bei wahnsinnigen Rindern bleiben Vanillas getrennte Audio- und Videoeinträge erhalten.
- Auf dem Referenz-Build werden zuerst die erwarteten RVAs semantisch validiert. Nach kleinen Spielupdates sucht ein gemeinsamer Resolver nur in ausführbaren PE-Sektionen nach eindeutigen Signaturen und leitet relative Ziele weiterhin aus dem Code ab.
- Handler werden unabhängig aufgelöst. Ein geänderter Effekthandler deaktiviert deshalb nur die davon abhängigen Ereignisse; ein Fehler der gemeinsamen Gebäudeprüfung oder Nachrichten-Queue betrifft weiterhin alle Ereignisse, die diese Vanilla-Komponente benötigen.
