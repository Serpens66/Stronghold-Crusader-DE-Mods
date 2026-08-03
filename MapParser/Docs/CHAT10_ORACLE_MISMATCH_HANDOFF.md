# Übergabe: Chat 10 und die 47 Oracle-Mismatches

## Auftrag und Stopplinie

Der nächste Chat setzt **Chat 10** aus `MapParser/AIV_PLACEMENT_ROADMAP.md`
fort. Er darf Chat 11 noch nicht beginnen. Ziel ist, die 47 reproduzierbaren
Abweichungen zwischen dem nativen AIV-Oracle und dem Offline-Analyzer nach ihrer
Ursache zu erklären und den Offline-Kern entsprechend zu korrigieren.

Chat 10 ist erst abgeschlossen, wenn ein erneuter Lauf aller 144 Fälle

- 0 Mismatches,
- 0 Fehler und
- nur nachweislich unvermeidbare `NotEvaluable`-Fälle

liefert. Der aktuelle Corpus besitzt alle benötigten Eingabedaten und erzeugt
0 `NotEvaluable`; eine Abweichung darf deshalb nicht ohne neuen technischen
Nachweis in `NotEvaluable` umetikettiert werden.

## Aktueller Sitzungs- und Belegungsnachtrag

Die ursprünglichen 144 Manifeste entstanden vor der Erfassung einer
Kartenstart-ID. Mehrere Auswahlgruppen derselben Karte dürfen deshalb nicht
allein wegen steigender Spieler-IDs als ein gemeinsamer Spielstart verkettet
werden. Der aktuelle Log-Importer schreibt für neue Daten in jeden Fall ein
explizites `SessionId` wie `map-load-001`, abgeleitet vom zugehörigen
`c_game_dll_loadmaptoplay_hook_impl`-Ereignis. Nur Fälle mit derselben
nichtleeren `SessionId` dürfen temporären Zustand vorheriger KI-Spieler teilen;
ein Manifest darf Fälle mit und ohne Sitzungs-ID nicht mischen.

Reale und temporär geplante Belegung sind im Offline-Modell getrennt:

- `BuildingId != 0` bezeichnet ausschließlich ein wirklich in der Map-
  beziehungsweise Laufzeitbelegung vorhandenes Gebäude;
- `PlannedOccupancies` bezeichnet den temporären nativen AIV-Prüfzustand und
  enthält `SessionId`, Spieler-ID, Mapper, Kategorie, Element- und Bauindex;
- eine solche Zelle erhält `PriorAivPlannedOccupied`, nicht
  `BuildingOccupied`; es wird keine künstliche Gebäude-ID als Ersatz erzeugt;
- blockierte AIV-Elemente und zugehörige Reservierungsflächen werden nicht als
  geplante Kernbelegung weitergereicht; Mapper 105 (Zugbrücke) ist durch den
  Spieler-3-Trace ebenfalls ausgenommen.

Der gesicherte Thasos-Mehrspielerlauf mit expliziter Sitzung umfasst 24 Fälle.
Spieler 2 und alle vier Rotationen von Spieler 3 stimmen exakt. Ab Spieler 4
muss noch geklärt werden, welche Mapper-Typen aus zwei vorherigen temporären
AIV-Zuständen wie native Gebäude-, Wall-, Trap- oder Terrainwerte wirken. Der
nächste kleinste Lauf ist deshalb der bereits installierte Vier-Rotationen-
Zelltrace für Spieler 4.

Der lokale `.native-analysis/TraceOverlapAnalyzer` erwartet als viertes
Argument immer Gradwerte `0`, `90`, `180` oder `270`, nicht die nativen Codes
`0`, `2`, `4`, `6`. Details und Beispiel stehen in
`.native-analysis/TraceOverlapAnalyzer/README.md`.

## Zuerst lesen

Diese Dateien in der angegebenen Reihenfolge lesen:

1. `MapParser/AIV_PLACEMENT_ROADMAP.md`, Abschnitt Chat 10;
2. dieses Übergabedokument;
3. `MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md` für Matrix und Hashes;
4. `MapParser/Docs/AIV_PLACEMENT_ORACLE.md` für nativen Vertrag und RVAs;
5. `MapParser/Docs/AIV_PLACEMENT_RULES.md` für die belegten Regeln;
6. `AIVPlacement/AIVPlacement.OracleComparison/Program.cs`;
7. `AIVPlacement/AIVPlacement.Core/AivCastleProjector.cs`;
8. `AIVParser/AIVParser.Core/AivBlockedAreaCatalog.cs`;
9. `AIVPlacement/AIVPlacement.Core/AivPlacementRuleEvaluator.cs`;
10. `AIVPlacement/AIVPlacement.Core/AivPreplacementMapState.cs`;
11. `ActiveAIVDetector/src/AivPlacementOracle.cs`.
12. `.native-analysis/TraceOverlapAnalyzer/README.md`.

## Verbindliche Ausgangslage

Die kanonische Baseline umfasst 144 Fälle: 97 exakt, 47 Mismatches,
0 `NotEvaluable`, 0 Fehler. Die Kartenaufschlüsselung steht im
Oracle-Vergleichsdokument. Sämtliche Rat-AIV-Fälle stimmen exakt. Abweichungen
treten nur bei folgenden AIVs auf:

- `wolf+.aivjson`: 55 Fälle, davon 23 exakt und 32 Mismatches;
- `testlord_serpcastle1.aivjson`: 17 Fälle, davon 2 exakt und 15 Mismatches.

Bei den Wolf-Abweichungen liegt `OfflineBlocked - NativeBlocked` immer zwischen
`-149` und `-1`: Der Offline-Analyzer blockiert also stets zu wenige Zellen.
Beim Testlord liegt die Spanne zwischen `-150` und `+1`; dort existieren
zusätzlich eigenständige Score-Abweichungen.

Folgende Punkte sind bereits belegt und werden nicht erneut als offene
Hypothese behandelt, solange keine widersprechende Evidenz entsteht:

- World Sizes 160, 200, 300, 400, 500, 600, 700 und 800 werden unterstützt;
- Keep-Anker kommen exakt aus Section 1013 beziehungsweise 4013;
- U4-Radarpositionen sind keine Keep-Tile-Anker;
- Rotation verwendet den festen nativen Ursprung der Orientierung 0 und rotiert
  nicht um den AIV-Keep-Marker;
- die Startgebäude und eindeutig angrenzenden Wall-Owner-Randzellen werden im
  Preplacement-Snapshot ausgeblendet;
- Organismen sind für den Skirmish-AIV-Aufruf kein Ausschlussgrund: Die native
  Initialisierung setzt den Modus auf `1` oder `99`, und der Validator wird für
  Spieler 0 aufgerufen;
- die `v_`-Karten sind unveränderte Vanilla-Kopien im Custom-Ordner;
- `unittest` ist die absichtlich kleine randnahe Kontrollkarte, `testmap` die
  leere 800er-Kontrollkarte.

## Zwei zuerst zu bearbeitende Reproduktionsfälle

Alle Befehle in diesem Dokument werden aus dem Workspace-Root ausgeführt.
Vorhandene native Sollwerte und Hashes im Corpus dürfen nicht geändert werden.

### Spur A: kleinste Blocked-Cell-Abweichung

`oracle-005-01-wolf-r0` auf `v-bow-ridge.json` ist der erste Fall:

- nativer Score 20, Offline-Score 20;
- native Blocked Cells 1215, Offline Blocked Cells 1214;
- Differenz genau eine fehlende Offline-Blockierung;
- erster gemeldeter Offline-Grund: `TerrainBlocked`, Mapper 76.

Reproduktion:

    dotnet run --project AIVPlacement\AIVPlacement.OracleComparison\AIVPlacement.OracleComparison.csproj -c Release --no-build -- AIVPlacement\OracleCorpus\Captured-2026-08-03\v-bow-ridge.json --case oracle-005-01-wolf-r0 --output .native-analysis\chat10-next\bow-ridge-wolf-r0.report.json

Exitcode 1 ist bei einem erfolgreich reproduzierten Mismatch erwartet.
Exitcode 2 bedeutet dagegen Konfigurations-, Parse- oder Laufzeitfehler.

### Spur B: Score-Abweichung bei gleicher Blocked-Cell-Zahl

`oracle-019-03-testlord-serpcastle1-r180` auf `v-a-friend-indeed.json` isoliert
die sequenzielle Score-/Build-Step-Semantik:

- nativer Score 15, Offline-Score 18;
- native und Offline Blocked Cells jeweils 291.

Reproduktion:

    dotnet run --project AIVPlacement\AIVPlacement.OracleComparison\AIVPlacement.OracleComparison.csproj -c Release --no-build -- AIVPlacement\OracleCorpus\Captured-2026-08-03\v-a-friend-indeed.json --case oracle-019-03-testlord-serpcastle1-r180 --output .native-analysis\chat10-next\friend-testlord-r180.report.json

Dieser Fall darf nicht durch eine pauschale Terrainregel „repariert“ werden,
weil die aggregierte Blocked-Cell-Zahl bereits exakt ist.

## Genaue Arbeitsreihenfolge

### 1. Baseline unverändert reproduzieren

1. `AIVPlacement/build.bat` ausführen.
2. Zuerst nur Spur A, danach nur Spur B ausführen.
3. Prüfen, dass die oben dokumentierten Scores und Zellzahlen unverändert
   erscheinen. Bei einer abweichenden Baseline nicht weiterportieren, sondern
   zuerst lokale Änderungen, Eingabehashes und Berichtspfade klären.

Weitere Kartenstarts durch den Benutzer sind zu diesem Zeitpunkt nicht nötig.

### 2. Diagnosefähigkeit ergänzen, bevor Regeln geändert werden

Der Offline-Vergleich soll für einen per `--case` ausgewählten Fall eine
opt-in Diagnose liefern, mindestens gruppiert nach

- AIV-Originalindex und Build-Schritt,
- Mapper,
- Rotation,
- AIV- und Map-Koordinate beziehungsweise Tile-ID,
- Kern-Footprint oder Zusatzfläche,
- Reason-Code und den entscheidungsrelevanten rohen Mapwerten.

Die normale kompakte Corpusausgabe darf dadurch nicht unnötig anwachsen. Die
Diagnose wird entweder über eine eigene CLI-Option aktiviert oder in einen
separaten Diagnosebericht geschrieben. Für jede neue Auswertungslogik sind
synthetische Regressionstests anzulegen.

Der native Oracle protokolliert derzeit nur Aggregate aus dem AIV-Zustand:

- `EvaluatedCellCountOffset = 0x5B4F8`;
- `BlockedCellCountOffset = 0x5B4FC`.

Reicht der bestehende Decompile zusammen mit der Offline-Diagnose nicht aus,
wird `ActiveAIVDetector` um einen **passiven, opt-in Per-Cell-Trace** erweitert.
Er darf nur innerhalb des aktiven `EvaluateCandidateFit`-Fensters erfassen,
muss andere Validator-Aufrufe ausfiltern und darf keinen Rückgabewert oder
Spielzustand ändern. Vor dem Hook müssen Aufrufkonvention und Argumente nativ
belegt werden. Logs erhalten Zeitstempel mit Millisekunden. Relevante bekannte
Einstiegspunkte für den gezielten Audit sind:

- `EvaluateCandidateFit`: RVA `0x562F0`;
- gemeinsamer Building-Validator: RVA `0x7A2D0`;
- `LoadCandidate`: RVA `0x54590`;
- `ApplyRotation`: RVA `0x558E0`.

Native Adressen immer zusammen mit RVA und dem in
`AIV_PLACEMENT_ORACLE_COMPARISON.md` dokumentierten DLL-Hash festhalten. Für
Rizin ausschließlich die vorgeschriebenen Wrapper aus `.native-analysis`
verwenden und zunächst nur die relevante Funktion dekompilieren.

### 3. Spur A vollständig erklären und beheben

Für `oracle-005-01-wolf-r0` zuerst bestätigen, dass auch die Zahl der
ausgewerteten Zellen nativ und offline identisch ist. Danach die Offline-Zellen
nach Element, Mapper und Fläche gruppieren und die eine fehlende native
Ablehnung lokalisieren.

Die Ursache in dieser Reihenfolge prüfen:

1. fehlende oder falsch ausgerichtete Zusatzfläche in
   `AivBlockedAreaCatalog`/`AivCastleProjector`;
2. falsche Koordinaten- oder Tile-Projektion genau dieses Elements;
3. zu weit gehende Normalisierung in `AivPreplacementMapState`;
4. fehlende Mapper-/Terrain-/Height-Sonderregel in
   `AivPlacementRuleEvaluator`;
5. abweichende native Behandlung interner Überlappungen.

Keine Regel allein aus dem Mappernamen oder aus einem einzelnen Gesamtwert
erraten. Die Korrektur muss durch nativen Kontrollfluss oder einen gefilterten
Per-Cell-Lauf belegt und durch einen synthetischen Regressionstest abgesichert
sein.

Nach der Korrektur zuerst die vier Bow-Ridge-Wolf-Rotationen ausführen. Danach
alle Wolf-Fälle auf Height Advantage, Bow Ridge, Thasos und Province of Bodrum
OP. Bereits exakte Wolf-Fälle dürfen nicht regressieren.

### 4. Spur B unabhängig erklären und beheben

Für `oracle-019-03-testlord-serpcastle1-r180` die gleiche Blocked-Cell-Zahl als
Kontrollbedingung bewahren. Den nativen Score-Aufbau in
`EvaluateCandidateFit` und der Kandidatenauswahl mit der Offline-Berechnung von
`FirstBlockingBuildStep` vergleichen. Insbesondere prüfen:

- Frame-/Build-Queue-Index gegenüber expandiertem Elementindex;
- mehrere Positionen innerhalb eines AIV-Frames;
- Pausen und Nicht-Placement-Einträge;
- Kern- und Zusatzflächen desselben Build-Schritts;
- ob der native Score beim ersten blockierten Frame, Element oder Zelltest
  festgeschrieben wird.

Die Definition des nativen Scores nicht an die aktuell gewünschte
`Complete`/`Partial`-Anzeige anpassen; sie muss den vorhandenen Oracle-Wert
reproduzieren. Danach alle 17 Testlord-Fälle laufen lassen.

### 5. Regression stufenweise ausweiten

Nach jeder belegten Korrektur in dieser Reihenfolge testen:

1. der konkrete Einzelfall;
2. alle Fälle derselben AIV auf derselben Karte;
3. alle Wolf- beziehungsweise alle Testlord-Fälle;
4. alle 144 Fälle aus sämtlichen Manifesten.

Die kanonischen Manifeste liegen hier:

- `AIVPlacement/OracleCorpus/MarshyMayhem-2026-08-02.json`;
- `AIVPlacement/OracleCorpus/Captured-2026-08-03/*.json`;
- `AIVPlacement/OracleCorpus/Captured-2026-08-03-800/testmap.json`.

Für den Abschlusslauf pro Manifest einen Bericht unter
`AIVPlacement/OracleCorpus/Results/` aktualisieren. Die CLI meldet Fortschritt,
Laufzeit und ETA. Exitcode 1 ist während der Arbeit erwartbar, am Ende von
Chat 10 aber nicht mehr.

### 6. Nur bei benötigter neuer Laufzeitevidenz den Benutzer einbinden

Ist ein gefilterter nativer Per-Cell-Trace unvermeidbar, zuerst den Diagnosemod
fertigstellen und bauen. Danach den Benutzer nur um den kleinsten notwendigen
Start bitten:

1. Bow Ridge mit Wolf, Rotation 0, für die fehlende Einzelzelle;
2. optional A Friend Indeed mit `testlord_serpcastle1`, Rotation 180, falls die
   Score-Semantik nicht allein aus dem nativen Kontrollfluss folgt.

Keine breite neue Kartenmatrix anfordern, solange diese beiden Fälle genügen.

## Änderungs- und Evidenzregeln

- Map-, AIV- und native Sollwerte im Corpus nicht an das Offline-Ergebnis
  angleichen.
- Proprietäre `.map`- oder `.aivjson`-Dateien nicht in das Repository kopieren.
- Jede neue Regel erhält kurze Warum-Kommentare und synthetische Tests.
- Diagnosecode bleibt opt-in und vom produktiven Offline-Kern getrennt.
- Vorhandenen alten Code nicht ungefragt als Fallback stehen lassen. Wenn eine
  belegte Implementierung ihn ersetzt, mit dem Benutzer klären, ob der alte
  Pfad gelöscht werden darf.
- Geänderte Textdateien auf CRLF prüfen.
- Am Ende die passenden `build.bat`-Dateien mit den für das Deployment nötigen
  Rechten ausführen; bei Änderungen an `ActiveAIVDetector` auch dessen Build.
- Roadmap und Oracle-Vergleich nach jeder abgeschlossenen Ursachenklasse mit
  neuer Quote, betroffenen Mappern und Evidenz aktualisieren.

## Abnahme und Übergabe an Chat 11

Chat 10 darf erst dann in der Roadmap auf `Abgeschlossen` gesetzt und Chat 11
auf `Nächster Schritt` gestellt werden, wenn

- alle 144 Fälle erneut hashgeprüft liefen;
- jeder Fall exakt ist oder eine einzeln dokumentierte, technisch zwingende
  `NotEvaluable`-Begründung besitzt;
- es 0 ungeklärte Mismatches und 0 Fehler gibt;
- alle Builds und synthetischen Tests erfolgreich sind;
- die neue Abschlussquote in
  `MapParser/Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md` steht.

## Kopierbarer Startprompt für einen neuen Chat

> Setze Chat 10 aus `MapParser/AIV_PLACEMENT_ROADMAP.md` fort. Lies zuerst
> `MapParser/Docs/CHAT10_ORACLE_MISMATCH_HANDOFF.md` vollständig und halte dich
> an die dortige Reihenfolge. Reproduziere zunächst
> `oracle-005-01-wolf-r0` auf Bow Ridge und danach unabhängig
> `oracle-019-03-testlord-serpcastle1-r180` auf A Friend Indeed. Ergänze zuerst
> gezielte Diagnoseevidenz, ändere keine Regeln auf Verdacht und starte Chat 11
> erst, wenn der vollständige 144-Fälle-Lauf keine ungeklärten Mismatches oder
> Fehler mehr enthält.
