# AIV tower and gate construction range

## Follow-up: copied-entry probe allocation failure

The 2026-10-05 18:51 startup logged a BugfixesAndQoL probe failure at heap address
0x2D00FCCD100: RedBird could not allocate a 65536-byte slab in its nearby window.
The actual EEF90 detour was never attempted. The fix places the full scan buffer
near the real target with NativeMemoryManager.AllocateStub and initializes it
through WriteStub. The process-wide slab is not freed; only the never-published
probe hook is rolled back. Both probe and live hook now allow only Indirect.
No change to the audited native range predicate, ABI, full function hash or
Indirect/10 contract. Installed SHCDESE hash remains DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF;
RedBird.Core hash EE4B036C486077F7D8898AE054F124EDDE73E6AE1A059E8991C361E2B720D29F.
The allocator's near-target behavior and public write API were read from the
installed assembly. Corrected gameplay acceptance is pending.

## Follow-up: zero mapper records in imported AIVs

High confidence for the same native hash: current managed AIVLoader.SaveData.GetRawData
encodes one-position type-zero frames as [0,offset]. Import 0x55320 tests signed
type < 0 for the counted branch, so zero consumes one offset. 0x53CA0 transforms
that offset; 0x6A190's -1 result is clamped to footprint one before the zero mapper
and frame index overwrite the raster. Rotation 0x56670 preserves the cell value;
0x57080 excludes zero cells from fit checks, and 0x53D00 marks them processed
without emitting a build record. This is a cell clear, not a positionless no-op.
The existing prepared/prebuild execution chain therefore needs no new hook.
Empty legacy [0,0] remains byte-compatible and clears cell zero. Multiple
positions for JSON type zero have no valid counted native representation.
The 18:51 log's snake2, snake7 and snake8-elite Goodwins Workshop warnings were
caused by our encoder/parser rejection and decoder's erroneous <=0 count branch.
See CastlePlanner/UpdateToNewDLL.md for the coordinated repair and regressions.

Audit date: 2026-10-05. Canonical installed native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
All addresses below are RVAs for this binary (image base `0x180000000`).
Installed SHCDESE SHA-256: `DE5B88749C18A257E5F6A6E246F685300BF8DF970969DF1E6EA35C8C95F2A4AF`.
Local Extender commit `f8d51730fcb54b25af43d3c9348d57db058e077f`, tree
`657af449e1397c58e6c5ec054977d83198b68e66`, match baseline provenance.

## Audited flow and confidence

High confidence for the range contract: current PE dispatch-table reads, direct
Iced disassembly of the installed binary, and baseline caller/callee decompilation
agree. The user subsequently confirmed (2026-10-05) that the castle builds on
500x500 after failing on 400x400. This is a user-observed comparison, not an
instrumented capture of the first rejecting branch.

- Managed AIVLoader.SaveData.GetRawData writes frames in order. Import `0x55320`
  calls `0x53CA0`: serialized `(x,y)` becomes `(x,99-y)` before painting each
  positive-X/Y square. `0x56670` rotates the raster. `0x53D00` prepares frame
  anchors from its first surviving cells; later writes can overwrite earlier cells.
- `0x94350` builds mapper 61 at the prepared Keep anchor with the selected AIV
  orientation. `0x74DA0 -> 0xD0630` publishes the Keep reference coordinate.
  Relative to the 7x7 Keep anchor it is `(3,7)`, `(-1,3)`, `(3,-1)`, `(7,3)`
  for native orientations 0,2,4,6. This is not the raw AIV Keep anchor.
- `0x539B0` processes prepared AIVs through `0x51790`; the alternative `0x52270`
  is the procedural-economy path, not an alternative that bypasses the prepared
  defensive frames. `0x55F50` also reaches `0x51790` for prebuild.
- `0x51790` checks frame status, delays, resources (`0xCC420`), building capacity,
  and AI availability (`0x41230/0x41280/0x41380/0x414A0`). It runs obstruction
  cleanup `0x5CD90`, checks access connectivity `0xC3BF0`, then calls `0x6D580`.
  The cleanup return is not a universal build veto. Neither free construction
  nor prebuild's free flag by itself bypasses placement validation.
- `0x6D580` normally calls `0x77E60`; a prevalidated-state flag is a separate
  bypass. Its rejection field at tile manager +0x204E6FC prevents construction.
  Success dispatches mapper 147/structure 45 to `0x74240`, and mapper 113/
  structure 77 to `0x77230`, which create records and tile state.
- The actual dispatch bytes at `0x79914 + mapper - 51`, followed by the RVA
  table at `0x798F4`, route both mapper 113 and 147 to `0x7833C`.
  This bypasses `0x78334`, which would set a +5 allowance: these towers and
  gates receive **no +5 allowance**. The local was initialized to zero.
- Outside the editor branch, this block checks enemy proximity (`0xEE640`),
  then, with the Skirmish global nonzero, range (`0x6AF00 -> 0xEEF90`) and enemy
  Keep proximity (`0xEE840`). It iterates every footprint cell using `0x69850`.
  `0x619C0` creates those offsets as `0..size-1` in both axes. The mapper size
  table in `0x6A190` gives tower 113 size 6 and gate 147 size 7.
- `0xEEF90` accepts a cell when its maximum absolute axis delta from the own
  stored Keep reference is <= range. Otherwise, a same-team Keep may accept
  that cell at <= integer(range/2). If neither does, it returns 1 and writes
  reason 18. `0x783F1 -> 0x78472 -> 0x7848A` publishes placement failure.
  Subsequent tile/height/occupancy checks can also reject; they do not turn this
  range failure into success. Editor/non-Skirmish paths have different gates.
- Back in `0x51790`, failure sets ordinary defensive frames to status 5 and
  retry delay 16; success sets status 3 and delay 10. The scheduler can revisit
  the failure. Waiting or supplying stone cannot remove a permanent range failure.

Default `0x6AF00` map-size/range pairs: 160/45, 200/50, 300/60, 400/70,
500/80, 600/90, 700/100, 800/100; unknown size falls back to 70.
The Extender replaces this function but reproduces those defaults; its public
KeepProximityOverride and SetKeepProximityRange can change them.

## Baibars file application

Reproduction: workspace `_inspect/BaibarsBuildRangeAudit.ps1`; native hash guarded.
The raster check found all 49 remote gate cells and all 36 cells of each remote
tower still belonging to their original frames. No later frame overwrites them.
The distant targets require range 76 for the gate and 73 for each tower, across
all four native rotations with the matching Keep rotation.

| File | SHA-256 | Gate frame | Tower frames |
| --- | --- | ---: | --- |
| Nimrod1.aivjson | BB78019A77A642843FE369EEE4DE42A23091B87323CDD7F098D2DD53294A8D71 | 77 | 78,79 |
| Nimrodwest.aivjson | 2A86142A92F61A2F1F675C87F5BD1C6777703AF4275CC8D6D9939203B3373CE5 | 96 | 99,117 |
| Nimrodwest2.aivjson | 0B19EE3D9521EE60F797AC73A1C463111DB714CAE5F03ED9E0D0DF9ED380DF7E | 121 | 123,131 |

Thus an ordinary size-400 Skirmish with range 70 and no allied-Keep exception
rejects all three, even on clear terrain. Size 500's range 80 passes this one
condition, but is not a guarantee against terrain, occupancy, resource, enemy
proximity, connectivity, or mod-specific rejection. The reported map/save and
live settings were not supplied, so its exact first rejecting branch is unknown.
The subsequent successful 500x500 comparison supports this distance diagnosis.

### Vanilla layouts and the 100-tile bound

A read-only scan of the 376 CastlePlanner VanillaAIV files (including Community
variants) found a maximum required range of 56 for mapper 113/147, in jewel3.
This subset contained 458 tower and 46 gate placements. Unlike Baibars' 73/76,
these all fit the default 400-map range of 70. This is a geometric comparison,
not a guarantee that every Vanilla castle builds on every terrain or tiny map.

Valid full footprints occupy cells 0..99 on each axis. The 7x7 Keep's anchor
occupies 0..93; its four rotated reference offsets put the reference between
-1 and 100. Therefore max(abs(dx),abs(dy)) is at most 100 for any valid cell.
The inclusive <= comparison accepts that bound. A Euclidean diagonal bound
is unnecessary. ExtraFeatures/tests/KeepBuildRangeTests.cs enumerates all Keep
anchors and four orientations against the raster's extremal corners.

### ExtraFeatures API integration

KeepBuildRange is a host setting, default -1, integer -1..500. Nonpositive
settings release only this feature's override; positive values use the existing
public GameBuildingManagerAPI.KeepProximityOverride. No private game members,
new native patches or changed map-size table are involved. Existing ExtraFeatures
mode restrictions apply. Human and AI construction share this global value.

The installed RedBird ManagedValue<int>.SetValue replaces the current top entry;
it does not push a new override. ClearOverrides on map unload removes entries
above the base only, so a base SetValue can survive unload. The feature must
explicitly release its value on mission end/mode disable and reconcile during
central initialization before native prebuild and after save restoration.
Restore the saved predecessor only when the current value still equals the last
own write. This API has no owner token: an external write of the identical value
cannot be distinguished from our value. Other writers of this setting require
coordination; the inspected Fixes sources do not write it.

Local mod review: ExtraFeatures' defense settings affect enemy proximity and
post-success rebuilding; BugfixesAndQoL's tower-ruin/overbuild policies affect
cleanup. TunnelPlacementDistanceFeature excludes AI players. The canonical
Fixes clone's custom Keep rotation applies to human players. No range override
was found in those feature sources. This does not establish which mods/settings
the reporting player actually used.

## Correction to older cleanup-radius evidence

`0x79C0` RETURNS `abs(dx)+abs(dy)` but also writes dx, dy, min and max to
the supplied four-int buffer. With base `0x1834A9F50`, `0x1834A9F5C` is MAX,
not the return value. Both `0xEEF90` and `0x5CD90` read that max output.
Consequently the native cleanup boundary in `0x5CD90` is Chebyshev <=20,
not Manhattan <=20 as stated in the older AITowerRuinRebuildNativeAnalysis.md.
The independent mod policy BetterAIOverbuildPolicy currently does use Manhattan;
that is a code/native discrepancy requiring a separate scoped review, not a
reason to silently alter the mod during this Lord-file diagnosis.

## AI-only distance experiment and settings-flow review (2026-10-05)

Testmods/AIKeepRangeLimitTest targets the complete EEF90 function, including its
7A3B0 and EE320 callers, not only AIV 51790. Procedural economy construction
52270 also reaches ordinary validation through 6D580. Other caller-owned terrain,
occupancy, enemy proximity and resource checks remain intact. The public player
API IsAIPlayer bounds IDs 1..MAX_PLAYERS (8) and derives AI status from GetAILord.

The audited 317-byte body SHA-256 is
83D062DDDBAFEC9EB33F704FA914609B6761E16DAE351A64F7491319984DF12E.
Installed NativeX64 1.5.0.0 Indirect detours use six patch bytes, displacing two
five-byte saved-register instructions. Resume at EEF9A. Full-function Iced
inspection and current database xrefs show no interior entry. Database xrefs
include calls 7A73A/EE611 and function reference 88F5680; placement call 783EA is
confirmed independently because the callgraph omits part of 77E60. An additional
executable-section rel32 candidate scan found no interior target. The testmod's
UpdateToNewDLL.md records all validation, trampoline and fail-closed contracts.

ExtraFeatures now avoids writes for already effective values and does not claim
a foreign matching override. Its original predecessor survives owned positive
changes. Its existing internal lock only guards bookkeeping. Host/preset/trail
handling remains shared; no private engine lock or new synchronization is added.
The earlier synthetic concurrent ManagedValue stress results establish missing
API thread safety, not a demonstrated failure of the normal pregame settings flow.

Automated backend tests execute the installed NativeX64 detour/trampoline using
the audited prologue and a synthetic continuation. Actual Baibars/testmod,
prebuild, rebuild, economic placement, save/load and multiplayer acceptance remain
pending. The user's successful 500x500 comparison predates both mod changes.
## Integration into BugfixesAndQoL

The AI-only EEF90 feature is now owned by BugfixesAndQoL, setting
RemoveAIKeepRangeLimit (SyncHostOnly, default/reset true), under Fixes?.
Its UpdateToNewDLL.md and tests/AIKeepRange.Tests replace the removed testmod's
runtime documentation and tests. Native identity, complete-function and
Indirect/10 trampoline contracts remain unchanged. The global ExtraFeatures
KeepBuildRange setting is independent. No private engine lock or extra sync.

The former testmod's 2026-10-05 log confirmed installation at 14:09:28,
AI bypass for player 2 at range 70 (14:11:28), human Vanilla forwarding for
player 1 at range 70 (14:12:35), and zero Error/Fatal/classification failures.
Two sessions were singleplayer. The user confirmed successful castle building.
This is prior feature evidence; integrated gameplay, save/load, prebuild/rebuild,
additional AI paths, multiplayer and other global slider values remain pending.

The integrated runtime emits one installation Info and one first-callback Debug
marker; classification errors are reported once and keep the feature disabled
until restart. The old testmod is detected before installation to prevent a
duplicate hook. Its source/installed folders are removed after build verification;
_inspect/AIKeepRangeLimit evidence is retained.
## CastlePlanner lobby practice range audit (2026-10-05)

For native SHA256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2,
CastlePlanner mirrors the prepared raster (55320 last writer, 53D00 first surviving
Y/X cell), KEEP2 facing reference (D0630), and whole-footprint rejection through
51790 -> 6D580 -> 77E60 -> 6AF00 -> EEF90. The 77E60 dispatch gives killing pit,
pitch ditch, drawbridge and dog cage +5; gate and tower variants receive no bonus.
AIV wall/moat constructors are distinct and excluded. Allied half-range truncates.
DLL_RegisterSkirmishUser (86550) records lobby teams; 94350 allocates distinct native
teams for zero-team players. Zero therefore does not imply an alliance.

The existing native hash gate also gates this offline contract. No native hook is
added. Positive SE overrides and the mutable range table are captured on the main
thread. Optional public mod queries preview the next lobby game without writes;
ExtraFeatures previews restoration of an owned value, and BugfixesAndQoL reports
only an installed, healthy, enabled AI bypass. Unknown providers withhold a verdict.
Snapshots include teams and effective policy in the existing generation fingerprint.
Vanilla fit, caches and selection retain their existing inputs. Allied start positions
are taken from possible Vanilla outcomes; unproven Keep survival/human starts remain
possible help rather than guaranteed help. Percent deductions union cell sets.

Automated tests cover all four rotations, exact/over-limit boundaries, complete
footprint rejection, overwritten anchors, odd allied half-range, duplicate deductions,
policy fingerprints and all three Baibars Nimrod variants (70 blocked, 80 clear).
Live lobby host/client refresh and visual acceptance remain game smoke tests.
