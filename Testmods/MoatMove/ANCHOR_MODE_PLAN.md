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

**Teil B – Bodenwege aus dem Anker veröffentlichen:** Auch Bodenentscheidungen würden über `TryReplaceUnsafeFallbackPath(requireMoat: false)` veröffentlicht, und `F4930` entfiele für alle übernommenen Einheiten. Das eigene Gruppenfeld von `11B520` und die Formationswahl `E1D30` blieben native.

**PerfProbe-Befund (Lauf 28.10.2026, 23:49, Log `Testmods/PerfProbe/.local/manual-tickgap-2.log`, Auswertung in `Testmods/PerfProbe/DIAGNOSTICS.md`):**
- Je Bewegungsbefehl mit etwa 1350 Einheiten steht die Simulation 476–617 ms still, bei 24–32 wartenden Simulationsschritten. Der Timer läuft normal.
- 65–80 % der Sim-Stichproben liegen in `CDE:0xDA5EF`, also in `DA590` (Größe 1212 Byte ab RVA `0xDA590`, Native-Hash wie oben). Das ist die BFS-Flutsuche mit +1 gerade und +2 diagonal, die im normalen Fallback bis 400.000 Knoten expandiert.
- Aufgerufen wird sie unter Vanillas Pfad-Builder `F4930` in der Hülle je Einheit; die Zeit gehört dem Vanilla-Original.
- Der Nutzer sieht denselben Lag mit reinem Vanilla. Teil B zielt damit direkt auf den gemessenen Engpass und ist der eigentliche Hebel gegen den Lag.

**Zusätzlich offen für Teil B:**
- Einheiten ohne Grabenfähigkeit (`CanDigMoat` falsch) laufen nicht durch MoatMoves Hooks und behielten `F4930`. Für sie bräuchte es ein reines Bodenfeld im Kern.
- Vanillas BFS wertet Diagonalen mit 2, der Anker gewichtet nach Profil. Gleich teure Wege können anders aussehen; das wird dokumentiert und ingame bewertet.
- Ziele ohne Weg: Vanilla expandiert dann bis zur Grenze. Der Anker meldet „unerreichbar“; dann läuft die bisherige Kette weiter, und hier bleibt die Vanilla-Last bestehen.

**Offene Punkte und Unsicherheiten:**
- Ob die Moduserkennung für jede Einheit eines Gruppenbefehls aufgerufen wird oder nur in bestimmten `196280`-Zweigen. Statisch nicht abschließend geklärt; die Zähler `ModeCalls` und `BuilderCalls` je Befehl müssen im DEVDIAG-Lauf der Einheitenzahl entsprechen.
- Abfragen im Kern müssen in deterministischer Reihenfolge kommen (Unteranker-Cache je Aufgabe). Die Reihenfolge der `196280`-Aufrufe aus `11B520` ist nativ fest; Zwischenziele (`region intermediate`) bekommen eine eigene Abfrage.
- Vanillas Ziel kann bei Regionsübergängen ein Zwischenziel sein (`BuilderIntermediateTargets`); der Anker antwortet dann für das tatsächliche Builder-Ziel.
- Multiplayer: Alle Entscheidungen hängen nur von Simulationszustand und Tick ab. Ein echter Host-/Clientlauf ist trotzdem Pflicht.

**Umsetzungsstand (29.10.2026):** A und B gemeinsam umgesetzt, noch nicht im Spiel geprüft.
- Vorfilter (`TryCaptureFastPlayerMove`): Übernommen wird jeder normale Bewegungsbefehl mit mindestens einer grabenfähigen Einheit, sobald die Spielermaske fertig ist. Die geplante Erfassung der Grabenkanten entfällt, weil Teil B auch reine Bodenbefehle lohnt.
- Moduserkennung (`Moved.UnitMovementContext.cs`, `TryAnchorQualifiesMoat`): Während eines Anker-Dispatch fragt sie direkt den Kern, nicht die Provider-Kette. Antwortet der Kern nicht, rechnet deshalb kein synchrones Fast-Feld je Einheit, sondern die bisherige Vanilla-zuerst-Kette läuft unverändert. Ein Grabenweg qualifiziert wie nach einem Vanilla-Fehlschlag.
- Builder (`Moved.MovementPathPublication.cs`, `TryPrepareAnchorGround`): Bodenentscheidungen werden über `TryReplaceUnsafeFallbackPath(requireMoat: false)` samt Live-Prüfung veröffentlicht, ohne `F4930`. Ausgenommen sind Zwischenziele (`ExactRouteEndpoints`), Arbeit, Angriffe und Vanillas eigener Grabenmodus (`VanillaModeDetected`). Bei Ablehnung wird der Puffer zurückgesetzt und Vanillas Builder läuft unverändert.
- Neue Messzahl `groundRoutes` in `stage=anchor-performance`. Erwartung im Spiel: `moatRoutes + groundRoutes` liegt nahe an der Zahl der grabenfähigen Einheiten je Befehl.
- Harness: Die Moduserkennung qualifiziert die Grabenroute des Kerns, der Builder erhält bei einer Bodenentscheidung die Bodenroute, und die vier Ausnahmen greifen. Die Kostenentscheidung selbst prüfen die Kernverträge und `GroupPathProbe --cost-min`; ein Harness-Fixture mit Bodenumweg fehlt noch.

**Einstieg korrigiert (29.10.2026, nach drei Ingame-Läufen):**
- Befund: In keinem Lauf wurde ein Befehl übernommen (`captureCalls=0`).
- BFQoLs Formations-Feature führt jeden Gruppenbefehl selbst aus. Es baut ein eigenes `FormationOrderPacket`, wendet es im Einzelspieler direkt im `EngineRunHook` vor `EngineInterface.run` an und im Multiplayer über einen Chore. Beides ruft `GameTribeManagerAPI.IssueMoveHereCommand` und damit die Hook-Implementierung des Extenders für `11B520` auf.
- Auch der Befehl mit Ziel (Chore 36, `12BF0 → 199C70 → 11E960`) erreicht `11B520` ohne `196100`.
- Fasts Aufschub an `196100` (Chore 17) sieht diese Wege nie. Die Annahme aus Phase 2, alle Spielerbewegungen kämen über `196100`, war falsch.
- Neuer Einstieg: `TribeR3EventHooks.OnTribeIssueOrderMoveHere` (Pre/Post um `11B520`, im installierten SHCDESE vorhanden).
  - Pre: Für lebende menschliche Spieler mit fertiger Maske werden die grabenfähigen Mitglieder erfasst und die Aufgabe synchron berechnet (`TryComputeAnchorTask`). Während des Aufrufs ist `anchorDispatchTask` gesetzt.
  - Post: Die Aufgabe wird freigegeben.
  - Verschachtelte Aufrufe bilden einen Stapel. Übersprungene Originale (kein Post) werden vom umgebenden Post oder im nächsten Tick abgeräumt.
  - Nur auf dem Sim-Thread (`anchorSimThread` aus dem Tick).
- Der Aufschub an `196100` ist im Anker-Modus abgeschaltet (Ablehnungsgrund `anchorMode`). `AdvanceAnchorCommand`, Neustart-Grenze und Tickbudget sind entfernt; Fast bleibt unverändert.
- Neue Messzahlen in `anchor-performance`: `maskPendingMoves`, `fieldMs`, `maxFieldMs`.

**Erster Ingame-Lauf mit synchronem Einstieg (29.10.2026, 11:23, Log `.local/ingame-sync-1.log`):** Editorkarte `test_moatmove`, rund 1350 Einheiten, BFQoL-Formation aktiv, sechs Rechtsklick-Befehle.
- Stillstände über 100 ms: nur einer, 142,7 ms beim ersten Befehl. Bei den fünf weiteren gab es keinen. Vorher lagen alle Befehle bei 490–660 ms.
- `anchor-performance`: sechs Aufgaben, je Befehl 1350–1352 veröffentlichte Routen (`moatRoutes` bzw. `groundRoutes`), `fallbacks=0`, `maxFieldMs` 2,0–10,7.
- Fasts synchrone Felder entfallen: `expanded=0`, `maxSynchronousMs` ≤ 1,0. BFQoLs Gruppenprüfung `uc.groupMoat` erscheint nicht mehr als Spike.
- Der Maskenaufbau für alle acht im Editor „lebenden“ Spieler kostete im ersten Fenster zusammen etwa 5 s CPU, verteilt auf rund 1250 Ticks (etwa 4 ms je Tick).
- Noch offen: sichtbares Verhalten der Einheiten, Kampf- und Arbeitsbefehle, Multiplayer.

**Einheiten ohne Grabenfähigkeit (29.10.2026, nicht im Spiel geprüft):**
- Befund: BFQoLs `GetUnitMovePlan` legt für sie keinen Plan an, und MoatMoves Builder-Hook rief deshalb immer Vanillas `F4930` auf. Den Bewegungsrahmen (`UnitMoveFrame` mit Einheiten-ID und Ziel) legt BFQoL dagegen für jede Einheit an.
- Kern, Vertrag `0x04000003`: Grabenkosten `0` bedeuten ein reines Bodenfeld; nasse Kanten werden in Feldaufbau und Abstieg nicht verwendet. Die Kernverträge von GroupPathProbe (`--anchor-core`, 1475 Vergleiche) bestehen weiter.
- Je Gruppenbefehl entstehen zwei Aufgaben: gewichtet für grabenfähige Mitglieder (`anchorDispatchTask`) und rein für den Boden für alle übrigen (`anchorGroundTask`).
- Ein verschachtelter Befehl verdeckt jetzt die Aufgaben des äußeren, auch wenn er selbst keine hat.
- Builder (`TryPublishAnchorGroundWithoutPlan`): Hat die Einheit keinen BFQoL-Plan und keine Grabenfähigkeit, wird die Route der reinen Bodenaufgabe veröffentlicht, mit derselben Puffer-, Identitäts- und Live-Prüfung. Das gilt nur, wenn das Builder-Ziel dem Ziel des Bewegungsrahmens entspricht; Zwischenziele bleiben bei Vanilla.
- Neue Messzahl `nonDiggerRoutes`.
- Harness: Die reine Bodenaufgabe sperrt den Weg durch den Graben, öffnet den Bodenteil und wird von grabenfähigen Einheiten nicht benutzt.

**KI-Gruppenbefehle und sparsamere Masken (29.10.2026, nicht im Spiel geprüft):**
- KI: Gruppenbefehle von KI-Spielern laufen jetzt ebenfalls über den synchronen Einstieg. Das ändert das KI-Verhalten: KI-Einheiten können eigene Gräben queren, wenn das günstiger ist, und Nicht-Gräber erhalten Bodenrouten aus dem Kern. Neue Messzahl `aiMoves`. Ein Lauf mit KI-Lords und ein Multiplayer-Lauf stehen aus.
- Masken: Statt für alle „lebenden“ Spieler (im Editor alle acht) entstehen sie nur für Spieler, die lebende Einheiten steuern. Geprüft wird beim ersten Anker-Tick und alle 256 Ticks (`ScanAnchorPlayers` über `UnitAccess.GetAllReallyAliveUnits`), dazu bei jedem Gruppenbefehl. Die Prüfung hängt nur von Tick und Simulationszustand ab und ist damit auf allen Peers gleich.

**Ingame-Lauf gemischte Armee und KI (29.10.2026, 12:09–12:12, Log `.local/ingame-mixed-ai-1.log`):**
- Editor mit Reitern: `nonDiggerRoutes` 21–871 je Fenster, 1371–1373 Routen je Befehl, `fallbacks=0`. Bei fünf Befehlen lagen die Stillstände bei 139–229 ms, bei den übrigen unter 100 ms. Ein Teil davon war Fasts synchrones Feld in der Gruppenprüfung (`uc.groupMoat`, 49–60 ms).
- KI (`test_stresstest4.sav`): 39 Stillstände von 350–1050 ms bei einer Baseline von 10–11 ms je Tick.
  - Hauptursache: Fasts synchrones Ersatzfeld außerhalb des Ankers (`uc.combatResume` und `uc.groupMoat` je 21×, `maxSynchronousMs` bis 930, bis 2,8 Mio. Knoten je Fenster).
  - `AnchorTick` kostete etwa 4–6 ms je Tick. Masken für KI-Spieler wurden kaum fertig (Spieler 2 nach 56 s, 3–8 nie), wahrscheinlich weil Resweeps fertiger Masken das Budget aufbrauchten.
- Der Fall „neuer Befehl hebt den alten nicht für alle Einheiten auf“ (Nutzerbild) ist aus dem Log nicht erklärbar: Jede Einheit bekam je Befehl eine Route. Offen; Gegenprobe mit `Mode = fast` bzw. ohne MoatMove.

**Korrekturen danach (29.10.2026, nicht im Spiel geprüft):**
- Im Anker-Modus läuft außerhalb des Ankers kein synchrones Fast-Feld mehr (`TryFindFastRequiredRouteCore`, Zähler `anchorSkips`). Es gilt dann Vanillas Ergebnis. Ausnahme ist Grabenarbeit (`MoatWorkMovement`), die für Graben- und Auffüllziele Fast behält. Folge: Ohne Anker-Antwort (Kampffortsetzung, KI vor fertiger Maske) quert eine Einheit keinen Graben, wenn Vanilla keinen Bodenweg findet.
- Anker-Aufgaben enthalten je Mitglied die native Startkachel und zusätzlich die aktuelle Kachel. Die Gruppenprüfung in `11B520` fragt mit der aktuellen Kachel; ohne sie meldete der Kern „unerreichbar“, und Fast sprang ein.
- Maskenaufbau: Unfertige Masken kommen zuerst an die Reihe, menschliche Spieler vor KI. Resweeps nutzen nur das verbleibende Budget.

**Zweiter Lauf gemischte Armee und KI (29.10.2026, 12:32–12:36, Log `.local/ingame-mixed-ai-2.log`):**
- Editor, 1352 Gräber und 950 Reiter: rund 2300 Routen je Befehl, `nonDiggerRoutes=950`, `anchorSkips=0`, kein Fast-Feld (`expanded=0`), `maxFieldMs` 1–2. Stillstand je Befehl weiterhin 140–220 ms bei geringer Sim-Last. Er liegt also außerhalb von `run`, im Einzelspieler-Formationsablauf von BFQoL; die genaue Aufteilung ist ungemessen.
- KI: Alle acht Masken waren nach 16 s fertig. Die Stillstände lagen meist bei 110–230 ms (vorher durchgehend 350–1050); Fast höchstens 7 ms je Abfrage; bis 48 KI-Befehle je Fenster über den Anker.
- Ein Ausreißer von 4,5 s um 12:34:45: 91 % der Stichproben in `mma_query`. Ursache: Starts, die den Anker nicht erreichen (KI hinter Mauern), sowie volle Zusatzanker. Beide bauten je Abfrage ein ungecachtes Feld über die ganze Karte.
- `AnchorTick` kostete auch mit fertigen Masken etwa 5 ms je Tick, durch Resweeps nach Tor- und Zugangsänderungen.

**Korrekturen danach (29.10.2026, nicht im Spiel geprüft):**
- Kern, Vertrag `0x04000004`:
  - Erreicht das Hauptfeld nicht alle Starts (erschöpft) und enthält es das Ziel, aber nicht den Start, meldet die Abfrage sofort „unerreichbar“. Das ist exakt: Im gerichteten Graphen erreicht der Start dann auch das Ziel nicht.
  - Sind alle 16 Zusatzanker belegt, meldet die Abfrage „über der Grenze“ statt ein Wegwerf-Feld zu bauen; die Einheit behält Vanilla.
  - Die GroupPathProbe-Kernverträge bestehen.
- Resweeps fertiger Masken nutzen höchstens 1024 Knoten je Tick. Veraltete Maskenteile erzeugen keine unsicheren Routen, weil jede Route live geprüft wird.

**Messausbau und Wiederverwendung vor dem nächsten Sammellauf (29.10.2026, Build 13:01, nicht im Spiel geprüft):**
- Neue Logzeile `stage=anchor-breakdown` je Messfenster, direkt nach `anchor-performance`:
  - `moveMs`/`maxMoveMs`: Zeit von Pre bis Post der Gruppenbewegung (`11B520`), nur für Bewegungen mit Anker-Aufgabe. `preMs`: Mitgliedererfassung und Feldberechnung im Pre. `queryMs`, `encodeMs`, `publishMs`: Kernabfragen, Live-Prüfung der Route und Pufferveröffentlichung je Einheit. Was `moveMs` darüber hinaus enthält, ist Vanillas eigene Arbeit innerhalb von `11B520`. Ein Stillstand außerhalb von `moveMs` liegt außerhalb der Gruppenbewegung (z. B. BFQoL-Formation).
  - Ablehnungen: `queryStatus(...)` nach Kernstatus, `encodeRejects` (Live-Prüfung), `publishRejects` (Veröffentlichung).
  - `routeReuses`: Der Builder übernimmt die Bodenroute der Modusprobe derselben Einheit im selben Tick, statt den Kern ein zweites Mal zu fragen.
  - `members`, `unreachedMembers`, `movesWithUnreached`: Gruppenmitglieder, die Vanilla in der Gruppenbewegung nicht neu geplant hat (`OnUnitMoveHere`, `196280`). Je Karte höchstens 40 Zeilen `anchor-move-unreached` mit bis zu acht Einheiten (Typ, Kachel, Stamm, Spieler, Lebenszustand). Damit ist der Bildfall „alter Befehl bleibt für einige Einheiten“ messbar.
  - `tick(...)`: Kosten von `AnchorTick` nach Refresh, Maskenaufbau und Resweep mit Knotenzahlen, Zahl der Resweeps und Zugangsänderungen.
  - `core(...)`: Zähler des Kerns (`mma_stats`, je Fenster zurückgesetzt): Abfragen, Zusatzanker, volle Zusatzanker, Schritt-, Abfrage- und Zusatzankerzeit, Speicherhöchststand.
- Die Modusprobe behält ihre Bodenroute auf dem Plan (`publishGround`). Harness: Wiederverwendung ohne zweite Abfrage und Zählung nicht neu geplanter Mitglieder.
- PerfProbe für den Sammellauf: `[Sampler] Enabled = true`, `SimOutsideRun = true`; nach der Auswertung wieder aus.

**Sammellauf (29.10.2026, 13:09–13:20, Log `.local/ingame-breakdown-1.log`):**
- Editor, 1350 Gräber und 1050 Reiter (2400 Mitglieder je Befehl), 15 Befehle:
  - Stillstand je Befehl 134–241 ms (PerfProbe `tick:gap`). Davon entfallen 100–200 ms auf `moveMs`, also auf MoatMoves Arbeit je Einheit innerhalb von `11B520`. Die frühere Vermutung, der Rest liege in BFQoLs Formationsablauf, ist damit widerlegt; `FormationRuntime.ApplyPacketLocked` hat nur etwa 5 % der Stichproben.
  - Bodenbefehl (13:10:14): `queryMs` 60, `publishMs` 33, `encodeMs` 5, `preMs` 4,5. Grabenbefehl (13:11:05): `queryMs` 66, `encodeMs` 34, `publishMs` 102. `publishMs` enthält bei Nicht-Gräbern deren Abfrage mit.
  - Stichproben aller 15 Lücken: `mma_query` 38 %, nativ `0x119F90` (Vanilla) 11 %, Live-Prüfungen (`TryGetTraversalEdge`, `HasCompatibleNativeHeight`, `FastLiveEdge`, `TryAuditFallbackPathCore`) etwa 10 %, `IsWalkableBuildingApproachEndpoint` 2–3 %.
  - Eine Abfrage kostet etwa 23 µs. Ursache ist das lokale exakte Feld je Einheit (`local.solve` über die Box aus Anker und Platz ± 8); bei breiten Formationen deckt diese Box Tausende Kacheln ab.
  - `routeReuses` greift: genau eine Kernabfrage je Einheit. `unreachedMembers=0` bei allen menschlichen Befehlen.
  - In einem Fenster (13:10:39) meldeten 5364 Abfragen „über der Grenze“ (alle 16 Zusatzanker belegt). Etwa 2700 Einheiten eines Befehls blieben dadurch bei Vanilla.
- KI (`test_stresstest4.sav`, etwa 9 Minuten):
  - 36 Lücken über 100 ms, davon 32 bei 100–150 ms und die größte bei 264 ms. Vorher waren es Lücken von 350–1050 ms sowie ein Ausreißer von 4,5 s. Kein Fast-Feld (`anchorSkips` 4522, `maxSynchronousMs` 0,1).
  - Die Stichproben der Lücken verteilen sich gemischt: GC und Warten (ntdll, mono) etwa 50 %, Cursor-Graph-Neubau nach Gebäudeänderungen (BFQoL `CursorRegionGraph..ctor`, MoatMove `DirtyCursorBuilding`) etwa 20 %, Vanillas BFS (`DA5EF`) 7 %, `mma_step` 7 %.
  - 84 % der Mitglieder von KI-Gruppenbewegungen plant Vanilla nicht neu (63540 von 75896), vor allem Einheiten, die schon am Sammelpunkt stehen. Ihre Felder (`preMs` 3,8 s) sind überwiegend umsonst gerechnet.
  - `AnchorTick` kostete 37 s in etwa 9 Minuten (etwa 7 %):
    - Refresh 11 s ohne einen einzigen geänderten Knoten. `EnsureCursorTopology`/`RefreshCursorPortals` liest dabei je Tick und Spieler 200 Portale.
    - Resweep 19 s: 166557 Resweep-Anforderungen bei nur 11 Zugangsänderungen. `MarkFastTopologyAll` (aus `InvalidateMovementSearchData` und aus jeder fehlgeschlagenen Live-Prüfung) setzt den Durchlauf jedes Mal auf 0 zurück. Er wird deshalb nie fertig und läuft dauerhaft mit 1024 Knoten je Tick.
  - Abgelehnt wurden 1038 Routen in der Live-Prüfung und 3658 bei der Veröffentlichung. Wahrscheinliche Ursache der Prüfablehnungen sind veraltete Maskenteile hinter dem nie fertigen Resweep. Gründe für die Veröffentlichungsablehnungen sind nicht erfasst.
  - Zusatzbefund: `MarkFastTopologyTile` hat keinen Aufrufer. Lokale Geländeänderungen erreichen MoatMove nur über `InvalidateFastMoatData()` ohne Kachel (BFQoL: `InvalidateMovementSearchData` bei jeder Kachel-, Graben- und Maskenänderung). Für die Anker-Masken bedeutet das stets einen vollen Resweep. Außerdem legte `AnchorTick` nach jedem `cursorTopologies.Clear()` (Maskenneubau `DAA50`) für alle acht Spieler neue Cursor-Topologien mit je drei großen Graphen an; das sind die `CursorRegionGraph..ctor`-Stichproben.

**Korrekturen danach (29.10.2026, nicht im Spiel geprüft):**
- Resweep:
  - Eine neue Anforderung verlängert die laufende Runde ab dem aktuellen Stand auf eine volle Karte (`anchorSweepLeft`), statt bei Knoten 0 neu zu beginnen. Jeder Knoten wird so spätestens nach einer vollen Runde neu gelesen; bei 1024 Knoten je Tick sind das auf 800×800 etwa 625 Ticks.
  - Eine fehlgeschlagene Live-Prüfung einer Anker-Route liest nur noch das 5×5-Umfeld des veralteten Knotens neu (`MarkAnchorArea`), statt alle Masken neu abzugleichen. Fasts eigene Caches verhalten sich unverändert.
  - Offen: Kachelgenaue Meldungen bräuchten eine Erweiterung des BFQoL-Vertrags (`InvalidateFastMoatData` mit Kachel). Das ist erst nach Abschluss und in einer Kopie vorgesehen.
- Tore: Die Anker-Masken führen eine eigene Liste gesperrter Torgebäude je Spieler (`RefreshAnchorBlocked`, gleiche Regel wie `RefreshCursorPortals`). Sie wird nur bei einer Änderung des Zugangs-Fingerabdrucks neu gelesen. `AnchorTick` und die Anker-Masken legen keine Cursor-Topologien mehr an. Die Live-Prüfung von Anker-Routen nutzt dieselbe Liste (`TryFinishFastEncoded(..., anchor: true)`).
- Felder bei Bedarf: Das Pre der Gruppenbewegung erfasst nur Starts und Profil (`AnchorTaskSpec`). Das Feld wird bei der ersten Abfrage eines Mitglieds berechnet (`ResolveAnchorTask`). Die Reihenfolge der Abfragen ist Vanillas deterministische Einheitenreihenfolge, daher rechnen alle Peers an derselben Stelle. Neue Zähler: `lazyTasks`, `unusedMoves`.
- Veröffentlichung:
  - Ablehnungen werden nach Art und Prüfdetail gezählt (`publishReasons`, Arten `qualified`, `ground`, `reconstruction`, `nonDigger`).
  - Nicht-Gräber ohne Kernantwort zählen nicht mehr als Ablehnung.
  - Die Gebäudezugangs-Abfrage der Veröffentlichungsprüfung läuft nur noch für die letzte Kante. BFQoLs `TryGetTraversalEdge` wertet den Parameter derzeit gar nicht aus.
- Kern, Vertrag `0x04000005`:
  - Das lokale Feld je Abfrage nutzt eine zielgerichtete exakte Suche (A* mit Chebyshev-Schranke zum näheren Ziel, Abbruch, sobald Anker und Eintrittspunkt feststehen). Der Abstieg darf berührte Knoten nutzen; das bleibt exakt, siehe Kommentar in `Field::solveTowards`.
  - Box-Test und Heuristik rechnen mit mitgeführten Koordinaten statt mit Divisionen. Abfragepuffer werden wiederverwendet.
  - Bis zu 48 statt 16 Zusatzanker je Aufgabe.
  - Neue Zähler, warum ein Anker eine Abfrage nicht beantwortet: `anchorMiss(noStart,noEntry,unreached,tooFar)`.
  - GroupPathProbe: Kernverträge bestehen (1475 unabhängige Vergleiche).

**Zweiter Sammellauf (29.10.2026, 14:09–14:14, Log `.local/ingame-breakdown-2.log`):**
- Editor, 1650 Gräber und 900 Reiter (2550 Mitglieder), 8 Befehle:
  - Stillstand je Bodenbefehl 127–160 ms; ein Befehl blieb unter der 100-ms-Schwelle. Vorher waren es 133–148 ms bei 2400 Mitgliedern.
  - Stillstand je Grabenbefehl 168–181 ms, vorher 215–241 ms.
  - `moveMs`: Boden 78–97 ms (vorher 98–110), Graben 131–143 ms (vorher 174–200).
  - Kernabfrage etwa 14 µs je Einheit (vorher etwa 23 µs). `queryMs` liegt bei 35–46 ms je Befehl, der Kern selbst bei 29–40 ms.
  - Bei Grabenbefehlen sind die beiden Live-Prüfungen jetzt der größte Posten: `encodeMs` 26–29 ms und `publishMs` 60–69 ms, letzteres einschließlich der 900 Nicht-Gräber-Abfragen.
  - `IsWalkableBuildingApproachEndpoint` ist aus den Stichproben verschwunden. `unreachedMembers=0`, keine Ablehnungen, `anchorMiss` 0.
- KI (`test_stresstest4.sav`, etwa 3 Minuten bei durchschnittlich 108 Ticks/s, also mehrfacher Spielgeschwindigkeit):
  - 13 Lücken über 100 ms: 9 bei 100–150 ms, 3 bei 150–260 ms und eine bei 1187 ms.
  - Die 1187-ms-Lücke um 14:14:31 folgte auf einen über Spectator erzwungenen KI-Angriff. Ursache sind Vanillas Assassinen-Suche `D9C40` (38 %) und BFQoLs `AssassinPathfindingRuntime` (etwa 35 %), nicht MoatMove; siehe Native-Baseline „Weighted Assassin search cost“.
  - `preMs` 56 ms gesamt (vorher 3,8 s in 9 Minuten). `lazyTasks` 1415 bei 1726 KI-Bewegungen, `unusedMoves` 343.
  - `refreshMs` 85 ms (vorher 11 s in 9 Minuten). Keine Veröffentlichungsablehnung mehr. `encodeRejects` 403; das sind veraltete Maskenteile, die die Prüfung abfängt.
  - Resweep bleibt der größte Dauerposten: 19,9 s in 3 Minuten, etwa 0,85 ms je Tick und etwa 1 µs je Knoten. Auslöser sind 101513 Anforderungen bei 13 Zugangsänderungen. Ursache ist BFQoLs `InvalidateMovementSearchData` ohne Kachelangabe bei jeder Kachel- oder Grabenänderung.
  - Zusatzanker: 1297 angelegt, `subMs` 1,0 s, in einem Fenster 244 ms. `anchorMiss.noStart` 14291 (Starts außerhalb des Hauptfelds, etwa KI hinter Mauern).

**Testlinie `dev/pathfinding` (ab 29.10.2026, Worktree `.work/a/pathfind/001`):**
- Zweck: Pathfinding-Optimierungen über die Mods hinweg (Anker-Modus, BFQoL-Invalidierung, später Assassinen-Suche). BFQoL-Änderungen bleiben auf diesem Branch, bis der Nutzer sie annimmt. `develop` im Hauptordner bleibt für andere Arbeit frei, Verwerfen heißt Branch löschen.
- Kachelgenaue Invalidierung (Commit `89f471d23`, nicht im Spiel geprüft):
  - BFQoL übergibt bei Kachel-, Grabenschreib- und Grabenlöschänderungen die Kachel an `UnitCommandTraversalProvider.InvalidateFastMoatTile(int)`. Ohne Override fällt der Aufruf auf die globale Invalidierung zurück.
  - Verschachtelte Einheitenbewegungen invalidieren keine Gelände-Caches mehr (`InvalidateMovementSearchData(terrain: false)`).
  - MoatMove liest dann nur das ±2-Umfeld neu (`MarkFastTopologyTile`), für Fast-Caches und Anker-Masken. Ein voller Resweep bleibt für Zugangsänderungen, den Maskenneubau `DAA50` und Kartenstarts.
  - Tests: BFQoL-Dispatch-Harness (Kachelmeldung bei deaktivierter Policy), MoatMove-Anker-Harness (Kachelmeldung macht die Aufgabe veraltet ohne Resweep, verschachtelte Bewegung ohne Resweep).
- Weitere Offline-Optimierungen (29.10.2026, nicht im Spiel geprüft):
  - Eine Prüfung statt zwei: Routen, die die Live-Prüfung des Ankers in diesem Tick bestanden haben (`anchorAuditedRoutes`, Objektidentität), überspringen bei der Veröffentlichung die zweite Kantenprüfung je Schritt (`TryAuditFallbackPath(..., edgesAudited)`). Grund: `FriendlyOnly`/`GroundOnly` lehnen jede Kante ab, die `AllowEnemyForDiagnostic` ablehnt (Feindgraben, Ecken, Tor-Richtlinie, Höhe, Grabenrand). Puffer-, Start-, Identitäts-, Eigentümer- und Endpunktprüfung bleiben. `GetReusableQualifiedRoute` verlangt denselben Tick, dieselbe Platzierungsrevision, denselben Start, dasselbe Ziel und dieselbe Einheit. Neuer Zähler `auditSkips`. Tests: Markierung nur für das geprüfte Objekt und nur im selben Tick; die Eigentümerprüfung lehnt auch mit `edgesAudited` eine Feindquerung ab.
  - Zusatzanker nur mit Starts derselben Klasse: Bei erschöpftem Hauptfeld zielt ein Zusatzanker nur auf Starts derselben Hauptfeld-Erreichbarkeit wie der abfragende Start. Antworten bleiben exakt. GroupPathProbe-Vertrag `core-split-group`: Der alte Stand kommt auf 3646 statt höchstens 3540 Knoten (Gegenprobe), der neue besteht.
  - Assassinen-Analyse mit Plan: `BugfixesAndQoL/Findings/ASSASSIN_PATHFINDING_PLAN.md`.

**Dritter Sammellauf (29.10.2026, 15:19–15:24, Log `Testmods/MoatMove/.local/ingame-breakdown-3.log` in der Kopie; Spiel lief beim Sichern noch):**
- Editor (2350 Mitglieder):
  - Grabenbefehle: Stillstand 141–169 ms (vorher 168–181), `moveMs` 104–132 (vorher 131–143), `publishMs` 36–58 (vorher 60–69).
  - `auditSkips` 2350 je Befehl, also jede Route nur einmal geprüft.
  - Bodenbefehl: 154 ms Stillstand bei 95 ms `moveMs`.
- KI (`test_stresstest4.sav`, etwa 4 Minuten bei etwa 119 Ticks/s):
  - Rückschritt: 7 Lücken über 500 ms, die größte 2233 ms. `FastTraversalCache.Mark` hatte 33 % der Lücken-Stichproben, in der größten Lücke 72 %. Ursache: Die kachelgenaue Meldung markiert je Kachelereignis 25 Knoten in allen Caches, und `SortedSet.Add` legt dabei je Eintrag Speicher an. KI-Bau, Graben und brennende Gebäude erzeugen Bursts solcher Ereignisse.
  - Resweep-Anforderungen sanken von 101513 auf 34. Der Resweep lief trotzdem fast durchgehend (25 s, 23,4 Mio. Knoten), weil jede der 13 Zugangsänderungen alle acht Masken voll neu abgleicht.
  - `refreshMs` 3,6 s bei 130785 geänderten Knoten.
  - Vanillas BFS `DA5EF` nur 0,9 % der Sim-Zeit; Anker-Kern 0,5 %. Die heißesten nativen Funktionen sind allgemeine Simulation (`0x18C160` 14 %, Tile-Renderer `0x41D60` 9 %, Einheitenbewegung `0x182B00` 8 %, `0xF5A40` 6 %).
- Korrekturen danach (Commit `ee35a37f4`, nicht im Spiel geprüft):
  - `FastTraversalCache` markiert mit Merkerfeld und Liste, sortiert einmal je Refresh. Reihenfolge und Ergebnis bleiben gleich; Test: 1000 Markierungen eines Knotens führen zu einem Neulesen.
  - Zugangsänderungen lesen nur die Grundflächen (±2) von Torgebäuden mit geändertem Portaleintrag. Bündnisse, Portalzahl oder eine unbekannte Grundfläche lösen weiterhin einen vollen Resweep aus. Neuer Zähler `gateRefreshes`.
  - Beim Neulesen wird `GroundOnly` nur noch nahe fertigen Gräben eigens geprüft (Endpunkte oder Diagonalecken). Sonst entscheidet `TryGetTraversalEdge` beide Regeln gleich. Gleichheitstest über den Graben-Korridor; die Suite erkennt eine falsche Abkürzung.

**Gebündelte Abfragen und gemeinsame Grundmaske (29.10.2026, Commits `f791f6f69`/`9cebbaa4c`, installiert 15:50, nicht im Spiel geprüft):**
- Kern, Vertrag `0x04000006`, `mma_query_batch`:
  - Ein paralleler Durchgang über die vorhandenen Anker, je Worker mit eigenem Arbeitsspeicher (lokales Feld, Pfadpuffer, Zähler); die Aufgabe wird dabei nur gelesen.
  - Abfragen, die einen neuen Zusatzanker bräuchten, gelten als zurückgestellt und laufen danach in Aufrufreihenfolge mit den gewohnten Regeln. Die Antworten sind daher identisch mit Einzelabfragen in derselben Reihenfolge, unabhängig von der Thread-Zahl.
  - GroupPathProbe-Verträge: 24 Zufallslasten, entfernte Plätze und getrennte Gruppe, jeweils mit 1 und 4 Threads gegen Einzelabfragen (Status, Kosten, Wegbytes).
  - Last (8 Threads, `results/core-20261029-143906-221` in der Kopie), Abfragezeit einzeln → gebündelt:
    - `moat-group-mix-10000` 78 → 22 ms, `worker-trips-10000` 52 → 18 ms, `converging-groups-10000` 118 → 75 ms.
    - `group-mix-10000` 114 → 65 ms, darin 33 ms Zusatzanker in der geordneten Phase.
    - `group-1024` 12 → 12 ms: Hier fällt die einmalige Anlage der Worker-Speicher an (etwa 8 MiB je Worker, im Spiel einmal je Prozess).
- BFQoL: Formationen melden ihre Einheitenziele im Pre von `11B520` an den Traversal-Provider (`UnitCommandTraversalProvider.ObserveFormationDestinations`, Standard: keine Aktion). Test in `FormationMovementEligibilityTests`: Die Meldung kommt genau einmal mit dem Platz jeder zugelassenen Einheit.
- MoatMove:
  - Die Meldung hängt am Frame derselben Gruppenbewegung, unabhängig von der Reihenfolge der Event-Handler.
  - Bei der ersten Abfrage (`ResolveAnchorTask`) werden alle Mitglieder der Aufgabenklasse mit Ziel gebündelt beantwortet. Die Einzelabfrage findet ihre Antwort über (Aufgabe, native Startkachel, Ziel).
  - Zwei wiederverwendete Puffersätze (Graben-/Bodenaufgabe) mit Aufgabenkennung, damit verschachtelte Bewegungen keine fremden Antworten liefern.
  - Zähler: `batch(calls,units,hits,ms)`. Ohne Formation (Vanillas eigene Verteilung, KI) bleibt es bei Einzelabfragen.
  - Test: Batch-Antwort bytegleich zur Einzelabfrage; eine Meldung für einen anderen Stamm hängt sich nicht an.
- Gemeinsame Grundmaske:
  - Spielermasken teilen die Wörter spielerunabhängiger Knoten (`FastSharedWords`, `AnchorPlayerNeutral`): kein fertiger Graben am Knoten oder an seinen Nachbarn, kein für irgendeinen Spieler gesperrtes Torgebäude darauf, gleiche Richtungsmaske der Torrichtlinie für alle Spieler mit Maske.
  - Markierungen machen geteilte Wörter ungültig; der Resweep liest immer live und teilt das Ergebnis.
  - Test: zwei Spieler mit unterschiedlich gesperrtem Tor im Korridor, geteilte Wörter gleich den ungeteilten Spielermasken; die Gegenprobe „alles neutral“ schlägt bei Kachel 59,10 fehl. Zähler `sharedReuses`.
 Die Wegqualität gegen Precise ist identisch mit der Messung vom 28.10. Abfragezeit je Einheit bei 8 Threads (`results/core-20261029-124523-008` gegen `core-20261028-172304-117`): `group-1024` 16,8 → 10,6 µs, `group-mix-10000` 12,9 → 7,5 µs, `converging-groups-10000` 12,2 → 7,1 µs, `scattered-group-mix-10000` 12,9 → 8,6 µs, `worker-trips-10000` 10,3 → 7,2 µs, `moat-group-mix-10000` 14,2 → 10,8 µs.

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
