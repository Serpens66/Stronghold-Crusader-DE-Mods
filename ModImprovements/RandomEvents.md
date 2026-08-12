# Random Events

## 1. Mittel: GameMode-Capture läuft pro Tick und bei Treffer doppelt

### Beleg

- `RandomEvents/src/RandomEventsRuntime.cs:133-177` verarbeitet jeden `GameTimeManagerAPI.OnTick`.
- An Zeile 147 ruft `GameModeHelper.IsRealMultiplayer(...)` intern einen vollständigen `Capture` auf.
- Wenn das Ergebnis wahr ist, ruft Zeile 149 sofort ein zweites `GameModeHelper.Capture(...)` nur für die Diagnose auf.
- `GameModeHelper.Capture` liest mehrere Singleton-/Plattformzustände und iteriert Lobby-/Game-Member.

### Auswirkung

Ein im normalen Kartenlauf stabiler Modus wird unnötig in jedem Tick erneut rekonstruiert. Im Übergangsfall wird dieselbe Arbeit doppelt ausgeführt.

### Fixvorschlag

Den bereits in `InitializeCurrentMap` an Zeile 183 erfassten Snapshot als Kartenstatus speichern. Falls die defensive Erkennung eines nachträglich aktiven Netzwerks erhalten bleiben soll, höchstens periodisch erneut prüfen, zum Beispiel einmal pro Sekunde oder alle N Ticks. Entscheidung und Diagnosetext müssen denselben Snapshot verwenden.

### Abnahme

GameMode-Capture läuft beim Kartenstart und nur noch in der gewählten niedrigen Kontrollfrequenz. Ein echter Netzwerkmodus deaktiviert RandomEvents weiterhin sicher und wird mit genau dem entscheidenden Snapshot geloggt.

## 2. Mittel: eigene Lokalisierungsimplementierung statt des verbindlichen gemeinsamen Systems

### Beleg

- `RandomEventsSettingsViewModel` erbt korrekt von `Shared.PresetLobbyModSettingsViewModel`, überschreibt die Textauflösung aber mit `RandomEventsLocalization.Get`.
- `RandomEvents.csproj` kompiliert `src/RandomEventsLocalization.cs`, bindet `Shared/SerpLocalization.cs` jedoch nicht ein.
- `RandomEventsLocalization` dupliziert Dateiladen, Locale-Normalisierung, Fallbacks und stille Exceptionbehandlung.
- `LoadFile` verwendet `Replace("\\r\n", Environment.NewLine)`. In diesem C#-Literal ist der zweite Teil bereits ein echtes Newline; die übliche Locale-Schreibweise `\n` wird damit nicht wie im gemeinsamen Loader behandelt. Die aktuellen Locale-Dateien enthalten keinen solchen Wert, daher ist dies derzeit latent.

### Fixvorschlag

1. `Shared/SerpLocalization.cs` ins Projekt linken.
2. RandomEvents-Keys und englische Fallbacks dort ergänzen.
3. Alle `RandomEventsLocalization.Get`-Aufrufe durch `SerpLocalization.Get` ersetzen.
4. `RandomEventsLocalization.cs` und den Compile-Eintrag entfernen; keinen parallelen Fallback behalten.
5. Für formatierte Werte weiterhin `CultureInfo.CurrentCulture` beziehungsweise die vorhandene gemeinsame Replacement-Funktion passend verwenden.

### Abnahme

Locale-Key-Parität und XAML-Audit bestehen in allen Sprachen. Wechsel der Spielsprache lädt die richtige Datei. Fehlende Keys fallen auf Englisch zurück. Ein Testwert mit literalem `\n` wird korrekt umgebrochen.

## 3. Niedrig: ungenutzte Konstante

`RandomEvents/src/ScenarioSignpostRegistry.cs:16` definiert `ReferenceSignpostIdsOffset`, ohne sie zu lesen. Entweder die Konstante in die beabsichtigte hashgebundene Layoutvalidierung einbauen oder entfernen. Wenn sie nur Dokumentation ist, gehört sie mit Hash/RVA und Bedeutung in `UpdateToNewDLL.md`, nicht als ungenutztes Codeelement.

## 4. Niedrig: Cleanup-Kommentar und tatsächlicher Lifecycle passen nicht zusammen

`RandomEventsPlugin.cs:54-61` bezeichnet `OnApplicationQuit` als sicheren Cleanup-Punkt, obwohl die Plugin-Komponente beim Start zerstört wird. Der dauerhafte Runtime-Pfad darf davon nicht abhängen. Den unerreichbaren Cleanup entfernen oder an einen wirklich persistenten Besitzer verlagern.
