# Arabian idle sword-draw audio contract

Reference native SHA256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed SHA256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Installed NativeX64 SHA256: `0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0`.

| Feature target | Reference RVA / derivation | Search and fallback |
| --- | --- | --- |
| Arabian unit handler | `0x16F590`, dispatch table `0x321CB0 + 75*8` -> handler | Unique executable-section prologue pattern in NativeContract; validate RVA first, search after mismatch; reject moved semantic boundary. |
| Spatial sound emitter | `0x1A41E0`, wrapper `0x2960`; idle call `0x1700CD` -> wrapper | Unique executable-section record-writing pattern in NativeContract; same fallback and boundary validation. |
| Current unit Game-ID | `0x9302C4`, RIP operand at handler+`0x1F`, next instruction handler+`0x23` | Derived only after complete prologue pattern validation; expected target and module bounds checked. |
| Native unit manager | `0x67E8400`, LEA operand at handler+`0x26`, next handler+`0x2A` | Derivation checked; runtime obtains units through public TryGetUnitById, not raw manager access. |

The pattern fallback resolves code entries only. Unit layout (stride 0x490), state semantics, sound record shape and marker availability have no general cross-version fallback; this single feature remains hash-bound. A changed hash or contract logs a timestamped Error and leaves Vanilla active. An already-published partial chain stays rooted and logically inactive; only unpublished candidates can be released.

NativeX64 Absolute only: handler span `[0x16F590,0x16F59F)` = 5+5+5, continuation push rdi; emitter span `[0x1A41E0,0x1A41EE)` = 4+3+4+3, continuation writes x. No branches or RIP operands in either displaced block; full-function direct edges and semantic xrefs contain no interior entry. Retest the actual backend, selected scheme, displacement, target, FF25 patch with hook pointer, zero PointerSlot and NOP padding after updates.

GameUnit slot1 = manager+0xAEC; raw native lookup = manager+0x65C+gameId*0x490. Public array lookup uses gameId-1. Type offset 0x8A, AIState 0x2BC, tile X/Y 0xC0/0xC2 are checked against the installed public struct. Type75, state1, sound180 and spatial flag0 select the single audited idle sword-draw emission; native RNG is untouched.

Emitter records contain 21 signed shorts (42 bytes): kind[0]=16, sound[3], volume[11], pan[12], spatial flag[13]. Normal spatial emission overwrites volume and pan with -1. The mod changes only volume[11] to -32768 on eligible newly appended records. Both negative values preserve the same Vanilla spatial branch; memcpy/pool transport preserves the marker. Managed processTestMap computes Vanilla volume/pan before the wrapper multiplies volume by 0.25 and calls public SFXManager.playSound once. No global sound settings, clip volumes, simulation state or additional random calls change.

Before accepting an update, re-audit the whole handler/dispatcher and other sound producers, both record writers, pending/tick transport, managed spatial/explicit paths and downstream channel selection. Inspect other mods for owners of both native entries and processTestMap. Current Script Extender 2.14.1 and local Fixes 1.25.1 do not hook these entries; their audio substitution/notification controls retain their normal path. Runtime logging and the in-game listening comparison are still required to confirm actual playback on Unity Mono.

## Startup and shared access regression

The first in-game test (2026-10-11 14:55:06.456) failed before hook publication: `ArgumentException: Invalid path`, caused by deriving installed file paths from GameMap.Assembly.Location. All binary paths now derive exclusively from BepInEx.Paths.GameRootPath. Test byte-loaded assemblies with empty Location and missing/wrong-hash files after changes to startup or hashing. Log the waiting state before subscribing because LibraryLoaded may replay synchronously.

Runtime unit lookup uses APIShared.UnitAccess.TryGetById (dependency>=0.7.0) and preserves the exact audited type/state/context checks. No extra liveness filter is applied. Compile against the verified local APIShared package; never bundle it. The shared preflight owns the UnitAccess scan and must stop the build for direct SDK lookups or ignored checked results. Sound factor and testmod version remain 0.25 and0.1.0 while the listening test is pending.
