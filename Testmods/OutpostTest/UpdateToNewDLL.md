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
