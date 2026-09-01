# SerpNativeAPI-Migrations- und Releaseplan

## 0. Arbeitsstand und Übergabe zwischen Chats

Stand: 1. September 2026. Die Umsetzung wird bewusst phasenweise in getrennten Chats durchgeführt. Dieser Arbeitsstand ist die maßgebliche Übergabe; ein neuer Chat soll vor Änderungen zusätzlich `git status --short` und die Diffs der konkret betroffenen Dateien lesen, damit parallele Benutzeränderungen erhalten bleiben.

### So verwendet ein neuer Chat diesen Plan

Dieser Plan ist gleichzeitig Spezifikation, Arbeitsreihenfolge und Übergabeprotokoll. Für jeden neuen Chat gilt:

1. Zuerst die Workspace-Anweisungen, diese Datei vollständig, `git status --short` und die Diffs aller Dateien lesen, die im vorgesehenen Arbeitspaket berührt werden könnten.
2. Den in der Statustabelle als `NÄCHSTES` markierten Arbeitspunkt bearbeiten. Ohne ausdrückliche Benutzeranweisung nicht stillschweigend in das nächste Arbeitspaket wechseln.
3. Bei nativen Arbeiten vor jeder Verwendung von RVA, Pattern oder Funktionsgrenze `_inspect/CrusaderDE-Native-Baseline/CURRENT.md` und `CURRENT.json` lesen und den Hash der installierten kanonischen `CrusaderDE.dll` vergleichen.
4. Bereits vorhandene oder parallel entstandene Änderungen nicht zurücksetzen. Insbesondere sind die aktuellen Gatehouse-Center-Distance-Änderungen maßgeblich.
5. Während Entwicklung und Tests keine Modversion erhöhen, keine Releases veröffentlichen und keine README verändern. Ein echter Upload oder Release gehört ausschließlich in Phase 10 und benötigt die dann geltende Benutzerfreigabe.
6. Pro Chat möglichst genau ein unten definiertes Arbeitspaket abschließen. Eine einzelne native Capability-Welle darf nicht auf API-Vertrag, Implementierung und Consumer auf mehrere halbfertige Stände verteilt werden.
7. Vor dem abschließenden `build.bat` alle statischen Prüfungen, Tests und CRLF-Audits erledigen. Danach den jeweiligen Buildtreiber genau einmal wie in den Workspace-Anweisungen vorgesehen ausführen.
8. Zum Abschluss diesen Abschnitt aktualisieren: Status, geänderte Dateien, entfernte Altimplementierung, ausgeführte Tests mit Ergebnis, noch ausstehende Laufzeittests, bekannte Risiken und exakt nächstes Arbeitspaket. Ein neuer Chat darf nicht aus einer bloßen Abschlussnachricht erraten müssen, was im Dateisystem gilt.

Statuswerte: `ERLEDIGT`, `NÄCHSTES`, `OFFEN`, `WARTET AUF SPIELTEST`, `BLOCKIERT DURCH ENTSCHEIDUNG`.

| Arbeitspaket | Status | Kurzbeschreibung |
|---|---|---|
| Phase 1 | ERLEDIGT | Öffentliche API-Grenze, XML-Dokumentation und Surface-Audit |
| Phase 2A | BLOCKIERT DURCH ENTSCHEIDUNG | API-Releaseprojekt und reproduzierbare Auswahl des veröffentlichten API-Artefakts |
| Phase 2B | OFFEN | Thin-/Bundle-Erzeugung, Provenance-Schema und Archiv-Audits |
| Phase 2C | OFFEN | Laufzeit-Duplikatschutz und Tests |
| Phase 3A | OFFEN | Gatehouse-Timing-Pilot in `ExtraFeatures` |
| Phase 3B | OFFEN | Gatehouse-Distanzursprung- und Selected-Unit-Piloten in `BugfixesAndQoL` |
| Phase 4 | OFFEN | Spieltests, Logauswertung und anschließende `APITest`-Bereinigung |
| Phase 5A–5E | OFFEN | Je eine Welle für AIV, Assassinen, Hunter, Troop Movement und Recruitment |
| Phase 6A–6C | OFFEN | Random Events, AI-Economy/Fixes und Vanilla-AIC-Export |
| Phase 7A–7C | OFFEN | Player/Readiness, HUD-Koordination und Shared-Bereinigung |
| Phase 8 | OFFEN | Workspaceweiter NativePatternResolver-/Legacy-Audit |
| Phase 9A–9C | OFFEN | finale GitHub-, Steam- und Nexus-Integration |
| Phase 10 | OFFEN | atomare Versionierung, vollständige Abnahme und Veröffentlichung |

`NÄCHSTES` darf erst gesetzt werden, nachdem die offenen Entscheidungen in Abschnitt 10 beantwortet wurden. Bis dahin ist Phase 2A der fachliche nächste Schritt, aber nicht implementierungsbereit.

### Abgeschlossener Umfang: Phase 1

Phase 1 aus Abschnitt 7 ist umgesetzt; nach ihrer Prüfung endet der aktuelle Umsetzungs-Chat. Erledigt sind:

- Die öffentliche Vertragsgrenze ist explizit auf die fachlichen API-Typen begrenzt: `ISerpNativeApi`, `SerpNativeApi`, Capability-Interfaces, das Selected-Unit-Registrierungs-Handle, unveränderliche Settings-/Kontext-/Diagnosetypen, Capability-IDs und Zustände sowie die dokumentierten Gatehouse-Werte. `SerpNativeAPIPlugin` bleibt als technisch notwendiger BepInEx-Einstieg öffentlich, ist aber kein Consumer-Service.
- Native Infrastruktur, Zielkataloge, RVAs, Adressen, PE-Auswertung, Ownership-Registry, Speicherzugriff, konkrete Services, Broker, Eventadapter und Resolver sind weiterhin `internal`. Tests greifen ausschließlich über `[InternalsVisibleTo("SerpNativeAPITests")]` darauf zu.
- Alle öffentlichen Typen und Member besitzen XML-Dokumentation. `SerpNativeAPI.csproj` erzeugt `SerpNativeAPI.xml`; fehlende öffentliche XML-Dokumentation wird über Compilerfehler `CS1591` verhindert.
- `_inspect/SerpNativeAPITests` enthält einen expliziten Allowlist-Audit aller exportierten Typen und lehnt Pointer, `IntPtr`/`UIntPtr` sowie nach RVA-, Pattern-, Detour- oder Memory-Writer-Implementierung benannte Signaturtypen ab.
- Die Gatehouse-API trennt `IGatehouseDistanceOriginCapability` für den 75-Byte-Distanzblock von `IGatehouseTimingCapability` für die vier Immediates. Die Capabilities haben getrennte Diagnosen, Besitzer und Intervalle, verwenden wegen der gemeinsamen Speicherseite aber einen gemeinsamen Mutations-Lock. Diese Trennung darf nicht wieder zu einer monolithischen Capability zurückgebaut werden.
- Phase 1 ist technisch geprüft: Rebuild von `_inspect/SerpNativeAPITests` erfolgreich, gesamte Testsuite einschließlich Surface-Audit erfolgreich, abschließender `SerpNativeAPI/build.bat /nopause` mit 0 Warnungen und 0 Fehlern erfolgreich und in den Spielordner installiert. Die erzeugte XML-Dokumentation liegt neben DLL und PDB.
- Der CRLF-Audit der geänderten API-, Gatehouse-Dokumentations- und API-Testdateien meldet keine nackten LF und keine versehentlich ausgeschriebenen Zeilenumbruch-Ersatzsequenzen.
- Es wurden keine Versionsnummern geändert und keine README-Datei bearbeitet.

### Bewusst noch nicht umgesetzt

- Phase 2 (API-Releaseprojekt, Thin-/Bundle-Schema, Provenance, Duplikatschutz und Archiv-Audits) ist noch vollständig offen. Eine zwischenzeitlich angelegte `SerpNativeAPI/release.bat`, eine Registrierung in `Shared/Release/release-projects.json` und ein erster Installationswächter wurden wieder entfernt, damit Phase 2 atomar in einem eigenen Chat umgesetzt werden kann.
- Die Pilotmigrationen aus Phase 3 sind noch nicht begonnen. `ExtraFeatures` verwendet weiterhin `GatehouseTimingPatch`; `BugfixesAndQoL` verwendet weiterhin seinen vorhandenen Selected-Unit-Code und besitzt noch keinen Mittelpunkt-Consumer. Es besteht noch keine API-HardDependency in diesen beiden Mods.
- `APITest` bleibt unverändert als Vergleichs- und Smoke-Test bestehen. Es darf erst nach abgeschlossener Pilotintegration, automatisierten Tests und bestätigtem Spieltest entfernt werden.
- Die weiteren Capability-Wellen, Shared-Migrationen sowie Steam-/Nexus-Anpassungen sind offen.

### Nächster Chat: Phase 2A

Nach Beantwortung der offenen Artefakt-Pin-Entscheidung in Abschnitt 10 soll der nächste Chat ausschließlich Phase 2A aus Abschnitt 7 umsetzen. Er liest zuerst `SerpNativeAPI/build.bat`, `Shared/Release/Invoke-Release.bat`, `Shared/Release/Release-Mod.ps1`, `Shared/Release/Release.Common.ps1`, `Shared/Release/release-projects.json`, `_inspect/TestReleaseWrappers.ps1` und die aktuellen Diffs. Phase 2B und 2C bleiben danach getrennte Arbeitspakete; insbesondere werden in Phase 2A noch keine Consumer und keine Steam-/Nexus-Skripte geändert.

Unabhängige Änderungen in `AssassinCombatFix`, `MoveMoatTest` und `_inspect/HostClientPresetTests` gehören nicht zu diesem Plan und dürfen in den Folgephasen nicht verändert oder zurückgesetzt werden.

## 1. Zielbild und Architektur

`SerpNativeAPI` wird als eigenständiger, von anderen Autoren nutzbarer BepInEx-Mod weiterentwickelt. Sie übernimmt:

- native Hooks, Detours, Speicherzugriffe und versionsgebundene Zielauflösung;
- prozessweit eindeutige Broker und Besitzverwaltung;
- typisierte, wiederverwendbare Spielzustandsdienste;
- Konflikterkennung, Diagnosen und Fail-Closed-Verhalten.

Nicht in die API gehören modbezogene Regeln, Einstellungen, Netzwerkprotokolle, UI-Texte oder Dateiformate.

Jeder Verbraucher referenziert die API mit `<Private>false>` und deklariert eine Mindestversion:

    [BepInDependency("SerpNativeAPI_Serp", "<Mindestversion>")]

Native Implementierungen werden vollständig aus Verbrauchermods entfernt, sobald ihre API-Capability getestet ist. Es gibt keinen parallelen Legacy-Fallback.

### Öffentliche Drittanbieter-API

Gezielt `public` werden ausschließlich:

- `ISerpNativeApi` und fachliche Capability-Interfaces;
- unveränderliche Settings-, Kontext-, Snapshot- und Ergebnisobjekte;
- Registrierungs-Handles mit `Enable()`, `Disable()` und `Dispose()`;
- Capability-IDs, Zustände und Diagnosen;
- der dokumentierte Readiness-Einstieg `SerpNativeApi.WhenReady(...)`.

`internal` bleiben:

- RVAs, Adressen, Pointer und native Delegates;
- Patternscanner, PE-Parser und Speicherwriter;
- Detour-, Trampoline- und Seitenschutzobjekte;
- konkrete Capability-Implementierungen und Adapter;
- Testschnittstellen über `InternalsVisibleTo`.

Alle öffentlichen Member erhalten XML-Dokumentation. Der API-Build erzeugt eine XML-Dokumentationsdatei; das separate API-Release enthält DLL, PDB, XML-Dokumentation, `info.json` und ein kompaktes Verbraucherbeispiel. Vor Version 1.0 dürfen Verträge kontrolliert brechen; jede Änderung wird aber mit allen Workspace-Verbrauchern atomar migriert. Ab 1.0 gelten SemVer und Rückwärtskompatibilität als feste öffentliche Zusage.

## 2. Migrationsgrenze für `Shared`

### In die API übernehmen

- `NativePatternResolver.cs`: vollständig in die interne native Infrastruktur integrieren. Verbraucher erhalten keine Scanneroberfläche.
- `ActivePlayerHelper.cs`: als öffentliche, typisierte Player-/Participant-Snapshot-Capability.
- `ActivePlayerKeepReadiness.cs`: als zentraler, prozessweiter Readiness-Dienst mit registrierbaren Handles und unveränderlichen Ergebnissen.
- `RecruitmentHookContext.cs` und `RecruitmentRequestPolicy.cs`: in einen zentralen Recruitment-Broker überführen, der Einschränkungen mehrerer Besitzer deterministisch kombiniert.
- `TroopActionButtonLayout.cs` und `TroopActionButtonLayoutPolicy.cs`: als zentraler HUD-Koordinator, sodass nur ein Hook installiert wird und Mods lediglich ihre Action-Definition registrieren.

### Source-linked belassen

- `DependencyFreeJson.cs`, weil dies für Runtime-Mods ausdrücklich die gemeinsame JSON-Implementierung bleiben muss.
- `PresetLobbyModSettingsViewModel.cs`, `ModSettingsSearch.cs`, `SerpLocalization.cs` und `ToolTipPresentation.cs`, weil sie modbezogene Persistenz, XAML-Typen, Lokalisierungsordner und Suchregistrierungen besitzen.
- `GameModeHelper.cs`, solange die Projektvorgabe das direkte Linken verlangt.
- `DebugLogHelper.cs`, `NumericTextInput.cs`, `WorkshopContentPaths.cs` und `WorkshopUploadStaging.cs`, da sie keine zentrale Hook-/Besitzinstanz benötigen.
- `LobbyLifecycle.cs`, solange kein Release-Mod einen nachgewiesenen zentralen Broker benötigt.

Ein späteres Zentralisieren der Settings-Infrastruktur wäre ein eigenes ABI-/XAML-Projekt und gehört nicht in diese Migration.

## 3. Migration der vorhandenen Release-Mods

| Mod | Geplante API-Nutzung | Im Mod verbleibt |
|---|---|---|
| `ActiveAIVDetector` | Native AIV-Einstiegspunkte und Oracle-Hooks in eine `IAivPlacementCapability`; Beobachterregistrierung über Besitzer-GUID. Gemeinsame Ziele mit `CastlePlanner` werden nur einmal gehookt. | Auswahlbewertung, Trace-Dateien, Lord-/AIVJSON-Auflösung und Diagnose-UI. |
| `AIDefense` | Zunächst nur gemeinsame Player-/Readiness-Dienste, wenn dadurch vorhandene Eigenlogik ersetzt wird. Keine Capability nur zur Erzeugung einer Abhängigkeit. | Verteidigungsregeln und Script-Extender-Eventverarbeitung. |
| `BugfixesAndQoL` | Piloten `IGatehouseDistanceOriginCapability` und `ISelectedUnitCommandCapability`; danach typisierte Capabilities für Assassinen-Pfadfindung/-rekonstruktion, AI-Rekrutierungsbedarf, Steinreserve, Assembly-Point-Patch, Overbuild, Plague-Prüfungen und Troop-Movement-Broker. | Mittelpunkt-Schalter, Feature-Policies, UI, Multiplayerpakete und modbezogene Entscheidungen. |
| `BuildingCosts` | Keine erzwungene Migration; nur spätere gemeinsame Dienste verwenden, wenn echte Duplikation entsteht. | Kostenregeln, Einstellungen und Extender-Events. |
| `BuildingLimit` | Optional zentraler Building-Snapshot-Dienst, falls er nachweislich mit anderen Mods geteilt wird. | Limitregeln, UI und modbezogener Cache. |
| `CastlePlanner` | Native AIV-Funktionen, Human-Start-Hook und versionsabhängige AIV-Zugriffe in dieselbe AIV-Capability wie `ActiveAIVDetector`. | JSON-Import, Bauplanung, Zusatzgebäude und Benutzeroptionen. |
| `CheatMod` | Keine erzwungene API-Abhängigkeit. | Cheatlogik und UI. |
| `CustomCustomTrail` | Keine native Migration; bestehendes Preset-/Trail-System bleibt source-linked. | Trail-Dateien, Snapshots, Missionseinstellungen und Upload-Staging. |
| `ExtraFeatures` | Gatehouse-Timing-Pilot ausschließlich über `IGatehouseTimingCapability`; danach AI-Economy/Market, Monk-Run, Plague-Werte, Church-/Quarry-/Repair-Zugriffe als getrennte fachliche Capabilities. | Timing-Einstellungen, Automatisierung und Gameplay-Policies; keine Entscheidung über den Gatehouse-Distanzursprung. |
| `ImprovedHunters` | Alle zusammengehörigen Hunter-Query-, Chicken-, Visibility-, Path- und Post-Shot-Hooks in eine intern zusammenhängende `IHunterBehaviorCapability`; Beobachter werden gebrokert. | Hunter-Regeln, Wiederholungsbudgets, Diagnoseauswertung und Settings. |
| `LinuxModding` | Keine API-Abhängigkeit, solange keine gemeinsame Capability verwendet wird. | Linux-/Proton-spezifische Anpassungen. |
| `RandomEvents` | Typisierte Capabilities für Vanilla-Eventdispatch, Wildlife-Dispatch, Signpost-Registry und Banditen-Popularitätsmutation. Temporäre Playerkontextänderungen werden transaktional gekapselt. | RNG, Zeitplanung, Netzwerkprotokoll, Save-State und Eventauswahl. |
| `StartConditions` | Gemeinsame Player-/Keep-Readiness-Capability ersetzt den eingebetteten Readiness-Helfer. | Startressourcen, Lobbysettings und Startlogik. |
| `UnitCosts` | Zentraler Recruitment-Broker statt eigener überlappender Action-/Hover-Hooks. | Preisberechnung, Einstellungen und Anzeigeformatierung. |
| `UnitLimit` | Derselbe Recruitment-Broker wie `UnitCosts`; optional gemeinsame Unit-/Siege-Snapshots, wenn diese stabil typisiert werden können. | Limitberechnung, Einstellungen und lokale Zähler. |
| `VanillaAICExporter` | Validierter, hashgebundener `IVanillaAicDataCapability`, der unveränderliche AIC-Snapshots liefert. | JSON-Konvertierung, Manifest und Dateiausgabe. |

Test- und Diagnosemods ohne `release.bat` werden beim jeweiligen Zielaudit berücksichtigt, damit sie keinen migrierten Hook parallel installieren. Dazu gehören insbesondere `AssassinCombatFix`, `HunterQueryTargetDiagnostic`, `EnemyGatePathfindingTest`, `MoveMoatTest` und `APITest`.

Der festgelegte Migrationsumfang umfasst die 16 Mods dieser Tabelle, die jeweils über eine `release.bat` verfügen. Zusätzliche Namen aus `Shared/Release/release-projects.json`, Hilfsprogramme und Projekte außerhalb dieser Tabelle werden nicht allein wegen eines Registry-Eintrags in die API-Migration aufgenommen. `SerpNativeAPI` selbst kommt in Phase 2 als separates Releaseprojekt hinzu, zählt aber nicht zu diesen 16 Verbrauchermods.

## 4. Integration der beiden Pilotfeatures

### `ExtraFeatures`

- `GatehouseTimingPatch` durch `IGatehouseTimingCapability` ersetzen.
- Settings erst nach `SerpNativeApi.WhenReady(...)` anwenden.
- Aktivieren, Ändern und Deaktivieren ausschließlich über `GatehouseTimingSettings`.
- `Enabled=false` stellt ausschließlich die vier konfigurierbaren Vanilla-Distanz-/Delay-Werte über die API wieder her. ExtraFeatures fordert `IGatehouseDistanceOriginCapability` nicht an und verändert den Distanzursprung weder direkt noch indirekt.
- Der Mod hält keine Adresse, keinen Scanner und keinen Memory-Writer mehr.
- Die kurzlebige BepInEx-Komponente darf in `OnDisable()`, `OnDestroy()` oder einem dadurch ausgelösten `Dispose()` weder die API-Registrierung lösen noch native Prozesszustände zurücksetzen. Einstellungen werden durch explizite Settings-Änderungen angewendet; die Capability bleibt prozesslang verwurzelt.

### `BugfixesAndQoL`

- `IGatehouseDistanceOriginCapability` über `SerpNativeApi.WhenReady(...)` beziehen und prozesslang halten.
- Einen standardmäßig aktiven `[SyncHostOnly]`-Schalter `EnableCenteredGatehouseDistanceFix` ergänzen. Der effektive Zustand ist `EnableMod && EnableCenteredGatehouseDistanceFix`.
- Bei aktivem Zustand `BuildingBoundsCenter`, sonst `VanillaBuildingBegin` anwenden. Der Mod enthält hierfür keinen eigenen RVA, Scanner, Seitenschutzaufruf oder Memory-Writer.
- Den eigenen Selected-Unit-NativeDetour durch `ISelectedUnitCommandCapability.TryRegisterBefore(...)` ersetzen.
- Das Registrierungs-Handle prozesslang statisch verwurzeln.
- Die Assassinen-Prüfung und Feldänderungen aus `APITest` sinnvoll in die bestehende Hauptmod-Policy integrieren.
- Die API vermittelt nur den synchronisierten Stop-Befehl; die fachliche Entscheidung bleibt im Mod.
- Nach erfolgreichem Laufzeittest werden der alte Selected-Unit-Detour und die ersetzte Runtime entfernt.

### `APITest`

- Während der Pilotintegration als Vergleichs- und Smoke-Test behalten.
- Erst in der späteren Pilot-/Laufzeitphase zusätzlich auf die getrennte `IGatehouseDistanceOriginCapability` umstellen; der aktuelle Timing-Aufruf aktiviert nach der API-Trennung keinen Mittelpunkt mehr.
- Nach Integration, automatisierten Tests und bestätigtem Spieltest vollständig entfernen.
- Es bleibt weder als veröffentlichter Mod noch als Produktions-Fallback bestehen.
- Wiederverwendbare Assertions wandern nach `_inspect/SerpNativeAPITests`; fachlicher Verbrauchercode wandert in die beiden Hauptmods.

## 5. Thin- und Bundle-Releases

### Artefakte

Jeder API-abhängige Mod erzeugt:

- `ModName-vX.Y.Z.zip`: Standalone-Bundle und bisheriger Hauptname;
- `ModName-vX.Y.Z-thin.zip`: nur der Verbrauchermod;
- je Artefakt eine SHA-256-Datei;
- eine Provenance-Datei mit beiden Artefakten, Dateilisten, API-Version und API-Hash.

Das Bundle enthält genau zwei gleichrangige Pluginordner:

    ModGuid/
    SerpNativeAPI_Serp/

Beide sind zum Entpacken direkt nach `BepInEx/plugins` bestimmt. Die API wird niemals in `ModGuid/`, `Mods/ModGuid/Dependencies` oder einem anderen modbezogenen Unterordner dupliziert.

Nicht von der API abhängige Mods behalten ihr einzelnes bisheriges ZIP. Das separate API-Release enthält ausschließlich `SerpNativeAPI_Serp`.

### Versions- und Duplikatschutz

- Jeder Verbraucher nennt die kleinste benötigte API-Version im `BepInDependency`-Attribut.
- Ein Bundle enthält eine veröffentlichte API-Version, die diese Mindestversion erfüllt.
- Der genaue API-Hash wird in Provenance und Release Notes festgehalten.
- Die API prüft beim Start auf weitere physische `SerpNativeAPI.dll`- oder passende `info.json`-Kopien. Bei uneindeutigen Installationen werden native Capabilities fail-closed gesperrt und alle Fundorte geloggt.
- Archive werden abgelehnt, wenn sie mehr als eine API-DLL, mehrere API-Manifeste oder eine API im Modunterordner enthalten.
- Eine alte manuell über eine neuere API entpackte Bundle-Version kann durch ein statisches ZIP nicht zuverlässig verhindert werden. Die Mindestversionsprüfung sorgt in diesem Fall dafür, dass neuere Verbraucher nicht unsicher mit der herabgestuften API starten.

Der verlinkte Script-Extender-Commit dedupliziert Asset-Mod-Verzeichnisse nach `info.json`-GUID und Version, ändert aber nicht die grundsätzliche BepInEx-Pluginauflösung. Die Paketierung verlässt sich daher weder unter 1.42.0 noch nach dem späteren Extender-Update auf dieses Verhalten: [Script-Extender-Commit 82564a5](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/commit/82564a58ed446a8f39fd07c75428c2b1782ad53b).

## 6. Release-Pipeline

### Einzelne `release.bat`

- `SerpNativeAPI` erhält eine eigene `release.bat` und wird in `Shared/Release/release-projects.json` registriert.
- Die vorhandenen Mod-Wrapper bleiben dünn; die zentrale Logik wird in `Invoke-Release.bat`, `Release-Mod.ps1` und `Release.Common.ps1` erweitert.
- Die API muss vor Verbrauchern veröffentlicht sein.
- Consumer-Releases beziehen das Bundle aus einem bereits veröffentlichten, hashgeprüften API-Artefakt und nicht aus einer möglicherweise veralteten lokalen DLL.
- Setup-Prüfung, Dependency-Records, Release Notes, Release-Index und Provenance erhalten API-Version, Tag, URL und SHA-256.
- GitHub-Releases laden Bundle, Thin-ZIP, beide Prüfsummen und Provenance gemeinsam hoch.
- Ein Wiederaufnehmen eines Drafts prüft, dass die bereits gewählte API-Version und deren Hash unverändert sind.

### `CreateSteamModPack.bat`

`CreateSteamModPack.bat` und `Shared/Steam/Create-SteamModPack.ps1` werden gemeinsam angepasst:

- Die BAT-Konfiguration benennt `SerpNativeAPI` als einmalige Pack-Abhängigkeit.
- Für Child-Mods werden Thin-Artefakte verwendet; Bundle-ZIPs dürfen nicht ungefiltert in den Pack übernommen werden.
- `SerpNativeAPI_Serp` wird genau einmal unter `BepInEx/plugins/SerpNativeAPI_Serp` neben dem Host installiert.
- Die API wird nicht unter jedem `SerpsModsHost/Mods/<GUID>` dupliziert.
- Der Pack-Content-Hash und die Provenance schließen API-Version, Release-Tag, Hash und Dateiliste ein.
- Validierung unterscheidet die Host-SoftDependency der Child-Mods von deren API-HardDependency.
- Der Build bricht ab, wenn eine Child-DLL die erforderliche API-Abhängigkeit nicht besitzt, die gebündelte API zu alt ist oder zusätzliche API-Kopien im Stage-Baum liegen.
- Wiederverwendung eines alten Steam-Packs ist nur erlaubt, wenn auch API-Version und API-Hash unverändert sind.

### `Update-NexusMods.bat`

`Update-NexusMods.bat`, `Update-NexusMods.ps1`, `NexusRelease.Common.ps1` und `Test-NexusRelease.ps1` werden angepasst:

- Nexus lädt `ModName-vX.Y.Z.zip`, also das Bundle, als Main-Datei hoch.
- Die lokale Prüfung erwartet bei API-Verbrauchern genau das Modmanifest und das API-Manifest; bei unabhängigen Mods weiterhin genau ein Manifest.
- Mod-GUID, API-GUID, Versionen, Verzeichnispositionen, HardDependency und Hash werden geprüft.
- Thin-Artefakte werden nicht als Nexus-Main-Datei ausgewählt.
- Vorschau und Abschlussausgabe nennen ausdrücklich `Bundle`, API-Version und API-Hash.
- Bestehende Nexus-Zielzuordnung und Archivierung der vorherigen Main-Version bleiben unverändert.

## 7. Umsetzungsreihenfolge

1. Öffentliche API-Grenze härten, XML-Dokumentation aktivieren und versehentlich öffentliche Implementierungsdetails auf `internal` reduzieren.
2. API-Releaseprojekt sowie Thin-/Bundle-Schema, Provenance und Archiv-Audits implementieren.
3. Gatehouse- und Selected-Unit-Piloten in `ExtraFeatures` und `BugfixesAndQoL` integrieren.
4. Piloten im Spiel testen und danach `APITest` sowie ersetzten Produktions-Altcode entfernen.
5. Prozessweit überlappende Ziele migrieren: AIV, Assassinen, Hunter Query, Troop Movement und Recruitment.
6. Einzelverbraucher mit hohem nativen Risiko migrieren: Random Events, AI Economy/Fixes und Vanilla-AIC-Export.
7. Gemeinsame Player-/Keep-Readiness- und HUD-Koordinatoren aus `Shared` übernehmen.
8. Nicht mehr benötigte `NativePatternResolver`-Links aus allen Verbrauchern entfernen.
9. Steam- und Nexus-Pipelines auf die finalen Artefakte umstellen.
10. Erst nach vollständigen Tests die API-Version und die Versionen der betroffenen Mods atomar erhöhen und releasen.

Jede Capability wird als atomare Welle umgesetzt: API-Vertrag, Implementierung, Tests, alle betroffenen Verbraucher und Audit werden gemeinsam fertiggestellt. Während Tests und Debugging erfolgen keine Versionsänderungen. Vor dem finalen Build werden alle aktiven Versionsstellen modweit geprüft; API-Erweiterungen erhöhen vor 1.0 die Minor-Version, fertige Verbraucheränderungen jeweils die Patch-Version.

### Verbindliche Definition einer Capability-Welle

Eine Capability-Welle gilt nur dann als code-seitig abgeschlossen, wenn alle folgenden Punkte im selben Arbeitsstand erfüllt sind:

- Öffentlicher Vertrag ist fachlich benannt, immutable beziehungsweise handle-basiert und vollständig XML-dokumentiert. Keine Adresse, RVA, Pointer, native Delegate-, Detour-, Patternscanner- oder Memory-Writer-Abstraktion ist öffentlich.
- Native Zielauflösung ist hashgebunden, überprüft ausführbaren Bereich, Funktions-/Blockgrenzen, erwartete Bytes oder Werte und arbeitet bei jeder Abweichung fail-closed. Neue native Erkenntnisse nennen Hash, RVA, Quelle und Evidenz in `ARCHITECTURE.md` sowie `SerpNativeAPI/_inspect/native-surface-audit.csv`.
- Besitz-, Mehrfachregistrierungs-, Callbackreihenfolge-, Reentranz-, Ausnahme- und Lebenszeitverhalten ist ausdrücklich definiert und getestet. Prozesslang benötigte Handles werden statisch oder anderweitig nachweisbar dauerhaft verwurzelt.
- Alle betroffenen Produktions-, Test- und Diagnoseverbraucher derselben Zielstelle wurden auditiert. Migrierte Produktionsmods enthalten anschließend weder den ersetzten Hook noch einen Legacy-Fallback. Ein Diagnosemod darf nur weiterbestehen, wenn es nicht parallel dieselbe Zielstelle besitzt.
- API-Tests, betroffene Modtests, statischer Legacy-Audit, CRLF-Prüfung und die erforderlichen Builds sind erfolgreich. Noch fehlende Spieltests werden als `WARTET AUF SPIELTEST` dokumentiert und dürfen nicht durch Annahmen ersetzt werden.
- Der Übergabeabschnitt enthält die exakten Dateien, Testbefehle/Ergebnisse und den nächsten sicheren Einstieg. Erst danach beginnt eine andere Capability-Welle.

### Phase 2A: API-Releaseprojekt und Artefaktauflösung

Eingang: Phase 1 ist abgeschlossen; die Entscheidungen aus Abschnitt 10 sind beantwortet.

Zu prüfen beziehungsweise zu bearbeiten:

- `SerpNativeAPI/build.bat`, neu anzulegendes `SerpNativeAPI/release.bat` und `SerpNativeAPI/info.json`;
- `Shared/Release/release-projects.json`, `Invoke-Release.bat`, `Release-Mod.ps1` und `Release.Common.ps1`;
- `_inspect/TestReleaseWrappers.ps1`, `Shared/Release/Test-ReleaseSetup.ps1` und bei Bedarf ein neues fokussiertes Testskript unter `_inspect/ReleasePipelineTests`.

Umfang:

1. API als eigenes Releaseprojekt registrieren und denselben dünnen Wrappervertrag wie bei den Mods verwenden.
2. API-Paket strikt auf einen Rootordner `SerpNativeAPI_Serp` mit DLL, PDB, XML-Dokumentation, `info.json` und einem kompakten Beispiel begrenzen. Das Beispiel ist Dokumentation, kein zweites Plugin.
3. Ein Modell für ein explizit aufgelöstes veröffentlichtes API-Artefakt implementieren: Version, Tag, Release-URL, ZIP-Hash und Hash der enthaltenen API-DLL müssen gemeinsam vorliegen und gemeinsam validiert werden.
4. Lokale API-Buildausgaben dürfen nur für Entwicklungsbuilds, nie als stiller Ersatz für ein fehlendes veröffentlichtes Release verwendet werden.
5. Keine GitHub-Veröffentlichung und keine Versionserhöhung durchführen. Tests arbeiten mit lokalen Fixtures beziehungsweise bereits vorhandenen Metadaten, nicht mit einem neu erzeugten echten Release.

Austritt: API-Wrapper-, Setup- und Metadatentests sind grün; das eigenständige API-Archiv ist lokal reproduzierbar; noch kein Consumer wurde verändert. Danach Übergabe auf Phase 2B.

### Phase 2B: Thin, Bundle, Provenance und Archiv-Audit

Eingang: Phase 2A liefert eine verifizierte API-Artefaktauflösung.

Umfang:

1. `Release-Mod.ps1` erzeugt für einen tatsächlich als API-Consumer klassifizierten Mod Haupt-Bundle und Thin-ZIP; unabhängige Mods behalten exakt den bisherigen Einzelartefaktpfad.
2. Consumer-Klassifizierung erfolgt nicht allein über einen Projektnamen. Mindestens Projekt-Referenz mit `<Private>false>`, BepInEx-HardDependency und auflösbare Mindestversion müssen übereinstimmen.
3. Bundle-Staging extrahiert das verifizierte veröffentlichte API-Artefakt neben den Consumerordner. Rekursives Einbetten oder Übernehmen einer lokalen API-DLL ist verboten.
4. Provenance wird versionsgebunden erweitert. Für neue Multi-Artefakt-Releases enthält sie mindestens `Bundle`, `Thin`, jeweilige SHA-256/Größe/Dateiliste sowie ein API-Objekt mit GUID, Version, Tag, URL, Archivhash und DLL-Hash. Bestehende Leser wie Nexus dürfen nicht durch eine lautlose Umdeutung des bisherigen `Package`-Felds brechen; Schemaänderung und Kompatibilitätsfeld werden ausdrücklich getestet.
5. ZIP-Audit expandiert jedes Archiv getrennt und vergleicht relative Pfade, Größen und Hashes bytegenau mit der jeweiligen Provenance. Pfadtraversal, doppelte API-Manifeste/-DLLs, API im Consumerunterordner und Runtime-Dateien führen zum Abbruch.
6. Tests verwenden einen künstlichen API-Consumer und einen unabhängigen Mod als Fixtures, damit Phase 2 keine echte Consumer-HardDependency vortäuscht.

Austritt: positive und negative Fixturetests decken Thin, Bundle, unabhängigen Mod, manipulierten Hash, zu alte API, falsche Verzeichnisstruktur und Draft-Wiederaufnahme mit abweichendem API-Pin ab. Danach Übergabe auf Phase 2C.

### Phase 2C: Laufzeit-Duplikatschutz

Eingang: Das Paketlayout aus Phase 2B ist festgelegt.

Umfang:

1. Die API durchsucht beim Start den kanonischen BepInEx-Pluginbaum nach physischen `SerpNativeAPI.dll`-Kopien und Manifesten mit GUID `SerpNativeAPI_Serp`.
2. Wenn `info.json` in der Unity-Runtime geparst wird, muss `Shared/DependencyFreeJson.cs` source-linked oder intern gleichwertig integriert werden; `System.Text.Json`, Newtonsoft und Regex als JSON-Ersatz sind nicht zulässig.
3. Nur genau eine zusammengehörige DLL-/Manifestinstallation am geladenen API-Standort ist gültig. Alle normalisierten Fundorte werden mit Zeitstempel protokolliert. Bei Mehrdeutigkeit erreicht die API einen terminalen unavailable-Zustand, benachrichtigt bereits registrierte `WhenReady`-Callbacks und führt keine native Mutation aus.
4. Die Erkennung erhält eine intern testbare, pfadparametrisierte Kernfunktion. Tests verwenden temporäre Bäume für gültige Installation, doppelte DLL, doppeltes Manifest, GUID-Manifeste mit anderem Dateinamen, fehlendes Manifest und API im Consumerunterordner.

Austritt: Duplikat- und Readiness-Tests sind grün, der Surface-Audit bleibt unverändert grün und der API-Build enthält keine neue öffentliche Infrastruktur. Danach ist Phase 3A das nächste Arbeitspaket.

### Phase 3A: Gatehouse-Timing-Pilot in `ExtraFeatures`

Eingang: Phasen 2A–2C sind abgeschlossen; `IGatehouseTimingCapability` ist getrennt von `IGatehouseDistanceOriginCapability` grün.

Primäre Dateien: `ExtraFeatures.csproj`, `src/ExtraFeaturesPlugin.cs`, `src/ExtraFeaturesRuntime.cs`, `src/ExtraFeaturesViewModel.cs`, `src/GatehouseAutomationRuntime.cs` und der zu ersetzende `src/GatehouseTimingPatch.cs`.

Umfang:

1. API-Referenz mit `<Private>false>` und HardDependency auf die kleinste tatsächlich benötigte API-Version hinzufügen.
2. Ausschließlich `IGatehouseTimingCapability` über `SerpNativeApi.WhenReady(...)` beziehen. Pending, unavailable und capability-spezifische Fehler verständlich loggen; andere ExtraFeatures-Funktionen bleiben funktionsfähig.
3. Settings und UI-Grenzen aus den öffentlichen `GatehouseTimingValues` beziehen. Änderungen über ein einziges `GatehouseTimingSettings`-Objekt transaktional anwenden.
4. Den alten `GatehouseTimingPatch`, seine Projektaufnahme und nur die dadurch überflüssigen nativen Abhängigkeiten entfernen. `Shared/NativePatternResolver.cs` noch nicht pauschal entlinken, solange andere ExtraFeatures-Komponenten ihn benötigen.
5. Kein Cleanup im frühen BepInEx-`OnDestroy`/`Dispose`, das die prozesslange API-Nutzung beendet. Deaktivierung erfolgt durch Settings und stellt nur die vier dokumentierten Vanilla-Werte wieder her. ExtraFeatures fordert die Distance-Origin-Capability nie an.

Austritt: API-/ExtraFeatures-Tests und Build grün; statischer Audit findet die Gatehouse-RVAs/Patterns/Writes nicht mehr in `ExtraFeatures`; Bundle-/Thin-Fixture für den nun echten Consumer grün. Status anschließend `WARTET AUF SPIELTEST`, aber Phase 3B darf code-seitig separat beginnen.

### Phase 3B: Gatehouse-Distanzursprung- und Selected-Unit-Piloten in `BugfixesAndQoL`

Eingang: Phasen 2A–2C sind abgeschlossen. Phase 3A darf bereits auf Spieltest warten.

Primäre Dateien: `BugfixesAndQoL.csproj`, `src/BugfixesAndQoLPlugin.cs`, `src/BugfixesAndQoLRuntime.cs`, `src/BugfixesAndQoLViewModel.cs`, Modsettings-XAML und Locales, `src/AssassinClimbCancellationRuntime.cs`, `src/AssassinClimbCancellationPolicy.cs` sowie APITest als spätere Vergleichsquelle.

Umfang:

1. API-Referenz und HardDependency wie in Phase 3A hinzufügen.
2. `IGatehouseDistanceOriginCapability` über `SerpNativeApi.WhenReady(...)` beziehen und prozesslang halten. Den standardmäßig aktiven `[SyncHostOnly]`-Schalter `EnableCenteredGatehouseDistanceFix` einschließlich Preset, Reset, XAML, Suche, Tooltip und aller Locales ergänzen.
3. Effektiv `EnableMod && EnableCenteredGatehouseDistanceFix` auswerten und explizit `BuildingBoundsCenter` beziehungsweise `VanillaBuildingBegin` anwenden. BugfixesAndQoL enthält dafür keinen eigenen RVA, Scanner, Seitenschutzaufruf oder Memory-Writer.
4. `ISelectedUnitCommandCapability.TryRegisterBefore(...)` verwenden. Das Handle prozesslang statisch verwurzeln; der frühe BepInEx-Lifecycle darf es nicht disposen.
5. Die API liefert nur den immutable Pre-Event-Kontext des Script Extenders. Assassinenzustände, Auswahlbitmap, Einheitenprüfung und Feldänderungen bleiben Modpolicy.
6. Den ersetzten Selected-Unit-`NativeDetour`, seine RVA-/Patternauflösung und nur dadurch unnötige Imports entfernen. Andere native Bugfixes und der eventuell weiterhin benötigte `NativePatternResolver`-Link bleiben unberührt.
7. Capability- und Callbackfehler isoliert und mit Millisekunden-Zeitstempel loggen; andere Bugfixes und Vanilla-Verarbeitung bleiben funktionsfähig.

Austritt: API-/Bugfixes-Tests, Host-/Client-Presettests, XAML-Audit und Build grün; statischer Audit bestätigt den fehlenden eigenen Gatehouse-Mittelpunktpatch und das Verschwinden genau des ersetzten Selected-Unit-Detours; gemeinsam installierte Pilotmods initialisieren die API nur einmal. Status danach `WARTET AUF SPIELTEST`.

### Phase 4: Pilot-Spieltest und Bereinigung

Diese Phase erfordert Benutzerinteraktion und darf nicht in einem unbeaufsichtigten Codechat als bestanden markiert werden.

1. APITest temporär auf beide getrennten Gatehouse-Capabilities umstellen und damit zunächst den Mittelpunktblock sowie Timing isoliert prüfen. Vor der Abnahme der Hauptmods APITest deaktivieren oder aus der Spielinstallation entfernen, damit es keine Capability reserviert.
2. Einen Testbuild für API und beide Consumer bereitstellen, ohne Versionserhöhung.
3. Der Benutzer führt die in Abschnitt 8 beschriebenen Singleplayer-/Host-/Client-Tests aus. Der Chat wertet Host- und, wenn erreichbar, Client-Log anhand des Startmarkers und der eigenen Millisekunden-Zeitstempel aus.
4. Bei Fehlern werden API oder Consumer korrigiert; der alte Produktionshook wird nicht als Fallback wieder eingeführt.
5. Erst nach ausdrücklicher Bestätigung beider Piloten wiederverwendbare Assertions nach `_inspect/SerpNativeAPITests` verschieben und `APITest` vollständig entfernen. Vor der Entfernung noch einmal workspaceweit prüfen, dass kein Test oder Build darauf verweist.
6. `ARCHITECTURE.md`, `SerpNativeAPI/_inspect/native-surface-audit.csv` und dieser Übergabeabschnitt erhalten den bestätigten Laufzeitstand.

Austritt: beide Piloten bestätigt, `APITest` entfernt, keine parallelen Alt-Hooks und keine offenen Pilotfehler. Danach Phase 5A.

### Phase 5: Überlappende native Ziele, je Unterphase ein eigener Chat

- **5A AIV:** `ActiveAIVDetector` und `CastlePlanner` gemeinsam auditieren; identische Einstiegspunkte in `IAivPlacementCapability` bündeln, Beobachter und exklusiven Besitzer klar trennen. Diagnose-/JSON-/Planungslogik bleibt in den Mods.
- **5B Assassinen:** `BugfixesAndQoL` und `AssassinCombatFix` einschließlich vorhandener Benutzeränderungen auditieren. Nur tatsächlich identische oder untrennbare native Ziele zentralisieren; Pfadfindungs-, Rekonstruktions- und Combat-Policies bleiben getrennt.
- **5C Hunter Query:** `ImprovedHunters` und `HunterQueryTargetDiagnostic` gemeinsam migrieren. Query-, Visibility-, Chicken-, Path- und Post-Shot-Ziele nur dann in einer Capability gruppieren, wenn ihre Lebenszeit und Fehlergrenze fachlich zusammengehören.
- **5D Troop Movement:** `BugfixesAndQoL` und `ExtraFeatures` einschließlich vorhandener Bridges auditieren. Ein Broker besitzt den gemeinsamen Hook; Mods registrieren typisierte Policies/Beobachter.
- **5E Recruitment:** `UnitCosts` und `UnitLimit` atomar auf einen Recruitment-Broker migrieren. Kombinationsregel für Preis- und Limitrestriktionen muss deterministisch, reentranzsicher und unabhängig von Registrierungsreihenfolge getestet sein.

Für jede Unterphase gelten die Capability-Definition-of-Done und ein eigener Spieltest-Stopp. Keine Unterphase darf wegen bloßer thematischer Nähe zusätzliche native Ziele aufnehmen.

### Phase 6: Einzelverbraucher mit hohem Risiko, je Unterphase ein eigener Chat

- **6A Random Events:** Vanilla-/Wildlife-Dispatch, Signpost-Registry und Popularitätsmutation als getrennte Capabilities behandeln. Temporäre Playerkontextänderung transaktional rücksetzen; Netzwerk, RNG, Save-State und Zeitplanung bleiben im Mod.
- **6B AI Economy/Fixes:** `ExtraFeatures` und `BugfixesAndQoL` zielstellenweise auditieren. Market, Economy, Recruitmentbedarf, Steinreserve, Assembly Point, Overbuild, Plague und Repair nicht in eine monolithische „AI“-Capability mischen; nur identische Ziele teilen.
- **6C Vanilla AIC Export:** `VanillaAICExporter` auf einen hashgebundenen immutable Snapshot umstellen. API liest und validiert; JSON-Konvertierung, Manifest und Dateien bleiben im Exporter.

Jede Unterphase beginnt mit frischem Native-Hashabgleich und endet mit eigenem statischen Altcode-Audit und Spielteststatus.

### Phase 7: Gemeinsame Dienste aus `Shared`, je Unterphase ein eigener Chat

- **7A Player/Readiness:** `ActivePlayerHelper.cs` und `ActivePlayerKeepReadiness.cs` nach Nutzeraudit als typisierte Snapshots beziehungsweise zentralen Readiness-Dienst übernehmen. Zuerst alle Verbraucher ermitteln; mindestens `StartConditions` und optional `AIDefense` nur bei echter Ersetzung migrieren.
- **7B HUD-Koordination:** `TroopActionButtonLayout.cs` und `TroopActionButtonLayoutPolicy.cs` in einen zentralen Koordinator überführen. Mods registrieren immutable Action-Definitionen und Handles; XAML-, Texte-, Commands- und Settingsbesitz bleibt im Consumer.
- **7C Shared-Bereinigung:** Erst wenn 7A/7B getestet sind, ersetzte Source-Links aus den betroffenen Projekten entfernen. Die ausdrücklich source-linked zu belassenden Dateien aus Abschnitt 2 dürfen nicht mitbereinigt werden.

### Phase 8: NativePatternResolver- und Legacy-Gesamtaudit

1. Alle Release-, Test- und Diagnoseprojekte nach `NativePatternResolver`, bekannten migrierten Patterns/RVAs, `NativeDetour`, `VirtualProtect`, `Marshal.Write*` und capability-spezifischen Konstanten durchsuchen.
2. Jeden Treffer einer noch nicht migrierten Zielstelle oder einer abgeschlossenen Capability zuordnen. Nur letztere entfernen; ein globales Löschen des Helpers ist erst erlaubt, wenn wirklich kein legitimer Verbraucher übrig ist.
3. Projektdateien auf tote References/Compile-Links und Ausgabeordner auf versehentlich mitkopierte API-DLLs prüfen.
4. Die 16 in Abschnitt 3 festgelegten Release-Mods prüfen. Andere Registry-Einträge und Hilfsprojekte sind nicht Teil dieses Consumer-Gesamtaudits.

Austritt: maschinenlesbarer Auditbericht ohne ungeklärten Treffer für bereits migrierte Capabilities; alle vorhandenen Releaseprojekte bauen gegen passende API-Stände.

### Phase 9: Finale Releasekanäle, je Unterphase ein eigener Chat

- **9A GitHub-Integration:** Die in Phase 2 mit Fixtures gebaute Pipeline gegen alle nun echten Consumer prüfen. Haupt-Bundle, Thin-ZIP, beide Prüfsummen, Provenance, Release Notes, Dependency-Records, Release-Index und Draft-Resume müssen denselben API-Pin tragen. Es wird weiterhin nichts veröffentlicht.
- **9B Steam-Pack:** `CreateSteamModPack.bat` und `Shared/Steam/Create-SteamModPack.ps1` gemeinsam ändern. `SERPS_STEAM_MODS` bleibt die Child-Liste; eine separate einmalige API-Konfiguration wird ergänzt. Child-Artefakte sind thin, API liegt genau einmal neben dem Host. `CreateSteamModPack.bat /validate /nopause` und negative Stage-Fixtures müssen grün sein.
- **9C Nexus:** `Update-NexusMods.bat`, `Shared/Release/Update-NexusMods.ps1`, `NexusRelease.Common.ps1` und `Test-NexusRelease.ps1` gemeinsam ändern. Nexus wählt ausschließlich das Haupt-Bundle; GUIDs, zwei Manifeste, Positionen, Dependency und Hash werden vor Upload geprüft. Tests dürfen keinen echten Upload ausführen.

### Phase 10: Versionierung, Endabnahme und Veröffentlichung

1. Vor jeder Änderung Benutzerbestätigung einholen, dass Implementierung und Spieltests final sind und Versionen erhöht werden sollen.
2. Aktive Versionsstellen je betroffenem Projekt modweit inventarisieren. API vor 1.0 Minor erhöhen; fertig migrierte Consumer Patch erhöhen; Manifest, Plugin-Konstanten, Assembly-/Paket-/Releasemetadaten atomar anpassen.
3. Nach der Änderung alte und neue Version erneut modweit suchen und historische Changelogs von aktiven Abweichungen unterscheiden.
4. Vollständige Testmatrix und Builds der 16 festgelegten Release-Mods sowie der API ausführen; CRLF, XAML/Locale/Tooltips und Artefaktaudits wiederholen.
5. Veröffentlichungsreihenfolge: API zuerst; danach Consumer-Bundles/Thin-Artefakte; anschließend Steam-Pack; zuletzt Nexus-Main-Dateien. Jeder Schritt verifiziert URL und Hash des vorherigen Schritts.
6. Bei einem Fehler nicht mit der nächsten Ebene fortfahren. Drafts nur mit identischem Commit und identischem API-Pin wiederaufnehmen.
7. Erst nach erfolgreicher Veröffentlichung README-Aktualisierungen separat anbieten; sie gehören nicht automatisch zu dieser Phase.

## 8. Tests und Abnahmekriterien

### API-Tests

- bekannte und unbekannte DLL-Hashes;
- unabhängiger Ausfall einzelner Capabilities;
- Besitzerkonflikte und idempotente Registrierung;
- deterministische Callbackreihenfolge;
- Reentranz und Callbackausnahmen;
- externe Mutation vor oder während eines Writes;
- vollständiger Rollback und Seitenschutzwiederherstellung;
- Handle-Lebenszyklus;
- doppelte physische API-Installationen;
- öffentliche Surface-Prüfung: keine Pointer-, RVA-, Detour- oder Memory-Writer-Typen.

### Consumer-Tests

- alle vorhandenen modbezogenen Testprojekte;
- `_inspect/SerpNativeAPITests`;
- `_inspect/HostClientPresetTests` für jede berührte Settings-Mod;
- XAML-Audit, Locale-Key-Parität, Tooltips und CRLF;
- Build aller 16 Release-Mods mit fehlender, zu alter, passender und neuerer API;
- statischer Audit, dass migrierte Mods keine betreffenden Patterns, RVAs, `NativeDetour`, `VirtualProtect` oder direkten Writes mehr enthalten.

### Laufzeit-Smoke-Tests

- `ExtraFeatures`: Gatehouse-Werte aktivieren, ändern, deaktivieren und Vanilla-Werte bestätigen.
- `BugfixesAndQoL`: Mittelpunktfix an/aus schalten und gleiche Schließdistanz an kleinen/großen Toren von beiden Seiten und diagonal bestätigen; außerdem Assassinen während Kletterzuständen per synchronisiertem Stop abbrechen. Singleplayer sowie Host/Client prüfen.
- Kombinationsmatrix: nur BugfixesAndQoL ergibt Mittelpunkt mit Vanilla-Werten; nur ExtraFeatures ergibt konfigurierbare Werte mit Vanilla-Ursprung; beide zusammen ergeben Mittelpunkt mit konfigurierbaren Werten.
- mehrere API-Verbraucher gemeinsam laden und genau eine API-Initialisierung bestätigen;
- fehlende/alte API erzeugt verständliche BepInEx-Abhängigkeitsfehler;
- doppelte API-Dateien werden erkannt und native Mutationen bleiben gesperrt;
- Prüfung zunächst mit Script Extender 1.42.0, danach erneut mit der vorgesehenen neueren Extender-Version.

### Release-Audits

- Thin-ZIP enthält genau einen Modordner und keine API.
- Bundle enthält genau einen Modordner und genau einen zentralen API-Ordner.
- Steam-Pack enthält die API genau einmal.
- Nexus wählt das Bundle als Main-Datei.
- GitHub enthält Thin und Bundle mit korrekten Hashes.
- ZIP-Entpackaudit entspricht bytegenau der Provenance.
- Keine `.msgpack`, Logs, temporären Dateien oder lokalen Lobbysettings gelangen in ein Artefakt.

### Mindestprüfungen je Arbeitspaket

Diese Liste ergänzt die capability-spezifischen Tests; ein Folgechat muss nicht jedes Mal die gesamte Endabnahme ausführen.

| Arbeitspaket | Mindestens auszuführen |
|---|---|
| Phase 2A | API-Build; `_inspect/TestReleaseWrappers.ps1`; `Shared/Release/Test-ReleaseSetup.ps1 -ModName SerpNativeAPI`; fokussierte Metadaten-/API-Artefakt-Fixtures |
| Phase 2B | positive und negative Thin-/Bundle-/Provenance-Fixtures; bytegenauer Entpackaudit; unabhängiger Mod unverändert |
| Phase 2C | `_inspect/SerpNativeAPITests`; Duplikatbaum-Fixtures; Public-Surface- und XML-Dokumentationsaudit |
| Phase 3A | `_inspect/SerpNativeAPITests`; relevante ExtraFeatures-Tests; `_inspect/HostClientPresetTests` nach dessen README und erhöhtem EXE-Aufruf; Gatehouse-Legacy-Suche; API- und ExtraFeatures-Build |
| Phase 3B | `_inspect/SerpNativeAPITests`; relevante Bugfixes-/Assassinen-Tests; `_inspect/HostClientPresetTests`; XAML-/Locale-Audit; Gatehouse-Origin- und Selected-Unit-Legacy-Suche; API- und Bugfixes-Build |
| Phase 4 | dokumentierte manuelle Smoke-Tests und Logauswertung; danach Tests erneut, bevor `APITest` entfernt bleibt |
| Phase 5–7 | API-Tests plus alle Tests jedes berührten Consumers/Diagnosemods, Surface-Audit, capability-spezifische Legacy-Suche und jeweilige Builds |
| Phase 8 | workspaceweiter statischer Audit und Builds aller 16 festgelegten Release-Mods gegen fehlende, zu alte, passende und neuere API, soweit sie tatsächlich Consumer sind |
| Phase 9A | Release-Fixtures, Wrappertest, Setup-/Dependency-/Draft-Resume-Tests ohne Upload |
| Phase 9B | Steam-Pack-Tests und `CreateSteamModPack.bat /validate /nopause`, ohne Workshop-Veröffentlichung |
| Phase 9C | `Shared/Release/Test-NexusRelease.ps1` und lokale Vorschau/Validierung, ohne Nexus-Upload |
| Phase 10 | komplette Matrix aus Abschnitt 8, Versionskonsistenzsuche, Builds, Artefaktaudits und anschließend freigegebene Veröffentlichungen |

Für klassische .NET-Framework-Testprojekte nicht `dotnet run` verwenden. Der MSBuild-Pfad wird aus dem jeweiligen `build.bat` beziehungsweise der dokumentierten Testanleitung übernommen. Ein erwarteter Negativtest muss Exitcode und konkrete Fehlermeldung prüfen; bloßes Fehlschlagen gilt nicht als ausreichender Test.

## 9. Dokumentation und Annahmen

- Der Plan selbst wird als `SerpNativeAPI/MIGRATION_PLAN.md` mit CRLF gespeichert.
- `SerpNativeAPI/README.md` und Mod-READMEs werden während der Planung nicht verändert. Nach finaler Implementierung wird separat gefragt, ob Installation, öffentliche API und neue Abhängigkeiten dort dokumentiert werden sollen.
- `SerpNativeAPI/ARCHITECTURE.md` und `SerpNativeAPI/_inspect/native-surface-audit.csv` werden bei jeder implementierten Capability aktualisiert.
- Die kanonische installierte `CrusaderDE.dll` und die Native-Baseline werden vor jeder neuen nativen Capability erneut hashgeprüft.
- Ein Mod erhält nur dann eine API-HardDependency und Bundle-Artefakte, wenn er tatsächlich mindestens eine Capability verwendet.
- Source-Linking bleibt für reine Helfer zulässig, aber niemals für API-Broker, native Besitzerregister oder mutierende Capabilities.

## 10. Entscheidungen und festgeschriebene Vorgaben

### Bereits entschieden

- Releaseform ist **Thin + Bundle**; das Bundle behält den bisherigen Hauptdateinamen.
- Die API ist ein eigenständiges BepInEx-Plugin und darf von externen Modautoren über die dokumentierte öffentliche Oberfläche verwendet werden.
- Bundles enthalten die API einmal als gleichrangigen Ordner; Source-Linking der API-Implementierung in jeden Mod ist ausgeschlossen.
- Der Consumer-Migrationsumfang sind die 16 Mods aus Abschnitt 3 mit `release.bat`. Hilfsprogramme, Diagnosemods und zusätzliche Registry-Namen zählen nicht automatisch dazu.
- Script-Extender-Deduplizierung wird weder mit 1.42.0 noch nach einem Update als Schutz für doppelte BepInEx-Plugins vorausgesetzt.
- Während Entwicklung keine Versionsänderungen, keine README-Anpassung und keine echte Veröffentlichung.

### Vor Phase 2A noch zu entscheiden: API-Artefakt-Pin

Für reproduzierbare Bundles muss feststehen, wo die exakt einzubettende veröffentlichte API-Version festgeschrieben wird.

Empfehlung: ein zentraler, expliziter API-Pin in der Releasekonfiguration für eine zusammengehörige Releasewelle. Er enthält Version, Tag, Release-URL, Archiv-SHA-256 und erwarteten DLL-SHA-256. Jeder Consumer behält zusätzlich seine eigene kleinste kompatible Version im `BepInDependency`-Attribut. Ein Draft übernimmt den aufgelösten Pin in seinen Zustand und darf mit einem anderen Pin nicht fortgesetzt werden.

Alternative: ein eigener exakter API-Pin pro Consumer. Das erlaubt verschiedene Bundle-Versionen gleichzeitig, erhöht aber Pflegeaufwand, Duplikatrisiko und die Zahl der atomar zu prüfenden Konfigurationsstellen.

Nicht zulässig ist „bei jedem Release automatisch das neueste passende API-Release wählen“, ohne die konkrete Auswahl vor Build und Draft-Wiederaufnahme dauerhaft festzuschreiben.
