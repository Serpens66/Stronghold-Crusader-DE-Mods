# Chat 10: Offline-/Oracle-Vergleich

## Ergebnis

Chat 10 ist abgenommen. Die hashgebundene Matrix enthält 144 native
Einzelversuche auf World Sizes 160, 200, 300, 400 und 800. Sie umfasst
Vanilla-Karten, für den Editor angelegte Vanilla-Kopien und zwei mit der
aktuellen DE-Version erzeugte Custom-Maps.

- 67 `ExactMatch`;
- 77 begründete `NotEvaluable`;
- 0 Mismatches;
- 0 Fehler.

Alle 67 offline auswertbaren Fälle stimmen in Status, sequenziellem Score,
Fit-Prozent, ausgewerteten Zellen und blockierten Zellen exakt mit dem nativen
Oracle überein. Die 77 übrigen Fälle werden nicht als Match gezählt. Sie sind
ausschließlich wegen der bereits dokumentierten, nicht in der Map
serialisierten lebenden Organismusklasse `NotEvaluable`.

## Reproduzierbarkeit

Binärbindung:

- `CrusaderDE.dll`: SHA-256
  `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- bevorzugte Image Base: `0x180000000`

Die Sechser-Matrix vom 2026-08-03 ist an den Quelllog-Hash
`779E0C83F5C5FBF82DAB0CEBE524E308CB0CE54581ED6FCB9D726F9FC816DE48`
gebunden. Der zusätzliche 800er-Lauf ist an
`7E83B9625C944426C9ABC61212F748CE2E02A5EB14FD97187D38CCE875F8C984`
gebunden; `testmap.map` besitzt den SHA-256
`E9B22012A24007232AF519BAEBB7F4A11D4FBF5E96C205D1E5B3A7F7171A4659`.

`AIVPlacement.OracleComparison` prüft Map- und AIV-Hashes vor jeder
Auswertung. Es ordnet den Keep über die exakte Gebäudeobjekt-Koordinate zu und
nimmt ausdrücklich nicht `playerId - 1` als Map-Slot an. Corpusläufe melden
Fallzahl, Fortschritt, verstrichene Zeit und ETA mit Millisekunden-Zeitstempel.
Ein Mismatch oder Fehler führt zu einem Exitcode ungleich null.

## Durch den Vergleich gefundene Korrekturen

Der Oracle-Vergleich deckte vier systematische Modelllücken auf:

1. `LoadCandidate` an RVA `0x54590` erzeugt für Keep-Mapper `60..64` eine
   5×5-, eine 7×7- und drei einzelne Verbindungsflächen. Die vorherige einzelne
   5×5-Editorfläche war unvollständig.
2. Derselbe Loader erzeugt zusätzliche Yard-Flächen für Mapper `79`, `86`,
   `87`, `88` und `89`.
3. Aktuelle Karten speichern Gebäudeobjekte in Section `4013` als 4000
   Datensätze zu `0x32C` Byte. Das Feldlayout ist identisch zu den 2000
   Datensätzen der älteren Section `1013`. Dadurch sind unter anderem
   `unittest`, `testmap` und alle sieben Thasos-Slots exakt auflösbar.
4. `ApplyRotation` an RVA `0x558E0` rotiert die beiden temporären 100×100-Grids,
   lässt den für Orientierung 0 gesetzten Weltursprung aber unverändert. Der
   Fit rotiert daher nicht um den AIV-Keep-Marker. Diese Korrektur machte auf
   der randnahen `unittest`-Karte alle 58 Fälle über sämtliche Rotationen exakt.

Die serialisierte Map enthält außerdem bereits die Spieler-Startgebäude und
ihre Occupancy-Wirkung. `AivPreplacementMapState` blendet deshalb ausschließlich
lebende Spieler-Startgebäude aus Section 1013 oder 4013 und die eindeutig
angrenzenden Wall-Owner-Randzellen aus. Der ursprüngliche Snapshot bleibt für
Diagnosen unverändert.

## Abnahmematrix

Die `v_`-Dateien sind unveränderte Vanilla-Karten, die der Benutzer in den
Custom-Ordner kopiert hat, um sie im Editor laden zu können.

| Karte | Art / World Size | Fälle | Exakt | `NotEvaluable` | Mismatch / Fehler | Zeit |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| `Height Advantage` | Vanilla-Kopie / 160 | 10 | 2 | 8 | 0 / 0 | 175,7 ms |
| `Bow Ridge` | Vanilla-Kopie / 200 | 12 | 0 | 12 | 0 / 0 | 174,9 ms |
| `Thasos` | Vanilla-Kopie, Section-1190-Anomalie / 300 | 22 | 0 | 22 | 0 / 0 | 199,5 ms |
| `A Friend Indeed` | Vanilla-Kopie / 400 | 14 | 1 | 13 | 0 / 0 | 190,5 ms |
| `Province of Bodrum OP` | Vanilla-Kopie / 400 | 20 | 0 | 20 | 0 / 0 | 210,2 ms |
| `unittest` | aktuelle Custom-Map / 160 | 58 | 58 | 0 | 0 / 0 | 237,4 ms |
| `testmap` | aktuelle Custom-Map / 800 | 4 | 4 | 0 | 0 / 0 | 126,8 ms |
| `Marshy Mayhem` | ältere Kontrollstichprobe / 400 | 4 | 2 | 2 | 0 / 0 | 103,2 ms |

Die Matrix deckt mehrere Keep-Slots, kleine bis große AIVs, alle vier
Rotationen und die nativen Zustände `Complete`, `Partial` und `Rejected` ab.
`unittest` ist absichtlich klein und randnah; `testmap` ist leer, 800×800 groß
und besitzt zwei frei platzierte Starts. Zusammen belegen sie sowohl den
maximalen Größenfall als auch empfindliche Rand- und Rotationsfälle.

Die maschinenlesbaren Quellen und Berichte liegen unter:

- `AIVPlacement/OracleCorpus/MarshyMayhem-2026-08-02.json`
- `AIVPlacement/OracleCorpus/Captured-2026-08-03/`
- `AIVPlacement/OracleCorpus/Captured-2026-08-03-800/`
- `AIVPlacement/OracleCorpus/Results/`

## Vorbereitung für Chat 11

Die Laufzeitmatrix bestätigt, dass `playerId - 1` kein verlässlicher
Map-Keep-Slot ist. Derselbe `playerId=2` wurde durch Positionswechsel auf
mehreren Karten unterschiedlichen Keep-Slots zugeordnet. Chat 11 muss deshalb
die in der Lobby ausgewählte Position explizit erfassen.

Pfad und Hash der geladenen Karte, exakte Keep-Anker aus 1013/4013,
Lord-/AIV-Listen und AIV-Hashes sind vorhanden. Der Offline-Kern unterstützt
die belegten World Sizes bis einschließlich 800 und liefert bei der einzigen
verbleibenden nicht serialisierten Regel sicher `NotEvaluable`. Es bestehen
keine ungeklärten Mismatches mehr, die eine Lobbyentscheidung verfälschen.

Auf Thasos erfasste der Oracle alle sechs KI-Auswahlen, aber nur fünf erreichten
den späteren `PrepareLayout`-Callback. Chat 11 muss Prüfaufträge daher für alle
belegten KI-Slots aus den Lobbydaten erzeugen und darf ihre Existenz nicht von
einem erfolgreichen Layout-Callback abhängig machen.
