# APIShared handoff

Current development target: Script Extender 2.4.0 (`5d5719c1002aec043d331162d72b2e7f3111b34b`). APIShared version remains unchanged during testing and keeps minimum Script Extender 2.3.0.

Implemented capabilities are Gatehouse Distance Origin, Gatehouse Timing and Unit HUD Presentation. The selected-unit broker and `Testmods/APITest` have been removed because Script Extender already exposes the complete Pre/Post event.

Production pilots:

- `ExtraFeatures` acquires Gatehouse Timing through `ApiShared.WhenReady` and has no gatehouse timing RVA, scanner or memory writer.
- `BugfixesAndQoL` acquires Gatehouse Distance Origin and applies centered or Vanilla origin from a default-enabled synchronized setting.
- `BugfixesAndQoL`, `Testmods/SkinTest` and `Testmods/VirtualUnitsPrototype` share the Unit HUD pipeline.
- `RandomEvents` uses the Script Extender 2.4.0 path-component grid directly and guards every index against its actual span length.

The native hash remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; managed links and semantic evidence were nevertheless re-audited against 2.4.0. Gatehouse targets are unchanged and still fail closed on any hash, opcode or state mismatch.

The APIShared test suite owns the public-surface allowlist, gatehouse transaction/rollback tests, centered-distance byte regression, HUD snapshot and startup protections, and consumer migration assertions. Remaining manual acceptance is documented in `MIGRATION_PLAN.md`.

`README.md` has intentionally not been modified. Its obsolete version and capability description are a separate documentation task requiring user approval.
