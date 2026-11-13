# UiAcceptance native and managed contracts

All native accesses are hash-bound to CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (checked over the whole
file before first use; an unknown DLL fails the probe step, no fallback). Test utility only.

| Feature | RVA | Use | Evidence |
|---|---|---|---|
| Player move probe | 0x196100 | Called like Vanilla's Chore-17 handler 0x10AE0 (`rcx = 0x67E8400`, lea at 0x10C7C, call at 0x10CAB) under the private `EngineInterface.threadLock`; deliberately goes through BugfixesAndQoL's detour. | `_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/MANUAL_GROUP_ADMISSION.md` |
| Tribe move mode | 0x7CC6720 + tribe*0x688 + 0x56C | ushort written by 0x196100 (bit 7 of argument 6), read before/after the probe. | same, `OUTPOSTS.md:24` |
| Unit manager | 0x67E8400 | First argument of the probe call (ignored by 0x196100). | DLL bytes at 0x10C7C |
| Path region grid | 0x50EC690 | ushort per tile id; RegionScan candidate selection only. | `Pathfinding/src/UnitCommands/Runtime/UnitCommandPathRuntime.State.cs` |
| Move target availability | 0x3A11EA4 | byte at y*800+x; RegionScan only. | same file, line 102 |
| UI mapper fixture | 0x87830, 0x8BD60, 0x90CD0, 0x93750 | Read-only native audit: pause must be zero for mapper start and placement; primary-unit brush size is zero, native count is fixed to one. Engineer/archer tile logic rejects mask 0x40501481. Runtime uses existing public GameTileManagerAPI bounds, packed IDs and named TilePropertyFlag values to select an unoccupied fixture tile; it does not call these native helpers directly. | Canonical FBCB9319 export and Iced instructions; dated evidence `../../.work/a/ai-game/001/ui-mapper-fixture-audit.md` |
| Current mapper observation | 0x60AD45C reference data | Reads the existing public Script Extender `GameGlobalsManager.CurrentContextMapperValueVA`, whose DLL_StartMapAction pattern resolves this field. A missing SDK address fails only the UI fixture; no private resolver or hardcoded-address fallback. | Same audit; `GameGlobalsManager.cs` current context mapper resolver. |

Managed members reached by reflection (not public in the real Assembly-CSharp; `verify.ps1` rejects
any compiled nonpublic access): `EngineInterface.threadLock`, `FRONT_Multiplayer.RefFileLists`,
`EditorDirector.placeEditorTroop(Enums.eMappers)` (validated private instance void; the real editor command),
`selectedMPHeader`, `MPMapValid`, `EditorDirector.preDLLCallActions`/`getMouseStateForEngine`
(Harmony postfixes, priority First, active only during a MapClick step), the Noesis component's
`View`, APIShared internals `IngameMenuActions.registry` and `UiWorkRuntime.scheduler`, and
`Spectator.SpectatorPlugin.processRoot`. A missing member fails only the step that needs it.

## Worker production integration acceptance

`WorkerProductionSteps` is armed only by its two explicit local scenarios. It registers
one desired profile before the fresh world, uses the existing Runner/UI work service
and mission lifecycle, and adds no publisher, hook, timer or serializer. The SP fixture
uses real woodcutter/mill buildings and observes a living Miller's GID, three-item cargo,
persisted batch provenance, complete delivery and stockpile increase. The editor fixture
uses the existing public CreateUnitLocal path while paused. Manual wood yield calls are
explicitly labelled integration probes: they do not prove the full wood transport cycle.

The installed Extender's `SHCDESE.Detours.BulkUnitDetours` type is reached by checked
reflection because the declaring type is internal. Its public static
`c_game_unit_calculate_worker_good_yield_hook_impl(int,int,int):long` is invoked, so the
real SE Pre/Post, original-actor context and installed APIShared native Core all run.
No replacement yield algorithm or additional hook is used. APIShared's internal
`WorkerProductionStateStore.RowsAddress` supplies a read-only view of the audited32-byte
rows; its schema is checked by the production owner and save/readback participant.

Additional hash-bound observations/call:

| RVA/layout | Contract |
|---|---|
| `22020` | `void(native network manager,int noSend)`; callers15170/15200 pass1,94350/D1AD0 pass0. The complete function checks manager+DA8==0 and manager+279FA4==0, computes per-unit/category checksums, restores temporarily cleared native unit/player/building fields, and publishes Chore12 only when arg2==0. The probe passes1 and never overrides the early gates. |
| `8574320` | Native network manager passed by all audited22020 callers. |
| manager+`36FA50` | Checksum storage slot; require0..8 before the call. |
| manager+`14F0CC + (slot*10000+oneBasedUnitId)*4` | Unit checksum written after the production CRC hook22149. |
| manager+`14D108 + slot*38` | Unit category sum, including the same changed unit CRC. |
| `379CEEC + playerId*583C` | Native productivity, also used by Tanner's probe. Require<=100 for the isolated quarter-output fixture; original18D940 floors lower values to100. |
| `GameUnit pointer-4`,1168bytes | Native saved unit record starts at origin+658, whereas public GameUnit starts+65C. Read only. Before/after manual yield must be byte-identical while the sidecar and actual unit/category CRC change. |

Save/load compares the exact sidecar row and native GID at NativeLoaded before the
managed simulation start. A linked restart checks its fresh table at that same checkpoint,
not after an arbitrary number of simulation frames. The actual file is additionally
inspected through the production owner's archive reader. Randomly named acceptance
saves/maps are retained as evidence and never overwrite existing player files. These
local scenarios do not constitute real multiplayer or write-failure acceptance.

The Miller fixture requests physical flour capacity through the existing public
`AllyTransfers.TryGetCapacity` service inside `SimulationTickEvents`. Setup and any
ordinary `MAPPER_STORES` extensions run in that same shared simulation scope. At
most16 successful placement attempts are allowed; the next native tick must confirm
at least64 free flour units before setup and again before delivery measurement pass.
The fixture first establishes512 free flour units before placing its production
buildings, because real wood arrivals can consume a64-unit empty-slot reserve in a
few seconds. Extensions try every living owned storage anchor with a six-tile
initial radius, matching76E80's four cells with three-tile spacing. Native placement
validation remains authoritative; a rejected first anchor does not end the search.
No inventory is removed and no delivery is injected. The observer is dormant without
an explicit pending request. Per-building stack fields are displayed only as evidence;
the shared native query decides capacity.

Miller delivery tracing emits at most20 lines (changed states only, including a reserved
final result). Read-only hash-bound fields are native unit+918 (public GameUnit+2BC,
AI and resume-state words), unit+98E (public+332, the Miller's storage target from the
1377C0/138162/138290 path), and the existing public production-building/tile/cargo fields.
The final diagnostic timeout includes the last observed identity, state, cargo, marker,
target, stock changes and last capacity snapshot. No production assertion is weakened.

## Pathfinding fire escape with a stale AI mark

`FireEscapeSteps` (scenario `SP-fire-escape.json`) writes 1 to `GameUnit.N000000D8` (+0x428, UInt16)
of its own fresh AI fixture units: the AI group-move exclusion mark that native `0x11B520` honours
(`_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/GROUP_MOVE_EXCLUSION_MARK.md`). It
creates pitch ditches with the public `GamePitchManagerAPI.CreatePitch` and sets their state to
`PitchState.OnFire` under `EngineInterface.threadLock`, as PerfProbe's ignition does. Fresh ditches
hold no pitch, so that alone made no fire record (run 002 of 09.11.2026); the step therefore also
lights each pitch tile through the installed Extender's public static
`SHCDESE.Detours.BulkProjectileDetours.c_game_spawn_fire_internal_hook_impl(int,int,int,int,int,int):int`
(internal declaring type, checked reflection; Extender Pre/Post and native `0x9C120` run). Its
coordinates are world units (tile*8, `0x9C120` adds a small random jitter); spread 2 adds no type
`0x1A` record. It passes
when every living marked fixture unit seen on a tile with a type-9 record in state 1 or 2 was later
seen with the mark 0 and a move target without fire (the jitter leaves some pitch tiles unlit, and
Pathfinding returns escaped soldiers once the short fire is out); only
Pathfinding's soldier move and the AI's own order routines write the mark, and the fresh group is
in no AI role table. A marked control group of two units in a fresh group at least 15 tiles away
must keep its marks. For exposure and the progress log it reads, after the full-file hash check, the
fire effect records (`0x2F7A680`, slots 1..5999 of `0xE8` bytes: `+0x3C` state 1 new or 2 active,
`+0x3E` type 9, `+0x60` native tile id; `FIRE_EFFECTS.md`); read only.
