# Recruit Transformation and Rally Tracking [Scope: Vanilla + mod integration]

## Provenance

- Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- The generic function names remain `candidate`; the field and control-flow claims below are confirmed for this binary by static data-flow analysis and the BugfixesAndQoL runtime trace from 2026-09-17.

## Recruitment entry points

- Mercenary recruitment at RVA `0x1819D0` and European recruitment at RVA `0x190CA0` select an existing type-1 unit slot. They store the requested type at manager-relative unit offset `+0x922`, set AI state `0x6D` at `+0x918`, and return the unchanged one-based unit ID.
- The slot's owner and GlobalId remain its identity throughout the recruitment transformation. `0x117000` performs the associated tribe/notification work; it does not allocate or return a successor slot.

## Transformation completion

- Type-1 dispatch handler RVA `0x12A8F0` advances the `0x6D` transition for 33 updates. On completion it sets the same slot's AliveState at `+0x6E4` from `2` to the transient value `4`, retains the requested type at `+0x922`, and leaves the slot type at `1` for the remainder of that update pass.
- Unit update orchestrator RVA `0x182B00` processes AliveState `4` through RVA `0x195D10`. That function restores AliveState `2`, restores the queued AI state, and calls RVA `0x19A240` with the value from `+0x922`.
- RVA `0x19A240` initializes the requested target type in the same unit slot by writing `+0x6E6` and the target type's dependent fields. There is no successor-ID handoff.

## Consumer contract [mod integration]

- Per-unit consumers that formerly had an outer `AliveState == 2` gate must skip all feature logic while the slot is in State `4` without invalidating identity or feature-owned tracking. Unit deletion remains a separate event/state transition.
- BugfixesAndQoL rally tracking is therefore keyed by the unchanged one-based unit ID plus GlobalId and owner. Its native cadence fastpath must replay Vanilla for every non-alive state and resume validation when the same slot returns to AliveState `2`.
- Observed trace: all affected recruits reached the cadence hook 33 times as type `1`, AI state `109 -> 0`, TransformType unchanged, and AliveState `2 -> 4`. Clearing tracking at that point prevented the final target type from receiving its rally animation and speed bonus.

## Natural employment before completed transformation, 27 November 2026 [Vanilla facts]

The canonical native hash above was freshly rechecked against CURRENT.json.
Complete190B30/B62D0/B6340/C9550/C98A0 and their CEF00/B6890/
C5990 hiring chain were read on that image.190B30 selects an existing
eligible living Peasant, records employer990/9C0 and Home992/994,
queues the desired type922 and publishes AI109/fade986=0100/work9CA=1.
B62D0 immediately records employee ID/global in building1FA+slot*2 /
204+slot*4 and unit employee-slot9EE/employer9F0. Genuine employment
can therefore already exist while the unit is still type1/Peasant.
The33-update109→Life4→195D10/19A240 chain above then completes the
type change in that same identity. Mine-specific B6340 requests type31
Miner1 in slot0 and type32 Miner2 in slot1. Employment alone does not
prove that either transformation has already finished.

C9550 validates positive employee ID, life other than0/3, matching
employer9F0 and recorded employee global. It does not require the final
worker type and does not reject transient Life4. C98A0 clears invalid
employee ID/global pairs, recounts employees and updates vacancies under
the building's required-worker/pause/type gates. Thus deleting an original
employee can expose a vacancy, but does not create its replacement.

CEF00's ordinary hiring branch is gated by native player/state/mode;
mode6 is excluded and mode1 does not enter that branch. B6890 collects
live owned positive-vacancy buildings, excludes types8/9, refreshes their
access and requires a nonzero player-specific PCL route toward the
player's reference component. C5990 orders candidates by building332
and BFB50/B8DA0 priority. B6340 subsequently assigns available Peasants.
Eligibility, reachability, a Peasant and a later hiring opportunity remain
necessary: no immediate or unconditional reemployment is implied.

Audit provenance: current-hash function records and
.work/a/rescue-farmers/001/REVIEW.md; pending employee transformation
was independently checked in .work/a/rescue-mine2/001/REVIEW.md.
No generic removal API, completed replacement gameplay test or job/cargo
transfer to the new employee is established by this employment contract.

## Removal, population recount and conditional migration, 27 November 2026 [Vanilla facts]

On the same freshly verified canonical image, complete1869C0 and its
186C20/19A5D0/186AD0→186AF0 chain remove hover counting, group/
selection membership and tile occupancy, clear the0x490-byte unit record
and1000-byte path, and release its auxiliary bookkeeping.186C20 is a
hover-counter operation, not population cleanup.182B00 processes Life3
through1869C0 before invoking the normal type handler. No goods/cargo
refund, worker respawn or immediate hiring occurs in this removal chain.

Surviving units contribute to per-owner counters18379AE54 and, through
type table3223D0,18379AE48. CDB20 derives player population+2180
from the normal recount (+84−217C−2174−53C and validated NPC
adjustments). Clearing a unit is not a synchronous population-decrement
or a direct instruction to spawn a Peasant.

Complete D2C40 advances normal migration from popularity+60 through
increment+70 and progress+6C, requiring progress>=4000 for arrival.
The positive branch requires KeepID+94, DoorID+1D4, population+2180
below housing+74, player/state/mode eligibility including+2210/+2244,
and idle Peasants+8C<24 or the explicit native option exception.
It calls17FEF0(type1) at the actual KeepDoor coordinates and resets
progress to2000; allocation may still fail. Low/negative progress has
its separate emigration branch. D1AD0 schedules ordinary migration only
outside modes1/6 and CEF00 hiring at periodic slot95/200.

Existing free Peasants may be assigned before a new migrant is needed.
Neither deletion nor a vacancy guarantees immediate replacement. The
exact subsequent physical KeepDoor-to-campfire movement and alternative
disband/unassignment chains were not closed by this audit; they must
not be inferred as an interchangeable public deletion/recycling API.
These are static contracts, not an executed general removal test.

## Additional civilian employment and completed target fields, 27 November 2026 [Vanilla facts]

Canonical DLL SHA-256 was rechecked as
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Complete B6340/190B30 and the actual Int32 employment table at RVA
0x2E4E20 confirm four ordinary Peasant-employment types beyond the
nineteen human production/travel types listed in the earlier worker audit:

- Building type22 (Inn) requests unit36 (Innkeeper); building23 requests
  unit34 (Healer, the Apothecary employee).
- Building types36/37/38 request unit33 (Priest). B6340 handles the
  multiple employee slots for types37/38 explicitly; 0x2E4EB4 and
  0x2E4EB8 both contain33.
- Building70 (Waterpot) requests unit53 (Fireman); its explicit three-slot
  branch loads53 from0x2E4F38. This is not a claim that every Well has
  the same employment contract.

The actual installed SE enum values match: PRIEST33, HEALER34,
INNKEEPER36, FIREMAN53. MONK37 and BEDOUIN_HEALER79 are different
unit types, not these employment roles. Ox9 is also different: AD180
allocates it through17FEF0 and records tether2C8/2C4 ID/global. It is
not produced by assigning a free Peasant through190B30.

Complete1819D0 and190CA0 publish the paid recruit's target922 and
AI109 before returning to their caller.1819D0 has an optional19BA40
relocation after922 but before109;190CA0 publishes109 before its
optional relocation. An arbitrary nested callback is therefore not the
same admission boundary as the next normal simulation tick.195D10
reads922 and calls19A240; neither complete function clears922.
A completed worker can retain its old target type. Requiring922==0
for completed workers is not justified by this transformation contract.

C60F0 invokes C98A0 before the current building's type handler.
C9550 already rejects a Life3 employee before1869C0 has cleared its
record. This handles the ordinary employee ID/global slots; it does not
prove immediate cleanup of every auxiliary production registration or
animal reservation. In particular158780 Cow4 validates Tanner global
and8F8, not Tanner Life3. Cow5 has its separate1001-update timeout
into6. No universal immediate cow-release operation is implied.

## Script Extender deletion and simulation boundary, 27 November 2026 [mod integration]

Dated implementation evidence, not a permanent SE-version requirement:
installed SHCDESE2.14.6, MVIDcb76e769-44b6-4d35-9cbd-311dcdcb6c6c,
SHA-256904F735ABAE1843D2FAA2C0229F060C129F0C4C455DB0EB5E9503019DAA3F61D.
The actual assembly IL for GameUnitManagerAPI.DeleteUnit/DeleteUnitSafe
and the corresponding local source were checked. Native behavior above
remains bound to the canonical native hash independently of this snapshot.

DeleteUnitSafe uses TryGetUnitById and writes only
AliveState.MarkedForDeletion (Life3), then returns true. The lookup proves
array bounds/pointer availability, not a living unit or unchanged global.
The caller must establish the actual life, owner, type and global itself.
True means marked, not removed, refunded or replaced. No deletion event
is raised synchronously by this marker method.

DeleteUnit instead calls BulkUnitDetours.c_game_unit_delete_hook_impl
synchronously. That wrapper raises OnUnitDelete Pre, invokes the native
Original only when SkipOriginalFunction is false, then raises Post.
UnitDeleteEventArgs.UnitId is get-only; its public API does not permit
regular ID redirection. A Pre veto prevents both Original and Post.
The wrapper has no own transactional exception/finally guarantee.
The normal182B00 Life3 call also reaches this installed SE detour;
marking does not bypass later observers or their veto. Never infer
completed removal from DeleteUnit's ignored return or a Pre event.
The SE tribe-remove detour used by19A5D0/123EA0 forwards to its
Original without raising a separate public removal event in this snapshot.

BulkTimeDetours' existing synchronous context callback invokes
StrongholdFrameProvider.Tick in the CDE60 time section, before the
subsequent D1AD0 player, C60F0 building and182B00 unit passes;
CDB20 recount follows the unit pass. A marker set at that normal
pre-pass boundary is consequently consumed before the target's normal
movement/type handler. This is not permission to remove synchronously
inside a construction, recruitment, move or delete callback. APIShared's
SimulationTickEvents adapts this source with mission/clock checks and
per-registration exception isolation; that adapter is external to Vanilla.

Source anchors: shcde-script-extender/src/SHCDESE.BepInEx/API/
GameUnitManagerAPI.cs (DeleteUnit/DeleteUnitSafe/TryGetUnitById),
Detours/BulkUnitDetours.cs (c_game_unit_delete_hook_impl),
EventAPI/Units/UnitDeleteEventArgs.cs, Detours/BulkTribeDetours.cs,
Detours/BulkTimeDetours.cs and API/Components/Timer/
StrongholdFrameProvider.cs. No runtime deletion/replacement test is
claimed by these static contracts.

## Migration table and public resource aliases, 27 November 2026 [Vanilla facts]

The canonical DLL was freshly rehashed against CURRENT.json as
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Complete D2C40 and its D1AD0 sender were rechecked. D2C40 selects the
Skirmish/nonzero-mode growth table at RVA0x311E50 using integer
popularity/500. At popularity10000 this is index20: RVA0x311EA0 contains
little-endian bytes28 00 00 00, the signed Int32 base increment40.
Its existing positive-growth option gates (including0x87EE314 and, in
mode99,0x87EE2F4) may double that value before storing the effective
increment. The table value alone is therefore not a live increment proof.
The cumulative progress at player+6C and the effective signed increment
at+70 are separate fields; arrival still needs the previously documented
4000 threshold and all ordinary eligibility/allocation checks.

### Public resource reader mapping [mod integration]

The actual public Script Extender GamePlayerResources names are historical
aliases: UInt32 r_PeasantSpawnProgressRequired is cumulative native+6C;
Int32 r_PeasantSpawnProgress is effective increment+70. The structure has
stride0x583C and no building/unit header subtraction. The public
GamePlayerManagerAPI.TryGetPlayerResourcesById validates the player1..8
and nonnull array, then returns array[playerId-1]; initialization places
that array at playerManager+0x1343FC. D2C40's initial pointer
playerManager+0x13446C equals this first resource record+70; its[-1]
and[-4] accesses are+6C and+60 respectively. Current installed SE metadata
confirms both public field widths. No raw player pointer or counter write
is required to read the ordinary migration progress through that API.

Reproduction: CURRENT-filtered full function queries D2C40/D1AD0;
canonical PE.get_data(0x311E50,84) decoded as21 little-endian signed
Int32 values, element20=40. Managed evidence is GamePlayerManagerAPI.cs
PLAYER_RESOURCES_OFFSET/constructor/TryGetPlayerResourcesById and public
GamePlayerResources.cs fields60/6C/70. These static facts do not establish
a particular earlier run's unlogged initial progress, effective option,
first arrival time or completed removal/replacement outcome.


## Auxiliary worker bindings and ordinary AI hiring, 27 November 2026 [Vanilla facts]

The installed canonical DLL was freshly hashed against CURRENT.json:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The statements below are confirmed static contracts, not completed removal
or replacement game tests. Offsets are native manager-relative: building
base plus ID*0x32C (typed view begins at +0x5C); unit base plus ID*0x490
(typed view begins at +0x65C). Building and unit IDs are one-based.

### Persistent employment versus per-pass heartbeat IDs

CDE60 runs C60F0 building maintenance before 182B00's unit pass. C60F0
calls C98A0 before the live building type handler. For eligible, unpaused
employment buildings C98A0 validates persistent employee WORD IDs at
1FA+2*slot and DWORD globals at 204+4*slot using C9550. Life0/3,
wrong employer9F0 or a changed global invalidates that pair; transient
Life4 is accepted. The removed pair is cleared and vacancies recounted.
Paused/nonemployment/deleted buildings do not promise this same cleanup
cadence, and neither validation nor vacancy creation guarantees hiring.

A different four-WORD array, native214/216/218/21A (typed1B8..1BE),
is cleared by C60F0 AFTER its live building handler. Subsequent unit
handlers republish IDs into it; there are no paired globals in this
heartbeat array. Miller1377C0 writes214+2*unit9EE on every handler
entry. Miner1 14D6F0 and Miner2 14DAC0 write it only from AI1.
MineAD540 reads214; Miller buildingA43C0 counts214/216/218.
Thus an old heartbeat can legitimately remain visible before its next
building pass, even after the employee was marked Life3. That pass can
read the old heartbeat once before C60F0 clears it; Life3 is consumed
by182B00/1869C0 before the worker can publish another heartbeat.
AD540 may even finish its existing animation and credit one ordinary
local iron in that first pass. This is native building continuation,
not a replay or goods refund by the removed worker. Assertions about
retirement must observe actual complete passes and original lifetime;
synchronous zero immediately after a marker is not a native invariant.

### Mine producer and carrier visibility

B6340 hires Miner1(type31) in employee slot0 and Miner2(type32) in
slot1. Miner1's14D6F0 AI2 validates188340 arrival and C9080 workplace
contact, then196F80 hides/unlinks it before AI1. Its AI1 branch only
resets animation and publishes214. It has no ordinary AI1-to-visible
return branch. The main182B00 employer-invalid/paused path instead
stops and eventually publishes transient Life4, target922=Peasant,
clears Home990 and unhides via187200; that is a transformation, not
an ordinary visible active producer. AI110/death paths are separate.
Therefore a visible original Miner1 AI2 can prove persistent employee
retirement, but is not proof of active214 cleanup. An active hidden
Miner1 and a visible ordinary Miner1 are distinct cases.

Miner2's14DAC0 AI2 also checks188340/C9080 but enters AI1 without
196F80. Its visible AI1 increments908; after20 updates193F10 checks
physical workplace contact. Failure refreshes Home access through
19B930 and schedules AI2, or marks Life3 when refresh fails. At contact
it selects AI5 when local iron is empty or AI3 otherwise. Regardless
of that transition, the same AI1 handler tail publishes214+2*9EE
(slot1 normally216). This allows a genuinely visible active heartbeat
without any fabricated hidden-state change. AI3 commits a store route
before taking native mine inventory and yield; AI4 uses physical
C9080 contact before storage credit. 193A70 updates per-unit work
statistics, not an external shadow actor. No independent Mine2 shadow
object follows from these paths.

### Miller's separate active production client

Miller1377C0 additionally acquires buildingWORD21C (typed1C0), a
single active production-client ID without a paired global. AI4/6
may acquire it after arrival when21C and production phase178 are
free, then enter AI5 and hide through196F80. AI5 republishes21C;
when phase178 reaches3 it clears21C BEFORE entering AI7 and
196FD0 unhide. BuildingA43C0 independently clears21C when the
referenced actor is not Life2/type15/AI5, resetting phase178 on the
invalid-client branch. Consequently a visible Miller can prove its
persistent employee and214/216/218 heartbeat cleanup, but does not
by itself prove retirement of an active hidden21C client. Another
real Miller may naturally occupy a client or heartbeat slot later.

### Reserved cow after Tanner removal

The complete Tanner13E0B0 AI2 contact branch establishes reciprocal
9F6 ID/9F8 global pairs, routes Tanner and cow to the Tanner home,
sets TannerAI3 and CowAI4, and trims the cow route via198620(2).
TannerAI3 uses188340 before switching to AI9; there is NO extra
Home-distance check in that transition. Normalizing movement at a
remote location is therefore not semantically neutral preparation:
Flags0 can satisfy arrival and advance the native Tanner job there.
TannerAI9 frame8 sets the selected CowAI6 (no HP/death-marker hit).

Cow158780 still checks its original farm lifetime, pause state and
nonzero farmer slot. In cow states4/5/6/110 it clears its original
farm animal slot when that slot still contains its own ID. This can
happen before Tanner removal; continuous animal-slot membership is
not a valid reservation requirement. A3A70 creates those cows in
slots1..3, records farm990/global9C0/slot9EE/employer9F0; slot0 is
the farmer. The animal birth relationship is distinct from the
Tanner reservation.

CowAI4 checks the Tanner global and Tanner8F8==0, NOT TannerLife2.
A marked Life3 Tanner can therefore still satisfy this check until
1869C0 clears its record. Cow4 may reach AI5 earlier in that same
unit pass, depending on ID order. If the global/marker check fails,
it stops, decrements the cow count and enters AI110. Cow5 does not
revalidate the Tanner: it increments920 and at1001 entersAI6, whose
animation then leads to110 and the ordinary33-update fade to Life3.
A cow already in5 is therefore allowed to use that native timeout
after the Tanner disappears. Cow9F6/9F8 need not become zero before
record deletion. Farm invalidation or other native retirement may
also mark it Life3. No universal immediate reservation-clear or
synchronous animal-slot release follows from deleting the Tanner.

### Ordinary hiring is player-ID based, including AI owners

Full CEF00 loops playerIDs1..8 without a human/AI ownership branch.
It excludes mode6; ordinary hiring is skipped in mode1. Each player's
Keep94 and KeepDoor1D4 must be positive, then vacancies68 and ready
peasants88 must be positive before B6890/C5990/B6340. B6890 accepts
live owner-matching buildings with vacancies, excludes building8/9,
refreshes access via C90E0 and requires the player's reachable PCL.
C5990 orders the candidates by332 and BFB50/B8DA0 priority. B6340
passes the same playerID to190B30. That allocator selects an existing
Life2/type1 Peasant with owner6EE==playerID and employer9F0==0;
it excludes AI4/5/109/106, sets target922/Home990/global9C0/access,
and starts AI109. B62D0 publishes employee ID/global/slot/employer.
Owner2 therefore uses the same ordinary hiring mechanics, provided
its actual Keep, door, peasant and reachable-workplace gates hold.
A prefab owner field alone does not establish those prerequisites.
This does not prove a particular AI strategy will create that world.

Reproduction: CURRENT-filtered complete function records for CDE60,
C60F0 (in particular its final four heartbeat clears), C9550/C98A0,
B6340/B62D0,14D6F0/14DAC0/193F10/19B930,196F80/196FD0/187200,
182B00,1377C0/A43C0/AD540,13E0B0/158780/A3A70 and
CEF00/B6890/C5990/190B30. Use tools/semantic/query.ps1 function RVA;
the database manifest and binary_hash must match CURRENT.json.
These contracts neither free all job-specific resources synchronously
nor authorize manually replaying work, modifying cargo or resuming
an old job after a removal marker.
