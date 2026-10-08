# APIShared development rules

- APIShared must build and test independently of the SerpsMods workspace.
- Production sources belong under `src`; public contracts and internal implementations
  are separated by responsibility. Do not compile sources outside this repository.
- Do not source-link workspace `Shared` helpers. Do not add mod-only behavior to the
  independently maintained API internals. Consumers use public contracts, not internal sources.
- Internal helpers initialized from workspace sources record their historical provenance.
  They are independently maintained; there is no automatic synchronization.
- JSON uses only the dependency-free parser in `src/ModSettings/Internal`.
  Do not introduce System.Text.Json, Newtonsoft.Json, JsonUtility or other serializers
  into the Unity runtime. External test/tool executables may use their framework JSON library.
- Preserve assembly name `APIShared`, plugin GUID `APIShared_Serp`, registration identities,
  settings/preset/save formats and existing friend-assembly contracts.
- SHCDE destroys early BepInEx components during startup. Long-lived services, delegates,
  hooks and subscriptions must be rooted statically or by persistent publishers.
  Do not rely on plugin Update/LateUpdate/FixedUpdate, coroutines or teardown callbacks.
- Published hooks remain installed until process exit. Roll back only unpublished failed
  initialization candidates. Settings and map transitions use logical activation gates.
- Before runtime changes, audit JSON, lifecycle and hook teardown. Before each build,
  run the machine checks covering these contracts, CRLF and XAML Content roots.
- Compile against the real installed Assembly-CSharp.dll and selected installed SHCDESE.dll.
  A publicized assembly is not evidence of runtime accessibility. Record and validate
  new game-member accesses before building.
- Native behavior changes require a complete feature-specific Vanilla/control-flow audit,
  including the installed detour backend, displaced bytes, incoming edges and register/flag
  preservation. A structural move must not change target addresses or native algorithms.
- Review compatibility with the selected Script Extender and Fixes source/version.
- XAML patches require exactly one direct element inside each Content node.
- Keep text files CRLF. Preserve unrelated user changes and compare the finished changes
  against the recorded Git baseline.
- Build and install only through elevated `build.bat /nopause` after checks pass.
  Keep versions unchanged during testing; coordinated release versions follow acceptance.
- Reference game/Extender assemblies with Private=false; never commit proprietary binaries,
  credentials, generated DLL/PDB outputs or machine-specific configuration.
