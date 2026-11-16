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
  - `0x74080` and `0x74240` call it for gatehouses (types 4 and 3; which one is the large
    or small gatehouse is inferred from the endpoint offsets, probable). Both are reached
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

- Exact meaning of the building identity field at `0x64CCCE4` and when demolition changes
  it; the destruction path itself was not traced.
- Whether the table is serialized in saves (load forces `[0] = 200` and then normalizes it,
  which suggests the entries survive loading).
- Whether a player chore placing a gatehouse runs before or after `0xF4540` in the same
  tick (probable before; chore dispatch inside `0xCDE60` was not confirmed).
- Creators of types 5 and 7 were not found in this image (their handlers exist).
