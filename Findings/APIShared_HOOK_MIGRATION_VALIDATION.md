# Final validation — APIShared migration

Date: 2026-10-10.

APIShared full build: 168/168 tests passed (88 Core, 66 installed/native, 14 preset); public consumer and third-party example compile. Workspace consumer compatibility suite passed. All eight build drivers completed successfully and installed matching DLLs.

| Mod | Manifest version | APIShared minimum | Build / installed SHA-256 |
| --- | --- | --- | --- |
| APIShared | 0.6.0 |  | 7E5286EF75ABD18579B9E30847707CBF2CF1C27BC7B5A058FA123322B6E46F63 |
| UnitCosts | 1.0.34 | 0.6.0 | 58A3740DF6B96276B95C542C275BB1AC9DF09C635FE70C3491893ED3CAAA34C3 |
| UnitLimit | 1.0.104 | 0.6.0 | AACE65D28DFD730B3D7FCE0C28C82F93946283BD17A22B076E242FAD1A526348 |
| BuildingCosts | 1.0.111 | 0.6.0 | 1C725F8AD707155BE3097D10BB91F681C5904C8686E30807E0DB31D0098E4C2E |
| BuildingLimit | 1.0.29 | 0.6.0 | 2B642152EEDDA14C4AD538AAA6DD2E9038F1D5EB6C8F574A66397F8F68CD4C45 |
| ExtraFeatures | 1.0.111 | 0.6.0 | C1D071A9814AB122D80519707CAB9A3B08FA8091965C7DC30E678C5151568122 |
| BugfixesAndQoL | 1.0.181 | 0.6.0 | 85D0D7E920F167CE986C8322665B6AF9228A18E958B9004EDF8B2ABFCE88483B |
| CastlePlanner | 0.8.39 | 0.6.0 | 43728E56FC9EB69F4488C3D55A6E143A30E6235B4FFB919196DEBB31B2A9BA77 |

All installed DLL hashes equal their corresponding built packages. APIShared assembly/file/informational/plugin/manifest version is 0.6.0; remaining 0.5.0 APIShared entries are historical changelog records. All seven migrated consumers declare minimum APIShared 0.6.0 consistently in plugin metadata and manifests. Their own versions are unchanged.

Additional build coverage: CastlePlanner 98/98 AIV tests; ExtraFeatures repair/keep-range/native backend tests; Bugfixes host/client preset, formation, waterboy, tanner, workshop, worker-pause, raid, native and moat suites. All required build checks passed.

Final source review includes sticky cancellation, failing-handler rollback, registration-private state, accepted preparation after all vetoes, replacement actions deferred until all Pre callbacks, exception cleanup, final recruitment reservation/type matching, startup-cleanup persistence, unpublished initialization rollback, removal of duplicate detour ownership, full native market bytes and productive backend ABI/trampoline tests.

Runtime limits: no interactive game start or multiplayer host/client gameplay run. No claim of arbitrary compatibility with a foreign independent native patch on the same instructions. Those price targets fail closed if occupied. README files and Script Extender source are unchanged. No commits or publication performed.

Detailed decisions: [hook migration audit](APIShared_HOOK_MIGRATION.md). Raw build logs, installed-artifact report and analysis evidence remain in .inspect/APISharedHookAudit. Baseline updates are scoped to the current native/managed identities.

## Final version release after game acceptance

The maintainer reported clean logs and no observed ingame errors on 2026-10-10. APIShared was already 0.6.0. The seven consumers now use UnitCosts 1.0.35, UnitLimit 1.0.105, BuildingCosts 1.0.112, BuildingLimit 1.0.30, ExtraFeatures 1.0.112, BugfixesAndQoL 1.0.182 and CastlePlanner 0.8.40. Plugin constants, active manifests and explicit assembly metadata are consistent; prior versions remain only in historical changelog entries. Minimum dependencies are unchanged.

All eight build drivers passed again and installed matching DLLs and manifest versions. Release logs and the installed SHA-256 comparison are retained in .inspect/APISharedHookAudit/release-*-build.log and release-installed-artifacts.json. This version-only release changes no runtime behavior. No commit or publication was performed.
