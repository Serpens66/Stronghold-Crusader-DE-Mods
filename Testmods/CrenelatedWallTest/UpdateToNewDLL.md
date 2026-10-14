# Native contracts: CrenelatedWallTest 0.1.0

Reference SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. Installed SE 2.14.1: E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3. APIShared 0.9.1 is consumed unchanged. Fixes source review: v1.26.4; low-wall cost hooks preserve the crenel branch.

## Runtime native access

The sole consumer-owned native address is the read-only wall availability getter, RVA `0xCF640`, `int(playerManager*, int playerId, int goodsId)`. Its entire 179-byte reference body is stored in `NativeAvailability.ReferenceCode`; SHA-256 F2C875634DF70ABE13AE3938E6F63B1C4BCF2E61061B753BCD3FB9EFFF23B21A. Pointer and one-based player ID are validated through public SE methods; goods ID uses `eGoods.STORED_STONE_BLOCKS` (4, checked against the installed enum). The delegate is retained by the static process runtime.

Resolution: first validate the reference RVA against live bytes. On mismatch, attempt the exact full-body pattern exclusively in executable PE sections of the SE snapshot, require uniqueness and revalidate live bytes. Since the body contains relative globals, an equal byte body at a different RVA is not accepted without a new semantic audit. Unknown DLL hashes, ambiguous/missing patterns, changed live bytes or invalid module bounds leave the entire test feature inactive. The hash gate runs before this address is used. No code bytes are patched; no hook or trampoline exists.

This getter preserves Vanilla's campaign/cheat result 100, native mode-6 resource branch, stone-times-two-minus-remainder branch and clamp to 250. The consumer does not interpret modes or calculate prices. In `0x6F230`, resource exhaustion is `available < alreadyPlaced`; the public command maximum independently counts raster visits. `WallPolicy.Predict` therefore limits new tiles to the exact getter result plus one, while `Raster` honors the original command maximum. The repeated endpoint cannot create a second tile. Re-audit both comparisons after updates.

## Borrowed SE and Vanilla contracts

All other native addresses are audit references, never direct consumer accesses. Input/preview: `0x85BD0`, `0x8AAF0`, `0x94120`, `0x7A3B0`, `0x61210`. Execution/field transport: `0x11B50`, `0x1F5F0`, `0x6F230`, `0xC8CC0`, `0xCF640`. Shape/height: `0x5C580`, `0x5C0E0`, `0x5C2D0`, `0x5C1C0`, `0x6CB20`, `0x65830`. Refresh/lifetime: `0xE3B90`, `0xCDE60`, `0x5BC70`. Readers: `0x6B530`, `0x9C730`, `0xD86F0`, `0xDC900`, `0xE9D90`, `0xE9FF0`, `0xF0710`, `0x10DF60`.

`OnBuildWall` Pre can modify `BuildWallsMaximum`; the installed wrapper ignores `SkipOriginalFunction` and emits Post with original inputs. Zero maximum stops before placement, but Vanilla still runs once and may update ordinary bookkeeping. Actual before/after tiles and stone prove rejection at runtime. Mirror-tool originals run before the event and are outside this feature's supported input path. The event's `ReturnValue` is not a build-success signal.

Tile grids are obtained only from public SE views: logic `+0x898400`, graphics `+0x140900`, wall body `+0x625B00`, height `+0xD7E5A0`, owner `+0xE1AFE0`, damage `+0xFA2980`, structure `+0xB0BCA0`. These offsets are evidence, not consumer constants. Owner is `(raw & 7)+1`, not the raw value returned by `GetTilePlayerOwnerId`. Capacity is 320800. Domain flags and mapper values must remain the installed enum symbols. Low/high changes only bit `CrenelatedHigh`; intact straight middles must have precisely two opposite cardinal crenel neighbors and no transverse neighbor.

The renderer preserves special-piece geometry by setting high on endpoints, corners and junctions. Do not clear that bit there or substitute a straight sprite silently. High is also used by height and pathing readers; refresh pathing and visuals through the public SE APIs after a logical change. Damage and structure footprints are not normalized. Native height additions (68/98), terrain, other flags and ownership remain authoritative.

Run the mod verifier after any game, SE, Fixes or APIShared update. Extend/revalidate this audit before admitting changed implementations. Keep all public APIShared source/metadata/package files unchanged during this task; the dependency snapshot checks 225 files. Actual multiplayer, graphics and saved-game runtime acceptance remain in `INGAME_TESTS.md`.
