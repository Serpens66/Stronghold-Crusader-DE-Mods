# Handelsfix: Lifecycle, Saves und gemeinsame Hooks

Stand: 22.11.2026. Entwicklungsstand, keine Veröffentlichung oder Multiplayerfreigabe.
Version des Testmods bleibt `0.1.0`; installiert ist der `/devdiag`-Build mit
SHA-256 `7C8CBFBBE91723D16E78BD50B9D4DD482AAFABC4B8CCA9EDF889DBFAD71B3F03`.
APIShared `0.62.0` wurde aus dem Arbeitsstand vollständig geprüft, gebaut und
einschließlich seines Bootstrap-Patchers installiert. Quellstände und Paketidentitäten:
[Installation](../../../.work/a/market-modes/001/installation.json),
[Modbuild](../../../.work/a/market-allgoods/001/install7.json),
[SDK-Buildlog](../../../.work/a/market-modes/001/api-build.log).
Es gab keine Commits, Branchwechsel oder Veröffentlichungen.

## Verhalten und Absicherung

- Gameplay-Aktivierung und Liefergutschriften hängen nicht mehr von Diagnosepuffern,
  Schreibfortschritt oder Beobachtungsfehlern ab. Der Editor ist ausdrücklich ausgeschlossen.
- Ein früher Ladebeginn sperrt die Erfassung der auslaufenden Welt. Erst der native
  Start beziehungsweise die bestätigte rohe Wiederherstellung aktiviert die neue Welt.
  Das spätere Startsignal setzt denselben Ledger nicht nochmals zurück.
- Künftige Saves speichern die private B/M/A-Zuordnung binär, deterministisch und
  begrenzt auf 184 Zeilen beziehungsweise 16.936 Bytes. Prozesspointer, lokale
  Sitzungsnummern, Beobachtungssequenzen und lokale Uhren werden nicht gespeichert.
- Der atomare Save-Pfad friert erforderliche Datensätze unter dem Engine-Monitor ein,
  prüft den tatsächlich geschriebenen Datensatz und ersetzt erst danach das Ziel.
  Fehlende/veraltete Daten, Schreibfehler und verschachtelte Saves erhalten den alten Save.
- Vor dem ersten Fold werden nur zum Ladeversuch passende Daten übernommen. Alte
  Saves ohne Datensatz beginnen mit leerer Zusatzbuchführung; ihre Wünsche werden
  nicht rückwirkend rekonstruiert oder umgeschrieben. Legitime Politikwechsel vor
  dem Speichern verwerfen nur die ungültige private Zuordnung.
- Im MP ist der Fix eine erforderliche gemeinsam abgestimmte Simulationsfunktion.
  Bei gemeinsamem Reparaturbeginn und -ende setzen beide Peers ihre Zusatzbuchführung
  zurück; native Kaufzellen bleiben unverändert. Damit gehen frühere Liefergutschriften
  bei einem Resync bewusst verloren. Ein einseitiger Abbruch erzeugt keine falsche
  Abschlussmeldung; kritische Fehler schließen die gemeinsame Simulationssperre.
- APIShared besitzt jetzt die vier gemeinsamen Markt-/Rekrutierungseinstiege.
  Beide Diagnosemods verwenden Registrierungen mit eigenen Vorher-/Nachher-Tokens.
  Native Originale bleiben unabhängig von Diagnosefehlern genau einmal ausgeführt.

## Prüfungen

Der vollständige SDK-Build bestand unter anderem 490 Core-Tests und 966 Runtime-Tests;
ein optionaler Scan fremder Consumer wurde mangels Eingabe übersprungen. Die neue
öffentliche API erweitert die vorhandene Oberfläche, ohne Einträge zu entfernen.
Native Fixtures führen die tatsächlichen Backend-Trampoline aus, einschließlich
beider Verkaufszweige und der RIP-relativen Resync-Phasenabfrage.

Der kanonische Modbuild besteht die Aggregations-, Lifecycle-, Codec-, Quellen-,
ABI-/Register-/Flags-/SIMD- und Allokationsprüfungen. Der aktuelle synthetische
Achtstundenlauf mit acht KIs und Änderungen aller 23 Güter pro Tick erzeugt
höchstens 33.407.335 Byte eigenen Logbeitrag (31,860 MiB). Feste Erfassungspuffer:
1.362.040 Byte; warme Erfassung: null verwaltete Allokationen, zuletzt p99 0,0115 ms.
Dies ist kein acht Stunden dauernder Spieltest und keine vollständige FPS-Messung.

Auch der normale Build wurde mit echtem Framework-MSBuild separat kompiliert und
auf ausgeschlossenen Entwicklungsdiagnosecode geprüft. Seine temporäre Ausgabe wurde
entfernt; installierter Diagnosebuild und SDK blieben unverändert.

## Reale Spieltests

Alle erfolgreichen Läufe benutzten einen nicht aktivierten Win32-Desktop ohne
globale Eingaben. Der Eingabedesktop blieb `Default`; der Runner bestätigte die
Wiederherstellung von Plugins, Einstellungen und ursprünglichen Spielständen.

| Fall | Ergebnis |
| --- | --- |
| Zukunfts-Save mit beiden Diagnosemods, Fixes und BugfixesAndQoL | [PASS](../../AIMarketAcceptance/Findings/SaveRoundtrip-20261122.md): echter Fünferwunsch für Streitkolben, Lieferung1 und Verbrauch1 ergeben Rest4/Gutschrift1. Ein Save, zweimal geladen; beide Male bereits vor dem ersten Fold exakt4/1 und danach weiterhin gültig. |
| Neue Partie, SE + Fixes + Handelsfix | [PASS](../../../.work/a/market-modes/001/fresh-result.json): sieben angeforderte Lords und sieben Märkte bestätigt; 60,6 Sekunden Beobachtung, 14.606 erfasste Ticks, Ausgabe vollständig geleert. |
| Zwei erste Save-Testversuche | Kein Reproduktionsnachweis: der benötigte native Rekrutierungszähler war noch null. Der erfolgreiche Test wartet auf dessen natürliche Erhöhung; keine gefälschten Kaufzellen oder Zähler. |
| Neuer Spielstart mit Begleitmods | Testbesetzung nicht erfüllt: eine bestehende Lord-Anpassung ersetzte Richard durch einen Custom Lord. Nicht als bestandene Prüfung der vorgeschriebenen Besetzung gezählt; der anschließende Lauf verwendet die unveränderte Besetzung. |

Im Save-Kombinationslauf wurde ein bestehender Konflikt außerhalb des Handels gefunden:
Die Baudiagnose verkettete sich am bereits von BugfixesAndQoL belegten Notabriss-Einstieg.
Sie prüft nun vor ihrer Installation die Live-Spanne, erhält fremde Hooks und meldet
die fehlende Beobachtungsabdeckung ausdrücklich. 52/52 Baudiagnose-Tests bestehen;
im erfolgreichen Spieltest blieb der Hook erhalten, ohne nachträgliche Überschreibung.

Der neue Partiestart erzeugte 75.810 Byte eigene und 344.239 Byte andere Logs.
Mittlere Erfassungszeit 17,794 Mikrosekunden, größter gewöhnlicher Aufruf
318,9 Mikrosekunden; erster Startaufruf 925,7 Mikrosekunden. Keine verlängerten
Intervalle, Schreibfehler oder übersprungenen Sitzungen. Diese Zeiten messen die
Übersichtserfassung, nicht sämtliche nativen Übergänge oder die vollständige Simulation.

## Verbleibende Abnahme

Ein echter Zwei-Rechner-MP-/Resync-Lauf ist offen: Der konfigurierte Client-Logpfad
war auch außerhalb der Sandbox nicht erreichbar. Offline-Peer-/Reparaturtests
ersetzen ihn nicht. Kampagne und Trail wurden nicht separat durchgespielt; der
Code schließt ausschließlich den Editor aus und unterscheidet lokal/echtes MP.
Ein langer realer Performance-Lauf bleibt ebenfalls offen. Der frühere wirtschaftliche
A/B-Nachweis und der längere Sieben-KI-Lauf stehen im historischen
[Abnahmebericht](Acceptance.md); sie werden nicht als Test des neuen SDK-Pakets ausgegeben.
