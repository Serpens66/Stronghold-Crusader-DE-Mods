# AI siege crew simulation fixture

Only explicit owned `.run` requests activate this separate testmod. It starts an
ordinary local Skirmish, uses public APIShared automation and public Extender
creation/group APIs, and performs native reads/writes under `InSimulation`.
No mouse or keyboard is controlled. Configurations, saves and installed packages
are restored by `Run.ps1` and the common transactional runner.

All direct native accesses require SHA256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Unknown images fail before native setup. There is no layout fallback.
Production patch and recruitment contracts are in
`../../BugfixesAndQoL/UpdateToNewDLL.md`, section AI harassment recruitment readiness.

The on-demand ordinary deployment entry is `0x2ECD0`, Win64 `(AI manager, owner)`.
The AI manager base is obtained from the Extender's resolved AILordManagerRVA.
It contains AICs of stride0x5E4; harassment mapper list is+0x1D0 and maximum+0x1F0.
Player offsets with owner stride0x583C: AIC index0x379D0D0, gold0x379B2DC,
machine index0x379E6E4, cooldown0x379E6E8, live machine count0x379E6EC,
role22 engineer count0x379E79C, reserve ID0x379F7BA and global0x379FB5C.
The fixture assigns its human slot an existing opponent AIC for this disposable
world and invokes the normal AI decision. Other AI scheduling is not simulated.
The live first word at Fixes' downstream order span0x2EFCB is logged for comparison.

Unit anchor is manager0x67E8400+ID*0x490. The slow scenarios seed the audited
pending recruitment fields on one of the fixture's two initialized, identified
engineers: state6D at+0x918, transition counter0 at+0x920, pending Engineer at+0x922,
pending state0 at+0x924, recruitment1 at+0x9CA, visual0x0100 at+0x986.
The peasant scenario also seeds current type Peasant at+0x6E6. These are controlled
state reproductions, not an assertion that the ordinary recruiter was observed.
Native conversion, fade, movement, pathfinding, orders, construction and loss then
run in the actual game without substituted callees. Native role22 is at+0xA82.
Crew count+0xA0C, ID words+0x96C and global dwords+0x974 must match both spawned
identities, both bound in state5 with recruitment0. Public KillUnit triggers
ordinary device destruction. The fixture waits180 ticks and requires both crew
identities gone before repeating, for three cycles per scenario.

`control-peasant` disables the existing host option before starting the match and
requires Vanilla to advance the machine index and leave one reserve member after
the original order. Run this control with `-WithoutFixesDelay`. Other scenarios
enable the host option before the match; options are never changed during play.
`slow-peasant` and `slow-engineer` require no deployment in repeated same-tick
attempts and after one and two simulation ticks, then allow native readiness.
`ready-cycles` checks immediate deployment with already ready recruits.
Run with and without Fixes' startup delay; the wrapper restores its exact original
configuration bytes. Logs certify only completed steps and matching run identities.

For a new DLL, repeat the complete production audit and recheck all setup offsets,
public interop fields, deployed engine mapper, function ABI and normal death path.
The fixture must never bypass production guards to turn an unknown image green.

## DEVDIAG save and binding observation

`build.bat -SdkRoot <verified SDK> /devdiag /nopause` compiles the diagnostic
observer through Shared.DevDiag. Without `/devdiag`, all observer registrations,
native readback and detailed logs are excluded. Save scenarios fail explicitly
without that build. The normal production mod contains none of this test logic.
`Run.ps1` accepts `-KeepInstalledLayout` to retain byte-identical unrelated plugins
when an external process holds a DLL open; the common runner still verifies and
restores the entire selected installation and APIShared plugin/patcher pair.

Recognized save scenarios are `save-before-on/off` and `save-happened-on/off`.
They load the named original save through public AutomationRun.LoadSave with the
explicitly selected current settings and observe at least6656 native ticks,
beyond the supplied snapshots'5639-tick gap. They inject no units, groups, gold,
AI orders or repairs. Fresh PlayState and the normal engine monitor guard all
readback. Original saves are covered by the common runner's inventory rollback.

The process-rooted EngineRunEvents observer samples only advancing native time,
under the original EngineInterface.threadLock. Existing tribe-order and unit
transition events report requests, not proof of execution. Completed native
records provide slot/global identity, type/life/state, recruitment, pending type,
tribe identity, timeout, positions/path, device crew pairs and tent progress.
A128-tick summary compares native role22 accounting with physical live Engineers.
Device crew references use per-type capacities and compare global identities;
new alias pairs are counted separately from aliases already present at load.
Save-before-on fails on any new alias pair. Diagnostic faults or insufficient
native-run samples cannot produce a successful save observation.

No additional native hook is installed by this observer. The module-relative
unit/device/pool fields use the production crew contracts; bounds are1..9999 for
units,1..3999 for buildings and1..4499 for tribes. Tent manager0x64CCBB0 has
stride0x32C; crew count+0x1F6, IDs+0x1FA, globals+0x204 and raw progress+0x2D4
are the existing builder/provenance layout. Detailed output is bounded to12000
lines; the counters and guard checks continue after that logging budget.

Early slow-recruit probes run at simulation speed5 because WaitForTicks requests
at least a number of ticks, not exact single stepping. Speed250 resumes after
those probes. A delayed Unity frame must not be mistaken for an unchanged
recruitment flag when the actual native transition already completed.

`Inspect-Saves.ps1` is a separate read-only PowerShell7 CLI. It compiles the
existing MapParser.Core in memory, verifies canonical hash and native load
section descriptors, DCL/CRC/lengths and unit/tribe ID bases, and emits JSON.
No game process or installation is involved. It compares actual ID/global crew
pairs; missing claims alone are reported without declaring an engineer stranded.
