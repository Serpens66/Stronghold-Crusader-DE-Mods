# Virtuelle Einheiten und Gebäude für SHCDE

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
- ausschließlich vorhandene Vanilla-Grafiken.

Der Script Extender 2.3.0 bleibt unverändert. Dieses Dokument plant die spätere Mod-Implementierung, implementiert sie aber noch nicht.

## 1.1 Übergabe an einen neuen Chat oder Implementierer

### Aktueller Stand

- Im Ordner `VirtualUnitsPrototype` existiert gegenwärtig ausschließlich diese Planungsdatei.
- Es gibt noch kein `.csproj`, keinen C#-Code, keinen XAML-Patch, keine `info.json`, keine Tests und keine `build.bat`.
- Es wurde noch kein Mod gebaut oder im Spiel getestet.
- Die beschriebenen PoC-Namen, Faktoren und Bedienungsentscheidungen sind festgelegt; technische Annahmen mit Verifikationskennzeichnung sind dagegen vor ihrer Nutzung zu prüfen.

### Verbindlicher Arbeitsauftrag

Ein neuer Chat soll nicht erneut die Architektur entwerfen, sondern die Abschnitte 3 bis 16 in der angegebenen Reihenfolge umsetzen. Vor dem ersten Code ist Abschnitt 4.5 vollständig abzuarbeiten und als kurze Verifikationsnotiz im Implementierungsfortschritt festzuhalten. Danach soll der Implementierer:

1. die einschlägige `AGENTS.md` vollständig lesen;
2. den aktuellen Inhalt dieses Ordners und passende vorhandene Workspace-Mods inventarisieren;
3. die installierte Script-Extender-Version und alle tatsächlich referenzierten Assemblies prüfen;
4. die mit `OFFEN` markierten Verträge durch Quellcode-, Assembly- oder kontrollierte Laufzeitdiagnose klären;
5. nur bestätigte Verträge implementieren und alle anderen Teile fail-closed lassen;
6. zunächst Registry und Unit-PoC, danach Building-PoC, HUD, Persistenz und Tests implementieren;
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
- keine Änderung am Script Extender 2.3.0;
- keine README- oder Versionsänderung während der Testphase.

### Stop- und Rückfrageregeln

Die Implementierung muss anhalten und den Benutzer mit konkreter Evidenz fragen, wenn:

- Plan, kanonischer 2.3.0-Quellcode und installierte `SHCDESE.dll` einander widersprechen;
- die installierte Zielversion nicht Script Extender 2.3.0 ist;
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
- Harte Abhängigkeiten ausschließlich von BepInEx, installierten Unity-/Spiel-Assemblies und Script Extender 2.3.0.
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
- `UnitSpriteProfile` und `BuildingTileVisualProfile`;
- `VirtualStatProfile` und `VirtualSpawnOptions`;
- `VirtualApiResult` mit Ergebniscode und lesbarer Fehlermeldung.

`VirtualUnitDefinition` enthält Typ-ID, positive Definitionsversion, Anzeigename, `eChimps`-Basistyp, Spriteprofil, rationale Gesundheits- und Geschwindigkeitsfaktoren sowie Diagnosemenü-Sichtbarkeit.

`VirtualBuildingDefinition` enthält Typ-ID, positive Definitionsversion, Anzeigename, `eStructs`-Basistyp, zugehörigen `eMappers`, den über `BuildingScales` ermittelten Scale, Tile-Visualprofil, rationalen Gesundheitsfaktor, Diagnosemenü-Sichtbarkeit und PoC-Spawnfreigabe. Mapper und Struct werden über die Extender-Zuordnung gegengeprüft.

Alle fachlichen Werte referenzieren unmittelbar die benannten Enums und Konstanten des Script Extenders 2.3.0. Eigene numerische Kopien von Unit-, Building-, Mapper-, Goods-, Chore- oder GM-Werten sind verboten.

### 4.2 API-Operationen

`VirtualEntityApi` bietet statische, intern synchronisierte Methoden:

- `RegisterUnitDefinition`, `RegisterBuildingDefinition`;
- `TryGetUnitDefinition`, `TryGetBuildingDefinition`;
- `TryAssignUnit`, `TryAssignBuilding`;
- `TryRemoveUnitAssignment`, `TryRemoveBuildingAssignment`;
- `TryGetUnitInstance`, `TryGetBuildingInstance`;
- `SpawnVirtualUnit`, `SpawnVirtualBuilding`.

Ereignisse melden erfolgreiche Zuweisung, Entfernung und abgelehnte Wiederherstellung. Definitionen dürfen nur während der Initialisierung registriert werden; danach wird die Registry versiegelt. Leere oder doppelte IDs, unbekannte Enumwerte, unzulässige Faktoren, falsche Basistypen und widersprüchliche Mapper-/Struct-Paare werden mit `VirtualApiResult` abgelehnt und protokolliert.

### 4.3 Festzulegende Signaturen vor Implementierungsbeginn

Die öffentliche API darf erst geschrieben werden, nachdem die konkreten Typen der Extender-Methoden geprüft wurden. Danach sind die öffentlichen Modsignaturen einmalig festzulegen und durch API-Tests zu sichern. Insbesondere müssen Rückgabetypen, Wertebereiche und Null-/Fehlersemantik der folgenden Verträge berücksichtigt werden:

- Unit-Erzeugung liefert `Int64`, die bestätigte positive Game-ID wird vor Registrynutzung auf den zulässigen `int`-Bereich geprüft.
- Building-Erzeugung über `CreatePrefab` liefert einen Ergebniswert, aber nicht verlässlich die gewünschte Building-ID; die ID stammt aus dem korrelierten Building-Spawn-Postereignis.
- `SetSpeed` und Building-`SetMaxHealth` verwenden `UInt16`; Modberechnungen müssen vor dem Aufruf begrenzen.
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
- `SpawnFailed`
- `VisualFeatureUnavailable`
- `SaveDefinitionIncompatible`
- `InternalError`

Normale Validierungsfehler dürfen nicht als Exceptions aus der öffentlichen API austreten. Unerwartete interne Fehler werden abgefangen, zeitgestempelt protokolliert und als `InternalError` gemeldet, ohne einen halbfertigen Registryeintrag zu hinterlassen.

### 4.5 Verifikationsmatrix vor dem ersten Code

#### Bereits im kanonischen 2.3.0-Quellbaum belegt

Die folgenden Punkte wurden bei Erstellung dieses Dokuments im lokalen Fork `shcde-script-extender` geprüft. Sie sind vor dem Build zusätzlich gegen die tatsächlich referenzierte installierte Assembly zu bestätigen:

- `GameUnitManagerAPI.CreateUnitLocal(...)` existiert und liefert `Int64`.
- Unit-APIs für maximale Gesundheit und Geschwindigkeit sind vorhanden; `SetSpeed` erwartet `UInt16`.
- `GameBuildingManagerAPI.CreatePrefab(...)` existiert, verwendet `eMappers` und liefert `Int64`.
- Building-APIs für aktuelle und maximale Gesundheit sind vorhanden; `SetMaxHealth` erwartet `UInt16`.
- `BuildingScales.GetScale(eMappers)` ist öffentlich und liefert bei unbekanntem Mapper `-1`.
- `GamePlayerManagerAPI.GetLocalPlayerId()` ist vorhanden.
- `UnitR3EventHooks.OnUnitUnityVisualSpawn` und `OnUnitUnityVisualRemove` sind vorhanden.
- `BuildingR3EventHooks.OnBuildingSpawn` und `OnBuildingDelete` sind vorhanden.
- `OnBuildingSpawn` wird in Pre- und Postphase ausgelöst; das Postereignis übernimmt den nativen Rückgabewert in `ReturnValue`.
- `ModSaveDataAPI.RegisterModDataHandler(...)` ist vorhanden.
- `GameTileManagerAPI.GetTileId(...)` und `StructureGrid` sind im Extender verfügbar.
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

- Die installierte `SHCDESE.dll` ist tatsächlich Version 2.3.0 und enthält dieselben Methodensignaturen und Enumzuordnungen wie der kanonische Fork.
- Die installierte Spiel-Assembly enthält `SpriteMapping.setGenericBuildingTileGraphic(GameMapTile,int,int,int)` genau einmal und mit hookbarer statischer Signatur.
- `GameMapTile.gameMapX`, `gameMapY`, `tileImage` und `light` haben in der installierten Managed-Assembly die erwarteten zugänglichen Typen.
- `spriteLoader.instance`, GM-Normal-/Alt-Arrays, Materialarrays und die benötigten Getter sind in der tatsächlich referenzierten Assembly erreichbar.
- Der genaue Weg zur Bestimmung von Quell-GM, Frameindex und Alt-Frame aus dem aktuellen Unit-Sprite ist eindeutig und kollisionsfrei. Eine bloße Sprite-Namensheuristik reicht nicht ohne Tests.
- Alle verwendeten Enumwerte, besonders `CHIMP_TYPE_ARCHER`, `STRUCT_HOVEL`, `MAPPER_HOVEL`, `GM_BODY_ARAB_BOW`, `GM_BUILDINGS1` und `GM_BUILDINGS2`, stimmen zwischen Quellcode und Assembly überein.
- Die Callbacksignaturen und Zeitpunkte von `ModSaveDataAPI` passen zum vorgesehenen Pending-Restore.
- Die für Map-Start und Map-Unload gewählten Events laufen in der benötigten Reihenfolge.
- `Shared/GameModeHelper.cs` kann als Quelllink ohne zusätzliche Modabhängigkeit eingebunden werden und seine benötigten Referenzen sind im neuen Projekt vollständig vorhanden.
- Das XAML-Patchziel und alle Bindingnamen kollidieren weder mit Vanilla noch mit gemeinsam installierten Workspace-Mods.
- Mauskoordinaten, Kartenkoordinaten und `CreateUnitLocal` verwenden dieselbe lokale Tilebasis; keine zusätzliche Achtfachskalierung darf modseitig erfolgen.

#### Im Spiel diagnostisch zu verifizieren

Diese Verträge lassen sich nicht allein durch Kompilierung hinreichend belegen. Dafür wird zunächst zeitgestempeltes Diagnose-Logging eingebaut, anschließend wird der jeweilige Pfad erst nach erfolgreichem Test als bestätigt markiert:

- `CreateUnitLocal` liefert unter realen Bedingungen die 1-basierte ID der erzeugten Unit und die Global-ID ist unmittelbar lesbar.
- Der Pending-Kontext korreliert ein einfaches Hovel eindeutig mit genau einem passenden Building-Spawn-Postereignis.
- Der Building-Spawn-Rückgabewert ist beim Hovel die 1-basierte Building-ID und stimmt mit dem gefundenen Struct/Global-ID-Paar überein.
- Die Registrierung geschieht früh genug, dass der erste Building-Renderpass den virtuellen Typ sieht; andernfalls funktioniert der Cache-/Refreshpfad.
- `StructureGrid[tileId]` liefert während des Managed-Grafikaufrufs die erwartete 1-basierte Building-ID.
- Der Wechsel `GM_BUILDINGS1`/`GM_BUILDINGS2` bei gleichem Frameindex erzeugt beim Hovel vorhandene, sichtbare und technisch sichere Testframes. Falls nicht, wird vor einer alternativen Zuordnung die konkrete Framebelegung untersucht.
- Der Unit-Frameindex zwischen Archer und `GM_BODY_ARAB_BOW` ist für Stand, Bewegung, Angriff, Treffer, Tod und alle Richtungen semantisch kompatibel.
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

## 5. Registry und Lebenszyklus

- Definitionen für Units und Gebäude liegen getrennt, verwenden aber einen gemeinsamen ID-Namensraum.
- Nach dem Versiegeln ist die Definitionsregistry unveränderlich und deterministisch sortierbar.
- `gameId` bezeichnet immer eine 1-basierte Game-ID; direkte Spanindizes sind 0-basiert und werden nur an der API-Grenze genau einmal umgerechnet.
- Vor jedem Lookup und jeder Mutation werden native Entität, Basistyp und Global-ID erneut validiert.
- Bei abweichender Global-ID wird der veraltete Eintrag verworfen.
- Pro Instanz existiert höchstens ein virtueller Typ.
- Eine Neuzuweisung entfernt kontrolliert den bisherigen Typ und übernimmt danach den neuen.

`OnUnitDelete`, `OnBuildingDelete`, fehlgeschlagene Global-ID-Prüfungen und Map-Unload entfernen logische Zustände. `OnUnitUnityVisualRemove` entfernt nur Rendererbindungen, solange die native Unit weiterlebt. Map-Unload leert zusätzlich Tilecache, Pending-Spawns und Pending-Save-Daten.

## 6. Werteänderungen

Beim ersten Zuweisen speichert der Mod ursprüngliche Maximalwerte und aktuellen Zustand.

Units erhalten im PoC geänderte maximale Lebenspunkte, proportional umgerechnete aktuelle Lebenspunkte und Geschwindigkeit. Gebäude erhalten geänderte maximale und proportional umgerechnete aktuelle Lebenspunkte. Kosten, Produktion, Arbeiter, Schaden und Fähigkeiten bleiben Vanilla.

Faktoren werden als ganzzahlige Brüche gespeichert. Zwischenrechnungen verhindern Überläufe und werden auf den API-Wertebereich begrenzt. Lebende Entitäten behalten mindestens einen Lebenspunkt. Beim Entfernen werden ursprüngliche Maximalwerte wiederhergestellt; der bis dahin erlittene Schadensanteil bleibt erhalten. Save/Load darf Faktoren niemals erneut auf bereits modifizierte Werte multiplizieren.

## 7. Unit-Darstellung

Der Mod abonniert `UnitR3EventHooks.OnUnitUnityVisualSpawn`, das auch bei laufenden Spriteaktualisierungen und Object-Pooling ausgelöst wird.

Pro Aufruf werden Unit-ID und Global-ID validiert, das aktuelle Vanilla-Sprite im Quell-GM beziehungsweise Alt-Array lokalisiert und Frameindex sowie Alt-Kennzeichen übernommen. Existiert der entsprechende Zielframe, setzt der Mod Ziel-Sprite, passendes Zielmaterial und den Ziel-Farbvertrag aus `spriteLoader.instance`. Transparenz, Flip, Sortierung, Rendereraktivität und Fußabschneidung bleiben erhalten.

Fehlt ein Zielframe, bleibt nur dieser Aufruf Vanilla. Die Kombination aus Typ-ID, Ziel-GM, Frameindex und Alt-Status wird nur einmal protokolliert. Ein fehlender Frame deaktiviert keine anderen Animationen.

Der PoC verwendet keine partiellen benutzerdefinierten Atlanten und hängt daher nicht von den in [Script-Extender Work Item 162](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/162) dokumentierten Material- und Arraylängenproblemen ab.

## 8. Building-Darstellung

### 8.1 Verwalteter Grafik-Hook

Die erste Visualschicht verwendet einen modlokalen Managed-Detour auf:

`SpriteMapping.setGenericBuildingTileGraphic(GameMapTile tile, int file, int image, int light)`

Der Detour ruft immer zuerst das Original auf. Danach ermittelt er aus `tile.gameMapX` und `tile.gameMapY` über `GameTileManagerAPI.GetTileId` die Tile-ID und liest aus `StructureGrid[tileId]` die 1-basierte Building-ID. Nach Prüfung von Building-ID, Global-ID, Basistyp und Definition speichert er den Vanilla-Deskriptor `(file, image, light)` und ersetzt ausschließlich `tile.tileImage`, falls das Visualprofil einen vorhandenen Zielframe liefert.

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

## 10. Erzeugungstransaktionen

### 10.1 Units

`SpawnVirtualUnit` validiert Definition, Spielmodus, lokalen Spieler und Zielkachel, ruft `GameUnitManagerAPI.CreateUnitLocal` mit dem Vanilla-Basistyp auf und behandelt den Rückgabewert als 1-basierte Unit-ID. Nach Rücklesen von Global-ID und Basistyp wird die Instanz registriert und erst dann werden Werte angewendet. Schlägt die Nachprüfung fehl, bleibt die erzeugte Unit Vanilla und kein Teilzustand wird behalten.

### 10.2 Gebäude

`SpawnVirtualBuilding` verwendet den definierten `eMappers`, den Wert aus `BuildingScales`, `GameBuildingManagerAPI.CreatePrefab`, `bIsFree=true` und `bypassPlacementRules=false`.

Während des synchronen Aufrufs existiert ein eng begrenzter Pending-Spawn-Kontext. `OnBuildingSpawn` akzeptiert nur einen Post-Aufruf, dessen Spieler, Struct-Typ, Koordinate und Rückgabewert zum Kontext passen. Danach werden Building-ID, Global-ID und Basistyp geprüft und die Instanz sofort registriert.

Der PoC erlaubt nur einfache Gebäude mit genau einem erwarteten Kerngebäude. Mehrfachobjekt-Bauten, Farmen, Mauern, Tore und Stockpiles werden klar abgelehnt, bis eine atomare Mehrfachinstanztransaktion existiert.

## 11. PoC-Definitionen

### 11.1 Desert Archer

- Typ-ID `serp.virtual-units:desert-archer`;
- Basistyp `eChimps.CHIMP_TYPE_ARCHER`;
- Ziel-GM `Enums.GM.GM_BODY_ARAB_BOW`;
- maximale Lebenspunkte Basiswert × 2;
- Geschwindigkeit Basiswert × 3 / 2;
- lokaler Besitzer und dessen Teamfarbe;
- kostenloser Diagnose-Spawn.

Beim Start wird die Verfügbarkeit normaler und alternativer Zielframes protokolliert. Im Spiel stehen ein Vanilla-Archer und ein Desert Archer zum direkten Vergleich nebeneinander.

### 11.2 Desert Hovel

- Typ-ID `serp.virtual-units:desert-hovel`;
- Basistyp `eStructs.STRUCT_HOVEL`;
- Platzierungstyp `eMappers.MAPPER_HOVEL`;
- Scale über `BuildingScales`;
- maximale Lebenspunkte Basiswert × 2;
- kostenlos, aber mit aktiven Vanilla-Platzierungsregeln.

Das diagnostische Visualprofil tauscht vorhandene statische Frames zwischen `Enums.GM.GM_BUILDINGS1` und `Enums.GM.GM_BUILDINGS2` bei gleichem Frameindex. Dies ist nur ein sichtbarer per-Instanz-Nachweis, kein endgültiges Artwork. Fehlende Zielframes bleiben Vanilla.

## 12. Speichern und Laden

`ModSaveDataAPI.RegisterModDataHandler` speichert Datenformatversion, Entitätsart, 1-basierte Game-ID, Global-ID, Typ-ID, Definitionsversion, ursprüngliche maximale Gesundheit und bei Units ursprüngliche Geschwindigkeit.

Der Load-Callback deserialisiert nur in eine Pending-Liste. Wiederherstellung beginnt erst bei vollständig geladener Karte, verfügbaren Managern und versiegelter Definitionsregistry. Jeder Eintrag wird unabhängig validiert. Unbekannte oder inkompatible Definitionen, falsche Basistypen und Global-ID-Abweichungen lassen die Vanilla-Entität unverändert und verhindern nicht die Wiederherstellung anderer Einträge.

## 13. Multiplayer

Der erste Build verweigert Menüplatzierung, Spawn, Zuweisung und Entfernung vor jeder Mutation, sobald `GameModeHelper` Multiplayer oder einen nicht eindeutig sicheren Kontext erkennt. `NetworkMode=1` gilt trotzdem bereits im PoC.

Eine spätere Freigabe verwendet ausschließlich Chores: Die lokale Eingabe sendet eine Anforderung, verändert noch nichts und wird erst beim tickgleichen Empfang verarbeitet. Alle Teilnehmer benötigen identische Typ-IDs, Definitionsversionen und Registry-Fingerprints.

## 14. Spätere Erweiterungen

Nach stabiler Registry folgen deterministische Module für Schaden, Angriffseffekte, Rekrutierungskosten, Fähigkeiten, Cooldowns, Building-Produktion, Building-Kosten und Ereignislogik. Module erhalten nur validierte `VirtualEntityKey`-Instanzen und dürfen keine globalen Vanilla-Typwerte ändern, wenn lediglich einzelne Instanzen betroffen sind. Persistenter Modulzustand benötigt ein eigenes versioniertes Saveformat; sonst müssen Module zustandslos sein.

Eine getrennte Atlas-Baker-Pipeline folgt erst nach erfolgreichem Registry-, Save- und Building-PoC. Bevorzugte Eingabe ist glTF/GLB; FBX benötigt einen optionalen externen Konverter. Ausgabe sind vollständige Atlanten, Teamfarbenmasken und Metadaten für Kamera, Richtungen, Animationen, Framebereiche, Pivot, Fußpunkt und Transparenz. Der Baker arbeitet außerhalb des Spiels und ändert den Registryvertrag nicht.

## 15. Tests und Abnahme

### 15.1 Automatisierte Tests

- Ablehnung von ID 0, negativen und falsch basierten IDs;
- Global-ID-Wechsel, Löschung und Slot-Wiederverwendung;
- doppelte Typ-ID und falsche Mapper-/Struct-Kombination;
- falscher Vanilla-Basistyp und unbekannte Definition;
- proportionale Gesundheit ohne Heilung oder erneute Multiplikation;
- Normalframe, Alt-Frame, fehlender Zielframe und Object-Pooling;
- Building-Hook ruft das Original genau einmal auf;
- Building-Override verändert keine nativen Grids;
- Tilecache stellt Vanilla-Grafiken wieder her;
- Auswahlklick platziert noch nichts;
- Rechtsklick und Escape brechen ab;
- Multiplayeraufrufe scheitern vor jeder Mutation.

### 15.2 Spieltests

Ein Vanilla-Archer und ein Desert Archer desselben Basistyps stehen nebeneinander. Nur der virtuelle Archer erhält dauerhaft alternative Grafik, doppelte Maximalgesundheit und 1,5-fache Geschwindigkeit. Bewegung, Angriff, Auswahl, Schaden, Tod und Object-Pooling funktionieren; ein wiederverwendeter Slot bleibt Vanilla.

Ein Vanilla-Hovel und ein Desert Hovel stehen nebeneinander. Nur das virtuelle Hovel erhält alternative Tilegrafik und doppelte Maximalgesundheit. Nachbartiles, Footprint, Wegfindung und Belegung bleiben unverändert. Ungültige Baupositionen erzeugen weder Registryeintrag noch Teilzustand.

Save/Load stellt beide Zuordnungen genau einmal wieder her. Mapwechsel leert Registry, Tilecache, Rendererbindungen und Pending-Spawns. Inkompatible Save-Daten lassen die betreffende Vanilla-Entität unverändert.

Eine spätere Multiplayerfreigabe verlangt identische Registrydefinitionen, tickgleiche Verarbeitung, Join-/Load-Prüfungen und gezielte Desync-Tests mit mindestens zwei Teilnehmern.

## 16. Umsetzungsreihenfolge

1. Projektgerüst, Abhängigkeiten, `info.json` und Initialisierung.
2. Öffentliche Definitionstypen und versiegelte Definitionsregistry.
3. Instanzregistry mit ID-/Global-ID-Validierung.
4. Unit-Zuweisung, Werteänderungen und Desert-Archer-Visual.
5. Save-/Load-Lebenszyklus für Units.
6. Managed-Building-Hook, Tilecache und Rückbau.
7. Building-Zuweisung, Lebenspunkte und Desert-Hovel-Test.
8. Kleines horizontales HUD und Kartenplatzierungszustand.
9. Unit- und Building-Spawntransaktionen.
10. Automatisierte Tests und statische Vertragsprüfungen.
11. Betroffene Textdateien auf CRLF und Code auf fachliche Zahlenwerte prüfen.
12. Einmalig `VirtualUnitsPrototype/build.bat` über den vorgeschriebenen erhöhten PowerShell-Aufruf ausführen.
13. Gemeinsamer Spieltest anhand der Abnahmekriterien.
14. Erst nach finaler Bestätigung Version erhöhen und fragen, ob das Feature in die README soll.

## 17. Festgelegte Grenzen und Annahmen

- Keine Änderung am Script Extender; Version 2.3.0 bleibt alleinige Mindest- und Zielversion.
- Keine neuen nativen Enum- oder Arrayeinträge.
- Keine geratenen RVAs, AOBs oder nativen Hooks.
- Keine Runtime-FBX-Unterstützung oder dauerhaft als 3D-Mesh gerenderten Units.
- Keine Multiplayerfreigabe, Rekrutierungs- oder Vanilla-Baumenüintegration im ersten Build.
- Keine komplexen Mehrfachobjekt-Gebäude im Diagnosemenü.
- Keine Abhängigkeit von CastlePlanner.
- Kein eigenes 3D-Modell und kein Unity Editor für den ersten Test.
- Building-Visuals arbeiten fail-closed, wenn der Managed-Vertrag nicht passt.
- Grafiken und Gameplaydefinitionen bleiben austauschbar.
- Der kanonische lokale Script Extender bleibt unverändert.
- Diese Markdown-Datei verwendet vollständig CRLF-Zeilenenden.

## 18. Erwartetes Endergebnis

Nach Umsetzung des PoC kann ein weiterer Mod über die öffentliche C#-API einen virtuellen Unit- oder Building-Typ registrieren, eine kompatible Vanilla-Entität erzeugen oder zuweisen und sie anhand von Game-ID plus Global-ID sicher wiedererkennen. Nur diese Instanz erhält ihre alternative Darstellung und Werte. Vanilla übernimmt weiterhin die Simulation; das virtuelle System kann anschließend um deterministische Verhaltensmodule, Chore-Synchronisierung und externe Atlas-Erzeugung erweitert werden.
