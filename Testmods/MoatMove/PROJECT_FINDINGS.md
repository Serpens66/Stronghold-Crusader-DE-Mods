# MoatMove comparison copy

Date: 2026-09-13. Test version: 0.1.0.

## Scope and provenance

MoatMove is the unchanged precise friendly/allied moat movement implementation
copied from the current BugfixesAndQoL working tree. SOURCE_PROVENANCE.json records
the SHA-256 of all 22 original files and their copied counterparts. The only
transformations in those files are the namespace/log name and replacement of
BugfixesAndQoLViewModel with MoatMoveOptions. The regression checks both hashes
and an ordinal comparison after exactly those substitutions.

The fixed options enable Exact (persisted value 1), with improved moat filling,
ladder attack correction and independent formation enhancements disabled. There
are no lobby settings, configuration files or runtime JSON dependencies. Shared
partial-class dependencies remain intact, including unreachable RequiredOnly
branches, so this comparison does not introduce an algorithmic refactor.

The copy retains eligibility, owner/alliance rules, cursor checks, ordinary and
attack movement, native queue/patrol tracking, post-combat recovery, Dig/Fill
follow-up movement, formation placement needed for moat traversal, movement cost
profiles, native buffer validation and rollback. It does not copy BugfixesAndQoL's
separate extended Shift-queue feature or any other independently enabled feature.

BugfixesAndQoL/MoatUnitBehaviorReverseEngineering.md is the historical behavioral
reference; its old Extender/default-setting statements are not current installation
requirements. This copy targets installed Script Extender 2.5.0 and RedBird.X64 1.1.0.

The user explicitly waived a repeated complete Vanilla behavior audit only for
this unchanged extraction. The static hook audit below is not a substitute for
the complete feature/data/control-flow audit required before optimization.

## Ownership and compatibility

Plugin GUID: MoatMove_Serp. Assembly: MoatMove.dll. NetworkMode: 1. All participants
in a multiplayer comparison must load the same test build. No APIShared or
BugfixesAndQoL assembly reference is required. APIShared is allowed but coexistence
still needs the specified in-game comparison.

Soft dependencies order the plugin after BugfixesAndQoL_Serp and
EnemyGatePathfindingTest_Serp when present. Either loaded GUID disables MoatMove
before it subscribes for initialization; the guard is repeated before native
hook installation. It never changes or unloads another mod.

The plugin statically roots the logger, fixed options, runtime and diagnostic
subscriptions. Copied constructor/transaction failure cleanup remains unchanged.
No plugin, map or startup path invokes runtime.Dispose. Map callbacks reset only
the existing map-scoped state; process hooks remain installed until process exit.

## Verification

Passed before the runtime build:

- Full source semantic compilation against the installed Extender APIs.
- All 22 copied sources match the declared transformations exactly.
- Actual fixed options/snapshot and conflict-policy tests, including no APIShared
  installation, APIShared present, and each conflicting plugin present.
- 193 production runtime members compiled and exercised by the copied fixtures;
  224,452 unit/plan/buffer/work assertions, 6,480 building-field checks, 18,262
  independent search assertions and 1,469,340 cursor connectivity comparisons.
- The retained fixtures also exercise disabled/fast/ladder/filling branches as
  reference regressions. Their mutable test settings are not compiled into the
  runtime; a separate test verifies the real immutable precise-only configuration.
- NativeContracts.py and Validate-PlacementContracts.py passed against the
  canonical installed DLL and hash-bound semantic database.
- NativeHookInventory.py checked 29 named resolutions, all 30 detour function
  targets, nine observer entries, 34 exact-byte guards, 40 relative-call guards,
  local detour-prefix branches and incoming baseline references to the inline span.
- InstalledRedBirdContract constructs a decode-only candidate over private fixture
  memory, confirms exactly 14 displaced bytes and verifies that memory is unchanged.
- The actual recovery emitter produces a fully decoded 73-byte body with the
  original native store and continuation intact; it is never executed by the test.

The old observer scanner silently matched zero entries because its call signature
was stale. The copied test now recognizes pendingTransaction and the new inventory
requires exactly nine observer entries across recovery and placement sources.

Known source-compilation warnings originate from the installed BepInEx framework
identity and RedBird logging-assembly version references. There were no semantic
compilation errors. Runtime compatibility is not established by compilation alone.

## Reproduce checks

Run from the workspace root:

```powershell
& 'Testmods\MoatMove\tests\Test-Preflight.ps1'
dotnet run --project 'Testmods\MoatMove\tests\MoatMove.Tests.csproj' --no-launch-profile -- .
dotnet run --project 'Testmods\MoatMove\tests\MoatMove.Tests.csproj' --no-launch-profile -- . --standalone-only
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' 'Testmods\MoatMove\tests\NativeContracts.py'
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' 'Testmods\MoatMove\tests\Validate-PlacementContracts.py'
& 'D:\CDesktopLink\Portable\Python\WinPy64\python\python.exe' 'Testmods\MoatMove\tests\NativeHookInventory.py'
```

The managed runner must precede NativeContracts.py because it emits the actual
recovery stub fixture. The pinned historical search-kernel blob used as a test
oracle remains test-only; no fallback implementation is shipped. Tests use the
workspace's existing SDK/framework/Python installations and local Git object.

After checks, build/install only through an elevated direct invocation:

```powershell
& 'Testmods\MoatMove\build.bat' /nopause
```

## Pending in-game acceptance

Use separate game starts and the same map/commands for original precise,
MoatMove alone, and MoatMove plus APIShared. Turn the independent filling, ladder
and formation features off in the reference configuration. Test legal own/allied
crossings, hostile/invalid moat rejection, eligible versus ineligible units,
starts on completed friendly moat, mixed groups, attacks, patrol, normal queues,
post-combat recovery, Dig/Fill follow-ups, save/load and a second map.

Expected MoatMove log markers are runtime published, map-start, post-startup-tick
and map-unload with tickObserved=True. Inspect all movement group initialization
messages as well; a successful ordinary-movement installation does not prove
that every optional attack/cursor/work hook group initialized. Reject a comparison
with callback errors, unexpected disabled groups, buffer violations or rollbacks.

No game run is claimed here. Existing precise lag is expected to remain. After
the user confirms behavioral equivalence, audit the complete relevant Vanilla
pathfinding chain and EnemyGatePathfindingTest's masks before choosing an
optimization. Integration into BugfixesAndQoL follows final optimization acceptance.

## Build and installation result

The runtime compiled successfully. The first installation stopped at the hash
check because the batch-launched PowerShell did not provide Get-FileHash. The
installer now uses .NET SHA-256. A second invocation of the same build driver
completed installation using the unchanged compiled DLL/PDB (their original
write timestamps were preserved by the incremental build).

Final local and installed DLL, PDB and manifest hashes match. Assembly version
is 0.1.0.0; plugin, informational and manifest versions are consistently 0.1.0.
The actual compiled assembly has no APIShared or BugfixesAndQoL dependency.

- DLL SHA-256: `2CBD8D6491B8468F6C593B96376F6A9FBF4CEA16683EBBAE5BAF1A5BA0B30EF4`
- PDB SHA-256: `286491958960E7E8BF78C97DCCD9A3EC9E3D26710285C8CAD844DFDD79084983`
- Manifest SHA-256: `8C6A397C52B83F06B07E8ABA52B3A04CB4D564DAD757F3E92DFCB7394C7B9BFC`

All 22 original-source and copied-source hashes were rechecked after installation.
Only MoatMove and its new preparation/documentation tools were changed by this task;
the pre-existing working-tree changes in other mods were preserved.
