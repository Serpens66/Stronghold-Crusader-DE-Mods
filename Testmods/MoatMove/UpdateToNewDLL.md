# Updating MoatMove for another native DLL

Current SE 2.6.0 integration supersedes the historical selection-detour entry below:
see [SE26_COMPATIBILITY.md](SE26_COMPATIBILITY.md). RVA 0x196870 is owned exclusively
by Script Extender; MoatMove uses six audited call-site inline adapters instead.

## Reference identity and scope

Feature owner for every entry below: MoatMove precise friendly/allied moat movement.
Reference SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Script Extender: 2.5.0, commit `5f02af6d074af7c741ebdaaccb48add39eba1bf4`.
Installed RedBird.X64: `1.1.0+0611afaa6da6f3e302ac40df00c88398880a81ef`.

Read CURRENT.md/CURRENT.json and verify the installed canonical DLL before using
these addresses. The user authorized an unchanged copy with a limited repeated
Vanilla-audit exception; no complete optimization audit is claimed. The behavioral
source and every copied hash are recorded in SOURCE_PROVENANCE.json.

## Resolution, signatures and failure contracts

- Named function targets use the corresponding `*Pattern` constant and `Resolve`
  call in the referenced source. On the exact hash, matching bytes at the reference
  RVA are used first. If they differ, Shared.NativePatternResolver searches executable
  PE sections for one match. The copied wrapper additionally requires the resolved
  RVA to equal its reference. Unknown hashes are rejected before construction.
- The group movement helper at `0x117C70` has an existing three-match fallback
  pattern. Its canonical entry bytes match. A fallback attempt is intentionally
  fail-closed; this extraction does not silently strengthen/change that signature.
- Observer entries use their inline exact byte strings, then an executable-section
  uniqueness check. A changed entry fails before installation; there is no alternate
  observer location. Reconstruction at `0xE32B0` validates all 56 bytes including its
  call to `0xE1640`; its detour target is the function entry, not that call site.
- Call-site entries validate complete five-byte E8 instructions and signed relative
  targets. Cursor gate and consumer entries are read/validated contexts, not global
  patches. Their exact byte arrays and derivations are in the linked source lines.
- Fixed data/table RVAs, manager prefixes and unit/tribe/moat/path-buffer offsets
  have no independent semantic relocation fallback. A code pattern alone does not
  prove these layouts. Keep the entire dependent feature hash-gated on an update.
- Rooting and constructor/transaction rollback are unchanged. Main initialization
  failure logs an error. Existing optional cursor/attack/work groups log their own
  failure and can leave ordinary movement installed: inspect every group in a test.

## Inline recovery machine contract

The sole inline hook is `[0x19664B, 0x196659)`, exactly 14 bytes:

`33 C0 8B D6 48 89 05 8E 70 F1 05 49 8B CF`

It displaces `xor eax,eax; mov edx,esi; mov [RIP+...],rax; mov rcx,r15`.
RedBird's installed implementation decodes at least max(requested length,14)
whole bytes. The decode-only installed-assembly test confirms DisplacedByteCount=14.
The constructor does not install a hook during this test. Function-detour prefix
checks are conservative 14-byte instruction boundaries, not an assumption that
all detour backends share X64InlineHook's implementation.

RSP is aligned at the site. RSI/RDI/RBP/R12-R15 remain live and are ABI-preserved;
volatile GPRs/flags are dead on the two audited continuations, and the containing
function has no XMM operands. The adapter reserves 0x30 bytes, reads original start
coordinates at the adjusted RSP+0xA0/+0xA8 before writing the argument slots at
RSP+0x20/+0x28, and passes manager R15 and 1-based unit ID ESI. A positive callback
branches to the original buffer initialization at 0x196585. Failure replays all
displaced instructions and returns through RedBird to 0x196659. The real emitted
73-byte body is decoded and checked, including its RIP-relative store.

Local branches and all hash-matched baseline cross-references show no entry into
the inline span's interior. Recheck full instruction/branch/ABI contracts on any
native or RedBird update. No runtime native patch, search or hook was optimized.

## Address inventory

Names retain the copied source's terminology, which is not an independent proof
of native parameter semantics. A further full feature audit must resolve remaining
semantic ambiguities before optimization. Source-relative line references identify
the authoritative pattern/byte validator or fixed-layout expression. Address-array
and inline-expression rows supplement named constants.

| Source symbol / derivation | Reference RVA | Source under src/ |
| --- | --- | --- |
| CentralMovementPlanRva | `0x18E1E0` | FriendlyMoatMovementRuntime.cs:149 |
| TribeFloodFillMembershipRva | `0x124740` | FriendlyMoatMovementRuntime.cs:150 |
| FirstGroupUnitOnCompletedMoatRva | `0x117BC0` | FriendlyMoatMovementRuntime.cs:151 |
| GetGroupUnitIdRva | `0x119F90` | FriendlyMoatMovementRuntime.cs:152 |
| GetTribeMovementModeRva | `0x117C70` | FriendlyMoatMovementRuntime.cs:153 |
| OrdinaryMovementGroupModeCallRva | `0x11B736` | FriendlyMoatMovementRuntime.cs:154 |
| GroupMoatModeCallRva | `0x11B666` | FriendlyMoatMovementRuntime.cs:155 |
| UnitStandingOnCompletedMoatRva | `0x196840` | FriendlyMoatMovementRuntime.cs:156 |
| RegionReachabilityRva | `0xE7C40` | FriendlyMoatMovementRuntime.cs:157 |
| CursorReachabilityRva | `0xE9FF0` | FriendlyMoatMovementRuntime.cs:158 |
| CursorTilePairFallbackSelectionRva | `0x196870` | FriendlyMoatMovementRuntime.cs:159 |
| SelectionCanDigMoatRva | `0x191C00` | FriendlyMoatMovementRuntime.cs:160 |
| SelectionCanDigMoatCallRva | `0x8D3CE` | FriendlyMoatMovementRuntime.cs:161 |
| CursorTilePairReachabilityRva | `0xE2CA0` | FriendlyMoatMovementRuntime.cs:162 |
| GetRepresentativeSelectedUnitRva | `0x18D460` | FriendlyMoatMovementRuntime.cs:163 |
| CursorRegionPrecheckRva | `0xE9D90` | FriendlyMoatMovementRuntime.cs:164 |
| PathBuilderRva | `0xF4930` | FriendlyMoatMovementRuntime.cs:165 |
| GroundPathBuilderRva | `0xDA590` | FriendlyMoatMovementRuntime.cs:166 |
| AlternativePathBuilderRva | `0xDB650` | FriendlyMoatMovementRuntime.cs:167 |
| GetMoatIdAtTileRva | `0x69560` | FriendlyMoatMovementRuntime.cs:168 |
| AttackApproachFloodBuilderRva | `0xDBC60` | FriendlyMoatMovementRuntime.cs:169 |
| BuildingApproachBuilderRva | `0xDA020` | FriendlyMoatMovementRuntime.cs:170 |
| BuildingCandidateConsumerRva | `0x123090` | FriendlyMoatMovementRuntime.cs:171 |
| RegionPairReachabilityRva | `0xE2610` | FriendlyMoatMovementRuntime.cs:172 |
| AttackApproachFloodCallRva | `0x11EE47` | FriendlyMoatMovementRuntime.cs:173 |
| AttackApproachFloodAlternativeCallRva | `0x11F46B` | FriendlyMoatMovementRuntime.cs:174 |
| BuildingApproachCallRva | `0x11FF9A` | FriendlyMoatMovementRuntime.cs:175 |
| BuildingCandidateConsumerCallRva | `0x11FFA7` | FriendlyMoatMovementRuntime.cs:176 |
| BuildingCandidateConsumerAlternativeCallRva | `0x1206DF` | FriendlyMoatMovementRuntime.cs:177 |
| BuildingCandidateConsumerForceCallRva | `0x120CCD` | FriendlyMoatMovementRuntime.cs:178 |
| AttackFloodRegionPairCallRva | `0xDBF0D` | FriendlyMoatMovementRuntime.cs:179 |
| AttackFloodTilePairCallRva | `0xDBF33` | FriendlyMoatMovementRuntime.cs:180 |
| BuildingApproachRegionPairCallRva | `0xDA1F9` | FriendlyMoatMovementRuntime.cs:181 |
| BuildingApproachTilePairCallRva | `0xDA232` | FriendlyMoatMovementRuntime.cs:182 |
| BuildingApproachAlternativeRegionPairCallRva | `0xDA47C` | FriendlyMoatMovementRuntime.cs:183 |
| BuildingApproachAlternativeTilePairCallRva | `0xDA4B1` | FriendlyMoatMovementRuntime.cs:184 |
| OrdinaryMovementLadderPrecheckCallRva | `0x11B768` | FriendlyMoatMovementRuntime.cs:185 |
| OrdinaryMovementLadderReachabilityCallRva | `0x11B785` | FriendlyMoatMovementRuntime.cs:186 |
| BuildingConsumerFallbackBuilderCallRva | `0x123102` | FriendlyMoatMovementRuntime.cs:187 |
| BuildingConsumerGroundBuilderCallRva | `0x12312C` | FriendlyMoatMovementRuntime.cs:188 |
| BuildingCursorReachabilityRva | `0xB70C0` | FriendlyMoatMovementRuntime.cs:189 |
| BuildingCursorReachabilityCallRva | `0x8DFF6` | FriendlyMoatMovementRuntime.cs:190 |
| CombatFinishResumeRva | `0x1853F0` | FriendlyMoatMovementRuntime.cs:191 |
| CombatFinishPostCombatCallRva | `0x18540D` | FriendlyMoatMovementRuntime.cs:192 |
| PostCombatRepathRva | `0x1976C0` | FriendlyMoatMovementRuntime.cs:193 |
| PostCombatMoveHereCallRva | `0x19772B` | FriendlyMoatMovementRuntime.cs:194 |
| CursorMoveStagerRva | `0x195E30` | FriendlyMoatMovementRuntime.cs:195 |
| NativeSpecialStructurePredicateRva | `0x107160` | FriendlyMoatMovementRuntime.cs:196 |
| NativeSpecialStructureContextRva | `0x32DE440` | FriendlyMoatMovementRuntime.cs:197 |
| NativeBuildingTypeBiasRva | `0x64CCCDE` | FriendlyMoatMovementRuntime.cs:198 |
| CursorMoveStagerRegionPairCallRva | `0x195F46` | FriendlyMoatMovementRuntime.cs:199 |
| Inline expression / observer / address array | `0x8F7BA` | FriendlyMoatMovementRuntime.cs:200 |
| Inline expression / observer / address array | `0x8FD3C` | FriendlyMoatMovementRuntime.cs:200 |
| Inline expression / observer / address array | `0x8FDC6` | FriendlyMoatMovementRuntime.cs:200 |
| Inline expression / observer / address array | `0x8FE54` | FriendlyMoatMovementRuntime.cs:200 |
| Inline expression / observer / address array | `0xE7F60` | FriendlyMoatMovementRuntime.cs:200 |
| DirectFillApproachRva | `0xE7F60` | FriendlyMoatMovementRuntime.cs:202 |
| DirectFillApproachRegionPairCallRva | `0xE81EB` | FriendlyMoatMovementRuntime.cs:203 |
| DirectFillApproachCommandCallRva | `0x120E9D` | FriendlyMoatMovementRuntime.cs:204 |
| MovementTerrainPhaseRva | `0x19B506` | FriendlyMoatMovementRuntime.cs:205 |
| MovementCadenceRva | `0x184203` | FriendlyMoatMovementRuntime.cs:206 |
| MovementSubstepRva | `0x1855A0` | FriendlyMoatMovementRuntime.cs:207 |
| MovementAdditionalSubstepsRva | `0x1857AA` | FriendlyMoatMovementRuntime.cs:208 |
| MoatPathConsumptionReadRva | `0x185934` | FriendlyMoatMovementRuntime.cs:209 |
| MoatPathConsumptionPersistRva | `0x19670C` | FriendlyMoatMovementRuntime.cs:210 |
| CursorCurrentTileFlagGateRva | `0x8F388` | FriendlyMoatMovementRuntime.cs:211 |
| CursorCurrentTileFlagGateJumpRva | `0x8F393` | FriendlyMoatMovementRuntime.cs:212 |
| AttackUnitPairGateJumpRva | `0x8D72B` | FriendlyMoatMovementRuntime.cs:213 |
| AttackBuildingPairGateJumpRva | `0x8E2C6` | FriendlyMoatMovementRuntime.cs:214 |
| AttackAlternativePairGateJumpRva | `0x8E557` | FriendlyMoatMovementRuntime.cs:215 |
| TileFlagsRva | `0x48F71B0` | FriendlyMoatMovementRuntime.cs:216 |
| MovementTargetAvailabilityRva | `0x3A11EA4` | FriendlyMoatMovementRuntime.cs:217 |
| NativeMovementMaskRva | `0x51890D0` | FriendlyMoatMovementRuntime.cs:218 |
| RowLookupRva | `0x402FF2C` | FriendlyMoatMovementRuntime.cs:219 |
| NativeBuildingLayerRva | `0x4B6AA50` | FriendlyMoatMovementRuntime.cs:220 |
| NativeHeightLayerRva | `0x4DDD350` | FriendlyMoatMovementRuntime.cs:221 |
| NativeDirectionMaskRva | `0x312620` | FriendlyMoatMovementRuntime.cs:222 |
| CursorTargetXRva | `0x3A11E2C` | FriendlyMoatMovementRuntime.cs:223 |
| CursorTargetYRva | `0x3A11E30` | FriendlyMoatMovementRuntime.cs:224 |
| PathRegionGridRva | `0x50EC690` | FriendlyMoatMovementRuntime.cs:225 |
| MoatPathModeRva | `0x60AD6E4` | FriendlyMoatMovementRuntime.cs:226 |
| NativePathManagerRva | `0x60AD660` | FriendlyMoatMovementRuntime.cs:227 |
| NativeUnitManagerRva | `0x67E8400` | FriendlyMoatMovementRuntime.cs:228 |
| NativeTribeManagerRva | `0x7CC6720` | FriendlyMoatMovementRuntime.cs:229 |
| Inline expression / observer / address array | `0xDB29A` | FriendlyMoatMovementRuntime.cs:231 |
| Inline expression / observer / address array | `0xDB2EC` | FriendlyMoatMovementRuntime.cs:231 |
| Inline expression / observer / address array | `0xDB37A` | FriendlyMoatMovementRuntime.cs:231 |
| Inline expression / observer / address array | `0xE1856` | FriendlyMoatMovementRuntime.cs:233 |
| Inline expression / observer / address array | `0xE18DF` | FriendlyMoatMovementRuntime.cs:233 |
| Inline expression / observer / address array | `0xE195B` | FriendlyMoatMovementRuntime.cs:233 |
| Module-relative expression | `0xE32B0` | FriendlyMoatMovementRuntime.cs:1079 |
| Module-relative expression | `0x51D75F0` | MoatPlacement.cs:100 |
| Module-relative expression | `0x9302C4` | MoatPlacement.cs:101 |
| Inline expression / observer / address array | `0x118E00` | MoatPlacement.cs:105 |
| Inline expression / observer / address array | `0x181890` | MoatPlacement.cs:108 |
| Inline expression / observer / address array | `0xF03C0` | MoatPlacement.cs:111 |
| FindMoatWorkTargetRva | `0x69D60` | MoatWorkTargetSelection.cs:26 |
| ResolveMoatWorkTileRva | `0x6AF60` | MoatWorkTargetSelection.cs:27 |
| HasFillMoatApproachRva | `0x6C490` | MoatWorkTargetSelection.cs:28 |
| FillApproachCallRva | `0x69EE6` | MoatWorkTargetSelection.cs:29 |
| DigStandingOnMoatCallRva | `0x69F91` | MoatWorkTargetSelection.cs:30 |
| DigRegionSearchCallRva | `0x69FE3` | MoatWorkTargetSelection.cs:31 |
| DigRegionPairCallRva | `0x6A014` | MoatWorkTargetSelection.cs:32 |
| DigAlternativeRegionPairCallRva | `0x6A0C2` | MoatWorkTargetSelection.cs:33 |
| MovementPlannerLowFlagGateRva | `0x196464` | MoatWorkTargetSelection.cs:34 |
| MovementPlannerStructureFlagGateRva | `0x19648D` | MoatWorkTargetSelection.cs:35 |
| Inline expression / observer / address array | `0xE1D30` | NativeFormationSlots.cs:36 |
| Inline expression / observer / address array | `0xE0970` | NativeFormationSlots.cs:41 |
| UnitTypeUpdateDispatchRva | `0x18410C` | NativeMovementCadenceResolver.cs:17 |
| Module-relative expression | `0x64CCED2` | NativeMovementRecovery.cs:51 |
| Inline expression / observer / address array | `0xD90D0` | NativeMovementRecovery.cs:54 |
| Inline expression / observer / address array | `0x59210` | NativeMovementRecovery.cs:60 |
| Inline expression / observer / address array | `0x61E70` | NativeMovementRecovery.cs:68 |
| Inline expression / observer / address array | `0xDAA50` | NativeMovementRecovery.cs:75 |
| failureRva | `0x19664B` | NativeMovementRecovery.cs:82 |
| Module-relative expression | `0x196585` | NativeMovementRecovery.cs:143 |

## Fire effects (option AvoidFire, `src/FireRouteRules.cs`)

Feature owner: MoatMove fire route rules. Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 29.10.2026). Read-only: the scan never writes native memory.

| Entry | Reference RVA | Pattern and derivation |
|---|---|---|
| Effect removal pass | `0xA2E60` | `RemovalPattern` (48 bytes, displacements wildcarded). `lea rdi,[rip+d32]` at +0x0F gives the effect manager (`0x2F7A680`); the pattern also pins `imul rcx,rax,0E8h` (record size) and `cmp word [rcx+rdi+3Ch],2` (state field). |
| Fire of a tile | `0xA02D0` | `LookupPattern` (50 bytes). `lea rdx,[rip+d32]` at +3 gives the per-tile effect list heads (`0x4CF23F0`, after the records); the pattern pins `imul r8,rdx,0E8h`, type `cmp word [..+3Eh],9` and the list link `movsx eax,word [..+32h]`. |

Record layout used: slots 1..5999 (`0xA2D80` relinks 0x19..5999, `0xA2E60` purges 1..0x18), state +0x3C (0 = free,
2 = removed in the same pass), type +0x3E (9 = fire, written by `0x9C120`, `0x9C450`, `0x9DCF0` through
`0x9B2B0`), native tile id +0x60 (int; `0x9F5B0` unlinks by it). Tile ids convert to coordinates with the
Extender's `GetTileVectorFromId` and are checked back with `GetTileId`. Both entries resolve through
`Shared.NativePatternResolver` (reference RVA first, unique executable-section pattern fallback); the derived
manager must lie before the tile heads by at least 6000 records. Any failure logs an error and registers no
fire rules; movement keeps working. The record layout has no second semantic source beyond these anchors, so
the feature stays hash-gated with the rest of MoatMove.
## Assassin single routes through the anchor core (`src/AnchorRouting.cs`, `TryPublishAnchorSingle`)

Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2` (audit 30.10.2026, summary in
PathfindingRoadmap.md). No new RVA: the decision reads path-manager fields of the builder call already validated
for anchor publication (`+0x80`, `+0x84`, `+0x88`, `+0x94`) and the path region grid (`0x50EC690`, through
BugfixesAndQoL's `pathRegionGrid`). In `0xF4930`, an Assassin request (`+0x88 != 0`, `+0x80 == 0`) with start and
target in one region runs `0xDA590` first; only its failure reaches `0xD9C40`. The core's ground route replaces
exactly that first search; `+0x94 != 0` (second pass through `0xDB650`) and different regions keep Vanilla. Recheck
this branch order and the four fields on a new DLL; the feature stays hash-gated with MoatMove.

## Failed ground builds (`src/GroundFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallGroundFailureProofs`)

Feature owner: MoatMove anchor mode. Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 30.10.2026, summary in PathfindingRoadmap.md). Inside one group move a start in a proven-closed failure set
replays `0xF4930`'s ground failure path without `0xDAAC0`/`0xDA590`; the region gate (`0xE2F60`) and the direct
attempt (`0xF32B0` for profile 0, `0xF3060` otherwise) still run natively, once each, as in Vanilla.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Region gate | `0xF49C2` | `41 8B D2 44 8B C8 E8 93 E5 FE FF 85 C0 0F 84`: `mov edx,r10d` (player), `mov r9d,eax` (target region), `call 0xE2F60`, `test`, `jz` (return 0 without writes). Called through the same address (a hook on it runs as in Vanilla's call); it increments `manager+0xC8`. |
| Profile branch | `0xF4A09` | `85 F6 74 7F E8 4E E6 FF FF 85 C0 75 7F`: `test esi,esi` (profile), `jz` to `F32B0`, `call 0xF3060`, `test`, `jnz` (success). |
| Direct attempt call | `0xF4A8C` | `E8 1F E8 FF FF 85 C0 74 81 FF 83 A8 00 00 00`: `call 0xF32B0`, `test`, `jz`, `inc [rbx+0A8h]` (success counter, replayed on a direct success). |
| Direct failure counter | `0xF4A2B` | `FF 83 AC 00 00 00`: `inc [rbx+0ACh]` (replayed). |
| Search flag | `0xF4A35` | `89 6B 7C E8 83 2F F1 FF`: `mov [rbx+7Ch],ebp` (0), `call 0x79C0` (scratch object `0x34A9F50`, not replayed: every reader writes it first). |
| Skipped searches | `0xF4AF5`, `0xF4ACF`, `0xF4A78` | `call 0xDAAC0` (`E8 C6 5F FE FF`), `call 0xDA590` (`E8 BC 5A FE FF`; with `+0x80 != 0` directly `E8 13 5B FE FF`, same failure writes). |
| Direct attempts | `0xF32B0`, `0xF3060` | Entries `48 89 5C 24 08 48 89 6C 24 10` and `48 89 5C 24 10 48 89 6C 24 18`; called natively (`manager, sx, sy, tx, ty`). Both step only over set direction bits of `0x51890D0` and write the unit's route buffer. |
| Search stamps and connections | `0xDA783` | `movsx r11d,byte [rdi+rdx+51890D0h]; cmp [rdx+rdi*2+52C254Eh],r8w` (BugfixesAndQoL's cursor proof contract): stamps `0x52C2550` (`nativeProbeGrid + 0x9CA40`), direction bytes `0x51890D0`. |
| Neighbour offsets | `0x405EDB0` | Per-row direction offsets (as BugfixesAndQoL's cursor proof). |

Path manager fields: start/target `+0x08..+0x14`, modes `+0x80`/`+0x84`/`+0x88`, generation `+0x04`, search flag
`+0x7C`, counters `+0xA8`/`+0xAC`, route length `+0x155F68`. Region gate inputs: `0x50EC690` through BugfixesAndQoL's
`pathRegionGrid` (read as signed shorts, target region 0 returns 0 without `0xE2F60`). There is no
pattern fallback: the replay is bound to the audited instruction sequence, so any byte mismatch logs an error
and leaves the feature off (every member keeps Vanilla's builder). On a new DLL repeat the audit: F4930's ground
path and writes, F32B0 and DAAC0/DA590 edge rules, the readers of search stamps/distances/queue/continuation and
of `0x34A9F50`, and BugfixesAndQoL's gate filter (`GateNativeSearchScopes`, `IsDirectionAllowed`).

## Failed moat-mode builds (`src/MoatFailureProofs.cs`, installed by `FriendlyMoatTraversalProvider.InstallMoatFailureProofs`)

Feature owner: MoatMove anchor mode. Reference SHA-256 `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
(audit 30.10.2026, `GROUND_ROUTE_BUILDERS.md` "0xF4930 moat mode and 0xDAFD0"). A moat-mode request whose start lies in
the stamped component of an earlier failed `0xDAFD0` search, with unchanged edge inputs on its border, replays `0xF4930`'s
moat-mode path natively up to `0xDAAC0` and skips `0xDAFD0`.

| Entry | Reference RVA | Validated bytes and use |
|---|---|---|
| Moat-mode entry | `0xF49D5` | `xor ebp,ebp; mov [rbx+7Ch],1; mov [rbx+155F68h],ebp; cmp [rbx+84h],ebp; jne` (23 bytes, replayed writes). |
| Failure writes, 79C0, budget | `0xF4A16` | 65 bytes: `lea rcx,[0x34A9F50]`, `inc [rbx+0ACh]`, `mov [rbx+7Ch],ebp`, `call 0x79C0` (called natively: `rectangle, sx, sy, tx, ty`), `mov eax,[0x34A9F5C]; add eax,4; mov [rbx+155F68h],ebp; imul eax,eax; cmp [rbx+80h],ebp; je`. |
| DAAC0 call | `0xF4AA0`, `0xF4AD6` | `cmp [rbx+88h],ebp; je` and 40 bytes ending in `call 0xDAAC0; test; jnz` (budget `eax+eax`, mode 0; called natively through the same address). |
| DAFD0 dispatch | `0xF4AFE` | 92 bytes: `+0x88`/`+0x84` checks and `call 0xDAFD0` with 400,000 and mode 0 (skipped for proven requests). |
| Failure return | `0xF4A7D` | `test eax,eax; jnz; xor eax,eax; jmp` (return 0). |
| DAFD0 validity, generation, budget, stamps | `0xDB004`, `0xDB070`, `0xDB164`, `0xDB519` | `0x3A11EA4[y*800+x]` test, `inc [r15+4]` with the 32,000 wrap, ring head compare with `param_6` and `cmp r13d,0x4E51F`, distance/stamp writes (`0x5225B10`, `0x52C2550`). |
| Edge inputs | `0xDB293`, `0xDB442`, `0x107160`, `0xC07C0` | `lea rcx,[0x32DE440]; call 0x107160`; `movsx edx,[0x4B6AA50]; lea rcx,[0x64CCBB0]; call 0xC07C0`; the predicate (47 bytes, `lea rdx,[rip+0x049C6EA6]` = `0x4ACE010`, stride `0x9C`, word `+0x6A`); `0xC07C0`'s type word (`+0x12E`, stride `0x32C`). |

Tables read for the border hash: `0x51890D0` (direction bytes), `0x48F71B0` (flags), `0x4DDD350` (heights), `0x4B6AA50`
(buildings) with `0x64CCCDE + b*0x32C` (type), `0x4ACE010` with `0x32DE440 + i*0x9C + 0x6A` (special-structure state);
`0x405EDB0` per-row neighbour offsets (also index `y*8 - 1`, as DAFD0's side test reads it), `0x402FF2C` row starts and
the Extender's column table. DAFD0's and DAAC0's direction loads (`0xDB242`, `0xDACB2`) are not pinned: APIShared's gate
filter hooks them. There is no pattern fallback; any byte mismatch logs an error and leaves the feature off. On a new
DLL repeat the audit: F4930's moat path and writes, DAFD0's exhaustiveness (ring size vs budget) and every edge input,
DAAC0's relation to DAFD0, and the readers of search stamps (see "Failed ground builds").
