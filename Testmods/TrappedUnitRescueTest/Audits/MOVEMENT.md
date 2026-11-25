# Offline-Audit: Bewegung, Belegung, Gruppenzustand und Rettung

Stand: 25.11.2026. Untersucht wurden der vorhandene Rettungsprototyp, die aktuellen Pathfinding-Besitzer, die relevanten Extender-Wrapper und die native Erzeuger-/Verbraucherkette. Keine Installation, kein Spielstart, keine Ausführung nativen Spielcodes und keine Runtimeänderung. Die Aussagen beschreiben statische Verträge; sie geben noch nicht implementierte Bewegungsfamilien nicht für den Spielbetrieb frei.

## 1. Identität und Beweisgrenzen

Die installierte kanonische `CrusaderDE.dll` wurde erneut gehasht: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Der Hash entspricht `CURRENT.json` und `DATABASE_INFO.json`. Die Abfragen wurden ausdrücklich auf diesen `binary_hash` gefiltert; ein RVA kann zusätzlich einen historischen Datensatz besitzen.

[MOVEMENT-NATIVE.json](MOVEMENT-NATIVE.json) enthält 63 Funktionsdatensätze mit Caller-/Callee-Grenzen, Baseline-Instruktionshash, erneuter Prüfung der tatsächlichen installierten Bytes und unabhängig mit Capstone dekodierten Schlüsselstellen. Alle 63 Hashvergleiche bestehen. Zusätzlich wurde der im Funktionsindex fehlende Portalhandler `D7D50` unmittelbar aus der kanonischen PE dekodiert und sein tatsächlicher Tabellenzeiger geprüft. `rawHash` im Ghidra-Export ist der Hash der Instruktionsbytes: Bei `E1640` sind das zwei Bereiche `[E1640,E17B9)` und `[E17C0,E1C2F)`, nicht einfach 1.512 zusammenhängende Bytes. Diese Unterscheidung wurde vor Bewertung des Vergleichs geklärt.

Generische Funktionsnamen behalten im Dataset Vertrauensstufe `candidate`; das stuft nicht automatisch jede konkrete Instruktions-/Feldbeobachtung ab. Im Folgenden bedeutet **bestätigt statisch**, dass vollständige relevante Körper und konkrete Felder/Branches abgeglichen sind. Laufhäufigkeiten, tatsächlich auftretende Fremdmodzustände und Spielergebnisse bleiben eigene Nachweise.

Die Baseline-Interop-Provenienz liegt hinter dem aktuellen Extender-Checkout. Der koordinierte Gesamtaudit behandelt dieses Delta. Die hier benutzten Unit-, Tile- und BulkUnit-/Pathing-Verträge wurden gegen die lokalen Quellen geprüft; keine daraus abgeleitete neue Laufzeitsignatur wurde veröffentlicht.

## 2. Einheiten-, Karten- und Koordinatendomänen

| Wert | Tatsächlicher Vertrag | Konsequenz |
|---|---|---|
| Native Unit-ID | Live-IDs `1..9999`, Slot 0 reserviert; 10.000 native Slots | „10.000 Einheiten“ ist die Kapazitätsklasse, keine Freigabe für Unit-ID 10000. `182B00` iteriert ab 1 mit `<10000`; `GameUnitManager` führt dieselben Grenzen explizit. |
| Unit-Adressierung | Manager `0x67E8400`, Slotstride `0x490`; typisierte Sicht beginnt bei Manager+ID×Stride+`0x65C` | Typisierte Offsets nie direkt zu Manager+ID×Stride addieren. Öffentliche Spanindizes sind separat 0-basiert. |
| Lebensidentität | Unit-ID plus Global-ID (`native+6F0`, typisiert+94) und Sitzung | Ein später lebender gleicher Slot ist kein Nachweis derselben Einheit. |
| Kartenkoordinaten | X/Y jeweils innerhalb der 800×800-Koordinatenhülle; tatsächliche Karte zusätzlich über aktive Maske/Geometrie | Nicht alle 640.000 Koordinatenpaare sind gültige gepackte Tiles. |
| Gepackte Tile-ID | X + native Zeilen-LUT `[3×Y]`; Kapazität `0x4E520` = 320.800 | Kein `Y×800+X` als nativer Tileindex. Rechteckige Arbeitsarrays von Pathfinding und gepackte native Felder sind verschiedene Domänen. |
| Position | Tile-Koordinaten WORD, Feinposition Tile×8+4, getrennte aktuelle und nächste Tile-ID | Feinposition oder Next allein belegen weder Current-Übernahme noch Belegung. |
| Höhe | `19BA40` erhält die 16-Bit-Basishöhe; typisierte/nativ gespeicherte Höhe, Gebäudehöhe und Renderoffset sind verschieden | Einen aus der Ansicht abgeleiteten Dachwert nicht als Bodenhöhe einsetzen. |

Quellen: `shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnitManager.cs:10`, `API/GameTileManagerAPI.cs:507` und `:911`; native Körper `182B00`, `1802A0`, `186AF0`, `196280`, `19BA40`. Der Kommentar-Cheat-Sheet am Anfang der Tile-API ist für diese Rechnung nicht maßgeblich; der konkrete Getter und die nativen LUT-Zugriffe sind es.

## 3. Geschlossene gewöhnliche Bewegungsfolge

Die vollständige relevante Kette ist `182B00 → Typzustand/Bewegungstakt → 1855A0 → DCE60/DCD60 → Feinpositionsänderung → 186AD0/180230/1802A0 → Höhen-/Darstellungsfortschreibung`. Abgelehnte neue Schritte führen gegebenenfalls über `196280` zur erneuten Planung; es gibt außerdem Portal-, Warte-, Kill-/Typ- und Zustandszweige.

### 3.1 Neue Route und Auswahl des Startpunkts

`196280(manager, unitId, uint targetX, uint targetY, selector)`:

1. Prüft Zielkoordinaten gegen die native gültige Karte. Ungültige Ziele liefern 0, ohne eine Rettungsberechtigung zu begründen.
2. Ruft `19B1B0`: Current X/Y werden aus der gepackten Current-ID normalisiert. Bei Inkonsistenz schreibt der Normalizer Current, Next und Feinposition neu. Eine reine Vorabprüfung darf diesen Normalizer nicht als nebenwirkungsfreies Probeverfahren aufrufen.
3. Wählt Current nur bei `PathFlags==0 && Substep==8`, sonst Next. **Substep 8 allein bedeutet nicht, dass die Planung bei Current beginnt.**
4. Schreibt angefragtes Ziel `744/746`, löscht Portal-ID `8EC` und Retry-Zustand `A4C`. Bei Start=Ziel beendet es den Weg ohne Builder mit Erfolg 1.
5. Prüft Zieloberfläche sowie Regionen. Gleiche Region führt unmittelbar zur Suche; andere Regionen benötigen Modus-/Portalentscheidung (`E2610`, `DF720`, `F4630`) oder die zulässigen Alternativzweige. Eine Regionsablehnung kann bereits vor dem Builder zurückkehren.
6. Vor dem Builder kopiert es den Unitrecord in den Arbeitsrecord, **löscht den unit-eigenen 1.000-Byte-Pfadpuffer** und setzt dessen Ausgabepointer. Selector 0 verwendet `F4930`, Selector ungleich 0 `E32B0`.
7. Bei positiver Länge veröffentlicht es Cursor 0, Länge, Flags 2, tatsächliches Routenziel, Start und gespeicherten Moat-Modus `9C8`; danach löscht es temporäre Managerzustände.
8. Bei Fehler werden vorherige Pfadbytes nicht wiederhergestellt. Alte Flags/Länge/Zielwerte können teilweise stehen bleiben. `199CD0` kann unter seinen Selektions-/Typ-/Mauer-/Strukturausgangsbedingungen außerdem AI110 eintragen. Rückgabewert 0 bedeutet weder unveränderten Unitrecord noch „sicher stillstehend“.

`E32B0` ist kein zweiter unabhängiger Suchversuch: Es rekonstruiert vorhandenen Suchzustand mit `E1640(mode0)` und liefert den gespeicherten Ausgabelängenwert. `F4930` hat neben `196280` einen Probecaller `18E1E0` mit fremdem/Stack-Ausgabepuffer. Ein Builderfehler ist daher ohne exakte Bindung an einen unit-eigenen Ausgabeslot kein identifizierter Einheitenfehler.

### 3.2 Physischer Schritt, Mittelpunkt und Belegung

`1855A0` prüft vor gewöhnlicher Fortbewegung unter anderem Kill-/Typ-/AI-Zustände und Flags. Flags 5 sowie keine aktive gewöhnliche Route können regulär 0 liefern. **Der Funktionsrückgabewert 0 ist kein allgemeines Feststeckersignal.**

Die physische gewöhnliche Route liest bei einem neuen Schritt die nächste gepackte Richtung (Low-Nibble zuerst, danach High-Nibble) und erhöht den Cursor. Für die gewöhnliche Klasse wird nur an dieser neuen Schrittgrenze `DCE60` und danach `DCD60` geprüft. Bei Annahme setzt sie Next auf Current plus eines der acht Nachbardeltas; Next-Tile-ID und alte Tile-ID werden separat gespeichert.

Bereits laufende Substeps erhöhen Feinpositionen und Substep, **ohne DCE60/DCD60 für diesen schon angenommenen Schritt erneut aufzurufen**. Die Richtungstabelle `335C98` legt fest, ab welcher Substep die temporäre DWORD-Markierung `740` gesetzt wird. Vor jeder echten Substep wird die alte Current-Kette entfernt. `180230` sieht `740`: bei ungleich 0 übernimmt es Next X/Y/Tile-ID in Current, dann fügt `1802A0` genau diese Current-Belegung ein. Anschließend wird `740` zurück auf 0 gesetzt. Das ist kein Beleg für zwei dauerhafte Occupancy-Ketten.

Auswirkung für den ursprünglichen Fehler: Ein Bauvorgang kann eine bereits akzeptierte Next-Kachel unpassierbar machen. Der laufende Schritt kann sie trotzdem erreichen. Eine ausschließlich nach neuer Richtungsablehnung suchende Erkennung kann zu spät sein; eine Erkennung ausschließlich über Belegung der Baukacheln verfehlt den Akteur vor dessen Mittelpunktübernahme.

### 3.3 Zusätzlicher spekulativer Aufruf

`182B00` ruft nach dem echten Fortschritt `1855A0` zusätzlich mit viertem Argument Byte 1 auf. Es kopiert vorher den Unitrecord, berechnet Darstellungs-/Vorausschaudaten und stellt danach den Record wieder her. Die echten Occupancy-Aufrufe in `1855A0` sind dabei durch `param4==0` gesperrt. Ein neuer Beobachter darf spekulative Aufrufe weder als echten Ortswechsel zählen noch daraus eine Rettung ausführen. Die bestehenden Gate-Diagnosen kennen diese Trennung bereits (`NativeAssassinMovementDiagnostics.DevDiag.cs:17`, `:75`). Ihr Diagnoseereignis ist keine funktionale allgemeine Rettungsschnittstelle.

## 4. Was einen legalen Ausgang beweist

`DCE60` ist kein reiner Boolean-Lookup. Es kann unter anderem Warte- und Assassin-Kletterfelder ändern. Sein normales class0-Kriterium akzeptiert **Source-Vorwärtsverbindung ODER Destination-Rückwärtsverbindung**. Eine leere Source-Verbindungsmaske beweist daher keinen Einschluss. In einem Fire-/Spezialgebäudezweig kann ein vorhandener Übergang einmaliges Warten setzen; das ist keine Topologie ohne Ausgang.

Ohne gewöhnliche Verbindung bestehen weitere Zweige:

- Assassin: Typ 73 und passende Mauer-/Ziel-/Baum-/Spezialstrukturbedingungen können Aufstieg/Abstieg aktivieren und Kletterhöhe/AI126 beziehungsweise AI128 schreiben. Elevated allein ist weder eine Mauer noch ein Kletterbeweis.
- Gespeicherter Moat-Modus `9C8 != 0`: Die Prüfung kann Eintritt in einen fertigen Graben beziehungsweise geeigneten Ausstieg ohne gewöhnliche Richtungskante erlauben. Der aktuelle Moat-Tileflag allein ersetzt diesen Modus nicht.
- Bewegungsklasse 1 beziehungsweise andere Klassen: eigener Übergangsvertrag. Die gewöhnliche Ausführung lässt class1 am normalen DCE60-/DCD60-Paar vorbei; daraus keine class0-Regel ableiten.
- Portal-/Transportzustände: `1988F0` kontrolliert Record-Identität, Richtung und Verbindungsklasse, kann Next auf Portalendpunkte setzen, `187110`/`196280` aufrufen oder in einen Transportzustand übergehen. Ein entfernter Next-Punkt ist deshalb nicht pauschal Beschädigung.

`DCD60` prüft nur den **betretenen Ziel-Tile**, nicht „ist der aktuelle Tile legal?“. Die vollständige restriktive Typmenge und der Whitelist-Vertrag stehen in `MOVEMENT_SURFACE_PROFILES.md`. Die Prüfung unterscheidet Wand-/Elevated-Maske `0x10000100`, Goodsyard ohne Building bei `(flags&0x102)==0x102` und Buildingtype-Whitelist `2E68D0`. Die Large-Suchregel ist breiter als diese physische Regel. Ein ungeeigneter Start kann trotzdem legalen Boden verlassen.

Der Extender besitzt den DCD60-Funktionseinstieg und berücksichtigt seinen typweiten Bypass (`BulkPathingDetours.cs:46`, `:138`). Pathfinding liest Profile und Bypass live (`MovementSurfacePolicy.cs:83`) und begrenzt veröffentlichte Pfade nach der effektiven Oberfläche. Eine neue Rettung darf diese prozessweiten Einstellungen nicht vorübergehend umschalten oder denselben Einstieg zusätzlich detouren.

**Statisch geschlossene Folgerung:** Weder Tileflag „unpassierbar“, PCL 0, acht fehlende Sourcebits, Suchfehler noch langes Stillstehen sind allein ein allgemeiner Rettungsnachweis. Der vorhandene Prototyp vermeidet falsche Positive durch konservative Ablehnung nicht qualifizierter Klassen/Moat-/Portal-/Kletter-/Raised-Fälle. Ein vollständig allgemeiner Ausgangsprädikat für alle legalen Sonderzustände ist eine eigene Implementierung; es darf nicht durch tatsächliche native Aufrufe mit ihren Seiteneffekten als Lesetest ersetzt werden.

## 5. Kandidaten ohne permanenten Einheitenscan

### 5.1 Current und Next-only aus begrenzter Umgebung

Die geprüfte gewöhnliche class0-Folge schreibt beim neuen Schritt Next ausschließlich als Current plus ein Nachbardelta. Vor dem Mittelpunkt liegt die belegte Current-Kachel höchstens Chebyshev-Distanz 1 von Next entfernt; nach der Übernahme ist Current=Next. Daraus folgt für **bekannte gewöhnliche Schritte ohne Portal/Transport/Klettern** eine konkrete Discovery-Grenze: Current-Occupancy der tatsächlich geänderten Footprint-Tiles **plus ein Tile Halo** lesen; anschließend Kandidaten exakt nach `Current in Delta || Next in Delta` und ihrer nachgewiesenen Klasse/Phase filtern. Ein Pro-Tick-Vollscan oder eine pro Einheit ständig gepflegte Next-Liste ist dafür nicht nötig.

Der Halo ist kein pauschaler Beweis für alle Next-Writer. Portalendpunkte, spezielle Transporte, künstliche oder fremd geschriebene Positionen können weiter auseinander liegen. Solche Fälle benötigen einen eigenen vorhandenen Erzeuger-/Zustandsvertrag und bleiben bis dahin nicht unterstützt. Die Auswahl muss zusätzlich die wirklich geänderten Zellen enthalten: bloßes Ankerrechteck oder Preview-Footprint kann mehr oder weniger als das Delta erfassen. Das behandelt der Bauaudit.

An Kartenrändern muss jede XY-Koordinate vor der gepackten LUT-Abfrage gegen gültige Karte und Kapazität geprüft werden. Ein teilweise außerhalb liegender Halo ist nicht automatisch ein Fehler der tatsächlichen Platzierung; außerhalb liegende Halofelder sind keine lesbaren Occupancy-Zellen. Die derzeitige `TryCaptureCurrentOccupants`-Methode verwirft ein komplett angefordertes ungültiges Rechteck. Sie implementiert noch keinen geclippten Halo und keine Next-only-Admisssion.

### 5.2 Ketten, Budgets und Überlast

`186AF0` erwartet eine kohärente Occupancy-Kette und besitzt im inneren Linksuchen keine allgemeine Zyklusgrenze. `1802A0` benutzt einen globalen Scratchpuffer und baut nach Einfügen eine nach Unit-ID geordnete Kette samt Rang-/Wartefeldern wieder auf. Es überspringt gemountete/verdeckte Einheiten (`9B0 != 0`). Raw-Writes an Current oder Occupancy sind daher kein zulässiger Ersatz.

Der Prototyp begrenzt Current-Capture auf 1.024 Kachelzellen, 4.096 Linkbesuche und 512 eindeutige IDs, erkennt Zyklen/ungültige IDs und validiert alle Ketten vor teurer Vorher-Reichweite (`RescueRuntime.cs:83`). Deduplizierung endet in sortierter Unit-ID-Reihenfolge. Die separate Fixture-`MutationScope` scannt dagegen alle Slots einmal pro ausdrücklich angefordertem Test; sie ist kein automatischer Laufzeitbeobachter und darf nicht zur generischen Eventimplementierung kopiert werden.

Ein enger Footprint kann im Extremfall mehr Einheiten als das Capturebudget enthalten. Dann bedeutet die sichere Ablehnung **unvollständige Erkennung/keine Rettungsfreigabe**, nicht „es gab keine Betroffenen“. Nachträglich alte Erreichbarkeit aus neuer Topologie zu berechnen wäre falsch. Für eine zukünftige Queue muss daher ein eindeutiger Overload-Ausgang vorgesehen sein; eine garantierte Rettung aller extremen Stapel ist aus dem bisherigen Budget nicht ableitbar. Ebenso dürfen verloren gegangene Vorherdaten, ID-Wiederverwendung oder neue Aufträge nicht durch einen Default-Plan ersetzt werden.

Bei unveränderter Welt und leerer Kandidatenliste benötigt diese ereignisgebundene Discovery keine zusätzlichen Einheiten-/Kachelprüfungen. Welche Zusatzkosten sie unter Massenbau tatsächlich verursacht, ist eine spätere Messung; aus O(Footprint + Links + Kandidatenarbeit) folgt keine behauptete Millisekunden- oder FPS-Garantie.

## 6. Sichere Versetzung und terminale Folgen

`19BA40(manager, oneBasedUnitId, uint x, uint y, ushort baseHeight)` prüft die native Karte, setzt Flags `74E=0`, `740=0` und DWORD `750=8` (Substep 8 **und Cursor 0**), entfernt die alte Current-Belegung, setzt Current/Next/Feinposition/Höhe/gepackte Current-ID, registriert wieder und aktualisiert Viewoffsets.

Die geschlossene Helferkette `186AD0→186AF0`, `180230→1802A0`, `18BE50` allokiert keine neue Einheit und schreibt weder Global-ID, Typ, Besitzer, Gesundheit, Tribe noch Arbeitsplatz/Ladung/Besatzungsreferenzen um. Sie normalisiert jedoch **nicht** alle Portal-/AI-/Job-/Ziel-/Pfadlängenfelder. Sie hat keine Erfolgsrückgabe. Gültiger Zielort, kohärente Quellkette und Nachkontrolle sind Callerpflichten; der Prototyp prüft diese ausdrücklich (`NativeRelocation.cs:123`, `RescueRuntime.cs:337`).

`187110` ist ein anderer Helfer: Er bereinigt zusätzliche Verbindungs-/Aux-Zustände, versucht ein zusätzliches Unlink über `6B8`, übernimmt das bereits vorbereitete Next nach Current und registriert neu. Das entspricht dem Fixes-Reiterpfad. Seine Existenz legitimiert weder frei gesetzte Aux-Felder noch einen Austausch beider Helfer ohne Zustandsvertrag. Der gewöhnliche Bewegungsweg beweist keine natürliche zusätzliche `6B8`-Belegung; nicht beobachtete Aux-Abdeckung bleibt offen.

`197950` löscht Cursor/Länge, `92A` und Portal-ID; **es löscht nicht die Pfadflags**. `188340` meldet Abschluss bei Flags 0 und Portal 0, ohne das ursprüngliche Arbeitsziel räumlich zu vergleichen. Deshalb kann ein auf Flags 0 gesetzter Arbeiter weiterarbeiten, obwohl sein physischer Zielort nie erreicht wurde. Das ist durch den Apple-Handler konkret belegt; ein allgemeines „erst stoppen, später neu planen“ ist für Arbeiter nicht freigegeben.

Die vorhandene Rettung wählt aus einer vor dem Bau ermittelten, lokalen gewöhnlichen Reichweite (Radius 8, feste Acht-Richtungs-BFS-Reihenfolge) einen danach freien gewöhnlichen Kandidaten. Damit wird nicht beliebig auf die andere Seite eines neuen Hindernisses geraten. Diese Liste ist kein allgemeiner Gebäude-/Dach-/Portal-Rettungsraum; fehlt sie, lautet das Ergebnis `NoCandidate`/`Unsupported`.

Nach Versetzung müssen mindestens Originalidentität, Gesundheit, Besitzer/Tribe, Quellbelegung 0 und Zielbelegung genau 1 geprüft werden. Eine fehlgeschlagene Wiederveröffentlichung kann bereits nach erfolgter Versetzung eintreten; der vorhandene Status `ResumeFailed` verspricht ausdrücklich kein Rollback. Physische spätere Bewegung, Arbeitsfortsetzung und ausbleibende zweite Versetzung sind zusätzliche Spielassertionen und hier nicht neu ausgeführt.

## 7. Gruppen, KI und Befehlsfortsetzung

Die physische Versetzung lässt die Tribezuordnung bestehen; das löst nicht automatisch jede Gruppenentscheidung. Native Gruppenbewegung hat zwei wichtige unabhängige Hindernisse:

1. `11B520` verwendet zunächst den echten 1-basierten Leader und dessen Region. In der gewöhnlichen Cross-Region-Kette ermittelt `117C70` den Gruppenmodus, dann `E2610(player, sourceRegion, targetRegion, signedMode)`. Erst bei Scheitern folgen `E9D90/E9FF0`. Der Dispatcher kann vor jeder Einzelbewegung 0 zurückgeben: Ein eingeschlossener Leader kann damit verbundene gesunde Mitglieder blockieren.
2. In der Gruppenflut `DA590`, `D9C40` oder `DAFD0` kontrolliert `124740` die Besuchsgeneration aller lebenden, nicht getöteten Mitglieder; nicht erreichte Mitglieder führen zu Wiederholungen mit erhöhtem Budget. Ein fehlerhafter Angehöriger kann also zusätzliche Sucharbeit auslösen. Daraus folgt keine universelle Aussage, dass jede feststeckende Einzelunit jede Gruppe immer vollständig blockiert.

Die Wiederholung ist nicht unbegrenzt: am Kapazitätsbudget `0x4E520` folgt abhängig vom gespeicherten Fallbackzustand ein letzter zielgerichteter Suchversuch beziehungsweise der passende Assassin-/Moat-Zweig. Dessen Scheitern liefert 0; andernfalls übernimmt `119E30` die alten Mitglied-Distanzwerte und der gemeinsame Orderpfad läuft weiter. Beim vorherigen vollständigen Fluterfolg übernimmt `119EE0` dagegen die frisch berechneten Werte. Beide vollständigen Helfer sind zusätzlich im Maschinenvergleich enthalten. Eine Rettung darf diesen Unterschied nicht in einen pauschalen gruppenweiten Abbruch umdeuten.

Der Fallback `118E00` hat eigene Alive-/Kill-/State-/Fähigkeitsprädikate. Vor `196280` schreibt er Ziel/AI101 und weitere Befehlsfelder um. Nachträgliche Ablehnung eines einzelnen `196280`-Aufrufs erhält diese alten Befehlsdaten nicht. Die bestehende manuelle Admission greift daher vor diesen Stores; sie ersetzt kein Rescue.

`ManualGroupAdmission.cs:103` schließt KI-Besitzer außerhalb des Editors ausdrücklich aus. Das Modsetting für verbesserte manuelle Befehle schützt somit **nicht allgemein KI-Gruppen**. Fast-Gruppenrouting und interne Wiederholungen besitzen eigene KI-Pfade; diese Aussagen sind kein Widerspruch.

Die AI-Membermarkierung typed `+428` wird von `11DD10/1245B0/126020` gepflegt und von `11B520` als Ausschluss gelesen. Sie kann bewusste KI-Auswahl darstellen. Bei Wiederholung der AI-eigenen Gruppe dürfen diese Marken nicht pauschal gelöscht werden. Native neue Aufträge, Angriff/Patrouille, Formation und Queue sind außerdem mehr als das gespeicherte Einzelbewegungsziel `744/746`.

Pathfinding besitzt bereits reale Auftragsidentität und Wiederaufnahmebedingungen (`RouteReplanWatch.cs:433`): Tribe-Global-ID/Besitzer, Sequence, neuere Order, Queue, Plain-Move-/Patrolpunktprüfung und getrennte Solo-/Move-/Group-Fortsetzung. Eine neue Rescue-Queue sollte diesen Besitzer nach definierter Schnittstelle nutzen; der Testmod kann dessen interne Klassen nicht als öffentliche API voraussetzen. Ein pauschaler `SoldierTribeMoves`-/neuer Gruppenauftrag würde gegebenenfalls neuere Befehle oder gesunde Mitglieder verändern und ist nicht freigegeben.

## 8. Ereignisse, Fehlerquellen und Ownership

| Grenze | Was sie zuverlässig liefert | Was sie nicht liefert / Ownership |
|---|---|---|
| Extender `OnUnitMoveHere` (`BulkUnitDetours.cs:1461`) | Synchrone abgefangene `196280`-Aufträge mit Unit-ID; Originalausgang nach ausgeführtem Original | Pre-Argumente sind veränderbar; Post enthält ursprüngliche Argumente. Skip und Exception liefern keinen garantierten Post. Kein Ereignis je physischem Substep. Extender besitzt den Einstieg. |
| Pathfinding `UnitMovementContext.cs:18` | Bestehende LIFO-/Epoch-/Tick-/Command-Zuordnung, effektive Pre-Args, Abandonment und Platzierungsabschluss | Ein zweiter unkoordinierter Stack wäre keine robuste Integration. Verschachtelte Aufträge invalidieren Suchkontext. |
| Pathfinding `F4930/E32B0`-Besitzer | Builder-/Rekonstruktionsausgang samt unit-eigenem Ausgabeslot und Fehlerbarriere | Probes/fremde Puffer werden nicht zu einer Unit umgedeutet; Fehler können normale unerreichbare Ziele sein. `MovementSurfacePolicy` kann unsichere Ausgaben bewusst verwerfen. |
| Pathfinding Pre-Builder-Grenze `19664B` | Identifizierte native früh gescheiterte Route in gültigem Bewegungsframe | Bestehende freundliche-Moat-Recovery ist keine allgemeine Versetzung. Kein konkurrierender Hook an dieser Spanne. |
| Extender DCD60 | Physische Oberflächenprüfung mit live typweitem Bypass | Kein öffentliches allgemeines Fehlerereignis; Einstieg bereits belegt. |
| `RouteSearchEvents` | Zusätzliche gewichtete Suchen teilnehmender Mods, synchron auf Aufruferthread | Keine universelle Unit-ID/Vanilla-/Schrittabdeckung; Weltmutation und Spielbefehle im Callback ausdrücklich verboten. |
| Gate-Diagnoseadapter um `1855A0` | Vorhandene bewusste Unterscheidung real/spekulativ und spezifische Kletterbeobachtung | DevDiag beziehungsweise Gatezweck, kein allgemeiner funktionaler Beobachtungsvertrag. Neue Call-Sites erst mit vollständiger Spannen-/Adjazenzprüfung freigeben. |

**Wichtig:** Fehlerereignisse dürfen nur Verdachtsfälle an einen nachgelagerten sicheren Punkt melden. Sie sind weder ein einschlussbeweisendes Prädikat noch eine Einladung, während einer laufenden fremden Suche Weltzustand zu ändern. Wiederholte Meldungen benötigen Identitäts-/Order-/Sitzungsdeduplizierung und ein begrenztes Budget. Ohne kausale Vorherdaten braucht eine Rettung aus alten Saves einen anderen, gesondert bewiesenen sicheren Zielort.

## 9. Ergebnis und verbleibende Freigaben

Der statische gewöhnliche Movement-/Occupancy-/Relocation-Vertrag einschließlich Abbruchpfaden ist geschlossen und gegen die installierten Funktionsbytes geprüft. Der Audit erklärt den Current-/Next-Fehler, grenzt einen gewöhnlichen Footprint+1-Halo für Ereignisdiscovery ab und weist nach, warum Fehler-/Gruppen-/Jobfälle getrennt bleiben müssen.

Es wurden **keine neuen bestätigten Runtimefehler im bestehenden konservativen Prototyp** abgeleitet. Seine dokumentierten Ausschlüsse sind bewusst und schützen vor den beschriebenen konkreten Fehlermutationen. Folgende Implementierungs-/Abnahmegrenzen bleiben:

- Halo-/Next-only-Capture, verzögerte Schrittbehandlung und Kandidatenqueue existieren noch nicht. Die alten Vorherdaten dürfen beim Warten weder veralten noch für neue Orders wiederverwendet werden.
- Eine erzwungene Midstep-Rückkehr ist trotz bekannter Feldschreiber noch kein allgemeiner Job-/Portal-/Klettervertrag. Ohne qualifizierte Familie keine Aktivierung. Die lokale Wahl zwischen sofortiger Bereinigung und Warten auf eine nachgewiesene zulässige Grenze braucht eine konkrete Implementierung und isolierte Verhaltenstests; natürliche Zwischenzustände bleiben spätere Spielabnahmen.
- Arbeiterfamilien, Belagerungswurzeln/Besatzungen und Sonderbewegungen werden einzeln qualifiziert; ein gewöhnlicher Soldatentest gibt sie nicht frei. Kein künstliches Aux-/AI-/Ladungsfeld als Ersatz für eine fehlende natürliche Vorbedingung.
- Vollständige Rückkehr zu einem Angriff, einer Queue oder AI-Gruppenabsicht verlangt den passenden Auftragsbesitzer und eine öffentliche Integrationsgrenze. Ein positiver Einzelpfad bestätigt nur dessen Veröffentlichung.
- Die spätere native Hookplatzierung braucht vor Veröffentlichung weiterhin exakte verdrängte Spanne, eingehende Kanten, ABI-/Flag-/Stackprüfung, Live-Fremdpatchkontrolle und Original-einmal-Vertrag. Dieser Audit veröffentlicht keinen zusätzlichen Hook und behauptet daher keinen Live-Spannennachweis.

### Explizite Grenze der allgemeinen Freigabe

| Bewegungsfamilie | Statisch geklärt | Aus dem Audit folgende Freigabe |
|---|---|---|
| Gewöhnlicher class0-Schritt ohne Portal | Current/Next-Produktion, Mittelpunkt, echte/spekulative Aufrufe, Ketten, Retry und sichere Stationärversetzung | Konkreter verzögerter/sofortiger Midstep-Adapter mit Order-/Lebensidentität und vollständigen Nachkontrollen; bisherige Policy lässt ihn aus |
| Class0 mit `9C8`-Moatmodus | Modus wird auf erfolgreicher Veröffentlichung gespeichert, DCE60 kann eigene Moatübergänge erlauben | Per-Job/Typ entscheiden, ob der Zustand legal läuft und welcher sichere Zielort/Resume existiert; nicht bei fehlenden gewöhnlichen Bits versetzen |
| Tunneler class1 | Untergrund-, Target-, Routengelände- und terminaler Abbaupfad einschließlich `185240` geschlossen; Abschnitt 10.1 | **Keine generische Versetzung dieser aktiven Zustände.** Klassen-/Hidden-/Tunneleffekt bleibt bei beiden Helfern bestehen; keine fehlende gewöhnliche Kante als Rettungsgrund |
| Assassin AI126–129 | Vollständige Progress-/Commit-/Höhen-/Stop- und Rückkehrkette geschlossen; Abschnitt 10.2 | **Keine automatische generische Versetzung während des Kletterns.** Bestehenden ausdrücklichen Stopbesitzer erhalten; erst wieder gewöhnlichen Zustand neu qualifizieren |
| Portal-/Transportphase | Allocation/Serial, Auswahl, Teilnehmerregistrierung, Endpunkt-/Höhenfortschritt, Identitätsverlust und Cleanup geschlossen; Abschnitt 10.3 | **Keine generische Versetzung während aktiver Verbindung.** Legale lange Übergänge beziehungsweise native Todesfolgen nicht als Einschluss behandeln |
| Andere class-/AI-/Sonderbewegungen | Der gewöhnliche Prädikatvertrag gilt nicht; derzeit konservativ abgelehnt | Nur mit benanntem Erzeuger, Consumer und terminalen Job-/Objektwirkungen zulassen; keine Standardfreigabe aus dem Vorhandensein einer Move-Methode |

Die drei ausdrücklich benannten Sonderfamilien wurden im erweiterten Audit unten tatsächlich geschlossen. Ihr Ergebnis ist eine **belegte Nichtfreigabe des generischen Relocate-Verfahrens**, keine als Spieltestproblem vertagte statische Untersuchung. Daraus folgt weiterhin keine Aussage über jeden Arbeits-/Kampfzustand aller 89 Typen. Eine spätere eigenständige Reparatur beschädigter Spezialobjekte wäre ein anderes Feature mit geändertem Verhalten; die vorhandene All-Units-Moat-Inventur ist dafür kein Freibrief.

Offene Spielbelege bleiben physische Ortsänderung bei natürlichem Bau, keine doppelte Belegung, vollständige Job-/Warenfortsetzung, CastlePlanner-Rotation im kombinierten Layout, Save/Editor, echte MP-Autorität und Lastmessung. Sie können nicht durch einen noch umfangreicheren statischen Audit in PASS umgewandelt werden.

## 10. Erweiterter Abschluss der Sonderbewegungsketten

### 10.1 Tunneler: kein gewöhnlicher eingeschlossener Soldat im Untergrund

Geschlossene Kette: `182B00 → 12E400`, Einstieg über den zugeordneten Tunnelbau/AI109, Tunnelzielsuche `187CF0/187E20 → F1400`, Veröffentlichung `196280(selector2) → E32B0/E1640`, verborgene class1-Fortbewegung `1855A0`, Arbeitsabschluss `185240`, AI4-Abbau und terminaler Lebenszustand 3. Sämtliche relevanten Funktionskörper wurden neu gelesen; die Spezialhelfer sind im installierten Bytevergleich enthalten.

- AI109 steuert die Bau-/Ausblendanimation. Im positiven Bauzweig erzeugt die erste passende Animation einen Tunnelbau über `6D580` (Struktur 57), verbindet dessen ID und Global-ID mit der ursprünglichen Unit und schreibt die Rückreferenz des Baus. Nach mehr als 32 Timerupdates übernimmt der Handler den gespeicherten Nachfolgezustand und ruft im positiven Ausblendzweig `196F80` auf.
- `196F80` setzt `9B0=1`, beendet die Route und entfernt Current-Occupancy. Das ist die beabsichtigte Hidden-/Mounting-Semantik; keine fehlende Belegung, die eine Rettung reparieren sollte. Das Gegenstück `196FD0` setzt Hidden zurück und registriert nur, wenn es zuvor gesetzt war; der generische Relocate führt dieses Gegenstück nicht aus.
- AI9 führt `187CF0` auf dem ursprünglichen Tunnelbau aus. Der Helfer versucht anhand des Baufortschritts `+1B8` alternierende Startkoordinaten und Suchbudgets 20/40/80; nach verbrauchten Versuchen wird der Baufortschritt -1. `F1400` ist eine eigene Ziel-/Tunnel-BFS mit Höhen-/Flag-/Gebäude-/Gegnerregeln und Suchstempel-/Distanzschreibzugriffen. Ein Treffer liefert globale Zielkoordinaten, nicht eine gewöhnliche begehbare Bodenroute.
- Nach Treffer setzt der Handler die Unit über `19BA40` auf die zum Tunnel gehörende Startposition und rekonstruiert mit Selector 2. Nur erfolgreicher Nachweis setzt native `6EC=1` und Flags 1. Das ist **eine Paarung mit dem Tunnelplan**, kein Beleg, dass derselbe Helfer mitten im Tunnel beliebige Bodenrettung erlaubt.
- AI3 lässt die Tunnelbewegung laufen. Am nativen Abschluss werden zuerst die aktuelle Gebäudekachel und Mauerflag geprüft. Bei geeigneter freier Position führt `185240` Gelände-/Schadensoperationen **entlang der gespeicherten Route** aus: gespeicherter Start `724/726/734`, vollständige Pfadlänge und die gepackten Richtungsbytes, nicht bloß Current oder verbleibender Cursor. Danach kann `187E20/F1400` einen weiteren Tunnelabschnitt auswählen und erneut Selector 2/class1 setzen. Scheitert das oder ist der Endort ungeeignet, folgen AI4 und zurückgesetzte Animationszähler.
- AI4 wartet auf den Abbauzähler. Zwischen 60 und 64 werden Tile-/Gebäudeschadens- und Effektaufrufe am aktuellen Ort ausgeführt, danach Terrain beziehungsweise verknüpftes Gebäude entfernt. Der gemeinsame Endzweig entfernt den ursprünglichen Tunnelbau, setzt Pfad-/Region-Dirtyflags und die Unit auf Lebenszustand 3. AI3 mit Flags 1 hat außerdem einen direkten terminalen Abbauzweig. Später entfernt `182B00 → 1869C0` den Unitrecord. Das ist ein vorgesehener Endzustand, kein Rettungsfehler.

Widerlegungsversuch: `19BA40` wird von Vanilla selbst innerhalb AI9 benutzt; könnte es deshalb beliebige Tunnelzustände retten? Nein: Vanilla bereitet dort die zum Bau gehörige Position und genau die passende Rekonstruktion vor. Der Helfer allein lässt `6EC`, `9B0`, AI, ursprünglichen Bau/Global-ID, Tunnelziel und Arbeitsphase bestehen. Bei Hidden=1 wird neue Occupancy absichtlich nicht eingefügt. Außerdem meldet `188340` nach Flags 0 Abschluss, sodass der nächste AI3-Pass Cleanup/Neusuche oder AI4-Effekte am falschen Ort auslösen kann. `187110` beseitigt weder class1 noch Hidden oder den Tunnelbau-/Jobvertrag und ist ebenso kein Ersatz.

**Entscheidung:** class1, Hidden-Tunnel und AI3/4/7/8/9/109 werden nicht über die generische Bodenrettung versetzt. Gewöhnliche oberirdische Kontrollzustände desselben Typs sind separat nach der vorhandenen ordinary-Policy prüfbar; der Typ wird nicht allein wegen seines Namens pauschal verboten. Es wird weder ein neuer Tunnel hergestellt noch ein vorhandener entfernt oder seine AI/Ladungs-/Bauphase künstlich umgeschrieben.

### 10.2 Assassin126–129: Current-Commit und Climbfortschritt gehören zusammen

Geschlossene Kette: akzeptierte Packed-Direction aus `196280/F4930`, `1855A0 → DCE60`, vollständiger Typdispatcher `16CD70`, Positionstransfer `180A80 → 186AD0/180230/18BE50/184FD0`, Rückkehr AI101 und nachfolgender gewöhnlicher Abschluss `188340`. Kampfunterbrechung ist getrennt: `195170`/`193B80 → AI106 → 1853F0/1976C0 →196280`, kein Cleanup-Ersatz für Klettern.

`DCE60` akzeptiert zuerst gewöhnliche Richtungsverbindungen. Nur im separaten Assassin-Mauerzweig wird AI126 (Quelle ohne IsWall) oder AI128 (Quelle mit IsWall) gewählt. Die Quelle/Ziel-Rohhöhen liefern eine signed Differenz; negative Differenz wird auf 0 geklemmt. Progress `A70` beginnt 0, Höhe `A6C`, Richtung/Owner-Hinweis und weitere Kletterfelder werden gesetzt. Die aktuelle Gebäude-ID muss hier nicht 0 sein. Auf normalen Keep-/Tor-/Mauerverbindungen braucht ein Assassin diesen Sonderzweig überhaupt nicht.

| Zustand | Vollständige relevante Fortsetzung | Orts-/Belegungsgrenze |
|---|---|---|
| 126 | Visual-Active setzen, Animationsvariante abhängig von Climbhöhe, Sound/Warning nur Darstellung; am Animationsende nach127, Animationszähler0 und Facing32 | Noch keine pauschale Übernahme von Next |
| 127 | Visual-Active bleibt1; Facing sinkt; höhenabhängige Darstellung und Animation. Nur passende Animationsfortschritte erhöhen Progress. Bei `Progress >= Height-35`: `180A80`, dann101, Progress0/Facing32/Visual0 | Current/Occupancy erst am Aufstiegsabschluss auf das vorbereitete Next übernehmen |
| 128 | Vorbereitungsanimation; am Ende129, Zähler0/Facing32, Effekt | Noch kein allgemeiner Ortscommit |
| 129 | Bei Progress0 zuerst `Progress=Height` und `180A80`; danach höhenabhängige Darstellung, Facingabbau und Progress−4. Unter32 nach101, Zähler0/Progress0/Facing32/Visual0 | Current/Occupancy bereits beim ersten Abstiegspass übernehmen, bevor die Darstellung endet |

`180A80` setzt Substep8/740=0, entfernt Current-Occupancy, übernimmt Next nach Current, Feinposition und Rohhöhe, registriert und ruft die echte StandingHeight-Fortschreibung auf. `184FD0` fügt eigenständige Gebäude-/Wand-/Roof-/Climbhöhen hinzu. Deshalb beweist eine scheinbar unbewegte oder vertikal versetzte Darstellung keinen festhängenden Current-Tile. Die spätere Updatephase suspendiert `1855A0` während126–129, statt einen gewöhnlichen fehlgeschlagenen Schritt zu melden.

Widerlegungsversuch: Beide Relocatehelfer können Current/Next bereinigen, also könnte man AI126–129 einfach erhalten? Nein: `19BA40` verändert den vorbereiteten Next, lässt aber Höhe, Progress, Facing, Visualflag, AI und Kletterabsicht bestehen. Der nächste127-/129-Pass würde die alte Höhen-/Animationsfortsetzung am neuen Ort ausführen; 129 kann den nun veränderten Next später erneut committen. `187110` übernimmt ebenfalls Next und lässt diese Climbfelder stehen. Bei Flags 0 kann nach der späteren Rückkehr101 zudem alter Auftrag als angekommen gelten. Das ist ein konkreter unzulässiger Zustand, kein bloß abstraktes Risiko.

Pathfinding hat bereits einen expliziten Normalisierungseigentümer: `AssassinClimbCancellationRuntime.ApplyCancellation` behält **Current und seine vorhandene Belegung** bei, setzt Substep8/AI101, Animationszähler und exakt die natürlichen Abschlussfelder zurück; danach läuft der endgültig akzeptierte native Stop. Der Aufruf ist an `TribeCommandDispatchBoundary`, ausdrücklichen `UnitStop`, Feature-/Layoutvalidierung und lebende ausgewählte Assassin-Mitglieder gebunden. Er teleportiert insbesondere129 nicht zum alten oberen Feld zurück. Das widerlegt eine pauschale Behauptung, Klettern könne grundsätzlich nie abgebrochen werden; es autorisiert aber keine automatische Rettung, keinen neuen Stop für eine fremde Gruppe und keine Änderung des Spielerbefehls.

**Entscheidung:**126–129 haben einen legalen eigenen Fortschritts-/Exitvertrag und erhalten keine generische automatische Bodenversetzung. Ein bereits erfasster Baukandidat darf erst nach tatsächlicher Rückkehr in einen gewöhnlichen qualifizierten Zustand mit aktueller Identität und aktuellem Auftrag neu bewertet werden. Ein expliziter Spieler-Stop bleibt Sache des vorhandenen Owners. Weder `kein ordinary Ausgang` noch `Current/Next unterscheiden sich` rechtfertigt den Abbruch einer legalen Kletterphase.

### 10.3 Portal-/Ladder-/Transportphase: Endpunkte, Serial und Teilnehmer

Geschlossene Kette: Allocation `D8510`/Ladder-Anmeldung `1950B0`, per-Tick-Refresh `F4540 → Tabellenhandler/D7D50 → F4760`, Auswahl `DF720`, Teilnehmerbindung `F4630`, Start/Ankunft `196280/1988F0`, aktive Progressphase `182B00 →19AB10`, Cleanup `187110`, Wiederplanung `196280/196810`. Freigabe der Connectionklasse pro Typ kommt aus `181E00` und der nativen Klasse×90-Typmatrix, nicht aus dem Large-Profil.

- `D8510` wählt freie IDs1..199, setzt Entry-State1, Klasse, Serial aus dem globalen Counter, Building- oder Unit-ID und passende Global-ID/Besitzer. Allocation legt noch keine endgültige aktuelle Region fest. Typ1 mit Unit als Quelle wird von `1950B0` angemeldet; der lokale Enumname ist `LadderClimb`, die konkrete Objektidentität ergibt sich aus Unit/Global-ID.
- `F4540` ruft bei jedem verwendeten Record dessen Klassenhandler auf, aktualisiert Teilnehmer und löscht ungültige Einträge via `DE090`. Der separat aus dem PE dekodierte `D7D50` prüft die gespeicherte Unit-Global-ID, AI3 und nicht getöteten Zustand; andernfalls Entry-State0. Bei Gültigkeit setzt er Enabled, leitet Richtung und beide Endpunkte/Regionen aus dem wirklichen Gerät ab und setzt sein Fallbackfeld. Er prüft nicht pauschal „neue Baukachel ist noch gewöhnlich begehbar“.
- `DF720` verlangt gültigen/enabled Record und zulässige Connectionklasse, unterscheidet Eintrittsrichtung und wählt nach Distanz plus Verbindungskosten. Ohne passende Verbindung ist 0 ein normaler Ausgang; der besondere Rückgabewert200 bezeichnet den vorgesehenen Fallback ohne gewöhnlichen Entry. Die Richtungsseite wird an Unit `8F4` gespeichert.
- `F4630` bindet den Unit an den benutzten Record: Serial nach `8F0`, bei den Gateklassen3/4/5 bewusst Serial0; Teilnehmerliste mit Unit-ID und Global-ID, begrenzt auf50. Fehlender Record/volle Grenze liefert−1, fehlender Suchkontext0. `F4760` bereinigt in jedem Refresh genau diese Identitäten gegen Alive, Global-ID und aktuelle Unit-Portal-ID und zählt neu. Nur die Record-ID ohne Serial/Global-ID ist deshalb keine stabile Lebensidentität.
- `1988F0` liefert für Portal0 Erfolg, für die spezielle200-Grenze keinen normalen Entry. Bei Serialmismatch löscht es die Portal-ID und verweigert die Fortsetzung. Bei normalen anderen Klassen3..7 kann es zuerst `187110` und Wiederplanung auf die gespeicherte Gesamtzielposition ausführen, danach das richtige Ende in Next setzen. Das kann einen nicht benachbarten Next-Punkt erzeugen und ist kein kaputter ordinary-Schritt.
- Bei Klasse1 sucht `1988F0` den zur Verbindung gehörenden Geräteträger in dessen Occupancy-Kette, übernimmt Orientierung, setzt `8F6=1`, Timer `9B4=0`, Progress `9BC=0` und die Richtung `9B6`. Falls nötig versetzt `185190` an den passenden Einstieg, danach wird der bisherige AI-Zustand nach `9CE` gesichert und AI104 gesetzt. Ein Eintritt von der Gegenseite verändert zusätzlich die Basishöhe. Current/Next und temporäre Höhen gehören nun zur Verbindung, nicht zu einem Bodenweg.
- `182B00` schickt `8F6!=0` nicht in den gewöhnlichen Bewegungstakt, sondern in `19AB10`. Bei gültigem Klasse1-Record erhöht dieser den Timer; erst nach96 Updates folgt ein Höhenfortschritt±15 und eine Progressstufe. Das ist eine bewusst langsame Bewegung. Erreichen/Überschreiten der Endhöhe kann den Abschluss beschleunigen. Bei Progress5 setzt er die Ziel-Rohhöhe, führt `187110` aus, stellt den gesicherten AI-Zustand `9CE` wieder her und plant das ursprüngliche Gesamtziel neu. Der normale Bewegungscontroller darf inzwischen keine vermeintlich wartende Unit isoliert versetzen.
- Bei Record-State0 beziehungsweise Serialmismatch unterscheidet `19AB10` die Lage: Für aktive Klasse1 mit negativem Progress kehrt er in einen gewöhnlichen Zustand zurück; nach bereits begonnenem gefährlichem Übergang kann er AI112 und `Killed=1` setzen. Bei den anderen Klassen kann ein spezifischer AI108-Zweig ebenfalls AI114/Killed setzen; sonst läuft generischer `187110`-Cleanup und Wiederplanung. Eine durch entfernte Leiter verursachte native Todesfolge ist nicht dieselbe Fehlerklasse wie ein lebender Soldat auf neu gebautem Bodenhindernis.

`187110` löscht Verbindungsdarstellung, `8F6`, Timer `9B4` und zusätzliche Aux-Tile-ID `6B8`, versucht vor dem Löschen deren Unlink, entfernt die alte Current-Belegung und übernimmt Next. Es **löscht nicht vollständig Portal-ID, gespeicherten AI-Nachfolger, alle Route-/Jobfelder oder Hidden**; dafür sorgen seine qualifizierten Caller und die folgende Wiederplanung. `19BA40` lässt sogar die aktive Verbindung und deren Timer/Progress stehen. Ein automatischer Aufruf während Ladderfortschritt könnte daher am neuen Bodenort alte Höhenstufen oder eine native Todesentscheidung nachholen; ein isolierter Cleanup würde umgekehrt bewusst vorgesehene Verbindungskonsequenzen abschneiden.

Widerlegungsversuch: Das neue Fixes verwendet `187110` erfolgreich, also könnte es die universelle Rettung sein? Der Fixes-Emitter normalisiert vor seinem Aufruf ausdrücklich den eng definierten Reiter-/Keepzustand und bereitet alle Zielpositionen vor. Das ist ein anderer Vertrag. Der ordinary-Unitpfad produziert keine belegte zweite natürliche `6B8`-Belegung. Die vollständige Suche nach direkten/indizierten offensichtlichen Produzenten in der bestehenden Baseline fand nur den Cleanup-Leser und andere Strukturen/Returnpuffer; Save-/Foreign-Zustände sind dadurch nicht ausgeschlossen. Ein positiver Auxfall wird daher weder erfunden noch einfach als ungefährlich behandelt. Bei fehlendem sicheren Ursprung beziehungsweise kaputter Kette bleibt die Rettung aus.

**Entscheidung:** Portal-ID, aktive Verbindung, Hidden oder nicht klassifizierter Auxzustand schließen den generischen Rettungsaufruf aus. Die Verbindung darf ihre eigene qualifizierte Completion/Cleanup ausführen; danach ist eine neue ordinary-Bewertung möglich. Es wird kein Geräteträger, Record, Serial, Teilnehmer oder AI-Nachfolger künstlich ersetzt. Der Ausschluss ist durch konkrete legitime Übergänge und konkrete schädliche Relocatefolgen belegt, nicht durch eine unerledigte Caller-/Consumer-Suche begründet.
