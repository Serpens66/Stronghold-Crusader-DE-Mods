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

## Resync-Datensätze

- Bei `MultiplayerResyncData.TryRegister` und `TryRegisterEarlyCapture` dürfen Owner-GUID und Record-ID jeweils nur aus 1 bis 96 ASCII-Buchstaben, Ziffern, Punkten, Unterstrichen oder Bindestrichen bestehen. Featurepräfixe mit Bindestrich schreiben, etwa `auto-send-rules`. Andere Registrierungs-APIs können `/` akzeptieren.
- Bei einer Übernahme in einen Hauptmod die tatsächliche Registrierung mit dem gebauten Mod und dem gewählten SDK prüfen. Den Datensatz auf beiden MP-Peers im Resync-Manifest bestätigen. Ein Spielstart oder fehlende Warnungen allein belegen die Registrierung nicht.

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
- Reguläre Gameplay-Mods binden `GameplayModActivationGate` als lokalen Einstellungsadapter ein. Die zentrale Freigabe samt Begründung ist maßgeblich; unbekannte Herkunft und Tutorial erhalten keine Freigabe. Einen Sperrzustand „widersprüchliche Herkunft“ gibt es nicht mehr: APIShared legt den Modus immer fest (Spielstand-Eintrag bzw. Quellsitzung beim Neustart; sonst die Angaben des Spiels) und loggt einen verworfenen Customize-Hinweis als Fehler `GAME_MODE_ORIGIN_REJECTED`. Bestehende Featureausnahmen erhalten. `IsRealMultiplayer()` darf außerhalb einer Mission für Lobbyautorität den gekapselten Netzwerkstatus lesen; das ist keine Missionsfreigabe.
- Neue Features funktionieren standardmäßig auch im Karteneditor (außer ausdrücklich ausgeschlossen). `BugfixesAndQoL`-, `Pathfinding`- und Testmod-Features bleiben in jedem Modus aktiv. Andere Mods nur im Editor und in den von `GameModeSnapshot.AllowsCustomGameMods` bzw. `GameplayModModePolicy` zugelassenen Custom-Kontexten; Vanilla-Kampagnen, -Einzelmissionen und unveränderte Trails bleiben ausgeschlossen.
- Missionsmeldungen: `Context.IsSave`, `Context.Mode`, `Context.IsHost` (Startart und MP-Status sind unabhängig). Diagnoselogs enthalten Sitzungskennung und `GameModeSnapshot.ToDiagnosticString()`; unbekannte Angaben bleiben unbekannt.

## Multiplayer-Tests

- Nur nachweislich echte MP-Läufe auswerten: Host- und Clientlog zeigen einen `GameModeSnapshot` mit mehreren menschlichen Teilnehmern (beliebiger Spielstart oder `gameMembers != null` genügt nicht).
- Neben kleinen Hoständerungen nach Beitritt immer prüfen: Hostwerte vor Beitritt, frischer Client mit abweichenden Defaults, große Settings deutlich über einem Netzwerkfragment, Übergang Lobby/Kartenstart, Änderungen nach Kartenstart.
- Bei Ausfällen mit einer funktionierenden Mod auf demselben Shared-/Extender-Pfad vergleichen; Änderungszeitpunkt, Payloadgröße und Empfangsphase getrennt untersuchen.
- Client-Log: `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` (siehe Root-`AGENTS.md`).
- Zwei-PC-Tests laufen automatisiert ohne Maus/Tastatur: Der Paired-Runner startet den Host unsichtbar, schreibt Szenarien und Join-Datei auf die Client-Freigabe und stimmt beide Seiten über Checkpoint-Dateien ab. Das Client-Spiel startet per Fernstart-Wächter, den der Nutzer bei anstehenden Tests auf dem Client selbst startet (`_Automation\Start-ClientStartWatcher.bat`); es gibt bewusst keinen Autostart bei der Anmeldung. Fehlt der Herzschlag `_Automation\watcher.alive`, vor dem Lauf beim Nutzer nachfragen statt auf einen manuellen Start zu warten. Ablauf, Schritte und Grenzen: [AutomatisierungTest.md](../AutomatisierungTest.md#echter-multiplayer).
- Spielgeschwindigkeit in Zwei-PC-Tests: Der Vanilla-Lobbyregler endet bei 80. Schneller geht es nur in der Partie über BugfixesAndQoLs synchronisierte Zeitsteuerung, in echtem MP mit zwei Menschen begrenzt durch die Host-Einstellung `MultiplayerGameSpeedMaximum` (höchstens 200). 250–300 wie in Einzelspielertests ist im echten MP nicht möglich.
- Testwelt in Zwei-PC-Tests (24.11.2026): Modsettings nur auf dem **Host** zurücksetzen und setzen (`ModSettingsDefaults`, `SetModSetting`, danach Burg-Spawn von CastlePlanner und ImprovedHunters ausdrücklich abschalten); der Runner sichert dort den ganzen Ordner `Documents\SHCDE\APIShared`. Die persönlichen Einstellungen des Clients liegen auf keiner erreichbaren Freigabe und dürfen deshalb im Test nicht geändert werden; spielrelevante Host-Einstellungen erhält der Client ohnehin synchronisiert.
- Lobby-Bereitmeldung und MP-Geschwindigkeit sind nicht beim ersten Versuch verlässlich: APIShareds Abgleich der Produktionsregeln lehnt ein frühes „Bereit“ kurz ab (geloggt als Error `LOBBY_LAUNCH_BLOCKED … worker-production-agreement`, wenige Sekunden später „All players are ready“), und eine Geschwindigkeitsanfrage direkt beim Missionsstart kann eingereiht werden, ohne je anzukommen. Testschritte wiederholen beides gedrosselt, bis der Zustand erreicht ist (`Mp.Ready`, `Mp.SetSpeed` nach laufender Simulation), und erlauben im Bereit-Schritt diese erwartete Error-Zeile.
- Speichern und Neuladen im selben Spielstart (24.11.2026): Verließ der Client die Partie 20 s vor dem Host, hing das anschließende MP-Laden nach dem nativen Laden (Client „Verbindung…“, Host ohne laufende Simulation). Mit der Reihenfolge Host zuerst, dann Client lief derselbe Ablauf manuell fehlerfrei. Testabläufe verlassen deshalb in dieser Reihenfolge; die genaue Ursache ist nicht geklärt.
- Für manuelle Zwei-PC-Tests: `Run-Paired.ps1 -Manual` richtet Layout, Debug-Logging und den passiven Beobachter (`Observe`, Szenarien `manual-observe-*`) ein, startet kein Spiel, beobachtet beide Seiten minütlich und wertet nach dem Beenden automatisch aus (`report.txt`: F1-Ladevorgänge, Fehler, Host-Client-Vergleich je Tick, Stillstände, neue Spielstände). Ausführlich: [AutomatisierungTest.md](../AutomatisierungTest.md#echter-multiplayer).
- Für MP-Ladevorgänge loggt APIShared `SAVE_SETTINGS_CONFIRMED` nur mit `[Diagnostics] Enabled=true` in `BepInEx\config\APIShared_Serp.cfg` (das Debug-Level allein reicht nicht). Ohne Debug-Logging den Erfolg am tatsächlich geladenen Spielstand auf beiden Peers und am Fehlen der Fehler-/Timeoutmarker festmachen.
