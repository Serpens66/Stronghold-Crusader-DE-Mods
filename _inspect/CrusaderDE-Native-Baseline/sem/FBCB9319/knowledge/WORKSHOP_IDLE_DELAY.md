# Poleturner and tanner idle-animation audit

Native hash: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Canonical Extender source commit f8d51730fcb54b25af43d3c9348d57db058e077f; tree 657af449e1397c58e6c5ec054977d83198b68e66. Native hash rechecked 2026-10-01.
Evidence: semantic function/caller/callee exports, installed PE disassembly and vtable 0x321CB0 (slots 18 poleturner, 21 tanner), installed interop metadata and installed RedBird.X64 implementation. Database function names remain candidate; update roles and instruction/layout contracts below are confirmed by independent vtable and machine-code evidence. Developer intent remains unknown.

Poleturner and tanner route state 0 -> 4 -> 1 on first arrival. Fear-factor release resumes 4 -> 1; ongoing entertainment may temporarily use state 1 with work mode 1. In state 1 they first advance an idle sequence, call 0x19B9D0, then exit unless animation completion EDI/EBP is nonzero. Material selection only follows completion. Fletcher selects material immediately in state 0; blacksmith in state 1; armourer has a fixed 20-update setup delay, not these worker idle sequences.

Poleturner idle sequence lengths are 54/26/100/156 words at 0x332D30/0x332CF8/0x332D80/0x332E00. Tanner sequences are 16/32/22/32/48 at 0x332788/0x3328C8/0x332AD8/0x332B08/0x332B40. Native status 0x189220 maps states 0/1 to Waiting for Wood (159) or Waiting for Cow (161) without checking resources. That UI delay follows the native idle state, not a managed text cache (native export 0x19D960 -> DLL_RunTick 0x86680 -> managed FatControler.getChimpActionText).

Poleturner selects stockpile wood through 0xB9280, validates access via 0xC90E0 and route via 0x196280, state 2 withdraws one wood through 0xB6100, state 3 deposits at workshop and repeats if more wood is required. Fade state 0x6D resumes 6, then 30-update preparation and state 5 production; states 7/8 deliver output. Existing work-rate/fear logic 0x193A70/0x191070 remains unchanged. Tanner uses workshop hides first or 0x18B470 to select a cow, states 2/3 acquire/return, state 9 slaughters and adds three hides. Fade resumes 6; preparation consumes one hide, followed by production and delivery. No fix should directly rewrite these states or reservation/consumption fields.

## Optional poleturner/tanner workshop idle delay

Reference native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
`EnableWorkshopIdleDelayFix` is a shared host setting, default/reset false. It changes only the idle-animation exit after the native positive-fear helper has returned zero. The native material selection, accessibility, routing, consumption and output routines remain authoritative.

| Native reference | Signature / derivation and contract |
| --- | --- |
| Poleturner update `0x13AAD0`; inline `0x13AC85`, continuation `0x13AC96`, exit `0x13BDAF` | `85 FF 0F 84 22 11 00 00 42 0F B7 84 23 D8 09 00 00`: test edi; je exit; movzx eax,[rbx+r12+9D8]. Full displacement 17 bytes. |
| Tanner update `0x13E0B0`; inline `0x13E39F`, continuation `0x13E3B0`, exit `0x13E6C2` | `85 ED 0F 84 1B 03 00 00 42 0F B7 84 23 D8 09 00 00`: test ebp; je exit; same movzx. Full displacement 17 bytes. |
| Pause entry `0x191070`, pause execution `0x185430`, animation helper `0x19B9D0` | Active entertainment can reuse AI state 1. The helper is called before both inline sites; work mode at manager-relative unit offset `0x956` equal to 1 must veto the bypass even when the helper returned zero. |
| Stockpile query `0xB9280`, resource-to-building lookup `0xBFD30`, table `0x2E76D0` | For `eGoods.STORED_WOOD_PLANKS`: reverse IDs from allocated-1 through 1; alive IsAlive, type STRUCT_GOODS_YARD, same signed-short owner, signed dword wood >= 1. No reachability check in this query. |
| Cow query `0x18B470`, distance helper `0x79C0`, scratch result `0x34A9F50` | Ascending IDs 1 through allocated-1; alive IsAlive, low short at `0x8F8` zero, same signed-short owner, CHIMP_TYPE_COW, AI state zero. Distance uses the scratch result at +12: max(abs(dx),abs(dy)) < 10000. The stub only checks existence and never calls/writes this scratch result. |
| Unit manager `0x67E8400` | Native next-ID bound at +0; stride 0x490; record origin manager+0x65C+gameId*stride. r12 is the manager, rbx is gameId*stride at both sites. Capacity 10000 including reserved slot zero. |
| Building manager `0x64CCBB0` | Native next-ID bound at +0x50; stride 0x32C; record origin manager+0x5C+gameId*stride. Capacity 4000 including reserved slot zero. |

Unit manager-relative fields read: alive 0x6E4; type 0x6E6; signed-short owner 0x6EE (native combines the Extender's owner byte and next byte); signed-short tile X/Y 0x71C/0x71E; low-short cow exclusion 0x8F8; AI state 0x918; work mode 0x956; workshop ID 0x990; workshop global ID 0x9C0; original animation variant 0x9D8 (the interop name r_TicksSinceRestPulse is reused by this native worker path).
Building fields: alive 0x12C, type 0x12E, owner 0x132, global ID 0x134; resource dword at 0x17C+4*eGoods (wood 0x184, hides 0x190). Hides are compared as signed >0, matching the tanner update.

Resolution first validates the reference RVA and then tries a unique executable-section signature if it differs. A relocated signature is rejected because register formulas, exits and fixed data roots have no independently proven migration contract. An overlapping hook therefore cannot be bypassed by hooking a lookalike. All these fixed-layout accesses remain hash-bound; unknown hashes disable only this feature. Successful resolutions are timestamped Info messages; failures are timestamped Error messages.

Both complete native update functions and semantic xrefs contain no incoming edges into the 17-byte interiors. Originals and continuations are checked against the installed PE. The installed X64InlineHook backend is probed without Generate/Enable before installation; its minimum 14-byte decode rounds to 17. Generated stubs are assembled and decoded before commit; callbacks validate the displaced instructions and branch destinations. After commit the actual handles, targets and displacements are checked before publication.

The stub saves RFLAGS, RAX, RCX, RDX, R8-R11; RBX/R12 and the completion register EDI/EBP stay untouched. No managed calls, SIMD instructions or Context wrapper occur. Both paths restore the stack/registers/flags before replaying Vanilla. Ready replays TEST and MOVZX and omits only JE; disabled/not-ready replays all three. The next CMP overwrites TEST flags as in Vanilla. No executable bytes are changed after publication: one aligned Volatile.Write changes the data gate. Static runtime roots retain the transaction and its allocation for process lifetime. Only unpublished, disabled initialization candidates may roll back.

Runtime validates installed interop sizes and field offsets and logs manager allocation samples at the first game tick. Existing Extender worker events occur after material selection or on state writes and cannot correct this idle exit. Local Fixes has no hook at these sites; the existing building-level tanner fade fix owns a separate function at 0xB10D0.

Automated tests execute the emitted stub in private buffers, check both paths, both resource sources, signed bounds, pauses, invalid/recycled workshops, registers, TEST flags and read-only game memory. They also decode the same installed X64InlineHook backend and compare unique signatures with the canonical DLL. In-game first arrival, return from a fear-factor break and host/client synchronization remain manual acceptance tests.
