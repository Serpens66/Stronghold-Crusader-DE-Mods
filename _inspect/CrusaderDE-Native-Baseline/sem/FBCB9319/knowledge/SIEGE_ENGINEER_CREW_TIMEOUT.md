# Siege engineer crew timeout [Scope: Vanilla]

Native image SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. RVAs below were verified against the executable sections of that image.

The record anchor is the unit manager plus the one-based unit ID multiplied by the native stride 0x490. For an engineer, the signed dword at anchor +0x974 (GameUnit +0x318) counts time spent bound without a crew refresh. A siege engine uses the same storage in its own record for the first crew global ID. The work/animation counter at anchor +0x908 is separate.

- The unit update at 0x182B00 increments the timeout at 0x1832C7 for engineer type 0x1E in main state 5. The siege-unit pass at 0x1839C0 clears it when the engine participates in the pass, the crew slot's global ID matches and the engineer is in state 5.
- The engineer handler at 0x14AED0 compares the timeout to 120 at 0x14C3E0. The greater-than path clears work, sets packed state 0x0000006D and visual word 0xFE20, then makes the engineer visible again. The transition runs to main state 0. The expired timeout survives this release.
- Full-crew completion in the building handlers for Arab ballista (0xA65A0), catapult (0xA8E40) and trebuchet (0xA93C0) writes main state 5 to each engineer at 0xA68EF, 0xA916A and 0xA97F6 respectively. These loops clear the low visual byte but omit timeout initialization. The completed engine takes the same one-based crew IDs and global IDs from the tent. Failed spawn and incomplete crew take separate paths.
- A stale timeout above 120 can therefore release the engineer again before a matching crew refresh, leaving the engine's crew entry intact. Explicit dismount in action 0x11 of 0x11E960 clears the device's crew slots and uses the referenced identities, which explains the visible unit count after dismount.

Observed on 2026-09-29 in BepInEx/logs/Log_185.log. Device 254/3075 left siege scope at tick 2687, and engineers 1/2927, 170/2945 and 173/2948 went from bound with counter 120 at tick 2807 to release with counter 121 at tick 2808. Their free records still held 121. Device 305/4575 received all three matching crew pairs at tick 3390 and all three engineers were in 0x0000006D while the device retained them at tick 3391. The test observer's detailed engineer transition budget had ended before that second completion, so its logs do not contain a fresh counter sample from tick 3391. See the semantic decompiler export for the timeout increment, reset, threshold and full-crew writes.


## Fatal fire damage [Vanilla facts]

A fire tile calls `0x9E030` for the units linked to that tile. Its caller passes the fire owner and reduced-damage flag to the per-unit routine at `0x192400`. Nonlethal damage returns before conversion. For the eight siege unit types the fatal branch at `0x1925DB` sets pending state 0 and life state 4. The switch at `0x192612` chooses pending type 2 for each. The unit pass at `0x182B00` consumes life state 4 via `0x195D10` and then dispatches to the burning-man handler at `0x12B980`. It no longer executes the old siege handler or refreshes the original crew. The captured release at ticks 2808 and 2841 is the existing engineers' timeout and exit transition.

Ordinary siege death uses the unit handlers listed below. Each checks the crew global IDs against the recorded unit slots and marks the matching engineers for deletion in its normal death branch. The dismissal path sets the marker at record anchor `+0xA48` via `0x186D10` and has separate crew handling. The unit pass consumes `MarkedForDeletion` via `0x1869C0`, which clears the unit and associated path state. Manual dismount uses action `0x11` in `0x11E960` and clears the device's crew entries.

| Device | Type | Handler RVA | Crew capacity |
| --- | --- | --- | --- |
| Catapult | 0x27 | 0x1520D0 | 2 |
| Trebuchet | 0x28 | 0x1535F0 | 3 |
| Mangonel | 0x29 | 0x1547F0 | 2 |
| Siege tower | 0x3A | 0x15ECE0 | 4 |
| Battering ram | 0x3B | 0x160420 | 4 |
| Portable shield | 0x3C | 0x161220 | 1 |
| Ballista | 0x3D | 0x161CB0 | 2 |
| Arab ballista | 0x4D | 0x171C50 | 2 |

The tower's native deployment has an alternate branch that releases its engineers and clears the crew count. The deployed building proxy type 0x45 is rejected by the Vanilla building ignition check at `0xC1F60` called from `0xC4D30`. The unit and building are distinct records.

## Harassment reserves and pending recruitment [Vanilla facts]

Audited on2026-11-13 against the same native hash. Recruitment at0x1819D0 reserves
an existing live peasant and immediately returns its identity. It writes requested
type+0x922, pending state+0x924, packed state0x0000006D at+0x918, recruitment word1
at+0x9CA and visual0x0100 at+0x986. The AI attaches that ID to its harassment reserve
before conversion/fade completes. The reserve entry is therefore not proof that
an Engineer is ready to receive an order.

Deployment0x2ECD0 requires reserve count>=2, active harassment devices below the
AIC maximum, and role22 engineer count below20. Both construction and recruitment
are skipped at20. Order15 in0x11E960 skips peasants but accepts Engineers without
checking+0x9CA; accepted members are removed from the group, so repeated ordinal
lookup while removing members is intentional, not duplicate iteration.

An Engineer's tent arrival in0x14AED0 writes packed state0x0007006D and appends the
same ID/global to the tent. If recruitment+0x9CA is still set, the transition at
0x14D252 schedules life4 (conversion) after33 updates. Converter0x195D10 then copies
pending main state0 over the bound/build state, keeping global identity. This path
is independent of the bound-crew timeout+0x974. Only idle Engineer processing after
initial fade clears the recruitment marker. Native role22 accounting still counts
stranded live engineers, explaining the quota stall without creating new identities.

Isolated execution of original order bytes with native membership operations gave
states8/8 with two ready Engineers and an empty reserve, states8/0 with one Engineer
and one pending peasant and one remaining reserve member, and states8/8 with a still
recruiting Engineer. After33 original Engineer transition updates, recruitment0
retained life2 whereas recruitment1 scheduled life4. Executing original converter
bytes from a bound Engineer with timeout0 produced state0/type30/life2 with the same
global ID and recruitment1. These probes substitute path/visual boundaries and do
not identify which trigger occurred in the reporter's match. A screenshot alone
cannot establish identity duplication or the original recruitment timing.

## Serialized user-save evidence (2026-11-13)

The load descriptor table at RVA `0x336B20` is passed by `DLL_LoadSaveGame`
(`0x853F0`) to loader `0x87D0`. Each descriptor is24 bytes: target pointer+0,
load size+12, UInt16 section ID+18. The following entries were checked in the
hash-bound installed PE, independently of inferred record contents:

| Section | Descriptor RVA | Target VA | Decoded bytes |
| --- | --- | --- | --- |
|3015|`0x337030`|`0x1867E8A58`|11680000|
|1016|`0x337090`|`0x187CC6748`|2508000|
|2016|`0x3370A8`|`0x187CC6748`|7524000|
|2022|`0x337108`|`0x18379ADD0`|203292|
|1046|`0x337210`|`0x183665F58`|4|

These serialized origins differ from the Extender structure origins. A one-based
unit ID has section3015 field offset `id*0x490 + nativeField - 0x658`.
A tribe has section2016 offset `id*0x688 + nativeField - 0x28`. Do not substitute
Extender unit+0x65C or tribe+0x2A. Existing MapParser.Core `Parse(byte[])` and
`Sections.Single(SectionId).ReadContent()` validated decoded length and DCL CRC.

User files `test AI engineer dupe before.sav` and `test AI engineer dupe happened.sav`
have hashes respectively `E79BD3B8914A64E297EC43765BE80C39EC1347457DE71764EF2AA3E14B747B51`
and `F9ABB6F6FA332684C3D1AD393D4DFEAA2F62DB924A2A6CE17B8274F511FA3FB2`.
Map ticks are26593 and32232 (delta5639). These are archived states, not proof of
which mod versions or transition path produced them.

In the later save, live owner4 catapults226/24870 and947/17499 both contain the
exact crew pairs771/16395 and772/16663. Both devices have life2, health10000,
death marker0 and dismissal0. Their main states are0 and9. Both engineers have
life2, health5000, main5, recruitment0, timeout0 and mounting word+0x9B0=1.
Their position is346/503. This is actual multiple reference to identical unit
and global IDs; counting sprites alone is not the evidence.

Engineers876/17404 and879/17488 are live owner4 role22 units in main1,
recruitment0, mounting0, tribeID0 and historical tribeGlobal17891. No live
crew-bearing device references either identity. They stand at346/508 and347/508.
The saved owner4 role22 counter rises2 to10; live Engineers overall19 to34.
Slot reuse in the other save is real: one-based slot772 still has global13904,
876 has15865 and879 has15982. Never identify a unit across saves by slot alone.
The creation ticks in unit+0x6F4 are original unit/peasant creation, not the
recruitment time: converter0x195D10 preserves identity and creation.

## Ready engineers can remain in states101 and105 [Vanilla facts]

Engineer handler0x14AED0 uses main101 (`0x65`) for ordinary movement and
main105 (`0x69`) for movement/waiting at a guild parking position. Idle handling
can set105 at0x14B0C7,0x14BFC7 and0x14C391. Case105 calls read-only arrival check
0x188340 and parking lookup0xBC6A0. At an already reached waiting position, the
lookup returns0; the path at0x14B4E4 writes only direction+0x90C=4 at0x14B4FE,
then returns. It does not restore main0..2. Dispatcher0x182B00 has no subsequent
105-to-idle normalization. Thus105 is not evidence of unfinished recruitment,
and waiting for main0..2 can block an otherwise ready reserve indefinitely.
Case101 can reset main to0 through a DWORD write at+0x916 on movement completion.
Original order15 accepts101 and105; its state-range guard excludes9..15.
The separate recruitment flag+0x9CA must still be checked.

The before-save independently contains Engineer815/14884, recruitment0,
role22, state105 and reciprocal reserve group4247/14999. This is a concrete
archived example of a completed recruit in the normal parking mode.

## Automatic device crew selection [Vanilla facts]

Each of the eight siege handlers has a state6 search for nearby Engineers.
Katapult0x1520D0 and Arab/fire ballista0x171C50 have an AI branch accepting a
candidate by native role22; this branch bypasses command16 and the target ID.
The other six searches require command16 and their own target ID. Successful
binding sets command3, so this normally excludes already used candidates there.
The searches retain their existing life, death, type, distance and owner rules.
Some intentionally group candidates by owner and can transfer an empty device;
adding a universal same-owner condition would change that existing behavior.

The KI role22 branch does not reject main5 (already operating another device)
or packed0x0005006D (already selected, transitioning to5). A later empty device
can therefore copy the same ID/global pairs while the earlier device retains
its crew list. This is independent of recruitment flag9CA and timeout974.
Main7 and packed0x0007006D are already assigned to a tent: Engineer case8 appends
ID/global to the tent before starting that transition. They are likewise not
free candidates. Main8 alone is only an approaching builder and cannot be
excluded indiscriminately: the original tent may have completed before arrival.

Native searches do not share one helper. Complete CMP/JNE bounds, next-candidate
branches and candidate anchors are:

| Type | Handler | CMP/JNE span | Next candidate | Candidate anchor |
| --- | --- | --- | --- | --- |
|Catapult|`0x1520D0`|`0x15258A..0x152599`|`0x152681`|R11-0x8F8|
|Trebuchet|`0x1535F0`|`0x153AFA..0x153B09`|`0x153BD6`|R11-0x8F8|
|Mangonel|`0x1547F0`|`0x154DBC..0x154DCC`|`0x154E94`|R8+R9|
|Tower|`0x15ECE0`|`0x15F278..0x15F287`|`0x15F34A`|R11-0x6E6|
|Ram|`0x160420`|`0x16082A..0x160839`|`0x160907`|R11-0x8F8|
|Shield|`0x161220`|`0x161549..0x161558`|`0x16161B`|R11-0x8F8|
|Ballista|`0x161CB0`|`0x16221C..0x16222C`|`0x1622F5`|R8+R9|
|Arab ballista|`0x171C50`|`0x17224A..0x172259`|`0x172345`|R11-0x8F8|

R8+R9 means manager plus candidate ID times0x490. The comparisons test type30,
except the tower span which tests command16 after its type check. Full direct
branch/call scans of all4478 functions found no interior entry to these spans.

Tent construction with only part of the crew arrived is a deliberate native
fallback. Catapult builder0xA8E40 can create a device with an empty crew, then
release the arrived workers with a pending command16 to man the new device.
Stopping all partial-crew completion would be an additional gameplay change,
and would not itself correct the permissive state6 candidate predicate.
The fallback writes the new device ID at Engineer+0x9F6 but not its global at
+0x9F8; command16 validation must not be inferred merely from that queued ID.

## Observed automatic reassignment, 2026-11-13 [Vanilla path in live control]

The uncorrected automatic-search path was reproduced from the supplied before
save with Fixes1.29.1's harassment delay enabled. Engineer771/16395 and245/6287
had recruitment0 before order15 at tick27886 (completed by27012 and27462).
Tent385/17005 completed with one arrived builder and created empty catapult
823/17303 at28596. At29769 this device bound both engineers. They were still
main5, mounted, at31189. Empty catapult847/21032 appeared at31297 and copied
both pairs at31305 while823 retained them. Thus unfinished recruitment is not
a necessary precondition for this second bug; neither a recruitment delay nor
resetting timeout974 can make its candidate predicate safe.

Evidence: workspace `.work/a/ai-save/001/before-off-long/0fdab722af38/LogOutput.log`.
This was an instrumented game with the duplicate-prevention option disabled,
not a claim that the whole process contained no mods. Readback used native
ID/global pairs after engine runs; request events alone were not treated as
executed orders. The original before/happened snapshots and all original
configuration bytes were preserved by the test runner. Fix acceptance evidence
belongs to BugfixesAndQoL/UpdateToNewDLL.md and the dated analysis result.
### Existing idle workers can replace a later arriving builder [live observation]

The happened-save continuation also distinguishes remaining idle engineers from
new duplicate references. Group4288/36070 contained new recruits780/35848 and
809/36303. Both had recruitment0 when order15 ran at39525. Tent413/36917 had
only780 when it completed at40298. Empty catapult802/37457 selected780 plus
876/17404 at40300;876 was already an unclaimed idle worker in the saved snapshot.
809 arrived later, changed from main8 to0 at40514, then main1 at40515, and stayed
unclaimed. No device pair was duplicated in this sequence. This is the original
partial-completion and free-role pickup behavior, not a second assignment of
an already bound engineer. Consequently, zero new alias pairs does not prove
that a previously damaged match contains no idle workers, or that every such
worker has the same cause. Evidence: workspace
`.work/a/ai-save/001/happened-on/edf82879dae6/LogOutput.log`, ticks39036..40515.