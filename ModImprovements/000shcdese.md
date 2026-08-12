# 000shcdese / Script Extender

## 1. Hoch: `UnityMainThreadDispatcher` besitzt keinen nachweislich dauerhaften Frame-Host

### Beleg

- `shcde-script-extender/src/SHCDESE.BepInEx/Bootstrap/Plugin.cs:82` erzwingt die Dispatcher-Instanz bereits im frühen Plugin-`Awake`.
- `shcde-script-extender/src/SHCDESE.BepInEx/API/UnityMainThreadDispatcher.cs:39-44` erstellt ein eigenes `GameObject` und markiert es mit `DontDestroyOnLoad`.
- Die Queue wird ausschließlich in `Update()` an `UnityMainThreadDispatcher.cs:70-88` geleert.
- `RandomEvents/NativeEventNotes.md:58` hält einen reproduzierten Befund fest: ein früh erstelltes `DontDestroyOnLoad`-Objekt bekam keine `LateUpdate`-Callbacks; auch eine über diesen Dispatcher gestartete Coroutine führte weder in RandomEvents noch SpawnCastle ihren ersten Callback aus.
- Der Dispatcher wird von zahlreichen Lua- und Manager-APIs konsumiert. Ein stillstehender Dispatcher betrifft daher nicht nur SpawnCastle.

### Auswirkung

Von Hintergrundthreads eingereihte Aktionen können dauerhaft in der Queue bleiben. Noch kritischer ist `EnqueueAndWait<T>()`: `UnityMainThreadDispatcher.cs:211` wartet ohne Timeout mit `WaitOne()`. Wenn `Update()` nicht läuft, blockiert der aufrufende Thread unbegrenzt.

`Dispatch(Action)` führt die Aktion an `UnityMainThreadDispatcher.cs:100-113` sofort aus, wenn noch keine Instanz existiert. Bei einem Hintergrundaufrufer verletzt dieser Fallback gerade die zugesicherte Main-Thread-Semantik und kann Unity-Aufrufe vom falschen Thread ausführen.

### Fixvorschlag

1. Zuerst einen im Spiel nachweislich wiederkehrenden, prozessweiten Script-Extender-/Native-Hook als Queue-Pumpe bestimmen. Kein weiteres früh erzeugtes `DontDestroyOnLoad`-Objekt als unbelegten Ersatz einführen.
2. Dispatcher-Initialisierung und Queue-Pumpe trennen. Die Queue darf bereits existieren, aber erst ein validierter Main-Thread-Takt verarbeitet sie.
3. Einen Gesundheitszustand pflegen: Main-Thread-ID, letzter Pump-Zeitpunkt, Anzahl offener Aktionen und einmalige Meldung, dass der erste echte Pump-Callback gelaufen ist.
4. `Dispatch` darf bei fehlendem Dispatcher auf einem Hintergrundthread nicht inline ausführen. Entweder sauber fehlschlagen oder bis zur Initialisierung puffern; die gewählte Semantik in der API dokumentieren.
5. `EnqueueAndWait` um begrenztes Timeout und möglichst Cancellation erweitern. Ein Timeout muss eine klare Exception/Fehlermeldung erzeugen und darf nicht still `default` als reguläres Ergebnis vortäuschen.
6. Falls `StartCoroutine` weiterhin Teil der öffentlichen Dispatcher-Funktion bleibt, muss deren Fortschritt ebenfalls über einen echten Integrationstest bewiesen werden.

### Abnahme

- Eine Aktion von einem Hintergrundthread wird nachweislich auf der Main-Thread-ID ausgeführt.
- Queue-Fortschritt bleibt über Frontend, Kartenstart, Kartenentladen und den nächsten Kartenstart erhalten.
- Ein absichtlich stillgelegter Pump führt bei `EnqueueAndWait` innerhalb des festgelegten Timeouts zu einem diagnostizierten Fehler statt Deadlock.
- Keine Background-Aktion wird wegen fehlender Instanz inline als vermeintliche Main-Thread-Aktion ausgeführt.
- SpawnCastle benötigt danach keinen eigenen Dispatcher-Coroutine-Fallback mehr.

## 2. Niedrig: wiederholte erwartbare Netzwerk-ID-Warnungen fluten das Log

### Beleg

Im letzten Logabschnitt stehen 60 identische Meldungen:

`[GameNetworkAPI] [GetLocalPlayerId] EditorDirector returned invalid ID -1 outside lobby phase`

Die Meldung wird an `shcde-script-extender/src/SHCDESE.BepInEx/API/GameNetworkAPI.cs:826-831` bei jedem ungültigen Aufruf erneut als Warning geschrieben. Mindestens BugfixesAndQoL ruft die Methode hochfrequent aus Property-Gettern auf; SpawnCastle besitzt zwei weitere direkte Aufrufstellen.

### Fixvorschlag

- Zuerst die falschen beziehungsweise unnötig häufigen Mod-Aufrufer korrigieren.
- Zusätzlich die API-Diagnose auf Zustandswechsel, einmal pro Phase oder zeitlich gedrosselt begrenzen.
- Eine `TryGetLocalPlayerId(out int)`-Variante ohne erwartbare Warning anbieten. Call-Sites, für die „noch nicht verfügbar“ normal ist, sollen diese verwenden.

### Abnahme

Ein normaler Start, Lobbywechsel und Kartenlauf erzeugt keine Serie identischer Warnungen. Ein unerwartet ungültiger Zustand bleibt trotzdem einmal nachvollziehbar diagnostiziert.
