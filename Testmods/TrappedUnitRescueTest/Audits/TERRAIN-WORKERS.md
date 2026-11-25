# Audit: Gelände, Grabenarbeit, Wirtschaftsarbeiter und Besatzungen

Datum: 25.11.2026. Ausschließlich statischer Offline-Audit. Keine Installation, kein Spielstart, keine veröffentlichten Hooks, keine Änderungen an Runtime- oder Fremdquellen.

## 1. Identität, Nachweise und Aussagegrenze

Der SHA-256 der kanonisch installierten `CrusaderDE.dll` wurde neu berechnet und gegen `CURRENT.json` und `DATABASE_INFO.json` geprüft:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Die zugehörige Datenbank ist `sem/FBCB9319/analysis/g72CA4AA9715C/index-2328d977c2fb.sqlite`. Ihre Extender-Provenienz ist `4014ca67c96974a5e6d697665baf5351cbfebde6`; der aktuelle saubere Extender-Quellbaum hat HEAD `81a7a945c47c2c5dcc347cd1ad3661a54be02429`, Tree `3215dd016cdca0ae7c3a89001f1116ba4d06d0ee`. Quell- und installierte Extender-Version sind getrennte Identitäten; die ältere Provenienz wurde nicht still ersetzt. Aktuelle Quellverträge wurden direkt gelesen, native Fakten am unveränderten nativen Image geprüft.

Einstieg war die Nur-Lese-Abfrage `query.ps1 function/callers/callees`. Bei großen Handlern wurde anschließend der vollständige Pseudocode der bereits identifizierten Funktion gezielt aus derselben SQLite-Datenbank gelesen; die 40.000-Zeichen-Kürzung der normalen Query wurde nicht als vollständiger Funktionskörper missverstanden. Die vollständigen Apple-/Ox-Handler haben 19.715 beziehungsweise 12.422 Zeichen. Zentrale Entscheidungen wurden zusätzlich direkt aus dem installierten PE mit Capstone dekodiert.

[TERRAIN-WORKERS-EVIDENCE.json](TERRAIN-WORKERS-EVIDENCE.json) enthält den Vergleich von 100 vollständigen Funktionskörpern, insgesamt 184.173 Bytes: alle Body-Hashes stimmen mit dem aktuellen PE überein. Darin stehen auch die beiden unveröffentlichten Dig-/Fill-Einstiegsspannen und ein Boundary-Call-Ledger aller20 Wirtschafts-Handler mit den Zeilen des hashgebundenen vollständigen Pseudocodes. Ein Body-Hash ist ein Identitätsbeleg, kein ausgeführter Verhaltenstest.

Die generischen Funktionsnamen und Decompiler-Prototypen bleiben überwiegend `candidate`. **Bestätigt statisch** bezeichnet hier konkret gelesene und mit Maschinenkode abgeglichene Verzweigungen/Feldzugriffe. Es behauptet keine erfolgreiche Spielfortsetzung. Keine aus `undefined ... (void)` rekonstruierte Signatur wird dadurch zur freigegebenen öffentlichen API.

## 2. Gemeinsame Datenverträge

- Einheit-/Gebäude-IDs sind 1-basiert. Der native Unit-Anker ist `unitManager + id*0x490`, der Gebäude-Anker `buildingManager + id*0x32C`. Die Extender-Unitansicht beginnt erst am nativen Offset `0x65C`; Wire-/Save-Ursprung `0x658` ist davon verschieden.
- Kartenkoordinaten X/Y sind keine gepackten Kachel-IDs. Die nativen Writer verwenden die Zeilentabelle bei `0x402FF2C`; die 800×800-Verfügbarkeitsmaske bei `0x3A11EA4` ist davon getrennt. Ein allgemeines `y*800+x` ersetzt die gepackte ID nicht.
- Moat-Task-Slots sind 16 Bytes groß, liegen tile-manager-relativ bei `0x1F3EE30`, Lookup `0x1EA23F0`. Slot 0 ist der native Kein-Task-Sentinel; Allokation läuft über 1..63999. SlotLimit und ActiveCount stehen bei `0x2038E30/34`.
- Task: gepackte Tile-ID `+0`, X/Y als Int16 `+4/+6`, Zielmodus als Word `+8`, Ownerbyte `+C`, Fortschrittsbyte `+D`, Arbeitstypbyte `+E`, Reservierungsaufschlag `+F`. Fortschritt wird in den Writer-Branches **signed** verglichen. Die aktuelle Extender-Ansicht hat `MoatTargetMode : Int16` plus ein separates unbekanntes Word; eine ältere Datenbanktypansicht mit vier Byte Zielmodus ist kein aktueller CLR-Vertrag.
- Source: `shcde-script-extender/src/SHCDESE.BepInEx/Interop/MoatWorkTask.cs:13`, `Interop/Enums/MoatTargetMode.cs:5`, `API/GameTileManagerAPI.cs:333`. Der öffentliche Taskzugriff validiert Kapazität/Slot/Owner und ersetzt keine Änderungstransaktion.
- Flags: `TilePropertyFlag.PlannedMoat` ist Bit 14, `IsMoat` Bit 30. Fortschritt, Arbeitstyp, PlannedMoat und fertiger Graben sind unterschiedliche Zustände. Domänenwerte einer späteren Implementierung müssen Extender-Symbole verwenden.

## 3. Normaler Graben: Erzeuger bis endgültiger Änderung

### Allokation ist noch kein fertiger Graben

`0x59210(tileManager, ownerByte, x, y, mode, replace)` prüft unsigned X/Y <800 und die Verfügbarkeitsmaske. Vorhandener Task: ohne Replace unverändert zurückgeben, mit Replace nur Ownerbyte ändern. Neuer Task: ersten freien Slot 1..63999 finden; bei Erschöpfung 0 zurückgeben; Tile/X/Y/Owner/Zielmodus schreiben, Lookup und Zähler setzen. Mode 0 erzeugt Arbeitstyp 1/Fortschritt 0; anderer Mode Arbeitstyp 2/Fortschritt 4.

Alle direkten nativen Aufrufer der Allokation sind `0x59730`, `0x64460`, `0x6FE90`, `0x739C0`. Der anschließende Aufrufer entscheidet erst, ob PlannedMoat oder IsMoat gesetzt beziehungsweise gelöscht wird und welche Höhe gilt. Besonders der Zugbrückenaufbau allokiert einen Task und **löscht danach** IsMoat auf der Brückenfläche. Ein Post von `59210` beweist deshalb weder fertigen Graben noch einen endgültigen Oberflächenzustand.

`0x59730` kommt aus `0x51790` und prüft die Kachelflags, Gebäude-/Vegetationsbelegung und Höhe. Auf geeigneter niedriger Kachel führt erfolgreiche Allokation je Mode zu PlannedMoat oder IsMoat/Höhe 0; andere Höhen können statt dessen in einen Baupfad über `0x6D580` führen. Ablehnung, Taskknappheit und baulicher Alternativpfad dürfen keinen Rettungserfolg melden.

### Zielwahl und Arbeit

Der gemeinsame Selector `0x69D60` wird von exakt zehn Digger-Handlern sowie `0x3E700/0x110740` benutzt. Es sind keine Wirtschaftsarbeiter-Handler:

`13F540, 143400, 145030, 146A70, 14AED0, 169B70, 174F90, 178350, 17B540, 17CF50`.

Order 6 wählt Dig-Modus 1, Order 7 Fill-Modus 2. Eigentum/Diplomatie, Taskflags, Reservierung, Startregion und spezielle Moat-Reichweite sind unterschiedliche vorgelagerte Gates. Der Selector gibt einen Kandidatenindex mit `-1` als Fehler zurück; die Aufrufer akzeptieren ausdrücklich auch Index 0. Diesen Kandidatenindex nicht mit einer gewöhnlichen Unit-ID verwechseln. Der zusätzliche Reservierungsaufschlag greift nur für Index >0; diese engere Bedingung ändert den Erfolgstest des Aufrufers nicht.

`0x6AF60` unterscheidet Zielmodus 1 (Taskkachel) von 2 (erreichbarer Nachbar). Für Modus 2 werden Höhe und PCL der Nachbarn geprüft und der streng bessere quadratische Abstand gewählt; Ausgabe geht auch in gemeinsame Scratch-Felder. Eine erfolgreiche Suche ist noch keine veröffentlichte Unitroute. Anschließend folgen `0x196280`, Wegverkürzung, Travel-/Arbeitszustand und Animation.

Alle zehn terminalen Dig-Calls wurden mit ihren umgebenden Guards gelesen: Schon fertiger Moat oder ein durch den jeweiligen Kachelbelegungs-/Unitfeld-Test gesperrtes Arbeitsziel führt zurück zu Idle; vor Animationsabschluss wird kein Dig-Writer ausgeführt. Die Belegungsprüfung liest den Kopf der Liste und das dortige native Word `+0x9C6`; daraus folgt keine vollständige Aufzählung aller Current-/Next-Betroffenen. Bei Nichtabschluss bleibt die Arbeitsphase bestehen. Nach erfolgreicher terminaler Änderung setzt der jeweilige Handler Arbeitszähler und seinen AI-Zustand zurück. Fill-Handler unterscheiden außerdem bereits verschwundenen Moat und ihren Order-/Gruppenfortsetzungspfad über `0x122800`.

### Terminaler Dig-Writer `0x639C0`

Dieser Helper erhält den Taskslot, nicht die Unit-ID. Er erhöht Fortschritt `+D`; **signed** Wert <4 gibt 0 zurück, ohne den Graben fertigzustellen. Bei >=4:

1. vorhandenen Pitch/Vegetation auf dem Ziel entfernen;
2. Word `+D=0x0204` schreiben: Fortschritt 4, Arbeitstyp 2; Reservierung `+F=0`;
3. Kachelhöhe auf 0, PlannedMoat und Pitchflag aus, IsMoat an, Darstellungsbyte zurücksetzen;
4. Regions-/Topologiearbeit über `E3B90`, dann `E0850 -> D90D0` und nötigenfalls `E04B0`;
5. Dirtyflag setzen, Rückgabe 1.

Maschinenanker: `639DA inc byte`, `639E0 cmp ...,4`, `639E7 jl`, `63A4E mov word ...,204h`, `63A91 or ...,40000000h`, `63AB7 call E3B90`, `63ACD call E0850`.

Der Task bleibt nach Fertigstellung als Fill-fähiger Record erhalten. Ein TaskDelete-Ereignis wäre hierfür die falsche alleinige Quelle. Ein Pre-Snapshot muss **vor** der terminalen Flag-/Höhenänderung entstehen; Wiederherstellung erst nach Abschluss der zusammengehörigen Topologiearbeit. Beim ersten/dritten Dig-Fortschritt entsteht kein neu eingeschlossener Standort allein aus diesem Writer.

### Terminaler Fill-Writer `0x69470`

Er vermindert Fortschritt `+D`. Signed Wert >0 gibt 0 zurück. Bei <=0 setzt er Höhe 8, löscht IsMoat, aktualisiert Region/Topologie (`E3B90`, `E0850`), setzt Dirty und löscht erst danach den Task über `61E70`. Falls eine Gebäudekachel betroffen ist, folgt ein Visualrefresh. Rückgabe 1.

Chore 41 (`13750`) mit Dug-Moat-Sentinel -1 setzt Fortschritt bereits vor dem Aufruf auf 0. Daher entfernt dieser Benutzerpfad den fertigen Graben in **einem** Aufruf; vier Arbeitsschritte sind dort keine Voraussetzung. Der negative Fortschritt nach dem Dekrement ist ein terminaler signed Zustand, keine positive 255er-Restarbeit.

`61E70` löscht ausschließlich einen gültigen belegten Task/Lookup und vermindert ActiveCount; der Helper allein verspricht kein Flag-/Höhen-/Topologieresultat. Unterschiedliche Aufrufer können davor oder danach weitere Oberflächenfelder schreiben. Der X/Y-Wrapper `61ED0` addiert die native Zeilenbasis und springt zu `61E70`; er ist kein vollständiger Fill-Vorgang. Für Rettung niemals den Namen oder die Existenz eines solchen Lowlevel-Calls als Abschlussbeleg verwenden.

## 4. Terrain-/Höhen-Editor: vollständige äußere Writergrenzen

Die sieben aktuellen Extender-AOBs wurden im `.text` der kanonischen DLL auf Eindeutigkeit geprüft; jeweils genau ein Treffer:

| Native RVA | Extender-Methode / Eingaben | Native Änderung und Ende |
|---|---|---|
| `6FE90` | `c_game_editor_brush_set_tiletype`, Manager, Owner-/Aufrufkontext, gepackte Mitte/Y, Brush-/Flag-/Typ-/Modusparameter | Schleife über tatsächliche Brushfelder; je Surface andere Guards. Moat: Planen, Plan abbrechen, direkt fertig graben und direkt löschen sind getrennte Modi. Jede wirksame Kachel aktualisiert D90D0; optional abschließender E04B0. |
| `7AE50` | SetTileTypeToNone, Manager, gepackte Mitte/Y, Brush | Zulässige Felder: Default-/Rawhöhe restaurieren, Moat-/Surfaceflags und Typbytes löschen; Topologie pro Feld und ggf. E04B0. Kein allgemeiner Building-Delete. |
| `6FA10` | SetHeightToPreset, Manager, gepackte Mitte/Y, Brush, Preset | Erst Brushauflösung, dann zulässige Felder; Flags-/Gebäude-/Moatguards, Preset-/Höhen-/Darstellungswrites, D90D0, ggf. E04B0. |
| `70DF0` | SetHeightToNumber, Manager, gepackte Mitte/Y, Brush, Int16-Parameter | Parameterzweige 0/1/2/5 und Default unterscheiden sich; Min/Max-Ermittlung und Höhenclamps, Raw-/Referenzhöhe, Terrainkorrektur, D90D0, E04B0/Dirty. Der Methodentitel ersetzt nicht die native Modussemantik. |
| `64700` | EvenHeight, Manager, gepackte Mitte/Y, Brush | Brush-Min/Max, schrittweise Annäherung an Mittelwert, Modus-/Surface-/Gebäudeguards, 8..156-Clamp und River-Korrektur, D90D0, ggf. E04B0. |
| `6C0F0` | RaiseHeight, Manager, gepackte Mitte/Y, Brush, signed Modifier | Surfaceabhängige Min/Maxhöhe; Addieren/Clampen, Referenz-/Riverkorrektur, D90D0; nach Brush E04B0 und Dirty. Auch `71120/E4310` sind native Aufrufer neben Editor-Chore `10FC0`. |
| `62AB0` | BrushDelete, Manager, **X/Y**, Brush | Drei Pässe: Vegetation, Gebäude/Wände, Einheiten. Der dritte Pass markiert die auf den Kacheln verlinkten Einheiten ausdrücklich mit AliveState 3. |

Parameterreihenfolge von `6FE90` aus dem nativen Datenfluss: `(tileManager, issuer, packedCenterTile, centerY, width, operationFlags, variant, auxiliaryByte)`. Der aktuelle Extender nennt den zweiten Parameter fälschlich `brushSize`; die wirkliche Brushbreite ist sein fünfter Parameter `a5/Unknown1`. `EditorBrush.cs:11–30` und `Fixture.Construction.cs:61–65` verwenden bereits den richtigen nativen Vertrag. Mirror-/Receipt-Footprints dürfen daher nicht aus `eventArgs.BrushSize` abgeleitet werden. Der Extender reicht die Werte in seiner Original-Closure positionsgleich weiter; die irreführende Bezeichnung allein ist kein Beleg für eine falsche Vanilla-Änderung.

Für alle Brushfunktionen entscheidet `6A620` beziehungsweise die Footprint-Scratchlogik, welche Kacheln tatsächlich berührt werden. Ein quadratischer Radius ist nur eine obere Beobachtungsgrenze; Kachelwritedeltas sind der Erfolgsbeleg. Surface-/Gebäude-/Moatguards können ein Teilfeld überspringen, ohne den ganzen Brush fehlschlagen zu lassen. Die Topologie kann innerhalb derselben Brushoperation mehrfach geändert werden. Eine einzelne D90D0-Rückkehr ist deshalb kein vollständiger Brushabschluss.

**Editor-Löschen ist keine zu heilende Feststecksituation.** Der Datenfluss ist `62AB0 -> occupancy head 4C559B0 -> next +8FA -> AliveState=3 -> 182B00 -> 1869C0 -> vollständige Unit-/Pathfreigabe`. Rettung darf weder tote Records versetzen noch absichtlich gelöschte Einheiten reaktivieren. Der Widerlegungsversuch anhand bloßen Gebäude-/Terrainlöschens greift nicht: Der dritte Pass ist im tatsächlichen Editor-Delete vorhanden. Dagegen ist normales Chore-29/41-Bulldozern eine andere Quelle.

### Extender-Vertrag und Mirror

`BulkMapEditorDetours.cs:215–302`, `309–460`, `469–502` wurde vollständig für diese Methoden gelesen:

- Mirror-Aufrufe gehen direkt an `Original` und können **vor** dem normalen Pre-Ereignis laufen.
- Das Original erhält die gegebenenfalls veränderten Pre-Argumente; das neu erzeugte Post-Objekt enthält dagegen die ursprünglichen Parameter.
- Skip verhindert Original und Post. Ein geworfener Original-/Subscriberpfad hat keinen garantierten Post in `finally`.
- SetTileType aktiviert `_suppressMirrorForSetTileTypeToNone`; interne None-Aufrufe sollen kein zweites Spiegelbild erzeugen. Das Flag wird auf dem normalen Rückkehrpfad zurückgesetzt; für eine robuste eigene Transaktion nicht auf einen ausnahmslos laufenden Post verlassen.
- Der Mutator kann die Flagmaske vor dem Ereignis verändern. Der Wunschparameter allein beweist den tatsächlich geschriebenen Surface-Typ nicht.

Damit sind die vorhandenen öffentlichen Pre/Post-Ereignisse als **Hinweise**, jedoch nicht als lückenlose Vorher-/Nachher-Klammer aller finalen/Mirror-Writes eingeordnet. Eine spätere öffentliche funktionale Brokergrenze muss Original- und Mirror-Closures beziehungsweise den endgültigen Writer gleichermaßen erfassen, verschachtelte Änderungen halten und im Fehlerfall den Receipt verwerfen. Es wird hier kein konkurrierender Detour auf bereits vom Extender besessene Eingänge freigegeben.

## 5. Rückbau und terminale Gebäudeentfernung

Ordentlicher Bulldozer: `DLL_MapAction 85BD0 -> 8AAF0 -> 90830 -> Chore 29/41`. Permission-/Cursorphase und tatsächliche Ausführung sind getrennt. Chore 29 (`12400`) validiert Slot/Global-ID, behandelt Refund/angehängte Zugbrücken und ruft `C4290`; dieser markiert den Bau und zusammengehörige Compound-Records als Zustand 3. Das ist **noch keine fertige Terrainentfernung**.

`C60F0` besucht Gebäude im Simulationstick und ruft bei Zustand 3 `B8310`. Sofortige Löschpfade über `C43A0` setzen dieselbe Markierung und rufen `B8310` synchron, einschließlich Compound-Kindern. `B8310` erledigt zugehörige Unit-/Wirtschafts-/Gebäudebuchhaltung, `61FC0` entfernt die Oberfläche, anschließend wird der Gebäuderecord gelöscht und pathingseitig invalidiert. `61FC0` hat eigene Permission-/Spezialtypbranches; Drawbridge-Typ benutzt `622D0`, andere Typen andere Floor-/Wall-/Proxy-Cleanupfunktionen. Compound-Rückbau ist eine Menge wirksamer Records, nicht nur die angeklickte ID.

Wall-Chore 41 ruft nach Refund `63700`. Dieser löscht zuerst das Pendingbit und prüft eine separate Belegungsschicht; nur erfolgreicher Abbau restauriert die Höhe, löscht Wallflags und aktualisiert Topologie. Ein Refund oder Post des Enqueue-/Markierungswegs beweist keine entfernte Wand. Nach regulärem Abbau normalisiert `182B00 -> 19B260` die verbleibenden Einheitenhöhen. Bloßes Höhenanpassen braucht keine Rettung.

Die aufgeführten vollständigen Bodies schließen den relevanten Lebenszyklus des Rückbaus; der bestätigte Abschlussbegriff ist tatsächlicher Kachel-/Gebäudezustand nach terminalem Cleanup, nicht Markierung oder Eingabebefehl. Eine Terrainänderung kann Zugänge schaffen oder beseitigen; Rettung bleibt an neu verlorene legale Bewegung gebunden. Verlust des Arbeitsplatzes durch Rückbau ist eine reguläre Arbeitszustandsänderung und keine Erlaubnis, das entfernte Gebäude per Teleportfortsetzung zu simulieren.

## 6. Wirtschaftsarbeiter: Reise ist kein beliebiger Arbeitszustand

### Gemeinsame Fortsetzungsfalle

`19BA40` setzt PathState `+74E=0`, Cursor/Teilfelder zurück und Substep `+750=8`, hängt die Current-Belegung um und normalisiert Current/Next/Fineposition. Es setzt **keinen** passenden Arbeitszustand und startet keinen Weg zum Arbeitsplatz/Ziel.

`188340` prüft PathState 0 und gegebenenfalls den Portal-/Zielvergleich; im gewöhnlichen Fall ist es **kein geometrischer Zielabstandstest**. Viele Jobs lesen diesen Helper als Reiseende. Daher kann „AI-State/Cargo blieb gleich“ nach Teleport trotzdem falsch sein: Die nächste Arbeitsaktualisierung hält einen noch nicht erreichten Auftrag für angekommen. Der bestehende Guard `NeedsExplicitCivilianResume` und die synchrone Reissue sind deshalb notwendig. Ein fehlgeschlagener Reissue darf keinen erfolgreichen Rettungsstatus und keinen erfundenen Idle-/Arbeitsfallback erhalten.

### Apple `135E30`: alle regulären Zustandszweige gelesen

| Zustand | Tatsächlicher nächster Pfad | Rettungsgrenze |
|---|---|---|
| 0/1 | Arbeitsplatz-/Baumauswahl, Animation/Warten, Tree-Endpunktwahl; beide Endpunkte können fehlschlagen | Auswahl/Warten ist kein Reiseauftrag; keine generische Jobumschreibung. |
| 2/8 | Route konsumieren über 188340; danach ausgewählten Tree-Typ prüfen und Zustand 3 beziehungsweise Rückkehr setzen | Nach Relocation nur mit erfolgreichem synchronem Originalziel-Reissue weiterlaufen lassen. |
| 3 | Ernteanimation, Zyklus-/Baumzähler, weitere Treewahl oder Storesuche; nach Animationsende kein neuer allgemeiner Distanztest | Stationäres Picken nach Teleport wäre unbelegt/unsicher. Weiter ablehnen. |
| 4/6 | Rückkehr-/Wartephase und spätere Storesuche/Produktion | Retry-/Transitionsflags sind zusätzliche Eingaben; im vorhandenen Prototyp nicht allgemein zugelassen. |
| 5 | Arbeitsplatz-Reise/Retry mit 19B930 und 196280, anschließend 188340 -> Zustand 0 | Aktive Route samt Originalziel und Identitäten erhalten; kein vorgetäuschtes Reiseende. |
| 7 | Transport; vor Entladen 188340, C9080-Nähe und BD470-Kapazität; falscher/full Store führt zu BF9F0/C90E0/196280-Rewahl oder Cargo-Verwerfen im Original | Reissue sichert nur das aktuelle Segment; reale Lieferung muss später geprüft werden. |
| 108/110/111 und verwandte Sonderzweige | Anhalten, Sterbe-/Retirementanimation, Timer >32 -> AliveState 3 und späterer Recordrelease | Kein Rescue-Reaktivieren solcher terminalen Records. |
| 121 | Leisure-/Pause-Fortsetzung über 185430 | Eigene gespeicherte Arbeits-/Besuchszustände; kein gewöhnlicher Reise-Snapshot. |

`BF9F0`/`C90E0`/`B6100` sind keine reinen Positionsleser: Storewahl, Erreichbarkeit, Identität/Kapazität und tatsächliche Wareneinlagerung bleiben Originalentscheidungen. `19B930` validiert Home-ID/Global-ID und schreibt nur Heimkoordinaten; es erzeugt keinen Weg. Der vollständig gelesene Apple-Handler stellt nach Segmentabschluss gegebenenfalls ein neues Ziel und weitere Jobs ein. Eine Rettung darf diese späteren nativen Entscheidungen nicht durch dauerhaftes Kopieren des alten Ziels übersteuern.

Der bestehende AppleMoatReceipt (`RescueRuntime.cs:225–293`) ist enger als der generische Apple-Classifier: ausschließlich Reise 5/7, Flags 2/4, Substep 8, Current=Next, gewöhnliche Klasse und Marker 0. Er erfasst Boden/Kandidaten **vor** dem Writer, fordert neu fertigen Moat ohne legalen Exit und einen tatsächlich abgelehnten Originalziel-Publish, prüft Employee/Home/Store/Cargo/Tree/Zähler, versetzt einmal und veröffentlicht sofort das ursprüngliche Jobziel. Er ist synchron/Single-use und nicht der automatische Grabenabschlussbeobachter.

### Quarry Ox `132910`: alle regulären Zustandszweige gelesen

- Fehlende beziehungsweise Global-ID-falsche Tether-/Workplace-Verknüpfung setzt Zustand 110. Eine neue Route darf diesen Verlust nicht kaschieren.
- Zustand 0/4 wartet/belädt/animiert. Die nativen Abfahrtschecks verwenden Cargo >=8; Mods können legitim kleinere Produktionsbatches besitzen. Diese Produktionskorrektur hat APIShared als eigenen Besitzer; Rettung setzt Cargo nie auf acht.
- Zustand 1 enthält Abfahrtsdelay und echten Reiseverbrauch über 188340, danach Zustand 2. Das ist der vorhandene freigegebene aktive Ox-Reiseabschnitt.
- Zustand 2 entlädt zeitlich versetzt je Ware mit C9080-Nähe, BD490-Kapazität und B6100-Global-ID-Validierung. Voller/falscher Store führt zu Neuentscheidung; erfolgloser Ersatzweg kann Cargo im Original verwerfen. Das darf eine Rettung nicht als ihren eigenen Erfolg zählen.
- Zustand 3 ist Heimreise/Retry mit einem One-shot-Flag, 19B930 und 196280. Fehlgeschlagener Weg auf gewöhnlichem Boden setzt einen Retrytimer 10; auf Moat beziehungsweise bei verlorenem Home folgt 110. Dieser eigene Fehlervertrag ist nicht allein durch die Zulassung von Zustand 1 abgedeckt.
- Zustand 110 läuft nach >32 Aktualisierungen in AliveState 3; spätere Unitfreigabe löscht denselben Record und seinen Weg. Identitäts-/Alive-Prüfung vor jedem verzögerten Eingriff ist zwingend.

Erzeugerkette des Cargos: Tether `AD180` erzeugt den Ox, Grunt `1319A0` liefert eine abgeschlossene Eingangsmenge und ruft `18D940` mit der **Ox-ID**, nicht seiner eigenen ID. Der Wert wird dem Ox-Cargo zugewiesen. Cargo/Fraction/Extra/Jobbindung werden gespeichert, übertragen und in Checksummen einbezogen; sie sind kein Diagnosefeld. Die allgemeine Rettung darf weder neu erschaffen noch per Auswahl-/Rekrutierungspfad ersetzen.

### Vollständige Fortsetzungsmatrix aller 20 Wirtschaftsrollen

Die folgende Ergänzung untersucht die Reise-/Ankunfts-/Kontakt-/Cargo-/Retrypfade aller 20 Rollen. Ausgangspunkt ist der bereits vorhandene vollständige Rollen- und Selektoraudit [MOAT_TRAVERSAL_ALL_UNITS.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOAT_TRAVERSAL_ALL_UNITS.md); die konkreten State-Zweige, Arrival-Calls, Writes nach Arrival und terminalen Alternativen wurden für diesen Rettungsauftrag erneut gezielt gelesen. Die generischen Decompilerzeilen allein bestimmen nicht die Freigabe: Ein Handler verwendet denselben lokalen Variablennamen später teilweise für einen Gebäudetyp. Die Tabelle folgt den tatsächlichen AI-State-Zweigen am Handleranfang und ihren Returns/Gotos.

**R** bezeichnet einen statisch abgegrenzten gewöhnlichen Reiseabschnitt: Er kann in einer späteren Implementierung unter dem unten stehenden gemeinsamen Erhaltungsvertrag wieder veröffentlicht werden. **S** bezeichnet einen Abschnitt mit zusätzlichem Entity-/Verbundvertrag. **K** ist Kontakt/Produktion/Animation und darf durch eine allgemeine Reise-Rettung nicht unverändert an einen beliebigen Ort fortgesetzt werden. Die Zahlen sind hashgebundene native AI-Statewerte zur Auditbeschreibung; es werden keine neuen Runtime-Zulassungen oder Tests als bestanden behauptet.

| Rolle / Update | Reise- und Folgezustände | Zu erhaltender Jobzustand; Kontakt-/Fehlergrenze |
|---|---|---|
| Woodcutter `12BC00` | R1 Baumapproach, R4 Heimtransport des bearbeiteten Holzes, R8 Lagerlieferung, R9 Heimreise/Retry. 1 prüft nach 188340 Baumabstand und gegebenenfalls E0EB0-Neuapproach; 4 lagert am Arbeitsplatz ein; 8 verwendet C9080/Storekapazität; 9 ->10 ->Idle. | Tree-ID **und Vegetationsserial** `9A0/700`, Arbeitsplatz/Slot/Global, Store/Global, Cargo und Zähler. K2 Fällen/K3 Baumverarbeitung ändern Vegetation über 107C90/1086D0/108E40. 5/6/7/10 sind Arbeits-/Animation-/Selektionsphasen, keine universellen Reisephasen. Heimfehler kann 110 und den Hut-Retirementzähler erreichen. |
| Fletcher `12D230` | R1 Holzeinkauf -> C9080/Stock/Global-Prüfung -> Warenausbuchung -> R2 beladene Heimreise; R2 lagert Input am Workshop ein; R3 Heimreise/Retry nach Zyklus; R7 Waffenlieferung mit wiederholter B9AA0-Prüfung. | Input-Cargo, Store/Global, Arbeitsplatz/WorkerSlot, für diesen Produktionslauf erfasster Waffen-Goods-Typ. K4/5 Produktion; 6 Storewahl, 8 wartender Fehlpfad. Nicht nachträglich den inzwischen umgeschalteten Workshopprodukt-Typ als Fracht ausgeben. |
| Hunter `12FC70` | R2 Heim-/Fleischtransport, R5 Lagerlieferung, R6 Heimreise/Retry. S1 lebende Beute verfolgen; S11 zum identifizierten Kadaver. 1 prüft aktuelle Entitydistanz/Reservation und kann erneut ansetzen, 11 prüft Global-ID und beeinflusst die Kadaverzeit. | Beute-Slot/Global `9F6/9F8`, Ziel-/Jagdverknüpfungen und Reservation `AA4` der Beute, Meat/Cargo und Arbeitsplatz. K9 Ausrichten/K10 Schuss; K3/4 Arbeits-/Verarbeitungsphasen. Ein statisches Tileziel allein erhält keinen beweglichen Beuteauftrag; native Rewahl und Verlustpfade bleiben maßgeblich. |
| Quarry Mason `131430` | R2 Arbeitsplatzreise; nach 188340 folgt C9080 und Arbeitsplatzregistrierung/Verbergen, sonst neue Homekoordinaten beziehungsweise 110. | Zugewiesener Quarry-/Worker-Slot samt Global und Sicht-/Konvertierungsstatus. K1/3 Arbeits-/Animationszustände. Kein eigenständiger Steinlieferjob; nicht mit Grunt oder Ox vereinigen. |
| Quarry Grunt `1319A0` | R3 publizierter Transportabschnitt -> Cargoabschluss -> R4 Heimreise; R101 Quarry-/Inputreise -> je Cargo Zustand 2 oder 1; R102 Lieferreise -> Kapazität/Storekontakt oder Zustand 6. | Quarry/Tether, gegebenenfalls Ox-Slot/Global, Store/Global, Carry sowie Reserven/Workcounter. K1/2/6/7 können wählen, abbuchen oder Cargo an einen anderen Record übertragen. Bei Ox-Ertragszuweisung ist die aktualisierte Einheit ausdrücklich **nicht** das Ertragsziel. Ein Grunt-Reissue darf keinen zweiten Yield auslösen. |
| Quarry Ox `132910` | R1 ausgehend; R3 Heimreise/Retry ist separat beschriebener nativer Reiseabschnitt, im derzeitigen Prototyp weiterhin nicht zugelassen. 1->2, 3->4. | Tether-Slot/Global, Store-Slot/Global, Carry/Abfahrtsdelay, Arbeits-/Retryflags. K0/4 Beladen/Warten, K2 tatsächliche Entladung. Homeverlust/Moat-Rückkehrfehler ->110. Keine Cargo-Mindestmenge künstlich herstellen. |
| Pitchman `1332B0` | R3 Lagerlieferung (188340, C9080, BD490, Rewahl), R4 Heimreise (19B930/Retry ->109 mit gespeichertem Folge-State1). | Arbeitsplatz/Slot/Global, Pitch-Cargo/Storeglobal, SavedState und Fade-/Verbergenfelder. K1 Produktionsfortschritt/K2 Auswahl der Lieferung; 5 ist separater Fehl-/Wartepfad. Der 109-Übergang ist reguläre Arbeitsplatzanimation, kein pauschaler Tod. |
| Wheat Farmer `133DD0` | R2 Heimreise, R3 Feldapproach ->4, R5 weiterer Feldabschnitt -> Feldzustandwrite und nächster Weg, R6 Ernteapproach ->7, R8 Abschlussapproach ->9, R10 Lagertransport. | Originales Feld `730/9E0` und Ziel-/Cropkoordinaten, Workplace/Global, Work-/Cropcounter, Carry und Storeglobal. K4 pflanzt auf dem Feld, K7 erntet/altert Cropstage, K9 weitere Arbeitsanimation. BBE00 ist eine Cropwahl ohne eigenen PCL-Filter. Niemals aufgrund erloschener Route einen fremden Feld-State schreiben. |
| Hops Farmer `134F00` | R2 Heimreise, R3 Pflanzapproach ->4, R5 Ernteapproach ->6, R8 Lagerlieferung. R5/R8 können direkt zur nächsten Farmphase führen, ohne Pause. | Feld-/Nachbarkoordinaten und Farmtype, Cropstage und Zähler, Cargo/Storeglobal. K4 Pflanzwrite/K6 Erntewrite/K7 Selektion/Arbeitsfortsetzung. BBB70 verlangt passende PCLs zwischen Farmaccess und Arbeitsnachbar; ein aufgegebener alter Arbeitsbereich darf nicht erfunden werden. |
| Apple Farmer `135E30` | R2/8 Treeapproach, R5 Home, R7 Lager; genaue Branches oben. | Tree/Jobcounter, Workplace, Home/Storeglobal und Cargo; K3 Picking. Der fixturebezogene Moat-Receipt bleibt enger auf5/7. |
| Cattle Farmer `136D40` | R2 Heimreise ->1, R5 Käselieferung mit C9080/BD470/Rewahl. | Arbeitsplatz/WorkerSlot, relevante Vieh-/Farmzustände, Cheese-Cargo/Storeglobal, Retry-/Pausenflag. K1 Produktions-/Idleübergänge, K3/4 Arbeits-/Storesuche. Keine Kuh an einen Teleport binden oder Produktionszähler vervielfachen. |
| Miller `1377C0` | R2 Home/Idle, R3 Weizen holen, R4 beladen zum Millenplatz, R8 Flourlieferung. R3 entnimmt Weizen erst nach C9080/Global/Stockprüfung, R4 registriert den eigenen der drei Worker-Slots. | Input-/Output-Cargo, Millen-WorkerSlot und Worker-ID, Workplace-/Storeglobals, gewählte Zutat, Flourproduktion. K5/6 Produktion/Registrierungswarten, 7 Storewahl. Drei Worker sind drei Identitäten; ein globaler „Miller fertig“-State ist falsch. R8 kann direkt neue Zutat holen. |
| Baker `138850` | R2 Home/Idle, R7 Flour holen, R8 beladen Heim ->109/Folgestate4, R6 Breadlieferung. | Flour-/Bread-Cargo, Workshop-/Storeglobals, Inputreserve, SavedState/Fade, Worktimer. K3/4/5 und109 Produktion/Animation, nicht einfaches Idle. Vor Start zusätzlicher Input-Approach-Guard1988B0; R6 kann sofort R7 starten. |
| Brewer `139950` | R2 Hops holen, R3 beladen Heim ->109/Folgestate6, R4 Home/Idle, R8 Alelieferung. | Zutat/Cargo, Workplace/Storeglobals, SavedState, Reserven/Timer; K5/6/7 und109 verarbeiten/animieren/wählen. Wiederholte Stocksuche hat andere Seiteneffekte als bloßes Reissue des bestehenden Segments. |
| Poleturner `13AAD0` | R2 Holz holen, R3 beladen Heim, R4 Home/Idle, R8 Waffenlieferung. | Erfasster Spear-/Pike-Goods-Typ, Store/Workshopglobals, Input-/Output-Cargo, SavedState; K5/6/7 und109 Produktions-/Abschlussphasen. Nach Arrival niemals einen neu umgeschalteten Workshop-Typ auf die alte Fracht übertragen. |
| Blacksmith `13BDD0` | R2 Iron holen, R3 beladen Heim ->Work6, R4 Home/Idle, R8 Waffenlieferung. | Captured Mace-/Sword-Typ, Input-/Output-Cargo, Store/Workshopglobals und Work-/Restcounter. K5/6/7/9,106/109 sind Produktions-/Animations-/Wartepfade, keine zu normalisierenden generischen Idlewerte. |
| Armourer `13CF30` | R2 Iron holen, R3 beladen Heim, R4 Home/Idle, R8 Armourlieferung. | Iron-/Armour-Cargo, Workshop/Storeglobal, SavedState und Input-/Workcounter. K5/6/7/9 und109 bleiben positions-/produktionsgebunden. Armourcapacity B9AA0 ist die gemeinsame Waffenlagerkapazität, nicht beliebige Foodkapazität. |
| Tanner `13E0B0` | R4 Home/Idle, R8 Armourlieferung. **S2** Kuh suchen/annähern; **S3** gemeinsame Heimreise von Tanner und Kuh, mit zwei verschiedenen Unitmoves und gegenseitiger Slot/Global-Verknüpfung. | Cow-ID/Global, Kuh-Folgezustand, separater verkürzter Cowpfad, Hide-/Armour-Cargo, Workshop/Storeglobals. K9 Schlachtung setzt Cowstate und erzeugt drei Hides; K5/6/7/109 Produktion. S2/S3 benötigen einen Verbund-/Entityreceipt; eine Einzelunit-Standardrettung ist hierfür unzureichend. |
| Miner1 `14D6F0` | R2 Arbeitsplatzreise; nach Arrival C9080, Worker-Slotregistrierung, Verbergen, State1. Fehlt die Nähe, neuer Home-/Retrystart. | Mine-/WorkerSlot/Global; K1 Arbeit. Kein eigener globaler Lagertransport und kein beliebiger `state1->move`-Ersatz. |
| Miner2 `14DAC0` | R2 Arbeitsplatzreise; R4 Ironlieferung mit C9080/BD490/Rewahl. State1 prüft über193F10 die acht Gebäude-Nachbarn und leitet sonst wieder R2 ein. | Zugewiesene Mine/Slot/Global, Storeglobal, Cargo/Retry/Workcounter. State3 bucht produzierte Ware erst nach erfolgreichem Routepublish aus dem Minebuffer aus und setzt Carry; State5 wartet. Diesen Selektions-/Entnahmebranch nicht erneut zum Retten aufrufen. |

#### Gemeinsamer Erhaltungsvertrag für R-Abschnitte

1. Snapshot einer lebenden Identität enthält Owner, Unit-/Workplace-/Store-Slots mit den jeweils von Vanilla geprüften Globals, WorkerSlot, AI-State, SavedState, Cargo, Ertrag/Fraction, Retry-/Pause-/Produktionsfelder und, je Rolle, Crop-/Vegetations-/Entityreferenzen. Der Satz ist rollenabhängig; nicht nur die sichtbare Ware sichern.
2. Erlaubnis nur bei einer **vorher vorhandenen aktiven Route** (bisher Flags2/4), einem belegten gewöhnlichen Bewegungsmodus und einem gesondert freigegebenen Schritt-/Belegungszustand. `State=R` allein genügt nicht: Viele R-States enthalten vorgelagerte Wait-/Retry-/Homewahlbranches. Kein Job-/Goodswrite darf zwischen Capture und Recovery passiert sein.
3. Nur die Position/gewöhnliche Belegung normalisieren; innerhalb derselben Simulationstransaktion vor der nächsten Handlerausführung das gespeicherte bestehende Segment neu veröffentlichen. Diese Aktion darf weder den ursprünglichen Selektor, Yield, Inputabzug, Cropwrite, Schlachtungs- noch Storedeposit wiederholen. Keine dauerhafte Beobachtung, die jeden späteren nativen Zielwechsel mit dem alten Ziel überschreibt.
4. `198620` kürzt einen publizierten Weg und schreibt seine Endpointfelder um. Die Rolle besitzt teilweise bewusst um1/2 Felder verkürzte Approachwege. Ein Rettungs-Reissue auf den bereits gespeicherten Endpunkt darf nicht nochmals unbesehen dieselbe Kürzung anwenden. Ein Entity-/Feldziel und der tatsächlich gespeicherte Segmentendpunkt sind nicht identisch.
5. Heim-, Store-, Crop- oder Entityverlust während verzögerter Beobachtung verwirft den alten Receipt. Native Ersatzsuche, Kapazitätsprüfung, Work-/Restrollup und terminale Freigabe bleiben autoritativ. Warenbestand darf weder zur Kompensation des Fehlers erhöht noch vorsorglich gelöscht werden.
6. Positiver Reissue beweist nur die neue Route. Fortgesetzte physische Arbeit und reale Warentransfers sind spätere Spielabnahme. Ein erfolgloser Reissue bleibt ein ausdrücklicher Fehler, auch wenn die lokale Position bereits geändert wurde; der Helper garantiert keinen Rollback.

#### Gemeinsame Writer-/Readerketten, Fehler und Terminale

- `B9280` wählt Inputstores nach Typ/Owner/Stock, ohne universellen PCLfilter; `C90E0` und der anschließende Move sind eigene Gates. `BF9F0` behandelt Piles und Foodstore verschieden; `B9AA0 -> C6EA0` sichert Weaponstore/Global/Sharedcapacity. Keine dieser Funktionen ist als folgenloser allgemeiner Rettungssensor einzusetzen.
- `C9080` ist ein strikt `<3`-Vergleich auf beiden Tileachsen zwischen aktueller Unit und Gebäudeaccess. Das ist ein zusätzlicher Kontaktcheck an vielen Abhol-/Liefergrenzen, aber nicht an jedem Arbeitsplatz-/Animationsabschluss.
- `BD470` testet die gemeinsame Foodmenge; `BD490` prüft passende Good-Belegung und freie Kapazität einer Pile. `B6100` validiert Global-ID, berechnet signed Bestand+Delta und schreibt nur bei passenden Grenzen/Writeflag; es verändert auch Playergoods, Visuals und Gebäude-/UIzustand. Erneutes Aufrufen zum bloßen „Prüfen“ wäre ein Warenwrite.
- `18D940` schreibt Ertrag-/Fractionfelder und kann eine andere Unit als den gerade ausgeführten Handler erhalten. `193A70` schreibt Workintervall/Rollup; `191050 ->191070` kann SavedState sichern und121 beginnen; `185430 -> BF0A0 ->196280` publiziert einen echten Freizeitweg und verbraucht Zufall. Diese Pfade sind kein No-op und kein allgemeiner Reissueersatz.
- Vegetationsarbeit: `107E00/E0C20/E0EB0` wählen/verschieben Baumapproaches; `1086A0/1086D0/107C90/108E40` validieren und ändern Baumzustände. Feldarbeit: `BBE00/BBB70`, Cropstage-/Gebäude-Reads und tilebezogene Writes in Wheat4/7/5/8 beziehungsweise Hops4/6. Jagd-/Tannerpfade prüfen Slot **und** Global, bewegen beziehungsweise reservieren eine zweite Unit und können diese töten. Die S-/K-Abgrenzung folgt diesen konkreten Seiteneffekten.
- Normales Scheitern kann Idle/Retry, Carryverwerfen, neues Home, neue Source oder110 auslösen. 108/109 sind je Handler auch gewöhnliche Halt-/Fade-/Produktionszustände; nicht pauschal als Tod deklarieren. Die tatsächlich terminalen110-/Deathbranches setzen AliveState3; `182B00 ->1869C0` entfernt Membership/Occupancy, leert0x490-Record und1000-Byte-Pfad. Alte gespeicherte IDs dürfen anschließend nicht auf einen wiederverwendeten Slot angewandt werden.
- Woodcutter110 trägt zusätzlich zum Hutcounter bei, der `ACF40/3B270/3B360` und schließlich `C4290 -> C60F0 -> B8310` beeinflusst. Ein Rettungsmod darf diese wirtschaftliche Wirkung nicht blind durch Reset des Worker-State oder Hutcounter unterdrücken. Das wäre eine eigene Verhaltenskorrektur.

Damit ist der Offline-Vertrag für **alle20 Familien** in Reise-, Entity-/Verbund-, Kontakt-/Produktions- und terminale Klassen aufgeteilt. Die sichere allgemeine Wiederveröffentlichung ist unter den genannten Vorbedingungen beschrieben; sie ist noch nicht implementiert. Der bestehende automatische Prototyp bleibt enger. Das ist eine Implementierungs- und spätere Abnahmelücke, kein fehlender statischer Nachweis eines vermeintlich universellen „alle Worker auf Idle“-Vertrags.

## 7. Geschütz und Ingenieure als Verbund

Die native Unitpass-Kette `182B00/1839C0` aktualisiert gebundene Engineers nur aus dem **Prefix der ersten CrewCount-Slots**. Crew-ID-Array liegt bei `96C`, Globals bei `974`, Count bei `A0C`. Der gleiche Offset `974` bedeutet beim Engineer einen Timeout; beim Geschütz ist er eine Crew-Global-ID. Ein ungeprüfter gemeinsamer Feldname wäre falsch.

Ein Engine-Snapshot braucht: zulässigen beweglichen Gerätetyp/-zustand, vollständige eindeutige Crew-ID/Global-Paare, genau zum Count passendes Prefix, lebende Engineers mit State 5 und Mount/Hidden-Bindung, keine zweite Engine mit derselben Crew. Veraltete Globals in leeren hinteren Slots sind möglich. Der aktuelle Guard `RescueRuntime.cs:150–185` prüft diese Bedingungen; seine fremde-Engine-Suche ist ein begrenzter Kandidatenprüfpfad, kein zulässiger pauschaler 10.000-Einheiten-Poll pro Tick.

Folgezustände aus [SIEGE_ENGINEER_CREW_TIMEOUT.md](../../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIEGE_ENGINEER_CREW_TIMEOUT.md): Engineer-Timeout >120 löst die Bindung; normale Gerätetode prüfen Crew-Global-IDs und markieren die Crew zur Löschung; Dismount/Deployment haben eigene Übergänge und Slotbereinigung. Automatische Crewwahl kann in bestimmten KI-Branches bereits gebundene Engineers erneut wählen. Rettung darf solche aliasierten Saves nicht adoptieren.

Zusätzlich prüft `182B00` bewegliche Catapult-/Arab-Ballista-Roots auf fertigem Moat vor dem Typ-Handler und kann Zustand 110 setzen. Der Gerätetyp-Handler lässt 109/110 nach Timer >32 terminal löschen. Ein vorhandener gültiger Weg allein verhindert diese Entfernung nicht. Trebuchet/Mangonel/Ballista haben eigene stationäre Command-Gates; eine Rettung darf ihnen keinen gewöhnlichen Move erfinden. Burning-/Konvertierungszustände gehören ebenfalls nicht zur Freigabe.

Der automatische AIV-Pilot schließt Zivilisten und Siege-Roots aus (`GeneralRescueObserver.cs:47ff` und zugehörige Capturepolicy). Kontrollierte Fixture-Geräte beweisen daher keine automatische Abdeckung. Eine spätere Verbundrettung muss vor dem nächsten Simulationsschritt die Crewpositionen/Bindings und den Root synchron konsistent halten, ohne Timeout oder Job künstlich zu nullen. Das statische Layout ist belegt; reale Sichtbarkeit, Occupancy nach dem nächsten Crewrefresh und Fortsetzung eines Geräteauftrags bleiben eine Spielabnahme.

## 8. Hookbesitzer, Spannen und Integrationsfolgerung

- Pathfinding besitzt in `NativeMovementRecovery.cs:55–87` tatsächlich `D90D0`, `59210`, `61E70`, `DAA50`. Diese Post-Invalidierungen dürfen Kandidaten markieren, aber nicht mitten in einer unfertigen äußeren Operation versetzen.
- WorkTargetHooks besitzt `69D60/6AF60/6C490`. Es ergänzt Ziel-/Approachregeln über bereits gemeinsame Reachability-Kontexte; daraus folgt kein Dig-/Fill-Abschlussereignis.
- `NativeDetourContracts.cs` führt `639C0/69470` als geprüfte Prefixe. Die aktuelle Source-Suche findet dort **keine dazugehörige installierende Runtime**; ein Katalogeintrag allein ist kein Hookbesitzer und kein veröffentlichtes Feature.
- Für einen späteren NativeX64-Indirect-Eingang wurden die tatsächlichen Minimalspannen statisch geprüft: Dig `[639C0,639C6)` = `mov [rsp+18],rbx` (5) + `push rsi` (1), Fortsetzung `sub rsp,20`; Fill `[69470,6947A)` = zwei fünf Byte lange Stack-Saves, Fortsetzung `push rdi`. Keine RIP-Operandrelokation oder verdrängte Branches in diesen Spannen. Im aktuellen Xrefindex gibt es keine Referenz auf ein Inneres der Spannen. Die unmodifizierten Funktionskörper enthalten dort keine internen Ziele.
- Das ist keine Hookveröffentlichung und keine Garantie für spätere Live-Patches. Eine Umsetzung muss den tatsächlich verwendeten Backendvertrag, unverändertes Ladeabbild, Live-Spanne, Namespace/Sitebesitzer, dauerhafte Rooting und Missions-/Editor-Rechecks erneut prüfen. Absolute-/Inline-Hooks mit anderer Minimalgröße sind durch diese 6-/10-Byte-Analyse nicht freigegeben.
- Editor-Eingänge gehören dem Extender. Keine doppelten eigenen Eingangshooks. Neue allgemeine funktionale Broker gehören an vorhandene Original-/Mirror-Owner oder eine ausdrücklich auditierte gemeinsame Grenze; endgültige Rettungsentscheidungen bleiben beim Rettungssystem.

**Abgeschlossener statischer Befund:** Es gibt geeignete getrennte endgültige Writer für normales Dig/Fill und äußere Editoroperationen. Die bestehenden Lowlevel-Invalidierungen sind keine universelle fertige Weltänderung. Sicheres Kandidatengewinnen muss diese Unterschiede, Current/Next und absichtliche Löschungen erhalten. Ein einziger globaler „Tile geändert -> Teleport“-Callback ist aus den geprüften Verträgen nicht zulässig.

## 9. Noch ausschließlich durch Laufzeit abnehmbar

Die folgenden Punkte sind keine fehlenden Suchtreffer, sondern die verbleibende Abnahme der späteren Implementierung:

1. Tatsächliches Ereignistiming und vollständige Betroffenenmenge bei nativen Current-/Next-Teilschritten, verschachtelten Änderungen und Mirroraktionen unter gemeinsam installierten Mods.
2. Tatsächliche physische Apple-/Ox-Zielankunft, nächste Ernte-/Lieferzyklen, unveränderte Warenmengen und kein zweiter Rettungseingriff. Ein positiver 196280-Return ist nur Publikation.
3. Vollständige Geräte-/Crewkonsistenz nach dem nächsten echten Unitpass, einschließlich natürlicher Bindungs-/Timeoutzustände; kein synthetisch erzeugter State ersetzt diesen Ablauf.
4. Editoransicht/Höhenkorrektur und absichtliche Löschung; Darstellung folgt nicht aus den reinen Datensätzen.
5. Multiplayer-Autorität, identische Kandidatenreihenfolge und Checksummen sowie reale Kosten unter 10.000 Units/800×800. Alle hier beschriebenen Regeln müssen beim späteren Entwurf budgetiert und deterministisch umgesetzt werden.

Die älteren Ingame-Nachweise bleiben datierte Nachweise; sie werden durch diesen Audit weder wiederholt noch auf einen neuen Extender-/Fixes-Stand übertragen. Offene Grabenarbeiter-/Ochsenfälle erhalten hier ausdrücklich kein PASS.

## Ergänzung 25.11.2026: tatsächliche Editor-Brushgeometrie

`6A620(param2!=0)` ruft `E3360` für jeden Brushordinal auf. Das ist keine width×width-Fläche: `2D1C38[width]` liefert für Breiten0..9 die Counts `0,1,5,13,25,41,85,145,313,545`. `E3360` liefert Ordinal0 als Mitte; weitere Ordinale sind neu entdeckte Kacheln einer Breitensuche. Die vier nativen Richtungen0/2/4/6 werden in dieser Reihenfolge betrachtet. Die korrekten Y-Deltas stammen aus dem tatsächlichen nativen 8-Byte-Richtungsrecord; Pseudocode-Symbolskalierungen sind hierfür kein Vertrag.

Ein bereits aus der Queue genommenes Feld expandiert nur bei `(TileFlags & 0x30)==0`. Seine Nachbarn werden dagegen schon beim ersten Entdecken gezählt, unabhängig von deren eigenen Expansionsflags. Eine blockierte Kachel gehört folglich zur Hülle, verhindert aber ihre eigene weitere Expansion. `E3360` prüft die aktive Koordinatenmaske nur für den Seed; Border-/inaktive Nachbarn dürfen nicht vorzeitig übersprungen werden, da dies spätere Ordinale verändert. Der native Scratch-/Visitedcounter wird verändert; der Hüllenplanner darf diese Funktion nicht aufrufen.

Direkte PE-Anker im unveränderten kanonischen Bild: `E3485` testet `byte ptr [tileManager + tile*4 + 898400],30`; `E34B0/E34B7` lesen/addieren den rowabhängigen Nachbaroffset; `E34CD` zählt Discovery vor dem Enqueue; `E34D2` erreicht den verlangten Ordinal; `E34DC/E34E9` speichern Nachbar-Y; `E34FB/E3506/E3511` schalten Richtung/Nachbar-/Y-Tabelle weiter; `E353D..E354A` bilden das Ergebnis-Y. Der DWORD-Stride der Tileflags ist direkt bestätigt, auch wenn die globale Decompilersymbolik anders aussieht.

Die normalen Höhen-/Surfacebrushes erhalten die Bits0x30; der reine Planner kann daher eine lokale BFS einmal berechnen, anstatt jede native Ordinalsuche nachzubauen. Bei erschöpfter Queue liefert Vanilla für fehlende Ordinale die vorher genullten Ergebnisfelder. Der Planner lehnt diesen Fall sichtbar ab, statt daraus still eine unvollständige Hülle zu machen. Er bildet rohe Borderkacheln zur Discovery ab und clippt erst die zurückgegebene Beobachtungsmenge auf die aktive Karte. Feste Arbeits- und Zellbudgets begrenzen die lokale Berechnung.

Sonderzweig `6FE90(operationFlags=40000000)` verwendet stattdessen zentrierte `69850`-Tabellenoffsets, iteriert jedoch weiterhin den Brushcount. Deshalb dürfen dessen zusätzlichen Offsets jenseits `width²` nicht still fehlen. Der Tabellenreader muss diese tatsächlichen Einträge liefern. `62AB0` bleibt absichtliches Editor-Löschen und wird vom Rettungshüllenplanner ausdrücklich abgelehnt. Ein normales Tile-/Höhenwrite ist weiterhin kein hinreichender Rettungsbeweis.
