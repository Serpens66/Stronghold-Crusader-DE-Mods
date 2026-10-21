# BugfixesAndQoL – Performance

Ergebnisse der Performance-Optimierung von BugfixesAndQoL (BFQoL) und APIShared im Vergleich zu „nur Script Extender“ (SE). Ziel: FPS möglichst nah an SE, keine von BFQoL verursachten Freezes über 100 ms, kürzerer Spielstart, ohne Funktionen zu verschlechtern.

- **Stand:** 21.10.2026, native DLL `FBCB9319…`.
- **Allgemeine Mess- und Analysemethoden:** [AGENTS/performance.md](AGENTS/performance.md).
- **Abgrenzung von Vanilla- und Extender-Kosten:** [VanillaOptimization.md](VanillaOptimization.md).

## Ergebnis

| Messgröße | vor der Optimierung | jetzt | nur SE |
|---|---|---|---|
| dusty-Save (Speed 250): FPS pausiert / laufend | 106–107 / 84–85 | 107,9 / 86,7 | 108–109 / 80,0–89,3 |
| dusty-Save: Frames > 100 ms | 0–1 | 1 | 1 |
| dusty-Save: Ticks/s | 143–145 | 203 | 208–217 |
| dusty-Save: größte Tick-Lücke | 323–334 ms | 150 ms | 102 ms |
| 8-KI-Save (Speed 300): FPS laufend | 142–155 | 189–206 | 193–207 |
| 8-KI-Save: Tick-Lücken > 100 ms pro Lauf | 7 | 1 | 1 |
| 8-KI-Save: größte Tick-Lücke | 940–1020 ms | 140–143 ms | 110–114 ms |
| erster Sim-Tick nach dem Laden (8 KI) | 487–495 ms | 174–210 ms | ~115–124 ms |
| BFQoL-Initialisierung (`LibraryLoaded`) | 6,0 s | 2,25–2,34 s | – |
| Hauptmenü nach Prozessstart | 20–21 s | 16,9–17,3 s | 12,5–13,0 s |

**Belastbarkeit:**

- Die SE-Werte und die 8-KI-Werte stammen aus je mindestens zwei Läufen.
- Für den aktuellen Stand gibt es auf dem dusty-Save **einen** verwertbaren BFQoL-Lauf. Der zweite fiel in den langsamen FPS-Modus, siehe „Befunde“.
- Die FPS-Gleichheit auf dem dusty-Save ist daher plausibel, aber noch nicht mit Wiederholungsläufen belegt.

**Verbleibende Unterschiede zu SE:**

- **Größte Tick-Lücke** (+35–48 ms): Ursache sind KI-Assassinen-Gruppenbefehle mit gewichteter Suche für die Einheit, deren Route tatsächlich veröffentlicht wird. Eine solche Suche kostet 16–48 ms (bis 92 550 Knoten) zusätzlich zu Vanillas eigener Suche.
- **Kartenstart** (+50–90 ms): Gate-Topologie und Gate-Zugang kosten im ersten Tick 28–47 ms.
- **Spielstart** (+4,4 s bis zum Hauptmenü): APIShared kostet ~2,6 s, BFQoL ~2,3 s (nach A3).
- **Sim-Thread im laufenden Spiel:** Gatehouse-Erreichbarkeit mit Zugbrücken ~1–2 %, Gate-Routing-Tick < 1 %.
- **Hauptthread:** keine BFQoL-Anteile über 1 %.

## Umgesetzte Optimierungen

| Nr. | Was | Wirkung | Verhalten |
|---|---|---|---|
| A1 | Assassinen: Existenzprüfung statt A*, wenn keine Route veröffentlicht wird | größte Tick-Lücke 323–334 → 145 ms | Antwort jeder Anfrage identisch |
| A2 | Sichtkachel-Marker-Hook mit nativem Bedarfsbyte (`DemandGatedContextHook`) | ≈ 1 ms pro Sim-Tick; Frames > 50 ms 15–20 → 3 | unverändert, sobald Marker existieren |
| A3 | Detours beim Laden der Bibliothek ohne Thread-Suspendierung (`HookInstallPhase`) | BFQoL-Initialisierung 6,0 → 2,0 s | gleiche Hooks, gleicher Zeitpunkt |
| A4 | APIShared: Fear-Popularity-Ziel per MVID merken | Hauptmenü −0,7 s ab dem 2. Start | identisch, gemerktes Ziel wird jedes Mal validiert |
| A5 | Same-PCL-Gate-Hooks beim Spielstart statt im ersten Kartentick | Kartenstart-Freeze 230 → 24 ms | Nutzerentscheidung |
| A6 | Gatehouse-Erreichbarkeit: Eingang/Ausgang zuerst, Zugbrücken nur bei Bedarf | Sim-Anteil ~7,6 → ~2,4 % | identisch |
| A7 | Wegpunkt-Verfolgung: Wertvergleich statt Signatur-String pro Tick | Allokationen pro Tick und Truppe entfallen | identisch |
| A8 | APIShared: Assassinen-Rekonstruktion akzeptiert die Kante, die Suche und Bewegung schon nutzen | 8 KI: Rekonstruktionsfehler 112 → 3, größte Tick-Lücke ~1 s → ~140 ms | Nutzerentscheidung |
| A9 | Gate-Routing-Laufzeit beim Laden der Bibliothek statt im ersten Kartentick | erster Sim-Tick 490 → 174–210 ms, Start +0,25 s | Nutzerentscheidung |
| A10 | Gebäude-Angriffs-Fallback: besuchte Felder nicht neu bewerten | Spitze 94–107 → 28–30 ms | identisch |

### A1 – Existenzprüfung für Assassinen-Gruppenanfragen

- **Ursache:** Bei einem KI-Gruppenbefehl lief für jede Einheit eine gewichtete A*-Suche (je ~13 000–15 000 Knoten, 6–7 ms). Verwendet wurde nur die Route der Einheit mit exaktem Frame; für die übrigen 41 von 42 Anfragen zählte nur, **ob** eine gültige Route existiert.
- **Lösung:** `FindAnyWeightedRoute` (`BugfixesAndQoL/src/AssassinPathfindingRuntime.cs`): eine gierige Bestensuche über dieselben Kanten wie A* (Boden, Klettern, reservierte Endpunkte, Gate-Policy).
  - Bedingungen: kein exakter Frame, kein Diagnosebeobachter, Knotenbudget ≥ Kachelzahl.
  - Kein Ziel erreicht → 0, wie A*.
  - Ziel erreicht mit Kosten unter `2000 × kardinale Ticks` → das Vanilla-Ergebnis; auch die optimale Route liegt dann sicher unter dem 2000-Knoten-Limit von A*.
  - Sonst läuft die unveränderte A*-Suche.
- **Bewusst akzeptiert:** Der Existenzpfad füllt die Routen- und Suffix-Caches des Befehls nicht. Spätere Suchen im selben Befehl können daher bei mehreren gleich teuren Routen eine andere, gleich teure Route wählen. Die Caches zu füllen würde wieder die volle Suche erfordern.
- **Test:** `VERIFY_EXISTENCE` im Produktionsfixture vergleicht die Existenzantwort mit dem echten A*-Ergebnis: vier Gelände, mit und ohne Klettern, ohne Maske, mit sperrender und mit musterweise sperrender Gate-Maske.

### A2 – Bedarfsgesteuerter Context-Hook

- **Ursache:** Der Marker-Hook in Vanillas Sichtkachel-Renderer `0x41D60` wechselte für jede sichtbare Kachel in verwalteten Code, auch ohne Marker. Sampler: Stub 3,4 %, Übergänge bis 10 %, Callback 1,5 % der Sim-Zeit.
- **Lösung:** `BugfixesAndQoL/src/DemandGatedContextHook.cs`.
  - Der Stub prüft zuerst ein natives Byte und führt bei 0 nur die verdrängten Instruktionen aus. Dabei ändert er keine Flags und stellt `RCX` wieder her.
  - `LargeMoveTargetMarkerRenderer.UpdateRenderDemand` berechnet das Byte bei jeder Änderung des Rendering-, Vorschau- oder Überlaufzustands neu.
- **Bewusst:** Der Hook wird nach dem Commit der übrigen Transaktion aktiviert. Scheitert das, bleibt der Zustand fail-open (Begründung im Code).
- **Test:** `DemandGatedContextHookTests` führt den echten Stub aus und prüft Flags, Register, XMM, Stack sowie die verdrängten Instruktionen auf beiden Pfaden. Der Vertrag von RedBirds Context-Generator mit `Suppress` ist nur über die Quelle belegt (siehe [AGENTS/native-hooks.md](AGENTS/native-hooks.md)).

### A3 – Detours ohne Thread-Suspendierung beim Laden

- **Ursache:** RedBird hält bei jedem `NativeDetour` alle Threads an (≈ 60 ms pro Detour); BFQoL setzt beim Start 63 eigene Detours.
- **Lösung:** `BugfixesAndQoL/src/HookInstallPhase.cs` liefert nur während BFQoLs `LibraryLoaded` und nur auf dem Thread, der dieses Fenster geöffnet hat, dasselbe Backend mit `SuspendThreadsDuringPatch = false`.
- **Begründung:** Zu diesem Zeitpunkt läuft noch keine Simulation, und der Script Extender installiert seine Detours zur selben Zeit ebenso. Später installierte Hooks behalten die Suspendierung.
- **Restliste geprüft (21.10.2026):** `DamagedHealthBarsRuntime`, `GatehouseLivingCapture/CaptureRuntime` und `RaidSearchObserver` installieren nur Inline-Hooks; diese schreiben ohne Thread-Suspendierung (Commit 0 ms). Die `CreateDetour`-Aufrufe in `UnitCommands/Native` sind nicht installierte Vertragsproben. Im Startlog vom 21.10.2026 00:32 (SE, APIShared, BFQoL, ExtendedData) kostet kein Mod-Commit ≥ 20 ms; nur der SE-eigene Commit (72 ms). Die verbleibende Startzeit liegt also nicht in Hook-Transaktionen.

### A4 – Fear-Popularity-Ziel merken (APIShared)

`APIShared/src/Economy/FearPopularityRuntime.cs` merkt sich das IL-Ziel als „MVID + Token“ in `BepInEx/cache/APIShared_FearPopularityTarget.txt` und validiert es bei jedem Start über dieselbe IL-Prüfung. Bei jeder Abweichung läuft der Vollscan wie bisher. Test: `RememberedTargetIsBoundToTheModuleBuild`.

### A5, A9 – Gate-Routing beim Laden statt im ersten Kartentick

- **Ursache:** Die Gate-Routing-Laufzeit samt APIShared-Gate-Hooks entstand erst im ersten Sim-Tick einer Karte. Im Sampler zeigte sich das mit RedBird, `ntdll` und Mono im Stack.
- **Lösung:**
  - `GateRoutingFeature.Initialize` erzeugt die Laufzeit (`EnsureRuntime`) und `SamePclGateRouteRuntime.InstallHooksEagerly` installiert ihre Hooks, sobald das Gate-Routing gebraucht werden kann: `EnableMod` und (`EnableEnemyGatePathfindingFix` oder fremde Gate-Regeln).
  - Ist der Fix aus, bleiben die Hooks uninstalliert; späteres Einschalten nutzt einmal den verzögerten Pfad.
  - Die Karte wird wie bisher erst beim Sessionstart aktiv.
- **Nutzerentscheidung (20.10.2026):** Ein längerer Spielstart ist besser als ein Freeze beim Kartenstart.
- **Test:** `_inspect/GateMigrationFeatureTests` (inkl. Modus `--own-fix-off` in `BugfixesAndQoL/Test-GateRouting.ps1`).

### A6 – Gatehouse-Erreichbarkeit

`ReachableEnemyGatehouseRuntime` prüft Eingang und Ausgang zuerst (eigener Tick-Cache). Die teure Zugbrücken-Auswertung läuft nur noch, wenn beides unerreichbar ist. Der Aufrufer behandelt „erreichbar“ und „nicht auswertbar“ gleich, das Verhalten ist also identisch.

### A7 – Wegpunkt-Verfolgung

`UnitCommandPathRuntime.QueueTracking` vergleicht alle sieben Felder des Snapshots direkt. Der Logtext entsteht nur noch bei Detaildiagnose, die Tribe-Liste nutzt einen wiederverwendeten Puffer.

### A8 – Assassinen-Rekonstruktion (APIShared, nativer Hook `0xE19C0`)

- **Symptom (8 KI):** 7 Stillstände über 100 ms pro Lauf, bis ~1 s.
- **Ursache:** Assassinen starten im eigenen Torhaus; der Schritt auf die Zugbrücke ist eine Einbahnkante.
  - Die Suche D9C40 baut das Distanzfeld über das Bit der startseitigen Kachel.
  - Die Rekonstruktion E1640 (Modus 3) prüft rückwärts nur das Bit der aktuellen Kachel. Damit scheiterte sie für jede Einheit und rief die Vollkarten-Reparatur auf (`DAA50`, `E49D0`, `C3E50`, `DE6A0`, ~19 ms pro Einheit).
  - Torhaus-Klettern lässt die KI solche Gruppen viel häufiger losschicken.
- **Lösung (Nutzerentscheidung):** Ein 14-Byte-Inline-Hook an `0xE19C0` akzeptiert in Modus 3 zusätzlich das Bit der Nachbarkachel in Gegenrichtung. Er ist logisch über die APIShared-Policy-Flags 1/2 geschaltet (Torhaus-Klettern oder gewichtete Rekonstruktion). Der physische Schritt DCE60 akzeptiert diese Kante ohnehin.
- **Verträge:**
  - `APIShared/UpdateToNewDLL.md`, `APIShared/docs/NATIVE_SITES.md`;
  - Native-Baseline `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md`.
- **Test:** `AssassinReconstructionConnectionTests` führt die echte E1640 aus der installierten DLL aus:
  - ohne Regel: Vanilla-Fehlschlag mit Reparatur;
  - mit Regel: korrekte Route ohne Reparatur;
  - Modus 1 bleibt unverändert;
  - ein Schritt ganz ohne Bit scheitert weiter.
  - Dazu kommen Spannentests in APIShared (Site-Registry) und BFQoL.
- **APIShared-Release:** Die Wirkung von `SetWeightedReconstructionEnabled` und `SetDirectGatehouseClimbing` ist erweitert (Remarks im Code). Beim nächsten APIShared-Release die Version anheben und die Änderung nennen.

### A10 – Gebäude-Angriffs-Fallback

- **Ursache:** Der Friendly-Moat-Fallback flutet per Breitensuche ein Erreichbarkeitsfeld (Beispiel: 1 Start, 8 Ziele, 177 661 Knoten). Dabei rief er pro Knoten acht Kanten-Delegates und acht Dictionary-Lookups auf, auch für bereits besuchte Nachbarn.
- **Lösung:** `MoatCandidateField.Resolve` überspringt besuchte Nicht-Ziele und erkennt offene Ziele über ein Generationsarray. Die Kantenfunktion ist bis auf Diagnosezustand seiteneffektfrei; Distanzen und Knotenzahl bleiben identisch. Feldsuche 102 → 22 ms.
- **Test:** `Testmods/MoatMove/tests/SearchKernelTests.cs` gegen die BFQoL-Produktivklasse:
  - unabhängige BFS-Referenz, 6480 Distanzen;
  - Prüfung, dass besuchte Nicht-Ziele nicht neu bewertet werden.

## ExtraFeatures (21.10.2026)

ExtraFeatures war in keiner Messreihe enthalten. Die Optimierungen beruhen auf Code- und Startlog-Analyse. Laufzeitnachweis (Lauf `EF1`, 8-KI-Save, Speed aus dem Save, SE + APIShared + BFQoL + ExtendedData + ExtraFeatures develop-Arbeitsstand): PASS, 7247 Ticks in 60 s, keine Error-Zeilen, beide Furcht-Stubs installiert, Heiler- und Pestflaggen-Commit < 20 ms. Ein A/B-Vergleich mit und ohne ExtraFeatures steht noch aus.

- **E3 (Nutzerentscheidung 21.10.2026: vorziehen):** Der KI-Verteidigungs-Detour `placementHook` und die 20 ElevatedMoat-Hooks werden jetzt im eigenen `LibraryLoaded` installiert, nicht mehr beim ersten Kartenstart mit aktivem Feature. Sie bleiben bis zur Aktivierung logisch abgeschaltet (Vanilla-Pfad). Der APIShared-Build-Step-Observer wird weiterhin erst bei Bedarf registriert, damit dessen Hook ohne Observer allokationsfrei bleibt. Ein Installationsfehler wird nur gemeldet, wenn das Feature eingeschaltet wird. Ergebnis im Log: Kartenstart ohne Commit statt 8 + 66 ms; Start +7 ms (Moat-Commit) bzw. 0 ms (Detour ohne Suspendierung).
- **E3-Lauf:** `EF3` (gleiches Setup wie `EF1`): PASS, 173,9 FPS / 125,6 Ticks/s gegenüber 170,8 / 120,8 bei EF1, keine Error-Zeilen. Der Lauf `EF2` direkt nach mehreren Builds lag bereits in der Pause-Phase (ohne Spiellogik) 15 % niedriger und gilt als Hintergrundlast, nicht als Effekt.

| Nr. | Was | Wirkung | Verhalten |
|---|---|---|---|
| E1 | Furchtfaktor: Detour auf die Schadensfunktion `0x180560` und Context-Hook im Einheiten-Overlay `0x1A19F2` durch rein native Inline-Stubs mit Flag-Byte ersetzt (`FearFactorNativeStubs`) | kein verwalteter Übergang mehr pro Nah-/Fernkampftreffer und pro markierter sichtbarer Einheit und Tick im Renderer `0x41D60`, auch nicht im Standardzustand „aus“; Start −55 ms (Detour-Suspendierung entfällt) | identisch (Ausführungstest gegen die Vanilla-Bytes) |
| E2 | Start-Detours ohne Thread-Suspendierung im eigenen `LibraryLoaded` (`ExtraFeaturesHookInfrastructure.DetourBackend`, wie A3) | Heiler-Detour, Heiler-Probe und KI-Pestflaggen-Detour sparen zusammen ~0,2 s (Log_349: 55 + 55 ms Commits plus ~0,1 s Probe) | gleiche Hooks, gleicher Zeitpunkt |

- **E1-Test:** `_inspect/ExtraFeaturesNativeTests/FearFactorStubTests.cs` (läuft im `build.bat`) führt Vanilla-Bytes und beide Stub-Pfade in einem Fake-Image mit den auditierten RVA-Abständen aus und vergleicht alle GPRs; dazu echte RedBird-Spannen und ein Rohscan aller ausführbaren Sektionen auf Sprünge in die 17-Byte-Spanne. Mutationsprobe: falsches Rückgaberegister bzw. fehlender Flag-Sprung lassen den Test scheitern.
- **Geprüft, nicht geändert:** Tick- und Event-Handler kehren früh zurück (der Script Extender allokiert seine Event-Args ohnehin unabhängig von Abonnenten). Der Pest-Lebensdauer-Hook `0x9A16C` läuft nur für Krankheitsprojektile, der Heiler-Listen-Detour einmal pro nativer Listen-Neuberechnung, der KI-Platzierungs-Detour pro KI-Bauschritt. Dort lohnt sich der Umbau nicht.

## Bestätigte und widerlegte Befunde

| Befund | Status | Beleg |
|---|---|---|
| APIShared kostet laufend ~15 % FPS | widerlegt | Fremdlast im ersten Lauf; in sauberen Wiederholungen gleich wie SE |
| FPS-Schwankung zwischen Läufen (~145–155 statt ~190–206 FPS auf 8 KI) stammt von BFQoL | widerlegt | gleiche Sampler-Anteile in schnellen und langsamen Läufen; tritt auch nur mit SE auf |
| Ticks/s zwischen Varianten messen den Mod-Overhead | widerlegt | Ticks/s mischen Overhead und geändertes KI-Verhalten; Sampler-Anteile und native Zähler sind belastbar |
| Die verbesserte Assassinen-Wegsuche verursacht die 8-KI-Stillstände | widerlegt | ohne gewichtete Suche weiter 110 Rekonstruktionsfehler; Ursache war die Rekonstruktion bei Torhaus-Klettern (A8) |
| Torhaus-Klettern löst gehäufte Vanilla-Fehlschläge in E1640 aus | bestätigt | Rekonstruktionsfehler: SE 10, BFQoL 112, ohne Torhaus-Klettern 10, mit A8 3 |
| Der lange erste Tick nach dem Laden kam von der Hook-Installation | bestätigt | 490 → 174–210 ms nach A9 |
| Die Spitze beim Gebäudeangriff liegt in BFQoLs Fallback, nicht in Vanilla | bestätigt | Vanilla-Teil < 3 ms, Fallback 92–105 ms |
| Der größte native Sim-Anteil (`CDE:0xF5A40` ~45–65 %, Renderer `0x41D60` ~15–20 %) ist Mod-Arbeit | widerlegt | in allen Varianten gleich, Vanilla |
| Ein Sampler als BepInEx-Preloader kann den Start profilieren | widerlegt | Heap-Korruption beim Start (0xC0000374); verworfen |
| Thread-Suspendierung kostet ≈ 60 ms pro Start-Detour | bestätigt | BFQoL-Initialisierung 6,0 → 2,0 s mit A3 |

## Weiterarbeit in einem neuen Chat

### Vorbereitung

1. Pflichtlektüre: Root-`AGENTS.md`, [AGENTS/performance.md](AGENTS/performance.md) und diese Datei. Bei nativen Hooks zusätzlich `AGENTS/native-hooks.md` und `AGENTS/native-analysis.md`, bei APIShared `AGENTS/apishared.md`.
2. Zustand prüfen:
   - `git status --short` im Workspace und in `APIShared`;
   - die installierten Plugins unter `BepInEx/plugins`;
   - die ursprüngliche Auswahl in `.work/a/bfqol-perf/001/original-plugins.txt`.
3. **Messpunkte:** Die Messpunkte für die offenen Punkte sind im Code mit `TEMP_PERF_DIAG` markiert (`git grep -n TEMP_PERF_DIAG`). Sie wirken nur im DEVDIAG-Build:
   - Startzeit: `startup.feature`, `startup.libraryLoaded(total)`;
   - Assassinen: `assassin:BuildWeightedPath`, `assassin:vanillaBuilder`;
   - Kartenstart: `gate.tick.*`, `gate.access.build`.
4. **Messen:**
   - DEVDIAG-Build: `.work/a/bfqol-perf/001/Build-Clean.ps1 -Driver BugfixesAndQoL\build.bat -Arguments /nopause,/devdiag`. Den Exitcode prüfen: Bei einem Fehlschlag bleibt die alte DLL installiert.
   - Serie, abwechselnd und mindestens zweimal je Variante, z. B. `.work/a/bfqol-perf/001/Run-Series.ps1 -Runs 'S1=SE','B1=BFQ','S2=SE','B2=BFQ' -SavePath test_dustynoattack2.sav -GameSpeed 250`.
   - 8-KI-Save: `test_8lordsmiddle.sav` mit `-GameSpeed 0`. Für den Sampler `+PerfProbe_Serp` anhängen und `-SamplerWindow 2|10` wählen.
5. **Nach jeder Serie:**
   - Plugin-Auswahl wiederherstellen: `.work/a/bfqol-perf/001/Run-Variant.ps1 -RestoreOnly -Label restore -Plugins x`;
   - im PerfProbe-Config `[Sampler] Enabled = false` setzen;
   - BFQoL wieder ohne `/devdiag` bauen.
6. **Abwägungsregel des Nutzers:** Arbeit nicht vom Spielstart in den Kartenstart oder ins laufende Spiel verschieben; solche Abwägungen vorher vorlegen. Funktionen bleiben exakt erhalten; bei mehreren wesentlich verschiedenen Lösungen oder einer empfohlenen Feature-Entfernung den Nutzer fragen.

### Empfohlene Reihenfolge

| Nr. | Aufgabe | Ziel | Vorgehen | Prüfung |
|---|---|---|---|---|
| 1 | Wiederholungsläufe dusty-Save | FPS-Gleichheit belegen | Serie SE/BFQ im Wechsel, je mindestens 2 gültige Läufe; Läufe mit pausiert < ~100 FPS nicht werten | Tabelle „Ergebnis“ aktualisieren |
| 2 | Spielstart | Hauptmenü näher an SE (heute +4,4 s) | a) A3-Restliste: erledigt bzw. gegenstandslos (siehe A3, 21.10.2026). b) APIShared- und BFQoL-Start untersuchen: Hook-Commits kosten laut Startlog nichts Nennenswertes mehr, die Zeit liegt in der übrigen Initialisierung (z. B. Workshop). Startphasen mit `startup.feature` eingrenzen | `startup menuReadySinceProcessStartS` über ≥ 2 Läufe; kein Freeze beim Kartenstart |
| 3 | Assassinen-Suche mit exaktem Frame | größte Tick-Lücke näher an SE (heute 140–150 ms) | Die gewichtete A*-Suche für die veröffentlichte Route (`SearchAndPublishWeightedPath` → `TryBuildWeightedRoute`) beschleunigen, ohne die Route zu ändern. Ansätze: Suffix-Heuristik über Befehle hinweg wiederverwenden, Kantenauswertung wie bei A10 entschlacken, Allokationen im Suchpfad prüfen. Ergebnis und Knotenzahl über `assassin:BuildWeightedPath` messen | Produktionsfixture: A* gegen Dijkstra, identische Routen-Prüfsummen; 8-KI-Save |
| 4 | Gate-Topologie im ersten Tick | Kartenstart näher an SE (heute +28–47 ms) | Den Topologie-Snapshot vor dem ersten Sim-Tick aufbauen | **vorher Nutzer fragen** (Arbeit verschiebt sich in den Ladevorgang) |
| 5 | Gatehouse-Erreichbarkeit mit Zugbrücken | 1–2 % Sim-Zeit | Footprint-Kandidaten pro Torhaus und Tick zwischenspeichern | nur bei geringem Aufwand; identisches Verhalten |

### Noch offen außerhalb der Optimierung

- **Sichtprüfungen im Spiel durch den Nutzer:**
  - Großbefehl-Marker (Vorschau beim Ziehen, Überlaufmarker; A2);
  - KI-Assassinen verlassen ihr Torhaus über die Zugbrücke (A8);
  - Gebäudeangriffe wählen dieselben Angriffsplätze (A10).
- **Diagnose aufräumen:** Jeden `TEMP_PERF_DIAG`-Messpunkt entfernen, sobald sein Punkt erledigt ist. Vor `main` oder einem Release: `Shared/Tools/Validation/Test-DevDiagnostics.ps1 -ModDirectory BugfixesAndQoL -Enforce`.
- **APIShared-Version:** beim nächsten Release anheben (A8).

## Geänderte Tests und Werkzeuge

- **Messwerkzeuge:**
  - `Testmods/PerfFixTest`: Automation mit Zielzoom, Spielgeschwindigkeit, Tick-Lücken, Startzeiten und nativen Pfadzählern (`nativecounters`); `Run-Automation.ps1` mit Hängererkennung.
  - `Testmods/PerfProbe`: Thread-Sampler für Main- und Sim-Thread (nur DEVDIAG).
  - Hilfsskripte unter `.work/a/bfqol-perf/001/` (siehe [AGENTS/performance.md](AGENTS/performance.md)).
- **BFQoL-Tests:**
  - `DemandGatedContextHookTests` (neu);
  - Spannentest für `0xE19C0`;
  - Existenztest im Assassinen-Produktionsfixture;
  - `_inspect/GateMigrationFeatureTests` mit dem Modus `--own-fix-off`.
- **APIShared-Tests:** `AssassinReconstructionConnectionTests`, `NativeSiteRegistryTests`, `FearPopularityIlTests`.
- **Behaltene Verhaltensprüfungen, entfernte Wortlautprüfungen:**
  - `AssassinPathfinding.Tests` vergleicht keine Methodentexte mit einem alten Commit mehr; die Verhaltensprüfungen bleiben.
  - `_inspect/TemporaryGateAcceptanceRuntimeTests` nutzt die echte `DevDiag.cs`.
  - Der Policytest „hostile filling …“ folgt der in Commit `987` bewusst geänderten Semantik (`TraversalEnabled`).
- **`Shared/Tools/Validation/UnitCommandSourceChecks`:** kompiliert APIShared.Content aus den Quellen, wenn die DLL noch nicht gebaut ist.
- **Entfernt:**
  - fünf seit Commit `600` ausgeschlossene Testdubletten in `BugfixesAndQoL/tests/FriendlyMoatMovement.Tests`; die ausgeführten Kopien liegen in `Testmods/MoatMove/tests`;
  - das unbrauchbare PerfProbe-Preloader-Projekt.
