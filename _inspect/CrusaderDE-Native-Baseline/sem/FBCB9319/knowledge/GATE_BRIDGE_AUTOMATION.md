# Independent manual gatehouse and drawbridge control

Native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Audit date: 2026-10-05. Static control/data-flow evidence; the new gameplay combinations still require an in-game acceptance run.

## Complete relevant control chain

The gate render/update handlers `A5320`, `A57F0`, `A5C80` call `B73D0` before consuming the gate state and command bytes. `B73D0` first handles capture/ownership and then the automatic gate decision. Its automatic section decrements the signed 16-bit reopen timer, checks the separate manual/open cooldown, scans the native enemy list on its 50-tick cadence and validates unit identity/liveness/control. Distance is strict Chebyshev `<140` for humans or `<200` for AI (native eighth-tile units), with delays 100/1200 ticks. APIShared owns the configured values and optional centered origin; the Script Extender owns the candidate event and BugfixesAndQoL may reject inaccessible enemies.

`C5300(void*, int buildingId, int open, int manualSource)` propagates commands. Gate sources find up to two live drawbridges through `B9330`, using the ordered footprint edge and exclusion ID. The owner argument to `B9330` is not used as an ownership filter; do not invent one. Bridge sources seek the main/inner gate types in that order. `D57B0` is the direct bridge command and passes `manualSource=1`; `D5810` is the direct gate command and passes zero after updating its own timer. Both verify the supplied Global-ID before writing. All building IDs are one-based. Native manager addressing is a sentinel-relative `buildingId * 0x32C`; do not reinterpret it as an ordinary span index.

Native manager base RVA `64CCBB0`: gate state/command at slot-relative `2FE/2FF`, manual cooldown at `314`, reopen timer at `31C`; bridge command/state at `2EF/2F0`. GameBuilding views have a different header anchor. New assembly accesses only the established native expressions; ExtraFeatures uses typed fields and never gate timer aliases on a bridge.

`B7C84` additionally propagates the ACTUAL gate state. For a manual gate this would contradict a bridge-only close/open decision; the manual branch therefore exits through `B7CB4` without visiting that state resynchronization. `B7BF4` is the no-enemy path; a nonzero reopen timer means no new bridge command. The close branch updates the exact selected low 16-bit delay and forwards once without changing the gate state/command.

`A5E20` contains bridge animation and command consumption, with no enemy scan. States 0/1/2/3 are lowered/raising/raised/lowering; commands 10/11 initiate raising/lowering when allowed by native state. `6C3B0` checks occupied relevant bridge cells before raising. `645C0` and `64460` perform moat flags/state, visual refresh, connection rebuilding, region invalidation and dirty publication. Existing elevated-moat hooks stay authoritative. An already accepted transition finishes normally when the automation preference changes.

## Hook ownership and machine contracts

Fixes owns `B7C39` (farmer recovery); APIShared's existing decision generator routes manual gates BEFORE that target. It must not invoke Fixes merely because an automatic bridge is closing. Script Extender's candidate span and APIShared's distance/decision spans are disjoint from the four new spans:

| Start | Length | End | Manual continuation |
| --- | ---: | --- | --- |
| B7A4E | 17 | B7A5F | B7A71: retain native enemy cadence, bypass local command cooldown |
| B7BF4 | 15 | B7C03 | Forward open only at timer zero; B7CB4 |
| C53D5 | 18 | C53E7 | C54E8: skip linked gate command and timer writes |
| C54A0 | 16 | C54B0 | C54D8: advance to the second bridge without changing the manual first one |

Full parent-function decoding and baseline xrefs show no incoming interior targets. The native installed RedBird X64InlineHook uses a minimum of 14 bytes rounded to whole instructions. Each committed span must match the table. Query arguments are R10D for gate handlers, EAX for the resolved linked gate, and RCX for the linked bridge before their displaced instructions consume these values. Save RAX, flags, all volatile general registers and XMM0..5; ABI nonvolatile state remains intact. R8/R9 carry the second bridge and must survive. Recompute the predicate test after the callback wrapper; do not trust wrapper flags. Close-routing preserves EAX's delay and restores RBP to module base before the native timer write. The native coupling call uses RCX=R13, EDX=R10D, R8D=open, R9D=0, with the original parent's aligned stack/shadow space, then exits normally.

Complete coupling body hash (519 bytes at C5300): `71652656B8970E86BBBAE5E3EC95C422489F9B5DE4B020ACEC4C01BA044612A1`. Some historical baseline rows also use RVA C5300: always filter by binary hash.

## Validation and remaining runtime evidence

`_inspect/APISharedTests/GateBridgeAutomationTests.cs` uses the installed inline backend and productive generators on private native buffers, including deliberately clobbering predicate calls. `_inspect/GateBridgeAutomation/Verify.ps1` checks source/XAML/lifecycle/publication contracts. The final in-game acceptance matrix must cover all four preferences, two bridges with differing preferences, blocked raising, ongoing animation, direct commands in both directions, save/map restore, recycled slots, and a genuine synchronized multiplayer session.
