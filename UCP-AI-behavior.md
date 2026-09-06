# UCP „AIV Troop Behaviour“ in Stronghold Crusader Definitive Edition

## Zweck und Kurzfazit

Dieses Dokument prüft, welche Teile der UCP3-Erweiterung **AIV Troop Behaviour 0.2.1** in Stronghold Crusader Definitive Edition (SHCDE) noch relevant sind und wie sie als BepInEx-Mod mit dem SHCDE Script Extender umgesetzt werden könnten.

Die wichtigste Erkenntnis ist eindeutig: **Der eigentliche UCP-Bugfix wird auch in der aktuellen DE-Fassung benötigt.** Die DE enthält weiterhin dieselbe Ausschlussmaske, durch die AIV-Verteidigungspositionen für Pikeniere, europäische Schwertkämpfer und arabische Schwertkämpfer ignoriert werden. Auch die nativen Abläufe, auf denen die optionalen UCP-Verhaltensweisen für Starttruppen, Verteidigungsgruppen und Patrouillen beruhen, sind in der DE noch vorhanden.

Die optionalen Funktionen sind technisch grundsätzlich portierbar, benötigen jedoch teilweise neue native Context-Hooks. Die öffentlichen Tribe-APIs des Script Extenders sind nützlich für Diagnosen und Prototypen, bilden aber nicht alle erforderlichen Eingriffspunkte robust genug ab.

## Übergabehinweis für einen neuen Chat

### Aktueller Arbeitsstand

Dieses Dokument ist eine **abgeschlossene Machbarkeits- und Native-Analyse, noch keine Implementierung**. Zum Zeitpunkt der Erstellung gilt:

- Es wurde noch kein AIV-Troop-Behaviour-Code in einen DE-Mod eingebaut.
- In `BugfixesAndQoL`, `ExtraFeatures`, `AIDefense` und dem kanonischen `shcde-script-extender` wurden für diese Untersuchung keine Dateien verändert.
- Es wurde kein Build ausgeführt und keine Testversion in das Spiel installiert.
- Der unmittelbar umsetzbare erste Arbeitsschritt ist der isolierte Sechs-Byte-Grundfix aus Abschnitt 2.
- Die dynamischen Features dürfen erst nach den jeweils genannten Register-/Datenflussprüfungen implementiert werden.
- Die Beduinen-Unterstützung bleibt gesperrt, bis der Test aus Abschnitt 7 ein eindeutiges Mapping liefert.

Ein neuer Chat sollte zuerst die `AGENTS.md` im Workspace lesen und danach dieses Dokument vollständig durcharbeiten. Bei Widersprüchen haben die aktuelle `AGENTS.md`, die aktuelle kanonische DLL und die aktuelle Native Baseline Vorrang vor den hier festgehaltenen Adressen.

### Zielbild und bereits getroffene Architekturentscheidung

Es gibt zwei voneinander getrennte Ausbaustufen:

1. **Nur der nachgewiesene Grundfix:** als Feature in `BugfixesAndQoL` implementieren.
2. **Komplette konfigurierbare UCP-Portierung:** als eigener Mod `AIVTroopBehaviour` mit `NetworkMode=1` implementieren; dieser besitzt alle dynamischen Hooks selbst.

Die dynamischen Hooks werden nicht zwischen `BugfixesAndQoL` und `ExtraFeatures` verteilt. `AIDefense` ist kein Zielmodul für diese Portierung. Der kanonische lokale Script Extender darf nicht verändert werden. Sollte dort eine zusätzliche öffentliche API sinnvoll erscheinen, ist stattdessen ein kurzer englischer Upstream-Report zu verfassen.

### Begriffe

- **AIV:** Burgenbauplan einer KI. Seine Misc-Einträge enthalten unter anderem bis zu zehn vorgesehene Verteidigungspositionen je Truppenreihe.
- **AIC/BAIC:** KI-Charakterprofil mit Wirtschafts-, Angriffs- und Verteidigungsparametern. `InternalAIC` ist in DE eine feste native Struktur.
- **Reihe/Row:** Index, über den ein Einheitentyp mit den für ihn vorgesehenen AIV-Positionen verbunden wird.
- **Tribe:** Native Einheitengruppe, über die die KI gemeinsame Rollen, Ziele und Bewegungsbefehle verwaltet.
- **Starttruppe:** Bereits beim Kartenstart oder durch das Szenario vorhandene, noch nicht von der KI eingruppierte Einheit. Das ist von später regulär rekrutierten Truppen zu unterscheiden.
- **Hold:** Jede Verteidigungsgruppe bleibt ihrer eigenen AIV-Position zugeordnet; Kampfreaktionen werden nicht deaktiviert.
- **Patrol:** Verteidigungsgruppen wechseln zeitgesteuert zwischen vorgesehenen AIV-Positionen.
- **RVA:** Adresse relativ zur geladenen Basis der `CrusaderDE.dll`; nur zusammen mit dem passenden vollständigen DLL-Hash verwendbar.
- **Fail-closed:** Bei unbekannter DLL, uneindeutigem Pattern, falschen Originalbytes oder Hookkonflikt wird das Feature deaktiviert, statt eine unsichere Änderung vorzunehmen.

## Untersuchte Versionen und Quellen

### Definitive Edition

- Kanonische Binärdatei: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Der Hash der installierten DLL stimmt mit `_inspect/CrusaderDE-Native-Baseline/CURRENT.json` überein.
- Alle hier genannten RVAs und Maschinenbytes gelten ausschließlich für diesen Hash.
- Verwendeter lokaler Script-Extender-Vertrag: 2.0.2.

### UCP-Referenz

- Lokale Quelle: `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\UCP_aiv-troops-behaviour-0.2.1`
- Untersuchte Erweiterung: AIV Troop Behaviour 0.2.1 für das klassische Stronghold Crusader/UCP3.
- Die UCP-Erweiterung selbst ist nicht direkt mit SHCDE kompatibel; ihre Regeln und nativen Eingriffsideen dienen hier als funktionale Referenz.

Für einen erneuten Abgleich sind in der UCP-Quelle besonders relevant:

- `README.md` und `description.md`: Funktionsbeschreibung und Grenzen;
- `options.yml`: angebotene Benutzeroptionen;
- `behavior/policy.lua`: Prioritäts- und Fallbackregeln;
- `behavior/game.lua`: native UCP-Patches und Hooks;
- `behavior/aic.lua`: zusätzliche AIC-artige Werte und Auflösung;
- `behavior/init.lua` sowie `init.lua`: Initialisierung und Verdrahtung.

### Relevante Workspace-Dateien

- `_inspect/CrusaderDE-Native-Baseline/CURRENT.md` und `CURRENT.json`: alleiniger Einstieg in die aktuelle Native Baseline;
- `_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1`: bevorzugte Nur-Lese-Abfrage für native Funktionen und Callgraphs;
- `ActiveAIVDetector/src/ActiveAIVDetectionRuntime.cs`: bereits vorhandener Entry-Detour von `c_game_aiv_prepare_layout` und Referenz für dessen Auflösung;
- `Shared/NativePatternResolver.cs`: gemeinsame Patternauflösung für Modcode;
- `BugfixesAndQoL/src/AssassinPathReconstructionPatch.cs`: Beispiel für einen validierten Sechs-Byte-NOP-Patch;
- `BugfixesAndQoL/src/AssemblyPointPlacementPatch.cs`: weiteres Beispiel für Originalbyteprüfung, Speicherschutz und Patchverwaltung;
- `BugfixesAndQoL/src/FriendlyMoatMovementRuntime.cs`: lokale Rekonstruktion der DE-fähigen Grabenarbeiter und der Grabenbewegung;
- `AIDefense/src/AIDefenseTribeUnassignAdapter.cs`: dokumentierter direkter Adapter für den in Script Extender 2.0.2 fehlerhaften `UnassignUnit`-Wrapper;
- `ExtraFeatures/src/ExtraFeaturesPlugin.cs`: bewährtes SHCDE-Lifecycle-Muster für eine nach dem Startup-Cleanup weiterlebende Runtime;
- `Shared/PresetLobbyModSettingsViewModel.cs` und `Shared/SerpLocalization.cs`: vorgeschriebene Basis für neue Lobby-Modsettings;
- `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/VanillaAIV`: untersuchter AIVJSON-Bestand;
- `AIVParser/AIVParser.Core/AivCatalogs.cs`: derzeitige lesbare Zuordnung der AIV-Misc-Typen.

### Reproduzierbarer Präflight

Vor jeder weiteren nativen Arbeit aus dem Workspace-Root ausführen:

    Get-Content -LiteralPath '_inspect\CrusaderDE-Native-Baseline\CURRENT.md'
    Get-Content -LiteralPath '_inspect\CrusaderDE-Native-Baseline\CURRENT.json'
    Get-FileHash -LiteralPath 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll' -Algorithm SHA256

Nur wenn alle vollständigen Hashwerte übereinstimmen, dürfen die RVAs dieses Dokuments als aktuelle Evidenz verwendet werden. Die zentralen Funktionen lassen sich dann reproduzierbar abfragen:

    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x53D00
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x29520
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x291F0
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x29360
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x2D760
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x2BC40
    & '_inspect\CrusaderDE-Native-Baseline\tools\semantic\query.ps1' function 0x3E850

Die Baseline führt mehrere dieser Funktionen noch unter automatisch erzeugten `FUN_...`-Namen und mit Vertrauensstufe `candidate`. Die in diesem Dokument verwendeten Bedeutungen beruhen auf Pseudocode, Datenzugriffen und Callgraph. Sie dürfen bei einer Implementierung präziser benannt werden, aber nicht ohne neue Evidenz als bereits upstream bestätigte APIs dargestellt werden.

### Vertrauenskennzeichnung

- **Bestätigt:** direkt durch aktuellen DE-Code, exakte Maschinenbytes, aktuelle Strukturen oder vorhandenen Script-Extender-Code belegt.
- **Starke Zuordnung:** Kontrollfluss und Datenzugriffe passen eindeutig zum erwarteten Verhalten, aber Name oder Bedeutung ist noch nicht durch einen Laufzeittest bestätigt.
- **Offen:** vor einer produktiven Aktivierung ist ein gezielter Laufzeittest oder eine weitergehende Register-/Datenflussanalyse erforderlich.

## 1. Was für die Definitive Edition sinnvoll ist

| Funktion | Bewertung für DE | Priorität | Implementierbarkeit |
| --- | --- | --- | --- |
| AIV-Positionen für Pikeniere und Schwertkämpfer wieder aktivieren | **Notwendiger Bugfix** | Sehr hoch | Direkt als kleiner Native-Patch |
| Start-/Szenariotruppen nach AIV-Verteidigungspositionen schicken | Sinnvolle optionale KI-Verbesserung | Hoch | Native Entscheidung hooken |
| Grabenfähige Starttruppen wahlweise als Grabenarbeiter einsetzen | Sinnvolle optionale KI-Variante | Mittel bis hoch | Native Entscheidung hooken |
| Verteidiger ihre individuelle AIV-Position halten lassen | Sinnvolle optionale Alternative zum Vanilla-Verteilen | Hoch | Slotwahl und Gruppenkapazität hooken |
| Verteidiger zwischen AIV-Positionen patrouillieren lassen | Sinnvolle optionale Alternative | Mittel | Native Slotwahl mit vorhandenen AIC-Zeitwerten hooken |
| Eigene AIV-Reihe trotz früher Pause verwenden | Notwendige Begleitkorrektur bei angepasstem Verhalten | Hoch | Einheit-zu-Reihe-Mapper selektiv hooken |
| Globale und truppenspezifische Regeln | Sinnvoll und gut bedienbar | Hoch | Mod-eigene synchronisierte Settings |
| Per-KI-/Per-Lord-Regeln | Sinnvoll für unterschiedliche KI-Profile | Mittel | Mod-eigene Policy/Sidecar statt BAIC-Erweiterung |
| Unterstützung der neuen Beduineneinheiten | Sinnvoll, aber noch nicht freigabereif | Mittel | Nach kontrolliertem Mapping-Test |
| Nachträgliche vollständige Tribe-Neuorganisation über öffentliche APIs | Als Produktionslösung nicht empfohlen | Niedrig | Nur Prototyp/Diagnose |

### Empfohlener Umfang

1. Den eigentlichen Positions-Bugfix unabhängig von den optionalen Verhaltensänderungen umsetzen.
2. Startrolle sowie Hold/Patrol nur als ausdrücklich aktivierbare Gameplay-Optionen anbieten.
3. Bei einer vollständigen Portierung alle dynamischen Hooks in einem einzigen Runtime-Eigentümer bündeln.
4. Beduineneinheiten erst aktivieren, nachdem deren AIV-Reihen zur Laufzeit eindeutig zugeordnet wurden.

## 2. Grundfix: ignorierte AIV-Verteidigungspositionen

### Verhalten in der UCP-Erweiterung

Das klassische Spiel überspringt beim Aufbereiten der AIV-Verteidigungspositionen die Misc-Reihen:

- 9: Pikenier
- 11: europäischer Schwertkämpfer
- 18: arabischer Schwertkämpfer

Die UCP-Erweiterung entfernt den bedingten Sprung. Dadurch stehen diese AIV-Positionen anschließend denselben nativen Verteidigungsabläufen zur Verfügung wie die Positionen anderer Truppentypen.

### DE-Befund: Fix weiterhin nötig

**Bestätigt:** Die aktuelle DE-Funktion bei RVA `0x53D00`, im Workspace bereits als `c_game_aiv_prepare_layout` zugeordnet, enthält weiterhin die Maske `0x40A00`. Die gesetzten Bits sind exakt 9, 11 und 18.

Sinngemäß lautet die betreffende Bedingung:

    Positionszähler = 0
    wenn Sonderzustand aktiv oder Reihe > 18 oder Bit der Reihe in 0x40A00 nicht gesetzt:
        Positionen dieser Reihe übernehmen
    sonst:
        Reihe überspringen

Die zusätzlich geprüfte globale Tabelle bei RVA `0x8D403C` enthält in der aktuellen DLL für alle Spieler Nullwerte. In der Baseline wurden für diese Stelle keine Schreibzugriffe gefunden. Damit ist die Sonderfreigabe im untersuchten Build nicht aktiv und der Ausschluss greift tatsächlich.

### Reale Auswirkung im vorhandenen DE-AIV-Bestand

Der lokale Bestand unter `CastlePlanner/BepInEx/plugins/CastlePlanner_Serp/VanillaAIV` umfasst 376 AIVJSON-Dateien:

| Reihe | Truppentyp | Dateien | Positionen |
| ---: | --- | ---: | ---: |
| 9 | Pikenier | 41 | 231 |
| 11 | europäischer Schwertkämpfer | 7 | 69 |
| 18 | arabischer Schwertkämpfer | 37 | 142 |

Insgesamt enthalten **84 verschiedene AIVs 442 betroffene Verteidigungspositionen**. Davon entfallen 313 Positionen in 57 AIVs auf Stock-/DE-Burgen und 129 Positionen in 27 AIVs auf Community-Burgen. Der Fehler betrifft somit tatsächlich verwendete DE-Inhalte.

### Vorgeschlagene Implementierung

- Funktion: RVA `0x53D00`
- Zielinstruktion: RVA `0x5472A`
- Originalinstruktion: `0F 82 9D 03 00 00` (`JB`, sechs Bytes)
- Ersatz: `90 90 90 90 90 90`
- Kontextsignatur:

      42 83 BC 93 3C 40 8D 00 00 C7 01 00 00 00 00 75
      0F 83 F8 12 77 0A 41 0F A3 C3 0F 82 9D 03 00 00

Diese 32-Byte-Folge wurde in der kanonischen DLL genau einmal gefunden. Ihr Datei-Offset ist `0x53B10`; die zu ersetzende Instruktion beginnt innerhalb dieses Treffers bei RVA `0x5472A`.

Der Patch sollte dem vorhandenen Muster in `BugfixesAndQoL` folgen:

1. `CrusaderDE.dll` über den vorhandenen `Shared.NativePatternResolver` auflösen.
2. Genau einen Signaturtreffer verlangen.
3. Den vollständigen Kontext und unmittelbar vor dem Schreiben die sechs Originalbytes prüfen.
4. Speicher temporär mit `VirtualProtect` schreibbar machen.
5. Sechs NOPs schreiben und den Instruction Cache leeren.
6. Bei jeder Abweichung fail-closed deaktivieren und mit Zeitstempel protokollieren.
7. Für einen echten Shutdownpfad optional die Originalbytes idempotent wiederherstellen; nicht im normalen BepInEx-`OnDestroy`, weil dieses in SHCDE bereits während des Startups aufgerufen wird.

### Hook-Kompatibilität

`ActiveAIVDetector` detourt den Einstieg derselben Funktion. Der hier vorgeschlagene Patch liegt jedoch innerhalb des Funktionskörpers bei RVA `0x5472A` und überschneidet sich nicht mit einem Entry-Detour bei RVA `0x53D00`. Trotzdem muss die Installation die erwarteten Bytes nach bereits geladenen Mods prüfen und eine klare Hook-Eigentümerschaft dokumentieren.

### Empfohlenes Zielmodul

Wenn ausschließlich dieser Bug behoben wird, passt der Fix fachlich in `BugfixesAndQoL`. Er verändert die Spielsimulation und bleibt daher Teil eines Mods mit `NetworkMode=1`.

## 3. Starttruppen: Verteidiger oder Grabenarbeiter

### Vorhandener nativer DE-Ablauf

**Starke Zuordnung:** Funktion RVA `0x29520` durchläuft zu Spiel-/KI-Beginn die vorhandenen Einheiten eines KI-Spielers. Sie verarbeitet nur Einheiten, die unter anderem:

- leben,
- dem betreffenden Spieler gehören,
- auswählbar sind,
- noch keinem Tribe zugewiesen sind,
- noch keine konkurrierende KI-Rolle tragen,
- nicht zu ausdrücklich ausgeschlossenen Lord-, Tunnelgräber- oder Belagerungstypen gehören.

Anschließend entscheidet sie zwischen:

- RVA `0x291F0`: einer passenden AIV-Verteidigungsgruppe zuweisen;
- RVA `0x29360`: einer Grabenarbeitergruppe zuweisen.

Die Entscheidung berücksichtigt den nativen AIC-Wert `moat_diggers`, Fernkämpfer-Sonderfälle und ein einheitenspezifisches Fähigkeitsfeld. Die normale spätere Rekrutierungsroutine bei RVA `0x40740` ruft zwar dieselben Hilfsfunktionen auf, entscheidet aber getrennt über Verteidiger, Grabenarbeiter, Harasser und Belagerer. Ein Hook im Startscanner muss daher die normale Rekrutierung nicht verändern.

### Sinnvolle DE-Optionen

Pro unterstütztem Truppentyp:

- **Vanilla:** unveränderte native Entscheidung;
- **Defend:** Starttruppe immer dem nativen Verteidigungspfad zuführen;
- **Dig:** Starttruppe dem nativen Grabenpfad zuführen, sofern der Truppentyp tatsächlich graben kann; andernfalls auf Defend zurückfallen.

Der Rückfall auf Defend ist wichtig, weil ein erzwungener Grabenauftrag für nicht unterstützte Einheitentypen keinen gültigen Chore-/Bewegungsablauf garantiert.

### DE-fähige Grabenarbeiter

Die aktuelle native `DigMoatTileId`-Behandlung und die vorhandene lokale Rekonstruktion in `BugfixesAndQoL` ergeben folgende Liste:

- Bogenschütze
- Speerträger
- Pikenier
- Streitkolbenkämpfer
- Ingenieur
- arabischer Sklave
- Beduinen-Eunuch
- Beduinen-Plänkler
- Beduinen-Sappeur
- Beduinen-Demolisher

Armbrustschützen gehören nach dem aktuellen Befund nicht zu dieser Liste.

### Bevorzugter Implementierungsweg

Ein RedBird-/Iced-Context-Hook soll direkt an der Verzweigung innerhalb RVA `0x29520` ansetzen. Der Hook verändert nur die Auswahl zwischen den bereits vorhandenen Aufrufen `0x291F0` und `0x29360`. Dadurch bleiben native Tribe-Erzeugung, AI-Kategorien, Einheitenzuweisung und sonstige Buchhaltung erhalten.

Vor einer Implementierung sind für den konkreten Hook-Span zwingend zu bestimmen:

- exakte Instruktionsgrenzen und Mindestüberschreibungsgröße;
- Register, welche Spieler-ID, Unit-ID, Einheitentyp und Fähigkeitswert enthalten;
- alle auf den Ausgangspfaden benötigten Flags und Register;
- korrekter Rücksprung für Vanilla, Defend und Dig;
- Verhalten, wenn die Policy während des Hooks nicht verfügbar oder ungültig ist: immer Vanilla.

### Nicht bevorzugte Alternative

Mit `GameTribeManagerAPI.AssignUnit`, `DigMoat`, `MoveTo` und verwandten Methoden ließe sich ein Prototyp nachträglich aufbauen. Das würde jedoch die bereits getroffene Vanilla-Zuweisung rückgängig machen, zusätzliche Tribe-Buchhaltung duplizieren und könnte mit der AI-Aktualisierung konkurrieren. Außerdem ist der öffentliche `UnassignUnit(tribeId, unitId)`-Wrapper in Script Extender 2.0.2 bezüglich der nativen Argumentreihenfolge fehlerhaft. Für Produktionscode ist daher der native Entscheidungs-Hook vorzuziehen.

## 4. Defensive AIV-Positionen halten oder patrouillieren

### Native Zielwahl

**Starke Zuordnung:** Funktion RVA `0x3E850` erhält einen Tribe, eine AIV-Reihe und einen Positionsordinal. Sie:

1. liest bis zu zehn gespeicherte Positionen der Reihe;
2. wählt den n-ten belegten Slot;
3. prüft die Erreichbarkeit über RVA `0x3E650`;
4. erteilt den nativen Bewegungsauftrag über RVA `0x11DD10`;
5. verwendet bei einem nicht erreichbaren Ziel den Fallback bei RVA `0x3E940`.

Die Funktion wird aus mehreren periodischen Verteidigungsabläufen (`0x3D9E0`, `0x3DB30`, `0x3DD60`, `0x3DEE0`) aufgerufen. Deshalb ist sie ein geeigneter zentraler Eingriffspunkt: Nur die Wahl des AIV-Slots wird verändert, während Pathfinding, Fallback und spätere Vanilla-Prioritäten erhalten bleiben.

### Hold

Ziel von Hold ist nicht, der Einheit Kampfhandlungen zu verbieten. Es soll lediglich jede Verteidigungsgruppe dauerhaft einer eigenen AIV-Position zuordnen, statt mehrere Gruppen zyklisch über weniger Zielpositionen zu verteilen.

Zusätzlich zur Slotwahl muss die Gruppenkapazität angepasst werden. RVA `0x2BC40` begrenzt die Zahl der Verteidigungsgruppen normalerweise anhand einer AIC-Gruppengrenze und der Zahl belegter Slots. Für Hold soll die effektive Grenze für konfigurierte Reihen auf **eine Gruppe pro belegtem AIV-Slot** gesetzt werden. Nicht konfigurierte Reihen und die nativen Sonderreihen 8, 10 und 17 bleiben unverändert.

### Patrol

RVA `0x3DB30` verteilt Positionsordinale abhängig von einem Rally-/Zeitwert und der kleineren Zahl aus Slots und Gruppenkapazität. Die `InternalAIC`-Struktur enthält unter anderem:

- `defense_patrol_trigger_level`
- `defense_patrols`
- `defense_patrol_style`
- `defense_patrol_delay`
- `defensive_trigger_level`

`defense_patrols` und `defense_patrol_delay` sind sehr wahrscheinlich die DE-Entsprechungen der UCP-Werte für Patrouillengruppen und Rally-Zeit. Die genaue Einheit, Nullbedeutung und Obergrenze müssen durch Laufzeitprotokollierung bestätigt werden, bevor UI-Grenzen oder Umrechnungen festgelegt werden.

### Grenzen der öffentlichen Script-Extender-Events

- `OnTribeGetNextPatrolWaypoint` ist im aktuellen Extender nur beobachtend nutzbar: Änderungen seiner EventArgs werden vom Detour nicht zurück in den nativen Kontext geschrieben.
- `OnTribeIssueOrderMoveHere` reicht geänderte Koordinaten und weitere EventArgs tatsächlich an die Originalfunktion weiter.
- Ein globales Umschreiben von `OnTribeIssueOrderMoveHere` wäre dennoch zu unspezifisch. Der Mod müsste AI-Verteidigungsaufträge sicher von Angriffen, Rückzügen, Spielerbefehlen und anderen Bewegungen unterscheiden.

Deshalb ist ein enger Context-Hook an RVA `0x3E850`, ergänzt durch die Kapazitätsbehandlung in `0x2BC40`, die robustere Produktionslösung.

### Verhalten bei Konflikten und Fehlern

- Keine gültige Policy: Vanilla-Ordinal unverändert verwenden.
- Keine belegte Position: nativen Fallback ausführen lassen.
- Position unerreichbar: native Erreichbarkeitsprüfung und Fallback nicht umgehen.
- Sonderauftrag oder Bedrohungsreaktion: spätere native Prioritäten nicht blockieren.
- Mehrere Mods beanspruchen denselben Span: Funktion deaktivieren und Konflikt protokollieren, nicht blind überschreiben.

## 5. Eigene AIV-Reihe statt früher Sklavenreihen-Umleitung

### DE-Verhalten

**Starke Zuordnung:** Der Einheit-zu-AIV-Reihe-Mapper bei RVA `0x2D760` liest den tatsächlichen Einheitentyp und liefert normalerweise den Index aus einer nativen Zuordnungstabelle. Unter bestimmten Bedingungen einer frühen AIV-Pause leitet er mehrere Fernkampftypen stattdessen auf Reihe 13, die Sklavenreihe, um.

Dieses Vanilla-Verhalten kann sinnvoll sein, solange keine eigene Regel für den betreffenden Typ existiert. Bei Hold-, Patrol- oder Startrollen-Konfigurationen würde es jedoch verhindern, dass die ausdrücklich konfigurierte eigene AIV-Reihe benutzt wird.

### Implementierung

- Mapper nur dann beeinflussen, wenn für den konkreten Spieler/Lord und Truppentyp eine nicht-Vanilla-Policy wirksam ist.
- In diesem Fall den Tabellenindex des tatsächlichen Einheitentyps zurückgeben.
- Für alle übrigen Aufrufe die Originalfunktion vollständig ausführen.
- Nicht pauschal die frühe-Pause-Regel abschalten.
- Vor dem Hook klären, ob derselbe Mapper auch von der normalen Rekrutierung verwendet wird; der aktuelle Callgraph zeigt einen Aufruf aus RVA `0x291F0`, das sowohl vom Startscanner als auch von der Rekrutierungsroutine aufgerufen wird. Die Policy muss daher unabhängig vom Aufrufer korrekt sein.

## 6. Einstellungen und UCP-artige Prioritätsregeln

### Warum keine zusätzlichen BAIC-Felder verwendet werden sollten

`InternalAIC` ist eine feste native Binärstruktur. Ihre Größe und Feldoffsets sind Teil des Spielvertrags. Beliebige neue Felder wie bei einer erweiterbaren UCP-AIC-Datei können nicht angehängt werden, ohne Layout und ABI zu beschädigen. Der Script Extender stellt derzeit ebenfalls keinen allgemeinen Speicher für zusätzliche benutzerdefinierte AIC-Felder bereit.

### Empfohlene Policy

Die effektive Einstellung sollte in dieser Reihenfolge gesucht werden:

1. Regel für konkreten Lord und konkreten Truppentyp;
2. gemeinsame Regel für den konkreten Lord;
3. globale Regel für den konkreten Truppentyp;
4. globale gemeinsame Modregel;
5. Vanilla-Verhalten.

Sinnvolle getrennte Werte sind:

- Startrolle: `Vanilla`, `Defend`, `Dig`;
- Bewegung: `Vanilla`, `Hold`, `Patrol`;
- optionaler Patrol-Gruppenwert;
- optionales Patrol-Intervall, erst nach Bestätigung der nativen Einheit.

### Lobby-Einstellungen

Gameplayrelevante Werte werden als `[SyncHostOnly]` klassifiziert und über `Shared/PresetLobbyModSettingsViewModel.cs` sowie `Shared/SerpLocalization.cs` registriert. Clients dürfen keine abweichenden lokalen Gameplaywerte anwenden. Commands und Runtime müssen die Hostrechte zusätzlich intern prüfen.

Bei sehr vielen Truppentypen sollte die Oberfläche globale Defaults und nur bei Bedarf ausklappbare Überschreibungen anbieten. Der äußere `ScrollViewer` benötigt horizontale und vertikale Scrollbars auf `Auto`.

### Per-Lord-Sidecar

Für umfangreiche Profile kann der Mod zusätzlich eine eigene JSON-Datei verwenden, die nach `AILords` beziehungsweise einem stabilen Custom-Lord-Namen indiziert wird. Runtime-JSON muss über `Shared/DependencyFreeJson.cs` gelesen werden. Schema, Enumwerte, Pflichtfelder und Wertebereiche sind fail-closed zu validieren.

Die Sidecar-Datei ersetzt keine synchronisierten Lobbywerte. Im Multiplayer muss der Host die daraus resultierende effektive Policy synchronisieren oder ein identisches gameplayrelevantes Modpaket verlangen.

## 7. Beduineneinheiten und ungeklärte Reihenverschiebung

### Bestätigte Daten

Die DE-AIVJSONs verwenden zusätzliche Misc-Typen ab 23. Der lokale Katalog ordnet zu:

- 23: Kamellanzenreiter
- 24: Heiler
- 25: Eunuch
- 26: Hinterhaltkämpfer
- 27: Plänkler
- 28: schweres Kamel
- 29: Sappeur
- 30: Demolisher

Die native Einheit-zu-Reihe-Tabelle bei RVA `0x2CEE00` enthält dagegen 30 Integerwerte. An ihren Indizes 22 bis 29 stehen die Unit-Typen 78 bis 85, also die acht Beduineneinheiten. Damit scheint der Kamellanzenreiter nativ auf Index 22 zu zeigen, während die AIVJSON-Darstellung bei 23 beginnt.

Im untersuchten AIV-Bestand kommen die zusätzlichen Werte 23 bis 26 tatsächlich vor. Der verwaltete `AIVLoader.SaveData.GetRawData()` übernimmt diese Werte unverändert, abgesehen von der bekannten Subtraktion von 9000 bei Werten über 9000.

### Konsequenz

Die Verschiebung darf nicht geraten oder allein anhand plausibler Namen korrigiert werden. Möglich sind unter anderem eine Umwandlung beim nativen Import, eine reservierte Reihe oder eine fehlerhafte bisherige Katalogannahme.

### Erforderlicher Laufzeittest

Eine kontrollierte AIV erhält je eine eindeutig getrennte Testposition für die Werte 22 bis 30. Anschließend werden die acht Beduineneinheiten einzeln als Starttruppen und als reguläre Verteidiger erzeugt. Zu protokollieren sind:

- AIVJSON-Wert;
- nach Import gespeicherter nativer Reihenindex;
- `eChimps`-/Unit-Typ;
- vom Mapper `0x2D760` zurückgegebene Reihe;
- ausgewählte Position in `0x3E850`;
- Ergebnis für Startzuweisung und normale Rekrutierung.

Bis dieser Test abgeschlossen ist, sollen die Beduineneinheiten in der produktiven Policy auf Vanilla verbleiben. Die klassischen Reihen 1 bis 21 sind davon nicht betroffen.

## 8. Empfohlene Modarchitektur

### Nur der Grundfix

- Integration in `BugfixesAndQoL`.
- Kleiner, hash- und patternvalidierter Inline-Patch.
- Keine Einstellungen erforderlich, sofern der Fix als reine Wiederherstellung vorhandener AIV-Daten behandelt wird.
- `NetworkMode=1` bleibt korrekt.

### Vollständige Verhaltensportierung

Empfohlen wird ein eigenständiger Mod **AIVTroopBehaviour** mit `NetworkMode=1`. Dieser Mod besitzt gemeinsam:

- den optionalen Startrollen-Hook;
- den Einheit-zu-Reihe-Mapper-Hook;
- den Slotwahl-Hook;
- den Gruppenkapazitäts-Hook;
- die Policy und synchronisierten Einstellungen.

Die dynamischen Hooks sollten nicht zwischen `BugfixesAndQoL` und `ExtraFeatures` aufgeteilt werden. Ein einziger Eigentümer verhindert unterschiedliche Installationsreihenfolgen, überlappende Trampoline und eine harte Laufzeitabhängigkeit zwischen ansonsten eigenständigen Mods.

Der Grundfix kann trotzdem in `BugfixesAndQoL` bleiben, solange der vollständige Mod dieselbe Stelle nicht erneut patcht. Der vollständige Mod muss erkennen, ob die sechs Bytes bereits korrekt genoppt sind, und diesen Zustand als kompatibel akzeptieren, ohne Eigentum oder Rollback für einen fremden Patch zu beanspruchen.

### Verhältnis zu AIDefense

`AIDefense` verfolgt ein anderes Ziel: Turmverteidiger werden in privaten neutralen Tribes gehalten, um sie vor konkurrierenden Vanilla-Kategorien zu schützen. Eine allgemeine AIV-Verteidigungssteuerung würde dagegen native Verteidigungsgruppen und AIV-Slots verändern.

Deshalb:

- nicht ungeprüft in `AIDefense` integrieren;
- keine Einheit übernehmen, die bereits von `AIDefense` als private Turmverteidigung verwaltet wird;
- vor Installation die belegten Hook-Spans vergleichen;
- bei einer späteren Kombination eine ausdrückliche gemeinsame Ownership-Policy definieren;
- niemals dieselbe Einheit gleichzeitig durch private AIDefense-Tribes und AIV-Hold/Patrol verwalten.

## 9. Sicherheitsanforderungen für native Hooks

Jede spätere Implementierung muss:

- den aktuellen vollständigen DLL-Hash mit der Baseline vergleichen;
- zusätzlich ein eindeutiges Pattern und erwartete Originalbytes prüfen;
- bei mehreren oder fehlenden Treffern fail-closed bleiben;
- Funktionsgrenzen und kompletten überschriebenen Instruktionsbereich dokumentieren;
- Register-Liveness einschließlich effektiver Adressen prüfen;
- Stack, ABI-erhaltene Register und benötigte Flags bewahren;
- alle Reads vor clobbernden Writes nachweisen;
- kontrollierte Rücksprünge für Vanilla- und Modpfade besitzen;
- native Delegates und Hooks prozessweit verwurzeln;
- Hooks nicht im normalen Plugin-`OnDestroy()` entfernen;
- Logs mit Zeitstempel einschließlich Millisekunden schreiben.

Ein RVA allein ist niemals eine ausreichende Auflösung. Bei einem neuen Spielbuild werden alle Adressen und Schlussfolgerungen gegen die neue kanonische DLL neu geprüft.

## 10. Prüf- und Akzeptanzmatrix

### Statische Prüfungen

- Installierte DLL und Baseline besitzen denselben vollständigen SHA-256.
- Jedes Pattern besitzt genau einen Treffer.
- Originalbytes, Patchlänge und Instruktionsgrenzen stimmen exakt.
- Rücksprungadresse liegt auf einer gültigen Instruktionsgrenze.
- Kein Hook überschneidet sich mit `ActiveAIVDetector`, `AIDefense` oder einem anderen installierten Mod.
- Der sechs Byte lange Grundpatch verändert ausschließlich den Ausschlusssprung.

### Grundfix

- Test-AIV mit mindestens einer Position in Reihe 9, 11 und 18 laden.
- Vor dem Patch bestätigen, dass die nativen Positionszähler null bleiben.
- Nach dem Patch bestätigen, dass alle drei Reihen und Positionen übernommen werden.
- Starttruppen und normal rekrutierte Verteidiger müssen die Positionen verwenden können.
- Alle übrigen Reihen müssen bitgenau dasselbe Verhalten wie Vanilla zeigen.

### Startrollen

- `Vanilla`, `Defend` und `Dig` getrennt je unterstütztem Typ prüfen.
- Nicht grabfähiger Typ mit `Dig` fällt auf Defend zurück.
- Bereits zugewiesene, tote, fremde oder nicht auswählbare Einheiten bleiben unverändert.
- Mehrere Starttruppen desselben Typs werden ohne Tribe-Korruption verteilt.
- Mehrere KI-Spieler verwenden nur ihre jeweils effektive Policy.
- Normale spätere Rekrutierung bleibt ohne eigene Einstellung unverändert.

### Hold und Patrol

- Ein und mehrere belegte AIV-Slots prüfen.
- Bei Hold genau eine Verteidigungsgruppe je belegtem Slot zulassen.
- Bei Patrol Reihenfolge, Richtungswechsel, Zeitintervall und Gruppenzahl protokollieren.
- Unerreichbare Position muss den nativen Fallback benutzen.
- Gegnerkontakt, Rückzug, Sonderaufgaben und Wegfindungsfehler dürfen nicht blockiert werden.
- Verhalten vor und nach der frühen AIV-Pause prüfen.

### Beduineneinheiten

- Werte 22 bis 30 kontrolliert gegeneinander testen.
- Mapper-Ergebnis, gespeicherte Reihe und tatsächlich gewählte Position vergleichen.
- Alle acht Beduineneinheiten einzeln prüfen.
- Erst nach eindeutiger Zuordnung in die produktive Typentabelle aufnehmen.

### Multiplayer

- Host und Client mit identischer Modversion und identischem Preset testen.
- Abweichende gameplayrelevante Installation muss über `NetworkMode=1` verhindert werden.
- Clients dürfen Hostwerte weder lokal überschreiben noch in ihre lokale Presetdatei übernehmen.
- Save/Load und Rückkehr in die Lobby dürfen die effektive Policy nicht verändern.

### Laufzeitnachweis

Ein bloßes `READY`-Log reicht nicht aus. Nach dem Startup-Cleanup müssen mindestens folgende mod-eigene Marker erscheinen:

- erfolgreiche Native-Auflösung und Hook-/Patchinstallation;
- Kartenstart oder Save-Load;
- mindestens eine ausgewertete Startrollenentscheidung oder Slotwahl;
- bei aktivem Patrol mindestens ein späterer periodischer Zielwechsel.

## 11. Empfohlene Umsetzungsreihenfolge

1. Grundfix in Isolation implementieren und mit Reihen 9, 11 und 18 verifizieren.
2. Diagnose-Hooks für Mapper, Startentscheidung und Slotwahl erstellen, zunächst ohne Verhaltensänderung.
3. Beduinen-Reihenmapping durch kontrollierte AIV-Laufzeittests abschließen.
4. Reine Policyklassen und statische Tests für die Überschreibungsreihenfolge erstellen.
5. Startrollen-Hook mit Vanilla-Fallback implementieren.
6. Hold samt Gruppenkapazität implementieren.
7. Native Bedeutung und Einheit der Patrol-AIC-Werte bestätigen, danach Patrol implementieren.
8. Synchronisierte Lobbyoberfläche und optionalen Per-Lord-Sidecar ergänzen.
9. Koexistenz- und Multiplayer-Tests mit den übrigen Mods durchführen.

## Schlussbewertung

Der zentrale UCP-Fix ist in SHCDE weder überholt noch bereits durch Firefly behoben. Er betrifft eine erhebliche Zahl vorhandener DE-Burgen und kann mit einem sehr kleinen, eindeutig identifizierten Native-Patch umgesetzt werden.

Die zusätzlichen Startrollen-, Hold- und Patrol-Möglichkeiten sind ebenfalls sinnvoll. Die DE besitzt weiterhin fast alle dafür notwendigen nativen Abläufe und Daten. Für eine robuste Umsetzung sollten diese Abläufe an engen Entscheidungspunkten beeinflusst und nicht durch eine parallele, vollständig verwaltete Tribe-KI ersetzt werden.

Die größten noch offenen Punkte sind das DE-spezifische Beduinen-Reihenmapping und die genaue Laufzeitbedeutung der Patrol-AIC-Werte. Beide lassen sich mit begrenzten instrumentierten Tests klären. Bis dahin können der Grundfix und die klassischen Truppentypen unabhängig und sicher weiterentwickelt werden.
