# UnitCommandPathAPI / MoatMove: Umbau und Abnahme

Stand: 2026-10-07. Versionen bleiben APIShared 0.4.10, BugfixesAndQoL 1.0.175 und MoatMove 0.1.3. README-Dateien bleiben unverändert.

## Zuständigkeiten und Vergleich mit Git

APIShared besitzt die überlappenden nativen Hooks und ihre Original-Trampoline. BugfixesAndQoL registriert die Befehlsrichtlinie; MoatMove registriert zusätzlich die Grabenquerung. Der Vergleich verwendet die bisherigen Quellen aus Git HEAD beider Mods, nicht einen nachträglich erzeugten Ersatzstand. Die ausgegliederten doppelten Runtime-Dateien wurden entfernt.

Die neue Host-Einstellung `EnableImprovedManualUnitCommands` ist standardmäßig aktiv. Alte `FriendlyMoatMovementMode`-Einträge werden ignoriert; sie bestimmen weder die neue Einstellung noch eine Querungsrichtlinie. Checkbox, Suche, Hilfetexte, alle vorhandenen Übersetzungen, Reset und Preset-/Host-Synchronisierung sind angepasst.

Die allgemeinen gewichteten Suchdienste bleiben für die unabhängig schaltbare verbesserte Grabenfüllung verfügbar. Zusätzliche Eintritte in fertige Gräben und die zugehörige Befehlsroutensuche sind ohne Querungsprovider gesperrt. Die Fast-Suchfelder, privaten nativen Suchfelder, Wiederaufnahme und gespeicherten Fast-Befehle bleiben im Zusatzmod. Leiter-Angriffsfix und Formationsverbesserungen behalten ihre eigenen Einstellungen.

Besonders nachgeprüfte Abweichungen gegenüber HEAD:

- Gemeinsame Append-/Target-Hooks führen Queue- und Querungsprovider geordnet zum einmaligen Originalaufruf. Script-Extender-Pre-/Post-Ereignisse bleiben die Befehlsgrenzen; bei `SkipOriginalFunction` wird kein Post erwartet.
- Verschachtelte Bewegung und Zielbefehle stellen Elternkontexte wieder her. Fehler und bereits verbrauchte Queue-Befehle hinterlassen keine fremden Befehlsrahmen.
- Der Hauptmod qualifiziert manuelle Cursorziele mit vorhandenen nativen Pfaden. Für gemischte Gruppen bleibt die native Einzelausführung über 118E00 erhalten; unerreichbare Mitglieder sperren die übrigen Mitglieder nicht.
- Fast verwendet weiterhin bereits fertige Suchfelder für die native Rekonstruktion. Interne und nach Kämpfen wiederaufgenommene Befehle behalten die bisherige unitgebundene Wiederaufnahmeberechtigung.
- Der Precise-Pfadvergleich mit dem bisherigen Hauptmod ergibt identische Pfade und Knotenzahlen für 1, 120 und 680 Einheiten.

## Native Grundlage

Geprüfte Spielbibliothek: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Grundlage sind CURRENT.json, der zugehörige semantische Datenbestand und der vollständige featurebezogene Vanilla-Audit. Ergänzende Artefakte liegen in `.inspect/UnitCommandSplit`.

18E1E0 verwendet den globalen Pfadmanager. Eine Cursorprobe sichert die gesamte GameUnit, den Pfadmanager bis zum Ende der letzten 320800-elementigen Queue (0x3C886C Bytes), beide zusammenhängenden Distanz-/Stempelfelder (0x139480 Bytes), den Rechteckhelfer sowie den davorliegenden 24-Byte-Speicherhelfer. Generationswerte werden zusammen mit ihren Arbeitsfeldern wiederhergestellt; auch der stackgebundene Ausgabepuffer-Zeiger wird zurückgesetzt.

Bei fehlgeschlagener Rekonstruktion kann E1640 über DAA50, E49D0, C3E50 und DE6A0 globale Kartenverbindungen reparieren. Dauerhafte, ausschließlich im threadlokalen Cursorprobekontext unterdrückende Hooks verhindern diese Simulationsänderung. Die native Probe bleibt erfolglos; echte Befehle führen die vollständige originale Reparatursequenz aus. Die vollständigen Funktionen, ihre Folgeaufrufe und Speicherwirkungen wurden hierfür zusätzlich geprüft. Enemy-Gate-Suchkontexte bleiben um den nativen Builder erhalten.

Alle 128 zugelassenen Indirect-Einstiegspräfixe stimmen mit der installierten Bibliothek überein. Die Baseline enthält keine eingehenden Xrefs in ihre verdrängten Innenbereiche (973 geprüfte Adressen). NativeX64 wird fail-closed auf Indirect begrenzt; Original-Trampoline, tatsächliche verdrängte Länge, Pointer-Slot und veröffentlichter Patch werden vor und nach Installation geprüft. Inline-Adapter besitzen getrennte Verträge. Der Modus-Adapter berechnet die nachfolgend benötigten Flags nach dem Wrapper neu.

Veröffentlichte Hooks, Provider und Delegates bleiben bis Prozessende verwurzelt. Einstellungen ändern logischen Zustand. Vorhandene Provider erhalten Cache-Invalidierung und Kontextbereinigung auch bei deaktivierter Richtlinie; dadurch bleiben Kartenänderungen beim Wiedereinschalten wirksam. Fehler nach Veröffentlichung geben keinen ausführbaren Hook-Speicher frei und patchen ihn nicht zurück. Langfristige Arbeit läuft über langlebige Script-Extender-/Spielereignisse und native Hooks. MoatMove besitzt einen Tick-Logmarker nach dem Startup-Cleanup.

## Automatische Prüfung

`Shared/Test-UnitCommandSplit.ps1` prüft die vollständigen Compile-Quellen aller drei Projekte auf JSON-Abhängigkeiten, Komponenten-Callbacks, Teardown, CRLF, XAML-Content-Wurzeln und permanente Hooks. Der workspaceweite permanente-Hook-Test umfasst jetzt auch MoatMove. Alle drei Build-Treiber führen diese Prüfung vor dem Runtime-Build aus.

`Shared/UnitCommandSourceChecks` kompiliert die Quellen in Speicher, ohne Runtime-Ausgabe oder Installation. Alle neuen Spielzugriffe sind gegen die echte installierte Assembly geprüft. Ein unveränderter, schon in HEAD vorhandener privater Zugriff auf `EditorDirector.gameLocalPlayerID` in MissionLifecycleCapability wird separat ausgewiesen und anhand des unveränderten vollständigen Git-Quelltextes abgegrenzt.

Bestandene Prüfungen:

- Hauptmod: tatsächliche manuelle Probe-/Gruppen-/Dispatcher-Methoden mit Speicher-, Rekursions-, Ausnahme-, Mischgruppen- und verschachtelten Befehlsfällen; 34 Assertions.
- Installiertes NativeX64-Backend: 35 tatsächliche Indirect-Detours, vollständige Instruktionsgrenzen, Trampolinfortsetzungen, veröffentlichte Pointer-Slots und Callback-Ausführung; Backend-DLL-Hashes stimmen mit der Installation überein. Der native Modus-Adapter erhält EAX und ZF für beide Ergebnisse.
- Queue: 9228 Prüfungen einschließlich Wegpunkten, Formationskodierung und Quellenintegration.
- Gemeinsame Runtime-/Testmod-Pipeline: 234384 Assertions, einschließlich fehlender zusätzlicher Grabeneintritte ohne Provider, verbleibendem Grabenausstieg und unabhängigem Füllkontakt.
- Gebäudefelder: 6480 Prüfungen; gewichtete gerichtete Suche: 84031 Prüfungen; Fast-Suche: 289388 Prüfungen; Fast-Pool-/Ownership-/Save-Verträge: 1092 Prüfungen; Cursor-Konnektivität: 1469340 Vergleiche.
- FastNative: tatsächliche native Suchfeld-Ausführung und Runtime-Pipeline, einschließlich unabhängiger BFS-Orakel, Grenzen, Interleaving und gepackter Pfade.
- Host-/Client-Presets sowie bestehende Hauptmod-, Assassin-, Fixes-, Lifecycle-, JSON-, XAML- und native Vertragsprüfungen. Der angepasste bestehende Hauptmod-Nativtest besteht mit 1322 Assertions und 67 Signaturen.

Compilerprüfungen, Suchorakel und ausführbare kopierte Native-Fixtures ersetzen keine Spielabnahme.

## Spielabnahme: noch offen

Die folgenden Fälle müssen im Spiel und im Multiplayer separat geprüft werden:

| Betrieb | Fälle | Status |
|---|---|---|
| Hauptmod allein | Grabenstart zu Boden, Keep, erreichbaren Strukturen; verschiedene Einheitentypen | Offen |
| Hauptmod allein | Gemischte Auswahl mit Boden- und Grabenführer; teilweise und vollständig unerreichbare Ziele | Offen |
| Hauptmod allein | Bewegung, Shift, Patrouille, Einheiten-/Gebäudeangriff, Grabenarbeit; keine zusätzliche Querung | Offen |
| Mit MoatMove | precise, fast, FastNative; Angriff, Arbeit, Wiederaufnahme, gespeicherte Queue | Offen |
| Host und Client | Einstellungssynchronisierung, identische menschliche Befehlsausführung, Cursor getrennt vom Simulationszustand | Offen |
| Lebensdauer | Startup-Cleanup-Tickmarker, Kartenwechsel, Fehlerpfade; keine erneute Hookinstallation oder Freigabe | Offen |

Build-/Installationsprotokolle werden unter `.inspect/UnitCommandSplit/build-api.txt`, `build-main.txt` und `build-addon.txt` abgelegt. Alle drei Treiber haben nach der abschließenden Cache-Korrektur Build und Installation erneut erfolgreich in der Reihenfolge APIShared → BugfixesAndQoL → MoatMove abgeschlossen.

## Nicht grabfähige Grabenstarter: Korrektur vom 2026-10-07

Der manuelle Editor-Test um 22:07 bis 22:09 lief mit MoatMove im Modus precise. Der Benutzer beobachtete bei gemischten Gruppen eine stehenbleibende beziehungsweise falsch laufende nicht grabfähige Einheit im Graben. Die ausgeschalteten detaillierten Befehlsdiagnosen erlauben keine eindeutige Zuordnung der falschen Laufrichtung. Dieser Spielbefund ersetzt keine Abnahme der folgenden Korrektur.

Die gemeinsame manuelle Gruppenkorrektur bleibt mit aktivem Querungsprovider für Gruppen verfügbar, die einen lebenden, vom Gruppenbesitzer kontrollierbaren nicht grabfähigen Grabenstarter enthalten. Mindestens ein aktuelles Gruppenmitglied muss das konkrete Ziel in der isolierten originalen nativen Probe erreichen können. Die tatsächliche Gruppenentscheidung bindet den wirklichen Führer; E7C40 führt das Original genau einmal mit allen Seiteneffekten aus und wählt anschließend nur für diesen gebundenen Gruppenaufruf Vanillas 118E00-Einzelausführung. Einzelpfadabfragen behalten ihr originales Ergebnis. Vollständig nativ unerreichbare Gruppen bleiben beim bisherigen Providerpfad.

Diese Zwischenkorrektur deaktivierte die zusätzliche MoatMove-Platzvergabe für den nativen gemeinsamen Befehlsweg. Das führte im anschließenden Editor-Test zum unten dokumentierten gemeinsamen Zielpunkt und wurde durch die folgende Korrektur ersetzt. Nicht grabfähige Mitglieder behalten das ursprüngliche Befehlsziel, Vanillas aktuellen Grabenmodus und den originalen Builder; sie erhalten weder einen Querungsplan noch neue Grabfähigkeit. CanDigMoat bleibt Voraussetzung für zusätzliche Querungspfade. Gruppen ohne nicht grabfähigen Grabenstarter behalten die bisherige precise-/fast-/FastNative-Verarbeitung. Es wurden keine nativen Hooks, Spielmember-Zugriffe, globalen Einheitentyp-Overrides, Einstellungen oder Versionen ergänzt oder verändert.

Erneute Codekontrolle: Die Runtime-Änderung gegenüber HEAD ist auf ManualUnitCommands und eine Platzvergabe-Bedingung begrenzt. Der Precise-Vergleich verwendet jetzt ausdrücklich den letzten Hauptmod-Stand vor der Auslagerung, Commit 7d5a5a9ef33fce47e09f545480829a5ebb13b060, statt der seit dem eingecheckten Umbau nicht mehr vorhandenen HEAD-Dateien. Für 1, 120 und 680 Einheiten bleiben Pfade und expandierte Knoten identisch.

Bestanden: 676 Assertions an tatsächlichen manuellen Probe-, Gruppenmodus-, Gruppen-Flood- und Dispatcher-Methoden; beide Führer, beide Grabenstarter, Reihenfolgen und alle Kombinationen erreichbarer Mitglieder. Die gemeinsame Zusatzmod-Pipeline besteht mit 234405 Assertions, darunter neue Originalziel-/Modus-/Builder-/Platzvergabe-Fälle in allen drei Modi. Außerdem bestehen Queue (9228), Assassin (15862), Hauptmod-Nativtests (1322/67 Signaturen), vorhandene Füll-/Policytests, NativeX64-Detours (35), FastNative-Runtime (3769 native Aufrufe), FastNative-Backend (4958 native Aufrufe), Quellenkompilierung gegen die echte Spielassembly und JSON-/Callback-/Lifecycle-/XAML-/CRLF-/permanente-Hook-Präflights. Ein temporärer-Datei-Test benötigte den erhöhten Testlauf und bestand dort; die Runtime-Quellen wurden nicht als Reaktion darauf geändert.

Build und Installation dieser Korrektur: alle drei erhöhten build.bat-Treiber in Reihenfolge APIShared -> BugfixesAndQoL -> MoatMove erfolgreich abgeschlossen. Die vorhandene MoatMove-Moduskonfiguration blieb erhalten. Protokolle: .inspect/UnitCommandSplit/non-digger-build-api.txt, non-digger-build-main.txt und non-digger-build-addon.txt.

Spielabnahme weiterhin offen: den gemeldeten Sonderfall erneut mit Boden- und Grabenführer testen, allein und mit jedem Zusatzmod-Modus, anschließend Bewegung, Shift, Patrouille, Angriffe und Multiplayer. Insbesondere die zuvor beobachtete falsche Laufrichtung muss im Spiel erneut geprüft werden.

## Individuelle Zielplätze: Korrektur vom 2026-10-07

Der Benutzer hat die vorherige Korrektur im Editor getestet und beobachtet, dass gemischte Boden-/Grabengruppen auf demselben Zielpunkt stehen. Native Ursache: 118E00 schreibt den gemeinsamen Klickpunkt in alle Einzelbefehle; dieser Ausweichweg ruft Vanillas E1D30-Platzvergabe nicht auf. Die vorherige Änderung deaktivierte zusätzlich unsere Platzvergabe. Der neue Spielbefund ist damit erklärt; eine Spielabnahme der aktuellen Korrektur steht noch aus.

APIShared legt für NativeCommonFallback jetzt einen eigenständigen Platzvergabekontext an, auch ohne Querungsprovider. Lebende kontrollierbare Mitglieder benötigen keine Grabfähigkeit. Kandidaten werden deterministisch neben dem Klickpunkt über verfügbare Zielzellen enumeriert, mit Vanillas Graben-Gruppenabstand eins und maximal 4000 Kandidaten. Dies ist keine zusätzliche Wegsuche. Jeder konkrete Teilnehmer beweist die Erreichbarkeit seines Ziels durch die originale isolierte native Probe; zwischen unterschiedlichen Einheiten wird kein Erreichbarkeitsresultat übernommen. Nur bei aktivem MoatMove und CanDigMoat darf nach einer negativen Vanilla-Probe ein zusätzlicher Querungsweg geprüft werden. Andere Formations-/Angriffs-/Arbeitswege bleiben bei ihren bisherigen Selektoren und Sonderfällen.

Zielargumente und native Zielfelder werden vor dem Originalaufruf gemeinsam gesetzt. Die Synchronisierung vor dem Einzelbuilder arbeitet jetzt auch ohne Querungsprovider und berücksichtigt spätere Pre-Änderungen. Platzreservierungen gelten pro Befehlsausführung; Fehler, übersprungene Befehle und unvollständige Ausführung geben sie frei. Verschachtelte Gruppen invalidieren die Kandidatensuche des Elternkontexts, ohne dessen erfolgreich reservierte Plätze zu verlieren. Unerreichbare Teilnehmer oder fehlende eigene Plätze behalten ihren Originalbefehl.

Der abschließende Git-Vergleich betrifft drei Runtime-Dateien: MoatPlacement, MoatPlacementSearch und UnitMovementContext. Bestehende Querungsalgorithmen, permanente Hookinstallation, öffentliche Einstellungen und Versionsangaben bleiben unverändert. Der zuvor verwendete gemeinsame Suchkern behält für bestehende Provider seinen Standardvertrag; ausschließlich der neue native Platzweg deaktiviert dessen Quell-/Ziel-Cache. Der Precise-Vergleich mit dem alten Hauptmod-Commit 7d5a5a9ef33fce47e09f545480829a5ebb13b060 bestätigt weiterhin identische Pfade und Knotenzahlen für 1, 120 und 680 Einheiten.

Geprüft werden tatsächliche gemeinsame Runtime-Methoden in nativen Speicher-Fixtures: unterschiedliche Ziele bei ausreichendem Platz, passende Zielfelder, angenommene Einzelbuilder, beide Führer und Iterationsreihenfolgen, gemischte Grabfähigkeit und unterschiedliche native Struktur-Zugangsrechte bei demselben Startpunkt, teilweise/vollständig unerreichbare Ziele, Reservierungsfreigabe nach Fehler/Skip, spätere Zieländerungen und verschachtelte Einzelbefehle. Die alten Tests mit gemeinsamem Ziel wurden ersetzt. Die Testkompilierung enthält zudem den inzwischen eingecheckten AssassinRouteHandoff, damit dessen bestehende Integration mitgeprüft wird.

Messung: 1/120/680 native Teilnehmer erhalten jeweils 1/120/680 unterschiedliche Plätze, mit 1/120/680 isolierten Erreichbarkeitsproben im freien Testkorridor. Die Platzvergabe-Fixture mit ersetzter nativer Suchfunktion benötigt nach Aufwärmen ungefähr 0,005/0,3/6 Millisekunden. Separat kosten die tatsächlichen Sicherungs-/Wiederherstellungsoperationen der nativen Probe ungefähr 0,5/55/301 Millisekunden. Diese zweite Messung verwendet echte Puffergrößen, ersetzt aber ebenfalls die native Suche: reale Spielkosten kommen zusätzlich hinzu. Die Messwerte sind Test-Fixture-Werte und keine Spiel-Performance-Abnahme.

Bestanden: manuelle Probe-/Gruppen-/Dispatcher-Fixture (1477 Assertions), originale NativeX64-Detours (35) einschließlich EAX/ZF, Queue (9228), Assassin (15892), Hauptmod-Nativtests (1323 Assertions / 67 Signaturen), Füll-/Policytests, Suchorakel und gemeinsame Pipeline für Hauptmod allein sowie precise, fast und FastNative. Quellenprüfung gegen die echte Spielassembly: keine neuen privaten Zugriffe; der bekannte unveränderte HEAD-Zugriff in MissionLifecycleCapability bleibt separat ausgewiesen. JSON-, Callback-, Lifecycle-, XAML-, CRLF-, permanente-Hook- und Zusatzmod-Präflights sowie vorhandene Fixes- und Extender-Vertragsprüfungen bestanden vor den Builds.

Der spätere Effizienzhinweis des Benutzers wurde berücksichtigt: keine zusätzliche experimentelle Suche ohne Provider; die im ersten Entwurf redundante Probe des Klickpunkts entfällt im erfolgreichen Normalfall. Eine originale native Erreichbarkeitsprobe für den konkreten zugewiesenen Platz bleibt zusätzlich zur eigentlichen Vanilla-Ausführung erforderlich. Vor dem Einzelbefehl vorhandene native Felder gehören zum Gruppenführer oder vorherigen Teilnehmer und beweisen die Zugangsrechte einer anderen Einheit nicht. Diese Mehrkosten sind ausdrücklich kein Null-Aufwand-Versprechen.

Während der Builds wurden Assassin-Runtime-Dateien im anderen Chat verändert. Eine spätere Korrektur der Prüfung lebender Einheiten im Assassin-Anfrageindex wurde nach ihrer Quellen-/Testprüfung ebenfalls mitgebaut. Dessen Inhalte blieben erhalten; nur CRLF wurde wiederhergestellt. Die betroffenen Test-Fixtures wurden um die neu gelesenen Geschwindigkeits-/Kontrollfelder ergänzt und das veraltete Zweidistanz-Orakel an die konservative Prüfung aller früheren Nachbarstempel angepasst. Die tatsächlichen Quellenkompilierungen gegen die echte Spielassembly sowie Assassin-Regression wurden danach erneut durchgeführt.

Aktuelle Protokolle: .inspect/UnitCommandSplit/formation-*.txt. Build und Installation erfolgreich: alle erhöhten build.bat-Treiber in Reihenfolge APIShared -> BugfixesAndQoL -> MoatMove abgeschlossen. Nach einer zuletzt eingegangenen Hauptmod-Änderung wurden Hauptmod und Zusatzmod erneut geprüft, gebaut und installiert. Lokale und installierte DLL-/PDB-/Manifestdateien stimmen per SHA-256 überein; 391 Runtime-Quelldateien bleiben gegenüber dem abschließenden Prüfstand identisch. README-Dateien und aktive Versionsangaben wurden nicht geändert: APIShared 0.4.10, BugfixesAndQoL 1.0.176, MoatMove 0.1.3. Die bestehende Zusatzmod-Moduskonfiguration wurde erhalten.

Spielabnahme der aktuellen Korrektur offen: gemischte Gruppen mit Boden-/Grabenführer, nicht grabfähigen Grabenstartern, Hindernissen, Keep/Strukturen und teilweise unerreichbaren Zielen; Bewegung, Shift-Queue, Patrouille und Angriffsbewegung, Hauptmod allein und alle Zusatzmod-Modi. Im Spiel zusätzlich individuelle Standplätze, tatsächliche Laufrichtung, Antwortzeit großer Gruppen und Multiplayer-Ausführung prüfen. Statische und ausführbare Native-Fixtures ersetzen diesen Nachweis nicht.
