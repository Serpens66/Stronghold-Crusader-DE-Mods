# Übergabe: Modsettings-Warnung bei automatischer Save-Wiederherstellung

Stand: 14.10.2026. Auftrag: Ursache prüfen und gezielt beheben. Diese Notiz dokumentiert einen Quellbefund und passende Logbelege; es wurde noch keine Korrektur implementiert.

## Beobachtung

Der Nutzer hat beim betreffenden Lauf keine Einstellungen geändert. Trotzdem meldet der Extender beim Laden von `test_gates_around.sav`:

```text
[Warning: SHCDE-SE] [T1-15:33:42.322] [GameXAMLManagerAPI] [RegisterLobbyModSettingsCore] [BugfixesAndQoL_Serp]: unauthorised local change to host-only property [EnableImprovedAssassinPathfinding]; not broadcast, not persisted
```

Quelle: `E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/LogOutput.log`, bei Untersuchung Zeile 3079. Die Datei kann inzwischen weitere Starts enthalten; anhand Zeit, Save und Kartenstart zuordnen.

Unmittelbar anschließend um 15:33:42.324 meldet BugfixesAndQoL `Entered read-only mission preset.` Um 15:33:42.346–347 erscheinen gleichartige Warnungen für StartConditions: `SetStartGoldHuman`, `StartGoodsAI`, `StartGoodsHuman`. Das ist deshalb nicht ausschließlich ein Assassin- oder Torpolicythema.

Im betreffenden Prozess geladen: APIShared 0.9.0, BugfixesAndQoL 1.0.191, Fixes 1.26.4.0. Installierte Assemblies und aktuelle Quellen vor einer Korrektur erneut abgleichen, da parallel weiterentwickelt wird.

## Geprüfter Quellpfad

Alle Pfade relativ zum Workspace:

1. `APIShared/src/Savegames/SavegameModSettings.cs:266`, `ApplyLoadedSettings`: wählt den wiederherzustellenden Snapshot und ruft um Zeile 319 `System_EnterMissionPreset(snapshot, "Savegame", false)` auf. Ein weiterer Einstieg steht um Zeile 219; bei der Untersuchung beide berücksichtigen.
2. `APIShared/src/ModSettings/Presets/PresetLobbyModSettingsViewModel.cs`, `System_EnterMissionPreset`: setzt den Missionskontext und delegiert an den Presetcontroller.
3. `APIShared/src/ModSettings/Presets/LobbyPresetController.Mission.cs`, `EnterMissionPreset`: ruft `ApplySnapshot(..., writeLocalStorage: false)` auf und schreibt anschließend die oben sichtbare Missionspreset-Meldung.
4. `APIShared/src/ModSettings/Presets/LobbyPresetController.Snapshots.cs:195`: setzt `applying = true`; beim reflektierten Propertypfad führt `TryApplyProperty` (Zeile 261) `property.SetValue(owner.SettingsTarget, value)` aus. In diesem Pfad ist kein expliziter Extender-Change-Origin-Scope erkennbar. Dynamische/Application-Backend-Pfade existieren daneben und müssen gesondert bewertet werden.
5. `PresetLobbyModSettingsViewModel.cs:688`: `CanMutateSetting` erlaubt Änderungen während `IsApplyingSnapshot`, unabhängig von der normalen UI-Sperre.
6. `BugfixesAndQoL/src/BugfixesAndQoLViewModel.cs:1662`: `SetSetting` übernimmt zunächst `field = value`, ruft `SettingChanged` und danach `OnPropertyChanged` auf.
7. `shcde-script-extender/src/SHCDESE.BepInEx/API/GameXAMLManagerAPI.cs`, Handler in `RegisterLobbyModSettingsCore`: ignoriert unter anderem `InitialLoad`, `IncomingNetwork` und UI-Revert. Sonst prüft er die lokale Autorität und schreibt bei Ablehnung um Zeile 284 die Warnung. `IsLocallyAuthoritative` um Zeile 388 verwendet `IsNetworkedEnvironment()` und `IsLocalHost()`.

## Gesichert und noch offen

- Die Warnung setzt keine manuelle Bedienung voraus. Der automatische Snapshotpfad kann dieselbe PropertyChanged-Meldung erzeugen; die Logreihenfolge passt genau zu diesem Ablauf.
- Der Extender verweigert an dieser Stelle Broadcast und Speicherung. Er setzt im gezeigten Handler nicht den bereits übernommenen Propertywert zurück. Die Aussage „die Einstellungsänderung wurde vollständig zurückgewiesen“ wäre daher falsch.
- APIShareds internes `applying` und `writeLocalStorage: false` markieren die Meldung nicht automatisch als autorisierte Wiederherstellung gegenüber dem separaten Extender-Handler.
- Ein synchroner Stacktrace des konkreten Warnungsereignisses fehlt. Quellpfad und zeitliche Einordnung sind starke Hinweise, aber noch kein vollständiger Laufzeitnachweis des Aufrufers.
- Alte/neue Werte, effektiver Assassin-Wert nach dem Laden und genaue Snapshotherkunft wurden durch diese Warnung nicht protokolliert. Nicht unterstellen, ob die Verbesserung ein- oder ausgeschaltet wurde.
- Ein benachbarter Modbericht klassifiziert den Lauf als Einzelspieler, zugleich mit `lowLevelNetworked=True`. Die genaue Autoritätsbewertung im Zeitpunkt der Warnung bleibt zu prüfen; Einzelspieler nicht ungeprüft mit „nicht vernetzt“ gleichsetzen.
- Kein Nachweis einer Multiplayer-Desynchronisation oder einer Lagursache. Keine pauschale Warnungsunterdrückung daraus ableiten.

## Nächste technische Schritte

1. Aktuellen installierten Extender-/APIShared-Vertrag samt beiden Save-Einstiegen prüfen. Snapshotquelle, tatsächlichen Change-Origin und Autorität während der Wiederherstellung nachvollziehen.
2. Den echten reflektierten Setter-/PropertyChanged-Pfad in einem kontrollierten Test ausführen: Wert vorher/nachher, Runtime-Benachrichtigung, Broadcast und Persistenz getrennt erfassen. Keine zusätzliche Spielrunde allein für diese technisch reproduzierbare Frage verlangen.
3. Eine eng begrenzte Lösung für automatische Missionseinstellungen wählen. Bestehende öffentliche Origin-/Restore-Möglichkeiten zunächst prüfen. Nicht pauschal alle Änderungen als IncomingNetwork oder InitialLoad kennzeichnen: Beim echten Multiplayer-Host kann die vorgesehene Synchronisation weiterhin erforderlich sein.
4. Singleplayer-Saveload, Multiplayer-Host/Client, eingehenden Sync, unerlaubte Clientbedienung, unveränderte Werte, Missionseintritt/-ende, verschachtelte Anwendung und Ausnahmen prüfen. Fremde persönliche Einstellungen dürfen nicht versehentlich gespeichert oder überschrieben werden.
5. Automatische Wiederherstellung soll den vorgesehenen effektiven Wert liefern, ohne irreführende lokale Änderungswarnung. Unzulässige echte Clientänderungen müssen weiterhin blockiert bleiben. Laufzeitreaktionen auf legitime Änderungen erhalten.
6. Vor Runtimeänderungen die AGENTS-Hard-Gates durchführen. Den kanonischen Extender-Fork nicht ändern; falls dort eine Korrektur nötig ist, einen kurzen englischen Autorenreport erstellen. Keine fremden parallelen Änderungen überschreiben.

## Bezug zur Torhausmigration

Der Nutzer bestätigt für den integrierten Torfix im Lauf 15:33:42–15:35:49 den langen Außenweg. Logs: 1.274 KI-Suchen, 491 erfolgreiche KI-Suchen mit verworfenen Torkanten, 2.288 verworfene Kantenprüfungen, keine negativen KI-Suchen, keine fehlenden erforderlichen Masken oder Scope-/Snapshot-/Ausnahmefehler. Diesen bestandenen Vergleich nicht wegen der Einstellungswarnung wieder öffnen.

Vor dem noch ausstehenden Assassin-Migrationsvergleich müssen die tatsächlich wirksamen Einstellungen zuverlässig feststehen. Die zuvor vorbereitete lokale Einstellung allein ist bei automatischen Save-/Missionspresets kein Nachweis des späteren Runtimewerts. Torpolicy und Savegames für diese Untersuchung unverändert lassen.
