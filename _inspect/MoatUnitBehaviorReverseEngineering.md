# Moat-Verhalten von Units: Vanilla-Reverse-Engineering und Testergebnisse

Stand: 2026-08-30

Thema: geplante und fertige Moats, Command 6 (`DigMoatTileId`), gewöhnliche Bewegung,
Cursorprüfung, Unit-Auswahl, Wegfindung, Besitzer/Allianz, Reservierung und bestehende
Bewegungsaufträge

## 1. Zweck und Status dieses Dokuments

Dieses Dokument bündelt die bislang über Quellcode, Disassemblierung, BepInEx-Logs und Ingame-Tests verteilten Erkenntnisse zum Graben von Moats. Es soll einem neuen Chat oder Entwickler ermöglichen, an diesem Feature oder an verwandten Moat-Features weiterzuarbeiten, ohne die bereits untersuchten Vanilla-Pfade erneut von Grund auf suchen zu müssen.

Wichtig ist die Trennung zwischen:

- **nachgewiesenen Fakten** aus der kanonischen DLL, validierten Hooks oder Logs;
- **aktuellen Implementierungsentscheidungen** in `MoatCommandTest/src/MoatDiggingReachabilityFix.cs`
  für Command 6 und in `MoveMoatTest/src/MoveMoatPathTest.cs` für gewöhnliche Bewegung;
- **noch offenen Fragen** und geplanten, aber noch nicht umgesetzten Änderungen.

Der aktuelle funktionale Stand von `MoatCommandTest` ist:

- Ein menschlicher Spieler kann einen grundsätzlich unerreichbaren freundlichen geplanten Moat anklicken; die Cursorprüfung wird dafür gezielt freigegeben.
- Das exakte befohlene Moat-Ziel kann bis zur Moat-State-Machine erhalten und ausgewählt werden.
- Der nachfolgende Vanilla-Tile-Path-Builder kann nach einem eng begrenzten Regions-BFS-Bypass einen echten Pfad durch fertige Moats bauen.
- Units können dadurch einen fertigen Moat überqueren und am befohlenen geplanten Moat graben.
- Ein bereits laufender normaler Bewegungsauftrag wird derzeit jedoch nicht beim Erteilen des Moat-Befehls beendet. Die Unit beendet erst den alten Auftrag und bearbeitet danach den Moat-Auftrag. Die bisherige späte Verwendung von Vanillas Pfadabbruchhelfer ist funktional wirksam, wird aber erst bei der späteren Moat-Auswahl erreicht und ist deshalb zu spät.
- Die grünen Zielmarker bleiben dadurch ebenfalls am alten Bewegungsziel beziehungsweise an Zuständen des alten Pfads. Das ist kein isolierter Renderfehler.

Die nächste fachlich abgesicherte Änderung für `MoatCommandTest` ist am Ende dieses Dokuments
beschrieben, aber zum Stand dieses Dokuments noch **nicht** umgesetzt.

Der davon getrennte funktionale Stand von `MoveMoatTest` ist:

- Gewöhnliche Move-Befehle können wiederholt einen fertigen Moat überqueren, sowohl im Map
  Editor als auch im Skirmish.
- Cursor, Tribe-Flood-Fill, Moat-Modus, Regionsprüfung und echter Tile-Builder bilden eine
  durch Logs bestätigte End-to-End-Kette.
- Mauern bleiben blockiert; der echte Builder wird nicht künstlich positiv gesetzt.
- Ein Editor-Test mit einer feindlichen Unit hat gezeigt, dass die bisherige Freigabe den
  Besitzer ignoriert: Die feindliche Unit durchquerte den fertigen Moat ebenso wie dessen
  Besitzer. Der echte Builder ist in der derzeit aktivierten Variante also keine ausreichende
  Owner-Schranke.
- Als erste abgesicherte Owner-Stufe liest der Testmod nun vor Cursorfreigabe und
  Builder-Routenvariantenwechsel Moat-ID, Owner und Allianz aus. Nur eigene und verbündete
  fertige Moats werden von der Sonderfreigabe akzeptiert; feindliche oder ungültige Daten
  fallen fail-closed auf Vanilla zurück. Diese Stufe ist implementiert, aber noch nicht
  ingame bestätigt.

Die Implementierung wurde aus `BugfixesAndQoL` in den eigenständigen Testmod `MoatCommandTest` extrahiert. Der Testmod ist ohne Settings-UI immer aktiv, verwendet die Plugin-GUID `MoatCommandTest_Serp` und deklariert mit `NetworkMode: 1`, dass er in Multiplayer-Partien bei allen Teilnehmern vorhanden sein muss. `BugfixesAndQoL` enthält weder den Moat-Featurecode noch dessen frühere Initialisierung, Setting-, XAML- oder Locale-Einträge. Der getrennte experimentelle Mod `MoveMoatTest` untersucht eine allgemeinere Moat-Passierbarkeit und soll für isolierte Diagnosen nicht gleichzeitig mit `MoatCommandTest` installiert werden.

## 2. Maßgebliche Binärdatei und Quellen

### 2.1 Kanonische Spiel-DLL

Für alle angegebenen RVAs, Bytes und Disassemblierungen wurde die aktuell installierte Spiel-DLL verwendet:

`E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`

SHA-256:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Alle festen RVAs müssen bei einem anderen Hash als inkompatibel behandelt werden. Die Workspace-Datei `x86_64/CrusaderDE.dll` ist nur eine historische Vergleichskopie und darf nicht stillschweigend als Autorität verwendet werden.

### 2.2 Weitere relevante Quellen

- `MoatCommandTest/src/MoatDiggingReachabilityFix.cs`: gegenwärtige Feature- und Diagnoselogik.
- `MoatCommandTest/src/MoatCommandTestPlugin.cs`: persistenter BepInEx-/Script-Extender-Einstieg; der native Featurecode bleibt über eine statische Referenz für die Prozesslaufzeit aktiv.
- `MoatCommandTest/MoatCommandTest.csproj`, `MoatCommandTest/info.json` und `MoatCommandTest/build.bat`: eigenständige Abhängigkeiten, Metadaten sowie Build- und Installationsweg des Testmods.
- `MoveMoatTest/src/MoveMoatPathTest.cs`: reduzierte allgemeine Move-Kette für fertige Moats,
  konservative Cursor-Suche und korrelierter Builder-Routenvariantenwechsel.
- `MoveMoatTest/src/MoveMoatTestPlugin.cs`, `MoveMoatTest/MoveMoatTest.csproj`,
  `MoveMoatTest/info.json` und `MoveMoatTest/build.bat`: persistenter Einstieg sowie Build- und
  Installationsweg des getrennten Bewegungstests.
- `shcde-script-extender/src/SHCDESE.BepInEx/Interop/GameUnit.cs`: öffentlich bekannte Unit-Felder und Offsets.
- `shcde-script-extender/src/SHCDESE.BepInEx/API/GameTribeManagerAPI.cs`: Script-Extender-Aufruf für `DigMoat`.
- `shcde-script-extender/src/SHCDESE.BepInEx/Detour/BulkTribeDetours.cs`: gemeinsamer Tribe-Command-Dispatcher und bereits existierender Detour.
- `shcde-script-extender/src/SHCDESE.BepInEx/API/GameTileManagerAPI.cs`: Tile-ID- und Tile-Manager-Zugriff.
- BepInEx-Log: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log`.
- Lokale Analysewerkzeuge unter `.tools` und `.native-analysis`, insbesondere Rizin/Cutter.

Der kanonische lokale Script-Extender-Fork ist read-only. Erkenntnisse, die eine Extender-Änderung nahelegen, müssen als Report für dessen Autor formuliert werden; sie dürfen nicht direkt dort eingebaut werden.

## 3. Begriffe und zentrale Daten

### 3.1 Command und Cursor-Modus

- Tribe-Command zum Graben: `TribeAICommand.DigMoatTileId`.
- Der native numerische Command-Wert ist **6**.
- Der menschliche Cursor besitzt einen eigenen Dig-Moat-Modus. Seine gewöhnliche Erreichbarkeitsprüfung kann einen Command bereits vor dem Tribe-Dispatcher blockieren.
- Menschliche und KI-/Low-Level-Aufrufe laufen anschließend über denselben nativen targeted tribe order dispatcher.

### 3.2 Moat-Tile und Moat-Datensatz

- Ein geplanter Moat ist am Tile über `TilePropertyFlag.PlannedMoat` erkennbar.
- In beobachteten Logs hatten geplante Moat-Tiles häufig Flags `0x0000C000`. Darin ist das Planned-Moat-Bit `0x00004000` enthalten; das weitere Bit darf nicht ohne separate Analyse semantisch umbenannt werden.
- Native Funktion zum Ermitteln der Moat-ID an einem Tile: RVA `0x69560`.
- **Moat-ID 0 ist ein ungültiger Sentinel.** Gültige Datensätze beginnen bei ID 1.
- Die Moat-Datensätze liegen relativ zum Tile-Manager:

| Wert | Offset / Größe |
|---|---:|
| Datensatzarray | `TileManager + 0x1F3EE30` |
| Anzahl Datensätze | `TileManager + 0x2038E30` |
| Datensatzgröße | `0x10` Bytes |
| Besitzerbyte | `record + 0x0C` |
| Reservierungsbyte | `record + 0x0F` |
| Vanilla-Reservierungsschritt | `+20` |

Eine gültige Prüfung lautet sinngemäß `id > 0 && id < moatCount`.

### 3.3 Besitzer und Verbündete

Für eine gezielte Ausnahme wird ein geplanter Moat nur akzeptiert, wenn sein Besitzer:

- mit `unit->r_ControllableForPlayerId` identisch ist; oder
- laut Player-Manager mit diesem Spieler verbündet ist.

Beim Cursor wird der lokale Spieler verwendet. Beim eigentlichen Command beziehungsweise pro Unit muss die Spielerzuordnung der Unit verwendet werden. Feindliche, ungültige oder nicht geplante Ziele fallen vollständig auf Vanilla zurück.

Für fertige Moats ist die Besitzerzuordnung inzwischen statisch bis in Vanillas moat-aware
Tile-Prüfung belegt: `0xF1C40` ruft für ein Tile mit Flag `0x40000000` den Moat-ID-Helfer
`0x69560` auf. Bei einer positiven ID liest `0xF1C61` das signierte Owner-Byte aus
`TileManager + 0x1F3EE30 + moatId * 0x10 + 0x0C`. Ab `0xF1C6A` werden über die Tabelle bei
RVA `0x37EDF3C` zwei Gruppenwerte verglichen. Der Gleichheitszweig bei `0xF1C72` führt zum
Return-0-Pfad `0xF2202`. Welche abstrakte Bedeutung Return 0 in jedem Aufrufer besitzt, darf
ohne erneute Callsite-Analyse nicht pauschal benannt werden.

Der Ingame-Test ist für die Gesamtwirkung eindeutiger: Mit der für allgemeine Bewegung
aktivierten Buildervariante konnten sowohl Besitzer als auch Feind denselben fertigen Moat
durchqueren. `MoveMoatTest` wiederholt deshalb nicht blind den internen bedingten Sprung,
sondern verwendet zunächst außerhalb der heißen nativen Nachbarschleife die bereits bekannte
fachliche Regel `owner == unitPlayer || IsPlayerAlliedTo(unitPlayer, owner)` als konservative
Vorschranke. `MoatCommandTest` verwendet dieselbe Regel weiterhin nur für geplante Moats.

### 3.4 Relevante `GameUnit`-Felder

Aus `Interop/GameUnit.cs`:

| Feld | Offset | Beobachtete Bedeutung |
|---|---:|---|
| `r_CurrentTilePositionX/Y` | `0x00C0/0x00C2` | aktuelles Tile |
| `r_TargetTilePositionX/Y` | `0x00C4/0x00C6` | primäres Bewegungsziel |
| `r_PreviousTilePositionX/Y` | `0x00C8/0x00CA` | vorheriges Tile |
| `r_NextTilePositionX2/Y2` | `0x00DC/0x00DE` | nächstes beziehungsweise alternativer Pfadstart |
| `r_TargetTilePositionX2/Y2` | `0x00E8/0x00EA` | sekundäres Ziel; wird auch von sichtbaren Ziel-/Folgezuständen benutzt |
| `r_PathPlanRelated1` | `0x00F0` | noch nicht vollständig benannt |
| `r_PathPlanStateBitFlags` | `0x00F2` | Pfadzustandsbits; beim Laufen häufig `2` |
| `r_MovingRelevant` | `0x00F4` | beeinflusst die Wahl des Pfadstarts |
| `p_CurrentPathPlanPosition` | `0x00F6` | aktuelle Position im Richtungs-/Pfadplan |
| `p_PathPlanSize` | `0x00F8` | Pfadplangröße |
| unbekannt | `0x028C` | in Diagnosen als `deferredShortening` protokolliert; Name nur Arbeitshypothese |
| `r_PathPlanRelated3` | `0x0290` | Pfad-Linkage beziehungsweise verwandter Pfadzustand |
| `r_AIState` | `0x02BC` | AI-State; Moat-Arbeitsfolge verwendet später State 124 |
| `r_AI_LastIssuedTribeCommand` | `0x0398` | zuletzt übernommener Tribe-Command |
| `r_ContextTargetTileX/Y` | `0x03E4/0x03E6` | gespeichertes Command-Ziel |

Die nativen Manageradressen verwenden vor dem eigentlichen `GameUnit`-Array einen Bias. Deshalb dürfen absolute Manager-Offets nicht direkt als relative `GameUnit`-Offsets gelesen werden.

## 4. End-to-End-Ablauf eines Moat-Befehls

### 4.1 Menschliche Cursorprüfung

Vanilla prüft beim Dig-Moat-Cursor die gewöhnliche Erreichbarkeit. Ist das angeklickte Tile unerreichbar, zeigt der Cursor bereits das entsprechende Verbotssymbol und der Befehl wird nicht ausgeführt.

Die aktuelle gezielte Ausnahme liegt an der aus dem Cursorpattern abgeleiteten Hookstelle RVA `0x8F3C5`:

- nur im Dig-Moat-Cursormodus;
- nur für das exakt angeklickte geplante Moat-Tile;
- nur bei eigenem oder verbündetem Besitzer;
- dann wird das Reachability-Ergebnis als erfolgreich behandelt;
- alles andere bleibt Vanilla.

Validierte 16-Byte-Spanne:

`85 C0 74 11 44 8B BC 24 C0 00 00 00 44 8D 6B 02`

Der Cursorfix ist erforderlich, löst aber allein nicht die spätere Command- oder Wegfindungsablehnung.

### 4.1.1 Gewöhnlicher Move-Cursor und frühe Ablehnung

Für die alternative allgemeine Moat-Bewegung wurde der gewöhnliche Move-Cursor gegen die kanonische
`CrusaderDE.dll` mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
weiter verfolgt. Der relevante Ablauf im großen Cursor-Update lautet:

1. `0x8F325` ruft den Auswahl-Sondermodus `0x196870` auf.
2. Bei dessen Ergebnis `0` geht Vanilla nach `0x8F365` in den normalen Unit-Pfad.
3. `EBX != 0` würde die folgenden Erreichbarkeitsprüfungen überspringen.
4. `R15D == 0` führt direkt zum Verbotsergebnis bei `0x8F3DA`.
5. Danach müssen die Flags des aktuellen Unit-Tiles die Maske `0x10000100` treffen.
6. Erst anschließend werden die Regionsvorprüfung `0xE9D90` und die eigentliche
   Cursor-Erreichbarkeit `0xE9FF0` aufgerufen.
7. `0x8F3DA` schreibt das Verbotsergebnis in die Cursor-Globals (`560=-10`, `548=0x41`,
   `550=0x10`, `54C=0xAC`, `55C=0x11`).

Ein Testlauf ohne `AssassinCombatFix` erreichte `0x196870` 77-mal, aber weder `0xE9D90`
noch `0xE9FF0`. Die Ablehnung liegt damit nachweislich vor den bislang behandelten
Regions- und Direktprüfungen. `MoveMoatTest` beobachtet deshalb testweise den geradlinigen
Fehlerblock bei `0x8F3DA`, ohne ein Ergebnis oder einen bedingten Sprung zu verändern. Dort
sind `R14` (nativer Unit-Index), `R15` (vorgeschaltetes Gate), `EBX` und der native aktuelle
Unit-Tile noch verfügbar. Das soll unterscheiden, ob das `R15`-Gate oder die Tile-Flag-Maske
den gewöhnlichen Cursor vorzeitig sperrt.

Validierte 26-Byte-Spanne bei `0x8F3DA`:

`C7 05 7C E1 01 06 F6 FF FF FF 41 BD 04 00 00 00 C7 05 54 E1 01 06 41 00 00 00`

Die Diagnose ergab anschließend für die ausgewählte Unit:

- Vanillas Zielverfügbarkeit war `1`;
- der normale Cursorpfad hatte noch keinen Erfolg gesetzt (`EBX=0`);
- `R15D` war ebenfalls gültig (`1`);
- das aktuelle Unit-Tile hatte `0x00008000`, während der nachfolgende Sonderpfad die
  Maske `0x10000100` verlangt;
- deshalb wurden `0xE9D90` und `0xE9FF0` nicht erreicht.

Ein erster allgemeiner Moat-Cursorfix setzte deshalb früher an RVA `0x8F1C4` an, unmittelbar
nach Vanillas normaler Erreichbarkeitsfunktion `0xE2610`. Er konnte den Cursor tatsächlich
grün machen. Der folgende Klick erzeugte jedoch weder `MoveHere`- noch gemeinsame Pfadlogs.
Zusätzlich wurden erreichbare Bodentiles hinter einer Mauer fälschlich grün. Der Test belegte
damit, dass eine lokale Änderung des `EAX`-Ergebnisses an dieser Callsite nur das sichtbare
Feedback ändert, nicht aber alle für die Befehlsannahme erforderlichen Prüfungen und Zustände.
Dieser Inline-Bypass ist daher wieder deaktiviert; seine Bytes bleiben nur zur Revisionskontrolle
validiert.

Validierte 14-Byte-Spanne bei `0x8F1C4`:

`85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00`

Die Logkorrelation zeigte beim Klick auf das nur durch den Moat erreichbare Ziel anschließend
`0` in der echten Regionsvorprüfung `0xE9D90`; `0xE9FF0`, `MoveHere` und die gemeinsamen
Pfadfunktionen wurden nicht erreicht. Die Freigabe muss deshalb in den von Hover und Klick
gemeinsam verwendeten nativen Prüffunktionen erfolgen. `MoveMoatTest` lässt nun zuerst jeweils
Vanilla laufen und ersetzt ein Ergebnis `0` nur dann durch `1`, wenn eine konservative
read-only Tile-Suche einen Weg vom nativen Unit-Tile zum Ziel findet, der mindestens ein
fertiges Moat-Tile (`0x40000000`) benutzt. Die Suche

- verwendet nur orthogonale Nachbarn, um kein diagonales Schneiden über Mauer-/Wasserecken
  zu simulieren;
- lässt vor dem ersten Moat nur die Startregion zu;
- lässt danach neben Moat-Tiles nur Start- und konkrete Zielregion zu;
- verlangt ein gültiges, verfügbares Ziel in einer positiven Pfadregion;
- verändert weder Tileflags noch den nativen Builderzustand und fällt bei Fehlern auf Vanilla
  zurück.

Damit sollten `0xE9D90` und anschließend `0xE9FF0` denselben effektiven Erfolg sehen. Die
damaligen Logs wiesen für beide Funktionen `vanilla`, `effective`, `completedMoatRoute` und den
Cache-Neuaufbau separat aus. Dieser Zwischenstand genügte allein noch nicht für einen
ausführbaren Auftrag; die später identifizierten Command- und Builder-Schranken sind in
Abschnitt 4.1.2 beschrieben. Interne Pathmanager-Ausgabefelder von `0xE9FF0` werden weiterhin
nicht künstlich geschrieben.

Der folgende Test zeigte, dass allein diese beiden Detours noch nicht erreichbar waren:
Bei allen getrennten Zielen sprang Vanilla weiterhin an `0x8F393` nach dem fehlgeschlagenen
`test [currentTileFlags],0x10000100` direkt in den Verbotblock. Die Logs meldeten konsistent
`reason=selected-unit-current-tile-flags`, `currentFlags=0x00008000`, `regionObserved=False`
und `directObserved=False`. Deshalb erhielt `MoveMoatTest` zunächst einen eng begrenzten Hook auf
der vollständigen 11-Byte-TEST-Instruktion bei `0x8F388`. Die verdrängte Instruktion läuft
zuerst. Nur wenn ihr Ergebnis ZF setzt und die konservative Moat-Suche für genau die ausgewählte
Unit und das aktuelle Cursorziel positiv ist, löscht der Callback ausschließlich ZF. Der
unveränderte `JE` bei `0x8F393` fällt dann in Vanillas `0xE9D90`-/`0xE9FF0`-Kette durch.
Tileflags, Register und Sprungcode werden nicht verändert; bei einer negativen Suche und bei
jedem Fehler bleibt das Vanilla-ZF bestehen.

Der erste Lauf mit diesem Hook erreichte weiterhin den Verbotblock und erzeugte keinen
`cursor-moat-grid`-Eintrag. Ursache war die Hookregistrierung nur mit
`X64SmartCPUContextRegs.All`: Statusflags müssen bei Zhuqiaomons Context-Hooks wie in den
bereits bewährten ImprovedHunters-ZF-Hooks ausdrücklich zusätzlich mit
`X64SmartCPUContextRegs.Flags` angefordert werden. Die Registrierung verwendet deshalb nun
`All | Flags`. Außerdem loggte der Callback direkt `vanillaZeroFlag`, das Ergebnis der
Moat-Suche und die Aktion `clear-zf` beziehungsweise `vanilla-je`.

Da auch der folgende Lauf keinen dieser Entscheidungslogs erzeugte, wurden für die nächsten
24 Callback-Eintritte zusätzlich bereits vor der ZF-/Armed-Schranke
`cursor-current-tile-flag-gate-entry` mit rohem `Rflags`, decodiertem ZF und
`cursorPollArmed` geschrieben. Fehlt selbst dieser Eintrag trotz nachfolgendem Verbotblock,
wird die Hookstelle nicht ausgeführt beziehungsweise später überschrieben; erscheint er,
ist die genaue früh zurückkehrende Bedingung unmittelbar sichtbar.

Validierte 11-Byte-Spanne bei `0x8F388`:

`F7 84 97 00 84 89 00 00 01 00 10`

Der anschließende Lauf bestätigte die Cursorfreigabe: Das nur durch den fertigen Moat
erreichbare Ziel blieb grün. Beim Klick bewegte sich die Unit trotzdem nicht. In diesem
Testaufbau hatten Boden, Moat und Ziel dieselbe positive Regions-ID, weshalb die grüne Farbe
teilweise sogar Vanillas regulärem Regionsergebnis entsprach. Ein grüner Cursor ist damit
kein Nachweis dafür, dass ein Bewegungsauftrag erzeugt oder konsumiert wurde.

Die weitere native Analyse des gewöhnlichen Ground-Move-Klicks ergab einen bislang getrennt
betrachteten Auftragspfad:

1. Im großen Eingabedispatcher beginnt der Ground-Move-Zweig bei `0x8F75E`. Nur
   `R13D == 2` fällt in diesen Zweig; danach werden Cursorzustand, lokaler Spieler und Ziel
   geladen.
2. Bei `0x8F7BA` wird `0x195E30` mit Spieler und Zielkoordinaten aufgerufen. Diese Funktion
   führt eigene Auswahl-, Unit-, Tile- und Modusprüfungen aus. Sie kann außerdem abhängig von
   `0x180B60` den separaten Floodfill-Helfer `0xDB650` verwenden.
3. Der Rückgabeblock dieses optionalen Floodfills bei `0x195FC5` ist keine sichere
   Inline-Hookstelle: Ein Vanilla-Sprung von `0x195F65` landet bei `0x195FD1` mitten in einer
   ausreichend großen möglichen Hookspanne. Er wird deshalb bewusst nicht gepatcht.
4. Der Eingabezweig ruft danach `0x199C30` für die eigentliche Weitergabe des Auftrags auf.
5. Der dekodierte beziehungsweise eingereihte Move-Auftrag wird an der Callsite `0x10CAB`
   durch den Wrapper `0x196100` konsumiert. Dieser ruft bei `0x19614F` den zentralen
   Tribe-MoveHere-Pfad `0x11B520` auf, der schließlich die bereits beobachteten per-Unit-
   MoveHere- und Pfadfunktionen erreicht.

Ein Diagnoseversuch beobachtete deshalb ausschließlich read-only und mit gemeinsamem
64-Einträge-Limit die Stationen `0x8F75E`, `0x195E30` und `0x196100`.
Die drei Logstufen `ground-click-branch`, `ground-command-entry` und
`ground-command-queue-consumer` unterscheiden, ob der Klick schon im UI-Dispatcher verloren
geht, zwar geprüft aber nicht weitergegeben wird oder erst nach der Queue in der eigentlichen
Tribe-/Unit-Bewegung scheitert. Alle drei Stellen sind gegen eindeutige Patterns und die
vollständigen verdrängten Originalbytes der kanonischen DLL validiert.

Dieser Diagnoseversuch wurde nach dem ersten Lauf vollständig zurückgenommen: Das Spiel
stürzte beim ersten gewöhnlichen, erreichbaren Move-Klick ab. Das Log endete noch während
eines normalen `cursor-poll`-Eintrags und enthielt keinen der neuen `ground-*`-Einträge. Am
wahrscheinlichsten wurde damit der interne Kontrollfluss-Hook bei `0x8F75E` bereits beim
Betreten seiner Trampoline beschädigt; eine formal vollständige Instruktionsspanne genügt an
einem internen Sprungziel nicht als Sicherheitsnachweis. Um keine weitere ungetestete ABI-
Annahme in denselben Reparaturlauf einzubauen, wurden auch die beiden Funktionsanfangs-
Observer bei `0x195E30` und `0x196100` zunächst entfernt. Die drei RVAs und der oben
dokumentierte Vanilla-Ablauf bleiben Analyseerkenntnisse, sind aber keine aktiven Hooks mehr.

Der nächste crashfreie Lauf erklärte anschließend das scheinbare „nur einmal pro Spielstart“:
Der erste erfolgreiche Moat-Auftrag lief von Region `1` zu Region `1`. Während der realen
Überquerung änderte sich die Regionskarte jedoch sichtbar. Das letzte Moat-Tile wechselte von
Region `1` auf `0`, das erste Bodentile auf der anderen Seite von Region `1` auf Region `2`.
Der Unit-Moat-Marker blieb dabei korrekt auf `1`. Ein weiterer normaler Auftrag innerhalb der
neuen Region `2` funktionierte; beim Hover zurück nach Region `1` sprang Vanilla wieder bei
`0x8F393` direkt in den Verbotblock. Es handelt sich damit nicht um eine verbrauchte
Moat-Freigabe, sondern um die erst nach dem ersten echten Pfad sichtbare Regionstrennung.

Im selben Lauf erschien trotz hunderter nachweislich nachfolgender Verbotblock-Aufrufe kein
einziger Entry des Hooks bei `0x8F388`. Die erste grüne Anzeige stammte folglich von der noch
gemeinsamen Region, nicht von der beabsichtigten ZF-Anpassung. Der wirkungslose 11-Byte-Hook
wurde vollständig entfernt.

Aktuell wird stattdessen ausschließlich der unmittelbar folgende Zwei-Byte-Sprung bei
`0x8F393` von `74 45` (`je 0x8F3DA`) auf `90 90` geändert. Dadurch fällt der Cursorpfad in
Vanillas echte Regionsprüfung `0xE9D90` und Direktprüfung `0xE9FF0` durch. Beide Funktionen
bleiben unverändert aufgerufen; ihre vorhandenen vollständigen Detours ersetzen ein
Vanilla-Ergebnis `0` nur bei einer konservativ nachgewiesenen Route durch mindestens einen
fertigen Moat. Ohne eine solche Route bleiben Mauer, Wasser und andere unerreichbare Ziele
abgelehnt. Der Patch validiert Originalbytes, verwendet `VirtualProtect`, stellt den
Speicherschutz wieder her, leert den Instruktionscache und wird bei einer fehlgeschlagenen
Installation zurückgerollt. Er benötigt kein Trampolin und keinen Managed-Callback im
internen Cursor-Kontrollfluss.

### 4.1.2 Erfolgreicher Gesamtversuch und reduzierte Lösungskette

Der anschließende Referenzlauf bestätigte die vollständige Kette sowohl im Map Editor als
auch in einer normalen Skirmish-Partie. Mehrere aufeinanderfolgende gewöhnliche Move-Befehle
über fertige Moats wurden angenommen und wirklich ausgeführt. Ein durch eine Mauer getrenntes
Ziel blieb dagegen bereits am Cursor gesperrt. Im erfolgreichen Log gab es keine Fehler. Die
für die Entscheidung relevanten Zähler waren:

- zentraler Moat-Modus `0x196840`: 23 erzwungene und 1 natürlicher positiver Rückgabewert;
- Bewegungs-Regionsprüfung `0xE7C40`: 20 echte Änderungen von Vanilla `0` auf die gültige
  Zielregion;
- Tribe-Flood-Fill-Mitgliedschaft `0x124740`: 8 Änderungen von Vanilla `0` auf `1`;
- konservative Cursor-Moat-Suche: 59 positive und 7 negative Entscheidungen;
- Cursor-Regionsvorprüfung `0xE9D90`: 13 echte Änderungen von Vanilla `0` auf `1`;
- direkte Cursor-Erreichbarkeit `0xE9FF0`: 31 echte Änderungen von Vanilla `0` auf `1`;
- beobachtetes Bewegungsschritt-Gate `0xDCEF2`: 100 natürliche Freigaben, aber kein einziger
  notwendiger Bypass.

Die Regionsänderung während der ersten echten Überquerung erklärt dabei den früheren
Einmal-Effekt vollständig: Vor der Überquerung lagen Start und Ziel noch in Region `1`.
Danach war der fertige Moat Region `0` und das gegenüberliegende Land Region `2`. Erst diese
korrekte dynamische Trennung machte bei weiteren Befehlen die zusätzlichen Freigaben sichtbar.

Die vermutliche Gesamtlösung besteht daher nicht aus einem globalen „Moat ist begehbar“-Bit.
Vanilla besitzt bereits einen vollständigen moat-aware Tile-Builder. Dieser wird nur durch
mehrere vorgeschaltete, voneinander getrennte Grobprüfungen nicht für gewöhnliche Bewegung
erreicht. Für die aktuelle Teststufe werden ausschließlich diese Schranken geöffnet:

1. Der gewöhnliche Cursor darf an `0x8F393` trotz des für normale Bodeneinheiten ungeeigneten
   Current-Tile-Flag-Tests zu Vanillas echten Prüfungen weiterlaufen.
2. `0xE9D90` und `0xE9FF0` werden nur dann positiv überstimmt, wenn eine read-only Suche einen
   orthogonalen Weg findet, der mindestens ein fertiges Moat-Tile benutzt. Die Suche lässt
   gewöhnliche Tiles nur in Start- und Zielregion zu und übernimmt Vanillas Zielverfügbarkeit.
   In der aktuellen Owner-Teststufe dürfen Moat-Tiles außerdem nur betreten werden, wenn ihre
   gespeicherten Besitzer gültig und mit der Unit identisch oder verbündet sind.
   Dadurch wird ein bloßes Ziel hinter einer Mauer nicht freigegeben.
3. Während eines echten `TribeIssueOrderMoveHere`-Auftrags liefert `0x196840` für eine gültige
   Unit den Moat-Modus `1`.
4. In genau diesem Auftragskontext darf `0xE7C40` bei aktivem Moat-Modus trotz getrennter
   positiver Zielregion den realen Builder erreichen.
5. Ebenfalls nur für denselben Tribe und denselben aktiven Move-Auftrag darf `0x124740` eine
   fehlgeschlagene Flood-Fill-Mitgliedschaft überbrücken.
6. Der echte Tile-Builder `0xF4930` läuft zunächst mit Vanillas ursprünglicher Routenvariante bei
   `pathManager+0x80` und dem vom Modushelfer ursprünglich gelieferten Moat-Modus. Liefert dieser
   erste Lauf einen positiven Pfad, wird er unverändert übernommen; Owner-Suche und
   Routenvariantenwechsel finden dann nicht statt. Nur bei Ergebnis `0`, ursprünglicher Variante
   `1`, beobachtetem Moduskontext und unmittelbar bestätigter freundlicher Owner-/Allianzroute
   folgt ein zweiter Builderlauf mit Variante `0` und aktiviertem Moat-Modus. Vanilla verwendet
   Variante `0` natürlich, wenn eine Unit bereits auf einem fertigen Moat steht; diese Variante
   konsumiert den moat-aware Pfadmodus auch für eine gewöhnliche Unit. Ein vorheriger
   `0→1`-Tribe-Flood-Bypass ist nach dem Patrol-Befund vom 30.08.2026 keine Voraussetzung. Liefert
   der Fallback keinen positiven Pfad oder wirft er eine Exception, wird die ursprüngliche
   Routenvariante sofort wiederhergestellt.
7. Der echte Tile-Builder bleibt damit die endgültige Schranke. Er erzeugt den realen Pfad
   durch den Moat oder lehnt Mauern, Wasser und tatsächlich unerreichbare Ziele weiterhin ab.

Ein erster Bereinigungsversuch entfernte den Planner- und Builder-Detour irrtümlich als reine
Diagnose. Danach blieb der Cursor grün, aber es entstand kein ausführbarer Pfad. Die erneute
Auswertung des erfolgreichen Referenzlaufs zeigte den Fehler eindeutig: Alle sechs einzeln
ausgewerteten Editor-Moat-Befehle hatten einen positiven Builder-Ausgang zusammen mit
`route80Override=retained`; insgesamt wurde die Variante im Referenzlauf 33-mal erfolgreich
beibehalten. Der Builder-Detour war daher funktional und nicht nur beobachtend.

Die korrigierte Reduktion behält acht vollständige Funktionsdetours: sechs für die bereits
beschriebene Cursor-/Command-Kette, den zentralen Planner ausschließlich als sicheren
per-Unit-Kontext und den Builder für die eng begrenzte Routenvariantenänderung. Entfernt
bleiben Common-Path-Observer, Unit-Tick-Tracking, Path-Preview, Cursor-Frame-Polling,
Verbotblock-Observer, Direction-Seed- und Tile-Expander-Diagnosen, alle nicht installierten
Breadcrumb-Hooks sowie der nachweislich unbenutzte Bewegungsschritt-Hook. Zusätzlich besaßen
Cursor und Bewegung in dieser Zwischenfassung getrennte Loglimits, damit häufige Hover-Aufrufe
die späteren Builderlogs nicht mehr verdrängten. Diese globalen Lebenszeitlimits wurden später
durch die unten beschriebene Moat-bezogene Befehlsfilterung ersetzt.

Der Bereinigungs-Re-Test vom 2026-08-30 bestätigte diese reduzierte Fassung erneut. Sechs
aufeinanderfolgende Builder-Aufrufe protokollierten jeweils `original=1`, `effective=0`, einen
positiven echten Pfad mit Längen `9`, `5`, `10`, `6`, `2` und `6` sowie `retained=True`.
Es gab sechs korrelierte Tribe-Flood-Fill-Bypasses, keinen Builder-Rollback und keinen
Callback-, Installations- oder Restore-Fehler. Die damals getrennten Cursor- und
Bewegungsloglimits wurden erwartungsgemäß erreicht; häufige Hover- beziehungsweise
Skirmish-Gruppenaufrufe verdrängten die sechs Builder-Nachweise dabei nicht mehr. Der spätere
Patrol-Test zeigte jedoch, dass auch getrennte globale Limits ungeeignet sind: automatische
Kartenstartbefehle konnten sämtliche Budgets noch vor dem eigentlichen Test verbrauchen.

### 4.2 Gemeinsamer Tribe-Command-Dispatcher

Der Script Extender identifiziert die Funktion als `c_game_tribe_issueorder_withtarget`. Die Signatur entspricht sinngemäß:

`(GameTribeManager*, tribeId, TribeAICommand command, targetValue1, targetValue2, a6)`

Auch `GameTribeManagerAPI.DigMoat(...)` ruft diesen Pfad über `IssueTargettedCommand(..., DigMoatTileId, x, y, 1000)` auf. Deshalb ist dieser Dispatcher sowohl für menschliche Befehle als auch für KI-Lords und andere gezielte Tribe-Aufrufe relevant.

Am Funktionseingang darf kein konkurrierender Detour installiert werden: Der Script Extender detourt diesen Dispatcher bereits. Die Moat-Lösung verwendet deshalb validierte innere Inline-Hookstellen.

### 4.3 Command-6-Vorprüfung und Ersatzziel

Relevanter Bereich: RVA `0x120E3E` bis ungefähr `0x120EC5`.

An RVA `0x120E6C` gelten nach der bisherigen Analyse:

- `R13`: Tribe-ID, nicht zuverlässig direkt die Player-ID;
- `R14`: angefordertes Ziel X;
- `R9`: angefordertes Ziel Y;
- `RDX`: repräsentative Unit-ID multipliziert mit `0x490`.

Die validierte 21-Byte-Spanne an `0x120E6C` lautet:

`45 85 C0 74 54 0F BF 8C 1A 1E 07 00 00 0F BF 84 1A 1C 07 00 00`

Vanilla führt dort `test r8d, r8d` aus. Bei `R8 == 0` springt der vorhandene `je` direkt nach `0x120EC5`. Andernfalls wird um `0x120E9D` die gewöhnliche Reachability-/Ersatzziel-Funktion RVA `0xE7F60` gerufen. Dieser Pfad kann:

- den Command ablehnen; oder
- das Originalziel durch eine erreichbare Ersatzkoordinate ersetzen.

Die aktuelle Moat-Ausnahme setzt deshalb nur bei einem validierten freundlichen geplanten Moat `R8 = 0`. Die überschriebenen Vanilla-Instruktionen und deren vorhandener Branch laufen danach normal weiter. RIP wird nicht manuell manipuliert.

Hinweis aus den jüngsten Logs: Obwohl das Feature funktional bis zur Moat-Auswahl gelangte, erschienen zuletzt keine `stage=direct-command`-Logs. Mögliche Erklärungen sind, dass der betroffene Unit-Typ bereits natürlich `R8 == 0` erhält oder dass die Diagnosebedingung an dieser Stelle nicht jeden realen Pfad korreliert. Das ist eine offene Diagnosefrage; der funktionale weitere Ablauf beweist nicht, dass dieser Callback in jedem Test ausgelöst wurde.

### 4.4 Vanillas per-Unit-Filterung und gemischte Truppen

Nach der gemeinsamen Command-Vorprüfung iteriert Vanilla ab ungefähr RVA `0x120F10` über die Units der Auswahl beziehungsweise Tribe-Gruppe. Die Funktion RVA `0x119F90` liefert dabei Unit-IDs. Danach folgen Vanilla-Prüfungen auf unter anderem Lebenszustand, Tribe und Unit-Typ sowie eine Unit-Typ-Sprungtabelle.

Die Sprungtabelle liegt um RVA `0x122194`; dazugehörige Selektorbytes liegen um `0x12219C`.

Der in den Tests beobachtete moat-fähige `unitType=24` gelangt in den Zweig bei RVA `0x120F7A`. Die konkrete Rollenbezeichnung des Typs sollte ohne gesonderte Enum-Verifikation nicht allein aus dieser Zahl abgeleitet werden.

An `0x120F7A` gelten:

- `RDX`: konkrete Unit-ID;
- `R14`: akzeptiertes Ziel X;
- `R15`: akzeptiertes Ziel Y.

Dieser Zweig schreibt später den letzten Tribe-Command und das Context-Ziel in die konkrete Unit und setzt weitere Command-Zustände.

**Folgerung für gemischte Truppen:** Es ist kein eigener Unit-Type-Filter nötig und auch nicht wünschenswert. Ein Hook in diesem bereits von Vanilla gefilterten Zweig erhält Vanillas Verhalten für gemischte Auswahlen. Nicht moat-fähige Typen gelangen nicht in den betreffenden Zweig und bleiben unverändert.

### 4.5 Moat-State-Machine und exakte Auswahl

Der spätere Moat-State-Pfad liegt um RVA `0x13F77E`. Dort wird die Funktion RVA `0x6AF60` aufgerufen, um einen geeigneten Moat auszuwählen.

Die Funktion RVA `0x69D60` ist Vanillas Suche nach einem nahen freundlichen Moat. `MoatCommandTest` detourt diese Funktion gezielt:

- besitzt die Unit Command 6;
- stimmen gespeicherte Context-Koordinaten mit einem eigenen oder verbündeten geplanten Moat überein;
- dann wird exakt dessen Moat-ID statt eines nahe gelegenen Ersatzmoats zurückgegeben;
- andernfalls wird vollständig die originale Vanilla-Funktion aufgerufen.

Vor der Rückgabe eines positiven Moat-Ergebnisses erhöht Vanilla die Reservierung um 20. Der Detour muss diese Nebenwirkung ebenfalls ausführen. Erfolgs-, Freigabe- und Fehlerpfade ziehen später entsprechend ab. Ohne das `+20` würde die Reservierungsarithmetik asymmetrisch und könnte andere Units oder spätere Moat-Aufträge beeinflussen.

### 4.6 MoveHere, Regions-BFS und tatsächlicher Pfadbau

Nach der Moat-Auswahl lädt Vanilla die Zielglobals, aktiviert den Moat-Pfadmodus und ruft um RVA `0x13F7A4` die allgemeine Bewegungsfunktion RVA `0x196280` (`MoveHere`) auf.

Relevante Globals:

| Global | RVA |
|---|---:|
| aktuelle Moat-Bewegungsziel-X-Koordinate | `0x6097BE8` |
| aktuelle Moat-Bewegungsziel-Y-Koordinate | `0x6097BEC` |
| Moat-Pfadmodus | `0x60AD6E4` |
| aktuelle Unit-ID | `0x9302C4` |
| Tile-Flags-Grid | `0x48F71B0` |
| Path-Region-Grid | `0x50EC690` |

Die Karte besitzt in diesem Kontext 320800 Tiles.

`MoveHere` wählt seinen Startpunkt abhängig von Pfadzustand und `r_MovingRelevant` entweder aus der aktuellen Position oder aus `r_NextTilePositionX2/Y2`. Eine frühere Hypothese lautete, dass stehende Units einen veralteten NextTile-Start verwenden. Die späteren Logs widerlegten das: In 24 protokollierten Attempts entsprach der tatsächlich gewählte Start der aktuellen beziehungsweise nächsten Position. `PreviousTilePosition` konnte abweichen, war aber in den geprüften Pfaden nicht der gewählte Start.

Innerhalb `0x196280` wird zunächst die Regions-/Tile-BFS RVA `0xE7C40` ausgeführt. Im Moat-Modus berücksichtigt deren Tileprüfung fertige Moats ausdrücklich über Bit 30 (`0x40000000`). Das ist die bereits vorhandene Vanilla-Grundlage dafür, dass eine im Moat-Arbeitsmodus befindliche Unit durch einen fertigen Graben laufen kann.

Der sichere Ergebnis-Hook liegt bei RVA `0x1964D6`, Länge 18:

`44 8B E0 3B C3 0F 84 A4 00 00 00 0F BF 8F B8 09 00 00`

Die bisherigen Logs zeigten bei unterschiedlichen gültigen Start- und Zielregionen durchgehend `originalBfs=0`. Der eng begrenzte Bypass setzt das effektive Ergebnis nur dann auf die Zielregion, wenn:

- ein exakt korrelierter Command-6-Attempt existiert;
- das Ziel weiterhin derselbe eigene oder verbündete geplante Moat ist;
- `pathMode == 1` gilt;
- Start- und Zielregion gültig und verschieden sind;
- die originale BFS 0 geliefert hat.

Dieser Bypass behauptet **keinen Bewegungserfolg**. Er erlaubt lediglich, dass Vanillas nachfolgender echter Tile-Path-Builder ausgeführt wird.

Historische Aggregation der protokollierten BFS-Bypasses:

| effektive Zielregion | Anzahl |
|---:|---:|
| 1 | 28 |
| 8 | 21 |
| 76 | 9 |
| 75 | 4 |
| **Summe** | **62** |

Eine zeitweise eingebaute Poststate-Diagnose scannte nach der BFS die Visited- und Distance-Grids genau einmal, nicht in der heißen Nachbarschleife. Vier protokollierte Fehlschläge zeigten:

- Queue-Lese- und Schreibposition jeweils 1;
- nur das Starttile als besucht;
- 0 besuchte fertige Moats;
- 0 besuchte Tiles der Zielregion;
- Ziel selbst nicht besucht.

Beispielhafte Frontiers:

- Attempt 8: Start `(547,291)`, Region 1; nächstes besuchbares Tile `(546,290)`, ebenfalls Region 1.
- Attempt 10: Start `(548,282)`, Region 75; nächstes Tile `(547,283)`.
- Attempt 11: nächster Frontier-Kandidat war ein geplanter Moat mit Flags `0x0000C000`, Region 75, Distanz 10.
- Attempt 14 zeigte ein analoges Muster.

Damit war belegt, dass `0xE7C40` in diesen Fällen bereits ohne echte Expansion zurückkam. Der genaue interne Grund dafür ist noch nicht bewiesen. Weil Vanillas nachfolgender Builder nach dem begrenzten Bypass einen positiven realen Pfad erzeugt, wurde kein Managed-Callback in die heiße BFS-Schleife eingebaut.

Nach der Regionsprüfung ruft `MoveHere` je nach internem Zustand den Builder RVA `0xF4930` oder RVA `0xE32B0` auf. Die korrelierten Moat-Tests verwendeten `0xF4930` und erhielten positive Pfade.

Der validierte Ergebnis-Hook liegt bei RVA `0x19667E`, Länge 18:

`45 33 C0 44 89 05 58 70 F1 05 85 C0 0F 8E A4 00 00 00`

Ein Pfad wird nur als akzeptiert korreliert, wenn:

- der Builder-Rückgabewert positiv ist;
- der Moat-Pfadmodus noch 1 ist;
- die in `R14/RBP` akzeptierten Zielkoordinaten sowohl zum Attempt als auch zu den nativen Zielglobals passen.

Der Builder-Rückgabewert wird niemals verändert oder künstlich positiv gemacht.

### 4.7 Pfadverkürzung und Arbeitsposition

Nach erfolgreichem `MoveHere` testet Vanilla bei ungefähr RVA `0x13F7A9` den Rückgabewert. Bei Erfolg wird `RBX` auf die aktuelle Unit gesetzt und bei RVA `0x13F7BC` die Funktion RVA `0x198620` aufgerufen.

Diese Funktion verkürzt den Moat-Pfad absichtlich auf einen Arbeitsstandort neben dem geplanten Moat. Eine Abweichung des endgültigen Unit-Ziels um ein Tile gegenüber dem angeklickten Moat-Tile kann deshalb korrektes Vanilla-Verhalten sein.

Danach lädt Vanilla ab ungefähr RVA `0x13F7C1` die Moat-Zielglobals, schreibt weitere Unit-Zustände und setzt später um `0x13F7FD` AI-State 124.

Die validierte 14-Byte-Spanne bei `0x13F7C1` lautet:

`0F BF 05 20 84 F5 05 48 69 CB 90 04 00 00`

Der gegenwärtige Diagnosehook dort hat in der jüngsten Testserie keine `stage=post-shortening`-Logs erzeugt. Er ist nicht Bestandteil des funktionalen Fixes und soll bei der nächsten Bereinigung entfernt werden. Das Ausbleiben beweist nicht, dass Vanilla `0x198620` nie aufruft; wahrscheinlich scheitert die Attempt-Korrelation oder der Vergleich mit den kurzlebigen nativen Zielglobals am Hookzeitpunkt.

## 5. Vanillas Pfadabbruchhelfer RVA `0x197950`

### 5.1 Disassemblierung und Signatur

Die Funktion wird mit folgender nativer Signatur verwendet:

`void ResetPathLinkage(GameUnitManager* manager, int unitId)`

Validierte 36 Bytes:

`48 63 C2 48 69 D0 90 04 00 00 33 C0 89 84 0A 52 07 00 00 66 89 84 0A 2A 09 00 00 66 89 84 0A EC 08 00 00 C3`

Sinngemäße Disassemblierung:

    movsxd rax, edx
    imul   rdx, rax, 0x490
    xor    eax, eax
    mov    dword [rdx+rcx+0x752], eax
    mov    word  [rdx+rcx+0x92A], ax
    mov    word  [rdx+rcx+0x8EC], ax
    ret

Mit dem Unit-Array-Bias entspricht der erste Dword-Schreibzugriff dem Bereich ab `GameUnit + 0xF6`: Er setzt `p_CurrentPathPlanPosition` auf 0 und die unteren zwei Bytes von `p_PathPlanSize` auf 0. Bei den beobachteten kleinen Pfadgrößen war damit die effektive Pfadgröße 0. Der Zugriff `manager + unit*0x490 + 0x8EC` entspricht `GameUnit + 0x290` und setzt `r_PathPlanRelated3` auf 0. Der Zugriff `+0x92A` entspricht nach dem abgeleiteten Bias ungefähr `GameUnit + 0x2CE`; dieses Feld ist im aktuellen öffentlichen `GameUnit`-Layout nicht benannt. Es ist **nicht** dasselbe wie das in Logs separat gelesene unbekannte Feld bei `GameUnit + 0x28C`.

Der Unit-Manager wird über `GameUnitManagerAPI.Instance.GetUnitManager().Pointer` bezogen; die Unit-ID ist das zweite Argument.

### 5.2 Nachgewiesene Vanilla-Verwendung

In der kanonischen `.text`-Section wurden 85 direkte CALL-Sites auf `0x197950` gefunden. Der Helfer ist damit ein verbreiteter Vanilla-Mechanismus zum Aufheben beziehungsweise Zurücksetzen bestehender Pfadverknüpfungen, kein Moat-spezifischer Hack.

Auch im selben großen Command-Dispatcher existiert bei RVA `0x121946` ein Aufruf für einen anderen Command-Zweig. Das stützt die Entscheidung, denselben Helfer beim Übernehmen eines neuen expliziten Moat-Befehls zu verwenden.

Der Helfer ist für stehende Units idempotent: Bereits null gesetzte Pfadfelder bleiben null.

## 6. Chronologie der Ingame-Tests und daraus abgeleitete Fakten

### 6.1 Ursprüngliches Vanilla-Problem

- Ein erreichbarer geplanter Moat konnte befohlen werden.
- Ein geplanter Moat hinter einem fertigen Moat wurde bereits durch Cursor/Command-Vorprüfungen abgelehnt.
- Units bewegten sich bei abgelehnten Befehlen nicht oder zuckten nur kurz.
- KI-Lords hatten denselben grundsätzlichen Dispatcher-/Erreichbarkeitspfad und konnten das Ziel ebenfalls nicht sinnvoll übernehmen.

### 6.2 Cursorfreigabe und frühe Direktzielversuche

Nach Freigabe des menschlichen Mausbefehls wurde der Command angenommen, aber Vanilla wählte weiterhin einen in der Nähe erreichbaren geplanten Moat statt des befohlenen unerreichbaren Ziels. Daraus folgte die exakte Zielauswahl über den `0x69D60`-Detour.

Eine wichtige Korrektur war, Moat-ID 0 wieder als Sentinel abzulehnen. Eine weitere war das Spiegeln der Vanilla-Reservierung `+20` vor der positiven Rückgabe.

### 6.3 Regions-BFS und funktionierendes Überqueren

Nach exakter Zielkorrelation erreichten die Attempts die BFS, die für unterschiedliche Regionen 0 zurückgab. Der eng begrenzte Ergebnis-Bypass ließ Vanillas echten Builder laufen. Dieser erzeugte positive Pfade und die Units konnten danach fertige Moats tatsächlich überqueren und graben.

Damit ist nachgewiesen:

- Vanillas späterer Pfadbuilder und Bewegungsprozessor können einen solchen Moat-Pfad ausführen.
- Für den eng begrenzten Command-6-Fix ist keine allgemeine Bewegungsänderung erforderlich.
  `MoveMoatTest` untersucht eine solche breitere Änderung inzwischen bewusst als getrennten
  Testmod und greift deshalb nicht in `MoatCommandTest` ein.
- Ein positives Ergebnis des eigentlichen Builders darf nicht erzwungen werden; der bestehende Bypass reicht nur bis zu dieser echten Validierung.

### 6.4 Unterschied zwischen stehenden und bereits laufenden Units

Ein entscheidendes Testbild war:

- Eine stehende Unit übernahm den sichtbaren Moat-Befehl, startete aber zunächst keine Bewegung.
- Eine Unit, die noch einen normalen Bewegungsauftrag ausführte, lief zuerst vollständig zu dessen altem Ziel. Danach lief sie durch den fertigen Moat und begann am neuen Ziel zu graben.

Dies bewies, dass der Moat-Auftrag nicht verloren war. Er war hinter dem bestehenden Pfadzustand beziehungsweise Auftrag eingereiht und wurde erst nach dessen Abschluss verarbeitet.

Die Diagnose des von `MoveHere` gewählten Startpunkts widerlegte die Vermutung, ein veraltetes NextTile oder PreviousTile sei die Ursache. Der tatsächliche Start war korrekt.

### 6.5 Sekundärziel und grüne Marker

Ein vorübergehender Fix schrieb nach positivem Path-Builder `r_TargetTilePositionX2/Y2` auf das akzeptierte Moat-Ziel. Die Logs zeigten jedoch, dass diese Felder bereits **vor** dem Schreibzugriff auf dem Moat-Ziel standen. Ingame änderte sich nichts:

- die grünen Marker blieben falsch beziehungsweise am alten Bewegungszusammenhang;
- ein vorhandener Bewegungsauftrag wurde weiter vollständig abgearbeitet.

Folgerung: Das Problem ist nicht ein einzelnes falsch gesetztes Renderfeld. Die Marker visualisieren den weiterhin aktiven alten Bewegungs-/Pfadzustand. Ein Rendererhook oder künstliches Verschieben der Marker würde nur das Symptom verdecken und könnte UI und tatsächliche Unit-Zustände auseinanderlaufen lassen.

### 6.6 Test des späten Vanilla-Pfadabbruchs

Der Helfer `0x197950` wurde testweise in `DirectCommandedMoatTarget`, unmittelbar vor Reservierung und Attempt-Registrierung, aufgerufen. Er setzte die protokollierten Pfadfelder wie erwartet zurück. Ingame gab es trotzdem keine frühere Reaktion.

Die jüngste relevante Testserie am 2026-08-29 enthielt:

- 45 `selection`-Einträge;
- 43 `path-builder-result`-Einträge;
- 32 `bfs-result`-Einträge;
- 0 `post-shortening`-Einträge.

Typische Logs zeigten bereits vor `selection` `pathPosition == pathSize`, also dass die Unit ihr altes Ziel schon erreicht hatte. Beispiel:

- Unit 136, Moat-Ziel `(425,130)`;
- vor dem Reset aktuelle, primäre und sekundäre Position etwa `(425,128)`;
- `pathPosition=10`, `pathSize=10`;
- nach `0x197950`: Position und Größe 0;
- anschließend positiver Builder-Pfad.

Bei einer laufenden Gruppe zeigten spätere Selections analog `pathPosition=pathSize=14` am weit entfernten alten Ziel, bevor der Helfer aufgerufen wurde.

**Entscheidende Ursache:** `DirectCommandedMoatTarget` wird erst später in der Moat-State-Machine erreicht. Zu diesem Zeitpunkt hat Vanilla den alten Bewegungsauftrag bereits vollendet. Der Helfer funktioniert, aber sein Aufrufzeitpunkt ist zu spät.

## 7. Aktuelle native Hook- und Funktionskarte

### 7.1 `MoatCommandTest`

| RVA | Länge | Rolle | Status |
|---:|---:|---|---|
| `0x69560` | Funktion | Moat-ID eines Tiles | verwendet |
| `0x69D60` | Funktion | nächster freundlicher Moat | gezielt detourt für exaktes Command-Ziel |
| `0x8D3C2` | Pattern | Dig-Moat-Cursormodus/globaler Bezug | verwendet |
| `0x8F3C5` | 16 | Cursor-Reachability-Ergebnis | funktionaler Hook |
| `0x120E6C` | 21 | Command-6-Vorprüfung/Direktzweig | funktionaler Hook, Diagnoseabdeckung offen |
| `0x120F7A` | 16 | bereits per Unit-Typ gefilterter Command-6-Zweig | validiert, geplanter früher Reset, noch nicht umgesetzt |
| `0x13F7C1` | 14 | Zustand nach `0x198620` | Diagnosehook ohne aktuelle Treffer; zur Entfernung vorgesehen |
| `0x196280` | Funktion | allgemeines `MoveHere` | nicht detouren; Assassin-Feature nutzt diesen Bereich bereits |
| `0x1964D6` | 18 | Ergebnis von `0xE7C40` | korrelierter BFS-Bypass/Log |
| `0x19667E` | 18 | Ergebnis des echten Builders | nur Diagnose/Korrelation, Ergebnis unverändert |
| `0x197950` | Funktion, 36 Bytes | Vanilla-Pfadabbruchhelfer | aktuell zu spät aufgerufen; früh zu verwenden |
| `0x198620` | Funktion | Verkürzung auf Moat-Arbeitsposition | Vanilla unverändert |

Weitere Pattern-RVAs im aktuellen Code:

| Symbol | RVA |
|---|---:|
| `DigMoatModePatternRva` | `0x8D3C2` |
| `CursorReachabilityPatternRva` | `0x8F3A8` |
| `GetMoatIdAtTilePatternRva` | `0x69560` |
| `FindNearestFriendlyMoatPatternRva` | `0x69D60` |

Validierte Bytes für den geplanten frühen Hook bei `0x120F7A`, Länge 16:

`48 69 C2 90 04 00 00 66 83 BC 03 18 09 00 00 69`

Instruktionen:

    0x120F7A  imul rax, rdx, 0x490
    0x120F81  cmp  word [rbx+rax+0x918], 0x69
    0x120F8A  ; Ende der 16-Byte-Spanne

Die Unit-Typ-Sprungtabelle zielt auf den Anfang `0x120F7A`. Es wurde kein XRef in das Innere der Spanne bei `0x120F81` gefunden. Vor jeder tatsächlichen Implementierung müssen Hash, Bytes, Instruktionsgrenzen und innere XRefs erneut gegen die kanonische DLL geprüft werden.

### 7.2 `MoveMoatTest`

Der reduzierte allgemeine Bewegungstest verwendet keine internen Inline-Trampoline. Sämtliche
Funktionsdetours laufen zuerst durch Vanilla; der Zwei-Byte-Patch besitzt eine exakte
Originalbyteprüfung und wird bei einer fehlgeschlagenen Installation zurückgerollt.

| RVA | Art | Rolle | Effekt |
|---:|---|---|---|
| `0x8F393` | Bytepatch `74 45` → `90 90` | früher gewöhnlicher Cursor-Sprung | lässt Vanillas echte Prüfungen erreichen |
| `0x196870` | Funktion | Auswahl-/Cursor-Vorprüfung | schaltet die abgesicherte Cursor-Auswertung erst bei belegter Auswahl scharf |
| `0xE9D90` | Funktion | Cursor-Regionsvorprüfung | `0→1` nur bei konservativem Weg durch mindestens ein fertiges Moat-Tile |
| `0xE9FF0` | Funktion | direkte Cursor-Erreichbarkeit | dieselbe konservative Freigabe; interne Ausgabefelder bleiben Vanilla |
| `0x124740` | Funktion | Tribe-Flood-Fill-Mitgliedschaft | `0→1` nur für Tribe des aktiven `MoveHere`-Auftrags |
| `0x18E1E0` | Funktion | zentraler per-Unit-Planner | stellt nur den korrekten Unit-Kontext für Modus und Builder her |
| `0x196840` | Funktion | fertiger-Moat-Modus | liefert für eine gültige Unit im aktiven Auftrag `1` |
| `0xE7C40` | Funktion | Bewegungs-Regionsprüfung | lässt bei aktivem Moat-Modus die positive Zielregion zum Builder durch |
| `0xF4930` | Funktion | echter Tile-Builder | setzt korreliert `pathManager+0x80` von `1` auf `0`; Builderergebnis bleibt echt |
| `0x69560` | Funktion | Moat-ID eines Tiles | read-only Owner-Vorprüfung für fertige Moats |
| `0xF1A80` | Funktion, nicht gehookt | moat-aware Tile-/Kandidatenprüfung | statischer Wiederauffindungsanker für Vanillas Owner-Datenfluss |

Die zehn vollständigen Patterns lösen in der kanonischen DLL jeweils genau einmal an diesen
RVAs beziehungsweise am umgebenden Cursor-Gate `0x8F388` auf. Nicht mehr vorhanden sind
Step-Gate-, Common-Path-, Verbotblock-, Frame-, Tick-, Direction-Seed- oder Tile-Expander-Hooks.

## 8. Abgesicherte nächste Korrektur

### 8.1 Nächster Schritt für `MoatCommandTest`

Die noch nicht umgesetzte nächste Änderung soll den bestehenden Bewegungsauftrag beim **Übernehmen des neuen Command 6 pro Unit** abbrechen, nicht erst bei der späteren Moat-Auswahl.

Vorgesehener Ablauf am validierten Zweig `0x120F7A`:

1. Vanilla hat die konkrete Unit bereits durch seine Command-6- und Unit-Typ-Filterung geschickt.
2. Unit-ID aus `RDX`, Ziel X/Y aus `R14/R15` lesen.
3. Unit über `GameUnitManagerAPI` auflösen und ihren Spieler über `r_ControllableForPlayerId` bestimmen.
4. Exakt prüfen, ob X/Y weiterhin einen gültigen geplanten Moat mit ID > 0 und eigenem oder verbündetem Besitzer bezeichnen.
5. Nur dann `0x197950(GameUnitManager*, unitId)` aufrufen.
6. Danach die überschriebenen Vanilla-Instruktionen normal ausführen lassen. Keine manuelle RIP-Änderung.

Warum diese Stelle sinnvoll ist:

- Sie liegt früh genug, bevor der neue Command hinter dem alten Bewegungsauftrag wartet.
- Sie wird sowohl vom menschlichen als auch vom AI-/Tribe-Command-Pfad erreicht.
- Vanilla hat gemischte Truppen bereits gefiltert; kein eigener Type-Filter ist nötig.
- Der Helfer ist Vanilla-eigen und idempotent.
- Normale Bewegung, feindliche Moats und ungültige Ziele werden nicht verändert.

Bei Umsetzung soll der derzeitige **späte** Aufruf des Helfers in `DirectCommandedMoatTarget` entfernt werden. Er soll nicht als redundanter Fallback bestehen bleiben, weil er keinen zusätzlichen Nutzen bringt und die Ursachenanalyse unnötig erschwert. Ebenfalls vorgesehen ist das Entfernen des nicht treffenden Diagnosehooks bei `0x13F7C1`, sofern kein neuer konkreter Diagnosebedarf entsteht.

Erwartetes Ingame-Verhalten danach:

- Ein neuer expliziter Moat-Befehl ersetzt einen alten normalen Bewegungsbefehl.
- Eine laufende Unit beendet höchstens den bereits begonnenen aktuellen Tileschritt, nicht den gesamten alten Weg.
- Die grünen Marker des alten Bewegungsziels verschwinden mit dem abgebrochenen alten Pfadzustand.
- Der neue Moat-Pfad wird weiterhin von Vanillas echtem Builder validiert und anschließend auf die Arbeitsposition verkürzt.

### 8.2 Aktuelle Owner-Teststufe für `MoveMoatTest`

Der erste Trennungstest und der statische Datenfluss sind abgeschlossen:

- Eine feindliche Editor-Unit konnte den fertigen Moat mit der bisherigen allgemeinen
  Freigabe wirklich durchqueren. Das Problem war nicht nur ein grüner Cursor.
- Im Kandidatenpfad `0xF1A80` wird ein fertiges Moat-Tile über Flag `0x40000000`, den
  Moat-ID-Helfer `0x69560`, Recordgröße `0x10`, Owneroffset `0x0C` und die Gruppentabelle
  `0x37EDF3C` bis zu einem bedingten Vergleich bei `0xF1C72` verfolgt.
- Ein interner Managed-Hook pro geprüftem Tile bleibt aus Crash- und Performancegründen
  bewusst vermieden.

Die nun implementierte erste Owner-Schranke arbeitet vollständig vor dem echten Builder:

1. Cursor- und Builder-Vorprüfung lösen die bewegte Unit und
   `r_ControllableForPlayerId` auf.
2. Die konservative Suche ruft für jedes erstmals erreichte fertige Moat-Tile `0x69560` auf.
3. Nur eine ID mit `id > 0 && id < moatCount` und ein gültiger Owner werden akzeptiert.
4. Passierbar ist das Tile nur bei gleichem Besitzer oder
   `GamePlayerManagerAPI.IsPlayerAlliedTo(unitPlayer, moatOwner)`.
5. Gibt es keinen solchen Weg, bleibt der Cursor bei Vanillas negativem Ergebnis und der
   funktionale `pathManager+0x80`-Override wird nicht angewandt. Der echte Vanilla-Builder
   wird dennoch normal aufgerufen; unser Mod erfindet kein negatives Builderergebnis.
6. Unbekannte Owner-, ID- oder Managerdaten sind fail-closed und deaktivieren nur die
   Sonderfreigabe.

Neue begrenzte Logs melden `player`, `friendlyTiles`, `enemyTiles`, `invalidTiles` und eine
`ownerMask`. Positive Builderfreigaben erscheinen als `stage=owner-gate ... effective=allow`;
eine verweigerte Sonderfreigabe als `effective=vanilla`. Beim Hover über einen nur durch
feindlichen Moat erreichbaren Bereich erscheinen zusätzlich
`cursor-region-owner-block` beziehungsweise `cursor-direct-owner-block`.

Der erste Re-Test dieser Stufe deckte einen reinen Kontextfehler der neuen Vorschranke auf:
Der eigene Moat wurde im Cursor korrekt als freundlich erkannt (`player=1`,
`friendlyTiles=36`, `enemyTiles=0`, `ownerMask=0x2`), aber der Builder loggte durchgehend
`target=(-1,-1)` und `effective=vanilla`. Einige gewöhnliche `MoveHere`-Pfade erreichen den
Moat-Modus und Builder also ohne den erwarteten vollständigen Scope des zentralen
Planner-Detours. Das exakte Ziel steht jedoch bereits im synchron umschließenden
`TribeIssueOrderMoveHere`-Pre-Event als `TileX/TileY`. Der Fallback-Plan übernimmt deshalb nun
diese Eventwerte. Die Ownerregel selbst wurde nicht gelockert. Nach Updates muss zusätzlich
geprüft werden, dass das Pre-/Post-Event den nativen Aufruf weiterhin synchron umschließt.

Der folgende Re-Test bestätigte danach die Builderseite vollständig: Spieler 1 durfte seinen
Moat mit echten Pfadlängen `9`, `7` und `4` überqueren; für Spieler 2 wurden beim selben
Ownerbit `0x2` jeweils `friendlyTiles=0`, `enemyTiles=36`, `effective=vanilla` und kein
`builder-route80` protokolliert. Die feindliche Unit bewegte sich nicht. Der Cursor blieb
zunächst dennoch grün, weil `vanillaResult != 0` in den detourten internen Cursorhelfern nur
deren moat-aware Zwischenergebnis bezeichnet. Vanillas ursprünglicher gewöhnlicher Cursorpfad
hätte diese Helfer ohne unseren Gate-Patch gar nicht erreicht.

Die minimale Cursorbegrenzung führt keine zweite „ohne Moat“- oder „alle Moats“-BFS ein:

1. Bei einem internen positiven Ergebnis werden zunächst nur Start- und Zielregion aus dem
   vorhandenen `pathRegionGrid` verglichen.
2. Sind beide positiven Regionen gleich, bleibt das Ergebnis sofort unverändert; die
   owner-aware Suche läuft für diesen gewöhnlichen Fall nicht.
3. Nur bei getrennten positiven Regionen wird die bereits vorhandene freundliche Moat-Suche
   ausgeführt.
4. Findet sie einen eigenen/verbündeten Weg, bleibt der Cursor positiv. Findet sie keinen
   freundlichen Weg, aber tatsächlich feindliche Moat-Tiles, wird ausschließlich das durch
   unseren Gate-Patch sichtbar gewordene interne Ergebnis auf `0` begrenzt.
5. Bei einem internen Ergebnis `0` gilt weiterhin die ursprüngliche Regel: Nur ein
   nachgewiesener freundlicher Moatweg darf es auf `1` ändern.

Damit bleibt normale positive Bewegung innerhalb derselben Region ohne zusätzliche Suche
Vanilla. Bewusste konservative Grenze: Solange der Map Editor beide Seiten eines frisch
fertiggestellten Moats noch derselben veralteten Region zuordnet, kann der Cursor vorübergehend
grün bleiben; die Builder-Owner-Schranke verhindert die feindliche Bewegung weiterhin sicher.

Der anschließende Editor-Test bestätigte genau diesen Übergang. Zunächst protokollierten eigene
und feindliche Befehle `regions=1->1`: Der eigene Builder lieferte einen echten Pfad der Länge
`10`, während die feindliche Unit mit `friendlyTiles=0`, `enemyTiles=45` und ohne
`builder-route80` stehen blieb; ihr Cursor durfte wegen der bewusst unangetasteten gleichen
Region noch grün sein. Nach Vanillas Regionsaktualisierung meldete der Cursor `regions=2->1`.
Die eigene Unit erhielt weiterhin einen freundlichen Pfad und einen echten Builderpfad der
Länge `6`; der feindliche Cursor wurde rot und erzeugte keinen nachfolgenden Move-Auftrag.

Damit ist auch die frühere Vermutung zum eigenständigen `AssassinCombatFix_Serp` widerlegt:
Dieser Mod war im bestätigenden Lauf weder installiert noch geladen. Die separat in
`BugfixesAndQoL` enthaltenen Assassin-Kletterfunktionen waren zwar aktiv, besitzen aber nicht
den untersuchten Combat-Resume-Detour des eigenständigen Testmods. Der reproduzierbare Wechsel
korrelierte stattdessen direkt mit `pathRegionGrid` von `1->1` zu `2->1`. Die vorläufigen
Hinweise, der Combat-Fix wirke beobachtbar auf andere Units, wurden deshalb aus dessen Quelle,
Runtimewarnung und Metadaten entfernt.

Positive Cursorlogs werden nun pro berechneter Reachability-Generation und Stage nur einmal
geschrieben. Owner-Block-Entscheidungen besitzen einen eigenen begrenzten Logzähler, damit
wiederholtes positives Hovering die späteren `cursor-*-owner-block`-Nachweise nicht mehr
verdrängt.

Nächster Ingame-Test:

1. Eigener fertiger Moat: Cursor grün, `owner-gate ... effective=allow`, echte Bewegung.
2. Verbündeter fertiger Moat: dasselbe Ergebnis, aber anderer Owner in `ownerMask`.
3. Feindlicher fertiger Moat: Cursor rot und keine Bewegung; Log mit `enemyTiles>0` und ohne
   beibehaltenen `builder-route80`-Override.
4. Feindlicher Moat mit normalem Umweg: normale Bewegung über den Umweg muss möglich bleiben.
5. Mauer und Wasser bleiben negative, normale freie Wege positive Kontrollen.
6. Danach dieselbe Matrix in Skirmish und mit einer Gruppe wiederholen.

## 9. Verworfene oder widerlegte Ansätze

### 9.1 Pauschales globales Passierbarkeitsbit für fertige Moats

Nicht gefunden und nicht umgesetzt. `MoveMoatTest` beweist inzwischen, dass allgemeine
Bewegung durch fertige Moats über Vanillas vorhandenen moat-aware Pfadmodus möglich ist. Dafür
müssen jedoch mehrere vorgeschaltete Cursor-, Command-, Regions- und Builderzustände
koordiniert werden; ein einzelnes globales „begehbar“-Bit ist weder nachgewiesen noch für die
funktionierende Lösung erforderlich. Der gewünschte Owner-/Allianzfilter bleibt der nächste
separate Schritt.

### 9.2 Managed Callback in der BFS-Nachbarschleife

Verworfen. Die Schleife ist ein heißer Pfad. Ein Managed-Callback pro Nachbar/Tile wäre performance- und crashriskant. Die einmalige Poststate-Auswertung nach Rückkehr war ausreichend, um die fehlende Expansion zu erkennen.

### 9.3 Positiven `MoveHere`- oder Builder-Rückgabewert erzwingen

Nie tun. Der Regions-BFS-Bypass darf nur den echten Vanilla-Builder erreichen. Dessen Rückgabewert bleibt die Sicherheits- und Gültigkeitsprüfung des tatsächlichen Tilepfads.

### 9.4 `PreviousTilePosition` pauschal überschreiben

Verworfen. Die gewählten Pfadstarts waren in den Logs korrekt; PreviousTile war nicht die nachgewiesene Ursache.

### 9.5 AI-State 124 pauschal am Ankommen hindern

Nicht erforderlich und nicht abgesichert. Die späteren Daten zeigten, dass der alte Auftrag vor der Moat-Auswahl beendet wurde; das Problem lag früher bei der Command-Übernahme.

### 9.6 Sekundärziel `r_TargetTilePositionX2/Y2` überschreiben

Widerlegt. Die Felder enthielten bereits das Moat-Ziel. Der Schreibzugriff änderte weder Marker noch Auftragsreihenfolge und wurde wieder entfernt.

### 9.7 Renderer beziehungsweise grüne Marker direkt patchen

Verworfen. Die Marker spiegeln den realen alten Bewegungszustand. Ein Renderpatch würde nur UI und Simulation entkoppeln.

### 9.8 Später Reset in `DirectCommandedMoatTarget`

Technisch wirksam, aber zu spät. Die Logs beweisen, dass der alte Pfad zu diesem Zeitpunkt bereits abgeschlossen ist.

### 9.9 Konkurrierender Detour am Dispatcher oder `MoveHere`

Vermeiden. Der Script Extender detourt den targeted tribe order dispatcher bereits; das Assassin-Feature verwendet ebenfalls relevante Bewegungs-/Dispatcherbereiche. Innere, bytevalidierte und nicht überlappende Inline-Hooks sind die sichere Integrationsform.

### 9.10 Früher crashender Diagnosehook bei `0x13F83E`

Nicht wiederherstellen. Dieser Hook überschieb real 18 Bytes, während ein externer Einsprung bei `0x13F846` in die überschriebene Spanne beziehungsweise Instruktionsgeometrie führte. Das verursachte reproduzierbar einen schnellen Crash mit `0xC000001D` (illegal instruction), noch bevor Moats getestet werden konnten.

Allgemeine Lehre: Hooklänge, vollständige Instruktionsgrenzen und **alle** XRefs in das Innere einer Hookspanne müssen vor Installation geprüft werden. Eine bytekorrekte Startadresse allein genügt nicht.

### 9.11 Moat-ID 0 oder fehlende Reservierung akzeptieren

Beides war fehlerhaft:

- ID 0 ist Sentinel und darf nie als echter Moatdatensatz behandelt werden.
- Ein direkt zurückgegebener Moat muss Vanillas Reservierung `+20` spiegeln, sonst sind spätere `-20`-Pfade asymmetrisch.

## 10. Offene Fragen

1. Warum liefert `0xE7C40` in den beobachteten getrennten Regionen bereits nach dem Starttile 0, obwohl der Moat-Modus fertige Moats über Bit 30 grundsätzlich zulässt? Der genaue frühe Exit ist nicht identifiziert.
2. Warum fehlten in der jüngsten Serie `stage=direct-command`-Logs, obwohl der weitere Command-6-Pfad funktionierte? Prüfen, ob bestimmte moat-fähige Typen bereits natürlich den Direktzweig erhalten oder ob Register-/Repräsentativunit-Korrelation zu eng ist.
3. Warum trifft die Attempt-Korrelation am Diagnosehook `0x13F7C1` nicht? Für den geplanten frühen Reset ist diese Frage nicht blockierend.
4. Warum führt Vanillas nachgewiesener Owner-/Gruppenvergleich im Kandidatenpfad nicht zu der
   gewünschten Gesamtwirkung? Für die erste sichere Modstufe ist diese interne Semantik nicht
   blockierend; vor einem späteren nativen Inline-Filter müssen Rückgabesemantik und sämtliche
   Callsite-Modi von `0xF1A80` jedoch vollständig geklärt werden.
5. Welche genaue Semantik besitzt das vom Resethelfer genullte Feld `GameUnit + ungefähr 0x2CE`? Für die Verwendung des Vanilla-Helfers ist eine Benennung nicht erforderlich, für andere Bewegungsfeatures kann sie relevant werden.
6. Welche sichtbaren Markerfelder werden nach dem frühen Pfadabbruch exakt aktualisiert? Erwartung ist, dass Vanilla sie mit dem realen Pfadwechsel selbst korrigiert; dies muss ingame bestätigt werden.

## 11. Testmatrix für weitere Änderungen

### 11.1 `MoatCommandTest`

Nach einer Änderung am frühen Reset oder an verwandten Command-6-Pfaden sollten mindestens folgende Fälle getrennt getestet werden:

1. Eine stehende moat-fähige Unit zu einem erreichbaren freundlichen geplanten Moat.
2. Dieselbe Unit stehend zu einem geplanten Moat hinter einem fertigen Moat.
3. Eine noch laufende Unit zu beiden Zielen; der alte Auftrag darf nicht vollständig beendet werden.
4. Zwei oder mehrere moat-fähige Units; jeder Attempt und jede Reservierung muss pro Unit plausibel sein.
5. Gemischte Auswahl; ausgeschlossene Unit-Typen müssen Vanilla bleiben.
6. Eigener, verbündeter und feindlicher geplanter Moat.
7. KI-Lord beziehungsweise `GameTribeManagerAPI.DigMoat`.
8. Normale Bewegungsbefehle ohne Command 6.
9. Vanilla-Kontrolle ohne installierten `MoatCommandTest`; der Testmod besitzt bewusst keine Deaktivierungsoption.
10. Zwei frische Spielstarts ohne Moat-Interaktion, um frühe Hook-/Instruktionscrashes auszuschließen.

Besonders prüfen:

- alter Pfad wird beim Command und nicht erst bei der Moat-Auswahl zurückgesetzt;
- `selection`, `bfs-result` und `path-builder-result` korrelieren dieselbe Unit und dasselbe Ziel;
- Builder-Rückgabe bleibt echt positiv;
- Unit überquert fertigen Moat und gräbt;
- Marker bleiben nicht am alten Bewegungsziel;
- Reservierungsbyte wird genau einmal um 20 erhöht und durch Vanilla später symmetrisch behandelt;
- keine fremden/ungültigen Ziele erhalten die Ausnahme.

### 11.2 `MoveMoatTest`

Nach einer Änderung an allgemeiner Bewegung oder Owner-Filterung mindestens getrennt testen:

1. Mehrere gewöhnliche Move-Befehle hin und zurück über einen eigenen fertigen Moat.
2. Dasselbe über einen verbündeten fertigen Moat.
3. Dasselbe über einen feindlichen fertigen Moat; Cursor und Builder müssen ablehnen.
4. Stehende und bereits laufende Units sowie eine Gruppe mehrerer militärischer Unit-Typen.
5. Editor und normale Skirmish-Partie jeweils ab frischem Prozessstart.
6. Erreichbarer Normalweg ohne Moat sowie ein möglicher Umweg neben dem Moat.
7. Vollständig durch Mauer oder Wasser blockiertes Ziel als Negativkontrolle.
8. Falls relevant Attack- und Patrol-Ziele, da nicht jeder Auftrag zwingend dieselbe
   `TribeIssueOrderMoveHere`-Ereigniskette verwendet.

Der aktuelle funktionale Bypass ist absichtlich auf den synchronen Umfang von
`TribeIssueOrderMoveHere` begrenzt. Ein `stage=move-command`-Eintrag beweist, dass ein getesteter
Auftrag diese zentrale Kette betreten hat. Für Attack, Patrol und autonome Folgebewegungen ist
noch nicht bestätigt, dass Vanilla sie sämtlich darüber abwickelt. Fehlt bei einem solchen Test
bereits `stage=move-command`, darf der Auftrag nicht durch einen pauschalen dauerhaften Moat-Modus
freigeschaltet werden; stattdessen ist zuerst dessen übergeordneter Vanilla-Dispatcher zu finden
und derselbe eng begrenzte Command-Scope dort wiederzuverwenden.

Der Patrol-Test vom 2026-08-30 präzisierte diese offene Frage: Patrol erzeugt seine automatischen
Hin- und Rückläufe tatsächlich wiederholt über `TribeIssueOrderMoveHere`. Fehlgeschlagene, ingame
als Zucken sichtbare Versuche erreichten `move-command` und teilweise `mode`, aber nicht die bisher
nur bei einem echten `0→1`-Bypass protokollierten Flood-Fill-, Owner- und Builderstufen. Spätere
Versuche derselben Patrol erzeugten vollständige positive Builderpfade. Patrol benötigt deshalb
voraussichtlich keinen eigenen Bewegungsbefehlspatch. Das Entfernen der zu strengen Kopplung des
Builder-Overrides an `FloodFillBypasses > 0` bestätigte anschließend, dass ein Builderpfad auch bei
`tribe-flood-observed ... vanilla=1 bypass=False` erfolgreich sein kann. Die weiterhin zuckenden
Versuche endeten jedoch bereits nach `mode` und erreichten weder Region noch Builder.

Die anschließende statische Analyse der kanonischen DLL fand nur zwei direkte Calls auf den
Tribe-Flood-Helper `0x124740`, bei `0x11B92E` und `0x11B9B1` in derselben übergeordneten
Tribe-/Unit-Verarbeitung. Danach führen zwei Calls bei `0x11C057` und `0x11C0BC` in `MoveHere`
`0x196280`. Innerhalb von `MoveHere` wird der Modus bei `0x19634D` abgefragt, der Builder aber erst
bei `0x196679` erreicht. Dazwischen liegen frühe Ausgänge für bereits erreichtes Ziel, bestimmte
Target-Tile-Flags und fehlgeschlagene Zwischenprüfungen. Deshalb protokolliert die nächste
Diagnosestufe den vollständigen `TribeIssueOrderMoveHere`-Pre/Post-Kontext einschließlich
`IsPatrolPath`, `IsNewOrder`, `MoveType` und `ReturnValue`, eine per-Command-Zählung der erreichten
Planner-/Flood-/Mode-/Region-/Builderstufen sowie beim Mode-Aufruf Position, Ziel, Regionen,
Tile-Flags und Zielverfügbarkeit. Diese Diagnose verändert keine Entscheidung.

Ein Attack-Klick auf eine feindliche Unit hinter dem Moat erzeugte dagegen nachweislich einen
gewöhnlichen `move-command` mit positivem MoveHere-Builderpfad. Der grüne Bewegungscursor war also
nicht nur eine falsche Darstellung: Vanillas Angriffsauswahl wurde vor der eigentlichen
Angriffsbewegung nicht erreicht. Ob der spätere Attack-Bewegungszustand denselben zentralen Planer
nutzt, ist damit noch nicht getestet.

Der Script Extender stellt dafür bereits das konfliktfreie Ereignis
`TribeR3EventHooks.OnTribeIssueOrderWithTarget` bereit. `MoveMoatTest` protokolliert dessen Pre- und
Post-Phase nun als `target-command` mit `AICommand`, beiden Targetwerten, `a6` und `ReturnValue`.
Bleibt dieser Stage beim Klick aus, wurde der echte Target-/Attack-Auftrag bereits vor dem nativen
Tribe-Dispatcher verworfen oder in einen gewöhnlichen Move-Auftrag umklassifiziert.

Eine erneute statische Calleranalyse derselben Referenz-DLL spricht gegen befehlsspezifische
Bewegungspatches:

- `0x196280` (`MoveHere`) besitzt 339 direkte CALL-Sites in zahlreichen Command- und AI-States.
  Diese einzeln zu erkennen oder zu patchen wäre weder wartbar noch nötig.
- `0x18E1E0` (zentraler per-Unit-Planer) besitzt nur eine direkte CALL-Site bei `0x120608` im
  großen Dispatcher `0x11E960`; seine Signatur enthält bereits Unit-ID und Ziel X/Y.
- `0xF4930` (der hier relevante echte Builder) besitzt nur zwei direkte CALL-Sites: `0x18E455`
  innerhalb des zentralen Planers und `0x196679` innerhalb von `MoveHere`.
- `0x196840` (Moat-Modus) wird ebenfalls aus beiden Planern aufgerufen; die dritte CALL-Site
  `0x69F91` gehört zu Vanillas Moat-spezifischem Pfad.

Damit existieren zwei sinnvollere gemeinsame Integrationsgrenzen. Der zentrale Planer kann
grundsätzlich allein aus seinen Argumenten durch eine read-only Owner-/Moat-Routensuche
qualifiziert werden, unabhängig vom auslösenden Befehl. Für den sehr breit verwendeten
`MoveHere`-Pfad muss ein ebenso sicherer Unit-/Ziel-Scope gewonnen werden. Ein zusätzlicher
vollständiger Detour von `MoveHere` wäre technisch naheliegend, überlappt aber mit dem eigenständig
funktionsfähigen `AssassinCombatFix` und ist deshalb keine konfliktfreie Endlösung. Eine universelle
Freigabe erst im Builder wäre noch kompakter, setzt jedoch eine belastbar korrelierte aktuelle
Unit-ID und das zugehörige Ziel an beiden CALL-Sites voraus; diese Daten dürfen nicht aus einem
unbestätigten Global geraten werden. Bis diese Korrelation bewiesen ist, bleibt der bestehende
synchrone MoveHere-Event-Scope die sichere Grenze.

Die nächste Teststufe setzt den konfliktfreien Teil dieser Alternative um: Betritt eine Bewegung
`0x18E1E0` außerhalb eines bekannten `TribeIssueOrderMoveHere`-Scopes, wird aus Unit-ID und Ziel
zuerst dieselbe konservative freundliche Moat-Route read-only geprüft. Nur bei positivem Ergebnis
entsteht ein `planner-owner-qualified`-Scope. Ausschließlich innerhalb dieses synchronen Scopes
dürfen Moat-Modus, Regionsweiterleitung und Buildervariante wie beim bewährten MoveHere-Pfad
wirken. Der Builder wiederholt die Owner-/Routenprüfung vor der Änderung von `pathManager+0x80`.
Damit werden zentrale Bewegungsplanungen unabhängig vom Command-Typ abgedeckt, ohne einen
zweiten Detour auf dem von `AssassinCombatFix` belegten `MoveHere`-Funktionsanfang zu installieren.

Dies ist noch kein globaler Builder-Bypass. `F4930`-Aufrufe aus `MoveHere`, die weder vom
Script-Extender-Event umschlossen noch vom zentralen Planer aufgerufen werden, bleiben Vanilla.
Sie pauschal freizugeben wäre erst vertretbar, wenn Unit-ID und Ziel am Builder zweifelsfrei
korreliert werden können. Die Attack-Cursor-/Befehlsauswahl liegt außerdem vor beiden Planern und
wird durch diese Erweiterung bewusst nicht umklassifiziert.

Der Lauf vom 30.08.2026 zeigte bei Patrol einen weiteren wichtigen Sonderfall: Vanillas
Tribe-Floodprüfung kann bereits `1` liefern, obwohl der anschließend gebaute Pfad den fertigen
Moat nicht überquert. Deshalb ist ein vorheriger `tribe-flood-fill`-Bypass keine Voraussetzung
mehr für die Buildervariante. Er bleibt Diagnose. Die Freigabe beruht stattdessen auf dem
synchronen Move-/Planner-Scope, erzwungenem Moat-Modus und der unmittelbar am Builder erneut
bestätigten Owner-/Allianzroute. Ohne diese letzte Routenprüfung bleibt der Builder Vanilla.

Für einen positiven Moat-Pfad müssen im reduzierten Diagnosemodus mindestens ein erzwungener
`mode`-Eintrag, bei getrennter Region `region`, `owner-gate ... effective=allow` und
`builder-route80 ... result>0 retained=True` korrelieren. `tribe-flood-observed ... vanilla=1`
ist dabei zulässig. `retained=False`, Callbackfehler oder ein positiver feindlicher Pfad sind
Fehlerbefunde.

Der anschließende Patrol-Vergleich zeigte eine zu breite Eingriffsbedingung der damaligen
Owner-first-Fassung: Auch die Kontrollstrecke ohne zu überquerenden Moat fand innerhalb derselben
verbundenen Kartenfläche eine alternative Route mit 282 freundlichen Moat-Tiles. Deshalb wechselte
der Mod dort bei 30 von 31 erfolgreichen Builderaufrufen ebenfalls vorsorglich auf Variante `0`.
Die Kontroll- und Moat-Strecken hatten zwar keinen einzigen negativen Command- oder Builderausgang,
waren dadurch aber beide modifiziert und nicht als Vanilla-Vergleich verwertbar.

Die daraus abgeleitete Vanilla-first-Fassung verwendet `0xF4930` selbst als maßgebliche
„ohne Moat erreichbar“-Prüfung. Eine zusätzliche BFS dafür existiert nicht. Die statische Analyse
der kanonischen DLL bestätigt die Wiederaufruf-Sicherheit des Fallbacks: Frühe Abbrüche vor der
eigentlichen Suche verändern keinen Suchzustand. Jeder Eintritt, der die unteren Suchroutinen
erreicht, setzt bei `0xF49D7` `pathManager+0x7C = 1`, löscht bei `0xF49DE` das Ergebnisfeld
`pathManager+0x155F68` und erhöht bei `0xF4A2B` beziehungsweise `0xF4A95` einen neuen
Generations-/Stamp-Zähler an `+0xAC` beziehungsweise `+0xA8`. Ein erster Fehlschlag wird daher
nicht fortgesetzt; der zweite Lauf beginnt entweder ohne vorherige Suchmutation oder mit frisch
initialisiertem Suchzustand. `builder-vanilla-first` protokolliert Originalvariante, ursprünglichen
Moat-Modus und Ergebnis. Nur `result=0 fallbackCandidate=True` darf anschließend zu `owner-gate`
und `builder-route80` führen.

Der erste Ingame-Test dieser Fassung bestätigte die Trennung exakt. Neun Builderläufe auf
Vanilla-erreichbaren Zielen lieferten unmittelbar positive Ergebnisse (`7×4`, `2×2`), jeweils mit
`fallbackBuilderCalls=0`. Acht nur über den eigenen fertigen Moat erreichbare Teilwege lieferten
zuerst `builder-vanilla-first result=0`; jeder davon erzeugte genau einen
`owner-gate ... effective=allow`-Fallback und anschließend einen positiven echten Pfad (`7×4`,
`1×6`). Es gab keinen positiven Vanilla-Lauf mit Fallback, keinen Nuller ohne Fallback, kein
`retained=False` und keinen Callbackfehler. Die Mauer-Negativkontrolle erzeugte erwartungsgemäß
keinen Move-Auftrag.

Die nachfolgende Diagnosebereinigung kennzeichnet bereits das Erreichen von `0xF4930`, nicht erst
eine ausgeführte Owner-BFS. Dadurch führt die Post-Phase ihre read-only Frühabbruchprüfung nur für
Aufträge aus, die wirklich vor dem Builder endeten. Ein positiver normaler Builderlauf löst weder
eine nachträgliche Owner-Suche noch einen unnötigen Logblock aus.

`planner-owner-rejected` protokolliert separat nur Moat-relevante oder belegte Ziele, die der
zentrale Planner außerhalb eines MoveHere-Scopes nicht owner-sicher qualifizieren konnte. Für
Attack ist insbesondere `targetAvailability=0 reason=target-unavailable-or-occupied` relevant:
Dann wurde der Planner zwar erreicht, die derzeitige Routensuche verlangt aber noch das belegte
Ziel statt eines zulässigen Angriffs-/Annäherungsfelds. Dieses Logging verändert die Entscheidung
nicht.

## 12. Reverse-Engineering- und Log-Arbeitsweise

### 12.1 Binärprüfung

Vor jeder nativen Änderung:

1. SHA-256 der installierten kanonischen DLL prüfen.
2. Exakte Bytes der gesamten Hookspanne prüfen.
3. Mit einem echten Disassembler vollständige Instruktionsgrenzen prüfen.
4. Direkte und indirekte XRefs auf den Beginn **und in das Innere** der Spanne prüfen.
5. Überschneidungen mit Script-Extender- und anderen Mod-Hooks prüfen.

PE-RVAs dürfen nicht als rohe Dateioffsets in ein Bytearray indexiert werden. Sections besitzen unterschiedliche Virtual- und Raw-Offets. Rizin/Cutter mit geladener PE-VA oder ein korrekter PE-Section-Mapper ist erforderlich.

Beispiel für den Hash in PowerShell:

    Get-FileHash -Algorithm SHA256 -LiteralPath 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll'

### 12.1.1 Wiederauffinden nach einem Spiel-DLL-Update

Die RVAs in diesem Dokument sind Referenzadressen für den oben genannten SHA-256, keine
dauerhaften API-Adressen. Für eine neue DLL muss zuerst eine neue Analysezuordnung erstellt
werden. Der bisherige Hash und seine RVAs bleiben dabei als historische Vergleichsspalte
erhalten; sie dürfen nicht einfach überschrieben werden.

Die maßgeblichen Suchanker stehen als vollständige Pattern-Konstanten in den beiden
Quelldateien. Für `MoveMoatTest` sind dies:

| Pattern-Konstante | alte Referenz-RVA | semantischer Anker |
|---|---:|---|
| `CursorCurrentTileFlagGatePattern` | `0x8F388` | Test der Current-Tile-Flags unmittelbar vor dem Sprung bei `+0x0B` |
| `CursorSpecialModePattern` | `0x196870` | 35 Auswahlslots ab Struktur-Offset `0x564`, Slot 22 ausgenommen |
| `CursorRegionPrecheckPattern` | `0xE9D90` | Cursor-Regionsvorprüfung mit Flood-Fill-Zähler im PathManager |
| `CursorReachabilityFunctionPattern` | `0xE9FF0` | direkte Cursorprüfung mit Unitindex und Ziel X/Y |
| `TribeFloodFillMembershipPattern` | `0x124740` | Tribe-ID mal Strukturgröße `0x688` und Flood-Fill-Stamp |
| `CentralMovementPlanPattern` | `0x18E1E0` | Unit-ID mal `0x490`, Ziel X/Y und großer per-Unit-Planer |
| `DetectCompletedMoatModePattern` | `0x196840` | Unit-ID mal `0x490`, Feld bei Manageroffset `0x72C`, Bitextraktion |
| `RegionReachabilityPattern` | `0xE7C40` | PathManager, Movement-Class, Zielregion und Start X/Y |
| `PathBuilderPattern` | `0xF4930` | zentraler Builder; liest früh PathManager-Feld `+0x0C` |
| `GetMoatIdAtTilePattern` | `0x69560` | Tile-ID → 16-Bit-Moat-ID aus TileManager-Tabelle |

Für `MoatCommandTest` sind insbesondere `DigMoatModePattern`, `CursorReachabilityPattern`,
`GetMoatIdAtTilePattern` und `FindNearestFriendlyMoatPattern` die Einstiegspunkte. Die Namen
der zugehörigen `Validate...HookSpan`-Methoden führen zu den vollständig erwarteten Bytes der
inneren Hookspannen. Eine neue DLL gilt erst als unterstützt, wenn jedes benötigte Pattern
eindeutig ist und die gesamte verwendete Instruktionsspanne separat validiert wurde.

Empfohlene Wiederauffindungsreihenfolge:

1. Neue kanonische DLL hashen und PE-Sections korrekt laden. Alte und neue DLL parallel im
   Disassembler öffnen.
2. Die Pattern-Konstanten gegen die neue `.text`-Section suchen. Ein eindeutiger Treffer ist
   nur ein Kandidat, noch keine Patchfreigabe.
3. Funktionsanfang, vollständige Instruktionsgrenzen, Calling Convention, alle direkten und
   indirekten XRefs sowie mögliche bestehende Extender-Detours erneut prüfen.
4. Die Cursor-Kette semantisch verfolgen:
   Cursor-Update → Current-Tile-Gate → Auswahlprüfung → Regionsvorprüfung → Direktprüfung.
   Die konkrete Sprungrichtung und die Bedeutung positiver/negativer Rückgaben neu bestätigen.
5. Die Bewegungskette verfolgen:
   `TribeIssueOrderMoveHere` → zentraler Unit-Planer → Moat-Modus → Regionsprüfung →
   Tribe-Flood-Fill → echter Builder. Das Feld `pathManager+0x80` anhand seines Datenflusses
   und Vanillas natürlichem Moat-Fall neu identifizieren; den Offset nicht ungeprüft übernehmen.
6. Den Owner-Pfad anhand seiner Semantik neu finden:
   Test auf fertiges-Moat-Flag → Moat-ID-Aufruf → positive-ID-Prüfung → Recordadressierung →
   Owner-Byte → zwei Player-/Gruppen-Lookups → bedingter Returnzweig. In der Referenz-DLL
   liegt dieser Block bei `0xF1C32` bis `0xF1C7E` innerhalb der Funktion `0xF1A80`.
7. Alle hart adressierten Globals erneut aus den RIP-relativen Referenzen ihrer Funktionen
   ableiten. Dazu gehören Tileflags, Zielverfügbarkeit, Cursorziel, Regionsgrid,
   Moat-Pfadmodus und Unit-Manager. Die heutigen RVAs `0x48F71B0`, `0x3A11EA4`,
   `0x3A11E2C/0x3A11E30`, `0x50EC690`, `0x60AD6E4` und `0x67E8400` sind nur
   Plausibilitätswerte für die Referenz-DLL.
8. Moat-Recordarray, Count, Recordgröße und Owneroffset durch den neu gefundenen
   Moat-Kandidatenblock und mindestens eine zweite Vanilla-Verwendung querprüfen. Erst danach
   die Werte `+0x1F3EE30`, `+0x2038E30`, `0x10` und `+0x0C` übernehmen oder aktualisieren.
9. Den Mod zunächst mit neuem Hash, aber weiterhin fail-closed bauen. Installation erst nach
   eindeutigen Patterns, exakten Bytes und einem dokumentierten Disassemblyvergleich öffnen.
10. Ingame zuerst freie Fläche, Mauer und Wasser prüfen, danach eigener, verbündeter und
    feindlicher Moat. Cursorannahme und echter Builderausgang immer getrennt auswerten.

Hilfreiche unveränderliche Beziehungen, wenn Compilerupdates die Bytes stärker verändern:

- `0x69560` ist eine sehr kleine Leaf-Funktion: sign-extend Tile-ID, 16-Bit-Lesezugriff aus
  einer TileManager-Tabelle, Return.
- Der Moat-Ownerzugriff skaliert die positive Moat-ID effektiv mit 16 und liest Byte `+0x0C`.
- Der zentrale Planer skaliert die Unit-ID mit `0x490`; dieser Strukturstride ist ein starker
  Querverweis zu `GameUnit` und zum Unit-Manager.
- Die funktionale Buildervariante lässt sich daran erkennen, dass Vanilla sie bei einer Unit,
  die bereits auf einem fertigen Moat steht, natürlich auswählt.
- Der Cursorpatch ist nur dann noch derselbe logische Ort, wenn sein Fallthrough weiterhin
  genau die zwei echten Reachability-Funktionen erreicht.

### 12.1.2 Prüfung nach einem Script-Extender-Update

Ein unveränderter `CrusaderDE.dll`-Hash genügt nicht, wenn sich der Script Extender geändert
hat. Vor einem Rebuild sind mindestens folgende Verträge zu prüfen:

- `TribeR3EventHooks.OnTribeIssueOrderMoveHere` liefert weiterhin Pre/Post mit stabiler
  `TribeId` und umschließt die per-Unit-Planung synchron;
- `MapLoaderR3EventHooks` und `EventHookPhase` sind binär und semantisch kompatibel;
- `GameUnitManagerAPI.TryGetUnitById`, `GameTileManagerAPI.GetTileManager`, `GetTileId`,
  `GamePlayerManagerAPI.IsPlayerIdValid` und `IsPlayerAlliedTo` besitzen weiterhin dieselbe
  Bedeutung;
- `GameUnit` behält insbesondere `r_ControllableForPlayerId` und
  `r_CurrentTilePositionX/Y` an den erwarteten nativen Offsets;
- Zhuqiaomons `NativePatternResolver`-/Detour-Verhalten und MonoMods Trampolinerzeugung sind
  unverändert;
- der Extender installiert keinen neuen überlappenden Detour oder Bytepatch an den hier
  verwendeten Funktionen.

Die lokalen Extender-Quellen und die tatsächlich zum Build verwendete `SHCDESE.dll` müssen
dabei zusammenpassen. Bei Abweichungen die Assembly mit `ilspycmd` prüfen und die nativen
Extender-Hooktabellen beziehungsweise `BulkTribeDetours` vergleichen. Erst nach erfolgreichem
Compile und einem Startup-Log mit allen erwarteten aufgelösten RVAs folgt der Ingame-Test.

### 12.2 Logauswertung

Das BepInEx-Log wird angehängt. Jeder neue Spielstart beginnt mit:

`[Message:   BepInEx] BepInEx 5.4.23.2 - Stronghold Crusader Definitive Edition`

Die Uhrzeit dieser BepInEx-Zeile ist nicht zuverlässig. Für die zeitliche Zuordnung die
eigenen Modlogs mit Millisekunden verwenden. `MoatCommandTest` verwendet die Präfixfolge
`Moat Command Test MoatCommand`; relevante Einträge lassen sich zusätzlich über `stage=`
beziehungsweise die konkreten Stages filtern:

- `direct-command`
- `selection`
- `bfs-result`
- `path-builder-result`
- historisch `post-shortening`

`MoveMoatTest` verwendet `Move Moat Test` beziehungsweise `MoveMoat` und aktuell:

- `cursor-region`
- `cursor-direct`
- `move-command`
- `move-command-result`
- `mode-context`
- `target-command`
- `planner-owner-qualified`
- `planner-owner-rejected`
- `tribe-flood-observed`
- `tribe-flood-fill`
- `mode`
- `region`
- `owner-gate`
- `cursor-region-owner-block`
- `cursor-direct-owner-block`
- `builder-vanilla-first`
- `builder-route80`
- `builder-gate`

Es gibt keine globalen Diagnosegrenzen mehr. Während eines synchronen `move-command` sammelt der
Mod zunächst dessen vollständige Command-, Flood-, Mode-, Region-, Owner- und Builderdiagnose im
zugehörigen `MoveCommandScope`. Erst wenn die konservative Owner-Routenprüfung tatsächlich
mindestens ein freundliches oder feindliches Moat-Tile beobachtet, wird der gesamte gepufferte
Befehl nach dessen Post-Phase ausgegeben. Befehle ohne jeden Moat-Befund werden verworfen. Dadurch
bleiben wiederholte Patrol-Hin- und Rückläufe vollständig sichtbar, während die zahlreichen
internen `MoveHere`-Aufträge beim Kartenstart das Log nicht füllen. Cursorentscheidungen werden
weiterhin anhand der bereits vorhandenen BFS-Generation dedupliziert; sie besitzen ebenfalls kein
abschaltendes Lebenszeitlimit.

Falls ein Befehl wie ein zuckender Patrol-Teilweg bereits nach der Moduswahl und vor dem Builder
endet, führt seine Post-Phase genau eine zusätzliche read-only Owner-Routenprüfung für den letzten
korrelierten Unit-Plan aus. Sie dient ausschließlich dazu, den gepufferten Befehl als Moat-relevant
zu erkennen; ihr Ergebnis ändert weder den Rückgabewert noch irgendeinen nativen Pfadzustand.

Für die Vanilla-Negativkontrolle ohne Moat wird ein Tribe ab dem ersten
`move-command ... patrol=1` gezielt als Patrol-Diagnosesitzung verfolgt. Nur für diesen Tribe
werden danach auch Moat-freie automatische `MoveHere`-Teilwege ausgegeben. Ein neuer expliziter
Nicht-Patrol-Befehl desselben Tribes oder ein Kartenwechsel beendet die Verfolgung. Der vorhandene
Script-Extender-Hook `OnTribeGetNextPatrolWaypoint` liefert zusätzlich `patrol-waypoint` mit
Tribe-ID und Wegpunktindex. Damit lassen sich Kontroll- und Moat-Test anhand derselben Command-,
Mode-, Region- und Builderstufen vergleichen, ohne allgemeine KI- oder Kartenstartbewegungen zu
protokollieren.

Ein fehlender späterer Logeintrag darf nicht sofort als fehlender nativer Funktionsaufruf interpretiert werden. Zuerst prüfen, ob Attempt-ID, globale Zielwerte oder Current-Unit-Korrelation den Callback herausfiltern.

## 13. Appendix: direkte CALL-Sites auf `0x197950`

Für die kanonische DLL wurden folgende 85 direkten Aufrufstellen gefunden:

`0xCD2A2`, `0xCD499`, `0x1150A3`, `0x11EB13`, `0x11EC5E`, `0x11ECEE`, `0x11F2E8`, `0x11F390`, `0x11F9BB`, `0x11FBFB`, `0x11FE5C`, `0x1204FE`, `0x121946`, `0x123E29`, `0x12BBDE`, `0x12D207`, `0x12E3D6`, `0x1313FD`, `0x131980`, `0x133284`, `0x133DBE`, `0x134EF0`, `0x135E0A`, `0x136D0D`, `0x137793`, `0x13881E`, `0x13992B`, `0x13AAC2`, `0x13BDA5`, `0x13CEFD`, `0x13E080`, `0x13F52D`, `0x14B75B`, `0x14DA97`, `0x14E4B3`, `0x14EC90`, `0x14ECEA`, `0x14F397`, `0x1501A2`, `0x1505A7`, `0x151067`, `0x155C35`, `0x1560FF`, `0x156F92`, `0x1575B6`, `0x158519`, `0x158BC1`, `0x158D9A`, `0x15917B`, `0x159C0D`, `0x15A64B`, `0x15A942`, `0x15B17F`, `0x15B1CD`, `0x15B2FB`, `0x15B64F`, `0x15D956`, `0x15D9A4`, `0x15DF69`, `0x15E542`, `0x15E58B`, `0x15ECAC`, `0x1603E6`, `0x1634CE`, `0x16364E`, `0x163B95`, `0x163EB4`, `0x163EFC`, `0x163FA4`, `0x1640F0`, `0x164336`, `0x166205`, `0x166959`, `0x166F9F`, `0x167368`, `0x1679E1`, `0x183F02`, `0x183F93`, `0x185D64`, `0x185DC4`, `0x185F00`, `0x185F11`, `0x193EDB`, `0x194965`, `0x19777E`.

Diese Liste belegt die breite Verwendung des Helpers, ersetzt aber bei einer neuen Hookentscheidung nicht die Analyse des jeweiligen Callsite-Kontexts.

## 14. Kurzfassung für die Weiterarbeit

### 14.1 `MoatCommandTest`

- Nicht bei Cursor, Renderer oder allgemeiner Moat-Passierbarkeit neu anfangen: Diese Schichten sind bereits getrennt untersucht.
- Der Command erreicht inzwischen das exakte freundliche geplante Moat-Ziel.
- Der begrenzte BFS-Bypass ist nötig, um Vanillas echten und erfolgreichen Builder zu erreichen; dessen Ergebnis bleibt unverändert.
- Die aktuelle Restursache liegt im Zeitpunkt des Pfadabbruchs: `0x197950` wird in der späteren Moat-Auswahl erst nach Abschluss des alten Auftrags erreicht.
- Der nächste sichere Ansatz ist der bereits Vanilla-gefilterte per-Unit-Zweig `0x120F7A`, mit Unit-ID `RDX` und Ziel `R14/R15`.
- Dort nur nach erneuter freundlicher Planned-Moat-Prüfung den Vanilla-Helfer `0x197950` aufrufen.
- Den späten Helper-Aufruf und den erfolglosen Post-Shortening-Diagnosehook anschließend entfernen.
- Gemischte Truppen weiter vollständig Vanillas Sprungtabelle überlassen.

### 14.2 `MoveMoatTest`

- Allgemeine wiederholte Bewegung durch fertige Moats funktioniert im Editor und Skirmish.
- Der Cursor benötigt den Fallthrough bei `0x8F393` und zwei konservativ gefilterte echte
  Reachability-Funktionen.
- Der Auftrag benötigt Flood-Fill-, Modus- und Regionsfreigabe; der Builder verwendet
  `pathManager+0x80 = 0` erst als owner-geprüften Fallback, nachdem Vanillas erster Lauf mit der
  ursprünglichen Variante tatsächlich `0` geliefert hat.
- Der echte Builder-Rückgabewert wird nie erzwungen; Mauerziele blieben im Test blockiert.
- Der bereinigte Re-Test lieferte sechs positive, beibehaltene Builderpfade ohne Fehler.
- Der Feindtest zeigte, dass die bisherige Gesamtkette Owner nicht wirksam blockiert; der
  statische Pfad zu Moat-ID, Owner und Gruppenvergleich ist nun dokumentiert.
- Die erste owner-aware Teststufe filtert Cursor und funktionalen Builder-Override
  konservativ auf eigene/verbündete Moats und protokolliert Owner-Maske sowie
  friendly/enemy/invalid Tiles.
- Als Nächstes diese Stufe mit eigenem, verbündetem und feindlichem Moat sowie Umweg-, Mauer-
  und Wasser-Kontrollen im Editor testen; danach Skirmish und Gruppenbewegung.
- Nach einem DLL-Update die semantische Wiederauffindungsanleitung in Abschnitt 12.1.1 nutzen;
  alte RVAs niemals allein aufgrund eines ähnlichen Hash-/Versionsumfelds übernehmen.

