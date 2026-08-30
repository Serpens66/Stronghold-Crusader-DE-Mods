# SHCDESE 1.43.2: Bewertung der entfernten ChoreNetworkTransport-API

## Entscheidung

Fuer dieses Thema ist kein Upstream-Report und kein Patch des Script Extenders erforderlich.

Unsere Mods benoetigen fuer bestimmte Multiplayer-Simulationsaenderungen weiterhin den tick-ausgerichteten Chore-Transport. Sie benoetigen aber nicht die in 1.43.2 entfernte Klasse `ChoreNetworkTransport`. Der verbleibende oeffentliche Aufruf `GameNetworkAPI.SendPacketToAllEx2<T>(..., viaChore: true)` kann mit modseitigen Vorpruefungen so verwendet werden, dass der aktuelle 1.43.2-Code nicht auf Steam zurueckfaellt.

Die Migration ist damit vollstaendig im Workspace-Modcode loesbar. Der kanonische Script Extender muss weder geaendert noch gepatcht werden.

## Geht der Entfernungsgrund aus dem Extender hervor?

Nicht ausdruecklich. Der Entfernungscommit `01d2bc63f02e3ebccd3ac18b296b8d0ed062d8c5` traegt nur den Titel `feat: Further improved Chore Networking API`; weder Committext noch Changelog erklaeren, weshalb die oeffentliche Klasse entfallen sollte.

Der Diff erlaubt aber eine klare technische Einordnung: Gleichzeitig mit der Loeschung von `ChoreNetworkTransport.cs` wurde der rohe Sendepfad als interne Methode nach `GameNetworkAPI` verlegt, `SendPacketToAllEx2` direkt daran angebunden und die bisherige Delegate-/`_isSending`-Zwischenschicht in `BulkChoreDetours` durch direkte native Funktionswrapper mit expliziter `ChorePhase` ersetzt. Die entfernte Klasse war damit fuer den Extender selbst nur noch eine redundante Vermittlungsschicht. Das ist eine aus dem Code abgeleitete Erklaerung, keine dokumentierte Absicht des Autors.

Dass die Dokumentation weiterhin auf `ChoreNetworkTransport.IsAvailable` verweist, spricht zudem fuer eine nicht vollstaendig nachgezogene API-/Dokumentationsbereinigung. Daraus folgt jedoch kein technischer Bedarf, die Klasse fuer unsere Mods wiederherzustellen.

## Tatsaechlicher Vertrag in 1.42.0

`ChoreNetworkTransport.SendRawBlob` vermittelte den Eindruck einer bestaetigten, fail-closed Queue-Operation. Der Quellcode zeigt jedoch einen schwaecheren Vertrag:

- `false`, wenn `GameGlobalsManager.Instance.ChoreManagerVA == 0` war;
- `false`, wenn die gesamte Chore-Payload groesser als 1200 Bytes war;
- andernfalls Aufruf der nativen Queue-Funktion mit Rueckgabetyp `void`;
- `true`, wenn dieser native Aufruf ohne Exception zurueckkehrte.

Der Rueckgabewert war daher weder eine Empfangsbestaetigung noch eine native Bestaetigung, dass alle Teilnehmer das Chore erhalten werden. Er bestaetigte nur die beiden Vorbedingungen und die normale Rueckkehr des nativen Aufrufs.

`ChoreNetworkTransport.IsAvailable` pruefte lediglich, ob der Extender seinen Delegate bereits gesetzt hatte. Im normalen Lifecycle geschah dies beim Aufbau von `BulkChoreDetours`.

## Tatsaechlicher Vertrag in 1.43.2

In 1.43.2 wurde derselbe rohe Sendepfad nach `GameNetworkAPI.SendScriptExtenderChorePayload(byte[])` verschoben und auf `internal` gesetzt. Die Methode prueft weiterhin exakt:

- `ChoreManagerVA != 0`;
- Payloadlaenge hoechstens 1200 Bytes.

Danach ruft sie weiterhin dieselbe Art nativer `void`-Queue-Funktion auf. `GameNetworkAPI.SendPacketToAllEx2<T>(..., viaChore: true)` verwendet diesen internen Pfad und faellt nur dann auf Steam zurueck, wenn eine dieser beiden Pruefungen `false` ergibt. Eine beim nativen Aufruf entstehende Exception loest keinen Steam-Fallback aus, sondern verlaesst den Aufruf.

Die Initialisierungsreihenfolge ist ebenfalls ausreichend:

1. `FindGameGlobals(memory)` ermittelt die nativen Adressen.
2. `DetourManager.ApplyNative(memory)` erstellt `BulkChoreDetours` und die nativen Funktionswrapper.
3. Erst danach werden die Netzwerk-Subscriber initialisiert.
4. Unsere Mods registrieren ihre Packet-Hooks nach `LibraryLoaded` und senden erst nach ihrer eigenen Netzwerkinitialisierung.

## Zuverlaessige modseitige Migration

Ein gemeinsamer `TrySendChore<T>`-Helper soll fuer jeden simulationskritischen Aufruf folgenden Vertrag erzwingen:

1. Die mod-eigene Netzwerkinitialisierung ist abgeschlossen und der typisierte Packet-Hook existiert.
2. Das Packet wird vorab mit `GameNetworkAPI.Serialize(packet)` serialisiert.
3. `sizeof(short) + body.Length` ist hoechstens 1200 Bytes.
4. `GameGlobalsManager.Instance.ChoreManagerVA` ist ungleich null.
5. Das Packetobjekt wird zwischen Vorabserialisierung und Send-Aufruf nicht mehr veraendert.
6. Danach wird `GameNetworkAPI.SendPacketToAllEx2(packet, packetId, viaChore: true)` aufgerufen.
7. Eine Exception wird geloggt und als Sendefehler behandelt.
8. Der Absender nimmt keine separate lokale Simulationsaenderung vor. Die Aenderung erfolgt ausschliesslich beim Chore-Empfang, der auch den Absender einschliesst.

Wenn die Punkte 2 bis 5 erfuellt sind, kann die interne 1.43.2-Methode weder wegen Uebergroesse noch wegen fehlendem Manager `false` liefern. Der Steam-Fallback ist in dieser Version damit vor dem Aufruf ausgeschlossen. Kehrt der oeffentliche Aufruf normal zurueck, besitzt der modseitige Helper dieselbe praktische Erfolgssemantik wie `SendRawBlob` in 1.42.0.

Die doppelte Serialisierung ist akzeptabel, weil unsere Packettypen explizite Formatter verwenden und das lokale Packet waehrend des Aufrufs unveraendert bleibt. Bei einer spaeteren Script-Extender-Version muss dieser implementierungsbezogene Vertrag erneut geprueft werden.

## Betroffene Mods und Funktionen

Chore bleibt fachlich notwendig, aber die rohe API wird ersetzt:

- `BugfixesAndQoL`: Assassin-Climb, Multiplayer-Spieltempo, Belagerungsmunition und Kapitulation. Der vorhandene direkte Entpausierpfad bleibt eine bewusste Sonderloesung, weil eine pausierte Simulation kein neues Chore abarbeiten kann.
- `ExtraFeatures`: Torautomatik, Einzelgebaeude-Pause, Rittertransformation und Steinbruchhaufen-Verschiebung.
- `RandomEvents`: Initialisierung, Eventbatches und Wegweiseraktionen; die bestehende Ein-Chore-pro-Tick-Regel bleibt erhalten.
- `ChoreTestMod`: nur Diagnose. Er wird auf den neuen oeffentlichen Pfad umgestellt oder nach erfolgreicher Abnahme stillgelegt.

Die vorhandenen variablen Payloadpruefungen sind bereits weitgehend ausreichend:

- `RandomEvents` prueft hoechstens 1200 Bytes inklusive Packet-ID.
- Die Rittertransformation erlaubt hoechstens 1198 Body-Bytes plus zweibyte Packet-ID.
- Siege-Restock lehnt mit `>= 1200` den exakten Grenzwert sogar konservativ ab.
- Die uebrigen Chore-Packets sind fest und deutlich kleiner.

## Nicht erforderliche oder ungeeignete Alternativen

- Kein Reflection-Zugriff auf die interne `SendScriptExtenderChorePayload`-Methode.
- Kein direkter Aufruf der nativen Wrapper aus `BulkChoreDetours`.
- Keine modseitige Kopie der entfernten `ChoreNetworkTransport`-Klasse; sie waere nicht mit den internen Extenderfeldern verbunden.
- Kein allgemeiner Ersatz durch `SendPacketToAll` und lokale Sofortausfuehrung, weil dies die Tick-Ausrichtung aufhebt.
- Kein eigener Steam-basierter Ziel-Tick-Scheduler. Dieser muesste Verspaetungen, Pause, Paketverlust, Senderausfuehrung und Resynchronisierung selbst loesen und waere unnoetig komplexer und fehleranfaelliger als der vorhandene Chore-Pfad.

## Wichtigkeit und Abschluss

Das Thema ist fuer den Quellbuild wichtig, weil vier Projekte die entfernte Klasse direkt referenzieren und gegen 1.43.2 nicht kompilieren. Es ist aber kein Script-Extender-Blocker und kein Beleg fuer unzuverlaessigen Multiplayer in 1.43.2.

Nach der beschriebenen Migration koennen die Mods ohne Script-Extender-Aenderung mit derselben praktischen Chore-Sendesicherheit wie unter 1.42.0 arbeiten. Abschliessend sind ein Build gegen die echte 1.43.2-Assembly sowie ein Host-/Client-Test erforderlich, der gleiche Operation-ID, gleichen Ausfuehrungstick, genau eine Anwendung pro Teilnehmer und ausbleibende Steam-Fallbacks belegt.

Ergebnis: kein Upstream-Report fuer die entfernte API. Die notwendige Arbeit ist eine lokale Modmigration und Multiplayer-Abnahme.

## Versionsnachweis

- Letzte Version mit `ChoreNetworkTransport`: `v1.42.0` (`171d68e155a8f98c5f8c4ee154d9af154c9a2443`)
- Gepruefte Zielversion: `v1.43.2` (`ac291f23d52435018d7851db288c17668c4a171f`)
- Entfernungscommit: `01d2bc63f02e3ebccd3ac18b296b8d0ed062d8c5`
