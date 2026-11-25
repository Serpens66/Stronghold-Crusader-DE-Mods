# Native contracts: CrenelatedWallTest

Reference native SHA-256: FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2. APIShared is a public-API dependency, not a version/file-hash-pinned implementation. Select its validated SDK through ApiSharedDir / SHCDE_API_SHARED_DIR; without an override use the local package. The project uses the ordinary workspace provenance gate and never ships APIShared. The minimum remains 0.9.1 because no newer public API is needed. At the 2026-10-23 update, local and installed APIShared were both 0.14.0; that observation is not a future compatibility condition.

## Runtime native access

The mod has no read-only native call. Since 2026-11-25 it rejects no construction (the blockade and its preview validator hook were removed); the only native code contract is the permanent graphics hook below.

## Enterability evidence (2026-11-22)

The rule set rests on this audited reader behaviour of the same hash. Its regression tokens are in `tests/audit-native.py`.
- `0xD86F0` gives a high crenel (bit `0x400000`) no path edges at all.
- `0x6A560` (ladder target, via `0xDC900`) requires `(flags & 0x400102) == 0x100`.
- `0xD9C40`/`0xDCE60` (assassin climb) exclude the mask `0x4A5014B1`, which contains the high bit.
- `0x181E30` (siege tower docking) never reads the high bit, so troops can leave the tower only through a low tile among the five wall tiles at its front.
- Vanilla placement `0x6F230` sets high exactly for odd `x+y`.

Re-audit these readers before changing the rules.

## Borrowed SE and Vanilla contracts

Except the graphics predicate documented here, native addresses are audit references. Input: `0x85BD0`, `0x8AAF0`, `0x94120`. Execution/field transport: `0x11B50`, `0x1F5F0`, `0x6F230`, `0xC8CC0`. Shape/height: `0x5C580`, `0x5C0E0`, `0x5C2D0`, `0x5C1C0`, `0x6CB20`, `0x65830`. Refresh/lifetime: `0xE3B90`, `0xCDE60`, `0x5BC70`. Readers: `0x6B530`, `0x9C730`, `0xD86F0`, `0xDC900`, `0xE9D90`, `0xE9FF0`, `0xF0710`, `0x10DF60`, `0x6A560`, `0xD9C40`, `0x181E30`.

`OnBuildWall` Pre records the raster (`BuildWallsMaximum` prefix) and Post compares it with the tiles actually built; the installed wrapper emits Post with original inputs. Mirror-tool originals run before the event and are outside this feature's supported input path. The event's `ReturnValue` is not a build-success signal.

Tile grids are obtained only from public SE views: logic `+0x898400`, graphics `+0x140900`, wall body `+0x625B00`, height `+0xD7E5A0`, owner `+0xE1AFE0`, damage `+0xFA2980`, structure `+0xB0BCA0`. These offsets are evidence, not consumer constants. Owner is `(raw & 7)+1`, not the raw value returned by `GetTilePlayerOwnerId`. Capacity is 320800. Domain flags and mapper values must remain the installed enum symbols. Low/high changes only bit `CrenelatedHigh`; intact straight middles must have precisely two opposite cardinal crenel neighbors and no transverse neighbor.

The renderer sets high on every shape except a straight middle. Since 2026-11-25 every managed intact crenel follows the map parity (pieces without axis but with diagonal neighbours: x parity), and the explicitly approved local hook below draws each low non-middle crenel as a straight piece, which skips that OR. High is also used by height and pathing readers; refresh pathing and visuals through the public SE APIs after a logical change. Damage and structure footprints are not normalized. Native height additions (68/98), terrain, other flags and ownership remain authoritative.

## Approved permanent graphics hook

RVA 0x5C2D0, full body 237 bytes, ABI `int(tileManager*, int tileId, int rowY, int direction)`. Native return is 0/1 in EAX. Only call sites are 0x65DD6 (direction 0) and 0x65E4F (direction 2), both inside 0x65830. The helper reads four cardinal neighbor masks through the native row-delta table. Its positive result selects the orientation/rotation-aware low/high straight sprite family in Vanilla; its negative fallback normally writes a special sprite and ORs high.

Backend: RedBird.Backends.NativeX64; its SHA-256 (BackendHash) is a build gate in tests/audit-native.py only, never a runtime lock (2026-11-25, AGENTS/native-hooks.md). At runtime Validate checks the real hook contract, the audited 237-byte body is compared with the unpatched load image, and Shared.NativePatchGuard checks the live displaced span before Enable, watches the published bytes and rechecks them at every mission and editor start (foreign patches are logged as errors with bytes and jump target). Only `Absolute`, no thunk following/call following/chaining, scan limit 237. Fourteen required patch bytes round to fifteen: `mov [rsp+8],rbx; mov [rsp+10h],rsi; mov [rsp+18h],rdi`. No branches, relative operands or flag dependencies are displaced. Continuation 0x5C2DF starts the original XOR. Full executable-section scan finds no incoming direct edge into the displaced interior. Installed patch is FF25/disp32=0, inline callback pointer, one NOP; PointerSlot must be zero. Real copied-code tests execute the trampoline and production disabled wrapper for every cardinal neighbor mask.

The callback calls the original exactly once, preserves positive results, and only returns an additional positive result for a published, still-low, intact, owner/row-matching tile whose current cardinal crenel neighbours are exactly the recorded ones, on the axis with more neighbours (tie: the axis of direction 0, which the renderer asks first). Public SE spans are reacquired per call; no native-memory view crosses a lifecycle transition. Immutable snapshots are published by the logical runtime, not generated from render timing. Rendering never writes flags, paths, owners or heights. Session retirement publishes an empty snapshot; no installed hook is disposed or repatched. Only a failed prepared candidate may be released. Failed registration cannot activate the session runtime.

Redraw reports (2026-11-24) replace the former per-tick scan of managed tiles. The callback records the id of every watched (managed) tile it is asked about, before and independent of the native result, and changes nothing else; the runtime drains the set on its next `OnTick` and normalizes only tiles whose shape signature changed. Straight-drawing assignments and the watched set are concurrent dictionaries updated per normalized tile (no whole-set snapshot), so every cost follows the change, not the number of managed crenels (load test in INGAME_TESTS.md). This relies on the audited contract that the only native writers setting `0x400000` on logic tiles are 0x6F230 and 0x6CB20 (both with SE events) and renderer 0x65EF0, which is reached only after this predicate returned 0 for the same tile; `tests/audit-native.py` asserts exactly these three writers. A new writer or a new predicate caller on a changed DLL requires a new audit before this replacement remains valid.

Run the mod verifier after game, SE, Fixes or relevant APIShared contract changes. It compiles the actual production sources and XAML bindings against the selected SDK, accepts synthetic version/additive-service changes, and rejects a missing required mission capability. Whole-APIShared and whole-SE file hashes are not consumer compatibility gates; the SE build wrapper's relevant semantics remain covered by the native audit. Native image, predicate bytes and RedBird backend checks above stay strict because the hook depends on those exact machine contracts.

The working-source driver records dependency hashes only before/after one build to prove it did not alter the SDK or installed APIShared. Those records are dated evidence under .work/a/crenel, never requirements imposed on subsequent SDK releases. Historic October 14/15 snapshots remain under _inspect for evidence and are no longer build inputs. Actual multiplayer, graphics and map/save runtime acceptance remain in INGAME_TESTS.md. Future common ownership is described in APISharedIntegration.md.

## Former preview validator

The preview validator hook at `0x7A3B0` (2026-10-23 to 2026-11-25) was removed together with the blockade. It is no longer installed or audited; its former contract is in the git history of this file.
