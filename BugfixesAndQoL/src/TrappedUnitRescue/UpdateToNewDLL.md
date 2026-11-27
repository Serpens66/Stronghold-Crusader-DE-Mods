# BQoL trapped-unit rescue: native update contract

Reference image: canonical installed `CrusaderDE.dll`, SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. Integration snapshot: 27 November 2026. Resolve subsequent baselines through the workspace `CURRENT.json`; do not infer compatibility from a mod version.

## Complete inherited inventory

The [original address, field, call-chain and worker-guard inventory](../../../Testmods/TrappedUnitRescueTest/UpdateToNewDLL.md) and its linked baseline audits remain the detailed native contract for these migrated core sources. The acceptance directory retains that evidence after its prototype runtime is removed. All unchanged addresses and layouts in that inventory remain hash-bound: unit stride/bias and identity, Current/Next/AI/order/crew/worker fields; tile grids and bounds; native movement, occupancy and work-state readers. No semantic fallback is claimed for an unknown layout. A matching function prologue alone cannot qualify those dependent records.

The production adapter checks the actual loaded canonical module hash through the existing shared file-hash cache, validates typed offsets, and validates reference entry bytes before publishing the consumer. An unavailable contract leaves rescue inactive with an Error; it does not install an alternate native owner.

## Production ownership changes

- `0x19BA40` is the five-argument `(unitManager, oneBasedId, tileX, tileY, height)` relocation entry. Rescue now requires the already published `BugfixesAndQoL_Serp` / `Lord.HomeTeleportGuard`, exactly sixteen bytes, and its verified live patch/stub prefix. It registers only `TrappedUnitRescue.RelocationCall` as CallOnly. A pristine unowned entry is deliberately rejected; no second detour or trampoline is installed. The Lord guard remains authoritative for its own feature.
- `0x196280` remains the existing single-unit movement pipeline, including the Script Extender, APIShared and Pathfinding participants. Current identity/order intent and the resulting physical state qualify continuation; a managed callback alone is not proof of successful movement.
- `FixesIntegration` uses the production owner GUID and relevant managed IL/helper contracts. Its installed worker-stub validation and logically gated suppression retain the detailed inventory above. Unknown relevant upstream changes disable the replacement rather than accepting a whole-DLL version/hash assumption. The mounted Keep rescue and unrelated Fixes branches are excluded from this suppression.
- The original save-participant registration is replaced by `BugfixesAndQoLTrappedUnitRescue` under the production owner. Prototype-owner archives are not silently imported.

## Removable mounted Keep correction

`FixesMountedScanner*.cs` is a separate compatibility island. Its corrected call target is **`0x187110`**, the two-argument manager/id helper, not the five-argument relocation entry above. Its entire 227-byte function, image identity, mapped executable span and live bytes are validated. The managed installer, deferred generator, emitter and relevant helpers are independently checked by structural contracts. A future scalar-address upstream implementation is recognized without installing this correction; unknown relevant changes are left unchanged.

Two permanent managed `SafeIlHook` registrations change only address capture and one CALL emission in the existing four Fixes native stubs. No additional native game hook owner is introduced. The emitter preserves registers, incoming flags and the original stack position before either call. The corrected immutable target and the original scanner's address as actually read at deferred emission remain separate. A process-lifetime activation cell selects between them; OFF executes the original Fixes call, without changing foreign settings or rewriting published code.

At every mission/editor start, `RecheckPublishedContracts()` compares only the already qualified 227 live bytes and memory span. An overlap/read failure logs one Error and permanently clears the correction gate. It neither scans nor rehashes the DLL, repairs code, nor reactivates after later byte restoration. Existing Fixes native-stub guards retain ownership of their spans.

## Activation and update verification

One Host setting, `EnableTrappedUnitRescue`, defaults ON. Both rescue and the mounted correction require `EnableMod && EnableTrappedUnitRescue` and a qualified SP/editor session; a local Client master does not gate Host simulation. Before session activation and after session end the mounted cell is zero. OFF performs no native historical-worker cleanup; archive ownership data is retained. Published hooks remain rooted and are never removed for a settings/map change.

Before an image/upstream update is accepted, re-audit each changed field/call/helper and all relevant producers/consumers, rerun the real-source kernel/IL/native-dispatch tests, and perform the separate production acceptance scenarios: source construction, group/crew preservation, civilian removal, pending save/load, new/loaded editor, master/option OFF, and the mounted type/Keep/rotation matrix. CastlePlanner can change Keep orientation, so verify actual campfire/exit geometry rather than assuming its initial orientation. An offline pass is not a game acceptance.
