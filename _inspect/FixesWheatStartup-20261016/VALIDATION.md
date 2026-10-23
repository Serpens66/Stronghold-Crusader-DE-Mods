# Validation record — Fixes wheat startup correction

2026-10-16. APIShared branch: dev/Serpens66. Workspace branch: develop.
No compilation, build, installation, version change, commit or publication.

## Completed read-only/static checks

- Installed game/Fixes/BepInEx/Extender/RedBird/Iced hashes match the identities
  recorded in APIShared/UpdateToNewDLL.md. Canonical native hash matches CURRENT.json.
- Installed Chainloader and BaseUnityPlugin confirm PluginInfo publication before
  AddComponent, synchronous Awake and Instance assignment after return. Installed
  preloader confirms Application..cctor injection. Installed Fixes Plugin confirms
  immediate LibraryLoaded subscription and ApplyNative path.
- EngineInterface, Director and GameMap static-initializer IL inspected: lock,
  arrays, scalar/null fields and culture setup; no game/map native execution.
  Engine run/load wrappers use threadLock; editor parameter wrappers do not.
- Original seller prolog and displaced bytes read from the canonical native PE.
  The production 75-byte template, with displacement 78, was decoded with the
  available Iced assembly: instruction boundaries 00,01,03,07,09,0D,0F,19,1E,20,
  22,27,29,2A,31,35,3D and return-pointer boundary 43 are valid. Both conditional
  branches target 27. The player load decodes as mov edx,[rsp+78h].
- C# syntax parsing of changed runtime/test sources passed with the PowerShell
  process's loaded Roslyn 5.0 parser. This is not compilation/type checking.
  An initial attempt to load the SDK's separate parser conflicted with the
  already-loaded assembly; the clean repeat used the loaded parser successfully.
- APIShared runtime JSON/lifecycle and plugin/manifest dependency checks passed.
- Fresh Windows PowerShell Test-PermanentHooks.ps1 passed, including the narrow
  Test-FixesWheatBoundary.ps1 check. The workspace's updated permanent-hook checker
  also passed. Only the exact APIShared memory helper is excepted after its
  dedicated startup-boundary validation; other runtime mutation rules remain.
- Fresh Windows PowerShell Test-Documentation.ps1 passed (274 local links/anchors).
- Changed text files normalized to CRLF with ordinal read-back checks; git diff
  whitespace checks passed. Other chat's diagnostic changes were not edited.

## Prepared, not executed

Core.Tests/FixesWheatContractTests.cs tests the production contract/correction
code: one-byte-only write, idempotence, rejection of every corrupted stub byte,
wrong return, denied write, post-write corruption, players 1..8 with arbitrary
saved RDI/flags, and rejected late/missing/reordered startup contexts.

APISharedTests/FixesWheatStartupTests.cs tests actual absent-Fixes initialization
and one-attempt lifetime, the compiled optional dependency through BepInEx's real
topological sorter, and unknown binary refusal. These require compilation later.

Remaining before release: build/type checks and test execution; actual backend
execution/protection/cache-failure fixtures; runtime startup-stack acceptance;
Fixes absent/disabled/enabled/already-correct/unsupported cases; engine-lock
refusal; repeated test_dusty.sav loads past tick 70 and fresh process starts;
editor and cooperating APIShared services after the changed load order.

BugfixesAndQoL requires no runtime adaptation: existing APIShared/Fixes dependency
order and disjoint 0x3F156 stone-reserve hook remain unchanged. A shared workspace
validation-script integration was necessary so the explicitly allowed one-time
correction does not cause future consumer preflight failures.
