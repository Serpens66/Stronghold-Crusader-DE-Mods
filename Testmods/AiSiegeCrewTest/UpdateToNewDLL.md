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
