# Updating MoatMove for another native DLL

Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (state 30.10.2026). Read
`CURRENT.md`/`CURRENT.json` of the native baseline and verify the installed canonical DLL before using any address below.
MoatMove checks the reference hash at startup (`ReportNativeLibraryVersion`, `requireCurrentVersion: true`) and does not
start on another DLL.

## Ownership

- BugfixesAndQoL's shared command runtime (`BugfixesAndQoL/src/UnitCommands`) owns every command hook, detour and the
  pre-builder recovery inline hook that MoatMove's provider code runs inside. Their addresses, resolution rules and the
  recovery machine contract are documented in `BugfixesAndQoL/UpdateToNewDLL.md` ("Command engine ownership" and
  "Shared command runtime: address inventory and recovery contract"). MoatMove installs no hook of its own.
- MoatMove owns the entries below: the Fast command entry it calls, the fire scan, the Assassin single-route decision and
  the two failed-build replays. The anchor core (`native/MoatMoveAnchor.cpp`) reads no game memory; it receives masks and
  starts from managed code.

## Fast command entry (`src/FastMovementScheduler.cs`)

Feature owner: MoatMove's Fast fallback (used when the anchor core is unavailable). BugfixesAndQoL installs the hooks at
`0x196100`, `0x12BF0` and `0x11C3A0` (`PermanentCommandHooks.InstallTraversalCommandHooks`) and hands MoatMove the
originals.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Player move entry | `0x196100` | `48 83 EC 48 48 63 C2 4C 8D 1D 12 06 B3 07` (`sub rsp,48h; movsxd rax,edx; lea r11,[TribeManager]`). A captured player move is replayed later through the original with all six Win64 arguments (signed bits kept). |
| Chore execution mode | `0x85F8FEC` | `int`, read only, when the target-order chore (`0x12BF0`) runs: `0` means the chore executes (not serialization), so the order counts as a player action. No pattern; hash-bound. |

On a new DLL repeat the audit of `0x196100` (command-state mutation, call into `0x11B520`), the chore path
`0x12BF0 -> 0x199C70 -> 0x11E960` and the global at `0x85F8FEC`.

## Fire effects (option AvoidFire, `src/FireRouteRules.cs`)

Feature owner: MoatMove fire route rules. Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 29.10.2026). Read-only: the scan never writes native memory.

| Entry | Reference RVA | Pattern and derivation |
|---|---|---|
| Effect removal pass | `0xA2E60` | `RemovalPattern` (48 bytes, displacements wildcarded). `lea rdi,[rip+d32]` at +0x0F gives the effect manager (`0x2F7A680`); the pattern also pins `imul rcx,rax,0E8h` (record size) and `cmp word [rcx+rdi+3Ch],2` (state field). |
| Fire of a tile | `0xA02D0` | `LookupPattern` (50 bytes). `lea rdx,[rip+d32]` at +3 gives the per-tile effect list heads (`0x4CF23F0`, after the records); the pattern pins `imul r8,rdx,0E8h`, type `cmp word [..+3Eh],9` and the list link `movsx eax,word [..+32h]`. |

Record layout used: slots 1..5999 (`0xA2D80` relinks 0x19..5999, `0xA2E60` purges the active records of 1..0x18),
state +0x3C (0 = free, 1 = new, 2 = active: `0x9F960` moves 1 to 2 on the first update, 3..8 end the record;
Vanilla's search mask `0x9C6B0` counts `state == 2 && type == 9` only, and so does `FireRouteRules`), type +0x3E
(9 = fire, written by `0x9C120`, `0x9C450`, `0x9DCF0` through `0x9B2B0`), native tile id +0x60 (int; `0x9F5B0`
unlinks by it). Tile ids convert to coordinates with the
Extender's `GetTileVectorFromId` and are checked back with `GetTileId`. Both entries resolve through
`Shared.NativePatternResolver` (reference RVA first, unique executable-section pattern fallback); the derived
manager must lie before the tile heads by at least 6000 records. Any failure logs an error and registers no
fire rules; movement keeps working. The record layout has no second semantic source beyond these anchors, so
the feature stays hash-gated with the rest of MoatMove.

Fire abort and escape (`src/RouteReplanWatch.cs`, `src/FireEscape.cs`, audit 01.11.2026, same hash). No new RVA is
resolved at runtime; the following Vanilla facts are hard-coded and must be rechecked on a new DLL:

- **Damage only on the tile itself:** `0x99420 -> 0x9E030 -> 0x192400`; tiles whose flags (BugfixesAndQoL's
  `tileFlags`) intersect `0xBA7001B1` (walls, raised, crenels, fords ...) take no fire damage
  (`FireImmuneTileFlags`). Vanilla has no flee behaviour; `0xDCE60` (+0x406) only makes a unit hesitate once
  before entering fire.
- **Soldiers:** `GameUnit.r_SelectionAllowed` (+0x2A0) is set by every military update routine and 0 for
  workers, civilians and firemen. Siege equipment is filtered by `eChimps` like Vanilla's group move
  `0x11B520` does. A fireman stopped in state 3 extinguishes from afar (`0x159C30 -> 0xC3C80`, no distance
  check) and workers treat a forced route end as reaching their job target (`0x188340`); both are therefore
  never stopped or re-planned.
- **`0x196280` (MoveToTile):** returns 1 when a route was published and 0 on failure; a failure (`0x199CD0`)
  keeps state, cursor and length but zeroes the route buffer, so MoatMove then stops the unit explicitly
  (target = movement start gives state 0 and length 0). The result is read from the Extender's
  `OnUnitMoveHere` Post (`ReturnValue`).
- **Escape targets:** `movementTargetAvailability != 0` and tile flags without `0x30`. Targets on unburnt pitch
  (`TilePropertyFlag.PitchTrap`, the logic grid bit 0x8 set by `0x59380`; a simulation bit, unlike the
  client-dependent display bit MiscDisplay 0x2000) or next to fire are only a fallback.

Soldier moves as Vanilla group commands (`src/SoldierTribeMoves.cs`, `src/RouteReplanWatch.cs`, audit 02.11.2026,
same hash; Extender APIs only, no RVA of MoatMove's own):

- **Why:** `0x196280` (MoveToTile) publishes a route but never sets the military walking state; `0x11B520`
  writes per admitted member `r_AIState` = 101, +0x342 = 0, +0x3A4 = 0, +0xEC = 0 before `0x196280` (122 with
  an exit route after a failed `0x196280`). A standing soldier moved by `0x196280` alone glides.
- **Command:** `IssueMoveHereCommand(tribe, x, y, false, 1, DefaultInSync)` equals the player move
  `0x8C5F0 -> 0x196100 -> 0x11B520(patrol 0, newOrder 1, flags 1)` without `0x196100`'s tribe +0x56C step. The
  move type is only tested `!= 0` (unit +0x78 marker counter). The result 1 does not prove a move (no admitted
  member, failed `0x196280`); MoatMove checks `r_AIState` 101/122 afterwards. Admission: AliveState 2,
  +0x29C/+0x29A/+0x40F/+0x428 zero, `r_AIState` != 207, +0x90 != 1, not types 40/41/61, types 39/77/58/59/60
  need their crew state (+0x3B0).
- **Single-member tribe:** the slot is the commanded tile (`0xDA590` seeds it, `0xE1D30` accepts it).
- **Membership:** `0x11D370` (assign) and `0x123EA0` (remove) never touch state, route or targets; removing
  the last member deletes the tribe in the same tick (`0x1261F0`). `Create` is `0x119C00` (slots 1..4499).
  An AI soldier without a valid tribe gets a typed AI tribe from `0x118660` (call-site coverage probable).
- **Order tracking:** `0x11B520` writes the two-point order (`r_PatrolPoint1` leader tile, `r_PatrolPoint2`
  commanded tile, index 1, count 2) only with `newOrder != 0`; AI legs pass `newOrder 0`.
  `r_ObjectiveTargetTileX/Y` is not written. MoatMove therefore tracks the last move (`OnTribeIssueOrderMoveHere`
  Pre) and target order (`OnTribeIssueOrderWithTarget` Pre) per tribe and marks player moves inside `0x196100`
  (MoatMove's move hook, queue flags excluded) as plain.
- **Call site:** the simulation tick (`APIShared.Simulation.SimulationTickEvents`, before unit updates,
  `0x1261F0` and tribe AI) has no flood or slot scratch of `0x11B520` open; never call it from a
  `0x11B520`/`0x196280` Pre or Post handler.

## Assassin single routes through the anchor core (`src/AnchorRouting.cs`, `TryPublishAnchorSingle`)

Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (audit 30.10.2026, summary in
PathfindingRoadmap.md). No new RVA: the decision reads path-manager fields of the builder call already validated
for anchor publication (`+0x80`, `+0x84`, `+0x88`, `+0x94`) and the path region grid (`0x50EC690`, through
BugfixesAndQoL's `pathRegionGrid`). In `0xF4930`, an Assassin request (`+0x88 != 0`, `+0x80 == 0`) with start and
target in one region runs `0xDA590` first; only its failure reaches `0xD9C40`. The core's ground route replaces
exactly that first search; `+0x94 != 0` (second pass through `0xDB650`) and different regions keep Vanilla. Recheck
this branch order and the four fields on a new DLL; the feature stays hash-gated with MoatMove.

## Failed ground builds (`src/GroundFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallGroundFailureProofs`)

Feature owner: MoatMove (anchor core active). Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 30.10.2026, summary in PathfindingRoadmap.md). Inside one group move a start in a proven-closed failure set
replays `0xF4930`'s ground failure path without `0xDAAC0`/`0xDA590`; the region gate (`0xE2F60`) and the direct
attempt (`0xF32B0` for profile 0, `0xF3060` otherwise) still run natively, once each, as in Vanilla.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Region gate | `0xF49C2` | `41 8B D2 44 8B C8 E8 93 E5 FE FF 85 C0 0F 84`: `mov edx,r10d` (player), `mov r9d,eax` (target region), `call 0xE2F60`, `test`, `jz` (return 0 without writes). Called through the same address (a hook on it runs as in Vanilla's call); it increments `manager+0xC8`. |
| Profile branch | `0xF4A09` | `85 F6 74 7F E8 4E E6 FF FF 85 C0 75 7F`: `test esi,esi` (profile), `jz` to `F32B0`, `call 0xF3060`, `test`, `jnz` (success). |
| Direct attempt call | `0xF4A8C` | `E8 1F E8 FF FF 85 C0 74 81 FF 83 A8 00 00 00`: `call 0xF32B0`, `test`, `jz`, `inc [rbx+0A8h]` (success counter, replayed on a direct success). |
| Direct failure counter | `0xF4A2B` | `FF 83 AC 00 00 00`: `inc [rbx+0ACh]` (replayed). |
| Search flag | `0xF4A35` | `89 6B 7C E8 83 2F F1 FF`: `mov [rbx+7Ch],ebp` (0), `call 0x79C0` (scratch object `0x34A9F50`, not replayed: every reader writes it first). |
| Skipped searches | `0xF4AF5`, `0xF4ACF`, `0xF4A78` | `call 0xDAAC0` (`E8 C6 5F FE FF`), `call 0xDA590` (`E8 BC 5A FE FF`; with `+0x80 != 0` directly `E8 13 5B FE FF`, same failure writes). |
| Direct attempts | `0xF32B0`, `0xF3060` | Entries `48 89 5C 24 08 48 89 6C 24 10` and `48 89 5C 24 10 48 89 6C 24 18`; called natively (`manager, sx, sy, tx, ty`). Both step only over set direction bits of `0x51890D0` and write the unit's route buffer. |
| Search stamps and connections | `0xDA783` | `movsx r11d,byte [rdi+rdx+51890D0h]; cmp [rdx+rdi*2+52C254Eh],r8w` (BugfixesAndQoL's cursor proof contract): stamps `0x52C2550` (`nativeProbeGrid + 0x9CA40`), direction bytes `0x51890D0`. |
| Neighbour offsets | `0x405EDB0` | Per-row direction offsets (as BugfixesAndQoL's cursor proof). |

Path manager fields: start/target `+0x08..+0x14`, modes `+0x80`/`+0x84`/`+0x88`, generation `+0x04`, search flag
`+0x7C`, counters `+0xA8`/`+0xAC`, route length `+0x155F68`. Region gate inputs: `0x50EC690` through BugfixesAndQoL's
`pathRegionGrid` (read as signed shorts, target region 0 returns 0 without `0xE2F60`). There is no
pattern fallback: the replay is bound to the audited instruction sequence, so any byte mismatch logs an error
and leaves the feature off (every member keeps Vanilla's builder). On a new DLL repeat the audit: F4930's ground
path and writes, F32B0 and DAAC0/DA590 edge rules, the readers of search stamps/distances/queue/continuation and
of `0x34A9F50`, and BugfixesAndQoL's gate filter (`GateNativeSearchScopes`, `IsDirectionAllowed`).

## Failed moat-mode builds (`src/MoatFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallMoatFailureProofs`)

Feature owner: MoatMove (anchor core active). Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 30.10.2026, `GROUND_ROUTE_BUILDERS.md` "0xF4930 moat mode and 0xDAFD0"). A moat-mode request whose start lies in
the stamped component of an earlier failed `0xDAFD0` search, with unchanged edge inputs on its border, replays `0xF4930`'s
moat-mode path natively up to `0xDAAC0` and skips `0xDAFD0`.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Moat-mode entry | `0xF49D5` | `xor ebp,ebp; mov [rbx+7Ch],1; mov [rbx+155F68h],ebp; cmp [rbx+84h],ebp; jne` (23 bytes, replayed writes). |
| Failure writes, 79C0, budget | `0xF4A16` | 65 bytes: `lea rcx,[0x34A9F50]`, `inc [rbx+0ACh]`, `mov [rbx+7Ch],ebp`, `call 0x79C0` (called natively: `rectangle, sx, sy, tx, ty`), `mov eax,[0x34A9F5C]; add eax,4; mov [rbx+155F68h],ebp; imul eax,eax; cmp [rbx+80h],ebp; je`. |
| DAAC0 call | `0xF4AA0`, `0xF4AD6` | `cmp [rbx+88h],ebp; je` and 40 bytes ending in `call 0xDAAC0; test; jnz` (budget `eax+eax`, mode 0; called natively through the same address). |
| DAFD0 dispatch | `0xF4AFE` | 92 bytes: `+0x88`/`+0x84` checks and `call 0xDAFD0` with 400,000 and mode 0 (skipped for proven requests). |
| Failure return | `0xF4A7D` | `test eax,eax; jnz; xor eax,eax; jmp` (return 0). |
| DAFD0 validity, generation, budget, stamps | `0xDB004`, `0xDB070`, `0xDB164`, `0xDB519` | `0x3A11EA4[y*800+x]` test, `inc [r15+4]` with the 32,000 wrap, ring head compare with `param_6` and `cmp r13d,0x4E51F`, distance/stamp writes (`0x5225B10`, `0x52C2550`). |
| Edge inputs | `0xDB293`, `0xDB442`, `0x107160`, `0xC07C0` | `lea rcx,[0x32DE440]; call 0x107160`; `movsx edx,[0x4B6AA50]; lea rcx,[0x64CCBB0]; call 0xC07C0`; the predicate (47 bytes, `lea rdx,[rip+0x049C6EA6]` = `0x4ACE010`, stride `0x9C`, word `+0x6A`); `0xC07C0`'s type word (`+0x12E`, stride `0x32C`). |

Tables read for the border hash: `0x51890D0` (direction bytes), `0x48F71B0` (flags), `0x4DDD350` (heights), `0x4B6AA50`
(buildings) with `0x64CCCDE + b*0x32C` (type), `0x4ACE010` with `0x32DE440 + i*0x9C + 0x6A` (special-structure state);
`0x405EDB0` per-row neighbour offsets (also index `y*8 - 1`, as DAFD0's side test reads it), `0x402FF2C` row starts and
the Extender's column table. DAFD0's and DAAC0's direction loads (`0xDB242`, `0xDACB2`) are not pinned: APIShared's gate
filter hooks them. There is no pattern fallback; any byte mismatch logs an error and leaves the feature off. On a new
DLL repeat the audit: F4930's moat path and writes, DAFD0's exhaustiveness (ring size vs budget) and every edge input,
DAAC0's relation to DAFD0, and the readers of search stamps (see "Failed ground builds").

Plague clouds (`src/PlagueRouteRules.cs`, `src/PlagueDamageModel.cs`, option AvoidPlague, audit 02.11.2026, same hash). The
records come from the effect manager `FireRouteRules` binds; no RVA of their own. Hard-coded Vanilla facts to recheck on a new DLL:

- **Records:** type `+0x3E` = 22 (`ProjectileType.Disease`), state `+0x3C` 2 (damaging until 3, fade phases 1016..1023 included;
  creation in state 1 does not damage). Position `+0x4C/+0x4E` in 1/8 tiles; `0x9C730` takes the tile as position >> 3.
  Step every 31 ticks (`+0xAA`), direction `+0xB0` (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW), steps left `+0xB2` (a new
  direction from the simulation RNG at 0).
- **Area (`0x9C730`):** cloud tile, 8 neighbours, 16-tile ring (offsets at `0x2DB750`), all within Chebyshev distance 2. Vanilla
  gate for neighbours and ring: `|H(cloud) - H(tile)| < 120` with `H` = `0x6B910` (raw height `0x4DDD350`, or building roof
  `0xC07C0` on raw height, on DefaultHeight for main and inner gates). Roof table in `PlagueDamageModel.Roof`.
- **Per-unit gate (`0x192750`):** tile flags `0x10000100`, building types 40..47 and 74..78, unit-type table `0x32BC40` (among
  soldiers only lord and healer are immune), healer resistance only scales the damage.
- **APIShared policy:** lanes and standing planes as in `APIShared/src/Projectiles/PlagueHeightAssembly.cs` (`GenerateSurface`,
+
+  returns no policy (Vanilla rules).
  returns no policy (Vanilla rules). A unit's standing height is taken as its tile's plane (exact for standing units on ordinary
  tiles; drawbridges and the unit field `+0xA5A` differ).
