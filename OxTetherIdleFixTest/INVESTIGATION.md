# Ox Tether Idle FixTest – Untersuchungsakte

Stand: 4. September 2026

## Ziel

Dieser Testmod untersucht den Vanilla-Bug, bei dem Steinbruch-Ochsen gelegentlich inaktiv stehen bleiben. Laut ursprünglichem Spielerbericht tritt dies häufiger bei sehr eng gebauten Ochsenstationen und gemeinsam genutzten schmalen Wegen auf. Schlafenlegen und erneutes Aktivieren der zugehörigen Station lässt den Ochsen wieder weiterarbeiten.

Die Untersuchung soll drei voneinander getrennte Aussagen belegen:

1. Die vermutete native Ursache kann im echten Vanilla-Ablauf entstehen.
2. Der Diagnosecode erkennt ausschließlich diese echte Fehlersignatur.
3. Das Löschen von `r_PathPlanRelated3` behebt eine bestätigte Episode, ohne AI-Zustand, Waren, Timer oder Gebäudezuordnung künstlich zu verändern.

## Zielumgebung und Verträge

- Spielversion: `2.8.0.1`
- Kanonische `CrusaderDE.dll`, SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Native Baseline: `_inspect/CrusaderDE-Native-Baseline`, semantischer Stand `sem/FBCB9319`
- Script Extender: `1.42.0`, Commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Modversion während der Untersuchung: `0.1.0`
- Harte Abhängigkeit: `000shcdese`
- `NetworkMode=1`
- Unit- und Building-Game-IDs sind 1-basiert; direkte native Arrays und Spans sind 0-basiert.

Die Runtime prüft den nativen Hash fail-closed. Die verwendeten `GameUnit`-Offsets werden beim Start gegen die auditierte Script-Extender-Struktur geprüft. Bei einem abweichenden Spielstand oder fehlerhaftem Speicherzugriff darf kein Diagnose- oder Schreibzugriff stattfinden.

## Statischer Befund aus der nativen Baseline

Die hashgleiche Baseline stützt folgende Fehlerkette:

1. Ein Ochse erhält ein exaktes Ziel für eine Fahrt zum Steinbruch beziehungsweise Lager.
2. Ist dieses exakte Feld beim Pathfinding blockiert, kann der Pathfinder ein alternatives, gültiges Endfeld wählen.
3. Nach Erreichen dieses alternativen Endfelds ist kein Pfad mehr aktiv, `r_PathPlanRelated3` bleibt jedoch ungleich null.
4. Die Vanilla-Ankunftsprüfung akzeptiert die Ankunft wegen dieses Markers nicht.
5. Der Ochse bleibt dadurch in Reisezustand 1 oder 3, obwohl kein aktiver Pfad mehr vorliegt.
6. Für diesen Zustand existiert kein ausreichender Vanilla-Watchdog. Schlafen und Aufwecken der Station stößt die Zustandsmaschine neu an und erklärt die Selbstheilung aus dem Spielerbericht.

Eng gebaute Ochsenstationen sind nach diesem Befund nicht selbst die unmittelbare Ursache. Sie erhöhen lediglich die Wahrscheinlichkeit, dass mehrere Einheiten gleichzeitig um dasselbe exakte Ziel oder denselben engen Anfahrtsbereich konkurrieren.

Dieser statische Befund ist noch kein Laufzeitnachweis dafür, dass die vollständige Kette im aktuellen Testaufbau natürlich entstanden ist.

## Exakte Diagnose- und Reparaturbedingung

Ein Ochse gilt erst nach 50 aufeinanderfolgenden Simulationsticks als bestätigte Episode, wenn währenddessen alle folgenden Eigenschaften unverändert bleiben:

- lebender `CHIMP_TYPE_QUARRY_OX`
- identische Unit-ID und Global-ID
- `r_AIState` ist 1 oder 3
- `r_PathPlanStateBitFlags == 0`
- `r_PathPlanRelated3 != 0`
- aktuelle Position weicht vom angeforderten Ziel ab
- Position, Ziel, Zustand und Alternativmarker bleiben unverändert

Bei Bestätigung schreibt der Fix ausschließlich:

    r_PathPlanRelated3 = 0

Danach wird 20 Ticks lang auf die Vanilla-Folgereaktion gewartet:

- Zustand 1 muss nach Zustand 2 wechseln.
- Zustand 3 muss nach Zustand 4 wechseln.

Die relevanten Erfolgsmarker sind:

- `OX_IDLE_BUG_CONFIRMED`
- `OX_IDLE_FIX_APPLIED`
- `OX_IDLE_FIX_VERIFIED`

Ein ausbleibender erwarteter Zustandswechsel wird als `OX_IDLE_FIX_UNVERIFIED` protokolliert. Eine Reparatur erfolgt pro unveränderter Fehlerepisode höchstens einmal.

## Lebenszyklusfehler im ersten Runtime-Aufbau

Eine frühe Fassung behandelte `BaseUnityPlugin.OnDestroy()` wie ein echtes Prozessende und entfernte dort langfristige Tick- und Event-Subscriptions. Das ist in SHCDE falsch: Früh in `Awake()` erzeugte BepInEx-/Unity-Komponenten werden während des normalen Startvorgangs zerstört. Ein `READY`-Log allein beweist daher nicht, dass die Runtime später auf der Karte noch aktiv ist.

Der relevante Script-Extender-Report ist:

<https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/128>

Der dort beschriebene Dispatcher kann sich selbst neu erzeugen. Diese Selbstheilung gilt nicht automatisch für beliebige Mod-Runtimes, native Hooks oder bereits entfernte Subscriptions. Die Lösung im Testmod ist deshalb:

- statisch verwurzelte, prozessweite Runtime
- Initialisierung nach `CrusaderLibrary.LibraryLoaded`
- kein Teardown der Runtime im normalen Plugin-`OnDestroy()`
- simulationsgebundene Arbeit über `GameTimeManagerAPI.OnTick`
- später Karten- und Tickmarker als tatsächlicher Laufzeitnachweis

Als Lifecycle-Vorlage ist `ExtraFeaturesPlugin` geeignet. `AIDefensePlugin` und `ImprovedHuntersPlugin` sind unfertige Testmods. `CustomCustomTrailPlugin` funktioniert zwar weitgehend, war zum Untersuchungszeitpunkt aber noch nicht veröffentlicht und ist daher ebenfalls nicht die bevorzugte kanonische Vorlage.

## Testaufbau für natürliche Reproduktion

Verwendet wurde insbesondere das Savegame `steintestox2.sav` mit folgenden Eigenschaften:

- mehrere, möglichst dicht gebaute Ochsenstationen
- gemeinsamer Steinbruch- und Lagerverkehr
- schmale Gasse zum Lager
- hohe Spielgeschwindigkeit
- viele gleichzeitige Hin- und Rückfahrten

Trotz eines vergleichsweise langen Laufs entstand keine natürliche exakte Fehlersignatur. Die allgemeinen Stalllogs zeigten wiederholt Stillstände von ungefähr 50 bis 83 Ticks, anschließend aber normale Bewegung. Solche Anlauf- oder Zwischenpausen sind daher keine ausreichende Bugdiagnose.

Ein gesetzter Alternativmarker allein ist ebenfalls kein Fehler. In den Logs traten beispielsweise Ochsen mit Marker `200` und weiterhin aktivem Pfad (`pathFlags=2`) auf, die anschließend normal weiterliefen.

## Verworfener synthetischer Fehlerinjektor

Eine frühe Injektorversion lief alle 30 Sekunden und erzeugte die Signatur direkt am Ziel-Ochsen:

- `p_CurrentPathPlanPosition` wurde an das Ende des Pfads gesetzt.
- `r_PathPlanRelated3` wurde beibehalten beziehungsweise künstlich gesetzt.
- Vanilla setzte anschließend den Pfadstatus auf 0.
- neu entstehende Vanilla-Replans wurden fortlaufend unterdrückt, damit die erzwungene Signatur 50 Ticks bestehen blieb.

Beispiel aus dem Log vom 4. September 2026:

- Injektion für Unit 222 bei Tick 2820
- Vanilla-Terminalisierung bei Tick 2830
- 24 unterdrückte Replans
- `OX_IDLE_BUG_CONFIRMED` bei Tick 2879
- `OX_IDLE_FIX_APPLIED` bei Tick 2879
- `OX_IDLE_FIX_VERIFIED` bei Tick 2880, Zustand 3 nach 4

Weitere Episoden wurden ebenfalls im Abstand von etwa 30 Sekunden erzeugt.

Erkenntnis: Dieser Versuch belegt, dass Diagnose, Einmaligkeitslogik und der minimale Fix für eine vorhandene Signatur funktionieren. Er belegt nicht, dass die Signatur durch die vermutete Vanilla-Zielblockade entsteht. Durch das direkte Vorziehen des Pfadcursors und die Replan-Unterdrückung wurde zu viel von dem zu beweisenden Verhalten künstlich vorgegeben. Dieser Injektor wurde daher entfernt.

## Verworfene direkte Belegungsraster-Manipulation

Ein zwischenzeitlich betrachteter Ansatz wollte das Ziel direkt im `TileUnitIdGrid` mit der ID einer anderen Einheit markieren. Das wurde verworfen, weil dadurch lediglich interne Belegungsdaten behauptet werden, ohne dass dort zwingend eine physische Vanilla-Einheit steht. Außerdem drohen inkonsistente oder verwaiste Rastereinträge.

Für `StockpileAccessFixTest` wurde parallel ein anderer Testansatz diskutiert, bei dem eine zivile Einheit auf das Feld teleportiert wird. Die Erkenntnis aus diesem Projekt ist, dass eine echte Vanilla-Belegung vorzuziehen ist: Eine Einheit soll das Feld über Vanillas Bewegungssystem betreten und auch wieder über Vanilla verlassen. Eine direkte Rasteränderung ist kein belastbarer Nachweis einer natürlichen Blockade.

## Fehlschlag: stationären Ochsen direkt auf das Ziel teleportieren

Die erste physische Blockade teleportierte einen zweiten lebenden Steinbruch-Ochsen direkt auf das leere Zielfeld. Die Positionsfelder zeigten danach zwar die Zielkoordinate, `TileUnitIdGrid` blieb jedoch 0. Zwei Versuche liefen nach 50 Ticks in einen Belegungs-Timeout. Ein dritter beobachtete lediglich eine bereits natürlich vorhandene Belegung.

Ursache: Als Blockierer war unter anderem ein stationärer Ochse in Zustand 4 ohne aktiven Pfad ausgewählt worden. `GameUnitManagerAPI.SetCurrentLocalTilePosition` schreibt Positions-, Ziel- und Interpolationsfelder, pflegt aber nicht das Vanilla-Unit-Belegungsraster. Eine bloße Teleportposition ist daher keine physische Vanilla-Belegung.

Folgerungen:

- Stationäre Ochsen sind als Blockierer ungeeignet.
- Ein Blockierer muss selbst einen aktiven Vanilla-Pfad besitzen.
- Das Zielfeld darf nicht direkt im Raster beschrieben werden.
- Nach einem bestätigten Rastereintrag darf der Blockierer nicht einfach wegteleportiert werden, weil Vanilla das Feld sonst möglicherweise nicht konsistent freigibt.

## Physischer Ansatz: Nachbarfeld plus Vanilla-`MoveToTile`

Der nächste Ansatz wählte nur laufende Steinbruch-Ochsen in Zustand 1 oder 3 mit aktivem Pfad. Ein rasterfreier Blockierer sollte auf ein freies, begehbares Nachbarfeld des Zieles gesetzt werden und anschließend über `MoveToTile` physisch in das Ziel laufen.

Zusätzliche Sicherheitsregeln:

- Ursprung, Ziel und Nachbarfeld werden über das Vanilla-Raster geprüft.
- Ein Blockierer mit einer anderen Einheit im Ursprungsraster wird nicht verwendet.
- Ist der Blockierer an seinem Ursprung selbst im Raster registriert, wird er nicht teleportiert, sondern direkt von dort per Vanilla-`MoveToTile` umgeleitet.
- Nach bestätigter Zielbelegung verlässt er das Feld ausschließlich per Vanilla-Bewegung.
- Der Ziel-Ochse und sein Pathfinding-Speicher werden durch diesen Auslöser nicht direkt verändert.

In den bisherigen Läufen wurde ausschließlich der sichere Fallback `VanillaMoveToTileFromCurrentPosition` benötigt; es stand kein geeigneter rasterfreier laufender Ochse für den Nachbarfeld-Teleport zur Verfügung.

## Fehlschlag: entfernungsbasierter Gesamt-Timeout

Die erste `MoveToTile`-Fassung berechnete einen festen Gesamt-Timeout aus Entfernung und einem Grundwert. Zwei dokumentierte Versuche:

### Versuch 1

- Ziel-Ox: Unit 2
- Blockierer: Unit 8
- Blockiererpfad: 53 Schritte
- Entfernung: 39 Felder
- Timeout: 206 Ticks
- Ergebnis: `vanillaOccupancyTimeout`

### Versuch 2

- Ziel-Ox: Unit 3
- Blockierer: Unit 7
- Blockiererpfad: 9 Schritte
- Entfernung: 5 Felder
- Timeout: 70 Ticks
- Ergebnis: `vanillaOccupancyTimeout`

Der zweite Blockierer bewegte sich währenddessen nachweislich von `530/283` über mehrere Felder bis `532/283`; sein Pfadcursor stieg von 0 auf 4 von 9. Der Timeout brach somit funktionierende Vanilla-Bewegung kurz vor dem Ziel ab.

Folgerung: Die Dauer darf nicht aus geometrischer Entfernung geschätzt werden. Spielgeschwindigkeit, Bewegungsphase und interne Achtelschritte machen einen festen Gesamt-Timeout ungeeignet.

## Aktueller Fortschritts-Watchdog

Der feste Gesamt-Timeout wurde durch einen Fortschritts-Watchdog ersetzt:

- Unbegrenzte Gesamtlaufzeit, solange der Blockierer echte Fortschritte macht.
- Fortschritt ist eine geänderte Kachelposition oder ein vorwärts laufender Pfadcursor.
- Animationen und bloße Cursor-Resets gelten nicht als Fortschritt.
- Abbruch erst nach 250 aufeinanderfolgenden Ticks ohne Fortschritt.
- Sofortiger Abbruch bei Identitätswechsel, verlorenem Pfad oder geändertem Ziel.
- Der allgemeine Stall-Wächter darf frühestens 50 Ticks nach bestätigter Zielbelegung eingreifen. Vor der Belegung aufgelaufene Stillstandszeit zählt hierfür nicht.

Die zugehörigen Policytests erhöhten den Stand auf 75 bestandene Assertions.

Beim ersten Build dieser Änderung trat der Compilerfehler `CS0136` auf: Eine Fehlertextvariable und der neue Snapshot hießen beide `blockerAfterCommand`. Die Fehlertextvariable wurde in `blockerAfterCommandDescription` umbenannt. Der anschließende Build lief mit 0 Warnungen und 0 Fehlern durch und wurde erfolgreich installiert.

## Erster erfolgreicher physischer Belegungsnachweis

Der Lauf ab 12:32 Uhr bestätigte, dass der Fortschritts-Watchdog das ursprüngliche Timeoutproblem löst:

- 5 neue Blockadeversuche
- 1 echte Belegung über `VanillaTileUnitIdGrid`
- 0 Laufzeitfehler
- 0 Bugkandidaten
- 0 bestätigte oder reparierte natürliche Episoden

Erfolgreicher Belegungsversuch:

- Ziel-Ox: Unit 2, Zustand 3
- Blockierer: Unit 8, anfänglich Zustand 1
- Ziel: `482/230`
- Blockiererpfad nach Umleitung: 53 Schritte
- Starttick: 2631
- Belegungsbestätigung: Tick 3483
- Anfahrt: 852 Ticks
- Rasterwert am Ziel: Unit-ID 8
- Marker: `OX_IDLE_TARGET_BLOCKADE_OCCUPANCY_CONFIRMED`

Die Belegung bestand nur 16 Ticks. Danach wechselte der Blockierer von Zustand 1 nach 2 und verließ das Zielfeld. Beim Verlassen befand sich der Ziel-Ochse erst bei `480/243`, also noch ungefähr 13 Felder entfernt, und besaß weiterhin seinen schon vor der Blockade berechneten aktiven Pfad.

Die übrigen vier Versuche endeten mit `realOccupantTookTarget`: Eine andere Einheit oder der Ziel-Ochse selbst erreichte das Feld vor dem vorgesehenen Blockierer.

## Aktueller Erkenntnisstand

Als belegt gelten inzwischen:

- Die Runtime überlebt den SHCDE-Startup-Cleanup und tickt auf der Karte weiter.
- Die Diagnose erkennt die erzwungene exakte Signatur.
- Das alleinige Löschen von `r_PathPlanRelated3` führte bei erzwungenen Episoden zur erwarteten Vanilla-Zustandsänderung und wurde als `FIX_VERIFIED` bestätigt.
- Ein umgeleiteter physischer Ochse kann das exakte Zielfeld über Vanilla erreichen und im echten `TileUnitIdGrid` eingetragen werden.
- Der Fortschritts-Watchdog erlaubt auch sehr lange, aber funktionierende Anfahrten.
- Dichte Stationen und eine schmale Gasse haben im bisherigen Lauf allein keine natürliche exakte Fehlersignatur erzeugt.

Noch nicht belegt sind:

- Dass eine echte Zielbelegung im getesteten Vanilla-Ablauf den alternativen Endpunkt mit der erwarteten Markerkombination erzeugt.
- Dass daraus ohne direkte Pathfinding-Manipulation dauerhaft Zustand 1 oder 3 mit `pathFlags=0` und Marker ungleich null entsteht.
- Dass der minimale Fix eine solche natürlich beziehungsweise ausschließlich durch physische Belegung ausgelöste Episode repariert.

Der Mod ist daher weiterhin ein Testmod und noch kein abschließend bestätigter Vanilla-Fix.

## Nächster empfohlener Versuch

Das bisherige Timing blockiert ein Ziel, für das der Ziel-Ochse seinen Pfad bereits vorher berechnet hat. Die Belegung endet häufig, bevor der Ziel-Ochse das Feld erreicht oder einen neuen Pfad benötigt.

Der nächste gezielte Versuch sollte deshalb unmittelbar nach `OX_IDLE_TARGET_BLOCKADE_OCCUPANCY_CONFIRMED` genau einmal Vanillas `MoveToTile` für den Ziel-Ochsen mit demselben bereits angeforderten Ziel aufrufen. Damit entsteht die entscheidende Situation:

1. Das exakte Ziel ist nachweislich durch eine physische Vanilla-Einheit belegt.
2. Der Ziel-Ochse muss in diesem Moment einen neuen Vanilla-Pfad zu genau diesem Ziel berechnen.
3. Es werden keine Pathfinding-Felder, Marker, Zustände oder Rasterwerte direkt geschrieben.
4. Vorher- und Nachher-Snapshots müssen Pfadgröße, Pfadcursor, Ziele, Flags und Alternativmarker festhalten.
5. Anschließend darf der normale Diagnosecode unbeeinflusst beobachten, ob der Ochse am alternativen Endfeld die echte Fehlersignatur entwickelt.

Erst eine Episode mit der vollständigen Markerfolge

    OX_IDLE_TARGET_BLOCKADE_OCCUPANCY_CONFIRMED
    OX_IDLE_BUG_CONFIRMED
    OX_IDLE_FIX_APPLIED
    OX_IDLE_FIX_VERIFIED

bestätigt den physischen Auslöser und den Fix gemeinsam.

## Ingame-Testhinweise

1. `steintestox2.sav` oder einen vergleichbaren Aufbau laden.
2. Steinbruch, Lager und Ochsenstationen aktiv lassen.
3. Höchste Spielgeschwindigkeit verwenden.
4. Stationen während eines aktiven Versuchs nicht schlafen legen oder umschalten.
5. Mindestens mehrere 30-Sekunden-Zyklen abwarten.
6. Die sichtbare Bewegung eines Testblockierers ist beabsichtigt; seine ursprüngliche Route wird beim sauberen Abbruch soweit zustandsverträglich wieder ausgegeben.
7. Nach dem Test das komplette BepInEx-Log auswerten, nicht nur einen sichtbaren Stillstand.

## Relevante Logmarker

- `OX_IDLE_DIAGNOSTIC_READY`: Runtime und aktive Teststrategie
- `OX_IDLE_MAP_TRACKING_STARTED`: Karten- oder Save-Lauf begonnen
- `OX_IDLE_TARGET_BLOCKADE_APPLIED`: Blockadeversuch und vollständige Snapshots
- `OX_IDLE_TARGET_BLOCKADE_OCCUPANCY_CONFIRMED`: physische Vanilla-Rasterbelegung bestätigt
- `OX_IDLE_TARGET_BLOCKADE_RELEASED`: Abbruchgrund und Wiederherstellungsdisposition
- `OX_IDLE_GENERAL_STALL_CONFIRMED`: unspezifischer Stillstand, noch kein Bugbeweis
- `OX_IDLE_CANDIDATE_STARTED`: exakte Fehlersignatur erstmals beobachtet
- `OX_IDLE_BUG_CONFIRMED`: exakte Signatur 50 Ticks stabil
- `OX_IDLE_FIX_APPLIED`: ausschließlich Alternativmarker gelöscht
- `OX_IDLE_FIX_VERIFIED`: erwarteter Vanilla-Zustandswechsel eingetreten
- `OX_IDLE_FIX_UNVERIFIED`: erwarteter Zustandswechsel blieb aus
- `OX_IDLE_MAP_SUMMARY`: Zusammenfassung des Kartenlaufs

## Dokumentationsregel für weitere Versuche

Für jede neue Strategie sollen mindestens folgende Informationen in dieser Datei ergänzt werden:

- genaue Codeänderung und welche Felder oder Vanilla-APIs sie berührt
- Grund, warum der Versuch die zu beweisende Fehlerkette nicht vorwegnimmt
- Savegame und Aufbau
- Anzahl der Versuche
- relevante Ticks und Unit-/Global-IDs
- Belegungs-, Kandidaten-, Fix- und Abbruchmarker
- Ergebnis und verbleibende alternative Erklärungen
