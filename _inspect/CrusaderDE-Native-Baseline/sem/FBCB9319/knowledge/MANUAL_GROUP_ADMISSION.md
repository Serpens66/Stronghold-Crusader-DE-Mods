# Dry mixed-group admission before order mutation [Scope: Vanilla + mod integration]

Observed 2026-10-26; installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Confirmed-static control/data flow; game command latency and real multiplayer
acceptance are separate, pending measurements. Replaces the Post-only repair in
[MANUAL_GROUP_REGION_RECOVERY.md](MANUAL_GROUP_REGION_RECOVERY.md).

## Closed native chain and mutation boundary

Input 195E30 stages the Chore before feedback. Player wrapper 196100 updates tribe
record +56C before invoking group dispatcher 11B520. Group records use stride 688,
leader +5A and count +5C; unit IDs remain one-based. The ordinary group's DA590 /
D9C40 / DAFD0 membership search can continue looking for disconnected members.
Use its existing 117BC0 / E7C40 branch to enter 118E00 early for a supported split
group: return the real leader from 117BC0, then zero from the matching group-only
E7C40 call without running that flood. The immediate zero branch calls 118E00;
no E7C40 flood output is consumed before this call.

118E00's whole 497-byte body SHA-256 is
`FE838B2718364376F36321E32940F4418AF6678151501D17A74E0F4B0E897A39`.
Its native alive/kill/state/ownership/mobility predicates precede 118F50. At 118F50
the first per-member order store writes target X to unit-manager + id*490 +934;
the following instructions overwrite AI state, movement state and target Y before
196280. That terminal function additionally writes requested target and can clear
the 1000-byte packed route before returning failure. A terminal move loop or a
post-return-only veto cannot preserve an old moving order.

Gate span [118F50,118F5E) is exactly two instructions (9 + 5 bytes):
`664689BC2934090000 BA65000000`. Resume is 118F5E; rejection continues at 118FBE.
Full native executable-section direct-edge scan found no incoming edges into the
span. 118E00 has no indirect jump table or alternate entry in this block. EAX is
unit ID, RCX=id*490, R13=unit manager, RSI=tribe, R15W=target X. The native gate
saves/restores R10/R11, flags and stack; no managed callback or allocation occurs.
Windows x64 NT_TIB.Self (GS:30) binds its active state to the executing thread.
Unit global identity is native +6F0: GameUnit starts at +65C and GlobalId is +94.

The full unit copy starting at +658 is not the GameUnit view base. In particular,
native +A9E is GameUnit +442 (N000000DB), not +446. The writer at 19A44C in 19A240
loads it from type table 3226B0[type*4]. Stationary Trebuchet/Mangonel/Ballista have
zero there; Spearman, Slave, Catapult and Fire Ballista have one. Keep the complete
native common predicates; the additional filter only excludes, never grants.

118E00 clears native +748 (GameUnit EC/EE) and +74C (GameUnit F0) before 196280.
It does NOT clear PathPlanStateBitFlags: the installed Extender layout puts those
at GameUnit F2 (native74E), MovementSubstep at F4 (native750). The checks at 196305
and 19630F select current position only when flags==0 AND substep==8; otherwise
NextTilePosition2. The existing GetNativeMovementStart helper matches these checks.
An initial draft incorrectly associated the +748 store with the flags; installed
Marshal.OffsetOf results and full disassembly disproved that association before
build. Regression coverage includes flags!=0 with substep8.

The preceding 19B1B0 normalizer derives current coordinates from packed current
tile ID. If they differ it rewrites both current and next coordinates and enters
18BE50. Such incoherent records remain unsupported by the filter; it neither
executes normalization as a probe nor predicts the post-normalization route.

## Region proof and integration [mod integration]

E2610(player, source, destination, signed native mode) has no tile path search.
Its complete body/write contract remains in UNIT_COMMAND_PROBE_SCRATCH.md:
manager+98/C0/C4 (three int32) restore in finally for query-only use. Equal positive
regions avoid a query; other answers are reused only for a single command and key.
A positive macro answer is not a proof of a tile route or movement permission.

The new batch is prepared before queue and formation mutation. Ordinary groups
with unsupported moat, Assassin, structure/exit or active-provider contexts keep
existing routing. Shift insertion is identified by a read-only queue predicate;
admission runs when its waypoint executes. All-excluded commands skip 11B520 and
never open queue/formation completion scopes. Player-wrapper +56C is restored for
that rejected invocation, with nested frame ownership preventing parent rollback.
Mixed groups enter 118E00 once and the native stamped exclusion table skips writes
to excluded identities. Topology invalidation disables the current mask; stale
parents cannot reactivate it on nested return. No tribe membership is rewritten.

Formation protocol 7 retains full ordered selection identity with a separate hash.
Canonical execution filters members before slot generation, preserving direction,
density and intended width. Its execution plan hash includes the selected identity
and admitted subset. A filtered plan is generated in synchronized simulation;
the sender's full-selection preview plan cannot authorize it. Missing preview
slots do not block native dispatch. Observed rejection/partial execution consumes
the input once instead of replaying the entire group. Multiplayer acceptance
requires matching builds on both peers; mixed protocol builds are not validated.

Production-source fixtures exercise actual admission, formation and dispatch code.
The real generated X64InlineHook is executed against destructive order/path writes,
checking unchanged old state for exclusions, thread/tribe/generation/global-ID
guards, ABI/flags, and 100/200/550/1000 members. Harness timings exclude the engine
and real E2610 and must not be reported as in-game command latency.

## Admission flood and member check 0x124740 (audit 30.10.2026)

Source: pseudocode of `0x11B520`, `0x124740`, `0x119F90` in the semantic export and the disassembly of the installed DLL (FBCB9319).

- `0x11B520` floods from the leader tile with the group's mode (`0xD9C40` Assassin, `0xDAFD0` moat, otherwise `0xDA590` without target) and budget `d*d/2` from `0x79C0` (`d` above 0x2D), then calls `0x124740(tribeManager, tribe, generation 0x60AD664)`. While it returns 0 the whole flood runs again with budget +5,000 (capped at `0x4E520`), each time followed by `0x124740`.
- `0x124740` (body 0xAC bytes, SHA-256 `FFB294BD9F96BE36B85A9D7D3031746B843F7D1D1F98148E169003E449C9817C`): `count` = signed word at tribe `+0x5C` (stride `0x688`); for `i` in `0..count-1` the member `u = 0x119F90(manager, tribe, i)`; it returns 0 at the first `u` with word `[0x67E8AE4 + u*0x490] == 2`, word `[0x67E8CF8 + u*0x490] == 0` and signed stamp `0x52C2550[int [0x67E8B2C + u*0x490]] != generation`; otherwise 1.
- `0x119F90` (body 0x8C bytes, SHA-256 `CD5DAE0B455BA6CBFC20D0193849591A8250C710C8B77BBDCFFF6908FBD1544F`): 0 unless tribe word `+0x40 == 2` and `0 <= i < count`; otherwise the bit position of the `i`-th set bit in 625 words from `+0x60` (bits 0..15 each, `word*16 + bit`), scanning from the first word, and 0 when there are fewer set bits.
- So one `0x124740` call costs O(count x bit positions); `pf-flood-1` (`test_stresstest4.sav`): 96-221 calls per second, about 9 ms of simulation time per second, at most 2.4 ms per call.

## One-pass member check in BugfixesAndQoL [mod integration]

- `BugfixesAndQoL/src/UnitCommands/Native/TribeFloodMembership.cs` answers `0x124740` in one pass over the bitset with the same member order, the same unit-slot-0 cases and the same first failing member. It is used only when both bodies above match (`UnitCommandPathRuntime.exactFloodMembership`); BugfixesAndQoL's flood hook keeps its own bypass rule on top. DEVDIAG compares every call with Vanilla's original: 0 differences in `pf-flood-2`.
