// Feature: Let valid moat-digging orders traverse already completed friendly moats.
using BepInEx.Logging;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace BugfixesAndQoL
{
    internal sealed unsafe class MoatDiggingReachabilityFix : IDisposable
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int FindNearestFriendlyMoatDelegate(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode);

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetMoatIdAtTileDelegate(IntPtr tileManager, int tileId);

        private const int DigMoatModePatternRva = 0x8D3C2;
        private const int CursorReachabilityPatternRva = 0x8F3A8;
        private const int GetMoatIdAtTilePatternRva = 0x69560;
        private const int FindNearestFriendlyMoatPatternRva = 0x69D60;
        private const int Command6PrecheckHookRva = 0x120E6C;
        private const int MoatPostShorteningHookRva = 0x13F7C1;
        private const int MoatMovementResultHookRva = 0x13F7A9;
        private const int MoatPathStartHookRva = 0x196352;
        private const int MoatBfsResultHookRva = 0x1964D6;
        private const int MoatPathBuilderResultHookRva = 0x19667E;
        private const int TileFlagsRva = 0x48F71B0;
        private const int PathRegionGridRva = 0x50EC690;
        private const int UnitPathPlansRva = 0x7338278;
        private const int MoatMovementTargetXRva = 0x6097BE8;
        private const int MoatMovementTargetYRva = 0x6097BEC;
        private const int MoatPathModeRva = 0x60AD6E4;
        private const int CurrentUnitIdRva = 0x9302C4;
        private const int CursorReachabilityHookOffset = 29;
        private const int CursorReachabilityHookLength = 16;
        private const int Command6PrecheckHookLength = 21;
        private const int MoatPostShorteningHookLength = 14;
        private const int MoatMovementResultHookLength = 14;
        private const int MoatPathStartHookLength = 18;
        private const int MoatBfsResultHookLength = 18;
        private const int MoatPathBuilderResultHookLength = 18;
        private const int GameUnitStride = 0x490;
        private const int TileCount = 320800;
        private const int UnitPathPlanStride = 0x3E8;
        private const int MaximumPathSteps = UnitPathPlanStride * 2;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatOwnerOffset = 0x0C;
        private const int MoatReservationOffset = 0x0F;
        private const int MoatReservationIncrement = 20;
        private const int MaximumFunctionalLogEntries = 128;
        private const int MaximumPendingAttempts = 64;
        private const int MaximumAttemptObservationTicks = 64;
        private const ushort DigMoatMovementState = 124;
        private const ushort FillMoatMovementState = 125;

        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };

        private const string DigMoatModePattern =
            "44 39 25 ?? ?? ?? ?? 74 3C 48 8B CE E8 ?? ?? ?? ?? " +
            "85 C0 74 30 B8 01 00 00 00 44 8B E8 89 44 24 54";

        private const string CursorReachabilityPattern =
            "44 8B 0D ?? ?? ?? ?? 48 8D 0D ?? ?? ?? ?? " +
            "44 8B 05 ?? ?? ?? ?? 41 8B D6 E8 ?? ?? ?? ?? " +
            "85 C0 74 11 44 8B BC 24 C0 00 00 00";

        private const string GetMoatIdAtTilePattern =
            "48 63 C2 0F B7 84 41 ?? ?? ?? ?? C3 CC CC CC";

        private const string FindNearestFriendlyMoatPattern =
            "44 89 44 24 18 89 54 24 10 55 56 57 41 54 41 55 41 56 " +
            "48 83 EC 68 48 8B E9 48 8D 3D ?? ?? ?? ?? 45 8B F1 " +
            "48 8D 87 1C 07 00 00 4D 63 C8 45 33 E4";

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HookTransaction transaction;
        private readonly int* digMoatMode;
        private readonly int* targetTileX;
        private readonly int* targetTileY;
        private readonly int* currentUnitId;
        private readonly int* moatPathMode;
        private readonly int* moatMovementTargetX;
        private readonly int* moatMovementTargetY;
        private readonly uint* tileFlags;
        private readonly short* pathRegions;
        private readonly byte* unitPathPlans;
        private HookRef<X64InlineHook> cursorReachabilityHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> command6PrecheckHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatPostShorteningHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatMovementResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatPathStartHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatBfsResultHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> moatPathBuilderResultHook = new HookRef<X64InlineHook>();
        private GetMoatIdAtTileDelegate getMoatIdAtTile;
        private FindNearestFriendlyMoatDelegate originalFindNearestFriendlyMoat;
        private FindNearestFriendlyMoatDelegate rootedFindNearestFriendlyMoat;
        private NativeDetour findNearestFriendlyMoatDetour;
        private readonly object functionalLogLock = new object();
        private readonly object attemptLock = new object();
        private readonly Dictionary<int, MoatAttempt> pendingAttempts = new Dictionary<int, MoatAttempt>();
        private long nextAttemptId;
        private int functionalLogEntryCount;
        private bool functionalLogLimitLogged;
        private bool commandPrecheckFailureLogged;
        private bool directedTargetFailureLogged;
        private bool tickObservationFailureLogged;
        private bool cursorFailureLogged;
        private bool disposed;

        public MoatDiggingReachabilityFix(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // The moat record array is not exposed by the Script Extender. Its fixed
            // offsets below are validated for the canonical DLL and must fail closed
            // instead of being guessed for a later game version.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    "The moat-digging reachability fix requires the validated CrusaderDE.dll layout.");
            }

            Shared.NativeResolution modeResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                DigMoatModePattern,
                DigMoatModePatternRva,
                referenceHashMatches,
                "DigMoat cursor mode",
                log: null);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                CursorReachabilityPattern,
                CursorReachabilityPatternRva,
                referenceHashMatches,
                "DigMoat cursor reachability check",
                log: null);
            Shared.NativeResolution moatLookupResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                GetMoatIdAtTilePattern,
                GetMoatIdAtTilePatternRva,
                referenceHashMatches,
                "moat ID lookup by tile",
                log: null);
            Shared.NativeResolution moatSearchResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                FindNearestFriendlyMoatPattern,
                FindNearestFriendlyMoatPatternRva,
                referenceHashMatches,
                "nearest friendly moat search",
                log: null);

            int modeRva = ResolveGlobalRva(
                memory,
                modeResolution.Rva + 3,
                modeResolution.Rva + 7,
                "DigMoat cursor mode");
            int targetYRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 3,
                cursorResolution.Rva + 7,
                "cursor target Y");
            int targetXRva = ResolveGlobalRva(
                memory,
                cursorResolution.Rva + 17,
                cursorResolution.Rva + 21,
                "cursor target X");
            int hookRva = checked(cursorResolution.Rva + CursorReachabilityHookOffset);
            ValidateCursorHookSpan(memory, hookRva);
            ValidateCommand6PrecheckHookSpan(memory);
            ValidateDiagnosticHookSpans(memory);

            digMoatMode = (int*)(libraryBase + unchecked((ulong)modeRva));
            targetTileX = (int*)(libraryBase + unchecked((ulong)targetXRva));
            targetTileY = (int*)(libraryBase + unchecked((ulong)targetYRva));
            currentUnitId = (int*)(libraryBase + CurrentUnitIdRva);
            moatPathMode = (int*)(libraryBase + MoatPathModeRva);
            moatMovementTargetX = (int*)(libraryBase + MoatMovementTargetXRva);
            moatMovementTargetY = (int*)(libraryBase + MoatMovementTargetYRva);
            tileFlags = (uint*)(libraryBase + TileFlagsRva);
            pathRegions = (short*)(libraryBase + PathRegionGridRva);
            unitPathPlans = (byte*)(libraryBase + UnitPathPlansRva);
            getMoatIdAtTile = Marshal.GetDelegateForFunctionPointer<GetMoatIdAtTileDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)moatLookupResolution.Rva)));

            try
            {
                transaction = new HookTransaction(
                    memory,
                    libraryBase,
                    loggerFactory: null,
                    failureMode: TransactionFailureMode.RollbackAndThrow);
                transaction.AddContextHook(
                    ref cursorReachabilityHook,
                    libraryBase + unchecked((ulong)hookRva),
                    AllowFriendlyPlannedMoatCursor,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: CursorReachabilityHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref command6PrecheckHook,
                    libraryBase + Command6PrecheckHookRva,
                    PreserveFriendlyPlannedMoatCommandTarget,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: Command6PrecheckHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatPostShorteningHook,
                    libraryBase + MoatPostShorteningHookRva,
                    RecordMoatPostShorteningState,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatPostShorteningHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatMovementResultHook,
                    libraryBase + MoatMovementResultHookRva,
                    RecordMoatMovementResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatMovementResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatPathStartHook,
                    libraryBase + MoatPathStartHookRva,
                    RecordMoatPathStart,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatPathStartHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatBfsResultHook,
                    libraryBase + MoatBfsResultHookRva,
                    RecordMoatBfsResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatBfsResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.AddContextHook(
                    ref moatPathBuilderResultHook,
                    libraryBase + MoatPathBuilderResultHookRva,
                    RecordMoatPathBuilderResult,
                    regs: X64SmartCPUContextRegs.All,
                    hookSize: MoatPathBuilderResultHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                transaction.Commit();

                if (!cursorReachabilityHook.Success || !command6PrecheckHook.Success ||
                    !moatPostShorteningHook.Success || !moatMovementResultHook.Success ||
                    !moatPathStartHook.Success || !moatBfsResultHook.Success ||
                    !moatPathBuilderResultHook.Success)
                {
                    throw new InvalidOperationException(
                        "The DigMoat functional and diagnostic hooks were not installed atomically.");
                }

                rootedFindNearestFriendlyMoat = DirectCommandedMoatTarget;
                IntPtr moatSearchAddress = (IntPtr)(libraryBase + unchecked((ulong)moatSearchResolution.Rva));
                findNearestFriendlyMoatDetour = new NativeDetour(
                    moatSearchAddress,
                    Marshal.GetFunctionPointerForDelegate(rootedFindNearestFriendlyMoat),
                    new NativeDetourConfig { ManualApply = true });
                originalFindNearestFriendlyMoat =
                    findNearestFriendlyMoatDetour.GenerateTrampoline<FindNearestFriendlyMoatDelegate>();
                findNearestFriendlyMoatDetour.Apply();
                GameTimeManagerAPI.Instance.OnTick += ObservePendingAttempts;

                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Bugfixes and QoL moat-digging reachability fix installed: " +
                    $"modeMethod={modeResolution.Method}, cursorMethod={cursorResolution.Method}, " +
                    $"lookupMethod={moatLookupResolution.Method}, searchMethod={moatSearchResolution.Method}, " +
                    $"modeRva=0x{modeRva:X}, targetXRva=0x{targetXRva:X}, " +
                    $"targetYRva=0x{targetYRva:X}, hookRva=0x{hookRva:X}, " +
                    $"lookupRva=0x{moatLookupResolution.Rva:X}, searchRva=0x{moatSearchResolution.Rva:X}, " +
                    $"command6PrecheckRva=0x{Command6PrecheckHookRva:X}, " +
                    $"postShorteningRva=0x{MoatPostShorteningHookRva:X}, " +
                    $"movementResultRva=0x{MoatMovementResultHookRva:X}, " +
                    $"pathStartRva=0x{MoatPathStartHookRva:X}, " +
                    $"bfsResultRva=0x{MoatBfsResultHookRva:X}, " +
                    $"pathBuilderResultRva=0x{MoatPathBuilderResultHookRva:X}.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            GameTimeManagerAPI.Instance.OnTick -= ObservePendingAttempts;
            findNearestFriendlyMoatDetour?.Dispose();
            findNearestFriendlyMoatDetour = null;
            originalFindNearestFriendlyMoat = null;
            rootedFindNearestFriendlyMoat = null;
            getMoatIdAtTile = null;
            transaction?.Unload();
            transaction?.Dispose();
        }

        private int DirectCommandedMoatTarget(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode)
        {
            try
            {
                if (IsEnabled && relationshipMode == 1 &&
                    GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                    unit != null &&
                    unit->r_AI_LastIssuedTribeCommand == (ushort)TribeAICommand.DigMoatTileId &&
                    TryGetFriendlyPlannedMoatId(
                        tileManager,
                        playerId,
                        unit->r_ContextTargetTileX,
                        unit->r_ContextTargetTileY,
                        out int commandedMoatId))
                {
                    byte reservationBefore = ReserveMoat(tileManager, commandedMoatId);
                    MoatAttempt attempt = RegisterAttempt(
                        playerId,
                        unitId,
                        unchecked((int)unit->r_UnitChimp),
                        unit->r_ContextTargetTileX,
                        unit->r_ContextTargetTileY,
                        commandedMoatId,
                        reservationBefore);
                    LogFunctional(
                        $"stage=selection attempt={attempt.Id} player={playerId} unit={unitId} " +
                        $"unitType={attempt.UnitType} " +
                        $"target=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                        $"moat={commandedMoatId} reservation={attempt.ReservationBefore}->" +
                        $"{unchecked((byte)(attempt.ReservationBefore + MoatReservationIncrement))}");

                    // Vanilla reserves every positive result before returning it. Mirroring the
                    // +20 here keeps its later success/release and failure/-20 paths symmetric.
                    return commandedMoatId;
                }
            }
            catch (Exception ex)
            {
                if (!directedTargetFailureLogged)
                {
                    directedTargetFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL directed moat selection failed once; Vanilla selection remains active: {ex}");
                }
            }

            return originalFindNearestFriendlyMoat(tileManager, playerId, unitId, relationshipMode);
        }

        private void PreserveFriendlyPlannedMoatCommandTarget(
            NativePointer<X64SmartCPUContext> context)
        {
            if (!IsEnabled)
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int tribeId = unchecked((int)(uint)registers->R13);
            int targetX = unchecked((int)(uint)registers->R14);
            int targetY = unchecked((int)(uint)registers->R9);

            try
            {
                ulong unitOffset = registers->RDX;
                if (unitOffset % GameUnitStride != 0)
                    return;

                ulong representativeUnitValue = unitOffset / GameUnitStride;
                if (representativeUnitValue == 0 || representativeUnitValue > int.MaxValue)
                    return;

                int representativeUnitId = (int)representativeUnitValue;
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        representativeUnitId,
                        out GameUnit* representativeUnit) ||
                    representativeUnit == null)
                {
                    return;
                }

                int playerId = representativeUnit->r_ControllableForPlayerId;
                if (!TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    playerId,
                    targetX,
                    targetY,
                    out int moatId))
                {
                    return;
                }

                // The overwritten Vanilla test/branch follows this callback. Clearing R8
                // selects its existing direct path, so the ordinary reachability search at
                // 0xE7F60 cannot reject or replace the commanded moat coordinates.
                registers->R8 = 0;
                LogFunctional(
                    $"stage=direct-command tribe={tribeId} player={playerId} " +
                    $"representativeUnit={representativeUnitId} target=({targetX},{targetY}) moat={moatId}");
            }
            catch (Exception ex)
            {
                if (!commandPrecheckFailureLogged)
                {
                    commandPrecheckFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Bugfixes and QoL moat Command-6 validation failed once; " +
                        $"Vanilla command validation remains active: {ex}");
                }
            }
        }

        private void RecordMoatPathStart(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit) ||
                !AttemptMatchesNativeTarget(attempt) || attempt.PathStartRecorded)
            {
                return;
            }

            X64SmartCPUContext* registers = context.Pointer;
            int chosenStartX = unchecked((ushort)registers->R12);
            int chosenStartY = unchecked((ushort)registers->RBX);
            attempt.PathStartRecorded = true;
            attempt.ChosenStartX = chosenStartX;
            attempt.ChosenStartY = chosenStartY;

            LogFunctional(
                $"stage=path-start attempt={attempt.Id} unit={attempt.UnitId} " +
                $"chosen=({chosenStartX},{chosenStartY}) " +
                $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                $"next=({unit->r_NextTilePositionX2},{unit->r_NextTilePositionY2}) " +
                $"previous=({unit->r_PreviousTilePositionX},{unit->r_PreviousTilePositionY}) " +
                $"secondaryTarget=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}) " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} " +
                $"moving={unit->r_MovingRelevant} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} pathSize={unit->p_PathPlanSize}");
        }

        private void RecordMoatBfsResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit) ||
                !AttemptMatchesNativeTarget(attempt))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int startRegion = unchecked((int)(uint)registers->R12);
            int targetRegion = unchecked((int)(uint)registers->RBX);
            int originalBfsResult = unchecked((int)(uint)registers->RAX);
            int targetX = *moatMovementTargetX;
            int targetY = *moatMovementTargetY;
            TileDiagnostic start = GetTileDiagnostic(
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY);
            TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
            int effectiveBfsResult = originalBfsResult;
            bool bypassApplied = false;

            if (originalBfsResult == 0 && *moatPathMode == 1 &&
                startRegion >= 0 && targetRegion > 0 && startRegion != targetRegion &&
                target.TileId == attempt.TargetTileId &&
                TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    attempt.PlayerId,
                    targetX,
                    targetY,
                    out int currentMoatId) &&
                currentMoatId == attempt.MoatId)
            {
                // A real E7C40 success with the target region only feeds this value
                // into Vanilla's existing comparison. F4930 still validates and builds
                // the actual path; its return value is never modified by this feature.
                registers->RAX = unchecked((ulong)(uint)targetRegion);
                effectiveBfsResult = targetRegion;
                bypassApplied = true;
                attempt.BypassApplied = true;
            }

            LogFunctional(
                $"stage=bfs-result attempt={attempt.Id} unit={attempt.UnitId} " +
                $"unitType={attempt.UnitType} start=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) target=({targetX},{targetY}) " +
                $"startTile={start.TileId} startFlags=0x{start.Flags:X8} startGridRegion={start.Region} " +
                $"targetTile={target.TileId} targetFlags=0x{target.Flags:X8} targetGridRegion={target.Region} " +
                $"startRegion={startRegion} targetRegion={targetRegion} " +
                $"originalBfs={originalBfsResult} effectiveBfs={effectiveBfsResult} " +
                $"bypass={bypassApplied} pathMode={*moatPathMode}");
        }

        private void RecordMoatPathBuilderResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit) ||
                !AttemptMatchesNativeTarget(attempt))
            {
                return;
            }

            int pathBuilderResult = unchecked((int)(uint)context.Pointer->RAX);
            bool usedF4930 = unchecked((int)(uint)context.Pointer->R13) == 0;
            LogFunctional(
                $"stage=path-builder-result attempt={attempt.Id} unit={attempt.UnitId} " +
                $"target=({attempt.TargetX},{attempt.TargetY}) result={pathBuilderResult} " +
                $"builder={(usedF4930 ? "F4930" : "E32B0")} " +
                $"bypass={attempt.BypassApplied} pathMode={*moatPathMode} " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} pathSize={unit->p_PathPlanSize}");
        }

        private void RecordMoatPostShorteningState(
            NativePointer<X64SmartCPUContext> _)
        {
            if (!TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit) ||
                !AttemptMatchesNativeTarget(attempt) || !attempt.MovementRecorded ||
                attempt.PostShorteningRecorded)
            {
                return;
            }

            attempt.PostShorteningRecorded = true;
            int shortenedSize = GetBoundedPathSize(unit->p_PathPlanSize);
            int firstDirection = shortenedSize > 0
                ? GetPathDirection(attempt.UnitId, 0)
                : -1;
            int lastDirection = shortenedSize > 0
                ? GetPathDirection(attempt.UnitId, shortenedSize - 1)
                : -1;
            bool endpointFromPreviousValid = TryComputePathEndpoint(
                attempt.UnitId,
                unit->r_PreviousTilePositionX,
                unit->r_PreviousTilePositionY,
                shortenedSize,
                out int endpointFromPreviousX,
                out int endpointFromPreviousY);
            bool endpointFromCurrentValid = TryComputePathEndpoint(
                attempt.UnitId,
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY,
                shortenedSize,
                out int endpointFromCurrentX,
                out int endpointFromCurrentY);
            ushort deferredShortening = *(ushort*)((byte*)unit + 0x28C);

            LogFunctional(
                $"stage=post-shortening attempt={attempt.Id} unit={attempt.UnitId} " +
                $"committedSize={attempt.CommittedPathSize} shortenedSize={unit->p_PathPlanSize} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} " +
                $"firstDirection={firstDirection} lastDirection={lastDirection} " +
                $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                $"previous=({unit->r_PreviousTilePositionX},{unit->r_PreviousTilePositionY}) " +
                $"secondaryTarget=({unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2}) " +
                $"endpointFromPrevious={FormatEndpoint(endpointFromPreviousValid, endpointFromPreviousX, endpointFromPreviousY)} " +
                $"endpointFromCurrent={FormatEndpoint(endpointFromCurrentValid, endpointFromCurrentX, endpointFromCurrentY)} " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} moving={unit->r_MovingRelevant} " +
                $"linkage={unit->r_PathPlanRelated3} deferredShortening={deferredShortening}");
        }

        private void RecordMoatMovementResult(NativePointer<X64SmartCPUContext> context)
        {
            if (!TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit) ||
                !AttemptMatchesNativeTarget(attempt))
                return;

            X64SmartCPUContext* registers = context.Pointer;
            int moveResult = unchecked((int)(uint)registers->RAX);
            int targetX = *moatMovementTargetX;
            int targetY = *moatMovementTargetY;
            TileDiagnostic start = GetTileDiagnostic(
                unit->r_CurrentTilePositionX,
                unit->r_CurrentTilePositionY);
            TileDiagnostic target = GetTileDiagnostic(targetX, targetY);
            string reservation = TryGetMoatReservation(attempt.MoatId, out byte reservationValue)
                ? reservationValue.ToString()
                : "invalid";

            LogFunctional(
                $"stage=movement-result attempt={attempt.Id} unit={attempt.UnitId} " +
                $"start=({unit->r_CurrentTilePositionX}," +
                $"{unit->r_CurrentTilePositionY}) target=({targetX},{targetY}) " +
                $"storedTarget=({unit->r_ContextTargetTileX},{unit->r_ContextTargetTileY}) " +
                $"startTile={start.TileId} startFlags=0x{start.Flags:X8} startRegion={start.Region} " +
                $"targetTile={target.TileId} targetFlags=0x{target.Flags:X8} targetRegion={target.Region} " +
                $"moveResult={moveResult} pathMode={*moatPathMode} " +
                $"pathState=0x{unit->r_PathPlanStateBitFlags:X4} " +
                $"pathPosition={unit->p_CurrentPathPlanPosition} pathSize={unit->p_PathPlanSize} " +
                $"aiState={unit->r_AIState} moat={attempt.MoatId} reservation={reservation} " +
                $"bypass={attempt.BypassApplied}");

            attempt.MoveResult = moveResult;
            attempt.MovementRecorded = true;
            attempt.CommittedPathSize = unit->p_PathPlanSize;
            if (moveResult == 0)
                RemoveAttempt(attempt);
        }

        private int GetPathDirection(int unitId, int stepIndex)
        {
            if (unitId <= 0 || stepIndex < 0 || stepIndex >= MaximumPathSteps)
                return -1;

            byte packed = unitPathPlans[unitId * UnitPathPlanStride + (stepIndex >> 1)];
            return (stepIndex & 1) == 0 ? packed & 0x0F : packed >> 4;
        }

        private bool TryComputePathEndpoint(
            int unitId,
            int startX,
            int startY,
            int stepCount,
            out int endpointX,
            out int endpointY)
        {
            endpointX = startX;
            endpointY = startY;
            if (stepCount < 0 || stepCount > MaximumPathSteps)
                return false;

            for (int step = 0; step < stepCount; step++)
            {
                int direction = GetPathDirection(unitId, step);
                if ((uint)direction >= DirectionX.Length)
                    return false;
                endpointX += DirectionX[direction];
                endpointY += DirectionY[direction];
            }

            return true;
        }

        private static int GetBoundedPathSize(uint pathSize) =>
            pathSize <= MaximumPathSteps ? (int)pathSize : -1;

        private static string FormatEndpoint(bool valid, int x, int y) =>
            valid ? $"({x},{y})" : "invalid";

        private bool TryGetPendingAttempt(out MoatAttempt attempt, out GameUnit* unit)
        {
            attempt = null;
            unit = null;
            if (!IsEnabled)
                return false;

            int unitId = *currentUnitId;
            if (unitId <= 0)
                return false;

            lock (attemptLock)
            {
                if (!pendingAttempts.TryGetValue(unitId, out attempt))
                    return false;
            }

            return GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out unit) && unit != null;
        }

        private MoatAttempt RegisterAttempt(
            int playerId,
            int unitId,
            int unitType,
            int targetX,
            int targetY,
            int moatId,
            byte reservationBefore)
        {
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            lock (attemptLock)
            {
                MoatAttempt attempt = new MoatAttempt(
                    ++nextAttemptId,
                    playerId,
                    unitId,
                    unitType,
                    targetX,
                    targetY,
                    targetTileId,
                    moatId,
                    reservationBefore);
                pendingAttempts[unitId] = attempt;

                while (pendingAttempts.Count > MaximumPendingAttempts)
                {
                    int oldestUnitId = 0;
                    long oldestAttemptId = long.MaxValue;
                    foreach (KeyValuePair<int, MoatAttempt> pair in pendingAttempts)
                    {
                        if (pair.Value.Id < oldestAttemptId)
                        {
                            oldestAttemptId = pair.Value.Id;
                            oldestUnitId = pair.Key;
                        }
                    }

                    if (oldestAttemptId == long.MaxValue)
                        break;
                    pendingAttempts.Remove(oldestUnitId);
                }

                return attempt;
            }
        }

        private bool AttemptMatchesNativeTarget(MoatAttempt attempt) =>
            attempt != null &&
            *moatMovementTargetX == attempt.TargetX &&
            *moatMovementTargetY == attempt.TargetY;

        private void RemoveAttempt(MoatAttempt attempt)
        {
            lock (attemptLock)
            {
                if (pendingAttempts.TryGetValue(attempt.UnitId, out MoatAttempt current) &&
                    current.Id == attempt.Id)
                {
                    pendingAttempts.Remove(attempt.UnitId);
                }
            }
        }

        private void ObservePendingAttempts(int _)
        {
            if (!IsEnabled)
            {
                lock (attemptLock)
                    pendingAttempts.Clear();
                return;
            }

            try
            {
                List<MoatAttempt> attempts;
                lock (attemptLock)
                    attempts = new List<MoatAttempt>(pendingAttempts.Values);

                foreach (MoatAttempt attempt in attempts)
                {
                    if (!attempt.MovementRecorded)
                    {
                        bool expired = false;
                        lock (attemptLock)
                        {
                            if (pendingAttempts.TryGetValue(
                                    attempt.UnitId,
                                    out MoatAttempt current) &&
                                current.Id == attempt.Id)
                            {
                                attempt.ObservedTicks++;
                                expired = attempt.ObservedTicks >= MaximumAttemptObservationTicks;
                                if (expired)
                                    pendingAttempts.Remove(attempt.UnitId);
                            }
                        }

                        if (expired)
                        {
                            LogFunctional(
                                $"stage=observation-end attempt={attempt.Id} unit={attempt.UnitId} " +
                                $"reason=no-movement-result ticks={attempt.ObservedTicks} " +
                                $"pathStart={attempt.PathStartRecorded} postShortening={attempt.PostShorteningRecorded}");
                        }
                        continue;
                    }

                    if (attempt.MoveResult == 0)
                        continue;

                    if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                            attempt.UnitId,
                            out GameUnit* unit) ||
                        unit == null)
                    {
                        RemoveAttempt(attempt);
                        continue;
                    }

                    MovementSnapshot snapshot = new MovementSnapshot(unit);
                    bool shouldLog;
                    bool ended;
                    string endReason = null;
                    int observedTick;

                    lock (attemptLock)
                    {
                        if (!pendingAttempts.TryGetValue(
                                attempt.UnitId,
                                out MoatAttempt current) ||
                            current.Id != attempt.Id)
                        {
                            continue;
                        }

                        attempt.ObservedTicks++;
                        observedTick = attempt.ObservedTicks;
                        shouldLog = !attempt.HasLastSnapshot ||
                            !attempt.LastSnapshot.Equals(snapshot);
                        if (shouldLog)
                        {
                            attempt.LastSnapshot = snapshot;
                            attempt.HasLastSnapshot = true;
                        }

                        if (snapshot.AiState == DigMoatMovementState ||
                            snapshot.AiState == FillMoatMovementState)
                        {
                            attempt.SeenMoatMovementState = true;
                        }

                        ended = attempt.SeenMoatMovementState &&
                            snapshot.AiState != DigMoatMovementState &&
                            snapshot.AiState != FillMoatMovementState;
                        if (ended)
                            endReason = "state-left-moat-movement";
                        else if (attempt.ObservedTicks >= MaximumAttemptObservationTicks)
                        {
                            ended = true;
                            endReason = "tick-limit";
                        }

                        if (ended)
                            pendingAttempts.Remove(attempt.UnitId);
                    }

                    if (shouldLog)
                    {
                        LogFunctional(
                            $"stage=tick attempt={attempt.Id} tick={observedTick} unit={attempt.UnitId} " +
                            $"aiState={snapshot.AiState} " +
                            $"current=({snapshot.CurrentX},{snapshot.CurrentY}) " +
                            $"pathState=0x{snapshot.PathState:X4} moving={snapshot.Moving} " +
                            $"pathPosition={snapshot.PathPosition} pathSize={snapshot.PathSize}");
                    }

                    if (ended)
                    {
                        LogFunctional(
                            $"stage=observation-end attempt={attempt.Id} unit={attempt.UnitId} " +
                            $"reason={endReason} ticks={observedTick} " +
                            $"pathStart={attempt.PathStartRecorded} " +
                            $"postShortening={attempt.PostShorteningRecorded} " +
                            $"moveResult={attempt.MoveResult} seenMoatState={attempt.SeenMoatMovementState}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (tickObservationFailureLogged)
                    return;

                tickObservationFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat tick observation failed once; " +
                    $"functional moat behavior remains unchanged: {ex}");
            }
        }

        private TileDiagnostic GetTileDiagnostic(int tileX, int tileY)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return TileDiagnostic.Invalid;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId))
                return TileDiagnostic.Invalid;

            return new TileDiagnostic(
                tileId,
                unchecked((uint)tileApi.GetTilePropertyFlag(tileId)),
                pathRegions[tileId]);
        }

        private bool TryGetMoatReservation(int moatId, out byte reservation)
        {
            reservation = 0;
            IntPtr tileManager = GameTileManagerPointer;
            if (tileManager == IntPtr.Zero)
                return false;

            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (moatId <= 0 || moatId >= moatCount)
                return false;

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            reservation = moatRecord[MoatReservationOffset];
            return true;
        }

        private void LogFunctional(string details)
        {
            bool shouldLog;
            bool logLimit;
            lock (functionalLogLock)
            {
                shouldLog = functionalLogEntryCount < MaximumFunctionalLogEntries;
                if (shouldLog)
                    functionalLogEntryCount++;
                logLimit = !shouldLog && !functionalLogLimitLogged;
                if (logLimit)
                    functionalLogLimitLogged = true;
            }

            if (shouldLog)
            {
                Shared.DebugLogHelper.LogDebug(log, $"Bugfixes and QoL MoatCommand {details}.");
            }
            else if (logLimit)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Bugfixes and QoL MoatCommand reached its {MaximumFunctionalLogEntries}-entry limit; " +
                    "further moat-command logs are suppressed.");
            }
        }

        private static byte ReserveMoat(IntPtr tileManager, int moatId)
        {
            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            byte previous = moatRecord[MoatReservationOffset];
            moatRecord[MoatReservationOffset] =
                unchecked((byte)(previous + MoatReservationIncrement));
            return previous;
        }

        private void AllowFriendlyPlannedMoatCursor(NativePointer<X64SmartCPUContext> context)
        {
            X64SmartCPUContext* registers = context.Pointer;
            if (unchecked((uint)registers->RAX) != 0 || !IsEnabled || *digMoatMode == 0)
                return;

            try
            {
                int playerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                if (TryGetFriendlyPlannedMoatId(
                    GameTileManagerPointer,
                    playerId,
                    *targetTileX,
                    *targetTileY,
                    out _))
                    registers->RAX = 1;
            }
            catch (Exception ex)
            {
                if (cursorFailureLogged)
                    return;

                cursorFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL moat cursor validation failed once; Vanilla cursor behavior remains active: {ex}");
            }
        }

        private bool IsEnabled =>
            !disposed && settings.EnableMod && settings.EnableMoatDiggingReachabilityFix;

        private IntPtr GameTileManagerPointer =>
            (IntPtr)GameGlobalsManager.Instance.GameTileManagerVA;

        private bool TryGetFriendlyPlannedMoatId(
            IntPtr tileManager,
            int playerId,
            int tileX,
            int tileY,
            out int moatId)
        {
            moatId = 0;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(playerId))
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int tileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(tileId) ||
                !tileApi.HasTilePropertyFlag(tileId, TilePropertyFlag.PlannedMoat))
            {
                return false;
            }

            moatId = getMoatIdAtTile(tileManager, tileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            // Record zero is Vanilla's dummy/sentinel. Its own owner lookup rejects it,
            // and its nearest-moat search only reserves strictly positive results.
            if (moatId <= 0 || moatId >= moatCount)
            {
                moatId = 0;
                return false;
            }

            byte* moatRecord = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            int moatOwnerId = moatRecord[MoatOwnerOffset];
            if (!playerApi.IsPlayerIdValid(moatOwnerId))
            {
                moatId = 0;
                return false;
            }

            return moatOwnerId == playerId || playerApi.IsPlayerAlliedTo(playerId, moatOwnerId);
        }

        private static int ResolveGlobalRva(
            ReadOnlySpan<byte> memory,
            int displacementRva,
            int nextInstructionRva,
            string label)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                displacementRva,
                nextInstructionRva);
            if (resolvedRva < 0 || resolvedRva > memory.Length - sizeof(int))
                throw new InvalidOperationException($"The native {label} global is outside CrusaderDE.dll.");
            return resolvedRva;
        }

        private static void ValidateCursorHookSpan(ReadOnlySpan<byte> memory, int hookRva)
        {
            byte[] expected =
            {
                0x85, 0xC0, 0x74, 0x11,
                0x44, 0x8B, 0xBC, 0x24, 0xC0, 0x00, 0x00, 0x00,
                0x44, 0x8D, 0x6B, 0x02
            };
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException("The native DigMoat cursor hook span did not match the validated instructions.");
            }
        }

        private static void ValidateCommand6PrecheckHookSpan(ReadOnlySpan<byte> memory)
        {
            ValidateHookSpan(
                memory,
                Command6PrecheckHookRva,
                new byte[]
                {
                    0x45, 0x85, 0xC0,
                    0x74, 0x54,
                    0x0F, 0xBF, 0x8C, 0x1A, 0x1E, 0x07, 0x00, 0x00,
                    0x0F, 0xBF, 0x84, 0x1A, 0x1C, 0x07, 0x00, 0x00
                },
                "Command-6 precheck");
        }

        private static void ValidateDiagnosticHookSpans(ReadOnlySpan<byte> memory)
        {
            ValidateHookSpan(
                memory,
                MoatPostShorteningHookRva,
                new byte[]
                {
                    0x0F, 0xB7, 0x05, 0x20, 0x84, 0xF5, 0x05,
                    0x48, 0x69, 0xCB, 0x90, 0x04, 0x00, 0x00
                },
                "Moat post-shortening state");
            ValidateHookSpan(
                memory,
                MoatMovementResultHookRva,
                new byte[]
                {
                    0x85, 0xC0,
                    0x74, 0x5E,
                    0x48, 0x63, 0x1D, 0x10, 0x0B, 0x7F, 0x00,
                    0x44, 0x8B, 0xC5
                },
                "Moat movement result");
            ValidateHookSpan(
                memory,
                MoatPathStartHookRva,
                new byte[]
                {
                    0x8B, 0x0D, 0x8C, 0x73, 0xF1, 0x05,
                    0x85, 0xC0,
                    0x41, 0xBB, 0x01, 0x00, 0x00, 0x00,
                    0x45, 0x0F, 0xBF, 0xC4
                },
                "Moat path start");
            ValidateHookSpan(
                memory,
                MoatBfsResultHookRva,
                new byte[]
                {
                    0x44, 0x8B, 0xE0,
                    0x3B, 0xC3,
                    0x0F, 0x84, 0xA4, 0x00, 0x00, 0x00,
                    0x0F, 0xBF, 0x8F, 0xB8, 0x09, 0x00, 0x00
                },
                "Moat BFS result");
            ValidateHookSpan(
                memory,
                MoatPathBuilderResultHookRva,
                new byte[]
                {
                    0x45, 0x33, 0xC0,
                    0x44, 0x89, 0x05, 0x58, 0x70, 0xF1, 0x05,
                    0x85, 0xC0,
                    0x0F, 0x8E, 0xA4, 0x00, 0x00, 0x00
                },
                "Moat path-builder result");
        }

        private static void ValidateHookSpan(
            ReadOnlySpan<byte> memory,
            int hookRva,
            byte[] expected,
            string label)
        {
            if (hookRva < 0 || hookRva > memory.Length - expected.Length ||
                !memory.Slice(hookRva, expected.Length).SequenceEqual(expected))
            {
                throw new InvalidOperationException(
                    $"The native {label} hook span did not match the validated instructions.");
            }
        }

        private readonly struct TileDiagnostic
        {
            public static readonly TileDiagnostic Invalid = new TileDiagnostic(-1, 0, -1);

            public TileDiagnostic(int tileId, uint flags, int region)
            {
                TileId = tileId;
                Flags = flags;
                Region = region;
            }

            public int TileId { get; }
            public uint Flags { get; }
            public int Region { get; }
        }

        private sealed class MoatAttempt
        {
            public MoatAttempt(
                long id,
                int playerId,
                int unitId,
                int unitType,
                int targetX,
                int targetY,
                int targetTileId,
                int moatId,
                byte reservationBefore)
            {
                Id = id;
                PlayerId = playerId;
                UnitId = unitId;
                UnitType = unitType;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                MoatId = moatId;
                ReservationBefore = reservationBefore;
            }

            public long Id { get; }
            public int PlayerId { get; }
            public int UnitId { get; }
            public int UnitType { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public int MoatId { get; }
            public byte ReservationBefore { get; }
            public bool BypassApplied { get; set; }
            public bool PathStartRecorded { get; set; }
            public int ChosenStartX { get; set; }
            public int ChosenStartY { get; set; }
            public bool MovementRecorded { get; set; }
            public int MoveResult { get; set; }
            public uint CommittedPathSize { get; set; }
            public bool PostShorteningRecorded { get; set; }
            public int ObservedTicks { get; set; }
            public bool SeenMoatMovementState { get; set; }
            public bool HasLastSnapshot { get; set; }
            public MovementSnapshot LastSnapshot { get; set; }
        }

        private readonly struct MovementSnapshot : IEquatable<MovementSnapshot>
        {
            public MovementSnapshot(GameUnit* unit)
            {
                AiState = unit->r_AIState;
                CurrentX = unit->r_CurrentTilePositionX;
                CurrentY = unit->r_CurrentTilePositionY;
                PathState = unit->r_PathPlanStateBitFlags;
                Moving = unit->r_MovingRelevant;
                PathPosition = unit->p_CurrentPathPlanPosition;
                PathSize = unit->p_PathPlanSize;
            }

            public ushort AiState { get; }
            public ushort CurrentX { get; }
            public ushort CurrentY { get; }
            public ushort PathState { get; }
            public ushort Moving { get; }
            public ushort PathPosition { get; }
            public uint PathSize { get; }

            public bool Equals(MovementSnapshot other) =>
                AiState == other.AiState &&
                CurrentX == other.CurrentX &&
                CurrentY == other.CurrentY &&
                PathState == other.PathState &&
                Moving == other.Moving &&
                PathPosition == other.PathPosition &&
                PathSize == other.PathSize;
        }

    }
}
