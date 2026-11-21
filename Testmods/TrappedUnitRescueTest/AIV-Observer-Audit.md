# AI AIV drawbridge observer pilot

Audit date: 2026-11-17. This is a mod integration contract, not a claim of general construction coverage or game-test success.

## Native producer closure

The installed canonical `CrusaderDE.dll` was rehashed on this date and matches
`CURRENT.json`: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Reproduce each function read with `_inspect/CrusaderDE-Native-Baseline/tools/semantic/query.ps1 function <RVA>`.

`51790` resolves the player's AIV village slot from the player resources field
at `22FC`. One build-step record has size `C`; mapper is at record+2 and the
single-building packed anchor at record+8. The whole AIV system includes a
four-byte leading word, so the native expressions are system+`38` for the state,
system+`3A` for the mapper and system+`40` for the anchor, plus slot*`6D98` and
frame*`C`. The public SDK's player-resource/village/build-step APIs represent
these same domains. Slot zero is reserved; live slots are1..8.

Drawbridge mapper105 (`MAPPER_DRAWBRIDGE`) takes the single-building branch:
`51790 -> 7A170` bridge preflight/orientation, `5CD90` preparation,
`C3BF0` collision validation, then `6D580`. Restricted mode exits before this
branch for drawbridges. Other early returns and failed placements remain possible.
The native result alone does not establish any topology change.

`6D580` resolves the structure type through `C77C0`, validates placement through
`77E60` and explicitly excludes drawbridge structure49 (`31` hexadecimal) from
its unit deletion/relocation loop. Drawbridge construction invokes `739C0`.
That constructor allocates through `B47E0`, iterates the size5 footprint through
`69850`, stores the building ID on its25 cells and applies its orientation table:
support cells receive `IsBuilding` (`400` hexadecimal), completed-moat markers
are cleared, moat decoration uses `59210`/`725A0`. Access and neighbor updates
are performed by `6CDD0`/`725E0`; final placement dirties/rebuilds path data
through `E3B90`/`D95A0` before returning. The tested native footprint origin is
the packed anchor, with x/y offsets0..4 (the existing real bridge fixture also
checks all25 actual building IDs). No unit identity recreation is necessary.

This selected branch does not call `6CB20`. The Script Extender AI-wall event
and Fixes wall recovery attach to `6CB20`; they are external integration, not
Vanilla behavior. They are not suppressed while the original AIV invocation runs.

## Public broker and lifetime

`APIShared/src/Diagnostics/AivBuildStepCapability.cs` owns the native entry.
Its public `IAivBuildStepCapability` observer contract brackets the one original
call and completes receipts in `finally`, with `VanillaCompleted` distinguishing
exceptional exits. The exact delegate is Cdecl `int(ulong,int,int,int,byte)`.
No second native hook is installed by this testmod. The owner-local registration
is `rescue-ai-drawbridge`, under the testmod GUID.

The observer keeps managed snapshots and a session token, never native pointers
or spans, across the original call. Completion is single-use, checks the identical
context object and current session, and refuses exceptional original completion.
There is no frame/tick/unit polling. Snapshot discovery traverses only25 tile
occupancy chains, validates all chains before candidate capture and fails closed
on malformed links or bounded budgets. Only actual current-tile occupancy counts;
auxiliary-only links do not nominate a unit for rescue.

## Activation and safety boundaries

The existing experimental setting remains off by default. Central mission start
and end callbacks activate/invalidate the observer. Unknown/conflicting modes
and real multiplayer are excluded. Save loading enables only future observed
placements; no old trap is inferred from an already-invalid start tile.
Editor sessions are accepted by lifecycle policy, but ordinary editor/human
building commands do not execute this AIV producer and are not covered.

Ordinary supported military roots only are admitted. Civilian job continuation,
siege/crew alias validation, active special movement and completed moats remain
outside this pilot. The runtime's existing conservative state, old reachable
candidates, newly-invalid source, physical exit, identity and occupancy checks
still decide each post-construction attempt. A normal no-op remains a no-op.

The general runtime has independent activation from fixture runtime. Ordinary
fixtures disable this observer; the dedicated real-AIV fixture opts in explicitly
under the public simulation monitor and calls the live native entry without the
fixture construction suppression scope. Only our own synchronous post-original
rescue/reissue opens the existing Fixes gate, and `finally` closes it. Native
`196280` contains no construction path, so that short scope cannot suppress an
original AIV wall build. The original AIV call always retains original Fixes behavior.

Focused tests exercise the actual production receipt class: single completion,
zero result requiring topology verification, exceptional original, mismatched
invocation, replaced session and non-replay after a recovery exception. They
do not prove native placement or gameplay acceptance. Real broker integration,
AI continuation, save/editor coverage and performance measurements require their
own dated runs; this document records no game PASS.
