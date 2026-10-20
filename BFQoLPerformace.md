# BugfixesAndQoL – Performance

Laufende Untersuchung (Beginn 20.10.2026): FPS, Lags (> 100 ms) und Startzeit von BugfixesAndQoL (BFQoL) und APIShared im Vergleich zu „nur Script Extender“ (SE). Ziel: möglichst nah an die Werte ohne Mods heranzukommen, ohne Funktionen zu verschlechtern.

## Stand (Momentaufnahme 20.10.2026, ~22:40)

| Bereich | Vorher | Jetzt | nur SE |
|---|---|---|---|
| dusty-Save, Speed 250: laufende FPS | 84–85 | 86,7 (D9B2) | 80,0 / 89,3 (D9S1/2) |
| dusty-Save: Frames > 50 / > 100 ms | 19–20 / 0–1 | 4 / 1 | 1 / 1 |
| dusty-Save: Ticks/s | 143–145 | 203 | 208–217 |
| dusty-Save: größte Tick-Lücke | 323–334 ms | 150 ms | 102 ms |
| 8-KI-Save: größte Tick-Lücke | 940–1020 ms | 140–143 ms | 110–114 ms |
| 8-KI-Save: Tick-Lücken > 100 ms pro Lauf | 7 | 1 | 1 |
| 8-KI-Save: laufende FPS | 142–155 (schwankend) | 189 / 206 | 193–207 |
| Kartenstart (erster Sim-Tick, 8 KI) | 487–495 ms | 174–210 ms | ~115–124 ms |
| BFQoL-Initialisierung (`LibraryLoaded`) | 6,0 s | 2,25–2,34 s | – |
| Hauptmenü nach Prozessstart | 20–21 s | 16,9–17,3 s (dusty) | 12,5–13,0 s |

- **Umgesetzt:** A1–A10 (Tabelle unten). Alle Änderungen erhalten das Verhalten; die Ausnahmen A5, A8 und A9 hat der Nutzer entschieden.
- **Ausstehend:** Sichtprüfungen im Spiel (siehe Offene Punkte).
- **Aufgeräumt (21.10.2026):** Abgeschlossene Untersuchungsdiagnose entfernt. Für offene Kandidaten benötigte Messpunkte bleiben mit `TEMP_PERF_DIAG` markiert (siehe Offene Punkte).
- **Installiert (21.10.2026):** BFQoL ohne Diagnose (normaler Build aus dem Arbeitsstand, kein Release) und APIShared aus dem Arbeitsstand. Die Plugin-Auswahl ist die ursprüngliche, der PerfProbe-Sampler ist abgeschaltet. Nichts ist committet.

## Messaufbau

- **Saves:**
  - `test_dustynoattack2.sav`: 2 KI-Lords, laufender Angriff; Speed 250 per Automation.
  - `test_8lordsmiddle.sav`: 8 KI-Lords, vom Nutzer in Vanilla erstellt; Speed 300 und Vanilla-Zoom-Maximum aus dem Save, Automation mit `-GameSpeed 0`.
- **Treiber:** Automation aus `Testmods/PerfFixTest` mit allen PerfFixTest-Fixes **aus** (reiner Messtreiber):
  - Save laden, 80 Ticks Aufwärmen, auf **Zoom 0,5** herauszoomen, 3 s warten;
  - 20 s pausiert messen, danach 60 s laufend messen, Spiel beenden.
- **Fester Zoom 0,5:** BFQoL schaltet einen erweiterten Zoom frei. Ohne festen Zielzoom würde mit BFQoL eine größere Szene gemessen. Zoom- oder FPS-Limit-Änderungen sind keine Lösung (Nutzervorgabe).
- **Messgrößen** (alle im BepInEx-Log, Marker `AUTOMATION`):
  - `framestats`: FPS, p50/p95/p99, Maximum, Anzahl Frames > 50 ms und > 100 ms (Lag), Summe der Lag-Zeit;
  - `tickstats`: Ticks/s und Lücken zwischen Simulationsticks > 50/100 ms (Simulationsstillstand, auch wenn Frames flüssig bleiben);
  - `startup`: Sekunden vom Prozessstart bis zum Hauptmenü und vom Laden bis zur Missionsbereitschaft;
  - `nativecounters` (neu): native Zähler des Pfadmanagers zu Beginn und Ende der Laufphase. Vergleich SE ↔ Mods (siehe A8):
    - `reconstructFailures` = `+0x78`, gescheiterte Rekonstruktionen mit Vollkarten-Reparatur;
    - `pclRebuilds` = `+0x74`.
- **Varianten** (Plugin-Ordner per `.work/a/bfqol-perf/001/Run-Variant.ps1` umgeschaltet; die ursprüngliche Plugin-Auswahl steht in `original-plugins.txt` und wird mit `-RestoreOnly` wiederhergestellt):
  - **SE:** Script Extender + Messtreiber;
  - **API:** SE + APIShared;
  - **BFQ:** SE + APIShared + BugfixesAndQoL;
  - jeweils optional `+PerfProbe_Serp` für den Sampler.
- **Serien:** `.work/a/bfqol-perf/001/Run-Series.ps1 -Runs 'Label=SE|API|BFQ[+Plugin]' -SavePath … -GameSpeed … -SamplerWindow …`. Logs liegen unter `.work/a/bfqol-perf/001/results/<Label>_LogOutput.log`.
- **Streuung:** Fremdlast und ein zweiter, langsamer FPS-Modus verändern die FPS deutlich (siehe A8-Abschnitt). Pro Fall mindestens 2 Läufe, abwechselnd.
- **Ursachenanalyse:** `Testmods/PerfProbe` (DEVDIAG) mit Sampling-Profiler `PerfProbeThreadSampler`:
  - hält Main- und Sim-Thread etwa 1-mal pro ms kurz an;
  - ordnet die Stack-Frames nativen Funktionen (`CDE:0x…`), Modulen oder verwalteten Methoden mit Assembly zu;
  - Ausgabe `threadSample … self/asm/incl/entry/inner` pro Zeitfenster;
  - Auswertung mit `.work/a/bfqol-perf/001/Show-SamplerHotspots.ps1 -Log … -Thread sim|main -View inner|self|… -MinPercent …`.
- **Diagnose in BFQoL:** DEVDIAG-Messpunkte (`Shared.DevDiag`, `build.bat /devdiag`), Spike-Zeilen ab einer Schwelle mit Kontext (z. B. `assassin:BuildWeightedPath`, `uc.maskRebuild`, `uc.attack.fallbackField`).

## Abgrenzung zu Vanilla und Script Extender

Grundlage: [VanillaOptimization.md](VanillaOptimization.md) (Messungen vom 20.10.2026, nur Script Extender + Testmods).

- **FPS-Grenze:** Der Unity-Main-Thread begrenzt die FPS, vor allem durch das Einzelobjekt-Rendering der Einheiten. Diese Grundkosten stecken in der SE-Baseline und sind nicht BFQoL zuzurechnen.
- **Vanilla-Sim-Spike:** Ein KI-Gruppenbefehl berechnet im selben Tick für jede Einheit eine Route (`0x11B520` → `0x196280` → `0xF4930`, A*-Budget bis 400 000) und erzeugt so Ticks von 450–490 ms. Während dieses Ticks hält die Simulation `EngineInterface.threadLock`. Vanillas GUI-Check (`GetMeritData`) wartet darauf, das Bild friert ein. V5 (nur in PerfFixTest) behebt das Einfrieren des Bildes, nicht den langen Tick.
- **Extender-Grundkosten:** Allokation des Interpolationsevents pro Einheit und Frame (V9) und der `interpChimp`-Detour.
- **Größter nativer Sim-Anteil** (8 KI, alle Varianten gleich): `CDE:0xF5A40` ~45–65 %, Renderer `0x41D60` ~15–20 %, `0x19CBE0` ~6–10 %. Das ist Vanilla-Arbeit.
- **Zuordnungsregel:** Ein Lag oder FPS-Verlust gilt nur dann als von BFQoL/APIShared verursacht, wenn:
  - er in der SE-Baseline unter gleichen Bedingungen fehlt, **oder**
  - der Sampler die Zeit in deren Code zeigt (`asm=`/`entry=`/`inner=`), **oder**
  - ein nativer Zähler (z. B. `reconstructFailures`) mit Mods deutlich höher liegt als mit SE.

## Ergebnisse

### Serie 1 (Spielgeschwindigkeit des Saves, je 1 Lauf, 20.10.2026)

| Variante | FPS pausiert | FPS laufend | p99 laufend | Frames > 100 ms | Ticks/s | max. Tick-Lücke | Menü nach | Mission bereit nach Laden |
|---|---|---|---|---|---|---|---|---|
| SE | 133,8 | 126,9 | 14,3 ms | 0 | 39,8 | 45 ms | 12,7 s | 0,8 s |
| API | 121,9 | 107,3 | 16,9 ms | 0 | 39,8 | 44 ms | 15,3 s | 0,9 s |
| BFQ | 131,4 | 107,8 | 18,6 ms | 0 | 39,8 | 60 ms | 21,1 s | 1,2 s |

- Je 1 Lauf. Den FPS-Verlust mit APIShared hat Serie 2 als Fremdlast entlarvt.
- **Startzeit:** APIShared +2,6 s, BFQoL weitere +5,8 s.
  - In BFQoL entfallen davon ~3,8 s auf 47 RedBird-Hook-Transaktionen (379 Hooks).
  - Ursache der ~65 ms pro Hook: Thread-Suspendierung, behoben mit A3.

### Serie 2 (Spielgeschwindigkeit 250, abwechselnd, 20.10.2026 16:00–16:11)

BFQ ist hier der DEVDIAG-Build vor allen Optimierungen (Messpunkte kosten nur in den instrumentierten Stellen).

| Lauf | FPS pausiert | FPS laufend | p99 laufend | Frames > 50 / > 100 ms | Ticks/s | max. Tick-Lücke (> 100 ms) | Menü nach |
|---|---|---|---|---|---|---|---|
| SE (1. Lauf) | 84,0 | 66,9 | 36,9 ms | 1 / 0 | 170 | 95 ms (0) | 19,2 s |
| API (1. Lauf) | 84,2 | 62,5 | 38,0 ms | 3 / 0 | 163 | 96 ms (0) | 14,6 s |
| SE | 110,3 | 92,3 | 28,3 ms | 1 / 1 | 220 | 106 ms (1) | 12,1 s |
| API | 111,7 | 89,9 | 29,1 ms | 1 / 1 | 218 | 106 ms (1) | 14,6 s |
| BFQ | 107,4 | 85,1 | 42,3 ms | 20 / 0 | 145 | 323 ms (2) | 20,7 s |
| BFQ | 106,1 | 84,1 | 42,8 ms | 19 / 1 | 143 | 334 ms (2) | 20,1 s |

- Die beiden ersten Läufe (SE, API) liefen mit Fremdlast (pausiert nur ~84 FPS) und werden nicht gewertet.
- **APIShared** kostet im Spiel praktisch nichts.
- **BFQoL** vor den Optimierungen:
  - pausiert −4 %, laufend −8 % FPS;
  - deutlich mehr Frames über 50 ms;
  - die Simulation schaffte bei Speed 250 nur **143 statt 220 Ticks/s** (≈ 2,4 ms BFQoL-Kosten pro Sim-Tick);
  - zwei Sim-Stillstände über 100 ms pro Lauf, der größte 323–334 ms (Assassinen, A1).

### Serie 3+ (nach A1–A3, Speed 250, 20.10.2026 16:20–18:40)

Einzelne Läufe mit Fremdlast (pausiert ≈ 84 FPS) sind nicht gewertet. Saubere Vergleichsläufe:

| Lauf | Stand | FPS pausiert | FPS laufend | p99 laufend | Frames > 50 ms | Ticks/s | max. Tick-Lücke | Menü nach |
|---|---|---|---|---|---|---|---|---|
| S3SE1 | SE | 109,4 | 90,5 | 28,7 ms | 1 | 219 | 107 ms | 12,1 s |
| S3BFQ2 | BFQ + A1 | 107,4 | 85,4 | 42,2 ms | 15 | 145 | 145 ms | 20,3 s |
| S9BFQ2 | BFQ + A1 + A2 | **117,0** | **92,0** | 34,6 ms | **3** | 193 | 143 ms | 20,6 s |
| S13STBFQ | BFQ + A1–A3 | – | – | – | 0 (> 50 ms-Tick-Lücken) | 142 | 49 ms | **17,6 s** |

S13STBFQ war ein kurzer Startlauf (15 s Laufphase) ohne FPS-Vergleichswert. Ticks/s sind zwischen Varianten nur eingeschränkt vergleichbar: Sie mischen Overhead und unterschiedliches KI-Verhalten (siehe [AGENTS/performance.md](AGENTS/performance.md)).

### 8-KI-Save (Speed 300, 20.10.2026 19:50–22:36)

| Lauf | Stand | FPS laufend | Frames > 50 / > 100 ms | max. Tick-Lücke | Lücken > 100 ms | Rekonstruktionsfehler |
|---|---|---|---|---|---|---|
| L8C1 | SE | 206,7 | 3 / 2 | 110 ms | 1 | 10 |
| S8P1 / S8P2 | SE + PerfProbe | 192,6 / 195,7 | 3 / 1 | 113 / 114 ms | 1 / 1 | – |
| L8C2 | BFQ vor A8 | 142,5 | 67 / 3 | 971 ms | 7 | 112 |
| L8F1 / L8F2 | BFQ + A8 | 147,3 / 191,4 | 85 / 16 | 138 / 141 ms | 2 / 3 | 3 / 3 |
| B8P1 / B8P2 | BFQ + A8 + PerfProbe | 191,4 / 155,1 | 14 / 24 | 154 / 148 ms | 3 / 4 | – |
| B8G1 / B8G2 | BFQ + A8 + A9 + PerfProbe | 145,5 / 194,7 | 39 / 9 | 139 / 145 ms | 4 / 4 | – |
| **L8I1 / L8I2** | **BFQ + A8–A10** | **188,8 / 205,6** | **20 / 9** | **140 / 143 ms** | **1 / 1** | – |

### Serie D9 (dusty-Save, aktueller Stand, SE/BFQ im Wechsel)

20.10.2026 22:36–22:44, Speed 250, Stand A1–A10.

| Lauf | Stand | FPS pausiert | FPS laufend | p99 laufend | Frames > 50 / > 100 ms | Ticks/s | max. Tick-Lücke | Lücken > 100 ms | Menü nach |
|---|---|---|---|---|---|---|---|---|---|
| D9S1 | SE | 108,8 | 89,3 | 29,1 ms | 1 / 1 | 217 | 102 ms | 1 | 13,0 s |
| D9B1 | BFQ | 85,6 | 65,4 | 39,7 ms | 2 / 0 | 151 | 138 ms | 1 | 17,3 s |
| D9S2 | SE | 108,0 | 80,0 | 30,2 ms | 1 / 1 | 208 | 102 ms | 1 | 12,5 s |
| D9B2 | BFQ | 107,9 | 86,7 | 31,2 ms | 4 / 1 | 203 | 150 ms | 2 | 16,9 s |

- **D9B1 nicht gewertet:** pausiert nur 85,6 FPS, also Fremdlast oder langsamer Modus. Dasselbe Bild hatten früher die SE-Läufe.
- **D9B2 gegen SE:**
  - pausiert gleich (107,9 zu 108–109 FPS);
  - laufend 86,7 FPS, zwischen den beiden SE-Läufen (80,0 / 89,3);
  - p99 31 ms (SE 29–30);
  - Ticks/s 203 (SE 208–217; vor den Optimierungen 143);
  - Frames über 100 ms gleich (1).
- **Rest-Lag:** Die größte Tick-Lücke liegt bei 150 ms (SE 102 ms), mit 2 statt 1 Lücke über 100 ms. Ursache wie auf dem 8-KI-Save: Assassinen-Gruppenbefehle mit gewichteter Suche (siehe Kandidaten).
- **Startzeit:** Hauptmenü 16,9–17,3 s (SE 12,5–13,0 s). Mission bereit nach dem Laden in 0,93 s (SE 0,83 s).

## Umgesetzte Optimierungen

| Nr. | Optimierung | Wirkung | Verhalten |
|---|---|---|---|
| A1 | Assassinen: Existenzprüfung statt A* für nicht veröffentlichte Gruppenanfragen | größte Tick-Lücke 323–334 → 145 ms | identisches Ergebnis pro Anfrage (Verhaltenstest gegen A*); Befehls-Caches siehe A1 |
| A2 | Sichtkachel-Marker-Hook (`LargeMoveTargetMarkerRenderer`) mit nativer Bedarfsprüfung (`DemandGatedContextHook`) | ≈ 1 ms pro Sim-Tick weniger; pausierte FPS auf SE-Niveau, Frames > 50 ms 15–20 → 3 | Callback läuft unverändert, sobald Vorschau- oder Überlaufmarker existieren |
| A3 | Detours beim ersten Laden ohne Thread-Suspendierung (`HookInstallPhase`) | BFQoL-Initialisierung 6,0 → 2,0 s; Menü 21,6 → 17,6 s | gleiche Hooks, gleicher Zeitpunkt; nur das Anhalten aller Threads pro Patch entfällt |
| A4 | APIShared: Fear-Popularity-Ziel per MVID merken (`BepInEx/cache/APIShared_FearPopularityTarget.txt`) | Start: Cecil-Vollscan des Extenders entfällt ab dem 2. Start; Menü 17,3 → 16,6 s | identisch; gemerktes Ziel wird bei jedem Start über dieselbe IL-Prüfung validiert, sonst Vollscan wie bisher |
| A5 | Gate-Routing-Hooks (Same-PCL) beim Spielstart statt im ersten Kartentick installieren | Kartenstart-Einfrierer 230 → 24 ms | Nutzerentscheidung 20.10.2026 (lieber längerer Start); leere Masken → Vanilla wie bisher |
| A6 | Gatehouse-Erreichbarkeit: Eingang/Ausgang zuerst, Zugbrücken nur bei Unerreichbarkeit | Sim-Anteil ~7,6 % → ~2,4 % | identisch: im erreichbaren Fall bleibt `ShouldClose` unverändert, wie bei nicht verfügbarer Zugbrückenauswertung |
| A7 | Wegpunkt-Verfolgung: Wertvergleich statt Signatur-String pro Tick, Logtext nur bei Detaildiagnose | Allokationen pro Tick/Truppe entfallen | identische Änderungserkennung (alle 7 Felder) |
| A8 | APIShared: Assassinen-Rekonstruktion (E1640, Modus 3) akzeptiert auch das startseitige Verbindungsbit (Hook 0xE19C0, aktiv mit Torhaus-Klettern oder gewichteter Rekonstruktion) | 8-KI-Save: Rekonstruktionsfehler 112 → 3, größte Tick-Lücke 940–1020 → 138–153 ms, längster Frame 634–841 → 94–196 ms | gleiche Kante, die das Distanzfeld (D9C40) und der physische Schritt (DCE60) bereits nutzen; Nutzerentscheidung 20.10.2026 |
| A9 | Gate-Routing-Laufzeit (`EnemyGatePathfindingRuntime` inkl. Vergleichs-, Bewegungs- und Abstiegshooks) beim Laden der Bibliothek statt im ersten Kartentick erzeugen | erster Sim-Tick nach dem Laden 487–495 → 174–210 ms; BFQoL-Initialisierung +0,25 s | gleiche Hooks, gleiche logische Aktivierung beim Kartenstart; verzögerter Pfad bleibt, falls die Mod beim Start aus war. Gleiche Abwägung wie A5 (Nutzerentscheidung) |
| A10 | Gebäude-Angriffs-Fallback (`MoatCandidateField.Resolve`): Kantenprüfung für bereits besuchte Nicht-Ziele überspringen, Ziel-Lookup per Generationsarray statt Dictionary | Spitze `uc.attack.buildingConsumer` 94–107 → 28–30 ms (Feldsuche 102 → 22 ms bei gleichen 177 661 Knoten) | identische Distanzen und Knotenzahl: Kantenauswertung ist bis auf Diagnose seiteneffektfrei |

### A2 im Detail

- **Ursache:** Der Context-Hook liegt in Vanillas Sichtkachel-Renderer `0x41D60` (Sim-Thread) und lief für jede sichtbare Kachel. Er wechselte jedes Mal in verwalteten Code (alle Register sichern, nativ→Mono), auch wenn kein Marker existierte.
- **Sampler-Befund:** Stub-Code (`dyn`) 3,4 %, Übergangswrapper bis 10 %, Callback 1,5 % der Sim-Zeit.
- **Lösung:**
  - Der Stub prüft zuerst ein natives Byte (`renderDemand`). Bei 0 führt er nur die verdrängten Instruktionen aus.
  - Das Byte wird bei jeder Änderung von Rendering-Zustand, Vorschau oder Überlaufpuffer unter einer Sperre neu berechnet.
  - Die Bedarfsprüfung nutzt nur `push`/`pop`/`mov`/`movzx`/`jrcxz`/`jmp` (keine Flags) und stellt `RCX` wieder her.
- **Test:** `DemandGatedContextHookTests` führt den echten Stub aus und prüft:
  - eingehende Flags (ZF) bis zur Instruktion nach dem Hook;
  - GPR, `RCX`, XMM und Stack;
  - den aktiven Rumpf nur bei Bedarf (jeder Wert ≠ 0);
  - die verdrängten Instruktionen genau einmal auf beiden Pfaden.

### A3 im Detail

- **Ursache:** RedBird patcht jeden `NativeDetour` standardmäßig über `ThreadPatchScope`: No-GC-Region beantragen, Thread-Snapshot, alle Threads anhalten, Kontexte prüfen, patchen, fortsetzen. Gemessen ≈ 60 ms pro Detour; BFQoL setzt beim Start 63 eigene Detours.
- **Lösung:** Nur während BFQoLs `LibraryLoaded`-Handler bekommen Transaktionen und Direkt-Detours dasselbe Backend mit `SuspendThreadsDuringPatch = false`.
- **Begründung:**
  - Es läuft noch keine Simulation.
  - Der Script Extender installiert seine Detours zum selben Zeitpunkt genauso.
  - BFQoLs Context-Hooks patchen schon immer direkt.
  - Später installierte Hooks behalten die Suspendierung.
- **Nutzervorgabe:** Kosten werden **nicht** in den Kartenstart verschoben (lieber längerer Spielstart als Kartenstart-Lags).

### A9 im Detail

- **Befund:** Mit BFQoL dauerte der erste Sim-Tick nach dem Laden ~490 ms, mit SE ~120 ms.
  - Sampler (2-s-Fenster): 29 % in `GateRoutingFeature:Tick`, darunter RedBird (35 % der Stacks), `ntdll` und Mono.
  - `GateRoutingFeature.RefreshCore` erzeugte die Gate-Routing-Laufzeit erst, wenn eine Karte aktiv war, und installierte dabei die APIShared-Gate-Hooks.
- **Lösung:** `GateRoutingFeature.Initialize` (läuft in `LibraryLoaded`) ruft `EnsureRuntime()` auf, wenn das Gate-Routing gebraucht werden kann: `EnableMod` und (`EnableEnemyGatePathfindingFix` oder fremde Gate-Regeln). Ist der Fix aus, bleiben die Hooks wie bisher uninstalliert (Review E1). `RefreshCore` nutzt dieselbe Methode weiter für den Fall, dass die Mod erst später eingeschaltet wird. Die Karte wird wie bisher erst mit `BeginMap` aktiv.
- **Test:** `_inspect/GateMigrationFeatureTests` erwartet jetzt bewusst: Native Laufzeit genau einmal beim Laden, Karte inaktiv bis zum Sessionstart, keine Neuinstallation bei Kartenwechsel oder Reaktivierung. Neuer Modus `--own-fix-off` (in `Test-GateRouting.ps1`): Fix aus und keine Regeln → beim Laden nichts installiert, späteres Einschalten nutzt einmal den verzögerten Pfad.
- **Rest im ersten Tick:** Gate-Topologie und -Zugang 28–47 ms (`gate.tick.topology+access`), siehe Kandidaten.

### A10 im Detail

- **Befund:** Auf dem 8-KI-Save kostete ein einzelner Gebäude-Angriffsbefehl 94–107 ms in einem Tick (`uc.attack.buildingConsumer`). Davon entfielen 92–105 ms auf BFQoLs Fallback, nicht auf Vanilla.
  - Der Fallback bewertet Angriffsplätze neu, die Vanilla für unerreichbar hält (Friendly-Moat-Regeln).
  - Dazu flutet er per Breitensuche ein Erreichbarkeitsfeld vom Anführer aus: Beispiel 1 Start, 8 Ziele, alle gefunden, 177 661 expandierte Knoten.
- **Ursache:** Pro expandiertem Knoten liefen 8 Kanten-Delegates (`BuildingCandidateEdge` → `TryGetTraversalEdge`) und 8 Dictionary-Lookups, auch für bereits markierte Nachbarn, deren Ergebnis nichts mehr ändert.
- **Lösung:** Besuchte Nicht-Ziele werden übersprungen, offene Ziele über ein Generationsarray erkannt. Der Algorithmus bleibt eine BFS; Distanzen, Knotenzahl, Terminalkanten und Budgets bleiben identisch.
- **Test:** `Testmods/MoatMove/tests/SearchKernelTests.cs` kompiliert die Produktionsklasse aus BFQoL und läuft im regulären MoatMove-Testlauf. Der unabhängige BFS-Referenztest (zufällige gerichtete Graphen mit Terminalkanten, 6480 Distanzen) besteht. Neu ist eine Prüfung, dass besuchte Nicht-Ziele nicht neu bewertet werden.

## Untersuchungen

### Bekannte Lag-Ursache: verbesserte Assassinen-Wegsuche (A1)

`AssassinPathfindingRuntime.BuildWeightedPath` ersetzt die Assassinen-Wegsuche:

- Zuerst läuft immer Vanillas Builder (`RunVanillaBuilder`), danach eine eigene A*-Suche in verwaltetem Code über bis zu `maximumNodes` Knoten auf dem 800×800-Raster.
- Routen- und Suffix-Caches gelten nur innerhalb eines Befehls-Scopes (`OnTribeIssueOrderMoveHere`/`WithTarget`).
- Unerreichbare Ziele expandieren bis zum Knotenlimit.

Messung: DEVDIAG-Punkte `assassin:BuildWeightedPath` (Spike-Zeilen ab 5 ms mit Ergebnis, expandierten Knoten, Start/Ziel, Scope) und `assassin:vanillaBuilder`.

**Befund (Serie 2):**

- Ein KI-Gruppenbefehl löste im selben Tick 42 gewichtete Suchen aus: je ~13 000–15 000 expandierte Knoten, 6–7 ms, zusammen ~280 ms (Tick-Lücke 323 ms).
- Nur die erste Suche hatte einen exakten Einheiten-Frame und wurde verwendet (`weighted-staged-exact`).
- Die übrigen 41 endeten mit `weighted-exact-fallback`: Vanillas Ergebnis wird zurückgegeben, die gewichtete Route verworfen. Für diese Anfragen zählt nur, **ob** eine gültige Route existiert.

**Optimierung A1: Existenzprüfung statt kostenoptimaler Suche**

- **Bedingungen:** kein exakter Einheiten-Frame, kein Diagnosebeobachter aktiv, Knotenbudget ≥ Kachelzahl.
- **Verfahren:** `FindAnyWeightedRoute` statt A*: gierige Bestensuche über exakt dieselben Kanten (Boden, Klettern, reservierte Endpunkte, Gate-Policy), Priorität nach Octile-Zieldistanz.
- **Exaktheit:**
  - *Kein Ziel erreicht:* Der Erreichbarkeitsbereich ist erschöpft; A* hätte ebenfalls keine Route gefunden → 0 wie bisher.
  - *Ziel erreicht:* A* lehnt nur Routen über 2000 Knoten zusätzlich ab. Liegen die Kosten der gefundenen Route unter `2000 × kardinale Ticks`, hat auch die optimale Route sicher weniger als 2000 Knoten → Vanilla-Ergebnis wie bisher.
  - Andernfalls läuft die unveränderte A*-Suche.
- **Befehls-Caches (Review E2):** Der Existenzpfad füllt `RouteCache` und die Suffix-Heuristik (`CacheSuffixes`) des Befehls nicht. Die Antwort jeder Anfrage bleibt identisch. Spätere Suchen mit exaktem Frame im selben Befehl können aber bei mehreren gleich teuren Routen eine andere, gleich teure Route veröffentlichen. Bewusst akzeptiert: Die Caches zu füllen würde wieder die volle A*-Suche erfordern.
- **Theoretischer Restunterschied:** Bei inkonsistenter Suffix-Heuristik kann A* Knoten mehrfach expandieren und nominell das Budget erreichen. Nie beobachtet (max. ~15 000).
- **Test:** Verhaltenstest im Produktionsfixture (`VERIFY_EXISTENCE`): Auf allen vier Geländen, mit und ohne Klettern, mit verschiedenen Gate-Masken muss die Existenzantwort dem echten A*-Ergebnis entsprechen.
- **Ergebnis:** größte Tick-Lücke 323–334 → 145 ms (Serie 3+).

### 8-KI-Save: Stillstände durch fehlschlagende Assassinen-Rekonstruktion (A8)

- **Symptom:** mit BFQoL 7 Tick-Lücken > 100 ms pro Lauf, größte 940–1020 ms; nur SE: 1 Lücke, 110 ms.
- **Sampler:** In den Stillstandsfenstern lagen 17–20 % im BFQoL-Detour um die native Vollkarten-Reparatur `0xDAA50` (Lambda in `InstallConnectivityAndRecovery`). Darunter lief nativ `0xD86F0` über alle 320 800 Kacheln (~10 ms pro Aufruf).
- **Auslöser (DevDiag mit verwaltetem Stack und Befehlskontext):** KI-Assassinen-Gruppenbefehle.
  - Für **jede** Einheit scheiterte die Pfadrekonstruktion `0xE1640`.
  - Jeder Fehlschlag ruft `DAA50`, `E49D0`, `C3E50` und `DE6A0` auf (~19 ms); 28 Einheiten ≈ 530 ms in einem Tick.
- **Abgrenzung** (nativer Zähler `pathManager+0x78`):

  | Variante | Rekonstruktionsfehler |
  |---|---|
  | SE | 10 |
  | BFQoL | 112 |
  | BFQoL, gewichtete Suche aus | 110 |
  | BFQoL, Torhaus-Klettern aus | 10 |

  → Die Ursache liegt im Torhaus-Klettern.
- **Ursache (Nachrechnung des Abstiegs auf dem unveränderten Feld):**
  - Die Assassinen starten im eigenen Torhaus. Der Schritt auf die Zugbrücke ist eine Einbahnkante.
  - D9C40 baut das Feld über das Bit der startseitigen Kachel. E1640 prüft rückwärts nur das Bit der aktuellen Kachel.
  - Alle 63 Fehlschläge eines Laufs hatten genau diese Form. Torhaus-Klettern lässt die KI solche Gruppen viel öfter losschicken.
- **Lösungswege (Nutzerentscheidung):** E1640-Patch in APIShared (gewählt) statt nur die Reparatur zu überspringen.
- **Lösung A8:**
  - Neuer 14-Byte-Inline-Hook an `0xE19C0` (nur Modus 3) mit Emitter `AssassinEndpointEmitter.EmitReverseConnection`.
  - Logisch über die vorhandenen APIShared-Policy-Flags 1/2 geschaltet.
  - Dokumentiert in `APIShared/UpdateToNewDLL.md` und `APIShared/docs/NATIVE_SITES.md`.
- **Tests:**
  - `AssassinReconstructionConnectionTests` führt die echte E1640 aus der installierten DLL in einem Fixture-Modul aus:
    - ohne Regel: Vanilla-Fehlschlag mit 4 Reparaturaufrufen;
    - mit Regel: korrekte Route ohne Reparatur;
    - Modus 1 bleibt unberührt;
    - ein Schritt ganz ohne Bit scheitert weiter.
  - Site-Registry-Test und BFQoL-Spannentest für `0xE19C0` ergänzt.
- **Baseline:** `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md`.

### FPS-Schwankung zwischen Läufen

- Läufe auf dem 8-KI-Save liegen entweder bei ~190–206 FPS (SE-Niveau) oder bei ~142–155 FPS.
- In beiden Fällen sind die Sampler-Anteile von BFQoL gleich: Haupt- und Sim-Thread zeigen dieselben nativen und verwalteten Hotspots.
- Auch reine SE-Läufe zeigten diesen Modus (Serie 2: 67 statt 92 FPS).
- Die Schwankung ist daher nicht BFQoL zuzuordnen. Vergleiche brauchen mehrere Läufe; einzelne langsame Läufe nicht als Mod-Kosten werten.

### Verbleibende BFQoL-Anteile (8 KI, Momentaufnahme nach A10)

- **Sim-Thread je 10-s-Fenster:**
  - Gatehouse-Erreichbarkeit mit Zugbrücken-Footprints (`GatehouseDrawbridgeCoupling.BuildOrderedFootprintCandidates`, `TryIsUnitReachableToGate`): ~1–2 %;
  - Gate-Routing-Tick: < 1 %;
  - der Eintragszähler um den Vanilla-Builder (`InstallNativeOriginalEntryCounters`) enthält nur Vanilla-Arbeit.
- **Größte verbleibende Tick-Lücke** (140–145 ms, SE 110–114 ms): KI-Assassinen-Gruppenbefehle. Eine gewichtete Suche mit exaktem Frame kostet dort 16–48 ms (`weighted-staged-exact`, bis 92 550 Knoten), zusätzlich zu Vanillas eigener Suche.
- **Hauptthread:** keine BFQoL-Hotspots über 1 %. Größter verwalteter Posten ist Vanillas `FatControler.NoesisGUIUpdateChecksInGame` (wartet auf den Sim-Lock).

## Optimierungskandidaten (noch nicht umgesetzt)

| Kandidat | Kosten heute | Idee | Abwägung |
|---|---|---|---|
| Gate-Topologie beim Kartenstart | 28–47 ms im ersten Tick | Topologie-Snapshot schon beim Laden der Karte (außerhalb des Sim-Ticks) aufbauen | verschiebt Arbeit in den Ladevorgang; vorher Nutzer fragen |
| Gewichtete Assassinen-Suche mit exaktem Frame | 16–48 ms pro Gruppenbefehl | Suche nur für den tatsächlich veröffentlichten Frame, bessere Heuristik (Suffix-Cache über Befehle) | Verhalten muss exakt bleiben; Aufwand mittel |
| Gatehouse-Erreichbarkeit mit Zugbrücken | ~1–2 % Sim-Zeit | Footprint-Kandidaten pro Torhaus und Tick zwischenspeichern | geringer Gewinn |
| Hauptmenü-Startzeit | +4,4 s gegenüber SE | APIShared-Workshop-Initialisierung und Audio-Detours (mit Thread-Suspendierung) untersuchen; außerdem A3 auf die restlichen Start-Detours ausweiten: `DamagedHealthBarsRuntime`, `GatehouseLivingCapture/CaptureRuntime`, `RaidSearchObserver`, Direkt-Detours in `UnitCommands/Native` (je ~60 ms, Review E6). Dort scheiterte A3 zunächst an Wortlautprüfungen der Preflights | Startzeit, keine Ingame-Lags |

## Test- und Werkzeuganpassungen

Nötig, damit BugfixesAndQoL (auch als DEVDIAG-Build) wieder baut und Optimierungen nicht an Wortlautprüfungen scheitern:

- **`Shared/Tools/Validation/UnitCommandSourceChecks`:**
  - APIShared referenziert seit Version 35.2 `APIShared.Content` per HintPath auf eine erst beim Build entstehende DLL. Eine frische Arbeitskopie hatte sie nicht → CS0103 für ModData-/ModSettings-Typen.
  - Neu: Eine solche Referenz in den eigenen `bin`-Ordner eines Nachbarprojekts wird aus dessen Quellen kompiliert.
  - SDK-Projekte zählen Unterordner wie beim echten SDK mit (ohne `bin`/`obj`).
  - `netstandard`-Projekte werden gegen die .NET-Framework-Referenzen geprüft, so wie die Mods sie nutzen.
- **`AssassinPathfinding.Tests`:**
  - Der Wortlautvergleich ausgewählter Methoden mit Git-Commit `f57dfdb02` (`TestGitContractPreservation`) wurde entfernt. Er war eine reine Quelltextprüfung und hätte jede Optimierung blockiert. Das Verhalten prüfen weiterhin: A* gegen Dijkstra, Cache-Validierung sowie identische Routen-Prüfsummen, Expansionen und Heap-Operationen bei Diagnose an/aus.
  - Der Fixture-Extraktor übernimmt jetzt alle Typdeklarationen (auch Enums).
  - Die echte `DevDiag.cs` wird mitkompiliert.
  - Die IL-Prüfung (keine Uhr- und Statistikaufrufe bei abgeschalteter Diagnose) deckt auch den neuen Suchrumpf ab.
  - Neuer Existenz-Verhaltenstest (siehe A1).
- **`_inspect/TemporaryGateAcceptanceRuntimeTests`:** Die handkopierte DevDiag-Attrappe wurde durch die echte `Shared/Runtime/Diagnostics/DevDiag.cs` ersetzt.
- **`_inspect/GateMigrationFeatureTests`:** Erwartung an A9 angepasst (siehe A9).
- **BFQoL `tests/Program.cs`:**
  - Spannentest für `0xE19C0` (14 Byte) ergänzt.
  - Policytest „hostile filling …“ nachgezogen: Vorbestehend rot seit Commit `987`. Der Dig-Fallback sperrt `relationshipMode == 1` bewusst über `TraversalEnabled` statt `friendlyMovementEnabled`.
- **APIShared-Tests:** `AssassinReconstructionConnectionTests` (neu), erwartete Spannen in `NativeSiteRegistryTests`. Der Fear-Popularity-Test `RememberedTargetIsBoundToTheModuleBuild` gehört zu A4.
- **`Testmods/PerfFixTest`:** `nativecounters`-Logzeile; Hängererkennung (`StallSeconds`), Tick-Lücken, Zielzoom und Spielgeschwindigkeit in der Automation.
- **`Testmods/PerfProbe`:** Thread-Sampler (eigene `.pdata`-Suche, Abbruch nach heuristischem Schritt, fehlertolerante Modulliste). Der alte Long-Tick-Sampler startet nicht, wenn der Thread-Sampler aktiv ist. Ein früher Preloader-Sampler verursachte Heap-Korruption beim Start; Preloader-Projekt und Einstiegsmethode wurden entfernt.
- **Entfernte tote Dateien:** In `BugfixesAndQoL/tests/FriendlyMoatMovement.Tests` waren `SearchKernelTests.cs`, `FillFormationTests.cs`, `RuntimeHarness.cs`, `CursorTests.cs` und `PlacementTests.cs` seit Commit `600` per `Compile Remove` ausgeschlossen und nur veraltete Kopien der tatsächlich ausgeführten Dateien in `Testmods/MoatMove/tests`. Sie sind samt Ausschlusseinträgen entfernt; `ManualHarness.cs` bleibt (wird von `Program.cs` per Pfad gelesen).

## Review der Änderungen (ChangesReview.md, 20.10.2026)

| Befund | Bewertung | Umsetzung |
|---|---|---|
| E1 Gate-Hooks auch bei ausgeschaltetem Fix | zutreffend | Bedingung in `GateRoutingFeature.Initialize` an `RefreshCore` angeglichen, Test `--own-fix-off` |
| E2 A1 füllt keine Befehls-Caches | zutreffend, unkritisch | akzeptiert, Doku präzisiert („identisch pro Anfrage“) |
| E3 Formatierung | zutreffend (Editierartefakte) | Zeilen getrennt, Initialisierer formatiert, `LibraryLoaded`-Rumpf nach `InitializeLibraryRuntime` ausgelagert (frühes Abbrechen lässt den Breadcrumb wie bisher unvollständig) |
| E4 A2-Hook außerhalb der Transaktion | zutreffend, fail-open | bewusst belassen, Begründung im Code |
| E5 Installationsfenster nicht threadgebunden | zutreffend | `HookInstallPhase` merkt die Thread-ID; andere Threads behalten die Suspendierung |
| E6 A3 nicht überall | Hinweis | in die Kandidaten übernommen |
| E7 Tests/Verträge | zutreffend | A10-Prüfung in die ausgeführte MoatMove-Kopie verschoben; RedBird-`Suppress`-Vertrag in `AGENTS/native-hooks.md`; Semantik von Commit 987 bestätigt (dort wurde der Produktivcode bewusst auf `TraversalEnabled` umgestellt); APIShared-Release-Hinweis unter Offene Punkte |

## Offene Punkte

- **Sichtprüfungen im Spiel durch den Nutzer:**
  - Großbefehl-Marker: Vorschau beim Ziehen und Überlaufmarker nach dem Befehl (A2);
  - KI-Assassinen verlassen ihr Torhaus über die Zugbrücke und erreichen das Ziel (A8);
  - Gebäudeangriff mit Friendly-Moat-Fallback wählt dieselben Angriffsplätze wie zuvor (A10, rechnerisch identisch).
- **Diagnose (Stand Aufräumen 20.10.2026):**
  - Entfernt, weil die Untersuchung abgeschlossen ist: `uc.maskRebuild` samt Nachrechnung des E1640-Abstiegs (A8), `uc.attack.buildingConsumer.vanilla/.fallback/.publish` und `uc.attack.fallbackField` (A10).
  - Behalten und mit `TEMP_PERF_DIAG` markiert, weil die offenen Kandidaten sie brauchen (nur DEVDIAG-Builds, im normalen Build entfernt):
    - `startup.feature`/`startup.libraryLoaded(total)`: Startzeit;
    - `assassin:BuildWeightedPath`, `assassin:vanillaBuilder` samt `diagOutcome`/`diagExpanded`: Assassinen-Suche;
    - `gate.tick.epochInit/accessRefresh/topologyRefresh`, `gate.access.build`: Kartenstart.
  - Entfernen, sobald der jeweilige Kandidat erledigt ist: `git grep -n TEMP_PERF_DIAG`; vor `main`/Release `Shared/Tools/Validation/Test-DevDiagnostics.ps1 -ModDirectory BugfixesAndQoL -Enforce`.
- Die Kandidaten oben bei Bedarf umsetzen (A9-artige Verschiebungen nur nach Rückfrage).
- **APIShared-Release:** A8 erweitert die Wirkung von `SetWeightedReconstructionEnabled` und `SetDirectGatehouseClimbing` (Remarks im Code ergänzt). Beim Release die Version anheben und die Änderung in den Release-Notizen nennen (Review E7).
- Version unverändert.
