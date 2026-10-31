# AI woodcutter retirement and repeated construction [Scope: Vanilla]

Static audit, 2026-10-31. Installed CrusaderDE.dll SHA-256
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2 matches CURRENT.json
and the selected database manifest. Sources: same-hash function pseudocode,
instructions and xrefs; key comparisons also decoded directly from the installed
DLL. Function names remain candidate labels; the control/data-flow statements
below describe the instructions. This is no diagnosis of a particular save.

## Regular woodcutter retirement

Scheduler 0x539B0 calls 0x3B360 before 0x2E1E0 eligibility and 0x51540 construction.
If retirement returns 1 it skips that pass's construction branch. It may construct
again on a later pass. 0x3B360 requires a nonzero effective profile. When global
0x3505470 < 1 it uses 0xBD320 to find the first owner/type-3 building whose state
is neither 0 nor 3. Otherwise it requires wood planks >= 21, fully staffed huts >= 2
and maximum stored worker path length >= 25, then uses the stored worst-building ID.
The stock and player values are signed int32 comparisons with player stride 0x583C.

Player roots: wood 0x379B2A8; effective profile 0x379D0D0; fully staffed huts
0x379D8EC; stored maximum 0x379D8F0; worst building ID 0x379D8F4. 0xACF40 counts
GameBuilding+0x19C == 0 as fully staffed, otherwise understaffed. It updates the
maximum/ID from the signed short at building+0x2D4; a nonzero short at +0x2C8
resets this stored path value to zero. The meaning of +0x2C8 remains qualified.

Worker 0x12BC00 selects a tree via 0x107E00, obtains an approach tile and routes
through 0x196280. On success it writes the unit's calculated path-plan length to
building+0x2D4. 0x196280 can route to an intermediate component waypoint, so the
stored value need not be the complete worker-to-tree distance. It is not Euclidean
distance or a hut-centered radius. 0x107E00 checks a reachable approach neighbor
and chooses by max(abs(dx),abs(dy)) via 0x79C0's fourth output; it does not rank
candidates by their full computed route length. Thus geometric proximity does not
exclude a long actual route.

## Counter retirement and accessibility

0xACF40 also calls 0x3B270 with the 1-based building ID. This reads signed low words
at building+0x2DC (counter) and +0x1B8 (guard; Interop exposes a UInt32 at that
offset). Positive counter, owner control == -1, owner enabled != 0 and active-game
flag != 0 enable its logic. Guard == 0 and counter >= 20 cause retirement. With
nonzero guard it may decrement the counter on the rotating owner's turn.
Operands: control 0x8574BCC+owner*4, enabled 0x8574C44+owner*4, game flag 0x8574B90,
rotating player 0x382D080. Do not assign a stronger meaning to the guard/counter.

In worker 0x12BC00, state 110 increments a unit counter and, after it exceeds 32,
adds 10 to the hut counter. It can remain in that state and add again; this is not
necessarily two separate failures. One entry to this state follows failure of
0x19B930 to obtain a return target for its assigned building. That helper checks
building/global identity and 0xC90E0 building accessibility. Other worker state
transitions also enter state 110; the full semantic meaning is not established.
Failure to find a tree instead enters a retry/idle state, not a direct hut deletion
in that tree-search branch.

### Combat-death input to the same counter (audit extension, 2026-10-31)

The fatal projectile branch in 0x192750 tests current health at native unit+0xA20
below 1 and the death marker at +0x8F8, then assigns state 0x6F or 0x72 at +0x918.
The melee function 0x199110 subtracts damage, returns early if health remains
positive, and otherwise assigns death state 0x71 or 0x72 according to attacker
type. Those native offsets correspond to GameUnit health +0x3C4, death marker
+0x29C and AI state +0x2BC (manager/header offset 0x65C).

In 0x12BC00, state 0x6F and the 0x6F..0x74 interval accepted by helper 0x199D50
complete their animation and transition to 110. Thus combat death, not only failed
return-target acquisition, reaches the hut-counter writer. At 0x12D190 the handler
sets unit alive state 3; 0x12D199 adds 10 to the signed hut counter. The following
site 0x12D1A2 sees its actual post-write value. Do not equate a threshold value of
20 with two distinct deaths without unit/global-ID evidence and actual writes.

The shared state store at 0x12CD07 also writes ordinary state 6. For value 110 its
incoming paths follow failed 0x19B930 return-target acquisition from worker states
3, 8 or 9. RCX contains unit ID*0x490 here; R14 may instead hold a vegetation base.
The counter writer uses RBX as the unit-record offset and RSI as the cached hut ID.
The complete handler has no SIMD operands. The 18-byte state-store span and
21-byte post-increment CMP/JNE/MOVSX span have no incoming interior xrefs in the
current semantic database. These are instruction/data-flow facts, not evidence
that any particular combat death caused the previously observed save's demolition.

The separate 0xC8F50 accessibility sweep excludes dead/already-marked buildings and
route-bypassed types; type 3 has bypass value zero at 0x2E4FE0+3*4. It calls 0xC90E0
and marks state 3 for results 0 or 2, then propagates state 3 to extant buildings
with the same nonzero group and calls 0x55E10 for the initiating building position.
The group key is GameBuilding+0x2A8. Mod protection can alter this inner decision;
Vanilla facts alone do not identify the original/effective result in a modded run.

## Grid wood and temporary avoidance

AIV root 0x34A9F70 + 0x5B500 equals global 0x3505470. Writer 0x50720 clears it and
adds 3 per qualifying tree tile: tree flag, vegetation type 1..4 (chestnut/oak/pine/
birch), alive 2, growth < 4 and positive identity/resource predicate 0x1086D0.
This map-wide grid wood weight differs from a local count of visible trees.

Both retirement functions call 0x55E10, request a 50-percent refund through 0xC4480,
and mark the building through 0xC4290. 0x55E10 uses cell x/5,y/5 with index
coarseX*160+coarseY and stride 0x30. Its signed bytes are root+cell*0x30+0x5B847
(removal count), +0x5B843 (retry delay), +0x5B837 (wood weight). If old count < 10
it increments the count and sets delay to 3*oldCount+10; otherwise delay is 10.
Mode-0 0x50720 decrements positive retry delays once per grid refresh, not once
per simulation tick. It does not clear the removal-count byte in that refresh.

New construction eligibility 0x2E1E0 uses global wood weight, profile/AIC wood
ratio and hut cap, population per hut and additional wood-weight thresholds for
larger hut counts. It does not reject the previous hut's stored worker path length
or failure counter. Site search 0x58020 ranks grid wood and search depth, requiring
zero retry delay; 0x58950 separately chooses a nearby empty coarse cell. Neither
is a permanent blacklist of the surrounding region. A removed cell can therefore
be selected after its delay, while neighbors can remain available sooner.

## Terminal state and limits

State 3 is a removal request, not an empty slot. Building update 0xC60F0 dispatches
marked entries to cleanup 0xB8310, which ultimately clears the record. Follow both
slot and global ID to distinguish that cleanup from reuse. A function return or
refund event alone does not prove the terminal state or historical reason.
Ordinary strategic emergency-demolition categories 1..7 in 0x2D810, selected by
0x2F080, do not include structure type 3. These statements do not rule out damage,
script actions or other mods as additional causes in a specific game.
