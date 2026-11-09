# PlayerControlRelayTest testen

Experimenteller Stand 0.1.0, geprüft am 05.11.2026. Kein automatischer Einbau
ins Spiel oder ins veröffentlichte Modpack.

## Umfang

Der Mod testet die Steuerung verbündeter Einheiten mit der normalen Maus und Vanillas
Truppen-HUD: Bewegen, Stopp, Haltung, Patrouille, Auflösen und „Hier angreifen“. Die Freigabe
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

Einheiten, die ihr Besitzer gerade selbst ausgewählt hat, erhalten den Befehl ebenfalls.
Wird dabei eine neue Gruppe gebildet, sieht der Besitzer die Einheit weiter als ausgewählt;
sein nächster Befehl erreicht sie erst, wenn er neu auswählt. Einen verbündeten Lord wählt
ein Klick, ein Rahmen nimmt ihn wie in Vanilla nicht mit. Die KI läuft weiter und kann spätere
Befehle erteilen. Aufträge für mehrere Besitzer sind getrennte Vorgänge. Bereits
eingereichte Aufträge werden bei späterem Allianzwechsel nicht zurückgerufen.

Klick auf Gegner, Spezialknöpfe (Öl, Tunnel, Belagerungsbau, Nachladen, Kuh), Vanillas eigene
Befehle für Spezialgebäude und Rekrutierung sind noch keine implementierten Testfälle. Die
Spezialknöpfe sind für verbündete Einheiten ausgeblendet.
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
   diese Einheiten erhalten grüne Rauten, und Vanillas Truppen-HUD öffnet sich. Eigene Einheiten
   wählt das Spiel wie gewohnt, beides darf gleichzeitig ausgewählt sein.
3. Die Maus folgt der Vanilla-Einstellung `SH1RTSControls`, eine Änderung gilt sofort:

   | | Befehl per Linksklick | Befehl per Rechtsklick |
   |---|---|---|
   | Bewegen | Linksklick auf den Boden | Rechtsklick |
   | Auswahl leeren | Rechtsklick | Linksklick auf den Boden |

   Ein Klick auf eine eigene Einheit oder ein Rahmen ohne Verbündete leert die Auswahl, Umschalt
   verhindert das. Ein Klick auf einen Gegner gibt keinen Befehl.
4. HUD und Tasten wirken wie in Vanilla: Stopp, Haltung und Auflösen sofort. Patrouille und „Hier
   angreifen“ bestätigt in beiden Mausmodi ein Linksklick, ein Rechtsklick bricht ab. Die Patrouille
   läuft zwischen dem aktuellen Platz der Gruppe und dem geklickten Punkt. Porträts filtern die Auswahl.
   Die KI läuft weiter und kann neue Befehle geben. Im automatischen Test (05.11.2026,
   frisch erzeugte Bogenschützen) bekam eine Einheit in ihrer alten KI-Gruppe binnen
   1,5 Sekunden wieder ein KI-Ziel. Neu gebildete Gruppen erreichten dagegen ihr Ziel und
   blieben rund 50 Sekunden unverändert; ob das in längeren Partien gilt, ist offen.
   Lehnt das Spiel das Ziel ab, meldet das Log `Uncertain`, und die Einheiten bleiben in
   ihrer neuen Gruppe, bis der nächste Befehl gelingt.
5. Status und BepInEx-Log prüfen. `Executed` bestätigt den abgeschlossenen nativen
   API-Aufruf, keine Ankunft am Ziel, und nennt Anzahl, Gruppe und übersprungene
   Einheiten. `Rejected` bedeutet: kein nativer Aufruf. `Uncertain` heißt: Es gab einen
   nativen Aufruf ohne bestätigten Erfolg. Es gibt keine automatische Wiederholung. Ein
   hängender Auftrag blockiert weitere Aufträge desselben Besitzers bis zum nächsten
   Weltstart; das vermeidet Doppelbefehle.

## Abnahme im Spiel

`UiAcceptance` mit `SP-relay.json` prüft im Einzelspieler den echten Spielaufruf
`EditorDirector.preDLLCallActions` in beiden Mausmodi. Ersetzt sind dabei nur Maustaste, Einheiten
unter dem Mauszeiger und Mauskachel; die Einstellung wird nur im Speicher umgeschaltet. Abgedeckt sind
die Auswahl von 1, 7, 2 (mit Umschalt), 138 Einheiten und dem Lord, Ziel und Bewegung, Vanillas HUD samt
Zurückziehen des Foreign-HUD, Stopp per Taste und Knopf, Haltung, Patrouille samt Abbruch, Auflösen,
„Hier angreifen“ bis zum nativen Aufruf, Porträtfilter und rund 50 Sekunden Weiterlaufen mit neu
gebildeten KI-Gruppen. Das Szenario läuft seit 09.11.2026 mit Friedenszeit, Geschwindigkeit 300 und ohne
RandomEvents. Weil die KI die Testeinheiten in der Friedenszeit nicht in eigene Gruppen übernimmt, setzt der
Schritt `inject-marks` bei jeder vierten der 138 Einheiten die KI-Ausschlussmarke `GameUnit+0x428`. `move-many`
verlangt dann, dass der Relay mindestens so viele Marken löscht (`aiMoveMarksCleared` im `Executed`-Log) und
alle 138 Einheiten das Ziel bekommen. Ziele liegen um die aktuelle Mitte der ausgewählten Einheiten. Stopp
wird an Befehlen gemessen, nicht an einer Entfernung nach fester Wartezeit: Nach dem Stopp erreicht kein
Gruppenbefehl die gestoppte Einheit, und Vanilla versetzt sie höchstens um wenige Kacheln (`RELAY_WATCH`).
Die Bedienung mit der echten Maus und alles Folgende ersetzt das nicht:

- Zuerst mit deklarierten Abhängigkeiten, danach mit dem normalen Modsatz testen.
  Einzelspieler und Editor einschließen, soweit deren Lifecycle/Netzwerkpfad die
  Bereitschaft erreicht. Ein fehlender Pfad muss deaktiviert bleiben.
- Echte Host/Client-Partie: Host steuert Client-Einheiten und umgekehrt; verbündete
  KI-Einheiten; nicht verbündete Einheiten dürfen nicht freigegeben werden.
- Besitzer ändert seine echte Auswahl, während der andere Spieler privat auswählt
  oder ein Auftrag unterwegs ist. Beim Auswählen dürfen keine zusätzlichen Chores
  oder Tribes entstehen. Eine vom Besitzer ausgewählte Einheit gehorcht und reagiert danach
  beim Besitzer wie beschrieben.
- Behobener Befund (05.11.2026 und 09.11.2026, 13 Läufe von `SP-relay.json`, KI-Besitzer, frisch erzeugte
  Bogenschützen): In 5 Läufen bekamen bei einem Befehl an 138 Einheiten 46 bzw. 33 davon kein neues Ziel, obwohl
  das Log `Executed` und `skipped=0` meldet.
  - Ursache (statisch gegen den DLL-Hash aus `UpdateToNewDLL.md`, mit den gesicherten Logs): Die KI verteilte die
    Testeinheiten reihum auf drei oder vier eigene Gruppen. Übersprungen wurden genau die Mitglieder einer dieser
    Gruppen. Der Gruppenbefehl `0x11B520` überspringt jede Einheit mit `GameUnit+0x428 != 0` (`N000000D8`, im Log
    `e84`). Dieses Feld ist die Ausschlussmarke der KI für ihren eigenen nächsten Gruppenbefehl; nur die
    KI-Routinen `0x11DD10` und `0x1245B0` setzen es, `0x126020` löscht es. Umgruppieren und die öffentliche
    Extender-API lassen es stehen, Spielerbefehle löschen es nicht. KI-Zustand 11 war nur die Folge. Kein
    Vanilla-Verhalten, das der Relay hinnehmen muss. Einzelheiten: `UpdateToNewDLL.md`, Abschnitt
    „Member exclusion mark“, und die Baseline `GROUP_MOVE_EXCLUSION_MARK.md`.
  - Korrektur (09.11.2026): Vor Move und Patrol schreibt `RelayWorld.Execute` im Chore für jede geplante Einheit
    `N000000D8 = 0`, wie Vanillas KI mit `0x126020` vor einem einfachen Gruppenbefehl. Das `Executed`-Log nennt die
    Anzahl als `aiMoveMarksCleared`.
  - Nachweis im Spiel (09.11.2026, Geschwindigkeit 300, ohne RandomEvents, Logs unter `.work/a/relay-mark/001`
    bis `004`): Ohne Friedenszeit übernahm die KI die Testeinheiten wie in den Fehlläufen, und der Relay löschte
    vor dem Befehl an 138 Einheiten 125, 43, 98 und 120 Marken; alle 138 bekamen jedes Mal das Ziel. Mit
    Friedenszeit und eingesetzten Marken: 3 von 3 Läufen vollständig bestanden (35 Marken gesetzt, 35 gelöscht,
    138 von 138 Einheiten befohlen).
  - Offen: Einheiten echter Spieler sind statisch nicht betroffen, weil nur KI-Routinen die Marke setzen; im
    Spiel ungeprüft. In einem Lauf mit Friedenszeit ohne Marken (Ordner `001`, Lauf 2) lief der Lord nach dem
    Relay-Befehl nicht los, sein Ziel stand nach 6 s wieder auf seiner eigenen Kachel. Die Ursache ist nicht
    geklärt; seitdem zeichnet `RELAY_WATCH` den Lord pro Tick auf, und in den drei damit aufgezeichneten Läufen lief er.
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
