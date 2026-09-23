# AIVPlacement: Sofortspawn und Lobby-Prognose – aktueller Forschungsstand

Stand: 2026-09-23. Maßgebliche installierte `CrusaderDE.dll`:
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

Der erneute Audit am selben Native-Hash bestätigt für die Fit-Kette `0x57080` -> `0x7B060` den einzelnen geprüften Tile pro AIV-Zelle (Spieler-ID 0, Modus 0). Der Sofortspawn-Pfad `0x51790` -> `0x5CD90`/`0x6D580` kann jedoch bestehende Strukturen entfernen und verzweigt in typspezifische Konstruktoren. Deren gesamte Tile-Schreibreichweite und Rückwirkungen sind für alle Varianten noch nicht belegt; der lokale Fixes-Mod verlagert zusätzlich den OrderedMapTileIds-Puffer. Deshalb wird aus den 41 gleichen Ergebnissen keine allgemeine räumliche Freigabe abgeleitet. Weitere unveränderte Crater-Lake-Spielstarts sind für diese Frage derzeit nicht erforderlich; zuerst muss der statische Schreibpfad begrenzt oder eine gezielt fehlende Variante identifiziert werden. Der reproduzierbare Read/Write-Abgleich liegt unter [`Compare-PrebuildReadWrite.ps1`](Diagnostics/Compare-PrebuildReadWrite.ps1); er prüft zuerst die Datei-Provenienz und vollständige Frames und meldet Überschneidungen ausschließlich für den tatsächlich aufgenommenen Auswahlpfad.

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

Die Positionsgrafik [`CraterLake-Referenzsetup.png`](Diagnostics/CraterLake-Referenzsetup.png) zeigt die Radarplätze. Der Screenshot der neuen Lobby zeigt dieselben Kartenplätze, aber eine andere Spielerfolge: Mensch, Nox, Marschall, Jewel, Abt, Wolf, Nizar, Nomade. Nizar ist dort P7 und Wolf P6. Das ist als räumlich getrennter Kontrollfall geeignet, nicht als isolierter Nizar-zu-Wolf-Bauvergleich. Die native Spielerreihenfolge bleibt bei Sofortspawn relevant, selbst wenn die geplanten Burgflächen nicht überlappen. Für die acht Kartenplätze beträgt die kleinste Chebyshev-Distanz zweier nativer Keep-Anker 133 Tiles (Abt/Nizar). Die projizierten 100×100-AIV-Raster können sich an diesen Ankern daher nicht direkt überschneiden; native Konstruktor- und Bereinigungseffekte außerhalb des Rasters sind damit noch nicht begrenzt.

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
[`Oracle-Korpus`](Diagnostics/CraterLake-20260923-1758-Oracle/unknown.json)
enthält 89 Native-Versuche. Der unveränderte Offline-Vergleicher meldet
43 `ExactMatch` (alle 42 Versuche ohne Sofortspawn plus den ersten mit
Sofortspawn), 46 absichtliche `NotEvaluable` für spätere Spieler mit
Sofortspawn, null Abweichungen und null Auswertungsfehler. Der
[`Vergleichsbericht`](Diagnostics/CraterLake-20260923-1758-Oracle/report.json)
und der dazu isolierte [`Logabschnitt`](Diagnostics/CraterLake-20260923-1758.log)
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
