# Plan: allgemeine Einheitenrettung und spätere Integration in Pathfinding

Erstellt: 17.11.2026. Fortschritt aktualisiert: 25.11.2026. Arbeitsplan und fortlaufendes Abnahmeprotokoll.

## 1. Ziel, Auftrag und Zuständigkeit

Einheiten sollen nach einer Bau- oder Geländeänderung zuverlässig erkannt werden, wenn sie ihren Standort nicht mehr regulär verlassen können. Die Rettung erhält dieselbe Einheit, Gesundheit, Waren, Arbeitsplatz, Besatzung und die weiterhin gültige Befehlsabsicht. Ein unerreichbares Ziel oder längeres Stillstehen allein berechtigt zu keiner Versetzung.

Der Testmod bleibt bis zur fachlichen Abnahme der Entwicklungs- und Testort. Das spätere Produktziel ist auf ausdrücklichen Nutzerwunsch **Pathfinding**. Das ersetzt für dieses Rettungssystem den früher erwogenen Zielort BugfixesAndQoL. Wiederverwendbare Beobachtungsverträge können nach APIShared gehören; Rettungsregeln, Einstellungen und Befehlsfortsetzung gehören zum Rettungssystem und später zu Pathfinding.

Dieser Auftrag erstellt den Plan auf Grundlage des aktuellen Arbeitsstands. Er installiert oder veröffentlicht nichts und beginnt noch keine Migration in den Hauptmod. Produktionscode, fremde Quellen und vorhandene Nutzeränderungen bleiben dabei erhalten. Die vorhandenen Änderungen an `Pathfinding/src/TODO.md` und `Pathfinding/UpdateToNewDLL.md` sind kein Auftrag, sie umzuschreiben.

### Was „allgemein“ hier bedeutet

- Menschen und KI werden anhand derselben fachlichen Rettungsregeln behandelt. Die aktuell vom Menschen ausgewählte Einheit ist keine Voraussetzung.
- Bau- und Bewegungsquellen sowie Einheitenfamilien werden über eine ausdrückliche Abdeckungsmatrix erschlossen. Ein erfolgreicher Speerträgertest gibt keine Arbeiterzustände frei.
- Legale Sonderbewegungen bleiben legal. Ein Graben, eine erhöhte Fläche oder eine Mauer ist nicht für jeden Einheitentyp ein ungültiger Standort.
- Fehlt ein sicherer Zielort oder ein belegter Zustandsvertrag, bleibt die Einheit unverändert und der Fall wird als nicht gerettet ausgewiesen. Unbekannte Speicherbeschädigung wird nicht durch blindes Teleportieren behandelt.
- Neue Fehler verhindern und vorhandene Fehler aus alten Saves reparieren sind unterschiedliche Aufgaben. Ohne Vorherdaten darf der zweite Fall nicht die Beweisregeln des ersten übernehmen.

## 2. Fortschritt und Arbeitsweise

Statuswerte: **offen**, **in Arbeit**, **teilweise geprüft**, **abgenommen**. Ein Kästchen wird erst mit benanntem Nachweis abgeschlossen. „Nicht ausgeführt“ und „nicht unterstützt“ sind kein PASS.

| ID | Arbeitspaket | Status | Abhängigkeit / Ergebnis |
|---|---|---|---|
| P0 | Vorhandene Rettung, Pathfinding und Schnittstellen einordnen | Abgenommen für die dokumentierte Bestandsaufnahme | Abschnitt 3; vorhandene Nachweise in Abschnitt 4 |
| P1 | Erzeuger, Zustandsübergänge und Beobachtungsverträge auditieren | Statischer Audit abgeschlossen; Korrektur/Runtimefreigabe offen | Ausführliche Audits vom 25.11.; bestätigter API-Befund F1 vor Brokererweiterung beheben |
| P2 | Gemeinsames Kandidatenmodell, Nachbeobachtung und Budgets | Offen | P1-Verträge; keine neuen permanenten Vollscans |
| P3 | Bau- und Geländeänderungen automatisch anbinden | Teilweise geprüft | KI-AIV-Zugbrücke vorhanden; weitere Quellen offen |
| P4 | Gewöhnliche Soldaten und KI-Gruppen im Normalbetrieb absichern | Teilweise geprüft | P2/P3; bisherige kontrollierte Gruppenbelege erhalten |
| P5 | Begonnene Schritte, Next-Belegung und Sonderflächen | Offen | Vorher eigener vollständiger Bewegungs-/Belegungsaudit |
| P6 | Zivile Arbeit, Grabenfälle und Besatzungen integrieren | Teilweise geprüft | Einzelne echte Abläufe bestanden; automatische Anbindung offen |
| P7 | Bewegungsfehler als weitere Kandidatenquelle | Offen | Bestehende Besitzer nutzen; keine Mutation im Such-Callback |
| P8 | Lifecycle, Saves, Editor und Multiplayer | Teilweise geprüft | Sitzungsgates vorhanden; neue echte Abnahmen erforderlich |
| P9 | Normalbetrieb, Last und Kompatibilität abnehmen | Offen | P3–P8, messbare Budget- und Verhaltensnachweise |
| P10 | Abgenommenen Kern nach Pathfinding überführen | Offen | Testmod-Abnahme und genau ein aktiver Rettungsbesitzer |

**Nächster konkreter Arbeitsschritt:** Den bestätigten Fehler F1 im AIV-Fehlerreporter gezielt korrigieren und die reproduzierten Gegenbeispiele als erfolgreiche Isolation absichern. Danach P2 und den additiven funktionalen Broker gemäß den abgeschlossenen statischen Verträgen implementieren. Die tatsächliche Live-Spannenprüfung und Spielabnahme bleiben bis zu einem entsprechenden Auftrag offen.

### Offline-Fortschritt vom 25.11.2026

Die relevanten ausführbaren Offline-Prüfungen des vorhandenen Prototyps und des aktuellen Pathfinding-Arbeitsstands bestehen. Einzelheiten, Quellidentitäten, verworfene Laufversuche, Vertragsmatrix und offene Grenzen stehen in [OFFLINE-CHECKS.md](OFFLINE-CHECKS.md). Geprüft wurden der Testmod-Build mit 1.807 Einzelprüfungen plus Fixes-Verträgen, NativePatchGuard, 105 ausgewählte APIShared-Tests, 62 Bauzustandsprüfungen, Quell-/XAML-/Locale-Prüfungen und sämtliche vom normalen Pathfinding-Build ausgewählten Offline-Suiten. Eine fehlende explizite Tooltip-Stilzuweisung wurde im Testmod korrigiert; die betroffenen Prüfungen und sein Build wurden erfolgreich wiederholt.

Es wurde nichts installiert und kein Spiel gestartet. Die Abnahme vom 17.11. bleibt historisch. Der aktuelle Referenzstand ist Extender 2.14.4, Fixes 1.31.0 und APIShared 0.73.3; Pathfinding-Quellversion 0.1.16. Der native Hash ist unverändert. Die ältere Interop-Provenienz des Baseline-Datasets muss vor zusätzlichen nativen Integrationen relevant validiert werden. Die bekannte veränderliche Scanner-Capture im Fixes-Reiterinstaller ist statisch weiterhin vorhanden; ihre aktuelle Spielwirkung wurde nicht neu geprüft.

P1 ist durch die dokumentierten AIV-/Bau-/Spiegel-/Editor-/Bewegungsverträge vorangekommen, aber noch nicht geschlossen. P2–P10 erhalten durch erfolgreiche Tests des bestehenden Codes keine pauschale Freigabe. Weiter offen sind insbesondere zusätzliche endgültige Änderungsabschlüsse, alte Ausweichbereiche, Next-/Schrittzustände, automatische Arbeiter-/Besatzungspfade und die spätere echte Spiel-/MP-/Lastabnahme. Bis zur nächsten Umsetzung zuerst diese P1-Lücken für die ausgewählte Quelle schließen.

### Anschließender vollständiger statischer Audit vom 25.11.2026

Der spätere ausdrückliche Auditauftrag schließt die zuvor genannten statischen P1-Lücken. Einstieg und verbindliche Teilberichte: [Audits/AUDIT.md](Audits/AUDIT.md). Untersucht wurden sämtliche 23 Creator-Dispatchfamilien des gemeinsamen Baupfads, AIV/Prebuild, Menschen-/KI-Mauern, finale Original-/Mirroraufrufe, terminales Dig/Fill, alle sieben äußeren Editorwriter, terminaler Abriss, gewöhnliche und besondere Current-/Next-/Belegungsfolgen, Tunnel/Assassinen/Portal-/Ladderzustände, alle 20 Wirtschaftsrollen, Geräte/Crew sowie Lifecycle, Save, Resync und aktuelle Hookbesitzer. Sichere Nichtfreigaben sind mit konkreten legitimen beziehungsweise schädlichen Folgezuständen begründet; nicht durch pauschales „unbekannt“ ersetzt.

Der Audit beschreibt den additiven Brokervertrag einschließlich eines äußeren Mirror-Batchscopes, effektiver Childinvocations, nicht werfender Fehlerbehandlung und begrenzter innerer nativer Arbeit. Er belegt eine Current-Occupancy-Discovery mit einem Tile Halo für gewöhnliche Next-Schritte und trennt sie ausdrücklich von entfernten Portal-/Transportendpunkten. Nominale BuildingScale-Quadrate sind für Compoundbauten nicht allgemein ausreichend. Die Worker-Matrix beschreibt rollenabhängige Reise-/Kontakt-/Cargo-/Retryverträge; sie aktiviert keine zusätzlichen Runtimezustände.

**Bestätigter Zusatzbefund:** [F1](Audits/AUDIT-FINDINGS.md): Ein erneut werfendes `Exception.ToString()` im AIV-Fehlerreporter kann Original oder weitere Receipts verhindern beziehungsweise eine Originalexception maskieren. Drei Gegenbeispiele und ein normaler Kontrollfall wurden am tatsächlichen Workspace-Produktionsdispatch offline reproduziert, unabhängig wiederholt und mit Assembly-SHA/MVID dokumentiert. F1 wurde in diesem Auditauftrag nicht korrigiert. Das ist ein seltener bestätigter Fehlerpfad, kein neu beobachteter Spielschaden.

Native/Managed-Hashes wurden erneut geprüft; die Extenderstände von Dataset, installierter Referenz und aktuellem Quellbaum2.14.6 wurden getrennt abgeglichen. Keine Installation, kein Spielstart, keine Runtimequellenänderung. Die wiederverwendbaren neuen nativen Fakten stehen zusätzlich im bestehenden Baseline-Dossier `TRAPPED_UNIT_RESCUE_AUDIT.md`. P2–P10 bleiben Implementierungs-/Abnahmearbeit; Altsave-/Besitzer-/Persistenzentscheidungen bleiben ausdrücklich offen.

### Bei jeder Fortsetzung

1. Diesen Plan, `TESTING.md`, relevante Updateverträge und `.work/INDEX.json` lesen; Branch, HEAD, Arbeitsstand und referenzierte Nachweise erneut prüfen.
2. Ein Arbeitspaket auswählen und als „in Arbeit“ markieren. Geänderte Annahmen und Entscheidungen im Änderungsprotokoll festhalten.
3. Vor neuen nativen Eingriffen den vollständigen featurebezogenen Audit einschließlich Abbruch-/Fehlerpfaden, nachgelagerten Lesern, Fremdpatches und Parameterdomänen abschließen. Die unten genannten Suchorte sind keine Freigabe für neue Hooks.
4. Kleine, überprüfbare Implementierung im Testmod beziehungsweise eine notwendige allgemeine APIShared-Erweiterung vornehmen. Betroffene vorhandene Tests im selben Schritt pflegen.
5. Relevante Offline-, Integrations- und Spielprüfungen durchführen. Erst dann Status, Abdeckung, Grenzen und Belegpfade aktualisieren.
6. Historische Fehlerbelege nicht überschreiben. Reproduzierbare temporäre Kopien aufräumen, Originalinstallation verifiziert wiederherstellen. Keine Commits oder Veröffentlichungen ohne Auftrag.

## 3. Geprüfter Ausgangsstand von Pathfinding

Die folgende Tabelle ist eine Momentaufnahme des lokalen Quellstands, kein dauerhafter Vertrag über Dateinamen oder Versionen. Ausgangs-HEAD des Mod-Repositories: `9dcd28b4384b8ff7efb5b2852a38832993cfc9db`, Branch `develop`, einschließlich vorhandener uncommitteter Änderungen. `Pathfinding/info.json` bezeichnet den Quellstand als Version `0.1.10`, mit APIShared-Mindestversion `0.53.0`.

Die installierte kanonische native DLL wurde für diese Bestandsaufnahme erneut gehasht und stimmt mit `CURRENT.json` überein:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Vor späterer Adressverwendung erneut gegen den dann aktuellen Baseline-Verweis prüfen. Es wurden für diesen Plan keine neuen Spiel- oder Buildläufe ausgeführt.

| Bereich / Quelle | Vorhandene Funktion | Bedeutung und Grenze für die Rettung |
|---|---|---|
| [NativeMovementRecovery.cs](../../Pathfinding/src/UnitCommands/Movement/NativeMovementRecovery.cs), `InstallConnectivityAndRecovery` | Besitzt native Beobachter für Kachelaktualisierung, Graben-Schreiben/-Löschen und Masken-Neuaufbau | Keine konkurrierenden Hooks an diesen Stellen. Meist Nachher-Invalidierung; kein vollständiger Vorher-/Nachher-Bauvorgang und keine alten Ausweichfelder. |
| Dieselbe Datei, `InvalidateMovementSearchData` | Erhöht `placementRevision`, verwirft Such-/Gruppencaches und invalidiert Kacheln, Richtungen oder ganze Daten | `placementRevision` steigt auch bei `terrain:false` in verschachtelten Bewegungsaufrufen. Nicht ungeprüft als reine Geländeänderung oder peerübergreifend identische Rettungsrevision benutzen. |
| Dieselbe Datei, `TryRecoverBeforeBuilder` | Eng begrenzte Wiederherstellung eines belegten freundlichen Grabenroutenfalls vor dem Builder | Kein allgemeiner Teleport oder beliebiger ungültiger Start. Benötigt vorhandenen UnitMoveFrame, passenden Plan/Modus/Ziel und validierte kodierbare Kanten. |
| [UnitMovementContext.cs](../../Pathfinding/src/UnitCommands/Movement/UnitMovementContext.cs) | `DispatchUnitMoveEvent` / `ObserveUnitMoveOrder` ordnen Pre/Post-Aufrufe mit Epoch, Tick und verschachteltem UnitMoveFrame zu | Nützlich für echte abgefangene Bewegungsaufträge, aber nicht jede native Suche oder physische Schrittablehnung. Post enthält ursprüngliche Argumente; übersprungene Originals haben kein Post. |
| [ManualGroupAdmission.cs](../../Pathfinding/src/UnitCommands/Runtime/ManualGroupAdmission.cs) | Behandelt manuelle Gruppen mit gemischt erreichbaren Mitgliedern | `BeginManualGroupAdmission` schließt KI-Besitzer außerhalb des Editors ausdrücklich aus. `EnableImprovedManualUnitCommands` schützt damit nicht allgemein KI-Gruppen. |
| [FastMovementScheduler.cs](../../Pathfinding/src/Engine/FastMovementScheduler.cs) | Gemeinsame Simulationstaktung, Gruppen-/Einzelbefehle und Route-Veröffentlichung | Fast-Gruppenrouting hat auch KI-Pfade. Das ist keine physische Rettung und keine Aufhebung der manuellen KI-Ausschlüsse. |
| [RouteReplanWatch.cs](../../Pathfinding/src/Engine/RouteReplanWatch.cs) | Begrenzte Routenvorschau, Kandidatenlisten, Wartezustände, Auftragssequenzen, Fortsetzung | Gute Vorbilder für Identität, neuere Befehle und deterministische Budgets. An `AnchorSingleMoves`, geeignete Soldaten und relevante Routenregeln gebunden; nicht unverändert für alle Arbeiter nutzbar. |
| [FireEscape.cs](../../Pathfinding/src/Engine/FireEscape.cs) | Gefahrenflucht und erneute Versuche | Eigene aktive Gefahrensteuerung schließt KI ausdrücklich aus. Diese Grenze nicht versehentlich in die neue allgemeine Rettung übernehmen oder pauschal entfernen. |
| [MovementSurfaces.cs](../../Pathfinding/src/Engine/MovementSurfaces.cs), [TraversalCommandState.cs](../../Pathfinding/src/Engine/TraversalCommandState.cs) | Oberflächenklassen, Revisionen und begrenzte Invalidierung | Wiederverwenden, soweit Vertrag/Profil/Thread passen; frisch validierte Live-Zielbedingungen bleiben erforderlich. |
| [GroundFailureProofs.cs](../../Pathfinding/src/Engine/GroundFailureProofs.cs), [MoatFailureProofs.cs](../../Pathfinding/src/Engine/MoatFailureProofs.cs), [AnchorRouting.cs](../../Pathfinding/src/Engine/AnchorRouting.cs) | Fehlernachweise, Such- und Veröffentlichungsbesitzer | Ein Fehlschlag zu einem Ziel belegt keinen eingeschlossenen Quellort. Integration innerhalb vorhandener Besitzer prüfen, keinen zweiten Builder-Detour installieren. |
| [SoldierTribeMoves.cs](../../Pathfinding/src/Engine/SoldierTribeMoves.cs) | Kontrollierte temporäre Einzelverbände und Rückkehr zu Gruppen | Befehlssemantik studieren; nicht blind für KI, Arbeiter oder Rettung übernehmen. Bevorzugt ursprünglichen Verband unverändert lassen. |
| [MovementCaseSave.cs](../../Pathfinding/src/Engine/MovementCaseSave.cs), [EngineHost.cs](../../Pathfinding/src/Engine/EngineHost.cs) | Versionierte Bewegungsfälle, Wiederherstellung, prozessweit verwurzelter Host und Missionsoptionen | Spätere Integrationsorte. Ausstehende Rettung ist keine gespeicherte native Pointeransicht und darf beim Laden nicht blind wiederholt werden. |

### Bestehende Kostenmodelle richtig einordnen

Pathfinding ist nicht vollständig ereignisbasiert. `RouteReplanWatch` verteilt unter Bedarf Prüfungen über ungefähr ein Vierzigstel der IDs je Tick, kurzzeitig ein Achtel, plus eine Kandidatenliste und begrenzte Routenvorschau. Die Gefahrenflucht kann bei aktiven Gefahren alle IDs in ihrem Acht-Tick-Takt prüfen. Anchor-Spielererfassung und Oberflächenaktualisierung besitzen weitere verteilte Prüfungen.

Das sind vorhandene, bedingte Verfahren. Der Rettungsmod soll nicht noch einen unabhängigen permanenten Einheitenscan hinzufügen. Ebenso wenig werden diese bestehenden Verfahren ohne gesonderten Nachweis als Teil der Rettungsarbeit umgebaut. Neue Budgets werden am zusätzlichen Aufwand und am gesamten Simulationstakt gemessen.

### Öffentliche API ist nicht mit interner Engine gleichzusetzen

- Die wesentlichen Pathfinding-Runtimeklassen und `UnitCommandPathAPI` sind intern. Der eigenständige Testmod verlinkt oder kopiert nicht die gesamte Engine. Notwendige Zusammenarbeit erhält einen kleinen belegten Vertrag; Tests dürfen interne Ergebnisse gezielt per Reflection prüfen.
- [RouteSearchEvents](../../APIShared/src/Pathfinding/Routes/RouteSearchEvents.cs) beobachten zusätzliche gewichtete Suchen teilnehmender Herausgeber. Sie sind kein allgemeiner nativer Fehlerkanal. Ihr Callback-Vertrag verbietet Befehle und Weltmutationen. Der Kontext enthält keine allgemeine stabile Einheitenidentität; alleinige Zuordnung über Endpunkte reicht nicht.
- `RouteTileRules` und deren Journal betreffen veröffentlichte Routenregeln. Das Journal ist kein vollständiges Protokoll aller nativen Geländeänderungen.
- `EnemyBridgeDiagnosticBridge` und `TemporaryGateRouteAcceptanceBridge` sind Diagnose-/Beobachtungsverträge. Aus ihrer Verfügbarkeit folgt keine Erlaubnis für funktionale Rettung darin.
- [PathReachability](../../APIShared/src/Pathfinding/Reachability/PathReachability.cs) ist ein grober spieler-/modusabhängiger Komponentenfilter. `Connected` beweist keinen ausführbaren Detailweg; ein unerreichbares Ziel beweist keinen feststeckenden Start. Die Abfrage verändert native Suchbuchhaltung und ist nicht beliebig nebenwirkungsfrei verschachtelbar.
- Der gelesene API-Katalog führt `PathReachability` ab APIShared `0.54.0`; Pathfinding deklariert derzeit mindestens `0.53.0`. Eine spätere Nutzung muss SDK, Laufzeitverfügbarkeit und Mindestversion tatsächlich klären, statt Quellpräsenz mit installierter API gleichzusetzen.

## 4. Bereits vorhandene Rettungsbelege und ihre Grenzen

Verbindlicher Ausgangsnachweis: [TESTING.md](TESTING.md), [AIV-Observer-Audit.md](AIV-Observer-Audit.md), [UpdateToNewDLL.md](UpdateToNewDLL.md), [zusammengefasste Laufdaten](../../.work/a/rescue-next/001/result.json).

- Automatische KI-AIV-Zugbrückenrettung für zugelassene aktuelle Soldaten-/Reiterbelegungen: echte Bauausführung, No-op-Gegenprobe, Bewegung danach, auch ohne optionale Hauptmods bestanden. Andere Bau-/Editorpfade und Next-only-Eintritte sind dadurch nicht abgenommen.
- Kontrollierte Brücken-, Gruppen-, KI-Mauer-, Besatzungs- und Apfelarbeitszyklen bestanden. Noch kein allgemeiner automatischer Einheiten-/Arbeitsbeobachter.
- Der gesonderte kausale Grabenfall rettete einen natürlich arbeitenden Apple-AI5-Arbeiter ohne Ladung nach tatsächlich abgelehnter Wegveröffentlichung; er verließ den Graben und erreichte sein ursprüngliches Arbeitssegmentziel. Beladener Apple-AI7 und vollständiger Arbeits-/Lieferzyklus nach Grabenrettung bleiben offen.
- Ein natürlich beladener Ochse wurde gerettet und lieferte alle zwölf ursprünglichen Steine ab. Die Einschlussgeometrie dieses Falls ist eine ausdrücklich künstliche Wand unter der real arbeitenden Einheit, kein Beleg für jeden natürlichen Baupfad.
- Die Bergfriedmatrix mit korrigierter Fixes-Testkopie bestand mit eigener Rettung aus/an: jeweils48 stehende und49 begonnene Fälle einschließlich Gegenproben und blockiertem Ausgang. Die tatsächlichen Gebäuderichtungen wurden geprüft; beliebige menschliche CastlePlanner-Startauswahlen sind damit nicht vollständig getestet.
- Ein zusätzlicher belegter Aux-Belegungszustand wurde nicht natürlich erreicht. Seine Bereinigung darf nicht als geprüft ausgegeben oder durch künstliche Unitfelder hergestellt werden.
- Letzter festgehaltener Offline-Stand:1538 Regel-/Zustandsprüfungen,263 native Guard-Prüfungen, sechs Produktions-Receipt-Verträge sowie relevante Managed-/Cecil-Verträge. Das ist keine aktuelle Vollabnahme aller Pathfinding-Funktionen.

### Fixes-Korrektur getrennt halten

Der Fehler des geprüften Fixes1.30.0 und seine Korrektur sind unabhängig vom allgemeinen Rettungsalgorithmus belegt: ursprüngliche Läufe046/047 mit aufgestellten echten Reitern scheitern; korrigierte Läufe073/074 bestehen auch bei ausgeschalteter eigener Rettung. Der Fehler betrifft den verzögert ausgewerteten veränderlichen Scanner.

`TemporaryFixes` korrigiert nur verwerfbare Testkopien. Es wird nicht ungeprüft Teil von Pathfinding. Nach einem Upstream-Update den unveränderten Mod und die tatsächlichen Aufrufziele erneut prüfen. Erst bei bestätigter Korrektur den Zusatz samt Aufrufen vollständig entfernen. Keine pauschale Fixes-DLL-Hash-/Versionssperre; relevante Verträge und echte installierte Stubs prüfen. Die Native-Hash-Bindung ist davon getrennt.

## 5. Zielarchitektur und unveränderliche Sicherheitsregeln

Die folgenden Namen beschreiben geplante Verantwortlichkeiten, keine bereits veröffentlichte API.

    Änderungsquelle oder belegter Bewegungsfehler
        -> kleine Menge betroffener Einheiten
        -> kausaler Nachweis / sicherer Kandidat
        -> begrenzte Nachbeobachtung am sicheren Simulationspunkt
        -> profilspezifische Entscheidung
        -> native Rettung und zulässige Befehlsfortsetzung
        -> physischer Erfolgsnachweis oder ausdrückliches Fehlerergebnis

### Kandidat und kausaler Nachweis

Ein Kandidat enthält nur benötigte Werte: Sitzung, Unit-ID und Global-ID, Besitzer, Typ/Bewegungsprofil, Quellort und aktuelle/Next-Belegung, Änderungsursache, relevante Befehls-/Arbeitsidentität und zuvor belegte lokale Erreichbarkeit. Keine nativen Pointer, Spans oder unveränderlich angenommenen Objektreferenzen über verzögerte Arbeit hinweg.

Die Ursache braucht eine eigene Bedeutung und Gültigkeit. Eine Cacheinvalidierung oder lokale Suchrevision ist nicht automatisch ein bestätigter Weltumbau. Verschachtelte Änderungen, Teilkonstruktion und mehrere Quellen derselben Änderung müssen koalesziert werden. Bei weiteren Änderungen darf ein alter Nachweis nicht unbemerkt durch eine nachträglich berechnete Erreichbarkeit ersetzt werden.

### Nachbeobachtung und Grenzen

- Nur konkrete Kandidaten nachbeobachten; kein dauerhaftes Fortschrittsregister aller Einheiten.
- Kandidaten anhand Sitzung und stabiler Identität deduplizieren; neue Ereignisse kontrolliert zusammenführen. Tod, ID-Wiederverwendung, Kartenwechsel und ungültiger Nachweis beenden den Fall.
- Stillstand, Zeitablauf oder wiederholter Suchfehlschlag sind Hinweise, kein Rettungsbeweis. Ein abgelaufenes Budget rechtfertigt keinen Teleport.
- Begrenzte Wiederholungen mit deterministischer Verteilung. Obergrenzen für Kandidaten, Kachel-/Kantenbesuche, Zielsuche und Versuche festlegen und messen. Keine Laufzeit-Millisekunden als MP-entscheidendes Abbruchkriterium.
- Neue Befehle respektieren: keine alte Bewegung, Warteschlange oder Arbeitsabsicht gegen einen inzwischen gültigen neueren Auftrag wiederherstellen.

### Eingriff und Erfolgsbegriff

- Zuerst belegen, ob die Einheit ihren Standort im tatsächlichen Modus regulär verlassen kann. Boden-Komponenten, Kachelflags und Routenfehler sind jeweils nur Teile dieses Nachweises.
- Die Rettung erweitert keine Bewegungsrechte und erlaubt kein Überspringen unverwandter Mauern, geschlossener Tore oder fremder Sperren. Zielorte müssen für die konkrete Einheit aktuell legal und sicher belegbar sein.
- Bei kausal beobachteter Änderung bevorzugt zuvor erreichbare lokale Ausweichfelder. Kein pauschaler Bergfriedteleport.
- Mehrere Rettungen berücksichtigen bereits belegte beziehungsweise im selben Ablauf reservierte Ziele; Auswahl und Verarbeitungsreihenfolge sind deterministisch.
- Native Wegveröffentlichung ist keine harmlose Probe. Im belegten Grabenfall löschte eine fehlgeschlagene Veröffentlichung die Wegbytes trotz alter Flags/Länge. Deshalb keine Diagnose durch mutierende „Testbefehle“ mit angenommener Rückrollbarkeit.
- Begonnene Schritte niemals durch ungeprüftes Nullsetzen von AI, Substep, Portal-, Ziel- oder Belegungsfeldern abbrechen. Benötigte native Bereinigung erst nach vollständigem Zustandsaudit freigeben.
- Nicht nur Rückgabewert oder Zielkoordinaten prüfen: aktuelle Position, alte/neue Belegung, stabile Identität und tatsächliche weitere Bewegung beziehungsweise Arbeit messen.
- Fehler nach einer bereits ausgeführten Versetzung ausdrücklich von Ablehnung vor jedem Eingriff unterscheiden. Keine atomare Rückrollbarkeit behaupten: fehlgeschlagene Auftragsfortsetzung kann eine bereits bewegte Einheit hinterlassen. Nicht blind an den ungültigen Quellort zurücksetzen oder denselben Rettungsaufruf wiederholen.

### Ein Besitzer je Eingriff

Der Testmod und Pathfinding dürfen niemals dieselben nativen Spannen konkurrierend belegen. Vorhandene Pathfinding- und APIShared-Besitzer bleiben maßgeblich. Eine notwendige öffentliche Beobachtung wird dort additiv und fehlerisoliert angeboten, nicht über kopierte Hooks oder funktionale Nutzung einer Diagnose-API.

Originale laufen genau einmal. Beobachtungsfehler dürfen weder Vanilla noch andere Teilnehmer verhindern. Funktionale Eingriffe erfolgen erst an einem dafür auditierten sicheren Punkt. Fixes bleibt überall aktiv, wo unser System den Fall nicht vollständig übernimmt. Bloße Kandidateneignung vor dem Bau ist kein Beleg, dass die spätere eigene Rettung gelingt.

## 6. Arbeitspakete mit Abnahmekriterien

### P1 — Erzeuger- und Vertragsmatrix

- [x] Alle relevanten Quellen aufführen: KI-AIV-Brücke, KI-Mauer-/Gebäudebau, menschlicher Bau einschließlich Spiegel-/Serienpfaden, Editorbau/-gelände, Grabenfortschritt und tatsächlicher Abschluss, Rückbau/Zerstörung sowie externe Bau-APIs. Siehe vier Teilaudits.
- [x] Je Quelle native/verwaltete Erzeuger, effektive Parameter, Footprint, betroffene Nachbarschaft, Thread, Reihenfolge, Abbrüche, Ausnahmen und stabilen Abschluss bis zur späteren Bewegung auditieren; nicht allgemein rettbare Zustände mit konkretem Vertrag abgrenzen.
- [x] Vorhandene Extender-Ereignisse und APIShared-/Pathfinding-Hookbesitzer zuordnen. Post-only-Kachelinvalidierung ausdrücklich von Vorher-/Nachher-Transaktion getrennt.
- [x] Relevante Fixes-Reaktionen und deren Reihenfolge geprüft; ursprüngliche Prävention und Fixtureisolation getrennt, Keep-Reiterrettung bleibt eigenständig.
- [x] Additiven funktionalen Brokervertrag entworfen: Quelle/Invocation, äußerer Mirrorrahmen, effektive Originaldaten, einmaliger Abschluss, Originalfehler, Thread/Lebensdauer und vollständige Callback-/Diagnosefehlerisolation. Siehe `Audits/AUDIT.md` Abschnitt3; noch keine APIänderung.
- [x] Native-/SDK-/Extender-Provenienz und offline relevante Einstiegsspannen einschließlich Dig/Fill-Innenreferenzen belegt. Keine neue Hookveröffentlichung.
- [ ] F1 gezielt korrigieren; danach die drei Fehlergegenbeispiele als erfolgreiche Isolation und Originalerhaltung prüfen.
- [ ] Tatsächliche Live-Spannen erst beim später autorisierten Start prüfen. Die Offline-PE-Analyse ersetzt keinen Live-Fremdpatchnachweis; keine neuen RVA-Adapter ungeprüft aus dem Plan ableiten.

**Abgenommen wenn:** Für jeden anzubindenden Pfad ist belegt, wie betroffene Einheiten vor der Änderung erfasst und nach dem vollständigen Umbau geprüft werden. Nicht belegte Pfade stehen sichtbar als offen in der Abdeckungsmatrix. Ein getesteter Pfad darf dabei bereits inkrementell weitergehen.

### P2 — Kandidatenkern und begrenzte Verarbeitung

- [ ] Bestehende Testmod-Snapshots/Ergebnisse so strukturieren, dass Erkennung, Nachweis, Planung, native Ausführung und Beobachtung getrennte Fehlerpfade besitzen.
- [ ] Kandidatenliste mit Identität, Ursache, Sitzung, Deduplizierung, Ablauf und begrenzten Wiedervorlagen implementieren. Vorherdaten nur so lange wie fachlich nötig halten.
- [ ] Vorhandene Simulationstaktung benutzen; bei leerer Arbeit sofort zurückkehren. Nachbeobachtung vorhandener Kandidaten ist erlaubt, permanentes Scannen aller Einheiten für den neuen Dienst nicht.
- [ ] Bereits vorhandene Pathfinding-Revisions- und Auftragsmuster nach Vertragsprüfung anpassen. Keine Engine-Quellkopie in den Testmod aufnehmen.
- [ ] Reine Verhaltenstests: doppelte/nested Ereignisse, ID-Wiederverwendung, neue Befehle, mehrere Umbauten, Sitzungstausch, Budgeterschöpfung, fehlender Zielort und deterministische Reihenfolge.

**Abgenommen wenn:** Ohne Ereignisse keine Einheiten-/Wegprüfungen; ein Verdachtsfall verursacht begrenzte Arbeit; abgelaufene oder ersetzte Fälle können keine Mutation mehr auslösen.

### P3 — Weitere automatische Bau-/Geländepfade

- [x] Vorhandenen echten KI-AIV-Zugbrückenpilot und dessen No-op-/Off-/On-Belege als Ausgangspunkt dokumentiert.
- [ ] Den Pilot in den gemeinsamen Kandidatenpfad überführen, ohne ursprüngliche Bauaktionen zu unterdrücken oder die bestehende Gegenprobe zu verlieren.
- [ ] In belegter Reihenfolge weitere KI-Bauten und danach Menschen-/Editorpfade anbinden. Keine pauschale Callback-Ausführung je Footprintkachel.
- [ ] Tatsächlichen Grabenabschluss separat anbinden; ein Eintrag/Schreibaufruf allein gilt nicht als fertiger unpassierbarer Graben.
- [ ] Teilbau, abgelehnte Platzierung, unverändertes Gelände, Rückbau und wiederholte Bauversuche als Gegenproben aufnehmen.
- [ ] Bounded Tile-Occupancy-Discovery für aktuelle Betroffene; Next-only-Kandidaten erst nach P5 belastbar ergänzen. Keine versprochene vollständige Erkennung durch die bisherige Current-Kette.

**Abgenommen wenn:** Echte normale Bauvorgänge lösen die Erkennung aus, ohne dass die Fixture ausdrücklich `BeginMutation` oder die Rettung aufrufen muss. Kontrollierte native Bauaufrufe und autonome KI-Abläufe werden im Nachweis getrennt.

### P4 — Soldaten, Besitzer und KI-Gruppen

- [ ] Stehende gewöhnliche Soldaten zuerst über neue automatische Quellen abnehmen: Einzelunit, mehrere Betroffene, tatsächlicher Gruppenführer und anderes Mitglied.
- [ ] KI-Gruppen mit regulärem Marsch-, Angriffs- und gegebenenfalls Patrouillenauftrag prüfen; gesunde Mitglieder müssen weiter funktionieren. Keine universelle Gruppenblockade voraussetzen.
- [ ] Bestehende manuelle Admission-Logik erhalten. Ihre KI-Sperre nicht als beiläufige Rettungskorrektur entfernen. Etwaige allgemeine KI-Gruppenkorrektur als eigenen belegten Fall behandeln.
- [ ] Ursprünglichen Verband bevorzugt beibehalten; fortgesetzter Auftrag darf keine neuere Order, Formation oder Shift-Warteschlange zerstören. `SoldierTribeMoves` nicht ungeprüft zur universellen Fortsetzung machen.
- [ ] Besitzerpolitik festhalten: bisheriger automatischer Pilot verlangt Besitzer = Bauherr. Alliierte, neutrale und feindliche Betroffene sind gesonderte Fälle. Ein globaler Rettungsschalter darf nicht heimlich nur dem lokalen Menschen helfen.
- [ ] Normales Warten, Kampf, befohlenes Halten und unerreichbares Ziel ohne eingeschlossenen Start als zwingende Negativtests.

**Abgenommen wenn:** Physische Rettung und tatsächliche sinnvolle Auftragsfortsetzung bei Mensch und KI belegt sind; das übrige Gruppenverhalten und nicht betroffene Einheiten bleiben erhalten.

### P5 — Begonnene Schritte und Sonderflächen

- [ ] Vollständige Writer-/Consumer-Kette für Current, Next, Interpolation, Verbindungs-/Portalzustand und Belegungslisten je freizugebender Bewegungsfamilie auditieren.
- [ ] Beide Hauptfälle reproduzieren: aktueller Standort wird ungültig; nur das bereits angenommene nächste Feld wird ungültig. Auch Änderung um die Mittelpunktübernahme herum prüfen.
- [ ] Sicheres Warten bis zu einer zulässigen Grenze und eine gegebenenfalls nötige native Bereinigung getrennt qualifizieren. Keine unbewiesene Rückkehr zu Idle erzwingen.
- [ ] Reiter/Large-Profil, Fußsoldaten, Assassinen sowie erhöhte und durch Gebäude verbundene Flächen getrennt abnehmen.
- [ ] Legales Klettern, Treppen, Tore, geschlossene/eroberte Tore und Brückenstellungen gegen falsche Rettungen prüfen. Tatsächliche Richtung/Höhe lesen, einschließlich CastlePlanner-Fällen.
- [ ] Natürliche Current-/Next-Belegung und alte/neue Listen prüfen. Zusätzliche Aux-Fälle nur bei belegtem natürlichem Erzeuger abnehmen, sonst offen ausweisen.

**Abgenommen wenn:** Der ursprüngliche „Einheit läuft während des Baus über das Feld“-Fehler über automatische Quellen zuverlässig behoben wird; weder doppelte Belegung noch zweite Versetzung oder Wiederaufnahme des alten ungültigen Schritts entstehen.

### P6 — Arbeiter, Grabenmodus und Besatzungen

- [ ] Die getesteten Apple-/Ox-/Besatzungsabläufe in dieselben automatischen Quellen integrieren; erfolgreiche direkte Fixture-Aufrufe nicht als automatische Abnahme zählen.
- [ ] Apple-AI5 ohne Ladung als Regression erhalten; beladenen Apple-AI7-Grabenfall und vollständigen anschließenden Ernte-/Lieferzyklus hinzufügen.
- [ ] Weitere Arbeiterfamilien anhand ihrer tatsächlichen Jobzustände qualifizieren. Erzwungenes Wegende darf nicht als aus der Ferne erreichtes Arbeitsziel wirken; insbesondere keine unkontrollierte Übernahme von Soldaten-Stoppregeln.
- [ ] Natürlich beladenen Ochsen erneut über echte Änderung erfassen und vollständige bestehende Ladung am tatsächlichen Lager nachweisen. Keine künstlichen Waren, AI-Zustände oder überlebenden Testgebäude voraussetzen.
- [ ] Gerät mit Besatzung, ursprüngliche Ingenieure, unmittelbares Absitzen und spätere Eigenbewegung getrennt prüfen. Verdeckte/montierte Ingenieure nicht als unabhängige Rettungskandidaten versetzen.
- [ ] Native Grabenbewegung zulassen, wenn sie legal funktioniert. Gesonderte kausale Grabenrettung nur bei vollständig belegtem Modus-/Ausgangs-/Arbeitsvertrag freigeben.

**Abgenommen wenn:** Für jede freigegebene Familie der konkrete Job beziehungsweise die Gerätefunktion nach echter Rettung weiterläuft und Identität, Ladung, Arbeitsplatz und Besatzung erhalten bleiben. Nicht qualifizierte Zustände bleiben sichtbar außerhalb der Freigabe.

### P7 — Bewegungsfehler und fehlende Bauereignisse

- [ ] Abdeckung von `OnUnitMoveHere`, vorhandenen Builder-/Recovery-Besitzern und tatsächlichen Schrittablehnungen vollständig unterscheiden und auditieren. Globale Auftragsbeobachtung ist kein vollständiger Bewegungssensor.
- [ ] Verlässliche Unit-ID/Global-ID, tatsächlichen Besitzer, effektiven Ziel-/Moduskontext und nativen Ausgang einem Aufruf zuordnen. Endpunktgleichheit oder aktuelle UI-Auswahl sind keine Identität.
- [ ] Skipped Original, veränderte Pre-Argumente, fehlendes Post bei Ausnahme und verschachtelte Aufrufe abnehmen. Keine neue instabile LIFO-Kopie neben bestehenden Besitzern.
- [ ] Aus Fehlermeldungen nur Kandidaten erzeugen. Keine Weltmutation in `RouteSearchEvents` oder in einem laufenden fremden Suchkontext.
- [ ] Ohne Vorherdaten erst einen eigenständigen Nachweis für aktuell fehlende legale Ausgänge und einen sicheren Zielort entwickeln. Nicht einfach den kausalen alten-Bank-Nachweis erfinden.
- [ ] Häufig wiederholte Fehler deduplizieren und zeitlich begrenzen. Vor einem verwalteten Callback an einem sehr heißen nativen Pfad dessen Eintrittskosten und ein natives Bedarfs-/Fehlergate prüfen.

**Abgenommen wenn:** Ein Fehler außerhalb der bereits beobachteten Bauquellen erkannt werden kann, ohne dass ein unerreichbares Ziel, Warten oder fremde Routenregeln unerlaubte Teleports auslösen. Ein nicht identifizierbarer Fehler bleibt ohne Eingriff.

### P8 — Sitzungen, Save-Laden, Editor und MP

- [ ] Zentrale Missions-/Editor-Lifecycle-Verträge verwenden; aktive Fälle auf Ende, Ersetzung und Fehlstart ungültig machen. Startup-Cleanup darf die Runtime nicht zerstören.
- [ ] Kartenwechsel, Neustart, Save-Laden und Laden desselben Saves mit wiederkehrenden IDs prüfen. Native Pointer/alte Beweissnapshots nicht über die Sitzung retten.
- [ ] Bewusst entscheiden, welche eigenen Warte-/Fortsetzungsfälle gespeichert werden müssen. Bestehendes `MovementCaseSave`-Schema kompatibel erweitern; keine formatlosen zusätzlichen Serialisierer.
- [ ] Für schon defekte alte Saves optional eine getrennte, einmalige verteilte Bestandsaufnahme oder angeforderte Prüfung entwickeln. Untätige Altfälle ohne Ereignis werden sonst nicht gefunden. Fehlende Vorherdaten und sichere Rückkehrseite bleiben ausdrücklich ein eigener Nachweis.
- [ ] Normalen Editorbau/-gelände und spätere Simulation testen; Editor-Created und Editor-Loaded nicht mit Skirmishstart gleichsetzen.
- [ ] Vor MP-Aktivierung Autorität festlegen: identische native Simulationsereignisse und deterministische Entscheidung oder belegter synchroner Chore-Weg. Keine Mutation nur auf dem Host oder bei jedem Peer zusätzlich zur empfangenen Mutation.
- [ ] Gleiche Einstellungen, Kandidatenordnung, Tick-/Arbeitsbudgets und Zielwahl auf Host/Client prüfen. Keine Wandzeit, Thread-Reihenfolge, lokale UI-Auswahl oder lokale Diagnose-/Suchrevision als gemeinsame Spielentscheidung.
- [ ] Reales Host-/Client-Spiel mit Bau, mehreren gleichzeitigen Rettungen, KI-Gruppen, Arbeitern und anschließendem Save/Laden abnehmen. Bis dahin bleibt der jetzige MP-Ausschluss aktiv.

**Abgenommen wenn:** Keine veraltete/doppelte Rettung über Lifecyclegrenzen und identisches Verhalten in den ausdrücklich freigegebenen Modi belegt ist. Ein lokaler Zweispieler-Ersatz ist keine echte MP-Abnahme.

### P9 — Last, Fehlertoleranz und Kompatibilität

- [ ] Auf großer Karte mit annähernd10.000 Einheiten Leerlauf, wenige Änderungen und Massenbau/gleichzeitige Betroffene vergleichen. Keine Pflicht, hierfür alle Kacheln pro Tick zu betrachten.
- [ ] Zusatzkosten messen: native→verwaltete Übergänge, Kandidaten/Versuche, Kanten-/Kachelbesuche, Queue-Spitze, Allokationen, gesamter Sim-Tick und Lags. Durchschnitt allein reicht nicht.
- [ ] Abwechselnde Vergleichsläufe, gleiche Szenarien und zusätzliche Kosten zu vorhandener Pathfinding-Taktung ausweisen. Keine unbelegte feste Millisekundengarantie in Einstellungen oder Dokumentation.
- [ ] Journalausfall, überlaufende Kandidatenmenge, ungültige Kette, fehlende Capability und Callbackfehler prüfen. Begrenztes Aufschieben/Verwerfen mit sichtbarem Ergebnis; nie stumme Rettung aufgrund verlorener Daten.
- [ ] Allein mit deklarierten Pflichtabhängigkeiten sowie mit Fixes, Pathfinding, BugfixesAndQoL und CastlePlanner testen. Pro Stelle eindeutige Ownership und Live-Spannen vor Veröffentlichung sowie bei Missions-/Editorstart prüfen.
- [ ] Relevante fremde Änderungen ablehnen; harmlose Versions-/DLL-Änderungen ohne Vertragsänderung akzeptieren. Originale und unabhängige Features bleiben aktiv; keine fremden Einstellungen umschreiben.

**Abgenommen wenn:** Keine zusätzliche permanente Vollprüfung aller Einheiten entsteht, Lastspitzen begrenzt sind und Ablehnung/Überlast weder Vanilla noch bestehende Features beschädigen. Release-Diagnose ist klein; temporäre Entwicklungsdiagnose läuft ausschließlich über `Shared.DevDiag`.

### P10 — Überführung nach Pathfinding

- [ ] Abdeckungsmatrix und offene Fälle gemeinsam bewerten. Einen begrenzten MVP nicht als vollständig allgemeine Rettung umbenennen.
- [ ] Rettungskern, native Ausführung, modinterne Policy und nötige allgemeine Beobachtungsschnittstellen mit eindeutigen Besitzern zuordnen. APIShared bleibt unabhängig von Pathfinding-Interna und fremden GUIDs nutzbar.
- [ ] Neue eigene Rettungsoption in Pathfinding konsistent übernehmen. Master-Aus deaktiviert sie; eine ausgeschaltete manuelle Verbesserung, schnelle Einzelsuche oder Gefahrenflucht darf nicht unbeabsichtigt die allgemeine Rettung sperren. Tatsächliche technische Voraussetzungen ausdrücklich in Vertrag/UI darstellen.
- [ ] Globaler Host-/Missionssnapshot, Presets, Locales und Modusfreigaben korrekt einbinden. Keine zusätzliche Einstellung speziell für die temporäre Fixes-Korrektur einführen.
- [ ] Den produktiven Runtimebesitzer genau einmal installieren. Beim Wechsel in einem neuen Spielprozess die eigenständige Runtime des Testmods ablösen; keine parallel aktiven alten Hooks oder ungefragte zweite Fallbackimplementierung behalten.
- [ ] Fixtures und Automationsszenarien bleiben außerhalb des Hauptmods. Der Testmod prüft danach den tatsächlichen Pathfinding-Code über geeignete öffentliche Grenzen beziehungsweise gezielte Test-Reflection.
- [ ] Bestehende Pathfinding-Tests nach tatsächlicher Betroffenheit pflegen und ausführen: manuelle Admission, native Recovery/Reject, Anchor-Integration und Veto, Oberflächen, Warte-/Save-Fälle, Formationsautorität, Topologieveröffentlichung und Gate-Kompatibilität.
- [ ] Anschließend dieselben Rettungsszenarien gegen den integrierten Hauptmod wiederholen; Erfolg des vorherigen eigenständigen Testmods ersetzt diese Prüfung nicht.
- [ ] Erst nach Fertigstellung Versionen konsistent ändern. README-Ergänzung gesondert abstimmen. Paketinstallation, Commit und Veröffentlichung richten sich nach dem dann ausdrücklichen Auftrag.

**Abgenommen wenn:** Pathfinding enthält genau eine getestete Rettungsruntime, der Testmod nur die Tests dafür, und die freigegebenen Modi/Familien/Quellen stimmen mit der Dokumentation überein.

## 7. Verbindliche Abnahmematrix

Für jede relevante Kombination einen Eintrag mit Status, Quelle, tatsächlichem Zustand, Trigger, Originalverhalten, eigenem Verhalten und Beleg führen. Keine bloße Aufzählung potenziell vorhandener Tests.

| Achse | Mindestfälle |
|---|---|
| Ursache | AIV-Brücke, weitere KI-Bauten, Menschen-/Editorbau, Grabenabschluss, Rückbau, Bewegungsfehler, Altsave getrennt |
| Bewegung | Stehend, aktuelle Kachel betroffen, nur Next betroffen, mehrere natürliche Teilschritte vor/nach Mittelpunkt, legale Sonderbewegung |
| Einheit | Fußsoldat, Assassine, vier geprüfte Reitertypen, Arbeiter ohne/mit Ware, Ochse, Gerät samt Ingenieuren; weitere Familien einzeln |
| Befehl | Kein Auftrag, Einzelmove, Gruppenführer/-mitglied, Angriff/Patrouille, Formation, Queue, neuerer Auftrag während Beobachtung, Arbeitsplatz |
| Besitzer | Mensch, KI, mehrere KI; Alliierte/Neutral/Feind nur nach festgelegter Policy |
| Geometrie | Freie Ausweichbank, mehrere Kandidaten, alle Ziele blockiert, enger Einschluss, Höhenwechsel, gültiger Kletterausgang, Tore/Brückenstellungen, tatsächliche Keep-Rotation |
| Zeit / Modi | Sofortiger Bauabschluss, verschachtelte Änderung, mehrere Betroffene, Kartenwechsel, Save/Reload, Editor, echter MP |
| Kompatibilität | Pflichtabhängigkeiten allein, ursprüngliches und gezielt korrigiertes Fixes getrennt, übrige Hauptmods, fremder Hook, relevante/unabhängige Updates |

Ein positiver Test umfasst: dieselbe globale Identität und Gesundheit, korrekte Belegung und keine zweite Versetzung, gültigen Zielort und tatsächliche Bewegung/Arbeit danach. Bei Arbeit zusätzlich reale Waren-/Arbeitsplatzbelege; bei Gruppen Befehls- und Mitgliedschaftsbelege. Negative Tests umfassen No-op, legalen Ausgang, nicht eingeschlossenen Start, unerreichbares Ziel, fehlenden Zielort und unbekannten Zustand.

## 8. Offene Entscheidungen, ohne stille Vorannahmen

| ID | Entscheidung | Bis zur Klärung |
|---|---|---|
| D1 | Besitzerpolitik für Alliierte, Neutral und Feinde bei fremdem Bau | Bisherigen Besitzer=Bauherr-Pilot nicht stillschweigend erweitern |
| D2 | Automatische Reparatur alter Saves oder ausdrücklich angeforderte Prüfung | Neue Ereignisse behandeln; keine versprochene rückwirkende Rettung |
| D3 | Persistenz ausstehender Fälle versus sichere Verwerfung und neue Erkennung | Keine alte Mutation nach Laden wiederholen; notwendige Fortsetzungszustände separat prüfen |
| D4 | Konkrete funktionale öffentliche Brokergrenze und APIShared-Mindestversion | Nach P1-Vertrag entscheiden; keine Diagnose-API funktional missbrauchen |
| D5 | MP-Ausführung und Autorität | Bestehender Testmod bleibt im echten MP gesperrt |
| D6 | Umfang erster produktiver Freigabe und Standardwert der Rettungsoption | Testmod bleibt explizit experimentell und standardmäßig aus |

Technische Details können innerhalb belegter Verträge entschieden werden. Mehrere wesentlich verschiedene Verhaltensoptionen werden vor ihrer Umsetzung konkret mit Folgen und Nachweisen zur Entscheidung vorgelegt; fehlende Antwort ist keine Freigabe.

## 9. Änderungs- und Abnahmeprotokoll

| Datum | Paket | Ergebnis / nächste Arbeit | Nachweise |
|---|---|---|---|
| 17.11.2026 | P0 / Planerstellung | Aktuellen Pathfinding-Quellstand, menschliche/KI-Grenzen, vorhandene Taktung, Hookbesitzer und öffentliche Vertragsgrenzen eingeordnet. Zielmigration ausdrücklich Pathfinding. Nächster Schritt P1. Keine neuen Runtimeänderungen, Builds oder Spieltests für diesen Plan. | Quellen in Abschnitt3; vorhandene Rettungsnachweise in Abschnitt4 |
| 25.11.2026 | Offline-Abnahme des vorhandenen Stands / P1 teilweise | Testmod, kompletter Pathfinding-Offline-Build und ausgewählte API-Verträge PASS. Testmod-Tooltip-Stil korrigiert; Nachprüfung PASS. Sandbox-Testhost-Startfehler durch unveränderte erfolgreiche Läufe außerhalb abgegrenzt. Keine Installation/Spieltests; keine neue allgemeine Rettungsquelle implementiert. Nächster Schritt: relevante Baseline-Provenienz validieren und P1 für die nächste Quelle vollständig schließen. | [OFFLINE-CHECKS.md](OFFLINE-CHECKS.md), `.work/a/rescue-offline/001/result.json` |
| 25.11.2026 | Vollständiger statischer Featureaudit | Bau-/Gelände-/Bewegungs-/Job-/Lifecycleketten und sichere Ausschlüsse geschlossen; alle20 Wirtschaftsrollen und besondere Tunnel-/Climb-/Portalzustände ergänzt. Neue native Bytebelege unabhängig gegengeprüft. F1 durch3 Gegenbeispiele+Kontrolle am Produktionsdispatch reproduziert; keine Runtimekorrektur. Keine Installation/Spieltests. Nächster Schritt F1-Korrektur, dann P2/Brokerimplementierung. | [Audits/AUDIT.md](Audits/AUDIT.md), [F1](Audits/AUDIT-FINDINGS.md), `.work/a/rescue-audit/001/` |

Für spätere Einträge mindestens Datum, tatsächlichen Quell-/Buildstand, geänderte Dateien/Verträge, bestandene und fehlgeschlagene Prüfungen, verbleibende Grenzen sowie den nächsten konkreten Schritt ergänzen. Bei neuen Erkenntnissen diesen Plan anpassen; erledigte technische Annahmen nicht als dauerhafte Sperren konservieren.
