# Diagnose kurzer Lags ab 100 ms

## Spielzeit-Stillstand, Lag-Arten und Marker (Quellstand 28.10.2026)

Auslöser: Editor, ca. 1350 Einheiten, Bewegungsbefehl (Log
`Testmods/MoatMove/.local/ingame-perfprobe-2.log`, 23:04–23:05). Die Spielwelt
stand sichtbar still, PerfProbe meldete nichts: 150–180 FPS, `run` gleichmäßig
55/s mit höchstens 21 ms. Spieltick-Lücken zählte nur die Automation
(`tickstats`), ohne Zeitstempel und ohne Sampler.

Stand: am 28.10.2026 um 23:30 als DEVDIAG-Build des Arbeitsstands (Basis
`6ef09c570`, Buildrecord `.work/b/working/575.build.json`) gebaut und
installiert.

Ingame-Nachweis 28.10.2026, 23:35 (Editor, ca. 1350 Einheiten, Log
`Testmods/PerfProbe/.local/manual-tickgap-1.log`, nicht versioniert): Auf vier
von fünf Rechtsklick-Markern folgt nach 87–108 ms ein `tick:gap` von 526, 121,
513 bzw. 455 ms. Bild und Main-Thread laufen
normal weiter (80–93 Frames je Lücke, Main-Stacks: Rendern/Warten). Im Fenster
gibt es genau zwei kurze Runs, einen an jeder Grenze (`runCallsDuring=1`,
`simBusyShare` 0,01–0,04, kein langer `run`, `lockHistory` 448–522 ms ohne
bekannten Besitzer). Jeder Run brachte genau einen Spieltick
(`runsWithoutTick=0`). Die Simulation wurde also etwa 0,5 s lang nicht
aufgerufen oder hing vor PerfProbes `run`-Prefix. Zwei BFQoL-Diagnoseprefixe
(`GatehouseDiagnostic`, `GateFreeze`) laufen vor PerfProbes Prefix; ihre Zeit
ist bisher unsichtbar. Offen und nächster Messschritt ist Erweiterung 1
(Simulationstakt), ergänzt um eine `run`-Zeitmessung außerhalb aller fremden
Prefixe.

### Simulationstakt (Erweiterung 1, Quellstand 28.10.2026 nach dem Messlauf)

- Prefix auf `Director.MyFixedUpdate(object)` (Threadpool-Timer, privat; zur
  Laufzeit gegen Signatur und `FramesToProcess:int` geprüft, sonst
  `timerProbe unavailable`, nur diese Messung entfällt). Er zählt Aufrufe,
  misst Abstände und liest `FramesToProcess` vor Vanillas Erhöhung.
- `tick:gap` ergänzt `timerCallsDuring`, `timerMaxIntervalMs` (einschließlich
  des offenen Intervalls), `expectedTimerMs` (`EngineFrameTime` / Low-FPS-Stufe),
  `framesToProcessMax`, `framesToProcessAtEnd` sowie die Threadpool-Belegung
  beim Schreiben der Zeile (nicht während der Lücke). `tickCounters` ergänzt
  `timerCalls` und `maxTimerIntervalMs`.
- Deutung in der Auswertung: fehlende Timer-Aufrufe → Timer/Threadpool
  ausgehungert oder Prozessstopp, Vanilla hat keine Frames angefordert.
  Steigt `FramesToProcess` ohne `run`, hängt der Sim-Thread vor `run`.
- PerfProbes `run`-Prefix läuft jetzt zuerst, Postfix und Finalizer zuletzt
  (`Priority.First`/`Last`). Damit enthalten `sim:EngineInterface.run`,
  `simBusyMs` und das Sim-Sampling die Prefixe anderer Mods. `preBodyMs` nennt
  die Zeit vor dem Originalkörper.
- `[Sampler] SimOutsideRun` (Standard `false`) tastet den Sim-Thread auch
  zwischen den `run`-Aufrufen ab, also in Vanillas Director-Schleife. Das
  kostet im Leerlauf etwa 1000 Suspendierungen/s; nur für gezielte Läufe.

Zweiter Ingame-Lauf 28.10.2026, 23:49 (gleiche Szene, `SimOutsideRun=true`, Log
`Testmods/PerfProbe/.local/manual-tickgap-2.log`): Auf drei von vier
Rechtsklicks folgen `tick:gap` von 617, 476 und 541 ms. Der Timer lief normal
(`timerMaxIntervalMs` ≤ 21, 26–34 Aufrufe je Lücke); `framesToProcessMax`
stieg auf 24–32. Vanilla forderte die Simulationsschritte also an, der
Sim-Thread arbeitete sie nicht ab. Er war beschäftigt: 65–80 % der Sim-Stichproben in
`CDE:0xDA5EF` unter BFQoLs Hülle um Vanillas Pfad-Builder F4930
(`InstallNativeOriginalEntryCounters>b__1`; native Zeit des Originals, kein
Overhead der Hülle). Kleinere Anteile: Hook-Stubs (`dyn@`), MoatMove
`FastLiveEdge`, BFQoL `TryGetTraversalEdge`. In den 2‑s‑Fenstern sind die
Einstiege MoatMove `ProcessFastCommands` (über `OnTick`), BFQoL
`PathBuilderBarrier`/`BuildPathWithCompletedMoatRouteVariant` und ein
MonoMod-`Hook<EngineInterface::run>` (`FormationRuntime:EngineRunHook`,
APIShared `MarkedUnitSelectionAPI:OnRun`). Der Nutzer beobachtete den Lag auch
mit „SCDE Vanilla.bat“ (ohne Messung).

**Messgrenze:** MonoMod-Hooks um `run` liegen außerhalb der Harmony-Kette.
Ihre Zeit fehlt in `sim:EngineInterface.run`, `simBusyMs` und `preBodyMs`
(längster gemessener `run` in diesem Lauf: 58 ms). Die Lücke selbst und
die Sim-Stacks erfassen sie trotzdem. Offen: Zuordnung von `CDE:0xDA5EF`
über die Native-Baseline sowie eine PerfProbe-Basismessung nur mit SE.

### Welche Lag-Art PerfProbe erkennt

| Lag-Art | Erkennung | Zeilen/Felder |
|---|---|---|
| Bild steht (Frame-Lücke) | Intervall der Render-Callbacks | `frame:interval` |
| Langer Sim-Aufruf | `run`-Zeitmessung mit Phasen | `sim:EngineInterface.run`, neu `gameTicksDuring` |
| **Spielzeit steht, Bild läuft** | **neu:** Extender-`OnTick` und nativer Kartentickzähler | `tick:gap`, `tick:mapGap`, `tickCounters` |
| Simulation nicht eingeplant oder leerlaufend | **neu:** Run-/Native-Aufrufe, Sim-Busy-Zeit im Lückenfenster | Felder der `tick:gap`-Zeile |
| Run ohne Spielfortschritt | **neu:** Ticks bzw. Kartenticks pro Run | `runsWithoutTick`, `runsWithoutMapTick`, `maxTicksPerRun` |
| Low-FPS-Ausgleich von Vanilla | **neu:** `run(true)` | `runCounters skipCalls`, `skipRunsDuring` |
| Langer Main-Thread-Aufruf | Director, GUI, `processTestMap`, Engine-Wrapper | `main:*` |
| Main-Thread wartet auf `threadLock` | Monitor-Messung nur an auditierten Wrappern, Frame-Akkumulator, `lockHistory` | `lockWaitMs`, `engineFrameLockWaitMs` |
| GC | Zähler und Heapdifferenz | `gcDuring`, `gc`, `allocThreads`; **keine Pausendauer** |
| Laden/Kartenstart | `LoadSaveFile`-Spike, `loadSpan`; Automation `startup` | neu: Stillstand direkt nach Laden/Pause als `afterHalt=True` |
| Lag nach Befehl | **neu:** Klick-/Tastenmarker | `inputMark` |

### Spieltick-Lücken (`tick:gap`)

- Quelle 1: `GameTimeManagerAPI.OnTick`. Der Extender löst es aus einem
  nativen Hook in der Tickschleife innerhalb von `DLL_RunTick` aus, nicht bei
  lokaler Pause. Quelle 2: der vom Extender gefundene native Kartentickzähler
  (`gCurrentGameTick`), gelesen nach jedem abgeschlossenen `run`. `tick:mapGap`
  bekommt nur Stackauszüge, solange keine `OnTick`-Ticks ankommen.
- Grenze: `[Spikes] TickGapMs` (Standard 100, 0 = Monitor aus) plus erwartetes
  Tickintervall der Spielgeschwindigkeit (`Director.EngineFrameTime`).
- Pausen und Laden: Der Main-Thread prüft pro Frame `Director.instance`,
  `SimRunning`, `Paused` und die lokale native Pause. Zeit bis zum letzten
  angehaltenen Frame zählt nicht (`haltedMs`). Ein Stillstand direkt danach
  wird trotzdem gemeldet (`afterHalt=True`, `haltKind`). Lücken, die nur durch
  Pause oder Laden lang sind, zählen als `haltedGapsExcluded`. Ein kleinerer
  Tickwert (neue Mission) beendet das Intervall ohne Meldung.
- Die Zeile enthält Start-/Endstempel, Tick davor/danach, `runCallsDuring`,
  `nativeCallsDuring`, `skipRunsDuring`, `noBufferDuring`, `simBusyMs`,
  `simBusyShare`, `framesDuring`, `mapTicksDuring` und `lowFpsLevel`. Sie wird
  einen Frame später auf dem Main-Thread geschrieben; `emittedAfterMs` nennt
  die Verschiebung der Uhrzeit, die Stempel sind exakt. Für das Fenster werden
  Main- und Sim-Stacks über den vorhandenen Spike-Sampler angefordert.
- Die Busy-Zeit enthält den Run, in dem der erste Tick des Intervalls lag.
- `tickCounters` (alle 5 s): Ticks/s, Lücken über der Grenze, größte aktive
  Lücke, ausgeschlossene Lücken, Kartenticks, Runs ohne Tick, mit mehreren
  Ticks und ohne Kartentick.

### Sim-Sampler außerhalb von `run`: nicht nötig

Audit des verwalteten Vanilla-Pfads (`Director.runSimTickTest`, Assembly-CSharp
BC8B6A39): Ein Threadpool-Timer (`MyFixedUpdate`) erhöht `FramesToProcess`
im Takt der Spielgeschwindigkeit, außer bei Pause. Der Sim-Thread ruft pro
Eintrag `run(skip)` auf, danach nur `EditorDirector.updateTick()` (ein
Zählerinkrement); sonst `Thread.Sleep(1)`. Außerhalb von `run` gibt es also
keine Arbeit mit Stack-Information. Ob die Simulation in einer Lücke arbeitet
oder leerläuft, zeigen `runCallsDuring`, `nativeCallsDuring` und
`simBusyShare` ohne zusätzliche Suspendierungen. `simOnlyWhileRunActive`
bleibt deshalb unverändert.

### Eingabemarker (`inputMark`)

`[Markers] Keys` (Standard `Mouse0,Mouse1,ScrollLock`; leer = aus) loggt jeden
Druck dieser Tasten mit Stempel über `InputR3EventHooks.OnKeyDown`. Der
Extender meldet dort alle Unity-`KeyCode`s einschließlich Maustasten; es gibt
kein Polling. Rechtsklick = Bewegungsbefehl, `ScrollLock` = freier Marker für
manuelle Läufe.

### Offline-Auswertung

`tools/Analyze-Spikes.ps1` beginnt mit einer Lauf-Zusammenfassung: Ereignisse
nach Art und Phase (Anzahl, Summe, Maximum), aufsummierte `tickCounters`,
Marker und Ereignisse bis 5 s nach einem Marker. Neue Ereignisart
`game-time-stall` (Spielzeit steht, kein Frame-Lag), Phase
`after-pause-or-load`. Hinweise kennzeichnen eine leerlaufende oder nicht
eingeplante Simulation, Runs ohne Kartentick und ein weiterlaufendes Bild. Je
Ereignis wird der letzte vorhergehende Marker als Zeitbezug genannt; das ist
kein Ursachennachweis. `tools/Run-Automation.ps1` gibt dieselbe Zusammenfassung
nach jedem Lauf aus. `tests/Test-SpikeAnalysis.ps1` prüft den echten Parser;
`tests/TickGapTests.cs` (über `Test-DiagnosticHelpers.ps1`) führt die
produktive Lückenklassifikation und die Sim→Main-Übergabe aus.

### Weitere Erweiterungen, nach Nutzen geordnet (nicht umgesetzt)

1. **Takt der Simulation:** nach dem Messlauf vom 28.10.2026 umgesetzt
   (Abschnitt „Simulationstakt“).
2. **Prozessweite Stopps (GC):** Mono liefert keine Pausendauer. Näherung:
   Weckverspätung des Sampler-Threads (`maxWakeLateUs`) als eigene Spikezeile
   ab etwa 50 ms.
3. **Native Arbeit ohne Tick:** Zeigt `tick:gap` viele native Runs ohne
   Kartentick, die Sim-Stacks des Fensters auswerten und erst danach gezielte
   native Zähler ergänzen.
4. **Render-Thread/GPU je Frame-Spike:** CPU-/GPU-Zeit aus
   `FrameTimingManager` in die `frame:interval`-Zeile.

## Entlastete Messung (Quellstand 23.10.2026)

Noch kein Mod-Build und keine Installation dieser Überarbeitung. Der Sampler
wartet mit normaler Threadpriorität auf einen privaten hochauflösenden
Windows-Wartetimer. Absolute monotone Zeitpunkte verhindern nachgeholte
Samplingserien: verpasste Intervalle werden gezählt und übersprungen.
Fehlt der Timer oder schlägt das Warten fehl, meldet der schlafende Ersatzpfad
`cadenceMode=sleep-degraded`. Keine Änderung der globalen Timerauflösung.
Der ältere Sim-Sampler benutzt dieselbe sichere Capture-/Analyse-Pipeline.
Bei abgeschaltetem regulärem Sampler erfasst dieser Ersatz nur Sim-Aufrufe
ab 80 ms; `longRunOnly=true` kennzeichnet diese eingeschränkte Abdeckung.

`samplerCapture` ergänzt `cadenceWaits`, `missedIntervals`, `meanWakeLateUs`
und `maxWakeLateUs`. Tatsächliche Sample-Abstände, Suspendierungszeit,
Sampling-/Analyse-CPU, Überläufe und GC-Zähler bleiben sichtbar. Die Vorgaben
bleiben 100 ms, 1000 µs und zwei Sekunden. Der isolierte Timer-Test fordert
mindestens 80 % weniger Warte-CPU als aktives Warten; dies garantiert keine
Samplingabdeckung im Spiel.

Unbekannte IPs erscheinen exakt als `dyn@0x...`
(`runContext nativeUnknownIp=exact`). Die Offline-Auswertung ordnet sie nur
eindeutigen, im selben Start protokollierten RedBird-Stubbereichen zu. Alte
64-Byte-Buckets müssen vollständig im Bereich liegen. Überlappende oder
fehlende Bereiche bleiben `unknown-or-ambiguous`. Stubs enthalten auch
verdrängte Vanilla-Instruktionen; dies beweist keine reinen Modkosten.

BFQoL sammelt häufige Shared-DevDiag-Zähler in threadlokalen Run-Batches.
Zusammenführung und Spike-Ausgabe erfolgen nach dem Scope-Ende; Überlast
bleibt sichtbar. Im aufgewärmten Torhauspfad ohne Zugbrücke entfallen
ID-Liste und eingefangene Delegates. Verhaltenstests fordern null Byte pro
Abfrage sowie identische Live-Abfragen, Routensuchen und Signaturen.
Die funktionale Merit-/Achievement-Brücke existiert in beiden Buildarten;
ihre Diagnosefelder und Messungen ausschließlich mit `SERP_DEVDIAG`.
Siehe [VanillaOptimization.md](../../VanillaOptimization.md) und
[Engine-Audit](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ENGINE_PRESENTATION_BRIDGE.md).

## Torhaus-Geometrie und fehlende PerfProbe-Messung (23.10.2026)

Der Lauf vom 22.10., 23:59 bis 23.10., 00:05 verwendete
`test_stresstest3.sav`. PerfProbe war durch einen späteren normalen Build
inaktiv; sieben `bfqol.gatehouse.run`-Spans von 101,7 bis 200,9 ms belegen
lange verwaltete Aufrufe, aber keine gemessenen Frame-Lags oder Sperrenursache.
In zwei Spans lagen die Torhaus-Callbacks bei 75,7/66,6 ms, darin die Sammlung
bei 65,4/59,4 ms. Ein 200,9-ms-Span enthielt nur 8,5 ms Torhaus-Callbacks.
Die Ursache der restlichen Zeit ist damit offen; inklusive Werte nicht addieren.

BFQoL hält nun maximal 256 reine Geometrien pro Thread, nach Ursprung X/Y und
Größe. Die unveränderlichen Koordinatenfolgen entstehen durch den bisherigen
APIShared-Helfer. Live-Belegung, Gebäudegültigkeit, Zugbrücken, PCL und bestehende
Ergebniscaches bleiben aktuell und unverändert. Leere Snapshot-Sammlungen werden
geteilt. `geometryCountersVersion=2` unterscheidet `footprintCalls` (tatsächliche
Geometrieerzeugungen), `geometryCacheHits` und `footprintScans`. `candidates` zählt
bereitgestellte Kandidaten pro Scan; `collectionTileProbes` zählt die tatsächlich
ausgeführten bestehenden Koordinatenproben einschließlich Außenansatzsuche.
Diese Zähler existieren ausschließlich im Diagnosebuild.

Der Vergleich gegen den vorherigen Git-Stand führt dieselben produktiven Abfragen
mit/ohne Diagnose aus. Entscheidungen, Routenzahlen, Kachelabfragen, Reihenfolge
und Signaturen stimmen überein, auch bei Zustandsänderungen im selben Tick und
Reentranz. Im isolierten CLR-Test mit 20 Kandidaten sinken die Allokationen der
aufgewärmten produktiven Abfrage von 1416 auf 256 Byte pro Aufruf. Der reine
Geometrie-Cachetreffer allokiert nichts. Das ist kein gemessener Ingame-FPS-Gewinn.
Nachweise: `.work/t/gate-geom/001`.

Die Offline-Auswertung meldet PerfProbe als `active`, `inactive` oder `missing`
(Aktivität unbestätigt). Leere Samplezeilen gelten nicht als nutzbare Stacks.
`long-run-call` kennzeichnet lange Aufrufe; nur ein entsprechendes PerfProbe-
Frameintervall ab der Grenze erhält `recorded-frame-gap`. Ladephasen bleiben
gesondert gekennzeichnet. Vor manuellen Vergleichen PerfProbe zuletzt mit
`/devdiag` installieren und im neuen Log Aktivität, `PERFPROBE_READY` und
nutzbare Samples prüfen; ein späterer normaler Build kann die Diagnose ersetzen.
Seit 23.10.2026 übergibt `build.bat` `/devdiag` immer selbst (auch per
Doppelklick), weil PerfProbe ohne DEVDIAG nichts misst; im Branchmenü `develop`
wählen, `main` lehnt DEVDIAG-Builds ab.

## Erweiterung für den reduzierten Vergleichslauf (22.10.2026)

Diese Ausbaustufe ist ausschließlich als Quelländerung geprüft: **kein Mod-Build
und keine Installation**. Die folgenden älteren Abschnitte beschreiben die erste
Ausbaustufe; für Zähler und neue Ausgaben gelten diese Präzisierungen:

- `runCounters` trennt begonnene/beendete `run`-Aufrufe, fehlende freie Puffer
  sowie betretene/beendete `DLL_RunTick`-Aufrufe. Auch ein nativer Aufruf beweist
  keinen fortgeschrittenen Spieltick. `skipArgument` bleibt unverändert sichtbar.
  Alte `simRunsCompletedDuring`-Werte bedeuten Aufrufzahlen, keine Spielticks.
- `run` misst Pufferbeschaffung, Monitor-Warten, Vorarbeit unter der Sperre,
  native inklusive Zeit und anschließende Deserialisierung/Kopie/Publikation.
  `nativeInclusiveMs` enthält Extender-/Mod-Callbacks und den kleinen Aufwand
  der Messgrenzen. `nativeThreadCpuMs` ist CPU des aufrufenden Threads, keine
  Gesamtsystem-CPU. Der pufferlose Pfad ruft keine CPU-Zeit-API auf.
- `engineFrameLockWaitMs` liest den Monitor-Akkumulator zwischen Rendergrenzen,
  damit verschachtelte Wrapper keine doppelte Summe verursachen. Die größten
  Engine-Aufrufe enthalten auch ihre Anzahl; mehrere Abfragen unter 100 ms
  können damit einen langen Frame erklären. Einzelne Wrapperzeiten sind inklusiv.
- `lockHistory` wertet höchstens acht Sekunden aus. Bis zu acht instrumentierte
  Besitzerthreads erhalten begrenzte Historien. Die acht längsten überlappenden
  Scopes enthalten Methode, Managed-Thread-ID, Sperrenanfang/-ende, Dauer und
  den Hinweis auf betretene native Aufrufe. `unattributedMs`/`unattributedOwner`
  bleiben unbekannt: abgeschlossene instrumentierte Scopes decken weder gerade
  offene noch fremde Sperrenbelegungen vollständig ab. Native-Beteiligung ist
  keine gemessene native Dauer. Verdrängte/verlorene Einträge werden ausgewiesen.
- Rohstacks gehen in vorab reservierte Übergabepuffer. Der Sampler wartet nicht
  auf Symbolauflösung; diese und die Sampler-Logformatierung laufen auf
  `PerfProbe.Analysis`. Es gibt zwei reguläre und zwei Spike-Puffer, höchstens
  16 wartende Spike-Anfragen und zwölf angenommene Spike-Analysen je fünf Sekunden.
  Eine Analyse umfasst gegebenenfalls beide Threads. Während einer Suspendierung
  führt der Aufnahmeweg weder Symbolauflösung noch Allokation oder Logging aus.
- `samplerCapture` meldet CPU, gesamte/maximale Suspendierungsdauer und verworfene
  Fenster/Anfragen. Seine monotonen Accounting-Grenzen können bei Überlast mehrere
  Fenster umfassen; `sampleStartStamp`/`sampleEndStamp` benennen das tatsächlich
  übergebene Samplefenster. `samplerAnalysis` meldet eigene Wall-/CPU-Zeit und
  GC-Zählerdifferenz. GC-Zähler sind keine gemessene GC-Pausendauer. Leere Fenster,
  OS-Thread-ID, größte Samplelücke und abgeschnittene Historien bleiben sichtbar.
- `runContext`, `runPlugin` und `patchOwners` erfassen wirksame Diagnoseoptionen,
  Plugin-GUIDs/Versionen/MVIDs sowie Harmony-Besitzer für die relevanten Methoden.
  `loadSpan` beschreibt nur den verwalteten Lade-Wrapper, keine Missionsbereitschaft.
  PerfProbe bekommt keine Abhängigkeit von BFQoL oder APIShared.
- BFQoL aggregiert Torhaus-Callbacks, direkte/vollständige Cache-Entscheidungen,
  tatsächlich aufgerufene Routensuchen, Footprint-, Zugbrücken-, Außenansatz-
  Trace- und Erreichbarkeitszeiten. `bfqol.gatehouse.run` erscheint erst ab 100 ms
  und enthält Start-/Endstempel sowie die teuersten vier erfassten Torhäuser.
  Höchstens 256 Identitäten pro Scope werden verfolgt; `gateOverflow` markiert
  fehlende Identitäten. Die Summe aller Abfragen bleibt erhalten. Verschachtelte
  Runs erhalten getrennte Sammler; ihre inklusiven Zeitspannen nicht addieren.
  `gatehouseWindow` enthält Fünf-Sekunden-Summen und effektive Aktivierungszustände.
  Keine zusätzliche Spielabfrage, Routensuche oder Änderung der Cachepolitik.
- Merit-Entscheidungen erhalten Summenzähler; langsame Wrapper zusätzlich monotone
  Grenzen. Die Offline-Auswertung verbindet überlappende Spans, trennt Ladephasen
  und markiert Messbelege sowie Grenzen der Zuordnung. Alte Logs bleiben lesbar.

Prüfnachweise dieser Änderung liegen unter `.work/t/perf-v2/001`: normale und
Diagnose-Quellprüfung gegen echte Spielreferenzen ohne Mod-DLL-Ausgabe, getrennte
MSBuild-Quelllistenauswertung, Lifecycle-/JSON-/Abhängigkeits- und CRLF-Prüfungen.
Die In-Memory-Tests führen den produktiven IL-Transpiler aus (pufferlos, nativ,
Skip, Reentranz, Exception/Finally) und prüfen 10.000 nebenläufige Übergaben.
Torhaus- und Merit-Produktionsquellen werden mit/ohne Diagnose ausgeführt;
Torhausentscheidungen, Routenzahlen und Kachelabfragen stimmen überein.
`tests/Test-SpikeAnalysis.ps1` prüft den tatsächlichen Offline-Parser einschließlich
Ladephasen, kumulierten Frame-Wartezeiten, verschachtelten und unvollständigen Daten.

Spätere manuelle Abnahme: 100 ms, 1000 µs und zwei Sekunden; gleicher Spielstand,
Zoom und Spielgeschwindigkeit. Mindestens ein Lauf ohne BFQoL und ein Lauf mit
BFQoL samt nötigen Abhängigkeiten. Spielstarts und Modauswahl übernimmt der Nutzer.
Erst deren Logs bestätigen Patchreihenfolge, Messaufwand und tatsächliche Ursache.

## Erste Ausbaustufe (historischer Stand)

Quelländerung vom 22.10.2026; BFQoL wird für diese Erweiterung noch nicht
gebaut oder installiert. PerfProbe wird separat geprüft und gebaut; sein
jeweiliger Buildnachweis beschreibt das erzeugte Paket. Die bisherigen
Messlogs stammen aus älteren Builds. Keine
Änderung an BFQoL-Modsettings oder deren Aktivierungsregeln.

## Messgrenze und Lebensdauer

Nur `/devdiag` kompiliert die Messungen und die neuen `*.DevDiag.cs`-Dateien.
Standardgrenze für Sim-, Main- und Frame-Spikes: 100 ms. Die zusätzliche
`freeze=true`-Markierung bleibt bei 400 ms. Vorhandene Configwerte behalten
Vorrang; keine neue Laufzeitaktivierung von Diagnose in normalen Builds.
Statische Harmony-Instanzen, `LibraryLoaded`, `Application.onBeforeRender`
und der statische Sampler-Thread behalten die bestehende prozessweite
Lebensdauer. Keine neuen Startup-Komponenten oder Hook-Teardownpfade.

Der Audit der Messgrenze gegen die installierte `Assembly-CSharp.dll`
(SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`)
bestätigt je eine `Monitor.Enter(object, ref bool)`-/`Exit(object)`-Sperre
auf `EngineInterface.threadLock` für `GetMeritData` und `SetAchData`.
`GetMeritData` ruft darin `DLL_GetMeritData(int*)` auf und kopiert danach
das Ergebnis; `SetAchData(int,int,int)` reicht seine Argumente unverändert
an `DLL_SetAchValues(int,int,int)` weiter. Die Freigabe erfolgt im `finally`.
Caller sind Allies-/Merit-UI bzw. `AchievementsCommon.StoreValuesInEngine`
und `UpdateAfterLoadingASave`. Der Simulationspfad `run` verwendet dieselbe
Sperre um `DLL_RunTick`. Keine neue native Adresse und keine neue Auslegung
von Spieldaten; die nativen Funktionsrümpfe bleiben für diese Zeitmessung opak.
Kanonischer Native-Hash wurde unverändert als
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` geprüft.

## Neue Ausgaben

- Langsame Main-Thread-Engine-Aufrufe: Aufrufer, GC-Zählerdifferenz und
  abgeschlossene Sim-Ticks im Zeitraum. Bei den beiden auditierten Methoden
  zusätzlich `lockMeasured`, `lockEntries`, `lockWaitMs`, `lockScopeMs`,
  `outsideLockMs` und `threadCpuMs`. Ein ausgelassener Originalaufruf kann
  korrekt `lockEntries=0` ergeben (z. B. Merit-Cache). `-1` bedeutet nicht
  messbar. Andere Engine-Methoden erhalten keine Lock-/CPU-Zuordnung.
- `lockWaitMs` misst das originale Enter inklusive Scheduling/Reentranz;
  `lockScopeMs` ist Wall-Time innerhalb des instrumentierten Scopes, keine
  reine native CPU-Zeit. Bei verschachtelten Scopes sind die Werte inklusiv.
  Wenig Thread-CPU bei hoher Wall-Time spricht für Warten/Descheduling,
  beweist aber allein keine bestimmte Sperrenpolitik.
- Frame-Spikes: inklusive Director-/GUI-/Map-Zeiten, die größten Engine-
  Aufrufe desselben Frames, `gcDuring`, Mono-Heapdifferenz und Herkunft eines
  verfügbaren Allokationswerts. Heapdifferenz und GC-Zähler sind keine
  GC-Dauer und kein genauer Allokationszähler. Recorderwerte sind als zuletzt
  gemeldeter Wert gekennzeichnet und können zeitlich verzögert sein.
- GC- und Engine-Summen erscheinen alle fünf Sekunden auch bei
  `RenderSnapshots=false`; `probe.summary` erfasst deren eigenen Aufwand.
- `spikeSample event=...` enthält Main-/Sim-Stacks ausschließlich innerhalb
  desselben monotonen `startStamp`-/`endStamp`-Intervalls wie der Spike.
  Die Rohhistorie hält ungefähr acht Sekunden unabhängig von den normalen
  Sampler-Fenstern. Selbst-/innerste Mod-/Managed-Caller-Sicht werden
  getrennt ausgegeben. Wenige/fehlende oder abgeschnittene Samples sind
  ausdrücklich keine Evidenz für fehlende Arbeit. `maxSampleGapMs` erfasst
  auch Messlücken innerhalb des Ereignisses und an seinen Grenzen.
- Diese Auszüge verursachen keine zusätzlichen Thread-Suspendierungen.
  Maximal 16 wartende Aufträge und 12 Analysen je fünf Sekunden; zusätzliche
  Aufträge werden als `spikeSample suppressed` gezählt. GUI-/Director-Spans
  lösen keine doppelten Auszüge aus. `samplerAnalysis wallMs` macht den
  zusätzlichen Analyseaufwand und mögliche Messlücken sichtbar.

Die Monitor-Transpiler ersetzen nur die Call-Operanden nach Prüfung des
Sperrenscopes. Labels, Exception-Blöcke, Parameter und der `ref bool` bleiben
erhalten. Bei abweichender Form wird die optionale Messung des betroffenen
Engine-Wrappers nicht installiert; Vanilla wird nicht durch eine Ersatzlogik
ausgeführt. Prefix/Postfix/Finalizer ändern keine Rückgaben oder Exceptions.

## Ergänzung in BugfixesAndQoL

`ui.merit.cache-wrapper` dokumentiert ab 100 ms die echte Entscheidung:
deaktiviert, Snapshot fehlt, Sperre bis nach Original gehalten,
Simulationsaktualisierung oder Snapshot bei belegter Sperre geliefert.
`stale-served-refresh-requested` kennzeichnet ein Alter über drei Sekunden.
Start-/Endstempel, Cachealter und GC-Zähler ergänzen die Korrelation.

`uc.vanilla.cursorPair`, `uc.vanilla.buildingApproach` und
`uc.vanilla.buildingConsumer` trennen den Aufruf des bereits vorhandenen
Originaldelegaten von der umgebenden Mod-Arbeit. Native Zeit enthält dabei
weiterhin eventuell ausgelöste Mod-Callbacks. Cursor-/BuildingApproach-Spans
ergänzen unveränderte Eingabewerte und den Befehlskontext ab 100 ms.

Die Auswertung `tools/Analyze-Spikes.ps1` verwendet standardmäßig 100 ms,
verknüpft Sampler-Auszüge über die Ereignisnummer und kennzeichnet
überlappende Zeitspannen. Frame-, GUI-, Cache- und native Zeiten nicht addieren.

## Prüfung ohne Mod-Build

`pwsh -NoProfile -File Testmods/PerfProbe/tests/Test-DiagnosticHelpers.ps1`
prüft die tatsächlichen Monitor-/Historienhelfer in einer isolierten
In-Memory-Testassembly: Contention, Reentranz, Exceptions, Freigabe,
IL-Metadaten, Ablehnung veränderter Scopes, Ringpuffer und Intervallfilter.
Die Fixture kompiliert weder PerfProbe noch BFQoL als Mod und installiert nichts.
`powershell.exe -NoProfile -File BugfixesAndQoL/tests/MeritDiagnostics/Test-MeritDiagnostics.ps1`
prüft die produktiven Cachezweige separat mit und ohne `-Diagnostic`:
deaktiviert, fehlend, abgelaufen, freie/belegte Sperre, Klonisolation und
unverändertes Cachealter, konkurrierende Tupel, Save-Fehler, Sitzungswechsel,
echte Harmony-Finalizer und die Kombination mit PerfProbe-Transpilern.
Sie erzeugt nur eine isolierte Test-EXE. Der funktionale Harmony-Zustand
existiert in beiden Varianten; seine Diagnosefelder entfallen normal.
Projektauswertung und In-Memory-Compilerprüfung kontrollieren normale und
Diagnosequellen ohne Mod-Ausgabe.
`tests/EnginePresentation.Tests/ActualGameIlAudit.csproj` prüft zusätzlich
die produktive Transformation an den echten installierten IL-Körpern von
`run` und `SaveSaveGame` sowie die Merit-/Achievement-Sperrenanker. Seine
Test-EXE liest Methodenmetadaten und führt keinen Spielcode aus.
Quell-/Syntaxprüfungen ersetzen nicht den späteren DevDiag-Build und den
Ingame-Nachweis der Harmony-Patchreihenfolge mit allen installierten Mods.
