# Virtuelle Einheiten und Gebäude für SHCDE

## Bestätigte Grenze zwischen Simulation und Präsentation

Virtuelle Units behalten einen echten Vanilla-Simulationstyp. Eine zusätzliche Zahl darf weder in `r_UnitChimp` noch in den festen Arrays `selectedChimpTypes`, `troop_counts` oder den Kontrollgruppen-Typarrays abgelegt werden. Die öffentliche Identität besteht aus `TypeId` und der bei jedem Zugriff validierten Kombination aus 1-basierter Game-ID und Global-ID.

Die bereits umgesetzte eigenständige Präsentation wird zentral durch `APIShared` an fünf verwalteten Stellen ergänzt: ausgewählte Truppentypen, ID-genaue Links-/Rechtsfilter, Einheiten-Hover, Armeereport und Kontrollgruppen. Eine Auswahländerung geht als validierte Liste 1-basierter IDs über `EngineInterface.TroopSelectionChanged(int[])` zurück an Vanilla. Kontrollgruppen werden aus den wirklichen, hash- und layoutvalidierten Vanilla-Gruppeneinträgen rekonstruiert; eine modseitige Schattenliste wird nicht mehr geschrieben. Alte PoC-Saves mit Schattenmetadaten bleiben lesbar, die veralteten Einträge werden ignoriert.

`APIShared` ist alleiniger Eigentümer der gemeinsamen Managed-Hooks auf `HUD_Troops.SetupSelectedTroops`, die Kategorie-Klickhandler, `HUD_ControlGroups.populate`, Kontrollgruppenaktionen und `MainViewModel.UpdateUITroopSprites`. Verbraucher registrieren unveränderliche Kategorien und Bildresolver über ihre Owner-GUID. Konkurrierende Matcher für dieselbe konkrete Unit fallen auf Vanilla zurück. Lord und Desert Archer durchlaufen dadurch dieselbe deterministische Hook-Kette, ohne synthetische `eChimps`-Werte oder Änderungen an festen nativen Arrays.

Der separate Bildersetzungsvertrag startet nach jedem vollständigen Vanilla-Aufruf von `UpdateUITroopSprites(colour, arabic)` neu. Resolver laufen nach Priorität, Owner-GUID und Override-ID und dürfen zunächst ausschließlich `UIBuildingsO011`, `UIBuildingsO012`, `UIButtonsK007` und `UIButtonsK008` ersetzen. `null` und Resolverfehler behalten das bisherige Bild; ein späterer Vanilla-Aufruf entfernt deaktivierte Overrides automatisch.

Als nächste Präsentationsebene ergänzt `APIShared` generische Rekrutierungsvarianten und die Einzelunit-Detailanzeige. Damit unterscheiden sich virtuelle Unit-Varianten künftig in Weltgrafik, Rekrutierung, Auswahl-HUD, Kontrollgruppen, Hover, Armeereport und Detailanzeige, ohne den zugrunde liegenden Vanilla-Simulationstyp zu verändern. Normale und virtuelle Units werden in den jeweiligen sichtbaren Kategorien strikt getrennt; Vanillas Gesamttruppenzahl bleibt unverändert.

Das Diagnose-HUD behandelt Noesis-Eingaben getrennt von Weltklicks. Ein Weltklick wird erst im folgenden Unity-Frame ausgewertet, nachdem die ausdrücklich benannten interaktiven Flächen (`VirtualUnitsPrototypeHudToggle` und `VirtualUnitsPrototypeHudPanel`) ihre `PreviewMouseDown`-Route ausführen konnten. Der äußere HUD-Host darf nicht als Eingabefläche verwendet werden, weil sein Layout-Slot die Weltkarte überdecken kann. Zusätzlich müssen das eigentliche Karten-HUD aktiv, Blackout und Briefing geschlossen, Karte und Tile gültig sowie Vanillas `overGUI`-Prüfung frei sein. Dadurch kann ein physischer Klick höchstens einen Spawnauftrag erzeugen und ein Klick auf eine VUP-Fläche keinen.

## 1. Ziel und Ergebnis

Diese Datei ist die entscheidungsvollständige Implementierungsspezifikation für den eigenständigen BepInEx-Mod `VirtualUnitsPrototype`. SHCDE simuliert weiterhin ausschließlich bekannte Vanilla-Einheiten und -Gebäude. Der Mod ordnet konkreten Instanzen zusätzliche virtuelle Typen zu und verändert Darstellung sowie ausgewählte Werte über belegte Script-Extender- und Managed-Verträge.

Der erste spielbare Proof of Concept umfasst:

- eine öffentliche C#-API zur Registrierung virtueller Typen;
- eine gemeinsame Instanzregistry für Units und Gebäude;
- den virtuellen `Desert Archer` und ein virtuelles Test-Hovel;
- ein kleines, langes und flaches Spawnmenü unten links;
- freie Platzierung per anschließendem Kartenklick;
- persistente Zuordnungen im Spielstand;
- strikt gesperrte Multiplayer-Zuweisungen;
- ausschließlich vorhandene Vanilla-Grafiken, die im PoC pro Instanz leicht eingefärbt werden.

Der kanonische Script Extender 2.5.0 bleibt unverändert. Der nachweislich rückwärtskompatible Mod behält 2.3.0 als Mindestversion, solange keine neuere API verwendet wird. Dieses Dokument beschreibt den inzwischen als Testversion `0.1.0` umgesetzten Prototyp und bleibt die maßgebliche Spezifikation für seine Ingame-Verifikation und Weiterentwicklung.

## 1.1 Übergabe an einen neuen Chat oder Implementierer

### Aktueller Stand

- Projekt, öffentliche API, Runtime, verwaltete Visual-Hooks, XAML-HUD, Saveformat, Tests, `info.json` und `build.bat` sind als Testversion `0.1.0` umgesetzt.
- Die automatisierten Checks decken Registry, Werte, Saveformat, Simulations-Queue, Threadgrenzen und Visual-Fallbacks ab.
- Build und Installation erfolgen ausschließlich über die mod-eigene `build.bat`, die das installierte Paket bytegenau gegen das lokale Paket prüft.
- Platzierung, Vanilla-basierte Sichtbarkeit, Unit-/Building-Tint und die zentrale `APIShared`-Präsentation wurden im Spiel grundsätzlich bestätigt. Die generische Archer-Rekrutierungsvariante und Einzelunit-Detailfläche aus Abschnitt 4.7 sind umgesetzt und warten auf die Spielabnahme.

### Verbindlicher Arbeitsauftrag

Ein neuer Chat soll weder die Architektur neu entwerfen noch den bestehenden PoC erneut implementieren. Vor weiteren Runtime-Änderungen sind Abschnitt 4.5 und der für die konkrete Änderung relevante Teil des Vanilla-Audits erneut gegen die installierten Binärdateien abzugleichen und als kurze Verifikationsnotiz im Implementierungsfortschritt festzuhalten. Danach soll der Implementierer:

1. die einschlägige `AGENTS.md` vollständig lesen;
2. den aktuellen Inhalt dieses Ordners und passende vorhandene Workspace-Mods inventarisieren;
3. die installierte Script-Extender-Version und alle tatsächlich referenzierten Assemblies prüfen;
4. den bestehenden PoC und die zentrale `APIShared`-Präsentation als Ausgangspunkt erhalten;
5. nur bestätigte Verträge implementieren und alle anderen Teile fail-closed lassen;
6. die nächste Etappe in der Reihenfolge API-Vertrag, generischer Variantenwähler, VUP-Rekrutierungskorrelation, Detailanzeige und Tests umsetzen;
7. vor dem ersten Build sämtliche statischen Prüfungen und CRLF-Kontrollen abschließen;
8. anschließend genau einmal die mod-eigene `build.bat` nach den Workspace-Regeln ausführen;
9. klar zwischen automatisiert bestanden, kompiliert und tatsächlich im Spiel verifiziert unterscheiden.

### Entscheidungsprotokoll

Folgende Produktentscheidungen sind bereits getroffen und dürfen nicht ohne Rückfrage geändert werden:

- öffentliche C#-API statt eines rein internen PoC;
- Units und Gebäude gehören beide zum ersten spielbaren Prototyp;
- eigener kleiner HUD-Button unten links;
- langes, flaches Panel, das nach einer Auswahl geöffnet bleibt;
- Auswahl eines Typs und anschließende freie Platzierung per Kartenklick;
- wiederholte Platzierung bleibt aktiv, bis Rechtsklick oder Escape abbricht;
- Diagnose-Spawns sind kostenlos, Vanilla-Platzierungsregeln für Gebäude bleiben aktiv;
- erster Build bleibt im Multiplayer vollständig gesperrt, verwendet aber `NetworkMode=1`;
- keine Änderung am Script Extender 2.5.0;
- keine README- oder Versionsänderung während der Testphase.

### Stop- und Rückfrageregeln

Die Implementierung muss anhalten und den Benutzer mit konkreter Evidenz fragen, wenn:

- Plan, kanonischer 2.5.0-Quellcode und installierte `SHCDESE.dll` einander widersprechen;
- die installierte Zielversion nicht Script Extender 2.5.0 ist;
- ein benötigter fachlicher Enumwert im Quellcode und in der referenzierten Assembly nicht übereinstimmt;
- für dieselbe Managed-Methode mehrere plausible Signaturen oder Hookziele existieren;
- ein sicherer Building-ID-, Grafik- oder Refreshvertrag nicht belegt werden kann;
- vorhandene Benutzeränderungen mit den geplanten Dateien kollidieren;
- die Umsetzung eine Änderung am Script Extender, einen nativen Hook oder eine neue Abhängigkeit benötigen würde;
- eine echte Produktentscheidung nötig wird, die im Entscheidungsprotokoll nicht festgelegt ist.

Ein fehlgeschlagener optionaler Grafikvertrag darf nicht durch geratene Offsets oder Adressen ersetzt werden. Der betreffende Visualpfad wird deaktiviert, während bereits sichere Registry- und Wertefunktionen weiterarbeiten dürfen.

## 2. Festgelegte Architektur

### 2.1 Vanilla-Simulation und virtuelle Identität

Eine virtuelle Entität verwendet immer einen kompatiblen Vanilla-Basistyp. Vanilla bleibt für Erzeugung, Bewegung, Wegfindung, Kollision, Auswahl, grundlegenden Kampf, Arbeit, Belegung und seine eigenen Speicherdaten verantwortlich. Neue Mechaniken werden später als ausdrücklich registrierte Verhaltensmodule ergänzt.

Zwei Entitäten desselben Vanilla-Typs dürfen unterschiedliche virtuelle Identitäten besitzen. Registry-Schlüssel sind:

`(VirtualEntityKind, gameId, globalId)`

Die Game-ID ist immer 1-basiert. Die Global-ID verhindert, dass ein nach Löschung wiederverwendeter nativer Arrayplatz die alte virtuelle Identität übernimmt.

### 2.2 Darstellung

2D-Sprites bleiben die Hauptdarstellung, weil sie zu SHCDEs Sortierung, Teamfarben, Verdeckung, Object-Pooling und hoher Unit-Anzahl passen. Grafiken dürfen aus Vanilla-GMs, vollständigen Mod-Atlanten oder später offline aus 3D-Modellen gebackenen Atlanten stammen. Direkt im Spiel gerenderte Skinned Meshes und Runtime-FBX-Import sind kein Bestandteil der Architektur.

## 3. Projekt- und Laufzeitstruktur

- Eigenständiger BepInEx-Mod `VirtualUnitsPrototype` für `.NET Framework 4.8.1`.
- Harte Abhängigkeiten ausschließlich von BepInEx, installierten Unity-/Spiel-Assemblies, `APIShared` und Script Extender. Geprüfte Zielversion ist 2.5.0; die deklarierte Script-Extender-Mindestversion bleibt 2.3.0. Die neuen Rekrutierungsverträge erfordern `APIShared` mindestens 0.3.2.
- Standardreferenz auf die installierte `BepInEx/plugins/000shcdese/SHCDESE.dll`; ein alternatives `ExtenderDir` ist nur als expliziter Buildparameter zulässig.
- Keine automatische Bevorzugung lokaler `bin`-, `mod_output`- oder Extender-Buildartefakte.
- Keine Abhängigkeit von CastlePlanner oder anderen Workspace-Mods; CastlePlanner dient nur als HUD-Referenz.
- `info.json` verwendet `NetworkMode=1`.
- Initialisierung erst nach `CrusaderLibrary.Instance.LibraryLoaded`.
- Alle Logs enthalten Datum, Uhrzeit und Millisekunden.
- Während PoC und Debugging keine Versions- oder README-Änderung.

Interne Subsysteme:

- `DefinitionRegistry`
- `InstanceRegistry`
- `VirtualEntityApi`
- `UnitVisualRuntime`
- `BuildingVisualRuntime`
- `StatOverrideRuntime`
- `VirtualSpawnRuntime`
- `VirtualSpawnHud`
- `VirtualSaveRuntime`
- `VirtualNetworkGuard`

## 4. Öffentliche C#-API

Der öffentliche Namespace lautet `VirtualUnitsPrototype.API`. Andere Mods referenzieren zunächst direkt `VirtualUnitsPrototype.dll` und deklarieren eine harte BepInEx-Abhängigkeit. Eine getrennte Contract-DLL gehört nicht zum PoC.

### 4.1 Typen und Definitionen

Typ-IDs verwenden `<Mod-GUID>:<lokaler-name>`, etwa `serp.virtual-units:desert-archer`. Units und Gebäude teilen denselben ID-Namensraum.

Öffentliche Grundtypen:

- `VirtualEntityKind` mit `Unit` und `Building`;
- `VirtualEntityKey` mit Art, 1-basierter Game-ID und Global-ID;
- `VirtualEntityInstance` mit Schlüssel, Typ-ID, Definitionsversion und Basiswerten;
- `VirtualUnitDefinition` und `VirtualBuildingDefinition`;
- gemeinsames unveränderliches `VirtualSpriteTintProfile` mit RGBA-Bytes;
- `VirtualStatProfile` und `VirtualSpawnOptions`;
- `VirtualApiResult` mit Ergebniscode und lesbarer Fehlermeldung.
- unveränderliche `VirtualOperationTicket`- und `VirtualOperationCompletedEventArgs`-Typen zur Korrelation asynchroner Mutationen.

`VirtualUnitDefinition` enthält Typ-ID, positive Definitionsversion, Anzeigename, `eChimps`-Basistyp, Spriteprofil, rationale Gesundheits- und Geschwindigkeitsfaktoren sowie Diagnosemenü-Sichtbarkeit.

`VirtualBuildingDefinition` enthält Typ-ID, positive Definitionsversion, Anzeigename, `eStructs`-Basistyp, zugehörigen `eMappers`, den über `BuildingScales` ermittelten Scale, Tile-Visualprofil, rationalen Gesundheitsfaktor, Diagnosemenü-Sichtbarkeit und PoC-Spawnfreigabe. Mapper und Struct werden über die Extender-Zuordnung gegengeprüft.

Alle fachlichen Werte referenzieren unmittelbar die benannten Enums und Konstanten des Script Extenders 2.5.0. Eigene numerische Kopien von Unit-, Building-, Mapper-, Goods-, Chore- oder GM-Werten sind verboten.

### 4.2 API-Operationen

`VirtualEntityApi` bietet statische, intern synchronisierte Methoden:

- `RegisterUnitDefinition`, `RegisterBuildingDefinition`;
- `TryGetUnitDefinition`, `TryGetBuildingDefinition`;
- `TryAssignUnit`, `TryAssignBuilding`;
- `TryRemoveUnitAssignment`, `TryRemoveBuildingAssignment`;
- `TryGetUnitInstance`, `TryGetBuildingInstance`;
- `SpawnVirtualUnit`, `SpawnVirtualBuilding`.
- `QueueVirtualUnitSpawn`, `QueueVirtualBuildingSpawn`, `QueueAssignUnit`, `QueueAssignBuilding`, `QueueRemoveUnitAssignment`, `QueueRemoveBuildingAssignment`.

Mutierende API-Aufrufe legen ausschließlich einen Auftrag an und liefern zunächst `InitializationPending`; `OperationCompleted` liefert auf dem Unity-Thread Ticket, Ergebnis und gegebenenfalls die fertige Instanz. Die bisherigen Mutatornamen bleiben als korrelationslose Queue-Wrapper erhalten. Ereignisse melden außerdem erfolgreiche Zuweisung, Entfernung und abgelehnte Wiederherstellung. Definitionen dürfen nur während der Initialisierung registriert werden; danach wird die Registry versiegelt. Leere oder doppelte IDs, unbekannte Enumwerte, unzulässige Faktoren, falsche Basistypen und widersprüchliche Mapper-/Struct-Paare werden mit `VirtualApiResult` abgelehnt und protokolliert.

### 4.3 Bestätigte Signaturen und Gate für Erweiterungen

Die vorhandene öffentliche API beruht auf den geprüften konkreten Typen der Extender-Methoden. Vor Erweiterungen sind diese Verträge gegen die tatsächlich referenzierte Assembly abzugleichen und weiterhin durch API-Tests zu sichern. Insbesondere müssen Rückgabetypen, Wertebereiche und Null-/Fehlersemantik der folgenden Verträge berücksichtigt werden:

- Unit-Erzeugung liefert `Int64`, die bestätigte positive Game-ID wird vor Registrynutzung auf den zulässigen `int`-Bereich geprüft.
- Building-Erzeugung über `CreatePrefab` liefert einen Ergebniswert, aber nicht verlässlich die gewünschte Building-ID; die ID stammt aus dem korrelierten Building-Spawn-Postereignis.
- Unit-`SetMaxHealth` und Unit-`SetCurrentHealth` verwenden `Int32`; `SetSpeed` verwendet `UInt16`.
- Building-`SetMaxHealth` verwendet `UInt16`, Building-`SetCurrentHealth` dagegen `Int16`. Der effektive Building-Maximalwert wird deshalb auf `Int16.MaxValue` begrenzt.
- API-Abfragen dürfen keine rohen Pointer in der öffentlichen Modoberfläche exponieren.
- Öffentliche Ereignisse dürfen unveränderliche Snapshots liefern und nicht die interne Registry mutierbar machen.

### 4.4 Fehlercodes der öffentlichen API

`VirtualApiResult` benötigt mindestens unterscheidbare Codes für:

- `Success`
- `NotInitialized`
- `DefinitionsSealed`
- `InvalidDefinition`
- `DuplicateTypeId`
- `UnknownTypeId`
- `InvalidGameId`
- `EntityNotFound`
- `GlobalIdMismatch`
- `BaseTypeMismatch`
- `UnsupportedGameMode`
- `InvalidPlacement`
- `InitializationPending`
- `SpawnFailed`
- `VisualFeatureUnavailable`
- `SaveDefinitionIncompatible`
- `InternalError`

Normale Validierungsfehler dürfen nicht als Exceptions aus der öffentlichen API austreten. Unerwartete interne Fehler werden abgefangen, zeitgestempelt protokolliert und als `InternalError` gemeldet, ohne einen halbfertigen Registryeintrag zu hinterlassen.

### 4.5 Verifikationsmatrix vor weiteren Runtime-Änderungen

#### Im kanonischen 2.5.0-Quellbaum belegt und auf Mindestversion 2.3.0 geprüft

Die folgenden Punkte wurden bei Erstellung dieses Dokuments im lokalen Fork `shcde-script-extender` geprüft. Sie sind vor dem Build zusätzlich gegen die tatsächlich referenzierte installierte Assembly zu bestätigen:

- `GameUnitManagerAPI.CreateUnitLocal(...)` existiert und liefert `Int64`.
- Unit-APIs für maximale Gesundheit und Geschwindigkeit sind vorhanden; `SetSpeed` erwartet `UInt16`.
- `GameBuildingManagerAPI.CreatePrefab(...)` existiert, verwendet `eMappers` und liefert `Int64`.
- Building-APIs für aktuelle und maximale Gesundheit sind vorhanden; `SetMaxHealth` erwartet `UInt16`.
- `BuildingScales.GetScale(eMappers)` ist öffentlich und liefert bei unbekanntem Mapper `-1`.
- `GamePlayerManagerAPI.GetLocalPlayerId()` ist vorhanden.
- `UnitR3EventHooks.OnUnitUnityVisualSpawn`, `OnUnitUnityVisualInterpolate` und `OnUnitUnityVisualRemove` sind vorhanden.
- `GameTimeManagerAPI.OnTick` läuft im nativen Simulationspfad und ist der einzige Ausführungskontext für modseitige native Mutationen.
- `BuildingR3EventHooks.OnBuildingSpawn` und `OnBuildingDelete` sind vorhanden.
- `OnBuildingSpawn` wird in Pre- und Postphase ausgelöst; das Postereignis übernimmt den nativen Rückgabewert in `ReturnValue`.
- `ModSaveDataAPI.RegisterModDataHandler(...)` ist vorhanden.
- `GameTileManagerAPI.GetTileId(...)` und die öffentliche Abfrage `GetTileBuildingId(...)` sind im Extender verfügbar.
- `eMappers.MAPPER_HOVEL` wird durch den Extender `eStructs.STRUCT_HOVEL` zugeordnet.
- `BuildingScales` weist `MAPPER_HOVEL` einen gültigen Scale zu.

Maßgebliche lokale Quellen sind insbesondere:

- `shcde-script-extender/src/SHCDESE.BepInEx/API/GameUnitManagerAPI.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/API/GameBuildingManagerAPI.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/API/BuildingScales.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/API/ModSaveDataAPI.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/EventAPI/UnitR3EventHooks.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/EventAPI/BuildingR3EventHooks.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/Interop/Enums.cs`
- `shcde-script-extender/src/SHCDESE.BepInEx/Interop/EStructs.cs`

#### Vor Implementierung statisch erneut zu prüfen

Diese Punkte sind verpflichtende Gates und dürfen nicht aus diesem Dokument allein übernommen werden:

- Die installierte `SHCDESE.dll` ist tatsächlich Version 2.5.0 und enthält dieselben Methodensignaturen und Enumzuordnungen wie der kanonische Fork.
- Die installierte Spiel-Assembly enthält `SpriteMapping.setGenericBuildingTileGraphic(GameMapTile,int,int,int)` genau einmal und mit hookbarer statischer Signatur.
- `GameMapTile.gameMapX`, `gameMapY`, `tileImage` und `light` haben in der installierten Managed-Assembly die erwarteten zugänglichen Typen.
- `spriteLoader.instance`, GM-Normal-/Alt-Arrays, Materialarrays und die benötigten Getter sind in der tatsächlich referenzierten Assembly erreichbar.
- `SpriteMapping.SetBodySprite(SpriteRenderer,int,int,int,bool,int,int)` ist genau einmal vorhanden und kann verwaltet detourt werden. Das Extender-Event bindet unmittelbar davor den Haupt-Renderer an die Unit-ID; der Detour erhält GM, Frame und Alt-Status ohne Sprite-Namensheuristik.
- Alle verwendeten Enumwerte, besonders `CHIMP_TYPE_ARCHER`, `STRUCT_HOVEL` und `MAPPER_HOVEL`, stimmen zwischen Quellcode und Assembly überein.
- Die Callbacksignaturen und Zeitpunkte von `ModSaveDataAPI` passen zum vorgesehenen Pending-Restore.
- Die für Map-Start und Map-Unload gewählten Events laufen in der benötigten Reihenfolge.
- `Shared/GameModeHelper.cs` kann als Quelllink ohne zusätzliche Modabhängigkeit eingebunden werden und seine benötigten Referenzen sind im neuen Projekt vollständig vorhanden.
- Das XAML-Patchziel und alle Bindingnamen kollidieren weder mit Vanilla noch mit gemeinsam installierten Workspace-Mods.
- `MainControls.getMouseMapTilePosition` liefert die kamerarotierte interne Tilebasis. Wie Vanilla muss der Mod zuerst `GameMap.instance.getMapTile` aufrufen und danach `gameMapX/gameMapY` als lokale Koordinaten an `CreateUnitLocal` beziehungsweise `CreatePrefab` übergeben; eine zusätzliche Achtfachskalierung darf modseitig nicht erfolgen.

#### Im Spiel diagnostisch zu verifizieren

Diese Verträge lassen sich nicht allein durch Kompilierung hinreichend belegen. Dafür wird zunächst zeitgestempeltes Diagnose-Logging eingebaut, anschließend wird der jeweilige Pfad erst nach erfolgreichem Test als bestätigt markiert:

- `CreateUnitLocal` liefert unter realen Bedingungen die 1-basierte ID der erzeugten Unit und die Global-ID ist unmittelbar lesbar.
- Der Pending-Kontext korreliert ein einfaches Hovel eindeutig mit genau einem passenden Building-Spawn-Postereignis.
- Der Building-Spawn-Rückgabewert ist beim Hovel die 1-basierte Building-ID und stimmt mit dem gefundenen Struct/Global-ID-Paar überein.
- Die Registrierung geschieht früh genug, dass der erste Building-Renderpass den virtuellen Typ sieht; andernfalls funktioniert der Cache-/Refreshpfad.
- `GameTileManagerAPI.GetTileBuildingId(tileId)` liefert während des Managed-Grafikaufrufs die erwartete 1-basierte Building-ID.
- Frameindizes verschiedener Building-GMs sind nicht semantisch oder geometrisch austauschbar; der beobachtete Hovel-Quellframe und der gleichnummerige Ziel-Frame besitzen unterschiedliche Abmessungen und Pivots.
- Frameindizes von Archer und Arab Bow sind wegen ihrer unterschiedlichen nativen Zustands- und Framebereiche nicht semantisch kompatibel. Der stabile PoC behält daher alle Vanilla-Archer-Frames bei.
- Zielmaterial, Teamfarbe, Transparenz und Fußabschneidung bleiben bei Unit-Normal- und Alt-Frames korrekt.
- Tilemap-Refresh aktualisiert nur Darstellung und verändert weder Nachbartiles noch logische Grids.
- HUD-Klicks werden zuverlässig von Kartenklicks getrennt und das Panel bleibt wie beschlossen geöffnet.
- Save/Load ruft Restore nicht zu früh auf und wendet Faktoren genau einmal an.

#### Noch nicht belegt und deshalb ausdrücklich außerhalb des PoC

- sichere Zuordnung von `GameMap.addUpdateBuildingAnim._objectID` zu Building-ID;
- visuelle Overrides komplexer oder aus mehreren Kernobjekten bestehender Gebäude;
- Chore-ID, Payload und Desync-Vertrag für Multiplayer;
- benutzerdefinierte partielle Atlanten trotz Work Item 162;
- Runtime-Import oder Runtime-Baking von glTF, GLB oder FBX.

Diese Punkte dürfen während des PoC nur diagnostisch untersucht werden. Sie sind kein Grund, Script Extender oder native Spielbibliothek zu verändern.

### 4.6 Abgeschlossener Vanilla-Audit für Unit-Präsentation und Rekrutierung

Der featurebezogene Audit wurde gegen die kanonisch installierte `CrusaderDE.dll` mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` durchgeführt. Der Hash stimmt exakt mit `CURRENT.json` und den verwendeten semantischen Datensätzen überein. Die geprüfte Managed-Baseline verwendet `Assembly-CSharp` mit SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`. Ihre Verknüpfungen wurden gegen Script Extender 2.5.0, Tag `v2.5.0`, Commit `5f02af6d074af7c741ebdaaccb48add39eba1bf4`, validiert.

Bestätigter Daten- und Kontrollfluss:

- `DLL_GameAction` bei RVA `0x81870` ist ein bestätigter Export mit der verwalteten Signatur `Int32 DLL_GameAction(Int32 action, Int32 structureID, Int32 value, Int32 value2)`. `MakeTroop` verwendet den Aktionswert `0x3EF`; `structureID` enthält in diesem Pfad tatsächlich die angeforderte Menge und `value` den Vanilla-Unit-Typ.
- Für europäische Units führt der menschliche Auftrag über `FUN_1800D5B40` bei RVA `0xD5B40` und die Vorprüfung `FUN_1800D69A0` bei RVA `0xD69A0` zur Chore 31. Deren Handler bei RVA `0x127A0` ruft `FUN_1800D5250` bei RVA `0xD5250` auf, die jede tatsächlich mögliche Unit einzeln über `FUN_180190CA0` bei RVA `0x190CA0` erzeugt. Die Funktionsnamen ohne Export sind `candidate`; Kontrollfluss, Tabellenzugriffe und Caller-/Callee-Kette sind für diesen Build strukturell belegt.
- `FUN_180190CA0` liest Goldpreis und bis zu vier Güteranforderungen aus festen, nach Vanilla-Typ indizierten Tabellen. Die Funktion prüft Gold, Güter und einen verfügbaren Peasant, wandelt dessen Slot um, zieht Ressourcen ab, aktualisiert Zähler und meldet die konkrete 1-basierte Unit-ID an den nachgelagerten Unit-Übergang.
- Die KI verwendet dieselbe Erzeugungs- und Kostenfunktion direkt. Geprüfte Rollenpfade sind Bodyguard bei RVA `0x40230`, Wirtschaftsschutz bei RVA `0x40430` sowie Verteidiger-, Harasser- und weitere Planrollen bei RVA `0x40740`. Die KI wählt ausschließlich Vanilla-Typen aus ihren festen Planfeldern und kennt keine virtuelle Unterkategorie.
- `UnitR3EventHooks.OnUnitTransition` des Script Extenders liefert am tatsächlichen Übergang die 1-basierte Unit-ID, Besitzer, Zieltyp und Quelle. Für europäische Kasernen ist die Quelle `EuropeanBarracks`. Eine Variantenkorrelation darf dieses Ereignis beobachten, aber `NextUnitType` nicht auf einen erfundenen Typ ändern.
- `DLL_TroopSelectionChanged` bei RVA `0x87B20` ist ein bestätigter Export. `FUN_18019A7E0` bei RVA `0x19A7E0` validiert jede übergebene 1-basierte ID anhand von Zustand, Besitzer und Selektierbarkeit und erzeugt anschließend Vanillas Auswahl-Chore `0x6C`. `APIShared` darf deshalb ausschließlich konkrete validierte IDs zurückgeben.
- Vanillas ausgewählte Typen werden in einem festen Array mit 89 Einträgen gezählt. `HUD_Troops.SetupSelectedTroops` verteilt bekannte Typen auf Seiten mit jeweils acht sichtbaren Slots. Eine virtuelle Kategorie darf weder das Array verlängern noch einen synthetischen Typ hineinschreiben.
- Der Kontrollgruppenspeicher beginnt beim geprüften Hash an RVA `0x36D78D0` und enthält zehn Gruppen mit je 10.000 Paaren aus 1-basierter Unit-ID und Global-ID. `FUN_180186300` bei RVA `0x186300` verwirft Global-ID-Abweichungen und erzeugt pro Gruppe vier nach Vanilla-Typ zusammengefasste sichtbare Kategorien plus Restzahl. Hinzufügen beziehungsweise Übertragen läuft unter anderem über RVA `0xCAE40`, Auswahl und Bereinigung über RVA `0x1915C0`, Laden über RVA `0xD4B60` beziehungsweise `0xD4C00`. Der Zugriff bleibt hash-, Pattern- und Layout-gebunden sowie außerhalb von Vanilla rein lesend.
- Der Armeereport erhält ein festes `troop_counts[34]`; `MainViewModel.UpdateSHTroopsData` kopiert es nach `AllTroops[1..34]`. Eine virtuelle Kategorie wird aus lebenden, identitätsvalidierten Instanzen errechnet, vom sichtbaren Basistyp abgezogen und in einem separaten gemeinsamen Host dargestellt. Native Zähler und Gesamttruppenzahl bleiben unverändert.
- `MainViewModel.UpdateUITroopSprites(int colour, bool arabic)` stellt bei jedem Farb- oder Kulturwechsel zunächst Vanillas Bilder wieder her. Der zentrale `APIShared`-Hook ruft Vanilla genau einmal auf und wendet danach geordnete Resolver an. Dadurch können deaktivierte oder fehlgeschlagene Overrides automatisch auf Vanilla zurückfallen.

Verbleibende Grenzen:

- Die aktuelle Chore 31 serialisiert zwei 16-Bit-Werte, während ein älterer Extender-Kommentar zwei `INT32`-Felder beschreibt. Der Mod baut deshalb keine eigene Chore-Payload nach und verwendet ausschließlich `EngineInterface.GameAction` sowie das öffentliche Transition-Ereignis.
- Ein nur nachträglich verrechneter eigener Goldpreis ist einfacher als eigene Güterkosten, wird aber von der KI ebenso wenig bei ihrer Kaufentscheidung berücksichtigt. Vollständig korrekte eigene Kosten benötigen Vorprüfungen und konsistente Abzüge vor allen menschlichen und KI-Rekrutierungswegen.
- Gleich teure KI-Varianten sind später möglich, wenn eine eigene deterministische Regel festlegt, welche von der KI erzeugten Vanilla-Units als Variante markiert werden. Diese Regel ist nicht Teil der ersten HUD-/Rekrutierungsetappe.

### 4.7 Nächste API-Etappe: generische Rekrutierungsvarianten

`APIShared` erweitert die vorhandene Präsentations-Capability um die Flächen Rekrutierung und Einzelunit-Details. Eine registrierte Variante definiert weiterhin Owner-GUID, stabile Category-ID, Vanilla-Basistyp, deterministische Position, Matcher und Icon-Tint. Zusätzlich liefert sie sprachabhängige Resolver für Anzeigename und Beschreibung sowie feste Fallbacktexte. Resolverfehler lassen den letzten sicheren beziehungsweise den definierten Fallbacktext bestehen.

Bis eigene vollständige Grafiken vorliegen, verwendet jede Fläche das zu ihr und zum Basistyp gehörende Vanilla-Symbol. Kaserne, Auswahl-HUD sowie Kontrollgruppen beziehungsweise Reports verwenden deshalb ihre jeweiligen `O`-, `K`- und Summary-Sprites und nicht denselben Sprite auf inkompatiblen Flächen. Darüber liegt ein nicht interaktiver RGB-Tint, dessen `UnitHudTint.Alpha` unmittelbar die Overlay-Deckkraft bestimmt; reines Weiß bedeutet unverändertes Vanilla. Der Desert Archer verwendet im HUD `RGB(64,128,255)` mit Alpha `115`, also rund 45 Prozent. Es wird kein zusätzliches Abzeichen eingeblendet. In der Einzelunit-Detailanzeige werden nur Typname, Bild und Beschreibung ersetzt; Lebensbalken, tatsächliche Werte, Besitzer, Befehle und übrige Vanilla-Daten bleiben erhalten.

Pro Vanilla-Basistyp verwaltet `APIShared` eine deterministisch nach Priorität, Owner-GUID und Category-ID sortierte Folge:

`Vanilla → Variante 1 → Variante 2 → … → Vanilla`

Die Zahl registrierter Varianten wird nicht künstlich begrenzt. Zwei kleine, halbtransparente orange Vektorpfeile links und rechts neben der Figur des vorhandenen Vanilla-Rekrutierungsbuttons schalten per Linksklick rückwärts beziehungsweise vorwärts. Sie liegen gemeinsam mit dem nicht interaktiven Tint in genau einem direkt an `BarracksPanel` angefügten, benannten Host; dadurch bleiben Namescope, Z-Reihenfolge und HUD-Neuaufbau eindeutig. Pfeilklicks werden behandelt und dürfen nicht an den darunterliegenden Rekrutierungsbutton durchgereicht werden. Hauptsymbol und Tooltip zeigen die aktive Auswahl. Die Auswahl wird je Basistyp bis zum Mapwechsel behalten; der Mapwechsel setzt alle Basistypen fail-closed auf Vanilla zurück.

Ist Vanilla aktiv, läuft der bestehende Rekrutierungsbutton vollständig unverändert. Ist eine Variante aktiv, erzeugt `APIShared` einen unveränderlichen Rekrutierungsauftrag mit Owner-/Category-ID, Vanilla-Basistyp, lokalem Spieler und der von Vanilla bestimmten Menge. Normal-, Shift- und Ctrl-Verhalten sowie Verfügbarkeit, Bogenbedarf, Goldpreis und Fehlermeldungen entsprechen zunächst exakt dem Vanilla-Archer. `APIShared` führt keine native Mutation aus dem UI-Thread aus.

`APIShared` setzt während `MainViewModel.ButtonCreateTroop(object)` nur einen threadlokalen Basistyp-Kontext. Erst wenn Vanilla nach seinen Verfügbarkeitsprüfungen wirklich `EngineInterface.GameAction(GameActionCommand.MakeTroop, amount, baseType, 0)` aufruft, aktiviert der zentrale Detour unmittelbar vor dem exakt einmal ausgeführten Vanilla-Trampolin den unveränderlichen Auftrag. `VirtualUnitsPrototype` löst keinen zweiten `GameAction` aus. Ein eng begrenzter Pending-Kontext korreliert ausschließlich nachfolgende `OnUnitTransition`-Ereignisse mit passender Quelle, passendem Besitzer und unverändertem Basistyp. Jede tatsächlich entstandene Unit wird im Simulationstick anhand von Game-ID, Global-ID, Alive-State, Besitzer und Basistyp erneut validiert und erst danach der virtuellen Definition zugeordnet. Gleichzeitig offene Vorgänge für denselben Basistyp werden bis zum Abschluss oder Timeout gesperrt; fremde oder überschüssige Übergänge bleiben Vanilla. Bezahlte Rekruten werden bei einem Korrelations- oder Visualfehler niemals gelöscht, sondern bleiben fail-closed normale Vanilla-Archer.

Der Desert Archer verwendet in dieser Etappe exakt die Kosten und Güteranforderungen des Vanilla-Archers. Eigener Goldpreis, eigene Güterkosten, KI-Auswahl und Multiplayer-Rekrutierung bleiben gesonderte Gameplaymodule. Die UI-Registrierung allein darf niemals vortäuschen, dass die KI eine Variante strategisch oder wirtschaftlich berücksichtigt.

## 5. Registry und Lebenszyklus

- Definitionen für Units und Gebäude liegen getrennt, verwenden aber einen gemeinsamen ID-Namensraum.
- Nach dem Versiegeln ist die Definitionsregistry unveränderlich und deterministisch sortierbar.
- `gameId` bezeichnet immer eine 1-basierte Game-ID; direkte Spanindizes sind 0-basiert und werden nur an der API-Grenze genau einmal umgerechnet.
- Vor jedem Lookup und jeder Mutation werden native Entität, Basistyp und Global-ID erneut validiert.
- Bei abweichender Global-ID wird der veraltete Eintrag verworfen.
- Pro Instanz existiert höchstens ein virtueller Typ.
- Eine Neuzuweisung entfernt kontrolliert den bisherigen Typ und übernimmt danach den neuen.

`OnUnitDelete`, `OnBuildingDelete`, fehlgeschlagene Global-ID-Prüfungen und Map-Unload entfernen logische Zustände. `OnUnitUnityVisualRemove` entfernt nur Rendererbindungen, solange die native Unit weiterlebt. Map-Unload leert zusätzlich Visualbindungen, Pending-Spawns und Pending-Save-Daten.

## 6. Werteänderungen

Beim ersten Zuweisen speichert der Mod ursprüngliche Maximalwerte und aktuellen Zustand.

Units erhalten im PoC geänderte maximale Lebenspunkte, proportional umgerechnete aktuelle Lebenspunkte und – soweit im ganzzahligen `UInt16`-Delaywert darstellbar – Geschwindigkeit. Der beobachtete Archer-Basiswert `1` bleibt für den Faktor 3/2 unverändert, weil kein kleinerer positiver Ganzzahlwert existiert. Gebäude erhalten geänderte maximale und proportional umgerechnete aktuelle Lebenspunkte. Kosten, Produktion, Arbeiter, Schaden und Fähigkeiten bleiben Vanilla.

Faktoren werden als ganzzahlige Brüche gespeichert. Zwischenrechnungen verhindern Überläufe und werden auf den API-Wertebereich begrenzt. Lebende Entitäten behalten mindestens einen Lebenspunkt. Beim Entfernen werden ursprüngliche Maximalwerte wiederhergestellt; der bis dahin erlittene Schadensanteil bleibt erhalten. Save/Load darf Faktoren niemals erneut auf bereits modifizierte Werte multiplizieren.

## 7. Unit-Darstellung

Der Mod abonniert `UnitR3EventHooks.OnUnitUnityVisualSpawn`, um den Haupt-`SpriteRenderer` bei der erstmaligen Erzeugung unmittelbar vor dessen erster Vanilla-Aktualisierung an die 1-basierte Unit-ID zu binden. Das Event wird nicht bei jeder Aktualisierung ausgelöst und ist selbst kein geeigneter Overridepunkt: Es läuft vor `SpriteMapping.SetBodySprite`, sodass eine dort gesetzte Grafik sofort wieder überschrieben würde. `OnUnitUnityVisualInterpolate` bindet bereits vorhandene oder beim Spawn verpasste `Chimp`-Renderer nach.

Ein modlokaler verwalteter Detour auf `SpriteMapping.SetBodySprite(SpriteRenderer,int,int,int,bool,int,int)` prüft bei jeder normalen oder alternativen Frameaktualisierung die gebundene Unit-ID und die zuletzt im Simulationstick validierte Global-ID-/Typzuordnung. Er ruft das Original genau einmal mit unverändertem GM, Frame, Alt-Status, Material-, Teamfarben-, Transparenz- und Fußabschneidungsvertrag auf. Anschließend multipliziert er ausschließlich die RGB-Komponenten der von Vanilla gesetzten Rendererfarbe mit dem virtuellen Tint; Vanillas Alpha bleibt unverändert. Ungebundene Renderer und ungültige Instanzen laufen unverändert durch das Original.

Damit bleiben Stand, Bewegung, Angriff, Treffer, Tod und alle Richtungen exakt an die native Archer-Animation gekoppelt. Der erste brauchbare Vanilla-Sprite und die angewandte Farbe werden einmal je Instanz protokolliert.

Der PoC verwendet keine partiellen benutzerdefinierten Atlanten und hängt daher nicht von den in [Script-Extender Work Item 162](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/162) dokumentierten Material- und Arraylängenproblemen ab.

## 8. Building-Darstellung

### 8.1 Verwalteter Grafik-Hook

Die erste Visualschicht verwendet einen modlokalen Managed-Detour auf:

`SpriteMapping.setGenericBuildingTileGraphic(GameMapTile tile, int file, int image, int light)`

Der Detour ruft immer zuerst das Original auf. Danach ermittelt er aus `tile.gameMapX` und `tile.gameMapY` über `GameTileManagerAPI.GetTileId` die Tile-ID und liest über `GameTileManagerAPI.GetTileBuildingId` die 1-basierte Building-ID. Nach Abgleich mit der zuletzt im Simulationstick validierten Global-ID-/Typzuordnung protokolliert er den Vanilla-Deskriptor `(file, image, light)` und bestätigt einen brauchbaren Vanilla-Sprite; `tile.tileImage` wird nicht ersetzt.

Ein zweiter verwalteter Detour auf `gameTile.setTileColour(GameMapTile,Vector3Int,int)` ruft Vanilla genau einmal auf und multipliziert danach für validierte virtuelle Buildings die bereits berechnete Tilefarbe mit dem RGB-Tint. Alpha, Licht- und Schattenabstufung bleiben erhalten. Mouse-over darf weiterhin Vanillas rote Hervorhebung verwenden.

`StructureGrid`, `AlphaGFXGrid`, Belegung, Wegfindung, Höhe, Building-Typ, Footprint und native Grafikdeskriptoren bleiben unangetastet. Der ursprüngliche `light`-Wert bleibt erhalten. Ungültige GMs und fehlende Frames fallen für das betreffende Tile auf Vanilla zurück.

### 8.2 Refresh und Rückbau

Ein Cache hält die zuletzt beobachteten Vanilla-Deskriptoren betroffener Tiles. Nach Zuweisung wird das Profil auf bekannte Footprint-Tiles angewendet und ein verwalteter Tilemap-Refresh ausgelöst. Beim Entfernen werden die Vanilla-Bilder über den Originalpfad wiederhergestellt. Map-Unload leert Cache und Refreshanforderungen.

Animated Layer aus `GameMap.addUpdateBuildingAnim` werden nicht überschrieben, solange deren `_objectID`-Zuordnung zur Building-ID nicht bestätigt ist. Kann der Managed-Detour nicht eindeutig anhand von Typ, Name und Signatur aufgelöst werden, bleibt die Registry aktiv, die Building-Grafik aber Vanilla. Es werden keine geratenen nativen Hooks oder Adressen eingesetzt.

## 9. Spawnmenü und Eingabe

Ein mod-eigener XAML-Patch ergänzt `IngameUIScreens.xaml` um einen kleinen Button links unten. Darüber öffnet sich ein langes, flaches Panel, das nur einen schmalen unteren Bildschirmbereich belegt und nach einer Auswahl geöffnet bleibt. Es besitzt kompakte Bereiche für `Einheiten` und `Gebäude`. Sichtbare Einträge stammen automatisch aus den registrierten Diagnosedefinitionen; Auswahl, Zielkachel und letztes Ergebnis werden angezeigt.

Eingabefluss:

1. Eine Menüauswahl ersetzt eine eventuell aktive Auswahl.
2. Der Auswahlklick muss vollständig losgelassen sein, bevor Platzierung aktiv wird.
3. Der nächste gültige Linksklick auf die Karte versucht die Erzeugung.
4. Typ und Panel bleiben für weitere Platzierungen aktiv.
5. Rechtsklick oder Escape hebt die Auswahl auf.
6. Klicks über Panel, Triggerbutton oder eindeutig erkannten UI-Flächen erzeugen nichts.
7. Bei ungültiger Position bleibt der Mod im Platzierungsmodus und zeigt den Fehler.

Es gibt im PoC keine Geistervorschau. Das Menü ist nur auf vollständig geladenen Einzelspieler-Gameplaykarten mit eindeutigem lokalen Spieler aktiv; Multiplayer, Lobby, Editor, Replay sowie Lade- und Entladephasen sind gesperrt.

`Application.onBeforeRender` erfasst ausschließlich Unity-Eingaben und leert die Completion-Queue. Native Spawns, Zuweisungen, Entfernungen, Statusänderungen und Tile-Refreshes laufen nur in `GameTimeManagerAPI.OnTick`. Pending-Fristen verwenden Simulationsticks statt Unity-Renderframes; Noesis- und Unity-Objekte werden niemals vom Simulationsthread verändert.

## 10. Erzeugungstransaktionen

### 10.1 Units

Ein HUD- oder API-Aufruf legt zunächst einen unveränderlichen Auftrag an. Ausschließlich `GameTimeManagerAPI.OnTick` validiert Definition, Spielmodus, lokalen Spieler und Zielkachel, ruft `GameUnitManagerAPI.CreateUnitLocal` mit dem Vanilla-Basistyp auf und behandelt den Rückgabewert als 1-basierte Unit-ID. Nach Rücklesen von Global-ID, Besitzer, Basistyp und unveränderten Ausgangswerten entsteht ein tickbasierter Pending-Eintrag. Erst nach `IsAlive`, plausibler Tileposition, Rendererbindung und passendem `SetBodySprite`-Hook wird die Instanz registriert und werden die Werte angewendet. Ein Timeout wird nur bei weiterhin passender Global-ID über `DeleteUnitSafe` bereinigt.

### 10.2 Gebäude

Der Simulationstick verarbeitet den Building-Auftrag mit dem definierten `eMappers`, dem Wert aus `BuildingScales`, `GameBuildingManagerAPI.CreatePrefab`, `bIsFree=true` und `bypassPlacementRules=false`.

Während des synchronen Aufrufs existiert ein eng begrenzter Pending-Spawn-Kontext. `OnBuildingSpawn` akzeptiert nur einen Post-Aufruf, dessen Spieler, Struct-Typ, Koordinate und Rückgabewert zum Kontext passen. Danach werden Building-ID, Global-ID, Besitzer, Basistyp und Ausgangswerte erfasst. Die Registrierung und Wertänderung erfolgen erst nach `IsAlive` und einer bestätigten Footprint-Verknüpfung über `GetTileBuildingId`. Der rohe `CreatePrefab`-Wert wird zusätzlich hexadezimal und als vorzeichenbehafteter Low-32-Bit-Wert protokolliert, aber nicht als Building-ID interpretiert.

Der PoC erlaubt nur einfache Gebäude mit genau einem erwarteten Kerngebäude. Mehrfachobjekt-Bauten, Farmen, Mauern, Tore und Stockpiles werden klar abgelehnt, bis eine atomare Mehrfachinstanztransaktion existiert.

## 11. PoC-Definitionen

### 11.1 Desert Archer

- Typ-ID `serp.virtual-units:desert-archer`;
- Basistyp `eChimps.CHIMP_TYPE_ARCHER`;
- vollständige Vanilla-Archer-Grafik mit leicht kühlem, bläulichem Instanz-Tint;
- maximale Lebenspunkte Basiswert × 2;
- angestrebte 1,5-fache Bewegungsgeschwindigkeit durch Basiswert × 2 / 3; beim realen Basiswert `1` fail-closed unverändert `1`;
- lokaler Besitzer und dessen Teamfarbe;
- kostenloser Diagnose-Spawn.

Beim ersten tatsächlichen Visualaufruf werden Rendererbindung, Vanilla-GM, Frame, Alt-Status, Spriteabmessungen sowie Vanilla- und Tintfarbe protokolliert. Im Spiel stehen ein Vanilla-Archer und ein Desert Archer zum direkten Vergleich nebeneinander.

### 11.2 Desert Hovel

- Typ-ID `serp.virtual-units:desert-hovel`;
- Basistyp `eStructs.STRUCT_HOVEL`;
- Platzierungstyp `eMappers.MAPPER_HOVEL`;
- Scale über `BuildingScales`;
- maximale Lebenspunkte Basiswert × 2;
- kostenlos, aber mit aktiven Vanilla-Platzierungsregeln.

Das diagnostische Visualprofil behält alle von Vanilla gewählten Hovel-Sprites und färbt ihre Tilefarben pro Instanz deutlich bläulich ein. So bleiben Abmessungen, Pivot, Footprint, Licht und Zustandsvarianten korrekt. Eine echte Bäckereidarstellung folgt erst nach separater Erfassung und Kalibrierung aller Hovel- und Workshop-Tiles; pauschale gleiche Frameindizes bleiben verboten.

## 12. Speichern und Laden

`ModSaveDataAPI.RegisterModDataHandler` speichert Datenformatversion, Entitätsart, 1-basierte Game-ID, Global-ID, Typ-ID, Definitionsversion, ursprüngliche maximale Gesundheit und bei Units ursprüngliche Geschwindigkeit.

Der Load-Callback deserialisiert nur in eine Pending-Liste. Wiederherstellung beginnt erst bei vollständig geladener Karte, verfügbaren Managern und versiegelter Definitionsregistry. Jeder Eintrag wird unabhängig validiert. Unbekannte oder inkompatible Definitionen, falsche Basistypen und Global-ID-Abweichungen lassen die Vanilla-Entität unverändert und verhindern nicht die Wiederherstellung anderer Einträge.

## 13. Multiplayer

Der erste Build verweigert Menüplatzierung, Spawn, Zuweisung und Entfernung vor jeder Mutation, sobald `GameModeHelper` Multiplayer oder einen nicht eindeutig sicheren Kontext erkennt. `NetworkMode=1` gilt trotzdem bereits im PoC.

Eine spätere Freigabe verwendet ausschließlich Chores: Die lokale Eingabe sendet eine Anforderung, verändert noch nichts und wird erst beim tickgleichen Empfang verarbeitet. Alle Teilnehmer benötigen identische Typ-IDs, Definitionsversionen und Registry-Fingerprints.

## 14. Spätere Erweiterungen

Nach stabiler Registry folgen deterministische Module für Schaden, Angriffseffekte, eigene Rekrutierungskosten, KI-Variantenwahl, Fähigkeiten, Cooldowns, Building-Produktion, Building-Kosten und Ereignislogik. Die vorherige gleich teure Rekrutierungsvariante ist ausschließlich eine Präsentations- und Instanzzuordnungsfunktion; sie ändert keine Vanilla-Kostentabelle. Module erhalten nur validierte `VirtualEntityKey`-Instanzen und dürfen keine globalen Vanilla-Typwerte ändern, wenn lediglich einzelne Instanzen betroffen sind. Persistenter Modulzustand benötigt ein eigenes versioniertes Saveformat; sonst müssen Module zustandslos sein.

Eine getrennte Atlas-Baker-Pipeline folgt erst nach erfolgreichem Registry-, Save- und Building-PoC. Bevorzugte Eingabe ist glTF/GLB; FBX benötigt einen optionalen externen Konverter. Ausgabe sind vollständige Atlanten, Teamfarbenmasken und Metadaten für Kamera, Richtungen, Animationen, Framebereiche, Pivot, Fußpunkt und Transparenz. Der Baker arbeitet außerhalb des Spiels und ändert den Registryvertrag nicht.

## 15. Tests und Abnahme

### 15.1 Automatisierte Tests

- Ablehnung von ID 0, negativen und falsch basierten IDs;
- Global-ID-Wechsel, Löschung und Slot-Wiederverwendung;
- doppelte Typ-ID und falsche Mapper-/Struct-Kombination;
- falscher Vanilla-Basistyp und unbekannte Definition;
- proportionale Gesundheit ohne Heilung oder erneute Multiplikation;
- Normalframe, Alt-Frame, unveränderte Animationsframes, Tint und Object-Pooling;
- Queue-Ausführung ausschließlich im Simulationstick, tickbasierter Timeout und getrennte Unity-Completion;
- Erfolg erst nach Renderer plus Body-Hook beziehungsweise Footprint plus Building-Tile-Hook;
- Building-Hook ruft das Original genau einmal auf;
- Building-Override verändert keine nativen Grids;
- visueller Rückbau stellt Vanilla-Farben und -Grafiken wieder her;
- Auswahlklick platziert noch nichts;
- Rechtsklick und Escape brechen ab;
- Multiplayeraufrufe scheitern vor jeder Mutation;
- strikt getrennte Vanilla-/Variantenanzahlen in Auswahl-HUD, Kontrollgruppen und Armeereport bei unveränderter Gesamttruppenzahl;
- sprachabhängige Namens- und Beschreibungsresolver einschließlich Exception- und Fallbackpfad;
- getöntes Basissymbol ohne Änderung des Vanilla-Sprites und korrektes Zurücksetzen nach Farb-, Kultur- und Mapwechsel;
- beliebig viele Varianten eines Basistyps in deterministischer Reihenfolge sowie zyklisches Vorwärts-/Rückwärtsschalten;
- Variantenwahl bleibt beim Schließen der Kaserne erhalten und wird beim Mapwechsel auf Vanilla zurückgesetzt;
- Rekrutierung verwendet für Vanilla und Variante dieselben Verfügbarkeits-, Kosten- und Mengenpfade;
- Pending-Rekrutierung akzeptiert nur passende `EuropeanBarracks`-Übergänge und lehnt mehrdeutige Aufträge ab.

### 15.2 Spieltests

Ein Vanilla-Archer und ein Desert Archer desselben Basistyps stehen nebeneinander. Nur der virtuelle Archer erhält dauerhaft den leichten Tint und doppelte Maximalgesundheit; beide verwenden dieselben korrekten Vanilla-Frames. Die Geschwindigkeit bleibt beim nicht darstellbaren Vanilla-Delay `1` unverändert und wird diagnostisch gemeldet. Bewegung, Angriff, Auswahl, Schaden, Tod und Object-Pooling funktionieren; ein wiederverwendeter Slot bleibt Vanilla.

Ein Vanilla-Hovel und ein Desert Hovel stehen nebeneinander. Nur das virtuelle Hovel erhält den deutlicheren Tile-Tint und doppelte Maximalgesundheit. Vanilla-Sprites, Nachbartiles, Footprint, Wegfindung und Belegung bleiben unverändert. Ungültige Baupositionen erzeugen weder Registryeintrag noch Teilzustand.

Save/Load stellt beide Zuordnungen genau einmal wieder her. Mapwechsel leert Registry, Visualbindungen und Pending-Spawns. Inkompatible Save-Daten lassen die betreffende Vanilla-Entität unverändert.

Für die nächste Rekrutierungsetappe stehen normaler Archer und Desert Archer am selben Kasernenbutton zur Auswahl. Der Variantenwähler schaltet in beide Richtungen, aktualisiert Symbol, Tooltip und Kurzlabel und behält seine Wahl bis zum Mapwechsel. Normal-, Shift- und Ctrl-Rekrutierung erzeugen exakt so viele Desert-Archer-Zuordnungen, wie Vanilla tatsächlich Archer erzeugt hat. Auswahl-HUD, Kontrollgruppen, Hover, Armeereport und Einzelunit-Detailansicht zeigen den eigenen Namen und strikt getrennte Anzahlen; das Icon bleibt ein ausschließlich getönter Vanilla-Archer.

Eine spätere Multiplayerfreigabe verlangt identische Registrydefinitionen, tickgleiche Verarbeitung, Join-/Load-Prüfungen und gezielte Desync-Tests mit mindestens zwei Teilnehmern.

## 16. Umsetzungsreihenfolge

1. Projektgerüst, Abhängigkeiten, `info.json` und Initialisierung.
2. Öffentliche Definitionstypen und versiegelte Definitionsregistry.
3. Instanzregistry mit ID-/Global-ID-Validierung.
4. Unit-Zuweisung, Werteänderungen und Desert-Archer-Visual.
5. Save-/Load-Lebenszyklus für Units.
6. Managed-Building-Hooks, Tint und Rückbau.
7. Building-Zuweisung, Lebenspunkte und Desert-Hovel-Test.
8. Kleines horizontales HUD und Kartenplatzierungszustand.
9. Unit- und Building-Spawntransaktionen.
10. Automatisierte Tests und statische Vertragsprüfungen.
11. Betroffene Textdateien auf CRLF und Code auf fachliche Zahlenwerte prüfen.
12. `APIShared`, `BugfixesAndQoL` und `VirtualUnitsPrototype` in dieser Reihenfolge jeweils einmalig über ihre erhöhten `build.bat /nopause`-Aufrufe bauen und installieren.
13. Gemeinsamer Spieltest mit Lord und Desert Archer anhand der Abnahmekriterien.
14. `APIShared` um generische Rekrutierungsvarianten, Textresolver und Einzelunit-Details erweitern.
15. Desert Archer am vorhandenen Archer-Button registrieren und Übergänge im Simulationstick korrelieren.
16. Rekrutierungs-, Mehrvarianten- und Detailanzeigentests durchführen; danach gemeinsamer Spieltest mit Vanilla-Archer, Desert Archer und Lord.
17. Erst nach finaler Bestätigung Version erhöhen und fragen, ob das Feature in die README soll.

## 17. Festgelegte Grenzen und Annahmen

- Keine Änderung am Script Extender; Version 2.5.0 ist die geprüfte Zielversion, während dieser nachweislich rückwärtskompatible Mod 2.3.0 als Mindestversion behält.
- Keine neuen nativen Enum- oder Arrayeinträge.
- Keine geratenen RVAs, AOBs oder nativen Hooks.
- Keine Runtime-FBX-Unterstützung oder dauerhaft als 3D-Mesh gerenderten Units.
- Keine Multiplayerfreigabe im ersten Build. Die geplante Kasernenintegration verwendet ausschließlich den vorhandenen Vanilla-Archer-Button mit generischem Variantenwähler; das Diagnose-Spawnmenü bleibt davon getrennt.
- Keine komplexen Mehrfachobjekt-Gebäude im Diagnosemenü.
- Keine Abhängigkeit von CastlePlanner.
- Kein eigenes 3D-Modell und kein Unity Editor für den ersten Test.
- Building-Visuals arbeiten fail-closed, wenn der Managed-Vertrag nicht passt.
- Grafiken und Gameplaydefinitionen bleiben austauschbar.
- Der kanonische lokale Script Extender bleibt unverändert.
- Diese Markdown-Datei verwendet vollständig CRLF-Zeilenenden.

## 18. Erwartetes Endergebnis

Nach Umsetzung des PoC kann ein weiterer Mod über die öffentliche C#-API einen virtuellen Unit- oder Building-Typ registrieren, eine kompatible Vanilla-Entität erzeugen oder zuweisen und sie anhand von Game-ID plus Global-ID sicher wiedererkennen. Nur diese Instanz erhält ihre alternative Darstellung und Werte. Die nächste API-Etappe lässt beliebig viele gleich teure Varianten am vorhandenen Rekrutierungsbutton auswählen und präsentiert sie in allen festgelegten HUD-Flächen als getrennte Kategorien. Vanilla übernimmt weiterhin die Simulation; das virtuelle System kann anschließend um deterministische Verhaltensmodule, KI-Auswahl, eigene Kosten, Chore-Synchronisierung und externe Atlas-Erzeugung erweitert werden.
