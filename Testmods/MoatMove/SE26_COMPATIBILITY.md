# MoatMove / Script Extender 2.6.0

This record supersedes the Assassin-selection hook entries in the original copy documentation. User-approved scope: six call-site adapters, unchanged Fast/Precise searches, no global pathfinding overrides. The user explicitly chose to retain the existing MoatMove version 0.1.1. Plugin and manifest require SE 2.6.0.

## Identity and completed feature audit

Canonical installed CrusaderDE.dll SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. CURRENT.json, semantic manifest and queried native records match. SE tag v2.6.0 / commit 2cee24e33b5a5d81d1c275efabc714ac59917b7b matches the refreshed baseline; installed SHCDESE is 2.6.0.0. Installed RedBird X64InlineHook decodes at least 14 bytes, rounds to whole instructions and appends its continuation after the custom generator. Its implementation and decode-only candidates were checked, rather than assuming HookSize was exact.

The complete previous movement/work/attack audit is retained in FAST_IMPLEMENTATION.md and _inspect/MoatMove/NativeAudit. This extension covers all selection callers: 8C5F0 (four calls), B70C0 and B72C0. 196870 reads the manager's 35 type counters at +564, with Assassin index22 at +5BC. SE calls this original once, preserves nonzero results and can extend a negative answer through its configured type overrides. MoatMove no longer resolves or detours that function entry. Native function names retain their baseline candidate confidence; the described parameter/branch contracts were checked against decompiler flow and canonical instruction bytes.

8C5F0's attack/tile fallbacks consume the result before E2CA0. B70C0 selects perimeter PCL versus tile-pair queries; B72C0 uses a positive answer to reject its ordinary neighbor-region alternative. These different continuations are preserved. Selection RCX is captured before the original call (not reconstructed from a possibly clobbered register). Active building recursion can lift only a negative result. Positive results remain full-width authoritative values; pending pair state is cleared even on this early return. Null/disposed context remains negative; the existing decision catch clears pending state on failure.

The accompanying SE feature audit follows profile table ->196280->F4930->E1640/E32B0->unit path publication, connection table181E00->DF720/182750, and movement1855A0->DCE60->DCD60. Profiles and connection permissions apply globally per type. DCD60 runs after transition validation; bypassing it does not open moat edges. No new profile, permission, surface-bypass or selection-override writes are introduced.

## Complete displaced blocks

All addresses below are RVAs for the hash above. Every block begins with its original call to196870. Original subsequent instructions retain branch and RIP-relative targets.

| Start | Length | End (exclusive) | Bytes |
|---|---:|---|---|
| 8D724 | 17 | 8D735 | E84791100085C07423468B84262C070000 |
| 8E2B8 | 14 | 8E2C6 | E8B3851000488D153C1DF7FF85C0 |
| 8E550 | 17 | 8E561 | E81B83100085C07423458B842C2C070000 |
| 8F325 | 18 | 8F337 | E846751000498BFE85C074348B15F12A9803 |
| B7161 | 15 | B7170 | E80AF70D004533F6B90100000085C0 |
| B7321 | 14 | B732F | E84AF50D0085C0757E488BF333DB |

The preserved conditional targets are8D750,8E57C,8F365,B73A8. The8E2C6 conditional and B7170 cmovne remain outside the blocks and consume the reproduced test flags. Existing attack gates are validations only, not separate mutations. The8F388/8F393 gate is outside all spans. Semantic xrefs contain no external entry into a displaced block interior.

At each original Win64 call site RSP is16-byte aligned and RCX carries the selection manager; no stack arguments or incoming flags are consumed by the predicate. The adapter reserves E0 bytes and saves RCX at+D0. It calls the live SE-owned entry exactly once, then preserves post-call RAX/RCX/RDX/R8-R11, XMM0-XMM5 and flags. Both calls have distinct usable32-byte shadow space at the current stack top. Saved values start at+20 and are outside that area. ABI-preserved registers and XMM6-XMM15 remain protected by the managed ABI. The decision callback receives saved RCX and the complete original RAX, and replaces only the saved RAX result. Flags and registers are restored, and LEA restores RSP without changing flags before relocated native instructions execute. No stack or pointer value is used after a clobber without restoration.

All six adapters participate in the central transaction. Generator return addresses/first call targets and actual DisplacedByteCount are checked. Failure rolls back the initialization candidate. Runtime, callback and handles remain rooted; published hooks are not disposed by map/plugin lifecycle.

## Verification and acceptance

SE26_PROVENANCE.json explicitly permits only the new adapter file, main runtime integration and plugin dependency changes, retaining the older copy/Fast provenance as historical evidence. Existing source and runtime fixtures cover Fast/Precise, groups, attacks, work, ownership and rollback. New fixtures assemble the actual emitter for all six sites, verify calls/branches/RIP targets and preserve nonzero SE results including full-width values. SelectionAdapterContracts.py independently interprets those emitted bytes for720 machine-state comparisons with destructive synthetic callees: aligned stack, full shadow-space writes, volatile GPR/XMM clobbers, original flags and taken/untaken branches. Installed RedBird is also checked using decode-only private buffers. No test installs a hook into the running game.

Final checks passed, including the complete source/runtime fixtures and all original extraction hashes against Git commit a64b82aae338d161928514ba30bfd332c7b41b3e. The elevated build.bat /nopause ran once successfully and installed only MoatMove0.1.1 with SE minimum2.6.0; DLL/PDB/manifest hashes matched and the existing config hash was unchanged. Installed DLL SHA-256: EDE78352082A1D7EFA4176AD74037C81687F868D522E2B8C035B05FC004038CB. The build reported only the existing CS0649 warnings for unused diagnostic counters. In-game acceptance remains separate: Fast and Precise, alone and with APIShared; mixed/Assassin selections, hostile units/buildings, cursor feedback and actual orders. No performance improvement or elimination of own searches is claimed for this compatibility change.
