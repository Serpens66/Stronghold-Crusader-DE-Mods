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

## Timeline-Lifecycle

- `GameTimeManagerAPI.OnTick` wird aus einem nativen Pre-Tick-Kontext vor der Zeit-/Datumsverarbeitung aufgerufen. Timeline-Vektoren dort zu verändern führte reproduzierbar zu einem nativen Zugriffsfehler beim Kartenstart.
- Ein eigenes, früh erzeugtes `DontDestroyOnLoad`-Objekt erhielt keine `LateUpdate()`-Callbacks. Auch der Script-Extender-`UnityMainThreadDispatcher` akzeptierte die Coroutine, führte aber in RandomEvents und SpawnCastle nie deren ersten Callback aus. Der gemeinsame Dispatcher und der davon abhängige Timeline-Pfad wurden deshalb entfernt.
- Bis ein sicherer direkter Ausführungspfad geklärt ist, werden die Aktionen `12`, `13`, `14`, `19` und `29` nicht vorbereitet und erzeugen keine Timeline-Einträge. Ihre Action-IDs bleiben in `RandomEventDefinitions` als Analysezuordnung erhalten.

## Übergabe für die direkte Timeline-Analyse

- Timeline-Prozessor: RVA `0xF8260`, VA `0x1800F8260`; Aufruf aus der Simulationsroutine um RVA `0xCDE10`.
- Einträge sind `0xE4` Byte groß; Anzahl am Szenario-Manager bei `+0x660`, Vektor ab `+0x664`.
- Zu verfolgen sind die Switch-Cases `12` (Getreidebefall), `13` (Hopfenkäfer), `14` (Obstfäule), `19` (wahnsinnige Rinder) und `29` (Kornspeicherdiebstahl).
- Case `29` prüft den Kornspeicherzustand und ruft anschließend den möglichen Direkthandler RVA `0xC5F20` mit Spieler-ID und `action_data` auf.
- Case `19` verwendet unter anderem die Funktionen bei RVA `0xB8D00`, `0x194BA0` und `0xC6040`; deren einzelne Zuständigkeiten und ABI sind noch zu klären.
- Relevante Exporte: `CreateScenarioAction` RVA `0x81320`, `ApplyScenarioEvent` RVA `0x81010`, `GameAction` RVA `0x81820`.
- Der FreeBuild-Ereignisdispatcher ab RVA `0x104550` besitzt keine Cases für diese fünf Text-IDs. `GameAction(FreeBuild_Event, ...)` ist für sie daher kein Ersatz.
- Bei einer Fortsetzung jeden einzelnen Action-Case bis zu separat aufrufbaren Handlern verfolgen und nur Funktionen mit vollständig geklärter ABI sowie eindeutiger semantischer Signatur nutzen; RVAs bleiben reine Referenzwerte für den oben genannten DLL-Hash.
