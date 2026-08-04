# Roadmap: AIVJSON vor dem Mapstart auf eine Startposition prüfen

## Zielbild

Langfristig soll bereits in der Skirmish-Lobby für jede Kombination aus

- ausgewählter `.map`-Datei,
- Lobby-/Spieler-Slot beziehungsweise Keep-Position,
- KI beziehungsweise ausgewählter `.aivjson`,
- und gegebenenfalls AIV-Rotation

bestimmt werden, ob die geplante Burg vollständig auf die Karte passt.

Projektweite Rotationsinvariante: Die ausgewählte AIV-Rotation gilt zugleich
für den realen Keep und den daran gekoppelten Startkomplex einschließlich des
5×5-Vorratslagers. Der feste Ursprung der nativen 100×100-Fit-Grids ist nur eine
Koordinatenregel. Er erlaubt keine vom Keep unabhängige AIV-Rotation.

Die Offline-Prüfung soll möglichst dieselben Ergebnisse wie die native
Spielprüfung liefern, darf dafür aber keine bereits geladene Karte voraussetzen.
Die Lobby soll mindestens zwischen folgenden Ergebnissen unterscheiden können:

- `Complete`: Alle relevanten AIV-Elemente können vollständig platziert werden.
- `Partial`: Nur ein Teil der AIV kann platziert werden.
- `Impossible`: Die AIV besitzt an dieser Position keine sinnvoll platzierbare
  Variante.
- `NotEvaluable`: Karte, Sections oder AIV-Variante können offline nicht sicher
  ausgewertet werden.

Diese Statusnamen sind zunächst unser eigenes Modell. Ob sie exakt den nativen
Werten `placementState = 2/1/0` entsprechen, wird erst durch den späteren
Oracle-Vergleich festgelegt.

Verbindliche Phasengrenze: Die erste produktive Ausbaustufe wird vollständig
für deaktivierten Sofortspawn (`advopt_pre_build=0`) fertiggestellt. Dazu
gehören Offline-Kern, Lobby-Datenfluss, Cache und sichtbare UI. Bei aktiviertem
Sofortspawn darf diese Stufe keine scheinbar exakten Ergebnisse für abhängige
Spieler erfinden; sie liefert dort ausdrücklich `NotEvaluable` mit verständlicher
Begründung. Erst nachdem die No-PreBuild-Ausbaustufe abgeschlossen ist, wird das
System in einer eigenen späteren Phase um die sequenzielle native
Sofortspawn-Ausführung erweitert.

## Bereits vorhandene Grundlage

Der derzeitige Workspace enthält:

- `MapParser`: liest `.map`, Metadaten, U4-Radarpositionen und Placement-Layer
  strikt und read-only.
- `AIVParser`: liest `.aivjson`, Build-Reihenfolge, Keep-Anker, Rotationen,
  Footprints und bekannte zugehörige Blockierflächen.
- `ActiveAIVDetector`: besitzt bereits Erkenntnisse zum aktiven AIV-Kandidaten
  und zum nativen `placementState`.
- `SpawnCastle`: enthält praktische Erfahrung mit AIV-Projektion und
  Gebäudeplatzierung im laufenden Spiel.

Noch nicht vorhanden sind der exakte Offline-Keep-Tile-Anker, die
AIV-Projektion, die eigentlichen Vanilla-Fit-Regeln und die Lobby-Anbindung.

## Arbeitsregeln für die folgenden Chats

Jeder nummerierte Schritt ist als eigener Chat gedacht. Ein Schritt gilt erst
als abgeschlossen, wenn seine Abnahmekriterien erfüllt und die zugehörigen
Tests erfolgreich sind.

Der Status an jeder Chat-Überschrift ist verbindlich. Bei der Anweisung
„Führe den nächsten Schritt aus“ wird der mit **Nächster Schritt** markierte
Chat bearbeitet. Bei erfolgreicher Abnahme muss derselbe Chat außerdem seinen
Status auf **Abgeschlossen** setzen und genau den unmittelbar folgenden Chat als
**Nächster Schritt** markieren. Es darf höchstens einen solchen Status geben. Aktualisiere auch die letzte Zeile dieser Roadmap.
Notwendige Arbeitsphasen dürfen nicht als unnummerierte Zwischenschritte zwischen
zwei Chats stehen bleiben.

- Keine nachfolgenden Phasen vorwegnehmen, wenn sie zur aktuellen Abnahme nicht
  erforderlich sind.
- Neue Annahmen über Koordinaten, Flags oder native Semantik zuerst belegen.
- Proprietäre Karten und AIV-Dateien nicht ins Repository kopieren.
- Reale Dateien dürfen für lokale Integrationstests verwendet werden;
  automatisierte Repository-Tests verwenden synthetische Fixtures.
- Native Analyse zunächst gezielt mit Funktion, Xrefs und Decompiler durchführen.
- Der Offline-Kern bleibt paketfrei und frei von Unity-/BepInEx-Abhängigkeiten.
- Diagnose-/Oracle-Code und produktive Offline-Regeln klar voneinander trennen.
- Nach jedem Code-Schritt CRLF prüfen und das passende `build.bat` ausführen.

---

## Chat 1: Koordinatensysteme und native Row-LUT belegen

**Status:** Abgeschlossen. Die Ergebnisse und nativen Nachweise stehen in
`Docs/MAP_COORDINATE_SYSTEM.md`; die Testvektoren in
`MapParser.Tests/Fixtures/MapTileGeometryVectors.json`.

**Plan-Korrektur aus der Analyse:** U4 enthält 200×200-Radarpositionen für die
Lobbydarstellung, keine nativen Keep-Tile-Koordinaten. Die linearen Sections
verwenden unabhängig von der World Size eine feste 800-Zeilen-Raute mit 320800
Tiles. Chat 4 muss deshalb den echten Tile-Anker jedes Keep-Slots offline
belegen, bevor Chat 5 mit der AIV-Projektion beginnt.

### Ziel

Eindeutig bestimmen, wie Map-Koordinaten auf die linearen Tile-Layer abgebildet
werden. Es darf keine unbestätigte Formel wie `y * width + x` verwendet werden.

### Zu klärende Koordinatenräume

- Keep-Positionen aus dem U4-Metadatenblock der `.map`
- lineare IDs der Map-Sections
- native Kartenkoordinaten des Spiels
- AIV-Grid mit 100×100 Positionen
- Keep-Anker und rotierte AIV-Offsets
- mögliche Unterschiede zwischen Kartenrand, sichtbarer Welt und internem Grid

### Vorgehen

1. In `CrusaderDE.dll` gezielt die native Row-LUT beziehungsweise die
   XY→Tile-ID-Funktion identifizieren.
2. Tabellenlänge, Zeilenanfänge, Kartenmaß und Grenzbedingungen dokumentieren.
3. Prüfen, ob unterschiedliche World Sizes dieselbe LUT mit anderen Grenzen
   oder unterschiedliche Tabellen verwenden.
4. Nach echtem Mapstart einige kontrollierte Koordinaten gegen native Daten oder
   eine geeignete Spiel-/Script-Extender-Funktion vergleichen.
5. Testvektoren für mindestens folgende Fälle festhalten:
   - Kartenmittelpunkt
   - erste und letzte gültige Position mehrerer Zeilen
   - vier Randbereiche
   - Keep-Positionen aus realen 160-/200-/300-/400-/800-Karten
   - negative und außerhalb liegende Koordinaten

### Ergebnisartefakte

- `MapParser/Docs/MAP_COORDINATE_SYSTEM.md`
- `MapParser/MapParser.Tests/Fixtures/MapTileGeometryVectors.json` oder eine
  äquivalente synthetische, nicht proprietäre Testdarstellung
- dokumentierte native Funktionsadresse/Symbole und Herkunft der Formel

### Abnahme

- Jede verwendete Koordinate und Achsenrichtung ist beschrieben.
- Die XY→Tile-ID-Regel ist nicht nur vermutet, sondern nativ oder durch
  kontrollierte Laufzeitmessung bestätigt.
- Testvektoren decken gültige und ungültige Kartenbereiche ab.

### Noch nicht Teil dieses Chats

- keine Placement-Regeln
- keine AIV-Footprint-Prüfung
- keine Lobby-Änderung

### Startprompt

> Bearbeite Chat 1 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Untersuche und
> dokumentiere die SCDE-Koordinatensysteme sowie die native XY→Tile-ID-Row-LUT.
> Implementiere noch keine Placement-Regeln.

---

## Chat 2: `MapTileGeometry` implementieren

**Status:** Abgeschlossen. `MapTileGeometry` und die nativen Testvektoren sind
implementiert; der MapParser-Build ist erfolgreich.

### Ziel

Die in Chat 1 bestätigte Transformation paketfrei in `MapParser.Core`
implementieren.

### Vorgesehene API

Die endgültigen Namen dürfen anhand der Erkenntnisse aus Chat 1 angepasst
werden. Erwartet wird ungefähr:

    public sealed class MapTileGeometry
    {
        public int TileCount { get; }
        public bool IsValidCoordinate(int x, int y);
        public bool TryGetTileId(int x, int y, out int tileId);
        public int GetTileId(int x, int y);
        public bool TryGetCoordinate(int tileId, out MapCoordinate coordinate);
    }

### Anforderungen

- Keine Unity- oder native Laufzeitabhängigkeit.
- Keine stillschweigende Begrenzung ungültiger Koordinaten.
- Vorwärts- und Rücktransformation müssen konsistent sein.
- Geometrie muss aus belegten Mapdaten beziehungsweise Formatparametern
  aufgebaut werden, nicht aus Dateinamen.
- Klare Fehler bei inkonsistentem TileCount oder nicht unterstützter Geometrie.

### Tests

- alle Testvektoren aus Chat 1
- Roundtrip `(x,y) → tileId → (x,y)`
- erste/letzte Tile-ID
- Zeilenübergänge
- ungültige Koordinaten und Tile-IDs
- alle unterstützten Kartengrößen

### Abnahme

- `MapParser/build.bat` ist erfolgreich.
- Alle nativen Testvektoren stimmen exakt.
- `MapTileGeometry` enthält noch keine AIV- oder Gebäuderegeln.

### Startprompt

> Bearbeite Chat 2 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere die
> in Chat 1 belegte XY→Tile-ID-Abbildung als `MapTileGeometry` in
> `MapParser.Core` und ergänze die beschriebenen Tests.

---

## Chat 3: `MapPlacementSnapshot` aufbauen

**Status:** Abgeschlossen. Der immutable Snapshot führt Geometrie und acht
Placement-Layer zusammen; der MapParser-Build und alle synthetischen Tests sind
erfolgreich.

### Ziel

Die für Placement benötigten Map-Sections über Tile-ID und Koordinate als eine
einheitliche, immutable Offline-Ansicht bereitstellen.

### Vorgesehene Modelle

Ungefähr:

    public sealed class MapPlacementSnapshot
    {
        public MapTileGeometry Geometry { get; }
        public int TileCount { get; }
        public MapPlacementTile GetTile(int tileId);
        public MapPlacementTile GetTile(int x, int y);
        public bool TryGetTile(int x, int y, out MapPlacementTile tile);
    }

    public readonly struct MapPlacementTile
    {
        public int TerrainFlags { get; }
        public byte SecondaryLogic { get; }
        public byte Height { get; }
        public byte DefaultHeight { get; }
        public ushort OrganismId { get; }
        public ushort BuildingId { get; }
        public ushort EntityId { get; }
        public byte OwnerId { get; }
    }

### Anforderungen

- Nur die benötigten lazy Sections werden dekomprimiert.
- Alle Layer müssen denselben belegten TileCount besitzen.
- `SectionsUnavailable` und unvollständige Layer-Sätze ergeben einen klaren
  `NotEvaluable`-Fehler beziehungsweise eine passende Exception.
- Keine Interpretation einzelner Flagbits in diesem Schritt.
- Keine veränderbaren Arrays nach außen geben.

### Tests

- synthetischer vollständiger Layer-Satz
- fehlende Section
- unterschiedliche Layerlängen
- alte und neue Section-IDs
- Zugriff per Tile-ID und per `(x,y)`
- ungültige Koordinaten
- weiterhin lazy Dekodierung

### Abnahme

- Snapshot und Geometrie liefern an denselben Koordinaten konsistente Werte.
- MapParser-Build und Tests sind erfolgreich.
- Noch keine Aussage, ob ein konkretes Gebäude platziert werden darf.

### Startprompt

> Bearbeite Chat 3 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere einen
> immutable `MapPlacementSnapshot`, der Geometrie und die acht Placement-Layer
> konsistent zusammenführt. Interpretiere die Flags noch nicht.

---

## Chat 4: Keep-Tile-Anker offline belegen

**Status:** Abgeschlossen. Der exakte Anker stammt aus dem lebenden
Keep-Gebäudedatensatz in Section 1013 oder der aktuellen erweiterten Section
4013. Native Herkunft, Feldlayout, Offline-/Runtime-Identität und
Realmap-Vektoren für 160/200/300/400/800 stehen in
`Docs/MAP_KEEP_TILE_ANCHORS.md`; die paketfreie API liefert pro Slot `Exact`
oder einen expliziten `NotEvaluable`-Grund.

### Grund

Chat 1 hat gezeigt, dass die U4-Werte nur Positionen auf der 200×200-Radaransicht
sind. Sie sind gerastert und nicht verlustfrei auf native Tiles umkehrbar.
Chat 5 darf deshalb nicht mit U4 als `keepPosition` beginnen.

### Ziel

Für jeden auswählbaren Keep-Slot vor dem Mapstart den exakten nativen
Tile-Anker bestimmen oder explizit nachweisen, dass dies für eine Kartenvariante
offline nicht möglich ist.

### Vorgehen

- Native Maplade-/Keep-Erzeugung gezielt vom U4-Puffer bis zum tatsächlichen
  `GamePlayerResources.r_KeepTilePositionX/Y` verfolgen.
- Prüfen, ob der Anker aus einer Map-Section, einem Marker, einer Struktur oder
  einer deterministischen nativen Transformation stammt.
- Reale 160-/200-/300-/400-/800-Karten nach Mapstart kontrolliert vergleichen:
  U4-Radarposition, ermittelter Offline-Anker und Runtime-Keep-Tile.
- Synthetische Fixtures für die belegte Offline-Datenquelle ergänzen.
- Keine Näherung aus Radar-Pixeln als exakten Tile-Anker ausgeben.

### Abnahme

- Pro Keep-Slot existiert ein exakter `MapCoordinate`-Anker oder ein klarer
  `NotEvaluable`-Grund.
- Mehrere reale Karten und World Sizes stimmen offline und zur Laufzeit exakt
  überein.
- Chat 5 erhält ausschließlich belegte native Tile-Koordinaten.

### Startprompt

> Bearbeite Chat 4 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Ermittle und belege
> für jeden auswählbaren Keep-Slot den exakten nativen Tile-Anker vor dem
> Mapstart. Verwende U4-Radarpositionen nicht als Näherung und beginne noch nicht
> mit der AIV-Projektion.

---

## Chat 5: AIV-Projektion als unabhängigen Offline-Kern entwickeln

**Status:** Abgeschlossen. Der paketfreie `AIVPlacement`-Kern projiziert
Build-Schritte, alle vier Rotationen, Footprints und bekannte Zusatzflächen
deterministisch auf absolute Map-Koordinaten. Der native Fit rotiert das
100×100-Grid um seinen festen Orientierung-0-Ursprung, nicht um den AIV-Keep;
diese Semantik stimmt auf einer randnahen Karte in allen vier Rotationen exakt.
Der Release-Build und alle synthetischen Tests sind erfolgreich.

### Ziel

Eine AIV vollständig relativ zu einer ausgewählten Keep-Position und Rotation
auf Map-Koordinaten projizieren, zunächst ohne zu entscheiden, ob die Tiles
baubar sind.

### Projektgrenze

Empfohlen wird eine eigene paketfreie Solution beziehungsweise ein Projekt wie:

    AIVPlacement/
      AIVPlacement.Core
      AIVPlacement.Tests
      build.bat

`AIVPlacement.Core` referenziert `AIVParser.Core` und `MapParser.Core`, aber
keine Unity-/BepInEx-Assembly.

### Zu projizierende Inhalte

- jedes Gebäude in ursprünglicher Build-Reihenfolge
- Keep-Anker
- alle vier Rotationen
- Gebäude-Footprints
- bekannte zugehörige Blockierflächen
- Mauern und niedrige Mauern
- Tore und zugehörige Zugbrücken
- Treppenstücke
- Pfad-/Mehrfach-Tile-Elemente
- Pausen oder Nicht-Placement-Einträge müssen erkennbar bleiben, dürfen aber
  keine Tiles belegen

### Vorgesehene Ausgabe

Zum Beispiel:

    AivProjectedCastle
    AivProjectedElement
    AivProjectedTile

Jedes Element sollte mindestens Originalindex, Build-Schritt, Mappertyp,
Rotation, AIV-Koordinaten, absolute Map-Koordinaten und belegte Tiles enthalten.

### Kritische Klärungen

- Welche AIV-Koordinate ist der tatsächliche Keep-Anker?
- Auf welchen Teil eines Footprints zeigt der gespeicherte AIV-Punkt?
- Welche Blockierflächen gehören zum Element, obwohl dort kein Kerngebäude
  entsteht?
- Wie werden Teile behandelt, die bewusst außerhalb des Kartenbereichs landen?

### Tests

- alle vier Rotationen
- mehrere bekannte asymmetrische Footprints
- Keep am Kartenmittelpunkt
- Keep nahe Kartenrand
- Tore, Zugbrücken und Treppen
- zwei AIV-Elemente mit überlappenden projizierten Tiles
- Build-Reihenfolge bleibt erhalten

### Abnahme

- Projektion ist deterministisch und enthält noch keine Terrainentscheidung.
- Jedes belegte Tile lässt sich zu seinem AIV-Element zurückverfolgen.
- `AIVPlacement/build.bat` ist erfolgreich.

### Startprompt

> Bearbeite Chat 5 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Erstelle den
> paketfreien `AIVPlacement`-Offline-Kern und projiziere AIV-Elemente inklusive
> Rotation und Footprints auf absolute Map-Koordinaten. Implementiere noch keine
> Vanilla-Bauregeln.

---

## Chat 6: Bedeutung von `placementState` und nativen Oracle-Aufruf klären

**Status:** Abgeschlossen. Der native Vertrag, die binärgebundenen RVAs und der
passive Laufzeit-Oracle sind in `Docs/AIV_PLACEMENT_ORACLE.md` dokumentiert.
Ein einziger breit angelegter Skirmish-Start lieferte 35 Versuche, alle vier
Rotationen sowie bestätigte Zustände `1` und `2`; Zustand `0` und der
`TestSpecificCandidate`-Fehlerpfad sind durch den eindeutigen nativen
Kontrollfluss belegt.

### Ziel

Den nativen Vergleichsmaßstab zuverlässig definieren. Vor der Regelportierung
muss klar sein, was `TestSpecificCandidate` und `placementState` tatsächlich
bewerten.

### Zu klärende Fragen

- Bedeutet `2` wirklich „gesamte AIV vollständig passend“?
- Bedeutet `1` „bester partieller Kandidat“, und nach welchem Maßstab?
- Wann wird `0` verwendet: kein Kandidat, ungültige AIV oder null platzierbare
  Elemente?
- Bewertet die Funktion den finalen Burgzustand oder die sequenzielle
  Build-Reihenfolge?
- Sind bestimmte AIV-Elemente optional oder bei der Vollständigkeit ignoriert?
- Welche Rotation, Keep-Verschiebung und Kandidatenposition fließen ein?
- Welche Mapzustände werden gelesen?

### Vorgehen

1. Vorhandene Erkenntnisse aus `ActiveAIVDetector` und `SpawnCastle` sammeln.
2. Native Funktion gezielt analysieren und Aufrufparameter dokumentieren.
3. Einen kleinen, klar getrennten Oracle-/Diagnoseweg erstellen.
4. Nach echtem Mapstart kontrollierte Kombinationen prüfen:
   - komplett freie Position
   - einzelne Terrain-Kollision
   - Kartenrand
   - mehrere blockierte Elemente
   - vier Rotationen
5. Logs immer mit Millisekunden-Zeitstempel schreiben.

### Ergebnisartefakte

- Dokumentation des nativen Vertrags
- reproduzierbarer Oracle-Aufruf oder Hook
- strukturierte Vergleichsausgabe mit Karte, Slot, AIV, Rotation,
  `placementState` und gegebenenfalls nativen Zwischenergebnissen

### Abnahme

- Die Bedeutung von 0/1/2 ist durch Codeanalyse und kontrollierte Versuche
  belegt.
- Der Oracle-Weg verändert die eigentliche Map nicht dauerhaft.
- Noch keine Offline-Regel wird allein aus einem Namen oder einer Vermutung
  abgeleitet.

### Noch nicht Teil dieses Chats

- keine Lobby-Anzeige
- kein automatisches Filtern von AIVs
- noch keine Behauptung vollständiger Offline-Parität

### Startprompt

> Bearbeite Chat 6 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Kläre die exakte
> Semantik von `TestSpecificCandidate` und `placementState` und baue einen
> reproduzierbaren nativen Oracle-Vergleich. Ändere noch nichts an der Lobby.

---

## Chat 7: Placement-Regeln inventarisieren und als Reason-Codes modellieren

**Status:** Abgeschlossen. Die nativen Ausschlussgründe, ihre belegten
Map-Layer und noch unbekannten Branchsemantiken sind in
`Docs/AIV_PLACEMENT_RULES.md` inventarisiert. Stabile Reason-Codes und immutable
Tile-Rohwerte stehen in `AIVPlacement.Core`; Release-Build und alle synthetischen
Tests sind erfolgreich.

### Ziel

Alle für eine AIV relevanten nativen Ausschlussgründe identifizieren und ein
stabiles Ergebnis-/Fehlermodell definieren, bevor die Regeln breit implementiert
werden.

### Erwartete Regelgruppen

- Koordinate außerhalb der Karte
- unzulässige Terrain-/Logic-Flags
- Wasser, Klippe oder anderer unbebaubarer Untergrund
- Höhe, Default-Höhe und Steigung
- Baum, Fels oder anderer Organismus
- vorhandenes Gebäude
- vorhandene Entity beziehungsweise Einheit
- Eigentümer-/Mauerbelegung
- Kollision innerhalb der projizierten AIV
- gebäudespezifische Anforderungen
- Sonderregeln für Mauern, Tore, Zugbrücken, Treppen und Pfade

Diese Liste ist nur ein Ausgangspunkt und muss gegen die native Analyse geprüft
werden.

### Vorgesehenes Fehlermodell

Ungefähr:

    [Flags]
    public enum AivPlacementIssueKind
    {
        None,
        OutsideMap,
        TerrainBlocked,
        HeightMismatch,
        OrganismOccupied,
        BuildingOccupied,
        EntityOccupied,
        OwnerConflict,
        InternalOverlap,
        BuildingRuleFailed
    }

Ein Issue benötigt zusätzlich AIV-Elementindex, Mappertyp, Tile-ID,
Map-Koordinate und die für die Entscheidung relevanten Rohwerte.

### Abnahme

- Jeder derzeit bekannte native Ablehnungsgrund besitzt einen Reason-Code.
- Dokumentiert ist, welche Map-Layer und Bits jede Regel verwendet.
- Unbekannte Bitfelder bleiben ausdrücklich unbekannt.
- Noch keine unüberschaubare Alles-in-einer-Methode-Implementierung.

### Startprompt

> Bearbeite Chat 7 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Inventarisiere die
> nativen AIV-Placement-Regeln und entwirf dafür nachvollziehbare Reason-Codes
> samt benötigten Map-Layern. Implementiere nur Regeln, deren Semantik belegt ist.

---

## Chat 8: Offline-Regeln schrittweise implementieren

**Status:** Abgeschlossen. Der paketfreie Offline-Evaluator prüft die belegten
Geometrie-, Layer-, Height-, Logic-, Eigentümer- und Überlappungsregeln pro
Element und erhält die vollständige Tile-Evidenz. Der zunächst offene
Organismuszweig ist inzwischen aufgelöst: Die Skirmish-Initialisierung setzt
den nativen Modus auf `1` oder `99`, wodurch der AIV-Aufruf mit Spieler `0`
sämtliche Organismusklassen akzeptiert. Die synthetische Testmatrix deckt
positive und negative Fälle einschließlich Multi-Tile- und Sonderflächen ab.

### Ziel

Die in Chat 7 belegten Regelgruppen im `AIVPlacement.Core` implementieren und
pro projiziertem AIV-Element auswerten.

### Empfohlene Unterteilung innerhalb des Chats

1. Kartenrand und ungültige Koordinaten
2. Belegungen durch Gebäude, Entities, Organismen und Eigentümer
3. Terrain-/Logic-Flags
4. Höhe und Steigung
5. interne AIV-Überlappungen
6. Gebäude- und AIV-Sonderfälle

Wenn die native Analyse einer Regel noch unsicher ist, wird sie nicht durch
einen permissiven Fallback ersetzt. Das Ergebnis muss dann `NotEvaluable` oder
einen expliziten unbekannten Grund liefern.

### Vorgesehene API

Ungefähr:

    AivElementPlacementResult EvaluateElement(
        MapPlacementSnapshot map,
        AivProjectedElement element);

### Tests

- pro Reason-Code mindestens ein positiver und ein negativer Fall
- Multi-Tile-Gebäude, bei dem nur ein Tile blockiert ist
- interne Überschneidung zweier AIV-Elemente
- Sonderflächen getrennt vom Kern-Footprint
- Fehler enthält das tatsächlich auslösende Tile

### Abnahme

- Ergebnisse sind deterministisch und erklären jede Ablehnung.
- Keine Unity-/BepInEx-Abhängigkeit im Offline-Kern.
- Synthetische Testmatrix ist vollständig grün.

### Startprompt

> Bearbeite Chat 8 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere die in
> Chat 7 belegten Placement-Regeln schrittweise im Offline-Kern und ergänze pro
> Reason-Code positive und negative Tests.

---

## Chat 9: Gesamte AIV bewerten und beste Variante bestimmen

**Status:** Abgeschlossen. Die immutable Gesamtbewertung definiert
`Complete`, `Partial`, `Impossible` und `NotEvaluable`, bewahrt beide nativen
Score-Dimensionen und wählt die beste Rotation in belegter nativer Reihenfolge.
Alle Issues bleiben bis zum Element und Tile nachvollziehbar; der gezielte
RVA-`0x541D0`-Audit und die synthetischen Tests belegen die Auswahlgrenzen.

### Ziel

Aus den Elementergebnissen einen stabilen Kandidatenstatus pro AIV, Keep-Position
und Rotation bilden.

### Vorgesehene Eingabe

    MapPlacementSnapshot map
    AivBlueprint aiv
    MapCoordinate keepPosition
    AivRotation rotation

### Vorgesehene Ausgabe

    AivPlacementResult
      Status
      Rotation
      TotalElementCount
      PlaceableElementCount
      BlockedElementCount
      Issues
      FirstBlockingBuildStep
      Score

Der `Score` darf erst definiert werden, wenn Chat 6 geklärt hat, wie Vanilla
partielle Kandidaten vergleicht.

### Zusätzliche Funktionen

- exakt eine Rotation prüfen
- alle erlaubten Rotationen prüfen
- beste Variante deterministisch auswählen
- vollständige und partielle Kandidaten getrennt sortieren
- `NotEvaluable` nicht als „passt nicht“ fehlinterpretieren

### Abnahme

- `Complete`, `Partial`, `Impossible` und `NotEvaluable` sind eindeutig definiert.
- Gleiche Eingaben ergeben unabhängig von Reihenfolge und Plattform dasselbe
  Ergebnis.
- Alle Issues bleiben bis zum verursachenden Element/Tile nachvollziehbar.

### Startprompt

> Bearbeite Chat 9 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere die
> Gesamtbewertung einer AIV pro Keep-Position und Rotation sowie die
> deterministische Auswahl der besten Variante anhand der belegten nativen
> Semantik.

---

## Chat 10: No-PreBuild-Ergebnis systematisch gegen den nativen Oracle vergleichen

**Status:** Abgeschlossen. Dieser Chat nimmt verbindlich nur die erste
Ausbaustufe mit deaktiviertem Sofortspawn (`advopt_pre_build=0`) ab. Die alte
144-Fall-Matrix wurde vollständig entfernt, weil ihre Logs weder verlässliche
Kartenstart-IDs noch den jeweiligen Optionswert enthielten. Die neue
kanonische Thasos-Sitzung ohne Sofortspawn umfasst 24 hashgebundene Fälle und
ist nach Rekonstruktion der gemeinsam rotierten Startkomplexe einschließlich
gekoppelter Wall-Flags mit 24/24 exakt. Der explizite 48-Fall-Paarkorpus
bestätigt ebenfalls 24/24 im Modus `0`. Damit besitzt die No-PreBuild-Stufe
0 Mismatches, 0 Fehler und keine stillschweigend akzeptierte Abweichung.

Die 24 Fälle mit `advopt_pre_build=1` gehören nicht zur Abnahme dieser ersten
Stufe. Vier Fälle des jeweils ersten KI-Spielers sind exakt; 20 abhängige Fälle
bleiben technisch begründet `NotEvaluable`, weil eine statische AIV-Projektion
den real ausgeführten sequenziellen Zustand nicht reproduziert. Diese Grenze
ist beabsichtigt und wird erst in den späteren Sofortspawn-Chats 14 bis 16
aufgehoben. Ergebnisse und Hashes stehen in
`Docs/AIV_PLACEMENT_ORACLE_COMPARISON.md` und
`Docs/CHAT10_ORACLE_MISMATCH_HANDOFF.md`.

**Vorarbeit für die spätere Sofortspawn-Phase:** Der wiederholte Wildcard-Lauf
mit ActiveAIVDetector 0.9.2
lieferte je einen Versuch und ein vollständiges Grid für Spieler 2 bis 7. Die
sechs Zwischenzustände sind valide und vollständig ausgewertet. Der ergänzende
ActiveAIVDetector-0.9.3-Lauf erfasste außerdem alle 77 `ExecuteBuildStep`-
Frames des ersten Spieler-2-Sofortspawns mit einem konsistenten Placement-
State-Zeiger, 0 Capturefehlern und 0 Hookwarnungen.

**Native Ausführungsanalyse:** `ExecuteBuildStep` umgeht bei
`freeOrForced=true` nur die Ressourcen-/Verfügbarkeitsprüfung. Der erneut gegen
den Live-Zustand ausgeführte Footprint-Helfer kann Objekte bereinigen, sein
Fehlschlag blockiert den nachfolgenden Konstruktor jedoch nicht. Der
Tunnelers-Guild-Konstruktor erzeugt Hauptgebäude und separaten 5×5-Hof und
erklärt damit die 50 erzwungenen Zellen vollständig. Drawbridges benötigen
zuvor ein passendes lebendes Tor samt Orientierung; der Goods-Yard-Konstruktor
erzeugt vier reale Gebäudedatensätze und neun Verbindungstiles. Der gezielte
Laufzeittrace entscheidet die konkreten Frames: Mapper 105 gab `0` zurück und
änderte keine Building-ID, Mapper 89 gab `1` zurück und erzeugte zwei IDs mit
je 25 Zellen, Mapper 52 gab `0` zurück und änderte das Building-Grid an keiner
Stelle. Diese Evidenz bleibt als Ausgangspunkt für Chat 14 erhalten. Bis zur
vollständigen sequenziellen Offline-Ausführung bleiben die 20 abhängigen
Sofortspawn-Fälle technisch begründet `NotEvaluable`; sie blockieren den
Abschluss der No-PreBuild-Stufe und die Chats 11 bis 13 nicht.

**Geometrienachtrag:** Ein Read-only-Scan aller 238 installierten offiziellen Karten
belegt zusätzlich die World Sizes 500, 600 und 700. Die Offline-Geometrie und
ihre Grenztests unterstützen damit alle acht offiziellen Größen 160, 200, 300,
400, 500, 600, 700 und 800. Die gelöschte historische Laufzeitmatrix ist dafür
kein Abnahmebeleg mehr; nicht unterstützte Sondergrößen werden weiterhin
bewusst abgelehnt.

**Sofortspawn-/Sitzungsnachtrag:** Neue Oracle-Importe erhalten pro tatsächlichem
Kartenstart eine explizite `SessionId` und den vor Spielstart gelesenen Wert
`advopt_pre_build`. Spieler werden nativ vollständig in ID-Reihenfolge `1..8`
verarbeitet. Ohne Sofortspawn werden frühere AIV-Pläne nicht als Blocker
weitergereicht; mit Sofortspawn sieht der nächste Spieler die bereits
ausgeführten Gebäude und Tile-Änderungen. Frühere Startkomplexe sind in beiden
Modi real. Die Herkunft steht getrennt in `AivTileOccupancyKind`; nur live
bestätigte Belegung verwendet `PriorAivPrebuiltOccupied`. Ein AIV-Plan wird
nicht in scheinbar reale Blocker umgedeutet und erfindet niemals eine
`BuildingId`. Das vollständige Verfahren und seine RVAs stehen in
`Docs/AIV_PREBUILD_AND_OVERLAP_ORDER.md`.

### Ziel

Die Regelparität der No-PreBuild-Ausbaustufe an einem kanonischen,
sitzungsgebundenen Corpus messen und jede verbleibende Abweichung
klassifizieren. Sofortspawn-Effekte dürfen dabei weder in den No-PreBuild-
Erfolg eingerechnet noch aus geplanten Footprints vorgetäuscht werden.

### Abgedeckte No-PreBuild-Matrix

- explizite Map-Load-Session mit `advopt_pre_build=0`
- sechs KI-Spieler beziehungsweise Keep-Slots auf `v_Thasos.map`
- vier native Rotationen pro ausgewähltem Spielerfall
- freie und blockierte Zellen einschließlich realer gedrehter Startkomplexe
- 24 hashgebundene Oracle-Fälle mit exakten Status- und Scorewerten
- zusätzliche synthetische Regeln und Geometrieabdeckung für alle acht
  offiziellen World Sizes

Eine breitere Mehrkarten-/Mehr-AIV-Matrix bleibt für die spätere
Sofortspawn-Regression sinnvoll, ist aber keine nachträgliche Voraussetzung für
die bereits exakte No-PreBuild-Basis.

### Vergleichsdatensatz

Für jeden Fall mindestens:

- Map-Identität beziehungsweise Hash, ohne die Karte zu kopieren
- AIV-Identität beziehungsweise Hash
- explizite Kartenstart-/Session-ID für zusammengehörige Mehrspielerfälle
- explizite aktuelle Einstellung `advopt_pre_build=0`
- Keep-Slot und Keep-Koordinate
- Rotation
- Offline-Status und Score
- nativer `placementState` und gegebenenfalls nativer Score
- erste abweichende Regel
- relevante Tile-Rohwerte
- reale Gebäude-ID getrennt von Map-, Start-, Plan-, Scheduled- und
  PreBuild-Belegungsherkunft

### Fortschrittsanforderung

Corpusläufe müssen Fortschritt, Anzahl, verstrichene Zeit und ETA melden. Zuerst
wird genau ein Fall gemessen, danach eine kleine Stichprobe. Ein großer Lauf wird
erst gestartet, wenn die geschätzte Laufzeit zumutbar ist.

### Abnahme

- Der kanonische No-PreBuild-Corpus ist 24/24 exakt.
- No-PreBuild besitzt 0 ungeklärte Mismatches und 0 Fehler.
- Sofortspawn-Abweichungen werden nicht als No-PreBuild-Erfolg gezählt, sondern
  bis zur späteren Erweiterungsphase ausdrücklich `NotEvaluable`.
- Ergebnisse, Hashes und reproduzierbare Einzeltests sind dokumentiert.

### Startprompt

> Chat 10 ist für `advopt_pre_build=0` abgeschlossen. Bewahre die
> ActiveAIVDetector-0.9.2/0.9.3-Evidenz als Vorarbeit für die späteren
> Sofortspawn-Chats 14 bis 16 auf, behandle abhängige Sofortspawn-Spieler bis
> dahin als `NotEvaluable` und fahre mit dem markierten nächsten Schritt fort.

---

## Chat 11: No-PreBuild-Lobby-Datenfluss ohne UI anbinden

**Status:** Nächster Schritt. Die produktive Ausbaustufe bleibt bis
einschließlich Chat 13 ausdrücklich auf deaktivierten Sofortspawn begrenzt.

### Ziel

Vor der sichtbaren UI sicher bestimmen, welche Mapdatei und Keep-Position zu
jedem Lobby-Slot gehören und welche AIV-Kandidaten bei
`advopt_pre_build=0` geprüft werden müssen.

### Phasengrenze

- Den aktuellen Lobbywert von `advopt_pre_build` zuverlässig erfassen.
- Bei Wert `0` den normalen No-PreBuild-Prüfauftrag erzeugen.
- Bei Wert `1` noch keine sequenzielle Belegung simulieren. Das Ergebnis muss
  mit einem eindeutigen Grund `NotEvaluable` sein, bis Chats 14 bis 16 die
  Sofortspawn-Unterstützung ergänzen.
- Die vorhandene 0.9.2/0.9.3-Evidenz nicht vorzeitig in produktive
  Projektionslogik umdeuten.

### Zu klärende Datenquellen

- Pfad der aktuell ausgewählten Karte
- Auflösung eingebauter, benutzerdefinierter und gegebenenfalls Workshop-Karten
- Lobby-Slot → Spielerslot → Keep-Index
- unbenutzte und Zuschauer-Slots
- ausgewählter Lord/KI
- verfügbare `.aivjson`-Varianten und aktuelle Auswahl
- Host-/Client-Verantwortung

### Umsetzung

- Noch keine visuelle Änderung.
- Bei jeder relevanten Lobbyänderung einen immutable Prüfauftrag erzeugen.
- MapParser und AIVParser nur außerhalb der Unity-Objektmutation verwenden.
- Reine Datei-/CPU-Prüfung darf im Hintergrund laufen.
- Unity- und UI-Zugriffe müssen zurück auf den Main Thread.
- Veraltete Ergebnisse über Generation-ID oder Cancellation verwerfen.

### Abnahme

- Für jeden belegten KI-Slot wird die korrekte Kombination aus Map, Keep und AIV
  sowie der aktuelle Sofortspawn-Wert protokolliert.
- Bei deaktiviertem Sofortspawn wird ein vollständiger Prüfauftrag erzeugt.
- Bei aktiviertem Sofortspawn wird nachvollziehbar `NotEvaluable` geliefert,
  niemals ein No-PreBuild-Ergebnis als scheinbar exakter Ersatz.
- Schneller Karten-/Slotwechsel kann kein Ergebnis der vorherigen Auswahl
  anzeigen.
- Fehlerhafte oder nicht unterstützte Dateien führen zu `NotEvaluable`, nicht
  zum Lobby-Absturz.

### Startprompt

> Bearbeite Chat 11 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Binde zunächst nur
> den No-PreBuild-Lobby-Datenfluss an. Ermittle zuverlässig Mapdatei, KI-Slot,
> Keep-Position, AIV-Kandidaten und `advopt_pre_build`. Werte nur Modus `0`
> produktiv aus und liefere für Modus `1` bis Chat 14 ausdrücklich
> `NotEvaluable`; ändere die sichtbare Lobby-UI noch nicht.

---

## Chat 12: No-PreBuild-Cache und asynchrone Auswertung implementieren

**Status:** Ausstehend.

### Ziel

Die Offline-Prüfung schnell genug für Karten-, Slot- und AIV-Wechsel in der
Lobby machen. Produktive Platzierungsergebnisse bleiben in dieser Phase auf
`advopt_pre_build=0` begrenzt.

### Empfohlene Cache-Schlüssel

Map-Cache:

- normalisierter vollständiger Pfad
- Dateigröße
- letzter Änderungszeitpunkt
- optional SHA-256, wenn Zeitstempel nicht verlässlich genug ist

AIV-Ergebnis-Cache:

- Map-Identität
- AIV-Identität
- Keep-Position beziehungsweise Slot
- Rotation
- Analyzer-/Regelversion
- `advopt_pre_build`, damit `NotEvaluable` im Modus `1` niemals mit einem
  No-PreBuild-Ergebnis kollidiert

### Anforderungen

- Begrenzte Cachegröße oder gezielte Invalidierung.
- Keine Unity-Objekte im Hintergrundthread verwenden.
- Doppelte gleichzeitige Prüfaufträge zusammenführen.
- `NotEvaluable` mit Ursache cachen, aber nach Dateiänderung neu prüfen.
- Messbare Zeiten für Parse, Snapshot, Projektion und Regelprüfung protokollieren.

### Abnahme

- Wiederholte Auswahl derselben Kombination verursacht keine vollständige
  Neuberechnung.
- Dateiänderungen invalidieren den passenden Eintrag.
- Schnelle Lobbyinteraktion bleibt responsiv.

### Startprompt

> Bearbeite Chat 12 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere einen
> begrenzten, invalidierbaren Cache und die sichere asynchrone Auswertung für
> Map/AIV/Keep/Rotation-Kombinationen der No-PreBuild-Stufe. Führe den
> Sofortspawn-Wert im Schlüssel und verwende keine Unity-Objekte im
> Hintergrundthread.

---

## Chat 13: No-PreBuild-Lobby-UI und Multiplayer-Verhalten fertigstellen

**Status:** Ausstehend.

### Ziel

Die geprüften Ergebnisse verständlich in der Skirmish-Lobby anzeigen und bei
Host/Client konsistent behandeln. Damit wird die erste produktive Ausbaustufe
für deaktivierten Sofortspawn abgeschlossen.

### Mögliche UI-Darstellung

- grünes Kennzeichen: vollständig passend
- gelbes Kennzeichen: teilweise passend
- rotes Kennzeichen: nicht passend
- graues Kennzeichen: nicht prüfbar oder Prüfung läuft
- optional kurze Zusammenfassung wie „3 Gebäude blockiert“
- optional Filter „nur vollständig passende AIVs“
- Details/Tooltip mit erstem Fehlergrund, ohne die Lobby zu überladen

Farben dürfen nicht das einzige Unterscheidungsmerkmal sein; Text oder Symbol
muss den Zustand ebenfalls ausdrücken.

Bei aktiviertem Sofortspawn muss die UI bis zum Abschluss von Chat 16 klar
anzeigen, dass die sequenzielle Prüfung noch nicht unterstützt wird. Sie darf
dafür kein Ergebnis aus Modus `0` wiederverwenden.

### Multiplayer-Fragen

- Entscheidet ausschließlich der Host oder rechnen alle Clients lokal?
- Müssen Ergebnis, gewählte AIV und Rotation synchronisiert werden?
- Wie wird mit unterschiedlichen lokalen Custom-/Extended-Lord-Dateien
  umgegangen?
- Darf eine nicht passende AIV nur gewarnt oder vollständig blockiert werden?

Diese Produktentscheidungen müssen vor der endgültigen UI-Logik mit dem Nutzer
festgelegt werden.

### Abnahme

- Karten-, Slot- und AIV-Wechsel aktualisieren den Status korrekt.
- Laufende Prüfungen und Fehlerzustände sind sichtbar.
- Kein veraltetes Ergebnis wird einem neuen Lobbyzustand zugeordnet.
- Host und Clients erhalten ein definiertes, dokumentiertes Verhalten.
- Die tatsächliche Spielauswahl wird nicht ohne ausdrückliche Entscheidung des
  Nutzers automatisch verändert.
- Der komplette Lobbypfad für `advopt_pre_build=0` ist damit produktiv
  abgeschlossen; Modus `1` bleibt sichtbar und begründet `NotEvaluable`.

### Startprompt

> Bearbeite Chat 13 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Ergänze die
> Skirmish-Lobby für die No-PreBuild-Stufe um verständliche
> Complete/Partial/Impossible/NotEvaluable-Anzeigen und kläre mit mir vorab, ob
> nur gewarnt, gefiltert oder eine Auswahl blockiert werden soll sowie welches
> Host-/Client-Verhalten gewünscht ist. Kennzeichne aktivierten Sofortspawn bis
> Chat 16 ausdrücklich als noch nicht sequenziell auswertbar.

---

## Chat 14: Sequenzielles Sofortspawn-Zustandsmodell entwickeln

**Status:** Ausstehend. Erst beginnen, nachdem die No-PreBuild-Ausbaustufe in
Chat 13 vollständig abgenommen ist.

### Ziel

Die echte native Ausführung früherer KI-Burgen so rekonstruieren, dass der
Offline-Zustand vor jedem nachfolgenden Spieler exakt bekannt ist. Eine
statische Vereinigung platzierbarer AIV-Footprints bleibt ausdrücklich
unzulässig.

### Vorhandene Grundlage

- sechs valide Zwischenzustände aus ActiveAIVDetector 0.9.2;
- der vollständige 77-Frame-Spieler-2-Trace aus ActiveAIVDetector 0.9.3;
- entschiedene Zweige für Mapper 52, 89 und 105;
- statische native Audits der gemeinsamen Vorprüfungen und Konstruktoren;
- die dokumentierte Spielerreihenfolge `1..8` und gemeinsame Rotation von AIV
  und Startkomplex.

### Vorgehen

- Den Diagnose-Trace kontrolliert auf spätere Spieler erweitern, insbesondere
  auf Frames mit entfernten oder ersetzten Gebäuden.
- Neben `BuildingId` alle für nachfolgende Fit-Prüfungen relevanten Tile-
  Änderungen, tatsächlichen Positionen und Mehrkomponentenobjekte erfassen.
- Mapperregeln als deterministische sequenzielle Zustandsübergänge im
  paketfreien Offline-Kern modellieren.
- Nicht belegte Mapper oder Seiteneffekte bleiben `NotEvaluable`; keine
  Heuristik darf als reale PreBuild-Belegung ausgegeben werden.
- Laufzeittraces zunächst an einem Spieler, dann an einer kleinen
  Mehrspielerprobe validieren, bevor ein größerer Lauf gestartet wird.

### Abnahme

- Für jeden modellierten Frame stimmen Rückgabewert und alle relevanten
  Zustandsänderungen mit dem nativen Trace überein.
- Gebäudeabrisse, Ersetzungen, Mehrkomponentengebäude und Tile-only-Effekte
  besitzen reproduzierbare Tests.
- Der Offline-Kern kann den exakten Zustand vor mindestens dem nächsten
  Spieler erzeugen, ohne beobachtete Live-Daten als Eingabe zu benötigen.
- Ungeklärte Zweige werden weiterhin ausdrücklich `NotEvaluable`.

### Startprompt

> Bearbeite Chat 14 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Erweitere nach
> Abschluss der No-PreBuild-Lobby den diagnostischen Sofortspawn-Trace auf die
> notwendigen späteren Spieler und implementiere daraus ein sequenzielles,
> paketfreies Zustandsmodell. Verwende keine statische Footprint-Vereinigung.

---

## Chat 15: Sofortspawn-Modell gegen den nativen Oracle validieren

**Status:** Ausstehend.

### Ziel

Das sequenzielle Modell aus Chat 14 mit explizit aktiviertem
`advopt_pre_build=1` gegen native, sitzungsgebundene Mehrspielerläufe prüfen.

### Vergleichsmatrix

- mehrere Karten und Kartengrößen;
- mehrere Keep-Zuordnungen und Spielerreihenfolgen;
- kleine, mittlere und große AIVs;
- alle relevanten Rotationen;
- erfolgreiche und abgebrochene mapperabhängige Konstruktoren;
- Zustände mit hinzugefügten, entfernten und ersetzten Gebäuden sowie
  sonstigen blockierenden Tile-Änderungen.

### Anforderungen

- Jeder Fall besitzt eine nichtleere `SessionId`, den expliziten Wert `1` und
  unveränderte Map-/AIV-/Log-Hashes.
- Zuerst ein Fall und eine kleine Stichprobe mit Fortschritt und ETA; größere
  Corpora erst nach plausibler Laufzeitschätzung.
- Native Sollwerte werden niemals an das Offline-Ergebnis angepasst.
- Verbleibende Lücken sind reproduzierbar und ausdrücklich `NotEvaluable`,
  nicht stillschweigend erfolgreich.

### Abnahme

- Alle unterstützten Sofortspawn-Fälle besitzen 0 ungeklärte Mismatches und
  0 Fehler.
- Spätere KI-Spieler werden aus dem rekonstruierten sequenziellen Zustand
  exakt bewertet.
- Die unterstützten und weiterhin `NotEvaluable` bleibenden Mappergrenzen sind
  dokumentiert und getestet.

### Startprompt

> Bearbeite Chat 15 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Validiere das
> sequenzielle Sofortspawn-Modell mit modus- und sitzungsgebundenen nativen
> Oracle-Läufen. Starte klein, protokolliere Fortschritt und akzeptiere keine
> ungeklärte Abweichung als Erfolg.

---

## Chat 16: Sofortspawn in Lobby, Cache und UI integrieren

**Status:** Ausstehend.

### Ziel

Die in Chats 14 und 15 abgenommene sequenzielle Auswertung in das bereits
fertige No-PreBuild-System einfügen, ohne dessen exakten Modus-0-Pfad zu
regressieren.

### Umsetzung

- `advopt_pre_build` bleibt Teil von Prüfauftrag und Cache-Schlüssel.
- Modus `0` verwendet unverändert den abgeschlossenen No-PreBuild-Pfad.
- Modus `1` wertet Spieler strikt in nativer Reihenfolge aus und reicht den
  rekonstruierten Zustand nur an nachfolgende Spieler weiter.
- UI und Multiplayer-Datenfluss zeigen unterstützte Ergebnisse sowie
  verbleibende `NotEvaluable`-Grenzen eindeutig an.
- Cacheeinträge und asynchrone Generationen der beiden Modi dürfen niemals
  verwechselt werden.

### Abnahme

- Die bestehenden No-PreBuild-Tests und Oracle-Corpora bleiben vollständig
  exakt.
- Unterstützte Sofortspawn-Sitzungen liefern in Lobby und Offline-Vergleich
  dieselben Ergebnisse wie der native Oracle.
- Schnelle Options-, Karten-, Slot- und AIV-Wechsel zeigen keine veralteten
  Ergebnisse des jeweils anderen Modus.
- Nicht unterstützte Sofortspawn-Zweige bleiben sichtbar `NotEvaluable` und
  verursachen keinen Lobby-Absturz.

### Startprompt

> Bearbeite Chat 16 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Integriere das in
> Chats 14 und 15 validierte sequenzielle Sofortspawn-Modell in Prüfauftrag,
> Cache und Lobby-UI. Bewahre den exakten No-PreBuild-Pfad unverändert und
> trenne beide Modi in allen Cache- und Generation-Schlüsseln.

---

## Empfohlene Reihenfolge und Stopppunkte

Die Chats werden in der Reihenfolge 1 bis 16 bearbeitet. Besonders wichtige
Stopppunkte sind:

1. Nach Chat 1: Keine Geometrie implementieren, solange die native Zuordnung
   nicht belegt ist.
2. Nach Chat 6: Keine Gesamtstatus-Semantik festschreiben, solange 0/1/2 nicht
   eindeutig verstanden sind.
3. Nach Chat 10: Chats 11 bis 13 nur für deaktivierten Sofortspawn umsetzen;
   Modus `1` bleibt bis zur Erweiterungsphase ausdrücklich `NotEvaluable`.
4. Vor Chat 13: Nutzerentscheidung einholen, ob die Lobby nur informiert,
   filtert oder ungültige AIV-Auswahlen blockiert.
5. Vor Chat 14: Die komplette No-PreBuild-Ausbaustufe einschließlich Lobby-UI
   abnehmen; Sofortspawn-Forschung nicht als Voraussetzung zurückziehen.
6. Vor Chat 16: Keine produktive Sofortspawn-Auswertung anbinden, solange das
   sequenzielle Modell nicht gegen den nativen Oracle abgenommen ist.

Der nächste auszuführende Schritt ist "Chat 11".


