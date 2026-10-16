# Fixes wheat-sale crash and startup correction audit

Date: 2026-10-16. Native SHA-256:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed Assembly-CSharp SHA-256:
BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Installed fixes.dll 1.26.4.0 SHA-256:
6A4ED4F492812BDA9342766AC432954F483732CC864C4CAAD67F9172A4F9E5BA.

## Confirmed machine/dataflow evidence

The trade controller 0x3F270 checks the market and alternates buy/sell by tail
branching to 0x3EC90/0x3EE10. Caller 0x2AE40 passes profile RCX and player EDX.
Seller 0x3EE10 saves EDX to [entry RSP+10] and subtracts 58 from RSP.
Fixes' hook at 0x3F0C9 pushes RDX and R8 then loads EDX from [RSP+50].
The true player slot is [RSP+78]; the old slot holds saved caller RDI.
The additional push inside TestNativeFlag occurs after the bad load.
Fixes reads byte [flagArray+RDX], selects Resource category for enabled wheat,
restores registers and replays 19 original bytes before returning to 0x3F0DC.
Actual sale at 0x29700 updates gold/stock/trade state. There is no player-ID
contract for the caller's RDI. The diagnostic managed wrapper's use of RDI for
the profile pointer is ABI-valid, but exposes Fixes' invalid indexing assumption.

Dump SHCDESE-crash-2026-10-16-18-47-38-777-pid53724-tid54124.dmp:
stub 0x7FFBCAAD5560, fault +19 at cmp byte [r8+rdx],0;
R8=0x26746026040, RDX=0x3E96C950, bad read=0x26784992990;
player slot=1, RDI=9 (wheat). At fault, the extra push moves true player to
RSP+80. Original caller profile was 0x7FFC3E96C950.
The 75-byte stub's player displacement is byte +0C; correct value is 78.
All remaining bytes, including branch/register operations and return, must agree
with the full contract. BugfixesAndQoL's 20-byte reserve hook at 0x3F156 does not
overlap and its inspected context wrapper preserves the relevant stack/registers.

First save load 18:38:25 passed tick 70 at 18:38:27.67; second load 18:47:36
crashed at tick 70 at 18:47:38.77. The first run's erroneous effective address is
not captured. Process-layout-dependent readability is plausible, not a confirmed
reconstruction of that first run. Successful execution does not validate the index.

## Startup access boundary

Semantic callers: 2AE40 <- 57330 <- CDE60. CDE60 is reachable from tick export
86680, load helper 71670 and editor export DLL_SetMapEditorParam at 87460.
Load helpers lead through save/map initialization; therefore tick exclusion alone
does not establish safety. Actual EngineInterface run/load wrappers lock private
static object threadLock. EditorChangeMap_* wrappers call SetMapEditorParam
without that lock. They rule out a general late correction under the lock alone.

Installed BepInEx Chainloader includes soft dependencies in TopologicalSort,
publishes PluginInfos then calls AddComponent synchronously, assigns Instance
only after Awake returns, and sets _loaded only after the loop. Its preloader
inserts Initialize/Start at Application..cctor. APIShared requires this actual
stack, _initialized=true/_loaded=false, unassigned own Instance, a completed
Fixes instance, CLR-null Director/GameMap fields and non-reentrant immediate
acquisition of EngineInterface.threadLock. It repeats checks under the lock.
Missing stack evidence is refusal, not inferred safety. Unsynchronized foreign
startup native calls are outside this boundary. Runtime acceptance is pending.

Fixes subscribes to the Extender's immediate LibraryLoaded replay in Awake and
installs the wheat hook before its plugin Instance is published. Its NativeFlagArray
has nine byte entries in its process-lived NativeStateBlock. Map events update
those flags; correcting the load does not override their values or settings.

## Implementation and verification status

APIShared's independent repository contains the portable identities and exact
contracts in UpdateToNewDLL.md, source under src/Compatibility, and the behavior/
startup limits in docs/FIXES_COMPATIBILITY.md. The explicit approved exception is
one startup operand write 50 -> 78, never disk mutation, later retry or rollback.
Unknown identities/structures remain unchanged. Read-back, protection restoration
and instruction-cache synchronization must all succeed before confirmed protection.

Audit artifacts are retained under workspace _inspect/FixesWheatStartup-20261016:
installed Chainloader, Preloader, X64InlineHook, HookHandle and NativeFlagArray
decompilations. The handoff AIBuildDiagnoseTest-Fixes-Kompatibilitaetsplan.md gives
the independent removal of the diagnostic controller detour for release testing.
No diagnostic source is changed by the APIShared implementation task.

Source/static checks only at this stage; no build, installed correction, backend
execution or game acceptance is claimed. See APIShared's guide for pending tests.
