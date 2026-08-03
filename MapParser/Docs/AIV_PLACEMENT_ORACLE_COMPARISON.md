# Chat 10: Offline-/Oracle-Vergleich

Die konkrete Übergabe für die Fortsetzung in einem neuen Chat steht in
`CHAT10_ORACLE_MISMATCH_HANDOFF.md`. Sie enthält die beiden priorisierten
Einzelfälle, direkt ausführbare Befehle, die Diagnose- und Evidenzanforderungen,
die Regressionsreihenfolge und die Stopplinie vor Chat 11.

## Ergebnis

Chat 10 ist nach der Organismusprüfung wieder geöffnet. Die hashgebundene Matrix enthält 144 native
Einzelversuche auf World Sizes 160, 200, 300, 400 und 800. Sie umfasst
Vanilla-Karten, für den Editor angelegte Vanilla-Kopien und zwei mit der
aktuellen DE-Version erzeugte Custom-Maps.

- 97 `ExactMatch`;
- 0 `NotEvaluable`;
- 47 Mismatches;
- 0 Fehler.

Die frühere Annahme eines fehlenden Organismusdatensatzes war zu konservativ.
Die native Skirmish-Initialisierung setzt den relevanten Modus auf `1` oder
`99`; zusammen mit dem festen AIV-Spielerwert `0` akzeptiert der Validator alle
Organismusklassen. Dadurch verschwanden sämtliche 77 `NotEvaluable`-Fälle. 30
davon stimmen nun zusätzlich exakt, 47 legen zuvor verdeckte Abweichungen in
anderen AIV-, Mapper- oder Placement-Regeln offen. Chat 11 darf erst beginnen,
wenn diese Abweichungen klassifiziert und behoben oder eng begründet sind.

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
| `Height Advantage` | Vanilla-Kopie / 160 | 10 | 2 | 0 | 8 / 0 | 171,0 ms |
| `Bow Ridge` | Vanilla-Kopie / 200 | 12 | 4 | 0 | 8 / 0 | 185,3 ms |
| `Thasos` | Vanilla-Kopie, Section-1190-Anomalie / 300 | 22 | 6 | 0 | 16 / 0 | 194,6 ms |
| `A Friend Indeed` | Vanilla-Kopie / 400 | 14 | 12 | 0 | 2 / 0 | 180,5 ms |
| `Province of Bodrum OP` | Vanilla-Kopie / 400 | 20 | 8 | 0 | 12 / 0 | 209,1 ms |
| `unittest` | aktuelle Custom-Map / 160 | 58 | 58 | 0 | 0 / 0 | 238,5 ms |
| `testmap` | aktuelle Custom-Map / 800 | 4 | 4 | 0 | 0 / 0 | 123,2 ms |
| `Marshy Mayhem` | ältere Kontrollstichprobe / 400 | 4 | 3 | 0 | 1 / 0 | 101,6 ms |

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

## Sperre vor Chat 11

Die Laufzeitmatrix bestätigt, dass `playerId - 1` kein verlässlicher
Map-Keep-Slot ist. Derselbe `playerId=2` wurde durch Positionswechsel auf
mehreren Karten unterschiedlichen Keep-Slots zugeordnet. Chat 11 muss deshalb
die in der Lobby ausgewählte Position explizit erfassen.

Pfad und Hash der geladenen Karte, exakte Keep-Anker aus 1013/4013,
Lord-/AIV-Listen und AIV-Hashes sind vorhanden. Der Offline-Kern unterstützt
alle acht im installierten offiziellen Kartenbestand belegten World Sizes 160,
200, 300, 400, 500, 600, 700 und 800. Die native Oracle-Matrix dieses Chats
enthält davon 160, 200, 300, 400 und 800. Die Organismusdaten sind für den
Skirmish-AIV-Aufruf vollständig geklärt und werden nicht benötigt. Die 47 nun
sichtbaren Mismatches können jedoch eine Lobbyentscheidung verfälschen; deshalb
bleibt Chat 10 der nächste Schritt.

Auf Thasos erfasste der Oracle alle sechs KI-Auswahlen, aber nur fünf erreichten
den späteren `PrepareLayout`-Callback. Chat 11 muss Prüfaufträge daher für alle
belegten KI-Slots aus den Lobbydaten erzeugen und darf ihre Existenz nicht von
einem erfolgreichen Layout-Callback abhängig machen.
