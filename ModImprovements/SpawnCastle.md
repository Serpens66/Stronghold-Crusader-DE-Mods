# Spawn Castle

## 1. Mittel: zwei nachweislich nicht laufende Frame-Fallbacks bleiben aktiv und werden als erfolgreich geloggt

### Beleg

- `SpawnCastle/src/BlueprintRuntimeController.cs:101-103` registriert den nachweislich funktionierenden statischen `Application.onBeforeRender`-Hook.
- Zeile 104 behauptet, der Script-Extender-Dispatcher überlebe den frühen BepInEx-Lifecycle; Zeile 106 startet dort `RunFrameLoop()` und Zeile 109 loggt den Start als persistent.
- `RunFrameLoop` steht an Zeile 126-148. Ein zusätzlicher `MonoBehaviour.Update`-Fallback steht an Zeile 150-161 und wird über `componentUpdateObserved` an Zeile 48 diagnostiziert.
- `RandomEvents/NativeEventNotes.md:58` dokumentiert dagegen den Laufzeittest: weder ein früh erzeugtes `DontDestroyOnLoad`-Objekt noch die Dispatcher-Coroutine führte ihren ersten Callback aus. Dieser Befund wurde ausdrücklich auch mit SpawnCastle beobachtet.

### Auswirkung

Die Blueprint-Funktion arbeitet wegen `onBeforeRender`, aber der Code enthält zwei tote Ausführungspfade, irreführende Kommentare und eine falsche Erfolgslogmeldung. Das erschwert Fehlerdiagnose und bindet SpawnCastle unnötig an den problematischen Dispatcher.

### Fixvorschlag

- `UnityMainThreadDispatcher.Instance.StartCoroutine(...)`, `RunFrameLoop`, `Update` und `componentUpdateObserved` entfernen.
- Den nicht mehr benötigten `System.Collections`-Import entfernen, sofern keine andere Nutzung verbleibt.
- Initialisierungslog nur auf den tatsächlich registrierten `Application.onBeforeRender`-Hook beziehen.
- Den vorhandenen Einmalmarker `beforeRenderCallbackObserved` behalten; er belegt den ersten echten Callback.
- Erst wenn der Script-Extender-Dispatcher separat repariert und integriert getestet ist, darf er als bewusster alternativer Host neu bewertet werden. SpawnCastle benötigt ihn für den funktionierenden Pfad derzeit nicht.

### Abnahme

Blueprint-Hotkey, Projektion, HUD und View-Settle funktionieren in Singleplayer und als lokaler Client weiterhin. Das Log enthält den bestätigten ersten `onBeforeRender`-Callback und keine Behauptung über eine nicht laufende Dispatcher-Coroutine.

## 2. Mittel: eigene Lokalisierungsimplementierung statt `Shared/SerpLocalization`

### Beleg

- `SpawnCastleSettingsViewModel` erbt vom gemeinsamen Preset-ViewModel, löst Texte aber über `SpawnCastleLocalization` auf.
- `BlueprintHudViewModel` verwendet dieselbe private Implementierung.
- `SpawnCastle.csproj:164` kompiliert `SpawnCastleLocalization.cs`; `Shared/SerpLocalization.cs` ist nicht eingebunden.
- Damit werden Dateiladen, Fallbacks, Locale-Normalisierung und stille Catches parallel gepflegt. Der lokale Loader ersetzt nur literal `\r\n`, während das gemeinsame Format `\n` unterstützt.

### Fixvorschlag

SpawnCastle- und HUD-Keys samt Fallbacks in `Shared/SerpLocalization.cs` aufnehmen, die gemeinsame Datei linken, alle Aufrufer migrieren und `SpawnCastleLocalization.cs` vollständig entfernen. Keinen alten Fallback parallel behalten.

### Abnahme

Modsettings- und HUD-Texte funktionieren in allen Locale-Dateien; Locale-Parität, XAML-Audit und CRLF-Prüfung bestehen.

## 3. Niedrig: direkte Netzwerk-Spieler-ID-Abfragen außerhalb eindeutiger Lobbyphasen

`SpawnCastleRuntime.cs:301` und `:927` rufen `GameNetworkAPI.GetLocalPlayerId()` direkt auf. Die API warnt, wenn `EditorDirector` außerhalb der Lobby keine ID liefert. Für GameMode-Diagnose und lokale Spielerbestimmung sollte der bereits erfasste gemeinsame Snapshot beziehungsweise die native lokale Spieler-ID im Kartenlauf verwendet werden. Erwartbar nicht verfügbare Werte dürfen keine wiederholte Warning erzeugen.
