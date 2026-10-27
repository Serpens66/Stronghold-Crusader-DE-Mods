# Ergebnisstand vom 26.10.2026

Aktueller Stand: siehe den Nachtrag **Neuaufbau 0.2.0 vom 27.10.2026** weiter unten.
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
