# Updateplan: SHCDE Script Extender 1.40.0 auf 1.41.0

Stand: 11. August 2026  
Status: Umsetzung läuft; gemeinsame Preset-/Hostautorisierung einschließlich Trail-Hostsync umgesetzt und statisch getestet, finale Multiplayer-Abnahme noch offen

## 1. Ziel und geprüfte Basis

Dieser Plan beschreibt die notwendigen Workspace-Anpassungen für den Wechsel vom Script Extender 1.40.0 auf 1.41.0. Geprüft wurden alle 27 Commits im Bereich `v1.40.0..v1.41.0`, einschließlich Quellcode, Guides, Referenzdokumentation und Beispielcode, sowie alle auffindbaren Verwendungen der geänderten APIs in den Workspace-Mods.

- Alter Release-Tag: `v1.40.0`, Commit `3681241`
- Neuer Release-Tag: `v1.41.0`, Commit `065184c`
- Umfang: 90 geänderte Dateien, 2711 Einfügungen, 353 Löschungen
- Kanonische installierte Spiel-DLL: `E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll`
- SHA-256 der installierten DLL: `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`
- Dieser Hash stimmt bereits mit den aktiven nativen Workspace-Dokumentationen und `Shared/DebugLogHelper.cs` überein. Für das Extender-Update ist deshalb keine neue native Layoutanalyse erforderlich.
- Vorhandene Benutzerarbeit: `ImprovedHunters_TEST/` ist bereits unversioniert und muss bei allen Schritten erhalten bleiben.

## 2. Kurzfazit

Direkte funktionale Anpassungen sind erforderlich in:

1. `Shared/PresetLobbyModSettingsViewModel.cs` und allen ViewModels mit `[SyncHostOnly]`, weil 1.41.0 die Autorisierung nicht mehr nur beim Senden, sondern vor jeder Mutation verlangt.
2. `ExtraFeatures`, wegen Query-IDs, korrigierter Spawnparameter, der neuen offiziellen Stall-Verknüpfung sowie der neuen verbindlichen Paketregistrierungs- und Chore-Transportregeln.
3. `AIDefense`, weil die eigene Umrechnung von Query-Indizes zu IDs unter 1.41.0 alle IDs nochmals um eins erhöhen würde.
4. `RandomEvents`, weil der bisherige Spawn-Workaround jetzt offizielles API-Verhalten ist und Kommentare/Dokumentation veraltet sind.

Zusätzliche Regressionstests sind für `StartConditions`, `CustomCustomTrail`, `SpawnCastle`, `ImprovedHunters` und alle übrigen Mods mit Lobby-Hostsettings nötig. Die übrigen Änderungen sind additiv, betreffen nur den Extender-Build oder haben derzeit keine wirksame Mod-API-Auswirkung.

## 3. Betroffenheitsmatrix

| Bereich/Mod | Priorität | Befund | Geplante Folge |
| --- | --- | --- | --- |
| `Shared/PresetLobbyModSettingsViewModel.cs` | kritisch | Gemeinsame UI-Sperren und Persistenz sind vorhanden, aber die modseitigen Setter rufen die neue Basisklassenprüfung `CanEdit(propertyName)` nicht systematisch vor der Mutation auf. | Gemeinsamen Autorisierungshelfer ergänzen; alle mutierenden Setterpfade daran anbinden; empfangene Hostwerte weiterhin von lokalen `.msgpack`-Presets fernhalten. |
| `ExtraFeatures` | kritisch | Mehrere Query-Werte werden noch als nullbasierte Indizes behandelt; ein Spawn übergibt Farbe vor Owner; Stallfelder und Slot-Schreibzugriff werden roh umgangen; drei Pakettypen werden bedingt registriert und verwenden den alten Steam-Sendepfad. | Alle vier Bereiche gemeinsam korrigieren und anschließend als ein zusammenhängendes Netzwerk-/Runtime-Paket testen. |
| `AIDefense` | kritisch | `ConvertZeroBasedQueryIndicesToGameIds` erhöht die ab 1.41.0 bereits einbasierten IDs erneut. | Aufruf und nun obsolete Hilfsmethode entfernen; ID-/Global-ID-Invarianten testen. |
| `RandomEvents` | hoch | Der Code übergibt bereits Owner und danach Farbe und bleibt funktional korrekt; Kommentar und `NativeEventNotes.md` beschreiben dies noch als Extender-Fehlerumgehung. Außerdem Hostsetter ohne neue Vorabprüfung. | Workaroundtext entfernen/aktualisieren und Hostsetter autorisieren; Spawndiagnose beibehalten. |
| `BuildingCosts`, `BuildingLimit`, `BugfixesAndQoL`, `ImprovedHunters`, `ImprovedHunters_TEST`, `StartConditions`, `UnitCosts`, `UnitLimit` | hoch | `[SyncHostOnly]` ist klassifiziert, aber die jeweiligen Setterhelfer mutieren vor einer 1.41-Autorisierungsprüfung. Editierbare Tabellen-/Zeilenmodelle sind gesondert zu prüfen. | Setterhelfer und direkte/nestete Mutationen vorab über den gemeinsamen Autorisierungshelfer absichern. |
| `StartConditions` Runtime | mittel | Query-Ergebnisse werden bereits direkt als IDs verwendet. Das entspricht jetzt dem 1.41-Vertrag, kann aber gegenüber 1.40 ein tatsächliches Off-by-one-Verhalten korrigieren. | Keine geplante Codeänderung; gezielte Starttruppen-Regression mit erster/letzter Unit-ID. |
| `CustomCustomTrail` | mittel | Der Coordinator erfasst weiterhin nur `[SyncHostOnly]`. Neue Setter-Gates können aber Mission-Snapshot-Anwendung sichtbar machen, falls sie in einem nicht autorisierten Kontext erfolgt. | Mission-Snapshot-, Trail-, Host- und Clienttests nach der gemeinsamen Setteränderung; keine persönlichen Werte auf Defaults setzen oder persistieren. |
| `SpawnCastle` | niedrig | Nur persönliche/lokale Settings; keine betroffene Query- oder Spawn-API gefunden. Die gemeinsame Presetbasis wird jedoch mitkompiliert und ComboBoxes erhalten automatisch den neuen Dropdown-Fix. | Kompilier-, Preset- und UI-Regression; keine Hostsetteränderung. |
| `ImprovedHunters` Runtime und Testkopie | niedrig | `CreateUnitLocal` verwendet benannte Argumente; die neue Parameterreihenfolge ändert das Ergebnis nicht. | Nur Compile-/Spawnregression; Produktions- und Testkopie konsistent halten, ohne vorhandene Benutzerarbeit zu überschreiben. |
| `ActiveAIVDetector`, `AIVPlacementLobby`, `MultiplayerLeaveFix`, `VanillaAICExporter`, `TestMod LUA` | keine direkte API-Anpassung | Keine Nutzung der brechenden APIs gefunden. Lobby-Hash und künftiges `NetworkMode` können Installation/Testumgebung beeinflussen. | Nur Metadatenklassifikation und Smoke-Test, falls diese Mods im gemeinsamen Testprofil geladen werden. |
| `MPTest` | Entscheidung vor Änderung | Enthält einen eigenen nativen Chore-Probeaufbau. 1.41.0 liefert nun einen offiziellen Chore-Transport. | Vor einer Umsetzung entscheiden, ob der native Probe entfernt oder ausdrücklich als Vergleichsdiagnose behalten wird; nicht beide ungefragt dauerhaft parallel pflegen. |

## 4. Vollständige Commitprüfung

| Commit | Änderung | Workspace-Auswirkung |
| --- | --- | --- |
| `bf45ef9` | Spielunterstützung auf 2.8.0.1 aktualisiert. | Kanonische DLL und aktive Workspace-Hashes entsprechen bereits 2.8.0.1; keine neue native Analyse. Beim Testen Hash im Initialisierungslog bestätigen. |
| `65bea75` | Veraltete Release-ZIP entfernt. | Keine. |
| `067f0ac` | Veraltete Issue-Dateien entfernt. | Keine. |
| `9058901` | Cursorpfade können durch Assets überschrieben werden. | Additiv; kein Workspace-Mod nutzt diese Pfade. |
| `c3ea3dd` | Vertauschte `playerColorId`-/`playerOwnerId`-Weitergabe beim Unit-Spawn korrigiert; öffentliche Reihenfolge ist nun Owner, dann Farbe. | `ExtraFeatures` muss seinen positionalen Aufruf umstellen. `RandomEvents` ist bereits korrekt, braucht aber aktualisierte Erklärung. Benannte bzw. gleiche Argumente in anderen Mods bleiben korrekt. |
| `b9c715a` | `SetStablesUnitIdLink` korrigiert und bidirektional gemacht; `GameUnit.r_LinkedStableBuildingId` und `r_LinkedStableGlobalId` ergänzt. | Obsolete rohe Stall-Workarounds in `ExtraFeatures` durch offizielle Felder/API ersetzen. |
| `59ac22e` | `UnlinkStablesUnitIdLink` ergänzt. | Bei der Pferd-/Ritter-Trennung in `ExtraFeatures` verwenden, soweit dies Vanillas vollständiger Freigabesemantik entspricht; das native Freigabeverhalten einschließlich Pferdezähler separat erhalten und testen. |
| `d0c7717` | Brechende Query-Änderung: `ToIdList`, `ForEach` und Enumerator liefern einbasierte Game-IDs; explizite Indexvarianten ergänzt. | Direkte Korrekturen in `AIDefense` und `ExtraFeatures`; Regression in `StartConditions`. |
| `91224dc` | Lobbys werden nach einem Hash aller aktiven BepInEx- und Asset-Mods gefiltert. | Host und Clients müssen exakt denselben geladenen Plugin-/Asset-Satz und dieselben Versionen haben. Testmods können die Lobbysichtbarkeit verhindern. |
| `0edf939` | Merge des Lobby-Hash-Features. | Keine zusätzliche Änderung über `91224dc`. |
| `de957f0` | Merge des Main-Branches. | Keine eigenständige Mod-Auswirkung. |
| `62d1898` | Nacharbeiten an der Lobby-Hash-Implementierung. | In der Hash-/Lobby-Regression mit abgedeckt. |
| `f3b3cba` | `ModInfo.NetworkMode` mit `Clientside=0` und `Networked=1` für spätere Nutzung ergänzt. | In 1.41.0 wird der Wert beim aktiven Mod-Hash noch nicht ausgewertet. Jetzt klassifizieren und dokumentieren, aber Metadaten erst ändern, wenn die Runtime den Wert tatsächlich nutzt oder die gewünschte Semantik upstream bestätigt ist. |
| `8d3760c` | Abhängigkeits-/Versionsanhebung. | Gegen die lokal gebauten 1.41.0-Assemblies kompilieren. |
| `268d502` | Unterstützung mehrerer Spielinstanzen. | Keine Codeänderung; kann für Host-/Clienttests genutzt werden. Steamworks-Einschränkungen und Lobby-Hash bleiben dabei zu beachten. |
| `774c3bf` | Fehlende DLL in Extender-Buildskript ergänzt. | Nur Extender-Build; lokale Kopie wurde bereits gebaut. |
| `d823e01` | XML-Dokumentationskorrekturen. | Keine Laufzeitänderung. |
| `4192813` | Commit beansprucht, den xxHash nicht mehr zu kürzen. | Im getaggten 1.41.0-Quellstand wird `hash64` weiterhin nach `uint` gecastet und nur auf 16 Hexstellen aufgefüllt. Keine Modkorrektur; nicht von voller 64-Bit-Kollisionsstärke ausgehen und als Upstream-Auffälligkeit behandeln. |
| `81548ac` | Bibliotheken aktualisiert, Chore-Testcode ins ExampleMod verschoben, Networking-Guide erweitert und ComboBox-Dropdown-Fix ergänzt. | `ExtraFeatures` auf den offiziellen Chore-Transport umstellen; alle Modsettings-ComboBoxes visuell testen. Keine manuelle ComboBox-Anpassung, solange kein konkreter Fehler auftritt. |
| `9860a2e` | Veraltete Dateien entfernt. | Keine. |
| `5e8eb47` | Extender-Loglevel korrigiert. | Keine Modänderung; Log-Smoketests dürfen sich nicht auf die früheren Level verlassen. |
| `cd82f83` | Modoptions-Autorität und Sicherheitsprüfung überarbeitet; neue Guides und APIs (`CanEdit`, autorisierte Update-Scope, Sender-Steam-ID, Host-/Spielerauflösung). | Gemeinsame Presetbasis und alle `[SyncHostOnly]`-Setter anpassen; Host-, Client-, Trail- und Persistenztests erweitern. |
| `2c0a1f6` | README aktualisiert. | Dokumentation geprüft; keine weitere Modänderung. |
| `d8464d5` | `gMaxMana` und `gManaRegenAmount` ergänzt. | Additiv; keine Workspace-Nutzung gefunden. |
| `322925f` | README-/Übersetzungs-/RE-Datenbankhinweise aktualisiert. | Keine Modänderung. |
| `9527cad` | Lua-Referenz aktualisiert. | Keine betroffene Lua-Nutzung gefunden. |
| `065184c` | Release 1.41.0 erstellt. | Abschluss-/Versionscommit ohne zusätzliche API-Änderung. |

## 5. Sequenzieller Umsetzungsplan

Die Reihenfolge ist bindend, weil die gemeinsame Autorisierung und die ExtraFeatures-Netzwerkänderungen mehrere nachfolgende Tests beeinflussen. Für einen Mod wird erst gebaut, wenn alle für ihn vorgesehenen Änderungen und Kontrollen abgeschlossen sind.

### Schritt 1: Regressionsprüfungen zuerst erweitern

**Umsetzungsstand 11. August 2026: abgeschlossen, ausgenommen der separate Legacy-Nachweis.** `HostClientPresetTests` deckt Clientablehnung vor der Mutation, autorisierten Hostempfang innerhalb und außerhalb des ausgewählten Trail-Presets, den Preset-Roundtrip zurück zum unveränderten autoritativen Trail-Snapshot, unveränderte lokale `.msgpack`-Daten, `[SyncPerPlayer]`, `[PresetLocal]`, Rollen-/Trail-Wechsel sowie atomare zusammengesetzte und verschachtelte Setter ab und ist grün. `CustomCustomTrail.Tests` ist mit 18/18 Tests grün; der XAML-/Tooltip-/Locale-/CRLF-Audit ist ebenfalls grün.

Noch offen:

- Der bestehende separate Legacy-Presettest validiert neun installierte MessagePack-Dateien, kann in der aktuellen Standalone-Testumgebung wegen der fehlenden nativen `Noesis.dll` aber nicht vollständig bis zum Ende laufen. Dies ist ein zusätzlicher Nachweis, kein derzeit festgestellter 1.41-Funktionsfehler.

- `.inspect/HostClientPresetTests` um 1.41-Szenarien erweitern:
  - Ein Client kann eine `[SyncHostOnly]`-Property nicht lokal mutieren.
  - Eine autorisierte eingehende Host-Aktualisierung wird auf dem Client angewandt.
  - Der empfangene Hostwert gelangt nicht in die lokale `.msgpack`.
  - `[SyncPerPlayer]` und `[PresetLocal]` behalten ihr bisheriges Verhalten.
  - Trail- und Rollenstatus bleiben unabhängig.
  - Zusammengesetzte Setter, Min/Max-Paare und editierbare Tabellenzeilen mutieren bei Ablehnung überhaupt keinen Teilzustand.
- Statische Audits ergänzen, die folgende Legacy-Muster nach Abschluss verbieten:
  - `ConvertZeroBasedQueryIndicesToGameIds`
  - `ConvertQueryBuildingIndexToId`
  - `SetStablesUnitIdLinkFixed`
  - rohe Zugriffe auf die nun benannten Stall-Link-Felder
  - bedingte oder einstellungsabhängige Registrierung der drei `ExtraFeatures`-Pakettypen
- Noch nicht bauen.

Abnahme: Die neuen Tests müssen vor den Änderungen die relevanten Altpfade erkennen und nach den jeweiligen Schritten grün werden.

### Schritt 2: Gemeinsame Host-Autorisierung integrieren

**Umsetzungsstand 11. August 2026: abgeschlossen, ausgenommen die reale Multiplayer-Abnahme.** Der gemeinsame Einstieg `CanMutateSetting(...)`, Revert-Benachrichtigungen ohne Sync/Persistenz, die sichere Presetzusammenführung und die Gates aller unten aufgeführten ViewModels einschließlich editierbarer Tabellenzeilen sind umgesetzt. Autorisierte Netzwerkupdates umgehen ausschließlich die lokale Trail-Schreibsperre, bleiben durch `CanEdit(propertyName)` abgesichert und aktualisieren den flüchtigen Trail-Snapshot ohne lokale Persistenz. Alle betroffenen Projekte kompilieren die gemeinsame Datei zusammen mit `Shared/GameModeHelper.cs` und verwenden die zentrale Presetregistrierung.

Noch offen:

- Danach einen realen Host-/Client-Lauf mit dem finalen Build durchführen und anhand des neuen BepInEx-Logsegments bestätigen, dass Hostwerte auf dem Client ankommen, Trail-Werte nicht lokal persistiert werden und keine Autorisierungs-/Revertfehler auftreten. Der bisher letzte Lauf endete vor dem finalen Shared-Fix und enthielt keinen echten Multiplayerkontext.

- In `Shared/PresetLobbyModSettingsViewModel.cs` einen geschützten gemeinsamen Einstieg ergänzen, der vor einer Property-Mutation `LobbyModSettingsBaseViewModel.CanEdit(propertyName)` aufruft.
- Die bestehende Erkennung eingehender Netzwerksynchronisation weiterhin nutzen, um Host-/Trail-Werte nicht in lokale Presets zu schreiben. Die neue Extender-Autorisierung ersetzt diese Persistenzgrenze nicht.
- Sicherstellen, dass Revert-Benachrichtigungen keine Presetpersistenz und keine erneute Netzwerksynchronisation auslösen.
- Danach jeden Setterhelfer und jeden direkten/nesteten Mutationspfad der folgenden ViewModels umstellen:
  1. `BuildingCosts`
  2. `BuildingLimit`
  3. `BugfixesAndQoL`
  4. `ExtraFeatures`
  5. `ImprovedHunters`
  6. `ImprovedHunters_TEST`
  7. `RandomEvents`
  8. `StartConditions`
  9. `UnitCosts`
  10. `UnitLimit`
- Insbesondere Kosten-/Limit-Tabellen dürfen nicht zuerst ihr Zeilenobjekt mutieren und erst danach die äußere `[SyncHostOnly]`-Property melden.

Abnahme: Host kann Werte ändern und synchronisieren; Clientänderungen werden vor jeder Mutation verworfen; empfangene Hostwerte bleiben wirksam, aber lokal ungespeichert; Reset/Preset/Trail verhalten sich gemäß Rollenbindings.

### Schritt 3: `ExtraFeatures` auf Query- und Spawnvertrag 1.41 umstellen

- In `KnightDismountRuntime.cs` die Ergebnisse der Building-Query direkt als Game-IDs verwenden und beide `+1`-Konvertierungen samt Hilfsmethode entfernen.
- In `ChurchPriestCountRuntime.cs` den historischen `queryValue + 1`-Fallback entfernen; eine ungültige ID explizit diagnostizieren, statt sie still umzudeuten.
- Beim `CreateUnitLocal`-Aufruf Owner zuerst und Farbe danach übergeben; benannte Argumente verwenden, damit die Semantik nicht erneut von Positionsannahmen abhängt.
- Grenzfälle mit ID `1`, letzter belegter ID und Lücken in der Struktur-/Unitliste testen.

Abnahme: Jede Query-ID löst exakt denselben Datensatz via `TryGetById` auf; keine doppelte Erhöhung; neu erzeugte Einheit besitzt den erwarteten Owner und die erwartete Spritefarbe.

### Schritt 4: `ExtraFeatures` auf offizielle Stall-Verknüpfung umstellen

- `GameUnit.r_LinkedStableBuildingId` und `r_LinkedStableGlobalId` statt roher Offsets verwenden.
- `SetStablesUnitIdLink(..., bidirectional: true)` statt `SetStablesUnitIdLinkFixed` verwenden.
- Beim Trennen `UnlinkStablesUnitIdLink(..., bidirectional: true)` einsetzen, wenn genau diese Verknüpfung gelöst werden soll.
- Den vorhandenen Vanilla-Freigabepfad nicht blind durch `Unlink` ersetzen: prüfen, ob er zusätzlich `r_TotalHorses`, Slotbelegung oder andere Zustände fortschreibt. Vanilla genau einmal ausführen.
- Nach erfolgreicher Umstellung alte Offsets, Setter und Fallbacks vollständig entfernen; keine parallele Legacy-Implementierung behalten.

Abnahme: `stable slot ID/global ID` und `unit linked stable ID/global ID` sind vor und nach Mount/Dismount bidirektional konsistent; Pferdezähler und Alive-State folgen Vanillas regulärem Pfad.

### Schritt 5: `ExtraFeatures`-Pakete deterministisch und desync-sicher machen

- `KnightDismountPacket`, `KnightMountPacket` und `QuarryPileRelocationPacket` am frühesten sicheren Extender-Netzwerk-Initialisierungspunkt genau einmal, unbedingt und in fester Reihenfolge registrieren.
- Registrierung und Eventsubscription dürfen weder von `EnableMod`, einzelnen Featureflags, DLL-Hashvalidierung noch Verfügbarkeit nativer Hooks abhängen. Nur die Handlerwirkung wird durch Settings und Runtimefähigkeit begrenzt.
- Die bestehenden expliziten Formatter und stabilen numerischen `[Key(...)]`-Werte beibehalten.
- Zustandsändernde Multiplayeraktionen über `SendPacketToAllEx2(..., viaChore: true)` senden.
- Da der Chore-Weg an den Sender selbst ausliefert, vor dem Senden nicht zusätzlich lokal anwenden. Im Singleplayer bleibt ein direkter lokaler Pfad.
- Vor jeder Multiplayeraktion `ChoreNetworkTransport.IsAvailable` prüfen. Bei Nichtverfügbarkeit sicher abbrechen und mit Zeitstempel protokollieren; kein stiller Steam-Fallback für zustandsändernde Aktionen.
- Paketgröße gegen das Chore-Limit von ungefähr 1200 Bytes absichern.
- Sender-/Besitzprüfung überprüfen: Chore-Pakete liefern derzeit keine `SenderSteamId`; deshalb müssen Unit-/Building-Owner, Auswahlidentität, Global-ID, Phase und lokale Berechtigung aus dem Paket und dem aktuellen Spielzustand konsistent validiert werden.

Abnahme: Host und Client registrieren identische Paket-IDs unabhängig von ihren gespeicherten Einstellungen; jede Aktion wird auf jedem Peer einschließlich Sender genau einmal angewandt; bei fehlendem Chore-Transport ändert sich kein Zustand.

### Schritt 6: `AIDefense`-Query-IDs korrigieren

- Den Aufruf `ConvertZeroBasedQueryIndicesToGameIds(aliveUnitIds)` entfernen.
- Die Hilfsmethode vollständig entfernen.
- Alle nachfolgenden Lookups und Logs sprachlich von „Index“ auf „ID“ korrigieren, soweit sie Query-Ergebnisse meinen.
- Spawnaufrufe mit identischer Owner-/Farb-ID bleiben funktional unverändert; nach Möglichkeit benannte Argumente zur Eindeutigkeit verwenden.

Abnahme: `eligible == changed + remaining` beziehungsweise die vorhandenen Runtime-Invarianten bleiben erfüllt; Unit-ID `1` und die letzte gültige Unit werden nicht verschoben oder ausgelassen.

### Schritt 7: `RandomEvents` und übrige Spawnverwendungen bereinigen

- In `RandomEventsRuntime.cs` den Owner-/Farb-Aufruf als reguläres 1.41-Verhalten dokumentieren, nicht mehr als Upstream-Workaround.
- `RandomEvents/NativeEventNotes.md` entsprechend aktualisieren; die native Feldvalidierung bleibt als Diagnose wertvoll.
- `ImprovedHunters`, `ImprovedHunters_TEST`, `StartConditions` und `AIDefense` kompilieren und ihren Spawn mit erwarteter Owner-/Farb-ID prüfen. Bei unterschiedlichen Werten ausschließlich benannte Argumente verwenden.

Abnahme: Alle Spawnstellen folgen dem Vertrag `playerOwnerId`, danach `playerColorId`; keine veraltete Aussage über vertauschte Extenderparameter verbleibt.

### Schritt 8: Lobby-Hash, `NetworkMode` und UI-Verhalten absichern

- Für jede ausgelieferte Mod eine gewünschte künftige Klassifikation festhalten:
  - `Networked`: verändert gemeinsame Simulation, Hostsettings oder synchronisierte Spielzustände.
  - `Clientside`: rein lokale Anzeige, Export oder lokale Bedienhilfe ohne gemeinsamen Zustand.
- `NetworkMode` in 1.41.0 noch nicht massenhaft in `info.json` eintragen, weil der getaggte Code das Feld beim Lobby-Hash nicht auswertet. Nach einer upstream bestätigten Nutzung die kanonische Datei und jede Paketkopie atomar aktualisieren.
- Für aktuelle Multiplayer-Tests auf Host und Client denselben vollständigen Plugin-/Asset-Satz und dieselben Versionen verwenden. `ImprovedHunters_TEST`, `MPTest` oder andere Diagnoseplugins dürfen nicht nur auf einer Seite geladen sein.
- Alle ComboBoxes der Modsettings in oberer und unterer Bildschirmhälfte sowie mindestens einer kleinen und einer großen Auflösung prüfen. Den neuen automatischen Dropdown-Fix nur bei einem reproduzierbaren Konflikt modseitig deaktivieren.
- Die Upstream-Auffälligkeiten getrennt notieren:
  - `NetworkMode` wird trotz Guide/Modell noch nicht im Hash verwendet.
  - Der angeblich ungekürzte xxHash wird im Releasecode weiterhin nach `uint` gecastet.
  - Der Chore-Guide nennt an einer Stelle „1.5.0“, obwohl die Funktion zum hier geprüften 1.41.0-Bereich gehört.

Abnahme: Host-/Client-Lobbysichtbarkeit ist mit identischem Satz reproduzierbar; abweichender Modsatz wird erwartungsgemäß gefiltert; kein ComboBox-Dropdown liegt außerhalb des sichtbaren Bereichs.

### Schritt 9: Gesamtaudit, Tests und genau ein Build je geändertem Mod

Vor jedem Build:

1. Betroffene Textdateien gezielt auf CRLF und nackte LF prüfen.
2. `.inspect/HostClientPresetTests` vollständig ausführen.
3. XAML-Audit auf Tooltips, `ToolTipService.ShowDuration="60000"`, Locale-Key-Parität und CRLF ausführen.
4. Statische Legacy-Suche aus Schritt 1 ausführen.
5. Projekte gegen die lokal gebauten 1.41.0-Assemblies kompilatorisch prüfen, ohne die Installationslogik der `build.bat` zu duplizieren.
6. `CustomCustomTrail/src/TrailMissionSettingsCoordinator.cs` mit Trail-Snapshots erneut testen.

Danach jede tatsächlich geänderte Mod genau einmal über ihre eigene `build.bat` direkt und erhöht bauen/installieren. Sinnvolle Reihenfolge nach gemeinsamem Code und Risiko:

1. `ExtraFeatures`
2. `AIDefense`
3. `RandomEvents`
4. `BuildingCosts`
5. `BuildingLimit`
6. `BugfixesAndQoL`
7. `ImprovedHunters`
8. `StartConditions`
9. `UnitCosts`
10. `UnitLimit`
11. `SpawnCastle`, falls die gemeinsame Datei eine neue Binärdatei erzeugt
12. `CustomCustomTrail`, falls Code/Metadaten geändert wurden
13. `ImprovedHunters_TEST` nur als bewusstes Testartefakt und ohne Produktionsinstallation zu überschreiben

Nach den Builds:

- BepInEx-Log ab der neuen Startmarke auswerten; eigene Logzeitstempel mit Millisekunden verwenden.
- Keine Callback-, Formatter-, Paket-ID-, Autorisierungs- oder Hashvalidierungsfehler zulassen.
- Host/Client/Trail sowie Singleplayer-Skirmish getrennt prüfen; `GameNetworkAPI.IsNetworkedEnvironment()` nicht allein zur Moduserkennung verwenden.
- Für `ExtraFeatures` mindestens Mount, Dismount und Quarry-Relocation mit genau-einmal-Ausführung auf beiden Peers prüfen.
- Für Query-Mods erste, mittlere und letzte gültige IDs sowie Lücken prüfen.

## 6. Definition of Done

Das Update ist abgeschlossen, wenn:

- keine eigene `+1`-Korrektur mehr auf 1.41-Query-IDs angewandt wird;
- alle Spawnstellen das Owner-/Farb-Vertragsmodell eindeutig verwenden;
- `ExtraFeatures` ausschließlich die offiziellen Stall-Link-Felder und -Methoden nutzt;
- alle eigenen Pakettypen unabhängig von Settings in deterministischer Reihenfolge registriert werden;
- zustandsändernde Multiplayerpakete ohne asynchronen Steam-Fallback und exakt einmal verarbeitet werden;
- jede `[SyncHostOnly]`-Mutation vorab autorisiert ist, einschließlich verschachtelter Tabellenwerte;
- empfangene Host-/Trail-Werte niemals in lokale Presetdateien gelangen;
- HostClientPresetTests, XAML-/Locale-/CRLF-Audits und Runtime-Invarianten grün sind;
- jeder geänderte Produktionsmod nach Abschluss aller Prüfungen genau einmal über seine `build.bat` gebaut und installiert wurde;
- die BepInEx-Logs der abschließenden Singleplayer- und Multiplayerläufe keine relevanten Fehler enthalten.

## 7. Bewusst nicht geplante Änderungen

- Keine erneute native RVA-/Layoutanalyse, solange die installierte DLL weiterhin den verifizierten SHA-256 besitzt.
- Keine Modnutzung der neuen Cursor-, Mana-, Keep-, Pitch-, Tile- oder sonstigen additiven APIs ohne eigenen fachlichen Bedarf.
- Kein eigener paralleler Preset-, Rollen-, Persistenz- oder Netzwerkvertrag neben den Shared-Komponenten und der 1.41-API.
- Kein dauerhafter Legacy-Fallback für die ersetzten Query-, Spawn- oder Stall-Workarounds.
- Keine ungefragte Entfernung oder Übernahme des bestehenden unversionierten `ImprovedHunters_TEST/`-Ordners.
