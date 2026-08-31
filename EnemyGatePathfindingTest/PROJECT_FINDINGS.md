# EnemyGatePathfindingTest – bisherige Erkenntnisse

Stand: 31. August 2026
Modversion während der Testphase: `0.1.0`

## Ziel und aktueller Status

Ziel ist, dass die globale Wegfindung feindliche Torhäuser und zugehörige Zugbrücken unabhängig vom Öffnungszustand als geschlossen behandelt. Eine Passage soll nur für Besitzer, Verbündete oder einen eigenen beziehungsweise verbündeten Eroberer möglich sein. Das soll möglichst gemeinsam für KI, menschliche Cursorprüfung und Bewegungsbefehle gelten.

Der derzeitige Build ist teilweise funktional und teilweise diagnostisch:

- Der Fremdtorfilter für unterschiedliche PCL-Verbindungen ist aktiv.
- Ein feindliches Tor, das durch einen nicht verbündeten dritten Spieler erobert wurde, wird nicht fälschlich passierbar.
- Positive Same-PCL-Ergebnisse werden bewusst noch nicht verändert.
- Die weiterhin problematische seitliche Zugbrückenroute wird momentan nur vermessen und protokolliert.

## Bestätigtes Vanilla-Verhalten beim menschlichen Spieler

Die Cursorprüfung und die spätere konkrete Routenwahl müssen getrennt betrachtet werden. Die folgenden Fälle wurden im Karteneditor mit menschlichen Befehlen beobachtet:

### Vollständig eingeschlossener Zielbereich hinter einem feindlichen Tor

Ist ein Bereich vollständig umschlossen und ausschließlich durch ein feindliches Torhaus erreichbar, blockiert Vanilla den Cursor bereits korrekt. Der Bewegungsbefehl wird nicht erteilt.

Das zeigt, dass Vanillas spielerbezogene Erreichbarkeitsprüfung ein feindliches Tor grundsätzlich als nicht verfügbare Verbindung berücksichtigen kann, wenn keine andere globale Verbindung zum Ziel existiert.

### Feindliches Tor in einer langen, aber umgehbaren Mauer

Endet die Mauer weit entfernt und ist der Zielpunkt hinter dem Tor über einen langen Umweg prinzipiell erreichbar, blockiert der Cursor nicht. Das ist für die reine globale Erreichbarkeitsfrage zunächst korrekt: Es existiert irgendein gültiger Weg zum Ziel.

Die anschließende Vanilla-Routenwahl bevorzugt jedoch den vermeintlich kürzeren Weg durch das momentan offene feindliche Tor. Sobald sich die Einheit nähert, schließt das Tor. Die Einheit kann den gewählten Weg dann nicht praktisch ausführen, obwohl ein längerer Weg um die Mauer existiert.

Folgerung: Eine Korrektur ausschließlich des Cursor-Erreichbarkeitstests reicht für Torhäuser nicht aus. Auch die konkrete globale Routenbewertung beziehungsweise der Tile-Path-Builder muss feindliche Torflächen unabhängig vom momentanen Öffnungszustand als geschlossen behandeln. Gleichzeitig darf der Cursor in diesem Beispiel weiterhin einen Befehl erlauben, sofern der lange Umweg tatsächlich erreichbar ist.

### Zugbrücke als einzige Verbindung zu einem sonst unerreichbaren Bereich

Erzeugt das Feld einer heruntergelassenen feindlichen Zugbrücke die einzige Verbindung zu einem ansonsten vollständig unerreichbaren Bereich, blockiert Vanilla den Cursor nicht. Die Zugbrückenfläche verbindet dabei normale Wegflächen innerhalb derselben PCL, sodass die feindliche Torprüfung vollständig umgangen wird.

Folgerung: Bei Zugbrücken ist bereits die globale Erreichbarkeitsentscheidung falsch. Nach dem Schließen existiert im Gegensatz zum langen Mauerbeispiel überhaupt kein alternativer Weg. Dieser Fall muss deshalb sowohl in der Cursorprüfung als auch in der tatsächlichen Routenbildung korrigiert werden.

### Zusammenfassung der benötigten Semantik

- Tor mit realem Umweg: Cursor darf den Befehl erlauben, aber die Route darf nicht durch das feindliche Tor geplant werden.
- Tor ohne Umweg: Cursor muss wie bereits in Vanilla blockieren.
- Zugbrücke als einzige Same-PCL-Verbindung: Cursor muss blockieren und die Route darf die feindliche Brückenfläche nicht verwenden.
- Eigene, verbündete oder passend eroberte Tor-/Brückenverbindungen müssen weiterhin nutzbar bleiben.

## Bestätigte technische Grundlage

- Kanonische Spiel-DLL: installierte `CrusaderDE.dll`
- Geprüfter SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender: `1.42.0`
- Geprüfter Script-Extender-Commit: `171d68e155a8f98c5f8c4ee154d9af154c9a2443`
- Zentrale PCL-Funktion: `GetNextReachablePCLToDestinationForPlayer`, RVA `0xE2610`
- Hostile-Capturer-Vergleich: RVA `0xE2710`
- Vom Script Extender bereits belegter gemeinsamer `MoveHere`-Einstieg: RVA `0x196280`
- Cursorziel-Globals: RVA `0x3A11E2C` und `0x3A11E30`

Alle davon abhängigen Codestellen tragen `UPDATE REVIEW`-Marker. Bei einer Änderung der Spiel-DLL oder des Script Extenders müssen Hash, Signaturen, RVAs, ABI, Structlayout und Eventsemantik erneut geprüft werden. Bei einer nicht eindeutig passenden DLL wird kein Hook installiert.

## Was funktioniert

### Zentraler PCL-Hook und Capturer-Policy

- Die gemeinsam von KI und menschlicher Pfadkontrolle verwendete PCL-Funktion lässt sich eindeutig auflösen und beobachten.
- Vanillas Verhalten bleibt erhalten, wenn die native Prüfung bereits fehlschlägt.
- Besitzer und Verbündete bleiben zugelassen.
- Ein durch den anfragenden Spieler oder dessen Verbündeten erobertes Tor bleibt zugelassen.
- Ein beliebiger nicht verbündeter Eroberer macht ein feindliches Tor nicht passierbar.
- Ungültige Spieler-, Gebäude- oder native Zustände werden fail-open behandelt.

### Sichere Diagnosearchitektur

- Native PCL- und `MoveHere`-Callbacks sammeln ausschließlich primitive Daten in vorallokierten Ringpuffern.
- In diesen Hot Paths finden keine Game-API-Aufrufe, Gebäudescans, Stacktraces, Locks, Stringerzeugung oder Logausgaben statt.
- Game-API-, Unit-, Auswahl-, Tile- und Gebäudelesungen erfolgen verzögert über den persistenten statischen `Application.onBeforeRender`-Callback und höchstens einmal pro Frame.
- Topologiesnapshots werden höchstens viermal pro Sekunde erstellt.
- Alle zehn Sekunden erscheint eine kompakte Zusammenfassung; Detail- und Fehlerlogs sind begrenzt.
- Der Karteneditor beginnt beim ersten gültigen PCL-Query oder `MoveHere` automatisch eine Diagnose-Epoche und ist nicht ausschließlich von `OnStartMap(Post)` abhängig.

### Editorzustände und Gebäude-IDs

- `GetBuildingsAsSpan()` ist nullbasiert; die dazugehörige öffentliche Gebäude-ID lautet `buildingId = buildingIndex + 1`.
- Für `r_GatehouseId` ist dagegen noch nicht bestätigt, welcher ID-Raum im Editorzustand verwendet wird. Der Wert darf nicht ungeprüft als öffentliche lokale Gebäude-ID interpretiert werden.
- `AliveState.NeedsInit` ist im Karteneditor ein temporär aktiver Zustand und muss für Diagnosen ebenso wie `AliveState.IsAlive` berücksichtigt werden.
- Vor dem Lesen eines Zugbrücken-Footprints werden Gebäude-ID, Global-ID, Gatehouse-Verknüpfung und eine begrenzte Gridgröße validiert.
- Ausschlüsse werden getrennt nach Brückenzustand, Gatehouse-ID, Torzustand, Global-ID, Gatehouse-Eintrag, Tür-Tiles, Footprint und inkonsistenter Rücklesung gezählt.

### Cursor- und Befehlsdiagnose

- Die menschliche Cursorprüfung erzeugt sehr viele PCL-Abfragen und nutzt dieselbe zentrale PCL-Funktion.
- `MoveHere` liefert Unit-ID und Ziel-X/Y, findet aber erst nach der Cursor-/PCL-Prüfung statt.
- PCL-Queries werden deshalb bis zu 1500 ms vorgehalten und nachträglich über Spieler, Zielkoordinaten, Ziel-PCL und den nächstliegenden vorherigen positiven Same-PCL-Treffer korreliert.
- `MoveHere`-Rollen werden verzögert aus dem Unit-Besitzer bestimmt. Human-, KI- und Unknown-Werte summieren sich immer zur Gesamtzahl.
- Maximal 32 der 48 menschlichen Detailproben dürfen reine Cursorproben sein; mindestens 16 Plätze bleiben für tatsächlich korrelierte Befehle.

### Bisherige Tests

- Der crashsichere Diagnosebuild lief im Karteneditor bis zum regulären Beenden ohne Diagnosefehler oder verworfene Pufferdatensätze.
- Ein Testlauf erfasste 3773 menschliche PCL-Prüfungen:
  - 3768 Same-PCL-Prüfungen waren positiv;
  - der Capturer-Filter wurde bei allen diesen Same-PCL-Prüfungen umgangen;
  - nur 5 Prüfungen verwendeten unterschiedliche PCLs, und diese waren negativ.
- 13 menschliche `MoveHere`-Befehle wurden erkannt und positiv beendet. Es gab keine verschachtelte PCL-Abfrage innerhalb von `MoveHere`, wodurch die zeitliche Nachkorrelation notwendig wurde.
- Aktueller Build: 76 Policy-Assertions, 0 Compilerwarnungen, 0 Compilerfehler.

Der Editortest vom 31. August 2026 um 19:26 bis 19:27 Uhr prüfte zuerst nur das Torhaus und anschließend die Zugbrücke:

- 2272 menschliche PCL-Prüfungen wurden erfasst.
- 2271 Prüfungen waren positive Same-PCL-Ergebnisse; eine Different-PCL-Prüfung war negativ.
- Der Capturer-Filter wurde bei sämtlichen Prüfungen umgangen. In diesem konkreten Aufbau wurde also kein fremder Tor-Datensatz im bestehenden Filter erreicht.
- 5 positive menschliche `MoveHere`-Befehle wurden beobachtet.
- 4 davon ließen sich mit einer vorherigen positiven Same-PCL-Prüfung korrelieren.
- Ein Befehl scheiterte nur an der Diagnosekorrelation `target-coordinate-mismatch`. Das bestätigt, dass Cursorziel-Globals und endgültige `MoveHere`-Koordinaten nicht bei jedem Befehl identisch sind; die Korrelation darf daher nicht selbst Teil eines funktionalen Fixes werden.
- Vor dem Bau der Zugbrücke wurden erwartungsgemäß keine Brückendatensätze gefunden.
- Nach dem Bau wurde genau eine Zugbrücke im Zustand `NeedsInit` erkannt. Sie passierte den Zustands- und Global-ID-Filter, während der Ausschlusszähler `gateId` dauerhaft auf 1 stieg.
- Dieser Zähler bedeutet, dass `r_GatehouseId` entweder 0, außerhalb des lokalen Gebäude-ID-Bereichs oder anderweitig nicht als gültige lokale Gebäude-ID auflösbar war. Der Rohwert wurde in diesem damaligen Build noch nicht ausgegeben.
- Weil keine Tor-/Brückenkombination aufgebaut werden konnte, blieben `samePclCandidates=0` und die Detailproben leer.
- Es gab keine Diagnosefehler, keine verworfenen Query-/Move-Puffereinträge und keinen Crash. Das Spiel wurde regulär geschlossen.

Der reine Torhausteil dieses Testlaufs wurde noch nicht ausreichend erfasst:

- Die damalige Topologieerfassung begann ausschließlich bei Zugbrücken. Ein eigenständiges Torhaus ohne zugeordnete Brücke erzeugte daher keinen Kandidatendatensatz.
- Die eine negative Different-PCL-Prüfung wurde vom damaligen Deferred-Filter nicht gespeichert, weil nur Same-PCL- und bereits vom Capturer-Filter berührte Abfragen detailliert ausgewertet wurden.
- Deshalb ist aus diesem Torhaustest noch kein belastbarer funktionaler Patchpunkt ableitbar. Das beobachtete Vanilla-Verhalten grenzt die Aufgabe fachlich ein, beweist aber noch nicht, welche konkrete Tile-Path-Funktion das offene Fremdtor als Abkürzung bewertet.

Der folgende Diagnosebuild schließt diese Lücken, ohne Pfadergebnisse zu verändern:

- Lebende Torhäuser werden direkt aus dem nativen Gatehouse-Array erfasst, auch ohne Zugbrücke.
- Ein `NeedsInit`-Tor, das noch keinen Gatehouse-Array-Eintrag besitzt, wird als deutlich markierter Footprint-Fallback erfasst.
- Auch negative Different-PCL-Abfragen werden in den Deferred-Puffer übernommen.
- Different-PCL-Kandidaten werden gegen die Tor-Ein-/Ausgangs-PCLs abgeglichen; beim Editor-Fallback wird nur eine diagnostische räumliche Berührung gemeldet.
- Zugbrückenlogs enthalten den rohen `r_GatehouseId`, ihren Footprint, Rand-PCLs und nach Rechteckdistanz sortierte Torhauskandidaten. Eine räumliche Nähe wird noch nicht als funktionale Verknüpfung verwendet.
- Eine nicht nativ zuordenbare Zugbrücke bleibt als `unlinked-bridge-diagnostic` anhand ihres eigenen Besitzers und ihrer Footprint-/Rand-PCLs mit Same-PCL-Abfragen korrelierbar. Auch dieser Datensatz ist rein diagnostisch.

## Was nicht funktioniert hat oder nicht ausreicht

### Nur Tor-Ein-/Ausgangs-PCLs zu filtern

Eine heruntergelassene Zugbrücke stellt nicht nur die direkte Torverbindung her. Sie verbindet zusätzlich normale benachbarte Wegflächen, über die eine Einheit seitlich am Torhaus vorbeigehen kann. Befinden sich Quelle und Ziel dadurch in derselben PCL, liefert Vanilla sofort positiv zurück, bevor der Besitzer-/Erobererfilter erreicht wird.

Folge: Eine reine Korrektur der Tor-Ein-/Ausgangsverbindung löst den Zugbrückenfehler nicht.

### Pauschales Blockieren von Same-PCL-Ergebnissen

Same-PCL ist sehr häufig und normalerweise legitim. Eine pauschale Ablehnung würde große Teile der normalen Wegfindung beschädigen. Ein späterer Fix muss beweisen, dass der konkrete Pfad die Footprint- oder Randflächen einer feindlichen Tor-/Zugbrückenkombination benötigt.

### Annahme, dass `MoveHere` die PCL-Prüfung umschließt

Der Script-Extender-Event um RVA `0x196280` beobachtet die eigentliche Auftragserteilung. Die Cursor-/PCL-Validierung wurde bereits vorher ausgeführt. Ein ThreadStatic-Kontext um `MoveHere` kann die vorherige PCL-Prüfung deshalb nicht direkt erfassen.

Richtiger Ansatz: getrennte primitive Query- und Move-Puffer und nachträgliche zeitliche Korrelation.

### Zweiter nativer `MoveHere`-Hook

Der Script Extender 1.42.0 detourt RVA `0x196280` bereits. Ein zusätzlicher überlappender nativer Hook wäre konflikt- und crashanfällig. Ausschließlich `UnitR3EventHooks.OnUnitMoveHere` verwenden.

### Game-API-/Topologiescans aus dem nativen PCL-Callback

Der erste Diagnoseansatz startete Gebäudescans noch innerhalb des zurückkehrenden nativen PCL-Detour-Stacks. Im Karteneditor traf dies auf ein Tor im Zustand `NeedsInit` und führte zusammen mit einer falschen Gebäude-ID-Abbildung zu einem nativen Crash.

Die zwei konkreten Ursachen waren:

1. Reentrante Game-API-/Topologiearbeit während einer nativen Gebäudemutation.
2. Off-by-one zwischen dem nullbasierten Buildings-Span und einsbasierten Gebäude-IDs.

Beides darf nicht wieder eingeführt werden.

### Ausschließlich `AliveState.IsAlive` verwenden

Im Editor wurden Tor und Zugbrücke bei der Erstellung als `NeedsInit` gemeldet. Der alte Scanner verwarf sie und protokollierte trotz vorhandener Gebäude dauerhaft `combinations=0`. Für die Diagnose müssen beide aktiven Zustände gelten; zerstörte oder andere inkonsistente Zustände bleiben ausgeschlossen.

Der Test vom 31. August 2026 bestätigt, dass diese Änderung funktioniert: Die neue Zugbrücke wurde im Zustand `NeedsInit` gescannt und erst in der nachfolgenden Gatehouse-ID-Prüfung verworfen.

### `r_GatehouseId` ungeprüft als lokale Gebäude-ID verwenden

Der aktuelle Editortest zeigt, dass diese Annahme zumindest für die dort erstellte Zugbrücke nicht ausreicht. Obwohl Torhaus 1 und Zugbrücke 2 vorhanden waren, konnte der gespeicherte `r_GatehouseId` nicht als gültige lokale Gebäude-ID aufgelöst werden.

Vor der nächsten fachlichen Korrektur müssen der rohe `r_GatehouseId`-Wert sowie mögliche alternative Zuordnungen protokolliert werden. Zu prüfen sind insbesondere 0 als fehlende Verknüpfung, ein globaler statt lokaler ID-Raum und eine räumliche Zuordnung über Gatehouse-/Zugbrücken-Tiles. Bis diese Semantik bestätigt ist, darf der Wert nicht zur funktionalen Blockierung verwendet werden.

Der folgende Diagnosebuild gibt diese Informationen als `orphanBridge` aus. `spatialGateCandidates` ist dabei ausschließlich ein Messwert und kein stiller Ersatz für eine bestätigte native Beziehung.

### Zu grobe Probendeduplizierung

Die erste Deduplizierung verwendete im Wesentlichen nur Spieler, PCL und Modus. Dadurch verbrauchte eine frühe bedeutungslose Prüfung bei Cursor `0/0` den Schlüssel, und der spätere Ritterbefehl wurde nicht mehr im Detail ausgegeben.

Die Deduplizierung muss Cursorziel, Auswahl-Signatur, Tor, Zugbrücke, Modus und Ursprung einbeziehen und Plätze für korrelierte Befehle reservieren.

### Unbegrenztes Logging im KI-Test

Ein normaler, vorgespulter KI-Test erzeugt sehr viele PCL-Abfragen und Befehle. Logs oder Strings pro Query würden das Spiel messbar ausbremsen und den Test verfälschen. Im Hot Path daher nur Zähler und Ringpuffer; Details ausschließlich dedupliziert und begrenzt.

## Empfohlener weiterer Ablauf

1. Zuerst ein feindliches Torhaus ohne Zugbrücke testen: einmal vollständig eingeschlossen und einmal mit einem langen realen Umweg um die Mauer.
2. Danach am selben oder einem klar identifizierbaren Tor eine seitlich passierbare heruntergelassene Zugbrücke bauen und erneut hinter die Brücke klicken.
3. Im Log prüfen:
   - `Gate/drawbridge topology changed` mit einem `standalone-gate`- oder ausdrücklich als Fallback bezeichneten Torhausdatensatz;
   - beim reinen Torhaustest `differentPclGate`, `pclResult`, Ein-/Ausgangs-PCLs und den tatsächlichen Cursor-/MoveHere-Verlauf;
   - Gate-/Bridge-ID, Besitzer, Eroberer, Zustände, Footprint und Rand-PCLs;
   - `callerClass=cursor-to-MoveHere`;
   - einen positiven Same-PCL-Treffer und einen korrelierten positiven `MoveHere`-Befehl;
   - `pendingDropped(query=0,move=0)` und keine Diagnosefehler.
   - falls die Brückenkombination erneut an `gateId` scheitert: `orphanBridge`, den rohen `r_GatehouseId`-Wert und `spatialGateCandidates` auswerten, bevor eine Policyentscheidung getroffen wird.
4. Vergleichsfälle mit eigenem, verbündetem, selbst/verbündet erobertem und von einem dritten Spieler erobertem Tor testen.
5. Danach mehrere Minuten normales Spiel stark vorspulen. KI-PCL-Abfragen und Befehle müssen steigen, ohne Logflut oder merkliche Blockade.
6. Erst anhand der bestätigten Footprint-/Randdaten den eigentlichen Same-PCL-Fix entwerfen. Keine pauschale Same-PCL-Sperre einbauen.

Für tieferliegende globale Tile-Wegfindung ist `MoveMoatTest` eine hilfreiche lokale Referenz. Dort wurden unter anderem der zentrale Movement Planner, Cursor-Reachability und der Tile-Path-Builder untersucht. Diese Hooks sind aber nicht automatisch die richtige Lösung und dürfen nicht überlappend installiert werden, wenn der andere Testmod aktiv ist.

## Logauswertung

Das BepInEx-Log ist auf Append gestellt. Für einen Test immer ab der letzten Zeile

`[Message:   BepInEx] BepInEx 5.4.23.2 - Stronghold Crusader Definitive Edition`

auswerten. Die Uhrzeit dieser BepInEx-Startzeile ist unzuverlässig; maßgeblich sind die Millisekunden-Zeitstempel der eigenen Modlogs.

Wichtige Zusammenfassungsfelder:

- `queries`, aufgeteilt in `human`, `ai`, `unknown`
- `same`, `different`, `positive`, `negative`
- `capturerFilter(reached=..., bypassed=...)`
- `candidates(samePcl=...,differentPclGate=...)`
- `MoveHere` mit Rollen, Returnwerten, `correlated` und Miss-Gründen
- `topologyRecords` mit akzeptierten und getrennt verworfenen Datensätzen
- `pendingDropped(query=...,move=...)`
- `errors(query=...,snapshot=...,sample=...)`

## Sicherheits- und Projektregeln

- README-Dateien während der Diagnosephase nicht ändern.
- Version `0.1.0` bis zum final bestätigten Fix beibehalten.
- Andere Workspace-Mods nicht als Laufzeitabhängigkeit voraussetzen.
- Keine überlappenden nativen Hooks installieren.
- Native Fehler immer fail-open behandeln und Vanilla-Rückgabewerte unverändert lassen.
- Nach Codeänderungen zuerst sämtliche Audits durchführen und danach `build.bat` genau einmal direkt und erhöht ausführen.
- Alle Textdateien im Mod verwenden CRLF.
