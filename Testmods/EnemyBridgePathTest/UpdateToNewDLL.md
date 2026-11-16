# EnemyBridgePathTest: native addresses and update checks

Feature: lowered drawbridges in front of gatehouses count as raised for route planning of
every player the gate does not admit. Code: `src/DrawbridgeDeckRuntime.cs`.
Earlier passive-diagnosis documentation of this test mod is kept unchanged in `history/`.

Reference image: `CrusaderDE.dll` SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (installed build checked
2026-11-16). The whole feature is hash-bound: on any other hash `Install` throws, logs one
error and nothing is hooked or written (Vanilla behaviour stays intact). There is no pattern
fallback yet, because several data tables below have no independent second anchor. After a
game update re-audit every entry before changing the reference hash.

## Hooked entries (RedBird NativeX64, scheme Indirect, permanent, logically switchable)

| RVA | Function | Audited entry bytes | Displaced | Reason |
|---|---|---|---|---|
| `0xC4BF0` | temporarily close (1), open (2) or restore (0) all gates; `void(buildingManager, mode)` | `48896C2418 4889742420` | 10 | The deck is cut after mode 1 and restored before the next call. |
| `0xF4540` | per-tick portal refresh, runs every type handler; `void(pathManager)` | `48895C2408 57` | 6 | The deck-side endpoint region of the gate portal is redirected afterwards. |

Both full function bodies have no branch into the displaced span (checked with capstone on
2026-11-16). No other owner hooks these entries (Script Extender, Fixes clone, APIShared,
Pathfinding, BugfixesAndQoL, ExtraFeatures checked); a changed entry makes `Install` fail
closed. Each published hook is validated (scheme, displaced length, `FF 25` patch, pointer
slot) and never unpatched.

## Called native function

- `0xD8CE0` `void(pathManager, buildingId)`: rewrites the two gate aperture edge pairs from the
  gate state byte. Entry bytes `48895C240848896C2418` checked. Called twice per affected gate
  inside a closure window (state temporarily 0, then back to the closed value 2) to obtain
  Vanilla's own open-gate edge bits; the closed bytes are compared afterwards.

## Data addresses and layouts (image RVAs, no second anchor yet)

| RVA / offset | Meaning | Source |
|---|---|---|
| `0x64CCBB0` | building manager, record = base + id*0x32C (sentinel-relative, 1-based ids); count at +0x50 | loop bound in 0xC4BF0 |
| record +0x12C / +0x12E / +0x132 / +0x322 | alive state / type / owner / capturer | 0xC4BF0, 0xB9330, 0xE2610 |
| record +0x14A / +0x14C / +0x154 / +0x15E | tile X / Y / footprint size / sprite variation (gate axis `== 0x50`, bridge orientation 0/2/4/6) | 0xD8CE0, 0x645C0 |
| record +0x224 | ordered occupied tile ids (uint[25]) of a drawbridge, mapper order | 0x739C0 |
| record +0x2FE / +0x32E | gate state byte (2 = closed) / portal record id written by the gatehouse handler | 0xC4BF0, 0xD8040 |
| `0x60AD660` | path manager; portal record id at +0x2024 + id*0x204 (ids 1..[0]-1, max 199) | 0xF4540, 0xD8510, 0xE2610 |
| portal +0 / +4 / +0xC / +0x24 / +0x30 / +0x34 / +0x38 / +0x1E8 | state / class (3,4 gatehouses) / building / entry tile / exit tile / region A / region B / region C | 0xD7E90, 0xD8040, 0xE2610 |
| `0x51890D0` | direction byte per packed tile; bits 01 N, 02 NE, 04 E, 08 SE, 10 S, 20 SW, 40 W, 80 NW | 0xE49D0 flood |
| `0x50EC690` | PCL (region) per packed tile (ushort) | 0xE49D0 |
| `0x48F71B0` | tile flags (uint); `0x40000000` = raised drawbridge deck | 0x645C0 |
| `0x4B6AA50` | building id per tile (short) | 0xB9330 |
| `0x5F011A0` | moat task index per tile (ushort); nonzero decides which mapper cells a raise blocks | 0x69560 / 0x645C0 |
| `0x2D1A30` | drawbridge mapper, 25 ints per orientation/2 (15 deck cells) | 0x645C0 |
| `0x402FF2C` | packed row table, 12-byte entries, row start at +0 | 0xD8CE0, 0xD8040; checked at runtime against the Extender's `GetTileId` |

Domain constants are compared with Extender symbols at installation: `STRUCT_GATE_MAIN..WOOD`
must be 0x2D..0x2F (the selection 0xC4BF0 uses), `STRUCT_DRAWBRIDGE` 0x31, `AliveState.IsAlive` 2.

## Update procedure

1. Compare the new DLL hash; if different, re-audit 0xC4BF0, 0xD8CE0, 0xE49D0, 0xF4540, the
   gatehouse portal handlers (table at `0x3125D0`), 0xE2610/0xE2F60 and 0x645C0 against the
   layout above, including all callers of 0xC4BF0 (0x57330, 0xD95E0, 0x113BC0, 0x113E00,
   0x113F60, 0xE49D0) and that nothing writes the direction grid inside a closure window.
2. Update RVAs, entry bytes and the reference hash together; run `build.bat /nopause` and the
   in-game acceptance (`bridge-<save>` scenario, see `HANDOFF.md`).
