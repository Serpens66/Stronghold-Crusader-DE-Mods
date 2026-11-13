# Knight replacement selection and input lifetime [Scope: Mod work, not Vanilla]

Static audit and implementation snapshot, 2026-11-04. Native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Both match the installed binaries and CURRENT.json / DATABASE_INFO.json.
Function labels below are mostly automatically generated `candidate` names;
the stated parameter, field and control flows were separately inspected in the
current exports and, for input/output boundaries, installed machine code.

## Creation, removal and local selection [Vanilla facts]

- Allocator RVA `0x17FEF0` sets the new slot's life state to 1 (`NeedsInit`).
  Its type, standing/tile and visual initialization calls (`0x19A240`,
  `0x180230` -> `0x1802A0`, `0x18BE50`) do not finalize state 2.
  The later unit loop `0x182B00` performs state 1 -> 2.
- State 3 requests deletion. The unit loop dispatches `0x1869C0`, which removes
  local marking via `0x186C20`, synchronized selection via `0x19A5D0`, then
  clears the record. Removing a selected source does not select its replacement.
- Output `0x19D960` emits one-based IDs only for state-2 units passing exclusion,
  view-owner and local-mark checks. Installed checks are at `0x19DEE0`,
  `0x19DEEA`, `0x19DEEF`, `0x19DEFE`; ID/type/count writes follow at
  `0x19DF07`, `0x19DF14`, `0x19DF31`.
- Local marking is the WORD at typed `GameUnit +0x30` (manager record +0x68C);
  synchronized selection is a different field, typed +0xAE. Slot stride is
  0x490 and IDs remain one-based. A slot lookup alone does not prove identity.
- `DLL_TroopSelectionChanged` (`0x87B20` -> `0x19A7E0`) requires a unit already
  to carry the local mark; it cannot add a newly unmarked replacement.

## Selection phases and the engine callback boundary [Vanilla facts]

`EngineInterface.run` holds its private `threadLock`, calls
`EditorDirector.preDLLCallActions` -> `TroopSelection` once, then calls
`DLL_RunTick` (`0x86680`). Native input `0x8C5F0` executes before
`0xCDE60` -> `0xD1AD0`; the Extender time site is `0xD1D4F`.
That ordinary run path reaches the time site at most once.

The native double-click branch can transiently expand a selected candidate to
all visible units of its type: same last-click identity, elapsed <500 ms and
mouseState 1. Installed comparisons/calls at `0x8F01B`, `0x8F02F`, `0x8F043`,
`0x8F04A`, `0x8F05A`, `0x8F062`, `0x8F07C`, `0x8F084` establish this branch.
This requires actual selection input; Stop subcommand 0x1F does not itself
write the local selection mark.

The time path also runs during initialization (`0x71670`) and editor resizing
(`DLL_SetMapEditorParam`, `0x87460`). Normal resize callers do not hold the
engine monitor. Therefore arbitrary time callbacks are not input acknowledgments.

## Extender handoff and remaining cancellation boundary

Installed SHCDESE SHA-256:
`5CB1C33BE5463086505A17F5C48AAA357D733CFFF7240E101C821F0E3C668498`;
canonical source commit `4014ca67c96974a5e6d697665baf5351cbfebde6`.
The inspected managed wrapper and installed API queue match: `SetSelectedChimps`
copies IDs into the static SelectionSetter; three later managed input calls
consume Press, Drag, Release. Three subsequent ordinary engine callbacks under
the already-held monitor conservatively cover these phases in the same mission.
Pause/skip can delay acknowledgment; this is not a fixed wall-clock deadline.

The installed API has no public cancel/token. Its internal Reset is called only
on the managed input hook's exception path. Pausing can suspend outstanding
phases; Vanilla GamePaused clears its own selector, not this managed queue.
A mission change before consumption can therefore carry old slot IDs into a
new mission. A private reset/new hook/overriding request is not a risk-free
cancellation and can affect another caller. No such intervention was added.

User decision, 2026-11-04: accept this rare boundary rather than introduce risk
for a three-input-call window (about 75 ms at the default rate; pause can extend
it). No practical cross-mission failure was reproduced in-game. Do not report
this again as a new defect or add mitigation without new runtime evidence or
an available demonstrably low-risk cancellation path. Keep local context guards.

## ExtraFeatures integration and verification [mod integration]

Commit `8f63c6f91` introduced a work gate that omitted pending selection.
A completed three-unit batch could remain dormant until a later two-unit batch
reactivated ticks, restoring the former three-unit selection before the new
delay elapsed. The replacement batch then stalled in turn.

The repair uses stable replacement identities, independent initialization
budgets and a bounded in-flight gesture. New requests merge fresh local marks
with ready replacements. The exact current gesture membership is retained only
for source entitlement during its transient native marks, never for constructing
a later request. Mission/player changes and deactivation clear all local state.

Evidence: `.work/a/knight/001/review.json`, retained build 821, frozen SDK
`.work/a/kn-sdk/001`. Production-queue/native tests (447 assertions), actual
SDK/game signature checks, full consumer build and installed package hashes pass.
The user confirmed the original three-knights/two-swordsmen round trip succeeds
in-game. Other editor/lifecycle cases and real host/client acceptance remain pending.
