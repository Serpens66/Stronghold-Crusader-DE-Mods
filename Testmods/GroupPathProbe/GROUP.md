# GroupPathProbe

Rein lesender Offline-Testmod (kein BepInEx-Plugin, keine Installation, keine Hooks). Er prüft, ob **Gruppenbefehle** deutlich günstiger geplant werden können als mit dem aktuellen Precise-Kern. Hintergrund und bisherige Versuche stehen in [BetterPathfinding.md](../../BetterPathfinding.md).

Bewusst getrennt von `BetterPathfindingTest`, an dem parallel gearbeitet wird. Kartenmodell, Referenzsuche und Snapshot-Leser liegen als **eingefrorene Kopien** unter `src/Core/` (Quelle: `Testmods/BetterPathfindingTest` bei Commit `df29be62e`, nur Namespace geändert). Der Precise-Vergleich bindet die aktuellen, unveränderten BugfixesAndQoL-Quellen offline ein; deren Hashes stehen je Lauf in `host.json`.

## Varianten

- **A – exakte Spuren:** Bis zu 64 Formationsplätze einer Gruppe werden in einer rückwärts laufenden Dial-Suche gemeinsam berechnet. Je Knoten trägt ein `uint64` die Plätze, deren Entfernung dort feststeht; jeder Platz bekommt dadurch sein eigenes exaktes Kostenfeld. Jede Einheit steigt deterministisch in ihrem Feld ab. Übersteigt die Entfernungsdifferenz zweier Plätze 255 Kosteneinheiten, wird der Block geteilt. Ergebnis: optimale Kosten je Einheit.
- **B – Anker + exakte Endstücke:**
  1. Je unterschiedlichem Anker und Kostenprofil ein exaktes Rückwärtsfeld (zu allen Starts) und ein früh stoppendes Vorwärtsfeld (zu allen Plätzen). Gruppen mit gleichem Anker und Profil teilen sie. Die Felder entstehen parallel auf Worker-Threads, jeder Thread mit eigenem Arbeitsbereich. Jede Einheit kennt damit ihren exakten Weg Start → Anker → Platz.
  2. **Ferne Plätze:** Liegen die Wegkosten vom Anker zum Platz über `Bodenkosten × (2 × Chebyshev-Abstand + 8)`, etwa hinter einer Mauer, bilden diese Plätze einen Unterplan mit eigenem Anker: dem fernen Platz mit der kleinsten Chebyshev-Summe zu den übrigen. Das wiederholt sich bis zu drei Ebenen tief. Die Schwelle ist eine Heuristik des Vorabtests.
  3. Jede Einheit folgt dem Feld ihres Plans bis zum Eintritt in das Rechteck um dessen Plätze (Rand einstellbar, Standard 2). Ab dort folgt ein exaktes, auf dieses Rechteck beschränktes Endstück zum eigenen Platz (Spuren). Scheitert das, wird ein um 16 Kacheln vergrößertes Rechteck versucht.
  4. Ist der Weg über einen Anker der Planfolge günstiger, oder gibt es kein Endstück, erhält die Einheit den günstigsten Ankerweg. Nur was auch so nicht erreichbar ist, rechnet Variante A.

- **B je Einheit (Modus 2, Form für MoatMove):** Ankerfelder vorab und parallel. Danach wird jede Einheit einzeln und in fester Reihenfolge beantwortet:
  - Abstieg im Ankerfeld bis zum Rechteck aus Anker und eigenem Platz plus Rand.
  - Darin eine kleine exakte Einspursuche, verglichen mit dem Weg über den Anker.
  - Ist der Platz im Rechteck vom Anker zu weit entfernt, entsteht einmal ein Feld an diesem Platz, das spätere Einheiten derselben Feldaufgabe mitnutzen.

  Die Wege hängen nicht von der Thread-Anzahl ab: Feldaufgaben schreiben nur ihre eigenen Pläne, alles Übrige läuft seriell in fester Reihenfolge. Die Kosten sind nicht garantiert optimal; die Abweichung wird gemessen.

Die Nachbarschaft liegt als vorab berechnete gerichtete Bitmaske vor (ein- und ausgehend, getrennt nach Graben), damit die Suchschleifen weder dividieren noch Grenzen prüfen. Die Präfixpuffer der Phase 1 werden nicht über den Speicherzähler erfasst.

Beide geben Wege über 2.000 Schritte nicht als Route aus (`Inconclusive`). Nicht abgedeckt: Grabenpflicht, Profilgrenzen, terminale Ausnahmen und Strukturfilter. Die 40-Tick-Regel wird nicht angewendet; als Graben-Entscheidung zählt, ob der Weg Grabenschritte enthält.

## Lasten

Diagnostische Befehle, keine aufgezeichneten Spielbefehle. Starts sind ein kompakter Cluster um echte Einheitenpositionen des Mitschnitts, die Ziele eine kompakte Formation um einen fernen Anker (mindestens 200 Kacheln Chebyshev-Abstand). Alle Anfragen sind per Erreichbarkeit positiv.

- `group-16/64/256/1024`: je eine Gruppe, Profil 1/7.
- `group-mix-10000`: 40 Gruppen à 250, Profile abwechselnd 1/7 und 3/9.
- `converging-groups-10000`: 40 Gruppen à 250 aus verschiedenen Startclustern zu nur 4 Zielen (je 10 Gruppen mit gleichem Anker und daher gleichen Plätzen), Profile abwechselnd.
- `scattered-group-mix-10000`: 40 Gruppen à 250, deren Starts zufällig über die ganze erreichbare Karte verteilt sind (≥ 100 Kacheln vom Anker).
- `worker-trips-10000`: 8 feste Ziele mit je 1.250 verstreuten Starts.
- `moat-group-mix-10000`: synthetische 800er-Karte, Grabenstreifen mit Mauer und Bodenlücke im Süden, 40 Gruppen à 250.

## Aufruf

Vom Workspace-Root:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Testmods/GroupPathProbe/native/Build-GroupProbe.ps1
    dotnet run --project Testmods/GroupPathProbe/tests/GroupPathProbe.Tests.csproj -c Release -- --contracts
    dotnet run --project Testmods/GroupPathProbe/tests/GroupPathProbe.Tests.csproj -c Release -- --capture Testmods/GroupPathProbe/input/capture-20261027-182413-691-s1.raw.gz

`--capture` misst beide B-Varianten mit Rand 2 bei 1, 4 und 8 Threads sowie mit Rand 8 bei 4 Threads. `--with-exact` misst zusätzlich Variante A, `--only name,name` nur die genannten Lasten (gleiche Eingaben). Je Last und Rand stehen die acht größten Umwege mit Graphabständen zum Anker in `results.json`. Der Lauf dauert mehrere Minuten, weil Precise jede Anfrage einzeln rechnet. Ergebnisse liegen in `results/group-<UTC>/` (`results.json`, Eingaben als `.bpf.gz`, Build- und Hostnachweis). Exitcode 0 = alles geprüft, 1 = Vertrags- oder Vergleichsfehler, 2 = Last nicht bildbar.

Die Eingabe `input/capture-20261027-182413-691-s1.raw.gz` ist eine Kopie des Mitschnitts aus `BetterPathfindingTest/results/cch-20261027-191740-311/input.raw.gz` (0.2.0-Lauf `20261027-182413-691-s1`, Snapshot 1).

## Prüfumfang

- Verträge: zufällige gerichtete Gitter mit Grabenkanten und drei Kostenprofilen (einschließlich zweier Gruppen mit geteiltem Feld), Blockteilung bei großer Spreizung, Weg über 2.000 Schritte, gemischte Graben-Entscheidungen in einer Gruppe, Platz hinter einer Mauer ohne Vollsuche, ferne Plätze mit eigenem Unterplan (höchstens 2 Kosteneinheiten über dem Optimum; bei B je Einheit genau ein gemeinsames Feld). Beide B-Varianten durchlaufen alle Verträge. B wird jeweils mit 1 und 4 Threads gerechnet und muss byte-gleich sein. A muss mit der unabhängigen Referenzsuche in Status und Kosten übereinstimmen. B muss gültig sein, den richtigen Status liefern und darf nicht unter dem Optimum liegen.
- Messung: Jeder Weg wird kantenweise geprüft. Bei A muss jede Einheit dieselben Kosten wie Precise haben, zusätzlich je Last drei Fälle gegen die unabhängige Referenz. Bei B werden Mehrkosten und abweichende Graben-Entscheidungen gezählt.

## Ergebnisse

Siehe Abschnitte 18 bis 18.3 in [BetterPathfinding.md](../../BetterPathfinding.md).

- `results/group-20261028-165858-216` und `results/group-20261028-170431-863` (Rand 8, drei Lasten): maßgeblich für B je Einheit.
- `results/group-20261028-161619-849`: maßgeblich für den aktuellen Stand des Gruppen-B (ferne Plätze, Unterpläne, günstigster Ankerweg).
- `results/group-20261028-154338-396`: optimiertes B vor der Ausreißerbegrenzung (Threads, geteilte Felder, Ersatzweg über den Anker).
- `group-20261028-155919-778`, `group-20261028-160517-854`, `group-20261028-160853-215`, `group-20261028-161515-212`: Zwischenläufe der Ausreißeranalyse (Ankerweg-Vergleich ohne Wirkung, Einzelfalldiagnose, einstufiger Unterplan, Teillauf).
- `results/group-20261027-210235-273`: maßgeblich für A und das erste B.
- `group-20261027-205417-681`: Vorlauf vor zwei Korrekturen (8×8-Bündelung bei A, erweitertes Endrechteck und Einspur-Ankerfeld bei B).

Hinweis zum Buildnachweis: Nach den maßgeblichen Läufen wurden einzelne Quellen nur auf CRLF umgestellt. Der jeweils im Ergebnisordner gespeicherte Quellhash gehört zur LF-Fassung mit identischem Inhalt; `.local/native/build.json` beschreibt den aktuellen Neubau.
