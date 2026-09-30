# Native targets for AI test experiments

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Wood-site acceptance guard (opt-in)

- Owning feature: experimental, wood-only acceptance check inside Vanilla `0x58950`.
- Reference RVA `0x58B26`, eight bytes `84 DB 0F 84 80 00 00 00`: `test bl,bl; je 0x58BAE`. The eight-byte span ends exactly at the fallback block `0x58B2E`, which has other incoming branches and must remain untouched.
- Fallback RVA `0x58B2E` continues the same BFS; success RVA `0x58BAE` writes candidate `(r11d,r10d)`. The guarded stub preserves the original `BL` gate and branches to these same destinations.
- The patch validates the installed hash and exact live site bytes. The byte pattern is short and not established as a unique semantic signature in other DLL versions, so there is **no pattern fallback**. A changed DLL or modified site disables only this experiment before installation.
- The candidate's fixed fine-tile anchor is `(r11d*5,r10d*5)`. Tile data is read through `APIShared.AiBuildDiagnostic.CaptureTiles`, whose native PCL and tile flags use the audited installed `0x50EC690`, `0x402FF2C` and tile-manager logic-grid contracts. A missing observer, invalid tile, differing native/API PCL views or callback exception leaves Vanilla's original acceptance decision intact.
- The one-time patch uses a near executable page and a checked rel32 jump. Its stub is assembled and decoded before publication. The installed branch and stub remain process rooted; map activation is logical through `MissionEvents.Started`/`Ended` and restricted to either the named disposable Canari save copy or the named Rat control map. No code bytes are changed after successful publication.
- Re-audit on a new native DLL: complete `0x58950` control flow, all edges into the eight-byte site, full register and flag liveness, both return paths, fixed 3-by-3 footprint, logic bit `0x4`, path-grid capacity and Script Extender member contract. Check compatibility with the installed `BugfixesAndQoL` entry detour and local `Fixes` patches before allowing the feature.

## Existing coarse-grid isolation hook

- Reference RVA `0xE49D0`, eight-byte prologue `40 53 41 57 48 83 EC 58`; existing mode-specific NativeX64 indirect detour remains unchanged.
- Its own runtime and `verify.ps1` validate the installed RedBird backend and fail closed if this contract changes.
