# MoveMoatTest – Erkenntnisse und Übergabestand

Stand: 5. September 2026

## Aktueller Übergabestand: besetzte Treppen, gemischte Starts und Platzvergabe

Diese Reparatur baut auf dem Cursor-Build `9AF6787D27D3137FD72DAF50E90A1BFAD59F3FE98CDED2C4FFCE9D6AEABE3FCF`
auf. Die unten folgenden älteren Übergaben bleiben historische Belege. Insbesondere die
bisherige Erklärung des direkten Klickadapters wird durch die folgenden Nachweise ersetzt.
Ziel bleibt Script Extender **1.42.0**, Modversion **1.0.0**; README und öffentliche QoL-Bridge
werden nicht geändert. Die alternative Umsetzung wurde ausdrücklich mit dem Nutzer besprochen.

### Befund des Tests vom 05.09.2026, 16:09 bis 16:12

- Die Cursorbedingung `!occupiedByLivingUnit || hostileUnitTarget || hostileBuildingTarget`
  ließ eine freundliche lebende Einheit auf einer Treppe die zusätzliche Bewegungsprüfung
  verhindern. Begehbare Strukturziele nehmen jetzt auch bei freundlicher Belegung an der
  gerichteten Regionsprüfung teil. Waffen-/Angriffsentscheidungen bleiben nativ.
- `SelectOwnerSafeGroupMoatMode` kehrte zurück, sobald `vanillaFirstMoat != lead` war.
  Damit wurde gerade die Kombination Boden-/Inselführer und späteres Moat-Mitglied nicht
  qualifiziert. Diese Gruppe kann jetzt den notwendigen gemeinsamen Moat-Zweig erreichen;
  bei einem Moat-Führer bleibt der geeignete native gemeinsame Unit-Zielzweig erhalten.
- Die Klickzeilen um 16:11:29.602, 31.546, 33.092, 35.982 und 51.221 bestätigen eine
  freundliche Verbindung zwischen Region 3 und 1. Fehlende `move-command`-Zeilen beweisen
  jedoch NICHT, dass der Auftrag nicht eingereiht wurde: `FlushCommandDiagnostics` konnte
  frühe schnelle Abbrüche ohne als moat-relevant markierten Plan vollständig verwerfen.
  Gruppen mit grabfähigen Einheiten und null Unit-Aufrufen werden nun ebenfalls ausgegeben.
  Die konkrete Abbruchstelle dieser historischen Klicks bleibt ohne damalige Marker offen;
  der nachgebildete native Zweig bestätigt den oben genannten Codefehler.
- Der Befehl `commandSeq=55` um 16:12:08.340 hat 62 Einheiten und ein Moat-Ziel in PCL 0;
  `floodCalls=0` passt zum gemeinsamen Zielzweig. Dessen Code weist jedem Mitglied exakt
  dieselben Koordinaten zu. Ein erfolgreicher Builder bestätigt keine räumliche Verteilung.

### Hashgebundene native Verträge und Eingriffstiefe

Die installierte DLL wurde erneut gegen CURRENT.json und die Datensätze geprüft:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die reproduzierbare Prüfung liegt unter
`_inspect/MoveMoatRegressionTests/Validate-PlacementContracts.py` (PE/Capstone, nur lesend).

- `0x195E30` ruft `0x23990` mit Chore-Opcode `0x11` VOR dem Start-Moat-Test auf.
  Der Dispatchtabelleneintrag führt nach `0x10AE0`, beim Ausführen weiter nach
  `0x196100 -> 0x11B520`. Die nachfolgende Startprüfung gehört zur unmittelbaren
  Klickrückmeldung, nicht zum Nachweis einer ausgeführten Bewegung. Das vorübergehende
  Löschen des echten Moat-Terrainflags wurde entfernt. Der Rückmeldungsadapter nutzt
  Regionsverbindungen und darf keine verschachtelte Befehls-/Arbeits-/Unit-Prüfung übernehmen.
- `0x11B520` verlangt für die normale Formation eine positive Ziel-PCL. Der alternative
  Zweig `0x118E00` schreibt je Unit `manager + id*0x490 + 0x934/0x936` und ruft bei
  `0x118FB9` die echte Unit-Bewegung `0x196280` auf. Diese Felder entsprechen bei
  `GameUnit`-Basisoffset `0x65C` genau `r_AttackMoveToTargetTileX/Y` (`0x2D8/0x2DA`).
  Die verwalteten Offsets werden beim Start zusätzlich geprüft.
- Die native spätere Entzerrung `0x181890` prüft die Belegung des aktuellen Feldes und
  ruft auf Boden `0xF03C0` bei `0x18195E`, auf Strukturen separat `0xF0710` auf.
  Bei einem gefundenen Feld folgt `0x196280` bei `0x18198E`. `0xF03C0` verlangt unter
  anderem eine nichtnull Boden-PCL, die native Belegungs-/Sperrsicht `0x51D75F0` und ein
  freies Unit-Feld. Sein verwendeter Ausgabevertrag ist X/Y/Tile bei `pathManager+0x44/0x48/0x4C`.
  Der Aufrufer verwendet außerdem die laufende native Unit-ID `0x9302C4`; diese muss zur
  gebundenen Unit passen. Struktur- und Teleport-Sonderzweige werden nicht ersetzt.

Es kommen drei Funktionsanfang-Detours hinzu, kein Hook auf `0x196280`. Vollständige,
mindestens 14 Bytes überdeckende Instruktionspräfixe sind:

| Einstieg | Ende exklusiv | Präfixinstruktionen |
| --- | --- | --- |
| `0x118E00` | `0x118E0F` | drei `mov [rsp+8/10h/18h], rbx/rbp/rsi` |
| `0x181890` | `0x18189F` | zwei Sicherungen von RBX/RSI, `push rdi`, `sub rsp,30h` |
| `0xF03C0` | `0xF03D1` | Sicherung von EDX, `push rbx/rsi/rdi`, `sub rsp,40h`, `inc [rcx+A0h]` |

Die Prüfung kontrolliert die vollständigen Präfixbytes, Endadressen, eindeutige Patterns,
Funktionsdatensätze und Aufrufketten. MonoMod führt die Originalinstruktionen im Trampolin
aus; kein eigener Ersatzblock verwendet Scratchregister. Win64: RCX/RDX/R8/R9 enthalten
die ersten Argumente, die übrigen Gruppenargumente liegen im Stack; Return ist RAX bzw.
void. Stack/Shadow-Space und nichtflüchtige GPR/XMM-Register werden über den Delegate-ABI
erhalten. Es gibt keinen benötigten eingehenden Flagwert. Die beobachteten Rücksprünge
verwenden eigene Tests/Loads, nicht vom ersetzten Suchaufruf erzeugte Flags. Die gespeicherten
Register werden vor Clobbern im Originalpräfix gelesen. Die vollständigen Call-/Byteangaben
gibt das Prüfscript aus; die kanonische Baseline bleibt unverändert.

### Platzierung, Sucharbeit und Rücksetzung

`MoatPlacementSearch` ergänzt nur fehlende individuelle Ziele im synchron gebundenen
gemeinsamen Gruppenzweig. Native Unit-Reihenfolge und Gruppen-/Patrol-Wegpunkte bleiben
erhalten. Die nächste freie Position wird deterministisch in Breitensuche gesucht;
freundliche Moats UND benachbarter erreichbarer Boden sind erlaubt. Gerichtete Kanten,
Höhen, Tore, fremde Moats, aktuelle Belegung und native Reservierungssicht werden geprüft.
Fehlender freier Platz erzeugt keinen Ersatzauftrag: der ursprüngliche Zielpunkt durchläuft
weiter die individuelle native Bewegung und den bestehenden vollständigen Pfadaudit.

Unterschiedliche Ziele teilen einen fortsetzbaren gerichteten Regionsnachweis: Erreicht die
Unit den Anker und der Anker den Kandidaten, ist die Verbindung bewiesen. Der Anker wird
NICHT als Bewegungswegpunkt eingesetzt. Andere gerichtete Verbindungen bleiben durch eine
eigene Regionsentscheidung möglich. Es werden keine Kosten, Pfadpuffer oder Unit-Pläne
zwischen Einheiten geteilt. Cursor- und Platzierungs-Suchknoten haben getrennte Zähler.

Reservierungen bleiben bei einem Suchdaten-Neuaufbau erhalten. Epoch/Tick, Global-ID,
Spieler und Aufrufkontext verhindern eine Übernahme durch andere Units oder Arbeitszyklen.
Bei Abbruch werden eigene Zielfeldänderungen zurückgesetzt, fremde spätere Schreibzugriffe
bleiben erhalten. Veränderte Pre-Argumente werden bei der ersten nativen Verwendung nochmals
gelesen: nachdem Vanilla sie bereits in Register kopiert hat, dürfen sie nicht nachträglich
auf das Klickziel zurückgeschrieben werden. Die eigenen Zielfelder folgen dann dem tatsächlichen
Argument; die alte Platzreservierung wird freigegeben. Bereits native Pfade behalten ihren
separaten Puffer-, Kanten- und Eigentümeraudit samt Rollback.

Automatische Entzerrung nutzt ausschließlich Vanillas bestehenden Aufruf für eine tatsächlich
überlappende geeignete Unit auf freundlichem Moat. Kein neuer Tick-Scan und keine Arbeit durch
Auswahländerung. Der Freifeldersatz gilt einmal pro gebundenem Aufruf; verschachtelte Aufrufe
übernehmen keinen fremden Suchkontext. Daten werden höchstens innerhalb desselben Ticks und
derselben Karten-/Topologierevision wiederverwendet; Belegung wird immer neu gelesen.

**Alternative bei unzureichendem Spielergebnis:** statt der begrenzten eigenen Platzvergabe
Vanillas Formations- und Freifeldsuche gezielt um Moat-PCL-Ausnahmen erweitern. Dieser Weg
verlangt mehr Eingriffe in gemeinsame native Suchpuffer und Strukturzweige. Er ist ausdrücklich
als nächste Alternative festgehalten, aber nicht als paralleler Runtime-Fallback eingebaut.

### Tatsächliche Prüfungen und noch offene Spielabnahme

- Gesamter Runtime-Code gegen lokalen SE-Tag `v1.42.0` und installierte Release-Assembly
  `1.42.0` semantisch geprüft. Der Roslyn-Prüfer meldet die bereits bekannte CS1701-Zuordnung
  zwischen BepInEx/mscorlib-Versionen, keine Quellfehler; er erzeugt keine Spiel-Modassembly.
- 161.320 Runtime-/Platzierungsassertionen, zusätzlich 6.792 unabhängige Pfadsuchassertionen
  und 1.469.340 gerichtete Cursorgraph-Vergleiche bestanden. Platzierung und Entzerrungsadapter
  werden als tatsächliche Produktionskomponenten mit simulierter nativer Aufrufreihenfolge
  geprüft. 100 kleine gerichtete Karten vergleichen Kandidaten und Ankerbeweise unabhängig.
- Die Produktionssimulation liefert bei 1/5/20/27/29/120 Einheiten entsprechend viele
  unterschiedliche freie Ziele. Im 120er-Korridorfall: 119 Kandidatenknoten, 117 zusätzliche
  Verbindungsknoten, rund 24,6 ms und 406.168 Bytes für den gemessenen Simulationsabschnitt
  einschließlich Unit-Plänen, Pfadveröffentlichungen und Testprüfungen. Das ist keine
  Ingame-Laufzeit; der erste kalte Regionsnachweis verursacht zusätzliche Arbeit.
- Belegte Strukturen, veränderte Argumente, Fehl-/Skip-Rückgaben, fehlendes Post, verschachtelte
  Unit-Aufrufe, Global-ID-Wechsel, laufende Schritte und Reservierungen nach Revision geprüft.
  Bestehende Fremdpuffer-, Eigentümer-, Fill-, Struktur- und Cursorregressionen bestehen weiter.
- Cursor bleibt ohne Wegsuche. Die vollständige Auswahl-/Cursor-Simulation allokiert für
  100 Abfragen 52.000 Bytes unabhängig von 1/120/1.000 ausgewählten Units; der isolierte
  Regionsadapter bleibt bei wiederholten unveränderten Abfragen ohne neue Allokationen/Knoten.

Neue Runtime-Marker: `placement hooks installed`, `stage=placement`, `placementBatches`,
`placementSlots`, `placementRollbacks`, `unstackCalls` und `unstackMoves`. Eine positive native
Entzerrungsrückgabe ist noch keine beobachtete Ankunft. Im Spiel stehen besetzte Treppen ohne
Bodenalternative, beide gemischten Gruppenführer, Moat-Verteilung einschließlich Platzmangel,
spätere Entzerrung, Queue/Patrol, KI, Dig/Fill, Angriffe und Host/Client noch zur Abnahme aus.
Lagfreiheit und tatsächliche Bewegung dieser neuen Version sind daher noch nicht bestätigt.

Abschluss dieser Reparatur: Nach der Modellunterbrechung wurde der gespeicherte Stand erneut geprüft
und die vollständige Regression erfolgreich wiederholt. Native Vertragsprüfung und CRLF-Kontrolle
sind bestanden. Am 05.09.2026 um 18:00:24 wurde `build.bat /nopause` einmal direkt erhöht
ausgeführt: **0 Warnungen, 0 Fehler**, Build und Installation erfolgreich. Lokale und installierte
Mod-DLL stimmen per SHA-256 überein: `32F875CF79656F049BC025149871EDCB109656A55F38BFC452059F627B693787`.
Modversion bleibt `1.0.0`; README und Script Extender wurden nicht verändert. Die oben genannte
Spielabnahme bleibt offen; diese Prüfungen ersetzen keinen Ingame-/Host-Client-Nachweis.

## Aktueller Übergabestand: Cursoranbindung korrigiert, 5. September 2026

Dieser Abschnitt ersetzt insbesondere die vorherigen Aussagen zur funktionierenden
Auswahlquelle. Die damalige Testattrappe hatte denselben falschen Namespace wie der Mod;
die Modellmessungen bewiesen deshalb keine Anbindung an die echte Spielauswahl.

### Ursachen und native Belege

Die Logzeilen vom 05.09.2026 um 15:27:28.076 und 15:27:33.077 melden `selected=0`, obwohl
Einheiten ausgewählt waren. Gesucht wurde `CrusaderDE.EngineInterface`; die Spielklasse
heißt **EngineInterface im globalen Namespace**. In der installierten Assembly-CSharp liegt
`selectedChimps` als **private static int[]** vor. Der SE-1.42.0-Wrapper liest daraus die
Game-IDs bei `i * 2`. Sein `using CrusaderDE` benennt nicht den Namespace dieser Klasse.
Der funktionierende Gebäudeadapter erhielt seine Unit-ID direkt.

Die installierte verwaltete Spielassembly hat SHA-256
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`, die native DLL weiterhin
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Beide stimmen mit der
zugehörigen Baseline überein. Im Standard-Unit-Angriffszweig von `0x8C5F0` prüft Vanilla die
PCLs von Angreifer und tatsächlichem Unit-Ziel. Bei Bedarf folgen `0x196870` und `0xE2CA0`
mit physischem Ziel-Tile und Start-Tile (`useCache=1`). Eine eigene Acht-Nachbar-Auswahl
gehört dort nicht zum Cursorvertrag. Die Waffen-/Befehlsentscheidung bleibt in der nativen
Fallunterscheidung. Gebäude `0xB70C0` und der spezielle `0xB72C0`-Zweig bleiben getrennt.

Zweiter bestätigter Fehler: Die direkte Cursorprüfung sperrte mit `0x10000100` pauschal
Strukturziele vor der gerichteten Erreichbarkeitsprüfung. Außerdem verwarf die Knotenbildung
Strukturendpunkte ohne ausgehende Maske, obwohl eine gültige eingehende Kante sie erreichen kann.

Die fünf Gruppenbefehle im letzten Lauf erreichten alle Builder: 1/1, 19/19, 1/1, 4/4 und
20/20 Unit-Aufrufe/Builder, jeweils ohne Vertragsablehnung. 53 von 54 Bewegungsabschlüssen
melden `path-completed-at-target`; keine Zeile meldet `ownerSafetyViolation=True`.
Der Vor-Builder-Recovery-Zähler blieb null, diese besondere Recovery wurde damit nicht
im Spiel nachgewiesen. Getesteter vorheriger Mod-Build:
`5D60272E0B2F108E28CF13F0041026B272950790A64649EDE660B38A4BCDA465`.

### Implementierte Reparatur

- Auswahlfeld einmalig in Assembly **Assembly-CSharp**, globalem Typ **EngineInterface**
  auflösen; statischen `int[]`-Vertrag und private/publicized Sicht berücksichtigen.
  Fehlende Quelle und leere Auswahl sind getrennt. Ein Bindungsfehler erlaubt keine
  Mod-Freigabe. Wiederholte Abfragen behalten ihre Arrays und Identitätstoken.
- Pauschalen Cursor-Strukturblock entfernen; niedrige Sperrflags und Zielverfügbarkeit
  bleiben. Gerichtete Strukturendpunkte dürfen ausschließlich eingehende Kanten besitzen;
  das erzeugt keine ausgehenden Wege. Höhen, Portalzustände und Besitzer bleiben geprüft.
- Unit-Angriffscursor an das tatsächliche Unit-Ziel binden. Dessen Belegung ist kein
  Bewegungsziel-Verbot. ID, Global-ID, Standort, Leben und Feindbeziehung bleiben geprüft;
  Sprite-Tile und physischer Tile bleiben getrennt. Tatsächliche Angriffs-/Arbeitsannäherungen
  behalten ihre bisherigen Suchen.
- E9D90/E9FF0, Gebäudevorschau, Sondertypen und Befehlsausführung behalten ihre Verträge.
  Keine neuen Hooks, keine Änderung am Extender oder an der öffentlichen QoL-Bridge.
- `cursor-decision` unterscheidet fehlende Auswahlquelle, ungültiges Bewegungs-/Angriffsziel,
  fehlende Verbindung und native/ungebundene Konsumenten. Details erscheinen einmal je
  Kategorie und Karte; `cursor-performance` enthält zusätzlich aggregierte Kategorien.

### Tatsächliche Tests und Abschluss

Die Standalone-Prüfung liest direkt die Metadaten der installierten Spielassembly:
Assemblyname, globaler Typ, Feldname, Attribute und Signatur. Die simulierte Assembly hat
denselben Namen und ein globales EngineInterface mit privatem Auswahlfeld. Der neue Test
beginnt in der tatsächlichen Produktionsmethode `ObserveCursorTilePairFallbackSelection`
und durchläuft Gruppenaufnahme und Scope-Erstellung bis zur E2CA0-Antwort und Auswahl der
nativen positiven/negativen Cursorfortsetzung. Der Scope wird nicht manuell vorgegeben.

Geprüft: Boden ohne Umweg, zwei freundliche Moats mit konsistent getrennten nativen PCLs,
feindliche Sperren, gerichtete Strukturendpunkte, unzulässige Höhe, blockierte/fremde und
wieder erlaubte Portale, belegtes Unit-Angriffsziel mit Sprite-Versatz, bewegte/tote/freundliche
Ziele, ID-Wiederverwendung und gemischte/nativ spezielle Auswahlen. Die vollständigen bisherigen
Bewegungs-/Fill-/Pufferregressionen bleiben Teil des Laufs.

Vollständiger Cursorablauf mit 1/120/1.000 Einheiten: jeweils 100 Abfragen ohne Pfadsuche und
mit konstant 52.000 temporären Bytes (520 je Aufruf, unabhängig von der Gruppengröße).
Die isolierte wiederholte Auswahl-/Regionsabfrage bleibt bei 0 temporären Bytes. Das sind
Desktop-Modellwerte, keine gemessenen Unity-/Mono-Framezeiten. Geprüft werden 14 Runtime-Dateien
und 113 tatsächliche Member: 3.364 Runtime-Assertions, 6.792 unabhängige Suchprüfungen und
1.469.340 unabhängige Graphvergleiche. Genaue Ergebnisse:
`_inspect/MoveMoatRegressionTests/latest-regression-results.txt` und `latest-native-contract.txt`.

Version **1.0.0**, Ziel/Referenzen **Script Extender 1.42.0**, README unverändert.
Buildabschluss dieser Cursorreparatur: `build.bat /nopause` am 05.09.2026 um 15:53:22
einmal direkt erhöht ausgeführt; Build und Installation erfolgreich, **0 Warnungen/0 Fehler**.
DLL, PDB und info.json sind lokal/installiert bytegleich. Neue Mod-DLL SHA-256:
`9AF6787D27D3137FD72DAF50E90A1BFAD59F3FE98CDED2C4FFCE9D6AEABE3FCF`. API-/Metadaten-/Native- und CRLF-Prüfungen bestanden.
Die erneute Spielabnahme muss Bewegung, Unit-Angriff und Treppe/Torhaus jeweils über eigenen
Moat ohne Bodenalternative bestätigen: passender Cursor, ausführbarer Befehl und spätere
Bewegung beziehungsweise Angriff. Die bereits beobachteten Bewegungen stammen vom vorherigen Build.

## Historischer Übergabestand: Cursorregionen, Vor-Builder-Abbruch und Fill, 5. September 2026

Dieser Abschnitt ersetzt die entsprechenden Annahmen des folgenden historischen Stands.
Insbesondere ist eine positive native Regionsantwort allein **kein** Beweis, dass Vanilla
anschließend einen Builder erreicht. Die Reparatur ist im Code umgesetzt; ein neuer Spieltest
mit diesem Build steht noch aus.

### Ausgangslauf und zusätzliche native Befunde

Der untersuchte letzte BepInEx-Abschnitt enthält unter anderem Gruppen mit 18 Buildern für
20 Unit-Aufrufe und 74 Buildern für 80 Aufrufe. Erfolgreiche veröffentlichte Pfade erklären
nicht die fehlenden Aufrufe. Der bisherige Zähler `modeCalls` zählt Modusfreigaben und durfte
nicht als Anzahl aller nativen Modusaufrufe gelesen werden. Die neuen Zähler trennen dies.
Im Fill-Abschnitt bis `2026-09-05 13:00:48.131` wurden 291 Fallback-Auswahlen ohne einen einzigen
geprüften Annäherungskandidaten beobachtet (`checkedApproachTiles=0`, `searchBuilds=0`), bei
Moat-Datensätzen im Bereich 950 und höher. Die Grenze 799 war fälschlich auf Datensatz-IDs
angewendet worden; sie beschreibt ausschließlich Kartenkoordinaten.

Alle folgenden Adressen beziehen sich auf die kanonische installierte `CrusaderDE.dll`,
SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
`CURRENT.json`, das Datenbankmanifest, die semantische Baseline und die installierte Datei
wurden abgeglichen. Rohbaseline und Script Extender wurden nicht verändert.

- Cursor `0x8C5F0` verwendet `0x196870` und danach `0xE2CA0`. Letztere Funktion kann selbst
  `0xD9C40` mit 400.000 Suchknoten anstoßen. Erst nach ihrem Originalaufruf schnell zu prüfen,
  verhindert diesen Aufwand nicht. Die gebundene Cursorantwort muss vorher erfolgen.
- `0x196870` prüft die Auswahltypen; seine native positive Spezialauswahl bleibt erhalten.
  Insbesondere erhalten Assassinen durch den Mod keine Moat-Fähigkeit. Gemischte Gruppen
  teilen Entscheidungen nach Bodenregion beziehungsweise Spezialknoten und Grabfähigkeit.
- Gebäudevorschau `0xB70C0` hat den Cursor als belegten Caller. Ihre native Kandidaten- und
  Höhenprüfung bleibt erhalten. Ihre `0xE2CA0`-Abfrage verwendet `(Start, Kandidat, useCache=0)`,
  die direkte Cursorvorschau `(Ziel, Start, useCache=1)`. Beide Kontexte sind ausdrücklich getrennt.
- `0xE9D90` ist eine Struktur-Austrittsprobe; `0xE9FF0` schreibt zusätzlich unter anderem
  Austrittsdaten bei Pfadkontext `+0x34/+0x4C`. Beide liefern wieder unveränderte native
  Rückgaben und Ausgaben. Die bisherigen pauschalen Bool-Freigaben sind entfernt.
- `0xE2610` kann in seiner zweiten Phase einen blockierten Portalweg positiv melden.
  Der Vorprüfadapter sichert/restauriert deshalb `+0xC0`, `+0xC4` und `+0x98`; nur positiv
  **ohne** Blockierungskennzeichen gilt als normale Bodenfreigabe.
- Die native Fill-Auswahl `0x69D60` ruft `0x6C490` vor Auswahl und Reservierung auf.
  Dessen Höhenprüfung ist an die Höhe des Moat-Kontaktfelds gebunden, nicht an die Höhe
  der entfernt stehenden Einheit. Reservierungsschritt bleibt nativ einmalig 20.

### Umsetzung und Kostenverhalten

`CursorRegionGraph.cs` speichert gerichtete Verbindungen mit Referenzzählern und eine
wiederverwendete rückwärtige Erreichbarkeitsmenge je Ziel. Er enthält keine Pfadkosten,
Vorgänger, Pfadkodierung oder Unit-Puffer. `CursorConnectivity.cs` bindet native Boden-PCLs,
freundliche Moat-Felder und gerichtete Strukturübergänge an diesen Graphen. Die Moat- und
Strukturkanten benutzen die bestehenden geprüften Traversierungsregeln einschließlich Höhe
und Diagonalecken; fremder Moat verbindet keine Regionen. Normale unterschiedliche PCLs
werden nicht allein wegen benachbarter Felder verbunden: Dafür gelten die nativen Portale.

Ein Ziel in derselben normalen Bodenregion wird ohne Aufbau der zusätzlichen Moat-Topologie
beantwortet. Sonst erfolgt ein vollständiger Aufbau bei Bedarf pro Spieler/Karten- bzw.
Regionsgeneration. Lokale Änderungen aktualisieren die betroffenen Randkanten. Beobachtet
werden die nativen Maskenänderungen, Moat-Schreiben/Löschen und vollständigen Maskenneubauten;
Portalzustände und die Bündnismatrix werden aktuell gelesen. Grundlegende PCL- oder
Diplomatieänderungen verwerfen den betroffenen Gesamtstand. Native Kartenlayer bleiben unverändert.

Die Auswahl wird ohne `GetSelectedChimps()`-Kopie und ohne Sortierung aus derselben
verwalteten Auswahlquelle gelesen, die der geprüfte 1.42.0-Wrapper benutzt. Wiederverwendete
Arrays vergleichen Game-ID, Global-ID, Position, Spieler, Typ und Lebenszustand. Ein anderer
Tribe allein löst weder einen Kartenaufbau noch eine Wegsuche aus. Bei unveränderter Auswahl
bleiben Puffer und kompakter Revisionstoken bestehen. Gruppenquellen werden nach Region und
Fähigkeit zusammengefasst; ein wiederverwendeter Prüfscope verhindert eine neue Scope-Allokation
pro Quelle. Gebäudediagnosen verwenden begrenzte numerische Schlüssel, keine Vollsignaturen
oder Detailstrings bei jeder Vorschau. Ziele und Angriffsidentitäten werden weiterhin aktuell
geprüft; die native Cursorlogik entscheidet nach der Erreichbarkeitsantwort über die Befehlsart.

Gebundene direkte und Gebäude-Cursorabfragen beantworten jetzt positive **und negative**
Erreichbarkeit vor dem nativen Flächensuchaufruf. Tatsächliche Arbeits-/Angriffskonsumenten
behalten ihren nativen Vertrag. Rein diagnostische Angriffsausgaben lösen keine zusätzlichen
Wegsuchen aus; eine umgedrehte Suche gilt nicht mehr als Beleg der Vorwärtsrichtung.
Die 2.000-Schritte-Grenze betrifft nur später veröffentlichte Wege, nicht die Cursorregionen.

`NativeMovementRecovery.cs` erfasst den gemeinsamen Portalfehler vor dem Builder. Es führt
höchstens einen Übergang je gebundenem Unit-Aufruf aus, nur mit aktuellem Start/Ziel,
verfügbarer Zielfläche und vollständig auditiertem, kodierbarem freundlichen Moat-Pfad.
Der Pfad wird für den Builder behalten. Es entstehen keine Ersatzbefehle oder Retry-Schleifen.
Vanilla initialisiert selbst den individuellen Puffer und setzt den ursprünglichen Auftrag fort.
Beide Builder bleiben an Unit/Global-ID, Besitzer, Start, Endpunkt und eigenen Puffer gebunden;
bei gescheiterter Veröffentlichung greifen Kantenprüfung und Rollback. Die beiden vor dem
Übergang veränderten Unit-Regionsfelder werden bei negativer nativer Rückgabe zurückgesetzt.
Spezielle Arbeits-, Angriffs- und Probe-Kontexte werden nicht vom allgemeinen Regionshandler
übernommen. Alte Builderkoordinaten sind vor dem Builder kein Zielkontext.

Fill benutzt die zentrale Datensatzgrenze `1..63999`, zusätzlich `id < aktueller Bestand`,
Kapazität höchstens 64000 und passende Datensatzkoordinaten. Die verbesserte Auswahl läuft
in **einem** nativen Durchlauf: ungeeignete Annäherungen werden schon in `0x6C490` ausgeschlossen.
Das wiederholte Ausschließen über vorübergehend veränderte Reservierungen ist entfernt.
Auswahl und Resolver teilen ihre fortsetzbare Suche, spätere Arbeitszyklen erhalten frische
Daten. Belegung, Besitzer und Arbeitsziel werden erneut gelesen. Die exakt gebundene terminale
Fill-Kontaktregel bleibt die einzige feindliche Arbeitsausnahme. Ohne QoL-Provider ist die
verbesserte Fill-Auswahl aktiv; der vorhandene Providervertrag bleibt kompatibel.

### Neuer Maschinenvertrag

Die Beobachter verwenden reguläre Win64-Funktionsdetours mit exakten eindeutigen 40-Byte-
Eingangssignaturen und geprüften Unwind-Funktionsgrenzen:

| RVA | Vertrag | Kopierter Mindestbereich | Funktionsende |
| --- | --- | --- | --- |
| `D90D0` | `void(manager, int y, int tile)` | 15 Bytes | `D9185` |
| `59210` | `ulong(manager, byte owner, uint x, uint y, int mode, byte replace)` | 15 Bytes | `59371` |
| `61E70` | `void(manager, uint moatId)` | 16 Bytes | `61ECC` |
| `DAA50` | `void(manager)` | 15 Bytes | `DAAB8` |

Der Inline-Übergang überschreibt exakt `0x19664B..0x196659`, 14 Bytes:
`33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF`.
Original: `xor eax,eax; mov edx,esi; mov [rip+0x5F1708E],rax; mov rcx,r15`.
Kein Sprung landet im Blockinneren. Ungültige Ziele verlassen den nativen Aufruf anders
(`0x19676C`); ein bereits fehlgeschlagener Builder ebenfalls (`0x196734`).

Am Hook ist RSP 16-Byte-ausgerichtet. RSI, RDI, RBP und R12-R15 sind lebender nichtflüchtiger
Zustand; der verwaltete Win64-Aufruf erhält ihn. Flüchtige GPRs und Flags werden auf beiden
Fortsetzungen nicht benötigt; die vollständige Funktion verwendet keine XMM/YMM-Werte.
Der Adapter reserviert `0x30` Bytes einschließlich Shadow-Space. Er liest den nativen Start
bei neuem `RSP+0xA0/+0xA8`, bevor er Zielargumente bei `+0x20/+0x28` schreibt. RCX=R15,
EDX=ESI, R8D/R9D=Start, Argument 5/6=R14D/EBP. Nach Rückkehr wird RSP restauriert.
Bei Ablehnung werden alle vier Originalinstruktionen einschließlich RIP-relativer Ausgabe
verlegt ausgeführt; bei Freigabe geht es zu Vanillas Pufferinitialisierung `0x196585`.
Die vollständigen 73 erzeugten Adapterbytes werden in den Tests mit Iced assembliert und
zusätzlich mit Capstone geprüft. Es wird dabei kein nativer Code ausgeführt.
Unit-Daten beginnen bei Slot `+0x65C`; die Regionsfelder liegen relativ dazu bei
`0x900-0x65C` und `0x8EC-0x65C`. Kein weiterer Entry-Hook bei `0x196280` wird installiert.

### Tatsächlich ausgeführte Prüfungen und verbleibende Abnahme

Die Regression verwendet alle 14 Runtime-Quelldateien für die semantische Prüfung, den
vollständigen Produktions-Suchkern und Cursorgraphen sowie den vollständigen Cursoradapter
gegen simulierte native Daten. Zusätzlich werden 105 tatsächliche Runtime-Member in der
Aufrufsimulation kompiliert. Die wichtigsten Ergebnisse:

- 3.333 Assertions für Unit-Kontexte, beide Pufferzweige, Vor-Builder-Abbruch und Rollback,
  Slotwiederverwendung, veränderte Events, fehlendes Post, Arbeitsauswahl und Cursoradapter.
  Der Gruppenablauf wird auch für 120 verschiedene Unit-IDs simuliert.
- 6.792 unabhängige Suchprüfungen einschließlich gerichteter Kanten, Profilkonflikte,
  Längengrenzen und Terrainänderungen; 1.469.340 unabhängige Graph-Erreichbarkeitsvergleiche
  nach Hinzufügen/Entfernen gerichteter Verbindungen.
- Tatsächlicher E2CA0-Adapter: positive und negative Cursorantwort rufen die native Suche
  nicht auf; ungebundene und echte verschachtelte Angriffskonsumenten behalten sie.
  Die Gebäudeprüfung testet die umgekehrten Argumente und Wiederherstellung des Scopes.
- Produktionsadapter mit 1/120/1.000 ausgewählten Einheiten: jeweils 1.000 unveränderte
  Auswahl-/Cursorabfragen ohne neue Suchknoten, ohne Pfadsuche und mit **0 temporären Bytes**.
  Rund 1/4/23 ms für alle 1.000 Abfragen im .NET-Testprozess, nicht pro Abfrage.
  Die zusätzliche vollständige Gruppenqualifikation benötigt in allen drei Fällen konstant
  20.800 Bytes für 100 Aufrufe (208 Bytes je Aufruf), nicht einen Scope je Einheit.
- Tatsächliche Fill-Datensatzleser mit 799/800/32768/63999, ungültige Grenzen, Kontakt- statt
  Arbeiterhöhe, belegte Annäherungen, genau ein Kandidatenscan/eine Reservierung sowie eine
  erneute Auswahl nach Besitzeränderung. Ein Ziel hinter freundlichem Moat wird ausgewählt.
- Native Hash-, Pattern-, Instruktions-, ABI- und Registerprüfung erfolgreich. Der separate
  Roslyn-Referenztest meldet nur CS1701 zur alten BepInEx/MonoMod-mscorlib-Referenzfamilie;
  keine mod-eigenen Compilerwarnungen oder Fehler.

Die Rohresultate liegen unter `_inspect/MoveMoatRegressionTests/latest-regression-results.txt`
und `latest-native-contract.txt`; `latest-recovery-stub.bin` enthält den geprüften Adapter.
Die Suchleistungsmodelle zeigen weniger Knoten, aber keine garantierte Zeit- oder
Allokationsverbesserung für jeden Lauf. Insbesondere bleibt die exakte Abkürzungssuche bei
schwierigen Aufträgen potenziell teuer. Es wird weder Lagfreiheit noch eine Spielbeschleunigung
allein aus diesen Desktop-Testwerten abgeleitet. Der initiale Topologieaufbau und reale
Portal-/Terrainänderungen müssen ebenfalls im Spiel gemessen werden.

Der lokale Extender-Checkout ist weiterhin sauber `v1.42.0`, Commit
`171d68e155a8f98c5f8c4ee154d9af154c9a2443`, lokale Referenz-SHA-256
`80465F8E3658484CE2E7DEAD5B5C2C1118D4BA154C89CFEC1B6B55B456B221A0`.
Die inzwischen installierte Release-DLL ist ebenfalls **1.42.0**, aber nicht mehr bytegleich:
SHA-256 `27DB3535D9747E3C0532EB5B09A2821AE6C4C29AC30733D01246FFFD2421BEB4`.
Alle Runtime-Quellen wurden gegen beide API-Oberflächen geprüft. Der unveränderte Buildtreiber
bevorzugt die lokale 1.42.0-Referenz. Modversion **1.0.0**, README, Extender und öffentliche
QoL-Bridge bleiben unverändert.

Die Runtime bleibt durch die statische Plugin-Referenz und langlebige Events verwurzelt.
`Application.onBeforeRender` liefert auch bei pausierter Simulation begrenzte aggregierte
Cursormarker. `nativeModeEntries`, `preBuilderFailures`, `preBuilderRecovered`, begrenzte
`unit-no-builder`-Details und nach Ursache aggregierte Ablehnungen ergänzen die vorhandenen
Builder-, Veröffentlichungs- und späteren Bewegungsmarker.

**Spielabnahme offen:** 1/5/20/27/29/120 Einheiten, Starts auf Moat, gemischte Gruppen,
Shift-Queue, Patrol, KI, Angriffe, Dig/Fill-Folgezyklen, Strukturen/Tore, pausierte Vorschau,
Terrain-/Bündniswechsel und Host/Client. Die nativen Zwischenabbrüche und der Inline-Übergang
wurden statisch und simuliert geprüft, noch nicht mit diesem Code im Spiel beobachtet.
Erst tatsächliche spätere Bewegung und neue Arbeitszyklen bestätigen die Reparatur.

Buildabschluss dieser Runde: Am 05.09.2026 um 15:04:38 wurde nach allen Prüfungen einmal
die vorhandene `build.bat /nopause` direkt erhöht ausgeführt. Build und Installation
erfolgreich, **0 Warnungen, 0 Fehler**. DLL, PDB und info.json sind lokal/installiert jeweils
bytegleich. SHA-256 der Mod-DLL: `5D60272E0B2F108E28CF13F0041026B272950790A64649EDE660B38A4BCDA465`.
Die installierte Extender-DLL blieb unverändert. Alle geprüften Textdateien verwenden CRLF;
keine nackten LF oder versehentlichen wörtlichen Zeilenumbruchsequenzen. README unverändert.
Es wurde noch kein neuer Spielprozess und damit kein Laufzeiterfolg dieses Builds bestätigt.

## Historischer Übergabestand: gemeinsamer Suchkern, 5. September 2026


Der lokale Script Extender wurde erneut geprüft: sauberer Checkout `v1.42.0`, Commit
`171d68e155a8f98c5f8c4ee154d9af154c9a2443`. Die lokale Referenz-DLL und die installierte
`000shcdese/SHCDESE.dll` sind bytegleich, SHA-256
`80465F8E3658484CE2E7DEAD5B5C2C1118D4BA154C89CFEC1B6B55B456B221A0`.
Die Informationsversion enthält genau diesen Commit; die Assembly-/Bootstrap-Version `1.0.0`
ist kein Nachweis einer abweichenden Extender-Releaseversion. Die vollständige semantische
Quellprüfung verwendet diese lokalen 1.42.0-Referenzen, ohne eine Mod-DLL zu erzeugen.
Der Nutzer hat den früheren Buildstopp aufgehoben und nach Abschluss der Prüfungen den
vorhandenen Buildtreiber einschließlich Installation autorisiert. Das konkrete Buildresultat
wird unten im Abschnitt „Abschluss dieser Umsetzung“ festgehalten.

### Ursachen und endgültige Architektur dieser Reparaturrunde

Die unten belegten 29 Modusfreigaben bei nur einem zugeordneten Builder waren kein Problem
fehlender Gruppenbefehle: Das gemeinsame Klickziel wurde mit den individuellen Formationszielen
verwechselt. Zusätzlich war der Rekonstruktionszweig `0xE32B0` nicht vollständig erfasst.
Vanilla behält deshalb Gruppenbildung, Formationen, Queue, Patrol und Arbeitsfortsetzung;
der Mod erzeugt weiterhin keine Ersatzbefehle.

- `UnitMovementContext.cs` bindet die veränderbaren `OnUnitMoveHere`-Pre-Argumente erst bei
  Verwendung. Pro Aufruf bleiben Game-ID, Global-ID, Spieler, nativer aktueller/nächster Start
  und Auftragsziel individuell. Ein fremder Arbeits-Handoff wird weder geerbt noch verbraucht.
  Zwischenziele erhalten einen lokal qualifizierten Builderplan. Verschachtelte Aufrufe,
  SkipOriginal ohne Post, Befehlsabschluss, Karten- und Tickwechsel bereinigen die Kontexte.
- `MovementSearchContext.cs` verbindet diese Identität mit der Regionsprüfung und dem
  zweiten Builder. Die zusätzliche native PCL-Vorprüfung benutzt das bestehende originale
  Trampolin, schützt vor Rekursion und setzt alle belegten Seiteneffekte zurück. Eine positive
  Regionsantwort verschiebt eine nötige exakte Suche bis zum tatsächlichen Builderfehlschlag;
  ein negatives Ergebnis verbietet niemals eine freundliche Moat-Verbindung.
- `MovementPathPublication.cs` führt den nativen Builder einmal aus. Ein gültiger Vanilla-Pfad
  bleibt erhalten; notwendige Erweiterungen verwenden anschließend den vorhandenen Suchkern
  direkt. Der zusätzliche native Moat-Retry wurde entfernt. Beide Ausgaben `0xF4930` und
  `0xE32B0` verwenden denselben Vertrag für Start, Ziel, Unit-Puffer und Veröffentlichung.
  Mod-beeinflusste native Pfade sowie alle E32B0-Ausgaben gebundener Digger werden auditiert.
  Ablehnungen setzen Puffer, Länge und veränderte Steuerwerte zurück; auch ein nicht unterstützter
  Rekonstruktionsmodus darf keinen positiven Rückgabewert für einen verworfenen Pfad behalten.
- Der frische Pfadaudit prüft Global-ID, Spieler, exakten nativen Start, eigenen Puffer,
  Endpunkt, Höhen, sämtliche Kanten und feindliche Diagonalecken. Bei geänderter Kante oder
  Eigentümerbeziehung werden auch die Suchfelder vor einem Ersatz verworfen.
  Die einzige feindliche Arbeitsausnahme bleibt exakt ein gebundenes Fill-Arbeitsfeld als
  vorletzter Knoten. Der verwaltete Ersatz kann einen freundlichen Präfix mit genau diesen
  beiden letzten Kontaktkanten bilden; der vollständige Pfad wird anschließend erneut auditiert.
- `WeightedMoatPublication.cs` dekodiert den nativen Weg einmal. Boden-/Moatkanten liefern
  anschließend alle Profilkosten. Eine Suche mit allen Kostenbedingungen ersetzt die früheren
  wiederholten Profil-Suchen und -Audits. Veröffentlichung verlangt weiterhin mindestens
  40 Ticks Gewinn im tatsächlichen Profil und strikt positive Ersparnis in allen belegten
  plausiblen Profilen. Nicht kalibrierte Strukturwege werden nicht gewichtet ersetzt.

### Suchverfahren und gemeinsame Arbeit

`MoatSearchKernel.cs` enthält keine Unit-IDs, nativen Puffer oder Spiel-APIs. Der tatsächliche
Produktionskern wird auch in den Tests kompiliert. Skalare A* verwendet deterministische
Gleichstände mit größerem bereits zurückgelegtem Preis. Nur rechnerisch unmögliche
Verbesserungen werden ausgeschlossen; es gibt kein Zeit- oder Suchknotenbudget.

Verfehlt die skalare beste Route die Profil-, Moat- oder Längenbedingungen, erhält die
Verfeinerung alle nicht dominierten Boden-/Moatkanten-Kombinationen pro Feld und Moat-Zustand.
Eine fortsetzbare Rückwärtssuche testet bei gerichteten Kanten immer die ursprüngliche
Vorwärtskante. Gleiches Ziel nutzt den Baum direkt; unterschiedliche Formationsziele erhalten
zulässige Landmarkenschranken aus genau berechneten Entfernungen und der offenen Suchfront.
Ein Kostenstopp ist niemals ein Unerreichbarkeitsbeweis.

Terrain und Eigentümerklassifikation werden nur im exakten synchronen Suchkontext geteilt.
Gewichtete und ungewichtete Erreichbarkeit haben getrennte Suchfelder, damit die spätere
Abkürzungssuche die notwendige Qualifikation nicht verdrängt. Kostenäquivalente Anfragen nutzen
bestehende Felder. Heap, Labels und paginierte Distanz-/Vorgängerarrays werden wiederverwendet;
die Seiten werden erst bei Bedarf angelegt. Diese Array-Seiten ersetzen Dictionary-Zugriffe
im heißen Rückwärtssuchpfad. Die Projektkonfiguration optimiert auch den Debug-Build, dessen
Symbole für Diagnose weiterhin erhalten bleiben.

Ein fehlender Cacheeintrag bedeutet „unbekannt“. Exakte positive und negative Endpunktentscheidungen
werden wiederverwendet; pauschale negative PCL-Paar-Schlüssel sind entfernt. Topologische
Erreichbarkeit ist von der nativen Grenze von 2.000 Wegschritten getrennt: Eine positive
Erreichbarkeitsentscheidung bleibt auch dann positiv, wenn die Veröffentlichung später
`no-encodable-route` meldet. Unit-Pläne und kodierte Ausgaben werden nicht gruppenweit geteilt.

Dig-/Fill-Auswahl und Resolvervalidierung teilen die bestehende Vorwärts-Erreichbarkeitskarte
vom exakten Start. Auch Gebäude-/Angriffskandidaten verwenden ihren eigenen synchronen Scope
und setzen dieselbe Suche fort. Ein gefundener Bodenweg beantwortet „Moat notwendig?“ sofort
negativ; andernfalls wird die offene Suche soweit nötig fortgeführt. Negative Arbeitsendpunkte
werden mitgespeichert. Belegung und Arbeitsobjekt bleiben unmittelbare Prüfungen, native
Reihenfolge und Distanzvergleich unverändert. Der nächste Arbeitszyklus erhält frische Daten.
Feindliche Diagnosezustände werden ausschließlich bei ausdrücklicher Cursorunterscheidung
berechnet, nicht bei normalen Arbeitsanfragen.

Der Vergleich mit `ImprovedHunters` (`HunterPclReachability`/`HunterActiveTargetReachability`)
und `BugfixesAndQoL` bestätigt den Nutzen grober nativer PCL-Antworten, liefert aber keine
Aussage über die schnellste zusätzliche Moat-Route. Hunters dokumentiert in
`UpdateToNewDLL.md` die Deaktivierung seiner früheren unbeschränkten verwalteten A*-Suche
wegen möglicher Spielstillstände. Diese Suche wird nicht übernommen. Die nativen Floods
`0xDA590`/`0xDAFD0` verändern gemeinsame Pfaddaten; sie sind keine reine Regionsvorprüfung.
Die vorhandene öffentliche Fill-Bridge zu QoL, README und Modversion `1.0.0` bleiben unverändert.

### Hashgebundener nativer Vertrag des ergänzten Hooks

Die installierte kanonische Spiel-DLL und `CURRENT.json` stimmen mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` überein.
Die semantische Baseline und die direkt disassemblierten installierten Bytes belegen:
`0x11B520 -> 0x196280` mit Formationszielen, alternativ gemeinsame Ziele aus `0x118E00`.
`0x18E1E0` ist eine Pfadprobe. Auf `0x196280` bleibt ausschließlich der bestehende
Script-Extender-Hook; MoveMoatTest subscribiert `OnUnitMoveHere`.

`0x196280` benutzt den aktuellen Start nur bei `PathPlanStateBitFlags == 0 && MovingRelevant == 8`,
sonst den nächsten Schritt. Das Ziel wird vor dem Builder gegebenenfalls nativ ersetzt.
Der eigene Ausgabepuffer liegt bei UnitManager + `0xB4FE78 + unitId * 1000`.
Der alternative Zweig ruft `0xE32B0 -> 0xE1640` direkt auf und umgeht `0xF4930`.

Der neue Funktionsdetour hat ABI `int (IntPtr pathManager)`, Windows x64. Die komplette
56-Byte-Funktion `[0xE32B0,0xE32E8)` wird zur Laufzeit als eindeutiges Pattern und als exakte
Bytefolge validiert. `NativeContracts.py` prüft dieselben Bytes unabhängig an der installierten
DLL sowie Instruktionsgrenzen und den relativen Call nach `0xE1640`.

Register-/Stackvertrag: `push rbx` (2 Byte), `sub rsp,0x30` (4),
`mov r9d,[rcx+0x10]` (4), `xor eax,eax` (2), `mov r8d,[rcx+0x0C]` (4).
RCX bleibt für alle folgenden Adressierungen erhalten; erst danach wird es nach RBX gesichert,
EDX liest `[rcx+8]`. EAX nullt den sechsten Stackparameter und `[rcx+0x155F68]`, danach liest
EAX `[rcx+0x14]` für den fünften Parameter. Der Call bei `0xE32D7` darf flüchtige Register
verändern; die Rückgabe liest deshalb `[rbx+0x155F68]`. Epilog: `add rsp,0x30; pop rbx; ret`.
Es existiert kein eigener Scratchregister-Ersatzblock. MonoMod versetzt den vollständigen
instruktionsweisen Prolog ins Trampolin; ein 5-Byte-Sprung benötigt 6, ein 14-Byte-Sprung
16 Originalbytes. Diese Bereiche enthalten keine RIP-relativen Operanden. RBX, Stackausgleich,
Alignment und EAX-Rückgabe bleiben erhalten; Eingangsflags werden hier nicht benötigt.

Die optionale Regionsvorprüfung `0xE2610` verwendet die durch `0x196280` belegte Reihenfolge
Spieler, Quell-PCL, Ziel-PCL, Modus. Modus ist ein SHORT bei `GameUnit+0x35C`.
Die Veränderungen an Pfadkontext `+0xC0`, `+0xC4` und `+0x98` werden im finally zurückgesetzt.
Die unveränderten Extender-1.42.0-/QoL-Quellen enthalten keinen konkurrierenden E32B0-Hook.

### Reproduzierbare Prüfungen dieser Umsetzung

Aus dem Workspace-Root:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj --no-restore -- .
    & 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' _inspect/MoveMoatRegressionTests/NativeContracts.py

Die Suite prüft den gesamten Runtime-Quelltext semantisch gegen die lokalen 1.42.0-Referenzen.
Zusätzlich führt sie 72 tatsächliche Runtime-Member sowie den vollständigen neuen Suchkern in
simuliertem nativen Speicher aus. Die verbleibenden Spiel-APIs sind Fixtures; dies ist kein
Ersatz für einen Spieltest. Ergebnis: **1.554 Assertions** für Unit-Kontexte, beide Builder,
Regionen, Puffer und Arbeitsauswahl; **6.792 Assertions** gegen eine unabhängige Referenzsuche.
11 Runtime-Quelldateien sind syntaktisch und semantisch geprüft.

Abgedeckt sind tatsächliche Pre/Mode/Region/Builder/Post-Ketten, IDs und Formationsoffsets,
native Zwischenziele, gemeinsame Ziele, Änderungen der Pre-Argumente, Verschachtelung,
Skip/fehlendes Post, Rückgaben ohne Builder, Starts auf Moat und nächste Bewegungsschritte,
fremde Puffer/Handoffs, Global-ID-Wiederverwendung, Besitzerwechsel, Rollback, terminaler Fill,
verbündete/feindliche Felder und Diagonalecken. Arbeitsprüfungen wiederholen positive und
negative Endpunkte einschließlich neuer Auswahl nach Terrainänderung. Die unabhängige
Referenz erhält kleine gerichtete Karten, Profilkonflikte und Längenbeschränkungen;
Erreichbarkeit über 2.000 Schritte und die exakte Puffergrenze werden getrennt geprüft.

Modellkarte 220x150, frische Suchinstanzen je Gruppengröße, identische optimale Wegkosten:

| Units | verbesserte Einzelsuche: Knoten | gemeinsames Feld: Knoten | einzeln ms | gemeinsam ms | Allokation einzeln / gemeinsam |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 8.111 | 7.516 | 10,28 | 7,86 | 3.409.336 / 1.218.872 B |
| 5 | 40.242 | 38.374 | 77,83 | 35,75 | 3.412.088 / 4.630.232 B |
| 20 | 164.492 | 143.786 | 102,13 | 58,91 | 3.422.408 / 4.657.048 B |
| 27 | 224.280 | 196.442 | 67,59 | 65,25 | 3.427.224 / 4.661.864 B |
| 29 | 241.778 | 211.241 | 72,42 | 67,67 | 3.428.600 / 4.663.240 B |

Diese Einzelmessung enthält JIT-/Aufwärmeffekte; insbesondere kleine und große Gruppen sind
zeitlich nicht direkt vergleichbar. Die Knotenwerte sind deterministisch. Bei 29 Zielen spart
Sharing gegenüber der bereits verbesserten Einzelsuche 12,6 % Knoten. Gegenüber dem früheren
Modellstand mit 318.665 Knoten ergeben Gleichstandsregel und Sharing zusammen rund 34 %.
Die Array-Seiten senkten die gemeinsame Allokation des 29er-Falls von rund 6,37 auf 4,66 MB.
Das gemeinsame Feld braucht trotzdem mehr Speicher als die Einzelsuche; es wird innerhalb
der Runtime wiederverwendet. Keine dieser Zahlen ist eine gemessene Spielbeschleunigung.

Suchläufe/-knoten und gemeinsame Feldtreffer werden tatsächlich gezählt. Commanddiagnosen
trennen native Unit-Aufrufe, Rückgaben ohne Builder, bereits angekommene Units, Builder,
veröffentlichte Pfade und Ablehnungsgründe. Die zusätzlich als `Total` gekennzeichneten
Such-/Regionszähler sind kumulativ. Pro Gruppenbefehl entstehen höchstens drei ausführliche
gewichtete Veröffentlichungstexte; der Pfadaudit erzeugt keine Knoten-Dictionaries mehr.
Bestehende spätere Bewegungs- und Arbeitsmarker bleiben erhalten.

### Abschluss dieser Umsetzung

Am 5. September 2026 um 12:56:12 Uhr wurde nach den abschließenden Regressionen,
Quellkontrollen, der nativen Vertragsprüfung und CRLF-/Diff-Kontrolle **einmal** die vorhandene
`MoveMoatTest/build.bat /nopause` direkt erhöht ausgeführt. Ergebnis: Exitcode 0,
**0 Fehler, 0 Warnungen**, Build und Installation erfolgreich. Die Compilerzeile bestätigt
lokale Extender-1.42.0-Referenzen und `/optimize+` bei weiterhin vorhandenen Debugsymbolen.

Lokales Paket und installierte Ausgabe wurden anschließend verglichen: DLL, PDB und
`info.json` sind jeweils bytegleich. SHA-256 der neuen `MoveMoatTest.dll`:
`A3E46F3B85130F92DC7EF69DFB918040DBF698B1B578586BCBF9B7C473DB92E6`.
Version bleibt einheitlich `1.0.0`; README, Bridge und Extender-Fork wurden nicht geändert.
Alle betroffenen Textdateien haben CRLF, keine nackten LF oder versehentlich eingefügten
wörtlichen Backslash-r/Backslash-n-Zeichenfolgen; `git diff --check` ist sauber.
Die abschließenden Testausgaben liegen in
`_inspect/MoveMoatRegressionTests/latest-regression-results.txt` und
`_inspect/MoveMoatRegressionTests/latest-native-contract.txt`.

Noch offen ist die Spielabnahme mit 1, 5, 20, 27 und 29 Units, allen Formationstypen,
gemischten Gruppen, Shift-Queue, Patrol, KI, Folgebewegung nach Kämpfen, Sprite-/Gebäudeangriffen,
Treppen/Rampen/Wällen sowie wiederholten Dig-/Fill-Arbeitszyklen. Host und Client müssen mit
identischen Paketen geprüft werden. Erst tatsächliche spätere Bewegungs-/Arbeitsmarker nach
Startup-Cleanup bestätigen den Laufzeiterfolg. Die Runtime bleibt statisch im Plugin verwurzelt;
es wurde kein OnDestroy-Teardown oder MonoBehaviour-Update-Laufzeitpfad eingeführt.

Die folgenden Abschnitte dokumentieren ältere Messungen und Zwischenstände; deren Retry- und
Cachebeschreibungen werden durch die oben beschriebene Umsetzung ersetzt.

## Historischer Zwischenstand: Formationsziele und native Unit-Aufrufe

**Historischer Buildstopp, inzwischen aufgehoben.** Der Nutzer hatte den Build dieser Reparatur
ausdrücklich ausgesetzt, weil er vorübergehend die lokale Script-Extender-Version
ändert. Version bleibt `1.0.0`; README und Script Extender wurden nicht bearbeitet.
Der weiter unten dokumentierte Build um 01:32 Uhr gehört zum vorherigen Quellstand.

### Restfehler im Spieltest des vorherigen Builds

Der Logabschnitt vom 5. September 2026, 01:34:36–01:35:59 Uhr, zeigt weiterhin
abgewiesene Gruppenpfade, jetzt mit korrekten Unit-IDs. Beispiele aus
`BepInEx/LogOutput.log`:

| commandSeq | aktive Units | modeCalls | builderCalls | contractRejections |
| --- | ---: | ---: | ---: | ---: |
| 1 | 20 | 20 | 1 | 19 |
| 17 | 29 | 29 | 1 | 28 |
| 25 | 27 | 27 | 27 | 0 |

`builderCalls` zählt hier zugeordnete Mod-Builder, nicht sämtliche nativen Aufrufe.
Bei Command 1 lautet das Klickziel `(391,357)`, während die gewichtete Diagnose für
Folgeunits beispielsweise `(391,359)`, `(390,359)` und `(389,359)` erfasst.
Die verbliebenen Pending-Pläne benutzten das Klickziel. Der exakte Zielvergleich
lehnte deshalb richtige Unitpuffer mit richtigen Formationszielen ab.
Die unabhängige gewichtete Veröffentlichung half einigen Units trotzdem; das
erklärte die wechselnde Zahl tatsächlich bewegter Gruppenmitglieder.

Die Fill-Bündelung zeigt im selben Lauf eine deutliche Verbesserung: untersuchte
Arbeitsauswahlen verwenden `searchBuilds=1`, mit beispielsweise 3,710–7,839 ms
Suchzeit statt der zuvor beobachteten 285–317 ms. Das ist ein Logbefund dieses
Laufs, keine allgemeine Laufzeitgarantie. Diese Bündelung bleibt erhalten.

### Hashgebundene native Evidenz und Extender-Vertrag

Die installierte `CrusaderDE.dll`, `CURRENT.json` und die verwendete Baseline
stimmten bei der Prüfung mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
überein. Einstieg: `tools/semantic/query.ps1 function <RVA>` unter
`_inspect/CrusaderDE-Native-Baseline`; bei mehreren Ergebnissen ausschließlich
den Datensatz dieses Hashes verwenden. Die Funktionsnamen sind teilweise
automatisch zugeordnet; maßgeblich ist die nachfolgend beschriebene Aufruf-/Datenflussevidenz.

- `0x11B520` verteilt Formationsziele und ruft für einzelne Units direkt `0x196280`
  mit deren Zielpaar auf. Bereits passende native Zustände können einen Aufruf
  überspringen; ausgewählte Unitanzahl ist daher kein Sollwert für Builderaufrufe.
- `0x118E00` verwendet dagegen gemeinsame Zielkoordinaten. Das erklärt den
  erfolgreichen Zweig mit vielen zugeordneten Buildern im selben Spieltest.
- `0x18E1E0` ist eine Probe mit temporärem Pfadpuffer, kein universeller Einstieg
  in die tatsächliche Bewegung. Die bisherige Bezeichnung `CentralMovementPlan`
  und der Marker `plannerCalls` sind historisch; sie erfassen Gruppenaufrufe
  nicht vollständig. Die vorhandene Probe bleibt für ihre bisherigen Aufrufer erhalten.
- `0x196280` erhält fünf Argumente einschließlich Unit-ID, Unit-Ziel und
  Buildervariante. Es kann ein natives Zwischenziel wählen und bindet den
  Unitpuffer, bevor es `0xF4930` beziehungsweise den alternativen Builder aufruft.
- Der Script Extender besitzt bereits den Hook auf `0x196280`. Verwendet wird
  ausschließlich `UnitR3EventHooks.OnUnitMoveHere`, kein überlappender Detour.
  Der Vertrag wurde auch im dekompilierten, damals installierten `SHCDESE.dll`
  geprüft: Pre ist veränderbar; Post enthält die ursprünglichen Eingaben;
  `SkipOriginalFunction` erzeugt kein Post. Lokale Referenz und installierte DLL
  hatten dabei SHA-256 `6D4C919ECF6B4AE0EE329081CBBE66BD074202492D45B5AEE74B713B9AF1C57D`.
  Das ist eine Analyseprovenienz, keine Vorgabe für die vorübergehende Extender-Version.

### Reparatur im Quellcode

- Jeder tatsächliche Unit-Aufruf bekommt einen eigenen Eventkontext. Pre-Argumente
  werden erst bei der Modusprüfung als Unit-Ziel gebunden; Änderungen anderer
  Subscriber werden berücksichtigt. Klickziel, Unit-Ziel und Builder-Endpunkt
  bleiben getrennt. Nicht grabfähige Units benötigen keinen Suchplan.
- Ein nativer Zwischenendpunkt erhält einen lokalen Plan mit exakt geprüftem
  Start und Ziel sowie erneuter Erreichbarkeitsqualifikation. Er überschreibt
  weder den Unit-Auftrag noch den äußeren Arbeits-/Angriffskontext. Ein nicht
  qualifizierter Zwischenendpunkt benutzt beim nativen Builder dessen ursprünglichen
  Modus, auch ohne aktiven Gruppenbefehl.
- Puffereigentümer, tatsächlicher Unitpointer und nativer Startvertrag bleiben
  strikt. Die Ausgabelänge wird vor dem Retry geprüft, nicht vor der nativen
  Initialisierung des Builders. Abgewiesene Retries behalten den vollständigen Rollback.
- Verschachtelte Unit-Aufrufe besitzen getrennte Frames. Übersprungene Frames
  werden vor Verwendung entfernt; Post stellt den Elternframe wieder her.
  Die synchrone LIFO-Reihenfolge dient als Zuordnung, da die unveränderten
  Post-Argumente eines geänderten Kindaufrufs sogar dem Elternziel gleichen können.
  Fehlendes Post wird spätestens bei Befehlsende,
  Befehlswechsel, Tick- oder Kartenwechsel bereinigt. Neue Aufrufe erhalten eigene
  Pläne. Der bestehende Arbeits-Handoff wird nach seinem Builder weiterhin verbraucht.
- Der echte Owner-Audit setzt seine Kanten-/Besitzklassifikation je Pfad zurück.
  Ein neuer Test deckte auf, dass sonst frühere Such- oder Playerklassifikationen
  einen gültigen Weg als `enemy-moat-diagonal-corner` ablehnen konnten.
  Feindliche Durchquerungen bleiben verboten; die enge terminale Fill-Kontaktregel
  bleibt ausdrücklich getestet. Die gemeinsame Arbeits-Erreichbarkeitskarte wird
  durch diesen Klassifikationsreset nicht neu aufgebaut.

Neue aggregierte Move-Felder: `unitMoveCalls`, `unitMoveCompleted`, `unitMovePositive`,
`unitMoveWithoutF4930`, `unitMoveAbandoned`, `builderIntermediateTargets` und
`contractReasons`. Ohne beobachteten `F4930` kann ein nativer Frühabbruch, ein
bereits erreichtes Ziel oder ein anderer Builder vorliegen. Eine positive
Unit-Rückgabe ist noch kein Bewegungsnachweis. Ablehnungsdetails mit Klick-,
Unit- und Builderziel sind auf die ersten zwölf Fälle begrenzt; Zähler laufen weiter.

### Prüfstand und ausstehende Abnahme

Der eigenständige Runner unter `_inspect/MoveMoatRegressionTests` kompiliert die
ausgewählten tatsächlichen Runtime-Methoden gegen simulierte Spiel-APIs, einschließlich
Builder-Wrapper, Retry und echtem Owner-Audit. Er baut oder installiert keine Mod-DLL.
Der alte Test mit gleichen Gruppenzielen und manuell gesetztem `activePlan` hatte
den nativen Formationsablauf nicht abgedeckt; diese Testlücke ist jetzt geschlossen.

Abgedeckt sind gemeinsame und unterschiedliche Ziele für 1, 5, 20, 27 und 29 Units,
echte Pfadbytes, Zwischenziele, ungültige/fremde Puffer und Unitpointer, native
Next-Tile-Starts, Starts auf Moat, gemischte Fähigkeiten, wechselnde Spieler beim
Audit, veränderbare Pre-Argumente, verschachtelte gleiche/verschiedene Units,
Skip/fehlendes Post und Kontextwechsel. Arbeitsübergabe, terminaler Fill-Kontakt,
feindliche/unerreichbare Endpunkte, Terrainänderungen und Rollbacks werden ebenfalls geprüft.

Ergebnis dieser Reparaturrunde: **1.363 Assertions bestanden**, 60 tatsächlich
extrahierte Runtime-Member kompiliert und geprüft; Syntaxprüfung aller sechs
Runtime-Quelldateien erfolgreich. Aufruf:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj -- .

Die vier bearbeiteten Textdateien wurden abschließend auf CRLF und verbliebene
nackte LF geprüft; `git diff --check` ist Teil der statischen Abschlusskontrolle.

**Offen:** vollständiger Mod-Build gegen die später festgelegte Extender-Version
und Spieltest dieses neuen Quellstands. Erst danach Gruppenbewegung einschließlich
wiederholter Befehle, Shift-Queue, Patrol sowie Angriffe, Dig, Fill und Strukturwege
im Spiel bestätigen. Spätere Bewegungs-/Tickmarker und weitere Arbeitszyklen müssen
den Laufzeiterfolg belegen; Event-Rückgaben oder bestandene Fixturetests genügen nicht.

## Vorheriger Reparaturstand: Build vom 5. September, 01:32 Uhr

Die letzte Optimierung (`5f4e696b`, Vergleichsbasis `ce67cd30`) verursachte zwei
Regressionen. Der Lauf vom 5. September, 01:07–01:08 Uhr, belegt beide:

- Bei einem Move mit 20 grabfähigen Units wurden 20 Builder aufgerufen, aber nur ein
  Fallback ausgeführt. Nach `mode unit=2` wurde weiter `builder unit=1` protokolliert.
  Ein bereits qualifizierter `pendingPlan` der ersten Unit wurde für weitere Units
  wiederverwendet. Der neue Puffervergleich lehnte deren abweichenden Unitpuffer korrekt ab.
- Beim Fill wurden innerhalb einer Arbeitszielauswahl wiederholt zielgerichtete Suchen
  gestartet. Deren Cache existierte nur im Move-Command, nicht bei automatischen
  Arbeitsfolgezielen. Das Log zeigt ungefähr 480.000 besuchte Knoten und 285–317 ms
  Suchzeit je Unit-Auswahl. Die reine gewichtete Pfadoptimierung war dort deutlich kürzer.

Der reparierte Quellstand enthält folgende Änderungen:

- Mode-Freigaben gehören immer zur konkreten Unit. Ein passender aktiver Plan hat Vorrang;
  andernfalls wird der passende ausstehende Plan verwendet. Ein fremder äußerer Plan darf
  einen passenden Arbeitsplan nicht verdecken. Zentral übergebene Formationstargets bleiben
  erhalten, verschachtelte Planneraufrufe stellen ihren vorherigen Kontext wieder her.
- Der Builder wählt seinen Plan anhand des tatsächlichen Unitpuffers und Zielpaars.
  Vor dem Retry wird zusätzlich der native Startvertrag geprüft: aktuelles Tile bei
  `r_PathPlanStateBitFlags == 0 && r_MovingRelevant == 8`, sonst `r_NextTilePositionX2/Y2`.
  Ein Cachemiss im Modepfad wird berechnet; die reine Diagnose darf weiterhin nur nachsehen.
  Beide Zugriffe verwenden dieselbe Implementierung und denselben Cache-Schlüssel.
- Dig-/Fill-Auswahlen verwenden eine lazily aufgebaute Boden-/Friendly-Erreichbarkeitskarte
  für ihren exakten Spieler und Start. Positive und negative Endpunktentscheidungen werden
  innerhalb der Auswahl geteilt. Die unmittelbar zugehörige Resolver-/Builderübergabe
  erhält denselben Suchkontext. Neue Auswahlen bauen frisch auf, auch bei unverändertem
  Start. Tickwechsel, andere Units/Starts und ersetzte Suchkarten dürfen keine alten
  Endpunktentscheidungen einschleusen. Belegung und Arbeitsobjekt werden live nachgeprüft.
- Eine Suchkarte wird erst nach vollständig erfolgreicher Traversierung als Cache freigegeben.
  Ein abgebrochener Aufbau bleibt ungültig. Feindliche Wege werden nur bei ausdrücklich
  angeforderter konservativer Cursorunterscheidung zusätzlich berechnet.
- Negative oder fehlerhafte Retries stellen die 1000 Pufferbytes, den Ausgabepointer,
  die Länge, Route-Variante und den Moatmodus wieder her. Das gilt auch bei einer
  Audit-Exception. Positive Vanilla-Builderpfade werden weiterhin genau einmal ausgeführt.
  Owner-Audit und eng begrenzter terminaler Fill-Kontakt bleiben erhalten.
- Routine-Mode-/Pipeline-Details werden bei Gruppen- und Arbeitsauswahlen nicht mehr
  vorab formatiert. Aggregierte Such-/Pfadzähler und relevante Ergebnisse bleiben sichtbar.

Neue beziehungsweise ergänzte Logfelder:

- Move: `targetedSearches` zählt Qualifikationen, `targetedSearchPasses` die tatsächlichen
  Boden-/Friendly-Suchdurchläufe; außerdem `contractRejections` und `fallbackRollbacks`.
- Arbeitsauswahl: `searchBuilds`, `endpointQueries`, `endpointCacheHits`, `expanded`,
  `searchMs`, `elapsedMs`. Erwartet wird normalerweise ein Kartenaufbau je Auswahl,
  unabhängig von der Zahl ihrer Kandidaten. Eine verschachtelte fremde Suche kann einen
  erneuten Aufbau notwendig machen.
- Die ersten drei Puffer-/Kontextabweichungen erscheinen zusätzlich als
  `stage=fallback-contract-rejected`; weitere Fälle werden im Command gezählt.

Die erneute Codeprüfung und die automatisierten Regressionstests sind abgeschlossen:
**309 Assertions erfolgreich**, Syntaxprüfung aller sechs Runtime-Quelldateien.
Der eigenständige Runner unter `_inspect/MoveMoatRegressionTests` extrahiert mit Roslyn
48 tatsächliche Runtime-Member und kompiliert sie zusammen mit dem unveränderten
`WeightedMoatRoutePlanner` gegen simulierte native Grids und API-Adapter. Er prüft
27 Gruppenmitglieder, getrennte Puffer, Formationstargets, verschachtelte Arbeitskontexte,
positive/negative Caches, Belegungswechsel, Terrainänderungen, Tickablauf, feindliche Moats,
Start auf Moat, Audit-/Retry-Exceptions und Wiederanlauf nach einem Suchfehler.

Aufruf aus dem Workspace-Root:

    dotnet run --project _inspect/MoveMoatRegressionTests/MoveMoatRegressionTests.csproj -- .

Build und Installation wurden am 5. September 2026 um 01:32 Uhr einmal über
`MoveMoatTest/build.bat /nopause` abgeschlossen: **0 Warnungen, 0 Fehler**.
Die installierten Dateien `MoveMoatTest.dll`, `MoveMoatTest.pdb` und `info.json`
stimmen per SHA-256 mit dem lokalen Buildpaket überein. DLL-Hash dieses Reparaturbuilds:

`7B03FB1789C84BBCC43EDDAB8EB8ACF7ACD194CBA6A63E8280DE92A7F8607122`

Diese Tests führen das Spiel nicht aus und belegen keine Ingame-Latenz oder vollständige
native Hookintegration. Die Gruppen-/Fill-Wiederholung im Spiel und der Multiplayer-Test
stehen weiterhin aus. Die historischen erfolgreichen Spieltests weiter unten sind keine
Abnahme dieses neuen Reparaturstands. Modversion bleibt während der Testphase `1.0.0`.

## Ziel und aktueller Vertrag

`MoveMoatTest` erlaubt ausgewählten Bodeneinheiten, fertige eigene oder verbündete Burggräben
als reguläre Wegkante zu benutzen. Der Mod bleibt Vanilla-first: Positive Vanilla-Ergebnisse
werden übernommen; ein Fallback greift nur, wenn Vanilla an einer Burggraben-Grenze scheitert
oder ein nachweislich schnellerer freundlicher Burggrabenweg veröffentlicht werden kann.

Die Fähigkeit ist absichtlich auf dieselben Unittypen begrenzt, die Vanillas Command 6
(`DigMoatTileId`) pro Unit akzeptiert. Maßgeblich ist der Inline-Switch in `0x11E960`, bestätigt
durch den Auswahlhelper `0x191C00` und dessen Call bei `0x8D3CE`:

- Bogenschütze (`CHIMP_TYPE_ARCHER`)
- Speerträger (`CHIMP_TYPE_SPEARMAN`)
- Pikeniere (`CHIMP_TYPE_PIKEMAN`)
- Streitkolbenkämpfer (`CHIMP_TYPE_MACEMAN`)
- Ingenieure (`CHIMP_TYPE_ENGINEER`)
- arabische Sklaven (`CHIMP_TYPE_ARAB_SLAVE`)
- Eunuchen (`CHIMP_TYPE_BEDOUIN_EUNUCH`)
- Plänkler (`CHIMP_TYPE_BEDOUIN_SKIRMISHER`)
- Sappeure (`CHIMP_TYPE_BEDOUIN_SAPPER`)
- Demolierer (`CHIMP_TYPE_BEDOUIN_DEMOLISHER`)

Assassinen, Armbrustschützen, Schwertkämpfer, Ritter und Belagerungsgeräte erhalten keinen
Moat-Fallback. Das historische Feld `unit+0x170` ist keine bestätigte Capabilityquelle.

Eigene und verbündete fertige Moats sind begehbar. Feindliche fertige Moats dürfen Arbeitsziel
zum Zuschütten sein, aber niemals Traversierungskante. Wasser, massive Gebäude und ungültige
Mauer-/Strukturkanten bleiben Vanilla.

## Native Bindung

Alle RVAs in diesem Dokument gelten ausschließlich für:

`CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Die installierte DLL und `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` müssen vor einer
Wiederverwendung übereinstimmen. Bei einem Update sind Pattern, vollständige Entrybytes,
Callziele, ABI und interne Kontrollflüsse erneut zu prüfen; andernfalls bleibt der Mod
fail-closed.

Wichtige Stellen:

| RVA | bestätigte Bedeutung |
| --- | --- |
| `0x11E960` | Tribe-Command-Dispatcher; Command 6/7 und per-Unit-Grabfilter |
| `0x191C00` | Auswahl enthält mindestens eine grabfähige Unit |
| `0x196840` | Unit steht aktuell auf einem fertigen Moat |
| `0x196870` | Auswahlarten-/Cursor-Gate vor genaueren Tileprüfungen |
| `0xE2CA0` | Tilepaarprüfung; ruft bei Regionsgrenzen den nativen Suchhelper auf |
| `0xE2610` / `0xE7C40` | frühe Regions-/Gruppenprüfungen; keine universellen booleschen Gates |
| `0x11B520` | gemeinsamer MoveHere-Gruppenpfad |
| `0x117BC0` / `0x119F90` | Gruppen-Moatmodus und Gruppeniterator |
| `0x196280` | per-Unit-Pfadanforderung und Bindung des echten Unit-Pfadpuffers |
| `0xF4930` | zentraler finaler Unit-Builder |
| `0xDAFD0` | nativer Moat-/Gruppenbuilder mit achtgerichtetem Tilegraph |
| `0xE1640` / `0xE4E90` | Rekonstruktion und nibble-codierter Pfad, maximal 2000 Schritte |
| `0xDBC60` | Annäherungssuche für `AttackUnit` |
| `0xB70C0` | Cursor-Erreichbarkeit eines Gebäude-Footprints |
| `0xDA020` / `0x123090` | Gebäude-Annäherung und Kandidatenverbrauch |
| `0x69D60` / `0x6AF60` / `0x6C490` | Auswahl und Auflösung von Dig-/Fill-Arbeitszielen |
| `0x1853F0` / `0x1976C0` | Wiederaufnahme gespeicherter Bewegung nach einem Kampf |
| `0x19B260` | wirksame Unitgeschwindigkeit und Moat-Verlangsamungsphase |
| `0x18410C` | Dispatchanker zur dynamischen Ermittlung möglicher `r_SpeedBonus`-Werte |
| `0x107160` | native Sonderstrukturprüfung für Treppen-/Mauer-/Rampenkanten |

`MoveMoatTest` detourt weder `0xD9C40` noch `0xDA590`. Die gemeinsame Installation mit
`BugfixesAndQoL` verwendet dessen Reflection-Bridge für die Moat-Arbeitszielhooks; im bestätigten
Lauf meldete `BugfixesAndQoL 1.0.126` `hookOwner=MoveMoatTest`. Es darf nur einen Owner dieser
Hookgruppe geben.

## Warum mehrere dünne Adapter nötig sind

Vanilla besitzt keinen einzelnen globalen Schalter „diese Unit darf Moats überqueren“. Cursor,
Gruppenregionsprüfung, Entity-Annäherung, Command-Zuweisung, Arbeitszielauswahl und finaler Builder
können jeweils vorher abbrechen. Ein frühes positives Cursorergebnis erzeugt noch keinen Pfad.

Die Lösung besteht deshalb aus einer gemeinsamen Traversierungsregel und schmalen Vanilla-first-
Adaptern an den Stellen, die den zentralen Builder sonst nicht erreichen würden. Entity-, Hover-
und Zielkontext wird nur zur sicheren Bindung verwendet; Vanilla bestimmt weiterhin Zielentity,
Annäherungstiles, Arbeitsreihenfolge und Formation. Es werden keine Commands künstlich aufgeteilt
oder erneut ausgegeben.

## Gemeinsame Traversierungsregel

Die owner-sichere Probe verwendet denselben achtgerichteten Kantenvertrag wie der gewichtete
Planer und ist an `0xDAFD0`/`0xE1640` angeglichen. Sie prüft Richtungs- und Bewegungsmasken, Höhe,
Diagonalbedingungen, StructureGrid und die native Sonderstrukturprüfung. Path-Regionen werden
nur noch für native Rückgabewerte und Diagnose verwendet, nicht als Traversierungs-Whitelist.

Die frühe Befehlsqualifikation flutet nicht mehr für jede Unit die vollständige Karte. Sie sucht
zielgerichtet zuerst ohne Moat und nur nach einem Fehlschlag mit eigenen beziehungsweise
verbündeten Moats. Eine feindliche Route wird ausschließlich dort berechnet, wo ein negativer
Cursorbefund sie wirklich unterscheiden muss. Starts auf einem freundlichen fertigen Moat werden
direkt als moatgebundener Start behandelt.

Innerhalb eines synchronen Gruppenbefehls werden boolesche Entscheidungen für gewöhnliche Ziele
nach Spieler, Startregion und Zielregion geteilt. Region `0`, Moat-, Struktur-, reservierte und
Arbeitsendpunkte bleiben an das exakte Tile gebunden. Formationsoffsets derselben Region lösen
somit keine vollständige Suche je Unit aus. Rekonstruierte Pfade werden nie regionsweise geteilt;
sie bleiben stets an das konkrete Start-/Zielpaar gebunden. Alle Command-Caches enden mit dem
synchronen Command.

## Funktionierende Bereiche

Folgende Fälle wurden in Editor und teilweise in Skirmish praktisch bestätigt:

- wiederholter normaler Move durch einen eigenen fertigen Moat;
- notwendige und optionale Moat-Routen sowie mehrere Bodenregionen;
- direktes Ziel auf einem eigenen Moat und Start auf einem eigenen Moat;
- Shift-Move-Queues mit notwendigen Moat-Wegpunkten an mehreren Queuepositionen;
- Patrol über Moat;
- `AttackUnit` einschließlich wiederholter Befehle und Sprite-Hover;
- `AttackBuilding` hinter eigenem Moat einschließlich vollständigem Gebäudesprite;
- kürzestes gültiges Gebäude-Annäherungsfeld statt eines festen Hovertiles;
- begehbare, reservierte Gebäude-Endpunkte;
- Post-Combat-Fortsetzung eines gespeicherten Moat-Move-Ziels;
- gemischte Gruppen vor, auf und hinter einem Moat;
- Gruppen aus grabfähigen und ungeeigneten Units; nur grabfähige Units erhalten den Fallback;
- direkte sowie automatische Folgeziele beim Ausheben und Zuschütten;
- feindlicher Moat als Fill-Arbeitsobjekt, aber nicht als erlaubte Wegkante;
- Treppen, Rampen und begehbare Wall-Top-Ziele hinter einem eigenen Moat;
- normales Assassinen-Mauerklettern ohne Moat bleibt Vanilla;
- KI-gesteuerte grabfähige Units nutzen ihren eigenen Moat;
- mehrere Befehle und mehrere Units pro Spielstart ohne den früheren Einmal-Effekt.

Die Gebäudeoptimierung reduzierte einen früheren großen KI-Fall von bis zu 16 vollständigen
Reachability-Suchen pro Unit auf ungefähr eine Karte je Unit und Zielregion. Gemessene
Gebäudephasen lagen anschließend ungefähr bei 10 ms (`0xDA020`) und höchstens 2,5 ms
(`0x123090`) statt einer Pause von rund 2,2 Sekunden.

Ein späterer siebenminütiger KI-Lauf zeigte dennoch, dass die allgemeine Qualifikation noch zu
teuer war: 16.521 MoveMoat-Zeilen (rund 8,5 MB), 634 vollständige Kartenaufbauten, bis zu 66.265
geprüfte Strukturkanten je Aufbau und ungefähr 1,3 Sekunden synchrone Modarbeit bei zehn
grabfähigen Units. In den auswertbaren Move-Befehlen summierte sich die Modzeit auf rund 32
Sekunden. Logging verstärkte die Pausen, Hauptursache war aber die Vollkartensuche pro Unit.

Die vorangegangene Optimierung ersetzte diese Vollkartensuchen im normalen
Commandpfad durch die oben beschriebene zielgerichtete, regionsweise geteilte Qualifikation.
`0x196840` liefert nur Vanillas Aussage, ob die konkrete Unit gerade auf einem fertigen Moat
steht, und startet keine eigene Suche mehr. Normale AI-/Bodenbefehle, leere Queue-Snapshots,
unveränderte Flood-Aufrufe und wiederholte Tick-/Stallzustände werden nicht mehr einzeln geloggt.
Ein Performanceeintrag entsteht bei Moat-Eingriff oder einem messbar langsamen Command.
Die dabei eingeführten Gruppen-/Fill-Regressionen und ihre Reparatur stehen im neuen
Abschnitt am Anfang dieses Dokuments.

Der letzte Strukturtest endete für 65 beobachtete Pfade am Ziel; 35 Moat-Eintritte und 35
Moat-Austritte wurden protokolliert. Es gab keine MoveMoat-Exception.

## Gewichtete Wegwahl

Der zentrale Publisher läuft am echten Unit-Pfadpuffer im `0xF4930`-Detour und ist nicht von
Move-, Attack-, Combat- oder Arbeits-Commands abhängig. Er kann daher auch automatische Dig-/Fill-
Folgewege optimieren.

Das Kostenmodell liest die Runtimefelder jeder konkreten Unit; es enthält keine feste
Unit-Geschwindigkeitstabelle. Berücksichtigt werden `r_CurrentSpeed`, `r_CurrentSpeed2`,
`r_SpeedBonus`, zusätzliche Teilschritte/Verzögerung und die Moatphase aus `0x19B260`. Eine
Moatkante verwendet den bestätigten stabilen Delay-Aufschlag `+6`. Strukturpfade werden mangels
kalibrierter Strukturkosten nicht durch den gewichteten Publisher ersetzt.

Ein Kandidat wird nur veröffentlicht, wenn:

- er mindestens eine eigene oder verbündete Moatkante verwendet;
- jede Kante und der nibble-codierte Roundtrip gültig sind;
- er unter jedem aus dem nativen Handler dekodierten plausiblen `SpeedBonus` strikt schneller
  als Vanillas Pfad bleibt;
- er im beim Builder erfassten tatsächlichen Runtimeprofil mindestens 40 Ticks spart.

Die frühere Regel verlangte 40 Ticks Ersparnis unter jedem theoretischen Profil. Der Fill-Test
vom 4. September zeigte deshalb innerhalb derselben Gruppe unterschiedliche Wege: 21
Fill-Auswertungen blieben trotz schnellerem Shadow-Pfad unveröffentlicht; darunter wurden 11
ausdrücklich allein wegen der alten Profilregel zurückgewiesen. Weitere 11 Fill-Pfade wurden
veröffentlicht. Bei den betroffenen Macemen war der tatsächlich erfasste `SpeedBonus` 0. Die
Regel wurde deshalb präzisiert: Alternative Profile
dürfen den Kandidaten weiterhin niemals langsamer machen, die volle Sicherheitsmarge gilt aber
für den konkreten Runtime-Snapshot. Damit soll die beobachtete unnötige lange Wegwahl beseitigt
werden, ohne einen unter irgendeinem bekannten Profil schlechteren Pfad zu veröffentlichen.

## Owner-Sicherheit veröffentlichter Fallbackpfade

Positive Vanilla-Builderpfade bleiben unverändert. Nur ein durch den Mod nach einem echten
Vanilla-Nuller erzeugter nativer Retry wird anschließend am tatsächlichen nibble-codierten
Unit-Pfad vollständig auditiert. Eigene und verbündete Moat-Tiles sind zulässig; ein fremder
Moat, eine fremde reine Diagonalecke oder ein ungültiger Owner macht den Retry unsicher.

Beim Zuschütten darf ausschließlich das exakt gebundene feindliche Arbeits-Moat einmal als
terminaler Arbeitskontakt vorkommen: Es muss der vorletzte Pfadknoten direkt vor Vanillas
Annäherungstile sein und darf weder wiederholt noch als Durchgang verwendet werden. Findet der
Audit einen anderen fremden Moat, berechnet der Mod nur dann einen exakten owner-sicheren
Ersatzpfad zum unveränderten Vanilla-Ziel. Der Ersatz wird über denselben 1000-Byte-Unitpuffer,
Längenvertrag und Decode-Roundtrip veröffentlicht. Scheitert irgendeine Prüfung, werden Puffer,
Länge und Builderzustand auf den Stand vor dem Retry zurückgesetzt.

`ownerSafetyViolation` bezeichnet damit nur noch einen vom Mod veröffentlichten Pfad mit fremdem
Nicht-Ziel-Moat oder ungültigem Owner. Ein unveränderter positiver Vanilla-Pfad wird nicht mehr
als Modverletzung klassifiziert.

## Verworfen oder ersetzt

- Globale Bytepatches im Cursordispatcher verursachten falsche Mauer-/Klettercursor und wurden
  vollständig entfernt.
- Der Assassin-Sonderpfad über `pathManager+0x88` und eine Reflection-Routenbrücke erzeugte zwar
  kombinierte Probewege, Vanilla konsumierte sie aber nicht zuverlässig. Assassinen sind nach
  dem finalen Capabilityvertrag ohnehin ausgeschlossen; dieser Sondercode wurde entfernt.
- Eine auf Start-/Zielregion beschränkte Managed-BFS schnitt gültige Zwischenregionen ab und
  wurde durch die regionsunabhängige Tilegraph-Probe ersetzt.
- Das nachträgliche Erfinden fehlender Gebäude-Kontexttiles (`candidate+4`) war falsch. Der
  Fallback greift jetzt früher in `0xDA020` ein, sodass Vanilla vollständige Kandidatenpaare
  erzeugt.
- Gebäude-Hover darf nicht von gelegentlich auf `(0,0)` springenden Cursor-X/Y-Globals abhängen.
  Maßgeblich sind Vanillas Hover-Building-ID, ein gültiges Mouse-Tile und das nächstgelegene echte
  StructureGrid-Tile desselben Gebäudes.
- Hochfrequente Shadow-, Per-Tick-, Stall-, leere Queue- und gewöhnliche Bodenwegdiagnosen wurden
  entfernt. Beibehalten sind verwendete Fallbacks/gewichtete Veröffentlichungen, Rollbacks,
  Exceptions, Ownerverletzungen und aggregierte langsame Commands.

## Noch offen beziehungsweise erneut zu bestätigen

- Der reparierte Unit-Kontext, der zielgerichtete Command-Cache, die gebündelte Arbeitszielsuche
  und der abschließende Retry-Pfadaudit benötigen einen
  gezielten Performance- und Fill-Wiederholungstest. Erwartet wird höchstens eine Qualifikation je
  Start-/Zielregion statt einer Suche je Formationsoffset oder Unit.
- Der früher als `ownerSafetyViolation=True` gemeldete Fill-Fall ist mit dem alten Log allein nicht
  eindeutig: Vanilla darf beim Zuschütten das feindliche Arbeitsobjekt berühren. Der neue Audit
  unterscheidet genau diesen einmaligen terminalen Kontakt von echter Durchquerung und rollt nur
  letztere zurück beziehungsweise ersetzt sie owner-sicher.
- Verbündete Moats verwenden denselben Allianzfilter wie eigene Moats, wurden aber nicht in allen
  Befehls- und Gruppenvarianten praktisch wiederholt.
- Multiplayer wurde architektonisch vorbereitet (`NetworkMode: 1`, deterministische Daten,
  kein eigenes Netzwerkprotokoll), aber ein Host-/Client-Smoke-Test mit identischen Modversionen
  und Desyncbeobachtung steht noch aus.
- `ForceAttackBuilding` konnte nicht in jedem Aufbau gezielt ausgelöst werden; der gemeinsame
  Gebäudeweg unterstützt den Commandwert, die vollständige praktische Abnahme ist offen.

## Empfohlener kurzer Abschlusstest

1. Gruppen mit 1, 5, 10 und 27 grabfähigen Units zunächst über normalen Boden und danach über
   einen zwingenden freundlichen Moat schicken. Normale Befehle dürfen keinen Fallback auslösen;
   gleiche Startregionen sollen Cachetreffer statt proportionaler Suchen zeigen.
2. Mehrere grabfähige Units gleichzeitig feindliche Moats zuschütten lassen. Ein terminaler
   Zielkontakt muss als zulässig erscheinen; jeder fremde Durchgang muss ersetzt oder vollständig
   zurückgerollt werden.
3. Optionalen kurzen Moat, gemischte Gruppen, Shift-Queue, Patrol, `AttackUnit`,
   `AttackBuilding`, Dig und Treppe regressionsprüfen.
4. KI-Skirmish mindestens zehn Minuten schnell vorlaufen lassen. Ziel sind typische Commands
   deutlich unter 50 ms, 27 gleiche Startregionen möglichst unter 100 ms, keine harten Pausen,
   keine Exceptions und keine tausenden Diagnosezeilen.
5. Danach Host und Client mit identischen Paketen starten, eigenen sowie feindlichen Moat testen
   und auf Desync/Exceptions achten.

## Update-Reihenfolge

Bei einer neuen Spiel- oder Extender-DLL:

1. installierten DLL-Hash gegen `CURRENT.json` prüfen;
2. zuerst `0x196280 -> 0xF4930`, echten Unit-Pfadpuffer und `0xDAFD0 -> 0xE1640` wiederfinden;
3. Command-6-Switch `0x11E960` und Auswahlhelper `0x191C00` erneut abgleichen;
4. Cursor-/Entitypfade `0x196870`, `0xE2CA0`, `0xB70C0`, `0xDBC60`, `0xDA020`, `0x123090`
   validieren;
5. Arbeitszielkette `0x69D60`, `0x6AF60`, `0x6C490` und Bridge-Ownership prüfen;
6. Geschwindigkeitsvertrag `0x19B260` und Unit-Handlerdispatch erneut dekodieren;
7. erst danach Hooks installieren; jede nicht vollständig validierte Gruppe bleibt Vanilla.

README und Modversion wurden während dieser Testphase bewusst nicht geändert.
