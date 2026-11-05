# PlayerControlRelayTest testen

Experimenteller Stand 0.1.0, geprüft am 05.11.2026. Kein automatischer Einbau
ins Spiel oder ins veröffentlichte Modpack.

## Umfang

Der Mod testet die Steuerung verbündeter Einheiten mit der normalen Maus, ohne eigenes
Fenster oder eigene Schaltfläche, sowie Bewegen und Stopp. Die Freigabe
kommt vom Besitzer-PC, für KI-Besitzer vom Host. Anschließend führt jeder Peer
denselben authentifizierten Chore in der Simulation aus. Die Auswahl selbst
erzeugt keine echten Auswahlbefehle, Tribes oder Mitgliedschaftsänderungen; das
geschieht erst beim Befehl.

Die Verbraucherregel dieses Testmods erlaubt eigene und verbündete Einheiten
automatisch. Diese Entscheidung gehört dem Testmod; APIShared bekommt dadurch
keine allgemeine Freigaberegel.

Der Befehl gilt genau für die angeklickten Einheiten, von einer einzelnen bis zu
beliebig vielen. Sind sie nicht genau eine ganze Gruppe, bildet jeder Peer im Chore
daraus eine neue Gruppe, wie Vanilla bei eigener Auswahl. Eine Auswahl, die nicht
in ein Paket passt (etwa 500 bis 1000 Einheiten, je nach Abstand der IDs), wird auf
mehrere Gruppen und Chores verteilt. Gestorbene Einheiten fallen weg.

Grenzen: Einheiten, die ihr Besitzer gerade selbst ausgewählt hat, werden übersprungen
und im Log gezählt. Lords sind nicht wählbar. Die KI läuft weiter und kann spätere
Befehle erteilen. Aufträge für mehrere Besitzer sind getrennte Vorgänge. Bereits
eingereichte Aufträge werden bei späterem Allianzwechsel nicht zurückgerufen.

Gebäudebefehle, Angriff und Rekrutierung sind noch keine implementierten Testfälle.
Der native Vertrag und die Gründe für diese Grenzen stehen in `UpdateToNewDLL.md`.

## Paket und Voraussetzungen

`build.bat /nopause` baut den Arbeitsstand gegen ein geprüftes, isoliertes
APIShared-develop-SDK. Die aktuelle lokale APIShared-Branch wird nicht gewechselt.
Das Paket liegt unter `.local/PlayerControlRelayTest.zip`; `.local/build.json`
dokumentiert Quellhashes, SDK-Commit und Paketidentität. Das Skript installiert
nichts. Die ZIP enthält den Pfad `BepInEx/plugins/PlayerControlRelayTest_Serp/`.

Voraussetzungen: Script Extender mindestens 2.14.0 und öffentliches APIShared
mindestens 0.27.0, jeweils mit zusammengehörigen Plugins und Patchern. Es wird
kein APIShared-WIP-Build benötigt. Alle Multiplayer-Teilnehmer benötigen
denselben Testmod und zueinander passende Abhängigkeiten und Gameplay-Mods.
Der native Backend-Vertrag ist an den in `UpdateToNewDLL.md` genannten DLL-Hash
gebunden; eine andere native Version bleibt gesperrt.

## Bedienung

Ein Verbündeter braucht dasselbe Team wie du, also in der Lobby denselben Team-Schild.
Ein Zuschauer hat keinen eigenen Spieler und keine Verbündeten, dort bleibt die
Funktion inaktiv; das Log nennt das einmal.

1. Partie starten und im Log `Relay ready` abwarten. Das dauert etwa eine Sekunde.
2. Verbündete Einheit anklicken oder einen Rahmen ziehen; Umschalt fügt hinzu. Genau
   diese Einheiten erhalten grüne Rauten. Eigene Einheiten wählt das Spiel wie gewohnt,
   beides darf gleichzeitig ausgewählt sein.
3. Auf den Boden klicken: Die Einheiten laufen dorthin, eigene ausgewählte Truppen wie
   gewohnt auch. Die normale Stopp-Taste stoppt sie.
   Die KI läuft weiter und kann neue Befehle geben. Im automatischen Test (05.11.2026,
   frisch erzeugte Bogenschützen) bekam eine Einheit in ihrer alten KI-Gruppe binnen
   1,5 Sekunden wieder ein KI-Ziel. Neu gebildete Gruppen erreichten dagegen ihr Ziel und
   blieben 51 Sekunden unverändert; ob das in längeren Partien gilt, ist offen.
   Lehnt das Spiel das Ziel ab, meldet das Log `Uncertain`, und die Einheiten bleiben in
   ihrer neuen Gruppe, bis der nächste Befehl gelingt.
4. Rechtsklick, ein Klick auf eine eigene Einheit oder ein Rahmen ohne Verbündete
   leert die Auswahl. Ein Klick auf einen Gegner bewirkt nichts.
5. Status und BepInEx-Log prüfen. `Executed` bestätigt den abgeschlossenen nativen
   API-Aufruf, keine Ankunft am Ziel, und nennt Anzahl, Gruppe und übersprungene
   Einheiten. `Rejected` bedeutet: kein nativer Aufruf. `Uncertain` heißt: Es gab einen
   nativen Aufruf ohne bestätigten Erfolg. Es gibt keine automatische Wiederholung. Ein
   hängender Auftrag blockiert weitere Aufträge desselben Besitzers bis zum nächsten
   Weltstart; das vermeidet Doppelbefehle.

## Abnahme im Spiel

`UiAcceptance` mit `SP-relay.json` prüft im Einzelspieler den echten Spielaufruf
`EditorDirector.preDLLCallActions`. Ersetzt sind dabei nur Maustaste, Einheiten unter
dem Mauszeiger und Mauskachel. Abgedeckt sind die Auswahl von 1, 7, 2 (mit Umschalt)
und 140 Einheiten, deren Bewegungsziel, die tatsächliche Bewegung, Stopp, Rechtsklick und
rund 50 Sekunden Weiterlaufen mit neu gebildeten KI-Gruppen. Die Bedienung mit der echten
Maus und alles Folgende ersetzt das nicht:

- Zuerst mit deklarierten Abhängigkeiten, danach mit dem normalen Modsatz testen.
  Einzelspieler und Editor einschließen, soweit deren Lifecycle/Netzwerkpfad die
  Bereitschaft erreicht. Ein fehlender Pfad muss deaktiviert bleiben.
- Echte Host/Client-Partie: Host steuert Client-Einheiten und umgekehrt; verbündete
  KI-Einheiten; nicht verbündete Einheiten dürfen nicht freigegeben werden.
- Besitzer ändert seine echte Auswahl, während der andere Spieler privat auswählt
  oder ein Auftrag unterwegs ist. Beim Auswählen dürfen keine zusätzlichen Chores
  oder Tribes entstehen. Vom Besitzer ausgewählte Einheiten werden übersprungen.
- Teilauswahl aus einer Rekrutengruppe, ganze Gruppe, mehr als 1000 Einheiten, Befehl
  im Kampf mit Verlusten, unpassierbares Ziel, Stop während besonderer Bewegung und
  gleichzeitige Besitzerbefehle prüfen. Nicht ausgewählte Einheiten müssen unverändert
  bleiben.
- Normale Bedienung bleibt unverändert: eigene Auswahl, Gebäude, Platzierung und
  Klicks auf das HUD lösen keinen Befehl an Verbündete aus; das gilt auch für den
  Platzierungsmodus der Spectator-Toolbar. Ausnahme: Ein Klick auf ein eigenes Gebäude
  schickt ausgewählte Verbündete dorthin. Das Log enthält kein „delivery rejected“.
- Speichern/Laden, neue Karte und Missionsende prüfen. Alte Rückmeldungen und
  Aufträge dürfen keine neue Welt oder einen neueren Auftrag treffen.
- Auf beiden PCs protokollierte Request-ID, Besitzer, Ausführungstick und
  Ergebnis vergleichen; danach Desync-/Checksum-Verhalten beobachten.

Statische Prüfung und Tests bestätigen die Grenzen der Implementierung.
Rendering, echte Netzwerkzustellung, Spielverhalten und kombinierte Modwirkung
sind erst durch diese Spieltests nachgewiesen.
