# Updateplan: SHCDE Script Extender 1.41.0 auf 1.42.0

Stand: 18. August 2026
Status: Analyse abgeschlossen; Workspace-Umsetzung, Tests, Builds und Ingame-Abnahme offen

## 1. Ziel und gepruefte Basis

Dieser Plan beschreibt die notwendigen Workspace-Anpassungen fuer den Wechsel vom Script Extender 1.41.0 auf 1.42.0. Geprueft wurden alle acht Commits in `v1.41.0..v1.42.0`, die finalen Quell- und Dokumentationsdiffs sowie alle auffindbaren Verwendungen der geaenderten APIs und Settingspfade im Workspace.

- Alter Release-Tag: `v1.41.0`, Commit `065184ccbb0c3c2f8c35a0a5467fff4e768e336f`
- Neuer Release-Tag: `v1.42.0`, Commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Umfang: 15 geaenderte Dateien, 289 Einfuegungen, 136 Loeschungen
- Der lokale kanonische Fork steht bereits auf dem 1.42.0-Releasecommit. Build-Ausgabe, `mod_output` und installierte `SHCDESE.dll` sind SHA-256-identisch: `23547134B21C42BC345465ADF4370881DEE1A53301BE03E3857CAC0A913E60A6`.
- Kanonische installierte Spiel-DLL: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- SHA-256 der installierten Spiel-DLL: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- 1.42.0 enthaelt kein Spielversionsupdate und keine fuer die Workspace-Mods relevante native Layoutaenderung. Eine neue RVA-/Layoutanalyse ist deshalb nicht erforderlich.
- Der Workspace war vor dem Anlegen dieses Plans sauber. Bestehende Modquellen wurden fuer die Analyse nicht veraendert und nicht gebaut.

## 2. Kurzfazit

1.42.0 ist fuer die vorhandenen Mods weitgehend binaer- und quellkompatibel. Es gibt keine entfernte oeffentliche API und keinen neuen zwingenden nativen Migrationspfad. Direkte Workspace-Arbeit ist dennoch in zwei gemeinsamen Dateien erforderlich:

1. `Shared/PresetLobbyModSettingsViewModel.cs`: Die neue Settings-Routinglogik ignoriert UI-only-Benachrichtigungen und bewahrt auf Clients lokal gespeicherte Hostwerte. Dadurch werden mehrere 1.41-Sanitisierungswrites ueberfluessig. Das eigene Zwei-Preset-/Trailformat bleibt jedoch erforderlich und darf nicht durch eine pauschale Umstellung auf `[PersistLocal]` oder `[DoNotPersist]` ersetzt werden.
2. `Shared/GameModeHelper.cs`: Der direkte Zugriff auf `Platform_Multiplayer.MPGameActive` kann durch die neue offizielle API `GameNetworkAPI.IsMultiplayerGame()` ersetzt werden. Die zusaetzlichen Signale des Helpers bleiben notwendig, insbesondere fuer Lobby, Multiplayer-Saves und belastbare Diagnosen.

Diese beiden Shared-Dateien werden von zehn Produktionsmods kompiliert. Deshalb muessen diese Mods nach Abschluss aller gemeinsamen Pruefungen neu gebaut, installiert und mit neuen hashrelevanten Versionswerten versehen werden:

- `BugfixesAndQoL`
- `BuildingCosts`
- `BuildingLimit`
- `ExtraFeatures`
- `ImprovedHunters`
- `RandomEvents`
- `CastlePlanner`
- `StartConditions`
- `UnitCosts`
- `UnitLimit`

Weitere wichtige Folgen:

- Der Lobby-Modhash ist nun tatsaechlich ein voller 64-Bit-xxHash. Die im 1.41-Plan dokumentierte Upstream-Auffaelligkeit ist damit behoben.
- Der Hash besteht weiterhin nur aus sortierten `GUID@Version`-Eintraegen fuer BepInEx- und Asset-Mods, nicht aus den DLL-Inhalten. Geaenderte Modbinaerdateien benoetigen deshalb neue, auf Host und Client identische Versionswerte.
- `NetworkMode` wird auch in 1.42.0 noch nicht in der Hashberechnung ausgewertet. Keine massenhafte Metadatenumstellung im Rahmen dieses Updates.
- `UnityMainThreadDispatcher.EnqueueAndWait<T>` ist nur als `[Obsolete]` und im Editor als verborgen markiert, nicht entfernt. Kein Workspace-Code ruft die Methode auf.
- `MPTest` verwendet `IsNetworkedEnvironment()` bewusst fuer den niedrigen Transport-/Infrastrukturzustand. Diese Stellen duerfen nicht mechanisch durch `IsMultiplayerGame()` ersetzt werden.

## 3. Betroffenheitsmatrix

| Bereich/Mod | Prioritaet | Befund | Geplante Folge |
| --- | --- | --- | --- |
| `Shared/PresetLobbyModSettingsViewModel.cs` | hoch | Der 1.41-Workaround schreibt bei UI-only-Access-Notifications erneut die komplette `.msgpack`. 1.42 ignoriert solche Notifications bereits. Das erzeugt nun unnoetige doppelte Schreibvorgaenge, waehrend die gemeinsame Presetmetadaten- und Trail-Sanitisierung weiterhin gebraucht wird. | UI-only-Sanitisierungswrites gezielt entfernen; echte Settings-, Preset- und Trailpfade beibehalten; Kommentare auf den 1.42-Vertrag aktualisieren. |
| Zehn Mods mit gemeinsamer Presetbasis | hoch | 101 `[SyncHostOnly]`-, 20 `[SyncPerPlayer]`- und sechs `CastlePlanner`-Properties mit `[Shared.PresetLocal]` laufen durch denselben gemeinsamen Controller. | Einmal zentral korrigieren, danach alle zehn Mods testen, Versionen atomar anheben und genau einmal bauen/installieren. |
| `CastlePlanner` | hoch | Reiner Clientmod mit elf `[SyncPerPlayer]`- und sechs `[Shared.PresetLocal]`-Properties. Der neue Extender kennt das eigene `[PresetLocal]` absichtlich nicht und behandelt es als UI-only. | Nachweisen, dass `[PresetLocal]` weiterhin ausschliesslich durch das gemeinsame Presetsystem gespeichert, nie gesendet und nach Neustart korrekt geladen wird. Keine pauschale Ersetzung durch `[PersistLocal]`. |
| `CustomCustomTrail` | hoch | Kompiliert die Shared-Datei nicht selbst, wendet aber Host-only-Missionssnapshots auf die zehn registrierten ViewModels an. | `TrailMissionSettingsCoordinator` sowie 18/18 vorhandene Tests gegen die geaenderte Persistenzreihenfolge erneut pruefen; weiterhin nur `[SyncHostOnly]` erfassen. |
| `Shared/GameModeHelper.cs` | mittel | Nutzt den nun offiziell gekapselten Wert noch direkt als `Platform_Multiplayer.MPGameActive`. | Auf `GameNetworkAPI.IsMultiplayerGame()` umstellen, ohne Lobby-/Member-/Director-/Save-Fallbacks zu entfernen. |
| Gesamtes Multiplayer-Testprofil | mittel | Der volle 64-Bit-Hash aendert die Lobby-Kompatibilitaet. 1.41- und 1.42-Installationen sowie unterschiedliche Modversionen sollen sich nicht sehen. | Host und Client koordiniert auf 1.42 und identische Modversionen bringen; kompletten 16-stelligen Hash und Eintragszahl logseitig vergleichen. |
| `MPTest` | niedrig | Fuenf Vorkommen von `IsNetworkedEnvironment()` pruefen Transportverfuegbarkeit, lokalen Player-Fallback und den nativen Chore-Probeaufbau, nicht die fachliche Spielmodusklassifikation. | Nicht mechanisch migrieren; nur Compile- und Transport-Smoke gegen 1.42. Chore-Migration bleibt fuer 1.50.0 reserviert. |
| `DispatcherLifecycleProbe` | niedrig | Verwendet Dispatcherinstanz, `Dispatch` und Queuepfade, aber nicht das neu als obsolet markierte `EnqueueAndWait<T>`. | Keine Codeaenderung; optionaler Compile-Smoke. Die bekannte fehlende Persistenz des Dispatcher-Framehosts bleibt unveraendert. |
| `ActiveAIVDetector`, `AIDefense`, `AIVPlacementLobby`, `HunterQueryTargetDiagnostic`, `MultiplayerLeaveFix`, `VanillaAICExporter` | keine direkte Anpassung | Referenzieren den Extender, nutzen aber keine in 1.42 geaenderte oeffentliche Semantik. `AIVPlacementLobby`-ViewModels sind keine registrierten Lobby-Modsettings. | Kein Rebuild nur fuer 1.42 erforderlich; Smoke-Test, falls sie im gemeinsamen Profil geladen werden. |
| `CustomCustomTrail`-fremde Tools und Datenprojekte | keine | Keine relevante SHCDESE-API-Nutzung gefunden. | Keine Aenderung. |

## 4. Vollstaendige Commitpruefung

| Commit | Aenderung | Workspace-Auswirkung |
| --- | --- | --- |
| `850eb8f` | `GameNetworkAPI.IsMultiplayerGame()` ergaenzt; gibt direkt `Platform_Multiplayer.MPGameActive` zurueck. Kommentar von `IsNetworkedEnvironment()` stellt klar, dass auch lokaler Skirmish als networked erscheinen kann. RE-Datenbank aktualisiert. | `Shared/GameModeHelper` kann den direkten Plattformzugriff durch die offizielle API ersetzen. Die bestehende Trennung von niedrigem Netzwerkzustand und echtem Multiplayer wird bestaetigt. |
| `da9b6b0` | Den in 1.41 weiterhin auf 32 Bit gekuerzten Modhash wirklich auf `hash64.ToString("X16")` umgestellt. Eine temporaere Release-ZIP wurde im Folgecommit wieder entfernt. | Keine Modcodeaenderung, aber koordinierte Host-/Client-Installation und korrekte Versionsbump-Praxis sind zwingend. Die 1.41-Auffaelligkeit ist geschlossen. |
| `cc1d357` | UI-Benachrichtigungen vor Sync/Persistenz klassifiziert, normale Singleplayeraenderungen nur noch als Debug statt Warning behandelt und Client-Hostwerte im Storage bewahrt. Zusaetzlich nur Diagnose-/Kommentararbeit in `BulkUnitDetours`. | Direkt relevant fuer das gemeinsame Presetsystem und seine Teststubs. Weniger falsche Syncwarnungen; keine ungeplanten UI-only-Speichervorgaenge durch den Extender. |
| `159ed33` | Finale Trennung von Synchronisation und Persistenz: neue Attribute `[PersistLocal]` und `[DoNotPersist]`, zentrale interne Routingregeln und aktualisierter Lobby-Settings-Guide. | Additive API. Bestehende Syncproperties bleiben standardmaessig persistent. Kein vorhandener Workspacewert ist als transient identifiziert; keine Massendekoration. Das eigene `[PresetLocal]` bleibt wegen der Zwei-Preset-/Trailsemantik bestehen. |
| `b822a18` | Contributor-Richtlinie aktualisiert; `UnityMainThreadDispatcher.EnqueueAndWait<T>` als verborgen und obsolet markiert. | Kein Treffer auf `EnqueueAndWait<T>` im Workspace. Keine Laufzeitaenderung. |
| `1ea016a` | Dokumentationsnavigation und Startseite ergaenzt, RE-Datenbank minimal aktualisiert, auskommentierten Hunter-Verbose-Log bereinigt. | Keine produktive Modauswirkung. Der bestehende Hunter-Eventvertrag aendert sich nicht. |
| `304010b` | Mergecommit mit Changelogzusammenfuehrung. | Keine zusaetzliche Runtimeaenderung. |
| `171d68e` | Release 1.42.0 erzeugt. | Abschluss-/Versionscommit ohne weitere API-Aenderung. |

## 5. Technische Bewertung der neuen Settingsattribute

Der 1.42-Vertrag lautet:

| Klassifikation | Netzwerk | Extender-Storage |
| --- | --- | --- |
| `[SyncHostOnly]` | Host zu Clients | lokal eigener Hostwert |
| `[SyncPerPlayer]` | eigener Spielerwert zu allen | nur eigener Spielerwert |
| `[PersistLocal]` | nein | lokaler Wert |
| Syncattribut plus `[DoNotPersist]` | wie Syncattribut | nein |
| kein bekanntes Attribut | nein | nein, UI-only |

Fuer den Workspace gelten zusaetzlich folgende Regeln:

- `[Shared.PresetLocal]` bleibt die ausdrueckliche Klassifikation fuer lokale Werte innerhalb des gemeinsamen Zwei-Preset-Systems. Es speichert nicht nur einen Einzelwert, sondern ordnet ihn Preset 1 oder 2 zu und muss deshalb vom Shared-Controller verwaltet werden.
- `[PersistLocal]` ist nur fuer einen kuenftigen lokalen Wert sinnvoll, der bewusst ausserhalb des gemeinsamen Presetsystems gespeichert werden soll. Fuer neue Lobby-Modsettings gilt weiterhin die Workspace-Vorgabe, das gemeinsame Presetsystem und `[PresetLocal]` zu verwenden.
- `[DoNotPersist]` ist nur fuer einen bewusst transienten synchronisierten Wert geeignet. Unter den derzeit 121 synchronisierten Properties wurde kein solcher Wert gefunden.
- Alle synchronisierten Properties pauschal mit `[DoNotPersist]` zu markieren waere falsch: Es wuerde den Extender-Legacy-Loadpfad veraendern, 121 Deklarationen aufblaehen und die bisherige Migration bestehender `.msgpack`-Dateien riskieren.
- Die 1.42-Clientbewahrung ersetzt nicht die gemeinsame Presetkomposition. Der Extender kennt weder `__SerpPreset1`, `__SerpPreset2`, `__SerpActivePreset` noch den Trail-Snapshot und wuerde diese reservierten Eintraege bei seinem normalen Vollsnapshot nicht selbst erhalten.

## 6. Sequenzieller Umsetzungsplan

### Schritt 1: 1.42-Testharness vor der Codeaenderung erweitern

- `.inspect/HostClientPresetTests` auf den finalen 1.42-Routingvertrag bringen:
  - Stubattribute fuer `[PersistLocal]` und `[DoNotPersist]` ergaenzen.
  - UI-only-`PropertyChanged` darf weder Broadcast noch Extender-Storage ausloesen.
  - `[PersistLocal]` wird gespeichert, aber nicht gesendet.
  - Sync plus `[DoNotPersist]` wird gesendet, aber nicht gespeichert.
  - Ein Client-Storage-Snapshot bewahrt den eigenen gecachten Hostwert statt des empfangenen Hostwerts.
  - Eine eingehende Hostaktualisierung bleibt runtimewirksam, wird nicht lokal persistiert und aktualisiert bei aktivem Trail nur den fluechtigen Trail-Snapshot.
  - `[Shared.PresetLocal]` bleibt in beiden lokalen Presets erhalten, ohne im Extender-Protokoll zu erscheinen.
  - Revert-, Preset-, Reset- und Rollennotifications erzeugen keine redundanten Dateischreibvorgaenge.
- Den `GameNetworkAPI`-Stub um `IsMultiplayerGame()` ergaenzen und `GameModeHelper` mindestens fuer diese Faelle pruefen:
  - lokaler Skirmish: `IsNetworkedEnvironment=true`, `IsMultiplayerGame=false`, kein echter Multiplayer;
  - echte Lobby vor Kartenstart: Mitgliedersignale erkennen Multiplayer auch dann, wenn `MPGameActive` noch false ist;
  - aktives Multiplayer-Spiel: `IsMultiplayerGame=true`;
  - Multiplayer-Save: uebergebenes `multiplayerSave=true` bleibt autoritativ.
- Statische Audits ergaenzen:
  - kein Workspace-Aufruf von `EnqueueAndWait<T>`;
  - kein direkter `Platform_Multiplayer.MPGameActive`-Zugriff ausser in gezielten Teststubs;
  - keine unbeabsichtigte Verwendung von `[PersistLocal]` oder `[DoNotPersist]` in den zehn Presetmods;
  - `NetworkMode` weiterhin nicht als bereits wirksamer Hashfilter behandeln.

Abnahme: Die Tests muessen die 1.41-spezifischen redundanten Writes vor der Shared-Aenderung sichtbar machen und nach Schritt 2 gruen sein.

### Schritt 2: Gemeinsames Presetsystem auf 1.42 abstimmen

- In `Shared/PresetLobbyModSettingsViewModel.cs` versionsgebundene Kommentare von „1.41 ownership gate“ auf den aktuellen beziehungsweise versionsneutralen Vertrag aktualisieren.
- Den alten Workaround entfernen, der nach jedem `RaiseAccessProperties()` ueber `SanitizeStorage()` erneut die gesamte Datei schreibt. 1.42 ignoriert diese UI-only-Notifications bereits.
- Den `property == null`-Pfad in `AfterPropertyChanged` nicht mehr pauschal als Anlass fuer `WriteCombinedPayload()` verwenden. UI-only-Zustand soll keinen Diskwrite ausloesen.
- Die expliziten Writes fuer folgende echten Zustandsaenderungen beibehalten:
  - klassifizierte Host-, Per-Player- und `[PresetLocal]`-Properties;
  - Presetwechsel und erstmalige Preseterzeugung;
  - Eintritt in und Austritt aus dem Trail-Kontext;
  - Wiederherstellung lokal eigener Hostwerte auf Clients;
  - reservierte Presetmetadaten nach einem normalen Extender-Vollsnapshot.
- Die Erkennung eingehender Netzwerksynchronisation weiterhin beibehalten. 1.42 bietet dafuer keine neue oeffentliche API; der autorisierte Lauf darf lokale Presets nicht veraendern, muss aber den fluechtigen Trail-Hostsnapshot aktualisieren.
- Keine parallele zweite Persistenzdatei und keine Migration weg vom gemeinsamen `.msgpack`-Container anlegen.

Abnahme: Pro echter lokaler Settingaenderung entsteht hoechstens der notwendige Extender-Snapshot plus genau ein finaler Shared-Snapshot; reine Rollen-/Visibility-/Tooltip-/Accessnotifications aendern die Datei nicht. Presetmetadaten und lokal eigene Hostwerte bleiben in jedem Fall erhalten.

### Schritt 3: Offizielle Multiplayer-API zentral uebernehmen

- In `Shared/GameModeHelper.cs` ausschliesslich den Ausdruck `Platform_Multiplayer.MPGameActive` durch `GameNetworkAPI.IsMultiplayerGame()` ersetzen.
- Feldname und Diagnoseausgabe `PlatformMultiplayer` duerfen zur Rueckwaertskontinuitaet bestehen bleiben, sofern klar dokumentiert ist, dass der Wert nun ueber die Extender-API kommt.
- `Director.MultiplayerGame`, reale Lobby-/Game-Member, `multiplayerSave` und `GameNetworkAPI.IsNetworkedEnvironment()` nicht entfernen. `IsMultiplayerGame()` ist waehrend einer Vorstartlobby nicht zwingend allein ausreichend und der niedrige Networked-Wert bleibt fuer die Skirmishdiagnose wertvoll.
- Die direkten `IsNetworkedEnvironment()`-Verwendungen in `MPTest` unveraendert lassen, weil sie andere Semantik pruefen.

Abnahme: Die Modusergebnisse und `ToDiagnosticString()` bleiben fuer Singleplayer-Skirmish, Trail, Lobby, aktives Multiplayer-Spiel und Multiplayer-Save gegenueber dem bisherigen Helper unveraendert.

### Schritt 4: Settingsklassifikation und Versionswerte auditieren

- Alle 101 `[SyncHostOnly]`-, 20 `[SyncPerPlayer]`- und sechs `[Shared.PresetLocal]`-Deklarationen erneut gegen ihre fachliche Eigentuemerschaft pruefen. Erwartung: keine Attributaenderung.
- Besonders `CastlePlanner` pruefen: seine lokalen AIV-/Blueprintwerte duerfen nie synchronisiert werden und muessen in beiden lokalen Presets erhalten bleiben.
- Vor dem Build jedes der zehn geaenderten Plugins mit einer neuen Version versehen. Sowohl der `BepInPlugin`-`PluginVersion`-Wert als auch alle kanonischen und paketierten `info.json`-Versionen muessen gemaess der jeweiligen Modkonvention atomar aktualisiert werden.
- Vorhandene Abweichungen zwischen Assembly- und Assetversion nicht stillschweigend angleichen, ohne die jeweilige Historie zu pruefen. Der 1.42-Hash kann beide Eintraege aufnehmen; entscheidend ist ein konsistentes, reproduzierbares Paket auf Host und Client.
- `NetworkMode` nicht allein wegen 1.42 in alle `info.json` eintragen. Der getaggte Hashcode wertet es weiterhin nicht aus.

Abnahme: Alte und neue Binaerpakete koennen nicht mit unveraenderten hashrelevanten Versionswerten verwechselt werden; lokale und installierte Metadatenkopien sind identisch.

### Schritt 5: Gesamtaudit vor jedem Mod-Build

Alle Pruefungen abschliessen, bevor die erste Mod-`build.bat` ausgefuehrt wird:

1. `.inspect/HostClientPresetTests` vollstaendig ausfuehren.
2. `CustomCustomTrail.Tests` mit 18/18 gruen ausfuehren und `TrailMissionSettingsCoordinator.cs` statisch gegen ausschliesslich `[SyncHostOnly]`-Snapshots pruefen.
3. XAML-Audit auf Tooltips, `ToolTipService.ShowDuration="60000"`, Locale-Key-Paritaet und die vorgeschriebenen ScrollViewer-Bindings ausfuehren.
4. Alle geaenderten Textdateien gezielt auf CRLF und nackte LF pruefen.
5. Statische Attribut-, Dispatcher-, Multiplayer-API- und Versionsaudits aus Schritt 1 und 4 ausfuehren.
6. Kompilatorische Pruefung gegen die lokal gebaute und installierte 1.42-Assembly; keine eigene parallele Installationslogik verwenden.

Danach jeden der zehn durch Shared-Code geaenderten Produktionsmods genau einmal ueber seine eigene `build.bat` direkt und erhoeht bauen/installieren. Empfohlene Reihenfolge:

1. `CastlePlanner` als reiner Client-/`PresetLocal`-Fall
2. `BugfixesAndQoL` als gemischter Host-/Per-Player-Fall
3. `ExtraFeatures`
4. `ImprovedHunters`
5. `RandomEvents`
6. `StartConditions`
7. `BuildingCosts`
8. `BuildingLimit`
9. `UnitCosts`
10. `UnitLimit`

`CustomCustomTrail` wird nur neu gebaut, wenn dessen eigener Code oder seine Metadaten tatsaechlich geaendert werden. Die uebrigen Extender-referenzierenden Mods benoetigen ohne eigenen Code- oder Metadatenunterschied keinen reinen 1.42-Rebuild.

### Schritt 6: Ingame- und Multiplayer-Abnahme

- BepInEx-Log jeweils ab einer neuen Startmarke auswerten und eigene Millisekunden-Zeitstempel verwenden.
- Singleplayer-Skirmish und Singleplayer-Trail:
  - `lowLevelNetworked=true` darf weiterhin vorkommen;
  - `IsRealMultiplayer` muss false bleiben;
  - Presetwechsel, Reset und Neustart muessen Host-, Per-Player- und lokale Werte korrekt erhalten.
- Echter Host-/Client-Lauf mit identischem 1.42-Modsatz:
  - vollen 16-stelligen Modhash und identische Eintragszahl vergleichen;
  - Hostsettings kommen auf dem Client an;
  - empfangene Host-/Trailwerte erscheinen nicht in der lokalen `.msgpack`;
  - Per-Player- und `[PresetLocal]`-Werte bleiben lokal gespeichert;
  - UI-only-Notifications erzeugen weder Syncwarnungen noch Dateischreibkaskaden;
  - Preset 1/2 und Trail koennen wie vorgesehen gewechselt und unveraendert wiederhergestellt werden.
- Kontrolllauf mit absichtlich unterschiedlicher Version oder 1.41 gegen 1.42: Lobbyfilterung muss die inkompatiblen Saetze erwartungsgemaess trennen.
- `MPTest` und Diagnoseplugins nur dann laden, wenn sie auf beiden Peers in exakt derselben hashrelevanten Version vorhanden sind.

## 7. Definition of Done

Das Update ist abgeschlossen, wenn:

- der gemeinsame Modushelper `GameNetworkAPI.IsMultiplayerGame()` nutzt, ohne seine robusteren Modussignale zu verlieren;
- reine UI-/Accessnotifications keine Presetdatei mehr schreiben;
- jede echte Settingaenderung Presetmetadaten und lokal eigene Hostwerte weiterhin sicher erhaelt;
- `[Shared.PresetLocal]` lokal persistent und netzwerkfrei bleibt;
- weder fremde Hostwerte noch Trailwerte in lokale Presets gelangen;
- kein vorhandener Wert unnoetig mit `[PersistLocal]` oder `[DoNotPersist]` umklassifiziert wurde;
- HostClientPresetTests, CustomCustomTrail.Tests, XAML-/Locale-/CRLF- und statische Audits gruen sind;
- alle zehn Shared-Code-Mods neue konsistente hashrelevante Versionen besitzen und nach allen Vorpruefungen genau einmal gebaut/installiert wurden;
- Host und Client denselben vollen 64-Bit-Modhash melden und inkompatible Saetze erwartungsgemaess getrennt werden;
- die abschliessenden Singleplayer-, Trail- und Multiplayerlogs keine relevanten Settings-, Persistenz-, Autorisierungs-, Dispatcher- oder Netzwerkfehler enthalten.

## 8. Bewusst nicht geplante Aenderungen

- Keine neue native RVA-/Layoutanalyse; die kanonische Spiel-DLL ist gegenueber der 1.41-Basis unveraendert.
- Keine Chore-Migration vor dem fuer 1.50.0 dokumentierten Vertrag.
- Kein Ersatz fachlicher Multiplayererkennung durch `IsNetworkedEnvironment()` oder allein durch `IsMultiplayerGame()`.
- Keine Entfernung der eigenen Preset-, Trail- und sicheren Hostwertkomposition zugunsten des einfachen Extender-Storage.
- Keine pauschale Dekoration aller Syncproperties mit `[DoNotPersist]`.
- Keine pauschale Ersetzung von `[Shared.PresetLocal]` durch `[PersistLocal]`.
- Keine massenhafte `NetworkMode`-Metadatenumstellung, solange der Releasecode den Wert beim Lobbyhash nicht auswertet.
- Kein Rebuild oder Versionsbump von Mods, deren Quellcode, eingebundene Shared-Dateien und Metadaten unveraendert bleiben.
