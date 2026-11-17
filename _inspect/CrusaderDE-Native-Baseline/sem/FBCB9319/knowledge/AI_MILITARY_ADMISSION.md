# AI military admission and recruitment accounting [Scope: Vanilla + integration]

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Recorded 2026-11-05 from the hash-matched baseline and current installed assemblies.
These are static/fixture observations, not real-game acceptance.

## Roles, identities and storage

Unit role (`GameUnit.r_AITribeRole`) is separate from tribe identity and from the
player's tribe storage index. Role zero permits automatic classification; `65535`
prevents that classification. Private isolation additionally requires a valid
single-member group and protection against other assignment calls.

Role writers/classifiers: `291F0`, `292F0`, `29360`, `29430`, `294B0`, `29520`.
Defense tables: `2CEE00`, `2CEE80`, `2CEF80`, `2CEFC8`. Resolve live defense through
`2BC40` (including the Fixes expanded-guarding owner), fallback `2DB60/2BF10`, and
harassment through `2B780`. Allocation `119D60`; membership assignment `11D370`.

| Native unit role | Meaning | Player storage |
| --- | --- | --- |
| 1 | Castle defense | Native class/position tables |
| 2 | Harassment | Native harassment classifier |
| 4 | Defense patrol | 170–179, configured count 1–10 |
| 5 | Home moat | 10, aggressive stance |
| 6 | Economy protection | 166 |
| 7 | Intervention | 167 |
| 10 | Siege engineers | 18 |
| 11 | Siege moat | 17 |
| 12 | Assassins | 11 |
| 13 | Diversion | 12 |
| 14 | Ladders | 13 |
| 15 | Tunnelers | 14 |
| 16 | Storm | 15–16, configured count 1–2 |
| 17 | Cover | 186–188 |
| 18 | Shock | 189 |
| 19 | Reserve | 190–191 |
| 20 | Wall | 192–199 |
| 22 | Harassment engineers | 165 |

Cover/reserve/wall nonpositive counts retain their base group. The wall type list
terminates at its first zero. The misleading Extender field `r_AILordMinusOne`
contains the one-based AIC identity: native readers subtract one before the `5E4`
stride. Player resources start at `379ADD0`; `379D0D0` is the corresponding field.

The native unit serialization record begins four bytes before the public GameUnit
pointer. A tribe's serialization record begins at manager-row `+28`; its public
GameTribe view begins at `+2A`. Copying a full serialization stride from a public
view would overwrite the next record.

## Orders and fresh-member execution

Group move: `11B520`; targeted group order: `11E960`; strategic dispatch: `122B40`;
single-unit route: `196280`. Group commands include unit attack 4, tile attack 5,
moat 6, building attack 9, special tile attack 22 and stop 31. Command 22 must retain
its distinct terminal setup. A route result zero is failure even if scratch fields
changed; result one can legitimately have zero route length when already arrived.

Strategic lists belong to their current owner/target context and have reservations.
Do not reset/rebuild lists (`111060/111330`) to admit one recruit or copy another
member's task/path. `10DF60` overwrites global task ownership. Shock global-list
selection must therefore have a completed observed owner/target pair.
Current siege phase, AIC identity, target Lord global identity, attempt and group
storage/role identity constrain retained strategic intent. Phase transitions are
not evidence that an old per-unit task is still valid.

Native mapper `93750` creates mobile machines without engineers. Full crew sizes
are catapult/Arabian ballista 2, tower/ram 4 and shield 1. Builder entries are
`A65A0`, `A8E40`, `A93C0`, `A9A30`, `AA030`, `AA630`; trebuchet construction uses
three engineers. Construction inheritance must account for every contributor,
not just the final attached crew or native role-21 classification's first engineer.

## Quota counts versus real strength

`1835F0` starts the natural fold; `183780` is its role-10 contribution boundary.
Recruitment consumes a prior completed fold, while readiness/strength and physical
crew/build guards consume real native counts. Additional troops must be excluded
at recruitment comparisons only, with a matching fold identity. Never rewrite
the shared count vector or subtract them globally.

The explicit interception contracts and instruction-complete byte spans are in
APIShared `AiQuotaNativeContract`. Its executable harness checks the generated
stubs against the canonical DLL, including live flags, registers, stack, inactive
branches, signed comparison bounds, role-22 global identity and demand-free folds.

Role 22 has two distinct limits: `2ECD0` requires its real physical population
below 20 before both recruitment and building, whereas `14AED0` retires only above
20. Thus merely permitting an additional engineer to reach exactly 20 can stall
building. Admission must count alive/initializing role-22 engineers and reserved
role-22 peasants, reserve any missing regular two-engineer quota, and remain below
20. Quota correction does not change either physical native boundary.

## Raw restore admission

Raw restore `87D0` returns int success 1. Its three callers have verified ordinary
failure branches on zero, before AI execution:

- Save caller `853F0`: call `85496`, test `8549B`, failure `85B4D`, return `85B74`.
- Map caller `94350`: call `94B15`, test `94B1A`, failure `96EBE`, return `96F1F`.
- Map caller `100530`: call `1006D7`, test `1006DC`, failure `102550`, return `10256A`.

Those zero branches contain only failure/epilogue work, not `71670`, `CDE60` or AI
initialization. The APIShared raw-restore participant uses this failure contract
when an authored army cannot be protected before initialization. All declarations
must be quarantined before detaching any member, because an Extender removal Pre
can veto one operation. Returning success after such a veto would let a partially
protected world execute. The failure is sticky through exceptional wrapper retries.

The ordinary map-header CRC is insufficient proof for appended simulation records.
Multiplayer map admission therefore also compares map-scoped simulation records
before GO and binds each peer's whole local file through native load. Save and map
preparations use distinct Extender packet types: registering two channels with the
same packet type is not supported even when their channel IDs differ.

The matching managed `Platform_Multiplayer.StartGame` calls
`EngineInterface.loadMultiplayerMap` (which forwards to private `loadMap`), then
calls `StartMultiplayerGame`/`postLoading` without testing the returned `errorCode`.
Returning only an error structure from a late admission gate is therefore
insufficient. A refused file ticket/protection transaction must unwind the entire
managed start at its outer boundary before those later native calls. Single-player
`EditorDirector.loadSaveGame` and `loadMapIntoEditor` do check success, but a shared
protection failure still needs a consistent failed lifecycle result.

The mapper's newly created unit is assigned at `118CA0` before its first settled
tick. Suppress that assignment at the final effective Original boundary for captured
AI-owned military units, including mutable Pre overrides. A mutable creation Post
can redirect its return to an unrelated unit: keep an invocation-local actual
Original result and refuse that substitution within the primary capture. Preserve
unrelated helper creation and ordinary human/civilian placement.

## Runtime trampoline encoding observation

The first real startup on 2026-11-05 exposed a validation error, before any military
admission. Installed `RedBird.Backends.NativeX64.dll` SHA-256
`0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`
uses Iced BlockEncoder over decoded displaced instructions plus the resume jump.
It does not copy raw prologue bytes verbatim. In builder entries `A65A0/A8E40`,
seven displaced source bytes become six relocated prologue bytes; `A93C0` has six
source bytes and five relocated bytes. The redundant `40` REX prefix before
`PUSH RBX` is removed. The other three builder prologues retain six bytes.

The resume target remains original entry plus source displacement length, never
entry plus relocated length. Runtime validation now compares the complete exact
reencoded trampoline and its emitted size, alongside unchanged canonical source,
live entry, padding, scheme, pointer-slot and gate checks. The native fixture uses
the same production validator and rejects deliberately corrupted original bytes,
prologue and resume jump. This observation validates the encoding contract; the
subsequent successful game acceptance must be recorded separately.

## Controller handoff of joined recruits

Static audit 2026-11-17 on the same native hash (Ghidra pseudocode and Xrefs; function
labels remain `candidate`, data flow confirmed-static, not yet real-game measured).

- Every Vanilla recruit classifier writes only the unit role (`GameUnit+0x426`) and the
  membership through `11D370`: `291F0` (role 1, group via `2BC40` or fallback `2DB60/2BF10`),
  `292F0` (2), `29360` (5), `29430` (4), `294B0` (generic role argument, callers `304B0`,
  `30550`, `40740`, `118B30`), and the role-6/7 regrouping `303E0/30310` (`123EA0` + `11D370`).
  None of them issues a member or group order.
- `57330 -> 2AE40` runs the owner AI passes (rotating counters `+2D078/+2D07C/+2D080`; exact
  per-player frequency not derived statically). `2FE20` (every pass of an AIC player) calls
  `3D9E0/3DEE0/3DB30/3DD60` for every populated castle-defense storage group, which reach
  `3E850` (AIV tile, reachability `3E650`), `3E940` (keep +3/+4) or `3E9A0` (stored fallback)
  and thus `11DD10`. `3EA50` re-targets each patrol group every `defense_patrol_delay` passes
  (AIC `+0x180`), gated by the group counters `+0x614/+0x61A/+0x616 < 11`. `3E700` (home moat)
  issues command 6 through `11E960` when `1245B0` marks no member, else `3E940`.
  `3E070`/`3D840` (economy protection/intervention, every pass) clear all marks (`126020`) and
  send `11B520` to the threat target, else `3E940`; below the AIC strength (`+0x150`, resp.
  the `+0x158` field) they issue nothing.
- `11DD10` marks members within 5 tiles, with a live unit/building target, in AI state `0x6A`,
  of types 39/58/59/60/77 (when its fifth argument is 0) or with `+0x42A` set; `+0x42A` is
  written only by `2EAC0`. It calls `11B520(...,0,0,0)` only if a member stays unmarked, and
  `11B520` skips marked members. A far joined recruit is therefore moved on the next pass
  while members at their post receive no write.
- Consequence [mod integration]: APIShared admits a recruit of roles 1/4/5/6/7 whose
  existing group has no observed order with membership and role only, like a Vanilla
  recruit. Harassment (`3E200`: cached building target, combat/interval gates) and siege
  roles were not shown to recommand joined members and keep requiring an observed order.

## Castle-defense recruit allocation (role 1)

Static audit 2026-11-17 on the same native hash (Capstone disassembly of the installed DLL plus
Ghidra pseudocode; data flow confirmed-static, function labels `candidate`, not real-game measured).

- `29520` (unclassified AI military pass) and `40740` (recruitment) call `291F0(AIC base, unit)`.
  `291F0` computes the class with `2D760(AIC base, unit, flag)`; flag = player `+0x3864 == 1` and
  signed word `+0x386E > 0`. `2D760` returns 13 if the flag is set, the AIC identity is not 7,
  the class-13 position count (`379EC90`) is positive and the type is in {22,23,70,72,76,81,82};
  otherwise the index of the type in `2CEE00` (30 entries), else 0. Spearman 24 is class 8, maceman
  26 class 10, Arabian horse archer 74 class 17, Arabian slinger 72 class 14.
- Position count `379EC5C[player*0x160F + class] > 0` selects `2BC40(AIC base, player, class)`;
  otherwise `2BF10(AIC base, player, 2DB60(unit) == 0)`. `2DB60` returns 1 for types in `2CEF40`
  {22,23,25,27,70,76,75,82,84,81,80,79,85}, so those use storage slot 0 and all other types slot 1.
  `2BF10` returns the tribe ID in EAX on both paths (stored slot ID or the `119D60` result), although
  Ghidra types it `void`. `291F0` then calls `11D370(tribe manager, unit, returned ID)` with no check.
- `2BC40` returns 0 if the AIC identity is 0, if the effective window is not positive, or if every
  window slot is current with at least 1000 members. The window is the position count, replaced by
  AIC `max_mobile_groups` (`+0x118`) only for classes 8, 10 and 17 and only when the count exceeds it
  (signed compare). The first empty or stale (ID 0 or global mismatch) slot is allocated through
  `119D60`; otherwise the slot with the fewest members wins. Stale means only an ID/global mismatch:
  a record with matching global but state 3 is returned unchanged.
- `11D370` does not test the tribe state. `123EA0` sets state 3 when the last member leaves;
  `1261F0` (called in `CDE60` after the AI passes `57330`) clears every state-3 record with `118C70`
  regardless of members, and also clears empty live records unless `+0x2A` is non-zero and at least
  `DAT_183665F58`. A recruit assigned to a state-3 tribe is therefore orphaned at the next `1261F0`.
- Position counts and tiles (`379E7AC`, 10 per class) are rebuilt in `53D00` from the AIV misc items
  (misc item type = defensive class). Classes 9, 11 and 18 (mask `0x40A00`) stay zero unless the
  per-player flag `8D403C` is set.
- Controller (`2FE20`, every AIC pass): storage slot 0 -> `3E6C0` -> `3E940` (keep origin +3/+4);
  storage slot 1 -> `3EA10` -> `3E9A0` (stored position `+0x1D8/+0x1DC` when `+0x1D4 > 0`, sets tribe
  `+0x634 = 2`). Class windows: wall `3D9E0` (`2CF020`: 6,7,14,16,19,26,25), castle `3DEE0` (`2CF060`:
  9,11,12,15,18,13,23,24,27), mobile `3DB30` (`2CF0A8`: 8,10,17,22,28,29; window clamped to
  `max_mobile_groups`, position ordinal `(+0x2B54 + i*step) % count`, threat branch `3E9A0`).
  `3E850` resolves the populated ordinal, tests leader-tile to target reachability with `3E650`
  (`E2610`), and uses `11DD10` or, if unreachable, `3E940`.
- `196280` returns 0 for an off-map/invalid target, a target with tile flags `0x30`, a wall/tower
  target (`0x10000100`) for a type whose `322540` entry is 0, unreachable components and failed path
  construction. Result 1 includes "already there".

### Admission consequences [mod integration]

- APIShared `ResolveDefense` mirrors `291F0` except that it rejects a returned ID whose record is not
  live/owned (`TryGetGroup`); Vanilla would assign into it. With `max_mobile_groups <= 0` and a positive
  class-8/10/17 count, `2BC40` returns 0 deterministically.
- The empty-group initial order sends every non-window group to keep +3/+4, which matches slot 0 but
  not slot 1 (`3E9A0` position). In the 2026-11-17 run, slot-1 members later stood near 229,431 while
  slot-0 members stayed near keep +3/+4 (238,430). It also omits the `3E650` reachability fallback.
