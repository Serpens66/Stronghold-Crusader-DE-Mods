# Native update contract

- Feature: suppress Vanilla lord-defeat gold and incoming-goods awards for human winners.
- Audited native SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Owning function: RVA `0x15A950`, VA `0x18015A950` (unit state machine; candidate symbol, confirmed reward instructions).
- Hook start: RVA `0x15C477`. The 16-byte span is `0F 84 46 09 00 00 66 42 83 BC 23 48 0A 00 00 00`: `JE 0x15CDC3` followed by the special-case `CMP`. `0x15C475` tests the one-based winner ID in EBP. The following `JNE` starts at `0x15C487`; the reward block starts at `0x15C48D`.
- The copied-buffer probe of the installed RedBird `X64InlineHook` backend must report `DisplacedByteCount=16`. No direct branch enters the interior of the displaced span. With `AfterCallback`, the callback changes only ZF before the relocated JE. The original branch then skips gold and incoming goods but rejoins Vanilla's death-state continuation at `0x15CDC3`.
- Source owner is read from the lord unit, and the winner ID from the kill attribution field. Both are 1-based player IDs. `GamePlayerManagerAPI.IsAIPlayer` classifies a validated winner from the native AI lineup. An invalid ID or callback failure leaves Vanilla flags unchanged.
- Reference-RVA bytes are checked first; `Shared.NativePatternResolver.ResolveUnique` also provides an exact, unique pattern search. A changed hash, ambiguous pattern, changed span, or changed branch target disables only this test feature, with an error log. Re-audit this complete state-machine path, player identity layout, RedBird backend, and overlapping Extender/Fixes hooks before supporting a new DLL.
