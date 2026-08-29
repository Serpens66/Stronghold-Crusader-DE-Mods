# Moat-Verhalten von Units: Vanilla-Reverse-Engineering und Testergebnisse

Stand: 2026-08-30

Thema: geplante und fertige Moats, Command 6 (`DigMoatTileId`), Cursorprüfung, Unit-Auswahl, Wegfindung, Reservierung und bestehende Bewegungsaufträge

## 1. Zweck und Status dieses Dokuments

Dieses Dokument bündelt die bislang über Quellcode, Disassemblierung, BepInEx-Logs und Ingame-Tests verteilten Erkenntnisse zum Graben von Moats. Es soll einem neuen Chat oder Entwickler ermöglichen, an diesem Feature oder an verwandten Moat-Features weiterzuarbeiten, ohne die bereits untersuchten Vanilla-Pfade erneut von Grund auf suchen zu müssen.

Wichtig ist die Trennung zwischen:

- **nachgewiesenen Fakten** aus der kanonischen DLL, validierten Hooks oder Logs;
- **aktuellen Implementierungsentscheidungen** in `MoatCommandTest/src/MoatDiggingReachabilityFix.cs`;
- **noch offenen Fragen** und geplanten, aber noch nicht umgesetzten Änderungen.

Der aktuelle funktionale Stand ist:

- Ein menschlicher Spieler kann einen grundsätzlich unerreichbaren freundlichen geplanten Moat anklicken; die Cursorprüfung wird dafür gezielt freigegeben.
- Das exakte befohlene Moat-Ziel kann bis zur Moat-State-Machine erhalten und ausgewählt werden.
- Der nachfolgende Vanilla-Tile-Path-Builder kann nach einem eng begrenzten Regions-BFS-Bypass einen echten Pfad durch fertige Moats bauen.
- Units können dadurch einen fertigen Moat überqueren und am befohlenen geplanten Moat graben.
- Ein bereits laufender normaler Bewegungsauftrag wird derzeit jedoch nicht beim Erteilen des Moat-Befehls beendet. Die Unit beendet erst den alten Auftrag und bearbeitet danach den Moat-Auftrag. Die bisherige späte Verwendung von Vanillas Pfadabbruchhelfer ist funktional wirksam, wird aber erst bei der späteren Moat-Auswahl erreicht und ist deshalb zu spät.
- Die grünen Zielmarker bleiben dadurch ebenfalls am alten Bewegungsziel beziehungsweise an Zuständen des alten Pfads. Das ist kein isolierter Renderfehler.

Die nächste fachlich abgesicherte Änderung ist am Ende dieses Dokuments beschrieben, aber zum Stand dieses Dokuments noch **nicht** umgesetzt.

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

Es ist **nicht** abschließend nachgewiesen, ob fertige Moat-Tiles für alle interessanten Systeme noch eine sinnvoll auswertbare Besitzerzuordnung besitzen. Die bisherige Lösung verändert deshalb nicht die allgemeine Passierbarkeit eigener oder verbündeter fertiger Moats.

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
- Es ist nicht erforderlich und wäre riskanter, allgemeine Bewegungsbefehle oder die gesamte Moat-Passierbarkeit zu verändern.
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

## 8. Abgesicherte nächste Korrektur

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

## 9. Verworfene oder widerlegte Ansätze

### 9.1 Globale Passierbarkeit eigener/verbündeter fertiger Moats

Nicht umgesetzt. Zwar lässt Vanillas Moat-Pfadmodus fertige Moats über Bit 30 passieren, aber eine allgemeine Ownership-basierte Passierbarkeit für jeden Bewegungsbefehl wäre eine wesentlich breitere Gameplayänderung. Zudem ist noch nicht abschließend belegt, wie Besitzerinformationen fertiger Moats in allen relevanten Pfadphasen vorliegen.

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
4. Welche belastbare Besitzerinformation bleibt an einem **fertigen** Moat erhalten, und berücksichtigt irgendein allgemeiner Vanilla-Pfadfinder diese Ownership? Das ist für mögliche spätere Features zur allgemeinen freundlichen Moat-Passierbarkeit gesondert zu untersuchen.
5. Welche genaue Semantik besitzt das vom Resethelfer genullte Feld `GameUnit + ungefähr 0x2CE`? Für die Verwendung des Vanilla-Helfers ist eine Benennung nicht erforderlich, für andere Bewegungsfeatures kann sie relevant werden.
6. Welche sichtbaren Markerfelder werden nach dem frühen Pfadabbruch exakt aktualisiert? Erwartung ist, dass Vanilla sie mit dem realen Pfadwechsel selbst korrigiert; dies muss ingame bestätigt werden.

## 11. Testmatrix für weitere Änderungen

Nach einer Änderung am frühen Reset oder an verwandten Moat-Pfaden sollten mindestens folgende Fälle getrennt getestet werden:

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

### 12.2 Logauswertung

Das BepInEx-Log wird angehängt. Jeder neue Spielstart beginnt mit:

`[Message:   BepInEx] BepInEx 5.4.23.2 - Stronghold Crusader Definitive Edition`

Die Uhrzeit dieser BepInEx-Zeile ist nicht zuverlässig. Für die zeitliche Zuordnung die eigenen Modlogs mit Millisekunden verwenden. Die aktuelle Logpräfixfolge lautet `Moat Command Test MoatCommand`; relevante Einträge lassen sich zusätzlich über `stage=` beziehungsweise die konkreten Stages filtern:

- `direct-command`
- `selection`
- `bfs-result`
- `path-builder-result`
- historisch `post-shortening`

Ein fehlender späterer Logeintrag darf nicht sofort als fehlender nativer Funktionsaufruf interpretiert werden. Zuerst prüfen, ob Attempt-ID, globale Zielwerte oder Current-Unit-Korrelation den Callback herausfiltern.

## 13. Appendix: direkte CALL-Sites auf `0x197950`

Für die kanonische DLL wurden folgende 85 direkten Aufrufstellen gefunden:

`0xCD2A2`, `0xCD499`, `0x1150A3`, `0x11EB13`, `0x11EC5E`, `0x11ECEE`, `0x11F2E8`, `0x11F390`, `0x11F9BB`, `0x11FBFB`, `0x11FE5C`, `0x1204FE`, `0x121946`, `0x123E29`, `0x12BBDE`, `0x12D207`, `0x12E3D6`, `0x1313FD`, `0x131980`, `0x133284`, `0x133DBE`, `0x134EF0`, `0x135E0A`, `0x136D0D`, `0x137793`, `0x13881E`, `0x13992B`, `0x13AAC2`, `0x13BDA5`, `0x13CEFD`, `0x13E080`, `0x13F52D`, `0x14B75B`, `0x14DA97`, `0x14E4B3`, `0x14EC90`, `0x14ECEA`, `0x14F397`, `0x1501A2`, `0x1505A7`, `0x151067`, `0x155C35`, `0x1560FF`, `0x156F92`, `0x1575B6`, `0x158519`, `0x158BC1`, `0x158D9A`, `0x15917B`, `0x159C0D`, `0x15A64B`, `0x15A942`, `0x15B17F`, `0x15B1CD`, `0x15B2FB`, `0x15B64F`, `0x15D956`, `0x15D9A4`, `0x15DF69`, `0x15E542`, `0x15E58B`, `0x15ECAC`, `0x1603E6`, `0x1634CE`, `0x16364E`, `0x163B95`, `0x163EB4`, `0x163EFC`, `0x163FA4`, `0x1640F0`, `0x164336`, `0x166205`, `0x166959`, `0x166F9F`, `0x167368`, `0x1679E1`, `0x183F02`, `0x183F93`, `0x185D64`, `0x185DC4`, `0x185F00`, `0x185F11`, `0x193EDB`, `0x194965`, `0x19777E`.

Diese Liste belegt die breite Verwendung des Helpers, ersetzt aber bei einer neuen Hookentscheidung nicht die Analyse des jeweiligen Callsite-Kontexts.

## 14. Kurzfassung für die Weiterarbeit

- Nicht bei Cursor, Renderer oder allgemeiner Moat-Passierbarkeit neu anfangen: Diese Schichten sind bereits getrennt untersucht.
- Der Command erreicht inzwischen das exakte freundliche geplante Moat-Ziel.
- Der begrenzte BFS-Bypass ist nötig, um Vanillas echten und erfolgreichen Builder zu erreichen; dessen Ergebnis bleibt unverändert.
- Die aktuelle Restursache liegt im Zeitpunkt des Pfadabbruchs: `0x197950` wird in der späteren Moat-Auswahl erst nach Abschluss des alten Auftrags erreicht.
- Der nächste sichere Ansatz ist der bereits Vanilla-gefilterte per-Unit-Zweig `0x120F7A`, mit Unit-ID `RDX` und Ziel `R14/R15`.
- Dort nur nach erneuter freundlicher Planned-Moat-Prüfung den Vanilla-Helfer `0x197950` aufrufen.
- Den späten Helper-Aufruf und den erfolglosen Post-Shortening-Diagnosehook anschließend entfernen.
- Gemischte Truppen weiter vollständig Vanillas Sprungtabelle überlassen.

