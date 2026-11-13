# Movement search and physical surface profiles [Scope: Vanilla + mod integration]

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Verified against the installed canonical DLL on 2026-11-13 before analysis.

## Vanilla search and movement contracts

The function names in the semantic database are partly candidate-level. The machine
branches and predicates below were independently checked in the installed bytes;
the original functions were also executed in an isolated, privately mapped image.
No game DLL, external component or historical baseline export was modified.

- `0x196280` reads the live Int32 profile at `0x322540 + unitType*4` and passes
  it as the third argument to `0xF4930`. Its output is the unit-owned 1000-byte
  packed route slot, low nibble first; the unit's path state/length is published
  only after the builder returns a positive length.
- The other `F4930` caller, `0x18E1E0`, is a read-only probe with a stack-owned
  output buffer. It does not publish unit movement. `0xE32B0` reconstruction is
  called only by `196280`. The final mod guard therefore binds a live unit by
  its exact native output-slot address and leaves foreign/probe buffers alone.
- In ground mode, `0xF4930` selects `0xF32B0` for profile zero (`Large`) and
  `0xF3060` otherwise. The branch at `0xF4A09` is `test esi,esi; je F32B0`.
  Only the Large direct attempt applies the surface predicate below.
- After failed direct movement, `0xDAAC0` and `0xDA590` search the ordinary
  direction graph. `0xE1640` reconstructs and counts unsuitable surfaces in
  path-manager `+0x94`. For profile zero and a nonzero count, `0xF4930` calls
  `0xDB650` to seek a surface-compatible alternative, then reconstructs it.
  A failed `DB650` deliberately falls back to `DA590`; Vanilla can therefore
  publish a physically unsuitable route when no compatible alternative exists.
- Large's entry predicate B is: no `0x10000100` wall/elevated bits, OR flag
  `0x2` (Goodsyard), OR a linked building whose type's Int32 entry at
  `0x2E68D0 + buildingType*4` is nonzero. It tests entered tiles, not the start.
- `0x1855A0` consumes packed directions and calls `0xDCE60` for transitions,
  then `0xDCD60` for the type-dependent surface validation. A rejection enters
  normal wait/replan handling, which may repeatedly request the same bad route.
- `0xDCD60` restricts Knight, Ladderman, Catapult, Trebuchet, Siege Tower,
  Battering Ram, Portable Shield, Arab Horseman, Arab Ballista, Camel Lancer
  and Heavy Camel. Its entry predicate R is: no wall/elevated bits, OR
  `(flags & 0x102) == 0x102` with building ID zero, OR a whitelisted building.
  R is a subset of B. The narrower Goodsyard exception must not be merged away.
- Building IDs are one-based; the tile's building layer is `0x4B6AA50`.
  The signed type word is at `0x64CCCDE + buildingId*0x32C`.
  Independent whitelist readers: `0xF34E9` and `0xDCDFE`; the type reader at
  `0xDCDF5` pins the same manager basis. These data contracts remain hash-bound.

## Independent counterexample

A synthetic bidirectional grid contains a four-step corridor through an ordinary
wall tile and an eight-step ground detour. Executing the original `F4930` returns
four steps for Default and eight for Large. Original `DCD60` rejects the Knight's
wall entry, accepts a wall/Goodsyard tile without a building, and rejects that tile
with a non-whitelisted building. The existing PathfindingAnchor contract
`0x0400000F`, DLL SHA-256
`69393e8ebbd6b99947a298b81c5083f4d4d3216ac524783ce74be2498f9203f5`,
chooses the four-step route. Filtering its entering edges yields the eight-step
route. This is an isolated execution proof, not an in-game movement observation.

## Profile-aware route integration [mod integration]

Pathfinding resolves three internal classes: Any, LargeSearch(B), and
RestrictedMovement(R). A physically restricted type without SE's independent
next-tile bypass uses R; otherwise Large uses B and Default uses Any. It reads
live profiles and bypass settings rather than changing global type permissions.

Native contract `0x04000010` adds a shared byte per node (valid bit0x80,
B bit0x01, R bit0x02). All searches filter entering edges through the selected
class. Tasks and field caches retain class and its revision. A forbidden start
can still leave for permitted ground. Mixed-group terrain/cost tasks are split
by class, without duplicating complete player masks.
Qualified managed routes and their audit memos also retain the class's revision;
the reuse check remains active when friendly-moat traversal is disabled.

Managed live audits and the final bound unit-output guard recheck entered tiles,
including successful Vanilla and exception-fallback results. An unsafe output is
cleared and returned as failure; the native caller performs normal failure-state
handling. This closes the deliberately permissive native fallback without adding
movement rights. Existing saved routes are not scanned or repaired.

The production C++ regression suite covers directed random graphs, A*, fields,
subanchors, batches, class-separated caches, metadata revisions and worker-count
determinism. Managed engine tests cover profile/bypass resolution, publication
barriers, mixed groups and hazard probes. Live acceptance and performance results
are recorded separately in `.work/a/pf-surface/001`; do not infer gameplay success
from the isolated tests.

### CPU-neutral counted workspace budget [mod integration]

Private native ABI `0x04000011` retains the surface interface introduced in
`0x04000010` and changes internal memory admission. One caller scratch and eight
logical batch banks receive fixed private quotas; their sum is reserved from the
store budget. The store includes player masks, shared surface metadata and stored
fields. A root allocator counts the actual `Vec`/`Alloc` allocations across all
partitions and enforces the existing total. At 768 MiB this is nine 32 MiB workspace
quotas plus 480 MiB store capacity. Previously uncounted bounded `std::vector`
containers remain uncounted; this is a core-buffer limit, not a process-RAM limit.

Each bank is constructed during context initialization. Its `Scratch.local`
replaces the separate main-field workspace, avoiding duplicate worker grids.
Fixed eight-job batches bind input index to logical bank and retain the same
allocation history on 1/2/4/8 physical workers. Worker arguments 1..64 remain
accepted, with execution clamped to eight. Main-field store admission and batch
error reduction run in input order. A 64 MiB/800-by-800 context uniformly returns
`NoMemory` and null because the minimum nine partitions cannot fit. Other quota
failures retain ordinary failure/fallback semantics; no extra gameplay permission
is granted.

Search ring cleanup runs on exceptional exits as well as success. A subanchor is
added to its task only after its complete stored field exists, and a failed mask
replacement preserves the previous ready mask. On 2026-11-13 the regular native
build passed 238,555 production assertions, including allocation faults/reentry,
1/2/4/8 workers with different warmup histories and tight-budget field publication.
The 288-task/768 MiB pressure case produced the same 192 ready and 96 failed tasks
and identical route bytes for every worker variant; counted peak was 598,563,448
bytes. See `.work/a/pf-surface/001/native-abi11-tests.log`. These resource tests
address internal counted-budget determinism, not actual multiplayer acceptance or
arbitrary operating-system allocation failures.
