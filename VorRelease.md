# Vor-Release: offene Punkte develop → main

Stand 2026-10-22, 19:00. Hier steht nur noch, was vor bzw. beim Release offen ist. Erledigtes steht in den Changelogs (`info.json`) und in der Git-Historie (Mod-Repo bis `f55021711` „1001“, APIShared bis `e72c436`).

## 1. Commit (Nutzer)

Seit den letzten Commits noch nicht committet, alles geprüft:

- **Mod-Repo:**
  - DEVDIAG-Reparatur nach der Verschiebung in B1 (`TraversalProvider` mit virtuellen Diagnose-Membern, `CursorConnectivity.cs`, `UnitCommandPathRuntime.Diagnostics.cs`, MoatMove `Moved.CursorConnectivity.cs`/`Moved.Diagnostics.cs`)
  - Test-Stubs (`FormationSpeech.Tests/RuntimeFixture.cs.txt`, `_inspect/AssassinDescentRuntimeTests/FeatureHarness.cs`)
  - `BugfixesAndQoL/tests/Issue6.Tests` (nur noch Patrol) und neu `StartConditions/tests/StartSettings.Tests`
  - ExtendedData-Kartencache-Grenze samt Test und Changelog
  - Umstellung der `errorlevel`-Prüfungen in 49 `build.bat` sowie der Interaktiv-Fix in `Shared/Tools/Validation/Test-BuildDrivers.ps1` (Stdin ohne UTF-8-BOM, sonst beendete die BOM unter PowerShell 5.1 die Pause)
  - BFQoL nutzt statt `APIShared.Internal.{DebugLogHelper, UnityMainThreadDispatch, GameBuildingFootprint, NativePatternResolver, NativePatternSearchScope, NativeResolution, MissionEvents}` die quellverlinkten `Shared.*`-Gegenstücke (62 Stellen in 14 Dateien unter `BugfixesAndQoL/src/Formations` und `src/UnitCommands`). **Noch nicht kompiliert und nicht getestet:** Der Build wurde zurückgestellt, weil andere Chats parallel an BFQoL arbeiten. Vor dem Release-Build `Test-UnitCommandSplit`, die BFQoL-Tests und die MoatMove-Suite laufen lassen.
  - diese Datei
  - Nicht von mir und unverändert: die nicht getrackten `ModsettingsFix.md` und `Testmods/AISellThresholdFixTest/`.
- **APIShared:**
  - zusammengefasster 0.12.5-Changelog in `info.json` und die `errorlevel`-Umstellung in `build.bat`
  - Review-Fixes, die APISharedTests (Workshop, ModSettingsSearch, UnitHud, Recruitment) sind mit 36/36 grün:
    - ModSettings-Suche: Nicht auflösbare Markup-Erweiterungen (`{StaticResource …}`, `{TemplateBinding …}`, `{x:Static …}`) erscheinen nicht mehr als Titel bzw. Tooltip. Mit `{}` maskierter Text wird korrekt gelesen. Dazu ein neuer Test in `ModSettingsSearchCatalogTests`.
    - Workshop: Ändert sich die Steam-Abo-Liste während der Initialisierung, wird bis zu dreimal neu gelesen, statt die Workshop-Startbehandlung für die ganze Sitzung abzuschalten (`WorkshopRuntime.InitialSubscriptions`).
    - Unit-HUD-Rekrutierung: Die Hover-Handler der Varianten-Buttons werden nur einmal pro Button-Instanz angehängt.
  - Neue öffentliche Provider-APIs für 0.12.5. Sie ersetzen die Friend-Interna, die BFQoL noch nutzt; die Public-API-Baseline enthält nur Zusätze (+43 Zeilen). Core.Tests 302/302, APISharedTests 370/370 (1 übersprungen), Doku-Linkprüfung PASS:
    - `RouteSearchEvents.BeginSearch` / `HasObservers`, `RouteSearchOperation` und ein öffentlicher, validierender `RouteSearchContext`-Konstruktor
    - `AssassinPathAPI.BeginRouteOutput` mit `AssassinRouteOutputFrame` (Complete/Leave)
    - `EnemyBridgeDiagnosticBridge.PublishTopologyOwner` (jetzt öffentlich)
    - `GateRoutingNativeLayout`: Adressen, Längen, Patterns und Record-Offsets als Laufzeit-Properties sowie `ValidateComparisonSites`/`ValidateMovementSites`
    - Katalog, `GATE_ROUTING.md`, `ARCHITECTURE.md` und der 0.12.5-Changelog sind ergänzt, ebenso für die drei Review-Fixes.
  - **BFQoL ist auf die öffentlichen APIs umgestellt** (Mod-Repo, nicht committet):
    - Routensuche über `BeginSearch` (`WeightedMoatRoutePlanner.cs`). Die Anfangskosten (8 bis etwa 5·10¹⁰) liegen sicher unter der Grenze von etwa 1,8·10¹², sodass die neue Kostenprüfung nie greift.
    - Assassinen-Ausgabe über `BeginRouteOutput` (`MovementPathPublication.cs`)
    - `GateRoutingNativeLayout` statt `EnemyGatePathfindingNativeDefinition` in 6 GateRouting-Dateien. Bei `libraryBase + RVA` ist jetzt ein `(ulong)`-Cast nötig, weil die Werte keine Konstanten mehr sind (`SamePclGateRouteRuntime.cs`).
    - ein eigenes `CommandDetourBackend` mit denselben Optionen statt `AssassinPathAPI.Backend` (`NativeDetourContracts.cs`, `UnitCommandPathRuntime.NativeContracts.cs`)
    - eine DEVDIAG-Kopie des Callback-Emitters (`src/GateRouting/DiagnosticCallbackAdapterEmitter.DevDiag.cs`)
    - Fixtures ergänzt: `GateRouting.Tests.csproj`, `AssassinPathfinding.Tests` (zwei Fixtures), MoatMove `tests/Program.cs` und der aktiv genutzte Harness `_inspect/TemporaryGateAcceptanceRuntimeTests/Fixture.cs`
  - **Geprüft:**
    - Compile-Check von BFQoL Release und DEVDIAG gegen das Arbeitsstand-APIShared erfolgreich
    - Cecil-Scan beider DLLs: keine Bindung an APIShared-Interna mehr; der offizielle Test `ScannedFriendConsumersBindOnlyPinnedInternals` besteht.
    - grün: GateRouting.Tests, AssassinPathfinding.Tests (16253 Assertions), MoatMove-Suite, `Test-RuntimePreflight`
  - **Rot durch fremde, uncommittete Arbeit, nicht durch diese Umstellung:**
    - `Test-SharedBoundaries`: LF in `BugfixesAndQoL/tests/Modsettings.Tests/Modsettings.Tests.csproj`
    - `Test-UnitCommandSplit`: `Shared.DevDiagRuntime` fehlt in `UnitCommandPathRuntime.Spikes.DevDiag.cs`.
    - Gate-Acceptance-Harness und FriendlyMoatMovement.Tests: Ihre Fixtures kennen das neue `partial void DiagFinishBuildingApproach` (BuildingCandidates/Spikes) nicht.
    - Nach deren Korrektur alle vier erneut laufen lassen.
  - Ein BFQoL-Build per `build.bat` braucht ein APIShared-SDK mit den neuen APIs, also APIShared committen oder den Arbeitsstand als SDK wählen.

## 2. Bauen und installieren (Spiel vorher schließen)

Reihenfolge, weil BugfixesAndQoL und ExtendedData jetzt APIShared 0.12.5 mit der neuen Coop-API voraussetzen:

1. APIShared: am 22.10. um 17:56 gebaut und installiert (Arbeitsstand).
2. BugfixesAndQoL: am 22.10. um 18:08 als **DEVDIAG**-Version installiert (Lag-Suche). Vor dem Release normal neu bauen; ein DEVDIAG-Build darf nie veröffentlicht werden.
3. ExtendedData, StartConditions und MoatMove noch per `build.bat /nopause` bauen und installieren. Ein installiertes MoatMove liefert der DEVDIAG-Cursor-Statistik von BFQoL seine Zähler erst nach einem Neubau.
4. Der frühere Ordner `BepInEx/plugins/BugfixesAndQoL_Serp_dev` ist nicht mehr vorhanden; aktiv ist nur `BugfixesAndQoL_Serp`.
5. Nach dem Start im Log prüfen:
   - Info-Zeilen erscheinen nur einmalig pro Start oder bei Nutzeraktionen.
   - Mit Fixes erscheint `Fixes wheat CORRECTED: derived from the actual code` bzw. `ALREADY-CORRECT`.

## 3. Release-Reihenfolge (Nutzer)

1. APIShared `dev/Serpens66` → develop → main mergen, Tag `APIShared/v0.12.5`, GitHub-Release und Workshop-Update. Erst danach das Modpack gegen das veröffentlichte SDK bauen, denn das Modpack verlangt mindestens APIShared 0.12.3, BFQoL und ExtendedData sogar 0.12.5.
2. Mod-Repo develop → main, Modpack-Release.
3. Auf den Steam-Workshop-Seiten von APIShared und Modpack ergänzen: Alle Spieler einer MP-Partie brauchen dieselben Versionen von APIShared und Modpack. Diese Texte stehen nicht im Repo.
4. Nach dem Release-Build die committeten `*/BepInEx/plugins/*/info.json` mit den Quell-`info.json` abgleichen (einige waren veraltet, z. B. ExtraFeatures 1.0.114, RandomEvents 1.0.52, SerpsModsHost 1.0.28).

## 4. Spieltests (Nutzer)

Bisher ist keiner durchgeführt. Vorrangig 1–4.

1. **Bewegung durch eigene Gräben (B1):** ohne MoatMove keine Bewegung durch eigene Gräben, kein Lag mehr. Dazu Angriffe von Einheiten mit Grab-Fähigkeit nahe eigener Gräber, Gebäudeangriffe, Feindgraben füllen und Bewegung aus dem eigenen Graben, jeweils mit und ohne MoatMove. Lag-Vergleich gegenüber dem bisherigen Stand.
2. **MP-Lobby und Presets (Host und Client):** Beitritt, Slotwechsel, Hoständerungen vor und nach dem Beitritt, Preset laden, Kartenstart. Ein Setting, das einen Wert ablehnt, darf das Preset nicht blockieren.
3. **Coop-Paketmission im MP mit mehreren Burgen pro KI-Slot:** Host unverändert; Host ändert nach der ersten Burg (es soll die synchronisierte Hostliste genommen werden); BFQoL-Synchronisierung aus (Meldung am Startknopf erwartet).
4. **Sprite-Merge:** Modus Automatisch auf einer Karte mit weniger als 8 GB und einer mit mindestens 8 GB VRAM (Logzeile `SPRITE_SHEET_MERGE_*`), Wechsel An/Aus mit Neustart, ComboBox in den Modoptionen.
5. **Upgrade:** von einer 0.9.2-/Modpack-main-Installation mit Presets, AI-Auswahl-Store und Saves. Presets sind danach in `Documents/SHCDE/APIShared`, die Migration läuft nur einmal, alte Saves lassen sich laden.
6. **MP-Save/Load:** beide Peers auf gleicher Version; ein Peer mit alter APIShared-Version (erwartet: sofortige Blockade mit Meldung beider Protokollversionen); Solo-Host lädt einen MP-Save; Save mit pausiertem Einzelgebäude und laufendem synchronisiertem Truppenmarsch; ein neuer Save lädt mit BFQoL 1.0.192.
7. **Neustart:** Skirmish-Neustart SP und MP; Neustart aus dem Editor; „Ja“ im Neustart-Dialog. CastlePlanner Preview → Commit → Restart im MP (Host und Client); prüfen, ob der 30-s-Timeout von `FreeCastleIdentityReadiness` bei langsamen Peers reicht.
8. **Determinismus MP:** Assassinen über Tore, Mauern und Zugbrücken (auch mit Fixes); Tor erobern und sofort durchlaufen; mehrere Minuten ohne Desync. MP-Rekrutierung mit UnitCosts und UnitLimit.
9. **Meldungen und Sprache:** Minimap-Video mit Sprache, Briefing schließen und wieder öffnen, schnelle Meldungsfolgen, Linksklick zum Ausblenden, Kartenwechsel. In der DEVDIAG-Version prüfen, dass `OnScreenText.addOSTEntry` nur auf dem Unity-Hauptthread läuft.
10. **Laden mit ExtendedData und APIShared:** Beide Lade-Dialog-Hosts setzen `Margin 45,360`; prüfen, dass sie nie gleichzeitig sichtbar sind.
11. **Kompatibilität:** alles einmal zusammen mit dem Fixes-Mod und dem aktuellen Script Extender; Startzeit messen (ExtraFeatures installiert ElevatedMoat- und AIDefenseRepair-Hooks jetzt immer beim Start).
12. **Karteneditor:** Start, Gate-Routing, Speichern im Editor; Trail Maker mit einer Trail ohne installierte Karte (Kartencache).

## 5. Hinweise für spätere Spiel- oder Extender-Updates

- Der Fix für Saves mit fehlender Karte in BFQoL (`SavegameMissingMap`) ist noch an den Hash von `Assembly-CSharp.dll` gebunden. Nach einem Spielupdate deaktiviert er sich mit einem Error. Eine Umstellung auf Codeprüfung wie bei der Weizen-Korrektur wäre eine eigene Native-Analyse.
- Feste Daten-RVAs in BFQoL (z. B. `ChorePatrolModeRva`, FormationSpeech) sind hashgebunden und müssen nach einem Spielupdate neu auditiert werden.
- **InternalsVisibleTo-Kopplung BFQoL → APIShared:**
  - Entschieden ist, dass Verbraucher nur öffentliche APIShared-APIs nutzen. Die Ersatz-APIs stehen in 0.12.5 bereit, und BFQoL ist umgestellt (§1).
  - Ein neues BFQoL bindet keine APIShared-Interna mehr. Den Release-Build vor der Veröffentlichung noch einmal scannen (`APISHARED_CONSUMER_SCAN`).
  - Grant und Pin-Datei `FriendInternals.BugfixesAndQoL.txt` bleiben, solange ältere BFQoL-Versionen unterstützt werden. Weil Breaking Changes derzeit erlaubt sind, können sie nach dem nächsten BFQoL-Release bewusst entfernt werden.
  - Die festen Werte in `GateRoutingNativeLayout` sind Properties: Ein neues BFQoL übernimmt nach einem Spielupdate korrigierte Adressen schon mit einem APIShared-Update.
- Der APIShared-Review von Workshop (alle Dateien), ModSettings-Suche, Recruitment, TroopActionHud und GateRoutingNativeHooks/CapturerComparisonAdapterEmitter ist abgeschlossen. Die Fixes stehen in §1. Ohne Handlungsbedarf bleiben einige Randbeobachtungen:
  - Im Workshop wird beim Abbruch die Cancel-Datei doppelt geschrieben (harmlos).
  - Ist der Unity-Dispatcher nicht verfügbar, bleibt ein Upload als aktiv markiert, bis das Spiel neu startet.
  - Die Suche faltet Groß-/Kleinschreibung mit der aktuellen Kultur.
- Nicht geprüft wurde der Inhalt reiner DEVDIAG-Diagnose.
