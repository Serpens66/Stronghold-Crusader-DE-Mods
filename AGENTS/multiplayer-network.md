# Netzwerk, Multiplayer, Chores und Spielmodus-Freigaben

**Lesen, wenn:** Custom-Pakete, Chore-Aktionen, `[SyncPerPlayer]`-Settings, Lobby-/Netzwerkautorität, Moduserkennung bzw. Feature-Freigaben je Spielmodus oder Multiplayer-Tests betroffen sind. Gilt zusätzlich zur Root-`AGENTS.md`.

## Threadvertrag für Custom-Pakete

- `R3PacketEventHook<T>` garantiert keinen Aufrufthread. Steam-Pakete können über Vanillas `System.Threading.Timer` auf wechselnden, sich überlappenden ThreadPool-Workern eintreffen; eine beobachtete Thread-ID ist weder stabil noch ein Main-Thread-Nachweis.
- Rohcallbacks von Steam-/Control-Plane-Paketen reihen nur den vollständig kopierten Payload und den Transportabsender in eine thread-sichere FIFO ein; keine Unity-, Noesis-, Lobby-, Restart-, Modsettings-, Protokoll- oder sonstige veränderliche Auswertung.
- Unitys `SynchronizationContext` und die Main-Thread-ID werden bei der Initialisierung auf dem validierten Main-Thread erfasst. Fehlen sie, bleibt die Paketverarbeitung fail-closed; nie auf dem Fremdthread ausführen.
- Main-Thread-Dispatch nicht blockierend, FIFO, mit koalesziertem Posting und Lost-Wakeup-Schutz. Kein `EnqueueAndWait`, keine Dispatcher-Initialisierung vom Fremdthread, kein Helper mit Inline-Fallback.
- Nach dem Dispatch Sitzung, Operation/Generation, Transportabsender und dessen Autorität sowie Runtime-/Lobbyzustand erneut validieren; die Prüfung vor dem Einreihen ersetzt das nicht.
- Chore-Pakete sind eine andere Transportklasse: deterministische Simulationsmutationen bleiben synchron im Chore-/Simulationskontext; Unity-/Noesis-UI und BepInEx-Logging aus diesem Calltree als unveränderliche Snapshots nicht blockierend zum Main-Thread auslagern.
- Gemischte Receiver klassifizieren den Transport explizit: vorhandener Steam-Absender = Control Plane (Main-Thread-Marshalling); fehlender Steam-Absender = Chore nur, wenn Pakettyp und Protokoll das ausdrücklich vorsehen.
- Custom-Pakete verändern auf Netzwerkthreads keine Modsettings, Noesis-Bindings oder anderen Main-Thread-Zustände.

## Serialisierung

- Eigene `GameNetworkAPI`-Pakete nicht per Reflection-/Contractless-Serialisierung oder `[MessagePackObject(true)]` (fehlende Assembly `System.Runtime.Serialization` → `MessagePackSerializationException`). Immer einen expliziten `IMessagePackFormatter<T>` mit stabilen numerischen `[Key(...)]` registrieren; vorhandene Workspace-Formatter als Vorlage.

## Tick-synchrone Chore-Aktionen

- Pakettypen, Formatter und Receiver beim Pluginstart auf allen Peers in derselben Reihenfolge registrieren.
- Simulationsmutation nicht vorab lokal anwenden: der Chore liefert auch an den Sender, nur der gemeinsame Receiver führt sie auf allen Peers im selben Tick aus.
- Payloads mit stabilen globalen Objekt-IDs und explizitem Zielzustand statt lokaler Slots oder Toggles.
- Keine rohen oder widersprüchlich gerahmten Chore-106-Daten; kein Reflection-Zugriff auf interne Sendemethoden; kein direkter Aufruf nativer Wrapper aus `BulkChoreDetours`.
- Senden mit `GameNetworkAPI.SendPacketToAllEx2(packet, packetId, viaChore: true)`; die Methode serialisiert und stellt die 2-Byte-Packet-ID selbst voran (kein manuelles Präfix).
- Fallback-Falle: Bei `GameGlobalsManager.Instance.ChoreManagerVA == 0` oder Payload > 1200 Byte fällt der Aufruf auf den nicht tick-synchronen Steam-Pfad zurück. Für fail-closed Mutationen vorher prüfen: Packet-Hook und Registrierung initialisiert, `GameNetworkAPI.Serialize(packet)` gelingt, `sizeof(short) + serializedBody.Length <= 1200`, `ChoreManagerVA != 0`. Das Paket zwischen Vorabserialisierung und Senden nicht mehr verändern.
- Den Send-Aufruf in `try`/`catch`; Exceptions mit ms-Zeitstempel loggen und als Sendefehler behandeln (kein Steam-Fallback). Normale Rückkehr bestätigt nur Vorbedingungen und Einreihen in die native `void`-Queue, nicht Zustellung oder Ausführung auf allen Peers.

## Modsettings im Multiplayer

- Alle Mods mit Lobby-Modsettings nutzen `LobbyModSettingsPresetRegistration.Register(...)`; keine eigenen Transport-, Registrierungs- oder Syncpfade.
- Jede `[SyncPerPlayer]`-Property braucht ein öffentlich lesbares Companion-Array `<PropertyName>Data` mit Slots 0–8 und kompatiblem Elementtyp. Widersprüchliche Klassifizierungen (`[SyncHostOnly, SyncPerPlayer]`) sind unzulässig; die Registrierung validiert vorab und bricht fail-closed ab.
- Fachliche Ergänzungen nur über `ConfigurePerPlayerLobbySettings(...)`: `ResetSlotsWith`, `RequireReport`, gemeinsame Aktualisierungs-/Veröffentlichungs-Callbacks, `WhenLocalPlayerResolved`. Keine mod-eigenen Roster-Poller, Join-Resync-Pakete oder parallelen Companion-Lebenszyklen.
- Shared erkennt Lobbywechsel, Join, Leave und Neubelegung einer Spieler-ID (Lobby-ID + Steam-ID), ignoriert KI- und `dummyToBeKicked`-Plätze, verwirft betroffene Slots und veröffentlicht nach vollständig aufgelöstem Roster alle lokalen `[SyncPerPlayer]`-Werte neu; Ingame-Listen filtern `MPGameMember.kicked`. Änderungen am Auflösungsstatus oder der lokalen Spieler-ID lösen ebenfalls einen Snapshot aus. Fehlt `activeLobby` am Kartenübergang, bleibt der Snapshot schon bei einem aktiven menschlichen `gameMember` erhalten (sonst würde Einzelspieler-Skirmish vor `OnStartMap(Pre)` zurückgesetzt). Ab `OnStartMap(Pre)` pausiert die Lobbybeobachtung bis `OnUnloadMap(Post)`; während der Partie keine periodische Resynchronisation.
- Vollständigkeit über `System_ArePerPlayerSettingsReady(...)` prüfen; fehlende Pflichtwerte blockieren nur die abhängige Funktion, nicht pauschal die Partie oder rein lokale Funktionen.
- Veränderliche persönliche Werte (z. B. Arrays) werden beim Einsetzen tief kopiert. Resetwerte drücken die Semantik aus: persistierte Präferenzen ihren echten Default, noch nicht gemeldete Status-/Auswahl-/Manifestwerte einen Sentinel wie `null`.
- Vor sicherer Spieler-ID-Auflösung nie ersatzweise in Slot 1 schreiben; die Mod hält den lokalen Wert unabhängig vom Companion-Array, Shared kopiert ihn nach Auflösung in den richtigen Slot.
- Clients erzeugen aus empfangenen `[SyncHostOnly]`-Werten keine zweite lokale Hostwertänderung: nur Darstellung bzw. abgeleitete Werte aktualisieren, nicht zurücksenden oder persistieren.

## Autorität und Phasen

- Netzwerkautorität stammt vom Transportabsender (gegen den Lobbyhost geprüft); eine Steam-ID im Payload ist kein Ersatz.
- Lobby- und Ingamephase nicht allein an einer vorhandenen `gameMembers`-Liste unterscheiden (am Kartenübergang evtl. schon gefüllt, obwohl die Lobby noch der richtige Zustellpfad ist).
- `GameNetworkAPI.IsNetworkedEnvironment()` nie allein als Multiplayer-Signal (auch lokaler Skirmish hat eine lokale `gameMembers`-Liste).

## Spielmodi und Feature-Freigaben

- Moduserkennung und Profilauswertung liegen in APIShared (`MissionModePolicy`, `GameplayModModePolicy`); Benachrichtigungskontext oder zentral gespeicherten `GameModeHelper.Capture()` verwenden. Feste Mod-/Feature-Freigaben liegen als modseitige Adapter in `Shared/Adapters/APIShared/SerpsModProfiles.cs` und `GameplayFeatureModePolicy.cs` (Namespace `Shared`), zusammen mit `GameplayModActivationGate` quellverlinkt; sie ändern keine Moduserkennung und installieren keine Hooks. Fremde Mods nutzen eigene Profile. `Shared/Adapters/APIShared/PlayerIdentityHelper.cs` enthält nur Spieleridentitätshelfer.
- Reguläre Gameplay-Mods binden `GameplayModActivationGate` als lokalen Einstellungsadapter ein. Die zentrale Freigabe samt Begründung ist maßgeblich; unbekannte/widersprüchliche Herkunft und Tutorial erhalten keine Freigabe. Bestehende Featureausnahmen erhalten. `IsRealMultiplayer()` darf außerhalb einer Mission für Lobbyautorität den gekapselten Netzwerkstatus lesen; das ist keine Missionsfreigabe.
- Neue Features funktionieren standardmäßig auch im Karteneditor (außer ausdrücklich ausgeschlossen). `BugfixesAndQoL`- und Testmod-Features bleiben in jedem Modus aktiv. Andere Mods nur im Editor und in den von `GameModeSnapshot.AllowsCustomGameMods` bzw. `GameplayModModePolicy` zugelassenen Custom-Kontexten; Vanilla-Kampagnen, -Einzelmissionen und unveränderte Trails bleiben ausgeschlossen.
- Missionsmeldungen: `Context.IsSave`, `Context.Mode`, `Context.IsHost` (Startart und MP-Status sind unabhängig). Diagnoselogs enthalten Sitzungskennung und `GameModeSnapshot.ToDiagnosticString()`; unbekannte Angaben bleiben unbekannt.

## Multiplayer-Tests

- Nur nachweislich echte MP-Läufe auswerten: Host- und Clientlog zeigen einen `GameModeSnapshot` mit mehreren menschlichen Teilnehmern (beliebiger Spielstart oder `gameMembers != null` genügt nicht).
- Neben kleinen Hoständerungen nach Beitritt immer prüfen: Hostwerte vor Beitritt, frischer Client mit abweichenden Defaults, große Settings deutlich über einem Netzwerkfragment, Übergang Lobby/Kartenstart, Änderungen nach Kartenstart.
- Bei Ausfällen mit einer funktionierenden Mod auf demselben Shared-/Extender-Pfad vergleichen; Änderungszeitpunkt, Payloadgröße und Empfangsphase getrennt untersuchen.
- Client-Log: `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` (siehe Root-`AGENTS.md`).
