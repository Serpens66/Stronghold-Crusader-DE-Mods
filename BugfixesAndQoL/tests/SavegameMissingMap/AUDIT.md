# Missing original map: save metadata audit

Game native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Managed SHA-256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.

Confirmed Vanilla path: EditorDirector.loadSaveGame clears restart state, calls
EngineInterface.LoadSaveFile -> DLL_LoadSaveGame (RVA 0x853F0), then separately
loads the header with loadRestartInfo=true. MapFileManager dispatches restart
versions 50..99 to HUD_IngameMenu.RestartSkirmishMapInfo.decode. The decoder reads
the embedded AIV/AIC/name/image/setup data, but rejects the completed object if
GetHeaderFromFileNameForSkirmishRestart cannot find the source map. Workshop,
built-in and local catalogs are searched separately. Custom trails additionally
attempt their Maker map. Malformed input is handled by a separate catch return.

test_dusty stores Workshop origin 2 and Tactical Boardgame (2 Player). Both
Mansur and The Leopard have embedded AIC, AIV, name and valid 144x144 PNG data.
The 81,822-byte restart payload round-trips byte for byte. The user confirmed
that installing only the original map restores the portraits/names in Vanilla.

UI consumers getAIFace and getComputerName use the retained restart object.
Absent restart objects produce the generic shield/empty name. No native hook
or native memory write is required by this fix.

The decoder IL transform replaces only the two map-availability rejection values,
not the exception return. Missing custom-trail dictionary entries are availability
failures; other exceptions keep Vanilla behavior. Successful editor/trail decoding
with a null map also records the identity for the encoder. Identity parsing is
bounded and strict UTF-8; no standalone copy of the complete Vanilla codec exists.

Actual selectedHeader remains null. A ConditionalWeakTable attaches the original
name/origin to each decoded object. Only the three selectedHeader reads in encode
use a serialization-only FileHeader. It is never registered as an installed map,
assigned to selectedHeader, loaded or written to disk as a map.

SetAsNormal is private and is targeted by a validated IL hook. RefButtonRestart
is private and accessed through a type-validated FieldInfo. The separate restart confirmation callback is guarded in its custom skirmish
branch. The shared public menu action is never hooked by this mod. APIShared already owns
RestartSkirmishGame interception; this mod does not hook it. FRONT_Multiplayer
doOpen clears selectedMPHeader before populating its actual installed map list.
Returning to a lobby therefore requires selecting an installed map for a new game.

Lifecycle/JSON precheck: no runtime serializer, Unity update host or teardown.
Static ILHook roots survive startup destruction. APIShared's public mission
observer supplies MISSION_STARTED; the persistent Extender tick supplies a single
RUNTIME_TICK marker after each start. Hooks are installed inactive and enabled only
after every installation succeeds. Failed initialization leaves any published
hooks permanently installed but logically inactive.

Explicit build exception authorized by the user: installed APIShared 0.9.2 main
is the reference, not the mutable workspace checkout. ImportDirectoryBuildTargets
is disabled only for these two projects to avoid the workspace's automatic SDK
redirection. The new build driver never builds or copies APIShared. verify.ps1
checks the installed version and the actual game members before compilation.

Desktop regressions transform and execute the actual installed decoder/encoder;
Unity image creation and catalog access are isolated. A clone of the actual setup
parser adapts Unity Mono's String.Split(string, options) to the equivalent desktop
Framework overload; setup parsing itself remains Vanilla. Image bytes are retained
and compared, but these tests do not prove Noesis rendering or gameplay acceptance.
Runtime acceptance: missing-map portrait/name display, save/reload, menu blocking,
lobby return and logs must still be checked in the game after installation.

Build/install acceptance (2026-10-16): build.bat /nopause completed successfully;
source/installed DLL, PDB and manifest hashes match. Shared boundary, dependency,
permanent-hook and actual transformed-code tests passed. Compiler warning MSB3277
reports the installed Mono.Cecil 0.10.4 / dependency-requested 0.11.4 mismatch;
no replacement dependency was installed. APIShared.dll and test_dusty.sav hashes
were unchanged before/after build. In-game acceptance remains pending.

Startup regression (2026-10-16): the first game log reported FIX_DISABLED because
Unity's byte-loaded Assembly-CSharp has an empty Assembly.Location. No hooks were
installed in that run. Validation now reads BepInEx.Paths.ManagedPath instead;
the expected SHA-256 and fail-closed behavior remain enforced. WAITING precedes
event subscription because LibraryLoaded can invoke the handler synchronously.
The shared runtime startup coordinator is tested with immediate, delayed and
duplicate callbacks, and failures before validation/each hook publication.
An actual byte-loaded game assembly reproduces the empty-Location condition in
the desktop test; missing files and mismatched hashes must also be rejected.
These tests do not replace the still-pending post-fix Unity runtime acceptance.

Crash correction (2026-10-16): the previous additional IL hook on
ButtonIngameMenuFunction rebuilt the Extender 2.14.1 owner's IL chain. Its
manipulator installed another exit-Yes hook and replaced a shared trampoline,
causing recursive unload notifications and EXCEPTION_STACK_OVERFLOW. The log
contains 2,494 Lua unload notifications from 21:59:50.560 to 21:59:51.054.
The owner hook is removed from the installation list, not undone at runtime.
A fresh process is required to use the correction.

The replacement target is resolved from the owner's ldarg.0/ldftn/Action delegate
construction and a unique call to FRONT_Multiplayer.RestartSkirmishGame. In the
validated game assembly this private callback is <ButtonIngameMenuFunction>b__24_0.
Its get_SkirmishTrailType < 0 branch enters the custom skirmish block; the guard
runs there before reading restartSkirmishMapInfo. Campaign and built-in trail
branches bypass it. The separate exit callback and APIShared target stay untouched.
No Extender/APIShared binary or source modifications are necessary.

Regression tests now execute the transformed callback with isolated game services,
and exercise real installed MonoMod hooks on an outer method and separate inner
callback. Hooking the inner callback must not rerun the outer manipulator; an
additional outer hook is a positive control. These desktop checks do not prove
in-game exit/lobby behavior. Fresh-process game acceptance is still pending.

ILLabel correction (2026-10-17): the 01:39 run installed the first three hooks,
then disabled the fix while installing the restart callback. ILContext.Invoke
wraps instruction branch operands in ILLabel before calling the manipulator.
The former direct-call tests and preflight skipped this conversion. Switching
the unmodified callback test to Invoke reproduced the same contract exception.

The resolver now accepts ILLabel.Target and direct Instructions only when the
target belongs to the current method. Signed blt and blt.s are accepted; field,
method, callback signature and unique branch contracts remain enforced. The
existing target is preserved, and the added exit branch uses an explicit ILLabel.
RunHookPlan preflights every method on a fresh copy via ILContext.Invoke before
any observer registration or hook publication; errors identify phase and target.
All transformation regressions now run inside Invoke. Negative tests snapshot
inside the callback using label-aware operand formatting. Both branch widths,
invalid/out-of-body targets and failure of the fourth preflight are covered.
The original common-menu hook remains absent. Fresh-process game acceptance
(portraits, save/reload, restart blocking and exit/lobby) is still pending.

Diagnostic isolation (2026-10-17): only logger delegate invocations are caught.
A failed RECOVERED, RESTART_BLOCKED, WAITING, READY or error sink cannot change
save recovery, restart protection or activation. The production plugin writer
uses the same protection for assembly, hook and mission diagnostic messages.
Actual data validation and installation exceptions retain their existing paths.
Recover obtains the per-object identity once, removing the second table lookup.
Throwing-logger regressions cover repeated byte-identical codec roundtrips,
restart blocking, malformed data and successful/failed one-time initialization.
This source preparation does not include a runtime build/install, commit or
main-mod integration. Save/reload and restart with an installed map still require
in-game acceptance before release.
Integration into BugfixesAndQoL / develop (2026-10-17)
====================================================
The earlier sections describe the historical standalone testmod. The active
production sources are now src/SavegameMissingMap in BugfixesAndQoL; this test
project source-links all four production files. The standalone plugin entry,
metadata and installation driver are retired.

Comparison against the current working-tree testmod (including its uncommitted
logger protection): SavegameFix's four IL transformations are byte-for-byte
identical. RuntimeStartup, RestartTests and App.config are identical after only
the namespace substitution. Reviewed functional differences are limited to:
- Namespace BugfixesAndQoL.SavegameMissingMap.
- Existing Runtime.InitializeNetwork / LibraryLoaded calls the static feature
  once; no second plugin or game-library event registration. The tested startup
  adapter is invoked immediately because the owning runtime is already ready.
- New Recover/Remember/TrailLookup operations additionally consult the live
  EnableClientFeatures predicate. Encoder and Missing/BlockRestart retain the
  installed Enabled state and object sidecars independently of that setting.
- Diagnostic notifications replace inline marker formatting. A single optional
  registration contains all APIShared observation and tick subscriptions.
  Failure of registration or logging cannot affect functional activation.
- Old plugin presence skips all feature installation. A soft dependency ensures
  its loading precedes the existing initialization path. The skip is logged once
  by the one-shot feature entry. Published hooks remain statically rooted.

Temporary diagnostics removal after game acceptance:
1. Remove SavegameMissingMapDiagnostics.cs.
2. Remove its Compile entries from the mod and this test project.
3. Remove the single TEMPORARY registration line in SavegameMissingMapFeature.
No codec, branch transformation, object metadata or restart guard changes are
needed. Default diagnostic delegates remain harmless no-ops. Permanent guarded
FIX_DISABLED error reporting remains in the feature.

Verification:
- Actual installed MonoMod Invoke transformations, branch labels/widths, invalid
  targets, preflight before any publication and isolated-callback hooks.
- Executed installed Vanilla codecs and restart callback, portraits/AIC/AIV,
  origins, repeated byte-identical roundtrips, corruption and game-mode branches.
- Empty Assembly.Location, missing DLL, wrong hash, immediate/deferred/repeated
  callbacks, partial-install failures and throwing diagnostic/error loggers.
- New client-setting regression: no new recovery while disabled; existing object
  still encodes byte-identically and blocks restart.
- Legacy-plugin conflict skips installation even with a failing skip logger.
- Scoped SharedBoundaries and dependency metadata checks. Dependency checker is
  run in its own PowerShell process, avoiding inherited Release.Common strict
  mode (optional MaximumVersion fields are intentionally absent).
- Branch installer fixture suite: 32 checks passed. Only two obsolete installer
  list entries were removed; no build/installation behavior otherwise changed.
- MSBuild warns about the installed Cecil 0.10.4/0.11.4 reference mix (MSB3277).
  Tests deliberately execute the installed BepInEx MonoMod/Cecil combination;
  no dependency binaries or redirects were replaced.

No mainmod build, game installation, commit, version or README change.
Installed testmod remains in place; remove it during the later explicitly
requested integrated-mod installation, otherwise SKIPPED is expected.
Fresh-process game acceptance is still open: portraits without the original map,
actual save/reload, restart with the original map installed, repeated exit to
menu/lobby without recursion. No integration game pass is claimed.

Compared original working-tree source SHA256:
src/SavegameFix.cs : F1C5F4A6FFB674421BF9EAC82FBE472A6E5CC719F2C221B332F27CC0DADC3BD0
src/RuntimeStartup.cs : 4A35301D400EF1E81EEBD29B9F45D4BA4E81C4F8F74871B9C1156DCC1CEB1D08
src/Plugin.cs : 109E47B477CFEB5C48AFCFFB41BF038B26533D2562F68E9BD5DC9DBC037EC857
tests/Tests.cs : C8C49550A992B179A14F520D54A5656B05714715B0CEF25E7DC30E76FEBE8293
tests/StartupTests.cs : 2360327E7D1962AA100852C4D01BDD13722CF372F6E24CC294E6C9C577C5E71A
tests/RestartTests.cs : D60E40022CF2DE076F8FB12B7F7FC13E26546DB551681FB4C8BD54CD5197B2A2

Follow-up integration review (2026-10-17, develop at 8431c5c6a plus working changes)
------------------------------------------------------------------------------
Rechecked the existing LibraryLoaded -> InitializeNetwork entry, permanent roots,
soft-dependency ordering, four hook targets, retained object metadata after the
client setting changes, diagnostic exception boundaries and neighboring Trail
features. No additional functional defect was established; production code was
left unchanged.

The current StartupTests uses the shared TestWorkDirectory helper. Reproduced
CS0103 because this test project intentionally disables Directory.Build.targets
(to keep the installed SDK), also excluding its Paths.targets import. Explicitly
imported only Paths.targets; preserved the existing TestWorkDirectory change.
No SDK selection, APIShared source or dependency binary was changed.

Added a test through the real SavegameMissingMapFeature.Initialize entry and
Chainloader.PluginInfos, beyond the existing boolean helper test. It verifies
that presence of the legacy plugin reaches no hook/member initialization, a null
logger cannot break the skip, and another Initialize call does not retry after
the legacy dictionary entry is removed. BepInEx is initialized with fixture-only
config/cache paths before accessing Chainloader; no game configuration is used
as writable test state.

Evidence: review-build.log captures the original compilation failure;
review-build-fixed.log and review-tests.log capture the corrected build and all
behavior tests. Expected isolated Vanilla exceptions originate from deliberate
corrupt-data fixtures. MSB3277 for the installed Cecil reference mix remains.
Mainmod build, installation and integrated in-game acceptance remain unperformed.

The regular verify.ps1 completed successfully after the correction; see
review-verification.log for scoped boundaries, dependency checks and behavior tests.
