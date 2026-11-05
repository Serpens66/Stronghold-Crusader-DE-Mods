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
