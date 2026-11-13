# Crusader Toolkit: Lernanalyse für unsere bestehenden Mods

## 1. Zweck, Quellenstand und Aussagegrenzen

Diese Entwickleranalyse untersucht, welche Mechaniken und Arbeitsweisen aus Dongges Crusader Toolkit unsere bestehenden Mods verbessern können. Sie ist keine Aufforderung, die Toolkit-Mods nachzubauen. Vorrang haben geringe laufende Kosten, zuverlässige Zustandsübergänge und die Integration in unsere vorhandenen Dienste. Besonders betrachtet werden ResourceLayout und interaktive CastlePlanner-Blueprints.

**Untersuchungsdatum: 13.11.2026.** Toolkit-Quelle: [DonggeLiu/crusader-toolkit, Commit af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf](https://github.com/DonggeLiu/crusader-toolkit/tree/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf). Lokaler Klon: `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Fremde Mods\crusader-toolkit`. Alle Toolkit-Links unten sind auf diesen Commit festgelegt. HEAD wurde beim Schreiben erneut geprüft; der Klon hatte keine von Git gemeldeten Änderungen.

Verglichen wurde der lokale Arbeitsstand unserer Mods, nicht pauschal ein veröffentlichter Release. Beim Schreiben zeigte das Mod-Repository HEAD `02f329b0fc6824c512fc12f82ba52d203e1a6847`, APIShared HEAD `26c7ecf5ae750e0118e611b0fb51261a218d17cc`. Dies sind datierte Bezugspunkte, keine Vorgaben für spätere Arbeit. Die relativen Links zu unseren Quellen führen zum jeweils lokalen Stand; Zeilenangaben sind Momentaufnahmen und nach Änderungen neu zu prüfen.

Geprüft wurden die Quellpfade der 13 Mods, wichtige Aufrufer, Zustands- und Renderingpfade sowie passende eigene Implementierungen. Drei unabhängige Teilanalysen zu weiteren Mods, Rendering und dem Baupfad wurden mit der Hauptanalyse zusammengeführt. Es wurden keine Mods installiert, keine Spielsitzungen gestartet und keine Ingame- oder Performance-Messungen ausgeführt. Die genannten Kosten folgen aus Schleifen, Allokationsstellen und Datenstrukturen; sie sind keine gemessenen FPS-Verluste oder Leistungsgewinne.

Die Aussagen sind wie folgt zu lesen:

- **Quellfakt:** direkt nachverfolgtes Verhalten der untersuchten Implementierung.
- **Übernahmeidee:** daraus abgeleiteter Verbesserungsvorschlag; noch keine getestete Implementierung.
- **Offen:** erforderlicher Laufzeit-, Kompatibilitäts- oder nativer Nachweis fehlt. Kommentare mit Aussagen über Vanilla sind ohne unabhängigen Audit nur Aussagen des Toolkit-Autors.

Bewertungen: **übernehmen** bezeichnet ein geeignetes Prinzip, nicht ungeprüft kopierbaren Code; **angepasst untersuchen** erfordert weitere Nachweise; **bereits vorhanden** vermeidet doppelte Infrastruktur; **nicht übernehmen** betrifft die konkrete Umsetzung, nicht den Nutzen des ursprünglichen Mods. Aufwand: klein = begrenzte Policy/UI-Anpassung; mittel = zusätzlicher Index oder Zustandsfluss; groß = native Verträge, synchronisierte Aktionen oder neue belastbare Datenerfassung. Es handelt sich nicht um Zeitschätzungen.

## 2. Priorisierte Ergebnisse

| Priorität | Erkenntnis | Ziel bei uns | Bewertung |
| --- | --- | --- | --- |
| 1 | Blueprint-Treffererkennung von der Darstellung trennen; nur ein aktives Ziel hervorheben | CastlePlanner | Angepasst untersuchen; günstige Interaktionsarchitektur übernehmen |
| 1 | Persistente Darstellung und bestehende Bündelung erhalten; keine zusätzliche Vollarbeit pro Frame | CastlePlanner | Bereits vorhanden; als Integrationsbedingung erhalten |
| 2 | Berechnungen und UI-Neuaufbau an relevante Änderungen binden | CastlePlanner, BFQoL, APIShared-Verbraucher | Prinzip übernehmen; zentrale Dienste bereits vorhanden |
| 2 | Räumliche Kandidatenlisten, wiederverwendete Puffer und sparsames Zurücksetzen nutzen | Blueprint-Index, bestehende Such-/Automationspfade | Bei konkretem Bedarf übernehmen; teilweise bereits vorhanden |
| 3 | Kohärente Snapshots; unbekannte, geschätzte und tatsächlich ausgeführte Zustände unterscheiden | HUD, Vorschauen, bestehende Automationen | Übernehmen |
| 3 | Manuelle Absicht gegenüber temporärer Automatik erhalten; Wiederholungen begrenzen | Vorhandene Befehls- und Automationsfunktionen | Übernehmen, wo fachlich passend |
| 4 | Wirtschaftliche Folgen und Engpässe bestehender Funktionen verständlicher erklären | CastlePlanner, ExtraFeatures, BFQoL | Optional angepasst untersuchen |

Die wichtigste neue Perspektive ist die günstige Interaktion mit einer bereits vorhandenen Vorschau. Die Toolkit-Renderer liefern hingegen keinen Nachweis, dass sie eine große Burg effizienter darstellen als CastlePlanner.

## 3. ResourceLayout und CastlePlanner im Detail

### 3.1 Wie die interaktive Vorschau tatsächlich funktioniert

**Quellfakt:** [ResourceLayoutPlugin.cs:574–617](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/ResourceLayoutPlugin.cs#L574-L617) verarbeitet einen tatsächlichen linken Mausklick. Es beendet die Prüfung bei Multiplayer, fehlenden Ghosts, UI-Treffern oder einer anderen aktiven Kartenaktion. Anschließend ermittelt es die Kartenkachel und sucht in `pinned` das erste rechteckige Gebäude, dessen Footprint diese Kachel enthält. Zwei Klicks auf dieselbe XY-Kennung innerhalb von 0,5 Sekunden lösen den Bauversuch aus.

Das sind keine anklickbaren Gebäudeobjekte. Die Noesis-Ghost-Icons sind ausdrücklich nicht hit-testbar; siehe [GhostIcons.cs:79–109](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GhostIcons.cs#L79-L109). Der dortige `Tag = "Ignore"` ist eine Anpassung an das Spiel-UI und kein allgemeiner Vertrag für unsere eigenen Oberflächen.

**Kosten:** Die lineare Footprint-Suche kostet O(N) pro Klick bei N festgehaltenen Ghosts. Sie läuft nicht als Vollsuche in jedem Frame. Die eigene Kartenkoordinaten-Rücksuche in [GameGrid.cs:73–135](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GameGrid.cs#L73-L135) ist separat zu betrachten: Sie wird benötigt, weil der Planungsmodus die normale Karteninteraktion blockiert. Diese Suchmethode müssen wir nicht automatisch mit übernehmen.

**Eigenvergleich:** [BlueprintLayout.cs:61–123](../CastlePlanner/src/BlueprintLayout.cs) enthält bereits Mapper und Weltgrenzen der Gebäude sowie zusätzliche Informationen für Tore und Treppen. [BlueprintRuntimeController.cs:104–140](../CastlePlanner/src/BlueprintRuntimeController.cs) verwendet APIShared-Präsentation, Extender-Eingabeereignisse und gemeinsame Missionsverwaltung. Ein neuer Treffermechanismus kann auf diesen Daten aufbauen, ohne unsere gesamte Darstellung auszutauschen.

**Bewertung: Prinzip übernehmen; konkrete Umsetzung angepasst untersuchen. Ziel: CastlePlanner. Aufwand: mittel für Trefferindex und Feedback; der Bauauftrag ist ein gesondertes großes Vorhaben.**

Ein geeigneter Untersuchungsansatz ist eine pro Layout aufgebaute Zuordnung von Kacheln oder räumlichen Buckets zu möglichen Bauteilen. Bei einer direkten Kachelzuordnung beträgt der Aufbau ungefähr O(A), wobei A die Summe der eingetragenen Footprint-Kacheln ist; Speicherbedarf ebenfalls O(A). Eine Anfrage kostet im üblichen Hash-Modell O(1 + k), mit k Kandidaten auf der betroffenen Kachel. Grobe Buckets sparen Einträge, verlangen aber zusätzliche Kandidatenprüfungen. Ein pauschal voll belegtes Array für die gesamte Karte ist dadurch nicht begründet.

Der Index entsteht bei einer Layoutänderung, nicht pro Frame. Überlagernde oder mehrteilige Elemente benötigen Kandidatenlisten und eine nachvollziehbare Zielwahl; ein einfacher Eintrag „Kachel → ein Gebäude“ darf legitime Überlagerungen nicht verlieren. Der Auswahlzustand braucht eine stabile Identität innerhalb der Layoutgeneration. Alte Treffer und ausstehende Aktionen werden nach Layout-, Karten- oder Rollenwechsel verworfen.

Für Feedback genügt zunächst eine wiederverwendbare Hervorhebung des aktiven Kandidaten. Mausbewegung innerhalb derselben Kachel und desselben Layouts muss keine neue Trefferberechnung auslösen; Kameraveränderungen sind bei der Koordinatenermittlung gesondert zu berücksichtigen. Es werden keine Collider, MonoBehaviours oder Noesis-Buttons je Blueprint-Gebäude benötigt. Eine notwendige aktuelle Platzierungsprüfung betrifft das konkrete Aktionsziel, nicht sämtliche Blueprint-Gebäude in jedem Frame.

Einzelklick, Doppelklick oder Zusatztaste bleiben eine spätere Bedienentscheidung. Dabei müssen Kartenbefehle, UI-Bedienung, Ziehen der Kamera, bestehende Bauaktionen und Zuschauerrollen berücksichtigt werden. Das Toolkit belegt die technische Trennbarkeit von Trefferprüfung und Rendering, entscheidet aber diese Bedienfragen nicht für uns.

### 3.2 Warum wir das Ghost-Rendering nicht auf ganze Burgen übertragen sollten

**Quellfakt:** [ResourceLayoutPlugin.cs:535–565](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/ResourceLayoutPlugin.cs#L535-L565) zeichnet alle festgehaltenen und aktuellen Ghosts im Framepfad neu. [GhostRenderer.cs:39–77](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GhostRenderer.cs#L39-L77) erzeugt je gewöhnlichem Gebäude eine Rechteckkontur und zwei Diagonalen. Jede dieser drei Linien wird als dunkler Rand plus helle Linie ausgegeben: sechs `LineRenderer`; ein Steinbruch erhält zusätzliche Linien für seinen Steinhaufen.

[GhostRenderer.cs:109–132](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GhostRenderer.cs#L109-L132) verwendet für jede ausgegebene Geometrie `points.ToArray()`, überträgt Positionen erneut und setzt Farben sowie Breiten. Die Punktbildung beinhaltet Koordinaten- und Geländeabfragen. [GhostIcons.cs:36–71](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GhostIcons.cs#L36-L71) projiziert zusätzlich die Gebäudeausdehnung in die UI und setzt Layoutwerte.

**Kostenmodell:** laufende CPU-Arbeit proportional zu den neu aufgebauten Linienpunkten und Icons; Rendererzahl proportional zur Ghostzahl; mehrere neue Punktarrays je gewöhnlichem Ghost und Frame. Als reine Größenrechnung ergeben 1.000 gewöhnliche Ghosts 6.000 Linienrenderer und 3.000 Punktarrays je vollständigem Zeichendurchlauf. Das ist keine Laufzeitmessung und sagt nichts über tatsächlich erreichbare Ghostzahlen oder konkrete Draw Calls aus.

**Eigenvergleich:** [BlueprintRenderer.cs:120–183](../CastlePlanner/src/BlueprintRenderer.cs) baut eine persistente Darstellung auf; [Zeile 284](../CastlePlanner/src/BlueprintRenderer.cs) zeigt sie bei passendem Layout und Ansicht wieder an. Bodenmarker werden ab Zeile 521 nach Kartenzeilen in Meshes gebündelt. Mesh- und Material-Caches bestehen bereits. Der Controller lädt ab [Zeile 329](../CastlePlanner/src/BlueprintRuntimeController.cs) höchstens einen abgeschlossenen Depth-Capture je Frame ein und baut bei tatsächlichen Ansichtsänderungen neu auf. [BlueprintBuildingImageLibrary.cs:22–41](../CastlePlanner/src/BlueprintBuildingImageLibrary.cs) dedupliziert Ladevorgänge und begrenzt parallele Lesevorgänge auf zwei.

**Bewertung: Toolkit-Gesamtrenderer nicht übernehmen; unsere Architektur bereits vorhanden und erhalten.** Pooling allein ist kein Grund für einen Austausch. Für einen einzelnen Hoverrahmen kann ein gepoolter Renderer sinnvoll sein, ohne die ganze Burg so zu zeichnen. Neue Bündelung oder räumliche Sichtbarkeitsunterteilung unserer Gebäudeansichten wäre erst nach Messung konkreter Engpässe zu entscheiden; Gelände- und Gebäude-Tiefensortierung darf dabei nicht verloren gehen.

### 3.3 Der Baupfad ist kein fertiges Integrationsbeispiel

**Quellfakt:** [ResourceLayoutPlugin.cs:621–645](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/ResourceLayoutPlugin.cs#L621-L645) beschafft das interne `c_game_player_build_structure_hook` per Reflection und ruft dessen `Original` auf. [GameGrid.cs:14–37](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/GameGrid.cs#L14-L37) verfährt beim Validator ebenso; der Kommentar nennt das Umgehen anderer Mod-Validierungsereignisse ausdrücklich.

Der [lokale Extender-Wrapper, BulkBuildingDetours.cs:983–1021](../shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkBuildingDetours.cs) enthält dagegen Mirror-Verhalten und `OnBuildStructure`-Pre/Post-Ereignisse um den Originalaufruf. Diese Wrapperarbeit wird durch den direkten Originalaufruf nicht ausgeführt. Das ist ein nachverfolgter Unterschied, keine Behauptung eines bereits reproduzierten Kompatibilitätsfehlers mit einem bestimmten Mod.

**Native Teilprüfung:** Die kanonische installierte DLL wurde in der vorausgehenden Analyse gegen [CURRENT.json](../_inspect/CrusaderDE-Native-Baseline/CURRENT.json) geprüft: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Die folgende Abgrenzung gilt für diesen untersuchten Stand:

| Teilpfad | RVA | Aussage und Vertrauensgrenze |
| --- | --- | --- |
| `DLL_MapAction` | `0x85BD0` | Datenbankzuordnung confirmed; Eingabepfad mit temporärem View-Spieler-Kontext |
| Normaler Gebäudeklick | `0x90CD0` | Generischer Datenbankname candidate; relevanter Pfad im bestehenden Placement-Dossier nachverfolgt: Kostenprüfung und Chore-Auftrag |
| Chore-Verarbeitung | `0x12090` | Name candidate; gelesener Ausführungszweig übernimmt Sender, XY, Mapper, Scale und Variante; bei laufender Partie erneute Kostenprüfung vor dem Bau |
| Strukturaufbau | `0x6D580` | Name candidate; Validierungs-/Abbruchzweige, mapperabhängiger Aufbau, Tile-Arbeit und Kostenabzug bei nicht freiem Bau |
| Kostenverfügbarkeit / Kostenabzug | `0xCC420` / `0xC8A90` | Namen candidate; unterschiedliche Rollen im gelesenen Kontrollfluss, nicht gleichbedeutend |
| Platzierungsvalidator | `0x77E60` | Keine vollständige geschlossene Unterfunktionsanalyse vorhanden; keine abschließende Aussage über alle indirekten Ressourcenprüfungen |

Vorhandene Quellen: [SPECTATOR_BUILD_PLACEMENT.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SPECTATOR_BUILD_PLACEMENT.md), [FARM_BUILD_PREVIEW.md](../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FARM_BUILD_PREVIEW.md) und [SpectatorRuntime.Placement.cs:376,405](../Spectator/src/SpectatorRuntime.Placement.cs). Spectator benutzt bereits `EngineInterface.PlaceMapperItem`. Auch dies ist noch keine Freigabe als Blueprint-Backend für alle Gebäudefamilien.

**Bewertung: Reflection-/Original-Baucode nicht übernehmen.** „Zieht Ressourcen ab“ bedeutet nicht „durchläuft dieselben vorherigen Prüfungen, Events und synchronisierten Aufträge“. Ein vollständiger featurebezogener Audit muss vor einer konkreten Bauimplementierung nachgeholt werden. Die Teilprüfung belegt keinen Ressourcen-Exploit; insbesondere fehlt die geschlossene Validator-Unterkette samt konkretem Laufzeitfall.

`CreatePrefab` ersetzt den fehlenden Nachweis ebenfalls nicht. [AutomationSetup.cs:136](../APIShared/src/Automation/AutomationSetup.cs) bietet einen Testaufbau mit freier Gebäudeerzeugung, keinen Produktionsvertrag für bezahlte Bauaufträge. In der gezielten APIShared-Suche wurde keine dedizierte öffentliche Fähigkeit für normale bezahlte Blueprint-Bauaufträge gefunden; dies ist eine Suchbeobachtung, keine dauerhafte Aussage über den API-Umfang.

Offen vor einer Umsetzung sind: Eingabe-/Cursorinitialisierung; Kartenkachel, Mittelpunkt und Footprint-Ursprung; Rotation und Variante; Mauern, Treppen, Tore und Compound-Gebäude; Kosten vor Auftrag und Ausführung; Besitz und Spielerrolle; Pause, Editor und Save-/Kartenwechsel; Konsum des Klicks gegenüber Vanilla; Engine-Monitor und Simulationsthread; Fremdmod-Ereignisse sowie echte Host-/Client-Synchronität. Keine dieser Lücken wird durch eine erfolgreiche lokale Gebäudeerzeugung geschlossen.

### 3.4 Hintergrundberechnung und ehrliche Ergebnisqualität

**Quellfakt:** [Region.cs:38–120](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/Region.cs#L38-L120) sammelt gültige Kandidaten mit gecachter Validitätsmaske, Queue, Radius- und Mengengrenzen. [StartPlan ab Zeile 435](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/ResourceLayoutPlugin.cs#L435-L502) erstellt diese Region synchron und führt danach den reinen Optimierer auf einem Hintergrundthread aus. Der Solver verwendet eine explizite Suchstruktur, obere Schranken und einen Undo-Trail statt vollständiger Zustandkopien je Suchschritt. [Optimizer.cs:233–289](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/Optimizer.cs#L233-L289) prüft Zeitlimit und Abbruch alle 1.024 Suchschritte und liefert Bestlösung, Knotenzahl, Laufzeit und Optimalitätsstatus.

**Lernwert:** Ein Ergebnis „bestes gefundenes innerhalb des Budgets“ ist besser als eine unbelegte Optimalitätsbehauptung. Pure Berechnung kann auf einem vorher erfassten Zustand arbeiten; Spiel- und UI-Zugriffe bleiben im dafür zulässigen Kontext. Günstige Cache-Schlüssel und begrenzte Wiederholungen verhindern, dass kleine Mausbewegungen dieselbe Planung unnötig neu starten.

**Grenzen:** Das Zeitlimit beginnt in `Solve`, nicht beim gesamten Planungsvorgang. Regionsaufbau, Konstruktion des Optimierers und die nachgelagerte [TetherPlanner.Plan-Verwendung ab Zeile 380](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/ResourceLayoutPlugin.cs#L380-L391) sind dadurch nicht automatisch begrenzt. Hintergrundarbeit allein verhindert deshalb keine Main-Thread-Spitzen. Ein neues Threadobjekt mit großem angefordertem Stack pro Lösung ist kein allgemeines Vorbild für unseren Aufgabenbetrieb. Die `Optimal`-Aussage betrifft den untersuchten Kandidatenraum und das interne Modell, nicht sämtliche denkbaren Spielplatzierungen.

**Eigenvergleich:** [AsyncPlacementEvaluation.cs](../CastlePlanner/AIVPlacement.Core/AsyncPlacementEvaluation.cs) besitzt bereits getrennte Worker-Eingaben, Cancellation, Ergebnis-/In-flight-Caches und Phasenzeiten. Das ist eine stärkere Anschlussstelle als ein zweiter Solver-Unterbau. Bei künftigen Erweiterungen vollständige Eingangsrevisionen erfassen und Ergebnisse nur für die weiterhin aktuelle Sitzung/Layoutgeneration annehmen; Cancellation allein nicht als Beweis der Aktualität behandeln.

**Bewertung: Ergebnisqualität und Phasentrennung übernehmen; Worker-/Cache-Infrastruktur bereits vorhanden. Aufwand: klein für bessere Statusangaben, mittel bei neuen Berechnungen.**

### 3.5 Geometrieprüfung ist noch keine nachgewiesene Spiel-Erreichbarkeit

[AccessCheck.cs](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/AccessCheck.cs#L20-L146) arbeitet mit wiederverwendeten flachen Arrays und einer Queue. Es flutet freie Zellen vom Rand einer lokalen Box und entfernt im Modell unerreichbare Platzierungen. Das ist ein gut überschaubares Muster für lokale geometrische Vorprüfungen. Ein Durchlauf ist ungefähr O(Boxfläche + belegte Footprint-Fläche); wiederholte Reparaturdurchläufe kommen hinzu.

Für CastlePlanner könnten daraus optionale Hinweise auf eingeschlossene Arbeitsbereiche entstehen. Diese dürfen nicht als bestätigte Wegeprüfung oder automatische Korrektur einer AIV ausgegeben werden: Das Modell ist keine vollständige Abbildung unseres Pathfinding mit Toren, Höhen, Moats, Spielerbeziehungen und modifizierten Übergängen. Die Transportfaktoren in [TetherPlanner.cs:23–29](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ResourceLayout/src/TetherPlanner.cs#L23-L29) sind ebenfalls keine von dieser Analyse verifizierten Vanilla-Verträge.

**Bewertung: angepasst untersuchen, geringe Priorität. Aufwand: mittel bis groß bei belastbaren Spielaussagen.** Vorhandene AIVPlacement-/Pathfinding-Modelle zuerst abgleichen; kein paralleles System mit anderer Erreichbarkeitsdefinition schaffen.

## 4. Weitere Mods: konkrete übertragbare Mechanismen

### 4.1 RangeDisplay: begrenzte Hervorhebung und kontextuelle Beschriftung

**Quellen:** [RangeDisplayPlugin.cs:281–354](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/RangeDisplay/src/RangeDisplayPlugin.cs#L281-L354) dedupliziert Kreise nach Position, Reichweite und Seite und begrenzt ihre Anzahl. [RangeRingRenderer.cs:26–89](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/RangeDisplay/src/RangeRingRenderer.cs#L26-L89) verwendet wiederverwendete Puffer und gepoolte Renderer. [RingIconRenderer.cs:47–110](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/RangeDisplay/src/RingIconRenderer.cs#L47-L110) sucht einen zusammenhängenden sichtbaren Bogen und setzt dort ein Symbol. [OtherUnitSelection.cs:40–75](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/RangeDisplay/src/OtherUnitSelection.cs#L40-L75) trennt Klick und Ziehen beim Loslassen der Maus.

**Nutzen bei uns:** Ein einziges aktives Blueprint-Ziel kann eine zoomunabhängig lesbare Umrandung und eine Beschriftung außerhalb verdeckender HUD-Flächen erhalten. Visuell gleichartige Hilfsanzeigen lassen sich deduplizieren, sofern die Identität der eigentlichen Bauteile erhalten bleibt. Die eigene Vorschau muss dafür nicht neu erstellt werden.

**Kosten und Grenze:** Die Ringgeometrie wird im aktiven Framepfad neu berechnet; ein Kreis kann bis zu 4.000 Punkte enthalten. Die Beschriftung projiziert den Bogen ebenfalls punktweise. Die Trefferauswahl durchsucht sichtbare Einheiten linear. Diese Techniken nicht für jedes Burggebäude in jedem Frame ausführen. Pixelbreite nur bei Zoom-/Auflösungsänderung anzupassen ist eine Untersuchungsoption; das Toolkit ist kein Leistungsnachweis dafür.

**Eigenvergleich:** Persistente Assets und ansichtsabhängige Updates in [BlueprintRenderer.cs](../CastlePlanner/src/BlueprintRenderer.cs) und [BlueprintRuntimeController.cs:329–375](../CastlePlanner/src/BlueprintRuntimeController.cs) bereits vorhanden. **Bewertung: angepasst übernehmen für ein begrenztes aktives Ziel; Aufwand klein bis mittel.** Top-Layer-Linien eignen sich als Auswahlhilfe, nicht als Ersatz für die Tiefensortierung der ganzen Burg.

### 4.2 BuildGrid: räumliche Begrenzung als grundlegende Optimierung

**Quellen:** [BuildGridPlugin.cs:40–57](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/BuildGrid/src/BuildGridPlugin.cs#L40-L57) begrenzt den einstellbaren Radius auf 3–15; [GridRenderer.cs:61–133](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/BuildGrid/src/GridRenderer.cs#L61-L133) betrachtet ein lokales Gebiet und darin verdeckende Sprites. [Zeilen 189–204 und 255–267](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/BuildGrid/src/GridRenderer.cs#L189-L267) erzeugen Gradienten und Schlüsselarrays beim Zeichnen.

**Nutzen bei uns:** Lokale Detailhilfe am Cursor begrenzen, statt eine ganze Burg mit zusätzlichen dynamischen Konturen zu versehen. Für einen Blueprint-Hover reichen das Ziel und gegebenenfalls seine unmittelbare Umgebung.

**Kosten und Grenze:** Die Verdeckungsprüfung kombiniert lokale Flächen-/Segmentarbeit mit Durchläufen durch die gesammelten Occluder; im ungünstigen Modell O(r² × C). Pooling verhindert die Gradientenallokationen nicht. Keine Erweiterung dieses Verfahrens auf die gesamte Burg ohne neue Datenstruktur und Messung.

**Eigenvergleich:** Unser Renderer bündelt Bodenmarker bereits und besitzt Geländeprojektion. **Bewertung: räumliche Begrenzung übernehmen; kompletten Grid-Renderer nicht übernehmen. Aufwand klein als Entwurfsregel, größer bei eigener Verdeckungslogik.**

### 4.3 HealthBarColors: gemeinsame Varianten und Hygiene bei Objektpools

**Quellen:** [HealthBarSprites.cs:88–165](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/HealthBarColors/src/HealthBarSprites.cs#L88-L165) cached nach Originalsprite und Stil, erkennt bereits angewandte Varianten und merkt sich ungeeignete Quellen. [HealthBarColorsPlugin.cs:207–220](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/HealthBarColors/src/HealthBarColorsPlugin.cs#L207-L220) stellt Sprites vor der Rückgabe von Renderern in den Spielpool wieder her.

**Nutzen bei uns:** Neue Auswahl-/Hoverdarstellung teilt unveränderliche Assets; wiederverwendete Objekte erhalten keinen Zustand des vorherigen Ziels. Fehlgeschlagene Assetauflösungen nicht fortwährend wiederholen. Für eine Hervorhebung ist ein kleiner veränderlicher Zustand meist günstiger als eine neue eingefärbte Textur je Gebäude.

**Kosten und Grenze:** Ein Cachetreffer ist günstig, die Erzeugung einer Variante nicht: Der Code kann GPU-Readback und CPU-Pixelbearbeitung verwenden. Die Begrenzung auf 2.048 aktive Varianten ist keine vollständige Speicherobergrenze über alle Einstellungswechsel; alte Zuordnungen bleiben zur Wiederherstellung absichtlich erhalten. Das ist hier kein als Fehler gemeldetes Verhalten, sondern ein wichtiger Besitz-/Lebensdauerunterschied.

**Eigenvergleich:** CastlePlanner teilt bereits Meshes, Materialien und Capture-Assets. [ForeignTroopPlayerColors.cs:18–70](../BugfixesAndQoL/src/Hud/ForeignTroopPlayerColors.cs) berücksichtigt die tatsächliche Palette und Multiplayer-Zuordnung; die feste Toolkit-Farbtabelle ist kein Ersatz. **Bewertung: Pool-Hygiene übernehmen; Asset-Caches bereits vorhanden. Aufwand klein bis mittel; keine generelle Texturvarianten-Infrastruktur hinzufügen.**

### 4.4 UnitStats: UI nur bei Bedeutungsänderung neu aufbauen

**Quellen:** [UnitStatsPlugin.cs:104–145](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/UnitStats/src/UnitStatsPlugin.cs#L104-L145) vergleicht Kontext, Sprache und einen Aktualisierungsbedarf. Erst danach baut es die Statistiktabelle neu auf; ein anderer HUD-Host invalidiert die Anzeige. [UnitStatsData.cs:112–164](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/UnitStats/src/UnitStatsData.cs#L112-L164) liest Spieltabellen statt alle Balancewerte separat festzuhalten.

**Nutzen bei uns:** Blueprint-Kostenübersicht, Auswahlbeschreibung oder bestehende HUD-Ergänzungen nur bei relevanten Inhalts-/Einstellungs-/Sprachänderungen neu berechnen. Günstigen Revisionsschlüssel vor teurer Sammlung prüfen. Bei durch Mods veränderbaren Daten müssen deren effektive Revisionen ebenfalls einbezogen werden; Menüwechsel allein ist dann kein ausreichender Cache-Schlüssel.

**Kosten und Grenze:** Der Toolkit-Kontextcheck läuft alle 0,25 Sekunden, der teurere Tabellenbau nur bei Änderung. Einzelne Werte sind Darstellungsvereinfachungen, etwa ein typischer Schaden über mehrere Zieltypen; nicht als exakter Schaden jedes Matchups übernehmen.

**Eigenvergleich:** [UiWorkScheduler.cs:30–95](../APIShared/src/Presentation/UiWork/UiWorkScheduler.cs) und [ForeignTroopHudRuntime.cs:68–185](../BugfixesAndQoL/src/Hud/ForeignTroopHudRuntime.cs) besitzen bereits bedarfsgesteuerte Ausführung und gezielte Aktualisierungsanlässe. **Bewertung: Invalidierungsprinzip übernehmen; Scheduler bereits vorhanden. Aufwand klein.** Keinen zusätzlichen `KeyManager.Update`-Patch je Feature einführen.

### 4.5 StockDelta: kleine Snapshots und ehrliche Lücken

**Quellen:** [StockDeltaPlugin.cs:26–59](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/StockDelta/src/StockDeltaPlugin.cs#L26-L59) hält feste Arrays für 25 Waren; [Zeilen 153–180](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/StockDelta/src/StockDeltaPlugin.cs#L153-L180) behandeln Monatswechsel und unbekannte Historie. [Zeilen 235–244 und 299–307](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/StockDelta/src/StockDeltaPlugin.cs#L235-L307) nutzen vorhandene Warenbindings und vermeiden unveränderte Textaktualisierungen.

**Nutzen bei uns:** Eine optionale Blueprint-Ressourcenübersicht könnte einen kleinen kohärenten Warenbestand mit den geplanten Kosten vergleichen. Fehlende Historie, übersprungene Monate und aktuelle Teilzeiträume müssen als solche erkennbar bleiben. Ein Bestandstrend ist eine Nettoänderung nach Bauen, Handel, Verbrauch und Produktion; ihn nicht als Produktionsrate beschriften.

**Kosten und Grenze:** Der Datenkern ist bei fester Warenzahl klein und unabhängig von der Anzahl der Blueprint-Gebäude. Die wiederholten UI-Baumsuchen und temporären Sammlungen in `UpdateLabels` sind dagegen keine Vorlage für einen großen Blueprint. Historische Trends sind kein belastbarer Ersatz für die Ressourcenprüfung eines Bauauftrags.

**Eigenvergleich:** An vorhandenen Blueprint-HUD und gemeinsame Präsentation anbinden; für Engine-Daten die dokumentierten Readback-/Threadverträge prüfen, etwa [APIShared-Katalog: Demand-driven engine readback](../APIShared/docs/API_CATALOG.md#demand-driven-engine-readback). **Bewertung: Snapshot-/Unbekannt-Prinzip übernehmen; Bestandsanzeige optional angepasst untersuchen. Aufwand mittel.**

### 4.6 BuildingProduction: tatsächliche Flüsse verständlich unterscheiden

**Quellen:** [ProductionLedger.cs:30–39 und 154–194](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/BuildingProduction/src/ProductionLedger.cs#L30-L194) unterscheiden Verbrauch und Lieferung und bilden Durchschnitte aus vollständigen Monaten. [Zeilen 201–305](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/BuildingProduction/src/ProductionLedger.cs#L201-L305) ordnen Arbeiter- und Lagerereignisse einander zu. Der Verlauf wird auf einen begrenzten Monatsbereich reduziert.

**Nutzen bei uns:** Bei ExtraFeatures-Produktionsboni oder zukünftigen Blueprint-Hinweisen „erzeugt“, „im Gebäude gepuffert“, „ins Lager geliefert“ und „als Rohstoff verbraucht“ getrennt erklären. Ein Transportgebäude mit gleichem Warentyp auf beiden Seiten darf nicht durch reine Nettierung als untätig erscheinen. Durchschnitt, aktueller Teilmonat und unbekannte Werte getrennt darstellen.

**Kosten und Grenze:** Die konkrete Ereigniszuordnung erfolgt über noch nicht gepaarte Ereignisse innerhalb eines Renderframe-Buckets. Eine gemeinsame Framenummer beweist keine gemeinsame Ursache. Reflection zur Ereignisentdeckung bzw. `UnitId`-Auswertung, zusätzliche Bestandsscans, Locks und Snapshot-Allokationen sind nicht als kostenlose Telemetrie zu übernehmen. Das Dokument behauptet keinen reproduzierten Fehlzuordnungsfall; es lehnt die Übernahme dieser Heuristik als exakten Erfassungsvertrag ab.

**Eigenvergleich:** [WorkerProductionBonusRuntime.cs:11–76](../ExtraFeatures/src/WorkerProductionBonusRuntime.cs) nutzt bereits APIShared als Besitzer der Produktionsmodifikatoren. Für zusätzliche Ist-Telemetrie wären auditierte kausale Ereignisgrenzen und simulationsseitige Erfassung nötig; nur kompakte Ergebnisse an die UI geben. **Bewertung: Darstellungssemantik übernehmen, Erfassungsheuristik nicht übernehmen. Aufwand klein für Erklärungen, groß für neue belastbare Telemetrie.**

### 4.7 GranaryOverview: aus Daten Handlungshinweise ableiten

**Quellen:** [FoodModel.cs:53–101](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/GranaryOverview/src/FoodModel.cs#L53-L101) rechnet in einem reinen Modell mehrere Rationsstufen und Nachhaltigkeit durch; [Zeilen 104–136](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/GranaryOverview/src/FoodModel.cs#L104-L129) übersetzen Defizite in Gebäudehinweise und unterscheiden Teile der Brotproduktionskette.

**Nutzen bei uns:** CastlePlanner könnte optional erklären, welche Produktionsketten eine Blaupause enthält und wo unter ausdrücklich genannten Annahmen ein Engpass liegt. ExtraFeatures könnte veränderte Produktions-/Beliebtheitswerte verständlicher erläutern. Das ist ein Mehrwert für vorhandene Features, kein Auftrag für ein zweites Granary-Panel.

**Kosten und Grenze:** Ein kompaktes Modell ist wesentlich leichter als laufende Einzelsimulation sämtlicher Arbeiter. Seine Eingänge müssen aber stimmen: Lagerbestand allein beweist keinen Flaschenhals; Wege, fehlende Arbeiter, Zwischenlager und veränderter Bedarf beeinflussen den Durchsatz. Die verwendeten festen Monats-, Essens- und Produktionswerte sind in dieser Analyse nicht unabhängig nativ verifiziert. Vorschläge daher als Schätzung mit Annahmen kennzeichnen.

**Eigenvergleich:** [PopularityCatalog.cs:12–29](../ExtraFeatures/src/PopularityCatalog.cs) und die Produktionsmodifikatoren verändern genau solche Modellparameter. Effektive Werte einlesen, keine zweite fest codierte Balancewahrheit schaffen. **Bewertung: angepasst untersuchen, Priorität 4. Aufwand mittel bis groß; Modell nach Inhalts-/Parameteränderung rechnen, nicht je Gebäude und Frame.**

### 4.8 AutoTax: stabile Automatik statt ständiges Umschalten

**Quellen:** [TaxPolicy.cs:36–120](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/AutoTax/src/TaxPolicy.cs#L36-L120) trennt Wiederherstellung, Ertrag, Gleichgewicht und manuelle Spenden. Es verwendet untere/obere Grenzen, berücksichtigt die erwartete nächste Änderung und zieht den Steueranteil aus demselben Eingangssnapshot ab. [AutoTax-Tests](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/tests/AutoTax/Program.cs#L12-L32) zeigen gezielte Szenarien für Grenzen und wechselnde Einflüsse.

**Nutzen bei uns:** Vorhandene Automatik über explizite Phasen und Hysterese stabilisieren. Zwischen „Ziel noch nicht erreicht“, „mit den verfügbaren Mitteln unerreichbar“ und „Nutzer hat absichtlich eingegriffen“ unterscheiden. Temporäre Blockierung nicht durch ständige neue Befehle beantworten. Diese Denkweise eignet sich etwa für bestehende Reparatur-/Befehlsabläufe, ohne eine neue Steuerautomatik einzuführen.

**Kosten und Grenze:** Die reine Policy prüft eine kleine feste Menge von Kandidaten; teurer und riskanter ist deren Runtime-Anbindung. Feste Steuereffekte, Food-Formeln, rohe Speicherzugriffe und zeitbasierte Schreib-Fallbacks nicht übernehmen. Die Runtime ist keine Multiplayer-Vorlage. Wir haben in dieser Analyse keinen vollständigen nativen Audit dieser Wirtschaftsformeln durchgeführt.

**Eigenvergleich:** Eigene Policy-Tests und APIShared-Modifikatoren bestehen bereits. [AIDefenseRepairRuntime.cs](../ExtraFeatures/src/AIDefenseRepairRuntime.cs) besitzt beispielsweise eine begrenzte Arbeitsqueue; das ist eine passende Vergleichsstelle für begrenztes Prüfen statt globaler Wiederholungen. **Bewertung: Zustandsmaschinenprinzip übernehmen; Steuerfeature nicht nachbauen. Aufwand klein bis mittel je bestehender Funktion.**

### 4.9 TradePresets: Vorgaben sind nicht der geladene Partiezustand

**Quellen:** [TradePresetsPlugin.cs:110–128](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TradePresets/src/TradePresetsPlugin.cs#L110-L128) unterscheidet neue Partie und Save-Laden. [Zeilen 177–223](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TradePresets/src/TradePresetsPlugin.cs#L177-L223) steuern Anwenden, Beobachten und Baseline-Erfassung. [Zeilen 261–272 und 330–365](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TradePresets/src/TradePresetsPlugin.cs#L261-L365) lassen Auto-Trade zunächst aus und speichern erkannte Änderungen an Schwellen.

**Nutzen bei uns:** Für SerpPresets und bestehende Missionseinstellungen explizit festhalten: Was ist Vorlage für eine neue Partie, was gehört dem Save, was hat der Spieler danach absichtlich geändert? Später Anschluss sollte vorhandenen Zustand beobachten statt Startvorgaben erneut durchzusetzen.

**Kosten und Grenze:** Beobachtung einiger fester Schwellen ist begrenzt; der komplette Settingsdienst darf dafür nicht ein zweites Mal entstehen. Feste Speicher-Offsets, Ersatz-Spieler-ID 1, direkte lokale Mutationen und ein pauschales „zwei Sekunden warten“ sind keine allgemeinen Readiness-Verträge. Die Quelle schließt Multiplayer aus.

**Eigenvergleich:** [Missions- und Settingsverträge im APIShared-Integrationsleitfaden](../APIShared/docs/THIRD_PARTY_GUIDE.md) und [gemeinsame Missionsverwaltung](../AGENTS/mission-lifecycle.md) sind maßgeblich. Dies belegt nicht, dass konkrete Markt-Schwellenpresets bei uns schon existieren. **Bewertung: Zustandsbesitz-Prinzip übernehmen, konkretes Marktfeature nur bei gesondertem Bedarf untersuchen. Aufwand klein für Vertragsklarheit, mittel für neue Fachfunktion.**

### 4.10 ContinuousRecruit: Absicht anzeigen, Ausführung separat behandeln

**Quellen:** [ContinuousRecruitPlugin.cs:148–180](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ContinuousRecruit/src/ContinuousRecruitPlugin.cs#L148-L180) hält aktivierte Truppentypen und versucht Rekrutierung über `GameActionCommand.MakeTroop`. [Zeilen 254–299](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/ContinuousRecruit/src/ContinuousRecruitPlugin.cs#L254-L299) zeigen einen nicht hit-testbaren Unendlich-Indikator am vorhandenen Button.

**Nutzen bei uns:** Eine geplante Aktion kann am bestehenden Ziel sichtbar sein, während Ausführung und Erfolg eigenständig behandelt werden. Für Blueprints sind „geplant“, „ausstehender Auftrag“ und „tatsächlich gebaut“ verschiedene Zustände. Ein sichtbarer Status darf keine falsche Erfolgsbestätigung liefern und soll bestehende Eingaben nicht abfangen.

**Kosten und Grenze:** Über aktive Absichten statt über alle denkbaren Objekte iterieren; Wiederholungen begrenzen. Polling, pro Frame erstellte Mengen und `ToList` aus dem Toolkit nicht als generelle Vorlage übernehmen. Der verwendete Truppenbefehl beweist weder sichere Gebäudekonstruktion noch Multiplayer-Unterstützung.

**Eigenvergleich:** CastlePlanner hat schon HUD und Eingabeereignisse; unsere [Gameplay-/Netzwerkverträge](../AGENTS/multiplayer-network.md) verlangen getrennte Vorbedingungen und synchronisierte Mutation. **Bewertung: Status-/Absichtsprinzip übernehmen; keine Rekrutierungsautomatik nachbauen. Aufwand klein bis mittel für vorhandene Funktionen.**

### 4.11 TargetPriority: räumliche Indizes optimieren nur den Teil, der sie nutzt

**Quellen:** [FireAllocation.cs:126–136](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TargetPriority/src/FireAllocation.cs#L126-L136) baut einen Zustand beim ersten Bedarf für eine neue beobachtete Spielzeit auf. [Zeilen 431–472](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TargetPriority/src/FireAllocation.cs#L431-L472) setzen berührte Einträge zurück und verwenden Listen erneut. [Zeilen 210–228](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TargetPriority/src/FireAllocation.cs#L210-L228) beschränken einen Trefferkernel auf benachbarte räumliche Buckets.

**Nutzen bei uns:** Einen Blueprint-Index nur bei Änderung aufbauen; lokale Kandidaten auswerten; temporäre Puffer teilen; bei geeigneten Suchfeldern nur berührte Bereiche zurücksetzen. Ein gemeinsamer Snapshot kann mehrere zulässige Anfragen versorgen, wenn seine Gültigkeit wirklich nachgewiesen ist.

**Kosten und Grenze:** [Rebuild ab Zeile 546](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/TargetPriority/src/FireAllocation.cs#L546-L586) läuft weiterhin über Gegner und eigene Schützen ineinander. Der betreffende Anteil ist O(E × S), obwohl ein anderer Teil Buckets besitzt. Keine Aussage „der Mod ist durch Spatial Hashing insgesamt günstig“. Spielzeitgleichheit nicht ungeprüft als Identität eines nativen Ausführungsdurchlaufs verwenden.

**Eigenvergleich:** [WeightedGridSearchKernel.cs:15–49,157–158](../Pathfinding/src/UnitCommands/Movement/WeightedGridSearchKernel.cs) besitzt bereits berührte Einträge und Generationszustände. Unsere [Performance-Regeln](../AGENTS/performance.md) behandeln pro-Durchlauf-Caches und günstige native Bedarfsschranken. **Bewertung: passende Datenstrukturprinzipien übernehmen, teilweise bereits vorhanden. Aufwand mittel für Blueprint-Index.** Native Zielauswahl, Offsets und temporäres Patchen/Zurückschreiben aus dem Toolkit nicht übertragen; keine Gameplay-Zielauswahländerung aus dieser Analyse ableiten.

### 4.12 SmartDodge: Nutzerabsicht und kurzzeitige Automatik unterscheiden

**Quellen:** [DodgeController.cs:238–354](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/SmartDodge/src/DodgeController.cs#L238-L354) speichert Bewegungsabsicht, verfolgt Fortschritt und begrenzt die Befehlsfolge. [Zeilen 360–447](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/SmartDodge/src/DodgeController.cs#L360-L447) bewerten mögliche Ausweichziele; [Zeilen 537–589](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/SmartDodge/src/DodgeController.cs#L537-L589) erfassen Gegner und berechnen Gefahr.

**Nutzen bei uns:** In bestehenden Pathfinding-/Automationsfunktionen die letzte gültige Nutzerabsicht getrennt von einer temporären Korrektur halten. Wiederaufnahme nur, wenn kein neuer Spielerbefehl oder Sitzungs-/Besitzwechsel die alte Absicht überholt hat. Fortschritt prüfen, statt allein nach Zeit immer denselben Auftrag zu wiederholen.

**Kosten und Grenze:** Mehrere Distanzen × Richtungen erzeugen Kandidaten. Pro Kandidat werden Gefahren, Gegner bzw. andere Ausweicher ausgewertet; zusätzlich können Wegprüfungen und Capturing-Delegates entstehen. Modellhaft O(K × (T × W + E + D)) zuzüglich Wegsuche für K Kandidaten, T Bedrohungen, W betrachtete Zeitfenster, E Gegner und D Ausweicher. Das ist keine geeignete Form für Hover-/Kachelpfade großer Vorschauen. Das renderseitig beobachtete Spielzeit-Deduplizieren ersetzt keinen nachgewiesenen simulationsseitigen Vertrag.

**Eigenvergleich:** Unsere Befehlsengine bleibt in [Pathfinding/src/UnitCommands](../Pathfinding/src/UnitCommands), nicht in einer zweiten Automation daneben. **Bewertung: Absichts-/Fortschrittsprinzip angepasst übernehmen; Ausweich-KI und Suchpfad nicht nachbauen. Aufwand mittel, Kompatibilitätsprüfung mit bestehenden Befehlen erforderlich.** Im Hauptmod enthaltene Testwerkzeuge sind ebenfalls kein Vorbild für unsere Trennung in Testmods und DevDiag.

## 5. Was wir bereits besitzen und bewusst weiterverwenden

| Vorhandener Baustein | Quellenvergleich | Folgerung |
| --- | --- | --- |
| Bedarfsgesteuerte UI-Ausführung | [UiWorkScheduler.cs](../APIShared/src/Presentation/UiWork/UiWorkScheduler.cs), [BlueprintRuntimeController.cs:226–280](../CastlePlanner/src/BlueprintRuntimeController.cs) | Keine unabhängigen Frame-Poller je neuer Idee; kleine günstige Frameprüfungen sind von teurem Neuaufbau zu unterscheiden |
| Persistente Projektion und Wiederanzeige | [BlueprintRenderer.cs:284–317](../CastlePlanner/src/BlueprintRenderer.cs) | Hover und Aktion müssen keine neue gesamte Burg erzeugen |
| Gemeinsame Meshes, Materialien und Asset-Caches | [BlueprintRenderer.cs](../CastlePlanner/src/BlueprintRenderer.cs), [BlueprintBuildingImageLibrary.cs](../CastlePlanner/src/BlueprintBuildingImageLibrary.cs) | Pooling aus dem Toolkit ist kein grundsätzlich neuer Architekturgewinn |
| Gemeinsamer Missionslifecycle und Rollenwechsel | [mission-lifecycle.md](../AGENTS/mission-lifecycle.md), [BlueprintRuntimeController.Role.cs](../CastlePlanner/src/BlueprintRuntimeController.Role.cs) | Keine eigene Sammlung unkoordinierter Start-/Load-/Unload-Detektoren |
| Auswahl-/HUD-Beobachtung und tatsächliche Spielerfarben | [ForeignTroopHudRuntime.cs](../BugfixesAndQoL/src/Hud/ForeignTroopHudRuntime.cs), [ForeignTroopPlayerColors.cs](../BugfixesAndQoL/src/Hud/ForeignTroopPlayerColors.cs) | Bestehende Aktualisierungs- und Farbverträge erhalten |
| Wirksame Produktions-/Beliebtheitsmodifikatoren | [WorkerProductionBonusRuntime.cs](../ExtraFeatures/src/WorkerProductionBonusRuntime.cs), [PopularityCatalog.cs](../ExtraFeatures/src/PopularityCatalog.cs) | Künftige Modelle beziehen die effektiven Werte statt Toolkit-Konstanten zu duplizieren |
| Wiederverwendete Suchfelder und begrenzte Arbeit | [WeightedGridSearchKernel.cs](../Pathfinding/src/UnitCommands/Movement/WeightedGridSearchKernel.cs), [AIDefenseRepairRuntime.cs](../ExtraFeatures/src/AIDefenseRepairRuntime.cs) | Konkrete Engpässe an vorhandenen Strukturen lösen; keine pauschale Umschreibung |

Diese Vergleiche sind keine Bestätigung, dass wir bereits jede konkrete Toolkit-Fachfunktion anbieten. Vorhandene Infrastruktur und vorhandene Produktfunktion sind verschiedene Aussagen.

## 6. Integrationsbedingungen für spätere Vorhaben

Die Lernanalyse ändert keine öffentliche API, keinen gespeicherten Datentyp und keine Netzwerknachricht. Eine spätere Implementierung prüft zunächst den aktuellen API-Katalog und passende Extender-Ereignisse. Modspezifische Bedienung, Zielwahl und Einstellungen bleiben im Mod; ein wiederverwendbarer Dienst gehört nur bei tatsächlichem Bedarf in APIShared.

Folgende Grenzen gelten insbesondere für interaktive Blueprints:

- **Lebensdauer:** Stabile Runtime-/Event-Verwurzelung erhalten; früh erzeugte Unity-Komponenten überleben das Startup-Cleanup nicht automatisch. Keine neue dauerhafte Arbeit über Plugin-`Update` oder Coroutine-Hosts einführen.
- **Threads:** Enginezustand nur unter dem nachgewiesenen Lese-/Aktionsvertrag verwenden. Simulationsentscheidungen und Mutation nicht in einen UI-Worker verschieben; Noesis nur im validierten UI-Kontext aktualisieren. Hintergrundberechnung erhält reine Eingaben und veröffentlicht aktuelle Ergebnisse begrenzt.
- **Sitzung und Rollen:** Editor, Save-Laden, Zuschauer und kontrollierter Spieler sind keine austauschbaren Fälle. Ein dargestelltes fremdes Gebäude gewährt keine Bauberechtigung.
- **Netzwerk:** Lokale Treffererkennung ist Darstellung. Ein Bauauftrag beeinflusst Gameplay und benötigt einen nachgewiesenen gewöhnlichen oder deterministisch synchronisierten Ausführungspfad. Kein vorab lokaler Bau mit anschließendem „Nachsynchronisieren“.
- **Native Integration:** Bestehende Hookbesitzer, Extender, Fixes und APIShared erhalten. Private Original-Delegates, temporäres Patchen und ungebundene Offsets sind keine Übernahmeschnittstelle. Normale Hooks logisch aktivieren, nicht laufend aus- und einpatchen.
- **Daten und Diagnose:** Bei künftigem Runtime-JSON ausschließlich unsere vorgeschriebene quellverlinkte DependencyFreeJson-Implementierung. Neue Entwicklungsdiagnose nur über DevDiag und den Diagnose-Build; Ingame-Szenarien gehören in Testmods. Dieser Auftrag fügt beides nicht hinzu.

## 7. Konkreter Prüfplan für spätere Performance- und Funktionsarbeit

Dieser Abschnitt ist ein Vorschlag für einen späteren Implementierungsauftrag, kein Bericht bereits ausgeführter Tests. Die vorhandenen Werkzeuge aus [AGENTS/performance.md](../AGENTS/performance.md) verwenden; keine neue parallele Testsammlung nur für diesen Bericht anlegen.

### Vergleichsaufbau

Aktueller CastlePlanner gegen dieselbe Version mit ausschließlich der neuen Interaktion vergleichen. Gleiche Map/Save, Blueprint-Inhalte, Zoom, Auflösung, Spielgeschwindigkeit, Pausenzustand und Plugin-Auswahl verwenden. Varianten mindestens zweimal abwechselnd ausführen; gestörte Läufe anhand der Baseline erkennen. Kleine Burg und größte tatsächlich verfügbare Burg verwenden und jeweils Gebäude-, Tile-, Renderer- und Meshzahlen protokollieren. Einzelne große Gebäude von vielen kleinen Mauerelementen unterscheiden.

| Szenario | Zu klärende Eigenschaft |
| --- | --- |
| Vorschau verborgen, keine ausstehende Arbeit | Kein zusätzlicher kontinuierlicher Vollscan und kein weiterlaufender Treffer-/Validierungsdienst ohne Bedarf |
| Sichtbare statische Vorschau, Maus unverändert | Persistente Geometrie bleibt bestehen; kein neuer Burg-Neuaufbau |
| Schnelle Mausbewegung innerhalb einer Kachel und über viele Ziele | Kandidatenarbeit hängt am relevanten Zielwechsel; Highlight wird wiederverwendet |
| Kamerabewegung, Zoom, alle Rotationen und Flattening | Treffer und Footprint bleiben korrekt; bestehende Ansichtsumstellung wird nicht durch zusätzliche Vollarbeit vervielfacht |
| Kalte und warme Asset-Caches | Einmalige Lade-/Erzeugungskosten getrennt von stationären Kosten erfassen |
| Wiederholte gültige und ungültige Klicks | Ein Eingabeereignis erzeugt höchstens die beabsichtigte Aktion; Ablehnung ist begrenzt und verständlich |
| UI, Dropdown, Kameraziehen und andere aktive Bauaktion | Keine versehentlichen Bauaufträge oder verlorenen regulären Eingaben |
| Überlagernde Teile, Tore, Treppen, Compound-Gebäude und Kartenrand | Eindeutige Zielidentität; keine falsche Vereinfachung auf ein quadratisches Standardgebäude |
| Pause, Save-/Kartenwechsel und Layoutwechsel während ausstehender Arbeit | Kein alter Treffer oder Auftrag wird in einer neuen Generation ausgeführt |
| Eigener Spieler, Editor und Zuschauer | Anzeige und Berechtigung bleiben getrennt |
| Späterer echter Multiplayer-Test | Autorität, Kosten, Reihenfolge und Ergebnis auf Host und Client übereinstimmend; lokale Einzelspielertests genügen nicht |

### Messwerte und Abnahmekriterien

Framezeiten mit p50/p95/p99 und Zahl der Frames über 50/100 ms erfassen; GC-Allokationen und Collections, CPU-Zeit der Treffer-/Highlightarbeit, Renderer-/Meshzahlen, Speicherentwicklung und Anzahl vollständiger Vorschau-Neubauten daneben auswerten. Bei tatsächlichen Bauaktionen Simulationsthread, Tick-Lücken und Chore-Ausführung getrennt von UI-Kosten betrachten. Versteckte wie sichtbare Vorschau messen: Nur einen ruhigen sichtbaren Frame zu prüfen reicht nicht.

Die erste Abnahme ist strukturell: Hover und Klick lösen keinen vollständigen `RenderCurrentLayout`-Durchlauf aus; es gibt keine neue Vollvalidierung aller Gebäude pro Frame und keine Interaktionskomponente je Bauteil. Ziel für den aufgewärmten unveränderten Treffer-/Highlightpfad ist keine neue verwaltete Allokation durch unsere zusätzliche Logik. Das muss gemessen werden, nicht aus dem Vorhandensein eines Pools geschlossen werden.

Danach vergleichende Abnahme: p95/p99 und lange Frames dürfen sich nicht reproduzierbar außerhalb der ermittelten Laufstreuung verschlechtern. Keine absolute FPS-Garantie erfinden. Kaltstart, Ansichtswechsel und Erstaufbau getrennt bewerten; teure Arbeit nicht unbemerkt vom Spielstart in die laufende Partie verschieben.

Relevante vorhandene Tests sind unter [CastlePlanner/AIVPlacement.Tests](../CastlePlanner/AIVPlacement.Tests), [CastlePlanner/Persistence.Tests](../CastlePlanner/Persistence.Tests) und den betroffenen APIShared-Diensten zu suchen. Spätere neue Verhaltenstests sollten konkret Indexergebnis gegen eine einfache Referenz, Überlagerungen, Rotation, Generationwechsel und Befehlsmehrfachauslösung absichern. Ein erfolgreicher Renderer-Test bestätigt keinen bezahlten nativen Bauauftrag.

## 8. Lizenz, Drittmaterial und weitere Nutzung

Das Repository kennzeichnet eigenen Code und Dokumentation in seiner [MIT-Lizenz](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/LICENSE) mit Copyright 2026 Dongge. Bei Übernahme von Code oder wesentlichen Teilen den dort verlangten Copyright- und Lizenzhinweis erhalten. Dieser Bericht übernimmt keine Implementierungsdateien.

[THIRD_PARTY_NOTICES.md](https://github.com/DonggeLiu/crusader-toolkit/blob/af52cc86324dc9da5ee2d96a5ab1bd69f1e372bf/THIRD_PARTY_NOTICES.md) grenzt Spieleassets und Drittsoftware gesondert ab. Die Codelizenz ist kein Nachweis für eine Weiterverbreitung von Spieletexturen oder Abhängigkeitsassemblies. Bestehende eigene Asset-/Abhängigkeitswege weiterverwenden und fremde Materialien nicht allein wegen ihrer Referenz im Toolkit übernehmen.

Die nächste sachlich begrenzte Weiterarbeit wäre die Untersuchung eines renderunabhängigen Blueprint-Trefferindexes samt einzelner Hervorhebung auf der bestehenden CastlePlanner-Architektur. Der tatsächlich bezahlte und synchronisierte Bau bleibt ein getrenntes, vorher vollständig zu auditierendes Vorhaben. Die wirtschaftlichen Hinweise folgen erst danach, sofern sie für vorhandene Funktionen gewünscht sind. Keine der hier beschriebenen Ideen autorisiert selbständig Runtime-Änderungen, Installation, Commits oder Veröffentlichung.
