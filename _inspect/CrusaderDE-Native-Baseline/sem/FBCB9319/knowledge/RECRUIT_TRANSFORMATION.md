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
