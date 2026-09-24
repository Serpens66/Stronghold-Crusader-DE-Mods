# Vier-Match-Test: native Record-Verknüpfung beim KI-Start

Vorherige abgeschlossene Serienkonfiguration und Fortschritt liegen als
`Previous-*` in diesem Ordner. Die neue Serie
`aiv-cleanup-link-probe-20260924` ist in
`Testmods/AivLobbyPresetTest/AivLobbyCleanupLinkProbeSeries.json`
definiert und in der Spielkonfiguration auf `nextIndex: 0` installiert.
Die vier unveränderten, bereits validierten Craggy-Cliffs-Aufstellungen
aus `AivLobbyStartRebuildRegressionSeries.json` laufen in dieser Reihenfolge:

1. `CC-A-off` – „Completed Castles“ aus.
2. `CC-A-on` – „Completed Castles“ an.
3. `CC-B-off` – „Completed Castles“ aus.
4. `CC-B-on` – „Completed Castles“ an.

Map-SHA-256: `C46B71C941EA299D1CA82C4F9649601E41F80517F05885ECDDA39DEEE5E4EF25`.
Native-SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Jeder Lauf enthält sieben KI-Spieler. Der Detector bleibt unabhängig von
festen Spieler-IDs. Seine neu gebaute Record-Spalte
`nativeCleanupLinkId` liest das Feld bei installiertem
`GameBuilding`-Offset `0x2A8` (`r_UsedInSiegeAttemptId`); die früheren
Traces hatten nur `r_GlobalId` aufgezeichnet.

## Spielablauf

Pro Lauf eine neue lokale Skirmish-Lobby öffnen, die automatisch gesetzte
Aufstellung und „Completed Castles“ unverändert lassen und die Karte
bis zum sichtbaren Spielbeginn starten. Auf regulären späteren KI-Bau
muss nicht gewartet werden. Alle vier Kartenstarts dürfen in einem
Spielprozess erfolgen. Der Fortschritt darf nur nach einem bestätigten
Kartenstart weiterlaufen. Nach dem vierten Lauf das Spiel beenden und
die Auswertung anfordern.

## Entscheidungsfrage

Die neuen Record-Snapshots zeigen, ob bei den beobachteten 60- und
16-Zellen-Räumungen ein nichtnuller Cleanup-Link-Wert vorlag und ob
weitere Records denselben Wert teilten. Der Aus/An-Vergleich trennt
Start-Räumung von vorausgegangenem Sofortbau. Zusammen mit vollständigen
Tile-Diffs und Native-Fit-Traces wird geprüft, ob für diese Zweige eine
sichere, engere Zustandsgrenze folgt. Wenn der Link-Wert null ist oder
kein fehlgeschlagener Start vorkommt, bleiben die anderen Gruppen-
beziehungsweise Abbruchzweige weiterhin unbelegt; einzelne beobachtete
Läufe werden nicht als allgemeine Freigabe verwendet.
