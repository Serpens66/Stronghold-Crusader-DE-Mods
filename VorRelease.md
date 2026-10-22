# Vor-Release-Prüfung develop → main

Momentaufnahme vom 2026-10-22. Am selben Tag nach Rückmeldung des Nutzers zu B1, B2 und A1 ergänzt, Stand der offenen Punkte 16:30 (siehe „Offen auf einen Blick“). Geprüft wurden:

- Mod-Repo, Branch `dev/Serpens66` (= develop, HEAD `ab291bbfb`): 47 Commits vor `origin/main` (Merge-Basis `a23aa1b32`).
- APIShared, Branch `dev/Serpens66` (= `origin/develop`): 106 Commits vor `main`. `main` steht auf 0.9.2, develop auf 0.12.5.
- Umfang (Nutzervorgabe): die Mods des Steam-Modpacks laut `Shared/Tools/Steam/WorkshopConfiguration.ps1`, also BugfixesAndQoL, BuildingCosts, BuildingLimit, ExtraFeatures, RandomEvents, CastlePlanner, StartConditions, UnitCosts, UnitLimit und ExtendedData, außerdem SerpsModsHost, die quellverlinkten `Shared/`-Dateien und APIShared.

Vorgehen: Diffs gegen `main`. Generierte Dateien (dll/pdb, Build-Ausgaben) und reine Diagnose-/Formatänderungen sind herausgefiltert, der Rest wurde gelesen. Jeder Befund wurde im umgebenden Code gegengeprüft, wo nötig auch gegen die echte `Assembly-CSharp.dll`, gegen `origin/main` und gegen den lokalen Fixes-Klon. Bei der Prüfung selbst wurde am Code nichts geändert. Die danach beauftragten Umsetzungen stehen bei den Befunden.

Schweregrade:

- **Hoch:** blockiert den Release oder kann Spielstände bzw. MP-Partien sichtbar beschädigen.
- **Mittel:** echtes Risiko oder unbeabsichtigte Verhaltensänderung, vor dem Release klären.
- **Niedrig:** Schönheitsfehler, Lognoise oder nur in Randfällen relevant.
- **Info:** kein Fehler, nur ein Hinweis für Test oder Changelog.

---

## Offen auf einen Blick

Stand 2026-10-22, 16:30. Ermittelt über `git status` und `git log` in beiden Repos sowie die Build-Records.

**Erledigt:**

- B1 umgesetzt und vom Nutzer committet (Mod-Repo `a511375df` „999“).
- B2 umgesetzt und committet (`d7bf93d1e` „1000“).
- A1 umgesetzt und committet (APIShared `2f2f84b` „41 fix modsettings“).
- APIShared, BugfixesAndQoL und MoatMove per `build.bat` gebaut und installiert.
- MoatMove-Testsuite repariert. T5 (APIShared-Auswahl in der MoatMove-`build.bat`) umgesetzt.
- O3/E1: `ExtendedData.Core` auf 1.0.16 angeglichen (nicht committet).
- O6/R2/E5: Versionshinweise in den Changelogs von APIShared 0.12.5, SerpsModsHost 1.0.30 und ExtendedData 1.0.16 sowie in der Modpack-Beschreibung (nicht committet).
- S1: Info-Logs werden nicht mehr unterdrückt, die Info-Stellen sind auditiert (nicht committet, nicht gebaut; siehe §3).
- Empfohlene Bereinigung ohne offene Entscheidung (16:30, nicht committet): O2, S2, B5, B8, H1, T1, T2, A9-Kommentar umgesetzt; R3, C3 und T3 geprüft ohne Handlungsbedarf; tote BFQoL-Member entfernt (§5 „Bereinigung 16:30“).
- A3, A6, A7 umgesetzt (nicht committet). APIShared ist per `build.bat /nopause /noinstall` gebaut und vollständig getestet, aber **nicht installiert**. BFQoL und ExtendedData sind nur kompiliert und geprüft (siehe §2).

**Offen – vor dem Release nötig:**

| # | Was | Art | Details |
|---|---|---|---|
| O1 | Commit der offenen Änderungen. Mod-Repo: MoatMove-Testanpassungen, S1 und S2 (Shared, BFQoL), O2, O3, O6, H1, T1, T2, A3 (Shared-Planfabrik, ExtendedData samt 21 Locales, BFQoL, Mindestversion APIShared 0.12.5 in BFQoL und ExtendedData), B5, B8, A9-Kommentar, entfernte tote BFQoL-Member, diese Datei. APIShared: O6, A3, A6, A7 samt Tests, Doku und API-Baseline | Commit (Nutzer) | §2–§5 |
| O4 | Spieltests §6, bisher keiner durchgeführt. Vorrangig: Punkt 6 (B1 mit und ohne MoatMove samt Lag-Vergleich), Punkt 2 (A1 im Lobby-Preset), Punkt 7 (B2 mit < 8 GB und ≥ 8 GB VRAM) | Test (Nutzer) | §6 |
| O5 | R1: APIShared develop → main mergen, Tag `APIShared/v0.12.5` setzen und veröffentlichen, **danach** das Modpack gegen das veröffentlichte SDK bauen | Release (Nutzer) | §1 |
| O6b | Den Versionshinweis zusätzlich in die Steam-Workshop-Seiten von APIShared und Modpack übernehmen. Diese Texte stehen nicht im Repo; der Upload setzt nur Titel und Release-Tag | Release (Nutzer) | §1 |
| O8 | APIShared installieren und BFQoL sowie ExtendedData per `build.bat` bauen und installieren (S1, A3, A6, A7). Weil BFQoL und ExtendedData jetzt APIShared 0.12.5 voraussetzen, APIShared zuerst. Danach im Log prüfen: Info-Zeilen nur einmalig pro Start oder bei Nutzeraktionen; mit Fixes erscheint `Fixes wheat CORRECTED: derived from the actual code` | Build und Test | §2, §3 |
| O9 | Spieltests der neuen Fixes: Coop-Paketmission im MP mit mehreren Burgen (Host unverändert, Host nach der ersten Burg geändert, BFQoL-Synchronisierung aus), Skirmish-Neustart im Editor bzw. „Ja“ ohne Session (A6), Spielstart mit Fixes (A7) | Test (Nutzer) | §2 |
| O7 | Vor den Spieltests: im Spiel liegt zusätzlich `BepInEx/plugins/BugfixesAndQoL_Serp_dev` (22.10. 00:04). Sicherstellen, dass nur eine BFQoL-Variante aktiv ist | Prüfung | §5 |

**Offen – Entscheidung oder Bestätigung des Nutzers:**

- **E2:** War die README-Änderung in ExtendedData autorisiert? (§4.2)
- **X2:** Sollen ExtraFeatures-Guard-Fenster während einer Pause wirklich nicht ablaufen? (§4.3)
- **A8:** Changelog 0.10.0–0.12.5 zusammenfassen oder nur die Zuordnung korrigieren? (§2)
- **T4:** Bleibt `TemporaryPackedRouteInspection.cs` (DEVDIAG) bestehen? (§5)
- **A9:** `ModSettingsStartLog`-Volumen bestätigen. Geschützte Arbeitskopie `.work/a/api/001` mit develop abgleichen und den Schutzvermerk in `.work/INDEX.json` aktualisieren (§2).

**Offen – empfohlene kleine Fixes, nicht release-blockierend (noch nicht umgesetzt):**

| Mod | Punkte |
|---|---|
| ExtendedData | E4 TrailMaker-Cache begrenzen (Grenze und Löschregel für Nutzerdaten sind eine Entscheidung) |
| StartConditions | SC1 modübergreifende Testabhängigkeit (verschieben oder optional machen ist eine Entscheidung) |
| Nach dem Release-Build | R3 die committeten `BepInEx/plugins/*/info.json` mit der Quelle abgleichen |

Reine Testpunkte ohne Codebefund (A2, A4, A5, B3, B4, B6, B7, C1, C2, E3, X1) sind im Testplan §6 abgedeckt.

---

## 0. Kurzfassung

| # | Schwere | Thema | Empfehlung |
|---|---|---|---|
| R1 | **Hoch** | Das Modpack verlangt APIShared ≥ 0.12.3. Veröffentlicht ist aber nur 0.9.2 (`main`). | Zuerst APIShared develop → main mergen, testen und 0.12.5 veröffentlichen, danach das Modpack. |
| R2 | **Hoch** | Alle Spieler einer MP-Partie brauchen dieselbe APIShared-Version (Save-Readiness-Protokoll v2). | Release von APIShared und Modpack zeitgleich ankündigen, im Changelog deutlich vermerken. |
| R3 | Mittel | MP-, Coop-, Restart- und Upgrade-Pfade sind neu bzw. stark umgebaut und noch nicht im Spiel getestet. | Testplan in §6 vor dem Merge abarbeiten. |
| B1 | **Hoch** (Performance) | BugfixesAndQoL: Ohne MoatMove laufen noch vier Pfade mit „Bewegung durch eigenen Graben“ (u. a. Flutfüllungen über die ganze Karte). Laut Nutzerentscheidung soll das ohne MoatMove gar nicht laufen. | **Umgesetzt und committet** (Gates und Verschiebung nach MoatMove, siehe §4.1 B1). Gebaut und installiert. Offen: Spieltest, Changelog (O2, O4). |
| B2 | Mittel | BugfixesAndQoL: Sprite-Sheet-Merge ist standardmäßig an und kostet etwa +1,6 GB Texturspeicher. | **Umgesetzt und committet**: Modus Automatisch (Default, ab 8 GB VRAM)/An/Aus (siehe §4.1 B2). Gebaut und installiert. Offen: Spieltest (O4). |
| E1 | Mittel | ExtendedData: `ExtendedData.Core.csproj` hatte AssemblyVersion 1.0.15, der Mod ist 1.0.16. | **Umgesetzt** (nicht committet). |
| A1 | **Hoch** | APIShared: Ein werfender Setter bricht das Anwenden eines Preset-Snapshots mitten drin ab. Die Werte davor sind dann schon gesetzt, die danach nicht. Beim Verlassen einer Mission bleibt der Mod bei einem dauerhaft werfenden Setter für immer im Missionskontext. Laut Nutzer nicht beabsichtigt. | **Umgesetzt und committet** (pro Setting isoliert, Default-Fallback, höchstens 3 Aufräumversuche, siehe §2 A1). Gebaut und installiert. Offen: Spieltest (O4). |

Alles Weitere steht in den folgenden Abschnitten.

---

## 1. Release-Reihenfolge und Abhängigkeiten

### R1 – APIShared muss vor dem Modpack veröffentlicht werden (Hoch)

- Befund: Der Modpack-Release nutzt den **veröffentlichten APIShared-main-Release** (ApiSharedSdkPolicy). Auf `main` liegt 0.9.2, die Tags reichen nur bis `APIShared/v0.9.2`. Die Mods verlangen aber:
  - BugfixesAndQoL, ExtendedData und SerpsModsHost: `MinimumVersion 0.12.3`
  - ExtraFeatures: `MinimumVersion 0.12.0`

  Dazu kommen APIs, die es nur auf develop gibt, z. B. `HostSettingsOpacity` (seit 0.12.1).
- Folge: Wird das Modpack zuerst veröffentlicht, lädt der Extender die Mods nicht oder nur mit Abhängigkeitsfehler. Der Build mit SDK-Policy schlägt ebenfalls fehl.
- Fix:
  1. APIShared develop → main mergen.
  2. Tag `APIShared/v0.12.5` setzen, GitHub-Release und Workshop-Update.
  3. Erst danach den Modpack-Build gegen das veröffentlichte SDK laufen lassen und veröffentlichen.

### R2 – Gleiche APIShared-Version für alle MP-Teilnehmer (Hoch, Kommunikation)

- Befund: Der SavegameLoadCoordinator verwendet Readiness-Protokoll v2 mit 60 s Timeout. Peers mit älterem APIShared beantworten das nicht, das Laden blockiert bzw. läuft in den Timeout.
- Fix: Kein Codefix nötig. In Workshop-Beschreibung und Changelog deutlich schreiben, dass alle Spieler APIShared und Modpack aktualisieren müssen. Optional: In der Lobby einen klaren Hinweis zeigen, wenn ein Peer eine ältere Protokollversion meldet.
- **Korrektur 2026-10-22 (Code geprüft, `SavegameLoadCoordinator.RejectProtocol`):** Ein Peer mit anderem Readiness-Protokoll läuft nicht in den Timeout. Das Laden wird sofort mit einer Meldung blockiert, die beide Protokollversionen nennt. Einem Protokoll-1-Peer wird zusätzlich ein Abbruch gesendet.
- **UMGESETZT (Changelog, nicht committet):** Je ein Eintrag in `APIShared/info.json` (0.12.5) und `SerpsModsHost/info.json` (1.0.30). Außerdem steht ein zweisprachiger Satz in der Modpack-`Description`. `Set-HostVersion` erkennt die Beschreibung weiterhin am Präfix „APIShared must“. Die Steam-Seiten selbst muss der Nutzer anpassen (O6b).

### R3 – Übrige Release-Hinweise (Info)

- Die getrackten Build-Ausgaben `*/BepInEx/plugins/*/info.json` sind teils veraltet: ExtraFeatures 1.0.114, RandomEvents 1.0.52, SerpsModsHost 1.0.28 mit APIShared-Minimum 0.9.2. Der Build überschreibt sie. Nach dem Release-Build prüfen, dass die committeten Ausgaben zur Quelle passen.
- Quellversionen sind konsistent (PluginVersion, info.json, AssemblyInfo): BFQoL 1.0.195, BuildingCosts 1.0.115, BuildingLimit 1.0.33, CastlePlanner 0.8.44, ExtendedData 1.0.16 (Ausnahme siehe E1), ExtraFeatures 1.0.116, RandomEvents 1.0.53, SerpsModsHost/serps-modpack 1.0.30, StartConditions 1.0.33, UnitCosts 1.0.37, UnitLimit 1.0.107.
- RandomEvents deklariert APIShared-Minimum 0.7.0 (Niedrig). Prüfen, ob die quellverlinkten `Shared/`-Dateien inzwischen neuere APIShared-APIs nutzen. Falls ja, das Minimum anheben. `Test-DependencyMetadata` war PASS, deckt das aber eventuell nicht ab. **Geprüft 2026-10-22:** Alle 76 APIShared-Typ- und Memberreferenzen des gebauten `RandomEvents.dll` (Stand 18.10.) existieren schon im Quellstand `APIShared/v0.7.1`, dem ältesten Tag. Kein Handlungsbedarf. Nach einem neuen RandomEvents-Build ggf. erneut prüfen.

---

## 2. APIShared (develop 0.12.5)

### A1 – LobbyPresetController: Ein werfender Setter bricht den Snapshot halb angewendet ab (Hoch, laut Nutzer nicht beabsichtigt) – UMGESETZT

*Umsetzungsstand 2026-10-22 (APIShared `dev/Serpens66`, vom Nutzer committet als `2f2f84b`):*

- **Isolierte Anwendung.** `ApplyPreparedValues` (`src/ModSettings/Presets/LobbyPresetController.Snapshots.cs`) setzt jede reflektierte Property einzeln.
  - Bei einer Exception wird einmal der Code-Default versucht. Scheitert auch der, gilt die Property als `Failed`.
  - Commit der Auswahl, `OnSettingsSnapshotApplied` und die Publikation laufen immer.
  - Fallbacks werden als Warning, endgültige Fehler als Error über `DebugLogHelper` bzw. RuntimeHealth gemeldet.
  - Dynamische Provider und Backends sind unverändert (Komplettvalidierung).
- **Ehrlicher Stand.**
  - Ein Default-Fallback wird in den gespeicherten Snapshot übernommen.
  - Ein veröffentlichtes Preset gilt dann als „geändert“ (`SetBasedOn(..., modified: true)` bzw. `missionPresetDirty`).
  - Eine Property, die auch den Default ablehnt, behält im gespeicherten Snapshot den Wunschwert, damit der nächste Ladevorgang es erneut versucht.
- **Begrenzte Wiederholung beim Aufräumen.**
  - `ExitMissionPreset` und `RestorePersonalHostIfPending` werfen bei einer endgültig gescheiterten Property weiterhin die originale Setter-Exception. Das geschieht erst nach Anwendung der übrigen Werte und mit zurückgesetzter Auswahl, die Session behält den Kontext.
  - Das gilt höchstens zweimal (`MaximumRestoreAttempts = 3`). Beim dritten Versuch wird der Kontext verlassen und ein Error nennt die betroffenen Properties.
  - Callback- und Publikationsfehler bleiben ohne Begrenzung wiederholbar, wie bisher.
- **Kompatibilität.** Nur interne Implementierung, keine öffentliche Signatur geändert. Gegenüber dem veröffentlichten 0.9.2 nähert sich das Verhalten wieder dem toleranten Default-Fallback an. Gegenüber dem unveröffentlichten develop werfen normale Ladepfade nicht mehr.
- **Doku.** `docs/THIRD_PARTY_GUIDE.md` (Abschnitt „Applying and leaving a mission context“), `docs/API_CATALOG.md` und der 0.12.5-Changelog in `info.json` sind angepasst. Die bisherige Changelog-Formulierung „without suppressing setter failures“ ist ersetzt.
- **Tests.**
  - `tests/LobbyModSettingsPresetTests`: 95/95 PASS. Neu sind `RejectedValueFallsBackToItsDefaultWithoutBlockingOtherSettings`, `SetterRejectingEvenItsDefaultNoLongerBlocksTheSnapshot` und `PermanentSetterFailureLeavesTheMissionAfterBoundedRetries`. Die bestehenden Tests `SetterFailureRetainsMissionMetadataAndCleanupCanBeRetried` und `CallbackFailureAfterSelectionCommitKeepsMissionReachableForCleanup` bestehen unverändert.
  - `_inspect/HostClientPresetTests` (kompiliert die geänderten Quellen): PASS.
  - `tests/Core.Tests`: 304/304 PASS.
  - `tools/Validation/Test-Documentation.ps1`: PASS.
  - CRLF der geänderten Dateien: geprüft.
- **Build:** APIShared-`build.bat /nopause` (inkl. API-Kompatibilitäts-Baseline und Installer-Tests) PASS, installiert am 2026-10-22 um 13:33.
- **Offen:** der Upgrade- bzw. Lobby-Test aus §6.

*Ursprünglicher Befund und Vorschlag:*

**Kontext (geprüft in `src/ModSettings/Presets/LobbyPresetController.Snapshots.cs`, `.Mission.cs`, `PresetLobbyModSettingsViewModel.cs:1082`, `MissionModSettingsSession.cs:196`, `Internal/LobbySettingsRestoreScope.cs`):**

- `main`: `ApplySnapshot` setzte jede Property einzeln über `TryApplyProperty`. Bei einer Exception wurde eine Warning geloggt und der Default-Wert versucht, danach ging es mit der nächsten Property weiter.
- develop: `ApplySnapshot` und das Laden veröffentlichter Presets laufen über `ApplyPreparedValues`. Dort steht `entry.Key.SetValue(...)` ohne try/catch, in einer Schleife innerhalb von `LobbySettingsRestoreScope.Execute`. Der Code-Kommentar nennt den Grund: „a retryable cleanup cannot be silently converted into a partial success“. Gemeint ist die Missions-Bereinigung. `ExitMissionPreset` soll bei einem Fehler werfen, damit `PresetLobbyModSettingsViewModel.System_ExitMissionPreset` den Missionskontext behält und `MissionModSettingsSession.ExitCore` es später erneut versucht. Diese Absicht ist für die Bereinigung sinnvoll, wurde aber auf **alle** Anwendungspfade ausgedehnt.
- Das Anfangsladen (`LobbyPresetController.Persistence.cs:34`) nutzt dagegen weiterhin das tolerante `TryApplyProperty`. Damit ist das Verhalten uneinheitlich.

**Folgen:**

1. **Halb angewendeter Zustand:** Wirft die k-te Property, sind die Properties 1…k-1 schon gesetzt, die übrigen nicht. `commit()` (Preset-Auswahl), `OnSettingsSnapshotApplied()`, `PublishSettingsSnapshot` und `WriteCombinedPayload` laufen nicht mehr. Der Rollback stellt nur den Auswahlindex wieder her, nicht die Werte. Die UI zeigt dann gemischte Werte, die weder dem alten noch dem neuen Preset entsprechen und auch nicht gespeichert oder an Clients gesendet werden.
2. **Missionskontext hängt fest:** Wirft ein Setter dauerhaft (z. B. Validierung eines alten Werts, Fremdmod-Fehler), scheitert `ExitMissionPreset` bei jedem Versuch. Der Mod bleibt dauerhaft im Missionskontext mit Missions- bzw. Trail-Werten. Jede weitere Missionsbereinigung wirft eine `AggregateException`, persönliche Host-Werte kommen nie zurück.
3. **Preset laden blockiert:** Ein einzelner fehlerhafter Setter verhindert, dass ein ganzes Preset übernommen wird (auch im Host-Lobby-Sync).

**Lösungsvorschlag:**

1. **Pro Setting isolieren, nicht abbrechen.** In `ApplyPreparedValues` jede Property einzeln setzen:
   - Bei einer Exception den Default dieser Property versuchen.
   - Scheitert auch der Default, die Property als `Failed` merken und mit den übrigen weitermachen.
   - Danach `commit()` und `OnSettingsSnapshotApplied()` immer ausführen, damit Auswahl und Werte zusammenpassen.
   - Rückgabe: ein Ergebnis (`Applied`, `FellBackToDefault[]`, `Failed[]`).
2. **Ehrliche Persistenz und Sync.** Nach dem Anwenden den tatsächlich gesetzten Stand per `CaptureCurrentSettings()` speichern und veröffentlichen, nicht den gewünschten Snapshot. Bei einem Default-Fallback das Preset als „geändert“ markieren (`SetBasedOn(..., modified: true)`), damit die UI nicht „unverändertes Preset“ anzeigt.
3. **Bereinigung bleibt wiederholbar, aber begrenzt.** `ExitMissionPreset` und `RestorePersonalHostIfPending` werfen nur noch, wenn `Failed` nicht leer ist, und zwar **nachdem** alle übrigen Werte und die Auswahl zurückgesetzt sind. Die Retry-Zählung pro Property begrenzen (z. B. 3 Versuche oder bis zum nächsten Lobby- bzw. Missionswechsel). Danach die Bereinigung als abgeschlossen behandeln, mit `RuntimeHealth`-Fehler (`MOD_FAILURE`, Mod und Property genannt). So bleibt kein Mod ewig im Missionskontext.
4. **Melden statt still schlucken.** Default-Fallback als Warning (einmal pro Mod und Property, mit Zeitstempel). Endgültiges Scheitern über RuntimeHealth. Damit bleibt der Vertrag „keine stille Teil-Erfolgsmeldung“ erhalten: Das Ergebnis meldet den Teil-Erfolg ausdrücklich.
5. **Einheitlich.** Anfangsladen, Preset-Laden, Snapshot-Anwendung und Missions-Exit nutzen denselben Pfad. Das alte `TryApplyProperty` geht darin auf.
6. **Tests** (`_inspect/HostClientPresetTests`, Pflicht laut `AGENTS/modsettings-ui.md`, mit erhöhten Rechten ausführen):
   - Setter wirft beim Preset-Laden → übrige Werte und Auswahl gesetzt, Property auf Default, Warning.
   - Setter wirft auch beim Default → Ergebnis `Failed`, übrige Werte gesetzt, gespeicherter Stand entspricht der UI.
   - Missions-Exit mit dauerhaft werfendem Setter → begrenzte Retries, danach ist der Kontext verlassen und RuntimeHealth meldet den Fehler.
   - Missions-Exit mit einmalig werfendem Setter → zweiter Versuch erfolgreich, keine bleibende Abweichung.
   - Client (keine Host-Properties) und dynamischer Provider/Backend (Komplettvalidierung bleibt unverändert).

### A2 – Personal Storage zieht nach `Documents/SHCDE/APIShared` um (Mittel, Testbedarf)

- Befund: Die Personal-Storage-Migration verschiebt Presets und `.msgpack`-Daten in einen neuen Ordner.
- Fix: Upgrade-Test von einer echten 0.9.2-Installation mit vorhandenen Presets (siehe §6). Prüfen, ob alte Dateien erhalten bleiben und ob ein zweiter Start nicht erneut migriert. Den neuen Speicherort im Changelog nennen.
- **Analyse 2026-10-22 (Code):** Kein Codefix nötig.
  - `LobbyPresetStorage` liest bevorzugt die neue Datei. Es zieht nur um, wenn sie fehlt und die alte existiert (`NeedsRelocation`), und löscht die alte Datei nicht.
  - Nach dem ersten Schreiben existiert die neue Datei, ein zweiter Start migriert also nicht erneut.
  - Die Migration des alten Preset-1/2-Schemas bewahrt bei einem Fehler die MessagePack-Daten für einen neuen Versuch (`legacyMigrationPending`).
  - Der Changelog nennt den neuen Ort bereits („Store presets … under Documents/SHCDE/APIShared“).
- Optionaler Zusatz: Im Changelog erwähnen, dass die alte Datei liegen bleibt und nach dem Umzug nicht mehr gelesen wird. Bleibt als Testpunkt §6/1.

### A3 – CoopAiStartImport: Plan-Konflikt bricht den Coop-Start ab (Niedrig nach Analyse) – UMGESETZT, nicht committet

- Befund: Melden BugfixesAndQoL (Custom-Lord-Auswahl) und ExtendedData für denselben Spieler widersprüchliche Import-Pläne, wirft der Coop-Start eine Exception.
- Risiko: Mit beiden Mods aus dem Modpack und gepackten Coop-Missionen mit Custom-AIC lässt sich die Mission nicht starten.
- Fix: MP-Test (§6). Bei Konflikt lieber eine deterministische Priorität oder eine klare UI-Meldung statt einer Exception. ExtendedData prüft BFQoL nur per GUID-Präsenz (`CoopAiStartBlockReason`), dort ebenfalls auf echte Aktivierung prüfen.

*Analyse 2026-10-22 (Code: `APIShared/src/Missions/CoopAiStartImportService.cs`, `ExtendedData/src/ExtendedDataRuntime.CoopAiStart.cs`, `BugfixesAndQoL/src/CoopCustomLordSelectionFeature.cs`, `Shared/Adapters/APIShared/CoopAiStartPlanFactory.cs`):*

- **Wann es passieren kann.** Nur beim Start einer gepackten ExtendedData-Coop-Mission, wenn BFQoL installiert ist und beide Provider für denselben KI-Slot einen Plan liefern.
  - Beide bauen ihre Pläne mit derselben `CoopAiStartPlanFactory` aus denselben Lobbydaten. Bei gleicher Eingabe sind die Pläne gleich, oder einer ist ein Präfix des anderen. APIShared führt sie dann zusammen.
  - Einen echten Widerspruch fand ich nur auf einem Pfad, und zwar im Mehrspieler: Der Host wählt für einen Paket-Slot einen Lord mit derselben AIC und derselben **ersten** Burg, aber einer anderen weiteren Burgliste (z. B. eine andere lokale Version desselben Custom Lords).
  - Die Startprüfung `CoopAiStartBlockReason` vergleicht alle Kandidaten. Sie erkennt „angepasst“ und lässt den Start zu, weil BFQoL installiert ist.
  - Der Provider `ProvideCoopAiStart` vergleicht im Mehrspieler aber nur Kandidat 0 (`IsSameSelection(..., singlePlayer)`). Er hält den Slot deshalb für unverändert und liefert die Burgliste des Pakets als **vollständig**.
  - BFQoL liefert aus dem bestätigten AIV-Sync-Manifest die Burgliste des Hosts, ebenfalls als **vollständig**.
  - Zwei vollständige, verschiedene Pläne führen in `Choose` zum Fault. `OnUpload` wirft dann im Vanilla-Upload-Loop `InvalidOperationException("Coop AI start aborted: …")`. Weil alle Peers dieselben Daten haben, scheitert der Start überall gleich, es gibt also keinen Desync. Was die UI danach zeigt, ist nicht getestet.
  - Weitere Fault-Quellen sind Provider, die selbst werfen, z. B. ExtendedData mit „Packaged Coop AI player X is no longer an AI“ oder „has no lobby selection“, wenn sich die Lobby zwischen Prüfung und Start ändert.
- **Wie realistisch.** Gering. Nötig sind Mehrspieler, BFQoL und ExtendedData, eine gepackte Mission mit einem Slot mit mehreren Burgen und eine gezielte Umwahl auf denselben Lord mit derselben ersten Burg. Im Einzelspieler-Coop nutzen beide Provider `self.AIVs` bzw. identische Paketdaten, ein Widerspruch ist dort praktisch ausgeschlossen. **Schwere deshalb auf Niedrig gesenkt.**
- **Sinnvoller Fix (Vorschlag vom 2026-10-22, inzwischen umgesetzt, siehe unten):**
  1. **Ursache in ExtendedData beheben (klein, empfohlen):** Im Mehrspieler einen expliziten Slot mit mehr als einem Kandidaten nicht als vollständig liefern, wenn nur Kandidat 0 verglichen werden konnte und BFQoL die Kandidatenliste synchronisiert. Stattdessen einen unvollständigen Plan mit Kandidat 0, wie im Zweig „angepasst“. BFQoLs Manifestplan gewinnt dann regulär als Präfix-Obermenge. Ohne BFQoL bleibt alles wie bisher, weil die Startprüfung dort die Gleichheit aller Kandidaten garantiert.
  2. **Aktivierung statt GUID-Präsenz (später):** Ist BFQoL installiert, aber dessen AIV-Synchronisierung aus oder fehlgeschlagen, wird nur Kandidat 0 importiert. Das stürzt nicht ab, die Burgauswahl ist aber unvollständig. Sauber wäre eine additive APIShared-Erweiterung: Ein Provider meldet für einen Slot „autoritative Kandidatenliste vorhanden“, und die Startprüfung fragt das ab statt der Plugin-GUID.
  3. **Kein Abbruch mitten im nativen Start (APIShared):** Den Widerspruch schon beim Startknopf des Hosts erkennen. Dazu laufen die Provider einmal trocken mit der künftigen Anfrage, und der Start wird mit einer UI-Meldung blockiert, die beide Owner nennt. So gibt es keinen halben Start. Der bestehende Fault-Pfad bleibt als letzte Absicherung. Das ist eine additive Prüfung ohne Vertragsänderung. Die Alternative „Slot auf Vanilla zurückfallen lassen“ würde den dokumentierten Fault-Vertrag ändern und bräuchte eine API-Kompatibilitätsbewertung.

*Umsetzung 2026-10-22 (Nutzerauftrag: sinnvollste finale Lösung, APIShared einbinden):*

- **APIShared (additiv, API-Baseline: nur 10 hinzugefügte Zeilen, keine entfernten oder geänderten):**
  - `CoopAiSlotPlan.ConfirmedCandidateCount` und eine neue Konstruktor-Überladung: wie viele führende Burgen ein Anbieter wirklich gegen Daten bestätigt hat, die alle Peers teilen (Standard: alle).
  - Zusammenführung (`CoopAiStartImportService.Choose`): Gewählt wird der vollständige Plan mit den meisten bestätigten Kandidaten. Ein vollständiger, nur teilweise bestätigter Plan weicht einem vollständig bestätigten, wenn dessen erste Burgen mit seinen bestätigten übereinstimmen. Zwei vollständig bestätigte, verschiedene Pläne widersprechen sich weiterhin. Für bestehende Pläne (alles bestätigt) ist die Regel unverändert.
  - `ICoopAiStartPreviewCapability.TryPreview` (vom selben Capability-Objekt implementiert) und `CoopAiStartRequest.IsPreview`: führt alle Anbieter einmal trocken aus und liefert die zusammengeführten Pläne oder den Abbruchgrund. Es wird nichts importiert, kein Error geloggt und kein Versuch angelegt.
  - Doku: `THIRD_PARTY_GUIDE.md`, `API_CATALOG.md`, Vertragskommentare, 0.12.5-Changelog.
- **ExtendedData:**
  - Liefert im Mehrspieler die Paket-Burgliste mit 1 bestätigten Kandidaten, weil nur Kandidat 0 mit den Lobbydaten verglichen werden kann.
  - Die Startprüfung `CoopAiStartBlockReason` nutzt die Vorschau statt der BFQoL-GUID. Widersprüchliche Pläne blockieren am Startknopf mit neuer Meldung `ExtendedData.ErrorCoopAiPlansConflict` (de-DE übersetzt, die übrigen 20 Locales englisch wie die vorhandenen Fehlermeldungen).
  - Ein angepasster Slot mit mehreren Burgen braucht einen vollständig bestätigten Plan mit der Burgzahl des Hosts, sonst kommt die bisherige Meldung „unsynchronisiert“. Das deckt jetzt auch „BFQoL installiert, aber Synchronisierung aus“ ab (bisheriger Teil 2).
- **BugfixesAndQoL:** Beantwortet die Vorschau auf dem Host im Mehrspieler-Coop mit den Burglisten, die der Host-Start übertragen wird. Vollständig gilt eine Liste nur, wenn die AIV-Synchronisierung aktiv ist (`MultiplayerAivSyncRuntime` meldet das über `CoopCustomLordSelectionFeature.HostStartSynchronizesCandidates`), sonst nur Kandidat 0.
- **Shared:** `CoopAiStartPlanFactory.Create` hat den optionalen Parameter `confirmedCandidates`.
- **Mindestversion:** BFQoL und ExtendedData setzen jetzt APIShared 0.12.5 voraus (Plugin-Attribut, `info.json`, Changelog), weil sie die neue Überladung aufrufen.
- **Ergebnis der Fälle:** Host unverändert → Paketliste. Host ändert nach der ersten Burg mit aktiver Synchronisierung → synchronisierte Hostliste statt Abbruch. Host ändert ohne Synchronisierung → Blockade am Startknopf mit Meldung. Echter Widerspruch → Blockade am Startknopf statt Abbruch im nativen Start. Der Fault-Pfad bleibt als letzte Absicherung.
- **Prüfungen:**
  - `CoopAiStartImportTests` mit 2 neuen Tests (teilweise bestätigte Liste in beiden Reihenfolgen, Widersprüche, Validierung, Vorschau ohne Import bzw. Versuch, Konflikt in der Vorschau, keine Vorschau während eines Startversuchs)
  - `ApiCompatibilityTests` mit aktualisierter Baseline
  - `Shared.CoopAiStart.Tests` mit neuer Prüfung
  - APIShared-`build.bat /nopause /noinstall`: 366 + 95 Tests PASS
  - `Test-APISharedConsumers`, `Test-SharedBoundaries`, `Test-UnitCommandSplit` (BFQoL kompiliert), MSBuild-Kompilierung von ExtendedData (in ein Scratch-Verzeichnis, 0 Fehler), `Test-DependencyMetadata`: alle PASS
  - Im Spiel nicht getestet (O9).

### A4 – LobbyStateCapability: strengere localPlayerId (Mittel, Testbedarf)

- Befund: `localPlayerId` gilt nur noch, wenn `GameNetworkAPI.GetPlayerIdForSteamId` übereinstimmt.
- Fix: MP-Test mit Lobby-Beitritt, Slotwechsel und Kartenstart, Host und Client.
- **Analyse 2026-10-22:** `LobbyStateCapability` ordnet Spieler über Vanillas `getThisPlayerFromSteamID` zu. Der Netzwerkwert aus `GetPlayerIdForSteamId` dient dort nur noch der Diagnose bei Abweichung. Ein Codefehler ist nicht erkennbar, also kein Codefix. Die übrigen Nutzungen in `GameModes/Internal/PlayerIdentityHelper.cs` habe ich nicht erneut geprüft. Bleibt Testpunkt §6/2.

### A5 – HostLobbySettingsPublisher / PerPlayerLobbyBroadcastGuard (Mittel, Testbedarf)

- Befund: Hook auf das private `GameXAMLManagerAPI.BroadcastSettingChange`. Senden wird verschoben, bis Lobby-ID und TransportReady vorliegen, und ist während einer Partie ausgesetzt.
- Fix: MP-Test mit Hoständerungen vor dem Beitritt, nach dem Beitritt, nach einem Slotwechsel und nach der Rückkehr in die Lobby.
- **Analyse 2026-10-22:** Ohne neuen Codebefund ist kein Codefix sinnvoll. Bleibt Testpunkt §6/2.

### A6 – MissionLifecycle Restart (Niedrig) – UMGESETZT, nicht committet

- Befund: Der restartSkirmish-Hook ruft `Restart()` auf. Bei einer Editor-Session oder einem fremden Thread wirft das innerhalb des Vanilla-Pfads. Ein Yes in der Bestätigung ohne aktive Session tut nichts und erzeugt nur einen Report.
- Fix: Im Hook vor `Restart()` mit `Try…` prüfen und bei nicht unterstütztem Kontext Vanilla durchlassen statt zu werfen.
- **Fix 2026-10-22 (umgesetzt wie vorgeschlagen; `Missions/MissionLifecycleCapability.cs`, `MissionLifecycleService.Restart.cs`):**
  1. Der `RestartSkirmishGame`-Hook ruft `Restart(...)` nur auf, wenn er auf dem Unity-Thread läuft, keine Operation aktiv ist und `state.Current` eine Spielmission ist (`!IsEditor`). Sonst nimmt er den bestehenden `Run(new Operation { … Restart = true }, …)`-Zweig, bzw. auf einem fremden Thread direkt das Original. Dann wirft der Vanilla-Pfad nicht mehr.
  2. Im Bestätigungs-Hook (`ShowConfirmation`) bei „Ja“ ohne verfolgte Session (`source == null`) das ursprüngliche `yes()` aufrufen statt nur zu melden. Vanilla startet dann wie bisher neu. Nur wenn die Session gewechselt hat, weiter melden und nichts tun.
  3. Test in `tests/…MissionLifecycle…`: Restart im Editor, „Ja“ ohne Session, „Ja“ nach Sessionwechsel.
- **Umsetzung:** Die Entscheidungen liegen in `MissionLifecycleService.IsLifecycleRestart` und `DecideConfirmedRestart` (Vanilla / veraltet / Lifecycle). Beim Hook nimmt ein nicht unterstützter Kontext den vorhandenen `Run(...)`-Zweig. Ein „Ja“ ohne verfolgte Spielmission, im Editor oder auf einem fremden Thread ruft Vanillas `yes()` auf. Test: `MissionLifecycleTests.RestartOutsideReadyGameplayKeepsVanillaInsteadOfThrowing` PASS.

### A7 – FixesWheatStartup.VerifyBinaries pinnt viele Hashes (Niedrig) – UMGESETZT, nicht committet

- Befund: Gepinnt sind BepInEx core, Assembly-CSharp, UnityEngine.CoreModule, RedBird, Iced, MonoMod und BepInEx.Preloader. Nach jedem Spiel- oder Extender-Update schreibt jeder Start mit installiertem Fixes einen Error „REFUSED/UNCONFIRMED“. Das Verhalten ist fail-closed und damit korrekt, erzeugt aber Lognoise und Supportanfragen. Der Bootstrap-HookRuntime pinnt den BepInEx-Hash (nur Warning).
- Fix: Diesen Fall als Warning statt Error und einmal pro Hash-Kombination loggen, mit klarer Aussage („Fixes-Weizen-Startkorrektur inaktiv, Spiel unverändert“).
- **Ursprünglicher Fix-Vorschlag 2026-10-22 (durch die Nutzervorgabe ersetzt, siehe unten):**
  - `InstalledAssemblyIdentity.Verify` wirft bei abweichendem Hash eine eigene Ausnahme (z. B. `BinaryIdentityChangedException` mit Datei, erwartetem und tatsächlichem Hash). `FixesWheatStartup.Initialize` fängt sie getrennt und loggt eine Warning: „Fixes wheat start correction inactive: <Datei> differs from the audited build (expected after a game, Script Extender or BepInEx update). Fixes runs unchanged; update APIShared.“
  - Alle anderen Fehler (Startfenster, Lock, Stub-Vertrag) bleiben Error.
  - Die Pins bleiben bestehen (fail-closed). Gleichzeitig lässt sich prüfen, ob die drei Backend-Pins (MonoMod, RedBird, Iced) nötig sind oder ein Versions- plus Semantikvertrag genügt. Das wäre eine separate Native-Analyse.

- **Nutzervorgabe 2026-10-22:** keine Versions- oder Hash-Bedingung, sondern Prüfung des tatsächlichen Codes, damit die Korrektur auch nach Updates externer Mods weiter funktioniert.
- **Umsetzung (`Compatibility/FixesWheatAnalysis.cs` neu, `FixesWheatStartup.cs`, `FixesWheatContract.cs`, `FixesWheatMemory.ReadAvailable`):**
  - **Entfallen** sind alle Datei-Hashes (CrusaderDE, Assembly-CSharp, UnityEngine.CoreModule, BepInEx, BepInEx.Preloader, RedBird ×3, Iced, MonoMod ×2), die festen RVAs (`SellerRva` 0x3EE10, `SiteRva` 0x3F0C9), die festen Byte-Vorlagen (Prolog, verdrängte Bytes, 75-Byte-Stub) und die Annahme von genau zwei erlaubten Displacements (0x50/0x78).
  - **Hook-Stelle:** Ziel von Fixes' eigenem Hook-Handle, in einer ausführbaren Sektion der geladenen DLL. Die verdrängten Bytes des Backends müssen den Disk-Bytes entsprechen und zu vollständigen, sprungfreien, nicht RIP-relativen, stackneutralen Instruktionen dekodieren. Der Live-Eintrag ist der Backend-Sprung zum Stub.
  - **Verkäuferframe:** aus `.pdata`/UNWIND_INFO der Funktion, die die Stelle enthält. Verkettete Fragmente werden bis zur Primärfunktion verfolgt. Fragmente dürfen den Stack nicht ändern, ein Frame-Register ist nicht erlaubt. Die Stackgröße aus den Unwind-Codes muss dem mit Iced dekodierten Prolog entsprechen. Der Prolog muss das unveränderte EDX (2. Argument = Spieler) genau einmal in den Home-Slot `+0x10` schreiben. Kein Befehl der Funktion und ihrer Fragmente darf diesen Slot beschreiben oder seine Adresse nehmen. Der Live-Prolog muss dem Disk-Prolog entsprechen.
  - **Fixes-Stub:** semantisch dekodiert. Er braucht ausgeglichene push/pop, genau einen Stack-Load (`mov r32,[rsp+disp8]`), dessen Register in `cmp byte [flags+reg],0` nach `mov flags, imm64` mit der echten Flag-Array-Adresse indexiert, sowie die verdrängten Instruktionen und den absoluten Rücksprung. Soll-Displacement = gepushte Bytes + Stackgröße + 0x10. Geschrieben wird nur dieses eine Byte und nur, wenn es abweicht. Danach wird alles erneut gelesen und erneut analysiert.
  - **Beibehalten (kein Versions- bzw. Hash-Gate):** die Herkunftsprüfung geladenes Modul = Disk-Datei (Name/MVID) aller Assemblies, deren private Member gelesen werden; Startfenster, Stack, Singletons, Engine-Lock und Einmaligkeit; Schreibschutz-, Cache- und Rückleseprüfung.
  - **Ergebnis:** Updates von Spiel, Fixes, BepInEx, Script Extender, RedBird, Iced oder MonoMod brechen die Korrektur nicht mehr, solange die Code-Regeln gelten. Verletzt ein Update eine Regel, wird fail-closed verweigert und der Error nennt die verletzte Regel. Das bisherige Log-Noise durch Hash-Abweichungen entfällt.
- **Nachweis auf der installierten DLL:** Ohne feste Werte ergibt die Analyse Verkäufer 0x3EE10, Frame 0x58 und Spieler-Slot 0x68, also Soll-Displacement 0x78. Das entspricht genau dem früheren Audit.
- **Prüfungen:**
  - Neu `FixesWheatAnalysisTests`: installierte DLL, synthetisches PE mit und ohne Überschreiben des Home-Slots, 1-Byte-Korrektur, beliebig falsches Displacement, Ablehnungen vor dem Schreiben, verweigertes Schreiben, Nachschreib-Korruption.
  - Angepasst `FixesWheatStartupTests`: semantische Analyse des echten installierten Fixes-Emitters samt Ausführungs-Fixture.
  - `Test-FixesWheatBoundary` bzw. die APIShared-Quell-Guardrails: PASS. Eine Guardrail verbietet `+=` in `FixesWheat*`, deshalb sind dort alle arithmetischen Zuweisungen ausgeschrieben.
  - Im Spiel nicht getestet (O8/O9).
- **Doku:** `docs/FIXES_COMPATIBILITY.md` (abgeleiteter Vertrag), `UpdateToNewDLL.md` (Hinweis, dass Hashes, RVAs und Vorlagen nur noch Beobachtungen sind; der bisherige Text bleibt unverändert erhalten), 0.12.5-Changelog.

### A8 – Changelog 0.10.0–0.12.5 (Niedrig)

- Befund: Diese Versionen wurden nie öffentlich veröffentlicht. Die 0.12.4-Einträge nennen Features, die schon im 0.12.3-Commit vorhanden waren.
- Fix: Für den Release optional zu einem gut lesbaren 0.12.5-Eintrag zusammenfassen (historische Einträge darunter erhalten) oder die Zuordnung korrigieren.
- **Empfehlung 2026-10-22:** Den Einträgen 0.10.0–0.12.4 nichts wegnehmen. Im 0.12.5-Eintrag eine erste Zeile ergänzen: „First public release since 0.9.2; includes all changes listed for 0.10.0–0.12.4 below.“ Die falsch zugeordneten 0.12.4-Zeilen nach 0.12.3 verschieben. Das ist reine Doku und braucht etwa 10 Minuten.

### A9 – Sonstiges (Niedrig/Info)

- `InternalsVisibleTo("MoatMove")` wurde entfernt. Das betrifft nur den Testmod (nicht im Modpack), sollte dort aber geprüft werden. **Erledigt durch B1:** MoatMove nutzt jetzt `Shared.GameBuildingFootprint` und baut ohne APIShared-Internals (Build PASS).
- `ModSettingsStartLog` loggt bei jedem Missionsstart alle Settings aller Mods als Info. Das Volumen ist vertretbar, kurz bestätigen. **Nachgemessen 2026-10-22 im echten Log (ein Start mit einer Partie):** Pro Missionsstart sind es 1 `MODSETTINGS_BEGIN` und 1 Zeile pro Mod (12). Das ist wertvoll für den Support und passt zur S1-Regel (einmal pro Missionsstart). Empfehlung: unverändert lassen.
- `GameplayModActivationGate.IsFeatureAllowed` (`Shared/Adapters/APIShared/GameplayModActivationGate.cs`) cacht nur nach `featureId`, nicht nach `modGuid`. Das ist korrekt, solange jede Assembly eine GUID hat, sollte aber kommentiert werden. Fix: ein Kommentarsatz, kein Verhaltensänderungsbedarf. **Kommentar ergänzt 2026-10-22;** alle Aufrufer übergeben die GUID ihrer eigenen Assembly.
- **Geschützte Arbeitskopie `.work/a/api/001`** (61 geänderte Dateien, Basis `93a67c6`, Stand 17.10.): Die Inhalte scheinen in develop umgesetzt zu sein (NativeHookPublicationState neuer, Pfade nach `Shared/Tools/Paths`), das ist aber **nicht verifiziert**. Vor dem Merge mit develop abgleichen, dann den Schutzvermerk in `.work/INDEX.json` aktualisieren.
- Nur stichprobenartig gelesen: Workshop/Search, Recruitment, TroopActionHud, GateRoutingNativeHooks (siehe §7).

---

## 3. Shared (quellverlinkt)

### S1 – `DebugLogHelper.LogInfo` ohne Diagnostics komplett unterdrückt (Niedrig) – UMGESETZT, nicht committet, nicht gebaut

- Befund: Ohne `DiagnosticsEnabled` schreiben Mods keine READY- oder Lifecycle-Marker mehr ins Release-Log. APIShared loggt Info dagegen immer. Das widerspricht der Logmarker-Empfehlung in `AGENTS/runtime-lifecycle.md` und erschwert Support mit Nutzerlogs.
- Nutzerentscheidung 2026-10-22: nicht beabsichtigt. Info-Logs werden nicht unterbunden. Stattdessen werden die Info-Stellen geprüft und Unnötiges lieber als Debug eingestuft.
- **Umsetzung:**
  - `Shared/Runtime/Diagnostics/DebugLogHelper.cs`: `LogInfo` (beide Überladungen) ist nicht mehr an `DiagnosticsEnabled` gebunden. `ForExternalCode` reicht Info jetzt immer durch und Debug nur mit aktivem Debug, wie APIShared. Warning und Error sind unverändert.
  - Einstufungsregel: **Info** für Einmaliges pro Prozess, Spielstart oder Missionsstart (Installation, Bestätigung nach dem Startup-Cleanup, Moduswahl, native Auflösungen laut `AGENTS/native-analysis.md`) sowie für Nutzeraktionen. **Debug** für alles pro Karte, Checkpoint, Befehl oder Ereignis und für Detailauszüge.
- **Audit:** Release-Code der Modpack-Mods, Shared und APIShared. Die `*.DevDiag.cs`-Dateien und alles unter `#if SERP_DEVDIAG` sind ausgenommen, sie sind nur in DevDiag-Builds enthalten.
  - **Auf Debug herabgestuft (BFQoL, 24 Stellen):**
    - Gate-Routing pro Karte, Checkpoint und Detail: `GateRouting/EnemyGatePathfindingRuntime.cs` (12), `GateRouting/GateTopologySnapshotProvider.cs` (9)
    - Assassinen-Torhaus „ready“ und Zusammenfassung pro Karte (2)
    - `HEALTH_BARS_FIELD_MAP` (1)
  - **Bleibt Info, redundanter `if (DiagnosticsEnabled)` entfernt (BFQoL, 12 Stellen):**
    - Arabian-Idle registriert bzw. erstmals angewendet
    - Gatehouse-Marker installiert, nach dem Start bestätigt, erste Korrektur
    - Rally-Terrain-Hook mit RVA
    - `SPRITE_SHEET_MERGE_APPLIED`
    - endgültiges Löschen von Trail, Save, Karte und Coop-Fortschritt (Nutzeraktionen)
  - **Bleibt Info ohne Änderung:**
    - Shared: `Native address resolved`
    - BFQoL: `VANILLA_PERF_*`, `SPRITE_SHEET_MERGE_OFF/SELECTED/SUPERSEDED`, `DRAWBRIDGE_FIX_*`, Lobby-Save-Loading installiert bzw. bestätigt, Formation aktiv bzw. Speech-Ziele
    - ExtendedData: Lord-Upload, Map-Settings speichern, Trail-Export bzw. -Veröffentlichung (Nutzeraktionen)
    - ExtraFeatures: AI-Marktpolitik registriert, Lord-HP inaktiv für einen Save
  - **Schon Debug:** Die privaten `LogInfo`-Wrapper leiten bereits an `LogDebugLimited` weiter. Das betrifft in BFQoL MultiplayerGameSpeed, QuarryPileRelocation, SiegeAmmoRestock, SingleBuildingPause, AssassinPathfinding und GatehouseLivingCapture, außerdem ExtraFeatures (ChurchPriestCount, GatehouseAutomation), RandomEvents und `ExtendedDataRuntime`. Unverändert; die Namen sind irreführend und könnten optional umbenannt werden.
  - **APIShared:** loggt Info schon immer. Im echten Log vom 22.10. (ein Start mit einer Partie) sind es 33 Info-Zeilen, davon 13 `MODSETTINGS` (siehe A9). Keine Änderung.
  - Erwartetes Mehrvolumen: einmalige Zeilen pro Spielstart (Hook-Installation, Bestätigungen, native Auflösungen) plus Zeilen bei Nutzeraktionen. Keine Info-Zeile pro Tick, Frame, Einheit oder Befehl.
- **Prüfungen:**
  - `Shared/Tests/Diagnostics.Tests`: 19/19 PASS. Der Test `DefaultConfigurationIsSilentAndCreatesNoCrashRecorder` erwartet jetzt: Info sichtbar, Debug stumm ohne Diagnostics.
  - `Test-SharedBoundaries` PASS, `Test-UnitCommandSplit` PASS (Roslyn-Kompilierung von APIShared, BFQoL und MoatMove: 0 Fehler), `Test-DependencyMetadata` PASS, CRLF geprüft.
- **Offen (O8):** Build und Installation sowie die Logkontrolle im Spiel.

### S2 – RuntimeHealthLedger: Warnungen als „MOD_FAILURE“ (Niedrig) – UMGESETZT, nicht committet

- Befund: Shared taggt Warnungen als `MOD_FAILURE`, APIShared unterscheidet `MOD_WARNING`. Außerdem postet Shared Fehler aus fremden Threads über `SynchronizationContext`, während APIShared sofort schreibt.
- Fix: Tag an APIShared angleichen (`MOD_WARNING`). Das Thread-Verhalten dokumentieren oder vereinheitlichen.
- **Umsetzung 2026-10-22:** `RuntimeHealthLedger` in `Shared/Runtime/Diagnostics/DebugLogHelper.cs` taggt Errors als `MOD_FAILURE` und Warnings als `MOD_WARNING`, mit denselben Konstantennamen wie APIShared. `RuntimeHealth.Forward` erkennt beide Tags als schon gemeldet. Test `ConstructionIsSilentAndFailuresRetainSeverityAndContext` prüft beide Tags, `Shared.Diagnostics.Tests` 19/19 PASS. Das Thread-Verhalten (Shared postet Fremdthread-Meldungen über den `SynchronizationContext`) bleibt unverändert. Es ist für die quellverlinkten Mods gewollt, weil BepInEx-Logging aus Simulations-Threads nicht blockieren darf.

---

## 4. Mods

### 4.1 BugfixesAndQoL (1.0.195)

**B1 – „Bewegung durch eigenen Graben“ läuft ohne MoatMove noch teilweise (Hoch, Performance) – UMGESETZT, noch nicht im Spiel getestet**

*Umsetzungsstand 2026-10-22 (vom Nutzer committet als `a511375df`; die Anpassungen der MoatMove-Testsuite und der `build.bat` danach sind noch nicht committet):*

- **Phase A (Gates).** Ohne MoatMove (`TraversalEnabled == false`) laufen an den Hooks nur noch Vanilla und die eigenständigen BFQoL-Fixes. Betroffen sind:
  - Einheiten-Angriffsflut, Gebäude-Builder und Gebäude-Consumer: nur noch Vanilla plus Leiter-Fix (`RunBuildingCandidateConsumerWithVanillaLadderFix` neu).
  - Attack-Scopes und Kandidatenerfassung (`OrderEvents`) sowie direktes Grabenfüllen.
  - Der E2610-Diagnose-String läuft nur noch unter `SERP_DEVDIAG`.
  - Fill-Präfix: ohne MoatMove `MoatTraversalPolicy.GroundOnly` (Nutzerentscheidung). Die volle Fill-„Improvement“-Route entfällt dort, weil sie Grabenkanten verlangt.
- **Phase B (Verschiebung).** An jeder Gate-Stelle wurde der Traversal-Teil als `…Traversal`-Methode herausgeschnitten, 18 Einstiegspunkte insgesamt. Sie sind jetzt abstrakt in `UnitCommandTraversalProvider` (`BugfixesAndQoL/src/UnitCommands/Movement/TraversalProvider.cs`) deklariert und werden von BFQoL nur über `Traversal.…` aufgerufen.
  - Alle Member, die danach nur noch von dort erreichbar waren, wurden semantisch ermittelt (Roslyn, transitive Hülle) und nach `Testmods/MoatMove/src/Moved/` verschoben: 130 Member (Methoden, Felder, Konstanten, drei Typen), rund 4 300 Zeilen aus 32 BFQoL-Dateien.
  - Die Umschreibung von `this`-Zugriffen auf `runtime.` erfolgte per Semantikmodell, nicht per Textsuche.
  - 11 reine Weiterleitungen aus `TraversalDispatch.cs` entfallen. Der verschobene Code ruft direkt die Provider-Implementierung.
  - Darunter: Graben-Flutfüllung (`ReachabilityMap`), Pflichtrouten-Suche, Angriffs- und Gebäude-Fallbacks samt Scopes und Veröffentlichung, Mode-/Builder-/Gruppen-Traversal, Cursor-Topologie, Platzierung und Unstacking über eigene Gräber, direktes Füllen, Fallback-Pfadpublikation und -Audit.
  - `APIShared.Internal.GameBuildingFootprint` wurde durch die identische quellverlinkte `Shared.GameBuildingFootprint` ersetzt (APIShared gibt MoatMove keine Internals mehr frei). `Shared/Runtime/Diagnostics/DevDiag.cs` ist in MoatMove verlinkt.
  - `Testmods/MoatMove/tests/Test-Preflight.ps1` löst jetzt Wildcard-Compile-Einträge auf.
- **Bewusst in BFQoL geblieben:**
  - die nativen Hooks und Fehlerbarrieren (Hook-Besitz, keine überlappenden Hooks)
  - Zustands- und Datentypen, die auch die BFQoL-Hooks nutzen (`PlanScope`, `MoveCommandScope`, Scopes, Delegates) als Schnittstelle zum Provider
  - der gewichtete Planer bzw. die Publikation, die das Feindgraben-Füllen braucht
  - „Verbesserte manuelle Einheitenbefehle“ für Einheiten, die schon im Graben stehen (native Vanilla-Proben)
- **Schon vorher tot (2026-10-22 bereinigt):** Neun Member waren bereits auf HEAD ohne Aufrufer. Entfernt sind `IsMoveFormationTargetAvailable`, `LogBuilderDecision`, `LogCursorOwnerBlockDecision`, `LogPositiveCursorDecision`, `MoatRecordReservationOffset`, `MoatReservationStep`, `ObserveRuntimeCadenceShadow` (samt seinem nur von dort aufgerufenen `CalculateAndLogRuntimeCadenceShadow`), `loggedFormationExecuteMoveTypes` und der dadurch verwaiste `LogCursorDecision`, außerdem der Harness-Stub `LogBuilderDecision` in MoatMove. `TryReadMoatRecord` bleibt, weil die MoatMove-Tests damit die gemeinsame Kapazität des Record-Lesers prüfen. Die gewichteten Planer-Methoden `TryBuild` und `HasSame*` bleiben als Planer-Schnittstelle, obwohl sie derzeit nur von der entfernten Shadow-Diagnose genutzt wurden.
- **Hooks nach APIShared (geprüft, nicht empfohlen):** Das widerspricht `AGENTS/apishared.md` („Native Algorithmen, Hooks … gehören nicht allein wegen eines gemeinsamen Addons nach APIShared“). Es kehrte außerdem die Entscheidung aus `f491e4360` und `_inspect/APISharedOwnership/COMMAND_OWNERSHIP_AUDIT.md` um und erzwänge eine dauerhaft kompatible öffentliche API für rund 18 Erweiterungspunkte samt Zustandstypen, also faktisch die Rückverlagerung der Engine. Sinnvoll erst, wenn MoatMove ohne BFQoL laufen soll.
- **Prüfungen:** siehe §5 „B1-Prüfungen“.

Nutzerentscheidung vom 2026-10-22: MoatMove und der zugehörige Code sind noch nicht effizient genug und wurden deshalb ausgelagert. Ohne den Testmod MoatMove darf **kein** Code zum Thema „Bewegung durch eigenen Graben“ laufen, weil er Lags verursacht. Der Umzug der Graben-Arbeitszielwahl (Commit `321e513ce`) ist damit gewollt. Ein Changelog-Eintrag genügt („Gräber-Fallback durch eigene Gräben vorerst entfernt“).

Audit (Codeanalyse, `BugfixesAndQoL/src/UnitCommands`). Ohne Provider gilt `UnitCommandPathAPI.Traversal == null` und damit `TraversalEnabled == false`. `RouteCalculationMode` ist dann `Exact` (nicht `RequiredOnly`).

*Korrekt an MoatMove gekoppelt:*

- `TryFindRequiredFriendlyCompletedMoatRouteForPlan` (`Movement/UnitCommandPathRuntime.RequiredRoutes.cs:46`): ohne Traversal nur `TryQualifyNativeMoatStart`, also native Probe für Einheiten, die bereits im eigenen Graben stehen. Das ist das vorgesehene Vanilla-Verhalten aus „Verbesserte manuelle Einheitenbefehle“. Darüber laufen auch `TryQualifyMoveCommandFloodBypass`, `…ToEndpoint` und `TryRecoverBeforeBuilder`.
- `ProbeCursorConnectivity` (`Cursor/CursorConnectivity.cs:255`), Cursor-Gruppenauswahl (`Cursor/UnitCommandPathRuntime.Selection.cs:38`), Graben-Arbeitsrouten (`Moat/UnitCommandPathRuntime.WorkTargetReachability.cs:58–70`), Graben-Arbeitszielwahl `relationshipMode == 1`, alle Weiterleitungen in `Movement/TraversalDispatch.cs`, `CreatePlacementSearch` (`Moat/MoatPlacement.cs:203`).

*Ohne MoatMove noch aktiv (zu gaten):*

1. **Angriff auf Einheiten:** `ObserveScopedRegionPairReachabilityCore` → `EvaluateAttackUnitRegionFallback` (`Attack/UnitCommandPathRuntime.RegionFallback.cs:151`) → `TryFindFriendlyCompletedMoatRouteToAttackApproach` (`RequiredRoutes.cs:426`) → `EnsureReachabilityMap` + `GetCachedRouteSummaryForTarget`. Das ist eine Flutfüllung über die ganze Karte mit Zuständen für eigene Gräber, bis zu 8× pro Region-Paar. Erfolg ist nur möglich mit `ReachedWithMoat && FriendlyMoatTiles > 0`. Ohne MoatMove ist das reine Arbeit ohne Nutzen. Dazu legt `ApproachFlood.cs:79` im Exact-Modus für **jeden** Angriffsbefehl einen Diagnose-Scope samt `EnsureAttackCommandCandidates` und `CaptureAttackApproachState` an.
2. **Angriff auf Gebäude:** `EvaluateAttackBuildingRegionFallback` (`RegionFallback.cs:196/341`) und `TryApplyBuildingConsumerFallback` (`Attack/UnitCommandPathRuntime.BuildingCandidates.cs:298`, `BuildingFallback.cs:28`) suchen mit `BuildingCandidateEdge(... MoatTraversalPolicy.FriendlyOnly ...)` Anlaufwege durch eigene Gräber.
3. **Feindgraben direkt füllen:** `TryAllowDirectFillRegionPair` (`Moat/DirectMoatCommandScopes.cs:247`) ruft je Start-Region `EnsureReachabilityMap` auf. Erlaubt wird nur bei `ReachedWithMoat && !ReachedWithoutMoat && FriendlyMoatTiles > 0`, also nur mit einem Weg durch den eigenen Graben.
4. **Gewichtete Fill-Routen (Shadow):** `TryCaptureBuilderWeightedScope` (`Movement/WeightedMoatPublication.cs:24/94`) erlaubt `fill-moat-work` ausdrücklich ohne Traversal (Feature „Feindliches Grabenfüllen verbessern“). Der Planer sucht dabei mit `FriendlyOnly` (`WeightedMoatRoutePlanner.cs:397/415/468/523/719`), Fill-Routen dürfen also eigene Gräber queren. Hier ist zu prüfen, ob für das Grabenfüllen ein Modus ohne eigene Gräber reicht (`GroundOnly` plus feindliche Kontaktkante).
5. **Nebenbefund Hot Path:** `ObserveScopedRegionPairReachabilityCore` (`RegionFallback.cs` ca. Z. 112) baut bei **jedem** nativen E2610-Aufruf während eines Bewegungsbefehls einen `$"E2610-observed:…"`-String und fügt ihn in ein HashSet ein, auch ohne Diagnose. Das ist eine Allokation pro Region-Paar-Abfrage → nur unter `SERP_DEVDIAG` bzw. `DetailedDiagnosticsEnabled`.

*Fix-Vorschlag (zentral statt Einzelpflaster):*

- **a) Flutfüllung:** In `EnsureReachabilityMap` und `VisitNeighbour` ohne `TraversalEnabled` niemals in den Zustand `FriendlyMoatRouteState` wechseln. Ein Start im eigenen Graben bleibt dem nativen Pfad (`TryQualifyNativeMoatStart`) überlassen. Damit endet jede Flut ohne MoatMove am Grabenrand.
- **b) Frühes Aussteigen:** Die Fallbacks 1–3 vor jeglicher Erfassung oder Flut mit `if (!TraversalEnabled) return Reject("traversal-unavailable")` verlassen. Ihr einziges Erfolgskriterium braucht eigene Gräber.
- **c) Angriffs-Scopes:** Im Exact-Modus nur anlegen, wenn `TraversalEnabled` oder DEVDIAG aktiv ist.
- **d) Fill-Shadow:** ohne Traversal eine Policy ohne eigene Gräber verwenden, oder nur nach kurzer Rücksprache, falls das Grabenfüllen sonst schlechter wird.
- **e) Punkt 5:** in DEVDIAG verschieben.
- **f) Absicherung:**
  - In DEVDIAG einen Zähler für „Friendly-Moat-Expansionen“ ergänzen und im Lauf ohne MoatMove auf 0 prüfen.
  - Einen statischen Vertragstest (wie `Testmods/MoatMove/tests/SplitContracts.cs`), der in BFQoL jede Nutzung von `FriendlyMoatRouteState` und `MoatTraversalPolicy.FriendlyOnly` außerhalb ausdrücklich freigegebener, gegateter Stellen meldet.
  - Danach die Spieltests aus §6 Punkt 6 (Angriffe von Einheiten mit Grab-Fähigkeit nahe eigener Gräber, Feindgraben füllen) mit und ohne MoatMove wiederholen und die Lags vergleichen.

**B2 – Sprite-Sheet-Merge: Default abhängig vom VRAM (Mittel) – UMGESETZT, noch nicht im Spiel getestet**

*Umsetzungsstand 2026-10-22 (vom Nutzer committet als `d7bf93d1e`):*

- **Neue Einstellung.** `SpriteSheetMergeMode` (`[PresetLocal]`, Default `Automatic`) mit `Automatic`/`On`/`Off` ersetzt `EnableSpriteSheetMerge`.
  - Das Enum und die Policy liegen in `BugfixesAndQoL/src/VanillaPerformance/SpriteSheetMergeMode.cs`. Der strikte MessagePack-Formatter akzeptiert nur 0–2.
  - Die alte Boolean-Property ist in `IsRetiredPresetProperty` stillgelegt (sie war nie veröffentlicht).
- **Entscheidung zur Laufzeit.** `SpriteSheetMergeOptimization.Decide` wertet `SystemInfo.graphicsMemorySize` erst nach dem Laden der Sprites aus:
  - `Automatic` ab 8192 MB
  - `On` ab 6144 MB (technische Mindestgrenze)
  - `Off` nie
  `EnableClientFeatures = false` gilt als `Off`. Defaults und Presets bleiben dadurch maschinenunabhängig. Die Entscheidung wird als `SPRITE_SHEET_MERGE_SELECTED` bzw. `…_OFF` (mit Modus und VRAM-Wert) geloggt. Seit S1 (2026-10-22) erscheint die Zeile auch ohne Diagnostics, sobald BFQoL neu gebaut ist. Technische Ablehnungen bleiben Warnings.
- **UI.** Die CheckBox ist durch eine ComboBox ersetzt, mit gleichem Suchschlüssel `bugfixes.sprite-sheet-merge` und 60000-ms-Tooltip.
- **Locales.** In allen 21 Sprachen wurden die Keys auf `SpriteSheetMergeMode*` umbenannt. Die Hilfe ist um die Modusbeschreibung ergänzt, dazu drei Optionsbeschriftungen. Die Übersetzungen der neuen Satzteile stammen von mir und sind nicht muttersprachlich geprüft.
- **Changelog.** `BugfixesAndQoL/info.json` (1.0.195) ist ergänzt.
- **Tests.**
  - `ModOptions.Tests`: neu `SpriteSheetMergeModeTests`. Geprüft werden die Schwellen 8191/8192 bzw. 6143/6144, dass `Off` nie zusammenlegt, der Roundtrip und dass ungültige gespeicherte Werte (3, Boolean) abgelehnt werden. 8080 Checks, PASS.
  - Bestanden: `Test-ModOptionsPerformance`, `Test-UnitCommandSplit` (Roslyn-Kompilierung von APIShared und BFQoL aus den Quellen gegen die echten Assemblies, XAML-Wurzeln), `Test-SharedBoundaries -ModDirectory BugfixesAndQoL` (inkl. Tooltip-Audit), `_inspect/HostClientPresetTests`, Locale-Key-Parität (21/21) und CRLF.
- **Hinweis (erledigt):** Die MSBuild-Kompilierung von BFQoL war bis zum APIShared-Build blockiert, weil `Directory.Build.targets` das veraltete APIShared-Paket ablehnte (gewollte Schutzprüfung). Seit dem APIShared-Build um 13:33 ist das Paket aktuell. Der BFQoL-`build.bat`-Lauf um 13:37 war PASS.
- **Offen:** Spieltest auf einer Karte mit weniger als 8 GB und einer mit mindestens 8 GB VRAM (Logzeile `SPRITE_SHEET_MERGE_*` prüfen), Wechsel der Modi mit Neustart, Darstellung der ComboBox in den Modoptionen.

*Ursprünglicher Befund und Vorschlag:*

- Befund: `EnableSpriteSheetMerge = true` ist der Default. Die Optimierung legt 16384²-Seiten an, die Originale bleiben geladen (etwa +1,6 GB, Vanilla etwa 2,4 GB). Die harte Schwelle `SystemInfo.graphicsMemorySize ≥ 6144 MB` ist für 6–8-GB-Karten knapp. Positiv: rein clientseitig, fail-closed, Asset-Overrides ausgespart, kein überlappender Patch im Fixes-Mod oder Extender.
- Nutzerwunsch: Das Standardverhalten soll davon abhängen, ob mindestens 8 GB VRAM vorhanden sind.
- **Warum nicht einfach den Default im ViewModel berechnen:** Die Defaults werden in `BugfixesAndQoLViewModel` (ca. Z. 1595 ff.) als feste Werte gesetzt und vom Presetsystem als Default-Snapshot, für „Zurücksetzen“ und in Preset-Dateien verwendet. Ein dort aus `SystemInfo` berechneter Wert würde Defaults und Preset-Exporte maschinenabhängig machen. Außerdem würde er die Desktop-Tests ohne Unity (`_inspect/HostClientPresetTests`) brechen. Und er würde beim ersten Speichern in die lokale `.msgpack` übernommen, eine spätere Grafikkarte würde ihn also nicht mehr ändern.
- **Vorschlag:**
  - Die bool-Einstellung durch eine Auswahl `SpriteSheetMergeMode` ersetzen: `Automatisch` (Default), `An`, `Aus`. Weiterhin `[PresetLocal]`.
  - Neuer Property-Name (z. B. `SpriteSheetMergeMode`). Die alte bool-Property gab es nur auf develop, sie war nie veröffentlicht. Deshalb ist laut AGENTS keine Rückwärtskompatibilität nötig, nur ein Retired-Eintrag, damit alte Testerdateien nicht stören.
  - Entschieden wird erst in `SpriteSheetMergeOptimization.Decide` (Unity-Hauptthread, nach dem Laden der Sprites):
    - `Automatisch` → Merge nur bei `SystemInfo.graphicsMemorySize ≥ 8192`.
    - `An` → Merge, solange die technischen Prüfungen (`maxTextureSize`, `CopyTexture`) und die harte Mindestgrenze 6144 MB erfüllt sind.
    - `Aus` → nie.
  - Ins Log kommt einmal, welcher Modus und welcher erkannte VRAM-Wert zur Entscheidung führten (`SPRITE_SHEET_MERGE_AUTO: graphicsMemory=… MB → on/off`).
  - Tooltip und Locales (alle Sprachen) anpassen: „Automatisch: ab 8 GB Grafikspeicher“.
- Hinweis: `SystemInfo.graphicsMemorySize` meldet bei integrierten GPUs teils Shared Memory. Optional `Automatisch` zusätzlich auf `SystemInfo.graphicsDeviceType`/dedizierte GPU beschränken, oder im Tooltip darauf hinweisen.
- Test: je ein Lauf mit Automatisch auf einer Karte < 8 GB und einer ≥ 8 GB (Log-Entscheidung prüfen), Wechsel An/Aus mit Neustart, `HostClientPresetTests` wegen der geänderten Property (Pflicht laut `AGENTS/modsettings-ui.md`).

**B3 – Neue Default-an-Features mit Spielauswirkung (Info/Testbedarf)**

- `EnableSoloMultiplayerSaveLoading`: ILHook auf `FRONT_Multiplayer.Update`, damit ein allein bereiter Host einen MP-Save laden kann. Das Vanilla-Remapping bleibt erhalten. Der IL-Vertrag wird streng geprüft und ist sonst fail-closed. → Test: Solo-Host lädt einen MP-Save, danach ein normaler MP-Save mit zwei Spielern.
- `FixSkirmishLoadButtonVisibility`: macht den Laden-Button im Skirmish-Setup sichtbar. → Test, dass er im Coop verborgen bleibt.
- `EnableVanillaPerformanceOptimizations` (Radar-Rebind, MeritData-Lock-Cache): nur UI. `GetMeritData` hat außer dem Spectator-UI und Vanilla-Panels keine Mod-Konsumenten. Ok.

**B4 – Neue Save-Records (Info/Testbedarf)**

- `SessionSaveRecordSlot` mit `BugfixesAndQoL-SingleBuildingPause`, `BugfixesAndQoL-TroopMovementSync` und neu der Seuchen-Popularitäts-Ledger. Schreibt bei fehlendem Zustand bewusst MessagePack-nil, damit ältere Einträge nicht wiederauferstehen. Logisch geprüft: ok.
- Test: Speichern und Laden (SP und MP) mit pausiertem Einzelgebäude und laufendem synchronisiertem Truppenmarsch. Zusätzlich: Ein mit der neuen Version erstellter Save wird mit 1.0.192 geladen (unbekannte Handler dürfen nicht stören).

**B5 – SavegameMissingMap: Hash-Pin auf Assembly-CSharp (Niedrig)**

- Befund: `RuntimeStartup.ValidateGameAssembly` pinnt `BC8B6A39…D789`. Das passt aktuell zur installierten DLL (geprüft). Nach jedem Spielupdate deaktiviert sich der Fix mit einem Error.
- Weitere Kleinigkeiten:
  - Die Statusmeldung „WAITING: installed APIShared 0.9.2“ ist veraltet (APIShared ist jetzt 0.12.5).
  - Der Hash wird selbst berechnet statt über `Shared.DebugLogHelper.GetFileSha256`.
  - Die Soft-`BepInDependency` auf `SavegameMissingMapFixTest_Serp` dient nur der Erkennung des Legacy-Testmods. Das ist in Ordnung, der frühere Verdacht auf Testreste ist damit ausgeräumt.
- Fix: Meldungstext korrigieren. Optional ein Warning statt Error beim Hash-Mismatch, mit klarer Aussage („nach Spielupdate inaktiv“). **Meldungstext umgesetzt 2026-10-22:** jetzt `WAITING: awaiting game library.` ohne Versionsangabe. Der Hash-Pin bleibt (siehe A7-Vorgabe; eine Umstellung auf Codeprüfung wäre eine eigene Native-Analyse).

**B6 – Assassinen-Pfadfindung: Kantenpruning und `FindAnyWeightedRoute` (Mittel, Testbedarf)**

- Logisch geprüft und korrekt. Pfadfindung wirkt aber auf alle Peers, deshalb ist der Testbedarf für MP und Determinismus hoch (Assassinen über Mauern, Torhäuser, Zugbrücken, mit Fixes-Mod).

**B7 – Gate-Routing auf Simulationsticks umgestellt (Info)**

- Tor-Policies und Topologie werden jetzt im Sim-Tick aktualisiert statt im Render-Frame bzw. nach Wall-Clock, im Editor weiter im Render-Frame. Das ist eine Determinismus-Verbesserung. Hooks werden jetzt beim Start installiert (Nutzerentscheidung vom 2026-10-20).
- Test: Tor erobern und im MP sofort durchlaufen lassen.

**B8 – NotificationLastFrame / NotificationSpeechPlayback umgebaut (Mittel, Testbedarf)**

- Neu: der Hook `OnScreenText.addOSTEntry` sowie eine Kopplung an `OST_Message_Bar_Vis`. Kanal-1-Sprache läuft über einen gemeinsamen Owner, der jetzt **jede** akzeptierte Kanal-1-Sprache zählt.
- Prüfpunkte:
  - `OnAddText` ruft `Finish()` und damit Noesis-`MediaElement.Stop/Close` direkt auf. Das ist korrekt, solange `addOSTEntry` nur auf dem Unity-Hauptthread läuft. Der Kommentar in `ResetMapState` deutet an, dass Missionsmeldungen aus anderen Threads kommen können. → Thread im Test loggen (DEVDIAG) bzw. bestätigen.
  - `folder.StartsWith("_SE_")` wirft bei `folder == null`. Das wird gefangen und geloggt, dann fehlt aber die Sprachausgabe. → Null-Check ergänzen (Niedrig). **Umgesetzt 2026-10-22:** `folder != null &&` vor `StartsWith`. Laut dekompiliertem Vanilla-`MyAudioManager.LoadClip` scheitert ein `null`-Ordner auch bei Vanilla an `Path.Combine`, unser Pfad verhält sich jetzt gleich.
- Test: Minimap-Video mit Sprache, Briefing schließen und wieder öffnen, schnell aufeinanderfolgende Meldungen, Linksklick zum Ausblenden, Karte wechseln.

**B9 – Weitere geprüfte Änderungen ohne Befund (Info)**

- Native Detour-Barrieren für alle zentralen Movement-Hooks (Vanilla läuft genau einmal), korrekt.
- `HookInstallPhase` ohne Thread-Suspension im LibraryLoaded-Fenster, korrekt, aber an den Hauptthread gebunden.
- Die Zeitmessungen `DetailedDiagnosticsEnabled ? Stopwatch… : 0` (54 Stellen) speisen nur Diagnose. Alle Suchbudgets sind knotenbasiert, nicht zeitbasiert. Geprüft.
- `QuarryPileRelocation`-Timeout jetzt in Ticks.
- SiegeAmmoRestock-, Plague- und Waterboy-Zustand pro Karte statt pro Prozess (behebt MP-Divergenz).
- `ReachableEnemyGatehouseRuntime`-Cache pro Tick.
- Atomares Schreiben in `SkirmishAiSelectionMemoryHook` plus Backup unlesbarer Stores.
- Spectator nutzt jetzt `eGameTypeModes`.

**B10 – Kleinere Punkte (Niedrig)**

- BFQoL nutzt `APIShared.Internal.*` per InternalsVisibleTo. Das ist vorbestehend, macht BFQoL aber bei jeder internen APIShared-Änderung release-gekoppelt.
- Neue feste Daten-RVAs (z. B. `ChorePatrolModeRva = 0x86C1338`, FormationSpeech) sind hashgebunden. Nach einem Spielupdate erneut auditieren.

### 4.2 ExtendedData (1.0.16)

- **E1 (Mittel) – UMGESETZT 2026-10-22 (nicht committet):** `ExtendedData.Core/ExtendedData.Core.csproj` hatte Assembly- und FileVersion 1.0.15, der Mod ist 1.0.16. Das verstößt gegen die modweit atomare Versionierung. → Jetzt 1.0.16.0. Die Datei war im Repo LF und ist jetzt CRLF. Die modweite Suche ergab kein weiteres aktives `1.0.15`, nur den historischen Changelog-Eintrag.
- **E2 (Niedrig):** `README.md` wurde auf develop geändert. Bestätigen, dass das autorisiert war.
- **E3 (Mittel, Testbedarf):** Der XAML-Patch `HUD_LoadSaveRequester` setzt beim ExtendedData-Host und beim APIShared-Host jeweils `Margin 45,360`. Das ist nur korrekt, wenn beide nie gleichzeitig sichtbar sind. → Ingame testen (Laden mit beiden Mods aktiv).
- **E4 (Niedrig):** Der Cache `BepInEx/cache/ExtendedData/TrailMaker` (TrailMakerEmbeddedMap) wächst unbegrenzt. → Größen- oder Altersgrenze einführen bzw. beim Start alte Einträge entfernen.
- **E5 (Info) – UMGESETZT (Changelog, nicht committet):** Coop-Pakete im Schema 5 sind nur ab 1.0.16 lesbar (`MissionLoader.CurrentSchemaVersion` war auf `origin/main` 4). Zusammen mit R2 kommunizieren. → Eintrag im 1.0.16-Changelog von `ExtendedData/info.json`.
- Siehe auch A3 (Coop-Plan-Konflikt mit BFQoL).

### 4.3 ExtraFeatures (1.0.116)

- Fear-Factor jetzt als native Stubs. Verifiziert: neutral entspricht `baseDamage`, korrekt.
- **X1 (Niedrig/Mittel):** Die Hooks ElevatedMoat und AIDefenseRepair werden jetzt immer beim Start installiert, auch wenn das Feature aus ist. Das kostet Startzeit und erhöht die Kompatibilitätsfläche. Laut Nutzerentscheidung ist eine längere Startzeit akzeptabel. → Startzeit messen und im Test mit dem Fixes-Mod prüfen.
- **X2 (Niedrig):** Die Guard-Fenster sind jetzt in Ticks (80) und laufen während einer Pause nicht ab. Das ist wahrscheinlich gewollt, kurz bestätigen.
- MarketPriceOverrides über OwnedValueOverrides: logisch ok.

### 4.4 CastlePlanner (0.8.44)

- **C1 (Mittel, Testbedarf):** Der Commit-Restart läuft über `APIShared.MissionRestart.Run(CommitRestartCore)` inklusive `startGameTrampoline`. → MP-Test Castle-Preview → Commit → Restart, Host und Client.
- **C2 (Niedrig):** `FreeCastleIdentityReadiness` hat einen Timeout von 30 s. Bei langsamen Peers prüfen, ob das reicht und ob die Fehlermeldung verständlich ist.
- **C3 (Niedrig):** `Prepare-ThinPackage.ps1` kennt die GUID `CastlePlanner_Serp_dev`. Prüfen, dass kein Dev-Paket ins Release gelangt. **Geprüft 2026-10-22:** `CastlePlanner_Serp_dev` steht nur in der Ziel-Allowlist von `Prepare-ThinPackage.ps1` (Entwicklungsinstallation). Die Steam-Werkzeuge kennen keine `_dev`-Pakete. Kein Handlungsbedarf.
- **C4 (Info):** Viele Wortlaut-Tests wurden entfernt. Das ist im Sinne der Vertragsregel, aber die Abdeckung der Kernpfade sollte erhalten bleiben.

### 4.5 UnitCosts (1.0.37) / UnitLimit (1.0.107)

- Direkte Zugriffe auf `MainViewModel.lastTroopBuildChimp/lastTroopsAmountToMake` gegen die echte Assembly-CSharp geprüft: public, ok.
- OwnedValueOverrides und RecruitmentAvailability über APIShared: logisch ok.
- Kein Befund. MP-Rekrutierung mit beiden Mods testen.

### 4.6 StartConditions (1.0.33)

- Fehlende Einträge werden jetzt zu -1/0 statt zum aktuellen Wert (Fix Issue 6): ok.
- **SC1 (Niedrig):** `build.bat` führt `BugfixesAndQoL/tests/Issue6.Tests` und einen `_inspect`-Test aus. Das ist eine modübergreifende Build-Abhängigkeit. → Test nach StartConditions verschieben oder optional machen.

### 4.7 BuildingCosts (1.0.115) / BuildingLimit (1.0.33) / RandomEvents (1.0.53)

- Nur Log-Guards und kleine Optimierungen, kein Befund. RandomEvents-Minimum siehe R3.

### 4.8 SerpsModsHost (1.0.30)

- **H1 (Niedrig, vorbestehend):** Die Beschreibung in `info.json` listet ExtendedData nicht unter „Included mods“. → Ergänzen. **Umgesetzt 2026-10-22:** „- Extended Data“ ergänzt.

---

## 5. Tooling und Doku

- **T1 (Niedrig):** `Shared/Tools/Validation/Test-IngameTooltips.ps1` schlägt standalone fehl (Exit 1). Ursache: `param([string]$Workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..')))`, und in PowerShell 5.1 ist `$PSScriptRoot` im Param-Default leer. Innerhalb von `Test-SharedBoundaries` läuft es durch (PASS). → Default im Body setzen (`if (-not $Workspace) { … }`). **Umgesetzt 2026-10-22:** Default im Body gesetzt, standalone PASS.
- **T2 (Niedrig):** `AGENTS/build-release.md` beschreibt `STEAM_MOD_XX`-Einträge im Kopf von `CreateSteamModPack.bat`. Die gibt es nicht mehr, die Mods stehen in `WorkshopConfiguration.ps1`. → Doku aktualisieren. **Umgesetzt 2026-10-22:** `AGENTS/build-release.md` verweist auf die Liste `Mods` in `WorkshopConfiguration.ps1`.
- **T3 (Info):** `Test-SharedBoundaries` meldet „SKIP: inventory testmod project no longer present: Testmods\FormationTest\FormationTest.csproj“. → Inventar bereinigen. **Geprüft 2026-10-22:** Die Meldung stammt aus dem historischen Analyseinventar `_inspect/UnitIdAccess/inventory.json`. Der Test überspringt das fehlende Projekt korrekt. Das Inventar bleibt als Nachweis unverändert, kein Handlungsbedarf.
- **T4 (Info):** `Test-DevDiagnostics` meldet ein Item (`Shared/Runtime/Native/TemporaryPackedRouteInspection.cs`, unter `#if SERP_DEVDIAG`). Für Release-Builds unkritisch. Vor dem finalen Release entscheiden, ob es bleibt.

Prüfergebnisse dieses Laufs: `Test-SharedBoundaries` PASS, `Test-DependencyMetadata` PASS (51 Manifeste), `Test-PermanentNativeRuntimePatches` PASS, alle info.json valides JSON, Changelogs vorhanden. Gebaut wurde nicht.

**B1-Prüfungen (2026-10-22, Arbeitsstand nach Umsetzung, zunächst nur MSBuild-Kompilierung; `build.bat`-Läufe siehe unten):**

| Prüfung | BFQoL | MoatMove |
|---|---|---|
| Kompilierung | PASS | PASS |
| `Test-UnitCommandSplit` (echte `Assembly-CSharp.dll`, CRLF, JSON/Lifecycle, keine Hook-Mutationen) | PASS | PASS |
| `Test-SharedBoundaries -ModDirectory` | PASS | PASS |
| `Test-PermanentNativeRuntimePatches -ModDirectory` | PASS | PASS |
| `tests/Test-Preflight.ps1` | – | PASS (jetzt mit Wildcard-Auflösung) |
| `ImprovedMoatFilling.Tests` | PASS | – |
| `AssassinPathfinding.Tests` | PASS | – |
| `FriendlyMoatMovement.Tests` (inkl. `SplitContracts`) | PASS | – |
| `git diff --check` | sauber | sauber |

- **MoatMove-Testsuite** (`dotnet run --project tests/MoatMove.Tests.csproj`): repariert (2026-10-22), danach PASS (49 Laufzeitdateien, 217 echte Laufzeitmember kompiliert und ausgeführt). Angepasst wurden nur veraltete Erwartungen und die Quellauswahl, keine Verhaltensprüfung:
  - `ValidateDetailedDiagnostics` akzeptiert auch `const bool` hinter `#if SERP_DEVDIAG`. Diese Abweichung bestand schon auf HEAD (Commit 995).
  - `src/Moved` wird eingelesen. Verschobene Teile ganz eingebundener Dateien (CursorConnectivity, MoatPlacement) kommen mit ihrer Restdatei. Die `…Traversal`-Seeds werden ausgewählt, sobald ein ausgewählter BFQoL-Aufrufer sie aufruft. Vom Harness gestubbte Member behalten ihren Stub.
  - `#if/#else … #endif`, die über Membergrenzen reichen, werden je Member ausgeglichen. `FastSearchNodeBudget` kommt jetzt aus dem Provider statt aus dem Harness, die Harness-Stubs haben `DiagnosticsEnabled` bekommen.
- **Builds und Installation (2026-10-22, Arbeitsstand, `build.bat /nopause`). APIShared und BFQoL entsprachen dabei den Commits `2f2f84b` bzw. `d7bf93d1e`. MoatMove enthielt die noch nicht committeten Testanpassungen:**
  - APIShared 0.12.5: PASS, installiert (`APIShared.dll` 13:33).
  - BugfixesAndQoL 1.0.195: PASS, installiert (13:37, Buildrecord `.work/b/working/287.build.json`).
  - MoatMove 0.1.5: PASS, installiert (13:46). Der erste Lauf (`288.build.json`) ist gescheitert, weil die Testsuite in der Build-Arbeitskopie kein gebautes `APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll` findet. Die BFQoL-`build.bat` setzt dafür `MOAT_TEST_API_SHARED_DLL`, die MoatMove-`build.bat` nicht. Der zweite Lauf ging mit von außen gesetzter Variable auf das Workspace-APIShared durch.
  - **T5 (Niedrig, Tooling) – UMGESETZT:** `Testmods/MoatMove/build.bat` wählt APIShared jetzt wie die BFQoL-`build.bat` (Standard Workspace-APIShared, Override `SHCDE_API_SHARED_DIR`, `Select-ApiSharedBranch.cmd` mit `/apishared:main|dev`). Sie setzt daraus `MOAT_TEST_API_SHARED_DLL` und übergibt `ApiSharedDir` an MSBuild, sodass Tests und Kompilierung dasselbe SDK nutzen. Kontroll-Build ohne externe Variable: PASS, installiert (13:50, `.work/b/working/290.build.json`).
  - **Hinweis:** Im Spiel liegt zusätzlich `BepInEx/plugins/BugfixesAndQoL_Serp_dev` (DLL vom 22.10. 00:04). Vor Spieltests prüfen, dass nur eine BFQoL-Variante aktiv ist.
- **Offen (O1, O2, O4):**
  - Commit der MoatMove-Testanpassungen.
  - B1-Changelog in der BFQoL-`info.json`.
  - Spieltests mit und ohne MoatMove (§6 Punkt 6): Angriffe von Einheiten mit Grab-Fähigkeit nahe eigener Gräber, Gebäudeangriffe, Feindgraben füllen, Bewegung aus dem eigenen Graben. Dazu der Lag-Vergleich ohne MoatMove gegenüber dem bisherigen Stand.
- **Bereinigung 16:30 (Prüfungen):** `Shared.Diagnostics.Tests` 19/19, `Test-IngameTooltips` standalone, `Test-SharedBoundaries`, `Test-UnitCommandSplit` (APIShared, BFQoL, MoatMove: 0 Fehler), MoatMove-Testsuite (217 Laufzeitmember) und `Test-DependencyMetadata`: alle PASS. Nichts gebaut oder installiert.
- **Verhaltensänderung ohne MoatMove (Changelog, O2 umgesetzt 2026-10-22 im 1.0.195-Eintrag von `BugfixesAndQoL/info.json`):**
  - Gräber-Fallbacks durch eigene Gräber entfallen.
  - Angriffsziele von Gräber-Einheiten werden nicht mehr gegen feindliche Gräber gefiltert, das macht jetzt Vanilla.
  - Fill-Routen queren keine eigenen Gräber mehr.

---

## 6. Testplan vor dem Merge (empfohlen)

1. **Upgrade:** saubere 0.9.2/Modpack-main-Installation mit vorhandenen Presets, AI-Auswahl-Store und Saves → auf APIShared 0.12.5 und das neue Modpack aktualisieren → Presets da, Migration nach `Documents/SHCDE/APIShared` einmalig, alte Saves laden (A2, B4).
2. **MP-Lobby (Host und Client):** Beitritt, Slotwechsel, Hoständerungen vor und nach dem Beitritt, Preset laden, Kartenstart (A1, A4, A5).
3. **MP-Save/Load:** mit beiden Peers auf gleicher Version; zusätzlich ein Peer mit alter APIShared-Version, damit Timeout und Meldung geprüft sind (R2). Solo-Host lädt einen MP-Save (B3).
4. **Coop:** gepackte Coop-Mission mit Custom-AIC, BFQoL und ExtendedData aktiv (A3, E5).
5. **Restart:** Skirmish-Restart SP und MP. CastlePlanner Preview → Commit → Restart im MP (A6, C1).
6. **Gameplay-Determinismus MP:** Assassinen über Tore, Mauern und Zugbrücken; Tor erobern; Gräber mit und ohne eigene Moats (B1, B6, B7). Die Partie mehrere Minuten laufen lassen und auf Desync achten.
7. **Client-Optik:** Sprite-Merge auf einer 8-GB-Karte, VRAM beobachten; Minimap-Videos und Meldungen (B2, B8).
8. **Kompatibilität:** alles einmal zusammen mit dem Fixes-Mod und dem aktuellen Script Extender; Startzeit messen (X1).
9. **Karteneditor:** Start, Gate-Routing im Render-Frame-Pfad, Save im Editor (kein Save-Record, siehe `SessionSaveRecordSlot`).

---

## 7. Grenzen dieser Prüfung

- Kein Ingame- oder MP-Test. Alle Laufzeitaussagen beruhen auf Codeanalyse. Gebaut wurden nur APIShared, BugfixesAndQoL und MoatMove (2026-10-22, nach den Umsetzungen), die übrigen Modpack-Mods nicht.
- APIShared-Bereiche Workshop/Search, Recruitment, TroopActionHud und GateRoutingNativeHooks wurden nur stichprobenartig gelesen.
- Locales und CRLF wurden nicht vollständig über alle Dateien geprüft (nur über die Validierungsskripte).
- Reine Diagnoseänderungen (`#if SERP_DEVDIAG`, `[Conditional("SERP_DEVDIAG")]`) wurden nur auf Release-Neutralität geprüft, nicht inhaltlich.
