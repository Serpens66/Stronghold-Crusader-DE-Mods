# Gate/drawbridge automation acceptance

Implementation date: 2026-10-05. Versions intentionally unchanged. Offline tests cannot establish real gameplay or multiplayer acceptance.

Pending game tests (run with Script Extender, Fixes, BugfixesAndQoL reachability and ExtraFeatures):

- Each of auto/auto, auto/manual, manual/auto, manual/manual: enemy enters/leaves; direct open/close on each building; both initial gate states.
- Two connected bridges with different modes; all orientations; an unlinked bridge must gain no enemy scanner.
- Existing raising/lowering animation and occupied bridge: retain native completion/occupancy rules.
- Demolish/rebuild including reused slots; new Global-ID starts automatic.
- Save/load with mixed modes; older saves; editor map typed locators; disable/re-enable the mod.
- Custom close distances/reopen delays, reachability rejection, and elevated drawbridges.
- Real host/client session, proven by GameModeSnapshot with multiple humans in both logs: synchronized toggles and commands, then save/load. Inspect for callback errors and `GATE_AUTOMATION_POST_STARTUP`.

Do not bump versions or change README until this acceptance has been completed with the user.

## Offline result

APIShared and ExtraFeatures were built and installed successfully with their own build.bat drivers on 2026-10-05. The productive automation generators assembled, fully decoded and executed through the installed RedBird X64InlineHook backend in a private synthetic native module. Tests cover both predicate states at every hook, manual cooldown signs, reopen delay zero/nonzero, both recipient directions, human/AI delay selection, strict distance boundaries, one-based IDs, volatile registers and SIMD. APIShared baseline/preset/consumer and ExtraFeatures native regressions passed. Static JSON, lifecycle, plugin callbacks, permanent hooks, XAML and CRLF checks passed. APIShared test infrastructure emits existing dependency/compiler warnings; ExtraFeatures builds without warnings. Build logs are retained alongside this document.

These results do not replace any pending in-game tests above.

## Follow-up code review

2026-10-05: Reviewed native continuations, full coupling loop, callback preservation, live building/Global-ID validation, map/save restoration, activation and UI bindings again against the same installed native hash. Fixed late editor locator resolution: this can run after ApplySettings, so it now clears a persisted gate sentinel when the mod or automation capability is inactive instead of reintroducing manual control. Bridges still never write gate timer fields. Normal map loading already runs ApplySettings after BeginMap; the defect was the later editor HUD/spawn path.

The added test executes the full 519-byte Vanilla C5300 body with both productive recipient hooks, retaining every command/timer write and both loop iterations. Only spatial lookup and the per-building predicate are fixtures. All 320 preference/state/source/fallback combinations pass, plus unlinked building paths. The existing generated-stub ABI/delay/distance tests and all build-driver regressions pass. ExtraFeatures was rebuilt and installed; versions are unchanged. Evidence: review-apishared.log and review-extrafeatures.log. Gameplay, real spatial linkage, occupancy/animation execution, and multiplayer acceptance remain pending.
