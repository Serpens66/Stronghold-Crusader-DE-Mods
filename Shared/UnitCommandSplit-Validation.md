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
