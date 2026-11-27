# Farm construction preview [Scope: Vanilla + mod integration]

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Static audit on 2026-10-13; first-stage red ground marking accepted by the user in-game; expanded counts and shared tooltip layout subsequently accepted by the user.

Full contract and update procedure: BugfixesAndQoL/UpdateToNewDLL.md in the workspace.

- 0x90CD0 -> 0x77E60 -> 0x5DD70 -> 0x41D60 -> 0x1A2D90 -> Director/GameMap/gameTile is the input, validation, preview, buffer transport and colouring chain. Actual construction rechecks through 0x6D580.
- Farm coverage excludes River and counts OasisGrass, ThickFoliage and Foliage separately, requiring at least scale squared. The separate green counter counts only OasisGrass and ThickFoliage once each and requires >=50; Foliage contributes zero to green, and River contributes zero to both. The former 2,2,1 description was incorrect. Combined bits must not be collapsed; visual texture alone is insufficient.
- The blocked preview reruns 0x7B060, which omits farm coverage and therefore leaves zero-contribution land dark (palette 62) even though the overall farm is blocked. Palette 55 is Vanilla red.
- 0x619C0 generates square XY footprints. Canonical manager RVA 0x405EDB0, native validity mask RVA 0x3A11EA4, row prefixes RVA 0x402FF2C. Preview graphics are temporary and cleared by the renderer.
- Preview extent [0x5DD70,0x60CAC) includes trailing switch-table data. Use reachable control-flow decoding, seeded by baseline COMPUTED_JUMP edges, not linear decoding of the whole PE extent. The fresh audit decodes 2562 reachable instructions and finds no interior xrefs into the six-byte Indirect entry span; it also verifies the unique signature and square-footprint producer.
- Installed RedBird NativeX64 1.5.0.0 Indirect displaces exactly six bytes (five-byte rbx store plus rbp push), resuming at 0x5DD76. Tests use this exact backend/ABI and validate the trampoline, patch form, pointer slot and pointed-to entry. Hooks remain process-lived and logically gated.

- Resource extension: quarry 6x6 >=8 HasStone; iron mine 4x4 >=4 HasIron; pitch workings 4x4 >=1 IsPitch. Resource counters precede River exclusion in 0x77E60. The native preview remains the only sampling callback; publication uses immutable session/mapper/XY snapshots and APIShared main-thread presentation observers.

Migration 2026-10-13: BugfixesAndQoL 1.0.189 owns the permanent preview hook and APIShared 0.9.0 Ground contribution. Local/default-on activation is generation-bound with immutable session snapshots. Contracts and regression tests are in BugfixesAndQoL/UpdateToNewDLL.md and BugfixesAndQoL/tests/BuildPreview.Tests; the prior testmod is retired after package verification. User-confirmed layout is retained; the integrated switch needs a visible game test.

## Map-edge handling, 2026-10-31 [mod integration]

The 2026-10-30 client reproduction on CenterOfPower.map (14:11:18.354) reached FootprintMarker's invalid-map-mask guard. The user tested apple farm and quarry below their BuildingLimit caps; an exhausted limit is not established as the cause. Native validator 0x78768..0x7879D branches to 0x798D1 to set placement state 2 for an invalid coordinate/mask; preview 0x5DD70 normally returns for that state. Extender custom validation/override setters can overwrite the state, but the reproduction does not capture mapper/XY/raw state and does not establish that mechanism as its trigger. Function role names remain candidate-level; these specific instructions were checked against the reference hash above.

The mod now treats any out-of-axis or masked-out footprint as a transient skip, with no partial grid writes or published counts. The callback clears current counts while preserving activation, and the next complete footprint works normally. Invalid packed indices/overflow and unsupported footprint scales remain fatal contract violations. No native code, hook lifetime, placement decision or ground/resource formula changes. The existing production memory-fixture tests cover all seven supported mappers and boundary recovery; real game/editor acceptance remains pending.

## Construction palette [Vanilla facts]

The installed native and managed hashes match the baseline. The tile renderer sends both 55 and 60 through its nonzero colour-only branch (no high word) to record index 7. In the matching managed assembly, GameMap.processTestMap copies that value into floatColour and flags a tile refresh on change. gameTile.setTileColour maps 55 to tileColRed (#F94D01) and 60 to tileColBrown (#A84522). The native blocked-building preview emits 55 for obstacle failures and 62 for dark footprint tiles.

## Ordinary building hover and batch admission [Vanilla facts]

Static audit against the installed FBCB9319 native image and BC8B6A39 managed
image, 2026-11-21. Generic names remain candidate labels. The following fields,
branches and layout were established from the canonical instruction stream and
managed consumers.

- Managed EditorDirector's construction input calls EngineInterface.PlaceMapperItem.
  DLL_MapAction 0x85BD0 uses the active native mapper, temporarily selects the
  passed player, and forwards the mouse state through 0x8AAF0 to 0x90CD0.
  Ordinary hover uses state 0 plus constructingOnly, centers XY by scale/2,
  calls 0x77E60 with preview byte 1 and publishes the result at 0x60AD4B0.
  The validator's per-tile helper 0x7B060 gets mode 2 on hover. Its unit writes
  in the actual placement path are skipped in mode 2. Hover can update
  generation-based pathing search scratch and placement presentation fields.
- Bakery, Fletcher, Poleturner, Brewer, Blacksmith, Armourer and Tanner take
  the 4×4 workshop preview path, including the adjacent-road selector at
  0x7AA60. Their GM 8 frame bases in that order are 289, 1, 577, 865, 1153,
  1441 and 1729. The open-ground variant is 8 × 16 frames after the base.
  Hovel is 4×4 with GM 7 and an appearance-indexed base table at 0x2E90A8.
  Mill is 3×3 with GM 7, first frame 219. For these mappers 0x5DD70 writes
  within their entire square footprint. A blocked position emits palette
  tokens rather than the normal building sprite tokens. In normal sprite
  tokens, high word = GM file and low word = image minus one.
- Ordinary clicks call 0xCC420, submit Chore 28 via 0x23990, and run the
  normal 0x12090/0x6D580 placement and debit. An ordinary click requires
  at least 20 global free building slots. In network mode it also requires
  headroom of at least two against 4000 / max(2, counted participants).
  Ordinary editor world kind 1 bypasses the cost check. Kind 4 adds tutorial
  execution restrictions, and kind 6 has an alternate charge path.
- 0x20950 scans 500 pending records, accepting signed state zero or >=10.
  When all are occupied it still returns a slot, which the producer overwrites.
  Chore 28 has ten body bytes, with a 23-byte framed network record and a
  one-million-byte outbound buffer. Native append does not check that bound.
  Delayed records carry the issuing player and top-left XY, not the cursor XY.

### Attached storage and the woodcutter [Vanilla facts]

The same native image routes Stores (5×5), Granary (4×4), and Armoury
(4×4) through selector `0x780D0`. Their generic preview tokens start at
Buildings2 frames 26, 278 and 294 respectively. Native network checks protect
against enemy units, enemy buildings or territory, and enemy keep proximity.
They run before the terminal cardinal-edge neighbor lookup. Calls at
`0x789D9`, `0x78A59`, and `0x78AD9` invoke `0xC72C0`, which requires
an active structure of the same type and owner. The first structure can
take the separate branch for a zero first-structure field. The family limit
checks `0xB8140` for live matching records whose signed current stack is
below one. The thresholds are 32, 8 and 8. One Stores command builds four
records via `0x76E80`; Granary and Armoury each build one via `0x75780`.

Woodcutter uses a 3×3 footprint and Buildings2 frame 1. Its placement
selector `0x7820C` includes a radius-two enemy proximity query and a
networked tile check. Ordinary Chore 28 construction creates one piece.
`0xB9710` grants the emergency wood exemption while the player's wood
is below the live row cost and the census-maintained flag at
`0x67E83E8 + player` is clear. The Granary exemption additionally uses
the Granary flag at `0x67E83F1 + player` and its own live row cost.

### Attached storage prototype [mod integration]

The installed Script Extender already detours `0xC72C0`. Its public
adjacency Post event exposes the original result. AreaConstructionTest
adds an adjacency hit only for a scoped prospective hover and uses
Vanilla adjacency for all real builds. An editor integration run placed
three contiguous pieces per family, confirmed occupied/unattached rejection
and draft-only acceptance, and observed 12/3/3 new records with unchanged
stock. The acceptance harness queried the native paths directly.

### Killing-pit footprint and ordinary construction [Vanilla facts]

The installed FBCB9319 native image and Extender enums identify
`MAPPER_KILLING_PIT` (98), `STRUCT_KILLING_PIT` (67), and a one-tile
footprint. The canonical sprite table at `0x2E7AF0` selects
`GM_KILLING_PITS` (139), with base image 1 at table `0x2E7CB0` and
no separate alternate at `0x2E7E70`. The stock hover emits one token
through the common path of `0x5DD70` using automatic variation 15.

The validator dispatches the mapper to `0x78334`. It preserves the
pit-specific enemy checks, territorial checks, and common terrain and
occupancy checks. There is no storage-style adjacent-piece requirement.
`0x90CD0` uses ordinary affordability `0xCC420` and Chore 28.
`0x12090` hands the decoded mapper, coordinates, and scale to
`0x6D580`, which validates and constructs through `0x75DF0`.
That constructor creates one record and registers the footprint before
the regular `0xC8A90` debit. The default data has a six-wood cost.
Charges use the live construction table.


## Cattle placement and livestock footprint, 27 November 2026 [Vanilla facts]

Fresh canonical DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, matching CURRENT.json and the selected semantic function records. Confidence: confirmed static. This closes the cattle branch of `0x77E60`, its actual-construction caller `0x6D580`, constructor `0x73450`, preparation/access helpers `0x72490`, `0x5D740`, `0x6CDD0`, `0x725E0`, and livestock consumers `0xA3A70`, `0x158780` and `0x198870`. It establishes construction and ordinary livestock requirements, not that any particular map contains a suitable vacant site.

- Mapper 73 (`0x49`) selects block `0x786F0` inside `0x77E60`, through selector byte `0x79914[73-0x33] = 2` and target table `0x798F4[2]`. The current semantic record has a decompiler gap here; this branch was checked against the canonical machine stream through the real return `0x798F2`, rather than treating `0x786F0` as an independent function. `0x69850` supplies exactly scale-squared square offsets; cattle scale 10 therefore visits 100 cells. Invalid axes/mask fail at `0x78768..0x7879D`.
- Counter increments `0x787C7..0x78878` use the terrain byte at TileManager `+0x9D2500`: non-River OasisGrass `0x10`, ThickFoliage `0x80`, and Foliage `0x01` each add one to coverage; only the first two add one each to green. River adds zero to both. Overlapping bits contribute separately. Farm mapper range 70..73 reaches final checks `0x79898` and `0x798B4`: coverage >=100 and green >=50 for scale 10; failures publish reasons22/23. Requiring all 100 cells to carry a green bit is stronger than Vanilla. For example, 50 OasisGrass-only plus 50 Foliage-only cells meet both counters, subject to every other placement check.
- Per-cell `0x7B060` has no additional terrain-byte fertility predicate. It preserves height, flags, structure, vegetation and unit checks; return0 accepts, return1 blocks and return2 denotes vegetation, also blocked by this caller. `0x7871F/0x7872A` turn preview byte into helper mode0 for construction or2 for hover; the actual call is `0x78889`. Mode2 suppresses the helper's two foreign-unit work-counter writes, but is not generally read-only: presentation/reason and search scratch can still change, including the cattle height-reason branch. Independently verified complete helper body: 1206 bytes at `0x7B060`, SHA-256 `3E9479B11E94F87C74D37B29DD924FE6976D3459592945F20F7D418C5D3EAFCE`.
- `0x6D580` revalidates through `0x77E60` unless the explicit placement override is active; a free construction argument bypasses the later debit `0xC8A90`, not this validator. Successful constructor identity/readback remains necessary: satisfying the two counters alone proves neither placement nor reachability.
- Cattle constructor `0x73450` creates the 3x3 hut via `0xB47E0`, prepares the full10x10 field via `0x72490`, and stores 27 records from `0x2D3E00`. These are **triples** `(x,y,initialDamage)`, stride12, with four variants of stride `0x144`, not wheat/hops XY-pair tables. Each variant has 27 unique XY records within0..9 on both axes. Records4..26 set table damage, FarmFence `0x08000000` and the farm BuildingID. The first three cow positions are respectively `(2,7),(6,5),(5,2)`; `(6,3),(6,6),(3,5)`; `(5,5),(1,6),(5,1)`; `(4,1),(4,4),(3,6)` across variants0..3. Preparation/access/path helpers do not add a full-field green requirement; hut-local width2 brush updates fit within the10x10 field plus a one-cell safety halo.
- Full cattle-farm `0xA3A70` and cow `0x158780` consumers likewise do not require every field cell to be green. Natural cows originate through `0x17FEF0` at the stored first three positions and receive real farm ID/global/animal-slot bindings; field geometry, native building acceptance and those actual lifetimes remain distinct requirements. See [the worker-binding audit](RECRUIT_TRANSFORMATION.md#auxiliary-worker-bindings-and-ordinary-ai-hiring-27-november-2026-vanilla-facts) for reservation and retirement timing.

Reproduction: use CURRENT.json -> DATABASE_INFO.json -> the selected SQLite `functions` records, filtering `binary_hash` by the full hash above, or `tools/semantic/query.ps1 function` for each listed function. For the un-decompiled cattle validator branch, read the canonical PE machine bytes `0x77E60..0x798F2`, the dispatch tables `0x798F4/0x79914`, and cattle table `0x2D3E00` with the strides above. No native preview invocation or terrain alteration is needed for this audit.
