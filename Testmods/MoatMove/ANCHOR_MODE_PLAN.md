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

### Phase 3 – Build

- `build.bat` von MoatMove kompiliert die DLL mit MSVC (Muster: `BetterPathfindingTest/native/Build-Native.ps1`), statische CRT, x64.
- Buildnachweis mit Quell- und DLL-Hash; die Paketprüfung umfasst die DLL.
- Die Version wird erst bei fertigem Feature erhöht.

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
