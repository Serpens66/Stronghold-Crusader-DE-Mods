# Farm construction preview

Native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Static audit on 2026-10-13; first-stage red ground marking accepted by the user in-game; expanded counts and shared tooltip layout subsequently accepted by the user.

Full contract and update procedure: BugfixesAndQoL/UpdateToNewDLL.md in the workspace.

- 0x90CD0 -> 0x77E60 -> 0x5DD70 -> 0x41D60 -> 0x1A2D90 -> Director/GameMap/gameTile is the input, validation, preview, buffer transport and colouring chain. Actual construction rechecks through 0x6D580.
- Farm coverage excludes River and counts OasisGrass, ThickFoliage and Foliage separately, requiring at least scale squared. The separate green counter counts only OasisGrass and ThickFoliage once each and requires >=50; Foliage contributes zero to green, and River contributes zero to both. The former 2,2,1 description was incorrect. Combined bits must not be collapsed; visual texture alone is insufficient.
- The blocked preview reruns 0x7B060, which omits farm coverage and therefore leaves zero-contribution land dark (palette 62) even though the overall farm is blocked. Palette 55 is Vanilla red.
- 0x619C0 generates square XY footprints. Canonical manager RVA 0x405EDB0, native validity mask RVA 0x3A11EA4, row prefixes RVA 0x402FF2C. Preview graphics are temporary and cleared by the renderer.
- Preview extent [0x5DD70,0x60CAC) includes trailing switch-table data. Use reachable control-flow decoding, seeded by baseline COMPUTED_JUMP edges, not linear decoding of the whole PE extent. The fresh audit decodes 2562 reachable instructions and finds no interior xrefs into the six-byte Indirect entry span; it also verifies the unique signature and square-footprint producer.
- Installed RedBird NativeX64 1.5.0.0 Indirect displaces exactly six bytes (five-byte rbx store plus rbp push), resuming at 0x5DD76. Tests use this exact backend/ABI and validate the trampoline, patch form, pointer slot and pointed-to entry. Hooks remain process-lived and logically gated.

- Resource extension: quarry 6x6 >=8 HasStone; iron mine 4x4 >=4 HasIron; pitch workings 4x4 >=1 IsPitch. Resource counters precede River exclusion in 0x77E60. The native preview remains the only sampling callback; publication uses immutable session/mapper/XY snapshots and APIShared main-thread presentation observers.

Migration 2026-10-13: BugfixesAndQoL 1.0.189 owns the permanent preview hook and APIShared 0.9.0 Ground contribution. Local/default-on activation is generation-bound with immutable session snapshots. Contracts and regression tests are in BugfixesAndQoL/UpdateToNewDLL.md and BugfixesAndQoL/tests/BuildPreview.Tests; the prior testmod is retired after package verification. User-confirmed layout is retained; the integrated switch needs a visible game test.
