# OutpostTest native contract

Reference Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Reference image base: `0x180000000`. Installed Extender 2.6.0, source commit `2cee24e33b5a5d81d1c275efabc714ac59917b7b`, tree `9cfb59b7b531553b23709b90b3cc3f10b0615cc1`. Installed RedBird.X64 FileVersion 1.1.0.0 was decompiled for this audit.

This first test is intentionally hash-bound. Do not enable it on another DLL merely because an entry pattern matches. Unknown fixed data layouts have no semantic fallback. On validation failure leave the gate inactive and do not produce custom units. Never patch the Script Extender fork.

## Code targets

| Owner / RVA | Contract and derivation | Validation / fallback |
|---|---|---|
| Production gate `0xABC78` | Shared update `0xABB90` for building types 2/106/107, dispatch table `0x2DEAE0`. Gate after manager registration and animation. Original bytes `44 39 1D A9 A2 5B 03 0F 84 56 11 00 00 45 85 FF`: CMP(7), JE(6), TEST(3). | Exact hash and bytes; executable-section unique-pattern fallback after failed RVA comparison; resolved RVA must remain reference RVA because following layouts are fixed. |
| Continuation `0xABC88` | First instruction after 16 displaced bytes; JE consumes TEST flags. | Generated from validated instruction boundary. Disabled path re-emits originals with RIP-relative relocation. |
| Early epilogue `0xACDDB` | `48 83 C4 60 41 5F 41 5D 41 5C 5E 5B C3`, RET at `0xACDE7`. RBP/RDI/R14 have not yet been spilled at gate. | Exact bytes plus executable pattern resolution. Active path preserves all registers, incoming flags and stack with PUSHFQ/PUSH RAX, balanced POPs. No managed call and no SIMD instructions. |
| Allocator `0x119D60` | `(tribeManager*, int owner) -> int tribeId`; dedicated outpost pool, descending by 8 from 4500-owner. Zero on exhaustion. Sets tribe alive=2, owner and global ID. | Exact first 25 bytes stored in source; executable unique fallback, hash-bound. Distinct from public generic Create allocator. |
| Handoff `0x2E2B0` | `(aiManager*, int tribeId, uint globalId) -> void`; empty group alive=3, human-owner branch returns, AI queues ID/global then calls `0x2A720`. | Exact first 25 bytes stored in source; executable unique fallback, hash-bound. Queue full is a native void-return limitation; log says called, not guaranteed accepted. |
| Extender-owned calls `0x17FEF0`, `0x11D370`, `0x11B520` | CreateUnitLocal (scales tile coords ×8), AssignUnit public arguments `(tribeId,unitId)` → native `(manager,unitId,tribeId)`, IssueMoveHereCommand exit `(x,y,false,0,NoChange)` | Use public APIs; no second detour. Re-audit after Extender native-binding changes. |

Full ABB90 disassembly and baseline xrefs were checked: external direct edge `0xABC6E -> 0xABC78` enters at the gate start, no edge targets its interior. Jump-table destinations at `0xACDE8`/`0xACDFC` are outside the span. Runtime additionally decodes the full `0xABB90..0xACDE8` code for interior branch targets. Probe the installed RedBird decoder before commit and verify actual `DisplacedByteCount=16` before activation; rollback unpublished initialization on mismatch. Published hooks, flag memory and callbacks remain rooted until process exit. Normal mission end only zeros the gate and drops managed schedules.

Installed `AddUnrestrictedJmp` emits `FF 25 00 00 00 00` plus an eight-byte target literal. The test decoder parses those literals as data and checks that generated branches do not enter a literal or instruction interior. Treating these ASLR-dependent address bytes as code caused a test-only false failure and was corrected before installation acceptance.

The bypass skips the entire post-animation production section, including the initial vicinity-role scan and native production timers, not only Create calls. Existing units are not deleted. The custom units receive the same role 50 explicitly. Native building dispatch, animation/registration prefix, general deletion and tile handling remain active. Existing unfinished production tribes are handed off once and their building references cleared.

## Data targets and field origins

All listed data RVAs are module-relative and hash-bound. Runtime structure sizes/known offsets and array pointer bases are checked before writing. No pattern match alone authorizes a changed data layout.

| RVA / offset | Meaning |
|---|---|
| `0x7CC6720` | Tribe manager; cross-check Extender GetTribeManager pointer. GameTribe pointer = base + `0x2A + id*0x688`. |
| `0x404C950` | AI manager; derived from ABB90 handoff LEA at `0xACD75`, also prefix registration. |
| `0x64CCBB0` | Building manager; GameBuilding pointer = base + `0x5C + id*0x32C`. Managed first entry is base+0x388. |
| `0x67E8400` | Unit manager; GameUnit pointer = base + `0x65C + id*0x490`. |
| GameBuilding `0xD2/D6/D8/FE/100` | Type, owner, global ID, exit tile X/Y. All Game IDs one-based; spans zero-based. |
| GameBuilding `0x302/304` | In-progress tribe ID(short)/global(uint). Validate identity before handoff; then clear reference. |
| GameTribe `0xA/32/60A/652` | Global ID, member count, stance(ushort=2), profile role(short=184). Global-ID comment in native managed source is not a substitute for measured Marshal offset. |
| GameUnit `0x88/94/AC/426` | Alive, global ID, recruitment-rally flag(ushort=0), AI role(short=50). Origin: native slot-relative `0x708` and `0xA82` minus struct prefix `0x65C`. |
| `0x3665F28` | Native mode; 1 is editor. |
| `0x8574B90` | Native mode controlling outpost production conditions and cap exceptions (0,99). |
| `0x37EF974`, `0x38722DC` | Existing production blockers (int and byte); require zero outside modes 0/99. Derived from `0xABC90/ABC9D`. |
| `0x379B30C + owner*0x583C`, `0x379E6D4 + owner*0x583C` | Two native player unit-count inputs from `0xACB12/ACB1A`; sum before cap check. Account additionally for this mod's successful allocations in the current tick. |
| `0x8574BCC + owner*4` | Player category; -1 selects first cap. |
| `0x37EF950/954` | Selected cap, compare count >= limit; derived from `0xACB2F/ACB37`. |
| Native internal pool cap `0x3668E34` | Read by native Create; retained via public API, never overridden by mod. |

Only valid owners 1..8 are produced for; neutral/unassigned records do not have a proven AI handoff contract. A native pool failure returns zero. No retry backlog. Macemen need no siege crew. New units retain NeedsInit until Vanilla advances them; do not force Alive=2. Native move may return false for initial units just as in the original synchronous spawn path; followup logs expose subsequent states rather than forcing an invented movement state.

## Validation and live acceptance

The build driver runs source JSON/lifecycle and CRLF preflight, then tests on installed layouts/hash, scheduler identity/limits, all gate branches and the installed RedBird length decoder. The actual production generator is assembled, decoded and executed against synthetic memory outside the game; editor/ordinary branches, enabled bypass, preserved RAX, TEST flags and stack balance are checked. These are not live game tests.

Game acceptance: all three outpost types for human and AI owners; five actual type-26 units per 200 ticks without vanilla guards; normal controllability and AI queue use; pause/speed; multiple outposts, cap exhaustion and partial waves; demolition/slot reuse/owner change; save load resets interval; editor and multiplayer unchanged. Markers: `gate installed inactive`, `session ... active`, `tracking`, `hook confirmed`, `wave`, `followup`. Logs use millisecond timestamps. No map/ownership inference from a stale slot or an unverified spawn-event argument.

No custom UI, no persistence schema, no runtime JSON, no changes to existing README files. Package metadata is build-time JSON only. Start version 0.1.0 remains a test version.

## Incremental Macemen production (2026-09-17)

The passive Vanilla observer is removed; the original validated 16-byte gate is active again. No new native function detours/delegates were added. Runtime now uses building fields 302/304 (tribe identity), 308 (shared signed short counter), 30A (target), 30C (profile=2), 30E (size), 310 (delay), 316/318 (accelerations). Check them against ABB90 on updates and test save/load and deletion. Keep the 106/107 vs 2 deletion-handoff distinction from B8310; clearing a matching link before native cleanup avoids double handoff.

Profile 2 at 2DD880+2*52: 250/100 interval bounds, 10+RNG%10 members times size+1, sole type26, role184. AC4C5..AC52F checks group wait max(2000-acceleration, native minima); AC871..AC915 computes interval and resets counter to RNG%40. AC939..AC962 holds the last unit while delay>0; ACA8A..ACAD7 creates target/2 units for an empty delayed AI group. ACD51..ACD7E checks members>=target and delay<=0 before handoff. World acceleration predicate reads int RVA3668E34>3000 and int RVA3669048<11; AI branch reads int RVA379D0D0+owner*583C !=0. These are exact predicates, not claims about undocumented field names.

The replacement uses the same arithmetic and batches but independent deterministic random draws (global ID/tick/salt), no global Vanilla RNG adapter. Saturating accelerations and stricter per-unit limit checks deliberately avoid counter wrap and limit overshoot. Tests verify profile bytes, group/interval minima, delay behavior, partial allocations, hook/control-flow and installed RedBird assembly/decode/execute contracts. Existing native signatures remain scoped to the verified hash.

## Human rally integration

Human owners now branch to singleton groups via public Create/AssignUnit/SetStance APIs, with separate saved production counts. Verify the installed Create relative-call target (0x11E17C -> 0x119C00) and Alive=2 initializer on updates. No direct unassignment adapter was introduced.

Native RunTo uses 0x196100 as a six-argument void Win64 delegate: manager, tribe ID, tile X/Y, patrol=0, flags=0x81. It deliberately calls through other installed detours rather than bypassing them or requiring unmodified live entry bytes. The canonical file hash is the authorization boundary, with offline byte tests for its entry and BTR bit7. Review 0x11B520, 0x19B260 and later command writers if the native build changes. Public MoveHere callbacks provide a synchronous result only when the command is not deferred by another mod.

New binary save schema: magic 0x4F505254, version 1, bounded building records and pending singleton orders. Uses BinaryReader/BinaryWriter through ModSaveDataAPI; its archive suffix does not imply MessagePack payload. The empty header/count payload is intentional. Preserve target snapshots, building/owner identity and native unit/tribe global IDs. The state codec rejects unknown versions, duplicate entries, invalid counts/IDs/coordinates and trailing/truncated data before publication.

Managed projection/input contracts are tied to Assembly-CSharp SHA256 BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789: Mouse2 through KeyManager events; MainControls GUI/off-world guards; GameMap coordinate rotation and testHeight. Verify flag placement in-game after Unity/managed updates.

## Native building selection (2026-09-17)

Same SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Optional selection gate at RVA 0x8A1F9, length 18, return 0x8A20B, rejected selection 0x89FF2. Instructions: CMP editor mode [RIP -> 0x3665F28],1 (7 bytes); JNE 0x89FF2 (6); MOV EBX,45 (5). The next instruction is JMP 0x8A6E0. The earlier owner/restriction branch and editor exception at 0x8A051 are untouched. All 2/106/107 main switch entries target 0x8A1F9. The enabled pure-assembly path preserves RAX, stack and flags, sets EBX=45, and rejoins the common Vanilla path. No callback ABI or XMM clobber.

Resolve uniquely in the current module using reference RVA and exact semantic instruction pattern `83 3D 28 BD 5D 03 01 0F 85 EC FD FF FF BB 2D 00 00 00 E9 D0 04 00 00`. Hash-bound layout; changed RVAs/unknown hashes are rejected. Verify the full selection body 0x89F40..0x8A75B and switch tables 0x8A75C (4 targets), 0x8A7D8 (9), 0x8A7FC (2), 0x8A86C (108) for interior entries. Probe actual installed RedBird DisplacedByteCount before transaction and verify 18 after commit. Failed unpublished selection installation rolls back independently of production; published hooks remain process-rooted. Mission gate uses the existing singleplayer activation flag, with a separate selection flag at allocation+4.

Vanilla sets pending building ID 0x67E8398 and panel 45, schedules mode 16 via 0xA110, and commits selected ID 0x67E8394 via 0x8470. 0x19D960 exports in_structure/type and panel-45 mask/size/delay (GameBuilding+0x300/+0x30E/+0x310). GameData detects mode/panel/building changes and calls InBuildingGameAction -> setUpInbuilding; this sets Show_HUD_Main=false, Show_HUD_Building=true. No managed HUD override. Existing editor controls remain interactive in gameplay for this test.

Presentation is rooted by Application.onBeforeRender, once per frame; input only by OnKeyDown. Selection additionally requires current managed mode/panel and matching in_structure, ID 1..3999, alive owner/type identity. Main-thread projection never runs from OnTick. Render takes a nonblocking lock only for a small target snapshot, releases it before Unity work, then projects one tile. Mesh/material are reused; unchanged transforms/visibility are not written. All normal per-frame work is allocation-free by code inspection, not yet proven by live profiling. Bounded diagnostic sampling reports allocation data only if this Mono provides GetAllocatedBytesForCurrentThread; otherwise explicitly unknown.