# Pitch and moat lines: acceptance handoff

Snapshot: 2026-11-11. Working source on `develop`, base commit
`85d40158bf473f88616a5d9889ce80089779baf9`. This is a local candidate,
not an accepted release. No commit, publication or game installation was made.

## Implemented

- Default-enabled personal `EnablePitchMoatLines` setting, gated only by
  `EnableClientFeatures`, with localized client-control QoL presentation.
- Shared preview/placement field list, native cost row, cached tooltip numbers,
  skipped obstacles and existing fields, bounded 64-field lines.
- All mission modes admitted, including solo-loaded multiplayer saves.
- Multiplayer pitch uses one authenticated field Chore and native stock/build
  semantics, with exact planned-moat preservation. Moat retains Vanilla commands.
- Queue-backed confirmation, no wall-clock execution timeout, no resend of an
  admitted command, and a Busy guard for the preceding native command.
- Native ownership and update contracts: [PitchMoatLines.Native.md](PitchMoatLines.Native.md).

## Completed automated verification

- Normal checked build: `.work/b/working/1144.build.json`, exit 0, quick suite.
- Diagnostic checked build: `.work/b/working/1141.build.json`, exit 0, quick suite.
- Retained diagnostic artifact build: `.work/b/working/1143.build.json`, exit 0.
  Its compilation-only run does not replace the checked diagnostic build above.
- Production raster, skipped-field, capacity, affordability, all four payment
  phases and peer-mask policy tests passed.
- Production native emitter: 36 executable ABI/branch cases, native/106 scopes,
  arm/retirement and queue identities; network coordinator and real MessagePack
  schema tests passed.
- Production settings metadata/UI check passed (1,531 checks); the relevant
  HostClientPresetTests persistence/reset/local-option case also passed.
- Adjacent integration checks passed: MoatTerrain 6,176, BuildPreview 3,468,
  and PitchStairs 11,361 checks, plus the normal checked-build suites.
- Source boundaries, lifecycle/JSON, permanent hooks, dependency metadata,
  tooltip/XAML/locale parity and changed-file CRLF checks passed.
- SDK transport/security contracts in the historical APIShared 0.43.0 revision
  were compared with the selected 0.46.0 SDK and are unchanged. Minimum stays 0.43.0.

## Preserved packages and preparation

`.work/a/pm-stage/001/n` is the normal plugin directory; `d` is diagnostic.
Both contain 50 files verified against their respective build output.
The adjacent normal/devdiag ZIP files use the installable
`BepInEx/plugins/BugfixesAndQoL_Serp/` layout and every entry was hash-verified.
The normal package passes the development-diagnostics release guard; the
diagnostic package is correctly rejected by that guard.

Build records, file SHA-256 manifests and package provenance are beside them.
The source-tree package under `BugfixesAndQoL/BepInEx` was not overwritten.
The candidate keeps version 1.0.220 pending completed game acceptance.

The current diagnostic package, including the completed local acceptance inputs,
is `.work/a/pm-stage/002/d` (build 1147); its exact test sources, package hashes,
build proof and run references are in the adjacent `proof` directory.
Stage 001 remains the earlier normal candidate and historical diagnostic build.

Prepared automation and scenario instructions:
`.work/t/pitch-moat-migrate/001/AcceptancePreparation.txt`.
Current portable peer bundle: `.work/t/pitch-moat-peer/002`, including matching SDK,
bootstrap, dependency layout, exact compiled test sources and build proofs.
Windows PowerShell 5.1 ValidateOnly runs passed for local preparation and all
six multiplayer role/variant combinations. These are runner checks, not game tests.

## Completed local game acceptance

The user subsequently authorized single-player tests. All four local scenarios
passed and each rollback runner verified restored plugins/settings and saves.
The game is closed and no rollback directory remains. No OS mouse or keyboard
input was sent, and no candidate remains installed.

| Scenario | Evidence under `.work/t/pitch-moat-migrate/001/results/` |
| --- | --- |
| Enhanced SP throughput, native HUD and renderer (1147) | `4e717b177274` |
| Enhanced editor, player 2 and interrupted batch (1147) | `f88649ea07fe` |
| Disabled line option, native ordinary/Shift brushes (1146) | `ae9428efe5b5` |
| Disabled client functions, native ordinary/Shift brushes (1146) | `d4011104240e` |

The SP/editor runs confirm 64-field lines on consecutive native map ticks,
all four pitch phases, no placement/debit while held, both overlay orders,
seven abort paths, native 0/1/16 cost-row values and pitch icon, and actual
half-alpha native sprites/blue moat tint. Native ordinary/Shift footprints are
pitch 1/4 and moat 9/25 fields. Editor player 2 ownership passed; switching back
after prefix 1 discarded the remaining released batch. The captured command had
already executed (`wasLive=False`), so this is not proof for an unexecuted queue
slot crossing that switch. The MapType 12 actor branch was not separately forced.

The first attempt `b5a4ebb05c53` timed out before a preview because the diagnostic
input fixture did not fully isolate background focus/physical mouse edges. Its
failure remains archived; the corrected diagnostic fixture passed the runs above.
It is not a demonstrated production placement failure.

## Remaining multiplayer acceptance and cleanup

LENOVO_SERP was unavailable; no real multiplayer acceptance is claimed.
Run genuine two-peer baseline and both asymmetric
option variants, including disabled host gameplay functions, and compare fields,
stock, payment phases, pitch slots and moat task owner/progress on both peers.
Use the standard rollback runner; do not control OS mouse/keyboard without the
separate permission required by AGENTS.md. Preserve the original installed state.

Cleanup update, 2026-11-12: the user explicitly requested removal before the
outstanding multiplayer acceptance. The three temporary acceptance files, startup
call, synthetic input/focus/UI branches and completed gesture/timing diagnostics
have therefore been removed from the main mod. Direct Vanilla/Unity input reads
are restored. Production regression tests and native contracts remain. Archived
diagnostic packages and test sources above are historical evidence; they are not
the cleaned current runtime. Future game acceptance must use an independent test
tool instead of putting scenario startup or synthetic input back into BFQoL.

Cleanup verification: normal build 1167 and diagnostic build 1168 succeeded,
without installation. Both compiled assemblies were inspected and contain none
of the removed acceptance types or temporary bridges. Scoped source/lifecycle,
dependency and tooltip checks, the line policy/network/36 native ABI cases,
and the affected Raid, Outpost and Savegame regression suites passed. The
independent source review found the cleaned line runtime semantically equivalent
to the previous normal-build projection. No game was started for this cleanup.

After the remaining multiplayer acceptance, update the version consistently,
build/check the normal package, replace
the installed BFQoL package with the game closed, and remove the installed old
testmod/config and `Testmods/PitchMoatLineTest` sources. The testmod is intentionally
retained until that acceptance and replacement is complete. Keep historical
`.work` evidence; do not rewrite old build records.
