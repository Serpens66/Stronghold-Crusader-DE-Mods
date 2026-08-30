# Random Events – native Ereignisnotizen

## Referenz-DLL

- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Die installierte `CrusaderDE.dll` ist die kanonische Quelle. RVAs sind nur zusammen mit diesem Hash feste Referenzen.
- Bei anderen DLLs werden die Funktionen über eindeutige semantische AOBs gesucht und strukturell gegeneinander validiert. Schlägt dies fehl, bleibt nur das betroffene Ereignis deaktiviert.

## Hasenplage

- Der Mod wählt eine zufällige lebende Getreide- oder Hopfenfarm des Zielspielers und innerhalb von 12 Tiles genau einen Vanilla-kompatiblen Quellpunkt.
- Statt roher Einzel-Units ruft er den gemeinsamen nativen Wildtierhandler mit Aktion `222` auf. Dieser erzeugt wie das Originalereignis einen richtigen Hasenstamm mit 14 bis 21 Tieren und registriert dessen Quelle; dadurch greifen Vanillas Verteilung und Farmfraß. Am gewählten Quellpunkt wird außerdem Vanillas ActionPoint eingereiht, damit das anklickbare Ausrufungszeichen dorthin springt.
- Vorher wird Vanillas Limit von 160 Hasen geprüft. Der originale 1200-Tick-Zustand und die Quellkoordinaten im Tribe-Manager werden wie im Vanilla-Wrapper gesetzt; anschließend werden die originale Video- und Sprachnachricht eingereiht.
- Handler RVA `0x11E150`, Prädikat RVA `0x1177A0` und Spawner RVA `0x123AC0` gelten nur für die Referenz-DLL; Tile-Maske und Adressen werden aus semantisch validierten Codepfaden abgeleitet.

## Lebende Zielspieler

- `r_LordUnitId` dient nur als Verweis. Unmittelbar vor jeder Ereignisausführung muss die referenzierte Einheit auflösbar sein, `AliveState.IsAlive` besitzen, vom Typ `CHIMP_TYPE_LORD` sein und dem Zielspieler gehören.
- Fehlende PlayerResources, ein ungültiger Verweis, ein toter Lord oder eine falsche Einheit überspringen das Ereignis. Periodisches Lord-ID-Logging wurde entfernt.

## Löwenangriff

- Der Mod wählt aus Vanillas registrierten, lebenden Wegweisern denjenigen mit der geringsten Entfernung zur lebenden Burg des Zielspielers. Ist keine Burg nutzbar, dient der lebende Lord als Distanzanker.
- Auf dem nächstgelegenen Vanilla-kompatiblen Tile innerhalb von 12 Tiles um diesen Wegweiser wird der gemeinsame Wildtierhandler mit Aktion `221` einmal je Stärkepunkt aufgerufen.
- Für jeden erzeugten Stamm wird Vanillas ActionPoint-Handler mit dem Spawnpunkt aufgerufen; dadurch erscheint wieder das anklickbare Ausrufungszeichen und verwendet den originalen Kamera-Sprung. Danach erhält der Stamm denselben Aktivierungswert `0x10000`, den Vanillas Ereigniswrapper setzt. Die originale Sprachnachricht `Random_Events14.wav` wird ohne Video eingereiht, da die Installation kein Löwen-Ereignisvideo enthält.
- Stamm-Stride, Aktivierungsfeld, Tile-Maske und ActionPoint-Pfad werden über getrennte semantische Signaturen aufgelöst. Scheitert nach einem Update nur die ActionPoint-Auflösung, bleiben Löwenangriff und Nachricht aktiv und ausschließlich das Ausrufungszeichen wird mit einem Error deaktiviert.

## Wegweiser-Auswahl und Bogenschützen

- Bogenschützen, Banditen und Löwen verwenden dieselbe deterministische Auswahl: kleinste Entfernung zur lebenden Burg des Zielspielers, ersatzweise zum lebenden Lord, und bei gleicher Entfernung die kleinere Building-ID.
- Vanillas `FreeBuild_Event`-Case `148` erzeugt neben den Bogenschützen auch den Mönch, native Gruppen, Befehlszustände, Marschziele, ActionPoint und Präsentation. RandomEvents lässt diesen vollständigen Pfad deshalb bestehen.
- Der Case liest die tatsächlich verwendete 32-Bit-Quellposition aus dem zum ausgewählten Wegweiser-Slot gehörenden Datensatz. Auf der Referenz-DLL beginnt der relevante Pfad bei RVA `0x104E13`; X/Y liegen bei `SignpostSlots + 0x40/+0x44`, die Datensätze haben Stride `0x10`.
- Nur während `GameAction` wird der gewählte Wegweiser als Slot null freigegeben. Als Quellposition dient das deterministisch nächste freie, begehbare Tile außerhalb seines Footprints, dessen Pfadkomponente mit einem lebenden Gebäude oder dem Lord des Zielspielers verbunden ist. Dadurch erzeugt Case `148` Bogenschützen und Mönch nicht innerhalb des blockierenden Wegweisers. Slots und Koordinaten werden unmittelbar verifiziert und auch nach einer Ausnahme vollständig wiederhergestellt. Gibt es kein verbundenes freies Perimeter-Tile oder schlägt die semantische Strukturprüfung fehl, wird das Bogenschützen-Ereignis fail-closed übersprungen; Banditen und Löwen behalten ihre expliziten Spawnpfade.

## Erster Ereignistermin

- Der erste Termin bleibt Kartenstart plus das konfigurierte Monatsintervall. Ereigniswürfe werden jedoch erst vorbereitet, sobald mindestens ein aktiver menschlicher Spieler einen auflösbaren lebenden Lord hat.
- Damit kann die frühe Karteninitialisierung keinen leeren ersten Batch mehr erzeugen. Bei zwei Monaten Intervall wird der erste vorbereitete Batch nach zwei statt erst nach vier Monaten ausgeführt.

## Automatische Wegweiser

- Kandidaten werden auf eine freie, begehbare, ebene 2x2-Fläche innerhalb der Kartengrenzen und auf Erreichbarkeit für jeden teilnehmenden Ereignisspieler gefiltert. Bei aktivierter KI-Option umfasst diese Prüfung auch lebende KI-Spieler. Da Nature (`playerId=0`) kein gültiger Spieler für Vanillas playergebundene Platzierungsprüfung ist, verwendet der anschließende `CreatePrefab`-Aufruf ausschließlich für diese vollständig vorgeprüften neutralen Wegweiser den Placement-Bypass.
- Schlägt die Erzeugung oder native Registrierung trotzdem fehl, wird der nächste bevorzugte Kandidat, danach jeder weitere vorgefilterte Kandidat und schließlich eine andere Randtiefe versucht. Ein unvollständig erzeugter neutraler Wegweiser wird sicher entfernt.
- Findet sich kein Randkandidat, wählt der Zentrum-Fallback die freie, ebene und erreichbare Position mit dem größtmöglichen Keep-Abstand. Die normale 100-Tile-Regel wird dort nicht erzwungen, weil sie bei einem 50-Tile-Notfallradius und zentralen Keeps geometrisch unmöglich sein kann.

## Manuelle Banditen

- Teststand 1.0.35 speichert beim Start einer neuen Karte den absoluten Monat und berechnet bei jeder Banditen- oder Bogenschützen-Auslösung `floor(vergangene Monate * gewürfelter Faktor / 3)`. Die Faktoren werden in 0,1-Schritten aus den Lobby-Grenzen 0,1 bis 5,0 gewürfelt. Banditen werden auf höchstens fünf möglichst gleich große Gruppen verteilt; Keeps werden nicht als Bewegungsziel angeboten. Das Bogenschützen-Ereignis bleibt Vanilla und erhält die berechnete Gesamtzahl als Action-Stärke.
- Owner `0` ist für militärische Banditen ungeeignet: Nature folgt nicht der normalen Spieler-Selbstfreundlichkeit. Tribe-lose Streitkolbenkämpfer desselben Spawns und Nature-Tribes konnten sich trotz gemeinsamem Owner gegenseitig als Ziele behandeln; ein früher scheinbar friedlicher Lauf war nicht reproduzierbar. Eine erfundene Player-ID `9` ist ebenfalls unzulässig, weil Player-Ressourcen, Teams, Diplomatie und weitere native Tabellen fest auf die regulären Slots `1` bis `8` begrenzt sind.
- Seit SHCDE-SE 1.41 folgen `CreateUnitLocal` und `CreateUnitWorld` dem nativen Vertrag `playerOwnerId`, danach `playerColorId`. Für die installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` liegt die Spawnfunktion bei RVA `0x17FEF0`; sie schreibt den Owner nach `GameUnit+0x92` (`r_ControllableForPlayerId`) und die Farbe nach `GameUnit+0x0C` (`r_SpritePlayerColorId`). RandomEvents übergibt den reservierten Owner und die Nature-Farbe `0` mit benannten Argumenten und validiert beide Felder unmittelbar nach dem Spawn.
- Teststand 1.0.33 reserviert deshalb den ersten nicht aktiven regulären Slot mit `r_LordUnitId == 0`. Existiert kein solcher Slot, endet das Ereignis vor Spawn, Präsentation und Popularitätsmalus. Der reservierte Slot erhält eine Teamnummer, die kein berücksichtigter Ereignisteilnehmer verwendet; die Feindschaft zu allen teilnehmenden Menschen und – bei aktivierter Option – KIs wird vor dem Spawn und erneut vor der Gruppenaktivierung verifiziert. Nur die Sprite-Farbe bleibt Nature `0`.
- Die Streitkolbenkämpfer werden 20 Simulationsticks nach dem Spawn über den persistenten Tick in Tribes bis fünf Einheiten eingeteilt, auf `Aggressive` gesetzt und mit `IssueMoveHereCommand` zu einem freien, über dieselbe Pfadkomponente erreichbaren Randtile eines zufälligen lebenden Zielspieler-Gebäudes geschickt. Die tickbasierte Verzögerung ersetzt die frühere lokale Echtzeitverzögerung und bleibt dadurch im Multiplayer deterministisch. Arrayindizes werden dabei explizit in die einbasierten Script-Extender-Gebäude-IDs umgerechnet.
- Teststand 1.0.32 entfernt den vollständigen verzögerten `MoveToTile`-Versuch wieder. Nach `CreateUnitLocal(0, 0, ...)` werden die Banditen nur noch auf Info-Level gelesen; es gibt keine Tribe-, Haltungs-, Bewegungs-, Angriffs- oder sonstige Unit-Mutation. Präsentation, Minimap-ActionPoint und der Popularitätsmalus des menschlichen Zielspielers bleiben unabhängig davon aktiv.
- Teststand 1.0.31 plant pro tribe-loser Einheit einen individuellen `GameUnitManagerAPI.MoveToTile`-Befehl für 0,5 Sekunden nach dem nativen Spawn. Das Ziel ist ein freies, verbundenes Randtile eines deterministisch zufällig gewählten lebenden Gebäudes des menschlichen Zielspielers. Erhält eine Unit während ihrer Initialisierung wider Erwarten einen Tribe, wird ihr Move-Befehl ausgelassen; eine Aggressivhaltung wird nicht gesetzt, weil der Script Extender diese ausschließlich für Tribes anbietet.
- Teststand 1.0.30 erzeugt keinen Tribe und weist den über `CreateUnitLocal(0, 0, ...)` erzeugten Streitkolbenkämpfern keinen Tribe zu. Ein Lauf mit aktivem KI-Spieler bestätigte unmittelbar nach dem nativen Spawn für alle 28 Einheiten `r_TribeId=0`, `r_TribeLeaderUnitId=0` und `r_AIState=0`; die Info-Diagnose liest diese Felder weiterhin direkt aus jeder Banditen-Unit.
- Teststand 1.0.29 erzeugt pro Event genau einen Tribe mit Owner `0` und weist alle über `CreateUnitLocal(0, 0, ...)` erzeugten Streitkolbenkämpfer dieses Spawns diesem selben Tribe zu. Das ist der einzige Eingriff nach dem Spawn; Haltung, Bewegung, Angriff, Zielzuweisung, verzögerte Aktionen und spätere Überwachung bleiben vollständig entfernt.
- Der Mittelpunkt eines Wegweiser-Footprints besitzt nicht zwingend eine native Pfadkomponente. Der tatsächliche Banditen-Spawn wird deshalb auf das nächstgelegene freie, begehbare Randtile mit einer Pfadkomponente ungleich `0` gelegt; dieselbe Komponente begrenzt anschließend die erreichbaren Ziele.
- Der native Stammesbefehl-Dispatcher liegt in der Referenz-DLL bei RVA `0x11E910`. Der vom Script Extender als `ForceAttackBuilding` bezeichnete Befehl `36` validiert seinen Zielwert im Zweig um RVA `0x11F106` mit dem Einheiten-Stride `0x490`; eine Gebäude-ID wird deshalb ohne Fehler, aber auch ohne Einheitenbefehl verworfen.
- Befehl `9` ist zwar ein Gebäudeangriff, sein Einheiten-Switch verwirft Streitkolbenkämpfer bei normalen Gebäudetypen. Auch Befehl `5` (`Attack Here`) lieferte zwar einen erfolgreichen API-Rückgabewert, setzte bei neutralen Streitkolbenkämpfern aber nachweislich keinen Bewegungs- oder Angriffskontext. Der einzelne Unit-`MoveHere`-Handler bewegte sie ohne Laufanimation. RandomEvents verwendet deshalb den vollständigen Tribe-`IssueMoveHereCommand` und schickt jede Gruppe auf ein freies, verbundenes Tile direkt neben ihrem zufälligen Ziel.
- Vanillas FreeBuild-Case `146` setzt vor dem Spawn für den lokalen Spieler einen 16-Bit-Ereignisstatus auf `16` (semantisch aufgelöster Write bei RVA `0x104CBA`). Dieser Status erzeugt und beendet den zeitweiligen Popularitätsmalus über die normale Simulation. RandomEvents setzt das abgeleitete Feld stattdessen für die explizite menschliche Zielspieler-ID.
- Schlägt diese Prüfung nach Wiederholungen fehl, werden nur weitere Banditenereignisse für die laufende Karte deaktiviert. Die übrigen Zufallsereignisse und ihre Timer bleiben aktiv.

## Multiplayer und Zufallsfolgen

- Der Host allein zieht Chancen und Stärken aus dem gespeicherten privaten RandomEvents-PRNG. Er überträgt die vollständig aufgelöste Aktionliste, deren Reihenfolge und den anschließenden PRNG-Zustand über einen tick-synchronen Script-Extender-Chore an alle Teilnehmer.
- „Gemeinsame Ereignisse“ würfelt einmal je Ereignis und wendet einen Treffer mit derselben Stärke auf jeden lebenden menschlichen Spieler an. „Individuelle Würfe“ würfelt separat für jeden Menschen und kann deshalb unterschiedliche Ereignisse und Stärken erzeugen. Die resultierende globale Ausführungsreihenfolge ist in beiden Modi auf allen PCs identisch.
- Direkte native Handler laufen innerhalb desselben Chore-Callbacks auf jedem PC. Dadurch konsumieren sie Vanillas synchronisierten Zufallszahlengenerator überall in derselben Folge. Vanilla-`GameAction`-Ereignisse werden nur vom Host eingereiht, weil das Spiel daraus selbst weitere native Chores erzeugt.
- Automatische Wegweiser und verzögerte Banditenbefehle werden ebenfalls per Chore beziehungsweise Simulationstick synchronisiert. Lokale Echtzeit und ein pro PC frisch erzeugter Zufallsseed dürfen keinen Simulationszustand bestimmen.

## Direkte Vanilla-Handler

- `GameTimeManagerAPI.OnTick` wird aus einem nativen Pre-Tick-Kontext vor der Zeit-/Datumsverarbeitung aufgerufen. Timeline-Vektoren dort zu verändern führte reproduzierbar zu einem nativen Zugriffsfehler beim Kartenstart.
- Ein eigenes, früh erzeugtes `DontDestroyOnLoad`-Objekt erhielt keine `LateUpdate()`-Callbacks. Auch der Script-Extender-`UnityMainThreadDispatcher` akzeptierte die Coroutine, führte aber in RandomEvents und CastlePlanner nie deren ersten Callback aus. Der gemeinsame Dispatcher und der davon abhängige Timeline-Pfad wurden deshalb entfernt.
- Die fünf Aktionen werden ohne Timeline-Eintrag über die Handler aus dem Vanilla-Switch ausgelöst. Vor jedem Effekt läuft dieselbe Vanilla-Voraussetzung: Farmtyp `30`, `31`, `32` oder `33` muss für den Zielspieler existieren; Kornspeicherdiebstahl verlangt eine gesetzte erste Kornspeicher-ID.
- Getreidebefall: Voraussetzung RVA `0xB8D50`, Effekt RVA `0xC3130`. Hopfenkäfer: Voraussetzung RVA `0xB8D50`, Effekt RVA `0xC2E30`. Obstfäule: Voraussetzung RVA `0xB8D50`, Effekt RVA `0xC2C30`.
- Wahnsinnige Rinder führt nach RVA `0xB8D50` zuerst den Einheitenhandler RVA `0x194C40` und danach den Gebäudehandler RVA `0xC6090` aus. Kornspeicherdiebstahl nutzt RVA `0xC5F70` mit Spieler-ID und Prozentstärke.
- Nach erfolgreichem Effekt wird Vanillas Nachrichtenhandler RVA `0x1031B0` verwendet. Videos und Sprachdateien entsprechen den Timeline-Cases; bei wahnsinnigen Rindern bleiben Vanillas getrennte Audio- und Videoeinträge erhalten.
- Auf dem Referenz-Build werden zuerst die erwarteten RVAs semantisch validiert. Nach kleinen Spielupdates sucht ein gemeinsamer Resolver nur in ausführbaren PE-Sektionen nach eindeutigen Signaturen und leitet relative Ziele weiterhin aus dem Code ab.
- Handler werden unabhängig aufgelöst. Ein geänderter Effekthandler deaktiviert deshalb nur die davon abhängigen Ereignisse; ein Fehler der gemeinsamen Gebäudeprüfung oder Nachrichten-Queue betrifft weiterhin alle Ereignisse, die diese Vanilla-Komponente benötigen.
