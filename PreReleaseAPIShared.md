# Pre-Release-Prüfung APIShared (`dev/Serpens66` gegen `main`)

## Prüfstand (Momentaufnahme 31.10.2026)

| Angabe | Wert |
|---|---|
| Repository | `APIShared/` (eigenständiger Checkout), Arbeitsbaum sauber |
| Geprüfter Stand | `dev/Serpens66` = `4a6941f` („62 Fixes“) |
| `develop` | `c37856d`, **Tree identisch** mit `dev/Serpens66` (`9316da6c…`) |
| Vergleichsbasis | `main` = `5034dad` (Version 0.16.3) |
| Umfang | 13 Commits, 96 Dateien, +6254/−124 Zeilen; Version 0.16.3 → 0.20.0 |
| Native DLL | installiert `FBCB9319…31E2` = `CURRENT.json` = auditierter Hash im Code |

Verifikationsweg: `git diff main...dev/Serpens66`, Gegenprüfung gegen den lokalen Script-Extender-Fork, die echte `Assembly-CSharp.dll` (ilspycmd) und die Native-Baseline (`query.ps1 function 0x13030 / 0x41D60`).

Geprüft wurden alle neuen bzw. geänderten Quellen: ESC-Menü-Broker (`IngameMenu*`), `MissionOverEvents`, `VisibleMapRenderEvents`/-Bridge, `SynchronizedSaveRuntime`/`SaveContextRuntime`, `MoveCommandFailureRuntime`, Lord-Media-Korrekturen und -Diagnose, `PackagedLordPresentation`/`CoopLordRestartPresentation`/`LordMetadataDocument`, `LordStatistics`, `EngineReadbacks` inkl. `MarkedUnitSelectionAPI`-Änderung, `RouteTileRules`, `SharedToolTipSizing`/`PopupPixelScale`, Preset-/Savegame-Änderungen, `NativeIndirectEntry`-Refactoring, XAML-Patch, Beispiele, `info.json`, `Verify-Interop.ps1`.

## Ergebnis in Kürze

Im neuen Code habe ich **keinen kritischen Laufzeitfehler** gefunden (kein Absturz-, Desync- oder Datenverlustpfad). Die nativen Eingriffe (`0x13030`, `0x41D60`) sind sauber abgesichert: Hash-Pin, Bytevergleich, geprüftes Indirect-Schema und dauerhaft verwurzelte Hooks. Alle CLI-Tests sind grün.

Vor dem Release sollten aber diese Punkte behoben werden:

| # | Schwere | Status | Thema |
|---|---|---|---|
| B1 | Mittel | bestätigt | Changelog unvollständig, Versionsangaben widersprüchlich (0.18.0 fehlt, globale SE-Verhaltensänderung nicht im Changelog) |
| B2 | Niedrig–Mittel | bestätigt | Native-Site-Inventar: `0x41D60` fehlt in `ApiSharedNativeSites.All()`/Katalogtest/Doku, `0x13030` fehlt in `NATIVE_SITES.md` |
| B3 | Niedrig–Mittel | bestätigt | `UpdateToNewDLL.md`: veralteter Re-Audit-Vertrag für das ESC-Menü |
| B4 | Niedrig | bestätigt (Code + Probe) | Statuszeile endet auf „ · “, z. B. direkt nach „Zurücksetzen“ |
| B5 | Niedrig | bestätigt (ausgeführt) | Neustart-Status zeigt „Map · Basierend auf: Map (modified) (geändert)“ |

Dazu kommen optionale Hinweise (H1–H6) und eine Liste der geprüften und verworfenen Verdachtsfälle. Die früheren Befunde B6 und B7 habe ich in der zweiten Runde herabgestuft bzw. verworfen (siehe [Nachprüfung](#nachprüfung-der-befunde-zweite-runde)).

## Nachprüfung der Befunde (zweite Runde)

Jeder Befund wurde nach der ersten Sichtung erneut gegen den restlichen Code, die Verbraucher-Mods, den Script Extender und die Git-Historie geprüft. B4 und B5 habe ich zusätzlich ausgeführt: zwei temporäre Probetests im Scratchpad, per `CustomAfterMicrosoftCommonTargets` in `tests/LobbyModSettingsPresetTests` eingebunden, ohne eine Repository-Datei zu ändern. Danach habe ich das Testprojekt normal neu gebaut.

| Befund | Ergebnis der Nachprüfung |
|---|---|
| B1 | **Bestätigt.** Der Changelog enthält zu Routenregeln, Statistiken, Readbacks, Archivkontext und Bewegungsfehler-Log keinen Eintrag. Die Versionszuordnung im Guide (ESC-Menü/Visible-Map 0.18.0, Statistiken 0.19.0) passt genau zu den Mindestversionen der Verbraucher: BugfixesAndQoL und Spectator nutzen ESC-Menü/Visible-Map/MissionOver mit 0.18.0, LordOverviewTest nutzt `LordStatistics` mit 0.19.0, ExtendedData nutzt `PackagedLordPresentation` mit 0.20.0. Der **Changelog ist also der Ausreißer**, nicht die Doku. |
| B2 | **Bestätigt.** Der Name `visible-map-render` kommt nur in `VisibleMapRenderEvents.cs` vor, kein Verbraucher und kein Test hängt daran. Ein Umbenennen ist also gefahrlos. |
| B3 | **Bestätigt, mit Herkunft.** `GuiChecks` und das Umhüllen von `ButtonQuitMission` stammen aus Commit `966442d` („59 ESC menu and tooltips“) und wurden in `6742bb2` („59.1 ESC Menu“) aus dem Code entfernt. Der Absatz in `UpdateToNewDLL.md` blieb auf dem Stand von 59. Der heutige Code hookt nur `HUD_IngameMenu.Init`. |
| B4 | **Bestätigt.** Probe: `GetStatusText` liefert ohne Preset und **direkt nach `ApplyDefaultsAsWorkingCopy`** `""`. `System_LoadModDefaults` setzt im selben Ablauf einen Hinweis („Standardwerte übernommen“ usw.). Alle Workspace-Mods (z. B. BugfixesAndQoL, ExtendedData, Spectator) binden genau `System_PresetStatusText`. Der Hinweis ist also nur dort sichtbar, und zwar als „… · “. Die ViewModel-Property selbst ist im Testprojekt per `#if !API_SHARED_PRESET_TESTS` ausgeblendet und dort nicht ausführbar. Der letzte Schritt ist eine reine Stringverkettung und per Code belegt. |
| B5 | **Bestätigt durch Ausführung.** Probe-Ausgabe: eingetreten `Map`, gespeichert `Map (modified)`, nach Neustart `Map · Based on: Map (modified)` bzw. deutsch `Map · Basierend auf: Map (modified)`, nach erneuter Änderung `Map · Basierend auf: Map (modified) (geändert)`. |
| B6 (alt) | **Herabgestuft zu Hinweis H6.** Die Warnung betrifft nur Lords, die im **lokalen** Vanilla-Katalog mit `lordmeta.json` liegen und von SE nicht registriert wurden. Diese Aussage bleibt auch dann richtig, wenn ExtendedData denselben Namen über `PackagedLordPresentation` darstellt. Bei Legacy-Trails ohne Metadaten ist auch „original media retained“ zutreffend. Ungenau ist nur der Satz über die Medien, und nur im Randfall „gleicher Name lokal unregistriert + Trail-Medien über ExtendedData“. |
| B7 (alt) | **Verworfen.** ExtendedData setzt einen Provider nur, wenn `GameAssetModManager.IsRegistered(provider)` bereits gilt. Die Medienordner sind inhaltsadressiert (`MediaId`). Ein Fehlschlag ist daher praktisch dauerhaft (Datei fehlt/kaputt). SE cacht Fehlschläge nicht und loggt bei jedem Versuch einen Fehler. Das negative Caching in APIShared **verhindert wiederholte Datei-I/O und Logflut** auf dem UI-Thread. Kein Fix. |

---

## B1 – Changelog unvollständig, Versionszuordnung widersprüchlich (Mittel, bestätigt)

**Ort:** `APIShared/info.json` (`SerpChangelog`), `docs/THIRD_PARTY_GUIDE.md:1755`, `docs/THIRD_PARTY_GUIDE.md:1882`, `UpdateToNewDLL.md:555`.

**Problem:**
- Der Changelog springt von 0.17.0 direkt auf 0.19.0. Einen Eintrag **0.18.0 gibt es nicht**. Trotzdem verweisen Doku und Verbraucher darauf: Laut Guide sind Visible-Map und ESC-Menü „since 0.18.0“, laut Changelog kamen sie mit 0.19.0. Lord-Statistiken sind laut Guide „since 0.19.0“, stehen aber in keinem Changelog-Eintrag. `UpdateToNewDLL.md` nennt die Visible-Map ebenfalls „(0.18.0)“.
- Diese neuen Features fehlen im Changelog ganz: `RouteTileRules` inkl. `RouteReplanPolicy`, `LordStatistics`, `EngineReadbacks`, `MoveCommandFailureRuntime`-Logkontext, Lord-Media-Registrierungsdiagnose und ruhiger Resolver-Fallback.
- Vor allem fehlt die **Korrektur des SE-Archivkontexts** (`SaveContextRuntime` + `SynchronizedSaveRuntime`). Sie ändert die Flags für **alle** SE-Datenhandler, auch für fremde Mods:
  - synchronisierte MP-Saves liefern jetzt `IsSaveFile=true` mit `FilePath=null`,
  - MP-Save-Loads über `OnLoadMap` werden als Save klassifiziert,
  - Editor-Loads über `OnLoadSave` als Map.

  Der Guide beschreibt das korrekt (`THIRD_PARTY_GUIDE.md`, Abschnitt Script-Extender-Bugfixes). Die APIShared-Regeln verlangen aber, Verhaltensänderungen nicht zu verstecken. Im Changelog, also dem, was Verbraucher bei einem Update zuerst lesen, steht davon nichts.
- Verbraucher deklarieren bereits `0.18.0` (BugfixesAndQoL, Spectator, RuleConsumerProbe) und `0.19.0` (LordOverviewTest). Funktional ist das unkritisch, weil öffentlich nur 0.16.3 → 0.20.0 erscheint. Die Zuordnung „Feature → Version“ ist für Verbraucher aber derzeit nicht nachvollziehbar.

**Fix:** Zwei Varianten:
1. **Empfohlen:** einen Eintrag 0.18.0 ergänzen (ESC-Menü, Visible-Map, Popup-Sizing, wie Doku und Verbraucher es schon annehmen) und den 0.19.0-Eintrag an die Doku angleichen (Lord-Statistiken, ggf. Engine-Readbacks). Alternativ alle Doku-Angaben auf die tatsächlichen Changelog-Versionen umstellen.
2. In jedem Fall die fehlenden Features ergänzen und **die SE-Kontextkorrektur ausdrücklich als Verhaltensänderung** aufnehmen, etwa so: „Correct Script Extender archive contexts: synchronized saves now report `IsSaveFile=true` (path unavailable); multiplayer-save loads are classified as saves and editor loads as maps for every mod-data handler.“

**Bewertung des Fixes:** reine Dokumentation, kein Laufzeitrisiko. Variante 1 hält die vorhandenen Mindestversionen der Verbraucher stimmig und ist deshalb die bessere Wahl. Versionsnummern werden nicht geändert (bleibt 0.20.0).

**Randnotiz (außerhalb von APIShared):** `Testmods/MoatMove/info.json` deklariert APIShared `0.4.10`, nutzt aber `RouteTileRules`, das erst mit diesem Release kommt.

---

## B2 – Native-Site-Inventar unvollständig (Niedrig–Mittel, bestätigt)

**Ort:** `src/Presentation/VisibleMapRenderEvents.cs:120`, `src/Core/NativeSites/ApiSharedNativeSites.cs:160` (`All()`), `docs/NATIVE_SITES.md:154` ff., `tests/APISharedTests/NativeSiteRegistryTests.cs` (`VerifyApiSharedSiteCatalog`).

**Problem:**
- `VisibleMapRenderEvents` reserviert `0x41D60/10` direkt über `NativeSiteRegistry.Register(…, "visible-map-render", …)` und nicht über die zentrale Gruppenliste `ApiSharedNativeSites`. Laut Kommentar an `All()` ist diese Liste die Grundlage „for the catalog test and the documentation table“. Der Katalogtest, der Überlappungen zwischen APIShared-Features ausschließt, prüft `0x41D60` deshalb nicht.
- Der Gruppenname `visible-map-render` weicht vom Schema `Bereich.Name` ab (z. B. `Savegames.SynchronizedSaveContext`).
- In der Tabelle „APIShared's own sites“ in `docs/NATIVE_SITES.md` fehlen sowohl `Savegames.SynchronizedSaveContext` (Exclusive 0x13030/6, Observe 0x13036/13) als auch die Visible-Map-Gruppe. Fremde Mod-Autoren und `native-hooks.md` verlangen ausdrücklich, dieses Register vor neuen Spannen auf Überschneidungen zu prüfen.

Beide Claims nutzen dieselbe Registry-Instanz (`ApiSharedRuntime.ProcessInstance.NativeOwnership`). Zur Laufzeit gibt es also keine Fehlfunktion, nur eine Lücke in Inventar, Test und Doku.

**Fix:**
1. In `ApiSharedNativeSites` eine Konstante `VisibleMapRender = "Presentation.VisibleMapRender"` und `VisibleMapRenderSpans() => new[] { Exclusive(VisibleMapRenderEvents.Rva, VisibleMapRenderEvents.Entry.Length) }` ergänzen und in `All()` aufnehmen.
2. In `VisibleMapRenderEvents.EnsureInstalled` `ApiSharedNativeSites.Claim(…, VisibleMapRender, VisibleMapRenderSpans(), reason => new InvalidOperationException(…))` verwenden. Im Fehlerpfad vor der Veröffentlichung `ApiSharedNativeSites.Release(…)` aufrufen, analog zu `SynchronizedSaveRuntime`.
3. In `VerifyApiSharedSiteCatalog` `(0x41D60, 10, VisibleMapRender)` und `(0x13030, 6, SynchronizedSave)` zu den `expectedWriters` hinzufügen.
4. In `NATIVE_SITES.md` zwei Tabellenzeilen ergänzen („When“: *LibraryLoaded* bzw. *erste Render-Registrierung*).

**Bewertung des Fixes:** Inventar, Test und Doku werden vereinheitlicht, ohne dass sich das Laufzeitverhalten ändert. Nur der Name der Registrierung wechselt. Das ist für fremde Mods sichtbar (Diagnose/Refusals), aber unkritisch, weil die Funktion noch nicht veröffentlicht ist. Den neuen Namen vor dem Release festlegen; danach nicht mehr ändern.

---

## B3 – `UpdateToNewDLL.md`: veralteter Re-Audit-Vertrag für das ESC-Menü (Niedrig–Mittel, bestätigt)

**Ort:** `APIShared/UpdateToNewDLL.md:572–577` (Abschnitt „Shared visible-map entry and pause-menu actions (0.18.0)“).

**Problem:** Der Absatz „Managed presentation“ beschreibt eine frühere Implementierung:
- erwähnt `HUD_IngameMenu.Close()` und das `PresentationEvents.GuiChecks`-Post-Event,
- sagt: „The XAML host wraps `ButtonQuitMission` …“.

Der aktuelle Code macht etwas anderes:
- hookt nur `HUD_IngameMenu.Init()`,
- beobachtet `MainViewModel.PropertyChanged(Show_HUD_IngameMenu)`,
- hängt **alle neun** Vanilla-Buttons aus `LeftPanel` in eigene Zeilen bzw. in ein unsichtbares Parking-Panel um,
- setzt voraus, dass `LayoutRoot`, `LeftPanel`, die neun `x:Name`-Buttons, der Stil `BTN_SH_GlowSDisable` und `Init()` als Setzer von `RefLayoutRoot.Height` existieren (geprüft: Vanilla setzt `Height = 400` bei jedem `Init`).

Wer bei einem Spiel-Update nach dieser Liste prüft, prüft also die falschen Verträge. Außerdem fehlt für `0x41D60` die nach `native-analysis.md` geforderte Begründung, warum es keinen Pattern-Fallback gibt. Für `0x13030` ist sie vorhanden.

**Fix:**
- Den Absatz durch die tatsächlich genutzten verwalteten Verträge ersetzen (Liste oben).
- Für `0x41D60` einen Satz „No unknown-image fallback: …“ mit Begründung ergänzen, analog zu `0x13030`.
- Optional die neuen verwalteten Hookziele auf SE-Interna als Re-Audit-Liste aufnehmen, weil sie bei SE-Updates brechen können:
  - `PackagedLordPresentation`: zehn `GameAIManagerAPI`-Methoden, darunter die interne `TryGetVideoAndAudio` und per Reflection das private `GameAssetManagerAPI.TryLoadTexture`, dazu `CustomisationFileManager.GetCustomLordText`, `OnScreenText.getComputerName`, `Platform_Multiplayer.getPlayerName`,
  - der IL-Patch auf `Platform_Multiplayer.StartGame`,
  - `MoveCommandFailureRuntime` (IL auf `GameTribeManagerAPI.IssueMoveHereCommand`),
  - `SaveContextRuntime` (bereits dokumentiert).

  Die MissionOver-Ziele stehen schon im Guide.

**Bewertung des Fixes:** reine Doku, kein Risiko. Ohne ihn droht bei einem Spiel-Update ein unvollständiger Audit.

---

## B4 – Statuszeile endet auf „ · “ (Niedrig, bestätigt)

**Ort:** `src/ModSettings/Presets/PresetLobbyModSettingsViewModel.Sources.cs:83` (`System_PresetStatusText`).

**Problem:** Neu wird `Hinweis + " · " + Status` zusammengesetzt. `GetStatusText` liefert in gewöhnlichen Fällen `""`, etwa ohne gewähltes Preset (`basedOnName` leer, `LobbyPresetController.cs:160`) oder ohne Controller. Dann zeigt die UI z. B. „Neustart erforderlich · “. Vorher ersetzte der Hinweis den Status, dort trat das nicht auf.

Der häufigste Auslöser ist der Button „Zurücksetzen“ außerhalb einer Mission:
1. `System_LoadModDefaults` ruft `ApplyDefaultsAsWorkingCopy` auf.
2. Das entfernt über `SetBasedOn(null)` das Basis-Preset, der Status wird `""` (per Probe bestätigt).
3. Direkt danach setzt die Methode einen Hinweis wie „DefaultResetApplied“.

Sichtbar wird das in jeder Mod, die `System_PresetStatusText` bindet (alle Preset-Mods im Workspace).

**Fix:**
```csharp
string status = presetController?.GetStatusText(...) ?? string.Empty;
string notice = System_ApplicationNotice;
return string.IsNullOrEmpty(notice) ? status
    : string.IsNullOrEmpty(status) ? notice : notice + " · " + status;
```

**Bewertung des Fixes:** rein darstellend. Die Sichtbarkeitslogik (`System_PresetStatusVisibility`) bleibt korrekt, weil der Text nur nicht leer sein muss. Ein kleiner Test in `LobbyModSettingsPresetTests` ist nur sinnvoll, wenn dort `System_PresetStatusText` ohnehin testbar ist (die Property ist bei `API_SHARED_PRESET_TESTS` aktiv). Sonst genügt eine gezielte Kontrolle.

---

## B5 – Neustart-Status: „Map · Based on: Map (modified)“ und englische Fragmente (Niedrig, bestätigt durch Ausführung)

**Ort:** `src/ModSettings/Presets/LobbyPresetController.cs:132–158`, `src/ModSettings/Presets/PresetLobbyModSettingsViewModel.cs:474–492`, Aufrufreihenfolge `MissionModSettingsSession.cs:169–170`.

**Ablauf:**
1. Die Mission betritt den Kontext „Map“ (`missionPresetLabel = missionContextLabel = "Map"`).
2. Der Spieler ändert einen Wert. Neu: `missionPresetDirty = true` auch ohne Basis-Preset (`Mission.cs:174`). `GetStatusText` liefert dann `"Map (modified)"`.
3. Die Neustartvorbereitung speichert diesen **Anzeigetext**. `CaptureRestartSources` erzeugt ihn mit hart codierten englischen Texten.
4. Nach dem Neustart: `EnterMissionPreset(..., "Map")`, danach `RestorePreparationLabel("Map (modified)")`.
5. `GetStatusText`: Label und Kontext unterscheiden sich, und `"Map (modified)"` beginnt nicht mit `"Map · "`. Das Ergebnis ist **„Map · Basierend auf: Map (modified)“**: falsche „Based on“-Angabe und ein englisches „(modified)“. Wurde zusätzlich mit Basis-Preset gearbeitet und danach erneut geändert, erscheint in nicht-englischen Sprachen das Änderungskennzeichen doppelt („(modified) (geändert)“).

Der vorhandene Test `RestartStatusDoesNotAccumulateContextPrefixes` deckt nur den Fall „Map · Based on: …“ ab, nicht „Kontext + (modified)“ ohne Basis-Preset.

**Fix (empfohlen):** Für den Neustart statt des fertigen Anzeigetexts **strukturierte Daten** speichern, z. B. `sourceLabel` (`missionPresetLabel`), `basedOnPresetId` und `modified` (bool). `RestorePreparationLabel` setzt Label und Dirty-Flag, und `GetStatusText` rendert wie gewohnt lokalisiert. Dann entfallen auch die heuristischen Präfix- und Suffixvergleiche in `GetStatusText`. Für bereits gespeicherte Journale mit altem `label` den bisherigen Pfad als Lesefallback behalten. Das ist eine Formatmigration und kein paralleler Fallback, bitte trotzdem kurz entscheiden.

**Minimalfix (Alternative):** Wenn `missionPresetLabel` gleich `missionContextLabel + " (" + modifiedText + ")"` oder `+ " (modified)"` ist, als Kontext behandeln und `missionPresetDirty = true` setzen. Das ist schnell, bleibt aber heuristisch und zeigt weiterhin englische Fragmente aus älteren Journalen.

**Bewertung:** Der empfohlene Fix behebt die Ursache (Anzeigetext als Persistenzformat) und die Sprachmischung. Er berührt das Neustart-Journalformat und braucht deshalb einen Test für alten und neuen Journaleintrag. Ein bestehender Test (`RestartStatusDoesNotAccumulateContextPrefixes`) sollte um den Fall „Kontext (modified)“ erweitert werden, statt einen neuen Test anzulegen.

**Nachbewertung:**
- Die Schlüssel `label`/`resetSource` werden nur in `PresetLobbyModSettingsViewModel` (`CaptureRestartSources`/`RestoreRestartSources`) gelesen und geschrieben. Es gibt keinen öffentlichen Vertrag und keinen Verbraucher.
- Das Journal ist nur eine kurzlebige Neustartvorbereitung. Zusätzliche Schlüssel (`sourceLabel`, `basedOnPreset`, `modified`) sind additiv. Ein altes Journal mit nur `label` kann den bisherigen Pfad weiter nutzen.
- `RestorePreparationLabel` muss dafür zusätzlich `missionPresetDirty` und bei vorhandenem Basis-Preset `missionBasedOnPreset` (Suche per `StableId` in `publishedPresets`) wiederherstellen.

Das Risiko ist gering. Der Fix ist deutlich sauberer als der Minimalfix.

---

## Optionale Hinweise (keine Fehler)

- **H1 – Allokation im Render-Pfad:** `VisibleMapRenderEvents.Dispatch` (`:151`) legt bei aktivem Bedarf pro Aufruf von `0x41D60` (einmal je Sim-Tick, Sim-Thread) ein Array und pro aktiver Registrierung ein EventArgs-Objekt an. Bei abgeschaltetem Bedarf passiert dank des nativen Bedarfsbytes nichts. Das Array könnte `[ThreadStatic]` wiederverwendet werden. Die EventArgs müssen neu bleiben, weil Aufrufer sie behalten dürfen. Nur umsetzen, wenn eine Messung Bedarf zeigt.
- **H2 – Doku `RouteTileRules.Version`** (`RouteTileRules.cs:51`): Dort steht, dass auch Registrierungen gezählt werden. `TryRegister` erhöht `sequence` aber nicht. Den Kommentar anpassen, nicht den Code.
- **H3 – `EngineReadbacks.TryRegister`** liefert als einzige neue Registrierungs-API keinen `out string reason`. Nach dem Release wäre nur noch eine zusätzliche Überladung möglich. Jetzt ist die letzte Gelegenheit, das ohne Kompatibilitätslast anzugleichen.
- **H4 – Ungenutzter XAML-Schlüssel `APISharedMenuActionButtonStyle`:** Der Code nutzt direkt `BTN_SH_GlowSDisable`. Der Stil wird nur durch `IngameMenuViewTests` festgehalten. Entweder als öffentlichen Ressourcenschlüssel für Verbraucher dokumentieren oder samt Testprüfung entfernen.
- **H5 – „Legacy“-Überladung von `IngameMenuActions.TryRegister`** (ohne Optionen): Sie war nie in `main` veröffentlicht, und im Workspace nutzt sie niemand (BugfixesAndQoL und Spectator nutzen die Options-Variante). Mit dem Release wird sie samt Sonderregeln (QuitMission/Left-Kandidaten, Konfliktregel) dauerhaft kompatibilitätspflichtig. Vorher klären, ob externe Verbraucher (z. B. Ensrick) sie brauchen. Sonst vor dem Release entfernen.
- **H6 – Präziserer Text der `SE_LORD_MEDIA`-Warnung** (früher B6, `LordMediaRegistrationRuntime.cs:59–79`): Die Warnung meldet zu Recht ein lokal nicht registriertes Paket. Der Zusatz „original game media paths are retained“ stimmt aber nicht, wenn ExtendedData denselben Lord-Namen über `PackagedLordPresentation` mit Trail-Medien darstellt. Empfehlung: den Satz abschwächen („… unless another presentation provider supplies media“) oder bei `PackagedLordPresentation.Claims(playerId, name)` nur diesen Satz weglassen, die Warnung selbst aber behalten. Ein Slot-Filter, der die Warnung ganz unterdrückt, wäre falsch, weil das lokale Paket tatsächlich defekt ist.

---

## Geprüfte und verworfene Verdachtsfälle (zweite Prüfrunde)

| Verdacht | Ergebnis der Gegenprüfung |
|---|---|
| `SaveContextRuntime.CorrectLoadFlag` lädt mit `ldarg.0` evtl. `this` statt EventArgs | SE `OnLoadMap`/`OnLoadSave` sind `private static` (`GameMapArchiveManagerAPI.cs:117/133`), `ldarg.0` ist das EventArgs-Objekt. **Kein Fehler.** |
| `SaveContext(true,false,null)` bricht APIShared-Savepfade | `SaveContext.FilePath` ist laut SE nullable. `SavegameModSettings` wertet beim Speichern nur `IsSaveFile`/`IsMapEditorSave` aus. Dokumentiert. **Kein Fehler.** |
| `0x13030` wird auch im Lesemodus der Chore aufgerufen | Baseline bestätigt Serialisierungs- und Ausführungsphase. `NormalizeSave` wirkt nur im SE-Save-Factory-Pfad (`PrepareMapArchiveAndGetBytes`), der nur vom Writer `0x52B0` erreicht wird. Ladekontexte bleiben unberührt. **Kein Fehler.** |
| ESC-Menü: `vanillaMenuHeight` wächst bei jedem Öffnen | Vanilla `HUD_IngameMenu.Init()` setzt `RefLayoutRoot.Height = 400` bei jedem Aufruf. APIShared liest die Höhe danach. **Kein Fehler.** |
| ESC-Menü: `fromAdditional` wird erst beim Klick gelesen | Funktional korrekt: Der Button ist nur klickbar, wenn seine Seite sichtbar ist. Nach `Refresh(true)` blockiert der Seitenwechsel veraltete Klicks. **Kein Fehler.** |
| `EngineReadbacks` läuft nie, weil `threadLock` nicht gehalten wird | Vanilla `EngineInterface.run` ruft `returnBuffer` innerhalb von `lock (threadLock)` auf. `MarkedUnitSelectionAPI` wird beim Start installiert. **Funktioniert.** |
| `CoopLordRestartPresentation`: `Ldarg_3` ist nicht `coopTrailID` | Vanilla-Signatur `StartGame(MPSetupData, FileHeader, int coopTrailID, int coopMissionID)`. Instanz- und DMD-Fall greifen beide auf `coopTrailID` zu. Vanilla füllt `LordNames` nur bei `coopTrailID == 0`, die Ergänzung zielt genau auf `> 0`. **Kein Fehler.** |
| `CoopLordRestartPresentation.Populate` wirft in `StartGame` | `MPAIVInfo.decode` und `RestartMPInfo.SetImage` fangen Ausnahmen in Vanilla selbst ab. Restrisiko sehr gering. **Kein Fix nötig** (ein `try/catch` mit Log wäre nur zusätzliche Absicherung). |
| `PackagedLordPresentation.Message` verbraucht RNG anders als SE | Gleiches `DeterministicRandom.Next(0, clips.Count)` nur bei nichtleerer Clip-Liste, unabhängig von lokalen Medien, wie SE `TryGetVideoAndAudio`. Gleiche Einträge auf allen Peers bleiben Verbraucherpflicht (dokumentiert). **Kein Fehler.** |
| Slotberechnung `lord - SK_X1 + 1`, Komma-Entfernung in Titeln | Entspricht SE (`GetSlotIndexByExtendedLordEnum`, `CustomisationFileManager_Hooks`). **Kein Fehler.** |
| Doppelte Hooks auf `TryGetVideoAndAudio` (Resolver + Packaged) | Managed-Hook-Kette: Packaged ist außen, sonst folgt der Resolver. Der Resolver validiert per DMD die Original-IL. **Kein Fehler.** |
| Teilweise installierte Hooks bei Fehler in `PackagedLordPresentation.Install` | `ManagedInterceptionHook.Install` ist idempotent. Ohne Einträge reichen alle Hooks durch. **Kein Fehler.** |
| Porträt-Fehlschläge werden in `PackagedLordPresentation` dauerhaft gecacht (früher B7) | Provider wird von ExtendedData nur nach `IsRegistered` gesetzt, Medien sind per `MediaId` inhaltsadressiert, ein Fehlschlag ist also praktisch dauerhaft. SE (`GameAssetManagerAPI.TryLoadTexture`) cacht nur Erfolge und loggt jeden Fehlversuch. Das negative Caching verhindert wiederholte Datei-I/O und Logflut. **Kein Fehler.** |
| `MoveCommandFailureRuntime`: `Emit(OpCodes.Ldarg, int)` erzeugt ungültige IL | `SaveContextTests` transformiert die echte SE-Methode und ruft `Generate()` erfolgreich auf. Argumentindizes 4/5/6 = `patrol`/`newOrder`/`moveType`. **Kein Fehler.** |
| `NativeIndirectEntry`-Refactoring ändert das Marktverhalten | Logik ist identisch, nur die Fehlermeldungstexte sind generischer. Kein Test prüft die alten Texte. **Kein Fehler.** |
| `RouteTileRules.MapSize = 800` passt nicht zu den Kartenkoordinaten | Stimmt mit `EnemyGatePathfindingNativeDefinition.MapGridWidth = 800` überein. **Kein Fehler.** |
| `Verify-Interop.ps1` prüft `r_TroopsPatrolMode` ohne APIShared-Verbraucher | Absicht: Verbraucherquellen kommen über `-AdditionalSourceFiles`. **Kein Fehler.** |
| Öffentliche API: Breaking Changes gegenüber `main` | `tests/ApiBaseline/*.txt` enthält nur Zusätze, keine entfernten Zeilen. `ModSettingsWorkingSourceKind.Personal = 3` ist additiv. **Keine binäre Inkompatibilität.** Die Verhaltensänderung der SE-Kontexte steht unter B1. |

## Ausgeführte Prüfungen

`dotnet test` auf dem geprüften Stand (Arbeitsbaum = `dev/Serpens66`), 31.10.2026 18:09–18:10:

| Suite | Ergebnis |
|---|---|
| `tests/Core.Tests` | 355/355 bestanden |
| `tests/APISharedTests` | 465 bestanden, 1 übersprungen, 0 Fehler |
| `tests/LobbyModSettingsPresetTests` | 102/102 bestanden |
| `tests/Content.Tests` | 24/24 bestanden |
| Probetests B4/B5 (temporär, nur Scratchpad, `--filter TestCategory=Probe`) | 2/2 ausgeführt, Ausgaben siehe [Nachprüfung](#nachprüfung-der-befunde-zweite-runde); danach Testprojekt normal neu gebaut |

Nicht ausgeführt: `build.bat`-Gesamtlauf, Doku-Link-Prüfung, Verbrauchertests (`Test-APISharedConsumers.ps1`), Spiel-, Multiplayer- und Coop-Läufe.

## Grenzen dieses Berichts

- Kein Spieltest. Die Wirkung von Visible-Map-Bridge, synchronisiertem Save (echte MP-Chore) und ESC-Menü-Layout bei verschiedenen Auflösungen sowie die Coop-Restart-Namen sind nur statisch bzw. über vorhandene Unit-Tests belegt. Für den Release sind laut `CONTRIBUTING`/`apishared.md` bei Runtime-Änderungen passende Spiel- und Integrationstests vorgesehen, insbesondere ein echter MP-Autosave mit anschließendem Laden (B1-Verhaltensänderung) und ein ESC-Menü mit zwei konkurrierenden QuitMission/Left-Claims (BugfixesAndQoL + Spectator).
- Ich habe keinen Code geändert. Neben dieser Datei sind nur Test-Build-Artefakte (`bin`/`obj`, gitignored) im APIShared-Checkout entstanden.
