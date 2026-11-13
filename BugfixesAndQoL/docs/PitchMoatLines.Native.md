# Pitch and moat line native contract

Reference image: installed CrusaderDE.dll, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Snapshot: 2026-11-13. Addresses are RVAs. These contracts remain hash-bound;
a matching function pattern alone does not establish field layouts or cost semantics.
The native database's unnamed functions remain candidate symbols; the particular
branches and parameter contracts below were checked against this image.

## Input and whole-line transport

The existing composed Harmony boundaries own Ctrl input, scene previews and the
locked engine quote. A mouse release freshly quotes and copies the eligible prefix
into one immutable batch, submits on Unity under the engine monitor, and clears the
gesture immediately. There is no outstanding-field input state. Tool/option/focus
changes cancel only an unconfirmed gesture, never an admitted native command.
A rejected/uncertain submission is logged and shown from AfterInput outside the monitor.

All modes, including single-player and editor, use one authenticated APIShared106
Chore per line. The closed explicit MessagePack array has five fields:
version2, operation (Pitch1 / PlannedMoat2 / CompletedMoat3), captured actor,
positive request ID and an array of packed XY integers (X | Y<<16).
The array has 1..64 fields, each coordinate0..799. The maximum valid body plus
two-byte packet ID fits the configured512-byte bound (native limit1200).
The constructor copies the buffer and publishes a read-only view. The formatter
bounds the nested array before allocation. No cost, native slot or trusted issuer
comes from the payload. Authenticated native execution supplies the issuer.

The channel is registered on all peers regardless of personal options. Local
missions require native readiness; real multiplayer additionally requires the
frozen peer agreement `pitch-moat-lines-batch-v2`. Receive never reads the local
tool, key, selected player, personal input option or host's general EnableMod.
The sender does not mutate simulation in advance. Local execution can complete
inside Send; no separate acknowledgement is required to admit another line.

Queue preflight still checks500 slots of stride0x500, states0 or signed>=10 free,
and captures slot/sender/opcode/command ID before the one Send. Submission failure
with a matching live queue identity counts as submitted. Ambiguous failures stop,
without retry. Native queue and authenticated context validate the command body;
there is no Steam fallback. Mission lifecycle retires the local context.

## Per-field execution and cost

All fields execute in packet order inside the same synchronous106 callback.
Existing/blocked fields are skipped. Stock/capacity exhaustion or an unconfirmed
terminal mutation stops the remainder; successful earlier fields remain.

Pitch uses exactly the ordinary28 admission then build sequence:
`0xCC420(playerManager, MAPPER_PITCH_DITCH, issuer, feedback0)`, followed by
`GameBuildingManagerAPI.CreatePrefab(actor,X,Y,PITCH,1,0,false,false)`.
That public route enters the live SE building detour and native0x6D580; it preserves
events, validation, visuals, path effects and payment0xC8A90. It is not a
BulkChoreDetours wrapper. Never infer success from its ABI return value.

The four-field payment cycle is native: phase0 removes one raw pitch, later phases
advance/wrap without debit. CC420's prepaid pitch-shortage branch skips later
gold/wood/stone checks, but not iron. Those goods are admission thresholds rather
than extra pitch debits. Free construction/builder and siege-builder behavior remain
native. No aggregate debit, stock reservation or substitute resource formula runs.

Ordinary28 uses issuer for stock admission, but MapType12 substitutes the editor
actor at0x88E3D78 for its builder. Capture that actor before enqueue and validate
1..8. A different payload actor is permitted only in editor MapType12; multiplayer
requires actor=issuer. Later editor selection cannot change the captured actor.
Pitch confirmation requires the preservation scope to execute, PitchTrap and a
coherent pitch lookup0x695C0 with the captured owner and exclusive signed slot bound.
Capacity uses0x6C9D0 with reserve15.

## Graben execution

The normal21 chain is0x111D0 ->0x6FE90 ->0x59210. The adapter calls the live
0x6FE90 entry, preserving SE BulkMapEditorDetours and BFQoL MoatTerrain interior
hooks. Win64 signature:

    void(TileManager*, int issuer, int packedTileId, int y,
         int width, uint operation, int variant, byte auxiliary)

Use width1, IsMoat operation0x40000000, variant0 planned or2 immediately completed,
auxiliary0. Packed X includes the native row offset; ordinary X is incorrect here.
After each call reproduce21's0xE3B90(PathContext*, radius, X, Y).
Radius is max(9,width+3)=9;0x1C0A0 is max (3BCA0F4FD18BC2C3).
These are live calls, not calls into a Chore handler with incompatible staging bytes.

Before execution check active-map bounds, empty moat/task occupancy, reserve50
through0x6C990, keep-path exclusion0xEC130 and geometry. Do not reuse0x5CA40 in
receive: it reads local player0x88E3D70 and the local pending-preview grid.
Its geometry mask can be checked independently; proximity and the planned height
gate remain with live0x6FE90 and the explicit issuer. In particular, do not duplicate
height<13 for planned batches: ExtraFeatures can allow elevated human moats at
both0x5CC1E and0x704CC. The existing completed-preview rules remain unchanged.

0x59210 allocates a16-byte task with packed tile, XY and owner. Planned variant
creates DigPlannedMoat/type1,progress0 plus PlannedMoat. Completed variant creates
FillDugMoat/type2,progress4 plus IsMoat. Confirm all those fields after mutation.
Native mode determines which variant is allowed; a packet cannot request completed
terrain in a planning-only mode. New bindings:
0x6FE90 prologue48895C240844894C24204489442418895424105556574154;
0xE3B90 prologue40535556574883EC284963F88BEA4963F1488BD9;
0x111D0 prologue48895C24084889742410574883EC40.
Call-only registry claims include both targets.

## Exact pitch/planned-moat preservation

The permanent seam remains0x6D788+16:
0FBFAC73006CBF0085ED0F8447010000. Preserve signed occupant load and TEST flags.
Empty ordinary tiles continue0x6D8DF; occupied0x6D798. Pitch's occupied dispatch
also reaches0x6D8DF without unit cleanup (table0x6DFE8, selector0x6E007).

Arm one tile at a time only inside the authenticated106 callback. Token binds
slot, sender, command, packed tile, XY, actor and opcode106. The hook checks
execute phase, width1, structure PITCH, manager, actor and exact command.
Ordinary28 commands cannot consume it. Clear in finally after every attempt.
A coherent planned moat branches to0x6D912, preserving its task and flags and the
adjacent MoatTerrain restoration history. Task checks include owner1..8, type1,
progress<4, exact tile/XY and exclusive bound<=64000. The adjacent writer at0x6D8DF
must be the exact registry-owned MoatTerrainRestoration span or unchanged bytes.
Published code and token stay rooted for process lifetime.

The obsolete local width hook0x923AB and per-field28/21 submission/receipt state
were removed, including the old28 token discriminator. Ordinary brushes are
untouched by this feature.

## Data/update ledger and verification

TileManager0x405EDB0; PlayerManager0x366C210; PathContext0x60AD660;
VegetationManager0x32DE440; ChoreManager0x8574320.
Active grid0x3A11EA4 uses800 columns. Input-only mapper0x60AD45C,
player0x88E3D70 and busy0x366A0F8 never decide a received command.
Mode0x3665F28, map type0x3665ED8, free construction0x3665FC8.
Pitch phase0x379D04C + player*0x583C; keep limit0x37EF8F8.
Queue base+0xB0BF8, cursor+0x370BF4, serial+0xDA0, executing slot+0x84CC8,
phase+0x84CCC, issuer+0x36FA4C. Record tick0,sender4,opcode8,state9,command12,
body17. Command identity is sender*100000000+serial.
Pitch bound+0x204C6C4; lookup0x695C0; validation0x77E60.
Quote scratch+0x204E6FC..+0x204E78C, reason0x60AD4B4 and distance0x34A9F5C
are restored after queries. Height+0xD7E5A0, vegetation+0xA6F260,
structure+0xB0BCA0 and packed task layer+0x1EA23F0 retain audited layouts.

On a DLL update re-audit producer/execute chains, all modes and terminal branches,
queue identity, stock/phase/debit, task bounds and SE/Fixes/ExtraFeatures sites.
A hash mismatch disables this feature; no old RVA fallback is applied.

The production native fixture executes the actual preservation emitter, including
GPR/XMM/flags/stack/branches, scope identity and queue recycling. Its moat fixture
injects only native boundaries and executes the production ApplyMoat invocation
to capture register/stack parameters and refresh order. Coordinator tests execute
the actual protocol/coordinator with boundary doubles, including64 fields per Chore,
multiple pending lines, immediate Send execution, skips, exhaustion and immutable
buffers. These checks do not substitute for real gameplay or host/client acceptance.
