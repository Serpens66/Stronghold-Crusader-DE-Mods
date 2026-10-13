# Drawbridge destruction test: native update contract

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Audited Extender source commit/tree: 5908e1f12437deb7ef5f5b1dc2d64e301178fba3 / c238441ea12db35ee36c5c5668d3c8fce0897295. Runtime references installed assemblies; no Extender source modification.

## Targets and derivation

Feature owner: DrawbridgeDestructionFixTest_Serp. Native image base is obtained from LibraryLoaded, never hard-coded at runtime.

| Target | Reference RVA | Contract |
| --- | --- | --- |
| Shared damage handler | 0x7EB00, size 0xD8A | Full feature audit includes all recorded callers, non-building terrain damage, immunity/alliance exits, signed-16 HP subtraction, early bridge deletion, survivor return and terminal destruction. |
| Decision hook | 0x7EE6B | cmp r13d,1 (4 bytes), jne 0x7EF87 (6), sub r8w,0x2D (5). Actual RedBird X64InlineHook displacement must be 15 before and after installation. |
| Trampoline continuation | 0x7EE7A | First following instruction cmp r8w,r13w; its ja goes to 0x7EF87. |
| Skip target | 0x7EF87 | Native type reload followed by unchanged attack notification and lethal checks. Derived from the validated displaced JNE; runtime hash binds the fixed RVA. |
| Post-hit health store | 0x7EE62 | Signed Int16 at manager + buildingId*0x32C + 0x168. |
| Later lethal check | 0x7F018 / 0x7F024 | Signed HP reload; jg bypasses ordinary destruction for surviving buildings. |

Semantic signature at the hook: `41 83 FD 01 0F 85 12 01 00 00 66 41 83 E8 2D 66 45 3B C5 0F 87 03 01 00 00`.
Search first at the reference RVA, then uniquely across executable PE sections even after reference-byte failure. Missing or ambiguous patterns abort the feature. Resolved address must remain the audited RVA for this test version.

The known native hash is mandatory because the manager header, stride, fields and complete incoming-edge audit have no independent update-safe structural fallback. Unknown DLLs are rejected without patching. The live function body from entry+64 through its audited end must match the reference file; the first 64 bytes allow Extender's existing function-entry detour. No concurrent interior hook is accepted.

## Machine state and permanent lifetime

R15 is the building manager, RBP is the one-based ID multiplied by native stride, R13D is argument 8. GameBuilding is the public view beginning at slot+0x5C; r_BuildingType+0xD2 maps to slot+0x12E and r_CurrentHealth+0x10C to slot+0x168. Initialization validates these mappings and named enum values against the actual referenced assembly.

The generated guard saves RAX and flags, touches no SIMD state, uses no managed callback, and restores stack/RAX/flags before either replaying all displaced instructions or skipping early removal. All other registers remain untouched. The skip path leaves the original R8 intact; Vanilla reloads it immediately at 0x7EF87. No original flags are consumed before Vanilla recalculates them there. State words are enabled/entered/suppressed; only enabled is published atomically from managed initialization. Counters are observations of the serialized simulation path, not synchronization primitives for foreign threads.

Hook, state memory, logger and subscriptions remain rooted until process exit. Executable bytes are installed once. Rollback can only occur during failed initialization before publication, with logical activation still zero. Normal map/plugin lifecycle has no hook teardown.

Diagnostic revision 2 binds APIShared's IMissionLifecycleCapability through ApiShared.ForMod with its own GUID and registration ID drawbridge-damage-diagnostics. Its process-lifetime observer arms both editor routes and regular saves/new games on managed-ready Start and retires only a matching active End. Registration can replay before returning; after successful registration, Current covers that same replay with SessionId deduplication. Failed initialization candidates leave permanent observer registrations inert through diagnosticsReady=false; rollback never unregisters a published observer. Existing OnTick remains the durable simulation publisher. No new native target, fixed data offset or private Assembly-CSharp access is added.

## Compatibility and update checklist

Extender owns 0x7EB00 at its function entry; this test owns only 0x7EE6B..0x7EE79. The existing damage event remains authoritative and executes Vanilla exactly once. Its Post arguments contain original inputs, not final effective damage/result; diagnostics use observed HP and identity instead. Local Fixes building hooks cover other gatehouse handling sites, not this span. Runtime body validation rejects unreviewed overlaps.

On native/Extender/RedBird updates, repeat the entire feature audit, installed enum/layout checks, full caller parameter flow, incoming xrefs/jump-table review and real backend displacement/generator tests. Hash unchanged does not prove updated Extender contracts.

The private native fixture executes the same production generator and compares disabled, non-target and lethal paths against original instructions. It does not execute the entire building removal handler or establish multiplayer correctness. In-game acceptance remains required.
