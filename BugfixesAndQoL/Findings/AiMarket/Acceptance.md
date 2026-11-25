# Handelskorrektur: geprüfter Entwicklungsstand

Stand: 22.11.2026, Einzelspieler-Abnahme. Version bleibt 0.1.0.
Installiert ist der `/devdiag`-Build mit SHA-256
`0E08A6AA50FE6414314C3C0B43B08D36C50F64A3470389389F6E812441EA7655`.
Der normale Build enthält ausschließlich die Beobachtung.
Kanonischer Build: `build.bat /source:working /apishared:package /devdiag /nopause`.
Quellbasis war develop/c9f9c544 plus die im Buildnachweis erfassten Arbeitsänderungen;
es wurden keine Commits, Branchwechsel oder Veröffentlichungen durchgeführt.

## Änderungen

- Feste Chargen behalten Liefergutschriften nach Verbrauch und gleichem Refresh.
- CC420 verwendet belegte Baukosten einschließlich des nativen Holzaufschlags.
- Automatische Munitionswünsche verwenden die konkrete leere Einheit und den
  atomaren Bedarf von zehn Stein; manuelle/unbekannte Quellen bleiben unowned.
- Native Stores werden vor und nach ihrer einmaligen Originalzuweisung beobachtet.
  Fertige Bestandsrechnungen und der B8122-Nullverlust schließen Zu- und Abgänge.
- Nur relevante Kaufregeln ändern die Zuordnung; andere AIC-Felder tun dies nicht.
- Kopierte, begrenzte B/M/A-Beispiele werden im Hintergrund formatiert. Auch
  nicht-kausale Testläufe warten auf die letzte Fix-Zusammenfassung.

## Prüfungen

Runtime-/JSON-/Lifecycle-, Abhängigkeits-, Quellgrenzen-, Native-Hook- und
CRLF-Prüfungen bestanden. Core-/Sessiontests bestehen einschließlich Refresh,
Lieferung und Verbrauch, Kostenänderung, AUTO-Zielen und null warmen Core-
Allokationen. Das tatsächlich installierte native Backend besteht 13.699
Prüfungen für Originalausführung, Register/Flags/SIMD, Gates, Stack, alle22
Stores, drei Bestandsmarker sowie die AUTO-Caller-/SDK-Verträge.
Ein bestehendes Shared-CS0649-Warning bleibt; der Build hat keine Fehler.

Der simulierte Achtstundenlauf mit acht KIs und Änderungen aller23 Güter je
Tick bleibt einschließlich Fix-Ausgabe unter32MiB: etwa31,859MiB. Die Erfassung
belegt1.362.040Byte feste Puffer, null warme Capture-Allokationen und zuletzt
p99=0,0082ms im Labortest. Das ist kein tatsächlich acht Stunden laufendes Spiel.

## Spielabnahme

Alle Läufe verwendeten den unsichtbaren Win32-Desktop ohne globale Eingaben,
mit Script Extender2.14.4, Fixes1.31 und dem geprüften APIShared-Paket0.61.0.
Native Referenz:FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

| Lauf | Ergebnis und Beleg |
| --- | --- |
| Vanilla-Ausgangsfall | [Baseline](../../../.work/a/market-backlog/003/532a83bb8eff/LogOutput.log): Metallrüstung5 bestellt bei Bestand0; Lieferung10; Wunsch bleibt5; später Kauf5 bei Bestand8, danach Verkauf7. Das belegt den veralteten Wunsch, aber keine FIFO-Duplikatliste und keinen isolierten Goldverlust290-210. |
| Erster integrierter Fixlauf | [Lauf004](../../../.work/a/market-backlog-fixed/004/6e61fee68f12/LogOutput.log): vollständiger Geschenkbedarf korrekt aufgehoben, null überflüssige Nachkäufe; insgesamt FAIL, weil der zusätzlich verlangte Teilmengenversuch nicht erreicht wurde. Die zu enge, nur alle250ms geprüfte Testauswahl wurde korrigiert. Kein Hook-/Capturefehler. |
| Tickgenauer Rückstauvergleich | [Lauf005](../../../.work/a/market-backlog-fixed/005/c4f331c61877/LogOutput.log): PASS. Golduntergrenze100000,36 echte Ein-Stück-Nahrungskäufe und38 Handelsphasen mit offener Ausrüstung im Stressfenster.955 native Aufrufe,95 Käufe,104 Verkäufe, alle Capture-/Kontext-/Originalfehler0, Ausgabe vollständig. |
| Neue Partie mit sieben KIs | [Frischer Lauf](../../../.work/a/market-fresh-fixed/001/aede0289005c/LogOutput.log): PASS. Alle sieben angeforderten Lords und ihre Märkte bestätigt;72932 erfasste Ticks bei Speed250,6991 Quellenzuweisungen,2465 Reduktionen,266 Erhöhungen,93 Nullsetzungen. Diese zählen Korrekturoperationen einschließlich Refreshes, keine einzigartigen verhinderten Käufe. |

Der Teilmengenbeleg im erfolgreichen Rückstaulauf ist eindeutig:
Tick221194 erzeugt einen echten Fünferwunsch für Bögen. Im nächsten Tick wird
physisch1 geliefert: Pending5→4. Nach sofortigem Verbrauch bleibt4. Weitere4
werden geliefert: Pending4→0. Ihr sofortiger Verbrauch lässt Pending0 und
Bestand0 zurück. Gold bleibt100405; die Fixture schreibt nie die Kaufzelle.
Der ursprüngliche volle Leder-Geschenkvergleich bleibt separat:5→0, kein
überflüssiger Nachkauf; ein späterer neuer Fünferkauf erfolgt regulär bei Bestand0.

In der frischen Partie wurden B-, M- und A-Pfade tatsächlich ausgeführt.
Beispiel AUTO: Wolf, Einheit19/Global32856/Erzeugung2797, Quelle D5791 bei
Tick10913; bei Tick18787 liegt1 Stein vor, die Einheit ist weiterhin leer,
und der Wunsch wird10→9. Weitere natürliche Waffen-/Nahrungszugänge und
Materialverluste führen ebenfalls zu den vorgesehenen Mengenänderungen.

## Aufwand, Ausgabe und Wiederherstellung

Der frische Lauf erzeugt230785Byte eigene Logs und342230Byte fremde Logs.
Keine verlängerten Intervalle, übersprungenen Sitzungen, ausgelassenen Beispiele,
unvollständigen Ticks oder Schreibfehler. Mittlere Tick-Erfassung16,80µs;
Maximum1,412ms im ersten Tick, danach höchstens0,547ms. Die vier verwalteten
Callback-p99-Obergrenzen am Ende sind2/4/1/4µs für Quelle/Vorher/Nachher/Bestand.
Sie messen weder die nativen Übergangsstubs noch Vanillas gesamte Tickdauer.
Es gab keinen beobachteten Simulationsstillstand; eine kontrollierte FPS-/
Gesamtoverhead-A/B-Messung wurde damit nicht ersetzt.

Beide erfolgreichen Läufe meldeten vollständige Ausgabe und verifizierte
Wiederherstellung von Plugins, Einstellungen und Spielständen. Der Eingabe-
Desktop bliebDefault; die Testdesktops sind geschlossen. Der installierte
Fix entspricht weiterhin dem obigen Hash. Alle49 APIShared-Paketdateien und
sein Bootstrap-Patcher wurden anschließend gegen das unverändert verwendete
SDK-Paket geprüft. Es wurde kein SDK neu gebaut oder heruntergestuft.
Kompakte maschinenlesbare Ergebnisse:
[Fix](../../../.work/a/market-allgoods/001/fixed-result.json),
[Frische Partie](../../../.work/a/market-allgoods/001/fresh-result.json),
[Installation](../../../.work/a/market-allgoods/001/install5.json).

## Verbleibender Umfang

Der geprüfte Einzelspieler-Teststand ist implementiert und installiert. Eine
allgemeine Release-/Multiplayerfreigabe ist das nicht. Der zusätzliche Ledger
wird noch nicht mit Saves oder MP-Resync übertragen: Nach einem Laden beginnt
die Zuordnung konservativ erst an neuen bestätigten Quellen. Bestehende
unbekannte Wünsche, beliebige gleichwertige Fremd-Pointerwrites, manuelle
Munitionswünsche und unbekannt aufgegebene Baupläne werden nicht rekonstruiert.
Echte Mehrspieler-/Resync-Abnahme, gegebenenfalls persistierte Provenienz und
ein langer realer Dauertest bleiben vor einer breiteren Veröffentlichung offen.
README, kanonischer Script-Extender-Fork und Fixes-Quellen wurden nicht geändert.
