# Recruit Transformation and Rally Tracking

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

## Consumer contract

- Per-unit consumers that formerly had an outer `AliveState == 2` gate must skip all feature logic while the slot is in State `4` without invalidating identity or feature-owned tracking. Unit deletion remains a separate event/state transition.
- BugfixesAndQoL rally tracking is therefore keyed by the unchanged one-based unit ID plus GlobalId and owner. Its native cadence fastpath must replay Vanilla for every non-alive state and resume validation when the same slot returns to AliveState `2`.
- Observed trace: all affected recruits reached the cadence hook 33 times as type `1`, AI state `109 -> 0`, TransformType unchanged, and AliveState `2 -> 4`. Clearing tracking at that point prevented the final target type from receiving its rally animation and speed bonus.
