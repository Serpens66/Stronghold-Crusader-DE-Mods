# Better Pathfinding für SHCDE

Aktueller Ergebnis- und Entwurfsstand: **27.10.2026**. Ursprüngliche Analyse und Quellenprüfung: **26.10.2026**. Historische Befunde bleiben datiert erhalten; die aktuelle Empfehlung steht in Abschnitt 13.

## 1. Ziel und Ergebnis

Gesucht ist ein grundsätzlich neues Navigationssystem für **10.000 Einheiten auf 800×800 großen Karten**, auf denen zusätzliche Bewegungsregeln wie MoatMove Precise dauerhaft gelten können, ohne spürbare zusätzliche Suchspitzen. Neue Aufträge sollen normalerweise innerhalb von ungefähr **100 ms** reagieren. Faktor 100 beschreibt die Größenordnung des angestrebten Fortschritts, keine zugesagte Beschleunigung jeder Einzelanfrage. Weitere Mikrooptimierungen des bestehenden Suchkerns erfüllen diesen Auftrag nicht.

**Aktuelle Empfehlung:** ein eigenständiger Navigationsdienst mit diskretem Flächen-/Portalnetz, gemeinsamer Gruppenplanung und wiederverwendbaren Bewegungsfeldern. Für den Endzustand ist ein kompakter C++-Kern mit gebündelter Anbindung vorgesehen. Große homogene Flächen sollen als zusammenhängende Bereiche behandelt werden; die spätere Integration muss native Sucharbeit tatsächlich ersetzen. MoatMove bleibt ein geeigneter erster Verbraucher. Einzelheiten und Nachweisfolge stehen in den Abschnitten 13 und 14.

Der ursprüngliche Kandidat vom 26.10.2026 wurde als lesender Testmod [BetterPathfindingTest](Testmods/BetterPathfindingTest/PROTOTYPE.md) implementiert und inzwischen offline sowie unter Unity/Mono gemessen. **Die konkrete 32×32-/128×128-Hierarchie mit vollständigen Grenzknoten-Distanzzeilen ist als Architekturversuch gescheitert**; Abschnitt 12 fasst die Nachweise zusammen. Der jetzt empfohlene Neuaufbau ist noch nicht implementiert. Seine Skalierung und die angestrebten Reaktionszeiten sind Entwicklungsziele, keine gemessenen Eigenschaften.

Erreichbarkeit, optimale Kosten und alle Precise-Zusatzbedingungen bleiben verbindlich. Gemäß der Präzisierung vom 27.10.2026 werden gleich teure Richtungsabweichungen dokumentiert und vor einer Verbraucherintegration gesondert bewertet; eine identische Richtungsfolge ist keine Vorbedingung für die Untersuchung des nächsten Prototyps.

## 2. Historischer Prüfstand und Nachweisgrenzen vom 26.10.2026

Der Mod-Workspace war vor der ursprünglichen Dokumentationsänderung am 26.10.2026 sauber:

- Branch: `develop`.
- HEAD: `46c1619b0771a5e07f814e61e9fc61e20197502c`.
- Separat gelesener APIShared-Checkout: HEAD `c14fa7c73f152ac4d3eac7672f9045131dccfd34`, ebenfalls ohne gemeldete lokale Änderungen.
- Kanonische installierte `CrusaderDE.dll`, erneut gehasht:
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Native-Hash stimmt mit [CURRENT.json](_inspect/CrusaderDE-Native-Baseline/CURRENT.json), [Datenbankmanifest](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/DATABASE_INFO.json) und den erneut abgefragten Funktionsdatensätzen überein.

Dies sind datierte Beobachtungen, keine Voraussetzungen für spätere Arbeiten. Aktuelle Quellen und SDK-/Hookverträge müssen bei einer Implementierung erneut bestimmt werden. Insbesondere beweist der gleiche Native-Hash keine unveränderte Script-Extender-Wissensschicht.

Die erneute Kurzprüfung hat die zentralen Quellstellen, Konstanten, Funktionsidentitäten, vorhandenen Auditberichte und die externen Fachquellen abgeglichen. Sie wiederholt keine vollständige DLL-Analyse. Die bereits untersuchte featurebezogene Kette und ihre Grenzen sind im [MoatMove-Audit](_inspect/MoatMove/NativeAudit/PROGRESS.md) dokumentiert; dort die spätere Abschlussbewertung zusammen mit den historischen Zwischenständen lesen.

Es wurden für diesen Bericht keine Runtime-Dateien geändert, keine Mods gebaut oder installiert und keine neuen Spiel-/Performanceläufe ausgeführt.

## 3. Belegte Probleme des aktuellen Aufbaus

### 3.1 Tatsächlich gemessene Suchkosten

Der [MoatMove-Messbericht](Testmods/MoatMove/PERFORMANCE_FINDINGS.md) dokumentiert für `Test-lag.sav` am **22.10.2026** fünf lange `EngineInterface.run`-Aufrufe:

| Dauer |
|---:|
| 2374,7 ms |
| 2763,8 ms |
| 1031,7 ms |
| 1459,3 ms |
| 1406,8 ms |

In drei untersuchten Fenstern entfielen **99,6 %, 98,6 % und 99,1 %** der Simulationsstichproben unmittelbar auf `WeightedGridSearchKernel` und `WeightedMoatRoutePlanner`. Das ist durch innerste Mod-Frames und die Self-Sicht gestützt, nicht lediglich durch eine Assembly irgendwo im Stack.

Der gespeicherte [Original-Log](Testmods/MoatMove/evidence/Test-lag-20261022-1823.log) wurde erneut gehasht. Sein SHA-256 stimmt mit dem Bericht überein:
`8A5EFC51327D83DA967E58FCA3314932039E61661182D2992A48B45C08325715`.

**Grenzen:** Diese Zahlen gehören zu jenem historischen Lauf. Suchanzahl, expandierte Knoten und Invalidierungen fehlen darin. Daraus lässt sich weder der heutige genaue Overhead noch die Häufigkeit einzelner Ursachen ableiten. Der dominante Rechenpfad ist hingegen direkt beobachtet.

Die [Vanilla-Untersuchung](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/SIM_TICK_SPIKE_PATHING.md) dokumentiert am 20.10.2026 Ticks von **483 und 480 ms** aus KI-Gruppenbewegung. Der Gruppenpfad erreicht wiederholt die Einzelplanung mit großen Suchbudgets. Das Problem existiert also bereits ohne zusätzliche Mod-Suche.

### 3.2 Warum die vorhandenen Optimierungen nicht grundsätzlich skalieren

| Aktueller Bereich | Erneut geprüfter Befund | Architektonische Folge |
|---|---|---|
| Precise-Sitzung | `SetSearchSession` invalidiert bei anderem Besitzerkontext, Spieler, Kartenepoche oder Tick. Null-Sitzungen erlauben keine dauerhafte Wiederverwendung. | Unveränderte Kartenteile werden zwischen Anfragen erneut erschlossen. |
| Precise-Rückwärtsfelder | Bis zu acht Felder je Kern; Zuordnung nach normalisiertem Kostenprofil und Strukturfilter. Ein anderes Ziel setzt das passende Feld zurück, sofern kein bereits vorhandener optimaler Präfix passt. | Leicht unterschiedliche Formationziele können große Wiederholungen verursachen. |
| Precise-Suchablauf | Rückwärtsfeld, anschließende Vorwärtssuche und gegebenenfalls ressourcenbeschränkte Verfeinerung. | Ein fachlicher Auftrag kann mehrere Suchdurchläufe benötigen. |
| Fast | Wiederverwendbare Rückwärtsfelder, Ground-/Friendly-Trennung und Gruppenbehandlung sind bereits vorhanden. Manche synchronen Aufrufe verwenden weiterhin `int.MaxValue` als Expansionsbudget. | Fast ist nicht durchgehend eine kleine, fest begrenzte Suche. |
| FastNative | Die vollständige private Eingangsmaske wird vorbereitet, bevor der native Flood beginnt; Änderungen können Masken und Felder verwerfen. | Ein schneller nativer Flood beseitigt die Kosten der Datenaufbereitung und des Neuaufbaus nicht. |
| Veröffentlichung | Ergebnisse werden pro Einheit auf Identität, Start, Ziel, Rechte, Kosten und Puffer geprüft. | Gemeinsame Planung ersetzt nicht automatisch die individuelle Übergabe. |

Maßgebliche Quellen: [Suchkern](BugfixesAndQoL/src/UnitCommands/Movement/WeightedGridSearchKernel.cs), [Planner](BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatRoutePlanner.cs), [Suchkontext](BugfixesAndQoL/src/UnitCommands/Movement/MovementSearchContext.cs), [Invalidierung](BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs), [Fast-Routing](Testmods/MoatMove/src/FastMoatRouting.cs), [Fast-Pool](Testmods/MoatMove/src/FastRoutePool.cs) und [FastNative-Felder](Testmods/MoatMove/src/FastNativeRouteField.cs).

Die historische [FastNative-Dokumentation](Testmods/MoatMove/FAST_NATIVE.md) enthält sogar einen synthetischen Vergleich, in dem FastNative einschließlich Vorbereitung langsamer als Managed Fast war. Das ist kein aktueller Spielbenchmark, bestätigt aber die Notwendigkeit, den gesamten Datenfluss zu bewerten.

Die Befunde rechtfertigen einen anderen Aufbau. Sie beweisen nicht, dass jede Codezeile bereits optimal ist oder jeder neue Auftrag sämtliche genannten Kosten auslöst.

## 4. Native Verträge für ein neues System

Alle folgenden RVAs sind ausschließlich auf den oben genannten Native-Hash bezogen. Funktionsnamen und fachliche Rollenzuordnungen sind von belegten Speicher-/Kontrollflussaussagen zu unterscheiden.

| RVA | Rolle in der untersuchten Kette | Namenszuordnung der Baseline |
|---|---|---|
| `0x11B520` | Gruppenbewegung; gemeinsame Felder und nachgelagerte Einzelbefehle | candidate |
| `0xE2610` | Nächste erreichbare PCL-Komponente für Spieler und Abfragemodus | confirmed |
| `0x196280` | Einzelplanung, Startnormalisierung, Regions-/Strukturpfade und Pufferbindung | candidate |
| `0xF4930` | Suchdispatcher mit Direktprüfung, Suchvarianten und Rekonstruktion | candidate |
| `0xE1640` / `0xE32B0` | Rekonstruktion aus nativen Suchfeldern | candidate |
| `0x1855A0` / `0xDCE60` | Konsum gespeicherter Richtungen und tatsächliche Übergangsprüfung | candidate |
| `0xD90D0` | Lokale Verbindungsaktualisierung | confirmed |
| `0xE3B90` | Gelände-/Darstellungs-Flood mit gemeinsam verwendeten Suchspeichern | candidate |
| `0x639C0` / `0x69470` | Grabenabschluss beziehungsweise Verfüllung mit Geländeänderung | candidate |

Die Identität und Verfügbarkeit der Datensätze wurde erneut mit `query.ps1 function <RVA>` geprüft. Das ist keine Freigabe, diese Adressen ungeprüft als neue Hooks zu verwenden.

### Konsequenzen

- **Regionsantwort ist kein fertiger Weg.** `E2610(manager, player, currentPCL, destinationPCL, mode)` liefert eine nächste Komponente; Gleichheit wird vor Nullprüfungen behandelt. Der Aufruf schreibt außerdem native Abfragezustände. Ein bloßes Boolean-Ersatzresultat wäre nicht gleichwertig.
- **Einzel- und Gruppenplanung müssen zusammenpassen.** Der Gruppenführer kann eine Vorprüfung bestimmen, während einzelne Mitglieder andere Regionen erreichen können. Siehe auch den aktuellen [Gruppen-Recovery-Audit](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_REGION_RECOVERY.md).
- **Native Suchspeicher sind kein dauerhafter Mod-Cache.** Andere Suchen und Geländeaktionen überschreiben Distanz-, Besuchs- und Queue-Daten. Das neue System benötigt eigene Speicher und Generationen.
- **Die Routenübergabe ist zustandsbehaftet.** Der heutige Einheitspuffer hat 1000 Bytes für gepackte Richtungsnibbles; die Mod-Publikation erlaubt höchstens 2000 Schritte. Erreichbarkeitsbeweise dürfen trotzdem längere Wege betreffen. Referenzen: [Zustandskonstanten](BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs), [Planner](BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatRoutePlanner.cs) und [Publikation](BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs).
- **Suche, Rekonstruktion und Bewegung müssen dieselben Übergänge verstehen.** Der [Assassinen-Reparaturaudit](_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_RECONSTRUCTION_REPAIR.md) dokumentiert andernfalls etwa 19 ms teure Reparaturen pro Fehlschlag.
- **Native Koordinaten und Tile-IDs sind verschieden.** Das 800x800-Koordinatenraster darf nicht pauschal als 640000 gültige native Tiles behandelt werden; die untersuchten nativen Raster haben Kapazität 320800. Gültigkeit und Abbildung müssen aus dem tatsächlichen Kartenvertrag stammen.
- **Precise ist nicht in jeder Ecke Vanilla.** Der bestehende Audit belegt beim DAFD0-Fallback für diagonalen Eintritt in einen Graben eine Beide-Ecken-Prüfung, während Precise dort eine zulässige Ecke genügen lässt. Das betrifft diesen Branch, nicht jede Diagonale. Ein Ersatz darf akzeptiertes Precise-Verhalten nicht versehentlich durch die Vanilla-Regel ersetzen.

Die bisherige Untersuchung deckt die genannte featurebezogene Kette ab. Ein vollständiger Austausch von Bewegung, Einheiten-KI oder sämtlichen nativen Suchaufrufern ist ein zusätzlicher Auditumfang.

## 5. Was ein Precise-Ersatz erhalten muss

Die Kostenwahl ist nur ein Teil des Vertrags. Die [gewichtete Publikation](BugfixesAndQoL/src/UnitCommands/Movement/WeightedMoatPublication.cs) berücksichtigt:

- exakte gerichtete Übergänge einschließlich Besitzer, Bündnisse, Höhen und Diagonalecken;
- tatsächliches Bewegungsprofil und weitere plausible Geschwindigkeitsprofile;
- mindestens 40 Ticks Einsparung im aktuellen Profil und positive Einsparung in den anderen berücksichtigten Profilen für die betreffende Verbesserung;
- Anforderungen wie „enthält Graben“, Strukturfilter und maximale veröffentlichbare Weglänge;
- besondere terminale Verfüllkontakte, die nicht als allgemein durchquerbare feindliche Grabenwege gelten dürfen;
- unveränderte Einheitenidentität, richtigen aktuellen/nächsten Start, Ziel und tatsächlichen Ausgabepuffer;
- die wirksamen Kosten aus beteiligten RouteSearch-Callbacks.

Ein einzelner skalarer „bester“ Weg zwischen zwei Regionen reicht für sämtliche dieser Bedingungen nicht aus. Ein teurerer Abschnitt kann weniger Grabenschritte oder eine geringere Gesamtlänge besitzen und dadurch als einziger die zusätzlichen Grenzen erfüllen.

Das neue System muss daher bei relevanten Anfragen zusätzliche Zustände oder nicht dominierte Kostenalternativen erhalten. Deren Zahl kann in schwierigen Fällen stark wachsen. Weder alle Alternativen vorab zu speichern noch nur eine beliebige kleine Auswahl zu behalten ist ohne weiteren Nachweis geeignet.

**Wegtreue hat drei Ebenen:**

1. Gleiche Bewegungsregeln und gleiche Erreichbarkeit.
2. Gleiche gewichtete Kosten und erfüllte zusätzliche Grenzen.
3. Gleiche konkrete Richtungsfolge bei gleichwertigen Wegen.

Die ersten beiden Ebenen sind mit einer passend aufgebauten exakten Hierarchie grundsätzlich vereinbar. Ebene 3 folgt daraus nicht. Der heutige Vorwärtskern verwendet bei Gleichstand Einfügereihenfolge, der Rückwärtskern Knotennummern und eine am ersten Start orientierte Priorität. Bereits vorhandene Felder können ebenfalls die Auswahl beeinflussen.

Die ursprüngliche Präferenz vom 26.10.2026 war „möglichst identischer Weg“. Für den nächsten Prototyp wurde am 27.10.2026 präzisiert: Bewegungsregeln, optimale Kosten und Zusatzbedingungen bleiben exakt; gleich teure Richtungsabweichungen werden reproduzierbar dokumentiert und vor der Verbraucherintegration bewertet. Ein bestandener Kostenvergleich ist weiterhin keine pauschale Abnahme der konkreten Wegwahl.

## 6. Ursprünglicher Lösungsweg A vom 26.10.2026: Exakte Hierarchie mit lokalen Distanzfeldern

Dieser Abschnitt beschreibt den damaligen Entwurf. Seine konkrete Umsetzung mit 32×32-/128×128-Regionen wurde untersucht und negativ bewertet; sie ist nicht mehr die bevorzugte Fortsetzung. Die Hinweise zu gerichteten Kanten, korrekten Fortsetzungskosten und Zustandsgrenzen bleiben fachlich relevant. Ergebnisse und neue Empfehlung stehen in den Abschnitten 12 bis 14.

### 6.1 Dauerhaftes Modell und getrennte Daten

Ein eigenes Modell speichert gültige Knoten, gerichtete Kanten und ihre Eigenschaften. Terrain und strukturelle Verbindungen werden von Zugriffsrechten, Kostenprofilen und auftragsspezifischen Endpunktausnahmen getrennt.

Die Aufbereitung liest den Spielzustand an kontrollierten Grenzen. Der Suchkern arbeitet auf eigenen kompakten Arrays ohne Spiel-API-Aufrufe oder native/verwaltete Übergänge pro expandierter Kante. Anfangs kann auch ein Managed-Kern geeignet sein; ein nativer Kern mit gebündelten Aufrufen ist eine mögliche spätere Ausführung.

Gemeinsam verwendet werden nur nachweislich äquivalente Traversierungs- und Kostenkontexte. Gleicher Einheitentyp oder dasselbe Team allein beweisen diese Äquivalenz nicht.

### 6.2 Exakte Hierarchie

Kleine Bereiche werden zu mehreren Ebenen verbunden. Fernanfragen laufen überwiegend auf dem reduzierten Graphen; genaue Kachelwege werden lokal erschlossen.

Für exakte skalare Kosten müssen alle relevanten gerichteten Grenzübergänge und die korrekten internen Distanzen erhalten bleiben. Ein langer Durchgang darf nicht einfach durch einen Portal-Mittelpunkt ersetzt werden. Ebenso ist ein vorab gewählter Korridor nur dann ausreichend, wenn die Suche außerhalb davon nachweislich keinen besseren Weg finden kann.

[Customizable Route Planning, Delling et al., 2011](https://www.microsoft.com/en-us/research/wp-content/uploads/2011/05/crp-sea.pdf) liefert hierfür ein passendes Prinzip: Partitionierung und exakte Overlaygraphen, getrennt von der Anpassung an Kostenmetriken. Die Arbeit setzt jedoch weitgehend stabile Topologie voraus. Eine SHCDE-Übertragung muss echte Topologieänderungen behandeln oder zulässige potenzielle Kanten vorhalten und über Kosten/Sperren schalten. Sie beweist außerdem nicht die Lösung unserer Mehrprofil- und Ressourcenbedingungen.

Die Verdichtung ist nicht kostenlos: viele Grenzknoten, enge Labyrinthe und zahlreiche Profile können Vorberechnung und Speicherbedarf erhöhen. Bereichsgröße und Zahl der Ebenen müssen gemessen werden.

### 6.3 Gemeinsame lokale Felder und Gruppenanfragen

Felder liefern exakte Kosten zum Ziel beziehungsweise zu korrekt bewerteten Grenzübergängen. Viele Einheiten können dieselben Ergebnisse verwenden.

Wichtig: Ein lokales Feld nur mit „dieses Portal erreichen“ ist nicht automatisch für jedes Ziel dahinter gleichwertig. Für exakte Wege müssen die tatsächlichen Fortsetzungskosten an den Grenzknoten berücksichtigt werden. Wiederverwendung verlangt passende Randwerte oder einen belegten gleichwertigen Vertrag.

Die Anfrageverarbeitung bündelt Gruppen nach gleichem Navigationsproblem. Unterschiedliche Formationsplätze bleiben unterschiedliche Ziele; ein Gruppenanker mit angehängten Endstücken garantiert keine optimalen Einzelwege.

[Emersons Flow-Field-Tiles-Kapitel](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf) beschreibt den kombinierten Einsatz von Portalgraph, lokalen Feldern, gemeinsamem Cache und Aktualisierung in Supreme Commander 2. Seine Portalmitten, Steuerungsmechanik und Integrationsverfahren sind nicht unverändert unser exakter Precise-Vertrag. Für SHCDE werden diskrete zulässige Richtungen und passende gewichtete Randwerte benötigt.

### 6.4 Erreichbarkeit und Kandidaten als eigene Abfragearten

Eine reine Erreichbarkeitsfrage erzeugt keinen vollständigen Weg. Gerichtete Komponenten und der daraus gebildete Graph können viele positive und negative Antworten wesentlich früher liefern. Gleiche stark zusammenhängende Komponente beweist gegenseitige Erreichbarkeit; verschiedene Komponenten bedeuten nicht automatisch Unerreichbarkeit.

Arbeits- und Angriffspositionen können gesammelt bewertet werden. Reservationskosten und besondere terminale Kanten bleiben Teil der jeweiligen Anfrage. Cursor und Simulation können dieselben unveränderlichen Kartendaten lesen; UI-Suchfortschritt oder UI-Budgets dürfen keine Simulationsentscheidung bestimmen.

### 6.5 Änderungen statt Tick-Verfall

Gebäude, Wände, Tore, Zugbrücken, Grabenfortschritt, Gelände, Besitzer und Bündnisse aktualisieren betroffene Daten. Eine neue Ticknummer allein macht die Karte nicht ungültig.

Lokale Kantenänderungen aktualisieren regionale Verbindungen und davon abhängige Kosteninformationen. Neue Abkürzungen müssen ebenfalls weitergegeben werden: Ein bisheriger Weg kann schlechter geworden sein, obwohl keine seiner Kacheln verändert wurde.

Die vollständige Erfassung der Writer ist eine zentrale offene Integrationsaufgabe. Vorhandene Invalidierungshooks sind Einstiegspunkte, kein Vollständigkeitsnachweis für einen dauerhaft gültigen neuen Graphen. Bloßes Umbenennen der bisherigen Invalidierung in eine Versionsnummer genügt nicht.

[D* Lite, Koenig/Likhachev, 2002](https://publications.ri.cmu.edu/d-lite) zeigt, wie ähnliche Suchaufgaben nach Änderungen Suchwissen wiederverwenden können. Solche Prinzipien kommen für die Aktualisierung infrage. D* Lite allein liefert weder unsere Regionshierarchie noch Gruppenplanung oder den Mehrprofilvertrag.

## 7. Weitere Lösungswege und ihre Grenzen

| Weg | Stärke | Wesentliche Grenze für SHCDE |
|---|---|---|
| **A: Exakte Hierarchie plus lokale Felder** | Ursprünglich erwartete Reduktion von Suchraum und wiederholter Arbeit | Konkreter Prototyp scheitert an Aufbau und Speicher; keine Fortsetzung der vollständigen Grenzknoten-Distanzzeilen empfohlen |
| **B: Polygonales Navmesh plus A*/Flow** | Starke Verdichtung großer offener Flächen | Kontinuierliche Geometrie und Glättung ändern diskrete Wege; Höhen, Einbahnkanten, Grabenränder und dynamische Belagerung brauchen besondere Modellierung |
| **C: Vollständig eigene Wegausführung und Ausweichsteuerung** | Kann auch individuelle vollständige Pfadpuffer und häufige native Neuplanung ersetzen | Zusätzlich Eingriffe in Bewegung, Belegung, Formation, Kampf, Arbeit, Save-Zustände und Multiplayer |
| **D: Approximative Hierarchie, etwa übliches HPA*** | Einfacherer Weg zu hoher Suchgeschwindigkeit | Optimale Kosten und konkrete Precise-Wege sind nicht generell erhalten |

Die [HPA*-Originalarbeit von Botea/Müller/Schaeffer](https://webdocs.cs.ualberta.ca/~mmueller/ps/hpastar.pdf) beschreibt eine annähernd optimale Hierarchie. Der erneut auffindbare [Originaltext im wissenschaftlichen Archiv](https://citeseerx.ist.psu.edu/document?doi=b0f0432ba69e4d730b93a75e3d19c8e9d811efac) bestätigt diese Einordnung. Eine darin gemessene geringe Abweichung ist keine allgemeine SHCDE-Garantie.

Parallelisierung, GPU und ein anderer Heap sind Ausführungsoptionen, kein Ersatz für das neue Karten- und Anfragekonzept. Sie beheben allein weder redundante Aufgaben noch uneinheitliche Rechte- oder Zustandsmodelle.

## 8. Integrationsmöglichkeiten

### 8.1 Erst eigener Kern, dann gezielte Verbraucher

Ein spielunabhängiger Kern erleichtert reproduzierbare Tests und Vergleiche mit Kartensnapshots. Der Runtime-Adapter übernimmt Zustandsaufnahme und Veröffentlichung.

MoatMove registriert bereits einen [Traversal-Provider](Testmods/MoatMove/src/FriendlyMoatTraversalProvider.cs) in der [gemeinsamen Befehlsruntime](BugfixesAndQoL/src/UnitCommands/Movement/TraversalProvider.cs). Das ist ein sinnvoller Einstieg für einen Verbraucher, aber keine fertige öffentliche Schnittstelle für den neuen Dienst.

Ein neues eigenständiges Mod-Plugin, eine gemeinsame Bibliothek oder eine Unterbringung wiederverwendbarer Dienste in APIShared sind organisatorische Optionen. Native Algorithmen und modspezifische Befehlslogik gehören nicht automatisch nach APIShared. Diese Zuordnung ist noch offen.

Die vorhandenen [RouteSearch-Events](APIShared/docs/API_CATALOG.md#additional-weighted-route-events) erlauben Beobachtung und Anpassung von Präferenzkosten, aber keine allgemeine Lieferung eines vollständigen Ersatzwegs. Eine neue Abfrageschnittstelle wäre bei Bedarf additiv zu entwerfen.

### 8.2 Native Arbeit wirklich ersetzen

Nur einen zweiten Solver nach Vanilla auszuführen kann weiterhin teuer bleiben. Für den vollständigen Nutzen muss der neue Dienst passende Sucharbeit ersetzen.

Das ist pro Aufrufer zu auditieren: Einige benötigen nur Erreichbarkeit, andere ein natives Feld, Initialisierungsseiteneffekte, eine Anschlusskomponente oder einen tatsächlich publizierten Einzelweg. Einen Rückgabewert vorzutäuschen genügt nicht.

Bestehende Hookbesitzer bleiben maßgeblich: unter anderem Script Extender für die Einzelbewegung, BFQoL für seine Builder-/Rekonstruktionsanbindung und APIShared für den Assassinen-Builder. Keine konkurrierenden Detours installieren. Fixes-Mod, erweiterte PCL-Daten und fremde Kosten-/Rechtebeiträge müssen berücksichtigt werden.

Bei erhaltener nativer Wegausführung bleiben Wegausgabe, Live-Prüfung und die 2000-Schritt-Grenze erhalten. Eine segmentierte Langroute oder direkte Flow-Steuerung wäre eine gesonderte Erweiterung des Verbraucherzustands.

### 8.3 Multiplayer, Lebensdauer und Startkosten

- Ganzzahlige Kosten, deterministische Gleichstände und gemeinsame logische Versionen verwenden.
- Ergebnisse nicht abhängig von Echtzeitbudget, Rechnergeschwindigkeit oder zufälligem Worker-Fertigwerden übernehmen. Ein Pending-Ergebnis ist kein Unerreichbarkeitsbeweis.
- Worker dürfen nur auf unabhängigen unveränderlichen Daten arbeiten. Native Speicher und Unity-Zustand werden nicht nebenläufig ungeprüft gelesen oder verändert.
- Kartenwechsel, Save-Laden, Editor und Neustart benötigen eindeutige Modellgenerationen.
- Prozessweite Dienste und Hooks dauerhaft verwurzeln; veröffentlichte Hooks nur logisch schalten.
- Startvorbereitung und erste Zielanfrage getrennt messen. Kartenabhängige Arbeit kann erst bei bekannter Karte geschehen; sie darf nicht unbemerkt als späterer Laufzeit-Lag versteckt werden.
- Ein eingeführter Ersatz wird nicht ungefragt mit einer veralteten Runtime-Implementierung als parallelem Fallback ausgeliefert. Referenzcode in Offline-Vergleichstests ist davon getrennt.

## 9. Erwarteter Nutzen und ehrliche Grenzen

Der angestrebte Wechsel lautet:

**Von wiederholter großer Suche je Anfrage zu geteilter Kartenaufbereitung, kleiner regionaler Suche und örtlicher Wegerschließung.**

Als reine Rechenillustration: 500 unabhängige Großsuchen gegenüber fünf gemeinsamen Ziel-/Profilberechnungen bedeuten hundertmal weniger Wiederholungen dieser Arbeit. Das setzt wirklich identische Probleme voraus, ignoriert weitere Kosten und ist insbesondere kein gemessener Faktor gegenüber dem bereits cachenden heutigen Code.

Es bleiben:

- individuelle Ziele und Profile;
- Ausgabe und Prüfung der tatsächlich benötigten Wege;
- Änderungen an Topologie, Rechten und Kosten;
- schwere Labyrinthe und ressourcenbeschränkte Sonderfälle;
- gewöhnliche Einheitenlogik, Belegung, Darstellung und weitere Enginekosten.

Damit sind deutliche Gewinne plausibel. Eine bestimmte maximale Einheitenzahl, durchgehend konstante Anfragekosten oder ein fester FPS-/Tickgewinn sind nicht belegt.

Zum [GitHub-Profil von JiuyeAyan](https://github.com/JiuyeAyan): Bei der Quellenprüfung vom 26.10.2026 nannte die gelieferte öffentliche Profilansicht drei Repositories: SCDE-Mod-Manager, JiuyeAyan-Fog-of-War und JiuyeAyan-Advanced-Control. Ein Navigationsprojekt war dort nicht sichtbar. Die Ansicht war gecacht; das ist kein Nachweis gegen private oder inzwischen anders veröffentlichte Arbeit. Die vom Nutzer zitierte Aussage über hunderttausende Einheiten ist technisch plausibel, aber für diesen Bericht kein unabhängig geprüfter Benchmark und keine Leistungszusage für SHCDE. Diese Profilprüfung wurde für den Nachtrag vom 27.10.2026 nicht wiederholt.

## 10. Ursprüngliche Nachweisfolge vom 26.10.2026

Der erste Prototyp sollte die Architektur prüfen, ohne bereits die gesamte Bewegung neu zu schreiben. Die folgende ursprüngliche Prüfliste bleibt als Historie erhalten; die aktuelle Nachweisfolge steht in Abschnitt 14:

1. Echte Kartensnapshots einschließlich gerichteter Kanten, Kosten und relevanter Kontextdaten gewinnen; Snapshot-Erzeugung selbst messen.
2. Exakte skalare Hierarchie und lokale Felder gegen eine unabhängige Referenz prüfen.
3. Precise-Ressourcenbedingungen und terminale Ausnahmen ergänzen; fachliche Gleichheit und konkrete Richtungsabweichungen getrennt ausweisen.
4. Gruppen, viele Einzelziele, unerreichbare Ziele und dynamische Belagerung einschließlich neu geöffneter Abkürzungen messen.
5. Erst nach überzeugendem Gesamtergebnis einen ausgewählten Verbraucher anbinden.

Dafür vorhandene sinnvolle Tests erweitern. Neue Fixtures nur für konkret zusätzlich abzusichernde Verträge schaffen.

| Prüffeld | Wichtige Szenarien |
|---|---|
| Regeln | Freundliche/feindliche Gräben, Diagonalecken, Höhen, Strukturen, Einbahnkanten und Assassinen |
| Ziele | Formationsplätze, Gebäude-/Einheitenangriff, Dig/Fill-Kontakte, reservierte Endpunkte |
| Kosten | Unterschiedliche Geschwindigkeiten, mehrere Profilgrenzen, 40-Tick-Schwelle und geänderte RouteSearch-Kosten |
| Grenzen | Kein Weg, Pending, ausgeschöpftes Budget, Weg über 2000 Schritte, viele nicht dominierte Alternativen |
| Aktualisierung | Bau/Abriss, Tor-/Brückenzustand, Besitzer/Bündnis, Verfüllung, neuer kürzerer Weg außerhalb des alten Verlaufs |
| Integration | Gleiche Identität, Start-/Zielbindung, echte Publikation und tatsächliche Bewegung ohne Reparaturserie |
| Lebenszyklus | Neue Karte, Save, Editor, Kartenwechsel, Abbruch/Ersetzung, Queue und Patrouille |
| Skalierung | Kleine Gruppen bis mehrere hundert Einheiten; gemeinsame und unterschiedliche Ziele; bis 800x800 |
| Multiplayer | Echte Host-/Clientläufe mit identischen Übernahmezeitpunkten und Simulationszuständen |

Erfassen: kalte/warme Anfrage, gesamte CPU-Zeit, p50/p95/p99 und Maximum, Knoten-/Kantenarbeit, Vorberechnung, Invalidierung, Speicher, Allokationen, Wegausgabe, Weg-/Kostenabweichungen sowie native Reparaturen. Spielvergleiche mit gleichen Einstellungen und abwechselnden Läufen durchführen. Vorbereitungsarbeit und Wartezeit gehören in die Gesamtbilanz.

Zum damaligen Stand blieben die konkrete Partitionierung, Umfang der Mehrzustandsdarstellung, Managed-/Native-Ausführung, Integrationsort, benötigtes Speicherbudget und der Umgang mit verbleibenden gleichwertigen Wegabweichungen offen. Die Präzisierungen vom 27.10.2026 und der vorgesehene native Endzustand stehen in den Abschnitten 1, 13 und 14. Ein konkreter Integrationsvertrag und ein belastbarer Speichernachweis stehen weiterhin aus.

## 11. Ergebnis der erneuten Quellenprüfung vom 26.10.2026

| Quelle | Erneuter Abgleich |
|---|---|
| Aktuelle lokale Quellen und Native-Baseline | Zentrale Aussagen und Konstanten bestätigt; Datenbankfunktionen passen zum installierten Hash. |
| MoatMove-Laufzeitbericht | Zahlen und Suchkette bestätigt; Original-Loghash stimmt. Historischer Messstand bleibt ausdrücklich begrenzt. |
| CRP-Originalarbeit | Exakte Overlaygraphen und Trennung von Topologie/Metrik bestätigt. Stabile Topologie und fehlender Nachweis unserer Ressourcenbedingungen präzisiert. |
| Emerson, Flow Field Tiles | Portalgraph und lokale gemeinsam genutzte Felder bestätigt. Kein Beweis identischer Precise-Wege. |
| D* Lite | Inkrementelle Wiederverwendung ähnlicher Suchaufgaben durch den offiziellen Publikationseintrag bestätigt. Direkter Abruf zeitweise mit Timeout. |
| HPA*-Originalarbeit | Annähernd optimale Auslegung erneut durch Originaltext im Archiv und Suchindex bestätigt; direkter Hochschul-PDF-Abruf wurde umgeleitet und war nicht zuverlässig verfügbar. |
| JiuyeAyan | Kein Navigationsrepository in der erneut gelieferten öffentlichen Profilansicht; Implementierung und Skalierungsbehauptung nicht unabhängig geprüft. |

Die damalige Empfehlung war Lösungsweg A. Der anschließend durchgeführte Prototyp hat die erhoffte Eignung nicht nachgewiesen. Die folgende Auswertung ersetzt diese Empfehlung, ohne die historischen Quellen- und Messbefunde umzuschreiben.

## 12. Auswertung von BetterPathfindingTest am 27.10.2026

### 12.1 Bezug und tatsächlicher Laufumfang

Ausgewertet wurde das installierte Diagnosepaket **0.1.1** auf der geladenen Karte „Back to Back“ (`test_stresstest4.sav`). Der Lauf beginnt im mod-eigenen Log um **16:40:48.775 Uhr**. Sein Diagnoseordner heißt `20261027-154048-775-s1`; der Ordnername enthält UTC, die genannten Logzeiten sind lokale Zeiten. Der Kartenvertrag umfasst 800×800 Koordinaten, aber im erfassten Graphen nur **317.604 gültige Koordinaten**. Das ist kein Test mit 10.000 unabhängig bewegten Einheiten.

| Nachweis | Ergebnis |
|---|---|
| Snapshot 1 / Snapshot 2 kopiert | **4,0448 / 1,248 ms**; beide Post-Startup-Callbacks ausgeführt |
| Graphaufbau für Snapshot 1 | **61,1095 ms**; Rohsnapshot-Puffer **4.273.521 Bytes** |
| Torregeln | `captured-immutable-published-policy`; Spieler 1 getestet, Spieler 1 bis 8 erfasst |
| Erste Anfrage mit gemeinsamem Ziel | Nach **17.108,7962 ms** mit `memory-budget` unvollständig |
| Unabhängige Vollreferenz desselben Falls | **140,6134 ms**; kein gültiger Speedup-Vergleich zu einer abgebrochenen Suche |
| Bis dahin aufgebaute Distanzzeilen | **9.672** für 32er- und **1.379** für 128er-Regionen; kein fertiges lokales Feld |
| Gruppen mit gemeinsamem Ziel: 1 / 32 / 128 / 512 | Alle am Speicherlimit gescheitert; jeweils **0 vollständig verglichene Anfragen** |
| Gesamter ausgewerteter Snapshot | **7/1.346** Anfragen verglichen, **0 gefundene Wege**, 7-mal `Unreachable`; Status `INCOMPLETE` |
| Ende | Nach **116,33 s**; `WM_CLOSE`/`Application_quitting` um 16:42:44, danach `ThreadAbortException` im Worker |

Primärnachweise: [Snapshot-1-Ergebnis mit Einzelfällen](<E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/cache/BetterPathfindingTest_Serp/20261027-154048-775-s1/snapshot-1.json>), [erster Suchfall](<E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/cache/BetterPathfindingTest_Serp/20261027-154048-775-s1/snapshot-1-shared-goal-1.md>) und [BepInEx-Log](<E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/LogOutput.log>). Die Pfade sind lokale Nachweise dieser Installation; das BepInEx-Log ist eine fortlaufend ergänzte Datei. Die Messdaten werden durch diese Dokumentationsänderung nicht verändert.

Der zweite Snapshot wurde um **16:41:00.245 Uhr** kopiert und eingereiht, aber vor dem Spielende nicht im Worker verarbeitet oder als Rohsnapshot gespeichert. Damit gibt es für diesen Lauf weder einen zweiten Abschlussbericht noch einen tatsächlichen Änderungsvergleich. Die 120-Sekunden-Grenze war beim Spielende noch nicht erreicht. Der beobachtete Threadabbruch fällt mit dem Herunterfahren zusammen; er belegt keinen unabhängig davon aufgetretenen Suchkernfehler.

### 12.2 Was bisherige Erfolgsmeldungen nicht nachweisen

Der [ältere Bericht vom 26.10.2026, Ordner `20261026-151328-021-s2`, Snapshot 1](<E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/cache/BetterPathfindingTest_Serp/20261026-151328-021-s2/snapshot-1.json>) meldet 1.346 bestandene Vergleiche. Die Einzelfälle enthalten jedoch **1.346-mal `Unreachable` und keinen gefundenen Weg**. Er bestätigt Übereinstimmung bei diesen negativen Fällen, keine erfolgreiche Ingame-Wegausgabe. Auch die sieben abgeschlossenen Fälle des neuesten Laufs sind ausschließlich negative Antworten. Der damalige Gesamtstatus ist daher keine positive Abnahme des Navigationssystems.

Der [zweite Snapshot desselben älteren Laufs](<E:/ProgrammeE/Steam/steamapps/common/Stronghold Crusader Definitive Edition/BepInEx/cache/BetterPathfindingTest_Serp/20261026-151328-021-s2/snapshot-2.json>) enthält acht übereinstimmende Änderungsvergleiche, aber **0 geänderte Koordinaten und 0 invalidierte Zeilen**. Das prüft einen unveränderten Zustand, keine Aktualisierung nach Bau, Abriss oder neuer Abkürzung. Der kontrollierte synthetische Test auf privaten Kopien bestand dagegen: Eine außerhalb des alten Weges geöffnete Verbindung senkte die Kosten von 925 auf 133; die anschließende Sperrung wurde erkannt. Diese beiden Nachweisarten bleiben getrennt.

Die bisher als `formation-profiles-*` bezeichnete Last wird hier als **verteilte Zielmenge mit Profilen** eingeordnet. Die gespeicherte 512er-Last des aktuellen Laufs enthält 512 Ziele über 18 verschiedene 32×32-Zielblöcke, 62 unterschiedliche Startkoordinaten und fünf rohe Kostenprofile. Das ist keine räumlich zusammenhängende Formation und kein Beleg für 512 unterschiedliche Einheiten. Quellstand und historische Dateinamen bleiben unverändert; künftige Prüfungen brauchen echte Formationen zusätzlich zu solchen verteilten Zielen.

Die [Offline-Nachweise vom 26.10.2026](Testmods/BetterPathfindingTest/RESULTS.md) bleiben gültige, gesonderte Befunde: 6.827 Assertions, 295 Referenzvergleiche, keine dort beobachteten Precise-Kosten-/Statusabweichungen und 60 offene gleich teure Richtungsabweichungen. Sie ersetzen weder positive Ingame-Fälle noch eine Skalierungsabnahme.

### 12.3 Speicher, Kostenursache und Bewertung

Das Limit beträgt **256 MiB = 268.435.456 Bytes** und betrifft konservativ erfasste Navigationspuffer einschließlich reservierter Arbeitsbereiche. Erschöpfung ergibt `INCOMPLETE`, niemals einen Unerreichbarkeitsbeweis. Der gesamte verwaltete Heap lag am Ende bei **1.004.785.664 Bytes, etwa 958 MiB**. Er enthält Spiel und andere Mods sowie noch nicht eingesammelte Objekte und ist kein gemessener Speicherverbrauch dieses Testmods. Die reservierten Navigationsbytes sind umgekehrt keine harte Obergrenze für den gesamten Unity-Prozess.

Die Hauptursache liegt in der konkreten [Hierarchie](Testmods/BetterPathfindingTest/src/Core/Hierarchy.cs): Pro Eintrittsknoten entstehen lokale Distanzzeilen mit vielen Grenzknotenverbindungen und vollständigen Teilwegen. Die zweite Ebene vervielfacht diese Arbeit. Ein lokales Zielfeld benötigt zudem globale Fortsetzungskosten, die im aktuellen Aufbau durch weitere globale Suchen beschafft werden. Auf offenen großen Flächen wird dadurch viel Arbeit materialisiert, bevor die erste Anfrage beantwortet ist.

Die [Teststeuerung](Testmods/BetterPathfindingTest/src/Runtime/Run.DevDiag.cs) startet für jede Arbeitslast einen neuen Navigator und wiederholt dadurch bereits gescheiterte Großaufbauten. Ihre zeitliche Aneinanderreihung verdrängte die Auswertung des zweiten Snapshots. Gemeldete warme Zeiten unvollständiger Anfragen sind kein Leistungsnachweis. Ebenso ist ein über den gesamten Snapshot geführter Speicherhöchststand kein isolierter Höchststand einer späteren kleinen Arbeitslast.

**Bewertung:** Die konkrete vollständige Grenzknoten-Hierarchie ist als Architekturversuch gescheitert. Eine höhere Speichergrenze oder weitere Optimierung ihrer Distanzzeilen ist keine empfohlene Fortsetzung. Snapshot-Erfassung und Replay-/Vergleichsinfrastruktur sind weiterhin nutzbar. Der Neuaufbau muss die grundlegende Darstellung und gemeinsame Verarbeitung ändern; ein anderer Heap, C++ allein oder ein weiterer Rückwärtsfeld-Cache genügt nicht. Der aktuelle Precise-Kern besitzt bereits eigene Rückwärtsfelder und Wiederverwendung.

## 13. Neuer Systementwurf für 10.000 Einheiten

Empfohlen wird ein **eigenständiger Navigationsdienst mit diskretem Flächen-/Portalnetz, gemeinsamer Gruppenplanung und wiederverwendbaren Bewegungsfeldern**. Dies ist ein grundlegender Neuaufbau mit noch ausstehendem Exaktheits- und Leistungsnachweis.

### 13.1 Weniger Arbeit durch ein anderes Karten- und Auftragsmodell

- **Homogene Flächen verdichten:** Große zusammenhängende Bereiche werden kompakt repräsentiert. Hindernisse, Grabenränder, Höhenwechsel, Strukturen und gerichtete Sonderübergänge bleiben ausdrücklich modelliert. Innerhalb geeigneter Flächen ersetzen kompakte Wegprimitive wiederholte Kachelsuchen. Deren Kosten, Boden-/Grabenschritte und Rekonstruktion müssen zur diskreten SHCDE-Metrik passen; kontinuierliche Luftlinien oder geglättete Navmesh-Wege sind kein Exaktheitsnachweis. Nicht nachweisbar verdichtbare Bereiche bleiben ausdrücklich dargestellt.
- **Keine vollständigen Grenzknotenmatrizen:** Übergänge speichern Topologie und kompakte Rekonstruktionsinformationen. Vollständige Teilwege zwischen sämtlichen Bereichseintritten werden nicht vorgehalten. Zu untersuchen sind implizite Flächendurchquerung und gemeinsam integrierte Randwerte, statt die gescheiterten Distanzzeilen nur in einer anderen Sprache nachzubauen.
- **Gruppen gemeinsam planen:** Äquivalente Ziel-, Rechte- und Kostenkontexte teilen Fernplanung und lokale Bewegungsfelder. Gleicher Einheitentyp oder gleiches Team genügt nicht als Freigabe für Wiederverwendung. Formationsplätze behalten ihre eigenen Ziele; ein gemeinsamer Anker mit angehängten Endstücken darf keine optimalen Einzelwege vortäuschen. Unterschiedliche Profile und räumlich verteilte Ziele werden gesondert gemessen.
- **Precise exakt erhalten:** Skalare Felder beantworten geeignete Fälle und liefern untere Kostenschranken. Grabenpflicht, Strukturfilter, mehrere Profilgrenzen, terminale Ausnahmen und Schrittgrenzen benötigen bei Bedarf zusätzliche Zustände beziehungsweise nicht dominierte Alternativen. Eine willkürlich feste Alternativenzahl darf keine gültigen Wege abschneiden. Die 40-Tick-Qualifikation und die 2.000-Schritt-Publikationsgrenze bleiben erhalten; längere Erreichbarkeit und publizierbare Wege bleiben verschiedene Abfragen.
- **Kartenwissen dauerhaft nutzen:** Ergebnisse gehören zu nachgewiesenen Topologie-, Rechte- und Kostengenerationen. Eine Ticknummer allein invalidiert keine unveränderten Daten. Änderungen müssen auch neue Abkürzungen außerhalb bestehender Wege berücksichtigen. Sind Abhängigkeiten noch nicht sicher erfasst, wird konservativ invalidiert; bloße Gültigkeit des alten Weges beweist keine unveränderte Optimalität.

### 13.2 Günstige Bewegung und kontrollierte Ausführung

Langfristig sollen Einheiten gemeinsame Routeninformationen und vorbereitete nächste Schritte konsumieren. Es soll keine vollständige Suche pro Einheit und Bewegungsschritt entstehen. Regeländerungen werden möglichst einmal pro betroffenem Kontext oder Bereich aufbereitet. Vollständige individuelle Wege ständig neu zu berechnen oder zusätzliche Planung nach Vanilla auszuführen würde das Skalierungsziel verfehlen.

Planung läuft unterbrechbar auf eigenen unveränderlichen Daten. Neue Aufträge sollen normalerweise innerhalb von ungefähr **100 ms** reagieren. Während einer Neuberechnung kann ein weiterhin zulässiger bestehender Weg genutzt werden; andernfalls wartet die Einheit kurz. Ungültige Kanten dürfen nicht wegen einer ausstehenden Berechnung benutzt werden. `Pending`, Budgetabbruch und Unerreichbarkeit bleiben unterscheidbar. Überschrittene Reaktionsziele werden ausgewiesen; ein Zeitlimit macht weder einen ungeprüften Weg optimal noch eine offene Suche unerreichbar.

Für den Endzustand ist ein **kompakter C++-Kern mit gebündelter Schnittstelle** vorgesehen, mit kompakten Puffern und Wiederverwendung statt Objektallokationen je Suchzustand. Übergänge zwischen nativer und verwalteter Ausführung dürfen nicht pro Einheit oder Suchkante erforderlich werden. C++ ist dabei eine Ausführungsentscheidung für die neue Architektur, kein eigenständiger Beschleunigungsnachweis. Verwaltete und native Navigationspuffer müssen künftig gemeinsam bilanziert werden; die bisherigen 256 MiB sind kein nachgewiesenes Budget für den Endzustand.

Die spätere Bewegungsanbindung benötigt einen eigenen vollständigen Integrationsaudit. Sie muss passende native Sucharbeit tatsächlich ersetzen und Belegung, Bewegung, Kampf, Arbeit, Kartenwechsel und gespeicherte Zustände konsistent behandeln. Bestehende Hookbesitzer bleiben maßgeblich. Für Multiplayer sind deterministische Übernahmezeitpunkte erforderlich; zufälliges Worker-Fertigwerden oder verstrichene Echtzeit dürfen keine abweichenden Spielentscheidungen erzeugen. Eine öffentliche Mod-API wird mit diesem Entwurf noch nicht festgelegt.

### 13.3 Fachquellen und Grenzen der Übertragung

| Quelle | Nutzbares Prinzip | Grenze für diesen Entwurf |
|---|---|---|
| [Emerson: Flow Field Tiles](https://www.gameaipro.com/GameAIPro/GameAIPro_Chapter23_Crowd_Pathfinding_and_Steering_Using_Flow_Field_Tiles.pdf) | Gemeinsame Bewegungsfelder, Portalgraph und örtlicher Cache statt wiederholter individueller Planung | Portalmitten und Steuerung sind kein Beweis für optimale diskrete Precise-Wege oder dessen Ressourcenbedingungen |
| [Harabor/Botea/Kilby: Rectangular Symmetry Reduction](https://pathfinding.ai/pdf/harabor-botea-kilby-sara11.pdf) | Homogene freie Flächen verdichten und über Makroübergänge exakt durchqueren | Untersucht ungerichtete Gitter mit einheitlichen Kosten und eigener Bewegungsmetrik; SHCDEs gerichtete Regeln und Profilgrenzen benötigen einen neuen Nachweis |
| [Uras/Koenig/Hernández: Subgoal Graphs](https://ojs.aaai.org/index.php/ICAPS/article/view/13568) | Suchgraph auf relevante Hindernis-/Verbindungsstellen konzentrieren | Die Exaktheit des dort behandelten Acht-Nachbar-Gitters deckt dynamische Grabenrechte, terminale Ausnahmen und Mehrprofilbedingungen nicht automatisch ab |

Diese Primärquellen wurden für den Entwurf vom 27.10.2026 herangezogen. Ihre Ergebnisse begründen die Forschungsrichtung, keine Leistungszusage für SHCDE. Keine der Arbeiten beweist unmittelbar die benötigte Kombination aller Regeln. Die konkrete Partitionierung, Makroübergänge und Reparaturstrategie müssen deshalb vor einer Integration nachgewiesen werden.

## 14. Entwicklungsfolge und Abnahmekriterien

Der nächste Implementierungsauftrag sollte den Neuaufbau zunächst wieder isoliert prüfen. Bestehende Mods werden erst nach Bewertung dieses Prototyps angeschlossen. Folgende Nachweise werden getrennt geführt:

1. **Verdichtung beweisen:** Gerichtete Kanten, homogene und gemischte Flächen, Diagonalen, Start/Ziel im Flächeninneren, unterschiedliche Profile und alle Precise-Zusatzbedingungen gegen die unabhängige Vollsuche prüfen. Gleiche optimale Kosten und ausschließlich zulässige ausgegebene Kanten sind Pflicht. Gleich teure Richtungsabweichungen werden mit reproduzierbaren Anfragen gespeichert und vor der Verbraucherintegration bewertet.
2. **Gruppenplanung messen:** Gemeinsame Ziele, räumlich zusammenhängende Formationen und verteilte Einzelziele getrennt mit 128, 512 und 10.000 Anfragen prüfen. Erreichbare reine Bodenwege, tatsächlich notwendige Grabenquerungen, wegen Zusatzbedingungen verworfene Wege und unerreichbare Ziele müssen jeweils vertreten sein. Anfragezahl und Zahl unterschiedlicher Einheiten werden getrennt ausgewiesen. Ein ausschließlich negativer Datensatz ist keine positive Gesamt-Abnahme.
3. **Dauerbetrieb prüfen:** Wiederholte Befehle, kalte und warme Kontexte, unterschiedliche Profile, Cache-Verdrängung, Kartenänderungen und neue Abkürzungen außerhalb alter Wege messen. Aktualisierte Antworten müssen mit einem unabhängigen vollständigen Neuaufbau übereinstimmen. Tatsächliche Änderungen, unveränderte Snapshots und synthetische Änderungen auf privaten Kopien bleiben getrennte Nachweise.
4. **Erst danach Bewegung anbinden:** Verbraucher einzeln integrieren und nachweisen, welche native Sucharbeit entfällt. Bereits vorhandene Bewegungs-, Kollisions-, KI- und Darstellungsarbeit separat erfassen. Der reine Suchtest erteilt weder eine Bewegungs- noch eine Multiplayer-Freigabe.

**Vergleichsmaßstab ist der aktuelle unveränderte Precise-Kern**, einschließlich seiner vorhandenen Felder, Caches und Wiederverwendung. Eine Beschleunigung nur gegenüber der unabhängigen, auf Korrektheit ausgelegten Vollreferenz genügt nicht. Quellen und Eingaben werden je Messserie festgehalten; Offline-.NET, nativer Prüfhost und Unity/Mono bleiben getrennte Laufzeitnachweise.

Gemessen werden Gesamtplanung einschließlich Vorbereitung, Aktualisierung und Wegausgabe, gesamte CPU-Arbeit, Speicher und Allokationen, Feld-/Graphgröße, tatsächlich geteilte Arbeit, Reaktionszeiten sowie zusätzliche Frame-/Tickspitzen. Kalte und warme Abfragen sowie Aufbau beim Kartenstart werden getrennt und zusätzlich in der Gesamtbilanz gezeigt. Für Reaktionszeiten sind p50/p95/p99, Maximum, ausstehende Aufträge und Überschreitungen des ungefähr 100-ms-Ziels auszuweisen. Vergleichsläufe verwenden dieselben Karten, Regeln, Einheiten und Einstellungen und werden abwechselnd wiederholt.

Vor weiteren Messungen ist die **Teststeuerung** anzupassen: Beide Snapshots früh persistieren; den tatsächlichen Änderungsvergleich priorisieren; Arbeit so aufteilen, dass lange Benchmarks nachfolgende Erfassungen und Berichte nicht verdrängen; bereits gescheiterte Großaufbauten nicht ohne neue Erkenntnis für größere Gruppen wiederholen. Referenzen erhalten unabhängige Arbeitsbudgets. Arbeitslastbezogene Speicherhöchststände, tatsächlicher Gesamtheap und Allokationen werden getrennt gemessen. Fehlende Phasen, fehlende positive Fälle, Schreibfehler, Budget- und Lifecycleabbrüche bleiben ausdrücklich unvollständig. Diese Anpassungen sind hier geplant, nicht implementiert.

**Grenze der Zusage:** Ziel ist, die zusätzlichen Navigationskosten für 10.000 Einheiten mit erweiterten Regeln beherrschbar zu machen. Ob SHCDE insgesamt 10.000 Einheiten flüssig simulieren und darstellen kann, ist eine andere Frage. Für beliebige gleichzeitige Kartenänderungen, völlig unabhängige Ziele und beliebig komplexe Ressourcenbedingungen ist keine konstante Antwortzeit bewiesen. Werden die gewünschten Reaktionszeiten oder die Gesamtperformance unter repräsentativer Last verfehlt, ist das ein negatives Ergebnis; es wird nicht durch gelockerte Regeln, ausgeblendete Vorarbeit oder ausschließlich günstige Teilmessungen verdeckt.

Der Nachtrag vom **27.10.2026** aktualisiert ausschließlich diese Dokumentation anhand vorhandener Quellen und Artefakte. Er enthält keine neue Solverimplementierung, keine Runtime-Änderung, keinen Build, keine Installation und keinen neu gestarteten Spieltest. Historische Messungen bleiben in ihren ursprünglichen Dateien erhalten.

