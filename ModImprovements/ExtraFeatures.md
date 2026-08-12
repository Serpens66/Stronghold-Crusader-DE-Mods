# Extra Features

## 1. Mittel: Paketversand verwendet `IsNetworkedEnvironment()` als alleiniges Multiplayer-Signal

### Beleg

- `ExtraFeatures/src/KnightDismountRuntime.cs:852` und `:882` entscheiden damit über Dismount-/Mount-Pakete.
- `ExtraFeatures/src/QuarryPileRelocationRuntime.cs:499` verwendet dieselbe Entscheidung.
- Ein lokaler Skirmish kann wegen seiner lokalen `gameMembers`-Struktur ebenfalls als „networked environment“ erscheinen. Das Signal unterscheidet daher keinen echten Multiplayer von lokalem Skirmish.
- `ExtraFeatures.csproj` bindet `Shared/GameModeHelper.cs` bereits ein; die korrekte gemeinsame Erkennung ist verfügbar.

### Auswirkung

In Singleplayer können unnötige Netzwerkpakete beziehungsweise Loopback-Pfade ausgeführt werden. Bei zustandsverändernden Operationen ist das zusätzlich ein Risiko für doppelte Verarbeitung oder irreführende Diagnose.

### Fixvorschlag

Beim Kartenstart `GameModeHelper.Capture(...)` verwenden und den validierten `IsRealMultiplayer`-Zustand für den Kartenlauf speichern. Nur in echtem Multiplayer senden. Nicht in jeder Buttonaktion erneut alle GameMode-Quellen erfassen.

### Abnahme

- Singleplayer-Skirmish und Trail verändern den Zustand genau einmal lokal und senden kein Paket.
- Echter Host und Client senden/verarbeiten weiterhin genau ein stabiles, explizit formatiertes MessagePack-Paket.
- Logs nennen den gespeicherten GameMode-Snapshot beim Kartenstart.

## 2. Mittel: Knight-Request-Deduplizierung wächst über Karten hinweg

### Beleg

- `KnightDismountRuntime.cs:312` hält `Dictionary<int, HashSet<int>> processedRequestIds` und fügt an `:1016-1027` jede empfangene Request-ID dauerhaft hinzu.
- `nextRequestId` wächst an `:690` und `:737` weiter.
- Beide Zustände werden nur in `Dispose()` an `:370-384` teilweise zurückgesetzt; `nextRequestId` wird dort nicht zurückgesetzt.
- `ExtraFeaturesRuntime.OnUnloadMap` an `ExtraFeaturesRuntime.cs:541-547` setzt den Knight-Zustand nicht zurück.
- Der vergleichbare Quarry-Pfad abonniert dagegen `OnUnloadMap(Pre)` und leert seine Map-Zustände einschließlich `nextRequestId` in `QuarryPileRelocationRuntime.cs:1556-1566`.

### Auswirkung

Der HashSet-Speicher wächst für die gesamte Prozesslaufzeit mit jeder Knight-Aktion. Ein bloßes Leeren beim Kartenwechsel ohne Sitzungskennung könnte umgekehrt verspätete Pakete einer alten Karte als neue Aktion akzeptieren.

### Fixvorschlag

1. Einen expliziten `ResetMapState` für Knight ergänzen und aus demselben Karten-Unload-Lifecycle wie Quarry aufrufen.
2. Deduplizierung begrenzen: pro Spieler eine höchste zusammenhängende Sequenz plus kleines Fenster für Reordering oder eine begrenzte LRU-Struktur verwenden.
3. Wenn Protokolländerungen möglich sind, eine Map-/Session-ID in beide Pakete aufnehmen. Damit können verspätete Pakete der vorherigen Karte sicher verworfen werden.
4. `nextRequestId` nur zusammen mit einer neuen Session-ID zurücksetzen.

### Abnahme

- Viele Aktionen lassen den Dedupe-Speicher nicht unbegrenzt wachsen.
- Doppelte und leicht umsortierte Pakete werden weiter korrekt erkannt.
- Ein verspätetes Paket einer entladenen Karte wird auf der Folgekarte verworfen.

## 3. Niedrig: Quit-Cleanup liegt auf der früh zerstörten Plugin-Komponente

`ExtraFeaturesPlugin.cs:47-61` versucht nur in `OnApplicationQuit` aufzuräumen. Dieser Callback ist auf der beim Start zerstörten BepInEx-Komponente nicht verlässlich erreichbar. Entweder den Prozesslebenszyklus ohne künstlichen Cleanup dokumentieren oder einen nachweislich persistenten Besitzer verwenden. Keinesfalls Runtime-Hooks im frühen `OnDestroy` lösen.
