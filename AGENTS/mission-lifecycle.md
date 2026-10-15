# Missionsstart/-ende, Zeit und Timeline

**Lesen, wenn:** Code auf Kartenstart, Save-Laden, Editor-Start, Missionsende reagiert, verzögerte Aktionen plant oder Timeline-/Szenarioereignisse berührt. Gilt zusätzlich zur Root-`AGENTS.md`. Vertrags- und Prüfnachweise: `knowledge/MISSION_LIFECYCLE.md` im aktuellen Semantic-Ordner (aus `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`).

## APIShared-Missionslifecycle

- Alle eigenen Missionsstarts und -enden zentral über `APIShared.IMissionLifecycleCapability` beziehen; Registrierung mit Mod-GUID und stabiler Registrierungs-ID. `OnStart`, `OnEnd`, `OnInitialization` tragen unveränderliche Kontextdaten. Shared stellt nur den pro Assembly eingebundenen Adapter `MissionEvents` bzw. `GameplaySessionLifecycle` bereit. Keine direkten Start-/Load-/Unload-Abonnements außerhalb dieses Dienstes, keine künstlichen Extender-EventArgs, keine parallelen Startdetektoren.
- **`OnStart`** = erfolgreiche Rückkehr aus der vollständigen Managed-Initialisierung; ein verzögerter MP-Client startet erst nach deren Abschluss im Nachrichtenpfad. Erster `lastGameState`, Renderdaten, vorhandene Gebäude und gestartete MP-Simulation sind zusätzliche Voraussetzungen. `EditorCreated`/`EditorLoaded`, neue Partie und Save-Wiederherstellung unterscheiden; Startressourcen und Burgenstarts nie für Saves, Editor oder Replay wiederholen.
- **`OnInitialization`** nur für nachgewiesene frühe Anforderungen: `BeforeLoad` (vor dem nativen Lade-/Erstellungswrapper), `BeforeNativeStart`/`AfterNativeStart` (um den nativen Spielstart), `NativeLoaded` (nach dem umschließenden Ladepfad). Nicht jeder Startweg erreicht jede Phase; Phasen werden nicht wiederholt. Lokale Coop-Starts nutzen ebenfalls den Skirmish-Einstieg; ihre PreInit-Parameter vor einer Modusfreigabe berücksichtigen.
- **`OnEnd`** beendet eine Sitzung bzw. einen begonnenen Versuch genau einmal (Endgrund, letzte Phase, `HasStarted`). Ersetzung und Fehlversuche lassen keine alte Sitzung aktiv; verschachtelte native Unloads gehören zum laufenden Versuch; Optionen und reine Sieg-/Niederlagenanzeigen beenden keine Karte. Späte Beobachter erhalten nur die aktuell fertige Sitzung als gekennzeichnetes Replay.
- Der Adapter aktualisiert das Aktivierungsgate vor fachlichen Callbacks. Bereinigung genau einmal über `MissionEvents.Ended` oder `SubscribeStarted(..., onEnded: ...)` registrieren, auch wenn das Feature inzwischen deaktiviert wurde.
- APIShared ist harte Abhängigkeit aller Verbraucher inkl. Begleitassemblies; fehlende Capability diagnostizieren und gesperrt bleiben.

## Zeit, OnTick und Timeline

- `GameTimeManagerAPI.OnTick` läuft im nativen Zeit-/Datums-Pre-Tick-Hook. Dort keine nativen Timeline-Vektoren über `CreateNewScenarioEvent`, `ApplyScenarioEvent` o. Ä. verändern (verursachte beim Kartenstart einen nativen Zugriffsfehler). Ohne validierten sicheren Kontext Timeline-Ereignisse gezielt deaktivieren.
- Direkt in `MapLoaderR3EventHooks.OnStartMap(Post)` angelegte `TimerEngine`-Actions können durch nachfolgendes `ReadTimerDataFromArchive` verschwinden; frühe verzögerte Aktionen dort nicht blind mit `AddDelayedAction(...)` planen. Stattdessen einen nachweislich funktionierenden Lifecycle-Mechanismus (wie bei StartConditions) oder einen laufenden persistenten Hook-/Scan-Takt mit `Stopwatch.GetTimestamp()` verwenden. Immer „geplant“ und „ausgeführt“ loggen.
