# Pre-Release-Prüfung Modpack „Serps Mods“ (develop gegen main)

Momentaufnahme vom 31.10.2026. Geprüft wurden die Commits auf `develop` (HEAD `206d9a7af`, „1057“) gegenüber dem gemeinsamen Vorfahren mit `main` (`d54abf066`). Das sind 133 Commits. Nicht committete Änderungen im Arbeitsbaum (Spectator, Testmods) gehören nicht zum Code-Review; der Spectator-Mod ist nur in B3 als künftiger Pack-Bestandteil bewertet. Native Aussagen gelten für `CrusaderDE.dll` mit SHA-256 `FBCB9319…31E2`. Das ist der Hash aus `_inspect/CrusaderDE-Native-Baseline/CURRENT.json`, und er stimmt mit der installierten DLL überein.

**Umfang:** die Mods der Liste `Mods` in `Shared/Tools/Steam/WorkshopConfiguration.ps1` (BugfixesAndQoL, BuildingCosts, BuildingLimit, ExtraFeatures, RandomEvents, CastlePlanner, StartConditions, UnitCosts, UnitLimit, ExtendedData) sowie der Pack-Host `SerpsModsHost` und die quellverlinkten Dateien unter `Shared/`. Nur BugfixesAndQoL, CastlePlanner, ExtendedData, ExtraFeatures, RandomEvents, SerpsModsHost und `Shared` haben sich geändert. BuildingCosts, BuildingLimit, StartConditions, UnitCosts und UnitLimit sind unverändert.

**Vorgehen in zwei Durchgängen:**
1. Diff-Durchsicht aller geänderten Quellen mit ersten Funden und Fix-Vorschlägen.
2. Zweiter Prüfdurchgang: Jeder Fund wurde im Kontext des restlichen Codes erneut bestätigt. Geprüft wurden Aufrufer, Thread-Verträge, Ausnahmebehandlung der Aufrufer und das Vanilla-Verhalten (dekompiliertes `Assembly-CSharp` und Disassembly der installierten DLL). Danach wurden die Fix-Vorschläge erneut bewertet. Dabei wurden H1 präzisiert, M1 korrigiert, M2 vom Verdacht zum bestätigten Fund hochgestuft sowie zwei frühere Funde (alt N2/N3) als nicht relevant zurückgestuft.

Es wurde kein Code geändert.

---

## Übersicht

| ID | Schwere | Mod | Kurzbeschreibung | Status nach 2. Prüfung |
|---|---|---|---|---|
| B1 | Release-Blocker | alle / APIShared | Mods verlangen APIShared 0.18.0 bzw. 0.20.0; öffentliches APIShared-`main` ist 0.16.3 | bestätigt |
| B2 | Release-Blocker | CastlePlanner | Neuer Inhalt, aber Version 0.8.47 = bereits veröffentlichtes Release | bestätigt |
| B3 | Release-Blocker | BugfixesAndQoL / Spectator | Zuschauer-Funktionen aus BugfixesAndQoL entfernt; Spectator-Mod soll sie liefern, ist aber noch nicht fertig | bestätigt, Entscheidung getroffen |
| H1 | Hoch | ExtendedData | Trail Maker: „Mission speichern“ wird bei nicht erfassbarem Custom-Lord vollständig abgebrochen | bestätigt, präzisiert |
| H2 | Hoch (seltener Auslöser) | ExtraFeatures | Fehler in optionaler Settings-Migration deaktiviert die gesamte Mod | bestätigt |
| M1 | Mittel | ExtendedData | Neuer Schritt im Trail-Backup-Hook ohne Fehlerkapselung bricht Import/Leeren/Export ab | bestätigt, Folge korrigiert |
| M2 | Mittel | ExtendedData | Fehler der Lord-Präsentation verhindern `fixes.Apply` beim Missionsstart | bestätigt (war „plausibel“) |
| M3 | Mittel | CastlePlanner | APIShared-Mindestversion 0.11.1 zu niedrig für neu genutzte API (0.14.0) | bestätigt |
| N1 | Niedrig | SerpsModsHost | Host-Changelog 1.0.33 nennt falsche Versionen und lässt wesentliche Änderungen aus | bestätigt |
| N2 | Niedrig | CastlePlanner | `RefreshHud` erzeugt bei jedem Aufruf einen neuen Brush und eine Property-Benachrichtigung | bestätigt, vernachlässigbar |

Zusätzlich gibt es eine Prüfempfehlung ohne Fehlerbefund (P1, native Assassin-Suche im Mehrspieler) und eine Liste der geprüften und **nicht** bestätigten Verdachtsfälle (am Ende).

---

## Release-Blocker

### B1 – APIShared-Mindestversionen über dem öffentlichen APIShared-Stand

**Befund:**
- `BugfixesAndQoL/info.json` und `BugfixesAndQoLPlugin` verlangen APIShared **0.18.0**.
- `ExtendedData/info.json` und `ExtendedDataPlugin` verlangen APIShared **0.20.0**.
- Das öffentliche APIShared-`main` (`origin/main`, letztes Tag `APIShared/v0.16.3`) steht auf **0.16.3**. 0.18.0 und 0.20.0 gibt es nur auf `develop`.
- Die neu genutzten APIs existieren in 0.16.3 nicht: `VisibleMapRenderEvents`, `IngameMenuActions` und `MissionOverEvents` (eingeführt mit 0.18.0, Commit `966442d`, genutzt von BugfixesAndQoL) sowie `PackagedLordPresentation` (0.20.0, genutzt von ExtendedData).

**Kontextprüfung:** `Create-SteamModPack.ps1` ruft für jede Verbraucher-Mindestversion `Assert-ApiSharedMinimumVersion` gegen das gepinnte APIShared-`main` auf und bricht bei Unterschreitung ab. Die Mindestversionen sind fachlich korrekt, weil die APIs tatsächlich genutzt werden.

**Fix:** Zuerst APIShared 0.20.0 regulär von `develop` nach `main` bringen und veröffentlichen (`CreateSteamModPack.bat` mit Ziel „Beide“ oder „APIShared“). Erst danach das Modpack bauen. Die Host-Mindestversion setzt der Packer automatisch.

**Bewertung:** Das ist der vorgesehene Release-Weg. Die Mindestversionen herabzusetzen wäre falsch.

### B2 – CastlePlanner: neue Funktion ohne Versionserhöhung

**Befund:** Mit den Commits 1029, 1035 und 1048 hat CastlePlanner eine Zuschauer-Ansicht des KI-Burgplans bekommen (`BlueprintRuntimeController.Spectator.cs`, `…AivNames.cs`, `BlueprintLivePlan.cs`, `BlueprintAivNames.cs`, `BlueprintSpectatorPolicy.cs`). Geändert wurden auch DLL, XAML-Patch und Locales im Paketordner. `PluginVersion` und `BepInEx/plugins/CastlePlanner_Serp/info.json` stehen aber weiterhin auf **0.8.47**, und das Tag `CastlePlanner/v0.8.47` existiert lokal und auf `origin`.

**Kontextprüfung:** `Create-SteamModPack.ps1` vergleicht den Paketinhalt mit dem letzten Release-Commit. Ist er geändert, die Version aber gleich, bricht der Packer ab: „has changed packaged plugin content but no prepared higher version/changelog“.

**Fix:** CastlePlanner modweit atomar auf 0.8.48 anheben (PluginVersion, Manifest, ggf. Assembly-/Projektversion) und einen Changelog-Eintrag für die KI-Plan-Ansicht für Zuschauer hinzufügen. Dabei M3 gleich mit erledigen. Danach modweit nach „0.8.47“ suchen und die Treffer prüfen.

**Bewertung:** notwendig, mit geringem Risiko. Die KI-Plan-Ansicht funktioniert auch ohne Spectator-Mod: Sie nutzt Vanillas `spectatorMode` und `PlayerPerspectiveAPI` aus APIShared.

### B3 – Zuschauer-Funktionen: Spectator-Mod muss vor dem Release fertig und im Pack sein

**Befund:** Commit 1041 entfernt aus BugfixesAndQoL die Host-Optionen `EnableSpectatorPerspective` und `EnableEliminatedPlayersBecomeSpectators` sowie den Zuschauer-Statistik-Button samt Refresh. Diese Funktionen sind in der veröffentlichten Version 1.0.203 enthalten. Technisch ist die Entfernung in BugfixesAndQoL sauber: Gespeicherte Werte werden über `IsRetiredPresetProperty` stillgelegt, und die Team-Badges der Statistik bleiben erhalten (`StatisticsTeamBadges`, die XAML-Elemente sind vorhanden).

**Entscheidung (Nutzer, 31.10.2026):** Die Funktionen kommen künftig aus dem Spectator-Mod (`Spectator_Serp`), der ins Pack aufgenommen wird, aber noch nicht fertig ist. Bis dahin ist ein Pack-Release von `develop` blockiert, denn sonst fehlen veröffentlichte Funktionen.

**Stand des Spectator-Mods für die Pack-Aufnahme** (Momentaufnahme des Arbeitsbaums, kein Code-Review):

| Voraussetzung | Stand |
|---|---|
| Soft-Dependency `SerpsMods_Serp` | vorhanden |
| Wirksamer Host-Aktivierungsschalter (`EnableMod`, `[SyncHostOnly]`) | vorhanden |
| `NetworkMode` = 1 (gameplaybeeinflussend) | vorhanden |
| APIShared-Mindestversion (0.18.0) ≤ Release-Stand 0.20.0 | passt |
| Testwerkzeuge im Mehrspieler gesperrt (`ToolSessionPolicy.CanMutate` verlangt `!multiplayer`) | vorhanden |
| Alle Quellen committet | **offen**: 4 neue Quelldateien und 1 Test sind noch nicht getrackt, 22 Dateien geändert. Der Pack-Build arbeitet aus einem `main`-Worktree und sieht nur Committetes. |
| Version und Changelog releasefähig | **offen**: 0.1.0, Changelog „Development version“ |
| Aufnahme in `WorkshopConfiguration.ps1` → `Mods` | **offen** |
| Übernahme früherer BugfixesAndQoL-Werte | **offen (Entscheidung)**: Spectator liest die alten BugfixesAndQoL-Werte nicht; alle Optionen starten mit `true`. Wer die Funktionen in BugfixesAndQoL abgeschaltet hatte, hat sie nach dem Update wieder an. |
| `EnableTestTools` standardmäßig `true` | **offen (Entscheidung)**: Im veröffentlichten Pack sähen Einzelspieler die lokale KI-Test-Werkzeugleiste standardmäßig. Für einen öffentlichen Release eher `false` erwägen. |

**Weitere Release-Schritte:**
- Den BugfixesAndQoL-Changelog um einen Hinweis auf die Auslagerung ergänzen, damit die geänderte Settings-Seite erklärt ist.
- Den Host-Changelog um den Spectator-Mod ergänzen (siehe N1).

**Bewertung:** Den Spectator-Mod ins Pack aufzunehmen ist der richtige Weg. Ein Rückbau in BugfixesAndQoL wird nicht empfohlen, weil sonst doppelte Hooks mit dem Spectator-Mod drohen.

---

## Hoch

### H1 – ExtendedData: „Mission speichern“ im Trail Maker wird vollständig abgebrochen

**Ort:** `ExtendedData/src/TrailMissionSettingsCoordinator.cs`, `SaveCustomTrailMapHook` (neu seit Commit 1057).

**Kontext (präzisiert):** Der Hook umschließt `EditorDirector.SaveCustomTrailMap`. Den ruft Vanilla ausschließlich aus `FRONT_ManageTrail.SaveMission` auf, also über den Button „Mission speichern“ im **Trail Maker**. Vanilla lädt dort die gewählte Karte und erzeugt aus ihr und den gewählten Lords die Datei `Trail_Mission_N.trail`. Die erste Fassung dieses Berichts sprach von verlorener Editorarbeit; das war zu weit gefasst. Verloren geht der Speichervorgang dieses Mission-Slots, nicht eine bearbeitete Karte.

**Befund:** Vor dem Vanilla-Aufruf läuft jetzt
`TrailLordRequirementSource.Resolve(trailPath, restartInfo, …, allowEmbeddedValuesOnly: false)`, bei aktivierter Medienoption zusätzlich `SelectMedia(…, true)` und `Stage(…)`. Das geschieht in demselben `try`-Block wie die Erfassung der Mod-Settings. Jede Ausnahme führt zu der Meldung „ModSettings save failed“ und `return`, ohne dass `saveCustomTrailMapOriginal` aufgerufen wird.

**Im Kontext bestätigte Auslöser** (nur Slots mit Custom-AIC-Lord; eingebaute Lords werden in `CustomLordPlayerIds` übersprungen):
- `TrailLordPackageRuntime.CaptureSlot` scheitert, und `Resolve` macht daraus wegen `allowEmbeddedValuesOnly: false` eine `TrailLordAuthoringUnavailableException`. Das passiert, wenn
  - die Autorquelle nicht eindeutig auflösbar ist (`ResolveAuthorSource`), oder
  - die **neue** Präsentationserfassung `TrailLordMediaStore.Capture` scheitert: eine `lordmeta.json` eines Fremd-Lords ist kein gültiges JSON-Objekt (`LordMetadataDocument.Parse` → `InvalidDataException`) oder kein gültiges UTF-8 (`DecoderFallbackException`). Diese Fehlerquelle kommt erst mit dem neuen Präsentations-Feature hinzu, oder
  - die Fixes-Erfassung scheitert.
- Ein vorhandenes `.lordrequirements.json` neben der Mission ist beschädigt (`ReadUnbound` wirft), oder eingebettete Medien eines gespeicherten Slots fehlen (`BindAndVerify` wirft).
- Bei aktivierter Medienoption fehlt eine referenzierte Mediendatei („Cannot embed unresolved media …“).

**Folge:** Die Mission lässt sich nicht speichern, solange der Zustand besteht, und die Meldung nennt fälschlich die Mod-Settings. Abhilfe hat der Nutzer nur, wenn er die ExtendedData-Editorfunktionen abschaltet. Der Export-Pfad behandelt denselben Fall dagegen mit einer Rückfrage (`OfferEmbeddedValuesOnlyExport`).

**Fix (nach erneuter Bewertung):**
1. Die Lord-Erfassung vom Settings-Capture trennen. Das Vanilla-Speichern muss auch dann stattfinden, wenn die Lord-Erfassung scheitert.
2. In `CaptureSlot` einen Fehler von `TrailLordMediaStore.Capture` nicht als Fehler des ganzen Slots behandeln. Stattdessen auf `new LordPresentationData { DisplayName = FallbackName(name) }` zurückfallen, so wie `Resolve` es für gespeicherte Slots schon tut, und eine Warnung loggen. Die Präsentation ist optional, Gameplay-Werte hängen nicht davon ab.
3. Bei verbleibender `TrailLordAuthoringUnavailableException` mit `allowEmbeddedValuesOnly: true` erneut auflösen, also mit derselben Semantik wie nach bestätigtem Export. Der Export fragt ohnehin noch einmal nach.
4. Scheitert auch das (beschädigtes Sidecar, fehlende Medien), nach dem Vanilla-Speichern das veraltete `.lordrequirements.json` entfernen und eine eigene, lokalisierte Meldung zeigen. Ein Sidecar mit altem `MissionDigest` lässt sonst das spätere gebundene Lesen scheitern (`TrailLordRequirements.Read`: „Lord requirements do not match this mission“). Das wirkt sich auch auf M1 aus.
5. Den Trail-Regressionstest ergänzen um die Fälle „ungültige `lordmeta.json`“ und „Autorquelle nicht auflösbar“. Erwartung in beiden Fällen: Die Mission wird gespeichert.

**Bewertung:** Schritt 2 beseitigt die neue Fehlerquelle ohne Risiko für Gameplay-Daten. Die Schritte 1, 3 und 4 stellen das Vanilla-Speichern als oberste Garantie wieder her. Ob ein Speichern mit „embedded-only“ ohne Rückfrage gewollt ist oder eine Bestätigung wie beim Export erscheinen soll, ist eine Produktentscheidung. Technisch funktionieren beide Varianten.

### H2 – ExtraFeatures: Fehler in optionaler Migration deaktiviert die ganze Mod

**Ort:** `ExtraFeatures/src/ExtraFeaturesPlugin.cs` (`InitializeLibraryRuntime`) und `ExtraFeaturesViewModel.EnemyProximity.cs` (`CaptureEnemyProximityStartup`).

**Befund:** `Settings.CaptureEnemyProximityStartup(...)` liest vorab die persönliche `settings.msgpack` beziehungsweise die alte `LobbyModSettings/*.msgpack`, um die alte Gegner-Nähe-Einstellung zu migrieren. Die Pfade stimmen mit APIShareds `PersonalSettingsPaths` überein. Der Aufruf steht im selben `try` wie `LobbyModSettingsPresetRegistration.Register(...)`, und dessen `catch` endet mit `return`. Ausnahmen entstehen durch:
- eine beschädigte Datei (`MessagePackSerializationException`),
- fehlende Rechte oder eine gesperrte Datei (`UnauthorizedAccessException`/`IOException`; `EnemyProximityFileExists` fängt nur `FileNotFound`/`DirectoryNotFound`),
- ein unerwartetes verschachteltes Format.

**Kontextprüfung:** Hinter dem `return` liegen in `InitializeLibraryRuntime` unter anderem `runtime.InitializeNative(...)` (alle nativen Hooks), `runtime.ApplySettings()`, der Marktpreis-Hook und die Button-Bindings. Für genau diesen Fall hat APIShared eine Behandlung (`LobbyPresetController.Persistence`: `BackupCorruptFile`, danach weiter mit Defaults), die hier aber nie erreicht wird. Der Fehler wiederholt sich deshalb bei jedem Start.

**Folge:** ExtraFeatures ist auf diesem Rechner vollständig inaktiv. Im Mehrspieler (`NetworkMode = 1`) läuft ein Peer ohne die gameplaybeeinflussenden Hooks; das ist ein Desync-Risiko.

**Fix:** `CaptureEnemyProximityStartup` in ein eigenes `try/catch` vor der Registrierung legen. Bei einem Fehler eine Warnung mit Zeitstempel loggen, `startupEnemyProximity = null` setzen und mit der Registrierung fortfahren. Zusätzlich einen Test mit beschädigter und mit gesperrter Datei: Die Registrierung muss trotzdem erfolgen.

**Bewertung:** minimal und risikoarm. Wenn die Migration ausfällt, wird nur ein alter Radius nicht übernommen, und der Wert fällt auf „Vanilla“ (-1) zurück. Das ist harmlos.

---

## Mittel

### M1 – ExtendedData: Trail-Backup-Hook ohne Fehlerkapselung

**Ort:** `TrailMissionSettingsCoordinator.BackupHook` (umschließt `FRONT_ManageTrail.DoBackup`).

**Befund:** `CopyLordPresentationSources(source, destination)` ist neu und steht als einziger Schritt **nicht** in `TryFileOperation`. Die Methode liest jede `*.trail` des Quellordners mit dem gebundenen `TrailLordRequirements.Read`. Das wirft bei einem Sidecar, dessen Digest nicht mehr zur Mission passt, etwa nach einem Speichern bei abgeschalteten Editorfunktionen oder nach H1/4, außerdem bei fehlenden eingebetteten Medien. In den Import- und Upload-Pfaden ist derselbe Aufruf abgesichert.

**Kontextprüfung (Folge korrigiert):** Vanilla ruft `DoBackup` innerhalb bestätigter Aktionen auf, und zwar jeweils **vor** dem eigentlichen Vorgang:
- Import: Backup → `ClearMakerFolder` → `ImportTrailMissions`
- Leeren: Backup → `ClearMakerFolder`
- Export in einen vorhandenen Ordner: Backup → `ExportTrailMissions`

Die Ausnahme beendet den Bestätigungs-Delegate. Die vom Nutzer bestätigte Aktion findet also **still nicht statt**, das Backup ist unvollständig (Coop-Paket fehlt), und es gibt keine Meldung. Datenverlust entsteht dadurch nicht.

**Fix:** `TryFileOperation("back up Trail Lord media", () => CopyLordPresentationSources(source, destination));`, zusätzlich eine sichtbare Meldung analog zu `ReportSkippedSidecars`, wenn Lord-Medien nicht gesichert werden konnten.

**Bewertung (Datensicherheit geprüft):** Nach dem Abfangen laufen Leeren und Import weiter. Das ist sicher, denn Vanillas `ClearMakerFolder` löscht nur `*.trail`, und der ExtendedData-`ClearMakerHook` löscht nur die Settings- und Lord-Requirements-Sidecars. Der `LordMedia`-Speicher des Trail Makers bleibt erhalten, ein unvollständiges Medien-Backup führt also nicht zu Verlust. Die Meldung ist nötig, damit der Nutzer vom Teilausfall erfährt.

### M2 – ExtendedData: Lord-Präsentation verhindert `fixes.Apply` beim Missionsstart

**Ort:** `LordDataSyncCoordinator.Presentation.cs` (`RefreshPresentation`/`ApplyPresentation`) mit Aufrufern in `LordDataSyncCoordinator.cs`.

**Befund:** `RefreshPresentation` führt `ApplyPresentation` auf dem Main-Thread **synchron** aus. Dort passieren Datei-IO und Hashing (`TrailLordMediaStore.Stage`), Schreiben nach `BepInEx/cache`, `RegisterAssetMod`, `File.ReadAllText(lordmeta.json)` und ggf. eine Suche in den Workshop-Wurzeln. Intern gefangen wird nur `InvalidDataException` um `BindAndVerify`. `Stage` selbst und die übrigen IO-Schritte sind nicht abgesichert.

**Kontextprüfung (vom Verdacht zur Bestätigung):** Laut APIShared-Katalog werden Missions-Lifecycle-Benachrichtigungen auf dem Publisher-Thread zugestellt; die verwaltete Initialisierung läuft auf dem Unity-Thread. Damit läuft `ApplyPresentation` in diesen Pfaden synchron:
- **Save-Initialisierung:** `RefreshPresentation(active)` steht unmittelbar vor `fixes.Apply(active)`, und zwar **ohne** `try`. Eine Ausnahme verhindert die Anwendung der Fixes-Präferenzen für den geladenen Spielstand.
- **MP-Map-Initialisierung:** Hier liegt der Aufruf im selben `try` vor `fixes.Apply`. Der `catch` loggt „stage=fixes-apply“, was die Ursache falsch zuordnet, und wirft erneut. Scheitert das nur auf einem Peer, laufen die KI-Präferenzen auseinander, mit Desync-Risiko.
- **`Publish`:** Der Aufruf steht nach `active = snapshot` und vor `SetNetworkSnapshot`. **`Invalidate`:** Er steht vor `fixes.Restore()`.

Realistische Auslöser sind Trails mit eingebetteten Lord-Medien (neues Feature), deren Quell- oder Cache-Dateien gesperrt oder verändert sind („Lord media changed during export“), sowie IO-Fehler beim Lesen der `lordmeta.json` installierter Lords. Außerdem staged `ApplyPresentation` die Medien bei jedem Aufruf neu in den Cache, auch wenn sie dort schon verifiziert liegen. Das erzeugt unnötige Schreibvorgänge auf Dateien, die als Asset-Mod bereits registriert sind.

**Fix:** `ApplyPresentation` vollständig kapseln (`catch (Exception)`: gedrosselt loggen, `presentationRegistration.SetEntries(empty)`, `presentationDigest = null`). Ein Scheitern von `TryEnqueue` in `RefreshPresentation` nur loggen statt werfen. Zusätzlich `Stage` in den Cache überspringen, wenn `cache/LordMedia/<MediaId>` bereits per `BindAndVerify` bestätigt ist.

**Bewertung:** Die Präsentation ist ausdrücklich optional. Sie fail-open zu machen, trennt Darstellung von den Gameplay-Pfaden, wie es die Projektregeln für getrennte Fehlerpfade verlangen. Den Aufruf nur hinter `fixes.Apply` zu verschieben, würde `Publish` und `Invalidate` nicht schützen; die Kapselung ist deshalb der eigentliche Fix. Der Cache-Skip senkt zusätzlich IO beim Missionsstart.

### M3 – CastlePlanner: APIShared-Mindestversion zu niedrig

**Befund:** CastlePlanner deklariert APIShared ≥ **0.11.1** (`BepInDependency` und `info.json`). Der neue Code ruft aber in `InitializeSpectatorBlueprints()` beim Erzeugen des Blueprint-Controllers `APIShared.PlayerPerspectiveAPI.SubscribeChanges` auf, das erst mit APIShared **0.14.0** eingeführt wurde (Commit `0a97e72`). `HudExtras`-`order` gibt es seit 0.7.1; das ist unkritisch.

**Folge:** Im Pack ohne Wirkung, weil andere Mods 0.20.0 erzwingen. Bei einem Einzelrelease (GitHub/Nexus) mit APIShared 0.11 bis 0.13 schlägt die Blueprint-Initialisierung mit `MissingMethodException` fehl.

**Fix:** Mindestversion in `BepInDependency` und `info.json` zusammen mit B2 auf 0.14.0 setzen.

**Bewertung:** risikolos. `Test-DependencyMetadata.ps1` prüft die Übereinstimmung.

---

## Niedrig

### N1 – SerpsModsHost: Changelog 1.0.33 veraltet
Der Eintrag nennt „Require APIShared 0.16.4 … include ExtendedData 1.0.21“. Tatsächlich gelten APIShared ≥ 0.20.0 (siehe B1) und ExtendedData 1.0.22. Außerdem fehlen der neue Spectator-Mod bzw. die Auslagerung (B3), die neuen Kill-Reward-Optionen und die zivile/militärische Gegner-Nähe (ExtraFeatures), der Lord-Teleport-Fix und die native Assassin-Suche (BugfixesAndQoL) sowie die KI-Plan-Ansicht (CastlePlanner). Der Packer setzt nur die `MinimumVersion`, nicht den Text.
**Fix:** den Eintrag vor dem Pack-Build korrigieren und ergänzen. **Bewertung:** reine Dokumentation, kein Risiko.

### N2 – CastlePlanner: unnötige Arbeit in `RefreshHud`
`RefreshHud` erzeugt bei jedem Aufruf einen neuen `SolidColorBrush` und löst `PropertyChanged(SpectatorCastleColour)` aus, auch außerhalb des Zuschauermodus. In der Kontextprüfung zeigte sich: `RefreshHud` ist ereignisgesteuert und läuft pro Frame höchstens während des schrittweisen Tiefenatlas-Ladens. Die Wirkung ist also gering.
**Fix (optional):** Brush nur bei geändertem Spieler bzw. geänderter Farbe neu setzen und nur dann benachrichtigen.

---

## Prüfempfehlung (kein Fehlerbefund)

### P1 – Native Assassin-Suche im Mehrspieler
BugfixesAndQoL liefert `BfqolAssassinSearch.dll` mit. `build.bat` baut und kopiert sie mit Hash-Nachweis, und der Packer überspringt Nicht-.NET-DLLs bei der Plugin-Erkennung. Die Simulation nutzt die DLL, wenn sie geladen werden kann, sonst die verwaltete Bucket-Suche. Desync-frei ist das nur, wenn beide Wege **exakt** dasselbe Ergebnis liefern. Ein Peer, bei dem die DLL fehlt (z. B. Virenscanner-Quarantäne), schreibt lediglich eine Warnung.
**Empfehlung vor dem Release:** einen DEVDIAG-Lauf mit vielen Assassinen-Befehlen auswerten (`bfqol.assassin.native.mismatch` muss 0 sein). Der Vergleich ist im Code vorhanden (`AssassinPathfindingRuntime.DevDiag.cs`).

---

## Geprüfte und nicht bestätigte Verdachtsfälle

Diese Stellen wurden gezielt nachgeprüft und sind **in Ordnung**:

- **ExtendedData `LordPresentationData.Decode` (alter Fund N2):** Bei fehlendem Feld wirft es `KeyNotFoundException` statt `InvalidDataException`. Alle Aufrufer (Netzwerk-Snapshot, Save-Datensatz, Sidecar-Lesen) fangen aber `Exception` allgemein ab. Das ist nur eine Stilfrage, kein Fehler.
- **ExtendedData `RejectLinks` (alter Fund N3):** Die Prüfung schließt zwar die Wurzel selbst ein, aber die Wurzeln sind Missions-, Workshop-Item- und Cache-Ordner. Das sind praktisch nie selbst Reparse-Points; eine verlegte Steam-Bibliothek liegt oberhalb der Grenze und wird nicht geprüft. Kein praktisch relevanter Fehler.
- **BugfixesAndQoL `TribeFloodMembership`:** Der Vanilla-Simulationscode wird über eine nur lokale Einstellung ersetzt. Gegen die Disassembly von `0x124740` und `0x119F90` geprüft: dieselbe Reihenfolge der Mitglieder, dieselbe Bitset-Iteration, Slot 0 für überzählige Mitglieder und andere Tribe-Arten, derselbe vorzeitige Abbruch, identische Basisadressen (`0x67E8400`, Stempel `0x52C2550 = 0x5225B10 + 0x139480/2`). Das Ergebnis ist identisch, also kein Desync-Risiko.
- **BugfixesAndQoL `LordTeleportGuard`:** Stub mit 4 Pushes, Rücksprungadresse bei `rsp+32`, alle Scratch-Register und Flags wiederhergestellt, Feldoffsets passen zu `RecordOrigin 0x65C`. Hash-gebunden, und der Rettungsaufruf für ungültige Standkacheln bleibt erhalten.
- **ExtraFeatures `EnemyProximityQuery`:** Prolog mit 4 Pushes ohne `sub rsp`, das 7. Argument liegt also bei `rsp+0x58`. An `0xEE748` sind RCX und RDX tot, das Typwort entspricht `0xEE791`, der militärische Radius wird von Vanilla selbst quadriert (`imul edi,edi`), der zivile wird bereits quadriert übergeben. Korrekt.
- **ExtraFeatures Kill-Reward:** Die Standardformel entspricht Vanilla (`0x15C49B`–`0x15C4D0`: mindestens 500, höchstens 100000, 25 % mit Abschneiden), Goods-Quelle und -Ziel (Abstand `0x64`) und die Benachrichtigung (`+0x3C`, Vanilla schreibt `r15d = 2`, gesetzt bei `0x15C2E8`) ebenfalls. Der Chunk-Loop der Goldmeldung hält die Wortgrenzen von max. 30000 ein, und R12 wird nach `0x15CA74` sofort überschrieben.
- **ExtraFeatures `new`-Überdeckungen der Snapshot-Methoden:** Die Reimplementierung von `IModSettingsWorkingCopyEndpoint` bildet nach C#-Spezifikation auch das Basis-Interface `IModSettingsPresetEndpoint` neu ab. `System_EnterMissionPreset`/`System_ExitMissionPreset` werden von APIShared erreicht.
- **ExtraFeatures und RandomEvents, APIShared-Mindestversionen (0.16.1 bzw. 0.16.0):** Alle genutzten APIs sind seit 0.7.1 vorhanden.
- **CastlePlanner `EngineInterface.threadLock` auf dem Main-Thread:** Vanilla nimmt diesen Lock selbst vielfach auf dem Main-Thread. Veraltete `restartSkirmishMapInfo` im Mehrspieler sind ausgeschlossen, weil Vanilla den Wert beim Öffnen des MP-Setups leert.
- **Formation-Protokoll:** Die Seitenverhältnis-Absicht wird als ganzzahliges Paar gesendet, Zeilen werden nach der Zulassung deterministisch berechnet, die Protokollversion ist auf 12 angehoben.
- **RandomEvents:** Die neue Banditen-Diagnose ist korrekt gedrosselt und ohne zusätzliche Weltabfragen.
- **XAML-Patches:** Alle geänderten Patches haben genau ein Wurzelelement pro `<Content>`.
- **Locales:** In allen geänderten Mods ist die Schlüsselparität über alle Sprachen gegeben.
- **Modoptionen-UI:** Alle neuen interaktiven Elemente haben Tooltips mit `ShowDuration="60000"`.
- **Versionen:** BugfixesAndQoL (1.0.207), ExtraFeatures (1.0.126), RandomEvents (1.0.56) und ExtendedData (1.0.22) sind angehoben und haben Changelog-Einträge.

---

## Empfohlene Reihenfolge

1. **H1** und **H2** beheben (beide klein und lokal begrenzt) und die zugehörigen Tests ergänzen.
2. **M1** und **M2** beheben.
3. CastlePlanner auf 0.8.48 anheben, mit Changelog und APIShared-Mindestversion (**B2** und **M3**).
4. Den Spectator-Mod fertigstellen und die offenen Punkte aus B3 abarbeiten: committen, Version und Changelog, `Mods`-Liste, Entscheidungen zu Übernahme alter Werte und Default der Testwerkzeuge.
5. Changelogs korrigieren: Host (**N1**) und BugfixesAndQoL (Hinweis auf die Auslagerung).
6. APIShared 0.20.0 nach `main` bringen und veröffentlichen (**B1**), danach `CreateSteamModPack.bat` ausführen.
7. Vor dem Upload den DEVDIAG-Vergleich der Assassin-Suche (**P1**) und einen echten MP-Test mit Host und Client durchführen, mit Spectator-Mod.
