# Diagnose kurzer Lags ab 100 ms

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

`ui.merit.cache-wrapper` dokumentiert ab 100 ms die echte Prefix-Entscheidung:
deaktiviert, Cache fehlt/abgelaufen, Sperre bei der vorhandenen Probe frei,
oder Cache wegen belegter Sperre verwendet. Dazu Cachealter, Ergebnisweg
und GC-Zähler. Keine zusätzliche Lockprobe und keine geänderte Cachegrenze.

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
`pwsh -NoProfile -File BugfixesAndQoL/tests/MeritDiagnostics/Test-MeritDiagnostics.ps1`
prüft die produktiven Cachezweige separat mit und ohne `-Diagnostic`:
deaktiviert, fehlend, abgelaufen, freie/belegte Sperre, Klonisolation und
unverändertes Cachealter. Ohne Diagnose ist der Harmony-Zustand weiterhin
ein `bool`; Diagnosezustand und Partial-Implementierungen entfallen.
Die reine MSBuild-Projektauswertung bestätigt beide neuen BFQoL-Dateien
nur bei `SerpDevDiagnostics=true` und keinen `SERP_DEVDIAG`-Define im
normalen Build. Sie führt keine Buildtargets aus.
Quell-/Syntaxprüfungen ersetzen nicht den späteren DevDiag-Build und den
Ingame-Nachweis der Harmony-Patchreihenfolge mit allen installierten Mods.
