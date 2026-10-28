# Lord home relocation and return-path fallback [Scope: Vanilla + mod integration]

Static audit observation 2026-10-28, canonical installed CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Decompiler, installed PE disassembly, function-pointer table and caller/Xref checks agree on the flow below.
Confirmed-static means machine-code/control-flow evidence; it is not fresh in-game acceptance.

## Return and relocation [Vanilla facts]

DLL_RunTick `0x86680` calls game tick `0xCDE60`, then unit dispatcher `0x182B00` dispatches through the
unit-update table at `0x321CB0`. Lord type `0x37` maps to `0x15A950`. Native unit IDs are one-based,
zero is reserved, stride `0x490`; the GameUnit view origin is manager `0x67E8400` + id*stride + `0x65C`.

Player maintenance `0xC23C0` refreshes Lord home fields at manager working offsets `0x992/0x994`
(GameUnit +0x336/+0x338), from the player's Keep position; normal Y is Keep Y-1, with a type-0x28 exception.
Lord state 1/new-route flag nonzero calls `0x196280(manager,id,homeX,homeY,0)`. A return value of zero
branches directly to the relocation CALL at `0x15ABD1`, targeting `0x19BA40`. There is no local human/AI
condition around that failure branch. The arguments are manager, current one-based ID, home X/Y and
tile height; the decompiler omits several arguments but installed disassembly proves all five.

`0x196280` may fail at coordinate/map validity, destination surface, cross-component/portal admission
or final route construction. The home relocation is not conditioned on a specific gate or wall type,
and no additional teleport timer is tested after the zero result.

`0x19BA40` removes old tile occupancy (`0x186AD0`), resets movement state, writes current/next tile X/Y,
fine coordinates tile*8+4, height and compact tile ID, adds occupancy (`0x180230`) and recomputes
view offsets (`0x18BE50`). The Lord caller sets state 1 and clears its new-route flag after the CALL;
the completed-path predicate `0x188340` then normally leads into state `0x6D`, resume state 2.

## Reproduction preconditions [Vanilla facts]

Closing the castle and leaving a human Lord outside does not itself guarantee state 1. In the native
nonzero-mode Skirmish/multiplayer path, ordinary movement completion leads to idle state 13. That state's
handler returns for human player slots before its autonomous return-to-Keep logic. Therefore waiting
alone is not a sufficient reproduction, and the shared failure branch's absent human check must not be
mistaken for unconditional reachability from human idle.

The building-attack state `0x6B` has target-loss/completion exits assigning state 0 in nonzero mode or
state 1 in zero mode. State 0 subsequently arms state 1/new-route, so destroying an exterior target while
the home route is blocked can reach the shared fallback. On 2026-10-28 the user confirmed the referenced
video shows the teleport after a building is destroyed; that report supports the trigger but is not a
controlled current-version gameplay test. The exact animation/route progression controls timing, not
a fixed delay measured from gate closure.

## Invalid-standing-component rescue [Vanilla facts]

The dispatcher's first pass has another direct home relocation CALL at `0x18300F`, return `0x183014`.
It checks one of sixteen unit-ID buckets per pass; the compact current tile must have component zero
and lack bit `0x40000000`. Special/building, state-0x6E and age gates also apply (age >=1000 ticks).
For Lord type 0x37 it relocates to the stored home point and writes state 1/new-route flag zero.
This also lacks a human/AI gate. It is separate from a Lord standing on otherwise valid ground outside
a disconnected castle. Other unit types take different recovery/removal paths.

## Selective prototype [mod integration]

BugfixesAndQoL now owns the 16-byte entry span at `0x19BA40` (migrated from LordTeleportGuardTest) and blocks only a live Lord controlled
by a player whose native lineup entry is zero, when both requested coordinates equal the stored home
point. Lineup RVA `0x8574C44` is independently identified by LEA at `0x28A7D` and SE GetAILord/IsAIPlayer.
The current native predicate exempts the invalid-standing-component caller (return 0x183014), retaining
Vanilla terrain rescue for humans as well as AI. The remaining home predicate still covers Lord-update
function copies that call the common helper, without intercepting unrelated unit relocations. No local-player or managed-roster decision is used.
See BugfixesAndQoL/UpdateToNewDLL.md for the current owner, spans, data contracts and fixture boundaries.
Actual game, editor, save-loading and real host/client acceptance remain pending until separately observed.

## Runtime acceptance 2026-10-28 [mod integration]

The user reported that the Lord did not teleport in game. The preserved current-start log
`.work/a/lord-teleport/002/LogOutput.log` independently records the prepared 16-byte guard, its first
simulation callback after startup cleanup, and four actual blocked return-path relocations at ticks
3613, 4569, 5619 and 8706. All identify unit 22/global ID 2039/controlling player 1, state 1,
reason `return-path-failed`, return address `0x7FFD1FEFABD6` (the return-path CALL continuation).
The source tiles are respectively (362,264), (342,289), (338,299), (400,253); the home is (408,285).
Final counters close exactly: blocked=4, records=4, dropped=0, invariant=True.

The central mode snapshots classify session 1 as a new singleplayer CustomGame/Skirmish,
realMultiplayer=False, mapEditor=False and multiplayerSave=False. Script Extender 2.14.3 and
Fixes 1.29.1 were co-installed. This start has zero Error/Fatal lines and no guard failure markers.
The installed testmod files still match `.work/a/lord-build/004/result.json`; the installed native
hash matches this baseline. The machine-checked report and snapshot SHA-256 are in
`.work/a/lord-teleport/002/result.json`. Earlier build evidence is retained unchanged.

This confirms human Lord return-path rescue suppression in the user's current game. It does not
yet confirm the separate invalid-standing-component branch in game, AI rescue behavior, movement
after reopening gates, save/load, editor operation or real two-peer multiplayer; those remain separate.

## Standing-tile causes audit 2026-10-28 [Vanilla facts]

Same canonical installed native SHA-256 as above. The condition concerns the unit's compact current
standing tile (manager working +0x72C / GameUnit +0xD0), not a route destination or the existence of a
route back to the Keep. Dispatcher 0x182B00 reads the uint16 component grid at RVA 0x50EC690 and
requires component zero. A disconnected but otherwise walkable region normally has a positive ID.

Completed moat is explicitly excluded: at 0x182EF5 the dispatcher tests tile flags (RVA 0x48F71B0)
for 0x40000000 and branches past rescue when set. The independent moat completion writer 0x639C0
sets this exact bit, clears planned-moat bit 0x4000, updates local path edges and marks the component
map dirty. The Script Extender TilePropertyFlag names these IsMoat and PlannedMoat respectively.
Merely planning a moat does not set a component-seed blocking bit. Thus digging a moat beneath a Lord
is not a trigger for THIS invalid-component rescue while the resulting tile retains IsMoat. This does
not exclude a separate return-path-failure teleport on another Lord state transition.

Rebuild 0xE49D0 clears and republishes the component map. Ordinary seed admission rejects mask
0x4A5014B1 (including impassable terrain, building/tree, river, crenellation, hops, farm fence and moat
bits), with vegetation exception 0x107160. Local edge updater 0xD86F0 handles additional wall, stair,
elevation and building cases; do not infer that every wall/building or every farm tile has component
zero. Ordinary wall bit 0x100 and bare farm-plot bit 0x4 alone are not in the seed exclusion mask.

Concrete producers include vegetation placement 0x6E2C0 and orchard-tree creation 0x1071A0: both set
IsTree, update local path data and mark components dirty. Vegetation type 0xF used by the orchard is
excluded from the passable-vegetation exception in 0x107160. Existing AI_WOODCUTTER_BUILD.md evidence
independently records orchard-associated component changes 1 -> 0 in a fresh-map diagnostic run.
That proves production of zero-component tiles, not that a Lord was standing there or that a Lord
teleport was observed. Natural propagation 0x108320 has extra candidate checks through 0x107A80;
do not assert that ordinary trees can necessarily grow beneath an occupied Lord tile without a
controlled reproduction. Terrain/editor changes and later path-map rebuilds can also invalidate an
existing standing tile, subject to each operation's placement checks and dirty-map behavior.

The age gate is current simulation tick (RVA 0x3665F58) minus the unit's stored creation tick
(manager working +0x6F4 / GameUnit +0x98), at least 1000. It is NOT a 1000-tick stuck timer. The
modulo-16 unit bucket, unnamed working +0x6EC != 1, state != 0x6E and building gates must also pass.
A nonzero building ID suppresses rescue unless the building is live, class table 0x2E68D0 is zero,
and type is not drawbridge 0x31. No natural-gameplay Lord reproduction of this second path has yet
been observed; the preserved guard log confirms only the return-path-failure branch.

Reproduction sources: semantic-decompiled-functions.c entries 0x639C0, 0x6E2C0, 0xD86F0, 0xE49D0,
0x107160, 0x1071A0, 0x107390, 0x107A80, 0x108320 and 0x182B00; installed-PE instruction checks
at 0x182EE6, 0x182EF5, 0x182F01, 0x182FAB-0x182FB8 and the existing moat/region-refresh audits.

## Rescue policy revision 2026-10-28 [mod integration]

After the standing-tile audit, the user explicitly chose to retain Vanilla invalid-component rescue
for human Lords. The native helper hook now passes through the validated caller at 0x18300F with
return address 0x183014; the existing human/home predicate remains unchanged for other callers.
The exemption is evaluated before unit data reads, preserves incoming registers/flags/stack and
adds no managed call, allocation, path search or tile scan. It is also present in non-diagnostic builds.
Normal and DEVDIAG execution fixtures must observe real position, height and occupancy writes for
this caller for all eight human slots, while the return-path-failure caller remains blocked. Historical
build 004 and its four singleplayer blocked calls above remain evidence for that earlier artifact;
current build and gameplay acceptance are recorded separately in the local work index.

## BugfixesAndQoL integration 2026-10-28 [mod integration]

The user authorized migration to BugfixesAndQoL and deletion of the unpublished standalone testmod.
EnableLordReturnTeleportFix is a default-true/reset-true SyncHostOnly option in HostTroops/Fixes.
The permanent helper hook is installed in LibraryLoaded even when disabled, using the same audited
native predicate and standing-rescue exemption. Its atomic data flag is installed && EnableMod &&
EnableLordReturnTeleportFix; ApplySettings reconciles local and received host/preset changes.
NativeSiteRegistry owner is BugfixesAndQoL_Serp and BepInIncompatibility names the retired test GUID.
The native emitter and its assembly helpers are text-identical to the last tested testmod version;
only namespace and the managed activation policy were added. Static feature/tick roots survive Unity
cleanup; no per-unit managed call, additional search or live unpatch is introduced.
Executed normal/diagnostic native regressions, built-assembly activation/UI checks and scoped shared
preset tests are recorded in .work/a/lord-bfqol/001. Existing testmod runtime records are preserved.
The user reported the revised testmod still works; no new integrated game/editor/save/real-MP run
has yet been observed. This is distinct from the successful offline integration checks.

## Integrated gameplay report and diagnosis cleanup 2026-10-28 [mod integration]

After installation of BugfixesAndQoL 1.0.207, the user reported that the integrated fix works.
This is a user-reported gameplay observation, without a fresh captured log or explicit editor,
save/load, AI-rescue or real two-peer multiplayer acceptance. Earlier testmod and integration
build/log evidence is preserved unchanged.

The completed diagnostic implementation is removed: native trace emission/storage/counters,
per-teleport records/context, READY/RUNTIME_ALIVE success dumps and the diagnostic-only tick
observer. The process-static feature root still retains the permanent hook and activation flag.
Logging now retains only the existing shared native address-resolution line and one explicit
initialization failure explaining that the feature is unavailable. No gameplay/settings/version
change is made. Normal emitter/restore equivalence and existing normal/diagnostic machine tests
are recorded separately under .work/a/lord-clean/001. Normal and /devdiag builds now use the same
Lord predicate; other BugfixesAndQoL diagnostic features retain their own compile-time gates.
