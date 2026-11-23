# ExtremePowers: Update auf eine neue CrusaderDE.dll

Referenz ist ausschließlich Steam-Build `24816905`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Bei einem anderen Hash bleibt das komplette native Backend deaktiviert. Ein Pattern-Fallback auf unbekannten Builds ist derzeit absichtlich nicht aktiv, weil die festen Ressourcen-, Manager- und Globaloffsets nicht allein durch passende Funktionsprologe bewiesen wären.

## Hook- und Funktionsziele

| Feature | Referenz-RVA | Validierung/Herleitung | Fallback und Fehlerverhalten |
|---|---:|---|---|
| finaler Power-Dispatcher | `0xCD630` | 20-Byte-Prolog; Power-Switch und gemeinsamer Abzug bei `0xCD814` manuell bestätigt | nur exakter Hash und Signatur; Abweichung deaktiviert die gesamte Hooktransaktion |
| Power-Auswahl/Kostenprüfung | `0x105510` | 8-Byte-Prolog; Aufruf aus `DLL_GameAction` Command 1068 und Kostenformel bestätigt | wie oben |
| Ressourcenupdate/Regeneration | `0xCDB20` | 27-Byte-Prolog, interner Block `0xCDD87`, einzige direkte Callsite `0xCE25E`, Tail `0xCDE3C` | alle vier Prüfungen müssen gemeinsam passen; sonst keine Hooks |
| Heilwirkung | `0xE1E70` | 16-Byte-Prolog und Dispatcher-Aufrufparameter bestätigt | wie oben |
| Pfeil-/Steinsalve | `0xDD6C0` | 16-Byte-Prolog und beide Dispatcher-Zweige bestätigt | wie oben |
| Gold-Zyklusfortschaltung | `0x7530` | 16-Byte-Signatur und Goldzweig bestätigt | wie oben |

Die drei Hooks sind dauerhafte RedBird-Detours (`Shared/Runtime/Native/AuditedEntryDetour.cs`, Backend NativeX64, Schema `Indirect`). Verdrängte Einstiege, geprüft auf dem Referenzhash mit RedBird 1.5:

| Hook | Verdrängter Einstieg | Konstante |
|---|---|---|
| Dispatcher `0xCD630` | `48 89 5C 24 10 48 89 6C 24 18` (10 Byte, `5+5`) | `DispatcherDetourEntry` |
| Auswahl `0x105510` | `40 53 48 83 EC 20` (6 Byte, `2+4`; RedBird kodiert `40 53` im Trampolin als `53`) | `SelectionDetourEntry` |
| Ressourcenupdate `0xCDB20` | `48 89 5C 24 08 48 89 6C 24 10` (10 Byte, `5+5`) | `ResourceUpdateDetourEntry` |

Geprüft: vollständige Instruktionen ohne RIP-relativen Operanden oder Sprung, kein direkter Sprung aus den ausführbaren Sektionen ins Innere einer Spanne, keine der drei Funktionen ruft sich selbst auf. Spectator hookt in der Auswahlfunktion erst ab `0x105593`, außerhalb der Spanne. Jeder Einstieg wird vor dem Schreiben gegen das Ladeabbild und live geprüft (Fremdpatch → `NativePatchGuard`-Error, Vanilla bleibt aktiv); `Shared/Tests/AuditedEntryDetour.Tests` lässt das installierte Backend im Build auf Kopien der echten Einstiege laufen. Diese Prüfung ist bei jeder neuen Hookimplementierung oder DLL erneut durchzuführen.

Der Regenerationsblock ist 69 Byte lang und greift über die verschobene Schleifenbasis auf `+0x3950` zu, entsprechend `GamePlayerResources.r_ExtremePowersMana + 0x39D4`. Er vergleicht gegen `7000`, prüft die Modulo-3-Kadenz und schreibt höchstens `+1`.

Beim Resync bleibt der private Rundungsrest vom ausgeführten Chore 54 bis zum
gemeinsamen Abschluss eingefroren. Der native Reparaturzweig in `0xCDE60`
umgeht das gewöhnliche Ressourcenupdate. APIShared überträgt acht einzelne
Reste im Bereich 0–99, passend zu den acht Slots des bestehenden Writers.
Der native Spielerrecord und sein Manawert haben ihren eigenen Reparaturpfad.

## Hashgebundene Datenziele

Diese Ziele besitzen keinen belastbaren eigenständigen Pattern-Fallback. Bei einer neuen DLL müssen Managerbasis, Spielerformel und mehrere bekannte Ressourcenfelder erneut gemeinsam validiert werden.

| Ziel | Referenz-RVA/Offset | Verwendung |
|---|---:|---|
| ausgewählte Power | `0x366A0C4` | Wiederherstellung der Gold-ID nach Kartenpunkt-Targeting |
| Mana Spieler 0, bildbasiert | `0x379E7A4` | Spieleradresse plus `playerId * 0x583C`; entspricht Ressourcenoffset `0x39D4` |
| Gold Spieler 0 | `0x379E7A8` | direkt folgendes `UInt32`-Feld |
| Gold-Zykluswert | `0x856A6D2` | `Int16`; Fortschaltungsobjekt beginnt zwei Byte davor |
| lokaler Spieler | `0x88E3D70` | Auswahlpfad |
| Effektmanager | `0x60AD660` | `this` für Heilung und Salven |

## Updateprüfung

1. Neue installierte DLL hashen und Steam-Build dokumentieren.
2. Referenz-RVAs nur als Startpunkte verwenden; Prologe, vollständige Instruktionen, Funktionsgrenzen, Calls, Switchzweige und Feldoffsets neu herleiten.
3. Für jeden Detour die tatsächlich von RedBird verdrängte Instruktionsspanne (`AuditedEntryDetour`, Schema `Indirect`) sowie alle eingehenden Kontrollflussziele prüfen; Einstiegskonstanten und `Shared/Tests/AuditedEntryDetour.Tests` gemeinsam anpassen.
4. Ressourcenupdate: Callsite-Anzahl, Prolog, vollständigen Regenerationsblock, Feldzuordnung, Grenze `7000`, Delta `+1` und Funktionstail erneut bestätigen.
5. `ExtremePowersBuildCompatibility.HasExpectedNativeSignatures` und die Runtime-Guards gemeinsam aktualisieren; Tests einmal mit der kanonischen und einmal mit absichtlich veränderten Bytes ausführen.
6. Erst danach den unterstützten Hash ändern. Jede Unsicherheit bleibt fail-closed auf Vanilla.
