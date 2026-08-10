using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;

namespace ActiveAIVDetector
{
    internal sealed unsafe class AivPlacementOracle
    {
        private const string SelectBestFitPattern =
            "44 88 44 24 18 89 54 24 10 55 56 41 54 41 55 41 56 41 57 48 83 EC 58";
        private const string TestSpecificCandidatePattern =
            "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 " +
            "48 89 7C 24 20 41 56 48 83 EC 20 41 8B F0 48 63 EA " +
            "48 8B F9 4C 8D 89 44 98 1B 00";
        private const string LoadCandidatePattern =
            "40 53 56 57 41 55 48 83 EC 38 8B 05 ?? ?? ?? ?? " +
            "48 8D 0D ?? ?? ?? ?? 41 8B D8 48 63 FA 85 C0";
        private const string ApplyRotationPattern =
            "85 D2 0F 84 ?? ?? ?? ?? 53 48 83 EC 20 48 89 74 24 30 " +
            "48 8B D9 48 89 7C 24 38 83 FA 06";
        private const string EvaluateCandidateFitPattern =
            "89 54 24 10 53 55 56 57 41 54 41 55 41 56 41 57 " +
            "48 83 EC 48 45 33 C9 48 8D 81 44 98 1B 00";
        private const string BuildingPlacementValidatorPattern =
            "40 53 55 56 57 41 56 48 83 EC 40 33 C0 49 63 E8 " +
            "83 BC 24 90 00 00 00 02";
        private const string ExecuteBuildStepPattern =
            "40 53 55 56 57 41 54 41 55 41 56 41 57 48 83 EC 78 4C 63 F2";
        private const string OrganismRecordTableReferencePattern =
            "48 8D 05 ?? ?? ?? ?? 41 B8 9C 00 00 00 48 03 D0";
        private const string ActiveLayoutIndexReferencePattern =
            "48 63 F2 48 8D 05 ?? ?? ?? ?? 4C 69 CE 3C 58 00 00";

        private const int SelectBestFitRva = 0x54F10;
        private const int TestSpecificCandidateRva = 0x54D90;
        private const int LoadCandidateRva = 0x552D0;
        private const int ApplyRotationRva = 0x56620;
        private const int EvaluateCandidateFitRva = 0x57030;
        private const int BuildingPlacementValidatorRva = 0x7B010;
        private const int ExecuteBuildStepRva = 0x51740;
        private const int OrganismRecordTableReferenceRva = 0x15A27;
        private const int ActiveLayoutIndexReferenceRva = 0x55F14;

        private const int AivSpecStride = 0x6D98;
        private const int PlayerIdOffset = 0x04;
        private const int OrientationOffset = 0x0C;
        private const int CandidateIdOffset = 0x10;
        private const int PlacementStateOffset = 0x14;
        private const int OriginXOffset = 0x28;
        private const int OriginYOffset = 0x2C;
        private const int KeepXOffset = 0x30;
        private const int KeepYOffset = 0x34;
        private const int MapperGridOffset = 0x3DA6C;
        private const int ScoreGridOffset = 0x4288C;
        private const int CellResultGridOffset = 0x1B9844;
        private const int EvaluatedCellCountOffset = 0x5B4F8;
        private const int BlockedCellCountOffset = 0x5B4FC;
        private const int CompleteFitScore = 999999;
        private const int AivGridSize = 100;
        private const int FixedMapTileCount = 320800;
        private const int OrganismRecordStride = 0x9C;
        private const int OrganismClassOffset = 0x46;
        private const int TerrainFlagsOffset = 0x898400;
        private const int HeightOffset = 0xD7E5A0;
        private const int DefaultHeightOffset = 0xDCCAC0;
        private const int OrganismGridOffset = 0xA6F260;
        private const int BuildingGridOffset = 0xB0BCA0;
        private const int EntityGridOffset = 0xBF6C00;
        private const int OwnerGridOffset = 0xE1AFE0;
        private const int PlayerRuntimeStateStride = 0x583C;
        private const int PreparedLayoutFrameCount = 0x922;
        private const int PreparedEntryBaseOffset = 0x38;
        private const int PreparedEntrySize = 0x0C;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SelectBestFitDelegate(
            ulong aivStateAddress,
            int aivSpecIndex,
            byte tryOtherRotations);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint TestSpecificCandidateDelegate(
            ulong aivStateAddress,
            int aivSpecIndex,
            int candidateId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void LoadCandidateDelegate(
            ulong aivStateAddress,
            int zeroBasedPlayerId,
            int candidateId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ApplyRotationDelegate(ulong aivStateAddress, int orientation);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int EvaluateCandidateFitDelegate(ulong aivStateAddress, int aivSpecIndex);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingPlacementValidatorDelegate(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ExecuteBuildStepDelegate(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced);

        private readonly ManualLogSource log;
        private readonly Action<OracleSelectionSnapshot> onSelectionCompleted;
        private readonly Action<OraclePrebuildFrameTraceSnapshot> onPrebuildFrameCaptured;
        private readonly OracleCellTraceOptions cellTraceOptions;
        private readonly OraclePrebuildTraceOptions prebuildTraceOptions;
        private readonly ulong organismRecordTableAddress;
        private readonly ulong activeLayoutIndexBaseAddress;
        private readonly ulong nativeLibraryBase;
        private HookRef<X64ManagedFunctionDetourAOB<SelectBestFitDelegate>> selectBestFitHook =
            new HookRef<X64ManagedFunctionDetourAOB<SelectBestFitDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<TestSpecificCandidateDelegate>> testSpecificCandidateHook =
            new HookRef<X64ManagedFunctionDetourAOB<TestSpecificCandidateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<LoadCandidateDelegate>> loadCandidateHook =
            new HookRef<X64ManagedFunctionDetourAOB<LoadCandidateDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<ApplyRotationDelegate>> applyRotationHook =
            new HookRef<X64ManagedFunctionDetourAOB<ApplyRotationDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<EvaluateCandidateFitDelegate>> evaluateCandidateFitHook =
            new HookRef<X64ManagedFunctionDetourAOB<EvaluateCandidateFitDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>>
            buildingPlacementValidatorHook =
                new HookRef<X64ManagedFunctionDetourAOB<BuildingPlacementValidatorDelegate>>();
        private HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>> executeBuildStepHook =
            new HookRef<X64ManagedFunctionDetourAOB<ExecuteBuildStepDelegate>>();

        private OracleSelectionSession activeSession;
        private ValidatorTraceContext activeValidatorTrace;
        private long nextSequence;
        private int cellTraceCaptureCount;
        private readonly HashSet<int> cellTraceCapturedPlayerIds = new HashSet<int>();
        private readonly Dictionary<int, PlacementStateObservation>
            prebuildPlacementStateByPlayerId = new Dictionary<int, PlacementStateObservation>();
        private readonly Dictionary<int, long> lastSelectionSequenceByPlayerId =
            new Dictionary<int, long>();
        private PlacementStateObservation activePrebuildPlacementStateObservation;
        private ulong activePrebuildCaptureAivStateAddress;
        private int activePrebuildCapturePlayerId = -1;
        private int activePrebuildCaptureSequence;
        private int activePrebuildCaptureFrameNumber;
        private int prebuildTraceCaptureCount;
        private int executeBuildStepDepth;
        private bool executeBuildStepReentrancyLogged;
        private bool prebuildPointerWarningLogged;
        private readonly ushort[] prebuildBeforeBuildingGrid = new ushort[FixedMapTileCount];
        private readonly ushort[] prebuildAfterBuildingGrid = new ushort[FixedMapTileCount];
        private bool callbackFailureLogged;

        public AivPlacementOracle(
            ManualLogSource log,
            Action<OracleSelectionSnapshot> onSelectionCompleted,
            OracleCellTraceOptions cellTraceOptions,
            Action<OraclePrebuildFrameTraceSnapshot> onPrebuildFrameCaptured,
            OraclePrebuildTraceOptions prebuildTraceOptions,
            IntPtr nativeLibraryHandle,
            ReadOnlySpan<byte> nativeLibraryMemory)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.onSelectionCompleted = onSelectionCompleted ??
                throw new ArgumentNullException(nameof(onSelectionCompleted));
            this.cellTraceOptions = cellTraceOptions ??
                throw new ArgumentNullException(nameof(cellTraceOptions));
            this.onPrebuildFrameCaptured = onPrebuildFrameCaptured ??
                throw new ArgumentNullException(nameof(onPrebuildFrameCaptured));
            this.prebuildTraceOptions = prebuildTraceOptions ??
                throw new ArgumentNullException(nameof(prebuildTraceOptions));
            nativeLibraryBase = unchecked((ulong)nativeLibraryHandle.ToInt64());
            ValidateReference(nativeLibraryMemory, SelectBestFitPattern, SelectBestFitRva, "select best fit");
            ValidateReference(nativeLibraryMemory, TestSpecificCandidatePattern, TestSpecificCandidateRva, "test specific candidate");
            ValidateReference(nativeLibraryMemory, LoadCandidatePattern, LoadCandidateRva, "load candidate");
            ValidateReference(nativeLibraryMemory, ApplyRotationPattern, ApplyRotationRva, "apply rotation");
            ValidateReference(nativeLibraryMemory, EvaluateCandidateFitPattern, EvaluateCandidateFitRva, "evaluate candidate fit");
            ValidateReference(nativeLibraryMemory, BuildingPlacementValidatorPattern, BuildingPlacementValidatorRva, "building placement validator");
            if (prebuildTraceOptions.Enabled)
                ValidateReference(nativeLibraryMemory, ExecuteBuildStepPattern, ExecuteBuildStepRva, "execute build step");
            organismRecordTableAddress = ResolveUniqueRipRelativeAddress(
                nativeLibraryHandle,
                nativeLibraryMemory,
                "organism record table",
                OrganismRecordTableReferencePattern,
                OrganismRecordTableReferenceRva,
                instructionOffset: 0,
                requiredBytes: 4000 * OrganismRecordStride);
            activeLayoutIndexBaseAddress = ResolveUniqueRipRelativeAddress(
                nativeLibraryHandle,
                nativeLibraryMemory,
                "active AIV layout index table",
                ActiveLayoutIndexReferencePattern,
                ActiveLayoutIndexReferenceRva,
                instructionOffset: 3,
                requiredBytes: 9 * PlayerRuntimeStateStride);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Active AIV native globals resolved from unique RIP-relative signatures: " +
                $"organismRecordTable=0x{organismRecordTableAddress:X}, " +
                $"activeLayoutIndexBase=0x{activeLayoutIndexBaseAddress:X}.");
        }

        public void RegisterHooks(HookTransaction transaction)
        {
            if (transaction == null)
                throw new ArgumentNullException(nameof(transaction));

            transaction.AddDetour(ref selectBestFitHook, nativeLibraryBase + SelectBestFitRva, SelectBestFit);
            transaction.AddDetour(
                ref testSpecificCandidateHook,
                nativeLibraryBase + TestSpecificCandidateRva,
                TestSpecificCandidate);
            transaction.AddDetour(ref loadCandidateHook, nativeLibraryBase + LoadCandidateRva, LoadCandidate);
            transaction.AddDetour(ref applyRotationHook, nativeLibraryBase + ApplyRotationRva, ApplyRotation);
            transaction.AddDetour(
                ref evaluateCandidateFitHook,
                nativeLibraryBase + EvaluateCandidateFitRva,
                EvaluateCandidateFit);
            transaction.AddDetour(
                ref buildingPlacementValidatorHook,
                nativeLibraryBase + BuildingPlacementValidatorRva,
                BuildingPlacementValidator);
            if (prebuildTraceOptions.Enabled)
            {
                // Keep the extra native detour absent unless this one-run diagnostic is explicit.
                transaction.AddDetour(
                    ref executeBuildStepHook,
                    nativeLibraryBase + ExecuteBuildStepRva,
                    ExecuteBuildStep);
            }
        }

        public void ValidateHooks()
        {
            List<string> missing = new List<string>();
            AddMissing(missing, selectBestFitHook.Success, "c_game_aiv_select_best_fit");
            AddMissing(missing, testSpecificCandidateHook.Success, "c_game_aiv_test_specific_candidate");
            AddMissing(missing, loadCandidateHook.Success, "c_game_aiv_load_candidate");
            AddMissing(missing, applyRotationHook.Success, "c_game_aiv_apply_rotation");
            AddMissing(missing, evaluateCandidateFitHook.Success, "c_game_aiv_evaluate_candidate_fit");
            AddMissing(
                missing,
                buildingPlacementValidatorHook.Success,
                "c_game_building_placement_validator");
            if (prebuildTraceOptions.Enabled)
                AddMissing(missing, executeBuildStepHook.Success, "c_game_aiv_execute_build_step");

            if (missing.Count != 0)
            {
                throw new InvalidOperationException(
                    "The native AIV placement oracle signatures were not found: " +
                    string.Join(", ", missing) + ".");
            }
        }

        private void SelectBestFit(
            ulong aivStateAddress,
            int aivSpecIndex,
            byte tryOtherRotations)
        {
            OracleSelectionSession session = TryBeginSession(
                "SelectBestFit",
                aivStateAddress,
                aivSpecIndex,
                tryOtherRotations != 0,
                null);
            try
            {
                selectBestFitHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    aivSpecIndex,
                    tryOtherRotations);
            }
            finally
            {
                CompleteSession(session, null);
            }
        }

        private uint TestSpecificCandidate(
            ulong aivStateAddress,
            int aivSpecIndex,
            int candidateId)
        {
            OracleSelectionSession session = TryBeginSession(
                "TestSpecificCandidate",
                aivStateAddress,
                aivSpecIndex,
                false,
                candidateId);
            uint result = 0;
            bool returned = false;
            try
            {
                result = testSpecificCandidateHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    aivSpecIndex,
                    candidateId);
                returned = true;
                return result;
            }
            finally
            {
                CompleteSession(session, returned ? unchecked((int)result) : (int?)null);
            }
        }

        private void LoadCandidate(
            ulong aivStateAddress,
            int zeroBasedPlayerId,
            int candidateId)
        {
            loadCandidateHook.Value.Hook.Trampoline(
                aivStateAddress,
                zeroBasedPlayerId,
                candidateId);

            OracleSelectionSession session = activeSession;
            if (session != null && session.AivStateAddress == aivStateAddress)
                session.CurrentCandidateId = candidateId;
        }

        private void ApplyRotation(ulong aivStateAddress, int orientation)
        {
            applyRotationHook.Value.Hook.Trampoline(aivStateAddress, orientation);

            OracleSelectionSession session = activeSession;
            if (session != null && session.AivStateAddress == aivStateAddress)
                session.CurrentOrientation = orientation;
        }

        private int EvaluateCandidateFit(ulong aivStateAddress, int aivSpecIndex)
        {
            OracleSelectionSession session = activeSession;
            ValidatorTraceContext validatorTrace = TryBeginValidatorTrace(
                session,
                aivStateAddress,
                aivSpecIndex);
            if (validatorTrace != null)
                activeValidatorTrace = validatorTrace;

            PlacementStateObservation prebuildObservation =
                TryGetPrebuildPlacementStateObservation(session);
            PlacementStateObservation previousPrebuildObservation =
                activePrebuildPlacementStateObservation;
            if (prebuildObservation != null)
                activePrebuildPlacementStateObservation = prebuildObservation;

            int rawFitScore;
            try
            {
                rawFitScore = evaluateCandidateFitHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    aivSpecIndex);
            }
            finally
            {
                if (ReferenceEquals(activeValidatorTrace, validatorTrace))
                    activeValidatorTrace = null;
                if (ReferenceEquals(activePrebuildPlacementStateObservation, prebuildObservation))
                    activePrebuildPlacementStateObservation = previousPrebuildObservation;
            }

            try
            {
                if (session == null ||
                    session.AivStateAddress != aivStateAddress ||
                    session.AivSpecIndex != aivSpecIndex)
                {
                    return rawFitScore;
                }

                int evaluatedCells = ReadInt32(aivStateAddress, EvaluatedCellCountOffset);
                int blockedCells = ReadInt32(aivStateAddress, BlockedCellCountOffset);
                int fitPercent = evaluatedCells == 0
                    ? 100
                    : ((evaluatedCells - blockedCells) * 100) / evaluatedCells;
                byte* spec = GetSpec(aivStateAddress, aivSpecIndex);
                OracleCellTraceSnapshot cellTrace = TryCaptureCellTrace(
                    session,
                    spec,
                    evaluatedCells,
                    blockedCells,
                    validatorTrace);

                session.Attempts.Add(new OracleAttemptSnapshot(
                    session.Attempts.Count + 1,
                    session.CurrentCandidateId,
                    session.CurrentOrientation,
                    rawFitScore,
                    fitPercent,
                    evaluatedCells,
                    blockedCells,
                    *(int*)(spec + OriginXOffset),
                    *(int*)(spec + OriginYOffset),
                    *(int*)(spec + KeepXOffset),
                    *(int*)(spec + KeepYOffset),
                    cellTrace));
            }
            catch (Exception ex)
            {
                LogCallbackFailure("fit result capture", ex);
            }

            // The oracle observes Vanilla's return value without changing it.
            return rawFitScore;
        }

        private int BuildingPlacementValidator(
            ulong placementStateAddress,
            int tileId,
            int playerId,
            int mapperValue,
            int mode)
        {
            ValidatorTraceContext trace = activeValidatorTrace;
            PlacementStateObservation prebuildObservation =
                activePrebuildPlacementStateObservation;
            if (prebuildObservation != null && placementStateAddress != 0)
                prebuildObservation.ObservePlacementStateAddress(placementStateAddress);

            int nativeTerrainFlags = 0;
            int nativeHeight = 0;
            int nativeDefaultHeight = 0;
            int nativeOrganismId = 0;
            int nativeOrganismClass = -1;
            int nativeBuildingId = 0;
            int nativeEntityId = 0;
            int nativeOwnerId = 0;
            int nativeGameMode = 0;
            if (trace != null && placementStateAddress != 0)
            {
                // The validator owns the TileManager-style placement-state pointer. Keep
                // that address separate from the unrelated AIV state used by the fit grids.
                trace.ObservePlacementStateAddress(placementStateAddress);

                // Read the exact live validator inputs before Vanilla evaluates the tile.
                byte* placementState = (byte*)placementStateAddress;
                nativeTerrainFlags = *(int*)(placementState + TerrainFlagsOffset + tileId * 4);
                nativeHeight = *(byte*)(placementState + HeightOffset + tileId);
                nativeDefaultHeight = *(byte*)(placementState + DefaultHeightOffset + tileId);
                nativeOrganismId = *(short*)(placementState + OrganismGridOffset + tileId * 2);
                nativeBuildingId = *(ushort*)(placementState + BuildingGridOffset + tileId * 2);
                nativeEntityId = *(ushort*)(placementState + EntityGridOffset + tileId * 2);
                nativeOwnerId = *(byte*)(placementState + OwnerGridOffset + tileId);
                nativeGameMode = (int)GamePlayerManagerAPI.Instance.GetCurrentSkirmishGameMode();
                if (nativeOrganismId > 0 && nativeOrganismId < 4000)
                {
                    nativeOrganismClass = *(short*)(
                        organismRecordTableAddress +
                        (ulong)(nativeOrganismId * OrganismRecordStride + OrganismClassOffset));
                }
            }

            int result = buildingPlacementValidatorHook.Value.Hook.Trampoline(
                placementStateAddress,
                tileId,
                playerId,
                mapperValue,
                mode);

            try
            {
                if (trace != null)
                {
                    trace.Calls.Add(new OracleValidatorCallEntry(
                        trace.Calls.Count,
                        tileId,
                        playerId,
                        mapperValue,
                        mode,
                        result,
                        nativeTerrainFlags,
                        nativeHeight,
                        nativeDefaultHeight,
                        nativeOrganismId,
                        nativeOrganismClass,
                        nativeBuildingId,
                        nativeEntityId,
                        nativeOwnerId,
                        nativeGameMode));
                }
            }
            catch (Exception ex)
            {
                LogCallbackFailure("validator result capture", ex);
            }

            // The trace always returns the single unchanged Vanilla result.
            return result;
        }

        private int ExecuteBuildStep(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            int restrictedMode,
            byte freeOrForced)
        {
            if (executeBuildStepDepth != 0)
            {
                if (!executeBuildStepReentrancyLogged)
                {
                    executeBuildStepReentrancyLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Oracle prebuild trace rejected a nested ExecuteBuildStep capture; " +
                        "the nested Vanilla call still runs unchanged.");
                }
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    playerId,
                    frameIndex,
                    restrictedMode,
                    freeOrForced);
            }

            if (!TryBeginOrContinuePrebuildCapture(aivStateAddress, playerId))
            {
                return executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    playerId,
                    frameIndex,
                    restrictedMode,
                    freeOrForced);
            }

            DateTimeOffset startedAtLocal = DateTimeOffset.Now;
            int activeLayoutIndex = -1;
            byte status = 0;
            byte helper = 0;
            short mapper = 0;
            short positionCount = 0;
            int firstPositionIndex = -1;
            string captureError = string.Empty;
            try
            {
                ReadPreparedFrame(
                    aivStateAddress,
                    playerId,
                    frameIndex,
                    out activeLayoutIndex,
                    out status,
                    out helper,
                    out mapper,
                    out positionCount,
                    out firstPositionIndex);
            }
            catch (Exception ex)
            {
                captureError = "Prepared frame read failed: " + ex.Message;
            }

            prebuildPlacementStateByPlayerId.TryGetValue(
                playerId,
                out PlacementStateObservation placementObservation);
            ulong placementStateAddress = 0;
            bool pointerWasConsistent = placementObservation != null &&
                placementObservation.TryGetPlacementStateAddress(out placementStateAddress);
            if (pointerWasConsistent)
            {
                CopyBuildingGrid(placementStateAddress, prebuildBeforeBuildingGrid);
            }
            else
            {
                LogPrebuildPointerProblemOnce(
                    placementObservation == null || !placementObservation.HasObservedAddress
                        ? "no placement-state pointer was observed during the filtered fit calls"
                        : "the filtered validator exposed inconsistent placement-state pointers");
            }

            PlacementStateObservation previousObservation =
                activePrebuildPlacementStateObservation;
            if (placementObservation != null)
                activePrebuildPlacementStateObservation = placementObservation;

            int result;
            executeBuildStepDepth++;
            try
            {
                result = executeBuildStepHook.Value.Hook.Trampoline(
                    aivStateAddress,
                    playerId,
                    frameIndex,
                    restrictedMode,
                    freeOrForced);
            }
            finally
            {
                executeBuildStepDepth--;
                if (ReferenceEquals(activePrebuildPlacementStateObservation, placementObservation))
                    activePrebuildPlacementStateObservation = previousObservation;
            }

            DateTimeOffset completedAtLocal = DateTimeOffset.Now;
            var changes = new List<OraclePrebuildBuildingGridChange>();
            int addedCount = 0;
            int removedCount = 0;
            int replacedCount = 0;
            ulong finalPlacementStateAddress = 0;
            bool pointerIsConsistent = placementObservation != null &&
                placementObservation.TryGetPlacementStateAddress(out finalPlacementStateAddress) &&
                finalPlacementStateAddress == placementStateAddress;
            if (pointerWasConsistent && pointerIsConsistent)
            {
                CopyBuildingGrid(finalPlacementStateAddress, prebuildAfterBuildingGrid);
                for (int tileId = 0; tileId < FixedMapTileCount; tileId++)
                {
                    ushort beforeId = prebuildBeforeBuildingGrid[tileId];
                    ushort afterId = prebuildAfterBuildingGrid[tileId];
                    if (beforeId == afterId)
                        continue;

                    if (beforeId == 0)
                        addedCount++;
                    else if (afterId == 0)
                        removedCount++;
                    else
                        replacedCount++;
                    changes.Add(new OraclePrebuildBuildingGridChange(
                        tileId,
                        beforeId,
                        afterId));
                }
            }
            else if (pointerWasConsistent)
            {
                LogPrebuildPointerProblemOnce(
                    "the placement-state pointer changed or became inconsistent inside ExecuteBuildStep");
            }

            try
            {
                lastSelectionSequenceByPlayerId.TryGetValue(
                    playerId,
                    out long selectionSequence);
                onPrebuildFrameCaptured(new OraclePrebuildFrameTraceSnapshot(
                    activePrebuildCaptureSequence,
                    ++activePrebuildCaptureFrameNumber,
                    startedAtLocal,
                    completedAtLocal,
                    selectionSequence,
                    playerId,
                    frameIndex,
                    activeLayoutIndex,
                    status,
                    helper,
                    mapper,
                    positionCount,
                    firstPositionIndex,
                    restrictedMode,
                    freeOrForced,
                    result,
                    placementStateAddress,
                    pointerWasConsistent && pointerIsConsistent,
                    addedCount,
                    removedCount,
                    replacedCount,
                    changes,
                    captureError));
            }
            catch (Exception ex)
            {
                LogCallbackFailure("prebuild frame capture", ex);
            }

            // The diagnostic returns the exact native result after one trampoline call.
            return result;
        }

        private bool TryBeginOrContinuePrebuildCapture(
            ulong aivStateAddress,
            int playerId)
        {
            if (!prebuildTraceOptions.Enabled)
                return false;
            if (playerId != prebuildTraceOptions.PlayerId)
            {
                if (activePrebuildCapturePlayerId >= 0 &&
                    playerId != activePrebuildCapturePlayerId)
                {
                    activePrebuildCapturePlayerId = -1;
                    activePrebuildCaptureAivStateAddress = 0;
                }
                return false;
            }

            if (activePrebuildCapturePlayerId == playerId)
                return activePrebuildCaptureAivStateAddress == aivStateAddress;
            if (prebuildTraceCaptureCount >= prebuildTraceOptions.MaximumCaptureCount)
                return false;

            prebuildTraceCaptureCount++;
            activePrebuildCaptureSequence = prebuildTraceCaptureCount;
            activePrebuildCaptureFrameNumber = 0;
            activePrebuildCapturePlayerId = playerId;
            activePrebuildCaptureAivStateAddress = aivStateAddress;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Started opt-in Oracle prebuild trace {activePrebuildCaptureSequence}/" +
                $"{prebuildTraceOptions.MaximumCaptureCount}: playerId={playerId}, " +
                $"aivStateAddress=0x{aivStateAddress:X}.");
            return true;
        }

        private void ReadPreparedFrame(
            ulong aivStateAddress,
            int playerId,
            int frameIndex,
            out int activeLayoutIndex,
            out byte status,
            out byte helper,
            out short mapper,
            out short positionCount,
            out int firstPositionIndex)
        {
            if (aivStateAddress == 0)
                throw new InvalidOperationException("The native AIV state pointer is null.");
            if (playerId < 1 || playerId > 8)
                throw new ArgumentOutOfRangeException(nameof(playerId));
            if (frameIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            activeLayoutIndex = *(int*)(
                activeLayoutIndexBaseAddress +
                (ulong)(playerId * PlayerRuntimeStateStride));
            long entryIndex = checked(
                (long)activeLayoutIndex * PreparedLayoutFrameCount + frameIndex);
            if (activeLayoutIndex < 0 || activeLayoutIndex >= 8 || entryIndex < 0)
                throw new InvalidOperationException($"Invalid active layout index {activeLayoutIndex}.");

            byte* entry = (byte*)aivStateAddress + PreparedEntryBaseOffset +
                checked(entryIndex * PreparedEntrySize);
            status = entry[0];
            helper = entry[1];
            mapper = *(short*)(entry + 2);
            positionCount = *(short*)(entry + 4);
            firstPositionIndex = *(int*)(entry + 8);
        }

        private static void CopyBuildingGrid(ulong placementStateAddress, ushort[] destination)
        {
            ushort* source = (ushort*)((byte*)placementStateAddress + BuildingGridOffset);
            fixed (ushort* target = destination)
            {
                long byteCount = FixedMapTileCount * sizeof(ushort);
                Buffer.MemoryCopy(source, target, byteCount, byteCount);
            }
        }

        private void LogPrebuildPointerProblemOnce(string reason)
        {
            if (prebuildPointerWarningLogged)
                return;

            prebuildPointerWarningLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Oracle prebuild trace cannot produce a reliable BuildingId-grid diff because {reason}.");
        }

        private ValidatorTraceContext TryBeginValidatorTrace(
            OracleSelectionSession session,
            ulong aivStateAddress,
            int aivSpecIndex)
        {
            if (session == null ||
                session.AivStateAddress != aivStateAddress ||
                session.AivSpecIndex != aivSpecIndex)
            {
                return null;
            }

            byte* spec = GetSpec(aivStateAddress, aivSpecIndex);
            return MatchesCellTraceFilter(session, spec)
                ? new ValidatorTraceContext()
                : null;
        }

        private PlacementStateObservation TryGetPrebuildPlacementStateObservation(
            OracleSelectionSession session)
        {
            if (session == null ||
                !prebuildTraceOptions.Enabled ||
                session.PlayerId != prebuildTraceOptions.PlayerId)
            {
                return null;
            }

            if (!prebuildPlacementStateByPlayerId.TryGetValue(
                    session.PlayerId,
                    out PlacementStateObservation observation))
            {
                observation = new PlacementStateObservation();
                prebuildPlacementStateByPlayerId.Add(session.PlayerId, observation);
            }
            return observation;
        }

        private OracleCellTraceSnapshot TryCaptureCellTrace(
            OracleSelectionSession session,
            byte* spec,
            int evaluatedCells,
            int blockedCells,
            ValidatorTraceContext validatorTrace)
        {
            if (!MatchesCellTraceFilter(session, spec))
                return null;

            // EvaluateCandidateFit leaves the rotated mapper, score and result grids intact.
            // Copy them before Vanilla can load or rotate the next candidate.
            short* mapperGrid = (short*)((byte*)session.AivStateAddress + MapperGridOffset);
            int* scoreGrid = (int*)((byte*)session.AivStateAddress + ScoreGridOffset);
            byte* resultGrid = (byte*)session.AivStateAddress + CellResultGridOffset;
            int originX = *(int*)(spec + OriginXOffset);
            int originY = *(int*)(spec + OriginYOffset);
            var cells = new List<OracleCellTraceEntry>(evaluatedCells);
            int resultGridBlockedCells = 0;

            for (int row = 0; row < AivGridSize; row++)
            {
                for (int column = 0; column < AivGridSize; column++)
                {
                    int index = row * AivGridSize + column;
                    short rawMapper = mapperGrid[index];
                    if (rawMapper == 0 || rawMapper == 1)
                        continue;

                    byte result = resultGrid[index];
                    if (result != 0)
                        resultGridBlockedCells++;

                    cells.Add(new OracleCellTraceEntry(
                        row,
                        column,
                        originX + column,
                        originY + row,
                        rawMapper,
                        rawMapper < 0 ? 86 : rawMapper,
                        scoreGrid[index],
                        result));
                }
            }

            IReadOnlyList<OracleLiveBuildingTileEntry> liveBuildingTiles =
                Array.Empty<OracleLiveBuildingTileEntry>();
            if (validatorTrace != null &&
                validatorTrace.TryGetPlacementStateAddress(out ulong placementStateAddress))
            {
                // Every wildcard-player capture needs its own pre-player snapshot;
                // one process-wide grid would hide later PreBuild state transitions.
                byte* placementState = (byte*)placementStateAddress;
                var occupied = new List<OracleLiveBuildingTileEntry>();
                for (int tileId = 0; tileId < FixedMapTileCount; tileId++)
                {
                    int buildingId = *(ushort*)(
                        placementState + BuildingGridOffset + tileId * 2);
                    if (buildingId == 0)
                        continue;

                    occupied.Add(new OracleLiveBuildingTileEntry(
                        tileId,
                        buildingId,
                        *(byte*)(placementState + OwnerGridOffset + tileId),
                        *(int*)(placementState + TerrainFlagsOffset + tileId * 4)));
                }
                liveBuildingTiles = occupied.AsReadOnly();
            }
            else
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    "Skipped the live building-grid snapshot because the filtered validator " +
                    "did not expose one consistent placement-state pointer.");
            }

            cellTraceCaptureCount++;
            cellTraceCapturedPlayerIds.Add(session.PlayerId);
            OracleCellTraceSnapshot trace = new OracleCellTraceSnapshot(
                DateTimeOffset.Now,
                evaluatedCells,
                blockedCells,
                resultGridBlockedCells,
                cells,
                validatorTrace == null
                    ? Array.Empty<OracleValidatorCallEntry>()
                    : validatorTrace.Calls,
                liveBuildingTiles);
            int validatorBlockedCells = 0;
            foreach (OracleValidatorCallEntry call in trace.ValidatorCalls)
            {
                if (call.Result != 0)
                    validatorBlockedCells++;
            }
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured opt-in AIV cell trace {cellTraceCaptureCount}/" +
                $"{cellTraceOptions.MaximumCaptureCount}: playerId={session.PlayerId}, " +
                $"candidateId={session.CurrentCandidateId}, orientation={session.CurrentOrientation}, " +
                $"keep=({*(int*)(spec + KeepXOffset)},{*(int*)(spec + KeepYOffset)}), " +
                $"evaluated={evaluatedCells}, nativeBlocked={blockedCells}, " +
                $"resultGridBlocked={resultGridBlockedCells}, " +
                $"validatorCalls={trace.ValidatorCalls.Count}, " +
                $"validatorBlocked={validatorBlockedCells}.");
            return trace;
        }

        private bool MatchesCellTraceFilter(
            OracleSelectionSession session,
            byte* spec)
        {
            return cellTraceOptions.Enabled &&
                cellTraceCaptureCount < cellTraceOptions.MaximumCaptureCount &&
                // A negative diagnostic player ID follows a randomly assigned Keep.
                (cellTraceOptions.PlayerId < 0 ||
                    session.PlayerId == cellTraceOptions.PlayerId) &&
                // Wildcard runs capture one state transition per player, not rotations.
                (cellTraceOptions.PlayerId >= 0 ||
                    !cellTraceCapturedPlayerIds.Contains(session.PlayerId)) &&
                session.CurrentCandidateId == cellTraceOptions.CandidateId &&
                (cellTraceOptions.Orientation < 0 ||
                    session.CurrentOrientation == cellTraceOptions.Orientation) &&
                // Negative coordinates let one player trace survive randomized starts.
                (cellTraceOptions.KeepX < 0 ||
                    *(int*)(spec + KeepXOffset) == cellTraceOptions.KeepX) &&
                (cellTraceOptions.KeepY < 0 ||
                    *(int*)(spec + KeepYOffset) == cellTraceOptions.KeepY);
        }

        private OracleSelectionSession TryBeginSession(
            string method,
            ulong aivStateAddress,
            int aivSpecIndex,
            bool tryOtherRotations,
            int? requestedCandidateId)
        {
            try
            {
                if (activeSession != null)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"AIV placement oracle ignored a nested {method} call while " +
                        $"{activeSession.Method} was active.");
                    return null;
                }
                if (aivStateAddress == 0)
                    throw new InvalidOperationException("The native AIV state pointer is null.");
                if (aivSpecIndex < 0 || aivSpecIndex > 8)
                    throw new ArgumentOutOfRangeException(nameof(aivSpecIndex));

                byte* spec = GetSpec(aivStateAddress, aivSpecIndex);
                OracleSelectionSession session = new OracleSelectionSession(
                    ++nextSequence,
                    method,
                    aivStateAddress,
                    aivSpecIndex,
                    *(int*)(spec + PlayerIdOffset),
                    tryOtherRotations,
                    requestedCandidateId ?? *(int*)(spec + CandidateIdOffset),
                    *(int*)(spec + OrientationOffset));
                activeSession = session;
                return session;
            }
            catch (Exception ex)
            {
                LogCallbackFailure($"{method} start", ex);
                return null;
            }
        }

        private void CompleteSession(OracleSelectionSession session, int? directReturnSigned)
        {
            if (session == null)
                return;

            if (ReferenceEquals(activeSession, session))
                activeSession = null;

            try
            {
                byte* spec = GetSpec(session.AivStateAddress, session.AivSpecIndex);
                lastSelectionSequenceByPlayerId[session.PlayerId] = session.Sequence;
                onSelectionCompleted(new OracleSelectionSnapshot(
                    session.Sequence,
                    session.Method,
                    session.PlayerId,
                    session.AivSpecIndex,
                    session.TryOtherRotations,
                    directReturnSigned,
                    *(int*)(spec + CandidateIdOffset),
                    *(int*)(spec + OrientationOffset),
                    *(int*)(spec + PlacementStateOffset),
                    session.Attempts));
            }
            catch (Exception ex)
            {
                LogCallbackFailure($"{session.Method} completion", ex);
            }
        }

        private void LogCallbackFailure(string operation, Exception ex)
        {
            if (callbackFailureLogged)
                return;

            callbackFailureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"AIV placement oracle {operation} failed; further capture errors are suppressed " +
                $"and Vanilla behavior remains unchanged: {ex}");
        }

        private static byte* GetSpec(ulong aivStateAddress, int aivSpecIndex)
        {
            return (byte*)aivStateAddress + checked(aivSpecIndex * AivSpecStride);
        }

        private static int ReadInt32(ulong address, int offset)
        {
            return *(int*)((byte*)address + offset);
        }

        private ulong ResolveUniqueRipRelativeAddress(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            string name,
            string pattern,
            int referenceRva,
            int instructionOffset,
            int requiredBytes)
        {
            int match = ValidateReference(memory, pattern, referenceRva, name);

            IntPtr instruction = IntPtr.Add(libraryHandle, match + instructionOffset);
            int displacement = Marshal.ReadInt32(instruction, 3);
            long target = checked(instruction.ToInt64() + 7L + displacement);
            long imageStart = libraryHandle.ToInt64();
            long imageEnd = checked(imageStart + memory.Length);
            if (target < imageStart || target > imageEnd - requiredBytes)
            {
                throw new InvalidOperationException(
                    $"Native {name} signature resolved outside the loaded image: " +
                    $"target=0x{target:X}, image=0x{imageStart:X}-0x{imageEnd:X}.");
            }

            return unchecked((ulong)target);
        }

        private int ValidateReference(
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            string name)
        {
            return Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                referenceRva,
                referenceHashMatches: true,
                name,
                log).Rva;
        }

        private static void AddMissing(List<string> missing, bool success, string name)
        {
            if (!success)
                missing.Add(name);
        }

        private sealed class OracleSelectionSession
        {
            public OracleSelectionSession(
                long sequence,
                string method,
                ulong aivStateAddress,
                int aivSpecIndex,
                int playerId,
                bool tryOtherRotations,
                int currentCandidateId,
                int currentOrientation)
            {
                Sequence = sequence;
                Method = method;
                AivStateAddress = aivStateAddress;
                AivSpecIndex = aivSpecIndex;
                PlayerId = playerId;
                TryOtherRotations = tryOtherRotations;
                CurrentCandidateId = currentCandidateId;
                CurrentOrientation = currentOrientation;
                Attempts = new List<OracleAttemptSnapshot>();
            }

            public long Sequence { get; }
            public string Method { get; }
            public ulong AivStateAddress { get; }
            public int AivSpecIndex { get; }
            public int PlayerId { get; }
            public bool TryOtherRotations { get; }
            public int CurrentCandidateId { get; set; }
            public int CurrentOrientation { get; set; }
            public List<OracleAttemptSnapshot> Attempts { get; }
        }

        private sealed class ValidatorTraceContext
        {
            private ulong placementStateAddress;
            private bool placementStateAddressMismatch;

            public ValidatorTraceContext()
            {
                Calls = new List<OracleValidatorCallEntry>();
            }

            public List<OracleValidatorCallEntry> Calls { get; }

            public void ObservePlacementStateAddress(ulong address)
            {
                if (placementStateAddress == 0)
                {
                    placementStateAddress = address;
                    return;
                }

                if (placementStateAddress != address)
                    placementStateAddressMismatch = true;
            }

            public bool TryGetPlacementStateAddress(out ulong address)
            {
                address = placementStateAddress;
                return address != 0 && !placementStateAddressMismatch;
            }
        }

        private sealed class PlacementStateObservation
        {
            private ulong placementStateAddress;
            private bool placementStateAddressMismatch;

            public bool HasObservedAddress => placementStateAddress != 0;

            public void ObservePlacementStateAddress(ulong address)
            {
                if (placementStateAddress == 0)
                {
                    placementStateAddress = address;
                    return;
                }

                if (placementStateAddress != address)
                    placementStateAddressMismatch = true;
            }

            public bool TryGetPlacementStateAddress(out ulong address)
            {
                address = placementStateAddress;
                return address != 0 && !placementStateAddressMismatch;
            }
        }
    }

    internal sealed class OracleSelectionSnapshot
    {
        public OracleSelectionSnapshot(
            long sequence,
            string method,
            int playerId,
            int aivSpecIndex,
            bool tryOtherRotations,
            int? directReturnSigned,
            int finalCandidateId,
            int finalOrientation,
            int placementState,
            IList<OracleAttemptSnapshot> attempts)
        {
            Sequence = sequence;
            Method = method;
            PlayerId = playerId;
            AivSpecIndex = aivSpecIndex;
            TryOtherRotations = tryOtherRotations;
            DirectReturnSigned = directReturnSigned;
            FinalCandidateId = finalCandidateId;
            FinalOrientation = finalOrientation;
            PlacementState = placementState;
            Attempts = new List<OracleAttemptSnapshot>(attempts).AsReadOnly();
        }

        public long Sequence { get; }
        public string Method { get; }
        public int PlayerId { get; }
        public int AivSpecIndex { get; }
        public bool TryOtherRotations { get; }
        public int? DirectReturnSigned { get; }
        public int FinalCandidateId { get; }
        public int FinalOrientation { get; }
        public int PlacementState { get; }
        public IReadOnlyList<OracleAttemptSnapshot> Attempts { get; }
    }

    internal readonly struct OracleAttemptSnapshot
    {
        public OracleAttemptSnapshot(
            int attemptNumber,
            int candidateId,
            int orientation,
            int rawFitScore,
            int fitPercent,
            int evaluatedCells,
            int blockedCells,
            int originX,
            int originY,
            int keepX,
            int keepY,
            OracleCellTraceSnapshot cellTrace)
        {
            AttemptNumber = attemptNumber;
            CandidateId = candidateId;
            Orientation = orientation;
            RawFitScore = rawFitScore;
            FitPercent = fitPercent;
            EvaluatedCells = evaluatedCells;
            BlockedCells = blockedCells;
            OriginX = originX;
            OriginY = originY;
            KeepX = keepX;
            KeepY = keepY;
            CellTrace = cellTrace;
        }

        public int AttemptNumber { get; }
        public int CandidateId { get; }
        public int Orientation { get; }
        public int RawFitScore { get; }
        public int FitPercent { get; }
        public int EvaluatedCells { get; }
        public int BlockedCells { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int KeepX { get; }
        public int KeepY { get; }
        public OracleCellTraceSnapshot CellTrace { get; }

        public string ResultKind => RawFitScore == 999999
            ? "Complete"
            : RawFitScore > 0
                ? "Partial"
                : "Rejected";
    }

    internal sealed class OracleCellTraceOptions
    {
        public OracleCellTraceOptions(
            bool enabled,
            int playerId,
            int candidateId,
            int orientation,
            int keepX,
            int keepY,
            int maximumCaptureCount,
            string outputDirectory)
        {
            Enabled = enabled;
            PlayerId = playerId;
            CandidateId = candidateId;
            Orientation = orientation;
            KeepX = keepX;
            KeepY = keepY;
            MaximumCaptureCount = maximumCaptureCount;
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        }

        public bool Enabled { get; }
        public int PlayerId { get; }
        public int CandidateId { get; }
        public int Orientation { get; }
        public int KeepX { get; }
        public int KeepY { get; }
        public int MaximumCaptureCount { get; }
        public string OutputDirectory { get; }
    }

    internal sealed class OraclePrebuildTraceOptions
    {
        public OraclePrebuildTraceOptions(
            bool enabled,
            int playerId,
            int maximumCaptureCount,
            string outputDirectory)
        {
            Enabled = enabled;
            PlayerId = playerId;
            MaximumCaptureCount = maximumCaptureCount;
            OutputDirectory = outputDirectory ?? throw new ArgumentNullException(nameof(outputDirectory));
        }

        public bool Enabled { get; }
        public int PlayerId { get; }
        public int MaximumCaptureCount { get; }
        public string OutputDirectory { get; }
    }

    internal sealed class OraclePrebuildFrameTraceSnapshot
    {
        public OraclePrebuildFrameTraceSnapshot(
            int captureSequence,
            int captureFrameNumber,
            DateTimeOffset startedAtLocal,
            DateTimeOffset completedAtLocal,
            long selectionSequence,
            int playerId,
            int frameIndex,
            int activeLayoutIndex,
            byte status,
            byte helper,
            short mapper,
            short positionCount,
            int firstPositionIndex,
            int restrictedMode,
            byte freeOrForced,
            int returnValue,
            ulong placementStateAddress,
            bool placementStatePointerConsistent,
            int addedCount,
            int removedCount,
            int replacedCount,
            IList<OraclePrebuildBuildingGridChange> changes,
            string captureError)
        {
            CaptureSequence = captureSequence;
            CaptureFrameNumber = captureFrameNumber;
            StartedAtLocal = startedAtLocal;
            CompletedAtLocal = completedAtLocal;
            SelectionSequence = selectionSequence;
            PlayerId = playerId;
            FrameIndex = frameIndex;
            ActiveLayoutIndex = activeLayoutIndex;
            Status = status;
            Helper = helper;
            Mapper = mapper;
            PositionCount = positionCount;
            FirstPositionIndex = firstPositionIndex;
            RestrictedMode = restrictedMode;
            FreeOrForced = freeOrForced;
            ReturnValue = returnValue;
            PlacementStateAddress = placementStateAddress;
            PlacementStatePointerConsistent = placementStatePointerConsistent;
            AddedCount = addedCount;
            RemovedCount = removedCount;
            ReplacedCount = replacedCount;
            Changes = new List<OraclePrebuildBuildingGridChange>(changes).AsReadOnly();
            CaptureError = captureError ?? string.Empty;
        }

        public int CaptureSequence { get; }
        public int CaptureFrameNumber { get; }
        public DateTimeOffset StartedAtLocal { get; }
        public DateTimeOffset CompletedAtLocal { get; }
        public long SelectionSequence { get; }
        public int PlayerId { get; }
        public int FrameIndex { get; }
        public int ActiveLayoutIndex { get; }
        public byte Status { get; }
        public byte Helper { get; }
        public short Mapper { get; }
        public short PositionCount { get; }
        public int FirstPositionIndex { get; }
        public int RestrictedMode { get; }
        public byte FreeOrForced { get; }
        public int ReturnValue { get; }
        public ulong PlacementStateAddress { get; }
        public bool PlacementStatePointerConsistent { get; }
        public int AddedCount { get; }
        public int RemovedCount { get; }
        public int ReplacedCount { get; }
        public IReadOnlyList<OraclePrebuildBuildingGridChange> Changes { get; }
        public string CaptureError { get; }
        public bool IsHighlightedMapper => Mapper == 52 || Mapper == 89 || Mapper == 105;
    }

    internal readonly struct OraclePrebuildBuildingGridChange
    {
        public OraclePrebuildBuildingGridChange(
            int tileId,
            ushort beforeId,
            ushort afterId)
        {
            TileId = tileId;
            BeforeId = beforeId;
            AfterId = afterId;
        }

        public int TileId { get; }
        public ushort BeforeId { get; }
        public ushort AfterId { get; }
        public string Kind => BeforeId == 0
            ? "Added"
            : AfterId == 0
                ? "Removed"
                : "Replaced";
    }

    internal sealed class OracleCellTraceSnapshot
    {
        public OracleCellTraceSnapshot(
            DateTimeOffset capturedAtLocal,
            int evaluatedCells,
            int nativeBlockedCells,
            int resultGridBlockedCells,
            IList<OracleCellTraceEntry> cells,
            IList<OracleValidatorCallEntry> validatorCalls,
            IReadOnlyList<OracleLiveBuildingTileEntry> liveBuildingTiles)
        {
            CapturedAtLocal = capturedAtLocal;
            EvaluatedCells = evaluatedCells;
            NativeBlockedCells = nativeBlockedCells;
            ResultGridBlockedCells = resultGridBlockedCells;
            Cells = new List<OracleCellTraceEntry>(cells).AsReadOnly();
            ValidatorCalls = new List<OracleValidatorCallEntry>(validatorCalls).AsReadOnly();
            LiveBuildingTiles = liveBuildingTiles ??
                throw new ArgumentNullException(nameof(liveBuildingTiles));
        }

        public DateTimeOffset CapturedAtLocal { get; }
        public int EvaluatedCells { get; }
        public int NativeBlockedCells { get; }
        public int ResultGridBlockedCells { get; }
        public IReadOnlyList<OracleCellTraceEntry> Cells { get; }
        public IReadOnlyList<OracleValidatorCallEntry> ValidatorCalls { get; }
        public IReadOnlyList<OracleLiveBuildingTileEntry> LiveBuildingTiles { get; }
    }

    internal readonly struct OracleCellTraceEntry
    {
        public OracleCellTraceEntry(
            int gridRow,
            int gridColumn,
            int worldX,
            int worldY,
            int rawMapper,
            int effectiveMapper,
            int scoreGridValue,
            byte resultGridValue)
        {
            GridRow = gridRow;
            GridColumn = gridColumn;
            WorldX = worldX;
            WorldY = worldY;
            RawMapper = rawMapper;
            EffectiveMapper = effectiveMapper;
            ScoreGridValue = scoreGridValue;
            ResultGridValue = resultGridValue;
        }

        public int GridRow { get; }
        public int GridColumn { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public int RawMapper { get; }
        public int EffectiveMapper { get; }
        public int ScoreGridValue { get; }
        public byte ResultGridValue { get; }
        public bool Blocked => ResultGridValue != 0;
    }

    internal readonly struct OracleValidatorCallEntry
    {
        public OracleValidatorCallEntry(
            int callIndex,
            int tileId,
            int playerId,
            int mapperValue,
            int mode,
            int result,
            int nativeTerrainFlags,
            int nativeHeight,
            int nativeDefaultHeight,
            int nativeOrganismId,
            int nativeOrganismClass,
            int nativeBuildingId,
            int nativeEntityId,
            int nativeOwnerId,
            int nativeGameMode)
        {
            CallIndex = callIndex;
            TileId = tileId;
            PlayerId = playerId;
            MapperValue = mapperValue;
            Mode = mode;
            Result = result;
            NativeTerrainFlags = nativeTerrainFlags;
            NativeHeight = nativeHeight;
            NativeDefaultHeight = nativeDefaultHeight;
            NativeOrganismId = nativeOrganismId;
            NativeOrganismClass = nativeOrganismClass;
            NativeBuildingId = nativeBuildingId;
            NativeEntityId = nativeEntityId;
            NativeOwnerId = nativeOwnerId;
            NativeGameMode = nativeGameMode;
        }

        public int CallIndex { get; }
        public int TileId { get; }
        public int PlayerId { get; }
        public int MapperValue { get; }
        public int Mode { get; }
        public int Result { get; }
        public int NativeTerrainFlags { get; }
        public int NativeHeight { get; }
        public int NativeDefaultHeight { get; }
        public int NativeOrganismId { get; }
        public int NativeOrganismClass { get; }
        public int NativeBuildingId { get; }
        public int NativeEntityId { get; }
        public int NativeOwnerId { get; }
        public int NativeGameMode { get; }
    }

    internal readonly struct OracleLiveBuildingTileEntry
    {
        public OracleLiveBuildingTileEntry(
            int tileId,
            int buildingId,
            int ownerId,
            int terrainFlags)
        {
            TileId = tileId;
            BuildingId = buildingId;
            OwnerId = ownerId;
            TerrainFlags = terrainFlags;
        }

        public int TileId { get; }
        public int BuildingId { get; }
        public int OwnerId { get; }
        public int TerrainFlags { get; }
    }
}
