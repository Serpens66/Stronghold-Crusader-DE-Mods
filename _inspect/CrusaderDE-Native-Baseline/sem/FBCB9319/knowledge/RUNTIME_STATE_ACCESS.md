# Live state access: manager globals, record layouts and read consistency [Scope: Vanilla + mod integration]

Observation date: 2026-11-14. Native canonical installed DLL SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, installed Script
Extender 2.14.4.0. RVAs below are evidence for this image only. Verification path: run
`Shared\Tools\GameMemReader` (`globals`, `layout`, `status`, `units`) against the installed game.

Evidence came from two kinds of runs:
- A read-only external pass during another session's test, on the map `4spielerkarte`, about
  400 units and 210 buildings.
- Two of our own runs with `test_dustynoattack.sav` (map `AI - Brother's Strife`, about
  1970 units and 535 buildings), with `Testmods/GameStateProbe` and `AutoLoadSave` installed
  and `RandomEvents` removed.

## Manager globals and ID bases [Vanilla facts]

The globals were resolved with the Script Extender's own AOB patterns
(`GameGlobalsManager.cs`). The patterns were scanned in the unmodified DLL file and the first
instruction was decoded with Iced:
- 103 of 103 patterns resolve. 66 are RIP-relative and 37 are plain `[reg+disp]` displacements.
- The patterns for `GameTileManagerVA` and `GameGatehouseManagerVA` are identical apart from a
  stray escaped quote, so both resolve to the same address, `0x405EDB0`. This is a property of
  the Script Extender's patterns, not a Vanilla fact.
- `GlobalImprovedSiegingBehaviourVA` matches twice. The first match is used.
- At runtime, `module base + RVA` equals the absolute VA the Extender logged for unit, building
  and player managers, game tick, local player and tile manager (module base `0x7FFC75810000`
  in that run).

| Global | RVA | Kind | Runtime use confirmed |
|---|---|---|---|
| Unit manager (`GameUnitManager`) | `0x67E8400` | rip | Yes. Unit records decode to plausible type, owner, position and health. Same as the code anchor in [MOVE_STAGER_DATA_ANCHORS.md](MOVE_STAGER_DATA_ANCHORS.md). |
| Building manager (`GameBuildingManager`) | `0x64CCBB0` | rip | Yes. Keep and keep doors of player 1 decode as building IDs 1–3. |
| Player manager | `0x366C210` | rip | Yes. Player resource records are at `+0x1343FC`, stride `0x583C` (22588), 1-based player IDs 1–8. |
| Tribe manager | `0x7CC6720` | rip | Matches the code anchor in MOVE_STAGER_DATA_ANCHORS.md. Not read at runtime. |
| Tile manager | `0x405EDB0` | rip | Yes. Packed grids are at the Extender's `GameTileManagerView` offsets. |
| Map row lookup table | `0x402FF2C` | disp, module-relative | Read. Row starts for y=400/401/402 were 160400/161199/161996. |
| Game tick | `0x3665F58` | rip | Yes. Increases monotonically while the game runs. |

Record layout, confirmed at runtime with the stated record sizes:
- **Units:** `GameUnitManager+0x65C` is the start of the unit array; slot 0 is reserved and
  unit ID *n* is at `+0x65C + n × 0x490`. The capacity is 10,000.
  - `r_AliveState` (`+0x88`, Int16) was `IsAlive` (2) for every non-empty slot.
  - The other confirmed fields are `r_UnitChimp` `+0x8A`, `r_GlobalId` `+0x94`, current tile
    X/Y `+0xC0`/`+0xC2`, `r_CurrentHealthPercentage` `+0x2D0` and `r_TribeId` `+0x2D4`.
- **Buildings:** `GameBuildingManager+0x5C` is the start of the building array; building ID *n*
  is at `+0x5C + n × 0x32C` (812 bytes). The capacity is 4,000.
  - The confirmed fields are owner `r_PlayerIdOwner` `+0xD6`, `r_BuildingType` and the origin
    tile `+0xEE`/`+0xF0`.
- **Players:** `GamePlayerResources` is 22,588 bytes. Popularity, peasant counts and total goods
  are plausible for the local player.

## Counters change during a simulation tick [Vanilla facts]

These are single observations, confidence `probable`:
- Read from outside in the middle of a tick, `GameUnitManager.r_TotalUnits` (`+0x4`) was 521.
- In the same second, read from inside the tick callback, it was 1971 (`r_NextUnitId` 1982).
- A full scan of `r_AliveState != 0` found 1972 units both from outside and from inside.

So Vanilla rebuilds this counter during the tick. A reader outside the simulation thread must
not treat aggregate counters as consistent with each other or with the record array. Per-record
fields from outside can come from different tick phases; for example, a unit's animation frame
jumped back and forth across 20 reads 50 ms apart.

The simulation tick (the Extender's `OnTick`) ran on a different managed thread from the Unity
main thread: thread 16 versus thread 1. The Extender's tick number is its own counter starting
at 1 after loading. The native game tick was about 215,000 at the same moment.

## Tools and usage [mod integration]

- **`Shared\Tools\GameMemReader`** (net10.0 console, read-only)
  - What it does:
    - Opens the process with `PROCESS_VM_READ` only and needs no mod installed.
    - Resolves the globals as described above and caches them per native hash and Extender
      source.
    - Takes field names and offsets from the installed `SHCDESE.dll` via reflection. Every
      struct size is checked against the runtime size, and `layout <Type>` prints the offset
      table.
  - Commands:
    - `status`, `units`/`unit <id>`, `buildings`/`building <id>`, `players [id]`
      - Field selection: `--fields a,b|*` and `--all-fields`.
      - Filters: `--where field=value` matches a number or an enum name.
    - `struct <Type> <addr>`, `read <type> <addr>`, `raw <addr> <len>`
    - `grid <GridName> x y [w h]`
    - `globals`, `types`, `layout`
    - Common options: `--repeat`, `--interval`, `--compact`.
  - A full read of the unit array (11.7 MB) took about 2.4 ms.
- **`Testmods\GameStateProbe`** (BepInEx, `NetworkMode=0`, development only)
  - Runs a line-JSON server on `127.0.0.1:47811`.
  - Commands run either inside `OnTick` (`context tick`, consistent between ticks) or on the
    Unity main thread (`context main`, which also works while paused). `auto` chooses tick while
    the simulation is running.
  - Commands:
    - `mem`/`memv` copy memory in-process. Each page is checked with `VirtualQuery` first, so
      invalid addresses are rejected cleanly (verified with `0x10` and an unmapped range).
    - `eval "Type::Member.Member[i].GetX()"` reads managed state (Vanilla, Extender and other
      mods).
      - Only read-style methods are allowed (Get/Is/Has/Can/Count/Find/Contains/ToString).
      - `SetCurrentYear` was correctly rejected.
      - `MainViewModel.Instance` is refused while `viewModelLoaded` is false.
    - `types`, `members`
    - `watch` repeats a request every N ticks.
    - `quit`
  - The client is the same `GameMemReader`:
    - `eval`, `ptypes`, `members` and `probe '<json>'`.
    - `--samples n --every k` repeats a request every k ticks.
    - `--source probe --context tick` makes every native command read through the probe.
  - Measured costs:
    - A request in tick context costs one queue check per tick; with no request there is
      nothing else.
    - A tick-consistent copy of the whole unit array via probe, including base64 transport,
      took about 300 ms end to end. For large reads during measurements, use `--fields` or
      `struct` on single records.
- Open points:
  - Not yet checked against `r_CurrentPositionTileId`: whether `tile index = x +
    MapRowLookupTable[3·y]` holds for every row.
  - No load test with 10,000 units yet.
  - Neither tool has ever written game state. `quit` is the probe's only state-changing
    command.
