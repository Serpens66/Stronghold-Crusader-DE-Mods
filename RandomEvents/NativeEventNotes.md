# Random Events – native Ereignisnotizen

## Referenz-DLL

- SHA-256: `1E6D4C2E10CC35A7B8082A7E2BCD8BB20680EBEDA803D9B943257B948145CB2B`
- Die installierte `CrusaderDE.dll` ist die kanonische Quelle. RVAs sind nur zusammen mit diesem Hash feste Referenzen.
- Bei anderen DLLs werden die Funktionen über eindeutige semantische AOBs gesucht und strukturell gegeneinander validiert. Schlägt dies fehl, bleibt nur das betroffene Ereignis deaktiviert.

## Hasenplage

- Die Vanilla-Aktion wird nicht mehr verwendet, weil sie trotz validierter Quellpunkte keine Hasen erzeugte.
- Der Mod wählt stattdessen eine zufällige lebende Getreide- oder Hopfenfarm des Zielspielers.
- Anschließend erzeugt er direkt 10 bis 50 neutrale Hasen auf zufälligen begehbaren, unbebauten Tiles in einem Radius von 12 Tiles um die Farm.
- Auswahl, Anzahl und Positionen verwenden den gespeicherten PRNG-Zustand des Mods.

## Direkte Vanilla-Handler

- `GameTimeManagerAPI.OnTick` wird aus einem nativen Pre-Tick-Kontext vor der Zeit-/Datumsverarbeitung aufgerufen. Timeline-Vektoren dort zu verändern führte reproduzierbar zu einem nativen Zugriffsfehler beim Kartenstart.
- Ein eigenes, früh erzeugtes `DontDestroyOnLoad`-Objekt erhielt keine `LateUpdate()`-Callbacks. Auch der Script-Extender-`UnityMainThreadDispatcher` akzeptierte die Coroutine, führte aber in RandomEvents und SpawnCastle nie deren ersten Callback aus. Der gemeinsame Dispatcher und der davon abhängige Timeline-Pfad wurden deshalb entfernt.
- Die fünf Aktionen werden ohne Timeline-Eintrag über die Handler aus dem Vanilla-Switch ausgelöst. Vor jedem Effekt läuft dieselbe Vanilla-Voraussetzung: Farmtyp `30`, `31`, `32` oder `33` muss für den Zielspieler existieren; Kornspeicherdiebstahl verlangt eine gesetzte erste Kornspeicher-ID.
- Getreidebefall: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC30E0`. Hopfenkäfer: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2DE0`. Obstfäule: Voraussetzung RVA `0xB8D00`, Effekt RVA `0xC2BE0`.
- Wahnsinnige Rinder führt nach RVA `0xB8D00` zuerst den Einheitenhandler RVA `0x194BA0` und danach den Gebäudehandler RVA `0xC6040` aus. Kornspeicherdiebstahl nutzt RVA `0xC5F20` mit Spieler-ID und Prozentstärke.
- Nach erfolgreichem Effekt wird Vanillas Nachrichtenhandler RVA `0x103110` verwendet. Videos und Sprachdateien entsprechen den Timeline-Cases; bei wahnsinnigen Rindern bleiben Vanillas getrennte Audio- und Videoeinträge erhalten.
- Alle Funktionen werden über semantische Signaturen validiert. Auf dem oben genannten Referenz-Build werden zusätzlich die erwarteten RVAs geprüft. Ein Fehler deaktiviert die fünf nativen Ereignisse sicher, während die übrigen Ereignisse aktiv bleiben.
