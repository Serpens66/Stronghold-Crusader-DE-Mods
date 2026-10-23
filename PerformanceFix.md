# PerformanceFix: Prüfung der leistungsintensiven BugfixesAndQoL-Funktionen

## Auftrag, Stand und Ergebnis

Übergabe für einen weiteren Chat, der den teuren Code gezielt auf Optimierungsmöglichkeiten untersuchen soll. Untersucht wurden die 13 zuvor in [ModsettingsRework.md](ModsettingsRework.md) unter „Performance Heavy“ zusammengefassten Optionen: Aktivierung, tatsächlicher Aufrufweg, zusätzliche Sucharbeit, Wiederholungen, Grenzen und Wiederverwendung. Es wurden keine Runtimequellen geändert, keine neuen Spielmessungen gestartet und keine Builds installiert.

**Stand: 23.10.2026, Quellsichtung bis etwa 15:35 Uhr MESZ.** Modpack-Basis-HEAD: `a56976a133391354f38edb0c75cf55bc22dac899`. Es bestehen uncommittete Änderungen, unter anderem an `AssassinPathfindingRuntime.cs`; diese wurden mitgelesen und müssen erhalten bleiben. Der Commit allein beschreibt den geprüften Code nicht. Insbesondere Assassin-Optimierungen und DEVDIAG-Erweiterungen werden parallel weiterentwickelt. Vor Umsetzung die hier genannten Methoden erneut lesen und die Fingerprints am Ende vergleichen.

### Einstufung der 13 Optionen

„Bestätigt“ bedeutet hier: Der aktuelle Quellpfad kann tatsächlich zusätzliche große Suchen, vollständige native Planerproben oder viele zusätzliche Suchaufträge auslösen. Es bedeutet nicht, dass dieser Aufwand ständig anfällt oder im aktuellen Release bereits mit einer bestimmten Millisekundenzahl gemessen wurde.

| Option | Ergebnis der Codeprüfung | Empfehlung für Modsettings |
| --- | --- | --- |
| `EnableImprovedAssassinPathfinding` | **Großes Suchpotenzial bestätigt:** native Suche plus zusätzliche gewichtete A*- bzw. Existenzsuche; bei Fehlschlägen kann eine große erreichbare Fläche untersucht werden. | Performance Heavy |
| `EnableEnemyGatePathfindingFix` | **Zusatzsuchen bestätigt:** Cursorvalidierung kann bis zu zwei zusätzliche native Kachelsuchen ausführen; dazu Filter in häufigen Suchpfaden und Snapshotpflege. | Performance Heavy |
| `EnableMoveFormationEnhancements` | **Großes Suchpotenzial bestätigt:** BFS für Formationsziele und Suche nach Ersatzpositionen; Aufwand wächst mit Fläche und Auswahl. | Performance Heavy |
| `EnableImprovedManualUnitCommands` | **Teure Proben bestätigt:** nativen Planer probeweise ausführen und rund 5 MiB Arbeitsdaten zurückkopieren; bei vielen negativen Mitgliedsproben entsprechend vielfach. | Performance Heavy |
| `EnableImprovedMoatFilling` | **Zusätzliche Optimierungssuche bestätigt:** unter passenden Grabenarbeitsbedingungen eine gewichtete Suche für den Bodenpräfix; möglicher A*-Verfeinerungslauf. | Performance Heavy |
| `EnableAiRaidRetargetFix` | **Zusätzliche native Suchaufträge bestätigt:** Ersatz-Gebäudeangriffe lösen neue Annäherungs-/Wegberechnungen aus; bis vier Aufträge je wartender Raidgruppe und Tick. | Performance Heavy |
| `RequireReachableEnemyForAutomaticGateClosing` | **Kumulatives Potenzial bestätigt:** wiederholte Portal-Erreichbarkeitsprüfungen und Ereignis-/Geometriearbeit; vorhandene Caches und Distanzfilter reduzieren dies stark. Keine eigene große Kachel-BFS. | Performance Heavy, Hinweis „viele Tore/Abfragen“ |
| `EnableLadderAttackPathfindingFix` | **Zusatzsuche bestätigt, Größe begrenzt:** bei negativer Regionsprüfung ein weiterer nativer Portalgraphlauf mit Gruppenmodus; anschließend begrenzte Kandidatenarbeit. | Performance Heavy, Hinweis „viele Angriffsprüfungen“ |
| `InaccessibleAIBuildingDemolitionProtection` | Begrenzter Portalgraph, Tick-/Spielercache und Gebäude-Identitätscache; keine Kachel-Suche und keine Schleife über sämtliche Einheiten. | Zurück zu KI/Wirtschaftsschutz |
| `EnableAssassinGatehouseClimbFix` | Endpunkt-/Übergangsregeln und lokaler Abstiegscheck; keine eigenständige zusätzliche A*-Suche. Veränderte Erreichbarkeit kann indirekt native Sucharbeit ändern. | Zurück zu Tore/Belagerungsregeln |
| `EnableMountedStockpileMovementFix` | Ziel-/Typkorrektur; bei geeignetem Stockpile-Cursorziel zusätzliche Auswahlprüfung O(Auswahlgröße), aber keine eigene Wegsuche. | Zurück zu Truppenbedienung |
| `EnableAssassinCombatResumeFix` | Korrigiert einen Kontextwert vor dem bereits vorhandenen nativen Wiederaufnahme-Aufruf; startet keine zusätzliche zweite Suche im Callback. | Zurück zu Truppenbedienung |
| `EnableAssemblyPointPlacementFix` | Überspringt native Ablehnungs-/Statusinstruktionen; kein neuer Suchaufruf im Fix. Spätere reguläre Rekrutenwege bleiben native Arbeit. | Zurück zu Truppenbedienung |

Damit sollte „Performance Heavy“ **acht** dieser Optionen enthalten: sechs mit potenziell großer zusätzlicher Such-/Prüfarbeit und zwei mit vor allem kumulativem Aufwand. Die anderen fünf sind nicht kostenlos, erfüllen aber anhand dieser Prüfung nicht die beabsichtigte Bedeutung „kann zusätzliche große Berechnungen auslösen“.

## Native Grundlage und Aussagegrenzen

Die installierte kanonische `CrusaderDE.dll` wurde per SHA-256 mit `CURRENT.json`, `DATABASE_INFO.json.identities.currentNativeHash` und dem `current-native`-Binäreintrag verglichen. Alle identifizieren:

`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`

Maßgebliche Unterlagen: [Baseline-Einstieg](_inspect/CrusaderDE-Native-Baseline/CURRENT.md), [native Gruppenwegsuche](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md), [Tor-Abfragepass](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GATEHOUSE_QUERY_PASS.md), [Raid-Kette](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RAID_RETARGET.md), [direktes Assassin-Klettern](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md), [Rekonstruktionsreparatur](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md) und [Formations-/Befehlsfreigabe](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FORMATION_MOVEMENT_PERMISSION.md).

Die vorhandenen hashgebundenen vollständigen Auditketten wurden mit den heutigen Mod-Einstiegen abgeglichen und die entscheidenden nativen Such-/Planerfunktionen zusätzlich über `query.ps1 function` gelesen. Das ist keine neue Laufzeitabnahme aller möglichen Spielzustände. Automatisch vergebene Funktionsnamen mit Vertrauensstufe `candidate` werden nicht zu bestätigten Namen erklärt; die verwendeten Kontrollfluss-/Suchverträge sind in den verlinkten Audits und produktiven Native-Verträgen belegt.

- **Befehle und Proben:** Gruppenauftrag `0x11B520` → freigegebene Mitglieder → `0x196280` → Pfadplanung `0xF4930`; Probe `0x18E1E0(unitManager, unitId, x, y)` prüft Koordinaten, Ziel und Verbindungen, kann früh ablehnen oder in `0xF4930` eintreten. Positive Wegprobe ist keine zusätzliche Befehlsberechtigung.
- **Kachelsuchen:** `0xF4930` kann nach lokalen/alternativen Versuchen `0xDA590`, `0xD9C40` oder weitere Profile mit hohen Suchbudgets verwenden; `0xDB650` ist ebenfalls eine echte Kachelsuche. Die Aufrufer verwenden bis zu 400000 als Arbeitsbudget, während native Kachelgitter 320800 Einträge haben. Ein solches Budget ist keine kurze Zeitbegrenzung.
- **Portalgraph:** `0xE2610` prüft Quell-/Ziel-PCL und Spieler-/Modusfreigaben und durchläuft die Portalverbindungen, einschließlich eines weiteren zulässigen Suchzweigs. Das ist keine Flut über alle Kartentiles. Der Mod prüft beim eigenen Topologieimport höchstens 200 Portalrecords einschließlich reserviertem Index; praktisch also höchstens 199 geprüfte Nutzrecords. Native interne Mehrfachscans können dennoch relevant sein, wenn Aufrufe häufig wiederholt werden.
- **Gebäudeangriffe:** `0x11E960` → `0xDA020` erzeugt Annäherungskandidaten → `0x123090` prüft/rankt sie und kann weitere Suchbuilder aufrufen. Erst der gültige Kandidat und der nachfolgende native Mitgliederpfad führen zu tatsächlichen Einzelbewegungen. Übersprungene Suche, unklare Scratch-Frische und Rückgabe 1 sind keine positiven Suchbelege.
- **Rekonstruktion:** `0xE1640` kann auf einem Fehlerpfad eine vollständige Reparatur anstoßen. Der dokumentierte historische Einbahnverbindungsfehler mit ungefähr 19 ms je Fehlschlag ist bereits durch die APIShared-Rekonstruktionsregel adressiert. Diesen alten Befund nicht als aktuellen Daueraufwand jeder Kletteroption ausgeben.

Die native Baseline belegt zudem bereits lange Gruppenbewegungs-Ticks ohne Modhook. Bei jeder Optimierung deshalb trennen: zusätzliche Modarbeit, bereits vorhandene native Suche und durch korrekt geändertes Gameplay häufiger oder anders ausgeführte native Arbeit.

## Konkrete teure Pfade und Optimierungsansätze

Die folgenden Ansätze sind Untersuchungsaufträge, keine bereits als korrekt oder schneller nachgewiesenen Änderungen. Vor einem Eingriff zuerst bestehende Wiederverwendung und aktuelle parallele Optimierungen prüfen.

### 1. Manuelle Einheitenbefehle: native Proben und Speicherverkehr

**Quellen:** [ManualUnitCommands.cs](BugfixesAndQoL/src/UnitCommands/Runtime/ManualUnitCommands.cs), insbesondere `ProbeNativeManualPath`, `ProbeNativeCursorPath`, `TryQualifyNativeSelection`, `PrepareNativeManualGroup`; ergänzend `CursorConnectivity.cs` und `UnitCommandPathRuntime.RequiredRoutes.cs` im bestehenden Befehlsmodul.

- `ProbeNativeManualPath` sichert den vollständigen Einheitenrecord und die auditierte Scratch-Closure, setzt den Probekontext und ruft `originalCentralMovementPlan` auf. `finally` stellt alle betroffenen Daten und Scopes wieder her.
- Hauptpuffer: `NativeProbeManagerBytes = 0x3C886C` und `NativeProbeGridBytes = 0x139480`, zusammen 5250284 Bytes, rund **5,01 MiB**, zuzüglich kleiner weiterer Puffer. Pro Probe werden die großen Puffer zurückgeschrieben; ohne wiederverwendbaren Batchsnapshot werden sie zuvor auch gelesen.
- Ein Batch spart wiederholtes Sichern, **nicht** die Wiederherstellung nach jeder Probe. Hundert tatsächliche Proben bedeuten allein für die großen Rückkopien ungefähr **501 MiB** Datenverkehr. Das ist eine aus den Puffergrößen abgeleitete Menge, keine gemessene Laufzeit.
- Die Cursorprüfung hört beim ersten erreichbaren Mitglied auf. Der Worst Case sind viele negative Prüfungen, nicht jede normale Auswahl. Der Cursorcache gilt für Tick, Einheit samt Record-Fingerprint, Ziel, Revision und Diplomatie und wird bei Tickwechsel bzw. 4096 Einträgen geleert. Eine Mausbewegung auf ein neues Ziel erzeugt andere Schlüssel.
- Der Gruppenfall ist zusätzlich auf die passenden menschlichen Moat-Start-/Fallbackbedingungen beschränkt. Auch dort gibt es frühe Abbrüche. Keine Behauptung „pro Tick für jede Einheit“ daraus ableiten.

**Zu untersuchen:** Pro Eingabe genau zählen, wie viele Cursor-, Gruppen- und Einzelplanproben aufgerufen werden und wie oft der Cache greift. Danach prüfen, ob identische Proben innerhalb des bewiesenen unveränderten Kontextes wiederverwendet werden können. Eine kleinere Scratch-Sicherung braucht einen vollständigen nativen Schreib-/Lesenachweis einschließlich Fehlerpfaden; Puffer nicht aus einem erfolgreichen Beispiel heraus verkleinern. Für häufig sofort ablehnende Ziele eine vorangestellte billige, semantisch vollständige Ablehnung prüfen. Nicht alle ausgewählten Einheiten vorprüfen, wenn für den konkreten Cursor nur ein gültiger Zeuge gebraucht wird; diesen frühen Abbruch gibt es bereits.

### 2. Gewichtete Assassin-Wege: zusätzliche Suche nach Vanilla

**Quelle:** [AssassinPathfindingRuntime.cs](BugfixesAndQoL/src/AssassinPathfindingRuntime.cs), insbesondere `BuildWeightedPathCore`, `TryBuildWeightedRoute`, `UsesRouteExistenceOnly`, `FindAnyWeightedRoute`, `TryLoadCachedRoute` und `GetRequestIndex`; cachegebundene Identitäten stehen auch in `AssassinPathfindingRuntime.CacheKeys.cs`.

- Zuerst läuft der native Builder. Erst danach folgen bei aktivem Feature, gültigem Ziel, neuem Request und geklärter Spieler-/Gate-Identität eigene Suchpfade. Fortsetzungen und Flood-Field-Anfragen mit negativem Ziel fallen früh auf das native Ergebnis zurück.
- Der normale Zusatzpfad ist gewichtetes A* mit acht Nachbarn. Der Expansionsdeckel ist `min(maximumNodes, 320800)`, mindestens 1. **2000 ist das Limit des veröffentlichten Weges, nicht das Limit der untersuchten Knoten.** Ein unerreichbares Ziel kann die erreichbare Fläche bis zum Suchlimit verbrauchen.
- Gruppen-/Feldanfragen ohne nutzbaren Einzel-Route-Publisher verwenden unter den geprüften Bedingungen bereits eine greedy Existenzsuche. Sie bricht bei einem ausreichend kurzen gefundenen Weg ab. Auf Fehlschlag läuft sie bis zur erschöpften Frontier; bei `Undecided` folgt noch das exakte A*. Das beschleunigt gewöhnliche positive Antworten, begrenzt aber den negativen Worst Case nicht auf wenige Knoten.
- Commandbezogene Routencaches, Suffixinformationen und Gültigkeitsprüfungen sind vorhanden; höchstens 64 genaue Routen werden im Commandcache gehalten. Unterschiedliche Startpunkte, Ziele, Profile oder Policies können dennoch unterschiedliche Suchen erfordern. Caches nicht als „einmal pro Gruppe“ ausgeben.
- Ohne direkten Einzelrequest muss die Spieler-/Geschwindigkeitszuordnung gegebenenfalls einen Einheitenindex aufbauen. Im Command wird er wiederverwendet; außerhalb eines Commands kann erneut über den Einheitenspan gegangen werden.
- Diagnosebeobachter erhalten absichtlich die ausführlichere Suchreihenfolge. Aktuelle zusätzliche Reverse-/Erklärsuchen in DEVDIAG dürfen nicht als gewöhnliche Releasekosten gezählt werden.

**Zu untersuchen:** Requests nach Caller, positiv/negativ, nativer/gewichteter/Existenzsuche, Ziel und Command gruppieren. Besonders viele unterschiedliche negative Ziele, automatische Neubefehle und Fallback von Existenz auf A* prüfen. Ein gemeinsam nutzbares Negativ-/Erreichbarkeitsfeld ist ein Ansatz, aber nur mit identischen gerichteten Kanten, Spielerrechten, Profilen, Start-/Zielregeln und sauberer Topologieinvalidierung. Keine positive Route in einen globalen Cache legen, der Belegung oder Eroberung überlebt. Die bereits vorhandene Existenzoptimierung nicht erneut als neue Idee implementieren.

### 3. Enemy Gates: zusätzliche Cursor-BFS und häufige Kantenprüfungen

**Quellen:** [SamePclGateRouteRuntime.cs](BugfixesAndQoL/src/GateRouting/SamePclGateRouteRuntime.cs), `TryValidateCursorPreview`, `ProcessTick`; [CursorPreviewCache.cs](BugfixesAndQoL/src/GateRouting/CursorPreviewCache.cs); `GateTopologySnapshotProvider.cs`, `GateRoutingFeature.cs` und die zugehörigen nativen Adapter.

- Auf einem zulässigen Cachemiss führt `TryValidateCursorPreview` eine native `0xDB650`-Referenzsuche aus. Ist diese positiv, folgt dieselbe Suche unter der Gatepolicy. **Bis zu zwei zusätzliche echte Kachelsuchen**, nicht bloß zwei Bitprüfungen.
- Das übergebene Budget `DirectCursorSearchNodeLimit = 0x61A80` entspricht **400000**. Es beschränkt nicht auf den Nahbereich des Mauszeigers. Ein abgeschnittener Umweg oder eine große negative Suchfläche kann teuer sein.
- Der exakte Cursorcache besitzt 32 Einträge und etwa 0,2 s Lebensdauer. Spieler, Einheit/Global-ID, Positionen, Regionen, Epoch, Policygeneration und Fingerprint gehören zum Schlüssel. Neue Zieltiles oder geänderte Identitäten/Topologie können fortlaufend Misses erzeugen. Der Wiedereintrittsschutz verhindert Rekursion, ist aber kein Arbeitsbudget.
- In den eigentlichen Suchschleifen wirken native Masken und zusätzliche Filter. Ein einzelner Test ist klein; sehr viele Suchkanten und native/verwaltete Übergänge können sich trotzdem summieren.
- Snapshot-/Topologiepflege ist separat zu messen: Ereignisse erzwingen Aktualisierungen, sonst gibt es periodische Prüfungen und Wiederverwendung. `ProcessTick` kontrolliert zudem den Tribe-Eigentümersnapshot; Übereinstimmung vermeidet Neuanlage, aber nicht jede Spanprüfung. Das sind keine eigenen Wegsuchen pro Tribe.

**Zu untersuchen:** Referenz- und gefilterte Suche getrennt zählen/messen; Cursorcache-Misses nach Zielwechsel, Ablauf und Policyänderung aufteilen. Prüfen, wann ein bereits vorhandener positiver/negativer nativer Nachweis unter derselben unveränderten Suchsemantik ausreicht. Die Referenzsuche nicht ungeprüft streichen: Ein negatives gefiltertes Ergebnis allein beweist keine Gatepolicy-Ursache. Häufige unveränderte Prüfungen nativ bzw. über publizierte Masken abfangen, ohne neue überlappende Hooks zu installieren. Topologiebau, Tribe-Scan und Kantenkosten nicht mit den beiden Cursor-BFS vermischen.

### 4. Formationen: erreichbare Plätze und Ersatzzuordnung

**Quellen:** [FormationRuntime.Destinations.cs](BugfixesAndQoL/src/Formations/FormationRuntime.Destinations.cs), `BuildManagedDestinations`, `CaptureReachableCandidates`, `CaptureVanillaDestinations`; [FormationModel.cs](BugfixesAndQoL/src/Formations/FormationModel.cs), `FormationPlacementModel.SelectCandidates`; `FormationRuntime.Preview.cs`, `PublishPreview`.

- Für eigene Aufstellungen werden `min(8192, max(256, 16 × Einheitenzahl))` Kandidaten angefordert. Das ist **kein allgemeines Expansionslimit** der BFS.
- Die BFS läuft weiter, solange Kandidaten fehlen **oder** gewünschte/benachbarte Slots aus `pending` nicht erreicht wurden. Auf großen, schmalen oder gerichteten Komponenten kann deshalb viel mehr Fläche als die angeforderte Kandidatenzahl besucht werden. Zusätzliche gewünschte Slots können auch über die angeforderte Ergebniszahl hinaus aufgenommen werden.
- Exakte passende Slots werden per Dictionary zuerst vergeben. Nur für übrige Slots folgt eine Schleife über die Kandidaten: im ungünstigen Fall ungefähr `unbesetzte Slots × Kandidaten`. Bei Hunderten Einheiten ist dies relevant; es passiert nicht für jede Einheit noch einmal eine unabhängige volle BFS.
- Die Vorschau merkt ihren Schlüssel und berechnet unveränderte Ziel-/Formations-/Auswahlparameter nicht erneut. Drag, Rad, Drehung oder Zielbewegung können den Schlüssel ändern und eine neue Berechnung auslösen. Native-Standardaufstellung hat einen eigenen Suchzweig; diesen getrennt betrachten.

**Zu untersuchen:** Besucht-Zähler, `pending`-Rest, Ergebniszahl, Zahl unbesetzter Slots und Anzahl vollständiger Vorschau-Neuberechnungen protokollieren. Flächen-BFS und Slotzuordnung getrennt messen. Räumlichen Index oder gleichwertige stufenweise Kandidatensuche für nicht exakt zuweisbare Slots prüfen. Frühabbruch einer nicht erreichbaren Probe braucht einen Beweis anhand gerichteter Kanten; gleiche PCL allein reicht dafür nicht. Eine Änderung der Kandidatenreihenfolge oder der Tie-Break-Regel kann die Aufstellung verändern und ist keine bloße Performancekorrektur.

### 5. Verbessertes Grabenfüllen: Suche für den Bodenpräfix

**Quellen:** [FillWeightedRoutes.cs](BugfixesAndQoL/src/UnitCommands/Movement/FillWeightedRoutes.cs), `TryImproveFillPrefix`; [WeightedMoatPublication.cs](BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatPublication.cs), `TryCaptureBuilderWeightedScope` und Publikationspfad; [WeightedGridSearchKernel.cs](BugfixesAndQoL/src/UnitCommands/Movement/WeightedGridSearchKernel.cs), `Search`, `Run`, `ReverseField.Settle`; ergänzend `UnitCommandPathRuntime.WorkTargetSelection.cs` und `UnitCommandPathRuntime.WorkTargetFillApproach.cs`.

- Die Wahl eines freien Arbeitsrandfeldes prüft zunächst nur eine kleine Nachbarschaft. Daraus allein folgt keine volle Kartensuche pro Kandidat. Bei gleicher passender PCL gibt es einen billigen Zweig.
- Der leistungsrelevante Zusatz liegt insbesondere im Pfad-Publikationspfad: Nach dem nativen Weg kann `TryImproveFillPrefix` für den Bodenabschnitt vor dem abschließenden Grabenkontakt eine gewichtete Verbesserung suchen. **Dieser Pfad existiert auch ohne MoatMove**, dann mit `MoatTraversalPolicy.GroundOnly`. Zusätzliche freundliche Grabenquerungen gehören dagegen zum separaten Traversalanbieter.
- Der Kern besitzt Reverse-Field- und Kantenwiederverwendung, bis zu acht Felder je Kernel. Bei geändertem Suchkontext/Spieler/Tick werden die entsprechenden Generationen invalidiert. Gleiche Ziele/Profile können dadurch günstig sein; neue Arbeits-/Befehlsscopes können Wiederverwendung begrenzen.
- `maximumExpanded` steht an diesem Aufruf standardmäßig auf `int.MaxValue`. Die zulässige Weglänge von 2000 ist **kein enger Deckel für besuchte Knoten oder Laufzeit**. Der Kern kann nach einer skalaren A*-Lösung eine zusätzliche ressourcenbeschränkte Verfeinerung ausführen, wenn die erste Lösung die Weg-/Kostenbedingungen nicht erfüllt. Mehrere nicht dominierte Labels an einem Knoten sind möglich.
- Viele Arbeiter und automatische Folgeziele können Suchen vervielfachen. Es gibt kein belegtes generelles Urteil „jede Grabenaktion führt zu einer maximalen Suche“.

**Zu untersuchen:** Arbeitszielwahl, native Wegbildung, erste gewichtete Suche, Reverse-Field-Erweiterung und Verfeinerung getrennt messen. Abweisen anhand bereits berechenbarer Kostenuntergrenzen ausbauen, sofern Ergebnisgleichheit bewiesen wird. Bereits fertige Präfixe bzw. Felder innerhalb identischer unveränderter Scopes wiederverwenden. Arbeit nicht über einen Scope/Tick mit geänderter Belegung hinaus teilen. Ein kleinerer Knotendeckel oder Auslassen einer Verbesserung ändert potenziell die Funktion und muss als bewusster Trade-off behandelt werden.

### 6. KI-Raid-Retarget: begrenzte, aber vervielfachte native Aufträge

**Quelle:** [AiRaidRetargetFixRuntime.cs](BugfixesAndQoL/src/AiRaidRetargetFixRuntime.cs), `ProcessRaidRetries`, `TryReadOrderedRaidCandidates`, `TryIsMeleeRaidGroup`, `OnTick`; dazu `RaidAttackFieldEvaluation.cs` und der bestehende Suchabschlussbeobachter.

- Nur frisch bestätigtes `NoAttackPoint` einer passenden normalen Raid-Gebäudeattacke löst den Retry aus. `Unknown`, veraltete Identität oder unsicherer Suchabschluss stoppen bzw. erhalten das native Ergebnis.
- Jeder Ersatzversuch ruft `AttackBuildingEx` auf. Damit kann erneut die native Gebäude-Annäherungssuche und anschließend die Mitgliederbewegung laufen. Der Beobachter selbst ist nicht die teure komplette Suche; die neuen Aufträge sind der Multiplikator.
- Höchstens 100 Gebäudekandidaten pro Liste und vier ausgegebene Ersatzaufträge **je Retrygruppe und Tick**. `issued` wird innerhalb der `foreach`-Schleife zurückgesetzt: **kein globales Vier-Aufträge-Budget**. Es gibt bis zu acht Spieler mit sechs Raidrollen; das theoretische Rollenmaximum ist deshalb 48 wartende Gruppen, nicht eine einzige.
- Negative Ziele werden 300 Ticks je Gruppe gemerkt; unvollständige Retrylisten werden über Ticks fortgesetzt. Ein gültiges Ersatzziel bzw. ein unklarer Ausgang beendet die jeweilige Suche früh.

**Zu untersuchen:** Zahl wartender Gruppen, tatsächlich ausgegebene Aufträge, Annäherungskandidaten, angehängte Einzelwege und Gesamtkosten pro Tick messen. Einzelnen Ausreißer bis zum verursachenden Auftrag verfolgen. Identische negative Geometrieprüfungen nur mit richtiger Gebäude-/Spieler-/Topologieidentität teilen. Ein globales Budget würde Reihenfolge und Timing ändern; nicht als rein interne Optimierung stillschweigend einführen. Weniger wiederholte aussichtslose Aufträge können schneller sein, aber die bestehende Kandidatenpriorität und Frischegarantie müssen erhalten bleiben.

### 7. Erreichbare Feinde beim Torschließen: viele kleine bis mittlere Prüfungen

**Quelle:** [ReachableEnemyGatehouseRuntime.cs](BugfixesAndQoL/src/ReachableEnemyGatehouseRuntime.cs), `OnGatehouseQuery`, `TryIsUnitReachableToGate`, `FindRoute`, `TryGetPassGatehouse`, `TryGetPassDrawbridges`.

- Ereignis je geprüftem Kandidaten. Inaktive Funktion, ungültige/tote/nicht schließrelevante Einheit und Entfernung außerhalb der nativen Schließreichweite brechen vor der Erreichbarkeitsarbeit ab.
- `FindRoute` führt bei unterschiedlichen Komponenten die native Portalgraphsuche `0xE2610` aus: zunächst Eingang/Ausgang, gegebenenfalls danach relevante Zugbrückenannäherungen. Es ist keine eigene Kachel-BFS.
- Direkte und vollständige Ergebnisse werden je Tick nach Spieler, Quell-PCL, Toreingängen, Modus und gegebenenfalls Brückensignatur geteilt. Viele Einheiten in derselben Region erzeugen deshalb nicht automatisch je zwei neue Suchläufe.
- Passbezogene Wiederverwendung von Tor-/Brückeninformationen und das Vorziehen positiver direkter Erreichbarkeit sind bereits vorhanden. Der historische Befund über teure wiederholte Footprintarbeit darf nicht so behandelt werden, als fehlten diese Optimierungen noch.
- Viele Tore, verschiedene Quellregionen und hohe Simulationsgeschwindigkeit erhöhen die Abfragezahl. Auch Cachehits und native/verwaltete Übergänge kosten etwas. Die maximale Kartenseitenlänge ist hier nicht der direkte Suchraumparameter.

**Zu untersuchen:** Ereigniszahl von tatsächlich ausgeführten `FindRoute`-Aufrufen trennen; RangeSkips, DirectHits/Misses, FullHits/Misses und PassHits auswerten. Native Graphkosten und Footprintarbeit separat messen. Andere Abonnenten dürfen die während eines Passes wiederverwendeten Daten nicht unbemerkt verändern. „Einmal pro Tick“ nicht global über unterschiedliche Tore/Spieler/Quellregionen ausdehnen.

### 8. Leiterangriffe: Wiederholung der Portalgraphsuche

**Quelle:** [UnitCommandPathRuntime.LadderAttackFix.cs](BugfixesAndQoL/src/UnitCommands/Attack/UnitCommandPathRuntime.LadderAttackFix.cs), `TryHandleVanillaLadderRegionPair`, `RestoreVanillaLadderBuildingCandidates`; ergänzend `UnitCommandPathRuntime.BuildingCandidates.cs` und `UnitCommandPathRuntime.ApproachFlood.cs`.

- Nur in einem passenden aktiven Angriffsscope und nach negativer nativer Regionsprüfung wird `originalRegionPairReachability` mit dem Gruppenbewegungsmodus nochmals aufgerufen. Das ist ein echter zusätzlicher `0xE2610`-Suchlauf, aber auf dem begrenzten Portalgraphen.
- Im untersuchten Retrypfad gibt es keine eigene Ergebnis-Memoisierung. Ein großer Angriffskandidatenlauf kann dieselben Regionspaare wiederholt anfragen. Positive Nachweise werden für die Kandidatenwiederherstellung gesammelt.
- Der Kandidatenpuffer ist auf die vorhandene Kapazität von 500 Records begrenzt; der Wiederherstellungspfad kombiniert Kandidaten mit den positiven Übergangsnachweisen und reserviert Platz für den Sentinel. Das ist zusätzliche Arbeit, keine vollständige neue Kachelsuche pro Eintrag.
- Endgültige individuelle Wegbildung nach einer nun zulässigen Attacke ist vom Wiederholungsaufwand der Modprüfung zu trennen.

**Zu untersuchen:** `ModeRetryCount`, tatsächliche unterschiedliche Schlüssel `(player, sourcePCL, targetPCL, groupMode)` und native Laufzeit innerhalb eines Angriffsscopes zählen. Ein scopesicherer Cache wäre ein Kandidat. **Nicht blind nur den Rückgabewert cachen:** Der vorhandene Vertrag erhält auch native Graph-/Scratch-Seiteneffekte des Trampolins. Vor dem Auslassen eines Wiederholungsaufrufs deren Relevanz nachweisen oder korrekt reproduzieren. Native Ergebnis- und Frischegarantien sowie Sentinel-Vertrag beibehalten.

## Die fünf nicht als Heavy bestätigten Korrekturen

### KI-Abrissschutz

[AIBuildingTemporaryAccessClassifier.cs](BugfixesAndQoL/src/AIBuildingTemporaryAccessClassifier.cs) importiert höchstens 199 Nutz-Portalrecords, cacht die Topologie je Spieler/Tick und das Ergebnis nach Gebäude-/Global-ID, Regionen und Topologiesignatur. [TemporaryGateBlockagePolicy.cs](BugfixesAndQoL/src/TemporaryGateBlockagePolicy.cs) baut daraus einen kleinen Adjazenzgraphen und führt BFS über dessen Regionen/Kanten aus. Die Prüfung wird in `AIEconomyProtectionHook.PreventInaccessibleBuildingDemolition` nur für passende lebende KI-Gebäude, den verbesserten Modus und das entsprechende negative native Ergebnis benötigt. „Immer verhindern“ und „Vanilla“ sind keine Varianten derselben teuren Suche.

**Ergebnis:** Aufwand abhängig von betroffenen Gebäuden und Portalgraph, nicht direkt von allen Kartentiles oder hunderten Einheiten. Kleine Allokationen, Sortierung und Diagnoseerzeugung sind ein möglicher Nebenansatz; eine Heavy-Einstufung wie bei A* ist nicht gerechtfertigt.

### Direktes Assassin-Torhausklettern

[AssassinPathAPI.cs](APIShared/src/Pathfinding/Assassin/AssassinPathAPI.cs), `IsDirectGatehouseClimbEndpoint` und die nativen Endpunktadapter, sowie [AssassinGateDescentRuntime.cs](BugfixesAndQoL/src/Assassins/AssassinGateDescentRuntime.cs), `Allows`, prüfen Tile-/Gebäude-/Einheitenidentität und lokale Übergangsdaten. Keine eigene zusätzliche A*- oder Vollkartenschleife. Es werden Kanten der ohnehin ausgeführten Suche zugelassen; dadurch kann sich ihr besuchter Suchraum indirekt ändern.

**Ergebnis:** Keine schwere zweite Suche im Feature selbst. Eine sich durch die Regel ändernde Suchfläche und mehr tatsächlich erteilte KI-Befehle sind mögliche indirekte Effekte. Der alte Rekonstruktionsreparaturfehler ist gesondert dokumentiert und adressiert, nicht mit dieser Einstufung als noch aktiv behauptet.

### Berittene Einheiten auf Stockpiles

[MountedStockpileMovementPatch.cs](BugfixesAndQoL/src/MountedStockpileMovementPatch.cs), `CorrectMountedClassification`, `CaptureMountedSelection`, `PermitMountedGoodsyardEndpoint`: frühe Ablehnung für ungeeignete Ziele, dann bei geeignetem Stockpileziel **eine Schleife über die aktuelle Auswahl** zur Typprüfung. Der Bewegungs-Endpunktcallback liest lokale Werte und ändert nur den geprüften Ziel-Flagzugriff. Kein Aufruf eines zusätzlichen Wegbuilders.

**Ergebnis:** Nicht fälschlich als durchgehend O(1) beschreiben. Wiederholtes Cursorhover mit großer Auswahl hat O(N)-Prüfaufwand und ist als kleinerer Profilingkandidat denkbar; große Karte allein vergrößert diese Schleife nicht. Ein Cache müsste Auswahl, Identitäten, Lebenszustände und Spielerkontext korrekt berücksichtigen.

### Assassin-Bewegung nach Kampf

[AssassinCombatResumeRuntime.cs](BugfixesAndQoL/src/AssassinCombatResumeRuntime.cs), `PreparePostCombatPathContext`: prüft den erwarteten Aufrufer und die einzelne lebende Einheit im passenden Zustand und setzt gegebenenfalls das Assassin-Kontextflag. Die native Kette `0x1853F0` → `0x1976C0` → `0x196280` läuft ohnehin; der Callback ruft nicht zusätzlich den Planer auf und scannt keine ganze Einheitenauswahl.

**Ergebnis:** Kleiner Korrekturcallback. Der danach gewählte Assassin-Suchpfad kann anders kosten als der zuvor fehlerhafte Pfad; das ist kein Beweis für eine zweite Suche durch den Fix. Gleichzeitige Kampfenden können bereits native Auftragsspitzen bündeln.

### Sammelpunkte

[AssemblyPointPlacementPatch.cs](BugfixesAndQoL/src/AssemblyPointPlacementPatch.cs) verwendet `PermanentInstructionSkipPatch` an den bereits dokumentierten Status-/Ablehnungsstellen. Das logische Aktivierungsflag wählt den vorhandenen Skip-/Originalpfad; es gibt keinen neuen Gameplay-Suchaufruf im Fix.

**Ergebnis:** Nicht Heavy. Reguläre Wege später rekrutierter Einheiten können natürlich Aufwand verursachen. Initiale Pattern-/Bytevalidierung ist getrennte Startarbeit und keine fortlaufende Wegsuche.

## Vorhandene Messungen sinnvoll weiterverwenden

- [PerfProbe-Ergebnisordner](.work/a/bfqol-perf/001/results): Serienzusammenfassungen und Rohlogs, inklusive E/R/S/X1 für Gate-/Path-/KI-Umschaltungen. [Run-SettingsSeries.ps1](.work/a/bfqol-perf/001/Run-SettingsSeries.ps1) und [Run-Variant.ps1](.work/a/bfqol-perf/001/Run-Variant.ps1) sind vorhandene Einstiegspunkte. Die aktuell gesetzten Treiberdefaults beschreiben nicht rückwirkend jede alte Serie; tatsächliche Laufkonfiguration aus dem jeweiligen Log bestimmen.
- [E-Serie](.work/a/bfqol-perf/001/results/E-series.txt): EnemyGate aus/an/aus; historischer Hinweis auf unterschiedliche Simulationsraten. Kein isolierter heutiger CPU- oder FPS-Preis jeder Gateoption.
- [X1-Serie](.work/a/bfqol-perf/001/results/X1-series.txt): 13 Optionen gemeinsam deaktiviert. **Kein Einzelbeleg für alle 13.** Auch die dort enthaltenen Tempokorrekturen werden dadurch nicht als eigene große Suchalgorithmen bestätigt.
- [Lag-Auswertung](.work/a/perf-lag/001/findings.json): prominente Tor-Erreichbarkeitssamples, ausdrücklich keine alleinige Ursachen- oder exakte Zeitzuordnung.
- [L2BFQD-Rohlog](.work/a/bfqol-perf/001/results/L2BFQD_LogOutput.log), beim Lesen geändert am 23.10.2026 15:17:45: neuere DEVDIAG-Zähler, etwa `gate.compare.pcl`, `uc.vanilla.regionPair` und Gatepflege. Manche Fenster enthalten zehntausende Filter-/Regionsaufrufe pro Sekunde. Aufrufhäufigkeit belegt keine entsprechend große Eigenlaufzeit; `uc.regionPair` kann inklusive nativer Arbeit sein. In diesem Log wurden keine Zeilen im Format `assassinSearchFound/None ... expanded=... ms=...` gefunden. Das bedeutet fehlende Einzelsearch-Zeilen dieses Formats, nicht „keine Assassin-Suchen“.
- Die in der Baseline beschriebenen langen Mod-freien AI-Gruppenticks und der bereits korrigierte Assassin-Reparaturfall sind Kontrollfälle, keine aktuellen Mod-Kostenpauschalen.

## Arbeitsauftrag für den Optimierungs-Chat

1. Arbeitsstand, Native-Hash, vorhandene Buildidentitäten und aktuelle Quell-Fingerprints feststellen. Vorhandene Nutzer-/Paralleländerungen erhalten. Quellenprüfung, getestetes DLL-Artefakt und tatsächlich installiertes Artefakt auseinanderhalten.
2. Zuerst die sechs großen Kandidaten einzeln messen; anschließend die beiden kumulativen Fälle. Start mit manuellen Proben, Assassin-Negativsuchen und Gate-Cursorvalidierung ist aufgrund der konkreten Kostenpfade sinnvoll. Keine Reihenfolge aus ungeprüften Prozentwerten ableiten.
3. Kontrollierte Fälle mit kleiner/großer Karte und 1/100/mehreren Hundert relevanten Einheiten vergleichen. Jeweils kurze positive Route, Hindernisumweg und unerreichbares Ziel; für Formationen zusätzlich bewegte Vorschau, für Grabenarbeit Folgeziele und viele Arbeiter, für Raid mehrere gleichzeitig wartende Rollen, für Tore mehrere Quellregionen.
4. Bei gleichem Save, Zoom, Spieltempo und Diagnoseumfang mindestens abwechselnde Vergleichsläufe durchführen. Simulationsdurchsatz, reale native Tickzahl, Framezeiten, Such-/Knotenzähler, kopierte Bytes und Allokationen getrennt betrachten. Gesamte native Suchzeit nicht dem umgebenden Modwrapper als Eigenzeit zuschreiben.
5. Neue temporäre Diagnose ausschließlich über `Shared.DevDiag` und `/devdiag`; vorhandene Zähler bevorzugen. Diagnosebeobachter können Suchreihenfolge, Logging und Laufzeit beeinflussen. Ein schnellerer DEVDIAG-Lauf allein ist kein Release- oder FPS-Nachweis.
6. Je Optimierung Ergebnisgleichheit und relevante Seiteneffekte sichern: gerichtete Kanten, reservierte Endpunkte, Rechte, Einheiten-/Gebäudeidentität, Topologieinvalidierung, Befehlsreihenfolge und Scratch-Wiederherstellung. Keine veröffentlichten Hooks aus-/umpatchen und keinen zweiten Hookbesitzer einführen.
7. Betroffene Tests im selben Arbeitsgang suchen und anpassen, einschließlich textueller Struktur-/Quellprüfungen sowie Buildprüfungen. Bereits vorhandene Anlaufstellen: `tests/AssassinPathfinding.Tests`, `tests/GateRouting.Tests`, `tests/FriendlyMoatMovement.Tests/ManualHarness.cs`, `tests/Formations.Core.Tests`, `tests/AiRaidRetarget.Tests` und die tatsächlich ausgeführten Suchkerntests unter `Testmods/MoatMove/tests`. Daraus folgt keine Pflicht zu sachfremden Gesamttests.
8. Nur belegte Änderungen umsetzen; anschließend betroffene Tests und erforderliche Vergleiche ausführen. Änderungen von Suchbudgets oder Retrytiming als Funktions-/Qualitätstrade-off benennen, nicht unbemerkt als interne Optimierung behandeln. Kein Commit, Release, neuer Mod oder laufendes Spiel durch diesen Bericht autorisiert.

## Geprüfte Quellidentitäten

SHA-256-Momentaufnahme vom 23.10.2026, 15:35:18 MESZ. Dies sind ausgewählte entscheidende Dateien, kein vollständiger Build-Provenienznachweis. Bei Abweichungen den neuen Code prüfen; die alten Nachweise nicht umschreiben, nur weil weitere Optimierungen hinzugekommen sind.

| Datei unter `BugfixesAndQoL/src`, sofern nicht anders genannt | SHA-256 |
| --- | --- |
| `AssassinPathfindingRuntime.cs` | `AB0A9D77BC3950FABC46920A00838D9555E1AED6C7961098A07A85A97E3CC63E` |
| `GateRouting/SamePclGateRouteRuntime.cs` | `99E88B4799DA411DA493C32557DAA576B351F059D76E683CBF2ED65AE8619342` |
| `UnitCommands/Runtime/ManualUnitCommands.cs` | `7D9E19AC5F69709E1778641C1382752E60204FCDE6A3462FB756C482224EAF9E` |
| `Formations/FormationRuntime.Destinations.cs` | `EF7862A2E4EC12B711D50BEE38411F79E798F0D9E08AA8C3B2E77112D1DC87B4` |
| `UnitCommands/Movement/WeightedGridSearchKernel.cs` | `72C86A549A0F3A3268267E6711766A722B1FCEF80DEDD077B684A8447442C36C` |
| `AiRaidRetargetFixRuntime.cs` | `141B137FE78C13549EBCF9D2323A4E5F31EAB43C261FDCC35BC3A2FD66985051` |
| `ReachableEnemyGatehouseRuntime.cs` | `0D3731DCD6774B23B7F2956A8031B71BD4FAE3D9B50E5C04FC1D6996C6B20896` |
| `UnitCommands/Attack/UnitCommandPathRuntime.LadderAttackFix.cs` | `E0362C45EBADCFBC4E48337A78D765CC868BD1FD754874756CC6841D2717A822` |
| `AIBuildingTemporaryAccessClassifier.cs` | `B99B22CF635F0BCF484749E333AE116485F2B5F40D7E09C0BA31E4235B55AC46` |
| `MountedStockpileMovementPatch.cs` | `51169BF96EBD383A3832F6E45A1C59A42BCA4D579A5F25E2C945624AF2F5C155` |
| `AssassinCombatResumeRuntime.cs` | `696DBAA086695734655B5CD0F94CAFBB133A3501CB42A13D3EAF2EAF3E40AFB6` |
| `AssemblyPointPlacementPatch.cs` | `A11629E8D05037FB76FB35484E76CADCF323F03015136D93095567281151E405` |
| `Assassins/AssassinGateDescentRuntime.cs` | `A6168B5EA6AAD7C87B0F2B4ECA7B4D2AB54B76361C1780FCD77DE2F4B8D32873` |
| `APIShared/src/Pathfinding/Assassin/AssassinPathAPI.cs` (eigenes Repo) | `29F0FA0A7DACFB42D105CF529AE25D6FC62F6DF4A1B50191D0B72CF47F262240` |
