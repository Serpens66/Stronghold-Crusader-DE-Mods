# Assassin stockpile wall flags and Keep side entry [Scope: Vanilla + mod integration]

Audit: 2026-11-13. Canonical installed CrusaderDE.dll SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Verified against CURRENT.json, DATABASE_INFO.json and the installed file.
Native function names not already curated remain candidate names; the following
instruction conditions are confirmed for this hash.

## Native data and execution

The user's editor map `test_assassinkeep.map`, SHA-256
`928340F97CB9768DF65C7FE60C111A7322E70C6DA14731744D05AA862F20A034`,
contains a stockpile access tile (368,398) with flags0x102, rawheight18,
connectionmask0x04 and buildingID0, next to a Keep roof tile (367,398) with
flags0x10008000, rawheight8, connectionmask0xF1 and buildingID1. The loaded game
reproduced these values. IsWall=0x100 and GoodsyardRelated=0x2 are independent;
stockpile connection tiles combine them. IsWall alone does not identify a
visible castle wall. Both ordinary east/west connection bits are absent here.

D9C40's ordinary connection branch precedes its cardinal Assassin fallback.
The fallback accepts a permitted target surface, zero source and target building
IDs and IsWall at either endpoint. Thus Vanilla's zero-building rule rejects
this direct Keep edge despite the stockpile wall bit. E1640 mode3 has matching
building/wall fallback checks after ordinary connection handling.

Physical step1855A0 ->DCE60 accepts an ordinary source-forward or target-reverse
connection first. Without either, its Assassin branch checks target surface and
IsWall, but not the search's zero-building rule. It chooses ascent126 if the
source lacks IsWall, descent128 otherwise. DCF66/DCF71 read byte heights from
4DDD350, not building roof offsets; the signed difference is stored at
GameUnit+0x410 and negative values are clamped to zero. Progress+0x414 starts0.

16CD70 plays the state128 preparation animation and enters129. On its first
progress pass129 calls180A80, which transfers occupancy and position to the
destination. 184FD0 then adds the independent standing/render roof height through
C07C0 for an elevated building; Keep2 adds92. This explains a visible upward
snap after a nominal descent of10 in the captured stockpile->Keep transition.
The separate ascent127 completion test progress>=height-35 is not the branch
that caused this captured case.

Normal Keep/wall connections are a separate ordinary route and never require
this climbing fallback. Gatehouses construct actual raised IsWall roof tiles;
their explicit building endpoint exception is described in
[the gatehouse audit](ASSASSIN_DIRECT_GATEHOUSE_CLIMB.md).

## Weighted-path correction [mod integration]

The weighted pathfinder's reservation exception admitted the Keep endpoint when
its connectionmask was nonzero. A nearby stockpile then supplied the only IsWall
bit. A real group command on the original map reproduced state128, height10,
from(368,398) to(367,398). The same map with weighted human routing disabled
reached the Keep with no126..129 states. Evidence is retained locally under
`.work/a/assassin-keep/001`.

The correction requires IsWall without GoodsyardRelated at at least one endpoint
of a nonordinary climb. It does not blacklist Keep types, change tile flags or
alter native animation/duration. Ordinary forward/reverse connections still win.
Managed/native weighted search, existence, backward proof, groups and cache
validation share the rule. APIShared's existing E19F9 owner applies it to native
reconstruction through an opt-in overload; existing consumers retain raw-IsWall
behavior by default. No additional game address or hook is introduced.

The regression fixture retains the exact two endpoint values, tests both
directions against the productive managed/native/group/cache paths and controls
for ordinary connections and a genuine wall. Eligibility tests do not establish
the physical duration of unrelated Vanilla climbing cases.
