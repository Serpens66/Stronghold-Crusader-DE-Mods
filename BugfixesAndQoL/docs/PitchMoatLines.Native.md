# Pitch and moat line native contract

Reference image: installed `CrusaderDE.dll`, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Addresses below are RVAs. This feature remains hash-bound: patterns alone cannot
establish the queue layouts, record bounds, cost cycle and writer control flow.
The script extender and Fixes remain on the normal call path.

## Local input and Vanilla commands

Single-player and editor pitch use normal Chore28; moat uses normal Chore21 in
every mode. The width hook at `0x923AB+18` changes only this line's native input
staging to one tile. Thread, player, current mapper and exact XY must match.
Ordinary brushes retain their size. The executed Chore21 carries its own width,
so remote moat execution needs no receive-side width override.

The width span is `890DA7B0010681FA1F0300000F872EFCFFFF`, resumes at `0x923BD`
and retains the native rejected branch `0x91FEB`. The RedBird backend must
displace exactly18 bytes and preserve the store/compare/branch sequence.

Shared input, tick and queue access holds the existing engine monitor. At most
one field is outstanding. An unchanged queue sequence proves no enqueue and
allows a fresh quote on a later tick. A full queue or held Shift also defers
before enqueue. Ambiguous submission failures never cause an automatic resend.
Receipts track `(slot, sender, opcode, commandId)` until native terminal state
or slot reuse; they do not time out after an arbitrary wall-clock interval.
States1..9 await execution; the executor writes state10 after the handler.
An old live command defers a later gesture even after a tool change. A field
changed by another same-owner command cannot acknowledge a still-live command.

## Multiplayer pitch

The packet registration is unconditional and in the same order on every peer,
including a peer where native installation subsequently fails. The protocol
is one authenticated APIShared Chore106 per pitch field, with a closed five-int
MessagePack schema: version1, Pitch operation1, X, Y and positive local request
ID. Player identity comes exclusively from the authenticated native issuer.
No array of fields, native slot, selected player or cost value is transmitted.

Unity sends an immutable packet through `RegisterAuthenticatedChore`. Its public
transport validates controlled-player identity and queue capacity under the
engine monitor and has no Steam fallback. The sender does not apply beforehand.
All receivers, including the sender, execute the same native builder synchronously.
Their receive activation depends on the common mission capability snapshot,
never the personal input option or the host's general EnableMod setting.

The receiver checks issuer1..8, executing issuer, active map coordinates,
no existing pitch or completed moat, coherent optional planned-moat task,
and the native pitch reserve15 (`0x6C9D0`). It then calls exactly the stock
check from Vanilla Chore28 (`0x12090`):

    CC420(playerManager, MAPPER_PITCH_DITCH, sender, feedback:0)
    scoped authenticated106 preservation
    GameBuildingManagerAPI.CreatePrefab(sender, X, Y, PITCH, 1, 0, false, false)

`CC420` must precede the builder. `0x6D580` validates placement and debits through
`0xC8A90` but does not itself enforce stock affordability. CC420's prepaid-pitch
branch can skip later gold/wood/stone checks; no replacement all-resource check
is equivalent. Configured zero pitch threshold and the four-field charge cycle
are retained. Gold/wood/iron/stone remain admission thresholds only: the pitch
branch returns before their generic debit path. No Lord/alive heuristic rejects native builder modes.

The public CreatePrefab signature was verified against the installed SHCDESE
assembly. It calls BulkBuildingDetours, preserving normal SE validation/build
events and native pathing/visual effects. It does not call a BulkChoreDetours
wrapper. Normal optional editor mirroring remains on that same SE route.

The executing actor at `ChoreManager+0x36FA4C` aliases RVA `0x88E3D6C` and is
already set by `0x1F7E0`; the receiver must not overwrite the local view/player
at `0x88E3D70`. It must not call the Vanilla28 handler on a106 slot or mutate
the28 staging fields. Native builder scratch and terminal state retain normal
semantics; preview-only validator scratch is separately restored.

Neither public CreatePrefab's long return nor native RAX proves success:
native6D580 is effectively void. Confirmation checks the scope execution,
PitchTrap flag, and `0x695C0` lookup with coherent XY/owner under the signed
exclusive live pitch-slot bound. A rejected terminal result ends the line.
Fresh shared geometry checks restore the same preview scratch as local quotes.
A newly existing pitch or blocked field returns Skipped, so later candidates
remain eligible. Stock/capacity exhaustion or an unconfirmed builder ends the line.

## Exact preservation scope

The seam is `0x6D788+16`:
`0FBFAC73006CBF0085ED0F8447010000`. Preserve the signed occupant load and TEST
flags. Ordinary empty tiles continue at `0x6D8DF`; occupied tiles at `0x6D798`.
For pitch (struct0x44), the latter dispatches to0x6D8DF as well, without unit
cleanup. The table at0x6DFE8 starts with0x6D8DF and selector0x6E007 is0.

Only an armed matching pitch tile, width1, actor, sender, execution slot,
command ID and execute phase may depart. Vanilla28 also requires its exact
ten-byte coordinates/mapper/width/variant/auxiliary payload. Authenticated106
uses a separate token discriminator and never interprets MessagePack as28.
APIShared verifies the exact106 body before the receiver arms the scope.

On a coherent planned moat, jump to0x6D912, skipping deletion through0x61ED0
and the associated flag clear. Task checks include packed tile, XY, owner1..8,
progress<4, DigPlannedMoat and a valid exclusive upper bound <=64000.
This bypass also preserves BFQoL MoatTerrain history at0x6D8DF+20. Any existing
adjacent detour must be that exact registry-owned MoatTerrainRestoration hook.
All unrelated commands retain original behavior. Scope closes in finally;
focus/tool changes do not revoke an already queued command. Published hooks
and their native memory remain rooted for process lifetime.

## Other audited native dependencies

TileManager0x405EDB0, PlayerManager0x366C210, ChoreManager0x8574320;
native input player0x88E3D70, Shift0x8EBA9F, mapper0x60AD45C;
mode0x3665F28, map type0x3665ED8, free construction0x3665FC8.
Pitch validation0x77E60, moat validation0x5CA40, keep proximity0xEC130,
moat free slots0x6C990, pitch free slots0x6C9D0, pitch lookup0x695C0,
editor vegetation0x107160. Their independently verified machine contracts
and complete reference layouts below are validated before use.

Queue slots are500 records at manager+0xB0BF8, stride0x500. Fields: tick0,
sender4, opcode8, state9, command12, payload17; state0 or signed>=10 is free.
Cursor+0x370BF4, sequence+0xDA0, executing slot+0x84CC8, phase+0x84CCC,
executing sender+0x36FA4C. Identity is player*100000000+sequence. Commands
can receive a different final tick through the host barrier; separate metadata
and Vanilla28 Chores therefore do not provide an atomic authorization pair.

Before updating the DLL, re-audit callers, every writer predecessor and branch,
all record layouts/bounds, native cost configuration and terminal states.
The executable fixture in tests/PitchMoatLines.Native.Tests runs the production
stub for both opcodes and checks flags, registers, stack, task coherency,
ordinary continuations and queue receipt identities. A successful fixture does
not replace live single-player/editor and genuine two-peer MP acceptance.

## Reference layout ledger

| RVA / Layout | Verwendung und Beleg |
| --- | --- |
| `0x923AB + 18` | Nur eigener Schreibbereich. `89 0D A7 B0 01 06` schreibt ECX nach `0x60AD458`, `81 FA 1F 03 00 00` vergleicht EDX mit der Rastergrenze, `0F 87 2E FC FF FF` verzweigt nach `0x91FEB`. Der Fall-through bei `0x923BD` beginnt mit `48 63 05 0C FA 97 03`. Disassembly der ausführbaren Sektion zeigt keine direkten eingehenden Kanten in die Spanne. Die Verzweigung aus der Mapperentscheidung landet vor dem Hook. |
| `0x92390..0x923AB`, `0x2D1C38`, `0x69850` | Vanilla berechnet Breite 3 oder 5 anhand des Shift-Bytes. Die Brush-Tabelle liefert für Breite 1 eine Kachel. Die Koordinatenversätze werden nach dem geprüften nativen Brush-Vertrag von `0x69850` angewandt. |
| `0x77E60`, `0x5CA40`, `0xEC130`, `0x6C990`, `0x6C9D0`, `0x107160` | Direkte Aufrufe der Live-Einstiege für Pechprüfung, normale Grabenprüfung, Erreichbarkeit des Keeps, freie Graben-/Pechslots und im Editor entfernbares Geländeobjekt. Jeweils an Prolog im unveränderten Abbild und native Signatur gebunden. |
| `0x695C0`, TileManager `+0x204C6C4` | Schreibfreier Pech-Lookup, Win64 `int(TileManager*, int packedTileId)`. Prolog `4C 63 91 C4 C6 04 02 41 B9 01 00 00 00 4D 3B D1`. Die vorzeichenbehaftete exklusive Suchgrenze wird gegen die Länge der Extender-Slotansicht geprüft. Vanilla beginnt bei Slot 1, vergleicht die gepackte Tile-ID und prüft den Besitzer als Belegungssignal. Rückgabe 0 bedeutet kein Eintrag. Positive Ergebnisse werden zusätzlich gegen Suchgrenze, Koordinaten und gültigen Besitzer geprüft. Die Extender-Ansicht `GetPitchSlotLookup()` mit 4.000 Elementen ist keine nach gepackter Tile-ID adressierbare Kartenebene. |
| `0x405EDB0`, `0x366C210`, `0x60AD660`, `0x32DE440` | Tile-, Player-, Pfad- und Vegetationsmanager aus den Vanilla-Aufrufen. Tile- und Playerbasis zusätzlich gegen die Extender-API abgleichen. |
| `0x3A11EA4`, `0x3A11DCC`, `0x3A11DD0` | Aktivitätsmatrix mit 800 Spalten und Eingabekoordinaten. Der Dispatcher schreibt die Koordinaten, die Grabenprüfung prüft diese Matrix. Gepackte Tile-IDs danach über den Extender ermitteln und gegen die tatsächliche Kapazität prüfen. |
| `0x60AD45C`, `0x88E3D70`, `0x8EBA9F` | Aktiver Mapper aus der Werkzeugauswahl, aktueller Spieler und Shift-Byte aus `DLL_RunTick`. `DLL_SetEditorPlayer` bei `0x872E0` setzt für reguläre Spieler-IDs denselben Spielerwert unter dem Engine-Monitor. `DLL_MapAction` setzt ihn vorübergehend auf den Auftragsspieler. Der Hook verlangt Thread, Mapper, Spieler und genau dieselbe Kachel. |
| `0x3665F28`, `0x3665ED8`, `0x3665FC8`, `0x366A0F8` | Nativer Modus, Map-Untertyp für den Editor-Eigentümer, Gratisbau- und Eingabesperre. Gelesen aus den Branches von `0x8BD60`, `0x12090`, `0xCC420/0xC8A90`, `0x90CD0`. |
| `0x379D04C + playerId * 0x583C`, `0x37EF8F8` | 16-Bit-Phase aus `0xCC57B` und `0xC8A90`, Reichweite aus dem Aufruf von `0xEC130`. Spieler-IDs sind im nativen Record 1-basiert. |
| TileManager `+0x204E6FC..+0x204E78C`, `0x60AD4B4`, `0x34A9F5C` | Platzierungs-, Pinsel-, Ablehnungs- und Abstandszwischenwerte für die Vorschau. Der Mod stellt diese Ergebnisse nach seinen Abfragen wieder her. Die nativen Pfadsuchfunktionen behalten ihren eigenen Scratch-Zustand für die nächste Suche. |
| TileManager `+0xD7E5A0`, `+0xA6F260`, `+0xB0BCA0` | Höhe, Vegetationsindex, Strukturindex. Byte bzw. 16-Bit-Felder im Grabencommit von `0x6FE90`. Pech- und Grabenbelegung aus der Extender-API. |
| `0x8574320` und `+0xDA0/+0x370BF4/+0xB0BF8` | Chore-Manager, Schreibsequenz, Cursor und Queue. `0x20950` sucht 500 Slots zu je `0x500` Bytes, Status bei Slot `+9`, frei bei 0 oder signed >=10. `0x23A16` erhöht die lokale Sequenz. Manager zusätzlich über Extender-Adresse prüfen. |
| `0x86C132C..0x86C133C` | Fünf Staging-Dwords für X, Y, Maß/Mapper, Operation/Maß und Variante aus `0x90CD0`, gelesen von `0x111D0` und `0x12090`. |

Der generierte RedBird-Stub erhält die GPRs. Nur ECX ändert sich beim
passenden, threadlokal markierten Auftrag. Der verdrängte CMP läuft
danach und erzeugt die Flags für den verdrängten JA erneut. Ohne
passenden Auftrag bleibt die Breite unverändert. Nach Veröffentlichung
bleibt der Stub bis zum Prozessende installiert. Es gibt keine
Überschneidung mit dem Extender-Validator bei `0x77E60` oder dem
ExtraFeatures-Höheneingriff innerhalb von `0x5CA40`. Der Fixes-Quellbaum
hat für die exklusive Spanne keinen Eingriff. Die tatsächliche Spanne
wird zusätzlich im Registry und gegen Live-Bytes geprüft.
