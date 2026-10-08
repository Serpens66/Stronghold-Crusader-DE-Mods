# Rally-point terrain slowdown audit (2026-10-08)

Native SHA256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Installed managed SHA256: BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789.
Canonical Extender source: commit 5049b5d051d0face848cc2eb098db1fedf5451a7,
tree cc31dd3beea7c50a1bd1b6825b5ae3624f0dee31.

## Vanilla data flow

The full feature chain was audited: recruitment (0x1819D0/0x190CA0), type
initialization (0x19A240), transformation (0x195D10), rally orders
(0xBED20/0xBFD40), path start (0x196280), all 26 dispatched handlers,
the unit-update dispatcher (0x182B00, length 0x23B1), movement (0x1855A0),
arrival (0x188340), and the complete terrain calculation (0x19B260).

AI state 105 handlers copy base movement delay to effective delay before
the dispatcher uses it. Terrain calculated after a step is consequently
discarded on the next update. This affects types 5, 22..30, 37, 70..76,
and 78..85, without an owner restriction. Handler reset RVAs, in that order:
12E5E8, 13FB77, 141FEF, 143951, 1454B7, 146EFB, 148588, 14956A,
14A635, 14B454, 151310, 16807C, 169F80, 16B48F, 16CF4E, 16E6E9,
16F731, 1707E4, 17337A, 17430D, 175400, 176B5B, 178956, 17A7B1,
17B9C3, 17D59C.

At 0x18410C the dispatcher calls [r14+rax*8+0x321CB0] (8 bytes).
At 0x184114 it loads EDX from the current unit ID at RVA 0x9302C4
(6 bytes). Continuation 0x18411A is MOVSXD RAX,EDX. RCX is the
one-based unit ID multiplied by 0x490; RBX is the unit-manager base.
The manager-relative unit origin is RBX+RCX. GameUnit starts 0x65C
bytes later. Raw xrefs and the entire dispatcher have no external
entry into the displaced span's interior; the preceding branch skips
to its continuation. The installed RedBird X64InlineHook decoder
displaces exactly 14 bytes here (minimum 14, rounded by instruction).

The common cadence load at 0x184203 reads the effective delay at
manager-relative +0x9A2, base delay is +0x9A4, running bonus +0x916,
and movement ticks +0x9A8. Alive state +0x6E4 must be 2; the low WORD
at death marker +0x8F8 must be zero. Type +0x6E6, global ID +0x6F0,
and AI state +0x918 establish the capture/restore identity.

Terrain calculation remains Vanilla: elevation comparison against
unit height at +0x712, swamp/moat flags 0x20000000/0x40000000
(delays 3/4/6), ford flag 0x00200000 (delay 2), and later state
adjustments. The old mod hook at 0x19B506 copied the base delay before
terrain checks and erased the already calculated elevation adjustment.
The terrain setting bypasses only that additional reset.

## Hook and lifecycle contract

BugfixesAndQoL captures the effective delay before the original handler,
then restores it at the shared cadence hook before Vanilla loads it.
Capture restores flags, RAX, RCX, RDX and the complete stack before
executing the indirect call once. Other registers and SIMD are untouched.
Restore uses only registers overwritten by the displaced cadence loads;
the common wrapper saves flags. Both before and after must be alive,
non-dying, state 105, with matching type and global ID.

Native snapshots use 16 bytes per one-based unit slot: global ID (0),
type (4), delay (6), enable epoch (8), valid marker (12). Capture first
invalidates the slot. Restore consumes it once, including rejected
snapshots. Atomic enable transitions increment the epoch; bit zero
is the enable flag. A setting roundtrip invalidates old snapshots.
No recruitment events, unit-list scans or managed callbacks per tick
are needed. Save-loaded units enter the same native capture path.

The optional capture transaction validates the pattern, complete span,
continuation and dispatcher incoming branches before commit, and checks
the actual DisplacedByteCount after commit before publication. Failure
rolls back only this unpublished candidate and leaves the terrain flag
off. Shared hooks, allocations and integration publisher are rooted for
process lifetime; setting changes only publish data flags. No published
hook is undone. Fixed layout is admitted only for the validated hash.

Canonical local Fixes 1.25.1 and the installed Script Extender have no
competing hook at this capture span. Existing shared movement ownership
is retained. Native execution tests assemble/decode and execute the
production generators, covering both independent settings, 26 types,
owners, original call/registers/flags, death, identity/state changes,
epoch rejection and single consumption. These are isolated execution
tests; real in-game terrain routes and save loading still require playtest.
