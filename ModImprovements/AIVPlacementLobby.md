# AIV Placement Lobby

## 1. Mittel: vollständiger Lobby-Capture und Fingerprint in jedem Frontend-Frame

### Beleg

- `AIVPlacementLobby/src/AIVPlacementLobbyRuntime.cs:104-120` hängt an `FRONT_Multiplayer.Update` und ruft bei aktivem Lobbykontext in jedem Frame `CaptureIfChanged` auf.
- In `CaptureIfChanged` werden an Zeile 176 zuerst `Capture(frontend)` und an Zeile 177 ein kompletter Fingerprint gebaut.
- Erst danach prüft Zeile 183 `nextSourcePollTimestamp`. Die vorhandene 500-ms-Grenze verhindert somit nicht den Capture und die Fingerprint-Allokationen.
- `Capture` ab Zeile 225 erzeugt Listen, Dictionaries, HashSets, Kandidatenabbilder und Spielerzuordnungen. Der Fingerprint erzeugt zusätzlich einen neuen String.

### Auswirkung

Auch bei unveränderter Lobby entstehen pro Frame kurzlebige Objektgraphen und Strings. Der teure Teil liegt vor der vermeintlichen Drosselung. Das erhöht Frontend-GC und CPU-Last, besonders mit vielen AIV-Kandidaten.

### Fixvorschlag

1. Eine billige Dirty-Erkennung vor den vollständigen Capture setzen.
2. Vorzugsweise konkrete Lobby-Mutationen markieren: Karte, Startplätze, Lord/AIV-Auswahl, Rotation, Pre-Build und Host-/Memberwechsel.
3. Als Sicherheitsnetz einen niedrig frequentierten Poll behalten, beispielsweise alle 100 bis 500 ms. Dieser Poll darf erst dann den vollständigen Snapshot und Fingerprint bauen.
4. Den erzwungenen Capture direkt vor `StartSkirmishGame` unverändert beibehalten; dort ist Aktualität wichtiger als Allokationsfreiheit.
5. Collection-Wiederverwendung nur auf dem bestätigten Main Thread vornehmen und niemals mutierbare Capture-Daten an Worker weiterreichen.

### Abnahme

- In einer unveränderten Lobby wird `Capture` nicht mehr mit der Frontend-Framerate aufgerufen.
- Änderungen an Karte, Lord, AIV, Rotation und Startplatz erscheinen ohne störende Verzögerung.
- Der erzwungene Start-Capture verhindert weiterhin einen Start mit veraltetem Ergebnis.
- Alle vorhandenen 131 AIV-/Parser-/Map-Tests bleiben grün; zusätzlich einen Zähler- oder Allocation-Test für eine unveränderte Lobby ergänzen.

## 2. Niedrig bis mittel: veraltete Auswertungen werden verworfen, aber nicht abgebrochen

### Beleg

`AIVPlacementLobbyRuntime.cs:354-397` startet `EvaluateBatchAsync`. Bei einer neuen Generation verhindert `LobbyRequestGenerationGate` zwar die Veröffentlichung alter Ergebnisse (`AIVPlacementLobbyRuntime.cs:431-450`), die alte Auswertung rechnet aber bis zum Ende weiter. Beim Verlassen der Lobby wird nur die Generation erhöht und die Ergebnisqueue geleert (`AIVPlacementLobbyRuntime.cs:663-674`).

### Auswirkung

Schnelle Lobbyänderungen können mehrere teure, bereits irrelevante AIV-Auswertungen parallel weiterlaufen lassen. Das verschwendet CPU und kann die aktuelle Generation verzögern.

### Fixvorschlag

- Pro Generation eine `CancellationTokenSource` halten und beim nächsten Capture sowie beim Verlassen des Lobbykontexts abbrechen.
- Cancellation durch `EvaluateBatchAsync` und die inneren Kandidaten-/Rotationsschleifen führen.
- Abbruch nicht als Fehler cachen oder als UI-Fehler veröffentlichen.

### Abnahme

Ein Test startet Generation A, erzeugt sofort Generation B und beweist, dass A abbricht, B veröffentlicht wird und kein Abbruch als Fehlerzustand erscheint.
