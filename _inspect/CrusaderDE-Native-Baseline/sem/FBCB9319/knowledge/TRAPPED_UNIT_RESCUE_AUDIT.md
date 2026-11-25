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

These are static contracts, not newly successful rescue implementations. No
universal freedom to interrupt every AI state or to reconstruct an old reachable
bank from a later world is inferred. Earlier dated game observations retain
their original scope and version identities.
