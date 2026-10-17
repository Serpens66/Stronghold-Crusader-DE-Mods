# Lifecycle observation installed — game acceptance pending

Completion 17.10.2026: canonical `build.bat /nopause /lifecycle` succeeded after
the user closed the game and restored the plugin directory they had moved.
Backup: `backup-2c4a1515dbf548dca1786f99aeaa697d`. Build log: `build-final.log`.
`installed-final.json` confirms installed outputs match the build, both switches
bind to the new patcher, APIShared and both loaders are unchanged.

The latest existing log revealed two original Workshop calls in one start.
Each is now captured under a separate ID and immutable report; the checker verifies
all snapshots, ordering and the latest alias. Runtime gate and checker regression
tests cover two successive calls, overlap, duplicate completion and missing phases.
The final build reran all relevant checks including all 96 package cases.

User-authorized cleanup: historical package fixture payloads are deduplicated in
`../compacted/`. Reports remain readable; archive manifests preserve all original
file mappings, bytes and link metadata. Accepted startup archives and this rollback
backup remain untouched. Earlier progress below is historical, not current status.

17.10.2026. Runtime sources and drivers implemented; no runtime build or install
completed. `build.bat /nopause /lifecycle` refused the running game before backup,
compilation or installation. Elevated process check confirmed PID 35820, start
02:01:40 local time. Do not close the user's game automatically.

Passed: actual DMD transforms and negative cases, 75 dependency comparisons,
inventory/correction/early-entry tests, ownership/metadata cases, synthetic log
checker including lifecycle build/run/completeness checks, continuation failure
cases, shared-boundary/lifetime and dependency metadata validation, and 96 actual
package/removal cases. Logs are retained here; package evidence is linked in AUDIT.

The first package-test invocation used a non-allowed fixture directory and was
correctly rejected. The unchanged suite subsequently passed in its authorized
default workspace fixture root. No removal-scope checks were relaxed.

The previous binary preflight rejected SE 2.14.2 against the old 2.14.1 pin.
Following explicit user authorization, the installed 2.14.2 methods were audited
and the diagnostic probe's target hash updated. `verify.ps1` and real-method
tests pass against the new target. APIShared has no SE maximum or SE hash gate
for this bootstrap and was not changed, built or installed.

`installed-at-stop.json` confirms unchanged installed probe/APIShared. Historical
2.14.1 accepted artifacts remain separate. The old installed probe cannot provide
the new lifecycle report and rejects correction under the newer Extender hash.

Next: one full startup, usable menu, short wait, no save. Unexpected update dialog: No.
Evaluate `Check-Run.ps1 -Mode Active -RequireLifecycle`, current Unity/BepInEx logs,
then read-only `Preview-Lifecycle.ps1`. No automatic removal or subscription work.
