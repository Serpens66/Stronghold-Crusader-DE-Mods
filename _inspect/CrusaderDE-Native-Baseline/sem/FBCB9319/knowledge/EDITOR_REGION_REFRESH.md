# Editor terrain changes and stale path regions

Observation: 2026-10-26. Native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256 `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
The native function names remain candidate database labels unless otherwise noted;
the described writes/gates are confirmed static machine facts, not GUI acceptance.

## Mutation and consumption

The confirmed export `DLL_MapAction` (0x85BD0) maps editor moat actions 106/107
to internal actions 45/44. Input handler 0x90CD0 chooses width 3, or 5 with Shift;
this is independent of the selectable landscape brush. It queues Chore 21 with
property 0x40000000 and operation 2 (direct completed placement) or 3 (removal).
Handler 0x111D0 calls 0x6FE90 with player, packed tile, Y, footprint, property and
operation. The ordinary landscape dispatcher 0x92FF0 reaches the same handler
for terrain type changes, including water.

0x6FE90 checks accepted cells, writes flags/height and (where applicable) moat
records, then calls the confirmed 3x3 edge updater 0xD90D0 at 0x7094D. Neither
the generic terrain writer nor the 3x3 edge updater marks context+0x6C dirty.
The latter's terminal global write is the separate building/render notification
at 0x67E6424, not the component dirty flag. Terrain clearing 0x7AE50 also updates
edges without directly setting component dirty. Texture-only branches may omit
the 3x3 update. They must not be inferred to be topology-free: clearing previous
terrain can also change passability.

Height preset 0x6FA10, numeric/shape height 0x70DF0, raise/lower 0x6C0F0 and
equalize 0x64700 have separate accepted-cell guards and deferred terrain work.
No blanket assertion that all of these fail is made. Vegetation placement
0x6E2C0 and the vegetation-removal branch of editor deletion 0x62AB0 mark the
component grid dirty. Completed digging 0x639C0, filling 0x69470 and ordinary
building placement 0x6D580 likewise set dirty. This explains why placing a wall
can cause old water/moat topology to become visible to path-region consumers;
the original hover-only explanation was withdrawn by the reproducer.

The real rebuild 0xE49D0 takes `(pathContext*, int force)` and returns int 0/1.
Nonzero force writes dirty=1 and resets the countdown. With force=0 a countdown
and dirty=0 can independently skip work; waiting does not repair an unmarked
edit. A completed flood increments context+0x74, clears dirty and republishes
the UInt16 PCL grid, component counts and gate-related processing. Equal stale
nonzero PCLs immediately succeed in the confirmed query 0xE2610. Do not replace
that valid fast path; repair the publication of the input regions instead.

## Safe consumer boundary and limits

The real public `EditorDirector.preDLLCallActions(ref int, ref int)` is called
exactly once inside `EngineInterface.run(bool)`'s existing private threadLock,
before `DLL_RunTick` and its cursor dispatcher 0x8C5F0. A managed postfix can
consume prior completed edits there, including while the native game clock is
paused. Editor lifecycle must come from APIShared's managed completion service.

The consumer uses existing SE Post events for terrain, height, deletion and
vegetation. Post means the native call returned, not that any cell was accepted;
conservative no-op requests are allowed and coalesced. The event's BrushSize
label is not a reliable semantic guide for the generic terrain delegate: its
second native argument is the player, and the actual footprint is argument five.
The correction deliberately does not interpret that payload.

Live calls through 0xE49D0 preserve the existing command-runtime repair guard.
No second native detour is installed. A changed generation since the latest edit
plus clean dirty marker proves Vanilla completed the pending batch; a clean marker
alone or a rebuild preceding a later unmarked edit does not.

## Release batching (2026-10-26)

The user confirmed the initial editor correction works. The follow-up efficiency
change defers its own dirty request as well as its forced rebuild until Mouse0 is
released. Native E49D0 checks dirty independently from its countdown, so merely
delaying the forced call while still setting dirty would retain avoidable Vanilla
rebuild requests during continuous drawing. Native or foreign dirty writers and
forced calls remain untouched.

Confirmed-static managed input chain: EditorDirector.Update reads physical primary
button down/held/up and produces its separate engine mouse state. Its preparation
method consumes that state, performs held painting and a final release placement;
the native Chore can complete later during DLL_RunTick. Consequently a late real
edit must remain pending even after a release-triggered rebuild.

KeyManager.Update independently advances its own key states and filters them for
Noesis keyboard capture; the SE raises down/held/up events after that method. These
events are not a reliable sole physical release boundary for terrain brushes.
The correction instead samples physical input in a permanent EditorDirector.Update
prefix only while edits are pending, atomically tagged with their batch. New work
starts unknown, held work returns before engine-side map reads, focus loss ends
deferral and focus regain requires resampling. The existing locked pre-cursor
boundary remains the only place where the correction invalidates/rebuilds regions.
No new native hooks or public APIs are required. Runtime cost savings from this
batching still require actual continuous-drawing measurements in a DEVDIAG build.

Tests execute the unchanged canonical 0xE49D0 and 0xE2610 in private image memory
with explicit flag/edge graph inputs and a fixture replacement for memory fill.
This proves flood/gate/query behavior, not execution of every editor tool or GUI
input. Actual water/moat cursor acceptance, continuous drawing and gameplay
latency remain separate in-game checks. See `BugfixesAndQoL/Test-EditorRegions.ps1`.
