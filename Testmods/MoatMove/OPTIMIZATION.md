# MoatMove: Precise-Optimierung und Fast-Konfiguration

Stand: 2026-09-13, Testversion weiterhin 0.1.0. BugfixesAndQoL wurde nicht verändert.

## Auswahl

Die Installation legt bei Bedarf `BepInEx\config\MoatMove_Serp.cfg` im Spielordner an. Eine vorhandene Datei bleibt erhalten. Einstellung:

```ini
[Movement]
Mode = precise
```

Für Fast `Mode = fast` setzen und das Spiel neu starten. Ohne Einstellung gilt `precise`. Die Konfiguration wird einmal gelesen; laufende Befehle behalten unveränderliche Optionen. Im Mehrspieler müssen alle Teilnehmer denselben Build und Modus verwenden. Keine Lobby-/APIShared-Abhängigkeit; APIShared bleibt zulässig. BugfixesAndQoL und EnemyGatePathfindingTest werden weiterhin als konkurrierende Hookbesitzer abgewiesen.

Precise erhält die bisherige gewichtete Auswahl einschließlich Besitzern, Verbündeten, Diagonalregeln, 40-Tick-Sicherheitsabstand, plausiblen Geschwindigkeitsprofilen und nativer Pufferprüfung. Fast aktiviert den bereits physisch kopierten RequiredOnly-Zweig aus BugfixesAndQoL: Bodenalternativen haben Vorrang, Grabenwege werden ohne zusätzliche Grabenkosten ermittelt, Gruppen nutzen gemeinsame Entscheidungen/Felder. Dessen bestehende Begrenzung auf 16384 Suchknoten bleibt erhalten; unbekannte Erreichbarkeit und erschöpfte Budgets erzeugen keine teure unbeschränkte Ersatzsuche. Verfüllverbesserung, Leiterfix und unabhängige Formationserweiterungen bleiben in beiden Modi aus.

## Optimierung

Die teure Übergangsprüfung wird innerhalb der bereits vorhandenen Gültigkeitsgrenzen zwischengespeichert. Ein 32-Bit-Wort enthält acht gerichtete Übergänge mit jeweils vier Bits: bekannt, erlaubt, Graben und Struktur. Die Rückwärtssuche liest damit ihre acht eingehenden Übergänge aus einem Wort. Unbekannte Übergänge verwenden unverändert den bisherigen vollständigen Prädikatcode.

Die 1024-Knoten-Seiten werden erst bei Bedarf angelegt. Maximaler zusätzlicher Nutzdatenspeicher: 2.56 MB pro vollständig erkundetem 800x800-Kern; bei sechs Kernen insgesamt 15.36 MB zuzüglich kleiner Seitenverwaltung. Keine unbeschränkt wachsende Ergebnissammlung. Invalidate verwirft die Gültigkeit zusammen mit den bisherigen Suchfeldern. Präzise Befehle bleiben an Sitzung, Spieler, Kartenepoche und Tick gebunden; der vorhandene Fast-Cursor verwendet seine Strukturrevision. Verschachtelte Bewegungen und native Strukturänderungen erreichen weiterhin die vorhandene Invalidierung.

Koordinaten werden pro expandiertem Knoten und Ziel berechnet, statt dieselben Divisionen für jeden Nachbarn zu wiederholen. Der Runtime-Build verwendet nun Compileroptimierung bei erhaltenen Debugsymbolen.

Suchreihenfolge, Kosten, Prioritäten, Tie-Breaks, Ressourcenbedingungen, Suchbudgets und Routenpublikation bleiben gleich. Pfade werden vor der Veröffentlichung weiterhin unabhängig anhand der aktuellen nativen Daten geprüft; der neue Cache ersetzt diese Prüfung nicht. Von den 22 kopierten Runtime-Dateien wurde ausschließlich MoatSearchKernel.cs verändert. Plugin/Optionen ergänzen die Konfiguration. SOURCE_PROVENANCE.json bleibt der Herkunftsnachweis der Vergleichskopie; OPTIMIZATION_PROVENANCE.json bindet den optimierten Kernelhash.

## Native Entscheidung

Der featurebezogene Audit liegt unter `_inspect/MoatMove/NativeAudit/PROGRESS.md`, mit hashgebundenen Belegen für 126 Funktionen. Installierte DLL: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Script Extender: 2.5.0, Commit 5f02af6d074af7c741ebdaaccb48add39eba1bf4.

Die Torhauslösung entfernt Übergänge durch ein Byte-AND. Sie liefert weder die Precise-Gewichtung noch die Besitzerregeln des Grabenmodus. Außerdem verlangt Vanillas DAFD0 beim diagonalen Eintritt in einen Graben in einem Fallbackzweig beide orthogonalen Grabenecken; die akzeptierte Precise-Version lässt eine Ecke genügen. Eine unveränderte Vanilla-Suche würde dadurch Verhalten verlieren. Native Distanz-/Besuchsfelder werden auch von Grabenarbeit und Platzierung überschrieben. Sie werden deshalb nicht als dauerhafter gemeinsamer Routenpuffer umfunktioniert.

Diese Fassung enthält weiterhin eigene Suchen. Es wurden keine neuen nativen Adapter installiert, keine PCLs erzeugt und keine globalen Richtungsgrids verändert. Eine pauschale Aussage, dass jede denkbare native Optimierung ausgeschlossen sei, folgt aus diesem Audit nicht.

## Prüfung und Messung

Vollständige Quellkompilierungsprüfung gegen installierten Extender, 224452 Runtime-Assertions, 84031 Such-Assertions und 1469340 Cursorvergleiche bestanden. Zusätzlich: 3600 zufällige gerichtete Anfragen gegen den tatsächlichen unveränderten Precise-Kernel aus BugfixesAndQoL, einschließlich Mehrprofilgrenzen, Strukturfiltern und Invalidierung. Erfolgreiche Wege stimmen inklusive Tie-Breaks exakt überein. Fast-Budgets und Ergebnisse wurden vor/nach Terrainänderungen und über mehrere Cache-Seiten mit dem Original verglichen.

Der Benchmark kompiliert auch den unveränderten originalen WeightedMoatRoutePlanner gegen den originalen Kernel. Seine kodierten Kandidaten stimmen bytegenau überein. Reproduzierbarer Kontrolllauf ohne dynamisches .NET-Tiering (`DOTNET_TieredCompilation=0`):

| Planner-Anfragen | Original | Optimiert | Reduktion |
|---|---:|---:|---:|
| 120 | 206.76 ms | 130.69 ms | 36.8 % |
| 680 | 1172.72 ms | 737.65 ms | 37.1 % |

Im gerichteten Kernel-Gruppentest mit 680 Anfragen werden 70665 Übergänge ausgewertet und 41016517 erneute Prüfungen eingespart. Die Zahl der expandierten Knoten ist absichtlich identisch. Die Laufzeiten sind synthetische .NET-Messungen, keine gemessenen Spiel-FPS oder Mono-Garantien. Sehr große Precise-Aufträge können weiterhin teuer sein. Der Fast-Modus bleibt die stärkere Vereinfachung.

Logs: `_inspect/MoatMove/optimization-tests.log` und `optimization-tests-no-tiering.log`. Quellvergleich, JSON-/Lifecycle-/CRLF-Präflight, installierte RedBird-Dekodierung, 30 native Funktionsziele, Inline-Spanne/Rücksprung sowie Byte-/Call-Verträge bestanden. Die bekannten Compiler-Referenzwarnungen CS1701/CS1702 sind im Prüfprotokoll enthalten. Native Hooks und deren Maschinenverträge wurden nicht verändert.

Ingame-Abnahme der optimierten Fassung steht aus. Die vorherige unveränderte Vergleichskopie wurde vom Nutzer im Spiel als funktionierend bestätigt; deren neuestes ausgewertetes Log enthielt keine Error/Fatal/Exception-Einträge. Dies ersetzt keinen Spieltest dieser Optimierung.

## Installation

Der erhöhte build.bat /nopause wurde nach den Prüfungen einmal erfolgreich ausgeführt. Installierte Assemblyversion: 0.1.0.0; SHA-256: 28CA1B98136FB156C3F2BCC6A738CBE343252151EA4946FC2F43522C489C503F. Lokale und installierte DLL/PDB/Manifest-Dateien wurden vom Treiber per Hash verglichen. Die fehlende MoatMove_Serp.cfg wurde mit Mode = precise angelegt. Die installierte DLL enthält den geprüften Übergangscache. Modversion und Manifest bleiben konsistent 0.1.0.
