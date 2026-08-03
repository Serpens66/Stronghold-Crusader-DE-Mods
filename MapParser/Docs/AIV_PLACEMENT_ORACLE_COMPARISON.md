# Chat 10: Offline-/Oracle-Vergleich

Die konkrete Übergabe für die Fortsetzung in einem neuen Chat steht in
`CHAT10_ORACLE_MISMATCH_HANDOFF.md`. Sie enthält die beiden priorisierten
Einzelfälle, direkt ausführbare Befehle, die Diagnose- und Evidenzanforderungen,
die Regressionsreihenfolge und die Stopplinie vor Chat 11.

## Ergebnis

> Hinweis zur Vergleichsbasis: Die unten stehende 144-Fall-Matrix entstand,
> bevor `advopt_pre_build` und eine Map-Load-Sitzung in jeder Oracle-Zeile
> erfasst wurden. Sie bleibt als historische Zell-Regression erhalten, darf
> aber nicht als Beleg für eine spielerübergreifende Sofortspawn-Simulation
> verwendet werden. Neue Sitzungs-Corpora werden ohne eindeutigen Wert `0` oder
> `1` bewusst als `NotEvaluable` behandelt.

Chat 10 ist nach der Organismusprüfung wieder geöffnet. Die hashgebundene Matrix enthält 144 native
Einzelversuche auf World Sizes 160, 200, 300, 400 und 800. Sie umfasst
Vanilla-Karten, für den Editor angelegte Vanilla-Kopien und zwei mit der
aktuellen DE-Version erzeugte Custom-Maps.

- 136 `ExactMatch`;
- 0 `NotEvaluable`;
- 8 Mismatches;
- 0 Fehler.

Die frühere Annahme eines fehlenden Organismusdatensatzes war zu konservativ.
Die native Skirmish-Initialisierung setzt den relevanten Modus auf `1` oder
`99`; zusammen mit dem festen AIV-Spielerwert `0` akzeptiert der Validator alle
Organismusklassen. Dadurch verschwanden sämtliche 77 `NotEvaluable`-Fälle.
Die anschließend nativ belegte Last-Writer-Wins-Flattening-Regel des
100×100-Kandidatenrasters erhöhte die Gesamtquote von 97 auf 136 exakte Fälle.
Die verbleibenden acht Abweichungen betreffen ausschließlich
`testlord_serpcastle1`; Chat 11 darf erst beginnen, wenn sie klassifiziert und
behoben oder eng begründet sind.

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

## Offizielle Reihenfolge und Sitzungsmodell

Der native Map-Start verarbeitet die Spieler-IDs `1` bis `8` vollständig
nacheinander. Für einen KI-Spieler werden Kandidaten und Rotationen geprüft,
die endgültige Burg vorbereitet, der eigene Startkomplex erzeugt und bei
`advopt_pre_build == 1` die Burg bis 100 Prozent ausgeführt. Erst danach wird
die Spieler-ID erhöht. Die 100-Prozent-Ausführung läuft ihrerseits in
aufsteigender AIV-Frame-Reihenfolge; Positionen eines Frames werden in ihrer
Quellreihenfolge abgearbeitet.

Die offizielle Fit-Prüfung vergleicht dagegen keine zwei AIVJSON-Dateien als
abstrakte Pläne. Sie lädt den aktuellen Kandidaten in ein temporäres
100×100-Raster. Spätere Frames beziehungsweise Positionen überschreiben dort
frühere Zellen. Anschließend wird dieses Endraster zeilenweise gegen den zu
diesem Zeitpunkt realen nativen Tile-/Building-Zustand geprüft. Deshalb gilt:

- ohne Sofortspawn blockieren frühere bloße AIV-Pläne nicht;
- mit Sofortspawn können erfolgreich realisierte Teile früherer Spieler den
  Fit späterer Spieler blockieren;
- interne Überlappungen eines Kandidaten sind Last-Writer-Wins-Evidenz und kein
  zusätzlicher Live-Gebäude-Blocker;
- Keep, Vorratslager und weitere Startgebäude sind von AIV-Frames getrennte
  reale Startobjekte.

Der Importer übernimmt nun den expliziten nativen `selection`-Datensatz. Ein
Prüfversuch bleibt `PlannedAivElement`; nur der tatsächlich ausgewählte Versuch
wird bei aktivem Sofortspawn als `ScheduledAivPrebuild` geführt. Nach der
Ausführung wird er für den nächsten Spieler zunächst ausdrücklich nur als
`ProjectedPrebuiltAivBuilding` oder `ProjectedPrebuiltAivTile` fortgeschrieben.
Erst Laufzeitevidenz darf daraus `PrebuiltAivBuilding` oder `PrebuiltAivTile`
machen. Details und RVAs stehen in
`AIV_PREBUILD_AND_OVERLAP_ORDER.md`.

Historische Logs ohne Optionsfeld dürfen nur mit einer ausdrücklich bekannten
Zuordnung importiert werden, zum Beispiel:

    AIVPlacement.OracleComparison import-log LogOutput.log corpus --session-prebuild map-load-001=0,map-load-002=1

Der Override widerspricht einem bereits im Log erfassten Wert niemals still:
Bei einem Konflikt bricht der Import ab. Ohne erfassten Wert oder expliziten
Override bleibt eine sitzungsabhängige Auswertung `NotEvaluable`.

Der gepaarte Thasos-Corpus vom 2026-08-03 enthält 48 Fälle, gleichmäßig auf
`map-load-001` mit Wert `0` und `map-load-002` mit Wert `1` verteilt. Der
modusbewusste Lauf liefert 27 exakte Fälle, 21 Mismatches, 0 `NotEvaluable`
und 0 Fehler. Getrennt nach Modus sind es:

| `advopt_pre_build` | Exakt | Mismatch |
| ---: | ---: | ---: |
| `0` | 17 | 7 |
| `1` | 10 | 14 |

Das vorherige, nicht sitzungsweise verkettete Modell erreichte auf demselben
Paar nur 16 exakte Fälle bei 32 Mismatches. Die offizielle Reihenfolge erklärt
damit elf weitere Fälle. Die verbleibenden Sofortspawn-Abweichungen markieren
die Grenze der reinen Planprojektion: Ohne Live-Zwischenzustand ist nicht für
jeden Mapper belegt, welche geplanten Zellen die Ausführung tatsächlich als
Gebäude oder Tile-Zustand realisiert hat. Der Bericht liegt unter
`.native-analysis/chat10-next/thasos-player5-spawn-mode-paired/comparison-prebuild-aware.json`.

## Abnahmematrix

Die `v_`-Dateien sind unveränderte Vanilla-Karten, die der Benutzer in den
Custom-Ordner kopiert hat, um sie im Editor laden zu können.

| Karte | Art / World Size | Fälle | Exakt | `NotEvaluable` | Mismatch / Fehler | Zeit |
| --- | --- | ---: | ---: | ---: | ---: | ---: |
| `Height Advantage` | Vanilla-Kopie / 160 | 10 | 10 | 0 | 0 / 0 | 974,6 ms |
| `Bow Ridge` | Vanilla-Kopie / 200 | 12 | 12 | 0 | 0 / 0 | 788,0 ms |
| `Thasos` | Vanilla-Kopie, Section-1190-Anomalie / 300 | 22 | 15 | 0 | 7 / 0 | 817,5 ms |
| `A Friend Indeed` | Vanilla-Kopie / 400 | 14 | 14 | 0 | 0 / 0 | 788,5 ms |
| `Province of Bodrum OP` | Vanilla-Kopie / 400 | 20 | 20 | 0 | 0 / 0 | 1.025,7 ms |
| `unittest` | aktuelle Custom-Map / 160 | 58 | 58 | 0 | 0 / 0 | 848,2 ms |
| `testmap` | aktuelle Custom-Map / 800 | 4 | 4 | 0 | 0 / 0 | 440,7 ms |
| `Marshy Mayhem` | ältere Kontrollstichprobe / 400 | 4 | 3 | 0 | 1 / 0 | 333,4 ms |

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
Skirmish-AIV-Aufruf vollständig geklärt und werden nicht benötigt. Die acht
verbleibenden Testlord-Mismatches können jedoch eine Lobbyentscheidung verfälschen; deshalb
bleibt Chat 10 der nächste Schritt.

Auf Thasos erfasste der Oracle alle sechs KI-Auswahlen, aber nur fünf erreichten
den späteren `PrepareLayout`-Callback. Chat 11 muss Prüfaufträge daher für alle
belegten KI-Slots aus den Lobbydaten erzeugen und darf ihre Existenz nicht von
einem erfolgreichen Layout-Callback abhängig machen.
