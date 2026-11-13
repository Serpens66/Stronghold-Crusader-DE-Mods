# Offene Pathfinding-Verbesserungen und Untersuchungen

Stand **13.11.2026**. Abgeglichen mit Modpack-HEAD `e95da5315e2f7567d7ae8c8f6d24f75f3e7804b0` und APIShared `d91fd86a6fbecd9d400062b5f44f534e52bbb00f`. Vor einer Fortsetzung Implementierungs- und Abnahmestand erneut prüfen.

Diese Datei ersetzt die offenen Arbeitslisten aus `PathfindingRoadmap.md` und `PathfindingFinalReport.md`. Sie enthält verbleibende Untersuchungen, mögliche Verbesserungen und ausstehende Freigaben. **Keine dieser Maßnahmen ist durch diese Sammlung zur Umsetzung für den Release vom 13.11.2026 freigegeben.** Erledigte Arbeiten und unverändert verworfene Versuche sind keine neuen Aufgaben.

Native RVAs beziehen sich ausschließlich auf SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Identität und Verträge stehen in der [aktuellen Baseline](_inspect/CrusaderDE-Native-Baseline/CURRENT.md); neue Erkenntnisse über Vanilla gehören dorthin. Diese Arbeitsliste dokumentiert, was darüber hinaus noch zu tun ist. Historische Gegenbeispiele stehen kompakt in den [Optimierungslehren](Pathfinding/migration/moatmove-notes.md#bewahrte-optimierungslehren).

## 1. Prioritäten und Status

| Thema | Status | Nächster sinnvoller Schritt |
|---|---|---|
| Native Feindsuche: exakte Arithmetik | Statisch begründete Kandidaten, kein Leistungsnachweis | Originalgleichheit prüfen, tatsächliche Teilkosten und Nettogewinn messen |
| Extreme Regeln und teure Vanilla-Rückfälle | Restproblem historisch gemessen; heutige Größenordnung offen | Aktuellen Burst reproduzieren und deterministischen Kosten-/Fallback-Schätzer untersuchen |
| Häufungen großer Gruppenfelder | Kleine abgeschlossene Starts bereits gefiltert; größerer Rest offen | Aktuelle Feldkosten und unerreichbare Startkomponenten getrennt profilieren |
| Späte Cursor-Topologie-Neuaufbauten | Vollaufbau bei neuer Regionsgeneration weiterhin vorhanden | Kosten und Invalidierungsursachen erfassen; erst danach lokale Aktualisierung untersuchen |
| Verbleibende unabhängige Assassinenanfragen | Gruppenfälle bereits optimiert; Einzelrest nicht ausreichend isoliert | Nur verbleibende Anfragen außerhalb der bestehenden Gruppenfelder neu profilieren |
| Feuer-/Pestvermeidung freigeben | Implementierung vorhanden, Release-Gate weiterhin geschlossen | Vollständige Ingame-, Sicht-, Save- und Host-/Client-Abnahme |
| Ziele auf verbotenen Kacheln | Offene Vertrags-/Gameplayentscheidung | Gewünschte Zielsemantik entscheiden, bevor ein Verhalten geändert wird |
| Belastbare Einzeloptions- und Szenarienvergleiche | Vergleichslücken vorhanden | Grabenquerung und menschliche Assassinenwege isoliert sowie weitere Karten/Lasten prüfen |

## 2. Native Feindsuche beschleunigen

### 2.1 Ausgangspunkt und Messgrenzen

`0x18C160` ermittelt nächste Gegnerentfernungen und gegebenenfalls ein Angriffsziel mit nachfolgenden Bewegungsaktionen. Es ist kein Ersatz für die bereits optimierten Gruppen- oder Einzelwegsuchen. Reguläre Aufrufe sind maskengesteuert verteilt; `0x19A1F0` kann sofortige Prüfungen anfordern. Die vorhandene Cadence bleibt bei den folgenden Arithmetikkandidaten unverändert.

Die **historischen Profile vor dem Spectator-Fix** zeigen 32,260 % eigene Simulationsstichproben in `current-p1` und 24,960/24,644 % in `spectatoroff-p1/p2`. Das sind Anteile der gesamten Funktion, keine gemessenen Divisionsanteile und keine aktuellen Leistungszusagen. Das Ausschalten anderer Funktionen verändert außerdem den untersuchten Ablauf.

Quellen: [Native Ablauf-, Cadence- und Arithmetikverträge](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md#enemy-scan-cadence-and-integer-contracts-2026-11-13-vanilla-facts), [Profilaggregation](.work/a/stress90/001/profile-summary.json) und zugehörige Originalprotokolle unter `.work/a/stress90/001/results/`. Der Sampler ordnet native Blattadressen Funktionen zu; einzelne Instruktionskosten sind daraus nicht bestimmbar.

### 2.2 Zielscore: festen Divisor spezialisieren

**Offen:** Die variable signed Division bei `0x18C6DA` durch einen pro Aufruf feststehenden Wert vermeiden. Der ursprüngliche Listencount bestimmt den Divisor: 1–4 Einträge → 5, 5–14 → 2, ab 15 → 1. Division durch 1 ist die Identität; `/2` und `/5` lassen sich mit exakt derselben Ganzzahlrundung berechnen.

Der originale aktuelle Tabellenzugriff muss erhalten bleiben. Keine fest einprogrammierte Jittertabelle und keine ungeprüfte Annahme über negative Indizes: normale Erzeugung liefert nichtnegative Seeds, der vollständige Save-/Transfervertrag ist jedoch noch offen, und die Tabellenregion ist schreibbar. Signed Addition, Modulo, Rundung gegen null, Listencount und Reihenfolge dürfen sich nicht ändern.

**Nachweis vor Freigabe:** Gleichheit mit Originalquotienten auch an signed Grenzen; identisches Ziel, Distanz-/Auxminimum, Cooldown, Bewegungs- und Abbruchverhalten; Register-/Flag-/Stack-Verträge der tatsächlich gewählten nativen Umsetzung. Erst anschließend gegen zusätzliche Dispatch-/Branchkosten messen. Noch ist weder diese native Umsetzung noch ihr Nettogewinn nachgewiesen.

### 2.3 Distanz: nicht benötigte Divisionen erkennen

Die exakte Formel lautet für `M=max(|dx|,|dy|)>0`, `m=min(...)`:

    P = floor(2*m/5)*m
    D = M + floor(P/M)

Der Nullfall bleibt separat. Bei den eingelesenen signed-short Koordinaten ist `P` höchstens 1.717.934.490; `D>=M` ist damit auch unter der tatsächlichen Ganzzahlarithmetik gesichert. Falls `P<M`, ist der Quotient exakt null. Der konstante `/5`-Teil ist im Original bereits durch Multiplikation optimiert; die verbleibende Division durch `M` ist der Untersuchungskandidat.

**Offen:** Diese exakten Sonderfälle und untere Schranken nutzen, ohne ein benötigtes Ergebnis auszulassen. Eine vollständige Distanzberechnung darf nur entfallen, wenn sie weder das nächste Distanzminimum noch ein anwendbares Auxminimum verbessern kann und der Zielpfad sie nicht benötigt.

Zu erhalten sind insbesondere die inklusive Reichweite, der separate Heimursprungs-Zweig, alte Listeneinträge vor dem Global-ID-Test und die einmal eingelesene Actor-Position. Eine reine Reichweitenprüfung vom aktuellen Standort ist kein ausreichender Ersatz. Zusätzliche Vergleiche können den Gewinn aufzehren; Einsparquote und Nettokosten sind noch zu messen.

### 2.4 Räumlicher Index erst nach zusätzlichem Audit

**Weitergehende Untersuchung, keine abgesicherte Lösung:** Ein Index könnte Kandidatenarbeit reduzieren, benötigt aber einen vollständigen Zustands- und Aktualisierungsvertrag. Vorher müssen Bewegung, Spawn, Teleport, Slotwiederverwendung, Laden, Transfer sowie der Zustand zum jeweiligen Scanzeitpunkt innerhalb des Ticks geschlossen sein. Ein Snapshot vom Tickanfang reicht bei aufsteigenden Unitupdates nicht automatisch aus.

Originale Listenzugehörigkeit, Count und Gleichstandsreihenfolge müssen erhalten bleiben. Entfernungsminimum, Auxminimum und gewichtete Zielwahl sind getrennte Verbraucher. Vanillas Distanzapproximation besitzt keine allgemeine Dreiecksungleichung; Euclid-Radien und gewöhnliche metrische Baumabschneidungen sind deshalb kein Gleichheitsbeweis. Die detaillierten Gegenbeispiele stehen in der Baseline. Diesen größeren Umbau erst bewerten, wenn die kleineren Arithmetikmaßnahmen nicht ausreichen und die Indexpflege mitgemessen werden kann.

## 3. Verbleibende Such- und Aktualisierungskosten

### 3.1 Extreme Regeln und Budget-Rückfälle

Die alten `pf-cons-3`-/`pf-rule-*`-Messungen zeigten Restspitzen bei kartenweiten Sperren, wenigen Durchlässen und vielen gleichzeitigen Aufträgen. **Die Budgets selbst sind vorhanden**: Für Boden-Einzelwege gilt ein Budget von 500.000 Knoten je Tick. Assassinenwege werden angerechnet, bei Erschöpfung aber ausdrücklich nicht begrenzt oder an ihre teurere Vanilla-Kette übergeben; die Zahl ist deshalb keine harte Obergrenze aller Einzelwegsuchen. Umplanprüfungen besitzen ein eigenes deterministisches Budget. Bodenbefehle können danach wieder Vanillas Suche ausführen, die zusätzliche Kachelregeln nicht kennt.

**Offen:** Prüfen, ob ein genauerer Umweg-/Arbeitskostenschätzer oder eine andere deterministische Aufteilung teure Rückfälle reduziert. Zuerst heutige Restkosten getrennt nach Kern, Vanilla-Fallback und Umplanwächter erfassen. Keine Verschiebung von Gruppenbefehlen über Ticks, kein wall-clock Budget und keine Gleichsetzung eines Budgetabbruchs mit Unerreichbarkeit. Der bestehende Fallback-Vertrag darf nicht stillschweigend geändert werden.

Einstieg: [AnchorRouting](Pathfinding/src/Engine/AnchorRouting.cs), [RouteReplanWatch](Pathfinding/src/Engine/RouteReplanWatch.cs), [vorhandene Integrationsprüfungen](Pathfinding/tests/Engine/AnchorIntegrationTests.cs) und [native Suchverträge](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUND_ROUTE_BUILDERS.md). Historische Größenordnungen sind keine Messung des heutigen Quellstands.

### 3.2 Cursor-Topologie nach Regionsänderungen

Tabellenbasierter Aufbau, Graphspeicher-Wiederverwendung und Vorbau beim Kartenstart sind vorhanden. Bei geänderter `RegionGeneration` wird die Cursor-Topologie dennoch ersetzt und bei Bedarf vollständig neu aufgebaut.

**Offen:** Ursache, Häufigkeit und Kosten solcher späteren Neuaufbauten im aktuellen Stand erfassen. Erst bei relevanter Last prüfen, ob betroffene Bereiche exakt lokal aktualisiert oder vermeidbare Invalidierungen eingespart werden können. Vor einem Wiederverwenden müssen sämtliche relevanten Regions-/Kantenänderungen nachweislich erfasst sein; ein einfaches Beibehalten des alten Graphen ist keine Lösung.

Einstieg: [Moved.CursorConnectivity](Pathfinding/src/Engine/Moved/Moved.CursorConnectivity.cs), [Vorbau](Pathfinding/src/Engine/FastMovementScheduler.cs), [CursorTests](Pathfinding/tests/Engine/CursorTests.cs). Aufbauzeit, Laden und spätere Laufzeitspitzen getrennt messen.

### 3.3 Assassinen: nur noch unabhängige Restanfragen untersuchen

**Offener Messpunkt:** Welche teuren Einzelanfragen bleiben außerhalb der bereits vorhandenen nativen Rückwärts-Gruppenfelder und ihrer geprüften Wiederverwendung? Erst bei einem belegten relevanten Restanteil gemeinsame Start-/Zielkontexte oder weitere exakte Wiederverwendung untersuchen.

Die alte Prognose „70 statt 1450 ms“ gehört zu einem anderen Aufbau und wird nicht übernommen. Weder eine erneute native Portierung noch ein pauschales Überspringen von `D9C40` ist hier geplant. Für eine neue Abkürzung sind die tatsächlich benötigten Such-, Rekonstruktions- und Folgezustände nachzuweisen.

Einstieg: [AssassinPathfindingRuntime](Pathfinding/src/Assassins/AssassinPathfindingRuntime.cs), [nativer Kern](Pathfinding/native/PathfindingAssassin.cpp), [Veröffentlichungstests](Pathfinding/tests/AssassinPathfinding.Tests/RuntimePublicationTests.cs), [Gruppenfeldvertrag](Pathfinding/UpdateToNewDLL.md) und die Native-Baseline.

### 3.4 Häufungen großer Gruppenfelder

**Offener Messpunkt auch ohne extreme Zusatzregeln:** Mehrere teure Gruppenfelder können im selben Tick zusammenkommen. Der vorhandene Startfilter erkennt bereits innerhalb von höchstens 16 Knoten abgeschlossene Vorwärtskomponenten ohne Anker. Unerreichbare Starts in größeren Komponenten können ein Feld weiterhin bis zur Erschöpfung treiben. Die alte Roadmap beschrieb diesen Rest; dessen heutige Häufigkeit und Bedeutung sind noch zu ermitteln.

Vor weiteren Änderungen Feldaufbau, tatsächlich abgefragte Mitglieder, abgeschlossene Startkomponenten und Invalidierungen getrennt erfassen. Eine größere Vorprüfung nur bei nachgewiesenem Nettogewinn erwägen: Sie kostet selbst Sucharbeit und darf keinen erreichbaren Start ausschließen. Kein spekulatives KI-Batchen oder Verschieben der Befehle über Ticks wieder einführen.

Einstieg: [Gruppen-/Startfilter im nativen Kern](Pathfinding/native/PathfindingAnchor.cpp) und [AnchorRouting](Pathfinding/src/Engine/AnchorRouting.cs). Die vorhandenen Batch-, Einzelabfrage- und Determinismusgarantien bleiben maßgeblich.

## 4. Offene Freigaben und Vertragsentscheidungen

### 4.1 Feuer-/Pestvermeidung vollständig abnehmen

**Offen ist die Freigabe, nicht die erneute Implementierung.** Bewegungsaufnahme über isolierte Soldaten-Tribes bzw. wiederholte Gruppenbefehle, unterschiedliche Rückkehrarten, getrennte Fristen, Ankunftszähler und gespeicherte Bewegungsfälle sind vorhanden. `EngineOptions.Capture` hält `AvoidFire` und `AvoidPlague` jedoch ausdrücklich bis zur vollständigen Ingame-Abnahme auf `false`, auch bei alten gespeicherten Opt-ins.

Noch nachzuweisen ist die vollständige Kette unter kontrollierter Testaktivierung: natürliches Wiederanlaufen/Fliehen/Rückkehren mit unterschiedlichen Soldatentypen, Treppen und Höhen; keine Mitbewegung anderer Gruppenmitglieder; keine Übernahme neuerer Aufträge; korrekter Abbruch bei Tod, neuer Global-ID oder Befehlswechsel; Save/Load sowie echte Host-/Client-Gleichheit. Erteilten Rückkehrbefehl und tatsächliches Ankommen getrennt erfassen. Vorhandene automatisierte Teilprüfungen nicht mit Sicht- oder Mehrspielerabnahme gleichsetzen.

Aktuelle Fristen verwenden statt der alten festen 400-Tick-Empfehlung: zum Prüfstand Feuer-Warten standardmäßig 60, Standpositionsrückkehr 120 Spielsekunden, beide konfigurierbar; die Rückkehrfrist beginnt beim sicheren Ankommen. Pest besitzt eigene Einstellungen. Daraus folgt keine neue Standardwertentscheidung.

Quellen: [EngineOptions](Pathfinding/src/Engine/EngineOptions.cs), [MovementCaseSave](Pathfinding/src/Engine/MovementCaseSave.cs), [Engine-Integrationsprüfungen](Pathfinding/tests/Engine/AnchorIntegrationTests.cs), [native Bewegung und Feuer](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIRE_EFFECTS.md). Das isolierte Pechfeuer-Experiment ist kein allgemeines Maximum für Flächen- oder Gebäudebrände.

### 4.2 Ziel auf einer verbotenen Kachel

**Offene Gameplay-/API-Vertragsfrage, kein nachgewiesener Performancefehler.** Der heutige `Forbidden`-Vertrag verbietet eingehende Wege und erlaubt das Verlassen einer solchen Kachel; Vanillas eigene Befehle/Suchen bleiben unverändert. Eine allgemeine Sonderregel für ein angeklicktes verbotenes Ziel ist nicht definiert.

Vor einer Erweiterung entscheiden, ob der Auftrag abgewiesen, ein ausdrücklich definiertes Ersatz-Endziel angeboten oder eine andere Zielregel eingeführt werden soll. Kein automatisches Umdeuten des Ziels und kein stilles Ändern des öffentlichen Fallback-Vertrags. Anschließend Start-im-Verbot, Ziel-im-Verbot, mehrdeutige Ersatzziele und deterministische Mehrspielerentscheidungen abnehmen.

Quellen: [RouteTileRules](APIShared/src/Pathfinding/Routes/RouteTileRules.cs), [FastMoatRouting](Pathfinding/src/Engine/FastMoatRouting.cs) und vorhandene Tests zum Eingangsverbot/Verlassen in `AnchorIntegrationTests.cs`.

### 4.3 Vergleichslücken schließen

Die früheren Stressläufe ersetzen keine breite Abnahme. Weiter sinnvoll sind ein anderer Save mit Mauerringen/Belagerung, menschliche Massenbefehle und der Karteneditor sowie echte Host-/Client-Läufe für die jeweils veränderten Funktionen. Vorher vorhandene Nachweise auf ihren konkreten Umfang prüfen; einzelne bestandene Fälle nicht pauschal erneut als fehlend behandeln.

Für freundliche Grabenquerung und menschliche Assassinenwege fehlt weiterhin ein isolierter aktueller Vergleich gegen ihren ausgeschalteten Zustand. Die günstigen KI-Assassinen-Vergleiche gelten nur für deren gemessenes Szenario. Für weitere Performance-Einstufungen Funktionen einzeln mit identischem Quell-/SDK-Stand, Save, Zoom und wirksamen Einstellungen vergleichen. Render-FPS, Simulationstakte, Tickpausen, Sucharbeit und Allokationen getrennt bewerten. Die Wiederholungsläufe müssen auch Laden/erste Ticks und tatsächliche Bewegung abdecken, nicht nur die stabile Messphase.

Messmethodik und vorhandene Werkzeuge: [AGENTS/performance.md](AGENTS/performance.md), [Ingame-Testverfahren](AutomatisierungTest.md). Die am 13.11.2026 geprüften Release-Fixes und ihre Ergebnisse bleiben separat in [.work/a/perf-release/001/Ergebnis.txt](.work/a/perf-release/001/Ergebnis.txt); sie sind keine Umsetzung der hier aufgeführten Maßnahmen.

## 5. Abgrenzung zu anderen Unterlagen

Der größere Suchkern-/Architekturprototyp hat eine eigene Entwicklungsgeschichte und offene Abnahmen in [BetterPathfinding.md](BetterPathfinding.md). Er ist keine bereits freigegebene Lösung der oben genannten Restprobleme. Hier werden seine alten Entwurfsvarianten nicht nochmals als neue Aufgaben eingereiht.

Wiederverwendbare Vanilla-Verträge: [Ground-Builder](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUND_ROUTE_BUILDERS.md), [Gruppenaufnahme](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md), [Tor-Abfragepass](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_QUERY_PASS.md), [Feuer und Bewegung](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FIRE_EFFECTS.md), [Feindsuche](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md). Aktuelle Mod- und Hookverträge stehen in [Pathfinding/UpdateToNewDLL.md](Pathfinding/UpdateToNewDLL.md).
