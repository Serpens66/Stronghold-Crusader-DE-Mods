# Gemeinsame Resync-Abnahme auf zwei Rechnern

Stand: 23.11.2026. Dies ist ein auszuführender Abnahmeablauf, kein bestandener
Multiplayertest. Der dokumentierte Client-Logpfad war auch außerhalb der Sandbox
nicht erreichbar. Ein vorhandener Hostprozess, ein lokaler Komponentenlauf oder
zwei simulierte Peers ersetzen die folgenden Versuche nicht.

## Vorbereitung

1. Zwei echte Steam-Teilnehmer mit getrennten angemeldeten Spielinstanzen verwenden.
   Kein Mehrfachstart unter derselben Identität und kein erfundener Client.
2. Auf beiden Rechnern bei geschlossenem Spiel dieselben ausgewählten Pakete und
   Einstellungen bereitstellen: Script Extender, das zu prüfende APIShared-Paar aus
   Plugin und Bootstrap-Patcher, Deposit Restoration, MakeDesync und den
   `/devdiag`-Build des Handelsfixes. Paket- und Native-DLL-Hashes festhalten.
   Die aktuellen Mindestversionen stehen in den jeweiligen Manifesten; MakeDesyncs
   private Deposit-Bindung verlangt derzeit Deposit Restoration 0.3.1.
3. Eine private Partie mit Host, menschlichem Client und mindestens einer wirtschaftlich
   aktiven KI starten. Beide Logs müssen echtes Multiplayer mit beiden menschlichen
   Identitäten und übereinstimmender Partie bestätigen. Lokale Sitzungsnummern dürfen
   verschieden sein. Erforderliche Feature- und Resync-Datensatzregistrierungen müssen
   vor dem ersten MP-Lobby-Abgleich abgeschlossen sein.
4. Eine eigene MP-Spielstandkopie mit übermauerten Stein- und Eisenvorkommen verwenden
   (bestehender Deposit-Testfall `test_stone`). Der Client-Lord muss volle Gesundheit
   und mindestens zwei Lebenspunkte haben. Kampf, Bauarbeiten und andere absichtliche
   Zustandsänderungen während des Versuchs unterlassen. Keine normalen Saves überschreiben.
5. Für die Marktprüfung zusätzlich einen **echten** Fünferauftrag mit gelieferter und
   verbrauchter Einzelware nachweisen: nativer Rest4, ursprüngliches Paket5,
   Liefergutschrift1 und Bestand0. Ausreichend Gold und den tatsächlich aktiven nativen
   Rekrutierungspfad bestätigen; freie Bauern allein beweisen diesen Pfad nicht.
   Die Einzelspieler-Save-Fixture ist keine bereits bestätigte MP-Fixture.

Die folgenden Kürzel betätigt der Nutzer selbst am Client. Eine Agentenübernahme
der globalen Tastatur erfordert eine gesonderte ausdrückliche Freigabe. Der bisherige
Hintergrundrunner automatisiert nur eine lokale Instanz; er besitzt keinen geprüften
Remote-Installations-, Lobby-Beitritts- oder Zwei-Rechner-Startweg.

## Native Teilreparaturen und Datenübertragung

| Versuch | Ausführung auf dem Client | Erforderliches Ergebnis auf beiden Peers |
| --- | --- | --- |
| Gleiche private Historien | `Strg+Umschalt+F9` genau einmal. | MakeDesync zieht beim Client-Lord einen Lebenspunkt ab. Vanilla repariert diese Einheitenabweichung. Start und Abschluss erscheinen auf beiden Peers; Lord-Identität und volle Gesundheit werden wiederhergestellt. Bei unveränderten Datensätzen: `requestedRecords=0`, `transferredBytes=0`. |
| Abweichende Vorkommenshistorie | Mit einer unveränderten Kopie derselben Ausgangspartie beginnen; `Strg+Umschalt+F10` genau einmal. | Zusätzlich zur Lebenspunktabweichung fehlt ein privater Eintrag unter einer lebenden Mauer beim Client. Die Hosthistorie wird übertragen und übernommen. Ist Deposit Restoration der einzige registrierte Datensatz, erwartet der Client `requestedRecords=1`, `transferredBytes=320804`. Sonst die tatsächlich registrierten Datensätze und übertragenen Größen auswerten. |
| Wiederholte Reparatur | Nach vollständigem Abschluss des ersten F9-Versuchs denselben F9-Versuch in der laufenden Partie wiederholen. Keine neue Reparatur während einer laufenden anfordern. | Zwei getrennte Start-/Abschlusspaare ohne verlorene Aktivierung, übernommene alte Anfragen oder doppelte Abschlüsse. Die native Reparaturkennung und die gemeinsame Partie zuordnen, nicht allein lokale Uhrzeit oder Sitzungsnummer. |
| Ergebnis der Historie im Spiel | Nach abgeschlossenem F9 beziehungsweise F10 die betreffenden Mauern durch normale synchronisierte Spielbefehle entfernen. | Beide Peers legen dieselben ursprünglichen Stein-/Eisenkacheln frei. Neu gebaute und wieder entfernte Mauern funktionieren weiterhin gleich. |

F9 und F10 sind **keine Vollreparatur-Auslöser**: Beide verursachen dieselbe native
Einheitenabweichung; F10 verändert zusätzlich private Moddaten. Ein größerer
privater Datensatz oder ein langer Resync beweist keine vollständige native
Kachel-/Gebäude-/Spielerreparatur.

## Markt- und Abschlussgrenzen

Die bestehende MakeDesync-Ausgabe prüft Lord und einen Vorkommenseintrag. Für die
folgenden Aussagen ist vor der Zwei-Rechner-Abnahme eine **externe** Testbeobachtung
zu ergänzen, beispielsweise im vorhandenen AIMarketAcceptance-Testmod. Keine
zusätzlichen Testszenarien in APIShared oder dem Handelsfix einbauen.

- Vor und nach dem Markt-Reset bei `Started` sowie vor und nach dem Markt-Reset bei
  `Completed` die nativen Kaufzellen und privaten Zuordnungen unter dem Engine-Monitor
  kopieren. Beobachter vor/nach der Marktregistrierung mit Ordnung−100 registrieren;
  zur zusätzlichen Deposit-Prüfung muss der späte Beobachter auch deren Ordnung0
  überschreiten. Die Testbeobachter selbst schreiben weder Kaufzellen noch Ledger.
- Innerhalb **jeder einzelnen Callbackgrenze** müssen die nativen Kaufzellen
  unverändert bleiben. Nicht den Clientbestand vor der gesamten Reparatur mit dem
  danach vergleichen: Vanilla darf während der Reparatur Host-Spielerrecords übernehmen.
- Nach beiden Markt-Resets müssen alle privaten Auftragszuordnungen leer sein. Während
  der Reparatur bleiben die Marktwriter gesperrt; erst nach erfolgreichem Abschluss
  sind die technisch verfügbaren, gemeinsam zugelassenen Gates wieder aktiv.
  Ein Rest4 darf durch den Reset selbst weder auf5 steigen noch auf0 fallen.
- Nach `Completed` ist die frühere Liefergutschrift absichtlich verworfen. Neue
  bestätigte native Quellen dürfen anschließend eine neue Zuordnung öffnen. Diesen
  akzeptierten Neustart nicht als Erhalt der alten Gutschrift bewerten.
- Am frühen Abschlussbeobachter muss die Deposit-Historie bereits übernommen und
  gegen die reparierte Karte gefiltert sein. Der späte Beobachter bestätigt die
  wieder aktivierten Deposit-Writer. Host und Client protokollieren SHA-256 ihrer
  endgültigen Historie plus Kartengröße; die Werte müssen übereinstimmen.

Ein lokaler Lauf mit `COMPONENT_ONLY`, `nativeChoresExecuted=False` oder
`dataTransportExecuted=False` prüft diese Besitzerlogik nur als Komponente. Er
erfüllt keinen der nativen oder Zwei-Rechner-Abnahmepunkte dieses Dokuments.

## Weitere verpflichtende Fälle und derzeitige Blocker

| Fall | Voraussetzung und Abnahmeregel |
| --- | --- |
| Tatsächliche native Vollreparatur | Noch kein geprüfter automatischer Auslöser vorhanden. Vor Durchführung einen eng begrenzten, vollständig auditierten Auslöser im separaten Testmod ergänzen. Native Reparaturrecords müssen die tatsächlich betroffenen Kategorien belegen. Derselbe Daten-/Markt-/Abschlussvergleich wie oben gilt; F10 allein erfüllt diesen Fall nicht. |
| Erfasste Kachel ist nach Reparatur keine Mauer mehr | Benötigt eine kontrollierte native Rasterabweichung mit nachgewiesener Hostfassung ohne Mauer und einer tatsächlich ausgeführten Kachelreparatur. Der alte private Eintrag muss danach auf beiden Peers null sein. Keine ungeprüften Rasterwrites oder willkürlichen IDs verwenden. Dieser Stimulus ist im vorhandenen MakeDesync nicht implementiert. |
| Anbieterfehler | Vor Lobbybeitritt auf beiden Peers denselben begrenzten Testdatensatz registrieren: Schema1, maximal ein Byte; Capture liefert ein konstantes Byte; der Client wirft in Prepare absichtlich, der Host bereitet regulär vor. Der eigene Commitzähler bleibt0. Beide Peers müssen die gemeinsame Welt terminal sperren; keine `Completed`-Meldung und keine wieder aktivierten Writer. Der Testanbieter gehört ausschließlich in den externen Testmod und ist gegenwärtig noch nicht vorhanden. |
| Abbruch/Teilnehmerverlust | In einem separaten Wegwerfversuch den Client während einer nachgewiesenen laufenden Übertragung manuell trennen. Host muss den Teilnehmerwechsel erkennen und darf den nativen Abschluss nicht durch eine synthetische Bestätigung freigeben. Keine erfolgreiche Abschlussmeldung auf dem überlebenden Peer. Dieser Fall ersetzt den Anbieterfehlertest nicht. |

## Nachweise und Ergebnisentscheidung

Pro Versuch beide BepInEx-Logs und die zugehörigen nativen Reparaturaufnahmen sichern.
Mindestens `MAKE_DESYNC_READY`, `MAKE_DESYNC_ARMED`, `MAKE_DESYNC_APPLIED`,
`MAKE_DESYNC_RESYNC_STARTED`, `MAKE_DESYNC_RESYNC_COMPLETED` und
`RESYNC_DATA_COMPLETED` derselben Partie zuordnen. Empfang/Versand allein sind
kein Ausführungsnachweis. Falsche Identitäten, fehlende Marker, ausgelassene
Callbacks oder unbestätigte native Kategorien ergeben **BLOCKED/FAIL**, kein PASS.

Ein terminaler Fehlertest besteht nur mit dokumentiertem Stillstand der Simulation,
ausbleibendem Abschluss und gesperrten Schreibern auf beiden Peers. Ein abgestürzter
Prozess oder ein Timeout des Prüfskripts allein bestätigt diese Semantik nicht.
Eigene Saves, temporäre Testpakete und Einstellungen anschließend wiederherstellen
und anhand des vorherigen Inventars prüfen. Historische Logs bleiben unverändert.
