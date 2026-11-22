# Native Handelsdiagnose: Aktualisierung

Geprüfter Stand vom 21.11.2026: `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Nur Entwicklungsdiagnose, keine Veränderung von Handelsargumenten oder Ergebnissen.

## Einstieg und ABI

| RVA | Funktion | Windows-x64-Argumente / Ergebnis | vollständige verdrängte Bytes |
| --- | --- | --- | --- |
| 3EC90 | KI-Kaufentscheidung | void(pointer root, int player) | 40 56 41 54 41 55 |
| 40740 | Rekrutierung | void(pointer root, int player) | 40 55 56 41 56 48 81 EC 60 01 00 00 |
| 29650 | Markt-Kauf | int(pointer root, int player, int good, int signedAmount) | 48 89 5C 24 08 48 89 6C 24 10 |
| 29700 | Markt-Verkauf | void(pointer root, int player, int good, int signedAmount) | 45 85 C9 0F 88 96 00 00 00 |

Die Verkaufs-JS verzweigt nach 2979F und muss beim Trampolinbau erhalten
bleiben. Keine Aufrufziele innerhalb dieser vier verdrängten Spannen im
geprüften Baseline-Xrefbestand. Controller 3F270 ausdrücklich nicht hooken.
Der tatsächliche Backendvertrag wird durch `Shared.AuditedEntryDetour`
geprüft: Indirect, vollständige Spanne, Originaltrampolin, Fortsetzung,
Livebytes und nach Veröffentlichung unveränderliche Besitzverhältnisse.
Keine Startup-Proben an ausführbaren Kopien. Offline-ABI-Prüfungen müssen
die vier obigen Einstiege mit dem tatsächlich verwendeten Backend ausführen.
Der spezialisierte Prüfeinstieg ist `tests/TradeEntries/TradeEntries.Tests.csproj`.
Er vergleicht native PE und produktive Konstanten, führt die tatsächlichen
Delegate-ABIs gegen private Stub-Fortsetzungen aus und prüft insbesondere
beide Verkaufs-JS-Zweige sowie den unveränderten Kauf-Rückgabewert. Der echte
Spielcode wird dabei weder gepatcht noch mit künstlichen Spielzeigern aufgerufen.

## Datenlayout und Aktualisierung

`MarketTradeTrace.ValidateLayout` bindet alle gelesenen Recordgrößen und
Offsets an die installierten öffentlichen SE-Interoptypen. Profilroot ist
`module+404C950`, AIC-Auswahl über den nativen Index minus eins; der gesamte
ausgewählte Record muss im geladenen Modul liegen. Warenarrays enthalten
25 signierte 32-Bit-Zellen, Spieler-IDs sind 1 bis 8.

Vor einer neuen DLL: aktuelle Identität und vollständige Erzeuger-/Verbraucher-
Pfade erneut auditieren, Einstiege disassemblieren, eingehende Sprünge und
alle gelesenen Layouts nachprüfen. Nicht nur die Hashprüfung aktualisieren.
Quellen: kanonischer SE unter `shcde-script-extender/src/SHCDESE.BepInEx/Interop`,
Baseline `sem/FBCB9319/knowledge/AI_ECONOMY_OBSERVATION.md`,
`AI_SELL_THRESHOLDS.md`, `AI_RESOURCE_RESERVE.md` und kanonischer Fixes-Klon.
Der Fixes-Pferdepfad und mögliche benutzerdefinierte Kaufanforderungen bleiben
aktiv; `AIBuildDiagnoseTest_Serp` darf die vier Einstiege nicht parallel besitzen.

## Aussage und Lebensdauer

Kaufwünsche sind pro Gut gespeicherte Mengen, keine nachgewiesene FIFO-Liste.
Ein ausgeführter Kauf wird durch Original-Rückgabe und Bestandszuwachs,
ein ausgeführter Verkauf durch Bestandsabnahme bestätigt. Ein späterer Verkauf
desselben Guts wird mit dem vorangegangenen Kauf verbunden; das beweist weder
Unnötigkeit noch identische physische Waren. Produktion, Geschenke, Verbrauch,
AIC-Verkaufsschwellen und Geldmangel müssen weiterhin geprüft werden.
Negative Verkaufsschwellen und Kaufpreisabfragen gesondert unterscheiden.
Kleine spezielle Erzeuger werden erst bei einem konkreten Trace gezielt ergänzt.

Native Originale laufen genau einmal und ohne gemeinsamen Lifecycle-Lock.
Kurze Capture-Sperren schützen Stack und Intervalle; Start/End werden bei einer
laufenden Originalkette bis zum äußersten Finish vorgemerkt. Ausschließlich
publizierte, kopierte Puffer gehen an den Hintergrundschreiber. Der Test unter
`tests/TradeSession` prüft diesen produktiv genutzten Zustandsvertrag inklusive
verschachtelter Ketten und gleichzeitigem Sitzungswechsel.

Pro Sitzung höchstens 1024 Detailbeispiele, davon 200 reservierte erste
Kauf-/Verkaufsketten; feste Puffer, vollständige aggregierte Zähler, explizite
Auslassungs-/Rückstauzähler. Die spezialisierten nativen Zusammenfassungen sind
für kurze kausale Diagnoseläufe bestimmt. Für lange Partien die getrennte,
kompakte Tick-Aggregation verwenden. Gesunde Tick-Automation allein beweist
keine funktionierende native Diagnose: READY, Callbackzahl, Capture-Health
und abschließende Ausgabe separat abnehmen.

## Kontrollierter Stale-Demand-Test und Testfix

`market-backlog` verändert ausschließlich in einer ausdrücklich angeforderten
lokalen Entwicklungsautomatisierung die Versuchsbedingungen: ein ursprünglich
waffenkaufender Custom-Lord erhält 100000 Gold als Untergrenze, Foodregeln
1/1/1/1 und die regulären Baldwin-Waffenwerte Kauf5/Verkauf5/Minimum2.
Einmaliger physischer Ausrüstungsabbau erzeugt echten nativen Rekrutierungsbedarf;
phasegebundene Ein-Stück-Foodentnahmen verzögern Aufträge. Ein protokollierter
Warenzugang erfüllt danach einen tatsächlich beobachteten offenen Waffenwunsch.
Die Versuchsvorbereitung schreibt keine Pending-Mengen. AIC- und Lagerzugriffe
verwenden geprüfte öffentliche SDK-Wege, die sieben AIC-Felder werden vor Ende
kooperativ mit vollständiger Rückleseprüfung wiederhergestellt.

`market-backlog-fixed` führt denselben Versuch aus und aktiviert erst nach dem
Warenzugang die experimentelle Korrektur. Sie berücksichtigt ausschließlich
beobachtete `40740`-Anforderungen für Ausrüstung, deren Bestand vor und nach dem
Erzeugeraufruf null war, bei LastStand0 und passender AIC-Kaufcharge. Das beim
leeren Lager beobachtete Auffüllziel bleibt erhalten; vor `3EC90` darf die
wartende Menge nur auf den noch nicht anderweitig gedeckten Anteil sinken.
Materialdefizite aus `CC420` haben eine andere Bedeutung und fallen nicht unter
diese Regel. Geänderte AIC-Chargen, fremde Mengen, beobachtetes Löschen sowie
Sitzungs-/Tickwechsel verwerfen die Zuordnung. Die Originalfunktion läuft auch
bei einem Korrekturfehler genau einmal; dann wird die Korrektur deaktiviert.

Dies ist ein begrenzter A/B-Testfix, keine allgemeine Freigabe für andere
Rekrutierungsquellen oder geänderte Einheitenkosten. Außerhalb des expliziten
Fixversuchs bleibt der installierte Mod beobachtend. Die Native-Entry-Hooks
bleiben prozessweit veröffentlicht; sie werden nicht zurückgepatcht.
Die produktive Mengenregel wird direkt in `tests/TradeSession` geprüft.

Der Testmod setzt inzwischen mindestens SE 2.14.4 voraus: Gegen genau diese
installierte öffentliche API wurde auch der Workshop-Teststartschalter geprüft.
Die Modversion bleibt während der Erprobung 0.1.0.
