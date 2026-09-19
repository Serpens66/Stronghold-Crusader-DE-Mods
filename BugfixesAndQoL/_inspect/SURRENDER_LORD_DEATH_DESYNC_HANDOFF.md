# Surrender-/Lordtod-Desync: Untersuchungsstand und Übergabe

Stand: 19. September 2026

## Zweck und gesicherter Status

Dieses Dokument übergibt den vollständigen bisherigen Untersuchungsstand an einen neuen Chat. Der Multiplayer-Desync ist reproduzierbar und nach der Beobachtung des Testers unmittelbar an Surrender beziehungsweise den dadurch ausgelösten Lordtod gekoppelt. Die sichtbare Resync-Oberfläche erscheint später, weil Vanilla die bereits entstandene Zustandsabweichung offenbar erst bei einem periodischen Synchronisationsvergleich erkennt.

Die Ursache ist noch nicht auf einen konkreten Writer oder ein konkretes natives Feld eingegrenzt. Insbesondere darf aus dem zeitlich später gesendeten Opcode 54 nicht geschlossen werden, dass die Abweichung erst dann entsteht.

## Reproduktionsablauf

- Realer Multiplayer mit Host und einem menschlichen Client sowie KI-Spielern.
- Ein menschlicher Spieler bestätigt den von BugfixesAndQoL bereitgestellten Surrender.
- Der synchronisierte Surrender-Chore tötet dessen Lord.
- APIShared beobachtet den Lordtod; der Host verschickt danach den synchronisierten Spectator-Chore.
- Der ausgeschiedene lokale Spieler wird in Vanillas Spectator-Modus versetzt.
- Der Desync entsteht nach wiederholter Nutzerbeobachtung direkt bei diesem Surrender-/Lordtod-Ablauf. Die Resync-Erkennung und Opcode 54 folgen erst später.
- Frühere Abnahmefolge: zuerst Host-Surrender, später Client-Surrender. Der auffällige Resync trat nach dem Tod des Clients auf. Ein späterer Lauf reproduzierte den Resync auch nach Host-Surrender. Der Fehler ist daher nicht auf die Rolle des aufgebenden Peers begrenzt.

## Neuester bestätigter Lauf

Verwendete BugfixesAndQoL-DLL auf Workspace, Host und Client:

`D881A22F78EF6745896929B0C69E7E2BFDEFC2BB39F6FC144703DAE443B7305F`

Host und Client verwendeten damit nachweislich denselben Build.

### Gemeinsame Ereignisse

- Surrender von Spieler 1.
- Surrender-Chore auf Host und Client: Ausführung in Simulationstick 2148, Lord-Unit-ID 14, Global-ID 13702.
- Spectator-Chore auf Host und Client: Ausführung in Tick 2156.
- Die protokollierten, begrenzten Lord-/Spieler-Checkpoints besitzen auf beiden Peers bis Offset 96 beziehungsweise Tick 2244 denselben SHA-256 `E5B098D20CFF4B744D92446E528F603CF244D52327811AC8EE64400E4C9FFE6D`.
- Die lokalen UI-Zustände unterscheiden sich erwartungsgemäß: Nur der ausgeschiedene lokale Host aktiviert Spectator; auf dem Client bleibt der lokale Spieler 2 Nicht-Spectator. Lokale UI-Daten fließen nicht in den Peer-Hash ein.

### Resync-Erkennung

- Host: Opcode 54 und Wechsel `resyncing false -> true` in Tick 2406.
- Client: Wechsel `resyncing false -> true` in Tick 2407.
- Host und Client beenden den Resync in Tick 2412; der Host sendet Opcode 67.
- Zwischen Surrender-Chore und Opcode 54 liegen 258 Simulationsticks. Dieser Abstand beschreibt den Erkennungszeitpunkt, nicht den Entstehungszeitpunkt der Abweichung.

### Auffällige Tick-Domänen

Im neuesten Lauf meldet APIShared den Lordtod lokal mit unterschiedlichen Werten (`lordDeathTick=2175` auf dem Host und `2172` auf dem Client), obwohl der Spectator-Chore auf beiden Peers bei `executionTick=2156` läuft. Diese Werte stammen offenbar nicht zuverlässig aus derselben Tick-Domäne beziehungsweise demselben Beobachtungszeitpunkt. Sie sind ein Untersuchungsansatz, aber noch kein Desync-Beweis. Der Spectator-Payload enthält nur die Player-ID; diese lokalen Diagnoseticks werden nicht übertragen.

## Aktueller Surrender-/Spectator-Datenfluss

Maßgebliche Implementierung: `BugfixesAndQoL/src/SurrenderFeature.cs`.

1. Ein Client verschickt einen `SurrenderRequestPacket` an den Host. Ein aufgebender Host überspringt diesen Request-Schritt.
2. Der Host validiert den authentifizierten menschlichen Spieler und dessen aktuellen Lord.
3. Der Host reiht einen `SurrenderExecutionPacket` als Chore ein. Übertragen wird absichtlich nur die stabile Player-ID.
4. Jeder Peer löst in `OnExecutionReceived` den aktuellen Lord des Spielers erneut über dessen Global-ID auf, validiert ihn und ruft `GameUnitManagerAPI.Instance.KillUnit(resolvedUnitId)` auf.
5. APIShared beobachtet den Alive-/Defeat-Übergang vor dem nativen Simulationstick. Der Host reiht daraufhin einen `EliminatedPlayerSpectatorPacket` als Chore ein.
6. Der Spectator-Chore wird auf allen Peers verarbeitet. Nur wenn die enthaltene Player-ID dem lokalen Spieler entspricht, wird lokal `EngineInterface.GameAction(SpectatorMode, 0, 0)` aufgerufen.

Die Logs belegen für die untersuchten Läufe eine Ausführung beider Chores auf beiden Peers. Die frühere unsynchronisierte Spectator-Umschaltung ist nicht wieder aufgetreten. Trotzdem muss noch nativ geprüft werden, ob `KillUnit` in diesem Chore-Callback auf allen Peers garantiert an derselben Stelle des Simulationsablaufs ausgeführt wird und ob die lokale Spectator-GameAction wirklich keinerlei deterministischen Zustand berührt.

## Bereits abgeschlossene native Grundlage

Kanonische installierte `CrusaderDE.dll` und Baseline:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Vor jeder weiteren Verwendung von RVAs muss dieser Hash erneut mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` und der installierten DLL verglichen werden.

Gesicherte beziehungsweise strukturell bestätigte Erkenntnisse:

- Der Simulationspfad `0xCDE60` ruft den Niederlagenwriter `0x1D160` vor der nachfolgenden Spieler-/Lordverarbeitung in `0x57330` auf.
- `0x1D160` ist Vanillas normaler Laufzeitwriter von `GamePlayerResources.r_WinLossState` und prüft periodisch die Lordgültigkeit.
- Relevante Spielerfelder: `r_LordUnitId=0x21F8`, `r_IsPaused=0x2210`, `r_WinLossState=0x2244`; Spieler- und Unit-IDs sind 1-basiert.
- APIShared beobachtet die acht Spielerrecords im `OnTick` vor Vanillas Simulationsschritt. Eine Mutation des aktuellen nativen Schritts wird daher regelmäßig erst beim nächsten Managed-Tick sichtbar.
- Frühere Analyse verfolgte außerdem Lordschaden über `0x199110` sowie die KI-Schleife `0x57330 -> 0xC8F50 -> 0xC90E0`. Funktionsnamen außerhalb kuratierter Claims besitzen teilweise nur Candidate-Vertrauen; Adressen und Kontrollfluss müssen im neuen Audit erneut über die Baseline belegt werden.

Die vorhandene Wissensbasis liegt unter `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PLAYER_DEFEAT.md` und `CHORE_SYSTEM.md`.

## Belastbar ausgeschlossene beziehungsweise widerlegte Hypothesen

### KI-Zugänglichkeit, PCL und Friendly Moat

Die kompakte `TEMP_AI_ACCESSIBILITY_DIAG` verglich im vorherigen vollständigen Lauf Host und Client:

- 22 KI-Sweeps pro Peer;
- 379 Gebäudeprüfungen;
- keine fehlenden Gegenstücke;
- keine unterschiedlichen Rückgabewerte;
- keine unterschiedlichen Kandidatenfolgen;
- keine unterschiedlichen `0xE2610`-Regionsresultate;
- identische PCL- und Portalhashes in allen Sweeps.

Unmittelbar vor dem Resync waren die Eingangshashes weiterhin identisch. Im betreffenden Sweep gab es nicht einmal Kandidatengenerator-Aufrufe. Der untersuchte KI-Zugänglichkeits-/Abrisspfad, Friendly-Moat-Detour sowie PCL-/Portalzustand erklären den Surrender-Desync daher nicht.

Die Diagnose selbst deutete Werte aus `0xBFFB0`/`0xC0270` zunächst fälschlich als absolute Kartenkoordinaten. Der native Audit zeigte, dass es relative Kandidatenoffsets aus statischen Formtabellen sind. Darum waren Diagnosewerte wie `xy=-1,2` und daraus berechnete Tiles ungültig. Die identischen Sequenzhashes bleiben zur Peer-Gleichheit der Kontrollfolge verwendbar; Tile-, Höhen-, Overlay- und PCL-Angaben dieser Kandidatenzeilen dürfen nicht als Evidenz benutzt werden.

### Warum Gebäude 203 zunächst wie die Ursache aussah

In einem älteren Lauf lieferte die Zugänglichkeitsprüfung für Gebäude 203 auf dem Host Ergebnis 2 und auf dem Client Ergebnis 1. Wegen der Pre-Tick-Beobachtung wurde diese Änderung dem nativen Tick 200 und der folgende Snapshot Tick 201 zugeordnet; der damals protokollierte synchronisierte Lord-Kill erschien erst bei Tick 212. Das machte den Gebäude-/KI-Pfad zunächst zum frühesten sichtbaren Unterschied.

Dieser Einzelbefund ist real als beobachtete unterschiedliche Rückgabe, aber nicht als Ursache des wiederholbaren Surrender-Desyncs bestätigt. Dafür sprechen mehrere Punkte:

- Spätere reproduzierte Surrender-Desyncs liefen bei identischen PCL-/Portalhashes, identischen Kandidatenfolgen, identischen Regionsresultaten und 379 vollständig übereinstimmenden Gebäudeentscheidungen ab. Der Surrender-Desync benötigt die Gebäudeabweichung daher nicht.
- Die damalige zeitliche Reihenfolge vermischte Managed-Pre-Tick, nativen Simulationsschritt, Chore-Ausführung, APIShared-Beobachtung und `ElapsedMapTick`. Die neuesten Logs zeigen selbst widersprüchlich wirkende Lordtod-Ticks nach dem bereits ausgeführten Spectator-Chore. Ohne gemeinsame Phasenmarke belegt `200 < 212` nicht sicher, dass die Gebäudeentscheidung vor jeder Surrender-/Lordtod-Mutation lag.
- Eine Gebäudeentscheidung kann ein **Folgesymptom** eines bereits abweichenden, damals noch nicht erfassten Zustands sein. Beispielsweise können Niederlagenbereinigung, Slot-/Global-ID-Wechsel, Arbeitszuweisungen, Gruppen oder interne Pathfinding-Arbeitsdaten bereits divergieren; die nächste KI-Zugänglichkeitsabfrage macht diesen Unterschied nur erstmals sichtbar.
- Das unterschiedliche Ergebnis kann auch eine **separate intermittierende Divergenz** desselben Testlaufs gewesen sein. Dass eine frühere unabhängige Abweichung vorlag, widerspricht nicht dem inzwischen mehrfach separat reproduzierten Surrender-Fehler.
- Die sehr umfangreiche frühere Diagnose erzeugte massiven Lag und veränderte die zeitliche Belastung beider Prozesse. Ein timingabhängiger Diagnoseartefakt oder eine unterschiedliche Zuordnung nicht exakt korrespondierender Tickzeilen ist deshalb plausibel. Die Diagnose darf nicht als neutrales Messinstrument vorausgesetzt werden.
- Ein unterschiedlicher Rückgabewert beweist zunächst nur einen abweichenden Leserpfad. Ob daraus ein persistenter deterministischer Writer folgte oder ob Vanilla den Zustand später wieder zusammenführte, wurde damals nicht erfasst.

Die beste aktuelle Einordnung lautet deshalb: Gebäude 203 war entweder ein nachgelagerter Anzeiger eines schon bestehenden Zustandsunterschieds, eine separate intermittierende Abweichung oder ein durch die damalige Diagnose/Zuordnung begünstigter Befund. Es ist nach den späteren Gegenproben sehr unwahrscheinlich, dass dieser KI-Gebäudepfad die notwendige Grundursache des Surrender-/Lordtod-Desyncs ist. Er darf erneut untersucht werden, falls ein künftiger sauber phasenmarkierter Lauf ihn als **ersten Writer** bestätigt, soll aber nicht mehr die nächste Diagnose dominieren.

### Rohhashes kompletter Building-Records

265 von 379 vollständigen Rohrecord-Hashes unterschieden sich schon ab Tick 201 zwischen den Prozessen, obwohl alle bekannten semantischen Felder und alle Entscheidungen identisch waren. 34 Records änderten sich zudem während einer Prüfung. Die Rohrecords enthalten damit transiente, Padding- oder prozesslokale Daten. Ein Hash des vollständigen Records ist kein deterministischer Peer-Hash und darf nicht als Desync-Beweis verwendet werden.

### Ausgewählter Lord und Troop-HUD

Ein früherer Lauf erzeugte nach dem Tod eines ausgewählten Lords Lookups mit Unit-ID 0 im Gesundheits-Overlay. Der HUD-Pfad wurde mit einem Guard für `unitId <= 0` abgesichert. Er liest lokale Darstellungsdaten und sendet weder Chores noch Spielaktionen. Dieser Fehler erklärte die HUD-Ausnahmen, aber nicht den Lockstep-Desync.

### `TryGetUnitById(-1)` und Script Extender

Die frühere Vermutung, ein unbekannter Script-Extender-Aufrufer oder Vanillas Slot 0 verursache `[-1/9999]`, war falsch. Der Aufrufer lag im Friendly-Moat-Code von BugfixesAndQoL (`CallBuildingCursorWithRegions`). Ungültige Unit-/Building-IDs werden inzwischen vor jeder ID-API abgefangen und unverändert an Vanillas Originalfunktion weitergereicht. `TryGetUnitById` weist ungültige IDs vor einem nativen Speicherzugriff sicher zurück; der reine fehlgeschlagene Lookup ist kein plausibler Desync-Writer. Ein hierzu formulierter Extender-Report ist zurückgezogen und darf nicht erneut verwendet werden.

### Chore-History-Parser

Der ältere `RESYNC_CHORE_HISTORY`-Parser meldet nahezu jeden Puffer als `malformed=length--1` und liest offensichtlich falsche Recordgrenzen, Ziele, Längen und Opcodes. Seine dekodierten Payloads sind nicht evidenzfähig. Die direkten Hooks auf ausgehende Opcodes 54 und 67 sowie die Zustandswechsel von `HUD_MPResync` sind dagegen verwendbar.

## Wichtigste offene Hypothesen

Diese Punkte sind ergebnisoffen und in dieser Reihenfolge zu untersuchen:

1. **Ausführungsphase von `KillUnit`:** Der verwaltete Chore-Callback kann auf beiden Peers denselben angezeigten Tick besitzen und trotzdem relativ zum nativen Simulationsschritt an einer unterschiedlichen Stelle laufen. Der vollständige Chore-Dispatch- und `KillUnit`-Callflow muss bis zu allen Writer- und Cleanup-Pfaden auditiert werden.
2. **Niederlagenbereinigung:** Lordtod und `WinLossState.Loss` können Units, Buildings, Tribes, Arbeitszuweisungen, Gruppen, Waren oder KI-Zustände massenhaft verändern. Der erste abweichende deterministische Record kann in einem dieser nachgelagerten Writer liegen. Die bisherigen kleinen Checkpoints enthalten nur lebende Spieler und Lordidentitäten und können diese Abweichung nicht sehen.
3. **Lokale Spectator-GameAction:** Die Umschaltung ist chore-synchronisiert und wird absichtlich nur auf dem betroffenen lokalen Peer ausgeführt. Es muss nativ belegt werden, dass `GameAction(SpectatorMode)` ausschließlich Präsentations-/Kontrollzustand verändert und keinen vom Lockstep gehashten Zustand oder eine weitere Simulationseingabe verändert.
4. **APIShared-Beobachtungszeitpunkt:** Die abweichenden `lordDeathTick`-Werte und ihre scheinbare Lage nach dem Spectator-Ausführungstick verlangen eine saubere Zuordnung von `Director`-Tick, `ElapsedMapTick`, Chore-Tick und Pre-/Post-Simulation.
5. **Unit-Limit-Cache bei Massenbereinigung:** In allen Läufen meldet Unit Limit beim Worker-Übergang ungültige `eventOwner`-Werte (`0` oder große negative, pro Prozess unterschiedliche Werte) und ersetzt sie durch `snapshotOwner`. Der Fallback verhindert bislang erkennbare Fehlzählungen, doch der Eventvertrag und alle Cache-Mutationen während Surrender-Cleanup müssen geprüft werden. Prozesslokale Garbage-Werte dürfen niemals in deterministischen Entscheidungen eingehen.
6. **Global-ID-/Slot-Wiederverwendung:** Nach Niederlage kann die alte Lord-Unit-ID bestehen bleiben, während der Slot oder die Global-ID später wiederverwendet wird. Alle Vergleiche müssen 1-basierte Game-ID, Global-ID, Alive-State und Generation getrennt behandeln.

## Anforderungen an die nächste Diagnose

- Vor einer neuen Instrumentierung den vollständigen featurebezogenen Vanilla-Audit von Chore-Dispatch, `KillUnit`, Lordtod, Loss-Writer, Niederlagenbereinigung und Spectator-GameAction abschließen und mit Hash, RVAs, Parametern, ID-Basen, Writerfeldern und Folgezuständen dokumentieren.
- Keine erneute Vollzustandsprotokollierung. Die frühere `TEMP_SURRENDER_RESYNC_DIAG` erzeugte Logs von ungefähr 830 MB und 472 MB, verursachte extremen Lag und konnte dadurch den Test selbst beeinflussen.
- Zuerst vorhandene deterministische Vanilla-Sync-/Checksum-Grenzen suchen. Wenn möglich, den ersten unterschiedlichen Subsystem-/Objekthash in Speicher halten und nur bei Opcode 54 kompakt ausgeben.
- Nur auditierte deterministische Felder hashen. Keine vollständigen Rawrecords, Pointer, Padding, lokale UI-Daten, Rollenflags, Framezeiten oder prozesslokale Handles in Peer-Vergleichshashes aufnehmen.
- Surrender, Lordtod und Spectator auf beiden Peers und bei jedem Ereignis erfassen; kein Host-only- oder einmal-pro-Karte-Guard.
- Tick-Domänen ausdrücklich getrennt loggen: Unity-Frame, Thread, Chore-/Director-Tick, `ElapsedMapTick`, Pre-/Post-Simulationsphase.
- Diagnosecode vollständig mit einem eindeutigen `TEMP_..._DIAG`-Marker kapseln, begrenzen und nach Ursachenfund wieder entfernen.
- Alternativ beziehungsweise ergänzend einen binären Isolationstest planen: Surrender-Chore mit Lordtod aber ohne Spectator-Aktion, danach Spectator-Aktion ohne modseitigen Kill. Eine solche Verhaltensänderung ist ein separater Testbuild und darf erst nach Audit und ausdrücklicher Festlegung erfolgen.

## Weitere gefundene Modfehler

Diese Fehler wurden in denselben oder vorangegangenen Logs gefunden, sind aber nicht als Ursache des Surrender-Desyncs belegt:

- **BugfixesAndQoL / Assassin Climb:** `CaptureSelectionState` ruft `GetSelectedChimps()` auf, während der Extender transient eine negative Auswahlanzahl liefert. Das erzeugt clientseitig `ArgumentOutOfRangeException`. Vor dem Aufruf `GetSelectedChimpsCount()` prüfen; negative oder unplausible Werte als transient behandeln und das Feature nicht dauerhaft deaktivieren.
- **ExtraFeatures / Knight Mount-Dismount:** Derselbe transiente Auswahlfehler tritt in `GetSelectedChimpsSafe()` auf. Die Ausnahme wird abgefangen, aber doppelt geloggt. Ebenfalls Count-Guard verwenden und transient leer zurückkehren.
- **CastlePlanner / Vanilla-Human-Start:** Es wird nur AIV-Kandidat 0 importiert, während die native Auswahl anschließend Kandidat 3 zurückgeben kann. Der Code bricht fail-closed ab und lässt Vanilla weiterarbeiten. Der Fehler trat auf beiden Peers gleich auf und erklärt den Desync nicht; Kandidatenbank/Reset und Auswahlvertrag müssen separat korrigiert werden.
- **CastlePlanner / Fearfactor:** Ein ergänzendes `MAPPER_STOCKS`-Objekt wurde wegen `footprint-out-of-bounds` ausgelassen. Nicht fatal, aber die geplante Ergänzung fehlt.
- **Unit Limit:** Wiederholte, bereits gedrosselte Warnungen über nicht passende Transition-Owner; siehe offene Hypothese oben.
- Fehlende optionale Modlogos, `MapArchive`-Hinweise, fehlende Lua-Quellen und die bekannten CoarseGrid-/`GameAIVManagerAPI`-Workarounds sind für diesen Desync nicht relevant. Die `GameAIVManagerAPI`-Fälle werden laut Nutzer bereits in einem anderen Chat bearbeitet und sollen hier unberührt bleiben.

## Arbeitsbaum und Schutzregeln für den nächsten Chat

- Der Arbeitsbaum enthält umfangreiche bestehende Änderungen an BugfixesAndQoL, MoatMove und Tests. Sie gehören zum laufenden Debugstand und dürfen nicht zurückgesetzt oder überschrieben werden.
- Die ausgeschöpfte `TEMP_AI_ACCESSIBILITY_DIAG` samt vier nativen Diagnose-Detours, Runtime-Wrappern, Projekt-Eintrag und ausschließlich zugehörigen Tests wurde am 19. September 2026 entfernt. Der produktive KI-Schutz, Friendly-Moat-Code und der Guard gegen nichtpositive Unit-/Building-IDs bleiben erhalten.
- Tests dürfen ausschließlich modrelevante Quellen und Artefakte voraussetzen. Sie dürfen diesen Übergabebericht, andere Findings-Dateien oder externe Bugreports nicht als Testvoraussetzung verwenden.
- Der kanonische Script-Extender-Fork darf nicht verändert werden. Falls dort ein belegter Fehler gefunden wird, nur einen kurzen englischen Markdown-Report für den Autor formulieren.
- Fixes-Mod und Script Extender vor jedem neuen Hook auf Konflikte prüfen. `GameAIVManagerAPI`-/CoarseGrid-Dateien nicht anfassen.
- Keine README- oder Versionsänderung während der Ursachenforschung.

## Relevante Logquellen

- Host: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`
- Client: `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`
- Alte Logs: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\logs`

Die Logs sind Append-Logs. Den neuesten Spielstart immer über die letzte Zeile nach dem Muster `[Message: BepInEx] BepInEx ... - Stronghold Crusader Definitive Edition` abgrenzen; nicht die unzuverlässige Uhrzeit dieser Startzeile verwenden.
