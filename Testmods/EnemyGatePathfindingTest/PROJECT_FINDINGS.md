# EnemyGatePathfindingTest – konsolidierte Erkenntnisse

Stand: 11. September 2026

## Ziel und aktueller Umfang

Der Testmod verhindert, dass eine PCL-Route ein feindliches Tor oder dessen Zugbrücke allein wegen einer Eroberung durch einen fremden dritten Spieler als zugänglich behandelt. Besitzer, Verbündete sowie der eigene oder ein verbündeter Eroberer behalten Vanillas Zugriff. Unsichere oder veraltete Snapshots führen immer zu Vanilla-Verhalten (fail-open).

Zusätzlich prüft der menschliche Bewegungscursor positive Same-PCL-Ergebnisse gegen unveränderliche Tor-/Brücken-Snapshots und das native Richtungsraster. Eine Route, die nur durch ein blockiertes feindliches Tor führt, wird verworfen; ein tatsächlich offener Umweg bleibt gültig.

Same-PCL-KI-Routen und cursorlose Same-PCL-Befehle werden in diesem Schritt ausdrücklich nicht verändert. Sie bleiben fail-open und sind ein separates Pathfinding-/Pfadpublikationsprojekt.

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

Vor der Hooktransaktion werden Hash, exakte Bytefolgen, Start-/Endadressen sowie Vorgänger- und Nachfolgesprünge validiert. Zusätzlich wird mit der installierten RedBird-Implementierung ein unveröffentlichter Probe-Hook erzeugt. Nur `DisplacedByteCount == 20` ist zulässig. Nach dem atomaren Commit wird die tatsächliche Länge erneut geprüft; eine Abweichung rollt den noch nicht veröffentlichten Initialisierungskandidaten vollständig zurück.

### Cursorfilter

Der Cursor-Hook bleibt bei `0x8F1C4`. Sein vollständiger Block ist exakt 14 Byte lang (`TEST`, `LEA`, `MOV`). Bytevertrag und RedBird-Verdrängung müssen exakt 14 Byte ergeben. Die Registermaske ist vollständig; im geprüften Block sind keine live XMM-/SIMD-Werte vorhanden. Der Callback arbeitet nur mit unveränderlichen Snapshots und einem read-only Zugriff auf das native Richtungsraster.

## Runtime- und Datenmodell

- `EnemyGatePathfindingRuntime` besitzt die beiden Capturer-Hooks und veröffentlicht sich erst nach erfolgreicher Initialisierung.
- `GateTopologySnapshotProvider` erzeugt außerhalb nativer Callbacks unveränderliche Gate-Zugriffs- und Cursor-Policy-Snapshots.
- `CursorGateRouteFilter` führt die begrenzte Cursor-BFS aus, ohne das globale Richtungsraster zu verändern.
- Prozessweit benötigte Runtimeobjekte, Logger, Delegates und Eventabonnements sind statisch verwurzelt.
- Kartenwechsel beenden nur Diagnoseepochen und leeren Snapshots. Es gibt keinen normalen Dispose-/Unhook-Pfad.
- JSON wird weder gelesen noch geschrieben und ist keine Runtime-Abhängigkeit.
- Gebäudestatus werden direkt mit `AliveState.NeedsInit` und `AliveState.IsAlive` geprüft; Gebäudetypen verwenden die bestätigten `eStructs`-Symbole.

Entfernt wurden die nicht mehr benötigten Query- und MoveHere-Ringpuffer, zeitliche Korrelation, Callerklassifikation, Builderdiagnostik und Routendecodierung. Sie werden nicht als Fallback mitgeführt.

## Abnahme

Die Maschinentests prüfen die beiden 20-Byte-Blöcke, ihre exklusiven Endadressen, umgebenden Sprünge und eine absichtlich mutierte Bytefolge. Insbesondere liegen `0xE271B` und `0xE303A` außerhalb der Hookspannen. Policytests decken Besitzer, Verbündete, eigenen/verbündeten Eroberer, fremden Eroberer, ungültige Snapshots und die Cursor-BFS ab.

Für den noch ausstehenden Laufzeittest:

1. Karte mit feindlichem Tor laden und mehrere Minuten vorspulen.
2. Sicherstellen, dass kein neuer echter Crashdump entsteht.
3. Im Log `pclGraphCapturerFilter=0xE2705`, `builderPrecheckCapturerFilter=0xE3024` und jeweils `displaced=20` bestätigen.
4. Fehlerfreie Snapshot-Aktualisierungen prüfen.
5. Different-PCL-Zugriff für Besitzer, Verbündete und verbündete Eroberer sowie die Sperre für einen fremden Eroberer prüfen.
6. Beim Cursor eine ausschließlich durch das feindliche Tor beziehungsweise die Zugbrücke führende Same-PCL-Route blockieren und einen echten Umweg erlauben lassen.
