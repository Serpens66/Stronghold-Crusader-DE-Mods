# Unit load, health and ordinary wall placement contracts [Scope: Vanilla + mod integration]

Static audits, 2026-11-12. Canonical installed CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
was rehashed against CURRENT.json and its selected DATABASE_INFO.json. Statements
below concern that image. Generic decompiler function names remain candidate
metadata; concrete instructions, parameter flow and field comparisons establish
the contracts. No callable signature is inferred merely from a decompiler header.

## Provenance and confidence

The selected database is resolved through CURRENT.json / DATABASE_INFO.json,
opened read-only. Evidence retained under `.work/a/pf-opt/001/` includes
`native-dissolve-audit.md`, `troop-cap-audit.md`, `resources-audit.md`,
`wall-placement-audit.md` and `direction-audit.md`. Their adjacent complete-body
exports, direct-reference inventories and verification manifests retain the
detailed branches, counterexamples and explicit limits. The function-body hashes
and current-image checks are reproduced in `unit-load-baseline-evidence.json`.
These development evidence paths are provenance, not prerequisites for gameplay.

Byte-confirmed core families: allocation/type initialization 0x17FEF0/0x19A240;
update/deletion 0x182B00/0x143400/0x1869C0; movement cleanup
0x1855A0/0x199CD0; disband 0x186D10/0x199C70; reachability retirement 0x19B820;
quota initialization/admission 0x26370/0xCBD90/0xABB90 and their recorded consumers;
Stone capacity/add/aggregation 0xB58B0/0xC5AD0/0xBFD30/0xB6100/0xC1CE0;
wall validation/execution 0x7A3B0/0x6F230 and the recorded predicate helpers.
The direction audit verifies 13,738 instruction ranges in 65 complete function
records. Static completeness of those identified families does not prove absence
of arbitrary computed aliases, foreign pointer writes or unknown runtime inputs.


Selected canonical body anchors (SHA covers the full selected extent, including
embedded data; an instruction-only baseline hash may therefore differ):

| RVA | Selected body size | Canonical contiguous-body SHA-256 |
| --- | ---: | --- |
| 0x17FEF0 | 829 | `E6CA69ECD167D6D3E3E911F405D4F5533A5ABE68B77049E5CF74FBAF7CF57AEA` |
| 0x19A240 | 698 | `CC16F14740945972AED985BBDB89F2124900CD5B9D63F1973D13A8EE3EF16F75` |
| 0x182B00 | 9137 | `F640FE9609EEC3199B9C675B91CCF488310B0A23B832BD010FDC80AB00DF153F` |
| 0x143400 | 7001 | `F39AFDE7543E274058168DD080F96621592AEABE0CDE897BCFBDB3A983F25C53` |
| 0xCBD90 | 232 | `D9D550C89D44ACB6441653BBA2847D903E5076F9FAFE9FBC0EB5D9EFF038DFBB` |
| 0xB58B0 | 900 | `29171719D9171A12200BB40F3431D02BB69A389D294104E31FB802653C2C649A` |
| 0x6F230 | 1865 | `E084DA95C5409AB4EBAF47F13A9F2BDEBB35FC52BD89096FDD5F3BE0DB62E506` |
| 0x4BC10 | 1410 | `6FBD011D7B6843806FAAE621BC7719133E9F8D09FD4F2B8D0C0194997413758D` |

Database identity: `sem/FBCB9319/analysis/g72CA4AA9715C/index-2328d977c2fb.sqlite`
(selected via the manifest, not a future hard-coded query path).

## Allocation, position registration and health

Unit manager RVA0x67E8400 uses one-based live IDs1..9999 and stride0x490.
Offsets in this section are relative to manager+ID*0x490. The public typed
GameUnit view starts at +0x65C; native clear begins at the preceding +0x658 fill
word. Do not mix typed offsets with these manager-relative offsets.

Allocator0x17FEF0 scans life0 slots below global0x3668E34, returns0 at exhaustion,
and assigns GlobalId0x6F0, creation tick0x6F4, life0x6E4=1, owner0x6EE and type.
Its world coordinates0x70E/0x710 are supplied world coordinates+4; tile
0x71C/0x71E are division8 and packed tile0x72C uses the native row table.
0x19A240 initializes both currentHP0xA20 and maxHP0xA24 from table0x322820 with
owner strength scaling. Type24 (ordinary spearman) has baseHP10000. Successful
allocation is not completed live initialization, selection or group admission.

0x180230 commits queued next position when movement field0x740 is nonzero,
otherwise registers the current packed tile through0x1802A0. The latter maintains
the ordered tile-unit chain0x4C559B0 using next0x8FA/rank0x912, with map-mask and
packed bounds checks. 0x18BE50 derives visual offsets. None of these routines
directly assignsAI110. Relocation0x19BA40 validates bounds/map mask, unregisters,
updates world/tile/next/packed fields and registers again; it is not an AI110 fix.

## AI110 can end a full-health spearman

0x182B00 promotes NeedsInit1 to Alive2 and dispatches current type through
table0x321CB0. Entry0x321D70 for type24 invokes0x143400. Its AI110 branch increments
signed byte0x986; when greater than32, it clamps32 and writes life3. No HP loss is
required. Optional0xCAC40 statistics/attacker credit do not establish combat as the
entry cause. On a subsequent update, life3 invokes0x1869C0: remove selection,
unregister tile, clear the0x490 record and1000 packed-path bytes. A cleared slot
therefore loses GlobalId/type/owner without necessarily being reused.

Relevant alternative entries must be distinguished:

- 0x182B00 checks a unit-ID/time-nibble cohort. Current PCL0x50EC690[tile]==0,
  no terrain0x40000000, record0x6EC!=1, and no exempt building permit the stale
  pathless-unit branch. After age>=1000 simulation ticks, retry short0xABE>13
  assigns110 and clears counter0x920. Type58 is exempt; type55 relocates instead.
  **0x6EC is not the type field0x6E6.** Separate zero-X/Y or negative-packed-tile
  branches also assign110; earlier invalid map/list checks can mark life3 directly.
- 0x1855A0 blocked movement and0x199CD0 failed individual path cleanup can assign110
  only under their additional selectable0x8FC==0/type/terrain predicates. The
  ordinary spearman update sets0x8FC=1. Failed movement alone is not a deletion rule.
- Spearman moving-state101 completion can assign110 for native slot flag-1,
  session-kind0 and tribe role0x400. Death-animation completion also reaches110.
- Native UnitControl Chore15 consumer0x12BF0 deserializes tribe/opcode/arguments;
  0x199C70 tail-jumps to tribe dispatcher0x11E960. Opcode30 invokes0x186D10 for
  spearmen. Depending on mode and valid owned keep, this marks deletion, assigns110
  with dismissal marker0xA48=1, or converts through109. This is separate from normal
  move Chore17:0x10AE0→0x196100→0x11B520/0x118E00→0x196280.
- Player cleanup0x19B820 requires selectable0x8FC==0, killed0x8F8==0 and byte0x984==0
  plus unreachable home PCL. It writes110, counter0x920=0, fade0x986=0 and **killed
  0x8F8=1**, not0xA48=1. Its pointer starts at life0x6E4; relative0x214 means0x8F8.

The detailed dossier also distinguishes AI group cleanup0x114D40, recruitment
0x1819D0, worker retirement0xCD1F0, special-type owner changes and wall-associated
unit cleanup. Unrelated worker/animal/siege type handlers are not all full audits.
AI110 vocabulary (including an editor export's UnitDissolve label) does not prove
that a player issued disband or that damage occurred.

## ExtremeTroops, quotas and large groups

Setup0x26370 chooses allocator bound3000 or10000; the latter has9999 usable IDs.
Separate quotas0x37EF950/0x37EF954 are calculated by0xCBD90: without qualifying
network slots they equal the bound; otherwise integer(bound-100)/activeSlots-40,
with a documented standard-mode special doubling branch. Do not assign human/AI
names to the two fields without proving the slot-role mapping.

All identified direct quota consumers gate further recruitment/spawn and warnings;
allocator exhaustion returns0. That family does not retire already-existing units
or assign110. A fixed owner1000 threshold causing excess-unit auto-deletion is
refuted for this audited family. This is not proof that every other subsystem is
unlimited. Group reader0x119F90 uses a10000-bit membership set. Movement0x11B520
has per-member eligibility guards and a4000 candidate-allocation loop boundary;
selection/group count alone does not prove every member accepted a move.

## Stone provisioning and ordinary wall cost

Good4 maps to Stores structure10. Canonical Stone capacity is48 per2x2 storage
record. An ordinary Stores section has four origins(0,0),(3,0),(0,3),(3,3): at most
192 Stone when all four are live and empty; other goods reduce usable capacity.
0xC5AD0 counts live matching owner/type storage, existing Stone free room and empty
records. 0xB58B0 rejects a request exceeding that capacity before mutation. Its
later distribution failure has no general rollback guarantee.0xB6100 checks
identity/count and0xC1CE0 rebuilds actual aggregates synchronously.

For ordinary wall mapper25,0xCF640 reads Stone and returns at most250 affordable
visits, based on2*Stone with remainder accounting; cheat/editor have separate
branches.0xC8CC0 charges actual new cells with the matching remainder.80 new ordinary
wall cells require40 Stone with zero prior remainder (41 suffices for either
remainder); Stone80 is therefore sufficient, not the minimum native cost. A
request200 is a different and potentially unsatisfiable storage prerequisite.

## Ordinary wall validation and range

0x6F230 validates the whole raster through0x7A3B0 before placement. Any rejected
intermediate/end cell prevents the whole command. Mapper25 requires no previous
UI preview or selected wall-type state. Empty unit/building layers, flat height
and walkability do not establish every native terrain/proximity predicate.

Per-cell tests include terrain/structure masks, occupying-unit eligibility,
hostile-unit proximity0xEE640, foreign-alliance wall/structure searches0xEE320 and
0xEB9A0, and session-dependent build range0xEEF90. The range uses maximum-axis
distance0x79C0 to the owner's keep reference or allied references (half radius).
Native0x6AF00 map-size radii:160→45,200→50,300→60,500→80,600→90,700/800→100;
default, including400,70. The entire intended line must satisfy the gates.

Validation status at tile-manager+0x204E6E4 starts invalid and clears only after
full success. Reason+0x204E704 is not reset on every entry, so an old reason alone
does not identify the rejecting branch. Maximum limits raster visits (including
endpoint revisit), not merely newly created cells. Successful ordinary placement
sets wall logic0x100, raises height90 and encodes owner-1 in low owner bits.
Read back every requested cell; invocation/void return is not success evidence.

## Packed direction mapping and producer boundaries

0x4BC10 builds800 row descriptors; widths2,4,...800,800,798,...2 total320800.
Row-prefix table is0x402FF2C; inverse-row table0x3AAE2A4 is built by0x50190.
Coordinate mask belongs to engine base0x3A11DC0, distinct from tile-manager0x405EDB0.
The expression x+rowPrefix[y] with numeric0<=ID<320800 permits coordinate aliases;
it is not equivalent to playable-diamond filtering or scanning320800 unique IDs.

Direction bytes start0x51890D0. Producers include0xD86F0, gate pair0xD8CE0,
approach bits0xE0530/0xE06F0/0xE0770, reverse repair0xDE6A0 and full initialization
clear0xE6250.0x16240 case23 also receives grid chunks via0x1F5F0→0x7140→memcpy;
its indirect dispatch-thread ownership remains unproved.0x22020 is a checksum
reader, not a writer. No general immutable-grid/global-lock contract is established.

## Test observations and external adapters [mod integration]

PathfindingLoadTest run013 observed id2261/global5014 at deletion-Pre with initial
and current/maxHP10000, killed0, AI110, life3, unchanged position364,310. The actual
entry cause remains **UNKNOWN**: no PCL/retry/age/triggering-caller history establishes
which native branch ran. This is neither a confirmed Pathfinding defect nor proof
of combat/disband/cap enforcement.

Current Extender public keep-position getters read the same native reference used
by0xEEF90. Its existing0x6AF00 hook can use KeepProximityOverride or a public radius
dictionary; the Vanilla radius table alone is not the installed effective radius.
Public CreateWall raises mutable observer events and returns void. Public
TryAddGood observers can change arguments/results; observe actual resource totals.
These external contracts are separate from the native facts above. Generic
CreatePrefab(free) does not turn ordinary wall mapper25 into a free6F230 call.

Pathfinding's direction watcher observes coordinate aliases separately with a
40000-coordinate-slot budget. The preservation audit licenses only scalar ordered
byte-read/shadow-update/callback behavior; it does not license bulk pre-reads,
deduplication or cadence changes. Managed parity and Mono timing are separate
mod-specific evidence, not new Vanilla guarantees.
