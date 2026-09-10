# Native update contract

## Reference build and failure behavior

- `CrusaderDE.dll` SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`
- Target Script Extender: 2.3.0
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
| Economy construction | `0x6D580` | `ConstructBuildingPattern` |
| PCL pair reachability | `0xE2610` | `RegionPairReachabilityPattern` |
| Economy-grid update | `0x50720` | `EconomyGridUpdatePattern` |
| Dominant PCL selection | `0x572B0` | `SelectDominantPclPattern` |
| Building initialization | `0xC3FA0`, `0xC43A0`, `0xB8310` | `InitializePlayerBuildingsPattern`, `InitializeBuildingPattern`, `ClearBuildingRecordPattern` |
| Legacy player-state conversion | `0xD4290` | `LegacyPlayerStateCopyPattern`; `void(void)`; copies nine old player records into the current layout |

## Inline context hook and derived target

- `CrushedTimerWriterBlockPattern` resolves the surrounding damage block at reference RVA `0x7F052`.
- The observed store target is derived by the invariant delta `0x7F074 - 0x7F052` and must resolve to RVA `0x7F074` inside damage function `0x7EB00..0x7F87A`.
- The exact 15 displaced bytes, full instruction boundaries, RedBird displacement length, saved register mask, callback placement, and return path are validated before commit. Any difference aborts the transaction.
- `ActiveLayoutReferencePattern` at reference RVA `0x55F64` provides a RIP-relative data address. The decoded target must remain inside the mapped image.

## Hash-bound data and validation-only locations

These locations do not have independent semantic byte patterns. Their offsets are used only after the complete native hash matches and their full ranges are bounds-checked.

| Purpose | Reference RVA / range | Contract |
| --- | ---: | --- |
| Native path manager | `0x60AD660` | Portal manager base; 200-record maximum and all accessed record fields are range-checked. |
| PCL grid | `0x50EC690..0x51890D0` | Exactly 320,800 `ushort` entries; the range is virtual `.data` and is not file-backed. |
| Legacy player source records | `0x37CC7EC` | Nine records with stride `0x39F4`; timer field at `+0x7E4`. |
| Current player destination records | `0x379ADD0` | Nine records with stride `0x583C`; timer field at `+0x7E4`. |
| Loaded map format version | `0x32DC084` | Read-only `int` used to describe the guarded legacy conversion. |
| Legacy conversion call site | `0x96CE` | Validation only: must be an `E8 rel32` targeting `0xD4290`; it is not hooked. |

## Update procedure

1. Replace the canonical installed DLL first and regenerate the native semantic baseline for its full SHA-256.
2. Resolve every function semantically in the new baseline and update reference RVAs and patterns together.
3. Re-derive every RIP-relative or call target and independently re-audit all fixed structure offsets and data bases.
4. Revalidate the complete inline-hook overwrite span, incoming control-flow targets, registers, flags, displaced instructions, and RedBird behavior.
5. Update static signature, function-boundary, ABI, PE mapping, data-range, and model tests before allowing the transaction to commit on the new hash.
