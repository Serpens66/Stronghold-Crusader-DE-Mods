# BetterPathfindingTest 0.2.0

Eigenständiger lesender Testmod mit privatem C++-Suchkern. Keine Spielbefehle,
Pfadpuffer-Schreibzugriffe, Traversal-Registrierung oder nativen Spielhooks.
`NetworkMode=0` beschreibt diese lokale Beobachtung. Es werden keine Verbraucher
angeschlossen und keine Spiel- oder Modkonfigurationen verändert.

## Aufbau

Die gescheiterte 32×32-/128×128-Hierarchie wurde ersetzt. Der neue Kern in
`native/Navigation.cpp` arbeitet ausschließlich auf eigenen Snapshot-Kopien:

- Homogene Rechteckflächen werden verdichtet. Ihre inneren Knoten entfallen im
  Fernsuchgraphen; Makroübergänge entstehen implizit und speichern keine vollständigen
  Teilwege. Richtungs-/Kosten-/Strukturausnahmen bleiben explizit.
- Die Anpassung verwendet diskrete Chebyshev-Schritte. Einheiten benutzen weiterhin
  acht Richtungen; es gibt keine kontinuierliche Navmesh-Glättung.
- Seltene oder unterschiedliche Ziele verwenden exaktes A* auf dem Flächengraphen.
  Ein generationengestempelter Arbeitsbereich vermeidet eine vollständige neue
  Arrayinitialisierung pro Anfrage.
- Ab 32 Anfragen zum selben Ziel/Kosten-/Zugriffskontext wird ein gemeinsames
  Rückwärtsfeld verwendet. Vorhandene Felder werden auch bei Einzelanfragen genutzt.
  Die vier Einträge des Caches sind nach Bedarf verdrängbar.
- Größere Gruppen im selben Startbereich verwenden zusätzlich echte lokale Felder.
  Deren Randwerte sind die bereits ermittelten globalen Fortsetzungskosten. Es
  entstehen keine separaten globalen Suchen für jeden Grenzknoten.
- Anfragen werden nach Kontext gebündelt; eine stabile Anfrage-ID bindet Ergebnisse
  nach dieser internen Umordnung wieder an die ursprüngliche Anfragefolge.
- Ein zusätzliches Wet-Visit-Bit erhält Grabenpflicht einschließlich positiver
  Rückkehrzyklen. Verletzt das skalare Optimum Ressourcen- oder Schrittgrenzen,
  folgt eine exakte native Mehrzustandssuche auf dem ursprünglichen Gitter.
  Terminale Endpunktausnahmen verwenden diese explizite Suche ebenfalls.

Grundlagen, Anpassungsgrenzen und Exaktheitsargument stehen in
[native/SOURCES.md](native/SOURCES.md). Eine feste kleine Zahl nicht dominierter
Alternativen wird nicht als vollständige Lösung ausgegeben. Erschöpfung bedeutet
`Inconclusive`, niemals `Unreachable`. Der alte Solver bleibt nicht als Runtime-
Fallback erhalten. Der aktuelle unveränderte Precise-Kern ist nur Offline-Oracle.

Die konservative Grenze von **256 MiB** umfasst jetzt eigene verwaltete und native
Navigationspuffer. Der C++-Allocator zählt Reservierungen, Höchststand und Allokationen.
Eigene zurückgegebene Wegpuffer bleiben bis zur Freigabe des Navigators bilanziert.
Unabhängige Korrektheitsreferenzen haben eigene begrenzte Budgets; der Gesamtprozess
und dessen verwalteter Heap sind keine isolierte Speichergröße des Navigators.

Native Arbeit läuft auf einem einzigen Worker mit niedriger Priorität. Suchläufe
geben nach begrenzten Operationspaketen die Kontrolle zurück. Lange Dominanzketten
besitzen zusätzlich einen diagnostischen Abbruchschutz. Diese Echtzeitgrenzen sind
keine Multiplayer-Übernahmeentscheidung und garantieren keinen optimalen Weg bei
Abbruch. Graphersetzung verwirft derzeit alle Zielfelder des betroffenen Kontexts,
auch für neue Abkürzungen außerhalb alter Wege. Inkrementelle Graphreparatur ist noch
nicht implementiert; unveränderte Graphen behalten ihre Felder.

## Bauen und installieren

`build.bat /nopause /source:working /devdiag` prüft, baut und installiert ausschließlich
`BepInEx/plugins/BetterPathfindingTest_Serp`. `/noinstall` lässt das Spielverzeichnis
unverändert. Die installierten Extender-/APIShared-Dateien werden gehasht und weder
neu gebaut noch ersetzt. Ein laufendes Spiel verhindert die Installation.

Benötigt werden der vorhandene Visual-Studio-x64-C++-Compiler, Windows SDK, MSBuild
und .NET. Der native Kern wird optimiert mit statischer CRT kompiliert; es ist keine
zusätzliche VC-Runtime-Installation erforderlich. Das Diagnosepaket enthält:

- `BetterPathfindingTest.dll` und `.pdb`;
- `BetterPathfindingNative.dll`, privates x64-C-ABI 3;
- `info.json`, Version 0.2.0.

Ein normaler Build ist inaktiv und enthält weder automatische Runtime noch native
Diagnose-DLL. Diagnosecode wird ausschließlich mit `/devdiag` aktiv. Binärprüfungen
kontrollieren diese Grenze sowie x64-Architektur, ABI und konsistente Modversion.
Der native Kernel selbst verweigert Kompilierung ohne Diagnosesymbol.

## Automatischer Ingame-Test

Nach einem regulären abgeschlossenen Kartenstart, Save-Laden oder Editor-Start
wird genau eine Sitzung angelegt. Die Erfassung kopiert unter der validierten
Engine-Sperre nur Rohdaten. Eine belegte Sperre oder kurzzeitig fehlende Tor-Snapshots
werden ohne blockierendes Warten höchstens 30 Sekunden lang erneut versucht.

Der erste Rohsnapshot wird **sofort gespeichert**. Große Benchmarks warten auf den
zweiten Snapshot, der zehn Sekunden nach dem ersten angefordert wird. Auch dieser
wird vor den Vergleichen gespeichert. Zuerst folgen tatsächlicher Änderungsvergleich
und kontrollierte private Änderungen, dann kleine synthetische Positivlasten und
schließlich die großen Karten-Arbeitslasten. Kartenwechsel brechen alte Arbeit logisch
ab; ein vorzeitig beendetes Snapshot-Paar bleibt ausdrücklich unvollständig.

Die Kartenlasten enthalten gemeinsame Ziele, räumlich zusammenhängende Formationen,
verteilte Ziele/Profilvarianten und erforderliche Grabenquerungen. Positive Eingaben
werden über tatsächliche gerichtete Verbindungen gewonnen. Anfragen sind dabei
**keine 10.000 aufgenommenen Spieleinheiten oder aufgezeichneten Befehle**. Fehlende
Grabenfälle werden als fehlende Abdeckung ausgewiesen, nicht künstlich bestanden.
Die Kostenprofile dieser Lasten sind ausdrücklich synthetisch; reale Rohprofile
bleiben für die separaten Profilprüfungen im Snapshot erhalten.

Pro Kartenlast werden 1/32/128/512/10.000 Anfragen untersucht; kleine Framework-
Fixtures bleiben bei 1/32/128/512. Einzelne Lasten haben 15 Sekunden, ein Snapshot-
Vergleich 120 Sekunden. Bereits ohne beantwortete Anfrage gescheiterte Aufbauten
werden nicht für größere Gruppen unverändert wiederholt. Referenzprüfungen auf
Karten über 4.096 Koordinaten sind auf drei feste Fälle pro Last beschränkt. Alle
zurückgegebenen kalten und warmen Wege werden zusätzlich kantenweise geprüft.
Stichproben beweisen keine vollständige Kostenparität aller Anfragen.

Die Diagnose kann den Rechner belasten. Sie ist kein Beleg für Spielflüssigkeit oder
bereits erreichte 100-ms-Reaktionszeit. Aufbau, private Wegausgabe, verwaltete Prüfung,
Aktualisierung und spätere echte Wegpublikation gehören in die Gesamtbewertung.

## Berichte und Offline-Prüfung

Die Dateien liegen unter `BepInEx/cache/BetterPathfindingTest_Serp/<UTC-Zeit>-s<ID>/`:

- `snapshot-N.raw.gz`: früh persistierter Rohsnapshot;
- `snapshot-N.json/.md`: zunächst ausstehende, später zusammengefasste Vergleiche;
- `snapshot-N-*.bpf.gz` und Arbeitslastberichte: Eingaben, Ergebnisse, gefundene Wege,
  Referenzumfang, Richtungsabweichungen und Kosten;
- `updates.json`: tatsächliche und kontrollierte Änderungen getrennt;
- `private-*.json/.md/.bpf.gz`: ausdrücklich synthetische Positivlasten;
- `snapshot-N.progress.json`: zuletzt abgeschlossene Last;
- `session.json`: gegebenenfalls vorzeitig beendetes Snapshot-Paar;
- `unsupported-*.json/.md`: nicht unterstützter Initialisierungs-/Capturevertrag.

Logmarker nennen Sitzung, Snapshot, Phase und Fallzahlen. Fortschrittsmarker während
längerer Phasen erscheinen höchstens alle fünf Sekunden. Schreibfehler werden
protokolliert; Cleanup und spätere Sitzungen bleiben möglich.

`dotnet run --project tests/BetterPathfindingTest.Tests.csproj -c Release` führt die
Verhaltenstests aus. Vorher `native/Build-Native.ps1` ausführen oder den Buildtreiber
verwenden. Der Framework-Testhost läuft in getrennten x64-Prozessen mit dateibasierter
und tatsächlicher Speicherladung der installierten Spielassembly. Er ruft private
native Berechnungen auf, aber keine nativen Spielmethoden.

Offline-Optionen nach `--`:

- `--replay <Datei.bpf.gz>`: gespeicherte Anfragefolge vergleichen;
- `--scale`: 800×800, Boden und erforderliche Grabenquerung, 1/128/512/10.000 Anfragen;
- `--captured <snapshot.raw.gz>`: positive Lasten aus einem aufgenommenen Kartengraphen;
- `--bench`: ergänzende offene Gitter 32×32/128×128/800×800.

Die Referenz ist eine unabhängige Vorwärts-Dijkstra-Suche auf dem ursprünglichen
Gitter mit eigener Zustandsverwaltung. Ein zulässiges skalares Optimum wird direkt
verwendet; sonst prüft eine unabhängige Pareto-Suche die Ressourcenbedingungen.
Der Precise-Oracle kompiliert aktuelle Workspace-Quellen unverändert. Seine
Messzeiten enthalten die ausdrücklich ausgewiesene Adapterkonvertierung und Prüfung.
Größentests und Capture-Replays speichern native Compiler-/Quell-/Binäridentität.

Gleiche optimale Kosten, korrekte Erreichbarkeit und erfüllte Nebenbedingungen sind
Pflicht. Gleich teure Richtungsabweichungen bleiben vor einer Integration zu bewerten.
Nur negative Antworten ergeben keine positive Abnahme. Kosten- und Statusfehler
führen zu Exitcode 1; unvollständige Replays zu Exitcode 2.

## Capturevertrag und Grenzen

Der installierte Assemblypfad stammt aus dem BepInEx-Spielverzeichnis; geladener
Name/MVID und optionaler Dateihash sind getrennt. Eine leere `Assembly.Location`
verhindert die Erfassung nicht. Der geladene Engine-IL-Vertrag und die verbindlichen
Native-/Layoutprüfungen bleiben erhalten. Der statisch verwurzelte Missions-/Render-
Beobachter überlebt SHCDEs Startup-Cleanup; er verwendet keine Plugin-Update-Schleife.

Die vorhandene lesende Native-Sicht bleibt hashgebunden. Sie und die Updateprüfung
stehen in [UpdateToNewDLL.md](UpdateToNewDLL.md). Vorhandene Torregeln werden über
`EnemyGatePathPolicyBridge.Current`, `IEnemyGatePathPolicy` und unveränderliche
`IEnemyGateRoutePolicySnapshot` erfasst. Ein vorhandener veröffentlichter Provider
ohne aktuelle passende Snapshots wird niemals durch uneingeschränkte Traversierung
ersetzt. BugfixesAndQoL und MoatMove bleiben optionale Laufzeitumgebung.

Nicht umgesetzt sind echte Bewegungsübernahme, Assassinenintegration, eine öffentliche
Mod-API, vollständiger Regel-/Writer-Audit für dauerhafte Live-Caches und Multiplayer-
Übernahme. Dauerbetrieb mit 10.000 echten Spieleinheiten sowie zusätzliche Frame-/
Tickkosten sind noch nicht abgenommen. Datierte Ergebnisse: [RESULTS.md](RESULTS.md).