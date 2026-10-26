# BetterPathfindingTest 0.1.0

Eigenständiger, ausschließlich beobachtender Testmod. Keine Spielbefehle, keine
Pfadpuffer-Schreibzugriffe, keine Traversal-Registrierung und keine eigenen nativen
Hooks. Kein Verbraucher wird angeschlossen. `NetworkMode=0` bezeichnet die lokale
Beobachtung; die erhebliche Diagnosearbeit kann den beobachteten Rechner belasten.

## Start und Dateien

Aus diesem Ordner `build.bat /nopause /source:working /devdiag` aufrufen.
`/noinstall` baut und prüft ausschließlich lokal. Der Treiber verwendet den aktuellen
Arbeitsstand und die bereits installierten Abhängigkeiten. Er baut weder APIShared
noch den Script Extender neu und prüft deren installierte Dateien per SHA-256 vor und
nach dem Build. Installiert werden ausschließlich DLL, PDB und info.json des neuen
Pakets `BepInEx/plugins/BetterPathfindingTest_Serp`.

Ein normaler Build enthält keinen Scheduler, Worker oder Capture-Adapter. Die
automatische Messung existiert ausschließlich im mit `/devdiag` erzeugten und als
DEVDIAG markierten Paket. Kein Laufzeitschalter ersetzt diese Compile-Grenze.

Beim nächsten regulären, vollständig abgeschlossenen Kartenstart startet automatisch
genau eine Sitzung. Das gilt für neue Spiele, Save-Laden und beide Editor-Startwege.
Zehn Sekunden nach der ersten Erfassung wird eine zweite Erfassung angefordert.
Ein Kartenwechsel bricht die alte Arbeit logisch ab. Das Spiel wird vom Build nicht
gestartet; Spiel- und Modkonfigurationen werden nicht geändert.

Diagnosen entstehen unter dem Spielverzeichnis in
`BepInEx/cache/BetterPathfindingTest_Serp/<UTC-Zeit>-s<Sitzung>/`:

- `snapshot-N.raw.gz`: versionierte Rohdaten einschließlich Kontext, Hashes und Profilen.
- `snapshot-N-*.bpf.gz`: abgeleiteter gerichteter Graph und vollständige Anfragefolge.
- `snapshot-N.json` und `.md`: Gesamtstatus, Erfassungs-/Aufbauzeiten, Aktualisierung,
  Speicher, fehlende Abdeckung und Zusammenfassung.
- Arbeitslastberichte pro Familie mit erster Abfrage nach Vorbereitung, warmen
  Abfragen, unabhängiger Referenz und Wegabweichungen.
- `unsupported-*.json/.md`: unbekannter DLL-, Assembly-, Lifecycle- oder Policy-Vertrag;
  dieser Zustand ist kein bestandener Vergleich.

Die Logmeldungen verwenden `Shared.DevDiag` mit Millisekunden-Zeitstempeln und nennen
Phase, Laufzeit und Fallzahlen. Es gibt keine Tastenbelegung oder Settings-UI.

## Aufbau und Exaktheit

`src/Core` wird unverändert in den .NET-Offlinetests und im .NET-Framework-Runtime-Mod
kompiliert. Unveränderliche gerichtete Graphen bewahren alle acht Richtungen,
Grabenkennzeichnung, Strukturkennzeichnung und den Spielerzugriff.

Die Hierarchie verwendet 32×32- und 128×128-Bereiche. Grenzknoten sind originale
Kachelkoordinaten, einschließlich diagonaler Grenzübergänge. Es gibt keine Reduktion
auf Portalmitten und keine Beschränkung auf einen heuristisch ausgewählten Korridor.
Innere Distanzzeilen und vollständige Wegnachweise entstehen bei Bedarf. Lokale
Zielfelder erhalten an sämtlichen erreichbaren Grenzknoten die tatsächlichen
globalen Fortsetzungskosten. Ein Feld wird ab 32 gemeinsam nutzbaren Anfragen im
gleichen Startbereich und mit gleichem Ziel/Kostenkontext vorbereitet.

Die skalare Hierarchiesuche prüft zunächst ihren optimalen Kandidaten gegen alle
Anfragebedingungen. Falls nötig, folgt eine ebenfalls eigene exakte Suche mit
nicht dominierten Boden-/Grabenschritt-Kombinationen. Grabenpflicht erhält getrennte
Zustände mit und ohne bisherige Grabenkante. Mehrere Profilgrenzen, Strukturfilter,
anfragebezogene terminale Kanten und Längenlimits werden berücksichtigt. Konkrete
Wege sind auf höchstens 2.000 Schritte beschränkt; reine topologische Erreichbarkeit
wird getrennt abgefragt. Positive ganzzahlige Kosten und stabile Gleichstandsregeln
vermeiden Fließkomma- und Zufallsabhängigkeiten. Diese internen Typen sind keine
zugesicherte öffentliche Mod-API.

Bei Änderungen werden betroffene Bereiche und ihre Nachbarn invalidiert. Alle
global gespeisten Zielfelder werden verworfen: Eine neue Abkürzung kann auch
außerhalb des bisher verwendeten Weges liegen. Lokale Zeilen unveränderter Bereiche
bleiben erhalten. Aktualisierungen werden mit einer neuen Hierarchie und mit einer
unabhängigen Vollsuche verglichen. Kontrollierte private Graphkopien erzwingen eine
Sperrung und eine neue, kürzere Verbindung außerhalb des alten Weges.

Der Speicherwächter führt konservative Reservierungen für Graphen, Cachezeilen,
Zeugen, Felder und Suchzustände. Es gibt keine unbegrenzte LRU-Umgehung des Limits.
256 MiB und 120 Sekunden gelten je automatischem Vergleichslauf über alle Phasen;
Zeit-/Speichererschöpfung ergibt `INCOMPLETE`, niemals `Unreachable`. Der gemessene
verwaltete Heap und tatsächlich verfügbare Thread-Allokationszähler werden zusätzlich
ausgewiesen. Reservierte Puffer sind keine Zusicherung über den gesamten Unity-Heap.

## Erfassung und geprüfte Verträge

Der zentrale APIShared-Missionslifecycle meldet abgeschlossene Initialisierung.
Die statisch verwurzelte `Application.onBeforeRender`-Subscription versucht danach
höchstens einmal je Frame die Erfassung. Damit ist keine laufende Simulation nötig.
Der Main-Thread verwendet `Monitor.TryEnter` auf dem per Reflection gekapselten
privaten `EngineInterface.threadLock`; bei Konkurrenz wird auf einen späteren Frame
verschoben. Ein echter Assembly-Test prüft den Monitor-Abschnitt um `DLL_RunTick`
und nicht blockierenden Zugriff bei einer zweiten besitzenden Test-Threadinstanz.
Er ruft keine native Spielmethode auf.

Unter der Sperre werden Layer und rohe Merkmale kopiert. Graphaufbau, abgeleitete
Strukturhöhen, Policy-Materialisierung, Suchen, Vergleiche und Datei-I/O laufen auf
einem einzelnen Hintergrundworker niedriger Priorität. Dessen Thread-Ziel und
Aufträge halten eigene Daten und unveränderliche Policy-Snapshots; keinen
Library-Kontext, keine nativen Pointer und keine Unity-Objekte. Der Main-Thread-
Adapter behält den geprüften Library-Kontext. Kein OnDestroy-/Update-Host wird für
die dauerhafte Arbeit verwendet. Neue Karten benötigen keinen Plugin-Neustart.

Die Erfassung verwendet öffentliche Lesezugriffe der tatsächlich installierten
Extender-Assembly und die echte `Assembly-CSharp.dll`, keine publicized Assembly.
Unit- und Building-Spans werden mit Game-ID `spanIndex + 1` kopiert. Der aktuelle
Vegetation-Span beginnt dagegen bei Slot null; diese unterschiedliche Basis ist im
lokalen Extender-Quellcode ausdrücklich nachgeprüft.

Der einzige fehlende öffentliche Layer wird über eine begrenzte, lesende Sicht
erfasst: rechteckige Verfügbarkeitsmatrix, RVA `0x3A11EA4`, 640.000 Bytes, Index
`y*800+x`. Die acht Richtungsmasken bei RVA `0x312620` werden zusätzlich gegen
`01 02 04 08 10 20 40 80` validiert. Beide Verträge gelten ausschließlich für die
kanonische installierte CrusaderDE.dll mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die 2.400 Row-Lookup-Ints werden über den öffentlichen Extender-Pointer kopiert;
sie sind nicht mit dessen 800 Row-Count-Einträgen zu verwechseln. Unbekannte
Kapazitäten, Layouts oder Hashes stoppen die Erfassung ausdrücklich.

Vorhandene veröffentlichte Torregeln werden nur über
`IEnemyGateRoutePolicyProvider` / `IEnemyGateRoutePolicySnapshot` übernommen.
Der Worker benutzt deren unveränderlichen Lesevertrag. Ein veröffentlichter Provider
ohne sicher erfassbaren Snapshot ergibt `UNSUPPORTED`; fehlende Provider werden als
fehlende Policy-Abdeckung dokumentiert. BugfixesAndQoL und MoatMove sind keine
harten Runtime-Abhängigkeiten.

## Prüfungen und Auswertung

`dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release` prüft
Suchverhalten, Einschränkungen, Grenzen, Updates und den Rohdaten-Kantenadapter.
Die unabhängige Vollsuche verwendet ihre eigene Zustandsverwaltung, Warteschlange
und Rekonstruktion. Der aktuelle Precise-Kern und sein Kantenprädikat werden nur im
Offlineprojekt aus unveränderten Workspace-Quellen kompiliert. Es gibt keinen
Runtime-Verweis auf deren Mod-DLLs und keinen historischen Fallback.

`dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release -- --replay <Datei.bpf.gz>`
vergleicht eine gespeicherte Anfragefolge mit beiden Referenzen und schreibt
maschinenlesbare Ergebnisse sowie einen Markdown-Bericht neben die Eingabe.
Exitcode 1 bedeutet Kosten-/Statusdifferenzen, 2 bedeutet unvollständige Prüfung.
Gleich teure Richtungsabweichungen werden separat ausgewiesen und nicht als
akzeptierte Wegtreue gewertet. Gegenbeispiele enthalten auch die vorausgehenden
Anfragen, damit Cache-Effekte reproduzierbar bleiben.

`dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release -- --bench`
misst synthetische gemeinsame Ziele bei 1/32/128/512 Anfragen auf 32×32, 128×128
und 800×800. Ein bereits beim ersten kalten Aufbau erschöpftes Größenprofil wird
nicht für größere Gruppen unverändert wiederholt. Automatische Runtime-Läufe
enthalten zusätzlich unterschiedliche Ziele, Profile und Einschränkungen.

Reale Karten decken unerreichbare und überlange Wege nicht zwangsläufig ab. Solche
Fälle sowie Mehrprofilgrenzen, Grabenpflicht, Strukturfilter, 39/40/41-Tick-Grenze,
reservierte/terminale Ziele und gerichtete Diagonalen haben separate synthetische
Vertragstests. Runtime-Berichte nennen diese Trennung. Pro Lauf wird ein ausgewählter
Spielerzugriff tatsächlich gesucht; die zweite Erfassung behält diesen Zugriff für
den Änderungsvergleich bei. Alternative plausible Kadenzprofile und vollständige
native Veröffentlichungsentscheidungen sind nicht als Ingame-Abnahme zugesichert.

Für die Bewertung zählen Erfassung, Aufbau, Vorbereitung, Änderungen und konkrete
Wegausgabe zusätzlich zur Suche. Die erste Suche nach Feldvorbereitung ist kein
isolierter kalter Start. Die private Nibble-Kodierung wird gemessen, aber niemals in
native Puffer geschrieben. .NET-Offlinemessungen sind kein Unity-/Mono-Nachweis.
Den datierten Implementierungs- und Messergebnisstand enthält `RESULTS.md`.
