# Crenelated wall construction and sticky high pieces [Scope: Vanilla + mod integration]

Native SHA-256 FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2; managed BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789. Installed SE 2.14.1 hash E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3, fork commit 5908e1f12437deb7ef5f5b1dc2d64e301178fba3, tree c238441ea12db35ee36c5c5668d3c8fce0897295. Fixes source v1.26.4. Audit date 2026-10-14. Findings are confirmed static data/control flow for these identities, not observed game execution; internal FUN names are not exported APIs.

## Complete feature path

Managed `EngineInterface.PlaceMapperItem` serializes access with `threadLock` and calls exported `DLL_MapAction` at 0x85BD0. This sets/restores temporary native view/player state and enters 0x8AAF0. Wall line handling 0x94120 uses 0x7A3B0 validation and 0x61210 preview, then enqueues Chore 25 on mouse release. Both requested coordinates must be active; validation retains terrain, structures, ownership/unit eligibility, distance and native path/build-range branches. Crenel eligibility calls 0x5C580 for adjacent suitable wall masks. Native line walking advances every nonzero coordinate difference by one per iteration and revisits the endpoint.

The current Chore table at 0x2C7A30 points opcode 25 to 0x11B50. Mode 1 writes fields; mode 0 restores them and calls 0x6F230 with the Chore sender from native global 0x88E3D6C, not the local view. 0x6F230 reruns validation and reads current availability at 0xCF640. Its command maximum limits raster visits, while availability limits actual new placements with a strict-less-than comparison. Native validation failure or zero maximum cannot place tiles. Intact existing walls are skipped for mapper 26; damaged cells may be reconstructed.

0xCF640 returns 100 for the native cheat/editor branch, uses its separate native mode-6 resource branch, otherwise computes twice the stone/plank balance minus one when a corresponding remainder exists, with maximum 250. 0x6F230 stops when availability is less than the already placed count, not less-than-or-equal. A delayed command can consequently place a shorter prefix than its queued maximum. 0xC8CC0 charges the actual placement count and retains a partial-stone accumulator; SE supports wall cost multipliers and Fixes has an additional low-wall-only quota adjustment. Do not infer current quota from a local preview or implement a replacement price formula merely to validate shape.

On successful crenel placement, 0x6F230 sets bit 0x200, raises native byte height by 68 or 98 according to 0x5C0E0, and sets bit 0x400000 when X/Y parity differs. It establishes wall/owner state, handles units/vegetation, refreshes tile/neighbour pathfinding, queues area refresh via 0xE3B90, charges actual wall count and marks component state dirty. Owner storage preserves upper bits and stores player-1 in the lower three bits. The public SE owner getter returns the raw byte; consumers must decode it.

AI placement 0x6CB20 (caller 0x51790) independently handles mappers 26/35, flags, native height, parity, ownership, refresh and costs. This is a countercheck, not the human entry point. The proposed consumer does not replace or subscribe to the AI placement path.

## Renderer and downstream semantics

0x65830 consumes changed tile/region state from the normal loop 0xCDE60 and reset path 0x5BC70. For intact wall/crenel cells without structures, 0x5C2D0 recognizes the two-opposite-neighbour straight case; 0x5C1C0 recognizes corner cases. Straight pieces select low/high sprite families 0xC0070..0xC007F according to orientation, tile variation and the high bit. Other endpoints, corners and junctions use special sprites 0xC0080..0xC0084 and OR high into the logic layer. That write is permanent: transitioning an already high endpoint into a straight middle never clears its old high bit. Parallel rows also fail the isolated-straight predicate and are forced high. This explains the single-placement and double-row behavior without assuming a different user-input mapper.

The three explicit 0x400000 logic-layer OR writers are human placement 0x6F230, AI placement 0x6CB20 and visual recomputation 0x65830. Generic clear/removal paths and construction conversion must still be respected. Damage uses its own graphics branch, and destruction/removal invalidates the old topology. Readbacks use current ownership, flags, damage and structure state; a consumer must not retain old tile identity blindly through unload/reload.

High is not cosmetic only. 0x6B530 adds 10 for a low crenel and 20 for a high piece and complements an inaccessible result according to owner/context. 0x9C730 also distinguishes height +10/+20. Path and edge readers at 0xD86F0, 0xDC900, 0xE9D90, 0xE9FF0, 0xF0710 and 0x10DF60 branch on the high bit. Render path 0x41D60 has additional height/presentation reads. Therefore a logical correction requires the public path/visual refresh, not only a sprite write.

## Corrected Chore-25 evidence

The older consolidated opcode-25 row's proposed six Int32 fields/24-byte size is not the contract of this build. 0x11B50 declares nominal payload size 12 and copies fields of sizes 2,2,2,2,1,2 through 0x1F5F0. Their consumed offsets are beginX 0, beginY 2, endX 4, endY 6, mapper 8 and signed build maximum 9. The four coordinates are zero-initialized before their low words are read. The sender comes from the decoded Chore slot, not a payload field. 0x1F5F0 advances its cursor by the literal copy size, hence these fields consume 11 bytes; the meaning/content of the remaining nominal byte is not interpreted by this handler. This current static audit supersedes that historical proposed row. The raw/consolidated artifacts and SQLite publication are preserved rather than silently relabeled during unrelated ongoing baseline work.

## Consumer contract and validation [mod integration]

SE's existing `OnBuildWall` publisher is process-lived. Its Pre inputs are mutable; the wrapper ignores `SkipOriginalFunction` and always calls Vanilla with the effective inputs. Post is reconstructed from original inputs and has no usable success result. Setting maximum zero is a supported native no-placement input, but a runtime before/after check remains necessary. The debug mirror tool calls native originals before Pre and is outside this interception boundary.

`Testmods/CrenelatedWallTest` uses these existing events and public APIShared mission/preset services. It installs no hooks, preserves Vanilla special pieces, corrects straight own middles, rejects new 2x2 blocks or unavoidable equal triples, and persists only touched cells. Its sole direct native access calls the unmodified 0xCF640 getter read-only, with full-body live validation, executable-pattern fallback, hash binding and permanent delegate rooting. Save classification uses APIShared because the installed SE can mark multiplayer saves as non-save archive loads; repeated archive callbacks stage data rather than applying it early.

Executable regression: `Testmods/CrenelatedWallTest/tests/audit-native.py` verifies the current identity, 27 feature functions, Chore table/dispatch, the complete 179-byte availability getter and all three explicit high-bit writers. Policy tests exercise actual production algorithms, preservation of modeled Vanilla special-piece redraws, ownership/bounds, rejected-command neutrality, resources/prefixes, deterministic two-peer maps and actual MessagePack encoding. Runtime sources and XAML bindings are type-checked against the installed true assemblies without emitting the mod DLL. Game graphics, unit height, native callback execution, real peer synchrony and save acceptance must be checked in game; do not promote fixture passes to those claims.

## Follow-up audit: low straight endpoints, 2026-10-15

This section supersedes the initial consumer's fixed-high endpoint/no-hook restriction above; the native identity and original observations remain unchanged. The user explicitly approved a permanent graphics hook owned exclusively by the testmod. APIShared itself remains unchanged by this follow-up. Its independent development source is now 0.10.0, the local DLL has assembly version 0.9.1 and lacks build evidence, while the game has installed 0.9.2. The user explicitly authorized consuming that installed 0.9.2 DLL without building/copying APIShared; both development and installed files are hash-checked unchanged.

The complete straight predicate at 0x5C2D0 is 237 bytes, Win64 ABI `int(tileManager*, int tileId, int rowY, int direction)`. EAX is 0 or 1. A full executable-section branch scan finds only two direct entries, 0x65DD6 and 0x65E4F, both in renderer 0x65830; pseudocode cross-check finds no non-renderer consumer. The callers use R9D=0 and R9D=2 respectively. The predicate reads exactly four cardinal crenel bits via row*8 neighbor deltas, with directions 0/4 sharing one axis and 2/6 the other. Native high/low straight-sprite selection immediately follows a positive result and already accounts for camera rotation and tile variation.

The admitted NativeX64 Absolute scheme requires 14 patch bytes, rounded to 15 by three full five-byte home-slot stores: RBX at RSP+8, RSI at RSP+10h, RDI at RSP+18h. No relative operands, branches or flags are displaced. Trampoline continuation is 0x5C2DF, the original XOR. No direct executable-section branch targets the interior of these 15 bytes. The full function and live entry are matched before publication; existing modifications/chaining are refused. Installed RedBird.Backends.NativeX64 hash is 0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0. Its actual NativeDetour, PrologueAnalyzer, JumpFactory and options were decompiled for this audit, rather than inferring behavior from another hook family.

The approved override calls the original first and preserves every positive result. A negative result can become positive solely for an immutable published low-endpoint assignment, matching manager, packed tile, row, owner and live intact/non-building state. Exactly one unchanged cardinal neighbor must remain, on the requested axis. This selects native low straight graphics without allowing the special fallback's sticky-high write. The callback writes no simulation data and makes no policy decisions. Session changes empty the snapshot; installed code remains published for process lifetime.

The actual production validator and disabled callback were executed through the installed NativeX64 backend on a copied full 237-byte function, with independently allocated tile data, for all 16 cardinal masks and four axis directions: 146 checks passed. This validates Absolute/15, inline target/NOP patch, the unchanged three displaced instructions, real trampoline continuation, argument/return semantics, and disabled Vanilla behavior. It does not confirm in-game endpoint appearance or peer synchrony.

## Follow-up audit: native red construction preview, 2026-10-23

Native identity remains FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2, verified against the installed canonical DLL. These are confirmed-static field/control-flow findings; visual game acceptance remains open. Earlier consumer-state observations above remain historical.

The complete 1627-byte 0x7A3B0 validator has Win64 ABI void(tileManager*, int playerId, uint beginX, uint beginY, uint endX, uint endY, short mapper). Executable-section direct-branch scan confirms only calls 0x6F299 and 0x941F1. It initializes the 64-bit pair at tileManager+0x204E6E4 to 1 (invalid int=1, next int=0), and clears only the invalid int after full successful raster validation. Early failure, bounds/support-wall/terrain/unit/build-range branches retain invalid. Crenel supporting-wall eligibility is 0x5C580, unchanged. Automated AI construction calls its separate validator 0x77CF0 and is not a caller of this hook.

The same field is independently proven by UI global 0x60AD494 = tileManager base 0x405EDB0 + 0x204E6E4. UI 0x94120 runs validation before either preview or release. 0x61210 selects invalid crenel ghost sprite 0x60014 versus valid 0x60004 from this int, zeroes the invalid preview count and calls the native preview-price path with preview flag 1. On release, 0x94120 enqueues Chore 25 only if this same status is zero. It obtains current CF640 quota and queues that value as maximum raster visits; execution's additional actual-placement quota remains a distinct comparison. Native execution revalidates at 0x6F230. No error-reason number needs fabrication for a mod shape veto.

The tested NativeX64 backend hash remains 0843DD4C381A3E77DD6D8B51D5CCF95465B3FB49BFDF2980F39A212D774AADB0. Absolute/14 minimum rounds to 18 bytes at the validator entry: mov [rsp+10h],edx (4), push rbx/rbp/rdi (1 each), push r12/r13 (2 each), sub rsp,80h (7). Continuation 0x7A3C2; no direct executable-section entry into the displaced interior. There are no displaced relative operands or branches. Consumer fixtures run the production callback and actual backend/trampoline with this exact prologue plus an isolated seven-argument native ABI/status probe; the real validator body depends on module globals and is checked statically, not executed outside the game. Disabled paths, original veto, additional veto, next-call reset and managed policy failure preserve original-once semantics.

The consumer optionally registers the entry as Exclusive and remaining body as Observe using existing public NativeSiteRegistry; APIShared itself is unchanged. Complete snapshot/live bytes are checked, no chaining is admitted, and a failed preview install retains existing execution protection. No published code is unpatched. Rendering only consumes the native invalid state; actual construction continues to use fresh synchronized admission, bypassing the preview policy through current-thread OnBuildWall scopes. Preview cache reuse validates every raw read, failed/empty boundary read, saved preference and request input; it never authorizes execution. The only preview write is the int invalid-status zero-to-one transition.

## Follow-up audit: demolition redraw and occupied walls, 2026-11-12 [Vanilla facts]

Canonical native SHA-256 remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`, independently rehashed and matched to CURRENT.json and the selected semantic dataset. The existing internal function names remain candidate labels; the concrete field accesses below were checked against their bodies and the canonical instructions. No new native entry or patch is introduced by this audit.

Wall demolition `0x63700(tileManager*, actor:int, packedTileId:int)` resets the target HeightGrid byte at `+0xD7E5A0` to DefaultHeightGrid at `+0xDCCAC0`, clears target logic bits with `0xFFBEF4FF`, removes same-tile static decoration and refreshes pathfinding. It calls `0xE3B90(pathManager*,9,x,y)`, whose eight-neighbour traversal marks ChangedGrid at `tileManager+0xA20D40` and the relevant ten-by-ten dirty blocks. Neighbouring wall logic/height are not rewritten synchronously by that traversal. On a later `0xCDE60` update, the render-maintenance order includes `0x7C0A0`, `0x7D170`, `0x65830`, `0x650C0`, then `0xE5E60` dirty clearing. In the already documented intact, structure-free crenellated-wall branch, **RVA `0x65EF0`** executes `or dword ptr [rbx+rax*4+0x898400],0x400000`; `rbx` is TileManager and `rax` comes from its current packed tile index at `+0x204E814`. Removing a cardinal neighbour can invalidate the isolated-straight predicate `0x5C2D0` and reach this sticky-High writer on the surviving wall. This is not proof of replacement, and does not establish that CrenelatedLow, IsLowWall or native Height changed. High retains the gameplay/path semantics documented above; it is not globally cosmetic.

Occupied terrain is separately protected. After the native wall/moat selector and owner checks, `0x79B90` reaches **RVA `0x7A047`**, `cmp word ptr [rbx+rax*2+0xBF6C00],0`; `rax` is the selected packed wall/moat tile. A nonzero TileUnitIdGrid entry causes **`0x7A052`** to set query-blocked at `tileManager+0x204E6FC` to1. There is no occupant owner/type exception on this branch. The field is the actual UInt16 tile-unit layer (`GameTileManagerView.TileUnitIdGrid`, public `GameTileManagerAPI.GetChimpLayer()`), independent of the cursor's hovered-unit selector. `0x63700` independently guards its physical wall removal with the same tile-unit short equal to zero (after its initial misc-display pending-bit clear). An own unit moving onto the wall can consequently invalidate an earlier otherwise identical eligibility result.

## Demolition runtime corroboration, 2026-11-12 [mod integration]

Observed corroboration, not a claim about every remaining tile: archived local run `.work/t/area-wolf1/009/walls/e23b931662da/LogOutput.log` records tile233160 changing `0x300 ->0x400300` before packet5 with owner1, Height106, DefaultHeight8, unit0 and structure0 unchanged; tile234924 shows the same High-only transition at Height76/DefaultHeight8. This matches the independently identified deferred Vanilla write.

Archived local run `.work/t/area-wolf1/012/combined/565da8ecb291/LogOutput.log`, lines6223-6224 and7473-7474, corroborates this second condition: tiles171965 and172735 had unit0 at initial capture, then unit13 at the native-permission guard while owner, type/flags and native height remained unchanged. These observations support the occupancy predicate; they do not justify bypassing live eligibility or clearing the native tile-unit layer.

## Follow-up audit: enterability of high crenels, 2026-11-22 [Vanilla facts]

Native SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` was rehashed against CURRENT.json. The findings below are static: they come from semantic pseudocode, capstone disassembly and installed table bytes. The two key tests at 0x6A560 and 0xD86F0 were cross-checked a second time against the pseudocode. Each finding gives its confidence.

**Tile flags and heights.** Placement 0x6F230 sets 0x200 and 0x100 on every crenel (0x300). A crenel stands 8 above its wall height: 68 or 98, against 60 or 90 for the wall. The +10/+20 that 0x6B530 and 0x9C730 add for low/high only offsets unit height and projectile collision; pathing reads the raw height grid. *(confirmed)*

**Edge builder 0xD86F0** (called via 0xD90D0 and 0xDAA50):
- **High crenel as centre:** a tile with bit 0x400000 gets no edges; the function returns mask 0. *(confirmed)*
- **High crenel as neighbour:** it never becomes passable. No unit can stand on, enter or cross a high crenel, archers included. *(confirmed)*
- **Low crenel:** it is linked to wall-top tiles (0x100) within a height difference of ±16 (the comparisons include the bound). That covers the walkway behind it and diagonally adjacent low crenels. *(confirmed)*
- **Docked siege-tower tiles (0x10000000):** they connect in all 8 directions to every neighbour that has 0x100 and is not high, with no height check. *(confirmed)*

**Ladders (type 29):**
- **Target choice (0xDC900):** it searches up to depth 10 for a ground tile that has an axis neighbour with 0x300 set, the high bit clear and a different owner. *(confirmed)*
- **Placement (0x6A560):** it takes the first neighbour, in order 0, 4, 2, 6, 1, 7, 3, 5, where `(flags & 0x400102) == 0x100`. If none matches it returns 0xF and the ladder is not placed. Otherwise 0x1950B0 sets 0x800000 on the ladder tile and allocates portal type 1 through 0xD8510. *(confirmed)*
- **Portal handler (0xD7D50):** it does not test the high bit again. A target that becomes high later probably leads into a tile with no edges. *(probable)*

**Assassins (type 73):** path search 0xD9C40 and the physical step 0xDCE60 require an axis step into a tile with none of the bits in mask 0x4A5014B1, which contains the high bit. The owner is not checked. *(confirmed)*

**Siege tower (type 58):**
- **Target search (0xDFCF0):** it accepts any tile with 0x100, including high crenels.
- **Spot check (0x181E30):** it requires the wall tile's two neighbours along the wall to have 0x100, and a free 3x3 footprint (tables 0x336950..0x336A28) two tiles away. It never consults the high bit. *(confirmed)*
- **Deployment:** it places building 0x5E, which maps to type 0x45; 0x76D40 marks all 9 tiles with 0x10000000.
- **Leaving the tower:** troops can only step onto a non-high tile among the 5 wall tiles in front of the tower (x-2..x+2). One low tile is enough; two adjacent low tiles are never needed. *(confirmed statically)*

**Double crenel rows.** In a 2-wide band every tile has a crenel neighbour across the band, so the straight-piece check 0x5C2D0 fails for all of them. Renderer 0x65830 therefore makes the whole band permanently high. Siege towers still dock, because 0x181E30 ignores the high bit, but nobody can unload. Ladders and assassins find no target at all. This matches the user's in-game observation.

**Other readers.**
- 0xE9D90, 0xE9FF0 and 0xF0710 (choosing a free tile after a climb, via 0x181890) all exclude high tiles. *(confirmed)*
- 0x10DF60 counts non-high wall tiles for AI wall planning. *(candidate)*
- 0x41D60 only uses the bit for a sprite offset.
- Corners, junctions and endpoints have no separate logic: special pieces behave exactly like straight high pieces.

**Still open:**
- The meaning of the byte at +0xFA2980 (compared against ≤ 20) on tower tiles.
- How the AI selects siege targets.

## Enterability rule set, 2026-11-22 [mod integration]

`Testmods/CrenelatedWallTest` now uses the facts above as follows. This supersedes the alternating optimizer described in the consumer sections above.
- **Height rule:** straight pieces and endpoints follow Vanilla placement parity (high when x+y is odd). Specials keep Vanilla's own height (high).
- **Scope:** only crenels placed by humans while the mod is active are managed and saved, by id and owner without heights. Map, AI and pre-mod walls are never rewritten.
- **Rejected constructions:** a command is rejected when it newly creates a filled 2x2 block, a diagonal chain of 3 isolated crenels, a chain of 3 cardinally connected special pieces, or a cardinal run of 4 high crenels.
- **Removed native read:** the former 0xCF640 quota read is gone; admission checks the whole raster conservatively.
- **Hooks:** the existing straight-predicate and preview hooks are unchanged.
- **Regression tokens:** `tests/audit-native.py` covers 0x6A560 and 0xD86F0.
- **Not yet verified in game:** graphics, actual docking and entering, and MP synchronization.

## In-game confirmation and crenel support predicate, 2026-11-22 [Vanilla facts + mod integration]

- **Crenel support 0x5C580** (disassembled 2026-11-22): returns 1 if any of the four cardinal neighbours (direction table entries 0, 2, 4, 6 at `rcx+row*8*4`) has `(logic & 0x102) == 0x100`, i.e. an intact wall or crenel tile without bit 0x2. A crenel cannot be placed on open ground; the first piece needs an adjacent wall, later pieces may lean on earlier crenels. *(confirmed by disassembly and by `CreateWall` refusing unsupported tiles in game)*
- **Placement needs stone:** with zero stone and SE wall cost multipliers at 0, `CreateWall` for mapper 26 placed nothing; with stone it placed. Consistent with the 0xCF640 availability read in 0x6F230. *(observed)*
- **Vanilla sticky high reproduced:** an AI-owned crenel row (unmanaged by the mod) built piece by piece with the simulation running between pieces ended with all 7 tiles high, 3 of them against parity. *(observed, run d673bb3e)*
- **Move order onto a high crenel:** `IssueMoveHereCommand` to a high crenel middle returned 0 (native rejection), while orders to low crenels led an Assassin onto the target tile. *(observed; consistent with 0xD86F0 building no edges for high crenels)*
- **Mod integration (CrenelatedWallTest, parity rules):** 42 managed crenels matched the rule after render maintenance on two start positions of opposite parity; 2x2, diagonal and one-step stairs were rejected; save/load kept all flags of managed tiles. On load the mod rewrote 6 tiles (`CRENEL_LOAD changed=6`); which load path raised them was not traced. Evidence: `.work/a/crenel-ingame/010`.

## Ladders and siege tower in game, 2026-11-22 [Vanilla facts + mod integration]

Run 13e32903, evidence `.work/a/crenel-ingame/021`. Attackers were units of an unused player slot; walls were the human's mod-managed crenels.
- **Ladder:** before the ladder, `IssueMoveHereCommand` of a spearman onto a low crenel returned 0. After `AttachLadderToWall(tribe, crenelTile)` the ladder tile (0x800000) was the ground tile directly in front of the crenel, the same move was accepted and the spearman stood on the crenel. *(observed)*
- **Siege tower:** a tower from `CreateUnitLocal` was manned by four engineers via `ManSiegeEquipment`. A manned tower has no tribe (`r_TribeId == 0`); a tribe created with `Create` + `AssignUnit` accepted `AttackWall(crenelTile)`. The deployed tower marked a 3x3 block with 0x10000000 directly in front of the wall. Before it, no low crenel of the segment was reachable; afterwards a spearman reached a low crenel among the five front tiles. *(observed; consistent with 0x181E30 and the front rule above)*
- **Test setup facts:** an AI player absorbs freshly spawned own units into its tribes and sends them home. Orders to a unit that is still NeedsInit are dropped. SE wall cost multipliers are not saved. Building a crenel was refused near enemy troops, and succeeded after they were removed. *(observed)*

## Editor UI path, bulldozer and damage in game, 2026-11-22 [Vanilla facts + mod integration]

Run d33717ce, evidence `.work/a/crenel-ingame/035`. Editor on 4spielerkarte.map, no opponents.
- **Building mode:** walls and crenels are placed only after switching the editor to `MEMode` 1 (app mode 14/10); `StartMapperItem(26)` then returns 26. *(observed)*
- **UI line path:** `EngineInterface.PlaceMapperItem(mapper, x, y, 0, player, inGame, true, state)` with states 0, 1, 2 per tile and 3 on release reproduces a drag. The invalid int at TileManager+0x204E6E4 (UI global 0x60AD494) becomes 1 per invalid tile while dragging; with 1 at release nothing is built. *(observed)*
- **Vanilla red causes seen:** an `IsSwamp` tile (0x20000000); building next to walls of another slot; a crenel next to a heavily damaged support wall (heights 24 and 66 instead of 98). *(observed)*
- **Bulldozer:** `EngineInterface.DeleteBuilding` alone did not remove a crenel. Driving the editor input path (hovered tile via `MainControls.getMouseMapTilePosition`, button states 1,2,2,3 via `EditorDirector.getMouseStateForEngine`, `overNoesisUI` false) removed it, consistent with the cursor-state dependency in AREA_DEMOLITION.md. *(observed)*
- **Melee damage:** three slot-2 macemen with `AttackWall` raised the damage layer of the target crenel 15, 30, 45, 60 and then destroyed it, plus two neighbours. *(observed)*
- **Mod integration:** editor builds of all slots are managed; a mod-disabled designer row kept its Vanilla sticky high bits (2 of 5) after saving the map and loading it with the mod enabled; demolition and destruction survivors and a rebuilt tile matched the parity rule. *(observed)*

## Neighbour table and diagonal crenel rows, 2026-11-22 [Vanilla facts + mod integration]

- **Neighbour table (live, run f6204c35):** TileManager+0 holds int[8] per row y with packed tile-id deltas. Directions 0=(0,-1), 1=(1,-1), 2=(1,0), 3=(1,1), 4=(0,1), 5=(-1,1), 6=(-1,0), 7=(-1,-1) in SE (x,y). The straight predicate 0x5C2D0 and crenel support 0x5C580 therefore use only axis neighbours. *(observed)*
- **Renderer order (disassembly):** for a crenel tile, 0x65830 calls 0x5C2D0 with direction 0 at 0x65DD6 before any neighbour counting; a result of 1 selects straight sprites 0xC0070..0xC007F by high bit and camera rotation and skips the OR at 0x65EF0. Directions 2 (0x65E4F) and the corner checks 0x5C1C0 follow only on 0. *(confirmed)*
- **Diagonal rows in Vanilla (editor, mod off):** every piece is isolated and ends high. A dragged diagonal kept path edges and was walkable (assassins reached all 5) because placement parity made it low while edges were built and the renderer raised it later without a path refresh; a piece-by-piece diagonal had no edges except the last piece. On the other parity a diagonal is placed high from the start. *(observed)*
- **Mod integration:** managed diagonal pieces alternate by x parity; low ones are published to the graphics hook (direction 0 only). Editor and skirmish acceptance passed; all low diagonal pieces were enterable. Visual appearance of low diagonal pieces not yet checked. *(observed)*

## Special-piece sprites and low corners, 2026-11-24 [Vanilla facts, static audit]

Notes: `.work/a/crenel-corners/AUDIT.md`. Hash FBCB9319, capstone disassembly, no live probes.

- **Special branch:** after both straight checks fail, 0x5C1C0 (directions 1/3/5/7 at 0x65E8A, 0x65F10, 0x65F67, 0x65FCF) returns 1 only for exactly two perpendicular crenel neighbours. L corners get sprites 0xC0080..0xC0083 by rotation; every other special (endpoint, T, cross, isolated) gets 0xC0084 (0x66029). *(confirmed)*
- **Unconditional high bit:** all special branches join at 0x65EDE (17 jumps, 3 more via 0x65ED3); 0x65EF0 `or [logic], 0x400000` has no condition. The special path never reads the high bit, and the sprite ids 0x60..0x91 are contiguous, so there are no low special sprites in code. Whether 0x80..0x84 look high is not checked visually. *(confirmed / visual candidate)*
- **Pathing of a low special:** 0xD86F0 aborts only on the high bit and builds edges to all low wall neighbours within +-16 height, independent of neighbour count. A low corner/T/cross would be enterable (ladders 0x6A560, assassins). The renderer issues no path refresh, so the bit must be right before edges are built. *(confirmed)*
- **Options:** correct visuals for low specials are impossible natively. Without a new patch, the existing 0x5C2D0 predicate can keep a special low only by drawing a straight sprite (wrong shape). A conditional skip of the 18-byte span at 0x65EF0 (or 11, xor 2, jmp rel32 5; inbound only 0x65ED3/0x65EDE) would keep the corner shape on a low tile with a presumably high-looking sprite; SE/Fixes overlap not yet checked. *(candidate)*

## AI placement path and pre-built castles, 2026-11-24 [Vanilla facts + mod integration]

- **AI path:** SE `OnAIBuildWall` wraps exactly 0x6CB20 (pattern resolves to one match). It runs validator 0x77CF0 first and skips placement while TileManager+0x204E6E4 is non-zero; mapper 26/35 set 0x200 and the parity high bit, owner byte = player-1. One tile per call. *(confirmed)*
- **Pre-built castles:** lobby option `MPsetupData.advopt_pre_build` (FRONT_Multiplayer, private field). With 1 the AI castle is placed through 0x6CB20 before the mission lifecycle reports Started (run e0091dd7: 117 crenel results). About a second later only 39 of the 117 AI wall tiles still carried 0x200. *(observed; corrected 2026-11-25: a test counting error, the loop covered only x/y below the map size while tile coordinates of a 400 map exceed 400; all placed walls remain, run 199b6441)*
- **Mod integration:** the height adjustment collects pre-Started AI crenel placements and adopts them at Started (not in editor or save sessions); later AI placements are adopted per call. Pre-built and manually built AI castles matched the target heights after render maintenance. *(observed)*

## Writers of wall and crenel bits, 2026-11-24 [Vanilla facts + mod integration]

Static audit of the canonical DLL (hash rechecked against CURRENT.json). Capstone scan of `.text` for every access to the logic layer `TileManager+0x898400` (direct displacement, LEA-derived pointers and immediates), grouped by function via the semantic database and traced to roots (Chore table 0x2C7A30, exports, 0xCDE60, 0x5BC70, 0x51790). SE event coverage taken from the detour patterns in `BulkBuildingDetours.cs`, resolved against this image.

- **Setting the high bit 0x400000:** only 0x6F230 (human placement, SE `OnBuildWall`), 0x6CB20 (AI placement, `OnAIBuildWall`) and renderer 0x65EF0. All other full-value writers are read-modify-writes of other bits (7, 0xA, 0xD, 0xE, 0xF, 0x1C, 0x1E), checked at 0x6903D, 0x7C1FF/0x7C320, 0x6CCD8/0x6CCE6, 0x599EB/0x599FE, 0x648D6, 0x6FB66, 0x71050, 0x7AF9B, 0x77394, 0x5961A, 0x6237D/0x623F1, 0xB95AA/0xB95CE, 0xB96D6. Register ORs in the structure path (0x73954: 0x100400 or derived value; 0x74FCA) belong to `BuildStructure` 0x6D580, which has an SE event and turns tiles into structure tiles. *(confirmed for the listed sites)*
- **Clearing wall/crenel bits during a match, with SE event:** wall demolition 0x63700 (`OnWallBulldoze`), tile damage/destruction 0x7EB00 (`OnBuildingTileTakeDamage`; also reached from projectile path 0x9C730), building bulldoze 0xB8310 → footprint removal 0x61FC0 (0x622D0..0x635D0), structure placement 0x6D580 (0x5D740, 0x6D711, 0x737E0..0x77230).
- **Without SE event:** 0x7DF60 (from 0x9C730 and 0x12E400), player removal/remap 0x71E90 (caller 0xCD190: Chore 77, `DLL_KickMPPlayer`, `DLL_RemapPlayers`), main-loop paths 0xED2E0 (callers 0x728D0, 0x8AE80) and 0x616B0 (caller 0xD1AD0), 0x5CD90 → 0x61FC0, editor tile chores 19/21/23/24/26 (0x7AE50, 0x6FA10, 0x70DF0, 0x6D130), 0x71FE0 (only from 0x12E400, no direct caller). Load/reset only: 0x59A90, 0x71390, 0x7D680, 0x7FB00, 0x7FF60, 0x80320, 0x80560, 0xB9510, 0xB9640. *(roots confirmed by call graph; semantics of the unnamed functions not individually audited)*
- **Consequence:** an SE-event-only design cannot see every crenel removal. A managed crenel's height can only be corrupted by 0x65EF0, which runs after 0x5C2D0 returned 0 for that tile.
- **Mod integration (CrenelatedWallTest):** the graphics hook on 0x5C2D0 reports every redraw of a managed tile (any result); the next `OnTick` re-checks only reported tiles. This replaces the 256-tiles-per-tick scan. Limit: a removal that never causes a redraw of the neighbours is not seen; Vanilla then raises nothing either. Stale managed ids are filtered on save and resync capture.

## Event-less wall removal paths in detail, 2026-11-25 [Vanilla facts]

Follow-up to the writer audit above; corrects parts of its "without SE event" list. Static, semantic pseudocode and call graph, hash FBCB9319.

- **0x7DF60, area damage to walls:** loops over the tiles of an area via 0x6A620 and lowers the height of intact walls (`flags & 0x10000400 == 0`, no structure, `0x100` set, `0x2` clear) per hit point; at default height it clears `0xFFBEF4FF`, resets height and damage and refreshes pathing for the 3x3 neighbourhood. Projectile impact 0x9C730 calls 0x7EB00 (SE `OnBuildingTileTakeDamage`) for the hit tile and then 0x7DF60 for the splash area, so neighbouring walls destroyed by catapult/trebuchet stones raise no SE event. Second caller 0x12E400 has no direct caller (referenced from data at 0x321CD8). *(confirmed static; which projectile types reach this branch not mapped)*
- **0x71E90, all walls of one player:** full-map loop clearing every wall tile whose owner equals the player, with pathing refresh. Only caller 0xCD190 (player removal: buildings, walls, units), called by `DLL_KickMPPlayer`, `DLL_RemapPlayers`, skirmish setup 0x94350, 0x17ED0 and 0x15A950 (no direct callers; 0x15A950 references victory/warning sounds, probably the defeat handler). *(callers confirmed; defeat meaning probable)*
- **0xED2E0:** timed breadth-first clearance from up to seven seed buildings over 40 ticks (radius from 0x6BB10); removes walls, most buildings and flags along the flood. Armed only by the event processor 0xF82B0 for one scripted map event (condition on `DAT_183a0fa28 == 0x97`), also run from `DLL_LoadMapToPlay`. Not part of a normal skirmish. *(control flow confirmed; scenario identity candidate)*
- **0x616B0** only counts crenel tiles per owner (read-only); it is not a writer. **0x71FE0** clears one wall tile, only reachable from 0x12E400 (data-referenced dispatch, not identified).
- **Common helper 0x9F410** (remove static decoration of a tile) runs right before the wall clear in 0x7DF60, 0x71E90, 0xED2E0, 0x71FE0, 0x63700 and 0x7EB00 (also 0x61EF0, 0x62460, 0x63860, 0xDF9D0). APIShared already owns an Indirect/7 detour on it (`Buildings.ConstructionDecorationRemoval`), so any further use must go through that owner. It runs before the bits change and does not cover the editor paths.
- **Editor paths** (tile type, heights, delete) have SE `MapEditorR3EventHooks` brush events (`BrushDelete`, `BrushSetTileType*`, `BrushSetTileHeight*`, `BrushEvenTileHeight`, `BrushRaiseTileHeight`).
- Placing buildings onto walls is not possible in play (user statement); the clears inside `BuildStructure` 0x6D580 affect the footprint of the new building only.

## Corner-anchored runs, 2026-11-25 [mod integration]

`Testmods/CrenelatedWallTest` no longer uses map parity for straight pieces next to corners. A run (maximal chain of straight middles and endpoints on one axis, `WallPolicy.LineRun`) ending at a special piece (corner, junction, crossing; always high through 0x65EF0) is low next to it and alternates; with specials at both ends the lower-coordinate end anchors the run, so an even run ends high next to the other special (user decision). Runs without a special keep placement parity. Reason: under parity a three-piece end row between two corners was high-high-high at one of the two map parities. Normalization covers whole runs, because a new or removed corner shifts the phase of the entire run.

## Diagonal walking between low crenels, 2026-11-25 [Vanilla facts]

Static (0xD86F0): a low crenel (0x100 set, 0x400000 clear) gets an edge to every one of its 8 neighbours with 0x100, not high and within +-16 height, diagonals included. The final diagonal filter removes NE/SE/SW/NW only when both adjacent axis tiles are walls and the diagonal tile itself is not a wall (`(diag & 0x10000102) == 0`), so crenel-to-crenel diagonals stay. *(confirmed static)*

In game (scenario `crenel-diagonal-walk`, runs `f344a763` and the following rerun; mod height adjustment off, a 4x4 crenel block and a 2x5 double row forced into a checkerboard, forced lows kept straight by a test-only postfix): *(observed)*
- An Assassin climbed a low edge crenel and walked diagonally to an interior low crenel whose four axis neighbours were high (path ground > (409,533) > (408,532)); in the double row it walked from the near low tile diagonally to the far low tile.
- A Spearman came up a ladder onto a low edge crenel and walked on diagonally to the same interior low crenel.
- A move order onto a high crenel of the block was refused.
- Edge masks of the forced lows show the diagonal bits (e.g. 0xAA on an interior low).

Consequence: a checkerboard of low and high crenels is crossable diagonally even in wide bands and blocks.

## Checkerboard for every crenel, blockade removed, 2026-11-25 [mod integration]

Supersedes the corner-anchored runs and the blockade of the sections above (those stay as dated evidence).
- Rule: every managed intact crenel takes map parity (high when x+y is odd), corners, junctions, crosses, double rows and blocks included; crenels without axis but with diagonal crenel neighbours alternate by x, because x+y is constant in parity along a diagonal and an all-high diagonal is not crossable. *(mod design, user decision after the diagonal-walk proof and visual check)*
- Graphics: each low managed crenel that is not a straight middle gets a straight-drawing assignment for 0x5C2D0 holding its axis neighbour ids; the hook answers on the axis with more crenel neighbours (tie: direction 0, asked first at 0x65DD6), so 0x65EF0 never ORs it high. The assignment is published before the height bit is written. *(implemented, offline-tested)*
- Visual check (user, editor, 2026-11-25): low band, block, L-corner, T and cross pieces drawn with the straight sprite look acceptable in the tested layouts. The X shape (centre plus four diagonal crenels) renders badly in Vanilla by camera rotation; cause per static audit: body sprite 0x6BDD0, column height 0x7C0A0 and missing filler tiles 0x6621D, none reachable through 0x5C2D0. *(observed / static)*
- The preview validator hook at 0x7A3B0 is no longer installed.

## Diagonal crenel rows without plain walls, 2026-11-25 [Vanilla facts, static audit + in-game experiment]

Question: diagonal crenel rows (screen-horizontal) look "doubled" once the parallel plain wall behind them is bulldozed. Static audit (capstone, hash FBCB9319, no live probes) plus editor experiment `crenel-look-diagonal` (testmod `CrenelatedWallAcceptance`, run `da7ccd6d`).
- **Camera neighbour fields (0x6C7B0..0x6C8BA):** per rotation R = [TM+0x204E65C], TM+0x204E66C/670/674 = R0: 3,2,4; R2: 5,4,6; R4: 7,6,0; R6: 1,0,2 (directions 0=N..7=NW). 0x7C0A0 uses them as the front (camera-side) neighbours for column height; 0x5C450 tests 670 (selector 2) and 674 (selector 4) for 0x100 only. *(confirmed; "front" meaning probable)*
- **Body 0x6BDD0** (only caller 0x65D67, written at 0x65D79 to TM+0x625B00 as (raw-1)|0xA0000 before the predicate call 0x65DD6, not overwritten on either crenel branch): strip 0xA0000..0xA001F when both axis neighbours on an axis are intact walls (0x5C870); else 0xA002F (wall on 670), 0xA003E (wall on 674), else 0xA0020. With the plain wall behind (R6 in the tested layout) and without it, a diagonal crenel gets the same body 0xA0020; column height (TM+0xEB7A20) is the same too. *(confirmed static)*
- **Body layer readers:** draw path 0x41D60 (0x433F7, 0x435F4, 0x429F3ff), 0x65830 (0x67A69), DLL_GetLayerDebug. No pathing/simulation/checksum (0x22020) reader; the layer is in the save section table (0x336E38, half size) but recomputed after load. *(confirmed / save: probable)*
- **Filler tiles 0x6621D** (ground tiles with (logic & 0xBF1E0581) == 0 and exactly two perpendicular axis wall neighbours, no 0x800 neighbour): height to TM+0x625B00 (Int32, crenel neighbours -8), sprite 0x2E..0x31 to TM+0x279D80 (Int32), variant bit 0x200/0x40/0x100/0x80 to TM+0xF05F40 (UInt16). 0x41D60 draws the filler only while (MiscDisplay & 0x3C0) != 0; 0x65830 clears 0x3C0 (0x65A51) for every changed tile. SE exposes the layers as GetWallGfxLayer, GetAlphaGfxLayer, GetMiscDisplayLayer. *(confirmed)*
- **Experiment:** clearing 0x3C0 on the back fillers of the wall-less row changed nothing visible; clearing the front fillers split the row into single columns (they are what makes a diagonal wall look closed). Neither the cap sprite (straight high sprite direction 0 or 2 for high pieces, tested) nor body nor fillers cause the "doubled" look. *(observed)* Cause not identified; candidate: the walkway of the plain wall behind hides the back of the crenel caps in the walled case.
- **Cap combinations (run `4949fe52`):** all reachable through 0x5C2D0 were tested on the wall-less row: low pieces straight in direction 0 or 2, high pieces Vanilla special 0xC0084 or straight in direction 0 or 2 (six combinations). Each keeps the zigzag crenel line on top of the row; none matches the walled row. The line is the axis-aligned cap sprites of adjacent diagonal pieces; removing it would need a different cap sprite written after the predicate (new native patch at the cap writes in 0x65830) and a suitable sprite, which the crenel sprite range 0x60..0x91 does not contain. *(observed / static)*
- The X shape (centre plus four diagonal crenels) lacks fillers instead (each gap has three crenel axis neighbours); not changed.

## Local full re-render and the logic high bit, 2026-11-25 [Vanilla facts, static audit + mod integration]

Trigger: in an MP test with the CrenelatedWallTest diagnostics build, only the host reported 135 managed crenels redrawn at once without any game event; heights and state hashes stayed equal on both peers. Static audit (capstone, hash FBCB9319, no live probes).
- **Local full re-render:** camera rotation (DLL_SetMapRotation -> 0x7A370 -> 0x6C780 -> 0x5D8A0), the flatten toggle (0x72720 -> 0x59A30 + 0x5D8A0) and chore 55 after an MP repair (receiving peer only) mark every tile changed, so 0x65830 runs over the whole map on that peer only, on the simulation thread between ticks. *(confirmed static; host-only redraw line observed)*
- **Correction to "Renderer and downstream semantics":** 0x65830 runs from 0xCDE60 in every DLL_RunTick frame (also frames without a simulation tick), not only once per simulation tick. *(confirmed static)*
- **Logic write without guard:** on the special branch 0x65EF0 ORs 0x400000 into the logic layer for every intact crenel for which 0x5C2D0 returned 0; there is no graphics-only flag and no path refresh, and the next simulation tick already reads the bit (e.g. 0xD86F0). The logic layer is part of the checksum 0x22020, so a peer-local OR is a desync. In Vanilla this is harmless because intact special pieces are already high in steady state. *(confirmed static)*
- **Side finding:** the same pass also writes other logic bits (bit 7 at 0x6903D, bit 15 at 0x7C1FF/0x7C320); not audited. *(candidate)*
- **Mod integration (CrenelatedWallTest):** the 0x5C2D0 hook must answer only from synchronized tile state. Since 2026-11-25 its assignment holds owner and row only (no recorded neighbourhood) and covers every managed low, intact, structure-free crenel; the axis comes from the live neighbours. Raising a crenel stays with the runtime normalization (with path refresh). Model regression and in-game step `heights-local-redraw` (four rotations, two flatten toggles, no managed crenel changes). *(static + tests)*
