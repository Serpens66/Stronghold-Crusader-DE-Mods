# Lord Teleport Guard Test native contract

Reference: installed CrusaderDE.dll SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Audit observation: 2026-10-28. Image base below is the preferred `0x180000000`, never a live fixed VA.

## Ownership and activation

This testmod owns only the permanent inline entry `[0x19BA40,0x19BA50)`.
APIShared.NativeSites group `Lord.HomeTeleportGuard` claims it exclusively before installation.
The unit-manager and lineup anchors and the standing-rescue CALL are registered as Observe. No APIShared/SE/Fixes source is changed.
Installed RedBird.X64 1.5.0 X64InlineHook decodes at least 14 bytes, emits the supplied generator,
then appends `FF 25 00000000` and the eight-byte continuation address. Here its actual displacement
is exactly 16 bytes. The prepared backend stub and installed patch are compared byte-for-byte.

Original instructions and lengths: `mov [rsp+18h],rbp` (5), `push rsi` (1), `push rdi` (1),
`push r14` (2), `sub rsp,20h` (4), `movsxd rsi,r8d` (3). Resume at `0x19BA50` is `mov r14,rcx`.
Direct-flow scan of all installed PE `.pdata` functions and existing semantic Xrefs found no target
inside the span; raw absolute-pointer scan found no pointer to an interior byte.
No neighboring foreign hook is bypassed: the normal branch resumes at the live continuation.

The hook is installed in LibraryLoaded with the logical flag initially zero, then published with flag one.
Its runtime, hook, flag and optional trace storage are process-rooted. There is no OnDestroy/OnDisable,
map-change disposal, live unpatch, setting or delayed installation. Only unpublished failure candidates
can roll back. A failed rollback retains storage and ownership with flag zero.

## Function and state flow

`0x19BA40(manager, oneBasedUnitId, signedTileX, signedTileY, height)` is a void relocation helper
(Windows x64 ABI: RCX/EDX/R8D/R9D, fifth argument on caller stack). It removes old occupancy through
`0x186AD0`, resets movement fields, writes next/current tile coordinates and fine coordinates `tile*8+4`,
height and compact tile ID, then adds occupancy through `0x180230` and refreshes view offsets `0x18BE50`.

Lord update `0x15A950`, CALL `0x15ABD1`, invokes it after `0x196280` returns zero in state 1 with
the new-route flag set. Return address `0x15ABD6`; Vanilla then clears the route flag and normally enters
state `0x6D`, resume state 2. Unit dispatcher `0x182B00`, CALL `0x18300F`, is the second rescue:
the current compact tile has component zero, lacks tile bit `0x40000000`, passes its building/special-unit
gates, is not state `0x6E`, and the unit age is at least 1000 ticks. It checks one of sixteen unit-ID buckets
per pass. It relocates Lords home and then sets state 1/new-route flag zero. Return address `0x183014`.

Important trigger correction: closing a gate and waiting is insufficient. In native nonzero game mode,
a human Lord finishing an ordinary move goes to state 13 and its normal idle handler does not automatically
leave that state. The building-attack state `0x6B` has target-loss/completion branches returning to state 0
(or state 1 for native zero mode), which reaches the return-path attempt. The user confirmed the video
shows a teleport after destruction of a building. Full user gameplay reproduction remains separate.

The standing-tile rescue is deliberately preserved for all players. The native stub compares the original
return address with image+0x183014 before inspecting unit data and runs Vanilla for this caller. Four
saved stack slots (flags/RAX/R10/R11) put that address at [rsp+32]. The five-byte CALL at 0x18300F is
validated against the captured image and registered as Observe. No caller or terrain code is patched.

For all other callers the patch suppresses only a live Lord with controlling player 1..8, lineup entry zero (SK_NULL), and both
requested coordinates equal to its stored home coordinates. It does not use local selection, local-player
identity, a managed roster, AI portrait or wall/tile searches. It changes no caller state or route result.
The blocked exit restores scratch registers and incoming flags before RET; the pass-through exit restores
them before replaying the original prologue and its normal flag effects. No XMM instruction or managed call
exists in the production predicate. The optional diagnostic path is compile-time-only and bounded to 32 rows.

## Address and layout map

| Contract | Reference RVA / offset | Evidence and update policy |
|---|---|---|
| Relocation entry | `0x19BA40`, resume `0x19BA50` | 31-byte exact semantic entry signature; executable PE sections only. Known hash checks reference bytes first, then unique signature fallback. A moved entry is refused because the rest of this prototype is hash-bound. |
| Standing rescue exemption | CALL `0x18300F`, return `0x183014` | Decode the five-byte direct CALL in captured memory, require relocation entry as target and the exact continuation; derive native comparison from validated image base. A changed contract refuses installation. |
| Unit manager | `0x67E8400` | RIP-relative LEA RBP at `0x15AB83`; independently CDE60 caller and native relocation member accesses. Runtime decodes the seven-byte anchor. |
| AI lineup | `0x8574C44` | RIP-relative LEA RAX at `0x28A7D`; same table as SE GamePlayerManagerAPI.GetAILord/IsAIPlayer. Int32 slots 0..8; zero is human/non-AI. Runtime decodes the anchor. |
| Unit addressing | `manager + id*0x490 + 0x65C` | Reserved slot zero, live IDs 1..9999; native loop bounds and installed GameUnitManager constants/layout. No `id-1` for this manager-relative formula. |
| Life/type/control/global | GameUnit `+0x88/+0x8A/+0x92/+0x94` | Manager working offsets `0x6E4/0x6E6/0x6EE/0x6F0`; layout checked against installed SHCDESE.dll. |
| Home X/Y | GameUnit `+0x336/+0x338` | Working offsets `0x992/0x994`; periodically written by `0xC23C0` from Keep position (Y-1 except Keep type `0x28`). |
| Current X/Y/height | GameUnit `+0xC0/+0xC2/+0xB6` | Working offsets `0x71C/0x71E/0x712`; actual native relocation writer and installed layout. |
| AI state | GameUnit `+0x2BC` | Working offset `0x918`; diagnostic only; caller writes checked by executed fixture. |

All current direct/derived RVAs used by runtime code are listed above. The standing-rescue CALL/return
is a validated gameplay exemption; other CALL/return addresses are diagnostic labels, not hook targets. Test-only native fixture references are the documented callees and caller blocks,
map validity `0x3A11EA4`, row-offset table `0x402FF2C`, tile height `0x4DDD350`, current-unit context `0x9302C4`,
route builder `0x196280`, completion query `0x188340`, Lord exit `0x15D3C5` and dispatcher continuation `0x183041`.
These fixture addresses never address the game process and are bound to the full reference file hash.

No data/layout fallback is claimed on a new hash: interop compatibility does not prove native storage or
player-table semantics. Unsupported hash, anchor, bytes, span or ownership disables only this feature with
LORD_TELEPORT_GUARD_UNAVAILABLE; no partial patch is published. Re-audit all writers/readers and both callers
before changing the hash, including zero/nonzero mode and human idle behavior. Do not merely update a pin.

## Build and evidence

`build.bat /source:working /nopause /devdiag` builds existing working sources, normal and diagnostic native
tests and artifacts, using the verified local APIShared SDK only if its DLL matches the installed DLL.
It never rebuilds or installs dependencies. `/noinstall` leaves a verified package in the workspace.
Outputs and logs use fresh WorkPaths allocations under `.work/a/lord-build/`; normal and DEVDIAG artifacts
are tested separately, including the actual release-rejection metadata. Only DLL/PDB/info.json are installed.

Tests execute the actual production emitter through the installed RedBird backend on a private mapped PE.
They run the unchanged two caller blocks with isolated path-result and occupancy/presentation boundaries,
then check native position/height writers and subsequent states: human return-path failures are blocked,
while invalid-standing-tile rescue is passed through for every human slot as well as AI. Repeated mixed
requests verify that allowed rescues neither increment blocked counters nor consume trace capacity. These are offline machine-code tests,
not claims of Unity/gameplay/editor or two-peer multiplayer acceptance. Removing the installed testmod
after closing the game restores Vanilla on the next process start; a live hook is never removed.
