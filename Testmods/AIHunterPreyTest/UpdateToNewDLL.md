# Native compatibility: AI Hunter Prey Test

Audit: 2026-11-15, confirmed static evidence; no game or multiplayer acceptance is implied.
Reference installed `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The selected [baseline](../../_inspect/CrusaderDE-Native-Baseline/CURRENT.md) and its
`CURRENT.json` select the same hash. RVAs below are relative to the loaded native module.

## Feature and ownership

### Active AIV reservation, 2026-11-19

Automatic hunter placement now snapshots the owning village's loaded plan through public `GameAIVManagerAPI` pointer/span accessors. It never reads/decode-writes `.aivjson` or the shared working rasters. `0x55320` imports, `0x56670` rotates, `0x53D00` materializes the map positions, `0x539B0` schedules and `0x51790` executes them. `MaximumBuildStep` is inclusive; reserve all nonempty records through it, independent of unlocked/progress state, including rebuilt/abandoned sites. Only Usable/Perfect selection and cardinal rotations are admitted. Missing/malformed plans stop that automatic build, not AIV construction or retirement.

Single anchors already denote the minimum world corner. Ordinary square scales are from installed SE `BuildingScales`, independently compared against all 440 enum entries in native `0x6A190` (selector `0x6A250`, RVA table `0x6A214`). Known special commands such as stairs clamp to one tile; unknown mapper values stop capture. Grouped walls/crenels/woodwalls/pitch/moat use absolute individual tiles, each 1x1 (moat's ordinary scale 2 does not apply). Ordered buffers are read through the relocation-aware SE API, so Fixes-expanded buffers remain supported. Full payload bounds are checked before conversion to X/Y; packed IDs must fit the public path-component grid capacity.

`0x51790` and `0x53D00` consume extra-component tables at `0x2D2E90`/`0x2D2EF0`; `0x72E10` independently confirms actual four-record barracks placement. Relative to the base 5x5 minimum corner, stockade/wooden barracks/stone barracks have three extra 5x5 squares:

| Rotation | First | Second | Third |
|---|---|---|---|
| South | (5,0) | (0,5) | (5,5) |
| East | (0,-5) | (5,0) | (5,-5) |
| North | (-5,0) | (0,-5) | (-5,-5) |
| West | (0,5) | (-5,0) | (-5,5) |

Engineers'/tunnelers' guilds use only the second extra. Oil smelters add a 4x4 square at (0,4)/(4,0)/(0,-4)/(-4,0) for South/East/North/West. These are copied semantic geometry contracts, not new native calls. Automatic construction requires the same audited native file identity already computed by the retirement adapter, separately from retirement hook/site readiness; no second hash pass. Unknown hashes disable this geometry-dependent automatic build without a pattern fallback, because matching one function would not establish all layouts/tables. No new live probes or hooks.

The transient mask is clipped to the 67x67 search window (radius32 plus hut width3), rebuilt once per eligible construction search. Candidate checks then read at most nine mask cells. No new tick subscription, per-frame scan, disk decode or cross-session cache. Update the public record/selection/coordinate/buffer contracts and all geometry tables together after another native/SDK revision; the pure reservation tests cover ordinary overlap, boundaries, all rotations and compound components. In-game active-plan capture remains separately testable.

2026-11-19 policy addition: chicken-only food permits one baseline hut; other prey permits additional AIV huts. No native call or layout changed. Renewal uses IH's captured positive `ChickensPerPlayer`, active chicken hunting, its registered public `ChickenPopulationModifierHandle.IsEnabled`, own-player catalog admission and an alive own bakery/mill/granary, outside the editor. The existing hash-bound APIShared chicken-population contract (`APIShared/docs/CHICKENS.md`, `UpdateToNewDLL.md` chicken-population audit) supplies this potential-spawn criterion. This does not prove immediate spawn success or override other population providers. Missing/unavailable IH integration never invents a supply. Existing meat-drain checks still apply to every removed hut.

`src/HunterRetirementNative.cs` reproduces the three calls in Vanilla's existing AI
retirement branch. It installs no hook, writes no executable bytes and never forges
the native failure counter to enter that branch. Its caller must hold the ordinary
simulation boundary and independently establish absence of eligible prey.

The public Script Extender deletion helper only marks a building; it does not provide
the full avoidance/refund/removal operation needed here. The three live function
entries are registered with APIShared `NativeSiteMode.CallOnly`, length 1, under
`AIHunterPreyTest_Serp/HunterRetirementCalls`. In particular the live refund entry is
called so the Script Extender's existing refund event and other entry hooks remain
in the call path. No original-bytes requirement is applied to those live entries.

Build admission uses the Script Extender's placement-validation Post event. Automatic
construction uses public ordinary `CreatePrefab` and spawn observation, with an
explicit affordability check. Neither feature owns a private native interception
site. Their public API contracts must still be rechecked after an Extender update.

## Image and function validation

At initialization, `GetModuleFileNameW(context.ModuleHandle)` identifies the actual
loaded file; SHA-256 is computed once for this retirement backend. This is a file
hash, not a hash of the much larger mapped image. All instruction/function checks
use `CrusaderLibraryLoadContext.Memory`, the original load snapshot. The snapshot
is owned by the Extender and is never disposed by this consumer.

The known reference image uses these complete function byte hashes. Lengths are
decimal; the audited ranges contain no PE base relocations. Each code target must
fit wholly within an executable PE section as well as within the snapshot.

| Purpose | RVA | Length | SHA-256 of original function bytes |
|---|---:|---:|---|
| Audited AI branch and its anchors | `0x3B270` | 229 | `EFF1F3C1FB0BB922746F17F266D62EAA252E6FF7C64610EF50856EA2833AAD9D` |
| Record coarse-cell avoidance | `0x55E10` | 109 | `84F5ED0A91B52B6FD62CB4B56F35A5EB320BAB9B4A1F18E9603B89C2208DB828` |
| Ordinary building refund | `0xC4480` | 1320 | `C1E4DC936740B9D35117AC1539BC0A07D48EF212F61CE57778DC2A82AA8445D7` |
| Mark building and demolition group | `0xC4290` | 257 | `713BF8FC70A1320F9EA7BB8B1CEBC82F0E52211C78A41D1DA3286EA5E24B0E56` |

There is no semantic pattern fallback for an unknown file hash. Finding these three
functions alone would not prove the hunter state machine, worker identity fields,
deferred meat yield, manager headers, demolition groups or update ordering. Those
contracts currently have no independent runtime derivation. A full feature audit
is required before admitting another image. The known hash directly pins the
reference addresses; no image scan or persistent address cache is needed. Existing
live hooks do not obstruct reference validation because the unmodified snapshot
is used. Any initialization mismatch disables retirement; independent public-API
placement/build behavior retains its own availability checks.

## Derived instructions, data roots and native arguments

After the complete `0x3B270` function hash matches, the following complete original
instructions supply targets. A relative target is `instructionRva + length + disp32`.
All derived targets must lie in the image; function targets additionally pass the
whole-function hash and executable-section checks above.

| Instruction RVA | Original bytes | Length / displacement offset | Derived reference |
|---|---|---|---|
| `0x3B27F` | `48 8D 35 2A 19 49 06` | 7 / 3 | building manager `0x64CCBB0` |
| `0x3B30E` | `48 8D 0D 5B EC 46 03` | 7 / 3 | AIV state `0x34A9F70` |
| `0x3B31E` | `E8 ED AA 01 00` | 5 / 1 | `0x55E10` |
| `0x3B331` | `E8 4A 91 08 00` | 5 / 1 | `0xC4480` |
| `0x3B33B` | `E8 50 8F 08 00` | 5 / 1 | `0xC4290` |

Both reference data roots lie in `.data` (`0xC0000040`, readable/writable);
the resolver also checks these section flags and boundaries before constructing pointers.
Before every retirement the derived building-manager pointer must equal the public
`GameBuildingManagerAPI.GetBuildingManager().Pointer`. Native arguments, in order:

1. `0x55E10(AIV-state pointer, tileX, tileY)`; coordinates are the building's origin
   tile words widened to Int32. It records temporary coarse-cell avoidance, not an
   AIV step-status reset. Its cell is `x/5 * 160 + y/5`, record stride `0x30`.
   Removal-count byte is root + cell*stride + `0x5B847`, delay byte `+0x5B843`.
   If old count < 10 it increments count and writes `3*oldCount+10`; otherwise
   delay becomes 10. Grid refresh, rather than each tick, ages this delay.
2. `0xC4480(building-manager pointer, one-based buildingId, ownerPlayerId, 50)`.
   Normal mode refunds native construction costs using integer percentage division.
   Native mode 1 gives no refund; mode 6 overrides the argument to 100. Existing
   refund hooks may also modify the result. Thus 50 is the Vanilla AI argument,
   not an unconditional promise of exactly half in every game mode/mod combination.
3. `0xC4290(building-manager pointer, one-based buildingId)`. It marks alive state
   3 and the normal removal flag; a nonzero group at public building `+0x2A8`
   propagates removal to other members. This consumer rejects nonzero groups.

The original `0x3B270` branch also tests a failure counter and owner/control flags;
the consumer does not change those fields. Instead it independently validates a
live AI-owned hunter hut with the expected nonzero global ID and an empty group.
The live ID/owner/type/global identity and drain predicate are checked again after
refund subscribers run. A partial failure permanently disables further retirement
calls in this backend to prevent a second refund. Successful marking requires the
same global ID and alive state 3. This is a removal request, not proof of terminal
slot cleanup: normal `0xC60F0` dispatches it to `0xB8310`, which eventually clears
the record and releases linked workers.

## Record and drain contracts

Public `GameBuilding` has size/stride `0x32C`; the one-based slot's public pointer
is manager + id*`0x32C` + `0x5C`. Public `GameUnit` has size/stride `0x490`; its
pointer is unit manager + id*`0x490` + `0x65C`. Public spans start at game ID 1,
so their array index is `id-1`. The backend validates these public structure sizes
and all directly consumed field offsets with `Marshal` before enabling retirement.

| Public record | Offset | Field / required meaning |
|---|---:|---|
| Building | `0x26`, `0x2C` | `r_ProductionCycleMode`, `r_ProductionProgress`; both must be zero |
| Building | `0xD0`, `0xD2`, `0xD6`, `0xD8` | alive state, type, owner, global ID |
| Building | `0xEE`, `0xF0` | origin tile X/Y |
| Building | `0x118` | `N00001758`, production animation stage; must be zero |
| Building | `0x150` | `r_MeatAmount`; must be zero |
| Building | `0x19E`, `0x1A8` | first worker ID and global ID (`N0000176A`); this is the Hunter |
| Building | `0x2A8` | `N0000178A`, demolition group; must be zero |
| Unit | `0x88`, `0x8A`, `0x94` | alive state, type, global ID |
| Unit | `0x2BC` | `r_AIState`; allow only idle states 0, 7, 8 |
| Unit | `0x364` | linked production-building global ID |
| Unit | `0x384` | actual carried goods; must be zero |

Hunter-hut update `0xAE570` verifies its first worker with the pair `+0x19E/+0x1A8`.
The second pair `+0x1A0/+0x1AC` is the hound (spawned type `0x34`). A hound's carry
field has a separate animation meaning; it must not block an otherwise empty hut.
The first worker, if assigned, must be an alive Hunter with matching worker/global
and linked hut/global identities. A reused or ambiguous worker slot waits. Every
other extant Hunter linked by the same hut global ID is checked too, so an orphaned
or additional delivery cannot be hidden by an empty first slot.

Hunter update `0x12FC70` must be audited in full when updating this predicate:
states 1/9/10/11 hunt or approach a corpse; state 2 carries the carcass home;
states 3/109 process it. State 0 with hut meat enters state 4 and removes six
native meat from the hut via `0xB6100`, good `0xC`, amount -6. Only a successful
granary route in state 4 changes to state 5 and calls `0x18D940(hunterId,6,1)`
at `0x1309FE` to write the actual carried amount. Therefore state 4 can own meat
while both visible amounts are zero. States 4 and 5 always wait, even for an IH
configured yield of zero. Delivery uses `0xBF9F0`, `0xC90E0`, `0xC9080`, `0xBD470`
and `0xB6100`; state 6 returns and is conservatively excluded as well. State 110,
death states and every unknown state wait.

`0xB6240(...,2)` clears progress and stage but briefly sets cycle mode to 2;
`0xAE570` recomputes the mode on later building passes. The strict zero predicate
may defer retirement and never forces these fields to zero. Unit `+0x300` is a
fractional productivity remainder, not a meat inventory, and is not tested.
APIShared/IH can change the base at the state-4-to-5 yield call; that does not
change the protection of deferred/actual cargo. A blocked delivery or inconsistent
identity can postpone retirement indefinitely; there is no timeout that discards meat.

## Scheduling and update procedure

The current public `SimulationTickEvents` source uses the Extender's synchronous
time callback. The installed signature `FF 87 ?? ?? ?? ?? 33 DB` uniquely matches
`0xD1D4F` within `0xD1AD0` (range `[0xD1AD0,0xD1E4F)`). Its sole native caller
`0xCDE60` increments map tick then calls `0xD1AD0`, before normal building update
`0xC60F0`, AI/AIV update `0x57330`, and unit passes `0x182B00`/`0x187B30`.
Earlier helpers within `0xD1AD0` finish before the callback. No HunterUpdate is
suspended there. The consumer's rare retirement/build operations run sequentially
within that boundary; marked huts go through normal cleanup before later unit work.

For another native image or Extender implementation:

1. Verify installed DLL identity and selected baseline provenance. Re-audit the
   whole retirement branch, avoidance/refund/removal callees and terminal cleanup.
2. Recover instruction boundaries, executable/data sections, RIP anchors, full
   function ranges/hashes and relocation effects; never transfer the old RVAs alone.
3. Re-establish record headers, strides, every consumed field and first-worker
   global identity from independent writers/readers, then update layout checks.
4. Follow all Hunter state transitions, hut production and delivery failures,
   including current APIShared/IH yield changes and save-loaded worker identities.
5. Recheck the public tick publisher's actual native location and native call order;
   avoid triggering construction/removal inside a suspended producer update.
6. Recheck existing SE/Fixes/APIShared ownership and live-entry behavior. Keep the
   CallOnly contract; do not add competing entry hooks or bypass refund events.
7. Run relevant drain-policy and native-resolution checks. Actual map/save/editor
   and multiplayer behavior still needs separate acceptance; never infer it from
   a successful build or the initialization marker.
