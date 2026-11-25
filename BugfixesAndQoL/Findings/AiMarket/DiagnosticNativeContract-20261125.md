# Native Handelsdiagnose: Aktualisierung

Geprüfter Stand vom 21.11.2026: `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Der normale Testmod enthält die quellbezogene Korrektur gespeicherter Kaufwünsche
unter `src/Fix/`. Optionale Diagnose liegt unter `src/Diagnostics/` und wird nur
mit `/devdiag` kompiliert; der externe Testaufbau liegt in `AIMarketAcceptance`. Handelsargumente und Ergebnisse der nativen Helfer bleiben unverändert.

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
Die unten dokumentierten Schreibstellen erfassen auch kleine spezielle Erzeuger.

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

`market-backlog-fixed` aktiviert die Korrektur nach dem protokollierten Warenzugang.
Die kontrollierten Baseline-Szenarien erfassen Quellen bei ausgeschalteter Korrektur.
Ein normal gestartetes Spiel mit diesem Entwicklungsbuild korrigiert beobachtete
native Pakete direkt im gespeicherten Wunsch vor `3EC90`.

## Alle nativen Auftragserzeuger: Restpaketvertrag vom 22.11.2026

Referenzhash bleibt der oben angegebene vollständige SHA-256. Die 22 produktiven
Schreibstellen und vollständigen Bytes stehen in `src/Fix/MarketDemandSites.cs`;
Herleitung, gesamte Erzeuger-/Verbraucherkette, eingehende Kanten und Grenzen in
`Findings/AllGoodsMaterialAudit.md` und `Findings/AllGoodsRecruitFoodAudit.md`.
Die Stellen sind: Nahrung `2A464/2A488/2A4AC/2A4D0/2A4F4`, Rekrutierung
`403A7/40597/40D3C/40D62`, Baukosten `CC555/CC5C1/CC657/CC692/CC6E4`,
Knappheit `2ADB5/2ADD7/2ADF9/2AE1B`, Pech `2EB9C`, Notkauf `2F56E`,
AIV `51ADB`, Steinmunition `D5791`.

Jede verdrängte Spanne ist genau eine vollständige MOV-Instruktion (6 bis
12 Byte), ohne RSP-/RIP-Operand oder inneres Sprungziel. Ein nativer Relay
sichert GPR, Flags und FXSAVE-Zustand. Der Vorher-Callback prüft die alte
Kaufzelle, der Relay führt die Originalzuweisung genau einmal aus, und der
Nachher-Callback korrigiert die nun tatsächlich geschriebene Menge. Danach
geht es direkt zur nativen Fortsetzung, ohne die Zuweisung erneut auszuführen.
Der installierte Indirect-Backendvertrag wird für jede Stelle offline geprüft;
ein 14-Byte-Inline-Hook wäre an mehreren Stellen unzulässig. Rohe Relayziele
werden über RedBirds öffentlichen IntermediaryFactory-Vertrag veröffentlicht;
ein Marshal-Delegate-Roundtrip ist unter Unity Mono kein Identitätsnachweis.

Feste Einkaufschargen (unter anderem Waffen und Nahrung) erhalten eine
Liefergutschrift: ursprüngliche Menge Q minus bestätigte Zugänge. Späterer
Verbrauch macht die Gutschrift nicht rückgängig. Eine wiederholte native
Zuweisung derselben Charge bei nachweislich noch eigener offener Kaufzelle
setzt die Gutschrift nicht zurück. Eine neue Menge, Quellenfamilie, relevante
Kaufregel oder tatsächlich gelöschte Zelle beginnt eine neue Zuordnung.
Unbeteiligte AIC-Felder ändern die Identität nicht. Die ursprüngliche Summe
S+Q dient nur als Beleg, nicht als immer wieder aufzufüllender Lagerbestand.

CC420-Materialanforderungen erhalten dagegen ihre aus den ausgeführten
Operanden belegten Baukosten: Kosten = Quellbestand + Menge - Holzpuffer.
Der Puffer beträgt bei Holz zwei, sonst null. Unterhalb der Kosten bleibt
Kosten - aktueller Bestand + Puffer offen; beim Erreichen der Kosten endet
der Auftrag. Verlust kann die noch offene Menge deshalb wieder erhöhen.
Gleichzeitig belegte eigene Materialkosten werden maximal zusammengeführt;
aufgegebene Bauvorhaben werden nicht aus den Mengen rekonstruiert.

Der effektive Zellzeiger wird gegen `module+379D840+player*583C+good*4` und
den tatsächlichen öffentlichen SDK-Zeiger geprüft; Bestand liegt 25A0 Byte
davor. AIC-Root `404C950`, Recordgröße 5E4 und Spielerlayout sind hashgebunden.
Ein geänderter relevanter AIC-/LastStand-Vertrag, abweichende Mengen, beobachtetes Löschen,
Sitzungs- oder Tickwechsel verwerfen die Zuordnung. Nicht zugeordnete gespeicherte
Wünsche bleiben unangetastet. Numerisch identische direkte Fremdzuweisungen ohne
beobachtbaren Schreiber sind nicht unterscheidbar und daher keine zugesicherte
Erzeugerkompatibilität. Der Fix rekonstruiert keine weggefallenen Einzelabsichten.

Fixes erweitert Nahrung um Holz/Eisen/Bier an `2A4D8`. Dessen tatsächlicher Stub
wird gegen die vollständige Semantik und die gebundenen Arrays geprüft. Der
optionale Beobachter `2A42A`, Bytes `4C 69 C0 3C 58 00 00`, erfasst garantiert
folgende Fixes-Zuweisungen vor der eigenen Funktion; diese verändert keinen Bestand
und hat bei AIC ungleich null keinen davor liegenden Rückweg. `2A4EB` wäre wegen
des vorherigen JLE bei ausgeschaltetem Hopfenkauf unvollständig. Der gesamte
Funktionskörper wird vor Installation und nach Veröffentlichung geprüft und an
Sitzungsgrenzen erneut verglichen. Unbekannte Fixes-Schreiber sperren nur die
Korrektur der betroffenen drei Güter.

Teil- und Vollerfüllung werden zusätzlich an zwei fertigen Bestandsveröffentlichungen
erkannt: `C1F4A`, Bytes `42 89 AC 36 CC F0 12 00`, nach der Nicht-Gold-Summe
eines Spielers (RSI=Spieler*583C), und `B9A30`, Bytes `48 8B 4C 24 28 48 33 CC`,
nach der Neuberechnung aller acht Spieler. Letzterer enthält einen RSP-relativen
Zugriff: der Relay stellt vor dem Trampolin den ursprünglichen Stack exakt wieder
her. Alle normalen nativen Nicht-Gold-Zugänge erreichen einen dieser Endpunkte;
Herleitung und vollständige Alias-/Callerprüfung: `Findings/StockReceiptAudit.md`.
Auch Teilmengen werden dort unmittelbar angerechnet; späterer Verbrauch hebt
die Gutschrift nicht auf. Der Nullbestands-Sonderpfad B8122 (Bytes
45 89 94 8E A0 B2 79 03) aktualisiert Verlustmetadaten auf null, bevor Vanilla
seinen möglicherweise transient negativen Gesamtwert auf null setzt. Eine
gesonderte native Datenfreigabe lässt diese Marker im Baselineversuch ohne
verwalteten Aufruf passieren. Während der Originalausführung von `3EC90` werden
diese Abschlüsse ausgelassen: dessen eigene erfolgreichen Käufe löschen ihren
Wunsch selbst und zählen nicht als vermiedene Käufe. Der zusätzliche Versuch
`MARKET_TERMINAL_SUPPLY_PASS` verlangt einen frischen nativen Auftrag, Teilzugang, Teilverbrauch, Restlieferung
und unmittelbaren Restverbrauch unter derselben Simulationssperre.

## Automatische Steinmunition

D5791 bleibt kein allgemeines Zehnerpaket. D5710 benötigt atomar zehn Stein
für zwanzig Schuss. Bei neun vorhandenen Steinen fehlt für diesen Vorgang nur
einer. Nur die fünf belegten automatischen Aufrufer werden zugeordnet:
Rücksprung-RVAs 153057, 153156, 1542C6, 1543C7 und 18FD8A. Ihre CALL-Ziele
werden gegen D5710 geprüft. Der Prolog D5710 (48 89 5C 24 08 57 48 83 EC 30)
belegt den Rücksprung auf Original-RSP+38h; D572B (48 63 DA) belegt die
Einheiten-ID aus EDX im an D5791 erhaltenen RBX. Der Relayrahmen liegt 128 Byte
darunter. Der manuelle Tailjump aus 18AE0 hat keinen Leer-Munition-Guard und
ist ausdrücklich keine zugeordnete automatische Quelle.

Öffentlicher UnitAccess-Lookup mit 1-basierter ID liefert eine unmittelbare Sicht.
Global-ID, Erzeugungstick und Eigentümer sichern die Identität innerhalb der
Sitzung. GameUnit-Größe490 und die öffentlichen Felder Alive88, Owner92,
GlobalId94, Creation98, Todesmarker29C und Munitionswort35E werden gegen das
installierte SDK-Layout geprüft (hexadezimale Werte). Munition ist ein signiertes
16-Bit-Wort über beide benachbarten SDK-Bytes, kein einzelnes Byte. Ein positiver
Munitionswert oder das Ende der Ursprungseinheit beendet den alten Bedarf.
Solange diese Einheit leer ist, gilt max(0,10-aktuellerSteinbestand).
Ein inzwischen aufgebrauchter Lagerzugang darf hier wieder Bedarf erzeugen,
weil der automatische Verbraucher nur volle zehn Stein verwenden kann.
Unbekannte/manuelle Quellen überschreiben alte Zuordnung ohne neue zu erfinden.
Details und Widerlegungsfälle: Findings/AllGoodsRecruitFoodAudit.md.

Alle Hooks bleiben prozessweit veröffentlicht; nur Daten-Gates wechseln. Kein
Pattern-Fallback bei unbekanntem Hash: Datenlayout und konkrete Schreibersemantik
sind ohne erneuten Audit nicht sicher migrierbar. Fehler lassen Vanilla weiterlaufen.
Die produktive Mengenregel wird direkt in `tests/TradeSession` geprüft; Register,
Flags, SIMD, neue Schreibstellen und beide bedingten Trampolinpfade in `tests/TradeEntries`.

Der Testmod setzt inzwischen mindestens SE 2.14.4 voraus: Gegen genau diese
installierte öffentliche API wurde auch der Workshop-Teststartschalter geprüft.
Die Modversion bleibt während der Erprobung 0.1.0.
