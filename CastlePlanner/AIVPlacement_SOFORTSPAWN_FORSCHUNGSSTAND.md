# AIVPlacement: Sofortspawn und Lobby-Prognose – aktueller Forschungsstand

Stand: 2026-09-24. Maßgebliche installierte `CrusaderDE.dll`:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(Image Base `0x180000000`). `RVA + 0x180000000 = VA`. Bei einem anderen Hash
sind die folgenden Adressen und Schlussfolgerungen erneut zu prüfen. Diese
Datei beschreibt den **aktuellen Arbeitsstand** des Mods und trennt aktuelle
Native-Analyse, ältere Laufzeitbelege und noch offene Fragen.

## Ziel und tatsächlich unterstützter Bereich

Die Lobby soll für die aktuelle Map und den gewählten KI-Start anzeigen, wie
eine AIVJSON zu Vanillas Platzierungsregeln passt. Die Anzeige ist eine
Offline-Prognose; sie verändert Vanillas Kandidatenwahl, Rotation und Bau
nicht. `MapParser`, `AIVParser` und `AIVPlacement` liefern Map-Snapshot,
Kandidatenraster und Fit-Scores. Der Script Extender bietet Live-AIV-Views,
aber keine Fit-Abfrage für eine in der Lobby noch ungeladene `.map`.

Bei **Completed enemy castles / Sofortspawn** ist der erste aktive KI-Spieler
auswertbar, sofern Karte, Keep und AIV-Quellen bekannt sind: Vor ihm hat noch
keine andere KI eine Burg ausgeführt. Spätere KI-Spieler erhalten im
produktiven Lobby-Pfad `NotEvaluable`, solange der echte sequenzielle Bauzustand
früherer Spieler nicht belegt ist. Auch ohne Sofortspawn sind spätere Starts
`NotEvaluable`, wenn eine vorherige Startrotation unbestimmt bleibt oder die
Karte eine ungeklärte, dicht überlappende Besitzerkonstellation enthält.
Die Native-Nachprüfung vom 2026-09-23 trennt hierfür Kandidat und Drehung:
Wenn ohne Sofortspawn mehrere zufallsabhängige AIV-Auswahlen dieselbe
Startdrehung ergeben, lässt sich der nächste Startzustand trotzdem eindeutig
rekonstruieren. `0x53D00` hält den gewählten Plan und Besitzermasken für den
späteren Bau bereit; der nachfolgende Fit liest sie nicht. Unterschiedliche
mögliche Drehungen, fehlende Fits und Sofortspawn bleiben gesperrt. Die
Lobby-Auswertung führt mögliche Autoergebnisse deshalb getrennt von der nur
bei eindeutigem Kandidaten veröffentlichten Autoauswahl.

## Aktueller Native-Pfad

| Funktion | RVA / VA | Rolle und Vertrauensgrad |
| --- | --- | --- |
| Skirmish-Start | `0x94350` / `0x180094350` | Spieler- und Auswahlfolge, Startbau, Sofortspawn-Aufruf: hoch; Zuordnung aller Optionsfelder: offen |
| Fester Kandidat | `0x54DE0` / `0x180054DE0` | Import, Drehung, Raster-Fit eines Kandidaten: hoch |
| Auto-Auswahl | `0x54F60` / `0x180054F60` | Varianten- und Rotationsfolge samt Schwellen: hoch |
| Layout vorbereiten | `0x53D00` / `0x180053D00` | gewählten Kandidaten in Build-Schritte und Layoutzustand überführen: hoch für Aufruf, mittel für alle Seiteneffekte |
| Kandidat importieren | `0x55320` / `0x180055320` | 100×100-Mapper- und Frame-Raster: hoch für Reihenfolge |
| Raster drehen | `0x56670` / `0x180056670` | native Anfangs- und Folgedrehung: hoch |
| Fit prüfen | `0x57080` / `0x180057080` | aktuelle Mapperzellen gegen momentane Map prüfen: hoch |
| Tile-Validator | `0x7B060` / `0x18007B060` | Terrain-, Gebäude-, Besitzer- und Mapperregeln: mittel für sämtliche Mapper |
| Bis Prozent ausführen | `0x55F50` / `0x180055F50` | Framefolge bei 100 %: hoch |
| Einen Frame ausführen | `0x51790` / `0x180051790` | Verzweigung nach Mapper, Zustand und Position: hoch für Kontrollfluss, mittel für vollständige Bauwirkung |
| Footprint-/Bereinigungshilfe | `0x5CD90` / `0x18005CD90` | im Baupfad aufgerufen; Ergebnis in den hier geprüften Aufrufen nicht als harte Bausperre benutzt: mittel |
| Bauvoraussetzung und Konstruktion | `0xC3BF0` / `0x1800C3BF0`, `0x6D580` / `0x18006D580` | separate Vorauswahl und Konstruktorpfad: mittel; typspezifische Unterfunktionen nicht vollständig übertragen |

`0x94350` bearbeitet Spieler-IDs in aufsteigender Reihenfolge bis 8. Bei
einem ausgewählten KI-Kandidaten ruft es `0x53D00` auf, erzeugt danach den
mit der AIV-Rotation gekoppelten Startkomplex und kann anschließend
`0x55F50(AivSystem*, playerId, 100)` aufrufen. Erst danach wird die Spieler-ID
erhöht. Der nächste Spieler sieht folglich den **wirklich veränderten**
TileManager-Zustand, nicht bloß den Plan des vorherigen Kandidaten. Ein
abgelehnter KI-Kandidat durchläuft diesen AIV-Bauzweig nicht; der native
Fallback-Start ist gesondert zu betrachten.

Die aktuelle Bedingung am Sofortspawn-Aufruf ist breiter als eine einzelne
Lobbyoption. In dekompilierter Form lautet sie sinngemäß:

```text
(G_1887EE2F8 != 0 &&
    (G_1887EE2F4 != 0 || (G_188574B90 != 99 && G_1887EE2F0 != 0)))
|| specialStartMode || forcedFixedCandidate
```

`specialStartMode` entsteht in `0x94350` unter einem gesonderten
Spielmoduszweig (`G_183669040 == 1`, `G_188574B90 == 99`, lokaler Modus 4).
`forcedFixedCandidate` wird gesetzt, wenn der feste Kandidatenselektor über
999 liegt. Die exakte Herkunft der drei globalen Optionsfelder aus
`MPsetupData.advopt_pre_build` und anderen Startmodi ist für diesen Hash
noch **nicht vollständig belegt**. Für die normale Lobby erfasst der Mod den
verwalteten `advopt_pre_build`-Wert. Aus diesem Feld allein wird keine
allgemeine Aussage über Sondermissionen oder erzwungene Starts abgeleitet.

`0x55F50` berechnet den letzten Frame als
`(percentage * vorbereiteter letzter Frame) / 100` und ruft `0x51790`
einschließlich dieses Frames für `0, 1, ...` auf. Der Startpfad übergibt
`percentage=100`; die Aufrufe an `0x51790` erhalten
`(AivSystem*, playerId, frameIndex, restrictedMode=0, freeOrForced=1)`.
Die Daten kommen aus dem vorbereiteten Village-Slot und dessen Build-Schritten:
`playerId` ist die 1-basierte Game-ID, `frameIndex` der 0-basierte Index im
vorbereiteten Build-Step-Array.

`0x51790` ist kein einfaches „Footprint frei ⇒ Gebäude steht“-Prädikat.
Es prüft unter anderem Frame-Status, Mapper, globale Spielzustände,
Nachbarschaft und Erzeugbarkeit. `freeOrForced=1` überspringt einen
Ressourcen-/Verfügbarkeitsaufruf, aber nicht diese übrigen Zweige. Normale
Strukturen führen über `0x5CD90` und `0x6D580`; gruppierte Tile-Mapper haben
eigene Schleifen und Konstruktorpfade. `0x5CD90` prüft Zellen mit `0x7B060`
unter der echten Spieler-ID und `mode=1`; bei geeigneter Belegung kann es
vorhandene Strukturdatensätze entfernen. `0x51790` verwertet seinen
Rückgabewert in den hier geprüften Aufrufen nicht als harte Bausperre.
`0x6D580` ruft typspezifische Konstruktion auf und verändert unter anderem
Terrain-, Organismus-, Entity- und Gebäudeschichten. Auch ein Rückgabewert `0` ist allein
kein Beweis für einen unveränderten Tilezustand: Der Mapper-99-Zweig ruft
`0x6D580` für seine Positionen auf und gibt anschließend `0` zurück.
Für eine exakte Offline-Rekonstruktion müssen daher alle relevanten
Kartenlayer **vor und nach jedem Frame** berücksichtigt werden.

Der Fit-Pfad benutzt dagegen das verdichtete, nach Rotation fertige
100×100-Raster. Später importierte Frames überschreiben frühere Zellen;
`0x57080` prüft die Endbelegung zeilenweise mit `0x7B060` im Modus
`playerId=0, mode=0`. Der spätere Bau prüft und verändert die reale Karte
mit der tatsächlichen Spieler-ID. Gleiche AIV-Datei und gleicher Fit-Score
reichen deshalb nicht als Beweis für gleiche Sofortspawn-Wirkung.

## Archivierte Laufzeitbelege – anderer Native-Hash

Die detaillierten Thasos-Aufnahmen in
[`AIV_PREBUILD_AND_OVERLAP_ORDER.md`](../Helpers/MapParser/Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md)
stammen aus der früheren DLL `17F8DD4A…`. Ihre RVAs sind **keine aktuellen
Adressen**. Als Verhaltensbelege zeigen sie:

- Mit Sofortspawn stiegen die blockierten Zellen beim späteren Spieler je
  Rotation von `320/189/275/284` auf `433/313/354/353`.
- Beim ersten beobachteten KI-Sofortspawn liefen 77 Frames `0..76`. Das
  `BuildingId`-Grid gewann 757 Zellen; innerhalb dieses Spielerfensters gab
  es keine entfernten oder ersetzten Gebäudezellen.
- Vom damaligen AIV-Plan waren nur 707 dieser 757 Zellen positionsgleich.
  Je 25 geplante Stockpile- und Drawbridge-Zellen fehlten; 50 andere Zellen
  stammten von einem Tunnellers-Guild-Hauptgebäude samt separatem 5×5-Hof,
  obwohl dessen Kandidaten-Fit blockiert gewesen war.
- Der Drawbridge-Frame 28 und Goods-Yard-Frame 41 änderten im konkreten Trace
  keine `BuildingId`-Zellen; der Guild-Frame 36 fügte genau 50 hinzu. Das
  beweist für jene Sitzung die Abweichung zwischen Plan und Live-Bau, nicht
  das aktuelle Verhalten aller Mapper oder aller Tilelayer.

Die älteren Konstruktor-RVAs und diese Zahlen werden erst nach einem
aktuellen, hashgebundenen Capture als Aussage über `FBCB9319…` übernommen.
Sie begründen bereits, warum der Mod geplante AIV-Elemente **nicht** als
fertig gebaute Hindernisse früherer KIs einsetzt.

## Bereits implementiert

| Bereich | Aktueller Code und Wirkung |
| --- | --- |
| Lobby-Eingabe | [`LobbyRequestBuilder`](AIVPlacement.Core/LobbyRequestBuilder.cs) erfasst Karte, Spieler/Keep, Kandidaten, Rotation und Sofortspawn-Wert. Host und KI-Spielerfolge werden geprüft. Der erste KI-Spieler ist bei Wert 1 zugelassen; spätere werden `PreBuildSequenceUnsupported`. |
| Offline-Fit | [`AsyncPlacementEvaluation`](AIVPlacement.Core/AsyncPlacementEvaluation.cs) nutzt Map-Snapshot, AIV-Parser, Projektion und Regeln pro Kandidat und für vier Drehungen. Caches berücksichtigen Map-/AIV-Änderungen und den Sofortspawn-Wert. |
| Startzustand | [`AivPreplacementMapState`](../AIVPlacement/AIVPlacement.Core/AivPreplacementMapState.cs) entfernt noch nicht erzeugte Startgebäude, behält frühere Startkomplexe und dreht sie mit der ausgewählten AIV-Rotation. Besitzerbehaftete Mauern werden nur dem passenden Start zugeordnet; Besitzer-0-Mauern folgen der belegten bisherigen Nachbarschaftsregel. |
| Dichte Starts | Eine nach einem vorherigen Start relevante Besitzerüberschneidung führt zu `StartOverlapUnproven` / `NotEvaluable`, statt einen sicheren Fit zu behaupten. |
| Auto-Auswahl | [`NativeAivAutoSelector`](AIVPlacement.Core/NativeAivAutoSelector.cs) wertet alle möglichen RNG-Startpunkte und beide noch nicht eindeutig aus dem Lobbyzustand abgeleiteten Rotationsmodi aus. Nur ein gemeinsamer Kandidat samt Drehung wird veröffentlicht. |
| Anzeige | [`AivSelectionDialogRuntime`](src/AIVPlacement/AivSelectionDialogRuntime.cs) zeigt die vier Einzelbewertungen und ein eindeutiges oder unbestimmtes Vanilla-Autoergebnis; es ändert Vanillas Auswahl nicht. |
| Aktualisierung | [`AivPlacementRuntime`](src/AIVPlacement/AivPlacementRuntime.cs) erfasst Host-Lobby, Keep-Zuordnung, gewählte Map, AIV-Dateien und `advopt_pre_build`; bekannte UI-Änderungen invalidieren die Erfassung. Das gedrosselte Polling prüft Lobbyzustand und Dateien erneut, und neue Generationen brechen veraltete Auswertungen ab. |
| Live-API | Der installierte Script Extender stellt importierte Varianten, Village-Slots, Build-Schritte, Tilefolgen und Layout-Grids über `GameAIVManagerAPI` bereit. Diese sind Diagnoseansichten nach nativer Initialisierung. `AivSystem` bleibt wegen 1.930.456 Byte ausschließlich pointer-/spanbasiert; der Mod verwendet die Views nicht als Ersatz für die Offline-Kartenprüfung. |

Der separate Native-Spawn-Pfad von CastlePlanner bindet auf diesem Hash
`0x55F50` als `ExecuteToPercentageDelegate`; seine Versions-/Layoutgates
stehen in [`UpdateToNewDLL.md`](UpdateToNewDLL.md). Daraus folgt keine
zusätzliche Offline-Sofortspawn-Simulation für die Lobby. Der lokale
`shcde-fixes-main`-Mod greift ebenfalls in AIV-Kapazitäten und KEEP3 ein;
neuer Code muss diese veränderte Runtime weiterhin berücksichtigen.

## Bisherige Vergleichsdaten und Grenze

| Korpus | Ergebnis des aktuellen Offline-Codes | Aussagegrenze |
| --- | --- | --- |
| Target Zone, Sofortspawn aus | 212/212 exakt | Archivlog ohne aktuellen DLL-Hash |
| `test AI overbuild eachother`, Sofortspawn aus | 4/145 exakt, 141 Abweichungen nach Besitzerfix | Dichte Starts bleiben produktiv für spätere KI fail-closed; Archivlog ohne aktuellen DLL-Hash |
| `testanimals` und Crater Lake, Sofortspawn an | 4/4 exakt im Log-075-Korpus | Frühe auswertbare Fälle; Archivlog ohne aktuellen DLL-Hash |
| Crater Lake, Sofortspawn an | 4 exakt, 6 `NotEvaluable` im Log-069-Korpus | Spätere Bauzustände werden ausdrücklich nicht geschätzt |
| Crater Lake, 2026-09-23, Sofortspawn aus | 12/16 native Versuche exakt, 4 Parserfehler für dieselbe Plague-Doctor-AIV | Aktuelle DLL `FBCB9319…`; keine Aussage zum Sofortspawn oder zu anderen Karten |

Die archivierten AIV-Dateien wurden für diese Vergleiche per SHA-256 auf
lokale Quellen umgebunden. Die Daten belegen die genannten Fälle, aber keine
allgemeine Gleichheit auf beliebigen Maps. Insbesondere ist die
Überlappungskarte ein sinnvoller Stresstest: Nahe Starts und Burgen können
dieselben Zellen beanspruchen und sich im wirklichen Bau gegenseitig ändern
oder blockieren.

Die aktuelle Crater-Lake-Aufnahme verwendet die Map mit SHA-256
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
Sie enthält sieben KI-Auswahlen und 16 native Fit-Versuche bei
`advopt_pre_build=0`. Für zwölf Versuche stimmen Status, Rohscore,
Fit-Prozent und Zellzähler exakt mit der Offline-Auswertung überein. Die vier
übrigen Versuche betreffen eine Plague-Doctor-AIV mit `miscItems.number`
außerhalb des unterstützten Bereichs `0..9`; der Offline-Parser lehnt sie ab,
sodass keine Prognose für diese Datei als exakt gelten darf. Drei Zelltrace-
Selbstprüfungen meldeten abweichende blockierte Zellen, obwohl die zwölf
vergleichbaren Native-Scores exakt waren. Das ist als Diagnosegrenze getrennt
zu untersuchen. Der Oracle-Vergleicher wurde auf denselben AIV-JSON-Loader wie
der Lobby-Pfad umgestellt, damit leere `{}`-Frames korrekt als No-op gelten.
Der nachfolgende aktuelle Native-Audit von `0x55320` zeigt für Misc-Einträge
einen Schreibindex `Typ * 10 + Nummer` in einen Puffer mit 320 `int`-Werten.
Werte ab `number=10` überlappen bei gültigen Typen folgende Zehnerblöcke;
`10` und `11` stehen in der beobachteten Plague-Doctor-AIV. Der Fit-Rasterpfad
`0x57080` liest diesen Misc-Puffer nicht. Der Offline-Parser lässt für die
Lobby-Fit-Auswertung jeden in den nativen Puffer passenden flachen Index zu
und warnt bei `number>9`. Das begrenzt weder die Zahl der ausgewählten AIVJSON-
Dateien noch die Kandidatenauswertung. Alle 16 Crater-Lake-Oracle-Fälle,
einschließlich der vier Plague-Doctor-Drehungen, stimmten nach dieser Änderung
in Status, Score, Prozent und Zellzahlen exakt überein. Spätere Bauwirkungen
sind dadurch noch nicht belegt.
Die älteren Thasos-Korpora konnten in dieser Umgebung nicht neu ausgeführt
werden, weil ihre referenzierte `v_Thasos.map` hier fehlt.

## Nächster belastbarer Forschungsnachweis

Der ActiveAIVDetector wurde für weitere Aufnahmen erweitert. Der bisherige
aktuelle Ingame-Beleg umfasst nur den Fit-Pfad ohne Sofortspawn. Sein vorhandener
`APIShared`-Observer auf `0x51790` bleibt der einzige Bau-Frame-Hook. Opt-in
erfasst er pro Kartenstart bis zu acht KI-Spieler und setzt die Quoten beim
nächsten Map-Ladeereignis zurück. Der Callback speichert vor Vanillas Frame
einen wiederverwendeten Snapshot und danach ausschließlich Differenzen der
acht für den auditierten Fit-Validator relevanten Tile-Schichten: Logic,
Logic2, Organism, BuildingId, TileUnitId, Height, DefaultHeight und
WallOwner. Hinzu kommen Änderungen von Alive-State, Typ, Besitzer, GlobalId,
Start-Tile, Footprintgröße und Startkoordinaten der Gebäudedatensätze. Der
vollständige Inhalt der Gebäudedatensätze und weitere grafische oder
pfadbezogene Tile-Schichten sind damit **noch nicht** rekonstruiert.

Die Auswahlspur nimmt die rohen nativen Optionen `0x188574B90` und
`0x1887EE2F0/2F4/2F8` am Selektoreintritt auf, sofern sie im geladenen
Image liegen. Die Zuordnung zu `advopt_pre_build` bleibt offen. Traces
vermerken Map-, AIV- und Native-Datei-SHA-256, Kandidat, Rotation und
Keep-Referenz. `frameSnapshotsComplete=false` kennzeichnet fehlende
Auswahlverknüpfung, Pointerprobleme oder Snapshotfehler; es ist **kein**
Beweis, dass eine vollständige Spieler-Bausequenz erfasst wurde. Diese
Sicherheitsgrenze bleibt auch nach einem erfolgreichen Build bestehen.

Für die gezielte Aufnahme ist vor dem einzigen Spielprozess-Start in der
ActiveAIVDetector-Konfiguration sowohl `Oracle cell trace/Enabled` als auch
`Oracle prebuild trace/Enabled` auf `true` zu setzen. `PlayerId=-1` folgt
allen Spielern; die Cell-Trace-Filter `CandidateId`, `Orientation`, `KeepX`
und `KeepY` akzeptieren ebenfalls `-1` als Wildcard. Die Standardquoten sind
2048 Fit-Grids und acht KI-Bausequenzen **je Kartenstart**. Die Fit-Quote
deckt acht KI-Spieler mit je 50 Kandidaten und vier Drehungen ab. Die Dateien liegen
unter `BepInEx/plugins/ActiveAIVDetector_Serp/CellTraces` beziehungsweise
`PrebuildTraces`. Vier Matches in einem Prozess sind der erste Durchlauf:
Crater Lake und `test AI overbuild eachother`, jeweils Sofortspawn aus und an.
Weitere Starts hängen von der konkret fehlenden Mapper- oder Auswahlabdeckung
ab; eine vollständige Rekonstruktion wird nicht aus der Anzahl vier abgeleitet.

1. Den verwalteten `advopt_pre_build`-Wert und die aktuellen nativen
   Optionsfelder `0x1887EE2F0/2F4/2F8` vor `0x94350` in **derselben**
   Startsession zusammen erfassen. Sonderzweige separat kennzeichnen.
2. Auf einer normalen und der Überlappungskarte pro KI den ausgewählten
   Kandidaten, die Rotation, den Platzierungsstatus und den Zustand direkt
   vor und nach `0x55F50` erfassen; in auffälligen Fällen alle Frames mit
   Mapper, Positionen, Rückgabe und Änderungen an BuildingId, Terrain/Logic,
   Owner, Höhe, Entity und Organism aufnehmen.
3. Jede Aufnahme mit dem zur Laufzeit **tatsächlich geladenen** DLL-Hash,
   Map-Hash, AIV-Hash, Spieler-ID, Keep-Slot und Sofortspawn-Wert binden.
   Erst dann aktuelle Constructor-/Mapperverträge und eine sequenzielle
   Offline-Simulation erweitern. Bis dahin bleiben spätere KI-Spieler mit
   Sofortspawn `NotEvaluable`.

## Laufzeitaufnahme 2026-09-23 und Diagnosekorrektur

Auf `Crater Lake.map` (SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`) mit der installierten Native-DLL `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` wurden in einem Prozess zwei Kartenstarts mit je sieben KI-Spielern aufgenommen. Bei `advopt_pre_build=1` protokollierte der Detector sieben Auswahlen und sieben Bauabläufe, aber in allen Frames `pointerProblemFrames=frames`, `placementStateAddress=0x0` und `frameSnapshotsComplete=false`. Die gemeldeten null Tile- und Gebäudedifferenzen sind deshalb kein Nachweis unveränderten Bauzustands. Die Ursache lag im Diagnosefilter: `PlayerId=-1` wurde für die Zeigerbeobachtung als konkrete ID statt als Wildcard verglichen. Der Filter wurde an die bereits korrekte Wildcard-Behandlung der Bau-Frame-Aufnahme angepasst und der Detector neu installiert. Eine Laufzeitbestätigung dieser Korrektur steht noch aus.

Bei `advopt_pre_build=0` wurden erneut sieben Native-Auswahlen erfasst. Die letzte Lobby-Generation wertete alle Kandidaten aus, darunter `21/21` beim Emir. Die sieben tatsächlich gewählten Drehungen liegen jeweils in den vorhergesagten möglichen Drehungen. Da die Kandidatenwahl vom Zufallsstart abhängt, bleibt das zusammengefasste Autoergebnis `NotEvaluable`; das sagt nichts gegen die Einzel-Fits. Ein exakter Score-Vergleich sämtlicher neuer Cell-Traces gegen die Offline-Auswertung wurde für diese Aufnahme noch nicht abgeschlossen.

Die erneute Crater-Lake-Aufnahme um 16:32 bestätigte den Fix zur Laufzeit: Für alle sieben KI-Spieler sind `frameSnapshotsComplete=true`, `provenanceComplete=true`, `pointerProblemFrames=0` und `captureErrorFrames=0`. Die erste gebaute Burg änderte 4.230 Werte in den erfassten Tile-Schichten und 203 Gebäudedatensätze. Der unmittelbar folgende Start mit `advopt_pre_build=0` verwendete dieselbe Karte und Besetzung. Bei 41 gemeinsamen (Spieler, Kandidat, Drehung)-Fitversuchen waren nativer Status, Rohscore, Prozent, Zellzahl und blockierte Zellzahl gleich. Bei allen 41 erfassten Fitversuchen späterer KI-Spieler mit Sofortspawn war die Schnittmenge zwischen den zuvor geänderten Tile-IDs und den vom Validator gelesenen Tile-IDs leer. Das belegt räumliche Unabhängigkeit für den tatsächlich beobachteten Auswahlpfad auf dieser Karte, aber noch keine solche Garantie für alle möglichen zufallsabhängigen Varianten oder Mapper-Bauwirkungen. Die Lobby lässt spätere KI-Spieler bei Sofortspawn deshalb weiterhin `NotEvaluable`. Eine vollständige Vorhersage benötigt entweder eine belegte Obergrenze aller möglichen Bauänderungen oder eine exakte Simulation aller möglichen vorherigen Bauzustände.

Der erneute Audit am selben Native-Hash bestätigt für die Fit-Kette `0x57080` -> `0x7B060` den einzelnen geprüften Tile pro AIV-Zelle (Spieler-ID 0, Modus 0). Der Sofortspawn-Pfad `0x51790` -> `0x5CD90`/`0x6D580` kann jedoch bestehende Strukturen entfernen und verzweigt in typspezifische Konstruktoren. Deren gesamte Tile-Schreibreichweite und Rückwirkungen sind für alle Varianten noch nicht belegt; der lokale Fixes-Mod verlagert zusätzlich den OrderedMapTileIds-Puffer. Deshalb wird aus den 41 gleichen Ergebnissen keine allgemeine räumliche Freigabe abgeleitet. Weitere unveränderte Crater-Lake-Spielstarts sind für diese Frage derzeit nicht erforderlich; zuerst muss der statische Schreibpfad begrenzt oder eine gezielt fehlende Variante identifiziert werden. Der reproduzierbare Read/Write-Abgleich liegt unter [`Compare-PrebuildReadWrite.ps1`](../Findings/AIVPlacement/Compare-PrebuildReadWrite.ps1); er prüft zuerst die Datei-Provenienz und vollständige Frames und meldet Überschneidungen ausschließlich für den tatsächlich aufgenommenen Auswahlpfad.

## Gezielte Variantenlücke, Audit vom 2026-09-23

Der erneut geprüfte installierte Native-Hash ist `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Der aktuelle Lobby-Selector ermittelt für den ersten KI-Spieler auf Crater Lake genau die möglichen Kandidaten 4 und 5, beide mit Drehung 270 Grad. Kandidat 4 ist `nizar5.aivjson` (SHA-256 `75EC63581C8D2D6A4DCF2A67C44690AF4080F597D3356B84503EC68944BB3CA7`) und wurde vollständig gebaut und erfasst. Kandidat 5 ist `nizar6.aivjson` (SHA-256 `1D86B141261297706AD65F60D870A3AE30A8C0DA05B6B3B1D4D923C702CB11AD`); für ihn fehlt ein Bau-Trace. Die Dateien haben 259 beziehungsweise 291 Bau-Frames und 1.509 beziehungsweise 1.329 unterschiedliche AIV-Positionsoffsets. 369 Offsets kommen nur in `nizar5`, 189 nur in `nizar6` vor. Gleiche Keep-Position und Drehung bedeuten daher keine gleiche Bauwirkung. Diese Zahlen beschreiben den AIV-Plan, nicht die tatsächlichen nativen Tile-Schreibmengen.

Der aktuelle `0x6D580`-Pfad (`VA 0x18006D580`) ruft vor seiner typspezifischen Konstruktor-Verteilung `0x77E60` (`VA 0x180077E60`) auf und kann nach dessen Ergebnis abbrechen. Danach bereinigt er pro Footprint-Tile unter anderem Logik-Flags, Terrainwerte und bestehende Gebäudeketten und ruft je nach gemapptem Strukturtyp verschiedene Konstruktoren auf. Der Decompiler-Export enthält für `0x77E60` keine abgeschlossene Funktion (`Flow exceeded maximum allowable instructions`); die Ghidra-Referenzen zeigen dort 16 Aufrufe von `0x69850`, 16 Aufrufe von `0x7B060` und weitere Abzweige. Ein aus den AIV-Offsets abgeleiteter fester Sicherheitsabstand wäre deshalb derzeit eine unbelegte Schranke. Vertrauen: hoch für den direkten `0x6D580`-Kontrollfluss und die Variantenlücke, mittel für die vollständige Schreibreichweite. Der Produktionscode bleibt bei späteren KIs mit Sofortspawn folgerichtig `NotEvaluable`.

Die vorherige Aufforderung, die zufällige Acht-Spieler-Aufstellung „unverändert“ zu wiederholen, war ohne Positionsangaben nicht ausführbar. Die Kartenmetadaten und die vollständigen Build-Traces erlauben inzwischen diese Rekonstruktion; `S` ist der nullbasierte Karten-Slot, während `P` die Spieler-ID beziehungsweise KI-Reihenfolge ist:

| P | Lord | S | Radarposition | nativer Keep | AIV-Modus | tatsächlich gewählte AIV im Sofortspawn-Lauf |
| ---: | --- | ---: | --- | --- | --- | --- |
| 1 | Mensch | 0 | `(150,177)` | `(654,448)` | – | – |
| 2 | Nizar | 6 | `(90,30)` | `(241,274)` | Default | `nizar5.aivjson` |
| 3 | Wolf | 5 | `(17,110)` | `(254,580)` | Default | `wolf8.aivjson` |
| 4 | Marschall | 2 | `(161,101)` | `(525,274)` | Custom | `marshal2.aivjson` |
| 5 | Nox | 1 | `(170,33)` | `(406,121)` | Custom | `nox 1.aivjson` |
| 6 | Abt | 4 | `(43,50)` | `(187,407)` | Default | `abbot2.aivjson` |
| 7 | Jewel | 3 | `(105,84)` | `(379,353)` | Default | `jewel5.aivjson` |
| 8 | Nomade | 7 | `(37,174)` | `(423,667)` | Default | `nomad4.aivjson` |

Die Positionsgrafik [`CraterLake-Referenzsetup.png`](../Findings/AIVPlacement/CraterLake-Referenzsetup.png) zeigt die Radarplätze. Der Screenshot der neuen Lobby zeigt dieselben Kartenplätze, aber eine andere Spielerfolge: Mensch, Nox, Marschall, Jewel, Abt, Wolf, Nizar, Nomade. Nizar ist dort P7 und Wolf P6. Das ist als räumlich getrennter Kontrollfall geeignet, nicht als isolierter Nizar-zu-Wolf-Bauvergleich. Die native Spielerreihenfolge bleibt bei Sofortspawn relevant, selbst wenn die geplanten Burgflächen nicht überlappen. Für die acht Kartenplätze beträgt die kleinste Chebyshev-Distanz zweier nativer Keep-Anker 133 Tiles (Abt/Nizar). Die projizierten 100×100-AIV-Raster können sich an diesen Ankern daher nicht direkt überschneiden; native Konstruktor- und Bereinigungseffekte außerhalb des Rasters sind damit noch nicht begrenzt.

Für die eingebaute Nizar-Auswahl heißen die relevanten Einträge in der **Lobby** `Default 5` und `Default 6`. Der Detector bezeichnet ihre äquivalenten Editor-Exporte als `nizar5.aivjson` beziehungsweise `nizar6.aivjson`. Das ist nicht gleichbedeutend mit einer lokal installierten Extended-AIV namens `nizar5` oder `nizar6`. `CustomisationFileManager.BuildExtendedLordDirectory` erzeugt `Default N` aus `AIVLoader.getAIVData(lordType, N-1)`; separat eingelesene Dateien erhalten den Dateinamen als Anzeigenamen. Soll nur eine eingebaute Variante zugelassen werden, muss die User-Auswahl genau den betreffenden `Default N`-Eintrag enthalten. Dabei kann die native Kandidaten-ID neu nummeriert werden; Datei-/Datenhash, Drehung und native Startoptionen müssen später verglichen werden.

Ein isolierter Vergleich könnte mit `Crater Lake`, P1 Mensch auf S0, P2 Nizar auf S6 und P3 Wolf auf S5 erfolgen; weitere Spieler bleiben leer. Drei Kartenstarts in einem Prozess würden die Fit-Basis ohne Sofortspawn und beide einzeln erzwungenen Nizar-Bauvarianten abdecken. Diese Messung wird erst angefordert, wenn der weitere statische Konstruktor-Audit bestimmt hat, welche fehlende Beobachtung für eine sichere Freigabe tatsächlich benötigt wird. Weitere identische Starts auf der großen Karte liefern allein keinen Beweis für eine allgemeine Schreibreichweite. Die Diagnose erfasst synchronen Sofortbau bereits beim Laden der Karte; ein Warten auf regulären KI-Bau ist nicht nötig.
Weiterführende Quellen: [aktuelle Native-Baseline](../_inspect/CrusaderDE-Native-Baseline/CURRENT.md),
[aktueller AIV-Auswahlaudit](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/AIV_LOBBY_SELECTION.md),
[Fit-Regelinventar](../Helpers/MapParser/Docs/AIV_PLACEMENT_RULES.md) und
[historischer Sofortspawn-Audit](../Helpers/MapParser/Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md).

## Crater-Lake-Aufnahme vom 23.09.2026, 17:58 Uhr

Erneut geprüft: installierte Native-DLL
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
`Crater Lake.map` mit SHA-256
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
Ein Kartenstart erfolgte mit `advopt_pre_build=1`, ein zweiter mit `0`.
Der Detector nahm beim eingeschalteten Sofortspawn alle sieben KI-Auswahlen,
sieben Bau-Sequenzen und 47 native Fitversuche auf. Alle sieben Bau-Traces
melden `frameSnapshotsComplete=True`, `provenanceComplete=True` und je null
Pointer- und Aufnahmefehler. Der Vergleichslauf ohne Sofortspawn enthält
42 Fitversuche. Die Dateihashes der AIVs stehen in den einzelnen Oracle-
und Bau-Traces; die bloße Kandidatennummer ist nur innerhalb der jeweiligen
Auswahl aussagekräftig.

| Spielerfolge | Lord | Keep | mit Sofortspawn gewählte AIV | Drehung | Bau-Frames | Tile-Werte / Gebäudedatensätze geändert |
| ---: | --- | --- | --- | ---: | ---: | ---: |
| 2 | Nox | `(406,121)` | `nox 1` (Custom) | 180° | 504 | 6.795 / 123 |
| 3 | Marschall | `(525,274)` | `Default 7` | 180° | 80 | 3.159 / 46 |
| 4 | Jewel | `(379,353)` | `jewel1` (Custom) | 180° | 125 | 5.073 / 52 |
| 5 | Abt | `(187,407)` | `Default 2` | 270° | 118 | 5.225 / 57 |
| 6 | Wolf | `(254,580)` | `wolf8` | 0° | 337 | 3.543 / 295 |
| 7 | Nizar | `(241,274)` | `Default 5` | 270° | 260 | 4.230 / 203 |
| 8 | Nomade | `(423,667)` | `nomad1` | 0° | 106 | 907 / 79 |

Für 39 in beiden Läufen gemeinsame Kombinationen aus Spieler, AIV-Dateihash
und Drehung stimmen nativer Status, Rohscore, Fit-Prozent, geprüfte und
blockierte Zellzahl exakt überein. Von den 47 Cell-Traces mit Sofortspawn
betreffen 46 spätere KI-Spieler. Bei keinem dieser 46 überschneiden sich die
von `0x7B060` tatsächlich gelesenen Tile-IDs mit den in vorherigen erfassten
`0x51790`-Frames geänderten IDs der acht beobachteten Tile-Schichten. Der
Abgleich erfolgt über die Spielerfolge aus den Trace-Metadaten, ohne fest
eingetragene Spieler-ID. Der Vergleichshelfer ignoriert Fit-Traces mit
`preBuildSetting=0`, wenn beide Kartenstarts im selben Zeitmuster liegen.

Dies ist ein **beobachteter** Kontrollfall mit räumlich getrennten Starts.
Insbesondere wurde Nizar erst als Spieler 7 gebaut; seine Burg kann in
diesem Lauf die bereits erfolgte Wolf-Auswahl nicht beeinflussen. Auch
`nizar6.aivjson` / Lobby-Name `Default 6` wurde nicht gebaut. Die Aufnahme
begrenzt weder die Schreibreichweite aller ungewählten AIV-Varianten noch
alle Nebenwirkungen der nativen Konstruktoren. Die acht Tile-Schichten und
ausgewählten Gebäudefelder sind keine vollständige Spielzustandskopie.
Spätere KI-Fits mit Sofortspawn bleiben daher im Produkt `NotEvaluable`.
Für weitere Diagnose gilt weiter: alle aktiven Spieler und Kandidaten mit
Map-, AIV- und Native-Hash protokollieren; keine feste Spieler-ID oder
zufällig beobachtete AIV als allgemeine Voraussetzung einbauen.

Der aus genau diesen beiden Kartenstarts importierte
[`Oracle-Korpus`](../Findings/AIVPlacement/CraterLake-20260923-1758-Oracle/unknown.json)
enthält 89 Native-Versuche. Der unveränderte Offline-Vergleicher meldet
43 `ExactMatch` (alle 42 Versuche ohne Sofortspawn plus den ersten mit
Sofortspawn), 46 absichtliche `NotEvaluable` für spätere Spieler mit
Sofortspawn, null Abweichungen und null Auswertungsfehler. Der
[`Vergleichsbericht`](../Findings/AIVPlacement/CraterLake-20260923-1758-Oracle/report.json)
und der dazu isolierte [`Logabschnitt`](../Findings/AIVPlacement/CraterLake-20260923-1758.log)
halten diese Prüfung reproduzierbar fest. Die 46 gesperrten Fälle sind keine
fehlgeschlagenen Fit-Vergleiche; deren Zustand wird bewusst nicht simuliert.

### Gezielter Folgetest ohne feste Spieler-ID

Der noch nicht erfasste eingebaute Nizar-Kandidat `Default 6` lässt sich mit
zwei Kartenstarts in einem Spielprozess prüfen. In `Crater Lake` bleiben ein
menschlicher Spieler am östlichen Keep `(654,448)`, Nizar am nordwestlichen
Keep `(241,274)` und Wolf am südwestlichen Keep `(254,580)` aktiv; alle
anderen KI-Plätze können leer sein. Entscheidend ist nur, dass Nizar in der
Lobby-Reihenfolge **vor** Wolf steht. Nizar erhält im Benutzer-AIV-Modus
ausschließlich den Eintrag `Default 6` (eingebaute AIV), Wolf eine unveränderte
Auswahl. Dieselbe Aufstellung wird einmal ohne und einmal mit `Completed
Castles` kurz bis zum geladenen Kartenbildschirm gestartet. Die Diagnose
zeichnet weiterhin alle tatsächlichen Spieler-IDs, Kandidaten, Hashes und
Bau-Frames auf; keine ID oder AIV wird im Code fest verdrahtet. Danach sind
insbesondere Nizars Dateihash und Wolfs Native-Fit zu vergleichen. Auch
dieses Paar wäre ein gezielter Variantenbeleg, kein Beweis für beliebige
Maps und Konstruktoren.

Für weitere Aufnahmen wurde die opt-in Cell-Trace-Standardquote am 23.09.2026
von 256 auf 2048 pro Kartenstart erhöht und der Detector über seine
`build.bat` erfolgreich installiert (0 Warnungen, 0 Fehler). Die vorhandene
lokale Konfiguration verwendet nun ebenfalls 2048, beide `PlayerId`-Filter
stehen auf `-1`, und die sieben KI-Bausequenzen bleiben innerhalb der Quote
von acht. Die installierte DLL stimmt per SHA-256 mit dem Build-Artefakt
überein. Ein erneuter Ingame-Lauf dieser Quotenerhöhung steht noch aus.

### Crater Lake, Default 6, erhöhter Burggraben und Zugbrücke (24.09.2026)

Der gezielte Lauf auf Crater Lake (Map-SHA-256
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`,
Native-SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`)
erfasste Nizar `Default 6` / `nizar6.aivjson` mit Dateihash
`1D86B141261297706AD65F60D870A3AE30A8C0DA05B6B3B1D4D923C702CB11AD`,
Keep `(241,274)`, Drehung 270 Grad und den später folgenden Wolf
`Default 8` / `wolf8.aivjson`, Keep `(254,580)`, Drehung 0 Grad.
Mit Sofortspawn waren beide Bauaufnahmen vollständig (292 bzw. 337 Frames,
null Pointer- und Aufnahmefehler); ohne Sofortspawn hatten beide nativen
Fits weiterhin den vollständigen Score 999999 mit null blockierten Zellen.
In dieser Konfiguration schneiden sich die für Wolfs Fit gelesenen Zellen
nicht mit Nizars aufgezeichneten Änderungen der acht Validator-Schichten.
Das ist eine Beobachtung für diese zwei ausgewählten AIVs, kein Beweis für
alle Varianten oder angrenzende Starts. Die Sperre späterer KI-Spieler bei
Sofortspawn bleibt bestehen.

Nizars Burggraben-Mapper 106 wurde in den Frames 15, 65 und 66 mit
368, 474 und 183 geplanten Positionen aufgerufen. Zellweise änderten sich
367, 474 und 181 Höhenwerte, insgesamt 1.022 verschiedene
Frame-Zelländerungen. Alle protokollierten Höhenwechsel waren `130 -> 122`
und hatten begleitende Logikänderungen. ExtraFeatures meldete seinen
KI-Höhen-Patch aktiv. Die drei unveränderten Positionen und weitere
Bau-Prüfungen sind nicht aus dem bloßen Frame-Return ableitbar; der
Frame-Returnwert 0 ist kein Beweis für unveränderte Tile-Schichten.
Nizars Zugbrücken-Mapper 105 wurde in Frame 28 mit einer AIV-Position
aufgerufen. Der Trace zeigt 25 neu belegte Building-ID-Zellen und
15 Höhenwechsel `130 -> 122`. Für die übrigen Footprint-Zellen folgt
aus dem fehlenden Höhenwechsel keine sichere Aussage über ihre Bauhöhe.
Die Auswertung beruht auf dem vollständigen nativen Differenztrace mit
hoher Sicherheit für die beobachteten Änderungen. Die Offline-Projektion zählt 1.025 Burggraben-Zellen und 25 Zugbrücken-Footprint-Zellen, deren ursprüngliche Map-Höhe jeweils 130 und damit über der Vanilla-Baugrenze 12 liegt.
Die geplanten
Projektionszellen und ihre **ursprüngliche** Map-Höhe werden im Offline-Code
nur als potentielles späteres Bauhindernis ausgewiesen; das ist keine
vollständige Simulation aller vorgelagerten Bau-Frames.
Seit 25.09.2026 wird daraus allein kein UI-Höhenhinweis mehr abgeleitet.
Dieser erfordert einen nachweislich deaktivierten ExtraFeatures-KI-Patch,
eindeutigen Vanilla-Kandidaten samt endgültiger Drehung sowie belegte Höhe
über 12 unmittelbar beim Bau-Frame. Der Worker liefert für letzteren
Nachweis noch keinen positiven Wert. Der aktuelle Umsetzungsstand und die
fehlenden Nachweise für weitere Baukonflikte stehen unter
`Findings/AIVPlacement/STATUS.md`.
Der Detector-Build vom 24.09.2026 entfernte die beiden Roh-Trace-Ordner unbeabsichtigt. Die zuvor ausgelesenen Frame-Summen und der archivierte Spiel-Log liegen vor, ein erneuter exakter Join jeder projizierten Zelle mit dem nativen Differenztrace ist aus diesen Artefakten nicht mehr möglich. Das Buildskript bewahrt künftig alle nicht mitgelieferten Ordner auf; für diesen Join wäre eine neue Aufnahme erforderlich.

Der Native-Fit `0x57080 -> 0x7B060` verwendet keine Burggraben-Sondergrenze
bei Höhe 12. Erst der Baupfad `0x51790 -> 0x59730` berücksichtigt diese
physische Höhe; ExtraFeatures ändert diesen Baupfad für KI-Spieler, wenn
sein Hook installiert und logisch aktiv ist.
Für Zugbrücken benutzt `0x51790` den Gebäudepfad `0x6D580 -> 0x739C0`;
der Höhenfehlerpfad bei `0x7870B` folgt auf Mapper 105 und eine maximale
Bauhöhe über 12. Derselbe aktive ExtraFeatures-Hook unterdrückt diesen
Fehler für KI-Spieler. Das `IsElevated`-Logikbit ist
von einer physischen Höhe über 12 zu unterscheiden. Die Fit-Farbe bleibt
eine Aussage über Vanillas Kandidatenprüfung. Ein separater Tooltip nennt
hohe geplante Burggraben- und Zugbrücken-Zellen je Drehung und den tatsächlich bekannten
ExtraFeatures-Zustand. Für eine nicht ausgewertete AIV wird kein Bauhinweis
behauptet.

### Neuer Zellabgleich und Serienplan vom 24.09.2026

Nach dem erneuten Crater-Lake-Start liegen vollständige Roh-Traces unter
`ActiveAIVDetector_Serp/PrebuildTraces` und `CellTraces` vor. Für Nizar
`Default 6` mit 270 Grad wurden die projizierten Core-Footprint-Tile-IDs
gegen die Bauänderungen derselben Aufnahme verglichen: Mapper 106 hat
1.025 projizierte Burggraben-Zellen, 1.022 unterschiedliche Zellen mit
nativer Höhenänderung, eine Schnittmenge von 1.022 und drei projizierte
Zellen ohne beobachtete Höhenänderung. Mapper 105 hat 25 projizierte
Zugbrücken-Footprint-Zellen und 25 neu belegte Building-ID-Zellen;
die Schnittmenge beträgt exakt 25. Keine beobachtete Zelle liegt
außerhalb der jeweiligen Projektion. Der Vergleich gilt für die konkret
gewählte Burg und den wirksamen ExtraFeatures-KI-Höhen-Patch; die drei
nicht geänderten Burggraben-Zellen werden nicht als fehlgeschlagener
Bau interpretiert. Vertrauen: hoch für diesen Zell-Join, offen für andere
Varianten, Karten und deaktivierten Patch.

Der spätere Fit von Wolf `Default 8` liest in dieser Aufnahme keine der
zuvor von Nizars Bau veränderten Validator-Tile-IDs. Beide Bau-Traces
haben `frameSnapshotsComplete=True`, `provenanceComplete=True` und null
Pointer-/Aufnahmefehler. Die vier Vor-/Nach-Scans benötigten für Nizar
2128,0/2491,9 ms und für Wolf 2488,3/2900,9 ms. Der größte beobachtete
Zeitanteil ist damit die vollständige Diagnoseaufnahme; sie bleibt für
die geplante Testserie vorerst aktiv.

Die nächste Serie verwendet fünf feste Aufstellungen mit sieben KIs,
jeweils bei ausgeschaltetem und eingeschaltetem Sofortspawn: Crater Lake
mit festen Default-Varianten, mit alternativen Varianten und mit
umgekehrter KI-Reihenfolge; danach Craggy Cliffs mit beiden
Variantenmengen. Die Karten-SHA-256 stehen in der editierbaren
Testserien-Datei. Dichte Starts auf der gesonderten Überlappungskarte
bleiben zurückgestellt. Der Testmod protokolliert den Run-Namen und
setzt den Fortschritt erst nach bestätigtem Matchstart fort.

### Zehn-Match-Serie vom 24.09.2026: Ergebnis

Alle zehn Presets wurden mit der vorgesehenen Karte, KI-Reihenfolge,
Keep-Zuordnung, AIV-Auswahl und „Completed Castles“-Option bestätigt; die
Fortschrittsdatei endet bei `CC-B-on` und `nextIndex=10`. Der versehentliche
zweite Start ohne Sofortspawn zwischen `CL-A-off` und `CL-A-on` wurde nicht
als Serienlauf gezählt. Die spätere freie Nutzung der Lobby nach Serienende
gehört ebenfalls nicht zur Auswertung. Native-DLL-SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Crater Lake hat Map-SHA-256 `C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`,
Craggy Cliffs `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.

Der [Serienbericht](../Findings/AIVPlacement/AivSeries-20260924/RESULTS.md) und die
getrennten Oracle-Korpora halten 119 Native-Fitversuche fest: 57 exakte
Offline-Vergleiche, 59 bewusst gesperrte Fälle nach vorherigem Sofortbau,
drei Abweichungen und null Auswertungsfehler. Die drei Abweichungen treten
ohne Sofortbau auf Craggy Cliffs bei Emir `Default 1` (0° und 180°) und
Jewel `Default 1` (270°) auf. Bei Jewel ist auch der Prozentwert verschieden:
Vanilla 95 %, Offline 94 %. Unmittelbar vor den Fitversuchen erfasste native
Gebäuderaster widerlegen einzelne vom Offline-Code rekonstruierte Zellen
früherer Startgebäude. Die bisherige affine 13x13-Abbildung reicht für
nahe Kandidaten also nicht aus. Die Abweichung darf nicht durch passend
gemachte Score-Zähler verdeckt werden; zuerst sind der native Startbaupfad
und alle betroffenen Tile-Schichten zu klären.

Alle fünf Sofortbau-Läufe haben vollständige und konsistente Bau-Frame-
Aufnahmen ohne Pointer- oder Capture-Fehler. Bei `CC-A-on` sind es sechs
statt sieben Bau-Sequenzen, weil Emir `Default 1` in allen vier Drehungen
abgelehnt wurde und keine Burg bekam. Bei Crater Lake bleiben 31 in den
aus/an-Paaren gemeinsame Native-Fits unverändert, ohne beobachtete
Schnittmenge zwischen früheren Tile-Änderungen und 28 späteren Fit-Lesespuren.
Craggy Cliffs liefert den Gegenfall: 5/13 beziehungsweise 6/11 gemeinsame
Native-Fits ändern sich; 8/15 beziehungsweise 12/16 spätere Fit-Traces
schneiden die vorherigen beobachteten Tile-Änderungen. Damit ist die Sperre
späterer KIs bei Sofortspawn weiterhin nötig, und ein einfacher
Crater-Lake-Abstandsbeleg wäre keine allgemeine Freigabe.

**Stand vor der folgenden Nachprüfung:** `0x94350 -> 0x6D580 -> 0x77E60`
für die native Startgebäude-Konstruktion einschließlich Abbrüchen und
Footprints zu Ende auditieren und die vorhandenen Live-Gebäuderaster
zellweise gegen das Offline-Modell verwenden. Danach entweder das Modell
belegen und korrigieren oder betroffene Lobby-Fälle gezielt `NotEvaluable`
setzen. Die 119 bereits archivierten Fälle erneut ausführen. Erst nach
dieser Korrektur wären die beiden Craggy-Cliffs-Paare als wenige gezielte
Ingame-Wiederholungen sinnvoll; unveränderte weitere Zehn-Match-Serien
bringen derzeit keinen zusätzlichen Beleg. Die Spezialkarte mit
überlappenden Starts bleibt zurückgestellt.

## 24.09.2026: KI-Startzustand nach Vergleich mit den Live-Rastern

Die installierte Native-DLL hat weiterhin SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Der Startpfad entfernt zuerst serialisierte Startobjekte (`0xC43A0`,
`0xC3FA0`), wählt und importiert die AIV (`0x54F60`/`0x54DE0`, `0x53D00`)
und ruft danach `0x6D580 -> 0x77E60` für den Startbau auf. Ein
fehlgeschlagener Validator lässt den Bau aus; der erfolgreiche Pfad
verändert Gebäude-, Besitzer- und Terrainbelegung sowie benachbarte
Pfad-Zellen. Die vielen Validatorzweige sind nicht vollständig auf
Offline-Eingaben abgebildet. Vanillas Autoauswahl oder CastlePlanners
Spielerrotation beweisen deshalb für sich keine gebauten Startzellen.

Auf Craggy Cliffs belegen die serialisierte Karte und die nativen
Vor-Fit-Raster den konkreten Versatzfehler: Keep und 7x7-Startlager
bleiben bei 0 Grad an ihren Quellzellen; bei 270 Grad rotiert die
Startgruppe relativ zum Keep mit `(13-y,x)` statt `(12-y,x+1)`.
Für 90 Grad stimmt der bestehende Offset mit einem beobachteten
7x7-Paar überein. 180 Grad und gescheiterte Bauten bleiben offen.
Das 7x7-Startlager ist im Objektabschnitt Typ 55 und nicht der
separate Goods Yard Typ 10. Vertrauensgrad: hoch für die genannten
Zell- und Typbeobachtungen, begrenzt für andere Karten und Abbruchpfade.

Der gemeinsame `AIVPlacement.Core` korrigiert die belegten 0- und
270-Grad-Offsets. CastlePlanner zeigt für Kandidaten mit Lesezellen
nahe einem zuvor rekonstruierten KI-Start `NotEvaluable` mit
`StartOverlapUnproven`. Die Grenze umfasst den Keep im 24-Zellen-Umkreis
sowie serialisierte und modellierte Startzellen mit vier Zellen Umgebung.
Ein nicht kanonischer AIV-Startmarker sperrt spätere KI-Fits vollständig,
bis dessen Bauzustand belegt ist. Diese Grenzen verhindern
eine sichere Farbaussage aus ungeklärten Konstruktorzweigen; sie ist
keine vollständige Simulation. Spätere KIs nach aktiviertem Sofortbau
bleiben wie bisher gesperrt. Der Spieler-Keep/Storageyard-Rotationspfad
und Vanillas AIV-Auswahl werden nicht verändert.

Der lokale Fixes-Mod kann im Keep-Spawntail den Goods Yard je Spieler
unterdrücken; CastlePlanner gleicht für manuelle Spielerrotation dessen
Rotationsdaten zeitweise ab. Dieser Pfad liefert keinen Beleg für die
KI-Startkonstruktion. Die archivierten 119 Oracle-Versuche sind nach
der Korrektur erneut zu vergleichen. Weitere Craggy-Starts sind erst
nötig, wenn ein konkret offener Konstruktorzweig für eine gewünschte
Fit-Freigabe gemessen werden muss.

Der erneute Vergleich der festgelegten 119 Versuche ergibt 38 exakt,
81 `NotEvaluable`, null Abweichungen und null Fehler. Im vollständigen
Import mit dem aus der Serie ausgeschlossenen Zusatzlauf sind es
47/82/0/0 bei 129 Versuchen. Die Berichte liegen unter
`.inspect/oracle-crater-safe.json` und `.inspect/oracle-craggy-safe.json`.

## 24.09.2026: Korrektur des nativen KI-Startkonstruktors

Für die installierte DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
ist Mapper `0x3d` über `0xC77C0` als Gebäudetyp `0x29` (41)
aufgelöst. `0x6D580` ruft nach erfolgreichem `0x77E60` deshalb den
**zusammengesetzten Keep-Konstruktor `0x74DA0`** auf. Der vorherige
Hinweis auf einen generischen Konstruktor war ungenau.

Ein erfolgreicher Aufruf erzeugt den 7x7-Keep, drei verknüpfte
Einzelzellen, ein getrenntes 7x7-Startlager (Typ 55) und über
`0x76E80` den Goods Yard mit vier 2x2-Gebäudeteilen und neun
weiteren Zellen. Die nativen Rotations-Offsets des Startlagers relativ
zum Keep-Anker sind bei 0/90/180/270 Grad `(0,+8)`, `(+8,0)`,
`(0,-8)`, `(-8,0)`. Für den Goods-Yard-Anker sind es `(+7,+2)`,
`(+2,-5)`, `(-5,0)`, `(0,+7)`. Die drei Einzelzellen und die
Tabellenadressen stehen in der Native-Baseline. Der Konstruktor räumt
vor dem Schreiben auch die 7x7- beziehungsweise 5x5-Zielbereiche.
Diese festen Offsets erklären die Lagerbelegung der archivierten
Craggy-Raster, beweisen aber keine starre Rotation aller serialisierten
Gebäude. Vertrauensgrad: hoch für den erfolgreichen nativen Pfad.

Für den KI-Start verwendet `0x77E60` bei Mapper `0x3d` seinen
Default-Zweig. Er berücksichtigt lebende Einheiten, Abstände zu anderen
Spielern, Erreichbarkeit und Tile-Regeln. Ein Fehlschlag stoppt
`0x6D580` vor der Konstruktion. Der vollständige Abbruchvertrag lässt
sich aus den vorhandenen Vor-Fit-Rastern und Bau-Diffs noch nicht
allgemein ableiten. Die lokale Fixes-Einstellung kann außerdem den
Goods Yard je Spieler unterdrücken. **Folge für den Mod:** Die bestehende
Sperre nahe früheren KI-Starts bleibt nötig; allein aus Drehung und
Quellkarte darf dort keine zusätzliche grüne Bewertung folgen.
Vertrauensgrad: hoch für Abhängigkeiten und Stopppunkt, begrenzt für
eine Offline-Vorhersage jedes Startabbruchs.

Die 119 archivierten Oracle-Versuche reichen aus, um die derzeit
freigegebenen Fälle auf null beobachtete Abweichungen zu prüfen. Sie
reichen nicht aus, um die offenen Validator- und Fixes-Zweige für alle
Karten freizugeben. Eine neue, unveränderte Zehn-Match-Serie wäre
deshalb nicht sinnvoll. Falls der genaue Startzustand in dichtem
Gelände statt `NotEvaluable` angezeigt werden soll, muss die
Validatorentscheidung mit ihren Eingaben und ihrem Fehlercode an
gezielt gewählten Starts erfasst oder der komplette Validator samt
Live-Eingaben offline rekonstruiert werden. Bis dahin bleibt die
graue Grenze bewusst bestehen.

Die nächste Messung ist im `ActiveAIVDetector` vorbereitet: Der bereits
vom Script Extender veröffentlichte `OnBuildStructure`-Event umschließt
den nativen Startbau bei Mapper `0x3d`. Der Detector liest pro Spieler
den nativen Status und Fehlergrund vor und nach dem Aufruf sowie Vorher/Nachher-Werte
der acht fitrelevanten Tile-Schichten in einer begrenzten Region um
den Keep. Das geschieht ohne zusätzliche native Detours und unabhängig
von festen Spieler-IDs. Neue Spuren werden unter `StartTraces` abgelegt;
ihre tatsächliche Vollständigkeit ist durch die vier unten genannten
Laufzeitstarts für diese Serie belegt. Das Script-Extender-Event liefert den nativen Rückgabewert
selbst nicht, daher werden die nativen Statusfelder direkt gelesen.

## 24.09.2026: Vier Craggy-Wiederholungen mit Keep-Start-Traces

Die Serie `CC-A-off/on`, `CC-B-off/on` ist abgeschlossen. Für alle vier
Kartenstarts liegen jeweils acht vollständige regionale Vorher/Nachher-
Aufnahmen vor. Karte: SHA-256
`C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`;
native DLL: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Alle 57 erneut beobachteten Fitversuche stimmen in Score, Prozent und
blockierten Zellen mit dem archivierten Craggy-Korpus überein. Die
Rohaufnahmen samt Hashliste und relevantem Laufzeitlog liegen unter
`../Findings/AIVPlacement/AivSeries-20260924/StartRebuildRegression`.

Alle 32 Startaufrufe endeten mit Fehlerflag 0. Trotzdem enthielt das
Fehlergrundfeld in 24 Fällen einen Wert ungleich null. Der Detector
wertet einen Grund daher künftig nur bei gesetztem Fehlerflag als
Abbruch. `CC-A-on` hat für Emir keinen akzeptierten AIV-Fit
(`placementState=0`), obwohl sein Keep-Startaufruf ausgeführt wurde;
`finalCandidateId=0` allein bezeichnet dort keine gewählte Burg.

Die normalen erfolgreichen Starts änderten je 117 Gebäude-ID-Zellen
(7x7-Keep, drei Einzelzellen, 7x7-Startlager, vier 2x2-Yardteile).
Bei Emirs Start in `CC-A-on` kamen 60 gelöschte und acht ersetzte
vorhandene Gebäudezellen hinzu. Bei Jewel in `CC-B-on` wurden 16
vorhandene Zellen gelöscht. Die acht erfassten Fit-Schichten änderten
sich im Archiv höchstens 22 Kacheln vom jeweiligen Map-Keep entfernt.
Das ist **keine** allgemeine Native-Obergrenze. Die gemessenen
Drehungen 0, 90 und 270 Grad belegen erfolgreiche Pfade; ein
180-Grad-Start oder ein fehlgeschlagener Validator ist nicht erfasst.

Der vollständige Schreibpfad enthält vor dem Konstruktor eine
Footprint-Räumung, mögliche Löschung vorhandener Gebäuderecords über
`0x5D3A0`/`0xC4290`, `0x5D740` für Lager/Yard, `0x6FE90` und
Pfadaktualisierungen. Deren Auswirkung auf beliebige angrenzende
Gebäude und Live-Zustände ist noch nicht abschließend räumlich
begrenzt. Der Offline-Kern belässt deshalb den bisherigen Schutz nahe
früheren Starts; bei verschobenem Startmarker und bei späteren KIs nach
Sofortbau bleibt ein nicht beweisbarer Fit `NotEvaluable`. Ein bloßes
Verkleinern auf die in diesen vier Matches beobachteten Zellen wäre
keine sichere Verbesserung. Vertrauensgrad: hoch für die konkreten
Traces und den erfolgreichen Konstruktorpfad, begrenzt für einen
allgemeinen Offline-Schreibbereich oder Abbruchvertrag.

### Korrektur des Messbereichs und nächste gezielte Aufnahme (24.09.2026)

Die 32 archivierten Keep-Start-Traces decken nur ein 41×41-Fenster um den
jeweiligen Startaufruf ab (`-16..+24` in beiden Koordinaten). Ihr Marker
`sampledRegionComplete=True` bestätigt nur dieses Fenster. Die Aussage
„höchstens 22 Kacheln geändert“ gilt folglich **innerhalb des Fensters**;
außerhalb wurden Änderungen bisher nicht gemessen. Der Offline-Kern bleibt
unverändert vorsichtig, besonders bei früheren Starts, verschobenen
AIV-Startmarkern und Sofortbau.

Der Detector liest nun vor und nach jedem Typ-41-Start alle 320.800
Tile-IDs in den acht für den Fit relevanten Schichten. Nur tatsächlich
geänderte Zellen landen im Trace; die Scan-Zeiten und
`fullMapTileLayersComplete=True` machen Kosten und Vollständigkeit prüfbar.
Die Änderung nutzt weiterhin den passiven Script-Extender-Event und setzt
keine Spieler-ID oder Karte im Detector fest. Für den installierten Native-Hash
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
ist dies eine Messvorbereitung, noch kein neues Laufzeitergebnis.

Native-Nachprüfung: Der Typ-41-Konstruktor reicht an `0x6FE90` für seine
Keep- und Lagerzellen `param_6=0` weiter. Dessen direkte Schichtschreibungen
verwenden die über `0x6A620`/`0xE3360` bestimmte Zelle. Die gesamte
Räumung, vorhandene Gebäuderecords, Yard-Konstruktion und nachgeschaltete
Pfad-/Nachbarfunktionen sind damit noch nicht allgemein räumlich bewiesen.
Die nächste Aufnahme soll zuerst den bisher unsichtbaren Außenbereich
prüfen; erst danach lässt sich eine sichere Vereinigung möglicher
Startänderungen für spätere Fits erwägen. Vertrauensgrad: hoch für die
Aufrufargumente und die Lücke des bisherigen Messfensters, zur Laufzeit
noch offen für die neue vollständige Aufnahme.

Der optionale Überschneidungszweig `0x74DA0 -> 0x5D3A0` hängt am
Tile-Manager-Feld `+0x204E7FC`. Er prüft vorhandene Gebäude in Keep-,
Lager- und Yard-Zellen, markiert deren Records über `0xC4290` und läuft
danach über 3.999 Gebäuderecords mit `0xB8310`. Der Detector erfasst nun
auch dieses rohe Schalterfeld und die zugehörige Record-Marke
`+0x204E778` vor/nach dem Start. Die regionale Altaufnahme kann den
Effekt dieses Zweigs außerhalb ihres Fensters nicht ausschließen.

### Vollkarten-Nachmessung `CC-A-on` (24.09.2026)

Die automatische Einzelserie wurde bestätigt (`nextIndex=1`). Der neue
Detector nahm acht vollständige Keep-Start-Diffs über alle 320.800 Tile-IDs
auf; `incomplete=False`. Alle acht Starts hatten vor dem Aufruf den
Räumschalter `+0x204E7FC=1`, danach null, eine gesetzte
Record-Löschmarke und kein Fehlerflag. Außerhalb des früheren
41×41-Fensters änderte sich in diesem Lauf keine der acht Fit-Schichten.
Innerhalb sind alle acht Roh-Zelllisten identisch mit der früheren
`CC-A-on`-Aufnahme. Das Scannen benötigte je Start rund 18–22 ms.

Auch die sechs Sofortbau-Spuren waren vollständig, ohne Pointer- oder
Framefehler. Ihre Gebäude- und Fit-Schichtänderungen stimmen für
dieselbe Aufstellung zell- und framegenau mit dem ursprünglichen
Zehn-Match-Lauf überein. Von 16 nativen Fit-Versuchen späterer KIs lasen
acht Zellen, die ein früherer Sofortbau geändert hatte; nur einer las
auch Zellen eines früheren Keep-Starts. Acht Versuche berührten in diesem
konkreten Baupfad keine gemessene frühere Änderung. Der Offline-Vergleich
ergab einen exakten ersten Fall, 15 vorsichtig graue Fälle und keine
Abweichung. Alle Daten und Hashes liegen unter
`../Findings/AIVPlacement/AivSeries-20260924/FullGridProbeResults`.

Die Messung belegt Reproduzierbarkeit dieser einen Aufstellung, aber
keine allgemeine Schreibbereichsgrenze. Der Sofortbau-Pfad `0x51790`
ruft bei passenden Mappern `0x5CD90` auf. Dessen Räumung kann über
`0xC43A0` verknüpfte Gebäuderecords und über `0xB8310`/`0x61FC0`
weitere typspezifische Tile-Updates auslösen. Daher wäre die bloße
Vereinigung aller geplanten AIV-Zellen keine sicher vollständige
Änderungsmenge. Spätere KIs bei „Completed Castles“ bleiben bis zu
einem bewiesenen Eingangsstatus `NotEvaluable`. Vertrauensgrad: hoch
für die genannten Traces und die geprüfte Native-Kette, begrenzt für
beliebige Maps/Mapper. Als nächster konkret fehlender Startzweig wird
eine 180°-Keep-Konstruktion mit Vollkarten-Diff aufgenommen; auf
Crater Lake ist eine solche Vanilla-Auswahl bereits archiviert.

## 24.09.2026: Sechs-Match-Serie und 180°-Startbau

Sechs automatisch bestätigte Starts auf Crater Lake und Craggy Cliffs
lieferten mit der unveränderten Native-DLL `FBCB9319…` 48 vollständige
Vollkarten-Start-Diffs und 28 vollständige Sofortbau-Traces. Alle 48
Startaufrufe hatten Fehlerflag null; bei keiner der acht Fit-Schichten
gab es in diesen Läufen eine Änderung außerhalb des früheren 41×41-
Fensters. Ein allgemeiner Schreibbereich oder Abbruchvertrag folgt
daraus nicht.

Für acht 180°-Starts mit kanonischem AIV-Keep-Marker `(56,43)` wurden
die 117 Gebäudezellen des serialisierten Startkomplexes mit dem nativen
Nachher-Raster zellweise abgeglichen. Alle acht passen exakt zu
`(keepX + 13 - dx, keepY + 13 - dy)`; mit Pivot 12 fehlen jeweils
31 Zellen. Der gemeinsame AIVPlacement-Kern wurde auf Pivot 13
korrigiert. Nichtkanonische Marker `(55,44)` und `(56,45)` führten
beobachtet zu anderen nativen Startankern; spätere Fits bleiben nach
solchen Markern `NotEvaluable`.

Die 69 neuen Native-Fitversuche ergeben nach der Korrektur 19 exakte,
50 absichtlich graue, null abweichende und null fehlerhafte Fälle.
Vollständige Traces und Vergleichsberichte:
`../Findings/AIVPlacement/AivSeries-20260924/SixMatchResults`. Vertrauen: hoch
für diese acht erfolgreichen 180°-Fußabdrücke und die native
Typ-41-Offsettabelle; offen für fehlgeschlagene Konstruktionen,
andere Marker sowie vollständige Sofortbauwirkungen.

Die installierte Pivot-13-Korrektur wurde anschließend mit zwei
bestätigten Crater-Lake-Starts (`CL-A-off/on`) geprüft. 20 Native-Fits:
10 exakt, 10 absichtlich `NotEvaluable`, keine Abweichung und kein
Importfehler. 16 Vollkarten-Starttraces und sieben Sofortbau-Traces
sind vollständig. Im Lauf ohne Sofortbau bleibt nur der letzte KI-Fit
wegen eines verschobenen früheren Startmarkers grau; bei Sofortbau
bleiben alle späteren KIs wegen des unbekannten Bauzustands grau.
Archiv: `../Findings/AIVPlacement/AivSeries-20260924/Pivot13RuntimeRegression/Observed`.
Vertrauen: hoch für diesen reproduzierten Lauf; keine neue allgemeine
Freigabe für andere Marker oder sequenziellen Sofortbau.

## 24.09.2026: ausgewählten Startmarker in den Folgezustand übernehmen

Der aktuelle Native-Build `FBCB9319…` bestimmt in `0x53D00` nach dem
Import der ausgewählten AIV den ersten rotierten Mapper `0x3D`. Dieser
Marker verschiebt den Startanker für `0x6D580`; er darf deshalb nicht
durch den kanonischen Marker `(56,43)` ersetzt werden. Der gemeinsame
Offline-Kern übergibt jetzt die **tatsächlich ausgewählte Drehung und den
Marker** an spätere KI-Fits. Bei mehrdeutiger Autoauswahl geschieht das
nur, wenn alle möglichen Ergebnisse denselben Marker und dieselbe
Drehung haben. Beide Werte gehören auch zum Cache-Schlüssel. Die
Unsicherheitszone umfasst alten und verschobenen Startanker sowie die
betroffenen Quell- und Zielzellen; nicht belegte Konstruktorzweige bleiben
`NotEvaluable`.

Fünf Korpora mit zusammen 218 Native-Fitversuchen ergeben damit 93
exakte Vergleiche und 125 `NotEvaluable`, ohne Abweichung oder
Verarbeitungsfehler. Gegenüber dem vorherigen Stand werden 17 weitere
Fälle exakt bewertet: neue Crater/Craggy-Serien 21 statt 19, ältere
Archive 61 statt 47 und die Laufzeitregression 11 statt 10. Die
Marker-Berichte liegen jeweils als `marker-report.json` in den
Corpus-Ordnern unter `../Findings/AIVPlacement/AivSeries-20260924/SixMatchResults`
bzw. `Pivot13RuntimeRegression/Observed`. Diese Korpora belegen die
beobachteten Marker und erfolgreichen Startkonstruktionen, nicht alle
Abbruchpfade oder den sequenziellen Sofortbau. Letzterer bleibt für
spätere KIs grau. Vertrauensgrad: hoch für die verglichenen Scores und
Marker-Transformationen; begrenzt für unbekannte Konstruktorfolgen.

## 24.09.2026: mehrdeutige Autoauswahl und drei weitere Crater-Lake-Starts

Die Serie `CL-B-off` und `CL-Reverse-off` wurde bestätigt; eine dritte
Aufnahme wiederholte `CL-Reverse-off`. Der installierte Native-Hash war
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`,
der Crater-Lake-Hash
`C5D9906AA37ED96EC1CF9B3EB0C7F6FB5B3E1D8063167FE22337E69C153BB887`.
Alle 24 Vollkarten-Starttraces sind vollständig, ohne gesetztes
Fehlerflag und mit jeweils 117 neu belegten Gebäudezellen. Der größte
beobachtete Chebyshev-Abstand einer geänderten Zelle zum Keep war 20;
das ist keine allgemeine Schreibgrenze. Von 31 Native-Fitversuchen
waren alle 31 mit dem Offline-Kern in Score, Prozentwert und blockierten
Zellen exakt. 21 Versuche betreffen die zwei verschiedenen Aufstellungen,
zehn sind die Wiederholung. Archiv, Rohdaten und SHA-256-Manifest:
`../Findings/AIVPlacement/AivSeries-20260924/MarkerRuntimeValidation/Observed`.

## Acht-Match-Serie mit mehreren Default-AIVs, 24.09.2026

Die acht bestätigten Sieben-KI-Starts sind unter
`../Findings/AIVPlacement/AivSeries-20260924/MultiAivEightMatch/Observed` mit dem
BepInEx-Abschnitt, 80 vollständigen Starttraces, 42 vollständigen
Sofortbau-Traces, 310 Zelltraces, den verwendeten Map- und AIV-Dateien
sowie SHA-256-Manifest gesichert. Davon gehören 64 Start- und 28
Sofortbau-Traces zur geplanten Serie; zwei spätere Craggy-Wiederholungen
liefern 16 beziehungsweise 14 weitere. Der installierte Native-Hash war
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die 114 geplanten Native-Fits ergeben 52 exakte Offline-Vergleiche und
62 bewusst graue Fälle; kein freigegebener Vergleich weicht in Score,
Prozent oder blockierten Zellen ab. Die beiden Wiederholungen ergeben
weitere zwei exakte und 40 graue Fälle. Der vollständige Bericht mit
Karten- und AIV-Hashes und Fallzahlen je Start liegt in
`../Findings/AIVPlacement/AivSeries-20260924/MultiAivEightMatch/RESULTS.md`.

Die Startkonstruktoren meldeten keinen Fehlerflag. Ihre beobachteten
117 neu belegten Zellen sind keine allgemeine Obergrenze: Der native
Kollisionspfad kann verbundene bestehende Gebäuderecords außerhalb der
Startfläche löschen, und `0x77E60` hat noch unbelegte Abbruchzweige.
Ebenso ist der spätere sequenzielle Sofortbau nicht vollständig
rekonstruiert. Daher bleiben die betroffenen späteren KI-Fits grau;
eine einzelne beobachtete Zufallsauswahl darf mögliche andere
Startmarker, Drehungen oder Bauwirkungen nicht ersetzen. Vertrauen hoch
für Aufnahme und exakte Oracle-Fälle, begrenzt für eine allgemeine
Freigabe weiterer Zustandsmengen.

Der erneut ausführbare Thasos-Altbestand deckte außerdem einen
unabhängigen Parser-Rückschritt auf: `MAPPER_DOG_CAGE` blieb für den
CastlePlanner-Spawnfilter korrekt als Trap klassifiziert, erhielt dadurch
aber fälschlich nur eine 1x1-Fitfläche. Native Importfunktion `0x55320`
verwendet die Mapper-Skala aus `0x6A190`; Script Extender 2.9.0 liefert
für den Hundekäfig Skala drei. Der frühere exakte Oracle-Fall verlor
damit acht geprüfte Zellen pro Drehung. Der gemeinsame Parser erhält
deshalb die Trap-Kategorie und die native 3x3-Fläche getrennt.
Der Thasos-Trace stammt ausdrücklich aus der historischen Native-DLL
`17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.
Die aktuelle DLL `FBCB9319...` und der installierte Extender belegen
den 3x3-Importpfad unabhängig davon; der historische Trace ist ein
zusätzlicher Regressionstest und keine neue aktuelle Laufzeitaufnahme.
Nach der Korrektur wurden 582 Einträge aus allen wiederherstellbaren
Alt- und Neu-Korpora geprüft: 234 exakt, 348 bewusst nicht auswertbar,
keine Abweichung und kein Verarbeitungsfehler. Mehrere Archive enthalten
Wiederholungen; dies sind Einträge, keine unabhängigen Spielzustände.

Bei `CL-B-off` konnte Wolf aus seinen möglichen Kandidaten die
Startdrehungen 0 und 90 Grad erreichen. Die zufällig beobachtete
Auswahl beweist keine eindeutige Lobby-Prognose. Spätere KIs bleiben
deshalb grau. Bei `CL-Reverse-off` waren die Startzustände eindeutig;
alle sieben KI-Spieler erhielten Auswertungen.

Der erneute Native-Audit verfolgt `0x94350 -> 0x53D00 -> 0x6D580 ->
0x77E60 -> 0x74DA0` samt `0x5D3A0` sowie optional `0x55F50 ->
0x51790 -> 0x5CD90`. Die Start-Räumung kann ganze kollidierende
Gebäuderecords und ihre verbundenen Zellen bearbeiten. Auch der
Sofortbau kann über einen getroffenen Record weitere Zellen räumen.
Die 24 lokalen Erfolge und der beobachtete Abstand 20 begrenzen diese
Seiteneffekte für beliebige Karten nicht. Die vollständigen
Abbruchzweige von `0x77E60` sind ebenfalls nicht belegt. Verschiedene
mögliche frühere Startzustände werden daher noch nicht vereinigt und
freigegeben; spätere Sofortbau-Zustände bleiben `NotEvaluable`.
Vertrauen: hoch für die aufgezeichneten Fits und erfolgreichen Starts,
begrenzt für allgemeine Schreibgrenzen und Startabbrüche.

Die nächste editierbare Serie `AivLobbyMultiAivSeries.json` prüft
acht Kartenstarts mit zwei geordneten Default-AIVs bei einem frühen
beziehungsweise mittleren KI-Spieler auf beiden Karten, je ohne und mit
Sofortbau. Der Detector bleibt unabhängig von festen Spieler-IDs.

## 2026-09-24: mögliche Startzustände und erneute Archivprüfung

Der installierte Native-Hash ist weiterhin
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Nach `0x94350 -> 0x54F60/0x53D00` wird der gewählte Start über
`0x6D580 -> 0x77E60` geprüft. `0x77E60` setzt bei Ablehnung das
Fehlerflag `+0x204E6FC`; `0x6D608` überspringt dann den Keep-Konstruktor.
Ein bloßer Fehlergrund ohne Flag zählt weiterhin nicht als Abbruch.
Die Bedingung hängt auch von Live-Einheiten, Nähe anderer Besitzer,
Pfaden und Tile-Regeln ab. Für eine unbekannte Auswahl wird deshalb
neben jeder möglichen gebauten Startburg auch „kein Start gebaut“
berücksichtigt. Vertrauen: hoch für die Flag-/Sprungkette, begrenzt
für die vollständigen Voraussetzungen eines Abbruchs.

Der gemeinsame AIVPlacement-Kern hält die möglichen Startzustände
einschließlich Marker, Drehung und ausgefallener Slots auseinander.
CastlePlanner prüft jede spätere AIV unter diesen Zuständen. Nur wenn
alle vier Drehungen dieselben Scores, Prozentwerte, blockierten Zellen
und Bauhinweise ergeben, wird ihre Einzelbewertung veröffentlicht.
Ein nicht auswertbarer Zustand oder eine Überschreitung der begrenzten
Arbeit führt zu `NotEvaluable`. Der Cache unterscheidet die
Zustandsidentitäten. „Completed Castles“ bleibt für spätere KIs grau,
weil `0x55F50 -> 0x51790` mit Mapper-spezifischen Konstruktoren und
verbundener Record-Räumung noch nicht exakt nachgebildet werden kann.

Der erfolgreiche Keep-Bau kann über `0x5D3A0 -> 0xC4290 -> 0xB8310`
ganze kollidierende Gebäuderecords löschen. Die Offline-Prüfung
verweigert eine Freigabe, sobald ein **fremder Record** innerhalb der
statisch weiter gefassten Startfläche liegt. Eigene Map-Startgebäude
werden über Record-ID und Starttransformation erkannt: Ihr Tile-Owner
kann im archivierten Crater-Lake-Snapshot null sein. Die erste, nur
auf Tile-Owner gestützte Sperre war deshalb zu breit und wurde vor dem
Build korrigiert. Die 11 archivierten Korpora ergeben danach erneut
234 exakte und 348 graue Einträge bei null Abweichungen. Diese
Vergleiche prüfen den beobachteten nativen Ablauf; alternative
Zufallsentscheidungen bleiben Gegenstand eines kurzen Laufzeittests.

Der lokale Fixes-Mod kann das Goods-Yard-Ende des Startkonstruktors
pro Spieler auslassen. Die Fit-Aussage behauptet weiterhin keinen
vollständig gebauten Start oder Sofortbau; die unsicheren Zellen und
Kollisionen bleiben abgesperrt. Für die neue Zustandsmengen-Auswertung
ist eine gezielte Laufzeitprüfung vorbereitet:
`aiv-possible-starts-regression-20260924` enthält sechs Starts
(`CL-Early-off`, `CL-Early-on`, `CL-Middle-off`, `CC-Early-off`,
`CC-Middle-off`, `CC-Middle-on`). Sie vergleicht frühe und mittlere
Mehrfach-AIV-Auswahlen auf beiden Karten; je ein Paar prüft zusätzlich
den Sofortspawn-Schalter. Die Aufstellungen stammen unverändert aus der
zuvor validierten Acht-Match-Serie. Bis zum Spielstart bleibt offen, ob
die alternativen Startzustände in der echten Lobby dieselbe Bewertung
liefern oder mit dem konkreten Grund grau bleiben. Es genügt pro Lauf
der sichtbare Spielbeginn; späterer regulärer KI-Bau ist nicht nötig.

## 2026-09-24: Sechs-Match-Serie ausgewertet

Der installierte Native-Hash ist unverändert
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Alle sechs vorbereiteten Starts wurden bestätigt; zwei zusätzliche
Craggy-Cliffs-Starts bleiben im Bericht separat. 129 Native-Fits wurden
mit hashgeprüften Karten und AIVs erneut verglichen: 52 exakt, 77
bewusst grau, null Abweichungen. Die 64 Keep-Start- und 28
Sofortbau-Traces sind vollständig. Der Befund und die Rohdaten liegen
unter `../Findings/AIVPlacement/AivSeries-20260924/PossibleStartsRuntimeResults/`.

Bei Crater Lake sind die 17 gemeinsamen Native-Fits des Paares mit und
ohne Sofortbau identisch. Bei Craggy Cliffs unterscheiden sich sechs
von elf gemeinsamen Fits. Ein Emir-Kandidat wechselt von null auf 252
blockierte Zellen; alle 252 geänderten Ergebniszellen liegen in den
fitrelevanten Schichten, die vorherige Sofortbau-Frames tatsächlich
geschrieben haben. Spätere KIs mit „Completed Castles“ bleiben daher
grau, solange alle möglichen Bauabläufe nicht exakt rekonstruiert sind.

Die 32 Warnungen der Zell-Diagnose waren eine falsche Zählannahme:
`0x57080` verwirft ungültige Kartenkoordinaten vor `0x7B060`. Für alle
129 Traces gilt `nativeBlocked = validatorBlocked + evaluatedCells -
validatorCalls`. Der Detector prüft diese vollständige Beziehung.
Auf Craggy Cliffs liest ein beobachteter Emir-Fit zwar keine der 552
von drei vorherigen Keep-Starts tatsächlich geänderten Zellen. Das
beweist nichts über alternative zufällige Starts oder verbundene
Record-Räumung. Eine Freigabe auf Basis dieses einzelnen Ablaufs wäre
unsicher. Die Lobby unterscheidet künftig ausdrücklich zwischen
unbewiesenem Startzustand und tatsächlich verschiedenen Fit-Ergebnissen.

## 2026-09-24: gezielte Record-Räumungsmessung vorbereitet

Die neue zweistufige Serie `CC-A-on`, `CC-B-on` auf Craggy Cliffs nutzt
unveränderte, bereits hashgeprüfte Aufstellungen. Bei beiden Aufstellungen
zeigten frühere Traces gelöschte Gebäudezellen. Der Detector vergleicht
jetzt zusätzlich vor und nach jedem KI-Keep-Start die ausgewählten Felder
aller nativen Gebäuderecords. Zusammen mit den vollständigen Tile-Diffs,
Global-IDs und Start-Statusflags soll dies die Wirkung des nativen Pfads
`0x74DA0 -> 0x5D3A0 -> 0xC4290 -> 0xB8310` eingrenzen. Native-Hash:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Das ist eine vorbereitete Messung, noch kein Beleg für eine allgemeine
Schreibgrenze oder einen Abbruchvertrag. Der genaue Ablauf steht in
`../Findings/AIVPlacement/AivSeries-20260924/ConnectedRecordProbeSetup/TEST_PLAN.md`.

## 2026-09-24: Ergebnis der Record-Räumungsmessung

`CC-A-on` und `CC-B-on` wurden vollständig aufgenommen; ein dritter
Kartenstart wiederholte `CC-B-on`. Die 24 Start- und 20 Sofortbau-Traces
sind vollständig und ohne Snapshot-, Pointer- oder Start-Fehlerflag.
Von 50 Native-Fit-Versuchen stimmen drei exakt mit dem Offline-Kern
überein, 47 bleiben bewusst grau; es gibt keine Abweichung. Rohdaten,
Datei-Hashes und Zell-/Record-Belege stehen unter
`../Findings/AIVPlacement/AivSeries-20260924/ConnectedRecordProbeResults/RESULTS.md`.

Der entscheidende Native-Befund am Hash
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`:
Die Kollisionsräumung `0x5D3A0` verwendet bei Typ 41 feste Offsets für
Lager und Yard, ohne die gewählte Keep-Drehung zu erhalten. Der spätere
Bau `0x74DA0` verwendet hingegen gedrehte Offsets. Bei Keep `(506,357)`
und Drehwert 2 traf die feste 7x7-Lagerfläche ab `(506,365)` zwei Zellen
eines bereits vorhandenen 4x4-Gebäudes. Daraufhin entfernte Vanilla alle
16 Zellen des Gebäudes, auch die außerhalb des neuen gedrehten
Startkomplexes. Die Wiederholung zeigte dieselbe Wirkung. Vertrauensgrad:
hoch für diesen Native-Zweig und beide beobachteten Abläufe.

Die verbundene Record-Räumung gruppiert über
`GameBuilding.r_UsedInSiegeAttemptId` bei Struct-Offset `0x2A8`
der **installierten** Script-Extender-DLL 2.9.0, **nicht** über
`r_GlobalId` bei `0xD8`. Der lokale C#-Interop-Quellcode bei Commit
`70a4483` stimmt damit überein; nur der ältere Reverse-Engineering-Header
`GameBuildingManager.h` bezeichnet `0x2A8` noch als `N0000178A`.
Die aktuelle Diagnose speicherte nur
`r_GlobalId` und kann somit keine beliebigen verbundenen Gruppen
abgrenzen. Der konservative `StartOverlapUnproven`-Schutz umfasst den
beobachteten Auslöser und bleibt bestehen. Es trat kein Startabbruch auf;
dessen Zweige sowie die Reichweite nicht beobachteter Record-Gruppen
sind weiterhin offen. Eine zusätzliche farbige Freigabe wäre anhand
dieser Messung nicht exakt belegt.

## 2026-09-24: Cleanup-Link-Folgeprobe vorbereitet

Der installierte Detector erfasst jetzt zusätzlich das native
Record-Feld bei `GameBuilding`-Offset `0x2A8` als
`nativeCleanupLinkId`. Der Member heißt in der installierten
Script-Extender-DLL 2.9.0 `r_UsedInSiegeAttemptId`; Typ `UInt32`,
Structgröße 812 Byte. Der alte Trace bleibt unverändert archiviert.
Eine vierteilige Craggy-Cliffs-Serie (`CC-A-off/on`, `CC-B-off/on`)
ist mit Fortschritt 0 vorbereitet; Testanleitung unter
`../Findings/AIVPlacement/AivSeries-20260924/CleanupLinkProbeSetup/TEST_PLAN.md`.
Sie soll die bisher nicht aufgezeichnete Gruppen-ID gegen konkrete
Start-Räumungen und den Sofortbau vergleichen. Bis zu dieser Auswertung
bleibt der breite Produktionsschutz unverändert.

## 2026-09-24: Ergebnis der Cleanup-Link-Folgeprobe

Die vier geplanten Craggy-Cliffs-Läufe wurden bestätigt. Vier weitere
Starts nach Serienende wiederholten `CC-B-on` ohne Preset-Übernahme und
werden getrennt gezählt. Insgesamt sind 64 Keep-Starttraces und 41
Sofortbau-Traces vollständig. Von 125 Native-Fits sind 21 exakt und 104
bewusst `NotEvaluable`; es gibt keine Abweichung oder Vergleichsfehler.
Alle Zellen-, Record- und Eingabedateien samt Hashes liegen unter
`../Findings/AIVPlacement/AivSeries-20260924/CleanupLinkProbeResults/RESULTS.md`.
Native-DLL-Hash weiterhin
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Ohne Sofortspawn überschrieb keiner der geplanten Keep-Starts einen
zuvor lebenden Gebäuderecord. Mit Sofortspawn räumte `CC-A-on` bei
Spieler 5 drei alte Record-Slots und 60 Gebäudezellen. Nur der alte
Typ-46-Record hatte einen nichtnullen Cleanup-Linkwert; ein durch
denselben Wert zusätzlich gelöschter Begleitrecord wurde nicht
beobachtet. `CC-B-on` räumte bei Spieler 7 einen alten Record mit
Linkwert null und dessen 16 Zellen; vier Wiederholungen bestätigten
dies. Der erfolgreiche Keep-Bau nach einer nicht akzeptierten
AIV-Auswahl zeigt erneut: Auswahlfehler und Konstruktorabbruch sind
getrennte Zustände. Sämtliche tatsächlichen Konstruktor-Fehlerflags
blieben null.

Die Karte enthält zwar serialisierte Gebäuderecords, aus denen der
Linkwert `0x2A8` offline lesbar wäre. Das genügt noch nicht, um
zufallsabhängige frühere Startkonstruktionen und dynamische
Sofortbau-Records für **alle** möglichen AIVs nachzubilden. Der
breite `StartOverlapUnproven`-Schutz bleibt daher bestehen; es wird
kein zusätzliches späteres Fit-Ergebnis freigegeben. Vertrauensgrad:
hoch für diese ausgeführten Fälle, offen für verbundene Mehrfachlöschung
und Startabbruch. Als Nächstes sind `0xB8310`/`0x61FC0` und die
serialisierten Linkgruppen statisch vollständig auf eine mögliche
Schreibgrenze zu prüfen. Erst ein daraus abgeleiteter unbelegter
Zweig rechtfertigt neue Spieltests.

Die serialisierte Gebäudesektion 4013 von Craggy Cliffs wurde zusätzlich
offline dekodiert: 72 lebende Startrecords, neun je Spieler. Die fünf
Keep-/Lager-/verknüpften Records teilen pro Besitzer einen nichtnullen
Wert an Offset `0x2A8`, die vier Yard-Records einen zweiten. Damit ist
die **anfängliche** Gruppenmitgliedschaft aus der `.map` lesbar. Die
später durch eine AIV-Auswahl und „Completed Castles“ erzeugten
Records sind darin nicht enthalten. Auch diese Karte allein liefert
also noch keine allgemeine sequenzielle Bauzustandsrekonstruktion.

Der anschließende statische Abgleich mit derselben installierten DLL
bestätigt: `0xC43A0` merkt sich den Linkwert bei nativem Record-Offset
`+0x304`, entfernt den Ausgangsrecord mit `0xB8310` und durchsucht
danach alle lebenden Records nach demselben Wert. Deren Löschung führt
jeweils über `0x61FC0` in typabhängige Kachel-, Pfad- und
Darstellungsänderungen. Sonderzweige betreffen unter anderem die Typen
10, 30–33, 49, 69 und 80–84. Die initialen Linkgruppen aus der `.map`
sind damit belegbar, aber die fitrelevante Schreibgrenze aller
Typzweige und dynamisch gebauter Records noch nicht. Vertrauensgrad:
hoch für direkten Kontrollfluss und Feldoffsets, unvollständig für die
gesamte räumliche Änderungsgrenze. Deshalb keine zusätzliche farbige
Freigabe aus dieser Messreihe.

Da die normalen Craggy-Cliffs-Starts auch mit dem neuen Linkfeld keine
Mehrfachlöschung auslösten, ist eine vierteilige, gezielte Folgeserie
auf der bereits archivierten Karte `test AI overbuild eachother.map`
vorbereitet. Zwei KI-Reihenfolgen werden jeweils mit Sofortspawn aus
und an geprüft. Karte, fünf Keep-Slots, eingebaute Default-AIVs und
Fortschritt sind vorab validiert; die installierte Testserie hat
`nextIndex: 0`. Details:
`../Findings/AIVPlacement/AivSeries-20260924/DenseStartLinkSetup/TEST_PLAN.md`.
Diese Serie prüft gezielt verbundene Startgruppen und einen möglichen
Konstruktorabbruch; die bisherige konservative Produktionsgrenze bleibt
bis zur zellweisen Auswertung erhalten.

## 2026-09-24: Dichte Startplätze ausgewertet

Alle vier vorbereiteten Läufe auf `test AI overbuild eachother.map`
wurden bestätigt; zwei spätere Kartenstarts wiederholten ohne
Preset-Übernahme den vierten Lauf. Der Native-Hash blieb
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die 30 Keep-Starttraces und acht Sofortbau-Traces sind vollständig.
Von 74 nativen Fits stimmen sechs exakt mit dem Offline-Kern überein;
68 bleiben bewusst `NotEvaluable`, null weichen ab. Details und
Prüfsummen:
`../Findings/AIVPlacement/AivSeries-20260924/DenseStartLinkResults/RESULTS.md`.

Die `.map` enthält 45 zunächst lebende, nichtnull verknüpfte
Startrecords. Beim ersten KI-Fit enthält das vollständige Live-Raster
jedoch nur die neun Records des menschlichen Startkomplexes: Die
serialisierten KI-Gruppen sind davor bereits entfernt worden. Von
279 während der beobachteten Keep-Aufrufe geänderten Record-Slots
waren 65 zuvor lebendig; keiner davon hatte einen nichtnullen
Linkwert. Neun Typ-67-Records wurden bei einem Vorwärtslauf mit
Sofortspawn und dessen zwei Wiederholungen vollständig gelöscht.
Alle neun hatten Linkwert null. Kein Startabbruch trat auf.

Damit belegen die Läufe tatsächliche Räumung bei nahen KI-Starts,
aber weder die zusätzliche Mehrfachlöschung über `0xC43A0` noch
die räumliche Wirkung der früheren Kartenstart-Normalisierung.
Die 373 installierten Karten wurden auf wählbare Keep-Abstände
untersucht; diese Spezialkarte hat mit 24,76 Kacheln bereits das
engste Paar. Identische Wiederholungen liefern für die offene
Gruppenregel keinen weiteren Beleg. Vertrauensgrad: hoch für
beobachtete Traces, offen für den nicht ausgelösten nativen Zweig.
Die produktive `StartOverlapUnproven`-Grenze bleibt deshalb erhalten.

Der erneute Native-Abgleich zeigt zudem bei `0x94350` zwei getrennte
Schleifen: Zuerst werden die serialisierten Startrecords der
konfigurierten Spieler über `0xC43A0` abgetragen, danach folgen
KI-Auswahl und Startbau. Elf graue Fälle der dichten Karte nennen
noch eine `source tile` aus einer solchen bereits entfernten
KI-Gruppe als möglichen Kollisionsauslöser. Building-ID 10 ist zum
Beispiel in der Karte ein Typ-41-Record von Spieler 2 mit Linkwert
11, fehlt aber beim ersten KI-Fit im vollständigen Live-Raster.
Neun weitere graue Fälle betreffen dagegen echte rekonstruierte
frühere Starts; 48 sind wegen vorausgegangenem Sofortbau gesperrt.
Die Korrektur nimmt ausschließlich solche bereits normalisierten
Source-Records aus der Kollisionsauslöserprüfung, die zum eindeutig
gefundenen Start-Keep oder dessen nichtnuller nativer Linkgruppe
gehören. Die bisherige Ausnahme für die eigenen transformierten
Startzellen bleibt erhalten. Der
kandidatenbezogene Guard und alle möglichen vorherigen
Startzustände bleiben erhalten. Bei elf dichten Oracle-Fällen
änderte sich damit nur der genaue graue Grund; keine Prognose
wurde unbelegt freigegeben. Vertrauensgrad: hoch für die
Native-Reihenfolge und den ersten Live-Zustand, offen für mögliche
vorherige Startausgänge.

### Offline-Folgeprüfung mit Script Extender 2.10.1

Die installierte Spiel-DLL hat weiterhin SHA-256 `FBCB9319…`; der
installierte Script Extender 2.10.1 SHA-256
`85591256082C6F2329EDC0BFB0C1C163D9CF1BF2991DC8B11F6790907B60F2F2`.
Die erneute Prüfung von 960 archivierten Vergleichsfällen ergab
316 exakte und 644 bewusst graue Ergebnisse, keine Abweichung;
die Korpora enthalten teilweise dieselben Spielsituationen.
Einzelheiten und Berichte stehen unter
`../Findings/AIVPlacement/AivSeries-20260924/GuardRefinement-20260924/`.

Die erneute direkte Prüfung von `0xC43A0 -> 0xB8310` zeigt eine
weitere Grenze: `0xB8310` ruft vor `0x61FC0` zusätzlich
`0xB8460`, `0x1977A0` und `0xB5C40` auf, danach `0xCFE90`.
Eine allgemeine Schreibgrenze darf sich daher nicht allein auf
den Typzweig in `0x61FC0` stützen. Von 185 installierten Karten
mit mindestens drei auswertbaren Startplätzen hatten nur die
dichte Spezialkarte und Caesarea Swampland anfänglich verknüpfte
Record-Ursprünge nahe einem fremden Keep; alle 42 Treffer
gehörten zu serialisierten Startgruppen, die Vanilla bereits
vor der KI-Auswahl entfernt. Der Scan erfasst keine zur Laufzeit
entstandenen Gruppen oder ganzen Gebäude-Footprints. Vertrauensgrad:
hoch für den direkten nativen Aufrufpfad und die gelesenen
Kartenrecords, offen für deren transitive Seiteneffekte.

## 2026-09-24: dynamische Linkgruppe erstmals vollständig gelöscht

Die vier Starts `DL-forward-off/on` und `DL-reverse-off/on` wurden
bestätigt; ein fünfter Start nach Serienende wiederholte den letzten
Aufbau. Beim unveränderten Native-Hash `FBCB9319…` erfasste der Detector
57 Fits, 25 vollständige Keep-Starts und fünf vollständige
Sofortbau-Traces. Mit Script Extender 2.10.1 sind fünf Fits bis auf jede
der zusammen 10.660 ausgewerteten Zellen exakt; 52 bleiben bewusst
grau, null weichen ab. Bei den 36 grauen Fits nach Sofortbau las der
Native-Validator tatsächlich zuvor geänderte Tile-IDs. Die übrigen
16 sind durch mögliche Start-Räumung gesperrt. Befund, Rohdaten,
Prüfsummen und reproduzierbares Analyse-Script stehen unter
`../Findings/AIVPlacement/AivSeries-20260924/DynamicLinkProbeResults/`.

Bei `DL-reverse-on` erzeugte Spieler 3 in Bau-Frame 53 mit Mapper 87
vier 5×5-Records mit gemeinsamem Cleanup-Linkwert `903`: IDs
62/63/65/340 an `(265,420)`, `(270,420)`, `(265,425)` und
`(270,425)`. Der spätere Start von Spieler 5 bei `(258,417)` löschte
alle 100 alten Gebäudezellen, davon wurden 12 sofort mit neuen
Startgebäuden überbaut und 88 frei. Die zusätzliche Wiederholung
bestätigte denselben Zellbefund. Das ist der bislang fehlende
Laufzeitbeleg einer **dynamisch gebauten** Linkgruppe; die frühere
Aussage „kein verbundener Begleitrecord beobachtet“ gilt nur für die
damals ausgewerteten Aufnahmen. Die Starttraces zeigten für die alten
Record-Slots wegen ID-Wiederverwendung `alive` vor und nach dem
Aufruf. Erst die synchronen Building-ID-Zellen belegen ihre
vollständige Räumung. Alle Start-Fehlerflags blieben null.

Der Native-Pfad ist `0x5D3A0 -> 0xC4290 -> 0xB8310`: `0xC4290`
markiert den getroffenen Record und alle lebenden Records mit
demselben nichtnullen Link bei Manager-Offset `+0x304` bzw.
`GameBuilding+0x2A8`; `0x5D3A0` entfernt anschließend sämtliche
markierten Records. Diese dynamische Räumung ist vom anfänglichen
`0xC43A0`-Durchlauf über die serialisierten Startgruppen zu
unterscheiden. Vertrauensgrad: hoch für diesen Ablauf und seine
100 Zellen, weiterhin offen für andere Mapper, Gegenvarianten,
`0x77E60`-Abbrüche und die vollständige Schreibgrenze aller
`0x61FC0`-Typzweige. Die sichere Produktgrenze bleibt deshalb
`NotEvaluable`, sobald ein möglicher früherer Zustand nicht exakt
rekonstruiert ist.

## 2026-09-24: natürlicher Keep-Verlust im ersten Crossing-Test

Auf `CrusadesCrossing.map` (SHA-256
`B5AB8BCC5C4C2783697EEF4BBE692AE829B2C7D59C4AFF540F79E140C49417FE`)
baute Spieler 2 bei ausgeschaltetem Sofortspawn zuerst einen vollständigen
Typ-41-Startkomplex. Sein 7×7-Keep lag nach der Vanilla-AIV-Drehung bei
`(510,502)`; der auswählbare Map-Keep-Slot war `(510,495)`. Spieler 3
startete danach tatsächlich bei `(523,489)`, obwohl sein Map-Slot bei
`(548,483)` liegt. Das erklärt, weshalb auf einer großen Karte die
tatsächlichen Startgebäude viel näher beieinander liegen als die
eingezeichneten Map-Slots vermuten lassen.

Die synchronen Starttraces der installierten Native-DLL
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
zeigen im Spieler-3-Aufruf 101 geleerte Zellen von Spieler 2:
49 Keep-Zellen (Record 10), 49 Camp-Zellen (Record 14) und drei
Verbindungszellen (Records 11–13). Alle fünf Records hatten denselben
Cleanup-Linkwert `465609`; ihre IDs wurden beim Start für Spieler 3
wiederverwendet. Die vier Lagerplatz-Records von Spieler 2 blieben im
nächsten Live-Raster vorhanden. Die beobachtete Szene mit Startflagge
und Lagerplatz, aber ohne Keep, ist damit durch Tile- und Record-Daten
erklärt. Beide Konstruktoren meldeten kein Fehlerflag.

Dies ist ein beobachteter Ablauf der modded Runtime innerhalb des
nativen Startaufrufs, kein Beleg für eine allgemeine Offline-Regel aller
möglichen AIV-Auswahlen. Der Fixes-Quellcode patcht dort optional den
nachgelagerten Goods-Yard-Tailcall, nicht den dokumentierten
Gruppenräumungspfad. Ein Einzel-Kontrolllauf mit möglichst wenigen
erforderlichen Mods ist vorbereitet. Rohdaten und Prüfsummen liegen
unter `../Findings/AIVPlacement/AivSeries-20260924/NaturalLinkEightResults/`.

## 2026-09-24: Abschluss der acht Natural-Link-Läufe

Die acht vorgesehenen Läufe sind vollständig. Danach gab es einen
zusätzlichen Reed-Sea-Start mit der letzten Aufstellung; er zählt als
Wiederholung und nicht als neunter Serientest. Die Rohdaten sind samt
DLL-, Map- und AIV-Hashes unter
`../Findings/AIVPlacement/AivSeries-20260924/NaturalLinkEightResults/` gesichert.
Der installierte Native-Hash blieb
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Für Crusades Crossing ergaben sich 16 exakte und 48 bewusst graue Fits,
ohne Abweichung. Reed Sea lieferte in den vier geplanten Starts neun exakte,
49 graue und acht Abweichungen im Offline-Oracle-Vergleich. Letztere
betreffen ausschließlich Sentinel Default 2 als dritte KI in den beiden
Vorwärtsläufen, jeweils alle vier Drehungen. Der Comparator verwendet
die **beobachtete** frühere Auswahl und rekonstruiert deren sequenziellen
Startzustand hier nicht exakt. CastlePlanner ließ diese Fälle in der
Lobby bereits grau (`StartOverlapUnproven` beziehungsweise
`PreBuildSequenceUnsupported`); eine falsch gefärbte Nutzeranzeige ist
aus diesen Daten nicht belegt. Die zusätzliche Wiederholung hatte vier
exakte und 13 graue Fälle ohne Abweichung.

Der beobachtete Verlust des ersten Crossing-Keeps bleibt ein einzelner
modded Laufzeitbefund. Ein separater Kontrolllauf mit nur den fünf
Diagnose-Abhängigkeiten ist vorbereitet, um Mod-Einfluss einzugrenzen.
Bis dahin keine neue allgemeine Regel für verbundene Records oder
spätere Sofortspawn-Fits freigeben. Vertrauensgrad: hoch für Live-Zellen,
Native-Scores und graue Produktanzeige; mittel für die Vanilla-Zuordnung
des Keep-Verlusts ohne diesen Kontrolllauf.

## 2026-09-25: Startvorbereitung und unabhängiger Vanilla-Versuch

Der Nutzer hat den fehlenden Wolf-Keep auf Crusades Crossing mit derselben
Aufstellung auch ohne Mods reproduziert. Der installierte Native-Hash ist
weiterhin `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Damit ist ein Eingriff unserer Mods als notwendige Ursache für diesen
konkreten Keep-Verlust ausgeschlossen. Der Kontrollversuch hat keinen
Detector-Zelltrace; die genaue Zellfolge stammt aus dem archivierten
modded Lauf.

Vanilla führt zwei getrennte Vorbereitungen aus: `0x94350 -> 0xC43A0`
entfernt zunächst gespeicherte KI-Startgruppen. Vor dem neuen Start setzt
`0x94350` auch für menschliche Spieler das Flag bei `0x1860AD5AC`.
Nach erfolgreicher Prüfung `0x6D580 -> 0x77E60` ruft der Keep-Konstruktor
`0x74DA0` dadurch `0x5D3A0` auf. Diese zweite Vorbereitung prüft feste,
**nicht mitrotierende** Zellen von Keep, drei Verbindungen, Lager und
Goods Yard. Bei Kontakt markiert `0xC4290` die gesamte nichtnullig
verknüpfte Record-Gruppe, die `0xB8310` anschließend löscht. Der
Sentinel-Start bei `(523,489)` prüft unter anderem `(523,502)` im
Lagerbereich. Dort lag Wolfs vorheriges Lager; dessen Linkgruppe umfasste
auch seinen Keep bei `(510,502)`, obwohl dessen eigene Zellen nicht vom
Sentinel-Start direkt geprüft wurden. Vertrauen: hoch für Native-Pfad,
Offsets und aufgezeichnete modded Löschung; unabhängig berichtete
Vanilla-Reproduktion bestätigt das sichtbare Ergebnis.

Die Lobby kann den **Kontakt** und einen möglichen Keep-Verlust als
separaten Bauhinweis prüfen. Die vollständigen Tile-Nebenwirkungen der
Löschung und sämtliche Konstruktorabbrüche sind damit noch nicht bewiesen.
Solche späteren Fits bleiben deshalb `NotEvaluable`, wenn sie diese
unbekannten Eingabezellen lesen. Ein weiteres allgemeines Nachspielen
dieses Keep-Verlusts ist nicht nötig.
