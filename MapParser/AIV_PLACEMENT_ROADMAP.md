# Roadmap: AIVJSON vor dem Mapstart auf eine Startposition prüfen

## Zielbild

Langfristig soll bereits in der Skirmish-Lobby für jede Kombination aus

- ausgewählter `.map`-Datei,
- Lobby-/Spieler-Slot beziehungsweise Keep-Position,
- KI beziehungsweise ausgewählter `.aivjson`,
- und gegebenenfalls AIV-Rotation

bestimmt werden, ob die geplante Burg vollständig auf die Karte passt.

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

## Bereits vorhandene Grundlage

Der derzeitige Workspace enthält:

- `MapParser`: liest `.map`, Metadaten, Keep-Positionen und Placement-Layer
  strikt und read-only.
- `AIVParser`: liest `.aivjson`, Build-Reihenfolge, Keep-Anker, Rotationen,
  Footprints und bekannte zugehörige Blockierflächen.
- `ActiveAIVDetector`: besitzt bereits Erkenntnisse zum aktiven AIV-Kandidaten
  und zum nativen `placementState`.
- `SpawnCastle`: enthält praktische Erfahrung mit AIV-Projektion und
  Gebäudeplatzierung im laufenden Spiel.

Noch nicht vorhanden sind die validierte Map-Koordinatenabbildung, ein
Placement-Snapshot, die eigentlichen Vanilla-Fit-Regeln und die Lobby-Anbindung.

## Arbeitsregeln für die folgenden Chats

Jeder nummerierte Schritt ist als eigener Chat gedacht. Ein Schritt gilt erst
als abgeschlossen, wenn seine Abnahmekriterien erfüllt und die zugehörigen
Tests erfolgreich sind.

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
   - Keep-Positionen aus realen 160-/200-/300-/400-Karten
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

## Chat 4: AIV-Projektion als unabhängigen Offline-Kern entwickeln

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

> Bearbeite Chat 4 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Erstelle den
> paketfreien `AIVPlacement`-Offline-Kern und projiziere AIV-Elemente inklusive
> Rotation und Footprints auf absolute Map-Koordinaten. Implementiere noch keine
> Vanilla-Bauregeln.

---

## Chat 5: Bedeutung von `placementState` und nativen Oracle-Aufruf klären

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

> Bearbeite Chat 5 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Kläre die exakte
> Semantik von `TestSpecificCandidate` und `placementState` und baue einen
> reproduzierbaren nativen Oracle-Vergleich. Ändere noch nichts an der Lobby.

---

## Chat 6: Placement-Regeln inventarisieren und als Reason-Codes modellieren

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

> Bearbeite Chat 6 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Inventarisiere die
> nativen AIV-Placement-Regeln und entwirf dafür nachvollziehbare Reason-Codes
> samt benötigten Map-Layern. Implementiere nur Regeln, deren Semantik belegt ist.

---

## Chat 7: Offline-Regeln schrittweise implementieren

### Ziel

Die in Chat 6 belegten Regelgruppen im `AIVPlacement.Core` implementieren und
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

> Bearbeite Chat 7 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere die in
> Chat 6 belegten Placement-Regeln schrittweise im Offline-Kern und ergänze pro
> Reason-Code positive und negative Tests.

---

## Chat 8: Gesamte AIV bewerten und beste Variante bestimmen

### Ziel

Aus den Elementergebnissen einen stabilen Kandidatenstatus pro AIV, Keep-Position
und Rotation bilden.

### Vorgesehene Eingabe

    MapPlacementSnapshot map
    AivDocument aiv
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

Der `Score` darf erst definiert werden, wenn Chat 5 geklärt hat, wie Vanilla
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

> Bearbeite Chat 8 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere die
> Gesamtbewertung einer AIV pro Keep-Position und Rotation sowie die
> deterministische Auswahl der besten Variante anhand der belegten nativen
> Semantik.

---

## Chat 9: Offline-Ergebnis systematisch gegen den nativen Oracle vergleichen

### Ziel

Die Regelparität nicht nur an Einzelfällen, sondern an einer kontrollierten
Matrix messen und verbleibende Abweichungen klassifizieren.

### Vergleichsmatrix

- mehrere Kartengrößen
- eingebaute und benutzerdefinierte reguläre Karten
- mehrere Keep-Slots pro Karte
- kleine, mittlere und große AIVs
- alle relevanten Rotationen
- freie, teilweise blockierte und offensichtlich unmögliche Positionen
- Karten mit Section-1190-Anomalie, da ihre Placement-Layer trotzdem verfügbar
  sind

### Vergleichsdatensatz

Für jeden Fall mindestens:

- Map-Identität beziehungsweise Hash, ohne die Karte zu kopieren
- AIV-Identität beziehungsweise Hash
- Keep-Slot und Keep-Koordinate
- Rotation
- Offline-Status und Score
- nativer `placementState` und gegebenenfalls nativer Score
- erste abweichende Regel
- relevante Tile-Rohwerte

### Fortschrittsanforderung

Corpusläufe müssen Fortschritt, Anzahl, verstrichene Zeit und ETA melden. Zuerst
wird genau ein Fall gemessen, danach eine kleine Stichprobe. Ein großer Lauf wird
erst gestartet, wenn die geschätzte Laufzeit zumutbar ist.

### Abnahme

- Übereinstimmungsquote und verbleibende Abweichungen sind dokumentiert.
- Keine bekannte Abweichung wird stillschweigend als Erfolg gezählt.
- Abweichungen besitzen reproduzierbare Einzeltests oder eine begründete
  `NotEvaluable`-Klassifikation.

### Startprompt

> Bearbeite Chat 9 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Vergleiche den
> Offline-Analyzer schrittweise gegen den nativen Oracle. Beginne mit genau einem
> Fall, miss die Laufzeit und verwende bei größeren Stichproben sichtbaren
> Fortschritt und ETA.

---

## Chat 10: Lobby-Datenfluss ohne UI anbinden

### Ziel

Vor der sichtbaren UI sicher bestimmen, welche Mapdatei und Keep-Position zu
jedem Lobby-Slot gehören und welche AIV-Kandidaten geprüft werden müssen.

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
  protokolliert.
- Schneller Karten-/Slotwechsel kann kein Ergebnis der vorherigen Auswahl
  anzeigen.
- Fehlerhafte oder nicht unterstützte Dateien führen zu `NotEvaluable`, nicht
  zum Lobby-Absturz.

### Startprompt

> Bearbeite Chat 10 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Binde zunächst nur
> den Lobby-Datenfluss an. Ermittle zuverlässig Mapdatei, KI-Slot, Keep-Position
> und AIV-Kandidaten, aber ändere die sichtbare Lobby-UI noch nicht.

---

## Chat 11: Cache und asynchrone Auswertung implementieren

### Ziel

Die Offline-Prüfung schnell genug für Karten-, Slot- und AIV-Wechsel in der
Lobby machen.

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

> Bearbeite Chat 11 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Implementiere einen
> begrenzten, invalidierbaren Cache und die sichere asynchrone Auswertung für
> Map/AIV/Keep/Rotation-Kombinationen. Verwende keine Unity-Objekte im
> Hintergrundthread.

---

## Chat 12: Lobby-UI und Multiplayer-Verhalten fertigstellen

### Ziel

Die geprüften Ergebnisse verständlich in der Skirmish-Lobby anzeigen und bei
Host/Client konsistent behandeln.

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

### Startprompt

> Bearbeite Chat 12 aus `MapParser/AIV_PLACEMENT_ROADMAP.md`: Ergänze die
> Skirmish-Lobby um verständliche Complete/Partial/Impossible/NotEvaluable-
> Anzeigen und kläre mit mir vorab, ob nur gewarnt, gefiltert oder eine Auswahl
> blockiert werden soll sowie welches Host-/Client-Verhalten gewünscht ist.

---

## Empfohlene Reihenfolge und Stopppunkte

Die Chats werden in der Reihenfolge 1 bis 12 bearbeitet. Besonders wichtige
Stopppunkte sind:

1. Nach Chat 1: Keine Geometrie implementieren, solange die native Zuordnung
   nicht belegt ist.
2. Nach Chat 5: Keine Gesamtstatus-Semantik festschreiben, solange 0/1/2 nicht
   eindeutig verstanden sind.
3. Nach Chat 9: Keine Lobbyentscheidung auf Basis ungeklärter Abweichungen
   erzwingen.
4. Vor Chat 12: Nutzerentscheidung einholen, ob die Lobby nur informiert,
   filtert oder ungültige AIV-Auswahlen blockiert.

Der unmittelbar nächste Schritt ist damit **Chat 1: Koordinatensysteme und
native Row-LUT belegen**.
