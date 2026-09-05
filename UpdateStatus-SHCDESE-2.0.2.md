# Status: SHCDE Script Extender 2.0.2 migration

This file is the durable handoff log for `UpdatePlan-SHCDESE-2.0.2.md`. The plan table remains the authoritative package status; this file contains the detailed evidence that does not fit in that table.

## P0 - Extender and native baseline

Status: `abgeschlossen`

Date: 2026-09-05

### Repository and build

- The user-updated canonical `shcde-script-extender` repository is clean on `main`.
- `HEAD`, `origin/main`, and `upstream/main` all resolve to `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`.
- `HEAD` is tagged `v2.0.2`; the commit subject is `chore(release): 2.0.2 [skip ci]`.
- `upstream` is `https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender.git`; `origin` remains the personal GitLab fork.
- `shcde-script-extender/build.bat /nopause` completed successfully and installed the package. Native build: 0 warnings, 0 errors. Managed build: 150 upstream compiler warnings, 0 errors. Packaging and installation completed successfully.

### Built and installed output

- The normal local source build produces `SHCDESE.dll` SHA-256 `6B73443305830231C3D90DB253B5A4C8B7968C6FB10B32EE34BFE62C2D77C170`, assembly/file version `1.0.0.0`, product version `1.0.0+6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`. This reproducibility discrepancy is documented separately and is not used as the runtime reference.
- The official GitLab `SHCDESE_2.0.2.zip` release asset was downloaded to `_inspect/SHCDESE-2.0.2-release/SHCDESE.zip`; SHA-256 `A5B638489E5D2C611FD8E54BEC6B9744B16A67F8263F33F8F142E7AEA66B0F04`.
- The official release `SHCDESE.dll` has SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`, assembly/file version `2.0.2.0`, and product version `2.0.2+42190be934efb40dd803163259a778d1516a5089`. Its `info.json` also declares `2.0.2`.
- The accidental local-build installation was preserved at `_inspect/SHCDESE-2.0.2-release/local-build-installation-backup`. The installed plugin directory was replaced with the official release tree.
- Full tree verification compared all 228 official plugin files with all 228 installed plugin files: zero missing files, zero extra files, and zero hash mismatches.
- `RedBird.Abstractions.dll`, `RedBird.Core.dll`, `RedBird.X64.dll`, `RedBird.Backends.NativeX64.dll`, and `Microsoft.Extensions.Logging.Abstractions.dll` are present in the official and installed output.
- `libredbird_thread_patch.so` is present in the official and installed output with SHA-256 `B0321E5621EE1AEE8CB07C5579CB5D21A30FB8CA68B3553450EC968374384BA3`, byte-identical to the tracked `deps` source.

### Native baseline and configuration

- Installed canonical `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- `CURRENT.json` current native hash: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- `sem/FBCB9319/DATABASE_INFO.json` identifies the same current native hash and reports its stored integrity check as `ok`.
- Installed `BepInEx/config/000shcdese.cfg` was migrated byte-preservingly from `MaxGameSpeed = 5000` to `MaxGameSpeed = 1500` and re-read successfully. Post-write file SHA-256: `C2E82EC667D1C3987B0D69B1DD7091A671DD7C79E200F5AC72B688A31162EFEC`.

### Source-build version discrepancy and resolution

The exact, clean official `v2.0.2` source tag declares its checked-in BepInEx/package versions as `1.0.0`:

- `src/SHCDESE.BepInEx/Bootstrap/Plugin.cs`: `PLUGIN_VERSION = "1.0.0"`
- `src/SHCDESE.BepInEx/SHCDESE.csproj`: `Version 1.0.0`, `AssemblyVersion 1.0.0.0`, `FileVersion 1.0.0.0`
- `deps/info.json`: `Version: 1.0.0`
- A normal local build confirms those source declarations.

The official GitLab release artifact is correctly versioned `2.0.2`, so it satisfies the plan-required `[BepInDependency("000shcdese", "2.0.2")]`. The canonical source tree was not modified. The official release artifact, rather than the incorrectly versioned normal source build, is the confirmed compile/runtime reference for subsequent packages.

### Preserved user work

No `MoveMoatTest` file was edited or built. Existing user changes in `MoveMoatTest/src/MoveMoatPathTest.cs`, `_inspect/MoveMoatRegressionTests/Program.cs`, and `_inspect/MoveMoatRegressionTests/RuntimeHarness.cs` were left untouched.

### Remaining note

P0 has no blocking remainder. The source-build reproducibility issue does not affect the official release package but should still be reported upstream. An author-ready report is in `SHCDESE-2.0.2-PluginVersion-Report.md`.

## P1 - Shared settings, Trail, Chore, and Host group

Status: `abgeschlossen`

Date started: 2026-09-05

### P1a - Shared settings workaround and CustomCustomTrail

Status: `abgeschlossen`

Fresh inventory before changes:

- `Shared/PresetLobbyModSettingsViewModel.cs` still contains `ScriptExtenderMultiplayerSyncWorkaround` and calls `EnsureInstalled` during preset registration.
- `CustomCustomTrailPlugin` still uses the old `(IntPtr, ReadOnlySpan<byte>)` LibraryLoaded callback and a flags-only SHCDESE dependency.
- Both authoritative and packaged `CustomCustomTrail/info.json` copies lack an explicit `NetworkMode`.
- `CustomCustomTrail` has no direct Zhuqiaomon or PolyHook source use; its project references are checked separately before removal decisions.
- Existing unrelated user changes in `MoveMoatTest` and `_inspect/MoveMoatRegressionTests` remain outside P1 and untouched.

Completed changes and checks:

- Removed the complete `ScriptExtenderMultiplayerSyncWorkaround` implementation and its `EnsureInstalled` call from the shared preset registration path. Preset, role, Trail-snapshot, host-only, per-player, and local-setting logic remains.
- Added a test-only compile boundary around the real Noesis/XAML registration path. Runtime behavior is unchanged; classic preset tests can now use minimal Noesis value stubs without loading Unity-only ECalls.
- Migrated `CustomCustomTrailPlugin` to `CrusaderLibraryLoadContext` and `[BepInDependency("000shcdese", "2.0.2")]`.
- Added explicit `NetworkMode: 1` to authoritative and packaged CustomCustomTrail manifests. Mod version remains `1.3.46`.
- Updated stale static test expectations from the removed transport workaround to the retained shared per-player convergence contract.
- Made `_inspect/LobbyModSettingsPresetTests` standalone through minimal Noesis stubs and made its incoming-network test open the actual SHCDESE 2.0.2 `_isProcessingNetworkSync` scope. This prevents the test harness from mistaking its own local setter call for an authenticated Extender update.

Test results:

- `CustomCustomTrail.Tests`: 40/40 passed, including the new 2.0.2 migration-contract test.
- `_inspect/LobbyModSettingsPresetTests`: classic MSBuild succeeded and all lobby-settings preset tests passed; eight installed legacy MessagePack files were validated in memory.
- `_inspect/HostClientPresetTests`: classic MSBuild succeeded and the elevated EXE ended with `PASS: 2.0.2 routing, authority, game modes, Trail/client locks, presets, and MessagePack sentinels`.
- P1a negative searches found no obsolete workaround symbol/call, old LibraryLoaded signature, flags-only SHCDESE dependency, Zhuqiaomon, or PolyHook reference in the P1a production scope.
- All changed P1a text files use CRLF. Ten literal `\\r\\n` occurrences in `CustomCustomTrail.Tests/Program.cs` are intentional CRLF fixture data/assertions.

Build and installation:

- `CustomCustomTrail/build.bat /nopause` ran once after all static checks. Tests: 40/40. Runtime compile: 0 warnings, 0 errors. Packaging and installation succeeded.
- Local and installed hashes match: `CustomCustomTrail.dll` `F89E7D52B889CF4F9FD9A3E7613C07AB7A644E6EB746B4C614F508AB989C0540`; `CustomCustomTrail.Core.dll` `9F4E413045F2477FEE8C150A8EC4035D2FFF1FAD25AFBC04FC69B27A260E512F`; `info.json` `2494071B6DBE49F8803ECB9AF49C019E92CC834EB90EA95A0481C3D26707F309`.
- Local and installed package trees contain no Zhuqiaomon/PolyHook file. Installed SHCDESE remains official assembly version `2.0.2.0`, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Runtime remainder: the later package-wide P1 host/client acceptance must still confirm a post-startup-cleanup CustomCustomTrail map/render marker. P1a itself has no source/build remainder.

### P1b - ChoreTestMod retirement, ExtremePowers, and RandomEvents

Status: `abgeschlossen`

Changes:

- Confirmed that `ChoreTestMod` has no active project, build-driver, source, manifest, P1 dependency, installed plugin, or lobby-comparison residue. Historical Markdown references remain documentation only; the deleted diagnostic mod was neither restored nor migrated.
- `ExtremePowers` and `RandomEvents` now declare an exact minimum dependency on SHCDESE `2.0.2`, consume `CrusaderLibraryLoadContext`, and keep their existing runtime-rooting behavior. No startup `OnDestroy` teardown was introduced.
- Removed stale Zhuqiaomon references from both ExtremePowers project files and from `RandomEvents.csproj`. Source, packaged output, installed output, and compiled-binary string checks contain no Zhuqiaomon or PolyHook residue.
- Added `NetworkMode: 1` to authoritative and packaged manifests without changing the tested versions (`ExtremePowers` `0.1.0`, `RandomEvents` `1.0.35`).
- Replaced the removed `ChoreNetworkTransport` callers with mod-local, fail-closed 2.0.2 send guards. Each guard requires a registered packet hook, serializes the unchanged packet once with `GameNetworkAPI.Serialize`, requires nonzero `ChoreManagerVA`, enforces `2 + body.Length <= 1200`, and only then passes the same object to `SendPacketToAllEx2(..., viaChore: true)`. There is no explicit Steam fallback and rejected sends do not reach the caller's mutation callback.
- Migrated the remaining Chore callers: ExtremePowers replacement execution; RandomEvents initialization/retry, event batch, and signpost initialization. RandomEvents retains its one-Chore-per-tick rule. Its initialization retry retains the selected immutable packet object as well as the diagnostic body/hash. Initialization ACKs intentionally remain ordinary control-plane packets.
- Added checked UInt32 calendar conversion for RandomEvents. Month values outside `0..11` and absolute month values above `Int32.MaxValue` disable the map path without wrapping the existing signed save/runtime schema.

Checks and tests:

- `ExtremePowers.ApiTests`: passed, including missing hook, missing manager, serializer/send exceptions, exact total sizes 1199/1200/1201, paused simulation, no mutation on rejection, same-object send, and byte-identical double serialization through `GameNetworkAPI.Serialize`.
- `RandomEventsProtocolTests`: passed with 86 assertions. The same Chore contract matrix and normal/boundary/overflow UInt32 calendar values are covered. The seven CS0649 warnings are expected from deliberately partial test fixtures; the runtime build itself has zero warnings.
- Both project XML files and all four changed manifests parse successfully. Static negative searches for the removed transport, old LibraryLoaded signatures, flags-only hard dependencies, Zhuqiaomon, and PolyHook are clean in P1b source/project scope.
- All changed P1b text files are UTF-8 without BOM, CRLF-only, with zero naked LF and zero accidental literal `\\r\\n` sequences.

Build and installation:

- `ExtremePowers/build.bat /nopause`: succeeded with 0 warnings and 0 errors; package and installation completed. Local/installed SHA-256 values match: `ExtremePowers.dll` `EDF4E25DA6C004B33E06165E9898B4E0761331C3EE0B65AC1E7C03411921B033`, `ExtremePowers.API.dll` `93C105493A6257CB3896E546428C30C62CA2085C508FCA2251A83CBFD868AE3B`, `info.json` `DE113F0619F390765AA986BB0BF5D0C1D58F47A9DB42B3511B1213F2290215CA`.
- `RandomEvents/build.bat /nopause`: succeeded with 0 warnings and 0 errors; provenance, package, and installation completed. Local/installed SHA-256 values match: `RandomEvents.dll` `D7FE51CF9681027D92EB24DE6C66B03C4973BCB3FF2480E46D57AF5DBD9652FF`, `info.json` `4F7819695B187519A125C7721DFCF4992A614B69C40C3A625BECEFACD94ABAEA`.
- Installed manifests report the unchanged versions and `NetworkMode=1`; neither installed mod contains a Zhuqiaomon/PolyHook file. Installed SHCDESE remains the official `2.0.2` assembly, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open for P1c: real host/client and post-startup-cleanup runtime evidence is package-wide acceptance work and remains assigned to P1c.

### P1c - SerpsModsHost, StartConditions, and package-wide acceptance

Status: `abgeschlossen`

Source and contract changes:

- `SerpsModsHost` now declares the exact SHCDESE `2.0.2` dependency, consumes `CrusaderLibraryLoadContext`, and no longer references Zhuqiaomon. After every `RegisterAssetMod`, it obtains the authoritative first registration through `TryGetRegisteredDirectory` and validates that directory against the expected child path before incrementing `registeredCount`; missing GUIDs and conflicting paths fail through `H004`.
- Added `RegisteredAssetDirectoryPolicy` so same-path, case-varied-path, missing-GUID, conflicting-path, and later-child/first-registration ownership cases are deterministic and independently testable.
- Both authoritative and packaged SerpsModsHost manifests now require Script Extender `2.0.2`; `NetworkMode` remains explicitly `1`, and mod version remains `1.0.7`.
- `StartConditions` now declares the exact SHCDESE `2.0.2` dependency, consumes `CrusaderLibraryLoadContext`, and no longer references Zhuqiaomon. Start gold remains signed from settings through `GamePlayerManagerAPI.SetPlayerGold(int, int)`; `StartGoldPolicy` admits only the sentinel `-1` or `0..1,000,000` and rejects all other negative/overflowing values before the game API call.
- The packaged StartConditions manifest now explicitly uses `NetworkMode: 1`; mod version remains `1.0.21`.
- The shared preset registration test boundary still executes the real registration/result checks. Under `SHARED_PRESET_TESTS` it reads the registered view reflectively so the classic test harness does not need to load the runtime-only `Noesis.NoesisGUI` assembly. Runtime builds keep the direct typed `registration.View` path.
- Runtime lifecycle review found no Plugin `OnDestroy` teardown in SerpsModsHost or StartConditions. SerpsModsHost child services remain rooted by `LobbyLifecycle`/`Application.onBeforeRender`; StartConditions remains rooted by its settings/mode events and, while enabled, its R3 map subscriptions.

Independent 2.0.2 verification:

- Canonical source confirms `GamePlayerManagerAPI.SetPlayerGold(int playerId, int gold)` and `GameAssetModManager.TryGetRegisteredDirectory(string guid, out string directory)` with the documented authoritative registered-directory semantics.
- P1 production source/project/build scope contains no Zhuqiaomon, PolyHook, `ChoreNetworkTransport`, obsolete settings workaround, old `(IntPtr, ReadOnlySpan<byte>)` LibraryLoaded handler, or flags-only SHCDESE hard dependency. The only remaining Zhuq/workaround text in this package is in explicit negative test assertions. The four flags-only dependency matches are optional `SerpsMods_Serp` soft dependencies, not SHCDESE dependencies.
- All inspected project XML and manifests parse. All P1 manifests are explicit gameplay mode `NetworkMode=1`, with unchanged mod versions.
- All 15 changed P1c text files were normalized through an exact-path .NET operation and ordinally re-read. They contain CRLF only, zero naked LF, and zero accidental literal `\\r\\n` sequences.

Tests after the final shared-source change:

- `CustomCustomTrail.Tests`: 40/40 passed.
- `_inspect/LobbyModSettingsPresetTests`: build 0 warnings/0 errors; eight installed legacy MessagePack files validated; all tests passed.
- `_inspect/HostClientPresetTests`: build 0 warnings/0 errors; `PASS: 2.0.2 routing, authority, game modes, Trail/client locks, presets, and MessagePack sentinels`. This includes signed StartGold boundaries.
- `ExtremePowers.ApiTests`: build 0 warnings/0 errors; all tests passed.
- `_inspect/RandomEventsProtocolTests`: build 0 warnings/0 errors; 86 assertions passed.
- `_inspect/SerpsModsHostDuplicateTests`: build 0 warnings/0 errors; host diagnostics, GUID/path cases, mod-hash comparison, and deterministic serialization passed.
- The classic file-persistence suites were run elevated because the workspace sandbox denies the metadata operation behind `File.Replace`; the same denial reproduced in two unrelated existing atomic-write paths. No runtime fallback was retained for this infrastructure-only restriction.

Build and installation:

- `SerpsModsHost/build.bat /nopause`: succeeded after all checks with 0 warnings and 0 errors; package and installation completed. Local/installed SHA-256 values match: `SerpsModsHost.dll` `B73D83C6739283101D6B80CCC3B1C5AEC88A058CA89DED51A44B650DF2FBBC6E`; `info.json` `1170D00DAD18120B6500DC059B5FBB4C1ACD3BBFABECAC996CBDECE4A09B65AE`.
- `StartConditions/build.bat /nopause`: succeeded after all checks with 0 warnings and 0 errors; package, provenance, and installation completed. Local/installed SHA-256 values match: `StartConditions.dll` `3D1C7F859C90C154FF9D1DAB1C37A4A67D3A3D1076704AF2821D5195B4B114F5`; `info.json` `0B878BCB68D3B60FF0E2E0E4191C41F60DCC483B4D5F1D8BAF645EC14FAF72F8`.
- Installed SerpsModsHost/StartConditions trees contain no Zhuqiaomon/PolyHook file. Installed SHCDESE remains official assembly `2.0.2.0`, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Optional package-wide runtime follow-up:

- No post-build real Host/Client session exists yet. The only host log starts at line 1 and was last written `2026-09-05 01:35:59.987 +02:00`; the newly installed SerpsModsHost and StartConditions DLLs were written at `02:41:29.418` and `02:41:38.708`. That old session used the former 1.0.0 placeholder artifact and logged the now-removed temporary settings workaround, so it is invalid as 2.0.2 evidence.
- The client log at `\\LENOVO_SERP\Stronghold Crusader Definitive Edition\BepInEx\LogOutput.log` is currently unreachable. Per repository rules, this establishes only client unavailability, not a bad path.
- By explicit user authorization on 2026-09-05, genuine game, Host/Client, and Proton sessions are optional follow-up evidence and no longer block package or plan progress. Static checks, automated tests, builds, installation checks, and artifact verification remain mandatory.
- If performed later, install the same current P1 packages and official SHCDESE 2.0.2 on the client, run a genuine Host/Client lobby and map session covering settings synchronization, a late join/leave/roster update, at least one accepted Chore action, and a CustomCustomTrail map/render path. Evidence should include post-startup-cleanup map/tick/render markers and no old workaround, sender/authority, registration, or Chore errors.
- P1 is complete on its mandatory evidence and P2 has begun. No MoveMoatTest source, artifact, test, or version was touched or built.

## P2 - Projects without direct Zhuq source usage

Status: `abgeschlossen`

Date started: 2026-09-05

### P2a - BuildingCosts, BuildingLimit, CheatMod, UnitCosts, and UnitLimit

Status: `abgeschlossen`

Fresh inventory and decisions:

- All five plugins still used the old flags-only SHCDESE hard dependency and the old `CrusaderLibrary` LibraryLoaded callback. Their project files still referenced Zhuqiaomon even though production source did not use it.
- MonoMod.RuntimeDetour remains required by the existing managed hook implementations and was therefore retained. No PolyHook source or package dependency was introduced.
- The current BuildingLimit source no longer contains the historical `OnTogglePause` `BuildingId + 1` workaround. Canonical SHCDESE 2.0.2 source was checked independently and now produces the event ID with `GetIndexByAddress(...) + 1`; no mod-side ID correction was necessary.
- Existing MoveMoatTest and MoveMoat regression-test user changes stayed outside the slice and were not touched or built.

Changes:

- Migrated all five plugins to `[BepInDependency(ScriptExtenderGuid, "2.0.2")]` and `OnCrusaderLibraryLoaded(CrusaderLibraryLoadContext context)`.
- Removed the unused Zhuqiaomon assembly reference from all five project files.
- Added explicit gameplay classification `NetworkMode: 1` to every authoritative/packaged manifest in scope. Versions remain unchanged: BuildingCosts `1.0.100`, BuildingLimit `1.0.18`, CheatMod `1.0.5`, UnitCosts `1.0.22`, UnitLimit `1.0.92`.
- Extended `_inspect/HostClientPresetTests` with a P2a migration-contract test covering exact dependencies, callback signatures, removed Zhuqiaomon references, manifest classification/version preservation, and absence of the obsolete BuildingLimit ID workaround.

Checks and tests:

- `_inspect/HostClientPresetTests`: build succeeded with 0 warnings and 0 errors; execution ended with `PASS: 2.0.2 routing, authority, game modes, Trail/client locks, presets, and MessagePack sentinels`.
- Static production-scope checks found exactly five 2.0.2 dependencies and five LoadContext callbacks, with zero old signatures, flags-only SHCDESE dependencies, Zhuqiaomon references, or plugin `OnDestroy` methods.
- All five project files parse as XML. All six manifests parse as JSON, use `NetworkMode=1`, and retain their expected versions.
- All 17 changed P2a text files were normalized by exact path and ordinally re-read as UTF-8 without BOM and CRLF-only, with zero naked LF and zero accidental literal `\\r\\n` sequences.
- One read-only Extender-location check initially failed because the guessed `BepInEx/plugins/SHCDESE` directory does not exist; discovery found the actual canonical installation at `BepInEx/plugins/000shcdese`. A subsequent read-only discovery command first hit PowerShell's prohibited direct `foreach { } |` parser form before execution; it was corrected to collect rows before piping. Neither failed command changed repository or installation state.

Build and installation:

- Each mod's `build.bat /nopause` ran once after the automated/static gates. BuildingCosts, BuildingLimit, CheatMod, UnitCosts, and UnitLimit all built successfully with 0 warnings and 0 errors and completed package, provenance, and game-installation steps. UnitCosts completion after truncated console capture was confirmed from its newly written `2026-09-05T03:24:52.3966595Z` provenance record and matching artifacts; it was not rebuilt unnecessarily.
- Package/installed SHA-256 values match for every DLL and `info.json`: BuildingCosts `2A94DDE5143E1EFC64ACC9E9F8DA6D0C06E294DE4DC5BF31C647F93A7253C476` / `3903B1F8CF0186089A5C8D74321177982AC4AF5D9C8228B96FF40A24519D541F`; BuildingLimit `D2A8F9FF8469EBA295FC8EB997B8DEBE7ED31BC7CED881CDF69014B6608DAD11` / `D4A86DEA8AAF5F0D17381FFE1C4585E5ACD1FC2BF240CABCCEAD022EBE0E9043`; CheatMod `6B058D15AB38F302B6B9CF82D53387AA95828385396F83B8E6EAAC4BB85FDA11` / `238BE1D9548FD91301141F393C3F71A1B41D78CD339EDC85D32682D5CEEA8044`; UnitCosts `29820DB8002FCAB8D7DCED872F18331B44867FABA3AF2CA78CFB0E278A933959` / `F294FC770DEDD724BD2BD9742AF191536D84ABB2783AFAF7478A231729D52B3F`; UnitLimit `B8F22BE08A0E9184542F9252DD0DD23954CC4F3DAFDFAC610428B78A97D0E64B` / `FBA40423E4BEEC9502EAD975FB2E68A0F89CC5FAD4E296903346E02BBCE1E89D`.
- Local package and installed trees contain zero Zhuqiaomon/PolyHook files. The installed Extender at `BepInEx/plugins/000shcdese/SHCDESE.dll` remains the official 2.0.2 artifact: 1,408,000 bytes, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and handoff:

- No mandatory P2a work remains. A real game/Host-Client session is optional follow-up evidence under the plan-wide authorization and does not block P2b.
- P2b must start from a fresh inventory of AIDefense, APITest, CustomLordUpload, EngineerSiegeFix, and VanillaAICExporter. In particular, re-evaluate every AIDefense `UnassignUnit` call against SHCDESE 2.0.2 and keep the migration fail-closed.

### P2b - AIDefense, APITest, CustomLordUpload, EngineerSiegeFix, and VanillaAICExporter

Status: `abgeschlossen`

Fresh inventory and independent contract verification:

- Canonical SHCDESE tag v2.0.2 still calls `BulkTribeDetours.c_game_tribe_remove_unit_hook_impl(_tribeManager, tribeId, unitId)` from `UnassignUnit(tribeId, unitId)`, while the detour/native delegate is `(manager, unitId, tribeId)`. All five AIDefense public-wrapper call sites therefore required replacement.
- The freshly hashed installed `CrusaderDE.dll` matches `CURRENT.json`: SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. The semantic baseline confirms RVA `0x123EA0`, size 312, two callees, and a confirmed old/new normalized-hash/CFG version match. Exact signature, unique occurrence, tail/RET, and following `0xCC` boundary were independently checked before use.
- APITest has no LibraryLoaded callback and retains its required SerpNativeAPI dependency. CustomLordUpload has no LibraryLoaded callback and is a local upload UI. EngineerSiegeFix and VanillaAICExporter used old callbacks and stale Zhuqiaomon project references; their public 2.0.2 `NativePointer`/`SimpleNativeArray` signatures instead require `RedBird.Core.dll` at compile time.
- Existing CustomLordUpload and EngineerSiegeFix Fachtests were rediscovered and used. MoveMoatTest and its regression work stayed out of scope and were not touched or built.

Source and metadata changes:

- All five plugins now declare exact `[BepInDependency(ScriptExtenderGuid, "2.0.2")]`. AIDefense, EngineerSiegeFix, and VanillaAICExporter now consume `CrusaderLibraryLoadContext`; the latter two obtain memory/module base from `context.Memory` and `context.ModuleHandle`.
- Added standalone `AIDefenseTribeUnassignAdapter`. It resolves the unique exact native signature at RVA `0x123EA0`, validates the 312-byte function tail and end boundary, creates a delegate with `(manager, unitId, tribeId)`, validates both one-based IDs and current membership before every call, rejects a null manager or native exception, and confirms membership changed afterward. Native validation completes before the AIDefense runtime subscribes or activates.
- Replaced all five AIDefense calls to the broken public wrapper. The existing failed-private-tribe cleanup remains ordered as native unassign, tribe deletion, and tracking reset; unresolved/incompatible native state prevents runtime activation entirely.
- AIDefense now compiles the shared `NativePatternResolver` and references `RedBird.Core`. EngineerSiegeFix and VanillaAICExporter replaced Zhuqiaomon with the 2.0.2 `RedBird.Core` public-type reference and explicit file validation. No RedBird DLL is copied into individual mod packages.
- Added explicit modes without changing versions: AIDefense `1.2.7` mode 1; APITest `0.1.0` mode 1; CustomLordUpload `1.0.1` mode 0; EngineerSiegeFix `0.1.0` mode 1; VanillaAICExporter `0.1.2` mode 0.
- Updated the durable `AGENTS.md` UnassignUnit rule after AIDefense acceptance: the defect is now recorded for 1.42.0 and exact 2.0.2; QueueTest and AIDefense must remain standalone and use their independently validated native adapters until a future tag is explicitly proven fixed.

Checks and tests:

- New `_inspect/AIDefenseUnassignTests`: 27 assertions passed. Coverage includes canonical native hash, RVA/size, exact and unique signature, exact tail/RET/end boundary, native argument order, unchanged one-based IDs, first/last/invalid boundaries, all five call sites, pre/post/error checks, activation ordering, and the failed-private-tribe rollback sequence.
- `_inspect/CustomLordUploadTests`: all 15 named groups passed, covering package/retry, conflict rollback, dynamic and version-specific rules, exact-tag and confirmation workflows, Vanilla bypass/staging reset, metadata, WAV and path validation.
- `_inspect/EngineerSiegeFixTests`: 773 assertions passed after replacing its stale assertion that Zhuqiaomon owns `NativePointer` with the actual 2.0.2 `RedBird.Core` ownership and a negative Zhuqiaomon assertion.
- Static gate: five project XML files valid; eight manifests valid with expected unchanged versions/modes; exactly five 2.0.2 dependencies and three LoadContext callbacks; zero old callbacks, flags-only SHCDESE dependencies, broken AIDefense `UnassignUnit` calls, Zhuqiaomon/PolyHook production references, or packaged Zhuqiaomon/PolyHook files.
- Lifecycle review: AIDefense teardown remains guarded by genuine `OnApplicationQuit`; its startup `OnDestroy` preserves runtime/subscriptions. CustomLordUpload and VanillaAICExporter `OnDestroy` methods only log and retain statically rooted state. EngineerSiegeFix remains rooted by static state and the LibraryLoaded subscription; APITest by static API registrations/state.
- The first sandboxed AIDefense test attempt was blocked before compile by NU1900 because NuGet vulnerability metadata was unreachable; the identical elevated run passed. This was infrastructure-only.

Build and installation:

- AIDefense's first build failed before installation with two compiler errors: missing `RedBird.Core` for the public `NativePointer` return type and definite assignment of the diagnostic `unit` pointer. Both were corrected, all AIDefense checks reran, and the permitted second build succeeded with 0 warnings/0 errors.
- APITest built and installed once with 0 warnings/0 errors.
- CustomLordUpload built and installed once with 0 errors and 13 pre-existing nullable warnings from linked shared `SerpLocalization`, `DebugLogHelper`, and `DependencyFreeJson`; its full Fachtest suite passed and no P2b source introduced the warnings.
- EngineerSiegeFix built and installed once with 0 warnings/0 errors.
- VanillaAICExporter's first build failed before installation because `SimpleNativeArray<T>` requires `RedBird.Core`. After the reference/existence check was added and XML/CRLF/negative checks reran, the permitted second build succeeded with 0 warnings/0 errors.
- Package/installed SHA-256 values match for every DLL and `info.json`: AIDefense `270B14267E8E0950985BF284D0AF856090403B3E5589D5F41A5B058CDFDDC125` / `7CE78D3CD578CA87EE45AA4513B7930E16B2F532740E97AA6D7C23B7B5FB5D7B`; APITest `21B6F813122F1F05472899EB30E1CEB625C6265780778AEE81E95BA05D9828AB` / `073CEF3D3E6322B8D3AD51F5B3457995AD1B368C40E140492348A35EFBE0A2ED`; CustomLordUpload `771D6BB77C02F9AC5BE576A774BDF5A832811412D70145E11BFB44228283AF87` / `707B49A027FC91E92614D494313DAE29F2681C708EC166BAA6C47613B83653EF`; EngineerSiegeFix `4AC2EEEF13EB597735B3BC3C1A743D32CCF3AC188F38525EAE15E410BE778A75` / `959BD48F20387628F1E3E2C3FD86AFEB8FF1F349D4310EB75D9EE4D2EF913696`; VanillaAICExporter `81FDE3C1F7EDE1272BFB5D3F3FC73A38D80F9B42E97FBD51C02297EB61892C8E` / `2A2BFB55837DCE7AA6D5C353F715E1AE5671461D7CBC725E961D77F6997DB4B1`.
- All local and installed P2b package trees contain zero Zhuqiaomon/PolyHook files. Installed SHCDESE remains the official 1,408,000-byte 2.0.2 artifact, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and handoff:

- No mandatory P2b work remains. Real AIDefense gameplay, APITest API, local Custom Lord upload, Engineer handoff, and Vanilla export sessions are optional follow-up evidence under the plan-wide authorization.
- P2c must freshly inspect SerpNativeAPI, TrailEditor.Core/CLI/tests, the authoritative TestMod LUA manifest, and the retired MoatFillTargetTest/MPTest footprints. APITest was compiled against the current SerpNativeAPI package; P2c remains responsible for migrating and fully testing/building that provider before P2 closes.

### P2c - SerpNativeAPI, TrailEditor chain, TestMod LUA, and retired diagnostics

Status: `abgeschlossen`

Inventory and decisions:

- SerpNativeAPI had one old LibraryLoaded callback, a flags-only SHCDESE dependency, and two manifests without explicit `NetworkMode`. It has no Zhuqiaomon/PolyHook dependency and its selected-command capability consumes the unchanged `OnTribeIssueOrderWithTarget` event rather than `GetSelectedChimps`; no SelectedUnitInfo migration was required.
- TrailEditor.Core, CLI, and Tests are an indirect .NET 10 chain. Core links the platform-neutral AIV/AIC codec sources from the exact local v2.0.2 Extender and MapParser.Core. All dependencies and real test trails exist; no concrete 2.0.2 source incompatibility was found, so TrailEditor source was not changed.
- `TestMod LUA/BepInEx/plugins/TestModSerp/info.json` is the sole authoritative package manifest and has no project/build/install driver. It was classified in place; no manual installation copy was invented. The test mod is currently not installed.
- `MoatFillTargetTest` and `MPTest` roots do not exist, Git tracks no files under either path, active nonhistorical source/project/build/config searches found no references, and the game installation contains no matching plugin directory or binary. Historical Markdown, baseline JSON/JSONL, and preserved evidence logs remain documentation only and were not removed.

Changes:

- SerpNativeAPI now declares `[BepInDependency(ScriptExtenderGuid, "2.0.2")]`, receives `CrusaderLibraryLoadContext`, and passes `context.ModuleHandle`/`context.Memory` into its existing process runtime. No Context/Region disposal or startup teardown was introduced.
- Both SerpNativeAPI manifests now retain version `0.1.0` and explicitly declare gameplay `NetworkMode: 1`.
- TestMod LUA retains version `0.1.0` and now explicitly declares `NetworkMode: 1`. Its previously mixed newline at the Website entry was normalized with the rest of the file.
- Extended `_inspect/SerpNativeAPITests` with exact dependency, LoadContext, module/memory forwarding, negative obsolete-dependency/callback, version, and manifest-mode assertions.

Checks and tests:

- `_inspect/SerpNativeAPITests` compiled the provider and passed the full baseline-hardened suite: public surface, PE and fixed-catalog validation, independent capabilities, ownership conflicts, centered-distance semantics, transactional gatehouse patching/rounding/rollback/page cleanup, selected-command broker/event service, and the new 2.0.2 migration assertions.
- The first compile of the extended test program failed only because its new file checks lacked `using System.IO`; production SerpNativeAPI had already compiled. After adding the namespace and rechecking CRLF, the full suite passed.
- `TrailEditor/build.bat` built MapParser.Core plus TrailEditor.Core/CLI/Tests successfully and then passed all 9 tests: signed/hidden setup roundtrips and validation, byte-identical real trail/container/bundle roundtrips, game-created Custom Lord data, AIV/AIC bundle data, config-v1 defaults, unknown-version rejection, and bundle path-escape rejection.
- TrailEditor restore/build emitted two NU1900 warnings because the configured vulnerability-data endpoint refused the connection at `127.0.0.1:9`; compilation completed with 0 errors and all tests passed. This is infrastructure-only.
- P2-wide static gate: exactly 11 exact 2.0.2 dependencies; zero old dependencies, old callbacks, Zhuqiaomon/PolyHook production references, or broken AIDefense public-wrapper calls. All 17 P2 source/package manifest copies, including TestMod LUA, have the expected explicit modes.
- All changed P2c text files were exact-path normalized, ordinally re-read as UTF-8 without BOM and CRLF-only, with zero naked LF and zero accidental literal `\\r\\n` sequences.

Build and artifacts:

- `SerpNativeAPI/build.bat /nopause` ran once after the Fachtest/static gates and succeeded with 0 warnings/0 errors, including package and game installation.
- SerpNativeAPI package/installed hashes match: `SerpNativeAPI.dll` `B2FA9E7FEF07AA83A7001564DDC28C8AAD79DB76B3A44616BC3285547BACE835`; PDB `9E724B28FCEC1A76E99CD75C5A3460CB177D28A74765BA056364E6815FD7BA06`; XML docs `3E6A82523347972AAE6670085A3D6D0EF6B1E3FAE5AC06D3047B3617B972C522`; `info.json` `5A733429AA007713490687A6F4087B63651DA937778E1D4BE5D66FFF6E11369D`. Local and installed provider trees contain no Zhuqiaomon/PolyHook file.
- TrailEditor outputs: Core DLL `23487A7E21A68FE4EEA8E11A352C449EE0F5097B532E3F0A37E1659E0E60F7CA`; CLI EXE `2A44D6EB727163EE7D921F5DB77F8D6DFEA66C874D7D96194D1592B3C772E8AE`; test DLL `ACDEB0CC5837D866C7BED01953920C596644178BE0E343E78286951A9E0CAB12`.
- Installed SHCDESE remains the official 2.0.2 artifact, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and package handoff:

- No mandatory P2c or P2 work remains. A genuine SerpNativeAPI/APITest game session is optional follow-up evidence and does not block progress.
- The durable MoatFillTargetTest-to-BugfixesAndQoL ownership/lifecycle wording remains assigned to P6, where the replacement implementation and standalone Moat hooks are actually tested; P2c establishes only the retired standalone-mod footprint.
- P2 is complete. P3a is next. MoveMoatTest remains explicitly deferred as P3M and must not be edited or built.

### P3a - ActiveAIVDetector, EnemyGatePathfindingTest, and HunterQueryTargetDiagnostic

Status: `abgeschlossen`

Fresh-session inventory:

- ActiveAIVDetector contains seven AIV-oracle managed-function detours plus the central prepare-layout detour, all still on Zhuqiaomon, an old LibraryLoaded callback, a flags-only SHCDESE dependency, and stale Zhuqiaomon/PolyHook project references. It is a local diagnostic and remains assigned `NetworkMode=0`.
- EnemyGatePathfindingTest contains two runtime inline hooks plus one cursor inline hook, an old LibraryLoaded callback, stale Zhuqiaomon/PolyHook references, and one `GetSelectedChimps()` call still typed as `int[]`. Its existing PolicyTests are the mandatory Gatehouse/ID regression path; the runtime remains gameplay-relevant with `NetworkMode=1`.
- HunterQueryTargetDiagnostic contains one passive diagnostic inline hook, an old LibraryLoaded callback, a flags-only SHCDESE dependency, and stale Zhuqiaomon references. It remains a local diagnostic with `NetworkMode=0`.
- MoveMoatTest remains outside P3a/P3 and was neither read for implementation nor changed or built. Manual game sessions remain optional and nonblocking; source review, native/static validation, automated tests, builds, installation, and artifact checks remain mandatory.

Changes and contracts:

- All three plugins now declare the exact SHCDESE 2.0.2 minimum dependency and consume `CrusaderLibraryLoadContext`. Every native transaction uses the Extender-owned `context.Region` without disposing it and the initialized Extender LoggerFactory.
- ActiveAIVDetector migrated all eight managed-function detours to `DetourHandle<T>`, explicit `HookTarget.FromAddress(...)`, checked `CommitResult.IsCompleteSuccess`, checked every required handle, and calls Vanilla through `DetourHandle.Original`. Its optional ExecuteBuildStep detour remains absent unless the diagnostic option is enabled.
- EnemyGatePathfindingTest migrated its two captured-player comparison hooks and cursor PCL hook to `HookHandle<X64InlineHook>`. Every hook retains its exact register mask, hook size, callback error mode, and overwritten-instruction placement through explicit `ContextHookOptions`. Its audit markers now identify Extender 2.0.2 commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`.
- EnemyGatePathfindingTest now consumes `SelectedUnitInfo[]` and projects only each element's one-based `UnitId`, preserving count, selection order, signature construction, and ownership lookup behavior.
- HunterQueryTargetDiagnostic migrated its passive state-7 writer observer to a RedBird inline handle with the original `Volatile | R13` register mask and `AfterCallback` placement. Native hook activation now requires the canonical DLL hash rather than accepting an unaudited future binary.
- All four transactions use `RollbackAndThrow`, validate the aggregate commit and individual handles, declare `OwnsHooks=false`, remain rooted by their process-lifetime runtime objects, and are never torn down from startup `OnDestroy`. ActiveAIVDetector's sole `OnDestroy` remains logging-only; the other two plugins have no startup teardown.
- Project references now use only the required centrally supplied RedBird Abstractions/Core/X64 and logging assemblies with `Private=false`; stale Zhuqiaomon, PolyHook, and unused Iced references/checks were removed. No RedBird assembly is packaged by a mod.
- Versions were deliberately unchanged. Network modes are explicit in every authoritative/package manifest: ActiveAIVDetector `0.9.7` mode 0; EnemyGatePathfindingTest `0.1.0` mode 1; HunterQueryTargetDiagnostic `1.4.4` mode 0.

Checks and tests:

- Installed `CrusaderDE.dll` and `CURRENT.json` both resolve to SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. A first read-only comparison used the obsolete guessed property name `binaryHash`; inspection corrected it to `currentNativeHash`. No state was changed by the failed comparison.
- New `_inspect/P3aMigrationTests` passed 60 assertions. It checks exact dependency/LoadContext/RedBird handle and transaction contracts, process-lifetime ownership, SelectedUnitInfo projection, manifest modes, audited Extender identity, and maps the canonical PE into virtual RVA layout. All 16 patterns used by these runtimes are unique and resolve to their audited RVAs, including twelve installed hook targets and four supporting global/audit scans.
- The first P3a native test represented the building-validator interior match `0x7B078` as the expected hook target; it correctly found the actual target `0x7B060` after the existing `-0x18` offset. Only the test expectation was corrected, then the complete suite passed.
- EnemyGatePathfindingTest PolicyTests passed 206 assertions, including Gatehouse ownership/ID behavior, native spans, caller classification, atomic dual-filter registration, explicit RedBird ownership/options/commit checks, fail-open behavior, route decoding, and absence of unsafe global mutation or a whole-PCL detour. An initial manual invocation from the workspace root could not find its relative `src` path; rerunning from the mod root exposed two stale `ref handle` assertions, which were migrated to the RedBird registration syntax before the successful run. A later newline-normalization command also prefixed the already-selected mod workdir twice; it emitted nonterminating path errors but the following tests still passed, and the intended file was then normalized and verified separately.
- Static gate: five project XML files and five manifests parse successfully; central 2.0.2 assemblies exist; exact dependency/callback counts match; no Zhuqiaomon, PolyHook, HookRef, old callback, flags-only SHCDESE dependency, packaged central assembly, or Context/Region disposal remains in the production scope.
- All 23 changed P3a text paths, including `PROJECT_FINDINGS.md` and both test sources, were exact-path normalized and ordinally re-read as UTF-8 without BOM and CRLF-only. Three intentional `writer.NewLine = "\\r\\n"` C# string literals in ActiveAIVDetector remain; no accidental literal newline escape was introduced.

Build, installation, and artifacts:

- `ActiveAIVDetector/build.bat /nopause`, `EnemyGatePathfindingTest/build.bat /nopause`, and `HunterQueryTargetDiagnostic/build.bat /nopause` each ran once after all source/test/static gates. All three runtime projects built and installed with 0 warnings and 0 errors; EnemyGate's driver also reran all 206 PolicyTests successfully.
- Package/installed SHA-256 values match: ActiveAIVDetector DLL `1B4EC6DA316EE1DBFA8313D75AA4FB962DD3374E29083C342539F950035BF607`, manifest `C4F950E54A4A4B881ADBCFE326E5CC9C8D3D79FD8EE291F9B2E73B628CC1EC4C`; EnemyGatePathfindingTest DLL `69B5AE25AF9BDEF3B39FB3FD88799159CCBE94210CA1DEE339DAD3EE34F7171F`, manifest `66D362E22F34DB1FF1C71EC01AD97A95BF0EA5EDC23BFD71D4600254C412CAAF`; HunterQueryTargetDiagnostic DLL `47CABD38A7DE2827935568EA925A24890D5CE1CC84DD988A5D2CB2359A940257`, manifest `0A571FA9DA804EC0E3308F5B5C525B73D97ED0EC1B0B7DB413CF0ACF40D693B0`.
- Local and installed package trees contain no Zhuqiaomon, PolyHook, RedBird, or private logging assembly. Installed SHCDESE remains the official 2.0.2 artifact, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.
- One post-build hash command had a PowerShell interpolation parser error at `$mod:`; the corrected `${mod}` form completed read-only verification. No artifact was changed by the failed command.

Open points and handoff:

- No mandatory P3a work remains. Actual Active-AIV selection, hostile-gate routing, and Hunter state-7 gameplay sessions are optional follow-up evidence and do not block P3b.
- P3b must start from a fresh inventory of MoatCommandTest, OxTetherIdleFixTest plus tests, QueueTest plus StaticTests/native Remove contract, and StockpileAccessFixTest. MoveMoatTest remains exclusively deferred as P3M and must not be changed or built.

### P3b - MoatCommandTest, OxTetherIdleFixTest, QueueTest, and StockpileAccessFixTest

Status: `abgeschlossen`

Fresh-session inventory:

- MoatCommandTest has five Zhuqiaomon inline hooks plus one direct PolyHook `NativeDetour`; it already declares gameplay `NetworkMode=1` but still uses the old dependency and LibraryLoaded contracts.
- QueueTest has five Zhuqiaomon managed-function detours spread across three transactions, one `GetSelectedChimps()` call still typed as `int[]`, and its exact native Remove adapter at RVA `0x123EA0`. The direct adapter and rollback behavior must remain independent from AIDefense and must be revalidated for exact 2.0.2. It already declares `NetworkMode=1`.
- OxTetherIdleFixTest and StockpileAccessFixTest have no direct hook transaction in their own code, but expose 2.0.2 RedBird Core memory types through SHCDESE APIs; both have stale Zhuqiaomon references, old callbacks/dependencies, existing gameplay `NetworkMode=1`, and Ox has its own automated tests.
- MoveMoatTest remains outside scope and will not be modified or built. Existing QueueTest user work will be preserved.

Implemented changes and contracts:

- All four plugins now pin `[BepInDependency("000shcdese", "2.0.2")]`, consume `CrusaderLibraryLoadContext`, retain their existing mod versions, and remain gameplay-synchronized with `NetworkMode=1`.
- MoatCommandTest replaces five Zhuqiaomon context hooks and its standalone PolyHook detour with five `HookHandle<X64InlineHook>` values plus one typed RedBird `DetourHandle`. One rollback-and-throw transaction uses six explicit `HookTarget.FromAddress` targets, preserves the audited hook sizes/register snapshots/callback placements, checks the aggregate `CommitResult` and every handle, and reaches Vanilla through the typed `Original`. The transaction owns its hooks only for genuine final feature cleanup; normal SHCDE startup never calls that cleanup.
- QueueTest replaces five Zhuqiaomon whole-function detours across its three logical transactions with typed RedBird handles and explicit targets. Every commit and every associated handle is checked. The transactions explicitly classify their hooks as process-lifetime (`OwnsHooks=false`), and all ten normal/fail-open Vanilla paths use typed `Original` delegates.
- QueueTest now consumes `SelectedUnitInfo[]` and projects `UnitId` in the existing order. Its independent native Remove delegate remains `(manager, unitId, tribeId)` at RVA `0x123EA0`; both normal isolation and rollback calls retain this ordering. The exact 2.0.2 source at commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79` was reread and still forwards `UnassignUnit(tribeId, unitId)` as `(_tribeManager, tribeId, unitId)`, contrary to the native contract. `NATIVE_CONTRACT.md` now records the 2.0.2 finding and the still-relevant ushort/tribe-event constraints.
- OxTetherIdleFixTest and StockpileAccessFixTest now use `RedBird.Core.Memory` for the memory view exposed by the 2.0.2 APIs; stale Zhuqiaomon project references and validation gates were replaced with `RedBird.Core`. Their investigation/audit identity was updated to exact 2.0.2 commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`.
- The comprehensive Stockpile `_inspect` test project was likewise changed from its removed Zhuqiaomon reference to RedBird Core. No MoveMoatTest source, project, documentation, artifact, or installed output was changed or built.

Checks and tests:

- The manually updated installed Script Extender exactly matches both canonical repository outputs: 1,408,000 bytes, assembly version `2.0.2.0`, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`. The extender worktree is clean at `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`; no repair was necessary.
- Installed `CrusaderDE.dll` and Native Baseline `CURRENT.json` both remain SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- QueueTest StaticTests passed 428 checks. Besides all queue, ID, Chore, exact function-body and Remove/rollback contracts, the expanded suite checks the four P3b source/manifests, exact dependencies/load contexts, RedBird handle/target/commit/original ownership, absence of legacy APIs, SelectedUnitInfo projection, and unique canonical-PE matches at all four MoatCommandTest scan RVAs. Its first migration run expected eleven typed Original call sites although the audited control flow contains ten; only that test count was corrected, and the full suite then passed first with 420 and finally with 428 checks after the four Moat pattern/RVA assertions were added.
- OxTetherIdleFixTest's prescribed .NET Framework policy suite passed 75 assertions. `dotnet run` cannot launch this old-style executable project, so the required MSBuild-plus-EXE invocation was used. A separate historical `_inspect/OxTetherIdleFixTestTests` harness still expects fault-injection APIs already absent before P3b and failed 11 compile assertions; an exploratory reference-only edit there was fully reverted and the file is clean. It is not the test project listed by the plan or called by the build driver.
- The comprehensive StockpileAccessFixTest harness passed 1,123 assertions after its compile reference was migrated to RedBird Core.
- Production sources/projects contain no Zhuqiaomon, PolyHook `NativeDetour`, `HookRef`, `.Hook.Trampoline`, old LibraryLoaded signature, flags-only SHCDESE dependency, or `int[]` assignment from `GetSelectedChimps()`. `git diff --check` passes.
- All 18 initially changed P3b text paths were normalized by explicit path as UTF-8 without BOM, ordinally reread, and confirmed CRLF-only with zero naked LF. The later Queue test addition and Stockpile harness project were normalized and independently rechecked as well. There are no accidental literal `\\r\\n` sequences.
- One early compile-only `dotnet build` of QueueTest and MoatCommandTest was mistakenly run before the complete static gate. It succeeded with zero warnings/errors and touched only their local build outputs; it did not install anything. After all checks and CRLF validation, the prescribed build drivers below were each still run exactly once. One preliminary CRLF command also hit PowerShell's prohibited empty-pipeline parser form before executing; the corrected collected-row form performed the operation. Two `rg` checks used unsupported Windows path globs and were repeated with directory arguments plus `--glob`; neither failed read changed state.

Build, installation, and artifact verification:

- `MoatCommandTest/build.bat /nopause`, `OxTetherIdleFixTest/build.bat /nopause`, `QueueTest/build.bat /nopause`, and `StockpileAccessFixTest/build.bat /nopause` each ran once after the final checks. Every runtime build and installation completed with 0 warnings and 0 errors; the Ox driver also reran all 75 policy assertions.
- Local package and installed DLL hashes match exactly: MoatCommandTest `729594CE60F08DF45E2F400096672D27318D07C4E94AA86A19F344D4BF701213` (43,008 bytes), OxTetherIdleFixTest `D3B64AFB4C8CD8A5F2AD84D263DDCC1B9F7A1BAC08531F1C1EE73F5E4625DBAF` (59,392 bytes), QueueTest `EC7B24D2CD0D23784027C4C5A1B0AC16E31B06D282FED435E295FFBE2525F869` (101,888 bytes), and StockpileAccessFixTest `63F17EEDD5677FBE5D4F3384A6A0C4CF91E748A65ECA3155A4EE474E2F57244A` (62,464 bytes).
- All installed manifests retain their source versions and `NetworkMode=1`. Local and installed package trees contain zero Zhuqiaomon, PolyHook, RedBird, or private Microsoft logging assembly. Installed SHCDESE remained byte-identical after all builds.

Open points and handoff:

- No mandatory P3/P3b work remains. Real Moat command, ox tether, queue multiplayer, and stockpile gameplay sessions are optional follow-up evidence and do not block P4.
- P4 must begin as a fresh session from CastlePlanner, its AIV placement/parser dependency chain, the current plan, AGENTS.md, and this status. MoveMoatTest remains exclusively deferred as P3M.

### P4 - CastlePlanner and parser/placement chain

Status: `abgeschlossen`

Fresh-session inventory:

- CastlePlanner has one direct Zhuqiaomon 16-byte x64 context hook at the audited human-Keep coordinate load RVA `0x95B3C`. The callback snapshots all registers and runs before the relocated instructions through Zhuqiaomon's `AfterCallback` naming. Its process-rooted runtime has no normal teardown path.
- The plugin still uses the flags-only Script Extender dependency and old `(IntPtr, ReadOnlySpan<byte>)` LibraryLoaded signature. Shared preset registration and the preset view-model base are already present; the packaged settings XAML already provides both scrollbars and separated host/client controls. The packaged manifest is the authoritative tracked `info.json`, version `0.8.21`, but initially lacked the required gameplay `NetworkMode=1` classification; the migration must add it.
- CastlePlanner references and packages `CastlePlanner.AIVPlacement.Core`, `AIVPlacement.Core`, `AIVParser.Core`, and `MapParser.Core`. Their net10 test projects are present. The active CustomLords catalog and localized display paths remain current; no active `getComputerName` caller was found.
- P4 source paths are clean before migration. README files remain out of scope.

Implemented changes and contracts:

- CastlePlanner now pins Script Extender `2.0.2`, accepts `CrusaderLibraryLoadContext`, and passes its module, memory, and region through the native initialization without mixing older API contracts.
- The single 16-byte human-Keep coordinate-load hook is now a typed `HookHandle<X64InlineHook>` in a rollback-and-throw RedBird transaction with an explicit `HookTarget.FromAddress`, all-register snapshot, `LogAndContinue`, and the same callback-before-relocated-instructions behavior expressed by RedBird's `AfterCallback` placement. Both `CommitResult.IsCompleteSuccess` and the handle success flag are required. `OwnsHooks=false` records its actual process-lifetime ownership; the startup `OnDestroy` path remains logging-only.
- CastlePlanner.csproj replaces Zhuqiaomon, PolyHook2 and Iced with non-private RedBird Abstractions/Core/X64 references and validates all three 2.0.2 files. Existing MonoMod runtime detours are managed UI/runtime hooks unrelated to the migrated native Zhuqiaomon site and remain unchanged.
- The tracked packaged `info.json` now explicitly declares `NetworkMode=1`. Mod version `0.8.21`, all preset classifications, separate host/client activation, shared preset registration, both settings scrollbars, CustomLords catalog behavior, and localized displays remain unchanged. No README was edited.
- CastlePlanner's placement test suite gained an exact migration contract check covering dependency/load context, RedBird handle/target/options/commit/ownership, project dependencies, manifest mode, settings reachability, and the canonical native pattern.

Checks and tests:

- MapParser passed 36/36 tests; AIVParser passed 38/38; standalone AIVPlacement passed 29/29; CastlePlanner.AIVPlacement passed 57/57 after the new migration test was added. Custom AIV/Lord resolution, parser roundtrips, placement policies, UI display naming, cache/concurrency, and spawn encoding all remain green.
- The new native check finds the 28-byte human-Keep signature exactly once in canonical `CrusaderDE.dll` and at audited RVA `0x95B3C`.
- The first CastlePlanner run passed 56/57 and correctly exposed that the tracked package manifest lacked `NetworkMode`; adding the required gameplay classification made the full 57/57 suite pass.
- A first chained AIVParser invocation ended with MSBuild's generic failure line and no warning or error details. A serial explicit restore confirmed all cached assets, after which the serial build/test and direct executable both passed 38/38. The build chain reports pre-existing `NU1900` warnings because the sandboxed NuGet audit endpoint is unavailable at its configured `127.0.0.1:9`, plus `MSB3277` warnings in net10 test projects that directly reference the net481 SHCDESE assembly (`System.Text.Json` and `DiagnosticSource` patch-version conflicts). These do not affect runtime compilation or test results.
- One read-only process check hit the documented WindowsApps `pwsh.exe` access-denied startup condition and succeeded unchanged through the required elevated path. It confirmed that the earlier long test chain had completed; the many remaining dotnet entries were reusable MSBuild nodes rather than active tests.
- Static scans and project XML parsing pass. Production CastlePlanner source/project contains no Zhuqiaomon, PolyHook2, `HookRef`, old LibraryLoaded callback, or flags-only SHCDESE dependency. All six changed P4 text paths plus the manifest/status correction are UTF-8 without BOM, ordinally reread, CRLF-only, and free of accidental literal `\\r\\n` sequences; `git diff --check` passes.

Build, installation, and artifacts:

- `CastlePlanner/build.bat /nopause` ran once after the complete static and automated gates. Its internal 57-test suite passed, the .NET Framework runtime project compiled with 0 warnings and 0 errors, 574 package files were overlaid into the game, and the local provenance record was generated. Only the test/Core restore/build phase emitted the documented pre-existing NuGet/assembly-version warnings.
- Local and installed hashes match: CastlePlanner.dll `507FFC3361D61A395F2BC9C732474D84874AE7FC0B21C4CF7AD0A5A5C9558E45`; CastlePlanner.AIVPlacement.Core.dll `FE33060EBBB24FF1099A81456912FC012BE0B260F7327BDF366CEE4C3E4AEE6F`; AIVPlacement.Core.dll `A8024C4552FFDB8922D381251324E6F5E4CAEDBF646DE9DC82A0C1E94B7681E8`; AIVParser.Core.dll `C0CC7A82A6E9174AA488510A3A177F14FC6F7FCA7D00FDCEFEA5BF63F12B7696`; MapParser.Core.dll `9ED857F18E8A5FDD33B39FCB533C5599E78354E1105E501815F1C3D4684B7580`; info.json `07E83B9EFC07DE0FABBC2845DB2094E60B9C62A8A680737B1D5661C23676BCCA`.
- Installed manifest reports version `0.8.21` and `NetworkMode=1`. Neither package nor installation contains private Zhuqiaomon, PolyHook, RedBird, or Microsoft logging assemblies. Installed SHCDESE remains unchanged at SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and handoff:

- No mandatory P4 work remains. An actual native Spawn/Blueprint gameplay marker is optional follow-up evidence and does not block P5.
- P5 must begin from a fresh inventory of ImprovedHunters, its own tests and shared hook/scanner infrastructure. MoveMoatTest remains deferred.

### P5 - ImprovedHunters

Status: `abgeschlossen` (P5a-P5c)

Result and source changes:

- The fresh inventory found 19 production source files plus the project with direct or stale Zhuqiaomon dependencies. All were migrated to RedBird 2.0.2 contracts. `ImprovedHunters.csproj` now references `RedBird.Abstractions`, `RedBird.Core`, and `RedBird.X64` with `Private=false`; direct Iced use remains intentionally referenced. Missing-RedBird build gates were added. Negative searches find no `Zhuqiaomon`, `HookRef`, `Unload`, old LibraryLoaded signature, flags-only Script Extender dependency, or direct `Kernel32.VirtualProtect` path.
- The plugin pins `[BepInDependency("000shcdese", "2.0.2")]`, consumes `CrusaderLibraryLoadContext`, and passes its borrowed `Region`, `Memory`, and `ModuleHandle` into the runtime. The existing lifecycle remains correct: startup `OnDestroy` preserves the static runtime; only `OnApplicationQuit` performs final cleanup. Version remains intentionally unchanged at `1.1.78`.
- `HunterHookInfrastructure` centralizes explicit `HookTarget.FromAddress`, `ContextHookOptions`, the official Script Extender logger factory, `RollbackAndThrow`, and `OwnsHooks=true`. The 16 typed context handles are GranaryChickenLimitPatch 1, HunterPostShotContinuationDiagnostic 4, HunterQueryActorWorkaround 1, HunterRemainingPathSpeedRecovery 1, HunterTargetSearchFallbackDiagnostic 6, HunterVanillaPathContinuationDiagnostic 2, and ManualChickenAttackPatch 1. All seven transactions check both `CommitResult.IsCompleteSuccess` and every handle. Their idempotent feature Dispose is a genuine failed-initialization or final runtime teardown, so it replaces the seven former `Unload(); Dispose()` pairs without startup teardown or double-unload.
- Every previous register mask, hook size, callback error mode, and overwritten-instruction placement is passed explicitly through the common helper. Transactions use the Extender-owned live `ScanRegion`, never an obsolete copied memory snapshot. Failure remains atomic and fail-closed.
- `AutomaticChickenTargetPatch` and `HunterHutVisibilityPatch` retain their one-byte dispatch-table contracts: validate expected original/current byte, write exactly one byte, verify the result, and restore only a still-owned value during Dispose. Writes now use public `CodePatch.Write`, which restores page protection and flushes the instruction cache. The serialized lifecycle/settings path and atomic byte-sized table entry are the synchronization boundary; no executable instruction block is partially rewritten.
- Stateful ownership is separated. `GameGlobalsManager.Instance.RabbitDespawnTickTime` remains Script-Extender-owned: ImprovedHunters creates only its own `SetOverride` scope, disposes that scope on disable/final teardown, and never disposes or force-resets the shared site. The locally constructed camel and chicken `ManagedAssemblyImmediate<short>` sites are mod-owned and are disposed at true final teardown, restoring their original instruction bytes. Their zero constant offsets and operand 1 selection remain unchanged.
- The package manifest now explicitly declares gameplay-affecting `NetworkMode: 1`. The installed package contains only `ImprovedHunters.dll`; no private Zhuqiaomon or RedBird copy was introduced.

Tests and checks:

- `_inspect/ImprovedHuntersChickenTests`: expanded from the existing policy coverage to 177 assertions and passed. It covers the prior chicken/query/manual-attack policies, RedBird signed imm8 limits (-128/127 accepted, -129/128 rejected), imm16 Get/Set bytes, Dispose restoration, 16 handles, seven aggregate commits, RedBird references, hook ownership, and rabbit/camel/chicken separation.
- The same suite maps the canonical PE into its loaded RVA layout and verifies SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. All 29 declared native patterns match their audited current source contract; output classified 19 unique fallback definitions and one intentionally reference-RVA-only ambiguous ResolveUnique definition. The ambiguous `StateNineCompletionWriterPattern` has three current-image matches, including its audited RVA, so the matching-hash reference path is valid while `FindUniquePattern` rejects a changed-hash fallback fail-closed.
- `_inspect/HunterActiveVisibilitySnapshotTests/Run.ps1`: passed. Two stale assertions were corrected to the already-current production contract: successful MoveHere additionally requires `CanRunHunterPathfinding()`, and the audited attack gate is RVA `0x130160` with first target `0x13017A` rather than the obsolete `0x130110`/`0x13012A`. No runtime behavior was relaxed.
- Final compile-only gate against the explicit canonical game directory: passed. An earlier diagnostic invocation without `/p:GameDir` failed only because the legacy project default did not resolve the external game references; the unchanged source compiled when the required canonical path was supplied.
- CRLF/encoding gate covered all 26 changed P5 text files: ordinal reread succeeded, naked LF count 0, literal `\\r\\n` count 0. `git diff --check` passed for P5; its only workspace warning concerned the not-yet-renormalized plan file before this status update.
- Test-only warnings: NU1900 because NuGet vulnerability metadata was unavailable, and MSB3277 because the net8 test host prefers its DiagnosticSource 8 reference while the net481-provided logging/RedBird assemblies were built with DiagnosticSource 10. These did not affect execution; all 177 assertions passed. The actual net481 mod build had 0 warnings and 0 errors.
- `ImprovedHunters/build.bat /nopause` was run once after all gates. Build succeeded with 0 warnings and 0 errors and installed 25 files. Decompiled output confirms `[BepInDependency("000shcdese", "2.0.2")]` and plugin version `1.1.78`.
- Local/installed artifact equality:
  - `ImprovedHunters.dll`: 520704 bytes, SHA-256 `9FAFC6218DC7CE346F974286AB8DACD2B815F370102801E69688496AED07BD7A`.
  - `ImprovedHunters.pdb`: 140448 bytes, SHA-256 `70DFE32A337A2D3C8A57E79AE2CE0C83E8DA6D8FC3B6B56A06AB9E92954E1A95`.
  - `info.json`: 34184 bytes, SHA-256 `70699FE8460422DED455193A35CE37D4DC84AB18073C5358614FDB3D6D35B91F`, installed `NetworkMode=1`.

Open points and handoff:

- No mandatory P5 work remains. An actual in-game hook/patch enable-disable and post-startup map/tick marker is optional follow-up evidence under the user's test waiver and does not block P6.
- P6 must start as a fresh session from BugfixesAndQoL, the shared Gatehouse policy, current Moat-Fill ownership, its tests, this status, the plan, and AGENTS.md. MoveMoatTest remains untouched and deferred.

### P6 - BugfixesAndQoL, Moat-Fill and Gatehouse policy

Status: `abgeschlossen` (P6a-P6c)

Fresh-session inventory:

- Current scope has 20 Zhuqiaomon-importing production files plus the project reference (21 `rg -l` results), exactly 18 `Unload()` calls, and two standalone PolyHook `NativeDetour` instances in `ImprovedMoatFillingFix`. This matches the plan once the project row is separated from the 20 source files.
- P6a API breaks are three direct selected-unit callers (`AssassinClimbRuntime`, `MountedStockpileMovementPatch`, `SiegeAmmoRestockFeature`), signed-gold logic in `CtrlMarketTradeHook`, the 1.42-only Gatehouse conversion in `Shared/GatehouseQueryUnitIdPolicy`/`ReachableEnemyGatehouseRuntime`, effective multiplayer speed configuration, and standalone Moat ownership/bridge behavior.
- Six fail-closed Chore senders remain in Assassin climb, multiplayer speed, quarry relocation, siege-ammo restock, single-building pause, and surrender. They are reserved for P6c after the P6b native migration.
- Five manual page-protection paths remain: `AiStoneReserveFix`, `AssassinPathReconstructionPatch`, `AssemblyPointPlacementPatch`, `HealerAttackCommandPatch`, and `LordControlGroupNativePatch`. They are reserved for P6b together with all RedBird hooks and the standalone Moat detours.
- Existing standalone ownership already selects BugfixesAndQoL when MoveMoat is absent, delegates only after bridge status 1, uses standalone ownership after explicit bridge status 0, and fails closed for missing/incompatible/unknown/throwing bridges. P6 will test and preserve this without reading, changing, or building MoveMoatTest.

#### P6a - API contracts and standalone ownership

Status: `abgeschlossen`

Changes and contracts:

- `AssassinClimbRuntime`, `MountedStockpileMovementPatch`, and `SiegeAmmoRestockFeature` now consume the Script Extender 2.0.2 `SelectedUnitInfo[]` result and project its already one-based `UnitId` exactly once. The obsolete `int[]` contract is absent from all three callers.
- The shared Gatehouse policy now validates event-provided game IDs in the inclusive range 1..span length and returns them unchanged. `ReachableEnemyGatehouseRuntime` no longer applies the pre-1.45 span-index `+1` correction; its logging identifies the value as an event UnitId.
- Ctrl market gold stays signed throughout. New `MarketGoldPolicy.CanAfford` fails closed for negative gold or cost and compares without arithmetic overflow; both the single-unit execution guard and UI affordance use it.
- `MultiplayerGameSpeedPolicy.MaximumSpeed` now matches the audited 2.0.2 default of 1500. The runtime already passes the live `Plugin.Instance.MaxGameSpeed.Value`, so deliberately different local Extender configuration remains authoritative.
- Standalone moat ownership behavior was reread and remains correct: absent MoveMoat and explicit bridge status 0 select the standalone owner; bridge status 1 delegates; missing/incompatible/unknown/throwing bridge states fail closed. MoveMoatTest was not read for implementation, changed, or built.

Checks and tests:

- `_inspect/BugfixesAndQoLNativeTests` passed 896 assertions and 61 canonical native signatures with 0 build warnings/errors. New assertions cover unchanged one-based Gatehouse boundary IDs, invalid 0/negative/out-of-range IDs, signed market gold including negative and `int.MaxValue`, and all three SelectedUnitInfo source contracts.
- `_inspect/HostClientPresetTests` passed completely with the 2.0.2 routing/authority/game-mode/Trail/preset/MessagePack sentinels. It now asserts the default 1500 ceiling, a deliberately different 925 configured ceiling, and a non-step configured ceiling.
- The Host/Client suite first exposed two test-environment issues rather than product failures. Its obsolete-Custom-Lord fixture had coupled its reload payload to unrelated live preset persistence; it now explicitly seeds the two false current values before adding the obsolete key, isolating the intended compatibility test. The sandbox then denied the suite's intentional atomic `File.Replace`; the unchanged elevated run passed. Failure messages retain the relevant values/error for future diagnosis.
- No actual game, Host/Client, or Proton session was required under the user's explicit waiver. Those sessions remain optional and nonblocking; all static and automated P6a gates are complete.

Open points and handoff:

- No mandatory P6a work remains. P6b must freshly migrate the exact dependency/load context, RedBird hook transactions and 18 Unload sites, five memory-write paths, and standalone moat detours. It must preserve the existing fail-closed ownership bridge and keep MoveMoatTest untouched.

#### P6b - RedBird hooks, memory writes, and standalone moat detours

Status: `abgeschlossen`

Changes and contracts:

- All 20 originally inventoried Zhuqiaomon production paths now use the Script Extender 2.0.2 RedBird contracts. `BugfixesAndQoL.csproj` replaces Zhuqiaomon and PolyHook2.NET with non-private RedBird Abstractions/Core/X64 references and validates their presence. The plugin pins exact Extender `2.0.2`, consumes `CrusaderLibraryLoadContext`, and the runtime borrows `ModuleHandle`, `Memory`, and `Region` without disposing the Extender-owned `ScanRegion`.
- The migrated native layer has 29 typed RedBird handles in 17 rollback-and-throw transactions. Every group checks `CommitResult.IsCompleteSuccess` and all required handles. Context-hook targets and options are explicit; feature-owned transactions use `OwnsHooks=true`, and reversible settings use each live handle's `Enable`/`Disable` state instead of destroying the transaction. The former 18 `Unload()` calls are gone; idempotent final/failed-initialization disposal now relies on owned transactions.
- Independent review found two additional direct `NativeDetour` users not counted among the original Zhuq files: weighted Assassin pathfinding and disbanded-unit control-group cleanup. Both were also migrated to typed RedBird detour handles, explicit targets, checked atomic commits, and `Original` calls, so the P6 negative gate has no direct PolyHook native detour left.
- `AIBuildingTemporaryAccessClassifier` and quarry relocation now consume `RedBird.Core.Memory`. The five manual executable-memory paths (`AiStoneReserveFix`, Assassin path reconstruction, assembly-point placement, Healer attack dispatch, and Lord control groups) use `CodePatch.Write` after retaining their existing original-byte, bounds, ownership, write verification, and rollback checks. No manual `VirtualProtect` remains.
- The two standalone moat detours are one durable `OwnsHooks=false` transaction because their process-lifetime owner intentionally survives normal startup cleanup. Both typed handles and the aggregate commit must succeed before either `Original` is callable. The existing absent/ready/failed/incompatible MoveMoat bridge ownership policy is unchanged, and MoveMoatTest was neither read for implementation nor changed or built.
- The authoritative manifest now explicitly declares gameplay `NetworkMode=1`; version remains `1.0.126`. No README was edited.

Checks and tests:

- Expanded `_inspect/BugfixesAndQoLNativeTests` passed 918 assertions and all 61 native signatures. New migration checks cover the 29 handles, 17 checked commits, exact dependency/load context, borrowed region, all five `CodePatch.Write` paths, absence of legacy APIs, the durable two-detour moat contract, manifest mode/version, and the newly migrated disband detour. Two pre-existing source assertions were updated from the old `Unload`/`NativeDetour` implementation to the equivalent RedBird ownership contract.
- `BugfixesAndQoL/tests/ImprovedMoatFilling.Tests` built with 0 warnings/errors and passed all 27 policy/native checks: standalone ownership, every bridge outcome allowed in P6, reservation rollback, selector/resolver entry bytes, downstream planner gates, canonical DLL existence/hash, and fail-closed live-entry validation. Combined execution with MoveMoat remains reserved for P3M.
- Production source/project static gates report zero `Zhuqiaomon`, `HookRef`, `.Unload()`, old trampoline access, `VirtualProtect`, direct `NativeDetour`, or `ManualApply`. Project XML and authoritative JSON parse successfully; `git diff --check` has no whitespace errors. CRLF normalization is deliberately deferred until the complete P6 source set is finished immediately before its one prescribed build.
- A compile-only diagnostic against the canonical game directory reaches exactly the 12 expected unresolved `ChoreNetworkTransport` references in the six P6c senders, with 0 warnings and no P6b error. This proves the complete P6b native changes compile through the next intentionally pending API break.

Open points and handoff:

- No mandatory P6b work remains. P6c must freshly migrate the six Chore senders, add serialization/size/failure tests, run the full BugfixesAndQoL and Host/Client test set, normalize all P6 text files, then run the single prescribed build/install and artifact checks. Actual game/Host/Client sessions remain optional and nonblocking.

#### P6c - Chore senders, full verification, build, and installation

Status: `abgeschlossen`

Changes and contracts:

- New `BugfixesAndQoLChoreSender` centralizes the exact public 2.0.2 fail-closed contract. It requires a registered packet hook, serializes one subsequently unchanged packet object, requires nonzero `ChoreManagerVA`, accepts total payloads through exactly 1200 bytes including the two-byte packet ID, and only then passes that same object to `SendPacketToAllEx2(..., viaChore: true)`. Missing prerequisites and serialization/send exceptions return without an explicit Steam path or caller-side gameplay mutation.
- Assassin climb, multiplayer time control, quarry-pile relocation, siege-ammunition restock, single-building pause, and surrender execution all use the helper. Availability checks now query their actual packet-hook registration plus `ChoreManagerVA`; nullable packet-ID reads fail closed. The old raw blob construction was removed except for surrender's post-success diagnostic hex reconstruction, which cannot affect or replace the already-sent original packet.
- The six packet classes use eager fields/arrays and their existing deterministic MessagePack formatters; no lazy, clock-dependent, or mutating formatter data is introduced. Exactly six `TrySend` call sites and zero `ChoreNetworkTransport`/`SendRawBlob` references remain.

Tests and final gates:

- `_inspect/HostClientPresetTests` passed after adding the common Chore helper. It serializes the same unchanged packet object twice through its `GameNetworkAPI.Serialize` test surface and compares bodies bytewise; it also proves missing hook/manager, serializer failure, send failure, totals 1199/1200/1201 bytes, paused simulation, original-object identity, fail-closed availability, and no rejected-path mutation. Static checks bind all six production callers to `GameNetworkAPI.Serialize(value)` and `SendPacketToAllEx2(value, id, viaChore: true)`. Existing routing, authority, game-mode, Trail/client-lock, preset, and MessagePack sentinel coverage remains green.
- The complete BugfixesAndQoL regression passed: NativeTests 918 assertions/61 signatures; ImprovedMoatFilling 27 checks; AIV memory 5 scenarios; AIV sync 18 scenarios; map-origin sort, random-AI, and startup-diagnostic suites all passed. The first generic runner correctly handled the .NET Framework executables but could not launch the two net10 projects from a nonexistent legacy `bin/*.exe` path; their already successful builds were then executed with the correct `dotnet run --no-build` path and both passed.
- Final production gates find no Zhuqiaomon, direct `NativeDetour`, HookRef, Unload, old trampoline access, VirtualProtect, removed Chore transport, old LibraryLoaded callback, flags-only hard dependency, Context/Region disposal, or obsolete selected-unit assignment. Six and only six Bugfixes Chore senders remain. XML/JSON parsing, version consistency (`1.0.126`), explicit `NetworkMode=1`, and `git diff --check` pass.
- All 47 changed P6 text paths were normalized by explicit path, ordinally reread as UTF-8 without BOM, and verified with zero naked LF. The one literal `\\r\\n` occurrence is an intentional C# source assertion for a CRLF attribute/property boundary, not file corruption.
- An early P6c compile-only diagnostic was run after implementing the helper but before the complete regression and succeeded with 0 warnings/errors. After all final tests and static/CRLF gates, the prescribed `BugfixesAndQoL/build.bat /nopause` ran once. Its internal moat suite passed; the runtime built with 0 warnings/errors and installed successfully.

Build, installation, and artifacts:

- Local and installed files match exactly: `BugfixesAndQoL.dll` 1,044,992 bytes, SHA-256 `7D26C5B2E5362DFE3F3B83B08D63920B1A1B6966609DEA706B330FCAB5908E16`; PDB 341,616 bytes, SHA-256 `48AB2AE2CCAA37980B720ED45118B1E18194C5A0FA1D8A60F5A039CDC4D18143`; manifest 30,500 bytes, SHA-256 `A3E855451F1FDCA6CEA5133340C296B3E21FE4C4AA718129EC00E4C6C891F025`.
- Source, package, and installed manifests all report version `1.0.126` and `NetworkMode=1`. Decompiled output confirms `[BepInDependency("000shcdese", "2.0.2")]` and plugin version `1.0.126`. Package and installation contain no private Zhuqiaomon, PolyHook, RedBird, or Microsoft logging assembly.
- Installed SHCDESE remains byte-identical to the canonical verified 2.0.2 build, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and handoff:

- No mandatory P6 work remains. Actual game and Host/Client sessions are optional under the user's waiver and do not block P7. Combined runtime ownership with MoveMoatTest remains deferred exclusively to P3M; MoveMoatTest was not changed or built.
- P7 must begin as a fresh session from ExtraFeatures, the now-final shared Gatehouse policy, its eight 2.0.2 migration surfaces, current tests, plan, AGENTS.md, and this status.

### P7 - ExtraFeatures

Status: `abgeschlossen` (P7a-P7b)

#### P7a - RedBird hooks, teardown ownership, and plague memory write

Status: `abgeschlossen`

Fresh-session inventory:

- ExtraFeatures contained the eight planned Zhuqiaomon source paths: two managed AI-defense detours, two managed AI-market detours, one AI-flag detour, the Monk assembly hook, two plague context hooks, the plague immediate writer, one managed refund-value type, and one stale Knight import. The six hook groups owned eight hook handles and the tree contained exactly ten explicit `Unload()` calls.
- The plugin/project still used the old LibraryLoaded callback and Zhuqiaomon/PolyHook references. The separate `GatehouseTimingPatch` manual writer is not the P7a plague writer and remains outside this slice.

Changes and contracts:

- ExtraFeatures now pins Script Extender `2.0.2`, consumes `CrusaderLibraryLoadContext`, and borrows `ModuleHandle`, `Memory`, and `Region` without disposing the Extender-owned `ScanRegion`. The project uses non-private RedBird Abstractions/Core/X64 references and retains Iced only for the existing audited assembly/decode logic.
- `ExtraFeaturesHookInfrastructure` centralizes the official Extender logger factory, rollback-and-throw transactions, `OwnsHooks=true`, explicit `HookTarget.FromAddress`, and explicit context-hook options. The six logical transactions own eight typed RedBird handles: five managed detours and three x64 inline hooks. Every transaction checks `CommitResult.IsCompleteSuccess` and every member handle; managed fallbacks call typed `Original` delegates.
- All ten former `Unload()` calls are gone. Failed initialization and genuine feature/final cleanup dispose the owned transaction once; the plugin's startup lifecycle still has no `OnDestroy` teardown.
- The Monk generator keeps its exact 20-byte audited boundary and both `AddUnrestrictedJmp` branches, now through `RedBird.X64.Extensions`. The plague lifetime immediate retains expected/current-byte validation, writes through `CodePatch.Write`, verifies the live value, and updates ownership state only after successful verification. The unrelated Gatehouse timing writer was deliberately not changed.
- Refund managed values now use `RedBird.Core.Memory.Managed.ManagedValue<float>` and the stale Knight Zhuqiaomon import was removed. Mod version remains `1.0.88`; the final gameplay manifest classification belongs to P7b before the package build.

Checks and tests:

- `_inspect/ExtraFeaturesNativeTests` was extended with the complete P7a migration contract and passed 270 assertions against canonical `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. It verifies all native function hashes/patterns plus exact RedBird dependency/load-context wiring, eight handles, six checked commits, no Zhuqiaomon/HookRef/Unload/old trampoline, plague `CodePatch.Write`, Monk unrestricted jumps, and deliberate preservation of the independently owned Gatehouse writer.
- A compile-only ExtraFeatures diagnostic reaches only the seven expected P7b breaks: five removed `ChoreNetworkTransport` references, the obsolete Gatehouse span-index conversion plus its dependent definite-assignment error, and the old `int[]` selected-unit return. No P7a compilation error remains.
- Production negative searches confirm zero Zhuqiaomon references, `HookRef`, `.Unload()`, or old trampoline access. CRLF normalization and the prescribed single build are deferred until P7b completes the remaining source set.

Open points and handoff:

- No mandatory P7a work remains. P7b must freshly migrate SelectedUnitInfo projection, treat the Gatehouse event value as an already one-based game ID through the shared validator, replace both Chore senders with the public 2.0.2 contract, add edge/serialization tests, declare `NetworkMode=1`, run the full regression, normalize all P7 text files, and then run the one prescribed build/install and artifact verification. Actual Host/Client/runtime sessions remain optional and nonblocking.

#### P7b - API contracts, Chore senders, full verification, build, and installation

Status: `abgeschlossen`

Changes and contracts:

- `KnightDismountRuntime.GetSelectedChimpsSafe` now consumes `SelectedUnitInfo[]`, projects each already one-based `UnitId` into a new `int[]`, and preserves selection order for both visibility checks and transformation snapshots.
- `GatehouseAutomationRuntime` treats `GatehouseQueryEventArgs.UnitId` as the normal one-based 2.0.2 game ID and validates it unchanged through `Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId`. The obsolete 1.42 span-index correction and its misleading log terminology are gone.
- New `ExtraFeaturesChoreSender` implements the public 2.0.2 fail-closed path shared by Gatehouse automation and Knight transformation: require the registered packet hook, serialize the same packet object once for size validation, require nonzero `ChoreManagerVA`, admit total payloads through exactly 1200 bytes including the two-byte packet ID, then pass that same object to `SendPacketToAllEx2(..., viaChore: true)`. There is no raw-blob or explicit Steam fallback, and rejected paths do not reach caller gameplay mutation.
- Both source and packaged manifests retain version `1.0.88` and now explicitly classify the gameplay mod as `NetworkMode=1`. No README was changed.

Tests and final gates:

- `_inspect/ExtraFeaturesNativeTests`: 270 assertions passed against the canonical native hash. Besides all function hashes and signatures, it checks the exact P7a RedBird inventory, six aggregate commits, plague writer, Monk hook boundary, dependency/load context, and legacy negative gates.
- `_inspect/KnightTransformationPacketTests`: 31 assertions passed, covering deterministic MessagePack encoding, metadata/target validation, payload bounds, additive decoding, and malformed/oversized input rejection.
- `_inspect/HostClientPresetTests`: built with 0 warnings/errors and passed the full 2.0.2 routing, authority, game-mode, Trail/client-lock, preset, and MessagePack suite. Its new ExtraFeatures cases serialize one unchanged packet twice through `GameNetworkAPI.Serialize`, prove original-object identity, missing hook/manager, serializer/send exceptions, totals 1199/1200/1201, paused simulation, no rejected-path mutation, both production sender call sites, selected-unit projection, Gatehouse ID wiring, and manifest classification. The executable required the already documented elevated path only for its intentional atomic filesystem cases.
- `_inspect/LobbyModSettingsPresetTests` validated eight installed legacy MessagePack files in memory and passed all preset tests. Its first sandboxed run could not perform the intended installed-file migration; the unchanged elevated run passed and restored the test state.
- `_inspect/AuditModSettings.ps1 -Mod ExtraFeatures` passed XAML, shared registration, personal-setting declarations, two-axis scrolling, tooltips, styles, locale parity/translations, and CRLF. The audit itself still expected the P1-removed pre-2.0.2 sync workaround plus obsolete direct roster/Steam transport markers; those stale expectations were replaced with negative workaround enforcement and the current `PlayerIdentityHelper`/official registration contracts before the audit passed.
- The final production gate reports exactly one exact dependency, one LoadContext callback, two Chore senders, eight RedBird handles and six checked transactions; it finds zero Zhuqiaomon, HookRef, Unload, old trampoline, removed raw Chore transport, obsolete Gatehouse conversion, or old selected-unit return. Project XML, source/package JSON, private-dependency scan, and `git diff --check` pass.
- All 22 changed P7/test/plan/status text paths were normalized by explicit path, ordinally reread as UTF-8 without BOM, and confirmed with zero naked LF. The two literal `\\r\\n` occurrences in the audit are intentional corruption-detection patterns.

Build, installation, and artifacts:

- After all source, test, static, and CRLF gates, `ExtraFeatures/build.bat /nopause` ran exactly once. The net481 runtime built with 0 warnings and 0 errors and installed successfully.
- Local package and installed files match bytewise: `ExtraFeatures.dll` 456,192 bytes, SHA-256 `B1847F9C65832E580B006B0D88A09C3E0023094FE90491E4726EFC86AE8E4B79`; PDB 136,456 bytes, SHA-256 `6FA1F5D851974878864B60EF08FDBB9EBE0B58059386B07DEFE51B2597F11B06`; `info.json` 19,584 bytes, SHA-256 `A6C40056D5B8972818E49FFA5D96C677DFFFC4C73FABF4D79D6827AED555A37E`.
- Source, package, and installed manifests all report `1.0.88` / `NetworkMode=1`. Neither package nor installation contains Zhuqiaomon, PolyHook, RedBird, or private Microsoft logging assemblies. Decompiled output confirms `[BepInDependency("000shcdese", "2.0.2")]` and plugin version `1.0.88`. Installed SHCDESE remains the verified official artifact, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.

Open points and handoff:

- No mandatory P7 work remains. Actual Gatehouse/Knight/Monk/plague gameplay and Host/Client sessions are optional under the user's waiver and do not block P8.
- P8 must begin as a fresh session from plan section 6, LinuxModding, its active package/build/release targets, current status, and AGENTS.md. Before any removal, every exact target must be enumerated and resolved under the workspace root. MoveMoatTest remains untouched and deferred.

### P8 - LinuxModding decoupling

Status: `abgeschlossen`

Fresh-session inventory and pre-removal target resolution:

- Every removal target below was confirmed tracked, clean, and resolved beneath workspace root `D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\Meine Mods`; no computed path, wildcard, game path, README, or `_inspect` artifact is a removal target.
- Active Runtime/build/release targets to remove: `LinuxModding/LinuxModding.csproj`, `LinuxModding/src/LinuxModdingPlugin.cs`, `LinuxModding/src/LinuxWorkshopUpdaterBridge.cs`, `LinuxModding/build.bat`, `LinuxModding/release.bat`, `LinuxModding/info.json`, and the `LinuxModding` entry in `Shared/Release/release-projects.json`.
- Active packaged plugin targets to remove: `LinuxModding/BepInEx/plugins/LinuxModding_Serp/LinuxModding.dll`, `LinuxModding.pdb`, and `info.json`. The two old packaged updater-loop scripts are also removed from that plugin folder; its tracked README remains untouched as explicitly required.
- Private-bridge probe targets to remove together: `LinuxModding/tests/DetourProbe/DetourProbe.csproj`, `Program.cs`, and `App.config`.
- `LinuxModding/shcde-linux-launcher.sh`, `install-linux.sh`, `test-windows.bat`, `tests/windows-launcher-tests.sh`, and `tests/fake-game.sh` are retained only after rewriting them as a standalone helper: set `WINEDLLOVERRIDES=winhttp=n,b`, execute the normal Steam command once, and check official BepInEx/SHCDESE 2.0.2/updater/thread-patch files. They must contain no staging, delete, wait, restart, request-file, private-method, plugin-build, or DetourProbe behavior.

Result and changes:

- All 14 predeclared active targets were removed exactly: C# project and both Runtime sources; build/release drivers and source manifest; packaged DLL/PDB/manifest and two packaged updater-loop scripts; the complete three-file DetourProbe. `Shared/Release/release-projects.json` no longer lists LinuxModding. Both README files and all images remain untouched; historical `_inspect` evidence was not removed.
- The retained launcher now performs exactly one runtime action before the normal Steam command: prepend `WINEDLLOVERRIDES=winhttp=n,b` and `exec` the command once. It creates no request, staging, delete, manifest-snapshot, wait, deployment, self-update, process-kill, or restart state.
- The retained checker verifies the BepInEx proxy/core, SHCDESE DLL and exact manifest version `2.0.2`, official `data/mod-updater.sh`, `libredbird_thread_patch.so`, and launcher. It states that it installs no plugin and replaces no updater. The standalone location is `BepInEx/tools/LinuxModding`, so nothing in the helper is registered as a BepInEx/Script-Extender mod or enters lobby comparison.
- `test-windows.bat` now runs only the launcher/checker test suite. Its exit handling compares the exact Git-Bash exit code with zero; this was hardened after the sandbox's negative fatal exit code bypassed CMD's `if errorlevel 1` and misleadingly printed success.

Checks and evidence:

- The elevated launcher-only suite passed 4/4: winhttp override plus exactly one execution, preservation of unrelated Wine overrides, acceptance of a complete official 2.0.2 fixture, and rejection of a 2.0.1 manifest. The first sandboxed attempt failed because Git Bash could not create its signal pipe (`Win32 error 5`); no scenario ran, and the corrected exact exit handling now reports such failures reliably.
- Canonical clean Extender source at `v2.0.2` was reread. `MapModManager.LaunchUpdaterAndExit` selects the Unix backend under Wine, translates game/staging/manifest paths, launches `data/mod-updater.sh`, and kills the game only after successful launch. The official POSIX script waits for game exit, containment-checks deletions, applies staged files, removes staging, and restarts through Steam/xdg-open. This is the complete ownership formerly duplicated by the removed bridge/launcher loop.
- Installed official files exist and are version-correct: `SHCDESE.dll` 1,408,000 bytes / `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`; `info.json` 446 bytes / `E31F76F0EF31F9E619DA067E4E59DF4450D1D47B27E945170D21039935BEDECC`; `data/mod-updater.sh` 2,187 bytes / `B408E37E924F71F0A19403B561672C598D6AD7F9B8B29886A303145C91621A88`; `libredbird_thread_patch.so` 18,432 bytes / `B0321E5621EE1AEE8CB07C5579CB5D21A30FB8CA68B3553450EC968374384BA3`. Manifest version is `2.0.2`.
- The game installation contains no `BepInEx/plugins/LinuxModding_Serp` directory. Active helper/release sources contain none of `LaunchUpdaterAndExit`, `LinuxWorkshopUpdaterBridge`, MonoMod detours, request/staging/delete/restart loops, LinuxModding DLL, DetourProbe, or old plugin-directory routing. All 14 tracked deletions are absent from the filesystem; `git diff --check` passes.
- Windows batch/JSON/plan/status files are UTF-8 without BOM and CRLF-only. The four intentionally Linux-targeted shell files are UTF-8 without BOM and LF-only, with no CRLF or literal `\\r\\n` corruption. Git's Windows autocrlf warning for these four working-tree files is expected; their verified current bytes remain LF-only.

Build and handoff:

- No Runtime mod or buildable Linux project remains, so P8 correctly has no `build.bat` build/install step. The standalone helper is covered by the four shell tests; a real Proton launch/update is optional and nonblocking.
- No mandatory P8 work remains. P9 must apply the user's explicit decision that MoveMoatTest is not part of this migration to the plan's scope, inventories, matrix, prerequisites, and Definition of Done before beginning the workspace-wide final audit.

### P9 - Workspace-wide final acceptance

Status: `abgeschlossen`

Scope decision and fresh-session boundary:

- The user's explicit 2026-09-05 decision permanently excludes `MoveMoatTest` from this migration. The plan now records P3M as `entfällt` and consistently updates scope, inventories, matrix, P9 prerequisite, slices, and Definition of Done. `MoveMoatTest`, its tests, documentation, build artifacts, installation, and version remain untouched and will not be built or tested.
- Of the plan's former 52 current project files, P8 removed the Linux runtime project and DetourProbe. Fifty current project files remain in the planned workspace inventory; one is the excluded MoveMoatTest, leaving 49 in-scope projects: 33 directly SHCDESE-related and 16 indirect projects.
- P9 starts only after this scope reconciliation. Its audit uses the current repository state, AGENTS.md, the adjusted plan, and this status file; prior package evidence is accepted where no later production change invalidated it.

Final inventory and corrections:

- `rg --files` confirms exactly 50 current non-`_inspect`, non-Extender project files from the planned workspace set. `MoveMoatTest.csproj` is the one expressly excluded current project, leaving exactly 49 in scope. The two P8 Linux projects and the three earlier historical Runtime projects remain absent.
- P9 found one previously missed manifest: `BugfixesAndQoL/CustomLordExtendedPackageTemplate/info.json`. Because that asset template contains gameplay scripts, it now declares fail-closed `NetworkMode: 1`. All 46 active source/package manifest copies outside the excluded MoveMoat and retired Linux trees parse successfully and declare an explicit mode.
- The common settings audit found that 19 SerpsModsHost fallback locales lacked six lobby-inventory keys present in English and German. Those files already intentionally use English fallback text; the six existing English values were added unchanged. All 21 locale files now contain the same 30 nonempty keys and use CRLF. No README was changed.
- Current production source/project searches outside MoveMoat contain zero Zhuqiaomon references, PolyHook/PolyHook2 detours, HookRef, old trampoline access, or `HookTransaction.Unload()` calls. The two remaining `NativeDetour` classes in RandomEvents are the separately owned MonoMod.RuntimeDetour implementations, not the forbidden PolyHook2 API. All 49 project XML files parse; the only private SHCDESE references are in three executable test projects that require the assembly at test runtime, never in a packaged Runtime mod.
- Exactly 27 Runtime plugin sources declare the hard minimum `[BepInDependency(..., "2.0.2")]`; no old 1.42/1.43/1.44/1.45/2.0.0/2.0.1 dependency remains. All 27 active source versions match every corresponding source/package manifest version, with no version change made by P9. Package trees contain no private Zhuqiaomon, PolyHook, RedBird, or Microsoft.Extensions.Logging assembly.

Final automated and static verification:

- The current project matrix has build output evidence for 49/49 in-scope projects. Runtime and dependency builds are represented by their completed P0-P8 package builds; all indirect test/tool projects have current executable/library output and their applicable tests were rerun. No unchanged Runtime mod was rebuilt merely for P9.
- Final project-chain runs passed: AIVParser 38/38; AIVPlacement 29/29; CastlePlanner placement 57/57; CustomCustomTrail 40/40; MapParser 36/36; QueueTest 392 checks; TrailEditor 9/9; ImprovedMoatFilling 27 checks; EnemyGatePathfinding 206 assertions; ExtremePowers API; and OxTether policy 75 assertions. CustomCustomTrail first produced only the known two sandbox-denied atomic replace cases (38/40) and then passed 40/40 unchanged through the required elevated path. EnemyGatePathfinding first ran from the workspace root and could not resolve its source-relative path; the same executable passed from its required project working directory.
- The mandatory common regressions passed: HostClientPresetTests reports the complete 2.0.2 routing/authority/game-mode/Trail/client-lock/preset/MessagePack contract; LobbyModSettingsPresetTests validated eight installed legacy MessagePack files in memory and passed; all 14 shared settings audits pass XAML, shared registration, personal-setting declarations, two-axis scrolling, tooltips, styles, locale parity/translations, and CRLF.
- Final focused harnesses passed: AIDefense 27; Better-AI overbuild; Bugfixes AIV memory 5, AIV sync 18, map-origin, Native 918/61 signatures, random-AI, and startup diagnostics; CustomLordUpload 15; EngineerSiegeFix 773; ExtraFeatures Native 270; ImprovedHunters 177; Knight packets 31; MultiplayerLeave 17; P3a 60; RandomEvents 86; Recruitment 53; SerpNativeAPI; SerpsModsHost diagnostics/hash/serialization; Stockpile 1123; and VanillaMapEditor policy.
- Two automatically discovered `_inspect` executables are explicitly outside the 49-project plan matrix and are not migration gates: `HealerAttackCommandFixTestTests` targets an already absent top-level source mod and therefore cannot locate its workspace; the historical `_inspect/OxTetherIdleFixTestTests` still asserts diagnostic/fault-injection source contracts removed before P3b, exactly as P3b already documented. The plan-listed `OxTetherIdleFixTest/tests` suite is authoritative and passed all 75 assertions.
- All 46 active manifests parse with explicit NetworkMode. All 49 project XML files parse. `git diff --check` passes; its only messages are expected Git autocrlf warnings for the four explicitly Linux-targeted LF shell scripts.
- A changed-text audit decoded 488 in-scope changed/untracked text files as strict UTF-8 and found no naked LF. Two naked-LF XAML copies under `_inspect/SHCDESE-2.0.2-release` are immutable extracted/backup evidence from the official archive and were deliberately not rewritten. The four Linux helper/test scripts remain intentionally LF-only; plan/status literal `\\r\\n` mentions are documentation rather than corruption.

Build, installation, and canonical hashes:

- The P9 SerpsModsHost locale correction was checked before the prescribed single `SerpsModsHost/build.bat /nopause` run. MSBuild completed with 0 warnings and 0 errors and installed successfully. All 27 local SerpsModsHost package files are byte-identical to the installed package. Principal hashes: DLL 273,920 bytes / `B73D83C6739283101D6B80CCC3B1C5AEC88A058CA89DED51A44B650DF2FBBC6E`; PDB 72,592 bytes / `033E35470966B83F62CB309E9D19FECB9D0BB5B36B86BAF85D4C9554BD1B98BA`; manifest 3,989 bytes / `1170D00DAD18120B6500DC059B5FBB4C1ACD3BBFABECAC996CBDECE4A09B65AE`.
- The full installed comparison covers all 27 migrated mod packages and 1,402 packaged files: every local file has an installed counterpart with the same SHA-256; zero mismatches remain.
- The canonical Extender fork is clean at commit `6dc82d1d92b0935abc93cd43ac16cd8ddccc5f79`, exact tag `v2.0.2`. Local build output, local packaged output, and installed `SHCDESE.dll` are each 1,408,000 bytes, assembly version `2.0.2.0`, SHA-256 `D657BF158C66EC5A439CE8ED6463A96F868E4059ED410C16A4373C2D240F3886`.
- Installed `CrusaderDE.dll` SHA-256 is `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` and matches `CURRENT.json.currentNativeHash` plus the semantic database manifest identity. An initial audit expression queried the nonexistent property name `binaryHash` from CURRENT.json and therefore reported a false mismatch; the corrected documented `currentNativeHash` comparison is true.

Definition-of-Done result and open points:

- Every mandatory Definition-of-Done item in the adjusted plan is satisfied. P0-P9 are complete; P3M is `entfällt` by explicit user decision. MoveMoatTest and its user changes were neither read for migration, changed, built, installed, nor tested.
- The SHCDESE 2.0.2 migration is complete for all 49 projects in scope. No mandatory implementation, automated test, build, installation, manifest, dependency, native-baseline, package, or documentation item remains.
- Actual game, Host/Client, and Proton sessions remain optional post-migration acceptance under the user's explicit waiver. They are the only optional open evidence and do not block completion.

### Post-acceptance review - package symbol consistency

Status: `abgeschlossen`

- An independent post-migration code review found no confirmed runtime or conversion defect. It rechecked the public SHCDESE 2.0.2 contracts for RedBird transactions, original calls, context ownership, selected-unit IDs, signed gold, Chore delivery, multiplayer sender identity, join synchronization, and the still-broken public tribe-unassign wrapper. Focused reruns passed: P3a 60 assertions, AIDefense 27, RandomEvents 86, BugfixesAndQoL 918 assertions/61 signatures, ExtraFeatures 270, and ImprovedHunters 177 assertions/29 native pattern definitions.
- Portable-PDB source checks found that `CustomCustomTrail`, `ExtremePowers`, and `RandomEvents` still referenced an earlier textual revision of `Shared/PresetLobbyModSettingsViewModel.cs`. Decompilation proved all three embedded implementations semantically identical to a current artifact, so this was a symbol/source consistency issue rather than a runtime defect.
- Before rebuilding, 333 affected text files were checked and contained zero naked LF. The prescribed `build.bat /nopause` was then run once for each affected mod. CustomCustomTrail passed all 40 internal tests; all three Runtime builds completed with 0 warnings and 0 errors and installed successfully.
- The rebuilt PDBs now match all 77 source documents used by those three builds. The workspace-wide authoritative Runtime-package check now covers 27 PDBs and 610 Workspace source documents with zero checksum mismatches.
- Local package and installed game copies are byte-identical for all nine principal files (DLL, PDB, and `info.json` for each mod). New hashes: CustomCustomTrail DLL `357F4B20CCFFE0BABEE435B5A6425243EE7CF0CAC430016124D782B46296F8FD`, PDB `5CE029216B265DD58BF8D35055AB8134E4B4070E22FD9754AAACE0C7F3DE45E6`; ExtremePowers DLL `58CB352D71F78EA5E77942BFFED9B2571BF1D492CA29E12085113934F89853E3`, PDB `D209A6FFF9DCFC1D7A5BA2C3FF5F6D811312A0DE25F493BBE1D1EA8794BEBAF2`; RandomEvents DLL `0877B7A1E30E99EF68459A8ADBF12EDC99D8087C0D2A26C48BA4B6B4C8B6A712`, PDB `1ACDC7C2916D3B761F2FAE09CF1DC15B5827D2315DBAC21B366B9DD257162E5D`.
- `git diff --check` remains clean apart from the expected autocrlf notices for the four intentionally LF-only Linux scripts. No version was changed, no README was edited, and MoveMoatTest remained excluded and untouched.
