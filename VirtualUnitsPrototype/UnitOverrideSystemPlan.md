# Virtuelle Einheiten und Gebäude für SHCDE

## 1. Ziel und Grundidee

Dieses Dokument beschreibt ein System, mit dem Mods scheinbar neue Einheiten und Gebäude bereitstellen können, ohne die nativen Typ-Enums, fest dimensionierten Arrays, Speicherformate und zahlreichen typabhängigen Verzweigungen von Stronghold Crusader Definitive Edition zu erweitern.

Eine virtuelle Entität verwendet intern immer einen geeigneten Vanilla-Basistyp. Das Spiel simuliert daher weiterhin ausschließlich bekannte `eChimps`- beziehungsweise `eStructs`-Typen. Der Mod führt daneben eine eigene Zuordnung von konkreten Spielinstanzen zu virtuellen Typen und verändert Darstellung, Werte und Sonderverhalten an den dafür vorhandenen Ereignis- und API-Grenzen.

Beispiel:

- Zwei Einheiten sind für Vanilla jeweils `eChimps.CHIMP_TYPE_ARCHER`.
- Nur eine der beiden 1-basierten `unitId`-Instanzen ist als `serp.virtualunits.desert_archer` registriert.
- Diese Instanz verwendet andere Sprites, maximale Lebenspunkte und Geschwindigkeit.
- Alle unveränderten Vanilla-Systeme behandeln sie weiterhin als Bogenschützen.

Das ist keine echte Erweiterung der nativen Unit- oder Building-Tabellen. Es ist eine kontrollierte Abstraktionsschicht, die den sichtbaren und modifizierbaren Teil einer Entität virtualisiert.

## 2. Festgelegte technische Richtung

### 2.1 Vanilla bleibt Eigentümer der Simulation

Vanilla bleibt verantwortlich für grundlegende Funktionen wie:

- Erzeugung, Bewegung, Wegfindung und Kollision
- Auswahl und Befehle
- grundlegende Kampf- und Arbeitsabläufe
- Speicherung der Vanilla-Entitätsdaten
- Netzwerk-Lockstep der Vanilla-Befehle

Der virtuelle Typ ergänzt diese Instanz. Er darf den Basistyp nicht unkontrolliert so stark umdeuten, dass dessen native Abläufe nicht mehr zum gewünschten Verhalten passen. Ein virtueller Fernkämpfer sollte beispielsweise auf einem kompatiblen Vanilla-Fernkämpfer beruhen. Vollständig neue Mechaniken müssen als ausdrückliche Verhaltensmodule implementiert werden.

### 2.2 Virtuelle Identität ist instanzbezogen

Die Zuordnung erfolgt nicht nur nach dem Vanilla-Typ, sondern pro konkreter Instanz. Dadurch können gleichzeitig ein unveränderter Bogenschütze und mehrere unterschiedliche virtuelle Bogenschützen existieren.

Eine laufende Instanz wird durch folgende Werte identifiziert:

- Entitätsart: Unit oder Building
- 1-basierte `unitId` beziehungsweise `buildingId`
- `r_GlobalId` der nativen Struktur
- eindeutige String-ID des virtuellen Typs

Die Game-ID adressiert die aktuelle Arrayposition. Die Global-ID dient als Generationsschutz. Stimmen gespeicherte und aktuelle Global-ID nicht überein, wurde der Slot wiederverwendet; die alte virtuelle Zuordnung muss entfernt und darf niemals auf die neue Entität übertragen werden.

### 2.3 2D-Sprites bleiben die primäre Darstellung

Die beste Zielarchitektur für SHCDE verwendet weiterhin 2D-Sprites. Sie passt zu:

- der bestehenden isometrischen Projektion
- Sprite- und Tile-Sortierung
- Mauern, Gelände und anderen Verdeckungen
- Teamfarbenmasken
- Fußabschneidung bei Wasser oder Gelände
- großen gleichzeitig sichtbaren Unit-Zahlen
- der bestehenden Atlas- und Override-Infrastruktur des Script Extenders

Ein späteres 3D-Werkzeug soll Modelle außerhalb des eigentlichen Spiels in fertige SHCDE-Sprites umwandeln. Als Austauschformat ist glTF/GLB vorzuziehen. Das Werkzeug erzeugt Farbe, Teamfarbenmaske, Frame-Rechtecke, Pivots und Atlasmetadaten. Ob ein Atlas aus 3D, handgezeichneten Einzelbildern oder bestehenden Spielgrafiken stammt, bleibt für die virtuelle Registry unerheblich.

## 3. Aktuell verfügbare Grundlage

Der erste Entwurf basiert ausschließlich auf Script Extender 2.3.0. Wobei hier bereits ein Bugreport existiert damit Assets zukünftig besser implementiert werden können: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/work_items/162

Bereits verwendbare Verträge sind:

- `UnitR3EventHooks.OnUnitUnityVisualSpawn` liefert die 1-basierte `UnitId`, das `GameObject` und den `SpriteRenderer` nach einer visuellen Unit-Aktualisierung.
- `OnUnitUnityVisualRemove`, `OnUnitDelete` und die Map-Unload-Ereignisse ermöglichen die Bereinigung gepoolter beziehungsweise gelöschter Darstellungen.
- `GameUnitManagerAPI.TryGetUnitById`, `SetMaxHealth` und `SetSpeed` arbeiten mit 1-basierten Unit-Game-IDs.
- `spriteLoader.instance` stellt die geladenen `gmSprites`, `gmAltSprites`, `gmMaterials`, `gmColors` und benannten Sprites bereit.
- `BuildingR3EventHooks.OnBuildingSpawn` liefert nach der Erzeugung die Building-ID; `OnBuildingDelete` ermöglicht die Bereinigung.
- `GameBuildingManagerAPI` bietet per-ID-Zugriff auf Building-Strukturen und Lebenspunkte.
- `GameTileManagerAPI.Instance.TileManager.StructureGrid` und `AlphaGFXGrid` legen Building-Zugehörigkeit und den gepackten visuellen Sprite-Deskriptor pro Tile offen.
- `ModSaveDataAPI` kann mod-eigene Daten zusammen mit Spielständen speichern und laden.
- `GameNetworkAPI` bietet für kleine gameplayrelevante Befehle einen tickgenauen Chore-Transport.

Die vorhandenen Atlas-Overrides ersetzen derzeit komplette GM-Gruppen. Für mehrere virtuelle Varianten desselben Basistyps reicht das allein nicht aus, weil die Auswahl pro Entity-ID erfolgen muss.

## 4. Vorgesehene Modstruktur

Der spätere eigenständige Mod trägt den Arbeitsnamen `VirtualUnitsPrototype`. Während der Test- und Debugphase wird seine Version nicht erhöht. Wegen der gameplayrelevanten Zuordnungen und Werte erhält er in `info.json` `NetworkMode=1`.

Die Implementierung wird in folgende Verantwortlichkeiten getrennt:

### 4.1 Definitionen

`VirtualUnitDefinition` enthält mindestens:

- eindeutige `VirtualTypeId`
- `BaseUnitType` als `eChimps`
- `UnitVisualProfile`
- optionale maximale Lebenspunkte
- optionale Geschwindigkeit
- Liste registrierter Unit-Verhaltensmodule
- Definitionsversion für Save-Kompatibilität

`VirtualBuildingDefinition` enthält mindestens:

- eindeutige `VirtualTypeId`
- `BaseBuildingType` als `eStructs`
- `BuildingVisualProfile` einschließlich erwarteter Grundfläche
- optionale maximale Lebenspunkte
- optionale Kosten- und Produktionsdefinition
- Liste registrierter Building-Verhaltensmodule
- Definitionsversion für Save-Kompatibilität

Fachliche Unit-, Building-, GM-, Goods- oder Mapperwerte werden im C#-Code über die benannten Enums von Script Extender 2.3.0 referenziert. Konfigurationsdateien verwenden Enum-Namen als Strings und werden beim Laden gegen die tatsächlich referenzierte Assembly validiert. Unbekannte Namen führen zur Ablehnung der betroffenen Definition, nicht zu einem numerischen Fallback.

### 4.2 Definitionsregistry

Eine unveränderliche Registry verwaltet Definitionen nach `VirtualTypeId`. Beim Aufbau gelten folgende Regeln:

- IDs sind ordinal und ohne Beachtung der Groß-/Kleinschreibung eindeutig.
- Basistyp, Visualprofil und Definitionsversion sind Pflichtangaben.
- Unit- und Building-Definitionen dürfen keine gemeinsame ID verwenden.
- Ein Visualprofil muss alle für seinen Modus notwendigen Ressourcen auflösen können.
- Fehlerhafte Definitionen werden mit Zeitstempel und Ursache protokolliert und nicht teilweise registriert.

Der erste Prototyp darf seine einzelne Definition direkt im Code anlegen. Das spätere Dateiformat wird erst eingeführt, wenn der Registryvertrag durch den Test bestätigt ist; dadurch wird kein instabiles öffentliches JSON-Schema vorzeitig festgeschrieben.

### 4.3 Instanzregistry

`VirtualEntityInstance` speichert:

- Entitätsart
- 1-basierte Game-ID
- beim Registrieren gelesene Global-ID
- `VirtualTypeId`
- Definitionsversion

Units und Gebäude liegen in getrennten Dictionaries, damit gleichlautende Game-IDs nicht kollidieren. Jeder Zugriff liest die aktuelle native Struktur über die passende `TryGet...ById`-API und vergleicht die Global-ID und den Vanilla-Basistyp mit der Definition. Bei einer Abweichung wird der Eintrag sofort verworfen.

Die Registry stellt ausdrücklich folgende Operationen bereit:

- Definition registrieren
- virtuelle Instanz zuweisen
- gültige Zuweisung abfragen
- Zuweisung entfernen
- alle Zuweisungen beim Map-Unload leeren
- serialisierbaren Snapshot erzeugen
- Snapshot validiert wiederherstellen

## 5. Unit-Darstellung

### 5.1 Visualprofil

Ein `UnitVisualProfile` beschreibt für den ersten Test:

- Ziel-GM als `Enums.GM`
- optionalen expliziten Frame-Mapper
- Verhalten bei fehlendem Zielframe

Der Standardmapper übernimmt den numerischen Frameindex und unterscheidet normale und alternative Frames. Die Information wird aus dem vom Vanilla-Aufruf bereits gesetzten Quell-Sprite beziehungsweise aus den GM-Arrays ermittelt. String-Manipulation ist nur eine kontrollierte Rückfallebene; die Zuordnung soll vorrangig über Arrayidentität und geprüfte Grenzen erfolgen.

### 5.2 Anwendung pro Renderer-Aktualisierung

Beim Ereignis `OnUnitUnityVisualSpawn` wird in dieser Reihenfolge gearbeitet:

1. `UnitId` als 1-basierte Game-ID behandeln und die aktuelle `GameUnit` über `TryGetUnitById` auflösen.
2. Registryeintrag gegen `r_GlobalId` und `r_UnitChimp` validieren.
3. Aktuelles Quell-Sprite, Alt-Frame-Zustand, Farbe, Transparenz und `_SpriteCutoff` des Renderers erfassen.
4. Den entsprechenden Frame innerhalb des Ziel-GM suchen.
5. Nur bei vollständig gültigem Zielframe Sprite und passendes Material setzen.
6. Rendererfarbe, Transparenz, Sortierung und Fußabschneidung beibehalten.
7. Bei einem fehlenden Frame den von Vanilla gesetzten Zustand unverändert lassen und die Kombination aus virtuellem Typ und Frame höchstens einmal protokollieren.

Das Zielmaterial stammt aus `spriteLoader.instance.gmMaterials` des Ziel-GM. Der zur aktuellen Fußabschneidung passende Materialslot wird über den vorhandenen `_SpriteCutoff`-Wert gewählt. Gemeinsame Vanilla-Materialien werden nicht verändert. Falls ein eigener Atlas später ein eigenes Material benötigt, erzeugt der Mod gecachte, immutable Materialvarianten statt pro Renderer neue Instanzen anzulegen.

Rendererzustand wird nur so lange gehalten, wie dies für Object-Pooling und Wiederherstellung erforderlich ist. Schlüssel sind Unity-Instance-ID plus validierte Unit-Global-ID. `OnUnitUnityVisualRemove`, `OnUnitDelete` und Map-Unload räumen die Einträge auf.

## 6. Unit-Werte und Verhalten

Bei der Zuweisung eines virtuellen Unit-Typs werden nur ausdrücklich definierte per-ID-Werte gesetzt:

- `SetMaxHealth(unitId, value)`
- `SetSpeed(unitId, value)`
- gegebenenfalls Anpassung der aktuellen Lebenspunkte nach einer dokumentierten Preserve-Ratio-Regel

Für den Prototyp bleibt der aktuelle Lebenspunkteanteil erhalten: `newCurrent = round(oldCurrent / oldMax * newMax)`, begrenzt auf `1..newMax` für eine lebende Einheit. Beim Entfernen einer Testzuweisung werden die bei der Zuweisung gespeicherten ursprünglichen Werte nur dann wiederhergestellt, wenn Game-ID und Global-ID noch übereinstimmen.

Spätere Verhaltensmodule werden nach Fähigkeit statt als große Typabfrage organisiert, beispielsweise:

- eingehender oder ausgehender Schaden
- Kosten und Rekrutierung
- Spezialfähigkeit
- Arbeits- oder Produktionsereignis
- periodischer Effekt

Jeder Hook fragt die beteiligten Game-IDs in der Registry ab. Verhalten darf niemals allein aufgrund des Vanilla-Basistyps auf alle Instanzen angewendet werden.

## 7. Erster ausführbarer Proof of Concept

Der erste Code-Meilenstein benötigt kein eigenes 3D-Modell, keinen Unity Editor und keine Änderung am Script Extender.

### 7.1 Testdefinition

Der virtuelle Typ heißt `serp.virtualunits.desert_archer`:

- Vanilla-Basistyp: `eChimps.CHIMP_TYPE_ARCHER`
- Zielgrafik: `Enums.GM.GM_BODY_ARAB_BOW`
- eigene maximale Lebenspunkte: ein benannter Prototyp-Konfigurationswert, kein fachlicher Enum-Ersatz
- eigene Geschwindigkeit: ein benannter Prototyp-Konfigurationswert

Die konkreten Testwerte werden vor Implementierung gegen Vanilla-Werte gelesen und so gewählt, dass der Unterschied sichtbar, aber nicht extrem ist. Sie gehören zur Testkonfiguration und werden mit ihrem Zweck kommentiert.

### 7.2 Testablauf

1. In einer Einzelspieler-Testkarte werden zwei normale Vanilla-Bogenschützen erzeugt.
2. Der Spieler wählt genau einen davon aus.
3. Ein klar protokollierter Diagnose-Hotkey weist ausschließlich der ausgewählten Unit den virtuellen Typ zu beziehungsweise entfernt ihn wieder.
4. Bei Mehrfachauswahl, falschem Vanilla-Typ, ungültiger ID oder Multiplayer wird die Aktion ohne Zustandsänderung abgelehnt.
5. Der virtuelle Bogenschütze verwendet, soweit indexkompatibel, die vorhandenen `GM_BODY_ARAB_BOW`-Frames und erhält eigene Lebenspunkte und Geschwindigkeit.
6. Der zweite Bogenschütze bleibt vollständig unverändert.

Der erste Prototyp sperrt die Hotkey-Zuweisung im Multiplayer. Die Sperre ist fail-closed und wird erst entfernt, nachdem der synchronisierte Zuweisungspfad implementiert und getestet wurde.

### 7.3 Erfolgskriterium

Der Kernnachweis ist erbracht, wenn zwei native Bogenschützen gleichzeitig existieren und nur die registrierte Game-/Global-ID-Kombination über Bewegung, Angriff, Auswahl, Schaden und Renderer-Pooling hinweg die alternative Darstellung und Werte behält.

## 8. Speichern und Laden

Der Mod registriert einen eindeutigen Handler bei `ModSaveDataAPI`. Gespeichert werden ausschließlich virtuelle Instanzzuordnungen und die zu ihrer Interpretation nötige Schema-/Definitionsversion. Definitionen selbst stammen weiterhin aus dem installierten Mod.

Beim Laden gilt:

1. Daten mit MessagePack deserialisieren und Schema prüfen.
2. Virtuellen Typ in der aktuellen Definitionsregistry suchen.
3. Game-ID als 1-basiert behandeln und native Entität auflösen.
4. Global-ID und Vanilla-Basistyp vergleichen.
5. Nur vollständig passende Einträge wiederherstellen.
6. Fehlende oder inkompatible Definitionen protokollieren und die Vanilla-Entität unverändert lassen.

Beim Map-Unload werden alle Laufzeit- und Renderer-Caches geleert. Geladene Save-Daten dürfen erst nach Verfügbarkeit der nativen Entitäten angewendet werden.

## 9. Multiplayer

Virtuelle Identität und gameplayrelevante Werte müssen auf allen Teilnehmern identisch sein. Deshalb gelten folgende Regeln:

- Der Mod ist `NetworkMode=1` und muss in gleicher Version installiert sein.
- Deterministisch aus einem bereits synchronisierten Vanilla-Ereignis abgeleitete Zuweisungen benötigen kein zusätzliches Paket, müssen aber auf allen Clients denselben Codepfad ausführen.
- Zuweisungen aus UI, Hotkeys oder anderen lokalen Eingaben werden als kleines MessagePack-Paket über `SendPacketToAllEx2(..., viaChore: true)` gesendet.
- Der Sender verändert den Zustand nicht vor Empfang seines eigenen Chore-Pakets.
- Das Paket enthält virtuelle Typ-ID, Entitätsart, Game-ID und Global-ID; alle Clients validieren denselben Basistyp.
- Ist der Chore-Transport nicht verfügbar oder fällt auf nicht tickgenaue Übertragung zurück, wird eine gameplayrelevante Zuweisung abgelehnt.
- Große Grafik- oder Definitionsdaten werden niemals während des Spiels übertragen. Ihre Identität wird in der Lobby über Modversion beziehungsweise später über einen Definitionshash abgesichert.

## 10. Virtuelle Gebäude

### 10.1 Logische Registry

Gebäude verwenden denselben Identitätsvertrag aus 1-basierter `buildingId`, `r_GlobalId`, virtuellem Typ und Vanilla-Basistyp. Spawn, Delete, Save/Load und Multiplayerzuweisung entsprechen konzeptionell dem Unit-Pfad.

Ein virtuelles Baumenüelement erzeugt weiterhin einen festgelegten Vanilla-Basistyp. Die virtuelle Auswahl wird bis zum zugehörigen `OnBuildingSpawn`-Post-Ereignis als synchronisierter Erzeugungsauftrag geführt und anschließend an die zurückgegebene Building-ID plus Global-ID gebunden. Ein Auftrag muss Spieler, Basistyp, Position und Tick ausreichend validieren, damit parallele Bauvorgänge nicht verwechselt werden.

### 10.2 Werte und Verhalten

Per-ID-Lebenspunkte können über `GameBuildingManagerAPI` angepasst werden. Kosten, Produktion, Pause, Reparatur, Refund und Schaden werden über die jeweils vorhandenen Building-Ereignisse modulweise überschrieben. Auch hier darf keine globale Änderung des Vanilla-Basistyps erfolgen.

Die Möglichkeiten eines virtuellen Gebäudes bleiben zunächst an einen kompatiblen Basistyp gebunden. Ein vollkommen neues Produktionssystem ist ein eigenes Verhaltensmodul und nicht Teil des visuellen Overrides.

### 10.3 Visuelle Machbarkeitsprüfung

Script Extender 2.3.0 bietet noch kein dem Unit-Visual-Spawn entsprechendes per-Building-Visualereignis. Deshalb wird vor jeder produktiven Building-Grafikänderung ein eigener Diagnosemeilenstein durchgeführt:

1. Für eine Testinstanz die von `StructureGrid` zugeordneten Tiles ermitteln.
2. Änderungen des `AlphaGFXGrid` bei Spawn, Konstruktion, Fertigstellung, Schaden, Reparatur, Animation und Löschung protokollieren.
3. Den verwalteten Aufrufer ermitteln, der diese visuellen Deskriptoren aktualisiert.
4. Nachweisen, dass ein Post-Hook die Sprite-Deskriptoren ausschließlich der Tiles einer bestimmten Building-ID ersetzen kann.
5. Kontrollieren, dass Logic-, Structure-, Pathfinding-, Ownership- und Damage-Grids unverändert bleiben.

Wird ein sicherer verwalteter Aktualisierungspunkt bestätigt, installiert ausschließlich der Mod einen Post-Hook und wendet anschließend das `BuildingVisualProfile` der betreffenden Building-ID an. Originaldeskriptoren werden für Wiederherstellung und Fehlerrollback gespeichert.

Kann dieser Vertrag nicht sicher belegt werden, endet die Building-Visualphase fail-closed. Es werden keine geratenen RVAs, unbestätigten AOBs oder direkten nativen Detours verwendet. Stattdessen wird ein kurzer englischer Markdown-Report für den Script-Extender-Autor erstellt, der ein per-Building-Visual-Refreshereignis mit Building-ID und betroffenen Tiles vorschlägt.

## 11. Spätere Atlas-Baker-Pipeline

Erst nachdem Registry, per-ID-Darstellung, Save-Daten und Synchronisierung stabil sind, wird die 3D-zu-Sprite-Pipeline getrennt geplant.

Vorgesehener Vertrag:

- Eingang: riggtes glTF/GLB-Modell, Animationen und ein deklaratives Mapping auf Vanilla-Aktions- und Richtungsframes
- Verarbeitung außerhalb des Spiels mit reproduzierbarer Kamera, Beleuchtung, Hintergrundtransparenz und Auflösung
- Ausgang: `atlas.png`, optionale `atlas_m.png`, `atlas.json` und ein Visualprofil
- feste Fußpunkt-/Pivot-Regeln und `pixelsPerUnit`
- Validierung sämtlicher erwarteter Frames, Atlasgrenzen und Maskendimensionen
- reproduzierbarer Inhalts-Hash für Cache und Multiplayerprüfung

Ein einmal erzeugter Atlas wird mit dem Mod ausgeliefert. Endnutzer müssen weder das 3D-Modell importieren noch beim Spielstart Tausende Frames rendern. Ein späterer komfortabler Script-Extender-Workflow kann denselben Ausgabecontract automatisieren, ohne Registry oder Laufzeitverhalten zu ändern.

## 12. Testplan

### 12.1 Automatisierte Registrytests

- 1-basierte Unit- und Building-IDs werden unverändert an ID-APIs übergeben.
- Direkter Span-Zugriff verwendet genau einmal `gameId - 1`.
- Doppelte virtuelle Typ-IDs werden abgelehnt.
- Unbekannte virtuelle Typen und falsche Basistypen werden abgelehnt.
- Eine abweichende Global-ID entfernt einen veralteten Eintrag.
- Unit- und Building-ID mit gleichem Zahlenwert kollidieren nicht.
- Map-Unload leert Instanzen und Rendererzustand, nicht aber Definitionen.
- Save-Daten mit unbekannter Schema- oder Definitionsversion verändern Vanilla nicht.

### 12.2 Automatisierte Visualtests

- Normaler Frame wird auf denselben Index des Ziel-GM abgebildet.
- Alt-Frame bleibt Alt-Frame.
- Fehlender, nuller oder außerhalb liegender Zielframe lässt Vanilla unverändert.
- Zielmaterial wird ohne Mutation eines gemeinsamen Vanilla-Materials gewählt.
- Farbe, Alpha, Sorting-Layer, Sorting-Order, Flip- und Cutoff-Zustand bleiben erhalten.
- Ein gepoolter Renderer erhält keine Grafik der vorherigen Unit-ID.
- Wiederholte Fehler werden pro Typ/Frame nur einmal geloggt.

### 12.3 Spieltests für Units

- Zwei Bogenschützen desselben Vanilla-Typs nebeneinander vergleichen.
- Nur einen davon als Desert Archer zuweisen und wieder zurücksetzen.
- Stillstand, alle Bewegungsrichtungen, Angriff, Trefferreaktion, Tod und Auswahl prüfen.
- Wasser-/Fußabschneidung und mehrere Spielerfarben prüfen.
- Unit löschen, denselben nativen Slot wiederverwenden lassen und auf Registry-Leak prüfen.
- Speichern, Spiel beenden, laden und Identität sowie Werte prüfen.
- Map wechseln und vollständige Bereinigung im Log bestätigen.

### 12.4 Spieltests für Gebäude

- Zwei Gebäude desselben Vanilla-Basistyps errichten und nur eines virtuell registrieren.
- Bauphase, Fertigstellung, Animation, Schaden, Reparatur, Pause und Abriss prüfen.
- Nachweisen, dass ausschließlich Tiles mit der geprüften Building-ID visuell geändert werden.
- Nachbargebäude, Pfadfindung, Belegung, Eigentum und Logikgrids bitgenau unverändert lassen.
- ID-Wiederverwendung sowie Save/Load prüfen.

### 12.5 Multiplayer-Freigabe

- Identische Definitionen und Modversionen in der Lobby erzwingen.
- Zuweisungen ausschließlich über den Chore-Empfang anwenden.
- Host und Client müssen am selben Tick dieselbe Game-/Global-ID-Zuordnung protokollieren.
- Rekrutierung, Bau, Save/Load und erneute Verbindung in einer kontrollierten Partie prüfen.
- Bei Transportfallback, Definitionshash-Abweichung oder ungültiger ID keine Simulation verändern.

## 13. Umsetzungsreihenfolge

1. Unit-Definitionen, Instanzregistry und Validierung implementieren.
2. Lebenszyklus- und Map-Unload-Bereinigung anbinden.
3. Zwei-Bogenschützen-Hotkeytest mit per-ID-Werten implementieren.
4. Per-ID-Sprite- und Materialersetzung einschließlich Object-Pooling abschließen.
5. Save-/Load-Vertrag ergänzen und testen.
6. Chore-basierte Zuweisung implementieren und erst danach Multiplayer freigeben.
7. Unit-Verhaltensmodule schrittweise ergänzen.
8. Building-Registry, Save-Daten und Wertepfad implementieren.
9. Building-Visualdiagnose durchführen und nur bei belegtem Vertrag fortsetzen.
10. Rekrutierungs- und Baumenüadapter entwickeln.
11. Getrennte glTF/GLB-Atlas-Baker-Pipeline spezifizieren und implementieren.

## 14. Abgrenzungen und Annahmen

- Es werden keine neuen nativen `eChimps`-, `eStructs`- oder GM-Werte eingeführt.
- Der erste Proof of Concept implementiert Units, nicht bereits die vollständige Building-Grafikpipeline.
- Der erste Test verwendet vorhandene Vanilla-Grafiken und benötigt weder 3D-Modell noch Unity Editor.
- Der Script Extender bleibt unverändert und wird nicht als stillschweigend modifizierbare Abhängigkeit behandelt.
- Der Mod funktioniert mit seinen deklarierten Abhängigkeiten eigenständig und setzt keinen anderen Workspace-Testmod voraus.
- Unbelegte native Hooks sind ausgeschlossen. Native Erkenntnisse müssen zur kanonischen DLL mit SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` und zur Script-Extender-Zielversion 2.3.0 passen.
- Versionen werden während der Test- und Debugphase nicht erhöht.
- Nach einem final abgeschlossenen Feature wird vor einer README-Ergänzung ausdrücklich beim Benutzer nachgefragt.

## 15. Erwartetes Endergebnis

Das fertige System soll Mods erlauben, mehrere sichtbar und spielerisch unterschiedliche virtuelle Entitäten auf kompatiblen Vanilla-Basistypen aufzubauen. Vanilla behält seine stabilen Datenstrukturen und Kernabläufe; der Mod entscheidet anhand der konkreten Game-/Global-ID-Kombination, welche zusätzlichen Grafiken, Werte und Verhaltensmodule gelten.

Der erste Unit-Prototyp beweist genau diese kritische Eigenschaft. Gebäude und aus 3D-Modellen erzeugte Grafiken werden danach auf denselben Identitäts- und Registryvertrag aufgesetzt, statt eigene inkompatible Systeme zu bilden.
