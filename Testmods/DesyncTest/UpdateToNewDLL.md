# DesyncTest: nativer Vertrag

Referenz vom 30.10.2026: CrusaderDE.dll SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Installiertes NativeX64-Backend SHA-256 `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

Der Inline-/Assemblervertrag ist ebenfalls hashgebunden: `RedBird.X64.dll` SHA-256 `E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB`, `Iced.dll` SHA-256 `CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F`. Ein Update dieser Komponenten verlangt erneute Generator-/ABI-Prüfung, bevor die Laufzeit freigegeben wird.

## Codeziele

Die exakten Bytes, Längen und Namen stehen in `NativeHooks.Sites`; `tools/Audit-Native.py` prüft sie gegen die installierte DLL und alle eingehenden Referenzen der ausgewählten Rohbaseline. Das Buildprotokoll enthält die vollständige Instruktionsliste. Registervertrag an Dispatch-Callsites: Tabellenbasis R15, R12 oder R14 plus RAX*8; originale Handler erhalten keine ausdrücklichen Argumente, ihr Ergebnis wird hier nicht konsumiert. Ein typisierter Wrapper ruft den tatsächlichen Live-Handler genau einmal auf. Nichtvolatile Register und Windows-x64-Stackvertrag bleiben erhalten. Nach der ersetzten Call-Instruktion werden die verdrängten Originalinstruktionen unverändert wieder ausgeführt; dort benötigte Flags entstehen wie zuvor. Kein RedBird-Context-Callback mit ungeklärten Flag-/XMM-Annahmen.

| RVA | Länge | Besitzer/Vertrag |
|---|---:|---|
| `0x22020` | 19 | Prüfsummenbuilder `void(manager,int)`; Post nur wenn Guard/Aufschub vor Original Berechnung zulassen |
| `0xD1AD0` | 17 | Scheduler `void(manager)`; zusätzliche Proben nach genau einem Originalaufruf |
| `0x1F5F0` | 14 | Feldtransfer `void(manager,field,length,scheduled,unpack)`; Pufferoffset noch vor Original, nur lesende Kopien |
| `0x86480` | 19 | DLL_ReceiveChore `int(player,data,length)`; identisches Rückgabeergebnis, Rohdatenaufnahme ohne Simulationsthreadannahme |
| `0x1F88B` | 14 | Tatsächlicher Pending-Dispatch über `[R15+RAX*8]` |
| `0x23A98` | 17 | Lokaler Pack-Dispatch über `[R12+RAX*8]` |
| `0x23B85` | 19 | Lokaler Immediate-Dispatch einschließlich Vorgängerstore und Opcode-Lesen; Ende `0x23B98` ist externes Sprungziel, deshalb niemals ab `0x23B94` 14 Byte überschreiben |
| `0x2400F` | 17 | Remote-Measure-Dispatch über `[R14+RAX*8]` |
| `0x24084` | 15 | Remote-Immediate-Dispatch; nachfolgender Cleanup-Call wird erhalten/relokiert |
| `0x1BFAD` | 18 | Forced-Run-Logger-Call, fünf ABI-Argumente inklusive Stackargument; Originalziel `0x1A4A50` (`RET 0`) wird genau einmal aufgerufen |

Einstiegsdetours erzwingen das echte NativeX64-Absolute-Schema mit genau dokumentierter verdrängter Länge; unveränderte Livebytes, Trampolin/Hookentry und Patchform werden geprüft. Inline-Hooks verlangen genau die dokumentierte Spanne. Alle Hooks werden vor Missionsbeginn installiert und behalten Prozesslebensdauer. Moduswechsel verändern nur den logischen Zustand.

Das Fixture führt die sechs produktiven Inline-Generatoren mit echten verdrängten Instruktionen aus. Nur externe Callees und RIP-Daten werden durch eigene Testgrenzen ersetzt. Zusätzlich wird die ursprüngliche native Hashroutine ohne DLL-Initialisierung aus ihrem positionunabhängigen Code ausgeführt und gegen die verwaltete Nachbildung verglichen. Die vier vollständigen nativen Spielfunktionen sind dadurch nicht als Gesamtverhalten im Spiel abgenommen.

## Datenziele und Feldbasen

Alle Adressen sind relativ zum geladenen Modul; absolute Adressen in Pseudocode nicht direkt verwenden. Der Modulbereich muss mindestens bis zum exklusiven Ende `0x88E4F1C` reichen (Manager plus Feldcursor und dessen vier Byte).

- Chore-Manager `0x8574320`; Timer-/Kartentick `0x3665F58`; Handlertabelle `0x2C7A30` (Liveziele werden verwendet).
- Manager: Phase `+0x84CCC`, Pending-Slot `+0x84CC8`, unscheduled Puffer `+0x84CD8`, Feldoffset `+0x370BF8`. Pendingkapazität 500, Stride `0x500`, Payload `+0xB0C09+slot*0x500`, maximale normale Payload `0x4EC`, unscheduled Feldgrenze 180000.
- Pending-Record: Ziel-/Ausführungstick `+0xB0BF8`, Spieler `+0xB0BFC`, Opcode `+0xB0C00`, Befehlskennung `+0xB0C04`. Ausgeführter Spieler nach nativer Remap-Funktion über Manager `+0x36FA4C`; lokaler Prüfsummenslot `+0x36FA50`.
- `DLL_ReceiveChore` reicht die übergebenen inneren Bytes an `0x23E70` weiter; erst dort entsteht der äußere FIFO-Record aus Länge, Sender und Payload. Normale innere Opcodes 2..120 besitzen Opcode an Byte 0, uint24-Plantick an Byte 1..3 und int32-Befehlskennung an Byte 4..7. Die Eingangsbeobachtung liest nur diesen mitgelieferten Puffer. Sonder-/Kompressionspfade behalten Rohdaten und bekommen keine erfundene normale Befehlskennung. Dies ist Eingang am Export, keine Bestätigung einer erfolgreichen FIFO-Einreihung.
- Prüfsummen: Guards `+0xDA8`, `+0x279FA4`; Prüftick `+0x14D0E4+player*4`, Kategorien `+0x14D108+player*56`, Flags `+0x14D34C`, Unitchecksums `+0x14F0CC+(player*10000+unitId)*4`, Resync-State `+0xDAC`.
- Wire-Recordbasis für Slot 0 `0x67E8A58`, Stride `0x490`; Pfadbasis für Slot 0 `0x7338278`, Stride 1000. Die native Prüfung erfasst IDs 1..9999. Die Extender-Sicht beginnt jeweils vier Byte nach dem Wire-Record; Global-ID raw `+0x98` entspricht `GameUnit.r_GlobalId +0x94`.
- Hashstart raw `+0x8C`, Länge `0x404`; maskierte acht Byte raw `+0x41C`; separate Pfaddaten 1000 Byte; zusätzliche uint32 raw `+0x3C`, `+0x40`, `+0x44`. Native Bytebehandlung ist signed, Wortbildung/Rotation modulo uint32. Die Diagnose schreibt kein Maskierungsbyte.
- Die nativen Handler für Chore 54, 56, 67 sind `0x15170`, `0x15710`, `0x16E40`. Sie werden nicht separat gepatcht. Beim eingehenden Unit-ID-Feld wird die ID aus dem bereits vorhandenen Wirepuffer gelesen, bevor Vanilla die global gespeicherte Ziel-ID oder die lokale Erhaltungsmarkierung ändert. Empfangsbytes des folgenden Records werden separat vor Anwendung kopiert; finaler Record/Pfad erst nach originalem Handlerabschluss.

## Hashbindung, Fallback und Updates

Code und Daten sind für diesen Prototyp bewusst gemeinsam hashgebunden. Es existiert noch keine unabhängige, doppelt belegte Ableitung sämtlicher Manager-/Recordfelder für unbekannte Images. Daher kein scheinbar versionsunabhängiger Pattern-Fallback, der trotz passender Funktion einen falschen Datenvertrag erlauben würde. Bei unbekanntem Native-Hash bleibt der Kern aus und meldet `DESYNC_CORE_UNAVAILABLE`. Bei Liveabweichung/Registrykonflikt ebenfalls fail-closed; bereits veröffentlichte Hooks bleiben verwurzelt und inaktiv. Kein Runtime-Unpatch.

Für ein Update die vollständige Kette RunTick → Simulation/Scheduler → Summenvergleich → Resync-Start → Kategorien-/Detailvergleich → blockweise Übertragung → Empfang/Erhaltungsregeln → Abschluss/Timeouts erneut prüfen. Normaler, lokaler unmittelbarer und entfernter unmittelbarer Dispatch müssen erhalten bleiben. Neue Strukturwerte nie allein aus einem Displacement übernehmen. Ein erfolgreicher Kompiliervorgang ersetzt diese Nachweise nicht.

Die statisch belegte Vier-Byte-Basisdifferenz präzisiert das ältere Aufgeben-Dossier: dessen strukturrelative Offsets sind kein Wire-Record-Offset. Historische Aufgeben-Traces bleiben unverändert; neue Pakete dokumentieren ihre Basis ausdrücklich. Aktuelle Slotzustände werden bei jeder Aufnahme neu gelesen.
