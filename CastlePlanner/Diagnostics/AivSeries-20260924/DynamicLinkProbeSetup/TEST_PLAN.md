# Gezielte Prüfung dynamischer Start-Linkgruppen

Die installierte Native-DLL hat SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die Karte `test AI overbuild eachother.map` hat SHA-256
`D63CD2FF3AEABA80BC3BC173BB615207666F1EAF759ECAC573FAD0DA61979DF3`.
Script Extender 2.10.1, ActiveAIVDetector und AivLobbyPresetTest sind
installiert. Die bisherige Serienkonfiguration und ihr Fortschritt
wurden vor dem Wechsel in diesem Ordner gesichert.

Die aktive Testserie `aiv-dynamic-link-probe-20260924` enthält vier
Kartenstarts in genau dieser Reihenfolge:

| Lauf | Completed Castles | Spieler 2 | Spieler 3 | Spieler 4 | Spieler 5 |
| --- | --- | --- | --- | --- | --- |
| `DL-forward-off` | aus | Sentinel, Default 2, Slot 0 | Wolf, Default 8, Slot 3 | Nizar, Default 6, Slot 1 | Marshal, Default 7, Slot 2 |
| `DL-forward-on` | an | Sentinel, Default 2, Slot 0 | Wolf, Default 8, Slot 3 | Nizar, Default 6, Slot 1 | Marshal, Default 7, Slot 2 |
| `DL-reverse-off` | aus | Wolf, Default 8, Slot 3 | Sentinel, Default 2, Slot 0 | Nizar, Default 6, Slot 1 | Marshal, Default 7, Slot 2 |
| `DL-reverse-on` | an | Wolf, Default 8, Slot 3 | Sentinel, Default 2, Slot 0 | Nizar, Default 6, Slot 1 | Marshal, Default 7, Slot 2 |

Der Mensch bleibt auf Slot 4. Alle AIVs sind **eingebaute Default-Dateien**,
keine gleichnamigen Extended-Dateien. Für Sentinel Default 2 ist der
Keep-Marker `8144` belegt; beim Kartenstart auf Slot 0 ist die erste
vollständig passende AIV-Drehung offline 270°. Der versetzte Start kann
den nachfolgenden Slot-3-Start in die Nähe einer zur Laufzeit gebauten
Linkgruppe bringen. Das ist ein gezielter Auslöseversuch, noch kein
Beleg, dass der native Gruppenlöschzweig tatsächlich erreicht wird.

Pro Lauf eine **neue lokale Skirmish-Lobby** öffnen, die automatisch
gesetzte Aufstellung und Completed-Castles-Option nicht verändern und
die Karte bis zum sichtbaren Spielbeginn starten. Auf späteren normalen
KI-Bau muss nicht gewartet werden. Zwischen den vier Läufen muss der
Spielprozess nicht beendet werden. Nach dem vierten Start das Spiel
beenden und die Auswertung anfordern. Der Testmod rückt nur nach einem
bestätigten Start weiter; bei einer abweichenden Lobby bleibt derselbe
Lauf aktiv.

Ausgewertet werden vollständige Native-Fits, Keep-Startrecords,
Cleanup-Linkwerte, mögliche Mehrfachlöschung, Abbruchflags und
Sofortbau-Frames. Die Diagnose ist generisch; feste Spieler-IDs und
Positionen stehen ausschließlich in dieser Testserie.
