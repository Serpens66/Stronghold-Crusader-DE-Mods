# Prebuilt AI workshop production

Installed `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Addresses below are RVAs for this hash. Evidence is the semantic decompiler export, the installed Script Extender interop, and the Frederick runtime trace below.

## Vanilla path

- Skirmish setup `0x94350` prepares the selected AIV and, with the complete AI castle option, sets that player's bit in the prebuilt-player field before calling `0x55F50`.
- Completed castle execution `0x55F50` enters frame executor `0x51790`. After a successful workshop placement through `0x6D580`, the frame executor calls production initializer `0x31000`. Ordinary AI construction also calls the same initializer.
- `0x31000` reads the player's AI Lord slot. A zero slot returns without writing production. For nonzero slots, AIC offsets `0xCC`, `0xD0`, and `0xD4` respectively configure blacksmiths, fletchers, and poleturners. The editor represents Both with `-999`.
- For Both, `0x31000` calls `0xBEC10` and writes the chosen good to both building offsets `0x28E` and `0x290`. The Script Extender names these `r_NextProducedGoodId` and `r_ProducedGoodId`.
- `0xBEC10` counts alive (`2`) workshops of the same owner and building type, reading the next good at `0x28E`. It counts every good other than the tested good as the opposite side. Fletcher ties select bows (`0x11`), poleturner ties spears (`0x13`), and blacksmith ties swords (`0x16`). The scan includes a just placed workshop if its alive state is already `2`.
- Both audited `0x51790` workshop placement branches set the new building's alive state to `NeedsInit` (`1`) immediately before calling `0x31000`. The current workshop therefore does not contribute to `0xBEC10` in these paths. In ordinary staged AIV construction, previously placed workshops contribute only once they have become alive (`2`); the complete-castle path processes its frames together before those earlier workshops advance. The correction models the consecutive choices that the same balancer makes when each previous workshop is already active, using the same two production fields as `0x31000`.

The installed editor's `PnlEditLordViewModel` maps Both to `-999`. Extracted Vanilla AIC data contains `fletchers_make = -999` for Baldwin, and his bundled AIVs contain several fletcher frames. This gives a reproduction scenario without modifying `testlord_serp`.

## Diagnostic boundary

`BuildingR3EventHooks.OnBuildingSpawn` Post fires after the native spawn call and before the caller's `0x31000` initializer. The testmod captures the Lord slot and AIC value there. Its correction is limited to a new complete-castle start with a confirmed Both AIC value and prebuilt workshops that retain `NeedsInit` and both default goods at managed start. Save loads and later workshop construction are outside the correction scope. The earlier zero-slot theory was refuted by the Frederick trace and was removed from the correction condition.

## Frederick runtime observation, 2026-09-27

The first local game session used a new custom game with seven Frederick AI players and complete starting castles. The testmod observed 41 prebuilt fletcher/blacksmith spawn events. Every event reported native Lord slot `10` and roster Lord `SK_FREDERICK`; the completed-start bitfield was `0xFE`. The zero-slot early return at `0x31000` is therefore not the cause for these observed spawns. The mod correctly made no production changes. Two subsequent new-game sessions without completed castles reported no prebuilt workshops and no production changes. The static Extender event and first-tick marker ran after startup cleanup.

This first diagnostic build logged no production goods for the 41 captured IDs because its startup scan filtered for `AliveState.IsAlive`. A newly spawned workshop may still be `AliveState.NeedsInit`; the absence of lines does not establish an absent or invalid building. A follow-up diagnostic must read those IDs directly and log alive state, AIC setting and both production fields at spawn, native-start completion, managed start and first tick before assigning a different cause.

## Frederick production trace, 2026-09-27

A second new game with a complete castle for player 2 yielded five captured workshops: fletchers `35`, `322`, `325` and blacksmiths `37`, `327`. At spawn, native-start completion, managed start, and simulation tick 1, every fletcher was `AliveState.NeedsInit`, with both production fields set to `STORED_BOWS`. The live Frederick AIC reported `fletchers_make = -999` at every observation and the native Lord slot was `10` at spawn. The blacksmiths used their fixed AIC value `22` and had both fields set to `STORED_SWORDS`.

This ties the observed default-only Fletcher result to the `0xBEC10` filter: it counts only alive-state `2` workshops, so the other prebuilt fletchers still in state `1` do not influence each subsequent Both choice. The call path through `0x51790` invokes `0x31000` after successful placement; the runtime trace by itself does not instrument entry to `0x31000`, so the exact execution count is supported by static control flow rather than a direct call trace. The original zero-Lord hypothesis is refuted for these sessions. No production values were changed by the diagnostic mod.

The bounded correction now plans choices in ascending captured building-ID order. It begins with existing alive workshops of the same player and type, then adds each planned prebuilt choice to the balancing counts before choosing the next. For fletcher IDs `35`, `322`, `325` with no alive fletcher baseline, this produces bows, crossbows, bows. It writes both production fields only after every captured group member still matches the confirmed `NeedsInit`/default-goods state. The next trace confirms this behavior in a new Frederick game.

## Frederick correction trace, 2026-09-27

A subsequent new complete-castle game for Frederick as player 2 captured three fletchers, IDs `34`, `246`, `247`, and two blacksmiths, IDs `36`, `248`. The prebuilt bitfield was `0x00000002`; the Lord slot was `10`, and Fletcher AIC value `-999` was stable from spawn through the first tick. Before correction all three fletchers were `NeedsInit` with both fields set to bows. At managed start the correction planned bows, crossbows, bows in ascending ID order, then wrote both fields for the group. First-tick snapshots confirmed that the crossbow choice persisted in both fields for ID `246`, with bows retained for IDs `34` and `247`. The blacksmith AIC value was the fixed swords value `22`; both blacksmiths retained swords in both fields. The post-startup-cleanup first-tick marker fired and this game section contains no testmod error or exception.

This confirms the correction through simulation tick 1 for the Frederick Fletcher case. Later production cycles, Both-configured blacksmiths and poleturners, save loading, and multiplayer remain separate runtime checks.
