# EnemyGatePathfindingTest – konsolidierte Erkenntnisse

Stand: 11. September 2026

## Ziel und aktueller Umfang

Der Testmod verhindert, dass eine PCL-Route ein feindliches Tor oder dessen Zugbrücke allein wegen einer Eroberung durch einen fremden dritten Spieler als zugänglich behandelt. Besitzer, Verbündete sowie der eigene oder ein verbündeter Eroberer behalten Vanillas Zugriff. Unsichere oder veraltete Snapshots führen immer zu Vanilla-Verhalten (fail-open).

Zusätzlich prüft der menschliche Bewegungscursor positive Same-PCL-Ergebnisse gegen unveränderliche Tor-/Brücken-Snapshots und das native Richtungsraster. Eine Route, die nur durch ein blockiertes feindliches Tor führt, wird verworfen; ein tatsächlich offener Umweg bleibt gültig.

Same-PCL-KI-Routen und cursorlose Befehle verwenden in der isolierten Testkonfiguration nun Vanillas Suche mit einer querylokalen Gate-Richtungsmaske. Es gibt keine verwaltete Ersatzsuche und keinen globalen Direction-Grid-Writer. Bei nicht belegtem Spieler-, Geometrie- oder Native-Vertrag bleibt Vanilla unverändert (fail-open).

## Referenzumgebung

- Kanonische `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender 2.5.0: Commit `5f02af6d074af7c741ebdaaccb48add39eba1bf4`
- RedBird.X64 1.1.0
- Modversion während des Tests: `0.1.3`

Der Runtime-Mod verweigert die Installation bei einem abweichenden nativen Hash oder einer abweichenden Bytefolge. Die Projektdatei verwendet standardmäßig ausschließlich die installierte Extender-Assembly unter `BepInEx\plugins\000shcdese`; ein anderer Pfad muss ausdrücklich über `ExtenderDir` beziehungsweise `SHCDESE_EXTENDER_DIR` gesetzt werden.

## Crashreport vom 11. September 2026

Der reproduzierte echte Crashdump `SHCDESE-crash-2026-09-11-13-04-19...` zeigt eine Access Violation bei `CrusaderDE.dll+0xE271D`. Der fehlerhafte Schreibzugriff verwendete ein beschädigtes `R11`.

Ursache war nicht die Snapshot-Policy, sondern eine falsche Annahme über RedBirds Hooklänge. Die alten Capturer-Hooks starteten bei `0xE2710` und `0xE302F` mit einer angeforderten Länge von neun Byte. RedBird.X64 1.1.0 behandelt `HookSize` jedoch als Mindestlänge, verdrängt mindestens 14 Byte und rundet auf vollständige Instruktionen auf. Der erste Hook verdrängte dadurch 21 Byte bis `0xE2725`.

Vanilla besitzt einen Sprung von `0xE2703` nach `0xE271B`. Dieses Ziel lag mitten im tatsächlich überschriebenen Hookbereich. Der Sprung konnte damit den Hookeinstieg umgehen und in die von RedBird überschriebenen Bytes eintreten. Das erklärt die Instruktionskorruption und den Crash bei `0xE271D`. Der zweite alte Hook hatte denselben strukturellen Fehler.

## Sichere Hookverträge

### PCL-Graph-Capturer

- Vorgängersprung: `0xE2703`
- Hookstart: `0xE2705`
- Verdrängter Bereich: `[0xE2705, 0xE2719)`, exakt 20 Byte
- Instruktionen: `MOVSXD`, `IMUL`, `CMP`
- Nachfolgendes `JE`: `0xE2719`
- Vanillas erlaubtes Sprungziel: `0xE271B`, außerhalb des Hookbereichs

### Builder-Precheck-Capturer

- Vorgängersprung: `0xE3022`
- Hookstart: `0xE3024`
- Verdrängter Bereich: `[0xE3024, 0xE3038)`, exakt 20 Byte
- Instruktionen: `MOVSXD`, `IMUL`, `CMP`
- Nachfolgendes `JE`: `0xE3038`
- Vanillas erlaubtes Sprungziel: `0xE303A`, außerhalb des Hookbereichs

Beide Context-Hooks verwenden `BeforeCallback`: RedBird führt zuerst den vollständigen verdrängten Block aus, danach passt der Callback ausschließlich das Zero Flag an, und erst anschließend erreicht Vanilla sein unverdrängtes `JE`. Die vollständige Registermaske bleibt aktiv. In beiden geprüften Blöcken existieren keine live XMM-/SIMD-Werte.

### RedBird-Flagkorrektur nach dem Stabilitätslauf

Der stabile Kartenlauf nach dem Crashfix zeigte 2.542.230 Capturer-Aufrufe, aber ebenso viele Fail-open-Entscheidungen. Die Gatesnapshots waren fehlerfrei; die Ursache lag erneut im tatsächlichen RedBird-Stub: Beim `BeforeCallback` werden die verdrängten Instruktionen zwar zuerst ausgeführt, doch `ContextAssemblyGenerator` verändert anschließend mit seiner Stackreservierung die Flags, bevor `pushfq` den Kontext sichert. `X64SmartCPUContext.Rflags` enthält im Callback deshalb nicht zuverlässig das Ergebnis des verdrängten `CMP`.

Der Callback liest nun den von Vanilla unmittelbar zuvor erfolgreich gelesenen Capture-Tabellenwert ein zweites Mal aus den exakten Operanden und rekonstruiert das ursprüngliche ZF:

- PCL-Graph: `word[RAX + RDX + 0x64CCED2] == 0`
- Builder-Precheck: `word[R13 + RDX + 0x64CCED2] == AX`

Das rekonstruierte ZF wird vor jeder normalen Rückkehr wiederhergestellt. Bei einer Exception nach erfolgreicher Rekonstruktion wird ebenfalls Vanillas ursprüngliches Ergebnis wiederhergestellt. Nur eine bestätigte, fremde Eroberung darf ZF anschließend auf »Verbindung ausschließen« setzen. Der Snapshot vergleicht den echten nativen Capture-Wert direkt mit seinem unveränderlichen Record; ein indirekter Vergleich über die beschädigten Callback-Flags existiert nicht mehr.

Die Laufzeitstatistik trennt erwartete Durchleitungen jetzt nach `untracked`, `invalidPlayer`, `recordIdMismatch`, `ownerMismatch`, `captureMismatch` und `exception`. Ein einziger Sammelzähler, der normale Nicht-Tor-Verbindungen wie echte Snapshotfehler aussehen lässt, wurde entfernt.

Vor der Hooktransaktion werden Hash, exakte Bytefolgen, Start-/Endadressen sowie Vorgänger- und Nachfolgesprünge validiert. Zusätzlich wird mit der installierten RedBird-Implementierung ein unveröffentlichter Probe-Hook erzeugt. Nur `DisplacedByteCount == 20` ist zulässig. Nach dem atomaren Commit wird die tatsächliche Länge erneut geprüft; eine Abweichung rollt den noch nicht veröffentlichten Initialisierungskandidaten vollständig zurück.

### Cursorfilter

Der Cursor-Hook bleibt bei `0x8F1C4`. Sein vollständiger Block ist exakt 14 Byte lang (`TEST`, `LEA`, `MOV`). Bytevertrag und RedBird-Verdrängung müssen exakt 14 Byte ergeben. Die Registermaske ist vollständig; im geprüften Block sind keine live XMM-/SIMD-Werte vorhanden. Der Callback arbeitet nur mit unveränderlichen Snapshots und einem read-only Zugriff auf das native Richtungsraster.

## Runtime- und Datenmodell

- `EnemyGatePathfindingRuntime` besitzt die beiden Capturer-Hooks und veröffentlicht sich erst nach erfolgreicher Initialisierung.
- `GateTopologySnapshotProvider` erzeugt außerhalb nativer Callbacks unveränderliche Gate-Zugriffs- und Cursor-Policy-Snapshots. Der kleine Access-Fingerprint wird pro gerendertem Frame ohne Snapshot-Allokation geprüft und nur bei Änderungen neu veröffentlicht. Die teure Tile-/Footprint-Topologie wird bei Signaturänderungen sofort und ansonsten höchstens einmal pro Sekunde als Sicherheitsabgleich neu aufgebaut.
- `CursorGateRouteFilter` führt die begrenzte Cursor-BFS aus, ohne das globale Richtungsraster zu verändern.
- Prozessweit benötigte Runtimeobjekte, Logger, Delegates und Eventabonnements sind statisch verwurzelt.
- Kartenwechsel beenden nur Diagnoseepochen und leeren Snapshots. Es gibt keinen normalen Dispose-/Unhook-Pfad.
- JSON wird weder gelesen noch geschrieben und ist keine Runtime-Abhängigkeit.
- Gebäudestatus werden direkt mit `AliveState.NeedsInit` und `AliveState.IsAlive` geprüft; Gebäudetypen verwenden die bestätigten `eStructs`-Symbole.

Entfernt wurden die nicht mehr benötigten Query- und MoveHere-Ringpuffer, zeitliche Korrelation, Callerklassifikation, Builderdiagnostik und Routendecodierung. Sie werden nicht als Fallback mitgeführt.

## Same-PCL-Audit und verbleibendes Sicherheits-Gate

`0xF4930` ist der gemeinsame Dispatcher der geprüften Routenaufträge und erhält die Query-Spieler-ID als zweites Argument. Er verwendet sechs primäre Suchvarianten und optional den Nachlauf `0xDB650`:

- `0xD9C40`: Direction-Grid-Read bei `0xD9EA6`
- `0xDA590`: Direction-Grid-Read bei `0xDA783`
- `0xDAAC0`: Direction-Grid-Read bei `0xDACB2`
- `0xDAFD0`: Direction-Grid-Read bei `0xDB242`
- `0xF3060`: Direction-Grid-Read bei `0xF31A8`
- `0xF32B0`: Direction-Grid-Read bei `0xF33F5`
- bedingter Nachlauf `0xDB650`: vier direkt konsumierte Grid-Tests bei `0xDB860`, `0xDB950`, `0xDBA3F` und `0xDBB2F`

Damit existieren zehn Hot-Path-Stellen. Mehrere laden beziehungsweise testen das Grid unmittelbar vor einem bedingten Sprung. RedBirds Mindestverdrängung von 14 Byte umfasst dort bereits den konsumierenden Sprung: `BeforeCallback` wäre zu spät, während `AfterCallback` vor dem eigentlichen Load keine Ergebnisänderung erlaubt. Ein Managed-Context-Callback an jedem expandierten Suchknoten wäre außerdem ein nicht vertretbarer Hot-Path-Übergang.

Die zehn Stellen werden deshalb durch reine native Iced-/RedBird-Adapter ergänzt. Ihre tatsächlichen Verdrängungslängen sind `14/18/18/17/15/14/17/17/17/17` Byte. Die letzten vier Hooks beginnen bewusst an den Produzenten `0xDB857`, `0xDB947`, `0xDBA36` und `0xDBB26`, unmittelbar vor den dokumentierten Konsumenten `0xDB860`, `0xDB950`, `0xDBA3F` und `0xDBB2F`. So bleibt jeder Adapter auf einer vollständigen Basic-Block-Grenze und maskiert Vanillas geladenes Richtungsbyte vor dessen Originaltest. Nachfolgende Originalinstruktionen stellen die benötigten Flags wieder her; Stack und alle benutzten Scratchregister werden symmetrisch gesichert. Kein Knoten wechselt in Managed-Code.

Ein fester 64-Slot-Pool wird über die Windows-x64-Thread-ID aus `GS:[0x48]` adressiert. Der verwaltete Funktionsscope bindet einen unveränderlichen nativen Maskensnapshot; verschachtelte Scopes speichern und restaurieren ihren Vorgänger. Threadslotkollisionen und ungültige Spieler sind fail-open und werden gezählt. Alte native Snapshotpuffer werden erst freigegeben, wenn kein gebundener Leser mehr existiert. Spielerlose Wartungssuchen erhalten keinen Context und bleiben unverändert.

## Abnahme

Die Maschinentests prüfen die beiden 20-Byte-Blöcke, ihre exklusiven Endadressen, umgebenden Sprünge und eine absichtlich mutierte Bytefolge. Insbesondere liegen `0xE271B` und `0xE303A` außerhalb der Hookspannen. Zusätzlich prüfen sie beide CMP-Rekonstruktionen, Setzen und Löschen von ZF ohne Veränderung anderer Flags sowie die getrennten Snapshot-Fehlerursachen. Policytests decken Besitzer, Verbündete, eigenen/verbündeten Eroberer, fremden Eroberer, ungültige Snapshots und die Cursor-BFS ab.

Für den noch ausstehenden Laufzeittest:

1. Karte mit feindlichem Tor laden und mehrere Minuten vorspulen.
2. Sicherstellen, dass kein neuer echter Crashdump entsteht.
3. Im Log `pclGraphCapturerFilter=0xE2705`, `builderPrecheckCapturerFilter=0xE3024` und jeweils `displaced=20` bestätigen.
4. Fehlerfreie Snapshot-Aktualisierungen prüfen.
5. Different-PCL-Zugriff für Besitzer, Verbündete und verbündete Eroberer sowie die Sperre für einen fremden Eroberer prüfen.
6. Beim Cursor eine ausschließlich durch das feindliche Tor beziehungsweise die Zugbrücke führende Same-PCL-Route blockieren und einen echten Umweg erlauben lassen.

## Ein-Lauf-Diagnose nach dem Lauf ab 14:07 Uhr

Der Lauf ab 14:07 Uhr blieb nach der Hookinstallation und während der geladenen Karte ohne echten neuen Crashdump. Beide Capturer-Hooks meldeten 20 verdrängte Byte, der Cursor-Hook 14 Byte. Der Cursor prüfte 74 positive Vanilla-Ergebnisse ohne Fehler, beobachtete in diesem Szenario jedoch nur erlaubte Routen. Eine belastbare Capturer-Zusammenfassung fehlte, weil der Script Extender beim beobachteten Unload zwar die `Pre`-Benachrichtigung auslöste, der native Unload-Aufruf aber nicht zuverlässig bis zur modseitig abonnierten `Post`-Phase zurückkehrte.

Der nachfolgende Spielstart ab 14:12 Uhr ist kein Modtest: In diesem Prozess wurde `EnemyGatePathfindingTest` nicht geladen. Der zugehörige etwa 47 MB große Dump entstand bereits vor einer möglichen Plugininitialisierung und entspricht dem bekannten Script-Extender-/Mono-Fehlalarm.

Die Diagnose verwendet deshalb nun `OnUnloadMap(Pre)` und erzeugt außerdem alle zehn Sekunden einen zentralen Checkpoint. Er enthält Gesamtwerte und Intervall-Deltas beider Capturer-Stellen, semantische Zugriffsklassen, ursprüngliches und finales ZF, aktive Query-Spieler, Snapshotzustand sowie Cursor-BFS-Ergebnisse, Laufzeit und Fail-open-Ursachen. Pro Hookstelle und Ergebnisklasse wird genau das erste primitive Beispiel im Callback allokationsfrei festgehalten und erst im Deferred-Pfad formatiert. Nicht beobachtete Sollfälle werden ausdrücklich als `not-observed` markiert.

Access- und Route-Policy-Fingerprints sind getrennt. Insbesondere beeinflussen dynamische Zustände wie `IsOpen`, PCL, Walkability, Tileflags und Gate-Path nicht mehr den Route-Policy-Fingerprint oder den Cursorcache. Relevant bleiben nur Gate-/Bridge-Identität, Footprint-Tiles und die Blockiermasken der Spieler. Der sekündliche Sicherheitsabgleich darf daher unveränderte Policies nicht mehr neu veröffentlichen. Access- und Topologieänderungen werden kompakt mit betroffenen Building-IDs, Besitzer-/Capturewerten, Masken, Bounds und Footprint-Tiles protokolliert; der frühere vollständige Perimeter-Tile-Dump entfällt.

## Stabilitäts- und Abdeckungslauf ab 14:36 Uhr

Der Kartenlauf ab 14:39:00 dauerte ungefähr 133 Sekunden und endete sauber über `OnUnloadMap(Pre)`. Der Pluginstart meldete erneut die erwarteten Hookspannen von 20, 20 und 14 Byte. Nach Installation der Hooks und Kartenstart entstand kein Crashdump. Der ungefähr 47 MB große Dump um 14:36:52 lag vor dem Pluginstart um 14:36:57 und bleibt der bekannte Extender-/Mono-Fehlalarm.

Die Capturer-Hooks liefen ohne Callback- oder Snapshotfehler: der PCL-Pfad 5.756.930-mal, der Builder-Precheck 70.268-mal. Sämtliche gültigen Entscheidungen betrafen nicht eroberte Tore; vier PCL-Aufrufe trafen während einer Snapshottransition kurzzeitig keinen veröffentlichten Record und blieben erwartungsgemäß fail-open. Kein Snapshot enthielt `captured != 0`, weshalb eigene, verbündete und fremde Eroberer in diesem Lauf nicht funktional beurteilt werden können.

`PreserveOwner` und `PreserveOwnerAlly` sind an diesen beiden Hookstellen keine erwartbaren Laufzeitfälle: Sowohl `0xE2610` als auch `0xE2F60` prüfen Besitzer beziehungsweise Besitzerallianz vor dem gehookten Capture-`CMP` und springen bei Erfolg am Hook vorbei. Die defensive Snapshotpolicy bleibt getestet, die Laufzeitabnahme kennzeichnet diese Kategorien aber als `NOT_APPLICABLE` statt als fehlende Abdeckung.

Der Cursor prüfte 888 positive Vanilla-Ergebnisse, davon 248 frische BFS-Läufe und 640 Cachetreffer, ohne Fehler. Alle frischen Suchen waren erreichbar; keine Suche traf ein gesperrtes Gate-/Bridge-Tile. Damit sind Stabilität und der erlaubte Normalpfad belegt, nicht aber Cursorblockade oder ein unter Berücksichtigung gesperrter Tiles gefundener Umweg.

## Präzisierte Ein-Lauf-Auswertung

Live-Policy und Diagnosehistorie sind nun getrennt. Ein Tick darf die veröffentlichte Policy weiterhin sofort fail-open leeren, verwirft aber nicht mehr den letzten stabilen Snapshot. Diffs nach einer Transition zeigen dadurch nur wirklich veränderte Building-IDs. Eine frisch geprüfte, semantisch identische Policy wird nach dem kurzen Fail-open-Fenster erneut veröffentlicht, ohne als Änderung gezählt zu werden. Änderungen ausschließlich am rohen Scan-Fingerprint werden separat als unterdrückt gezählt.

Die Kartenepoche merkt sich Spitzenwerte für Records, Eroberungen und blockierte Spieler-/Gate-Paare sowie Capture- und Recapture-Übergänge, beobachtete Zugbrücken und Cursorläufe mit tatsächlicher Begegnung eines gesperrten Tiles. Der finale Checkpoint enthält eine maschinenlesbare `Enemy-gate acceptance verdict`-Zeile. Jede Abnahmekategorie verwendet ausschließlich `PASS`, `FAIL`, `NOT_OBSERVED` oder `NOT_APPLICABLE`; transiente `UntrackedConnection`-Aufrufe werden separat ausgewiesen und lösen allein keinen Integritätsfehler aus.

Topologieänderungen protokollieren keine vollständigen Footprintlisten mehr. Bounds, Tileanzahl, kleinste/größte Tile-ID und ein stabiler Footprinthash reichen zur Zuordnung; die erste Liste akzeptierter Entitäten wird nicht zusätzlich im Detailblock dupliziert. Mikrosekundenwerte verwenden invariant einen Dezimalpunkt.

## Abgleich mit Native-Baseline und vorhandenen Workspace-Pfaden

`CURRENT.json` bestätigt weiterhin exakt den kanonischen Hash `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Das Evidenzpaket für `0xE2610` ist `confirmed` und zeigt den vorgelagerten Besitzer-/Allianztest sowie den Capture-Tabellenzugriff. `0xE2F60` besitzt dieselbe relevante Kandidatenbedingung, ist semantisch aber weiterhin nur `candidate`; die Versionszuordnung zum vorherigen Build ist `confirmed`. Die Baseline bestätigt außerdem `0xF4930` als gemeinsamen Aufrufer des alternativen Suchpfads `0xDB650` und `0xDB650` als Leser des nativen Richtungsrasters `0x51890D0`.

Die KI-Abrisskorrektur in `BugfixesAndQoL` enthält bereits eine getestete PCL-Portal-BFS. Sie verwendet die drei öffentlichen Felder `PathConnectionRecord.r_PathComponentA/B/C`, filtert Portale nach Besitzerallianz und rekonstruiert die konkret benutzte Gatefolge. Diese Logik eignet sich als Makro-Topologiediagnose und als Testorakel für Different-PCL-Fälle. Die Capturer-Erstbeispiele dieses Testmods protokollieren deshalb nun ebenfalls alle drei nativen Portal-PCLs.

Für Same-PCL existiert im Workspace bereits wesentlich mehr verwertbare Infrastruktur als zunächst angenommen. `FriendlyMoatMovementRuntime` validiert und detourt `0xF4930` atomar, bindet Aufträge über `[ThreadStatic]`-Scopes an Unit, Spieler und Ziel, prüft native Ausgabepuffer und publiziert nur vollständig auditierte Ersatzrouten. `WeightedMoatRoutePlanner` und `MoatSearchKernel` bilden native Richtungs-, Höhen-, Belegungs- und Sonderstrukturregeln nach und können echte Umwege erzeugen. Diese Komponenten sind derzeit stark an die Moat-Policy gekoppelt, ihre Such- und Publikationsverträge sind aber die geeignete Grundlage für einen generalisierten Blocked-Tile-Filter.

Ein zweiter Funktionsdetour auf `0xF4930` im Testmod wäre nicht zulässig, sobald `BugfixesAndQoL_Serp` geladen ist. Die aktuelle Inline-Instrumentierung bei `0xE2705` liegt dagegen außerhalb des von dessen `0xE2610`-Einstiegsdetour überschriebenen Prologs; die zukünftige Same-PCL-Lösung muss dennoch einen einzigen Hookbesitzer haben. Empfohlen ist, den generischen Tile-Suchkern und den geprüften Publikationsvertrag in eine gemeinsam nutzbare interne Komponente zu extrahieren und die produktive Enemy-Gate-Policy in den bereits zentralen `BugfixesAndQoL`-Movement-Detour einzuhängen. Der Testmod bleibt dabei der isolierte Diagnose- und Policyprüfer und veröffentlicht keinen konkurrierenden `0xF4930`-Hook.

Der Callgraph präzisiert außerdem den erforderlichen Umfang: `0xF4930` hat in der Baseline nur die direkten Caller `0x18E1E0` und `0x196280`, während `0xDB650` zusätzlich von `0x8C5F0`, `0xCF020`, `0x123090`, `0x1232E0` und `0x195E30` aufgerufen wird. `BugfixesAndQoL` behandelt darüber hinaus die Angriffszielsuche `0xDBC60`, die Gebäudeannäherung `0xDA020`, den Gebäudekandidatenverbraucher `0x123090` sowie Cursor-, Gruppen- und Post-Combat-Einstiege separat. Eine ausschließlich um `0xF4930` gelegte Lösung würde daher nicht das vollständige KI-/cursorlose Modziel abdecken. Die produktive Umsetzung muss diese bereits kartierten Consumer entweder über den vorhandenen zentralen Hookbesitzer einbeziehen oder für jeden ausgelassenen Pfad ausdrücklich fail-open bleiben.

## Lauf und aktive Same-PCL-Teststufe ab 15:04 Uhr

Der Lauf von 15:04:51 bis 15:08:49 endete sauber über `OnUnloadMap(Pre)`. Nach der Plugininitialisierung entstand kein Dump und keine Error-Level-Logzeile. Der Dump mit ungefähr 46,9 MB um 15:04:53 lag vor dem Pluginstart und ist weiterhin der bekannte Extender-/Mono-Fehlalarm. Die Capturer-Stellen liefen 964.838 beziehungsweise 24.963 Mal ohne Callbackfehler. Ein Tor wechselte von nicht erobert zu Eroberer 4 und zurück; eigener Eroberer und fremde Eroberer wurden beobachtet. Fremde Eroberer änderten 3.660 Mal das rekonstruierte Vanilla-ZF von null auf eins. Ein verbündeter Eroberer fehlt weiterhin in der Laufzeitabdeckung. Der Client war nicht erreichbar.

Die frühere Cursorabnahme war zu großzügig: `cursorBlocked=PASS` beruhte auf 241 lokalen `NoRoute`-Ergebnissen, obwohl keine Suche ein gesperrtes Tile berührte. Der Cursor führt deshalb nun eine ungefilterte Referenz- und eine gefilterte Policysuche aus. Nur wenn die Referenz erreichbar ist und die Policy das Ziel oder die verbleibenden Wege nach einer tatsächlichen Tilebegegnung trennt, wird Vanillas positives Ergebnis gelöscht. Eine nicht reproduzierbare Vanilla-Route bleibt als `cursorVanillaNoRoute` fail-open. Ein `cursorForcedDetour` setzt zusätzlich eine längere gefilterte kürzeste Route voraus.

Der anschließende 7-KI-Laglauf zeigte die Grenze dieser ersten Implementierung klar: 30.043 zentrale Builderaufrufe erzeugten 7.133 als unsicher erkannte Pfade und damit 7.133 verwaltete Vollkartensuchen. Keine davon publizierte einen Ersatzweg. Am Laufende entstanden ungefähr 430 bis 475 teure Suchläufe je zehn Sekunden. Das erklärt die starke zusätzliche Verlangsamung; Capturer-, Snapshot- und Cursordiagnose waren nicht die Ursache.

Die Vollkartensuche, Routenpufferersetzung, Nibble-Neucodierung, nachträgliche Auditierung und Rollbackdiagnose sind deshalb vollständig entfernt. Der aktuelle Teststand bindet Spielercontexts um `0xF4930`, `0xDBC60`, `0xDA020`, `0x123090` und `0x195E30` und lässt Vanillas eigentliche Suche selbst entscheiden. Eine Richtungsmaske beginnt mit `0xFF` und löscht nur die bestätigten Übergänge durch die Mittelachse eines unzugänglichen Gatehouse beziehungsweise seiner verknüpften Zugbrücke einschließlich diagonaler Schnitte. Der vollständige 5×5-Footprint wird ausdrücklich nicht gesperrt: Annäherung, Bewegung auf angrenzenden Mauern und das Verlassen eines Gatebereichs bleiben möglich. Entry-/Exit-Tiles bestimmen bevorzugt die Passageachse; eindeutig längliche Footprints dienen als belegter Fallback. Quadratische oder sonst mehrdeutige Geometrie bleibt offen.

Der sehr große Cursordispatcher `0x8C5F0` wird nicht zusätzlich als Funktion detourt: Sein bestehender kausaler Cursorhook bleibt die abgesicherte Publikationsgrenze. Direkte `0xDB650`-Aufrufe außerhalb eines der belegten Scopes – insbesondere der spielerlose Wartungspfad und der noch nicht spielergebunden belegte `0x1232E0`-Pfad – sehen keinen Threadslot und bleiben unverändert. Damit wird keine Spieler-ID aus überlappenden Indizes oder unsicheren Registerannahmen erraten.

Diagnosewerte heißen nun `edgeRejected`, `vanillaDetours`, `policyNoRoute`, `humanBuilderDetour`, `aiDetour`, `attackDetour`, `buildingApproachDetour` und `cursorDetour`. `edgeRejected` wird nur erhöht, wenn ein im Original-Richtungsbyte tatsächlich gesetztes Bit durch die Policy gelöscht wurde. Ein erfolgreicher Vanilla-Aufruf mit solchen Löschungen belegt einen policybeeinflussten Vanilla-Weg; ein erfolgloser Builderaufruf belegt `policyNoRoute`. Bei geladenem `BugfixesAndQoL_Serp` wird die komplette überlappende Same-PCL-Gruppe vor Vorbereitung unterdrückt; die Capturer-Hooks bleiben unabhängig aktiv.

## Lauf ab 20:20 Uhr und korrigierter Adaptervertrag

Der Kartenlauf von 20:21:46 bis 20:23:44 endete sauber über `OnUnloadMap(Pre)`. Die beiden Capturer-Hooks liefen 1.298.904 beziehungsweise 35.644 Mal ohne Callbackfehler; der Cursor prüfte 983 positive Ergebnisse. Nach Pluginstart entstand kein neuer Crashdump. Die Dumps des Prozesses ab 20:18 Uhr gehören zu einem vorherigen Lauf mit `BugfixesAndQoL` und dieser Prozess lief nach beiden Berichten weiter. Der nachfolgende Start ab 20:24 Uhr lud `EnemyGatePathfindingTest` nicht.

Die Same-PCL-Transaktion wurde vor Veröffentlichung korrekt zurückgerollt. Iced meldete beim ersten Adapter `Multiple instructions with the same IP`, weil die identischen dekodierten Vanilla-Instruktionen in einem gefilterten und einem ungefilterten Zweig erneut angefügt wurden. Der korrigierte Emitter besitzt nun je Zielregister einen einzigen gemeinsamen Originalpfad: Kontext und Maskenadresse werden vorher bestimmt, der native Producer wird genau einmal ausgeführt, sein Ergebnis optional maskiert und die restlichen Originalinstruktionen werden ebenfalls genau einmal angefügt. Vor RedBirds Probe und Transaktion wird jeder vollständige Adapter wirklich assembliert und wieder disassembliert; externe Vanilla-Sprungziele müssen erhalten bleiben.

Der Lauf zeigte außerdem, dass alle zwölf Gatehouse-Records zwar einen Entry-Tile, aber `ExitTileId=0` besaßen. Der alte Snapshot verwarf damit versehentlich auch die separaten Entry-/Exit-Koordinaten und den Orientierungsrohwert; die resultierenden quadratischen 5×5-Footprints lieferten keine Richtungsmaske. Ungültige Tile-IDs verwerfen den Record deshalb nicht mehr. Die Achse wird zuerst aus gültigen Koordinaten und danach aus der eindeutigen kardinalen Lage einer verknüpften Zugbrücke bestimmt. `r_SubtypeOrOrientation` wird nur diagnostisch protokolliert, bis seine Semantik unabhängig bestätigt ist. Quadratische Geometrie ohne diese Evidenz bleibt fail-open.

Same-PCL-Hooks werden erst beim ersten Snapshot mit mindestens einer tatsächlich nichtleeren Spielermaske atomar veröffentlicht. Snapshots protokollieren `axisSource`, maskierte gerichtete Kanten, mehrdeutige Passagen und die Zahl nichtleerer Spielermasken. Nicht verfolgte Capturer-Records beeinflussen die Runtimeintegrität nur noch, wenn Building-ID und Subject-Global-ID zu einem aktiven Gatehouse passen; normale Path-Connections werden separat gezählt.

Die kausale Cursor-Doppelsuche verbrauchte im Lauf insgesamt 3,808 Sekunden und erreichte in einer Einzelsuche 20,136 ms. Als vorübergehende Vergleichsdiagnose wird sie deshalb auf höchstens zehn frische Suchen pro Sekunde und 160.000 besuchte Knoten je Teilsuche begrenzt. Eine Drosselung oder Budgetüberschreitung bleibt ausdrücklich fail-open. Nach erfolgreicher nativer Cursorabdeckung soll diese verwaltete Vergleichssuche nur noch im Diagnosemodus laufen oder entfallen.

## Echter Adaptercrash um 21:59 Uhr

Der Lauf ab 21:58:35 installierte die Capturer- und Cursorhooks zunächst erfolgreich. Der erste stabile Topologiesnapshot um 21:59:28 enthielt elf Gates, acht nichtleere Spielermasken und 258 maskierte gerichtete Kanten. Direkt danach wurde die vollständige Same-PCL-Gruppe mit fünf Funktionsdetours und zehn Direction-Adaptern veröffentlicht. Um 21:59:31 entstand ein echter 189.693.330-Byte-Dump mit `EXCEPTION_ACCESS_VIOLATION`; anders als der bekannte 46,9-MB-Extender-Fehlalarm lag er nach Pluginstart, Kartenstart und Hookveröffentlichung.

Der Crash-RIP `0x00007FFA10411782` liegt im Stub für `0xF33F5`, exakt auf `and cl, byte [r10]`. Der Threadslot enthielt den gültigen Maskenpointer `0x00000153BB3EFB50`. Der Adapter hatte dazu jedoch `RCX=0x00007FFA80260000`, die Basis von `CrusaderDE.dll`, addiert und so den ungültigen Pointer `0x0000814E3B64FB50` erzeugt. Das native Original adressiert das Direction-Grid dort mit `R8 + RCX + 0x51890D0`: `R8` ist der Tile-Index, `RCX` die Modulbasis.

Das erneute Datenflussaudit ergibt die feste Tile-Registertabelle `D9EA6=R8`, `DA783=RDI`, `DACB2=RDI`, `DB242=R10`, `F31A8=RAX`, `F33F5=R8` und `DB857/DB947/DBA36/DBB26=RDI`. Damit waren neben `F33F5` auch `D9EA6` und `DB242` falsch zugeordnet. Jeder Adapter prüft den ausgewählten 32-Bit-Tile-Index nun vor der Pointeraddition unsigned gegen die native Kapazität 320.800. Der geprüfte Wert wird anschließend über ein Scratchregister nullerweitert, das der folgende Vanilla-Producer ohnehin überschreibt oder das der Adapter selbst sichert; damit können auch keine alten High-Bits in die Adresse gelangen. Negative Werte, Modulbasiswerte und Werte außerhalb des Grids nehmen denselben unveränderten Vanilla-Pfad wie ein fehlender Context. Iced validiert vor Veröffentlichung genau einen Guard, genau eine Nullerweiterung aus dem belegten Tile-Register und genau eine Addition dieses Indexes; die Laufzeit protokolliert Register, Verdrängung und Grenze für alle zehn Stellen.

## Vollständiger Direction-Grid- und Spieler-Scope-Audit nach dem Editortest

Der Editortest mit Toren und Mauern von Spieler 1 sowie Einheiten von Spieler 2 zeigte zwei getrennte Fehler. Der Cursor wechselte regelmäßig zwischen erlaubt und verboten, weil sein Ergebnis nur 50 ms gültig war, frische Suchen aber höchstens alle 100 ms liefen. Die dazwischenliegenden 50 ms wurden ausdrücklich fail-open behandelt. Ein identischer Query-Key behält deshalb nun seine letzte definitive Entscheidung, bis eine erfolgreiche Aktualisierung vorliegt. Ein neuer Spieler-, Start-, Ziel- oder Policy-Key wird sofort geprüft; Throttling verändert niemals mehr die Durchsetzungsentscheidung.

Der vollständige Aufrufergraph der hashgleichen Baseline korrigiert außerdem mehrere Spielerannahmen. `F4930` erhält den Spieler als Argument 2, `DBC60` ausdrücklich als Argument 8, `DA020` als Argument 6 und `DC3C0` als Argument 5. `195E30` verwendet intern den nativen aktiven Spieler aus `DAT_1888E3D70` und prüft später den Besitzer der als Argument 2 übergebenen Tribe-ID gegen genau diesen Wert. `123090` und `1232E0` verwenden Argument 2 jeweils als Tribe-ID. Der Mod folgt nun diesen Quellen direkt; `GetLocalPlayerId()` und die frühere unbelegte Interpretation von `DBC60`-Argument 2 sind entfernt. Widersprüche zwischen explizitem Spieler und Tribe-Besitzer bleiben fail-open und werden als eigener Vertragsfehler protokolliert.

Alle Direction-Grid-Referenzen der Baseline wurden inventarisiert. Die zehn vorhandenen Adapter decken die sieben Reader des zentralen `F4930`-Builders vollständig ab. Zusätzlich ist `DC3C0` eine eigenständige spielerabhängige Kandidatensuche der Aufrufer `11E960` und `188070`. Ihr elfter Adapter liegt bei `0xDC536`, verdrängt exakt 16 Byte bis `0xDC546`, lädt das Richtungsbyte über den Tile-Index `R12` nach `ECX` und lässt die Modulbasis `R9` unangetastet. Der zweite direkte `DB650`-Verbraucher `1232E0` erhält ebenfalls einen Tribe-Scope. Grid-Erzeuger, Wartung, Bewegungsverbrauch und der spielerlose Pfad `CF020 → DB650` bleiben Vanilla.

Die vollständige Same-PCL-Gruppe besteht damit aus elf nativen Direction-Adaptern und sieben Funktions-Scopes. Hash, Einstiegsbytes, Instruktionsgrenzen, reale RedBird-Verdrängung, externe Sprungziele, Tile-/Modulbasisregister und Rücksprünge werden vor der gemeinsamen Veröffentlichung geprüft. Kandidatensuchen mit `void`-Rückgabe werden nicht mehr fälschlich als erfolgreiche Umwege gewertet; ihre Diagnose zählt ausschließlich tatsächlich gefilterte Kanten.

Die bisherige Snapshotübernahme besaß ein theoretisches Use-after-free-Fenster: Ein Reader konnte den alten Snapshot laden, bevor er dessen Reader-Zähler erhöhte, während der Deferred-Pfad denselben nativen Puffer bereits freigab. Vier prozesslang verwurzelte native Slots ersetzen deshalb die dynamische Allokation und Freigabe. Erwerb, Reader-Zähler, Veröffentlichung und Slotreservierung verwenden ein gemeinsames kurzes Lock; die native Suche selbst läuft außerhalb davon. Kein belegter oder gerade befüllter Slot wird überschrieben. Verschachtelte Threadscopes führen eine explizite Tiefe, sodass auch eine gültige Nullmaske den äußeren Scope nicht vorzeitig freigibt.

## BFS-freier Gatehouse- und Cursorvertrag

Der nachfolgende Editortest bestätigte die Zugbrückenbarriere, während ein alleinstehendes fremdes Torhaus weiterhin vollständig passierbar blieb. Beim protokollierten Tor `bounds=399/367–403/371` und `entry/exit=401/372–401/366` lag die bisherige Barriere in der Footprintmitte. Vanillas Torhausdurchquerung kann diese interne Kante umgehen. Torhäuser maskieren deshalb nun die beiden durch Entry/Exit belegten äußeren Grenzen: `y=366↔367` und `y=371↔372`, jeweils über die ganze Breite einschließlich der diagonalen Schnitte. Horizontale Tore verwenden entsprechend `x=minX-1↔minX` und `x=maxX↔maxX+1`. Entry-/Exit-Koordinaten und Bounds müssen exakt zusammenpassen; mehrdeutige Geometrie bleibt fail-open. Zugbrücken behalten ihre im Test bestätigte Mittelbarriere.

Der verwaltete `CursorGateRouteFilter`, sein Hook bei `0x8F1C4`, Cache und beide Vollkartensuchen sind vollständig entfernt. Die Baseline belegt stattdessen den direkten Vanilla-Aufruf `0x8F269 → 0xDB650`, Rücksprung `0x8F26E`. Ein 29-Byte-Inlineadapter beginnt an `0x8F251`, emittiert die drei unveränderten Argumentinstruktionen genau einmal und ersetzt ausschließlich den direkten Call durch einen statisch verwurzelten Sechs-Argument-Wrapper. Dieser liest `DAT_1888E3D70`, bindet den bereits veröffentlichten nativen Maskenpointer, ruft dieselbe Vanilla-Funktion `0xDB650` genau einmal auf und stellt den verschachtelten Threadcontext wieder her. Callsite, sieben Funktions-Scopes und elf Direction-Adapter werden in einer einzigen Transaktion veröffentlicht; die reale RedBird-Verdrängung muss exakt 29 Byte betragen.

Damit existiert pro Befehl nur Vanillas ohnehin erforderliche Suche. Der Mod erzeugt keine PCLs, keinen eigenen PCL-Graphen, keine Ersatzroute und keine Referenz-BFS. In den elf Knotenpfaden laufen ausschließlich Threadslotprüfung, unsigned Bounds-Guard, Maskenpointeraddition und Byte-AND; Treffer werden im Queryslot gesammelt und erst am Suchende global übernommen. Die verwalteten Querywrapper erzeugen keine Closures oder sonstigen per-Query-Objekte. Access- und Topologiesicherheit werden höchstens einmal pro Sekunde geprüft, außer ein bereits verfolgtes Gebäude meldet auf einem Spieltick eine konkrete Struktur- oder Zugriffsänderung. Semantisch unveränderte Zustände erzeugen weder eine neue Richtungsmaske noch eine Kopie in den nativen Slotpool.

Editorläufe ohne normale Kartenereignisse starten ihre zentrale Diagnoseepoche nun beim ersten Deferred-Zyklus mit gültiger Kartengröße. Die Abnahmezeile unterscheidet den direkten nativen Cursorscope und dort tatsächlich gefilterte Kanten; die alten BFS-Verdicts sind entfernt. Dieser Abschnitt ersetzt alle älteren Aussagen, nach denen der Hook `0x8F1C4` oder eine verwaltete Cursor-Doppelsuche noch aktiv sei.
