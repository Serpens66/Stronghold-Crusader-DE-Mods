# Validation snapshot: 2026-11-15

Target: Serps Mods 1.0.37, public APIShared 0.53.0 main release
(`9307434dcf3b59333ae70e6bcd6f7c781a09f616`), Script Extender 2.14.3.
Development remains on the existing develop working tree; no SDK/main mod was
modified or rebuilt for this feature. The exact build inputs and output hashes
are recorded by `obj/build.json`. That file is regenerated only on success.

## Completed checks

- Native production DLL/collector: clean shutdown, handled and unhandled access
  violations, inaccessible pages, chained VEH, concurrent exceptions, capture and
  trail rotation, collector loss, actual Unity Mono invocation and unknown-runtime
  rejection. Actual fault bytes decode as `mov eax, dword ptr [rcx]` with RCX=0.
- Production bounded chunk registry: collision/removal holes, capacity, address
  reuse, overflow and known/unknown destruction spans.
- Collector production-code tests: rolling latest log retention, final-process
  generation guards, compact MAPS2 bytes, generic saturation, priority invalidation
  preservation and 100000 short method records below the priority cap.
- 26 adversarial/behavioral parser tests; all seven original user-supplied crash
  files parse without inventing missing code bytes or method identities.
- 74 compiled-artifact checks: ordinary builds inactive, diagnostic release marker,
  public dependency metadata, x64 exports and P/Invoke ABI/encoding agreement.
- Scoped runtime JSON/lifecycle, dependency, CRLF, ownership and permanent-hook
  checks. These are guardrails, not substitutes for runtime validation.

## Windows game observations

The official transactional runner completed six runs and verified restoration of
the original plugins, settings and saves after each run:

1. Startup and post-cleanup runtime/collector readiness.
2. Load of an existing test copy of attacktest1.sav, current mission context and
   real map ticks.
3. Four alternating baseline/diagnostic 20-second runs of the same save.

| Run | Frames/s | Tick events | Game CPU seconds |
| --- | ---: | ---: | ---: |
| Baseline 1 | 297.746 | 797 | 32.828 |
| Diagnostic 1 | 298.127 | 797 | 29.375 |
| Baseline 2 | 297.738 | 797 | 30.031 |
| Diagnostic 2 | 298.165 | 797 | 29.047 |

No average FPS regression appeared in this fixture. CPU variation does not prove
an optimization or a universal below-2-percent overhead guarantee. These counters
measure the game process; external collector CPU and individual frame-time spikes
are not included. No global mouse/keyboard input or intentional game fault was used.

The final compact MAPS2 disk-format change affects only collector persistence and
offline parsing. The game observations above used the preceding fixed-width map
journal; the final writer/parser are additionally validated in the production
subprocess and byte-level tests. Native shared-memory ABI and game callbacks are
unchanged. The shared game-test window was released to the coordinating chat after
verifying a free test mutex, no own game/collector process and no rollback folder.

## Explicitly open

- Actual Proton execution, including collector launch, file mapping and retention.
- Actual host/client multiplayer with installation on only one peer.
- Reproduction and explanation of the original users' sporadic crash.

This is a Windows-validated diagnostic pilot, not a claim of a diagnosed root
cause or completed Proton/multiplayer acceptance. Detailed Windows evidence is
referenced from `obj/game-fixture.json`; source-controlled validation is this
dated summary. Relevant compact evidence is retained; reproducible fixtures and
duplicate build output are cleaned after completion.

## Distribution review: 0.1.1

Optional inventory failures are isolated from native capture initialization and
post-startup readiness. The same package targets the Windows host and Proton
client without replacing the release SDK or starting game automation. Installation
instructions include first-launch readiness and the four-session storage budget.
This revision is rebuilt with the existing subprocess and artifact checks; no
additional game, Proton or multiplayer run is claimed.
