# EnemyGatePathfindingTest – konsolidierte Erkenntnisse

Stand: 11. September 2026

## Ziel und aktueller Umfang

Der Testmod verhindert, dass eine PCL-Route ein feindliches Tor oder dessen Zugbrücke allein wegen einer Eroberung durch einen fremden dritten Spieler als zugänglich behandelt. Besitzer, Verbündete sowie der eigene oder ein verbündeter Eroberer behalten Vanillas Zugriff. Unsichere oder veraltete Snapshots führen immer zu Vanilla-Verhalten (fail-open).

Zusätzlich prüft der menschliche Bewegungscursor positive Same-PCL-Ergebnisse gegen unveränderliche Tor-/Brücken-Snapshots und das native Richtungsraster. Eine Route, die nur durch ein blockiertes feindliches Tor führt, wird verworfen; ein tatsächlich offener Umweg bleibt gültig.

Same-PCL-KI-Routen und cursorlose Same-PCL-Befehle bleiben vorerst fail-open. Der vollständige Audit ist unten dokumentiert; ein sicherer und ausreichend schneller nativer Filterstub fehlt noch.

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

Deshalb wird kein Same-PCL-Hook veröffentlicht. Der nächste sichere Schritt ist ein kleiner nativer, TLS-fähiger Filterstub, der innerhalb des `0xF4930`-Scopes ausschließlich die zehn belegten Edge-Prüfungen maskiert, ohne das globale Direction-Grid zu verändern. Vor seiner Freigabe müssen für jede Stelle exakte Ersatzbytes, eingehende Sprünge, Register-/Flag-Liveness, Reentranz und alle Caller von `0xF4930` belegt werden. `0xE1640` rekonstruiert anschließend eine bereits gefundene Route und ist kein Ersatz für die lokale Kantenfilterung.

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
