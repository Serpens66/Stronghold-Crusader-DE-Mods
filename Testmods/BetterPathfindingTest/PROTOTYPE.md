# BetterPathfindingTest 0.3.0

Stand: 27.10.2026. Lesender, experimenteller Navigationsdienst für normale Bodenwege.
Der Nutzer hat nach dem negativen CCH-Tor ausdrücklich entschieden, zuerst das
Grundsystem und seine API fertigzustellen und den Suchkern später auszutauschen.
Die Performance-Abnahme bleibt offen. Keine Spielbefehle, Bewegungseinbindung,
Pfadpuffer-Schreibzugriffe, Traversal-Anbieter oder eigenen nativen Spielhooks.

## Dauerhafter Dienst

- Ein statisch verwurzelter Dienst und ein Hintergrundworker mit niedriger Priorität
  leben über Kartenwechsel hinweg. Pro Missionssitzung wird genau einmal begonnen;
  neue Karte, Save und Editor verwenden den bestehenden Missionslifecycle.
- Der lokale Spieler wird automatisch vorbereitet. Weitere Perspektiven werden über
  die API angefordert. Nur exakt gleiche gerichtete Graphen teilen einen Backend-
  Kontext; unterschiedliche Rechte bleiben getrennt.
- Main-Thread-Erfassung unter der validierten, nicht blockierend betretenen Engine-
  Sperre. Auf dem Worker liegen ausschließlich eigene Arrays und unveränderliche
  Tor-Policy-Snapshots. Dateiausgabe und Graphaufbau erfolgen außerhalb der Sperre.
- Alle 500 ms wird eine kohärente Aufnahme angefordert. Öffentliche Gebäude-,
  Vegetations- und Editorereignisse liefern zusätzliche Hinweise; sie ersetzen kein
  vollständiges Änderungsjournal. Hinweise führen frühestens nach 250 ms zur Aufnahme.
- Neuere wartende Aufnahmen werden zusammengefasst. `Ready` wird erst veröffentlicht,
  wenn der Kontext den neuesten bereits angebotenen Snapshot verarbeitet hat.
  Unveränderte Navigationsdaten behalten Generation, Index und Zielfelder. Die Tick-
  nummer allein invalidiert nichts.
- Änderungen führen zu `Updating`, neuer Generation und vollständiger Ersetzung des
  betroffenen nativen Kontexts. Dadurch werden auch neue Abkürzungen außerhalb alter
  Wege erfasst. **Inkrementelle Reparatur ist noch nicht implementiert.**
- Initialisierung höchstens 300 s je angefordertem Kontext; einzelne Arbeitsaufträge
  einschließlich Warteschlange höchstens 120 s. Fehler und Budgets liefern niemals
  vermeintliche Unerreichbarkeit. Belegte Sperren werden ohne blockierendes Warten
  erneut versucht; eine einzelne überfällige Erfassung endet nach 30 s unvollständig.

`Ready` bedeutet ausschließlich: für diese Spielerperspektive und erfasste Generation
vollständig vorbereitet. Es bestätigt weder aktuelle Bewegungssicherheit zwischen
Aufnahmen noch 100-ms-Latenz, Faktor 100 oder flüssige Simulation von 10.000 Einheiten.
`PerformanceAccepted` bleibt deshalb ausdrücklich `false`.

## Öffentliche experimentelle API

Namespace `BetterPathfindingTest.Api`, Vertragsversion 1, direkt aus der Testmod-
Assembly referenzierbar. Vollständiger Ablauf und Besitzregeln: [API.md](API.md).

Bereitschaft, Sitzungs-/Generationskontext, asynchrone Batches bis 10.000 Paare,
stabile Anfrage-IDs, Kosten, Schrittzahl, Routenhandles und erste acht Richtungen
sind verfügbar. Weitere Schritte und vollständige Wege werden gebündelt abgeholt.
Abbruch, Freigabe und veraltete Handles sind ausdrücklich definiert. Öffentliche
Typen enthalten keine Unity-Objekte oder nativen Pointer.

Es gelten einheitliche positive Schrittkosten auf dem gerichteten trockenen Teil des
bereits auditierten Precise-Snapshotgraphen. Freundliche Grabentraversierung,
Assassinen, Profil-/Ressourcengrenzen und terminale Sonderübergänge werden über diese
API noch nicht angeboten. Die bisherigen Offline-Spezialtests bleiben erhalten.

## Austauschbarer Suchkern und Speicher

`Service/ResidentBackend.cs` trennt Dienst und Suchkern. Aktuell wird der vorhandene
exakte C++-Flächen-/A*- und Rückwärtsfeldkern verwendet. Er verdichtet geeignete
Rechtecke, hält Sonderkanten explizit und teilt Zielfelder. Seine Grundlagen und
Grenzen stehen in [native/SOURCES.md](native/SOURCES.md). Der CCH-Kandidat bleibt
**ausschließlich offline**; es gibt keinen stillen Runtime-Fallback auf CCH.

Die API liefert kompakte Präfixe, der derzeitige Backend-Adapter berechnet intern
jedoch bereits vollständige Wege. Nachträgliche Wegabholung misst zusätzliche Kopien,
keine erst dann erfolgende vollständige Entfaltung. Auch dies bleibt Arbeit für den
späteren Suchkernaustausch.

Speicherbudget: Minimum aus 1 GiB und einem Viertel des bei Initialisierung verfügbaren
physischen RAM, ermittelt über `GlobalMemoryStatusEx`. Bei fehlender Ermittlung gilt
256 MiB; unter 256 MiB startet der Dienst nicht. Konservative Quell-/Clientpools plus
Backend-Puffer und behaltene Wegpuffer werden gemeinsam bilanziert. Ergebnisse werden
mit `Release` freigegeben; nach Kartenwechsel/Fehler werden eigene Daten bereinigt.
Vom Aufrufer nach Freigabe weiter behaltene Ergebnisobjekte sowie Spiel-/Modheap sind
nicht dem Dienst zurechenbar. Ein dauerhaft null meldender Allokationszähler gilt als
nicht verfügbar, nicht als Nachweis fehlender Allokationen.

Native Sucharbeit wird in Operationspakete zerlegt. Das angestrebte 2-ms-Paket ist
keine harte Garantie: der aktuelle Replay maß maximal 2,66 ms. Native Kontextanlage
und -ersetzung sind noch zusammenhängende Aufrufe; die gesamte Vorbereitungszeit
wird separat gemessen. Die API ruft keine Suche auf dem Spielthread auf.

## Bauen und nächster Ingame-Test

    build.bat /nopause /source:working /devdiag

`/noinstall` unterdrückt die Installation. Ausschließlich das eigene Testpaket wird
gebaut/installiert; installierte Extender-/APIShared-Abhängigkeiten werden davor und
danach gehasht und weder gebaut noch ersetzt. Ein laufendes Spiel verhindert die
Installation. Diagnosepaket: `BetterPathfindingTest.dll`, `.pdb`,
`BetterPathfindingNative.dll` (privates x64-ABI 4), `info.json`, Version 0.3.0.

Ein normaler Build enthält die API, meldet jedoch ausdrücklich `Unsupported` und
enthält weder Dienst noch automatische Erfassung oder native Diagnose-DLL. Die
Binärprüfung kompiliert einen getrennten Framework-Verbraucher dagegen. Diagnose-
Code wird ausschließlich mit `/devdiag` eingebaut. Keine Konfiguration wird umgestellt.

Zum Testen genügt ein regulärer Karten-/Save-/Editorstart. Erst einige Sekunden ruhig
laufen lassen, dann normale Bau-/Abriss- oder Editoränderungen vornehmen und noch
mindestens 15 Sekunden laufen lassen. Das Spiel wird vom Build nicht gestartet.
Ein späterer Kartenwechsel soll erneut initialisieren. Sichtbare Bewegung bleibt
vollständig vom bestehenden Spielsystem bestimmt.

Unter `BepInEx/cache/BetterPathfindingTest_Serp/<UTC>-s<ID>/` entstehen:

- `snapshot-1.raw.gz` und Metadaten früh im Lauf; eine zweite Rohaufnahme frühestens
  zehn Sekunden später. Der Dienst erfasst danach weiterhin, speichert aber nicht
  jede halbe Sekunde die ganze Karte.
- `resident.json/.md`: letzter Zustand, Alter, Speicher, Zähler und Zeitmessungen;
  `resident-ready.json` bewahrt einen erreichten Bereitschaftsnachweis auch nach Ende.
- `api-requests.json`, `api-probe.bpf.gz`, `api-probe.json`: reproduzierbare 32er-Probe
  über denselben API-Auftragsweg, mit passendem eigenem Graphen und Anfrage-IDs.
- `unsupported-*.json/.md`: nicht unterstützter Engine-/Layoutvertrag.

Ein Schreibfehler stoppt keine gültige Navigation; fehlende Nachweisdateien bleiben
fehlende Abdeckung. Fortschrittsmarker werden während langer Workerarbeit höchstens
alle fünf Sekunden ausgegeben. Frühere paarweise 120-s-Runtime-Großbenchmarks wurden
durch den dauerhaften Dienst ersetzt; aktuelle Skalierungsmessungen laufen offline.

## Prüfungen und Ergebnisse

Der Build führt scoped Abhängigkeits-, Lifecycle-, Diagnose- und Quellprüfungen,
getrennte Framework-Prozesse mit Datei-/Byte-Ladung der echten Spielassembly,
Offline-Such-/Replay-/Aktualisierungsverträge und einen unabhängig kompilierten
API-Verbraucher auf einem echten Hintergrundthread aus. Keine native Spielmethode
wird durch diese Tests aufgerufen. Der vorhandene unveränderte Precise-Kern bleibt
nur Offline-Oracle, keine harte Runtime-Abhängigkeit.

    dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File tests/Test-ServiceApi.ps1
    dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release -- --resident-bench <erstes.raw.gz> <zweites.raw.gz>

Datierte Ergebnisse, negative Befunde und historische Nachweise stehen vollständig
in [RESULTS.md](RESULTS.md), aktuelle Replay-Dateien unter
[results/resident-20261027-201635-923/](results/resident-20261027-201635-923/).

Der CCH-Versuch vom 27.10.2026 bleibt in `native/CchProbe.cpp`, seinen Offline-Tests
und [results/cch-20261027-191740-311/](results/cch-20261027-191740-311/) erhalten.
12.408 unabhängige Vertragsvergleiche bestanden; die vielen unterschiedlichen Ziele
verfehlten das Ziel deutlich. Der damalige Stopp vor Dienst/API beschreibt den
Zeitpunkt **vor** der anschließenden Nutzerentscheidung, diese Teile zuerst zu bauen.
Quellenprüfung: [native/SOURCES.md](native/SOURCES.md). Eigene lesende Spielverträge
und Updatevorgehen: [UpdateToNewDLL.md](UpdateToNewDLL.md).

Weiter offen: Unity-/Mono-Abnahme von 0.3.0, Dauerlast während realer Belagerung,
zusätzliche Main-Thread-/Framekosten der periodischen Erfassung, optimaler Suchkern
für sehr viele verschiedene Ziele, inkrementelle Reparatur und echte Bewegung.
Verbraucher werden weiterhin nicht angeschlossen.

Wenn der Editor keinen lokalen Simulationsspieler meldet, wird Perspektive 1
automatisch als Diagnosekontext vorbereitet; der Status nennt diese Spieler-ID.
Die rohe Metadatei bewahrt die tatsächlich gemeldete lokale ID.

Auslieferungsstand am Ende dieses Arbeitsgangs (27.10.2026): **0.3.0 gebaut und
geprüft, auf anschließenden Nutzerwunsch nicht installiert**. APIShared wird parallel
weiterentwickelt und änderte sich nach den Builds; vor einer späteren Installation
ist der aktuelle SDK-Abgleich zu erneuern. Der historische Installationsnachweis
für 0.2.0 bestätigt keine aktuelle Installation; der damalige Zielordner war beim
abschließenden Leseabgleich nicht vorhanden. Für jetzt ist kein Ingame-Test nötig.
Übergabe und offene Punkte: [RESULTS.md](RESULTS.md).
