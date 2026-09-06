# Updateplan: SHCDE Script Extender 2.0.2 -> 2.2.0

Dieser Plan migriert alle gegen Script Extender 2.0.2 geprüften Workspace-Mods auf den finalen Release `v2.2.0` (`10d28f717d38166e5875c666f20fc5653ae44b0c`). Bereits vorhandene 2.2.0-Metadaten gelten erst nach Quell-, Build- und Vertragstest als bestätigt.

## Arbeitspakete

1. Finalen Extender über `update.bat` und `build.bat` reproduzieren, installieren und per Version/Hash als einzige Referenzbasis festlegen.
2. Runtime-Plugins, Projekte, Manifeste, Lua sowie gemeinsame Quellen neu inventarisieren und aktive 2.0.2-Verträge klassifizieren.
3. Den in 2.2.0 korrigierten `GameTribeManagerAPI.UnassignUnit`-Wrapper in AIDefense und QueueTest verwenden und die versionsgebundenen direkten Adapter entfernen.
4. Native Hooks, Layouts, ID-Verträge, Runtime-Lifecycle und Paketgrenzen gegen 2.2.0 prüfen; neue C#-Gameplayfeatures bleiben außerhalb des Scopes.
5. Einen rein lesenden Lua-Smoke für `eGameTypeModes` und `Player_GetCurrentGameTypeMode` ergänzen.
6. BugfixesAndQoL nach Abschluss der laufenden MoveMoat-Verlagerung zuletzt inventarisieren; den Horse-Demand-Fix auf das typisierte 2.2.0-Feld umstellen.
7. Die semantische Native-Baseline bei unverändertem Spielhash über `Knowledge`, `GhidraExports`, `Index`, `Validate` auf den 2.2.0-Extender-Commit aktualisieren.
8. AGENTS.md um die bestätigten 2.2.0-Verträge ergänzen, sämtliche Projekte testen und erst danach die jeweiligen `build.bat`-Treiber ausführen.

## Festgelegte Entscheidungen

- `Shared/GameModeHelper.cs` bleibt maßgeblich; die neue Game-Type-API wird in C# nicht verwendet.
- Neue Player-/AI-Felder erzeugen keine neuen Funktionen. Nur bestehender Code wird sinnvoll typisiert.
- Der Lua-Test beobachtet ausschließlich und verändert keinen Spielzustand.
- `MaxNativeCrashDumpFiles` bleibt beim Extender-Standardwert 5.
- Modversionen und README-Dateien bleiben unverändert.

## Abnahme

- Alle erfassten Projekte bauen und alle Tests bestehen gegen dieselbe validierte 2.2.0-Assembly.
- Keine aktive 2.0.2-Annahme, kein versionsgebundener direkter Tribe-Remove-Adapter und keine privat paketierte Extender-Abhängigkeit bleibt zurück.
- Native Hashbindung, semantische Baseline, Manifeste, CRLF und installierte Artefakte sind konsistent.
- Historische 2.0.2-Nennungen bleiben nur dort erhalten, wo sie ausdrücklich als Historie gekennzeichnet sind.
