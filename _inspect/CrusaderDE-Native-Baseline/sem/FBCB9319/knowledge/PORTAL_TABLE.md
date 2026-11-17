# Portal table in the path manager: id bound, allocation and per-tick refresh [Scope: Vanilla]

Audit observation: 2026-11-16. Native canonical installed DLL SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. RVAs are evidence
for this image only. Evidence: semantic-database pseudocode of `0xF4540`, `0xD8510`,
`0x1950B0` and `0xDE090`; capstone disassembly of the installed PE for the type
handlers, the jump table, `0x71895`, `0x74080`/`0x74240` and the call order in `0xCDE60`;
a linear `.text` sweep for stores to the table header. No hook contract is defined here.

## Layout (confirmed)

Base `0x60AD660` (path manager), viewed as `int*`. Entry `id` (1..199) starts at byte
`id * 0x204`; int indices below are relative to the base.

| Byte offset in entry | Int index | Meaning |
|---|---|---|
| `+0x2024` | `id*0x81 + 0x809` | state, 0 = free slot |
| `+0x2028` | `+0x80A` | type (jump-table index) |
| `+0x202C` | `+0x80B` | serial, global counter at allocation |
| `+0x2030` | `+0x80C` | building id (0 for unit-based entries) |
| `+0x2034` | `+0x80D` | unit id (unit-based entries) |
| `+0x2038` | `+0x80E` | identity snapshot of the building or unit |
| `+0x203C` | `+0x80F` | enabled |
| `+0x2058`, `+0x205C` | `+0x816`, `+0x817` | region (PCL) at the two access tiles |
| `+0x2208` | `+0x882` | owner player |
| `+0x220C` | `+0x883` | region (PCL) at the centre tile |

The header int `[0]` is **not an entry count**. It is the highest used id + 1, or 0 when
no entry is used.

## Writers

- `0xF4540` (confirmed): the only per-tick writer of `[0]`. It sets `[0] = 0`, then for
  each id 1..199 with state != 0 sets `[0] = id + 1`, runs the type handler from the
  table at `0x3125D0` and `0xF4760`, and wipes the entry with `0xDE090` when the handler
  cleared the state. `[0]` is written before the handler runs, so a slot freed in this
  pass still counts until the next pass.
- `0x71895` in the start/load normalization `0x71670` (confirmed): `[0] = 200`; the same
  function calls `0xF4540` at `0x71B7B`, which shrinks `[0]` again.
- `0xD8510` (confirmed): allocates the **first free** slot 1..199 (returns 0 when full).
  It writes state 1, type, serial, building or unit, enabled 0 and the identity/owner
  snapshot. It writes neither `[0]` nor the region fields.
  - `0x74080` and `0x74240` call it for gatehouses (types 4 and 3; confirmed 2026-11-17: type 3
    is the large 7x7 gatehouse, type 4 the small 5x5 one, see the endpoint section). Both are reached
    from building placement `0x6D580`, synchronously at placement time.
  - `0x1950B0` (from `0x14A3F0`) calls it with type 1, building 0 and a unit id. This is
    the unit-based entry; it is almost certainly the siege ladder (probable).
- Type handlers from `0x3125D0` (confirmed): type 1 `0xD7D50`, type 3 `0xD7E90`, type 4
  `0xD8040`, type 5 `0xD81F0`; the other slots point to `ret` stubs. The gatehouse
  handlers rewrite owner, enabled, endpoints and all three region fields every pass, write
  the entry id back into the building (`0x64CCEDE`), and clear the state when the identity
  snapshot no longer matches the building record (`0x64CCCE4`). The ladder handler clears
  the state when the unit identity or state no longer matches.
- `0xDE090` (confirmed): clears one whole 0x204-byte entry. Its callers besides `0xF4540`
  (load migration `0xE32F0`, construction `0xE62F0`, failed allocations in `0xD8510`) come
  from the xref list and were not re-read individually.
- Region rebuild `0xE49D0` does not touch entries; renumbered regions reach them only
  through the next `0xF4540` pass.

## Timing and consequences

- `0xCDE60` (game tick from `DLL_RunTick` `0x86680`) calls the conditional region
  rebuild `0xE49D0` at `0xCE0BD` and then `0xF4540` at `0xCE1A8`, once per simulation tick
  (confirmed). AI and unit updates follow in the same function.
- A new gatehouse or ladder entry exists immediately after placement; `[0]` and the region
  fields reflect it after the next `0xF4540` pass.
- Destroyed or replaced gates are not freed explicitly: the handler notices the identity
  mismatch in the next pass, the entry is wiped, and `[0]` drops one pass later and only
  if it was the top slot.
- Live entries are never renumbered or compacted, but freed slots are reused, so an id
  can denote a different building later (serial or building + identity distinguish them).
- `[0]` changes only when the top slot changes; slots below it can be allocated, freed or
  reused without any change of `[0]`. Walls, terrain and region rebuilds never change `[0]`.
- The macro region query `0xE2610` reads the same range (ids 1..`[0]`-1) and fields
  `0x809`, `0x80A`, `0x80C`, `0x80F`, `0x882` plus the gate open/captured short at
  `0x64CCED2 + building * 0x32C`.

## Open points

- Answered 2026-11-17: `0x64CCCE4` is building `+0x134` (GlobalId); `0xD8040` compares it with
  portal `+0x2038` and clears the state on mismatch. Still open: when demolition changes it.
- Whether the table is serialized in saves (load forces `[0] = 200` and then normalizes it,
  which suggests the entries survive loading).
- Whether a player chore placing a gatehouse runs before or after `0xF4540` in the same
  tick (probable before; chore dispatch inside `0xCDE60` was not confirmed).
- Creators of types 5 and 7 were not found in this image (their handlers exist).

## Gatehouse endpoints and the hooked refresh entry (2026-11-17)

Static evidence on the installed DLL (capstone), runtime use in the drawbridge acceptance runs
under `.work/t/bridge-accept/` (dated observations).

- Field offsets below are absolute from the path manager `0x60AD660`; "relative" is from the
  record base `+0x2024 + id*0x204`.
  - Entry tile `+0x2048` (relative `+0x24`), exit tile `+0x2054` (relative `+0x30`): confirmed
    (`0xD8040` at `0xD8153`/`0xD8177`/`0xD8190`/`0xD81B2`, used by every accepted closure run).
  - Region A `+0x2058` is the PCL of the entry tile, region B `+0x205C` the PCL of the exit tile:
    confirmed.
  - Entry X/Y `+0x2040`/`+0x2044`, exit X/Y `+0x204C`/`+0x2050` and axis selector `+0x21F4`
    (2 or 0: row or column layout): probable (single static reading of `0xD8040`).
  - Region C `+0x220C` = PCL of footprint tile (X+1, Y+1) for both gatehouse types: confirmed
    (2026-11-17, the last stores of `0xD7E90` at `0xD802C` and `0xD8040` at `0xD81DC` read
    `[rowTable(Y+1) + X] * 2 + 0x50EC692`). In `test_gates_bridge3.sav` the whole 5x5 gatehouse
    floor of gate 600 was one region, so C equals the corridor region there.
- Type 4 handler `0xD8040` places the endpoints at X+5/X-1 or Y+5/Y-1 around the footprint
  centre +2, i.e. the small 5x5 gatehouse; type 3 `0xD7E90` is the large 7x7 one (confirmed;
  the Extender names them `PathConnectionClass.GatehouseSmall = 4` / `GatehouseBig = 3`).
- The endpoints lie exactly on the outside tiles of the gate aperture edges written by
  `0xD8CE0` (see DRAWBRIDGE_REGION_CLOSURE.md).
- Hook contract of the per-tick refresh `0xF4540(pathManager)`: entry bytes
  `48 89 5C 24 08 57` (NativeX64 Indirect displaces 6 bytes); no branch of the full function
  body targets the displaced span (capstone, 2026-11-16). Mod hook owners are recorded in
  DRAWBRIDGE_REGION_CLOSURE.md (integration section), not here.

## Region rebuild outside the tick position (2026-11-17)

- `0xE49D0` is called from `0x15200`, `0x5BC70`, `0x71670` (load normalization), `0x76780`,
  `0x87460`, `0x87D10`, `0xCDE60` (the simulation tick, at `0xCE0BD`, before `0xF4540` at
  `0xCE1A8`), `0xE1110` and `0xE1640` (path reconstruction repair) (baseline callers query).
  Its countdown/force logic (`DAT_1837ed4cc`, 200 calls; force flag `+0x6C`) decides whether a
  call really rebuilds; the flood stops at region id 999.
- Consequence (probable, observed once): a rebuild that runs after `0xF4540` in the same tick
  renumbers regions while the portal region fields keep the old numbers until the next
  `0xF4540`. During that window region queries through the portals use stale ids (all
  portals, Vanilla). Observed 2026-11-17 in an acceptance pause: gate 600 portal C = 91 while
  the footprint tile and the corridor were region 92; the next runs showed C = 92.
