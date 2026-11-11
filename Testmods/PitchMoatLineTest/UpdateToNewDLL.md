# Native Prüfung für den Pech- und Grabenlinien-Testmod

Stand 10.11.2026. Installierte `CrusaderDE.dll`: SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die Analyse verwendet das gleichnamige installierte Image und den über
`_inspect/CrusaderDE-Native-Baseline/CURRENT.json` ausgewählten semantischen Export.
Für den generierten Stub sind auch RedBird.X64 (`E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB`)
und Iced (`CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F`) geprüft.

## Eingabe und Vanilla-Befehle

`EditorDirector.preDLLCallActions` erreicht `EngineInterface.PlaceMapperItem`
unter dem Engine-Monitor. Der Mod bleibt in dieser Sperre. Der Export
`DLL_MapAction` bei RVA `0x85BD0` setzt für die aktuelle Aktion kurz den
Spieler und stellt ihn nach `0x8AAF0` wieder her. Der Auswahleinstieg
`0x87830 -> 0x8BD60` setzt den nativen Mapper. Das Grabenwerkzeug wird in
den nativen Builder-Modi 1 und 6 von `MAPPER_MOAT` auf
`MAPPER_DUGMOAT` abgebildet. Der Eingabedispatcher `0x90CD0` nutzt den
aktiven Mapper und die aktuellen Kartenkoordinaten. Anders als Mauern
(`0x94120 -> 0x7A3B0/0x61210`, Chore 25) nimmt er pro Chore einen
Pinselmittelpunkt entgegen. Mauszustand 1 ist der unabhängige Einzelauftrag.

- Pech: `0x90CD0 -> 0x77E60 -> 0xCC420 -> 0x23990`, Chore 28.
  `0x12090` liest die Chore-Felder, übernimmt den Absender und delegiert
  nach `0x6D580`. Dort folgen die neue Prüfung, `0x59380` für den
  Pecheintrag und `0xC8A90` für die Zahlung. Bei normaler Zahlung
  belastet Phase 0 genau eine Einheit Rohpech. Phase 3 geht auf 0 zurück.
  Die vier übrigen Materialschwellen und die Rohpechschwelle kommen aus
  den laufenden Kostentabellen. Eisen wird zuerst geprüft. Unterhalb der
  Rohpechschwelle wird Phase 0 abgelehnt, ein vorausbezahltes Feld überspringt
  die folgenden Gold-, Holz- und Steinprüfungen. Bei konfigurierter Schwelle 0
  lässt Vanilla auch Phase 0 ohne Rohpech zu. Der Abbuchungsversuch findet dann
  keinen Vorrat und die Phase rückt trotzdem weiter. Die Vorschau zählt nur
  Abbuchungen für baubare, bezahlbare Felder. Übersprungene Felder verbrauchen
  weder einen Slot noch eine Zahlungsphase. Native Gratis- und Editormodi
  sowie der Zahlungsweg von Modus 6 haben getrennte Vorschauzweige.
- Graben: `0x90CD0 -> 0xEC130 -> 0x5CA40 -> 0x23990`, Chore 21.
  Die normale Variante behält Abstands-, Gelände-, Belegungs- und
  Kapazitätsprüfungen. `0x111D0` liest die Felder und ruft den
  Geländepfad `0x6FE90 -> 0x59210` zur Erzeugung des Auftrags auf.
  Die Editorvariante benutzt die Vanilla-Aktion für den fertig
  ausgehobenen Graben. Die Sonderprüfungen von ExtraFeatures für erhöhte
  Gräben bleiben im regulären Ausführungspfad; die Vorschau des
  fertig ausgehobenen Pinsels verwendet die Vanilla-Höhengrenze.
- Chore 21 packt X/Y als je 16 Bit, Breite als Byte, Operation als 32
  Bit, Variante als Byte. Chore 28 packt X/Y und Mapper als je 16 Bit,
  Maß und Variante als je ein Byte, abschließend ein 16-Bit-Feld.
  Der Mod serialisiert keines dieser Pakete. Er prüft die freie Queue
  vor der normalen Eingabe und beobachtet anschließend Zähler und
  Nutzdaten. Als abgeschlossen gilt der Auftrag erst nach einem neuen
  Pech- oder Grabeneintrag an der erwarteten Position und mit dem
  erwarteten Besitzer. Ein Chore wird niemals bei fehlender Bestätigung
  erneut gesendet.

Die Verarbeitung nutzt den nativen Kartentick über APIShared `SimulationClock`.
Pro fortschreitendem Tick wird höchstens ein Feld eingereicht. `DLL_RunTick`
bei `0x86680` führt fällige Chores über `0x1F7E0` aus, bevor `0xCDE60` den
Kartentick erhöht. Nach bestätigtem Feld kann der nächste Engine-Aufruf daher
das nächste Feld mit dem aktuellen Vorrat prüfen und einreichen. Die separate
32-ms-Pause entfällt. Wiederholte Engine-Aufrufe ohne Tickfortschritt senden
kein weiteres Feld. Die native lokale Choreverzögerung wird nach dem
Partiestart normalerweise null; der Mod verändert sie nicht.

## Pech und geplanter Graben auf demselben Feld

`0x5CA40` und der Grabenplanpfad `0x6FE90 -> 0x59210` lassen das Pechflag
stehen. Der Pechvalidator `0x77E60` akzeptiert eine Grabenmarkierung. Beim
Commit entfernt `0x6D580` über `0x61ED0` den Grabeneintrag und beide Grabenflags,
bevor `0x59380` Pech platziert. Der fertige Graben bleibt durch die Pechprüfung
gesperrt. Fertiggraben bei `0x639C0` und der fertige Editorgraben bei `0x6FE90`
entfernen Pech weiterhin nativ.

Der optionale Hook bei `0x6D788 + 16` ersetzt die Belegungsabfrage, den Test
und den Zweig zu `0x6D8DF`. Er führt Abfrage und Test aus und erhält die
resultierenden Flags und die Scratch-Register. Der Pech-Eintrag der
Sprungtabelle `0x6DFF0/0x6DFE8` führt bei belegtem Feld ebenfalls nach
`0x6D8DF`. Das ist auch der Einstieg des BuildingPrepare-Hooks von
BugfixesAndQoL. Der unbeteiligte Belegungszweig führt weiter zu `0x6D798`.
Nur der bestätigte Chore 28 für ein einzelnes Pechlinienfeld auf einer
konsistenten Grabenmarkierung verzweigt zu `0x6D912`. Das erhält den
Grabenauftrag samt Besitzer, Arbeitsfortschritt und gegebenenfalls den von
MoatTerrain gespeicherten Boden. Der Pechwriter und die Vanilla-Abbuchung
laufen weiter. Für Fremdbefehle bleibt der reguläre Nachfolger maßgeblich.

Vor `0x23990` wird der freie Slot aus dem Allocator-Vertrag `0x20950`
bestimmt. Chore-ID und Slot werden mit Absender, Ausführungsrichtung,
Pechmapper, Breite, Variante und Koordinaten in `0x12090` korreliert. Der
Slot enthält den Payload auch beim unmittelbaren Ausführungspfad, weil
Opcode 28 mit dem expliziten Slotparameter von `0x1F5F0` arbeitet. Ein
abgeschickter Auftrag behält die Zuordnung bei Werkzeugwechsel oder Abbruch
bis zur Ausführung oder bis die Queue ihn verwirft. Der optionale Eingriff
bleibt bei unbekannter Hookbelegung und im echten Multiplayer inaktiv.

## Nativer Eingriff und Lesezugriffe

| RVA / Layout | Verwendung und Beleg |
| --- | --- |
| `0x923AB + 18` | Nur eigener Schreibbereich. `89 0D A7 B0 01 06` schreibt ECX nach `0x60AD458`, `81 FA 1F 03 00 00` vergleicht EDX mit der Rastergrenze, `0F 87 2E FC FF FF` verzweigt nach `0x91FEB`. Der Fall-through bei `0x923BD` beginnt mit `48 63 05 0C FA 97 03`. Disassembly der ausführbaren Sektion zeigt keine direkten eingehenden Kanten in die Spanne. Die Verzweigung aus der Mapperentscheidung landet vor dem Hook. |
| `0x92390..0x923AB`, `0x2D1C38`, `0x69850` | Vanilla berechnet Breite 3 oder 5 anhand des Shift-Bytes. Die Brush-Tabelle liefert für Breite 1 eine Kachel. Die Koordinatenversätze werden nach dem geprüften nativen Brush-Vertrag von `0x69850` angewandt. |
| `0x77E60`, `0x5CA40`, `0xEC130`, `0x6C990`, `0x6C9D0`, `0x107160` | Direkte Aufrufe der Live-Einstiege für Pechprüfung, normale Grabenprüfung, Erreichbarkeit des Keeps, freie Graben-/Pechslots und im Editor entfernbares Geländeobjekt. Jeweils an Prolog im unveränderten Abbild und native Signatur gebunden. |
| `0x695C0`, TileManager `+0x204C6C4` | Schreibfreier Pech-Lookup, Win64 `int(TileManager*, int packedTileId)`. Prolog `4C 63 91 C4 C6 04 02 41 B9 01 00 00 00 4D 3B D1`. Die vorzeichenbehaftete exklusive Suchgrenze wird gegen die Länge der Extender-Slotansicht geprüft. Vanilla beginnt bei Slot 1, vergleicht die gepackte Tile-ID und prüft den Besitzer als Belegungssignal. Rückgabe 0 bedeutet kein Eintrag. Positive Ergebnisse werden zusätzlich gegen Suchgrenze, Koordinaten und gültigen Besitzer geprüft. Die Extender-Ansicht `GetPitchSlotLookup()` mit 4.000 Elementen ist keine nach gepackter Tile-ID adressierbare Kartenebene. |
| `0x405EDB0`, `0x366C210`, `0x60AD660`, `0x32DE440` | Tile-, Player-, Pfad- und Vegetationsmanager aus den Vanilla-Aufrufen. Tile- und Playerbasis zusätzlich gegen die Extender-API abgleichen. |
| `0x3A11EA4`, `0x3A11DCC`, `0x3A11DD0` | Aktivitätsmatrix mit 800 Spalten und Eingabekoordinaten. Der Dispatcher schreibt die Koordinaten, die Grabenprüfung prüft diese Matrix. Gepackte Tile-IDs danach über den Extender ermitteln und gegen die tatsächliche Kapazität prüfen. |
| `0x60AD45C`, `0x88E3D70`, `0x8EBA9F` | Aktiver Mapper aus der Werkzeugauswahl, aktueller Spieler und Shift-Byte aus `DLL_RunTick`. `DLL_SetEditorPlayer` bei `0x872E0` setzt für reguläre Spieler-IDs denselben Spielerwert unter dem Engine-Monitor. `DLL_MapAction` setzt ihn vorübergehend auf den Auftragsspieler. Der Hook verlangt Thread, Mapper, Spieler und genau dieselbe Kachel. |
| `0x3665F28`, `0x3665ED8`, `0x3665FC8`, `0x366A0F8` | Nativer Modus, Map-Untertyp für den Editor-Eigentümer, Gratisbau- und Eingabesperre. Gelesen aus den Branches von `0x8BD60`, `0x12090`, `0xCC420/0xC8A90`, `0x90CD0`. |
| `0x379D04C + playerId * 0x583C`, `0x37EF8F8` | 16-Bit-Phase aus `0xCC57B` und `0xC8A90`, Reichweite aus dem Aufruf von `0xEC130`. Spieler-IDs sind im nativen Record 1-basiert. |
| TileManager `+0x204E6FC..+0x204E78C`, `0x60AD4B4`, `0x34A9F5C` | Platzierungs-, Pinsel-, Ablehnungs- und Abstandszwischenwerte für die Vorschau. Der Mod stellt diese Ergebnisse nach seinen Abfragen wieder her. Die nativen Pfadsuchfunktionen behalten ihren eigenen Scratch-Zustand für die nächste Suche. |
| TileManager `+0xD7E5A0`, `+0xA6F260`, `+0xB0BCA0` | Höhe, Vegetationsindex, Strukturindex. Byte bzw. 16-Bit-Felder im Grabencommit von `0x6FE90`. Pech- und Grabenbelegung aus der Extender-API. |
| `0x8574320` und `+0xDA0/+0x370BF4/+0xB0BF8` | Chore-Manager, Schreibsequenz, Cursor und Queue. `0x20950` sucht 500 Slots zu je `0x500` Bytes, Status bei Slot `+9`, frei bei 0 oder signed >=10. `0x23A16` erhöht die lokale Sequenz. Manager zusätzlich über Extender-Adresse prüfen. |
| `0x86C132C..0x86C133C` | Fünf Staging-Dwords für X, Y, Maß/Mapper, Operation/Maß und Variante aus `0x90CD0`, gelesen von `0x111D0` und `0x12090`. |

Der generierte RedBird-Stub erhält die GPRs. Nur ECX ändert sich beim
passenden, threadlokal markierten Auftrag. Der verdrängte CMP läuft
danach und erzeugt die Flags für den verdrängten JA erneut. Ohne
passenden Auftrag bleibt die Breite unverändert. Nach Veröffentlichung
bleibt der Stub bis zum Prozessende installiert. Es gibt keine
Überschneidung mit dem Extender-Validator bei `0x77E60` oder dem
ExtraFeatures-Höheneingriff innerhalb von `0x5CA40`. Der Fixes-Quellbaum
hat für die exklusive Spanne keinen Eingriff. Die tatsächliche Spanne
wird zusätzlich im Registry und gegen Live-Bytes geprüft.

## Nach einem Update

Die Referenz-RVAs sind absichtlich hashgebunden. Ein Treffer für den
Breitenstore allein beweist weder das Layout der Vorschau und der
Chore-Queue noch die Semantik der vier Versand- und Zahlungsvarianten.
Daher gibt es keinen Scan-Fallback und keine Freigabe bei abweichender
DLL oder abweichendem RedBird/Iced. Erneut prüfen: beide Hooknachfolger,
alle eingehenden Kanten, Relokation und Register-Liveness, bestehende
Mod-Hooks, Managerbasen und Live-Exports, Chore-Versand und
Terminalzustände, Reichweite/Brush sowie vollständigen Preis- und
Vorratsfluss. Die statische Prüfung ersetzt keinen Spieltest.
