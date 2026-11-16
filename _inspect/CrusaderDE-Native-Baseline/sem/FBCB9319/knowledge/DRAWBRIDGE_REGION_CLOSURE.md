# Drawbridges in Vanilla region topology and the planning closure [Scope: Vanilla + mod integration]

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Date: 2026-11-16. Static evidence: semantic pseudocode and capstone disassembly of the installed
DLL. Runtime evidence: automated runs of `Testmods/EnemyBridgePathTest` with
`test_gates_bridge.sav`, `test_gates_bridge2.sav` and `test_stresstest5.sav`, evidence under
`.work/t/bridge-accept/` (dated observations, logs are not pinned).

## Temporary gate closure (confirmed) [Vanilla facts]

- `0xC4BF0(buildingManager 0x64CCBB0, mode)` walks building ids `1 .. [manager+0x50]-1`, selects
  alive (`+0x12C != 0`) types `0x2D..0x2F` and per gate:
  - mode 1 saves the live state `+0x2FE` into `+0x128` and sets state 2 (closed);
  - mode 2 saves the state and sets it to 0 (open);
  - mode 0 restores the saved state.
  - Each mode writes the portal record's `enabled` (`+0x203C`, record id from building `+0x32E`)
    as `state != 2` and calls `0xD8CE0(pathManager, gate)`.
- Callers and modes:
  - `0xE49D0` (component flood): mode 1, then 0.
  - `0xD95E0` (AI siege distance field): 1, then 0.
  - `0x113BC0`, `0x113E00`, `0x113F60` (AI tactical position searches through `0xEE980`/`0xEEC80`): 1, then 0.
  - `0x57330`: mode 2, then 0.
  - Inside these windows the callers only read the direction grid `0x51890D0`.
- `0xD8CE0` toggles exactly two edge pairs per gate, outside tile ↔ footprint edge tile:
  - Column `X + size/2` when building `+0x15E != 0x50`: tiles `(c, Y-1)/(c, Y)` and `(c, Y+size)/(c, Y+size-1)`.
  - Otherwise row `Y + size/2`: tiles `(X-1, r)/(X, r)` and `(X+size, r)/(X+size-1, r)`.
  - An edge pair is closed for state 2 or when surface or height checks fail.
- Gatehouse portal handlers `0xD7E90` (class 3, 7x7) and `0xD8040` (class 4, 5x5) come from the
  table at `0x3125D0` and are called every tick by `0xF4540`.
  - They place the endpoints exactly on those outside tiles.
  - They rewrite owner, enabled, the endpoints and the three region fields every pass:
    `+0x2058` (A = PCL of entry tile), `+0x205C` (B), `+0x220C` (C, an inner gate tile).
- The only readers of the region fields are the region queries `0xE2610` and `0xE2F60`.
  - They treat A, B and C of an eligible record as mutually connected.
  - A record is eligible when it is active, enabled, and either player 0 is queried, the querying
    player is in the owner's alliance group, or the gate is captured (`+0x322 != 0`).

## Why lowered drawbridges break planning [Vanilla facts]

- Raising a bridge (`0x645C0`) blocks only the 15 mapper cells that have a moat task index. The
  5x5 footprint usually keeps a land abutment row on each end, and the gate endpoint lies on the
  abutment, not on the deck.
- The flood closes all gates but leaves lowered decks open, so the deck joins the outside region.
  - If the deck touches land beside the gatehouse, it can join the castle interior to the outside.
  - Observed with Vanilla behaviour in `test_gates_bridge2.sav`: the interior region of player 1
    became region 1, the whole outside.
- Consequences:
  - Region queries accept hostile routes onto the deck or into the castle.
  - `0x11B520` group moves and Pathfinding's anchor routes then compute real tile paths across
    the deck (observed: 6 of 6 hostile `MoveHere` orders planned over the deck).
  - AI melee units walk onto lowered enemy bridges (observed: 11 distinct units in 7 500 ticks
    with gates held open).

## Planning closure in EnemyBridgePathTest [mod integration]

- Detour on `0xC4BF0`: after every mode-1 call, lowered decks in front of a gatehouse portal are
  cut from all non-deck neighbours.
  - The near abutment edge and the gate aperture edge are re-added. The aperture edge uses
    `0xD8CE0` evaluated with the gate state temporarily 0.
  - Every changed byte is restored before the next `0xC4BF0` call.
- Detour on `0xF4540`: when the endpoint tile shares the gate tile's region, the endpoint's region
  field is pointed at the region beyond the bridge footprint.
- Result:
  - Only players the gate admits reach the deck and the far side through the portal.
  - Everyone else is treated as if the bridge were raised.
- Measured cost:
  - About 27 µs per closure window with bridges and about 4 µs per portal tick.
  - On the 800-map with about 10 000 units this was roughly 0.1 % of run time.
  - The ticks per second difference between modes followed the number of surviving units, not
    Vanilla planning time; the closure windows and region query counts were comparable.
- Contracts and update checks: `Testmods/EnemyBridgePathTest/UpdateToNewDLL.md`.
