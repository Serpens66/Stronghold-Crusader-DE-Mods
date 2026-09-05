# StockpileAccessFixTest – Untersuchungsstand

Stand: 4. September 2026  
Modversion: `0.1.0`  
Zielspielversion: `2.8.0.1`  
Zielversion des Script Extenders: `2.0.2`, Commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`

## Ausgangsfehler

Untersucht wird folgender Vanilla-Fehler:

> Wenn ein Arbeiter Rohstoffe aus einem Stockpile-Segment holen will, während er sich außerhalb des Stockpiles befindet, und der zunächst freie Zugang später unzugänglich wird, kann der Arbeiter dauerhaft im Abholzustand stehen bleiben. Er wählt dann keinen anderen erreichbaren Zugang, beispielsweise innerhalb des Stockpiles.

Der Testmod soll den Zustand diagnostizieren, reproduzierbar hervorrufen und anschließend ausschließlich mit Vanillas erneuter Zugangswahl und Vanillas Bewegung beheben. Er darf weder teleportierend reparieren noch das Erreichen des Ziels vortäuschen oder AI-Zustände direkt ändern.

## Bestätigte Native-Verträge

- Die geprüfte installierte `CrusaderDE.dll` besitzt SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Vanillas Funktion zur erneuten Gebäudezugangswahl liegt bei RVA `0xC90E0`. Der verwendete Vertrag lautet `(buildingManager, buildingId, requiredCandidate)`.
- Bewegung erfolgt ausschließlich über `GameUnitManagerAPI.MoveToTile`. Dadurch wird der vorhandene Script-Extender-Detour von Vanillas MoveHere bei RVA `0x196280` benutzt und nicht überlappt.
- `GameUnit` hat Größe `0x490`, `GameBuilding` Größe `0x32C`.
- Verwendete Felder und Offsets werden vor Aktivierung statisch geprüft. Dazu gehören unter anderem:
  - `GameUnit.r_PathPlanStateBitFlags` bei `0xF2`
  - `GameUnit.r_PathPlanRelated3` bei `0x290`
  - `GameUnit.r_AIState` bei `0x2BC`
  - Stockpile-Building-ID bei Roh-Offset `0x332`
  - gespeicherte Building-Global-ID bei Roh-Offset `0x9C`
  - Stockpile-Zugang X/Y bei `0xFE` und `0x100`
- Unit- und Building-Game-IDs sind 1-basiert; direkte native Arrayindizes sind 0-basiert.
- Bei Hash-, Pattern-, Layout-, Manager- oder Speicherfehlern arbeitet der Mod fail-closed und lässt Vanilla unverändert weiterlaufen.

## Unterstützte Worker und Fetch-Zustände

Der Fehlerdetektor deckt genau diese bestätigten Kombinationen ab:

| Worker | Fetch-Zustand |
|---|---:|
| Fletcher | 1 |
| Miller | 3 |
| Baker | 7 |
| Brewer | 2 |
| Poleturner | 2 |
| Blacksmith | 2 |
| Armourer | 2 |
| Innkeeper | 2 |

Die zugehörigen nativen Handler-Einträge werden durch Vertragstests gegen die installierte DLL geprüft.

## Bedeutung von `r_PathPlanRelated3`

Das Feld wurde anfangs im Log irreführend `pathMarker` genannt und ein Wert ungleich null wurde vorübergehend als Teil der Hängesignatur betrachtet. Das war falsch.

Die native Analyse von MoveHere ergab:

- MoveHere setzt das Feld zunächst auf `0` zurück.
- Nur wenn Start- und Zielregion verschiedene Path-Connection-Regionen benötigen, kann dort eine ausgewählte intermediäre beziehungsweise alternative Path-Connection-ID abgelegt werden.
- Das Feld ist kein Kennzeichen für menschliche oder KI-gesteuerte Einheiten.
- Menschliche Fletcher besitzen in den beobachteten normalen Fetch-Routen regelmäßig den Wert `0`.
- Auch Werte wie `200`, die überwiegend bei beobachteten KI-Einheiten vorkamen, erklären sich durch deren Route beziehungsweise Kartenregion und nicht durch den Besitzer.

Der Mod nennt den Wert deshalb jetzt `alternatePathConnectionId`. Er wird diagnostisch protokolliert und auf Änderungen während einer laufenden Episode geprüft, ist aber weder für die Bugerkennung noch für die Auswahl eines Testblockers erforderlich.

## Lage und Eigenschaften des Stockpile-Zugangs

Die von den Fletchern gespeicherten Zielkoordinaten entsprechen dem gecachten Gebäudezugang des Stockpiles. Die beobachteten Ziele `532/278` und `194/416` sind keine Felder neben dem Stockpile, sondern interne `GoodsyardConnection`-Tiles.

- `TilePropertyFlag.GoodsyardConnection` hat den kombinierten Wert `0x00000102`.
- Der enthaltene `IsWall`-Bit bedeutet hier nicht, dass dort eine normale Mauer steht. Die Kombination ist der spezielle passierbare Stockpile-Zugang.
- Normale Stockpile-Lagerfelder verwenden `Goodsyard` beziehungsweise `0x00000502` und sind nicht dasselbe wie der Zugang.
- Auf dem Zugang ist die Building-ID im beobachteten Lauf `0`.
- Die dynamische Belegung durch eine Unit wird separat im `TileUnitIdGrid` geführt. Deshalb bleibt das Property-Flag bei einer Unit-Belegung `0x102`.

Damit ist bestätigt, dass eine auf dem Zielfeld gemeldete Unit-ID eine echte dynamische Belegung des internen Stockpile-Zugangs darstellt.

## Ungeeignete oder fehlgeschlagene Reproduktionsansätze

### Soldaten als Blocker

Soldaten erwiesen sich für diesen Test nicht als geeignete Blocker. Ihre Anwesenheit erzeugte nicht zuverlässig die für den Worker-Pathfinder benötigte zivile Tile-Belegung beziehungsweise keinen reproduzierbaren dauerhaften Fetch-Fehler.

### Mauer oder Gebäude auf dem Ziel

Eine Mauer oder ein anderes Gebäude ist weder ein überzeugender normaler Vanilla-Auslöser noch ein sicherer Testmechanismus:

- Das Ziel liegt auf dem speziellen Stockpile-Zugang und damit innerhalb der Stockpile-Geometrie.
- Das künstliche Erstellen eines Bauwerks auf einem bereits zum Stockpile gehörenden Tile ist nicht als regulärer Vanilla-Bauvorgang abgesichert.
- Selbst wenn eine erzwungene Erzeugung technisch gelänge, würde sie einen anderen und deutlich invasiveren Blockadetyp testen.

Der aktuelle Mod erzeugt und löscht deshalb keine Mauern oder Gebäude und verändert keine Tile-Property- oder Building-ID-Raster direkt.

### Direkte Teleportation auf das Ziel

Das direkte Setzen der Positionskoordinaten einer Unit auf das Ziel ist kein gültiger Beleg für eine Blockade. Bei einem vergleichbaren Ox-Test wurde bestätigt, dass `SetCurrentLocalTilePosition` allein das `TileUnitIdGrid` nicht zuverlässig mit der Unit-ID belegt.

Daraus folgt für den Fletcher-Test:

1. Ein synthetischer Blocker darf nur auf ein freies Ausgangsfeld versetzt werden.
2. Er muss das Ziel danach über Vanillas `MoveToTile` erreichen.
3. Erst wenn `TileUnitIdGrid` am Ziel exakt seine Unit-ID enthält, gilt die Blockade als bestätigt.

### Erster Fletcher-Blockertest

Die erste zivile Automatik verlangte einen zweiten aktiven Fletcher desselben Stockpiles und suchte nur die vier orthogonalen Nachbarfelder des Zugangs ab.

Der Loglauf ab `2026-09-04 12:15:37` zeigte:

- 50 erkannte Fetch-Routen
- 7.409 `STOCKPILE_TEST_BLOCKER_READY`
- 5.147 `STOCKPILE_TEST_BLOCKER_FAILED`
- 0 erfolgreich gestartete Blocker
- 0 bestätigte Belegungen
- 0 injizierte Fehler
- 0 Fixdurchläufe

Häufigste Ursachen:

- 3.241-mal kein freies, als begehbar erkanntes orthogonales Nachbarfeld bei Zugang `532/278`
- 649-mal kein zweiter aktiver Fletcher für Stockpile 17
- 521-mal kein zweiter aktiver Fletcher für Stockpile 10
- weitere Ablehnungen, weil der Zugang bereits natürlich belegt war oder die vorgemerkte Route inzwischen endete

Zusätzlich konnten zwei abwechselnd vorgemerkte Fletcher den damaligen routenbezogenen Cooldown gegenseitig umgehen. Das verursachte die sehr große Zahl an Logmeldungen.

## Bestätigte natürliche Belegung

Die Logs belegen, dass normale zivile Units den internen Stockpile-Zugang in Vanilla tatsächlich belegen können. Am Zugang `532/278` wurden unter anderem die Unit-IDs `107`, `205`, `312` und weitere im nativen Raster beobachtet; auch `194/416` war wiederholt belegt.

Dies beantwortet eine zentrale Reproduktionsfrage:

- Ein ziviler Blocker auf dem Stockpile-Zugang ist ein real vorkommender Vanilla-Zustand.
- Das Tile bleibt dabei `GoodsyardConnection` mit Flag `0x102`.
- Die Belegung wird durch `TileUnitIdGrid` belegt und nicht aus der sichtbaren Position abgeleitet.

Eine gewöhnliche kurzzeitige zivile Belegung allein führte bisher jedoch nicht stabil zum gemeldeten Dauerfehler.

## Zweiter Loglauf mit natürlicher Belegung

Der Lauf ab `2026-09-04 12:32:22` verwendete die verbesserte Automatik:

- Natürliche Belegung wird vor einer synthetischen Manipulation bevorzugt.
- Die synthetische Ausgangssuche umfasst quadratische Ringe bis Radius 8 einschließlich diagonaler Felder.
- Ein Fletcher darf nur teleportiert werden, wenn sein aktuelles Tile keine Unit-ID im nativen Raster enthält. Dadurch bleibt am Ursprung keine verwaiste Belegung zurück.
- Der synthetische Blocker muss demselben Spieler gehören und ein vom Opfer unabhängiges Ziel besitzen.
- Ein registrierter synthetischer Blocker wird ausschließlich per Vanilla-`MoveToTile` zu seinem ursprünglichen Ziel zurückgeschickt.
- Nach einem fehlgeschlagenen Versuch gilt ein globaler Cooldown von 50 Ticks. Wechselnde Fletcher können ihn nicht umgehen.

Ergebnis dieses Laufs:

- 81 erkannte Fetch-Routen
- 220 gedrosselte Testversuche über ungefähr 12.000 Ticks
- 24 verwendete natürliche Belegungen
- 24 exakt im `TileUnitIdGrid` bestätigte Belegungen
- 0 synthetisch gestartete Fletcher-Blocker
- 1 `STOCKPILE_ACCESS_BUG_CANDIDATE`
- 0 `STOCKPILE_ACCESS_BUG_CONFIRMED`
- 0 Zugangsauswahlen und Fixanwendungen
- 0 Diagnose-Deaktivierungen oder Mod-Exceptions

Die synthetische Variante scheiterte 190-mal, weil kein gleichspielerischer, fahrender Fletcher mit unabhängigem Ziel und unbelegtem Ursprung verfügbar war. Sechsmal war eine vorgemerkte Route vor der Ausführung bereits beendet.

### Der entscheidende temporäre Pfadverlust

Bei Tick `2880` trat folgende Kette auf:

- Opfer: Fletcher Unit 217, Global-ID 7484841, Spieler 1
- Position: `533/278`
- Stockpile-Zugang und Ziel: `532/278`
- Blocker: natürlich anwesende Unit 107, Spieler 1
- Vorherige Path-Flags: `2`
- `TileUnitIdGrid` am Ziel: `107`
- Rückgabewert des erzwungenen `MoveToTile`: `1`
- Path-Flags danach: `0`
- Direkt anschließend: `STOCKPILE_ACCESS_BUG_CANDIDATE`

Der Kandidat blieb nicht 50 aufeinanderfolgende Ticks stabil. Vanilla setzte den Fetch-Zyklus beziehungsweise eine Route selbstständig fort, weshalb kein `BUG_CONFIRMED` und kein Fixversuch ausgelöst wurde. Das ist das gewünschte fail-closed-Verhalten des Detektors: Ein kurzer, selbstheilender Pfadverlust darf nicht als dauerhafter Bug behandelt werden.

Der Lauf zeigt außerdem, dass der MoveHere-Rückgabewert `1` lediglich die Annahme beziehungsweise Verarbeitung des Befehls bedeuten kann. Er beweist nicht, dass danach ein aktiver Pfad existiert. `pathFlagsAfter=0` ist für die Diagnose des tatsächlichen Pfadzustands aussagekräftiger. Die aktuelle Logbezeichnung `routeFailureCreated=False` ist in diesem Sonderfall daher zu streng und soll in einem nächsten Diagnoseschritt getrennt werden in:

- Move-Befehl angenommen oder abgelehnt
- aktiver Pfad nach dem Aufruf vorhanden oder nicht vorhanden
- Pfadverlust innerhalb von 50 Ticks selbst geheilt oder dauerhaft

## Aktuelle Hängesignatur

Ein Worker wird nur als Kandidat betrachtet, wenn gleichzeitig gilt:

- lebender unterstützter Worker im bestätigten Fetch-Zustand
- unveränderte Unit-ID, Unit-Global-ID und Workerart
- lebender eigener `STRUCT_GOODS_YARD`
- gespeicherte Building-Global-ID stimmt weiterhin
- Sekundärziel entspricht dem gecachten Stockpile-Zugang
- `r_PathPlanStateBitFlags == 0`
- Worker steht noch nicht auf dem Ziel
- unmittelbar zuvor, höchstens zwei Ticks früher, wurde für dieselbe Unit-, Stockpile- und Zielsignatur ein aktiver Fetch-Pfad beobachtet

Position, Ziel, Zustand, Storage- und Production-Building sowie `alternatePathConnectionId` müssen danach 50 fortlaufende Simulationsticks unverändert bleiben. Bewegung, Zielwechsel, Zustandswechsel, Slot-Wiederverwendung, unterbrochene Tickfolge oder eine wieder aktive Route setzen den Kandidaten zurück.

Die Einschränkung auf einen unmittelbar zuvor aktiven passenden Fetch-Pfad verhindert, dass ein normal wartender menschlicher Fletcher mit `alternatePathConnectionId=0` fälschlich als Bug erkannt wird.

## Recovery-Verhalten

Erst nach 50 bestätigten stabilen Ticks führt der Mod die eigentliche Korrektur aus:

1. Alten Gebäudezugang protokollieren.
2. Vanillas Zugangswahl bei RVA `0xC90E0` mit demselben Stockpile und `requiredCandidate=1` ausführen.
3. Neuen Zugang und nativen Rückgabewert protokollieren.
4. Bei gültigem Zugang `GameUnitManagerAPI.MoveToTile(unitId, newX, newY, 0)` ausführen.
5. AI-State, Fetch-Marker und `r_PathPlanRelated3` nicht direkt verändern.
6. Bewegung, Erreichen des neuen Zugangs und späteres Verlassen des Fetch-Zustands separat verifizieren.

Bei einem fehlgeschlagenen Recovery-Versuch gilt für denselben Worker ein Cooldown von 200 Ticks. Es gibt keine Teleport-Reparatur, keine Fernentnahme und kein vorgetäuschtes Erreichen des Ziels.

## Gegenwärtiger Beweisstand

Bestätigt sind:

- Ziel und gecachter Zugang liegen auf dem internen `GoodsyardConnection`-Tile des Stockpiles.
- Zivile Units können dieses Tile im normalen Vanilla-Spiel belegen.
- `TileUnitIdGrid` ist der maßgebliche Beleg für diese Belegung.
- Direkte Teleportation allein reicht dafür nicht.
- Natürliche Belegung kann bei einem Fletcher einen Wechsel von aktiven Path-Flags zu `0` auslösen.
- `MoveToTile` kann dabei `1` zurückgeben, obwohl unmittelbar danach kein aktiver Pfad vorliegt.
- Der aktuelle Detektor erkennt einen solchen Übergang als Kandidat und verwirft ihn korrekt, wenn Vanilla sich vor Tick 50 selbst heilt.
- `alternatePathConnectionId=0` ist bei menschlichen Fletchern normal und kein Ausschlusskriterium.
- Der globale 50-Tick-Cooldown verhindert die zuvor beobachtete Logflut.

Noch nicht bestätigt sind:

- eine automatisiert erzeugte Blockade, die den Fetch-Zustand mindestens 50 Ticks unverändert festhält
- ein vollständiger `BUG_CONFIRMED`-Durchlauf
- eine erfolgreiche erneute Vanilla-Zugangsauswahl nach einem bestätigten Dauerfehler
- `FIX_APPLIED`, anschließende Bewegung und `FIX_VERIFIED` im Spiel
- ob eine reine kurzzeitige zivile Belegung überhaupt der ursprüngliche dauerhafte Vanilla-Auslöser ist oder nur einen selbstheilenden verwandten Zustand erzeugt

Es gibt deshalb noch keinen Laufzeitbeweis, dass der Recovery-Fix den gemeldeten Dauerfehler behebt. Bisher bewiesen sind die Diagnosegrundlagen und ein temporärer, von Vanilla selbst geheilter Pfadverlust.

## Sinnvolle nächste Schritte

1. Die Injektionsauswertung so trennen, dass `moveResult`, `pathFlagsAfter` und die anschließende Stabilitätsdauer unabhängig protokolliert werden. `pathFlagsAfter=0` soll als erzeugter Pfadverlust gelten, auch wenn MoveHere `1` zurückgibt.
2. Einen erzeugten Pfadverlust weiter beobachten und nach weniger als 50 Ticks ausdrücklich als `transient/self-recovered` protokollieren, statt ihn nur still zurückzusetzen.
3. Eine sichere länger anhaltende zivile Blockadestrategie untersuchen. Sie muss das Ziel über native Bewegung belegen und später ebenfalls über native Bewegung räumen; direktes Schreiben des Unit-Rasters oder Festsetzen des AI-State bleibt ausgeschlossen.
4. Falls ein Fletcher mit demselben Stockpile-Ziel als Blocker verwendet wird, muss sein Weg vom Ziel und die anschließende Wiederaufnahme seines ursprünglichen Fetch-Zyklus als eigene mehrstufige Cleanup-Episode nachgewiesen werden. Eine direkte Rückteleportation nach registrierter Belegung ist nicht zulässig.
5. Nach einem echten `BUG_CONFIRMED` die vollständige Kette `ACCESS_RESELECTED` → `FIX_APPLIED` → `FIX_PROGRESS` → `FIX_VERIFIED` anhand von Log und sichtbarem Produktionszyklus abnehmen.

## Tests und Buildstand

- Das separate Konsolenprojekt `_inspect/StockpileAccessFixTestTests` prüft Native-Verträge, Layouts, alle acht Workerzustände, Kandidaten-Reset, Cooldowns, Blockerregeln und verbotene direkte Manipulationen.
- Letzter Stand: 1.123 Assertions bestanden.
- Letzter Modbuild: 0 Warnungen, 0 Fehler.
- Installierte und lokale DLL waren nach dem letzten Build bytegleich.
- Die Testversion bleibt absichtlich `0.1.0`, solange Reproduktion und Runtime-Nachweis noch nicht vollständig abgeschlossen sind.

