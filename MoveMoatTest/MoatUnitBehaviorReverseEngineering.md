# Moat-Verhalten von Units: Vanilla-Reverse-Engineering und Testergebnisse

Stand: 2026-09-01

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
- Die inzwischen in Editor und Skirmish mit mehreren Units bestätigte Owner-Stufe liest vor Cursorfreigabe und
  Builder-Routenvariantenwechsel Moat-ID, Owner und Allianz aus. Nur eigene und verbündete
  fertige Moats werden von der Sonderfreigabe akzeptiert; feindliche oder ungültige Daten
  fallen fail-closed auf Vanilla zurück. Eigener Moat funktioniert wiederholt; eine feindliche
  Unit wird nach der Editor-Regionsaktualisierung korrekt abgelehnt. Verbündete Moats bleiben
  mangels einfachem Testaufbau noch praktisch zu bestätigen, verwenden aber denselben
  `IsPlayerAlliedTo`-Zweig.

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

Der reduzierte allgemeine Bewegungstest verwendet keine internen Inline-Trampoline und keine
Bytepatches im Cursor-Dispatcher. Sämtliche Funktionsdetours laufen zuerst durch Vanilla. Die
früher testweise veränderten Sprünge werden weiterhin durch Pattern und Originalbytes validiert,
bleiben zur Laufzeit aber unverändert.

| RVA | Art | Rolle | Effekt |
|---:|---|---|---|
| `0x8F393` | unveränderter Suchanker `74 45` | allgemeiner Tile-/Mauerzweig | bleibt Vanilla; seine frühere globale Öffnung verursachte die Kletterregression |
| `0x196870` | Funktion | Auswahlarten-/Cursor-Gate | Vanilla-first; hebt nur ein konkretes owner-geprüftes notwendiges Moat-Ziel von `0` auf `1` |
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

### 11.2.1 Bestätigte Attack-Cursor-Kette und aktueller Testkandidat

Die hashgleiche Ghidra-Baseline unter `_inspect/CrusaderDE-Native-Baseline` präzisiert die zuvor
nur aus Laufzeitbeobachtungen abgeleitete Blockade:

- `0x196840` ist kein abstrakter Commandmodus. Die Funktion skaliert die Unit-ID mit `0x490`,
  liest deren Current-Tile-ID bei Manageroffset `0x72C` und gibt ausschließlich Bit 30 der
  Tileflags zurück. Semantisch lautet die Frage daher: **Steht diese Unit gerade auf einem
  fertigen Moat?** Der bestehende Detour simuliert gezielt diesen natürlichen Vanilla-Zustand.
- `0x196870` ist keine Reachability-Funktion. Sie prüft die belegten Auswahlarten-Slots ab
  `UnitManager+0x564`, mit Sonderbehandlung für Slot 22.
- Der Cursor-Dispatcher ab `0x8C5F0` überspringt nach einem Nullergebnis von `0x196870` an den
  vier Sprüngen `0x8D72B`, `0x8E2C6`, `0x8E557` und `0x8F32F` die genauere Tilepaarprüfung.
  Die Sprünge sind jeweils vollständige Zwei-Byte-Instruktionen; die Baseline enthält keine
  eingehenden XRefs auf ihre inneren Patchadressen.
- Die Tilepaarprüfung `0xE2CA0(pathManager, targetTile, selectedUnitTile, 1)` vergleicht Start-
  und Zielregion. Sind sie getrennt, ruft sie den echten BFS-Helfer `0xD9C40` auf. Dieser BFS
  behandelt das fertige-Moat-Bit weiterhin als Blockade und konsumiert keinen Moat-Modus.
- Ein Nullergebnis entsteht damit vor dem Target-Command-Dispatcher. Das Hoverziel wird zu Move
  oder „verboten“ umklassifiziert. Erst danach würden `AttackUnit = 4`, `AttackBuilding = 9`
  beziehungsweise in bestimmten Modi `ForceAttackBuilding = 36` ausgegeben.
- Vanillas repräsentative ausgewählte Unit bestimmt der Cursor über `0x18D460(unitManager, 1)`.
  Dieser Helper bleibt unverändert und wird nur gelesen, damit Starttile, Owner und Zielpaar des
  unmittelbar folgenden `0xE2CA0`-Aufrufs exakt gebunden werden können.

Der vierte Sprung `0x8F32F` und der spätere Sprung `0x8F393` gehören jedoch nicht zu einem reinen Entity-Angriffszweig, sondern zum
allgemeinen Tile-/Mauerpfad. Seine testweise Öffnung erzeugte eine bestätigte Regression: normale
nicht kletterfähige Units erhielten einen falschen Mauercursor, während Assassinen nicht mehr
korrekt kletterten. Deshalb bleiben jetzt alle vier zuvor geöffneten Sprünge `0x8F393`,
`0x8D72B`, `0x8E2C6` und `0x8E557` bytegenau Vanilla. Ihre Patterns und Originalbytes dienen nur
als Update-Suchanker. Stattdessen läuft `0x196870` zuerst durch Vanilla. Ein positives Ergebnis
bleibt unverändert; nur ein Nullergebnis darf nach vollständiger Prüfung der repräsentativen Unit,
Kartenepoch, Zielidentität und notwendigen owner-sicheren Moat-Route effektiv `1` werden.
`0xE2CA0` läuft ebenfalls immer zuerst unverändert. Nur wenn Vanilla `0` liefert und Starttile,
Zieltile, Cachemodus, Kartenepoch und repräsentative Unit exakt zum direkt vorher erfassten Kontext
passen, wird derselbe bereits vorqualifizierte Fallback erneut geprüft. Für Units werden die acht
freien Nachbartiles geprüft. Für Gebäude werden StructureGrid-ID, Alive-State, Global-ID, Owner,
Typ und der reale belegte Footprint gebunden; Kandidaten sind alle freien Außenfelder dieses
Footprints. Das tatsächliche `0xE2CA0`-Zieltile muss weiterhin zum gespeicherten Gebäude gehören.
Reine Holz-/Stein-/Zinnenmauern, Treppen und ehemalige Mauerstrukturen sind ausdrücklich kein
Building-Scope. Ein Annäherungstile wird ausschließlich positiv, wenn es mit Moat erreichbar und
ohne Moat nicht erreichbar ist und der gefundene Zustandsweg mindestens ein eigenes oder
verbündetes fertiges Moat-Tile benutzt. Eine zusätzlich abgeleitete Trennung der Regionsnummern
ist **keine** Freigabebedingung: Der gültige Lauf zeigte sowohl gleich nummerierte konkrete
Regionen als auch Vanillas `targetRegion=0`-Sentinel innerhalb von `UnitFlood`, obwohl dieselbe
owner-geprüfte Tile-BFS die notwendige Moat-Route eindeutig mit
`attackWithMoat=true/attackWithoutMoat=false` belegte. `attackRegionTopology` bleibt deshalb nur
als Diagnosewert erhalten. Feindliche Moats, Mauern, Wasser, ungültige Daten, freie
Vanilla-Routen, irrelevante Moat-Umwege und fremde Aufrufkontexte bleiben durch dieselbe
Tile-Traversierung ausgeschlossen.

Beim funktionalen `0xE2610`-Fallback muss die aufgelöste Quellregion weiterhin exakt zum nativen
`sourceRegion` passen. Eine positive native Zielregion wird ebenfalls exakt gebunden. Nur der
Wert `targetRegion=0` wird als bestätigter Annäherungssuch-Sentinel akzeptiert; in diesem Fall
entscheidet die positive Region des konkret gefundenen freien Annäherungstiles. Tribe, Unit,
Spieler, Bewegungsklasse, Command und feindliches Ziel bleiben unverändert exakt gebunden.

Diese Stufe soll zunächst nur beweisen, ob Cursor und Target-Command dadurch korrekt werden. Der
spätere Attack-AI-Pfad wird nicht vorauseilend verändert. Nach einem erfolgreichen
`AttackUnit`-, `AttackBuilding`- oder `ForceAttackBuilding`-Target-Command werden ausschließlich
die Units erfasst, deren Tribe-ID, gespeicherter Command und nativer Unit-/Building-Zielkontext
exakt zu den Eventparametern passen. `GameTimeManagerAPI.OnTick` liest diese Units danach nur bei
Zustandsänderungen: AI-State, Positionen, Ziel- und Next-Tiles, Attack-Move-Ziel, Kontextziele,
Geschwindigkeit und bekannte Pfadstatusfelder. Zusätzlich markieren die bereits vorhandenen
Hooks lesend, ob Moat-Standhelper, zentraler Planer oder Builder für genau diese Unit erreicht
wurden. Ein neuer Command, ein anderes Ziel, Tod, Kartenwechsel oder Commandende beendet den
Tracker semantisch; es gibt kein Logbudget und keine Attack-Bewegungserzwingung.

Erreicht eine Unit mit
`AttackUnit`, `AttackBuilding` oder `ForceAttackBuilding` den Moat-Standhelper außerhalb eines
bekannten Move-/Planner-Scopes, protokolliert `attack-mode-unscoped` dedupliziert AI-State,
Position, Attack-Move-Ziel, Unit-/Building-/Tilekontext und Vanillas echtes Bit-30-Ergebnis. Ein
anschließendes `planner-owner-qualified` würde belegen, dass der bestehende gemeinsame Planer-
und Builderfallback genügt. Bleibt ausschließlich `attack-mode-unscoped`, kann im nächsten
Schritt daraus ein eng begrenzter Attack-Bewegungsscope abgeleitet werden; ein pauschaler
`MoveHere`-Detour oder global positiver Builder ist weiterhin nicht gerechtfertigt.

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
| `CursorCurrentTileFlagGatePattern` | `0x8F388`, Sprung `0x8F393` | allgemeiner Tile-/Mauerzweig; Originalbytes validieren, niemals global öffnen |
| `CursorTilePairFallbackSelectionPattern` | `0x196870` | Auswahlarten-Gate: 35 Slots ab Struktur-Offset `0x564`, Slot 22 ausgenommen |
| historischer Wall-Stager | `0x199B70` | Stager für `AttackWallTileId=0x17` und `AttachLadderToWall=0x18`; der beobachtete Assassin-Kletterklick läuft nicht primär hierüber |
| `CursorMoveCommandStagerPattern` | `0x195E30` | allgemeiner Cursor-`MoveHere`-Stager; vier bestätigte Calls bei `0x8F7BA`, `0x8FD3C`, `0x8FDC6` und `0x8FE54` |
| `GetRepresentativeSelectedUnitPattern` | `0x18D460` | vom Cursor verwendete repräsentative ausgewählte Unit ab Startindex 1 |
| `CursorTilePairReachabilityPattern` | `0xE2CA0` | Start-/Zieltilevergleich; bei getrennten Regionen Call auf BFS `0xD9C40` |
| `AttackUnitPairGatePattern` | `0x8D71D`, Sprung `0x8D72B` | Call auf `0x196870`, danach Tilepaarprüfung für Unit-Angriffszweig |
| `AttackBuildingPairGatePattern` | `0x8E2B5`, Sprung `0x8E2C6` | Auswahlgate vor erhaltenem Vanilla-Typ-Switch und Tilepaarprüfung |
| `AttackAlternativePairGatePattern` | `0x8E549`, Sprung `0x8E557` | weiterer Attack-Cursorzweig vor Tilepaarprüfung |
| historischer Disassembly-Anker | `0x8F322`, Sprung `0x8F32F` | frühes allgemeines Tile-/Mauergate; vollständig Vanilla lassen |
| `CursorRegionPrecheckPattern` | `0xE9D90` | Cursor-Regionsvorprüfung mit Flood-Fill-Zähler im PathManager |
| `CursorReachabilityFunctionPattern` | `0xE9FF0` | direkte Cursorprüfung mit Unitindex und Ziel X/Y |
| `TribeFloodFillMembershipPattern` | `0x124740` | Tribe-ID mal Strukturgröße `0x688` und Flood-Fill-Stamp |
| `CentralMovementPlanPattern` | `0x18E1E0` | Unit-ID mal `0x490`, Ziel X/Y und großer per-Unit-Planer |
| `UnitStandingOnCompletedMoatPattern` | `0x196840` | Unit-ID mal `0x490`, Current-Tile-ID bei Manageroffset `0x72C`, Tileflag-Bit 30 |
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
- `TribeR3EventHooks.OnTribeIssueOrderWithTarget` liefert weiterhin Pre/Post mit stabilen
  Command-, Tribe- und Zielwerten und setzt die Unit-Kontextfelder vor der positiven Post-Phase;
- `GameTimeManagerAPI.OnTick` bleibt ein persistenter Simulationstick und führt reine Leser auf
  `GameUnit` in einem gültigen Kartenkontext aus;
- `MapLoaderR3EventHooks` und `EventHookPhase` sind binär und semantisch kompatibel;
- `GameUnitManagerAPI.TryGetUnitById`, `GameTileManagerAPI.GetTileManager`, `GetTileId`,
  `GamePlayerManagerAPI.IsPlayerIdValid` und `IsPlayerAlliedTo` besitzen weiterhin dieselbe
  Bedeutung;
- `GameUnit` behält insbesondere `r_ControllableForPlayerId`, `r_CurrentTilePositionX/Y`,
  `r_AIState`, `r_AI_LastIssuedTribeCommand`, `r_AttackMoveToTargetTileX/Y` sowie die
  `r_AI_ContextTarget...`-/`r_ContextTargetTile...`-Felder an den erwarteten nativen Offsets;
- `TribeAICommand` behält für die Diagnose die Werte `AttackUnit=4`, `AttackBuilding=9` und
  `ForceAttackBuilding=36`;
- Zhuqiaomons `NativePatternResolver`-/Detour-Verhalten und MonoMods Trampolinerzeugung sind
  unverändert;
- der Extender installiert keinen neuen überlappenden Detour oder Bytepatch an den hier
  verwendeten Funktionen.

Die lokalen Extender-Quellen und die tatsächlich zum Build verwendete `SHCDESE.dll` müssen
dabei zusammenpassen. Bei Abweichungen die Assembly mit `ilspycmd` prüfen und die nativen
Extender-Hooktabellen beziehungsweise `BulkTribeDetours` vergleichen. Erst nach erfolgreichem
Compile und einem Startup-Log mit allen erwarteten aufgelösten RVAs folgt der Ingame-Test.

### 12.1.3 Versuchter synchroner Attack-Scope und Assassin-Builderzweig

Ein früherer fehlgeschlagener Attack-Test zeigte die wiederholte Statefolge `101 → 0 → 1` und
erreichte `0x196840` synchron innerhalb von `OnTribeIssueOrderWithTarget(Pre/Post)`, während der
erst in Post angelegte Tracker zu diesem Zeitpunkt noch nicht existierte. Daraus entstand der
folgende Kandidat: Der Mod legt für `AttackUnit`, `AttackBuilding`
und `ForceAttackBuilding` bereits in Pre einen threadlokalen Scope an. Eine Unit wird nur
qualifiziert, wenn Tribe, Command, Zielwerte und die nativen `r_AI_ContextTarget...`-Felder exakt
passen. Als Bewegungsziel dienen ausschließlich die von Vanilla bereits gesetzten
`r_AttackMoveToTargetTileX/Y`. Zusätzlich muss der owner-geprüfte Weg das Ziel mit eigenem oder
verbündetem fertigem Moat erreichen, ohne Moat aber nicht. Erst dann simuliert `0x196840` den
Vanilla-Zustand „Unit steht auf fertigem Moat“ und erzeugt denselben `PlanScope`, den Regions- und
Builderfallback bereits verwenden. Post entfernt den synchronen Scope stets; nur nach positivem
Command-Ergebnis bleibt die read-only Tick-Verfolgung bestehen. Die späteren `changedUnits=0`-Läufe
erreichten diesen Scope jedoch nicht mehr und belegen den noch früheren Abbruch aus Abschnitt
12.1.4.

Der zentrale Builder `0xF4930` besitzt neben `pathManager+0x80` einen Assassin-Zweig über
`pathManager+0x88`. In der Referenz-DLL beginnt dessen eindeutige Folge bei `0xF4B0C`; der CALL
bei `0xF4B27` zielt auf den speziellen Builder `0xD9C40`. Normale Units erreichten im zweiten
Moat-Fallback den Bodenbuilder, Assassinen wegen `+0x88 != 0` weiterhin `0xD9C40` und scheiterten
am fertigen Moat. Der erste Vanilla-Builderlauf bleibt nun vollständig unangetastet. Nur nach
dessen echtem Nuller, positivem Owner-/Allianzbefund und einer reinen, tatsächlich notwendigen
Moat-Route wird `+0x88` für genau den synchronen zweiten Aufruf temporär auf null gesetzt und in
`finally` wiederhergestellt. Es gibt keinen Hook auf `0xD9C40`; die gewichtete
Assassin-Mauerpfadfindung aus `BugfixesAndQoL` bleibt daher für Vanilla- und Mauerwege zuständig.

Der gültige Lauf nach der funktionalen Attack-Regionsfreigabe bestätigte die genaue
Assassin-Konstellation: `0xF4930` wurde mit `pathManager+0x80=0`, `+0x84=1` und `+0x88=1`
erreicht. Der unveränderte erste Lauf verwendete damit bei getrennten Regionen `0xD9C40` und
lieferte `0`. `builder-assassin-ground-fallback` fehlte nur, weil die damalige gemeinsame
Retry-Schranke zusätzlich `+0x80==1` verlangte. Der Assassin benötigt stattdessen einen eigenen
zweiten Kandidaten: Nach demselben Vanilla-Nuller und derselben notwendigen owner-geprüften
Moat-Route bleibt `+0x80` unverändert `0`, während nur `+0x88` für den synchronen Retry
`1 → 0 → 1` wechselt. Die Builderdiagnose liest weiterhin alle drei Felder am Eintritt, nach
Vanilla, vor dem Retry und nach dessen Rückkehr.

Bei einem Spielupdate ist der Assassin-Zweig nicht nur über das Bytepattern wiederzufinden:
Innerhalb des zentralen Builders muss ein Vergleich des Kontextfelds `pathManager+0x88` den
Spezialzweig wählen, der CALL muss weiterhin zum für Kletterkanten gewichteten Builder führen,
und der alternative Zweig muss den normalen Bodenbuilder verwenden. Instruktionsgrenzen,
vollständige Bytes und relatives Callziel sind gemeinsam zu validieren. Ändern sich die Offsets
`+0x80` oder `+0x88`, dürfen sie nicht aus der alten Version übernommen werden.

### 12.1.4 Vorgelagerte Attack-Annäherungssuchen

Die jüngsten Logs korrigieren auch die frühere Attack-Einordnung: In den aktuellen fehlgeschlagenen
Attack-Aufrufen wird `0x196840` gerade nicht erreicht. Der positive Target-Dispatcher endet mit
`changedUnits=0`; weder Unitfelder noch `MoveHere`, zentraler Planer oder Builder folgen. Die
hashgleiche Ghidra-Baseline zeigt davor getrennte Annäherungspipelines. `AttackUnit` verwendet die
tribeweite Floodsuche `0xDBC60`; ihre direkten Calls im Dispatcher liegen bei `0x11EE47` und
`0x11F46B`. Die Funktion erhält PathManager, Tribe, Zielkontext, angepasste Zielkoordinaten,
gewünschte Ergebniszahl, Ausgangsregion und Bewegungsklasse. Sie füllt ihre Queue ab
`pathManager+0x155F3C/+0x155F44` und schreibt Annäherungsergebnisse als 12-Byte-Einträge ab
`pathManager+0x1B344`.

Beim Übergang zwischen Regionen ruft `0xDBC60` an `0xDBF0D` Vanillas breiten Regionshelper
`0xE2610(pathManager, movementClass, sourceRegion, targetRegion, routeKind)` auf. Nur wenn die
aktuelle Auswahl vollständig aus Assassinen besteht, kann danach an `0xDBF33` zusätzlich die
Tilepaarprüfung `0xE2CA0` folgen. Der bereits vorhandene `0xE2CA0`-Fallback war bislang absichtlich
an einen unmittelbar zuvor erzeugten Cursor-Kontext gebunden und kann diese internen Flood-Aufrufe
daher nicht freigeben.

Die Auswahlentscheidung stammt aus `0x117820`: Der Helper iteriert die aktiven ausgewählten Units
des Tribes und liefert nur dann positiv, wenn jede davon den Chimp-Typ `0x49` besitzt. Dieser Wert
ist im Extender-Enum `CHIMP_TYPE_ARAB_ASSASIN`. Damit ist „alle ausgewählten Units sind
Assassinen“ bestätigt und keine bloße Ableitung aus dem Builderverhalten.

`AttackBuilding` und `ForceAttackBuilding` verwenden dagegen den Approach-Builder `0xDA020`,
aufgerufen bei `0x11FF9A`. Er prüft seine Gebäuderandkandidaten ebenfalls zuerst über `0xE2610`
und für reine Assassin-Auswahlen zusätzlich über `0xE2CA0`; die beiden Paare liegen bei
`0xDA1F9/0xDA232` und `0xDA47C/0xDA4B1`. Danach konsumiert `0x123090` die erzeugten Kandidaten.
Sein dritter Parameter ist nach aktuellem Stand nur als Buildervariante belegt, nicht als
Kandidatenzahl. `0x123090` wählt über dasselbe Assassin-Gate zwischen `0xD9C40`, dem Bodenbuilder
`0xDA590` und der Alternative `0xDB650`.

Für den kanonischen DLL-Hash
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` detourt die Diagnose
`0xDBC60`, `0xDA020`, `0x123090` und `0xE2610` ausschließlich read-only und nur im jeweils
passenden synchronen Attack-Event. Vanilla wird immer unverändert ausgeführt. Regionpaare werden
nach Region, Bewegungsklasse, Routekind und Vanilla-Ergebnis semantisch aggregiert. Interne
`0xE2CA0`-Aufrufe werden ebenfalls unverändert zurückgegeben; zusätzlich wird protokolliert, ob
die owner-geprüfte Moat-BFS das konkrete Tilepaar erreichen könnte. Tilepaare werden nach
Ergebnis-/Ownersemantik gruppiert und mit erstem und letztem Paar ausgegeben, statt pro
untersuchtem Nachbartile eine Logzeile zu erzeugen.
Da diese Boden-/Moat-Erreichbarkeit symmetrisch ist, startet die Diagnose ihre Map am innerhalb
eines Flood-Aufrufs stabilen Ziel und prüft die wechselnden Nachbartiles rückwärts. Dadurch wird
dieselbe Reachability-Map für alle Paare derselben Zielregion wiederverwendet; es entsteht nicht
für jedes untersuchte Tile eine neue Vollkarten-BFS. Unveränderte Kombinationen aus Command,
Tribe, Ziel und Ergebniszustand werden innerhalb desselben Commands semantisch dedupliziert; es
gibt kein abschaltendes Mengenlimit.

Der erste Lauf dieser Diagnose ist ungültig: Das Entry-Pattern für `0xDBC60` enthielt irrtümlich
`4D 8B E6` statt der realen Bytes `45 8B E6`. Die Diagnosegruppe installierte deshalb
ordnungsgemäß fail-closed nicht, während die bereits funktionierende normale Moat-Bewegung aktiv
blieb. Aus dem Fehlen von `attack-approach`-Logs in diesem Lauf darf keine Aussage über den
nativen Attackpfad abgeleitet werden.

Auch der darauffolgende Lauf vom 1. September 2026 ist für diese Diagnose ungültig. Das Pattern war
zwar korrigiert, aber die zusätzliche Prüfung der RIP-relativen `LEA` am Anfang von `0xDBC60` las
den Displacementbereich um ein Byte versetzt als `+0x1E/+0x22`. Die Instruktion beginnt bei
`+0x1A`, ihr Displacement liegt bei `+0x1D` und ihr Ende bei `+0x21`; nur diese Grenzen ergeben den
in der Baseline bestätigten Tribe-Manager `0x7CC6720`. Die falsche Berechnung ergab
`0x45157B2C`, woraufhin die gesamte neue Hookgruppe erneut korrekt fail-closed zurückgerollt wurde.
Die gleichzeitig beobachteten `AttackUnit`-Commands und AI-Zustände stammen nur aus der älteren
Command-/Tick-Diagnose und belegen noch keinen Aufruf von `0xDBC60`.

Ein Gebäude hinter dem Moat blieb in demselben Lauf bereits am roten Cursor hängen; es entstand
kein `AttackBuilding`- oder `ForceAttackBuilding`-Event. Damit können `0xDA020` und `0x123090` in
diesem Fall noch nicht laufen. Zur Trennung beobachtet `0x196870` deshalb zusätzlich read-only
Vanillas Rückgabewert, die repräsentative Unit, das Cursor-Tile und eine 35-Bit-Maske der belegten
Auswahlslots. Ein separater kurzlebiger Diagnosekontext wird auch bei positivem Vanilla-Ergebnis an
den unmittelbar folgenden `0xE2CA0`-Aufruf weitergegeben. Er protokolliert das tatsächliche
Tilepaar und den Grund, weshalb der funktionale Fallback nicht bewaffnet war, verändert aber nie
das Ergebnis. Der funktionale `AttackCursorPairScope` entsteht weiterhin ausschließlich nach
Vanilla `0`, mit gültiger Unit und gültigem Tilepaar. Dadurch erhalten insbesondere Gebäude-,
Mauer-, Wasser- und Kletterpfade keine neue Freigabe durch diese Diagnose.

Bei einem Update müssen alle vier gehookten Funktions-Entries und `0x117820` über eindeutige
Patterns und vollständige Instruktionsbytes neu gefunden werden. Zusätzlich müssen sämtliche
Dispatcher- und internen Calls weiterhin relativ auf die semantisch passenden Funktionen zeigen;
alte RVAs allein genügen nicht. Die historische Baseline bietet zusätzliche Suchanker:
`0xDBC60` entspricht semantisch dem historischen Kandidaten `0xDAE20`, `0xDA020` dem Kandidaten
`0xD91F0`, und `0x117820` ist automatisch bestätigt auf historisch `0x116940` abgebildet. Die
ersten beiden Kandidaten wurden vom automatischen Versionsmatcher nicht akzeptiert, besitzen aber
gleiche Signatur, Blockanzahl, Callee-Rollen und Kontrollflussstruktur und dürfen deshalb nur als
Suchhilfe, nicht als alleiniger Adressbeweis verwendet werden. Ein Validierungs- oder
Installationsfehler rollt die ganze Diagnosegruppe zurück und lässt den normalen Bewegungspfad
aktiv.

### 12.2 Logauswertung

Das BepInEx-Log wird angehängt. Jeder neue Spielstart beginnt mit:

`[Message:   BepInEx] BepInEx ... - Stronghold Crusader Definitive Edition`

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
- `cursor-selection-gate`
- `cursor-tile-pair-observed`
- `attack-cursor-pair`
- `attack-track-start`
- `attack-state-end`
- `attack-mode-unscoped`
- `attack-command-candidate`
- `attack-scope-qualified`
- `attack-scope-rejected`
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
- `builder-assassin-ground-fallback`
- `builder-gate`
- `builder-native-entry`
- `builder-native-after-vanilla-first`
- `builder-native-before-fallback`
- `builder-native-after-fallback`
- `move-track-start`
- `move-milestone`
- `move-state-end`
- `building-consumer-performance`
- `attack-approach` mit `UnitFlood`, `BuildingApproach` oder `BuildingCandidateConsumer`
- `wall-command-staged`, `wall-track-start`, `wall-state`, `wall-mode`, `wall-planner` und
  `wall-builder-entry/return`

Es gibt keine globalen Diagnosegrenzen mehr. Während eines synchronen `move-command` sammelt der
Mod zunächst dessen vollständige Command-, Flood-, Mode-, Region-, Owner- und Builderdiagnose im
zugehörigen `MoveCommandScope`. Erst wenn die konservative Owner-Routenprüfung tatsächlich
mindestens ein freundliches oder feindliches Moat-Tile beobachtet, wird der gesamte gepufferte
Befehl nach dessen Post-Phase ausgegeben. Befehle ohne jeden Moat-Befund werden verworfen. Dadurch
bleiben Moat-relevante Patrol-Hin- und Rückläufe sichtbar, während normale Patrol-, KI- und
Kartenstartbewegungen das Log nicht füllen. Cursorentscheidungen werden
weiterhin anhand der bereits vorhandenen BFS-Generation beziehungsweise des unveränderten
Auswahl-, Unit- und Tilepaarzustands dedupliziert; sie besitzen ebenfalls kein abschaltendes
Lebenszeitlimit.

Die frühere Attack-/Move-Tickdiagnose schrieb zwar nur bei Zustandsänderungen, erzeugte im
KI-Schnellvorlauf aber dennoch mehr als 107.000 Zeilen in knapp fünf Minuten. Der Tracker meldet
deshalb ohne Mengenbudget nur noch semantische Meilensteine: Bewegungsbeginn, Betreten und
Verlassen des Moats, Zielerreichung, einmaliges Feststecken oder Commandende. Ein erfolgreicher
Moataustritt beendet weitere Moat-Übergangsmeldungen; der schlanke Tracker bleibt nur bis zum
Annäherungsziel, einem einmaligen Stillstandsbefund oder Commandende erhalten. Die synchronen
Attack-Pipelineflags werden bereits nach der Command-Postphase zusammengefasst und danach
verworfen. Damit bleiben relevante Fehlerzustände sichtbar, ohne jeden Animations- oder
Pfadschritt zu protokollieren.

Falls ein Befehl wie ein zuckender Patrol-Teilweg bereits nach der Moduswahl und vor dem Builder
endet, führt seine Post-Phase genau eine zusätzliche read-only Owner-Routenprüfung für den letzten
korrelierten Unit-Plan aus. Sie dient ausschließlich dazu, den gepufferten Befehl als Moat-relevant
zu erkennen; ihr Ergebnis ändert weder den Rückgabewert noch irgendeinen nativen Pfadzustand.

Nach dem bestätigten Patrol-Vergleich wurde die dafür vorübergehend vorhandene tribeweite
Diagnosesitzung wieder entfernt. Insbesondere installiert `MoveMoatTest` keinen
`OnTribeGetNextPatrolWaypoint`-Observer mehr und speichert keinen Patrolzustand über einzelne
Move-Aufträge hinweg. Patrol bleibt funktional durch denselben allgemeinen Move-/Builderpfad
abgedeckt; nur Moat-relevante Teilwege werden weiterhin gepuffert ausgegeben. Die unabhängige
`target-command`-Beobachtung bleibt dagegen gezielt für die weitere Attack-Analyse aktiv.

Ein fehlender späterer Logeintrag darf nicht sofort als fehlender nativer Funktionsaufruf interpretiert werden. Zuerst prüfen, ob Attempt-ID, globale Zielwerte oder Current-Unit-Korrelation den Callback herausfiltern.

### 12.1.2 Gültiger Attack-Approach-Lauf und erster funktionaler Regionsfallback

Nach Korrektur des `0xDBC60`-Patterns und der RIP-relativen Tribe-Manager-`LEA` wurde die gesamte
Attack-Approach-Diagnose beim Start nachweislich installiert. Der gültige Lauf vom 1. September
2026 zeigte für drei `AttackUnit`-Befehle eines Maceman jeweils dasselbe Ergebnis:

- `0xDBC60` lief als `UnitFlood` mit `sourceRegion=1`, `movementClass=1` und einer normalen,
  nicht ausschließlich aus Assassinen bestehenden Auswahl.
- `0xE2610` prüfte achtmal `regions=1->2`, `routeKind=0`; Vanilla lieferte jedes Mal `0`.
- `0xDBC60` beendete den Lauf mit `results=0`. Der äußere Target-Command lieferte zwar `1`,
  aktualisierte aber keine Unitfelder.
- Für dieselben Ziele hatte die owner-geprüfte Suche bereits `visitedWithMoat=true`,
  `visitedWithoutMoat=false`, freundliche fertige Moats und getrennte Regionen bestätigt.

`0xE2610` erhält deshalb nun ausschließlich innerhalb eines passenden `UnitFlood` einen
funktionalen Vanilla-first-Fallback. Er validiert Command, Tribe, repräsentative Unit,
Bewegungsklasse, Quellregion, Target-Unit-ID und Global-ID sowie das tatsächlich durch die
Owner-BFS erreichte Annäherungs-Regionspaar. Nur eine ohne Moat unmögliche Route über eigenen oder
verbündeten fertigen Moat darf aus Vanilla `0` effektiv `1` machen. Die Entscheidung wird pro
Command und Regionspaar wiederverwendet und als `attack-unit-region-fallback` protokolliert.

Der positive Rückgabewert von `0x196870` bezeichnet bei ausschließlich belegtem Slot 22 die
Assassin-Auswahlart. Für diesen Fall darf der nachfolgende `0xE2CA0`-Nuller nur noch in zwei
belegbaren Situationen überstimmt werden: ein freies gewöhnlich begehbares Ziel mit notwendiger
freundlicher Moat-Route oder eine lebende feindliche Unit exakt auf dem Zieltile mit entsprechend
notwendigem Annäherungsweg. Der allgemeine Cursorzweig `0x8F32F` bleibt ungepatcht; Gebäude,
Mauern und sonstige belegte Tiles werden durch diese Assassin-Erweiterung nicht freigegeben. Für
direkte Ziele und Annäherungstiles schließt sie zusätzlich Vanillas Struktur-/Höhenmaske
`0x10000300` aus; damit reicht das allgemeine Walkable-Bit allein nicht zur Freigabe eines
Mauertiles.

Der anschließende Funktionslauf bestätigte die `UnitFlood`-Freigabe: Für einen Maceman entstanden
aus zuvor `results=0` nun 50 Annäherungskandidaten. Attack-Scope, Moat-Modus, Regionsfallback und
der zweite Builderlauf wurden erreicht; dessen echter Rückgabewert `14` erzeugte einen
vollständig konsumierten Pfad über den Moat. Ein Assassin erreichte danach dieselbe Pipeline und
denselben owner-qualifizierten Plan, blieb aber am oben beschriebenen `+0x80=0/+0x88=1`-Fall
hängen. Damit ist dieser Builderzweig der einzige im Lauf beobachtete verbleibende
Assassin-Abbruch.

### 12.1.5 Historischer Versuch: zentrale Assassin-Moat-Kanten im gewichteten Builder

> Status: nicht als funktionale Lösung bestätigt und wieder entfernt. Die folgenden Absätze
> dokumentieren den Versuch, nicht den aktuellen Code. Maßgeblich ist Abschnitt 15.

Für die kanonische DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
ist `0xD9C40` die gemeinsame Assassin-Graphsuche. Die Baseline weist vier direkte Caller aus:
`0xE2CA0`, `0xF4930`, `0x11B520` und `0x123090`. Damit liegt die Funktion unter Cursor-Tilepaar,
zentralem Pfadbuilder, Kletterbewegung und Gebäude-Kandidatenverbrauch. Ihr Vanilla-Fallback prüft
Zieltile mit der Maske `0x4A5014B1`; Bit 30 für fertige Moats ist darin enthalten. Das erklärt,
warum einzelne Cursor- oder Commandfreigaben einen Assassin zwar bis zu diesem Builder bringen,
aber keine kombinierte Moat-/Kletterroute erzeugen können.

`BugfixesAndQoL` besitzt bereits den einzigen Detour auf `0xD9C40` und ersetzt ihn bei aktivierter
verbesserter Assassin-Wegfindung durch eine gewichtete Suche. Die Testlösung erweitert genau diese
Suche um owner-sichere Moat-Kanten. `MoveMoatTest` installiert ausdrücklich keinen zweiten Hook,
sondern registriert per Reflection einen internen Callback `playerId/tileId -> friendly moat`.
Die Brücke akzeptiert höchstens einen Provider, hält ohne Provider das bisherige Verhalten logisch
unverändert und behandelt fehlende oder fehlerhafte Daten als nicht begehbar. Die
Rekonstruktionsfreigabe wird nur aktiv, solange entweder die vorhandene Assassin-Option oder dieser
Testprovider aktiv ist.

Die gewichtete Suche unterscheidet drei Kantenarten:

- normale Boden-/Diagonalübergänge nach den unveränderten nativen Richtungs- und
  Belegungsmasken;
- kardinale Übergänge Boden↔freundlicher Moat und freundlicher Moat↔freundlicher Moat;
- kardinale Kletterübergänge, weiterhin gebunden an Vanillas Wall-, Stair-, Building- und
  Kletterfreigaben sowie an die bestehenden Kletterkosten.

Ein fertiger Moat mit negativem Providerergebnis ist keine Kante. Eine nur durch den Testprovider
aktivierte Route wird ausschließlich veröffentlicht, wenn Vanillas erster Lauf `0` lieferte und
die rekonstruierte Route tatsächlich mindestens ein eigenes oder verbündetes fertiges Moat-Tile
enthält. Ohne aktive verbesserte Assassin-Wegfindung kann die Brücke daher keine reine alternative
Boden- oder Kletterroute ersetzen. Der bisherige zweite Bodenbuilderlauf in `MoveMoatTest` bleibt
nur als Kompatibilitätspfad erhalten, wenn `BugfixesAndQoL` beziehungsweise die Brücke fehlt.

Dieselbe Suche besitzt eine read-only Probe. Sie schreibt weder Generationen noch Distanzfelder
und liefert nur die Flags `reachable`, `used friendly moat` und `used climb edge`. Der Cursor kann
dadurch Startpositionen auf Mauern und Ziele nach kombinierten Moat-/Kletterkanten prüfen, ohne für
jeden Assassin-Command oder AI-State einen eigenen Bewegungsfallback zu benötigen. Die Probe ist
nicht reentrant; Fehler behalten Vanilla.

Beim Gebäude-Hover ist die Argumentsemantik von `0xE2CA0` wichtig: Der beobachtete relevante
Aufruf verwendet als Zieltile bereits ein freies Annäherungsfeld außerhalb des verifizierten
Gebäude-Footprints und `useCache=0`. Es ist weder das Hovertile noch zwingend der zuvor gespeicherte
globale Zielpunkt. Der Building-Scope darf deshalb `useCache=0` nur akzeptieren, wenn Starttile und
ausgewählte Unit exakt passen und das tatsächliche Zieltile ein freies Außenfeld desselben
lebenden, feindlichen Gebäudes ist. Die owner-sichere Route wird genau zu diesem Tile geprüft;
Wall-, Stair- und Ramp-Strukturen bleiben aus dem Building-Scope ausgeschlossen. `0xDA020` und
`0x123090` bleiben zunächst read-only, bis ein echter Building-Command den nächsten möglichen
Abbruch belegt.

Nach einem Spielupdate darf weder die RVA noch die Maske allein übernommen werden. Zuerst den
aktuellen Hash gegen `CURRENT.json` prüfen, dann die Funktion über Signatur, CFG beziehungsweise
normalisierten Hash und ihre vier Caller wiederfinden. Anschließend die Zugriffe auf
Validitätsgrid, Row-Lookup, Tileflags, Building-Layer, Occupancy-Layer, Distanz-/Visit-Felder und
Richtungsmasken erneut bestätigen. Die historische Zuordnung `0xD8E10 -> 0xD9C40` ist in der
aktuellen Baseline mit `unique-normalized-hash-and-cfg` bestätigt und eignet sich nur als
zusätzlicher Suchanker.

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

Die ältere Assassin- und Kletterbeschreibung weiter unten in diesem Abschnitt ist historisch.
Der aktuelle, absichtlich kleinere Funktionsumfang und seine Vanilla-Capability-Grenze stehen in
Abschnitt 15 und ersetzen widersprechende Aussagen dieser Kurzfassung.

- Allgemeine wiederholte Bewegung durch fertige Moats funktioniert im Editor und Skirmish.
- Der frühere globale Fallthrough bei `0x8F393` sowie die drei Entity-Sprungpatches sind entfernt.
  Alle vier Stellen bleiben Vanilla und werden nur als bytevalidierte Update-Suchanker geführt.
- `0x196870` ist nun das semantische Cursor-Gate: Ein Vanilla-Nuller wird nur nach exakter
  Zielklassifikation und notwendiger owner-sicherer Moat-Route effektiv positiv. `0xE2CA0`
  validiert denselben kurzlebigen Kontext erneut. Positive Regions-/Direktresultate werden
  zusätzlich owner-sicher geprüft, weil der Editor auch bei gleicher Region hinter feindlichem
  Moat positiv antworten kann. Nur ein nachgewiesenes ausschließlich feindliches Moat-Hindernis
  darf dabei Vanillas positives Ergebnis auf `0` ändern; unvollständige Daten bleiben Vanilla.
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
- Unit- und Gebäudeziele verwenden denselben Gate-Mechanismus. Gebäude werden über Hover-ID,
  StructureGrid-ID, Alive-State, Global-ID, Owner, Typ und realen Footprint gebunden; reine
  Mauer-/Treppenstrukturen sind ausgeschlossen. Der relevante `0xE2CA0`-Aufruf darf im exakt
  gebundenen Building-Scope `useCache=0` verwenden und wird gegen sein tatsächliches freies
  Annäherungstile außerhalb des Footprints geprüft. `0xDA020` und `0x123090` bleiben zunächst
  read-only, damit der nächste Gebäudetest den späteren Abbruch eindeutig zeigt.
- Attack-Commands erhalten während ihres synchronen Pre/Post-Aufrufs einen exakten
  Tribe-/Command-/Zielscope. Nur das von Vanilla gesetzte Attack-Move-Ziel und eine ohne
  eigenen/verbündeten Moat unmögliche Route dürfen denselben allgemeinen Plan-, Regions- und
  Builderfallback aktivieren. Früh angelegte Tracker behalten ihre Mode-, Planer- und
  Buildermarkierungen in der anschließenden read-only Tick-Verfolgung.
- Assassinen verwenden bei `pathManager+0x88 != 0` den Spezialbuilder `0xD9C40`. Der bestätigte
  Moat-Fehlfall tritt mit `+0x80=0/+0x88=1` ein. Die bevorzugte Lösung ist jetzt die zentrale
  owner-sichere Erweiterung des bereits von `BugfixesAndQoL` kontrollierten gewichteten Builders;
  sie erhält Boden- und Kletterkanten in derselben Suche. `MoveMoatTest` installiert keinen
  zweiten Hook. Der frühere temporäre Wechsel `+0x88: 1 → 0 → 1` bleibt ausschließlich als
  fail-closed Kompatibilitätspfad ohne aktive Brücke bestehen.
- Der streng owner-geprüfte `UnitFlood`-Regionsfallback ist für normale `AttackUnit`-Befehle
  funktional bestätigt: `0xDBC60` erzeugte 50 Kandidaten und der echte Builderpfad wurde
  vollständig bewegt. Gebäudeangriffe über `0xDA020`/`0x123090` bleiben vorerst diagnostisch.
- Der A/B-bestätigte normale Kletterfehler entstand nach den Laufzeitlogs dadurch, dass
  `0x196840` für jeden synchronen `MoveHere`-Aufruf `0 → 1` erzwang, auch wenn kein Moat nötig
  war. Ein aktiver Command ist daher keine ausreichende Freigabe mehr: Modus, Plan und Builder
  werden nur nach `reachedWithMoat=true`, `reachedWithoutMoat=false` und mindestens einem
  eigenen/verbündeten Moat aktiviert. Gewöhnliche Wege und normale Mauerannäherungen bleiben
  vollständig bei Vanilla.
- `0x199B70` war als Hauptdiagnose für Assassin-Klettern falsch gewählt. Die hashgleiche Baseline
  bestätigt stattdessen `0x195E30 → 0x11B520`: `0x195E30` übernimmt Unitmanager, Tribe und
  Zielkoordinaten, prüft die repräsentative Unit über `0x18D460` und stößt den allgemeinen
  `MoveHere`-Pfad an. Der neue read-only Detour validiert die 33 Entrybytes sowie alle vier Calls
  `0x8F7BA`, `0x8FD3C`, `0x8FDC6` und `0x8FE54`. `0x199B70` bleibt nur historischer Suchanker.
- Gebäude-Hover dürfen nicht von den globalen Zielkoordinaten abhängen: Im beobachteten roten
  Hover waren diese `(0,0)` beziehungsweise ergaben Tile `-399`, während rohe Building-ID und
  Mouse-Tile gültig waren. Der aktuelle Scope akzeptiert deshalb ausschließlich eine 1-basierte,
  lebende feindliche Building-ID und ein `r_MouseTileId`/`r_MouseTileId2`, das durch erneute
  `GetTileId(x,y)`-Prüfung eindeutig innerhalb des realen Footprints liegt. Global-ID, Owner,
  StructureGrid-ID und Typ werden erneut gebunden; Wall-, Stair- und Ramp-Typen bleiben draußen.
- Für eine Mauer hinter freundlichem Moat wird nur ein Assassin-Scope geöffnet, wenn Vanilla das
  Wall-Hover grundsätzlich akzeptiert und die read-only Probe des gewichteten Builders eine
  notwendige owner-sichere Route mit mindestens einer freundlichen Moat-Kante bestätigt. Dieselbe
  zentrale Suche erzeugt anschließend die kombinierte Moat-/Kletterroute und deckt auch einen
  Assassin-Start auf einer Mauer ab.
- `0x196840` bedeutet nachweislich „Unit steht auf einem Tile mit fertigem-Moat-Bit“, während
  `0x196870` nur Auswahlarten prüft; diese Semantik bei Updates nicht wieder verallgemeinern.
- Als Nächstes diese Stufe mit eigenem, verbündetem und feindlichem Moat sowie Umweg-, Mauer-
  und Wasser-Kontrollen im Editor testen; danach Skirmish und Gruppenbewegung.
- Nach einem DLL-Update die semantische Wiederauffindungsanleitung in Abschnitt 12.1.1 nutzen;
  alte RVAs niemals allein aufgrund eines ähnlichen Hash-/Versionsumfelds übernehmen.

## 15. Aktueller Kandidat: Überquerung nur für Vanilla-grabfähige Units

### 15.1 Entscheidung und Ergebnis des Assassin-Versuchs

Die universelle Erweiterung des gewichteten Assassin-Builders `0xD9C40` wurde im gemeinsamen
Test mit `BugfixesAndQoL` zwar erreicht, aber nicht erfolgreich von Vanillas späterem
Pfadverbrauch übernommen. Die Brückenprobe fand nachweislich kombinierte Routen mit freundlichen
Moat- und Kletterkanten. Beim wirklichen Auftrag veröffentlichte Vanilla jedoch keinen nutzbaren
Pfad; der Assassin blieb stehen und wechselte anschließend wieder durch die bereits bekannte
Abbruchfolge der AI-States. Eine gefundene Probe ist daher kein Beleg für einen konsumierbaren
nativen Pfad.

Dieser Ansatz ist ausdrücklich **nicht** als Lösung bestätigt. Die Reflection-Brücke, ihre
Assassin-Routenprobe, der spezielle Assassin-Bodenretry und alle Moat-spezifischen
Wall-/Kletterfreigaben wurden wieder entfernt. `MoveMoatTest` und `BugfixesAndQoL` sind erneut
voneinander unabhängig. `BugfixesAndQoL` besitzt wieder ausschließlich seine normale gewichtete
Assassin-Pfadfindung; ohne Moat bleibt das gewöhnliche Klettern unverändert.

Die fachliche Grenze lautet nun: Nur eine Unit, die Vanillas Befehl `DigMoatTileId = 6`
tatsächlich pro Unit akzeptiert, darf den owner-geprüften Moat-Fallback erhalten. Dadurch fallen
Assassinen, Armbrustschützen, Schwertkämpfer, Ritter und Belagerungsgeräte ohne zusätzliche
Whitelist- oder Sonderpfade heraus.

### 15.2 Maßgebliche per-Unit-Quelle: `0x11E960`, Command 6

Für die kanonische DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
liegt der gemeinsame Tribe-Command-Dispatcher bei RVA `0x11E960` (VA `0x18011E960`). Seine
aktuelle semantische Baseline weist Größe `13291`, Raw-Hash
`8F682F4F350FBC384475C747C1EB17C7181BA64BABF1A8155BFEA411988658A1` und
Normalized-Hash `3B70D8BB37D897E161746F7289B0B645AD3EAEB37D2A281003176B5866D1CA01`
aus. Die bestätigte historische Zuordnung lautet `0x11DA80 -> 0x11E960`
(`unique-normalized-hash-and-cfg`).

Im Switchfall `param_3 == 6` iteriert Vanilla die Units des Befehls und führt einen inline
Unit-Type-Switch aus. Nur diese zehn `eChimps`-Werte gelangen in den Zweig, der Command, Ziel und
Moat-Arbeitsfelder der jeweiligen Unit setzt:

- `CHIMP_TYPE_ARCHER` (`0x16`)
- `CHIMP_TYPE_SPEARMAN` (`0x18`)
- `CHIMP_TYPE_PIKEMAN` (`0x19`)
- `CHIMP_TYPE_MACEMAN` (`0x1A`)
- `CHIMP_TYPE_ENGINEER` (`0x1E`)
- `CHIMP_TYPE_ARAB_SLAVE` (`0x47`)
- `CHIMP_TYPE_BEDOUIN_EUNUCH` (`0x50`)
- `CHIMP_TYPE_BEDOUIN_SKIRMISHER` (`0x52`)
- `CHIMP_TYPE_BEDOUIN_SAPPER` (`0x54`)
- `CHIMP_TYPE_BEDOUIN_DEMOLISHER` (`0x55`)

Das ist die maßgebliche per-Unit-Definition von Grabfähigkeit. Der aktuelle Command-6-Pfad liest
kein allgemeines Capability-Feld. Insbesondere wird das historisch diskutierte Feld
`unit+0x170` hier nicht gelesen und darf nicht als bestätigte Grabfähigkeit behandelt werden.
`MoveMoatTest.CanDigMoat(GameUnit*)` spiegelt deshalb exakt diesen Switch mit Enumkonstanten.

### 15.3 Unveränderter Auswahlhelper: `0x191C00`

RVA `0x191C00` (VA `0x180191C00`, Größe 99) ist der bestätigte
`selectionCanDigMoat`-Helper. Er prüft die zehn Auswahlzähler bei Offsets `0x580`, `0x5B4`,
`0x568`, `0x564`, `0x56C`, `0x5E8`, `0x5EC`, `0x5E0`, `0x5D8` und `0x574` und liefert `1`,
sobald mindestens einer davon ungleich null ist. Die Zähler werden von `0x198260` aus denselben
Unit-Typen aufgebaut. Der Helper sagt daher nur „mindestens eine grabfähige Kategorie ist
ausgewählt“; er ersetzt nicht die spätere per-Unit-Filterung.

Vollständiges Funktionspattern der aktuellen DLL:

    83 B9 80 05 00 00 00 75 54 83 B9 B4 05 00 00 00 75 4B
    83 B9 68 05 00 00 00 75 42 83 B9 64 05 00 00 00 75 39
    83 B9 6C 05 00 00 00 75 30 83 B9 E8 05 00 00 00 75 27
    83 B9 EC 05 00 00 00 75 1E 83 B9 E0 05 00 00 00 75 15
    83 B9 D8 05 00 00 00 75 0C 83 B9 74 05 00 00 00 75 03
    33 C0 C3 B8 01 00 00 00 C3

Die Cursor-Callsite liegt bei RVA `0x8D3CE`; ihre exakten Callbytes sind
`E8 2D 48 10 00` und ihr Ziel muss erneut `0x191C00` ergeben. Der umgebende Suchanker beginnt
bei `0x8D3C2`:

    44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ??
    85 C0 74 30 B8 01 00 00 00

Der aktuelle Mod ruft diesen Helper unverändert auf; er detourt ihn nicht. Bei einer gemischten
Auswahl genügt sein positives Ergebnis für den grünen Gruppenbefehl. Alle Plan-, Modus-,
Regions-, Attack-Approach- und Builderentscheidungen werden danach erneut pro konkreter Unit mit
`CanDigMoat` abgesichert. Nur passende Units erhalten den Fallback. Da nachfolgende
Cursorprüfungen eine beliebige repräsentative ausgewählte Unit liefern können, bindet der Mod
seine owner-sichere Cursorprobe bei einer gemischten Auswahl an eine tatsächlich ausgewählte,
lebende grabfähige Unit desselben Spielers. Die unmittelbar folgende Tilepaarprüfung darf dabei
eine andere repräsentative Gruppenunit melden; die Freigabe bleibt an den weiterhin ausgewählten
Gräber und das unveränderte Ziel gebunden. Dadurch hängt der Gruppencursor nicht von der
Auswahlreihenfolge ab, während der spätere Bewegungsfallback weiterhin strikt pro Unit arbeitet.

### 15.4 Laufzeitvertrag und Fail-closed-Verhalten

Die Owner-BFS bleibt unverändert streng: Das Ziel muss mit eigenem oder verbündetem fertigem Moat
erreichbar und ohne Moat unerreichbar sein; feindlicher Moat, Wasser und Mauern werden nicht als
Bodenkanten freigegeben. Eine Unit außerhalb der zehn Typen darf weder `PlanScope` noch
erzwungenen Moat-Modus, Regionsfallback, Attack-Approach-Fallback oder Builderretry erhalten.
Dies gilt unabhängig davon, ob Spieler, Editor, Script Extender oder KI-Tribe den Befehl auslöst.

Cursorentscheidungen verwenden für die Auswahl Vanillas `0x191C00`. Reine ungeeignete Auswahlen
erhalten keinen positiven Moat-Fallback. Bei gemischten Gruppen darf der Cursor positiv sein,
aber die tatsächliche Bewegung wird weiterhin pro Unit gefiltert. Positive Editor-/Regionswerte
werden zusätzlich owner-sicher geprüft: Hinter einem notwendigen feindlichen Moat bleibt der
Cursor rot; für eine ungeeignete Unit zählt auch ein freundlicher Moat nicht als owner-sichere
Route.

Capability-Diagnosen werden semantisch nach Kartenepoch, Quelle, Unit-Typ, Command und Ziel
dedupliziert und als `stage=vanilla-digger ... accepted=True/False` ausgegeben. Es gibt weiterhin
keine abschaltende globale Loggrenze.

### 15.5 Wiederauffinden und Validieren nach Updates

Nach einem Spielupdate in dieser Reihenfolge vorgehen:

1. Hash der installierten `CrusaderDE.dll` mit `CURRENT.json` und dem `binaryHash` der Baseline
   vergleichen. Alte RVAs bei Abweichung niemals patchen oder als aktuellen Vertrag behandeln.
2. Den 99-Byte-Helper über das vollständige Pattern, Funktionsgrenzen, Raw-/Normalized-Hash und
   seinen Caller im Cursor-Dispatcher wiederfinden.
3. Die Callsite semantisch als DigMoat-Cursorfall identifizieren und Callziel sowie vollständige
   Callbytes erneut validieren; nicht nur nach einer relativen Adresse suchen.
4. Den großen Command-Dispatcher über normalisierten Hash, CFG, bestätigtes historisches Match
   und seinen äußeren Command-Switch wiederfinden. Im Fall Command 6 den inline Unit-Type-Switch
   erneut vollständig auslesen.
5. Über `0x198260` kontrollieren, welche Unit-Typen die zehn vom Auswahlhelper gelesenen Zähler
   befüllen. Auswahlhelper und per-Unit-Switch müssen weiterhin dieselbe Menge ausdrücken.
6. Weichen die Mengen ab oder ist eine Zuordnung nur `candidate`, bleibt das gesamte
   Capability-Feature fail-closed, bis Auswahl- und Commandpfad erneut bestätigt sind.
7. Nach einem Script-Extender-Update zusätzlich die Namen und Zahlenwerte aller verwendeten
   `eChimps`-Konstanten prüfen; keine historischen numerischen Werte stillschweigend übernehmen.

Die historische Zuordnung `0x190BC0 -> 0x191C00` ist für den Auswahlhelper durch
`unique-raw-hash` bestätigt. Sie ist ein Suchanker, keine Erlaubnis, die aktuelle RVA ohne
erneute Hash- und Byteprüfung zu verwenden.

### 15.6 Mischgruppen: Cursorentscheidung nach Gruppenerreichbarkeit

Der Test vom 3. September 2026 bestätigte einen Randfall der ersten Capability-Fassung. Eine
grabfähige Unit stand bereits hinter dem eigenen Moat direkt beim Ziel, während ungeeignete
Mitglieder derselben Auswahl noch auf der anderen Seite standen. Der Cursor blieb rot und es
entstand kein Command. Die Logs zeigten gleichzeitig:

- `vanillaDiggerSelection=True` für die gemischte Auswahl;
- die ausgewählte grabfähige Unit 3 bei `start=(419,376)/142148`;
- für nahe Ziele `fallbackArmed=False` mit `no-required-friendly-moat-route`, weil diese Unit
  das Ziel auf ihrer Seite bereits ohne Moat erreichte;
- bei weiter entfernten Zielen verwendete `0xE2CA0` eine andere repräsentative Gruppenunit auf
  Tile `156048` statt des gespeicherten Starttiles `142148`.

Die Ursache lag damit im Cursor-Aggregat, nicht im späteren Bewegungsfallback. Eine notwendige
freundliche Moat-Route der gewählten Gräber-Referenz allein bildet gemischte Auswahlen nicht
vollständig ab.

Der Cursorpfad bewertet freie Ziele und `AttackUnit`-Annäherungsfelder deshalb nun über alle
lebenden ausgewählten Units desselben Spielers:

- eine Unit ist legal erreichbar, wenn sie das Ziel ohne Moat erreicht oder als
  `CanDigMoat`-Unit einen notwendigen eigenen beziehungsweise verbündeten Moat verwenden kann;
- eine freundliche Moat-Trennung liegt vor, wenn eine ausgewählte Unit das Ziel mit erlaubten
  freundlichen Moat-Tiles, aber nicht ohne Moat erreicht;
- Vanillas negatives Auswahlresultat wird nur angehoben, wenn mindestens eine Unit legal
  erreichbar ist und mindestens eine Unit durch einen freundlichen Moat getrennt ist;
- reine ungeeignete Auswahlen, feindliche Moats, Wasser und Mauern erhalten dadurch keine neue
  Freigabe.

Die Entscheidung ist an Kartenepoch, Spieler, Zielart, Zieltile sowie eine sortierte Signatur aus
Unit-ID, Typ, aktuellem Tile und Grabfähigkeit aller gültigen ausgewählten Units gebunden. Eine
unmittelbar folgende `0xE2CA0`-Prüfung darf eine andere repräsentative Gruppenunit verwenden,
solange diese Signatur und das Ziel unverändert sind. Planer, Modus, Regionsprüfung und Builder
bleiben anschließend strikt pro Unit durch `CanDigMoat` begrenzt. Die Diagnose
`stage=cursor-group-route` fasst Auswahlgröße, Gräber, legal erreichbare Units, durch freundlichen
Moat getrennte Units und das effektive Ergebnis zusammen.

Die Gruppenprobe führt nicht pro Unit eine Vollkarten-BFS aus. Units mit derselben positiven
Vanilla-Startregion teilen sich eine Probe, weil die Region bereits eine zusammenhängende
Bodenkomponente bezeichnet. Nur regionslose Starttiles werden getrennt behandelt. Zusätzlich
wird das Ergebnis für die vollständige Auswahl-/Positions-/Zielsignatur zwischengespeichert.
Damit wachsen die teuren Prüfungen mit der Zahl verschiedener Startregionen statt mit der Zahl
ausgewählter Units; jede Auswahl-, Tile-, Ziel- oder Kartenänderung verwirft den Cache semantisch.

### 15.7 Separater Vanilla-Randfall: Assassin in gemischter Kletterauswahl

Vanilla blockiert auch ohne diesen Mod einen Mauer-Kletterbefehl, wenn ein Assassin gemeinsam
mit einer gewöhnlichen, nicht kletterfähigen Unit ausgewählt ist. Die aktuelle Baseline zeigt in
`0x11B520`, dass `0x117820` einmal für die gesamte Auswahl prüft, ob alle aktiven ausgewählten
Units Assassinen sind. Nur dann wird der gemeinsame Assassin-/Kletterbuilder gewählt; erst später
iteriert die Funktion wieder über einzelne Units.

Dieser Befund gehört nicht zur Moat-Capability: Assassinen sind keine Vanilla-Gräber und erhalten
in `MoveMoatTest` keinen Moat-Fallback. Eine Änderung müsste den gemeinsamen Builderentscheid in
`0x11B520` und die spätere per-Unit-Ausgabe des Kletterbefehls zusammen behandeln. Sie bleibt ein
separater QoL-Kandidat und darf nicht durch eine pauschale Mauer-Cursorfreigabe in diesem Mod
vorweggenommen werden.

### 15.8 Vanilla-Gruppenfehler bei Units auf und außerhalb eines fertigen Moats

Der Editor-Test vom 3. September 2026 zeigte einen zweiten, vom Cursorfall aus Abschnitt 15.6
getrennten Gruppenfehler: Stehen beim Erteilen eines `MoveHere`-Befehls einige grabfähige Units
bereits im fertigen Moat und andere außerhalb, bleiben je nach interner Gruppenreihenfolge
entweder die Units im Moat oder die außerhalb stehenden Units ohne verwertbaren Pfad stehen.
Vanilla zeigt denselben Grundfehler: Eine einzelne Unit im Moat und eine Gruppe, deren Mitglieder
alle im Moat stehen, lassen sich bewegen; in einer gemischten Innen-/Außengruppe reagieren die
Units im Moat dagegen nicht zuverlässig.

Die hashgleiche Native-Baseline lokalisiert die gemeinsame Entscheidung in `MoveHere` bei RVA
`0x11B520` (historisch bestätigt `0x11A640 -> 0x11B520`). Die Funktion liest bei
`tribe + tribeId * 0x688` die Leitunit aus Offset `0x5A` und ruft bei RVA `0x11B666` den Helper
`0x117BC0` auf. Die entscheidende Sequenz lautet:

    48 8B CF E8 55 C5 FF FF 44 3B F8 75 72

Das entspricht `mov rcx,rdi; call 0x117BC0; cmp r15d,eax; jne ...`. `0x117BC0` iteriert die
Gruppe über `0x119F90` und berücksichtigt eine Unit nur bei `AliveState == 2` und einem unteren
16-Bit-Wert von `0` bei `unit+0x29C`. Anschließend liefert es die erste solche Unit zurück, deren
aktuelles Tile Bit 30 (`0x40000000`) besitzt. Nur wenn diese erste Moat-Unit zugleich die zuvor
gewählte Leitunit ist, aktiviert `0x11B520` seinen gemeinsamen Moatpfad. Deshalb ist das Ergebnis
von Auswahlreihenfolge und Leitunit abhängig. Der Gruppeniterator ist dabei die maßgebliche
Mitgliedschaftsquelle; ein zusätzlicher Vergleich mit dem kurzzeitig veränderlichen
`unit.r_TribeId` gehört nicht zum Vanilla-Helper.

Die anschließende Builderauswahl in `0x11B520` ist ebenfalls gruppenweit:

- gewöhnliche Gruppen verwenden `0xDA590`;
- bei gesetztem gemeinsamen Moatmodus wird `0xDAFD0` verwendet;
- reine Assassin-Gruppen verwenden den separaten Builder `0xD9C40`.

Eine pauschale Auswahl von `0xDAFD0` wäre für `MoveMoatTest` falsch, weil dadurch auch
nicht grabfähige Gruppenmitglieder den gemeinsamen Moatpfad erhalten könnten. Der aktuelle
Kandidat detourt daher `0x117BC0`, ruft immer zuerst Vanilla auf und normalisiert ausschließlich
eine validierte gemischte Gruppe mit einer grabfähigen Unit auf eigenem oder verbündetem Moat
auf Ergebnis `0`. Die teurere Gruppen- und Routenprüfung beginnt erst, wenn Vanillas Rückgabewert
tatsächlich der Leitunit entspricht und `0x11B520` andernfalls den gemeinsamen Moatbuilder wählen
würde. Nach der ersten positiven owner-sicheren Routenprobe werden für weitere Gruppenmitglieder
keine vollständigen Karten-BFS mehr ausgeführt. Damit verwendet die Gruppe den gewöhnlichen gemeinsamen Floodbuilder; die
bereits vorhandenen owner- und `CanDigMoat`-geprüften Hooks entscheiden anschließend pro Unit,
wer tatsächlich einen Moatpfad erhalten darf. Gruppen, deren aktive Mitglieder sämtlich im
Moat stehen, bleiben vollständig Vanilla.

Ein zweiter belegter Fehler lag im eigenen Builder-Gate: `0x196840` liefert für eine bereits im
fertigen Moat stehende Unit korrekt `1`, aber `VanillaModeDetected == true` schloss sie bislang
vom Retry aus. Die protokollierten Aufrufe zeigten dabei `path80=1`, einen ersten Builderwert `0`
und später einen separaten Bodenbuilderwert `4`. Der Retry akzeptiert deshalb nun auch diesen
Vanilla-Modus, weiterhin erst nach echtem Vanilla-Nuller, `CanDigMoat`, `path80=1` und einer
notwendigen owner-sicheren Route. Die Diagnose bezeichnet diesen Fall als
`standing-on-moat-route80`.

Regions- und Builderfallback verlangen zusätzlich ausdrücklich einen bereits owner-qualifizierten
Plan (`FriendlyRouteQualified`), einen beobachteten Moatmodus und den aktiven globalen Moatmodus.
Damit kann ein bloßes positives Vanilla-Ergebnis von `0x196840`, etwa weil eine Unit bereits auf
einem fertigen Moat steht, keine spätere Regions- oder Builderfreigabe ohne vorherige Ownerprüfung
auslösen.

Update-Suchanker für die kanonische DLL
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`:

- `0x117BC0`, Größe 173, Raw-Hash
  `22F2A1110140E18C7337E6098937E483A98C3BF0D1DCC36BF6B33DD2EFA7FDC8`, historisch
  `0x116CE0 -> 0x117BC0` über `unique-normalized-hash-and-cfg` bestätigt;
- `0x119F90`, Größe 140, Raw-Hash
  `CD5DAE0B455BA6CBFC20D0193849591A8250C710C8B77BBDCFFF6908FBD1544F`, historisch
  `0x1190B0 -> 0x119F90` über `unique-raw-hash` bestätigt;
- `0x117BC0` besitzt in dieser Baseline nur `0x11B520` als Caller und verwendet `0x119F90` als
  einzigen Callee;
- bei Updates zuerst `0x11B520` semantisch wiederfinden, den Vergleich zwischen Leitunit und
  erster Moat-Unit sowie die drei Builderzweige bestätigen und erst dann Helper, Iterator,
  Callbytes und vollständige Detour-Entrybytes übernehmen.

Schlägt eine dieser Validierungen fehl, wird der neue funktionale Hook gemeinsam mit den
zentralen Bewegungshooks zurückgerollt. Es gibt keinen gruppenweiten Fallback und keine
Aufteilung oder erneute Ausgabe des Befehls.

### 15.9 Gebäude-Cursor: zentraler Erreichbarkeitshelper `0xB70C0`

Die bisherigen Gebäudetests trennten zwei Fälle eindeutig. Ein `AttackBuilding`-Befehl wurde
nur erzeugt, wenn wenigstens ein ausgewähltes Gruppenmitglied bereits auf der Gebäudeseite des
Moats stand. Standen alle Units auf der anderen Seite, blieb der Cursor rot und es entstand kein
Target-Command. Die funktionale Vorbereitung über den Auswahlhelper `0x196870` und die
Tilepaarprüfung `0xE2CA0` war für gewöhnliche Gebäude am falschen Unterpfad: Der eigentliche
Gebäudehelper ruft diese Funktionen während seiner eigenen Annäherungsfeldsuche erneut auf und
überschrieb beziehungsweise verbrauchte den kurzlebigen äußeren Scope. Ein beobachteter
`0x195E30`-Aufruf mit globalem Ziel `(0,0)` war ebenfalls kein belastbarer Gebäude-Stager und wird
für Gebäude nicht mehr funktional vorbereitet.

Die hashgleiche Baseline identifiziert stattdessen RVA `0xB70C0` als zentralen
Gebäude-Erreichbarkeitshelper. Seine Signatur ist

    int BuildingCursorReachability(buildingManager, buildingGameId, unitGameId)

Die Funktion enumeriert die möglichen Annäherungsfelder des Gebäude-Footprints und entscheidet,
ob der Cursor-Dispatcher das Gebäude als angreifbar behandeln darf. In der kanonischen DLL
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` besitzt sie genau einen
bestätigten Caller: den Near-Call bei RVA `0x8DFF6` im Cursor-Dispatcher `0x8C5F0`. Dessen Bytes
sind `E8 C5 90 02 00`. Das historische Gegenstück ist RVA `0xB62D0`; die Baseline bestätigt die
Zuordnung über normalisierten Funktionshash und Kontrollfluss.

`MoveMoatTest` detourt deshalb `0xB70C0` Vanilla-first. Ein positives Vanilla-Ergebnis bleibt
unverändert. Nach Vanilla `0` wird nur dann effektiv `1` geliefert, wenn alle folgenden Verträge
erfüllt sind:

- Building- und Unit-ID sind gültige 1-basierte Game-IDs;
- die Unit lebt und liefert einen gültigen kontrollierenden Spieler;
- die rohe Hover-Building-ID stimmt exakt mit dem Funktionsargument überein;
- Vanillas rohe Hover-Building-ID bindet das sichtbare Ziel exakt an das Funktionsargument; ein
  echtes StructureGrid-Kontexttile desselben Gebäudes wird nach der in Abschnitt 15.12
  dokumentierten Priorität aufgelöst;
- das Gebäude lebt, besitzt eine gültige Global-ID, ist feindlich und ist keine Mauer, Treppe
  oder Rampe;
- die gruppenweite Probe findet mindestens eine regelkonform erreichbare Unit und eine relevante
  Trennung durch eigenen oder verbündeten fertigen Moat.

Für die Routenprobe werden ausschließlich freie, gewöhnlich begehbare Außenfelder verwendet, die
unmittelbar an den verifizierten Footprint grenzen. Nicht grabfähige Units dürfen nur normale
Wege benutzen; die Moat-Variante zählt ausschließlich für `CanDigMoat`-Units. Die bestehende BFS
öffnet weder feindlichen Moat noch Wasser oder Mauern. Die Cursorentscheidung ist gruppenweit,
der spätere Planer-, Regions- und Builderfallback bleibt dagegen strikt pro konkreter Unit
gefiltert.

Die Diagnose `stage=building-cursor-reachability` bindet das Ergebnis an Kartenepoch,
Building-/Global-ID, Unit, Hover-Tile und vollständige Auswahl-/Positionssignatur. Sie protokolliert
Vanilla- und Effektivergebnis sowie Auswahlgröße, Grabfähigkeit, legal erreichbare und durch
freundlichen Moat getrennte Units. `0xDA020` und `0x123090` bleiben read-only instrumentiert; erst
ein tatsächlich erzeugter Moat-`AttackBuilding`-Befehl kann belegen, ob in der späteren
Building-Approach-Pipeline noch ein eigener Abbruch existiert.

Nach einem Spielupdate zuerst `0xB70C0` über Semantik und historischen Match wiederfinden, dann
Funktionsgrenzen, vollständige Entrybytes, den einzigen Dispatcher-Caller und dessen Callziel
erneut bestätigen. Die aktuellen Entrybytes beginnen mit:

    48 89 5C 24 08 55 56 57 41 54 41 55 41 56 41 57
    48 83 EC 40 4C 8B E1 85 D2

Ein Patterntreffer allein genügt nicht. Stimmen Hash, Signatur, 1-basierter ID-Vertrag,
Footprint-Enumeration oder Caller nicht mehr überein, bleibt die Gebäudefreigabe fail-closed auf
Vanilla.

### 15.10 Gebäude-Command: Kandidatenverbrauch in `0x123090`

Der erste Lauf mit dem funktionalen `0xB70C0`-Cursorhook belegt den anschließenden gemeinsamen
Commandpfad. `AttackBuilding` wurde ausgegeben und `0xDA020` erzeugte 30 Einträge, aber die Unit
blieb unverändert (`changedUnits=0`); weder `MoveHere` noch der zentrale Planer oder Builder wurden
erreicht. Der Abbruch liegt damit zwischen der Erzeugung der Gebäude-Annäherungsfelder und ihrer
Zuweisung an die Units.

Die hashgleiche Baseline bestätigt die Folge

    0x11E960 -> 0xDA020 -> 0x123090 -> 0x196280 (MoveHere)

`0xDA020` schreibt ab `pathManager + 0x1B344` höchstens 500 Einträge mit einer Schrittweite von
12 Bytes:

- `+0`: freies Annäherungstile;
- `+4`: unmittelbar angrenzendes Tile des Zielgebäude-Footprints;
- `+8`: Pfaddistanz beziehungsweise `10.000.000` für nicht erreichbar.

`0x123090` ruft abhängig von Auswahl und Variante `0xDA590`, `0xD9C40` oder `0xDB650` auf, bewertet
die Annäherungstiles über die globale Stamp-/Distanzkarte, sortiert die Tripel und entfernt Einträge
mit der Distanz `10.000.000`. Der normale Pfad `0xDA590` kann einen fertigen Moat nicht überqueren.
Danach prüft `0x11E960` insbesondere das Feld `+4`; nur aus einem vollständigen Tripel setzt es das
Attack-Move-Ziel, ruft `0x196280` auf und speichert anschließend `+4` als
`r_AI_ContextTargetBuildingTileId`.

Die ältere Diagnose zählte einen Eintrag bereits dann, wenn nicht sowohl `+0` als auch `+4` null
waren. Ein protokolliertes `results=30` bewies daher nicht, dass `0x11E960` auch nur einen
verwertbaren Kandidaten erhalten hatte. Die neue Diagnose trennt rohe, vollständig verwertbare und
unvollständige Tripel und gibt das erste Tripel einschließlich Distanz aus.

Der Lauf vom 3. September 2026 nach der ersten Reservierungslockerung präzisierte den Abbruch. Bei
einem Woodcutter hinter eigenem Moat erzeugte `0xDA020` 50 rohe Einträge, aber alle besaßen
`+4 == 0`. `0x123090` konnte daher kein ausführbares Tripel veröffentlichen; `0x11E960` lieferte
zwar Command-Ergebnis `1`, änderte jedoch keine Unit. Die Owner-BFS und die Reservierungsprüfung
wurden dabei überhaupt nicht erreicht. Eine nachträgliche Rekonstruktion von `+4` ist an dieser
Stelle konzeptionell falsch und wurde entfernt.

Die Baseline erklärt die Nullwerte: `0xDA020` prüft bereits während der Erzeugung jedes echten
Annäherungstiles über `0xE2610`, ob dessen Region von der Ausgangsregion erreicht werden kann. Erst
nach positiver Regionsprüfung schreibt der erste Enumerationspfad `+0` und sucht über vier
kardinale Nachbarn ein StructureGrid-Tile des Zielgebäudes für `+4`. Da `0xE2610` fertige Moats
blockiert, verwirft Vanilla genau diese vollständigen Kandidaten vorzeitig. Die später erzeugten
Ersatzkandidaten dürfen dagegen absichtlich `+4 == 0` besitzen und werden von `0x11E960` nicht als
normale Gebäudeannäherung ausgeführt.

Der funktionale Eingriff beginnt deshalb nun Vanilla-first im bereits detourten `0xE2610`, aber
ausschließlich innerhalb des synchronen, exakt an Command, Tribe, Gebäude und Kartenepoch
gebundenen `BuildingApproach`-Scopes von `0xDA020`. Ein negatives Regionspaar wird nur geöffnet,
wenn mindestens eine lebende grabfähige Command-Unit ein zum Zielgebäude gehörendes
Annäherungstile dieser Zielregion ausschließlich über eigenen oder verbündeten fertigen Moat
erreicht. Danach erzeugt Vanilla selbst vollständige `+0/+4`-Paare. `0x123090` bleibt Vanilla-first;
sein nachgelagerter Fallback darf lediglich solche vollständigen Vanilla-Paare nach dem erwarteten
Nuller des Bodenbuilders owner-geprüft wieder veröffentlichen. `0xDA590` und `0xD9C40` werden nicht
zusätzlich detourt.

Für `+4` ist die StructureGrid-ID des exakt validierten Zielgebäudes zusammen mit Vanillas Maske
`tileFlags & 0x0F000000 == 0` maßgeblich. Die Building-Record-Boundingbox ist hierfür nicht
maßgeblich, da Gebäude auch außerhalb dieser kleineren Rechtecke gültige Kontext- und
Reservierungstiles besitzen können. Ein Annäherungstile mit StructureGrid-ID ungleich null bleibt
nur dann als begehbares reserviertes Endtile zulässig, wenn das native
Occupancy-/Bewegungsmaskenbyte bei RVA `0x51890D0` ungleich null ist. Falls die vereinfachte
Owner-BFS dieses reservierte Endtile nicht selbst betritt, darf ausschließlich der letzte Übergang
von einem bereits owner-qualifiziert erreichten Nachbartile anhand der von `0xDA590` verwendeten
gerichteten Bewegungsmaske bestätigt werden. Reservierte Tiles werden dadurch nicht allgemein als
Zwischenfelder geöffnet.

Die Diagnose `building-approach-region-fallback` hält das tatsächlich geöffnete Regionspaar und
den Ownerbefund fest. `building-consumer-fallback` trennt fehlende Kontexttiles, ungültige
Kontextpaare, blockierte beziehungsweise begehbare Reservierungen und erst in der Owner-Routenprobe
verworfene Kandidaten. Dadurch ist bei einem Update sofort erkennbar, ob sich die
`0xDA020`-Erzeugung, der 12-Byte-Vertrag oder die native Reservierungssemantik geändert hat.

Eine zweite bestätigte Besonderheit betrifft den Unitzustand: `0x11E960` setzt bei diesem
Gebäudepfad `r_AI_LastIssuedTribeCommand` nicht auf `AttackBuilding`; in den Logs erscheint dort
korrekt `Unknown0`. Zudem wird `r_AI_ContextTargetBuildingTileId` erst nach dem synchronen
`MoveHere`-Aufruf geschrieben. Der Movement-Scope muss während `MoveHere` deshalb das bereits
gesetzte Attack-Move-Tile gegen die von `0x123090` veröffentlichte Zuordnung
`Annäherungstile -> Footprint-Tile` prüfen. Erst nach der Rückkehr darf der gespeicherte
Building-Kontext als zusätzliche Bestätigung verwendet werden. `AttackUnit` behält dagegen seine
bisherige Prüfung von LastIssued-Command sowie Unit-ID und Global-ID.

Bei einem Spielupdate sind neben den Entrybytes und Callsites besonders die Pufferbasis
`pathManager + 0x1B344`, die Schrittweite `0x0C`, die drei Feldbedeutungen, der Unerreichbarwert
`0x989680` und die Reihenfolge der Feldzuweisungen um den `MoveHere`-Call erneut zu bestätigen.
Weicht einer dieser Verträge ab, bleibt der Gebäude-Consumerfallback deaktiviert.

### 15.11 Gebäude-Annäherung nach dem ersten funktionalen Lauf

Der erste Lauf mit der `BuildingApproach`-Regionsfreigabe bestätigte den vollständigen Ablauf:
`0xDA020` erzeugte zwölf vollständige Paare, der Consumerfallback veröffentlichte alle zwölf,
`0x11E960` rief `MoveHere` auf und der zentrale Builder erzeugte einen realen Pfad. Die Unit lief
zum Gebäude und griff es an.

Dabei wurde ein reiner Sortierfehler des Testmods sichtbar. Nach dem Nuller des Vanilla-Builders
überschrieb der Fallback die Distanzfelder mit `1, 2, 3, ...` in der ursprünglichen
Enumerationsreihenfolge von `0xDA020`. Diese Reihenfolge beschreibt die Gebäudegeometrie, nicht die
Entfernung von der angreifenden Unit. Deshalb wählte `0x11E960` beispielsweise das erste Paar mit
Annäherungsziel `(405,356)`, obwohl ein kürzerer Zugang auf der Vorderseite des Gebäudes existierte.
Vanilla ohne Moat zeigt den Fehler nicht, weil `0x123090` die echte Flood-Distanz in `+8` schreibt
und danach aufsteigend sortiert.

Die owner-sichere BFS führt deshalb zusätzlich getrennte Distanzen für Zustände ohne und nach
Benutzung eines freundlichen Moats. Für jedes vollständige Vanilla-Paar wird die kürzeste legale
Distanz aller beteiligten grabfähigen Units als `+8` veröffentlicht. Bei begehbaren reservierten
Endtiles zählt der abschließende, über Vanillas gerichtete Bewegungsmaske bestätigte Übergang als
ein weiterer Schritt. Die Kandidaten werden stabil nach Distanz und bei Gleichstand nach ihrer
ursprünglichen Vanilla-Reihenfolge sortiert. Erreichbarkeit, Ownerfilter und die Menge zulässiger
Tiles ändern sich dadurch nicht; ausschließlich die zuvor künstliche Kandidatenpriorität wird an
Vanillas Semantik angenähert.

### 15.12 Gebäude-Spriteüberhang und stabiles Hover-Kontexttile

Der anschließende Vergleich des Angriffscursors über die gesamte sichtbare Gebäudegrafik zeigte
einen weiteren, vom Pfadbau unabhängigen Randfall. Auf den unteren beziehungsweise tatsächlich vom
StructureGrid belegten Bildbereichen war der Cursor freigegeben; auf Teilen des Dachs und des
rechten oder oberen Spriteüberhangs blieb er dagegen verboten. Die Grenze war nicht horizontal
verschoben und entsprach nicht der sichtbaren Sprite-Hitbox.

Die Logs vom 3. September 2026 bestätigen die Trennung der beiden Ebenen. Auch über den roten
Bildbereichen lieferte Vanillas Sprite-Hit-Test weiterhin exakt dieselbe 1-basierte
`r_HoverOverBuildingId`. Dagegen waren `r_HoverOverBuildingTileId` und `r_MouseTileId2` null, das
globale Cursorziel blieb `(0,0)`, und `r_MouseTileId` bezeichnete das unter dem überhängenden
Sprite liegende Geländetile. Der Cursor-Dispatcher `0x8C5F0` erreichte den einzigen Call
`0x8DFF6 -> 0xB70C0` dennoch; nur der Mod verwarf anschließend den Fallback, weil er bislang
verlangte, dass das rohe Maus-Tile selbst zum StructureGrid-Footprint des Gebäudes gehört.

Für die visuelle Zielidentität ist deshalb `r_HoverOverBuildingId` maßgeblich. Nach erneuter
Validierung von Game-ID, Alive-State, Global-ID, Besitzer, Feindschaft und Gebäudetyp wird ein
stabiles echtes Gebäudekontexttile gesucht. Die Priorität lautet:

1. gültiges `r_HoverOverBuildingTileId` desselben Gebäudes;
2. gültiges `r_MouseTileId2` desselben Gebäudes;
3. gültiges `r_MouseTileId` desselben Gebäudes;
4. das dem validierten Maus-Tile nächstgelegene StructureGrid-Tile mit exakt derselben Building-ID
   innerhalb der Building-Bounds, bei Distanzgleichstand die kleinere Tile-ID.

Der vierte Fall erweitert die Hitbox nicht selbst: Er ist nur zulässig, solange Vanilla über dem
aktuellen Bildschirmpixel die konkrete rohe Building-ID meldet. Das gefundene Tile dient außerdem
nur als stabiler Identitäts- und Diagnosekontext. Die echten Annäherungsfelder werden weiterhin
vollständig aus dem verifizierten Gebäude-Footprint enumeriert und anschließend owner-sicher
bewertet. Die Auswahl des kürzesten Angriffsziels aus Abschnitt 15.11 bleibt dadurch unverändert.

Die Diagnose `building-cursor-reachability` protokolliert die drei rohen Tilefelder, das
aufgelöste Kontexttile und `hoverTileSource=buildingTile|mouse2|mouse|nearest-footprint`. Der
Auswahlhelper verwendet denselben Resolver, schreibt jedoch weder die globalen Cursorzielwerte
noch öffnet er einen zusätzlichen Dispatcherzweig. Positive Vanilla-Ergebnisse bleiben
unangetastet; eigene und verbündete Gebäude sowie Mauern, Treppen und Rampen werden weiterhin
nicht durch diesen Gebäudefallback freigegeben.

Für Spielupdates sind neben dem Hash
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` erneut die Strukturfelder
des `GameCursorManager`, die 1-basierte Building-ID-Semantik, der Dispatcher-Call
`0x8DFF6 -> 0xB70C0` und die StructureGrid-Zuordnung zu prüfen. Stimmen Sprite-Hover-ID und
StructureGrid-Vertrag nicht mehr überein, bleibt dieser Fallback fail-closed.

Der erste Test dieses Resolvers zeigte eine weitere Inkonsistenz der Cursorfelder. Beim Übergang
vom gültigen zum roten Spritebereich blieb `r_HoverOverBuildingId=1` und
`r_MouseTileId=125319`, während die separaten `r_MouseTileX/Y` beziehungsweise das globale
Cursorziel `(0,0)` enthielten. Die erste Umsetzung verlangte irrtümlich, dass diese X/Y-Werte
wieder genau `r_MouseTileId` ergeben, und verwarf deshalb gerade den vorgesehenen
`nearest-footprint`-Fall. Maßgeblich ist nun das validierte Tile: Seine Koordinaten werden über
`GetTileVectorFromId(r_MouseTileId)` rekonstruiert. Rohe X/Y-Werte dienen nur als Fallback, wenn
sie selbst wieder ein gültiges Tile ergeben. Die Diagnose protokolliert beide Quellen, damit diese
Priorität nach einem Spielupdate erneut geprüft werden kann.

### 15.13 Performance des Gebäude-Consumers bei großen KI-Gruppen

Ein KI-Schnellvorlauf vom 3. September 2026 zeigte reproduzierbare synchrone Pausen im
`0x123090`-Detour. Zwischen der abgeschlossenen `BuildingApproach`-Diagnose und dem Ergebnis des
Consumers lagen bei 5, 11, 20 und 27 grabfähigen Units ungefähr 409, 903, 1.631 und 2.191 ms.
Die nahezu linearen rund 80 ms pro Unit entsprechen der bisherigen verschachtelten Auswertung:
16 vollständige Gebäude-Kandidaten wurden außen und alle Units innen durchlaufen. Da
`EnsureReachabilityMap` absichtlich nur eine Karte hält und sein Schlüssel die Unit enthält,
verdrängte jede innere Unit die vorherige Karte. Beim nächsten Kandidaten begann dieselbe Folge
erneut; 27 Units mal 16 Kandidaten ergaben bis zu 432 BFS-Aufbauten.

Die Auswertung läuft deshalb nun unitweise. Für eine Unit werden die Kandidaten nur zur
Berechnung nach Zielregion gruppiert und gegen dieselbe Reachability-Generation geprüft; die
veröffentlichte Reihenfolge wird weiterhin ausschließlich aus echter Distanz und ursprünglicher
Vanilla-Reihenfolge bestimmt. Semantik, Ownerfilter und Kandidatenmenge bleiben unverändert. Der
Moat-Owner eines Tiles wird zusätzlich nur für die Dauer dieses synchronen Consumer-Aufrufs
memoisiert. Der Cache wird weder zwischen Commands noch über einen Kartenwechsel hinweg benutzt.

`building-consumer-performance` misst Vanilla und den Modfallback getrennt und nennt rohe sowie
gültige Kandidaten, Gräber, Kandidatenprüfungen, tatsächlich aufgebaute Reachability-Karten,
Cachetreffer und Moat-Owner-Cachetreffer. Bei einer gemeinsamen Zielregion werden damit ungefähr
eine Karte pro Unit statt eine Karte pro Unit und Kandidat erwartet. Bleibt danach eine Pause in
der separat ausgewiesenen Vanilla-Zeit, muss `0x123090` selbst erneut untersucht werden; sie darf
nicht durch einen ungeprüften globalen Bypass verdeckt werden.

### 15.14 Native Tile-Anzahl und Performance der Gebäude-Approach-Suche

Eine anschließende Codekontrolle deckte in der funktionalen `BuildingApproach`-Freigabe einen
gefährlichen Größenfehler auf. Der rechteckige Koordinatenraum ist `800 * 800 = 640.000` Zellen
groß; die nativen tile-indizierten Arrays wie PathRegionGrid, StructureGrid und TileFlags besitzen
dagegen nur `0x4E520 = 320.800` Einträge. Die hashgleiche Baseline verwendet `0x4E520` an den
Initialisierungs-, Serialisierungs- und Tilezugriffsstellen durchgängig. Die alte Schleife lief bis
640.000 und las `pathRegionGrid[tileId]` sogar vor der Gültigkeitsprüfung. Damit waren ungefähr
319.200 native Reads außerhalb des bestätigten Arrays möglich. Neben unnötiger Arbeit konnte dies
falsche Regionsübereinstimmungen oder einen Crash verursachen.

Der Code trennt deshalb nun ausdrücklich:

- `MapCellCount = 640.000` ausschließlich für die verwalteten BFS-Zustände, die per `(y * 800) + x`
  adressiert werden;
- `NativeTileCount = 0x4E520` für jede Iteration oder Gültigkeitsprüfung einer nativen Tile-ID.

Die Approach-Freigabe durchsucht außerdem nicht mehr für jedes von `0xDA020` angefragte
Regionspaar erneut alle Tiles. Innerhalb genau eines synchronen `0xDA020`-Aufrufs wird einmal ein
Index des Zielgebäudes erzeugt: StructureGrid-Tiles mit passender 1-basierter Building-ID und
Vanillas Kontextflagprüfung werden erfasst, anschließend werden ihre vier kardinalen Nachbarn mit
der bestehenden Endpoint- und Reservierungsprüfung nach PathRegion gruppiert. Diese Konstruktion
entspricht dem in der Baseline sichtbaren Vier-Nachbarn-Vertrag von `0xDA020`. Sie verwendet
bewusst nicht nur die kleinere Boundingbox des Building-Records, weil gültige begehbare
Reservierungen außerhalb dieser Bounds bereits praktisch bestätigt wurden.

Auch diese Regionsprobe läuft unitweise: Alle Annäherungstiles derselben Zielregion werden für
eine Unit geprüft, bevor zur nächsten Unit gewechselt wird. Damit bleibt der Eintrag in
`EnsureReachabilityMap` erhalten und die frühere Kandidat-mal-Unit-Verdrängung entsteht nicht noch
einmal im vorgelagerten `0xDA020`-Fallback.

`building-approach-performance` misst nun den gesamten beobachteten `0xDA020`-Aufruf, die darin
verbrauchte Zeit der owner-geprüften Regionsfallbacks, Aufbauzeit und Größe des einmaligen Indexes
sowie Reachability- und Moat-Owner-Cachetreffer. `vanillaEstimatedMs` ist lediglich `totalMs` minus
der gemessenen Fallbackzeit und deshalb als Schätzung gekennzeichnet; der Detour kann Vanillas Zeit
und die synchron darin aufgerufenen Observer nicht anderweitig trennen. Alle Approach- und
Consumer-Performancezustände sind threadlokal und werden nach dem jeweiligen nativen Aufruf
verworfen.

### 15.15 Gebäudeangriff bei einem Start auf fertigem Moat

Der erste Test nach der Performancekorrektur bestätigte einen letzten getrennten Gebäude-Approach-
Fall. Zwei Gebäudeangriffe vom normalen Boden erzeugten vollständige Kandidaten, qualifizierten den
owner-sicheren Builder und bewegten die Unit. Ein unmittelbar danach erteilter dritter Befehl traf
die Unit dagegen auf dem fertigen Moat-Tile `(386,381)`. `0xDA020` erhielt dabei
`sourceRegion=0`, erzeugte nur 50 Einträge ohne Gebäudekontext und aktualisierte keine Unit.

`sourceRegion=0` ist für diesen Zustand kein ungültiger Parameter: Die Baseline zeigt, dass
`0xDA020` die übergebene Ausgangsregion direkt mit der Kandidatenregion vergleicht beziehungsweise
an `0xE2610` weiterreicht. Fertige Moat-Tiles besitzen im bestätigten Aufbau keine normale
PathRegion. Die Modprüfung hatte Null jedoch pauschal durch
`sourceRegion <= 0` ausgeschlossen.

Der Building-Approach-Fallback akzeptiert Null deshalb nur unter den folgenden gemeinsam
erforderlichen Bedingungen:

- Die konkrete Command-Unit lebt, gehört zum passenden Tribe und besteht `CanDigMoat`.
- Ihr aktuelles Tile besitzt das fertige Moat-Bit.
- `GetMoatIdAtTile` liefert einen gültigen Record, dessen Besitzer die Unit kontrolliert oder mit
  ihrem Spieler verbündet ist.
- Die bereits bestehende Zielprüfung findet anschließend weiterhin eine notwendige Route durch
  ausschließlich freundlichen fertigen Moat zu einem echten Gebäudeannäherungstile.

Ein regionsloser Start auf normalem Gelände, feindlichem Moat oder mit unvollständigen Daten bleibt
Vanilla. Der positive Diagnosegrund lautet `required-friendly-moat-route-from-moat`; die Ablehnung
ohne passende Unit lautet `no-friendly-moat-source-digger`. Es wird weder ein neuer Hook noch ein
besonderer Gebäude-Builder benötigt.

