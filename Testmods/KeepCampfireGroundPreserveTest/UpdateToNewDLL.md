# Native contract for this test

Audited game build: `CrusaderDE.dll` SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`; installed Script Extender 2.10.1.0 and RedBird.X64 1.5.0. The local `shcde-fixes-main` does not patch the generic visual store at this RVA. The test fails closed on any changed native hash, pattern, instruction span, or occupied hook site.

- RVA `0x6E620`: `c_game_update_visual_resourcetile`. Its generic building branch includes type `0x37` (campground).
- RVA `0x6F0A0`: 8-byte GFX store to tile manager offset `0x140900`, followed by an 8-byte record read at `0x6F0A8`. The installed `X64InlineHook` displaces both instructions, exactly 16 bytes. There is no incoming branch to the displaced interior in this build.
- `0x6F0B0..0x6F0D7` computes and stores the corresponding AlphaGFX value at tile-manager offset `0x279D80`. Jumping to `0x6F0D8` skips this work but retains the common loop tail and all previous building logic. The hook checks the current building record's type word at image-relative `0x64CCCDE` for `0x37` before skipping.
- RVA `0x65830`: general terrain recomputation can call the visual function; this is why the hook remains installed for the process and is only logically enabled during allowed missions.
- Demolition: `0xB8310 -> 0x61FC0 -> 0x628F0` clears structure occupancy and dirties graphics; `0xB5C40` has no additional campground removal branch. The hook does not intercept this path.

Before updating the DLL, re-audit the full Keep/campground/terrain and demolition paths, incoming edges, RedBird displacement, installed extender and Fixes interactions. Update hashes, patterns and RVAs together. Do not carry this hook to a new native build based only on a matching short byte pattern.
