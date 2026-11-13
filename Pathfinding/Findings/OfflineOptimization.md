# Offline-Untersuchung der verbleibenden Pathfinding-Kosten

Prüfung vom 13.11.2026, Modpack-HEAD `1d10d7ddf56eb02eefad502b8051c3ed40f6358c`
zuzüglich Arbeitsstand. Kein Spiel gestartet, keine installierte Datei geändert.
Kompakte Nachweise: `.work/a/pf-offline/001/enemy-scan.json`,
`enemy-scan-release.log`, `assassin-benchmark.log`, `group-core.log`,
`group-capture.log`, `group-capture.json`, `group-native-build.json` und
`comparison-plan.json`. Diese datierten Ergebnisse sind
keine Ingame-/MP-Abnahme und keine Aussage über heutige Häufigkeiten im Spiel.

## G1/G2: konkrete native Kandidaten geprüft, nicht integriert

Der erneute Audit bindet die installierte kanonische DLL an
SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
CURRENT.json und dessen DATABASE_INFO.json. Der vollständige 2665-Byte-Körper
`0x18C160` stimmt mit `53FF884138B941ECD72D0CAC182228680D3702B5D94B2A991DFFE2B8230C22D0`
überein. Generische Funktionsnamen bleiben Kandidaten; konkrete Feld-, Arithmetik-
und Branchverträge sind über die vollständigen ursprünglichen Audits in
[SIM_TICK_SPIKE_PATHING](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md)
und die aktuelle Instruktions-/Byteprüfung abgesichert.

Geschlossene Relevanzkette: normale/erzwungene Scaneinstiege, ursprüngliche
Listenzählung und Reihenfolge, vorläufige Bewegung nach bereits gelesener
Actorposition, beide Distanzzweige, separates Distanz-/Auxminimum vor
Global-ID-Prüfung, Heimursprung, inklusive Reichweite, live gelesene Scoretabelle,
strikte Gleichstandsauswahl, Nachbarkachelhöhe, PCL/Bewegung, Cooldown sowie
Fehl-/Rückkehrpfade. Kandidaten dürfen diese Entscheidungen nicht überspringen.
Ein allgemeiner Seedbereich oder unveränderliche Tabelleninhalte wurde nicht
vorausgesetzt. Die unbekannten übrigen Seedwriter verhindern diese reine
signed-Arithmetikprüfung nicht, weil die Kandidaten den vollen signed Wertebereich
und den originalen dynamischen Tabellenzugriff erhalten.

Implementierte **Offline-Kandidaten**:

- G1 ersetzt die variable Scoredivision durch Identität beziehungsweise exakt
  gegen null gerundete Division durch 2/5. Quotient und Rest werden erhalten,
  ebenso ADD-Ergebnis/Flags und die übrigen Register. Die Tabelle wird weiterhin
  genau einmal gelesen; kein Jittercache. Ein alternativer IDIV-Pfad erhält
  abweichende Divisoren, obwohl native Listencounts nur 1/2/5 erzeugen.
- G2 prüft `P=floor(2*m/5)*m < M`; dann sind Quotient 0 und Rest P exakt.
  Sonst läuft die ursprüngliche Division. Beide Major-Zweige erhalten alle
  Ausgaben; der originale Folge-CMP setzt die danach verwendeten Flags.
- Hookspannen: Score `0x18C6D2..0x18C6E0` (14 Byte), Distanz
  `0x18C50C..0x18C52C` und `0x18C533..0x18C553` (je 32 Byte).
  Ganze Instruktionen, kein innerer Kontrollfluss; sämtliche exportierten
  eingehenden Xrefs werden maschinell auf Innenziele geprüft. Die unveränderten
  anschließenden Sprünge/CMPS liegen außerhalb der Spannen.

Die eigenständige Suite
[EnemyScanOptimization.Tests](../tests/EnemyScanOptimization.Tests/Program.cs)
führt die Originalbytes und Kandidaten im **installierten RedBird-X64InlineHook**
auf privaten ausführbaren Puffern aus. 7.095.335 Assertions bestanden:
signed Extremwerte und Zufallswerte, dynamische positive/negative Tabellenindizes,
beide Distanzzweige über alle Major-Werte 1..65535, zusätzliche Grenz-/Zufallspaare,
Quotient/Rest/Ergebnis, definierte Flags, Register und inaktiver/aktiver Logikpfad.
Die installierte Spiel-DLL wird nur gelesen, niemals geladen oder gepatcht.

Release-Messung: .NET 10.0.11, Iced 1.21.0.0, RedBird 1.5.0.0,
AMD64 Family 25 Model 97. Je Fall acht abwechselnde Original-/Kandidatenmessungen,
Median der mittleren beiden Werte, jeweils eine Million native Aufrufe. Eine
verwaltete Überleitung pro gesamtem Batch; enthalten sind native Aufrufhülle,
tatsächliche Hooksprünge, Logikschalter und Kandidatendispatch.

| Fall | Original ms | Kandidat ms | Kandidat/Original |
| --- | ---: | ---: | ---: |
| Score /1 | 2,756 | 2,957 | 1,073 |
| Score /2 | 2,761 | 3,391 | 1,228 |
| Score /5 | 2,768 | 3,569 | 1,290 |
| Distanz m=0, M=5000 | 2,758 | 3,207 | 1,163 |
| Distanz m=2, M=5000 | 2,766 | 3,151 | 1,139 |
| Distanz m=50, M=5000 | 2,761 | 3,160 | 1,144 |
| Distanz m=4000, M=5000 | 2,761 | 3,155 | 1,143 |
| Distanz m=M=65535 | 2,750 | 3,136 | 1,140 |

**Entscheidung:** keine produktive Integration dieser Kandidaten. Der tatsächliche
Einbau ist in allen finalen Messfällen langsamer; bloße Divisionsersparnis wäre
kein Nettogewinnnachweis. Kandidaten und Prüfungen bleiben ausschließlich im
Offline-Testprojekt, keine neue Hookstelle, kein Runtime-Schalter und kein
unbenutzter Optimierungscode im Hauptmod. Eine spätere Integration wäre durch
erneuten Gleichheits-/Maschinennachweis und Messung einer anderen konkreten
Einbauform zu rechtfertigen. Das Ergebnis widerlegt nicht jede Form von
Loop-Multiversionierung oder jede andere CPU; es liefert auch keine FPS-Prognose.

Reproduktion vom Workspace-Root:

    dotnet run --project Pathfinding/tests/EnemyScanOptimization.Tests/EnemyScanOptimization.Tests.csproj -c Release -- .

## G3: räumlicher Index – offene Verträge konkretisiert

Ein Tickanfangsindex ist kein Ersatz für während aufsteigender Unitupdates
gelesene aktuelle Positionen. Mindestens Bewegung, Spawn/NeedsInit→Alive,
Teleport, Slot-/Global-ID-Wechsel, Besitzerwechsel, Save-/Transferinitialisierung
und Änderungen zwischen zwei Scans benötigen einen identischen Beobachtungszeitpunkt.
Zusätzlich bleiben ursprüngliche Listenzugehörigkeit, Count/Divisor,
Gleichstandsreihenfolge und der Beitrag veralteter Listeneinträge zum Distanzminimum
maßgeblich. Ein Index nur für Angriffskandidaten beseitigt nicht automatisch die
Arbeit für Distanz und Auxminimum. Euclid-/Dreiecksabschneidung bleibt unzulässig.

Nächster belastbarer Schritt wäre ein kontrollierter Trace der **gleichen**
Scanaufrufe vor/nach den jeweiligen Writern; erst danach ein Replay mit Index
gegen Originalausgaben einschließlich Bewegungsaufrufen und deren Argumenten.
Ohne diesen Vertrag und gemessene Pflegekosten kein produktiver Index.

## G4–G7: heutiger Code und verbleibende Messgrenzen

- **G4:** `AnchorRouting.TryPublishAnchorSingle` hat weiterhin 500000 Settles pro
  Tick für Bodenwege. Assassinen werden angerechnet, aber mit Budget 0 unbegrenzt
  gesucht; ein Bodenabbruch kann Vanillas Builder zurückgeben. Die alte Zahl ist
  keine harte globale Arbeitsgrenze. Für die Spielmessung Kern, Vanilla-Fallback
  und Neuplanwächter getrennt zählen; kein Budgetabbruch als Unreachable behandeln.
- **G5:** `EnsureCursorTopology` ersetzt die Topologie bei Mapepoch oder
  RegionGeneration und baut sie bei Bedarf vollständig. Diplomatie verwirft die
  gespeicherten Topologien ebenfalls. Für neue Messungen vorhandenen
  DEVDIAG-Marker `cursor-topology` mit `reason` sowie Builds/Updates verwenden.
  Vor lokaler Wiederverwendung müssen Split/Merge alter Regionen, Richtungswechsel,
  Tor-/Graben-/Gebäudeänderungen vollständig verfolgt sein. Kein alter Graph wird
  allein zur Verringerung der Aufbauzahl behalten.
- **G6:** Bestehender Produktionskern-Benchmark `--native-benchmark` wurde ohne
  Spiel ausgeführt: castle 54,3 ms/200 Anfragen, detour 44,4 ms/2000, gate
  24,3 ms/2000 und open 16,3 ms/2000. Das sind isolierte Fixture-Gesamtkosten,
  keine Häufigkeit unabhängiger Restanfragen im aktuellen Spiel. Erst deren Anteil
  außerhalb der vorhandenen Gruppenfelder rechtfertigt einen neuen Cache.
- **G7:** GroupPathProbe `--anchor-core` bestand 2961 unabhängige Vergleiche gegen
  aktuellen nativen Kern einschließlich Thread-/Batchgleichheit. Der zusätzliche
  Capturerunner bestand die synthetische Last `moat-group-mix-10000` mit 10000
  Anfragen in 40 Gruppen: 5.706.887 Settles, Feldaufbau 162,2 ms (ein Thread)
  beziehungsweise 49,3 ms (acht), Einzelabfragen 81,2/80,9 ms, Batchabfragen
  88,1/25,0 ms. Alle Routen waren legal und mit unabhängiger Precise-Referenz
  verglichen; Ergebnisse blieben thread-/batchgleich. 4172 Routen lagen über dem
  Optimum, maximal 15 Kosten beziehungsweise 4,36%; Grabenentscheidungen wichen
  nicht ab. Das ist ein Qualitäts-/Lastbefund innerhalb des vorhandenen
  Anchor-Vertrags, keine neue Regression. Die rund 180 s Referenzlaufzeit ist
  Offline-Vergleichsarbeit, keine beobachtete Spiellag-Dauer.
  Der gelesene historische Snapshot (30.10.2026) lieferte keine weiteren Gruppen
  oberhalb der Messschwelle 1024; die genannten Zahlen stammen ausdrücklich aus
  der synthetischen Last. Kern-DLL und Quellhash wurden gegen build.json geprüft.
  Das ist keine Messung heutiger Spielhäufigkeit. Der vorhandene Startfilter
  `StartFilterBudget=16` bleibt bestehen; keine neue Vorprüfung ohne Nettogewinn.

Bestehende Aufrufe:

    dotnet run --project Testmods/GroupPathProbe/tests/GroupPathProbe.Tests.csproj -- --anchor-core
    dotnet run --project Pathfinding/tests/AssassinPathfinding.Tests/AssassinPathfinding.Tests.csproj -- . --native-benchmark

## G9: ausführbare Parameter vorbereiten, Ergebnisse offen lassen

[Prepare-ComparisonMatrix.ps1](../tools/Prepare-ComparisonMatrix.ps1) erzeugt zwölf
vorbereitete Parameterzeilen in A/B–B/A-Reihenfolge: freundliche Grabenquerung,
menschliche Assassinenwege und KI-Assassinenwege jeweils aus/an, andere Werte
konstant. Zusätzlich beschreibt die Ausgabe regelbedingte Bursts, Regionswechsel,
unabhängige Assassin-Anfragen, große Gruppenfelder, Editor und echte Host-/Clientfälle.

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File Pathfinding/tools/Prepare-ComparisonMatrix.ps1 -OutputFile .work/a/pf-offline/001/comparison-plan.json

Der Vorbereitungslauf bestand im frischen Windows-PowerShell-Unterprozess.
Er startet nichts und verändert keine Spieleinstellungen. Die Ausgabe enthält
Runnerargumente für den vorhandenen Iterationsrunner, erforderliche Hostsettings,
Szenariovoraussetzungen und Messgrößen. Der Iterationsrunner setzt die benötigten
Modsettings nicht selbst: vor späterem Ausführen über vorhandene Preset-/Test-API
anwenden und wirksame Werte nachlesen. Der Stresssave ist bis zur Szenarioprüfung
nur ein Platzhalter, insbesondere für menschliche Befehle und den Editor.
Diagnose-/Samplerläufe und uninstrumentierte Kontrollen getrennt behandeln.
Laden/erste Ticks, Bewegung, Renderer, Simulation, Allokationen sowie vollständige
Wiederherstellung getrennt nachweisen. Alle Spiel-/MP-Ergebnisse bleiben offen.
