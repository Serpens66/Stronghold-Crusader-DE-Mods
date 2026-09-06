# Status: SHCDE Script Extender 2.2.0

Zielcommit: `10d28f717d38166e5875c666f20fc5653ae44b0c` (`v2.2.0`)

| Paket | Status | Nachweis |
|---|---|---|
| P0 Extender-Basis | abgeschlossen | `update.bat` aktuell; finaler Release-Build und Installation erfolgreich; Assembly `2.2.0.0` |
| P1 Inventar und Verträge | abgeschlossen | 51 Projekte, 27 C#-Runtime-Plugins plus TestMod LUA und 45 Manifestkopien; `MoatCommandTest` ist inzwischen in BugfixesAndQoL aufgegangen |
| P2 AIDefense / QueueTest | abgeschlossen | direkte 2.0.2-Unassign-Adapter entfernt; korrigierter öffentlicher Wrapper samt Vor-/Nachkontrollen und Rollback verwendet |
| P3 übrige Mods | abgeschlossen | Quellen, native Verträge, Lua, Manifeste und gemeinsame Dateien gegen 2.2.0 geprüft |
| P4 BugfixesAndQoL | abgeschlossen | zuletzt neu inventarisiert; MoveMoat-Integration berücksichtigt; Horse-Demand-Fix verwendet das benannte Interop-Feld |
| P5 semantische Baseline | abgeschlossen | `Knowledge`, `GhidraExports`, `Index` und `Validate` für `sem/FBCB9319` erfolgreich |
| P6 AGENTS / Abschlussaudit | abgeschlossen | 51/51 Projekte, 13/13 Testsuiten und 31/31 Treiber erfolgreich; installierte Pakete bytegleich |

## Referenzbasis

- Kanonischer Fork: Tag `v2.2.0`, Commit `10d28f717d38166e5875c666f20fc5653ae44b0c`; `origin/main` und `upstream/main` stimmen überein.
- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; stimmt mit `CURRENT.json` überein.
- Final gebaute `SHCDESE.dll`: Assembly-/Dateiversion `2.2.0.0`, Produktversion `2.2.0`, SHA-256 `1F3725AFC43E23C502EAE49BEB35322A6763ECA8CF22405F2E09BCB229B72F3B`.
- `mod_output/000shcdese` und `src/SHCDESE.BepInEx/bin/net481` enthalten dieselbe finale `SHCDESE.dll`.
- Der unveränderte offizielle v2.2.0-Build-Treiber verwendet `System.ValueTuple` 4.6.2 aus `lib/net462` (75.024 Byte). Die Planannahme einer vorgesehenen 16.136-Byte-net472-Datei war für diesen Release-Build nicht zutreffend.

## Vertrags- und Quelländerungen

- AIDefense und QueueTest verwenden `GameTribeManagerAPI.UnassignUnit(tribeId, unitId)`. `AIDefenseTribeUnassignAdapter`, RemoveUnit-RVA, Pattern und Delegate wurden entfernt.
- QueueTest behält Membership-Prüfung, Nachkontrolle und fail-closed Rollback. `NATIVE_CONTRACT.md` führt den Fehler von exakt 2.0.2 nur noch historisch.
- Der BugfixesAndQoL-Horse-Demand-Fix schreibt über `GameUnitManager.r_RecruitmentResultMissingGoodId`; die Layouttests prüfen `0x650`, `0x654`, `0x658`, `0x65C` per `Marshal.OffsetOf` und die Gesamtgröße `0xF7C`.
- `Shared/GameModeHelper.cs` bleibt die maßgebliche C#-Abstraktion. TestMod LUA prüft `eGameTypeModes` und `Player_GetCurrentGameTypeMode` rein lesend bei neuem und geladenem Spiel.
- Der Extender-Standard `MaxNativeCrashDumpFiles = 5` wurde statisch bestätigt und nicht überschrieben.
- Workspaceweit wurden keine Extender-`Unload`-Aufrufe und keine alten direkten Tribe-Remove-Adapter gefunden. Vorhandene `OnDestroy()`-Methoden behalten prozessweite Runtimes beim Startup-Cleanup bei oder bereinigen nur auf nachgewiesenem Application-Quit.
- Keine Runtime-Paketkopie enthält privat `SHCDESE.dll`, RedBird oder `System.ValueTuple.dll`.
- `AGENTS.md` dokumentiert die korrigierten 2.2.0-Verträge, neue Interop-Felder, Game-Type-Fallback, ValueTuple-Herkunft, Crashlog-Standard und Baseline-Identität.
- README-Dateien und Modversionen wurden nicht geändert.

## Semantische Baseline

- `Knowledge`: 478 Quell-/Headerdateien, 334 Patterns, 137 Delegates, 105 Typen, 9.492 Typfelder und 345 VTable-Einträge.
- `GhidraExports`: aktuelle Baseline 4.478 Funktionen, davon 4.475 dekompiliert und drei bekannte Fehlschläge; historische Vergleichsbasis 4.476/4.473/drei.
- `Index`: SQLite SHA-256 `9CCEC6EDC4BC57B6DCE4F32F681486869BA625EBA95E0FBA42FA1367E8D108CA`, 154.914.816 Byte; logischer und physischer Index stimmen überein.
- `Validate`: `rawBaselineUnchanged=true`, `scriptExtenderUnchanged=true`, 77 P/Invokes, SQLite-Integrität und beide Ghidra-Validierungen grün.
- Die Rohbaseline und XAML-Ressourcen wurden bei unverändertem Native-Hash nicht neu erzeugt.

## Builds und Tests

- Alle 51 inventarisierten Projekte wurden in Release gegen `shcde-script-extender/mod_output/000shcdese` gebaut. 43 liefen im ersten Durchlauf; acht SDK-Projekte wurden nach einem ausschließlich infrastrukturellen NuGet-Audit-/Sandboxfehler erhöht wiederholt. Endergebnis: 51/51 ohne Compilerfehler.
- Alle 13 Testprogramme sind grün: AIVParser 38/38, AIVPlacement 29/29, FriendlyMoatMovement inklusive 18.258 Suchassertions und 1.469.340 Cursorvergleichen, ImprovedMoatFilling/native contracts, CastlePlanner 57/57, CustomCustomTrail 40/40, EnemyGatePathfinding 206, ExtremePowers API, MapParser 36/36, OxTether 75, QueueTest 420, ShieldTower sowie TrailEditor 9/9.
- Klassische .NET-Framework-Testprojekte wurden direkt über ihre EXE ausgeführt. CustomCustomTrail benötigte für seine atomaren Temp-Datei-Tests erhöhten Dateisystemzugriff; der in der Sandbox aufgetretene `UnauthorizedAccessException` war nicht fachlich.
- Testhost-Warnungen betreffen bekannte Assembly-Bindungen beziehungsweise NuGet-Audit-Metadaten. Die Runtime-Projekte selbst sind ohne Fehler gebaut.
- Alle 31 vorgesehenen `build.bat /nopause`-Treiber liefen erhöht und ohne Fehler; dies umfasst 27 C#-Runtime-Mods sowie AIVParser, AIVPlacement, MapParser und TrailEditor. BugfixesAndQoL wurde als letztes Modpaket gebaut und installiert.
- 1.402 Dateien aus 27 installierten C#-Runtime-Paketen wurden per SHA-256 verglichen; es fehlen keine Dateien und es gibt keine Abweichung. Der installierte Extender ist ebenfalls `2.2.0.0` mit Hash `1F3725AFC43E23C502EAE49BEB35322A6763ECA8CF22405F2E09BCB229B72F3B` und Manifestversion `2.2.0`.
- Sämtliche 28 Runtime-Manifeste (27 C# plus TestMod LUA) fordern mindestens 2.2.0. LinuxModding und TestMod LUA besitzen keinen Installations-Treiber und waren bereits vor dem Audit nicht im Spielordner installiert; ihre Workspace-Dateien wurden separat geprüft.
- Im installierten Pluginbaum existieren weder ein altes `MoatCommandTest_Serp`-Paket noch private Extender-, RedBird- oder ValueTuple-Kopien außerhalb von `000shcdese`.
- Der abschließende `git diff --check` und die CRLF-Prüfung aller geänderten Textdateien sind grün. README-Dateien und Modversionen blieben unverändert.

## Optionale Laufzeitnachweise

- Unter dem bereits für die 2.0.2-Migration ausdrücklich erteilten Laufzeittest-Waiver blockieren echte Spiel-, Host/Client- und Proton-Sitzungen den Abschluss nicht.
- Ein optionaler neuer Spielstart kann Extender-Start, späte Runtime-Marker, Tribe-Zuweisung und Horse-Demand bestätigen. TestMod LUA muss für seinen Enum-/Getter-Smoke zuvor bewusst installiert werden.
- `BepInEx/config/SHCDESE.cfg` existiert vor dem ersten 2.2.0-Spielstart noch nicht. Daher ist der statisch bestätigte Standard `MaxNativeCrashDumpFiles = 5` noch nicht als erzeugter Konfigurationseintrag nachweisbar.
