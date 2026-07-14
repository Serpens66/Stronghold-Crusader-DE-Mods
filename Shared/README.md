# Shared-Hilfsfunktionen

Die Dateien in diesem Ordner werden als verlinkte C#-Quelldateien in die jeweiligen Mod-Projekte aufgenommen. Dadurch entsteht keine zusätzliche Shared-DLL, die separat installiert oder geladen werden müsste.

## Deterministische Multiplayer-Commands

`DeterministicMultiplayerCommandBus.cs` ist für Aktionen gedacht, die aus einer Mod-UI kommen und den synchronisierten Spielzustand verändern. Ein Netzwerk-Callback führt dabei niemals direkt Spiellogik aus.

Der Ablauf ist:

1. Der auslösende Client sendet nur einen Command-Request an den Host.
2. Der Host vergibt eine globale Sequenznummer und sendet `Prepare` an alle Clients.
3. Erst nachdem alle verbundenen menschlichen Spieler den Command bestätigt haben, sendet der Host `Commit` mit einem zukünftigen Spiel-Tick.
4. Alle Instanzen führen Commands für diesen Tick in identischer Sequenz im `StrongholdFrameProvider.OnGameTick`-Kontext aus.
5. Verspätete Commands werden nicht nachträglich ausgeführt. Der Host gibt die autoritative Map-Generation vor; Clients übernehmen sie beim `Prepare`, während der Host lokale Client-Zähler in Requests ignoriert.

Jeder nutzende Mod benötigt:

- einen pro Mod eindeutigen `scopeId`, normalerweise die BepInEx-Plugin-GUID;
- pro Aktion einen eindeutigen Channel-Namen;
- einen Command-Typ mit stabilen numerischen `[Key(...)]`-Werten;
- einen expliziten `IMessagePackFormatter<TCommand>`;
- eine rein strukturelle Validierungsfunktion;
- eine Ausführungsfunktion, die ausschließlich deterministische Spielzustandsänderungen enthält.

Minimale Einbindung in eine klassische `.csproj`:

    <Compile Include="..\Shared\DeterministicMultiplayerCommandBus.cs">
      <Link>Shared\DeterministicMultiplayerCommandBus.cs</Link>
    </Compile>

Zusätzlich werden dieselben Referenzen wie bei der Network API benötigt: `SHCDESE`, `R3`, `MessagePack`, `MessagePack.Annotations` und `System.Memory`.

Typische Initialisierung:

    commandBus = new Shared.DeterministicMultiplayerCommandBus(log, PluginGuid);
    commandBus.RegisterChannel<MyCommand>(
        "my-command",
        ValidateMyCommand,
        ExecuteMyCommand);
    commandBus.Initialize();

Einreichen aus dem UI-Callback:

    int requestId = commandBus.ReserveRequestId();
    commandBus.Submit("my-command", requestId, new MyCommand { /* Daten */ });

Die Ausführungsfunktion erhält einen `DeterministicCommandContext` mit Absender, Request-ID, Host-Sequenz und Ausführungs-Tick. UI, Audio und andere nichtdeterministische Unity-Arbeit sollte sie über `UnityMainThreadDispatcher.EnqueueStatic(...)` auslagern. Die Simulationsänderung selbst muss synchron im Callback bleiben.

Für die Fehlersuche kann jeder Peer an definierten Checkpoints einen stabil aufgebauten Zustandsfingerprint melden:

    commandBus.ReportStateFingerprint(context, "after-1", fingerprint);

Im Multiplayer sendet der Bus Client-Fingerprints an den Host. Sobald alle aktiven menschlichen Spieler gemeldet haben, schreibt der Host automatisch `MATCH` oder `MISMATCH` einschließlich der beteiligten Fingerprints ins Log. Im Singleplayer wird der lokale Fingerprint nur protokolliert. Fingerprints dürfen höchstens 16 KiB groß sein und dürfen keine lokalen Zeiger, Objekt-Hashcodes oder andere nichtdeterministische Werte enthalten.

Der standardmäßige Ausführungsvorlauf beträgt 16 Simulationsticks. Mods mit besonderen Latenzanforderungen können im Konstruktor einen anderen Wert übergeben.

`Dispose()` ist nur beim tatsächlichen Prozessende oder beim bewussten vollständigen Deaktivieren des Mods aufzurufen, nicht aus `BaseUnityPlugin.OnDestroy()`.

## Nativer Multiplayer-Zustand

`MultiplayerStateDiagnostics.cs` beobachtet die nativen Spielzustände für Resynchronisierung, Abschnitt/Layer, Verbindungspause und den Verbindungszustand der Spieler. Es protokolliert nur Änderungen und verändert selbst keinen Spielzustand.

Einbindung:

    <Compile Include="..\Shared\MultiplayerStateDiagnostics.cs">
      <Link>Shared\MultiplayerStateDiagnostics.cs</Link>
    </Compile>

Initialisierung:

    multiplayerDiagnostics = new Shared.MultiplayerStateDiagnostics(log, PluginGuid);
    multiplayerDiagnostics.Initialize();

## Logging und Lokalisierung

- `DebugLogHelper.cs` versieht Logeinträge zentral mit einem Zeitstempel inklusive Millisekunden.
- `SerpLocalization.cs` kapselt die gemeinsame Locale-Erkennung und Textauflösung.
