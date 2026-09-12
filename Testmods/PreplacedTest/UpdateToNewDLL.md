# Native update contract

## Reference build and failure behavior

- `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Target Script Extender: 2.5.0, commit `5f02af6d074af7c741ebdaaccb48add39eba1bf4`
- All native diagnostics are installed in one transaction configured with `TransactionFailureMode.RollbackAndThrow`. A hash, signature, function-boundary, ABI, call-target, displacement, or data-range mismatch disables the complete native diagnostic set. Script-Extender event diagnostics continue and identify themselves as incomplete.
- Function patterns are searched uniquely in the executable image. At the reference hash the reference RVA is validated first; the existing resolver may use the unique pattern fallback if the RVA bytes were already detoured.

## Function detours

| Feature | Reference RVA | Semantic contract / pattern symbol |
| --- | ---: | --- |
| AIV allocation | `0x50680` | `AllocateSpecPattern` |
| AIV placement | `0x54EC0` | `SetPlacementPattern` |
| Candidate selection | `0x54F60`, `0x54DE0`, `0x55320`, `0x56670`, `0x57080` | `SelectBestFitPattern`, `TestSpecificCandidatePattern`, `LoadCandidatePattern`, `ApplyRotationPattern`, `EvaluateCandidateFitPattern` |
| Layout preparation and scheduling | `0x53D00`, `0x539B0` | `PrepareLayoutPattern`, `SchedulerPattern` |
| AIV execution | `0x51790`, `0x52270` | `ExecuteBuildStepPattern`, `AlternativeExecutionPattern` |
| Placement and validation | `0x5CD90`, `0x7B060`, `0xCC420` | `PlacementHelperPattern`, `ValidatorPattern`, `ResourceGatePattern` |
| Mapper wait gates | `0x414A0`, `0x41230`, `0x41380`, `0x41280` | `MapperWaitOnePattern` through `MapperWaitFourPattern` |
| Hovel and maintenance | `0x3B1D0`, `0x50340`, `0x504F0` | `DeleteHovelPattern`, `MaintenanceOnePattern`, `MaintenanceTwoPattern` |
| Building counts and reachability | `0xB8270`, `0xC3BF0`, `0xC8F50`, `0xC90E0` | `CountBuildingsPattern`, `PlacementReachabilityPattern`, `AccessibilitySweepPattern`, `BuildingAccessibilityPattern` |
| Economy phases | `0x50D80`, `0x50E00`, `0x50F90`, `0x51190`, `0x51270`, `0x51540` | `EconomyFarmPattern` through `EconomyWoodPattern` |
| Economy searches | `0x575B0`, `0x57B80`, `0x58020`, `0x58950` | `FarmSearchPattern`, `ResourceSearchPattern`, `WoodSearchPattern`, `NearbySearchPattern` |
| Scoped wood-score floor | `0x58057..0x58065` | RedBird context hook with `BeforeCallback`; exact 15-byte block `48 63 C2 4C 69 C8 3C 58 00 00 B8 67 66 66 66`, return at `0x58066`. The displaced instructions sign-extend the player ID through `RAX`, compute the player stride in `R9`, and load the following constant in `EAX`; `RCX` remains the state pointer and `EDX` still contains the player ID. The callback may change only `EBX` from Vanilla's local best-score initializer `-100` to `INT_MIN`, and only inside an eligible active wood-search overlay. |
| Inaccessible-building economy penalty | `0x3B270`, `0x3B360`, `0x55E10` | `InaccessibleBuildingCheckPattern`, `InaccessibleBuildingSelectionPattern`, `EconomyCellPenaltyPattern`; `0x55E10(state, worldX, worldY)` updates cell `byte+17` and `byte+13`, and its only audited callers are `0x3B270`, `0x3B360`, and `0xC8F50` |
| Audited AIV-only `byte+04` consumers | `0x583A0`, `0x58BE0` | Called only from `0x52270` and `0x54CC0`; remain outside the external-economy overlay. Reconfirm these call relationships before changing that scope. |
| Economy construction | `0x6D580` | `ConstructBuildingPattern` |
| PCL pair reachability | `0xE2610` | `RegionPairReachabilityPattern` |
| Economy-grid update | `0x50720` | `EconomyGridUpdatePattern` |
| Initial economy-availability census | `0x55FE0` | `InitializeEconomyAvailabilityPattern`; `void(state, playerId)`; four-neighbor traversal from the native per-player start, depth 60, expansion while signed `byte+04-byte+16 < 16`, then updates the five availability counts and their search-release fields |
| Dominant PCL selection | `0x572B0` | `SelectDominantPclPattern` |
| Building initialization | `0xC3FA0`, `0xC43A0`, `0xB8310` | `InitializePlayerBuildingsPattern`, `InitializeBuildingPattern`, `ClearBuildingRecordPattern` |
| Legacy player-state conversion | `0xD4290` | `LegacyPlayerStateCopyPattern`; `void(void)`; copies nine old player records into the current layout |
| Current player-state chore | `0x15B90` | `PlayerStateChorePattern`; `void(void)`; the complete-record call at `0x15C4A` passes base `0x379ADD0`, stride/size `0x583C`, and targets `0x1F5F0` |
| Chore field copy | `0x1F5F0..0x1F68D` | `ChoreCopyFieldPattern`; `void(manager, fieldAddress, size, bufferMode, direction)`; the copy call at `0x1F65D` targets `0x7140`, advances cursor `+0x370BF8`, and diagnostics are emitted only for overlaps with the current player-record array |
| Final map-start checkpoints | `0x115830`, `0x102C30`, `0x2A340`, `0x55FE0` | `InitializeUnitSubsystemPattern`, `ResetMapObjectSubsystemPattern`, `InitializePlayerPathingPattern`, `InitializeEconomyAvailabilityPattern`; passive timer comparisons surround the full economy-grid rebuild. In `0x94350`, call sites `0x96D2C`, `0x96D38`, `0x96D49`, `0x96D55`, and `0x96E30` must target these functions, `0x50720`, and finally `0x55FE0` in that order. |

## Forbidden former inline hook and branch-safety regression

- Do not restore the former inline context hook at RVA `0x7F074`. RedBird displaced 15 bytes, while native branches at `0x7F05D` and `0x7F072` target `0x7F07C` inside that span; the resulting mid-stub entry caused a verified access violation.
- Any future inline hook must first pass the inbound-branch safety check for its complete actual displaced span. Damage observation currently uses Script Extender events and timer comparisons and needs no inline writer hook.
- The regression test resolves both short-branch targets from the canonical DLL and proves that the former 15-byte span is unsafe. New inline instrumentation is forbidden unless its full backend-reported displaced span has no inbound target.
- The scoped wood-score hook is separately permitted only at `0x58057` with a backend-reported `DisplacedByteCount` of exactly 15. The complete audited branch-target set of `0x58020` has no target in the interior `0x58058..0x58065`. At callback time `RCX` is the state pointer and `EDX` still contains the player ID; `EDX` is overwritten by Vanilla immediately after the callback return. No register except `RBX/EBX` may be changed by the callback.
- `ActiveLayoutReferencePattern` at reference RVA `0x55F64` provides a RIP-relative data address. The decoded target must remain inside the mapped image.

## Hash-bound data and validation-only locations

These locations do not have independent semantic byte patterns. Their offsets are used only after the complete native hash matches and their full ranges are bounds-checked.

| Purpose | Reference RVA / range | Contract |
| --- | ---: | --- |
| Native path manager | `0x60AD660` | Portal manager base; 200-record maximum and all accessed record fields are range-checked. |
| PCL grid | `0x50EC690..0x51890D0` | Exactly 320,800 `ushort` entries; the range is virtual `.data` and is not file-backed. |
| Legacy player source records | `0x37CC7EC` | Nine records with stride `0x39F4`; the embedded `GamePlayerResources` begins at `+0x22FC`, so its timer at resources offset `+0x7E4` is record offset `+0x2AE0`. |
| Current serialized player records | `0x379ADD0` | Nine records with stride `0x583C`; embedded `GamePlayerResources` begins at `+0x22FC`. |
| Active player-resource records | `0x379D0CC` | RIP-relative base resolved through `0x55F64`; nine records with stride `0x583C`, timer at resources offset `+0x7E4`. |
| Native economy-search origins | `0x379AFA8`, `0x379AFAC` | Per-player world X/Y with stride `0x583C`; `0x55FE0`, `0x575B0`, `0x57B80`, and `0x58020` divide both values by five for their exact 160×160 BFS origin. |
| Wood frontier marker order | `0x58020` | Vanilla writes the current visit generation before testing `signed byte+04 < 16` and `byte+13 == 0`; rejected frontier cells therefore appear in the marker set but not in the queue. Preserve this ordering when auditing or patching the direct wood-search comparison. |
| Wood candidate score | `0x581A5..0x581DD` | Formal candidates increment the candidate counter before scoring. Score is `signed(byte+07) * 5 - parentDepth * 3`; if `byte+06 != 0`, positive scores are halved and nonpositive scores doubled. Vanilla initializes the best score to `-100` and accepts only a strict improvement. The scoped fix changes only that initializer for eligible preplaced-portal/breach searches; traversal, candidate predicates, scoring, result storage, cooldowns and downstream placement remain Vanilla. |
| Chore manager and field cursor | `0x8574320`, manager `+0x370BF8` | Current linear payload is at manager `+0x84CD8+cursor`; field transfers must retain the audited direction contract of `0x1F5F0`. |
| Loaded map format version | `0x32DC084` | Read-only `int` used to describe the guarded legacy conversion. |
| Legacy conversion call site | `0x96CE` | Validation only: must be an `E8 rel32` targeting `0xD4290`; it is not hooked. |

## Update procedure

1. Replace the canonical installed DLL first and regenerate the native semantic baseline for its full SHA-256.
2. Resolve every function semantically in the new baseline and update reference RVAs and patterns together.
3. Re-derive every RIP-relative or call target and independently re-audit all fixed structure offsets and data bases.
4. Revalidate the complete inline-hook overwrite span, incoming control-flow targets, registers, flags, displaced instructions, and RedBird behavior.
5. Update static signature, function-boundary, ABI, PE mapping, data-range, and model tests before allowing the transaction to commit on the new hash.
