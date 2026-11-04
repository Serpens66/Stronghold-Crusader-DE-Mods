# Spectator building and wall placement [Scope: Vanilla + mod integration]

Native baseline SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
These findings are static; the spectator testmod still requires in-game confirmation.

- `DLL_MapAction` dispatches wall input through `0x8AAF0` to `0x94120`. Preview/line handling reaches `0x61210`; the delayed wall chore reaches `0x11B50`, `0x6F230`, `0xC8CC0`, and `0xB7D80`. The line limit uses stone at `0xCF640`. A click-time stone adjustment alone cannot make the later construction free.
- The building click path at `0x90CD0` checks `0xCC420` before enqueuing chore `0x1C`. The chore handler at `0x12090` checks `0xCC420` again before entering `0x6D580`. The latter is the normal structure placement/construction path and retains terrain and collision validation.
- RVA `0xCC420` begins with bytes `48 89 5C 24 20 44 89 44 24 18`: two complete five-byte instructions. For the installed RedBird NativeX64 1.5.0.0 backend, the Indirect detour uses a six-byte entry patch and displaces ten bytes here. No incoming edge into this ten-byte range was found in the audited call contexts. A runtime hook must still validate hash, bytes, backend schema, displaced length, patch form, and pointer slot before publishing.
- The Script Extender exposes `OnBuildStructure.IsFree` at the native construction call and `OnBuildingSpawn` for the actual building record. It exposes `OnBuildWall` and player resource subtraction events for wall accounting. The installed Fixes mod already patches a separate wall cost site at `0x6F230`; no overlap with the `0xCC420` entry hook was found.
- `DLL_MapAction` at `0x85BD0` saves native view `DAT_1888e3d70`, writes its `playerID` argument for the action, then restores the saved value. The queue producer at `0x23990` reads sender field `+0x36FA50` relative to queue base `DAT_188574320`, which aliases `DAT_1888e3d70`; delayed building and wall handlers at `0x12090` and `0x11B50` use the decoded sender `DAT_1888e3d6c`. Thus a human single-player session can submit a mapped action for an AI owner without permanently switching the human view, provided the mod passes that owner to `DLL_MapAction` through the managed wrapper.
- Unit mapper `0x93750` reads the temporary native view/player ID. Ordinary troop creation passes it to `0x118CA0`, which creates units through `0x17FEF0` and assigns them to a tribe where Vanilla normally does so. Special cases can call `0x17FEF0` directly and keep their native no-tribe behavior.

Pending runtime checks: zero-stock walls of all three mapper types, delayed order matching, unchanged stone/cost remainder, regular versus spectator well sprite variation and tile layers, and non-spectator behavior.

## Storage automatic-sprite parameter (2026-11-03 audit) [Vanilla facts]

The canonical installed DLL was rechecked against CURRENT.json and DATABASE_INFO.json: SHA-256 remains `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`. This is a static parameter-flow audit, not a rendered-image acceptance result. The database labels the generic unnamed functions below `candidate`; the specific instruction and data-flow facts were checked against canonical bytes and existing exports.

- Placement validator `0x77E60` has no complete decompiler export. Canonical instruction `0x77EA5`, bytes `48 C7 81 0C E7 04 02 0F 00 00 00`, writes the qword value 15 at tile-manager `+0x204E70C`. With tile-manager base RVA `0x405EDB0`, its low dword aliases global RVA `0x60AD4BC`. The same write clears the adjacent high dword.
- Mapper values Stores 52, Granary 80 and Armoury 81 are present in both the Extender source and installed assembly. The validator's selector-byte table at `0x79914` and target table at `0x798F4` resolve all three to branch `0x780D0`. Subsequent storage checks at `0x7897F`, `0x789FF` and the following armoury branch can reject placement; they do not replace the automatic variation. Neighbor helper `0xC72C0` reads existing building ownership/type/footprint data. Canonical instruction inspection of the validator/shared block through `0x798F3` found no second direct write to `+0x204E70C`.
- After native validation, the general building producer `0x90CD0` reads that result into Chore `0x1C` argument five at RVA `0x86C133C`. Decoder/consumer `0x12090` passes it as argument seven of `0x6D580`. The live-game owner remains the decoded sender at `0x88E3D6C`; coordinates and mapper/scale retain their existing chore fields.
- `0x6D580` converts the mapper to a structure type. Granary/Armoury enter generic constructor `0x75780`, which passes the effective variation as allocator `0xB47E0` argument nine; the same player supplies logical and visual ownership before Vanilla normalization. Stores enter `0x76E80`, which creates four stockpile records with the same supplied variation and native subrecord ordinals. These constructors retain normal footprint/tile registration.
- Allocator instruction `0xB4AB5`, bytes `66 43 89 94 30 5E 01 00 00`, persists the effective variation at raw manager slot offset `+0x15E`; specialized family normalization is still native. `0x6E620` reads the record variation: `0x6EFAA` (`41 83 F8 0F`) compares against 15, and `0x6EFAE` (`0F 84 96 00 00 00`) selects the automatic branch at `0x6F04A`. Granary/Armoury use this general sprite selection rather than camera-relative sheet offsets. Stores retain their separate goods-resource tile-rendering branch.

## Comparison with direct supplemental creation [mod integration]

CastlePlanner commit `b14c74db65ce893488fa43bfc9638049328ee19c` corrected its direct `CreatePrefab` calls from argument seven 0 to automatic value 15 for an explicitly audited supplemental set. Its `SupplementalPrefabVisualPolicy`, `UpdateToNewDLL.md` and retained `.work/a/castle-spawn-fix/001/supplemental-audit.json` describe that separate entry path.

Spectator Place follows the native cursor/validation/chore route above. Its matched `OnBuildStructure` callback changes only `IsFree`, preserving `Unknown1`/variation. Its visual wrappers at `0x65830` and `0x6E620` bracket the original functions with spectator visibility flag `0x3666080`; they do not replace the stored variation or owner. Therefore the historical CastlePlanner direct-argument-zero problem is not present in Spectator's audited Stores/Granary/Armoury path. No graphics correction follows from this comparison. This does not establish correct rendering for every mapper family or repair an already malformed persisted record.

The dated machine/source evidence for this audit is retained at `.work/a/spec-ui/001/placement-graphics-audit.json`; no runtime visual test was performed by that audit.

## Mapper selection camera producer (2026-11-04) [Vanilla facts]

The canonical native hash above was reconfirmed. A further read-only check matched
575 decoded instructions across `0x8BD60` (395), `0x45F20` (4), `0x50220` (54)
and `0x4B270` (122) against the installed PE bytes. These internal function labels
remain candidate names; the specific operands, branches and aliases are verified
for this image. Historical `placement-owner-audit.json` remains unchanged.

`EngineInterface.StartMapperItem(int)` holds its private static `threadLock` around
`DLL_StartMapAction` (`0x87830`). Nonzero actions enter `0x8BD60`; action zero
cancels separately. First-family camera branches cover Stores 0x34, Tradepost
0x4D, Bedouin stockade 0x4F, Granary 0x50, Armoury 0x51, wooden/stone barracks
0x56/0x57 and engineers/tunnelers guild 0x58/0x59. They validate the current
view's first-family ID through `0xCC140` and read its building coordinates. The
Bedouin and wooden-barracks branches also check partial footprint completion.
The shared CALL at `0x8BF96` targets `0x45F20`, with return address `0x8BF9B`,
passing building begin tile plus one on both axes. Mapper state, resource feedback
and placement ownership have separate paths after this camera call.

`0x45F20` is `49 63 C0 4C 8D 04 40 42 03 94 81 6C E1 61 00 E9 EC A2 00 00`:
MOVSXD RAX,R8D; LEA R8,[RAX+RAX*2]; ADD EDX,[RCX+R8*4+0x61E16C]; JMP
`0x50220`. The latter writes pending camera target at manager `0x3A11DC0`
+0x7C/+0x80/+0x84, current camera offsets +0x98/+0x9C, then calls clamp
`0x4B270`, which can also write +0xA0/+0xA4. Serializer `0x19D960` copies
pending target low words into report +0x554/+0x556/+0x558 and resets the pending
XY qword. The managed buffered report reaches `GameData.SetCameraFromGameState`
and applies the Unity camera later. A standalone Unity postfix therefore does
not replace this producer, and clearing pending XY alone leaves other native
camera side effects while potentially discarding another legitimate request.

The real function entry's NativeX64 Indirect minimum of six bytes rounds to the
two complete instructions `[0x45F20,0x45F27)` (3+4 bytes). Known complete-function
control flow/xrefs have no entry inside it. A fourteen-byte context-hook span at
the mapper CALL would include another switch entry `0x8BFA0`, targeted by the
computed branch `0x8BE82`; that call-site placement is not suitable. Current local
Extender, canonical Fixes, APIShared and BFQoL sources have no competing hook at
the function entry or mapper CALL.

## Scoped camera-only integration [mod integration]

APIShared's owner-bound `MapperSelectionCameraEvents` broker can suppress only this
camera producer while preserving the original managed selection exactly once. Its
permanent pure-native entry bridge returns early only for scoped demand plus the
exact mapper return address. Demand zero and every other camera caller enter the
complete original trampoline. It preserves flags, scratch register and stack and
does not write the native view. Demand is serialized with the same engine monitor,
restored before Post, and cleared on native-world/session replacement; callback
errors retain owner-private cleanup state without retaining a failed suppression.
Unknown hashes and incomplete native/managed publication leave policies unavailable.

For an optional selected-owner jump, the public coordinate converter and public
Unity camera setter can be used synchronously after checking the current session,
world, scene, building owner/type/alive/global ID and target tile. This duplicates
the public SDK convenience setter's immediate camera/cache operations without its
queued dispatcher delay. Conversion failure must keep the camera stationary; it
must never substitute an unvalidated zero coordinate. Native producer suppression
must precede this optional movement. No in-game framing or runtime acceptance is
claimed by these source/native findings.
