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

## Installed-binary recheck, 2026-10-17

Native/managed game hashes still match the full identities at the start of this
audit. Installed Fixes 1.26.5.0 has SHA-256
47D77AC11585423AA2B054873C11EC3AF888CF92E955A0BA376F24BA1B7ECE2A;
SHCDESE 2.14.2.0 has SHA-256
577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175.
All other binaries pinned by the startup correction retain their original hashes.
The installed Fixes generator still passes [rsp+0x50] to TestNativeFlag before
its helper pushes R8, so the original player derivation and +0x78 correction hold.
Flags, categories, displaced instructions and continuation retain their contracts.

APISharedTests.FixesWheatStartupTests.InstalledFixesGeneratorMatchesCompleteAuditedStub
invokes a private copy of the installed emitter with only its flag-address source
substituted. All 61 emitted body bytes match. A private executable fixture applies
the production correction and executes the full stub for players 1..8, enabled
and disabled flags, wheat and another good, independent other-player flags and
three unrelated values in the old stack slot. Those cases pass on the desktop
x64 runtime. No foreign game hook is constructed or patched by the fixture.
The complete production binary preflight also passes. Unity byte-loaded managed
assembly paths now use the installed GameRootPath image with hash/name/MVID checks.
These are new fixture observations; the earlier historical evidence is unchanged.
Actual Unity startup and gameplay acceptance remain separate requirements.

## Unity startup confirmation and contract refinement, 2026-10-17 23:14 CEST

The startup trace at 23:10:49 showed initialized=true, loaded=false, own plugin
instance absent, Director/GameMap CLR-null and Awake/Application frame indices
2/6. The loader was the dependency bootstrap's registered MonoMod DMD body, so
the old exact Start-name check missed it (index -1). This was a false refusal,
not evidence that correction had moved into gameplay. Read-only inspection of
the installed MonoMod ILHook.Context.Refresh and Detour runtime pins establishes
the original Method -> active Detour.Target -> pinned runtime handle mapping.
APIShared now uses that mapping, without accepting a generated method by name.
Original startup order/state, repeated checks, lock and one-write bounds remain.

The fresh Unity run at 23:14:48.819 confirmed CORRECTED, full live 75-byte stub,
site RVA 0x3F0C9 and player displacement 0x78. APIShared reached Ready and the
current startup segment had no Error/Fatal. Save/editor/actual trade acceptance
is not inferred from this menu startup.

Fixes and Extender whole-file hashes above now describe observations, not managed
version allowlists. Their full loaded/disk identity and MVID must match; the actual
foreign object, live native entry, full stub and continuation remain decisive.
The native game and private backend contracts stay hash-bound. The installed
MonoMod.RuntimeDetour/Utils hashes are respectively
40E49BB314391CD7BDDC2644F8553EEBA92C194B940836B103DF16955C464E0C and
9D1495F147AC93C4F81F84538C1A326E8F8A6AEFC78D6289D798F3CE1162C5E9.
Tests accept either audited player displacement, including no write when already
corrected, and reject an unregistered generated loader copy. This supersedes the
earlier whole-managed-binary refusal policy without changing native byte safety.

## Fixes 1.29.1 review and bounded startup repair, 2026-10-26

Native SHA-256 remains
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Canonical external source tag v1.29.1/main was verified against GitLab commit
ce1ec8094775b8dd069e41c49bcc8b9a4b781704. The productive source diff from
v1.29.0 changes only the wheat emitter. Installed fixes.dll 1.29.1 was inspected
independently through its actual private emitter; external sources remain unchanged.

Confirmed instruction/data-flow chain: scheduler 0x2AE40 passes EDX player to
controller 0x3F270; market/phase branches select seller 0x3EE10. Its home slot
and frame retain the earlier audited contract. At 0x3EE27 R9 is initially
player*0x583C, but each goods iteration overwrites R9D at 0x3F0E3 and 0x3F1FA.
The backedge 0x3F215 returns to 0x3F0C3 without recomputing R9. The 20-entry
goods/category table at 0x2CEC90 places raw pitch (good 7) immediately before
wheat (good 9). If the loop reaches wheat, R9 holds the pitch stock, not a player
offset. A prior successful sale leaves the loop through 0x29700 instead.

The 1.29.1 emitter moves R9 into RAX, clears RDX and divides by the 0x583C
GamePlayerResources size. DIV leaves quotient in RAX and remainder in RDX;
the flag helper indexes [flags+RDX]. A stock of 100 therefore indexes flag 100,
outside the nine-byte array. This is a confirmed possible invalid index, not a
reproduced game crash. Finally, POP RCX restores the Food category saved before
MOV ECX,Resource; the category fix cannot survive to the reserve branches.
These three findings are independent. Helper disassembly confirms it neither
bounds nor transforms the supplied 64-bit index and preserves only its scratch.

The complete supported prefix is now compared with a newly assembled instruction
contract using actual flags, frame and Extender domain/layout values. Merely
finding one stack load and a matching flag reference is no longer sufficient.
The user authorized the narrow startup exception to cover one existing prefix
replacement, retaining all synchronous-start/engine-lock/provenance checks.
For the observed 94-byte complete stub, only its first 61 bytes change: a 47-byte
stack-load/bounds/flag/category body plus 14 NOPs. The 19 displaced bytes at +61
and 14-byte absolute return at +80 retain their exact positions and bytes.
Legacy wrong/correct stack variants retain one-byte/no-write behavior.

Existing private native fixtures execute the historical and actual installed
emitters after the production correction, including independent R9 stocks/old
stack values, player flags, non-wheat, stack/GPR/XMM state and bounded invalid
indices. Existing memory fixtures cover exact ranges, preimage races, region
crossings, denied protection and restoration/cache/read-back failures. This is
offline execution evidence; fresh Unity startup, repeated saves, editor, actual
AI sales and real host/client acceptance remain pending until separately recorded.
No new game hook, native allocation, simulation callback or config mutation is
introduced. Historical audit sections above retain their original observations.

The final review also tightened the seller-home proof: exact-address-only checks
would miss a wider stack store overlapping the four-byte player value. The full
write width is now checked; indexed stack writes and stack-address LEAs are
refused instead of assuming no alias. A dedicated synthetic-function case in the
existing analysis suite covers partial overlaps and aliases. The current native
seller contains none of these newly refused forms.
