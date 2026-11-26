# Unity-Mono LoadFile acceptance probe — source staging

This is a separate pre-mission game test, not a desktop CLR proof. No active source,
project, runner or SDK was changed. Review loadcache-probe.patch and the three
standalone stage files. No binary, installation or game execution was produced.

## Integration

- LoadCacheAsset/LoadCacheAsset.cs + .csproj: pure owned Framework4.8.1 DLL, no
  BepPlugin attribute, Unity/API/game reference, module/type initializer or hooks.
  Only a public Int32 static sentinel and ordinary version attributes.
- Main csproj: ProjectReference with ReferenceOutputAssembly=false and Private=false.
  Its build emits the asset directly beneath the existing testmod plugin package,
  Assets/LoadCache/TrappedUnitRescueTest.LoadCacheAsset.dll. The main runtime never
  references an asset type, so compiler/linker loading cannot pre-satisfy the test.
  Existing Run-Tests recursively copies the whole package; no proprietary DLL or
  separate preloader/plugin is added. No Build.ps1 edit is necessary.
- src/Fixture.AssemblyLoadCache.cs: owned rescue-loadcache scenario. The routing hunk
  executes it BEFORE the ordinary skirmish/prepare steps and then yield-breaks.
  The normal series select/return-menu path remains unchanged. A fresh game process
  is required; repetition in one AppDomain intentionally fails the first-load guard.

## Actual game assertion order

1. Public automation waits for the existing qualified frontend MenuReady predicate,
   with CurrentMission=null. Mono.Runtime and mscorlib are mandatory. No input is
   simulated and no mission is created, reset or saved.
2. Assert the uniquely named asset is not already loaded in the AppDomain. Cecil
   reads its exact own metadata, BCL-only references, absence of plugin/initializers,
   sentinel shape and disk MVID; SHA256 is logged.
3. Call the actual Assembly.LoadFile on the canonical absolute asset path twice.
   Compare object ReferenceEquals, Type ReferenceEquals, both actual MVIDs against
   the disk record, both loaded paths and unchanged file hash.
4. Reflect only the testasset Value field: first-view write must appear through
   second, second-view write through first. Finally restore BOTH original values
   and verify readback. The asset remains loaded until ordinary process exit;
   there is no forced unload or executable patch/teardown.
5. Use the APIShared assembly already actually serving this mod (SafeIlHook's
   declaring assembly), verify its loaded MVID against its actual file, then repeat
   LoadFile on that same file. Require identical Assembly and Type references/MVID
   and file identity. No APIShared method is invoked or static field read/written.
6. Require unchanged pre-mission frontend; emit loadcache-complete. Only the normal
   AutomationRun terminal PASS in the actual hidden game run establishes acceptance.

## Expected evidence and limits

Keep exact game run GUID/LogOutput.log, frontend context, the two identity result
records with actual paths/MVIDs/hashes, sentinel reset=true, own terminal PASS and
normal transactional restoration. If fresh-load or API identity fails, stop this
case, retain those actual facts and do not infer reuse from matching names/MVID
alone. A same-MVID but different Assembly/Type object is a FAIL.

This verifies the actual Unity-Mono LoadFile behavior exercised by the game's
loader, including an already loaded real APIShared. It does not instantiate BQoL,
load a declarative provider, validate arbitrary future module initializers or solve
missing/failed consumer dependencies. The planned provider remains in the existing
plugin root, with metadata-only declaration discovery and loaded-path/MVID checks
against actual selected winners; later owner Awake success remains unknown. The
scanner consumer is not implemented by this probe.

## Current checks

C# parse, project XML and CRLF checks only. Asset source must be compiled and its
actual metadata checked when Root approves the package build; the actual hidden
Unity-Mono case is still pending. No Desktop-.NET PASS is substituted here.
