# Native update contract: Pathfinding

Momentaufnahme vom 07.11.2026, zusammengeführt aus den `UpdateToNewDLL.md` von MoatMove und BugfixesAndQoL. Verträge, RVAs und Herleitungen gelten für den dort genannten Native-Hash. Die Abschnitte stammen unverändert aus den Quellmods: Pfade `src/...` von MoatMove liegen jetzt unter `Pathfinding/src/Engine/...`, BFQoL-Pfade unter `Pathfinding/src/...`, und die DLLs heißen `PathfindingAnchor.dll` und `PathfindingAssassin.dll`.

## Eigentümer in diesem Mod

Alle Befehls-Hooks gehören `UnitCommands`; der Engine-Provider (`src/Engine`) installiert selbst keinen Hook. Nativer Fehlschlag schaltet das betroffene Teilfeature ab, Vanilla übernimmt. Neu seit der Zusammenführung: Ohne Grabenquerung veröffentlicht der Kern Bodenwege nur bei `+0x84 == 0` (`0xF4930`-Grabenmodus bleibt Vanilla, `GROUND_ROUTE_BUILDERS.md`), siehe `BuildEngineRoute`.

# Aus MoatMove


Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (state 30.10.2026). Read
`CURRENT.md`/`CURRENT.json` of the native baseline and verify the installed canonical DLL before using any address below.
MoatMove checks the reference hash at startup (`ReportNativeLibraryVersion`, `requireCurrentVersion: true`) and does not
start on another DLL.

### Ownership

- BugfixesAndQoL's shared command runtime (`BugfixesAndQoL/src/UnitCommands`) owns every command hook, detour and the
  pre-builder recovery inline hook that MoatMove's provider code runs inside. Their addresses, resolution rules and the
  recovery machine contract are documented in `BugfixesAndQoL/UpdateToNewDLL.md` ("Command engine ownership" and
  "Shared command runtime: address inventory and recovery contract"). MoatMove installs no hook of its own.
- MoatMove owns the entries below: the Fast command entry it calls, the fire scan, the Assassin single-route decision and
  the two failed-build replays. The anchor core (`native/MoatMoveAnchor.cpp`) reads no game memory; it receives masks and
  starts from managed code.

### Fast command entry (`src/FastMovementScheduler.cs`)

Feature owner: MoatMove's Fast fallback (used when the anchor core is unavailable). BugfixesAndQoL installs the hooks at
`0x196100`, `0x12BF0` and `0x11C3A0` (`PermanentCommandHooks.InstallTraversalCommandHooks`) and hands MoatMove the
originals.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Player move entry | `0x196100` | `48 83 EC 48 48 63 C2 4C 8D 1D 12 06 B3 07` (`sub rsp,48h; movsxd rax,edx; lea r11,[TribeManager]`). A captured player move is replayed later through the original with all six Win64 arguments (signed bits kept). |
| Chore execution mode | `0x85F8FEC` | `int`, read only, when the target-order chore (`0x12BF0`) runs: `0` means the chore executes (not serialization), so the order counts as a player action. No pattern; hash-bound. |

On a new DLL repeat the audit of `0x196100` (command-state mutation, call into `0x11B520`), the chore path
`0x12BF0 -> 0x199C70 -> 0x11E960` and the global at `0x85F8FEC`.

### Fire effects (option AvoidFire, `src/FireRouteRules.cs`)

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

### Assassin single routes through the anchor core (`src/AnchorRouting.cs`, `TryPublishAnchorSingle`)

Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (audit 30.10.2026, summary in
PathfindingRoadmap.md). No new RVA: the decision reads path-manager fields of the builder call already validated
for anchor publication (`+0x80`, `+0x84`, `+0x88`, `+0x94`) and the path region grid (`0x50EC690`, through
BugfixesAndQoL's `pathRegionGrid`). In `0xF4930`, an Assassin request (`+0x88 != 0`, `+0x80 == 0`) with start and
target in one region runs `0xDA590` first; only its failure reaches `0xD9C40`. The core's ground route replaces
exactly that first search; `+0x94 != 0` (second pass through `0xDB650`) and different regions keep Vanilla. Recheck
this branch order and the four fields on a new DLL; the feature stays hash-gated with MoatMove.

### Failed ground builds (`src/GroundFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallGroundFailureProofs`)

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

### Failed moat-mode builds (`src/MoatFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallMoatFailureProofs`)

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

# Aus BugfixesAndQoL

### 2026-10-02: read-only Assassin gate diagnosis

See the hash-bound baseline `knowledge/ENEMY_GATE_ASSASSINS.md`. Four pre-change runs had 403/402/207/624 NoRoute results; the first three observed open/open, open/closed and closed/closed configurations. Automatic closure in the open runs led to retargeting; no persistent stuck-unit conclusion follows from the counters.

An optional passive APIShared observer measures the existing D9C40 detour, weighted/cache-route edges and native cache contents; it never changes search results. Existing deferred aggregates retain exact counts and numeric sums with no event cap. Gate edge IDs come from mask construction, overlapping identities remain ambiguous. New Unit fields were checked against installed public SE members, and the complete control WORD is reported. Native singleton reads use +0x84/+0x88/+0x90 and ten positive/negative PCL pairs at +0x416D8C/+0x416DDC with stride 8. No new hooks or executable mutations; original route/search/cache algorithms remain unchanged. Native-only flood fields and cache-hit/fallback measurement gaps are explicitly labeled. Versions and README are unchanged.

Abnahme remains open until paired Assassin runs locate the first different reachability result. No additional Assassin movement correction is included in this diagnostic build.

## Optional Assassin gate route policy (2026-10-02)

The existing D9C40 owner also filters its own weighted routes when an APIShared gate snapshot provider is active. No new native RVA, hook span or query. A* admission, route-cache validation and pre-publication validation share one immutable player snapshot; route and suffix cache keys separate its identity. Control uses verified public GameUnit low/high fields at 0x92/0x93 for gate contexts. Negative-destination/continuation calls and inactive-policy operation retain existing paths; freshness/identity failures retain the one native result before writing a replacement field. Preserve legitimate unmasked wall-climb transitions. Test mainmods without Testmod on updates.


### 2026-10-03: independent bridge diagnosis and gatehouse boundary

All bridge edge masks (including the old center seam) have been removed from EnemyGatePathfindingTest. Gate identity/axis linkage is retained. EnemyBridgePathTest 0.1.0 is read-only and independently registered through APIShared; mainmod-owned hooks emit existing results only when an observer is registered. No new native hooks or active bridge policy. The 22:46 experimental mask and its 5,712 NoRoute result are historical, not the current gate policy. Native/SE identities remain confirmed. Pure-gate game acceptance remains pending; see Testmods/EnemyGatePathfindingTest/ACCEPTANCE.md and Testmods/EnemyBridgePathTest/HANDOFF.md. Work commands use existing nested MoveHere and synchronous before/after fields; no task-index or return value is interpreted as proof of work execution.


### 2026-10-07: shared Assassin path hooks

APIShared.AssassinPathAPI is the sole owner of builder D9C40 and endpoint guards
D9F0C, D9F1C, E19D8 and E19F9. BugfixesAndQoL registers its existing weighted
builder and calls the shared original exactly once; its reconstruction wrapper
publishes logical state only. The testmod registers the optional gate rule.
Reference SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Complete audit: _inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md.

NativeX64 Indirect-only builder span is 10 bytes; actual scheme, FF25 entry,
pointer slot and trampoline displacement are verified before publication.
X64InlineHook spans are 16/15/18/23 bytes. Patterns and full incoming-edge checks
are in AssassinPathNativeDefinition; unique executable signature fallback cannot
authorize relocated continuation/layout contracts. Unknown hashes fail closed.
The installed RedBird 1.5 implementation was checked and exercised directly.
Pure assembler guards save flags/RAX/R10/R11, replay remaining wall/surface tests
and accept nonzero IDs only for live gate roof endpoints (types 45/46, IsWall).
Weighted reconstruction preserves the prior optional broad guard relaxation.

Hash-bound grids: building ID WORD at 4B6AA50, tile flags DWORD at 48F71B0,
320800 tiles. One-based building IDs 1..3999 use stride 32C, alive WORD base
64CCCDC and type WORD base 64CCCDE. These bases include the native sentinel.
Re-audit layout, roof placement/rebuild, cursor, AI gate action, reconstruction,
physical climb and neighboring-wall contracts on every native update.
SE's 196870 detour and mainmod selection adapters retain their owners. Fixes
1.25.1 gate filtering EAD8C/departure EACC3 has no overlap; preserve its targeting.
Hooks remain rooted until process exit; activation writes aligned policy data
only. No executable repatching or published teardown is permitted.

Source-linked production tests: _inspect/AssassinGateClimb/tests. Runtime and
installed-member preflight: _inspect/AssassinGateClimb/verify.ps1. Real in-game
human/AI acceptance is pending; see Testmods/AssassinGatehouseClimbTest/ACCEPTANCE.md.

### Exact weighted single-unit route publication (2026-10-07)

Native reference hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
E1640 reconstructs from step distances, with a mutable best distance inside its direction loop.
For target-first nodes T(1,1,d4),R(2,1,d3),P(2,2,d2),S(1,2,d1), it can accept R at direction 2,
then S at direction 4 against the already reduced distance 3. This replaces the cheap
S->P->R->T entrance route with a costly S->T climb. Weighted parents and tick costs
are not represented by the published unit-step distances. This is a confirmed static
counterexample, not yet causal proof of the user's particular gameplay run.

APIShared's existing F4930 owner now opens a synchronous, thread-local single-unit
handoff. Bugfixes stages a copied exact low-nibble-first forward direction sequence;
F4930 still executes once, then its checked output buffer receives the prepared bytes
before the 196280 consumer latches length/cursor/state. Successful staging retains the
original D9C40 field/result until the outer builder publishes. Failure inside a unit
frame preserves native data. Flood/continuation calls never stage; standalone queries
retain the checked stamp/distance publication path. Nested builders shadow all outer
frames, including unqualified/manual-probe frames. Delegates live only in the synchronous
frame, whose Leave restores its predecessor in finally; native hook roots remain permanent.

Existing hash-bound data contracts (no additional hook, fixed RVA, AOB or executable write):
- Path manager at module+60AD660: source int32 +8/+C, destination int32 +10/+14;
  Assassin flag int32 +88, moat flag +84, later alternate-builder flag +94.
  Exact publication requires Assassin enabled and the other two flags zero.
- F4930/E1640/E4E90/196280 prove output pointer +155F60, direction-count int32 +155F68.
- Unit manager module+67E8400: buffer +B4FE78 + one-based unitId*1000, capacity
  1000 bytes/2000 directions. Buffer arithmetic derives the ID and checks it against
  UnitAccess, type, true life, Global-ID, control player and native movement start.
- Existing grid bases: connections 51890D0, direction masks 312620, row table 402FF2C,
  heights 4DDD350, surfaces 48F71B0, building IDs 4B6AA50. Physical DCE60 accepts
  source-forward OR destination-reverse before climbing. E1640 tests its own
  target-to-predecessor connection, so reconstruction capability is checked separately.

All addresses derive from the canonical installed-hash disassembly and the existing
shared command layout validator. Fixed layouts have no independent semantic fallback;
search scope is the supported installed module only. Unknown hashes retain the existing
fail-closed feature initialization. F4930 hook resolution/backend remain unchanged;
its existing section-bounded function signature fallback and displacement checks apply.
On native updates re-audit field layout, buffer ownership, nibble reversal, movement-start
selection, alternate builders and the terminal 196280 consumer before permitting publication.
No new Assembly-CSharp access. Fixes gate targeting and Script Extender selection ownership
remain unchanged; the existing soft dependency loads the mainmod after Fixes.

Regression sources compile the productive handoff, encoder, and actual shared builder/
publication methods. Tests cover the expensive reconstruction shortcut, exact buffer bytes,
length, ID reuse, changed policy/pointer, nested frames and native exceptions. Physical
walking/climbing and open owned/captured gate roof acceptance still require an in-game run.
README files and versions remain unchanged during that acceptance.


### Integrated formation command contract (2026-10-08)

FormationTest is absorbed into BugfixesAndQoL; checkbox/presets/host synchronization keep
their existing key. FormationRuntime in APIShared uses the existing E1D30/E0970/118E00
dispatchers rather than installing a second native detour. Native 196280 remains owned
by the Script Extender event. Formation targets run before UnitMovementContext Pre;
completion runs after its Post. The accepted protocol-5 command carries kind, density,
direction, width, role placement, unit count and deterministic identity/target hash.
Consumed left/right release state is never restored. Shift queue transport retains only
its own marker; no old density encoding or fallback remains.

FBCB9319 native closure: 879A0/86680, 8B7E0/8C5F0, 195E30, chore 10AE0,
196100/11B520, E1D30/E0970/118E00/119F90/196280, and cursor/overlay
8F3DA..90088, 1222A0/417A0/41D10/41D60/436DE/1A13C0. Preserve authoritative
object, Assassin, terrain, region and owner checks. NativeDetour Indirect contracts are
validated by NativeDetourContracts; marker context displaces 17 bytes to 436EF,
AfterCallback, with All GPRs and replayed TEST/JE. Installed RedBird is exercised by
the copied-buffer regression; no published hook is torn down.

The merged marker renderer is process-owned by UnitCommandPathAPI.MoveMarkers.
Drag preview authorization is memoized per native pass; role dots use the same
authorization. Overflow and queue rendering share the original overlay/draw callbacks.
Installed interop is checked by _inspect/FormationIntegration/Verify-Interop.ps1:
GameUnit 0x490, GameTribe 0x688, cursor view 0x480, including all consumed field
widths/offsets. UnitAccess.IsReallyAlive uses UInt16 r_IsKilledByProjectile at 0x29C.
Game APIs are checked without emission against the true Assembly-CSharp.dll.
Fixes 1.26.0 additions (111C00 and 6A5D0) do not overlap this closure.
Formation config migration reads FormationTest_Serp.cfg without modifying it; existing
main keys win, invalid/missing legacy values use Block/2/Off/true.
Re-audit the complete closure, editor release fields, tile grids and both installed
hook backends after native/Extender updates. Runtime versions remain unchanged during
testing. Multiplayer/gameplay acceptance still requires an actual game session.

## Rally-point terrain slowdown (1.0.180)

The independent default-enabled `EnableRallyTerrainSlowdownFix` captures
effective movement delay before the type handler and restores it at the
common cadence hook. Full audit and hook contracts:
`../_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/RALLY_TERRAIN_SLOWDOWN.md`.

For a native update, re-audit the entire recruitment/rally/type-handler/
dispatcher/movement/arrival/terrain chain for all 26 supported types.
Validate hash-bound unit layout, dispatch pattern at RVA 0x18410C,
its 8-byte indirect call plus 6-byte current-ID load, continuation
0x18411A, incoming edges, and actual installed RedBird inline displacement
(14 bytes). Also recheck cadence 0x184203 (23 displaced bytes), terrain
hook 0x19B506, and preservation of elevation and all later terrain/state
calculations. Never infer a new fixed layout from a signature match.

The capture transaction is optional: validation failure disables only
the new terrain correction. Settings publish atomic data flags with an
epoch; published hooks and native tables persist until process exit.
Keep the terrain setting separate from recruit running, including host
sync, presets, reset and search. Run the actual production generators in
`RallyTerrainGeneratorTests` and repeat in-game swamp, ford, uphill,
downhill and level routes with both running settings and loaded saves.

Formation rows follow-up (2026-10-08): internal protocol 6 adds ushort Rows. Wheel adjusts gesture-local rows; drag only sets the eight-sector facing. Width is validated against maximum rank width. Integer local coordinates precede rotation; exact reachable slots are reserved before obstacle fallback. Existing native selectors, permanent input/renderer hooks and installed SHCDESE 2.14.0 contracts are unchanged. No new public API or game-assembly access.


### Command engine ownership (2026-10-09)

The coherent command engine is owned by BugfixesAndQoL/src/UnitCommands;
formations remain BugfixesAndQoL/src/Formations. MoatMove provides its optional
search/traversal implementation through the internal BugfixesAndQoL assembly
contract. APIShared only owns the public managed route-search preference events.
This ownership move does not alter native addresses, algorithms, delegates,
registration order, settings, formats or process lifetime.

Reference native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
NativeDetourContracts.cs is the exact128-entry function-prefix catalog; feature
native-contract files in Runtime/Native/Cursor/Movement/Moat/Attack/Formation
retain their reference RVAs, patterns, derivations and ABI checks. Initialization
validates the reference bytes against the Extender-owned scan image and the
existing bounded resolver. Fixed record/scratch/path layouts remain hash-bound:
a code signature does not prove a new native layout. Existing feature groups
fail closed independently; published hooks stay rooted and installed. No new
fallback is introduced by the move. Reaudit all affected flows, displaced spans,
incoming edges, actual NativeX64 scheme and layouts on a native/backend update.

Native movement wrapper18E1E0 has four parameters;196280 is its distinct core.
Unit records are0x490 bytes; movement preview snapshots/restores the record and
must retain the distinction between preview and actual world notifications.
The cursor dispatcher8C5F0 has PE unwind range[8C5F0,90500) including19 embedded
switch tables; the old candidate database end90001 is incomplete. Do not use
linear decoding of that entire range as proof of executable instructions.

Detailed local review/evidence: _inspect/APISharedOwnership/COMMAND_OWNERSHIP_AUDIT.md,
cursor-dispatch-targets.log, fixes-dynamic-command-spans.json and preserved
before-command-ownership sources. They are workspace audit artifacts, not public
APIShared dependencies. Source equivalence across70 moved files is checked modulo
namespace/friend ownership and two imports. Game acceptance remains outstanding.


### Formation movement permission correction (2026-10-11)

Owning feature: Formations. Reference native SHA256:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The existing 195E30/196100/11B520/118E00/196280 chain and 18E1E0 path probe
retain their existing signatures, search scopes, resolution and fail-closed rules.
No new RVA, hook or layout dependency is introduced. Group-member permission is
owned by Vanilla, not inferred from a positive path query. 11B520 excludes stationary
Ballista (3D), Trebuchet (28), Mangonel (29) and restricted states; 118E00 has its
own member predicates. 196280 does not repeat those checks.

The formation post-order retry now requires a matched, unvetoed terminal Post
with observed return zero. Missing calls, abandoned/vetoed frames and positive
returns never receive direct MoveToTile retries. Recheck current global identity,
life and tribe and retain the original extra parameter. Reaudit these event and
permission boundaries after Native/Extender updates; do not substitute a type list.
Evidence and pending gameplay acceptance:
_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/FORMATION_MOVEMENT_PERMISSION.md.


### 2026-10-11: preserve native ground cursor permission

Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The complete cursor CFG [8C5F0,90500), including its embedded dispatch tables,
shows a separate permission channel from route reachability. At 8F10F R15D
starts at one. Target veto 198C40 at 8F1E5 and selection predicate 1811A0 at
8F1F8 can clear both EBX (reachability) and R15D (permission). 1811A0 scans
selected living, non-killed units controlled by the active native player and
requires a type other than 28/29/3D. These are the native stationary engines;
no mod-maintained type list is introduced. 198C40 retains its target/structure
restrictions. From 8F206 to the call at 8F325 no instruction writes R15.
Function names remain candidate; these register/data-flow findings are static.

The 196870 call at 8F325 is the native Assassin/special-selection fallback,
not a fresh movement permission check. Promoting its zero result from a
positive 18E1E0 path witness previously re-enabled EBX via E2CA0 at 8F350,
bypassing the prior permission veto. This produced false accepted cursor
feedback; 195E30 stages Chore17 before its own feedback/type checks. The later
formation fallback fix protected execution but did not protect this UI path.

The existing six-site adapter now passes its RVA and, only at 8F325, R15D.
The callback clears pending cursor state and retains a zero result when that
native ground permission is zero. Nonzero original/Extender results remain
full-width and authoritative. The other five sites are attack queries and
never consume their unrelated R15. Existing reachable-member and moat route
corrections remain after the permission gate. No marker suppression, extra
order, second 196870 detour, permission probe, or public interface is added.

Producer bytes and the existing complete displaced spans are checked before
publication. Production emitter tests execute all six Win64 callback paths
with permission zero/one and zero/nonzero/full-width results. The complete
production cursor fixture exercises pending-state clearing, provider settings,
route repair and attack isolation. Rendering still reads final native feedback;
formations and Shift commands continue through the original group/member
checks. Visible Vanilla comparison and multiplayer acceptance remain pending.

#### TEMP_GATE_ROUTE_ACCEPTANCE: output-mode evidence (2026-10-11)

Owner: existing UnitCommandPathRuntime F4930 detour; no added hook. Reference DLL SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, RVA 0xF4930. At the existing entry/exit, read already audited manager modes +0x84/+0x88/+0x94 and output length +0x155F68; capture original arguments 2/3 as raw values. F4930 at 0xF4B9C tests +0x94 and argument 3 zero before DB650/DA590 and E1640 mode-1 reconstruction. Entry eligibility is not runtime proof that this branch executed. +0x94 remains excluded from exact staged Assassin publication. Use the existing hash/signature and displaced-span validation of the functional owner; no separate executable patch or address fallback. Unknown/unowned context is not read; diagnosis is inactive without an observer and cannot authorize publication. On future native updates re-audit the whole F4930 search/reconstruction flow rather than removing this guard.

#### Drawbridge nonlethal gatehouse damage (2026-10-13)

Owner: src/DrawbridgeDestructionFix/FixRuntime.cs, permanent X64InlineHook.
Reference native SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audit the complete direct damage function 0x7EB00..0x7F88A, all incoming
edges, bridge coupling/removal, area damage, demolition and deferred removal
before adapting this feature. Hook 0x7EE6B displaces exactly 15 bytes (4+6+5),
continues at 0x7EE7A, and skips early bridge removal at 0x7EF87 only when
argument 8 is 1, type is 45/46 and the newly stored signed Int16 HP is positive.
Native manager slot stride 0x32C, type +0x12E, health +0x168; public view +0x5C.
Original displaced instructions must execute fully when disabled. Preserve
RAX, flags and all other live state. The installed RedBird backend must again
assemble/decode the production emitter and confirm its 15-byte displacement.

Test-DrawbridgeDestructionFix.ps1 and tests/DrawbridgeDestructionFix.Tests
verify hash, original bytes, layout, full-function control flow and 150 native
execution cases. Live interior bytes must match the reference function;
Extender entry instrumentation is allowed only in the first audited 64 bytes.
Unknown hash, conflicting bytes or an unexpected span abort this feature only.
Installation candidates roll back before publication; published hooks and
subscriptions remain statically rooted and activation changes only data.

EnableDrawbridgeDestructionFix is SyncHostOnly, defaults true, resets true and
participates in the existing preset/host/client/trail rules. Effective state
is EnableMod && EnableDrawbridgeDestructionFix. Existing dependency minima
and main version remain unchanged during main-mod acceptance. APIShared is
unchanged: existing public damage/coupling/lifecycle services suffice and no
second user justifies a new public native service. Diagnose installation and
state transitions, one identity-checked protected hit, and at most one observer
error; the observer exits immediately after confirmation. Do not restore test
snapshots, map summaries, geometry caches or per-hit logging.

Independent testmod revision 2 editor acceptance: 2026-10-13 16:57..16:59,
668 paired calls, 665 protected native decisions, 64 detailed surviving-hit
bridge captures and 3 lethal removals (both stone gate sizes), no errors or
pending follow-ups. This confirms the testmod, not main-mod or multiplayer
acceptance. Main-mod testing must include two bridges and bridge states,
option/mod disabled, demolition, projectile/area damage and multiplayer.


### Infinite patrol isolation from the Shift queue (2026-10-17)

Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The existing hash-bound Chore-17 hook also reads the signed patrol-mode staging
field at RVA `0x86C1338` (between Y `0x86C1334` and move type `0x86C133C`).
Producer `0x195E30` stores argument 5 there; Chore-17 handler `0x10AE0` packs
one byte and sign-extends it on unpack before `0x196100` / `0x11B520`.
The field is read only during outgoing Chore packing. No additional hook or
executable-memory mutation is introduced. No semantic relocation fallback is
available for these staging globals; the existing fixed-layout hash gate remains
mandatory. On a native update recheck the complete pack/unpack flow and field
width/order together with the existing queue marker contract.

`TribePatrolMode.PatrolInfinite` bypasses queue marking and capture. Chore 71
(`0x176C0` -> `0x11C3A0`) retains its original one-based tribe ID, coordinates,
waypoint index and signed mode. Only the affected tribe's old queue and dedup
state are discarded. `PatrolOnce` and ordinary Shift Moves retain queue handling.
Production-entrypoint regressions: `tests/Issue6.Tests`; actual in-game looping
and real host/client acceptance are separate from those boundary-double tests.



### Formation command speech (2026-10-17, Issue 6)

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
FormationFeature retains the canonical native hash gate. FormationSpeechNative is a
mod-internal callable adapter, not a hook or a replacement movement command.

- 0x11A1B0: selected dominant voice type. ABI: int(nativeTribeManager, int tribe,
  int* optionalCount). RCX/RDX are unused in this binary; R8 is the optional output.
  Despite the decompiler's void declaration, EAX returns the chosen type (verified
  complete disassembly and 0x195EA7 call). It counts selected, alive, local units of
  types below 90; ties prefer the lower type. No parallel voice table is maintained.
- 0x18D460: native selected representative. ABI: int(nativeUnitManager, int firstId).
  Use manager RVA 0x67E8400 and first Game-ID 1. This scans by native type priority
  and uses current-unit scratch global 0x9302C4; capture saves/restores that Int32
  in finally. The representative's current LogicGrid tile must not have bit
  0x40000000. Engineer (Extender enum) with GameUnit+0x388 != 0 suppresses speech;
  this field is manager+0x658+gameId*0x490+0x388, matching global 0x67E8DE0.
- 0xE0E0: int(nativeSoundManager), manager RVA 0x88E72D0. Preserves native
  busy-channel policy, including its per-player exception. This is checked inside
  the native frame, after DLL_RunTick has copied current Unity channel states.
- 0x3BC0: void(dialogueManager, int voiceType, int response, int targetTile).
  Dialogue manager RVA 0x64C4490. Response 6 is used when both leader/anchor axis
  differences are <=72; response 10 otherwise. Response 6 receives GetTileId(anchor),
  response 10 receives zero. Capture leader position before dispatch; never derive
  the voice from a delayed multiplayer receiver. NoChange (native zero) excludes Archer,
  Crossbowman and Catapult; Trebuchet, Mangonel and Ballista are always excluded.
  Archer_debug remains allowed. Native 0x3BC0 owns selection, duplicate suppression
  and special-type branches; 0x3770/0x38A0 and 0xEDF0 remain untouched.

Each new code target uses its complete-instruction signature, scanned only in PE
executable sections; duplicate or missing matches fail closed. An invalid reference
RVA still tries that executable pattern fallback. Data layouts have no semantic
fallback and remain hash-bound. Resolution logs retain method and resolved RVA.
Only speech is disabled on resolution/capture/playback failure; formation and
marker availability are not changed. No hook is added to any of these targets.

Local release capture -> accepted dispatch -> consumed input -> pending speech.
The existing 0x41D10 draw-list reset detour invokes the internal feedback callback
after its original. Full flow: DLL_RunTick 0x86680 updates channels, render 0x41D60
ends at 0x41D10, then dialogue 0x41C0 and sound/export paths run. 0xEDF0 writes
channel flags/path and emits a sound-volume record; the managed EngineInterface
reads speechFileName from returned play-state data. Callback exceptions cannot
repeat the original reset or disable markers. Only a matching thread-local
EngineInterface.run scope, held engine lock and observed render entry can consume
pending feedback on the simulation thread. No-buffer runs retain only the latest
request for at most one monotonic second; stale
map/player/tribe-global identities and feature disable discard it. Consume before
checking busy channels or emitting, so suppression/errors never replay a response.
This feedback remains local and does not change synchronized movement success.

Offline verification: tests/FormationSpeech.Tests executes production engine and
speech methods with boundary doubles, and checks real installed enum/layout,
canonical binary signatures, complete instruction spans and ambiguous/fallback
resolution. Audible output, Mono execution and real Host/Client acceptance still
require an in-game test; no installation is performed by the Issue 6 build.

Engine synchronization is initialized independently of optional audio through the
validated private static object EngineInterface.threadLock. Release-side capture
acquires it before stateSync; UI preview uses only nonblocking acquisition.
Both the temporary 18D460 scratch write and its restore
are inside this lock. The render-end emitter also requires Monitor.IsEntered
on the same object. EngineInterface.run holds it around preDLLCallActions,
DLL_RunTick and play-state copying; no new native call races the simulation.
The offline test checks this field against the real Assembly-CSharp metadata,
not a publicized assembly, and exercises separate input/simulation threads,
unprotected/protected callbacks, nested scopes, bounded pending feedback and idle
scope allocations. Native move types are NoChange=0, DefaultInSync=1, Fast=-255.

2026-10-26 integration correction: internal protocol 9 transmits requested rows
(zero = automatic), full selection count/hash and command intent. Wire fields 11
and 13 are reserved nil values. Preview width/hash never authorize or reject a
command; authoritative admitted members determine geometry and the execution hash.
Both shared destination collections stop after 4001 examined tiles; shortages
reuse valid slots deterministically, empty ordinary plans preserve previous orders.


### Gate attack freeze diagnosis (DEVDIAG only, 2026-10-25)

Reference native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
`GateAttackFreezeRuntime.DevDiag.cs` reads the existing UnitCommandPathRuntime-validated
path manager (reference RVA 0x60AD660), WORD/array contracts already owned by that
runtime, and DWORD counters +0x78 (E1640 failed reconstruction before repair) and
+0x74 (E49D0 component rebuilds). The counters are never reset or written. Unsigned
modular deltas are observations, not proof of repair duration or causality. No
independent address fallback is introduced: if the hash-bound command owner is
unavailable, the counter reader is not registered and reports say unknown.
Builder output buffer/stride/unit identity uses the same validated F4930 publication
contract; ordinary SDK reads use existing public UnitAccess/GameUnit/Tribe fields.

The managed run diagnosis validates the real EngineInterface.run(bool)->int IL:
getFreeBuffer -> Monitor.Enter(object,ref bool) -> DLL_RunTick -> returnBuffer ->
Monitor.Exit inside the original finally. It preserves arguments/results and labels/EH,
and returns incoming IL unchanged on any mismatch. A managed run without DLL_RunTick
is not a native tick. Timings of original native delegates include active SE/mod hooks.
All additions compile only with SERP_DEVDIAG; no new native site, ownership or gameplay
policy is introduced. Update validation: actual-game IL audit, nested/exception/no-buffer
fixtures, normal binary exclusion and DEVDIAG release-gate rejection.


### 2026-10-25: per-cursor mandatory region rejection reuse

Owner: Improved manual unit commands. Native SHA-256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The installed 18E1E0 instruction stream confirms the ordinary branch at
18E391..18E3C6: with manager modes +84/+88 zero, E2610 is mandatory for unequal
PCLs and zero immediately returns failure. Its five arguments are manager,
signed player, signed source PCL, signed destination PCL and signed connection
mode. 18E39A reads mode at native record +9B8 (GameUnit +35C); player +6EE is
GameUnit +92, coordinates +71C/+71E are GameUnit +C0/+C2. The structure bias
is 65C and unit IDs/stride stay one-based/490. 196840 reads the completed-moat
flag from the unit's current tile ID (+72C, GameUnit +D0); do not substitute
GetNativeMovementStart's next-tile coordinates for this wrapper's coordinates.

The optional optimization validates that complete ordinary branch and the real
GameUnit field offsets at startup. It requires the already-published E2610 hook;
failure disables only reuse, retaining full probes. No native entry, hook span,
public API or serializer is added. The existing effective E2610 return is learned
only after one matching zero call and a failed, fully restored native probe.
Same-PCL/arrived/moat/special/ambiguous cases retain their full checks. Proofs
live only inside the existing consecutive cursor batch (8 contexts, 128 keys),
never across ticks, targets or native building calls. All executed probes retain
the full buffers, copies and repair suppression. Reaudit the complete wrapper,
mode hook, E2610 filtering and context isolation on native/interop/hook updates;
there is no signature-only fallback for this branch/layout proof.


### Cursor closed-set negative witnesses (2026-10-25)

Owner: ordinary manual cursor probes only. Native reference SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
No new native hook or native query. The existing F4930 wrapper observes its result
before the unchanged complete probe restoration; E2610 observation remains authoritative
for mandatory region rejection, including unequal zero/sign-extended PCL values.

The constructor already requires that exact full native hash. The optional ground
contract additionally checks the original F4930 prefix and DA783 connection/stamp
load span, never live patched bytes. Its dependent data layout is hash-bound: manager
+4 generation; +8/+C actual start and +10/+14 actual target; +84 moat and +88 Assassin
modes. Stamps are the second 9CA40-byte half of the existing 139480-byte probe-grid
snapshot (RVA 52C2550). Connections are 51890D0, direction bits 312620, row-indexed
eight-int neighbor deltas 405EDB0; Y is the public SE MapColumnLookupTable, independently
used by GetTileVectorFromId. The profile is read live via the validated APIShared
replacement, not SE's incorrectly-strided permission table. Profile Large=0 and
Default=1 follow F32B0/F3060, DAAC0, DA590 and DB650; all ordinary search edges are
subsets of the same directed connections. Special moat/Assassin modes are excluded.

A stamp generation in 1..32000 only proposes a vertex set. The set is validated over
all 320800 slots: actual start present, target absent, every allowed outgoing edge
ends inside the set, with bounded neighbor/row accesses. This direct closure proof
does not trust return zero, queue exhaustion, stale generations, search budgets or
successful reconstruction as evidence. Direction is preserved, never symmetrized.
Gate filtering is allowed only with the exact immutable snapshot captured inside the
existing builder scope; reuse enters a fresh existing scope and checks provider,
snapshot identity and IsCurrent. Missing scope/provider support validates against
the unfiltered superset instead. No new public API or APIShared source change.

Native 18E1E0 checks target availability (3A11EA4, y*800+x) before arrival, then
target flags 30, then profile-zero-only flags 10000100. The pure cursor precheck
preserves that order; these immediates are native bitmask contracts, not unit IDs.
No real order, building-query batch boundary or executed probe snapshot is changed.
Eight eager contexts contain four 5013-ulong sets each; capture requires selection
size >=16 and at most four attempts in one existing contiguous cursor batch.

No semantic fallback is claimed for these data layouts on an unknown hash. On byte,
field/table or policy failure, retain full probes; new optional acceleration alone
is disabled. For a native update re-audit both direct-line profiles, all F4930
fallbacks, edge filtering, stamp/neighbor/row tables and 18E1E0 early-return ordering.
Regression evidence: FriendlyMoatMovement ManualHarness production paths and closed-set
oracle, existing GateRouting/Assassin fixtures; real island timing requires replay.


### Dry mixed-group recovery (2026-10-25)

Historical implementation, superseded by pre-mutation admission below.
Owner: failed human dry-ground Move/queue/patrol commands. Reference native SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
No new hook/site/span. The existing E2610 observer supplies an effective rejection;
tribe Post completes one existing 118E00 fallback only after zero return and zero
prior unit movement attempts, before frame clearing and queue/formation completion.
Original patrol/new-order inputs and native member permissions remain authoritative.

The E2610 query-only closure is manager+98/+C0/+C4 (three int32), bound to the full
native hash and complete 1589-byte body SHA-256
7F875ED003B5E0A047F394632F0F2F952D4364DCC9DD8C9A62D179441B39A0ED.
Original-image writer anchors E2660/E2747/E2C29 validate the displacements and
RIP-derived fallback at manager RVA 60AD660 +98 = RVA 60AD6F8. Independent writes:
E297E/E2996 for +C0; E298A/E29A5 for +98. The whole-function audit establishes the
exclusive write set; +C4 is E2660's counter increment. Query-only changes are
restored in finally; real simulation queries keep their state. No tile/unit buffer
snapshot is needed. GameUnit.r_PathConnectionMode at +35C uses signed-short native
semantics. Target availability uses existing 3A11EA4 y*800+x; PCL/flags use existing
packed tile views. No extra terrain traversal or placement search is authorized.

Unknown hash, writer/field mismatch or ambiguous/mutated command context disables
only this optional recovery; there is no signature-only fallback for the closure.
Re-audit 11B520 rejection/exit branches, 117C70, the whole E2610 and all writes,
118E00 member/waypoint checks, 196280 permission/path flow and Extender Pre/Post
input/result ordering on update. Baseline knowledge/MANUAL_GROUP_REGION_RECOVERY.md
and production FriendlyMoatMovement recovery/cost tests hold the evidence; game
latency and host/client acceptance are distinct from fingerprint/fixture results.


### Mixed-group admission before native order writes (2026-10-26)

Owner: improved manual dry ground commands and their executed queue/patrol steps.
Reference SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
This replaces the Post-only repair; it is not an additional retry/fallback layer.

New inline span 118F50..118F5E (14 bytes, 9-byte target store + 5-byte MOV):
664689BC2934090000BA65000000. Normal resume 118F5E, excluded-member target 118FBE.
The complete 118E00 body SHA-256 is FE838B2718364376F36321E32940F4418AF6678151501D17A74E0F4B0E897A39.
No incoming edge into the span or internal jump table was found. Existing common
entry 118E00 and Fixes' ordinary formation site 11BB38 do not overlap. Validate
immutable instructions plus unmodified live own span, actual backend displacement,
member predicates, EAX/RCX/R13/RSI identities and all continuation registers/flags.
The native stub performs no managed transition; its Windows x64 GS:30 token and
generation/global identity bind the reusable table to one executing command.

The enclosing runtime is full-hash bound. There is no signature-only update
fallback for this semantic closure. An unknown DLL inherits the enclosing runtime's
compatibility gate; within a compatible runtime, span/field mismatch or overlap
disables only admission. Permanent hooks remain installed and logically dormant.
The existing 117BC0 and E7C40 hooks choose the native common branch before its
unnecessary flood, only for the matching supported mixed command.

Additional audited data: GameUnit base65C, GlobalId +94 (native6F0), native mobility
field +442 (nativeA9E), query mode +35C. Independent mobility writer 19A44C reads
3226B0[type*4]. Player wrapper 196100 writes tribe+56C before the group event;
fully rejected invocations restore only that earlier flag through a nested frame.
Owner: the 196100 detour (reference RVA 0x196100, entry bytes
`48 83 EC 48 48 63 C2 4C 8D 1D 12 06 B3 07`, NativeX64 Indirect) is published in the
same transaction as the 118F50 admission gate, so the restore works without a
traversal provider. Its only caller is Chore-17 consumer 10AE0; without a provider it
calls the original exactly once. A provider registered later (MoatMove) only becomes
its logical target; if admission failed, `InstallTraversalCommandHooks` installs the
detour itself as before. Neither the Extender, Fixes nor APIShared hooks 196100
(checked 2026-11-03; the Extender hooks 11B520).
Argument contract: 10AE0 passes the unit manager 67E8400 in rcx (lea at 10C7C, call at
10CAB); 196100 ignores it and writes tribe manager 7CC6720 + tribe*688 + 56C. The
restore therefore addresses the runtime's validated tribe manager, never the first
argument (the earlier `manager == tribe manager` guard never matched Vanilla input).
118E00 clears native748/74C (GameUnit EC/EE/F0), NOT path flags F2/native74E.
196305/19630F read flags F2 and substep F4/native750: current only at flags0 AND
substep8, otherwise next2. Reuse GetNativeMovementStart; preserve unknown native
position-normalization cases (19B1B0) instead of simulating its writes.
Existing E2610 closure98/C0/C4 restores in finally; same-region equality is free.

Re-audit the entire input/Chore/player/group/common/unit/path chain, special-route
exceptions, queue insertion/execution distinction, zero-Post skips, formation
protocol7/full-selection identity and topology invalidation on update. See native
knowledge/MANUAL_GROUP_ADMISSION.md and the production/native execution fixtures.
Real game/editor latency and host/client results remain independent acceptance.



### 2026-10-26: separated-group cursor, gesture proof and ordinary destination slots

Owner: Improved Manual Commands / Formation Enhancements. Reference native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

APIShared VisibleMapRenderEvents now owns the permanent demand-gated entry at
`41D60`; BFQ registers its preparation callback there. The former BFQ context hook
is removed. The shared Indirect/10 publication displaces the first two 5-byte saves
(`48895C2410 48896C2418`) and resumes at `41D6A`; see the shared contract below.
The complete renderer range is `41D60..4580E` (exclusive end), body SHA-256
`3EF71AC141815F4AAD74E1A20B6991425175728DA09EC4F07E2C813DF8533FFF`.
`DLL_RunTick` calls cursor selection at `86920 -> 8C5F0`, then renderer at
`86A68 -> 41D60`. Cursor kind is cleared at `45702` before `4570C -> 41D10`;
the existing end-of-render speech callback cannot authorize a gesture.

The entry has six Win64 integer/pointer parameters, with no incoming flags or
XMM parameter contract. The production guard bypasses Mono when no gesture is
pending. The full executable-section direct-branch superset scan and exception
function entries contain no interior entry into `[41D60,41D6F)`. The sole direct
caller is `86A68`; audited switch data does not target the prologue. Inspect the
whole function and its indirect/switch targets again on any native update.

Resolution intentionally remains hash-bound: the common save prologue alone is
not a safe unique semantic fallback for the cursor-producer/render/clear timing
contract or the existing cursor field layout. Validate pristine entry bytes and
exact live span before enabling; an overlap/layout failure disables local formation
input while the independent admitted-group/default-slot path remains available.
The observer and its demand byte remain rooted until process exit; resets only
clear logical demand. No extra per-unit or per-visible-tile callback is added.

Own-unit repair is bound to ground call-site `8F325`, ordinary mode 1, the native
nonzero R15D permission, stable own living-unit identity and an ordinary positive
same-PCL witness. Stationary native types from `1811A0` (Trebuchet, Mangonel,
Ballista) and Assassins do not provide the shortcut. Digger capability alone does
not block an ordinary same-PCL witness; actual moat/structure starts retain their
special checks. Current
packed tile and native next movement start must both be ordinary and in the target
PCL. Attacks and special paths keep their existing exact providers. No new full
cursor path-probe loop is entered for friendly hovers.

`118E00` has no selector; ordinary mixed groups without a prepared formation collect
one bounded set of default slots, using the audited `11BB33..11BB45 -> E1D30`
spacing 1. Capture is shared with Vanilla formations, at most 4001 inspected nodes,
with reused visited/queue buffers and finally cleanup. True slot shortage repeats
existing cells deterministically; no valid slot rejects before queue/member writes.
Destinations are consumed through the existing UnitMoveHere Pre event after the
unchanged native `118F50` admission gate. No extra full path probes or move calls.

Preview admission captures once per gesture under the existing validated private
engine lock (nonblocking from UI, otherwise deferred into the native run). It never
activates a native mask or constrains later authoritative admission. Internal formation
protocol is 8; packet shape, public APIs, SDK and mod version are unchanged.

Evidence: `.work/a/island-targets/001/native-audit.json`, production friendly/moat,
formation, Shift, Assassin and gate regression suites; `GroundFrameMachineTests`
executes actual entry bytes/production demand guard with six arguments and stack
checks. Game latency, actual island replay and matching-build host/client acceptance
remain separate from these offline proofs.


### Formation anchored cancellation and survivors (2026-10-26)

Reference native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
No native hook or public API is added. Protocol 10 preserves the 16-field wire
layout and permits one surviving member of a gesture begun with multiple units.
Current full selection count/hash remain mandatory at synchronized execution.

Ground rejection ownership requires coherent fresh anchor feedback, mode1,
kind5, fileAC, image41, command17/detail-10 and no hovered unit/building/wall.
These are the terminal 8F3DA..8F45E ordinary-ground result and cursor kind5;
other reject sprites, mode5 attacks and interaction details1/3 do not qualify.
An owned two-tile drag consumes its rejected release without staging any move
or voice. A confirmed Move keeps its press anchor. Vanilla/unclassified clicks
retain the native path. Once claimed, dispatch/UI errors cannot restore the edge.

Survivor refresh runs under EngineInterface.threadLock then stateSync, using the
existing LocalSelectionAPI completed snapshot and UnitAccess.IsReallyAlive.
The native WORD death marker at GameUnit+29C covers melee as well as projectiles;
AliveState alone does not prove life during death animation. Per-gesture identity
maps and marks are reused; the unchanged refresh allocates no buffers. Dead entries
may remain in the completed selection; filter them from current live metadata.
Reused selected IDs, live deselections, ownership/type changes and new IDs never
inherit the original proof. Proven survivors retain shape/facing/row intent.
Removed original slots or a death-related group change use Vanilla placement at
the same anchor, density1, no role placement. Empty survivors consume without move.

Cursor count may lag confirmed deaths until the next native export; only an
engine-validated survivor rebind permits counts between surviving and original
count, and the current/original group. Cursor output never authorizes a new ID.
UI preview removes dead members without repeating regional classification.

Unavailable selection defers only an already-owned release: run the original once
with neutral input, restore its exact snapshot only if the same gesture still
owns it, then retry after simulation progress. Abort clears that old command edge
without clearing a new down/opposite button. A native-only deferred release also
freezes held controls. Released/deferred previews cannot republish markers.
The original input contract is documented in MOVE_COMMAND_RELEASE.md. Two-thread
speech, actual gesture/packet/formatter/dispatcher/slot/unit-state fixtures and
native mask/entry machines remain separate from actual editor/host-client acceptance.


### Native weighted Assassin search (2026-10-30, `native/AssassinSearch.cpp`, `src/Assassins/AssassinNativeSearch.cs`)

`BfqolAssassinSearch.dll` (contract `0x01000002`) runs the bucket variant of `TryBuildWeightedRoute` with identical
results. It adds no RVA of its own: it reads exactly the tables the managed search binds in `InitializeNative`
(valid coordinates, row lookup, tile flags, building, height, occupancy and direction masks) and calls the special
tile predicate `0x107160` with context `0x32DE440` through the same addresses. Gate policy masks come from the
published `RouteTilePolicySnapshot` bytes; masked edges, other policy shapes and direct gatehouse endpoints call back
into the managed predicates. On a new DLL nothing changes here beyond the managed search's own RVA checks; the
offline comparison (`tests/AssassinPathfinding.Tests`, `VerifyNative`) must stay identical on every fixture terrain.
Without the DLL, or on a contract mismatch, a warning is logged and the managed search runs.
`VerifyNative` also compares the production policy shape (byte mask read by the kernel, tiles beyond
the mask allowed, masked climb callbacks answering true at a live gate) and a throwing managed callback: callbacks never
let an exception unwind through the kernel; a failed callback discards the native call, the managed search repeats it
and fails at the same predicate, and the following searches still agree. `GateRouting.Tests` checks that
`DirectionMask` and `IsDirectionAllowed` of every captured gate snapshot decide identically.

#### Group fields of Assassin group moves (2026-11-03, contract `0x01000002`)

`bfq_assassin_field_create`/`bfq_assassin_field_route` answer the members of one Assassin move command with one
backward Dijkstra from the move target over exactly `Search::Run`'s edges and costs (`Search::Edge`): each member
follows the exact descent along it until the box around target and slot (margin 8), then `Run`'s A* to its own slot.
Near-optimal like MoatMove's anchor routes (user decision 2026-11-03), never cheaper than the per-unit optimum
(`VerifyNative`); the per-unit `Run` is unchanged. Reference SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Managed contract (`TryStageGroupRoute`,
`UnitCommandPathRuntime.TryPublishAssassinGroupRoute`):
- Fields are built only inside a move command scope (`TribeIssueOrderMoveHere`, target tile = anchor), for the path manager singleton
  `0x60AD660` with `+0x88 != 0`, `+0x84 == 0` and `+0x80 == 0` (F4930's Assassin branch, audit `ASSASSIN_RECONSTRUCTION_REPAIR.md`),
  request start/target from `+0x08..+0x14`, and a qualified single-unit frame (`AssassinPathAPI.TryGetCurrentWeightedRequest`).
- The staged route is published through the frame without calling F4930 (no `0xD9C40`, `0xE1640`, second `+0x94`
  pass), the precedent of MoatMove's single routes. The frame's `+0x94` identity test is skipped while Vanilla's
  builder did not run, because `+0x94` then holds the value of an earlier reconstruction (`E1110`/`E1640` are its only writers).
- `Complete` follows `Stage` directly, with no game code in between: the group route's validator validates in full at
  `Stage` and answers `Complete`'s repeat once with that result (`CachedRoute.GetGroupRouteValidator`; the unit identity is
  still rechecked). Any other outcome, including an exception after staging, replaces the frame, so no group route
  reaches the builder path.
- Every other request, a rejected or unpublished route, keeps the unchanged per-unit path (Vanilla builder + weighted search).
- Recent fields (as many as the native pool keeps) are reused for 400 map ticks by later move commands to the same
  target with the same profile key, and by single requests outside a command scope whose target is a slot such a field
  answered (post-combat resumption, `AssassinCombatResumeRuntime`). A single request never builds a field.
On a new DLL: re-audit F4930's Assassin branch, the writers of `+0x94` and the frame publication fields before enabling it.


### One-pass flood member check (2026-10-30, `src/UnitCommands/Native/TribeFloodMembership.cs`)

Feature owner: BugfixesAndQoL's tribe flood hook (`AllowTribeFloodFillForMoveOrder`). Reference SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Instead of calling Vanilla's `0x124740`, the hook
computes its result in one pass (audit: `MANUAL_GROUP_ADMISSION.md` "Admission flood and member check 0x124740").

| Entry | Reference RVA | Binding |
|---|---|---|
| Member check | `0x124740` | Resolved RVA must equal the reference; body `0xAC` bytes, SHA-256 `FFB294BD...C9817C`. |
| Group member lookup | `0x119F90` | Resolved RVA must equal the reference; body `0x8C` bytes, SHA-256 `CD5DAE0B...BD1544F`. |
| Data | tribe stride `0x688` (`+0x40`, `+0x5C`, bitset `+0x60`, 625 words); unit stride `0x490` from `0x67E8400` (`+0x6E4`, `+0x8F8`, `+0x72C`); stamps `0x52C2550` | Read exactly as the two bodies read them. |

No pattern fallback: the replica is bound to the two audited bodies. A different RVA or body leaves
`exactFloodMembership` false and the hook calls Vanilla's original as before. On a new DLL repeat the audit of both
bodies and of `0x11B520`'s retry loop, update the hashes, and run the replica test in MoatMove's harness
(`FloodMembershipExactTests`) plus a DEVDIAG run (`uc.flood.exactMismatch` must stay absent).


### Shared command runtime: address inventory and recovery contract (moved from MoatMove, 2026-10-30)

Feature owner: BugfixesAndQoL's shared command runtime (`src/UnitCommands`). MoatMove registers only as its
traversal provider and owns none of these entries. Reference SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Until 2026-10-30 this inventory was kept in
MoatMove's `UpdateToNewDLL.md` with references to MoatMove's former copy of the runtime; the source locations below
are the current ones (`NativeDetourContracts.cs` remains the function-prefix catalog, see "Command engine ownership").

#### Resolution, signatures and failure contracts

- Named function targets use their `*Pattern` constant and `UnitCommandPathRuntime.Resolve`. On the reference hash the
  bytes at the reference RVA are used first; otherwise `Shared.NativePatternResolver` searches executable PE sections
  for one match. `Resolve` additionally requires the resolved RVA to equal the reference.
- The group movement helper at `0x117C70` has a three-match fallback pattern; a fallback attempt fails closed.
- Observer entries use inline exact byte strings plus an executable-section uniqueness check; a changed entry fails
  before installation. Reconstruction at `0xE32B0` validates all 56 bytes including its call to `0xE1640`; its detour
  target is the function entry, not that call site.
- Call-site entries validate complete five-byte `E8` instructions and signed relative targets. Cursor gate and consumer
  entries are validated read contexts, not global patches.
- Fixed data/table RVAs, manager prefixes and unit/tribe/moat/path-buffer offsets have no semantic relocation fallback;
  a code pattern does not prove these layouts. Keep the dependent features hash-gated on an update.
- Initialization failure logs an error and rolls back only an unpublished candidate; optional cursor/attack/work groups
  fail independently and can leave ordinary movement installed. Inspect every group in a test.

#### Pre-builder recovery inline hook (`src/UnitCommands/Movement/NativeMovementRecovery.cs`)

The only inline hook of this group is `[0x19664B, 0x196659)`, exactly 14 bytes: `33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF`
(`xor eax,eax; mov edx,esi; mov [RIP+...],rax; mov rcx,r15`). RedBird's installed implementation decodes at least
max(requested length, 14) whole bytes; the decode-only installed-assembly test confirms `DisplacedByteCount = 14`.

RSP is aligned at the site. RSI/RDI/RBP/R12-R15 stay live and ABI-preserved; volatile GPRs and flags are dead on both
audited continuations, and the containing function has no XMM operands. The adapter reserves 0x30 bytes, reads the
original start coordinates at the adjusted RSP+0xA0/+0xA8 before writing the argument slots at RSP+0x20/+0x28, and
passes manager R15 and the 1-based unit ID ESI. A positive callback branches to the original buffer initialization at
`0x196585`; otherwise all displaced instructions are replayed and RedBird returns to `0x196659`. The emitted 73-byte
body is decoded and checked in MoatMove's harness ("actual recovery emitter assembled and decoded"), including its
RIP-relative store. Local branches and the hash-matched baseline cross-references show no entry into the span's
interior. Recheck instruction, branch and ABI contracts on any native or RedBird update.

#### Address inventory

Names follow the source constants; they are not an independent proof of native parameter semantics.

| Source symbol / derivation | Reference RVA | Current source |
| --- | --- | --- |
| CentralMovementPlanRva | `0x18E1E0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:35 |
| TribeFloodFillMembershipRva | `0x124740` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:36 |
| FirstGroupUnitOnCompletedMoatRva | `0x117BC0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:37 |
| GetGroupUnitIdRva | `0x119F90` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:40, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:38 |
| GetTribeMovementModeRva | `0x117C70` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:39 |
| OrdinaryMovementGroupModeCallRva | `0x11B736` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:40 |
| GroupMoatModeCallRva | `0x11B666` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:41 |
| UnitStandingOnCompletedMoatRva | `0x196840` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:42 |
| RegionReachabilityRva | `0xE7C40` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:43 |
| CursorReachabilityRva | `0xE9FF0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:44 |
| CursorTilePairFallbackSelectionRva | `0x196870` | Entry owned by the Script Extender; its six call sites are adapted in BugfixesAndQoL/src/UnitCommands/Integration/AssassinSelectionAdapters.cs (see "preserve native ground cursor permission") |
| SelectionCanDigMoatRva | `0x191C00` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:45 |
| SelectionCanDigMoatCallRva | `0x8D3CE` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:46 |
| CursorTilePairReachabilityRva | `0xE2CA0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:47 |
| GetRepresentativeSelectedUnitRva | `0x18D460` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:48 |
| CursorRegionPrecheckRva | `0xE9D90` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:49 |
| PathBuilderRva | `0xF4930` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:50 |
| GroundPathBuilderRva | `0xDA590` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:51 |
| AlternativePathBuilderRva | `0xDB650` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:52 |
| GetMoatIdAtTileRva | `0x69560` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:53 |
| AttackApproachFloodBuilderRva | `0xDBC60` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:54 |
| BuildingApproachBuilderRva | `0xDA020` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:55 |
| BuildingCandidateConsumerRva | `0x123090` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:56 |
| RegionPairReachabilityRva | `0xE2610` | BugfixesAndQoL/src/AI/Prebuilt/AIPreplacedBuildingFixRuntime.cs:89, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:57 |
| AttackApproachFloodCallRva | `0x11EE47` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:58 |
| AttackApproachFloodAlternativeCallRva | `0x11F46B` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:59 |
| BuildingApproachCallRva | `0x11FF9A` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:60 |
| BuildingCandidateConsumerCallRva | `0x11FFA7` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:61 |
| BuildingCandidateConsumerAlternativeCallRva | `0x1206DF` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:62 |
| BuildingCandidateConsumerForceCallRva | `0x120CCD` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:63 |
| AttackFloodRegionPairCallRva | `0xDBF0D` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:64 |
| AttackFloodTilePairCallRva | `0xDBF33` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:65 |
| BuildingApproachRegionPairCallRva | `0xDA1F9` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:66 |
| BuildingApproachTilePairCallRva | `0xDA232` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:67 |
| BuildingApproachAlternativeRegionPairCallRva | `0xDA47C` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:68 |
| BuildingApproachAlternativeTilePairCallRva | `0xDA4B1` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:69 |
| OrdinaryMovementLadderPrecheckCallRva | `0x11B768` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:70 |
| OrdinaryMovementLadderReachabilityCallRva | `0x11B785` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:71 |
| BuildingConsumerFallbackBuilderCallRva | `0x123102` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:72 |
| BuildingConsumerGroundBuilderCallRva | `0x12312C` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:73 |
| BuildingCursorReachabilityRva | `0xB70C0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:74 |
| BuildingCursorReachabilityCallRva | `0x8DFF6` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:75 |
| CombatFinishResumeRva | `0x1853F0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:76 |
| CombatFinishPostCombatCallRva | `0x18540D` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:77 |
| PostCombatRepathRva | `0x1976C0` | BugfixesAndQoL/src/Assassins/AssassinCombatResumeNativeDefinition.cs:27, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:78 |
| PostCombatMoveHereCallRva | `0x19772B` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:79 |
| CursorMoveStagerRva | `0x195E30` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:80 |
| NativeSpecialStructurePredicateRva | `0x107160` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:81 |
| NativeSpecialStructureContextRva | `0x32DE440` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:82 |
| NativeBuildingTypeBiasRva | `0x64CCCDE` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:83 |
| CursorMoveStagerRegionPairCallRva | `0x195F46` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:84 |
| Inline expression / observer / address array | `0x8F7BA` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:86 |
| Inline expression / observer / address array | `0x8FD3C` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:86 |
| Inline expression / observer / address array | `0x8FDC6` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:86 |
| Inline expression / observer / address array | `0x8FE54` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:86 |
| Inline expression / observer / address array | `0xE7F60` | BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:76, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:87 |
| DirectFillApproachRva | `0xE7F60` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:87 |
| DirectFillApproachRegionPairCallRva | `0xE81EB` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:88 |
| DirectFillApproachCommandCallRva | `0x120E9D` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:89 |
| MovementTerrainPhaseRva | `0x19B506` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:90 |
| MovementCadenceRva | `0x184203` | BugfixesAndQoL/src/TroopMovement/SynchronizedMovementCadencePatch.cs:125, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:91 |
| MovementSubstepRva | `0x1855A0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:92 |
| MovementAdditionalSubstepsRva | `0x1857AA` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:93 |
| MoatPathConsumptionReadRva | `0x185934` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:94 |
| MoatPathConsumptionPersistRva | `0x19670C` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:95 |
| CursorCurrentTileFlagGateRva | `0x8F388` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:96 |
| CursorCurrentTileFlagGateJumpRva | `0x8F393` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:97 |
| AttackUnitPairGateJumpRva | `0x8D72B` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:98 |
| AttackBuildingPairGateJumpRva | `0x8E2C6` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:99 |
| AttackAlternativePairGateJumpRva | `0x8E557` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:100 |
| TileFlagsRva | `0x48F71B0` | BugfixesAndQoL/src/Assassins/AssassinPathfindingRuntime.cs:36, BugfixesAndQoL/src/Formations/Markers/LargeMoveTargetMarkerRenderer.cs:35, BugfixesAndQoL/src/TroopMovement/MountedStockpileMovementPatch.cs:59 |
| MovementTargetAvailabilityRva | `0x3A11EA4` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:43, BugfixesAndQoL/src/TroopMovement/MountedStockpileMovementPatch.cs:56, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:102 |
| NativeMovementMaskRva | `0x51890D0` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:103 |
| RowLookupRva | `0x402FF2C` | BugfixesAndQoL/src/Assassins/AssassinPathfindingRuntime.cs:35, BugfixesAndQoL/src/TroopMovement/MountedStockpileMovementPatch.cs:58, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:104 |
| NativeBuildingLayerRva | `0x4B6AA50` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:105 |
| NativeHeightLayerRva | `0x4DDD350` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:106 |
| NativeDirectionMaskRva | `0x312620` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:107 |
| CursorTargetXRva | `0x3A11E2C` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:108 |
| CursorTargetYRva | `0x3A11E30` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:109 |
| PathRegionGridRva | `0x50EC690` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:110 |
| MoatPathModeRva | `0x60AD6E4` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:111 |
| NativePathManagerRva | `0x60AD660` | BugfixesAndQoL/src/AI/Economy/AIBuildingTemporaryAccessClassifier.cs:48, BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:41, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:112 |
| NativeUnitManagerRva | `0x67E8400` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:113 |
| NativeTribeManagerRva | `0x7CC6720` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:42, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:114 |
| Inline expression / observer / address array | `0xDB29A` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:117 |
| Inline expression / observer / address array | `0xDB2EC` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:117 |
| Inline expression / observer / address array | `0xDB37A` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:117 |
| Inline expression / observer / address array | `0xE1856` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:119 |
| Inline expression / observer / address array | `0xE18DF` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:119 |
| Inline expression / observer / address array | `0xE195B` | BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs:119 |
| Module-relative expression | `0xE32B0` | BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:70, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.Initialization.cs:461, BugfixesAndQoL/src/UnitCommands/Runtime/UnitCommandPathRuntime.Initialization.cs:462 |
| Module-relative expression | `0x51D75F0` | BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:96 |
| Module-relative expression | `0x9302C4` | BugfixesAndQoL/src/Formations/FormationSpeechNative.cs:18, BugfixesAndQoL/src/GateRouting/DiagnosticCallbackAdapterEmitter.DevDiag.cs:28, BugfixesAndQoL/src/GateRouting/NativeAssassinMovementDiagnostics.DevDiag.cs:77 |
| Inline expression / observer / address array | `0x118E00` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:38, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:101, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:120 |
| Inline expression / observer / address array | `0x181890` | BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:104, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:120, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:120 |
| Inline expression / observer / address array | `0xF03C0` | BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:107, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:120, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:80 |
| FindMoatWorkTargetRva | `0x69D60` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:14 |
| ResolveMoatWorkTileRva | `0x6AF60` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:15 |
| HasFillMoatApproachRva | `0x6C490` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:16 |
| FillApproachCallRva | `0x69EE6` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:17 |
| DigStandingOnMoatCallRva | `0x69F91` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:18 |
| DigRegionSearchCallRva | `0x69FE3` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:19 |
| DigRegionPairCallRva | `0x6A014` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:20 |
| DigAlternativeRegionPairCallRva | `0x6A0C2` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:21 |
| MovementPlannerLowFlagGateRva | `0x196464` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:22 |
| MovementPlannerStructureFlagGateRva | `0x19648D` | BugfixesAndQoL/src/UnitCommands/Moat/UnitCommandPathRuntime.WorkTargetState.cs:23 |
| Inline expression / observer / address array | `0xE1D30` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:36, BugfixesAndQoL/src/UnitCommands/Formation/NativeFormationSlots.cs:41, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:120 |
| Inline expression / observer / address array | `0xE0970` | BugfixesAndQoL/src/Formations/FormationRuntime.State.cs:37, BugfixesAndQoL/src/UnitCommands/Formation/NativeFormationSlots.cs:47, BugfixesAndQoL/src/UnitCommands/Moat/MoatPlacement.cs:120 |
| UnitTypeUpdateDispatchRva | `0x18410C` | BugfixesAndQoL/src/TroopMovement/SynchronizedMovementCadencePatch.cs:124, BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementCadenceResolver.cs:18 |
| Module-relative expression | `0x64CCED2` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:52 |
| Inline expression / observer / address array | `0xD90D0` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:55, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:44 |
| Inline expression / observer / address array | `0x59210` | BugfixesAndQoL/src/MoatTerrain/NativeDefinition.cs:32, BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:61, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:24 |
| Inline expression / observer / address array | `0x61E70` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:73, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:25 |
| Inline expression / observer / address array | `0xDAA50` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:80, BugfixesAndQoL/src/UnitCommands/Movement/TraversalProvider.cs:67, BugfixesAndQoL/src/UnitCommands/Native/NativeDetourContracts.cs:48 |
| failureRva | `0x19664B` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:90 |
| Module-relative expression | `0x196585` | BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:163, BugfixesAndQoL/src/UnitCommands/Movement/NativeMovementRecovery.cs:93 |

#### Preview map-edge handling, 2026-10-31

Reference native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; no new RVA or hook. FootprintMarker.TryApply returns false with a default result when any XY cell is outside the 800-axis buffer or native validity mask. Full-footprint validation still precedes every construction-grid write; packed-row overflow, invalid packed indices and unsupported scales remain contract errors. The callback clears its captured ground snapshot on a skipped footprint without disabling readiness or activation. The next complete footprint is sampled normally, and Vanilla Original still runs exactly once.

Client evidence: CenterOfPower.map, 2026-10-30 14:11:18.354, BUILD_PREVIEW_CALLBACK_ERROR. The user reproduced with apple farm and quarry below their BuildingLimit caps. Do not attribute this reproduction to an exhausted limit. Vanilla's validator (0x78768..0x7879D -> 0x798D1) writes state 2 for invalid footprint coordinates/mask; 0x5DD70 normally omits that state. The Extender's post-validation overrides can replace it, but the concrete mapper/XY/state transition was not logged, so the reason this particular callback passed admission remains unresolved. Geometry is checked independently of that admission state.

Regression: existing BuildPreview.Tests production memory fixture covers all seven supported mappers, first/last invalid mask cells, both coordinate axes including extremes, valid-edge-valid recovery, unchanged complete-placement sampling, and fatal corrupt-row/scale contracts. Existing snapshot tests cover retirement and generation safety. This is offline evidence, not in-game acceptance; retest apple farm and quarry below and at their building limits.


