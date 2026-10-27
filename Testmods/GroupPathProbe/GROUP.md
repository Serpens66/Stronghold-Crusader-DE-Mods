# GroupPathProbe

Rein lesender Offline-Testmod (kein BepInEx-Plugin, keine Installation, keine Hooks). Er prüft, ob **Gruppenbefehle** deutlich günstiger geplant werden können als mit dem aktuellen Precise-Kern. Hintergrund und bisherige Versuche stehen in [BetterPathfinding.md](../../BetterPathfinding.md).

Bewusst getrennt von `BetterPathfindingTest`, an dem parallel gearbeitet wird. Kartenmodell, Referenzsuche und Snapshot-Leser liegen als **eingefrorene Kopien** unter `src/Core/` (Quelle: `Testmods/BetterPathfindingTest` bei Commit `df29be62e`, nur Namespace geändert). Der Precise-Vergleich bindet die aktuellen, unveränderten BugfixesAndQoL-Quellen offline ein; deren Hashes stehen je Lauf in `host.json`.

## Varianten

- **A – exakte Spuren:** Bis zu 64 Formationsplätze einer Gruppe werden in einer rückwärts laufenden Dial-Suche gemeinsam berechnet. Je Knoten trägt ein `uint64` die Plätze, deren Entfernung dort feststeht; jeder Platz bekommt dadurch sein eigenes exaktes Kostenfeld. Jede Einheit steigt deterministisch in ihrem Feld ab. Übersteigt die Entfernungsdifferenz zweier Plätze 255 Kosteneinheiten, wird der Block geteilt. Ergebnis: optimale Kosten je Einheit.
- **B – Anker + exakte Endstücke:** Ein exaktes Feld zum Gruppenanker (Formationsmitte). Jede Einheit folgt ihm bis zum Eintritt in das Rechteck um die Formationsplätze (Rand 2). Ab dort folgt ein exaktes, auf dieses Rechteck beschränktes Endstück zum eigenen Platz (wieder Spuren). Einheiten ohne solchen Weg erhalten Variante A. Die Kosten sind nicht garantiert optimal; die Abweichung wird gemessen.

Beide geben Wege über 2.000 Schritte nicht als Route aus (`Inconclusive`). Nicht abgedeckt: Grabenpflicht, Profilgrenzen, terminale Ausnahmen und Strukturfilter. Die 40-Tick-Regel wird nicht angewendet; als Graben-Entscheidung zählt, ob der Weg Grabenschritte enthält.

## Lasten

Diagnostische Befehle, keine aufgezeichneten Spielbefehle. Starts sind ein kompakter Cluster um echte Einheitenpositionen des Mitschnitts, die Ziele eine kompakte Formation um einen fernen Anker (mindestens 200 Kacheln Chebyshev-Abstand). Alle Anfragen sind per Erreichbarkeit positiv.

- `group-16/64/256/1024`: je eine Gruppe, Profil 1/7.
- `group-mix-10000`: 40 Gruppen à 250, Profile abwechselnd 1/7 und 3/9.
- `worker-trips-10000`: 8 feste Ziele mit je 1.250 verstreuten Starts.
- `moat-group-mix-10000`: synthetische 800er-Karte, Grabenstreifen mit Mauer und Bodenlücke im Süden, 40 Gruppen à 250.

## Aufruf

Vom Workspace-Root:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Testmods/GroupPathProbe/native/Build-GroupProbe.ps1
    dotnet run --project Testmods/GroupPathProbe/tests/GroupPathProbe.Tests.csproj -c Release -- --contracts
    dotnet run --project Testmods/GroupPathProbe/tests/GroupPathProbe.Tests.csproj -c Release -- --capture Testmods/GroupPathProbe/input/capture-20261027-182413-691-s1.raw.gz

`--capture` dauert mehrere Minuten, weil Precise jede Anfrage einzeln rechnet. Ergebnisse liegen in `results/group-<UTC>/` (`results.json`, Eingaben als `.bpf.gz`, Build- und Hostnachweis). Exitcode 0 = alles geprüft, 1 = Vertrags- oder Vergleichsfehler, 2 = Last nicht bildbar.

Die Eingabe `input/capture-20261027-182413-691-s1.raw.gz` ist eine Kopie des Mitschnitts aus `BetterPathfindingTest/results/cch-20261027-191740-311/input.raw.gz` (0.2.0-Lauf `20261027-182413-691-s1`, Snapshot 1).

## Prüfumfang

- Verträge: zufällige gerichtete Gitter mit Grabenkanten und drei Kostenprofilen, Blockteilung bei großer Spreizung, Weg über 2.000 Schritte, gemischte Graben-Entscheidungen in einer Gruppe. A muss mit der unabhängigen Referenzsuche in Status und Kosten übereinstimmen. B muss gültig sein, den richtigen Status liefern und darf nicht unter dem Optimum liegen.
- Messung: Jeder Weg wird kantenweise geprüft. Bei A muss jede Einheit dieselben Kosten wie Precise haben, zusätzlich je Last drei Fälle gegen die unabhängige Referenz. Bei B werden Mehrkosten und abweichende Graben-Entscheidungen gezählt.

## Ergebnisse

Siehe Abschnitt 18 in [BetterPathfinding.md](../../BetterPathfinding.md). Maßgeblich ist `results/group-20261027-210235-273`. `group-20261027-205417-681` ist der Vorlauf vor zwei Korrekturen (8×8-Bündelung bei A, erweitertes Endrechteck und Einspur-Ankerfeld bei B).

Hinweis zum Buildnachweis: Danach wurden die Quellen nur auf CRLF umgestellt. Der im Ergebnisordner gespeicherte Quellhash gehört zur LF-Fassung mit identischem Inhalt; `.local/native/build.json` beschreibt den aktuellen Neubau.
