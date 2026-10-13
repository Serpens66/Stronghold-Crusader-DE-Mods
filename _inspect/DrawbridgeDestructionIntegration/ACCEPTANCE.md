# Drawbridge destruction fix integration acceptance

BugfixesAndQoL 1.0.187 integration, 2026-10-13. Version stays unchanged until
main-mod acceptance. No publication or APIShared changes are included.

## Evidence retained

- testmod-accepted-editor-run.log: accepted revision-2 editor session from
  BepInEx/logs/Log_315.log, 16:57..16:59. 668 paired calls, 665 protected
  decisions, 64 detailed survivor captures, 3 lethal removals, no errors/pending.
- testmod-ACCEPTANCE.md and testmod-UpdateToNewDLL.md: historical standalone
  instructions, retained as evidence; their paths/config no longer apply.
- testmod-build.log: successful standalone build evidence.
- testmod-last-run.log: later 17:14 empty session, zero damage; separate evidence.
- Native executable tests moved into the main mod and compile its production
  NativeContract.cs; 150 cases cover types, direct-attack flag, signed remaining
  HP, disabled forwarding, register/flag semantics and actual backend span.
- main-build-encoding-failure.log: first attempt identified test-file encoding
  corruption, corrected against the original source before retry.
- main-build-game-running.log: second attempt stopped before installation
  because the game had been started during the checks.

## Pending main-mod in-game acceptance

1. Start with the standalone package removed. Check DRAWBRIDGE_FIX_INSTALLED
   and DRAWBRIDGE_FIX_ACTIVE enabled=True in the new process section.
2. Editor and test_gates_v2.sav: both stone gate sizes survive nonlethal direct
   damage without losing bridges; exactly lethal and overlethal damage remove
   both gate and bridges through regular Vanilla removal.
3. Include no bridge, one bridge and two bridges, lowered and raised states.
4. Check one DRAWBRIDGE_FIX_CONFIRMED with plausible gate/global identity and
   remaining HP. Subsequent hits must not emit repeated damage/summary logs.
5. Toggle the individual host option and EnableMod: disabled reproduces Vanilla,
   reactivation restores protection without native unpatch/repatch. Confirm
   reset=true and save/reload persistence, including an older settings file.
6. Check manual demolition, projectile and area damage. Check joint loading
   with Script Extender/Fixes, then separately multiplayer host/client sync,
   client UI lock and deterministic destruction behavior.

After successful main-mod acceptance, raise the then-current main version
consistently by a patch increment and ask about documenting the feature in
README. Multiplayer approval remains required before production release.

## Completed integration checks

2026-10-13 17:29:59: prescribed elevated main build.bat completed with exit 0.
150 native drawbridge execution cases and the complete host/client/Trail/preset
suite passed, followed by all existing main-mod regressions. Main compilation
has 0 errors; its 27 warnings are outside the new drawbridge feature.
Installed/local DLL SHA256 f40b7607ff0b21e50239efd13ba00dd49f10ea5f97f3bbbee1bbdff948cb1143.
Installed feature type, host setting and assembly version 1.0.187.0 verified.
Standalone test package and Testmods/DrawbridgeDestructionFixTest removed only
after this verification. Regression tests and historical evidence are retained.
APIShared, dependency minima and README files were not changed for this fix.
The in-game main-mod acceptance checklist above remains pending.

## User acceptance and final patch

2026-10-13: user confirms thorough main-mod testing and expected behavior; authorizes version 1.0.188 and concise main README documentation. This supersedes the pending main acceptance above. Multiplayer coverage was not separately specified. Runtime code is unchanged.

Final version 1.0.188 built and installed via elevated build.bat on 2026-10-13 17:41:35 (exit 0). All prescribed regression checks passed; installed AssemblyVersion 1.0.188.0 and source/local/installed manifests are consistent. Installed DLL/manifest hashes match the local package. Remaining 1.0.187 entries are historical changelog records. README fix documentation added as authorized.
