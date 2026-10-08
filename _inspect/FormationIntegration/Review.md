# Formation integration review — 2026-10-08

The implementation was compared with Git HEAD's FormationTest runtime, model,
release state machine, packet, menu, overlay and preview model, and with the
previous BugfixesAndQoL renderer. The generated `review-*.diff` files preserve
these comparisons. Existing unrelated changes were preserved.

Formation geometry, role assignment, deterministic ordering, protocol 5 and
permanent accepted-release consumption are retained. Removed code consists of
the old density gesture/planner/transport, prototype-only default migration,
duplicate native hook installation and duplicate renderer ownership. General
queue, owner/terrain checks, manual probes and pathfinding remain in place.

Integration-specific corrections reviewed:

- Selector failure restores its output triple before continuing through Vanilla.
- Presentation/logging failures cannot prevent the runtime's fail-open path.
- A dispatch scope prevents a synchronized command from being reinterpreted as
  a Shift queue command even when formation preparation fails.
- Selection uses LocalSelectionAPI's checked, immutable completed-state snapshot.
- The internal MessagePack formatter has a public reflection constructor; all
  14 fields round-trip through the installed serializer.
- Shared command callbacks run formation preparation before queue/path observers
  and formation completion after general context cleanup.
- Updated regressions retain the old native and queue checks while following the
  new APIShared renderer location and third IngameUIScreens XAML operation.

Automated validation includes installed interop field types, offsets, sizes and
strides, compilation against the true game assembly, JSON/lifecycle/publication
rules, XAML Content roots, CRLF and whitespace. The full feature-native closure,
installed RedBird contracts and Fixes 1.26.0 hook ranges are documented in both
mods' UpdateToNewDLL.md. No Script Extender source was changed.

The Formation suite passes 9,731 assertions; queue regression passes 8,999 checks;
native BugfixesAndQoL regression passes 1,324 assertions and 67 signatures. The
manual-path suite exercises 1,481 assertions and 35 actual copied-buffer NativeX64
detours. Further driver regressions cover presets/host authority, pathfinding,
unit life guards and existing features. Build logs are retained in this folder.

Runtime versions remain APIShared 0.4.12 and BugfixesAndQoL 1.0.179. README and
version/dependency publication changes await final acceptance.

Both elevated build.bat drivers completed successfully. Installed DLLs and
metadata match the local build packages by SHA256; both integrated XAML patches
also match. The prototype workspace was removed after successful tests and
installation. No standalone FormationTest DLL remains in the game plugins.
The retained legacy config still matches its pre-cleanup hash.

The final framework-reference audit now follows explicit project references;
System.Numerics is included in both runtime projects for the Noesis preview.
The main build retains source-linked Shared type conflict warnings; it reports
zero errors. No game-session acceptance is claimed.

Live game acceptance remains pending; automated tests do not substitute for it:

| Scenario | Expected behavior |
|---|---|
| Checkbox off or permanent runtime fault | Vanilla Move; no density fallback |
| Both mouse control modes, hold/drag/wheel/release | Preview matches placement; exactly one Move |
| Every formation and role placement; large groups | Correct geometry/roles and complete visible target markers |
| Shift waypoints and queued object commands | Existing queue behavior and queue markers |
| Object targets, deselection and changed selection | No interception of object commands; stale drag discarded |
| Map change and save/load | No stale gesture, unit identity or preview; hooks persist |
| Multiplayer host/client | Same packet parameters and deterministic targets; host checkbox respected |

The legacy FormationTest_Serp.cfg is retained as the migration source. Its
pre-cleanup SHA256 is C609655625A676EC4780FEBACFE664F237FF3A0E47F69AC7F54C61278EA9B73B.
