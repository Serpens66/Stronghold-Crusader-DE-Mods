# Trapped-unit rescue prototype contracts [Scope: Mod work, not Vanilla]

Observed 2026-11-15 against the installed canonical CrusaderDE.dll, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The same hash is selected by CURRENT.json and the semantic function records.
Generic function names remain candidate labels; the specific machine/data-flow
facts below were checked in complete containing functions and related producers/
consumers, with key instructions independently decoded from the installed PE.
This note records static contracts, not completed worker gameplay acceptance.

## Relocation, occupancy and identity [Vanilla facts]

`0x19BA40` takes native unit manager, one-based unit ID, uint tile X/Y and a
16-bit base height. It validates coordinates against the active map, clears
native unit `+0x74E` and `+0x740`, and writes DWORD `+0x750=8` (substep8/index0).
It calls `0x186AD0 -> 0x186AF0` to unlink the old tile, writes current/next X/Y,
fine coordinates `tile*8+4`, base height and compact current tile ID, then calls
`0x180230 -> 0x1802A0` to link the new tile and `0x18BE50` for view offsets.
It neither allocates a unit nor changes its slot/global identity, owner, type,
health, tribe or crew references. It is not a general job/state normalizer.

Occupancy heads are at RVA `0x4C559B0`; native unit `+0x8FA` is the next link.
The unlinker assumes a coherent old chain. The inserter sorts the resulting
chain and updates occupancy ordinals/wait fields, but skips units whose native
mounting word `+0x9B0` is nonzero. Source-chain validity therefore matters before
calling a relocation helper; raw coordinate assignment is not equivalent.
Unit records use stride `0x490`, GameUnit view bias `0x65C`, and one-based IDs.

`0x19BA40` leaves portal state, old target fields, path length/bytes and special
AI/climb state untouched. `0x197950` separately clears index/length at `+0x752`,
word `+0x92A` and portal `+0x8EC`, not path flags. `0x188340` returns arrived when
path flags are zero and portal state is zero, without comparing the old target.
Thus cancellation cannot by itself promise continuation of a worker's task.
See [Lord relocation](LORD_HOME_TELEPORT.md) and
[standing-height consumers](PLAGUE_HEIGHT.md) for their separate contracts.

## Search and physical exits differ [Vanilla facts]

At `0x1855A0`, starting a new packed-direction step normally calls `0xDCE60`
then `0xDCD60`. Already accepted substeps advance without repeating these gates.
The native movement class at unit `+0x6EC` selects additional behavior; class1
bypasses this ordinary executor branch. It must not be judged from ordinary
direction bytes alone.

For ordinary class0, `0xDCE60` accepts a source-forward connection OR the
destination's reverse connection before its separate Assassin/moat branches.
A zero source connection byte is therefore not proof that every physical exit
is blocked. `0xDCD60` checks the destination surface for its restricted type
switch, not whether the current surface is valid. Assassin climbing and stored
moat permission require their own mode/state predicates.

`0x196280` first calls coordinate normalizer `0x19B1B0`. Its planning start is
current X/Y only if path flags are zero AND substep is8; otherwise it uses next
X/Y. Before ordinary `0xF4930` publication it copies the selected unit into the
native working slot and clears the unit's packed route. `0x18E1E0` instead uses
current X/Y and a stack-backed output buffer. Neither a positive macro-region
query nor a saved boolean cursor probe proves that a later modded move will
publish the same route. See [probe closure](UNIT_COMMAND_PROBE_SCRATCH.md).

## Mounted siege crew and dismount [Vanilla facts]

Siege records keep crew IDs at native `+0x96C`, globals at `+0x974` and count at
`+0xA0C`. Engineer state5 in `0x14AED0` and the crew-refresh passage of
`0x182B00` around `0x1839C0` do not make their coordinates follow the machine.
The latter validates crew identity/state and refreshes the engineer's timeout.
The engineer timeout shares native `+0x974` with device crew-global storage;
it must not be interpreted uniformly across types.

`0x196F80` sets mounting1, clears movement state and unlinks occupancy.
`0x196FD0` clears mounting and re-registers the engineer. Native explicit
dismount in `0x11E960`, action `0x11`, validates the original crew globals,
searches around the machine's CURRENT coordinates, writes each matching
engineer's new position and then unmasks/relinks it. Moving a normal device
root therefore does not require manufacturing replacement engineers or forcing
mounted engineers into independent tile lists. Active deployment/transport and
malformed shared-crew references are separate problems, not covered by this fact.
See [crew lifetime and timeout](SIEGE_ENGINEER_CREW_TIMEOUT.md).

## Apple-worker arrival and production boundaries [Vanilla facts]

Complete handler `0x135E30` uses native unit `+0x9A0` as selected vegetation ID,
the low WORD at `+0x9EC` as picking-cycle counter, `+0x9E0` as carry amount,
and `+0x98E` / `+0x6F8` as storage ID/global. Native `+0x990` is the workplace ID.
`0x108040` chooses an apple tree and one of its approaches at tree X/Y plus or
minus2. After picking, the handler can select the NEXT tree in the same call
that increments the counter. A later observer comparing only the NEW selected
tree to the counter increment can therefore attribute the wrong tree.

Arrival states2/8 call `0x188340` and inspect the selected vegetation field
before entering work state3; that transition does not independently test target
distance. State3 advances the picking counter and, after the fourth stage,
searches storage, plans an outgoing route and assigns the yield. Clearing an
in-flight route can thus advance this cycle at an incorrect physical location.
This is a static conditional consequence, not a reproduced gameplay claim.

Instruction boundary `0x13640E` precedes `0x18D940` and the assignment to carry:
it is a batch-production boundary, not every picking step. Boundary `0x136901`
follows the `0xB6100` fruit-storage call and precedes carry decrement and
production-stat increments. `0xB6100` validates storage global identity and
capacity before adding the good; `0xC9080` independently requires storage-access
distance `<3` on BOTH axes before the normal apple delivery branch.

Other workers differ: woodcutter `0x12BC00` state1 checks tree distance `<3` and
returns to selection state0 when too far away. Ox `0x132910` also checks storage
distance; its failed storage-recovery branch can clear cargo. There is no
general guarantee that canceling any worker route merely reissues the same job.
See [production chains](WORKER_PRODUCTION_BONUS.md).

## Apple observer ID-domain adapter [mod integration]

On2026-11-15 the fixture's initial remote-picking measurement mixed the native
selected-tree array index with the public SDK's one-based array ID. Canonical
machine code at `0x1080EE` multiplies the selected index by `0x9C`; `0x1080F5`
and `0x108103` read manager-relative X/Y at `+0x86/+0x88`. The typed array starts
at manager `+0x18`, with record X/Y at `+0x6E/+0x70`. Independently, `0x135E30`
reads growth at manager `+0xA4 + index*0x9C` and writes the selector's returned
index unchanged to unit `+0x9A0`.

The inspected installed public SDK getter resolves `VegetationArray[id-1]`;
the installed RedBird constructor does not shift that array base. The fixture
therefore translates a positive native index below span capacity to SDK
`index+1` before capture and identity validation. This is an adapter between
distinct domains, not a claim that the SDK's one-based getter is wrong. The
previous measurement compared a different vegetation record; it cannot prove
remote harvesting. The distance guard remains at2 and needs new gameplay
evidence with the corrected record mapping. See the testmod update-contract
document for the complete adapter contract; no external source was changed.

## Native caller bytes [Vanilla facts]

The pristine instruction span `[0x196342,0x196352)` is16 bytes:
`498BCF410FBFC489442470E8EE040000`. Its four instructions prepare arguments and
call `0x196840`; that leaf returns the completed-moat bit in EAX and ends with
`AND eax,1`. These bytes and their flags are Vanilla facts. The installed-PE
direct-branch scan found no entry strictly inside this span.

## External suppression [mod integration]

The adjacent Fixes deletion hook, its leading JZ, the prototype's conditional
flag bridge, managed AI-wall suppression and LordTeleportGuard integration are
EXTERNAL/MOD contracts, not Vanilla behavior. Their current implementation and
byte/ownership checks are documented in
[testmod update contracts](../../../../../Testmods/TrappedUnitRescueTest/UpdateToNewDLL.md).
No Fixes callback or testmod rescue is added to the Vanilla model by this note.

## Experiment status and evidence limits [mod integration]

The mutable experimental implementation is `Testmods/TrappedUnitRescueTest`.
Its causal snapshots and synthetic/native fixture distinctions are test policy.
This note does not claim PASS for worker full-cycle continuation, siege crew,
autonomous AI, save/load or multiplayer; ongoing test results belong to their
dated run records. No old native exports, projects or historical audits changed.

## AI AIV drawbridge producer, 2026-11-17 [Vanilla facts]

The canonical DLL SHA-256 was rechecked as
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The reproducible evidence is the CURRENT semantic function records for RVAs
`51790`, `7A170`, `5CD90`, `C3BF0`, `6D580`, `C77C0`, `77E60`, `739C0`,
`69850`, `B47E0`, `59210`, `725A0`, `6CDD0`, `725E0`, `E3B90` and `D95A0`.
Read them using `tools/semantic/query.ps1 function <RVA>`; no exports changed.

`51790` obtains the player's village slot from player resources+`22FC`.
The AIV state has a four-byte initial word and village stride`6D98`; each
build-step record has size`C`. Its state, mapper and packed single-building
anchor resolve at system+`38`, +`3A` and +`40`, plus the village/frame terms.
Drawbridge mapper105 follows the ordinary single-building branch, with
`7A170` preflight/orientation, `5CD90` preparation, `C3BF0` collision validation
and then `6D580` placement. Restricted mode exits early for this mapper;
several other native conditions can also leave placement unchanged.

`6D580` explicitly skips its unit deletion/relocation loop for structure49
(drawbridge). `739C0` allocates through `B47E0`, enumerates the5x5 footprint
using `69850`, stores the building ID and sets the bridge table's solid support
cells to `IsBuilding` (`400` hexadecimal). It clears completed-moat markers
and updates decoration through `59210`/`725A0`. `6CDD0` updates access data;
`725E0` performs footprint neighbor/path updates. Final placement performs
`E3B90`/`D95A0` path invalidation/update before returning. This selected
drawbridge branch does not call `6CB20`. These facts explain how a surviving
unit identity can have its current terrain changed underneath it.

## AIV observation boundary [mod integration]

The public APIShared AIV broker, activation, bounded occupancy snapshots,
conservative eligibility and scoped post-original recovery are testmod policy,
not Vanilla behavior. Their exact integration and remaining evidence limits
are in `Testmods/TrappedUnitRescueTest/AIV-Observer-Audit.md` in the workspace.
This addition claims neither game-test success nor coverage for human/editor
placement, completed moat digging, retrospective save repair or multiplayer.

## Dismount placement versus later engineer motion (2026-11-17) [Vanilla facts]

For the current canonical native hash, the synchronous targeted tribe command
`11E960`, action17 (`0x11`), validates each recorded crew global identity and
searches around the machine's current71C/71E using `F0030`. It writes the selected
engineer current/next/fine coordinates and tile IDs, calls `18BE50` and `196FD0`,
then clears the machine crew count. The engineer starts state109 (`0x6D`).

`F0030` is a component-constrained breadth-first search, not a search with a
16-tile distance cap. Its comparison `<0x10` tests the absolute difference between
queried machine standing-height inputs (native712+714 of the device ID passed
to `F0030`) and the candidate's `6B910` height result. The
queue can traverse the reachable component; the dismount caller also retries
candidate offsets and has a machine-current-tile fallback. A fixed horizontal
radius16 is therefore not a general native placement guarantee.

In `14AED0`, state109 advances its animation counter and, once the state timer
exceeds32 updates, selects the stored successor state (zero in the inspected
explicit dismount path). Ordinary engineer behavior can then continue. A position
observed48 simulation ticks after the command is not necessarily the position
chosen synchronously by dismount. Placement evidence must be captured at command
return; later identity, unmounted state and occupancy are separate observations.

### Historical fixture observation [mod integration]

The failed test assertion in run052 measured proximity only after48 ticks and
called any distance above16 a historical-coordinate error. That message alone
does not establish either historical-coordinate placement or a CastlePlanner
regression; the relevant original positions were not logged in that run. This
clarification does not claim that a subsequently changed fixture or game run
passes.

## Ordinary riding occupancy and unproved auxiliary coverage (2026-11-17) [Vanilla facts]

In the audited ordinary `1855A0` locomotion chain, each substep unlinks the old
current occupancy through `186AD0` and registers one current/next occupancy through
`180230`/`1802A0`. Direction-specific midpoint thresholds come from `335C98`;
the transient dword at native740 selects the next coordinates/tile for that
registration and is cleared again before the ordinary call returns. This is not
evidence of two simultaneous occupied tile chains.

Native6B8 (typed GameUnit5C, currently `N0000010A`) is read and cleared by `187110`
when it attempts an additional unlink. The ordinary movement chain does not write
a nonzero value there. The checked `1988F0` connection dispatch does not establish
a nonzero producer either. Searches of the current semantic baseline for direct
6B8/absolute1867E8AB8 and indexed1AE/35C access, cross-checked against executable
instruction displacements, did not identify a nonzero unit-record producer. Other
matching direct writes inspected at F80E0/103370/1036EE use0xE4-stride records and
zero values, and19D960 writes a return/UI buffer; they do not establish a mounted
unit auxiliary tile. This bounded audit does not prove that arbitrary saved,
foreign-modified or other unclassified states can never contain such a value.

### Consequences for fixture coverage [mod integration]

Waiting for a later ordinary riding substep is therefore not an established way
to obtain a distinct occupied6B8 tile. A fixture must retain and check any real
observed auxiliary occupancy, but report that coverage as not executed when none
was observed; it must not fabricate the field or require it as a prerequisite for
the independently observable current/next recovery cases. Natural approach
snapshots can separately verify a before-midpoint Current1/Next0 occupancy and a
later Current reaching the previously observed Next with old occupancy0/new1.

## Rejected publication and a mode0 Apple on new moat, 2026-11-17 [Vanilla facts]

Identity remains CURRENT native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Complete relevant function bodies are reproducible through
`tools/semantic/query.ps1 function` for `196280`, `196840`, `F4930`, `E32B0`,
`199CD0`, `1855A0`, `DCE60`, `DCD60`, `135E30`, `188340`, `19BA40`,
`186AD0`, `186AF0`, `180230`, `1802A0` and `18BE50`.

Before its actual builder call, `196280` clears the unit-owned1000-byte path
slot at manager+`B4FE78`+unitId*1000 (image RVA`7338278`+unitId*1000).
The builder-failure branch does not restore those bytes or clear every previous
route flag/length. The stored moat word at native`9C8` is written on successful
publication, not on this failure. An old positive length and flags2/4 therefore
do not establish that old route bytes remain valid after a rejected rebuild.
Selector1 is not an independent fallback search: `E32B0` only reconstructs
existing search state through `E1640(mode0)`.

For class0, marker0, non-Assassin Apple movement, `DCE60` admits only the
ordinary source-outgoing or destination-reverse edge. The Assassin fallback
is type73-only; the moat fallback requires the stored marker. Consequently,
if all eight forward/reverse pairs are absent, this precise Apple boundary
has no physical escape merely because a completed-moat flag now exists.
`1855A0` consumes a packed nibble under the retained route state and, on a
rejected physical step, can call `196280` again toward the stored route endpoint.
That retry happens in the common movement consumer; it need not wait for an
Apple-specific job retry.

`19BA40` resets movement state, unlinks old tile occupancy, changes tile/fine
position and height, relinks occupancy and refreshes render offsets. Its
enumerated helper closure does not write Apple AI state918, employee/home
fields990/992/994/99C, cargo9E0 or picking counter9EC. Apple handler135E30
states5/7 use `188340` before their arrival/job transitions. Preserving these
states while relocating therefore still requires a valid native publication
to the intended job destination before subsequent native updates.

### Run068 observation and receipt boundary [mod integration]

Evidence: `.work/a/rescue-game/068/rescue-moatworker-on/33dada44cfa6/LogOutput.log`,
own timestamps14:42:10.698--14:42:10.700. Unit1/global432, AI5, flags2,
length9, cursor0, substep8 and stored marker0 remained unchanged. The complete
path SHA-256 before and after the real brush was
`20D5C2B4FE77BA4D586BB34E47B92DF39A3E594E4F2A88FBB78A05442AD1D555`;
after rejected native reissue it was the1000-zero-byte hash
`541B3E9DAA09B20BF85FA273E5CBD3E80185AA4EC298E765DB87742B70138A53`.
Source199,413 became flags40008000/height0 and had no forward/reverse edge
to any of eight neighbors; ordinary neighboring banks had height8, while
three neighbors belonged to the granary. This modded run confirms the invalid
old-route-preservation assumption; it does not isolate why the builder failed
and is not presented as Vanilla-only gameplay.

The dedicated testmod receipt, old reachable bank choice, short Fixes gate,
original-goal reissue and physical-arrival assertions are mod/test policy.
The generic completed-moat path remains unsupported; future automatic moat
completion and the newly added receipt have no gameplay-success claim in this
audit. See `Testmods/TrappedUnitRescueTest/UpdateToNewDLL.md` for exact scope.

## 2026-11-25 mutation and in-flight boundaries [Vanilla facts]

Rechecked against the same canonical native SHA-256 above, without game startup.
The detailed offline audit and compact native evidence are maintained under
`Testmods/TrappedUnitRescueTest/Audits/`. Those reports separate native facts,
external adapters, implementation requirements and outstanding game acceptance.

- `0x1855A0` produces an ordinary class0 Next from Current plus one of eight
  neighbour deltas. Accepted substeps do not rerun the new-step connection and
  surface gates. At its midpoint, `0x180230` consumes marker `+0x740` to transfer
  Next into Current before `0x1802A0` registers Current occupancy. `0x182B00`
  additionally calls the movement executor on a copied record for prediction;
  this is not a physical occupancy transition. Ordinary Next-only candidates
  can therefore be found through Current occupancy within a one-tile halo of
  the actual mutation, subject to the ordinary/no-portal state contract. This
  bound is not a portal, transport, tunnelling or climb bound.
- The 800-by-800 coordinate envelope is distinct from native packed tile IDs.
  Packed IDs use X plus the row lookup, with capacity 320800. Native unit loops
  use IDs 1 through 9999; slot zero is reserved in the 10000-slot allocation.
- `0x59210` allocates or updates a moat task; its return is not a completed
  terrain change. Dig writer `0x639C0` increments the signed progress byte and
  commits a completed moat only at progress four: height zero, PlannedMoat off,
  IsMoat on, then topology work through `0xE3B90` and `0xE0850 -> 0xD90D0`.
  Fill writer `0x69470` removes IsMoat and restores height eight only at terminal
  progress, before task deletion through `0x61E70`. Drawbridge `0x739C0` can
  allocate a task and subsequently clear IsMoat again. Neither task allocation
  nor an intermediate tile refresh is a whole-operation completion signal.
- The `0x6D580` creator dispatch includes child plots, direction-dependent Keep
  parts and dynamic secondary placement; the scale square is not a universal
  mutation envelope. Its pre-creator unit loop can relocate or delete units.
  `0x6F230` has independent unit handling and partial-line termination. Cleanup
  `0x5CD90` can cover an enlarged area and linked building removals. A return
  value alone establishes neither unchanged geometry nor survival of a unit.
- Editor deletion `0x62AB0` intentionally marks affected units for deletion.
  Ordinary building demolition marks records first, with terminal cleanup via
  `0xC60F0 -> 0xB8310 -> 0x61FC0` later (or synchronously through `0xC43A0`).
  A queued demolition command or a dead-building mark is not yet terrain removal.
- The twenty economic handler contracts distinguish ordinary travel, entity/
  linked-actor travel, contact/production and retry/terminal states. `0x188340`
  is not a universal geometric arrival test. Relocation followed by stale job
  execution can advance work without arrival; shortening helper `0x198620`
  also changes the stored segment endpoint. Reissuing that endpoint must not
  blindly shorten it a second time. Tanner/cow and hunter/prey have additional
  identity/reservation/linked-actor state, unlike a plain tile destination.
- Tunnel handler `0x12E400` pairs class1/Hidden state with a specific tunnel
  building and reconstructed route. Its `0x185240` work consumer traverses the
  stored full route for terrain/damage effects; terminal AI4 removes the linked
  tunnel and retires the unit. Ordinary relocation does not normalize this job.
- Assassin `0x16CD70` commits Next through `0x180A80` at the end of AI127 but at
  the beginning of AI129, then continues height/animation progress. Retaining
  those states across an arbitrary relocation retains old climb effects.
- Portal/ladders bind serial, object global identity and participant records.
  `0x19AB10` can legitimately wait 96 updates for a height-progress step and
  can retire/kill a participant after a broken active transition. `0x187110`
  performs partial connection/Aux cleanup but is not a complete portal/job/
  Hidden normalizer. A distant Next or slow progress is not itself a trapped
  ordinary unit.

These are static contracts, not newly successful rescue implementations. No
universal freedom to interrupt every AI state or to reconstruct an old reachable
bank from a later world is inferred. Earlier dated game observations retain
their original scope and version identities.

## Construction envelope closure, 2026-11-25 [Vanilla facts]

Identity: the installed canonical DLL was rehashed on 2026-11-25 and still
matches CURRENT.json and the dataset binaryHash:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Confidence is confirmed for the machine contracts below; no new game run is
claimed. Reproduce the containing functions with `query.ps1 function <RVA>`.
The complete branch inventory and independently decoded table/expansion evidence
are in `Testmods/TrappedUnitRescueTest/Audits/CONSTRUCTION.md` and
`Audits/TERRAIN-WORKERS.md`; these are mutable integration reports, so the
reusable native conclusions are recorded here explicitly.

- `0x6A620` with a nonzero brush selector enumerates `0xE3360`, a breadth-first
  expansion, rather than a width-by-width square. Counts at `0x2D1C38` for
  widths 0..9 are `0,1,5,13,25,41,85,145,313,545`. Cardinal directions are visited
  in order 0,2,4,6. A discovered neighbour is counted before its own expansion
  test; a popped tile expands only when its flags have neither bit in `0x30`
  (RealityEdge and MapBorder). Checking only active neighbours would change
  later ordinals. Native scratch is modified by these enumeration functions.
- The relevant cleanup/terrain writers preserve these two expansion bits,
  including terminal removal through `0x61FC0`, its type-specific branches,
  vegetation removal `0x107970 -> 0x71D60`, and refresh helpers
  `0x725E0/0x5B020/0x5D740`. This is a contract for bits `0x30` only: building
  occupancy, height and other flags can change. It does not generalize to an
  arbitrary foreign writer.
- A possible counterexample through `0xB5C40 -> 0x9B2B0 -> 0x9C730 ->
  0x7DF60 -> ... -> 0xFEE50` is infeasible for this cleanup: `0xB5C40` requests
  effect `0x1E`, while that transition uses other effect types. The actual
  handler table `0x2DA9C0[30]` selects `0x9A6E0`, which changes effect animation.
  The dataset's size 1778 for `0x9B2B0` is not a contiguous-function-end proof:
  its actual control flow includes tailcalls at `0x9B9E6/0x9B9F0`, indirect
  dispatch at `0x9BA06`, and `0x9BA16 -> 0x9B468..0x9B47F`. Inspect those
  machine instructions when revisiting this contract; the missing decompiler
  body or an unqualified transitive callgraph cannot close it.
- `0x61DB0 -> 0x619C0` initializes square-offset blocks only for sizes 0..13.
  Nevertheless `0x5CD90` cleanup size 10 reaches size 14 in its second pass.
  `0x69850` then addresses 196 records starting at `0x8EBA90` in adjacent mutable
  native data. Those live X/Y pairs are not an initialized 14-by-14 square.
  Duplicate or distant offsets cannot be replaced by a guessed rectangle.
- `0xC43A0 -> 0xB8310 -> 0x61FC0` can remove distant building records sharing
  the same nonzero compound key (raw building offset `0x304`, typed `0x2A8`).
  `0xB8ED0` additionally searches all 36 stored farm-plot entries under flag
  `0x04000000`, even for farm types with fewer actual plots. `0xB8E00` finds
  farm preparation areas under flag 4. Building-layer zero is not sufficient
  to exclude either path. Apple removal uses preparation size 11, whereas its
  creator preparation is size 10.
- Keep replacement preparation `0x77E60` precedes the relevant `0x5D3A0`
  scratch-dependent branch. The latter includes main area, marker, camp and
  goods parts and finalizes already Alive3-marked slots 1..3999. Main area and
  the originally requested orientation alone do not bound that operation.
- `0x59730` has a low-ground moat path and a high-ground alternative reaching
  `0x6D580` with mapper `0x62` (KillingPit), scale 1 and direction 15. A moat
  task callback alone therefore does not identify the eventual surface writer.
- Dig `0x639C0` and fill `0x69470` first increment/decrement the stored byte,
  including byte wrap, and then compare its signed result (dig >=4, fill <=0).
  Nonterminal calls return before geometric mutation. This refines the earlier
  progress-four summary; it is not permission to ignore malformed byte values.

### Orientation and mapper integration limits [mod integration]

Fixes or CastlePlanner can change the effective Keep direction below the upper
construction observation boundary. The testmod captures the union of all four
native orientations and validates the actual resulting world separately; a
Before callback's requested direction is not treated as immutable. The native
direction/variation distinction remains as documented in KEEP_DIRECTION.md.

An offline comparison of all 440 declared installed SDK mapper values against
native `0xC77C0` and scale lookup `0x6A190` found a discrepancy for legacy
MAPPER_KEEP4/KEEP5: the SDK converts to KEEP_FOUR/FIVE, whereas Vanilla uses
STRUCT_NULL with scale -1. Regular Keep1..3 agree. This is a dated SDK comparison,
not a new native Keep type or a general SDK compatibility promise.

## Movement publication and safe waiting, 2026-11-25 [Vanilla facts]

Same hash and confidence as above. Reproducible containing functions:
`0x196280`, `0x196840`, `0x199CD0`, `0xE32B0`, `0x182B00`, `0x1855A0`,
`0x186AD0`, `0x180230`, `0x143400`, `0x181890`, `0x19BA40`, `0x11D370`,
and `0x118660`.

- `0x196840` tests completed moat from the unit's Current packed tile `+0x72C`.
  `0x196280` can therefore select moat escape planning for an idle unit too.
  Before search it writes requested X/Y `+0x744/+0x746`, clears portal `+0x8EC`
  and `+0xA4C`, and can clear the unit's 1000-byte route buffer. Failure can
  leave changes to `+0x900/+0x92A`; type `0x52` also receives `+0xA7E=100`
  on the failed-builder branch. These are real writes, not a read-only probe.
- On failure `0x199CD0` can assign AI110 and clear `+0x986` when `+0x8FC=0`,
  Current has Wall bit `0x100`, type is neither Lord, Lady nor Jester, and
  `0xE9D90` returns nonzero. A simultaneous Moat bit does not bypass this Wall
  branch. Returning zero from a move is not a guarantee that AI is unchanged.
- The same-start/goal branch of `0x196280` returns 1 with zero path length and
  flags zero. Thus return 1 alone is not proof of a positive route or physical
  movement. Selector 1 uses `0xE32B0` reconstruction of existing search scratch;
  it is not an independent fresh search or a read-only fallback.
- `0x182B00` dispatches the type handler and subsequently the common movement
  executor. Flags 2/4 can therefore advance an accepted ordinary step while AI
  is idle 0/2; forcing AI101 is not required by that physical executor. However,
  the type handler and other earlier AI consumers can independently select
  targets or change intent. `0x1855A0` also respects pause `+0x8F8` and physical
  edge/portal checks. A published route is not proof of eventual arrival.
- Path flags 5 prevent `0x188340` from reporting normal arrival, but do not
  freeze all unit logic or undo a pending midpoint marker. `0x186AD0 ->
  0x180230` can consume nonzero DWORD `+0x740` to commit Next. A stable parked
  pose additionally requires substep 8, coherent Current packed ID, equal
  Current/Next XY and a zero DWORD marker; flag 5 alone does not establish
  those conditions. Packed Next73C is an inactive cache at that boundary and
  need not equal Current (see the dated 26.11.2026 correction below).
- `0x19BA40` clears flags `+0x74E`, DWORD `+0x740`, and writes DWORD `+0x750=8`
  (substep8/cursor0). It does not clear requested `+0x744/+0x746`, stored moat
  `+0x9C8`, or old length `+0x754`. Combining relocation with a rejected search
  is not a generic worker-arrival or old-route restoration mechanism.
- `0x11D370` writes the tribe ID at unit `+0x930` and stamps its global identity
  at `+0x93C` from tribe record `+0x34` (stride `0x688`). Independently,
  `0x118660` checks that stamp against the currently referenced tribe record
  before deciding whether to reassign membership. An unchanged numeric tribe
  slot alone does not prove unchanged membership identity.
- Type dispatch in `0x182B00` uses pointer table `0x321CB0`. Constructors
  `0x19A240/0x19C130` initialize unit `+0xA9E` from `0x3226B0`; `0x19D960`
  can override it through a Lord definition. The table's value 1 also occurs
  for animals. That field alone is not proof of a military consumer or safe
  command-resume behavior. Trebuchet handler `0x1535F0` is a separate owner:
  its AI101-to-0 branch does not use `0x188340` as an arrival test.
- The military dig/fill command selector is native `+0x9F4`, not `+0x956`.
  For example, complete handlers `0x143400/0x14AED0/0x174F90` test values 6/7
  before the `0x69D60(1/2)`, `0x6AF60`, `0x196280` and conditional `0x198620`
  sequence. A value 6 in work-mode metadata `+0x956` alone is not a dig order.
- Siege attach `0x1520D0` stores engineer IDs/globals on the engine at
  `+0x96C/+0x974`. Engineer AI5 instead uses its own DWORD `+0x974` as a
  watchdog. The engine pass in `0x182B00` refreshes matching global/AI5 crew
  entries up to the recorded crew count. Those fields do not establish a
  reverse Engineer-to-engine pointer at `+0x9F6/+0x9F8`; a rescue must not
  manufacture one from their numeric contents.

These facts constrain rescue policy; they do not establish that every military
idle, worker state or failed path can be resumed safely. The new managed owners,
their bounded receipts and offline fixtures remain implementation evidence.
No additional installation, in-game or multiplayer acceptance is asserted.

### Command phase limits, 2026-11-25 [Vanilla facts]

The same-hash continuation audit also closes these relevant counterexamples:

- `0x193B80` writes path flags 5 AND copies current AI `+0x918` into `+0x91E`.
  It is not a side-effect-free replacement for a single arrival-barrier write.
- Engineer `0x14AED0` AI0 can enter payload work without `0x188340`, select
  dig/fill, or invoke a group role. `0x199C70` wraps `0x11E960`, command 15;
  that producer can assign Engineer AI8/9 and building identity `+0x98E/+0x6F8`.
  An empty route and AI0 alone therefore do not prove an idle no-job engineer.
- The Archer, Arabian Bow, Slinger, Grenadier, Ambusher and Skirmisher handlers
  (`0x13F540/0x167D40/0x16B220/0x170570/0x1768A0/0x178350`) have no top-level
  AI1 case. Crossbow `0x141DA0` AI1 instead reaches the shared animation tail
  `0x142DF2/0x142E0E`, selecting AI0 and `0x181890`; Slave `0x169B70` has the
  corresponding tail `0x16AE2C/0x16AE32`. Numeric phase equality is not a
  cross-type semantic contract.
- `0x11E960` independently supplies the `+0x9F4` command producer: it writes
  the command payload there, with separate branches assigning 3/4/5. Together
  with the handler readers above this closes the producer/consumer distinction
  from the unrelated `+0x956` work-mode word.

The complete per-handler matrix and its rescue admission consequences are in
`Testmods/TrappedUnitRescueTest/Audits/COMMAND-RESUME.md`. The matrix's `R/N`
labels describe mod policy, not new Vanilla states or gameplay acceptance.

### Selected vegetation index and lifetime, 2026-11-25 [Vanilla facts]

The same-hash `0x108040` returns its selected array index only when the final
candidate satisfies `0 < index`; its failure return is zero. Apple handler
`0x135E30` explicitly branches on zero before storing a successful return
unchanged at unit `+0x9A0` (state2/state8). Zero is therefore no selected tree
for this contract, not a public one-based ID or an admissible selected slot.

Woodcutter `0x12BC00` likewise stores the positive selector result unchanged in
unit `+0x9A0`, computes `index*0x9C`, and copies the selected record's global
identity into unit `+0x700`. Apple does not have that equivalent selected-tree
lifetime stamp in its audited travel contract. Re-reading an Apple slot's
global only after an intervening removal/reallocation cannot establish that it
is the previously selected lifetime. A stable slot number alone is insufficient.

### Current SDK vegetation boundary [mod integration]

The installed Script Extender 2.14.6 getter was independently read again:
`TryGetVegetationById(id)` accepts `1..Length` and returns array base plus
`id-1`. Its public span retains that base. The testmod now shares one adapter
between runtime and fixture: translate the positive native index to `index+1`,
check strict native index bounds, and verify the returned address equals span
base plus native index. The causal snapshot separately captures the selected
tree global before mutation; recovery and saved pending records cannot adopt a
new lifetime from the same slot. These are integration safeguards, not a claim
of a defective one-based SDK contract or completed worker gameplay acceptance.

### Failed-order trigger versus spatial authority, 2026-11-25 [mod integration]

The testmod's P7 integration reuses the final existing UnitMove owner; it adds no
native hook and does not interpret native196280 result0 as proof of entrapment.
An unreachable destination from legal ground remains a valid native refusal.
Spatial authority comes only from an earlier observed construction/terrain
capture, separated from a newly captured active typed intent. A positive later
native publication cancels the retained authority even when its Begin belongs
to an older outer call than a nested failed call. Completion precedes mutable
legacy Post and native caller tails, so actual rescue stays at a later admitted
simulation boundary with fresh validation and transient save gating.

The original local candidate frame remains anchored at original Current. Moving
to the one originally accepted Next does not rebase its radius8 search to another
origin: an old legitimate candidate can then be nine coordinates from the fresh
origin. Geometry codec and canonical join preserve both frames. These are tested
managed ownership rules; they establish neither a universal physical failure
sensor nor acceptance of native gameplay/save/worker behaviour. See testmod
Audits/MOVE-FAILURE-SOURCE.md, IMPLEMENTATION.md and INGAME-PENDING.md.

### Automation lord ID domains, 2026-11-26 [mod integration]

The actual referenced Script Extender2.14.6 AILords domain uses built-ins1..29
(SK_NULL0, SK_X1 excluded), whereas FRONT_Multiplayer.SkirmishAIAddClick and
MPLobbyMember.GetLordType use lobby built-in indices0..28. The latter reader
returns (SteamID-1)/8 and the producer stores the passed lobby type. In contrast,
GamePlayerManagerAPI.GetAILord reads the one-based native AI lineup as AILords.
These are distinct boundaries; subtracting at the native lineup reader is wrong.

The isolated development SDK's StartSkirmish previously passed the enum integer
directly to the lobby, making requested SK_RAT select the next built-in instead.
Its effective Add/Count path now converts the validated enum at that boundary.
Two regression tests cover all29 accepted built-ins and rejected sentinel,
custom/undefined values; fullSDK1789 tests passed. This is an API integration
correction, not a new native RVA/layout finding or proof of game acceptance.

The new rescue fixture pins Rat Default1 source AIV data and catalogue metadata
before Play, with native starting slots/peace/prebuild and actual native lord
identity checked after loading. The input AIV hash alone is not a native plan
readback. Game acceptance and final source/package identities are tracked in
Testmods/TrappedUnitRescueTest/SP-ACCEPTANCE.md and .work/a/rescue-sp/001.

## 26.11.2026 — inactive packed Next after ordinary relocation [Vanilla facts]

Reference hash remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; the installed canonical DLL and the selected dataset were rechecked. This corrects the earlier overstatement that both packed tile IDs must agree at a normalized stationary boundary.

- Complete 19BA40 writes Current72C coherently, copies its selected XY to Current71C/71E and Next738/73A, clears the full DWORD740 and writes DWORD750=8 (substep8/cursor0). It does **not** write packed Next73C. Callees186AD0→186AF0 unlink and180230→1802A0 relink occupancy; with marker740=0,180230 consumes Current72C. The final18BE50 updates rendering offsets, not73C.
- Complete1855A0 returns from its flags5 branch before199E10. Its ordinary active-step path publishes fresh NextXY and packed73C before consuming that step or calling199E10. The latter's only ordinary caller is1855A0. The fixed global aliases of73C found in the writer/reader review are writers; the separate copied scratch alias provides no later read authority.
- Reissue196280→19B1B0 uses CurrentXY for a stopped substep8 pose, otherwise NextXY;19B1B0 derives normalized XY from Current72C. This path does not require the inactive packed Next to equal Current. A successful route subsequently publishes the new accepted step before its packed Next is consumed.196840 uses Current72C;18BC00 is rendering work.
- Concrete legal output: accepted A→B followed by19BA40(A) leaves packed Next=B while Current=A, NextXY=A, substep8 and the full midpoint DWORD=0. Relocation A→C may similarly leave the inactive cache atA. Do not invent a write to73C merely to make a managed guard pass. Active substep<8 still requires a coherent accepted packed Next.

## 26.11.2026 — normalized-pose consumer regression [mod integration]

The prototype's WorkerResumeWorld parking and IdleEscapeWorld stationary predicates incorrectly required Current72C==Next73C even with zero midpoint marker and substep8. The common read contract now retains coherent Current, equal Current/NextXY, substep8 and full DWORD740=0 while ignoring only that inactive cache. No native hook or extra native write is added. The typed-record regression in StartedStepFixture rejects the previous predicate with the exact A→B/relocate(A) state and retains negative Current/XY/substep/low+high-marker tests. Offline before-fix failure and after-fix results are under `.work/a/rescue-sp/001/normalized-before-fix.log` and `normalized-build.log`; this alone is not a game acceptance claim. The original run086 failure and the planned owned-reissue post-call readback are separate runtime evidence.

Actual run090 (`.work/a/rescue-game/090/186265f22096/LogOutput.log`) confirmed the native post-call state before owned reissue: Current150410, inactive Next150411, NextXY equal Current266,387, full marker0, substep8, flags5. After the corrected read predicate the same original unit1/global998 published exactly one own Native1 continuation and physically reached its unchanged goal276,387 with identity/health/tribe/occupancy preserved. The otherwise identical Next-only on case in086 failed at parking with the old predicate. This closes that specific runtime attribution; it is not a full worker/idle/save acceptance.

The same inactive73C equality was also removed from the fresh failed-order handoff reader. Current72C, complete marker740, SS8, XY, native-result, AI-policy and lifetime guards remain. Existing CommandJourneyFixture now includes stale-cache handoffs across flags0/2/4/5 while retaining the negative guards. Integrated offline result: `access-matrix-build-final.log`; the actual foreign failed-order/save scenario is still pending.

## 26.11.2026 — ordinary step boundary and exact Moat brush [Vanilla facts]

Same canonical hash as above. The complete1855A0 path increments750 at185815; when an intermediate step reaches8 and cursor752 still precedes length754, the same call resets750 to0, validates/publishes the following Next and continues its first movement. No ordinary intermediate-tile tick ends atSS8. Endroute instead writes DWORD74E=0x80000 (flags0/SS8); error/special branches can also writeSS8.196280/19B1B0 do not create a newSS8 boundary during an accepted step. Fresh ordinary worker routes can begin atSS8, but this is not evidence that normal transit offers the same sampling window. The full135E30/188340/196280 and movement-helper chain was rechecked; slower polling cannot create this missing phase.

For6FE90, resolve the actual operation value first: IsMoat is40000000, so it selects69850 rather than generic6A620/E3360 BFS.61DB0 calls619C0 for sizes0..13.619C0 zeroes each169-record block (169×24=FD8 bytes) and fills the first size-squared XY records for sizes>1, leaving unused entries zero.69850 reads the requested ordinal from block8DDCC0+(size×169+ordinal)×24;6FE90 subtracts floor(size/2) from both axes. Width2 uses count5 from2D1C38 but only four unique initialized square offsets; the fifth ordinal repeats zero offset. Its unique affected rectangle is [centerX-1..centerX]×[centerY-1..centerY], not a five-cell diamond. Current and an adjacent accepted Next fit when center uses the coordinatewise maximum (with either appropriate pad on an unchanged axis). Generic non-Moat brushes still use the separately audited BFS contract. NativeMutationFootprints already distinguishes the40000000 centered-square case; this is a fixture correction, not a changed production footprint algorithm.

## 26.11.2026 — Miller registration and event timing [Vanilla facts]

1377C0 registers each Miller through native building214+WORD(unit9EE)×2. The GameBuilding view begins at native5C (global134 maps to typedD8, access15A maps to typedFE), making that three-WORD array typed1B8. It is separate from generic employment fields. In successful input phaseR3 the handler first commits AI4, then calls B6100 for RawWheat-1; an event at that goods operation observes AI4. Flour output inR8 calls B6100 with AI8 and positive cargo still present, then clears cargo after return. The acceptance reader keeps exact Unit/context/slot/building-global/body-position and actual physical goods-delta checks; an event name alone is insufficient.


## 26.11.2026 — Archer stationary animation and absent AI1 [Vanilla facts]

Canonical SHA-256 was rechecked against CURRENT.json: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Public CreateUnitLocal reaches17FEF0/type initializer19A240; removed records are cleared by1869C0, and182B00 changes NeedsInit to alive. The installed/source Archer enum is22. Dispatcher182B00 indexes321CB0 by that type; raw slot321D60 contains18013F540. The machine switch reads byte141CF4[AI] and dword141C98[selector]. AI1 selects22 and returns through141C7D; this is not a missing decompiler case. AI7/8/11 select1410B2/1411CE/1412F6.

The complete13F540 branches establish that7 only performs idle facing197BC0 and animation before returning to0;197BC0 writes direction90C/6AC, not an order. AI8 performs idle animation using9EA/9D8 and then selects0 and181890. AI11 selects longer idle animation variants and likewise reaches0/181890. None of these branches applies damage, produces goods or resumes a worker job.181890 derives positional/occupancy recovery from actual Current and may publish101 through196280, or relocate through19BA40 for its separate terrain case. Such native changes remain authoritative.

Combat/aiming is owned by0/2/101 and4->5->6, including target acquisition18E9A0. Ranged adversary99C and global6F8 are independent of context9F6/9F8 and tileA00;99C also acceptsFFFF/FFFE aiming sentinels. The selector handles explicit commands4/5/9/22/23/34. In particular command5 can install tile aiming fromA40/A42 even with9F6 andA00 zero. The lost-command4 branch can consume deferred748/74A and publish101. Thus zero9F6/A00 or an idle-looking animation does not prove absence of native intent.

### Testmod consequence [Mod policy, not a Vanilla restriction]

Only stationary Archer7/8/11 gain a new idle proof. Their phase-specific immutable schema extends existing common fields with748/74A; existing type/phase layouts are unchanged. The new family admits command0 or completed MoveHerePosition3 only, with zero996/998,99C/6F8,9F6/9F8,A00 and748/74A. Ordinary normalized pose, Current=requested, owner/lifetime/tribe, old ordinary-to-new-Moat causality and all existing special-mode exclusions still apply. AI1, other types with the same numbers and active continuation of7/8/11 remain excluded. Final-original source checks and refusal fallback revalidate the immutable intent; native AI/target/order changes supersede the read-only monitor without restoring old AI. Actual run098/a6d87d24b416 showed a natural ArcherAI8 with normalized pose and zero logged targets being rejected before this extension. This observation is a qualification failure, not an in-game acceptance of the extension. New offline fixtures are source-only until the coordinating build runs.

## Miller stockpile-global and moat placement range,26.11.2026 [Vanilla facts]

Canonical DLL rehashed againstCURRENT.json and DATABASE_INFO.json: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Full1377C0 plus B9280/BF9F0/B9AA0/C6EA0/B6100 chain and direct PE displacements were checked. Miller stockpile lifetime is unit native6F8 (typedGameUnit+9C), paired with native98E. It is not700 (typed+A4, role target context). Writers:137AC4(inputR1→3),137DFA(inputreselection),13814C(outputR7→8),138352(outputreselection),138443(nextwheat). Readers137CCA validates input lifetime;137D77 passes6F8 toB6100 wheatdebit(call137D86);138272 passes6F8 toB6100 flourcredit(call13827D). The entire Miller handler has no700 read/write. Selectors returnBuildingID; the caller copies BuildingGlobal(native134,publicD8) into6F8.

All20 audited worker handlers agree on the storage lifetime domain: the18 roles with storage phases use6F8; Mason131430 andMiner1 14D6F0 have no own storage binding. Woodcutter700 holds vegetation serial; Hunter700 holds target context, neither changes the stockpile contract. Inactive role phases may legitimately retain0/stale6F8; only a phase requiring an external store may validate its bound storage lifetime.

Moat EditorTileType6FE90 outside editor has world-level admission gates before physical tile writes: EE640 enemy-unit Euclidean radius15+width/2; EE320 enemy-building/wall BFS radius7+width/2; EEF90 own/allied keep reach. EE320 writes BFS scratch and is not a pure query. Own range uses6AF00(mapSize)+5+width/2; allied reach has separate reduced rules. For a400-wide map the own base is70, so width1 requires the own75-tile allowance. Final eligibility includesHeight<13 and59210, which is a writer/allocator over the64000-slot moat domain, not a read-only placement query. Test/source selection must respect the range rather than forging editor state or invoking these writers as probes.

### Fixture consequence [mod integration]

A legitimate Miller with actualStoreGlobalG,6F8=G and700=0 remains valid natively. A fixture comparing700 toG wrongly rejects it; this is a test setup defect, not a reason to relax runtime bindings. The natural Miller fixture now uses6F8 for exact store lifetime, and logs700 separately as raw target context. Run100 failed acquisition before any moat writer; a repeat with raw original fields is still required to attribute its observed qualification failure precisely.

Run095's real charged Ox stood far beyond ownKeep reach when the actual moat writer rejected placement. The new resource fixture puts its delivery stockpiles near its own keep and waits for all four original storage records to reachIsAlive before the native capacity query. These setup changes are not worker rescue acceptance.


## 26.11.2026 — ordinary movement cadence and published substeps [Vanilla facts]

The canonical DLL was rehashed with Get-ApiRepositoryHash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, matching CURRENT.json and its database manifest. The complete182B00 and1855A0 bodies were reread under that binary_hash, including real/speculative calls, caller skips, animation skips, route exhaustion and failed-edge branches.335C98 was read from the matching canonical PE: direction thresholds are4,4,3,3,3,4,4,4.

For ordinary movement182B00 first increments native unit DWORD9AC. It skips the real1855A0 call while signed32(9AC-9A8) <= signedWORD74C + signedWORD9A2 + signedWORD916; on admission it writes9A8=9AC and passes signed916 as the movement budget.1855A0 adds signedWORD A2A, initializes its loop counter to0 and continues while the incremented counter <= that budget. Thus916=1/A2A=0 admits two inner iterations, not one. A new path step resets750 to0 and performs its first physical substep in that same iteration; the second iteration publishesSS1. Subsequent admitted movement calls publishSS3 andSS5. With northward direction0, the innerSS4 iteration commits Current to the accepted Next through180230; the published end state isSS5 with Current=Next and raw740 cleared. The speculative param4=1 call remains separately restored by182B00 and is not physical evidence.

The per-inner-iteration animation gate reads BYTE985, not BYTE984: mode1 admits signedWORD9D6 phases4..11, mode2 phases5..9, and other modes reach the ordinary increment branch directly. Closed gates can skip movement even when the caller budget loop executes. BYTE984 instead appears in the distinct failed-step cleanup branch. The already audited occupancy/render helpers do not supply a hidden single-substep boundary. Endroute/failure/special paths retain their separate contracts; the arithmetic above is not a universal promise of motion on every simulation notification.

Acceptance-fixture consequence [test policy]: use genuine published preSS1 or postSS3 on the observed native916=1/A2A=0 Spearman, require an open animation gate and a due next caller update from live9AC/9A8/74C/9A2/916, then verify actual next-tickSS3 orSS5 before ordinary recovery. This disposable fresh-unit fixture rejects wrapping/reordered counters rather than reconstructing them. It never writes speed, counters, animation, AI or pose. Historical run108 (61ae00394621, LogOutput.log2212..2222) exposed the incorrect916=0 fixture prerequisite; it supplied no runtime post-midpoint recovery proof.

## 26.11.2026 — Baker input, output and production event ordering [Vanilla facts]

Canonical DLL rehashed against CURRENT.json and DATABASE_INFO.json: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Complete138850 was read from the matching semantic record (raw hash9147865051044C84A0DC70D8BFAB8D896CC6FA97AAD8433FF4271309EF2BC789); the feature audit also closes1988B0, BD470/B9280 and the existing BF9F0/C90E0/B6100/19B930/188340 contracts. The role's active storage identity is98E/6F8. Native storage mapping2E76D0 routes Flour16 to Goodsyard10 and Bread10 to Granary19; use the actual extender enums in code rather than copying these domain numbers.

Successful R7 Flour pickup computes the natural input yield, validates the home journey, commits AI8 and then calls B6100(Flour,-1,32,Add=true). The event therefore sees AI8 and the already published positive cargo. R8 returns home through188340 and commits109/saved4; production4 changes to3. On completion production3 commits DWORD AI/SavedAI=0x5006D (109/saved5), calls18D940 with base8, and only afterwards stores the returned bread cargo. An output-yield Post does not by itself prove that the caller has stored its returned cargo yet. These production phases are not admitted worker travel phases.

R5 selects a Granary with BF9F0, publishes196280 to its access, stores98E/6F8 and enters R6. R6 waits for188340 and current C9080 body proximity plus BD470(Bread,250). If either storage condition fails it can legitimately select another Granary, update98E/6F8 and publish a new route while retaining the bread job. A successful delivery calls B6100(Bread,+1,250,Add=true) once per bread, and decrements cargo only after that function returns; both goods Pre and Post still see the prior positive cargo. After the final bread it selects fresh Flour and publishes R7. Native building214's worker registration is written in3/4; there is no basis for an invented continuous registration write/check in R6.

Fixture consequence [test policy]: prove every credit's physical delta, the original actor/workplace/storage lifetime and actual body contact; distinguish a complete bread batch from its first +1 event. Require a fresh Flour debit and actual production-yield event before the second full batch. A demand-bound observation of the post-batch zero cargo prevents a GUI sample from missing it after the next ordinary ingredients journey begins. This observation does not write AI, cargo, saved phase or yield and does not replay native production.


## 26.11.2026 — Iron workshop production and delivery [Vanilla facts]

Canonical DLL SHA-256 was rechecked against CURRENT.json and its database manifest: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Complete Blacksmith13BDD0 and Armourer13CF30 were read under that binary_hash, including initialization, ordinary travel, resting/terminal branches, storage reselection and production. The canonical dispatch table321CB0 maps native types19/20 to these handlers. The audit closes the existing B9280/B9AA0/C90E0/C9080, shared-Armoury C6EA0/C6FC0, B6100, B6240, 19B930/188340, 196F80/196FD0 and18D940 contracts. Domain numbers below document native machine semantics; fixture code uses verified actual Script Extender enums.

Both roles pick up Iron6 through B6100(Iron,-1,48,Add=true), after committing AI3 and positive cargo1. Their original workplace is990/global-linked identity, and home is992/994; selected external storage is98E/6F8. At home Smith commits AI6 before B6100(Iron,+1,48), clears cargo only after return and hides through196F80. Armourer instead commits109/saved6 before that credit; its fade continuation subsequently hides. Both consume the actual local Iron via B6100(Iron,-1,48) while AI6, then start B6240 work and commit AI5. Ordinary native hiding unlinks occupancy without killing/replacing the worker; production observations must not impose a visible-unit occupancy invariant while hidden.

Smith13BDD0 reads desired product at building2EC and copies it to latched2EA. The real GameBuilding view begins at native+5C: these are public typed fields r_ProducedGoodId and r_NextProducedGoodId respectively, whose source names alone do not establish their meanings. Output is captured Maces21 or Swords22. After fresh-Iron work is ready, Smith resets workshop work, unhides through196FD0, commits AI7 DIRECTLY and calls18D940(unit,1,true); it assigns returned cargo only after the call. It does not enter109/saved7 here and does not insert a local product credit/debit on this fresh-Iron path. Its separate AI6/no-local-Iron branch withdraws an existing latched local product before the same yield; a fresh-input test must distinguish that fallback.

Armourer13CF30 instead uses constant MetalArmour24 and does not read either product word. Completed AI5 work calls B6100(MetalArmour,+1,48) while still AI5, then commits6. With local Iron exhausted, AI6 calls B6100(MetalArmour,-1,48), resets work, commits109/saved7 and calls18D940(unit,1,true), assigning cargo afterwards and then unhiding. It is incorrect to require Armourer product-word contents or Smith's direct-AI7 sequence for this role.

Both R8 handlers deliver one product per B6100(product,+1,50,Add=true), with native body proximity C9080 and exact selected storage lifetime. Pre and Post still see the prior positive unit cargo; the caller decrements it only after return. B9AA0/C6EA0 Armoury acceptance shares50 slots across all25 physical goods columns, rather than providing50 slots per product. R8 may legitimately replace98E/6F8 and issue196280 to another actual Armoury access after the old destination fails proximity/capacity. A test must first preserve the owned original continuation and then prove any actual native handoff, rather than restoring an obsolete goal.

Fixture consequence [test policy]: the paired Iron-workshop harness observes full physical first and second output batches for the original naturally employed actor, with one fresh external Iron debit, exact role-specific home credit, local Iron consumption and natural production yield between them. Armourer additionally requires its actual local product +1/−1. Smith requires unchanged captured/desired product but tolerates its native hidden production phase. No role, cargo, production-state, product-selection or yield fabrication is authorized by these facts.

## 26.11.2026 — Public moat order producer precedes task approach [Vanilla facts]

Canonical installed DLL rehashed as FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Existing SE GameTribeManagerAPI.DigMoat calls IssueTargettedCommand with command6; its managed detour signature uniquely matches native11E960 in this image. The complete relevant switch branch and common return were read from the matching full semantic record (the query's40000-character preview truncates this69249-character function). The complete11B520 body,196280,198620,188340 and the existing Spearman143400 task producer/consumer were cross-checked. No new native hook is proposed.

Commands6/7 share the11E960 branch which first calls11B520 with the effective command coordinates (machine callsite120EEA), ignores that call's result and then writes the unit's9F4 command (120F9D). Thus a successful public command return is not proof that its group move succeeded or that a particular task was selected.11B520 publishes ordinary group movement/AI101 (AI writer11BF90, route call11C057), and its own common tail writes command3 before11E960 replaces it with6/7. Command7 has an earlierE7F60 approach search; failure there can return1 before the common command-writing branch. Task selection is later, in the unit handler:69D60 searches qualifying tasks and uses the command coordinates as a bias, not an exact task identity requirement. The subsequent Dig approach uses6AF60 mode1,196280 and198620(1).198620 rewrites the native shortened endpoint; it is not a substitute for the preceding public group move and must never be repeated by a recovery implementation.

Fixture consequence [test policy]: a genuine bank lifecycle control may issue public command6 at its actual adjacent bank/search anchor, leaving native69D60 to select the neighboring planned task; it must prove the selectedA10 task index,A0C/A0E task coordinates, actualAI124 and physical bank pose at the real writer. The task-standing control instead uses its actual task anchor and separately proves that pose. AI124's188340 gate alone does not prove physical contact because ordinary Flags0 is accepted without a distance check. Historical run128 stopped on a combined pose guard before recording the first writer pose: this static producer fact explains why the old setup was not a guaranteed bank setup, but does not establish run128's actual Current or exact failing subcondition. No game acceptance follows from the corrected fixture source.
## Spawn capacity, Extreme lobby flag and bounded tribes (26 November 2026) [Vanilla facts]

Canonical Native SHA-256 rechecked against CURRENT.json and the selected semantic records: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. The real managed assembly matches BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789. Complete relevant bodies17FEF0,26370,119C00,11D370,11E820 and119F90 were read; setup readback20420/84280 and ordinary save/load serialization references were checked. These are confirmed static contracts, not higher-population game-test results.

Public SE CreateUnitLocal multiplies local XY by8 and calls CreateUnitWorld/the existing spawn owner17FEF0; native spawning adds the tile-centre4. The physical array has10000 slots with0 reserved, and the exposed span starts at slot1 with length9999. Separately,17FEF0 searches IDs below signed global3668E34: normal3000 permits usable IDs1..2999, Extreme10000 permits1..9999. Array length alone therefore does not prove actual currently selectable spawn capacity. Removed records may remain occupied until ordinary native cleanup; every requested fixture spawn must still be read back by actualID/global/type/owner/XY and initialization state.

The ordinary setup applier26370 reads transfer DWORD0x12: zero writes3668E34=3000 and3668E38=3000; nonzero writes10000 and6000. The managed MultiplayerSetupTransferData layout contains ten scalar DWORDs, eight Keep-order DWORDs and then extreme_troops, matching that0x12 word. EngineInterface.setMPSetup copies the public MultiplayerSetupData.extreme_troops into this transfer field. Thus new high-population fixtures can select the real normal frontend Extreme option before launch; they do not need to write native globals or change an in-party rule. The secondary3668E38 is recorded here only as the paired3000/6000 write; its complete recruitment/owner-limit semantics are NOT established by this spawn-cap audit.

Public EngineInterface.getMultiplayerStartingData allocates the normal transfer buffer, holds threadLock, calls DLL_GetMultiplayerSetupData84280 and converts via getMPSetup.84280 delegates to20420, whose output DWORD0x12 is `(3000 <3668E34)`. The actual real managed getter and MultiplayerSetupData.extreme_troops were checked public via Cecil. This readback identifies the ordinary mode; it is not an unrestricted numeric raw-cap getter for arbitrary externally modified saves. The known normal lobby application plus owned ordinary save/load gives the numeric3000/10000 provenance.52B0 saves3668E34/E38,87D0 restores them for supported save formats (older formats have defaults/upgrades), and15C60 includes both in its archive path; owned saves must retain the intended mode across every reload.

The SE exposes4500 tribe-array records plus its documented sentinel-slot constant. Native119C00 searches free tribe IDs from1 while `<4500`, so ordinary creation can yield1..4499 and returns0 at exhaustion.11D370 increments native tribe WORD5C, sets the Unit-ID bit in the625-WORD membership set, fills the leader if absent and records the tribe/global binding on the unit;11E820 only finds the first occupied bitset word. Neither operation establishes a special32-member maximum.119F90 enumerates up to625x16 Unit-ID bits with the stored member count. A32-member fixture group is therefore a deliberately bounded, already game-tested workload choice, not a claimed native hard limit. Assignment must occur exactly once per freshly spawned unit; its low-level native body does not deduplicate count increments. Larger groups require their own formation/dispatch validation rather than assuming a limit from the bitset capacity.
## 26.11.2026 — Completed natural Spearman moat work, runtime controls [observed facts]

Under the existing CURRENT FBCB9319 native contract, controlled SP run131 (`6bd57095d9c645f2a4032bfaf23f1896`) completed real public Command6 and Command7 workflows. Dig bank and actual task-standing each returned0/0/0/1; the writer Post remainedAI124, the next tick showed the completed callerAI0/Command6, and the later ordinary handler producedAI1/Command3. In all four OFF/ON bank/task cases a subsequent genuine public move published one actual196280 Native1 and physically reached its new land goal with original identity/HP/tribe/occupancy and zero own rescue calls. Thus this observed capable Spearman can escape its newly completed moat after a new order; standing on that moat alone does not prove it needs recovery. No claim covers every digger type or a passing unrelated unit.

The opposing-team human bank Fill control acquired a real Task via publicCommand7, returned0/0/0/1 while progress4→0, retired the Taskgrid entry, then completed its callerAI0/Command7/A00=0 and physically executed a subsequent ordinary move. The retired task record was not read after the terminal writer. Both OFF/ON variants required no own recovery. These observations corroborate the audited writer/caller boundary; they do not establish the AI-owner122800 successful-retarget branch or rescue of passers. Evidence: `.work/a/rescue-game/131/6bd57095d9c6/LogOutput.log` and its restored transaction metadata; fixture assertions check actual native results and physical outcomes, not only API returns.

## 26.11.2026 — Orchard transit construction eligibility and external apple assets [Vanilla facts]

Canonical SHA256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Full6CB20,77CF0,5B020 and the relevant6FE90 moat branch were rechecked with the existing SE/Fixes producer contracts.77CF0 rejects IsFarm bit4 before the actual AI-wall write; changing a fixture from moat to AI-wall cannot make the observed00008004 orchard steps legal construction sources. Fixes' inspected77CF0 interior adjustment handles Stair6, not this farm gate.6CB20 has a one-tile geometry write, marks a selected vegetation record for removal when IsTree is present, removes a planned moat through5B020 and refreshes topology. A genuine Next-only source must therefore remain empty/non-tree and preserve all original tree/job accesses; earlier Fixes Pre sees empty Next and does not teleport that actor. Historical130 proves the recorded flags/fixture rejection, not that a wall was actually attempted there.

Complete108040 selects the nearest alive2/type15/growth3 tree with distance<30. It does not restrict selection to the worker's own farm, team or IsFarm tiles. Its four approach pairs are treeX/Y plus/minus2 on one axis. The public GameVegetationManagerAPI.CreateEx normal constructor accepts the AppleTree enum and initial growthStage3. It calls native107390, which allocates a positive native array index, publishes a new global identity, creates NeedsInit1/type15/stage3 and calls the type handler through31DC70[15]. That entry points to106A70; its entire machine body106A70..106D41 was read, including all growth/display branches: it writes vegetation state, animation and render data, not farm/terrain flags.108800 performs the genuine1-to-2 initialization on a subsequent native update. Native108320 excludes type15 from generic tree spreading. No artificial worker/tree-link/cargo field is needed for native108040 to select the asset.

Native108280 maps AppleTree stage>=2 to proximity level2.59520 resolves that level through2D1C38 (counts0,1,5,13) and uses6A620/E3360 to visit centre plus the four adjacent cardinal tiles. It only ORs TreeProximity0x2000 on admissible terrain; it does not create IsFarm4. The public creation wrapper sets the actual centre IsTree/tile vegetation link and performs its existing native path update. The native result is a zero-sentinel ARRAY INDEX; its real record must be independently validated using the established native-index-to-SDK-index+1 adapter, never mistaken for the SDK's one-based lookup domain.

The native orchard constructor72A50 was read in full machine form72A50..72E0F; its field call72AC8 invokes72490 with the original placement origin and size10. Complete72490/69850 establish the100-cell field enumeration and IsFarm writes. Existing own orchard trees are created through1071A0 at initial stage0. The AppleTree type handler uses the31E3C8 progression table (500,300,200,1000,40,40), so initial own trees are not immediately ripe; selection of an initially mature external asset must still be observed, not assumed from timing.

Fixture policy [test policy]: create a genuine external AppleTree through this public constructor before natural farmer acquisition, outside the checked original10x10 orchard and with four free real approaches. Keep every original farm tree and its normal work unchanged. Capture the new native index/global/tile, actual proximity and native NeedsInit-to-Alive transition; require the farmer's actual R2/R8 selected index/global to match. Acquire real ordinary non-farm intermediate steps and retain all existing original tree/job/cargo/goal, physical contact and production/delivery proofs. Public initial object maturity is a controlled world fixture, not a later yield or AI write. No performance run or this source preparation establishes Apple2/8 game acceptance.
## 26.11.2026 — Apple R2 can legitimately reject a no-longer-ripe selected tree [runtime corroboration]

The hash-bound135E30 R2 arrival branch reads the currently selected tree's growth stage after188340. OnlyStage3 enters pickingAI3; another stage entersAI5/resume2 with a20-tick timer and ordinary subsequent return/reselection. Runtime run134 (`9d1703c9b9e549d489f9ddc6013cfd0e`) corroborates this without any tested construction or own recovery: original selected AppleTree136/global1009 was Stage3/progress921 at sourceTick752, changed toStage4/progress0 at832, was physically approached at its actual261,391 goal withFlags0/SS8 at848, then producedAI2→5/resume2/timer20 at849. The first later physical pick was naturally on tree85/global1003. The corresponding changed-geometry case reached its original goal and took the same observed maturity-rejection branch after the owned original segment resumed.

This disproves an unconditional test expectation that an initially selected ripe tree must remain the first later harvested tree. Source identity/goal preservation through recovery and later native maturity-driven reselection are separate contracts. A positive unchanged-tree harvesting test must supply and verify enough natural remaining ripeness; no growth/job/target write is justified. Exact archived source, ticks, world/lifetime and physical-pick evidence: `.work/a/rescue-game/134/9d1703c9b9e5/LogOutput.log`; fixtures/control wrappers are test-only, and this record is not a general Apple2/8 rescue acceptance.
