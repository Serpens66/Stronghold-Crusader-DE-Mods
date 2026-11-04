# PlayerControlRelayTest testen

Experimenteller Stand 0.1.0, geprüft am 04.11.2026. Kein automatischer Einbau
ins Spiel oder ins veröffentlichte Modpack.

## Umfang

Der Mod testet private Einheitenauswahl sowie Bewegen und Stopp. Die Freigabe
kommt vom Besitzer-PC, für KI-Besitzer vom Host. Anschließend führt jeder Peer
denselben authentifizierten Chore in der Simulation aus. Die Auswahl selbst
erzeugt keine echten Auswahlbefehle, Tribes oder Mitgliedschaftsänderungen.

Die Verbraucherregel dieses Testmods erlaubt eigene und verbündete Einheiten
automatisch. Diese Entscheidung gehört dem Testmod; APIShared bekommt dadurch
keine allgemeine Freigaberegel.

Unterstützt werden vollständige bestehende Gruppen, insgesamt höchstens
64 Einheiten pro Auftrag. Eine echte Teilgruppe wird abgelehnt und niemals
stillschweigend erweitert. Die KI läuft weiter und kann spätere Befehle erteilen.
Aufträge für mehrere Besitzer sind getrennte Vorgänge. Bereits eingereichte
Aufträge werden bei späterem Allianzwechsel nicht zurückgerufen.

Gebäudebefehle, Angriff, Rekrutierung und beliebige Teilgruppen sind noch keine
implementierten Testfälle. Der native Vertrag und die Gründe für diese Grenzen
stehen in `UpdateToNewDLL.md`.

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

1. Partie oder unterstützten Editor starten und die Relay-Bereitschaft abwarten.
   Die Schaltfläche `PC` erscheint im Mod-Bereich des Seiten-HUDs erst dann.
2. Laufende Platzierung oder Auswahlgeste beenden und das Testfenster öffnen.
   Währenddessen ist die private Auswahloberfläche modal.
3. Besitzerfilter wählen. Einheiten anklicken oder einen Rahmen ziehen;
   Umschalt fügt hinzu. Der Filter wirkt auf neue Auswahlgesten, nicht auf eine
   bereits zusammengestellte Auswahl. `Auswahl leeren` beginnt neu.
4. Für einen positiven Test alle Mitglieder einer bestehenden Gruppe erfassen.
   `Bewegen: Ziel wählen` und danach ein Kartenziel anklicken, oder `Stopp` wählen.
   Rechtsklick bricht den Zielmodus ab; Escape schließt das Fenster.
5. Status und BepInEx-Log prüfen. `Executed` bestätigt abgeschlossene native
   API-Aufrufe, keine Ankunft am Ziel. Bei unbekanntem Ausgang gibt es keine
   automatische Wiederholung. Ein hängender Auftrag blockiert weitere Aufträge
   desselben Besitzers bis zum nächsten Weltstart; das vermeidet Doppelbefehle.

## Abnahme im Spiel

Die Offline-Prüfung ersetzt diese noch ausstehende Abnahme nicht:

- Zuerst mit deklarierten Abhängigkeiten, danach mit dem normalen Modsatz testen.
  Einzelspieler und Editor einschließen, soweit deren Lifecycle/Netzwerkpfad die
  Bereitschaft erreicht. Ein fehlender Pfad muss deaktiviert bleiben.
- Echte Host/Client-Partie: Host steuert Client-Gruppe und umgekehrt; verbündete
  KI-Gruppe; nicht verbündete Einheiten dürfen nicht freigegeben werden.
- Besitzer ändert seine echte Auswahl, während der andere Spieler privat auswählt
  oder ein Auftrag unterwegs ist. Es dürfen keine zusätzlichen Auswahl-Chores
  oder Tribes entstehen. Eine mittlerweile geänderte Gruppenmitgliedschaft darf
  abgelehnt werden.
- Teilgruppe, mehr als 64 Einheiten, tote oder wiederverwendete Unit-ID,
  unpassierbares Ziel, Stop während besonderer Bewegung und gleichzeitige
  Besitzerbefehle prüfen. Nicht ausgewählte Gruppen müssen unverändert bleiben.
- Öffnen/Schließen bei gehaltenen Tasten, Escape, Klicks über dem Panel und
  vorheriger Vanilla-Auswahlgeste prüfen: kein unbeabsichtigter Spielbefehl.
- Speichern/Laden, neue Karte und Missionsende prüfen. Alte Rückmeldungen und
  Aufträge dürfen keine neue Welt oder einen neueren Auftrag treffen.
- Auf beiden PCs protokollierte Request-ID, Besitzer, Ausführungstick und
  Ergebnis vergleichen; danach Desync-/Checksum-Verhalten beobachten.

Statische Prüfung und Tests bestätigen die Grenzen der Implementierung.
Rendering, echte Netzwerkzustellung, Spielverhalten und kombinierte Modwirkung
sind erst durch diese Spieltests nachgewiesen.
