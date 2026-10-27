# Selected movement marker records [Scope: Vanilla + mod integration]

Observed 2026-10-27. Native SHA-256:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Evidence: current semantic function exports for RVAs 0x8C5F0, 0x1222A0,
0x417A0, 0x41D60, 0x41D10 and 0x1A13C0, checked against the installed DLL hash.
Roles and record flow below are confirmed-static; the database's anonymous
function names remain candidate names rather than an invocation API.

## Vanilla records and lifetime

The selected-group publisher at 0x1222A0 is reached from 0x8C5F0. It enumerates
the selected tribe and publishes persistent movement markers (category 107,
sprites 82..89), the separate short click animation and other order overlays.
Animation transparency changes the upper flags without changing tile identity.

0x417A0 receives (drawManager, category, sprite, horizontalOffset,
verticalOffset, tileId, flags). A negative signed tileId is silently ignored.
Records are accepted only while manager+0x622248 is below 250. The index starts
at one: usable records are 1..249 and index zero terminates a list.

The 28-byte records start at drawManager+0x6206F0. Their first two int32 fields
are **sprite at +0**, then **category at +4**. Horizontal offset, vertical
offset, tileId, flags and next index follow at +8/+12/+16/+20/+24. Duplicate
identity is (tileId, sprite, category), independent of offset and transparency.
The global ushort tile head grid is at RVA 0x47BDD30.

0x41D60 traverses the visible tiles and these linked records. Its normal
overlay branch forwards category and sprite in that order to 0x1A13C0 mode 8,
including terrain/structure height adjustment and transparency. Mode 8 indexes
its hash cache by identity, at manager+0x101D0B0 (8-byte entries); the following
table begins at +0x1025580. The existing bounded extension reserves identities
250..4249 (4,000 additional identities), without extending native storage.
The reset at 0x41D10 clears used tile heads and next links and resets count to
one at the end of the visible rendering pass.

## Renderer recovery [mod integration]

The 2026-10-27 11:34:02.581 game log reported an overflow exception from the
additional-marker renderer. Its former boolean insertion result combined an
invalid tile and a full buffer into the same failure. The available log does
not identify which condition occurred. The renderer's process failure latch
then made FormationRuntime.TryStartDrag reject every later gesture, matching
the user's observation that small selections also stopped working.

The corrected insertion separates Added, Duplicate, InvalidTile and
CapacityReached; the last two skip that marker without latching a failure.
Native duplicate reads follow the field order above. Additional identity
capacity remains 4,000; command/selection capacity is separately 10,000.
Unexpected presentation errors count once per real native render pass or
input gesture. One healthy completed attempt resets the applicable streak;
critical contract failures and three consecutive matching failures stop the
affected local path. Published native hooks are never removed or repatched.

Map-editor manual admission ignores saved AI flags for all eight players;
the editor user controls every player. Regular game AI commands retain their
existing early return. Preview and execution use the existing native region
query and preserve its scratch state, without additional per-unit full paths.
Production-source fixture acceptance and actual Unity/editor acceptance are
separate: fixture success does not establish in-game visual behavior.
