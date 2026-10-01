# Spectator building and wall placement

Native baseline SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
These findings are static; the spectator testmod still requires in-game confirmation.

- `DLL_MapAction` dispatches wall input through `0x8AAF0` to `0x94120`. Preview/line handling reaches `0x61210`; the delayed wall chore reaches `0x11B50`, `0x6F230`, `0xC8CC0`, and `0xB7D80`. The line limit uses stone at `0xCF640`. A click-time stone adjustment alone cannot make the later construction free.
- The building click path at `0x90CD0` checks `0xCC420` before enqueuing chore `0x1C`. The chore handler at `0x12090` checks `0xCC420` again before entering `0x6D580`. The latter is the normal structure placement/construction path and retains terrain and collision validation.
- RVA `0xCC420` begins with bytes `48 89 5C 24 20 44 89 44 24 18`: two complete five-byte instructions. For the installed RedBird NativeX64 1.5.0.0 backend, the Indirect detour uses a six-byte entry patch and displaces ten bytes here. No incoming edge into this ten-byte range was found in the audited call contexts. A runtime hook must still validate hash, bytes, backend schema, displaced length, patch form, and pointer slot before publishing.
- The Script Extender exposes `OnBuildStructure.IsFree` at the native construction call and `OnBuildingSpawn` for the actual building record. It exposes `OnBuildWall` and player resource subtraction events for wall accounting. The installed Fixes mod already patches a separate wall cost site at `0x6F230`; no overlap with the `0xCC420` entry hook was found.

Pending runtime checks: zero-stock walls of all three mapper types, delayed order matching, unchanged stone/cost remainder, regular versus spectator well sprite variation and tile layers, and non-spectator behavior.
