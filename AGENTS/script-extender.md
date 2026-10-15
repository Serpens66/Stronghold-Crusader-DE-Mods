# Script Extender: Fork, Updates, API-Verträge, bekannte Fehler

**Lesen, wenn:** Script-Extender-APIs mit besonderem Vertrag verwendet werden (Pathing, AIV, Gebäudeerzeugung, XAML-Patches, Lobby-Immediate-Properties), der Extender aktualisiert wird oder Extender-Verhalten diagnostiziert wird. Für ID-/Indexfragen [ids-enums.md](ids-enums.md), für Fehler im Extender [external-components.md](external-components.md). Gilt zusätzlich zur Root-`AGENTS.md`.

## Kanonischer Fork und Updates

- Einziger kanonischer lokaler Extender ist der Git-Fork `shcde-script-extender` im Workspace. Er wird nicht geändert und dient als Analysequelle. `origin` = eigener GitLab-Fork, `upstream` = Rawras Original. Lokale `build.bat`/`update.bat` sind über `.git/info/exclude` ausgeschlossen und werden nie hochgeladen.
- Updates ausschließlich mit der lokalen `update.bat` (Fetch/Merge); den Quellbaum nie per ZIP oder Robocopy überschreiben. Für vollständige Updates den inventarbasierten Treiber und die Regeln unter `Shared\Tools\ScriptExtenderUpdate` (eigene `AGENTS.md`) sowie `SEUpdate.md` verwenden.
- Die zu prüfende Extender-Version nie als feste Zielversion in Anweisungsdateien festhalten: vor Extender-Arbeit Version, Tag, Commit und Tree dynamisch aus installierter Assembly, lokalem Fork und Baseline-Provenienz bestimmen.
- Runtime-Mods kompilieren standardmäßig gegen die installierte `BepInEx\plugins\000shcdese\SHCDESE.dll`. Fehlt sie, einen alternativen `ExtenderDir` ausdrücklich angeben; lokale `bin`-/`mod_output`-Artefakte nicht automatisch bevorzugen.
- Bestehende kompatible Mods behalten ihre Mindestversion; nur erhöhen, wenn tatsächlich eine neuere API oder ein versionsgebundener Fix genutzt wird.
- Der Extender liest Informationen grundsätzlich direkt aus der nativen Bibliothek; was dort fehlt, wird normalerweise nicht unterstützt, selbst wenn es Unity-seitig rekonstruierbar wäre (seltene Ausnahmen).
- Dokumentation: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/main/docs · Reverse-Engineering-Hinweise: `shcde-script-extender\ReverseEngineering`.

## Aktuell bekannte Extender-Fehler

Ohne Versionsnummer führen. Bei jedem Extender-Update jeden Eintrag gegen den neuen Stand prüfen; neue Version oder fehlende Changelog-Erwähnung sind kein Behebungsnachweis. Erst entfernen, wenn Quellprüfung und bei Laufzeitfehlern ein Laufzeittest bzw. aktuelle Artefakte die Behebung bestätigen.

- **Fehlalarm-Minidump beim Start:** Der native Crash-Handler kann in der frühen verwalteten Start-/Hookphase einen ca. 47 MB großen Minidump für einen Null-Lesezugriff (`EXCEPTION_ACCESS_VIOLATION` auf `0x0`, Mono-Frames im Backtrace) erzeugen; Mono behandelt den Zugriff, das Spiel läuft normal. Bei Updates anhand neuer Dump-/Log-Zeitstempel prüfen; Hinweis nur entfernen, wenn ein kontrollierter Start mit aktivem Crash-Handler keinen passenden Dump erzeugt und die Quellprüfung nicht widerspricht.

## API-Verträge

- **XAML-Patches:** `<Content>` enthält exakt ein direktes Wurzelelement, weil `GameXAMLManagerAPI` nur `contentNode.Elements().FirstOrDefault()` verarbeitet und Geschwister stillschweigend verwirft. Mehrere Elemente auf getrennte Operationen oder einen Container verteilen. Vor jedem Build alle XAML-Patches des Mods maschinell prüfen (`Test-SharedBoundaries.ps1` prüft XAML-Wurzeln).
- **`LobbyModSettingsBaseViewModel.SetHostManagedImmediate<T>`:** nur für hostverwaltete, als String an XAML gebundene ganzzahlige `ManagedAssemblyImmediate<T>`-Properties. Vorher prüfen, ob eine direkte Immediate-Eigenschaft vorliegt; Runtime-, Override-, Apply- oder Restore-Logik nicht durch diesen UI-Helper ersetzen.
- **Pathing-Profile/Overrides pro Einheitentyp:** Live schreibbare Pathfinding-Profile, Verbindungsklassen sowie die globalen Overrides für die Next-Tile-Oberflächenprüfung und die Assassin-Auswahl wirken prozessweit auf den ganzen Einheitentyp, bauen vorhandene Pfade nicht neu auf und sind kein lokaler Befehls-, Spieler- oder Auswahlkontext.
  - Der Assassin-Override besitzt allein den Funktions-Detour auf RVA `0x196870`; kein konkurrierender Detour dort. Kontextgebundene Zusatzentscheidungen nur an vollständig auditierten Call-Sites mit dem vom Extender/Vanilla gelieferten Ergebnis verknüpfen; positive Ergebnisse bleiben autoritativ.
  - Der Next-Tile-Bypass hebt nur die Oberflächenablehnung auf, nicht übrige Übergangs-, Verbindungs- oder Zustandsprüfungen.
- **PCL/Pathing-Manager:** `GameTileManager.PathConnectionGrid` und `GamePathingManagerAPI.GetPathComponentGrid()` haben Vanillas Kapazität von 320.800 `ushort` (`0..320799`); Zugriffe trotzdem fail-closed gegen die Spanlänge prüfen und bei Extender-/Native-Update erneut auditieren. PCL-, Edge-, Connection-, Moat- und Packed-Plan-Zugriffe liegen in `GamePathingManagerAPI`; Gebäudeverknüpfungen über `TryGetPathConnectionRecordByBuildingId`. Routenabfragen: `FindNextComponentTowardDestination(playerId, currentComponentId, destinationComponentId, PathConnectionQueryMode)` in genau dieser Reihenfolge.
- **`AivSystem` (1.930.456 Byte):** nie als CLR-Wert konstruieren, kopieren, zurückgeben, übergeben, boxen, capturen, cachen oder speichern; nicht als Array-, Collection-, Nullable-, Delegate- oder generischen Payload. Nur über `AivSystem*`, `NativePointer<AivSystem>` oder Pointer-/Span-/`ref`-Views; `AsRef()` bleibt `ref`. Spezialisierte `GameAIVManagerAPI`-Zugriffe dem rohen Root-Pointer vorziehen.
- **GameBuilding:** Der Reverse-Engineering-Header kann vom kompilierten Interop-Layout abweichen.
- **Gebäudeerzeugung:** Direkt nach `GameBuildingManagerAPI.Create(...)` ist ein Gebäude meist `AliveState.NeedsInit` → bei unmittelbarer Spawn-Verifikation `NeedsInit` und `IsAlive` zulassen, bestehende Gebäude strikt auf `IsAlive` prüfen; den Zustand nie manuell auf `IsAlive` setzen. `Create(...)` erzeugt nur das rohe Kerngebäude ohne evtl. Tile-, Visual- und Interaktionsregistrierung; für spielartige Gebäude `CreatePrefab(...)` nutzen und die ID synchron über `BuildingR3EventHooks.OnBuildingSpawn(Post)` erfassen.
- **`GamePlayerManagerAPI.GetSelectedChimps()`** liefert `SelectedUnitInfo[]`; `UnitId` und bei Bedarf `UnitType` auswerten.
- **`GameTribeManagerAPI.UnassignUnit(tribeId, unitId)`** nur über den öffentlichen Wrapper mit modseitigen Vor- und Nachkontrollen; keine eigenen RVA-, Pattern-, Delegate- oder Pointeradapter.
- **Chore-/Netzwerk-API:** siehe [multiplayer-network.md](multiplayer-network.md).
