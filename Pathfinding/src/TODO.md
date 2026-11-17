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

## Ungeklärte Fragen (keine bestätigten Fehler)

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
  löscht die Queue. Abgewiesene Mitglieder mit altem Zustand 101/122 gelten nicht als
  Erfolg des neuen Befehls (offline behoben); tatsächliches Ankommen hier mitprüfen.
- [ ] **T6: Save-/Lifecycle-Matrix ergänzen.**
  **Offline erweitert, echte Saves/Editor offen:** überlappender Solo-/Fluchtbesitz,
  v1-Fallbytes,
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
- [ ] **R2: Nach vollständiger Abnahme die Funktionen bewusst freigeben.**
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
Abnahmestand vor einer Fortsetzung erneut prüfen. Die Kandidaten G3–G9 sind
Untersuchungsaufträge, keine bestätigten Fehler und keine automatische
Umsetzungsfreigabe für den Release vom 13.11.2026. Erledigte oder unverändert
verworfene Versuche sind keine neuen Aufgaben. Historische Gegenbeispiele stehen
in den [Optimierungslehren](../migration/moatmove-notes.md#bewahrte-optimierungslehren).
Native RVAs gelten ausschließlich für den oben genannten DLL-Hash; neue
Vanilla-Erkenntnisse in der [aktuellen Baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) sichern.
Die frühere Feuer-/Pestaufgabe 4.1 ist in Q2, Q4, T2–T10 und R1–R3 aufgegangen und
wird hier nicht als zweite Aufgabenliste geführt. Offline erledigte Punkte (überlappender
Solo-/Fluchtbesitz, Gefahrenverhalten, Zustand 101/122, Scannerquellen, Ziel auf
verbotener Kachel) sind entfernt; ihre Spielabnahme steht in T2–T10.

### Native Feindsuche beschleunigen

#### Ausgangspunkt und Messgrenzen

`0x18C160` ermittelt nächste Gegnerentfernungen und gegebenenfalls ein Angriffsziel mit nachfolgenden Bewegungsaktionen. Es ist kein Ersatz für die bereits optimierten Gruppen- oder Einzelwegsuchen. Reguläre Aufrufe sind maskengesteuert verteilt; `0x19A1F0` kann sofortige Prüfungen anfordern. Die vorhandene Cadence bleibt bei den folgenden Arithmetikkandidaten unverändert.

Die **historischen Profile vor dem Spectator-Fix** zeigen 32,260 % eigene Simulationsstichproben in `current-p1` und 24,960/24,644 % in `spectatoroff-p1/p2`. Das sind Anteile der gesamten Funktion, keine gemessenen Divisionsanteile und keine aktuellen Leistungszusagen. Das Ausschalten anderer Funktionen verändert außerdem den untersuchten Ablauf.

Quellen: [Native Ablauf-, Cadence- und Arithmetikverträge](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md#enemy-scan-cadence-and-integer-contracts-2026-11-13-vanilla-facts), [Profilaggregation](../../.work/a/stress90/001/profile-summary.json) und zugehörige Originalprotokolle unter `.work/a/stress90/001/results/`. Der Sampler ordnet native Blattadressen Funktionen zu; einzelne Instruktionskosten sind daraus nicht bestimmbar.

#### G3: Räumlicher Index erst nach zusätzlichem Audit

**Offline eingegrenzt, weiter offen:** konkrete Writer-/Reihenfolge-Gegenbeispiele
und benötigter gleichzeitiger Trace-/Replayvertrag in OfflineOptimization gesammelt.
Kein produktiver Index ohne vollständigen Aktualisierungs- und Kostenvertrag.

- [ ] **Weitergehende Untersuchung, keine abgesicherte Lösung:** Ein Index könnte Kandidatenarbeit reduzieren, benötigt aber einen vollständigen Zustands- und Aktualisierungsvertrag. Vorher müssen Bewegung, Spawn, Teleport, Slotwiederverwendung, Laden, Transfer sowie der Zustand zum jeweiligen Scanzeitpunkt innerhalb des Ticks geschlossen sein. Ein Snapshot vom Tickanfang reicht bei aufsteigenden Unitupdates nicht automatisch aus.

Originale Listenzugehörigkeit, Count und Gleichstandsreihenfolge müssen erhalten bleiben. Entfernungsminimum, Auxminimum und gewichtete Zielwahl sind getrennte Verbraucher. Vanillas Distanzapproximation besitzt keine allgemeine Dreiecksungleichung; Euclid-Radien und gewöhnliche metrische Baumabschneidungen sind deshalb kein Gleichheitsbeweis. Die detaillierten Gegenbeispiele stehen in der Baseline. Diesen größeren Umbau erst bewerten, wenn die Indexpflege mitgemessen werden kann. Die kleineren Arithmetikkandidaten (fester Divisor im Zielscore, Division in der Distanz) wurden offline untersucht und mangels Nettogewinn nicht integriert; Nachweise und Verträge in [OfflineOptimization](../Findings/OfflineOptimization.md).

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

### G9: Vergleichslücken schließen

**Vorbereitet:** [Prepare-ComparisonMatrix](../tools/Prepare-ComparisonMatrix.ps1)
erzeugt zwölf A/B–B/A-Parameterfälle und zusätzliche Szenarien. Wirksame Hostsettings
müssen vor späterem Lauf über bestehende Preset-/Test-API angewandt und ausgelesen
werden; das Werkzeug startet nichts. Sämtliche neuen Spielmessungen bleiben offen.

- [ ] **Isolierte Options- und weitere Szenarienvergleiche nachholen.** Die früheren Stressläufe ersetzen keine breite Abnahme. Weiter sinnvoll sind ein anderer Save mit Mauerringen/Belagerung, menschliche Massenbefehle und der Karteneditor sowie echte Host-/Client-Läufe für die jeweils veränderten Funktionen. Vorher vorhandene Nachweise auf ihren konkreten Umfang prüfen; einzelne bestandene Fälle nicht pauschal erneut als fehlend behandeln.

Für freundliche Grabenquerung und menschliche Assassinenwege fehlt weiterhin ein isolierter aktueller Vergleich gegen ihren ausgeschalteten Zustand. Die günstigen KI-Assassinen-Vergleiche gelten nur für deren gemessenes Szenario. Für weitere Performance-Einstufungen Funktionen einzeln mit identischem Quell-/SDK-Stand, Save, Zoom und wirksamen Einstellungen vergleichen. Render-FPS, Simulationstakte, Tickpausen, Sucharbeit und Allokationen getrennt bewerten. Die Wiederholungsläufe müssen auch Laden/erste Ticks und tatsächliche Bewegung abdecken, nicht nur die stabile Messphase.

Messmethodik und vorhandene Werkzeuge: [AGENTS/performance.md](../../AGENTS/performance.md), [Ingame-Testverfahren](../../AutomatisierungTest.md). Die am 13.11.2026 geprüften Release-Fixes und ihre Ergebnisse bleiben separat in [.work/a/perf-release/001/Ergebnis.txt](../../.work/a/perf-release/001/Ergebnis.txt); sie sind keine Umsetzung der hier aufgeführten Maßnahmen.

### G10: Wegform bei größeren Hindernissen

- [ ] **Umgesetzt in 0.1.9 (16.11.2026); Host/Client-Abnahme offen.**
  Ursache: Gleich teure gerade/diagonale Schritte plus die Richtungswahl
  „niedrigster Index zuerst“ in `Descend` erzeugten große L-Formen. Der
  Direktstrahl aus 0.1.8 half nur bei vollständig freier Strecke. Neu begradigt
  `StraightenRoute` im nativen Kern jede gefundene Boden- oder Grabenroute
  kostenneutral (Rasterstrahl nur bei erlaubten Kanten und Kosten ≤ Abschnitt).
  Das ist ohne eigene Option fest in „Gruppenwege gemeinsam berechnen“ und die
  schnelle Einzelwegsuche eingebaut (Nutzerentscheidung vom 16.11.2026).

Offline-Ergebnis siehe [Findings/RouteShape.md](../Findings/RouteShape.md):

- **Kosten:** Keine Route wird teurer, kein Statuswechsel. Etwa 15 % der Routen
  werden billiger, weil Anker-Umwege wegfallen.
- **Wegform:** Im echten Capture halbieren sich die geraden Teilstücke pro Route
  ungefähr.
- **Laufzeit:** Einzelwege bleiben unverändert. Gruppenbefehle kosten je Einheit
  etwa 2–10 µs mehr, gemessen +13–73 % bzw. +6–26 ms je 10.000 Einheiten.

Die Gruppenmehrkosten hat der Nutzer am 16.11.2026 akzeptiert, solange FPS und
Ticks nicht spürbar leiden. Der Einzelspieler-Spieltest vom selben Tag ist bestanden:
gültige Routen, gleiche Ankunft und gleiche Befehlskosten bei 5000 Einheiten. Details
stehen in Findings/RouteShape.md. Tooltips und Version 0.1.9 sind angepasst.

Offen:

- Host/Client-Abnahme (die Routenbytes ändern sich; die Version muss übereinstimmen).
- Im Spieltest war die Begradigung nicht sichtbar: Bei Spieler-Gruppenbefehlen liefen
  nur 18–19 Mitglieder über den Pathfinding-Routenbau, und deren Wege waren bereits
  gerade. Ein Spielszenario mit großer, weit verteilter Gruppe steht noch aus.
- Eigener Punkt, nicht durch G10 verursacht: In zwei Läufen vom 16.11.2026 lösten
  204 zusätzliche globale Invalidierungen (`global=216`) eine kartenweite Neuberechnung
  aus; die Quelle ist offen. Die DEVDIAG-Messpunkte `pf-global`/`pf-resweep` (nur
  `/devdiag`) zeigen sie beim nächsten Auftreten. Die übrigen Auslöser sind behoben,
  siehe Findings/RouteShape.md.

Das Werkzeug [tools/RouteShape](../tools/RouteShape/Run-RouteShape.ps1) vergleicht
Basis und Arbeitsstand auf identischen Anfragen. Freie Flächen, Mauerringe,
Engstellen, gerichtete Kanten, Gräben, Strafkacheln und große Gruppen sind
enthalten. Flüssiges Spielen und kurze Suchzeiten behalten Vorrang vor schöneren
Wegen.

### G11: Grabenquerung für weitere Einheitentypen (optional, großer Umfang)

- [ ] **Optionale Erweiterung, keine aktuelle Umsetzungsfreigabe.** Vor einem
  eigenen Auftrag Umfang und Risiken ausdrücklich abwägen: Die vollständige
  Erweiterung ist insgesamt **mittel bis hoch riskant** und kein kleiner
  Modsettings-Zusatz. Statischer Audit und Risikobewertung vom 15.11.2026 sind
  abgeschlossen; Implementierung, Spielabnahme und konkrete Hookauswahl sind offen.

Gewünschter Umfang: Die bestehende Option bleibt für grabenfähige Einheiten.
Zwei zusätzliche, standardmäßig ausgeschaltete Hostoptionen geben unabhängig
davon Zivilisten beziehungsweise übriges Militär frei. Zusammen sollen die drei
Schalter alle einem Spieler zugeordneten, ansonsten bewegungsfähigen Einheiten
abdecken: eigene/verbündete fertige Gräben, zivile Transporttiere, Reiter und mobile
Belagerungsgeräte eingeschlossen. Zivilisten benötigen vollständige automatische
Arbeitszielsuche, Hinwege, Ablieferung und Rückwege. Die reine Durchquerung verleiht
keine Grabenarbeitsfähigkeit; stationäre Objekte erhalten keinen neuen Bewegungsbefehl.

| Teilbereich | Implementierungsrisiko | Konfliktrisiko mit anderen Mods |
| --- | --- | --- |
| Modsettings und bestehende Synchronisierung | Niedrig | Niedrig |
| Weitere normale Soldaten einschließlich Reiter | Mittel | Niedrig bis mittel |
| Katapulte und Feuerballisten | Hoch | Mittel |
| Vollständige zivile Arbeitswege | Hoch | Mittel bis hoch |
| Spaziergänge und tierische Sonderbewegungen | Mittel bis hoch | Mittel |

Dies sind technische Einschätzungen aus dem Audit, keine gemessenen
Fehlerwahrscheinlichkeiten und kein Nachweis unvermeidlicher Inkompatibilität.
Das genaue Konfliktrisiko hängt von den später gewählten Eingriffsstellen ab.

Wesentliche Grenzen für eine spätere Entscheidung:

- Bewegung, Cursor, Gruppen-/Angriffsbefehle, Platzierung und Veröffentlichung
  müssen dieselbe einheiten- und spielerbezogene Freigabe verwenden. `CanDigMoat`
  nicht pauschal erweitern: Es wird auch für echte Grabenarbeit benutzt.
- Zivile Zielprüfungen sind unterschiedlich; einige vergleichen Regionen direkt.
  Gebäudezugangsprüfungen verändern gemeinsam verwendete Zugangskacheln und
  Zustände, die auch die KI auswertet. Keine globale positive Erreichbarkeit
  oder pauschale Typ-/Oberflächenfreigabe als Abkürzung voraussetzen.
- Katapulte und Feuerballisten besitzen außerhalb der Wegsuche eine Vanilla-Regel,
  die sie auf fertigem Graben über Zustand 110 schließlich entfernt. Diese Regel,
  sonstige Entfernungsgründe und Besatzungsbedingungen getrennt berücksichtigen.
- Spaziergänge haben einen eigenen Routenpublisher. Herdenziele und manche
  Tier-/Arbeitszustände benötigen zusätzliche Prüfungen; vorhandene
  Soldaten-Kostenmodelle belegen nicht die Kosten aller Zivilistenwege.
- Zusätzliche Suchen für viele Arbeitszielkandidaten können teuer werden. CPU-Zeit,
  Sucharbeit, wiederholte Fehlerpfade und tatsächliche Bewegung messen; Mehrkosten
  sind derzeit nicht quantifiziert.
- Script Extender/APIShared besitzen bereits Oberflächen-, Assassinen-, Tor- und
  Jäger-Eingriffe. Fixes verändert Regionsdaten/Grabenhilfen; BugfixesAndQoL nutzt
  Gebäudezugänglichkeit für KI-Schutz und verändert Heilerziele; ImprovedHunters
  erweitert Jägerzielsuche. Diese Verträge erhalten und konkrete Hookspannen vor
  Implementierung prüfen. Eine spätere Ladereihenfolge allein beweist keine
  Verträglichkeit bei überlappenden oder semantisch widersprüchlichen Eingriffen.
- Gespeicherte und bereits laufende Wege, Abschalten der Optionen zwischen Partien,
  Besitzer-/Bündnis-/Terrainwechsel, Karteneditor und echter Host-/Clientbetrieb
  benötigen gesonderte Abnahme. Kein neues Saveformat ist aus dem Audit als nötig
  belegt; native Wege und Grabenmarker werden bereits gespeichert/übertragen.

Empfohlene Reihenfolge **nur bei späterer gesonderter Freigabe**: zusätzliches
normales Militär zuerst bewerten, mobile Belagerungsgeräte separat behandeln und
vollständige Zivilistenwege als eigenen größeren Entwicklungsschritt planen.
Keine konkrete Hookarchitektur oder universelle Kompatibilität ist damit zugesagt.

Wiederverwendbare Vanilla-Verträge, vollständige Rollenmatrix, Maschinenbelege und
getrennte Integrationsgrenzen:
[Audit zur Grabenquerung aller Einheitentypen](../../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MOAT_TRAVERSAL_ALL_UNITS.md).

### G12: Abgesenkte Zugbrücke bei geschlossenem Tor für den Besitzer (optional)

- [ ] **Optional, Nutzerentscheidung vom 17.11.2026: vorerst so belassen.** Die neue
  Einstellung für das Zugbrückenverhalten soll diese Grenze kurz und verständlich nennen.

Ausgangslage: Die Zugbrückensperre für Feinde ist seit 0.1.10 in Pathfinding integriert
(Einstellung `EnableEnemyDrawbridgeClosure`, `src/GateRouting/DrawbridgeDeckClosure.cs` und die
Deck-Kantenmaske in `GateTopologySnapshotProvider.cs`). Spielabnahme: früherer Testmod
`Testmods/EnemyBridgePathTest`, am 17.11.2026 entfernt; bei Bedarf mit
`git checkout 582d6e57f -- Testmods/EnemyBridgePathTest` wiederherstellen.
- Sie behandelt eine abgesenkte Zugbrücke vor einem Torhaus im Regionsaufbau wie
  hochgezogen.
- Zugelassene Spieler gelangen nur über das Torportal auf und über das Deck. Das Portal
  ist nur bei offenem Tor aktiv (`enabled = Torzustand != 2`).
- Folge: Ist das Tor geschlossen, die Brücke aber noch unten (Einheiten auf dem Deck,
  laufende Animation, Handsteuerung), dann gelten Deck, Brückenkopf und nur seitlich
  über das Deck erreichbares Land auch für Besitzer und Verbündete als unerreichbar.
- Das betrifft nur Ziele, die ausschließlich über das Deck erreichbar sind. Bei einem
  anderen Weg (eine Region) nutzen Besitzer und Verbündete das Deck weiter, weil die
  Kantenmaske nur nicht zugelassene Spieler sperrt.
- Sichtbar wird das bei einer Mauerlücke direkt neben dem Torhaus
  (`test_gates_bridge2.sav`, Tor 600). Eigene Einheiten aus dem Graben erreichen den
  Keep dann nicht über das Deck, während Vanilla diesen Seitenweg zulässt.

Erste Ansätze (vor Umsetzung jeweils nativ auditieren und messen):

1. **Eigener Portaleintrag je Zugbrücke** statt Umleitung des Torportals.
   - Ein Eintrag mit Endpunkten Landestelle ↔ Deckregion (dritter Endpunkt z. B. seitliche
     Landregion), Besitzer und Eroberer des Tors, `enabled` = Brücke abgesenkt statt
     Torzustand.
   - Vorab klären:
     - Allokation über `0xD8510`.
     - Typ mit `ret`-Handler (Tabelle `0x3125D0`), damit `0xF4540` den Eintrag nicht überschreibt.
     - Pflege der Regionsfelder nach jedem `0xE49D0`.
     - Freigabe beim Zerstören/Neuladen.
     - Ob die Portaltabelle in Spielständen gespeichert wird. Verwaiste Einträge nach dem
       Entfernen des Mods wären ein Spielstandrisiko.
2. **Seitliche Deckkanten zur Innenseite behalten.**
   - Seitliches Land, das ohne Deck die Region des Burginneren (Torendpunkt B) hätte,
     gehört zur Burg.
   - Braucht eine Klassifizierung vor dem Fluten, z. B. aus dem letzten Aufbau oder einer
     begrenzten lokalen Flut.
   - Risiko: Ohne Kantenmaske würden Feinde über diese Kanten wieder hineinplanen. Die
     Pro-Spieler-Maske muss sie daher weiter sperren, und Region und Kachelsuche müssen für
     Feinde konsistent bleiben (sonst teure Fehlsuchen).
3. **Portal bei „Tor zu, Brücke unten“ für Zugelassene aktiv lassen** (`enabled` in der
   `0xF4540`-Nachbearbeitung setzen).
   - Billig, aber Besitzersuchen durch das physisch geschlossene Tor scheitern dann
     kachelweise.
   - Folgen für das automatische Öffnen eigener Tore, die Cursorvorschau und die
     Gebäudesperre der schnellen Suche (liest `+0x203C`) prüfen.

Abnahme: Testmod-Szenarien `bridge-<save>` und `bridge-ai-on|off-<save>` mit
`test_gates_bridge2.sav` (Tor zu, seitliche Lücke) und `test_gates_bridge3.sav` (zweiter
Weg). Hinzu kommt ein neuer Befehl „eigene Einheiten Graben → Keep“, der mit der
Erweiterung gelingen und für Feinde weiterhin ohne Deckpfad bleiben muss.

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
