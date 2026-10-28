# Plan: neuer MoatMove-Modus „anchor“ (Variante B aus GroupPathProbe)

Stand 28.10.2026, Entwurf mit den Entscheidungen des Nutzers vom selben Tag, noch nichts umgesetzt. Grundlage: [GroupPathProbe](../GroupPathProbe/GROUP.md) und `BetterPathfinding.md` §18–18.2.

## Ziel und Abgrenzung

- **Neuer zusätzlicher Modus** `[Movement] Mode = anchor`. Precise, Fast und FastNative bleiben unverändert. Ersetzt wird erst nach Ingame-Bewährung und eigener Freigabe.
- B liefert „fast optimale“ Wege: mittlere Mehrkosten 2–5 Kosteneinheiten, p95 ≤ 6 % in den Offline-Tests. Der Modus lockert damit bewusst „optimale Kosten je Einheit“.
- **Graben-Entscheidung (entschieden):** reine Kostenminimierung mit gewichteten Grabenkosten, wie offline gemessen. Ein Feld je Anker und Profil; keine 40-Tick-Schwelle, kein zweites Bodenfeld.
- Erste Ausbaustufe: dieselben Befehle, die Fast heute verzögert, also menschliche Bewegungsbefehle für grabenfähige Einheiten. KI-Befehle, Angriffe, Arbeit, aktive Wegpunkt- und Patrouillenetappen laufen wie im Fast-Modus synchron weiter.

## Befunde aus dem Bestand (Momentaufnahme 28.10.2026)

- **Fast als Rahmen:**
  - Fast fängt Spielerbewegungen vor `0x196100` ab und reiht sie ein (`CaptureFastPlayerMove`, `FastCommandQueue`).
  - Die Felder werden pro Tick mit festem, deterministischem Budget vorangetrieben (`ProcessFastCommands`, 8.192 Expansionen).
  - Danach wird der Befehl über Vanilla ausgeführt (`DispatchFastCommand`). Jeder Einheitenweg entsteht erst dabei in `TryFastEncoded` und wird live geprüft und veröffentlicht.
  - Diese Stellen übernimmt der Anker-Modus; **neue native Hooks entfallen**.
- **Formation/LineUp (BugfixesAndQoL):** `ChooseOwnerSafeFormationSlot` fragt zuerst `FormationRuntime.TryChooseStandardFormationSlot` (bzw. den Assassinen-Wähler). Erst ohne aktive BFQoL-Formation greift MoatMoves `TryChooseFastFormation`, danach Vanillas `E1D30` mit Burggraben-Filter.
  - Die Plätze stehen also erst beim Ausführen fest und kommen aus verschiedenen Quellen.
  - BFQoL wählt sie per Breitensuche entlang der nativen Kanten ab dem Anker (`CaptureGroundSlots`, `CaptureReachableCandidates`). Sie sind deshalb meist auch über das Wegenetz nahe am Anker.
  - **Folge für den Entwurf:** Der Anker-Modus nimmt jeden vorgegebenen Platz je Einheit an und rechnet das Endstück **pro Einheit** beim Wegaufbau, nicht vorab für die Gruppe.
  - Heute bekommt Fast mit aktiver BFQoL-Formation für jeden fremden Platz ein eigenes Feld (`TryFastGroupSuffix` findet keinen Fast-Platz). Genau diesen Aufwand soll der Anker-Modus ersetzen.
- **Runtime-Hard-Gate:**
  - MoatMove hat keine JSON-Abhängigkeit und keine `OnDestroy`-, `OnDisable`- oder `OnApplicationQuit`-Pfade.
  - Die Runtime ist statisch verwurzelt: `MoatMovePlugin` mit statischen Feldern, Start über `CrusaderLibrary.Instance.LibraryLoaded`, Takt über `GameTimeManagerAPI.OnTick`, Missionsereignisse über `Shared.MissionEvents`.
  - Der Anker-Modus fügt keine Lifecyclemethode hinzu. Die native DLL wird einmal pro Prozess geladen und nie entladen.
- **Vanilla-Audit-Gate:** Vor Phase 2 den Datenfluss über Befehl, Formationswahl, `0x196280` und `TryFastEncoded` bis zur Veröffentlichung gegen den aktuellen Native-Hash und `_inspect/MoatMove/NativeAudit` neu prüfen und zusammenfassen. Das schließt die Reihenfolge der Platzwähler und die Platzübergabe je Einheit ein.
- **Multiplayer:** `NetworkMode=1`, wie heute müssen alle Spieler denselben Modus nutzen. B ist offline unabhängig von Thread-Anzahl und Zeit (byte-weise geprüft). Übernahme und Budget hängen nur an Ticks, siehe Phase 2.

## Aufbau

### Phase 0 – Offline-Nachweis der Einzel-Endstücke

Die bisherige Messung nutzte ein Gruppenrechteck mit Spurfeldern. Für die Einbindung wird in GroupPathProbe eine Variante „B je Einheit“ ergänzt und gemessen:
- Ankerfeld vorab.
- Je Einheit ein Rechteck aus Anker und eigenem Platz plus Rand.
- Darin eine kleine exakte Einspursuche vom Eintrittspunkt zum Platz. Zusätzlich wird der Weg über den Anker bewertet; der günstigere gewinnt.
- Ferne Plätze (Wegkosten vom Anker weit über dem räumlichen Abstand) erhalten ein eigenes, im Befehl zwischengespeichertes Feld.

Erfolgskriterium: Qualität mindestens wie §18.2, Zeit je Einheit im Mikrosekundenbereich. Wird das verfehlt, kommt der Plan vor der Umsetzung erneut zur Entscheidung.

**Ergebnis 28.10.2026** (`BetterPathfinding.md` §18.3, GroupPathProbe Modus 2):
- Mit Rand 8 wird die Qualität des Gruppen-B auf Mitschnitt und verstreuten Starts erreicht. Bei einer einzelnen Formation mit 1.024 Einheiten bleibt sie schlechter (p95 3,8 % statt 2,7 %).
- Die Abfrage je Einheit dauert 3–14 µs.
- Felder für ferne Plätze entstehen seriell bei der Abfrage, zusammen 16–46 ms je 10.000 Einheiten. Im Spiel fielen sie in den Ausführungstick.
- Standardrand für Phase 1 daher 8.

### Phase 1 – nativer Kern `MoatMoveAnchor.dll`

Aus GroupPathProbe abgeleitet, eigene C-ABI, im Plugin-Ordner ausgeliefert, per Vollpfad geladen, ABI- und Versionsprüfung beim Start.

- **Kartenmasken je Spieler und Kontext:**
  - Ein- und ausgehende Kantenbits sowie Grabenbits, aus demselben Prädikat wie Fast (`FastLiveEdge` → `TryGetTraversalEdge`).
  - Aufbau in Paketen mit fester Knotenzahl je Tick; einzelne Knoten lassen sich nach Änderungen ersetzen.
  - Kein Zugriff auf Spielspeicher aus Worker-Threads.
- **Feldaufgabe je Befehl:**
  - Gewichtetes Rückwärtsfeld vom Klickziel (Anker) für das Gruppenprofil, kompakt gespeichert (`uint32` je Knoten) bis zur Ausführung.
  - Profil: das in der Gruppe häufigste Geschwindigkeitsprofil. Andere Profile laufen auf demselben Feld mit ihren echten Kosten in der Endstückwahl.
- **Wegabfrage je Einheit:** Start + vorgegebener Platz → gepackte Richtungen, Boden- und Grabenschritte.
  - Abstieg im Ankerfeld, kleine Rechtecksuche, Vergleich mit dem Weg über den Anker.
  - Bei fernen Plätzen ein zwischengespeichertes Platzfeld.
  - Wege über 2.000 Schritte werden nie ausgegeben.
- **Fork-Join:** Die Feldaufgaben eines Ticks laufen parallel; der Simulationsthread wartet im selben Tick auf ihr Ende. Danach schlafen die Worker, sie warten nicht aktiv.
- **Threadzahl (entschieden: automatisch):** physische Kerne − 2, mindestens 1, höchstens 8.
  - Die zwei Kerne bleiben für Unity-Main- und Simulations-/Renderthread.
  - Physische Kerne werden über `GetLogicalProcessorInformationEx` ermittelt, damit SMT-Threads nicht doppelt zählen.
  - Obergrenze 8, weil offline von 4 auf 8 Threads kaum noch Gewinn entstand und mehr Worker auf großen CPUs schaden können (NVIDIA-Empfehlung).
  - Unterschiedliche Threadzahlen zwischen MP-Spielern sind unkritisch, weil die Ergebnisse gleich bleiben.
- **Speicher:** gezählter Allokator mit fester Obergrenze. Bei Erschöpfung gilt ein Befehl als unvollständig und wartet, nie als unerreichbar.

**Ergebnis 28.10.2026:** umgesetzt als [native/MoatMoveAnchor.cpp](native/MoatMoveAnchor.cpp), gebaut mit [native/Build-MoatMoveAnchor.ps1](native/Build-MoatMoveAnchor.ps1) nur nach `.local/anchor/`. Noch nicht eingebunden oder installiert.
- **Offline-Verträge** in GroupPathProbe (`--anchor-core`): 1.475 unabhängige Vergleiche bestehen. Geprüft werden:
  - Masken stückweise mit 1 Thread und in einem Stück mit 4 Threads, byte-gleich;
  - Pending → Ready, Stale nach Kartenänderung, nach Neustart die neue Abkürzung;
  - freigegebene Aufgaben antworten nicht mehr;
  - ferne Plätze mit genau einem gemeinsamen Feld, 2.000-Schritt-Grenze, Speicherbudget.
- **Messung** [core-20261028-172304-117](../GroupPathProbe/results/core-20261028-172304-117/results.json), eine Aufgabe je Gruppe, Rand 8, 10.000 Einheiten in 40 Gruppen:

  | | Mitschnitt | → 4 Ziele | verstreut | Arbeiterwege | Grabenkarte |
  |---|---:|---:|---:|---:|---:|
  | Felder, 8 Threads | 62 ms | 39 ms | 49 ms | 26 ms | 88 ms |
  | Felder, 1 Thread | 192 ms | 136 ms | 213 ms | 49 ms | 414 ms |
  | Abfragen gesamt (seriell) | 155 ms | 158 ms | 147 ms | 103 ms | 142 ms |
  | davon Felder für ferne Plätze | 26 ms | 36 ms | 18 ms | 0 | 0 |
  | je Einheit ohne diese Felder | 13 µs | 12 µs | 13 µs | 10 µs | 14 µs |
  | Mehrkosten p95 | 3,2 % | 5,6 % | 4,3 % | 0 % | 2,0 % |

- Die Qualität entspricht Phase 0 mit Rand 8. Die Graben-Entscheidung weicht nie ab.
- Das Schreiben der vollständigen Maske kostet im Kern 6–10 ms. Der Aufwand des verwalteten Prädikats ist darin nicht enthalten und wird in Phase 2 gemessen.
- Höchster Speicherstand mit 8 Threads 77–199 MiB.
- **Neuer Engpass:** die seriellen Abfragen bei der Ausführung, etwa 13 µs je Einheit (ein Befehl mit 250 Einheiten ≈ 3–4 ms im Ausführungstick). Die Felder vorab sind mit 8 Threads billiger als die Abfragen.

### Zwischenprüfung vor Phase 2: Graben-Entscheidung (28.10.2026)

Ergebnis in `BetterPathfinding.md` §18.5:
- **Wo entschieden wird:** „Graben nur ohne Bodenweg“ fällt in MoatMove (`TryFindFastRequiredRoute` → `FastGroundReachable`). BugfixesAndQoL fragt diese Entscheidung an allen relevanten Stellen über den Provider ab. `VanillaFailureProven` wird nicht ausgewertet.
- **Folge:** Kostenminimierung lässt sich voraussichtlich ohne Umbau von BugfixesAndQoL umsetzen. Für Bewegungsbefehle ersetzt sie in `TryFindFastRequiredRoute` die Bodenprüfung: Ein Grabenweg wird veröffentlicht, wenn der Anker-Weg Grabenschritte enthält.
- **Erste Stufe:** Angriffe, Arbeit und Cursor bleiben bei der Fast-Regel.
- **Messung:** Ein gewichtetes Feld je Befehl genügt; ein zusätzliches Bodenfeld verbessert keine Entscheidung. Gegenüber der Fast-Regel kostet das etwa 25 % mehr Feldzeit, vermeidet aber deren große Umwege (bis +86 % Gesamtkosten auf einer Mauerkarte).

### Phase 2 – verwaltete Anbindung in MoatMove

- `MoatMoveOptions`: neuer Wert `anchor`, `RequiredOnly`-Verhalten wie Fast.
- **Übergangsphase (entschieden):**
  - Solange die Kartenmasken des Spielers nicht vollständig sind (Kartenstart, Save-Laden, Neuaufbau nach Bündnis- oder Torwechsel), rechnet der vorhandene Fast-Pfad.
  - Ob ein Befehl Fast oder anchor nutzt, entscheidet allein der deterministische Maskenstand zu Beginn des Ticks. Er ist auf allen Peers gleich, weil der Aufbau tickgebunden ist.
  - Das ist eine vom Nutzer ausdrücklich erlaubte zeitweise Parallelnutzung von Fast.
- **Masken aktuell halten:** über die vorhandenen Wege `MarkFastTopologyTile`, `FastTraversalCache.Refresh` und den Zugriffs-Fingerabdruck. Geänderte Knoten werden an die DLL übertragen; hängende Feldaufgaben betroffener Befehle starten neu.
- **`ProcessFastCommands`:** Im Anker-Modus gehen die anstehenden Befehle als Feldaufgaben in einen Fork-Join-Aufruf. Das Budget zählt Feldaufgaben je Tick, nie Zeit. Was nicht hineinpasst, wartet wie heute.
- **Platzwahl:** keine eigene Verteilung. BFQoL-Formation/LineUp, Assassinen-Wähler und Vanilla-Wähler mit Burggraben-Filter bleiben unverändert und haben Vorrang. `TryChooseFastFormation` liefert im Anker-Modus nichts, damit kein Fast-Platz mit Endstückregeln die Formation überlagert.
- **`TryFastEncoded`:** Im Anker-Modus kommt der Weg aus der Wegabfrage. Die vorhandene Live-Prüfung (Identität, Start, Ziel, Puffer, alle Übergänge) und die Veröffentlichung bleiben unverändert.
- **Speichern und Laden:** Wie bei Fast werden nur die anstehenden Befehle gespeichert. Die Felder entstehen nach dem Laden neu; bis dahin gilt die Übergangsphase.
- **Diagnose:** über `Shared.DevDiag` und `build.bat /devdiag`. Gemessen werden Maskenaufbau, Felder je Tick, Fork-Join-Dauer, Abfragen je Einheit, ferne Plätze und die Übergangsdauer.

**Umsetzung 28.10.2026** (noch nicht über `build.bat` gebaut, nicht installiert, nicht im Spiel getestet):
- **Neu:** [src/AnchorRouting.cs](src/AnchorRouting.cs).
  - Interop zum Kern; die DLL wird per Vollpfad aus dem Plugin-Ordner geladen.
  - Eigene Kantencaches je Spieler. Masken entstehen auf Anforderung beim ersten Befehl eines Spielers, mit 4.096 Knoten je Tick.
  - Bei Änderungen der Zugriffsrechte oder Tore und nach fehlgeschlagener Live-Prüfung werden die Masken stückweise neu gelesen; Fasts Caches würden dafür synchron alle bekannten Knoten neu lesen.
  - Bis zu 16 Feldaufgaben je Tick mit automatisch gewählter Threadzahl.
  - Entscheidung per Kostenminimierung beim Ausführen.
  - Leistungszeile `stage=anchor-performance`.
- **Geänderte Einhängepunkte:**
  - `MoatMoveOptions` (`anchor`, Plugin-Pfad) und `MoatMovePlugin` (Beschreibung des Config-Werts).
  - `FriendlyMoatTraversalProvider.Install` startet den Kern.
  - `FastMovementScheduler`:
    - Anker-Tick vor der Befehlsschleife;
    - `AdvanceAnchorCommand` (Aufgabe anlegen oder abfragen; ohne fertige Maske weiter Fast);
    - bis zu 64 gleichzeitige Befehle;
    - Aufgabe während `DispatchFastCommand` aktiv;
    - Freigabe über `IDisposable`;
    - Kartenwechsel.
  - `FastMoatRouting`:
    - Anker-Entscheidung vor `FastGroundReachable`;
    - Live-Prüfung ausgelagert nach `TryFinishFastEncoded` (verhaltensgleich);
    - Kachel- und Gesamtmarkierungen erreichen auch die Anker-Caches.
  - `FastTraversalCache`: `Word`/`Reread`.
  - `FastGroupDistribution`: Fast-Platzverteilung im Anker-Modus aus.
  - `FastIntegration`: Leistungszeile.
- **Nicht im Anker-Modus:** Befehle ohne Aufgabe (Neuplanung nach dem Ausführen, nach Kämpfen, KI, Angriffe, Arbeit, Wegpunkte) behalten die Fast-Regel.
- **Kern:** Kostengrenze 255 (Profile werden durch den ggT gekürzt), Vertrag 0x04000002.
- **Prüfung:**
  - MoatMove-Preflight und die vollständige `MoatMove.Tests`-Suite bestehen; der Prüfrahmen kompiliert `AnchorRouting.cs` mit.
  - MSBuild ohne neue Warnungen, Ausgabe nur nach `.local/build/`.
  - Kern-Verträge (1.475 Vergleiche) bestehen.
  - Die verwaltete Anker-Anbindung selbst hat keinen eigenen Offline-Test; sie wird im Spiel geprüft.
- **Offen für Phase 3:** `build.bat` muss `MoatMoveAnchor.dll` bauen und ins Paket legen. Ohne die DLL meldet der Modus einen Fehler und arbeitet wie Fast.

### Phase 3 – Build

- `build.bat` von MoatMove kompiliert die DLL mit MSVC (Muster: `BetterPathfindingTest/native/Build-Native.ps1`), statische CRT, x64.
- Buildnachweis mit Quell- und DLL-Hash; die Paketprüfung umfasst die DLL.
- Die Version wird erst bei fertigem Feature erhöht.

**Umsetzungsstand (28.10.2026, 20:18):**
- `build.bat` ruft nach MSBuild `native/Build-MoatMoveAnchor.ps1 -OutputDirectory BepInEx\plugins\MoatMove_Serp` auf. Objekte, Importbibliothek und `build.json` bleiben in `.local/anchor` und kommen nicht ins Paket.
- `tests/Install-Package.ps1` verlangt `MoatMoveAnchor.dll` im Paket und prüft ihren Hash nach dem Kopieren. Der Standard-Konfigurationskommentar nennt `anchor`.
- `build.bat` begrenzt `Test-UnitCommandSplit.ps1` jetzt auf `-ModDirectory Testmods/MoatMove`, wie BFQoL. Die Branch-Build-Umschreibung begrenzt nur `Test-SharedBoundaries.ps1`; ungeschränkt scheiterte der Lauf an `APIShared/src/Projectiles/PlagueNativeCode.cs:24` (`VirtualProtect`). Das ist ein Workspace-Befund außerhalb von MoatMove.
- `build.bat /nopause` lief vollständig: Preflight, MoatMove-Tests, MSBuild, nativer Kern, Installation. Im Spielordner stimmen die Hashes von `MoatMove.dll`, `.pdb`, `MoatMoveAnchor.dll` und `info.json` mit dem Build überein (Buildnachweis `.work/b/working/566.build.json`). Version unverändert 0.1.5.
- Die vorhandene Spielkonfiguration bleibt erhalten (`Mode = precise`). Zum Testen `Mode = anchor` setzen und das Spiel neu starten.
- Noch offen: Ingame-Prüfung nach dem Abschnitt „Prüfung“.

### Robustheit nach dem ersten Ingame-Test (28.10.2026, abends)

Erster Editortest (Log `.local/ingame-anchor-1.log`): Mod und Modus aktiv, aber kein Befehl übernommen (`queued=0`), keine Maske angefordert. Der Lag von etwa 1 s bestand auch ohne MoatMove; seine Ursache untersucht eine eigene PerfProbe-Session.

**Korrektur zu §18.5 (BetterPathfinding.md):** Der Wegaufbau je Einheit in `src/Moved/Moved.UnitMovementContext.cs` ist im RequiredOnly-Ablauf strikt „Vanilla zuerst“. Den Provider fragt er nur nach einem Vanilla-Fehlschlag (`late-route-qualification` in `Moved.MovementPathPublication.cs`) oder nach einer gescheiterten Gruppen- bzw. Arbeitsprüfung. Für ein über Boden erreichbares Ziel fragt niemand nach dem Graben. Der Anker-Modus entscheidet deshalb nur für Einheiten ohne Bodenweg. Praktisch gilt die Fast-Semantik, aber mit dem Anker-Kern. Ein breiterer Vorfilter allein würde solche Befehle nur verzögern; er wurde deshalb nicht eingebaut. Echte Kostenminimierung braucht eine Änderung dieses Ablaufs samt Native-Audit und ist eine eigene Entscheidung.

**Umgesetzt:**
- Masken starten ohne Befehl: beim ersten Anker-Tick und alle 256 Ticks für lebende menschliche Spieler (`GetAlivePlayerIds`, ohne KI) sowie bei jedem Bewegungsbefehl eines Spielers. Bewusst nie über den lokalen Spieler, weil das im Multiplayer zwischen den Peers abweichen würde. Fehlgeschlagene Masken (`-2`) bleiben bis zum Kartenwechsel bei Fast; Anforderung und Fehler werden geloggt.
- Keine Allokationen im Tickpfad: wiederverwendete Puffer für Maskenaufbau, Resweep, Änderungen und Startlisten; Anker-Caches als Array statt `SortedDictionary`.
- Neue Aufgaben werden im selben Tick berechnet, solange das Tickbudget von 16 Aufgaben reicht (`StepAnchorTasks`). Das spart einen Tick Wartezeit.
- Veraltete Aufgaben starten höchstens dreimal neu (`AnchorMaxRestarts`), danach übernimmt Fast den Befehl.
- `tests/AnchorIntegrationTests.cs` prüft mit der echten DLL: Maskenaufbau in genau ⌈Knoten/4096⌉ Ticks, Masken nur für lebende Spieler, Berechnung im selben Tick, Grabenroute durch die Live-Prüfung, Veralten nach einer Geländeänderung, die Neustart-Grenze und einen allokationsfreien Leerlauf-Tick.
- `build.bat` kompiliert den Kern jetzt vor den Tests nach `.local/anchor` und kopiert ihn nach MSBuild hashgeprüft ins Paket (`Build-MoatMoveAnchor.ps1 -CopyOnly`).

### Phase 4 – Entwurf: echte Kostenminimierung und spätere Bodenübernahme (28.10.2026, nicht umgesetzt)

Anlass: Der Nutzer hat den Lag mit reinem Vanilla (`SCDE Vanilla.bat`) reproduziert; er ist also Vanilla-Arbeit. Kostenminimierung für über Boden erreichbare Ziele war ausdrücklich gewünscht.

**Audit-Grundlage (Native-Hash `FBCB93195FC7…2831E2`, gleich `CURRENT.json`):** Die native Kette ist im bestehenden Audit `_inspect/MoatMove/NativeAudit/PROGRESS.md` geschlossen und hier nicht neu bewertet:
- `196100 → 11B520`: Gruppenfeld, Formationsplätze `E1D30`, danach `196280` je Einheit.
- `196280 → F4930/E32B0`: Suche und Rekonstruktion in den eigenen 1000-Byte-Puffer der Einheit; danach Veröffentlichung und Konsum über `1855A0`.
- Grabenrouten für über Boden erreichbare Ziele veröffentlicht Precise schon heute über diese Kette.

Neu geprüft wurde die verwaltete Verzweigung in MoatMove:
- **Moduserkennung je Einheit** `ResolveScopedCompletedMoatModeTraversal` (`src/Moved/Moved.UnitMovementContext.cs`): Im RequiredOnly-Ablauf kehrt sie ohne Qualifizierung mit Vanillas Ergebnis zurück („Vanilla zuerst“, Zähler `fastVanillaBypasses`). Danach kommt `TryDeferToNativeGroundPlan`: gleiche PCL-Region oder ein native Regionspaar-Probe. Erst danach fragt sie den Provider (`TryFindRequiredFriendlyCompletedMoatRouteForPlan → TryFindFastRequiredRoute`). Eine Qualifizierung setzt `ModeObserved` und liefert den Moatmodus 1.
- **Builder-Hook** (`src/Moved/Moved.MovementPathPublication.cs`): Im RequiredOnly-Ablauf ersetzt `TryReplaceUnsafeFallbackPath` eine qualifizierte Route im bereits gebundenen Einheitenpuffer, ohne `F4930` für diese Einheit auszuführen. Bedingungen: eigener Puffer, Variante 0/1, `requireMoat`. Danach wird die Route live geprüft, und `variant`/`moatPathMode` werden wie bei einer nativen Veröffentlichung gesetzt. Für Rekonstruktionen gibt es denselben Weg schon mit `requireMoat: false` (`groundReconstruction`).
- **Später Weg:** `late-route-qualification` nach einem Vanilla-Fehlschlag mit `NativeGroundPrecheck`.

**Teil A – Kostenminimierung (empfohlen als nächster Schritt):**
1. Während eines Anker-Dispatch (`anchorDispatchTask != 0`) überspringt die Moduserkennung den Vanilla-zuerst-Rücksprung und `TryDeferToNativeGroundPlan`. Sie fragt direkt den Provider, der über `TryAnchorRequiredRoute` antwortet:
   - Grabenweg günstiger: Qualifizierung, Modus 1, Veröffentlichung ohne `F4930` über den vorhandenen Builder-Weg.
   - Bodenentscheidung: Rückkehr mit Vanillas Ergebnis; Vanilla rechnet wie bisher.
   - Kann der Kern nicht antworten (veraltet, unerreichbar, über der Grenze): bisherige Fast-Kette unverändert.
2. Den Vorfilter in `TryCaptureFastPlayerMove` wieder erweitern: Übernommen wird jeder normale Bewegungsbefehl mit grabenfähigen Einheiten, wenn die Spielermaske fertig ist und Grabenkanten enthält. Die Grabenkanten werden deterministisch in der Maske erfasst.
3. Unverändert bleiben: Angriffe, Arbeit, Wegpunkte, Cursor, Assassinen, BFQoL-Formation/LineUp (Plätze stehen beim Dispatch fest), alle nativen Hooks und Adressen. Neue native Adressen gibt es nicht.
4. Kosten: ein Ankerfeld je übernommenem Befehl, synchron im Sim-Tick (gemessen offline 26–88 ms für 10.000 Einheiten mit 8 Threads). Dazu kommen höchstens die Ticks bis zur Berechnung.

**Teil B – Bodenwege aus dem Anker veröffentlichen (erst nach PerfProbe-Befund):** Auch Bodenentscheidungen würden über `TryReplaceUnsafeFallbackPath(requireMoat: false)` veröffentlicht, und `F4930` entfiele für alle übernommenen Einheiten. Das hilft nur, wenn der Lag tatsächlich die Suche je Einheit in `196280` ist. Das eigene Gruppenfeld von `11B520` (Flood bis 400.000 Knoten) und die Formationswahl `E1D30` blieben native. Die neue PerfProbe-Takt-Messung muss zuerst zeigen, welche Funktionen während des Stillstands laufen.

**Offene Punkte und Unsicherheiten:**
- Ob die Moduserkennung für jede Einheit eines Gruppenbefehls aufgerufen wird oder nur in bestimmten `196280`-Zweigen. Statisch nicht abschließend geklärt; die Zähler `ModeCalls` und `BuilderCalls` je Befehl müssen im DEVDIAG-Lauf der Einheitenzahl entsprechen.
- Abfragen im Kern müssen in deterministischer Reihenfolge kommen (Unteranker-Cache je Aufgabe). Die Reihenfolge der `196280`-Aufrufe aus `11B520` ist nativ fest; Zwischenziele (`region intermediate`) bekommen eine eigene Abfrage.
- Vanillas Ziel kann bei Regionsübergängen ein Zwischenziel sein (`BuilderIntermediateTargets`); der Anker antwortet dann für das tatsächliche Builder-Ziel.
- Multiplayer: Alle Entscheidungen hängen nur von Simulationszustand und Tick ab. Ein echter Host-/Clientlauf ist trotzdem Pflicht.

**Prüfung für Teil A:**
- Harness: Ein Fixture mit Bodenumweg und günstigerem Graben (Mauer-Muster aus §18.5) zeigt Grabenwahl im Anker-Modus und Bodenwahl in Fast.
- Ein Fixture mit günstigerem Bodenweg zeigt, dass Vanilla unverändert bleibt.
- Vorhandene Suite und Anker-Integrationstest bleiben grün.
- Ingame (DEVDIAG): Zähler `ModeCalls`, `BuilderCalls`, `anchor-performance`, PerfProbe-Tick-Lücken gegen Fast auf derselben Karte.

## Prüfung

- **Offline:**
  - GroupPathProbe-Verträge auf den Kern übertragen: Referenzsuche, Thread-Unabhängigkeit, ferne Plätze, 2.000-Schritt-Grenze, paketweiser Maskenaufbau gleich Gesamtaufbau.
  - Vergleich gegen Precise auf Masken aus dem echten MoatMove-Prädikat.
  - Vorhandene `MoatMove.Tests` bleiben grün; deren Befehls-, Speicher- und Rücknahmefälle zusätzlich im Anker-Modus und in der Übergangsphase.
- **Prüfauswahl nach Relevanz:** `Test-SharedBoundaries.ps1 -ModDirectory Testmods/MoatMove`, `tests/Test-Preflight.ps1`, Native-Hook-Inventar unverändert, CRLF.
- **Ingame (Einzelspieler):** Precise, Fast und anchor auf derselben Stresskarte. Szenarien:
  - große Gruppen mit und ohne BFQoL-Formation/LineUp, auch Assassinen-Gruppen;
  - Grabenquerung;
  - Stop, schneller Ersatzbefehl, Teilgruppen, Shift und Patrouille;
  - Speichern mit anstehenden Befehlen, Kartenwechsel, Befehle während der Übergangsphase.

  Gemessen werden Ticklücken über 50/100 ms (PerfProbe) und sichtbare Umwege.
- **Multiplayer:** echter Host- und Clientlauf mit gleichem Modus und unterschiedlicher Kernzahl. Übergangsende und Übernahmetiks müssen übereinstimmen, und es darf keine Desynchronisation geben.

## Risiken

- Der verwaltete Maskenaufbau kann teurer sein als die Wegsuche (Befund aus FastNative). Das wird in Phase 1 gemessen, und die Paketgröße je Tick danach festgelegt.
- Der Fork-Join hält den Simulations-Tick an, bis alle Feldaufgaben fertig sind. Viele gleichzeitige Gruppen können daher weiterhin Spitzen von etwa 100 ms erzeugen, statt bisher Sekunden.
- Gemischte Profile in einer Gruppe nutzen ein gemeinsames Feld. Für Minderheitsprofile ist der Weg weniger genau.
- Die Offline-Lasten sind konstruiert. Die echten Befehlsmuster und BFQoL-Formationen auf realen Karten sind noch nicht gemessen.
