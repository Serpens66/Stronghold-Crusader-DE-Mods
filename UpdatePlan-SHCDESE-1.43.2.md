# Updateplan: SHCDE Script Extender 1.42.0 auf 1.43.2

Stand: 30. August 2026
Status: Statische Analyse abgeschlossen; zwei Umsetzungsentscheidungen, Tests, Codeanpassungen, Versionsschritte und Builds offen

## 1. Ziel und gepruefte Basis

Dieser Plan beschreibt die notwendigen Workspace-Anpassungen fuer den Wechsel vom Script Extender 1.42.0 auf 1.43.2. Geprueft wurden alle 32 Commits in `v1.42.0..v1.43.2`, die finalen Quell- und Dokumentationsdiffs, die oeffentlichen Signaturaenderungen sowie alle auffindbaren Verwendungen der betroffenen APIs und Workarounds im Workspace.

- Alter Release-Tag: `v1.42.0`, Commit `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Neuer Release-Tag: `v1.43.2`, Commit `ac291f23d52435018d7851db288c17668c4a171f`
- Umfang: 40 geaenderte Dateien, 2445 Einfuegungen, 415 Loeschungen
- Der kanonische lokale Git-Fork steht auf `v1.43.2` und ist quellseitig aktuell.
- Die lokalen Referenzausgaben wurden am 30.08.2026 neu gebaut und sind jetzt aktuell:
  - `shcde-script-extender/src/SHCDESE.BepInEx/bin/net481/SHCDESE.dll`
  - `shcde-script-extender/mod_output/000shcdese/SHCDESE.dll`
  - Beide sind bytegleich mit SHA-256 `69CF4D019DE7D63F610F39285A9FA0B6873D574E78B735568EBB85DAB9724ACA` und ProductVersion `1.0.0+ac291f23d52435018d7851db288c17668c4a171f`; der eingebettete Commit entspricht exakt dem Tag `v1.43.2`.
- Die installierte Extender-DLL ist bereits 1.43.2:
  - Pfad: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll`
  - SHA-256: `6DB7544BE51B9FEB6263E833CF564F1326906B3E6A255F5777F168EFE5FCC1A1`
  - FileVersion `1.43.2.0`, ProductVersion `1.43.2+2a1f4bed5458f6ec579a920c9ef7d510d4876d9d`; der abweichende Hash ist durch die anders versionierte offizielle Releaseausgabe erklaert und kein verbleibender 1.42.0-Stand.
  - FileVersion/ProductVersion: `1.43.2.0` / `1.43.2+2a1f4be...`
- Die kanonische installierte Spiel-DLL hat sich seit dem 1.42.0-Plan geaendert:
  - Aktuell: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, 3.451.392 Bytes, Zeitstempel 24. August 2026
  - Im 1.42.0-Plan: SHA-256 `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
  - Die historische Workspace-Kopie `x86_64/CrusaderDE.dll` ist weiterhin nicht massgeblich; sie hat SHA-256 `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`.
- Das BepInEx-Log war am dokumentierten Pfad beim Audit nicht vorhanden. Eine reale 1.43.2-Laufzeitauswertung konnte deshalb noch nicht erfolgen.
- Die beim Analysestart offene Benutzerarbeit in `MoveMoatTest` und `_inspect/MoatUnitBehaviorReverseEngineering.md` wurde waehrend des Audits extern als Commit `81102ab6` eingecheckt. Sie wurde fuer diesen Plan nicht veraendert; die erneute API-Suche nach dem Commit ergab keinen zusaetzlichen 1.43.2-Bruch.

## 2. Kurzfazit

1.43.2 ist **nicht** voll quell- oder binaerkompatibel zu 1.42.0. Es gibt vier zentrale Migrationsbereiche:

1. Die oeffentliche Klasse `ChoreNetworkTransport` wurde entfernt. `BugfixesAndQoL`, `ExtraFeatures`, `RandomEvents` und `ChoreTestMod` verwenden sie direkt und kompilieren deshalb gegen 1.43.2 nicht. Die offizielle Alternative `GameNetworkAPI.SendPacketToAllEx2(..., viaChore: true)` ist verwendbar, wenn jeder simulationskritische Aufruf zuvor Packetregistrierung, unveraenderliche Payload, `ChoreManagerVA != 0` und hoechstens 1200 Bytes inklusive Packet-ID sicherstellt. Unter genau diesen Bedingungen kann der aktuelle 1.43.2-Code nicht in seinen Steam-Fallback gelangen.
2. Der Goldtyp wurde von `UInt32` auf `Int32` korrigiert. `BugfixesAndQoL` und `StartConditions` enthalten dadurch konkrete Quell- und binaere Signaturbrueche.
3. Alle drei im Workspace umgangenen Settings-Transportfehler wurden upstream behoben. Der komplette `ScriptExtenderMultiplayerSyncWorkaround` in `Shared/PresetLobbyModSettingsViewModel.cs` muss entfernt werden, damit keine doppelten Join-Snapshots, parallelen Transportdetours oder konkurrierenden Per-Player-Anwendungen verbleiben. Diese Shared-Datei wird von 13 Runtime-Mods kompiliert; alle muessen nach der finalen Aenderung neu gebaut werden.
4. Der Extender implementiert nun selbst Linux-/Proton-Workshop-Deployment und schuetzt die Updatermethoden mit Anti-Tamper. `LinuxModding` detouriert genau `MapModManager.LaunchUpdaterAndExit`; dieser Runtime-Mod ist damit technisch ueberholt und mit aktiviertem Anti-Tamper unvereinbar.

Zusaetzlich sollte `SerpsModsHost` die neuen GUID-basierten Registrierungsabfragen verwenden, damit eine vom Extender verworfene spaete Duplikatregistrierung nicht irrtuemlich als erfolgreicher Pack-Child gezaehlt wird.

## 3. Entscheidungen vor der Umsetzung

### 3.1 Fail-closed Chore-Transport

Fuer simulationsveraendernde Aktionen gilt weiterhin: kein stiller Steam-Fallback und keine lokale Mutation, wenn der tick-ausgerichtete Chore nicht sicher eingereiht wurde.

Empfohlener Weg:

- Einen gemeinsamen modseitigen `TrySendChore<T>`-Helper verwenden, der den Packet-Hook und die abgeschlossene Netzwerkinitialisierung verlangt.
- Das Packet einmal vorab serialisieren und `sizeof(short) + body.Length <= 1200` pruefen. Dasselbe Packetobjekt darf bis zum Send-Aufruf nicht mehr veraendert werden.
- `GameGlobalsManager.Instance.ChoreManagerVA != 0` pruefen und andernfalls ohne Steam-Sendung abbrechen.
- Danach `GameNetworkAPI.SendPacketToAllEx2(packet, packetId, viaChore: true)` aufrufen. Exceptions werden geloggt und fuehren zu keiner separaten lokalen Mutation.

Diese Vorbedingungen decken exakt die beiden `false`-Faelle der internen 1.43.2-Chore-Methode ab. Der alte `SendRawBlob`-Rueckgabewert war keine native Zustell- oder Queue-Bestaetigung: Nach denselben beiden Pruefungen rief auch 1.42.0 eine native `void`-Funktion auf und meldete `true`, sofern keine Exception austrat. Die modseitige Migration behaelt daher die bisherige praktische Fail-closed-Staerke.

Nicht verwenden:

- keinen Reflection-Zugriff auf `SendScriptExtenderChorePayload`;
- keinen direkten Aufruf der oeffentlichen Low-Level-Funktionswrapper in `BulkChoreDetours`;
- keine eigene Kompatibilitaetskopie der geloeschten Klasse, da sie nicht mit den internen Extenderfeldern verbunden waere;
- keinen direkten Steam-Ersatz fuer Simulationsmutationen.

Die Datei `SHCDESE-1.43.2-ChoreTransport-API-Report.md` dokumentiert die abschliessende interne Bewertung und soll nicht als Upstream-Report versendet werden. Eine neue Upstream-Try-API waere lediglich Komfort; sie ist weder fuer die Migration noch fuer zuverlaessigen Multiplayer erforderlich.

### 3.2 Zukunft von `LinuxModding`

Empfohlen ist, den BepInEx-Runtime-Hook zu entfernen beziehungsweise den Mod einzustellen und nur noch zu pruefen, ob die vorhandenen Launcher-/Installationsskripte als rein externe Hilfen weiterhin einen Zweck haben. Eine parallele alte Hook-Implementierung darf nicht als Fallback behalten werden: Sie dupliziert die neue Extenderfunktion und trifft bewusst geschuetzte Methoden.

Vor der Umsetzung ist zu entscheiden, ob:

1. `LinuxModding` vollstaendig aus dem aktiven Modbestand entfernt wird, oder
2. nur der Runtime-Mod entfernt und ein klar abgegrenztes launcher-only Hilfspaket behalten wird.

## 4. Betroffenheitsmatrix

| Bereich/Mod | Prioritaet | Befund | Erforderliche Folge |
| --- | --- | --- | --- |
| Lokale SHCDESE-Referenzbasis | erledigt | Git, `src/SHCDESE.BepInEx/bin/net481` und `mod_output/000shcdese` stehen auf Tag-Commit `ac291f...`; beide lokalen DLLs sind mit SHA-256 `69CF4D...` bytegleich. | Diese Ausgaben als eindeutige 1.43.2-Referenz fuer die folgenden Modkompilierungen verwenden. |
| `Shared/PresetLobbyModSettingsViewModel.cs` | hoch | Installiert weiterhin vier temporaere 1.42-Workarounds: Join-Snapshot, reliable lobby send, in-game sender propagation und eigene Per-Player-Zuordnung/Deferral. Alle vier Pfade existieren nun upstream. | `ScriptExtenderMultiplayerSyncWorkaround`, beide Hook-Anker und den Aufruf in `LobbyModSettingsPresetRegistration.Register(...)` entfernen; normales Preset-, Trail-, Persistenz- und Roster-System beibehalten. |
| 13 Mods mit gemeinsamer Presetbasis | hoch | Kompilieren die geaenderte Shared-Datei direkt. | Nach finaler Shared-Aenderung testen, final atomar versionieren und neu bauen: `BugfixesAndQoL`, `BuildingCosts`, `BuildingLimit`, `CastlePlanner`, `CheatMod`, `CustomCustomTrail`, `ExtraFeatures`, `ImprovedHunters`, `RandomEvents`, `SerpsModsHost`, `StartConditions`, `UnitCosts`, `UnitLimit`. |
| `BugfixesAndQoL` | hoch (Quellbruch) | Direkte `ChoreNetworkTransport`-Nutzung in Assassin-Climb, Multiplayer-Spieltempo, Belagerungsmunition und Kapitulation. Diese Aktionen brauchen weiterhin Chore-Taktausrichtung, aber nicht die geloeschte rohe API. `GetPlayerGold()` wird noch als `uint` behandelt und mit `uint` verglichen. | Chorepfade auf den vorgeprueften gemeinsamen `SendPacketToAllEx2`-Helper migrieren; Goldlogik konsequent auf `int` und nichtnegative fachliche Grenzen anpassen; gezielte Protokolltests. Der bestehende bewusste Direktpfad zum Entpausieren bleibt eine getrennte Sonderloesung. |
| `ExtraFeatures` | hoch (Quellbruch) | Direkte Chore-Nutzung in Torautomatik, Einzelgebaeude-Pause, Ritter-Absteigen und Steinbruchhaufen-Verschiebung. Diese Simulationsmutationen brauchen Chore weiterhin. | Alle vier Pfade auf denselben vorgeprueften `SendPacketToAllEx2`-Helper migrieren; bestehende Payloadlimits und „keine lokale Mutation bei Sendefehler“-Semantik erhalten. `AddPlayerGold(int,int)` bleibt kompatibel. |
| `RandomEvents` | hoch (Quellbruch) | Initialisierung, Batch- und Wegweiserereignisse senden ueber die entfernte rohe Chore-API. Die Ein-Chore-pro-Tick- und Sender-eingeschlossen-Semantik bleibt fachlich notwendig. | Zentralen `TrySendRawChore`-Pfad in einen typisierten, vorgeprueften `TrySendChore<T>`-Pfad umstellen; Ein-Chore-pro-Tick-Regel, 1200-Byte-Grenze, Hashdiagnose und Fail-closed-Abschaltung erhalten. |
| `RandomEvents` Ingame-Senderausnahme | mittel | Der Mod toleriert derzeit bei einem Readiness-Paket einen fehlenden Ingame-Transport-Sender, weil SHCDESE 1.42.0 `fromMember` nicht weiterreichte. 1.43.2 reicht die authentifizierte Steam-ID nun bis `HandleRawPacket` durch. | Ausnahme nach Host/Client-Test entfernen beziehungsweise auf den normalen authentifizierten Senderpfad vereinheitlichen; fehlender oder nicht zuordenbarer Sender muss wieder fail-closed bleiben. |
| `ChoreTestMod` | niedrig (Diagnose) | Prueft `ChoreNetworkTransport.IsAvailable` und ruft `SendRawBlob` direkt auf. Dies ist nur ein Diagnosemod, keine Produktionsabhaengigkeit. | Diagnose auf `SendPacketToAllEx2(..., viaChore: true)` mit denselben Vorbedingungen umstellen oder nach abgeschlossener Migration stilllegen. Empfang ohne `SenderSteamId` belegt weiterhin den Chore-Pfad. |
| `StartConditions` | hoch | `SetPlayerGold(playerId, (uint)setGold)` bindet an die entfernte UInt32-Signatur. Das vorhandene Binary kann auf 1.43.2 einen `MissingMethodException` ausloesen. | Cast entfernen, Wertebereich als `int` pruefen und Startgoldtests um negative/hohe Grenzwerte erweitern. |
| `LinuxModding` | blockierend | Detouriert die nun upstream implementierte und Anti-Tamper-geschuetzte Methode `MapModManager.LaunchUpdaterAndExit`. | Nach Benutzerentscheidung Runtime-Mod entfernen oder in ein hookfreies launcher-only Paket umwandeln; nicht durch Abschalten von Anti-Tamper „reparieren“. |
| `SerpsModsHost` | mittel | Extender registriert Asset-Mods nun GUID-idempotent und verwirft spaete Duplikate. Die aktuelle Erfolgskontrolle sucht nur irgendeinen Eintrag mit gleicher GUID und kann deshalb den falschen Ordner als Erfolg zaehlen. | Nach `RegisterAssetMod` `TryGetRegisteredDirectory(guid, out path)` verwenden, Pfad mit dem erwarteten Pack-Child vergleichen und andernfalls H004/fail-closed melden. Bestehenden Duplikat-Audit behalten. |
| `Shared/GameModeHelper.cs` | mittel | Kommentare beschreiben `GetLocalPlayerId`, `GetPlayerIdForSteamId` und `IsMultiplayerGame` noch als bekannte Upstreamfehler. 1.43.2 nutzt nun finale Slot-Tabellen/Roster und verbessert die fruehe Multiplayererkennung. | Tests und Kommentare auf 1.43.2 aktualisieren. Den robusten Multi-Source-Resolver erst nach echtem Host/Client-Nachweis vereinfachen; `IsPlayerSlotMappingAvailable()` als Pending-vs-invalid-Signal pruefen. |
| `BugfixesAndQoL` Custom-Lord-Titel | mittel | Upstream ergaenzt Lobby-Titel, aber der finale 1.43.2-Code verwendet im `OnScreenText`-Hook wieder `internalLordId` statt `computerName`. Der Workspace-Fix deckt mehr als den neuen Upstreamfix ab. | Nicht pauschal entfernen. Lobby- und Ingamepfad getrennt testen; nur nachgewiesen redundanten Lobbyanteil entfernen, Ingame-Subtype- und Duplikatfallback vorerst behalten. |
| `BulkUnitDetours` / Unit-ID 0 | hoch zu beobachten | Vier alte 1.42.0-Logkopien enthalten zusammen 549 Meldungen `TryGetUnitById ... [0/10000]`. Es wurden keine negativen Unit-IDs gefunden. `BulkUnitDetours.cs` und `GameUnitManagerAPI.cs` sind zwischen 1.42.0 und 1.43.2 unveraendert; IDB- und Crashhandler-Updates sind kein Laufzeitfix. | Nicht als durch 1.43.2 behoben markieren. Nach dem Update dieselben Spielsituationen mit Beduinenheilern, Mauerzielwahl und den Unit-Hooks reproduzieren; bei erneutem Auftreten den konkreten Hook mit gezielter Diagnose isolieren und erst dann einen Upstream-Report erstellen. |
| Native AOB-/Layoutmods | hoch zu testen | Die kanonische Spiel-DLL hat einen neuen Hash. Der Extender aktualisierte seine IDB und die Chore-Phase-Aufloesung. Aus dem Diff ist ausser dem signierten Goldfeld kein weiterer Workspace-Layoutbruch bewiesen. | Alle eigenen Signaturen gegen die kanonische DLL auf genau einen Treffer pruefen und danach Runtime-Smokes ausfuehren; keine Erkenntnisse aus der historischen Workspace-DLL uebernehmen. |
| `ActiveAIVDetector`, `AIDefense`, `AssassinCombatFix`, `HunterQueryTargetDiagnostic`, `MoatCommandTest`, `MoveMoatTest`, `MPTest`, `VanillaAICExporter` | niedrig/mittel | Keine weitere entfernte oder geaenderte oeffentliche API-Nutzung gefunden. Einige besitzen eigene native Signaturen oder Diagnosepfade. | Kein API-bedingter Sourcechange nachgewiesen; gegen aktuelle Referenz kompilieren beziehungsweise ueber vorgesehene Smokes/AOB-Audits pruefen. Benutzerarbeit in `MoveMoatTest` nicht ueberschreiben. |
| `AIVParser`-Tests und reine CLI/Core-Projekte | niedrig | Keine relevante Runtime-API-Aenderung. | Nur bestehende Tests ausfuehren, kein Runtime-Rebuild allein fuer 1.43.2. |

## 5. Technische Bewertung der relevanten Extender-Aenderungen

### 5.1 Chore Networking API

1.42.0 stellte `SHCDESE.API.Components.Network.ChoreNetworkTransport` mit `SendRawBlob` und `IsAvailable` oeffentlich bereit. Die Workspace-Mods nutzen diese Ebene bewusst, um vor einem Steam-Fallback abzubrechen. Der boolesche Wert war jedoch keine native Queue- oder Zustellbestaetigung: `false` entstand nur bei fehlendem `ChoreManagerVA` oder mehr als 1200 Bytes; danach wurde eine native `void`-Funktion aufgerufen und bei normaler Rueckkehr `true` geliefert.

1.43.2 hat:

- `ChoreNetworkTransport.cs` geloescht;
- den rohen Sendepfad als `internal GameNetworkAPI.SendScriptExtenderChorePayload(byte[])` verschoben;
- `SendPacketToAllEx2<T>(..., viaChore: true)` beibehalten, aber weiterhin als `void` mit automatischem Steam-Fallback;
- Chore-Pack/Unpack auf native Funktionswrapper und eine AOB-aufgeloeste `ChoreSendPhaseVA` umgestellt.

Das alte Komfortsignal fehlt, die Chore-Funktionalitaet aber nicht. Ein Mod kann vor dem offiziellen `SendPacketToAllEx2` dieselben zwei Abbruchbedingungen pruefen. Da die Extender-Initialisierung zuerst `FindGameGlobals`, danach `BulkChoreDetours` und erst danach die Netzwerk-Subscriber initialisiert, ist der native Chorepfad bei unseren nach `LibraryLoaded` registrierten Packet-Hooks bereits aufgebaut. Bei unveraendertem Packet und bestandener Groessen-/Managerpruefung ist der automatische Steam-Fallback im aktuellen 1.43.2-Code nicht erreichbar. Eine Upstream-Try-API waere klarer und zukunftsstabiler, ist aber keine Voraussetzung fuer die Modmigration.

Der Extender nennt keinen ausdruecklichen Grund fuer die Entfernung. Commit `01d2bc63f02e3ebccd3ac18b296b8d0ed062d8c5` fasst sie nur als `Further improved Chore Networking API` zusammen. Aus dem Diff folgt als technische Erklaerung, dass der Sendepfad in `GameNetworkAPI` konsolidiert und die alte Delegate-/`_isSending`-Zwischenschicht durch direkte native Wrapper mit `ChorePhase` ersetzt wurde; `ChoreNetworkTransport` war intern dadurch redundant. Dies ist eine Codeinferenz, keine dokumentierte Begruendung. Die weiterhin vorhandene Dokumentationsreferenz auf `ChoreNetworkTransport.IsAvailable` ist veraltet.

Die vorhandenen variablen Payloadpfade sind bereits weitgehend vorbereitet: `RandomEvents` akzeptiert hoechstens 1200 Bytes inklusive Packet-ID, die Rittertransformation hoechstens 1198 Body-Bytes, und Siege-Restock lehnt mit `>= 1200` sogar den vom Extender noch erlaubten exakten Grenzwert konservativ ab. Die uebrigen Chore-Packets sind fest und deutlich kleiner. Bei der Migration werden diese Pruefungen zentralisiert; ob Siege-Restock exakt 1200 Bytes ebenfalls zulassen soll, ist keine Kompatibilitaetsvoraussetzung.

### 5.2 Settings-Transport und Player-Identitaet

Die drei im Workspace dokumentierten 1.42-Fehler sind im finalen Code behoben:

- `Platform_Multiplayer.SendCustomInfoToMember` wird nun tatsaechlich gehookt und ruft den Join-Snapshot-Pfad auf.
- Direkte Lobby-Sends verwenden `Reliable | AutoRestartBrokenSession` statt des falschen Werts 64.
- Der Ingame-`processMessage`-IL-Hook reicht `fromMember` als authentifizierte Steam-ID an `HandleRawPacket` weiter.

Zusaetzlich:

- `GetPlayerIdForSteamId` verwendet im Spiel das finale `gameMembers.playerID` und in der Lobby `getThisPlayerFromSteamID`.
- `GetLocalPlayerId` gibt frueh `-1` statt eines geratenen Host-/Listenplatzes zurueck.
- `IsPlayerSlotMappingAvailable` unterscheidet „noch keine Zuordnung“ von „Identitaet besitzt keinen Slot“.
- Fruehe Per-Player-Pakete werden begrenzt zwischengespeichert und nach verfuegbarer Slot-Tabelle erneut angewendet.
- `ReceiveLobbyMessage` stoesst diese Wiederanwendung an.

Der Shared-Workaround muss deshalb als Ganzes entfernt werden. Nur einzelne Detours stehenzulassen wuerde doppelte Snapshots, doppelte Anwendungen oder eine von der Upstream-Authentifizierung abweichende Parallelsemantik riskieren.

Auch die darauf beruhende Sonderbehandlung in `RandomEvents`, die bei einem Readiness-Paket einen fehlenden Ingame-Sender toleriert, ist nach erfolgreicher echter Host/Client-Abnahme nicht mehr noetig. Sie soll entfernt und durch den normalen authentifizierten Sender-/Player-ID-Pfad ersetzt werden. Das Readiness-Paket darf weiterhin niemals allein eine fachliche Mutation autorisieren.

Nicht entfernt werden duerfen:

- die eigene Zwei-Preset-/Trail-Persistenz;
- `[SyncHostOnly]`, `[SyncPerPlayer]` und `[PresetLocal]`;
- Companion-Arrays und der gemeinsame Roster-Lifecycle;
- das Verbot, empfangene Host-/Trailwerte lokal zu persistieren;
- die interne Rechtepruefung der Commands und Setter.

### 5.3 Signiertes Gold

Die folgenden oeffentlichen CLR-Signaturen haben sich geaendert:

- `SetPlayerGold(int, UInt32)` -> `SetPlayerGold(int, Int32)`
- `GetPlayerGold(int) : UInt32` -> `GetPlayerGold(int) : Int32`
- `GamePlayerResources.r_TotalGoodsGold : UInt32` -> `Int32`

`AddPlayerGold(int, Int32)` ist trotz Schreibweisenbereinigung binaer gleich geblieben. Die Rueckgabe- und Parametersignaturen der beiden anderen Methoden sind jedoch echte CLR-Vertragsaenderungen; alte Binaries muessen neu kompiliert werden.

### 5.4 Asset-Mod-Duplikate

`GameAssetModManager`:

- registriert GUIDs nur noch einmal;
- waehlt bei der initialen Ordnersuche die hoechste parsebare Version;
- ignoriert spaeter registrierte Duplikate, selbst wenn sie neuer sind;
- bietet neu `IsRegistered(guid)` und `TryGetRegisteredDirectory(guid, out directory)`.

Das ist fuer normale Einzelmods transparent. `SerpsModsHost` registriert seine verschachtelten Pack-Children jedoch spaeter manuell und muss daher den tatsaechlich registrierten Pfad pruefen.

### 5.5 Linux-/Proton-Deployment und Anti-Tamper

Der Extender besitzt nun eigene Windows- und Unix-Updaterpfade, `mod-updater.sh`, Wine-Pfadkonvertierung und Retry-/Staginglogik. Gleichzeitig prueft Anti-Tamper unter anderem:

- `MapModManager.LaunchUpdaterAndExit`
- `MapModManager.LaunchUpdaterAndExit_Win32`
- `MapModManager.LaunchUpdaterAndExit_Unix`
- `Plugin.LoadNativeLibrary`

`LinuxModding` detouriert die erste Methode. Die richtige Migration ist daher Entfernung oder hookfreie Neuabgrenzung, nicht ein Anti-Tamper-Opt-out.

### 5.6 Weitere Aenderungen ohne direkten Workspace-Sourcechange

- Native und VEH-Crashhandler: bessere Fehlerdiagnose; keine Mod-API-Migration.
- `msvcp140.dll` und Buildskripte im Releasepaket: Deploymentthema, keine Runtime-Quellaenderung unserer Mods.
- Steam-Kultur hat Vorrang vor Systemkultur: Lokalisierungs-Smoke erforderlich; kein direkter API-Bruch gefunden.
- `TimerEngine` aendert nur eine fehlerhafte Logformatierung.
- `LuaExtensions.RegisterExportedStaticMethods` aendert seine Signatur; kein Workspace-Aufruf gefunden.
- `RuntimeHelpers.GetSubArray` wurde entfernt, war aber intern und wird im Workspace nicht verwendet.
- `GamePlayerResources.r_WallRefundStoneAccumulator` benennt ein bisher unbekanntes Feld; kein Workspace-Zugriff gefunden.
- Debugmenue zeigt den ChoreManager; keine Runtime-Auswirkung.

### 5.7 `BulkUnitDetours` und die notierte Unit-ID-Auffaelligkeit

Die alten Logs belegen die Auffaelligkeit genauer:

- `LogOutput - Kopie.log`: 17 Fehler;
- `LogOutput - Kopie (2).log`: 11 Fehler;
- `LogOutput - Kopie (3).log`: 147 Fehler;
- `LogOutput - Kopie (4).log`: 374 Fehler;
- insgesamt 549 Meldungen von `GameUnitManagerAPI.TryGetUnitById` mit dem ausschliesslichen ungueltigen Wert `0/10000`;
- keine gefundene negative Unit-ID.

Alle elf ausgewerteten Logkopien liefen laut BepInEx-Ladezeile mit `SHCDE-SE 1.42.0.0`. Die Kopien 5 bis 11 enthalten die Meldung nicht mehr, obwohl auch sie weiterhin 1.42.0 verwendeten. Das Verschwinden kann daher nicht durch 1.43.2 verursacht worden sein und beweist keinen Extenderfix; moegliche Erklaerungen sind ein geaenderter Testfall, eine modseitige Aenderung oder ein nicht mehr ausgeloester nativer Pfad.

Der Quellvergleich `v1.42.0..v1.43.2` ergibt keinen Unterschied fuer:

- `Detours/BulkUnitDetours.cs`;
- `API/GameUnitManagerAPI.cs`, einschliesslich `IsValidId` und `TryGetUnitById`.

Die neue Reverse-Engineering-Datenbank `a2d369b` veraendert nur das mitgefuehrte Analyseartefakt. Die neuen nativen/VEH-Crashhandler `4d71b10` und `2a7beb3` verbessern Diagnose und Dumps, korrigieren aber keinen Unit-Hook. Damit ist die Unit-ID-0-Auffaelligkeit in 1.43.2 weder nachweislich behoben noch durch diesen Versionsdiff erklaert.

Als plausible interne Quellen bleiben insbesondere die zwei `BulkUnitDetours`-Pfade, die ohne eigene Positivpruefung eine berechnete ID an `GameUnitManagerAPI.GetType` weiterreichen: der Beduinenheiler-Hook und die Mauerzielwahl ueber `GetIndexByOffset`. Die vorhandenen Logs enthalten keinen Stacktrace und erlauben keine eindeutige Zuordnung. Deshalb weder unsere Mods noch den Extender auf Verdacht patchen. Zuerst mit 1.43.2 gezielt reproduzieren und bei einem Treffer den ausloesenden Hook instrumentieren.

### 5.8 Entscheidung zu bestehenden Workarounds

| Einstufung | Bereich | Entscheidung fuer 1.43.2 |
| --- | --- | --- |
| Entfernen | `ScriptExtenderMultiplayerSyncWorkaround` | Alle vier Ursachen sind upstream behoben; die Parallelhooks wuerden nun eher Duplikate und abweichende Authentifizierung erzeugen. |
| Entfernen nach echtem MP-Test | `RandomEvents`-Ausnahme fuer fehlenden Ingame-Sender | `fromMember` wird nun propagiert. Fehlender oder nicht zuordenbarer Sender soll wieder fail-closed sein. |
| Entfernen/neu abgrenzen | `LinuxModding`-Runtimehook | Upstream besitzt den Unix-Updater selbst und schuetzt genau den gehookten Einstieg per Anti-Tamper. Nur ein nachweislich noch nuetzliches hookfreies Launcherpaket kaeme als Restbestand infrage. |
| Migrieren, nicht entfernen | Chore-Helfer der vier betroffenen Mods | Chore-Taktausrichtung bleibt erforderlich; nur die entfernte Vermittlungsklasse wird durch den vorgeprueften oeffentlichen 1.43.2-Pfad ersetzt. |
| Beibehalten | Preset-, Trail-, Persistenz- und Rosterlogik | Das sind fachliche Modfunktionen und keine Ersatzimplementierung der drei behobenen Extenderfehler. |
| Beibehalten | `PlayerIdentityHelper`-Validierung und fachliche Identitaetsregeln | Die verbesserte Extenderzuordnung ersetzt den privaten Identity-Detour, aber nicht unsere autoritativen Bereichs-, Roster- und Trailpruefungen. |
| Vorlaeufig beibehalten | robuster `GameModeHelper` | `IsMultiplayerGame` wurde verbessert, aber der Multi-Source-Resolver deckt weitere Spielmodi und Uebergangsphasen ab. Erst nach realen Lobby-/Kartenuebergangstests vereinfachen. |
| Vorlaeufig beibehalten | Custom-Lord-Ingame-Fallback | Der finale 1.43.2-Ingame-Hook ging wieder auf `internalLordId` zurueck; nur der getrennt nachgewiesene Lobbyanteil kann redundant sein. |
| Beibehalten bis gezielter Gegenbeweis | `ImprovedHunters`-Hunter-Query-, Chicken-Koordinaten- und `UnitLimit`-Owner-Workarounds | In den jeweils relevanten Extenderpfaden wurde kein Fix gefunden. Diese Workarounds nicht allein wegen des Versionssprungs entfernen. |
| Neu testen, nicht als Fix verbuchen | `BulkUnitDetours` / Unit-ID 0 | Quellpfade sind unveraendert; die spaeteren fehlerfreien Logs liefen ebenfalls noch mit 1.42.0. Ein belastbarer 1.43.2-Test fehlt. |

## 6. Vollstaendige Commitpruefung

| Commit(s) | Aenderung | Workspace-Auswirkung |
| --- | --- | --- |
| `70a74e4` | Goldfeld und Gold-API auf signed `Int32` korrigiert. | Direkter Bruch in `BugfixesAndQoL` und `StartConditions`; alte Binaries neu bauen. |
| `4d71b10`, `2a7beb3` | Native Crashhandler und VEH-Diagnose. | Keine API-Migration; Startup-/Crash-Smoke und neue Logartefakte beachten. |
| `a2d369b` | Reverse-Engineering-Datenbank aktualisiert. | Bestaetigt neue Analysebasis, ist aber keine Runtimekorrektur; eigene AOBs gegen die installierte DLL pruefen und insbesondere keinen BulkUnit-Fix daraus ableiten. |
| `2cad6ba`, `cc483f4`, `36d89f5`, `10824f5` | CI-/Releaseausgabe angepasst. | Lokale Referenz- und Installationshashes nach Extender-Build angleichen. |
| `667b9bc`, `86056fc` | Fruehe Multiplayer- und lokale Player-ID-Semantik korrigiert/dokumentiert. | `GameModeHelper` und Identity-Tests neu validieren; keine fruehe Ersatz-ID 1 einfuehren. |
| `5acefed` | Reliable + AutoRestart fuer Extender-Steamnachrichten. | Eigenen reliable-send Detour entfernen. |
| `6717b21` | Finale Player-ID-Aufloesung ueber Roster/Slot-Tabelle. | Eigenen Per-Player-Identity-Detour entfernen; robusten Shared-Resolver zunaechst als Validierung behalten. |
| `632366a` | Ingame-Sender-ID an Paketverarbeitung weitergereicht. | Eigenen `processMessage`-Senderdetour entfernen. |
| `eb7f94c` | Join-Snapshot-Hook tatsaechlich installiert. | Eigenen `SendCustomInfoToMember`-Detour entfernen. |
| `d88ec65` | Deferred Per-Player-Updates werden bei Lobby-Nachrichten erneut verarbeitet. | Eigenen unlimitierten onBeforeRender-Deferralpfad entfernen und Upstreamgrenze testen. |
| `82564a5` | Neueste Asset-Mod-Version gewinnt; GUID-Duplikate werden ignoriert. | `SerpsModsHost`-Pfadverifikation anpassen. |
| `523816d` | Experimentelles Linux-/Proton-Workshop-Deployment. | `LinuxModding` wird ersetzt und darf nicht parallel hooken. |
| `6155113`, `4785eed` | Custom-Lord-Titelpfade geaendert/erweitert. | `BugfixesAndQoL` Lobby/Ingame getrennt testen; finaler Ingame-Index bleibt auffaellig. |
| `0e62185` | Steam-Sprache vor Systemkultur. | Lokalisierungs-Smoke in allen unterstuetzten Sprachen. |
| `2dab9d2` | ChoreManager im Debugmenue. | Keine Modcodeaenderung. |
| `01d2bc6` | Chore-Implementierung umgebaut und oeffentlichen Raw-Transport entfernt. | Quellbruch fuer vier Mods, aber kein Extender-Blocker: auf den vorgeprueften oeffentlichen `SendPacketToAllEx2`-Pfad migrieren; kein privater Adapter und kein Upstream-Patch erforderlich. |
| `097dbb6` | Chore-Phase per AOB statt festem Offset. | Verbessert Extenderrobustheit; Chore-Multiplayer-Smoke zwingend. |
| `4907d67`, Merge-/Releasecommits | Temporaeres Archiv entfernt und Releases 1.43.0-1.43.2 erzeugt. | Keine zusaetzliche Modsemantik. |

## 7. Sequenzieller Umsetzungsplan

### Schritt 0: Referenzbasis konsistent herstellen — erledigt

1. Der kanonische Fork blieb unveraendert auf Tag `v1.43.2`/Commit `ac291f...`.
2. Die vorgesehene Extender-`build.bat` wurde ausgefuehrt; `bin/net481` und `mod_output` enthalten jetzt denselben Build dieses Commits.
3. Lokaler Source-Build und `mod_output`: SHA-256 `69CF4D019DE7D63F610F39285A9FA0B6873D574E78B735568EBB85DAB9724ACA`, ProductVersion `1.0.0+ac291f23d52435018d7851db288c17668c4a171f`.
4. Installierte offizielle DLL: SHA-256 `6DB7544BE51B9FEB6263E833CF564F1326906B3E6A255F5777F168EFE5FCC1A1`, FileVersion `1.43.2.0`, ProductVersion `1.43.2+2a1f4bed5458f6ec579a920c9ef7d510d4876d9d`.

Abnahme erfuellt: Die von den Modprojekten bevorzugten lokalen Pfade zeigen nicht mehr auf die alte `235471...`-Binary, sondern eindeutig auf den neu gebauten Tag-Commit `ac291f...`.

### Schritt 1: Testharness auf den finalen 1.43.2-Vertrag bringen

- `_inspect/HostClientPresetTests`:
  - `IsPlayerSlotMappingAvailable()` im Stub ergaenzen.
  - Fruehe lokale Per-Player-Aenderung mit Player-ID `-1` darf nicht in Slot 1 geraten.
  - Eingehende Per-Player-Aenderung ohne Slot-Tabelle wird deferred und nach Mapping genau einmal angewendet.
  - Join-Snapshot, reliable lobby delivery und Ingame-Sender kommen aus dem simulierten Extenderpfad, nicht aus Shared-Detours.
  - Test, dass keine Klasse `ScriptExtenderMultiplayerSyncWorkaround` und kein Reflection-Hook auf `ApplyPerPlayerUpdate`, `processMessage`, `SendPacketToAllLobby` oder `SendCustomInfoToMember` verbleibt.
- `_inspect/LobbyModSettingsPresetTests` entsprechend aktualisieren.
- `CustomCustomTrail.Tests` weiter gegen ausschliesslich `[SyncHostOnly]`-Mission-Snapshots laufen lassen.
- Bestehende Identitaetstests beibehalten: finaler Roster-/Lobby-Slot gewinnt, keine geratene Ersatz-ID.

Abnahme: Tests schlagen mit dem alten Shared-Workaround gezielt fehl und werden erst nach Schritt 2 gruen.

### Schritt 2: Obsoleten Settings-Workaround zentral entfernen

- In `Shared/PresetLobbyModSettingsViewModel.cs` die gesamte Klasse `ScriptExtenderMultiplayerSyncWorkaround` entfernen.
- Den Aufruf `ScriptExtenderMultiplayerSyncWorkaround.EnsureInstalled(log)` aus der Registrierung entfernen.
- Alle nun unbenutzten Reflection-, Detour-, GCHandle- und Steamworks-Hilfsimports nur entfernen, wenn sie nicht vom restlichen Presetsystem gebraucht werden.
- Preset-/Trail-/Persistenzcontroller und den fachlichen Per-Player-Roster-Lifecycle unveraendert erhalten.
- Versionsgebundene Kommentare in `Shared/GameModeHelper.cs` auf den 1.43.2-Status aktualisieren. Multi-Source-Pruefungen nicht vor der realen Abnahme entfernen.

Abnahme: Kein Shared-Code detouriert mehr Extender- oder Vanilla-Netzwerkmethoden; genau ein Upstreampfad authentifiziert und appliziert Settings.

### Schritt 3: Gold-API migrieren

- `BugfixesAndQoL/src/CtrlMarketTradeHook.cs`:
  - lokale Goldwerte auf `int` umstellen;
  - Vergleiche mit Kaufkosten ohne unsichere `uint`-Casts ausfuehren;
  - negative native Zwischenwerte fail-closed beziehungsweise fachlich als nicht verfuegbares Gold behandeln.
- `StartConditions/src/StartConditionsRuntime.StartResources.cs`:
  - `(uint)setGold` entfernen;
  - Eingabebereich vor `SetPlayerGold(int,int)` validieren;
  - bestehende Startressourcen-Semantik fuer 0, Normalwerte und hohe Werte testen.
- Workspaceweit erneut nach `SetPlayerGold`, `GetPlayerGold`, `AddPlayerGold` und `r_TotalGoodsGold` suchen.

Abnahme: Kein Aufruf bindet mehr an UInt32-Signaturen; keine unchecked signed/unsigned-Konvertierung bleibt.

### Schritt 4: Chore-Migration auf die typisierte 1.43.2-API

- Einen einzigen gemeinsamen Sendepfad pro Mod verwenden, der:
  - den typisierten Packet-Body mit dem registrierten Packet-ID-Vertrag sendet;
  - `true` nur liefert, wenn alle Chore-Vorbedingungen erfuellt waren und der native Queue-Aufruf normal zurueckkehrte;
  - bei Nichtverfuegbarkeit, Uebergroesse oder Exception `false` liefert;
  - durch Vorpruefung verhindert, dass der aktuelle 1.43.2-Code auf Steam zurueckfaellt;
  - keine separate lokale Simulation mutiert, sondern wie bisher auf den sender-eingeschlossenen Chore-Empfang wartet.
- Migrieren:
  - `BugfixesAndQoL`: vier Funktionsgruppen;
  - `ExtraFeatures`: vier Funktionsgruppen;
  - `RandomEvents`: zentraler Raw-Chore-Helper;
  - `ChoreTestMod`: Transport- und Serializerprobe.
- Vorhandene Payloadgrenzen gegen den Extenderwert 1200 Bytes inklusive zweibyte Packet-ID pruefen.
- Einen gemeinsamen Helper einsetzen, der vor `GameNetworkAPI.SendPacketToAllEx2(..., viaChore: true)` Netzwerkinitialisierung, Hook, `ChoreManagerVA`, unveraenderliche Serialisierung und die 1200-Byte-Grenze prueft. Nur dieser vorgepruefte Aufruf ist fuer die simulationskritischen Migrationsfaelle zulaessig.

Abnahme: Statischer Audit findet kein `ChoreNetworkTransport`; jeder simulationskritische `SendPacketToAllEx2(... viaChore: true)`-Aufruf laeuft ausschliesslich durch den vorgeprueften Helper. Uebergroesse, fehlender Manager oder Exception fuehren zu keiner lokalen Mutation und keiner expliziten Steam-Sendung.

### Schritt 5: `SerpsModsHost` auf GUID-idempotente Registrierung abstimmen

- Nach jeder Child-Registrierung `TryGetRegisteredDirectory` verwenden.
- Pfade kanonisch und case-insensitive mit dem erwarteten Packpfad vergleichen.
- Ein bereits aus einem separaten Ordner registrierter gleicher GUID darf `registeredCount` nicht erhoehen.
- Den bestehenden Dateisystem-Duplikatdetektor behalten; die neue API ist die autoritative Laufzeitkontrolle.
- `_inspect/SerpsModsHostDuplicateTests` um „gleiche GUID, anderer registrierter Pfad“ sowie „spaeterer neuerer Child kann vorhandene Registrierung nicht ersetzen“ erweitern.

Abnahme: Diagnosezaehler und H004 melden exakt den tatsaechlich geladenen Ordner.

### Schritt 6: Linux-Mod nach Benutzerentscheidung bereinigen

- Bei vollstaendiger Entfernung alle aktiven Paket-/Releaseverweise gezielt ermitteln und erst nach bestaetigtem Umfang entfernen.
- Bei launcher-only Variante:
  - `LinuxModding.dll` und `LinuxWorkshopUpdaterBridge` entfernen;
  - keine Reflection-/Detourverbindung zu `MapModManager` behalten;
  - Launcher/Installationsskripte nur behalten, wenn sie eine vom 1.43.2-Updater nicht abgedeckte, nachgewiesene Aufgabe besitzen;
  - Tests auf den externen Ablauf begrenzen.
- Anti-Tamper nicht deaktivieren und keine geschuetzte Updaterfunktion hooken.

Abnahme: Installierter aktiver Code enthaelt keinen Hook auf eine der vier geschuetzten Methoden.

### Schritt 7: Custom-Lord- und Kulturverhalten revalidieren

- `BugfixesAndQoL` mit mindestens zwei Custom Lords und mehreren Subtypes pruefen:
  - Lobbyanzeige;
  - Ingame-Name ueber `OnScreenText.getComputerName`;
  - lokalisierte und doppelte Titel;
  - Sprachwechsel beziehungsweise Steam-Sprache.
- Belegen, welcher Teil bereits korrekt upstream geliefert wird.
- Erst danach redundanten Lobbycode entfernen. Den Ingame-Subtype-Fix nicht entfernen, solange finaler 1.43.2-Code beziehungsweise Laufzeitlogs die falsche Indexquelle zeigen.

Abnahme: Keine doppelten Titel, keine falsche Slot-/Subtype-Zuordnung und korrekte Localequelle.

### Schritt 8: Native Signaturen gegen die kanonische Spiel-DLL pruefen

Vor dem ersten Mod-Build alle produktiv verwendeten AOBs/Bytepattern gegen SHA-256 `FBCB...` pruefen:

- genau ein Treffer im vorgesehenen PE-Bereich;
- alle abgeleiteten Displacements/Zieladressen innerhalb des Moduls;
- Strukturstrides und relevante Feldoffsets unveraendert oder neu belegt;
- Referenz-RVAs nur verwenden, wenn ihr deklarierter Hash wirklich passt;
- keine Validierung gegen `x86_64/CrusaderDE.dll` als Ersatz.

Besonders betroffen sind eigene native Pfade in `ActiveAIVDetector`, `AssassinCombatFix`, `BugfixesAndQoL`, `BuildingCosts`, `CastlePlanner`, `ExtraFeatures`, `HunterQueryTargetDiagnostic`, `ImprovedHunters`, `MoatCommandTest`, `MoveMoatTest`, `MPTest` und `RandomEvents` sowie die von ihnen gelinkten Shared-Resolver.

Abnahme: Jeder produktive Patternpfad besitzt einen reproduzierbaren eindeutigen Treffer oder wird fail-closed deaktiviert. Ein Fehlschlag fuehrt zu einer separaten fachlichen Entscheidung statt zu einem geratenen RVA.

### Schritt 9: Gesamtaudit, Versionen und Builds

Alle Code- und Testpruefungen abschliessen, bevor die erste Mod-`build.bat` ausgefuehrt wird:

1. HostClientPresetTests ueber den dokumentierten klassischen MSBuild-/EXE-Pfad; die EXE erhoeht ausfuehren.
2. LobbyModSettingsPresetTests und `CustomCustomTrail.Tests`.
3. SerpsModsHost-Duplikattests.
4. Betroffene fachliche Tests fuer Chore, Gold, RandomEvents und Custom Lords.
5. XAML-Audit auf Tooltips, Locale-Key-Paritaet, Suchmetadaten und ScrollViewer.
6. Native Pattern-/Layoutpruefung gegen die kanonische DLL.
7. CRLF- und Audit auf versehentlich sichtbar ausgeschriebene Backslash-Zeilenumbruchsequenzen in allen geaenderten Textdateien.
8. Statische Suche nach entfernten APIs, privaten Reflection-Adaptern, alten Workaroundkommentaren und unsigned Goldsignaturen.

Versionen waehrend der Test-/Debugphase nicht anheben. Erst wenn ein Modfix final abgenommen ist, seine aktiven Plugin-, Assembly-, Manifest-, Host-, Paket- und `info.json`-Versionen atomar erhoehen und die alte/neue Version modweit konsistent pruefen.

Danach jede tatsaechlich geaenderte Runtime-Mod genau einmal ueber ihre eigene `build.bat` direkt und erhoeht bauen/installieren. Aufgrund der zentralen Shared-Aenderung sind mindestens die 13 in Abschnitt 4 genannten Presetmods betroffen. Zusaetzlich `ChoreTestMod` nur als Diagnosemod und `LinuxModding` nur, falls nach der Entscheidung noch ein buildbares Runtimeprojekt existiert.

## 8. Ingame- und Multiplayer-Abnahme

- Neuer Spielstart mit 1.43.2; BepInEx-Log ab der echten Startmarke auswerten.
- Extender-Startup:
  - keine alte 1.42-Binary geladen;
  - keine Anti-Tamper-Warnung;
  - Crashhandler initialisiert oder klar diagnostiziert;
  - alle eigenen AOBs eindeutig aufgeloest.
- Singleplayer-Skirmish und Trail:
  - echte Multiplayerklassifikation bleibt false;
  - Presets, Reset, Neustart und Trailwechsel erhalten lokale/Host/Per-Player-Werte korrekt;
  - Gold- und Startbedingungen funktionieren an Grenzwerten.
- Echter Host-/Client-Lauf mit frischem Clientprofil:
  - Hostwerte vor Clientbeitritt werden durch genau einen Join-Snapshot geliefert;
  - grosse Live-Settings deutlich ueber einem einzelnen Fragment kommen reliable an;
  - Host-only-Updates nach Kartenstart werden mit authentifiziertem Sender akzeptiert;
  - `RandomEvents`-Readiness empfaengt einen authentifizierten, eindeutig zuordenbaren Sender und benoetigt keine Missing-Sender-Ausnahme mehr;
  - fruehe Per-Player-Werte warten auf die Slot-Tabelle und landen genau im finalen Slot;
  - keine fremden Host-/Trailwerte gelangen in lokale `.msgpack`;
  - Shared-Logs enthalten keine Meldung ueber installierte temporaere Transportworkarounds.
- Chore-Funktionen:
  - Queue-Erfolg und Ausfuehrung mit Operation-ID, Tick und Millisekundenzeitstempel belegen;
  - Nichtverfuegbarkeit/Uebergroesse fuehrt zu keiner lokalen Mutation und keinem Steam-Fallback;
  - Host und Client wenden jede Operation genau einmal und im selben Takt an.
- Bulk-Unit-Regressionsprobe:
  - Beduinenheiler und Mauerzielwahl gezielt ausloesen;
  - Log auf `TryGetUnitById` mit ID `0` oder negativer ID pruefen;
  - bei erneutem Treffer erst den ausloesenden Hook instrumentieren, nicht pauschal einen Extenderfix unterstellen.
- Asset-Pack:
  - jeder Child-GUID zeigt auf den erwarteten Packpfad;
  - separates Duplikat wird gemeldet und nicht als erfolgreicher Child gezaehlt.
- Custom Lords und Steam-Locale gemaess Schritt 7.

## 9. Definition of Done

Das Update ist abgeschlossen, wenn:

- alle Modprojekte nachweislich gegen eine lokale 1.43.2-Referenz und nicht gegen die alte `235471...`-DLL gebaut wurden;
- kein Runtimecode mehr `ChoreNetworkTransport` referenziert;
- simulationskritische Chorepfade den vorgeprueften oeffentlichen `SendPacketToAllEx2`-Pfad verwenden und bei nicht erfuellten Chore-Vorbedingungen ohne Steam-Sendung abbrechen;
- alle Goldaufrufe signed und binaer zu 1.43.2 kompatibel sind;
- `ScriptExtenderMultiplayerSyncWorkaround` vollstaendig entfernt ist und die Settings-Synchronisation nur noch den Upstreamtransport verwendet;
- Preset-, Trail-, lokale Persistenz- und Companion-Array-Semantik unveraendert korrekt bleiben;
- `SerpsModsHost` den tatsaechlich registrierten GUID-Pfad prueft;
- `LinuxModding` keine geschuetzte Extenderfunktion mehr hookt;
- Custom-Lord-Lobby/Ingamepfade ohne Regression funktionieren;
- alle produktiven nativen Signaturen gegen die aktuelle kanonische Spiel-DLL validiert sind;
- alle Tests, XAML-/Locale-/CRLF-/Versionsaudits gruen sind;
- final geaenderte Mods atomar konsistente neue Versionswerte besitzen und ueber ihre `build.bat` gebaut/installiert wurden;
- ein echter Host-/Client-Lauf die vier reparierten Settingspfade und die Chore-Fail-closed-Semantik belegt.

## 10. Bewusst nicht geplante Aenderungen

- Keine Aenderung am kanonischen `shcde-script-extender`-Quellbaum.
- Kein ungepruefter Aufruf von `SendPacketToAllEx2` fuer simulationskritische Aktionen und kein Reflection-/Low-Level-Ersatz fuer die entfernte rohe Chore-API.
- Kein Abschalten von Anti-Tamper zugunsten des alten Linux-Hooks.
- Keine pauschale Entfernung des Custom-Lord-Titelfixes ohne getrennte Lobby-/Ingame-Evidenz.
- Keine Entfernung des robusten `GameModeHelper`- und Roster-Lifecycles allein aufgrund neuer Upstreammethoden.
- Keine Persistenzmigration weg vom gemeinsamen Preset-/Trailformat.
- Kein Versionsbump waehrend offener Tests oder vor finaler Abnahme.
- Kein Build nicht betroffener reiner CLI-/Core-Projekte allein wegen des Extender-Releases.
- Keine Bearbeitung vorhandener Benutzerarbeit in `MoveMoatTest` oder sachfremder Workspace-Dateien.
