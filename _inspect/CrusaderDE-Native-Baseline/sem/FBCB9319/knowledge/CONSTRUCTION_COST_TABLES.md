# Construction cost table identities [Scope: Vanilla + mod integration]

Static audit, 2026-11-15. Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256:
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Native function names remain candidate labels; the table accesses and explicit managed
translations below are confirmed in matching exports and canonical-image instructions.

## Table extents and data flow

`FUN_1800C33A0` loads 110 interleaved records, each five signed Int32 fields in
wood/stone/iron/pitch/gold order. Row zero is RVA 0x2DE240; the source cursor is
0x2DE244 (stone field), increments by 20 and stops at 0x2DEADC. Destination live row
zero is RVA 0x67E5B8C. `FUN_1800C8A90` reads the current row using building type
times20, with material fields at +0/+4/+8/+12/+16. Its builder/siege/free-building
branches decide whether and where resources are charged; the table is not a quote
for every current mission context.

The managed private static readonly GameData.game_data_txt is a distinct Int32[545]
(109 records). GameData.getStructureCosts applies current free woodcutter/granary/
marketplace exceptions, explicit aliases, and array bounds before reading it.
HUD_Main.UpdateRollover calls that method and displays at most two nonzero material
entries; SetRolloverData accepts only column0 or1.

## Explicit menu aliases

- Mangonel tooltip structure115 maps to native row86; Ballista116 maps to row87.
  Both default records contain (0,0,0,0,50). Native successful stationary-engine
  construction at RVA0x138D0 maps unit types0x29/0x3D to those same cost rows before
  calling 0xC8A90. Rows86/87 are named TOWER1_DESTROYED/TOWER2_DESTROYED in eStructs;
  those names do not make tooltip IDs115/116 valid native indexes.
- Wooden gate variants127..130 map to47; small stone gate131/132 map to46; large
  stone gate133/134 map to45.
- Garden menu structures118..120 map to66. Pond structures121/122 and184..187 map
  to104. These are lookup aliases rather than extra cost records.
- Brazier structure114 is outside both tables and has no regular construction row.
  Its actual placement is a static projectile decoration through Chore69.

## Limits

A valid extended enum value is not proof of a valid cost-table index. Native row109
(Dock) is inside the native table but outside the managed table. The separate table
capacities must be checked independently. This audit establishes static data flow,
not a new runtime or multiplayer acceptance run.

### Extender refund identity and deletion bindings [mod integration]

Game acceptance on 2026-11-15 exposed two integration defects under the canonical
FBCB9319 native image. Resolve the actual installed SE constructor AddDetour AOBs:
`c_game_building_delete_hook_impl` targets C4290 (mark state3), while
`c_game_building_bulldoze_hook_impl` targets B8310 (terminal clear). The nearby SE
comments describe the opposite names and cannot establish this lifecycle contract.
Production wrapper tests extract the selected callback from compiled APIShared IL,
then its AOB from installed SE IL, and assert the canonical executable targets.

The regular C4480 material interceptor at C487A obtains an ID from caller stack
`29 * IntPtr.Size`; this is beyond C4480's own 0x58-byte frame and is invalid when
managed Original wrappers change its caller. The failed game run read nonzero
pointer bits as a building ID and overwrote all refunds with type-zero costs.
A permanent, shape-checked SafeIlHook replaces only that initial stack read with
SE's RefundContextBuildingId, which its entry wrapper sets after Pre and before
Original. Preserve the remaining material calculations and register writes.
Final generic-policy ID/percentage arguments are synchronized with that context
around Original and previous values are restored in finally. Sentinel paths
bypass this material callback. Tests execute the transformed actual identity
prefix with a null CPU-context pointer and check nested/failing context restoration.

### Native Undo parameter contract (2026-11-15)

Canonical image FBCB9319: genuine construction Undo at 63CD0 enqueues opcode75
(0x4B); 17B10 handles two signed Int16 parameters, mode then object slot.
1F5F0 reads those fields at pending-record +17 and +19. This is a direct Vanilla
parameter body, not opcode106's explicit Int32 blob length followed by bytes.
For example mode1/slot25 begins 01 00 19 00; treating that as length4 rejects it.
Mode1 marks a decoration for removal; mode2 refunds the stationary tower weapon
via C4480's sentinel route before marking it. The normal building Undo arm uses
opcode29 with a100-percent action refund. Current material multipliers still apply.

At execute entry, the immediate enqueue/receive paths and due scheduler 1F7E0
establish mode0, current slot, authenticated sender and transfer cursor0. The due
scheduler does not reset payload-size global +0x84CD4: 17B10 itself publishes4
inside the handler, after an entry detour runs. Never require that stale global
to equal4 at detour entry. The pending fields are authenticated by the current
native Chore identity; recheck identity after reading the two parameters.

### FreeBuild integration probe [mod integration]

Game probe462794c61769 launched the genuine FreeBuild menu and shipped map
A Mightier Oasis. Native worldkind2/mode0, actor=view1. Actual Chore69 paid12
Human gold for a Brazier; genuine Undo removed the object. The old parameter
reader rejected it, leaving receipt1 and payout0. This disproves the earlier
hypothesis that normal FreeBuild never supports this Undo path.

### Corrected Undo acceptance [mod integration]

2026-11-15 17:32:56 local, game rund4d63543b320: the corrected fixed-field decoder
ran against the canonical native image in genuine FreeBuild. Chore69 paid12 extra
gold for the Brazier; actual Chore75 refunded6 at the current0.5 material factor
and consumed its receipt. Ballista and Mangonel each paid50 native +12 Human gold
through actual Chore42, then real Chore75 returned62 and consumed the receipt.
The official runner confirmed plugin/settings restoration at17:33:01. APIShared
runtime SHA-256319482C5A3CA48C29AF69186FF424EA8F15E240E0EA26DA453EC9CF7DBAD6205.
This is local game acceptance; actual host/client multiplayer remains untested.

### Active command attribution [Vanilla facts]

Rechecked on2026-11-15 against canonical FBCB9319. Chore execution1F7E0 sets the
current slot/issuer, mode0 and cursor0, invokes the opcode handler, then marks the
slot state byte+9 as10 without clearing the other attribution fields. Pending
selection21EE0 accepts a signed nonzero state below10. Local23990/received23F10
start at state1; immediate completion uses1DDB0 to clear the whole slot. Local
mode0/99 paths in1CD20 and23620 do not supply a later automatic context reset.
Thus an unchanged current slot/issuer/mode is insufficient after due completion.
The state must participate in both attribution reads; a universal cursor0 check
would reject valid effect/Post consumers after field transfer.

### Gold refund origin boundaries [Vanilla facts]

C4480 (length0x528, SHA256
C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7)
has gold calls C4793:E8 18 11 FF FF and C496C:E8 3F 0F FF FF, both toB58B0,
returning atC4798/C4971. The former serves stationary-unit sentinels, the latter
regular buildings. C45AB adds only stone/wood. Whole-function instruction/CFG
inspection shows no entries inside either five-byte CALL. SE's C487A material
interceptor is separate. Earlier material-add failures can callD05B0, whose
space_warning5.wav speech path is outside the resource wrapper; counting nested
resource calls alone does not establish the origin of all possible gold gifts.

### Refund callback isolation [mod integration]

The2026-11-15 correction preserves payment receipts and native arithmetic. Two
startup-only CALL retargets use nearby fourteen-byte FF25 relays and the existing
live B58B0 entry, including SE Pre/Skip/Original/Post. A refund-frame token and
complete managed resource-wrapper scope distinguish that native gold call from
nested independent gifts. Complete spawn-wrapper scopes similarly keep callback
raw spawns from acquiring a paid primary object's receipt. Published relays and
callbacks remain process-rooted even on partial publication failure.
Executable ABI and actual SE-wrapper fixtures are required before installation;
these static contracts are not a new game or host/client acceptance claim.
