# Resync- und Handelsabnahme vom 23.11.2026

Geprüfter Arbeitsstand: Mod-Repository `84b60e95fb29`, APIShared `70882196fd34`,
jeweils einschließlich der uncommitteten Änderungen dieses Auftrags.
APIShared bleibt `0.67.0`, beide Testmods bleiben `0.1.0`.
Die vollständigen Paketidentitäten stehen in
[identities.json](../../../.work/a/resync-review/001/identities.json).
Dies ist ein datierter Nachweis, keine Vorgabe für spätere Arbeitsstände.

## Umsetzung und Zuständigkeit

- APIShared verwendet einen einzigen internen `MultiplayerResyncDataSession` für
  den Produktionsbetrieb und die Mehrteilnehmer-Tests. Zeit, Unity-Dispatcher,
  Transport, Weltbindung und native Zustandsabfragen sind injizierte Grenzen.
  Öffentliche Signaturen, Registrierungsidentitäten, Datenformat und Protokoll
  bleiben unverändert. Der normale Engine-Aufruf ohne Reparatur bleibt nach
  Aufwärmen ohne Allokationen; eine ausstehende Weltbindung wird zusammengefasst.
- Der native Resync prüft die zentrale `ApiSharedRuntime.SupportedHash` statt
  einer Konstante des KI-Markt-Dienstes. Die auditierten nativen Spannen und der
  unterstützte DLL-Hash wurden nicht verändert.
- Kachelhistorie und Einkaufsabsichten bleiben vollständig bei ihren Verbrauchern.
  Der Markt-Ledger wird gemäß der bestehenden Entscheidung bei Reparaturbeginn
  und Reparaturabschluss auf beiden Seiten geleert. Diese Callbacks ändern keine
  nativen Pending-Zellen. Der Ledger wird nicht zusätzlich per Data-API übertragen.
- `CaptureLedger` lässt ungültige Zeilen im gespeicherten Snapshot aus, ohne den
  laufenden Request zu verändern. Ein anschließend scheiternder Speichervorgang
  kann dadurch keine Kaufzuordnung mehr allein durch die Erfassung verwerfen.
- Fixed-Spieltests verlangen nun tatsächlich aktive Korrektur sowie beide nativen
  Gates. Der absichtliche abschließende Output-Drain bleibt davon ausgenommen.
  Die externen Owner-Tests liegen ausschließlich in `AIMarketAcceptance`.

Keine neue Handelsstrategie, keine nachträgliche Reparatur alter Saves und keine
Korrektur im Editor wurden ergänzt. README-Dateien, Script-Extender-Fork und
Fixes-Quellen wurden nicht verändert. Es gab keinen Commit und keine Veröffentlichung.

## Spielunabhängige Prüfungen

- Finaler kanonischer APIShared-Build mit `/source:working /tests:full /noinstall`
  bestanden. APISharedTests: **1002 bestanden, einer übersprungen**; der optionale
  Friend-Consumer-Scan war nicht aktiviert. Core.Tests: **506 bestanden**.
  Die übrigen ausgewählten SDK-, Native-, Metadaten-, Dokumentations-, Paket-
  und Bootstrap-Prüfungen sind ebenfalls bestanden.
- **23 gezielte Resync-Tests** bestanden. Drei Instanzen des Produktionsautomaten
  verwenden den echten Paketformatter: gleiche und verschiedene Records über
  64 KiB, mehrere Fenster, frühe/späte Erfassung, verspätetes Hello, leere Hostdaten,
  veraltete Identitäten, Nonces, Warteschlangenfehler, Zeitablauf, nativer Abbruch
  sowie Fehler bei Capture/Prepare/Commit. Insbesondere bleibt der letzte gültige
  37-Byte-Block über drei Enginezyklen zurückgehalten: kein Prepare dieses Clients,
  kein Commit und kein nativer Abschluss. Nach Zustellung schließen alle Peers ab.
- Beide Consumer-Builds und ihre relevanten Runtime-, Lifecycle-, JSON-,
  Kompatibilitäts-, nativen Backend- und CRLF-Prüfungen bestanden.
- Separater normaler Framework-Build bestanden. Cecil bestätigt: keine
  Korrektur-/Automations-/Szenariotypen und keine RedBird-Referenz im normalen
  Diagnosemod. Der Fix bleibt Bestandteil des expliziten `/devdiag`-Testpakets.
- Synthetische acht Stunden, acht KIs, alle 23 Güter fortlaufend geändert:
  **33.406.744 Byte = 31,859 MiB** eigener Logbeitrag einschließlich Fix-Zusammenfassungen,
  **1.362.040 Byte** feste Erfassungsfelder, **0** warme Erfassungsallokationen,
  Erfassungs-p99 **0,0062 ms**. Das Modell verwendet 40 Ticks/s und ist kein
  achtstündiger Spieltest. Es gibt keine Rotation; langfristig wächst das Log weiter.

Ein erster vollständiger Build scheiterte im separaten zufälligen
Workshop-Prozessabbruchsversuch an `Prepared file count changed`. Der gezielte
Wiederholungslauf und beide folgenden vollständigen Builds bestanden jeweils
60 Swap-, 30 Preloader-, 15 Staging- und sieben Gruppenabbrüche.
Der Fehler ließ sich nicht erneut auslösen; Workshop-Code wurde nicht geändert.
Ein zwischenzeitlicher Spielstart wurde vor dem Start korrekt wegen eines durch
den Testbuild neu erzeugten SDK-Binaries ohne erneuerten Paar-Digest abgelehnt.
Die unten aufgeführten finalen Läufe verwendeten ausschließlich das abschließend
geprüfte Plugin-/Bootstrap-Paar.

Nachweise: [finaler SDK-Build](../../../.work/a/resync-review/001/api-final.log),
[SDK-Prüfschritte](../../../.work/a/resync-review/001/sdk-build-steps.json),
[Mod-Build](../../../.work/a/resync-review/001/market-build.log),
[Normal-Build](../../../.work/a/resync-review/001/market-normal-proof.json).

## Finale Spieltests mit aktivem Fixes-Mod

Alle drei Läufe bestanden auf eigenen unsichtbaren Win32-Desktops. Der
Eingabedesktop blieb `Default`, keine globalen Eingaben wurden verwendet.
Der Runner bestätigte anschließend die Wiederherstellung der ursprünglichen
Plugins, Einstellungen und Spielstände. Die Testinstallation bleibt nicht aktiv.

| Lauf | Ergebnis | Eigener Markt-Logbeitrag | Beiträge anderer Logger |
| --- | --- | ---: | ---: |
| Speichern und zweimal Laden | Echter nativer Fünferauftrag, Lieferung und Verbrauch einer Ware: Rest4/Gutschrift1 vor dem Speichern und nach beiden nativen Restore-Grenzen vor dem ersten Fold bestätigt. | 14.344 B | 376.044 B |
| Kontrollierte kleine Käufe | Eine geeignete Custom-KI, auf 100.000 Gold aufgefüllt; 300 Sekunden Vergleich, Stress, größere Chargen und Erholung. 36 echte Einzelwarenkäufe und 38 Kaufphasen mit offener Ausrüstung während der Stressphase. | 272.801 B | 328.379 B |
| Neue Partie | Sieben erwartete Lords mit Märkten; 300,5 Sekunden, 72.807 Erfassungen und 72.594 fortgeschrittene native Ticks. 5.589 erfasste Quellschreibvorgänge, 2.315 Reduktionen, 228 Erhöhungen und 97 Streichungen. | 229.689 B | 346.606 B |

Der Belastungslauf zeigt Spieler2/Gut23 konkret: nativer Quellauftrag5 bei
Tick221044; physische Lieferung10 bei Tick221050; Vanilla-Pending weiterhin5.
Nach Aktivierung aller Gates korrigiert der Fix bei Tick221094 auf0
(`package=5 credited=5 stock=10`). Über diesen erfüllten Auftrag hinaus wurden
keine zusätzlichen Einheiten gekauft. Ein späterer echter Kauf5 aus Bestand0
bleibt erlaubt. Eine unabhängige terminale Probe belegt **5 → Lieferung1 → 4 →
Verbrauch1 → 4 → Lieferung4 → 0 → Verbrauch4 → 0**, ohne Testschreibzugriff auf Pending.

Die externe Komponentenprüfung verwendet den echten Markt-Owner und die geordnete
API-Registry: PACK/SIZE ignoriert, beide Resets leeren private Requests, Gates
suspendieren/rearmen, native Pending-Werte bleiben je Callback-Grenze unverändert,
Capture während Reparatur wird abgelehnt. Nach dem Reset muss erst eine neue
native Quelle wieder eine Zuordnung erzeugen. Der Capture-Negativfall lässt
eine ungültige private Policy-Zeile aus und erhält den Live-Ledger exakt.
Ein gewöhnlicher fremder Data-Provider wurde erfolgreich registriert.
**Diese Komponentenprüfung führte weder native Reparatur-Chores noch eine
Datenübertragung zwischen Rechnern aus.**

Im neuen Sieben-KI-Spiel betrug die mittlere Snapshot-Erfassungszeit **16,3 µs**,
der größte Einzelwert **786,2 µs**. Die größten berichteten Intervall-p99-Werte
der vier nativen Korrekturpfade waren **4/32/1/4 µs**. In den kurzen Lade-/Probe-
Läufen gab es kalte Aufrufe bis zur 2.048-µs-Histogrammstufe; diese sind nicht
als warme Dauerlastmessung zu interpretieren. Es gab keine ausgelassenen
Diagnosebelege und keine APIShared-/Markt-Error-Zeilen in den drei finalen Logs.
Eine vollständige FPS-Messung oder garantierte Obergrenze jedes Aufrufs folgt daraus nicht.

Rohlogs, Desktopnachweise, Messmethode und getrennte Beiträge:
[game-results.json](../../../.work/a/resync-review/001/game-results.json).
Der abschließend erzeugte Testspielstand bleibt beim Save-Lauf erhalten.

## Noch offen

Der dokumentierte Client-Logpfad war zuletzt am **23.11.2026, 15:51:38 Uhr
Europe/Berlin** auch außerhalb der Sandbox nicht erreichbar. Ein echter
Zweirechner-Resync mit Steam, nativen Chores, Kachelhistorie und Markt-Owner ist
deshalb weiterhin nicht abgenommen. Auch native Vollreparatur und gezielter
Abbruch im echten MP sind offen; F9/F10 von MakeDesync beweisen keine Vollreparatur.
Der konkrete Ablauf und die noch fehlenden Voraussetzungen stehen in
[ResyncPairedAcceptance.md](ResyncPairedAcceptance.md).
Kampagne und Trail wurden in diesem Auftrag nicht separat gespielt; der Editor
bleibt entsprechend Nutzerauftrag vom Fix ausgeschlossen.
