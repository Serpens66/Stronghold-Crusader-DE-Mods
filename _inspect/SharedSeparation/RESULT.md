# Shared separation and independent APIShared repository

## Baseline and ownership

The clean starting commit was `a7888900e217fbf9bc213d540cde4f77bb0177dc`.
The previous working state and helper hashes are recorded beside this report.
The workspace Shared directory now groups mod runtime helpers, public API adapters,
development tools and tests by responsibility. Existing mod-side C# namespaces are
preserved independently of the new filesystem paths.

APIShared compiles only its own sources. Production consumers reference its public
assembly, never its internal source files. The eleven independently owned helper
implementations record their origin; they are not automatically synchronized.
Both dependency-free JSON implementations have independent behavioral coverage.
The root AGENTS.md was updated locally; it remains excluded by existing Git policy.
APIShared has its own tracked AGENTS.md for standalone contributors.

## Dependencies and tests

Installed BepInEx already treats BepInDependency(GUID, version) as a minimum.
Installed Script Extender already supports arbitrary Dependencies entries with GUID,
inclusive MinimumVersion and optional MaximumVersion in info.json. Their existing
implementations were inspected; no Extender fork changes were needed.
MinimumScriptExtenderVersion remains supported. Generic dependency manifests now
mirror the effective existing hard requirements; soft dependencies remain optional.
Duplicate plugin declarations were consolidated without increasing effective minima.

Hardcoded APIShared current-version assertions were removed from consumer tests.
The maintained tests check dependency consistency and actual API behavior instead.
Standalone tests and public consumer examples live in APIShared; additional
workspace integration tests remain in the mod repository.

Final verification passed:

- 97 projects and 1,489 explicit source/project links, plus 41 runtime manifests.
- Runtime JSON/lifecycle/scheduling, permanent-hook, XAML root and CRLF checks.
- 63 projects compiled by source analysis against real installed game/Extender assemblies.
- Git member comparison: 145 implementations, zero unexpected executable-member changes.
- 42 behavior checks on the independent JSON implementations.
- APIShared, preset, public-consumer and workspace integration regressions.
- Existing missions, HUD, host/client, gatehouse, formation and MoatMove regressions.
- All 36 changed build.bat drivers, including their own required checks.
- Release status/setup, Nexus, release wrappers and Script Extender update tests.
- Isolated subtree bootstrap/import/export roundtrip and preservation of unrelated files.
- Standalone APIShared checkout build without workspace dependencies.

Build logs are retained locally beside this report and excluded from Git.
OutpostTest and HunterQueryTargetDiagnostic were built with /noinstall to preserve
the previous diagnostic installation selection. EnemyBridgePathTest was likewise
built without installation because of the previously known Fixes hook conflict.
All other changed drivers built and installed successfully. Versions were unchanged.

The separate pre-existing Workshop artifact compatibility fixture remains stale:
it expects 840 archive entries while the installed SerpsMods archive has 883.
Its old artifact version/hash assumptions were not relaxed to make it pass.
This fixture is outside the successful structural/runtime regression gates above.

## GitHub and subtree

Canonical public MIT source repository: https://github.com/SHCDE-APIShared/APIShared
Independent local checkout: ../SHCDE-APIShared
Independent source commit before the initial join: `77e0c62`.

The workspace keeps APIShared as ordinary files under a controlled Git subtree.
Only explicitly selected community revisions are imported. Export contains only
APIShared sources; generated packages and old workspace history are not published.
No hardlinks, automatic synchronization, new administrator invitations or binary
release publication were introduced. GitHub access uses the existing Serpens66 CLI login.
The workflow is documented in Shared/Tools/ApiSharedRepository/WORKFLOW.md.

## Remaining gameplay acceptance

Static checks and successful builds do not replace a controlled game start after
startup cleanup, host/client lobby convergence, preset/save roundtrips, missions,
HUD/recruitment, gatehouse and formation/MoatMove checks with the installed Fixes mod.
No new gameplay results are claimed. Keep versions unchanged until this acceptance.
