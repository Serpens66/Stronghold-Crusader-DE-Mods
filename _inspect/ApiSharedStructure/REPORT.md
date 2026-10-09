# APIShared checkout and feature structure acceptance

Starting workspace: `c596189f3d934cdc87801093ef7c3a3a7a69b4dc`.
Starting public APIShared: `7715bd72ab4e9796b5adbadb43eb7b795a90c198`.
Final published APIShared: `c9332b5eafbb26c4e655d4fbc3529fe7f3894077`.
Version remains `0.4.12`; consumer versions and published minimum dependencies remain unchanged.

## Checkout and workflow

The existing public clone is now the sole production APIShared checkout, registered
as the workspace submodule. The old external clone path no longer exists. Original
history, local artifacts and configuration are preserved by backup branches,
`_inspect/ApiSharedSubmodule/apishared-before.bundle` and `workspace-api-before.zip`.
The obsolete subtree remote and import/export helpers have been replaced.

Status is read-only. Updates fetch and merge explicitly, protect open changes and
ignored local configuration, preserve detached starting commits, and leave conflicts
for manual resolution. Package validation hashes actual build inputs and package files.
Release validation requires a clean published APIShared commit recorded by the parent.
No automatic push, stash, reset, parent commit or build is performed by the helpers.

## Feature structure

Implementation stays with its subject. Buildings, presentation, pathfinding and
settings have distinct feature areas. Shared native infrastructure is in
`Core/Internal/Native`; command-wide interception and backend contracts are in
`UnitCommands/Native`. Movement, attack, cursor, moat and formation implementation
have named parts. HUD registration, categories, recruitment, images and hooks are
separate parts of the same service. Architecture documents how to add a feature
and retain one owner for shared interception points.

Methods and field definitions are unchanged. Partial classes preserve the existing
runtime owner, publication and permanent lifetime. The Git declaration comparison
against `a48a668` reports 5,899 original declarations, no missing declarations and
no new declarations (repeated partial headers excluded). All 109 non-constant field
initializers in the four split services are unchanged and retain ordering within
each original source part. Independent pre-existing partials can have a different
relative compilation order after moves. Compiled comparison with the archived
pre-migration DLL confirms 2,280 unchanged public API/parameter contracts.

## Verification

- Final APIShared own elevated build: 18 core, 36 runtime and 8 preset MSTest tests;
  public consumer examples compile without friend access; build and installation pass.
- Final community CI: https://github.com/SHCDE-APIShared/APIShared/actions/runs/37864225347
- All 34 unique consumers pass their own elevated `build.bat /nopause` and installation.
  The 40 installed consumer DLL hashes match the local packages. Exactly one installed
  APIShared DLL exists and matches its fresh local package.
- Workspace runtime, JSON, lifecycle, scheduling, XAML, permanent-hook and actual
  installed interop checks pass. Inventory: 98 projects, 1,348 valid explicit links,
  41 manifests with consistent generic minimum dependency declarations.
- Existing APIShared, presets, host/client, HUD, gatehouse, assassin, formation and
  MoatMove consumer regressions pass. Production algorithms and native emitters remain
  exercised; historical Git oracle paths remain historical, not rewritten to current paths.
- Git fixtures cover initialization, detached HEAD, divergent commits, explicit
  revision selection, branch collisions, open files, merge conflicts and abort,
  ignored-file collisions, stale/tampered/missing/extra packages, pin mismatches,
  unpublished commits and historical embedded/submodule source reads.
- Status works from another current directory without changing Git refs. Release
  status is checked after recording the final parent pin. A newer `origin/main`
  is not required when an older reviewed published commit is selected.
- Steam staging checks matching/mismatching/missing local and published DLLs.
  An installation fixture verifies that host cleanup retains independently installed
  APIShared infrastructure and player settings while removing obsolete host output.
- No consumer runtime C# implementation changed. No new game-member access, native
  target, hook backend, algorithm, public contract or persisted/network format was added.
  Affected text files use CRLF; final Git whitespace checks pass.

Obsolete single-file source assumptions were replaced with feature/type-aware readers
and method-body checks. The testmod's keyword search through added Git lines was
replaced by the canonical permanent-hook runtime audit: test strings containing
`NativeDetour` are not executable mutations. Meaningful existing regressions remain.

Full build logs are retained locally in this directory and excluded from Git.
`consumer-build-results.json` and `installed-consumer-hashes.json` record the final run.
Earlier failed attempts were preflight stops (old paths, obsolete driver/test rules,
environment configuration or a transient CRLF editing state), not accepted results.

## Still requires game testing

No interactive game or two-PC session was performed. Check startup cleanup and
persistent callbacks, mission transitions, host/client settings and presets, HUD
extensions, gatehouse behavior, formations, and movement/attack across moats.
Source commits are published; no new version or binary release was published.
