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

- `GetBuildingsAsSpan()` ist nullbasiert, öffentliche Gebäude-IDs und `r_GatehouseId` sind dagegen einsbasiert. Die korrekte Abbildung lautet `buildingId = buildingIndex + 1`.
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
- Aktueller Build: 64 Policy-Assertions, 0 Compilerwarnungen, 0 Compilerfehler.

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

### Zu grobe Probendeduplizierung

Die erste Deduplizierung verwendete im Wesentlichen nur Spieler, PCL und Modus. Dadurch verbrauchte eine frühe bedeutungslose Prüfung bei Cursor `0/0` den Schlüssel, und der spätere Ritterbefehl wurde nicht mehr im Detail ausgegeben.

Die Deduplizierung muss Cursorziel, Auswahl-Signatur, Tor, Zugbrücke, Modus und Ursprung einbeziehen und Plätze für korrelierte Befehle reservieren.

### Unbegrenztes Logging im KI-Test

Ein normaler, vorgespulter KI-Test erzeugt sehr viele PCL-Abfragen und Befehle. Logs oder Strings pro Query würden das Spiel messbar ausbremsen und den Test verfälschen. Im Hot Path daher nur Zähler und Ringpuffer; Details ausschließlich dedupliziert und begrenzt.

## Empfohlener weiterer Ablauf

1. Zuerst im Karteneditor ein feindliches Tor mit seitlich passierbarer heruntergelassener Zugbrücke bauen.
2. Eine eigene Einheit auswählen und einen Zielpunkt hinter der Brücke anklicken.
3. Im Log prüfen:
   - `Gate/drawbridge topology changed` mit `combinations=1`, oder einen eindeutigen Ausschlusszähler;
   - Gate-/Bridge-ID, Besitzer, Eroberer, Zustände, Footprint und Rand-PCLs;
   - `callerClass=cursor-to-MoveHere`;
   - einen positiven Same-PCL-Treffer und einen korrelierten positiven `MoveHere`-Befehl;
   - `pendingDropped(query=0,move=0)` und keine Diagnosefehler.
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
- `samePclCandidates`
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
