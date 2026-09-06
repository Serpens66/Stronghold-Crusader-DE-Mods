# AI Defense Test

## Erklärung für Spieler

AI Defense Test sorgt dafür, dass die Türme computergesteuerter Burgherren mit Fernkämpfern besetzt bleiben. Der Mod prüft alle lebenden KI-Türme regelmäßig. Befindet sich auf einem Turm bereits ein eigener Bogenschütze, Armbrustschütze oder ein anderer unterstützter Fernkämpfer, greift der Mod nicht ein. Ist der Turm leer, wird auf einem freien Turmfeld ein Bogenschütze für den Besitzer des Turms erzeugt.

Diese zusätzlich erzeugten Verteidiger bleiben normale, auswählbare und angreifbare Einheiten. Sie werden jedoch aus den üblichen Angriffs-, Patrouillen- und Belagerungsgruppen der KI herausgehalten. Dadurch kann die KI sie nicht für einen Angriff vom Turm abziehen. Bewegungen innerhalb des eigenen Turms bleiben erlaubt, damit ein Verteidiger seine Position für eine freie Schusslinie anpassen kann; Bewegungs- und Zielbefehle außerhalb des Turms werden blockiert.

Stirbt ein geschützter Verteidiger, wird beim nächsten Scan wieder geprüft, ob der Turm Ersatz benötigt. Wird ein Turm zerstört oder wechselt sein Zustand, gibt der Mod seine interne Zuordnung frei. Verlorene interne Gruppen werden automatisch neu erstellt. Im Karteneditor ist der Mod vollständig deaktiviert.

Der Mod beeinflusst Einheiten und damit das Spielgeschehen. Er verwendet deshalb `NetworkMode=1`; in einer Mehrspielerpartie muss bei allen Teilnehmern dieselbe Mod vorhanden sein.

## Direkte Erklärung des Codes

`AIDefenseTestPlugin` ist der BepInEx-Einstiegspunkt. `Awake()` erzeugt genau eine statisch verwurzelte `AIDefenseTestRuntime` und wartet auf `CrusaderLibrary.LibraryLoaded`. Erst nach erfolgreicher Prüfung der nativen Spielversion ruft der Plugin-Code `Apply()` auf. Das normale frühe `OnDestroy()` des SHCDE-Startvorgangs lässt die Runtime absichtlich bestehen; aufgeräumt wird nur beim echten Beenden über `OnApplicationQuit()`.

`AIDefenseTestRuntime.Apply()` registriert Map-, Tribe- und Unit-Events des Script Extenders sowie `GameTimeManagerAPI.OnTick`. `OnStartMap()` und `OnLoadSave()` starten die Verfolgung einer Karte, `OnUnloadMap()` beendet sie. Editor-Karten werden an allen wichtigen Grenzen erkannt und mit `DisableForMapEditor()` fail-closed abgeschaltet. Der erste Scan erfolgt nach 20 Ticks, weitere Scans folgen jeweils nach 250 Ticks.

`ScanDefenses()` liest die einbasierten Game-IDs aller lebenden Einheiten und Gebäude. Zuerst entsteht eine Belegungskarte aller Einheitentiles sowie eine Zuordnung vorhandener Fernkämpfer. Danach werden lebende Gebäude auf die unterstützten Turmtypen und einen gültigen KI-Besitzer gefiltert. `GetTowerTileIds()` liest bis zu 36 gültige belegte Tiles des Turms. `HasFriendlyRangedDefender()` verhindert einen Spawn, sobald bereits ein eigener unterstützter Fernkämpfer auf einem dieser Tiles steht.

`TrySpawnProtectedDefender()` wählt unter den freien Turmtiles das geeignete Spawnfeld und erzeugt dort mit `CreateUnitLocal()` einen Bogenschützen für den Besitzer und die Farbe des Turms. Die Runtime merkt sich Einheit und Turm sowohl über ihre aktuellen Game-IDs als auch über stabile Global-IDs. Damit erkennt sie wiederverwendete Slots und veraltete Zuordnungen, statt versehentlich eine andere Einheit oder Gruppe zu bearbeiten.

`TryEnsurePrivateTribe()` entfernt den Verteidiger gegebenenfalls aus einer unerwarteten Tribe-Gruppe, erstellt für ihn eine eigene neutrale Gruppe und setzt deren Haltung auf `Hold`. Die beiden Felder für das KI-Verhalten werden durch `EnsureProtectedAIBehaviour()` auf Related-Wert `0` und den vorzeichenbehafteten Sentinel `-1` gesetzt. Dadurch fällt die Einheit in keine der bekannten normalen KI-Verhaltenskategorien von 0 bis 22. Vor und nach `GameTribeManagerAPI.UnassignUnit()` wird die Mitgliedschaft validiert; der öffentliche, ab Script Extender 2.2.0 korrigierte Wrapper wird mit einbasierten IDs verwendet.

Die Pre-Events `OnTribeAssignUnit`, `OnTribeIssueOrderMoveHere`, `OnTribeIssueOrderWithTarget` und `OnUnitMoveHere` bilden die Schutzschicht gegen spätere KI-Befehle. Eine unerwartete Gruppenzuweisung sowie Ziel- oder Bewegungsbefehle außerhalb des Turms setzen `SkipOriginalFunction` und liefern einen neutralen Rückgabewert. `IsMovementTargetOnTower()` erlaubt dagegen lokale Bewegungen, deren Ziel zu den gespeicherten Turmtiles gehört.

`OnUnitDelete()` entfernt tote Verteidiger aus den Dictionaries. `OnTribeDelete()` verwirft eine gelöschte private Gruppe, sodass der nächste Scan sie ersetzt. `ReleaseDefendersOfMissingTowers()`, `ReleaseProtectedDefender()` und die Cleanup-Methoden lösen veraltete Mitgliedschaften, markieren noch vorhandene private Gruppen zur Löschung und entfernen alle internen Zuordnungen. Wiederholte Eingriffe und Fehler werden gedrosselt und mit Zeitstempel sowie zusammengefassten Diagnosezählern protokolliert.
