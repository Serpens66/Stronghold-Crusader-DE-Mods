# Code-Review der installierten Workspace-Mods

Stand: 2026-08-12

## Zweck

Diese Dateien sind eine Übergabe für einen nachfolgenden Chat. Sie dokumentieren bestätigte oder klar begründete Verbesserungen, ändern aber noch keinen Modcode. Vor einem Fix müssen die genannten Stellen erneut gegen den dann aktuellen Arbeitsbaum geprüft werden.

## Abgrenzung und Zuordnung

Geprüft wurden die installierten Verzeichnisse unter `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins`, soweit eine eindeutige Workspace-Quelle vorhanden ist. Bei allen folgenden Haupt-DLLs war die installierte DLL bytegleich mit dem lokalen Buildartefakt:

| Installierter Mod | Workspace | SHA-256 der Haupt-DLL |
| --- | --- | --- |
| `AIVPlacementLobby_Serp` | `AIVPlacementLobby` | `386EB63EAB6170B49469309F510238F2185F7F5C10956E362DADD29747C787D8` |
| `BugfixesAndQoL_Serp` | `BugfixesAndQoL` | `C404023E8EC155C2AA0956585D4CA672950E10EA96450D099CC021DA23A3D0BA` |
| `BuildingCosts_Serp` | `BuildingCosts` | `13D01F1AF7B0D9D93817A5AEFF64E61E8629A4F9DADC486A6EC6B212214F3A2D` |
| `BuildingLimit_Serp` | `BuildingLimit` | `8EA0807CEDE35B1ECD93D6F08A295057A5C6E24677F6FCB02995A84ABDE28AAA` |
| `CustomCustomTrail_Serp` | `CustomCustomTrail` | `0AE22B10834D93B68CEE3BEF58D12CF062EF0FC40E6B4FD5122B1D6C477088F3` |
| `ExtraFeatures_Serp` | `ExtraFeatures` | `27183101E479F9281CFA39C0955C187B8C3962D3368526855960A25E6C931589` |
| `RandomEvents_Serp` | `RandomEvents` | `B521F19E2038FA449C07DB2CBED2A1C47EF575CCB51F24AB04C3E64F441B8E08` |
| `SpawnCastle_Serp` | `SpawnCastle` | `C4443761678E64C7481BAD1B1734DBD999B88E0B69F5A4F97D92C3AF235E2D89` |
| `StartConditions_Serp` | `StartConditions` | `6F6E024695F500237D447C6B62F400969C806381CF0E904BDC5917DF2C68E032` |
| `UnitCosts_Serp` | `UnitCosts` | `EAC717A88DB7F9A67D5A149540C648F0E28FA54CCD97E606C438EC00A8E67798` |
| `UnitLimit_Serp` | `UnitLimit` | `9E4EDD8FE1BCAEFDD448EE1A88E31DA56035EB618F115DE5F419E626AA766BDB` |

Die Hashgleichheit belegt die Zuordnung zur lokalen Buildausgabe. Sie belegt nicht, dass ein aktuell geänderter, noch nicht neu gebauter Quellbaum exakt dem Artefakt entspricht. Zum Reviewzeitpunkt gab es bereits Benutzeränderungen unter anderem in `BugfixesAndQoL`-Artefakten und in `RandomEvents`-Quellen. Diese dürfen ein Fix-Chat nicht überschreiben.

Für `fixes` und `LorrdyAISharesGold` wurde keine eindeutige Workspace-Quelle gefunden; sie sind deshalb nicht Bestandteil des Quellreviews. Im aktuellen Log scheitert `LorrdyAISharesGold` mit `Could not find global Lua function '{FunctionName}'`. Das ist ein realer Laufzeitfehler, kann ohne dessen Quelle hier aber nicht seriös behoben werden.

## Ergebnisübersicht

| Mod | Priorität | Kurzbefund | Detaildatei |
| --- | --- | --- | --- |
| AIV Placement Lobby | kein Befund | die bestätigten Capture- und Worker-Probleme wurden behoben | [AIVPlacementLobby.md](AIVPlacementLobby.md) |
| Bugfixes and QoL | niedrig | fehlendes Logo | [BugfixesAndQoL.md](BugfixesAndQoL.md) |
| Building Costs | kein Befund | Dead Code entfernt; Initialisierung und Lifecycle bereinigt | [BuildingCosts.md](BuildingCosts.md) |
| Building Limit | kein Befund | alten Scanpfad entfernt; Initialisierung und Lifecycle bereinigt | [BuildingLimit.md](BuildingLimit.md) |
| Custom Custom Trail | kein Befund | im geprüften Umfang kein umsetzbarer Fehler oder unnötiger Pfad gefunden | – |
| Extra Features | mittel | falsches Multiplayer-Signal für Pakete; Knight-Deduplizierung wird nicht pro Karte begrenzt | [ExtraFeatures.md](ExtraFeatures.md) |
| Random Events | mittel | GameMode wird pro Tick doppelt erfasst; parallele Lokalisierung; kleiner Dead Code | [RandomEvents.md](RandomEvents.md) |
| Spawn Castle | mittel | nachweislich nicht laufende Dispatcher-/`Update`-Fallbacks; parallele Lokalisierung | [SpawnCastle.md](SpawnCastle.md) |
| Start Conditions | niedrig | zwei ungenutzte Codebestandteile; widersprüchlicher Plugin-Lifecycle | [StartConditions.md](StartConditions.md) |
| Unit Costs | mittel | Tooltip erzeugt unverändert pro GUI-Frame mehrere Objekte und Strings | [UnitCosts.md](UnitCosts.md) |
| Unit Limit | niedrig | veralteter auskommentierter Scanpfad; widersprüchlicher Plugin-Lifecycle | [UnitLimit.md](UnitLimit.md) |

Die mit AIV Placement installierten Workspace-Bibliotheken `AIVParser.Core`, `AIVPlacement.Core`, `AIVPlacementLobby.Core` und `MapParser.Core` wurden über ihre vorhandenen Tests mit abgedeckt. `CustomCustomTrail.Core` wurde ebenfalls über seine vorhandenen Tests abgedeckt.

## Empfohlene Fixreihenfolge

2. `ExtraFeatures`: Netzwerkmodus korrekt bestimmen und Request-Deduplizierung kartengebunden machen.
3. `UnitCosts`, `BugfixesAndQoL`: Hot-Path-Allokationen beziehungsweise Warnungsflut beseitigen.
4. `RandomEvents` und `SpawnCastle`: gemeinsame Lokalisierung verwenden; bei SpawnCastle die nachweislich toten Frame-Fallbacks entfernen.
5. Plugin-Lifecycle und kleinen Dead Code in StartConditions bereinigen.

## Bereits ausgeführte Prüfungen

- `AIVParser.Tests`: 35/35 bestanden.
- `AIVPlacement.Tests`: 29/29 bestanden.
- `MapParser.Tests`: 36/36 bestanden.
- `AIVPlacementLobby.Tests`: 34/34 bestanden.
- `CustomCustomTrail.Tests`: 18/18 bestanden.
- `.inspect/HostClientPresetTests/bin/HostClientPresetTests.exe`: bestanden.
- `.inspect/AuditModSettings.ps1`: 10 XAML-Dateien bestanden, einschließlich Tooltips, gemeinsamer Styles, Locale-Key-Parität und CRLF.
- Letzter BepInEx-Logabschnitt: kein Fehler der eindeutig zugeordneten eigenen Mod-DLLs. Auffällig waren das fehlende Bugfixes-Logo und 60 identische `GameNetworkAPI.GetLocalPlayerId`-Warnungen. Die Warnungen wurden bei der Nachprüfung als wiederholte Mod-Aufrufe außerhalb einer gültigen Lobby-/Ingame-Phase eingeordnet und deshalb nicht als Script-Extender-Bug übernommen.

Der Benutzer hat den Capture-Fix anschließend im Spiel erfolgreich getestet. Der zugehörige
BepInEx-Logabschnitt zeigte vollständige Auswertungen ohne Fehler oder Warnungen der Mod; das
dabei noch aktive ausführliche Diagnose-Logging wurde danach aus dem Produktionspfad entfernt.

## Regeln für den Fix-Chat

- Vor jeder Änderung den aktuellen `git status` und die jeweilige Datei erneut lesen; vorhandene Benutzeränderungen erhalten.
- Nicht mehrere wesentlich verschiedene Architekturvarianten ohne Rückfrage auswählen.
- Modsettings-Änderungen wieder mit HostClientPresetTests, XAML-Audit, Locale-Parität und CRLF prüfen.
- Nach allen Prüfungen pro tatsächlich geändertem Mod genau einmal dessen `build.bat` direkt nach den Workspace-Anweisungen ausführen. Diese Review-Erstellung selbst ändert keinen Mod und benötigt daher keinen Mod-Build.
