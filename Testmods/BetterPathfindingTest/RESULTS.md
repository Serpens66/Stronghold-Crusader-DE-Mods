# Ergebnisstand vom 26.10.2026

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
