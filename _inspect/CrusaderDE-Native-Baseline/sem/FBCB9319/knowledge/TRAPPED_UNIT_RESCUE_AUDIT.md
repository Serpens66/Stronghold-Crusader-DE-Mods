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
