# Vor-Release: offene Punkte develop → main

Stand 2026-10-22, abends. Hier steht nur noch, was vor bzw. beim Release offen ist. Erledigtes steht in den Changelogs (`info.json`) und in der Git-Historie (Mod-Repo bis `8b6ebb9b3` „1002“, APIShared bis `e72c436`).

## 1. Commit (Nutzer)

Seit den letzten Commits noch nicht committet. Parallel arbeiten andere Chats an BFQoL und APIShared (z. B. APIShared 0.12.6, Native-Switches, Modsettings-Tests). Vor dem Commit deshalb `git status` prüfen.

**APIShared**
- Neue öffentliche Provider-APIs (im 0.12.6-Changelog):
  - `RouteSearchEvents.BeginSearch`
  - `RouteSearchContext`-Konstruktor
  - `AssassinPathAPI.BeginRouteOutput`
  - `EnemyBridgeDiagnosticBridge.PublishTopologyOwner`
  - `GateRoutingNativeLayout`
  - Slot-Layout-Properties in `GateNativeSearchScopes`
- Public-API-Baseline: nur Zusätze.
- `InternalsVisibleTo("BugfixesAndQoL")` und `FriendInternals.BugfixesAndQoL.txt` sind entfernt; die Friend-Tests kommen jetzt auch ohne Friend-Konsumenten aus.
- Review-Fixes:
  - Markup-Erweiterungen in der ModSettings-Suche
  - Workshop: Neuversuch der Abo-Liste, kein doppeltes Cancel, Upload ohne Dispatcher schlägt fehl
  - Suche faltet kulturunabhängig
  - Recruitment-Hover-Handler werden nur einmal angehängt
- Audio: Der Managed-Teil hat keinen Assembly-CSharp-Hash mehr (IL-Vertrag); der native Teil nutzt den einmal berechneten Native-Hash.
- Doku: Katalog, `GATE_ROUTING.md`, `ARCHITECTURE.md`, `API_COMPATIBILITY.md`.
- Tests: Core.Tests 302/302, APISharedTests 371/371 (1 übersprungen), Doku-Linkprüfung PASS.

**Mod-Repo**
- BFQoL nutzt nur noch öffentliche APIShared-APIs. Dazu gehören Routensuche, Assassinen-Ausgabe, `GateRoutingNativeLayout`, Slot-Layout, ein eigenes `CommandDetourBackend` und eine DEVDIAG-Kopie des Callback-Emitters; die Fixtures sind angepasst.
- Native Prüfungen:
  - neuer `BfqolNativeCache` über `Shared/Runtime/Native/NativeResolutionCache.cs` und `NativeInstructionOperands.cs`
  - persistenter Datei-Hash-Cache in `Shared/Runtime/Diagnostics/DebugLogHelper.cs`
  - NotificationQueue mit Referenz-RVAs
  - Formation und neun weitere Stellen ohne Vollscan bei bekanntem Hash
  - neue Tests in `Shared/Tests/Native.Tests` und `Diagnostics.Tests`
- SavegameMissingMap ohne Assembly-CSharp-Hash (IL-Vertrag plus kompilierter Dry-Run).
- `PermanentNativeSwitch`: Ein fremder Hook (Script Extender bei 0x8EA60) an einer belegten Grenze wird akzeptiert; behebt den Startfehler des Friedenszeit-Fixes. Ein Test dafür ist ergänzt.
- AGENTS-Regeln:
  - `native-analysis.md`: Prüfkosten, Offset-Ableitung mit Zweitbeleg
  - `native-hooks.md`: fremde Hooks, Prüfung am unveränderten Abbild
  - `performance.md`: Verweis auf das Prüfverfahren
- Neu: `HashAdressTODO.md`; Baseline-Wissen `MOVE_STAGER_DATA_ANCHORS.md` mit Index in `CURRENT.md`; Abschnitt in `BugfixesAndQoL/UpdateToNewDLL.md`.
- Ältere, noch nicht committete Punkte:
  - DEVDIAG-Reparatur nach B1
  - Test-Stubs
  - `Issue6.Tests` und `StartSettings.Tests`
  - ExtendedData-Kartencache-Grenze
  - `errorlevel`-Umstellung in den `build.bat` und der BOM-Fix in `Test-BuildDrivers.ps1`
- Grün:
  - BugfixesAndQoLNativeTests, VanillaPeaceTimeNativeTests, ImprovedMoatFilling.Tests
  - ExtendedShiftCommandQueue.Tests, FormationSpeech.Tests, ModsettingsNative.Tests
  - GateRouting.Tests, AssassinPathfinding.Tests, MoatMove-Suite, `Test-RuntimePreflight`
  - Consumer-Scan ohne Interna

**Rot durch fremde, uncommittete Arbeit (nicht durch diese Änderungen)**
- `Test-SharedBoundaries`: LF in `BugfixesAndQoL/tests/Modsettings.Tests/Modsettings.Tests.csproj`.
- `Test-UnitCommandSplit` und ein Compile ohne Workspace-Targets: `Shared.DevDiagRuntime` fehlt für `UnitCommandPathRuntime.Spikes.DevDiag.cs`.
- Gate-Acceptance-Harness und FriendlyMoatMovement.Tests: Die Fixtures kennen `partial void DiagFinishBuildingApproach` nicht.
- Nach der Korrektur durch den zuständigen Chat diese vier erneut laufen lassen.

## 2. Bauen und installieren (Spiel vorher schließen)

1. **APIShared neu bauen** (`build.bat /nopause`). Das Paket unter `APIShared/BepInEx/plugins/APIShared_Serp` ist zurzeit gemischt: Ein Testlauf hat dort die `APIShared.dll` neu erzeugt, nachdem ein anderer Chat das 0.12.6-Paket gebaut hatte. Die Paketprüfung meldet deshalb „Incomplete or mixed bootstrap pair“, und Verbraucher-Builds brechen ab.
2. Danach BugfixesAndQoL normal bauen, nicht als DEVDIAG. Installiert ist noch die DEVDIAG-Version vom 22.10., 18:08, und eine DEVDIAG-Version darf nie veröffentlicht werden. Der Build braucht das APIShared-SDK mit den neuen APIs, also APIShared committen oder den Arbeitsstand als SDK wählen.
3. ExtendedData, StartConditions und MoatMove per `build.bat /nopause` bauen und installieren.
4. Nach dem Start im Log prüfen:
   - Info-Zeilen erscheinen nur einmalig pro Start oder bei Nutzeraktionen.
   - Mit Fixes erscheint `Fixes wheat CORRECTED` bzw. `ALREADY-CORRECT`.
   - `Native address resolved: … method=reference-rva` erscheint für NotificationQueue; nirgends steht `signature-fallback`.
   - Der Friedenszeit-Fix startet ohne „Native switch following live instruction differs“.
   - SavegameMissingMap meldet `READY`.
5. Den BFQoL-Release-Build vor der Veröffentlichung mit `APISHARED_CONSUMER_SCAN` gegen APIShared scannen. Es dürfen keine Interna gebunden sein: Ohne Grant würden sie zur Laufzeit scheitern.

## 3. Release-Reihenfolge (Nutzer)

1. APIShared `dev/Serpens66` → develop → main mergen, Tag für die veröffentlichte Version (derzeit 0.12.6), GitHub-Release und Workshop-Update. Erst danach das Modpack gegen das veröffentlichte SDK bauen; BFQoL verlangt APIShared 0.12.6.
2. Mod-Repo develop → main, Modpack-Release. Alle Mods erscheinen zeitgleich; ältere BFQoL-Versionen sind mit dem neuen APIShared nicht kompatibel, weil der Friend-Zugriff entfernt ist.
3. Auf den Steam-Workshop-Seiten von APIShared und Modpack ergänzen: Alle Spieler einer MP-Partie brauchen dieselben Versionen von APIShared und Modpack. Diese Texte stehen nicht im Repo.
4. Nach dem Release-Build die committeten `*/BepInEx/plugins/*/info.json` mit den Quell-`info.json` abgleichen (einige waren veraltet, z. B. ExtraFeatures 1.0.114, RandomEvents 1.0.52, SerpsModsHost 1.0.28).

## 4. Spieltests (Nutzer)

Bisher ist keiner durchgeführt. Vorrangig 1–4 und 13.

1. **Bewegung durch eigene Gräben (B1):** ohne MoatMove keine Bewegung durch eigene Gräben, kein Lag mehr. Dazu Angriffe von Einheiten mit Grab-Fähigkeit nahe eigener Gräber, Gebäudeangriffe, Feindgraben füllen und Bewegung aus dem eigenen Graben, jeweils mit und ohne MoatMove. Lag-Vergleich gegenüber dem bisherigen Stand.
2. **MP-Lobby und Presets (Host und Client):** Beitritt, Slotwechsel, Hoständerungen vor und nach dem Beitritt, Preset laden, Kartenstart. Ein Setting, das einen Wert ablehnt, darf das Preset nicht blockieren.
3. **Coop-Paketmission im MP mit mehreren Burgen pro KI-Slot:** Host unverändert; Host ändert nach der ersten Burg (es soll die synchronisierte Hostliste genommen werden); BFQoL-Synchronisierung aus (Meldung am Startknopf erwartet).
4. **Sprite-Merge:** Modus Automatisch auf einer Karte mit weniger als 8 GB und einer mit mindestens 8 GB VRAM (Logzeile `SPRITE_SHEET_MERGE_*`), Wechsel An/Aus mit Neustart, ComboBox in den Modoptionen.
5. **Upgrade:** von einer 0.9.2-/Modpack-main-Installation mit Presets, AI-Auswahl-Store und Saves. Presets sind danach in `Documents/SHCDE/APIShared`, die Migration läuft nur einmal, alte Saves lassen sich laden.
6. **MP-Save/Load:** beide Peers auf gleicher Version; ein Peer mit alter APIShared-Version (erwartet: sofortige Blockade mit Meldung beider Protokollversionen); Solo-Host lädt einen MP-Save; Save mit pausiertem Einzelgebäude und laufendem synchronisiertem Truppenmarsch.
7. **Neustart:** Skirmish-Neustart SP und MP; Neustart aus dem Editor; „Ja“ im Neustart-Dialog. CastlePlanner Preview → Commit → Restart im MP (Host und Client); prüfen, ob der 30-s-Timeout von `FreeCastleIdentityReadiness` bei langsamen Peers reicht.
8. **Determinismus MP:** Assassinen über Tore, Mauern und Zugbrücken (auch mit Fixes); Tor erobern und sofort durchlaufen; mehrere Minuten ohne Desync. MP-Rekrutierung mit UnitCosts und UnitLimit. Shift-Patrol und Formationen im MP (Chore-Synchronisierung).
9. **Meldungen und Sprache:** Minimap-Video mit Sprache, Briefing schließen und wieder öffnen, schnelle Meldungsfolgen, Linksklick zum Ausblenden, Kartenwechsel. Formationsbefehle mit Sprachausgabe.
10. **Laden mit ExtendedData und APIShared:** Beide Lade-Dialog-Hosts setzen `Margin 45,360`; prüfen, dass sie nie gleichzeitig sichtbar sind. Save mit fehlender Karte laden (SavegameMissingMap ohne Datei-Hash).
11. **Kompatibilität:** alles einmal zusammen mit dem Fixes-Mod und dem aktuellen Script Extender; Startzeit messen (`startup menuReadySinceProcessStartS`) und mit dem vorherigen Stand vergleichen. Friedenszeit an- und abschalten.
12. **Karteneditor:** Start, Gate-Routing, Speichern im Editor; Trail Maker mit einer Trail ohne installierte Karte (Kartencache).
13. **Workshop:** ModSettings-Suche (auch mit türkischer Systemsprache), Upload eines eigenen Items, Startbehandlung bei laufenden Downloads.

## 5. Hinweise für spätere Spiel- oder Extender-Updates

- **Hashgebunden bleiben:**
  - die gemeinsame Befehls-Runtime `UnitCommandPathRuntime` (etwa 40 Detours, rund 100 Konstanten) und damit ExtendedShiftCommandQueue sowie Formation/FormationSpeech
  - weitere BFQoL-Features mit festen Daten-RVAs
  - PopularityDisplay (Managed-Hash, kein IL-Vertrag)

  Die vollständige Liste, die Reihenfolge und die schon auditierten Anker stehen in `HashAdressTODO.md` und `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOVE_STAGER_DATA_ANCHORS.md`.
- **Nach einem Spielupdate:** Funktionen mit Pattern-Fallback suchen einmal und cachen das Ergebnis (`BepInEx/cache/BugfixesAndQoL_Serp.native-cache.tsv`). Ab dem zweiten Start entfällt der Scan.
