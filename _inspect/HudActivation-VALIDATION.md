# HUD activation validation — 2026-09-17

## Implemented behavior

APIShared retains its process-lifetime hooks and offers owner-bound `IUnitHudActivationCapability` alongside the existing capability. Legacy registrations default to active; capability lookup alone is passive. Active views and surface flags are rebuilt under the registration lock. Owner, category and image activation are independent. Recruitment requires an actual active handler.

The render callback returns before Unity/ViewModel access when no surface, refresh, restoration or recruitment ticket requires work. Image-only users do not enable unit scans. Visible active categories retain the existing frame cadence, including pause. Restoration runs on the Unity path and waits for HUD readiness. Text restoration checks that the currently displayed value is still the mod's own value. Remaining active image overrides run after the Vanilla sprite path.

Lord-HUD reports its settings; SkinTest reports map activity; VirtualUnitsPrototype reports successful initialization and registration; MainViewModelInitProbe activates only its explicit registration test. Control-group cleanup remains a passive API consumer.

## Automated evidence

- All five runtime projects passed the JSON/lifecycle preflight, CRLF checks and code checks before their builds.
- APIShared, BugfixesAndQoL, SkinTest, VirtualUnitsPrototype and MainViewModelInitProbe were built and installed using their elevated `build.bat /nopause` drivers. All driver tests passed. SHA-256 comparisons confirmed all five installed DLLs match their workspace build outputs.
- BugfixesAndQoL emitted MSB3277 for competing Mono.Cecil 0.10.4.0 / 0.11.4.0 references; build and tests completed successfully. The other four builds reported zero warnings.
- `_inspect/APISharedTests` exercises the actual activation service, including passive users, legacy registrations, independent owners, per-registration activation, recruitment filtering, image-only demand and restoration flags. Its test fixture resolves `Assembly-CSharp-publicized.dll` by its internal assembly identity.
- `_inspect/Verify-HudActivation.ps1` exercises extracted production activation methods and checks render/expiry/army visibility ordering.
- `_inspect/Verify-HudDispatch.ps1` exercises previous/current production render entry methods with managed stand-ins. It verifies active frame cadence, pending restoration, delayed HUD readiness, image-only idleness and inactive ticket maintenance. Area bodies and ticket expiry itself are stand-ins; this is scheduling evidence, not an in-game UI or ticket integration test.
- `_inspect/Verify-HudResources.ps1` exercises previous/current production tint methods with managed Noesis stand-ins, including changed inputs, null images and external property overwrites.

Run each standalone PowerShell verification script in its own process: their deliberately minimal stand-in type names overlap.

For 10,000 calls in the dispatch fixture:

| Scenario | Unity frame reads | ViewModel resolutions | Area dispatches |
| --- | ---: | ---: | ---: |
| Previous, no registrations | 20,000 | 0 | 0 |
| Current, no registrations or passive owner | 0 | 0 | 0 |
| Previous, registered but unused category | 20,000 | 10,000 | 50,000 |
| Current, registered inactive category | 0 | 0 | 0 |
| Current, image-only after refresh | 0 | 0 | 0 |

For 10,000 stable tint updates, brush creation fell from 20,000 objects / 480,000 allocated bytes to zero after cache warmup. These numbers measure standalone managed fixtures, not Unity frame time, total game allocations or native Noesis allocations. Real named-control lookup counts were not instrumented in-game.

## Still requiring an in-game check

- No HUD users; only control-group cleanup; last-user disable and subsequent idle frames.
- Two independent users, one disabled; image-only user; late registration and queued refresh.
- Disable/reactivate with a recruitment ticket pending, including actual completion and timeout.
- Pause, panel recreation, map transitions, Lord-only/mixed/empty selection, death and language changes.
- All four army pages; restored Vanilla counts, text and images after deactivation.
- Actual frame timings, named-control resolutions and allocations in Unity/Noesis.

No game session was launched for these changes. README files, source versions and dependency minimum versions remain unchanged during testing. Final release version/minimum-version updates await successful in-game validation.

The native/managed audit is recorded in `CrusaderDE-Native-Baseline/sem/FBCB9319/knowledge/HUD_PRESENTATION.md`. Build logs are beside this file as `HudActivation-<mod>-build.log`.
