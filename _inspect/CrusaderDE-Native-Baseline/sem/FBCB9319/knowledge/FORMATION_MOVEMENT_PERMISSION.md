# Formation fallback and native movement permission

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Installed hash rechecked on 2026-10-11. Semantic function names remain candidate;
the following predicates/data flow are static findings, not a new gameplay result.

## Permission precedes path construction

The managed release/native command chain is documented in MOVE_COMMAND_RELEASE.md:
cursor staging 0x195E30, synchronized staging 0x196100, group dispatcher 0x11B520,
and terminal unit movement 0x196280. IDs are one-based. Group membership comes
from 0x119F90; tribe records use stride 0x688 and unit records stride 0x490.
The native unit-manager record prefix is 0x658, distinct from GameUnit field offsets.

0x11B520 checks target bounds/availability/flags, chooses ordinary, Assassin or
moat routing, and may choose common-group dispatch 0x118E00. Its normal per-member
assignment/execution branches check alive/kill markers, several unit state flags,
AI state, type and siege deployment state before writing order metadata or calling
0x196280. Type exclusions include 0x28 (Trebuchet), 0x29 (Mangonel), and 0x3D
(stationary Ballista), verified against the managed eChimps enum. These predicates
are present before both slot assignment and terminal execution. 0x118E00 has its
own member checks, including a nonzero field at native unit record offset 0x446
(manager+unitId*0x490+0xA9E). Its acceptance must not be replaced by the normal
branch's type list or a generic IsAlive test.

0x18E1E0 probes path construction; it is not the group-order permission predicate.
0x196280 normalizes coordinates through 0x19B1B0, validates the target, stores the
requested destination, handles already-arrived/cross-component paths, and builds
the packed route. Success publishes path length/state and returns 1. Failure
returns 0 and may enter 0x199CD0's structure-exit state. Neither entry repeats
0x11B520's stationary-type exclusions. A positive path probe therefore does not
authorize a direct manual move of a native-skipped member.

## Managed defect and correction

GameUnitManagerAPI.MoveToTile calls the Extender's terminal move implementation
directly. Its Pre may mutate/veto; Post occurs only after the original and carries
the original input values plus its return value. FormationRuntime previously
retried every captured member without a confirmed formation target, including
members that Vanilla never called. Missing per-member parameters became zero.
This bypassed the native permission layer for stationary engines.

The corrected consumer separately records Pre attempts, matched Post feedback,
and confirmed destination success. Only an unvetoed matched Post with return 0
may schedule a direct retry; skipped members, abandoned frames, ambiguous feedback
and positive returns cannot. Current global identity, life and tribe are rechecked.
The original extra parameter is retained. Native member predicates and moat-route
corrections remain authoritative; no new native patch or permission blacklist exists.

Evidence: semantic query packages for the above RVAs; complete group pseudocode
under `_inspect/APISharedOwnership/native-commands/0x11B520.c`; Extender
GameUnitManagerAPI.MoveToTile and BulkUnitDetours.c_game_unit_issueorder_movehere_hook_impl;
BugfixesAndQoL FormationRuntime.OrderEvents/SlotHooks; production-source regression
FormationMovementEligibilityTests. Singleplayer/multiplayer game acceptance is pending.

## 2026-10-11: preserve native ground cursor permission

Native SHA-256 remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
The complete cursor CFG [8C5F0,90500), including its embedded dispatch tables,
shows a separate permission channel from route reachability. At 8F10F R15D
starts at one. Target veto 198C40 at 8F1E5 and selection predicate 1811A0 at
8F1F8 can clear both EBX (reachability) and R15D (permission). 1811A0 scans
selected living, non-killed units controlled by the active native player and
requires a type other than 28/29/3D. These are the native stationary engines;
no mod-maintained type list is introduced. 198C40 retains its target/structure
restrictions. From 8F206 to the call at 8F325 no instruction writes R15.
Function names remain candidate; these register/data-flow findings are static.

The 196870 call at 8F325 is the native Assassin/special-selection fallback,
not a fresh movement permission check. Promoting its zero result from a
positive 18E1E0 path witness previously re-enabled EBX via E2CA0 at 8F350,
bypassing the prior permission veto. This produced false accepted cursor
feedback; 195E30 stages Chore17 before its own feedback/type checks. The later
formation fallback fix protected execution but did not protect this UI path.

The existing six-site adapter now passes its RVA and, only at 8F325, R15D.
The callback clears pending cursor state and retains a zero result when that
native ground permission is zero. Nonzero original/Extender results remain
full-width and authoritative. The other five sites are attack queries and
never consume their unrelated R15. Existing reachable-member and moat route
corrections remain after the permission gate. No marker suppression, extra
order, second 196870 detour, permission probe, or public interface is added.

Producer bytes and the existing complete displaced spans are checked before
publication. Production emitter tests execute all six Win64 callback paths
with permission zero/one and zero/nonzero/full-width results. The complete
production cursor fixture exercises pending-state clearing, provider settings,
route repair and attack isolation. Rendering still reads final native feedback;
formations and Shift commands continue through the original group/member
checks. Visible Vanilla comparison and multiplayer acceptance remain pending.