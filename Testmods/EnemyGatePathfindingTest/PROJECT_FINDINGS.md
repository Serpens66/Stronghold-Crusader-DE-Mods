# EnemyGatePathfindingTest – konsolidierte Erkenntnisse

Stand: 19. September 2026

## Ziel und aktueller Umfang

Der Testmod verhindert, dass eine PCL-Route ein feindliches Tor oder dessen Zugbrücke allein wegen einer Eroberung durch einen fremden dritten Spieler als zugänglich behandelt. Besitzer, Verbündete sowie der eigene oder ein verbündeter Eroberer behalten Vanillas Zugriff. Unsichere oder veraltete Snapshots führen immer zu Vanilla-Verhalten (fail-open).

Zusätzlich prüft der menschliche Bewegungscursor positive Same-PCL-Ergebnisse gegen unveränderliche Tor-/Brücken-Snapshots und das native Richtungsraster. Eine Route, die nur durch ein blockiertes feindliches Tor führt, wird verworfen; ein tatsächlich offener Umweg bleibt gültig.

Same-PCL-KI-Routen und cursorlose Befehle verwenden in der isolierten Testkonfiguration nun Vanillas Suche mit einer querylokalen Gate-Richtungsmaske. Es gibt keine verwaltete Ersatzsuche und keinen globalen Direction-Grid-Writer. Bei nicht belegtem Spieler-, Geometrie- oder Native-Vertrag bleibt Vanilla unverändert (fail-open).

## Referenzumgebung

- Kanonische `CrusaderDE.dll`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Script Extender 2.8.0: Tag `v2.8.0`, Commit `5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33`
- RedBird.X64 1.3.2.0
- APIShared 0.3.7 für den editorfähigen Mission-Lifecycle
- gleichzeitig geprüfter Fixes-Mod: 1.17.1, lokaler Commit `d79b2267e22f49b02a9363f93c3c0836475b3e44`
- Modversion während des Tests: `0.1.5`

Der Runtime-Mod verweigert die Installation bei einem abweichenden nativen Hash oder einer abweichenden Bytefolge. Die Projektdatei verwendet standardmäßig ausschließlich die installierte Extender-Assembly unter `BepInEx\plugins\000shcdese`; ein anderer Pfad muss ausdrücklich über `ExtenderDir` beziehungsweise `SHCDESE_EXTENDER_DIR` gesetzt werden.

## SE-2.8.0-Audit und ungültiger Editorlauf vom 19. September 2026

Der Editorlauf ab 22:04 Uhr war kein Test des Gatefilters. Der Asset-Loader fand zwar das Paketverzeichnis, BepInEx verweigerte aber das Laden von `Enemy Gate Pathfinding Test 0.1.5`, weil `APIShared_Serp >= 0.3.6` fehlte. In einem späteren Start ab 22:15 Uhr wurde `APIShared 0.3.7` erfolgreich geladen, der Testmod war zu diesem Zeitpunkt jedoch nicht installiert. Die beobachtete Tor-/Zugbrückenbewegung war deshalb in beiden Fällen Vanilla- beziehungsweise Fixes-Verhalten. Die jeweils etwa 47 MB großen Dumps entstanden vor der Plugininitialisierung und gehören zum bekannten, von Mono behandelten SE-Startfehler; es gab keinen Dump eines aktiven EnemyGatePathfindingTest-Hooks.

Der vollständige featurebezogene Audit der unveränderten DLL `FBCB9319…` bestätigt den Befehlsfluss. `0x18E1E0` und `0x196280` prüfen zunächst Quell- und Ziel-PCL. Bei verschiedenen PCLs wählen `0xE2610` und anschließend `0xDF720` den erreichbaren Übergang; bei gleichem PCL geht der Auftrag direkt an `0xF4930`. Dieser Dispatcher verwendet die vollständig abgedeckten Suchen `0xD9C40`, `0xDA590`, `0xDAAC0`, `0xDAFD0`, `0xF3060` und `0xF32B0` und rekonstruiert erfolgreiche Routen über `0xE1640`. `0xDB650` bleibt der boolesche Direkt- und Cursorpfad. Die elf Direction-Adapter des Mods liegen ausschließlich in diesen spielergebundenen Suchpfaden.

Vanilla lässt in `0xE2610` feindliche, nicht eroberte Gate-Verbindungen bereits aus, nimmt eroberte Verbindungen aber wieder in den PCL-Graphen auf. Die beiden 20-Byte-Capturer-Hooks ergänzen genau dort die Besitzer-/Allianz-/Erobererpolicy. Same-PCL-Suchen erhalten dagegen ausschließlich die vorab berechnete Richtungsmaske. Es werden weiterhin keine PCLs erzeugt, keine verwaltete BFS gestartet und keine Ersatzrouten publiziert.

Der Vergleich `v2.7.1..v2.8.0` im kanonischen Extender-Fork enthält keine Änderung an `GamePathingManagerAPI`, den Player-Pathfinding-Detours oder den verwendeten Eventverträgen. RedBird bleibt bei 1.3.2. Die konkrete Kompatibilitätsentscheidung basiert weiterhin auf Hash, Bytefolgen, echter Assemblierung/Disassemblierung, Sprungzielen und `DisplacedByteCount`; die Versionsnummer wird diagnostisch protokolliert und ist kein Ersatz für diese Verträge.

Fixes 1.17.1 erweitert den PCL-Neuaufbau an `0xE4AA3`, `0xE4B61`, `0xE4DF4`, `0xE7CE6` und `0xE7E92`. Diese Intervalle überschneiden weder die Capturer-Hooks noch den Builder, die Cursoradapter oder einen der elf Direction-Adapter. Fixes verändert damit die PCL-Speicherkapazität und den Neuaufbau, besitzt aber keinen konkurrierenden Hook auf den querylokalen Gatefiltern.

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
- Der Cursor verwendet keine verwaltete BFS. Ein ABI-genauer Inlineadapter prüft eine repräsentative Einheit gedrosselt mit Vanillas eigener `DB650`-Suche und derselben nativen Richtungsmaske wie der Befehlsweg.
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

Die Maschinentests prüfen die beiden 20-Byte-Blöcke, ihre exklusiven Endadressen, umgebenden Sprünge und eine absichtlich mutierte Bytefolge. Insbesondere liegen `0xE271B` und `0xE303A` außerhalb der Hookspannen. Zusätzlich prüfen sie beide CMP-Rekonstruktionen, Setzen und Löschen von ZF ohne Veränderung anderer Flags sowie die getrennten Snapshot-Fehlerursachen. Policytests decken Besitzer, Verbündete, eigenen/verbündeten Eroberer, fremden Eroberer, ungültige Snapshots und die native Cursorvalidierung ab.

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

## Cursorabgleich nach dem Editorlauf ab 00:56 Uhr

Der Editorlauf vom 12. September 2026 ab 00:56:34 bestätigt den nativen Befehlsfilter. Zwei fremde Torhäuser und eine Zugbrücke ergaben sieben nichtleere Spielermasken und 150 maskierte gerichtete Kanten. Für Spieler 2 wurden bis 00:58:14 insgesamt 544 Kanten verworfen; 40 Builder-Suchen lieferten dadurch `NoRoute`, weitere fünf fanden mit Vanillas eigener Suche einen zulässigen Umweg. Es traten keine Exceptions, Threadslotkonflikte oder Snapshot-Poolerschöpfungen auf. Damit stimmen die beobachteten verweigerten Befehle mit der Richtungsmaske überein. Der grüne Cursor wurde dagegen ebenfalls erklärt: `directCursorQueries=0` und `directCursorEdgesFiltered=0`, weil der normale Cursorpfad die bei `0x8F1BF` ausgewertete PCL-Entscheidung verwendet und den besonderen Aufruf `0x8F269 → 0xDB650` in diesem Fall nicht betritt.

Der vollständige Baselinefluss für den normalen Cursor ist `DLL_RunTick → 0x8C5F0 → 0x18D460/E7C40 → 0xE2610`. `0x18D460` liefert in `R14` genau eine priorisierte, kontrollierbare 1-basierte Unit-ID. `0xE7C40` liefert Ziel- und Erreichbarkeitsdaten; am Block `0x8F1BF–0x8F1CC` führt Vanilla `CALL 0xE2610`, `TEST EAX,EAX` und eine RIP-relative `LEA` aus. Der nachfolgende Verbraucher bei `0x8F1D2` setzt den grünen Zustand nur bei `ZF=0`. Die Zielkoordinaten stehen während dieses Blocks konsistent im Cursormanager `0x3A11DC0` an den Offsets `0x6C/0x70/0x78`; die Tile-ID wird zusätzlich gegen die Row-Start-Tabelle `0x402FF2C` geprüft. Der exakte 14-Byte-Block besitzt keine eingehenden Sprungziele und keine live SIMD-Werte. Da RedBird 1.1.0 beim Aufbau des Contexts die von `TEST` gesetzten Flags verändert, rekonstruiert der Callback ZF aus `EAX` und stellt es vor der Rückkehr ausdrücklich wieder her.

Für eine Cursorentscheidung wird nun ausschließlich bei positivem Same-PCL-Ergebnis ein primitiver Request aus aktivem nativen Spieler, der von Vanilla bereits gewählten Unit-ID, Ziel und Policyfingerprint veröffentlicht. Außerhalb des nativen Callbacks validiert der Deferred-Pfad diese einzelne Unit und ruft höchstens einmal je 200 ms dieselbe Vanilla-Suche `0xDB650` mit ihrem originalen Limit 400.000 auf. Der vorhandene Threadscope macht dabei nur die bereits vorberechnete Richtungsmaske sichtbar. Ein Ergebnis wird nur dann rot, wenn Vanilla `NoRoute` meldet und die nativen Adapter in genau dieser Suche mindestens eine maskierte Gate-/Bridge-Kante verworfen haben; gewöhnliche unerreichbare Ziele bleiben fail-open. Erfolgreiche Suchen, die maskierte Direktkanten umgehen, bleiben grün. Der Cache hält die letzte Entscheidung für denselben Spieler-/Unit-/Ziel-/Policy-Key stabil, bis eine erfolgreiche Aktualisierung vorliegt.

Dieser Cursorabgleich berechnet keine PCLs, traversiert keinen Portalgraphen und enthält keine verwaltete BFS. Im Knoten-Hotpath ändert sich nichts: Bounds-Guard, Maskenpointer und Byte-AND bleiben die einzigen Zusatzoperationen. Pro Cursorzustand wird höchstens eine repräsentative Unit geprüft, niemals die gesamte Auswahl; tausende ausgewählte Einheiten erzeugen daher keine modseitige lineare Arbeit und höchstens fünf zusätzliche bereits native `DB650`-Suchen pro Sekunde. Die Checkpoints protokollieren Refresh-/Cache-/Blockadezähler, durchschnittliche und maximale Validierungszeit sowie genau ein deferred formatiertes Beispiel.

## Korrektur des Cursor-PCL-Callsite-Vertrags

Der Editorlauf vom 12. September 2026 ab 13:08:40 installierte den 14-Byte-Hook und veröffentlichte sieben nichtleere Spielermasken mit 150 maskierten Kanten. Der Befehlsweg arbeitete mit 496 verworfenen Kanten und 35 `NoRoute`-Ergebnissen fehlerfrei. Gleichzeitig blieben `cursorPclChecks`, Refreshes, Cachetreffer und Cursorbeispiele vollständig bei null. Das war kein fehlendes Testszenario, sondern ein unerreichbarer Callbackzweig.

Der korrigierte vollständige Baselinefluss zeigt die Ursache: Vor `0x8F1BF` stehen Ziel-PCL in `R8D`, Quell-PCL in `R9D` und die repräsentative 1-basierte Unit-ID in `R14D`. RedBirds `BeforeCallback` führte jedoch den gesamten verdrängten Block `CALL E2610; TEST EAX,EAX; LEA RDI,[0x18405EDB0]` aus. Beim Callback war `RDI` deshalb bereits die globale Rasterbasis und kein PCL mehr; `ESI==EDI` konnte nie wahr werden. Der frühere Quelltest prüfte lediglich das Vorkommen der Registernamen und simulierte diesen Post-Displacement-Zustand nicht.

Der Context-Hook ist deshalb vollständig durch einen Inline-Callsite-Adapter ersetzt. Ein eigener 48-Byte-Win64-Callframe reicht Vanillas fünf unveränderte `E2610`-Argumente und `R14D` als sechstes Wrapperargument weiter. Der statisch verwurzelte Wrapper ruft das originale `E2610` genau einmal auf, entscheidet anhand der echten ABI-PCLs und gibt nur bei einem passenden blockierenden Cacheeintrag statt eines positiven Ergebnisses null zurück. Danach emittiert der Adapter Vanillas `TEST` und RIP-relative `LEA` jeweils genau einmal. Ziel- und Quell-PCL gehören nun zum Cache-Key. Die gedrosselte Prüfung bleibt eine einzelne native `DB650`-Suche für eine repräsentative Unit, höchstens fünfmal pro Sekunde; es entstehen weiterhin keine neuen PCLs, keine verwaltete BFS und keine Arbeit proportional zur ausgewählten Einheitenzahl.

## Kurzfassung: Lösung ohne eigene teure Pfadsuche

- Der Mod erzeugt keine PCLs und berechnet keine Ersatzroute.
- Bei einer echten Struktur- oder Zugriffsänderung entsteht einmalig pro Spieler eine unveränderliche Richtungsmaske. Sie löscht nur die Übergänge durch unzugängliche fremde Torhäuser und Zugbrücken.
- Während Vanillas ohnehin notwendiger Wegsuche führen elf kleine native Adapter lediglich einen Bounds-Guard und ein zusätzliches Byte-AND mit dieser Maske aus. Dadurch behandelt Vanilla die Passage wie ein geschlossenes Tor und findet selbst einen Umweg oder meldet `NoRoute`.
- Der Cursor verwendet dieselbe Policy. Eine einzige repräsentative Einheit wird mit Vanillas `DB650` höchstens fünfmal pro Sekunde geprüft; die Zahl ausgewählter Einheiten erhöht die Arbeit nicht.
- Positive Same- und Different-PCL-Cursorentscheidungen werden gleich behandelt. Eine bestätigte Blockade bleibt für denselben Spieler-, Unit-, PCL- und Policykontext bis zur nächsten Aktualisierung rot, sodass Cursorbewegungen keine Grün-Blitze mehr erzeugen.

Der Editorlauf vom 12. September 2026 ab 13:53:11 belegte die technische Grundlage: sieben Spielermasken, 150 maskierte Kanten, 3.468 ABI-korrekte Cursorwrapper-Aufrufe, 508 native Cursoraktualisierungen mit durchschnittlich etwa 0,1 ms und 841 erzwungene Rot-Ergebnisse ohne Exceptions oder Slotkonflikte. Das Torhaus blieb nur deshalb grün, weil die damalige Cursorprüfung Different-PCL-Fälle ausließ; diese Einschränkung ist nun entfernt. Die auditierten KI-Suchscopes sind implementiert, ihre praktische Abnahme bleibt anhand der getrennten AI-/Angriffs-/Gebäude-/Kandidatenzähler nachzuweisen.

## Einordnung der neuen Pathfinding-APIs aus Script Extender 2.6.0

Der vollständige featurebezogene Audit gegen `FBCB9319` zeigt, dass die neuen schreibbaren Tabellen keine Alternative zur querylokalen Gatepolicy sind. Die Profil-Tabelle bei RVA `0x322540` wird von `0x18E1E0`, `0x196280` und `0x198ED0` gelesen. Über das dritte Argument von `0xF4930` wählt sie Such- und Oberflächenvarianten für einen gesamten Einheitentyp. Der öffentliche Verbindungsklassenbereich beginnt mit Klasse 1 bei RVA `0x32BDB0`; `0x181E00(manager, unitId, connectionClass)` liest ihn für `0xDF720` und `0x182750`. Beide Tabellen besitzen weder Spieler- noch Besitzer-, Capture- oder Building-ID-Dimensionen. Ein modseitiges Umschalten würde deshalb eigene, verbündete und fremde Tore gemeinsam verändern und ist für dieses Ziel ungeeignet.

Der neue Next-Tile-Override detourt `0xDCD60`, das im Bewegungsnachlauf `0x1855A0` eine bestimmte Wand-/Hochflächenablehnung ausführt. Der Override kann diese Ablehnung ausschließlich in Erfolg ändern und daher kein feindliches Tor sperren. Der Assassin-Auswahl-Override erweitert das positive Ergebnis von `0x196870`; dessen Caller `0x8C5F0`, `0xB70C0` und `0xB72C0` verwenden es in Sonderpfaden um Erreichbarkeit und Kandidatenauswahl. Der Testmod setzt keinen dieser globalen Overrides und installiert insbesondere keinen konkurrierenden Hook auf `0x196870`.

Zur Konfliktdiagnose liest der Mod beide neuen Tabellen genau einmal im ersten deferred Runtime-Durchlauf über die öffentlichen SE-2.6.0-APIs. Die Adressrechnung des nativen Readers lautet `0x32BDB0 + (unitType + connectionClass * 90) * 4 - 0x168`. Weil `0x168 = 90 * 4`, beginnt Klasse 1 weiterhin bei `0x32BDB0`; die davorliegende Zeile bei `0x32BC48` ist Klasse 0 (`Unknown`) und gehört nicht zum öffentlichen Span für Klassen 1 bis 6. Der zuvor behauptete Extender-Zeilenversatz war ein Analysefehler des Testmods und wurde vollständig zurückgenommen; es wird kein Fehlerbericht an den Extender-Autor gesendet.

Die kanonische DLL enthält 13 `Large`- und 77 `Default`-Profile; die echten Klassen 1 bis 6 erlauben `4/16/89/89/89/83` Einheitentypen. Ein kompakter Bitvergleich meldet Abweichungsanzahl, Fingerprint und höchstens zwölf konkrete Tabellenpositionen. Diese Prüfung schreibt nichts, läuft nur einmal beim Start, liegt nie im Pathfinding-Hotpath und beeinflusst weder PCLs, Richtungsmasken noch bestehende Einheitspfade. Sie dient ausschließlich dazu, globale Änderungen anderer Mods sichtbar zu machen.

## KI-Stresstest und Sparse-Footprint-Vertrag

Der 7-KI-Lauf belegte keinen Fehler der nativen Richtungsadapter, sondern einen globalen Ausfall des vorgeschalteten Topologieaufbaus. Gebäudeslot 73 war zunächst ein aktives Tor von Spieler 2, wurde entfernt und später für ein Gebäude von Spieler 4 wiederverwendet. Während dieses Lebenszyklus enthielt sein Inline-Footprint mindestens eine für den bisherigen strikten Bounds-Helfer unbrauchbare Zelle. `ComputeTopologySignature` ließ die daraus entstehende Exception bis zum globalen Deferred-Handler laufen. Dadurch stiegen die Snapshotfehler fortlaufend, es wurden keine Spielermasken veröffentlicht und die atomare Same-PCL-Gruppe blieb folgerichtig inaktiv.

Die hashgleiche Vanilla-Baseline zeigt den maßgeblichen Slotvertrag: Die Anlage bei RVA `0xB47E0` initialisiert einen 0x32C Byte großen Gebäudeslot zunächst als `NeedsInit`; die relevanten Writer und Verbraucher bei `0x739C0` und `0x6CDD0` arbeiten mit dem begrenzten Inline-Footprint; die Entfernung bei `0xB8310` baut Pfad-/Tilezustand ab und nullt den Slot erst am Ende. Deshalb dürfen Slots außerhalb `NeedsInit`/`IsAlive`, Slots ohne Global-ID und unbelegte Nullzellen keine globale Policyinvalidierung verursachen.

Der Testmod liest für relevante aktive Gebäude nun ausschließlich Gridgrößen 1 bis 6 und höchstens 36 Inline-Zellen. Tile-ID 0 ist eine unbelegte Zelle und wird übersprungen. Eine nicht-nullige ungültige Tile-ID, ein vollständig leerer Footprint oder eine unsichere Gridgröße lehnt nur die einzelne Entität kategorisiert ab. Derselbe allokationsfreie Accumulator liefert Builder und Änderungssignatur identische Status-, Bounds- und Fingerprintsemantik. Aktive Connection-Records mischen zusätzlich Building-/Global-Identität sowie Entry-/Exit-Tiles und -Koordinaten in die Signatur. Ein Connection-Record-Tor darf bei lokal unbrauchbarem Footprint weiterhin den validierten Entry-/Exit-Fallback benutzen; ohne eindeutige Barriere bleibt nur dieses Tor fail-open.

Die Orphan-Bridge-Diagnose berechnet Bounds und Distanzen ausschließlich aus bereits validierten `TileDiagnostic`-Footprints. Die letzten werfenden Aufrufe von `Shared.GameBuildingFootprint`, die alte Rechteck-Hilfslogik und der tote Orphan-Hashpfad wurden aus dem Provider entfernt. Native Hookadressen, Such-Hotpath, Richtungsfilter und PCL-Verhalten sind von dieser Korrektur unverändert.

## KI-Zielwahl vor der Bewegungssuche

Der jüngste 7-KI-Lauf war technisch stabil und belegte den Bewegungsfilter unter Last: 53.319 KI-Suchen untersuchten 204.337 gesperrte Kanten, Vanilla fand 2.250 zulässige Umwege und meldete 3.856-mal `NoRoute`. Gegen Ende stiegen Ablehnungen und `aiNoRoute` jedoch gemeinsam stark an. Die KI erteilte weiterhin Befehle gegen Gebäude oder Einheiten innerhalb einer geschlossenen Burg; erst die nachgeschaltete Bewegungssuche verweigerte den Weg. Die wartenden Soldaten waren daher kein Fehler des Richtungsfilters, sondern eine bisher ungefilterte taktische Zielwahl.

Der vollständige `FBCB9319`-Baselinefluss beginnt für diesen Fall im Planer `0x10B870`. Zustand `0x419` ruft ausschließlich `0x113BC0` auf. Diese Funktion sucht zunächst mit `0xF17F0` im Radius 15 nach einem Gebäude und mit `0xEF0D0` im Radius 10 nach einer Einheit. Ein Treffer wird über `0x11E960` als Gebäude- beziehungsweise Einheitenangriff veröffentlicht. Ohne Treffer löscht Vanilla den alten Auftrag, aktiviert über `0xC4BF0` seinen bestehenden Gate-Sonderzustand und sucht mit `0xEE980` nacheinander eine Fallbackposition in den Radien 6, 30 und 50. Erst danach entstehen über die unveränderte Befehls- und Bewegungsverarbeitung die bereits gefilterten Aufrufe von `0x196280 → 0xF4930`.

Die drei lokalen Fluten berücksichtigten bislang keine spielerabhängige Gatepolicy. `0xF17F0` und `0xEF0D0` lesen Nachbaroffsets und globale Tileflags, aber nicht Vanillas globales Direction-Grid; `0xEE980` liest zwar dieses Grid, kennt jedoch ebenfalls keinen angreifenden Spieler. Deshalb besitzt `0x113BC0` nun einen eigenen querylokalen Scope. Der Spieler wird aus dem nativen Tribe-Record `tribeId * 0x688 + 0x2C` gelesen und gegen den unveränderlichen Tribe-/Player-Snapshot geprüft. Ungültige oder widersprüchliche Werte bleiben fail-open.

Drei native Adapter filtern Vanillas vorhandene Fluten: `0xF1910–0xF1920` für Gebäude, `0xEF1F0–0xEF200` für Einheiten und `0xEEAD0–0xEEAE1` für die Fallbackposition. Sie lesen ausschließlich den bereits gebundenen Maskenpointer, prüfen Tilegrenzen und verknüpfen das vorhandene Richtungsbit mit der Gate-/Bridge-Maske. Gebäude- und Einheitenkanten springen bei einer Sperre zu Vanillas jeweiligem Ablehnungspfad; die Fallbacksuche löscht das Richtungsbit vor Vanillas eigenem `TEST`. Es entstehen weder zusätzliche PCLs noch eine zweite Flut, Ersatzroute oder modseitig gewähltes Angriffsziel. Vanillas bestehende Mauer-, Gate-, Belagerungs- und Burggrabenaufgaben bleiben für die weitere Reaktion verantwortlich.

Der taktische Scope verwendet separate native Threadslots, sodass verschachtelte Bewegungssuchen ihren bisherigen Context unabhängig behalten. Kanten- und Erstbeispiele werden nur im Threadslot gesammelt und nach Ende der gesamten taktischen Suche übernommen. Scope-Detour und alle drei Adapter gehören zur atomaren Same-PCL-Transaktion; ihre realen RedBird-Verdrängungen müssen exakt `17/17/18` Byte betragen. Die Diagnose unterscheidet künftig `attackApproachQueries` von `aiTacticalTargetQueries` und zählt gefilterte Gebäude-, Einheiten- und Fallbackkanten getrennt.

## Begrenzte A/B-Diagnose des normalen KI-Angriffs

Der anschließende Lauf zeigte, dass die vorstehende Analyse zu eng abgegrenzt war: Der spezielle Plannerzustand `0x419 → 0x113BC0` wurde nicht ausgeführt (`aiTacticalTargetQueries=0`), während der normale Angriff weiterhin tausende durch die Gatepolicy verursachte `NoRoute`-Ergebnisse erzeugte. Der erneut vollständig geprüfte normale Kontrollfluss beginnt bei `0x11A980`. Ein vorhandenes Angriffsziel führt dort zu `0x11E960` mit Befehl 4 (`AttackUnit`); die alternative Zielliste kann Befehl `0x20` (`Unknown32`) veröffentlichen. Erst innerhalb `0x11E960` folgen `0xDBC60`, die Verteilung auf einzelne Einheiten und deren `0x196280`-Aufrufe. `0x196280` verwendet `0xF4930`; bei einem Ergebnis kleiner eins setzt `0x199CD0` nur den Bewegungszustand der einzelnen Einheit zurück. Der übergeordnete Gruppenauftrag wird in diesen Zweigen nicht zurückgenommen. Der Fallback `0xEDF30` gehört ebenfalls zu `0x11A980`, ist aber nicht die erste Entscheidung für ein bereits vorhandenes Innenziel.

Um den ersten Unterschied zwischen einem physisch geschlossenen Tor und einem offenen, für den Angreifer feindlichen Tor belastbar zu finden, beobachtet der Testmod nun das vorhandene Script-Extender-Ereignis `OnTribeIssueOrderWithTarget`. Es umschließt exakt Vanillas `0x11E960`; der Mod verändert weder Argumente noch Rückgabewert und unterdrückt keinen Auftrag. Erfasst werden ausschließlich KI-Aufträge der Typen `AttackUnit`, `Unknown32`, `AttackBuilding`, `AttackWallTileId`, `DigMoatTileId` und `AttackTilePosition`.

Der Pre-/Post-Kontext ist pro Thread bis Tiefe acht verschachtelbar. Innerhalb dieses Scopes werden die ohnehin stattfindenden `F4930`-Aufrufe aggregiert: Anzahl, Erfolg, durch die Richtungsmaske erzwungener Vanilla-Umweg, `NoRoute` und berührte Policykanten. Pro Tribe, Befehl und Ziel wird höchstens ein vollständiges Beispiel in einem festen Pool von 32 Einträgen behalten und erst im Deferred-Pfad formatiert. Das Beispiel enthält repräsentative Leader-Einheit, Quell-/Zielkoordinaten und -PCL, Policyfingerprint sowie das passende Tor mit Owner/Capturer, `IsOpen`, Entry-/Exit-PCL, Vanilla-GatePath-Bytes und den modseitigen Maskenbytes. Periodische Checkpoints enthalten nur Summen und Deltas. Es gibt keine Ausgabe pro Einheit oder Kante.

Diese Diagnose startet keine Suche, erzeugt keine PCLs und baut keine Route. Der spezielle `0x419`-Scope bleibt funktional vorhanden, wird in den Logs aber ausdrücklich als `plannerState0x419` bezeichnet und nicht mehr als Abdeckung der normalen KI-Zielwahl ausgegeben. Der folgende A/B-Lauf entscheidet ergebnisoffen, ob die endgültige Korrektur eine vorab erzeugte spielerabhängige Regionssicht, einen vorgelagerten Vanilla-Branch oder den Filter einer bereits vorhandenen Vanilla-Suche benötigt.

## Gemeinsamer Betrieb mit BugfixesAndQoL: Buildstand, Laufzeittest offen

Für die hashgleiche Native-Baseline wurden die fünf überlappenden Funktions-Einstiege `F4930`, `DBC60`, `DA020`, `123090` und `195E30` dem bereits geladenen BugfixesAndQoL zugeordnet. Der Testmod installiert in diesem Modus dort keinen zweiten Detour. Seine übrigen Scopes und Inlineadapter behalten ihre geprüften Vanilla-Einstiegsbytes und RedBird-Spannen. APIShared hält nur eine optionale, einmal registrierte Policyreferenz; ohne Testmod bleibt sie leer. Bei Kartenende veröffentlicht der Testmod eine leere Maske.

BugfixesAndQoL bindet bei vorhandener Maske den Spieler-Scope um seine bestehenden Such-Hooks und prüft selbst erzeugte Routen über denselben gerichteten Kantenfilter. Der rekonstruierte Einheitspfad ist in diesen Scope eingeschlossen. Die normale KI-Zielwahl aus `11A980` bleibt eine separate offene Untersuchung.

Am 30.09.2026 bestanden die APIShared-Tests, die vollständigen BugfixesAndQoL-Buildtests einschließlich Movement-Regression ohne registrierte Gatepolicy und die Policytests des Testmods. Alle drei DLLs wurden durch ihre `build.bat` installiert; lokale und installierte SHA-256-Werte stimmen jeweils überein. Das letzte Spielprotokoll stammt von 22:00 Uhr und liegt vor diesen Builds. Gemeinsames Laden und die Tor-, Cursor-, KI-, `NoRoute`- und Kartenwechselpfade sind daher noch nicht im Spiel bestätigt.

## Korrektur der falschen Cursor-Sperre am eigenen Bergfried

Im folgenden gemeinsamen Spielstart ab 22:29 Uhr waren für Spieler 1 nur fremde Torhäuser maskiert (neun Tore, Owner 2 bis 8, 540 gerichtete Kanten). Dennoch ersetzte der Cursorwrapper 416 positive PCL-Ergebnisse durch null; 184 Treffer stammten aus dem negativen Same-PCL-Cache. Ein fehlgeschlagener maskierter `DB650`-Aufruf samt mindestens einer untersuchten Sperrkante beweist nicht, dass gerade diese Kante die Zielsuche scheitern ließ. Der breite Cache übertrug die Entscheidung zusätzlich auf andere Zielkoordinaten desselben PCL. Diese beiden Schlüsse waren für den eigenen Bergfried unzulässig. Die 66 `invalidPlayer`-Diagnosen stammten aus offenen BugfixesAndQoL-Native-Kontexten; Ausnahmen und Threadslotkonflikte wurden nicht beobachtet.

Die hashgleiche Native-Baseline bestätigt für `DB650` mehrere unabhängige Null-Rückgaben und die vier Richtungsleser `DB857`, `DB947`, `DBA36`, `DBB26`. Der Cursor vergleicht nun für identischen Spieler, Unit, Start und Ziel zuerst eine Suche mit explizit leerem Maskenslot und danach nur bei Referenzerfolg die gefilterte Suche. Nur `Referenz > 0`, `gefiltert = 0` und wirklich verworfene Policykanten begründen eine rote Entscheidung. Die zweite Suche läuft zuletzt, damit ihr nativer Suchzustand der letzte ist. Bei Ausnahme, Scopefehler oder Snapshotwechsel bleibt Vanillas Cursorentscheidung bestehen. Negative Cachetreffer verlangen nun auch identische Start- und Zielkoordinaten, Unit-Global-ID, beide PCLs und Policyfingerprint. Ungültige BugfixesAndQoL-Spieler-IDs betreten keinen Gate-Scope mehr. Die bisherigen Aussagen oben zu einer einzelnen `DB650`-Suche und einer auf andere Ziele übertragenen roten Same-PCL-Entscheidung sind durch diesen Abschnitt ersetzt.

Nach den JSON-, Lifecycle-, XAML-, Hookmutations- und CRLF-Prüfungen liefen beide vorgesehenen `build.bat`-Treiber am 30.09.2026 erfolgreich. Der Testmod bestand 1004 Policy-Assertions; die vollständige BugfixesAndQoL-Suite einschließlich der Movement-Regressionen bestand ebenfalls. Für beide Mods stimmen lokale und installierte DLL-SHA-256-Werte überein. Das BepInEx-Log wurde zuletzt um 22:32:40 geändert und enthält deshalb noch keinen Lauf mit dieser Korrektur. Der Bergfried, ein gesperrtes fremdes Tor, ein echter Umweg, `NoRoute` und Kartenwechsel bleiben praktische Abnahmepunkte.

## Verlustarme Diagnose für zwei Tore und sieben KI-Lords

Der bisherige Pool von 32 vollständigen Angriffsbeispielen konnte spätere Befehle und Torwechsel verdecken. Er wurde durch unbegrenzte semantische Zähler ersetzt. In jedem Zehn-Sekunden-Fenster wird pro tatsächlich beobachteter Kombination aus Spieler, exakter Tor-Building-ID, Stufe, Ergebnis und Befehl eine kompakte Zeile ausgegeben; erster und letzter Tribe-/Zielwert sowie Zielwechsel bleiben erhalten. Die Rohzahlen für Pre-/Post-Ereignisse und Diagnosefehler sind separat sichtbar. Ein synthetischer Regressionstest speist 160 unterschiedliche Befehle mit wiederholtem Wechsel zwischen den Building-IDs 101 und 202 ein und verlangt 320 vollständig gezählte Beobachtungen in vier Aggregatzeilen.

Der Tor-Precheck meldet Vanillas und die durch die Policy resultierende Sperrentscheidung mit der nativen Building-ID. Nur gegen den aktuellen Snapshot bestätigte Toridentitäten werden als konkrete Tore bezeichnet. Richtungsfilter-Treffer und andere Prechecks bleiben nicht zugeordnet. Die bereits vorhandenen Script-Extender-Ereignisse um `0x11E960`, Tribe-Move und `0x196280` werden beobachtet, ohne ihre Argumente oder Rückgaben zu ändern. Ein kurzlebiger verschachtelter Aufrufkontext zählt die innerhalb eines Einzelbefehls beobachteten Builder-Suchen. Jede Builder-Suche hält ihre eigenen tatsächlich geprüften Tor-IDs fest und wird als kein, ein oder mehrere Tore klassifiziert. Wechsel zwischen diesen Kategorien innerhalb desselben laufenden Tribe-Auftrags erhalten eigene Zähler sowie erste und letzte Kategorie. Die Kategorien bezeichnen ausschließlich Prüfungen, weder eine ausgewählte Route noch einen an ein Tor erteilten Befehl. Spieler-Scope-Abweichungen werden nach Aufrufquelle und beiden IDs aggregiert. Es gibt keine dauerhafte Verfolgung von Einheiten und keine neue native Hookstelle.

Der folgende Spielvergleich muss noch zeigen, welche KI-Entscheidungsstufe bei einem physisch geschlossenen Tor erstmals von einem für den Angreifer policygeschlossenen, physisch offenen Tor abweicht. Erst danach ist eine Verhaltensänderung begründbar. Die ältere Beschreibung eines festen Acht-Ebenen-Kontexts und 32er-Beispielpools oben ist durch diesen Abschnitt ersetzt. Am 30.09.2026 bestanden nach JSON-, Lifecycle-, XAML-, Hookmutations- und CRLF-Prüfung die 1006 Policy-Assertions; der Testmod wurde über `build.bat` ohne Warnungen oder Fehler gebaut und installiert. Die lokale und die installierte DLL haben den gleichen SHA-256-Wert. Der Spielstart um 23:49 Uhr fand vor der letzten Installation statt und lud den Testmod nicht; er belegt deshalb weder die neue Diagnose noch den A/B-Vergleich.

## Offenes gegen geschlossenes Tor: Native-Vertrag und noch fehlender Messwert

Die installierte DLL stimmt weiter mit Baseline `FBCB9319...` überein. Die erneute Native-Prüfung der bereits bekannten Tor- und Auftragswege identifiziert `0xC4BF0` als Vanillas globale, temporäre Gate-Passierbarkeitsumschaltung. Modus 1 speichert den bisherigen Gatezustand im Gebäudefeld bei Offset `0xCC`, setzt das Zustandsbyte bei `0x2A2` auf 2 und schaltet den Path-Connection-Record aus. Modus 2 setzt den Zustand auf 0; Modus 0 stellt den gespeicherten Zustand wieder her. Alle drei Wege aktualisieren über `0xD8CE0` die Tor-Richtungsbits. Der PCL-Neuaufbau `0xE49D0` ruft Modus 1 vor und Modus 0 nach dem Aufbau auf; `0x113BC0`, `0x113E00` und `0x113F60` verwenden dieselbe Umschaltung für lokale taktische Suchen. Diese globale Funktion darf nicht als spielerabhängiger Testmod-Eingriff missbraucht werden. Der Kommentar zu `GatePathOverrideMode` im Script Extender bezeichnet den Speicherplatz bei `0xD4` als gesicherten Zustand; die dekompilierte `0xC4BF0`-Implementierung verwendet dafür `0xCC`. Das ist vor jeder API-Nutzung erneut zu verifizieren.

`0xE2610` prüft `r_IsEnabledOrOpen` schon vor Besitzer-/Erobererprüfung und vor dem bestehenden Gate-Precheck. Ein physisch geschlossenes Tor kann deshalb einen anderen Suchpfad auslösen, ohne dass der bisherige Capturer-Hook es überhaupt sieht. Der Auftragsweg `0x11A980 -> 0x11E960 -> 0x196280 -> 0xF4930` und die separaten Burggraben-Selektoren `0x69D60/0x6AF60` bleiben für die spätere Kausalzuordnung maßgeblich. Die Logs des offenen 7-Lords-Laufs enthalten 2.366 KI-`NoRoute`-Ergebnisse und 2.359 fehlgeschlagene Einzelbewegungen mit zuletzt gespeichertem `MoveHerePosition`; der vom Nutzer als physisch geschlossen beobachtete Lauf enthält 54 beziehungsweise 86. Die 54 KI-`NoRoute`-Ergebnisse entstanden in dessen ersten zehn Sekunden; bis zum Ende bei 8.933 KI-Suchen kamen keine weiteren hinzu. Das frühere Topologiefeld `open=True` las nur `r_IsEnabledOrOpen` zu einem deferred Zeitpunkt und beweist keine physische Torstellung. Ebenso bezeichnet `vanilla=closed` beim Precheck den Capture-Vergleich, nicht den sichtbaren Zustand.

Die nächste Diagnose liest an den bereits vorhandenen Tribe-Ziel-/Bewegungsereignissen und periodischen Checkpoints für bestätigte Tor-/Zugbrücken-Kombinationen die unveränderten Rohwerte `r_GateState`, Offset `0xCC`, `r_AIWalkableState`, `r_IsEnabledOrOpen` und die aktuellen Entry-/Exit-Richtungsbytes. Sie erzeugt weder eigene Suche noch einen neuen Hook. Ein erneutes offenes/geschlossenes Testpaar muss damit zuerst den echten Zustand von Tor 819 und 826 und den frühesten unterschiedlichen Vanilla-Rückgabepfad belegen. Bis dahin ist kein zusätzlicher Verhaltenshook festgelegt.

Am 01.10.2026 bestand der Testmod nach den statischen JSON-, Lifecycle-, XAML-, Hookmutations- und CRLF-Kontrollen 1007 Policy-Assertions. `build.bat` baute und installierte ihn mit null Warnungen und null Fehlern; lokale und installierte DLL haben denselben SHA-256-Wert `87788CFE6999E41BF572F6FE04505A4381E311124F7D91211485A39C77DF8CAF`. Die Laufzeitwirkung und die erste unterschiedliche KI-Entscheidungsstufe sind damit noch nicht gemessen.

## Vergleich `test_gates.sav` und nächste lesende Ergebnisdiagnose

Das archivierte `Log_212.log` enthält zwei Karten-Epochen desselben Saves und derselben Building-/Global-IDs 819/2423576 und 826/2423362. In Epoche 1 (01:06:54–01:07:29) hatten beide Verbindungen den Wert 1; die Rohzustände der Tore waren 3072 beziehungsweise 0. In Epoche 2 (01:08:12–01:09:07) hatten beide Verbindungen den Wert 0 und beide Torzustände 3074. Das Topologie-Snapshot meldete in beiden Epochen denselben Fingerprint und anfangs `connectionEnabledAtSnapshot=True`; dieser Startwert ist somit ausdrücklich keine spätere Live-Zustandsmessung. Die KI-Builder meldeten für Bewegungsbefehl 3 insgesamt 1774 `NoRoute` in Epoche 1, aber 100 in Epoche 2, sämtlich im ersten Zehn-Sekunden-Fenster. Die Tor-Prechecks sahen in beiden Fällen bereits die geschlossene Policy. Dieser Vergleich belegt noch keinen sicheren Verhaltenshook.

Die früheren `diagnosticErrors` von 9478 beziehungsweise 13325 bestehen exakt aus `unresolved-player:unit-move` und `unresolved-player:tribe-move`. Solche nicht zuordenbaren Kontexte werden nun separat und ohne Integritätsfehler gezählt; echte Ausnahmen, Scope-Abweichungen und nicht gepaarte Ereignisse bleiben Fehler. Der installierte BugfixesAndQoL-Detour besitzt `0xE2610` allein und kann bei registrierter Testpolicy dessen Vanilla- und effektives Ergebnis an einen optionalen lesenden APIShared-Beobachter melden. Ohne Testmod findet kein Gatebeobachteraufruf statt. Die Testmod-Aggregate ordnen die Ergebnispaare dem aktuellen kurzlebigen Auftrag oder ausdrücklich keinem Auftrag zu; sie behaupten keine konkrete Torwahl ohne Building-ID.

Native Befehl 7 ruft `0xE7F60` am Aufrufort `0x120E9D` auf. Bei Rückgabe null springt `0x11E960` vor der Arbeitszuweisung zurück, bei Erfolg fällt es in die Behandlung von Befehl 6. Die RedBird-Mindestüberschreibung von 14 Byte würde an diesem Aufrufort `CALL`, `TEST`, den bedingten `JE` und eine folgende `MOV` verdrängen. Deren bedingter Sprung kann bei `BeforeCallback` bereits vor einer Beobachtung von `EAX` abzweigen; der Ort bleibt daher ohne neuen Inline-Hook. Der Rückgabewert von `0xE7F60` bleibt eine ausdrücklich fehlende Messung. Script Extender und lokaler Fixes-Mod besitzen dort keinen geeigneten publizierten Ergebnis-Eventpfad. Eine Verhaltensänderung wartet auf den ersten eindeutig belegten Ergebnis- und Auftragsunterschied im nächsten gepaarten Lauf.

## Korrektur der Capture-Adapter am 01.10.2026

Auditgrundlage ist unverändert die installierte Native-DLL mit SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Die vollständig geprüften 20-Byte-Blöcke bei 0xE2705 und 0xE3024 führen MOVSXD, IMUL und CMP aus; danach folgt ausschließlich die ZF-Abfrage durch den ursprünglichen JE. Besitzer-/Allianzannahme überspringt diese Blöcke. BugfixesAndQoL bleibt Besitzer des Funktionsdetours 0xE2610; Script Extender und lokaler Fixes-Mod besitzen keinen überschneidenden Hook an den beiden Innenstellen.

Die installierte RedBird.X64 1.5.0.0 verändert beim Standard-ContextWrapper nach POPFQ durch ADD RSP,144 erneut die Flags. Die frühere Aussage, das Schreiben von Rflags stelle den Vanilla-Vergleich für den anschließenden JE wieder her, ist daher falsch. Historische originalZF/finalZF-Logs zeigen die beabsichtigte Callbackentscheidung, keinen Beleg für den tatsächlich ausgeführten Sprung. Die neuen Inline-Adapter führen den nativen Vergleich einmal aus, sichern SETNE in R11b bei gesichertem Original-R11 und erzeugen erst nach Callback-/Stackbereinigung durch TEST die endgültige Sprungbedingung. POP R11 und der Backend-Rücksprung verändern diese Flags nicht. Leere Policy, ungültige Spieler und Callbackfehler erhalten den gesicherten nativen Vergleich. GPRs und XMM0..5 werden über den Callback erhalten; Delegates bleiben im statisch verwurzelten Runtimeobjekt.

R9 zeigt auf Offset 0x18 des aktuellen 0x204-Byte-Verbindungsrecords. Component A/B/C liegen relativ dazu bei +0x1C/+0x20/+0x1D0. Die vorherigen negativen Diagnoseoffsets lasen jeweils den Vorgängerrecord und dürfen nicht zur Torzuordnung verwendet werden. Der Maschinentest belegt die korrigierten Zugriffe mit verschieden belegten Nachbarrecords.

Die isolierten Tests mit dem tatsächlich installierten X64InlineHook führen beide echten JE-Ergebnisse aus (je 20 Kombinationen aus nativer Gleichheit/Ungleichheit, leerer Policy, fremder/berechtigter Eroberung, ungültigem Spieler und Callbackfehler). Sie prüfen die 20-Byte-Verdrängung, das installierte Kontextlayout, GPR-/XMM-Erhaltung und Stackgleichstand. Der bestehende Policy-Test bestand mit 1004 Assertions. Diese Belege bestätigen den Adaptervertrag, aber noch nicht Vanillas alternative KI-Auftragswahl im Spiel. Dafür bleibt der offene/geschlossene Vergleich desselben test_gates.sav erforderlich; die E7F60-Ergebnismessung bleibt offen.

Buildnachweis: Am 01.10.2026 um 14:12 baute und installierte ausschließlich der vorgesehene build.bat-Treiber den Testmod, mit null Warnungen und null Fehlern. Vorprüfungen und Maschinentests wurden dabei erneut ausgeführt. Lokale und installierte DLL stimmen im SHA-256 69C460FE4C2A8D0046045C4775C840A9D3F8F509BAD06585534683BA374E82F7 überein. Modversionen wurden nicht geändert. Die Prüfung im Spiel und der Hauptmod-Vergleich ohne Testmod stehen weiterhin aus.

## Bestaetigter Spielvergleich und Torrollen-Diagnose (01.10.2026)

Der Nutzer bestaetigte drei aufeinanderfolgende Laeufe desselben test_gates.sav nach Installation der post-cleanup-test-Adapter. Die damalige LogOutput.log-Sitzung begann um 16:13; Zuordnung ueber Mission-Epochen und Savepfad:

| Epoche / Zeitraum | Ausgangslage laut Nutzer | Beobachtung laut Nutzer | KI-Suchen / NoRoute | Scope-Abweichungen |
| --- | --- | --- | --- | --- |
| 1: 16:13:17.405 - 16:14:00.037 | Tore offen | Macemen fuellen den Burggraben | 8888 / 0 | 26 |
| 2: 16:14:06.615 - 16:14:48.171 | Tore geschlossen | Macemen fuellen den Burggraben | 8612 / 0 | 9 |
| 3: 16:14:54.304 - 16:16:05.433 | Tore offen plus echter offener Zugang | Gegner benutzen den echten Zugang | 9554 / 0 | 3 |

Die Verbindungsmessungen von Tor 819/826 bestaetigen anfangs 1/1, 0/0 und 1/1. Spaetere Torstellungswechsel erscheinen ebenfalls; die Ausgangslage ist keine Behauptung einer konstanten Torstellung ueber die ganze Epoche. Alle drei Epochen hatten snapshotErrors=0, callbackWarnings=0 und exceptions=0. ExcludeForeignCapture wurde nicht beobachtet: Der neue Adapter stellte hier Vanillas nativen Vergleich wieder korrekt her. Die erfolgreiche Verhaltenskorrektur ist belegt; eine Eroberungs-/Allianz-/Langzeitabnahme folgt daraus nicht. runtimeIntegrity=FAIL blieb wegen der Scope-Abweichungen bestehen; die Log-Aggregate ordneten sie shared-building mit Suchwert 1 gegen Tribe-Spieler 5 zu.

Die naechste Diagnose fuehrt ownerRelation und captureRelation unabhaengig, mit raw owner/captured IDs, Building-/Global-ID, policyDecision, edgePolicy und classificationSource. Eigene Tore koennen damit gleichzeitig captured-by-other sein. Unbekannte Identitaeten oder veraltete Owner-/Capture-Snapshots werden unknown mit Ursache; keine Diagnose bestimmt die Runtime-Policy. Live-Zeilen erfassen jeden identifizierten Gate-Snapshotrecord einmal pro Spieler/Beobachtung, mit optionalen Bruecken. Alle acht Spieler erhalten am bestehenden Zehn-Sekunden-Checkpoint Abdeckung, unabhaengig von KI-Events. NativeComparison/effectiveComparison bezeichnet Annahme/Ablehnung des Capture-Vergleichs, nicht die physische Torstellung; gateStateRaw und connectionEnabled bleiben getrennt.

Am bestehenden ResolveBuildingPlayer-Pfad werden rawSearchArgument (argumentRole=unverified), Tribe-ID/Global-ID/Besitzer, verwendeter Spieler und der aktuelle kurzlebige Auftragskontext aggregiert. Die bisherigen 26/9/3 Konflikte sind synthetisch als exakte Zaehlregression abgedeckt. Weder Spielerersatz noch Downgrade der Integritaetsfehler erfolgt. Vor einer spaeteren Resolveraenderung ist die Herkunft des sechsten DA020-Arguments fuer beide Caller und die nachgelagerten E2610-/Alternativsuchen zu belegen. Die Bezeichnung movementClass im Hauptmod ist kein solcher Nachweis. Kein neuer Hook, keine Wegsuche, kein dauerhaftes Unittracking und keine Ereignisobergrenze wurden eingefuehrt. APIShared und BugfixesAndQoL bleiben in diesem Schritt unveraendert.

Buildnachweis fuer diese Diagnose: build.bat am 01.10.2026 um 16:43, 1117 bestandene Assertions und beide nativen Adapter-Maschinentests; null Warnungen und Fehler. Vorpruefungen fuer JSON/Lifecycle, workspaceweite Hookmutationen, XAML und CRLF bestanden. Neue lesende Verwendung von GameTribe.r_GlobalId (public uint) und r_PlayerIdOwner (public ushort) gegen die installierte SHCDESE.dll geprueft; keine neuen Assembly-CSharp-Zugriffe. Lokale/installierte DLL SHA-256: D1892A0DF9BE3DFF1D9FACF2DADB1441381B9804ADDCE16DBD89CFDF7BB168F2. Version 0.1.5 unveraendert; APIShared/BugfixesAndQoL wurden in diesem Schritt nicht bearbeitet. Abnahme der neuen Logklassifizierung im Spiel und Hauptmodvergleich ohne Testmod stehen aus.

## Eroberungslauf und bestätigte Snapshot-Aufholung (01.10.2026)

Der vom Nutzer zugeordnete erste Lauf der LogOutput.log von 23:03:24.188 bis 23:04:54.934 lud test_gates.sav. Tor 819 / Global-ID 2423576 / ursprünglicher Besitzer 1 wurde laut Spielbeobachtung nach Eroberung korrekt benutzt. Die Capture-Prechecks bewahrten 6270 Vergleiche für den Eroberer selbst und 53515 für dessen Verbündete. Die ursprüngliche Besitzerrolle und die Erobererrolle werden getrennt protokolliert. Verbündete ursprüngliche Torbesitzer bleiben statisch geprüft, im Spiel nicht beobachtet; dieser Lauf belegt keine Freigabe für unbeteiligte dritte Spieler.

Bei insgesamt 16835 KI-Suchen blieben die vier frühen NoRoute ab 23:03:43 konstant. snapshotErrors, callbackWarnings und Exceptions waren null. 43 CaptureMismatch (30 PCL / 13 Builder) und zwölf shared-building-Scope-Konflikte (roher Suchwert 1 gegen Tribe-Spieler 5) bleiben getrennte Rohbefunde. Die bisherigen Logs beweisen nicht rückwirkend, dass alle 43 Abweichungen später exakt aufgeholt wurden. Die zweite Sitzung ab 23:06:21 zeigt uncaptured und ist kein Gegenbeleg zum vom Nutzer zugeordneten ersten Lauf.

Die neue Diagnose zählt jede identitätsgeprüfte CaptureMismatch nach Karten-Epoche, Quellgeneration, Building-/Global-ID, Besitzer und altem/neuem Eroberer, ohne Ereignis- oder Musterobergrenze. Sie setzt eine Interlocked-Refresh-Anforderung; mehrere native Beobachtungen werden bis zum nächsten bestehenden deferred Durchlauf gebündelt. Im Callback werden keine zusätzlichen Spielabfragen oder Suchänderungen ausgeführt. Access und Topologie werden am sicheren Publisherpfad zur Aktualisierung fällig. Stabile Access-Veröffentlichungen tragen eine pro Epoche fortlaufende Generation; auch eine angeforderte äquivalente Policy wird frisch veröffentlicht.

Bestätigte Aufholung verlangt eine nach der Beobachtung erfolgende stabile Veröffentlichung mit exakt derselben Toridentität, demselben Besitzer und dem zuvor nativ beobachteten Eroberer. Andere Werte, alte Generationen und wiederverwendete Building-IDs liefern keinen Beweis. Rohzähler bleiben unverändert; die Integritätswertung zieht ausschließlich bestätigte Aufholungen ab. Nicht aufgeholte Übergänge bleiben im letzten Karten-Checkpoint sichtbar. Scope-Konflikte, Identitätsfehler und Ausnahmen bleiben Fehler. Keine neuen Hooks, öffentlichen APIShared-Schnittstellen, Spielmemberzugriffe oder Hauptmodänderungen; Version 0.1.5 bleibt unverändert.

Der erneute Caller-Abgleich des unveränderten Native-Builds FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 bestätigt: 0x30620 übergibt den zuvor im Zielplanungsweg ermittelten Rohwert als sechstes Argument an 0xDA020; 0x11E960 verwendet dort für Befehle 9/0x26 einen leaderbezogenen ushort-Recordwert, sonst null. 0xDA020 reicht diesen Wert an 0xE2610 weiter und besitzt daneben den Same-PCL-/0xE2CA0-Pfad. Diese Herkunft rechtfertigt noch keine allgemeine Gleichsetzung mit dem Tribe-Besitzer. Der Gebäude-Resolver bleibt unverändert fail-open; die semantische Parameterklärung bleibt offen. Die auditierten Capture-Adapterspannen bei 0xE2705 und 0xE3024 sowie die installierten Script-Extender-/RedBird-/Fixes-Hookbesitzer bleiben unverändert.

Verbleibende Spielabnahme: Eroberung und Rückeroberung; eigener Tor-/Bergfriedzugang nach gesperrtem Fremdziel; mehrere KI-Angriffswellen; erneutes Laden desselben Saves; kurzer Hauptmodlauf ohne Testmod. Ein zusätzlicher Test der Erobererverbündeten ist aufgrund der beobachteten Aufrufe nicht erforderlich.
Buildnachweis: build.bat baute und installierte am 01.10.2026 um 23:31 ausschließlich den Testmod mit null Warnungen und Fehlern. 1583 Assertions einschließlich beider installierten RedBird-Adapter-Maschinentests sowie JSON-/Lifecycle-/Hookmutations-/XAML-/CRLF-Prüfungen bestanden. Lokale und installierte DLL: SHA-256 68C520CFE85C8220CA7055A40DDFA2555623BDBAD584FF5378642CC3ED701157. Neue Spielmemberzugriffe: keine. Die Spielabnahme der neuen Refresh-/Aufholungsdiagnose steht aus.


## 2026-10-02: unmittelbarer Cursor und native Formationsfreigabe

Logbefund vom 01.10.: Editor-Epoche 23:48:47.548–23:50:15.458: 115 bewiesene
Gateblockaden, 685 erzwungene Cursorablehnungen, 835 fehlende passende
Cachetreffer, 208 Doppelprüfungen, 21 erfolglose Referenzen; mittlere Prüfdauer
0,229 ms, Maximum 0,503 ms. Keine Scope-Konflikte oder Exceptions, Integrität PASS.
Der Nutzer bestätigte durch deaktivierte Formation, dass die grünen Zielpunkte
aus der Formationsvorschau stammen. KI-Epoche 23:50:24.292–23:51:41.890:
9.132 KI-Suchen, 0 NoRoute; Tor 819/global 2423576, Eroberer 6→7,
Generation 5→6 exakt aufgeholt. Ein Gebäude-Scope-Konflikt (Suchwert 1,
Tribe-Besitzer 5) bleibt offen; keine Exceptions oder Snapshotfehler.

Der bestehende Cursoradapter prüft bei Cachefehler sofort eine repräsentative
Vanilla-Einheit: leere Referenzmaske zuerst, Policysuche zuletzt, beide Scopes
mit finally. Nur Referenzerfolg + Policyfehler + verworfene Gatekante sperren.
32 exakte LRU-Cacheplätze mit maximal 200 ms Alter beschränken nur gespeicherte
Ergebnisse; verdrängte Ziele werden neu geprüft. Schlüssel: Spieler, Unit-/Global-ID,
Start/Ziel, beide PCLs, Karten-Epoche, Policygeneration und Maskenfingerprint.
Verschachtelung, ungültiger Kontext, Ausnahme oder Snapshotwechsel bleiben offen.
Der bisherige periodische Cursor-Doppelprüfpfad entfällt. Aggregierte Suchzahl und
Prüfdauer bleiben erhalten; cursorValidationRequests/Deferrals ersetzen die
historischen Request-/Throttle-Bezeichnungen.

BugfixesAndQoL bindet nur zusätzliche Formationsvorschauen an die fertige native
Bodenfreigabe. Der erste bestehende sichtbare Tile-Callback liest einmal pro
Renderdurchlauf; Draw-List-Reset ist dessen Ende. Keine neue Wegsuche, kein neuer
Hook und keine Testmod-/APIShared-Abhängigkeit. Bestätigung gilt für Spieler,
Tribe, Auswahlgröße, Modus und den festen Gestenpunkt. Nicht bestätigte oder
abgelehnte Ausgabe verbirgt die Vorschau. Beim Ziehen bleibt der Befehlsanker fest;
Auswahl-/Karten-/Steuerungswechsel und Loslassen verwerfen die Geste. Native
Vanilla-Befehlsmarker und Overflow-Veröffentlichung bleiben getrennt erhalten.

Runtime-Projekte verwenden die echte installierte Assembly-CSharp.dll. Keine neuen
Spielmemberzugriffe; bereits benutzte öffentliche Unit-/Player-APIs bleiben gleich.
Statische Runtime-/Event-/Hookwurzeln tragen die Arbeit nach Startup-Cleanup;
keine neuen MonoBehaviour-Lifecyclepfade. Hookspannen, RedBird-Adapter und Fixes-/SE-
Besitzer bleiben unverändert. Versionen und README unverändert.

Spielabnahme dieser Änderung noch offen: schnelle erlaubte/gesperrte Hoverwechsel,
kurzer Klick und gehaltene Formation unter beiden Maussteuerungen, eigener
Bergfried, echter Umweg, Kartenwechsel und dieselben Gesten ohne Testmod.


Buildnachweis 02.10.2026: Beide betroffenen build.bat-Treiber abgeschlossen,
DLLs lokal/installiert SHA-256-identisch. Testmod: 1.699 Assertions einschließlich
beider installierten RedBird-Maschinentests, 0 Warnungen/Fehler. Hauptmod:
9.228 Formations-/Queue-Checks und vollständige Treiber-Regressionen bestanden;
0 Fehler, 1 MSB3277-Warnung für MonoMod.Utils-Referenzversionen. Der alte
Quelltexttest im Hauptmod-Nativeharness wurde auf die atomare Veröffentlichung
von Markerkacheln und Autorisierungsdelegate aktualisiert; der erneute komplette
Treiber bestand. JSON-/Lifecycle-/Hookmutations-/XAML-/CRLF-Vorprüfungen bestanden.
Testmod DLL: 19C3DAB31C0B5CEE3054E771CBEA98BB5A20F11FA00DA354EEA97322AC0056B6.
BugfixesAndQoL DLL: 4CD2567C4565FAA0E916561B40092A78A593DD897EDBC4117E6397BF00A36036.
APIShared unverändert; keine Versionserhöhung. Spielabnahme weiterhin offen.


## 2026-10-02: Gebäude-Suchspieler statisch geklärt

Der vollständige featurebezogene Audit des unveränderten installierten Native-
Builds FBCB9319 bestätigt zwei unterschiedliche Rollen des sechsten DA020-
Arguments. Zielplaner 0x30620 übergibt den ausgewählten Gegner: Quelle ist das
untere WORD von Tribeoffset 0x620 (N00000580), geschrieben durch 0x2A720.
Es ist nicht r_AttackTargetOwnerPlayerId bei 0x61C. Im Befehlsverteiler
0x11E960 verwenden 9 und 38 das signed WORD des Anführers bei Unitoffset 0x92;
36 verwendet null. Die installierte Interop benennt nur dessen unteres BYTE
als r_ControllableForPlayerId. Die unveränderte 1-basierte Anführer-ID ist
wegen des nativen Unit-Sentinels korrekt; kein Nachbarrecordzugriff.

Vanilla reicht den Suchspieler in beiden DA020-Phasen an E2610 weiter. Die
Same-PCL-Akzeptanz und profilabhängige E2CA0-Alternativsuche können diese
Regionsprüfung umgehen. Die nachgelagerte Kandidatenfilterung über 123090
führt eine eigene anführerbezogene Suche aus. Ein pauschaler Ersatz durch
den Tribe-Besitzer wäre deshalb keine belegte Resolverkorrektur.

Die zwei bisherigen Abweichungen rawSearchArgument=2 / tribePlayer=5 bei
Tribe 4423/global 2417968 ohne Auftragskontext sind mit einem regulären
Planeraufruf vereinbar, aber dessen konkrete Herkunft ist nicht geloggt.
Sie bleiben einzeln offen; keine pauschale Bereinigung der Fehlerwertung.

Vertrag, Schreiber, Branches, ID-Basis und Regressionen sind in
_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/
ENEMY_GATE_BUILDING_CONTEXT.md dokumentiert. Reproduzierbare Prüfung:
64 Native-/Datenflusschecks sowie acht installierte Memberverträge und zwei
Recordgrößen bestanden. Vollständige Belege liegen unter
_inspect/EnemyGateBuildingContextAudit/evidence.txt.

Für diese statisch beantwortete Vertragsfrage keine zusätzliche Runtime-
Diagnose. Suchargumente, Resolverentscheidung, Policy und Integritätswertung
bleiben unverändert; keine neuen Hooks, APIShared- oder Hauptmodänderungen.
Version und README unverändert; kein Build erforderlich. Die nächste
Resolveränderung muss Suchspieler und unabhängig geprüften Bewegungskontext
getrennt behandeln und alle beschriebenen Suchzweige einschließen.


## 2026-10-02: geprüfter Bewegungs-Scope für Gebäude

Gemeinsamer Resolver für standalone DA020 und IEnemyGatePathPolicy.
Der native sechste Parameter bleibt unverändert. Die Gate-Richtungssuche
verwendet stattdessen einen identitätsgeprüften Bewegungsspieler:
Tribe-ID/Global-ID und Besitzer aus dem veröffentlichten Snapshot stimmen mit
den Livewerten überein; Anführer-ID/Global-ID sind verfügbar und sein signed
Kontroll-WORD bei Unitoffset 0x92 stimmt mit dem Tribe-Besitzer überein.
Das WORD wird aus beiden geprüften öffentlichen BYTE-Feldern gebildet.
Identitäts-/Kontrollwechsel während des Lesens bleiben offen.

Der Suchwert muss zur gespeicherten Planungsrolle (signed unteres WORD von
N00000580, außerhalb Editor Ersatz 1 bei null), Anführerkontrolle oder null
passen. Überschneidungen heißen compatible-planner-or-leader; keine
Callerherkunft wird allein aus diesen Werten behauptet. Editorstatus kommt aus
dem vorhandenen Mission-Lifecycle. Der Tribe-Snapshot enthält nun Global-IDs
und wird am Kartenstart zurückgesetzt; sein bisheriger deferred Takt bleibt.

Die existing building-search-context-Aggregate zeigen Rohsuchspieler,
Bewegungsspieler, Planungswerte, Kontroll-WORD, beide Tribeidentitäten,
Anführeridentität und konkrete Fehlerursache. buildingRoleDifferences zählt
geprüfte Rollenunterschiede; buildingContextFailures zählt ungelöste Kontexte.
Letztere, Exceptions und tatsächliche Scope-Konflikte bleiben Integritätsfehler.
Historische Ereignisse werden nicht nachträglich als gelöst gewertet.
Keine neue Wegsuche, zusätzliche Pollingarbeit oder dauerhafte Unitverfolgung.

Gemeinsamer Betrieb braucht keine Hauptmod- oder APIShared-Änderung. Bestehende
Funktionsbesitzer, Inlineadapter, Graphabfragen, Same-PCL-/Alternativpfade und
Kandidatenfilter bleiben erhalten; die beiden Gebäudeeinstiege verwenden den
neuen Resolver und ihre vorhandenen finally-Scopes. Neue Hooks: keine.
Runtime hängt weiterhin an statisch verwurzelten Hooks, R3-Publishern und dem
vorhandenen onBeforeRender-Drain nach Startup-Cleanup. Version 0.1.5 und README
bleiben unverändert. Build- und Testergebnis wird nach dem Treiber ergänzt.

Spielabnahme: bekannte offene Tore versus echter offener Zugang; normales und
Raid-Gebäudeziel innen/außen; eigener Tor-/Bergfriedzugang und Eroberung /
Rückeroberung; mehrere Angriffswellen und Save erneut laden; kurzer Hauptmodlauf
ohne Testmod. Keine neue Verbündetenkarte erforderlich. Neue wiederholte
NoRoute-Serien verhindern die Abnahme und erfordern gezielte Suchpfaddiagnose.


Buildnachweis 02.10.2026: ausschließlich Testmod über build.bat /nopause
gebaut und installiert. 1824 Assertions einschließlich beider installierten
RedBird-Capturer-Maschinentests bestanden; 0 Warnungen und 0 Fehler.
64 native Vertragschecks, zehn öffentliche installierte Feldverträge, zwei
Recordgrößen und die öffentlichen TryGet-Signaturen geprüft. JSON-, Lifecycle-,
workspaceweite und modlokale Hookmutations-, XAML- sowie CRLF-Prüfungen bestanden.
Lokale/installierte Testmod-DLL SHA-256 identisch:
69C349B4892F3B2850CDB351F21BA5DD3AD173CF7D01FDC2F5700B7832F57BB0.
BugfixesAndQoL unverändert: 0CCF91D597E287CA5512FD7D7EAE59A123113ABAEA920182AC2DE65D6FB6377A.
APIShared unverändert: DE37BD7835AC1E76C59AFDB0801FAB17A0E7E4F48F6FDE531C0AFFDA017A342F.
Buildlog: _inspect/EnemyGateBuildingContextAudit/build.log.
Spielabnahme und Laufzeitbeobachtung der neuen Rollendiagnose stehen aus.
Die Scope-Restauration ist unverändert und statisch geprüft; es wird kein
zusätzlicher nativer Ausführungstest der verschachtelten Building-Scopes behauptet.


## Diagnosebereinigung und Spielbefunde, 02.10.2026

Die neue Gebäude-Scope-Fassung wurde in drei aktiven test_gates.sav-Epochen
beobachtet: 13:35:51.837–13:36:47.889 (KI-Zähler 11100, Rollenunterschiede 13),
13:36:51.958–13:37:56.844 (14151 / 14), 13:39:09.823–13:41:29.280 (8888 / 30).
Alle beobachteten Builder-Ergebnisse waren positiv, kein NoRoute, keine
Gebäude-Kontext-/Scope-/Snapshotfehler oder Exceptions; runtimeIntegrity=PASS.
Im zweiten Lauf enthielten die Ergebnisaggregate 14157 positive Beobachtungen,
also sechs mehr als der KI-Eintrittszähler. Das ist eine historische Zähldifferenz,
kein nachträglich aufgeklärter Einzelfall. Native Entry/Exit und Ergebnisobserver
verwendeten unterschiedliche KI-Abfragen; künftig bleibt die Entryklassifizierung
im QueryScope eingefroren und gilt für alle drei Stellen. Originalargumente,
Suchergebnisse, Scopemasken und Hookadressen bleiben unverändert.

Gemeinsame Suchaufrufe zählen Attack, BuildingApproach, BuildingConsumer,
CursorCommand und AI Builder jeweils einmal. BuildingConsumer bleibt im
Kantenzähler dieselbe Suchklasse wie bisher, erhält aber seinen eigenen
Aufrufzähler. Standalone CursorCommand erhält ebenfalls einen Aufrufzähler.

Gate-live-Zustände werden ohne Obergrenze nach Spieler, Building-ID, vollständigem
Zustand und vollständigem Detail intern gespeichert. Global-ID, Owner-/Capture-
Relation, Rohwerte, Policy, Ursachen und Brücken stehen in einer Definition
`Enemy-gate state: epoch=N,id=M,...`; Ergebniszeilen referenzieren `gateState=N/M`.
Definitionen erscheinen vor ihrer ersten Verwendung; Definitionen und aktive
Aggregate werden atomar ausgelesen. Identische Zustände behalten ihre Kennung über
Zehn-Sekunden-Fenster, Änderungen erhalten eine andere Kennung. Jede Observation,
erster/letzter Tribe-/Zielwert und bestehende Wechselzähler bleiben erhalten.
Reset erfolgt nur beim bestehenden Kartenstart; Epoche wird erhöht. Kein Unittracking.

Installierter SE 2.12.0, Tag v2.12.0, Commit f8d51730fcb54b25af43d3c9348d57db058e077f,
Tree 657af449e1397c58e6c5ec054977d83198b68e66 und Assemblyhash
DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF sind auditiert.
Die Mindestabhängigkeit bleibt unverändert; der Log-Versionsmatch ist weiterhin
ein Versionsvergleich und beweist keinen Laufzeit-Hashmatch.

Vanilla FBCB9319 liest Connection-Permissions in 0x181E00 mit 90 Int32 pro Zeile
(IMUL 0x5A, Zeilenabstand 0x168), Klassen 1–6 ab 0x32BDB0. Der aktuelle öffentliche
API-Konstantwert ist 89. Der flache Getter liefert 534 statt 540 Einträge;
unser Vergleich interpretiert den vorhandenen Prefix weiterhin korrekt mit
Native-Stride 90. Es fehlen Profileindex 89 und Permissionindizes 534–539
(Klasse 6, Unitindices 84–89). Alle gelesenen Werte der letzten Läufe stimmten.
Die Warnung bezeichnet jetzt unvollständige Abdeckung, keine Tabellenmutation;
Changed-Value-Warnungen bleiben getrennt. Der öffentliche Row-Getter verwendet
jedoch Stride 89 und adressiert Klassen 2–6 falsch; dies betrifft auch seinen
Can/Set-Verbraucher. Unsere Policy verwendet diese Getter/Setter nicht. Fork und
Hauptmods wurden für diesen Schritt nicht geändert. Vollständige Native-Reader-
Belege: _inspect/EnemyGateBuildingContextAudit/pathfinding-globals-evidence.txt.
Der englische Autorenreport liegt daneben als SCRIPT_EXTENDER_PATHFINDING_REPORT.md.

Logabnahmegrenzen: Alle Pre/Post-Paare und Aggregatsummen stimmten. Der dritte
Lauf beobachtete 12 gefilterte Gebäudekanten. Eroberung, nachgewiesene Cursorsperre
und Raid-Gebäudebefehl 9 wurden in diesen drei Läufen nicht beobachtet. Die
Spielbeobachtungen dieser Epochen sind noch nicht zugeordnet. Ein späterer Lauf
ohne Testmod ist ein eigener Hauptmodvergleich, kein Gatepolicy-Nachweis.
Offen: normaler/Raid-Gebäudeangriff, Eroberung/Rückeroberung, eigener Zugang nach
gesperrtem Cursorziel, mehrere Wellen und beobachteter Hauptmodvergleich. Keine
weitergehende KI-Verhaltensänderung ist aus diesen Logs gerechtfertigt.

Buildabnahme der Diagnosebereinigung: build.bat hat am 02.10.2026 die Fassung
mit 2847 bestandenen Assertions und null Warnungen/Fehlern gebaut und installiert.
Beide installierten RedBird-Capturer-Maschinentests bestanden. Die neuen Tests
prüfen 1500 Beobachtungen über 100 vollständige Torzustände und drei Zeitfenster,
Rekonstruktion, Epochenreset, eingefrorene KI-Zuordnung und unvollständige Tabellen.
Ein erster Testlauf stoppte vor der Installation wegen einer unbeabsichtigten
Änderung am direkten Cursorwrapper; dieser Pfad wurde vollständig wiederhergestellt.
Danach bestanden Tests und sämtliche Vorprüfungen erneut vor dem finalen Treiberlauf.
Lokale/installierte Testmod-DLL SHA-256:
B85CFC510B7912A4D1DF7D6F81ADB7EFB773FBE60B2F41E25853648CF6358C10.
BugfixesAndQoL und APIShared blieben gegenüber dem Stand vor diesem Build identisch
(00A0512B1F94C598B5417B6B5790A396E7B67228B27DEE2AB81E4BA466552CF7 /
02332AF3506C92C47DB066C1C2AC764A312D75E2CAFC144F1FB3B5E27617C6FF).
Version unverändert 0.1.5, README unverändert. Neue Spielabnahme der Ausgabe steht aus.

## 2026-10-02: read-only Assassin gate diagnosis

See the hash-bound baseline `knowledge/ENEMY_GATE_ASSASSINS.md`. Four pre-change runs had 403/402/207/624 NoRoute results; the first three observed open/open, open/closed and closed/closed configurations. Automatic closure in the open runs led to retargeting; no persistent stuck-unit conclusion follows from the counters.

An optional passive APIShared observer measures the existing D9C40 detour, weighted/cache-route edges and native cache contents; it never changes search results. Existing deferred aggregates retain exact counts and numeric sums with no event cap. Gate edge IDs come from mask construction, overlapping identities remain ambiguous. New Unit fields were checked against installed public SE members, and the complete control WORD is reported. Native singleton reads use +0x84/+0x88/+0x90 and ten positive/negative PCL pairs at +0x416D8C/+0x416DDC with stride 8. No new hooks or executable mutations; original route/search/cache algorithms remain unchanged. Native-only flood fields and cache-hit/fallback measurement gaps are explicitly labeled. Versions and README are unchanged.

Abnahme remains open until paired Assassin runs locate the first different reachability result. No additional Assassin movement correction is included in this diagnostic build.

### Diagnostic build verification

APIShared, BugfixesAndQoL and EnemyGatePathfindingTest were built and installed only through their build.bat drivers. Local and installed SHA-256 values match: APIShared BE36060EA2D7270C9EADDF8437F38801E8D67C3CF04CAF2E977EA284066D112B; BugfixesAndQoL 72CA51A2381603F2EBB64F79B4A3D535BB109B55A18861E6DC7DFCA0548AB37D; Testmod D91CEEE9E4E733D1CBC6FBA4390E3D3ACC686D75F28890183011DBB01DBE0D43.

APIShared baseline/preset/consumer tests passed. Assassin A*/Dijkstra tests: 13871 assertions. Testmod: 3655 assertions, including installed RedBird machine tests and 400 read-only route observations across 80 gates. Full Bugfixes movement tests also passed (269682 unit-plan assertions, 6480 building-field comparisons, 18262 independent search assertions, 1469340 cursor connectivity comparisons). APIShared/Testmod builds have zero warnings/errors; the Bugfixes build has one MSB3277 assembly-reference warning and zero errors. JSON/lifecycle/permanent-hook/XAML/CRLF checks passed. No build process remains running.

Building-approach context now retains explicit tribe/building ID, global ID, owner, source PCL and raw search argument even outside a tribe-order event. Installed public GameBuilding owner/global fields are UInt16 +0xD6 and UInt32 +0xD8. Void building searches are not labeled failed boolean searches. The algorithm/cache/publication suffix of AssassinPathfindingRuntime remains byte-for-byte identical to HEAD, and the Original native call still occurs exactly once. Game validation remains pending; native pair-cache hits and fallback acceptance are explicitly not directly measured.
# Weighted Assassin compatibility (2026-10-02)

Built and installed through all three build.bat drivers. Assassin tests: 15494 assertions; gate tests: 3830; full APIShared and mainmod native/movement suites passed. Installed DLLs match local packages. Main runtime has one MSB3277 reference warning and no errors; Testmod has zero warnings/errors. Game acceptance is still pending; versions and README unchanged.

The 20:58:48 active run proves 15 native rejections were overwritten by weighted success, with 34 blocked ground-edge occurrences at gates 134/148; no cache hits or blocked climbs were involved. The user confirms native-only Assassin searches choose alternatives.

The testmod now provides immutable, player-specific gate route snapshots to the existing Bugfixes Assassin builder. Query nesting and explicitly unmasked contexts are preserved; publication changes and empty map states invalidate old cache identities. Candidate rejections are counted separately as assassin-policy-filter aggregates, while assassin-route-edge remains a prepared-route violation. No permanent unit tracking or additional searches/hooks. Open/closed, true-access, normal-wall-climb and own/captured-gate game acceptance remains pending.

## Lateral drawbridge closure (2026-10-02)

The user reports weighted Assassin compatibility now behaves correctly. The subsequent shifted gate/bridge case is separate: gate 578/global 2433141 and bridge 539/global 2433175 replaced demolished gate 819 in the first 22:17:41 test_gates epoch. Its 10,602 AI builders, zero NoRoute and passing integrity did not expose the partial lateral deck crossing. Gate 578 has equal portal PCLs, so early reachability remains an explicit acceptance gap.

Native raising 645C0 closes the 15 nonzero mapper cells with moat records, not the entire 25-cell rectangle. The center seam is replaced by those exact cells' incoming/outgoing cardinal and diagonal edge masks. Parent ownership, alliances and capture policy remain unchanged. Bridge geometry no longer depends on a gate-axis decision; native and weighted Assassin routes use the same immutable publication. Moat-record presence and closure-cell changes invalidate route identity; physical animation is not changed or used to toggle hooks.

The existing compact diagnostics distinguish same-PCL/region inputs and later single-route outcomes in short-lived order contexts. Separate gate/bridge IDs and global IDs are retained. PCL-based bridge candidates are expressly unproven traversal attribution. No new hooks, public API, extra path queries or persistent unit monitoring. See baseline knowledge/ENEMY_GATE_DRAWBRIDGE_POLICY.md for audit evidence and acceptance boundaries. Versions and README remain unchanged.

Build/install completed via the testmod build.bat at 22:46:31 on 2026-10-02: 8,534 policy assertions including installed-RedBird machine execution; 15,494 Assassin A*/Dijkstra regression assertions. Runtime build: zero warnings/errors. JSON/lifecycle, workspace hook-mutation, XAML and CRLF checks passed. Installed testmod matches the local package (SHA-256 8EB5F9B148589AACA348E62F277842AA18633E286AD087290EE41836755FA150). APIShared and BugfixesAndQoL installed hashes remain unchanged. Game acceptance of the lateral bridge and early same-PCL planning remains pending.

## 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.

## 2026-10-03: completed technical verification

All four affected build.bat drivers completed successfully (APIShared, BugfixesAndQoL, EnemyGatePathfindingTest, EnemyBridgePathTest). Local and installed DLL SHA-256 values match. Existing versions and README files are unchanged; the new bridge mod is 0.1.0. Gate: 3,830 policy assertions and real installed-RedBird adapter execution. Bridge: 4,849 geometry/observer/aggregate assertions, including independent registrations, native-call counts, observer failures, nesting, map reset and over-32 state coverage. Mainmod: 15,494 weighted Assassin assertions; 269,682 unit-plan assertions, 6,480 building-distance checks, 18,262 independent search assertions and 1,469,340 cursor comparisons. Full gate/bridge source compilation was checked against installed assemblies before building.

JSON/lifecycle, enduring callback ownership, installed public members/layout, permanent hook mutation, XAML and CRLF checks passed. All builds had zero errors; both testmod runtime builds had zero warnings. BugfixesAndQoL retained an MSB3277 Mono.Cecil reference-version warning; test dependencies also report framework/reference warnings. No new runtime JSON dependency was introduced.

Final review corrected occupied-array capacity to 36 cells and records ordered raw footprint values. Bridge contexts distinguish suppressed events (SE has no Post in that case) from unsuppressed missing Post; retained mutable Pre arguments and original-input Post arguments are identified separately. Nested work movement is observed evidence, not a direct E7F60 return or proof of execution. Assassin diagnostic failures are counted without changing results. No extra native hooks, active bridge masks or persistent unit monitoring were added. Pure-gate in-game regression remains required before integration.
## 2026-10-04: gate-only game regression after bridge split

LogOutput.log contains one process start and one test_gates.sav map epoch, 16:12:12.928–16:13:53.811. Loaded APIShared 0.4.7, BugfixesAndQoL 1.0.174 and EnemyGatePathfindingTest 0.1.5; no EnemyBridgePathTest loading/runtime lines. User reports that everything appeared to work in game; specific scenarios were not individually identified, so this does not certify every outstanding acceptance case.

Final counters: 21,204 AI builder searches, zero AI NoRoute; 23,953 total queries, 866 rejected edges, 673 AI vanilla detours and zero policy NoRoute. Building approach: 35 calls and 35 validated argument-role differences, zero building-context failures. Scope mismatches, exceptions, thread-slot conflicts, snapshot-pool exhaustion and snapshot errors all zero. Hook execution/runtime integrity/thread-slot/lifecycle verdicts PASS. Final command events are paired: target 263/263, tribe movement 1,010/1,010, unit movement 50,930/50,930. Sum of every decision aggregate count is exactly 978,565, matching the final observation counter.

Gate 819 retains Global-ID 2423576 during repeated capturer changes: one initial capture and sixteen subsequent capture transitions. All 17 callback CaptureMismatch observations (14 graph, 3 builder) were confirmed recovered by later matching publication, none unresolved at map end. Owner and capture relations remain separate: own + captured-by-other is recorded; capturer/self and capturer/ally are recorded as permitted. These observations do not by themselves prove recapture by the original owner.

Cursor: 781 requests, 172 refreshes, 609 exact cache hits, no validation deferrals or exceptions; average 0.205 ms, maximum 3.177 ms. No proven cursor-policy block was exercised in this run. Seven Assassin observations were native flood-field queries without a materialized route; this is not a new weighted Assassin route/cache acceptance test. Raid-specific retargeting and a genuine alternative access are not separately proven by the supplied observation. Duration about 101 seconds does not replace a multi-wave endurance test.

Known path-table coverage warning remains 89/90 profiles and 534/540 permissions; all compared values match, no mutation evidence. No Error/Fatal log lines. The legacy hookOwnerConflict=True label denotes shared mainmod ownership, not a failing integrity verdict. No runtime code or installed DLL was changed for this analysis. This run supports the gate-only regression and independence from the bridge observer; remaining targeted acceptance cases retain their documented limits.
Source log SHA-256: 53FCE6F47F5086030DCB0334CAA84393A421933DB4950A526A1C984BBAFA893C.
