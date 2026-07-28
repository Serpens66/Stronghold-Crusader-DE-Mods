# SHCDE Chore-System: native Analyse

Stand: 2026-07-28

## Kurzfazit

Das Chore-System ist ein klassisches deterministisches Lockstep-Command-System. Ein Chore wird nicht
sofort ausgeführt, sondern als Command mit einer zukünftigen Simulations-Ticknummer in eine gemeinsame
Pending-Queue gestellt. Derselbe Command wird an die anderen Spieler übertragen. Host-seitige
`SyncEvent`-Chores listen zusätzlich konkrete Command-IDs auf; ein Client läuft an der zugehörigen
Barriere erst weiter, wenn diese Commands lokal angekommen sind. Nach drei Sekunden gibt es einen
nativen Forced-Run-Fallback.

Damit ist auch erklärt, warum ein Script-Extender-Custom-Packet trotz zuverlässigem Steam-Transport
nicht dieselbe Synchronisationsgarantie hat: Es besitzt weder einen nativen Chore-Slot mit
Ausführungs-Tick und Command-ID noch wird es in die Host-Barrieren aufgenommen.

Für eigene deterministische Multiplayer-Befehle ist ein nativer Erweiterungspunkt technisch
realistisch:

1. einen für die exakte Spielversion nachweislich unbenutzten Chore-Opcode registrieren,
2. dessen Handler-Tabelleneintrag auf einen persistenten Mod-Handler umleiten,
3. den originalen lokalen Chore-Enqueue-Pfad verwenden und
4. den eigentlichen Spieleingriff ausschließlich im späteren Execute-Modus des Handlers ausführen.

Opcode `111` ist in der untersuchten DLL der derzeit beste Kandidat, aber nicht allgemein frei:
In Stronghold Crusader HD bedeutete derselbe Wert `Skirmish Add AI`. In der untersuchten DE-DLL zeigt
sein Handler auf ein einzelnes `ret`, und es gibt keinen statischen Aufrufer, der diesen Opcode
erzeugt. Diese Aussage gilt ausschließlich für den unten genannten DLL-Hash.

## Untersuchter Build

- Datei: `x86_64/CrusaderDE.dll`
- Größe: `3,446,784` Bytes
- SHA-256: `17F8DD4A92FF6125BD6A3A70ABC80C727682E489696C218D146A7EA6D2F88BF4`
- PE Image Base: `0x180000000`
- PDB-Verweis:
  `D:\Jenkins\.jenkins\workspace\CrusaderDE\CDE-DLL-STABLE\CrusaderDEDLL\Source\ff_gfx_manager\Release\Crusader.pdb`
- PDB GUID: `{C1E26511-89D7-4315-843A-C3DF84430ECC}`, Age `7`

Alle Adressen in diesem Dokument sind RVAs relativ zur geladenen Modulbasis, sofern nicht ausdrücklich
anders angegeben. Feste RVAs dürfen nicht ohne Versionsprüfung in produktivem Mod-Code benutzt werden.

Für die native Analyse wurden Cutter 2.4.1, Rizin 0.8.1 und der gebündelte Ghidra-Decompiler verwendet.
Die portable Installation liegt im ignorierten Arbeitsordner `.native-analysis`.

## Gesamtfluss

    Benutzeraktion
        |
        v
    EngineInterface.GameAction(...)
        |  hält EngineInterface.threadLock
        v
    DLL_GameAction(...)
        |
        v
    interner Aktionspfad setzt Command-Parameter
        |
        v
    QueueLocalChore(opcode)
        |-- erzeugt lokalen Pending-Slot
        |-- vergibt scheduledTick und commandId
        |-- ruft Handler in Mode 1 zum Serialisieren auf
        `-- schreibt denselben Chore in den ausgehenden MP-Puffer
                                      |
                                      v
                           packetType 1 / Steam channel 2
                                      |
                                      v
                         DLL_ReceiveChore(sender, data, len)
                                      |
                                      v
                           nativer Incoming-FIFO
                                      |
                                      v
                         Parser + Handler Mode 2
                                      |
                                      v
                          Remote Pending-Slot

    Am scheduledTick:
      lokaler und remote empfangener Slot -> derselbe Handler in Mode 0 -> Simulationseffekt

Der Sender schleift sein eigenes Netzwerkpaket nicht zurück. Seine lokale Ausführung kommt aus dem
bereits beim Enqueue angelegten Pending-Slot. Die Empfänger erzeugen ihre Slots aus dem Paket.

## Paketformate

### Äußerer managed/native Chore-Puffer

Der bereits managed rekonstruierte Datensatz ist durch die nativen Writer- und Reader-Funktionen
bestätigt:

    int32 payloadLength
    byte  targetOrSenderPlayerId
    byte  payload[payloadLength]

Auf der Sendeseite bedeutet das Byte `targetPlayerId`; `0` ist der normale Broadcast. Im nativen
Incoming-FIFO steht an derselben Position die vom managed Netzwerkcode separat übergebene
`senderPlayerId`.

Der ausgehende Puffer endet mit einer negativen `payloadLength`. Jeder Datensatz benötigt
`payloadLength + 5` Bytes.

`DLL_ReceiveChore` hängt eingehende Datensätze intern in genau dieser Form an:

    [int32 length][byte senderPlayerId][payload]

Der Incoming-FIFO hat in diesem Build ungefähr eine Million Bytes Kapazität. Bei Überlauf wird ein
Datensatz im untersuchten Pfad verworfen; eine robuste Mod-API darf diese Grenze nicht annähernd
ausreizen.

### Innerer normaler Chore

Für normale opcodierte Commands ist das innere Format bestätigt:

| Offset | Größe | Bedeutung |
|---:|---:|---|
| `0x00` | 1 | Opcode |
| `0x01` | 3 | `scheduledTick`, Little-Endian `uint24` |
| `0x04` | 4 | `commandId`, Little-Endian `int32` |
| `0x08` | variabel | Opcode-spezifische Parameter |

Die äußere `payloadLength` enthält den acht Byte großen inneren Header.

Die 24-Bit-Ticknummer wird beim Empfang auf 32 Bit nullerweitert. Ein theoretischer Wrap liegt bei
`16,777,216` Ticks, also nach ungefähr 116,5 Stunden bei 40 Ticks pro Sekunde. Das Verhalten eines
so langen Matches wurde nicht dynamisch getestet.

Die OpCodes `0`, `1`, `125`, `126` und `127` benutzen Sonderpfade und dürfen nicht wie normale
Commands behandelt werden:

- `0` und `1`: Timing-/Synchronisationssteuerung
- `125`: Wrapper-/Dekompressionspfad
- `126` und `127`: Ping-/Timing-Kontrollpakete

Die normale Handler-Tabelle ist bis einschließlich Opcode `120` gültig. Die Bytes für `121` bis `124`
sind keine verlässlichen Funktionszeiger; diese Werte sind keine sicheren Custom-Slots.

## Pending-Chore-Slots

Ein normaler Slot ist `0x500` Bytes groß. Die Queue besitzt in DE wesentlich mehr Kapazität als das
alte HD-System; die Analyse zeigt 500 Slot-Kandidaten. Der relevante Slot-Anfang ist:

| Slot-Offset | Bedeutung |
|---:|---|
| `0x00` | `scheduledTick` |
| `0x04` | Sender-/Player-ID |
| `0x08` | Opcode |
| `0x09` | Status/State; `1` ist ein neu angelegter Pending-Command |
| `0x0C` | `commandId` |
| `0x10` | bereits in eine Host-Sync-Barriere aufgenommen |
| `0x11` | Beginn der opcodespezifischen Parameter |

Für neue Mod-Chores sollte der Payload klein und fest dimensioniert bleiben. Die vorhandenen
Built-in-Commands liegen weit unter der theoretischen Slot-Grenze.

### Lokales Enqueue

Die zentrale Funktion bei RVA `0x23960` wird in diesem Dokument `QueueLocalChore` genannt. Ihre
effektive Signatur ist:

    void QueueLocalChore(ChoreManager* manager, byte opcode)

Sie:

1. reserviert und initialisiert einen Pending-Slot,
2. übernimmt die lokale Player-ID,
3. erzeugt eine Command-ID,
4. berechnet einen zukünftigen Ausführungs-Tick,
5. ruft den Opcode-Handler im Serialisierungsmodus auf und
6. erzeugt das ausgehende Chore-Paket.

Die Command-ID wird nach folgendem Schema gebildet:

    commandId = playerId * 100000000 + perPlayerCounter

Der normale geplante Tick folgt sinngemäß:

    scheduledTick =
        max(currentTick, trackedReferenceTick)
        + currentDynamicCommandDelay

Der Delay ist nicht konstant. Das Spiel passt ihn anhand der Netzwerktiming-Daten an. Ein besonderer
Zweig plant bestimmte Commands mit `currentTick + syncPeriod * 50`; bei der beobachteten Periode `4`
sind das 200 Ticks beziehungsweise fünf Sekunden.

### Empfang

Der normale Empfangspfad bei RVA `0x23EE0` nimmt effektiv folgende Werte entgegen:

    ChoreManager*
    opcode
    senderPlayerId
    scheduledTick
    commandId
    payload

Er erzeugt denselben Slotaufbau wie der Sender. Früh eingetroffene Commands aktualisieren den
verbleibenden Vorlauf. Zu spät eingetroffene Commands erhöhen einen Lag-/Lateness-Zähler.

Commands mit `scheduledTick < 1` sind Meta-/Immediate-Commands: Sie werden sofort ausgeführt und
wieder freigegeben. Commands mit `scheduledTick >= 1` bleiben bis zum Ziel-Tick pending.

## Handler-Tabelle und die drei Modi

Die Handler-Tabelle liegt in diesem Build bei RVA `0x2C5A30`. Sie besteht aus acht Byte großen
Funktionszeigern und ist schreibbar. Ein Handler wird ohne explizite Argumente aufgerufen; er liest
seinen Kontext aus dem globalen ChoreManager-Zustand.

Der Modus steht relativ zum ChoreManager bei `+0x84CCC`:

| Mode | Bedeutung |
|---:|---|
| `0` | Execute: Parameter aus dem Slot lesen und Simulation verändern |
| `1` | lokales Schedule/Send: Parameter in den lokalen Slot serialisieren |
| `2` | Receive sizing: erwartete Payload-Größe veröffentlichen |

Die Payload-Größe steht bei `ChoreManager + 0x84CD4`, der laufende Feldcursor bei
`ChoreManager + 0x370BF8`.

Die Hilfsfunktion bei RVA `0x1F5C0` entspricht semantisch der HD-Funktion
`serializeOrDeserializeCommandParameter`. Sie kopiert ein Feld zwischen einer lokalen Adresse und dem
aktuellen Chore-Parameterbereich und erhöht danach den Cursor. Ein Handler beschreibt sein Format,
indem er dieselbe Feldfolge in Serialize- und Execute-Richtung benutzt.

Beispiel: Der Handler für Opcode `68` veröffentlicht zehn Payload-Bytes und verarbeitet die Folge
`2 + 2 + 2 + 4` Bytes. Der Handler für `MakeTroop`, Opcode `31`, benutzt fünf Payload-Bytes.

### Konsequenz für Custom-Payloads

Der normale Receive-Pfad fragt den Handler im Mode `2` nach der zu kopierenden Payload-Größe, bevor
die Parameter in den Pending-Slot kopiert werden. Eine erste Mod-API sollte daher pro Opcode eine
feste Payload-Größe verlangen. Variable Daten können zunächst als fester Envelope mit eigener
Längenangabe und harter Maximalgröße abgebildet werden. Eine wirklich variable Länge würde einen
zusätzlichen Patch des Receive-Dispatchers benötigen.

## Lockstep-Barriere

Die entscheidende Frame-Garantie sitzt im aus `DLL_RunTick` aufgerufenen Pfad bei RVA `0x1BCC0`,
hier `CanAdvanceSyncFrame` genannt.

Der Host erzeugt ungefähr alle vier Ticks einen `SyncEvent`-Chore mit Opcode `120` (`0x78`). Dabei
werden aktive Pending-Slots ausgewertet. Noch nicht in eine Barriere aufgenommene Commands werden
markiert und ihre `commandId` wird in das SyncEvent geschrieben. Pro Event wird nur eine begrenzte
Anzahl, beobachtet ungefähr 25 IDs, aufgenommen.

Die drei festen `int32`-Felder des SyncEvent-Payloads werden aus ihrer Verwendung als folgende Werte
interpretiert:

- Ziel-Tick, normalerweise `currentTick + 4`
- Anzahl der aufgelisteten Command-IDs
- Barrieren-/Sequenznummer

Danach folgt die Liste der `commandId`-Werte.

Auf dem Client werden SyncEvents sortiert in einer Host-Sync-Queue gehalten. Erreicht die Simulation
die Barriere, prüft `CanAdvanceSyncFrame`, ob jede vom Host genannte Command-ID bereits in der lokalen
Pending-Queue existiert. Fehlt eine ID, liefert die Funktion `false`; `DLL_RunTick` setzt dadurch den
Multiplayer-Stall-/Frame-Skip-Pfad.

Nach drei Sekunden protokolliert der native Code `SyncEvent - Forced run` und lässt die Simulation
trotz eines fehlenden Commands weiterlaufen. Die Garantie ist damit absichtlich begrenzt, verhindert
aber im normalen Netzbetrieb, dass ein Peer den Ziel-Tick vor dem Eintreffen eines bekannten Commands
überschreitet.

Das ist der wichtigste Vorteil des originalen Enqueue-Pfads: Ein darüber erzeugter Custom-Chore
bekommt automatisch einen Pending-Slot, eine Command-ID, einen Ziel-Tick und die Möglichkeit, in die
Host-Barriere aufgenommen zu werden. Ein Script-Extender-Custom-Packet bekommt nichts davon.

## Relevante native Funktionen

| RVA | Arbeitsname/Bedeutung |
|---:|---|
| `0x080AE0` | Export `DLL_GameAction` |
| `0x0856F0` | Export `DLL_ReceiveChore` |
| `0x0858F0` | Export `DLL_RunTick` |
| `0x023960` | `QueueLocalChore` |
| `0x0237D0` | inneren normalen Chore bauen |
| `0x023E40` | empfangenen Datensatz an Incoming-FIFO anhängen |
| `0x023C00` | nächsten Incoming-Datensatz entnehmen |
| `0x0235F0` | Incoming-Payload parsen und dispatchen |
| `0x023EE0` | empfangenen normalen Command planen |
| `0x19C370` | äußeren ausgehenden Datensatz schreiben |
| `0x01F5C0` | Chore-Feld serialisieren/deserialisieren |
| `0x01F7B0` | fällige Pending-Chores ausführen |
| `0x01BCC0` | Host-Sync-Barriere / `CanAdvanceSyncFrame` |
| `0x01ADE0` | Handler für Opcode `120` / SyncEvent |
| `0x027DB0` | dynamischen Turn-/Command-Delay anpassen |
| `0x024E50` | Frame-Lag-/Skip-Berechnung |
| `0x01CCF0` | weiterer Ping-/Lag-Verwaltungspfad |
| `0x01F6B0` | synchronisierten Autosave-Chore erzeugen |
| `0x0127A0` | Handler für Opcode `31` / `MakeTroop` |
| `0x02C5A30` | Chore-Handler-Tabelle |
| `0x08571310` | ChoreManager-Datenbasis |

Die drei Exporte liegen wegen der großen DLL-Datenbereiche deutlich weiter hinten als die internen
Codefunktionen. Bei ASLR muss immer `moduleBase + RVA` verwendet werden.

## Opcode-Befund

Statische Calls auf `QueueLocalChore` wurden nach konstanten Opcode-Werten ausgewertet:

- 140 direkte Call-Sites
- 83 unterschiedliche konstante OpCodes
- normale Dispatch-Tabelle sicher bis einschließlich `120`
- kein direkter statischer Erzeuger für `111`
- Tabellenziel von `111`: RVA `0x00FC30`, exakt ein `ret`

Weitere scheinbare No-op-Einträge sind `103`, `104`, `105`, `106`, `107` und `117`. Diese Werte sind
nicht automatisch besser: Im HD-Vorgänger hatten mehrere davon konkrete Lobby-, Map- oder
Transferaufgaben. Opcode `116` zeigt ebenfalls auf einen trivialen Handler, wird in DE aber statisch
erzeugt und ist deshalb ausgeschlossen.

Auch bei `111` bleiben mögliche indirekte oder datengetriebene Aufrufer offen. Eine Registrierung muss
daher:

1. den exakten DLL-Build prüfen,
2. den erwarteten ursprünglichen Funktionszeiger validieren,
3. Kollisionen mit anderen Mods verhindern und
4. sich bei jeder Abweichung sicher deaktivieren.

## Empfohlener Script-Extender-Entwurf

Eine sinnvolle öffentliche API könnte konzeptionell so aussehen:

    RegisterCustomChore(
        byte opcode,
        int fixedPayloadSize,
        Action<ChoreReader> execute);

    EnqueueCustomChore(
        byte opcode,
        ReadOnlySpan<byte> payload);

Intern wäre der Ablauf:

1. Beim `LibraryLoaded`-Zeitpunkt die Signaturen, Tabelle und Originalpointer prüfen.
2. Einen prozessweit verwurzelten nativen Stub/managed Delegate für den Opcode installieren.
3. Beim Enqueue `EngineInterface.threadLock` halten.
4. Payload in einen kurzlebigen, durch denselben Lock geschützten Staging-Puffer kopieren.
5. `QueueLocalChore(ChoreManager, opcode)` aufrufen.
6. Im synchron aufgerufenen Handler-Mode `1` die Staging-Daten mit der nativen Feldkopierfunktion in
   den Slot schreiben.
7. Im Mode `2` ausschließlich die feste Payload-Größe veröffentlichen.
8. Im späteren Mode `0` die Felder aus dem Slot lesen und den registrierten Simulationseffekt
   ausführen.

Der Button beziehungsweise Custom-Packet-Handler darf den Effekt niemals zusätzlich lokal ausführen.
Er erzeugt nur den Chore. Sender und Empfänger gelangen später über Mode `0` durch denselben
Ausführungspfad.

### Warum `EngineInterface.threadLock` zwingend ist

Die Publicized-Assembly zeigt:

    public static object threadLock = new object();

Sowohl `EngineInterface.GameAction(...)` als auch `EngineInterface.ReceiveChore(...)` halten dieses
Objekt während der nativen Aufrufe. Eine eigene Enqueue-API muss denselben Lock verwenden, damit sie
nicht gleichzeitig mit `DLL_RunTick`, `DLL_ReceiveChore` oder einem anderen GameAction-Aufruf die
globalen Chore-Parameter und Queue-Indizes verändert.

Ein managed Detour am Einstieg von `EngineInterface.GameAction` läuft noch vor dem `lock` des
Originalcodes und ist daher nicht automatisch geschützt. Ruft der Detour für einen reservierten
Action-Wert direkt `QueueLocalChore` auf, muss er `EngineInterface.threadLock` selbst halten. Erst der
Original-/Trampolinpfad übernimmt den Lock für seinen eigenen Aufruf von `DLL_GameAction`.

### Prozesslebensdauer

Der Handlerpointer, Delegate, native Stub und alle Hook-Objekte müssen statisch beziehungsweise
prozessweit verwurzelt bleiben. Sie dürfen in dieser SHCDE-BepInEx-Umgebung nicht in `OnDestroy()`
entfernt oder disposed werden, weil die `BaseUnityPlugin`-Komponente bereits beim Spielstart zerstört
wird, obwohl der Prozess und die Mod-Funktionalität weiterlaufen.

### Multiplayer-Kompatibilität

Alle Peers müssen denselben Mod, dieselbe Chore-Protokollversion und dieselbe Payload-Semantik
verwenden. Ein ungemoddeter Peer würde Opcode `111` als No-op ausführen, während ein gemoddeter Peer
den Simulationseffekt anwendet; der Desync wäre sicher.

Ein Script-Extender-Custom-Packet bleibt für eine Lobby-/Capability-Handschlagprüfung geeignet. Es
sollte aber nur Version und Bereitschaft aushandeln, nicht den Simulationseffekt auslösen. Bei
fehlender oder abweichender Antwort muss Custom-Chore-Gameplay deaktiviert oder der Matchstart
blockiert werden.

### Beispielpayload für MPTest

Für den Woodcutter-Swordsman-Test bietet sich ein kleiner fester Payload an, etwa:

| Feld | Zweck |
|---|---|
| Protokollversion | Payload-Kompatibilität |
| Requester-/Player-ID | Eigentümer und Plausibilitätsprüfung |
| Building-ID | ausgewählte Holzfällerhütte |
| Unit-Typ | erwarteter Schwerterkämpfer |
| exakte Tile-ID oder X/Y | identischer Spawnort auf allen Peers |
| Nonce/Request-ID | Diagnose und Deduplizierung |

Die Position sollte vor dem Enqueue genau bestimmt und serialisiert werden. Im Execute-Modus kann
jeder Peer Gebäudezustand, Besitzer und Tile noch deterministisch validieren. Erst danach wird
`CreateUnitLocal` genau einmal aus dem Chore-Handler aufgerufen.

## Risiken und noch notwendige dynamische Tests

Vor einem produktiven Script-Extender-Patch sind mindestens folgende Tests nötig:

1. **Handler-Capture ohne Mutation:** Opcode, Mode, Größe, Command-ID und Tick für Vanilla-Aktionen
   protokollieren.
2. **No-op-Custom-Chore:** Opcode `111` mit festem Testpayload über zwei echte Peers senden, aber noch
   keinen Spielzustand verändern.
3. **Barrierenachweis:** Im Log zeigen, dass die Custom-Command-ID in einem Host-SyncEvent auftaucht
   und beide Peers denselben Execute-Tick sehen.
4. **Paketverzögerung:** künstlich verzögern und prüfen, ob der Client vor der Barriere stoppt.
5. **Doppelte und fehlende Pakete:** Verhalten sowie den nativen Drei-Sekunden-Forced-Run erfassen.
6. **Save/Load und Resync:** Pending Custom-Chores während Multiplayer-Save, Resync und Mapwechsel
   testen.
7. **Hostwechsel/Disconnect:** prüfen, ob Barrieren und Command-ID-Räume korrekt weiterlaufen.
8. **Langzeittest:** Slot-Recycling, Command-Counter und Tick-Wrap beobachten.

Jede spätere Instrumentierung sollte Zeitstempel mit Millisekunden und mindestens folgende Werte
loggen:

- lokaler/remote Enqueue-Zeitpunkt
- Opcode
- Command-ID
- scheduledTick
- aktueller Tick
- Handler-Mode
- Sender-ID
- Payload-Protokollversion
- Aufnahme in SyncEvent
- tatsächlicher Execute-Zeitpunkt

Managed Exceptions dürfen niemals über eine native Callback-Grenze entweichen. Der Handler muss
Fehler vollständig abfangen, protokollieren und sich deterministisch verhalten.

## Nutzen der anderen Projekte

### OpenSHC und lokale HD-Reverse-Engineering-Daten: sehr hoch

OpenSHC enthält den rekonstruierten `GameSynchronyState`, den `GameCommand`-Slot und die Command-Enums
des HD-Vorgängers. Die lokalen Reverse-Engineering-Notizen benennen zusätzlich die zentralen
Funktionen:

- `queueCommand`
- `scheduleReceivedCommand`
- `serializeOrDeserializeCommandParameter`
- `processWaitingCommands`
- `determineGameTicksToPerform`
- `updateTurnDelayFromSyncPacket`

Ihre Semantik stimmt sehr eng mit den neu identifizierten DE-Funktionen überein. Dadurch lassen sich
die namenlosen DE-Routinen belastbar einordnen. DE erweitert das alte Modell unter anderem um
Command-IDs und die expliziten Host-SyncEvent-Barrieren.

Relevante OpenSHC-Dateien:

- https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Synchrony/GameSynchronyState.hpp
- https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Commands/GameCommand.hpp
- https://github.com/sourcehold/OpenSHC/blob/1acd3d86810b060e04de694923151404fa7286f6/src/OpenSHC/Commands/GameCommandType.hpp

Lokale Notizen:

- `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Stronghold Crusader HD reversed\shc_functions_catalog_EN.md`
- `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Stronghold Crusader HD reversed\stronghold_RE_ghidra_verified_addendum_EN.md`

### shcde-fixes: hoch für die Implementierung, niedrig für Chore-Semantik

Das Projekt enthält keine zusätzliche Chore-Dokumentation, zeigt aber passende produktive Muster für:

- AOB-/Pattern-Scanning statt fester Adressen
- x64-Inline-Hooks und Detours
- sichere managed Callbacks über native Assembler-Stubs
- dauerhaft verwurzelte Hook-Objekte

Verglichener Commit: `400a7c7c75332ff6fbddc854f3e0ce1fadce19b8`

### Crusader DE Tweaker: mittel

Der Tweaker enthält keine innere Chore-Analyse. Sein `MakeTroopRecruitHook` bestätigt aber praktisch,
dass `EngineInterface.GameAction` managed per MonoMod detourbar ist und der Originalaufruf danach
weiter in den Vanilla-Chore-Pfad läuft. Das ist für Validierung, Capability-Gates oder das Abfangen
eines reservierten managed Action-Werts nützlich.

Verglichener Commit: `d8b16dca9633871dcb152df5dec4d0c594a02f66`

### Stronghold Crusader DE AI Buff: gering für Chores

AI Buff verwendet Script-Extender-Events und synchronisierte Lobbywerte, greift aber nicht in die
Chore-Queue ein. Hilfreich ist lediglich die bewusste Vermeidung nichtdeterministischer
Floating-Point-Akkumulationen im Multiplayer.

Verglichener Commit: `c8a4a86f3c9845cf558d31582eb6f25566b72e95`

### UCP3: gering für das DE-Chore-System

UCP3 ist als allgemeine native Patch-/Modularisierungsreferenz für den HD-Vorgänger interessant. Im
geprüften Top-Level-Code wurden jedoch keine zusätzlichen Chore-/Command-Queue-Erkenntnisse gefunden,
die über OpenSHC und die lokalen HD-Reverse-Engineering-Daten hinausgehen.

Verglichener Commit: `02a7a6bc8ab956a91fc752e8c8ed215c149855e7`

### Lokaler Script Extender

Der Script Extender findet bereits den ChoreManager und stellt erprobte Pattern-Scanner-, Hook- und
API-Strukturen bereit. Es fehlt aktuell die eigentliche Custom-Chore-Registrierung und das Enqueue.

Verglichener Commit: `9ddb419ca6a5f05d7c8f85a10ba0795c1193c318`

## Zuordnung HD zu DE

| HD-Funktion | DE-RVA | DE-Arbeitsname |
|---|---:|---|
| `queueCommand` `0x00489100` | `0x023960` | `QueueLocalChore` |
| `scheduleReceivedCommand` `0x00480210` | `0x023EE0` | empfangenen Command planen |
| `serializeOrDeserializeCommandParameter` `0x004805D0` | `0x01F5C0` | Chore-Feldkopie |
| `processWaitingCommands` `0x004892F0` | `0x01F7B0` | fällige Chores ausführen |
| `updateTurnDelayFromSyncPacket` `0x00488010` | `0x027DB0` | dynamischen Delay anpassen |

Diese Zuordnung basiert nicht nur auf ähnlichen Call-Positionen, sondern auf übereinstimmenden
Datenlayouts, Mode-Wechseln, Cursor-Kopierlogik, Scheduling und Lag-Behandlung.

## Verbleibende Unsicherheiten

- Die semantischen Namen mehrerer ChoreManager-Felder sind aus ihrer Verwendung abgeleitet und nicht
  aus Symbolen bestätigt.
- Indirekte oder datengetriebene Erzeugung von Opcode `111` ist durch eine statische Call-Site-Suche
  nicht vollständig auszuschließen.
- Save-/Resync-Serialisierung pending Custom-Chores wurde noch nicht dynamisch geprüft.
- Die genaue Obergrenze pro Host-SyncEvent und ihr Verhalten bei sehr vielen Commands sollte noch
  instrumentiert werden.
- Der Drei-Sekunden-Forced-Run ist nativer Fallback und verhindert eine absolute Zustellgarantie.
- Das Verhalten am 24-Bit-Tick-Wrap ist ungeprüft.
- Ein sicherer Mod-Registry-/Handshake-Mechanismus für mehrere Mods existiert noch nicht.

Trotz dieser offenen Punkte ist die Kernfrage beantwortet: Der originale Enqueue-Pfad ist der
geeignete Integrationspunkt. Rohes Einspeisen in `DLL_ReceiveChore`, manuelles Bauen von
`packetType = 1` oder ein Custom-Packet mit festem Zieltick wären jeweils unvollständig, weil sie den
lokalen Sender-Slot beziehungsweise die native Command-ID-/Barrierenlogik nicht korrekt abbilden.
