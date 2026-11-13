# Group-move member exclusion mark `GameUnit+0x428` [Scope: Vanilla + mod integration]

Observed 2026-11-09; installed native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Confirmed-static data flow from the complete Ghidra Xref export and decompilation, plus
in-game log correlation (acceptance scenario `SP-relay.json`). Function names below are
RVAs; their semantic labels in the database remain `candidate`.

## Field and readers

- Typed unit offset `+0x428` (Script Extender 2.14.3 `GameUnit.N000000D8`, `ushort`; older
  headers spell it as a uint32). Raw address of slot `i`: `0x1867E8E84 + i*0x490`.
- The raw Xref export lists 20 references to `0x1867E8E84`, in exactly four functions:
  `0x11B520`, `0x11DD10`, `0x1245B0` and `0x126020`. No other function references it.
- Only reader: the group move `0x11B520`. Each of its three member loops (target spread,
  order write with AI state `0x65`, final clear) admits a member only if this field is 0,
  next to the other admission tests (see `FIRE_EFFECTS.md`). A skipped member receives no
  write and keeps its previous order and AI state; `0x11B520` still returns 1.

## Writers

- `0x11DD10` (AI group move with per-member preselection): for every living member
  (`AliveState 2`, `+0x29C == 0`) it first writes 0, then 1 when `+0xAC != 0`, selection
  is not allowed (`+0x2A0 == 0`), the member is less than 5 tiles from the target
  (`0x79C0` distance), it is bound to certain targets or buildings (`+0x342`, `+0x340`),
  it is in AI state `0x6A`, it is of type 39, 58, 59, 60 or 77 while `param_5` is 0, or
  `+0x42A != 0` while (`+0x384 == 0` or byte `+0x3C0 != 0`). If at least one member stays at 0
  it calls `0x11B520(...,0,0,0)` and returns 1. The marks stay behind afterwards.
- `0x1245B0` (AI precheck): for every living member writes 0, then 1 when `+0xAC != 0` or
  `+0x2A0 == 0`; returns whether any member was marked. Its caller `0x3E700` issues
  target command 6 only when no member was marked.
- `0x126020`: writes 0 for every living member (`AliveState 2`, `+0x29C == 0`).
- Callers: `0x11DD10` from AI routines `0x30620`, `0x3E200`, `0x3E850`, `0x3E940`,
  `0x3E9A0`, `0x3EA50`; `0x1245B0` from `0x3E700`; `0x126020` from `0x3CF20`, `0x3D840`,
  `0x3DD60`, `0x3E070`. `0x3D840` and `0x3E070` call `0x126020` immediately before their
  plain `0x11B520` call. The inspected callers `0x3E200`, `0x3EA50` and `0x3E700` select their tribes from the
  per-AI role tables (`0x583C` records), checked against the tribe global ID.

## Consequences

- The field is AI bookkeeping for the AI's own next group order. Player orders (Chore 17,
  `0x10AE0 -> 0x196100 -> 0x11B520`) never clear it, so they rely on human units never
  carrying a mark. Statically only AI routines set it; not confirmed in game for human units.
- Tribe creation `0x119C00`, `0x11D370` (assign) and `0x123EA0` (unassign) leave the field
  unchanged. A unit moved out of an AI group keeps the mark of that group's last AI order,
  and any later `0x11B520` order skips it until an AI writer runs on its current group.
- `0x11E960` reaches `0x11B520` only in cases 6 and 7; case 5 (Attack Here), `0x1E`
  (disband) and `0x1F` (stop) neither read nor write this field.

## Runtime evidence [mod integration]

`SP-relay.json` spawns 150 archers for an AI ally. Within 0.3 s the AI spreads them
round-robin over three or four of its own groups. In four runs on 2026-11-05 and one on
2026-11-09 a relay Move of 138 of them skipped exactly one AI group's share (46 of three
groups, 33 of four): every skipped unit had `+0x428 == 1`, AI state 11 and met every other
admission test, while ordered units had 0 and AI state `0x65`. Runs with the same group layout
also passed; the outcome depends on that AI group's last order.

After the relay began clearing the field (below), four runs without peace time at game speed 300
(2026-11-09) cleared 125, 43, 98 and 120 AI marks before the 138-unit order, and all 138 units received
the target each time. With peace time the AI did not take the spawned archers into its groups at all
(no mark in four runs), and with only one, allied AI it did not either (three runs).

## PlayerControlRelayTest [mod integration]

For Move and Patrol, `RelayWorld.Execute` writes 0 to `N000000D8` for exactly the planned
units after regrouping and before the public Extender group call, inside the authenticated
Chore on every peer. This is `0x126020`'s effect restricted to the ordered units. Contract
and update checks: `Testmods/PlayerControlRelayTest/UpdateToNewDLL.md`. Other workspace
callers that order AI-owned groups through the public move API can meet the same mark.

## Spawn, deletion and slot reuse

Static audit 2026-11-09, same native hash (decompilation and Xref export). A unit created in a
reused slot does not inherit the previous occupant's mark:

- Unit spawn `0x17FEF0` (Extender `c_game_unit_spawn_ex`) takes the first slot from 1 whose
  AliveState (`+0x88`, manager `+0x6E4`) is 0 and then writes single fields. Neither it nor
  `0x19A240`, `0x180230`, `0x18BE50` and `0x1909A0` writes `+0x428` (manager-relative `0xA84`) or
  copies a whole record.
- Native code writes AliveState only as 1 (spawn), 2, 3, 4 or 5, never 0. A slot becomes free only
  through `0x1869C0`, which clears the whole `0x490`-byte record from manager `+0x658 + slot*0x490`
  (`0x7490` is a memset); the public record starts at `+0x65C`, so `+0x428` is included. Callers:
  `0x6D580`, `0x6F230`, `0x8AE80`, `0x94350`, `0x100530`, `0x182B00` and the reset of all slots
  `0x194FE0`.
- The other whole-record copies (`0x196280`, `0x197050`, via `0x7140`) write the scratch slot 0,
  which is no unit ID; `0xB520` is the generic memory copy. Pointer-relative writes of 0 at `+0x88`
  found in the decompilation belong to effect (`0xE8`) and building records.

A freshly spawned unit therefore starts with mark 0; only an AI order routine of a group it belongs
to can set it. Save loading restores the field like any other unit field. Not checked in game.

## Pathfinding fire escape [mod integration]

`Pathfinding` `TryMoveSoldier` (fire escape, solo replan) clears the mark of the one soldier it orders
through APIShared `Commands.GroupMoveExclusion` right before its `0x11B520` group move.
`ResumeReplanGroup`, which repeats an AI order on the AI's own group, keeps the marks. Acceptance
`SP-fire-escape.json` (2026-11-09, peace time, speed 300, runs 012 to 014): six fire rounds with 5, 4, 6,
8, 5 and 3 marked AI soldiers seen on burning tiles; all 31 were ordered (mark cleared) and received a
fire-free move target, and a marked control group outside the fire kept its marks.

RandomEvents bandits keep no group between spawn and activation: `ActivatePendingBanditGroup` rejects a
unit with a tribe or tribe leader before it creates their group and orders it. OutpostTest orders its
own group right after adding freshly spawned units; the AI never orders a tribe it did not create
(`FIRE_EFFECTS.md`). With mark 0 at spawn, neither path meets a stale mark; no clearing was added.
