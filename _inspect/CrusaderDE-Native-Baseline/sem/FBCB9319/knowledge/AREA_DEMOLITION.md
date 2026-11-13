# Bulldozer query and typed removal [Scope: Vanilla + mod integration]

Static audit dated 2026-11-11, canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Internal function labels remain candidate names; the specific branches and fields
below were checked in the matching export and canonical machine code. No game
acceptance is claimed. Managed members were checked against the installed genuine
`Assembly-CSharp.dll`, not a publicized reference.

## Input and native permission

`EditorDirector.preDLLCallActions` calls `EngineInterface.DeleteBuilding` for
primary states 1/2, not release 3. The wrapper locks the engine monitor and enters
`DLL_MapAction` 0x85BD0 with delete mapper 39. Dispatch 0x8AAF0 reaches 0x90830.
Default mouse state zero does not enqueue removal. The dispatcher consumes
cursor-derived fields as well as coordinates: arbitrary-coordinate wrapper loops
are not a complete targeting contract.

Permission 0x79B90 takes `(tileManager, actor, tileX, tileY)`. Its full writer set
is the seven tile-manager dwords 204E6FC/704/720/76C/770/774/7F8, cursor state
within RVA 3A11DC0's first 80 hexadecimal bytes, and eight neighbor integers at
8DDCA0..8DDCBC. Callees are read-only proximity scan EE640, moat lookup 69560,
and neighbor scratch builder 64F80. Flattened selector 204E694 is an input.
Moat eligibility admits teammates; exact ownership is a separate consumer rule.
The ordinary cursor builder 459D0 can emit a hover event through 109EF0 and is
not a side-effect-free replacement for explicit permission input preparation.

## Ordinary typed execution

Chore 29 at 12400 reads signed 16-bit building/pitch slot, refund byte and global
identity. A positive slot with matching identity refunds through C4480 and marks
through C4290. Parent refund tests the LOW 16 bits of raw slot +2E4 (typed +288),
not the entire dword. Gate types GATE_MAIN/INNER search at most two attached
drawbridges through B9330, refund/mark them, then process the parent. C4290 also
marks compound-linked records; later C60F0 -> B8310 performs terminal cleanup.
Negative slot with matching pitch identity removes via 62EB0 without refund.

Chore 41 at 13750 reads a tile and signed refund byte. Normal wall removal refunds
via C4480 then calls 63700. Canonical branch 13880 is `7F 11`: height minus default
height <=60 halves the producer's 50 percent refund. Fixes' recognized `EB 11`
skips this halving. This parent-handler policy is outside the reusable helpers.

**Dug-moat sentinel -1 removes in one operation.** At 1383D the byte write targets
`tileManager + taskIndex*16 + 1F3EE3D`, task progress +D, not work type +E.
Progress is zeroed BEFORE 69470 decrements/tests it. Terminal removal clears moat,
restores height, frees lookup/task through 61E70, and the handler clears tile
MiscDisplay bit 400. An earlier provisional four-step interpretation was disproved
by the full instruction sequence and must not be reused.

Un-dug PlannedMoat is a separate selector and Chore 21 cancellation route. Its
full cancellation contract is not established by this audit.

## Testmod integration [mod integration]

AreaDemolitionTest supplies typed targets under the engine monitor and restores
all cursor, flattened, actor and neighbor scratch it changes. It calls existing
live helper entries; no additional native detour or native reader substitution is
needed. Exact target ownership and all affected linked children are revalidated.
Query and helper bindings, ABI, native hashes and reference/fallback behavior are
documented in `Testmods/AreaDemolitionTest/UpdateToNewDLL.md` from the workspace root.

Large selections use authenticated APIShared batches rather than one queued native
29/41 per target. Local queue space alone is not a proof of remote queue capacity:
both local 23990 and receive 23F10 use slot finder 20950, which returns an active
slot if all 500 are busy. The testmod bounds its own outstanding batches with
all-simulator acknowledgements and never recursively enqueues per-target commands.
This does not guarantee that unrelated game/mod traffic cannot exhaust the queue.

Private-memory tests cover scratch restoration, layouts, refund policy and the
ordered one-step moat writes; they do not establish optical or real multiplayer
acceptance. Ingame acceptance was explicitly deferred by the user on 2026-11-11.
