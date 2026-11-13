# Offline-Nachweise für Gefahrenscans und Höhenmodell

Prüfdatum 13.11.2026, Modpack-HEAD `1d10d7ddf56eb02eefad502b8051c3ed40f6358c`
plus Arbeitsänderungen dieses Auftrags. Kein Spielstart, keine Installation und
keine native Änderung im Spielprozess. Die Ergebnisse sind keine Ingame- oder MP-Abnahme.

## T1: Produktionsscans

`tests/Engine/HazardScannerTests.cs` kompiliert die vollständigen Quellen
`FireRouteRules`, `PlagueRouteRules` und `PlagueDamageModel` mit Roslyn. Zusätzlich
werden der tatsächliche `HazardTileSet`, APIShareds `RouteTileRules`, dessen
Registrierungsordnung und `PlagueHeightPolicies` verwendet. Extender-Enums stammen
aus dem lokalen Extender-Quellcode. Die Kategoriezuordnung wird gegen das tatsächliche
`BuildingCategories`-Feld des nativen APIShared-Emitters geprüft.

Ersetzt werden ausschließlich Spielgrenzen: Effektmanager-Speicher in einem eigenen
Puffer, Kachel-/Gebäudezugriff, Capability-Verfügbarkeit und Logging. `TryBind` wird
nicht ausgeführt; sein nativer Adress-/Layoutvertrag wird durch diese Tests nicht neu
bewiesen. Die Scannerschleifen, Feldleser, Mengenänderungen, Kosten, Policy-Lesung
und Journalveröffentlichung sind Produktionscode.

Bestanden: 1273 Assertions mit

    dotnet run --project Pathfinding/tests/Engine/Pathfinding.Engine.Tests.csproj --no-restore -- . --hazards-only

Abgedeckt sind Effektzustände 1/2/3, Anfangs-/Endslot und ausgeschlossener Slot 0,
falscher Effekttyp, doppelte Feuer, Überlappung und Entfernung von Wolken,
Slotwiederverwendung, Scanintervall, Reset samt neuem Kachelzählbereich,
Pest-Ausblendphase, alle acht Driftrichtungen, sichere versus neu gezogene Richtung,
Subkachelgrenzen, negative/äußere Koordinaten und die vier Kartenränder. Regeln
werden für alle acht Spieler geprüft. Policy-Tests verwenden die echte Owner-Union,
einzelne/alle 16 Kategorienkombinationen, toleranzgenaue Grenzen, nicht verfügbare
und fehlende Policy-Leser. Unveränderte Mengen erzeugen keine neuen Journaländerungen.

## Q2: Schadens- und Höhenvertrag

Die installierte kanonische DLL wurde erneut gehasht:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Der Hash stimmt mit CURRENT.json und dem ausgewählten DATABASE_INFO.json überein.
Die vollständigen bestehenden Audits unter `knowledge/PLAGUE_HEIGHT.md` und
`FIRE_EFFECTS.md` wurden gegen die Implementierungen abgeglichen. Es wurden keine
neuen nativen Adressen oder angeblichen Laufzeitverträge eingeführt.

Zusätzlich bestand die vorhandene APIShared-Suite gegen diese echte DLL:

    dotnet run --project APIShared/tests/PlagueHeight.NativeTests/PlagueHeight.NativeTests.csproj "-p:GameDir=E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition" -- "E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\Stronghold Crusader Definitive Edition_Data\Plugins\x86_64\CrusaderDE.dll"

Ergebnis: **1206 native Ausführungs-/Backend-/Höhen-/Aktivierungs-/Register-/Stackprüfungen**.
Darunter Ausführung der vollständigen Disease-Schadensroutine auf isoliertem Speicher,
Bytegleichheit der Unit-Records, Radien/Heilerresistenz, tödlicher/nichttödlicher Schaden,
Gebäudeausnahmen, Höhenablehnung, Self-/Dead-Ausschluss und originale Roof-Tabelle.
Die erste Variante mit `--no-restore` scheiterte nur an fehlender project.assets.json;
der reguläre Aufruf bestand. NuGet meldete nicht erreichbare Audit-Metadaten (NU1900),
MSBuild außerdem vorhandene System.Text.Json-Versionskonflikte in diesem eigenständigen
Testprogramm. Keine Runtime-JSON-Abhängigkeit wurde hinzugefügt.

Kein bestätigter Modellfehler wurde gefunden. Die statische Kachelebene stimmt bei
gewöhnlich stehenden Einheiten mit dem auditierten Standvertrag überein. Der native
APIShared-Stub vergleicht beim Schaden aber die tatsächliche Einheitenbasis plus
Höhenzugabe. Tunnel-, Belagerungs-, Treppen-/Kletter- und Bewegungstransitionen
werden durch eine reine Kachelvorhersage nicht vollständig beschrieben. Welche
solchen Zustände im späteren Gefahrentest tatsächlich erreicht werden, bleibt offen;
deshalb keine vermeintliche Korrektur durch ungeprüfte zusätzliche Höhenannahmen.

## Q4: Reihenfolge und Erkennungsfenster

Der Test führt den unveränderten `if (runtime.EngineEnabled)`-Block aus
`ProcessFastCommands` aus; Anchor-/Replan-/Escape-Einstiege sind instrumentierte
Grenzen, Feuer-/Pestscanner und Routenjournal tatsächlich ausgeführte Produktion.
Er bestätigt: Anchor und Replan lesen den bisherigen Regelstand, danach veröffentlichen
die Scanner Änderungen und erst danach liest die Flucht den neuen Gefahrenstand.
Im Folgetakt ist die Journaländerung am Anchor-Einstieg sichtbar.

Diese Prüfung beweist nicht die vollständige native Maskenübernahme oder den wirklich
gelaufenen Weg. Für tatsächliches Auftreten unmittelbar nach einem Scan gelten bis
zum nächsten Vier-Tick-Scan weitere Simulationstakte; die Flucht läuft im Acht-Tick-
Raster und die verteilte Neuplanüberwachung zusätzlich in ihren eigenen Intervallen.
Auf dem ersten Kartentakt werden Masken vor dem ersten Gefahrenscan gebaut. Der
bestätigte Unterschied zwischen Fluchtzielsuche und anschließendem Gruppenroutenbau
bleibt ein gezielter Ingame-Prüfpunkt, keine Zusage sofortigen Schadensschutzes.

## T7: Isolierte Lastmessungen und verbleibende Abnahme

Der Scanner-Test wärmt fünfmal auf und misst anschließend 20 echte Scans. Beispiel
des ersten bestandenen Laufs auf dieser Maschine, ohne parallele Spielsimulation:

| Kontrollierte Last | Median pro Scan | Maximum | Allokationen über 20 Scans |
| --- | ---: | ---: | ---: |
| 1 Feuer | 0,0079 ms | 0,0083 ms | 0 Byte |
| 225 Feuer | 0,0258 ms | 0,0265 ms | 0 Byte |
| 5999 Feuer | 0,3920 ms | 0,6535 ms | 536 Byte |
| 1 Pestwolke | 0,0099 ms | 0,0106 ms | 0 Byte |
| 225 überlappende Wolken | 0,6507 ms | 0,6890 ms | 0 Byte |
| 1000 überlappende Wolken | 2,7615 ms | 3,2803 ms | 0 Byte |

Dies sind CLR-Fixtures mit kontrollierten Kachelzugriffen, keine Mono-, FPS- oder
Spiel-Tickmessung. Vollständige Wolkenüberlappung hält die Ausgabemenge konstant
und isoliert die wiederholte Scanarbeit; sie repräsentiert nicht jede Wolkenverteilung.
Aus den einzelnen Allokations-/Zeitwerten wird kein Produktionsfix abgeleitet.

`HazardEscapeLoadTests.cs` ergänzt echte Fluchtmethoden auf dem bestehenden nativen
Korridor für 1/225/1000 Soldaten, mit absichtlich fehlendem Ausgang. Die Grenze
1000 stammt aus der tatsächlich unterstützten UnitAccess-Fixture (IDs 1..1024),
nicht aus einer geänderten Spielgrenze. Gemessen werden Versuche, Wiederholungsfrist,
Kosten und Bedienung der höchsten ID. Außerdem gibt es einen ausdrücklich kontrollierten
Restbudgetversuch für wartende Einheiten; ein künstlich auf einen Settle beschränktes
Restbudget ist kein Nachweis, dass die echte 250000-Grenze in dieser Korridorkarte
erschöpft wird.

Im integrierten Lauf wurden alle 1/225/1000 Einheiten einschließlich der höchsten
ID versucht, ohne Ausgang keine falsche Flucht angelegt und vor Ablauf von 40 Ticks
keine erneute Suche gestartet. Beispielkosten des ersten Ticks: 3,6019/1,8336/7,6438 ms
mit 112/15872/57104 Byte Allokationen. Das erste kleine Szenario enthält insbesondere
JIT-/Aufwärmkosten; diese Zahlen sind keine saubere Skalierungskurve oder Spielmessung.

### Bestätigte Budget-Benachteiligung und Korrektur

Der kontrollierte Fall mit 32 unbegrenzt wartenden Soldaten und nur einem verbleibenden
Settle je Verarbeitung lieferte vor Korrektur nach 40 Durchläufen nur fünf bediente
IDs (1..5); ID 32 wurde nie geprüft. Die niedrigeren IDs wurden nach ihrem erneuten
Probeintervall wieder zuerst bedient. Die neue Fairness-Assertion scheiterte vor
dem Codeeingriff mit `indefinite wait probe budget starves later unit IDs under sustained pressure`.
Das belegt eine Benachteiligung im Produktions-Verteilungsalgorithmus unter dauerhaft
knappem Restbudget; die reale Häufigkeit dieses Lastzustands bleibt ungemessen.

`ProcessReplanWaits` verwendet nun eine wiederverwendete, nach IDs aufgebaute Arbeitsliste
und verschiebt deren Startposition anhand des gespeicherten nativen Ticks (`uint tick >> 3`,
modulo Wartendenzahl). Die Rotation ist nur aktiv, wenn mindestens eine eigene
unbegrenzte Gefahrenwartepolicy vorliegt. Ausschließlich begrenzte/fremde Wartefälle
behalten ihre bisherige ID-Reihenfolge. Der Gruppennachlauf bleibt erhalten; Budget
und Suchgrenzen sind unverändert. Es gibt keinen zusätzlichen persistenten Cursor
und keine Änderung des Saveformats. Nach Verarbeitung werden die Referenzen der
Arbeitsliste freigegeben, ihre Kapazität bleibt wiederverwendbar.

Nach Korrektur wurden im selben 40-Durchlauf-Fall alle 32 IDs bedient. Die Wiederholung
über tatsächliches Capture/Stage/Apply der Bewegungsdatensätze und denselben
wiederhergestellten nativen Tick ergab dieselbe vollständige Bedienreihenfolge.
Der integrierte Engine-Lauf einschließlich dieser Regression bestand mit 215518
Haupt-Assertions und den separaten Scanner-/Settings-/Fast-/Cursorprüfungen.

Weiter im Spiel zu prüfen: echte Scan-/Callbackausführung, dynamische Höhenübergänge,
viele räumlich verteilte Wolken, große gleichzeitige Fluchten ohne gemeinsamen
Suchbudgetdeckel, Wirkung auf KI-Aufträge, wirklicher Hin-/Rückweg und Host-/Client-
Gleichheit. Offline bestandene Teilverträge schließen diese Abnahme nicht ab.
