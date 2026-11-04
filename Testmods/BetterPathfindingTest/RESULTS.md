# Ergebnisstand vom 26.10.2026

Aktueller Entwicklungsstand: siehe das **CCH-Entscheidungstor vom 27.10.2026** weiter unten. Installiert bleibt 0.2.0.
Die folgenden älteren Abschnitte bleiben historische Nachweise der abgelösten Hierarchie.

## Einordnung

Der Prototyp implementiert eine eigene exakte Navigationshierarchie. Die bisherige
Suchimplementierung wurde nicht verändert. Das erste Performance-Ergebnis ist
**negativ**: Diese konkrete Hierarchie erfüllt das Ziel einer Größenordnung weniger
Gesamt-Suchkosten auf großen Karten nicht. Verbraucher sollten nicht angeschlossen
werden. Gute warme Einzelwerte kompensieren hier weder Aufbau noch Speicherbedarf.

## Fachliche Offline-Prüfung

6.827 Assertions, 295 Vergleiche mit der unabhängigen Vollsuche bestanden im
Implementierungslauf. In den Vergleichen mit dem aktuellen Precise-Kern: keine
Kosten- oder Erreichbarkeitsabweichungen, 60 gleich teure Richtungsabweichungen.
Diese Abweichungen bleiben offen; sie sind keine akzeptierte Wegtreue.

Erfasst sind unter anderem gerichtete Zufallsgraphen mit festem Seed, Mehrprofil-
Grenzen, Grabenpflicht, Strukturfilter, terminale Ausnahme, über 2.000 Schritte,
beide Hierarchiestufen einschließlich diagonaler Grenzen, globale Fortsetzungskosten
lokaler Felder, Sperrung, neue Abkürzung außerhalb des alten Weges, Budgetabbruch,
Abbruchtoken und Replay-Roundtrip. Der Rohdatenadapter wird zusätzlich kantenweise
gegen das unveränderte Precise-Prädikat mit Besitzern, Allianzen, Höhen und
Strukturklassen verglichen. Tatsächlich akzeptierte erfasste Kadenzprofile stimmen
mit `WeightedMovementCostProfile` überein. Die 39/40/41-Tick-Grenze ist separat geprüft.

Die Dateien `results/offline-*/precise-tie-*.bpf.gz` enthalten reproduzierbare
Gegenbeispiele mit Anfragehistorie. Ein Replay von `precise-tie-1` hat die
Richtungsabweichung bei unveränderten optimalen Kosten erneut bestätigt.
Frühe Entwicklungsartefakte unter älteren `offline-*`-Verzeichnissen können
vorläufige Vergleichsadapter-Befunde enthalten. Sie werden als Historie aufbewahrt;
maßgeblich sind die hier genannten abschließenden Läufe. Ein anfänglicher
Strukturfilterfehler im Offline-Adapter wurde korrigiert, ohne Produktionscode zu
ändern.

## Erster Maßstabstest: .NET 10.0.11, synthetische offene Gitter

Messserie `results/bench-20261026-115931`, lokaler Entwicklungsrechner. Die Abfragen
beginnen in derselben kleinen Startregion und haben ein gemeinsames fernes Ziel.
Die Zeitangaben sind Beobachtungen dieses Laufs, keine stabilen Leistungszusagen.

| Karte / Gruppe | Vorbereitung | Erste Abfragen nach Vorbereitung | Precise-Abfragen | verwaltete Puffer, konservativ reserviert |
|---|---:|---:|---:|---:|
| 32×32 / 512 | 0,674 ms | 1,214 ms | 1,977 ms | 4.471.296 Bytes |
| 128×128 / 512 | 602,874 ms | 1,476 ms | 3,755 ms | 30.460.970 Bytes |
| 800×800 / 1 | 0,005 ms | Abbruch nach 8.565,860 ms | nicht verglichen | 268.435.419 Bytes |

Der 800×800-Fall erschöpft das Limit von 256 MiB; es gibt keine vollständig
beantwortete Anfrage. Auch die Vollreferenz kann unter dem dann belegten gemeinsamen
Budget keinen vollständigen Vergleich liefern. Das ist `INCOMPLETE`, kein Beweis
für Unerreichbarkeit und kein bestandener Korrektheits-/Performance-Test.

Auf 128×128 summieren sich Vorbereitung und erste Abfragen bereits auf rund
604 ms gegenüber rund 3,8 ms für den bestehenden Precise-Kern. Selbst warme
Abfragen alleine erreichen hier keine Größenordnung Verbesserung gegenüber
Precise. Die sehr viel teurere unabhängige Pareto-Vollreferenz eignet sich als
Korrektheitsreferenz; ein Speedup nur gegen diese Referenz wäre irreführend.

Die Ursache liegt in dieser konkreten Datenstruktur: Sie erhält alle Grenzknoten
und materialisiert pro Eintrittsknoten exakte lokale Distanzzeilen samt vollständigen
Wegzeugen. Offene Bereiche erzeugen viele miteinander verbundene Grenzpunkte;
mehrere Ebenen und Profile verstärken Cache- und Zeugenwachstum. Die lokale
Feldabfrage selbst ist günstig, ihre exakten globalen Randwerte entstehen jedoch
erst nach dieser teuren Vorbereitung.

Die Benchmarkserie enthält auch vollständig beantwortete Gruppen 1/32/128/512 auf
32×32 und 128×128. Größere Gruppen für 800×800 wurden nach dem gescheiterten ersten
kalten Aufbau nicht ohne neuen Erkenntnisgewinn wiederholt. Die automatisch
erzeugten Berichte enthalten Messwerte und sämtliche reproduzierbaren Eingaben.

## Runtime-Nachweis und Grenzen

Der normale Build besteht mit binär geprüfter Abwesenheit der automatischen
Diagnose. Der `/devdiag`-Build enthält die Markierung, den Scheduler und Capture-
Adapter. Der echte installierte Engine-Monitor wurde einschließlich Konkurrenzfall
getestet, ohne native Spielmethoden aufzurufen. Scoped Source-, Abhängigkeits-,
Lifecycle- und Diagnoseprüfungen sowie Offline-Verhaltenstests sind Bestandteil
des Buildtreibers. Eine Warnung im unveränderten verlinkten Shared-Hashhelfer
betrifft dessen hier nicht verwendetes optionales PersistentHashDirectory.

**Noch kein Ingame-Lauf beobachtet.** Automatische Erfassung, Pausenverhalten,
Save-/Editor-Start und echte Kartenänderungen müssen beim nächsten regulären
Kartenstart anhand der erzeugten Berichte und Post-Startup-Logmarker bestätigt
werden. Die installierten Dependencies werden vom Build nicht ersetzt. Quelle,
Buildmodus, Artefakthash und vor/nachher geprüfte installierte Dateien stehen unter
`.local/build*.json`.

Es gibt keine Assassinenintegration, keine native Bewegungsübernahme und keine
Multiplayer-Freigabe für einen späteren gameplaybeeinflussenden Verbraucher.
Fehlende Tor-Policies, nicht erfasste Zugriffskontexte und synthetisch statt real
geprüfte Bedingungen sind in den Laufberichten ausdrücklich markiert.

## Folgerung für einen weiteren Systementwurf

Das Ergebnis spricht gegen die direkte Fortsetzung mit vollständigen
Grenzknoten-Distanzzeilen und materialisierten Wegzeugen als grundlegender
Abstraktion. Ein weiterer eigener Systementwurf sollte große homogene freie
Flächen kompakt repräsentieren und gemeinsam verwendbare Zielberechnungen zuerst
auf dieser kompakten Repräsentation ausführen. Denkbar sind exakte Portalintervalle
oder ein aus Gehflächen abgeleitetes Mesh mit getrennten Graben-/Strukturklassen,
kompakter Rekonstruktion und gerichteten Übergängen.

Eine solche Kompression benötigt einen eigenen Exaktheitsnachweis: Profilgrenzen,
Grabenpflicht, gerichtete Diagonalen und das 2.000-Schritt-Limit dürfen weiterhin
keine gültigen Alternativen verlieren. Besonders lokale Randwerte und neu entstehende
Abkürzungen bleiben Pflichtfälle. Dieser Folgeschritt ist nicht in diesem
Prototyp implementiert; der aktuelle Mod liefert die unabhängige Vergleichs- und
Replay-Infrastruktur dafür. Kein positives Ergebnis eines fremden A*/Flowfield-
Systems wird als Leistungsnachweis für diesen Code verwendet.

## Nachtrag: Laufzeitkorrektur 0.1.1 vom 26.10.2026

Die ersten echten Spielstarts um 14:34 und 15:29 Uhr erreichten den Render-Callback,
brachen jedoch vor der ersten Erfassung ab. `Assembly.Location` war für die aus
Speicher geladene Spielassembly leer. Beide `unsupported-*.json`-Berichte enthalten
diese Ursache; es gab zu diesem Zeitpunkt keine Rohsnapshots oder Ingame-Suchvergleiche.
Dieser Initialisierungsfehler war im ursprünglichen Framework-Test nicht abgedeckt.

Die Korrektur bestimmt den installierten Dateipfad aus dem Spielverzeichnis.
Geladener Assemblyname und MVID werden getrennt vom optionalen installierten
Dateihash erfasst. Der Engine-IL-Vertrag und die Native-/Layoutprüfungen bleiben
verbindlich. Die nach dem bisherigen Abbruch folgenden Layout-/Enumprüfungen sind
jetzt Bestandteil des Tests mit der tatsächlich installierten SDK-Assembly.

Der erweiterte Framework-Test läuft in zwei getrennten Prozessen: Dateiladung und
`Assembly.Load(byte[])` mit nachweislich leerer `Location`. Beide bestehen. Er prüft
auch Erfassungstimeout mit kontrollierter Uhr, doppelte Starts, Kartenwechsel,
Sitzungsfehler und Wiederanlauf, fehlende/veraltete/falsch zugeordnete Tor-Snapshots,
optionale Metadaten- und Allokationsfehler sowie Fortschrittsdrosselung.

Zusätzlich führt er den echten Worker mit kleinen eigenen Kartendaten aus und
bestätigt 1.346 vollständig verglichene Anfragen. Injizierte Schreib- und
Verzeichnisfehler prüfen dessen Ressourcenfreigabe sowie einen nachfolgenden Lauf.
Diese Tests rufen keine nativen Spielmethoden auf und ersetzen keine Ingame-Abnahme.
Der echte Änderungsvergleich hat beim zweiten Snapshot jetzt Vorrang vor den
Gruppenbenchmarks. Graphaufbau und Policy-Materialisierung beachten Abbruch/Budget;
ein nicht unterstützter Allokationszähler beendet keine Vergleiche.

Version 0.1.1 ist für einen erneuten regulären Spielstart bestimmt. Die
Performancebewertung der obigen historischen Messserie und die 60 offenen
Richtungsabweichungen bleiben bestehen. Die Grenzen bleiben 256 MiB und 120 Sekunden
je Vergleichslauf; für ausstehende Erfassungen gelten 30 Sekunden ohne blockierendes
Warten. Suchalgorithmus, vorhandene Mods und gemeinsam genutzte Quellen wurden für
diese Korrektur nicht geändert. Die neue Ingame-Erfassung ist nach Installation
noch gesondert nachzuweisen.

Spätere Beobachtung am selben Tag: Der Spielstart mit 0.1.1 meldete um 16:10:03.999
`READY`. Die zuvor fehlschlagende Initialisierung ist damit auch unter Unity/Mono
belegt. Für diesen Lauf liegen weiterhin keine Kartensnapshots oder abgeschlossenen
Ingame-Suchvergleiche vor. Eine abschließende Ergänzung klassifiziert abweichende
SDK-Layerkapazitäten dauerhaft als `UNSUPPORTED`; Kartenfehler bleiben sitzungsbezogen.
## Neuaufbau 0.2.0 vom 27.10.2026: C++-Flächengraph und gemeinsame Felder

Dieser Nachtrag ersetzt die aktuelle Implementierung, nicht die historischen
Messungen oben. Die alte Hierarchie wurde entfernt. Der neue private C++-Kern
verdichtet homogene Rechtecke, erzeugt Makroübergänge implizit und rekonstruiert
Wege ohne gespeicherte Grenzknotenmatrizen. Seltene Ziele verwenden A* auf dem
reduzierten Graphen; passende Gruppen ab 32 Anfragen teilen Rückwärtsfelder und
gegebenenfalls lokale Felder mit exakten globalen Randkosten. Eine ausschließlich
auf vollständige Zielfelder setzende Zwischenversion war bei verteilten Zielen zu
teuer; der gemessene Endstand verwendet deshalb diesen Hybrid.

Die Umsetzung ist eigenständig. RSR, Flow Field Tiles und Subgoal Graphs liefern
Prinzipien, keinen übernommenen Code oder fremden Leistungsnachweis. Die Anpassung
an gleich teure diskrete Diagonalschritte, gerichtete Ausnahmen und Ressourcen-
bedingungen ist in [native/SOURCES.md](native/SOURCES.md) beschrieben. Bei verletzten
Ressourcengrenzen des skalaren Optimums folgt exakte Mehrzustandssuche. Es gibt keine
feste kleine Obergrenze nicht dominierter Alternativen. Abbruch bleibt unvollständig.

### Fachliche und technische Prüfung

Der abschließende Buildlauf besteht mit **39.963 Assertions und 5.938 Vergleichen
gegen die unabhängige Vollreferenz**. Dabei wurden **0 Precise-Kosten-/Statusfehler**
und **196 gleich teure Precise-Richtungsabweichungen** beobachtet. Die Abweichungen
bleiben offen. Ihre Replays speichern getrennt die Precise-Historie und die nativen
Batchgrenzen einschließlich Cache-Vorbereitung; ein erneuter Replay mit einer
32er-Vorbereitungsgruppe reproduzierte die Richtungsabweichung ohne Kostenfehler.
Nachweise: [abschließende Offline-Fixtures](results/offline-20261027-180420-223/).

Geprüft werden unter anderem Rechteck-Innenpunkte und Grenzpunkte, gerichtete
Nähte/Diagonalen, gemischte Boden-/Graben-/Strukturklassen, Grabenpflicht samt
positivem Rückkehrzyklus, billigere Grabenschritte, mehrere Ressourcenprofile,
terminale Ausnahmen, Schrittgrenzen, lokale Felder, Umordnung von Anfragen,
Cache-Verdrängung und unveränderte Karten. Sperrungen und neue Abkürzungen werden
mit vollständigem Neuaufbau verglichen. Der eigene Graph wird bei Änderungen derzeit
konservativ vollständig ersetzt; inkrementelle Reparatur ist noch nicht umgesetzt.
Speicherabbruch und anschließender erfolgreicher kleiner Lauf sind ebenfalls geprüft.

Der x64-Framework-Host besteht in getrennten Prozessen mit Dateiladung und echter
`Assembly.Load(byte[])`-Ladung mit leerer `Location`. Er prüft weiterhin den installierten
verwalteten Enginevertrag ohne native Spielmethoden. Sein tatsächlicher Worker führt
jeweils **2.019 positive Kartenanfragen** auf einer kleinen privaten Bodenfixture aus;
fehlende reale Grabenabdeckung bleibt ausdrücklich unvollständig. Frühes Speichern
beider Rohsnapshots, Vorrang des Änderungsvergleichs, Kartenwechsel, Abbruch,
Schreibfehler, Datenfreigabe und erfolgreicher Wiederanlauf werden geprüft.

Scoped Abhängigkeits-, Shared-, Lifecycle-, Capture- und Diagnoseprüfungen bestehen.
Ein normaler Build enthält binär geprüft weder automatische Diagnose noch native
Diagnose-DLL. Der `/devdiag`-Build enthält die Markierung und das private x64-ABI 3.
Buildnachweise: [.local/build-20261027-175619-675.json](.local/build-20261027-175619-675.json)
(normal) und [.local/build-20261027-180422-971.json](.local/build-20261027-180422-971.json)
(Diagnose). Alle **282 installierten Abhängigkeitsdateien** wurden unverändert bestätigt.

### Messungen des neuen Kerns

**Diese Messungen sind .NET 10.0.11 plus optimiertem x64-C++, keine Unity-/Mono-
Messungen.** MSVC 14.41.34120, Windows SDK 10.0.22621.0, statische CRT und ABI 3.
Eingaben, Compiler-/Quell-/Binäridentität und Hostidentität liegen jeweils neben den
Berichten. Die Zahlen stammen aus einer Messserie; sie sind keine statistisch
abgesicherte Obergrenze. Frühere Zwischenstände bleiben in ihren Ergebnisordnern.

Zeiten in ms. Aufbau meint den privaten nativen Flächengraphen; die kalte und warme
Batchzeit enthalten die private Wegrekonstruktion und die verwaltete Batchübergabe.
"Prüfung" enthält die zusätzliche verwaltete Kanten-/Vertragsprüfung beider Batches
und das testweise Packen kalter Wege. Keine dieser Spalten enthält eine Veröffentlichung
von Wegen an das Spiel. Die Precise-Spalte umfasst alle Anfragen mit dessen vorhandener
Wiederverwendung sowie der unveränderten Offline-Adapterkonvertierung/-prüfung.
Kartenmaterialisierung, Oracle-Vorbereitung und unabhängige Referenz sind gesondert
in den JSON-Berichten ausgewiesen. Ein Quotient nur aus den Suchspalten wäre kein
Ende-zu-Ende-Beschleunigungsnachweis.

Synthetische 800×800-Karten mit langen positiven Wegen; "Graben" verlangt die Querung
eines Grabenstreifens. Alle acht Lasten sind vollständig beantwortet, ohne beobachtete
Kosten-/Statusfehler. Bei mehr als drei Anfragen prüft die unabhängige Vollreferenz drei
feste Stichproben; der Precise-Oracle und die Kantenprüfung laufen für sämtliche Anfragen.

| Karte | Anfragen | Aufbau | Kalt | Warm | Prüfung | Precise |
|---|---:|---:|---:|---:|---:|---:|
| Boden | 1 | 13,23 | 4,34 | 0,76 | 0,41 | 4,67 |
| Boden | 128 | 10,58 | 3,19 | 1,57 | 14,19 | 49,80 |
| Boden | 512 | 10,69 | 3,85 | 3,59 | 4,07 | 111,80 |
| Boden | 10.000 | 11,58 | 77,16 | 60,14 | 72,36 | 301,00 |
| Graben | 1 | 11,65 | 7,06 | 6,96 | 0,02 | 57,53 |
| Graben | 128 | 11,81 | 68,35 | 2,41 | 1,01 | 73,19 |
| Graben | 512 | 11,64 | 71,32 | 4,79 | 3,75 | 97,62 |
| Graben | 10.000 | 11,82 | 134,48 | 63,91 | 72,06 | 285,87 |

Nachweise: [800er-Serie](results/areas-20261027-174928/), insbesondere
[Boden, 10.000](results/areas-20261027-174928/ground-800-10000.md) und
[Graben, 10.000](results/areas-20261027-174928/moat-800-10000.md).
Die konservativ bilanzierten eigenen verwalteten und nativen Navigationspuffer
liegen bei den 10.000er-Lasten bei **48,42 bzw. 52,62 MiB**. Das ist kein isolierter
Gesamtprozessspeicher einschließlich der separat budgetierten Referenzsuche.

Zusätzlich wurde der bereits aufgenommene reale 800×800-Graph mit 317.604 gültigen
Koordinaten erneut offline verarbeitet. Nur **53.793 innere Koordinaten** lassen sich
hier entfernen; die Verdichtung ist deutlich geringer als im offenen synthetischen
Gitter. Positive Eingaben stammen aus einer tatsächlich verbundenen Komponente;
10.000 Anfragen sind keine 10.000 verschiedenen erfassten Spieleinheiten.

| Last | Vollständig / gefunden | Aufbau | Kalt | Warm | Prüfung | Precise |
|---|---:|---:|---:|---:|---:|---:|
| Gemeinsames Ziel | 10.000 / 10.000 | 13,84 | 48,56 | 12,57 | 21,21 | 86,66 |
| Zusammenhängende Formation | 128 / 128 | 9,96 | 11,49 | 9,24 | 0,33 | 11,44 |
| Verteilte Ziele/Profile | 128 / 128 | 10,67 | 11,42 | 9,25 | 13,52 | 13,54 |

Nachweise: [Capture-Replay](results/captured-20261027-174927/).
Alle zurückgegebenen kalten und warmen Wege sind kantenweise geprüft; drei feste
Fälle pro Last wurden zusätzlich unabhängig optimal geprüft und alle mit Precise
verglichen. Kosten-/Statusfehler: 0. Gleich teure Richtungsabweichungen sind häufig,
beim gemeinsamen Ziel beispielsweise 6.760; das ist keine akzeptierte Wegtreue.
Die Eingabenerzeugung fand in diesem Capture **keine geeignete reale Last mit
Grabenpflicht**. Deren Bericht bleibt `INCOMPLETE`, und das Capture-Werkzeug beendet
sich deshalb mit Exitcode 2. Synthetische Grabenfälle ersetzen diesen Nachweis nicht.

### Bewertung und nächster Ingame-Test

Der Neuaufbau beseitigt den vorherigen 256-MiB-Abbruch der großen offenen Lasten und
liefert erstmals umfangreiche positive Wege auf dem aufgenommenen Graphen. Er ist
als weitere Testbasis brauchbar. **Das Entwicklungsziel ist noch nicht erreicht:**
Eine 100-fache Verbesserung gegenüber Precise ist nicht nachgewiesen. Bei 10.000
langen synthetischen Wegen ergeben Aufbau, kalter Batch und zusätzliche Prüfung
konservativ etwa **161 ms auf Boden und 218 ms mit Grabenpflicht**. Die kalte Graben-
Berechnung alleine überschreitet 100 ms. Unterschiedliche Ziele sind im Capture
nicht deutlich schneller als Precise. Vorbereitung, Aktualisierung und vollständige
Wegausgabe dürfen daher nicht aus der Gesamtbewertung verschwinden.

Für die nächste Architekturentscheidung sind echte Grabenquerungen unter den
veröffentlichten Zugriffsregeln, größere zusammenhängende Formationen, viele eigene
Ziele und Profile, Änderungen unter Belagerung sowie längerer Dauerbetrieb erforderlich.
Ressourcenbedingt nötige Mehrzustandssuchen und vollständige Wegpuffer pro Einheit
bleiben potenziell teuer. Ein späterer Verbraucher sollte gemeinsame vorbereitete
nächste Schritte konsumieren; diese Bewegungsanbindung ist noch nicht implementiert.

**Für 0.2.0 liegt noch kein Ingame-Lauf unter Unity/Mono vor.** Nach Installation eine
reguläre Karte oder einen Save laden, vorzugsweise die bisherige Stresskarte und
zusätzlich eine Karte mit freundlichem Graben samt erreichbarer Gegenseite. Der Test
startet automatisch, speichert zwei Rohsnapshots im Abstand von mindestens zehn
Sekunden und führt zuerst die Änderungsvergleiche aus. Für die Gruppenläufe einige
Minuten offen lassen; 120 Sekunden pro Snapshot und 15 Sekunden pro Einzellast
bleiben diagnostische Grenzen. Beenden oder Kartenwechsel ergibt gegebenenfalls
einen unvollständigen Lauf. Bestehende Bewegung wird durch diesen Mod nicht ersetzt.

Das geprüfte **0.2.0-/devdiag-Paket ist installiert**. Vier Paketdateien wurden per
Hash bestätigt; alle 282 installierten Abhängigkeitsdateien sind davor und danach
unverändert. Das Spiel wurde nicht gestartet. Installationsnachweis:
[.local/installed.json](.local/installed.json).

## CCH-Entscheidungstor vom 27.10.2026: Stopp vor Runtime-Integration

### Bezug und Umsetzung

Der anschließende Auftrag erlaubt bis zu fünf Minuten Hintergrundvorbereitung und
bei ausreichend freiem RAM bis zu 1 GiB. Er verlangt vor weiterer Runtime-Integration
ein Offline-Entscheidungstor. Dieses wurde mit einem eigenen C++-CCH-Kandidaten
umgesetzt. Die öffentliche Bereitschafts-API und der dauerhafte Live-Dienst sind
**noch nicht implementiert**, weil der Kandidat dieses Tor nicht besteht.

Der Kern bereitet die volle Potenzialtopologie von 640.000 Koordinaten mit acht
Nachbarn, geometrischer Nested Dissection und symbolischer Elimination vor.
Die gerichteten aktuellen Kanten erhalten Kosten 1 oder unendlich. Teilwege werden
über Shortcut-Zeugen rekonstruiert. Es gibt keine vollständige Start-Ziel-Matrix.
Die abschließende Fassung enthält gemeinsame Rückwärtsfelder, wiederverwendbare
Zielsuchdaten und exakte gecachte Vorwärtssuchdaten identischer Starts. Stabile
Anfrage-IDs erhalten nach interner Umordnung die Ergebniszuordnung.

Compiler: MSVC 14.41.34120, Windows SDK 10.0.22621.0, C++17, optimiert, statische CRT.
Ausführung: .NET 10 plus eigene C++-Probe, **kein neuer Unity-/Mono-Lauf**.
Die Bodenmetrik ist einheitlich; unterschiedliche Kostenprofile und Precise-
Ressourcenbedingungen gehören nicht zur Freigabe dieses Kandidaten. Deren bisherige
Tests wurden weder entfernt noch gelockert.

### Fachliche Nachweise

- **12.408 unabhängige Vertragsvergleiche** bestehen: gerichtete Zufallsgraphen,
  Nested-Dissection-Grenzen, ungültige Koordinaten, isolierte Ziele, gerichtete
  schmale Karten, lange Wege und die 2.000-Schritt-Grenze, kompakte Präfixe,
  Ziel-/Startwiederverwendung sowie erneute Metrikberechnung.
- Eine neue Öffnung außerhalb des bisherigen Weges senkt die Kosten. Aktualisierte
  Antworten stimmen mit Vollsuche und unabhängig vorbereitetem neuen Index überein.
  Die symbolische Topologie bleibt erhalten. Das prüft vollständige neue
  Customization, **noch keine inkrementelle Reparatur**.
- Auf dem gespeicherten realen Graphen wurden **42.560 Anfragen** vollständig
  beantwortet, alle mit gefundenem Weg. Alle vollständigen Wege sind kantenweise
  geprüft, kompakte Ergebnisse stimmen mit Kosten und ersten acht Schritten überein.
- Der aktuelle unveränderte Precise-Oracle wurde für sämtliche Anfragen verglichen;
  zusätzlich bestanden **384 unabhängige Vollsuchvergleiche** auf diesem Capture.
  Es wurden keine Kosten-/Statusfehler beobachtet. Das ist kein vollständiger
  unabhängiger Optimalitätsnachweis aller 42.560 Fälle und keine Wegtreue-Abnahme.
- Die bestehende Suite bleibt grün: **39.963 Assertions, 5.938 unabhängige Vergleiche,
  0 Precise-Kosten-/Statusfehler und 196 offene gleich teure Richtungsabweichungen**.
  Die begrenzten passiven Quellprüfungen und scoped Shared-/Lifecycle-/Abhängigkeits-
  Guardrails bestehen ebenfalls.

### Gemessene Skalierung

Maßgebliche Serie: [cch-20261027-191740-311](results/cch-20261027-191740-311/),
[maschinenlesbare Entscheidung](results/cch-20261027-191740-311/decision.json) und
[abschließende Einordnung](results/cch-20261027-191740-311/review.json).
Rohsnapshot, reproduzierbare Eingaben, Native-Quell-/Binärhash, Compiler und
Testhostidentität liegen im selben Ordner. Der Input stammt aus dem 0.2.0-Lauf
`20261027-182413-691-s1`, Snapshot 1, Spieler 1, 317.604 gültige Koordinaten.

Vorbereitung: **27,81 s**, **34.724.180 Shortcut-/Grundkanten** im Index und rund
**4,926 Milliarden** durchlaufene Dreieckskombinationen. Der native Pufferstand
nach Aufbau beträgt **697,16 MiB**, der höchste native Stand während der gesamten
Serie **782,34 MiB**. Zusätzlicher verwalteter Testspeicher ist darin nicht vollständig
erfasst; eine gemeinsame residentenbezogene Speicherabnahme liegt deshalb nicht vor.
Der längste gemessene native Arbeitsabschnitt einschließlich Aufbau lag bei
**49,11 ms**. Die angestrebten 2-ms-Pakete sind noch nicht durchgängig erreicht.

Zeiten je 10.000 Anfragen in ms, nach Indexvorbereitung:

| Last | Kompakte Antwort | Wiederholung | Vollständige Wege | Precise, volle Ausgabe |
|---|---:|---:|---:|---:|
| Gemeinsames Ziel, Feld bereits vorbereitet | 3,96 | 3,51 | 4,25 | 58,25 |
| Zusammenhängende Formationsziele | 6.551,29 | 4.711,31 | 5.111,19 | 487,79 |
| Verteilte individuelle Ziele | 2.444,59 | 2.206,67 | 2.201,38 | 335,73 |
| Kartenübergreifende Start-/Zielpaare | 18.012,98 | 24.895,30 | 24.845,58 | 83.562,77 |

Kompakte Antworten enthalten Kosten und bis zu acht Richtungen, keine öffentlichen
Routenhandles. Die Zeiten enthalten native Such-/Ausgabearbeit und die verwaltete
Batchanpassung. Der Index und zuvor aufgebaute Suchdaten bleiben zwischen den Lasten
erhalten; diese Serie ist nicht für jede Zeile vollständig kalt. Das erste gemeinsame
Zielfeld wurde in der 128er-Last aufgebaut; diese Last dauerte 6,09 ms. Der schnelle
10.000er-Gemeinzielwert ist daher kein kalter CCH-Abfragewert. Die Cachezähler belegen
tatsächliche Feld- und Startdatenwiederverwendung. Lange Fälle enthalten Wege bis
705 Schritte; einzelne kürzere Fälle bleiben in der global verteilten Stichprobe.

Die vorangegangene Fassung ohne diese gemeinsame Vorbereitung scheiterte ebenfalls:
[erste Capture-Serie](results/cch-20261027-190631-797/). Die ursprüngliche offene
800er-Messung in [cch-20261027-190231-365](results/cch-20261027-190231-365/) ist ein
historischer Zwischenstand: Ihre Abfragen verwendeten ausschließlich kantenweise
geprüfte direkte kürzeste Wege, nicht den CCH-Suchgraphen. Sie belegt keine schnelle
CCH-Navigation um Hindernisse. Frühe Exitcodes beschreiben dort nur den damaligen
fachlichen Prüfumfang; die aktuelle CLI gibt bei verfehltem Reaktionsziel Exitcode 2.

Dies sind Einzelmessserien mit erheblichen Laufzeitunterschieden, keine statistisch
abgesicherten Latenzgrenzen. Eine Beschleunigung um Faktor 100 ist nicht nachgewiesen.
Der große Abstand zu 100 ms ist auch ohne eine solche Zusage ein negatives Ergebnis.

### Architekturentscheidung und unveränderte Installation

**Stopp vor Runtime-Integration.** Die breite, hindernisunabhängige Potenzialtopologie
verursacht starke symbolische Auffüllung. Für viele verschiedene Ziele bleiben die
aufsteigenden Suchräume trotz Vorbereitung und Wiederverwendung teuer. Gemeinsame
Ziele profitieren vom Rückwärtsfeld; dessen schnelle Antworten benötigen den großen
CCH-Index nicht. Zusätzlicher RAM oder längeres Warten beim Laden beheben diesen
Unterschied nicht. Die allgemeine CCH-Verfahrensfamilie wird damit nicht widerlegt;
geometrische Ordnung, Topologie und Abfrageverfahren sind konkrete Grenzen dieses
Versuchs. Ein anderes Pruning oder eine andere Verdichtung ist nicht automatisch
als bereits geprüft anzusehen.

Die im Plan folgenden Schritte sind damit ausdrücklich offen: öffentlicher
Bereitschaftsvertrag, asynchroner dauerhafter Dienst, automatische RAM-Auswahl,
Aufholen laufender Änderungen, partielle CCH-Reparatur, Runtime-Generationen,
Routenhandles und die Ingame-Abnahme. Der Bereitschaftsdienst wird gemäß dem vorgeschalteten Entscheidungstor erst nach
einem tragfähigen Suchkern-Nachweis weiter umgesetzt.

Nur Offline-Probe, Adapter, gezielte Tests und Dokumentation wurden ergänzt. Es gab
keinen neuen Runtime-Build, keinen Versionssprung und keine Installation. Die vier
installierten Paketdateien von **0.2.0** und 17 bisherige Runtime-/Native-Quellinputs
wurden gegen den alten Buildnachweis unverändert bestätigt. Das Spiel wurde nicht
gestartet und keine Konfiguration wurde geändert. Gegen den historischen 0.2.0-
Abhängigkeitsnachweis sind acht APIShared-Dateihashes inzwischen anders; diese Arbeit
hat keine Abhängigkeit gebaut, installiert oder zurückgesetzt. Der Abgleich ist als
Momentaufnahme in `review.json` festgehalten und kein behaupteter Vorher-/Nachher-
Nachweis dieses Tasks.

Für die Einordnung des vorhandenen 0.2.0-Pakets bleibt der zwischenzeitliche echte
Unity-/Mono-Lauf um 19:24 Uhr maßgeblich: zwei gespeicherte Snapshots, insgesamt
64.038 positive Anfragen, keine dort gemeldeten Vergleichsfehler, 925 geänderte
Graphkoordinaten und acht übereinstimmende Änderungsantworten. Die reale Grabenlast
war unvollständig; das blockiert die Bodenbewertung nicht. Auch dort verfehlten
viele unterschiedliche Ziele das Reaktionsziel. Der ältere Satz „noch kein Ingame-
Lauf von 0.2.0“ beschreibt den Zeitpunkt vor diesem späteren Lauf.
### Anschließende Nutzerentscheidung: Grundsystem und API zuerst

Nach Sichtung des negativen CCH-Tores wurde ausdrücklich entschieden, zuerst den
dauerhaften Dienst und die Bereitschafts-API auf dem vorhandenen korrekten Suchkern
aufzubauen. Der Suchkern wird austauschbar gehalten; seine unzureichende Performance
bleibt offen und wird getrennt von funktionaler Bereitschaft ausgewiesen. Der CCH-
Kandidat bleibt ausschließlich als dokumentierter Offline-Versuch erhalten. Der
oben dokumentierte Stopp beschreibt den Zustand vor dieser anschließenden Entscheidung.
Die Umsetzung des residenten Grundsystems wird jetzt fortgesetzt.

## 27.10.2026: Dauerhafter Dienst und öffentliche Test-API, Version 0.3.0

Die anschließende ausdrückliche Nutzerentscheidung wurde umgesetzt: Grundsystem und
API zuerst, Austausch des zu langsamen Suchkerns später. Der CCH-Offlineversuch wird
nicht in die Runtime integriert. Der resident verwendete Flächen-/Feldkern bleibt
algorithmisch unverändert; sein privates ABI 4 ergänzt nur die Speicher-Spitzenmessung.

### Funktionaler Stand und Grenzen

- Prozessweit verwurzelter Dienst mit einem Hintergrundworker; Vorbereitung des
  lokalen Spielers automatisch, weitere Perspektiven auf Anfrage. Kontextfreigabe
  pro Sitzung/Generation; nur vollständig gleiche Graphen teilen einen Backendkontext.
- Öffentliche experimentelle API für Bereitschaft, asynchrone Batches bis 10.000,
  stabile Anfrage-IDs, Kosten, Schrittzahl, erste acht Richtungen und Routenhandles.
  Weitere Schritte/volle Wege, Abbruch, Freigabe und veraltete Handles sind definiert.
  Dokumentation: [API.md](API.md), Betrieb: [PROTOTYPE.md](PROTOTYPE.md).
- Kohärente Aufnahmen im Abstand von 500 ms; öffentliche Änderungsereignisse liefern
  zusätzliche Hinweise. Neuere Aufnahmen werden während Arbeit zusammengefasst.
  Unveränderte Graphen behalten Generation und Felder. Änderungen ersetzen derzeit
  den gesamten betroffenen Kontext; keine inkrementelle Reparatur behauptet.
- Initialisierung bis 300 s, Auftrag einschließlich Warteschlange bis 120 s;
  konservatives gemeinsames Speicherbudget bis 1 GiB beziehungsweise ein Viertel
  freien physischen RAM. Fallback 256 MiB, Ablehnung bei weniger als 256 MiB.
- `Ready` belegt nur den vollständig vorbereiteten aufgenommenen Stand.
  `PerformanceAccepted=false` bleibt fest. Normale Builds enthalten die API, melden
  jedoch `Unsupported`; Dienst/Erfassung/native Diagnose-DLL fehlen dort vollständig.
- Kein Verbraucher, kein nativer Spielhook, keine Bewegung, keine Konfigurationsänderung.
  Bodenwege verwenden gleiche Schrittkosten; Spezialregeln bleiben außerhalb der API.

Die API überträgt kompakte Präfixe, der vorhandene Adapter erzeugt intern aber bereits
volle Wege. Zusätzliche volle Ausgabe misst Kopieren, keine aufgeschobene Entfaltung.
Native Suchpakete werden begrenzt; Kontextanlage/-ersetzung bleibt ein zusammenhängender
nativer Aufruf. Ein harter 2-ms-Nachweis für sämtliche Workerabschnitte fehlt.

### Offline-Nachweise

Die aktuelle Standardtestsuite besteht mit **40.214 Assertions**, **5.938 unabhängigen
Suchvergleichen**, keinen Kosten-/Statusabweichungen zum aktuellen Precise und
**196 gleich teuren Richtungsabweichungen**. Diese Zahl gehört zur kleinen
Standardtestsuite; die größere Replay-Serie zählt Abweichungen gesondert.

Gezielt geprüft sind nicht bereite Kontexte, unabhängige Spielerperspektiven,
Eingabekopien, unveränderliche Antworten, Wegabschnitte, volle Wege, wiederholte
Freigabe ohne Speicherakkumulation, begrenzte Warteschlange, Freigabe wartender
Closures, Abbruch, Wartedeadline, Öffnungen/Sperren, neue Generationen, Nachholen
während Initialisierung, Kartenwechsel während Aufbau, Fehler und Wiederanlauf.
Ein unabhängig kompilierter Verbraucher verwendet dieselbe öffentliche API auf einem
echten Hintergrundworker. Separate Framework-Prozesse prüfen Datei- und tatsächliche
Byte-Ladung der installierten Spielassembly mit leerer `Location`, Engine-IL,
verwaltete Layouts/Enums, periodische Erfassung, fehlerhafte Tor-Snapshots,
Schreibfehler und nachfolgenden Sitzungsstart. Native **Spielmethoden** werden dabei
nicht aufgerufen. Ein dauerhaft null meldender Allokationszähler gilt als unverfügbar.

### 800×800-Replay auf dem residenten Dienst

Nachweis: [result.json](results/resident-20261027-201635-923/result.json), passende
Rohaufnahmen, neun reproduzierbare `.bpf.gz`-Lasten, `progress.json`, Native-Buildproof,
Hostidentität sowie archivierter Replay-Host und eigener nativer Backend-Binärstand
im selben Ergebnisordner. Laufzeit: **.NET 10.0.11**, kein Unity-/Mono-Lauf.

Vorbereitung **64,03 ms** einschließlich eigener Kopien/Graphprojektion und Kontextanlage;
925 tatsächlich geänderte Graphkoordinaten, Aktualisierung **59,03 ms** und **128**
übereinstimmende Antworten gegen vollständigen unabhängigen Kontextneuaufbau.
Konservativer Speicherhöchststand **210,89 MiB** bei explizitem 1-GiB-Testbudget;
keine am Schluss behaltenen Client-/Routenleases. Die Rohaufnahmen stammen aus dem
historischen 0.2.0-Spieltest, nicht aus einem neuen 0.3.0-Ingame-Lauf.

| Last, jeweils 10.000 | API-Berechnung ms | Wiederholung ms | Zusätzliche volle Ausgabe ms | Aktueller Precise ms |
|---|---:|---:|---:|---:|
| Gemeinsames Ziel | 26,00 | 15,19 | 2,94 | 52,39 |
| Zusammenhängende Formationsziele | 857,35 | 1.240,59 | 1,15 | 478,25 |
| Verteilte individuelle Ziele | 1.007,79 | 1.000,18 | 8,01 | 486,86 |

Die 128er-/512er-Lasten stehen vollständig in `result.json`. Insgesamt **31.920**
erste Antworten plus ebenso viele Wiederholungsantworten; alle ersten Antworten
wurden gegen aktuellen unveränderten Precise geprüft, **27** zusätzlich gegen
unabhängige Vollsuche. Jede erste volle API-Wegausgabe wurde kantenweise validiert.
Alle diese Eingaben waren erreichbar; Negativfälle sind separat in den kleinen
Vertragstests vorhanden. Auch diese Lasten repräsentieren erzeugte regionale
Anfragen und keine Aufzeichnung von 10.000 verschiedenen Einheitenbefehlen.
Kartenübergreifende Dauerlast ist damit nicht neu nachgewiesen.

Die Lasten laufen auf demselben residenten Index in Serie. Das gemeinsame Zielfeld
wurde bereits in der ersten 128er-Last aufgebaut; der 10.000er-Wert ist deshalb
**kein vollständig kalter Indexwert**. Beide Systeme dürfen vorhandene Wiederverwendung
nutzen; Precise erhält pro Last einen eigenen aktuellen Oracle. API-Zeiten enthalten
Backend, Validierung, vollständige interne Wegerzeugung und Ergebnisaufbau; Queuezeit
ist separat erfasst. Referenz-Stichproben liegen außerhalb der Precise-Zeit.
Die verteilte Last verwendet für diesen API-Vertrag ausschließlich Schrittkosten 1,
keine unterschiedlichen Profile. Reproduzierbare gleich teure Richtungsabweichungen
werden je Last gezählt; daraus folgt keine Freigabe ihrer Wegtreue für Verbraucher.

Maximales gemessenes natives Suchpaket **2,66 ms**, angestrebt 2 ms. Die Serie ist
eine Einzelmessung, keine Latenzgarantie. Die Formations-/Einzelziellasten verfehlen
100 ms deutlich und sind hier sogar langsamer als Precise. **Performance-Abnahme
negativ; Faktor 100 nicht nachgewiesen.** Das funktionale Grundsystem wird dadurch
nicht als erfolgreicher neuer Suchkern ausgegeben.

### Nächster Runtime-Nachweis

Beim nächsten regulären Karten-/Save-/Editorstart soll der Dienst initialisieren,
32 reproduzierbare API-Proben beantworten und bei weiteren Aufnahmen Änderungen
verarbeiten. `resident.json/.md`, `resident-ready.json`, `api-requests.json`,
`api-probe.bpf.gz`, `api-probe.json` und zwei frühe Rohaufnahmen werden erwartet.
Der Dienst läuft danach weiter; die Speicherung ganzer Rohkarten bleibt auf zwei
begrenzt. Fehlende oder fehlgeschlagene Phasen gelten nicht als bestanden.

Offen bleiben Unity/Mono 0.3.0, fünfminütige Initialisierung unter repräsentativen
Stressbedingungen, Main-Thread-/Framekosten der 500-ms-Erfassung, Änderungen während
Belagerungen und Cache-/Mehrspieler-Dauerlast. Die spätere Suchkernentwicklung muss
insbesondere viele unterschiedliche Ziele günstiger machen. Freundliche Gräben und
andere Spezialfälle bleiben eigene Ausbaustufen und blockieren diesen Bodenversuch
nicht. Build- und Installationsnachweise werden nach den abschließenden Prüfungen
in `.local/build-*.json` beziehungsweise `.local/installed.json` festgehalten.

### Abschluss- und Übergabestand: auf Nutzerwunsch keine Installation

Normal- und Diagnose-Build von **0.3.0** bestanden, zuletzt erneut gegen die zu diesem
Zeitpunkt installierte APIShared **0.18.0**. Nachweise: `.local/normal-current-sdk.log`,
`.local/diagnostic-current-sdk.log`, datierte `.local/build-*.json` und
[maschinenlesbarer Übergabestand](.local/handoff-0.3.0.json). Die aktuelle Suite
besteht mit **40.214 Assertions**; der separat wiederholte CCH-Vertragslauf bestand
mit 12.408 unabhängigen Vergleichen. Die normale Assembly enthält eine explizit
inaktive API, die Diagnose-Assembly den residenten Dienst; beide Binärgrenzen und
getrennt kompilierte öffentliche Verbraucher wurden geprüft.

Die erste Framework-Verbraucherprüfung kollidierte mit den Net10-Zwischendateien
im gemeinsamen `obj`-Verzeichnis. Getrennte Framework-Zwischenpfade beheben dies;
die danach erfolgreichen Prüfungen sind in den Buildlogs enthalten. Das RAM-Budget
wird bei jedem abgeschlossenen Kartenstart neu gewählt. Im Editor ohne gültigen
lokalen Spieler wird ausdrücklich Diagnoseperspektive 1 vorbereitet.

APIShared wird parallel durch einen anderen Chat bearbeitet. Nach beiden zunächst
bestandenen Buildständen änderten sich installierte Abhängigkeitshashes erneut,
auch die APIShared-DLL. Zwei Installationsversuche stoppten **vor dem Kopieren**.
Diese Arbeit hat keine SDK-Datei gebaut, installiert oder zurückgesetzt. Die
Buildprüfung kontrolliert Abhängigkeiten jetzt zusätzlich nach der Binärprüfung
und vor ihrem Abschluss. Drift-Nachweise bleiben in `.local/dependency-drift-*.json`.

Der Nutzer entschied anschließend ausdrücklich: **aktuell keine Installation und
später weiterarbeiten**. Deshalb wurde 0.3.0 nicht installiert, das Spiel nicht
gestartet und keine Konfiguration geändert. Beim abschließenden Leseabgleich war
der historische Zielordner `BepInEx/plugins/BetterPathfindingTest_Serp` nicht mehr
vorhanden; der alte `.local/installed.json`-Nachweis für 0.2.0 ist damit ausdrücklich
historisch und keine Bestätigung der aktuellen Installation. Diese Arbeit hat
keinen installierten Testmod entfernt.

Für jetzt ist **kein neuer Ingame-Test nötig**. Beim Fortsetzen zunächst den dann
aktuellen installierten SDK-Stand abgleichen, betroffene Prüfungen/Build erneuern
und erst nach Installation den residenten Dienst unter Unity/Mono beobachten:
Bereitschaft, 32er-Probe über die öffentliche API, fortlaufende Aufnahmen, echte
Bau-/Abrissänderungen und Kartenwechsel. Ergebnisse bleiben im Testmod-Ordner;
eine zusätzliche README wurde auf Nutzerwunsch nicht angelegt. Die dokumentierten
negativen Performance-Ergebnisse und offenen Richtungsabweichungen bleiben bestehen.
