# Worker production bonus and reduced output [Scope: Vanilla + mod integration]

Static audit observed 2026-11-06 against canonical native SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Generic Ghidra function labels remain candidate symbols; the machine/data-flow
observations below are confirmed-static. They do not assert live game acceptance.

## Calculation and complete caller set

Productivity producer `0xCD850` maps signed fear levels to 50..150 percent.
Yield helper `0x18D940..0x18D9E1` takes one-based unit ID, signed base amount,
and a nonzero fixed-bonus flag. It floors productivity at 100, adds 50 when
native mode `0x8574B90 != 0` and the flag is nonzero, then computes signed extra
output with a per-unit fractional carry. The complete conditional contribution
block `0x18D976..0x18D987` is 17 bytes (7,2,3,2,3), with both branches targeting
its continuation. There are no incoming edges into its interior in the audited
function/reference packets. The Extender owns the function entry and retains
Pre/native/Post around it; the block is separate from that entry.

There are 19 direct calls in 17 worker handlers. Fixed-bonus callers are wood
`12BC00`, fletcher `12D230`, hunter `12FC70`, stone driver `1319A0`, pitch
`1332B0`, hops `134F00`, apple `135E30` (two paths), cattle `136D40`, brewer
`139950`, poleturner `13AAD0`, blacksmith `13BDD0`, armourer `13CF30`, tanner
`13E0B0`, and iron miner `14DAC0`. Wheat `133DD0`, miller `1377C0`, and baker
`138850` (two paths) pass zero for the fixed bonus. The baker call whose
decompiler omits the third argument explicitly clears R8D in disassembly.

The signed remainder is manager/unit-relative `+0x95C` (typed GameUnit `+0x300`),
extra output `+0x95E` (typed `+0x302`), and carried output `+0x9E0` (typed `+0x384`).
The Extender views the first two as UInt16; native reads use MOVSX. Do not treat
raw 65526 as a positive quantity: its signed remainder is -10. Signed division
truncates toward zero and fractions accumulate across calls. Retaining a previous
positive credit may permit one residual output after switching to -100 percent.

## Completion boundaries and stone quantities

Wood, hunter, pitch, hops, apples, cattle and iron choose storage and build an
outgoing route before calculating yield. Their successful arrival calls are
`12C965`, `130A59`, `133840`, `135901`, `136854`, `137302`, `14DF7B`; each precedes
storage validity/capacity, re-search and deposit. Preserve that route and detect
zero after arrival. A blanket early return/state assignment would leave a route
or lose normal work accounting/rest/farm continuation.

Fletcher and blacksmith enter storage search after production; brewer, poleturner,
armourer and tanner first finish state109 animation. A zero quantity must bypass
the initial storage search, not merely deposit: Tanner state7 can otherwise wait
for an armoury while carrying zero. Existing native work-finished branches retain
`193A70` accounting, `191070` rest decisions, next ingredients, `19B930` workplace
coordinates and invalid-workplace fallbacks. `19B930` does not itself create a
path. No state change inside a yield Post event is sufficient: later caller
stores overwrite it, including Tanner's `AIState/SavedState=0x7006D`.

Stone driver `1319A0` calls yield with the Ox ID, not its own ID. The Ox is created
by tether update `AD180`, type9, stored at building raw `+0x2C8`. A completed input
stack of eight is consumed and the result assigned (not added) to Ox carry.
Ox update `132910` tests output >=8 at `132A09` and `132C17`, which would strand
reduced positive batches 1..7 and let subsequent assignments overwrite them.
Its unload state2 already rejects carry <1 before depositing and returns/waits
normally when empty. The driver's additional state6 reserve check at `1320B0`
rejects only <0 before decrementing and creating a Carry1 trip; a negative-output
policy must also reject zero. Its external first entry was not established as
a frequent ordinary path. Other eight-unit comparisons govern input/work counts.

## Save, transfer, checksum and display

Save writer `52B0`, called from `86F80` and synchronized-save handler `13030`,
uses table `336B20`. Entry54 at `337030` stores raw unit section3015, base
`67E8A58`, length `B23900 = 10000*490`, compressed. Loader `853F0 -> 87D0 -> CF20`
restores the same raw bytes. Unit post-load normalizers `186C50`/`19A500` touch
other fields (`68C`/`70A`), not carry. Slot release `1869C0 -> 7490` clears the
whole 490-byte wire record, including both signed production words.

Chore56 `15710` transfers that complete record; remainder/extra are wire offsets
304/306. The separately preserved word is wire34. Checksum entry `22020` includes
both words (unit1 offsets278/27A from hashed start67E8F74, length404), outside
the temporary eight-byte mask at offset390. No extra save/network format is needed.

Render/helper `18CE60`, called by `97DA0`, reads extra at `18D2D6` and `18D300`.
For productivity >=101 and a transport state accepted by `193F80`, any nonzero
extra enters positive-symbol selection; negative values fall into the same final
case as a large positive amount (code1A). Treat negative extra as no positive
yield symbol without modifying the field or return-value arithmetic.

## Shared implementation and evidence [mod integration]

APIShared owns the calculation/completion/transport/display hooks and process-held
owner registrations. ExtraFeatures owns the synchronized percent and mode policy.
No active override preserves ordinary Vanilla-reachable production. A saved
negative batch still requires completion/transport/display protection after the
consumer is disabled or its policy changes. Native `18D9C6` stores actual-minus-base
as signed extra. With productivity floored at100, fixed bonus >=0 and valid signed
carry -99..99, native truncating division cannot produce negative extra. Conversely,
zero output from positive base, or an Ox output1..7 from base8, requires negative
extra. This existing saved/transferred/checksummed word therefore supplies history
provenance; zero completion also checks Carry0. The driver path uses its RBX Ox,
and rendering uses the original RAX record. Unit-slot release eventually reaches
`1A8D20`, clearing that provenance with the rest of the record.

The shared implementation keeps coefficient50 and a separate process-only ready
DWORD0 throughout preparation/publication. Ready becomes1 only after validation
of all18 installed spans, so a partial install cannot enable historical guards.
After complete installation, guards admit current negative mode-eligible policy
or negative extra provenance. No new save/network field is introduced. These
follow-up generator changes were source-reviewed during a user-requested build
hold; earlier execution results do not validate the changed generators.
All native site bytes, continuations and footer register maps are maintained beside
`APIShared/src/Economy/WorkerProductionBonusNativeDefinition.cs` and the production
assembly generator. Layout remains hash-bound unless independently requalified.

The current ImprovedHunters Tanner hooks at `13EBF7/14`, `13ED8B/16`, `13EEFB/16`,
`13EF38/16`, `13F009/15` retain states7/8 for both armour and meat. The early
workshop zero boundary covers both without reading private run tables. Its yield
stub still calls the common helper with a configured base1..100. These sources
were active development state at observation time and must be rechecked.

Initial private-copy execution of the actual yield body passed8960 cases, changing
only its copied fixed-bonus immediate. Independent inspection compared11088
instruction packets across the eligible handlers/Ox/tether with the installed
DLL. Maintained execution tests live in APIShared's WorkerProductionBonus.NativeTests.
Neither these isolated tests nor raw archive roundtrips replace new/load/restart,
current-mod co-installation, editor rendering or real host/client game acceptance.

## Tweaker interaction observed 2026-11-06 [mod integration]

The official Crusader DE Tweaker main at `c5886d1a6de775b88ebbcde6bc3659072a631cf2`
identifies as2.11.2. Its production code equals the `v2.11.2` tag for this audit.
`Config/Core/GoodYield.cs` scales the base amount in Extender Pre, then clamps a
Quarry Ox's returned amount to at least8 in Post when the Quarry Grunt multiplier
is not1 (`YieldOwner` maps Ox to Grunt). It does not replace the fixed50 contribution.

Confirmed-static conflict: mode99, neutral productivity100, native carry0, Quarry
Grunt multiplier2 and shared fixed bonus-100 give base8 -> Pre16 -> native return0
(signed extra-16) -> Tweaker Post8. Extender `BulkUnitDetours.cs` returns that Post
value; native `131C9B` stores it on the Ox. The positive-load departure guards admit
the resulting8, so successful delivery produces8 stones instead of0. Mod load order
does not move a Post handler before the native calculation. With multiplier1,
Tweaker returns before its Pre scaling and Post clamp, so this conflict is absent.

There is no overlap with the18 worker-bonus spans: Tweaker's additional Miller
patches are `138162/9` and `138290/9`; the miller passes bonusflag0. The local fork
checkout `aef5663998d0dc460df2947939436bd6bf6a8301` (2.10.0) lacks the later Ox mapping,
minimum-eight Post rule and Miller patch, so that checked-out source does not
contain this specific conflict. This is not a runtime compatibility approval for
all multipliers, third-party base changes or existing Tweaker fractional state.

Primary source: [Tweaker yield implementation](https://gitlab.com/ensrick7/crusader-de-tweaker/-/blob/c5886d1a6de775b88ebbcde6bc3659072a631cf2/Config/Core/GoodYield.cs#L194).
Local audit metadata and limits: `.work/a/tweaker-bonus/001/review.json`. No Tweaker
checkout, production code, build, installation or live game was changed/run.

## Shared base-output follow-up observed 2026-11-07 [mod integration]

This section supersedes the earlier implementation-only claims that there are
18 total shared spans and that no extra state or wire data is ever needed. The
native facts about Vanilla's signed bonus words remain unchanged. The extended
service separately accumulates rational base-output credit and batch provenance;
it does not change Vanilla unit records, the two native production words or
their existing save/checksum meaning. Legacy-only worlds retain native Chore56.

New output boundaries are Wheat `1349B7/16`, Baker `1391F5/17` and `139337/18`,
Miller prepare `1380A1/17`, pickup `138162/17`, deposit `138290/17`, and finish
`1383AF/19`. Function hashes, original bytes, footer register contracts and
incoming-edge validation are in the shared output definition and executable
tests. Miller's actual output starts at one flour, not the wheat intake helper's
quantity; repeated native delivery at `B6100` deposits one flour each time. The
new output spans overlap the old Tweaker Miller patches. Their migration must
remove those patches, the Yield Pre/Post handlers and the minimum-eight Ox clamp.
No automatic compatibility with these direct foreign patches is claimed.

Native raw restore `87D0` reads files, with callers `853F0`, `94350`, `100530`.
The ordinary saved-game/editor-save route `853F0` first calls `87D10`, which sets
scene DWORD `3665ED8` to40. The save section table does not serialize that DWORD.
The first `71670 -> CDE60` fold cannot dispatch workers there: helper `7E50`
accepts scenes12/14/16. Other direct map callers do not provide the same general
scene40 guarantee. Initialization flag `366A0F8=-1` alone does not prevent the
fold. No memory-snapshot restore or editor-resize caller of `87D0` was established.

The disk writer `52B0` has46 native calls to CRT write `1DDB94` and a close at
`1DBB20`; Script Extender's ZIP append calls the same write entry. Write returns
the actual byte count or -1; close returns0 or -1. Both validated entry spans
start with two five-byte MOVs and have no incoming edges into the displaced ten
bytes. The shared critical-save cohort keeps sticky short-write/error/close
failure state, checks the actual saved sidecar and replaces the target only after
successful completion. A valid ZIP suffix does not establish native save success.

The Chore56 record begins at raw origin `+658`; its native Global-ID at `+6F0`
is record offset98 (body9C), and kind at `+6E6` is record8E (body92). The typed
GameUnit begins four bytes later. Extended worlds append one versioned32-byte
sidecar; individual checksum injection `22149/16` reaches both category and
single-unit checksum consumers. Empty normalized rows leave checksum unchanged.

Evidence: `.work/a/prod-state/002/result.json`, maintained shared native execution
suites and source-linked state/agreement tests. These are isolated execution and
managed-boundary checks, not game/editor or real host/client acceptance.

The temporary save pathname was separately traced through all three writer
callers. Within `52B0` it reaches only the no-op logger and physical file open;
`F4DF0` is the minimap builder and receives no pathname. None of the200 section
descriptors includes path object `30D0F30`, selector `30D1EE0` or slot `31C74B6`.
The restart blob at `8EC020/28` is copied from its existing source. Script
Extender captures its managed logical save path from the original outer argument,
before the shared writer swaps the native destination. There is no established
temporary-name leak into the native header, restart source or Extender context.
Evidence: `.work/a/prod-state/002/temp-routing-review.json`.
