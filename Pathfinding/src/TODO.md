# Pathfinding: offene Arbeiten

Zentrale Arbeitsliste für Gefahrenvermeidung, verbleibende Optimierungen und
Migrationsabnahmen. Die Sammlung allein autorisiert keine Umsetzung der Aufgaben.

Ausgangspunkt: 13.11.2026, Mod-Repository `develop`, HEAD
`1d10d7ddf56eb02eefad502b8051c3ed40f6358c`; damals vor der Dokumentation sauber.
Datierter Ausgangspunkt vor dem nun autorisierten Offline-Implementierungsauftrag;
der aktuelle Arbeitsstand enthält die unten genannten Änderungen. Keine neue
Spielabnahme. Prioritäten im Gefahrenabschnitt beziehen sich auf die spätere Freigabe der derzeit
gesperrten Funktionen, nicht auf einen nachgewiesenen Fehler im normalen Spielbetrieb.

## Status und bereits vorhandene Absicherung

Aktualisiert am 13.11.2026 nach Offline-Implementierung. **Offline abgeschlossen**
bezeichnet bestandene isolierte Produktionscode-/Maschinenverträge;
**vorbereitet** bezeichnet reproduzierbare spätere Spielprüfungen;
**offen** bezeichnet fehlende fachliche oder dynamische Nachweise. Große Aufgaben
bleiben trotz bestandener Teilprüfungen offen. Zusammenführung und Nachweise:
[OfflineImplementation](../Findings/OfflineImplementation.md),
[Gefahrenscans](../Findings/OfflineHazards.md) und
[Optimierungen](../Findings/OfflineOptimization.md).

- Der normale vollständige Build-/Testtreiber und der DEVDIAG-Build ohne Installation
  bestehen. Eine bestätigte Treiberabhängigkeit ist behoben: `Movement.Tests`
  läuft nach dem Bau seiner Haupt-DLL. Der getrennte Gefahren-Testmod kompiliert
  und besteht 97 Offline-Vertragsprüfungen. Quell-/SDK-/Paketnachweise und Grenzen
  stehen im integrierten Bericht; daraus folgt keine Spielabnahme.

- Feuer/Pest bleiben in [EngineOptions.Capture](Engine/EngineOptions.cs) unabhängig
  von gespeicherten Opt-ins auf `false`. Die beiden Bereiche in
  [PathfindingSettings.xaml](../Override/ScriptExtenderUI/PathfindingSettings.xaml)
  sind ausgeblendet und von der Einstellungssuche ausgeschlossen.
- Implementiert sind Gefahrenscans, Routenkosten, Neuplanung, Warten/Abbruch,
  Flucht, unterschiedliche Rückkehrarten, temporäre Soldatengruppen und gespeicherte
  Bewegungsfälle. Das ist keine Aufforderung zur erneuten Implementierung.
- Zum bisherigen Prüfstand: Feuer-Warten standardmäßig 60, Rückkehr zur alten
  Standposition 120 Spielsekunden, beide konfigurierbar; die Rückkehrfrist beginnt
  beim sicheren Ankommen. Pest hat eigene Fristen. Die frühere feste Empfehlung
  von 400 Ticks gilt nicht als Arbeitsauftrag. Das isolierte Pechfeuer-Experiment
  ist kein allgemeines Maximum für Flächen- oder Gebäudebrände.
- Bestehende [Engine-Tests](../tests/Engine/AnchorIntegrationTests.cs) prüfen echte
  Produktionsmethoden mit ersetzten Spielgrenzen: `ReplanWatchTests`,
  `FireEscapeTests`, `MovementCaseSaveTests`,
  `PlagueDamageModelTests` und `PlagueEscapeTests`.
  Enthalten sind unter anderem Umweggrenzen, explizites Stoppen nach fehlgeschlagenem
  Routenbau, Probe-Budgets, Warteabbruch, Gruppen-/Ziel-/Solo-Fortsetzung,
  Warteschlangen-Sperre, wiederholte Gefahr, Rückkehrfristen, KI-Ausschlussmarke,
  getrennte Save-Fälle und erhöhte Pestflächen. Keine pauschal fehlenden Tests behaupten.
- Am aktualisierten Prüfstand bestand `dotnet run --project Pathfinding/tests/Engine/Pathfinding.Engine.Tests.csproj --no-restore -- .`
  vom Workspace-Root vollständig: 546 Settingsprüfungen, 1273 Produktionsscanner-
  Assertions, 30 Wartemodusprüfungen, 9 integrierte Veto-Subscriberprüfungen,
  215580 Runtime-Assertions,
  84032 Suchprüfungen, 289388 Fast-Assertions, 1103 Zustands-/Saveprüfungen,
  1469340 Cursorvergleiche; 56 Runtime-Dateien und 225 ausgewählte Produktionsmember.
  Zwei Compilerwarnungen CS8321 betreffen ungenutzte lokale Testhelfer. Dies war ein
  Offline-Testlauf, keine Installation und kein FPS-Nachweis. Build-/SDK-Nachweise
  werden separat im integrierten Bericht geführt.
- Native Grundlage: installierte DLL und `CURRENT.json`/`DATABASE_INFO.json` mit
  SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
  Verträge: [Feuer/Bewegung](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIRE_EFFECTS.md),
  [Pest](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PLAGUE_HEIGHT.md),
  [DLL-Update](../UpdateToNewDLL.md). Generische Decompiler-Namen sind Kandidaten;
  konkrete Feld-/Branchverträge stammen aus den dortigen vollständigen Audits und
  dem Abgleich mit aktuellen Funktionsabfragen. Keine neue Vollanalyse der gesamten DLL.

## Bestätigte Fehler – offline korrigiert

- [x] **B1 / P2: Offline abgeschlossen – überlappenden Solo-/Fluchtbesitz erhalten.**
  [MovementCaseSave](Engine/MovementCaseSave.cs) stellt beide Schichten wieder her;
  `ProcessReplanSolos` wartet wie im ununterbrochenen Lauf auf das Fluchtende.
  Ist `AnchorSingleMoves` beim Laden aus, übernimmt die passende geborgte Flucht
  den Solo-Gruppenbesitz statt ihn vorzeitig zurückzugeben. Unit-/Tribe-Global-ID,
  Eigentümer und Split-Vertrag sichern den Übergang; Save-Identitäten bleiben erhalten.
  Regressionen mit tatsächlichem Restore-/Verarbeitungs-/Gruppencode für Feuer/Pest,
  Hinweg, sicheres Warten und Rückkehr vergleichen ununterbrochenen Lauf mit Save/Load;
  abgeschaltete Gefahren beziehungsweise Soloüberwachung werden einbezogen.
  Fehlende Wiederherstellung sowie der deaktivierte Solo-Wächter scheiterten vor
  ihren Korrekturen (Nachweise `b1-before.log`, `b1-watch-disabled-before.log`,
  `b1-after.log` unter `.work/a/pf-offline/001`). Abnahmekriterium offline erfüllt;
  echter Save-Lauf mit Spielanimation bleibt T6/T10.

## Ungeklärte Fragen (keine bestätigten Fehler)

- [x] **Q1: Verhalten entschieden und offline umgesetzt.**
  Kosten 254 bleiben ein weiches Routing-Signal für alle Einheiten. Aktives
  Warten/Flüchten gilt ausschließlich für geeignete menschliche Soldaten;
  Zivilisten, Belagerungsgeräte und bestehende Immunitäten bleiben ausgeschlossen.
  KI erhält sichere Routen innerhalb ihrer vorhandenen Befehle, keine neuen
  automatischen Stop-/Fluchtbefehle vor separatem Zustandsaudit und Spielabnahme.
  `FireWaitIndefinitely` und `PlagueWaitIndefinitely` sind host-synchronisiert,
  standardmäßig aus, mit Preset-/Settings-/Locale-/versteckter UI-Anbindung.
  Begrenztes Warten bricht nach Frist ab; unbegrenztes wartet bis zum zulässigen
  sicheren Umweg. Eigene Wartefälle laufen nach Ablauf nie absichtlich durch Gefahr.
  Rückkehrfristen bleiben getrennt; APIShareds `MaximumWaitTicks=0` bleibt unverändert.
  Neue Befehle, Tod, Eigentums-/Identitätswechsel und Mission ersetzen alte Fälle.
  Nachweis: 30 Produktions-Warteprüfungen und 546 Settings-/UI-Prüfungen.
  Laufende Spielabnahme und die Schutzgrenzen ohne Einzelrouten bleiben T2–T6.

- [ ] **Q2: Pestmodell an echten Höhenübergängen abgleichen.**
  **Offline geprüft, dynamisch offen:** alle Kategorien-/Policykombinationen und
  Toleranzgrenzen geprüft; 1206 native APIShared-Ausführungs-/Backendprüfungen
  bestanden. Kein bestätigter Modellfehler. Siehe [OfflineHazards](../Findings/OfflineHazards.md).
  `PlagueDamageModel.cs:50` verwendet die Standebene der Kachel; APIShareds
  `PlagueHeightAssembly.GenerateDamage` vergleicht die tatsächlichen Einheitenfelder
  Basis + Höhenzugabe. Der Unterschied bei Bewegung/Treppe/Klettern ist bereits im
  Native-Audit benannt. Gebäudekategorien und Toleranz+1 entsprechen im geprüften
  Code dem APIShared-Stub; das beweist die Gleichheit bei Übergängen nicht.
  **Fertig wenn:** Vorhersage und echter Schadenspfad für Boden, Treppen, Mauern,
  Tore, Türme, Burgfried, zerstörte Varianten und Übergänge verglichen sind, mit
  Vanilla sowie einzelnen/vereinigten APIShared-Höhenpolicies und Toleranzgrenzen.

- [x] **Q3: Offline abgeschlossen – falschen Erfolg bei altem Zustand 101/122 korrigiert.**
  [SoldierTribeMoves.TryMoveSoldier](Engine/SoldierTribeMoves.cs) prüft vor dem
  Gruppenbefehl die vollständig auditierten Aufnahmebedingungen von `0x11B520`;
  der bestehende KI-Ausschlussmarkenhelfer läuft zuvor. Ein alter Walking-/Exit-
  Zustand eines abgewiesenen Mitglieds gilt damit nicht als Erfolg des neuen Befehls.
  Die Regression mit tatsächlichem Produktionscode scheiterte vorher und besteht
  nun für alte Zustände 101/122 und Aufnahmeausschlüsse; echte Spielmember und
  Offsets wurden gegen die installierte Assembly reflektiert.
  Nachweise `q3-before.log`/`q3-after.log` in `.work/a/pf-offline/001`,
  Feldverträge in [UpdateToNewDLL](../UpdateToNewDLL.md).
  Tatsächliches Ankommen und Animation bleiben T4/T10.

- [ ] **Q4: Gefahrenerkennung und tatsächlich ausgeführte Route zeitlich abgleichen.**
  **Offline geprüft, Spielmessung vorbereitet:** Produktionsscanner und echter
  Schedulerblock belegen Scan → Journal → Flucht sowie Sichtbarkeit am Anchor im
  Folgetakt. Vollständige native Maskenübernahme und gelaufene Wege bleiben offen.
  `FastMovementScheduler.ProcessFastCommands` führt Anchor/Neuplanung vor den
  Vier-Tick-Gefahrenscans und danach die Acht-Tick-Flucht aus. Regeln aus dem Scan
  erreichen den Anchor damit erst im Folgetakt. `TryFindFireEscape` sucht lokal
  mit anderer Gefahrengewichtung als der anschließend ausgeführte Vanilla-Gruppenbefehl.
  **Fertig wenn:** bei neuem Feuer, driftender Pest, Mapstart und überlappenden Gefahren
  Erkennungsfenster, geplantes Ziel und wirklich gelaufener Weg gemessen sind;
  verbleibender Schaden und Grenzen sind dokumentiert. Ein gefundenes Fluchtziel
  allein beweist keinen sicheren tatsächlich veröffentlichten Weg.

- [ ] **Q5: Historischen gesunden AI110-Auflösungsfall zuordnen, falls reproduzierbar.**
  **Weiter offen:** Nachweise erneut ausgewertet; AI110 bei vollem HP und Life=3
  belegt weder den vorherigen Pfad noch Flags/Auslöser. Kein kausaler Fix.
  Vergleich mit/ohne Pathfinding bei identischem Save, Befehlen und Quellstand
  vorbereiten; den ersten Übergang mit vorherigem Zustand/Pfad protokollieren.
  Aus MIGRATION übernommen: früherer Fall auf der Moat-Testkarte, keine bestätigte
  Pathfinding-Regression. [UNIT_LOAD_PLACEMENT_CONTRACTS](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/UNIT_LOAD_PLACEMENT_CONTRACTS.md)
  erklärt den nativen AI110-Endpfad, nicht den Auslöser dieses Einzelfalls.
  Nachweise: `.work/a/pf-opt/001` und `.work/a/pf-startup/001` vom 12.11.2026.
  **Fertig wenn:** Auslöser durch Vergleich mit/ohne Pathfinding kausal zugeordnet
  oder die fehlende Reproduzierbarkeit mit geprüftem Szenario festgehalten ist.
  Nicht mit der später behobenen BFQoL-Gebäude-ID-0-Startmeldung vermischen.

## Fehlende Tests und Abnahmen

Alle folgenden Kästchen bezeichnen fehlende zusätzliche Nachweise, nicht die
Abwesenheit der oben genannten Offline-Tests. Neue Ingame-Szenarien gehören in
einen Testmod unter `Testmods/` über die öffentliche Test-API, nicht in den Hauptmod.

- [x] **T1: Offline abgeschlossen – tatsächliche Scanquellen geprüft.**
  [HazardScannerTests](../tests/Engine/HazardScannerTests.cs) kompiliert vollständige
  `FireRouteRules`, `PlagueRouteRules`, `PlagueDamageModel` und `HazardTileSet`;
  kontrollierte Spielgrenzen statt nachgeschriebener Scannerlogik. 1273 Assertions
  prüfen Zustände, Slots/Wiederverwendung, Überlappung, Entfernung, Drift/Richtungen,
  Subkachelgrenzen, Kartenränder, Reset, echte APIShared-Policies und Journal.
  Die erwarteten Mengen und deren Entfernung/Reset stimmen. Native Bindung und
  echte Callbackausführung bleiben ausdrücklich Teil der Spielabnahme.

- [ ] **T2: Feuer-Ingame-Matrix abschließen.** Pech, Feuerpfeile/-töpfe und
  Gebäudebrand; stehende/laufende/wartende Soldaten, Feuer entsteht auf Restweg,
  erlaubter/zu langer/fehlender Umweg, gefährliches Ziel und eingekreiste Einheit.
  Fertig: Umweg beziehungsweise Warten/Abbruch, realer Schaden, Animation und
  Wiederholungsversuche sind anhand von Zustand und Sichtprüfung bestätigt.
- [ ] **T3: Pest-Ingame-Matrix abschließen.** Natürliche und Geschosswolken,
  Drift, Ausblendphase, Behandlung und Entfernung; Lord/Heiler, Heilerresistenz,
  erhöhte Garnison mit `EscapePlagueRaised` aus/an. Fertig: zusammen mit Q2
  Schadensflächen, Schutz/Ausnahme und Rückkehr nach Entfernung belegt.
- [ ] **T4: Flucht und Rückkehr vollständig abnehmen.** `FireEscape.cs:317`:
  **Offline erweitert, Spielabnahme offen:** reale Flucht-/Rückkehr-/Besitzmethoden
  einschließlich erneuter Gefahr und überlappender Savefälle bestehen.
  Standposition versus altes Bewegungsziel, getrennte Feuer-/Pestfristen ab sicherer
  Ankunft, blockierte Rückkehr, Fristablauf, neues Feuer/Pest am Fluchtziel und
  Gefahrenwechsel während Rückkehr. Fertig: Endposition und Gruppenaufnahme sind
  korrekt, keine Befehlsoszillation und keine unbeabsichtigte Fristverlängerung.
  Erteilten Rückkehrbefehl und tatsächliches Ankommen getrennt nachweisen.
- [ ] **T5: Befehlsintegration im Spiel prüfen.**
  **Offline erweitert, Spielabnahme offen:** Vorrang neuer Befehle, Queue-Sperren,
  Gruppenbesitz, Aufnahmeausschlüsse und Veto-Subscriber sind regressionsgesichert.
  `RouteReplanWatch`, `SoldierTribeMoves`, Formationen und CommandQueue:
  neue Move-/Attack-/Patrolbefehle,
  Auswahl/Neugruppierung, Shift-Folgebefehle, Zielfeldverschiebung, KI-Kampfverhalten,
  Gruppenverlust/-erschöpfung. Fertig: neuere Befehle haben Vorrang, Formation und
  Warteschlange behalten ihre Bedeutung; kein versehentlicher Wiederholungsbefehl
  löscht die Queue. Q3 einschließen.
- [ ] **T6: Save-/Lifecycle-Matrix ergänzen.**
  **Offline erweitert, echte Saves/Editor offen:** B1 abgeschlossen; v1-Fallbytes,
  deaktivierte Optionen, Unit-/Tribe-Identitätswechsel und Tick-basierte
  Warteverteilung sind in Produktionsmethoden geprüft.
  Feuer/Pest jeweils während Flucht, sicherem Warten, Rückkehr, Routenwarten und Solo-Fortsetzung
  speichern/laden. Tod, Unit-/Tribe-ID-Wiederverwendung, fehlender Datensatz,
  deaktiviertes Feature nach Laden, pausiert neu speichern, neue Karte und Editor
  einbeziehen. Fertig: Identitäten und Gruppenbesitz bleiben korrekt, keine alten
  Regeln/Fälle auf neuer Karte und keine verwaisten temporären KI-Gruppen.
- [ ] **T7: Last und deterministische Budgets mit aktiven Gefahren messen.**
  **Offline gemessen, Spielmessung offen:** 1/225/5999 Feuer, 1/225/1000 Wolken,
  1/225/1000 erfolglose Fluchten; Versuche, Allokationen, Wiederholungsfrist und
  höchste ID erfasst. Bestätigte Benachteiligung später IDs bei dauerhaft engem
  Restbudget durch deterministische Tick-Rotation für unbegrenzte eigene Wartefälle
  behoben; gleicher Bedienverlauf nach Save/Load. Grenzen und Messwerte in
  [OfflineHazards](../Findings/OfflineHazards.md); kein allgemeiner FPS-Nachweis.
  Beide Scans lesen bis zu 5999 Slots alle vier Ticks. `FireEscapeTick` durchsucht
  alle Einheiten alle acht Ticks; lokale Fluchtsuchen und `IsEscapeTaken` haben kein
  gemeinsames Tickbudget. Rückkehr-/Warteproben teilen dagegen 250000 Settles;
  `ProcessReplanWaits` läuft nur alle acht Ticks. Fertig: viele gleichzeitige Fluchten,
  wiederholt erfolglose Versuche, viele Wolken und lange Wartelisten vermessen;
  Tickspitzen, Allokationen und faire Bedienung später Unit-IDs belegt. Optimierung
  nur bei gemessenem Bedarf; Zeitmessungen nie als MP-Entscheidungsbudget verwenden.
- [ ] **T8: Kombination und Standalone prüfen.**
  **Offline teilweise geprüft, Spielkombinationen offen:** tatsächliche APIShared-
  Owner-Union, fehlende/unverfügbare optionale Höhenpolicy und Regeländerungen
  geprüft. Andere Mods im echten Spiel nicht neu abgenommen.
  Nur harte Abhängigkeiten sowie mit BFQoL, Fixes und ExtraFeatures-Höhenpolicy;
  Feuer und Pest einzeln/gemeinsam,
  eigene und fremde APIShared-Routenregeln, Tore/Gräben und Höhenwege.
  Fertig: Regeländerungen invalidieren Kosten/Wege korrekt; Ausfall eines optionalen
  Dienstes lässt Vanilla und unabhängige Funktionen bestehen. Vorhandene allgemeine
  SP-Abnahmen mit gesperrten Gefahren gelten hierfür nicht als Abnahme.
- [ ] **T9: Echter Zweirechner-MP-Test (auch allgemeine Migrations-Releasevoraussetzung).**
  Host/Client und KI, gleiche wirksame Settings/Policies, gleichzeitige Gefahren,
  neue Befehle, Save/Load und viele wartende/fliehende Soldaten. Fertig: gleiche
  Entscheidungen, Gruppen, Endzustände und gespeicherte Fallbytes; keine Desyncs.
- [ ] **T10: Manueller Augentest (auch allgemeine Migrations-Releasevoraussetzung).**
  Flucht/Rückkehr ohne Gleiten, korrekte Formation, Auswahl, Marker, Garnisonen und
  sichtbare Gefahr; pausierter Zustand, Editor und Einstellungsdarstellung prüfen.
  Fertig: datierter Sichtbefund mit Konfiguration und Grenzen. Vor Eingabesteuerung
  durch einen Agenten ausdrücklich die Freigabe gemäß Arbeitsanweisungen einholen.

## Freigabearbeiten

- [ ] **R1: Abnahme reproduzierbar vorbereiten.**
  **Vorbereitet, nicht abgenommen:** der getrennte
  [Gefahren-Testmod](../../Testmods/PathfindingHazardTest/ACCEPTANCE.md) ist kompiliert;
  97 Konfigurations-/reale-DLL-Reflexionsprüfungen bestehen. Sechs Szenarien
  beobachten echte Flucht/Rückkehr sowie begrenztes/unbegrenztes Warten für beide
  Gefahren. Passende echte Testsaves und die tatsächliche Ausführung fehlen noch.
  Kein Spielstart dieses Auftrags und kein erfolgreicher Gefahrenlauf aus einer
  Offline-Fixture abgeleitet. Fehler/Cancel und pausiertes Cleanup selbst abnehmen.
  Die produktive Sperre bleibt bis zur Freigabe bestehen. Für spätere Spieltests einen getrennten
  Testweg mit explizit aktivierten EngineOptions festlegen; kein Lauf, der weiterhin
  `AvoidFire=false`/`AvoidPlague=false` nutzt, darf als Gefahrentest zählen.
  Fertig: Testprotokoll weist effektive Optionen und echte Scan-/Fluchtcallbacks nach.
- [ ] **R2: Nach B1 und vollständiger Abnahme die Funktionen bewusst freigeben.**
  `EngineOptions.Capture`, beide ausgeblendeten XAML-Bereiche und
  `SettingsDependencyTests` gemeinsam umstellen; unabhängige Core-/Einzelrouten-
  Abhängigkeiten und Hostsettings erhalten. Alte Presets/Saves dürfen vor Freigabe
  nichts aktivieren; nachher gilt die ausdrücklich gewählte Default-/Opt-in-Politik.
  Fertig: Settings-, Such-, Locale-, Offline-, Save-, Ingame- und MP-Prüfungen passend
  zum freigegebenen Verhalten bestanden. Keine bloße Anpassung roter Erwartungen.
- [ ] **R3: Abnahmestand und Spielerbeschreibung abschließen.** Neue Resultate mit
  Quell-/DLL-/SDK-Identität, Szenario, Ergebnis und Grenzen festhalten. Vor einer
  README-Ergänzung gesondert fragen; Version erst nach fertiger Freigabe ändern.
  Commit und Veröffentlichung ausschließlich auf ausdrücklichen Nutzerauftrag.

## Allgemeine Verbesserungen und Untersuchungen

Übernommen am 13.11.2026 aus der bisherigen Verbesserungsdatei, deren Prüfstand
Modpack `e95da5315e2f7567d7ae8c8f6d24f75f3e7804b0` und APIShared
`d91fd86a6fbecd9d400062b5f44f534e52bbb00f` war. Implementierungs- und
Abnahmestand vor einer Fortsetzung erneut prüfen. Die Kandidaten G1–G9 sind
Untersuchungsaufträge, keine bestätigten Fehler und keine automatische
Umsetzungsfreigabe für den Release vom 13.11.2026. Erledigte oder unverändert
verworfene Versuche sind keine neuen Aufgaben. Historische Gegenbeispiele stehen
in den [Optimierungslehren](../migration/moatmove-notes.md#bewahrte-optimierungslehren).
Native RVAs gelten ausschließlich für den oben genannten DLL-Hash; neue
Vanilla-Erkenntnisse in der [aktuellen Baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) sichern.
Die frühere Feuer-/Pestaufgabe 4.1 ist mit B1, Q1–Q4, T1–T10 und R1–R3
zusammengeführt und wird hier nicht als zweite Aufgabenliste geführt.

### Native Feindsuche beschleunigen

#### Ausgangspunkt und Messgrenzen

`0x18C160` ermittelt nächste Gegnerentfernungen und gegebenenfalls ein Angriffsziel mit nachfolgenden Bewegungsaktionen. Es ist kein Ersatz für die bereits optimierten Gruppen- oder Einzelwegsuchen. Reguläre Aufrufe sind maskengesteuert verteilt; `0x19A1F0` kann sofortige Prüfungen anfordern. Die vorhandene Cadence bleibt bei den folgenden Arithmetikkandidaten unverändert.

Die **historischen Profile vor dem Spectator-Fix** zeigen 32,260 % eigene Simulationsstichproben in `current-p1` und 24,960/24,644 % in `spectatoroff-p1/p2`. Das sind Anteile der gesamten Funktion, keine gemessenen Divisionsanteile und keine aktuellen Leistungszusagen. Das Ausschalten anderer Funktionen verändert außerdem den untersuchten Ablauf.

Quellen: [Native Ablauf-, Cadence- und Arithmetikverträge](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md#enemy-scan-cadence-and-integer-contracts-2026-11-13-vanilla-facts), [Profilaggregation](../../.work/a/stress90/001/profile-summary.json) und zugehörige Originalprotokolle unter `.work/a/stress90/001/results/`. Der Sampler ordnet native Blattadressen Funktionen zu; einzelne Instruktionskosten sind daraus nicht bestimmbar.

#### G1: Zielscore – festen Divisor spezialisieren

- [x] **Offline-Untersuchung abgeschlossen, Kandidat nicht integriert:** tatsächlicher
  Assembler und installierter RedBird-Backend geprüft; mit Einbaukosten langsamer
  (Score +7,3 bis +29,0%). Original bleibt aktiv. Vollständige signed-/Maschinen-
  Nachweise und reproduzierbarer Benchmark in [OfflineOptimization](../Findings/OfflineOptimization.md).
  Eine andere Einbauform benötigt einen neuen Gleichheits- und Nettogewinnnachweis.

Der originale aktuelle Tabellenzugriff muss erhalten bleiben. Keine fest einprogrammierte Jittertabelle und keine ungeprüfte Annahme über negative Indizes: normale Erzeugung liefert nichtnegative Seeds, der vollständige Save-/Transfervertrag ist jedoch noch offen, und die Tabellenregion ist schreibbar. Signed Addition, Modulo, Rundung gegen null, Listencount und Reihenfolge dürfen sich nicht ändern.

**Vertrag für jeden späteren Versuch:** Gleichheit auch an signed Grenzen,
unveränderte Ziel-/Minimum-/Folgezustände und vollständige Register-/Flag-/Stack-
Verträge. Die jetzigen Kandidaten bestehen 7.095.335 gemeinsame G1/G2-Prüfungen,
erbringen jedoch keinen Nettogewinn. Keine produktiven Zusatzhooks angelegt.

#### G2: Distanz – nicht benötigte Divisionen erkennen

Die exakte Formel lautet für `M=max(|dx|,|dy|)>0`, `m=min(...)`:

    P = floor(2*m/5)*m
    D = M + floor(P/M)

Der Nullfall bleibt separat. Bei den eingelesenen signed-short Koordinaten ist `P` höchstens 1.717.934.490; `D>=M` ist damit auch unter der tatsächlichen Ganzzahlarithmetik gesichert. Falls `P<M`, ist der Quotient exakt null. Der konstante `/5`-Teil ist im Original bereits durch Multiplikation optimiert; die verbleibende Division durch `M` ist der Untersuchungskandidat.

- [x] **Offline-Untersuchung abgeschlossen, Kandidat nicht integriert:** der exakte
  `P<M`-Pfad erhält Quotient/Rest und beide Major-Zweige, ist einschließlich tatsächlicher
  Hooksprünge und Logikschalter +13,9 bis +16,3% langsamer. Original bleibt aktiv.
  Keine weitergehende Minimum-/Zielabschneidung ohne eigenen vollständigen Nachweis.

Zu erhalten sind insbesondere die inklusive Reichweite, der separate Heimursprungs-Zweig,
alte Listeneinträge vor dem Global-ID-Test und die einmal eingelesene Actor-Position.
Eine reine Reichweitenprüfung vom aktuellen Standort ist kein ausreichender Ersatz.

#### G3: Räumlicher Index erst nach zusätzlichem Audit

**Offline eingegrenzt, weiter offen:** konkrete Writer-/Reihenfolge-Gegenbeispiele
und benötigter gleichzeitiger Trace-/Replayvertrag in OfflineOptimization gesammelt.
Kein produktiver Index ohne vollständigen Aktualisierungs- und Kostenvertrag.

- [ ] **Weitergehende Untersuchung, keine abgesicherte Lösung:** Ein Index könnte Kandidatenarbeit reduzieren, benötigt aber einen vollständigen Zustands- und Aktualisierungsvertrag. Vorher müssen Bewegung, Spawn, Teleport, Slotwiederverwendung, Laden, Transfer sowie der Zustand zum jeweiligen Scanzeitpunkt innerhalb des Ticks geschlossen sein. Ein Snapshot vom Tickanfang reicht bei aufsteigenden Unitupdates nicht automatisch aus.

Originale Listenzugehörigkeit, Count und Gleichstandsreihenfolge müssen erhalten bleiben. Entfernungsminimum, Auxminimum und gewichtete Zielwahl sind getrennte Verbraucher. Vanillas Distanzapproximation besitzt keine allgemeine Dreiecksungleichung; Euclid-Radien und gewöhnliche metrische Baumabschneidungen sind deshalb kein Gleichheitsbeweis. Die detaillierten Gegenbeispiele stehen in der Baseline. Diesen größeren Umbau erst bewerten, wenn die kleineren Arithmetikmaßnahmen nicht ausreichen und die Indexpflege mitgemessen werden kann.

### Verbleibende Such- und Aktualisierungskosten

#### G4: Extreme Regeln und Budget-Rückfälle

**Vorbereitet:** aktuelle Budget-/Fallbackverträge verfolgt; Messmatrix trennt Kern,
Vanilla und Wächter. Aktuelle Ingame-Häufigkeit bleibt offen.

- [ ] **Aktuelle Restkosten erfassen und Rückfälle untersuchen.** Die alten `pf-cons-3`-/`pf-rule-*`-Messungen zeigten Restspitzen bei kartenweiten Sperren, wenigen Durchlässen und vielen gleichzeitigen Aufträgen. **Die Budgets selbst sind vorhanden**: Für Boden-Einzelwege gilt ein Budget von 500.000 Knoten je Tick. Assassinenwege werden angerechnet, bei Erschöpfung aber ausdrücklich nicht begrenzt oder an ihre teurere Vanilla-Kette übergeben; die Zahl ist deshalb keine harte Obergrenze aller Einzelwegsuchen. Umplanprüfungen besitzen ein eigenes deterministisches Budget. Bodenbefehle können danach wieder Vanillas Suche ausführen, die zusätzliche Kachelregeln nicht kennt.

**Offen:** Prüfen, ob ein genauerer Umweg-/Arbeitskostenschätzer oder eine andere deterministische Aufteilung teure Rückfälle reduziert. Zuerst heutige Restkosten getrennt nach Kern, Vanilla-Fallback und Umplanwächter erfassen. Keine Verschiebung von Gruppenbefehlen über Ticks, kein wall-clock Budget und keine Gleichsetzung eines Budgetabbruchs mit Unerreichbarkeit. Der bestehende Fallback-Vertrag darf nicht stillschweigend geändert werden.

Einstieg: [AnchorRouting](../../Pathfinding/src/Engine/AnchorRouting.cs), [RouteReplanWatch](../../Pathfinding/src/Engine/RouteReplanWatch.cs), [vorhandene Integrationsprüfungen](../../Pathfinding/tests/Engine/AnchorIntegrationTests.cs) und [native Suchverträge](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUND_ROUTE_BUILDERS.md). Historische Größenordnungen sind keine Messung des heutigen Quellstands.

#### G5: Cursor-Topologie nach Regionsänderungen

**Vorbereitet:** Mapepoch-/RegionGeneration-/Diplomatiepfade und vorhandene
DEVDIAG-Messmarker dokumentiert; vollständige lokale Updategarantie bleibt offen.

- [ ] **Späte Topologie-Neuaufbauten profilieren und erst bei Bedarf optimieren.** Tabellenbasierter Aufbau, Graphspeicher-Wiederverwendung und Vorbau beim Kartenstart sind vorhanden. Bei geänderter `RegionGeneration` wird die Cursor-Topologie dennoch ersetzt und bei Bedarf vollständig neu aufgebaut.

**Offen:** Ursache, Häufigkeit und Kosten solcher späteren Neuaufbauten im aktuellen Stand erfassen. Erst bei relevanter Last prüfen, ob betroffene Bereiche exakt lokal aktualisiert oder vermeidbare Invalidierungen eingespart werden können. Vor einem Wiederverwenden müssen sämtliche relevanten Regions-/Kantenänderungen nachweislich erfasst sein; ein einfaches Beibehalten des alten Graphen ist keine Lösung.

Einstieg: [Moved.CursorConnectivity](../../Pathfinding/src/Engine/Moved/Moved.CursorConnectivity.cs), [Vorbau](../../Pathfinding/src/Engine/FastMovementScheduler.cs), [CursorTests](../../Pathfinding/tests/Engine/CursorTests.cs). Aufbauzeit, Laden und spätere Laufzeitspitzen getrennt messen.

#### G6: Assassinen – unabhängige Restanfragen untersuchen

**Offline profiliert, Spielanteil offen:** tatsächlicher Produktionskern auf vier
kontrollierten Szenarien gemessen; kein spekulativer Zusatzcache integriert.

- [ ] **Offener Messpunkt:** Welche teuren Einzelanfragen bleiben außerhalb der bereits vorhandenen nativen Rückwärts-Gruppenfelder und ihrer geprüften Wiederverwendung? Erst bei einem belegten relevanten Restanteil gemeinsame Start-/Zielkontexte oder weitere exakte Wiederverwendung untersuchen.

Die alte Prognose „70 statt 1450 ms“ gehört zu einem anderen Aufbau und wird nicht übernommen. Weder eine erneute native Portierung noch ein pauschales Überspringen von `D9C40` ist hier geplant. Für eine neue Abkürzung sind die tatsächlich benötigten Such-, Rekonstruktions- und Folgezustände nachzuweisen.

Einstieg: [AssassinPathfindingRuntime](../../Pathfinding/src/Assassins/AssassinPathfindingRuntime.cs), [nativer Kern](../../Pathfinding/native/PathfindingAssassin.cpp), [Veröffentlichungstests](../../Pathfinding/tests/AssassinPathfinding.Tests/RuntimePublicationTests.cs), [Gruppenfeldvertrag](../../Pathfinding/UpdateToNewDLL.md) und die Native-Baseline.

#### G7: Häufungen großer Gruppenfelder

**Offline geprüft/profiliert, Spielanteil offen:** 2961 unabhängige Kernvergleiche
und synthetische 10000 Anfragen/40 Gruppen einschließlich Precise-, Thread- und
Batchvergleich. Laufzeiten/Qualitätsgrenzen stehen in OfflineOptimization.

- [ ] **Offener Messpunkt auch ohne extreme Zusatzregeln:** Mehrere teure Gruppenfelder können im selben Tick zusammenkommen. Der vorhandene Startfilter erkennt bereits innerhalb von höchstens 16 Knoten abgeschlossene Vorwärtskomponenten ohne Anker. Unerreichbare Starts in größeren Komponenten können ein Feld weiterhin bis zur Erschöpfung treiben. Die alte Roadmap beschrieb diesen Rest; dessen heutige Häufigkeit und Bedeutung sind noch zu ermitteln.

Vor weiteren Änderungen Feldaufbau, tatsächlich abgefragte Mitglieder, abgeschlossene Startkomponenten und Invalidierungen getrennt erfassen. Eine größere Vorprüfung nur bei nachgewiesenem Nettogewinn erwägen: Sie kostet selbst Sucharbeit und darf keinen erreichbaren Start ausschließen. Kein spekulatives KI-Batchen oder Verschieben der Befehle über Ticks wieder einführen.

Einstieg: [Gruppen-/Startfilter im nativen Kern](../../Pathfinding/native/PathfindingAnchor.cpp) und [AnchorRouting](../../Pathfinding/src/Engine/AnchorRouting.cs). Die vorhandenen Batch-, Einzelabfrage- und Determinismusgarantien bleiben maßgeblich.

### G8: Ziel auf einer verbotenen Kachel

- [x] **G8: Offline abgeschlossen – betroffenen Bewegungsbefehl vor Änderung ablehnen.**
  [ForbiddenMoveTargets](UnitCommands/Runtime/ForbiddenMoveTargets.cs) prüft
  ausschließlich aktive Pathfinding-Regeln und den echten lebenden Gruppeneigentümer.
  [PermanentCommandHooks](UnitCommands/Native/PermanentCommandHooks.cs) lehnt direkte
  Bewegungen und neue Queue-Wegpunkte vor Auftrags-/Queueänderungen ab; der
  Spielerwrapper erhält seinen vorherigen `+0x56C`-Wert. Verschachtelte Ablehnungen
  verändern keinen akzeptierten Elternauftrag. Die spätere Anchor-Subscription
  ignoriert übersprungene Pre-Ereignisse, für die der Extender kein Post sendet.
  Start-im-Verbot darf weiter verlassen werden. Keine Ersatzzielsuche und keine
  globale Änderung fremder Vanilla-Befehle oder APIShared-Verträge.
  Nachweis: 18 Friendly-Movement- und 9 integrierte tatsächliche Subscriberprüfungen.
  Sichtbares Auftrags-/Marker-/Queueverhalten und Host/Client bleiben T5/T9/T10.

### G9: Vergleichslücken schließen

**Vorbereitet:** [Prepare-ComparisonMatrix](../tools/Prepare-ComparisonMatrix.ps1)
erzeugt zwölf A/B–B/A-Parameterfälle und zusätzliche Szenarien. Wirksame Hostsettings
müssen vor späterem Lauf über bestehende Preset-/Test-API angewandt und ausgelesen
werden; das Werkzeug startet nichts. Sämtliche neuen Spielmessungen bleiben offen.

- [ ] **Isolierte Options- und weitere Szenarienvergleiche nachholen.** Die früheren Stressläufe ersetzen keine breite Abnahme. Weiter sinnvoll sind ein anderer Save mit Mauerringen/Belagerung, menschliche Massenbefehle und der Karteneditor sowie echte Host-/Client-Läufe für die jeweils veränderten Funktionen. Vorher vorhandene Nachweise auf ihren konkreten Umfang prüfen; einzelne bestandene Fälle nicht pauschal erneut als fehlend behandeln.

Für freundliche Grabenquerung und menschliche Assassinenwege fehlt weiterhin ein isolierter aktueller Vergleich gegen ihren ausgeschalteten Zustand. Die günstigen KI-Assassinen-Vergleiche gelten nur für deren gemessenes Szenario. Für weitere Performance-Einstufungen Funktionen einzeln mit identischem Quell-/SDK-Stand, Save, Zoom und wirksamen Einstellungen vergleichen. Render-FPS, Simulationstakte, Tickpausen, Sucharbeit und Allokationen getrennt bewerten. Die Wiederholungsläufe müssen auch Laden/erste Ticks und tatsächliche Bewegung abdecken, nicht nur die stabile Messphase.

Messmethodik und vorhandene Werkzeuge: [AGENTS/performance.md](../../AGENTS/performance.md), [Ingame-Testverfahren](../../AutomatisierungTest.md). Die am 13.11.2026 geprüften Release-Fixes und ihre Ergebnisse bleiben separat in [.work/a/perf-release/001/Ergebnis.txt](../../.work/a/perf-release/001/Ergebnis.txt); sie sind keine Umsetzung der hier aufgeführten Maßnahmen.

### Weiterführende Quellen und Abgrenzung

Der größere Suchkern-/Architekturprototyp hat eine eigene Entwicklungsgeschichte und offene Abnahmen in [BetterPathfinding.md](../../BetterPathfinding.md). Er ist keine bereits freigegebene Lösung der oben genannten Restprobleme. Hier werden seine alten Entwurfsvarianten nicht nochmals als neue Aufgaben eingereiht.

Wiederverwendbare Vanilla-Verträge: [Ground-Builder](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUND_ROUTE_BUILDERS.md), [Gruppenaufnahme](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md), [Tor-Abfragepass](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_QUERY_PASS.md), [Feuer und Bewegung](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIRE_EFFECTS.md), [Feindsuche](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md). Aktuelle Mod- und Hookverträge stehen in [Pathfinding/UpdateToNewDLL.md](../../Pathfinding/UpdateToNewDLL.md).

## Herkunft und Abgrenzung

Offene Migrationspunkte und dauerhaftes Integrationswissen wurden aus
`Pathfinding/MIGRATION.md`, Git-Blob `5cd5a2a5d2b44fc17a973986c3a2d5af0c0edc62`,
übernommen. Die kompakte [Migrationszusammenfassung](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/PATHFINDING_MIGRATION.md)
bewahrt die historischen Nachweise. Die BFQoL-Startmeldung `TryGetBuildingById(0/3999)`
ist laut späterem Nachtrag behoben und keine offene Aufgabe. Parallele
BFQoL-PlacementAcceptance-/Menü-/Autosave-Arbeiten sind kein Pathfinding-Backlog.
Die bisherigen allgemeinen Verbesserungsaufgaben sind im Abschnitt oben integriert.
Ihre ursprüngliche Fassung bleibt als Git-Blob
`657b3335389216a8780fa6752021bdc26fbaf60c` erhalten; Quellprüfstand und
Messgrenzen sind bei den übernommenen Aufgaben vermerkt.
