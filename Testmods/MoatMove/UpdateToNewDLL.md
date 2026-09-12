# Updating MoatMove for another native DLL

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
